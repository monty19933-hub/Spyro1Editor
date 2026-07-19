namespace Spyro.Editor.Core.Rendering;

public enum NativeTerrainHqQueueTier
{
    None = 0,
    Normal = 1,
    Close = 2
}

public enum NativeTerrainHqFaceKind
{
    Quad = 0,
    Triangle = 1
}

public enum NativeTerrainHqTilePrimitive
{
    Gt3 = 0,
    Gt4 = 1
}

/// <summary>
/// Result of the retail HQ producer's integer depth/face-flag decision. For a
/// triangle, <see cref="WeightedDepthSum"/> repeats its third source SZ just as
/// the native renderer does.
/// </summary>
public readonly record struct NativeTerrainHqQueueSelection(
    NativeTerrainHqQueueTier Tier,
    int WeightedDepthSum,
    bool AnySourceDepthBelowCloseThreshold,
    bool TierControlBit6,
    bool HqDisableBit7);

/// <summary>
/// One first-generation base tile in exact retail table order. Vertex indexes
/// address the caller-supplied generated SZ lattice; <see cref="Vertex3"/> is
/// -1 for GT3 entries. <see cref="RetailTableWord"/> preserves the corresponding
/// little-endian word from math.data.s verbatim.
/// </summary>
public readonly record struct NativeTerrainHqTileTopology(
    NativeTerrainHqQueueTier Tier,
    NativeTerrainHqFaceKind FaceKind,
    NativeTerrainHqTilePrimitive Primitive,
    int TileIndex,
    int Vertex0,
    int Vertex1,
    int Vertex2,
    int Vertex3,
    uint RetailTableWord,
    byte RetailFlags)
{
    public int VertexCount => Primitive == NativeTerrainHqTilePrimitive.Gt3 ? 3 : 4;
}

public readonly record struct NativeTerrainHqBaseTileBucket(
    NativeTerrainHqTileTopology Topology,
    int OtBucket);

/// <summary>
/// Bounded, integer-input contract for the retail normal/close HQ queue and
/// first-generation base-tile ordering math.
///
/// This contract proves producer thresholds, midpoint truncation, the four
/// fixed base topology tables, and GT3/GT4 bucket equations. It deliberately
/// does not project editor/world coordinates or claim PS1 GTE-equivalent SXY,
/// SZ, FLAG, NCLIP, seam emission, perspective correction, or recursive packet
/// splitting. Callers must supply the generated integer SZ lattice explicitly.
/// </summary>
public static class NativeTerrainHqQueuedRenderContract
{
    public const string Name = "native-terrain-hq-queued-render-integer-input-v1";
    public const string InputBoundary =
        "retail-topology-and-equations-over-caller-supplied-generated-12-bit-sz-lattice";

    public const int DefaultHighPolyLodDepthSum = 0x8000;
    public const int HqDepthSumThreshold = 0x2000;
    public const int CloseSourceDepthThreshold = 0x140;
    public const int MaximumSourceGteDepth = 0xFFFF;
    public const int MaximumGeneratedScratchDepth = 0x0FFF;
    public const int ScratchVertexStride = 0x10;
    public const uint ScratchBaseAddress = 0x1F800000;
    public const uint TierControlBit6Mask = 0x40;
    public const uint HqDisableBit7Mask = 0x80;
    public const uint OtBiasMask = 0x38;
    public const uint PackedColorAverageMask = 0xFFFEFEFF;

    public const int NormalQuadGridPointCount = 9;
    public const int NormalTriangleGridPointCount = 6;
    public const int CloseQuadGridPointCount = 25;
    public const int CloseTriangleGridPointCount = 15;

    private static readonly uint[] NormalQuadOriginWordData =
    [
        0x1F800000, 0x1F800010, 0x1F800030, 0x1F800040
    ];

    private static readonly uint[] CloseQuadOriginWordData =
    [
        0x1F800000, 0x1F800010, 0x1F800020, 0x1F800030,
        0x1F800050, 0x1F800060, 0x1F800070, 0x1F800080,
        0x1F8000A0, 0x1F8000B0, 0x1F8000C0, 0x1F8000D0,
        0x1F8000F0, 0x1F800100, 0x1F800110, 0x1F800120
    ];

    private static readonly uint[] NormalTriangleDescriptorWordData =
    [
        0x00010300, 0x04030102, 0x01020404, 0x03040508
    ];

