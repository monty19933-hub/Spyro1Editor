using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Rendering;

AssertRgb5Packing();
AssertBoundedTextureModulation();
string ditherSha256 = AssertPsxGpuDithering();
AssertNativeTerrainOrderingFormulaFixtures();
NativeTerrainHqIntegerFixtureReport nativeHqInteger = AssertNativeTerrainHqIntegerContract();
NativeTerrainPhaseFixtureReport nativePhase = AssertNativeTerrainCoarsePhaseFixture();
NativeTerrainOrderingInventoryReport nativeOrderingInventory = AssertNativeTerrainOrderingInventory();
AssertAbrBoundaryFixtures();
AssertExhaustiveChannelArithmetic();
AssertTexturedPixelBoundaryFixtures();
AssertExhaustiveTextureWordClassification();
AssertExhaustiveHighPolyPrimitiveClassification();
AssertInvalidInputsFailClosed();
BoundedRasterFixtureReport boundedRaster = AssertBoundedRasterFixtures();
ProductionPaintReport productionPaint = AssertProductionPaintMode();

string arithmeticSha256 = ComputeArithmeticFingerprint();
string pixelClassificationSha256 = ComputePixelClassificationFingerprint();
string modulationSha256 = ComputeTextureModulationFingerprint();
Assert(arithmeticSha256 == "1A6DFB75D274617D174F911DDDB8BA507AD86BCDBCB3F81ACBCF82A339E60E6E",
    $"ABR arithmetic fingerprint changed: {arithmeticSha256}.");
Assert(pixelClassificationSha256 == "D053005A903FBAF4784AAF4433D510351D804E088430FCE50B75AE76FE9F3F3E",
    $"Texture/STP pixel classification fingerprint changed: {pixelClassificationSha256}.");
Assert(modulationSha256 == "D6DD94C746ADB4B46F3D1F102AACC09707944A852A66603A1967DD9670BF5C79",
    $"Bounded texture modulation fingerprint changed: {modulationSha256}.");
Assert(ditherSha256 == "10F813AF291E47312E9E64A681E905D2230B8D7A250657005E858FA64F32C989",
    $"Native GPU dither fingerprint changed: {ditherSha256}.");
Assert(boundedRaster.Sha256 == "8D0ACA0E4F6D9414F7666546CEB5C6D2DF5DD2BC64ECE42AAF94CFB8FE4A6AA2",
    $"Bounded RGB5 raster fingerprint changed: {boundedRaster.Sha256}.");
Console.WriteLine("PSX terrain blend smoke passed.");
Console.WriteLine("- RGB5 pack/unpack: 32,768 words");
Console.WriteLine("- ABR channel arithmetic: 4,096 exhaustive combinations plus boundaries");
Console.WriteLine("- Texture/STP classification: 131,072 exhaustive raw-word/primitive combinations");
Console.WriteLine("- HP material-byte classification: all 256 values, including opaque 0xFF sentinel");
Console.WriteLine($"- Native terrain OT: {nativeOrderingInventory.LevelCount} levels, " +
                  $"{nativeOrderingInventory.HighPolyFaceCount:N0} HP / {nativeOrderingInventory.LowPolyFaceCount:N0} LP faces, " +
                  $"{nativeOrderingInventory.HighPolyRendererFlagAliasCount:N0} HP flag-alias guards");
Console.WriteLine($"- Native coarse phase order: {nativePhase.CommandCount} tied commands, " +
                  $"frame {nativePhase.FrameSha256}");
Console.WriteLine($"- Native HQ integer contract: {nativeHqInteger.SelectionFixtureCount} tier selections, " +
                  $"{nativeHqInteger.BaseTileCount} base tiles, tables {nativeHqInteger.TopologyTableSha256}");
Console.WriteLine($"- Native HQ flag inventory: bit6={nativeOrderingInventory.HighPolyTierControlBit6Count:N0}, " +
                  $"bit7={nativeOrderingInventory.HighPolyHqDisableBit7Count:N0}");
Console.WriteLine($"- Native OT raw-field SHA-256: {nativeOrderingInventory.RawFieldSha256}");
Console.WriteLine($"- Arithmetic SHA-256: {arithmeticSha256}");
Console.WriteLine($"- Pixel classification SHA-256: {pixelClassificationSha256}");
Console.WriteLine($"- Bounded modulation SHA-256: {modulationSha256}");
Console.WriteLine($"- Native GPU dither SHA-256: {ditherSha256}");
Console.WriteLine($"- Bounded raster fixtures: {boundedRaster.FixtureCount} fixtures, " +
                  $"{boundedRaster.Commands} commands, {boundedRaster.CoveredSamples} covered samples, " +
                  $"{boundedRaster.FramebufferWrites} framebuffer writes, " +
                  $"{boundedRaster.TransparentTexelSkips} transparent texel skips");
Console.WriteLine($"- Bounded raster write split: {boundedRaster.OpaqueWrites} opaque, " +
                  $"{boundedRaster.SemiTransparentWrites} semitransparent " +
                  $"(ABR0/1/2/3 = {boundedRaster.Abr0Writes}/{boundedRaster.Abr1Writes}/" +
                  $"{boundedRaster.Abr2Writes}/{boundedRaster.Abr3Writes})");
Console.WriteLine($"- Bounded raster SHA-256: {boundedRaster.Sha256}");
Console.WriteLine($"- Production Paint: {productionPaint.CommandCount} commands, " +
                  $"{productionPaint.ProductionAllocatedBytes:N0} B without painter capture vs " +
                  $"{productionPaint.CapturedAllocatedBytes:N0} B with capture " +
                  $"({productionPaint.SavedAllocatedBytes:N0} B saved), " +
                  $"{productionPaint.ProductionMilliseconds:0.00}/{productionPaint.CapturedMilliseconds:0.00} ms, " +
                  $"frame {productionPaint.FrameSha256}");

static void AssertRgb5Packing()
{
    for (int packed = 0; packed <= 0x7FFF; packed++)
    {
        PsxRgb5 color = PsxRgb5.FromPackedWord((ushort)packed);
        Assert(color.PackedWord == packed, $"RGB5 pack/unpack changed 0x{packed:X4}.");
    }

    Psx555TextureWord stpWhite = new(0xFFFF);
    Assert(stpWhite.RawWord == 0xFFFF && stpWhite.HasSemiTransparencyBit &&
           stpWhite.Color.PackedWord == 0x7FFF,
        "Raw PSX555 word or STP state was not preserved independently from RGB5.");
}

static void AssertBoundedTextureModulation()
{
    for (int texture = 0; texture <= 31; texture++)
    {
        for (int shade = 0; shade <= byte.MaxValue; shade++)
        {
            int expected = Math.Min(31, (texture * shade) >> 7);
            byte actual = PsxTerrainBoundedRasterizer.ModulateTextureChannel5(texture, shade);
            Assert(actual == expected,
                $"Bounded modulation texel={texture}, shade={shade} produced {actual}, expected {expected}.");
        }
    }

    PsxRgb5 source = new(31, 17, 1);
    Assert(PsxTerrainBoundedRasterizer.ModulateTextureColor(source, PsxRgb8.NeutralModulation) == source,
        "RGB8 0x80 modulation must be exactly neutral in all RGB5 channels.");
    Assert(PsxTerrainBoundedRasterizer.ModulateTextureColor(source, new PsxRgb8(0, 64, 255)) ==
           new PsxRgb5(0, 8, 1),
        "Bounded vector modulation did not shade, truncate, and clamp channels independently.");
}

static string AssertPsxGpuDithering()
{
    int[,] expectedMatrix =
    {
        { -4,  0, -3,  1 },
        {  2, -2,  3, -1 },
        { -3,  1, -4,  0 },
        {  3, -1,  2, -2 }
    };
    byte[] fingerprint = new byte[(16 * 512) + (16 * 32 * 256)];
    int cursor = 0;
    for (int y = 0; y < 4; y++)
    {
        for (int x = 0; x < 4; x++)
        {
            int offset = expectedMatrix[y, x];
            Assert(PsxGpuDither.OffsetAt(x, y) == offset &&
                   PsxGpuDither.OffsetAt(x + 4, y + 4) == offset,
                $"Native GPU dither matrix/period changed at ({x},{y}).");

            for (int value = 0; value <= PsxGpuDither.MaximumPreQuantizedChannel; value++)
            {
                byte expected = (byte)Math.Clamp((value + offset) >> 3, 0, 31);
                byte actual = PsxGpuDither.QuantizeChannel(value, x, y);
                Assert(actual == expected,
                    $"Native GPU dither value {value} at ({x},{y}) produced {actual}, expected {expected}.");
                fingerprint[cursor++] = actual;
            }

            for (int texture = 0; texture <= 31; texture++)
            {
                for (int shade = 0; shade <= byte.MaxValue; shade++)
                {
                    int preQuantized = (texture * shade) >> 4;
                    byte expected = (byte)Math.Clamp((preQuantized + offset) >> 3, 0, 31);
                    byte actual = PsxGpuDither.ModulateTextureChannel(texture, shade, x, y);
                    Assert(actual == expected,
                        $"Dithered texture modulation texel={texture}, shade={shade}, ({x},{y}) produced {actual}, expected {expected}.");
                    fingerprint[cursor++] = actual;
                }
            }
        }
    }
    Assert(cursor == fingerprint.Length, "Native GPU dither fingerprint payload was not filled exactly.");

    Assert(PsxGpuDither.QuantizeColor(new PsxRgb8(0, 128, 255), 0, 0) == new PsxRgb5(0, 15, 31),
        "Untextured RGB8 dither vector changed at matrix origin (-4).");
    Assert(PsxGpuDither.ModulateTextureColor(new PsxRgb5(31, 17, 1), PsxRgb8.NeutralModulation, 3, 2) ==
           new PsxRgb5(31, 17, 1),
        "Neutral textured modulation changed at the zero-offset matrix cell.");
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.QuantizeChannel(-1, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.QuantizeChannel(512, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.ModulateTextureChannel(-1, 0, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.ModulateTextureChannel(32, 0, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.ModulateTextureChannel(0, -1, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => PsxGpuDither.ModulateTextureChannel(0, 256, 0, 0));

    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    AppendString(hash, PsxGpuDither.Contract);
    hash.AppendData(fingerprint);
    return Convert.ToHexString(hash.GetHashAndReset());
}

static void AssertNativeTerrainOrderingFormulaFixtures()
{
    double[] hp512 = [512, 512, 512, 512];
    Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               hp512,
               nativeFaceWord3: 0,
               out int hpBase,
               out int hpRawSum) &&
           hpRawSum == 0x2000 && hpBase == 0x40,
        "Native HP planar depth 512 did not quantize to raw sum 0x2000 / bucket 0x40.");
    Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               hp512,
               nativeFaceWord3: 0x80,
               out int hpFlagAlias,
               out _) &&
           hpFlagAlias == hpBase,
        "HP renderer flag bit 7 leaked into the native OT bias.");
    Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               hp512,
               nativeFaceWord3: 0x38,
               out int hpMaximumBias,
               out _) &&
           hpMaximumBias == hpBase + 28,
        "HP word-3 bits 3..5 did not contribute their exact maximum OT bias.");
    Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               [511, 511, 511, 511],
               nativeFaceWord3: 0,
               out int hp511,
               out _) &&
           hp511 == 63,
        "Native HP bucket quantization changed at the 511/512 boundary.");

    double[] lp1792 = [1792, 1792, 1792, 1792];
    Assert(NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
               lp1792,
               rawWord1: 0,
               out int lpBase,
               out int lpRawSum) &&
           lpRawSum == 0x1C00 && lpBase == 288,
        "Native LP planar depth 1792 did not include its +0x40 base bucket offset.");
    Assert(NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
               lp1792,
               rawWord1: 0xF8,
               out int lpMaximumBias,
               out _) &&
           lpMaximumBias == lpBase + 248,
        "LP word-1 bits 3..7 did not contribute their exact maximum OT bias.");

    Assert(!NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               [512, 512, 512], 0, out _, out _),
        "Native HP OT accepted fewer than four raw face slots.");
    Assert(!NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
               [1792, double.NaN, 1792, 1792], 0, out _, out _),
        "Native LP OT accepted a non-finite depth.");
    Assert(!NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               [0, 1, 1, 1], 0, out _, out _),
        "Native HP OT accepted a nonpositive GTE depth.");
    Assert(!NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
               [20000, 20000, 20000, 20000], 0, out _, out _),
        "Native HP OT silently accepted a depth beyond the 16-bit GTE SZ domain.");
    Assert(!NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
               [16383, 16383, 16383, 16383], 0xF8, out _, out _),
        "Native LP OT silently emitted a bucket beyond the retail 0x800-entry world OT.");
}

