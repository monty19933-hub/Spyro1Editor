using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

// Source-free checks for built-level face sides and distant meshes (Lost Fleet holes, 2026-10-10):
// - a save (SceneDocument.Capture) keeps TwoSided, FrontReversed and FarMeshes; JSON leaves them out when unset;
// - the compiled near faces carry flag 0x02 (reversed) / 0x04 (two-sided), the far copies turn the other way / carry
//   0x80, and a reversed vertical wall's collision is solid from its other side;
// - a sector with a scene-given distant mesh draws it (fitted to 64 corners and colours, split sectors sharing it);
//   without one it gets the simplified copy as before.
int failures = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) failures++; }

string Id() => Guid.NewGuid().ToString("N");
var material = new AuthoredMaterial(10, 66);
ColorRgba grey = new(128, 128, 128), dark = new(60, 60, 60);
AuthoredSurface Quad(Vector3f a, Vector3f b, Vector3f c, Vector3f d, int? sector) => new(Id(), [a, b, c, d], grey, material, [grey, grey, grey, grey], [dark, dark, dark, dark]) { Sector = sector };
List<AuthoredSurface> surfaces = [];
// Sector 7: a 9 x 9 floor grid (100 corners, so it needs a far mesh).
for (int i = 0; i < 9; i++) for (int j = 0; j < 9; j++)
{
    float x = 1000 + i * 64, y = 1000 + j * 64;
    surfaces.Add(Quad(new(x, y, 500), new(x + 64, y, 500), new(x + 64, y + 64, 500), new(x, y + 64, 500), 7));
}
// Sector 9: few corners (its far copy is its near faces): a normal wall, the same wall reversed, a two-sided floor.
var wall = Quad(new(2000, 1000, 500), new(2100, 1000, 500), new(2100, 1000, 600), new(2000, 1000, 600), 9);
var reversedWall = Quad(new(2000, 1200, 500), new(2100, 1200, 500), new(2100, 1200, 600), new(2000, 1200, 600), 9) with { FrontReversed = true };
var bothFloor = Quad(new(2000, 1400, 500), new(2100, 1400, 500), new(2100, 1500, 500), new(2000, 1500, 500), 9) with { TwoSided = true };
surfaces.AddRange([wall, reversedWall, bothFloor]);
// Sector 11: a 15 x 15 grid (256 corners: split into two native sectors, each within the 170 near corners the
// game's scratchpad holds).
for (int i = 0; i < 15; i++) for (int j = 0; j < 15; j++)
{
    float x = 3000 + i * 64, y = 1000 + j * 64;
    surfaces.Add(Quad(new(x, y, 500), new(x + 64, y, 500), new(x + 64, y + 64, 500), new(x, y + 64, 500), 11));
}
// Sector 13: 9 x 9 grid whose distant mesh has too many corners and colours (a 70-corner fan of 69 triangles).
for (int i = 0; i < 9; i++) for (int j = 0; j < 9; j++)
{
    float x = 5000 + i * 64, y = 1000 + j * 64;
    surfaces.Add(Quad(new(x, y, 500), new(x + 64, y, 500), new(x + 64, y + 64, 500), new(x, y + 64, 500), 13));
}
ColorRgba C(int k) => new((byte)(k * 3), (byte)(255 - k * 3), 90);
Vector3f[] fan = [new(5288, 1288, 500), .. Enumerable.Range(0, 69).Select(k => new Vector3f(5288 + MathF.Round(280 * MathF.Cos(k * MathF.Tau / 69)), 1288 + MathF.Round(280 * MathF.Sin(k * MathF.Tau / 69)), 500))];
AuthoredFarMesh[] far =
[
    new(7, [new(1000, 1000, 500), new(1288, 1000, 500), new(1576, 1000, 500), new(1576, 1576, 500), new(1288, 1576, 500), new(1000, 1576, 500)],
        [new(100, 100, 100), new(200, 200, 200)], [new([0, 1, 4, 5], [0, 0, 1, 1]), new([1, 2, 3, 4], [0, 0, 1, 1])]),
    new(11, [new(3000, 1000, 500), new(3544, 1000, 500), new(4088, 1000, 500), new(4088, 2088, 500), new(3544, 2088, 500), new(3000, 2088, 500)],
        [new(100, 100, 100)], [new([0, 1, 4, 5], [0, 0, 0, 0]), new([1, 2, 3, 4], [0, 0, 0, 0])]),
    new(13, fan, [.. Enumerable.Range(0, 80).Select(C)], [.. Enumerable.Range(0, 69).Select(k => new AuthoredFarFace([0, 1 + k, 1 + (k + 1) % 69], [k % 80, (k + 7) % 80, (k + 11) % 80]))]),
    new(42, [new(9000, 9000, 500), new(9100, 9000, 500), new(9100, 9100, 500)], [grey], [new([0, 1, 2], [0, 0, 0])]),
];
var doc = SceneDocument.New("SIDES") with { Surfaces = surfaces, FarMeshes = far };
doc.Validate();

