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
        var crashEntries = entries.Where(e => e.Default.SourceLevelKey == UniversalCrashBoxes.SourceKey).ToArray();
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
}
