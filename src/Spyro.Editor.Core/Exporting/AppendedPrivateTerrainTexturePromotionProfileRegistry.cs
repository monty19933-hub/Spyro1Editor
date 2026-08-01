using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact retail preimage used to decide whether an appended-private terrain
/// texture writer may leave research. Image identity alone is insufficient:
/// the destination WAD row, texture-table count, texture component, and whole
/// level-data preimage must all match the profile that received runtime proof.
/// </summary>
public sealed record AppendedPrivateTerrainTextureRetailFingerprint(
    int BindingVersion,
    string SourceImageSha256,
    int TargetWadEntry,
    int ExpectedSourceTextureCount,
    string ExpectedTextureComponentSha256,
    string ExpectedLevelDataSha256)
{
    public bool Matches(NativeTerrainTextureRecordAppendSourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return BindingVersion == binding.Version &&
               TargetWadEntry == binding.TargetWadEntry &&
               ExpectedSourceTextureCount == binding.ExpectedSourceTextureCount &&
               ShaEquals(SourceImageSha256, binding.SourceImageSha256) &&
               ShaEquals(ExpectedTextureComponentSha256, binding.ExpectedTextureComponentSha256) &&
               ShaEquals(ExpectedLevelDataSha256, binding.ExpectedLevelDataSha256);
    }

    internal static bool ShaEquals(string left, string right) =>
        string.Equals(
            NormalizeSha256(left),
            NormalizeSha256(right),
            StringComparison.Ordinal);

    internal static string NormalizeSha256(string? value) =>
        (value ?? "").Trim().ToLowerInvariant();

    internal static bool IsSha256(string? value)
    {
        string normalized = NormalizeSha256(value);
        return normalized.Length == 64 &&
               normalized.All(character =>
                   character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}

/// <summary>
/// One checked promotion candidate. RuntimeProven is deliberately separate
/// from successful static planning/readback: normal Create BIN may consume
/// only an exact profile whose runtime proof has been recorded.
/// </summary>
public sealed record AppendedPrivateTerrainTexturePromotionProfile(
    string Id,
    string TargetLevelKey,
    AppendedPrivateTerrainTextureRetailFingerprint Retail,
    NativeTerrainTexturePrivateImageWriterKind WriterKind,
    NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy,
    string EvidenceId,
    string StaticProofOutputSha256,
    bool RuntimeProven,
    string RuntimeProofOutputSha256,
    int RuntimeProvenMaxAppendedRecords,
    string EvidenceSummary)
{
    public string NormalizedTargetLevelKey =>
        LevelCatalog.NormalizeKey(TargetLevelKey);

    public bool IsExactMatch(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return WriterKind == writerKind &&
               StructuralGrowthPolicy == structuralGrowthPolicy &&
               string.Equals(
                   NormalizedTargetLevelKey,
                   LevelCatalog.NormalizeKey(binding.TargetLevelKey),
                   StringComparison.OrdinalIgnoreCase) &&
               Retail.Matches(binding);
    }

    public bool IsExactMatch(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind) =>
        IsExactMatch(
            binding,
            writerKind,
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);

    public bool AllowsNormalRelease(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy) =>
        RuntimeProven &&
        AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(RuntimeProofOutputSha256) &&
        appendedRecordCount > 0 &&
        appendedRecordCount <= RuntimeProvenMaxAppendedRecords &&
        IsExactMatch(binding, writerKind, structuralGrowthPolicy);

    public bool AllowsNormalRelease(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount) =>
        AllowsNormalRelease(
            binding,
            writerKind,
            appendedRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
}

/// <summary>
/// Checked promotion profiles for appended-private terrain textures. Adding a
/// statically valid row here does not enable it: normal release authorization
/// requires RuntimeProven plus an exact retail fingerprint and writer match.
/// </summary>
public static class AppendedPrivateTerrainTexturePromotionProfileRegistry
{
    public const string CleanUsaImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

    public const string ArtisansTextureComponentSha256 =
        "23845db55d3c73f3c9cef950f31e5d10a742fde10313fa6a5389694e7da19658";

    public const string ArtisansLevelDataSha256 =
        "0a670f58d08e1f86c872caeec5eff53933615d6f85b29291113a7c6dd243a1d5";

    public const string ArtisansFixedTailStaticProofOutputSha256 =
        "884209f2d8882c57001b4236e5a05a0f7d0ec45d5f2f318eb2e871f852ce98eb";

    public const string ArtisansFixedTailRuntimeProofOutputSha256 =
        "6e1a85298d01f336c602f06d5a74152868f978b27bf04ee5824d8ede616d7de0";

    public const string ArtisansFixedTailFourRecordReadbackOutputSha256 =
        "1b8a14389901c0fc86b0be86ee2914295e070467f424221f246ea07877adb94a";

    public const string GnastysWorldTextureComponentSha256 =
        "ceda4db243e0dbed3443788ff81c4fb14c965ad8ececb10a28fe9bf9e7379903";

    public const string GnastysWorldLevelDataSha256 =
        "7a1a725800001261d7cc453ae2d97dcab8e774c6442460a87b752ed8ad2790df";

    public const string GnastysWorldFiftyRecordOutputSha256 =
        "0b08545f51fe859e47c87c7c856d4c5f58bc15ae1813c6ccbcb65ca5d4668c6f";

    public const string WizardPeakTextureComponentSha256 =
        "02ceaa5ddc54ae39f7b871731664726e8a0276faf474ce042032782e572a8d35";

    public const string WizardPeakLevelDataSha256 =
        "6b6f680b0712c206f1421d06abc6052189d48276707cabba7cdbb2468a0f33a0";

    public const string WizardPeakFiftyFaceOutputSha256 =
        "27e92eeee3b1f83bb676beb3f4f8a54a35346a3d55e6e1624177d30d542880e3";

    private static readonly IReadOnlyList<AppendedPrivateTerrainTexturePromotionProfile>
        CheckedProfiles =
        [
            new(
                Id: "terrain-private-artisans-clean-usa-fixed-tail-v1",
                TargetLevelKey: "artisans",
                Retail: new AppendedPrivateTerrainTextureRetailFingerprint(
                    BindingVersion: NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                    SourceImageSha256: CleanUsaImageSha256,
                    TargetWadEntry: 10,
                    ExpectedSourceTextureCount: 68,
                    ExpectedTextureComponentSha256: ArtisansTextureComponentSha256,
                    ExpectedLevelDataSha256: ArtisansLevelDataSha256),
                WriterKind: NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                StructuralGrowthPolicy:
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                EvidenceId:
                    "artisans-retail-fixed-tail-t68-t71-four-row-duckstation-2026-07-26",
                StaticProofOutputSha256: ArtisansFixedTailStaticProofOutputSha256,
                RuntimeProven: true,
                RuntimeProofOutputSha256: ArtisansFixedTailRuntimeProofOutputSha256,
                RuntimeProvenMaxAppendedRecords: 4,
                EvidenceSummary:
                    "The exact clean-USA Artisans fixed-tail writer first passed DuckStation with one appended dragon-eye row. The later T68-T71 build passed exact private-step readback at SHA-256 1b8a14389901c0fc86b0be86ee2914295e070467f424221f246ea07877adb94a; all four private textures were then confirmed visible in DuckStation from combined output SHA-256 6e1a85298d01f336c602f06d5a74152868f978b27bf04ee5824d8ede616d7de0 without a crash. Normal release is limited to four appended private records at a time."),
            new(
                Id: "terrain-private-gnastysworld-clean-usa-sector-fifty-v1",
                TargetLevelKey: "gnastysworld",
                Retail: new AppendedPrivateTerrainTextureRetailFingerprint(
                    BindingVersion: NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                    SourceImageSha256: CleanUsaImageSha256,
                    TargetWadEntry: 70,
                    ExpectedSourceTextureCount: 32,
                    ExpectedTextureComponentSha256:
                        GnastysWorldTextureComponentSha256,
                    ExpectedLevelDataSha256: GnastysWorldLevelDataSha256),
                WriterKind:
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                StructuralGrowthPolicy:
                    NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended,
                EvidenceId:
                    "gnastysworld-retail-sector-t32-t81-fifty-row-duckstation-2026-07-27",
                StaticProofOutputSha256:
                    GnastysWorldFiftyRecordOutputSha256,
                RuntimeProven: true,
                RuntimeProofOutputSha256:
                    GnastysWorldFiftyRecordOutputSha256,
                RuntimeProvenMaxAppendedRecords: 50,
                EvidenceSummary:
                    "The exact clean-USA Gnasty's World sector-relocation candidate installed T32-T81, assigned fifty source-bound terrain faces, grew the destination by exactly +0x2800, and passed final-BIN, immutable-source, runtime-target, and global logical readback at SHA-256 0b08545f51fe859e47c87c7c856d4c5f58bc15ae1813c6ccbcb65ca5d4668c6f. The user then confirmed the exact candidate worked in DuckStation without a crash. Normal release remains bound to this retail preimage, SectorRelocation, RuntimeProvenExtended, and at most fifty appended records."),
            new(
                Id: "terrain-private-wizardpeak-clean-usa-sector-46-appended-v1",
                TargetLevelKey: "wizardpeak",
                Retail: new AppendedPrivateTerrainTextureRetailFingerprint(
                    BindingVersion: NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                    SourceImageSha256: CleanUsaImageSha256,
                    TargetWadEntry: 40,
                    ExpectedSourceTextureCount: 82,
                    ExpectedTextureComponentSha256:
                        WizardPeakTextureComponentSha256,
                    ExpectedLevelDataSha256: WizardPeakLevelDataSha256),
                WriterKind:
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                StructuralGrowthPolicy:
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000,
                EvidenceId:
                    "wizardpeak-retail-sector-4-native-46-appended-fifty-face-duckstation-2026-07-27",
                StaticProofOutputSha256:
                    WizardPeakFiftyFaceOutputSha256,
                RuntimeProven: true,
                RuntimeProofOutputSha256:
                    WizardPeakFiftyFaceOutputSha256,
                RuntimeProvenMaxAppendedRecords: 46,
                EvidenceSummary:
                    "The exact clean-USA Wizard Peak sector-relocation candidate installed 46 appended rows alongside four artifact-only animation-source diagnostic rows, assigned all fifty candidate rows to source-bound HP terrain faces, and grew the destination by exactly +0x2000. Final-BIN, immutable-source, runtime-target, and global logical readback passed at SHA-256 27e92eeee3b1f83bb676beb3f4f8a54a35346a3d55e6e1624177d30d542880e3; the user then confirmed the corrected visible-face candidate worked in DuckStation without a crash. Normal release remains bound to this retail preimage, SectorRelocation, RuntimeProvenPlus2000, and at most 46 appended records. The normal editor deliberately excludes the four animation-source rows from unused-native allocation, so this profile does not advertise fifty normal editor slots.")
        ];

    static AppendedPrivateTerrainTexturePromotionProfileRegistry()
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> evidenceIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (AppendedPrivateTerrainTexturePromotionProfile profile in CheckedProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id.Trim()))
                throw new InvalidDataException("Appended-private promotion profile ids must be unique and non-empty.");
            if (string.IsNullOrWhiteSpace(profile.NormalizedTargetLevelKey))
                throw new InvalidDataException($"Promotion profile '{profile.Id}' has no normalized destination level.");
            if (string.IsNullOrWhiteSpace(profile.EvidenceId) ||
                !evidenceIds.Add(profile.EvidenceId.Trim()))
            {
                throw new InvalidDataException(
                    $"Promotion profile '{profile.Id}' has a missing or duplicate evidence identity.");
            }
            ValidateFingerprint(profile.Id, profile.Retail);
            _ = NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(
                profile.StructuralGrowthPolicy);
            if (!AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(
                    profile.StaticProofOutputSha256))
            {
                throw new InvalidDataException(
                    $"Promotion profile '{profile.Id}' has no valid static-proof output SHA-256.");
            }
            if (profile.RuntimeProven &&
                !AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(
                    profile.RuntimeProofOutputSha256))
            {
                throw new InvalidDataException(
                    $"Runtime-proven promotion profile '{profile.Id}' has no valid runtime-proof output SHA-256.");
            }
            if (!profile.RuntimeProven &&
                !string.IsNullOrWhiteSpace(profile.RuntimeProofOutputSha256))
            {
                throw new InvalidDataException(
                    $"Unproven promotion profile '{profile.Id}' must not claim a runtime-proof output digest.");
            }
            if (profile.RuntimeProvenMaxAppendedRecords < 0 ||
                profile.RuntimeProven && profile.RuntimeProvenMaxAppendedRecords == 0 ||
                !profile.RuntimeProven && profile.RuntimeProvenMaxAppendedRecords != 0)
            {
                throw new InvalidDataException(
                    $"Promotion profile '{profile.Id}' has an invalid runtime-proven appended-record capacity.");
            }
            if (profile.RuntimeProven &&
                NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                    profile.StructuralGrowthPolicy))
            {
                throw new InvalidDataException(
                    $"Runtime-proven promotion profile '{profile.Id}' cannot use static-research-only structural policy {profile.StructuralGrowthPolicy}.");
            }
            if (profile.RuntimeProvenMaxAppendedRecords >
                    NativeTerrainTextureRecordAppendBuilder
                        .GetMaximumRequestedRecordCount(
                            profile.StructuralGrowthPolicy) ||
                profile.Retail.ExpectedSourceTextureCount +
                    profile.RuntimeProvenMaxAppendedRecords >
                        NativeTerrainTextureRecordAppendBuilder
                            .MaximumTextureRecordCount)
            {
                throw new InvalidDataException(
                    $"Promotion profile '{profile.Id}' exceeds its structural-policy or seven-bit texture-record capacity.");
            }
        }
    }

    public static IReadOnlyList<AppendedPrivateTerrainTexturePromotionProfile> Profiles =>
        CheckedProfiles;

    public static bool TryFindExact(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy,
        out AppendedPrivateTerrainTexturePromotionProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(binding);
        profile = CheckedProfiles.FirstOrDefault(candidate =>
            candidate.IsExactMatch(
                binding,
                writerKind,
                structuralGrowthPolicy));
        return profile != null;
    }

    public static bool TryFindExact(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        out AppendedPrivateTerrainTexturePromotionProfile? profile) =>
        TryFindExact(
            binding,
            writerKind,
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
            out profile);

    public static NativeTerrainTextureStructuralGrowthPolicy
        ResolveNormalReleaseStructuralGrowthPolicy(
            NativeTerrainTextureRecordAppendSourceBinding binding,
            int appendedRecordCount)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (appendedRecordCount <= 0)
            return NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

        AppendedPrivateTerrainTexturePromotionProfile[] matches =
            CheckedProfiles
                .Where(profile =>
                    profile.RuntimeProven &&
                    appendedRecordCount <=
                        profile.RuntimeProvenMaxAppendedRecords &&
                    string.Equals(
                        profile.NormalizedTargetLevelKey,
                        LevelCatalog.NormalizeKey(binding.TargetLevelKey),
                        StringComparison.OrdinalIgnoreCase) &&
                    profile.Retail.Matches(binding))
                .ToArray();
        if (matches.Length == 0)
            return NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

        NativeTerrainTextureStructuralGrowthPolicy[] policies =
            matches.Select(profile => profile.StructuralGrowthPolicy)
                .Distinct()
                .ToArray();
        if (policies.Length != 1)
        {
            throw new InvalidDataException(
                $"The exact appended-private retail binding for {LevelCatalog.NormalizeKey(binding.TargetLevelKey)} resolves to conflicting normal-release structural policies.");
        }
        return policies[0];
    }

    public static bool TryAuthorizeNormalRelease(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy,
        out AppendedPrivateTerrainTexturePromotionProfile? profile,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!TryFindExact(
                binding,
                writerKind,
                structuralGrowthPolicy,
                out profile) ||
            profile == null)
        {
            reason =
                $"No exact appended-private promotion profile matches " +
                $"{LevelCatalog.NormalizeKey(binding.TargetLevelKey)} on source " +
                $"{ShortSha(binding.SourceImageSha256)} with writer {writerKind} " +
                $"and structural policy {structuralGrowthPolicy}. Normal release " +
                "requires an exact checked retail preimage, writer, and policy.";
            return false;
        }

        if (!profile.RuntimeProven)
        {
            reason =
                $"Profile '{profile.Id}' matches the exact retail preimage and {writerKind} writer, " +
                "but it has static readback evidence only; its DuckStation runtime proof is still pending.";
            return false;
        }

        if (!AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(
                profile.RuntimeProofOutputSha256))
        {
            reason =
                $"Profile '{profile.Id}' is marked runtime-proven without a valid runtime output digest.";
            return false;
        }

        if (appendedRecordCount <= 0)
        {
            reason =
                $"Profile '{profile.Id}' requires at least one appended private terrain texture record.";
            return false;
        }

        if (appendedRecordCount > profile.RuntimeProvenMaxAppendedRecords)
        {
            reason =
                $"Profile '{profile.Id}' is runtime-proven for at most " +
                $"{profile.RuntimeProvenMaxAppendedRecords} appended private texture record(s), " +
                $"but this build requests {appendedRecordCount}. Remove or undo the extra private texture before normal Create BIN.";
            return false;
        }

        reason =
            $"Runtime-proven exact appended-private profile '{profile.Id}' matched " +
            $"{profile.NormalizedTargetLevelKey}, its retail source preimage, {writerKind}, " +
            $"{structuralGrowthPolicy}, " +
            $"and {appendedRecordCount}/{profile.RuntimeProvenMaxAppendedRecords} proven private record(s).";
        return true;
    }

    public static bool TryAuthorizeNormalRelease(
        NativeTerrainTextureRecordAppendSourceBinding binding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount,
        out AppendedPrivateTerrainTexturePromotionProfile? profile,
        out string reason) =>
        TryAuthorizeNormalRelease(
            binding,
            writerKind,
            appendedRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
            out profile,
            out reason);

    private static void ValidateFingerprint(
        string profileId,
        AppendedPrivateTerrainTextureRetailFingerprint retail)
    {
        ArgumentNullException.ThrowIfNull(retail);
        if (retail.BindingVersion <= 0 ||
            retail.TargetWadEntry < 0 ||
            retail.ExpectedSourceTextureCount is <= 0 or > 128 ||
            !AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(retail.SourceImageSha256) ||
            !AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(
                retail.ExpectedTextureComponentSha256) ||
            !AppendedPrivateTerrainTextureRetailFingerprint.IsSha256(
                retail.ExpectedLevelDataSha256))
        {
            throw new InvalidDataException(
                $"Promotion profile '{profileId}' has an invalid exact retail fingerprint.");
        }
    }

    private static string ShortSha(string value)
    {
        string normalized =
            AppendedPrivateTerrainTextureRetailFingerprint.NormalizeSha256(value);
        return normalized.Length >= 12 ? normalized[..12] : normalized;
    }
}
