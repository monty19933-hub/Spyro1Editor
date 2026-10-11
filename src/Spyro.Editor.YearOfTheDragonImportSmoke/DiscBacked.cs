using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.YearOfTheDragon;

/// <summary>Checks against the user's Year of the Dragon disc (and the original Spyro the Dragon disc for carriers).</summary>
static class DiscBacked
{
    public static async Task<int> Run(string yotdPath, string usa, string workspace, string? reference)
    {
        int checks = 0;
        void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); checks++; }
        var disc = new YearOfTheDragonDisc(yotdPath);
        var levels = YearOfTheDragonLevels.Read(disc);
        Check(levels.Count == 37 && YearOfTheDragonLevels.ReadNames(disc.Executable()).SequenceEqual(levels.Select(l => l.Name)), "the level table reads back");

        // Every level's main area decodes, with collision and faces.
        foreach (var level in levels)
        {
            var d = YearOfTheDragonLevelData.Read(disc, level);
            Check(d.Sectors.Any(s => s.NearFaces.Count > 0) && d.Collision.Count > 0 && d.Textures.Count > 0 && d.SkyBackground != null, $"{level.Name} decodes");
            Console.WriteLine(Survey.Line(disc, level));
        }

        // Lost Fleet: the general decoder's distant meshes are the pinned reader's.
        var lostFleet = YearOfTheDragonLevels.Find("Lost Fleet");
        var lf = YearOfTheDragonLevelData.Read(disc, lostFleet);
        var pinned = YearOfTheDragonTerrain.LostFleetFarMeshes(disc);
        var general = YearOfTheDragonTerrain.FarMeshes(disc, lostFleet);
        Check(pinned.Count == general.Count && pinned.Zip(general).All(p => p.First.Sector == p.Second.Sector && p.First.Vertices.SequenceEqual(p.Second.Vertices) &&
            p.First.Colors.SequenceEqual(p.Second.Colors) && p.First.Faces.Count == p.Second.Faces.Count &&
            p.First.Faces.Zip(p.Second.Faces).All(f => f.First.Corners.SequenceEqual(f.Second.Corners) && f.First.Colors.SequenceEqual(f.Second.Colors))),
            "Lost Fleet's distant meshes match the pinned reader");
        Check(lf.GroundKinds.Count == 3 && lf.GroundKinds[0].Type == 1 && lf.Collision.Count(t => t.Kind == 0) == 477, "Lost Fleet's acid is its 477 hot collision triangles");
        Check(lf.SkyBackground == new ColorRgba(0x32, 0x24, 0xAB) && lf.Objects.Count == 227, "Lost Fleet's sky colour (the sky converter's) and its 227 objects");

        // Kill heights of the replaced levels (Artisans keeps Lost Fleet at its own heights).
        var exe = Spyro.Editor.Core.Universal.UniversalDiscImage.Read(usa).Exe;
        Check(YearOfTheDragonLevelImport.KillHeight(exe, 10) == 32 && YearOfTheDragonLevelImport.KillHeight(exe, 23) == 1000 &&
              YearOfTheDragonLevelImport.KillHeight(exe, 21) == 687.5f && YearOfTheDragonLevelImport.KillHeight(exe, 22) == 781.25f, "kill heights");

        // Lost Fleet into Artisans.
        var catalog = LevelCatalog.Load(workspace);
        string scratch = Path.Combine(Path.GetTempPath(), "yotd-import-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = await YearOfTheDragonLevelImport.ImportAsync(yotdPath, lostFleet, usa, catalog, Survey.Binding(usa, catalog, "artisans"),
                new() { PictureFolder = scratch });
            foreach (string line in result.Report.Lines()) Console.WriteLine("  " + line);
            Console.WriteLine($"  imported in {watch.Elapsed.TotalSeconds:0}s");
            var scene = result.Scene;
            scene.Validate();
            Check(scene.Surfaces.Count == result.Report.Faces && scene.Surfaces.All(s => s.Material?.Image is { } p && File.Exists(p)), "every face has a written picture");
            Check(result.Report.NativeSectors <= 127 && result.Report.KeptTextures <= 127 && result.Report.Lift == 0, "within the sector and material limits, not raised");
            Check(scene.Surfaces.Count(s => s.Kind == AuthoredSurfaceKind.DeadlyOoze) >= 265, "the acid is deadly ooze");
            if (reference != null) checks += Compare(scene, JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(reference))!);
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
        Console.WriteLine($"YOTD_IMPORT_DISC_OK checks={checks}");
        return 0;
    }

    static string Key(IEnumerable<Spyro.Editor.Core.Primitives.Vector3f> v) => string.Join(";", v.Select(p => $"{p.X},{p.Y},{p.Z}").Order(StringComparer.Ordinal));

    /// <summary>The hand-built Lost Fleet (lab faces + the holes step) against the import: same faces (corners in the
    /// same order), colours, sectors, sides and ground kinds; textures may differ by merge.</summary>
    static int Compare(SceneDocument imported, SceneDocument reference)
    {
        var refFaces = reference.Surfaces.Where(s => s.Sector != null).ToList();
        var byKey = imported.Surfaces.GroupBy(s => Key(s.Vertices)).ToDictionary(g => g.Key, g => g.ToList());
        int matched = 0, order = 0, colours = 0, sectors = 0, sides = 0, kinds = 0, waterOnly = 0;
        List<string> missing = [];
        foreach (var r in refFaces)
        {
            if (!byKey.TryGetValue(Key(r.Vertices), out var list) || list.Count == 0) { missing.Add(Key(r.Vertices)); continue; }
            var s = list.FirstOrDefault(x => x.Vertices.SequenceEqual(r.Vertices)) ?? list[0];
            list.Remove(s);
            matched++;
            if (s.Vertices.SequenceEqual(r.Vertices)) order++;
            if (s.NearColors!.SequenceEqual(r.NearColors!) && s.FarColors!.SequenceEqual(r.FarColors!)) colours++;
            if (s.Sector == r.Sector) sectors++;
            if (s.TwoSided == r.TwoSided && s.FrontReversed == r.FrontReversed) sides++;
            if (s.Kind == r.Kind) kinds++;
            else if (s.Kind == AuthoredSurfaceKind.Water && r.Kind == null) waterOnly++;
        }
        int extra = byKey.Values.Sum(l => l.Count);
        Console.WriteLine($"  reference: {refFaces.Count} Lost Fleet faces ({reference.Surfaces.Count - refFaces.Count} doorway frames not compared); import {imported.Surfaces.Count}");
        Console.WriteLine($"  matched {matched}, same corner order {order}, same colours {colours}, same sector {sectors}, same sides {sides}, same ground {kinds} ({waterOnly} water surfaces the hand build left solid); reference-only {missing.Count}, import-only {extra}");
        Console.WriteLine($"  reference far meshes {reference.FarMeshes?.Count ?? 0}, import {imported.FarMeshes?.Count ?? 0}; spawn ref ({reference.Spawn.X},{reference.Spawn.Y},{reference.Spawn.Z}) import ({imported.Spawn.X},{imported.Spawn.Y},{imported.Spawn.Z})");
        foreach (string m in missing.Take(5)) Console.WriteLine("    reference-only " + m);
        // The hand build left the five acid-edge faces whose collision carries Year of the Dragon's water bit (and no hot
        // flag) solid; the importer makes every water surface water.
        if (matched != refFaces.Count || extra != 0 || order != matched || colours != matched || sides != matched || matched - kinds != waterOnly || waterOnly != 5)
            throw new InvalidOperationException("The import differs from the hand-built Lost Fleet.");
        // Distant meshes: the reference took Year of the Dragon's own per sector; the import groups nothing in Lost Fleet.
        var refFar = reference.FarMeshes!.ToDictionary(m => m.Sector);
        int sameFar = imported.FarMeshes!.Count(m => refFar.TryGetValue(m.Sector, out var r) && r.Vertices.SequenceEqual(m.Vertices) && r.Colors.SequenceEqual(m.Colors) &&
            r.Faces.Count == m.Faces.Count && r.Faces.Zip(m.Faces).All(f => f.First.Corners.SequenceEqual(f.Second.Corners) && f.First.Colors.SequenceEqual(f.Second.Colors)));
        Console.WriteLine($"  distant meshes identical: {sameFar} of {refFar.Count}");
        if (sameFar != refFar.Count || imported.FarMeshes!.Count != refFar.Count) throw new InvalidOperationException("The distant meshes differ from the hand-built Lost Fleet.");
        return 7;
    }
}
