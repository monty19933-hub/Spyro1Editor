using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

if (args.Length == 5 && args[0] == "--fingerprint")
{
    // A level's fingerprint in a process of its own (the gate compares it with the one made in the gate's process).
    var catalog = LevelCatalog.Load(args[4]);
    var r = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(new Spyro.Editor.Core.Generation.RandomLevelOptions
    { Seed = long.Parse(args[1]), Style = Enum.Parse<Spyro.Editor.Core.Generation.LevelStyle>(args[2]), Size = Enum.Parse<Spyro.Editor.Core.Generation.LevelSize>(args[3]) }, catalog);
    Console.WriteLine("FINGERPRINT " + r.Scene.Fingerprint());
    return 0;
}
// Gate (no disc): [repo root] [--maps <folder>] [--seeds N]
if (args.Length == 0 || args[0] is not ("--fingerprint" or "--calibration" or "--export" or "--surface-kinds" or "--dump-exe" or "--dump-textures" or "--export-random" or "--check-themes"))
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
    var result = Spyro.Editor.Core.Generation.RandomLevelGenerator.Generate(options, catalog, carriers);
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