static NativeTerrainHqIntegerFixtureReport AssertNativeTerrainHqIntegerContract()
{
    int selectionFixtureCount = 0;

    NativeTerrainHqQueueSelection Select(
        NativeTerrainHqFaceKind faceKind,
        IReadOnlyList<int> depths,
        uint word3 = 0,
        int lodDistance = NativeTerrainHqQueuedRenderContract.DefaultHighPolyLodDepthSum)
    {
        selectionFixtureCount++;
        return NativeTerrainHqQueuedRenderContract.SelectQueuedTier(
            faceKind,
            depths,
            word3,
            lodDistance);
    }

    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x140, 0x140, 0x140, 0x140]).Tier ==
           NativeTerrainHqQueueTier.Normal,
        "A quad exactly on the 0x140 source-SZ boundary did not select normal HQ.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x140, 0x140, 0x140, 0x140], 0x40).Tier ==
           NativeTerrainHqQueueTier.None,
        "Word-3 bit 6 must bypass HQ in the normal-depth band.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x13F, 0x140, 0x140, 0x140]).Tier ==
           NativeTerrainHqQueueTier.Close,
        "A quad below the 0x140 source-SZ boundary did not select close HQ.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x13F, 0x140, 0x140, 0x140], 0x40).Tier ==
           NativeTerrainHqQueueTier.Normal,
        "Word-3 bit 6 must route a close-depth face through normal HQ.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x13F, 0x140, 0x140, 0x140], 0x80).Tier ==
           NativeTerrainHqQueueTier.None,
        "Word-3 bit 7 must bypass HQ at close depth.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0, 0, 0, 0]).Tier ==
           NativeTerrainHqQueueTier.None,
        "The native zero-weight close candidate must not enter an HQ queue.");

    NativeTerrainHqQueueSelection quad1fff = Select(
        NativeTerrainHqFaceKind.Quad,
        [0x7FF, 0x7FF, 0x7FF, 0x802]);
    NativeTerrainHqQueueSelection quad2000 = Select(
        NativeTerrainHqFaceKind.Quad,
        [0x7FF, 0x7FF, 0x7FF, 0x803]);
    Assert(quad1fff.WeightedDepthSum == 0x1FFF && quad1fff.Tier == NativeTerrainHqQueueTier.Normal &&
           quad2000.WeightedDepthSum == 0x2000 && quad2000.Tier == NativeTerrainHqQueueTier.None,
        "Quad HQ selection changed at the strict 0x2000 weighted-depth boundary.");

    NativeTerrainHqQueueSelection triangle1fff = Select(
        NativeTerrainHqFaceKind.Triangle,
        [0x3FF, 0x400, 0xC00]);
    NativeTerrainHqQueueSelection triangle2000 = Select(
        NativeTerrainHqFaceKind.Triangle,
        [0x400, 0x400, 0xC00]);
    Assert(triangle1fff.WeightedDepthSum == 0x1FFF && triangle1fff.Tier == NativeTerrainHqQueueTier.Normal &&
           triangle2000.WeightedDepthSum == 0x2000 && triangle2000.Tier == NativeTerrainHqQueueTier.None,
        "Triangle HQ selection did not duplicate its third SZ at the 0x2000 boundary.");
    Assert(Select(NativeTerrainHqFaceKind.Triangle, [0x140, 0x140, 0x13F]).Tier ==
           NativeTerrainHqQueueTier.Close,
        "Triangle close selection did not test all three unique source depths.");
    Assert(Select(NativeTerrainHqFaceKind.Triangle, [0x140, 0x140, 0x13F], 0x40).Tier ==
           NativeTerrainHqQueueTier.Normal,
        "Triangle word-3 bit 6 did not suppress only the close tier.");
    Assert(Select(NativeTerrainHqFaceKind.Quad, [0x140, 0x140, 0x140, 0x140], lodDistance: 0x500).Tier ==
           NativeTerrainHqQueueTier.None &&
           Select(NativeTerrainHqFaceKind.Quad, [0x140, 0x140, 0x140, 0x140], lodDistance: 0x501).Tier ==
           NativeTerrainHqQueueTier.Normal,
        "HQ selection did not retain the strict high-poly LOD-distance boundary.");

    AssertThrows<ArgumentException>(() => NativeTerrainHqQueuedRenderContract.SelectQueuedTier(
        NativeTerrainHqFaceKind.Quad, [1, 2, 3], 0));
    AssertThrows<ArgumentOutOfRangeException>(() => NativeTerrainHqQueuedRenderContract.SelectQueuedTier(
        NativeTerrainHqFaceKind.Triangle, [1, 2, 0x10000], 0));
    AssertThrows<ArgumentOutOfRangeException>(() => NativeTerrainHqQueuedRenderContract.SelectQueuedTier(
        (NativeTerrainHqFaceKind)99, [1, 2, 3], 0));
    AssertThrows<ArgumentOutOfRangeException>(() => NativeTerrainHqQueuedRenderContract.SelectQueuedTier(
        NativeTerrainHqFaceKind.Triangle, [1, 2, 3], 0, highPolyLodDepthSum: 0));

    Assert(NativeTerrainHqQueuedRenderContract.SignedCoordinateMidpoint(1, 2) == 1 &&
           NativeTerrainHqQueuedRenderContract.SignedCoordinateMidpoint(-1, 0) == -1 &&
           NativeTerrainHqQueuedRenderContract.SignedCoordinateMidpoint(-2, -1) == -2 &&
           NativeTerrainHqQueuedRenderContract.SignedCoordinateMidpoint(short.MinValue, short.MaxValue) == -1,
        "Signed HQ midpoint no longer matches MIPS arithmetic-shift truncation.");
    Assert(NativeTerrainHqQueuedRenderContract.PackedColorMidpoint(0x34010203, 0x34040506) == 0x34020304 &&
           NativeTerrainHqQueuedRenderContract.PackedColorMidpoint(0x34FFFFFF, 0x34000000) == 0x347F7F7F &&
           NativeTerrainHqQueuedRenderContract.PackedColorMidpoint(0x00000000, 0xFFFFFFFF) == 0x7FFF7F7F,
        "Packed HQ color midpoint changed its 0xFFFEFEFF mask/logical-shift behavior.");

    Assert(NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(0x100, 0x101, 0x102, 0x38) == 36 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0x100, 0x101, 0x102, 0x103, 0x38) == 36,
        "Native GT3/GT4 tile-local OT equations changed on the locked mixed-depth fixture.");
    Assert(NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(0, 0, 0x3F, 0) == 0 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(0, 0, 0x40, 0) == 1 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0, 0, 0, 0x7F, 0) == 0 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0, 0, 0, 0x80, 0) == 1,
        "Native tile-local OT right-shift changed at the 0x7F/0x80 sum boundary.");
    Assert(NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(0xFFF, 0xFFF, 0xFFF, 0xF8) == 155 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0xFFF, 0xFFF, 0xFFF, 0xFFF, 0xF8) == 155 &&
           NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0x100, 0x101, 0x102, 0x103, 0xF8) == 36,
        "Native tile-local OT bias leaked renderer flag bits 6/7 or changed its 12-bit SZ maximum.");
    AssertThrows<ArgumentOutOfRangeException>(() =>
        NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(-1, 0, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(0, 0, 0, 0x1000, 0));

    Assert(NativeTerrainHqQueuedRenderContract.NormalQuadTopology
               .Select(tile => (tile.Vertex0, tile.Vertex1, tile.Vertex2, tile.Vertex3))
               .SequenceEqual(new[] { (0, 1, 3, 4), (1, 2, 4, 5), (3, 4, 6, 7), (4, 5, 7, 8) }),
        "Normal-quad 3x3/2x2 base topology changed.");
    Assert(NativeTerrainHqQueuedRenderContract.NormalTriangleTopology
               .Select(tile => (tile.Vertex0, tile.Vertex1, tile.Vertex2))
               .SequenceEqual(new[] { (0, 1, 3), (4, 3, 1), (1, 2, 4), (3, 4, 5) }),
        "Normal-triangle order-2 base topology changed.");
    Assert(NativeTerrainHqQueuedRenderContract.CloseTriangleTopology
               .Select(tile => (tile.Vertex0, tile.Vertex1, tile.Vertex2))
               .SequenceEqual(new[]
               {
                   (0, 1, 5), (6, 5, 1), (1, 2, 6), (7, 6, 2),
                   (2, 3, 7), (8, 7, 3), (3, 4, 8), (5, 6, 9),
                   (10, 9, 6), (6, 7, 10), (11, 10, 7), (7, 8, 11),
                   (9, 10, 12), (13, 12, 10), (10, 11, 13), (12, 13, 14)
               }),
        "Close-triangle order-4 base topology changed.");
    Assert(NativeTerrainHqQueuedRenderContract.CloseQuadTopology.Count == 16 &&
           NativeTerrainHqQueuedRenderContract.CloseQuadTopology.Select((tile, index) =>
               tile.Vertex0 == (index / 4) * 5 + index % 4 &&
               tile.Vertex1 == tile.Vertex0 + 1 &&
               tile.Vertex2 == tile.Vertex0 + 5 &&
               tile.Vertex3 == tile.Vertex0 + 6).All(valid => valid),
        "Close-quad 5x5/4x4 base topology changed.");

    using IncrementalHash topologyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    AppendRetailWords(topologyHash, NativeTerrainHqQueuedRenderContract.NormalQuadOriginWords);
    AppendRetailWords(topologyHash, NativeTerrainHqQueuedRenderContract.CloseQuadOriginWords);
    AppendRetailWords(topologyHash, NativeTerrainHqQueuedRenderContract.NormalTriangleDescriptorWords);
    AppendRetailWords(topologyHash, NativeTerrainHqQueuedRenderContract.CloseTriangleDescriptorWords);
    string topologyTableSha256 = Convert.ToHexString(topologyHash.GetHashAndReset());
    Assert(topologyTableSha256 == "B588456351AA40CAE5D86CB88F905C05C92BB5F7CF4358454C11722F2D001AA9",
        $"Native HQ retail base-topology table fingerprint changed: {topologyTableSha256}.");

    int baseTileCount = 0;
    foreach ((NativeTerrainHqQueueTier Tier, NativeTerrainHqFaceKind FaceKind, int PointCount, int TileCount) fixture in
             new[]
             {
                 (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Quad, 9, 4),
                 (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Triangle, 6, 4),
                 (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Quad, 25, 16),
                 (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Triangle, 15, 16)
             })
    {
        int[] generatedDepths = Enumerable.Range(0, fixture.PointCount).Select(index => 0x100 + index).ToArray();
        IReadOnlyList<NativeTerrainHqBaseTileBucket> buckets =
            NativeTerrainHqQueuedRenderContract.ComputeBaseTileBuckets(
                fixture.Tier,
                fixture.FaceKind,
                generatedDepths,
                0x38);
        Assert(buckets.Count == fixture.TileCount &&
               buckets.Select(entry => entry.Topology.TileIndex).SequenceEqual(Enumerable.Range(0, fixture.TileCount)),
            $"{fixture.Tier} {fixture.FaceKind} base tiles lost retail table/FIFO order.");
        foreach (NativeTerrainHqBaseTileBucket entry in buckets)
        {
            NativeTerrainHqTileTopology tile = entry.Topology;
            int direct = tile.Primitive == NativeTerrainHqTilePrimitive.Gt3
                ? NativeTerrainHqQueuedRenderContract.ComputeGt3Bucket(
                    generatedDepths[tile.Vertex0], generatedDepths[tile.Vertex1], generatedDepths[tile.Vertex2], 0x38)
                : NativeTerrainHqQueuedRenderContract.ComputeGt4Bucket(
                    generatedDepths[tile.Vertex0], generatedDepths[tile.Vertex1],
                    generatedDepths[tile.Vertex2], generatedDepths[tile.Vertex3], 0x38);
            Assert(entry.OtBucket == direct,
                $"{fixture.Tier} {fixture.FaceKind} tile {tile.TileIndex} did not use its local SZ topology.");
        }
        baseTileCount += buckets.Count;
    }

    AssertThrows<ArgumentException>(() => NativeTerrainHqQueuedRenderContract.ComputeBaseTileBuckets(
        NativeTerrainHqQueueTier.Normal,
        NativeTerrainHqFaceKind.Quad,
        new int[8],
        0));
    AssertThrows<ArgumentOutOfRangeException>(() => NativeTerrainHqQueuedRenderContract.ComputeBaseTileBuckets(
        NativeTerrainHqQueueTier.None,
        NativeTerrainHqFaceKind.Quad,
        new int[9],
        0));

    return new NativeTerrainHqIntegerFixtureReport(
        selectionFixtureCount,
        baseTileCount,
        topologyTableSha256);
}

