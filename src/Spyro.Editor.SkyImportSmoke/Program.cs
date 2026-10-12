using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Sky;

// Sky import: panoramas and sky models become native Spyro 1 sky blocks.
//   (no arguments)        source-free checks
//   --source <disc .bin>  also rebuilds every retail sky from its own panorama
//   --out <folder>        writes the pictures it makes there
int checks = 0;
void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("check failed: " + what); checks++; }
string? source = null, outDir = null, skyScene = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--source") source = args[++i];
    else if (args[i] == "--out") outDir = args[++i];
    else if (args[i] == "--sky-scene") skyScene = args[++i];
}
if (outDir != null) Directory.CreateDirectory(outDir);
async Task Save(SkyImage image, string name)
{
    if (outDir != null) await TerrainTexturePngWriter.WriteRgbaAsync(Path.Combine(outDir, name), image.Width, image.Height, image.Pixels);
}

// A test panorama: sky blue above (lighter towards the horizon), green below, a red patch facing heading 0 (+X) on the
// horizon, a yellow one facing +Y (a quarter turn anticlockwise: left of the middle), a white sun high up behind (-X).
SkyImage TestPanorama(int width)
{
    int height = width / 2;
    var pixels = new Rgba32[width * height];
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double u = (x + 0.5) / width, v = (y + 0.5) / height;
            Rgba32 c = v < 0.5 ? new((byte)(60 + 140 * v * 2), (byte)(120 + 100 * v * 2), 230, 255) : new(40, 140, 50, 255);
            if (Math.Abs(u - 0.5) < 0.03 && Math.Abs(v - 0.5) < 0.06) c = new(230, 20, 20, 255);
            if (Math.Abs(u - 0.25) < 0.03 && Math.Abs(v - 0.5) < 0.06) c = new(240, 220, 20, 255);
            if (Math.Abs(u - 0.0) < 0.02 || Math.Abs(u - 1.0) < 0.02) if (Math.Abs(v - 0.2) < 0.04) c = new(255, 255, 255, 255);
            pixels[y * width + x] = c;
        }
    return new SkyImage(width, height, pixels);
}

// The colour the game shows in a direction: the native triangle around it, interpolated.
Rgba32 Seen(byte[] block, SkyVector d)
{
    foreach (var t in NativeSkyPreview.Triangles(block))
    {
        double volume = t.A.Dot(t.B.Cross(t.C));
        double wa = t.B.Cross(t.C).Dot(d) / volume, wb = t.C.Cross(t.A).Dot(d) / volume, wc = t.A.Cross(t.B).Dot(d) / volume, s = wa + wb + wc;
        if (wa < 0 || wb < 0 || wc < 0 || s <= 0) continue;
        wa /= s; wb /= s; wc /= s;
        return new((byte)(t.ColorA.R * wa + t.ColorB.R * wb + t.ColorC.R * wc), (byte)(t.ColorA.G * wa + t.ColorB.G * wb + t.ColorC.G * wc),
            (byte)(t.ColorA.B * wa + t.ColorB.B * wb + t.ColorC.B * wc), 255);
    }
    throw new InvalidOperationException("No sky triangle covers a direction.");
}
bool Near(Rgba32 c, int r, int g, int b, int tolerance = 60) => Math.Abs(c.R - r) + Math.Abs(c.G - g) + Math.Abs(c.B - b) <= tolerance;

// The game's limits on every part (USA 0x8004EBA8..0x8004EFFC): corners and colours by 10-bit byte offset, packed
// corners in range, culling sphere well in front of the camera's plane for the part's direction.
void Limits(byte[] block, string what)
{
    Check(Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(block), what + ": a valid native sky block");
    int parts = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(8));
    Check(parts is > 0 and <= 254, what + ": at most 254 parts (one view group lists them all)");
    var triangles = NativeSkyPreview.Triangles(block);
    for (int p = 0; p < parts; p++)
    {
        int at = 4 + BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12 + 4 * p));
        int vertices = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(at + 12)), colors = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(at + 18));
        int cx = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at)), cy = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at + 2));
        int radius = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at + 4)), cz = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at + 6));
        if (vertices > 256 || colors > 256) Check(false, what + ": part within 256 corners and colours");
        double distance = Math.Sqrt((double)cx * cx + (double)cy * cy + (double)cz * cz);
        if (radius > 0.36 * distance) Check(false, $"{what}: part {p} radius {radius} small against its distance {distance:0}");
        // Every corner inside its culling sphere.
        foreach (var t in triangles.Where(t => t.Part == p))
            foreach (var q in new[] { t.A, t.B, t.C })
            {
                double dx = -q.Y - cx, dy = -q.Z - cy, dz = q.X - cz;
                if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > radius) Check(false, $"{what}: part {p} corner outside its culling sphere");
            }
    }
    checks += 3;
}

