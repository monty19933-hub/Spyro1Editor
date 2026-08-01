using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Selects the checked low-level writer used by a destination profile.  The
/// Artisans V2 writer remains intact behind this adapter so introducing the
/// destination-neutral contract cannot change its proven output bytes.
/// </summary>
public enum LockedChestRuntimeBundleWriter
{
    ArtisansNativeV2
}

/// <summary>
/// Destination-owned allocation and runtime-proof facts for one Key + Locked
/// Chest transplant.  Adding a destination requires a complete, independently
/// validated profile; no Artisans address is inferred from the level name.
/// </summary>
public sealed record LockedChestRuntimeBundleDestinationProfile(
    string Id,
    string TargetLevelKey,
    string SourceLevelKey,
    string DiscImageSha256,
    string RecipeId,
    string RequiredExporterFeature,
    string KeyTemplateId,
    string LockedChestTemplateId,
    int KeyOutputTrueIndex,
    int LockedChestOutputTrueIndex,
    IReadOnlyList<int> RewardMarkerOutputTrueIndices,
    int SourceRuntimeCountBefore,
    int SourceRuntimeCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter,
    int WadGrowthBytes,
    int RuntimeSlotsConsumed,
    int MaxBundleInstances,
    bool RelocatesExecutable,
    string RuntimeProofOutputSha256,
    LockedChestRuntimeBundleWriter Writer,
    string EvidenceNote)
{
    public int AppendedSourceRowCount => SourceRuntimeCountAfter - SourceRuntimeCountBefore;

    public int TreasureDelta => TreasureTargetAfter - TreasureTargetBefore;

    public void Validate()
    {
        RequireText(Id, nameof(Id));
        RequireText(TargetLevelKey, nameof(TargetLevelKey));
        RequireText(SourceLevelKey, nameof(SourceLevelKey));
        RequireText(RecipeId, nameof(RecipeId));
        RequireText(RequiredExporterFeature, nameof(RequiredExporterFeature));
        RequireText(KeyTemplateId, nameof(KeyTemplateId));
        RequireText(LockedChestTemplateId, nameof(LockedChestTemplateId));
        RequireText(EvidenceNote, nameof(EvidenceNote));
        RequireSha256(DiscImageSha256, nameof(DiscImageSha256));
        RequireSha256(RuntimeProofOutputSha256, nameof(RuntimeProofOutputSha256));

        if (!string.Equals(TargetLevelKey, LevelCatalog.NormalizeKey(TargetLevelKey), StringComparison.Ordinal))
            throw new InvalidDataException($"Locked-chest profile '{Id}' must use a normalized target-level key.");
        if (!string.Equals(SourceLevelKey, LevelCatalog.NormalizeKey(SourceLevelKey), StringComparison.Ordinal))
            throw new InvalidDataException($"Locked-chest profile '{Id}' must use a normalized source-level key.");
        if (string.Equals(KeyTemplateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Locked-chest profile '{Id}' assigns the same template to its Key and chest.");
        if (!Enum.IsDefined(Writer))
            throw new InvalidDataException($"Locked-chest profile '{Id}' selects an unknown writer.");
        if (SourceRuntimeCountBefore < 0 || SourceRuntimeCountAfter <= SourceRuntimeCountBefore)
            throw new InvalidDataException($"Locked-chest profile '{Id}' has an invalid source-row allocation.");
        if (TreasureTargetBefore < 0 || TreasureTargetAfter <= TreasureTargetBefore)
            throw new InvalidDataException($"Locked-chest profile '{Id}' must declare a positive treasure delta.");
        if (WadGrowthBytes <= 0 || WadGrowthBytes % 0x800 != 0)
            throw new InvalidDataException($"Locked-chest profile '{Id}' must declare sector-aligned positive WAD growth.");
        if (RuntimeSlotsConsumed <= 0 || MaxBundleInstances <= 0)
            throw new InvalidDataException($"Locked-chest profile '{Id}' has an invalid runtime-slot or instance budget.");
        if (RewardMarkerOutputTrueIndices.Count == 0 ||
            RewardMarkerOutputTrueIndices.Any(index => index < 0) ||
            RewardMarkerOutputTrueIndices.Distinct().Count() != RewardMarkerOutputTrueIndices.Count)
        {
            throw new InvalidDataException($"Locked-chest profile '{Id}' must declare unique non-negative hidden reward rows.");
        }

        int[] allocatedRows =
        [
            KeyOutputTrueIndex,
            LockedChestOutputTrueIndex,
            .. RewardMarkerOutputTrueIndices
        ];
        if (allocatedRows.Any(index => index < SourceRuntimeCountBefore || index >= SourceRuntimeCountAfter) ||
            allocatedRows.Distinct().Count() != allocatedRows.Length)
        {
            throw new InvalidDataException($"Locked-chest profile '{Id}' has overlapping or out-of-range visible/hidden rows.");
        }
        if (allocatedRows.Length != AppendedSourceRowCount ||
            !allocatedRows.Order().SequenceEqual(Enumerable.Range(SourceRuntimeCountBefore, AppendedSourceRowCount)))
        {
            throw new InvalidDataException($"Locked-chest profile '{Id}' must own its complete contiguous appended source-row range.");
        }
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Locked-chest profile field '{label}' is empty.");
    }

    private static void RequireSha256(string value, string label)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"Locked-chest profile field '{label}' is not a SHA-256 digest.");
    }
}

