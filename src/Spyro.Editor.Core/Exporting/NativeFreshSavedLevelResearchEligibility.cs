using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral UI preflight for the existing closed mixed and family saved-level research arms.
/// This checks document shape only: no I/O, saved revision, source authority,
/// native ownership, runtime proof or publication approval is issued.
/// </summary>
public static class NativeFreshSavedLevelResearchEligibility
{
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