// 1. Saving keeps the sides and the distant meshes.
var captured = doc.Capture(doc.ProjectGeometry(), []);
Check(captured.Surfaces.Count(s => s.FrontReversed) == 1 && captured.Surfaces.Count(s => s.TwoSided) == 1, "a save keeps FrontReversed and TwoSided");
Check(captured.Surfaces.Single(s => s.FrontReversed).Id == reversedWall.Id && captured.Surfaces.Single(s => s.TwoSided).Id == bothFloor.Id, "on the same faces");
Check(captured.FarMeshes == doc.FarMeshes, "a save keeps the distant meshes");
var copy = (captured with { Surfaces = [.. captured.Surfaces.Select(s => s with { Vertices = [.. s.Vertices.Select(v => v with { Z = v.Z + 1 })] })] }).Capture(captured.ProjectGeometry(), []);
Check(copy.Surfaces.Count(s => s.FrontReversed) == 1, "a second save keeps them");

// 2. JSON: absent unless used, and they round-trip.
string plain = System.Text.Encoding.UTF8.GetString(SceneDocument.New("PLAIN").Serialize());
Check(!plain.Contains("FrontReversed") && !plain.Contains("TwoSided") && !plain.Contains("FarMeshes"), "a scene without them writes none of the new fields");
var json = JsonSerializer.Deserialize<SceneDocument>(captured.Serialize())!;
Check(json.Surfaces.Count(s => s.FrontReversed) == 1 && json.Surfaces.Count(s => s.TwoSided) == 1, "JSON keeps the sides");
Check(json.FarMeshes!.Count == far.Length && json.FarMeshes[2].Faces.Count == 69 && json.FarMeshes[0].Faces[1].Colors.SequenceEqual([0, 0, 1, 1]), "JSON keeps the distant meshes");
json.Validate();
try { (doc with { FarMeshes = [far[0] with { Faces = [new([0, 1, 9], [0, 0, 0])] }] }).Validate(); Check(false, "a distant face outside its corners is refused"); }
catch (InvalidDataException) { Check(true, "a distant face outside its corners is refused"); }