/// <summary>
/// Checked destination profiles exposed to the generic composer.  Artisans is
/// intentionally the only entry until another exact target passes DuckStation.
/// </summary>
public static class LockedChestRuntimeBundleProfileCatalog
{
    public const string CleanUsaImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const string ArtisansV2ProfileId = "spyro1.special-chest.locked.artisans.runtime-bundle.v2";
    public const string ArtisansV2RuntimeProofOutputSha256 = "67b82ae66851d770b99d5e2d78400671268c31ad62b1815163e63324adc07d34";

    public static LockedChestRuntimeBundleDestinationProfile ArtisansV2 { get; } = new(
        Id: ArtisansV2ProfileId,
        TargetLevelKey: ArtisansNativeLockedChestRuntimeBundleComposer.TargetLevelKey,
        SourceLevelKey: "peacekeepers",
        DiscImageSha256: CleanUsaImageSha256,
        RecipeId: ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId,
        RequiredExporterFeature: ArtisansNativeLockedChestRuntimeBundleComposer.RequiredExporterFeature,
        KeyTemplateId: ArtisansNativeLockedChestRuntimeBundleComposer.KeyTemplateId,
        LockedChestTemplateId: ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestTemplateId,
        KeyOutputTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.KeyOutputTrueIndex,
        LockedChestOutputTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestOutputTrueIndex,
        RewardMarkerOutputTrueIndices: [176, 177, 178, 179, 180],
        SourceRuntimeCountBefore: ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountBefore,
        SourceRuntimeCountAfter: ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountAfter,
        TreasureTargetBefore: ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetBefore,
        TreasureTargetAfter: ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetAfter,
        WadGrowthBytes: 0x2000,
        RuntimeSlotsConsumed: 6,
        MaxBundleInstances: 1,
        RelocatesExecutable: true,
        RuntimeProofOutputSha256: ArtisansV2RuntimeProofOutputSha256,
        Writer: LockedChestRuntimeBundleWriter.ArtisansNativeV2,
        EvidenceNote: "Exact Artisans V2 Key + Locked Chest handler/model/reward/private-texture composition passed DuckStation and remains limited to one atomic pair.");

    private static readonly IReadOnlyList<LockedChestRuntimeBundleDestinationProfile> Profiles = [ArtisansV2];

    static LockedChestRuntimeBundleProfileCatalog()
    {
        foreach (LockedChestRuntimeBundleDestinationProfile profile in Profiles)
            profile.Validate();
        if (Profiles.Select(profile => (profile.TargetLevelKey, profile.DiscImageSha256.ToLowerInvariant())).Distinct().Count() != Profiles.Count)
            throw new InvalidDataException("Locked-chest runtime-bundle destination profile keys must be unique.");
    }

    public static IReadOnlyList<LockedChestRuntimeBundleDestinationProfile> AllProfiles => Profiles;

