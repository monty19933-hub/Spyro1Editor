using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.YearOfTheDragon;

// Year of the Dragon level importer checks.
//   --source-free                                   decoder, face rules, ground kinds, merging, sector groups and start on
//                                                   a synthetic level (no disc)
//   --disc YOTD.bin USA.bin workspace [reference.scene.json]
//                                                   every level decodes; Lost Fleet's distant meshes match the pinned
//                                                   reader; Lost Fleet imports into Artisans and (with a reference scene)
//                                                   matches the hand-built Lost Fleet's faces, positions and flags
//   --import YOTD.bin USA.bin workspace level target out-dir
//                                                   one import: out-dir/<target>.scene.json, pictures, report
//   --disc-qa USA.bin workspace                     as --disc, the Year of the Dragon disc from YOTD_DISC or the workspace
//   --survey YOTD.bin                               decode every level, one line each
//   --all YOTD.bin USA.bin workspace target         import every level for the target (pictures to a temporary folder)
//   --vram USA.bin workspace target scene.json      texture memory left after a scene's materials and the rescue scenes
static int Fail(string message) { Console.WriteLine("FAILED: " + message); return 1; }

if (args.Length == 1 && args[0] == "--source-free") return SourceFree.Run();
if (args.Length == 1 && args[0] == "--level-fill") { LevelFillCases.Run(); return 0; }
if (args.Length == 2 && args[0] == "--survey")
{
    var disc = new YearOfTheDragonDisc(args[1]);
    foreach (var level in YearOfTheDragonLevels.Read(disc)) Console.WriteLine(Survey.Line(disc, level));
    return 0;
}
if (args.Length == 7 && args[0] == "--import")
{
    var catalog = LevelCatalog.Load(args[3]);
    var level = YearOfTheDragonLevels.Find(args[4]);
    var binding = Survey.Binding(args[2], catalog, args[5]);
    Directory.CreateDirectory(args[6]);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var result = await YearOfTheDragonLevelImport.ImportAsync(args[1], level, args[2], catalog, binding,
        new() { PictureFolder = Path.Combine(args[6], YearOfTheDragonLevelImport.Slug(level.Name)) }, new Progress<string>(m => Console.WriteLine($"[{watch.Elapsed.TotalSeconds,5:0.0}s] {m}")));
    File.WriteAllBytes(Path.Combine(args[6], args[5] + ".scene.json"), result.Scene.Serialize());
    File.WriteAllLines(Path.Combine(args[6], args[5] + ".report.txt"), result.Report.Lines());
    foreach (string line in result.Report.Lines()) Console.WriteLine(line);
    Console.WriteLine($"imported in {watch.Elapsed.TotalSeconds:0}s");
    return 0;
}
if (args.Length == 5 && args[0] == "--vram")
{
    // --vram USA.bin workspace target scene.json: texture memory left after a scene's materials and the rescue scenes
    var catalog = LevelCatalog.Load(args[2]);
    var scene = JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(args[4]))!;
    var materials = scene.Surfaces.Where(s => s.Material != null).Select(s => s.Material!).Distinct().ToList();
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var vram = await NativeAuthoredPictureVram.CaptureAsync(args[1], materials[0], lib.Get(args[3]).Data.Vram().ToArray());
    for (int n = materials.Count; n >= Math.Max(1, materials.Count - 30); n -= 6)
        Console.WriteLine($"{n} materials: {vram.Test(materials.Take(n).ToList())}");
    return 0;
}
if (args.Length is 4 or 5 && args[0] == "--disc") return await DiscBacked.Run(args[1], args[2], args[3], args.Length == 5 ? args[4] : null);
if (args.Length == 5 && args[0] == "--all")
{
    // --all YOTD.bin USA.bin workspace target: every level imported for the target (pictures in a temporary folder)
    var catalog = LevelCatalog.Load(args[3]);
    var disc = new YearOfTheDragonDisc(args[1]);
    var lib = new UniversalSourceLibrary(args[2], catalog);
    var carriers = YearOfTheDragonLevelImport.Carriers(lib, catalog, [.. catalog.Levels.Select(l => l.Key)], args[2]);
    var binding = Survey.Binding(args[2], catalog, args[4]);
    var vram = await NativeAuthoredPictureVram.CaptureAsync(args[2], new(carriers[0].Wad, carriers[0].Id), lib.Get(args[4]).Data.Vram().ToArray());
    string folder = Path.Combine(Path.GetTempPath(), "yotd-all-" + Guid.NewGuid().ToString("N"));
    try
    {
        foreach (var level in YearOfTheDragonLevels.Read(disc))
        {
            try
            {
                var r = await YearOfTheDragonLevelImport.BuildAsync(YearOfTheDragonLevelData.Read(disc, level), binding, carriers, vram.Test, new() { PictureFolder = folder },
                    killHeight: NativeKillHeights.Minimum(lib.Disc.Exe, binding.LevelId));
                var p = r.Report;
                Console.WriteLine($"{level.Index,2} {level.Name,-23} OK  faces {p.Faces,5} sectors {p.YotdSectors,3}->{p.NativeSectors,3} textures {p.Textures,3}->{p.KeptTextures,3} " +
                    $"free {p.FreeTextureBytes,7:N0} terrain {p.TerrainBytes,7:N0} lift {p.Lift,4:0} ground [{string.Join(", ", p.GroundKinds.Select(k => $"{k.Value} {k.Key}"))}] start {p.SpawnSource.Split(' ').Skip(5).FirstOrDefault()} warnings {p.Warnings.Count}");
            }
            catch (YearOfTheDragonImportException e) { Console.WriteLine($"{level.Index,2} {level.Name,-23} REFUSED {e.Message}"); }
        }
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    return 0;
}
if (args.Length == 3 && args[0] == "--disc-qa")
{
    // The regression runner's source QA: USA.bin workspace; the Year of the Dragon disc is YOTD_DISC or the workspace's.
    string yotd = Environment.GetEnvironmentVariable("YOTD_DISC") is { Length: > 0 } configured ? configured
        : YearOfTheDragonDiscLocator.Find(new Spyro.Editor.Core.Workspace.EditorWorkspace(args[2]));
    if (yotd.Length == 0) return Fail("No Spyro: Year of the Dragon disc: set YOTD_DISC or choose it in the editor (Music > Year of the Dragon disc).");
    return await DiscBacked.Run(yotd, args[1], args[2], null);
}
Console.WriteLine("usage: --source-free | --disc YOTD.bin USA.bin workspace [reference.scene.json] | --import YOTD.bin USA.bin workspace level target out | --survey YOTD.bin");
return 2;

static class Survey
{
    public static string Line(YearOfTheDragonDisc disc, YearOfTheDragonLevel level)
    {
        var d = YearOfTheDragonLevelData.Read(disc, level);
        var faces = d.Sectors.SelectMany(s => s.NearFaces).ToList();
        var pts = faces.SelectMany(f => f.Corners).ToList();
        return $"{level.Index,2} {level.WadEntry} {level.Name,-24} {level.Kind,-9} sectors {d.Sectors.Count,3} ({d.Sectors.Count(s => s.FarOnly)} far-only) faces {faces.Count,5} " +
            $"textures {d.Textures.Count,3}/{faces.Select(f => f.Texture).Distinct().Count(),3} collision {d.Collision.Count,5} kinds [{string.Join("; ", d.GroundKinds.Select(k => k.Label))}] " +
            $"objects {d.Objects.Count} sky {d.SkyBackground} x {pts.Min(p => p.X)}..{pts.Max(p => p.X)} y {pts.Min(p => p.Y)}..{pts.Max(p => p.Y)} z {pts.Min(p => p.Z)}..{pts.Max(p => p.Z)}";
    }

    /// <summary>A blank-level binding for a retail level (as Start Blank makes it).</summary>
    public static SceneTargetBinding Binding(string usa, LevelCatalog catalog, string key)
    {
        var target = catalog.FindByKey(key) ?? throw new ArgumentException("Unknown target " + key);
        int prefix = new UniversalLevelData(UniversalDiscImage.Read(usa).EntryBytes(target.SourceWadEntry)).Scene().RowCount;
        return new(target.Key, target.LevelId, target.SourceWadEntry, target.SourceWadEntry - 1, UniversalSourceLibrary.OriginalUsaSha256, prefix);
    }
}