    private static readonly uint[] CloseTriangleDescriptorWordData =
    [
        0x00010500, 0x06050102, 0x01020604, 0x07060206,
        0x02030708, 0x0807030A, 0x0304080C, 0x05060910,
        0x0A090612, 0x06070A14, 0x0B0A0716, 0x07080B18,
        0x090A0C20, 0x0D0C0A22, 0x0A0B0D24, 0x0C0D0E30
    ];

    private static readonly IReadOnlyList<NativeTerrainHqTileTopology> NormalQuadTopologyData =
        Array.AsReadOnly(BuildQuadTopology(
            NativeTerrainHqQueueTier.Normal,
            gridColumns: 3,
            NormalQuadOriginWordData));

    private static readonly IReadOnlyList<NativeTerrainHqTileTopology> CloseQuadTopologyData =
        Array.AsReadOnly(BuildQuadTopology(
            NativeTerrainHqQueueTier.Close,
            gridColumns: 5,
            CloseQuadOriginWordData));

    private static readonly IReadOnlyList<NativeTerrainHqTileTopology> NormalTriangleTopologyData =
        Array.AsReadOnly(BuildTriangleTopology(
            NativeTerrainHqQueueTier.Normal,
            NormalTriangleDescriptorWordData));

    private static readonly IReadOnlyList<NativeTerrainHqTileTopology> CloseTriangleTopologyData =
        Array.AsReadOnly(BuildTriangleTopology(
            NativeTerrainHqQueueTier.Close,
            CloseTriangleDescriptorWordData));

    public static ReadOnlySpan<uint> NormalQuadOriginWords => NormalQuadOriginWordData;
    public static ReadOnlySpan<uint> CloseQuadOriginWords => CloseQuadOriginWordData;
    public static ReadOnlySpan<uint> NormalTriangleDescriptorWords => NormalTriangleDescriptorWordData;
    public static ReadOnlySpan<uint> CloseTriangleDescriptorWords => CloseTriangleDescriptorWordData;

    public static IReadOnlyList<NativeTerrainHqTileTopology> NormalQuadTopology => NormalQuadTopologyData;
    public static IReadOnlyList<NativeTerrainHqTileTopology> CloseQuadTopology => CloseQuadTopologyData;
    public static IReadOnlyList<NativeTerrainHqTileTopology> NormalTriangleTopology => NormalTriangleTopologyData;
    public static IReadOnlyList<NativeTerrainHqTileTopology> CloseTriangleTopology => CloseTriangleTopologyData;

    public static NativeTerrainHqQueueSelection SelectQueuedTier(
        NativeTerrainHqFaceKind faceKind,
        IReadOnlyList<int> sourceGteDepths,
        uint nativeFaceWord3,
        int highPolyLodDepthSum = DefaultHighPolyLodDepthSum)
    {
        ArgumentNullException.ThrowIfNull(sourceGteDepths);
        ValidateEnum(faceKind);
        if (highPolyLodDepthSum <= 0)
            throw new ArgumentOutOfRangeException(nameof(highPolyLodDepthSum));

        int expectedCount = faceKind == NativeTerrainHqFaceKind.Quad ? 4 : 3;
        if (sourceGteDepths.Count != expectedCount)
        {
            throw new ArgumentException(
                $"A native HQ {faceKind.ToString().ToLowerInvariant()} selection requires exactly {expectedCount} source SZ values.",
                nameof(sourceGteDepths));
        }

        foreach (int depth in sourceGteDepths)
            ValidateSourceDepth(depth, nameof(sourceGteDepths));

        int weightedDepthSum = faceKind == NativeTerrainHqFaceKind.Quad
            ? checked(sourceGteDepths[0] + sourceGteDepths[1] + sourceGteDepths[2] + sourceGteDepths[3])
            : checked(sourceGteDepths[0] + sourceGteDepths[1] + sourceGteDepths[2] + sourceGteDepths[2]);
        bool anyClose = sourceGteDepths.Any(depth => depth < CloseSourceDepthThreshold);
        bool bit6 = (nativeFaceWord3 & TierControlBit6Mask) != 0;
        bool bit7 = (nativeFaceWord3 & HqDisableBit7Mask) != 0;

        NativeTerrainHqQueueTier tier;
        if (weightedDepthSum >= highPolyLodDepthSum ||
            weightedDepthSum >= HqDepthSumThreshold ||
            bit7)
        {
            tier = NativeTerrainHqQueueTier.None;
        }
        else if (!anyClose)
        {
            // In the normal-depth band bit 6 bypasses HQ entirely.
            tier = bit6 ? NativeTerrainHqQueueTier.None : NativeTerrainHqQueueTier.Normal;
        }
        else if (weightedDepthSum == 0)
        {
            tier = NativeTerrainHqQueueTier.None;
        }
        else
        {
            // At close depth bit 6 suppresses only the close tier and routes
            // the face through the normal queue.
            tier = bit6 ? NativeTerrainHqQueueTier.Normal : NativeTerrainHqQueueTier.Close;
        }

        return new NativeTerrainHqQueueSelection(tier, weightedDepthSum, anyClose, bit6, bit7);
    }