// --sky-scene <scene.json>: gives a saved built level the test panorama as its own sky (for an in-game check).
if (skyScene != null)
{
    var block = NativeSkyBuilder.Build(SkyPanorama.FromImage(TestPanorama(2048)), new()).Block;
    var scene = SceneDocument.Read(skyScene) with { CustomSky = block, SkySourceWadEntry = null };
    await scene.SaveAsync(skyScene);
    Console.WriteLine($"SKY_SCENE_OK {skyScene} {block.Length} bytes");
    return;
}

// 1. Panorama → sky: budgets, limits, orientation, turning.
var panorama = SkyPanorama.FromImage(TestPanorama(4096));
Check(panorama.Notes.Count == 0 && panorama.Width == 2048, "a 2:1 picture is taken without remarks, at 2048 wide");
foreach (int budget in new[] { SkyBuildOptions.MinTriangles, 1500, 2200, SkyBuildOptions.DefaultTriangles, SkyBuildOptions.MaxTriangles })
{
    var built = NativeSkyBuilder.Build(panorama, new(budget));
    Console.WriteLine($"budget {budget}: {built.Triangles} triangles, {built.Vertices} corners, {built.Colors} colours, {built.Parts} parts, {built.Block.Length} bytes");
    // At the smallest budget the spacing limit (no triangle wider than 12°) may need a few more.
    Check(budget == SkyBuildOptions.MinTriangles ? built.Triangles >= budget && built.Triangles <= budget * 11 / 10 : built.Triangles == budget, $"budget {budget} gives that many triangles");
    Check(NativeSkyPreview.Triangles(built.Block).Count == built.Triangles, $"budget {budget}: the block holds them all");
    Limits(built.Block, $"budget {budget}");
    Check(built.Block.Length < 64 * 1024, $"budget {budget}: under 64 KB like the game's own skies");
}
var sky = NativeSkyBuilder.Build(panorama, new());
Rgba32 east = Seen(sky.Block, new(1, 0, 0)), north = Seen(sky.Block, new(0, 1, 0)), up = Seen(sky.Block, new(0, 0, 1)), down = Seen(sky.Block, new(0, 0, -1));
Console.WriteLine($"seen: +X {east}, +Y {north}, up {up}, down {down}");
Check(Near(east, 230, 20, 20, 120), "heading 0 (+X) shows the middle of the picture (red)");
Check(Near(north, 240, 220, 20, 120), "+Y (a quarter turn anticlockwise) shows the left quarter of the picture (yellow): not mirrored");
Check(Near(down, 40, 140, 50) && up.B > 200, "the top of the picture is up, the bottom down");
var sun = Seen(sky.Block, SkyDirections.FromUv(0.0, 0.2));
Check(Near(sun, 255, 255, 255, 120), "the sun behind and high up");
var turned = NativeSkyBuilder.Build(panorama, new(TurnDegrees: 90));
Check(Near(Seen(turned.Block, new(0, 1, 0)), 230, 20, 20, 120), "Turn 90° brings the middle of the picture round to +Y");
Console.WriteLine($"background {sky.Block[4]},{sky.Block[5]},{sky.Block[6]}");
Check(Near(new Rgba32(sky.Block[4], sky.Block[5], sky.Block[6], 255), 40, 140, 50, 40), "the background colour is about the ground below the horizon");
var preview = NativeSkyPreview.Render(sky.Block, 512);
var expected = panorama.ToImage(512);
double error = preview.Pixels.Zip(expected.Pixels).Average(p => (Math.Abs(p.First.R - p.Second.R) + Math.Abs(p.First.G - p.Second.G) + Math.Abs(p.First.B - p.Second.B)) / 3.0);
Console.WriteLine($"preview against the panorama: mean difference {error:0.0} per channel");
Check(error < 8, "the built sky looks like the panorama (mean difference under 8 per channel)");
await Save(expected, "test-panorama.png");
await Save(preview, "test-sky-preview.png");

