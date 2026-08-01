using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeLevelReplacementWadPayloadFingerprint(
    int DirectoryIndex,
    long WadOffset,
    int ByteLength,
    uint FirstWord,
    string Sha256);

/// <summary>
/// A code-owned level-replacement recipe. Runtime evidence belongs here rather than in the
/// editable baseline manifest or intent document, so changing JSON cannot promote a recipe.
/// </summary>
public sealed record NativeLevelReplacementProfile(
    string Id,
    int RecipeVersion,
    string TargetLevelKey,
    int TargetLevelId,
    string DonorLevelKey,
    int DonorLevelId,
    string SourceImageSha256,
    string SourceWadHeaderSha256,
    string SourceExecutableSha256,
    NativeLevelReplacementWadPayloadFingerprint TargetOverlay,
    NativeLevelReplacementWadPayloadFingerprint TargetData,
    NativeLevelReplacementWadPayloadFingerprint DonorOverlay,
    NativeLevelReplacementWadPayloadFingerprint DonorData,
    string OutputTargetOverlaySha256,
    string OutputTargetDataSha256,
    string OutputWadHeaderSha256,
    string OutputExecutableSha256,
    string OutputImageSha256,
    NativeLevelReplacementEvidenceStatus Evidence,
    string EvidenceId,
    string EvidenceSummary)
{
    public string NormalizedTargetLevelKey => LevelCatalog.NormalizeKey(TargetLevelKey);
    public string NormalizedDonorLevelKey => LevelCatalog.NormalizeKey(DonorLevelKey);

    public bool MatchesBaseline(NativeLevelReplacementManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Evidence == NativeLevelReplacementEvidenceStatus.RuntimeProven &&
               RecipeVersion > 0 &&
               string.Equals(
                   NormalizedTargetLevelKey,
                   LevelCatalog.NormalizeKey(manifest.Slot.TargetLevelKey),
                   StringComparison.Ordinal) &&
               manifest.Slot.LevelId == TargetLevelId &&
               ShaEquals(SourceImageSha256, manifest.Source.SourceImageSha256) &&
               ShaEquals(SourceWadHeaderSha256, manifest.Source.WadArchiveHeaderSha256) &&
               ShaEquals(SourceExecutableSha256, manifest.Source.Executable.Sha256) &&
               Matches(TargetOverlay, manifest.Source.LoadedDataPredecessorEntry) &&
               Matches(TargetData, manifest.Source.LevelDataEntry);
    }

    private static bool Matches(
        NativeLevelReplacementWadPayloadFingerprint expected,
        NativeWadEntryPreimage actual) =>
        expected.DirectoryIndex == actual.DirectoryIndex &&
        expected.WadOffset == actual.WadOffset &&
        expected.ByteLength == actual.ByteLength &&
        expected.FirstWord == actual.FirstWord &&
        ShaEquals(expected.Sha256, actual.Sha256);

    internal static bool ShaEquals(string? left, string? right) =>
        string.Equals(NormalizeSha256(left), NormalizeSha256(right), StringComparison.Ordinal);

    internal static string NormalizeSha256(string? value) =>
        (value ?? "").Trim().ToLowerInvariant();

    internal static bool IsSha256(string? value)
    {
        string normalized = NormalizeSha256(value);
        return normalized.Length == 64 &&
               normalized.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}

/// <summary>
/// V5 research profiles that have both exact static readback and a completed DuckStation checklist.
/// This registry is intentionally not consumed by normal V4 Create BIN.
/// </summary>
public static class NativeLevelReplacementProfileRegistry
{
    public const string TownSquareIntoStoneHillProfileId =
        "native-level-replacement-stonehill-townsquare-clean-usa-v1";

    public const string CleanUsaImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

    public const string TownSquareIntoStoneHillOutputImageSha256 =
        "5c23ad350edc6dcdab2c144d9d07a8d8ea98dda9b35c151d0335dd58a636c93d";

    private static readonly IReadOnlyList<NativeLevelReplacementProfile> CheckedProfiles =
        Array.AsReadOnly<NativeLevelReplacementProfile>(
    [
        new(
            Id: TownSquareIntoStoneHillProfileId,
            RecipeVersion: 1,
            TargetLevelKey: "stonehill",
            TargetLevelId: 11,
            DonorLevelKey: "townsquare",
            DonorLevelId: 13,
            SourceImageSha256: CleanUsaImageSha256,
            SourceWadHeaderSha256:
                "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd",
            SourceExecutableSha256:
                "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998",
            TargetOverlay: new(
                DirectoryIndex: 11,
                WadOffset: 0xB83800,
                ByteLength: 0x10000,
                FirstWord: 12,
                Sha256: "876c0145649bb5b26921858d4d677468463974901dcc416dcba5ccea25caa069"),
            TargetData: new(
                DirectoryIndex: 12,
                WadOffset: 0xB93800,
                ByteLength: 0x362800,
                FirstWord: 0x800,
                Sha256: "c341a3a10a67590c69d23547f6ad147f05f0b6fb7d0fb1360d572793276e796b"),
            DonorOverlay: new(
                DirectoryIndex: 15,
                WadOffset: 0x118E800,
                ByteLength: 0xF800,
                FirstWord: 14,
                Sha256: "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"),
            DonorData: new(
                DirectoryIndex: 16,
                WadOffset: 0x119E000,
                ByteLength: 0x2E2000,
                FirstWord: 0x800,
                Sha256: "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"),
            OutputTargetOverlaySha256:
                "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
            OutputTargetDataSha256:
                "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
            OutputWadHeaderSha256:
                "dcce03cf0af4be68ef5015b708dc0cad34bc18210107b9b0f21647a5bd545702",
            OutputExecutableSha256:
                "558d4f5f0f7dd482b035d5f5793bfc6cf886d9cdd218562f4cedd4b1effbfab9",
            OutputImageSha256: TownSquareIntoStoneHillOutputImageSha256,
            Evidence: NativeLevelReplacementEvidenceStatus.RuntimeProven,
            EvidenceId:
                "stonehill-slot-townsquare-complete-pair-duckstation-2026-08-01",
            EvidenceSummary:
                "The exact clean-USA complete-pair candidate passed static logical/readback and raw-sector integrity at output SHA-256 5c23ad350edc6dcdab2c144d9d07a8d8ea98dda9b35c151d0335dd58a636c93d. The complete DuckStation checklist passed: the user entered Town Square through Stone Hill, exercised movement and combat, collected gems, rescued dragons, collected the egg thief, used pause and Inventory, died/reloaded, returned home, re-entered, saved/reloaded, and observed the safety-rerouted Doctor Shemp title demo without a freeze or crash.")
    ]);

    static NativeLevelReplacementProfileRegistry()
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> routes = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> evidenceIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (NativeLevelReplacementProfile profile in CheckedProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id.Trim()))
                throw new InvalidDataException("Native level-replacement profile ids must be unique and non-empty.");
            if (profile.RecipeVersion <= 0 || profile.TargetLevelId <= 0 || profile.DonorLevelId <= 0 ||
                string.IsNullOrWhiteSpace(profile.NormalizedTargetLevelKey) ||
                string.IsNullOrWhiteSpace(profile.NormalizedDonorLevelKey) ||
                string.Equals(profile.NormalizedTargetLevelKey, profile.NormalizedDonorLevelKey, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Native level-replacement profile '{profile.Id}' has an invalid route.");
            }
            if (!routes.Add($"{profile.NormalizedTargetLevelKey}<-{profile.NormalizedDonorLevelKey}"))
                throw new InvalidDataException("Native level-replacement profile routes must be unique.");
            if (string.IsNullOrWhiteSpace(profile.EvidenceId) || !evidenceIds.Add(profile.EvidenceId.Trim()))
                throw new InvalidDataException($"Native level-replacement profile '{profile.Id}' has invalid evidence identity.");
            if (profile.Evidence != NativeLevelReplacementEvidenceStatus.RuntimeProven)
                throw new InvalidDataException($"Native level-replacement profile '{profile.Id}' is not runtime-proven.");

            string[] hashes =
            [
                profile.SourceImageSha256,
                profile.SourceWadHeaderSha256,
                profile.SourceExecutableSha256,
                profile.TargetOverlay.Sha256,
                profile.TargetData.Sha256,
                profile.DonorOverlay.Sha256,
                profile.DonorData.Sha256,
                profile.OutputTargetOverlaySha256,
                profile.OutputTargetDataSha256,
                profile.OutputWadHeaderSha256,
                profile.OutputExecutableSha256,
                profile.OutputImageSha256
            ];
            if (hashes.Any(hash => !NativeLevelReplacementProfile.IsSha256(hash)))
                throw new InvalidDataException($"Native level-replacement profile '{profile.Id}' has an invalid SHA-256.");
            ValidatePayload(profile.Id, profile.TargetOverlay);
            ValidatePayload(profile.Id, profile.TargetData);
            ValidatePayload(profile.Id, profile.DonorOverlay);
            ValidatePayload(profile.Id, profile.DonorData);
            if (!NativeLevelReplacementProfile.ShaEquals(
                    profile.OutputTargetOverlaySha256,
                    profile.DonorOverlay.Sha256) ||
                !NativeLevelReplacementProfile.ShaEquals(
                    profile.OutputTargetDataSha256,
                    profile.DonorData.Sha256))
            {
                throw new InvalidDataException(
                    $"Native level-replacement profile '{profile.Id}' does not install its exact donor pair.");
            }
        }
    }

    public static IReadOnlyList<NativeLevelReplacementProfile> Profiles => CheckedProfiles;

    public static NativeLevelReplacementProfile? Find(string profileId)
    {
        string id = (profileId ?? "").Trim();
        return CheckedProfiles.SingleOrDefault(profile =>
            string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static NativeLevelReplacementProfile RequireRuntimeProven(
        string profileId,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(catalog);
        NativeLevelReplacementStore.Validate(manifest, catalog);
        NativeLevelReplacementProfile profile = Find(profileId)
            ?? throw new InvalidDataException(
                $"Native level-replacement profile '{profileId}' is not in the checked V5 registry.");
        if (profile.Evidence != NativeLevelReplacementEvidenceStatus.RuntimeProven ||
            !profile.MatchesBaseline(manifest))
        {
            throw new InvalidDataException(
                $"Native level-replacement profile '{profile.Id}' does not authorize this exact retail baseline.");
        }

        LevelDefinition donor = catalog.FindByKey(profile.NormalizedDonorLevelKey)
            ?? throw new InvalidDataException(
                $"The donor '{profile.DonorLevelKey}' is missing from the retail level catalog.");
        if (donor.LevelId != profile.DonorLevelId || donor.SourceWadEntry != profile.DonorData.DirectoryIndex)
            throw new InvalidDataException($"The donor catalog contract for profile '{profile.Id}' changed.");
        return profile;
    }

    private static void ValidatePayload(
        string profileId,
        NativeLevelReplacementWadPayloadFingerprint payload)
    {
        if (payload.DirectoryIndex < 0 || payload.WadOffset < 0 || payload.ByteLength <= 0 ||
            !NativeLevelReplacementProfile.IsSha256(payload.Sha256))
        {
            throw new InvalidDataException(
                $"Native level-replacement profile '{profileId}' has an invalid WAD payload fingerprint.");
        }
    }
}
