using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

// Doorways (Terrain › Doorways): predetermined shapes, their frames, the doorway records Create BIN writes, scene editing,
// and old scenes reading and exporting exactly as before.
//   (no arguments)        source-free checks
//   --source <disc .bin>  also every original homeworld doorway from the disc: an original doorway's record and entry
//                         triangles are byte-for-byte what the editor wrote before shapes existed, and a shaped one's
//                         record parses back with its own opening, the original's head, sky header and sky
int checks = 0;
void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("check failed: " + what); checks++; }
string? source = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--source") source = args[++i];

string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "spyro-level-catalog.json"))) root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("No level catalog above the smoke.");
var catalog = LevelCatalog.Load(root);
int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));

// ---- The shapes: what the game can draw (<= 10 points, convex, anticlockwise, floor edge), sizes like the game's own.
Check(DoorwayShapes.All.Count == 6 && DoorwayShapes.All.Select(s => s.Id).Distinct().Count() == 6, "six shapes with their own ids");
Check(DoorwayShapes.Find("stone-arch") == DoorwayShapes.StoneArch && DoorwayShapes.Find("nope") == null && DoorwayShapes.Find(null) == null, "shapes are found by id");
foreach (var shape in DoorwayShapes.All)
{
    foreach (double scale in new[] { DoorwayShapes.MinScale, 1.0, DoorwayShapes.MaxScale })
        Check(DoorwayShapes.Check(DoorwayShapes.Opening(shape, scale)) == null, $"{shape.Name} at {scale} is an opening the game can draw");
    Check(shape.Opening.Count is >= 4 and <= DoorwayShapes.MaxOutlinePoints, $"{shape.Name}: {shape.Opening.Count} points");
    Check(shape.Width is >= 128 and <= 200 && shape.Height is >= 160 and <= 215, $"{shape.Name} is the size of the game's doorways ({shape.Width:0} x {shape.Height:0})");
    // Spyro walks the original doorway's path about 75-95 units above its floor: even the smallest size leaves room.
    var small = DoorwayShapes.Opening(shape, DoorwayShapes.MinScale);
    double Inside(double v) { var xs = new List<double>(); for (int k = 0; k < small.Count; k++) { var a = small[k]; var b = small[(k + 1) % small.Count]; if ((a.V - v) * (b.V - v) <= 0 && a.V != b.V) xs.Add(a.U + (v - a.V) / (b.V - a.V) * (b.U - a.U)); } return xs.Count < 2 ? 0 : xs.Max() - xs.Min(); }
    Check(Inside(95) >= 64 && Inside(10) >= 64, $"{shape.Name}: Spyro's walk-in path fits through it at three quarters size");
}
Check(DoorwayShapes.Check([(64, 0), (64, 100), (0, 60), (-64, 100), (-64, 0)])!.Contains("convex"), "a notched (concave) opening is refused");
Check(DoorwayShapes.Check(Enumerable.Range(0, 11).Select(k => (Math.Cos(Math.PI * k / 10) * 64, k is 0 or 10 ? 0 : Math.Sin(Math.PI * k / 10) * 64 + 10)).ToArray())!.Contains("at most 10"), "an opening of 11 points is refused");
Check(DoorwayShapes.Check([(64, 0), (-64, 100), (64, 100), (-64, 0)]) != null, "a clockwise or crossed opening is refused");

