using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Sky;
using Spyro.Editor.Core.Universal;

// The New World's doorway in one of the game's own homeworlds (NativeWorldEntrance).
//   (no arguments)              disc-free checks: skies, the component writers (ground kinds, view groups, collision),
//                               the frame's sectors, the way back, the saved placement (world-entrance.json), refusals
//   --source <USA bin>          also the entrance in each of the six homeworlds (read only): records, objects, collision,
//                               memory in both modes, refusals, after another object edit, determinism
//   --bin <USA bin> <out.bin> <x,y,z,heading> [shape] [scale] [sky] [homeworld]
//                               writes the emulator proof image: homeworld 70 (a copy of Artisans whose Sunny Flight doorway
//                               leads back to the entry homeworld) and the entrance in that homeworld; <out>.json has the facts
int checks = 0, failed = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL: " + what); } else if (verbose) Console.WriteLine("ok   " + what); }
bool Throws(Action a, string? containing = null)
{
    try { a(); return false; }
    catch (InvalidDataException e) { if (containing != null && !e.Message.Contains(containing, StringComparison.Ordinal)) { Console.WriteLine("  (message: " + e.Message + ")"); return false; } return true; }
}
int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);

if (args.Length >= 4 && args[0] == "--bin")
    return Proof.Build(args[1], args[2], args[3], args.Length > 4 ? args[4] : DoorwayShapes.StoneArch.Id,
        args.Length > 5 ? double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 1,
        args.Length > 6 ? Enum.Parse<WorldEntranceSky>(args[6], true) : WorldEntranceSky.Auto, args.Length > 7 ? args[7] : "artisans");

// ---------------- skies ----------------
var catalogFree = LevelCatalog.Load(Proof.Root());
var uniform = new SkyImage(64, 32, Enumerable.Range(0, 64 * 32).Select(i => i / 64 < 16 ? new Rgba32(40, 90, 200, 255) : new Rgba32(30, 160, 60, 255)).ToArray());
byte[] builtSky = NativeSkyBuilder.Build(SkyPanorama.FromImage(uniform), new SkyBuildOptions(1000)).Block;
Check(Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(builtSky) && I(builtSky, 8) > 10, "a test sky is built (a blue sky over green ground)");
byte[] plain = NativeWorldEntrance.PlainSky(new ColorRgba(10, 20, 30));
Check(plain.Length == 16 && I(plain, 0) == 16 && plain[4] == 10 && plain[5] == 20 && plain[6] == 30 && I(plain, 8) == 0, "a plain sky is 16 bytes: length, colour, no parts");
var horizon = NativeWorldEntrance.HorizonColour(builtSky);
Check(horizon.B > horizon.G && horizon.B > 150, $"the horizon colour of a blue-over-green sky is the blue above the horizon ({horizon.R},{horizon.G},{horizon.B})");
Check(NativeWorldEntrance.Sky(builtSky, WorldEntranceSky.Plain) is var p2 && p2.Length == 16 && p2[4] == horizon.R && p2[6] == horizon.B, "Plain: one colour, the horizon's");
Check(NativeWorldEntrance.Sky(builtSky, WorldEntranceSky.Full).AsSpan().SequenceEqual(builtSky), "Full: the sky as given");
foreach (int budget in new[] { 400, 2000, 6000, 20000 })
{
    byte[] reduced = NativeWorldEntrance.ReducedSky(builtSky, budget);
    int parts = I(reduced, 8);
    Check(reduced.Length <= Math.Max(16, budget) && (parts == 0 ? reduced.Length == 16 : Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(reduced)) && I(reduced, 0) == reduced.Length,
        $"a sky reduced to {budget} bytes is a valid block within it ({reduced.Length} bytes, {parts} parts)");
    if (parts > 0)
    {
        // Every kept part is one of the sky's own, byte for byte, in the sky's order.
        var own = Enumerable.Range(0, I(builtSky, 8)).Select(k => builtSky.AsSpan(4 + I(builtSky, 12 + 4 * k), (k + 1 < I(builtSky, 8) ? 4 + I(builtSky, 16 + 4 * k) : builtSky.Length) - 4 - I(builtSky, 12 + 4 * k)).ToArray()).ToList();
        var kept = Enumerable.Range(0, parts).Select(k => reduced.AsSpan(4 + I(reduced, 12 + 4 * k), (k + 1 < parts ? 4 + I(reduced, 16 + 4 * k) : reduced.Length) - 4 - I(reduced, 12 + 4 * k)).ToArray()).ToList();
        var at = kept.Select(k => own.FindIndex(o => o.AsSpan().SequenceEqual(k))).ToList();
        Check(at.All(i => i >= 0) && at.SequenceEqual(at.Order()), $"the {budget}-byte sky keeps whole parts of the sky in its drawing order");
        // The kept parts are the ones nearest the doorway's line of sight (just above the horizon).
        double Elevation(byte[] part) => Math.Atan2(-BinaryPrimitives.ReadInt16LittleEndian(part.AsSpan(2)),
            Math.Sqrt(Math.Pow(BinaryPrimitives.ReadInt16LittleEndian(part.AsSpan(0)), 2) + Math.Pow(BinaryPrimitives.ReadInt16LittleEndian(part.AsSpan(6)), 2))) * 180 / Math.PI;
        Check(kept.Max(k => Math.Abs(Elevation(k) - 10)) <= own.Where(o => !kept.Any(k => k.AsSpan().SequenceEqual(o))).DefaultIfEmpty(own[0]).Min(o => Math.Abs(Elevation(o) - 10)) + 25,
            $"the {budget}-byte sky keeps the parts near the horizon first");
    }
}
Check(NativeWorldEntrance.ReducedSky(builtSky, 12) is var none && none.Length == 16 && I(none, 8) == 0, "a sky reduced below one part is a plain sky of the left-out parts' colour");
byte[] onePart = plain.ToArray(); W(onePart, 8, 1);
Check(Throws(() => NativeWorldEntrance.Sky(new byte[40], WorldEntranceSky.Full)) && Throws(() => NativeWorldEntrance.Sky(onePart, WorldEntranceSky.Full)),
    "a block that is not a native sky is refused");