    /// <summary>
    /// Exact MIPS signed midpoint used for the camera-space coordinate grids.
    /// Inputs are signed halfwords, their sum is 32-bit, and arithmetic shift
    /// rounds a negative odd sum toward negative infinity before halfword store.
    /// </summary>
    public static short SignedCoordinateMidpoint(short left, short right) =>
        checked((short)(((int)left + right) >> 1));

    /// <summary>
    /// Exact packed color midpoint. The channel mask prevents cross-channel
    /// carries and the final shift is logical.
    /// </summary>
    public static uint PackedColorMidpoint(uint left, uint right) =>
        unchecked(((left & PackedColorAverageMask) + (right & PackedColorAverageMask)) >> 1);

    public static int ComputeGt3Bucket(
        int depth0,
        int depth1,
        int depth2,
        uint nativeFaceWord3)
    {
        ValidateGeneratedDepth(depth0, nameof(depth0));
        ValidateGeneratedDepth(depth1, nameof(depth1));
        ValidateGeneratedDepth(depth2, nameof(depth2));
        int depthSum = checked(depth0 + depth1 + depth2 + depth2);
        return (depthSum >> 7) + FaceOtBias(nativeFaceWord3);
    }

    public static int ComputeGt4Bucket(
        int depth0,
        int depth1,
        int depth2,
        int depth3,
        uint nativeFaceWord3)
    {
        ValidateGeneratedDepth(depth0, nameof(depth0));
        ValidateGeneratedDepth(depth1, nameof(depth1));
        ValidateGeneratedDepth(depth2, nameof(depth2));
        ValidateGeneratedDepth(depth3, nameof(depth3));
        int depthSum = checked(depth0 + depth1 + depth2 + depth3);
        return (depthSum >> 7) + FaceOtBias(nativeFaceWord3);
    }

    /// <summary>
    /// Computes first-generation base-tile buckets in exact retail table/FIFO
    /// insertion order. The supplied lattice must already contain generated
    /// post-RTPS low-12-bit SZ values; this method performs no projection.
    /// </summary>
    public static IReadOnlyList<NativeTerrainHqBaseTileBucket> ComputeBaseTileBuckets(
        NativeTerrainHqQueueTier tier,
        NativeTerrainHqFaceKind faceKind,
        IReadOnlyList<int> generatedScratchDepths,
        uint nativeFaceWord3)
    {
        ArgumentNullException.ThrowIfNull(generatedScratchDepths);
        if (tier is not NativeTerrainHqQueueTier.Normal and not NativeTerrainHqQueueTier.Close)
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "Base tiles require the normal or close HQ tier.");
        ValidateEnum(faceKind);

        int expectedCount = GetGeneratedGridPointCount(tier, faceKind);
        if (generatedScratchDepths.Count != expectedCount)
        {
            throw new ArgumentException(
                $"The native {tier.ToString().ToLowerInvariant()} {faceKind.ToString().ToLowerInvariant()} lattice requires exactly {expectedCount} generated SZ values.",
                nameof(generatedScratchDepths));
        }
        foreach (int depth in generatedScratchDepths)
            ValidateGeneratedDepth(depth, nameof(generatedScratchDepths));