    public static LockedChestRuntimeBundleDestinationProfile? Find(string targetLevelKey, string discImageSha256)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        string fingerprint = (discImageSha256 ?? "").Trim();
        return Profiles.FirstOrDefault(profile =>
            string.Equals(profile.TargetLevelKey, target, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(profile.DiscImageSha256, fingerprint, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record LockedChestRuntimeBundleIntent(
    string ProfileId,
    string RecipeId,
    string RequiredExporterFeature,
    string TargetLevelKey,
    string KeyTemplateId,
    int KeyEditorTrueIndex,
    string KeyLabel,
    int KeyRawX,
    int KeyRawY,
    int KeyRawZ,
    int KeyYawByte,
    string LockedChestTemplateId,
    int LockedChestEditorTrueIndex,
    string LockedChestLabel,
    int LockedChestRawX,
    int LockedChestRawY,
    int LockedChestRawZ,
    int LockedChestYawByte,
    int KeyOutputTrueIndex,
    int LockedChestOutputTrueIndex,
    IReadOnlyList<int> RewardMarkerOutputTrueIndices,
    int SourceRuntimeCountBefore,
    int SourceRuntimeCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter)
{
    public int AppendedSourceRowCount => SourceRuntimeCountAfter - SourceRuntimeCountBefore;

    public int TreasureDelta => TreasureTargetAfter - TreasureTargetBefore;
}

public sealed record LockedChestRuntimeBundleRequest(
    LockedChestRuntimeBundleDestinationProfile Profile,
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    LockedChestRuntimeBundleIntent Intent,
    bool WriteImage = true);

public sealed record LockedChestRuntimeBundleResult(
    string ProfileId,
    string RecipeId,
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    bool InstalledFresh,
    bool ReusedInstalledBundle,
    bool WroteImage,
    bool Verified,
    string Verification);

/// <summary>
/// Destination-neutral entry point.  It validates a complete target profile
/// and common intent before dispatching to that profile's checked writer.
/// </summary>
public static class LockedChestRuntimeBundleComposer
{
    public static bool IsAvailable(LockedChestRuntimeBundleDestinationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return profile.Writer == LockedChestRuntimeBundleWriter.ArtisansNativeV2 &&
            ArtisansNativeLockedChestRuntimeBundleComposer.IsAvailable;
    }

    public static bool TryDetectIntent(
        LockedChestRuntimeBundleDestinationProfile profile,
        LevelDefinition level,
        JsonElement editsElement,
        out LockedChestRuntimeBundleIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(level);
        profile.Validate();
        GuardTarget(profile, level.Key);

        intent = null;
        switch (profile.Writer)
        {
            case LockedChestRuntimeBundleWriter.ArtisansNativeV2:
                if (!ArtisansNativeLockedChestRuntimeBundleComposer.TryDetectIntent(level, editsElement, out ArtisansNativeLockedChestRuntimeBundleIntent? artisansIntent))
                    return false;
                intent = FromArtisans(profile, artisansIntent!);
                ValidateIntent(profile, intent);
                return true;
            default:
                throw new InvalidOperationException($"Locked-chest profile '{profile.Id}' has no registered writer.");
        }
    }

    public static bool IsBundleTemplateEdit(LockedChestRuntimeBundleDestinationProfile profile, JsonElement edit)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (!edit.TryGetProperty("crossLevelTemplate", out JsonElement template) || template.ValueKind != JsonValueKind.Object)
            return false;
        string templateId = template.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() ?? ""
            : "";
        return string.Equals(templateId, profile.KeyTemplateId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, profile.LockedChestTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    public static LockedChestRuntimeBundleResult ApplyAndVerify(LockedChestRuntimeBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.Intent);
        request.Profile.Validate();
        ValidateIntent(request.Profile, request.Intent);

        switch (request.Profile.Writer)
        {
            case LockedChestRuntimeBundleWriter.ArtisansNativeV2:
            {
                ArtisansNativeLockedChestRuntimeBundleResult result =
                    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
                        new ArtisansNativeLockedChestRuntimeBundleRequest(
                            request.SourceImagePath,
                            request.SourceCuePath,
                            request.OutputPrefix,
                            request.WadAnalysisPath,
                            ToArtisans(request.Intent),
                            request.WriteImage));
                GuardBackendResult(request.Profile, result);
                return new LockedChestRuntimeBundleResult(
                    ProfileId: request.Profile.Id,
                    RecipeId: result.Plan.RecipeId,
                    OutputImagePath: result.OutputImagePath,
                    OutputCuePath: result.OutputCuePath,
                    OutputPlanPath: result.OutputPlanPath,
                    InstalledFresh: result.Plan.InstalledFresh,
                    ReusedInstalledBundle: result.Plan.ReusedInstalledBundle,
                    WroteImage: result.WroteImage,
                    Verified: result.Verified,
                    Verification: result.Plan.Verification);
            }
            default:
                throw new InvalidOperationException($"Locked-chest profile '{request.Profile.Id}' has no registered writer.");
        }
    }

    public static void ValidateIntent(
        LockedChestRuntimeBundleDestinationProfile profile,
        LockedChestRuntimeBundleIntent intent)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(intent);
        profile.Validate();
        if (!string.Equals(intent.ProfileId, profile.Id, StringComparison.Ordinal) ||
            !string.Equals(intent.RecipeId, profile.RecipeId, StringComparison.Ordinal) ||
            !string.Equals(intent.RequiredExporterFeature, profile.RequiredExporterFeature, StringComparison.Ordinal) ||
            !string.Equals(LevelCatalog.NormalizeKey(intent.TargetLevelKey), profile.TargetLevelKey, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(intent.KeyTemplateId, profile.KeyTemplateId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(intent.LockedChestTemplateId, profile.LockedChestTemplateId, StringComparison.OrdinalIgnoreCase) ||
            intent.KeyOutputTrueIndex != profile.KeyOutputTrueIndex ||
            intent.LockedChestOutputTrueIndex != profile.LockedChestOutputTrueIndex ||
            !intent.RewardMarkerOutputTrueIndices.SequenceEqual(profile.RewardMarkerOutputTrueIndices) ||
            intent.SourceRuntimeCountBefore != profile.SourceRuntimeCountBefore ||
            intent.SourceRuntimeCountAfter != profile.SourceRuntimeCountAfter ||
            intent.TreasureTargetBefore != profile.TreasureTargetBefore ||
            intent.TreasureTargetAfter != profile.TreasureTargetAfter)
        {
            throw new InvalidOperationException($"The Key + Locked Chest intent does not match destination profile '{profile.Id}'.");
        }
        if (intent.KeyEditorTrueIndex < 0 || intent.LockedChestEditorTrueIndex < 0 ||
            intent.KeyEditorTrueIndex == intent.LockedChestEditorTrueIndex)
        {
            throw new InvalidOperationException("The Key and Locked Chest require distinct non-negative editor object indices.");
        }
        if (intent.KeyYawByte is < 0 or > 255 || intent.LockedChestYawByte is < 0 or > 255)
            throw new InvalidOperationException("The Key and Locked Chest yaw bytes must be in the native 0x00-0xFF range.");
    }

    private static LockedChestRuntimeBundleIntent FromArtisans(
        LockedChestRuntimeBundleDestinationProfile profile,
        ArtisansNativeLockedChestRuntimeBundleIntent intent) =>
        new(
            ProfileId: profile.Id,
            RecipeId: intent.RecipeId,
            RequiredExporterFeature: intent.RequiredExporterFeature,
            TargetLevelKey: intent.TargetLevelKey,
            KeyTemplateId: intent.KeyTemplateId,
            KeyEditorTrueIndex: intent.KeyEditorTrueIndex,
            KeyLabel: intent.KeyLabel,
            KeyRawX: intent.KeyRawX,
            KeyRawY: intent.KeyRawY,
            KeyRawZ: intent.KeyRawZ,
            KeyYawByte: intent.KeyYawByte,
            LockedChestTemplateId: intent.LockedChestTemplateId,
            LockedChestEditorTrueIndex: intent.LockedChestEditorTrueIndex,
            LockedChestLabel: intent.LockedChestLabel,
            LockedChestRawX: intent.LockedChestRawX,
            LockedChestRawY: intent.LockedChestRawY,
            LockedChestRawZ: intent.LockedChestRawZ,
            LockedChestYawByte: intent.LockedChestYawByte,
            KeyOutputTrueIndex: intent.KeyOutputTrueIndex,
            LockedChestOutputTrueIndex: intent.LockedChestOutputTrueIndex,
            RewardMarkerOutputTrueIndices: intent.RewardMarkerOutputTrueIndices,
            SourceRuntimeCountBefore: intent.SourceRuntimeCountBefore,
            SourceRuntimeCountAfter: intent.SourceRuntimeCountAfter,
            TreasureTargetBefore: intent.TreasureTargetBefore,
            TreasureTargetAfter: intent.TreasureTargetAfter);

    private static ArtisansNativeLockedChestRuntimeBundleIntent ToArtisans(LockedChestRuntimeBundleIntent intent) =>
        new(
            RecipeId: intent.RecipeId,
            RequiredExporterFeature: intent.RequiredExporterFeature,
            TargetLevelKey: intent.TargetLevelKey,
            KeyTemplateId: intent.KeyTemplateId,
            KeyEditorTrueIndex: intent.KeyEditorTrueIndex,
            KeyLabel: intent.KeyLabel,
            KeyRawX: intent.KeyRawX,
            KeyRawY: intent.KeyRawY,
            KeyRawZ: intent.KeyRawZ,
            KeyYawByte: intent.KeyYawByte,
            LockedChestTemplateId: intent.LockedChestTemplateId,
            LockedChestEditorTrueIndex: intent.LockedChestEditorTrueIndex,
            LockedChestLabel: intent.LockedChestLabel,
            LockedChestRawX: intent.LockedChestRawX,
            LockedChestRawY: intent.LockedChestRawY,
            LockedChestRawZ: intent.LockedChestRawZ,
            LockedChestYawByte: intent.LockedChestYawByte,
            KeyOutputTrueIndex: intent.KeyOutputTrueIndex,
            LockedChestOutputTrueIndex: intent.LockedChestOutputTrueIndex,
            RewardMarkerOutputTrueIndices: intent.RewardMarkerOutputTrueIndices,
            SourceRuntimeCountBefore: intent.SourceRuntimeCountBefore,
            SourceRuntimeCountAfter: intent.SourceRuntimeCountAfter,
            TreasureTargetBefore: intent.TreasureTargetBefore,
            TreasureTargetAfter: intent.TreasureTargetAfter);

    private static void GuardBackendResult(
        LockedChestRuntimeBundleDestinationProfile profile,
        ArtisansNativeLockedChestRuntimeBundleResult result)
    {
        if (!result.Verified ||
            !string.Equals(result.Plan.RecipeId, profile.RecipeId, StringComparison.Ordinal) ||
            !string.Equals(result.Plan.RequiredExporterFeature, profile.RequiredExporterFeature, StringComparison.Ordinal) ||
            result.Plan.KeyOutputTrueIndex != profile.KeyOutputTrueIndex ||
            result.Plan.LockedChestOutputTrueIndex != profile.LockedChestOutputTrueIndex ||
            !result.Plan.RewardMarkerOutputTrueIndices.SequenceEqual(profile.RewardMarkerOutputTrueIndices) ||
            result.Plan.SourceRuntimeCountBefore != profile.SourceRuntimeCountBefore ||
            result.Plan.SourceRuntimeCountAfter != profile.SourceRuntimeCountAfter ||
            result.Plan.TreasureTargetBefore != profile.TreasureTargetBefore ||
            result.Plan.TreasureTargetAfter != profile.TreasureTargetAfter)
        {
            throw new InvalidDataException($"Locked-chest writer output no longer matches destination profile '{profile.Id}'.");
        }
    }

    private static void GuardTarget(LockedChestRuntimeBundleDestinationProfile profile, string levelKey)
    {
        string target = LevelCatalog.NormalizeKey(levelKey);
        if (!string.Equals(target, profile.TargetLevelKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Locked-chest profile '{profile.Id}' targets '{profile.TargetLevelKey}', not '{target}'.");
    }
}
