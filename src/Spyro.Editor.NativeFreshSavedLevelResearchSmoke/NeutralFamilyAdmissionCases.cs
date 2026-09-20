using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

// Persisting schema5 must not widen any existing saved/native admission arm.
internal static class NeutralFamilyAdmissionCases
{
    internal static async Task Free()
    {
        var old = FamilySavedCases.Document();
        var source = NativeFreshLockedChestFamilyReference.Create(
            NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,
            NativeFreshLockedChestFamilyReference.SupportedDefinitionId);
        var family = NativeFreshLockedChestFamilyPlacementIntent.Create(
            "33333333333333333333333333333333", source, 9728, 9120, 8704, 10000, 9800, 8704,
            NativeFreshChestRewardDestinationOverrides.Create([
                new(NativeFreshChestRewardRole.Reward2, 0, 262128, 17)]));
        int count = 0;
        foreach (bool tiled in new[] { false, true })
        {
            var modern = old.UpgradeForFamilies();
            if (tiled) modern = modern.UpgradeToTiled("44444444444444444444444444444444");
            foreach (var rows in new NativeFreshPlacementIntent[][]
            {
                [], old.Placements.ToArray(), [family], [.. old.Placements, family]
            })
            {
                var intent = modern.WithPlacements(rows);
                Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedMaterialProfile(intent) &&
                    !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(intent) &&
                    !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(intent) &&
                    !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(intent) &&
                    !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(intent),
                    "schema5 empty/old-only/family authorship is not any old native admission profile");
                byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(intent);
                var (project, saved) = Save("neutral-family-admission-" + count++, intent);
                var error = await Throws<InvalidDataException>(() =>
                    NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
                Check(!Leaves(error).Any(e => e is FileNotFoundException or DirectoryNotFoundException) &&
                    !Leaves(error).Any(e => e.Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal)),
                    "existing candidate factory refuses whole schema5 before nonexistent source access or placement projection");
                Check(NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentStore.Capture(project).Intent!)
                    .SequenceEqual(canonical), "refused compilation retains every saved family/content field unchanged");
            }
        }
        Check(count == 8 && NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(old),
            "eight schema5 negative combinations preserve the existing schema3 family positive profile");
    }
}
