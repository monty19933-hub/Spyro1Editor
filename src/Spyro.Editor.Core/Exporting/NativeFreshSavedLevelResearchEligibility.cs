using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral UI preflight for the closed mixed, family and tiled saved-level research arms.
/// This checks document shape and bounded canonical data only: no I/O, saved revision, source authority,
/// native ownership, runtime proof or publication approval is issued.
/// </summary>
public static class NativeFreshSavedLevelResearchEligibility
{
    public static bool IsSupportedTiledProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            // Same WHOLE-document neutral preflight as the saved candidate issuer.
            // Tile count or selected material alone cannot bound the native tree.
            var (_, surface) = NativeId65TiledSurfaceBundle.PrepareDocument(document);
            NativeId65TiledNormalDataLayout.RequireFixedSurfaceAllocation(surface.CopyBundle().Length);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsSupportedTiledMaterialProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            // Exactly two distinct selections across the actual whole document,
            // including both complete records in the measured data allocation.
            // This does not authenticate either selection as a static source.
            _ = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsSupportedFamilyProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            _ = NativeFreshSavedMobyFamilyCompilation.ProjectBaseDocument(document);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsSupportedMixedProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            _ = NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(document);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