// ---- Frames: whole-unit planar quads facing out, cheap, around the opening (not over it), the same at every heading.
var at = new Vector3f(3000, 3000, 1000);
var budget = new Dictionary<string, int> { ["stone-arch"] = 56, ["pointed-arch"] = 56, ["square-gate"] = 48, ["round-ring"] = 42, ["wide-arch"] = 56, ["hex-gate"] = 46 };
foreach (var shape in DoorwayShapes.All)
{
    var frame = DoorwayShapes.Frame(shape, 1, at, 0);
    Check(frame.Count == budget[shape.Id], $"{shape.Name}: {frame.Count} frame faces");
    Check(frame.All(f => f.Corners.Length == 4 && f.Corners.All(c => c.X == MathF.Round(c.X) && c.Y == MathF.Round(c.Y) && c.Z == MathF.Round(c.Z))), $"{shape.Name}: whole-unit quads");
    Check(frame.Count(f => f.Part == DoorwayFramePart.Face) >= frame.Count / 3 && frame.Count(f => f.Part == DoorwayFramePart.Side) >= frame.Count / 3, $"{shape.Name}: front/back and side parts");
    // Heading 0: Spyro walks in towards +X; the frame's faces at the least X show towards -X (their front is anticlockwise from there).
    double minX = frame.Min(f => f.Corners.Min(c => c.X));
    foreach (var f in frame.Where(f => f.Corners.All(c => c.X == minX)))
    {
        var a = f.Corners; double nx = (a[1].Y - a[0].Y) * (a[2].Z - a[0].Z) - (a[1].Z - a[0].Z) * (a[2].Y - a[0].Y);
        Check(nx < 0, $"{shape.Name}: the approach side's faces show towards the approach");
    }
    // Every opening edge but the floor has an inner face lying on it (the frame follows the opening exactly).
    var outline = DoorwayShapes.OutlineInLevel(shape, 1, at, 0);
    for (int k = 0; k + 1 < outline.Count; k++)
    {
        var (p, q) = (outline[k], outline[k + 1]);
        bool OnEdge(Vector3f c) { double ex = q.Y - p.Y, ez = q.Z - p.Z, l = Math.Sqrt(ex * ex + ez * ez); return Math.Abs(((c.Y - p.Y) * ez - (c.Z - p.Z) * ex) / l) <= 1.0; }
        Check(frame.Any(f => f.Part == DoorwayFramePart.Side && f.Corners.All(OnEdge)), $"{shape.Name}: edge {k} has its inner face");
    }
    // Turned a quarter: the same frame turned (corners within a unit).
    var turned = DoorwayShapes.Frame(shape, 1, at, 64);
    Check(turned.Count == frame.Count && frame.Zip(turned).All(z => z.First.Corners.Zip(z.Second.Corners).All(c =>
        Math.Abs((at.X - (c.First.Y - at.Y)) - c.Second.X) <= 1 && Math.Abs((at.Y + (c.First.X - at.X)) - c.Second.Y) <= 1 && c.First.Z == c.Second.Z)), $"{shape.Name}: turned a quarter is the same frame");
    var big = DoorwayShapes.Frame(shape, 1.5, at, 0);
    Check(big.Count >= frame.Count && big.Max(f => f.Corners.Max(c => c.Z)) > frame.Max(f => f.Corners.Max(c => c.Z)) * 1.0 + 50, $"{shape.Name}: larger sizes make a larger frame");
}

// ---- Doorway records: a synthetic original (Artisans' doorway to Toasty: five points, sky header, a small sky).
byte[] fakeSky = new byte[64]; BinaryPrimitives.WriteInt32LittleEndian(fakeSky, fakeSky.Length); fakeSky[10] = 0x5A;
byte[] fakeStub = Enumerable.Range(0, 20).Select(i => (byte)(0xA0 + i)).ToArray();
int[][] artPoints = [[134462, 83968, 5347], [134462, 83729, 8070], [134462, 82947, 8404], [134462, 82159, 8070], [134462, 81920, 5347]];
byte[] head = new byte[0x20];
BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(4), 5); BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(8), 4096);
BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(0x14), -1); BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(0x18), 40); BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(0x1C), 14);
byte[] templateRecord = [.. head, .. artPoints.SelectMany(p => p.SelectMany(v => BitConverter.GetBytes(v))), .. fakeStub, .. fakeSky];
var toasty = HomeworldPortalControlCatalogEntry(14);
static Spyro.Editor.Core.Scene.HomeworldPortalControlDefinition HomeworldPortalControlCatalogEntry(int d) => Spyro.Editor.Core.Scene.HomeworldPortalControlCatalog.All.Single(p => p.DestinationLevelId == d);
var fakeTriggers = new[] { (new PortalSourcePoint(8404, 5248, 334, 0), new PortalSourcePoint(8404, 5184, 525, 0), new PortalSourcePoint(8404, 5233, 504, 0)) };
var template = new NativeHubPortals.DoorwayTemplate("artisans", 14, templateRecord, fakeTriggers, toasty, 0);
Check(template.PointCount == 5 && template.StubAt == 0x20 + 60 && template.BottomMiddle == (134462, 82944, 5347), "the template's opening and bottom middle");

