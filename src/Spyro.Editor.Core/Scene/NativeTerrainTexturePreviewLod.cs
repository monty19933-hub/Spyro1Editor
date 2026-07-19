namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Selects the native high-detail terrain texture descriptor bank used by the
/// Fly 3D preview. The retail renderer compares each projected high-poly face
/// corner against GTE SZ 0x140. Native high-poly coordinates are four times
/// the editor's world-coordinate scale, so that cutoff is 80 editor units.
/// </summary>
public static class NativeTerrainTexturePreviewLod
{
    public const int NativeCloseDepth = 0x140;
    public const double NativeDepthUnitsPerEditorUnit = 4.0;
    public const double CloseDepthEditorUnits = NativeCloseDepth / NativeDepthUnitsPerEditorUnit;

    public static NativeTerrainTexturePreviewTier SelectForMinimumCameraDepth(double minimumCameraDepth) =>
        double.IsFinite(minimumCameraDepth) &&
        minimumCameraDepth >= 0 &&
        minimumCameraDepth < CloseDepthEditorUnits
            ? NativeTerrainTexturePreviewTier.Close
            : NativeTerrainTexturePreviewTier.Normal;

    public static NativeTerrainTexturePreviewTier FromDescriptorTier(string descriptorTier) =>
        string.Equals(descriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase)
            ? NativeTerrainTexturePreviewTier.Close
            : NativeTerrainTexturePreviewTier.Normal;

    public static string FileSuffix(NativeTerrainTexturePreviewTier tier) =>
        tier == NativeTerrainTexturePreviewTier.Close ? "close" : "normal";
}

public enum NativeTerrainTexturePreviewTier
{
    Normal,
    Close
}
