using System.Buffers.Binary;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreeFlightSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreeFlightWindow(string Name, uint Address, byte[] Before,
    byte[] After, uint SuccessAddress = 0)
{
    internal int Offset => checked((int)(Address - 0x8000F800));
}

/// <summary>
/// In-place, level-scoped instruction scheduling. No allocator, code cave,
/// invented RAM field, flight-level classification or GP assumption.
/// </summary>
internal static class NativeFreeFlightPatchBuilder
{
    internal static IReadOnlyList<NativeFreeFlightWindow> Build(int targetLevelId)
    {
        if (targetLevelId < 10 || targetLevelId > 64 || targetLevelId % 10 > 4)
            throw new ArgumentException("Free Flight currently supports a retail normal destination only.", nameof(targetLevelId));

        List<NativeFreeFlightWindow> result = [];
        Add("permission", 0x80044040,
            "0780033c6c59638c4000022408006214000000000880023cf48a428c0000000003004014010002240880013ca48c22ac",
            [Lui(1, 0x8007), Lw(3, 1, 0x596C), Li(2, targetLevelId),
             Beq(3, 2, 3, 7), Lui(1, 0x8008), Li(2, 64), Bne(3, 2, 6, 12),
             Lw(2, 1, -0x750C), 0, Bne(2, 0, 9, 12), Li(2, 1), Sw(2, 1, -0x735C)]);

        Entry("enter-jump", 0x80041E9C,
            "0780023c9056428c0000000045084014200004240780033c6c59638c40000224400862140f000424f00f010820000424");
        Add("enter-cross", 0x80041F84,
            "0780023c9056428c0000000006004014400002240780033c6c59638c000000000e00621400000000",
            [Lui(3, 0x8007), Lw(2, 3, 0x5690), Lw(3, 3, 0x596C),
             BranchTo(5, 2, 0, 0x80041F90, 0x80041FAC), Li(2, 64),
             BranchTo(4, 3, 2, 0x80041F98, 0x80041FAC), Li(2, targetLevelId),
             BranchTo(5, 3, 2, 0x80041FA0, 0x80041FE0), 0, 0]);
        Entry("enter-fall", 0x800430B4,
            "0780023c9056428c00000000bf034014200004240780033c6c59638c40000224ba0362140f000424f00f010820000424");
        Entry("enter-recover", 0x800432D8,
            "0780023c9056428c0000000036034014200004240780033c6c59638c40000224310362140f000424f00f010820000424");
        Entry("enter-state", 0x800434DC,
            "0780023c9056428c00000000b5024014200004240780033c6c59638c40000224b00262140f000424f00f010820000424");

        Height("height-glide", 0x800429A0, 0x80043FC8, 0x800429E0, false,
            "0880023cd48a428c0000000086054014000000000880043c608a848c0880023cb08b428c0000000023108200010242280300401400000000");
        Height("height-flight", 0x80043844, 0x800438D8, 0x8004388C, false,
            "0880023cd48a428c0000000021004014000000000880043c608a848c0880023cb08b428c0000000023108200010242280500401400000000");
        Height("height-rise", 0x80043A88, 0x80043B14, 0x80043AD4, true,
            "0880043cd48a84240000828c000000001e004014000000000880053c608aa58c0880023cb08b428c000000002310a200010242280500401400000000");
        Height("height-dive", 0x80043CEC, 0x80043D78, 0x80043D38, true,
            "0880043cd48a84240000828c000000001e004014000000000880053c608aa58c0880023cb08b428c000000002310a200010242280500401400000000");

        // s5 is established at 80045670..74 as the speed field 80078B70;
        // pitch is s5+8. Reusing this native base frees room for the target
        // predicate. Compute native s2/s3 even for the selected target; only
        // v1 is additional scratch, killed by the LUI at 80045708. The lower
        // -800 clamp is retained for every destination.
        Add("pitch-ceiling", 0x80045690,
            "32015226839812000200610600000000219800000880023c788b428c000000002a10620203004010000000000880013c788b33ac0880023c788b428c00000000e0fc422803004010e0fc02240880013c788b22ac",
            [Addiu(18, 18, 306), Sra(19, 18, 2), Bgez(19, 2, 5), Lui(2, 0x8007),
             Addu(19, 0, 0), Lw(2, 2, 0x596C), Li(3, targetLevelId), Beq(2, 3, 7, 15),
             Lw(2, 21, 8), 0, Slt(2, 19, 2), Beq(2, 0, 11, 15), 0, Sw(19, 21, 8), 0,
             Lw(2, 21, 8), 0, Slti(2, 2, -800), Beq(2, 0, 18, 21), Li(2, -800), Sw(2, 21, 8)]);
        return result.AsReadOnly();

        void Entry(string name, uint address, string before)
        {
            // Keep the original flight-global OR level64 logic exactly, then
            // add only the selected target. In particular this does not use
            // permission as a substitute for native64 during respawn.
            Add(name, address, before,
                [Lui(3, 0x8007), Lw(2, 3, 0x5690), Lw(3, 3, 0x596C),
                 BranchTo(5, 2, 0, address + 12, 0x80043FC0), Li(4, 32), Li(2, 64),
                 BranchTo(4, 3, 2, address + 24, 0x80043FC0), Li(2, targetLevelId),
                 BranchTo(4, 3, 2, address + 32, 0x80043FC0), 0,
                 Jump(0x80043FC0), Li(4, 15)]);
        }

        void Height(string name, uint address, uint substateExit, uint success, bool pointer, string before)
        {
            uint[] code = pointer
                ? [Lui(4, 0x8008), Addiu(4, 4, -0x752C), Lw(2, 4, 0), Lui(3, 0x8007),
                   BranchTo(5, 2, 0, address + 16, substateExit), Lw(3, 3, 0x596C),
                   Lw(2, 4, 0xDC), Lw(5, 4, -0x74), Addiu(3, 3, -targetLevelId),
                   BranchTo(4, 3, 0, address + 36, success), Subu(2, 5, 2), Slti(2, 2, 513),
                   BranchTo(5, 2, 0, address + 48, success), 0, 0]
                : [Lui(2, 0x8008), Lw(2, 2, -0x752C), Lui(3, 0x8007),
                   BranchTo(5, 2, 0, address + 12, substateExit), Lw(3, 3, 0x596C),
                   Lui(4, 0x8008), Lw(2, 4, -0x7450), Lw(4, 4, -0x75A0), Addiu(3, 3, -targetLevelId),
                   BranchTo(4, 3, 0, address + 36, success), Subu(2, 4, 2), Slti(2, 2, 513),
                   BranchTo(5, 2, 0, address + 48, success), 0];
            Add(name, address, before, code, success);
        }

        void Add(string name, uint address, string before, uint[] instructions, uint success = 0)
        {
            byte[] original = Convert.FromHexString(before);
            byte[] after = new byte[instructions.Length * 4];
            for (int i = 0; i < instructions.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(i * 4, 4), instructions[i]);
            if (original.Length != after.Length)
                throw new InvalidDataException("Free Flight instruction window size changed: " + name);
            ExecutablePatchSafety.GuardPatchRange(address, after.Length, "Free Flight " + name);
            result.Add(new(name, address, original, after, success));
        }
    }