// 3. Compiled terrain.
var materials = new[] { material };
var bundle = NativeAuthoredMesh.Compile(doc, materials);
var sectors = Sectors(bundle);
Check(NativeAuthoredMeshRenderer.LastFarReport == (4, 0), $"four far meshes are the scene's own (report {NativeAuthoredMeshRenderer.LastFarReport})");
var s9 = sectors.Single(s => s.Hp.Any(f => f.Points.Contains(new Vector3f(2000, 1200, 500))));
uint FlagsOf(AuthoredSurface surface) => s9.Hp.Single(f => f.Points.ToHashSet().SetEquals(surface.Vertices)).Flags;
Check(FlagsOf(wall) == 0x40 && FlagsOf(reversedWall) == 0x42 && FlagsOf(bothFloor) == 0x44, $"near flags 0x40 / 0x42 / 0x44 (got {FlagsOf(wall):X2} {FlagsOf(reversedWall):X2} {FlagsOf(bothFloor):X2})");
Check(s9.Lp.Count == 3, "a small sector's far faces are its near faces");
(Vector3f[] Points, uint Low) LpOf(AuthoredSurface surface) => s9.Lp.Single(f => f.Points.ToHashSet().SetEquals(surface.Vertices));
Check(Cyclic(LpOf(wall).Points, wall.Vertices), "a normal face's far copy keeps its corner order");
Check(Cyclic(LpOf(reversedWall).Points, [reversedWall.Vertices[0], reversedWall.Vertices[3], reversedWall.Vertices[2], reversedWall.Vertices[1]]), "a reversed face's far copy turns the other way");
Check(LpOf(bothFloor).Low == 0x80 && LpOf(wall).Low == 0 && LpOf(reversedWall).Low == 0, "a two-sided face's far copy carries 0x80");
var s7 = sectors.Single(s => s.Hp.Any(f => f.Points.Contains(new Vector3f(1000, 1000, 500))));
Check(s7.Lp.Count == 2 && s7.LpVertices.Count == 6 && s7.LpColours == 2, $"sector 7 draws its own distant mesh far away ({s7.Lp.Count} faces, {s7.LpVertices.Count} corners)");
Check(Cyclic(s7.Lp[0].Points, [new(1000, 1000, 500), new(1288, 1000, 500), new(1288, 1576, 500), new(1000, 1576, 500)]), "in the scene's corner order");
var s11 = sectors.Where(s => s.Hp.Any(f => f.Points.Any(p => p.X >= 3000 && p.X <= 4088))).ToList();
Check(s11.Count == 2 && s11.All(s => s.Lp.Count == 1), $"a split sector's pieces share its distant faces ({string.Join(",", s11.Select(s => s.Lp.Count))})");
var s13 = sectors.Single(s => s.Hp.Any(f => f.Points.Contains(new Vector3f(5000, 1000, 500))));
Check(s13.LpVertices.Count <= 64 && s13.LpColours <= 64 && s13.Lp.Count is > 50 and < 69, $"an oversized distant mesh is fitted ({s13.LpVertices.Count} corners, {s13.LpColours} colours, {s13.Lp.Count} faces)");
Check(s13.LpVertices.All(p => p.X >= 5000 && p.X <= 5576 && p.Y >= 1000 && p.Y <= 1576), "inside its sector's box");

// Without distant meshes (and without sides) the sectors compile as before: simplified copies.
var plainDoc = doc with { FarMeshes = null, Surfaces = [.. doc.Surfaces.Select(s => s with { TwoSided = false, FrontReversed = false })] };
var plainSectors = Sectors(NativeAuthoredMesh.Compile(plainDoc, materials));
Check(NativeAuthoredMeshRenderer.LastFarReport == (0, 4), $"without them every big sector is simplified (report {NativeAuthoredMeshRenderer.LastFarReport})");
Check(plainSectors.SelectMany(s => s.Hp).All(f => f.Flags == 0x40) && plainSectors.SelectMany(s => s.Lp).All(f => f.Low == 0), "and no side flags are written");

// 4. Collision: a reversed vertical wall is solid from its other side.
var triangles = Collision(bundle);
Vector3f Normal((Vector3f A, Vector3f B, Vector3f C) t)
{
    Vector3f u = new(t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z), v = new(t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z);
    return new(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);
}
var normalWall = triangles.Where(t => t.A.Y == 1000 && t.B.Y == 1000 && t.C.Y == 1000 && t.A.X >= 2000).Select(Normal).ToList();
var flippedWall = triangles.Where(t => t.A.Y == 1200 && t.B.Y == 1200 && t.C.Y == 1200 && t.A.X >= 2000).Select(Normal).ToList();
Check(normalWall.Count == 2 && flippedWall.Count == 2 && normalWall.All(n => n.Y > 0) && flippedWall.All(n => n.Y < 0), "the reversed wall's collision faces the other way");

Console.WriteLine(failures == 0 ? "AUTHORED_FACE_SIDES_OK" : $"{failures} FAILED");
return failures == 0 ? 0 : 1;

static bool Cyclic(Vector3f[] a, Vector3f[] b) => a.Length == b.Length && Enumerable.Range(0, a.Length).Any(r => Enumerable.Range(0, a.Length).All(k => a[(k + r) % a.Length] == b[k]));
static uint U(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));

