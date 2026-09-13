using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral UI preflight for the existing closed mixed saved-level research arm.
/// This checks document shape only: no I/O, saved revision, source authority,
/// native ownership, runtime proof or publication approval is issued.
/// </summary>
public static class NativeFreshSavedLevelResearchEligibility
{
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
