using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Sky;

// Sky model readers (Core/Sky: ObjSkyReader, FbxSkyReader) on fixtures written here, in a temporary folder:
//   OBJ + MTL: quads and a pentagon, negative indices, map_Kd options, a backslash path with spaces, a Windows absolute path
//   found next to the OBJ, a material with only Kd, vertex colours, a missing texture and a missing MTL (notes), one picture
//   shared by two materials (decoded once);
//   FBX: binary 7400 and 7500, zlib and raw arrays, and ASCII: a cube with UVs (ByPolygonVertex IndexToDirect), a material,
//   an embedded texture, a translated/rotated/scaled model under a translated parent, a vertex-coloured triangle; textures
//   on disk and in <name>.fbm; the up axes; and broken or too-large files refused.
int checks = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what)
{
    checks++;
    if (!ok) throw new InvalidOperationException("FAIL: " + what);
    if (verbose) Console.WriteLine("ok   " + what);
}
void Refuses(string what, Action read, string words)
{
    string? message = null;
    try { read(); }
    catch (InvalidDataException e) { message = e.Message; }
    Check(message != null && message.Contains(words, StringComparison.OrdinalIgnoreCase), $"{what} is refused mentioning “{words}” ({message ?? "no error"})");
}
bool At(SkyModelCorner c, double x, double y, double z) => Math.Abs(c.X - x) < 1e-9 && Math.Abs(c.Y - y) < 1e-9 && Math.Abs(c.Z - z) < 1e-9;
bool Uv(SkyModelCorner c, double u, double v) => Math.Abs(c.U - u) < 1e-9 && Math.Abs(c.V - v) < 1e-9;

Rgba32 red = new(255, 0, 0, 255), green = new(0, 255, 0, 255), blue = new(0, 0, 255, 255), grey = new(128, 128, 128, 255);
string temp = Path.Combine(Path.GetTempPath(), $"spyro-sky-model-smoke-{Environment.ProcessId}");
Directory.CreateDirectory(temp);
int decodes = 0;
SkyImage Decode(byte[] bytes, int maxWidth)
{
    if (maxWidth != 4096) throw new InvalidOperationException($"The decoder was asked for width {maxWidth}, not 4096.");
    decodes++;
    string file = Path.Combine(temp, $"decode-{decodes}.png");
    File.WriteAllBytes(file, bytes);
    try
    {
        Rgba32[] pixels = PngRgbaImage.ReadRgba(file, out int width, out int height);
        return new SkyImage(width, height, pixels);
    }
    finally { File.Delete(file); }
}
SkyImageDecoder decoder = Decode;

