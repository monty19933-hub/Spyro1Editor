using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

// Crash-style level research (work in progress).
//   --carriers USA.bin workspace level   terrain textures of a level that can carry a picture (no mirrored tiles)
//   --trial USA.bin workspace out.bin pictures-dir   a cleared Dark Hollow with a large textured test field
//   --village USA.bin workspace out.bin pictures-dir   WUMPA VILLAGE (geometry only) in a cleared Dark Hollow, with
//     the faces, sectors and simulated close-up queue load written next to the image
if (args.Length == 4 && args[0] == "--carriers")
{
    var lib = new UniversalSourceLibrary(args[1], LevelCatalog.Load(args[2]));
    var data = lib.Get(args[3]).Data;
    var model = data.Subfiles()[1];
    var list = data.Bytes.AsSpan(model.Offset, model.Length);
    var records = NativeTerrainTextureImageWriter.Records(list);
    var groups = NativeTerrainTextureImageWriter.SharingGroups(NativeTerrainTextureImageWriter.Footprint(list));
    for (int i = 0; i < records.Count; i++)
    {
        var r = records[i];
        int group = groups.Select((g, k) => (g, k)).First(x => x.g.Contains(i)).k;
        Console.WriteLine($"{i,3}: {(r == null ? "no" : r.Descriptors.Any(d => d.Mirrored) ? "mirrored" : "ok")} group {group} size {groups[group].Count}");
    }
    return;
}
if (args.Length == 5 && args[0] == "--trial")
{
    var catalog = LevelCatalog.Load(args[2]);
    var target = catalog.FindByKey("darkhollow")!;
    var lib = new UniversalSourceLibrary(args[1], catalog);
    int rows = lib.Get("darkhollow").Data.Scene().RowCount;
    string Pic(int n) => Path.Combine(args[4], $"{n:000}.png");
    int dh = target.SourceWadEntry;
    AuthoredMaterial M(int carrier, int picture) => new(dh, carrier) { Image = Pic(picture) };
    var cobble = M(3, 12); var grass = M(4, 185); var water = M(5, 290); var stone = M(6, 263); var brick = M(7, 101); var straw = M(8, 252); var wood = M(9, 148);
    List<AuthoredSurface> surfaces = [];
    ColorRgba lit = new(128, 128, 128), dark = new(90, 90, 100);
    string Id() => Guid.NewGuid().ToString("N");
    void Floor(float x0, float y0, float x1, float y1, float z, AuthoredMaterial m) =>
        surfaces.Add(new(Id(), [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)], lit, m));
    void Wall(float ax, float ay, float bx, float by, float z0, float z1, AuthoredMaterial m) =>
        surfaces.Add(new(Id(), [new(ax, ay, z0), new(bx, by, z0), new(bx, by, z1), new(ax, ay, z1)], lit, m, [dark, dark, lit, lit], [dark, dark, lit, lit]));
    const float T = 192, x0 = 1200, y0 = 1200, z = 700; const int N = 20;
    for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
    {
        var m = (i == 9 || i == 10 || j == 9 || j == 10) ? cobble : (i >= 14 && j >= 14) ? water : grass;
        Floor(x0 + i * T, y0 + j * T, x0 + (i + 1) * T, y0 + (j + 1) * T, z, m);
    }
    // Perimeter walls (two courses of 192), drawn on the inside (right of a->b going clockwise seen from above).
    float x1 = x0 + N * T, y1 = y0 + N * T;
    for (int k = 0; k < N; k++)
        foreach (var (z0, z1) in new[] { (z, z + 192f), (z + 192f, z + 384f) })
        {
            Wall(x0 + k * T, y0, x0 + (k + 1) * T, y0, z0, z1, stone);
            Wall(x1, y0 + k * T, x1, y0 + (k + 1) * T, z0, z1, stone);
            Wall(x1 - k * T, y1, x1 - (k + 1) * T, y1, z0, z1, stone);
            Wall(x0, y1 - k * T, x0, y1 - (k + 1) * T, z0, z1, stone);
        }
    // Three brick houses with straw roofs (flat tops to stand on) and a wooden platform.
    void House(float hx, float hy, float w, float h)
    {
        Wall(hx, hy, hx, hy + w, z, z + h, brick); Wall(hx, hy + w, hx + w, hy + w, z, z + h, brick);
        Wall(hx + w, hy + w, hx + w, hy, z, z + h, brick); Wall(hx + w, hy, hx, hy, z, z + h, brick);
        Floor(hx, hy, hx + w, hy + w, z + h, straw);
    }
    House(1400, 1400, 192, 128); House(2600, 1500, 256, 192); House(1500, 2700, 192, 160);
    Floor(2400, 2400, 2592, 2592, z + 96, wood);
    var doc = SceneDocument.New("WUMPA TEST") with
    {
        Spawn = new(x0 + 9.5f * T + 96, y0 + 9.5f * T + 96, z + 32), Heading = 0, Surfaces = surfaces,
        TargetBinding = new(target.Key, target.LevelId, target.SourceWadEntry, target.SourceWadEntry - 1, UniversalSourceLibrary.OriginalUsaSha256, rows),
    };
    Console.WriteLine($"faces {surfaces.Count}");
    var compiled = await NativeClearedLevelCompilation.CaptureAsync(args[1], doc, catalog, args[3]);
    Console.WriteLine($"level data {compiled.CopyLevelData().Length} overlay {compiled.CopyOverlay().Length}; image {args[3]}");
    Console.WriteLine("CRASH_LEVEL_TRIAL_OK");
    return;
}
if (args.Length == 5 && args[0] == "--village")
{
    var catalog = LevelCatalog.Load(args[2]);
    var target = catalog.FindByKey("darkhollow")!;
    var lib = new UniversalSourceLibrary(args[1], catalog);
    int rows = lib.Get("darkhollow").Data.Scene().RowCount;
    var blank = SceneDocument.New(WumpaVillage.Name) with
    {
        TargetBinding = new(target.Key, target.LevelId, target.SourceWadEntry, target.SourceWadEntry - 1, UniversalSourceLibrary.OriginalUsaSha256, rows),
    };
    var doc = WumpaVillage.Scene(blank, target.SourceWadEntry, catalog.FindByKey("townsquare")!.SourceWadEntry, args[4]);
    var surfaces = doc.Surfaces;
    // Faces as text for a quick top-down check: carrier, then x y z per corner.
    File.WriteAllLines(args[3] + ".faces.txt", surfaces.Select(f => $"{f.Material?.TextureId ?? -1} " + string.Join(" ", f.Vertices.Select(v => $"{v.X} {v.Y} {v.Z}"))));
    Console.WriteLine($"faces {surfaces.Count}");
    try { await NativeClearedLevelCompilation.CaptureAsync(args[1], doc, catalog, args[3]); }
    catch (InvalidDataException e) { Console.WriteLine(e.Message); }
    finally
    {
        var (mx, my, load) = NativeAuthoredMeshRenderer.LastLoadMap;
        if (load != null)
            File.WriteAllLines(args[3] + ".load.txt", Enumerable.Range(0, load.GetLength(1)).Select(j =>
                $"{my + j * 128}:{mx}:" + string.Join(" ", Enumerable.Range(0, load.GetLength(0)).Select(i => load[i, j]))));
    }
    Console.WriteLine($"sectors, heaviest close-up queue, at: {NativeAuthoredMeshRenderer.LastReport}");
    // Sectors with their textured faces (centre, radius, then per face: corner count, corners, normal).
    File.WriteAllLines(args[3] + ".sectors.txt", NativeAuthoredMeshRenderer.LastQueueSectors.Select(q =>
        $"{q.X} {q.Y} {q.Z} {q.R}|" + string.Join("|", q.Counts.Select((n, i) => $"{n} " +
            string.Join(" ", q.Corners.Skip(q.Counts.Take(i).Sum() * 3).Take(n * 3)) + " " + string.Join(" ", q.Normals.Skip(i * 3).Take(3))))));
    Console.WriteLine("CRASH_LEVEL_VILLAGE_OK");
    return;
}
if (args.Length == 3 && args[0] == "--arena")
{
    // Dynamic object slots (spawned drops) left in a built image's level: --arena image.bin wad-entry
    var data = new UniversalLevelData(UniversalDiscImage.Read(args[1]).EntryBytes(int.Parse(args[2])));
    var sc = data.Scene();
    Console.WriteLine($"rows {sc.RowCount} rowsOffset 0x{sc.RowsOffset:X} arenaEnd 0x{sc.ArenaEnd:X} dynamic slots {UniversalMobyComposer.ArenaSlots(data)}");
    return;
}
{
    // Source-free: the village's terrain compiles within the game's limits (no disc needed for the mesh), walls and
    // floors get the game's collision winding, and built levels keep the retail spare-object arena.
    int checks = 0;
    void Check(bool ok, string what) { checks++; if (!ok) throw new Exception(what); }
    var blank = SceneDocument.New(WumpaVillage.Name) with { TargetBinding = new("darkhollow", 12, 14, 13, UniversalSourceLibrary.OriginalUsaSha256, 128) };
    var doc = WumpaVillage.Scene(blank, 14, 16, Path.GetTempPath());
    var materials = doc.Surfaces.Select(f => f.Material!).Distinct().ToArray();
    Check(materials.Length == WumpaVillage.Materials.Length && doc.Surfaces.Count <= SceneDocument.MaxSurfaces, "village materials and face count");
    byte[] bundle = NativeAuthoredMesh.Compile(doc, materials);
    var (sectors, load, _) = NativeAuthoredMeshRenderer.LastReport;
    Check(bundle.Length <= NativeAuthoredMesh.MaxSurfaceBytes && sectors <= NativeAuthoredMeshRenderer.MaxSectors, "village fits the level data and sector lists");
    Check(load <= NativeAuthoredMeshRenderer.SubdivisionQueueEntries - NativeAuthoredMeshRenderer.SubdivisionMargin, "village close-up queues");
    Check(NativeAuthoredMeshRenderer.LastQueueSectors.All(q => q.R < 0x1FFF), "sector radii");
    // A wall visible from the south (corners top-b, top-a, bottom-a) is stored with its normal into the wall (north);
    // a floor visible from above is stored clockwise from above.
    static Vector3f N((Vector3f A, Vector3f B, Vector3f C) t) { var u = new Vector3f(t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z); var v = new Vector3f(t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z); return new(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X); }
    var wall = NativeAuthoredMesh.CollisionWinding(new(100, 0, 64), new(0, 0, 64), new(0, 0, 0));
    Check(N(wall).Y > 0 && wall.A.Z == 0, "wall winding reversed from its visible side, lowest corner first");
    var floor = NativeAuthoredMesh.CollisionWinding(new(0, 0, 10), new(100, 0, 10), new(100, 100, 10));
    Check(N(floor).Z < 0, "floor stored clockwise from above");
    Check(UniversalMobyComposer.BuiltLevelDynamicSlots(65) == 91 && UniversalMobyComposer.BuiltLevelDynamicSlots(200) == 56 &&
        UniversalMobyComposer.BuiltLevelDynamicSlots(250) == UniversalMobyComposer.MinimumDynamicSlots, "built-level spare object slots");
    Check(WumpaVillage.Objects.Length <= 71, "village objects fit Dark Hollow's free save rows");
    Console.WriteLine($"CRASH_LEVEL_FREE_OK checks={checks} faces={doc.Surfaces.Count} sectors={sectors} queue={load}");
}
