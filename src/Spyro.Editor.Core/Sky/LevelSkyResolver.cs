using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.Core.Sky;

/// <summary>The sky the game shows in a level of this project, as a native sky block: a level you build uses its imported
/// sky or the level sky it borrows (none: its plain colour); a game level uses its saved sky edit (recoloured, another
/// level's, a ready-made one or imported) applied to its original sky, or the original.</summary>
public static class LevelSkyResolver
{
    public static byte[]? Resolve(string sourceDisc, string workspacePath, LevelCatalog catalog, LevelDefinition level, SceneDocument? scene)
    {
        if (scene != null)
            return scene.CustomSky?.ToArray() ?? (scene.SkySourceWadEntry is int entry ? Original(sourceDisc, entry) : null);
        if (level.SourceWadEntry is < 10 or > 78) return null;
        NativeSkyEditPlan? plan = NativeSkyEditStore.Load(workspacePath, level);
        if (plan == null) return Original(sourceDisc, level.SourceWadEntry);
        if (plan.IsImport)
        {
            string path = Path.IsPathRooted(plan.ImportedSkyPath) ? plan.ImportedSkyPath : Path.Combine(workspacePath, plan.ImportedSkyPath);
            return File.Exists(path) ? Spyro1SkyBlockAnalyzer.NormalizeStandaloneSkyFile(File.ReadAllBytes(path)) : Original(sourceDisc, level.SourceWadEntry);
        }
        byte[]? geometry = plan.IsSwap || plan.IsOriginalPreset
            ? catalog.FindByKey(plan.DonorLevelKey) is { } donor ? Original(sourceDisc, donor.SourceWadEntry) : null
            : Original(sourceDisc, level.SourceWadEntry);
        if (geometry == null) return Original(sourceDisc, level.SourceWadEntry);
        return plan.IsPalette || plan.IsOriginalPreset ? NativeSkyPatchExporter.RecolorStandalone(geometry, plan) : geometry;
    }

    /// <summary>A level's own sky on the original disc (the first sky block of its model data), or null.</summary>
    public static byte[]? Original(string sourceDisc, int wadEntry) =>
        NativeAuthoredMesh.CaptureSky(sourceDisc, SceneDocument.New("sky") with { SkySourceWadEntry = wadEntry });
}