try
{
    // A 4x2 picture: top row red, bottom row blue; a 2x2 green one.
    Rgba32[] skyPixels = [red, red, red, red, blue, blue, blue, blue];
    string skyPng = Path.Combine(temp, "sky.png");
    await TerrainTexturePngWriter.WriteRgbaAsync(skyPng, 4, 2, skyPixels);
    byte[] skyBytes = File.ReadAllBytes(skyPng);

    // ---------------- OBJ ----------------
    string objFolder = Path.Combine(temp, "obj");
    Directory.CreateDirectory(Path.Combine(objFolder, "materials", "textures"));
    File.Copy(skyPng, Path.Combine(objFolder, "materials", "textures", "my sky.png"));
    await TerrainTexturePngWriter.WriteRgbaAsync(Path.Combine(objFolder, "side.png"), 2, 2, [green, green, green, green]);
    File.WriteAllText(Path.Combine(objFolder, "materials", "sky.mtl"), """
        # materials
        newmtl Textured
        Ka 0 0 0
        Kd 1 1 1
        map_Kd -s 1 1 1 -o 0 0 textures\My Sky.png
        newmtl KdOnly
        Kd 0.2 0.4 0.6
        newmtl Missing
        Kd 1 0 0
        map_Kd nothere.png
        newmtl SameTexture
        map_Kd ./TEXTURES/my sky.png
        newmtl NextToObj
        Kd 0 1 0
        map_Kd C:\Users\artist\skies\side.png
        """);
    File.WriteAllText(Path.Combine(objFolder, "extra.mtl"), "newmtl ExtraKd\nKd 0 0 0.5\n");
    string objPath = Path.Combine(objFolder, "model.obj");
    File.WriteAllText(objPath, """
        # sky smoke
        mtllib materials/sky.mtl extra.mtl
        mtllib gone.mtl
        o Sky
        g dome
        v -1 -1 -1
        v 1 -1 -1
        v 1 1 -1
        v -1 1 -1
        v 0 0 5 1 0 0
        v 1 0 5 0 1 0
        v 0 1 5 0 0 1
        vt 0 0
        vt 1 0
        vt 1 1
        vt 0.25 0.75
        vn 0 0 1
        usemtl Textured
        f 1/1 2/2 3/3 4/4
        usemtl KdOnly
        f -3//1 -2//1 -1//1
        usemtl Missing
        f 1/1/1 2/2/1 3/3/1
        usemtl SameTexture
        f 1/4 2/4 3/4
        usemtl NextToObj
        f 4/1 3/2 2/3 1/4 5/1
        usemtl ExtraKd
        f 1 2 3
        usemtl Nowhere
        f 5 6 7
        """.ReplaceLineEndings("\r\n"));
    SkyModel obj = ObjSkyReader.Read(objPath, decoder);
    var t = obj.Triangles;
    Check(t.Count == 10, $"OBJ: 10 triangles (quad 2, pentagon 3, five triangles), got {t.Count}");
    Check(obj.UpAxis == SkyModelUpAxis.Y, "OBJ: up axis is Y");
    Check(obj.Textures.Count == 2 && decodes == 2, $"OBJ: two distinct pictures, each decoded once ({obj.Textures.Count}, {decodes} decodes)");
    int skyIndex = t[0].Texture, sideIndex = t[5].Texture;
    Check(skyIndex >= 0 && obj.Textures[skyIndex].Width == 4 && obj.Textures[skyIndex].Height == 2, "OBJ: map_Kd with options, backslashes, spaces and other letter case decodes the 4x2 picture");
    Check(obj.Textures[skyIndex].Pixels[0] == red && obj.Textures[skyIndex].Pixels[4] == blue, "OBJ: the picture's rows run top to bottom");
    Check(At(t[0].A, -1, -1, -1) && At(t[0].B, 1, -1, -1) && At(t[0].C, 1, 1, -1), "OBJ: the quad's first triangle has corners 1, 2, 3");
    Check(At(t[1].A, -1, -1, -1) && At(t[1].B, 1, 1, -1) && At(t[1].C, -1, 1, -1), "OBJ: the quad's fan triangle has corners 1, 3, 4");
    Check(Uv(t[0].A, 0, 1) && Uv(t[0].C, 1, 0) && Uv(t[1].C, 0.25, 0.25), "OBJ: V is flipped so 0 is the top row");
    Check(obj.Textures[skyIndex].Sample(t[0].C.U, t[0].C.V).R > 250, "OBJ: vt (1, 1) samples the picture's top (red) row");
    Check(t[0].Texture == t[1].Texture && t[0].MaterialColor == new Rgba32(255, 255, 255, 255), "OBJ: the textured quad keeps its Kd");
    Check(t[0].A.Color == null, "OBJ: corners without vertex colours have none");
    Check(t[2].Texture == -1 && t[2].MaterialColor == new Rgba32(51, 102, 153, 255), "OBJ: a material with only Kd gives its colour");
    Check(At(t[2].A, 0, 0, 5) && At(t[2].C, 0, 1, 5), "OBJ: negative indices count back from the latest vertex");
    Check(t[2].A.Color == red && t[2].B.Color == green && t[2].C.Color == blue, "OBJ: vertex colours are read");
    Check(t[3].Texture == -1 && t[3].MaterialColor == red, "OBJ: a missing picture falls back to Kd");
    Check(t[4].Texture == skyIndex && t[4].MaterialColor == grey && Uv(t[4].A, 0.25, 0.25), "OBJ: the same picture named another way is shared; no Kd gives grey");
    Check(sideIndex >= 0 && sideIndex != skyIndex && obj.Textures[sideIndex].Width == 2, "OBJ: a Windows absolute path is found by name next to the OBJ");
    Check(t[5].Texture == t[6].Texture && t[7].Texture == sideIndex && At(t[7].A, -1, 1, -1) && At(t[7].C, 0, 0, 5), "OBJ: the pentagon is a three-triangle fan");
    Check(t[8].MaterialColor == new Rgba32(0, 0, 128, 255) && t[8].Texture == -1, "OBJ: a second MTL on the same mtllib line is read");
    Check(t[9].Texture == -1 && t[9].MaterialColor == grey && t[9].A.Color == red, "OBJ: an unknown material is grey and keeps vertex colours");
    Check(obj.Notes.Count == 2 && obj.Notes.Any(n => n.Contains("gone.mtl")) && obj.Notes.Any(n => n.Contains("nothere.png")),
        $"OBJ: notes name the missing MTL and the missing picture ({string.Join(" | ", obj.Notes)})");

    string badObj = Path.Combine(temp, "bad.obj");
    void ObjFile(string text) => File.WriteAllText(badObj, text);
    ObjFile("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 9\n");
    Refuses("OBJ: a corner past the last vertex", () => ObjSkyReader.Read(badObj, decoder), "out of range");
    ObjFile("v 0 0 0\nv 1 0 0\nv 0 1 0\nf -9 1 2\n");
    Refuses("OBJ: a negative corner before the first vertex", () => ObjSkyReader.Read(badObj, decoder), "out of range");
    ObjFile("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 0 1 2\n");
    Refuses("OBJ: corner index 0", () => ObjSkyReader.Read(badObj, decoder), "Line 4");
    ObjFile("v 0 x 0\n");
    Refuses("OBJ: a vertex with a word for a number", () => ObjSkyReader.Read(badObj, decoder), "number");
    ObjFile("v 0 0 0\n");
    Refuses("OBJ: no faces", () => ObjSkyReader.Read(badObj, decoder), "no faces");
    ObjFile("v 0 0 0\nf " + string.Join(' ', Enumerable.Repeat("1", 2_000_003)) + "\n");
    Refuses("OBJ: a face of 2,000,001 triangles", () => ObjSkyReader.Read(badObj, decoder), "2,000,000");
    string hugeObj = Path.Combine(temp, "huge.obj");
    using (FileStream huge = File.Create(hugeObj)) huge.SetLength(201L * 1024 * 1024);
    Refuses("OBJ: a 201 MB file", () => ObjSkyReader.Read(hugeObj, decoder), "200 MB");
    File.Delete(hugeObj);

    // ---------------- FBX ----------------
    string fbxFolder = Path.Combine(temp, "fbx");
    Directory.CreateDirectory(fbxFolder);
    string Fbx(string name, byte[] bytes) { string file = Path.Combine(fbxFolder, name); File.WriteAllBytes(file, bytes); return file; }
    SkyModel ReadFbx(string name, byte[] bytes) => FbxSkyReader.Read(Fbx(name, bytes), decoder);

    // The cube's first corner (1, 1, -1): scaled (1, 2, 3), turned X 90 then Z 90, moved (1, 2, 3), parent moved (0, 0, 10).
    decodes = 0;
    SkyModel baseline = ReadFbx("base.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes)), 7400, compress: true));
    var f = baseline.Triangles;
    Check(f.Count == 13, $"FBX: 13 triangles (cube 12, triangle 1), got {f.Count}");
    Check(baseline.UpAxis == SkyModelUpAxis.Y && baseline.Notes.Count == 0, $"FBX: Y up and no notes ({string.Join(" | ", baseline.Notes)})");
    Check(At(f[0].A, -2, 3, 15), $"FBX: rotation X then Z, scale, translation and the parent place corner (1, 1, -1) at (-2, 3, 15), got ({f[0].A.X}, {f[0].A.Y}, {f[0].A.Z})");
    Check(At(f[2].C, 4, 1, 11), "FBX: corner (-1, -1, 1) lands at (4, 1, 11)");
    Check(Uv(f[0].A, 0, 1) && Uv(f[1].C, 0.25, 0.25), "FBX: UVs by polygon vertex through UVIndex, V flipped (UV set 0, not the set written first)");
    Check(f[0].Texture == 0 && baseline.Textures.Count == 1 && baseline.Textures[0].Width == 4 && baseline.Textures[0].Height == 2 && decodes == 1,
        "FBX: the embedded picture is decoded once at 4x2");
    Check(f.Take(12).All(x => x.Texture == 0 && x.MaterialColor == new Rgba32(64, 128, 255, 255)), "FBX: every cube face uses the material's texture and DiffuseColor");
    Check(At(f[12].A, 0, 0, -50) && At(f[12].B, 10, 0, -50) && f[12].A.Color == red && f[12].B.Color == green && f[12].C.Color == blue,
        "FBX: an untransformed model's triangle keeps its vertex colours");
    Check(f[12].Texture == -1 && f[12].MaterialColor == grey, "FBX: a mesh without a material is grey");

    bool Same(SkyModel a, SkyModel b) =>
        a.Triangles.Count == b.Triangles.Count && a.Textures.Count == b.Textures.Count && a.UpAxis == b.UpAxis &&
        a.Triangles.Zip(b.Triangles).All(p => p.First == p.Second);
    foreach (var (version, compress) in new[] { (7400, false), (7500, true), (7500, false) })
        Check(Same(baseline, ReadFbx($"v{version}{compress}.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes)), version, compress))),
            $"FBX: version {version} with {(compress ? "zlib" : "raw")} arrays reads the same");
    string ascii = FbxFixture.Ascii(FbxFixture.Scene(new(Png: skyBytes, Ascii: true)));
    SkyModel asciiModel = ReadFbx("ascii.fbx", Encoding.UTF8.GetBytes(ascii));
    Check(Same(baseline, asciiModel) && asciiModel.Textures[0].Width == 4, "FBX: the ASCII file reads the same (base64 picture included)");

    Directory.CreateDirectory(Path.Combine(fbxFolder, "fbm.fbm"));
    File.Copy(skyPng, Path.Combine(fbxFolder, "fbm.fbm", "sky.png"));
    SkyModel fbm = ReadFbx("fbm.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Embed: false, Relative: @"C:\elsewhere\Sky.PNG", Full: @"C:\elsewhere\Sky.PNG")), 7500, true));
    Check(fbm.Triangles[0].Texture == 0 && fbm.Textures[0].Width == 4 && fbm.Notes.Count == 0, "FBX: a picture not embedded is found in the <name>.fbm folder");
    Directory.CreateDirectory(Path.Combine(fbxFolder, "textures"));
    File.Copy(skyPng, Path.Combine(fbxFolder, "textures", "sky.png"));
    SkyModel onDisk = ReadFbx("disk.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Embed: false)), 7400, false));
    Check(onDisk.Triangles[0].Texture == 0 && onDisk.Textures[0].Height == 2, "FBX: a picture not embedded is found by its relative name (backslashes)");
    SkyModel missing = ReadFbx("missing.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Embed: false, Relative: "gone/nowhere.png", Full: "/no/such/nowhere.png")), 7400, true));
    Check(missing.Triangles[0].Texture == -1 && missing.Triangles[0].MaterialColor == new Rgba32(64, 128, 255, 255) && missing.Textures.Count == 0,
        "FBX: a missing picture falls back to the material colour");
    Check(missing.Notes.Count == 1 && missing.Notes[0].Contains("nowhere.png"), $"FBX: the missing picture is named in a note ({string.Join(" | ", missing.Notes)})");

    SkyModel zUp = ReadFbx("zup.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Up: 2, Front: 1, FrontSign: -1)), 7400, true));
    Check(zUp.UpAxis == SkyModelUpAxis.Z && At(zUp.Triangles[0].A, -2, 3, 15) && zUp.Notes.Count == 0, "FBX: 3ds Max's Z-up axes stay as they are, up axis Z");
    SkyModel xUp = ReadFbx("xup.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Up: 0, Front: 1, Coord: 2)), 7400, true));
    Check(xUp.UpAxis == SkyModelUpAxis.Y && At(xUp.Triangles[0].A, 15, -2, 3) && xUp.Notes.Count == 1, $"FBX: X-up axes are turned to Y-up ({string.Join(" | ", xUp.Notes)})");
    SkyModel down = ReadFbx("down.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, UpSign: -1, CoordSign: -1)), 7400, true));
    Check(down.UpAxis == SkyModelUpAxis.Y && At(down.Triangles[0].A, 2, -3, 15) && down.Notes.Count == 1, $"FBX: -Y-up axes are turned to +Y-up ({string.Join(" | ", down.Notes)})");
    SkyModel zyx = ReadFbx("zyx.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, RotationOrder: 5)), 7500, true));
    Check(At(zyx.Triangles[0].A, -1, 5, 14), "FBX: rotation order ZYX turns Z first");

    byte[] good = FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes)), 7400, compress: true);
    Refuses("FBX: a file cut short", () => ReadFbx("cut.fbx", good[..(good.Length * 6 / 10)]), "cut short");
    Refuses("FBX: a file cut inside its header", () => ReadFbx("cuthead.fbx", good[..12]), "cut short");
    Refuses("FBX: an ASCII file cut short", () => ReadFbx("cut-ascii.fbx", Encoding.UTF8.GetBytes(ascii[..(ascii.Length * 7 / 10)])), "cut short");
    Refuses("FBX: damaged zlib data", () => ReadFbx("zlib.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes)), 7400, true, corruptZlib: true)), "zlib");
    Refuses("FBX: a corner past the last vertex", () => ReadFbx("corner.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Polygons: [0, 1, ~99])), 7400, true)), "out of range");
    Refuses("FBX: a UV index past the UV list", () => ReadFbx("uv.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, UvIndexEntry: 77)), 7500, false)), "out of range");
    int[] fan = new int[2_000_003];
    fan[^1] = ~0;
    Refuses("FBX: a polygon of 2,000,001 triangles", () => ReadFbx("fan.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes, Polygons: fan)), 7400, true)), "2,000,000");
    Refuses("FBX: version 6.1", () => ReadFbx("old.fbx", FbxFixture.Binary(FbxFixture.Scene(new(Png: skyBytes)), 6100, true)), "too old");
    Refuses("FBX: not an FBX file", () => ReadFbx("text.fbx", Encoding.UTF8.GetBytes("hello, this is not a model\n")), "not an FBX");
    string hugeFbx = Path.Combine(fbxFolder, "huge.fbx");
    using (FileStream huge = File.Create(hugeFbx)) huge.SetLength(201L * 1024 * 1024);
    Refuses("FBX: a 201 MB file", () => FbxSkyReader.Read(hugeFbx, decoder), "200 MB");
    File.Delete(hugeFbx);
}
finally
{
    try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
}
Console.WriteLine($"SKY_MODEL_OK checks={checks}");

/// <summary>Minimal FBX writers (binary 7.x and ASCII) and the test scene.</summary>
static class FbxFixture
{
    public sealed record Node(string Name, object[] Props, Node[]? Children = null);

    public sealed record Options(
        byte[] Png, bool Ascii = false, bool Embed = true, string Relative = @"textures\sky.png", string Full = @"C:\art\sky.png",
        int Up = 1, int UpSign = 1, int Front = 2, int FrontSign = 1, int Coord = 0, int CoordSign = 1, int RotationOrder = -1,
        int[]? Polygons = null, int UvIndexEntry = -1);

    /// <summary>A Null "Root" moved (0, 0, 10) holding the mesh model "Sky" (moved (1, 2, 3), turned (90, 0, 90), scaled
    /// (1, 2, 3)) with a cube, its material and embedded texture; and a vertex-coloured triangle "Tri" at the root.</summary>
    public static Node[] Scene(Options o)
    {
        string N(string name, string cls) => o.Ascii ? $"{cls}::{name}" : $"{name}\0\u0001{cls}";
        static Node P(string name, string type, string label, string flags, params object[] values) => new("P", [name, type, label, flags, .. values]);
        static Node Props(params Node[] p) => new("Properties70", [], p);
        static Node V(string name, params object[] values) => new(name, values);

        double[] cube = [1, 1, -1, -1, 1, -1, -1, -1, -1, 1, -1, -1, 1, 1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1];
        int[] polygons = o.Polygons ?? [0, 1, 2, ~3, 4, 7, 6, ~5, 0, 4, 5, ~1, 1, 5, 6, ~2, 2, 6, 7, ~3, 3, 7, 4, ~0];
        int[] uvIndex = [.. Enumerable.Range(0, 24).Select(i => i % 4)];
        if (o.UvIndexEntry >= 0) uvIndex[5] = o.UvIndexEntry;
        List<Node> skyProps =
        [
            P("Lcl Translation", "Lcl Translation", "", "A", 1.0, 2.0, 3.0),
            P("Lcl Rotation", "Lcl Rotation", "", "A", 90.0, 0.0, 90.0),
            P("Lcl Scaling", "Lcl Scaling", "", "A", 1.0, 2.0, 3.0),
        ];
        if (o.RotationOrder >= 0)
        {
            skyProps.Add(P("RotationActive", "bool", "", "", 1));
            skyProps.Add(P("RotationOrder", "enum", "", "", o.RotationOrder));
        }
        Node[] objects =
        [
            new("Geometry", [100L, N("Cube", "Geometry"), "Mesh"],
            [
                V("Vertices", cube),
                V("PolygonVertexIndex", polygons),
                V("GeometryVersion", 124),
                new("LayerElementUV", [1],
                [
                    V("Version", 101), V("Name", "Second"), V("MappingInformationType", "ByPolygonVertex"),
                    V("ReferenceInformationType", "Direct"), V("UV", Enumerable.Repeat(0.9, 48).ToArray()),
                ]),
                new("LayerElementUV", [0],
                [
                    V("Version", 101), V("Name", "UVMap"), V("MappingInformationType", "ByPolygonVertex"),
                    V("ReferenceInformationType", "IndexToDirect"), V("UV", new double[] { 0, 0, 1, 0, 1, 1, 0.25, 0.75 }), V("UVIndex", uvIndex),
                ]),
                new("LayerElementMaterial", [0],
                [
                    V("Version", 101), V("Name", ""), V("MappingInformationType", "AllSame"),
                    V("ReferenceInformationType", "IndexToDirect"), V("Materials", new[] { 0 }),
                ]),
                new("Layer", [0], [V("Version", 100), new("LayerElement", [], [V("Type", "LayerElementUV"), V("TypedIndex", 0)])]),
            ]),
            new("Geometry", [101L, N("Tri", "Geometry"), "Mesh"],
            [
                V("Vertices", new double[] { 0, 0, -50, 10, 0, -50, 0, 10, -50 }),
                V("PolygonVertexIndex", new[] { 0, 1, ~2 }),
                new("LayerElementColor", [0],
                [
                    V("Version", 101), V("Name", "Col"), V("MappingInformationType", "ByPolygonVertex"), V("ReferenceInformationType", "Direct"),
                    V("Colors", new double[] { 1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1 }),
                ]),
            ]),
            new("Model", [200L, N("Root", "Model"), "Null"], [V("Version", 232), Props(P("Lcl Translation", "Lcl Translation", "", "A", 0.0, 0.0, 10.0))]),
            new("Model", [201L, N("Sky", "Model"), "Mesh"], [V("Version", 232), Props([.. skyProps])]),
            new("Model", [202L, N("Tri", "Model"), "Mesh"], [V("Version", 232), Props()]),
            new("Material", [300L, N("SkyMat", "Material"), ""],
                [V("Version", 102), V("ShadingModel", "phong"), Props(P("DiffuseColor", "Color", "", "A", 0.25, 0.5, 1.0))]),
            new("Texture", [400L, N("SkyTex", "Texture"), ""],
            [
                V("Type", "TextureVideoClip"), V("Version", 202), V("TextureName", N("SkyTex", "Texture")), Props(),
                V("Media", N("SkyTex", "Video")), V("FileName", o.Full), V("RelativeFilename", o.Relative),
            ]),
            new("Video", [500L, N("SkyTex", "Video"), "Clip"],
            [
                V("Type", "Clip"), Props(P("Path", "KString", "XRefUrl", "", o.Full)), V("UseMipMap", 0),
                V("Filename", o.Full), V("RelativeFilename", o.Relative), V("Content", o.Embed ? o.Png : Array.Empty<byte>()),
            ]),
        ];
        Node C(string kind, long child, long parent, string? property = null) =>
            new("C", property == null ? [kind, child, parent] : [kind, child, parent, property]);
        return
        [
            new("FBXHeaderExtension", [], [V("FBXHeaderVersion", 1003), V("FBXVersion", 7400)]),
            new("GlobalSettings", [],
            [
                V("Version", 1000),
                Props(P("UpAxis", "int", "Integer", "", o.Up), P("UpAxisSign", "int", "Integer", "", o.UpSign),
                    P("FrontAxis", "int", "Integer", "", o.Front), P("FrontAxisSign", "int", "Integer", "", o.FrontSign),
                    P("CoordAxis", "int", "Integer", "", o.Coord), P("CoordAxisSign", "int", "Integer", "", o.CoordSign),
                    P("UnitScaleFactor", "double", "Number", "", 1.0)),
            ]),
            new("Objects", [], objects),
            new("Connections", [],
            [
                C("OO", 200, 0), C("OO", 201, 200), C("OO", 202, 0), C("OO", 100, 201), C("OO", 101, 202),
                C("OO", 300, 201), C("OP", 400, 300, "DiffuseColor"), C("OO", 500, 400),
            ]),
        ];
    }

    public static byte[] Binary(Node[] nodes, int version, bool compress, bool corruptZlib = false)
    {
        bool wide = version >= 7500;
        using MemoryStream stream = new();
        BinaryWriter w = new(stream);
        w.Write(Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0"));
        w.Write((byte)0x1A);
        w.Write((byte)0);
        w.Write((uint)version);
        foreach (Node node in nodes) WriteNode(w, node, wide, compress, corruptZlib);
        w.Write(new byte[wide ? 25 : 13]);
        w.Write(new byte[16]);
        w.Flush();
        return stream.ToArray();
    }

    private static void WriteOffset(BinaryWriter w, long value, bool wide)
    {
        if (wide) w.Write((ulong)value); else w.Write((uint)value);
    }

    private static void WriteNode(BinaryWriter w, Node node, bool wide, bool compress, bool corrupt)
    {
        long start = w.BaseStream.Position;
        for (int i = 0; i < 3; i++) WriteOffset(w, 0, wide);
        w.Write((byte)node.Name.Length);
        w.Write(Encoding.ASCII.GetBytes(node.Name));
        long propStart = w.BaseStream.Position;
        foreach (object prop in node.Props) WriteProp(w, prop, compress, corrupt);
        long propEnd = w.BaseStream.Position;
        if (node.Children != null)
        {
            foreach (Node child in node.Children) WriteNode(w, child, wide, compress, corrupt);
            w.Write(new byte[wide ? 25 : 13]);
        }
        long end = w.BaseStream.Position;
        w.Seek((int)start, SeekOrigin.Begin);
        WriteOffset(w, end, wide);
        WriteOffset(w, node.Props.Length, wide);
        WriteOffset(w, propEnd - propStart, wide);
        w.Seek((int)end, SeekOrigin.Begin);
    }

    private static void WriteProp(BinaryWriter w, object prop, bool compress, bool corrupt)
    {
        switch (prop)
        {
            case int i: w.Write((byte)'I'); w.Write(i); break;
            case long l: w.Write((byte)'L'); w.Write(l); break;
            case double d: w.Write((byte)'D'); w.Write(d); break;
            case string s: byte[] text = Encoding.UTF8.GetBytes(s); w.Write((byte)'S'); w.Write((uint)text.Length); w.Write(text); break;
            case byte[] raw: w.Write((byte)'R'); w.Write((uint)raw.Length); w.Write(raw); break;
            case double[] ds: WriteArray(w, 'd', ds.Length, MemoryMarshal.AsBytes(ds.AsSpan()).ToArray(), compress, corrupt); break;
            case int[] its: WriteArray(w, 'i', its.Length, MemoryMarshal.AsBytes(its.AsSpan()).ToArray(), compress, corrupt); break;
            default: throw new NotSupportedException(prop.GetType().Name);
        }
    }

    private static void WriteArray(BinaryWriter w, char type, int count, byte[] raw, bool compress, bool corrupt)
    {
        w.Write((byte)type);
        w.Write((uint)count);
        if (!compress)
        {
            w.Write(0u);
            w.Write((uint)raw.Length);
            w.Write(raw);
            return;
        }
        using MemoryStream packed = new();
        using (ZLibStream zlib = new(packed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw);
        byte[] bytes = packed.ToArray();
        if (corrupt) for (int i = 2; i < bytes.Length; i++) bytes[i] ^= 0x5A;
        w.Write(1u);
        w.Write((uint)bytes.Length);
        w.Write(bytes);
    }

    public static string Ascii(Node[] nodes)
    {
        StringBuilder text = new("; FBX 7.4.0 project file\n; ----------------------------------------------------\n\n");
        foreach (Node node in nodes) WriteAscii(text, node, "");
        return text.ToString();
    }

    private static string Value(object value) => value switch
    {
        string s => $"\"{s}\"",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException(value.GetType().Name),
    };

    private static void WriteAscii(StringBuilder text, Node node, string indent)
    {
        text.Append(indent).Append(node.Name).Append(": ");
        if (node.Props is [double[] or int[]])
        {
            double[] values = node.Props[0] is double[] d ? d : Array.ConvertAll((int[])node.Props[0], v => (double)v);
            text.Append('*').Append(values.Length).Append(" {\n").Append(indent).Append("\ta: ");
            for (int i = 0; i < values.Length; i++)
                text.Append(i == 0 ? "" : i % 7 == 0 ? ",\n" + indent + "\t" : ",").Append(Value(values[i]));
            text.Append('\n').Append(indent).Append("}\n");
            return;
        }
        if (node.Props is [byte[] content])
        {
            text.Append(", \"").Append(Convert.ToBase64String(content)).Append("\"\n");
            return;
        }
        text.Append(string.Join(", ", node.Props.Select(Value)));
        if (node.Children == null) { text.Append('\n'); return; }
        text.Append(" {\n");
        foreach (Node child in node.Children) WriteAscii(text, child, indent + "\t");
        text.Append(indent).Append("}\n");
    }
}
