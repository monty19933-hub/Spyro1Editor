using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnusedLevel65BlankLevelLabCapabilityState
{
    LockedBaseline,
    ResearchOnly,
    Unavailable
}

public sealed record UnusedLevel65BlankLevelLabCapabilities(
    UnusedLevel65BlankLevelLabCapabilityState RuntimeBaseBootstrap,
    UnusedLevel65BlankLevelLabCapabilityState ExistingHpZTerrainEdits,
    UnusedLevel65BlankLevelLabCapabilityState TerrainTextureEdits,
    UnusedLevel65BlankLevelLabCapabilityState StructuralTerrainGrowth,
    UnusedLevel65BlankLevelLabCapabilityState LowDetailTerrainAuthoring,
    UnusedLevel65BlankLevelLabCapabilityState ObjectTableInspection,
    UnusedLevel65BlankLevelLabCapabilityState ObjectMutation,
    UnusedLevel65BlankLevelLabCapabilityState CrossLevelObjectImport,
    UnusedLevel65BlankLevelLabCapabilityState PortalExitRouting,
    UnusedLevel65BlankLevelLabCapabilityState SaveOwnership,
    UnusedLevel65BlankLevelLabCapabilityState NormalCreateBin,
    UnusedLevel65BlankLevelLabCapabilityState ReleasePromotion);

public sealed record UnusedLevel65BlankLevelLabEvidenceBinding(
    string EvidenceId,
    string EvidenceStatus,
    string EvidenceRelativePath,
    string EvidenceSha256,
    string ProfileId,
    string ImageSha256,
    bool PromotionAuthorized);

