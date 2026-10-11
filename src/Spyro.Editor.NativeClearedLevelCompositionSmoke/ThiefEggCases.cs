using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

/// <summary>A rebuilt level's own egg thief carries its own dragon egg: the thief (0x21) and the egg it spawns (0x22)
/// keep their numbers and run the level's own code, and the thief keeps its original save identity (the bit the
/// game sets when the egg is collected and reads at load). Another level's thief is renumbered with its egg.</summary>
internal static class ThiefEggCases
{
    private const int Thief = LevelCompletionCheck.EggThiefClass, Egg = LevelCompletionCheck.DragonEggClass;

    internal static void Run(Action<bool, string> check)
    {
        // Town Square-like original rows: 88 thief, 10 statue, 11 dragon, 12 pedestal, 5 gem.
        int Original(int row) => row switch { 88 => Thief, 10 => 0xFA, 11 => 0x6E, 12 => 0x14B, _ => 0x53 };
        UniversalMobyPlacement P(string key, int row, int slot) => new(key, row, 0, 0, 0, EditorId: "e" + slot + key + row, PersistentSlot: slot);
        bool Same(IReadOnlySet<int>? set, params int[] expected) => set != null && set.Order().SequenceEqual(expected.Order());
        var thief = P("townsquare", 88, 0);
        check(Same(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [thief], Original), Thief, Egg), "own thief releases thief and egg");
        check(Same(NativeClearedLevelCompilation.ReleasedClasses("TownSquare", [thief], Original), Thief, Egg), "level key case ignored");
        check(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("stonehill", 88, 0)], Original) == null, "another level's thief keeps nothing");
        check(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [thief with { Retired = true }], Original) == null, "retired own thief keeps nothing");
        check(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("townsquare", 5, 0)], Original) == null, "own gem keeps nothing");
        check(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [thief with { SpecialId = "crash-tnt", SourceRowIndex = -1 }], Original) == null, "special object keeps nothing");
        check(Same(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("townsquare", 10, 0), P("townsquare", 11, 1)], Original), 0xFA, 0x6E, 0xFB), "own dragon pair unchanged");
        check(Same(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("townsquare", 10, 0)], Original), 0xFA, 0xFB), "own statue alone unchanged");
        check(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("townsquare", 11, 0)], Original) == null, "dragon without its statue unchanged");
        check(Same(NativeClearedLevelCompilation.ReleasedClasses("townsquare", [P("townsquare", 10, 0), thief with { PersistentSlot = 1 }], Original), 0xFA, 0xFB, Thief, Egg), "dragon and thief together");

        var kept = NativeClearedLevelCompilation.KeepOwnIdentities("townsquare", 107,
            [thief, P("townsquare", 88, 1), P("stonehill", 88, 2), P("townsquare", 5, 3), P("townsquare", 10, 4), P("townsquare", 12, 5) with { SaveIdentity = 200 }], Original);
        check(kept[0].SaveIdentity == 88, "own thief keeps its original save identity (its egg bit)");
        check(kept[1].SaveIdentity == null, "a second copy of the thief takes a new identity");
        check(kept[2].SaveIdentity == null, "another level's thief takes a new identity");
        check(kept[3].SaveIdentity == null, "own gem takes a new identity");
        check(kept[4].SaveIdentity == 10, "own statue keeps its identity");
        check(kept[5].SaveIdentity == 200, "an explicit identity is kept");
        check(NativeClearedLevelCompilation.KeepOwnIdentities("townsquare", 80, [thief], Original)[0].SaveIdentity == null, "rows past the prefix are not original identities");
        check(NativeClearedLevelCompilation.KeepOwnIdentities("townsquare", 107, [thief with { Retired = true }], Original)[0].SaveIdentity == null, "retired thief takes no identity");
    }

    /// <summary>Town Square rebuilt from scratch with its own thief (row 88), alone and next to Stone Hill's thief. The
    /// cleared host image it composes onto (a full disc image in <paramref name="artifacts"/>) is deleted afterwards.</summary>
    internal static async Task RunSource(string original, LevelCatalog catalog, UniversalSourceLibrary donors, string artifacts, Action<bool, string> check)
    {
        string host = Path.Combine(artifacts, "thief-host.bin");
        try { await Compose(original, catalog, donors, host, check); }
        finally { File.Delete(host); }
    }

    private static async Task Compose(string original, LevelCatalog catalog, UniversalSourceLibrary donors, string host, Action<bool, string> check)
    {
        var town = donors.Get("townsquare");
        int prefix = town.Data.Scene().RowCount;
        int ownRow = Enumerable.Range(0, prefix).Single(r => ClassOf(town.Data, r) == Thief);
        check(ownRow == 88, "Town Square's thief is row 88");
        var binding = new SceneTargetBinding(town.Level.Key, town.Level.LevelId, town.Level.SourceWadEntry, town.Level.SourceWadEntry - 1, UniversalSourceLibrary.OriginalUsaSha256, prefix);
        var document = SceneDocument.New("Blank Town Square") with { TargetBinding = binding };
        var cleared = await NativeClearedLevelCompilation.CaptureAsync(original, document, catalog, host);
        var stone = donors.Get("stonehill");
        int stoneRow = Enumerable.Range(0, stone.Data.Scene().RowCount).Single(r => ClassOf(stone.Data, r) == Thief);
        int x = 560 * 16, y = 560 * 16, z = 544 * 16;
        UniversalMobyPlacement Own(int slot) => new("townsquare", ownRow, x, y, z, EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: slot);
        UniversalMobyPlacement Other(int slot) => new("stonehill", stoneRow, x + 2048, y, z, EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: slot);

        var alone = cleared.Compose(donors, [Own(0)]);
        var data = new UniversalLevelData(alone.LevelData);
        check(ClassOf(data, alone.NewRowIndices[0]) == Thief, "own thief row keeps class 0x21");
        check(alone.PlacementSaveRows.SequenceEqual([88]) && alone.PhysicalToLogicalSaveRows.SequenceEqual([88]), "own thief keeps save identity 88");
        RequireOwnEgg(alone, check, "alone");
        check(!data.Registrations().Any(r => r.ClassId == Egg), "the egg keeps the resident shared model (no copy)");

        foreach (var (name, placements) in new[] { ("own first", new[] { Own(0), Other(1) }), ("other first", new[] { Other(0), Own(1) }) })
        {
            var both = cleared.Compose(donors, placements);
            var rows = new UniversalLevelData(both.LevelData);
            int own = placements[0].SourceLevelKey == "townsquare" ? 0 : 1;
            check(ClassOf(rows, both.NewRowIndices[own]) == Thief, name + ": own thief keeps 0x21");
            int otherClass = ClassOf(rows, both.NewRowIndices[1 - own]);
            check(otherClass != Thief && otherClass != Egg, name + ": Stone Hill's thief is renumbered");
            var stoneEgg = both.Classes.Single(c => c.SourceLevelKey == "stonehill" && c.ClassId == Egg);
            check(stoneEgg.DestinationClassId != Egg && stoneEgg.DestinationClassId != Thief && stoneEgg.BehaviorImported, name + ": Stone Hill's egg is renumbered with its own code");
            RequireOwnEgg(both, check, name);
        }
        var otherOnly = cleared.Compose(donors, [Other(0)]);
        check(ClassOf(new UniversalLevelData(otherOnly.LevelData), otherOnly.NewRowIndices[0]) != Thief, "another level's thief alone is renumbered");
        check(Released(otherOnly.Overlay).Count == 0 && Occurrences(otherOnly.Overlay, SpawnOwnEgg) == 1, "another level's thief alone releases nothing (kernel spawn only)");
    }

    private static void RequireOwnEgg(UniversalComposeResult result, Action<bool, string> check, string name)
    {
        var egg = result.Classes.Single(c => c.SourceLevelKey == "townsquare" && c.ClassId == Egg);
        check(egg.DestinationClassId == Egg && egg.BehaviorImported && egg.DependencyOnly, name + ": own egg keeps class 0x22 and runs Town Square's egg code");
        var thief = result.Classes.Single(c => c.SourceLevelKey == "townsquare" && c.ClassId == Thief);
        check(thief.DestinationClassId == Thief && thief.BehaviorImported, name + ": own thief runs its imported code under 0x21");
        // The kernel (Town Square's own overlay) spawns its egg once; the imported thief code spawns 0x22 too, never a renumbered egg.
        check(Occurrences(result.Overlay, SpawnOwnEgg) == 2, name + ": imported thief spawns class 0x22 through the spawn pointer");
        check(Released(result.Overlay).Order().SequenceEqual([Thief, Egg]), name + ": release stub hands 0x21 and 0x22 to the imported code");
    }

    // addiu a0, zero, 0x22; lui v0, 0x8007; lw v0, 0x58CC(v0): the thief's spawn of its egg (Town Square 0x8007F438).
    private static readonly uint[] SpawnOwnEgg = [0x24040022, 0x3C028007, 0x8C4258CC];

    /// <summary>The classes the release stub (UniversalNativeRelease) sends to imported code: its compares
    /// (addiu at, zero, class; beq a0, at) just before its "addiu a0, zero, 0x7FFF".</summary>
    private static List<int> Released(byte[] code)
    {
        List<int> classes = [];
        for (int o = 0; o + 4 <= code.Length; o += 4)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(o)) != 0x24047FFF) continue;
            for (int k = Math.Max(0, o - 4 * 40); k + 8 <= o; k += 4)
            {
                uint w = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(k)), next = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(k + 4));
                if ((w & 0xFFFF0000u) == 0x24010000u && (next & 0xFFFF0000u) == 0x10810000u) classes.Add((int)(w & 0xFFFF));
            }
        }
        return classes;
    }

    private static int Occurrences(byte[] code, uint[] words)
    {
        int n = 0;
        for (int o = 0; o + 4 * words.Length <= code.Length; o += 4)
            if (Enumerable.Range(0, words.Length).All(k => BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(o + 4 * k)) == words[k])) n++;
        return n;
    }

    private static int ClassOf(UniversalLevelData data, int row) => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36));
}