        IReadOnlyList<NativeTerrainHqTileTopology> topology = GetBaseTopology(tier, faceKind);
        var result = new NativeTerrainHqBaseTileBucket[topology.Count];
        for (int index = 0; index < topology.Count; index++)
        {
            NativeTerrainHqTileTopology tile = topology[index];
            int bucket = tile.Primitive == NativeTerrainHqTilePrimitive.Gt3
                ? ComputeGt3Bucket(
                    generatedScratchDepths[tile.Vertex0],
                    generatedScratchDepths[tile.Vertex1],
                    generatedScratchDepths[tile.Vertex2],
                    nativeFaceWord3)
                : ComputeGt4Bucket(
                    generatedScratchDepths[tile.Vertex0],
                    generatedScratchDepths[tile.Vertex1],
                    generatedScratchDepths[tile.Vertex2],
                    generatedScratchDepths[tile.Vertex3],
                    nativeFaceWord3);
            result[index] = new NativeTerrainHqBaseTileBucket(tile, bucket);
        }
        return Array.AsReadOnly(result);
    }

    public static int GetGeneratedGridPointCount(
        NativeTerrainHqQueueTier tier,
        NativeTerrainHqFaceKind faceKind)
    {
        ValidateEnum(faceKind);
        return (tier, faceKind) switch
        {
            (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Quad) => NormalQuadGridPointCount,
            (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Triangle) => NormalTriangleGridPointCount,
            (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Quad) => CloseQuadGridPointCount,
            (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Triangle) => CloseTriangleGridPointCount,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Generated grids require the normal or close HQ tier.")
        };
    }

    public static IReadOnlyList<NativeTerrainHqTileTopology> GetBaseTopology(
        NativeTerrainHqQueueTier tier,
        NativeTerrainHqFaceKind faceKind)
    {
        ValidateEnum(faceKind);
        return (tier, faceKind) switch
        {
            (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Quad) => NormalQuadTopologyData,
            (NativeTerrainHqQueueTier.Normal, NativeTerrainHqFaceKind.Triangle) => NormalTriangleTopologyData,
            (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Quad) => CloseQuadTopologyData,
            (NativeTerrainHqQueueTier.Close, NativeTerrainHqFaceKind.Triangle) => CloseTriangleTopologyData,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Base topology requires the normal or close HQ tier.")
        };
    }

    private static NativeTerrainHqTileTopology[] BuildQuadTopology(
        NativeTerrainHqQueueTier tier,
        int gridColumns,
        IReadOnlyList<uint> originWords)
    {
        var result = new NativeTerrainHqTileTopology[originWords.Count];
        for (int index = 0; index < originWords.Count; index++)
        {
            uint raw = originWords[index];
            int origin = checked((int)(raw - ScratchBaseAddress)) / ScratchVertexStride;
            result[index] = new NativeTerrainHqTileTopology(
                tier,
                NativeTerrainHqFaceKind.Quad,
                NativeTerrainHqTilePrimitive.Gt4,
                index,
                origin,
                origin + 1,
                origin + gridColumns,
                origin + gridColumns + 1,
                raw,
                RetailFlags: 0);
        }
        return result;
    }

    private static NativeTerrainHqTileTopology[] BuildTriangleTopology(
        NativeTerrainHqQueueTier tier,
        IReadOnlyList<uint> descriptorWords)
    {
        var result = new NativeTerrainHqTileTopology[descriptorWords.Count];
        for (int index = 0; index < descriptorWords.Count; index++)
        {
            uint raw = descriptorWords[index];
            result[index] = new NativeTerrainHqTileTopology(
                tier,
                NativeTerrainHqFaceKind.Triangle,
                NativeTerrainHqTilePrimitive.Gt3,
                index,
                (int)((raw >> 24) & 0xFF),
                (int)((raw >> 16) & 0xFF),
                (int)((raw >> 8) & 0xFF),
                Vertex3: -1,
                raw,
                (byte)raw);
        }
        return result;
    }

    private static int FaceOtBias(uint nativeFaceWord3) =>
        (int)((nativeFaceWord3 & OtBiasMask) >> 1);

    private static void ValidateSourceDepth(int depth, string parameterName)
    {
        if ((uint)depth > MaximumSourceGteDepth)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                depth,
                $"A source GTE SZ must be in 0..0x{MaximumSourceGteDepth:X}.");
        }
    }

    private static void ValidateGeneratedDepth(int depth, string parameterName)
    {
        if ((uint)depth > MaximumGeneratedScratchDepth)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                depth,
                $"A generated scratch SZ must be in 0..0x{MaximumGeneratedScratchDepth:X} after removing outcodes.");
        }
    }

    private static void ValidateEnum(NativeTerrainHqFaceKind faceKind)
    {
        if (!Enum.IsDefined(faceKind))
            throw new ArgumentOutOfRangeException(nameof(faceKind), faceKind, "Unknown native HQ face kind.");
    }
}
