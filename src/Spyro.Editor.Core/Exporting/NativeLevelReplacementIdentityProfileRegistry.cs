using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// A code-owned runtime profile for a derived identity layer over an already-proven
/// level-replacement BIN. It is intentionally separate from the unique donor-route
/// registry: the underlying Stone Hill &lt;- Town Square payload profile remains the
/// rollback/control and save ownership remains slot 11.
/// </summary>
public sealed record NativeLevelReplacementIdentityProfile(
    string Id,
    int RecipeVersion,
    string BaseProfileId,
    string BaseOutputImageSha256,
    string BaseExecutableSha256,
    string OutputExecutableSha256,
    string OutputImageSha256,
    long NamePointerTableFileOffset,
    long TargetNamePointerFileOffset,
    long DonorNamePointerFileOffset,
    string TargetNamePointerBeforeHex,
    string TargetNamePointerAfterHex,
    StoneHillTownSquareIdentityTotals TargetTotals,
    StoneHillTownSquareIdentityTotals DonorTotals,
    long ExpectedLogicalChangedBytes,
    long ExpectedPhysicalChangedBytes,
    int ExpectedRebuiltRawSectorCount,
    NativeLevelReplacementEvidenceStatus Evidence,
    string EvidenceId,
    string EvidenceSummary);

public static class NativeLevelReplacementIdentityProfileRegistry
{
    public const string TownSquareDisplayIdentityProfileId =
        "native-level-replacement-stonehill-townsquare-display-identity-clean-usa-v1";
    public const string TownSquareDisplayIdentityOutputImageSha256 =
        "71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9";
    public const string TownSquareDisplayIdentityEvidenceId =
        "stonehill-slot-townsquare-display-identity-duckstation-2026-08-01";

    private static readonly NativeLevelReplacementIdentityProfile CheckedProfile = new(
        Id: TownSquareDisplayIdentityProfileId,
        RecipeVersion: 1,
        BaseProfileId: NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
        BaseOutputImageSha256:
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256,
        BaseExecutableSha256:
            "558d4f5f0f7dd482b035d5f5793bfc6cf886d9cdd218562f4cedd4b1effbfab9",
        OutputExecutableSha256:
            "b17fd7679d586562ef325813b7c469aff58430f5200633f64b8e6900409e3304",
        OutputImageSha256: TownSquareDisplayIdentityOutputImageSha256,
        NamePointerTableFileOffset: 0x5FFF0,
        TargetNamePointerFileOffset: 0x5FFF4,
        DonorNamePointerFileOffset: 0x5FFFC,
        TargetNamePointerBeforeHex: "FC010180",
        TargetNamePointerAfterHex: "E4010180",
        TargetTotals: new(200, 4, 1),
        DonorTotals: new(200, 4, 1),
        ExpectedLogicalChangedBytes: 1,
        ExpectedPhysicalChangedBytes: 31,
        ExpectedRebuiltRawSectorCount: 1,
        Evidence: NativeLevelReplacementEvidenceStatus.RuntimeProven,
        EvidenceId: TownSquareDisplayIdentityEvidenceId,
        EvidenceSummary:
            "The exact display-identity candidate at BIN SHA-256 " +
            "71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9 " +
            "passed its complete DuckStation checklist in an interactive user report on " +
            "2026-08-01. The former Stone Hill portal, fly-in, guidebook, and Inventory " +
            "displayed Town Square; the 200-gem, four-dragon, and one-egg counters, gameplay, " +
            "death/reload, Return Home, re-entry, save/reload, and original Town Square slot " +
            "worked as listed. Stone Hill music and the known transition behavior remained " +
            "unchanged as expected.");

    static NativeLevelReplacementIdentityProfileRegistry()
    {
        NativeLevelReplacementProfile baseProfile =
            NativeLevelReplacementProfileRegistry.Find(CheckedProfile.BaseProfileId)
            ?? throw new InvalidDataException(
                "The display-identity profile's runtime-proven base profile is missing.");
        if (CheckedProfile.Id != TownSquareDisplayIdentityProfileId ||
            CheckedProfile.RecipeVersion != 1 ||
            CheckedProfile.Evidence != NativeLevelReplacementEvidenceStatus.RuntimeProven ||
            string.IsNullOrWhiteSpace(CheckedProfile.EvidenceId) ||
            CheckedProfile.EvidenceId != TownSquareDisplayIdentityEvidenceId ||
            !NativeLevelReplacementProfile.ShaEquals(
                CheckedProfile.BaseOutputImageSha256,
                baseProfile.OutputImageSha256) ||
            !NativeLevelReplacementProfile.ShaEquals(
                CheckedProfile.BaseExecutableSha256,
                baseProfile.OutputExecutableSha256) ||
            !NativeLevelReplacementProfile.ShaEquals(
                CheckedProfile.OutputImageSha256,
                TownSquareDisplayIdentityOutputImageSha256) ||
            CheckedProfile.NamePointerTableFileOffset != 0x5FFF0 ||
            CheckedProfile.TargetNamePointerFileOffset != 0x5FFF4 ||
            CheckedProfile.DonorNamePointerFileOffset != 0x5FFFC ||
            CheckedProfile.TargetNamePointerBeforeHex != "FC010180" ||
            CheckedProfile.TargetNamePointerAfterHex != "E4010180" ||
            CheckedProfile.TargetTotals != new StoneHillTownSquareIdentityTotals(200, 4, 1) ||
            CheckedProfile.DonorTotals != new StoneHillTownSquareIdentityTotals(200, 4, 1) ||
            CheckedProfile.ExpectedLogicalChangedBytes != 1 ||
            CheckedProfile.ExpectedPhysicalChangedBytes != 31 ||
            CheckedProfile.ExpectedRebuiltRawSectorCount != 1)
        {
            throw new InvalidDataException(
                "The code-owned Town Square display-identity runtime profile is malformed.");
        }

        string[] hashes =
        [
            CheckedProfile.BaseOutputImageSha256,
            CheckedProfile.BaseExecutableSha256,
            CheckedProfile.OutputExecutableSha256,
            CheckedProfile.OutputImageSha256
        ];
        if (hashes.Any(hash => !NativeLevelReplacementProfile.IsSha256(hash)))
            throw new InvalidDataException("The display-identity profile contains an invalid SHA-256.");
        _ = Convert.FromHexString(CheckedProfile.TargetNamePointerBeforeHex);
        _ = Convert.FromHexString(CheckedProfile.TargetNamePointerAfterHex);
    }

    public static NativeLevelReplacementIdentityProfile RequireRuntimeProven(string profileId)
    {
        if (!string.Equals(
                (profileId ?? "").Trim(),
                CheckedProfile.Id,
                StringComparison.OrdinalIgnoreCase) ||
            CheckedProfile.Evidence != NativeLevelReplacementEvidenceStatus.RuntimeProven)
        {
            throw new InvalidDataException(
                $"Identity profile '{profileId}' is not the checked runtime-proven profile.");
        }
        return CheckedProfile;
    }
}
