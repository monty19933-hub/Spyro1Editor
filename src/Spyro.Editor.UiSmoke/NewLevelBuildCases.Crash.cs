using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Workspace;

// Crash-style level steps (a retail level re-themed with Crash Bandicoot: Warped art and boxes):
//   crash-disc      CRASH_DISC=<Crash Bandicoot - Warped (USA).bin>: remember the disc, as "Choose Crash disc…" does.
//   crash-textures  CRASH_LEVEL=<key>, CRASH_TEXTURES=<folder of key-NN.png>: copy the images into the project and
//                   add them as the level's custom terrain textures (the Terrain tab's image import).
//   crash-enemies   CRASH_LEVEL=<key>, CRASH_ENEMIES=<json {"enemies": [{"id", "x", "y", "z", "yaw"}]}>: add Crash
//                   enemies through the object browser, each next to the nearest native object, then move and turn it.
//   crash-objects   CRASH_LEVEL=<key>, CRASH_LAYOUT=<json>: replace every native basket with a Crash crate holding the
//                   same gem, then add the layout's boxes ({"boxes": [{"id", "x", "y", "z"}]}, editor units).
internal static partial class NewLevelBuildCases
{
    private static EditorWorkspace Workspace(MainWindow window) => Get<EditorWorkspace>(window, "_workspace");

    private static void CrashDisc(MainWindow window)
    {
        string path = Environment.GetEnvironmentVariable("CRASH_DISC") ?? throw new ArgumentException("Set CRASH_DISC.");
        CrashWarpedDiscLocator.Configure(Workspace(window), path);
        Console.WriteLine("crash disc: " + CrashWarpedDiscLocator.Find(Workspace(window)));
        Console.WriteLine("NEW_LEVEL_CRASH_DISC_OK");
    }

    private static void CrashTextures(MainWindow window)
    {
        string key = Environment.GetEnvironmentVariable("CRASH_LEVEL") ?? "darkhollow";
        string folder = Environment.GetEnvironmentVariable("CRASH_TEXTURES") ?? throw new ArgumentException("Set CRASH_TEXTURES.");
        LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(key)!;
        string root = Workspace(window).RootPath;
        string target = Path.Combine(root, "custom-terrain-textures", key);
        Directory.CreateDirectory(target);
        int count = 0;
        foreach (string png in Directory.GetFiles(folder, key + "-*.png").Order(StringComparer.Ordinal))
        {
            int id = int.Parse(Path.GetFileNameWithoutExtension(png)[(key.Length + 1)..]);
            string copy = Path.Combine(target, Path.GetFileName(png));
            File.Copy(png, copy, overwrite: true);
            var save = Task.Run(() => CustomTerrainTextureStore.AddOrReplaceAsync(root, key, level.DisplayName, id, copy, Path.GetFileName(png),
                descriptorTier: "both", tileSize: 64, sourceKind: "imported-image"));
            Wait(() => save.IsCompleted, "texture " + id, 60);
            if (save.IsFaulted) throw save.Exception!;
            count++;
        }
        Console.WriteLine($"{count} images in {CustomTerrainTextureStore.ManifestPath(root, key)}");
        // Reload the level so the editor shows them.
        Load(window, "artisans");
        Load(window, key);
        Console.WriteLine("NEW_LEVEL_CRASH_TEXTURES_OK");
    }

    private static void CrashObjects(MainWindow window)
    {
        string key = Environment.GetEnvironmentVariable("CRASH_LEVEL") ?? "darkhollow";
        using var layout = JsonDocument.Parse(File.ReadAllBytes(Environment.GetEnvironmentVariable("CRASH_LAYOUT") ?? throw new ArgumentException("Set CRASH_LAYOUT.")));
        Load(window, key);
        var (browser, entries) = OpenBrowser(window);
        browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var crash = (CrashWarpedDisc?)Invoke(window, "ResolveUniversalCrashDisc") ?? throw new InvalidOperationException("Run crash-disc first.");
        var crashEntries = entries.Where(e => e.Default.SourceLevelKey == UniversalCrashBoxes.SourceKey && UniversalCrashBoxes.IsClass(e.ClassId)).ToArray();
        if (crashEntries.Length != 3 || crashEntries.Any(e => e.RestrictionReason != null))
            throw new InvalidOperationException("The object browser does not offer the Crash boxes.");
        (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant) Box(string id)
        {
            foreach (var e in crashEntries)
                foreach (var v in e.Variants)
                    if (UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) == id) return (e, v);
            throw new InvalidOperationException("No Crash box " + id);
        }
        Moby Add(Moby reference, string id, Vector3f target)
        {
            var (entry, variant) = Box(id);
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", reference);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, variant, false, japanese)!;
            Wait(() => add.IsCompleted, "add " + id, 120);
            if (add.IsFaulted) throw add.Exception!;
            Moby added = Mobys(window).Single(m => !before.Contains(m));
            SetField(window, "_selectedMoby", added);
            Invoke(window, "NudgeSelectedMoby", target.X - added.Position.X, target.Y - added.Position.Y, target.Z - added.Position.Z);
            return added;
        }