// ---------------- the way back ----------------
Check(NativeWorldEntrance.WayBack(10) == (10, 10, 0) && NativeWorldEntrance.WayBack(40) == (40, 40, 18) && NativeWorldEntrance.WayBack(60) == (60, 60, 30),
    "70's way back leads to the entry homeworld, carries its id as the return token and shows its name (0, 18, 30)");
Check(Throws(() => NativeWorldEntrance.WayBack(11)) && Throws(() => NativeWorldEntrance.WayBack(70)) && Throws(() => NativeWorldEntrance.WayBack(0)), "the way back only leads to one of the game's homeworlds");

// ---------------- ground kinds ----------------
{
    byte[] special = new byte[8 + 8 + 12 + 8];
    W(special, 0, special.Length); W(special, 4, 2); W(special, 8, 12); W(special, 12, 24);
    W(special, 16, 6); W(special, 20, 11); W(special, 24, 0); W(special, 28, 0); W(special, 32, 1);
    byte[] grown = NativeWorldEntrance.AddSpecialSurface(special, AuthoredSurfaceKind.Portal(70, 5).NativeRecord());
    Check(I(grown, 0) == grown.Length && I(grown, 4) == 3 && I(grown, 8) == 16 && I(grown, 12) == 28 && I(grown, 16) == 36 &&
          grown.AsSpan(20, 20).SequenceEqual(special.AsSpan(16, 20)) && I(grown, 40) == 6 && I(grown, 44) == 70 && I(grown, 48) == 5,
        "a ground kind is added after the level's own (their bytes and numbers kept; type 6, destination 70, doorway 5)");
    byte[] bad = special.ToArray(); W(bad, 12, 400);
    Check(Throws(() => NativeWorldEntrance.AddSpecialSurface(bad, new byte[12])), "a ground-kind table pointing outside itself is refused");
}

// ---------------- view groups ----------------
{
    // two lists ([0,1,FF..], [2,FF..]) and a sky part list
    byte[] vis = new byte[12 + 8 + 4 + 4 + 12];
    int sky = 12 + 8 + 4 + 4;
    W(vis, 0, vis.Length); W(vis, 4, sky - 4); W(vis, 8, 2); W(vis, 12, 20 - 4 + 0); W(vis, 16, 24 - 4);
    vis.AsSpan(20, 8).Fill(255); vis[20] = 0; vis[21] = 1; vis[24] = 2;
    W(vis, sky, 1); W(vis, sky + 4, 8); vis.AsSpan(sky + 8, 4).Fill(255); vis[sky + 8] = 3;
    byte[] grown = NativeWorldEntrance.AddToViewGroups(vis, [7, 8], "test");
    int lists = I(grown, 8), newSky = 4 + I(grown, 4);
    byte[] List(int n) { int a = 4 + I(grown, 12 + 4 * n), e = n + 1 < lists ? 4 + I(grown, 16 + 4 * n) : newSky; return grown.AsSpan(a, e - a).ToArray(); }
    Check(I(grown, 0) == grown.Length && lists == 2 && List(0).Take(5).SequenceEqual(new byte[] { 0, 1, 7, 8, 255 }) && List(1).Take(4).SequenceEqual(new byte[] { 2, 7, 8, 255 }) &&
          List(0).Length % 4 == 0 && grown.AsSpan(newSky).SequenceEqual(vis.AsSpan(sky)), "the frame's parts join every view group; the sky's lists stay");
    Check(NativeWorldEntrance.AddToViewGroups([4, 0, 0, 0], [5], "test").SequenceEqual(new byte[] { 4, 0, 0, 0 }), "a level without view groups (Beast Makers) needs none");
    byte[] full = new byte[12 + 4 + 256 + 12]; int fsky = 12 + 4 + 256;
    W(full, 0, full.Length); W(full, 4, fsky - 4); W(full, 8, 1); W(full, 12, 12); full.AsSpan(16, 256).Fill(255);
    for (int k = 0; k < 253; k++) full[16 + k] = (byte)k;
    Check(Throws(() => NativeWorldEntrance.AddToViewGroups(full, [253, 254], "test")), "a view group that would list more than 254 parts is refused");
}

