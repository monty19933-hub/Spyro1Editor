namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Named, deterministic bridge from the editor's game-facing Fly camera to
/// the explicit fixed-point input consumed by <see cref="SpyroNativeTerrainCamera"/>.
/// The result reproduces native register arithmetic for the quantized editor
/// camera; it is not provenance for a camera captured from retail gameplay.
/// </summary>
public static class SpyroEditorDerivedTerrainCamera
{
    public const string Contract = "spyro-editor-fly-camera-to-fixed16-4096turn-v1";
    public const string PositionQuantization = "nearest-away-from-zero-times16-v1";
    public const string RotationQuantization = "nearest-away-from-zero-wrap-signed12-v1";

    private const double AngleUnitsPerRadian =
        SpyroNativeTerrainCamera.AngleUnitsPerTurn / (Math.PI * 2.0);

    /// <summary>
    /// Quantizes the current Fly camera. Only the game-view Y orientation is
    /// supported because the unflipped editor view is a reflection, not a
    /// native camera rotation.
    /// </summary>
    public static bool TryCreate(
        double flyX,
        double flyY,
        double flyZ,
        double flyYawRadians,
        double flyPitchRadians,
        bool gameViewYFlipped,
        out SpyroRetailTerrainCameraInput input)
    {
        input = default;
        if (!gameViewYFlipped ||
            !double.IsFinite(flyX) ||
            !double.IsFinite(flyY) ||
            !double.IsFinite(flyZ) ||
            !double.IsFinite(flyYawRadians) ||
            !double.IsFinite(flyPitchRadians) ||
            !TryQuantizePosition(flyX, out int nativeX) ||
            !TryQuantizePosition(-flyY, out int nativeY) ||
            !TryQuantizePosition(flyZ, out int nativeZ) ||
            !TryQuantizeRotation(-flyPitchRadians, out short nativeYRotation) ||
            !TryQuantizeRotation(-flyYawRadians, out short nativeZRotation))
        {
            return false;
        }

        input = new SpyroRetailTerrainCameraInput(
            new SpyroRetailFixed16Position(nativeX, nativeY, nativeZ),
            new SpyroRetailRotation4096(0, nativeYRotation, nativeZRotation),
            SpyroRetailCameraInputCertification
                .EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation);
        return true;
    }

    private static bool TryQuantizePosition(double editorWorldCoordinate, out int fixed16)
    {
        double scaled = editorWorldCoordinate * SpyroNativeTerrainCamera.CameraPositionScale;
        if (!double.IsFinite(scaled) || scaled < int.MinValue || scaled > int.MaxValue)
        {
            fixed16 = 0;
            return false;
        }

        double rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
        if (rounded < int.MinValue || rounded > int.MaxValue)
        {
            fixed16 = 0;
            return false;
        }

        fixed16 = checked((int)rounded);
        return true;
    }

    private static bool TryQuantizeRotation(double radians, out short signedAngle)
    {
        double scaled = radians * AngleUnitsPerRadian;
        if (!double.IsFinite(scaled) || scaled < int.MinValue || scaled > int.MaxValue)
        {
            signedAngle = 0;
            return false;
        }

        int rounded = checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero));
        int wrapped = rounded & 0x0FFF;
        if (wrapped >= 0x0800)
            wrapped -= 0x1000;
        signedAngle = checked((short)wrapped);
        return true;
    }
}
