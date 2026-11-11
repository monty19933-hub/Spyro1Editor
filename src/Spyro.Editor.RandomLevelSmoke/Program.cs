using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

if (args.Length == 5 && args[0] == "--fingerprint")
{
    // A level's fingerprint in a process of its own (the gate compares it with the one made in the gate's process).
    var catalog = LevelCatalog.Load(args[4]);
    var r = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(new Spyro.Editor.Core.Generation.RandomLevelOptions
    { Seed = long.Parse(args[1]), Style = Enum.Parse<Spyro.Editor.Core.Generation.LevelStyle>(args[2]), Size = Enum.Parse<Spyro.Editor.Core.Generation.LevelSize>(args[3]) }, catalog, source: RandomLevelCases.LookSource());
    Console.WriteLine("FINGERPRINT " + r.Scene.Fingerprint());
    return 0;
}
if (args.Length >= 6 && args[0] == "--dump")
{
    // --dump <seed> <style> <size> <repo> <out.json> [theme]: the level's faces for the study renderer (no disc).
    var catalog = LevelCatalog.Load(args[4]);
    var options = new Spyro.Editor.Core.Generation.RandomLevelOptions
    {
        Seed = long.Parse(args[1]), Style = args[2] == "-" ? null : Enum.Parse<Spyro.Editor.Core.Generation.LevelStyle>(args[2]),
        Size = Enum.Parse<Spyro.Editor.Core.Generation.LevelSize>(args[3]), Theme = args.Length > 6 && args[6] != "-" ? args[6] : null,
        // RANDOM_DOORWAYS=n: a homeworld with n doorways (built as a new world's, to be looked at); RANDOM_DIFFICULTY.
        Doorways = int.TryParse(Environment.GetEnvironmentVariable("RANDOM_DOORWAYS"), out int doors) ? doors : 0,
        Difficulty = Enum.TryParse<Spyro.Editor.Core.Generation.LevelDifficulty>(Environment.GetEnvironmentVariable("RANDOM_DIFFICULTY"), out var hard) ? hard : Spyro.Editor.Core.Generation.LevelDifficulty.Medium,
        // RANDOM_LAYOUT: journey, loop, hub, arena, climb, gorge or web (unset: the seed's own).
        Layout = Environment.GetEnvironmentVariable("RANDOM_LAYOUT") is { Length: > 0 } layout ? layout : null,
    };
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var source = RandomLevelCases.LookSource();
    var r = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(options, catalog, compile: Environment.GetEnvironmentVariable("RANDOM_NO_COMPILE") != "1", source: source);
    Console.WriteLine($"{watch.ElapsedMilliseconds} ms: {r.Report.Summary}");
    Console.WriteLine("draw: " + r.Report.DrawPlan);
    foreach (var note in r.Report.Notes) Console.WriteLine("note: " + note);
    var keyOf = catalog.Levels.GroupBy(l => l.SourceWadEntry).ToDictionary(g => g.Key, g => g.First().Key);
    if (r.Doorways.Count > 0)
    {
        var destinations = Enumerable.Range(71, Math.Min(4, r.Doorways.Count)).Select(id => new DoorwayDestination(id, "LEVEL " + (id - 70), false, null))
            .Concat(r.Doorways.Count > 4 ? [new DoorwayDestination(10, "Artisans", false, SceneDoorways.WayBackNote)] : []).ToArray();
        var (built, added, problems) = SceneDoorways.AddAll(r.Scene with { WorldLevelId = NewWorldDocument.Homeworld }, r.Doorways, destinations,
            Spyro.Editor.Core.Generation.LevelThemes.DoorwayShape(r.Report.Theme), r.DoorwayMaterial!);
        Console.WriteLine($"doorways: {string.Join(", ", built.Portals!.Select(d => $"{d.DestinationLevelId} {d.Shape} x{d.SizeScale:0.00}"))} {string.Join(" ", problems)}");
        r = r with { Scene = built };
    }
    // Doorway frames are added after the level's own faces: the play-floor marks cover the level's own.
    var faces = r.Scene.Surfaces.Select((s, index) => new
    {
        w = index < r.PlayFloor.Count && r.PlayFloor[index] ? 1 : 0,
        p = s.Vertices.Select(v => new[] { v.X, v.Y, v.Z }).ToArray(),
        c = (s.NearColors ?? []).Select(c => new[] { (int)c.R, c.G, c.B }).ToArray(),
        t = s.Material == null ? null : $"{keyOf.GetValueOrDefault(s.Material.SourceWadEntry, "?")}:{s.Material.TextureId}",
        k = s.Kind?.Type,
    });
    await File.WriteAllTextAsync(args[5], System.Text.Json.JsonSerializer.Serialize(new
    {
        name = r.Scene.Name, spawn = new[] { r.Scene.Spawn.X, r.Scene.Spawn.Y, r.Scene.Spawn.Z }, heading = r.Scene.Heading, shape = r.Shape,
        areas = r.Report.Areas, counts = r.Report.Counts, theme = r.Report.Theme, style = r.Report.Style.ToString(),
        gems = (r.Scene.GemSpots ?? []).Select(g => new[] { g.X, g.Y, g.Z }).ToArray(),
        links = r.Report.Links.Select(l => new { l.Kind, from = new[] { l.From.X, l.From.Y, l.From.Z }, to = new[] { l.To.X, l.To.Y, l.To.Z } }).ToArray(), faces,
    }));
    await TerrainTexturePngWriter.WriteRgbaAsync(Path.ChangeExtension(args[5], ".map.png"), r.MapWidth, r.MapHeight, r.Map);
    return 0;
}
if (args.Length >= 2 && args[0] == "--variety")
{
    // --variety <repo> [seeds]: how much the levels of each kind differ over that many seeds (no disc; the gate
    // runs the same checks over two dozen).
    int failed = 0;
    RandomLevelCases.Variety(LevelCatalog.Load(args[1]), RandomLevelCases.LookSource(), (ok, what) => { if (!ok) { failed++; Console.WriteLine("FAIL " + what); } }, args.Length > 2 ? int.Parse(args[2]) : 24);
    Console.WriteLine(failed == 0 ? "VARIETY_OK" : $"VARIETY_FAILED {failed}");
    return failed == 0 ? 0 : 1;
}
// Gate (no disc): [repo root] [--maps <folder>] [--seeds N]
if (args.Length == 0 || args[0] is not ("--fingerprint" or "--variety" or "--dump" or "--static-textures" or "--catalog" or "--calibration" or "--export" or "--surface-kinds" or "--dump-exe" or "--dump-textures" or "--export-random" or "--check-themes" or "--doorway-facts" or "--balloon-facts" or "--scatter-facts" or "--model-colours" or "--thief-colours"))
{
    string repo = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : Directory.GetCurrentDirectory();
    string? maps = args.SkipWhile(a => a != "--maps").Skip(1).FirstOrDefault();
    int seeds = int.TryParse(args.SkipWhile(a => a != "--seeds").Skip(1).FirstOrDefault(), out int s) ? s : 2;
    string? only = args.SkipWhile(a => a != "--style").Skip(1).FirstOrDefault();
    return await RandomLevelCases.Run(repo, maps, seeds, only);
}
// Lab modes (need the original USA disc; never part of the gate):
//   --calibration <disc.bin> <repo root (catalog)> <out.bin>   movement-measuring level, New Game starts in it
//   --export <scene.json> <disc.bin> <repo root> <out.bin>     any saved scene as the new level (New Game starts in it)
if (args.Length >= 4 && args[0] == "--calibration")
{
    var catalog = LevelCatalog.Load(args[2]);
    var scene = Calibration.Build(catalog);
    await scene.SaveAsync(Path.ChangeExtension(Path.GetFullPath(args[3]), ".scene.json"));
    await Export(scene, args[1], catalog, args[3]);
    return 0;
}
if (args.Length >= 5 && args[0] == "--export")
{
    var catalog = LevelCatalog.Load(args[3]);
    await Export(SceneDocument.Read(args[1]), args[2], catalog, args[4]);
    return 0;
}
if (args.Length >= 4 && args[0] == "--dump-textures")
{
    var catalog = LevelCatalog.Load(args[2]);
    foreach (var level in catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64))
    {
        string folder = Path.Combine(args[3], level.Key);
        Directory.CreateDirectory(folder);
        try
        {
            var done = await TerrainPatchExporter.ExportTerrainTextureImagesAsync(args[1], level, Enumerable.Range(0, 128), folder);
            Console.WriteLine($"{level.Key}: {done.Count} textures");
        }
        catch (Exception e) { Console.WriteLine($"{level.Key}: {e.Message}"); }
    }
    return 0;
}
if (args.Length >= 7 && args[0] == "--export-random")
{
    // --export-random <seed> <style> <size> <disc> <repo> <out.bin> [theme] [pictures]
    var catalog = LevelCatalog.Load(args[5]);
    var options = new Spyro.Editor.Core.Generation.RandomLevelOptions
    {
        Seed = long.Parse(args[1]), Style = Enum.Parse<Spyro.Editor.Core.Generation.LevelStyle>(args[2]), Size = Enum.Parse<Spyro.Editor.Core.Generation.LevelSize>(args[3]),
        Theme = args.Length > 7 && args[7] != "-" ? args[7] : null,
        ChargePaths = Environment.GetEnvironmentVariable("RANDOM_CHARGE") == "1" ? true : null,
        Textures = args.Length > 8 && args[8] == "pictures" ? Spyro.Editor.Core.Generation.LevelTextureMode.GeneratedPictures : Spyro.Editor.Core.Generation.LevelTextureMode.GameTextures,
    };
    string output = Path.GetFullPath(args[6]);
    Spyro.Editor.Core.Generation.PictureCarriers? carriers = null;
    if (options.Textures == Spyro.Editor.Core.Generation.LevelTextureMode.GeneratedPictures)
    {
        var lib = new UniversalSourceLibrary(args[4], catalog);
        var slots = Spyro.Editor.Core.Crash.CrashLevelPictures.Carriers(lib, catalog, catalog.Levels.Select(l => l.Key), args[4]);
        carriers = new([.. slots.Select(c => (c.Wad, c.Id))], Path.Combine(Path.GetDirectoryName(output)!, "pictures"));
        Directory.CreateDirectory(carriers.Folder);
    }
    var source = RandomLevelCases.LookSource();
    var result = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(options, catalog, carriers, source: source);
    foreach (var p in result.Pictures) await TerrainTexturePngWriter.WriteRgbaAsync(p.Path, 64, 64, p.Pixels);
    Console.WriteLine(result.Report.Summary);
    await result.Scene.SaveAsync(Path.ChangeExtension(output, ".scene.json"));
    await File.WriteAllTextAsync(Path.ChangeExtension(output, ".report.json"), System.Text.Json.JsonSerializer.Serialize(result.Report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    await TerrainTexturePngWriter.WriteRgbaAsync(Path.ChangeExtension(output, ".map.png"), result.MapWidth, result.MapHeight, result.Map);
    await Export(result.Scene, args[4], catalog, output);
    return 0;
}
if (args.Length >= 3 && args[0] == "--check-themes")
{
    // Every theme texture must be a still a built level can copy; each theme's whole set must fit texture memory.
    var catalog = LevelCatalog.Load(args[2]);
    int bad = 0;
    foreach (var theme in Spyro.Editor.Core.Generation.LevelThemes.All)
    {
        var materials = new List<AuthoredMaterial>();
        foreach (var (role, t) in theme.Textures)
        {
            var level = catalog.FindByKey(t.Level);
            if (level == null) { Console.WriteLine($"{theme.Key} {role}: no level {t.Level}"); bad++; continue; }
            try { NativeStaticTerrainTextureSource.Capture(args[1], level.SourceWadEntry, t.Id); materials.Add(new(level.SourceWadEntry, t.Id)); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { Console.WriteLine($"{theme.Key} {role}: {t.Level} {t.Id} refused: {e.Message}"); bad++; }
        }
        materials = [.. materials.Distinct()];
        var vram = await NativeAuthoredPictureVram.CaptureAsync(args[1], materials[0], null);
        var (fits, free, reason) = vram.Test(materials);
        Console.WriteLine($"{theme.Key,-9} {materials.Count} textures: {(fits ? $"fit, {free:N0} bytes left" : "DO NOT FIT: " + reason)}");
        if (!fits) bad++;
    }
    Console.WriteLine(bad == 0 ? "THEMES_OK" : $"THEMES_BAD {bad}");
    return bad == 0 ? 0 : 1;
}
if (args.Length >= 4 && args[0] == "--static-textures")
{
    // --static-textures <disc> <repo> <out.txt> [level keys]: per level, the terrain textures a built level can carry
    // (still pictures in the usual layout), for study/dna_table.py. Slow: the disc is checked for every texture.
    var catalog = LevelCatalog.Load(args[2]);
    var keys = args.Length > 4 ? args[4..] : [.. Spyro.Editor.Core.Generation.LevelThemes.All.Select(t => t.Key)];
    using var writer = new StreamWriter(args[3]);
    foreach (string key in keys)
    {
        var level = catalog.FindByKey(key) ?? throw new InvalidDataException(key);
        var ok = new List<int>(); int misses = 0;
        for (int id = 0; id < 128 && misses < 40; id++)
        {
            try { NativeStaticTerrainTextureSource.Capture(args[1], level.SourceWadEntry, id); ok.Add(id); misses = 0; }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException or IndexOutOfRangeException) { misses++; }
        }
        writer.WriteLine($"{key}: {string.Join(",", ok)}"); writer.Flush();
        Console.WriteLine($"{key}: {ok.Count} still textures");
    }
    return 0;
}
if (args.Length >= 4 && args[0] == "--catalog")
{
    // --catalog <disc> <repo> <project folder>: the placeable objects by kind and level (names from the project's level cache).
    var catalog = LevelCatalog.Load(args[2]);
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var workspace = new Spyro.Editor.Core.Workspace.EditorWorkspace(args[3]);
    Dictionary<(string, int), (string, string)> index = [];
    foreach (var level in catalog.Levels)
    {
        string path = Path.Combine(args[3], "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(path)) continue;
        var mobys = Spyro.Editor.Core.Scene.MobyLoader.LoadCached(path).ToList();
        Spyro.Editor.Core.Scene.MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        foreach (var m in mobys.Where(m => m.TrueIndex >= 0)) index[(level.Key, m.TrueIndex)] = (m.DisplayLabel, m.CandidateKind);
    }
    var entries = UniversalMobyCatalogBuilder.Build(lib, catalog, (level, row) => index.TryGetValue((level, row), out var n) ? n : null);
    foreach (var g in entries.GroupBy(e => (e.Category, e.SubCategory)).OrderBy(g => g.Key.Category).ThenBy(g => g.Key.SubCategory))
    {
        Console.WriteLine($"== {g.Key.Category} / {g.Key.SubCategory} ({g.Count()})");
        foreach (var e in g.OrderBy(e => e.ClassId))
            Console.WriteLine($"   {e.ClassHex} {e.Name,-34} {(e.Placeable ? "ok " : "NO ")} levels: {string.Join(",", e.Variants.Where(v => v.RestrictionReason == null).Select(v => v.SourceLevelKey).Distinct())}" +
                (e.Placeable ? "" : " | " + e.RestrictionReason));
    }
    return 0;
}
if (args.Length >= 3 && args[0] == "--dump-exe")
{
    File.WriteAllBytes(args[2], UniversalDiscImage.Read(args[1]).Exe.ToArray());
    return 0;
}
if (args.Length >= 3 && args[0] == "--surface-kinds")
{
    var catalog = LevelCatalog.Load(args[2]);
    foreach (var level in catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64))
    {
        try
        {
            var src = PortalSourceDataLocator.LocateTerrainSurfaces(args[1], level);
            var byType = src.CollisionSurfaceTriangles.Where(t => t.SurfaceType >= 0).GroupBy(t => (t.SurfaceType, t.Param1, t.Param2));
            foreach (var g in byType)
            {
                var pts = g.SelectMany(t => new[] { t.P1, t.P2, t.P3 }).ToList();
                Console.WriteLine($"{level.Key,-14} type {g.Key.SurfaceType} ({g.Key.Param1},{g.Key.Param2}) x{g.Count()} first at {pts[0]}");
                if (g.Key.SurfaceType == 4)
                {
                    var slopes = g.Select(t =>
                    {
                        double ux = t.P2.X - t.P1.X, uy = t.P2.Y - t.P1.Y, uz = t.P2.Z - t.P1.Z, vx = t.P3.X - t.P1.X, vy = t.P3.Y - t.P1.Y, vz = t.P3.Z - t.P1.Z;
                        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                        return Math.Round(Math.Acos(Math.Abs(nz) / Math.Sqrt(nx * nx + ny * ny + nz * nz)) * 180 / Math.PI);
                    }).GroupBy(a => (int)(a / 5) * 5).OrderBy(x => x.Key).Select(x => $"{x.Key}-{x.Key + 4}:{x.Count()}");
                    Console.WriteLine("    slopes (deg) " + string.Join(" ", slopes));
                }
            }
        }
        catch (Exception e) { Console.WriteLine($"{level.Key}: {e.Message}"); }
    }
    return 0;
}
if (args.Length >= 2 && args[0] == "--scatter-facts")
{
    // --scatter-facts <repo> [treasure] [difficulty]: Random objects on a level of every kind, measured the way
    // study/objects.py measures the game's own levels, to be set beside its figures.
    var catalog = LevelCatalog.Load(args[1]);
    int treasure = args.Length > 2 ? int.Parse(args[2]) : 300;
    var difficulty = args.Length > 3 ? Enum.Parse<Spyro.Editor.Core.Generation.LevelDifficulty>(args[3]) : Spyro.Editor.Core.Generation.LevelDifficulty.Medium;
    static double Pct(List<double> v, double q) { v.Sort(); return v.Count == 0 ? 0 : v[Math.Min(v.Count - 1, (int)(q * v.Count))]; }
    static double D(Spyro.Editor.Core.Primitives.Vector3f a, Spyro.Editor.Core.Primitives.Vector3f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    static List<double> Nearest(List<Spyro.Editor.Core.Primitives.Vector3f> p) => p.Count < 2 ? [] : [.. p.Select(a => p.Where(b => b != a).Min(b => D(a, b)))];
    static List<int> Groups(List<Spyro.Editor.Core.Primitives.Vector3f> p, double link)
    {
        var left = Enumerable.Range(0, p.Count).ToList(); List<int> sizes = [];
        while (left.Count > 0)
        {
            var stack = new Stack<int>([left[^1]]); left.RemoveAt(left.Count - 1); int n = 0;
            while (stack.Count > 0) { int i = stack.Pop(); n++; foreach (int j in left.Where(j => D(p[i], p[j]) <= link).ToList()) { left.Remove(j); stack.Push(j); } }
            sizes.Add(n);
        }
        return sizes;
    }
    static string Share(List<int> sizes) { double all = sizes.Sum(); return all == 0 ? "-" : $"alone {sizes.Where(x => x == 1).Sum() / all:0%}, 2-3 {sizes.Where(x => x is 2 or 3).Sum() / all:0%}, 4+ {sizes.Where(x => x >= 4).Sum() / all:0%}"; }
    List<double> gemNear = [], foeNear = [], chestNear = [], gemWall = [], chestWall = [], foeWall = [];
    List<int> gemGroups = [], foeGroups = [], chestGroups = [];
    var colours = new Dictionary<int, int>(); int loose = 0, inChests = 0, fromFoes = 0, all = 0, gemCount = 0, chestCount = 0, foeCount = 0, levels = 0, oneColour = 0, manyGroups = 0;
    foreach (var style in Enum.GetValues<Spyro.Editor.Core.Generation.LevelStyle>())
        for (int k = 0; k < 2; k++)
        {
            long seed = 5000 + (int)style * 10 + k;
            var r = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(new() { Seed = seed, Style = style, Difficulty = difficulty }, catalog, source: RandomLevelCases.LookSource());
            var plan = LevelScatter.Make(r.Scene, new LevelScatter.Request(seed, LevelScatter.EnemiesFor(treasure, difficulty), LevelScatter.AnimalsFor(difficulty), treasure, 236)
            { Exit = true, ChestKinds = LevelScatter.Chests.ToHashSet(), Difficulty = difficulty }, r.Scene.GemSpots);
            var gems = plan.Gems.Select(g => g.Position).ToList();
            var foes = plan.Of(LevelScatter.Kind.Enemy).Select(i => i.Position).ToList();
            var chests = plan.Items.Where(i => i.Kind is LevelScatter.Kind.Basket or LevelScatter.Kind.StrongChest or LevelScatter.Kind.FlameChest or LevelScatter.Kind.Firework or LevelScatter.Kind.SpringChest or LevelScatter.Kind.LifeChest or LevelScatter.Kind.LockedChest).ToList();
            int l = plan.Gems.Sum(g => g.Value), c = chests.Sum(i => i.Reward), f = plan.Of(LevelScatter.Kind.Enemy).Sum(i => i.Reward);
            Console.WriteLine($"{style,-10} {seed}: treasure {plan.Treasure} = loose {l} in {gems.Count} gems + {chests.Count} chests {c} + {foes.Count} enemies {f}; " +
                $"{string.Join(" ", plan.Gems.GroupBy(g => g.Value).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}"))}; {string.Join(", ", plan.Items.GroupBy(i => i.Kind).Select(g => $"{g.Count()} {g.Key}"))} {string.Join(" ", plan.Notes)}");
            foreach (var g in plan.Gems) colours[g.Value] = colours.GetValueOrDefault(g.Value) + 1;
            loose += l; inChests += c; fromFoes += f; all += plan.Treasure; gemCount += gems.Count; chestCount += chests.Count; foeCount += foes.Count; levels++;
            gemNear.AddRange(Nearest(gems)); foeNear.AddRange(Nearest(foes)); chestNear.AddRange(Nearest([.. chests.Select(i => i.Position)]));
            gemGroups.AddRange(Groups(gems, 150)); foeGroups.AddRange(Groups(foes, 450)); chestGroups.AddRange(Groups([.. chests.Select(i => i.Position)], 200));
            gemWall.AddRange(gems.Select(p => (double)RandomLevelCases.WallDistance(r.Scene, p))); chestWall.AddRange(chests.Select(i => (double)RandomLevelCases.WallDistance(r.Scene, i.Position)));
            foeWall.AddRange(foes.Select(p => (double)RandomLevelCases.WallDistance(r.Scene, p)));
            foreach (var g in plan.Gems.Select((gem, i) => (gem, Group: plan.GemGroups[i])).GroupBy(x => x.Group).Where(g => g.Count() > 1)) { manyGroups++; if (g.Select(x => x.gem.Value).Distinct().Count() == 1) oneColour++; }
        }
    Console.WriteLine($"\n==== {levels} levels, treasure {treasure}, {difficulty} (the game's figures in brackets) ====");
    Console.WriteLine($"treasure: loose {loose / (double)all:0%} [31%], chests {inChests / (double)all:0%} [46%], enemies {fromFoes / (double)all:0%} [23%]");
    int n = colours.Values.Sum();
    Console.WriteLine($"loose gem colours {string.Join(", ", colours.OrderBy(c => c.Key).Select(c => $"{c.Key}: {c.Value / (double)n:0%}"))} [1: 62%, 2: 24%, 5: 10%, 10: 3%, 25: 1%]; value per loose gem {loose / (double)Math.Max(1, gemCount):0.00} [2.13]");
    Console.WriteLine($"per 100 treasure: enemies {100.0 * foeCount / all:0.0} [6.7], chests {100.0 * chestCount / all:0.0} [9.6], loose gems {100.0 * gemCount / all:0.0} [15.7]");
    Console.WriteLine($"gem nearest p25/p50/p75 {Pct(gemNear, .25):0}/{Pct(gemNear, .5):0}/{Pct(gemNear, .75):0} [81/103/134]; groups {Share(gemGroups)} [alone 19%, 2-3 58%, 4+ 23%]; groups of one colour {oneColour} of {manyGroups} [about 3 in 4]");
    Console.WriteLine($"enemy nearest p25/p50/p75 {Pct(foeNear, .25):0}/{Pct(foeNear, .5):0}/{Pct(foeNear, .75):0} [146/275/491]; groups {Share(foeGroups)} [alone 28%, 2-3 43%, 4+ 29%]");
    Console.WriteLine($"chest nearest p25/p50/p75 {Pct(chestNear, .25):0}/{Pct(chestNear, .5):0}/{Pct(chestNear, .75):0} [92/118/236]; groups {Share(chestGroups)} [alone 28%, 2-3 55%, 4+ 17%]");
    Console.WriteLine($"wall distance p25/p50/p75: gems {Pct(gemWall, .25):0}/{Pct(gemWall, .5):0}/{Pct(gemWall, .75):0} [54/96/202], chests {Pct(chestWall, .25):0}/{Pct(chestWall, .5):0}/{Pct(chestWall, .75):0} [64/97/190], enemies {Pct(foeWall, .25):0}/{Pct(foeWall, .5):0}/{Pct(foeWall, .75):0} [89/156/254]");
    return 0;
}
if (args.Length >= 5 && args[0] == "--model-colours")
{
    // --model-colours <disc> <repo> <level key> <class hex>: an animated object model's colour tables and how its faces use them.
    var catalog = LevelCatalog.Load(args[2]);
    var library = new UniversalSourceLibrary(args[1], catalog);
    byte[] package = library.Get(args[3]).Data.Package(Convert.ToInt32(args[4], 16));
    uint W(int at) => BitConverter.ToUInt32(package, at);
    int animations = (int)W(0), md = (int)W(0x34);
    Console.WriteLine($"{args[3]} class {args[4]}: package {package.Length} bytes sha {Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(package))[..12]}, {animations} animations, model data at 0x{md:X}");
    var tables = new Dictionary<int, (int Count, List<int> Animations)>();
    var faceTables = new HashSet<int>();
    for (int i = 0; i < animations; i++)
    {
        int animation = (int)W(0x38 + i * 4);
        if (animation <= 0) continue;
        int colours = md + (int)W(animation + 0x18), count = BitConverter.ToUInt16(package, animation + 2), faces = md + (int)W(animation + 0x14);
        if (!tables.TryGetValue(colours, out var t)) tables[colours] = t = (count, []);
        t.Animations.Add(i); tables[colours] = (Math.Max(t.Count, count), t.Animations);
        faceTables.Add(faces);
    }
    foreach (var (at, t) in tables.OrderBy(t => t.Key))
    {
        Console.WriteLine($"colour table at 0x{at:X}: {t.Count} colours, animations {string.Join(",", t.Animations)}");
        var rows = Enumerable.Range(0, t.Count).Select(k => W(at + 4 * k)).ToList();
        Console.WriteLine("   " + string.Join(" ", rows.Select(c => $"{c & 255:X2}{c >> 8 & 255:X2}{c >> 16 & 255:X2}/{c >> 24:X2}")));
    }
    foreach (int table in faceTables.OrderBy(x => x))
    {
        int end = table + 4 + (int)W(table), plain = 0, textured = 0; var used = new Dictionary<int, int>();
        for (int p2 = table + 4; p2 < end;)
        {
            uint word = W(p2), colour = W(p2 + 4);
            bool quad = (word & 0x80000000) != 0, tex = (word & 2) != 0 || quad && (word & 4) != 0;
            foreach (int ci in new[] { (int)(colour >> 19 & 511), (int)(colour >> 10 & 511), (int)(colour >> 1 & 511) }) used[ci] = used.GetValueOrDefault(ci) + 1;
            if (tex) textured++; else plain++;
            p2 += quad ? tex ? 24 : 12 : tex ? 20 : 8;
        }
        Console.WriteLine($"face table at 0x{table:X}: {plain} plain faces, {textured} textured; colour indexes used {used.Count} (max {used.Keys.Max()})");
        // The palettes its textured faces name (4-bit: 16 colours at VRAM (x, y)), with their texel use left out.
        byte[] vram = library.Get(args[3]).Data.Vram().ToArray();
        var cluts = new Dictionary<(int Clut, int Page), int>();
        for (int p2 = table + 4; p2 < end;)
        {
            uint word = W(p2);
            bool quad = (word & 0x80000000) != 0, tex = (word & 2) != 0 || quad && (word & 4) != 0;
            if (tex) { int t = p2 + (quad ? 12 : 8); var key = (BitConverter.ToUInt16(package, t + 2), BitConverter.ToUInt16(package, t + 6)); cluts[key] = cluts.GetValueOrDefault(key) + 1; }
            p2 += quad ? tex ? 24 : 12 : tex ? 20 : 8;
        }
        foreach (var ((clut, page), n) in cluts.OrderBy(c => c.Key.Clut))
        {
            int x = (clut & 63) * 16, y = clut >> 6, at = y * 1024 + (x - 512) * 2;
            string Row(int row) => string.Join(" ", Enumerable.Range(0, 16).Select(i => { ushort c = BitConverter.ToUInt16(vram, at + row * 1024 + 2 * i); return c == 0 ? "------" : $"{(c & 31) * 255 / 31:X2}{(c >> 5 & 31) * 255 / 31:X2}{(c >> 10 & 31) * 255 / 31:X2}"; }));
            Console.WriteLine($"  clut 0x{clut:X4} at ({x},{y}) page 0x{page:X4} mode {page >> 7 & 3}, {n} faces\n     {Row(0)}\n  +7 {Row(7)}");
        }
    }
    return 0;
}
if (args.Length >= 3 && args[0] == "--thief-colours")
{
    // --thief-colours <disc> <repo> [#RRGGBB]: every level's egg thieves: found by what they are, their own colour,
    // what a recolour changes (bytes, all inside the level's texture memory), and who else uses their palettes.
    var catalog = LevelCatalog.Load(args[2]);
    var library = new UniversalSourceLibrary(args[1], catalog);
    var colour = args.Length > 3 && Spyro.Editor.Core.Persistence.SpyroColourSettings.TryParse(args[3], out var given) ? given : ((byte)200, (byte)40, (byte)40);
    int levels = 0, failures = 0;
    foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0).OrderBy(l => l.LevelId))
    {
        byte[] bytes = library.Disc.EntryBytes(level.SourceWadEntry);
        var own = NativeEggThiefColours.OriginalColour(bytes);
        UniversalLevelData data; bool hasClass;
        try { data = new UniversalLevelData(bytes); hasClass = data.Registrations().Any(r => r.ClassId == NativeEggThiefColours.ThiefClass); } catch (Exception) { continue; }
        if (own == null && !hasClass) continue;
        levels++;
        byte[]? copy = NativeEggThiefColours.Recolour(bytes, colour);
        int differing = copy == null ? 0 : Enumerable.Range(0, bytes.Length).Count(i => bytes[i] != copy[i]);
        int first = copy == null ? -1 : Enumerable.Range(0, bytes.Length).First(i => bytes[i] != copy[i]), last = copy == null ? -1 : Enumerable.Range(0, bytes.Length).Last(i => bytes[i] != copy[i]);
        var after = copy == null ? null : NativeEggThiefColours.OriginalColour(copy);
        bool again = copy != null && NativeEggThiefColours.Recolour(copy, colour) == null;
        // Other models naming the thief's palettes.
        var mine = !hasClass ? [] : (NativeEggThiefColours.Palettes(data.Package(NativeEggThiefColours.ThiefClass)) ?? new Dictionary<ushort, int>()).Where(m => m.Value > 8).ToDictionary(m => m.Key, m => m.Value);
        List<string> sharers = [];
        foreach (var reg in data.Registrations().Where(r => r.ClassId != NativeEggThiefColours.ThiefClass))
        {
            byte[] package; try { package = data.Package(reg.ClassId); } catch (Exception) { continue; }
            for (int p2 = 2; p2 + 6 <= package.Length; p2 += 4)
                if (mine.ContainsKey(BitConverter.ToUInt16(package, p2)) && (BitConverter.ToUInt16(package, p2 + 4) & 0xFF80) == 0 && BitConverter.ToUInt16(package, p2 + 4) is > 0 and < 0x20)
                { sharers.Add($"0x{reg.ClassId:X3}"); break; }
        }
        bool ok = hasClass && own != null && copy != null && first >= 0x800 && last < 0x800 + 0x80000 && again && after != null &&
            Math.Abs(after.Value.R - colour.Item1) + Math.Abs(after.Value.G - colour.Item2) + Math.Abs(after.Value.B - colour.Item3) < 40;
        if (!ok) failures++;
        Console.WriteLine($"{level.Key,-14} class 0x21 {(hasClass ? "yes" : "NO ")} own {(own is { } o ? $"#{o.R:X2}{o.G:X2}{o.B:X2}" : "-")} -> {(after is { } a ? $"#{a.R:X2}{a.G:X2}{a.B:X2}" : "-")}: {differing} bytes differ (0x{first:X}..0x{last:X}), again {(again ? "same" : "CHANGES")}, palettes {string.Join(",", mine.Select(m => $"0x{m.Key:X4}x{m.Value}"))}, shared with {(sharers.Count == 0 ? "none" : string.Join(" ", sharers.Distinct()))} {(ok ? "" : "FAIL")}");
    }
    Console.WriteLine(failures == 0 ? $"THIEF_COLOURS_OK levels={levels}" : $"THIEF_COLOURS_FAILED {failures}");
    return failures == 0 ? 0 : 1;
}
if (args.Length >= 3 && args[0] == "--doorway-facts")
{
    // --doorway-facts <disc> <repo>: every homeworld doorway's objects and path as the doorway sees them (along: the way
    // Spyro walks in; side: to its left; up), in editor units. What a doorway needs clear in front of it.
    var catalog = LevelCatalog.Load(args[2]);
    var library = new UniversalSourceLibrary(args[1], catalog);
    foreach (var home in catalog.Levels.Where(l => l.LevelId % 10 == 0 && Spyro.Editor.Core.Scene.HomeworldPortalControlCatalog.ForLevel(l.Key).Count > 0))
    {
        var templates = NativeHubPortals.Templates(args[1], catalog, home.Key);
        var data = library.Get(home.Key).Data;
        foreach (var t in templates.Values.OrderBy(t => t.DestinationLevelId))
        {
            var (bx, by, bz) = t.BottomMiddle;
            double fx = Math.Cos(t.FacingAngle), fy = Math.Sin(t.FacingAngle);
            string Local(int x, int y, int z) => $"({((x - bx) * fx + (y - by) * fy) / 16:0},{(-(x - bx) * fy + (y - by) * fx) / 16:0},{(z - bz) / 16.0:0})";
            var points = Enumerable.Range(0, t.PointCount).Select(t.Point).ToArray();
            // Which way round the opening's points go seen from the side the normal points to (the game draws the sky
            // for points anticlockwise from the viewer): the signed area in (side, up).
            double area = 0;
            for (int k = 0; k < points.Length; k++)
            {
                var a = points[k]; var b = points[(k + 1) % points.Length];
                double au = -(a.X - bx) * fy + (a.Y - by) * fx, bu = -(b.X - bx) * fy + (b.Y - by) * fx;
                area += au * (b.Z - bz) - bu * (a.Z - bz);
            }
            Console.WriteLine($"{home.Key} -> {t.DestinationLevelId} {t.Control.DestinationName}: facing {t.FacingAngle * 180 / Math.PI:0}°, points {(area > 0 ? "anticlockwise" : "clockwise")} seen from behind the normal, opening {t.PointCount} points {string.Join(" ", points.Select(q => Local(q.X, q.Y, q.Z)))}");
            foreach (var (name, index) in new[] { ("path", t.Control.PathTrueIndex), ("name", t.Control.LetteringTrueIndex), ("return", t.Control.CompanionTrueIndex) })
            {
                int row = index - t.SourceMobyIndexBias;
                byte[] r = data.Row(row);
                int I(byte[] b, int at) => BitConverter.ToInt32(b, at);
                var captured = data.CaptureRow(home.Key, row);
                List<string> nodes = [];
                foreach (var (_, target) in captured.InternalPointers)
                {
                    var q = captured.Properties;
                    if (target + 8 > q.Length) continue;
                    int count = q[target];
                    if (count is < 1 or > 16 || q[target + 1] >= count || q[target + 2] != 0 || q[target + 3] != 0 || target + 8 + 16 * count > q.Length) continue;
                    for (int n = 0; n < count; n++) nodes.Add(Local(I(q, target + 8 + 16 * n), I(q, target + 12 + 16 * n), I(q, target + 16 + 16 * n)));
                }
                double turned = ((r[0x46] * 360.0 / 256 - t.FacingAngle * 180 / Math.PI) % 360 + 360) % 360;
                Console.WriteLine($"    {name,-6} class 0x{BitConverter.ToUInt16(r, 0x36):X3} at {Local(I(r, 0x0C), I(r, 0x10), I(r, 0x14))} yaw {r[0x46]} (from the facing {turned:0}°)" + (nodes.Count > 0 ? " nodes " + string.Join(" ", nodes) : ""));
                if (Environment.GetEnvironmentVariable("DOORWAY_WORDS") == "1")
                    Console.WriteLine("           words " + string.Join(" ", Enumerable.Range(0, Math.Min(captured.Properties.Length / 4, 20)).Select(k => I(captured.Properties, 4 * k).ToString("X"))) + " pointers " + string.Join(" ", captured.InternalPointers.Select(q => $"{q.Offset:X}->{q.Target:X}")));
            }
        }
    }
    return 0;
}
if (args.Length >= 3 && args[0] == "--balloon-facts")
{
    // --balloon-facts <disc> <repo>: every homeworld's balloonist and balloon rows beside the game's arrival tables
    // (USA 0x8006EA4C: where the balloon lands, 16 bytes a world; 0x8006EADC: where Spyro steps off, 8 bytes a world).
    var catalog = LevelCatalog.Load(args[2]);
    var library = new UniversalSourceLibrary(args[1], catalog);
    byte[] exe = library.Disc.Exe;
    int W(uint a) => BitConverter.ToInt32(exe, (int)(a - 0x80010000 + 0x800));
    foreach (var home in catalog.Levels.Where(l => l.LevelId % 10 == 0 && l.LevelId is >= 10 and <= 60))
    {
        int w = home.LevelId / 10 - 1;
        var data = library.Get(home.Key).Data;
        Console.WriteLine($"{home.Key}: lands at ({W(0x8006EA4C + 16 * (uint)w) / 16.0:0.#},{W(0x8006EA50 + 16 * (uint)w) / 16.0:0.#},{W(0x8006EA54 + 16 * (uint)w) / 16.0:0.#}) yaw {W(0x8006EA58 + 16 * (uint)w)}, Spyro steps off at ({W(0x8006EADC + 8 * (uint)w) / 16.0:0.#},{W(0x8006EAE0 + 8 * (uint)w) / 16.0:0.#})");
        for (int i = 0; i < data.Scene().RowCount; i++)
        {
            byte[] r = data.Row(i);
            int c = BitConverter.ToUInt16(r, 0x36);
            if (c is not ((>= 0xBB and <= 0xC0) or 0x1A0 or 0xFA)) continue;
            var t = data.CaptureRow(home.Key, i);
            Console.WriteLine($"    row {i} class 0x{c:X3} at ({BitConverter.ToInt32(r, 0x0C) / 16.0:0.#},{BitConverter.ToInt32(r, 0x10) / 16.0:0.#},{BitConverter.ToInt32(r, 0x14) / 16.0:0.#}) yaw {r[0x46]} state {r[0x48]} props {t.Properties.Length} bytes: {string.Join(" ", Enumerable.Range(0, Math.Min(t.Properties.Length / 4, 14)).Select(k => BitConverter.ToInt32(t.Properties, 4 * k).ToString("X")))} rows {string.Join(",", t.SourceRowIndices.Select(q => q.ToString()))}");
        }
    }
    return 0;
}
Console.Error.WriteLine("usage: --calibration <disc> <repo> <out.bin> | --export <scene.json> <disc> <repo> <out.bin>");
return 2;

static async Task Export(SceneDocument scene, string disc, LevelCatalog catalog, string output)
{
    var materials = scene.Surfaces.Select(s => s.Material).OfType<AuthoredMaterial>().Distinct().ToList();
    byte[] bundle = NativeAuthoredMesh.Compile(scene, materials);
    Console.WriteLine($"compiled: {scene.Surfaces.Count} faces, {materials.Count} materials, {bundle.Length:N0} bytes, sectors {NativeAuthoredMeshRenderer.LastReport}, draw {NativeAuthoredMeshRenderer.LastDrawPlan?.Describe()}");
    output = Path.GetFullPath(output);
    string work = Path.Combine(Path.GetDirectoryName(output)!, "export-work");
    Directory.CreateDirectory(work);
    var donors = new UniversalSourceLibrary(disc, catalog);
    string hash = await NativeAuthoredProjectExporter.ExportAsync(donors, catalog, [new(scene, [])], null, output, work, 65);
    Console.WriteLine($"exported {output} sha256 {hash}");
}
