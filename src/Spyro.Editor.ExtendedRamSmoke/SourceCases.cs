using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

/// <summary>On a copy of the original USA image (read only): the patch in the real executable, every retail level against
/// both layouts, and Tree Tops (4.8 KB to spare) with enemies from Beast Makers and Lofty Castle: refused with 2 MB,
/// composed with 8 MB, and the export step then names it as needing 8 MB.</summary>
internal static class SourceCases
{
    internal static void Run(Action<bool, string> check, string image, string workspace)
    {
        UniversalSourceLibrary.RequireOriginal(image);
        var catalog = LevelCatalog.Load(workspace);
        var lib = new UniversalSourceLibrary(image, catalog);
        byte[] original = lib.Disc.Exe.ToArray(), exe = original.ToArray();
        NativeExtendedRam.PatchExe(exe);
        int Off(uint a) => (int)(a - UniversalExeBindings.ExeBase);
        uint Word(byte[] e, uint a) => BinaryPrimitives.ReadUInt32LittleEndian(e.AsSpan(Off(a)));
        var changed = Enumerable.Range(0, exe.Length).Where(i => exe[i] != original[i]).ToArray();
        bool Allowed(int i) => i >= Off(NativeExtendedRam.Cave) && i < Off(NativeExtendedRam.CaveEnd) ||
            i >= Off(NativeExtendedRam.InGameHook) && i < Off(NativeExtendedRam.InGameHook) + 4 ||
            i >= Off(NativeExtendedRam.StartupHook) && i < Off(NativeExtendedRam.StartupHook) + 8;
        check(NativeExtendedRam.IsInstalled(exe) && changed.Length > 0 && changed.All(Allowed),
            $"installed in the USA executable: only the two hooks and the unused routine change ({changed.Length} bytes)");
        check(Word(exe, 0x80015B50) == 0x0C01758A && Word(exe, 0x80015B54) == 0x24630001 && Word(exe, 0x800149B4) == 0x0C017595 && Word(exe, 0x800149B8) == 0,
            "jal 0x8005D628 at the in-game sum (its delay slot kept), jal 0x8005D654 at the start-up sum with a nop delay slot");
        check(exe.AsSpan(Off(NativeExtendedRam.Cave), 0x90).SequenceEqual(NativeExtendedRam.CaveImage()), "the routine holds the two checks");
        var once = exe.ToArray(); NativeExtendedRam.PatchExe(exe);
        check(exe.SequenceEqual(once), "installing again changes nothing");

        // Every retail level fits both layouts as shipped (Twilight Harbor already ends 324 bytes inside the margin).
        var levels = catalog.Levels.Where(l => l.SourceWadEntry > 0).ToArray();
        var survey = NativeExtendedRam.Survey(exe, lib.Disc.EntryBytes, levels, lib.Disc);
        check(survey.Count == 35 && survey.All(f => f.FitsPlayStation && f.FitsExtended), $"all {survey.Count} retail levels fit 2 MB and the 8 MB layout");
        var twilight = survey.Single(f => f.LevelId == 62);
        check(twilight.RuntimeEnd > GameRam.LowLimit && twilight.FitsPlayStation, "Twilight Harbor, past the margin as shipped, counts as fitting (it is the game's own)");
        check(survey.Max(f => f.SceneBytes) <= 0x30000 && survey.All(f => f.LowEnd < f.RuntimeEnd), "retail scenes are at most 192 KB; each frees its bytes below the buffers with 8 MB");

        var tree = catalog.FindByKey("treetops")!; var host = lib.Get("treetops");
        var spawn = host.Data.Row(0);
        int x = BinaryPrimitives.ReadInt32LittleEndian(spawn.AsSpan(0x0C)), y = BinaryPrimitives.ReadInt32LittleEndian(spawn.AsSpan(0x10)), z = BinaryPrimitives.ReadInt32LittleEndian(spawn.AsSpan(0x14));
        UniversalMobyPlacement[] Enemies(params (string Key, int Row)[] rows) => [.. rows.Select((r, i) =>
            new UniversalMobyPlacement(r.Key, r.Row, x + 400 * (i + 1), y, z) { EditorId = "eight-mb-" + i, PersistentSlot = i })];
        UniversalComposeResult Compose(GameMemoryMode mode, UniversalMobyPlacement[] placements)
        {
            using var scope = GameRam.Use(mode);
            return UniversalMobyComposer.Compose(tree, host.Data.Bytes, host.Overlay.Bytes.ToArray(), lib.Disc.Exe, lib.Get, lib.SharedClasses,
                UniversalMobyExport.DefaultProtectedVram(image, tree), placements, sharedModelPackage: lib.SharedModelPackage,
                playerAnimationSource: lib.PlayerAnimations, nativeSharedEntry8: lib.Disc.EntryBytes(8));
        }
        var enemies = Enemies(("beastmakers", 2), ("beastmakers", 2), ("loftycastle", 15), ("loftycastle", 15));
        string refusal = "";
        try { Compose(GameMemoryMode.PlayStation2MB, enemies); }
        catch (UniversalBudgetException e) { refusal = e.Message; }
        check(refusal.StartsWith("Tree Tops does not have enough game RAM for these objects", StringComparison.Ordinal),
            "2 MB: Tree Tops with two Beast Makers and two Lofty Castle enemies is refused: " + refusal);
        var composed = Compose(GameMemoryMode.Emulator8MB, enemies);
        var data = new UniversalLevelData(composed.LevelData);
        uint copy = new UniversalOverlay(composed.Overlay, UniversalExeBindings.Read(composed.Executable, 43)).CopyBuffer;
        check(composed.Ram.ExtendedRam && composed.Ram.FinalHeadroom > 0x400 && composed.Ram.SceneHeadroom > 0x100000,
            $"8 MB: composed, {composed.Ram.FinalHeadroom:N0} bytes left below the draw buffers and {composed.Ram.SceneHeadroom:N0} for the scene");
        check(GameRam.FitsExtended(data, copy) && !GameRam.FitsPlayStation(data, copy), "the composed level fits the 8 MB layout and not 2 MB");
        check(composed.ArenaSlotsRemaining >= UniversalMobyComposer.RetailDynamicSlots && composed.NewRowIndices.Count == 4,
            $"its spare object slots are back at the retail reserve ({composed.ArenaSlotsRemaining}) and the four enemies are placed");

        // The export step: patch, survey, and the sentence naming Tree Tops.
        var replaced = new Dictionary<int, byte[]> { [tree.SourceWadEntry] = composed.LevelData, [tree.SourceWadEntry - 1] = composed.Overlay };
        byte[] final = composed.Executable.ToArray();
        using (var scope = GameRam.Use(GameMemoryMode.Emulator8MB))
        {
            var fits = NativeExtendedRam.ApplyForExport(final, e => replaced.TryGetValue(e, out var b) ? b : lib.Disc.EntryBytes(e), levels, lib.Disc);
            check(NativeExtendedRam.IsInstalled(final) && fits.Count(f => !f.FitsPlayStation) == 1 && scope.LevelsNeedingExtendedRam.SequenceEqual(["Tree Tops"]),
                "the export step installs the patch and names Tree Tops as needing 8 MB");
            check(NativeExtendedRam.ExportNote(scope).Contains("Tree Tops does not fit 2 MB"), "Create BIN's sentence: " + NativeExtendedRam.ExportNote(scope));
        }
        using (GameRam.Use(GameMemoryMode.PlayStation2MB))
        {
            byte[] plain = composed.Executable.ToArray();
            check(NativeExtendedRam.ApplyForExport(plain, lib.Disc.EntryBytes, levels, lib.Disc).Count == 0 && plain.SequenceEqual(composed.Executable),
                "with 2 MB the export step leaves the executable byte for byte");
        }
        // Models never move: a 107 KB model is refused even with 8 MB, in words that say why.
        string big = "";
        try { Compose(GameMemoryMode.Emulator8MB, Enemies(("blowhard", 11))); }
        catch (UniversalBudgetException e) { big = e.Message; }
        check(big.Contains("does not have enough game RAM") && big.Contains("below the game's draw buffers"), "8 MB: too many models are still refused: " + big);
    }
}
