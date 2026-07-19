using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

const int SectorOffset = 0x100;
const int HeaderBytes = 28;
const int HpVertexCount = 1;
const int HpColorCount = 4;
const int HpFaceCount = 1;
const int HpFaceBytes = 16;

int hpColorStart = SectorOffset + HeaderBytes + (HpVertexCount * 4);
int hpFaceOffset = hpColorStart + NativeTerrainHpColorLayout.TotalByteLength(HpColorCount);
int sectorSize = HeaderBytes + (HpVertexCount * 4) +
                 NativeTerrainHpColorLayout.TotalByteLength(HpColorCount) +
                 (HpFaceCount * HpFaceBytes);
byte[] logicalWad = new byte[SectorOffset + sectorSize];

logicalWad[SectorOffset + 20] = HpVertexCount;
logicalWad[SectorOffset + 21] = HpColorCount;
logicalWad[SectorOffset + 22] = HpFaceCount;

ColorRgba[] table1 =
[
    ColorRgba.FromRgb(11, 12, 13),
    ColorRgba.FromRgb(21, 22, 23),
    ColorRgba.FromRgb(31, 32, 33),
    ColorRgba.FromRgb(41, 42, 43)
];
ColorRgba[] table2 =
[
    ColorRgba.FromRgb(111, 112, 113),
    ColorRgba.FromRgb(121, 122, 123),
    ColorRgba.FromRgb(131, 132, 133),
    ColorRgba.FromRgb(141, 142, 143)
];
for (int index = 0; index < HpColorCount; index++)
{
    NativeTerrainHpColorOffsets offsets =
        NativeTerrainHpColorLayout.GetColorOffsets(hpColorStart, HpColorCount, index);
    WriteColor(logicalWad, offsets.Table1Offset, table1[index]);
    WriteColor(logicalWad, offsets.Table2Offset, table2[index]);
}

byte[] faceColorIndexes = [3, 1, 2, 0];
faceColorIndexes.CopyTo(logicalWad, hpFaceOffset + 4);
BinaryPrimitives.WriteUInt32LittleEndian(logicalWad.AsSpan(hpFaceOffset + 8, 4), 36);

NativeTerrainHpColorOffsets finalOffsets =
    NativeTerrainHpColorLayout.GetColorOffsets(hpColorStart, HpColorCount, 1);
Assert(finalOffsets.Table1Offset == hpColorStart + 4,
    "Table 1 did not use a four-byte indexed stride.");
Assert(finalOffsets.Table2Offset == hpColorStart + 20,
    "Table 2 did not begin after the complete table-1 bank.");
Assert(finalOffsets.Table1Offset != hpColorStart + (1 * 8) &&
       finalOffsets.Table2Offset != hpColorStart + (1 * 8) + 4,
    "The split-bank fixture accidentally matched the rejected interleaved layout.");

Assert(
    NativeTerrainTextureVisualInspector.TryInspectLogicalWad(
        logicalWad,
        "fixture",
        "hp-s0-f0",
        SectorOffset,
        hpFaceOffset,
        out TerrainTextureVisualEdit visual,
        out string error),
    $"The split-bank source fixture was rejected: {error}");
Assert(visual.SourceTextureId == 36, "The fixture texture id changed.");

ColorRgba[] expectedTable1 = faceColorIndexes.Select(index => table1[index]).ToArray();
ColorRgba[] expectedTable2 = faceColorIndexes.Select(index => table2[index]).ToArray();
Assert(visual.Corners.Select(corner => corner.FarColor).SequenceEqual(expectedTable1),
    "The legacy FarColor fields did not read physical HP table 1.");
Assert(visual.Corners.Select(corner => corner.NearColor).SequenceEqual(expectedTable2),
    "The legacy NearColor fields did not read physical HP table 2.");
Assert(SourceSceneOverlayContract.CurrentFormatVersion >= 9 &&
       SourceSceneOverlayContract.HpColorLayout == "split-contiguous-4-byte-table1-table2-v1",
    "The source-overlay cache contract does not invalidate the former interleaved decode.");

int retailFixtureArg = Array.FindIndex(
    args,
    arg => arg.Equals("--retail-artisans", StringComparison.OrdinalIgnoreCase));
if (retailFixtureArg >= 0)
{
    Assert(retailFixtureArg + 2 < args.Length,
        "--retail-artisans requires the workspace root and USA retail BIN path.");
    RunRetailArtisansFixture(args[retailFixtureArg + 1], args[retailFixtureArg + 2]);
}