// 2. Odd pictures: not 2:1 → a remark, still built.
var square = SkyPanorama.FromImage(TestPanorama(512) is var p2 ? new SkyImage(512, 512, Enumerable.Range(0, 512 * 512).Select(i => p2.Pixels[(i / 512 / 2) * 512 + i % 512]).ToArray()) : null!);
Check(square.Notes.Count == 1 && square.Notes[0].Contains("not twice as wide"), "a square picture gets a remark");
Limits(NativeSkyBuilder.Build(square, new(1000)).Block, "square picture");

// 3. Sky models: a box around the viewer (Y-up, as OBJ and FBX usually are), each side its own colour. Game +X is the
// file's +x, game +Y is the file's -z, game up is the file's +y.
SkyModelTriangle Tri(double[] a, double[] b, double[] c, Rgba32 color) =>
    new(new(a[0], a[1], a[2], 0, 0, null), new(b[0], b[1], b[2], 0, 0, null), new(c[0], c[1], c[2], 0, 0, null), -1, color);
List<SkyModelTriangle> box = [];
void Side(double[] o, double[] e1, double[] e2, Rgba32 color)
{
    double[] P(double s, double t) => [o[0] + e1[0] * s + e2[0] * t, o[1] + e1[1] * s + e2[1] * t, o[2] + e1[2] * s + e2[2] * t];
    box.Add(Tri(P(0, 0), P(1, 0), P(1, 1), color)); box.Add(Tri(P(0, 0), P(1, 1), P(0, 1), color));
}
Rgba32 red = new(220, 30, 30, 255), green = new(30, 200, 30, 255), blue = new(30, 30, 220, 255), white = new(240, 240, 240, 255), black = new(10, 10, 10, 255), orange = new(240, 140, 20, 255);
Side([10, -10, -10], [0, 20, 0], [0, 0, 20], red);      // file +x
Side([-10, -10, -10], [0, 20, 0], [0, 0, 20], orange);  // file -x
Side([-10, 10, -10], [20, 0, 0], [0, 0, 20], white);    // file +y (up)
Side([-10, -10, -10], [20, 0, 0], [0, 0, 20], black);   // file -y
Side([-10, -10, 10], [20, 0, 0], [0, 20, 0], blue);     // file +z
Side([-10, -10, -10], [20, 0, 0], [0, 20, 0], green);   // file -z
var boxSky = NativeSkyBuilder.Build(SkyPanorama.FromModel(new SkyModel(box, [], SkyModelUpAxis.Y, [])), new());
Rgba32 bx = Seen(boxSky.Block, new(1, 0, 0)), by = Seen(boxSky.Block, new(0, 1, 0)), bz = Seen(boxSky.Block, new(0, 0, 1)), bmy = Seen(boxSky.Block, new(0, -1, 0));
Console.WriteLine($"box seen: +X {bx}, +Y {by}, -Y {bmy}, up {bz}");
Check(Near(bx, 220, 30, 30), "a Y-up model's +x side is the game's +X");
Check(Near(by, 30, 200, 30), "a Y-up model's -z side is the game's +Y (handedness kept)");
Check(Near(bmy, 30, 30, 220), "a Y-up model's +z side is the game's -Y");
Check(Near(bz, 240, 240, 240), "a Y-up model's +y side is up");
var zUp = box.Select(t => new SkyModelTriangle(t.A with { Y = -t.A.Z, Z = t.A.Y }, t.B with { Y = -t.B.Z, Z = t.B.Y }, t.C with { Y = -t.C.Z, Z = t.C.Y }, -1, t.MaterialColor)).ToList();
var zSky = NativeSkyBuilder.Build(SkyPanorama.FromModel(new SkyModel(zUp, [], SkyModelUpAxis.Z, [])), new());
Check(Near(Seen(zSky.Block, new(0, 1, 0)), 30, 200, 30) && Near(Seen(zSky.Block, new(0, 0, 1)), 240, 240, 240), "a Z-up model keeps its axes");

// A textured sphere carrying the test panorama with equirectangular UVs (as skybox programs export it): the same sky.
List<SkyModelTriangle> sphere = [];
const int Rings = 48, Segments = 96;
SkyModelCorner SphereCorner(int ring, int segment)
{
    double u = segment / (double)Segments, v = ring / (double)Rings;
    var d = SkyDirections.FromUv(u, v); // game axes; written back in Y-up file axes
    return new(d.X * 50, d.Z * 50, -d.Y * 50, u, v, null);
}
for (int r = 0; r < Rings; r++)
    for (int s = 0; s < Segments; s++)
    {
        sphere.Add(new(SphereCorner(r, s), SphereCorner(r + 1, s), SphereCorner(r + 1, s + 1), 0, white));
        sphere.Add(new(SphereCorner(r, s), SphereCorner(r + 1, s + 1), SphereCorner(r, s + 1), 0, white));
    }