static void AppendRetailWords(IncrementalHash hash, ReadOnlySpan<uint> words)
{
    Span<byte> buffer = stackalloc byte[sizeof(uint)];
    foreach (uint word in words)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, word);
        hash.AppendData(buffer);
    }
}

static NativeTerrainPhaseFixtureReport AssertNativeTerrainCoarsePhaseFixture()
{
    var builder = new NativeTerrainPhasedCommandBuilder();
    PsxLowPolyRasterTriangle close = CreateFullPixelLowPolyTriangle(
        new PsxRgb5(31, 31, 31), false, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    PsxLowPolyRasterTriangle hpFirst = CreateFullPixelLowPolyTriangle(
        new PsxRgb5(0, 31, 0), false, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    PsxLowPolyRasterTriangle normal = CreateFullPixelLowPolyTriangle(
        new PsxRgb5(0, 0, 31), false, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    PsxLowPolyRasterTriangle lp = CreateFullPixelLowPolyTriangle(
        new PsxRgb5(31, 0, 0), false, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    PsxLowPolyRasterTriangle hpSecond = CreateFullPixelLowPolyTriangle(
        new PsxRgb5(0, 16, 0), false, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);

    // Deliberately enqueue phases out of order. The builder must retain FIFO
    // inside a phase while flattening the retail coarse phase order.
    builder.Add(NativeTerrainStaticPassPhase.CloseHighQuality, 100, close);
    builder.Add(NativeTerrainStaticPassPhase.HighPolyBase, 100, hpFirst);
    builder.Add(NativeTerrainStaticPassPhase.NormalHighQuality, 100, normal);
    builder.Add(NativeTerrainStaticPassPhase.LowPoly, 100, lp);
    builder.Add(NativeTerrainStaticPassPhase.HighPolyBase, 100, hpSecond);
    List<PsxTerrainBoundedRenderCommand> commands = builder.Build();
    Assert(commands.Count == 5 && builder.Count == 5,
        "Native coarse phase builder lost a staged terrain command.");
    Assert(ReferenceEquals(commands[0].Primitive, lp) &&
           ReferenceEquals(commands[1].Primitive, hpFirst) &&
           ReferenceEquals(commands[2].Primitive, hpSecond) &&
           ReferenceEquals(commands[3].Primitive, normal) &&
           ReferenceEquals(commands[4].Primitive, close),
        "Native terrain phases were not LP -> all HP base -> normal HQ -> close HQ with FIFO ties.");
    Assert(commands.Select(command => command.InsertSequence).SequenceEqual(Enumerable.Range(0, 5)) &&
           commands.All(command => command.OtBucket == 100),
        "Native coarse phase flattening did not assign one stable global insertion sequence.");

    var framebuffer = new PsxRgb5Framebuffer(1, 1, new PsxRgb5(0, 0, 0));
    PsxTerrainRasterResult result = PsxTerrainBoundedRasterizer.Paint(framebuffer, commands);
    Assert(framebuffer.GetPixel(0, 0) == new PsxRgb5(31, 31, 31) &&
           result.PainterSequence.Select(entry => entry.InsertSequence).SequenceEqual(Enumerable.Range(0, 5)),
        "Bounded OT execution did not retain the coarse-phase FIFO sequence inside a tied bucket.");

    AssertThrows<ArgumentOutOfRangeException>(() => builder.Add(
        NativeTerrainStaticPassPhase.LowPoly,
        NativeTerrainOrderingTableContract.MaximumBucket + 1,
        lp));
    AssertThrows<ArgumentOutOfRangeException>(() => builder.Add(
        (NativeTerrainStaticPassPhase)99,
        0,
        lp));

    return new NativeTerrainPhaseFixtureReport(
        commands.Count,
        Convert.ToHexString(SHA256.HashData(framebuffer.CopyPackedLittleEndianBytes())));
}

static NativeTerrainOrderingInventoryReport AssertNativeTerrainOrderingInventory()
{
    string workspaceRoot = FindWorkspaceRoot();
    string cacheRoot = Path.Combine(workspaceRoot, "editor-cache");
    string[] overlays = Directory.GetFiles(
            cacheRoot,
            "*-runtime-scene-editor-overlay.json",
            SearchOption.TopDirectoryOnly)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Assert(overlays.Length == 35,
        $"Native terrain OT inventory expected 35 retail overlays, found {overlays.Length} in {cacheRoot}.");

    int highPolyFaceCount = 0;
    int highPolyLowThreeBiasCount = 0;
    int highPolyRendererFlagAliasCount = 0;
    int highPolyTierControlBit6Count = 0;
    int highPolyHqDisableBit7Count = 0;
    int lowPolyFaceCount = 0;
    int lowPolyOrderingBiasCount = 0;
    using IncrementalHash rawFieldHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    AppendString(rawFieldHash, NativeTerrainOrderingTableContract.Name);
    AppendString(rawFieldHash, NativeTerrainOrderingTableContract.PassOrder);

    foreach (string overlay in overlays)
    {
        string levelKey = Path.GetFileName(overlay)
            .Replace("-runtime-scene-editor-overlay.json", "", StringComparison.Ordinal);
        AppendString(rawFieldHash, levelKey);
        using FileStream stream = File.OpenRead(overlay);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        JsonElement highPoly = root.GetProperty("candidates")[0].GetProperty("polygons");
        JsonElement lowPoly = root.GetProperty("lowDetail").GetProperty("polygons");

        foreach (JsonElement face in highPoly.EnumerateArray())
        {
            uint word3 = ParseHexUInt32(face.GetProperty("word4").GetString());
            int serializedDepth = face.GetProperty("depth").GetInt32();
            int exactLegacyField = (int)((word3 >> 3) & 0x1Fu);
            Assert(serializedDepth == exactLegacyField,
                $"{levelKey} HP face serialized depth does not match native word-3 bits 3..7.");
            Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
                       [512, 512, 512, 512],
                       word3,
                       out int bucket,
                       out _) &&
                   bucket == 64 + (int)((word3 & 0x38u) >> 1),
                $"{levelKey} HP face did not resolve its exact native base OT bias.");

            highPolyFaceCount++;
            if ((word3 & 0x38u) != 0)
                highPolyLowThreeBiasCount++;
            if ((word3 & 0xC0u) != 0)
            {
                Assert(NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
                           [512, 512, 512, 512],
                           word3 & ~0xC0u,
                           out int bucketWithoutRendererFlags,
                           out _) &&
                       bucketWithoutRendererFlags == bucket,
                    $"{levelKey} HP renderer flag bits 6/7 inflated its native base OT bucket.");
                highPolyRendererFlagAliasCount++;
            }
            if ((word3 & NativeTerrainHqQueuedRenderContract.TierControlBit6Mask) != 0)
                highPolyTierControlBit6Count++;
            if ((word3 & NativeTerrainHqQueuedRenderContract.HqDisableBit7Mask) != 0)
                highPolyHqDisableBit7Count++;
            AppendInt32(rawFieldHash, unchecked((int)word3));
        }

        foreach (JsonElement face in lowPoly.EnumerateArray())
        {
            uint rawWord1 = face.GetProperty("rawWord1").GetUInt32();
            int serializedBias = face.GetProperty("orderingTableBias").GetInt32();
            int exactBias = (int)((rawWord1 >> 3) & 0x1Fu);
            Assert(serializedBias == exactBias,
                $"{levelKey} LP face ordering bias does not match raw word-1 bits 3..7.");
            Assert(NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
                       [1792, 1792, 1792, 1792],
                       rawWord1,
                       out int bucket,
                       out _) &&
                   bucket == 288 + (int)(rawWord1 & 0xF8u),
                $"{levelKey} LP face did not resolve its exact native OT bias.");

            lowPolyFaceCount++;
            if ((rawWord1 & 0xF8u) != 0)
                lowPolyOrderingBiasCount++;
            AppendInt32(rawFieldHash, unchecked((int)rawWord1));
        }
    }

    Assert(highPolyFaceCount == 190640,
        $"Native HP OT inventory changed: {highPolyFaceCount:N0} faces.");
    Assert(highPolyLowThreeBiasCount == 75394,
        $"Native HP low-three-bit OT-bias inventory changed: {highPolyLowThreeBiasCount:N0} faces.");
    Assert(highPolyRendererFlagAliasCount == 1353,
        $"Native HP flag-alias guard inventory changed: {highPolyRendererFlagAliasCount:N0} faces.");
    Assert(highPolyTierControlBit6Count == 0,
        $"Native HP word-3 bit-6 inventory changed: {highPolyTierControlBit6Count:N0} faces.");
    Assert(highPolyHqDisableBit7Count == 1353,
        $"Native HP word-3 bit-7 inventory changed: {highPolyHqDisableBit7Count:N0} faces.");
    Assert(lowPolyFaceCount == 83401,
        $"Native LP OT inventory changed: {lowPolyFaceCount:N0} faces.");
    Assert(lowPolyOrderingBiasCount == 3679,
        $"Native LP OT-bias inventory changed: {lowPolyOrderingBiasCount:N0} faces.");

    string rawFieldSha256 = Convert.ToHexString(rawFieldHash.GetHashAndReset());
    Assert(rawFieldSha256 == "4467F959DE67D3D3FF9A4B4844D3A18B0F13C4E1B5A64BE094C848D4ED3C1D3A",
        $"Native all-35 HP/LP ordering raw-field fingerprint changed: {rawFieldSha256}.");
    return new NativeTerrainOrderingInventoryReport(
        overlays.Length,
        highPolyFaceCount,
        highPolyLowThreeBiasCount,
        highPolyRendererFlagAliasCount,
        highPolyTierControlBit6Count,
        highPolyHqDisableBit7Count,
        lowPolyFaceCount,
        lowPolyOrderingBiasCount,
        rawFieldSha256);
}