        int replaced = 0;
        foreach (Moby basket in Mobys(window).Where(m => !m.IsAdded && !m.IsRemoved && m.SourceByte36 == UniversalCrashBoxes.BasketClass).ToArray())
        {
            string id = UniversalCrashBoxes.Crates.FirstOrDefault(c => c.Reward == basket.Flag4B).Id ?? "crash-crate";
            Moby crate = Add(basket, id, basket.Position);
            SetField(window, "_selectedMoby", basket); Invoke(window, "RemoveSelectedMoby");
            Console.WriteLine($"  basket row {basket.TrueIndex} (reward 0x{basket.Flag4B:X2}) -> {id} at ({crate.Position.X:0},{crate.Position.Y:0},{crate.Position.Z:0})");
            replaced++;
        }
        var natives = Mobys(window).Where(m => !m.IsAdded && !m.IsRemoved).ToArray();
        int added = 0;
        foreach (var b in layout.RootElement.GetProperty("boxes").EnumerateArray())
        {
            var target = new Vector3f(b.GetProperty("x").GetSingle(), b.GetProperty("y").GetSingle(), b.GetProperty("z").GetSingle());
            Moby near = natives.MinBy(m => (m.Position.X - target.X) * (m.Position.X - target.X) + (m.Position.Y - target.Y) * (m.Position.Y - target.Y))!;
            Moby box = Add(near, b.GetProperty("id").GetString()!, target);
            if (Math.Abs(box.Position.X - target.X) > 1 || Math.Abs(box.Position.Y - target.Y) > 1 || Math.Abs(box.Position.Z - target.Z) > 1)
                throw new InvalidOperationException($"{b.GetProperty("id").GetString()} landed at {box.Position}, not {target}.");
            added++;
        }
        Console.WriteLine($"replaced {replaced} baskets, added {added} boxes — {Status(window)}");

        LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(key)!;
        var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var special = UniversalMobyExport.SpecialProvider(japanese, lib, crash);
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, level, placements, special, removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        Console.WriteLine($"fit check: {placements.Count} added, {removed.Count} removed: {task.Result.Error ?? "fits"}");
        if (task.Result.Result is { } r) Console.WriteLine($"  treasure delta {r.TreasureDelta}");
        if (task.Result.Error != null) throw new InvalidOperationException(task.Result.Error);
        Save(window);
        Console.WriteLine("NEW_LEVEL_CRASH_OBJECTS_OK");
    }

    private static void CrashEnemies(MainWindow window)
    {
        string key = Environment.GetEnvironmentVariable("CRASH_LEVEL") ?? "darkhollow";
        using var layout = JsonDocument.Parse(File.ReadAllBytes(Environment.GetEnvironmentVariable("CRASH_ENEMIES") ?? throw new ArgumentException("Set CRASH_ENEMIES.")));
        Load(window, key);
        var (browser, entries) = OpenBrowser(window);
        browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var crash = (CrashWarpedDisc?)Invoke(window, "ResolveUniversalCrashDisc") ?? throw new InvalidOperationException("Run crash-disc first.");
        var enemyEntries = entries.Where(e => e.Category == "Enemies" && e.Default.SourceLevelKey == UniversalCrashEnemies.SourceKey).ToArray();
        if (enemyEntries.Length != UniversalCrashEnemies.Enemies.Count || enemyEntries.Any(e => e.RestrictionReason != null))
            throw new InvalidOperationException("The object browser does not offer the Crash enemies.");
        var natives = Mobys(window).Where(m => !m.IsAdded && !m.IsRemoved).ToArray();
        int added = 0;
        foreach (var b in layout.RootElement.GetProperty("enemies").EnumerateArray())
        {
            string id = b.GetProperty("id").GetString()!;
            var entry = enemyEntries.Single(e => UniversalSpecialCatalog.SpecialIdForClass(e.ClassId) == id);
            var target = new Vector3f(b.GetProperty("x").GetSingle(), b.GetProperty("y").GetSingle(), b.GetProperty("z").GetSingle());
            Moby near = natives.MinBy(m => (m.Position.X - target.X) * (m.Position.X - target.X) + (m.Position.Y - target.Y) * (m.Position.Y - target.Y))!;
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", near);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, entry.Default, false, japanese)!;
            Wait(() => add.IsCompleted, "add " + id, 120);
            if (add.IsFaulted) throw add.Exception!;
            Moby enemy = Mobys(window).Single(m => !before.Contains(m));
            SetField(window, "_selectedMoby", enemy);
            Invoke(window, "NudgeSelectedMoby", target.X - enemy.Position.X, target.Y - enemy.Position.Y, target.Z - enemy.Position.Z);
            // A nudge with no height change follows the ground, which can be an overhang above the spot: set the
            // height again on its own.
            if (Math.Abs(enemy.Position.Z - target.Z) > 1) Invoke(window, "NudgeSelectedMoby", 0f, 0f, target.Z - enemy.Position.Z);
            int yaw = b.TryGetProperty("yaw", out var y) ? y.GetInt32() & 255 : 0;
            Invoke(window, "RotateMobyFromViewport", new MobyRotateRequestedEventArgs(enemy, yaw));
            if (Math.Abs(enemy.Position.X - target.X) > 1 || Math.Abs(enemy.Position.Y - target.Y) > 1 || Math.Abs(enemy.Position.Z - target.Z) > 1 || enemy.YawByte != yaw)
                throw new InvalidOperationException($"{id} landed at {enemy.Position} yaw {enemy.YawByte}, not {target} yaw {yaw}.");
            Console.WriteLine($"  {id} at ({enemy.Position.X:0},{enemy.Position.Y:0},{enemy.Position.Z:0}) yaw {yaw}");
            added++;
        }
        Console.WriteLine($"added {added} Crash enemies — {Status(window)}");

        LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(key)!;
        var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
        var removed = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
        var special = UniversalMobyExport.SpecialProvider(japanese, lib, crash);
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(lib, level, placements, special, removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        Console.WriteLine($"fit check: {placements.Count} added, {removed.Count} removed: {task.Result.Error ?? "fits"}");
        if (task.Result.Result is { } r) Console.WriteLine($"  treasure delta {r.TreasureDelta}");
        if (task.Result.Error != null) throw new InvalidOperationException(task.Result.Error);
        Save(window);
        Console.WriteLine("NEW_LEVEL_CRASH_ENEMIES_OK");
    }
}