Console.WriteLine(
    "Native HP split color banks: PASS table1[4-byte index], table2[bank + 4-byte index], raw face-slot order, and cache-contract invalidation.");

static void RunRetailArtisansFixture(string workspaceRoot, string sourceImagePath)
{
    string overlayPath = Path.Combine(
        Path.GetFullPath(workspaceRoot),
        "editor-cache",
        "artisans-runtime-scene-editor-overlay.json");
    Assert(File.Exists(sourceImagePath), $"Missing USA retail BIN: {sourceImagePath}");
    Assert(File.Exists(overlayPath), $"Missing rebuilt Artisans overlay: {overlayPath}");

    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(overlayPath));
    JsonElement face = document.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("polygons")
        .EnumerateArray()
        .Single(polygon =>
            polygon.GetProperty("sectorIndex").GetInt32() == 38 &&
            polygon.GetProperty("faceIndex").GetInt32() == 36);
    Assert(face.GetProperty("textureId").GetInt32() == 27,
        "The Artisans 38:36 retail fixture no longer uses texture 27.");

    int sectorOffset = ParseHex(face.GetProperty("sectorOffset").GetString());
    int faceOffset = ParseHex(face.GetProperty("sourceWadOffset").GetString());
    int[] colorIndexes = face.GetProperty("colourIndexes")
        .EnumerateArray()
        .Select(value => value.GetInt32())
        .ToArray();
    Assert(colorIndexes.SequenceEqual(new[] { 45, 45, 44, 43 }),
        "The Artisans 38:36 raw color-index fixture changed.");

    byte[] header = ReadLogicalWadBytes(sourceImagePath, sectorOffset, HeaderBytes);
    int colorStart = sectorOffset + HeaderBytes +
                     ((header[16] + header[17] + (header[18] * 2) + header[20]) * 4);
    int colorCount = header[21];
    Assert(ReadLogicalWadBytes(sourceImagePath, faceOffset + 4, 4).SequenceEqual(colorIndexes.Select(index => (byte)index)),
        "The rebuilt Artisans overlay color indexes do not match the retail face bytes.");

    ColorRgba[] sourceTable1 = colorIndexes
        .Select(index => ReadRetailColor(
            sourceImagePath,
            NativeTerrainHpColorLayout.GetColorOffsets(colorStart, colorCount, index).Table1Offset))
        .ToArray();
    ColorRgba[] sourceTable2 = colorIndexes
        .Select(index => ReadRetailColor(
            sourceImagePath,
            NativeTerrainHpColorLayout.GetColorOffsets(colorStart, colorCount, index).Table2Offset))
        .ToArray();
    ColorRgba[] expectedTable1 =
    [
        ColorRgba.FromRgb(47, 178, 242),
        ColorRgba.FromRgb(47, 178, 242),
        ColorRgba.FromRgb(47, 178, 242),
        ColorRgba.FromRgb(47, 178, 242)
    ];
    ColorRgba[] expectedTable2 =
    [
        ColorRgba.FromRgb(68, 180, 255),
        ColorRgba.FromRgb(68, 180, 255),
        ColorRgba.FromRgb(67, 182, 255),
        ColorRgba.FromRgb(57, 179, 255)
    ];
    Assert(sourceTable1.SequenceEqual(expectedTable1) && sourceTable2.SequenceEqual(expectedTable2),
        "The USA retail Artisans 38:36 split-bank source colors changed.");

    ColorRgba[] overlayTable1 = ReadOverlayColors(face, "farColors");
    ColorRgba[] overlayTable2 = ReadOverlayColors(face, "nearColors");
    Assert(overlayTable1.SequenceEqual(sourceTable1) && overlayTable2.SequenceEqual(sourceTable2),
        "The rebuilt Artisans 38:36 overlay does not match its direct retail split-bank addresses.");

    JsonElement brownWallFace = document.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("polygons")
        .EnumerateArray()
        .Single(polygon =>
            polygon.GetProperty("sectorIndex").GetInt32() == 38 &&
            polygon.GetProperty("faceIndex").GetInt32() == 74);
    Assert(brownWallFace.GetProperty("textureId").GetInt32() == 36,
        "The Artisans 38:74 brown-wall fixture no longer uses texture 36.");
    int brownWallSectorOffset = ParseHex(brownWallFace.GetProperty("sectorOffset").GetString());
    int[] brownWallColorIndexes = brownWallFace.GetProperty("colourIndexes")
        .EnumerateArray()
        .Select(value => value.GetInt32())
        .ToArray();
    Assert(brownWallColorIndexes.SequenceEqual(new[] { 100, 96, 97, 101 }),
        "The Artisans 38:74 brown-wall color-index fixture changed.");
    byte[] brownWallHeader = ReadLogicalWadBytes(sourceImagePath, brownWallSectorOffset, HeaderBytes);
    int brownWallColorStart = brownWallSectorOffset + HeaderBytes +
                              ((brownWallHeader[16] + brownWallHeader[17] +
                                (brownWallHeader[18] * 2) + brownWallHeader[20]) * 4);
    int brownWallColorCount = brownWallHeader[21];
    ColorRgba[] brownWallTable1 = brownWallColorIndexes
        .Select(index => ReadRetailColor(
            sourceImagePath,
            NativeTerrainHpColorLayout.GetColorOffsets(
                brownWallColorStart,
                brownWallColorCount,
                index).Table1Offset))
        .ToArray();
    ColorRgba[] brownWallTable2 = brownWallColorIndexes
        .Select(index => ReadRetailColor(
            sourceImagePath,
            NativeTerrainHpColorLayout.GetColorOffsets(
                brownWallColorStart,
                brownWallColorCount,
                index).Table2Offset))
        .ToArray();
    Assert(brownWallTable1.SequenceEqual(new[]
    {
        ColorRgba.FromRgb(221, 186, 161),
        ColorRgba.FromRgb(137, 105, 100),
        ColorRgba.FromRgb(154, 122, 114),
        ColorRgba.FromRgb(228, 192, 167)
    }), "The USA retail Artisans 38:74 table-1 wall colors changed.");
    Assert(brownWallTable2.SequenceEqual(new[]
    {
        ColorRgba.FromRgb(110, 69, 28),
        ColorRgba.FromRgb(68, 39, 18),
        ColorRgba.FromRgb(77, 44, 19),
        ColorRgba.FromRgb(115, 70, 28)
    }), "The USA retail Artisans 38:74 table-2 wall colors changed.");
    Assert(ReadOverlayColors(brownWallFace, "farColors").SequenceEqual(brownWallTable1) &&
           ReadOverlayColors(brownWallFace, "nearColors").SequenceEqual(brownWallTable2),
        "The rebuilt Artisans 38:74 brown wall does not match its direct retail split-bank addresses.");
    Console.WriteLine(
        "USA Artisans fixtures: PASS cyan hp-s38-f36 and brown hp-s38-f74 exact direct-source table1/table2 colors.");
}

