namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Proven static-terrain subset of Spyro 1's world ordering-table contract.
/// The bucket equations and coarse renderer phases come directly from the
/// retail environment renderer. Native HQ tile-local depth, subdivision,
/// GTE projection/clipping, occlusion groups, and non-terrain interleaving are
/// deliberately outside this contract.
/// </summary>
public static class NativeTerrainOrderingTableContract
{
    public const string Name = "native-terrain-base-ot-buckets-and-coarse-hq-phases-v1";
    public const string PassOrder = "lp-then-all-hp-base-then-normal-hq-then-close-hq-fifo-v1";
    public const int WorldBucketCount = 0x800;
    public const int MinimumBucket = 0;
    public const int MaximumBucket = WorldBucketCount - 1;
    public const int MaximumGteDepth = 0xFFFF;
    public const int LowPolyBaseBucketOffset = 0x40;

    /// <summary>
    /// Computes the HP LQ/sentinel base bucket from four editor-camera depths.
    /// HP scene coordinates are four times editor scale. Only native face-word
    /// bits 3..5 are OT bias; bits 6/7 are renderer flags and must not leak into
    /// the bucket.
    /// </summary>
    public static bool TryComputeHighPolyBaseBucket(
        IReadOnlyList<double> editorCameraDepths,
        uint nativeFaceWord3,
        out int bucket,
        out int rawGteDepthSum)
    {
        bucket = 0;
        rawGteDepthSum = 0;
        if (!TryQuantizeDepthSum(editorCameraDepths, scale: 4.0, out rawGteDepthSum))
            return false;

        int candidate = (rawGteDepthSum >> 7) + (int)((nativeFaceWord3 & 0x38u) >> 1);
        if (!IsValidBucket(candidate))
            return false;
        bucket = candidate;
        return true;
    }

    /// <summary>
    /// Computes the LP Gouraud bucket. LP coordinates are already in editor
    /// scale; raw word-1 bits 3..7 contribute their authored OT bias directly.
    /// </summary>
    public static bool TryComputeLowPolyBucket(
        IReadOnlyList<double> editorCameraDepths,
        uint rawWord1,
        out int bucket,
        out int rawGteDepthSum)
    {
        bucket = 0;
        rawGteDepthSum = 0;
        if (!TryQuantizeDepthSum(editorCameraDepths, scale: 1.0, out rawGteDepthSum))
            return false;

        int candidate = (rawGteDepthSum >> 5) + (int)(rawWord1 & 0xF8u) + LowPolyBaseBucketOffset;
        if (!IsValidBucket(candidate))
            return false;
        bucket = candidate;
        return true;
    }

    public static bool IsValidBucket(int bucket) =>
        bucket is >= MinimumBucket and <= MaximumBucket;

    private static bool TryQuantizeDepthSum(
        IReadOnlyList<double>? editorCameraDepths,
        double scale,
        out int rawGteDepthSum)
    {
        rawGteDepthSum = 0;
        if (editorCameraDepths == null || editorCameraDepths.Count != 4)
            return false;

        int sum = 0;
        foreach (double editorDepth in editorCameraDepths)
        {
            if (!double.IsFinite(editorDepth))
                return false;
            double scaled = Math.Floor(editorDepth * scale);
            if (scaled <= 0 || scaled > MaximumGteDepth)
                return false;
            sum = checked(sum + (int)scaled);
        }

        rawGteDepthSum = sum;
        return true;
    }
}

/// <summary>
/// Coarse terrain packet creation phases proved by the retail renderer. Values
/// intentionally encode their stable FIFO flattening order.
/// </summary>
public enum NativeTerrainStaticPassPhase
{
    LowPoly = 0,
    HighPolyBase = 1,
    NormalHighQuality = 2,
    CloseHighQuality = 3
}

/// <summary>
/// Stages bounded raster primitives in retail coarse pass order while retaining
/// append order inside each phase. The resulting explicit sequences are then
/// consumed FIFO within descending OT buckets by the bounded rasterizer.
/// </summary>
public sealed class NativeTerrainPhasedCommandBuilder
{
    private readonly List<PendingCommand>[] _phases =
        Enumerable.Range(0, Enum.GetValues<NativeTerrainStaticPassPhase>().Length)
            .Select(_ => new List<PendingCommand>())
            .ToArray();

    public int Count => _phases.Sum(phase => phase.Count);

    public void Add(
        NativeTerrainStaticPassPhase phase,
        int otBucket,
        PsxTerrainRasterPrimitive primitive)
    {
        int phaseIndex = (int)phase;
        if ((uint)phaseIndex >= (uint)_phases.Length || !Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown native terrain pass phase.");
        if (!NativeTerrainOrderingTableContract.IsValidBucket(otBucket))
        {
            throw new ArgumentOutOfRangeException(
                nameof(otBucket),
                otBucket,
                $"Native terrain OT bucket must be in 0..0x{NativeTerrainOrderingTableContract.MaximumBucket:X}.");
        }

        _phases[phaseIndex].Add(new PendingCommand(
            otBucket,
            primitive ?? throw new ArgumentNullException(nameof(primitive))));
    }

    public List<PsxTerrainBoundedRenderCommand> Build()
    {
        List<PsxTerrainBoundedRenderCommand> commands = new(Count);
        int sequence = 0;
        foreach (List<PendingCommand> phase in _phases)
        {
            foreach (PendingCommand pending in phase)
            {
                commands.Add(new PsxTerrainBoundedRenderCommand(
                    pending.OtBucket,
                    sequence++,
                    pending.Primitive));
            }
        }
        return commands;
    }

    private sealed record PendingCommand(int OtBucket, PsxTerrainRasterPrimitive Primitive);
}