// The record an original doorway gets is exactly what the editor wrote before shapes (that code, kept here).
(byte[] Record, (Vector3f, Vector3f, Vector3f)[] Triggers) OldPlace(byte[] original, (PortalSourcePoint P1, PortalSourcePoint P2, PortalSourcePoint P3)[] triggers, AuthoredPortal portal)
{
    byte[] record = original.ToArray();
    int pointCount = I(record, 4);
    var outline = Enumerable.Range(0, pointCount).Select(i => (X: I(record, 0x20 + 12 * i), Y: I(record, 0x24 + 12 * i), Z: I(record, 0x28 + 12 * i))).ToArray();
    int bottom = outline.Min(p => p.Z);
    var low = outline.Where(p => p.Z - bottom <= 16).ToArray();
    double ax = low.Average(p => p.X), ay = low.Average(p => p.Y), az = bottom;
    double from = Math.Atan2(I(record, 0x0C), I(record, 0x08)), to = portal.Heading * Math.PI / 128;
    double turn = to - from, cos = Math.Cos(turn), sin = Math.Sin(turn);
    double tx = portal.Position.X * 16, ty = portal.Position.Y * 16, tz = portal.Position.Z * 16;
    (int X, int Y, int Z) Move(double x, double y, double z)
    {
        double dx = x - ax, dy = y - ay;
        return ((int)Math.Round(tx + dx * cos - dy * sin), (int)Math.Round(ty + dx * sin + dy * cos), (int)Math.Round(tz + z - az));
    }
    double nx = I(record, 0x08), ny = I(record, 0x0C);
    BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x08), (int)Math.Round(nx * cos - ny * sin)); BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x0C), (int)Math.Round(nx * sin + ny * cos));
    BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x18), 0);
    for (int i = 0; i < pointCount; i++)
    {
        var (x, y, z) = Move(outline[i].X, outline[i].Y, outline[i].Z);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x20 + 12 * i), x); BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x24 + 12 * i), y); BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x28 + 12 * i), z);
    }
    var t = triggers.Select(t =>
    {
        Vector3f P(PortalSourcePoint q) { var (x, y, z) = Move(q.X * 16.0, q.Y * 16.0, q.Z * 16.0); return new(MathF.Round(x / 16f), MathF.Round(y / 16f), MathF.Round(z / 16f)); }
        return (P(t.P1), P(t.P2), P(t.P3));
    }).ToArray();
    return (record, t);
}
foreach (var pose in new[] { new AuthoredPortal(14, new(3680, 2624, 1481), 197), new AuthoredPortal(14, new(8404, 5184, 334), 0), new AuthoredPortal(14, new(100, 16000, 7), 13) })
{
    var (record, triggers, _) = NativeHubPortals.PlaceRecord(template, pose);
    var old = OldPlace(templateRecord, fakeTriggers, pose);
    Check(record.AsSpan().SequenceEqual(old.Record) && triggers.SequenceEqual(old.Triggers), $"an original doorway at {pose.Position} heading {pose.Heading} is written exactly as before");
}