static ColorRgba[] ReadOverlayColors(JsonElement face, string propertyName) =>
    face.GetProperty(propertyName)
        .EnumerateArray()
        .Select(value => ColorRgba.FromRgb(
            value.GetProperty("r").GetInt32(),
            value.GetProperty("g").GetInt32(),
            value.GetProperty("b").GetInt32()))
        .ToArray();

static ColorRgba ReadRetailColor(string sourceImagePath, int wadOffset)
{
    byte[] rgb = ReadLogicalWadBytes(sourceImagePath, wadOffset, 3);
    return ColorRgba.FromRgb(rgb[0], rgb[1], rgb[2]);
}

static byte[] ReadLogicalWadBytes(string sourceImagePath, int wadOffset, int byteCount)
{
    const int WadLba = 37;
    long imageLength = new FileInfo(sourceImagePath).Length;
    int sectorBytes = imageLength % 2352 == 0 ? 2352 : 2048;
    int userDataOffset = sectorBytes == 2352 ? 24 : 0;
    byte[] result = new byte[byteCount];
    using FileStream image = File.OpenRead(sourceImagePath);
    int copied = 0;
    while (copied < byteCount)
    {
        int logicalOffset = checked(wadOffset + copied);
        int logicalSector = logicalOffset / 2048;
        int sectorOffset = logicalOffset % 2048;
        int count = Math.Min(byteCount - copied, 2048 - sectorOffset);
        image.Position = ((long)(WadLba + logicalSector) * sectorBytes) + userDataOffset + sectorOffset;
        image.ReadExactly(result.AsSpan(copied, count));
        copied += count;
    }
    return result;
}

static int ParseHex(string? value)
{
    Assert(!string.IsNullOrWhiteSpace(value), "Missing hexadecimal source offset.");
    return Convert.ToInt32(value![2..], 16);
}

static void WriteColor(byte[] bytes, int offset, ColorRgba color)
{
    bytes[offset] = color.R;
    bytes[offset + 1] = color.G;
    bytes[offset + 2] = color.B;
    bytes[offset + 3] = 0;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
