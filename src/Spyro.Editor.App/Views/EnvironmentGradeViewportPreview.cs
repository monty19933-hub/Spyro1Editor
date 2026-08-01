using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Editor-only environment preview. Native texture pixels stay unchanged: their
/// visible colors guide the composite PS1 match, which is then represented by
/// the same safe scene/vertex transform used by export.
/// </summary>
internal sealed record EnvironmentGradeViewportPreview(
    NativeEnvironmentColorTransform SceneTransform,
    bool IncludesNativeTextureColors)
{
    public static EnvironmentGradeViewportPreview Build(
        NativeEnvironmentGradeMatch match,
        NativeEnvironmentGradePlan grade)
    {
        ArgumentNullException.ThrowIfNull(match);
        NativeEnvironmentGradePlan normalized = grade.Normalize(match.DonorLevelKey);
        NativeEnvironmentColorTransform transform =
            NativeEnvironmentGradeExporter.BuildPreviewSceneTransform(match, normalized);
        return new EnvironmentGradeViewportPreview(
            transform,
            normalized.GradeTexturePalettes);
    }

    public ColorRgba TransformSceneColor(ColorRgba source) =>
        SceneTransform.ApplyTerrainSmoothing(source);
}