    // The original classifier sets g75690 only for native flight courses. A
    // zero value identifies a normal-loaded player, including an issued fresh
    // normal level. Native flight courses keep their own permission/ceilings.
    internal static IReadOnlyList<NativeFreeFlightWindow> BuildGlobalNormal()
    {
        List<NativeFreeFlightWindow> windows = [];
        foreach (var window in Build(14))
        {
            uint[] words = Enumerable.Range(0, window.After.Length / 4)
                .Select(i => BinaryPrimitives.ReadUInt32LittleEndian(window.After.AsSpan(i * 4, 4))).ToArray();
            if (window.Name == "permission")
            {
                windows.Add(BuildGlobalPermission(window));
                continue;
            }
            else if (window.Name == "enter-cross")
            {
                // The native flight/global and level64 branches run first.
                // All remaining classifier-normal entries can now fly.
                words[7] = BranchTo(5, 0, 0, window.Address + 28, 0x80041FE0);
            }
            else if (window.Name.StartsWith("enter-", StringComparison.Ordinal))
                words[8] = BranchTo(4, 0, 0, window.Address + 32, 0x80043FC0);
            else if (window.Name.StartsWith("height-", StringComparison.Ordinal))
            {
                bool pointer = window.Name is "height-rise" or "height-dive";
                words[pointer ? 5 : 4] = Lw(3, 3, 0x5690);
                words[8] = 0; // v1 now contains the native classifier itself.
            }
            else if (window.Name == "pitch-ceiling")
            {
                words[5] = Lw(2, 2, 0x5690);
                words[6] = Li(3, 0);
            }
            byte[] after = new byte[window.After.Length];
            for (int i = 0; i < words.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(i * 4, 4), words[i]);
            windows.Add(window with { After = after });
        }
        windows.Add(NativeFreeFlightAirborneLoader.Build());
        return windows.AsReadOnly();
    }