static string FindWorkspaceRoot()
{
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        DirectoryInfo? directory = new(Path.GetFullPath(start));
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "editor-cache")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException("Could not locate the Spyro editor workspace/editor-cache for the all-35 native OT inventory.");
}

static uint ParseHexUInt32(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
        throw new InvalidDataException("Native HP word-3 text is missing.");
    string value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
    return uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

static void AssertAbrBoundaryFixtures()
{
    AssertBlend(0, 0, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground, 0);
    AssertBlend(1, 1, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground, 0);
    AssertBlend(31, 31, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground, 30);
    AssertBlend(31, 0, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground, 15);
    AssertBlend(30, 31, PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground, 30);

    AssertBlend(0, 31, PsxSemiTransparencyMode.BackgroundPlusForeground, 31);
    AssertBlend(31, 1, PsxSemiTransparencyMode.BackgroundPlusForeground, 31);
    AssertBlend(14, 17, PsxSemiTransparencyMode.BackgroundPlusForeground, 31);

    AssertBlend(0, 31, PsxSemiTransparencyMode.BackgroundMinusForeground, 0);
    AssertBlend(31, 1, PsxSemiTransparencyMode.BackgroundMinusForeground, 30);
    AssertBlend(17, 14, PsxSemiTransparencyMode.BackgroundMinusForeground, 3);

    AssertBlend(0, 3, PsxSemiTransparencyMode.BackgroundPlusQuarterForeground, 0);
    AssertBlend(0, 4, PsxSemiTransparencyMode.BackgroundPlusQuarterForeground, 1);
    AssertBlend(23, 31, PsxSemiTransparencyMode.BackgroundPlusQuarterForeground, 30);
    AssertBlend(31, 31, PsxSemiTransparencyMode.BackgroundPlusQuarterForeground, 31);
}

static void AssertExhaustiveChannelArithmetic()
{
    foreach (PsxSemiTransparencyMode mode in Enum.GetValues<PsxSemiTransparencyMode>())
    {
        for (int background = 0; background <= 31; background++)
        {
            for (int foreground = 0; foreground <= 31; foreground++)
            {
                int expected = mode switch
                {
                    PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground =>
                        (background >> 1) + (foreground >> 1),
                    PsxSemiTransparencyMode.BackgroundPlusForeground =>
                        Math.Min(31, background + foreground),
                    PsxSemiTransparencyMode.BackgroundMinusForeground =>
                        Math.Max(0, background - foreground),
                    PsxSemiTransparencyMode.BackgroundPlusQuarterForeground =>
                        Math.Min(31, background + (foreground >> 2)),
                    _ => throw new InvalidOperationException()
                };
                byte actual = PsxTerrainBlendKernel.BlendChannel5(background, foreground, mode);
                Assert(actual == expected,
                    $"{mode}: bg={background}, fg={foreground} produced {actual}, expected {expected}.");
            }
        }
    }

    PsxRgb5 vector = PsxTerrainBlendKernel.Blend(
        new PsxRgb5(31, 10, 0),
        new PsxRgb5(1, 30, 31),
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    Assert(vector == new PsxRgb5(30, 0, 0), "RGB vector blending did not apply channels independently.");
}

static void AssertTexturedPixelBoundaryFixtures()
{
    PsxRgb5 background = new(20, 12, 4);

    foreach (bool primitiveSemiTransparent in new[] { false, true })
    {
        PsxTerrainTexturedPixelResult transparent = PsxTerrainBlendKernel.ResolveTexturedPixel(
            background,
            new Psx555TextureWord(0x0000),
            primitiveSemiTransparent,
            PsxSemiTransparencyMode.BackgroundPlusForeground);
        Assert(!transparent.WritesFramebuffer &&
               transparent.Disposition == PsxTexturedPixelDisposition.Transparent &&
               transparent.OutputColor == background && transparent.TextureWord.RawWord == 0,
            "Raw word 0x0000 must skip the framebuffer for either primitive state.");
    }

    PsxTerrainTexturedPixelResult opaqueBlack = PsxTerrainBlendKernel.ResolveTexturedPixel(
        background,
        new Psx555TextureWord(0x8000),
        primitiveSemiTransparent: false,
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    Assert(opaqueBlack.WritesFramebuffer &&
           opaqueBlack.Disposition == PsxTexturedPixelDisposition.Opaque &&
           opaqueBlack.OutputColor == new PsxRgb5(0, 0, 0),
        "0x8000 must be opaque black when primitive semitransparency is disabled.");

    PsxTerrainTexturedPixelResult stpBlackHalf = PsxTerrainBlendKernel.ResolveTexturedPixel(
        background,
        new Psx555TextureWord(0x8000),
        primitiveSemiTransparent: true,
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    Assert(stpBlackHalf.Disposition == PsxTexturedPixelDisposition.SemiTransparent &&
           stpBlackHalf.OutputColor == new PsxRgb5(10, 6, 2),
        "STP-set black must apply ABR0 rather than become transparent or opaque.");

    PsxTerrainTexturedPixelResult stpClearRed = PsxTerrainBlendKernel.ResolveTexturedPixel(
        background,
        new Psx555TextureWord(0x001F),
        primitiveSemiTransparent: true,
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    Assert(stpClearRed.Disposition == PsxTexturedPixelDisposition.Opaque &&
           stpClearRed.OutputColor == new PsxRgb5(31, 0, 0),
        "A nonzero STP-clear texel must remain opaque even on a semitransparent primitive.");

    PsxTerrainTexturedPixelResult stpRed = PsxTerrainBlendKernel.ResolveTexturedPixel(
        background,
        new Psx555TextureWord(0x801F),
        primitiveSemiTransparent: true,
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    Assert(stpRed.Disposition == PsxTexturedPixelDisposition.SemiTransparent &&
           stpRed.OutputColor == new PsxRgb5(0, 12, 4) && stpRed.TextureWord.RawWord == 0x801F,
        "An STP-set red texel must preserve its raw word and apply ABR2.");

    foreach (PsxSemiTransparencyMode mode in Enum.GetValues<PsxSemiTransparencyMode>())
    {
        PsxTerrainTexturedPixelResult result = PsxTerrainBlendKernel.ResolveTexturedPixel(
            background,
            new Psx555TextureWord(0xFFFF),
            primitiveSemiTransparent: true,
            mode);
        Assert(result.OutputColor == PsxTerrainBlendKernel.Blend(background, new PsxRgb5(31, 31, 31), mode),
            $"STP white did not use {mode} against the existing framebuffer.");
    }
}

static void AssertExhaustiveTextureWordClassification()
{
    PsxRgb5 background = new(7, 19, 31);
    for (int raw = 0; raw <= ushort.MaxValue; raw++)
    {
        Psx555TextureWord word = new((ushort)raw);
        foreach (bool primitiveSemiTransparent in new[] { false, true })
        {
            PsxTexturedPixelDisposition expected = raw == 0
                ? PsxTexturedPixelDisposition.Transparent
                : primitiveSemiTransparent && (raw & 0x8000) != 0
                    ? PsxTexturedPixelDisposition.SemiTransparent
                    : PsxTexturedPixelDisposition.Opaque;
            PsxTexturedPixelDisposition actual =
                PsxTerrainBlendKernel.ClassifyTexturedPixel(word, primitiveSemiTransparent);
            Assert(actual == expected,
                $"Texture word 0x{raw:X4}, primitiveSemi={primitiveSemiTransparent} classified as {actual}, expected {expected}.");

            PsxTerrainTexturedPixelResult resolved = PsxTerrainBlendKernel.ResolveTexturedPixel(
                background,
                word,
                primitiveSemiTransparent,
                PsxSemiTransparencyMode.BackgroundPlusQuarterForeground);
            Assert(resolved.TextureWord.RawWord == raw,
                $"Texture word 0x{raw:X4} lost raw PSX555/STP state.");
            Assert(resolved.WritesFramebuffer == (raw != 0),
                $"Texture word 0x{raw:X4} produced an invalid framebuffer write decision.");
            if (expected == PsxTexturedPixelDisposition.Opaque)
            {
                Assert(resolved.OutputColor == word.Color,
                    $"Opaque texture word 0x{raw:X4} did not overwrite with its RGB5 value.");
            }
        }
    }
}

static void AssertExhaustiveHighPolyPrimitiveClassification()
{
    for (int raw = 0; raw <= byte.MaxValue; raw++)
    {
        PsxTerrainPrimitiveClassification classification =
            PsxTerrainBlendKernel.ClassifyHighPolyMaterialByte((byte)raw);
        Assert(classification.RawMaterialByte == raw,
            $"HP material byte 0x{raw:X2} was not preserved.");
        if (raw == PsxTerrainBlendKernel.OpaqueUntexturedMaterialSentinel)
        {
            Assert(classification.Kind == PsxTerrainPrimitiveMaterialKind.OpaqueUntexturedSentinel &&
                   classification.IsOpaqueUntexturedSentinel && !classification.IsTextured &&
                   !classification.PrimitiveSemiTransparent && classification.TextureId == -1,
                "HP material byte 0xFF must be the opaque untextured sentinel, not semitransparent texture 127.");
            continue;
        }

        bool expectedSemi = (raw & 0x80) != 0;
        Assert(classification.IsTextured && !classification.IsOpaqueUntexturedSentinel &&
               classification.PrimitiveSemiTransparent == expectedSemi &&
               classification.TextureId == (raw & 0x7F) &&
               classification.Kind == (expectedSemi
                   ? PsxTerrainPrimitiveMaterialKind.SemiTransparentTextured
                   : PsxTerrainPrimitiveMaterialKind.OpaqueTextured),
            $"HP material byte 0x{raw:X2} classified incorrectly.");
    }
}

static void AssertInvalidInputsFailClosed()
{
    AssertThrows<ArgumentOutOfRangeException>(() => new PsxRgb5(-1, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => new PsxRgb5(0, 32, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => new PsxRgb8(-1, 0, 0));
    AssertThrows<ArgumentOutOfRangeException>(() => new PsxRgb8(0, 256, 0));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBoundedRasterizer.ModulateTextureChannel5(-1, 128));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBoundedRasterizer.ModulateTextureChannel5(32, 128));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBoundedRasterizer.ModulateTextureChannel5(31, -1));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBoundedRasterizer.ModulateTextureChannel5(31, 256));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBlendKernel.BlendChannel5(32, 0, PsxSemiTransparencyMode.BackgroundPlusForeground));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBlendKernel.BlendChannel5(0, -1, PsxSemiTransparencyMode.BackgroundPlusForeground));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBlendKernel.BlendChannel5(0, 0, (PsxSemiTransparencyMode)4));
    AssertThrows<ArgumentOutOfRangeException>(() =>
        PsxTerrainBlendKernel.ResolveTexturedPixel(
            new PsxRgb5(0, 0, 0),
            new Psx555TextureWord(0),
            false,
            (PsxSemiTransparencyMode)255));
}

static BoundedRasterFixtureReport AssertBoundedRasterFixtures()
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    AppendString(hash, PsxTerrainBoundedRasterContract.Name);
    AppendString(hash, PsxTerrainBoundedRasterContract.PainterOrdering);
    AppendString(hash, PsxTerrainBoundedRasterContract.CoverageRule);
    AppendString(hash, PsxTerrainBoundedRasterContract.TextureModulation);
    AppendString(hash, PsxTerrainBoundedRasterContract.Dithering);

    int fixtureCount = 0;
    int commands = 0;
    long coveredSamples = 0;
    long framebufferWrites = 0;
    long transparentTexelSkips = 0;
    long opaqueWrites = 0;
    long semiTransparentWrites = 0;
    long abr0Writes = 0;
    long abr1Writes = 0;
    long abr2Writes = 0;
    long abr3Writes = 0;

    void IncludeFixture(
        string name,
        PsxRgb5Framebuffer framebuffer,
        PsxTerrainRasterResult result)
    {
        fixtureCount++;
        commands += result.Statistics.CommandsSubmitted;
        coveredSamples += result.Statistics.CoveredSamples;
        framebufferWrites += result.Statistics.FramebufferWrites;
        transparentTexelSkips += result.Statistics.TransparentTexelSkips;
        opaqueWrites += result.Statistics.OpaqueWrites;
        semiTransparentWrites += result.Statistics.SemiTransparentWrites;
        abr0Writes += result.Statistics.Abr0Writes;
        abr1Writes += result.Statistics.Abr1Writes;
        abr2Writes += result.Statistics.Abr2Writes;
        abr3Writes += result.Statistics.Abr3Writes;

        AppendString(hash, name);
        AppendInt32(hash, framebuffer.Width);
        AppendInt32(hash, framebuffer.Height);
        hash.AppendData(framebuffer.CopyPackedLittleEndianBytes());
        AppendStatistics(hash, result.Statistics);
        foreach (PsxTerrainPainterSequenceEntry entry in result.PainterSequence)
        {
            AppendInt32(hash, entry.OtBucket);
            AppendInt32(hash, entry.InsertSequence);
            AppendString(hash, entry.PrimitiveKind);
        }
    }

    // Two semitransparent triangles share a pixel-center diagonal. The
    // top-left rule must assign every sample in the 4x4 quad exactly once.
    PsxRgb5 sharedEdgeBackground = new(4, 4, 4);
    PsxRgb5 sharedEdgeUpperForeground = new(1, 2, 3);
    PsxRgb5 sharedEdgeLowerForeground = new(3, 1, 2);
    PsxRgb5 sharedEdgeUpperExpected = new(5, 6, 7);
    PsxRgb5 sharedEdgeLowerExpected = new(7, 5, 6);
    var sharedEdgeFrame = new PsxRgb5Framebuffer(6, 6, sharedEdgeBackground);
    PsxTerrainRasterResult sharedEdgeResult = PsxTerrainBoundedRasterizer.Paint(
        sharedEdgeFrame,
        [
            new PsxTerrainBoundedRenderCommand(
                100,
                0,
                CreateLowPolyTriangle(
                    new PsxScreenPoint(1, 1),
                    new PsxScreenPoint(5, 1),
                    new PsxScreenPoint(5, 5),
                    sharedEdgeUpperForeground,
                    semiTransparent: true,
                    PsxSemiTransparencyMode.BackgroundPlusForeground)),
            new PsxTerrainBoundedRenderCommand(
                100,
                1,
                CreateLowPolyTriangle(
                    new PsxScreenPoint(1, 1),
                    new PsxScreenPoint(5, 5),
                    new PsxScreenPoint(1, 5),
                    sharedEdgeLowerForeground,
                    semiTransparent: true,
                    PsxSemiTransparencyMode.BackgroundPlusForeground))
        ]);
    Assert(sharedEdgeResult.Statistics.CoveredSamples == 16 &&
           sharedEdgeResult.Statistics.FramebufferWrites == 16 &&
           sharedEdgeResult.Statistics.SemiTransparentWrites == 16 &&
           sharedEdgeResult.Statistics.Abr1Writes == 16,
        "Top-left shared-edge fixture did not cover its 4x4 quad exactly once.");
    for (int y = 0; y < sharedEdgeFrame.Height; y++)
    {
        for (int x = 0; x < sharedEdgeFrame.Width; x++)
        {
            bool insideQuad = x is >= 1 and < 5 && y is >= 1 and < 5;
            PsxRgb5 expected = !insideQuad
                ? sharedEdgeBackground
                : x >= y
                    ? sharedEdgeUpperExpected
                    : sharedEdgeLowerExpected;
            Assert(sharedEdgeFrame.GetPixel(x, y) == expected,
                $"Top-left shared-edge fixture pixel ({x},{y}) changed unexpectedly.");
        }
    }
    IncludeFixture("shared-edge-top-left", sharedEdgeFrame, sharedEdgeResult);

    // LP opaque and all four LP semitransparency modes exercise the no-STP
    // path. Reversed winding on the opaque triangle also locks normalization.
    PsxRgb5 lpBackground = new(13, 7, 29);
    PsxRgb5 lpForeground = new(5, 11, 3);
    var opaqueLpFrame = new PsxRgb5Framebuffer(1, 1, lpBackground);
    PsxLowPolyRasterTriangle reversedOpaque = CreateLowPolyTriangle(
        new PsxScreenPoint(-1, -1),
        new PsxScreenPoint(-1, 3),
        new PsxScreenPoint(3, -1),
        lpForeground,
        semiTransparent: false,
        PsxSemiTransparencyMode.BackgroundPlusQuarterForeground);
    PsxTerrainRasterResult opaqueLpResult = PsxTerrainBoundedRasterizer.Paint(
        opaqueLpFrame,
        [new PsxTerrainBoundedRenderCommand(0, 0, reversedOpaque)]);
    Assert(opaqueLpFrame.GetPixel(0, 0) == lpForeground &&
           opaqueLpResult.Statistics.OpaqueWrites == 1 &&
           opaqueLpResult.Statistics.SemiTransparentWrites == 0,
        "Opaque LP triangle did not overwrite one covered RGB5 pixel.");
    IncludeFixture("lp-opaque-reversed-winding", opaqueLpFrame, opaqueLpResult);

    var gouraudFrame = new PsxRgb5Framebuffer(1, 1, lpBackground);
    var gouraudTriangle = new PsxLowPolyRasterTriangle(
        new PsxLowPolyRasterVertex(new PsxScreenPoint(-1, -1), new PsxRgb5(0, 0, 0)),
        new PsxLowPolyRasterVertex(new PsxScreenPoint(3, -1), new PsxRgb5(16, 0, 0)),
        new PsxLowPolyRasterVertex(new PsxScreenPoint(-1, 3), new PsxRgb5(0, 16, 0)),
        semiTransparent: false,
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    PsxTerrainRasterResult gouraudResult = PsxTerrainBoundedRasterizer.Paint(
        gouraudFrame,
        [new PsxTerrainBoundedRenderCommand(0, 0, gouraudTriangle)]);
    Assert(gouraudFrame.GetPixel(0, 0) == new PsxRgb5(6, 6, 0) &&
           gouraudResult.Statistics.OpaqueWrites == 1,
        "Bounded LP Gouraud interpolation changed at the half-pixel fixture sample.");
    IncludeFixture("lp-gouraud-half-pixel", gouraudFrame, gouraudResult);

    foreach (PsxSemiTransparencyMode mode in Enum.GetValues<PsxSemiTransparencyMode>())
    {
        var semiLpFrame = new PsxRgb5Framebuffer(1, 1, lpBackground);
        PsxTerrainRasterResult semiLpResult = PsxTerrainBoundedRasterizer.Paint(
            semiLpFrame,
            [
                new PsxTerrainBoundedRenderCommand(
                    0,
                    0,
                    CreateFullPixelLowPolyTriangle(lpForeground, semiTransparent: true, mode))
            ]);
        Assert(semiLpFrame.GetPixel(0, 0) == PsxTerrainBlendKernel.Blend(lpBackground, lpForeground, mode) &&
               semiLpResult.Statistics.SemiTransparentWrites == 1 &&
               semiLpResult.Statistics.GetAbrWriteCount(mode) == 1,
            $"LP triangle did not apply descriptor-free {mode} blending to every covered pixel.");
        IncludeFixture($"lp-semitrans-{(byte)mode}", semiLpFrame, semiLpResult);
    }

    // One 2x2 descriptor carries zero, STP-clear red, STP-set red, and
    // STP-set black. This catches whole-primitive alpha approximations.
    ushort[] mixedWords = [0x0000, 0x001F, 0x801F, 0x8000];
    var mixedDescriptor = new PsxTerrainTextureDescriptor(
        2,
        2,
        mixedWords,
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    PsxRgb5 mixedBackground = new(10, 12, 14);
    var mixedSemiFrame = new PsxRgb5Framebuffer(2, 2, mixedBackground);
    PsxTerrainRasterResult mixedSemiResult = PsxTerrainBoundedRasterizer.Paint(
        mixedSemiFrame,
        CreateTexturedQuadCommands(
            0,
            0,
            2,
            mixedDescriptor,
            primitiveSemiTransparent: true,
            otBucket: 8,
            firstInsertSequence: 0));
    Assert(mixedSemiFrame.GetPixel(0, 0) == mixedBackground,
        "Texture word 0x0000 painted a rasterized pixel.");
    Assert(mixedSemiFrame.GetPixel(1, 0) == new PsxRgb5(31, 0, 0),
        "STP-clear red did not remain opaque on a semitransparent primitive.");
    Assert(mixedSemiFrame.GetPixel(0, 1) == new PsxRgb5(0, 12, 14),
        "STP-set red did not apply descriptor-local ABR2.");
    Assert(mixedSemiFrame.GetPixel(1, 1) == mixedBackground,
        "STP-set black did not blend as black under ABR2.");
    Assert(mixedSemiResult.Statistics.CoveredSamples == 4 &&
           mixedSemiResult.Statistics.TransparentTexelSkips == 1 &&
           mixedSemiResult.Statistics.OpaqueWrites == 1 &&
           mixedSemiResult.Statistics.SemiTransparentWrites == 2 &&
           mixedSemiResult.Statistics.Abr2Writes == 2,
        "Mixed STP raster statistics changed.");
    IncludeFixture("textured-mixed-semitrans", mixedSemiFrame, mixedSemiResult);

    var mixedOpaqueFrame = new PsxRgb5Framebuffer(2, 2, mixedBackground);
    PsxTerrainRasterResult mixedOpaqueResult = PsxTerrainBoundedRasterizer.Paint(
        mixedOpaqueFrame,
        CreateTexturedQuadCommands(
            0,
            0,
            2,
            mixedDescriptor,
            primitiveSemiTransparent: false,
            otBucket: 8,
            firstInsertSequence: 0));
    Assert(mixedOpaqueFrame.GetPixel(0, 0) == mixedBackground &&
           mixedOpaqueFrame.GetPixel(1, 0) == new PsxRgb5(31, 0, 0) &&
           mixedOpaqueFrame.GetPixel(0, 1) == new PsxRgb5(31, 0, 0) &&
           mixedOpaqueFrame.GetPixel(1, 1) == new PsxRgb5(0, 0, 0),
        "Primitive-semi-off raster did not treat every nonzero STP word as opaque.");
    Assert(mixedOpaqueResult.Statistics.TransparentTexelSkips == 1 &&
           mixedOpaqueResult.Statistics.OpaqueWrites == 3 &&
           mixedOpaqueResult.Statistics.SemiTransparentWrites == 0,
        "Opaque mixed-STP raster statistics changed.");
    IncludeFixture("textured-mixed-opaque", mixedOpaqueFrame, mixedOpaqueResult);

    // The legacy three-argument textured vertex constructor must remain
    // byte-identical to explicit neutral 0x80 modulation.
    var explicitNeutralFrame = new PsxRgb5Framebuffer(2, 2, mixedBackground);
    PsxTerrainRasterResult explicitNeutralResult = PsxTerrainBoundedRasterizer.Paint(
        explicitNeutralFrame,
        CreateTexturedQuadCommands(
            0,
            0,
            2,
            mixedDescriptor,
            primitiveSemiTransparent: false,
            otBucket: 8,
            firstInsertSequence: 0,
            modulation: PsxRgb8.NeutralModulation));
    Assert(explicitNeutralFrame.CopyPackedLittleEndianBytes().SequenceEqual(
               mixedOpaqueFrame.CopyPackedLittleEndianBytes()) &&
           explicitNeutralResult.Statistics == mixedOpaqueResult.Statistics,
        "Explicit RGB8 0x80 modulation changed the legacy neutral textured result.");
    IncludeFixture("textured-explicit-neutral", explicitNeutralFrame, explicitNeutralResult);

    // One covered half-pixel sample locks barycentric RGB8 interpolation before
    // the shift-by-seven RGB5 modulation equation.
    var modulationDescriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x7FFF],
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    var modulationFrame = new PsxRgb5Framebuffer(1, 1, new PsxRgb5(3, 4, 5));
    var modulationTriangle = new PsxTexturedRasterTriangle(
        new PsxTexturedRasterVertex(
            new PsxScreenPoint(-1, -1),
            0,
            0,
            new PsxRgb8(0, 0, 0)),
        new PsxTexturedRasterVertex(
            new PsxScreenPoint(3, -1),
            0,
            0,
            new PsxRgb8(128, 0, 0)),
        new PsxTexturedRasterVertex(
            new PsxScreenPoint(-1, 3),
            0,
            0,
            new PsxRgb8(0, 128, 0)),
        modulationDescriptor,
        primitiveSemiTransparent: false);
    PsxTerrainRasterResult modulationResult = PsxTerrainBoundedRasterizer.Paint(
        modulationFrame,
        [new PsxTerrainBoundedRenderCommand(3, 0, modulationTriangle)]);
    Assert(modulationFrame.GetPixel(0, 0) == new PsxRgb5(11, 11, 0) &&
           modulationResult.Statistics.OpaqueWrites == 1,
        "Barycentric RGB8 modulation changed at the half-pixel fixture sample.");
    IncludeFixture("textured-gouraud-modulation", modulationFrame, modulationResult);

    // Shading changes only the foreground. Raw word zero and STP continue to
    // choose skip/opaque/blend before that shaded foreground is written.
    PsxRgb5 shadedBackground = new(20, 12, 4);
    PsxRgb8 quarterRedShade = new(64, 128, 128);
    var stpDescriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x801F],
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    var shadedStpFrame = new PsxRgb5Framebuffer(1, 1, shadedBackground);
    PsxTerrainRasterResult shadedStpResult = PsxTerrainBoundedRasterizer.Paint(
        shadedStpFrame,
        CreateTexturedQuadCommands(0, 0, 1, stpDescriptor, true, 2, 0, quarterRedShade));
    Assert(shadedStpFrame.GetPixel(0, 0) == new PsxRgb5(5, 12, 4) &&
           shadedStpResult.Statistics.SemiTransparentWrites == 1 &&
           shadedStpResult.Statistics.Abr2Writes == 1,
        "STP-set texel did not blend its shaded foreground through descriptor ABR2.");
    IncludeFixture("textured-modulated-stp-blend", shadedStpFrame, shadedStpResult);

    var stpClearDescriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x001F],
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    var shadedOpaqueFrame = new PsxRgb5Framebuffer(1, 1, shadedBackground);
    PsxTerrainRasterResult shadedOpaqueResult = PsxTerrainBoundedRasterizer.Paint(
        shadedOpaqueFrame,
        CreateTexturedQuadCommands(0, 0, 1, stpClearDescriptor, true, 2, 0, quarterRedShade));
    Assert(shadedOpaqueFrame.GetPixel(0, 0) == new PsxRgb5(15, 0, 0) &&
           shadedOpaqueResult.Statistics.OpaqueWrites == 1 &&
           shadedOpaqueResult.Statistics.SemiTransparentWrites == 0,
        "STP-clear texel did not overwrite with its shaded foreground.");
    IncludeFixture("textured-modulated-stp-clear", shadedOpaqueFrame, shadedOpaqueResult);

    var zeroDescriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x0000],
        PsxSemiTransparencyMode.BackgroundPlusForeground);
    var shadedZeroFrame = new PsxRgb5Framebuffer(1, 1, shadedBackground);
    PsxTerrainRasterResult shadedZeroResult = PsxTerrainBoundedRasterizer.Paint(
        shadedZeroFrame,
        CreateTexturedQuadCommands(0, 0, 1, zeroDescriptor, true, 2, 0, new PsxRgb8(255, 255, 255)));
    Assert(shadedZeroFrame.GetPixel(0, 0) == shadedBackground &&
           shadedZeroResult.Statistics.TransparentTexelSkips == 1 &&
           shadedZeroResult.Statistics.FramebufferWrites == 0,
        "RGB8 modulation must not turn raw texture word zero into a framebuffer write.");
    IncludeFixture("textured-modulated-zero-skip", shadedZeroFrame, shadedZeroResult);

    // Adjacent descriptor-local ABR modes sample the same STP-set red texel.
    // A global blend-mode implementation would make both halves identical.
    var abr0Descriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x801F],
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground);
    var abr2Descriptor = new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x801F],
        PsxSemiTransparencyMode.BackgroundMinusForeground);
    PsxRgb5 descriptorBackground = new(10, 10, 10);
    var descriptorFrame = new PsxRgb5Framebuffer(4, 2, descriptorBackground);
    var descriptorCommands = new List<PsxTerrainBoundedRenderCommand>();
    descriptorCommands.AddRange(CreateTexturedQuadCommands(
        0, 0, 2, abr0Descriptor, true, 4, 0));
    descriptorCommands.AddRange(CreateTexturedQuadCommands(
        2, 0, 2, abr2Descriptor, true, 4, 2));
    PsxTerrainRasterResult descriptorResult = PsxTerrainBoundedRasterizer.Paint(
        descriptorFrame,
        descriptorCommands);
    for (int y = 0; y < 2; y++)
    {
        Assert(descriptorFrame.GetPixel(0, y) == new PsxRgb5(20, 5, 5) &&
               descriptorFrame.GetPixel(1, y) == new PsxRgb5(20, 5, 5),
            "ABR0 descriptor half did not use its local mode.");
        Assert(descriptorFrame.GetPixel(2, y) == new PsxRgb5(0, 10, 10) &&
               descriptorFrame.GetPixel(3, y) == new PsxRgb5(0, 10, 10),
            "ABR2 descriptor half did not use its local mode.");
    }
    Assert(descriptorResult.Statistics.Abr0Writes == 4 &&
           descriptorResult.Statistics.Abr2Writes == 4 &&
           descriptorResult.Statistics.SemiTransparentWrites == 8,
        "Descriptor-local ABR write counts changed.");
    IncludeFixture("descriptor-local-abr", descriptorFrame, descriptorResult);

    // Input collection order is intentionally scrambled. Painter order is
    // bucket descending, then explicit insertion sequence ascending.
    PsxRgb5 painterGreen = new(0, 31, 0);
    PsxRgb5 painterBlue = new(0, 0, 31);
    PsxRgb5 painterRed = new(31, 0, 0);
    var painterFrame = new PsxRgb5Framebuffer(1, 1, new PsxRgb5(0, 0, 0));
    PsxTerrainRasterResult painterResult = PsxTerrainBoundedRasterizer.Paint(
        painterFrame,
        [
            new PsxTerrainBoundedRenderCommand(10, 20, CreateFullPixelLowPolyTriangle(painterRed, false, 0)),
            new PsxTerrainBoundedRenderCommand(20, 0, CreateFullPixelLowPolyTriangle(painterGreen, false, 0)),
            new PsxTerrainBoundedRenderCommand(10, 10, CreateFullPixelLowPolyTriangle(painterBlue, false, 0))
        ]);
    Assert(painterFrame.GetPixel(0, 0) == painterRed,
        "Bounded painter did not execute the last tied insertion last.");
    Assert(painterResult.PainterSequence.Count == 3 &&
           painterResult.PainterSequence[0].OtBucket == 20 &&
           painterResult.PainterSequence[0].InsertSequence == 0 &&
           painterResult.PainterSequence[1].OtBucket == 10 &&
           painterResult.PainterSequence[1].InsertSequence == 10 &&
           painterResult.PainterSequence[2].OtBucket == 10 &&
           painterResult.PainterSequence[2].InsertSequence == 20,
        "Bounded painter sequence changed or fell back to input collection order.");
    IncludeFixture("bounded-painter-sequence", painterFrame, painterResult);

    AssertThrows<ArgumentException>(() => PsxTerrainBoundedRasterizer.Paint(
        new PsxRgb5Framebuffer(1, 1, new PsxRgb5(0, 0, 0)),
        [
            new PsxTerrainBoundedRenderCommand(1, 4, CreateFullPixelLowPolyTriangle(painterRed, false, 0)),
            new PsxTerrainBoundedRenderCommand(1, 4, CreateFullPixelLowPolyTriangle(painterBlue, false, 0))
        ]));
    AssertThrows<ArgumentOutOfRangeException>(() => new PsxTerrainTextureDescriptor(
        1,
        1,
        [0x0000],
        (PsxSemiTransparencyMode)4));
    AssertThrows<ArgumentException>(() => new PsxTerrainTextureDescriptor(
        2,
        2,
        [0x0000],
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground));

    Assert(fixtureCount == 16, $"Expected 16 bounded raster fixtures, found {fixtureCount}.");
    Assert(commands == 28, $"Expected 28 bounded raster commands, found {commands}.");
    Assert(coveredSamples == 49, $"Expected 49 bounded raster samples, found {coveredSamples}.");
    Assert(framebufferWrites == 45, $"Expected 45 bounded framebuffer writes, found {framebufferWrites}.");
    Assert(transparentTexelSkips == 4, $"Expected four rasterized zero-word skips, found {transparentTexelSkips}.");
    Assert(opaqueWrites == 14 && semiTransparentWrites == 31,
        $"Bounded raster write split changed: {opaqueWrites} opaque / {semiTransparentWrites} semitransparent.");
    Assert(abr0Writes == 5 && abr1Writes == 17 && abr2Writes == 8 && abr3Writes == 1,
        $"Bounded raster ABR split changed: {abr0Writes}/{abr1Writes}/{abr2Writes}/{abr3Writes}.");

    string sha256 = Convert.ToHexString(hash.GetHashAndReset());
    return new BoundedRasterFixtureReport(
        fixtureCount,
        commands,
        coveredSamples,
        framebufferWrites,
        transparentTexelSkips,
        opaqueWrites,
        semiTransparentWrites,
        abr0Writes,
        abr1Writes,
        abr2Writes,
        abr3Writes,
        sha256);
}

