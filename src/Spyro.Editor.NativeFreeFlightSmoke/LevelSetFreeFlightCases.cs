using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

/// <summary>
/// Bounded CPU oracle for Fly Anywhere in a chosen set of levels: the entry consumers and the permission
/// rule decide by the presence of animation slot 33, and the level-set loader's classification fills that
/// slot only for the chosen levels. Native flight courses and the global windows are unchanged.
/// </summary>
internal static class LevelSetFreeFlightCases
{
    private const uint RootTablePointer = 0x80076378;
    private const uint RootTable = 0x801A0000;
    private const uint Slot33 = RootTable + 0x38 + 33 * 4;

    internal static int Run()
    {
        int checks = 0;
        int[][] sets = [[10], [23], [10, 11, 12, 13, 14], [60, 61, 62, 63], [65], Enumerable.Range(10, 55).Where(NativeFreeFlightCompositionPlan.IsNormalLevel).ToArray()];
        var global = NativeFreeFlightPatchBuilder.BuildGlobalNormal();
        foreach (int[] set in sets)
        {
            var windows = NativeFreeFlightPatchBuilder.BuildLevelSet(set);
            Check(windows.Count == 12, "same bounded instruction ownership");
            Check(windows.All(w => w.Before.Length == w.After.Length), "no code growth");
            Check(windows.Select(w => w.Address).Order().SequenceEqual(global.Select(w => w.Address).Order()), "same window owners as the global plan");
            foreach (var window in windows.Where(w => w.Name.StartsWith("height-", StringComparison.Ordinal) || w.Name == "pitch-ceiling"))
                Check(window.After.SequenceEqual(global.Single(g => g.Name == window.Name).After), "ceiling and pitch windows are the global ones");

            foreach (int level in Enumerable.Range(10, 56))
            foreach (int classification in new[] { 0, 1, -1, int.MinValue })
            foreach (bool present in new[] { false, true })
            {
                var memory = Memory(level, classification, present);
                foreach (var window in windows.Where(w => w.Name.StartsWith("enter-", StringComparison.Ordinal)))
                {
                    var before = Machine.Run(window.Address, window.Before, memory, Registers());
                    var after = Machine.Run(window.Address, window.After, memory, Registers());
                    bool flying = classification != 0 || present;
                    if (window.Name == "enter-cross")
                        Check(after.Exit == (flying ? 0x80041FACu : 0x80041FE0u), "Cross entry follows slot 33 after the native course term");
                    else
                        Check(after.Exit == 0x80043FC0 && after.Registers[4] == (flying ? 32 : 15), "entry selects flight only with slot 33 present");
                    Check(after.Writes.Count == 0, "entry no writes");
                    Same(before, after, [2, 3, 4]);
                    if (classification != 0)
                        Check(before.Exit == after.Exit && (window.Name == "enter-cross" || before.Registers[4] == after.Registers[4]), "flight courses keep native entry");
                    if (level == 64 && present)
                        Check(before.Exit == after.Exit && (window.Name == "enter-cross" || before.Registers[4] == after.Registers[4]), "Gnasty's Loot keeps native entry");
                }
                foreach (int gameState in new[] { 0, 1, 2, 13, -1, int.MinValue })
                foreach (int held in new[] { 0, 1 })
                {
                    memory[0x800757D8] = gameState; memory[0x80078CA4] = held;
                    var permission = windows.Single(w => w.Name == "permission");
                    int[] registers = Registers(); registers[3] = gameState;
                    var before = Machine.Run(permission.Address, permission.Before, memory, registers);
                    var after = Machine.Run(permission.Address, permission.After, memory, registers);
                    Check(before.Exit == 0x80044070 && after.Exit == 0x80044070, "both versions leave at the state dispatch");
                    int expected = classification != 0 || !present ? held : gameState == 0 ? 1 : 0;
                    Check(after.Memory[0x80078CA4] == expected, "permission: slot 33 present grants in normal gameplay and withdraws outside it");
                    if (classification != 0 || !present)
                        Check(after.Writes.Count == 0, "flight courses and unlisted levels keep their permission untouched");
                    Check(after.Registers[19] == before.Registers[19] && after.Registers[19] == unchecked(registers[2] + registers[4]), "the displaced addu keeps its inputs and result");
                    Same(before, after, [1, 2, 3]);
                    Check(after.Writes.All(a => a == 0x80078CA4), "only native permission field");
                }
            }

            // The loader's classification block: t1 = 0 (fill resident slots) exactly for the chosen levels and native64.
            var loader = windows.Single(w => w.Name == "level-set-resident-airborne-loader");
            uint[] words = Enumerable.Range(0, loader.After.Length / 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(loader.After.AsSpan(i * 4, 4))).ToArray();
            int start = Array.IndexOf(words, 0x2523FFF6u); // addiu v1, t1, -10
            int end = Array.IndexOf(words, 0x2407FFFFu);   // li a3, -1 at "classified"
            Check(start > 0 && end > start, "classification block located");
            byte[] block = loader.After[(start * 4)..(end * 4)];
            foreach (int level in Enumerable.Range(-5, 120))
            {
                int[] registers = Registers(); registers[9] = level;
                var result = Machine.Run(loader.Address + (uint)start * 4, block, new Dictionary<uint, int>(), registers);
                bool expectedFill = set.Contains(level) || level == 64 || level == 65;
                Check(result.Exit == loader.Address + (uint)end * 4 && result.Writes.Count == 0, "classification falls through without memory access");
                Check(result.Registers[9] == (expectedFill ? 0 : 1), $"loader fills slots exactly for the chosen levels (level {level})");
            }
            Check(NativeFreeFlightAirborneLoader.UsedBytes <= NativeFreeFlightAirborneLoader.ByteLength, "level-set loader fits its owner");

            // The slot loop: from "classified" (li a3,-1) to the loop's last delay slot (addiu a1,a1,4 after the
            // back branch). Slots 33..35 are cleared in an unlisted level even when they hold the valid resident
            // pointer a listed level left, and filled from resident 16 in a listed level when invalid or absent.
            int loopEnd = Array.IndexOf(words, 0x24A50004u, end); // addiu a1, a1, 4
            Check(loopEnd > end, "slot loop located");
            byte[] loop = loader.After[(end * 4)..((loopEnd + 1) * 4)];
            const uint root = 0x801A0000, resident16 = 0x801B2C00, staleLocal = 0x80110000, validLocal = 0x801B4000, boundary = 0x801B0000;
            foreach (int t1 in new[] { 0, 1 })
            foreach (int heldSlot33 in new[] { 0, -1, unchecked((int)staleLocal), unchecked((int)resident16), unchecked((int)validLocal) })
            {
                Dictionary<uint, int> memory = new();
                for (int slot = 0; slot < 46; slot++)
                    memory[root + 0x38 + (uint)slot * 4] = slot is 33 or 34 or 35 ? heldSlot33 : unchecked((int)validLocal);
                memory[root + 0x78] = unchecked((int)resident16); // slot 16: the resident airborne record
                int[] registers = Registers(); registers[9] = t1; registers[12] = unchecked((int)root); registers[8] = unchecked((int)boundary);
                var result = Machine.Run(loader.Address + (uint)end * 4, loop, memory, registers, maxSteps: 2000);
                Check(result.Exit == loader.Address + (uint)(loopEnd + 1) * 4, "slot loop runs to its end");
                int expected = t1 != 0 ? 0 : heldSlot33 == unchecked((int)resident16) || heldSlot33 == unchecked((int)validLocal) ? heldSlot33 : unchecked((int)resident16);
                Check(result.Memory[root + 0x38 + 33 * 4] == expected && result.Memory[root + 0x38 + 35 * 4] == expected,
                    $"flight slots after the loop (fill={t1 == 0}, held={heldSlot33:X8}) should be {expected:X8}, got {result.Memory[root + 0x38 + 33 * 4]:X8}/{result.Memory[root + 0x38 + 35 * 4]:X8}; exit {result.Exit:X8} steps budget ok");
                Check(result.Memory[root + 0x38] == unchecked((int)validLocal) && result.Memory[root + 0x38 + 32 * 4] == unchecked((int)validLocal), "other valid slots are kept");
            }
        }
        try { _ = NativeFreeFlightPatchBuilder.BuildLevelSet([15]); throw new Exception("A flight course was accepted."); }
        catch (ArgumentException) { checks++; }
        try { _ = NativeFreeFlightPatchBuilder.BuildLevelSet([]); throw new Exception("An empty set was accepted."); }
        catch (ArgumentException) { checks++; }
        return checks;

        void Check(bool value, string label)
        { if (!value) throw new InvalidDataException("LEVEL-SET FLIGHT FAILED: " + label); checks++; }
        void Same(Machine.Result before, Machine.Result after, int[] allowed) => Check(
            Enumerable.Range(0, 32).All(r => allowed.Contains(r) || before.Registers[r] == after.Registers[r]),
            "unowned registers preserved");
    }

    private static Dictionary<uint, int> Memory(int level, int classifier, bool slot33Present) => new()
    {
        [0x8007596C] = level, [0x80075690] = classifier, [0x800757D8] = 0, [0x80078AF4] = 0, [0x80078CA4] = 0,
        [0x80078AD4] = 0, [0x80078A60] = 0, [0x80078BB0] = 0, [0x80078B78] = 0,
        [RootTablePointer] = unchecked((int)RootTable), [Slot33] = slot33Present ? unchecked((int)0x801B2C00) : 0
    };
    private static int[] Registers() => Enumerable.Range(0, 32)
        .Select(i => i == 0 ? 0 : unchecked((int)(0x51A00000u + (uint)i * 0x1001))).ToArray();
}
