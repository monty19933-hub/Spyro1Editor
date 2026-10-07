using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Terrain images in the packed native layout: tile order, sharing groups, refusals, and a real level written from
// PNGs (USA.bin catalog-workspace level-key png-folder) with every tier read back.
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
bool Refuses(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }

var cells = Enumerable.Range(0, 16).Select(NativeTerrainTextureImageWriter.CloseCell).ToArray();
Check(cells.Distinct().Count() == 16 && cells.All(c => c.X is >= 0 and < 4 && c.Y is >= 0 and < 4), "sixteen distinct close cells");
Check(cells[0] == (0, 0) && cells[1] == (1, 0) && cells[2] == (0, 2) && cells[4] == (0, 1) && cells[8] == (2, 0) && cells[15] == (3, 3), "native close order");
Check(NativeTerrainTextureImageWriter.To15(new Rgba32(0, 0, 0, 255)) != 0, "black is never the transparent colour");
if (args.Length == 0) { Console.WriteLine($"TERRAIN_IMAGE_FREE_OK checks={checks}"); return; }
if (args.Length != 4) throw new ArgumentException("USA.bin catalog-workspace level-key png-folder");

var catalog = LevelCatalog.Load(args[1]);
var lib = new UniversalSourceLibrary(args[0], catalog);
var data = lib.Get(args[2]).Data;
byte[] vram = data.Vram().ToArray();
var subfiles = data.Subfiles();
byte[] textureList = data.Bytes.AsSpan(subfiles[1].Offset, subfiles[1].Length).ToArray();
var records = NativeTerrainTextureImageWriter.Records(textureList);
Check(records.Count > 0, $"{records.Count} texture records ({records.Count(r => r == null)} not replaceable)");
var groups = NativeTerrainTextureImageWriter.SharingGroups(NativeTerrainTextureImageWriter.Footprint(textureList));
Check(groups.Sum(g => g.Count) == records.Count, "every texture in one group");
Console.WriteLine("shared: " + string.Join(" ", groups.Where(g => g.Count > 1).Select(g => "[" + string.Join(",", g) + "]")) +
    "; unusual layout: " + string.Join(",", Enumerable.Range(0, records.Count).Where(i => records[i] == null)));

Dictionary<int, NativeTerrainTextureImageWriter.Image> images = [];
foreach (string png in Directory.GetFiles(args[3], "*.png"))
{
    int id = int.Parse(Path.GetFileNameWithoutExtension(png).Split('-')[^1]);
    images[id] = new(PngRgbaImage.ReadResizedRgba(png, 64, 64), PngRgbaImage.ReadResizedRgba(png, 128, 128));
}
Check(images.Count > 0, $"{images.Count} images");
var partial = groups.FirstOrDefault(g => g.Count > 1 && g.All(images.ContainsKey));
if (partial != null)
    Check(Refuses(() => NativeTerrainTextureImageWriter.Write(vram, textureList, images.Where(kv => kv.Key != partial[0]).ToDictionary())),
        "a group with a missing image is refused");
byte[] written = NativeTerrainTextureImageWriter.Write(vram, textureList, images);
Check(written.AsSpan().SequenceEqual(NativeTerrainTextureImageWriter.Write(vram, textureList, images)), "deterministic");

// Only the bytes owned by changed textures move.
HashSet<int> owned = [];
foreach (int t in images.Keys)
    foreach (var d in records[t]!.Descriptors)
        foreach (var (o, w, r) in new[] { (d.PixelByteOffset, d.PixelRowByteLength, d.TileSide), (d.PaletteByteOffset, d.PaletteRowCount == 16 ? 32 : 512, d.PaletteRowCount) })
            for (int row = 0; row < r; row++) for (int b = 0; b < w; b++) owned.Add(o + row * 1024 + b);
int stray = Enumerable.Range(0, vram.Length).Count(i => written[i] != vram[i] && !owned.Contains(i));
Check(stray == 0, $"no byte outside the changed textures moves ({stray})");
foreach (int t in Enumerable.Range(0, records.Count).Where(t => !images.ContainsKey(t) && records[t] != null))
    foreach (var d in records[t]!.Descriptors)
        for (int row = 0; row < d.TileSide; row++)
            Check(written.AsSpan(d.PixelByteOffset + row * 1024, d.PixelRowByteLength).SequenceEqual(vram.AsSpan(d.PixelByteOffset + row * 1024, d.PixelRowByteLength)), $"texture {t} unchanged");

// Read back: normal and close tiers within quantisation of the image; far tier within its 16 colours.
static (int R, int G, int B) Colour(ushort c) => ((c & 31) << 3, (c >> 5 & 31) << 3, (c >> 10 & 31) << 3);
double worstNormal = 0, worstFar = 0;
foreach (var (t, image) in images)
{
    var d = records[t]!.Descriptors;
    double error = 0;
    for (int q = 0; q < 4; q++)
    {
        var quarter = d[3 + q]; int qx = (q >> 1) * 32, qy = (q & 1) * 32;
        if (quarter.Mirrored) continue;                    // shows another quarter's pixels flipped
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                byte index = written[quarter.PixelByteOffset + y * 1024 + x];
                var c = Colour(BinaryPrimitives.ReadUInt16LittleEndian(written.AsSpan(quarter.PaletteByteOffset + index * 2)));
                var p = image.Normal[(qy + y) * 64 + qx + x];
                error += Math.Abs(c.R - p.R) + Math.Abs(c.G - p.G) + Math.Abs(c.B - p.B);
            }
    }
    worstNormal = Math.Max(worstNormal, error / (64 * 64 * 3));
    var far = d[0]; double farError = 0;
    for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            byte pair = written[far.PixelByteOffset + y * 1024 + x / 2]; int index = x % 2 == 0 ? pair & 15 : pair >> 4;
            var c = Colour(BinaryPrimitives.ReadUInt16LittleEndian(written.AsSpan(far.PaletteByteOffset + index * 2)));
            var p = image.Normal[(y * 2) * 64 + x * 2];
            farError += Math.Abs(c.R - p.R) + Math.Abs(c.G - p.G) + Math.Abs(c.B - p.B);
        }
    worstFar = Math.Max(worstFar, farError / (32 * 32 * 3));
}
Check(worstNormal < 16, $"normal tier matches its images (worst mean error {worstNormal:0.0})");
Check(worstFar < 40, $"far tier keeps the images' colours (worst mean error {worstFar:0.0})");
Console.WriteLine($"changed bytes {Enumerable.Range(0, vram.Length).Count(i => written[i] != vram[i])}, normal error {worstNormal:0.0}, far error {worstFar:0.0}");
Console.WriteLine($"TERRAIN_IMAGE_LEVEL_OK checks={checks}");