static ProductionPaintReport AssertProductionPaintMode()
{
    const int commandCount = 1024;
    const int width = 160;
    const int height = 96;
    PsxRgb5 background = new(3, 5, 7);
    var descriptor = new PsxTerrainTextureDescriptor(
        2,
        2,
        [0x0000, 0x001F, 0x83E0, 0xFC00],
        PsxSemiTransparencyMode.BackgroundPlusQuarterForeground);
    var commands = new List<PsxTerrainBoundedRenderCommand>(commandCount);
    for (int index = 0; index < commandCount; index++)
    {
        int x = (index * 11) % (width - 6);
        int y = (index * 17) % (height - 6);
        PsxScreenPoint point0 = new(x, y);
        PsxScreenPoint point1 = new(x + 6, y);
        PsxScreenPoint point2 = new(x, y + 6);
        PsxTerrainRasterPrimitive primitive;
        if ((index & 1) == 0)
        {
            primitive = new PsxLowPolyRasterTriangle(
                new PsxLowPolyRasterVertex(point0, new PsxRgb5(index & 31, 8, 12)),
                new PsxLowPolyRasterVertex(point1, new PsxRgb5(12, (index >> 1) & 31, 8)),
                new PsxLowPolyRasterVertex(point2, new PsxRgb5(8, 12, (index >> 2) & 31)),
                semiTransparent: (index & 6) == 0,
                (PsxSemiTransparencyMode)(index & 3));
        }
        else
        {
            PsxRgb8 shade0 = new(index & 255, 128, 255 - (index & 255));
            PsxRgb8 shade1 = new(128, (index * 3) & 255, 64);
            PsxRgb8 shade2 = new(255, 64, (index * 5) & 255);
            primitive = new PsxTexturedRasterTriangle(
                new PsxTexturedRasterVertex(point0, 0, 0, shade0),
                new PsxTexturedRasterVertex(point1, descriptor.Width, 0, shade1),
                new PsxTexturedRasterVertex(point2, 0, descriptor.Height, shade2),
                descriptor,
                primitiveSemiTransparent: (index & 3) == 1);
        }

        commands.Add(new PsxTerrainBoundedRenderCommand(
            otBucket: (index * 37) & 255,
            insertSequence: index,
            primitive));
    }

    // Warm both paths so the allocation comparison does not include first-use
    // JIT or generic comparer initialization.
    _ = PsxTerrainBoundedRasterizer.Paint(
        new PsxRgb5Framebuffer(width, height, background),
        commands,
        capturePainterSequence: false);
    _ = PsxTerrainBoundedRasterizer.Paint(
        new PsxRgb5Framebuffer(width, height, background),
        commands,
        capturePainterSequence: true);

    var productionFrame = new PsxRgb5Framebuffer(width, height, background);
    (PsxTerrainRasterResult production, long productionAllocated, double productionMilliseconds) =
        Measure(productionFrame, capturePainterSequence: false);
    var capturedFrame = new PsxRgb5Framebuffer(width, height, background);
    (PsxTerrainRasterResult captured, long capturedAllocated, double capturedMilliseconds) =
        Measure(capturedFrame, capturePainterSequence: true);

    Assert(production.PainterSequence.Count == 0,
        "Production Paint unexpectedly retained per-command painter diagnostics.");
    Assert(captured.PainterSequence.Count == commandCount,
        "Diagnostic Paint did not retain its complete painter sequence.");
    Assert(production.Statistics == captured.Statistics &&
           productionFrame.CopyPackedLittleEndianBytes().SequenceEqual(capturedFrame.CopyPackedLittleEndianBytes()),
        "Omitting painter-sequence capture changed bounded pixels or statistics.");

    long saved = capturedAllocated - productionAllocated;
    Assert(saved >= commandCount * 12L,
        $"Production Paint saved only {saved:N0} bytes for {commandCount:N0} commands.");
    Assert(productionAllocated <= commandCount * 32L,
        $"Production Paint allocated {productionAllocated:N0} bytes for {commandCount:N0} commands.");

    string frameSha256 = Convert.ToHexString(SHA256.HashData(productionFrame.CopyPackedLittleEndianBytes()));
    return new ProductionPaintReport(
        commandCount,
        productionAllocated,
        capturedAllocated,
        saved,
        productionMilliseconds,
        capturedMilliseconds,
        frameSha256);

    (PsxTerrainRasterResult Result, long Allocated, double Milliseconds) Measure(
        PsxRgb5Framebuffer framebuffer,
        bool capturePainterSequence)
    {
        framebuffer.Clear(background);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        PsxTerrainRasterResult result = PsxTerrainBoundedRasterizer.Paint(
            framebuffer,
            commands,
            capturePainterSequence);
        double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return (result, allocated, milliseconds);
    }
}

