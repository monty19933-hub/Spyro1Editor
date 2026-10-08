using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

/// <summary>Source-backed cases on a copy of the original USA image: every retail level composed with copies of its
/// own drop-capable objects keeps its retail spare slots, with every native row, property, pod and collision byte and
/// every pointer intact; a level short of game memory is refused in plain words; optionally a game image with Dark
/// Hollow plus 49 added objects for the emulator.</summary>
internal static class SourceCases
{
    internal static Task<int> Run(string[] args, int checks)
    {
        if (args.Length is not (3 or 4) || args.Length == 4 && args[3] != "--bin")
            throw new ArgumentException("Arguments: ORIGINAL_USA_BIN_COPY CATALOG_WORKSPACE OUTPUT_DIR [--bin]");
        void Check(bool ok, string name) { checks++; if (!ok) throw new InvalidOperationException(name); }
        string image = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[2]);
        UniversalSourceLibrary.RequireOriginal(image);
        var catalog = LevelCatalog.Load(Path.GetFullPath(args[1]));
        var lib = new UniversalSourceLibrary(image, catalog);
        Directory.CreateDirectory(output);
        List<object> report = [];
        List<string> refusals = [];

        UniversalComposeResult Compose(LevelDefinition level, IReadOnlyList<UniversalMobyPlacement> placements)
        {
            var host = lib.Get(level.Key);
            return UniversalMobyComposer.Compose(level, host.Data.Bytes, host.Overlay.Bytes.ToArray(), lib.Disc.Exe, lib.Get, lib.SharedClasses,
                UniversalMobyExport.DefaultProtectedVram(image, level), placements,
                sharedModelPackage: lib.SharedModelPackage, playerAnimationSource: lib.PlayerAnimations, nativeSharedEntry8: lib.Disc.EntryBytes(8));
        }

        foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0).OrderBy(l => l.LevelId))
        {
            var original = lib.Get(level.Key).Data;
            var o = original.Scene();
            int retail = UniversalMobyComposer.ArenaSlots(original);
            Check(retail is 91 or 182 or 272, level.Key + ": retail reserve read");
            // Copies of one of the level's own objects that drop something (the most common such class), or of its
            // most common object in levels with none (flight levels).
            var rows = Enumerable.Range(0, o.RowCount).Select(r => (Row: r, Bytes: original.Row(r))).ToList();
            int Class(byte[] row) => BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
            var dropRows = rows.Where(r => r.Bytes[0x53] != 0xFF && Class(r.Bytes) is not (>= 0x53 and <= 0x57)).ToList();
            var pool = dropRows.Count > 0 ? dropRows : rows;
            int sourceRow = pool.GroupBy(r => Class(r.Bytes)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().First().Row;
            int added = Math.Min(49, 255 - o.RowCount);
            byte[] template = original.Row(sourceRow);
            int x = BinaryPrimitives.ReadInt32LittleEndian(template.AsSpan(0x0C)), y = BinaryPrimitives.ReadInt32LittleEndian(template.AsSpan(0x10)),
                z = BinaryPrimitives.ReadInt32LittleEndian(template.AsSpan(0x14));
            var placements = Enumerable.Range(0, added).Select(i => new UniversalMobyPlacement(level.Key, sourceRow,
                x + 256 * (i % 7 - 3), y + 256 * (i / 7 - 3), z) { EditorId = $"slots-{level.Key}-{i}", PersistentSlot = i }).ToArray();
            UniversalComposeResult result;
            try { result = Compose(level, placements); }
            catch (Exception e) when (e is InvalidDataException or UniversalSourceCollisionException or IOException)
            {
                bool slots = e.Message.Contains("spare object slots"), ram = e is UniversalBudgetException;
                if (slots)
                {
                    // Game RAM cannot give back the reserve and the level's droppers need more than remain: refused in
                    // plain words, and fewer copies still compose.
                    Check(e.Message.StartsWith($"Only ") && e.Message.Contains($"spare object slots would remain in {level.DisplayName}") &&
                        e.Message.Contains("not enough game memory") && e.Message.Contains("remove some of the objects added"),
                        level.Key + ": plain refusal when game memory cannot keep the spare slots");
                    refusals.Add(level.Key);
                }
                if (slots || ram)
                {
                    // Fewer copies (as many as game RAM takes) still keep the reserve, or as much of it as fits.
                    added = 8;
                    placements = placements.Take(added).ToArray();
                    try { result = Compose(level, placements); }
                    catch (Exception again) when (again is InvalidDataException or IOException)
                    {
                        report.Add(new { level.Key, sourceRow, Class = Class(template), added, Skipped = again.Message, First = e.Message });
                        Console.WriteLine($"{level.Key,-15} row {sourceRow,3} class 0x{Class(template):X3}: skipped ({again.Message})");
                        continue;
                    }
                }
                else
                {
                    // Some objects cannot be copied on their own (linked or certified groups); that is not this check.
                    report.Add(new { level.Key, sourceRow, Class = Class(template), added, Skipped = e.Message });
                    Console.WriteLine($"{level.Key,-15} row {sourceRow,3} class 0x{Class(template):X3}: skipped ({e.Message})");
                    continue;
                }
            }
            var final = new UniversalLevelData(result.LevelData); var f = final.Scene();
            int before = (o.ArenaEnd - (o.RowsOffset + (o.RowCount + added) * UniversalLevelData.RowStride)) / UniversalLevelData.ArenaSlotBytes;
            Console.WriteLine($"{level.Key,-15} rows {o.RowCount}+{added} (row {sourceRow} class 0x{Class(template):X3}) spare slots {before} -> " +
                $"{result.ArenaSlotsRemaining} (+{result.ArenaSlotsAdded}) of {retail}; droppers {UniversalMobyComposer.DropCapableRows(final)}; " +
                $"RAM headroom {result.Ram.FinalHeadroom}");
            Check(f.RowCount == o.RowCount + added && result.NewRowIndices.SequenceEqual(Enumerable.Range(o.RowCount, added)), level.Key + ": rows appended");
            int droppers = UniversalMobyComposer.DropCapableRows(final);
            Check(UniversalMobyComposer.ArenaSlots(final) == result.ArenaSlotsRemaining && result.ArenaSlotsAdded == result.ArenaSlotsRemaining - before,
                level.Key + ": added slots reported");
            // Game RAM short of the whole reserve: as many as fit, at least what the droppers need, and a warning.
            bool partial = result.ArenaSlotsRemaining < retail;
            Check(!partial ? result.ArenaSlotsRemaining == retail :
                result.ArenaSlotsRemaining >= UniversalMobyComposer.RequiredDynamicSlots(retail, droppers) &&
                result.Ram.FinalHeadroom - UniversalMobyComposer.RamSafetyMargin < 0x800 + UniversalLevelData.ArenaSlotBytes &&
                result.Warnings.Any(w => w.Contains($"keeps {result.ArenaSlotsRemaining} of its {retail} spare object slots")),
                level.Key + ": the retail spare slots are kept, or as many as game RAM allows");
            Check(result.Ram.FinalEnd <= UniversalMobyComposer.NormalBufferFloor - UniversalMobyComposer.RamSafetyMargin, level.Key + ": within game RAM");
            RequireIntact(level.Key, original, final, added, Check);
            report.Add(new { level.Key, sourceRow, Class = Class(template), added, Before = before, After = result.ArenaSlotsRemaining,
                result.ArenaSlotsAdded, Retail = retail, Droppers = droppers, Partial = partial, result.Ram.FinalHeadroom, result.Ram.SceneGrowth });
        }
        string?[] composed = report.Where(r => r.GetType().GetProperty("After") != null).Select(r => r.GetType().GetProperty("Key")!.GetValue(r) as string).ToArray();
        Console.WriteLine($"composed {composed.Length} of 35 retail levels; refused for spare slots: {string.Join(", ", refusals)}");
        Check(composed.Length >= 31, $"copies composed in {composed.Length} of 35 retail levels");
        Check(composed.Contains("darkhollow") && composed.Contains("hauntedtowers") && composed.Contains("gnastysworld"),
            "Dark Hollow (Bandicoot Keep's level) and the 272- and 182-slot levels composed");
        Check(refusals.Contains("peacekeepers"), "Peace Keepers (little game memory, 108 droppers) refuses 49 copies in plain words");

        if (args.Length == 4)
        {
            // Dark Hollow with 49 copies of its gem vase, row 61 (88 droppers, 91 spare slots): a game image for the
            // emulator, written by the same Create BIN stage the editor uses. (49 copies of the enemy in row 35 on this
            // grid stop the level running with or without the restored slots, so the emulator test uses the vase.)
            var level = catalog.FindByKey("darkhollow")!;
            var host = lib.Get(level.Key).Data;
            int row = 61; byte[] t = host.Row(row);
            Check(BinaryPrimitives.ReadUInt16LittleEndian(t.AsSpan(0x36)) == 0xC2 && t[0x53] is >= 0x53 and <= 0x57, "Dark Hollow row 61 is a gem vase");
            int x = BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(0x0C)), y = BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(0x10)),
                z = BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(0x14));
            var placements = Enumerable.Range(0, 49).Select(i => new UniversalMobyPlacement(level.Key, row,
                x + 256 * (i % 7 - 3), y + 256 * (i / 7 - 3), z) { EditorId = $"slots-bin-{i}", PersistentSlot = i }).ToArray();
            string bin = Path.Combine(output, "darkhollow-49-added.bin");
            if (File.Exists(bin)) File.Delete(bin);
            var exported = UniversalMobyExport.Apply(lib, image, bin, catalog,
                new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> { [level.Key] = placements });
            var composedLevel = exported.Levels.Single().Compose;
            Check(composedLevel.ArenaSlotsRemaining == 91 && composedLevel.ArenaSlotsAdded == 39, "exported Dark Hollow keeps 91 spare slots");
            Check(UniversalMobyComposer.DropCapableRows(new UniversalLevelData(composedLevel.LevelData)) == 88, "exported Dark Hollow has 88 droppers");
            var written = UniversalDiscImage.Read(bin);
            Check(written.EntryBytes(level.SourceWadEntry).AsSpan().SequenceEqual(composedLevel.LevelData), "the image holds the composed level");
            File.WriteAllText(Path.ChangeExtension(bin, ".cue"), $"FILE \"{Path.GetFileName(bin)}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
            var sc = new UniversalLevelData(composedLevel.LevelData).Scene();
            File.WriteAllText(Path.Combine(output, "darkhollow-49-added.json"), JsonSerializer.Serialize(new
            {
                Bin = bin, Sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(bin))),
                Rows = sc.RowCount, SpareSlots = composedLevel.ArenaSlotsRemaining, composedLevel.ArenaSlotsAdded, Droppers = 88,
                RowsOffset = sc.RowsOffset, sc.ArenaEnd, sc.PropertiesComponent, NewRows = composedLevel.NewRowIndices
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"wrote {bin}");
        }

        File.WriteAllText(Path.Combine(output, "retail-dynamic-slots.json"), JsonSerializer.Serialize(new { checks, Levels = report },
            new JsonSerializerOptions { WriteIndented = true }));
        return Task.FromResult(checks);
    }

    /// <summary>Independent of the composer: every native row, property, pod and collision byte keeps its value
    /// (pointers follow their targets), the new slots are empty and every pointer stays inside the scene.</summary>
    private static void RequireIntact(string key, UniversalLevelData original, UniversalLevelData final, int added, Action<bool, string> check)
    {
        var o = original.Scene(); var f = final.Scene();
        byte[] os = original.Bytes.AsSpan(original.Subfiles()[3].Offset, o.UsedEnd).ToArray();
        byte[] fs = final.Bytes.AsSpan(final.Subfiles()[3].Offset, f.UsedEnd).ToArray();
        int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        // The new spare slots move the properties; the added objects' properties (appended to that component) also
        // move everything after it.
        int delta = f.PropertiesComponent - o.PropertiesComponent, appended = f.PropertiesSize - o.PropertiesSize;
        int Move(int v) => v < o.ArenaEnd ? v : v < o.PropertiesEnd ? v + delta : v + delta + appended;
        var fixedUp = o.Fixups.SelectMany(a => Enumerable.Range(a, 4)).ToHashSet();
        check(fs.AsSpan(0, o.MobyComponent).SequenceEqual(os.AsSpan(0, o.MobyComponent)), key + ": scene header and components before the objects unchanged");
        int nativeRows = o.RowCount * UniversalLevelData.RowStride;
        int rowDiff = Enumerable.Range(o.RowsOffset, nativeRows).Count(i => !fixedUp.Contains(i) && os[i] != fs[i]);
        check(rowDiff == 0, $"{key}: native rows unchanged ({rowDiff} bytes differ)");
        int propDiff = Enumerable.Range(o.PropertiesComponent + 4, o.PropertiesSize - 4).Count(i => !fixedUp.Contains(i) && os[i] != fs[i + delta]);
        check(propDiff == 0, $"{key}: native properties unchanged ({propDiff} bytes differ)");
        int podLength = o.FixupsOffset - o.PodComponent;
        check(fs.AsSpan(f.PodComponent, f.FixupsOffset - f.PodComponent).SequenceEqual(os.AsSpan(o.PodComponent, podLength)), key + ": pods and collision unchanged");
        foreach (int a in o.Fixups)
        {
            int at = Move(a);
            check(f.Fixups.Contains(at) && I(fs, at) == Move(I(os, a)), $"{key}: native pointer at 0x{a:X} follows its target");
        }
        int rowsEnd = f.RowsOffset + f.RowCount * UniversalLevelData.RowStride;
        check(fs.AsSpan(rowsEnd, f.ArenaEnd - rowsEnd).IndexOfAnyExcept((byte)0) < 0, key + ": spare slots empty");
        check(f.Fixups.All(a => a >= 0 && a + 4 <= f.UsedEnd && I(fs, a) >= 0 && I(fs, a) < f.UsedEnd), key + ": every pointer inside the scene");
        for (int r = o.RowCount; r < o.RowCount + added; r++)
        {
            int p = I(fs, f.RowsOffset + r * UniversalLevelData.RowStride);
            check(p == 0 || p >= f.PropertiesComponent + 4 && p < f.PropertiesEnd, $"{key}: added row {r} owns properties inside the component");
        }
    }
}