// ---------------- collision ----------------
{
    // a small level: 6 floor triangles, the first 2 with flags
    Vector3f V(float x, float y, float z) => new(x, y, z);
    var floor = new List<(Vector3f, Vector3f, Vector3f)>();
    for (int k = 0; k < 6; k++) floor.Add((V(1000 + 100 * k, 1000, 500), V(1100 + 100 * k, 1000, 500), V(1000 + 100 * k, 1100, 500)));
    byte[] tris = new byte[12 * 6];
    for (int k = 0; k < 6; k++) { var (x, y, z) = NativeWorldEntrance.Pack(floor[k].Item1, floor[k].Item2, floor[k].Item3); W(tris, 12 * k, (int)x); W(tris, 12 * k + 4, (int)y); W(tris, 12 * k + 8, (int)z); }
    var cells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>();
    for (int k = 0; k < 6; k++)
    {
        var cell = new NativeCollisionCell((1000 + 100 * k) >> 8, 1000 >> 8, 500 >> 8);
        cells[cell] = (cells.TryGetValue(cell, out var existing) ? existing.Append(k) : [k]).OrderDescending().ToArray();
    }
    var index = NativeCollisionIndexCodec.Create(cells, 6, true);
    byte[] tree = index.CopyTree(), blocks = index.CopyBlocks();
    int ta = 32 + tree.Length + blocks.Length, aa = ta + 72, fa = aa + 8;
    byte[] comp = new byte[fa + 4];
    W(comp, 0, comp.Length); W(comp, 4, 6); W(comp, 8, 2); W(comp, 12, 28); W(comp, 16, 28 + tree.Length); W(comp, 20, ta - 4); W(comp, 24, aa - 4); W(comp, 28, fa - 4);
    tree.CopyTo(comp, 32); blocks.CopyTo(comp, 32 + tree.Length); tris.CopyTo(comp, ta);
    comp.AsSpan(aa, 6).Fill(255); comp[aa] = 3; comp[aa + 4] = 4; comp[fa] = 1; comp[fa + 1] = 0x3F;
    var entry = new List<(Vector3f, Vector3f, Vector3f)> { (V(1250, 1050, 500), V(1250, 1050, 600), V(1250, 1100, 500)), (V(1250, 1050, 500), V(1250, 1100, 500), V(1250, 1050, 600)) };
    var solid = new List<(Vector3f, Vector3f, Vector3f)> { (V(1300, 1000, 500), V(1300, 1000, 560), V(1300, 1060, 500)) };
    byte[] grown = NativeWorldEntrance.AddCollision(comp, entry, 9, solid, "test", out int first);
    var l = NativeWorldEntrance.ReadCollision(grown, "test");
    Check(first == 2 && l.Count == 9 && l.FlagCount == 4, "entry triangles go right after the flagged ones (flag count 2 -> 4); 9 triangles in all");
    Check(grown[l.Flags] == 1 && grown[l.Flags + 1] == 0x3F && grown[l.Flags + 2] == 9 && grown[l.Flags + 3] == 9, "old flags kept; the entry triangles name the doorway's ground kind");
    Check(grown.AsSpan(l.Triangles, 24).SequenceEqual(tris.AsSpan(0, 24)) && grown.AsSpan(l.Triangles + 48, 48).SequenceEqual(tris.AsSpan(24, 48)),
        "the level's own triangles keep their words (the unflagged ones two further on)");
    Check(grown[l.Assignment] == 3 && grown[l.Assignment + 2] == 255 && grown[l.Assignment + 3] == 255 && grown[l.Assignment + 6] == 4 && grown[l.Assignment + 8] == 255,
        "view-group assignments move with their triangles; new ones change nothing (255)");
    var decoded = NativeCollisionIndexCodec.Decode(grown.AsSpan(l.Tree, l.Blocks - l.Tree), grown.AsSpan(l.Blocks, l.Triangles - l.Blocks), l.UsedBlocks, l.Count, false);
    int Moved(int t) => t < 2 ? t : t + 2;
    Check(cells.All(c => decoded.Cells.TryGetValue(c.Key, out var cell) && cell.OrderedTriangleIndexes.Where(t => t < 2 || t >= 4 && t < 8).SequenceEqual(c.Value.Select(Moved))),
        "every original cell keeps its triangles, renumbered, in their order");
    Check(decoded.Cells.Values.Any(c => c.OrderedTriangleIndexes.Contains(2)) && decoded.Cells.Values.Any(c => c.OrderedTriangleIndexes.Contains(8)),
        "the new entry and frame triangles are in the lookup grid");
    Check(decoded.Cells.Values.All(c => c.OrderedTriangleIndexes.Zip(c.OrderedTriangleIndexes.Skip(1)).All(p => p.First > p.Second)), "cells still list their triangles from the highest number down");
    var (px, py, pz) = NativeWorldEntrance.Pack(V(100, 200, 300), V(110, 190, 250), V(90, 210, 280));
    Check((pz & 0x3FFF) == 250 && (px & 0x3FFF) == 110 && ((px >> 14) & 511) == 492 && ((px >> 23) & 511) == 502 && (pz >> 16 & 255) == 30 && (pz >> 24) == 50 && (py & 0x3FFF) == 190,
        "a collision triangle starts at its lowest corner (turned, not reversed), with signed side offsets and unsigned height offsets");
    Check(Throws(() => NativeWorldEntrance.Pack(V(0, 0, 0), V(300, 0, 0), V(0, 10, 5))), "a collision triangle wider than 255 units is refused");
    var pieces = NativeWorldEntrance.Pieces(V(1000, 1000, 100), V(1600, 1000, 100), V(1000, 1500, 400)).ToArray();
    Check(pieces.Length >= 4 && pieces.All(t => new[] { t.A, t.B, t.C }.Max(p => p.X) - new[] { t.A, t.B, t.C }.Min(p => p.X) <= 240 &&
        new[] { t.A, t.B, t.C }.Max(p => p.Z) - new[] { t.A, t.B, t.C }.Min(p => p.Z) <= 240), "a big frame face is cut into pieces of at most 240 units");
    byte[] damaged = comp.ToArray(); W(damaged, 24, 999);
    Check(Throws(() => NativeWorldEntrance.AddCollision(damaged, entry, 9, solid, "test", out _)), "a collision that is not the game's layout is refused");
}