static PsxLowPolyRasterTriangle CreateFullPixelLowPolyTriangle(
    PsxRgb5 color,
    bool semiTransparent,
    PsxSemiTransparencyMode mode) =>
    CreateLowPolyTriangle(
        new PsxScreenPoint(-1, -1),
        new PsxScreenPoint(3, -1),
        new PsxScreenPoint(-1, 3),
        color,
        semiTransparent,
        mode);

static PsxLowPolyRasterTriangle CreateLowPolyTriangle(
    PsxScreenPoint point0,
    PsxScreenPoint point1,
    PsxScreenPoint point2,
    PsxRgb5 color,
    bool semiTransparent,
    PsxSemiTransparencyMode mode) =>
    new(
        new PsxLowPolyRasterVertex(point0, color),
        new PsxLowPolyRasterVertex(point1, color),
        new PsxLowPolyRasterVertex(point2, color),
        semiTransparent,
        mode);

static IReadOnlyList<PsxTerrainBoundedRenderCommand> CreateTexturedQuadCommands(
    int x,
    int y,
    int size,
    PsxTerrainTextureDescriptor descriptor,
    bool primitiveSemiTransparent,
    int otBucket,
    int firstInsertSequence,
    PsxRgb8? modulation = null)
{
    PsxRgb8 shade = modulation ?? PsxRgb8.NeutralModulation;
    var topLeft = new PsxTexturedRasterVertex(new PsxScreenPoint(x, y), 0, 0, shade);
    var topRight = new PsxTexturedRasterVertex(new PsxScreenPoint(x + size, y), descriptor.Width, 0, shade);
    var bottomRight = new PsxTexturedRasterVertex(
        new PsxScreenPoint(x + size, y + size),
        descriptor.Width,
        descriptor.Height,
        shade);
    var bottomLeft = new PsxTexturedRasterVertex(new PsxScreenPoint(x, y + size), 0, descriptor.Height, shade);
    return
    [
        new PsxTerrainBoundedRenderCommand(
            otBucket,
            firstInsertSequence,
            new PsxTexturedRasterTriangle(
                topLeft,
                topRight,
                bottomRight,
                descriptor,
                primitiveSemiTransparent)),
        new PsxTerrainBoundedRenderCommand(
            otBucket,
            checked(firstInsertSequence + 1),
            new PsxTexturedRasterTriangle(
                topLeft,
                bottomRight,
                bottomLeft,
                descriptor,
                primitiveSemiTransparent))
    ];
}