// A shaped doorway's record: the shape's opening (in game units, anticlockwise from the far side), its facing, the
// original's head, sky header and sky after it; entry triangles fill the opening on both sides.
foreach (var shape in DoorwayShapes.All)
foreach (var (heading, scale) in new[] { (0, 1f), (197, 1f), (64, 1.5f), (13, 0.75f) })
{
    var portal = new AuthoredPortal(14, new(3680, 2624, 1481), heading) { Shape = shape.Id, Scale = scale == 1 ? null : scale };
    var (record, triggers, _) = NativeHubPortals.PlaceRecord(template, portal);
    int n = I(record, 4);
    var expected = DoorwayShapes.OutlineInLevel(shape, scale, portal.Position, heading);
    Check(n == shape.Opening.Count && record.Length == 0x20 + 12 * n + 20 + fakeSky.Length, $"{shape.Name}: record length for {n} points");
    Check(Enumerable.Range(0, n).All(i => I(record, 0x20 + 12 * i) == (int)Math.Round(expected[i].X * 16) && I(record, 0x24 + 12 * i) == (int)Math.Round(expected[i].Y * 16) && I(record, 0x28 + 12 * i) == (int)Math.Round(expected[i].Z * 16)),
        $"{shape.Name} heading {heading}: the record holds the shape's opening");
    var (fx, fy) = DoorwayShapes.Facing(heading);
    Check(I(record, 8) == (int)Math.Round(fx * 4096) && I(record, 12) == (int)Math.Round(fy * 4096) && I(record, 16) == 0, $"{shape.Name}: facing along the heading");
    Check(I(record, 0) == 0 && I(record, 0x14) == -1 && I(record, 0x18) == 0 && I(record, 0x1C) == 14, $"{shape.Name}: the original's sector, return token and an unbound path row");
    Check(record.AsSpan(0x20 + 12 * n, 20).SequenceEqual(fakeStub) && record.AsSpan(0x20 + 12 * n + 20).SequenceEqual(fakeSky), $"{shape.Name}: sky header and sky follow the opening");
    // Anticlockwise seen from the far side: the signed area of the opening in (along, up) is positive.
    double area = 0;
    for (int i = 0; i < n; i++)
    {
        double U(int k) => -fy * I(record, 0x20 + 12 * k) + fx * I(record, 0x24 + 12 * k);
        double V(int k) => I(record, 0x28 + 12 * k);
        int j = (i + 1) % n; area += U(i) * V(j) - U(j) * V(i);
    }
    Check(area > 0, $"{shape.Name}: anticlockwise like the game's own doorways");
    double plane = fx * portal.Position.X + fy * portal.Position.Y;
    Check(triggers.Count >= 2 * (n - 2) && triggers.Count % 2 == 0 && triggers.All(t => new[] { t.A, t.B, t.C }.All(c => Math.Abs(fx * c.X + fy * c.Y - plane) <= 1.0)),
        $"{shape.Name}: entry triangles lie in the opening");
    Check(triggers.All(t => new[] { t.A.X, t.B.X, t.C.X }.Max() - new[] { t.A.X, t.B.X, t.C.X }.Min() <= 240 && new[] { t.A.Y, t.B.Y, t.C.Y }.Max() - new[] { t.A.Y, t.B.Y, t.C.Y }.Min() <= 240 &&
        new[] { t.A.Z, t.B.Z, t.C.Z }.Max() - new[] { t.A.Z, t.B.Z, t.C.Z }.Min() <= 240), $"{shape.Name} at {scale}: no entry triangle spans more than a collision triangle can");
    double TriArea(Vector3f a, Vector3f b, Vector3f c) { double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z; double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx; return Math.Sqrt(cx * cx + cy * cy + cz * cz) / 2; }
    double covered = triggers.Sum(t => TriArea(t.A, t.B, t.C)) / 2, opening = area / 2 / 256;
    Check(Math.Abs(covered - opening) <= opening * 0.03 + 40, $"{shape.Name}: the entry triangles cover the whole opening ({covered:0} of {opening:0})");
}

// ---- Old scenes: a doorway saved before shapes reads, validates and writes back byte for byte.
var binding = new SceneTargetBinding("artisans", 10, 10, 9, UniversalSourceLibrary.OriginalUsaSha256, 174);
var plain = SceneDocument.New("LOST FLEET") with { TargetBinding = binding, Portals = [new AuthoredPortal(11, new(4064, 3200, 1472), 14), new AuthoredPortal(15, new(4064, 2816, 1480), 236)] };
string oldJson = System.Text.Encoding.UTF8.GetString(plain.Serialize());
Check(oldJson.Contains("\"Portals\": [") && !oldJson.Contains("Shape") && !oldJson.Contains("FrameFaces") && !oldJson.Contains("Scale") && !oldJson.Contains("FaceMaterial"),
    "a doorway without a shape writes only what it always did");