var sphereSky = NativeSkyBuilder.Build(SkyPanorama.FromModel(new SkyModel(sphere, [TestPanorama(2048)], SkyModelUpAxis.Y, [])), new());
Check(Near(Seen(sphereSky.Block, new(1, 0, 0)), 230, 20, 20, 120) && Near(Seen(sphereSky.Block, new(0, 1, 0)), 240, 220, 20, 120),
    "a textured sphere model gives the same sky as its panorama");
var spherePreview = NativeSkyPreview.Render(sphereSky.Block, 512);
double sphereError = spherePreview.Pixels.Zip(preview.Pixels).Average(p => (Math.Abs(p.First.R - p.Second.R) + Math.Abs(p.First.G - p.Second.G) + Math.Abs(p.First.B - p.Second.B)) / 3.0);
Console.WriteLine($"textured sphere against the panorama sky: mean difference {sphereError:0.0} per channel");
Check(sphereError < 6, "the sphere's sky matches the panorama's sky");

// A dome with no floor: a remark, and the open bottom continues the dome's lowest colour.
var dome = sphere.Where(t => t.A.Y >= 0 && t.B.Y >= 0 && t.C.Y >= 0).ToList();
var domePanorama = SkyPanorama.FromModel(new SkyModel(dome, [TestPanorama(2048)], SkyModelUpAxis.Y, []));
Check(domePanorama.Notes.Any(n => n.Contains("open")), "a dome without a floor gets a remark");
Limits(NativeSkyBuilder.Build(domePanorama, new()).Block, "open dome");
// A model away from its origin is seen from its middle.
var moved = box.Select(t => new SkyModelTriangle(t.A with { X = t.A.X + 500 }, t.B with { X = t.B.X + 500 }, t.C with { X = t.C.X + 500 }, -1, t.MaterialColor)).ToList();
var movedPanorama = SkyPanorama.FromModel(new SkyModel(moved, [], SkyModelUpAxis.Y, []));
Check(movedPanorama.Notes.Any(n => n.Contains("middle")) && Near(Seen(NativeSkyBuilder.Build(movedPanorama, new()).Block, new(1, 0, 0)), 220, 30, 30), "a model away from the origin is seen from its middle");

// 4. Retail skies (with --source): each drawn as a panorama and rebuilt within the limits, looking the same.
if (source != null)
{
    var catalog = LevelCatalog.Load(FindRepo());
    double worst = 0;
    foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry is >= 10 and <= 78))
    {
        var scene = SceneDocument.New("sky") with { SkySourceWadEntry = level.SourceWadEntry };
        byte[]? retail = NativeAuthoredMesh.CaptureSky(source, scene);
        if (retail == null) continue;
        var retailImage = NativeSkyPreview.Render(retail, 1024);
        var rebuilt = NativeSkyBuilder.Build(SkyPanorama.FromImage(retailImage), new());
        Limits(rebuilt.Block, level.DisplayName);
        var again = NativeSkyPreview.Render(rebuilt.Block, 512);
        var reference = NativeSkyPreview.Render(retail, 512);
        double diff = again.Pixels.Zip(reference.Pixels).Average(p => (Math.Abs(p.First.R - p.Second.R) + Math.Abs(p.First.G - p.Second.G) + Math.Abs(p.First.B - p.Second.B)) / 3.0);
        worst = Math.Max(worst, diff);
        Console.WriteLine($"{level.DisplayName,-18} retail {NativeSkyPreview.Triangles(retail).Count,5} triangles → rebuilt {rebuilt.Triangles}, {rebuilt.Parts} parts, {rebuilt.Block.Length} bytes; mean difference {diff:0.0}");
        Check(diff < 12, $"{level.DisplayName}: the rebuilt sky looks like the original");
        await Save(retailImage, $"retail-{level.Key}.png");
        await Save(again, $"rebuilt-{level.Key}.png");
    }
    Console.WriteLine($"SKY_IMPORT_SOURCE_OK checks={checks} worst={worst:0.0}");
}
else
    Console.WriteLine($"SKY_IMPORT_FREE_OK checks={checks}");

static string FindRepo()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "spyro-level-catalog.json"))) return dir.FullName;
    throw new InvalidOperationException("The level catalog was not found.");
}