static void AppendStatistics(IncrementalHash hash, PsxTerrainRasterStatistics statistics)
{
    AppendInt32(hash, statistics.CommandsSubmitted);
    AppendInt32(hash, statistics.CommandsExecuted);
    AppendInt32(hash, statistics.DegenerateTriangles);
    AppendInt64(hash, statistics.CoveredSamples);
    AppendInt64(hash, statistics.FramebufferWrites);
    AppendInt64(hash, statistics.TransparentTexelSkips);
    AppendInt64(hash, statistics.OpaqueWrites);
    AppendInt64(hash, statistics.SemiTransparentWrites);
    AppendInt64(hash, statistics.Abr0Writes);
    AppendInt64(hash, statistics.Abr1Writes);
    AppendInt64(hash, statistics.Abr2Writes);
    AppendInt64(hash, statistics.Abr3Writes);
}

static void AppendString(IncrementalHash hash, string value)
{
    byte[] bytes = Encoding.UTF8.GetBytes(value);
    AppendInt32(hash, bytes.Length);
    hash.AppendData(bytes);
}

static void AppendInt32(IncrementalHash hash, int value)
{
    Span<byte> bytes = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
    hash.AppendData(bytes);
}

static void AppendInt64(IncrementalHash hash, long value)
{
    Span<byte> bytes = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
    hash.AppendData(bytes);
}