// ---------------- the frame's sectors ----------------
foreach (var shape in DoorwayShapes.All)
{
    var frame = DoorwayShapes.Frame(shape, 1.5, new Vector3f(4000, 4000, 500), 37);
    var faces = frame.Select(f => new NativeAuthoredMeshRenderer.AddedFace(f.Corners, [.. Enumerable.Repeat(f.Shade, f.Corners.Length)], [.. Enumerable.Repeat(f.Shade, f.Corners.Length)], 4)).ToArray();
    var sectors = NativeAuthoredMeshRenderer.AddedSectors(faces, 5200);
    int nearFaces = sectors.Sum(s => s[22]);
    int cache = 5200; bool cacheOk = true;
    foreach (var s in sectors) { cacheOk &= (BinaryPrimitives.ReadUInt32LittleEndian(s.AsSpan(12)) & 0xFFFF) == cache; cache += s[22]; }
    bool flags = sectors.All(s => Enumerable.Range(0, s[22]).All(k => { int hpf = 28 + 4 * s[23] + 4 * s[20] + 8 * s[21] + 16 * k; return I(s, hpf + 8) == 4 && (I(s, hpf + 12) & 0x40) != 0; }));
    Check(sectors.Count == 1 && nearFaces >= frame.Count && cacheOk && flags && sectors.All(s => s[20] <= 170),
        $"{shape.Name} at 150%: its frame is one sector of {nearFaces} faces (texture 4, subdivided only up close), per-face runs from 5200");
}
Check(Throws(() => NativeAuthoredMeshRenderer.AddedSectors([new([new(0, 0, 0), new(10, 0, 0), new(0, 10, 0)], [new(1, 1, 1), new(1, 1, 1), new(1, 1, 1)], [new(1, 1, 1), new(1, 1, 1), new(1, 1, 1)], 200)], 1)),
    "a texture number past the game's 7 bits is refused");

// ---------------- the saved placement ----------------
{
    string dir = Path.Combine(Path.GetTempPath(), "world-entrance-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        Check(WorldEntranceDocument.Read(dir) == null, "a project without an entrance has none");
        var doc = new WorldEntranceDocument(1, "artisans", new Vector3f(4428, 3024, 384), 128, DoorwayShapes.StoneArch.Id, 1.25f) { FaceTexture = 4, SideTexture = 6 };
        await doc.SaveAsync(dir);
        var back = WorldEntranceDocument.Read(dir);
        Check(back != null && back.HomeworldKey == "artisans" && back.Position == new Vector3f(4428, 3024, 384) && back.Heading == 128 && back.Shape == "stone-arch" &&
              Math.Abs(back.Scale - 1.25f) < 1e-6 && back.FaceTexture == 4 && back.SideTexture == 6 && back.SolidFrame, "the entrance is saved and read back (world-entrance.json)");
        var spec = back!.Spec();
        Check(spec.HomeworldKey == "artisans" && spec.Heading == 128 && Math.Abs(spec.Scale - 1.25) < 1e-6 && spec.FaceTexture == 4 && spec.SideTexture == 6, "the saved entrance is Create BIN's spec");
        File.WriteAllText(WorldEntranceDocument.PathFor(dir), "{\"schemaVersion\":1,\"homeworldKey\":\"stonehill\",\"position\":{\"x\":1,\"y\":2,\"z\":3},\"heading\":0,\"shape\":\"stone-arch\",\"scale\":1}");
        Check(Throws(() => WorldEntranceDocument.Read(dir)), "an entrance in a level that is not a homeworld is refused when read");
        Check(Throws(() => new WorldEntranceDocument(1, "artisans", new Vector3f(1, 2, 3), 300, "stone-arch", 1).Validate()) &&
              Throws(() => new WorldEntranceDocument(1, "artisans", new Vector3f(1, 2, 3), 0, "spiral", 1).Validate()) &&
              Throws(() => new WorldEntranceDocument(1, "artisans", new Vector3f(1, 2, 3), 0, "stone-arch", 3).Validate()), "a heading, shape or size the game can't have is refused");
        WorldEntranceDocument.Delete(dir);
        Check(WorldEntranceDocument.Read(dir) == null, "removing the entrance deletes its file");
    }
    finally { Directory.Delete(dir, true); }
}

// ---------------- refusals before any disc is read ----------------
Check(Throws(() => NativeWorldEntrance.Apply(null!, catalogFree, new WorldEntranceSpec("stonehill", new Vector3f(1000, 1000, 100), 0, "stone-arch"), [], [], [], builtSky), "homeworlds"),
    "an entrance outside the six homeworlds is refused");
Check(Throws(() => NativeWorldEntrance.Apply(null!, catalogFree, new WorldEntranceSpec("artisans", new Vector3f(1000, 1000, 100), 0, "stone-arch"), [], [], [], new byte[20]), "sky"),
    "a destination sky that is not a native sky is refused");

if (args.Length >= 2 && args[0] == "--source")
{
    Source.Run(args[1], Check, Throws);
    Console.WriteLine(failed == 0 ? $"WORLD_ENTRANCE_SOURCE_OK checks={checks}" : $"WORLD_ENTRANCE_SOURCE_FAILED {failed}/{checks}");
    return failed == 0 ? 0 : 1;
}
Console.WriteLine(failed == 0 ? $"WORLD_ENTRANCE_FREE_OK checks={checks}" : $"WORLD_ENTRANCE_FREE_FAILED {failed}/{checks}");
return failed == 0 ? 0 : 1;

