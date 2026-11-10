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
        Console.WriteLine($"doorways: {string.Join(", ", built.Portals!.Select(d => $"{d.DestinationLevelId} at {d.Position} heading {d.Heading}"))} {string.Join(" ", problems)}");
        r = r with { Scene = built };
    }
    var faces = r.Scene.Surfaces.Select(s => new
    {
        p = s.Vertices.Select(v => new[] { v.X, v.Y, v.Z }).ToArray(),
        c = (s.NearColors ?? []).Select(c => new[] { (int)c.R, c.G, c.B }).ToArray(),
        t = s.Material == null ? null : $"{keyOf.GetValueOrDefault(s.Material.SourceWadEntry, "?")}:{s.Material.TextureId}",
        k = s.Kind?.Type,
    });
    await File.WriteAllTextAsync(args[5], System.Text.Json.JsonSerializer.Serialize(new
    {
        name = r.Scene.Name, spawn = new[] { r.Scene.Spawn.X, r.Scene.Spawn.Y, r.Scene.Spawn.Z }, heading = r.Scene.Heading,
        gems = (r.Scene.GemSpots ?? []).Select(g => new[] { g.X, g.Y, g.Z }).ToArray(),
        links = r.Report.Links.Select(l => new { l.Kind, from = new[] { l.From.X, l.From.Y, l.From.Z }, to = new[] { l.To.X, l.To.Y, l.To.Z } }).ToArray(), faces,
    }));
    await TerrainTexturePngWriter.WriteRgbaAsync(Path.ChangeExtension(args[5], ".map.png"), r.MapWidth, r.MapHeight, r.Map);
    return 0;
}
// Gate (no disc): [repo root] [--maps <folder>] [--seeds N]
if (args.Length == 0 || args[0] is not ("--fingerprint" or "--dump" or "--static-textures" or "--catalog" or "--calibration" or "--export" or "--surface-kinds" or "--dump-exe" or "--dump-textures" or "--export-random" or "--check-themes" or "--doorway-facts"))
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
            Console.WriteLine($"{home.Key} -> {t.DestinationLevelId} {t.Control.DestinationName}: opening {t.PointCount} points {string.Join(" ", points.Select(q => Local(q.X, q.Y, q.Z)))}");
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
                Console.WriteLine($"    {name,-6} class 0x{BitConverter.ToUInt16(r, 0x36):X3} at {Local(I(r, 0x0C), I(r, 0x10), I(r, 0x14))} yaw {r[0x46]}" + (nodes.Count > 0 ? " nodes " + string.Join(" ", nodes) : ""));
            }
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