var reread = JsonSerializer.Deserialize<SceneDocument>(oldJson)!;
reread.Validate();
Check(reread.Serialize().AsSpan().SequenceEqual(plain.Serialize()) && reread.Fingerprint() == plain.Fingerprint() && reread.Portals!.All(p => p.Shape == null && p.FrameFaces == null && p.SizeScale == 1),
    "an old scene reads back with the same fingerprint (its export is unchanged)");
var portalJson = JsonDocument.Parse(JsonSerializer.Serialize(new AuthoredPortal(12, new(1, 2, 3), 4))).RootElement;
Check(portalJson.EnumerateObject().Select(p => p.Name).SequenceEqual(["DestinationLevelId", "Position", "Heading"]), "an unshaped doorway's JSON has the old three fields");

// ---- Scene editing: add, move, turn, reshape, repaint, retarget, remove, and an original frame found and adopted.
var stone = GroundTypes.Stone.Material(catalog);
var wood = GroundTypes.Wood.Material(catalog);
var floor = new AuthoredSurface(Guid.NewGuid().ToString("N"), [new(2800, 2800, 1000), new(3200, 2800, 1000), new(3200, 3200, 1000), new(2800, 3200, 1000)], new(128, 128, 128), stone);
var home = SceneDocument.New("TEST HUB") with { TargetBinding = binding, Surfaces = [floor] };
Check(SceneDoorways.Unavailable(home, catalog) == null, "a rebuilt homeworld can have doorways");
Check(SceneDoorways.Destinations(home, catalog).Select(d => d.LevelId).SequenceEqual([11, 12, 13, 14, 15]) && SceneDoorways.Destinations(home, catalog).All(d => !d.Taken),
    "its doorways can lead where Artisans' did");
var stoneHillScene = SceneDocument.New("NOT A HUB") with { TargetBinding = new SceneTargetBinding("stonehill", 11, 12, 11, UniversalSourceLibrary.OriginalUsaSha256, 0) };
Check(SceneDoorways.Unavailable(stoneHillScene, catalog)!.Contains("only work in a homeworld"), "a level in Stone Hill's place says why it has none");
Check(SceneDoorways.Unavailable(SceneDocument.New("NEW"), catalog)!.Contains("doorway code"), "File › New Level's level says why it has none");
Check(SceneDoorways.Unavailable(SceneDocument.New("WORLD") with { WorldLevelId = 71 }, catalog)!.Contains("homeworld"), "a new world level points to its homeworld");
var added = SceneDoorways.Add(home, new AuthoredPortal(12, new(3000.4f, 3000.2f, 1000), 0) { Shape = "stone-arch", FaceMaterial = stone, SideMaterial = wood });
var door = SceneDoorways.Find(added, 12)!;
Check(door.Position == new Vector3f(3000, 3000, 1000) && door.FrameFaces!.Count == 56 && added.Surfaces.Count == 57 &&
    door.FrameFaces.All(id => added.Surfaces.Any(s => s.Id == id)), "Add puts the doorway on whole units with its frame as scene faces");
Check(SceneDoorways.OwnerOf(added, door.FrameFaces[3])?.DestinationLevelId == 12 && SceneDoorways.OwnerOf(added, floor.Id) == null, "a frame face knows its doorway");
var geometry = DoorwayShapes.Frame(DoorwayShapes.StoneArch, 1, door.Position, 0);
Check(door.FrameFaces.Select((id, i) => added.Surfaces.Single(s => s.Id == id).Material == (geometry[i].Part == DoorwayFramePart.Face ? stone : wood)).All(x => x),
    "the frame's front and back take one texture, its sides the other");
