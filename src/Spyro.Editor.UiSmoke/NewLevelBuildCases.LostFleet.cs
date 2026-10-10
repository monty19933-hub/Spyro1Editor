using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Lost Fleet (Spyro: Year of the Dragon) in place of Artisans: the scene the Lost Fleet lab compiled (faces,
/// doorway frames, five doorways, sky, music) is applied to a blank Artisans, its pictures copied into the project; then
/// its objects are added through the Add Object browser as a user would.</summary>
internal static partial class NewLevelBuildCases
{
    private static void YearOfTheDragonDisc(MainWindow window)
    {
        string path = Environment.GetEnvironmentVariable("YOTD_DISC") ?? throw new ArgumentException("Set YOTD_DISC.");
        Spyro.Editor.Core.Music.YearOfTheDragonDiscLocator.Configure(Workspace(window), path);
        Console.WriteLine("year of the dragon disc: " + Spyro.Editor.Core.Music.YearOfTheDragonDiscLocator.Find(Workspace(window)));
        Console.WriteLine("NEW_LEVEL_YOTD_DISC_OK");
    }

    private static void LostFleetBuild(MainWindow window)
    {
        string scenePath = Environment.GetEnvironmentVariable("LOSTFLEET_SCENE")
            ?? throw new InvalidOperationException("Set LOSTFLEET_SCENE to the Lost Fleet lab's artisans-replacement.scene.json");
        StartBlank(window, "artisans");
        var lab = JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(scenePath))!;
        string dir = Path.Combine(Workspace(window).RootPath, "custom-terrain-textures", "lost-fleet");
        Directory.CreateDirectory(dir);
        Dictionary<string, string> copied = [];
        AuthoredMaterial? Move(AuthoredMaterial? material)
        {
            if (material?.Image is not { } source) return material;
            if (!copied.TryGetValue(source, out var target))
            {
                target = Path.Combine(dir, Path.GetFileName(source));
                File.Copy(source, target, true);
                copied[source] = target;
            }
            return material with { Image = target };
        }
        var doc = window.FreshSceneForTesting! with
        {
            Name = lab.Name, Spawn = lab.Spawn, Heading = lab.Heading, SkySourceWadEntry = lab.SkySourceWadEntry, SkyColor = lab.SkyColor,
            MusicTrack = lab.MusicTrack, Portals = lab.Portals, Surfaces = lab.Surfaces.Select(s => s with { Material = Move(s.Material) }).ToArray(),
        };
        Console.WriteLine($"lost fleet: {doc.Surfaces.Count} faces, {copied.Count} pictures, {doc.Portals?.Count ?? 0} doorways, sky {doc.SkySourceWadEntry}, music {doc.MusicTrack}");
        ApplyScene(window, doc);
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_OK");
    }

    /// <summary>The first project build placed Artisans row 115 (a Sparx anchor, class 0x143) where sheep were meant:
    /// it is removed and three of Artisans' sheep (class 0x00A) take the spots in the objects file.</summary>
    private static void LostFleetSheep(MainWindow window)
    {
        string objectsPath = Environment.GetEnvironmentVariable("LOSTFLEET_OBJECTS")
            ?? throw new InvalidOperationException("Set LOSTFLEET_OBJECTS to lf-objects-final.json");
        Load(window, "artisans");
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.CrossLevelSourceLevelKey == "artisans" && m.CrossLevelSourceTrueIndex == 115).ToList())
        { SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby"); Console.WriteLine("  removed " + m.DisplayLabel); }
        var (browser, entries) = OpenBrowser(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var entry = entries.First(e => e.ClassId == 0x00A && e.SourceLevels.Contains("artisans"));
        var variant = entry.Variants.First(v => v.SourceLevelKey == "artisans");
        foreach (var o in JsonDocument.Parse(File.ReadAllText(objectsPath)).RootElement.EnumerateArray().Where(o => o.TryGetProperty("class", out var c) && c.GetInt32() == 0x00A))
        {
            float x = o.GetProperty("x").GetSingle(), y = o.GetProperty("y").GetSingle(), z = o.GetProperty("z").GetSingle();
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, variant, false, japanese)!;
            Wait(() => add.IsCompleted, "add sheep", 120);
            if (add.IsFaulted) throw add.Exception!;
            var moby = Mobys(window).First(m => !before.Contains(m));
            SetField(window, "_selectedMoby", moby);
            Invoke(window, "NudgeSelectedMoby", x - moby.Position.X, y - moby.Position.Y, z - moby.Position.Z);
            Console.WriteLine($"  {moby.DisplayLabel,-34} ({moby.Position.X:0},{moby.Position.Y:0},{moby.Position.Z:0})");
        }
        Console.WriteLine($"objects {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved)}, editor-known treasure {Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue)}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_SHEEP_OK");
    }

    private static void LostFleetObjects(MainWindow window)
    {
        string objectsPath = Environment.GetEnvironmentVariable("LOSTFLEET_OBJECTS")
            ?? throw new InvalidOperationException("Set LOSTFLEET_OBJECTS to lf-objects-final.json");
        Load(window, "artisans");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Artisans is not blank");
        var (browser, entries) = OpenBrowser(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var variants = entries.SelectMany(e => e.Variants.Select(v => (Entry: e, Variant: v))).ToArray();
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).ToList())
        { SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby"); }
        // Group members the editor adds with their root (the balloon with the balloonist), skipped once each when listed.
        List<(string Source, int Row)> linked = [];
        foreach (var o in JsonDocument.Parse(File.ReadAllText(objectsPath)).RootElement.EnumerateArray())
        {
            float x = o.GetProperty("x").GetSingle(), y = o.GetProperty("y").GetSingle(), z = o.GetProperty("z").GetSingle();
            (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant) pick;
            if (o.TryGetProperty("class", out var cls))
            {
                int c = cls.GetInt32();
                pick = variants.Where(p => p.Variant.ClassId == c && p.Entry.ClassId == c).OrderBy(p => p.Variant.SourceLevelKey == "artisans" ? 0 : 1).First();
            }
            else
            {
                string source = o.GetProperty("source").GetString()!; int row = o.GetProperty("row").GetInt32();
                if (linked.Remove((source, row))) continue;
                pick = variants.First(p => p.Variant.SourceLevelKey == source && p.Variant.SourceRow == row);
            }
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, pick.Entry, pick.Variant, false, japanese)!;
            Wait(() => add.IsCompleted, $"add {pick.Variant.SourceLevelKey}:{pick.Variant.SourceRow}", 120);
            if (add.IsFaulted) throw add.Exception!;
            var added = Mobys(window).Where(m => !before.Contains(m)).ToList();
            if (added.Count == 0) throw new InvalidOperationException($"not added: {pick.Variant.SourceLevelKey}:{pick.Variant.SourceRow}: {Status(window)}");
            Moby moby = added[0];
            linked.AddRange(added.Skip(1).Select(m => (m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex)));
            SetField(window, "_selectedMoby", moby);
            Invoke(window, "NudgeSelectedMoby", x - moby.Position.X, y - moby.Position.Y, z - moby.Position.Z);
            Console.WriteLine($"  {moby.DisplayLabel,-34} ({moby.Position.X:0},{moby.Position.Y:0},{moby.Position.Z:0}) gem {moby.TreasureValue} +{added.Count - 1} linked");
        }
        int treasure = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);
        Console.WriteLine($"objects {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved)}, editor-known treasure {treasure}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_OBJECTS_OK");
    }
}