static class Source
{
    static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));

    /// <summary>A spot on the ground 300 units in front of the homeworld's first doorway, facing the same way.</summary>
    public static (Vector3f Position, int Heading) Spot(UniversalSourceLibrary lib, LevelCatalog catalog, string key)
    {
        var t = NativeWorldEntrance.Template(lib, catalog, key);
        var (bx, by, bz) = t.BottomMiddle; double a = t.FacingAngle;
        return (new Vector3f((float)Math.Round(bx / 16 - Math.Cos(a) * 300), (float)Math.Round(by / 16 - Math.Sin(a) * 300), (float)Math.Round(bz / 16)),
            (int)Math.Round(a * 128 / Math.PI) & 255);
    }

    public static void Run(string usa, Action<bool, string> Check, Func<Action, string?, bool> Throws)
    {
        UniversalSourceLibrary.RequireOriginal(usa);
        var catalog = LevelCatalog.Load(Proof.Root());
        var lib = new UniversalSourceLibrary(usa, catalog);
        var image = UniversalDiscImage.Read(usa);
        var artisans = lib.Get("artisans");
        var (o1, l1) = artisans.Data.Subfiles()[1];
        byte[] sky = Spyro1SkyBlockAnalyzer.ReadPrimarySkyBlock(artisans.Data.Bytes.AsSpan(o1, l1).ToArray())!;
        Check(Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(sky) && sky.Length == 32900, "the test's new-world sky is Artisans' own (32,900 bytes)");
        byte[] reducedSky = NativeWorldEntrance.ReducedSky(sky, 6000);
        Check(Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(reducedSky) && reducedSky.Length <= 6000 && I(reducedSky, 8) > 0, "Artisans' sky reduced to 6,000 bytes is a valid sky");
        var room = new List<string>();
        foreach (var key in NewWorldDocument.HomeworldKeys)
        {
            var level = catalog.FindByKey(key)!;
            var (position, heading) = Spot(lib, catalog, key);
            byte[] data = image.EntryBytes(level.SourceWadEntry), overlay = image.EntryBytes(level.SourceWadEntry - 1);
            var before = new UniversalLevelData(data);
            var original = PortalSourceDataLocator.Locate(usa, level);
            var spec = new WorldEntranceSpec(key, position, heading, DoorwayShapes.StoneArch.Id);
            WorldEntranceResult r;
            try { r = NativeWorldEntrance.Apply(lib, catalog, spec, data, overlay, image.Exe, sky); }
            catch (InvalidDataException e) { Check(false, $"{key}: the entrance is built ({e.Message})"); continue; }
            var after = new UniversalLevelData(r.LevelData);
            uint copy = new UniversalOverlay(overlay, UniversalExeBindings.Read(image.Exe, level.LevelId)).CopyBuffer;
            Check(GameRam.FitsPlayStation(after, copy), $"{key}: the homeworld with its entrance fits a 2 MB console ({r.Sky}, {r.SkyBytes:N0}-byte sky, {r.RoomLeft:N0} bytes left)");
            room.Add($"{level.DisplayName}: {GameRam.ModelRoom(before, copy):N0} bytes free before; entrance (stone arch) with {r.Sky} sky ({r.SkyBytes:N0}) leaves {r.RoomLeft:N0}");
            if (key is "artisans" or "gnastysworld") Check(r.Sky == WorldEntranceSky.Full, $"{key}: there is room for the whole sky");
            else Check(r.Sky == WorldEntranceSky.Reduced && r.SkyBytes < sky.Length, $"{key}: only a reduced sky fits ({r.SkyBytes:N0} bytes)");
            // the record
            var (o, l) = after.Subfiles()[1];
            byte[] body = after.Bytes.AsSpan(o, l).ToArray();
            int at = 0; for (int n = 0; n < 6; n++) at += I(body, at);
            int count = I(body, at); at += 4;
            var records = new List<int>();
            for (int k = 0; k < count; k++) { records.Add(at); int pts = I(body, at + 4); at = at + 0x20 + 12 * pts + 20; at += I(body, at); }
            int rec = records[^1];
            Check(count == original.Portals.Count + 1 && r.DoorwayIndex == original.Portals.Count, $"{key}: the entrance is doorway {count} of {count}");
            Check(I(body, rec + 0x1C) == 70 && I(body, rec + 0x14) == -1 && I(body, rec + 0x18) == r.PathRow && I(body, rec) == 0 && I(body, rec + 4) == r.OpeningPoints &&
                  Math.Abs(I(body, rec + 8) - Math.Round(Math.Cos(heading * Math.PI / 128) * 4096)) <= 1 && Math.Abs(I(body, rec + 12) - Math.Round(Math.Sin(heading * Math.PI / 128) * 4096)) <= 1,
                $"{key}: the record: return token 70, always considered, its path row, {r.OpeningPoints} points, facing the heading");
            var outline = DoorwayShapes.OutlineInLevel(DoorwayShapes.StoneArch, 1, position, heading);
            Check(Enumerable.Range(0, r.OpeningPoints).All(k => Math.Abs(I(body, rec + 0x20 + 12 * k) - outline[k].X * 16) <= 1 && Math.Abs(I(body, rec + 0x28 + 12 * k) - outline[k].Z * 16) <= 1),
                $"{key}: the record's points are the stone arch's opening in game units");
            int skyAt = rec + 0x20 + 12 * r.OpeningPoints + 20;
            Check(body.AsSpan(skyAt - 20, 20).IndexOfAnyExcept((byte)0) < 0 && I(body, skyAt) == r.SkyBytes, $"{key}: an empty sky header (the loader fills it) and the sky");
            // ground kinds and collision
            var surfaces = PortalSourceDataLocator.ParseTerrainSurfaces(body, o, key, level.DisplayName);
            var mine = surfaces.SpecialSurfaces.Single(s => s.Type == 6 && s.Param1 == 70);
            Check(mine.Index == original.SpecialSurfaces.Count && mine.Param2 == r.DoorwayIndex, $"{key}: a ground kind of type 6 leads to 70 through doorway {r.DoorwayIndex}");
            var entry = surfaces.CollisionSurfaceTriangles.Where(t => t.SurfaceType == 6 && t.Param1 == 70).ToArray();
            Check(entry.Length == r.EntryTriangles && entry.All(t => t.Param2 == r.DoorwayIndex), $"{key}: {entry.Length} entry triangles");
            Check(surfaces.CollisionSurfaceTriangles.Where(t => t.SurfaceType == 6 && t.Param1 != 70).GroupBy(t => t.Param1).ToDictionary(g => g.Key, g => g.Count())
                  .OrderBy(p => p.Key).SequenceEqual(original.TriggerTriangles.GroupBy(t => t.DestinationLevelId).ToDictionary(g => g.Key, g => g.Count()).OrderBy(p => p.Key)),
                $"{key}: the homeworld's own doorways keep their entry triangles");
            // objects
            var scene = after.Scene();
            int[] classes = r.Rows.Select(row => (int)BinaryPrimitives.ReadUInt16LittleEndian(after.Row(row).AsSpan(0x36))).ToArray();
            Check(classes.SequenceEqual(new[] { 0x18E, 1, 0x11E }) && r.Rows[0] == r.PathRow && r.Rows.All(row => row >= before.Scene().RowCount),
                $"{key}: the path object, the floating name and the return controller are added after the level's own objects");
            var name = after.CaptureRow(key, r.Rows[1]);
            Check(I(name.Properties, 0) == NativeLevelWindow.VirtualIndex(70), $"{key}: the floating name shows name 36 (the window's homeworld name)");
            var path = after.CaptureRow(key, r.Rows[0]);
            var target = path.InternalPointers.Single().Target;
            var nodes = Enumerable.Range(0, path.Properties[target]).Select(n => (X: I(path.Properties, target + 8 + 16 * n) / 16.0, Y: I(path.Properties, target + 12 + 16 * n) / 16.0)).ToArray();
            Check(nodes.Length == 2 && nodes.All(n => Math.Abs(n.X - position.X) + Math.Abs(n.Y - position.Y) < 400), $"{key}: the path's two nodes moved with the doorway");
            var (fx, fy) = DoorwayShapes.Facing(heading);
            double along0 = (nodes[0].X - position.X) * fx + (nodes[0].Y - position.Y) * fy, along1 = (nodes[1].X - position.X) * fx + (nodes[1].Y - position.Y) * fy;
            Check(Math.Sign(along0) != Math.Sign(along1), $"{key}: the path crosses the opening ({along0:0} / {along1:0} units along the facing)");
            // nothing of the homeworld's own lost
            Check(after.Subfiles()[2].Length == before.Subfiles()[2].Length &&
                  after.Bytes.AsSpan(after.Subfiles()[2].Offset, after.Subfiles()[2].Length).SequenceEqual(before.Bytes.AsSpan(before.Subfiles()[2].Offset, before.Subfiles()[2].Length)) &&
                  r.Overlay.AsSpan().SequenceEqual(overlay), $"{key}: models and overlay unchanged");
            // determinism and refusals
            var again = NativeWorldEntrance.Apply(lib, catalog, spec, data, overlay, image.Exe, sky);
            Check(again.LevelData.AsSpan().SequenceEqual(r.LevelData), $"{key}: the same entrance builds the same bytes");
            Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, spec, r.LevelData, overlay, image.Exe, sky), "already has a doorway to the New World"),
                $"{key}: a second entrance is refused");
            if (key is "peacekeepers" or "dreamweavers")
            {
                Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, spec, data, overlay, image.Exe, sky, WorldEntranceSky.Full), "no room"),
                    $"{key}: the whole sky does not fit a 2 MB console and is refused in plain words");
                using (GameRam.Use(GameMemoryMode.Emulator8MB))
                {
                    var big = NativeWorldEntrance.Apply(lib, catalog, spec, data, overlay, image.Exe, sky);
                    Check(big.Sky == WorldEntranceSky.Full, $"{key}: with the 8 MB setting the whole sky fits");
                }
            }
        }
        foreach (var line in room) Console.WriteLine("  " + line);
        // ---- Artisans: refusals, options, the doorway table full, after another object edit ----
        var art = catalog.FindByKey("artisans")!;
        byte[] aData = image.EntryBytes(10), aOverlay = image.EntryBytes(9);
        var basic = new WorldEntranceSpec("artisans", new Vector3f(4428, 3024, 384), 128, DoorwayShapes.StoneArch.Id);
        Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, basic with { Position = new Vector3f(10, 3024, 384) }, aData, aOverlay, image.Exe, sky), "outside the game's map"),
            "a spot whose doorway would reach past the map's edge is refused");
        Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, basic with { Shape = "spiral" }, aData, aOverlay, image.Exe, sky), "shape"), "an unknown shape is refused");
        Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, basic with { Scale = 2 }, aData, aOverlay, image.Exe, sky), "size"), "a size past 150% is refused");
        Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, basic with { FaceTexture = 68 }, aData, aOverlay, image.Exe, sky), "textures 0-67"), "a texture Artisans does not have is refused");
        Check(NativeWorldEntrance.TextureCount(aData) == 68 && NativeWorldEntrance.ArchTexture(lib, catalog, "artisans") == 4, "Artisans has 68 textures; its arches use texture 4");
        var plainR = NativeWorldEntrance.Apply(lib, catalog, basic with { SolidFrame = false, FaceTexture = 6, SideTexture = 3 }, aData, aOverlay, image.Exe, sky, WorldEntranceSky.Plain);
        Check(plainR.Sky == WorldEntranceSky.Plain && plainR.SkyBytes == 16 && plainR.FrameTriangles == 0 && plainR.FaceTexture == 6 && plainR.SideTexture == 3 && plainR.AddedBytes < 3500,
            $"a one-colour sky and no frame collision cost {plainR.AddedBytes:N0} bytes in Artisans");
        foreach (var shape in DoorwayShapes.All)
        foreach (double scale in new[] { DoorwayShapes.MinScale, DoorwayShapes.MaxScale })
        {
            try
            {
                var s = NativeWorldEntrance.Apply(lib, catalog, basic with { Shape = shape.Id, Scale = scale }, aData, aOverlay, image.Exe, sky, WorldEntranceSky.Plain);
                Check(s.FrameSectors == 1 && s.OpeningPoints == shape.Opening.Count, $"Artisans: a {shape.Name} at {scale:P0} builds ({s.FrameFaces} faces, {s.EntryTriangles} entry triangles)");
            }
            catch (InvalidDataException e) { Check(false, $"Artisans: a {shape.Name} at {scale:P0} builds ({e.Message})"); }
        }
        // a homeworld whose doorway table is full: Gnasty's World has 4 doorways; give it a 5th (another entrance's record
        // copied in) and a 6th, then the entrance is refused.
        {
            var gn = catalog.FindByKey("gnastysworld")!;
            var (gp, gh) = Spot(lib, catalog, "gnastysworld");
            var first = NativeWorldEntrance.Apply(lib, catalog, new WorldEntranceSpec("gnastysworld", gp, gh, "square-gate"), image.EntryBytes(70), image.EntryBytes(69), image.Exe, sky, WorldEntranceSky.Plain);
            byte[] full = first.LevelData.ToArray();
            // pretend the entrance record is an ordinary doorway (token 99): the table then has 5 and room for one
            BinaryPrimitives.WriteInt32LittleEndian(full.AsSpan(LastRecord(full) + 0x1C), 99);
            var second = NativeWorldEntrance.Apply(lib, catalog, new WorldEntranceSpec("gnastysworld", gp with { X = gp.X + 400 }, gh, "square-gate"), full, image.EntryBytes(69), image.Exe, sky, WorldEntranceSky.Plain);
            Check(second.DoorwayIndex == 5, "Gnasty's World with five doorways takes the entrance as its sixth");
            byte[] six = second.LevelData.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(six.AsSpan(LastRecord(six) + 0x1C), 98);
            Check(Throws(() => NativeWorldEntrance.Apply(lib, catalog, new WorldEntranceSpec("gnastysworld", gp with { Y = gp.Y + 400 }, gh, "square-gate"), six, image.EntryBytes(69), image.Exe, sky, WorldEntranceSky.Plain), "at most 6"),
                "a homeworld that already has six doorways is refused");
            _ = gn;
        }
        // after another object edit in Artisans (a copied gem, composed as Create BIN's object stage does)
        {
            var gemRow = Enumerable.Range(0, artisans.Data.Scene().RowCount).First(i => GemValue.TryFromIdByte(BinaryPrimitives.ReadUInt16LittleEndian(artisans.Data.Row(i).AsSpan(0x36)), out _));
            byte[] g = artisans.Data.Row(gemRow);
            var composed = UniversalMobyComposer.Compose(art, aData, aOverlay, image.Exe, k => lib.Get(k), lib.SharedClasses, [],
                [new UniversalMobyPlacement("artisans", gemRow, I(g, 0x0C) + 800, I(g, 0x10), I(g, 0x14))], sharedModelPackage: lib.SharedModelPackage);
            var staged = NativeWorldEntrance.Apply(lib, catalog, basic, composed.LevelData, composed.Overlay, composed.Executable, sky);
            var sd = new UniversalLevelData(staged.LevelData);
            Check(staged.Rows[0] == artisans.Data.Scene().RowCount + 1 && BinaryPrimitives.ReadUInt16LittleEndian(sd.Row(artisans.Data.Scene().RowCount).AsSpan(0x36)) == BinaryPrimitives.ReadUInt16LittleEndian(g.AsSpan(0x36)),
                "after an object added to Artisans, the entrance's objects come after it and the copied gem stays");
        }
    }

    static int LastRecord(byte[] data)
    {
        var d = new UniversalLevelData(data);
        int table = d.Subfiles()[1].Offset;
        for (int n = 0; n < 6; n++) table += I(data, table);
        int count = I(data, table), at = table + 4, last = at;
        for (int k = 0; k < count; k++) { last = at; int pts = I(data, at + 4); at = at + 0x20 + 12 * pts + 20; at += I(data, at); }
        return last;
    }
}