Check(SceneDoorways.Destinations(added, catalog).Single(d => d.LevelId == 12).Taken, "a destination with a doorway is taken");
bool refused = false;
try { SceneDoorways.Add(added, new AuthoredPortal(12, new(2900, 2900, 1000), 0) { Shape = "hex-gate", FaceMaterial = stone, SideMaterial = stone }); } catch (InvalidDataException) { refused = true; }
Check(refused, "a second doorway to the same level is refused");
// The user repaints one frame face, then the doorway moves: every face keeps its identity and texture.
string painted = door.FrameFaces[5];
var repainted = added with { Surfaces = added.Surfaces.Select(s => s.Id == painted ? s with { Material = wood, Kind = AuthoredSurfaceKind.Lava } : s).ToArray() };
var moved = SceneDoorways.Update(repainted, 12, door with { Position = new(3016, 2984, 1008) });
var movedDoor = SceneDoorways.Find(moved, 12)!;
Check(movedDoor.FrameFaces!.SequenceEqual(door.FrameFaces) && moved.Surfaces.Count == added.Surfaces.Count, "moving keeps the frame's faces");
Check(door.FrameFaces.All(id =>
{
    var a = repainted.Surfaces.Single(s => s.Id == id); var b = moved.Surfaces.Single(s => s.Id == id);
    return a.Material == b.Material && a.Kind == b.Kind && a.Vertices.Zip(b.Vertices).All(v => v.Second.X - v.First.X == 16 && v.Second.Y - v.First.Y == -16 && v.Second.Z - v.First.Z == 8);
}), "moving moves every frame face by exactly the move and keeps each one's texture and ground");
var turnedScene = SceneDoorways.Update(moved, 12, movedDoor with { Heading = SceneDoorways.Turn(movedDoor.Heading, 6) });
Check(SceneDoorways.Find(turnedScene, 12)!.Heading == 64 && SceneDoorways.Find(turnedScene, 12)!.FrameFaces!.SequenceEqual(door.FrameFaces), "turning 90° keeps the frame's faces");
Check(SceneDoorways.Turn(0, 1) == 11 && SceneDoorways.Turn(0, 24) == 0 && SceneDoorways.Turn(250, 1) == 0 && SceneDoorways.Turn(0, -1) == 245 &&
    Enumerable.Range(0, 24).All(k => Math.Abs(SceneDoorways.Degrees(SceneDoorways.Turn(0, k)) - 15 * k) <= 1), "15° steps land on the nearest 256th of a turn");
Check(SceneDoorways.HeadingTowards(0, 0, 0, 10) == 64 && SceneDoorways.HeadingTowards(10, 10, 0, 10) == 128, "facing away from the camera");
// Reshape: a new frame; each part keeps the texture most of its old faces showed.
var reshaped = SceneDoorways.Update(turnedScene, 12, SceneDoorways.Find(turnedScene, 12)! with { Shape = "square-gate", Scale = 1.25f });
var gate = SceneDoorways.Find(reshaped, 12)!;
Check(gate.FrameFaces!.Count == DoorwayShapes.Frame(DoorwayShapes.SquareGate, 1.25, gate.Position, gate.Heading).Count && gate.FrameFaces.All(id => door.FrameFaces.All(o => o != id)) &&
    door.FrameFaces.All(id => reshaped.Surfaces.All(s => s.Id != id)) && reshaped.Surfaces.Count == 1 + gate.FrameFaces.Count, "a new shape replaces the frame");
var gateGeometry = DoorwayShapes.Frame(DoorwayShapes.SquareGate, 1.25, gate.Position, gate.Heading);
Check(gate.FrameFaces.Select((id, i) => reshaped.Surfaces.Single(s => s.Id == id).Material == (gateGeometry[i].Part == DoorwayFramePart.Face ? stone : wood)).All(x => x) &&
    gate.FaceMaterial == stone && gate.SideMaterial == wood, "the new frame keeps the old frame's textures");
var sand = new AuthoredMaterial(catalog.FindByKey("drycanyon")!.SourceWadEntry, 20);
var sanded = SceneDoorways.Update(reshaped, 12, gate with { SideMaterial = sand }, repaint: DoorwayFramePart.Side);
Check(gate.FrameFaces.Select((id, i) => sanded.Surfaces.Single(s => s.Id == id).Material == (gateGeometry[i].Part == DoorwayFramePart.Face ? stone : sand)).All(x => x),
    "Side Texture repaints the sides only");
