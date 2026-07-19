namespace Spyro.Editor.Core.Scene;

public readonly record struct NativeTerrainLqPreviewSelection(
    bool HighPolyVisible,
    bool HqOverlayEligible,
    int DescriptorIndex,
    int PaletteRow,
    int RawHpDepthSum,
    bool LqFadeBypass);

/// <summary>
/// Static retail terrain-distance rules. This deliberately does not claim
/// camera occlusion-group, ordering-table, clipping, or animation equivalence.
/// </summary>
public static class NativeTerrainFarLod
{
    public const int LodDistanceFixed16 = 0x8000;
    public const double LodDistanceEditorUnits = LodDistanceFixed16 / 16.0;
    public const double SectorQueueOverlapEditorUnits = 0x100;
    public const int HqOverlayDepthSum = 0x2000;
    public const double HqOverlayPlanarDepthEditorUnits = HqOverlayDepthSum / 16.0;
    public const int ConservativeAllSectorCullingDistanceFixed16 = 0x28000;
    public const int NoOcclusionGameplayCullingDistanceFixed16 = 0x1C000;

    public static bool ShouldQueueHighDetailSector(double centerCameraDepth, double radius, bool disableHighDetail) =>
        !disableHighDetail && centerCameraDepth - Math.Max(0, radius) < LodDistanceEditorUnits;

    public static bool ShouldQueueLowDetailSector(double centerCameraDepth, double radius, bool disableLowDetail, bool forceLowDetail) =>
        !disableLowDetail &&
        (forceLowDetail || centerCameraDepth + Math.Max(0, radius) + SectorQueueOverlapEditorUnits > LodDistanceEditorUnits);

    public static bool ShouldRenderHighDetailFace(IReadOnlyList<double> editorCameraDepths) =>
        RawHpDepthSum(editorCameraDepths) < LodDistanceFixed16;

    public static bool ShouldRenderLowDetailFace(
        IReadOnlyList<double> editorCameraDepths,
        int transitionBias,
        int cullingDistanceFixed16 = ConservativeAllSectorCullingDistanceFixed16)
    {
        if (!TryGetLowDetailAverageBucket(
                editorCameraDepths,
                cullingDistanceFixed16,
                out int averageBucket,
                out int[] depths))
        {
            return false;
        }

        int clampedBias = transitionBias & 0x1F;
        int nearBucket = (LodDistanceFixed16 >> 7) - 0x20 - (clampedBias << 3);
        if (averageBucket >= nearBucket)
            return true;
        if (averageBucket + 0x20 <= nearBucket)
            return false;

        int cornerThreshold = nearBucket << 3;
        return depths.Any(depth => depth >= cornerThreshold);
    }

    public static NativeTerrainLqPreviewSelection SelectHighDetailTexturePath(
        IReadOnlyList<double> editorCameraDepths,
        bool lqFadeBypass,
        bool hqOverlayBypass)
    {
        int rawDepthSum = RawHpDepthSum(editorCameraDepths);
        bool highVisible = rawDepthSum < LodDistanceFixed16;
        bool hqEligible = highVisible && !hqOverlayBypass && rawDepthSum < HqOverlayDepthSum;
        int threshold = (LodDistanceFixed16 >> 2) + (lqFadeBypass ? 0x2000 : 0);
        int palettePhase = ((rawDepthSum >> 7) << 5) - threshold + 0x1000;
        int descriptor = palettePhase > 0 ? 0 : 1;
        int paletteRow = palettePhase > 0 ? Math.Clamp(palettePhase >> 8, 0, 15) : 0;
        return new NativeTerrainLqPreviewSelection(
            highVisible,
            hqEligible,
            descriptor,
            paletteRow,
            rawDepthSum,
            lqFadeBypass);
    }

    private static int RawHpDepthSum(IReadOnlyList<double> editorCameraDepths)
    {
        if (editorCameraDepths.Count != SourceSceneOverlayContract.CornerSlotCount)
            return int.MaxValue;

        long sum = 0;
        foreach (double depth in editorCameraDepths)
        {
            if (!double.IsFinite(depth))
                return int.MaxValue;
            sum += (int)Math.Floor(depth * 4.0);
        }

        return sum > int.MaxValue ? int.MaxValue : sum < int.MinValue ? int.MinValue : (int)sum;
    }

    private static bool TryGetLowDetailAverageBucket(
        IReadOnlyList<double> editorCameraDepths,
        int cullingDistanceFixed16,
        out int averageBucket,
        out int[] depths)
    {
        averageBucket = int.MaxValue;
        depths = Array.Empty<int>();
        if (editorCameraDepths.Count != SourceSceneOverlayContract.CornerSlotCount)
            return false;

        depths = editorCameraDepths
            .Select(depth => double.IsFinite(depth) ? (int)Math.Floor(depth) : int.MinValue)
            .ToArray();
        if (depths.Any(depth => depth <= 0))
            return false;

        averageBucket = depths.Sum() >> 5;
        return averageBucket < (cullingDistanceFixed16 >> 7);
    }
}