static string ComputeArithmeticFingerprint()
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (PsxSemiTransparencyMode mode in Enum.GetValues<PsxSemiTransparencyMode>())
    {
        for (int background = 0; background <= 31; background++)
        {
            for (int foreground = 0; foreground <= 31; foreground++)
            {
                hash.AppendData([
                    (byte)mode,
                    (byte)background,
                    (byte)foreground,
                    PsxTerrainBlendKernel.BlendChannel5(background, foreground, mode)
                ]);
            }
        }
    }
    return Convert.ToHexString(hash.GetHashAndReset());
}

static string ComputeTextureModulationFingerprint()
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    AppendString(hash, PsxTerrainBoundedRasterContract.TextureModulation);
    for (int texture = 0; texture <= 31; texture++)
    {
        for (int shade = 0; shade <= byte.MaxValue; shade++)
        {
            hash.AppendData([
                (byte)texture,
                (byte)shade,
                PsxTerrainBoundedRasterizer.ModulateTextureChannel5(texture, shade)
            ]);
        }
    }
    return Convert.ToHexString(hash.GetHashAndReset());
}

static string ComputePixelClassificationFingerprint()
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    PsxRgb5 background = new(7, 19, 31);
    for (int raw = 0; raw <= ushort.MaxValue; raw++)
    {
        foreach (bool primitiveSemiTransparent in new[] { false, true })
        {
            PsxTerrainTexturedPixelResult result = PsxTerrainBlendKernel.ResolveTexturedPixel(
                background,
                new Psx555TextureWord((ushort)raw),
                primitiveSemiTransparent,
                PsxSemiTransparencyMode.BackgroundPlusQuarterForeground);
            hash.AppendData([
                (byte)raw,
                (byte)(raw >> 8),
                primitiveSemiTransparent ? (byte)1 : (byte)0,
                (byte)result.Disposition,
                result.WritesFramebuffer ? (byte)1 : (byte)0,
                (byte)result.OutputColor.PackedWord,
                (byte)(result.OutputColor.PackedWord >> 8)
            ]);
        }
    }
    return Convert.ToHexString(hash.GetHashAndReset());
}

static void AssertBlend(int background, int foreground, PsxSemiTransparencyMode mode, int expected)
{
    byte actual = PsxTerrainBlendKernel.BlendChannel5(background, foreground, mode);
    Assert(actual == expected,
        $"Boundary {mode}: bg={background}, fg={foreground} produced {actual}, expected {expected}.");
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record BoundedRasterFixtureReport(
    int FixtureCount,
    int Commands,
    long CoveredSamples,
    long FramebufferWrites,
    long TransparentTexelSkips,
    long OpaqueWrites,
    long SemiTransparentWrites,
    long Abr0Writes,
    long Abr1Writes,
    long Abr2Writes,
    long Abr3Writes,
    string Sha256);

internal sealed record ProductionPaintReport(
    int CommandCount,
    long ProductionAllocatedBytes,
    long CapturedAllocatedBytes,
    long SavedAllocatedBytes,
    double ProductionMilliseconds,
    double CapturedMilliseconds,
    string FrameSha256);

internal sealed record NativeTerrainPhaseFixtureReport(
    int CommandCount,
    string FrameSha256);

internal sealed record NativeTerrainHqIntegerFixtureReport(
    int SelectionFixtureCount,
    int BaseTileCount,
    string TopologyTableSha256);

internal sealed record NativeTerrainOrderingInventoryReport(
    int LevelCount,
    int HighPolyFaceCount,
    int HighPolyLowThreeBiasCount,
    int HighPolyRendererFlagAliasCount,
    int HighPolyTierControlBit6Count,
    int HighPolyHqDisableBit7Count,
    int LowPolyFaceCount,
    int LowPolyOrderingBiasCount,
    string RawFieldSha256);