// The native environment chunk (NativeAuthoredMeshRenderer.Encode): per sector a 28-byte header, LP corners, colours
// and faces, then HP corners, far and near colours and faces.
static List<Sector> Sectors(byte[] bundle)
{
    List<Sector> result = [];
    int count = (int)U(bundle, 4);
    for (int i = 0; i < count; i++)
    {
        int a = 4 + (int)U(bundle, 8 + 4 * i);
        int x = (int)(U(bundle, a + 8) >> 16), y = (int)(U(bundle, a + 8) & 0xFFFF), z = (int)(U(bundle, a + 12) >> 16);
        int lv = bundle[a + 16], lc = bundle[a + 17], lf = bundle[a + 18], v = bundle[a + 20], c = bundle[a + 21], f = bundle[a + 22];
        int lpv = a + 28, lpc = lpv + 4 * lv, lpf = lpc + 4 * lc, hpv = lpf + 8 * lf, hpf = hpv + 8 * c + 4 * v;
        Vector3f Unpack(uint p) => new(x + (p >> 21), y + (p >> 10 & 0x7FF), z + (p & 0x3FF));
        var lpVertices = Enumerable.Range(0, lv).Select(k => Unpack(U(bundle, lpv + 4 * k))).ToList();
        var hpVertices = Enumerable.Range(0, v).Select(k => Unpack(U(bundle, hpv + 4 * k))).ToList();
        List<(Vector3f[] Points, uint Low)> lp = [];
        for (int k = 0; k < lf; k++)
        {
            uint w = U(bundle, lpf + 8 * k);
            int[] i4 = [(int)(w >> 26 & 63), (int)(w >> 20 & 63), (int)(w >> 14 & 63), (int)(w >> 8 & 63)];
            int[] ring = i4[2] == i4[3] ? [i4[2], i4[1], i4[0]] : [i4[2], i4[3], i4[1], i4[0]];
            lp.Add(([.. ring.Select(r => lpVertices[r])], w & 0xFF));
        }
        List<(Vector3f[] Points, uint Flags)> hp = [];
        for (int k = 0; k < f; k++)
        {
            uint w = U(bundle, hpf + 16 * k);
            int[] i4 = [(int)(w >> 24 & 255), (int)(w >> 16 & 255), (int)(w >> 8 & 255), (int)(w & 255)];
            int[] ring = i4[2] == i4[3] ? [i4[2], i4[1], i4[0]] : [i4[3], i4[2], i4[1], i4[0]];
            hp.Add(([.. ring.Select(r => hpVertices[r])], U(bundle, hpf + 16 * k + 12)));
        }
        result.Add(new(lpVertices, lc, lp, hp));
    }
    return result;
}

// The collision chunk follows the environment, view-group and special-surface chunks (each starts with its length).
static List<(Vector3f A, Vector3f B, Vector3f C)> Collision(byte[] bundle)
{
    int at = 0;
    for (int k = 0; k < 3; k++) at += (int)U(bundle, at);
    int n = (int)U(bundle, at + 4), start = at + (int)U(bundle, at + 20) + 4;
    static int S9(uint v) => (int)(v & 0x1FF) - ((v & 0x100) != 0 ? 0x200 : 0);
    List<(Vector3f, Vector3f, Vector3f)> result = [];
    for (int i = 0; i < n; i++)
    {
        uint wx = U(bundle, start + 12 * i), wy = U(bundle, start + 12 * i + 4), wz = U(bundle, start + 12 * i + 8);
        int x = (int)(wx & 0x3FFF), y = (int)(wy & 0x3FFF), z = (int)(wz & 0xFFFF);
        result.Add((new(x, y, z), new(x + S9(wx >> 14), y + S9(wy >> 14), z + (int)(wz >> 16 & 0xFF)), new(x + S9(wx >> 23), y + S9(wy >> 23), z + (int)(wz >> 24))));
    }
    return result;
}

record Sector(List<Vector3f> LpVertices, int LpColours, List<(Vector3f[] Points, uint Low)> Lp, List<(Vector3f[] Points, uint Flags)> Hp);