var retargeted = SceneDoorways.Update(sanded, 12, SceneDoorways.Find(sanded, 12)! with { DestinationLevelId = 15 });
Check(SceneDoorways.Find(retargeted, 15)!.FrameFaces!.SequenceEqual(gate.FrameFaces) && SceneDoorways.Find(retargeted, 12) == null, "Leads To changes where it goes and keeps its frame");
var two = SceneDoorways.Add(retargeted, new AuthoredPortal(11, new(2900, 2900, 1000), 32) { Shape = "round-ring", FaceMaterial = wood, SideMaterial = wood });
var removed = SceneDoorways.Remove(two, 15);
Check(SceneDoorways.Find(removed, 15) == null && gate.FrameFaces.All(id => removed.Surfaces.All(s => s.Id != id)) && SceneDoorways.Find(removed, 11)!.FrameFaces!.All(id => removed.Surfaces.Any(s => s.Id == id)) &&
    removed.Surfaces.Any(s => s.Id == floor.Id), "Remove takes the doorway and its frame and nothing else");
Check(SceneDoorways.Remove(removed, 11).Portals == null, "removing the last doorway leaves none");
refused = false;
try { SceneDoorways.Add(home, new AuthoredPortal(13, new(10, 10, 0), 0) { Shape = "wide-arch", FaceMaterial = stone, SideMaterial = stone }); } catch (InvalidDataException e) { refused = e.Message.Contains("coordinate"); }
Check(refused, "a doorway whose frame would leave the map is refused");
// An original doorway (made before the tool, framed as Lost Fleet's were): its frame is found face for face and moves with it.
var original = new AuthoredPortal(14, new(3000, 3000, 1000), 64);
var (oOutline, oNormal) = NativeHubPortals.PlaceOriginal(template, original);
var legacyFrame = NativeHubPortals.Frame(new NativeHubPortals.Doorway(14, [], [], []) { Outline = oOutline, Normal = oNormal }, stone, wood);
var legacyScene = home with { Surfaces = [.. home.Surfaces, .. legacyFrame], Portals = [original] };
legacyScene.Validate();
var (adopted, found) = SceneDoorways.AdoptOriginalFrame(legacyScene, 14, template);
var adoptedDoor = SceneDoorways.Find(adopted, 14)!;
Check(found == legacyFrame.Count && adoptedDoor.FrameFaces!.SequenceEqual(legacyFrame.Select(f => f.Id)) && adoptedDoor.FaceMaterial == stone && adoptedDoor.SideMaterial == wood,
    $"an original doorway's frame is found face for face ({found} of {legacyFrame.Count}) with its textures");
var movedOriginal = SceneDoorways.Update(adopted, 14, adoptedDoor with { Position = new(3032, 3000, 1000) }, d => d == 14 ? template : null);
Check(SceneDoorways.Find(movedOriginal, 14)!.FrameFaces!.SequenceEqual(adoptedDoor.FrameFaces) &&
    legacyFrame.All(f => movedOriginal.Surfaces.Single(s => s.Id == f.Id).Vertices.Zip(f.Vertices).All(v => v.First.X - v.Second.X == 32 && v.Second.Y == v.First.Y)),
    "an original doorway moves with its frame");
var shapedOriginal = SceneDoorways.Update(movedOriginal, 14, SceneDoorways.Find(movedOriginal, 14)! with { Shape = "stone-arch" }, d => d == 14 ? template : null);
Check(legacyFrame.All(f => shapedOriginal.Surfaces.All(s => s.Id != f.Id)) && SceneDoorways.Find(shapedOriginal, 14)!.FrameFaces!.Count == 56 &&
    SceneDoorways.Find(shapedOriginal, 14)!.FaceMaterial == stone, "giving an original doorway a shape replaces its old frame and keeps its textures");
Check(SceneDoorways.AdoptOriginalFrame(home with { Portals = [original] }, 14, template).Found == 0, "no frame is invented where none is");

// ---- Validation, the new world, and Create BIN's refusals.
SceneDocument world = SceneDocument.New("SKY ISLES") with { WorldLevelId = 70 };
var worldDoor = SceneDoorways.Add(world, new AuthoredPortal(71, new(2000, 2000, 500), 0) { Shape = "pointed-arch", FaceMaterial = stone, SideMaterial = stone });
Check(SceneDoorways.Unavailable(world, catalog) == null && SceneDoorways.Destinations(worldDoor, catalog, id => id == 71 ? "LEVEL ONE" : id == 72 ? "LEVEL TWO" : null)
    .Select(d => d.ToString()).SequenceEqual(["LEVEL ONE (playable after New World export)", "LEVEL TWO (playable after New World export)"]), "the new world's homeworld leads to its own levels");