    /// <summary>
    /// Flight in a chosen set of normal levels. The level-set loader fills the resident airborne slots 33..35
    /// only for the chosen levels (and native64), so "slot 33 is present" is the per-level decision the
    /// movement code can read in two instructions, from the player's animation table at 80076378 + 0xBC:
    /// the five entry consumers grant the flight state when it is present (after the unchanged native
    /// flight-course term), and the permission rule grants the flag when it is present, in normal gameplay.
    /// The ceiling and pitch windows are the global ones: they only matter once a flight state exists.
    /// </summary>
    internal static IReadOnlyList<NativeFreeFlightWindow> BuildLevelSet(IReadOnlyCollection<int> levelIds)
    {
        ArgumentNullException.ThrowIfNull(levelIds);
        if (levelIds.Count == 0 || levelIds.Any(id => !NativeFreeFlightCompositionPlan.IsNormalLevel(id) && id != 65))
            throw new ArgumentException("Free Flight level sets hold retail normal levels (and the level of your own) only.", nameof(levelIds));
        const uint rootTable = 0x6378;   // player animation table pointer, 80076378
        const int slot33 = 0x38 + 33 * 4; // entry of animation slot 33 inside that table
        List<NativeFreeFlightWindow> windows = [];
        foreach (var window in BuildGlobalNormal())
        {
            if (window.Name == "normal-resident-airborne-loader")
                continue;
            uint[] words = Enumerable.Range(0, window.After.Length / 4)
                .Select(i => BinaryPrimitives.ReadUInt32LittleEndian(window.After.AsSpan(i * 4, 4))).ToArray();
            if (window.Name == "permission")
            {
                // v1 holds the game state on entry; the displaced addu keeps its place. Flight courses leave.
                // Then: slot 33 absent -> leave; present -> flag = (game state == 0). The native64 rule also
                // held the previous flag on the ground; here the flag is simply current in normal gameplay.
                words =
                [
                    Lui(1, 0x8007), 0x00449821 /* addu s3, v0, a0 */, Lw(2, 1, 0x5690), Lw(1, 1, (int)rootTable),
                    Bne(2, 0, 4, 14), Lw(2, 1, slot33), Lui(1, 0x8008), Beq(2, 0, 7, 14),
                    Sltiu(2, 3, 1), Sw(2, 1, -0x735C), 0, 0, 0, 0
                ];
            }
            else if (window.Name == "enter-cross")
            {
                words =
                [
                    Lui(3, 0x8007), Lw(2, 3, 0x5690), Lw(3, 3, (int)rootTable),
                    BranchTo(5, 2, 0, 0x80041F90, 0x80041FAC), Lw(2, 3, slot33), 0,
                    BranchTo(4, 2, 0, 0x80041F9C, 0x80041FE0), 0, 0, 0
                ];
            }
            else if (window.Name.StartsWith("enter-", StringComparison.Ordinal))
            {
                uint address = window.Address;
                words =
                [
                    Lui(3, 0x8007), Lw(2, 3, 0x5690), Lw(3, 3, (int)rootTable),
                    BranchTo(5, 2, 0, address + 12, 0x80043FC0), Li(4, 32), Lw(2, 3, slot33), 0,
                    BranchTo(5, 2, 0, address + 28, 0x80043FC0), 0, Jump(0x80043FC0), Li(4, 15), 0
                ];
            }
            byte[] after = new byte[window.After.Length];
            if (words.Length * 4 != after.Length)
                throw new InvalidDataException("Free Flight level-set window size changed: " + window.Name);
            for (int i = 0; i < words.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(i * 4, 4), words[i]);
            windows.Add(window with { After = after });
        }
        windows.Add(NativeFreeFlightAirborneLoader.Build(NativeFreeFlightAirborneLoader.LevelMask(levelIds.Append(64).Append(65).Distinct())));
        return windows.AsReadOnly();
    }

