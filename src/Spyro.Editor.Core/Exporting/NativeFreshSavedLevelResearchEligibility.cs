using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral UI preflight for the closed legacy, mixed, family and tiled saved research arms.
/// This checks document shape and bounded canonical data only: no I/O, saved revision, source authority,
/// native ownership, runtime proof or publication approval is issued.
/// </summary>
public static class NativeFreshSavedLevelResearchEligibility
{
    public static bool IsSupportedUntexturedProfile(NativeId65FreshDocumentIntent? document)
    {
        if (document == null) return false;
        try { RequireHistoricalFloorBounds(document); return true; }
        catch (InvalidDataException) { return false; }
    }

    public static bool IsSupportedMaterialProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try { _ = RequireMaterialProfile(document); return true; }
        catch (InvalidDataException) { return false; }
    }

    // Neutral single-floor authoring also serves larger internal experiments.
    // Existing public saved/export profiles retain their historical dimension
    // limit independently of that editor/encoder bound. No source claim here.
    internal static void RequireHistoricalFloorBounds(NativeId65FreshDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FloorWidth > 2048 || document.FloorDepth > 2048)
            throw new InvalidDataException("Existing public saved research profiles require floor width and depth at most 2048.");
    }

    internal static (NativeFreshRetailTerrainMaterialIntent First, NativeFreshRetailTerrainMaterialIntent Second)
        RequireMaterialProfile(NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 2 || document.FloorMaterials == null)
            throw new InvalidDataException("Saved material compilation requires schema 2.");
        if (document.Placements.Count != 0)
            throw new InvalidDataException("Saved material compilation requires zero placements; Wizard/material mixing is unavailable.");
        if (document.FloorMaterials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent first ||
            document.FloorMaterials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent second ||
            first.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant ||
            second.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant)
            throw new InvalidDataException("Saved material compilation requires two retail terrain record descriptors.");
        RequireHistoricalFloorBounds(document.Base);
        return (first, second);
    }

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
            // Measure every distinct selection across the actual whole document,
            // including every complete record in the native data allocation.
            // Source authenticity, GPU allocation and receipt fit are checked
            // later by the shared candidate issuer, not this neutral preflight.
            _ = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(document);
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

    public static bool IsSupportedLockedChestProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            // Reuse the saved issuer's whole-document policy. Eligibility is
            // neutral authorship only, not source or runtime certification.
            _ = NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(document);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsSupportedTiledFamilyProfile(NativeFreshLevelDocumentIntent? document)
    {
        if (document == null) return false;
        try
        {
            // Share the saved compiler's complete closed-profile preflight.
            // This grants UI readiness only; source, revision, lineage and
            // publication authority still come from the actual Candidate.
            _ = NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(document);
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