static class Proof
{
    public static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "spyro-level-catalog.json"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("spyro-level-catalog.json");
        return root;
    }

    /// <summary>Points homeworld <paramref name="data"/>'s doorway to <paramref name="from"/> at <paramref name="to"/> instead:
    /// its ground kind's destination, its return token and its floating name (the way the route composer retargets one).</summary>
    public static byte[] Retarget(byte[] data, string key, int from, int to)
    {
        var d = new UniversalLevelData(data.ToArray());
        var (offset, length) = d.Subfiles()[1];
        var surfaces = PortalSourceDataLocator.ParseTerrainSurfaces(d.Bytes.AsSpan(offset, length).ToArray(), offset, key, key);
        var surface = surfaces.SpecialSurfaces.Single(s => s.Type == 6 && s.Param1 == from);
        BinaryPrimitives.WriteInt32LittleEndian(d.Bytes.AsSpan((int)surface.WadOffset + 4), to);
        int at = offset;
        for (int i = 0; i < 6; i++) at += BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(at));
        int count = BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(at)); at += 4;
        for (int k = 0; k < count; k++)
        {
            int pts = BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(at + 4));
            if (BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(at + 0x1C)) == from) BinaryPrimitives.WriteInt32LittleEndian(d.Bytes.AsSpan(at + 0x1C), to);
            int sky = at + 0x20 + 12 * pts + 20;
            at = sky + BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(sky));
        }
        var control = HomeworldPortalControlCatalog.ForLevel(key).Single(c => c.DestinationLevelId == from);
        var scene = d.Scene();
        int row = d.Subfiles()[3].Offset + scene.RowsOffset + control.LetteringTrueIndex * UniversalLevelData.RowStride;
        int props = d.Subfiles()[3].Offset + BinaryPrimitives.ReadInt32LittleEndian(d.Bytes.AsSpan(row));
        BinaryPrimitives.WriteInt32LittleEndian(d.Bytes.AsSpan(props), NativeLevelWindow.VirtualIndex(to));
        return d.Bytes;
    }

    public static int Build(string usa, string output, string at, string shape, double scale, WorldEntranceSky sky, string homeworld)
    {
        UniversalSourceLibrary.RequireOriginal(usa);
        var catalog = LevelCatalog.Load(Root());
        var originals = new UniversalSourceLibrary(usa, catalog);
        var image = UniversalDiscImage.Read(usa);
        var level = catalog.FindByKey(homeworld) ?? throw new InvalidDataException(homeworld);
        float[] v = at.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var spec = new WorldEntranceSpec(homeworld, new Vector3f(v[0], v[1], v[2]), (int)v[3], shape, scale);
        byte[] exe = image.Exe.ToArray();
        // 70: a copy of Artisans whose Sunny Flight doorway leads back to the entry homeworld (its sky is Artisans').
        var artisans = originals.Get("artisans");
        byte[] home70 = Retarget(image.EntryBytes(10), "artisans", 15, level.LevelId);
        var (o1, l1) = artisans.Data.Subfiles()[1];
        byte[] sky70 = Spyro1SkyBlockAnalyzer.ReadPrimarySkyBlock(artisans.Data.Bytes.AsSpan(o1, l1).ToArray())!;
        var entrance = NativeWorldEntrance.Apply(originals, catalog, spec, image.EntryBytes(level.SourceWadEntry), image.EntryBytes(level.SourceWadEntry - 1), exe, sky70, sky);
        Console.WriteLine(entrance.Describe(level.DisplayName));
        byte[] wad = image.RebuildWad(new Dictionary<int, byte[]> { [level.SourceWadEntry] = entrance.LevelData, [level.SourceWadEntry - 1] = entrance.Overlay });
        var entries = new Dictionary<int, byte[]> { [102] = image.EntryBytes(9), [103] = home70 };
        var placement = NativeLevelWindowDisc.Place(image, wad, entries, exe.Length, forceAfterImage: true);
        int Music(int index) => (int)BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(0x8006EF9C + 4 * (uint)index - 0x8000F800)));
        int Kill(int index) => (int)BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(0x8006E9A4 + 4 * (uint)index - 0x8000F800)));
        int Gems(int index) => BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan((int)(0x8006F438 + 2 * index - 0x8000F800)));
        int Dragons(int index) => exe[(int)(0x8006F414 + index - 0x8000F800)];
        WindowGuest[] world = [new(70, "NEW HOME", 10, Music(0), Kill(0), 8192, Gems(0), Dragons(0), 102, 103)];
        var plan = NativeLevelWindow.Plan(exe, placement.Table, world, "NEW WORLD");
        NativeLevelWindow.Install(exe, placement.Table, world, "NEW WORLD");
        NativeLevelWindow.PatchSaveOverlayInWad(placement.Wad);
        if (File.Exists(output)) File.Delete(output);
        NativeLevelWindowDisc.Write(image, output, placement, exe);
        File.WriteAllText(Path.ChangeExtension(output, ".cue"), $"FILE \"{Path.GetFileName(output)}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        var read = UniversalDiscImage.Read(output);
        if (!read.EntryBytes(level.SourceWadEntry).AsSpan().SequenceEqual(entrance.LevelData)) throw new InvalidDataException("The homeworld's readback differs.");
        foreach (var (entry, bytes) in entries)
            if (!NativeLevelWindowDisc.ReadEntry(output, entry).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException($"WAD entry {entry} readback differs.");
        var template = NativeWorldEntrance.Template(originals, catalog, homeworld);
        var info = new
        {
            image = output,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(output))),
            entrance = new
            {
                spec.HomeworldKey, level.LevelId, x = spec.Position.X, y = spec.Position.Y, z = spec.Position.Z, spec.Heading, spec.Shape, spec.Scale,
                entrance.DoorwayIndex, entrance.PathRow, entrance.Rows, sky = entrance.Sky.ToString(), entrance.SkyBytes, entrance.SkyParts, entrance.OpeningPoints,
                entrance.EntryTriangles, entrance.FrameFaces, entrance.FrameSectors, entrance.FrameTriangles, entrance.FaceTexture, entrance.SideTexture,
                entrance.AddedBytes, entrance.RoomLeft, template = template.DestinationLevelId,
            },
            wayBack = new { level = 70, destination = level.LevelId, token = level.LevelId, from = 15 },
            labels = plan.Labels.Where(p => p.Key.StartsWith("LW_", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => $"0x{p.Value:X8}"),
        };
        File.WriteAllText(Path.ChangeExtension(output, ".json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"WORLD_ENTRANCE_PROOF_IMAGE {output}");
        return 0;
    }
}