    /// <summary>
    /// The movement routine tests the game state just before the native permission rule and skips the
    /// rule outside normal gameplay, so the flag set in a level survives into the level change that
    /// follows. With the flag set the glide code draws the wing-trail effects by calling into the level
    /// overlay, and the scripted glide through a portal runs while the next level is being read over
    /// that overlay. The original game never has the flag set in a level that owns a portal glide.
    /// This window therefore starts two instructions earlier, at that game-state test, and withdraws
    /// the permission whenever the game is not in normal gameplay.
    ///   v1 = game state on entry (0 = normal gameplay); the displaced addu keeps its place and inputs.
    ///   Flight courses (classifier set) are left alone: their loader owns the flag.
    ///   Elsewhere (the classifier is 0 in Gnasty's Loot too): store (game state == 0) when airborne
    ///   or not in normal gameplay; on the ground in normal gameplay nothing is written.
    /// </summary>
    private static NativeFreeFlightWindow BuildGlobalPermission(NativeFreeFlightWindow native)
    {
        const uint address = 0x80044038;
        if (native.Address != address + 8)
            throw new InvalidDataException("Free Flight permission window moved.");
        byte[] before = [.. Convert.FromHexString("0d00601421984400"), .. native.Before];
        uint[] words = [Lui(1, 0x8007), 0x00449821 /* addu s3, v0, a0 */, Lw(2, 1, 0x5690), Lui(1, 0x8008),
            Bne(2, 0, 4, 14), Lw(2, 1, -0x750C), 0, Sltiu(2, 2, 1),
            Or(2, 2, 3), Beq(2, 0, 9, 14), Sltiu(2, 3, 1), Sw(2, 1, -0x735C), 0, 0];
        byte[] after = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(i * 4, 4), words[i]);
        if (before.Length != after.Length)
            throw new InvalidDataException("Free Flight instruction window size changed: permission");
        ExecutablePatchSafety.GuardPatchRange(address, after.Length, "Free Flight permission");
        return new("permission", address, before, after);
    }

    private static uint I(int op, int rs, int rt, int immediate) =>
        ((uint)op << 26) | ((uint)rs << 21) | ((uint)rt << 16) | unchecked((ushort)immediate);
    private static uint R(int rs, int rt, int rd, int shift, int fn) =>
        ((uint)rs << 21) | ((uint)rt << 16) | ((uint)rd << 11) | ((uint)shift << 6) | (uint)fn;
    private static uint Lui(int rt, int value) => I(15, 0, rt, value);
    private static uint Lw(int rt, int rs, int offset) => I(35, rs, rt, offset);
    private static uint Sw(int rt, int rs, int offset) => I(43, rs, rt, offset);
    private static uint Li(int rt, int value) => I(9, 0, rt, value);
    private static uint Addiu(int rt, int rs, int value) => I(9, rs, rt, value);
    private static uint Slti(int rt, int rs, int value) => I(10, rs, rt, value);
    private static uint Sltiu(int rt, int rs, int value) => I(11, rs, rt, value);
    private static uint Or(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x25);
    private static uint Sra(int rd, int rt, int shift) => R(0, rt, rd, shift, 3);
    private static uint Addu(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x21);
    private static uint Subu(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x23);
    private static uint Slt(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x2A);
    private static uint Beq(int rs, int rt, int index, int target) => I(4, rs, rt, target - index - 1);
    private static uint Bne(int rs, int rt, int index, int target) => I(5, rs, rt, target - index - 1);
    private static uint Bgez(int rs, int index, int target) => I(1, rs, 1, target - index - 1);
    private static uint Jump(uint target) => 0x08000000 | ((target >> 2) & 0x03FFFFFF);
    private static uint BranchTo(int op, int rs, int rt, uint address, uint target) =>
        I(op, rs, rt, checked((short)(((long)target - address - 4) / 4)));
}
