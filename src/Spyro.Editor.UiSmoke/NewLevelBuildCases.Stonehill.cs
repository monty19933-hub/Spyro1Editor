using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static partial class NewLevelBuildCases
{
    // Rams and shepherds (by their source row) become cold-weather enemies, easiest near the start.
    private static readonly (int Row, string Hex, string Source, string Name)[] EnemySwaps =
    [
        (14, "0x0D3", "icecavern", "Snowballer"), (4, "0x0D3", "icecavern", "Snowballer"), (0, "0x0D3", "icecavern", "Snowballer"),
        (78, "0x0D3", "icecavern", "Snowballer"),
        (8, "0x0D4", "icecavern", "Agile Snowballer"), (6, "0x0D4", "icecavern", "Agile Snowballer"), (15, "0x0D4", "icecavern", "Agile Snowballer"),
        (5, "0x131", "highcaves", "Metalback Spider"), (7, "0x131", "highcaves", "Metalback Spider"), (16, "0x131", "highcaves", "Metalback Spider"),
        (17, "0x131", "highcaves", "Metalback Spider"),
    ];

    // Eggs: (reference object row to stand next to, offset, colour).
    private static readonly (int NearRow, float Dx, float Dy, string Colour)[] EggSpots =
    [
        (111, 70, 70, "dragonfly-egg-gold"),     // high platform with the flame chest
        (130, 60, 50, "dragonfly-egg-blue"),     // hill top near the start
        (26, -70, 50, "dragonfly-egg-red"),      // down by the key
        (127, 60, 60, "dragonfly-egg-purple"),   // ledge with three red gems
        (121, 60, 60, "dragonfly-egg-pink"),     // castle walkway green gem
        (12, 120, 60, "dragonfly-egg-white"),    // by the flame chests near the start
    ];

    private static void StonehillObjects(MainWindow window)
    {
        Load(window, "stonehill");
        var (browser, entries) = OpenBrowser(window);
        browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        Console.WriteLine("japanese disc: " + (japanese == null ? "MISSING" : "ok"));
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        Moby AddNear(Moby reference, UniversalMobyCatalogEntry entry, UniversalMobyVariant variant, Vector3f target)
        {
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", reference);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, variant, false, japanese)!;
            Wait(() => add.IsCompleted, "add " + entry.ClassId.ToString("X"), 120);
            if (add.IsFaulted) throw add.Exception!;
            Moby added = Mobys(window).Single(m => !before.Contains(m));
            SetField(window, "_selectedMoby", added);
            Invoke(window, "NudgeSelectedMoby", target.X - added.Position.X, target.Y - added.Position.Y, 0f);
            Console.WriteLine($"  added {added.DisplayLabel} at ({added.Position.X:0},{added.Position.Y:0},{added.Position.Z:0}) target ({target.X:0},{target.Y:0},{target.Z:0}) — {Status(window)}");
            return added;
        }

        foreach (var swap in EnemySwaps)
        {
            Moby old = Mobys(window).Single(m => !m.IsAdded && m.TrueIndex == swap.Row);
            if (old.DisplayLabel is not ("Ram" or "Shepherd")) throw new InvalidOperationException($"row {swap.Row} is {old.DisplayLabel}");
            var entry = Entry(entries, swap.Hex, swap.Source);
            var variant = entry.Variants.FirstOrDefault(v => v.SourceLevelKey == swap.Source) ?? entry.Default;
            AddNear(old, entry, variant, old.Position);
            SetField(window, "_selectedMoby", old);
            Invoke(window, "RemoveSelectedMoby");
            Console.WriteLine($"  removed {old.DisplayLabel} row {old.TrueIndex} (gem {old.TreasureValue}) for a {swap.Name}");
        }

        var eggEntry = entries.First(e => e.SubCategory == "Dragonfly Eggs" || e.Name.Contains("Dragonfly Egg", StringComparison.OrdinalIgnoreCase));
        foreach (var spot in EggSpots)
        {
            Moby near = Mobys(window).Single(m => !m.IsAdded && m.TrueIndex == spot.NearRow);
            var variant = eggEntry.Variants.FirstOrDefault(v => UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) == spot.Colour) ?? eggEntry.Default;
            Moby egg = AddNear(near, eggEntry, variant, new Vector3f(near.Position.X + spot.Dx, near.Position.Y + spot.Dy, near.Position.Z));
            Invoke(window, "SetUniversalEggColour", egg, spot.Colour);
            Console.WriteLine($"  egg {spot.Colour} next to {near.DisplayLabel}");
        }

        LevelDefinition stone = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("stonehill")!;
        var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var special = UniversalMobyExport.SpecialProvider(japanese, lib);
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, stone, placements, special, removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        Console.WriteLine($"fit check: {placements.Count} added, {removed.Count} removed: {(task.Result.Error ?? "fits")}");
        if (task.Result.Result is { } r) Console.WriteLine($"  treasure delta {r.TreasureDelta}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_OBJECTS_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static void FitMix(MainWindow window)
    {
        Load(window, "stonehill");
        var (browser, _) = OpenBrowser(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var special = UniversalMobyExport.SpecialProvider(japanese, lib);
        LevelDefinition stone = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("stonehill")!;
        var all = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var eggs = all.Where(p => p.SpecialId != null).ToList();
        var enemies = all.Where(p => p.SpecialId == null).ToList();
        Console.WriteLine($"placements: {enemies.Count} enemies ({string.Join(",", enemies.Select(p => p.SourceLevelKey + ":" + p.SourceRowIndex).Distinct())}), {eggs.Count} eggs ({string.Join(",", eggs.Select(e => e.SpecialId))})");
        void Try(string label, IEnumerable<UniversalMobyPlacement> set)
        {
            var list = set.ToList();
            var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, stone, list, special, removed));
            Wait(() => task.IsCompleted, "dry run", 900);
            Console.WriteLine($"{label,-48} {(task.Result.Error ?? $"fits (treasure delta {task.Result.Result?.TreasureDelta})")}");
        }
        string kind(UniversalMobyPlacement p) => p.SourceLevelKey + ":" + p.SourceRowIndex;
        var kinds = enemies.Select(kind).Distinct().ToList();
        Try("enemies only", enemies);
        Try("eggs only", eggs);
        Try("enemies + 6 eggs one colour", enemies.Concat(eggs.Select(e => e with { SpecialId = eggs[0].SpecialId })));
        Try("enemies + 6 eggs two colours", enemies.Concat(eggs.Select((e, i) => e with { SpecialId = eggs[i % 2].SpecialId })));
        Try("enemies + 6 eggs three colours", enemies.Concat(eggs.Select((e, i) => e with { SpecialId = eggs[i % 3].SpecialId })));
        foreach (string k in kinds)
            Try("without " + k + " + 6 eggs mixed", enemies.Where(p => kind(p) != k).Concat(eggs));
        Console.WriteLine("NEW_LEVEL_FITMIX_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static readonly (float X, float Y)[] EggTries = [(60, 60), (-60, 60), (60, -60), (-60, -60), (0, 80), (80, 0), (0, -80), (-80, 0), (40, 0), (0, 40)];

    private static void EggsFix(MainWindow window)
    {
        Load(window, "stonehill");
        var (browser, _) = OpenBrowser(window); browser.Close(); Pump(10);
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        float? Ground(float x, float y, float refZ)
        {
            var args = new object?[] { x, y, refZ, 0f, -1, false };
            return (bool)tryZ.Invoke(window, args)! ? (float)args[3]! : null;
        }
        var eggs = Mobys(window).Where(m => m.IsAdded && m.DisplayLabel.Contains("Dragonfly Egg", StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"eggs: {eggs.Count}");
        int n = 0;
        foreach (var spot in EggSpots)
        {
            Moby near = Mobys(window).Single(m => !m.IsAdded && m.TrueIndex == spot.NearRow);
            Moby egg = eggs.OrderBy(e => Math.Abs(e.Position.X - near.Position.X - spot.Dx) + Math.Abs(e.Position.Y - near.Position.Y - spot.Dy)).First();
            eggs.Remove(egg);
            float nearGround = Ground(near.Position.X, near.Position.Y, near.Position.Z) ?? near.Position.Z;
            float eggLift = egg.Position.Z - (Ground(egg.Position.X, egg.Position.Y, egg.Position.Z) ?? egg.Position.Z);
            (float X, float Y, float Z)? target = null;
            foreach (var (tx, ty) in EggTries)
            {
                float x = near.Position.X + tx, y = near.Position.Y + ty;
                if (Ground(x, y, nearGround) is float gz && Math.Abs(gz - nearGround) <= 25) { target = (x, y, gz + Math.Max(eggLift, 0)); break; }
            }
            if (target == null) { Console.WriteLine($"  no level ground beside {near.DisplayLabel}"); continue; }
            string colour = n++ % 2 == 0 ? "dragonfly-egg-gold" : "dragonfly-egg-blue";
            Invoke(window, "SetUniversalEggColour", egg, colour);
            SetField(window, "_selectedMoby", egg);
            Invoke(window, "NudgeSelectedMoby", target.Value.X - egg.Position.X, target.Value.Y - egg.Position.Y, target.Value.Z - egg.Position.Z);
            Console.WriteLine($"  {colour} egg beside {near.DisplayLabel} ({near.Position.X:0},{near.Position.Y:0},{near.Position.Z:0}) now at ({egg.Position.X:0},{egg.Position.Y:0},{egg.Position.Z:0}), ground {nearGround:0}");
        }
        LevelDefinition stone = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("stonehill")!;
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, stone, placements, UniversalMobyExport.SpecialProvider(japanese, lib), removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        Console.WriteLine($"fit check: {placements.Count} added ({string.Join(",", placements.Where(p => p.SpecialId != null).Select(p => p.SpecialId))}), {removed.Count} removed: {(task.Result.Error ?? $"fits, treasure delta {task.Result.Result?.TreasureDelta}")}");
        Save(window);
        using (var w = new StreamWriter(Path.Combine(Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE")!, "..", "..", "runtime", "stonehill-spec.json")))
        {
            var added = Mobys(window).Where(m => m.IsAdded).ToList();
            string J(Moby m, string key, string val) => $"{{\"{key}\":\"{val}\",\"x\":{m.Position.X:0},\"y\":{m.Position.Y:0},\"z\":{m.Position.Z:0}}}";
            w.Write("{\"enemies\":[" + string.Join(",", added.Where(m => !m.DisplayLabel.Contains("Egg")).Select(m => J(m, "name", m.DisplayLabel))) + "],");
            w.Write("\"eggs\":[" + string.Join(",", added.Where(m => m.DisplayLabel.Contains("Egg")).Select(m => J(m, "colour", m.DisplayLabel))) + "]}");
        }
        Console.WriteLine("NEW_LEVEL_EGGS_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static readonly (int NearRow, string Gem)[] GemSpots = [(122, "Green"), (13, "Red")];

    private static void Gems(MainWindow window)
    {
        Load(window, "stonehill");
        var (browser, entries) = OpenBrowser(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var gemEntries = entries.Where(e => e.Name.Contains("gem", StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine("gem entries: " + string.Join("; ", gemEntries.Select(e => $"{e.Name} 0x{e.ClassId:X} [{string.Join(",", e.SourceLevels.Take(3))}]")));
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        float? Ground(float x, float y, float refZ)
        {
            var args = new object?[] { x, y, refZ, 0f, -1, false };
            return (bool)tryZ.Invoke(window, args)! ? (float)args[3]! : null;
        }
        foreach (var spot in GemSpots)
        {
            var entry = gemEntries.FirstOrDefault(e => e.Name.StartsWith(spot.Gem, StringComparison.OrdinalIgnoreCase) && e.SourceLevels.Contains("stonehill"))
                ?? gemEntries.First(e => e.Name.StartsWith(spot.Gem, StringComparison.OrdinalIgnoreCase));
            var variant = entry.Variants.FirstOrDefault(v => v.SourceLevelKey == "stonehill") ?? entry.Default;
            Moby near = Mobys(window).Single(m => !m.IsAdded && m.TrueIndex == spot.NearRow);
            float nearGround = Ground(near.Position.X, near.Position.Y, near.Position.Z) ?? near.Position.Z;
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", near);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, variant, false, japanese)!;
            Wait(() => add.IsCompleted, "add gem", 120);
            Moby gem = Mobys(window).Single(m => !before.Contains(m));
            float lift = gem.Position.Z - (Ground(gem.Position.X, gem.Position.Y, gem.Position.Z) ?? gem.Position.Z);
            foreach (var (tx, ty) in EggTries)
            {
                float x = near.Position.X - tx, y = near.Position.Y - ty;
                if (Ground(x, y, nearGround) is float gz && Math.Abs(gz - nearGround) <= 25)
                {
                    SetField(window, "_selectedMoby", gem);
                    Invoke(window, "NudgeSelectedMoby", x - gem.Position.X, y - gem.Position.Y, gz + Math.Max(lift, 0) - gem.Position.Z);
                    break;
                }
            }
            Console.WriteLine($"  {gem.DisplayLabel} (gem {gem.TreasureValue}) from {variant.SourceLevelKey} beside {near.DisplayLabel} at ({gem.Position.X:0},{gem.Position.Y:0},{gem.Position.Z:0})");
        }
        LevelDefinition stone = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("stonehill")!;
        var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, stone, placements, UniversalMobyExport.SpecialProvider(japanese, lib), removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        Console.WriteLine($"fit check: {placements.Count} added: {(task.Result.Error ?? $"fits, treasure delta {task.Result.Result?.TreasureDelta}, RAM headroom {task.Result.Result?.Ram.FinalHeadroom}")}");
        if (task.Result.Error == null) Save(window); else Console.WriteLine("NOT saved");
        Console.WriteLine("NEW_LEVEL_GEMS_OK");
    }
}