public sealed record UnusedLevel65BlankLevelLabProfile(
    string ProfileId,
    int ProfileVersion,
    string Key,
    string DisplayName,
    int LevelId,
    int DataWadEntry,
    long ObjectTableWadOffset,
    long ObjectTableRelativeOffset,
    int ObjectRecordCount,
    int ExpectedRetailCatalogCount,
    string CleanUsaImageSha256,
    string PhysicalCloneProfileId,
    string PhysicalCloneImageSha256,
    UnusedLevel65BlankLevelLabEvidenceBinding PhysicalCloneEvidence,
    string LockedBaseProfileId,
    string LockedBaseImageSha256,
    UnusedLevel65BlankLevelLabEvidenceBinding LockedBaseEvidence,
    int WorkspaceLayoutVersion,
    string WorkspaceLayoutRelativeRoot,
    UnusedLevel65BlankLevelLabCapabilities Capabilities,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

public sealed record UnusedLevel65BlankLevelLabWorkspacePaths(
    string ContainerRootPath,
    string RootPath,
    string ManifestPath,
    string LockedBaseDirectoryPath,
    string LockedBaseImagePath,
    string LockedBaseCuePath,
    string AuthoredEditsDirectoryPath,
    string OperationsDirectoryPath);

public sealed record UnusedLevel65BlankLevelLabManifest(
    int ManifestSchemaVersion,
    string ProfileId,
    int ProfileVersion,
    string WorkspaceKey,
    int WorkspaceLayoutVersion,
    string WorkspaceLayoutRelativeRoot,
    string DisplayName,
    int LevelId,
    int DataWadEntry,
    long ObjectTableWadOffset,
    long ObjectTableRelativeOffset,
    int ObjectRecordCount,
    string LockedBaseImageRelativePath,
    string LockedBaseCueRelativePath,
    string AuthoredEditsRelativePath,
    string CleanSourceImageSha256,
    string PhysicalCloneProfileId,
    string PhysicalCloneImageSha256,
    UnusedLevel65BlankLevelLabEvidenceBinding PhysicalCloneEvidence,
    string LockedBaseProfileId,
    string LockedBaseImageSha256,
    UnusedLevel65BlankLevelLabEvidenceBinding LockedBaseEvidence,
    UnusedLevel65BlankLevelLabCapabilities Capabilities,
    bool ExternalGameDataRequired,
    bool GameDataEmbedded,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

public sealed record UnusedLevel65BlankLevelLabBootstrapRequest(
    string SourceImagePath,
    string SourceCuePath,
    string WorkspaceContainerPath,
    bool ReplaceInvalidExistingWorkspace = false,
    Action<string>? TestStageHook = null);

public sealed record UnusedLevel65BlankLevelLabBootstrapResult(
    UnusedLevel65BlankLevelLabWorkspacePaths Paths,
    UnusedLevel65BlankLevelLabManifest Manifest,
    string SourceImageSha256,
    string PhysicalCloneImageSha256,
    string LockedBaseImageSha256,
    bool SourceImagePreserved,
    bool AtomicPublicationCompleted,
    bool OwnedTemporaryIntermediatesRemoved,
    bool ReusedExistingLockedBase);

/// <summary>
/// Defines the research-only ID65 lab without changing the retail catalog on
/// disk. The locked runtime substrate is the exact focused-runtime-passed
/// display-name candidate; it is not a blank level and is never a normal
/// Create BIN or release profile.
/// </summary>
public static class UnusedLevel65BlankLevelLabProfileRegistry
{
    public const string Key = "unusedlevel65blank";
    public const string ProfileId = "unused-level-65-blank-level-lab-clean-usa-research-v1";
    public const int ProfileVersion = 1;
    public const int WorkspaceLayoutVersion = 1;
    public const int ManifestSchemaVersion = 1;
    public const int ExpectedRetailCatalogCount = 35;
    public const int LevelId = 65;
    public const int DataWadEntry = 80;
    public const int ResidentTextureCount = 66;
    public const long ObjectTableWadOffset = 0x6B06970;
    public const long ObjectTableRelativeOffset = 0x1D0170;
    public const int ObjectRecordCount = 107;
    public const string WorkspaceLayoutRelativeRoot = "_research/unusedlevel65blank/v1";
    public const string CleanUsaImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

    public const string PhysicalCloneEvidenceId =
        "unused-level-65-town-square-physical-clone-focused-pass-duckstation-2026-08-08";
    public const string PhysicalCloneEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-town-square-physical-clone-focused-pass-2026-08-08.json";
    public const string PhysicalCloneEvidenceSha256 =
        "4067469eba247c84cd895055cae1f11dc7343747d09bda981cee20313988462b";
    public const string LockedBaseEvidenceId =
        "unused-level-65-town-square-display-name-focused-pass-duckstation-2026-08-08";
    public const string LockedBaseEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-town-square-display-name-focused-pass-2026-08-08.json";
    public const string LockedBaseEvidenceSha256 =
        "fde5ad44d3850ebd9ac10057b94c5d16f54b30dac0d69c12fab7a0da95d276fb";

    private const string LockedBaseFileStem = "unusedlevel65blank-locked-base-v1";

    public static UnusedLevel65BlankLevelLabCapabilities Capabilities { get; } = new(
        RuntimeBaseBootstrap: UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly,
        ExistingHpZTerrainEdits: UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly,
        TerrainTextureEdits: UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly,
        StructuralTerrainGrowth: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        LowDetailTerrainAuthoring: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        ObjectTableInspection: UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly,
        ObjectMutation: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        CrossLevelObjectImport: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        PortalExitRouting: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        SaveOwnership: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        NormalCreateBin: UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
        ReleasePromotion: UnusedLevel65BlankLevelLabCapabilityState.Unavailable);

    public static UnusedLevel65BlankLevelLabEvidenceBinding PhysicalCloneEvidence { get; } = new(
        PhysicalCloneEvidenceId,
        "focused-runtime-pass",
        PhysicalCloneEvidenceRelativePath,
        PhysicalCloneEvidenceSha256,
        UnusedLevel65PhysicalCloneCandidateExporter.ProfileId,
        UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256,
        PromotionAuthorized: false);

    public static UnusedLevel65BlankLevelLabEvidenceBinding LockedBaseEvidence { get; } = new(
        LockedBaseEvidenceId,
        "focused-runtime-pass",
        LockedBaseEvidenceRelativePath,
        LockedBaseEvidenceSha256,
        UnusedLevel65DisplayNameCandidateExporter.ProfileId,
        UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256,
        PromotionAuthorized: false);

    public static UnusedLevel65BlankLevelLabProfile Profile { get; } = new(
        ProfileId,
        ProfileVersion,
        Key,
        "ID65 Blank-Level Lab",
        LevelId,
        DataWadEntry,
        ObjectTableWadOffset,
        ObjectTableRelativeOffset,
        ObjectRecordCount,
        ExpectedRetailCatalogCount,
        CleanUsaImageSha256,
        UnusedLevel65PhysicalCloneCandidateExporter.ProfileId,
        UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256,
        PhysicalCloneEvidence,
        UnusedLevel65DisplayNameCandidateExporter.ProfileId,
        UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256,
        LockedBaseEvidence,
        WorkspaceLayoutVersion,
        WorkspaceLayoutRelativeRoot,
        Capabilities,
        PromotionAuthorized: false,
        NormalCreateBinEnabled: false);

    public static LevelDefinition Definition { get; } = new()
    {
        Key = Key,
        ScriptKey = "townsquare",
        DisplayName = "ID65 Blank-Level Lab",
        LevelId = LevelId,
        SourceWadEntry = DataWadEntry,
        SourceTableWadOffset = $"0x{ObjectTableWadOffset:X}",
        SourceTableRelativeOffset = $"0x{ObjectTableRelativeOffset:X}",
        SourceRecordCount = ObjectRecordCount,
        Confidence = "research-only-locked-display-name-base-v1",
        RuntimeMobyPointer = ""
    };

    static UnusedLevel65BlankLevelLabProfileRegistry()
    {
        if (ObjectTableWadOffset - ObjectTableRelativeOffset != 0x6936800 ||
            ResidentTextureCount != 66 ||
            !IsSha256(CleanUsaImageSha256) ||
            !IsSha256(PhysicalCloneEvidenceSha256) ||
            !IsSha256(LockedBaseEvidenceSha256) ||
            Profile.PromotionAuthorized ||
            Profile.NormalCreateBinEnabled ||
            Capabilities.NormalCreateBin != UnusedLevel65BlankLevelLabCapabilityState.Unavailable ||
            Capabilities.ReleasePromotion != UnusedLevel65BlankLevelLabCapabilityState.Unavailable)
        {
            throw new InvalidDataException("The ID65 Blank-Level Lab profile constants are inconsistent.");
        }
    }

    public static LevelCatalog AugmentCatalog(LevelCatalog retailCatalog)
    {
        ArgumentNullException.ThrowIfNull(retailCatalog);
        LevelDefinition[] existingLabRows = retailCatalog.Levels.Where(IsLabLevel).ToArray();
        LevelDefinition[] conflictingRows = retailCatalog.Levels.Where(level =>
            !IsLabLevel(level) &&
            (LevelCatalog.NormalizeKey(level.Key) == LevelCatalog.NormalizeKey(Key) ||
             level.LevelId == LevelId ||
             level.SourceWadEntry == DataWadEntry)).ToArray();
        if (conflictingRows.Length > 0 || existingLabRows.Length > 1)
            throw new InvalidDataException("The catalog already contains a conflicting ID65 research definition.");

        if (existingLabRows.Length == 1)
        {
            if (retailCatalog.Levels.Count != ExpectedRetailCatalogCount + 1)
                throw new InvalidDataException("The augmented ID65 lab catalog no longer contains exactly 35 retail levels plus one research level.");
            return retailCatalog;
        }

        if (retailCatalog.Levels.Count != ExpectedRetailCatalogCount)
            throw new InvalidDataException($"The ID65 lab requires the exact {ExpectedRetailCatalogCount}-level retail catalog before augmentation.");
        return new LevelCatalog([.. retailCatalog.Levels, Definition]);
    }

    public static bool IsLabLevel(LevelDefinition? level)
    {
        return level != null &&
               LevelCatalog.NormalizeKey(level.Key) == LevelCatalog.NormalizeKey(Key) &&
               level.ScriptKey == Definition.ScriptKey &&
               level.DisplayName == Definition.DisplayName &&
               level.LevelId == LevelId &&
               level.SourceWadEntry == DataWadEntry &&
               string.Equals(level.SourceTableWadOffset, Definition.SourceTableWadOffset, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(level.SourceTableRelativeOffset, Definition.SourceTableRelativeOffset, StringComparison.OrdinalIgnoreCase) &&
               level.SourceRecordCount == ObjectRecordCount &&
               level.Confidence == Definition.Confidence;
    }

    public static bool TryResolve(LevelDefinition? level, out UnusedLevel65BlankLevelLabProfile? profile)
    {
        if (IsLabLevel(level))
        {
            profile = Profile;
            return true;
        }

        profile = null;
        return false;
    }

    public static UnusedLevel65BlankLevelLabProfile Resolve(LevelDefinition level)
    {
        return TryResolve(level, out UnusedLevel65BlankLevelLabProfile? profile) && profile != null
            ? profile
            : throw new InvalidOperationException("The supplied level definition is not the exact ID65 Blank-Level Lab definition.");
    }

    public static UnusedLevel65BlankLevelLabWorkspacePaths CreateWorkspacePaths(string workspaceContainerPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceContainerPath))
            throw new ArgumentException("A workspace container path is required.", nameof(workspaceContainerPath));

        string container = Path.GetFullPath(workspaceContainerPath);
        string root = Path.Combine(container, "_research", Key, $"v{WorkspaceLayoutVersion}");
        string locked = Path.Combine(root, "locked-base");
        return new UnusedLevel65BlankLevelLabWorkspacePaths(
            container,
            root,
            Path.Combine(root, "lab-manifest.json"),
            locked,
            Path.Combine(locked, LockedBaseFileStem + ".bin"),
            Path.Combine(locked, LockedBaseFileStem + ".cue"),
            Path.Combine(root, "authored-edits"),
            Path.Combine(root, ".bootstrap-operations"));
    }

    public static UnusedLevel65BlankLevelLabManifest CreateManifest(
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        RequireExpectedPaths(paths);
        return new UnusedLevel65BlankLevelLabManifest(
            ManifestSchemaVersion,
            Profile.ProfileId,
            Profile.ProfileVersion,
            Key,
            WorkspaceLayoutVersion,
            WorkspaceLayoutRelativeRoot,
            Profile.DisplayName,
            LevelId,
            DataWadEntry,
            ObjectTableWadOffset,
            ObjectTableRelativeOffset,
            ObjectRecordCount,
            RelativeManifestPath(paths.RootPath, paths.LockedBaseImagePath),
            RelativeManifestPath(paths.RootPath, paths.LockedBaseCuePath),
            RelativeManifestPath(paths.RootPath, paths.AuthoredEditsDirectoryPath),
            CleanUsaImageSha256,
            Profile.PhysicalCloneProfileId,
            Profile.PhysicalCloneImageSha256,
            PhysicalCloneEvidence,
            Profile.LockedBaseProfileId,
            Profile.LockedBaseImageSha256,
            LockedBaseEvidence,
            Capabilities,
            ExternalGameDataRequired: true,
            GameDataEmbedded: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    public static void ValidateManifest(
        UnusedLevel65BlankLevelLabManifest manifest,
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(paths);
        RequireExpectedPaths(paths);
        UnusedLevel65BlankLevelLabManifest expected = CreateManifest(paths);
        if (manifest != expected)
            throw new InvalidDataException("The ID65 Blank-Level Lab manifest is stale, promoted, path-divergent, or bound to unapproved source/runtime evidence.");
    }

    private static void RequireExpectedPaths(UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        UnusedLevel65BlankLevelLabWorkspacePaths expected = CreateWorkspacePaths(paths.ContainerRootPath);
        if (!PathsEqual(paths.RootPath, expected.RootPath) ||
            !PathsEqual(paths.ManifestPath, expected.ManifestPath) ||
            !PathsEqual(paths.LockedBaseDirectoryPath, expected.LockedBaseDirectoryPath) ||
            !PathsEqual(paths.LockedBaseImagePath, expected.LockedBaseImagePath) ||
            !PathsEqual(paths.LockedBaseCuePath, expected.LockedBaseCuePath) ||
            !PathsEqual(paths.AuthoredEditsDirectoryPath, expected.AuthoredEditsDirectoryPath) ||
            !PathsEqual(paths.OperationsDirectoryPath, expected.OperationsDirectoryPath))
        {
            throw new InvalidDataException("The ID65 Blank-Level Lab paths do not match the isolated versioned workspace layout.");
        }
    }

    private static string RelativeManifestPath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidDataException("An ID65 lab manifest path escapes its versioned workspace root.");
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}

/// <summary>
/// Builds the exact locked ID65 lab substrate from a user's clean USA BIN/CUE.
/// All intermediate game data remains inside one uniquely owned operation
/// directory and is removed after transactional publication.
/// </summary>
public static class UnusedLevel65BlankLevelLabBootstrapper
{
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationJournalFileName = "operation-journal.json";
    private const string OperationKind = "unused-level-65-blank-level-lab-bootstrap";
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhasePreviousBackedUp = "previous-workspace-backed-up";
    private const string OperationPhaseWorkspacePublished = "workspace-published";
    private const string OperationPhaseNewWorkspaceRemoved = "new-workspace-removed";
    private const string OperationPhaseRollbackComplete = "rollback-complete";
    private const string OperationPhaseCommitted = "committed";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly string[] KnownBootstrapOperationPhases =
    [
        OperationPhaseStaging,
        OperationPhasePreviousBackedUp,
        OperationPhaseWorkspacePublished,
        OperationPhaseNewWorkspaceRemoved,
        OperationPhaseRollbackComplete,
        OperationPhaseCommitted
    ];

    public static async Task<UnusedLevel65BlankLevelLabBootstrapResult> BootstrapAsync(
        UnusedLevel65BlankLevelLabBootstrapRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string sourceImage = RequireExistingFile(request.SourceImagePath, "clean USA source BIN");
        string sourceCue = RequireExistingFile(request.SourceCuePath, "clean USA source CUE");
        UnusedLevel65BlankLevelLabWorkspacePaths paths =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(request.WorkspaceContainerPath);
        NativeLevelReplacementBaselineExporter.ValidateCue(sourceCue, sourceImage, "MODE2/2352");
        string sourceHashBefore = await HashFileAsync(sourceImage, cancellationToken);
        if (!string.Equals(
                sourceHashBefore,
                UnusedLevel65BlankLevelLabProfileRegistry.CleanUsaImageSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The ID65 lab bootstrap accepts only exact clean USA BIN {UnusedLevel65BlankLevelLabProfileRegistry.CleanUsaImageSha256}; selected SHA-256 was {sourceHashBefore}.");
        }

        RecoverOwnedBootstrapOperationDebris(paths);
        bool anyPublishedPath = PublishedPaths(paths).Any(File.Exists);
        bool allPublishedPaths = PublishedPaths(paths).All(File.Exists);
        if (allPublishedPaths)
        {
            try
            {
                UnusedLevel65BlankLevelLabManifest existing =
                    await ValidatePublishedWorkspaceAsync(paths, cancellationToken);
                request.TestStageHook?.Invoke("before-existing-workspace-reuse");
                return new UnusedLevel65BlankLevelLabBootstrapResult(
                    paths,
                    existing,
                    sourceHashBefore,
                    UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256,
                    UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256,
                    SourceImagePreserved: true,
                    AtomicPublicationCompleted: true,
                    OwnedTemporaryIntermediatesRemoved: !Directory.Exists(paths.OperationsDirectoryPath),
                    ReusedExistingLockedBase: true);
            }
            catch (Exception existingFailure) when (
                request.ReplaceInvalidExistingWorkspace &&
                existingFailure is InvalidDataException or IOException or JsonException)
            {
                // A caller must explicitly authorize replacement of an invalid
                // prior lab; publication below still preserves it transactionally.
            }
        }
        if (!allPublishedPaths && anyPublishedPath && !request.ReplaceInvalidExistingWorkspace)
        {
            throw new InvalidDataException(
                "The ID65 lab workspace is incomplete. Refusing to replace it without explicit ReplaceInvalidExistingWorkspace authorization.");
        }

        if (anyPublishedPath && !request.ReplaceInvalidExistingWorkspace)
        {
            throw new InvalidDataException(
                "The existing ID65 lab workspace failed validation. Refusing to replace it without explicit authorization.");
        }

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"bootstrap-{Guid.NewGuid():N}");
        EnsureOwnedOperationPath(operationRoot, paths.OperationsDirectoryPath);
        Directory.CreateDirectory(operationRoot);
        FileStream operationLease = CreateOperationLease(operationRoot);

        UnusedLevel65BlankLevelLabBootstrapResult? result = null;
        BootstrapPublication publication = CreateBootstrapPublication(
            operationRoot,
            paths);
        WriteBootstrapOperationJournal(publication, OperationPhaseStaging);
        bool recoveryComplete = true;
        try
        {
            string physicalDirectory = Path.Combine(operationRoot, "physical-clone");
            string publishDirectory = Path.Combine(operationRoot, "publish");
            Directory.CreateDirectory(physicalDirectory);
            Directory.CreateDirectory(publishDirectory);
            string physicalPrefix = Path.Combine(physicalDirectory, "id65-physical-clone-intermediate");
            string stagedImage = Path.Combine(publishDirectory, Path.GetFileName(paths.LockedBaseImagePath));
            string stagedCue = Path.Combine(publishDirectory, Path.GetFileName(paths.LockedBaseCuePath));
            string stagedManifest = Path.Combine(publishDirectory, Path.GetFileName(paths.ManifestPath));

            UnusedLevel65PhysicalCloneCandidateResult physical =
                await UnusedLevel65PhysicalCloneCandidateExporter.ExportAsync(
                    new UnusedLevel65PhysicalCloneCandidateRequest(
                        sourceImage,
                        sourceCue,
                        physicalPrefix + ".bin",
                        physicalPrefix + ".cue"),
                    cancellationToken);
            if (!physical.AtomicRenameCompleted ||
                !physical.IndependentPayloadReadbackVerified ||
                !string.Equals(
                    physical.OutputImageSha256,
                    UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The ID65 physical-clone stage no longer matches its checked runtime-passed base.");
            }

            UnusedLevel65DisplayNameCandidateResult locked =
                await UnusedLevel65DisplayNameCandidateExporter.ExportAsync(
                    new UnusedLevel65DisplayNameCandidateRequest(
                        physical.OutputImagePath,
                        physical.OutputCuePath,
                        stagedImage,
                        stagedCue),
                    cancellationToken);
            if (!locked.AtomicRenameCompleted ||
                !locked.ProtectedScopesPreserved ||
                !string.Equals(
                    locked.OutputImageSha256,
                    UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The ID65 lab locked-base stage no longer matches the exact display-name candidate.");
            }

            UnusedLevel65BlankLevelLabManifest manifest =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest, paths);
            string manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions) + "\n";
            await File.WriteAllTextAsync(
                stagedManifest,
                manifestJson,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            UnusedLevel65BlankLevelLabManifest stagedReadback =
                await ReadAndValidateManifestAsync(stagedManifest, paths, cancellationToken);
            NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");
            string stagedHash = await HashFileAsync(stagedImage, cancellationToken);
            if (!string.Equals(stagedHash, manifest.LockedBaseImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The staged ID65 lab image no longer matches its manifest hash.");

            Directory.CreateDirectory(paths.RootPath);
            Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
            Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
            await BeginPublishAtomicallyAsync(
                publication,
                paths,
                stagedImage,
                stagedCue,
                stagedManifest,
                request.TestStageHook,
                cancellationToken);

            string sourceHashAfter = await HashFileAsync(sourceImage, cancellationToken);
            if (!string.Equals(sourceHashAfter, sourceHashBefore, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The clean USA source BIN changed during ID65 lab bootstrap.");
            result = new UnusedLevel65BlankLevelLabBootstrapResult(
                paths,
                stagedReadback,
                sourceHashAfter,
                physical.OutputImageSha256,
                locked.OutputImageSha256,
                SourceImagePreserved: true,
                AtomicPublicationCompleted: true,
                OwnedTemporaryIntermediatesRemoved: true,
                ReusedExistingLockedBase: false);
            WriteBootstrapOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
        }
        catch (Exception publicationFailure)
        {
            if (!publication.Committed)
            {
                recoveryComplete = false;
                RollBackBootstrapPublication(
                    publication,
                    publicationFailure,
                    request.TestStageHook);
                recoveryComplete = publication.RecoveryComplete;
            }
            throw;
        }
        finally
        {
            operationLease.Dispose();
            if (recoveryComplete)
            {
                TryDeleteOwnedOperationDirectory(operationRoot, paths.OperationsDirectoryPath);
                TryDeleteEmptyDirectory(paths.OperationsDirectoryPath);
            }
        }

        if (Directory.Exists(operationRoot) || Directory.Exists(paths.OperationsDirectoryPath))
            throw new IOException("The ID65 lab bootstrap published its base but could not remove all owned temporary intermediates.");
        return result ?? throw new InvalidOperationException("The ID65 lab bootstrap completed without a result.");
    }

    public static async Task<UnusedLevel65BlankLevelLabManifest> ReadAndValidateManifestAsync(
        string manifestPath,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(manifestPath);
        string text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        UnusedLevel65BlankLevelLabManifest manifest = JsonSerializer.Deserialize<UnusedLevel65BlankLevelLabManifest>(
            text,
            ManifestJsonOptions) ?? throw new InvalidDataException("The ID65 lab manifest is empty.");
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest, paths);
        return manifest;
    }

    public static Task<UnusedLevel65BlankLevelLabManifest> ValidatePublishedWorkspaceAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        CancellationToken cancellationToken = default) =>
        ValidatePublishedWorkspaceCoreAsync(paths, recoverStaleOperations: true, cancellationToken);

    private static async Task<UnusedLevel65BlankLevelLabManifest> ValidatePublishedWorkspaceCoreAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        bool recoverStaleOperations,
        CancellationToken cancellationToken)
    {
        if (recoverStaleOperations)
            RecoverOwnedBootstrapOperationDebris(paths);
        if (!File.Exists(paths.ManifestPath) ||
            !File.Exists(paths.LockedBaseImagePath) ||
            !File.Exists(paths.LockedBaseCuePath))
        {
            throw new InvalidDataException("The ID65 lab workspace is missing its manifest or locked BIN/CUE.");
        }

        UnusedLevel65BlankLevelLabManifest manifest =
            await ReadAndValidateManifestAsync(paths.ManifestPath, paths, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.LockedBaseCuePath,
            paths.LockedBaseImagePath,
            "MODE2/2352");
        string hash = await HashFileAsync(paths.LockedBaseImagePath, cancellationToken);
        if (!string.Equals(hash, manifest.LockedBaseImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ID65 lab locked BIN does not match its manifest hash.");
        return manifest;
    }

    private static async Task BeginPublishAtomicallyAsync(
        BootstrapPublication publication,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        string stagedImage,
        string stagedCue,
        string stagedManifest,
        Action<string>? testStageHook,
        CancellationToken cancellationToken)
    {
        (string Staged, BootstrapPublicationFile File)[] files =
        [
            (stagedImage, publication.Files.Single(file => file.Role == "locked-base-image")),
            (stagedCue, publication.Files.Single(file => file.Role == "locked-base-cue")),
            (stagedManifest, publication.Files.Single(file => file.Role == "lab-manifest"))
        ];
        foreach ((_, BootstrapPublicationFile file) in files)
        {
            if (!File.Exists(file.DestinationPath))
                continue;
            File.Move(file.DestinationPath, file.BackupPath);
            file.BackedUp = true;
            WriteBootstrapOperationJournal(publication, OperationPhasePreviousBackedUp);
        }
        foreach ((string staged, BootstrapPublicationFile file) in files)
        {
            File.Move(staged, file.DestinationPath);
            file.Published = true;
            WriteBootstrapOperationJournal(publication, OperationPhaseWorkspacePublished);
        }
        testStageHook?.Invoke("after-workspace-publication");
        await ValidatePublishedWorkspaceCoreAsync(
            paths,
            recoverStaleOperations: false,
            cancellationToken);
    }

    private static BootstrapPublication CreateBootstrapPublication(
        string operationRoot,
        UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        new(
            operationRoot,
            [
                new BootstrapPublicationFile(
                    "locked-base-image",
                    paths.LockedBaseImagePath,
                    Path.Combine(operationRoot, "previous-locked-base.bin"),
                    File.Exists(paths.LockedBaseImagePath)),
                new BootstrapPublicationFile(
                    "locked-base-cue",
                    paths.LockedBaseCuePath,
                    Path.Combine(operationRoot, "previous-locked-base.cue"),
                    File.Exists(paths.LockedBaseCuePath)),
                new BootstrapPublicationFile(
                    "lab-manifest",
                    paths.ManifestPath,
                    Path.Combine(operationRoot, "previous-lab-manifest.json"),
                    File.Exists(paths.ManifestPath))
            ]);

    private static void RollBackBootstrapPublication(
        BootstrapPublication publication,
        Exception publicationFailure,
        Action<string>? testStageHook)
    {
        List<Exception> recoveryFailures = [];
        foreach (BootstrapPublicationFile file in publication.Files.Where(file => file.Published))
        {
            try
            {
                if (File.Exists(file.DestinationPath))
                    File.Delete(file.DestinationPath);
            }
            catch (Exception recoveryFailure)
            {
                recoveryFailures.Add(recoveryFailure);
            }
        }
        if (recoveryFailures.Count == 0)
        {
            try
            {
                WriteBootstrapOperationJournal(publication, OperationPhaseNewWorkspaceRemoved);
            }
            catch (Exception recoveryFailure)
            {
                recoveryFailures.Add(recoveryFailure);
            }
        }

        if (recoveryFailures.Count == 0 && publication.Files.Any(file => file.BackedUp))
        {
            try
            {
                testStageHook?.Invoke("before-previous-workspace-restore");
            }
            catch (Exception recoveryFailure)
            {
                recoveryFailures.Add(recoveryFailure);
            }
        }
        if (recoveryFailures.Count == 0)
        {
            foreach (BootstrapPublicationFile file in publication.Files.Where(file => file.BackedUp))
            {
                try
                {
                    if (File.Exists(file.DestinationPath))
                        throw new IOException($"The failed published {file.Role} still occupies its rollback destination.");
                    if (!File.Exists(file.BackupPath))
                        throw new IOException($"The prior {file.Role} backup is missing during rollback.");
                    File.Move(file.BackupPath, file.DestinationPath);
                }
                catch (Exception recoveryFailure)
                {
                    recoveryFailures.Add(recoveryFailure);
                }
            }
        }

        if (recoveryFailures.Count > 0)
        {
            throw new IOException(
                "ID65 lab publication failed and recovery of the previous workspace was incomplete; the owned operation directory was preserved for restart recovery.",
                new AggregateException([publicationFailure, .. recoveryFailures]));
        }
        WriteBootstrapOperationJournal(publication, OperationPhaseRollbackComplete);
        publication.RecoveryComplete = true;
    }

    private static IEnumerable<string> PublishedPaths(UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        yield return paths.LockedBaseImagePath;
        yield return paths.LockedBaseCuePath;
        yield return paths.ManifestPath;
    }

    private static void EnsureOwnedOperationPath(string operationRoot, string operationsRoot)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(operationRoot)) ?? "";
        string expectedParent = Path.GetFullPath(operationsRoot).TrimEnd(Path.DirectorySeparatorChar);
        string name = Path.GetFileName(operationRoot);
        if (!string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), expectedParent, PathComparison()) ||
            !IsExactOwnedOperationName(name, "bootstrap-"))
        {
            throw new InvalidOperationException("Refusing to use a non-owned ID65 lab operation directory.");
        }
    }

    private static FileStream CreateOperationLease(string operationRoot)
    {
        return new FileStream(
            Path.Combine(operationRoot, ".active-operation.lock"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    private static void WriteBootstrapOperationJournal(
        BootstrapPublication publication,
        string phase)
    {
        if (!KnownBootstrapOperationPhases.Contains(phase, StringComparer.Ordinal))
            throw new InvalidOperationException($"Unknown bootstrap operation phase '{phase}'.");
        publication.Phase = phase;
        BootstrapOperationJournal journal = new(
            OperationJournalSchemaVersion,
            OperationKind,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion,
            phase,
            publication.Files.Select(file => new BootstrapOperationJournalFile(
                file.Role,
                file.DestinationPath,
                file.BackupPath,
                file.ExistedAtStart,
                file.BackedUp,
                file.Published)).ToArray());
        string journalPath = Path.Combine(publication.OperationRoot, OperationJournalFileName);
        string temporaryPath = journalPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(journal, ManifestJsonOptions) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, journalPath, overwrite: true);
    }

    private static void RecoverOwnedBootstrapOperationDebris(
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        string operationsRoot = paths.OperationsDirectoryPath;
        if (!Directory.Exists(operationsRoot))
            return;
        if ((File.GetAttributes(operationsRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The bootstrap operations root cannot be a symbolic link.");

        string[] entries = Directory.GetFileSystemEntries(operationsRoot);
        foreach (string entry in entries)
        {
            string name = Path.GetFileName(entry);
            if (!Directory.Exists(entry) ||
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 ||
                !IsExactOwnedOperationName(name, "bootstrap-"))
            {
                throw new InvalidDataException(
                    $"Refusing to clean non-owned bootstrap operation entry '{name}'.");
            }

            string leasePath = Path.Combine(entry, ".active-operation.lock");
            if (File.Exists(leasePath))
            {
                try
                {
                    using FileStream _ = new(
                        leasePath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException ex)
                {
                    throw new IOException(
                        $"Bootstrap operation '{name}' is still active; refusing stale cleanup.",
                        ex);
                }
            }
        }

        foreach (string entry in entries)
        {
            string name = Path.GetFileName(entry);
            string journalPath = Path.Combine(entry, OperationJournalFileName);
            if (File.Exists(journalPath))
            {
                BootstrapOperationJournal journal = ReadAndValidateBootstrapOperationJournal(
                    entry,
                    paths,
                    journalPath);
                RecoverBootstrapOperation(entry, journal);
            }
            else
            {
                bool hasPriorWorkspaceBackup = Directory
                    .EnumerateFileSystemEntries(entry, "previous-*", SearchOption.TopDirectoryOnly)
                    .Any();
                if (hasPriorWorkspaceBackup)
                {
                    throw new InvalidDataException(
                        $"Bootstrap operation '{name}' contains unjournaled prior-workspace backups; refusing automatic deletion.");
                }
                Directory.Delete(entry, recursive: true);
            }
        }

        TryDeleteEmptyDirectory(operationsRoot);
        if (Directory.Exists(operationsRoot))
            throw new IOException("Owned stale bootstrap operation debris could not be removed.");
    }

    private static BootstrapOperationJournal ReadAndValidateBootstrapOperationJournal(
        string operationRoot,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        string journalPath)
    {
        BootstrapOperationJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<BootstrapOperationJournal>(
                File.ReadAllText(journalPath),
                ManifestJsonOptions) ?? throw new InvalidDataException("The bootstrap operation journal is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The bootstrap operation journal is malformed; preserving recovery data.", ex);
        }

        Dictionary<string, (string Destination, string Backup)> expected = new(StringComparer.Ordinal)
        {
            ["locked-base-image"] = (
                paths.LockedBaseImagePath,
                Path.Combine(operationRoot, "previous-locked-base.bin")),
            ["locked-base-cue"] = (
                paths.LockedBaseCuePath,
                Path.Combine(operationRoot, "previous-locked-base.cue")),
            ["lab-manifest"] = (
                paths.ManifestPath,
                Path.Combine(operationRoot, "previous-lab-manifest.json"))
        };
        if (journal.SchemaVersion != OperationJournalSchemaVersion ||
            journal.OperationKind != OperationKind ||
            journal.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            journal.ProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            !KnownBootstrapOperationPhases.Contains(journal.Phase, StringComparer.Ordinal) ||
            journal.Files.Count != expected.Count ||
            journal.Files.Select(file => file.Role).Distinct(StringComparer.Ordinal).Count() != expected.Count)
        {
            throw new InvalidDataException(
                "The bootstrap operation journal has stale identity, state, or profile data; preserving recovery data.");
        }
        foreach (BootstrapOperationJournalFile file in journal.Files)
        {
            if (!expected.TryGetValue(file.Role, out (string Destination, string Backup) exact) ||
                !PathsEqual(file.DestinationPath, exact.Destination) ||
                !PathsEqual(file.BackupPath, exact.Backup) ||
                (file.BackedUp && !file.ExistedAtStart))
            {
                throw new InvalidDataException(
                    "The bootstrap operation journal has non-exact paths or impossible backup state; preserving recovery data.");
            }
            RejectBootstrapRecoveryReparsePoint(file.DestinationPath, $"{file.Role} recovery destination");
            RejectBootstrapRecoveryReparsePoint(file.BackupPath, $"{file.Role} recovery backup");
        }
        return journal;
    }

    private static void RecoverBootstrapOperation(
        string operationRoot,
        BootstrapOperationJournal journal)
    {
        if (journal.Phase == OperationPhaseCommitted)
        {
            if (journal.Files.Any(file => !File.Exists(file.DestinationPath)))
            {
                throw new InvalidDataException(
                    "A committed bootstrap recovery journal has an incomplete published workspace; preserving its backups.");
            }
            Directory.Delete(operationRoot, recursive: true);
            return;
        }

        foreach (BootstrapOperationJournalFile file in journal.Files)
        {
            bool destinationExists = File.Exists(file.DestinationPath);
            bool backupExists = File.Exists(file.BackupPath);
            if (file.ExistedAtStart)
            {
                if (backupExists)
                {
                    if (destinationExists)
                        File.Delete(file.DestinationPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);
                    File.Move(file.BackupPath, file.DestinationPath);
                }
                else if (!destinationExists ||
                         (file.BackedUp &&
                          journal.Phase != OperationPhaseNewWorkspaceRemoved &&
                          journal.Phase != OperationPhaseRollbackComplete))
                {
                    throw new InvalidDataException(
                        $"An incomplete bootstrap rollback lost its prior {file.Role} backup; preserving recovery evidence.");
                }
            }
            else
            {
                if (backupExists)
                    throw new InvalidDataException($"Bootstrap recovery found an unexpected prior {file.Role} backup.");
                if (destinationExists)
                    File.Delete(file.DestinationPath);
            }
        }
        Directory.Delete(operationRoot, recursive: true);
    }

    private static void RejectBootstrapRecoveryReparsePoint(string path, string label)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The bootstrap {label} cannot be a symbolic link.");
        }
    }

    private static bool IsExactOwnedOperationName(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            name.Length != prefix.Length + 32)
        {
            return false;
        }
        string id = name[prefix.Length..];
        return id.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f')) &&
            Guid.TryParseExact(id, "N", out _);
    }

    private static void TryDeleteOwnedOperationDirectory(string operationRoot, string operationsRoot)
    {
        try
        {
            EnsureOwnedOperationPath(operationRoot, operationsRoot);
            if (Directory.Exists(operationRoot))
                Directory.Delete(operationRoot, recursive: true);
        }
        catch
        {
            // The caller checks for surviving debris and reports a hard failure
            // after preserving any primary export exception.
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path, recursive: false);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static string RequireExistingFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"A {label} path is required.");
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Missing {label}.", fullPath);
        return fullPath;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            PathComparison());

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private sealed record BootstrapOperationJournal(
        int SchemaVersion,
        string OperationKind,
        string ProfileId,
        int ProfileVersion,
        string Phase,
        IReadOnlyList<BootstrapOperationJournalFile> Files);

    private sealed record BootstrapOperationJournalFile(
        string Role,
        string DestinationPath,
        string BackupPath,
        bool ExistedAtStart,
        bool BackedUp,
        bool Published);

    private sealed class BootstrapPublication(
        string operationRoot,
        IReadOnlyList<BootstrapPublicationFile> files)
    {
        public string OperationRoot { get; } = operationRoot;
        public IReadOnlyList<BootstrapPublicationFile> Files { get; } = files;
        public string Phase { get; set; } = OperationPhaseStaging;
        public bool Committed { get; set; }
        public bool RecoveryComplete { get; set; }
    }

    private sealed class BootstrapPublicationFile(
        string role,
        string destinationPath,
        string backupPath,
        bool existedAtStart)
    {
        public string Role { get; } = role;
        public string DestinationPath { get; } = destinationPath;
        public string BackupPath { get; } = backupPath;
        public bool ExistedAtStart { get; } = existedAtStart;
        public bool BackedUp { get; set; }
        public bool Published { get; set; }
    }
}