string? worldRefusal = null;
try { NativeHubPortals.Build("/nonexistent/disc.bin", catalog, worldDoor); } catch (InvalidDataException e) { worldRefusal = e.Message; }
Check(worldRefusal == NativeHubPortals.NewWorldNotExportable && worldRefusal.Contains("can't go into a game yet"), "Create BIN says plainly that New World doorways can't export yet");
bool Invalid(SceneDocument s) { try { s.Validate(); return false; } catch (InvalidDataException) { return true; } }
Check(Invalid(world with { Portals = [new AuthoredPortal(11, new(10, 10, 10), 0)] }), "the new world's homeworld can't lead to a retail level");
Check(Invalid(home with { Portals = [new AuthoredPortal(21, new(10, 10, 10), 0)] }), "Artisans can't lead to Peace Keepers' levels");
Check(Invalid(home with { Portals = [new AuthoredPortal(11, new(10, 10, 10), 0) { Shape = "triangle" }] }), "an unknown shape is refused");
Check(Invalid(home with { Portals = [new AuthoredPortal(11, new(10, 10, 10), 0) { Scale = 2 }] }), "a size past 1.5 is refused");
string id = Guid.NewGuid().ToString("N");
Check(Invalid(home with { Portals = [new AuthoredPortal(11, new(10, 10, 10), 0) { FrameFaces = [id] }, new AuthoredPortal(12, new(10, 10, 10), 0) { FrameFaces = [id] }] }), "two doorways can't share a frame face");
Check(NativeHubPortals.MaxDoorways == 6, "the game keeps six doorways per homeworld");

if (source != null)
{
    // Every original homeworld doorway from the disc: written as before (records and entry triangles), and as each shape.
    int originals = 0;
    foreach (var key in NewWorldDocument.HomeworldKeys)
    {
        var templates = NativeHubPortals.Templates(source, catalog, key);
        foreach (var (dest, t) in templates)
        {
            Check(t.PointCount is >= 3 and <= DoorwayShapes.MaxOutlinePoints && t.Triggers.Count >= 2 && I(t.Record, 0x1C) == dest, $"{key} doorway to {dest}: {t.PointCount} points, {t.Triggers.Count} entry triangles");
            Console.WriteLine($"{key} doorway to {dest}: {t.PointCount} points, {t.Triggers.Count} entry triangles, record {t.Record.Length} bytes");
            foreach (var pose in new[] { new AuthoredPortal(dest, new(4000, 3000, 1400), 0), new AuthoredPortal(dest, new(5000, 6000, 900), 141) })
            {
                var (record, triggers, _) = NativeHubPortals.PlaceRecord(t, pose);
                var old = OldPlace(t.Record, t.Triggers.ToArray(), pose);
                Check(record.AsSpan().SequenceEqual(old.Record) && triggers.SequenceEqual(old.Triggers), $"{key} doorway to {dest} is written exactly as before");
                foreach (var shape in DoorwayShapes.All)
                {
                    var (shaped, _, _) = NativeHubPortals.PlaceRecord(t, pose with { Shape = shape.Id });
                    int n = I(shaped, 4);
                    Check(n == shape.Opening.Count && shaped.AsSpan(0x20 + 12 * n).SequenceEqual(t.Record.AsSpan(t.StubAt)) && I(shaped, 0x1C) == dest && I(shaped, 0x14) == I(t.Record, 0x14),
                        $"{key} doorway to {dest} as a {shape.Name}: the original's sky header and sky follow the shape's opening");
                }
            }
            originals++;
        }
    }
    Check(originals == 29, "all 29 original homeworld doorways");
    Console.WriteLine($"DOORWAY_SOURCE_OK checks={checks}");
}
else
    Console.WriteLine($"DOORWAY_FREE_OK checks={checks}");
