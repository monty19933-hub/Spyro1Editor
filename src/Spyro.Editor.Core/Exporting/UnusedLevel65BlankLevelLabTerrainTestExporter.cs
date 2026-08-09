using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65BlankLevelLabTerrainTestRequest(
    string WorkspaceContainerPath,
    string TerrainEditsPath,
    string TestKey,
    string TestDisplayName,
    bool ReplaceExistingTest = false,
    bool RequestFinderReveal = true,
    Action<string>? TestStageHook = null);

public sealed record UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt(
    string Kind,
    int Count);

public sealed record UnusedLevel65BlankLevelLabTerrainTestLoadCodeReceipt(
    string TestName,
    int LevelId,
    string TargetSelection,
    string InputCode);

public sealed record UnusedLevel65BlankLevelLabTerrainTestReceipt(
    int ReceiptSchemaVersion,
    string ProfileId,
    int ProfileVersion,
    string WorkspaceKey,
    string TestKey,
    string TestDisplayName,
    string WorkspaceRootPath,
    string ManifestPath,
    string SourceTerrainEditsPath,
    string TerrainEditsSnapshotPath,
    string TerrainEditsSnapshotSha256,
    string SourceOverlayPath,
    string SourceSearchPath,
    string LockedBaseImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    string RuntimeChecklistPath,
    string? FinderHelperPath,
    string StaticReceiptPath,
    string LockedBaseImageSha256,
    string OutputImageSha256,
    string OutputPlanSha256,
    int PatchCount,
    IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt> PatchKinds,
    int VerifiedRawSectorCount,
    bool LogicalPatchReadbackVerified,
    bool LockedBasePreserved,
    bool PromotionAuthorized,
    bool NormalCreateBinAuthorized,
    IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestLoadCodeReceipt> LoadCodes);

public sealed record UnusedLevel65BlankLevelLabTerrainTestPaths(
    UnusedLevel65BlankLevelLabWorkspacePaths Lab,
    string CacheDirectoryPath,
    string WadAnalysisPath,
    string SourceOverlayPath,
    string SourceSearchPath,
    string TestsDirectoryPath,
    string TestDirectoryPath,
    string OutputPrefix,
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    string TerrainEditsSnapshotPath,
    string RuntimeChecklistPath,
    string FinderHelperPath,
    string StaticReceiptPath,
    string OperationsDirectoryPath);

public sealed record UnusedLevel65BlankLevelLabTerrainTestResult(
    UnusedLevel65BlankLevelLabTerrainTestPaths Paths,
    TerrainPatchResult Terrain,
    UnusedLevel65BlankLevelLabTerrainTestReceipt Receipt,
    RuntimeCandidateFinderReveal? FinderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    string LockedBaseImageSha256,
    string OutputImageSha256,
    int AuthoredEditCount,
    int VerifiedRawSectorCount,
    bool LogicalPatchReadbackVerified,
    bool LockedBasePreserved,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

/// <summary>
/// Produces a disposable ID65 runtime test from an exact, locked Blank-Level
/// Lab base. This path accepts only existing high-detail Z edits, preserves
/// the locked base, and remains isolated from normal Create BIN and releases.
/// </summary>
public static class UnusedLevel65BlankLevelLabTerrainTestExporter
{
    private const int WadLba = 37;
    private const int ReceiptSchemaVersion = 2;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationJournalFileName = "operation-journal.json";
    private const string OperationKind = "unused-level-65-blank-level-lab-terrain-test";
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhasePreviousBackedUp = "previous-candidate-backed-up";
    private const string OperationPhaseCandidatePublished = "candidate-published";
    private const string OperationPhaseNewCandidateRemoved = "new-candidate-removed";
    private const string OperationPhaseRollbackComplete = "rollback-complete";
    private const string OperationPhaseCommitted = "committed";
    private const string CacheVersion = "terrain-source-v1";
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly Regex TestKeyPattern = new(
        "^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$|^[a-z0-9]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RuntimeKeyPattern = new(
        "^(?<sector>[0-9]+):(?<face>[0-9]+):hp$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] KnownCandidateOperationPhases =
    [
        OperationPhaseStaging,
        OperationPhasePreviousBackedUp,
        OperationPhaseCandidatePublished,
        OperationPhaseNewCandidateRemoved,
        OperationPhaseRollbackComplete,
        OperationPhaseCommitted
    ];

    public static async Task<UnusedLevel65BlankLevelLabTerrainTestResult> CreateDisposableTerrainTestAsync(
        UnusedLevel65BlankLevelLabTerrainTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string testKey = RequireTestKey(request.TestKey);
        string displayName = RequireDisplayName(request.TestDisplayName);
        UnusedLevel65BlankLevelLabTerrainTestPaths paths = CreatePaths(
            request.WorkspaceContainerPath,
            testKey);
        RecoverOwnedTerrainTestOperationDebris(paths.OperationsDirectoryPath);
        UnusedLevel65BlankLevelLabManifest manifest =
            await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(
                paths.Lab,
                cancellationToken);
        if (manifest.PromotionAuthorized ||
            manifest.NormalCreateBinEnabled ||
            manifest.Capabilities.ExistingHpZTerrainEdits !=
                UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly ||
            manifest.Capabilities.StructuralTerrainGrowth !=
                UnusedLevel65BlankLevelLabCapabilityState.Unavailable)
        {
            throw new InvalidDataException(
                "The ID65 lab manifest no longer permits only guarded research HP-Z terrain tests.");
        }

        string terrainEditsPath = RequireLabAuthoredEditsPath(
            request.TerrainEditsPath,
            paths.Lab.AuthoredEditsDirectoryPath);
        byte[] terrainEditsSnapshotBytes = await File.ReadAllBytesAsync(
            terrainEditsPath,
            cancellationToken);
        string terrainEditsSnapshotSha256 = HashBytes(terrainEditsSnapshotBytes);
        IReadOnlyList<string> editedRuntimeKeys = ValidateExistingHpZEdits(terrainEditsSnapshotBytes);
        string lockedHashBefore = await HashFileAsync(paths.Lab.LockedBaseImagePath, cancellationToken);
        if (!string.Equals(
                lockedHashBefore,
                manifest.LockedBaseImageSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The ID65 lab locked base changed before terrain-test export.");
        }

        await EnsureSourceTerrainCacheAsync(paths, editedRuntimeKeys, cancellationToken);
        PrepareDestination(paths, request.ReplaceExistingTest);
        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"terrain-test-{Guid.NewGuid():N}");
        EnsureDescendant(operationRoot, paths.OperationsDirectoryPath, "terrain-test operation");
        Directory.CreateDirectory(operationRoot);
        FileStream operationLease = CreateOperationLease(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        Directory.CreateDirectory(stagedDirectory);
        string stagedPrefix = Path.Combine(stagedDirectory, testKey);
        string stagedTerrainEditsSnapshotPath = stagedPrefix + "-terrain-edits-snapshot.json";

        UnusedLevel65BlankLevelLabTerrainTestResult? result = null;
        CandidatePublication publication = new(
            operationRoot,
            testKey,
            paths.TestDirectoryPath,
            Path.Combine(operationRoot, "previous-candidate"));
        WriteCandidateOperationJournal(publication, OperationPhaseStaging);
        bool recoveryComplete = true;
        try
        {
            await File.WriteAllBytesAsync(
                stagedTerrainEditsSnapshotPath,
                terrainEditsSnapshotBytes,
                cancellationToken);
            if (!string.Equals(
                    await HashFileAsync(stagedTerrainEditsSnapshotPath, cancellationToken),
                    terrainEditsSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The immutable terrain-edit snapshot failed staged SHA-256 readback.");
            }
            ValidateExistingHpZEdits(stagedTerrainEditsSnapshotPath);
            TerrainPatchResult stagedTerrain = await TerrainPatchExporter.ExportAsync(
                new TerrainPatchRequest(
                    paths.Lab.LockedBaseImagePath,
                    paths.Lab.LockedBaseCuePath,
                    stagedPrefix,
                    UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                    RamPath: "",
                    SourceSearchPath: paths.SourceSearchPath,
                    TerrainEditsPath: stagedTerrainEditsSnapshotPath,
                    CustomTexturesPath: "",
                    WriteImage: true,
                    NativeTextureRelocationsPath: "",
                    ConsumeDisposableSourceImage: false),
                cancellationToken);
            ValidateTerrainPlan(stagedTerrain, editedRuntimeKeys);
            int verifiedRawSectors = VerifyRawSectorsAndLogicalReadback(
                paths.Lab.LockedBaseImagePath,
                stagedTerrain.OutputImagePath,
                stagedTerrain.Plan);
            string outputHash = await HashFileAsync(stagedTerrain.OutputImagePath, cancellationToken);
            string lockedHashAfterExport = await HashFileAsync(paths.Lab.LockedBaseImagePath, cancellationToken);
            if (!string.Equals(lockedHashAfterExport, lockedHashBefore, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The disposable terrain exporter consumed or changed the locked lab base.");

            NativeLevelReplacementBaselineExporter.ValidateCue(
                stagedTerrain.OutputCuePath,
                stagedTerrain.OutputImagePath,
                "MODE2/2352");
            string stagedChecklist = stagedPrefix + "-runtime-checklist.md";
            bool includeFinderReveal = request.RequestFinderReveal && OperatingSystem.IsMacOS();
            RuntimeCandidateFinderReveal? stagedReveal = includeFinderReveal
                ? await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(
                    stagedTerrain.OutputCuePath,
                    cancellationToken)
                : null;
            IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
                RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
            VerifyLoadCodes(loadCodes);

            RuntimeCandidateFinderReveal? finalReveal = stagedReveal == null
                ? null
                : new RuntimeCandidateFinderReveal(
                    paths.OutputCuePath,
                    paths.OutputImagePath,
                    paths.FinderHelperPath,
                    $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
                    CuePairingVerified: true,
                    HelperIsExecutable: stagedReveal.HelperIsExecutable);
            string checklist = BuildChecklist(
                displayName,
                outputHash,
                finalReveal,
                loadCodes,
                stagedTerrain.Plan,
                paths.OutputCuePath,
                paths.TestDirectoryPath);
            VerifyChecklistReadback(checklist, finalReveal, loadCodes, paths.OutputCuePath);
            await File.WriteAllTextAsync(
                stagedChecklist,
                checklist,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            TerrainPatchPlan publishedPlan = stagedTerrain.Plan with
            {
                OutputImagePath = paths.OutputImagePath,
                OutputCuePath = paths.OutputCuePath,
                TerrainEditsPath = paths.TerrainEditsSnapshotPath
            };
            await File.WriteAllTextAsync(
                stagedTerrain.OutputPlanPath,
                JsonSerializer.Serialize(publishedPlan, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            string outputPlanSha256 = await HashFileAsync(
                stagedTerrain.OutputPlanPath,
                cancellationToken);

            string stagedReceiptPath = stagedPrefix + "-static-readback-receipt.json";
            UnusedLevel65BlankLevelLabTerrainTestReceipt stagedReceipt = BuildReceipt(
                testKey,
                displayName,
                paths,
                terrainEditsPath,
                terrainEditsSnapshotSha256,
                finalReveal,
                lockedHashBefore,
                outputHash,
                outputPlanSha256,
                publishedPlan,
                verifiedRawSectors,
                loadCodes);
            await File.WriteAllTextAsync(
                stagedReceiptPath,
                JsonSerializer.Serialize(stagedReceipt, ReceiptJsonOptions) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            await ReadAndValidateReceiptContractAsync(
                stagedReceiptPath,
                stagedReceipt,
                cancellationToken);

            VerifyStagedArtifactNames(
                stagedTerrain,
                stagedChecklist,
                stagedReveal?.HelperPath,
                stagedTerrainEditsSnapshotPath,
                stagedReceiptPath,
                testKey);
            BeginCandidatePublication(
                publication,
                stagedDirectory,
                request.ReplaceExistingTest);
            request.TestStageHook?.Invoke("after-candidate-publication");

            TerrainPatchResult publishedTerrain = new(
                paths.OutputImagePath,
                paths.OutputCuePath,
                paths.OutputPlanPath,
                publishedPlan,
                WroteImage: true);
            NativeLevelReplacementBaselineExporter.ValidateCue(
                publishedTerrain.OutputCuePath,
                publishedTerrain.OutputImagePath,
                "MODE2/2352");
            VerifyRawSectorsAndLogicalReadback(
                paths.Lab.LockedBaseImagePath,
                publishedTerrain.OutputImagePath,
                publishedTerrain.Plan);
            string checklistReadback = await File.ReadAllTextAsync(
                paths.RuntimeChecklistPath,
                cancellationToken);
            VerifyChecklistReadback(
                checklistReadback,
                finalReveal,
                loadCodes,
                paths.OutputCuePath);
            if (finalReveal != null)
            {
                if (!OperatingSystem.IsMacOS() ||
                    !File.Exists(paths.FinderHelperPath) ||
                    File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
                {
                    throw new InvalidDataException("The published Finder helper is missing or is not exact 0755.");
                }
            }
            else if (File.Exists(paths.FinderHelperPath))
            {
                throw new InvalidDataException("A Finder helper was emitted when the platform-aware handoff disabled it.");
            }

            string lockedHashAfterPublication = await HashFileAsync(
                paths.Lab.LockedBaseImagePath,
                cancellationToken);
            if (!string.Equals(lockedHashAfterPublication, lockedHashBefore, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The locked lab base changed during candidate publication.");
            if (!string.Equals(
                    await HashFileAsync(paths.OutputImagePath, cancellationToken),
                    outputHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The published terrain-test BIN differs from its verified staged image.");
            }

            UnusedLevel65BlankLevelLabTerrainTestReceipt receiptReadback =
                await ReadAndValidateReceiptCoreAsync(
                    paths,
                    recoverStaleOperations: false,
                    cancellationToken);
            await ReadAndValidateReceiptContractAsync(
                paths.StaticReceiptPath,
                stagedReceipt,
                cancellationToken);

            result = new UnusedLevel65BlankLevelLabTerrainTestResult(
                paths,
                publishedTerrain,
                receiptReadback,
                finalReveal,
                loadCodes,
                lockedHashAfterPublication,
                outputHash,
                editedRuntimeKeys.Count,
                verifiedRawSectors,
                LogicalPatchReadbackVerified: true,
                LockedBasePreserved: true,
                FinderHandoffVerified: finalReveal != null,
                PromotionAuthorized: false,
                NormalCreateBinEnabled: false);
            WriteCandidateOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
        }
        catch (Exception publicationFailure)
        {
            if (!publication.Committed)
            {
                recoveryComplete = false;
                RollBackCandidatePublication(
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
                TryDeleteOwnedDirectory(operationRoot, paths.OperationsDirectoryPath);
                TryDeleteEmptyDirectory(paths.OperationsDirectoryPath);
            }
        }

        if (Directory.Exists(operationRoot))
            throw new IOException("The terrain-test operation directory could not be removed.");
        return result ?? throw new InvalidOperationException("The terrain-test export completed without a result.");
    }

    public static UnusedLevel65BlankLevelLabTerrainTestPaths CreatePaths(
        string workspaceContainerPath,
        string testKey)
    {
        string normalizedKey = RequireTestKey(testKey);
        UnusedLevel65BlankLevelLabWorkspacePaths lab =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(workspaceContainerPath);
        string cache = Path.Combine(lab.RootPath, "generated-cache", CacheVersion);
        string tests = Path.Combine(lab.RootPath, "runtime-tests");
        string test = Path.Combine(tests, normalizedKey);
        string prefix = Path.Combine(test, normalizedKey);
        UnusedLevel65BlankLevelLabTerrainTestPaths result = new(
            lab,
            cache,
            Path.Combine(cache, "id65-wad-analysis.json"),
            Path.Combine(cache, "id65-source-overlay.json"),
            Path.Combine(cache, "id65-source-search.json"),
            tests,
            test,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + ".terrain-patch-plan.json",
            prefix + "-terrain-edits-snapshot.json",
            prefix + "-runtime-checklist.md",
            prefix + "-Reveal-in-Finder.command",
            prefix + "-static-readback-receipt.json",
            Path.Combine(lab.RootPath, ".terrain-test-operations"));
        RequireAllPathsInsideLab(result);
        return result;
    }

    public static Task<UnusedLevel65BlankLevelLabTerrainTestReceipt> ReadAndValidateReceiptAsync(
        UnusedLevel65BlankLevelLabTerrainTestPaths paths,
        CancellationToken cancellationToken = default) =>
        ReadAndValidateReceiptCoreAsync(paths, recoverStaleOperations: true, cancellationToken);

    private static async Task<UnusedLevel65BlankLevelLabTerrainTestReceipt> ReadAndValidateReceiptCoreAsync(
        UnusedLevel65BlankLevelLabTerrainTestPaths paths,
        bool recoverStaleOperations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        RequireAllPathsInsideLab(paths);
        string testKey = Path.GetFileName(paths.TestDirectoryPath);
        UnusedLevel65BlankLevelLabTerrainTestPaths expectedPaths = CreatePaths(
            paths.Lab.ContainerRootPath,
            testKey);
        RequireExactPaths(paths, expectedPaths);
        if (recoverStaleOperations)
            RecoverOwnedTerrainTestOperationDebris(paths.OperationsDirectoryPath);
        if (!File.Exists(paths.StaticReceiptPath))
            throw new InvalidDataException("The disposable terrain test is missing its static/readback receipt.");

        UnusedLevel65BlankLevelLabTerrainTestReceipt receipt =
            await ReadReceiptAsync(paths.StaticReceiptPath, cancellationToken);
        RequireDisplayName(receipt.TestDisplayName);
        UnusedLevel65BlankLevelLabManifest manifest =
            await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(
                paths.Lab,
                cancellationToken);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestLoadCodeReceipt> expectedCodes =
            LoadCodeReceipts(loadCodes);

        if (receipt.ReceiptSchemaVersion != ReceiptSchemaVersion ||
            receipt.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            receipt.ProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            receipt.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            receipt.TestKey != testKey ||
            string.IsNullOrWhiteSpace(receipt.TestDisplayName) ||
            !PathsEqual(receipt.WorkspaceRootPath, paths.Lab.RootPath) ||
            !PathsEqual(receipt.ManifestPath, paths.Lab.ManifestPath) ||
            !PathsEqual(receipt.TerrainEditsSnapshotPath, paths.TerrainEditsSnapshotPath) ||
            !PathsEqual(receipt.SourceOverlayPath, paths.SourceOverlayPath) ||
            !PathsEqual(receipt.SourceSearchPath, paths.SourceSearchPath) ||
            !PathsEqual(receipt.LockedBaseImagePath, paths.Lab.LockedBaseImagePath) ||
            !PathsEqual(receipt.OutputImagePath, paths.OutputImagePath) ||
            !PathsEqual(receipt.OutputCuePath, paths.OutputCuePath) ||
            !PathsEqual(receipt.OutputPlanPath, paths.OutputPlanPath) ||
            !PathsEqual(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath) ||
            !PathsEqual(receipt.StaticReceiptPath, paths.StaticReceiptPath) ||
            receipt.PromotionAuthorized ||
            receipt.NormalCreateBinAuthorized ||
            !receipt.LogicalPatchReadbackVerified ||
            !receipt.LockedBasePreserved ||
            !receipt.LoadCodes.SequenceEqual(expectedCodes))
        {
            throw new InvalidDataException(
                "The disposable terrain static/readback receipt has stale profile, path, safety, or load-code fields.");
        }
        if (receipt.FinderHelperPath != null &&
            !PathsEqual(receipt.FinderHelperPath, paths.FinderHelperPath))
        {
            throw new InvalidDataException("The receipt Finder-helper path is not the exact candidate sidecar path.");
        }
        if (receipt.FinderHelperPath == null && File.Exists(paths.FinderHelperPath))
            throw new InvalidDataException("The receipt omits a Finder helper that exists beside the candidate.");

        EnsureDescendant(
            receipt.SourceTerrainEditsPath,
            paths.Lab.AuthoredEditsDirectoryPath,
            "source terrain-edit provenance");
        if (!File.Exists(paths.TerrainEditsSnapshotPath))
            throw new InvalidDataException("The immutable terrain-edit snapshot is missing.");
        string terrainEditsSnapshotSha256 = await HashFileAsync(
            paths.TerrainEditsSnapshotPath,
            cancellationToken);
        if (!string.Equals(
                receipt.TerrainEditsSnapshotSha256,
                terrainEditsSnapshotSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The immutable terrain-edit snapshot SHA-256 failed receipt readback.");
        }
        IReadOnlyList<string> editedRuntimeKeys = ValidateExistingHpZEdits(
            paths.TerrainEditsSnapshotPath);
        if (!File.Exists(paths.SourceOverlayPath) || !File.Exists(paths.SourceSearchPath))
            throw new InvalidDataException("The receipt source-overlay/search cache is missing.");
        if (!File.Exists(paths.OutputImagePath) ||
            !File.Exists(paths.OutputCuePath) ||
            !File.Exists(paths.OutputPlanPath) ||
            !File.Exists(paths.RuntimeChecklistPath))
        {
            throw new InvalidDataException("The receipt references an incomplete disposable test artifact set.");
        }

        string planJson = await File.ReadAllTextAsync(paths.OutputPlanPath, cancellationToken);
        string outputPlanSha256 = await HashFileAsync(paths.OutputPlanPath, cancellationToken);
        if (!string.Equals(receipt.OutputPlanSha256, outputPlanSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The terrain patch-plan SHA-256 failed receipt readback.");
        TerrainPatchPlan plan = JsonSerializer.Deserialize<TerrainPatchPlan>(
            planJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The receipt terrain patch plan is empty.");
        TerrainPatchResult terrain = new(
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.OutputPlanPath,
            plan,
            WroteImage: true);
        ValidateTerrainPlan(terrain, editedRuntimeKeys);
        IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt> expectedKinds =
            PatchKindReceipts(plan);
        if (receipt.PatchCount != plan.PatchCount ||
            receipt.PatchCount != plan.Patches.Count ||
            !receipt.PatchKinds.SequenceEqual(expectedKinds) ||
            !PathsEqual(plan.OutputImagePath, paths.OutputImagePath) ||
            !PathsEqual(plan.OutputCuePath, paths.OutputCuePath) ||
            !PathsEqual(plan.TerrainEditsPath, paths.TerrainEditsSnapshotPath))
        {
            throw new InvalidDataException("The receipt patch count, kinds, paths, or edit binding differs from its plan.");
        }

        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        string lockedHash = await HashFileAsync(paths.Lab.LockedBaseImagePath, cancellationToken);
        string outputHash = await HashFileAsync(paths.OutputImagePath, cancellationToken);
        if (!string.Equals(lockedHash, manifest.LockedBaseImageSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.LockedBaseImageSha256, lockedHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.OutputImageSha256, outputHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The receipt locked-base or output SHA-256 failed artifact readback.");
        }
        int verifiedRawSectors = VerifyRawSectorsAndLogicalReadback(
            paths.Lab.LockedBaseImagePath,
            paths.OutputImagePath,
            plan);
        if (receipt.VerifiedRawSectorCount != verifiedRawSectors)
            throw new InvalidDataException("The receipt raw-sector count differs from MODE2 EDC/ECC readback.");

        RuntimeCandidateFinderReveal? reveal = null;
        if (receipt.FinderHelperPath != null)
        {
            if (!File.Exists(receipt.FinderHelperPath))
                throw new InvalidDataException("The receipt Finder helper is missing.");
            bool executable = !OperatingSystem.IsMacOS() ||
                File.GetUnixFileMode(receipt.FinderHelperPath) == ExactFinderMode();
            if (!executable)
                throw new InvalidDataException("The receipt Finder helper is not exact 0755.");
            reveal = new RuntimeCandidateFinderReveal(
                paths.OutputCuePath,
                paths.OutputImagePath,
                receipt.FinderHelperPath,
                $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
                CuePairingVerified: true,
                HelperIsExecutable: executable);
        }
        string checklist = await File.ReadAllTextAsync(paths.RuntimeChecklistPath, cancellationToken);
        VerifyChecklistReadback(checklist, reveal, loadCodes, paths.OutputCuePath);
        return receipt;
    }

    private static UnusedLevel65BlankLevelLabTerrainTestReceipt BuildReceipt(
        string testKey,
        string testDisplayName,
        UnusedLevel65BlankLevelLabTerrainTestPaths paths,
        string sourceTerrainEditsPath,
        string terrainEditsSnapshotSha256,
        RuntimeCandidateFinderReveal? reveal,
        string lockedBaseImageSha256,
        string outputImageSha256,
        string outputPlanSha256,
        TerrainPatchPlan plan,
        int verifiedRawSectorCount,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        return new UnusedLevel65BlankLevelLabTerrainTestReceipt(
            ReceiptSchemaVersion,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion,
            UnusedLevel65BlankLevelLabProfileRegistry.Key,
            testKey,
            testDisplayName,
            paths.Lab.RootPath,
            paths.Lab.ManifestPath,
            sourceTerrainEditsPath,
            paths.TerrainEditsSnapshotPath,
            terrainEditsSnapshotSha256,
            paths.SourceOverlayPath,
            paths.SourceSearchPath,
            paths.Lab.LockedBaseImagePath,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.OutputPlanPath,
            paths.RuntimeChecklistPath,
            reveal?.HelperPath,
            paths.StaticReceiptPath,
            lockedBaseImageSha256,
            outputImageSha256,
            outputPlanSha256,
            plan.PatchCount,
            PatchKindReceipts(plan),
            verifiedRawSectorCount,
            LogicalPatchReadbackVerified: true,
            LockedBasePreserved: true,
            PromotionAuthorized: false,
            NormalCreateBinAuthorized: false,
            LoadCodes: LoadCodeReceipts(loadCodes));
    }

    private static async Task<UnusedLevel65BlankLevelLabTerrainTestReceipt> ReadReceiptAsync(
        string receiptPath,
        CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(receiptPath, cancellationToken);
        return JsonSerializer.Deserialize<UnusedLevel65BlankLevelLabTerrainTestReceipt>(
            json,
            ReceiptJsonOptions) ?? throw new InvalidDataException("The disposable terrain receipt is empty.");
    }

    private static async Task ReadAndValidateReceiptContractAsync(
        string receiptPath,
        UnusedLevel65BlankLevelLabTerrainTestReceipt expected,
        CancellationToken cancellationToken)
    {
        UnusedLevel65BlankLevelLabTerrainTestReceipt actual =
            await ReadReceiptAsync(receiptPath, cancellationToken);
        string expectedCanonical = JsonSerializer.Serialize(expected, ReceiptJsonOptions);
        string actualCanonical = JsonSerializer.Serialize(actual, ReceiptJsonOptions);
        if (!string.Equals(expectedCanonical, actualCanonical, StringComparison.Ordinal))
            throw new InvalidDataException("The staged terrain-test receipt failed exact JSON readback.");
    }

    private static IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt> PatchKindReceipts(
        TerrainPatchPlan plan) =>
        plan.Patches
            .GroupBy(patch => patch.Kind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new UnusedLevel65BlankLevelLabTerrainTestPatchKindReceipt(
                group.Key,
                group.Count()))
            .ToArray();

    private static IReadOnlyList<UnusedLevel65BlankLevelLabTerrainTestLoadCodeReceipt> LoadCodeReceipts(
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes) =>
        loadCodes.Select(code => new UnusedLevel65BlankLevelLabTerrainTestLoadCodeReceipt(
            code.TestName,
            code.LevelId,
            code.TargetSelection,
            code.InputCode)).ToArray();

    private static void RequireExactPaths(
        UnusedLevel65BlankLevelLabTerrainTestPaths actual,
        UnusedLevel65BlankLevelLabTerrainTestPaths expected)
    {
        string[] actualPaths =
        [
            actual.Lab.RootPath,
            actual.CacheDirectoryPath,
            actual.WadAnalysisPath,
            actual.SourceOverlayPath,
            actual.SourceSearchPath,
            actual.TestsDirectoryPath,
            actual.TestDirectoryPath,
            actual.OutputPrefix,
            actual.OutputImagePath,
            actual.OutputCuePath,
            actual.OutputPlanPath,
            actual.TerrainEditsSnapshotPath,
            actual.RuntimeChecklistPath,
            actual.FinderHelperPath,
            actual.StaticReceiptPath,
            actual.OperationsDirectoryPath
        ];
        string[] expectedPaths =
        [
            expected.Lab.RootPath,
            expected.CacheDirectoryPath,
            expected.WadAnalysisPath,
            expected.SourceOverlayPath,
            expected.SourceSearchPath,
            expected.TestsDirectoryPath,
            expected.TestDirectoryPath,
            expected.OutputPrefix,
            expected.OutputImagePath,
            expected.OutputCuePath,
            expected.OutputPlanPath,
            expected.TerrainEditsSnapshotPath,
            expected.RuntimeChecklistPath,
            expected.FinderHelperPath,
            expected.StaticReceiptPath,
            expected.OperationsDirectoryPath
        ];
        if (!actualPaths.Zip(expectedPaths).All(pair => PathsEqual(pair.First, pair.Second)))
            throw new InvalidDataException("The terrain-test receipt paths do not match the isolated lab layout.");
    }

    private static async Task EnsureSourceTerrainCacheAsync(
        UnusedLevel65BlankLevelLabTerrainTestPaths paths,
        IReadOnlyList<string> editedRuntimeKeys,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.CacheDirectoryPath);
        WadAnalysisEnsureResult analysis = await WadAnalysisBuilder.EnsureCompatibleAsync(
            paths.Lab.LockedBaseImagePath,
            paths.WadAnalysisPath,
            cancellationToken: cancellationToken);
        if (analysis.EntryCount != 102)
            throw new InvalidDataException($"The locked lab WAD has {analysis.EntryCount} rows instead of the exact extended count 102.");

        string overlayTemporary = paths.SourceOverlayPath + $".{Guid.NewGuid():N}.tmp";
        string searchTemporary = paths.SourceSearchPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            SourceSceneOverlayResult overlay = await SourceSceneOverlayExporter.ExportAsync(
                paths.Lab.LockedBaseImagePath,
                paths.WadAnalysisPath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                overlayTemporary,
                modelSubfileIndex: 1,
                minSectorCount: 16,
                cancellationToken);
            if (overlay.WadEntry != 80 || overlay.SectorCount != 216 || overlay.HpFaces != 3887)
            {
                throw new InvalidDataException(
                    $"The ID65 locked terrain cache drifted: row={overlay.WadEntry}, sectors={overlay.SectorCount}, HP faces={overlay.HpFaces}.");
            }

            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayTemporary);
            TerrainSourceSearchResult search = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
                new SourceDerivedTerrainSourceSearchRequest(
                    paths.Lab.LockedBaseImagePath,
                    searchTemporary,
                    UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                    geometry),
                cancellationToken);
            if (search.Report.SectorCount <= 0 ||
                search.Report.MatchedSectorCount != search.Report.SectorCount ||
                search.Report.MissingSectorCount != 0 ||
                search.Report.AmbiguousSectorCount != 0)
            {
                throw new InvalidDataException(
                    "The ID65 source-derived terrain map is incomplete or ambiguous for its polygon-bearing sectors.");
            }

            foreach (string runtimeKey in editedRuntimeKeys)
            {
                TerrainSourceSearchEntry[] hits = search.Report.Results
                    .Where(entry => entry.Edit.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (hits.Length != 1 || hits[0].FullSectorHits.Count != 1)
                {
                    throw new InvalidDataException(
                        $"Edited HP face {runtimeKey} does not have exactly one locked-base source-sector binding.");
                }
            }

            File.Move(overlayTemporary, paths.SourceOverlayPath, overwrite: true);
            File.Move(searchTemporary, paths.SourceSearchPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(overlayTemporary);
            TryDeleteFile(searchTemporary);
        }
    }

    private static IReadOnlyList<string> ValidateExistingHpZEdits(string editsPath)
    {
        return ValidateExistingHpZEdits(File.ReadAllBytes(editsPath));
    }

    private static IReadOnlyList<string> ValidateExistingHpZEdits(ReadOnlyMemory<byte> editsBytes)
    {
        using JsonDocument document = JsonDocument.Parse(editsBytes);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) ||
            edits.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The lab terrain edit file has no edits array.");
        }

        JsonElement[] rows = edits.EnumerateArray().ToArray();
        if (rows.Length == 0)
            throw new InvalidDataException("A disposable lab terrain test requires at least one HP-Z edit.");
        if (document.RootElement.TryGetProperty("editCount", out JsonElement editCount) &&
            editCount.ValueKind == JsonValueKind.Number &&
            editCount.GetInt32() != rows.Length)
        {
            throw new InvalidDataException("The lab terrain edit count does not match its edits array.");
        }

        HashSet<string> runtimeKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement edit in rows)
        {
            string runtimeKey = GetString(edit, "runtimeKey");
            Match runtimeMatch = RuntimeKeyPattern.Match(runtimeKey);
            int sectorIndex = GetInt32(edit, "sectorIndex", -1);
            int faceIndex = GetInt32(edit, "faceIndex", -1);
            if (!runtimeMatch.Success ||
                !int.TryParse(runtimeMatch.Groups["sector"].Value, out int runtimeSector) ||
                !int.TryParse(runtimeMatch.Groups["face"].Value, out int runtimeFace) ||
                runtimeSector != sectorIndex ||
                runtimeFace != faceIndex ||
                !GetString(edit, "detail").Equals("hp", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Terrain edit {runtimeKey} is not an exact existing HP face identity.");
            }
            if (!runtimeKeys.Add(runtimeKey))
                throw new InvalidDataException($"Terrain edit {runtimeKey} is duplicated.");

            string structureMode = GetString(edit, "structureEditMode");
            if (!string.IsNullOrWhiteSpace(structureMode) &&
                !structureMode.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Terrain edit {runtimeKey} requests structural terrain; the lab accepts existing HP-Z edits only.");
            }
            foreach (string forbidden in new[]
            {
                "textureEditMode",
                "textureIdEdited",
                "nativeTextureVisualEdit",
                "nativeSurfaceBehaviorEdit",
                "nativeSurfaceType",
                "nativeSurfaceParam1",
                "nativeSurfaceParam2",
                "recordMutation",
                "editKind",
                "added",
                "removed"
            })
            {
                if (edit.TryGetProperty(forbidden, out JsonElement value) &&
                    value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined &&
                    !(value.ValueKind == JsonValueKind.False))
                {
                    throw new InvalidDataException(
                        $"Terrain edit {runtimeKey} contains forbidden non-HP-Z field {forbidden}.");
                }
            }

            IReadOnlyList<int> vertexIndexes = ReadIntArray(edit, "vertexIndexes");
            IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
            IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
            IReadOnlyList<(float X, float Y)> originalPoints = ReadPoints(edit, "originalPoints");
            IReadOnlyList<(float X, float Y)> editedPoints = ReadPoints(edit, "editedPoints");
            if (vertexIndexes.Count is < 3 or > 4 ||
                originalZ.Count != vertexIndexes.Count ||
                editedZ.Count != originalZ.Count ||
                originalPoints.Count != originalZ.Count ||
                editedPoints.Count != originalPoints.Count)
            {
                throw new InvalidDataException(
                    $"Terrain edit {runtimeKey} does not contain one complete 3/4-corner HP face preimage.");
            }
            if (!originalPoints.SequenceEqual(editedPoints))
                throw new InvalidDataException($"Terrain edit {runtimeKey} changes XY; the lab accepts Z edits only.");
            if (!originalZ.Zip(editedZ).Any(pair => Math.Abs(pair.First - pair.Second) > 0.001f))
                throw new InvalidDataException($"Terrain edit {runtimeKey} does not change a Z value.");
            if (originalZ.Concat(editedZ).Any(value => !float.IsFinite(value)))
                throw new InvalidDataException($"Terrain edit {runtimeKey} contains a non-finite Z value.");

            if (edit.TryGetProperty("vertexDeltaXY", out JsonElement xyDeltas) &&
                xyDeltas.ValueKind == JsonValueKind.Array &&
                ReadPoints(edit, "vertexDeltaXY").Any(point =>
                    Math.Abs(point.X) > 0.001f || Math.Abs(point.Y) > 0.001f))
            {
                throw new InvalidDataException($"Terrain edit {runtimeKey} contains a non-zero XY delta.");
            }
            if (edit.TryGetProperty("vertexDeltaZ", out JsonElement _) &&
                !ReadFloatArray(edit, "vertexDeltaZ")
                    .SequenceEqual(originalZ.Zip(editedZ).Select(pair => pair.Second - pair.First)))
            {
                throw new InvalidDataException($"Terrain edit {runtimeKey} has inconsistent vertex Z deltas.");
            }
        }

        return runtimeKeys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ValidateTerrainPlan(
        TerrainPatchResult result,
        IReadOnlyList<string> editedRuntimeKeys)
    {
        if (!result.WroteImage || !File.Exists(result.OutputImagePath) || !File.Exists(result.OutputCuePath))
            throw new InvalidDataException("The guarded terrain path did not produce a disposable BIN/CUE.");
        if (result.Plan.SkippedEdits.Count != 0)
        {
            throw new InvalidDataException(
                $"The guarded terrain export skipped edits: {string.Join(" | ", result.Plan.SkippedEdits)}");
        }
        if (result.Plan.Patches.Count == 0 ||
            result.Plan.Patches.Any(patch =>
                !patch.Kind.Equals("visual-hp-topology-safe", StringComparison.Ordinal) &&
                !patch.Kind.Equals("collision-triangle-fan", StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The lab terrain plan escaped the existing HP-Z plus exact collision-fan boundary.");
        }
        if (result.Plan.TerrainSideWalls.Count != 0 ||
            result.Plan.CustomTextureImportCount != 0 ||
            result.Plan.NativeTextureRelocationCount != 0)
        {
            throw new InvalidDataException(
                "The lab terrain plan emitted sidewalls, textures, or relocations outside HP-Z scope.");
        }
        foreach (string runtimeKey in editedRuntimeKeys)
        {
            if (!result.Plan.Patches.Any(patch =>
                    patch.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase) &&
                    patch.Kind.Equals("visual-hp-topology-safe", StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Edited face {runtimeKey} has no topology-safe visual patch.");
            }
        }
        if (!result.Plan.Patches.Any(patch => patch.Kind == "collision-triangle-fan"))
            throw new InvalidDataException("The lab HP-Z edit did not resolve an exact native collision fan.");
    }

    private static int VerifyRawSectorsAndLogicalReadback(
        string lockedBaseImagePath,
        string outputImagePath,
        TerrainPatchPlan plan)
    {
        DiscLayout lockedLayout = DiscImage.DetectLayout(lockedBaseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (lockedLayout.SectorSize != 2352 || lockedLayout.UserOffset != 24 ||
            outputLayout.SectorSize != 2352 || outputLayout.UserOffset != 24)
        {
            throw new InvalidDataException("The lab terrain test requires exact MODE2/2352 raw images.");
        }

        using FileStream locked = File.OpenRead(lockedBaseImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        List<(int Lba, int SectorCount)> sectors = plan.Patches
            .Select(patch =>
            {
                long offset = ParseHexOffset(patch.WadRelativeOffset);
                byte[] before = ParseHexBytes(patch.BeforeHexPreview);
                byte[] after = ParseHexBytes(patch.AfterHexPreview);
                if (patch.ByteLength <= 0 ||
                    before.Length != patch.ByteLength ||
                    after.Length != patch.ByteLength)
                {
                    throw new InvalidDataException(
                        $"Terrain patch {patch.Kind} has an inconsistent encoded byte length.");
                }
                byte[] lockedBytes = DiscImage.ReadFileBytes(
                    locked,
                    lockedLayout,
                    WadLba,
                    offset,
                    patch.ByteLength);
                byte[] outputBytes = DiscImage.ReadFileBytes(
                    output,
                    outputLayout,
                    WadLba,
                    offset,
                    patch.ByteLength);
                if (!lockedBytes.SequenceEqual(before) || !outputBytes.SequenceEqual(after))
                {
                    throw new InvalidDataException(
                        $"Terrain patch {patch.Kind} at 0x{offset:X} failed exact locked/output readback.");
                }
                int first = checked(WadLba + (int)(offset / 2048));
                int count = checked((int)((offset + patch.ByteLength - 1) / 2048 - offset / 2048 + 1));
                return (first, count);
            })
            .SelectMany(range => Enumerable.Range(range.first, range.count))
            .Distinct()
            .Order()
            .Select(lba => (Lba: lba, SectorCount: 1))
            .ToList();
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            sectors);
        if (verified != sectors.Count)
            throw new InvalidDataException("Not every modified raw sector passed EDC/ECC verification.");
        return verified;
    }

    private static string BuildChecklist(
        string displayName,
        string outputHash,
        RuntimeCandidateFinderReveal? reveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        TerrainPatchPlan plan,
        string outputCuePath,
        string testDirectoryPath)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# {displayName} — disposable DuckStation checklist");
        builder.AppendLine();
        builder.AppendLine($"- BIN SHA-256: `{outputHash}`");
        builder.AppendLine($"- Lab profile: `{UnusedLevel65BlankLevelLabProfileRegistry.ProfileId}`");
        builder.AppendLine($"- Terrain edits: `{Path.GetFileName(plan.TerrainEditsPath)}`");
        builder.AppendLine("- Scope: existing high-detail Z terrain plus its exact native collision fan only.");
        builder.AppendLine("- Promotion: **false**. Normal Create BIN: **false**.");
        builder.AppendLine();
        if (reveal != null)
        {
            RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, reveal);
        }
        else
        {
            builder.AppendLine("## Candidate disc");
            builder.AppendLine();
            builder.AppendLine($"- CUE: `{Path.GetFileName(outputCuePath)}`");
            builder.AppendLine($"- Folder: `{testDirectoryPath}`");
            builder.AppendLine("- Finder reveal helper: unavailable or not requested on this platform.");
            builder.AppendLine("- DuckStation: load the **CUE**, not the BIN.");
            builder.AppendLine();
        }
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## Test conditions");
        builder.AppendLine();
        builder.AppendLine("1. Turn cheats OFF.");
        builder.AppendLine("2. Set memory-card slots 1 and 2 to NONE.");
        builder.AppendLine("3. Cold boot the supplied CUE; do not use a save state.");
        builder.AppendLine("4. Load ID65 and check visibility, walking, charging, jumping, landing, reverse traversal, reset, and re-entry on every edited surface.");
        builder.AppendLine("5. Load retail Town Square, Gnasty's Loot, and Sunny Flight with the table above and confirm they remain stable and unedited.");
        builder.AppendLine();
        builder.AppendLine("Record runtime observations separately; this disposable build is not release evidence by itself.");
        return builder.ToString();
    }

    private static void VerifyChecklistReadback(
        string checklist,
        RuntimeCandidateFinderReveal? reveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        string cuePath)
    {
        if (reveal != null)
        {
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, reveal, loadCodes);
            return;
        }

        if (!checklist.Contains(Path.GetFileName(cuePath), StringComparison.Ordinal) ||
            !checklist.Contains("load the **CUE**, not the BIN", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The platform-aware runtime checklist omitted its exact CUE handoff.");
        }
        foreach (RuntimeCandidateLoadCode loadCode in loadCodes)
        {
            if (!checklist.Contains(loadCode.TestName, StringComparison.Ordinal) ||
                !checklist.Contains(loadCode.LevelId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                !checklist.Contains(loadCode.InputCode, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The platform-aware runtime checklist omitted the load code for {loadCode.TestName}.");
            }
        }
    }

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> codes)
    {
        int[] expectedIds = [65, 13, 64, 15];
        if (codes.Count != expectedIds.Length ||
            !codes.Select(code => code.LevelId).SequenceEqual(expectedIds) ||
            codes.Any(code => string.IsNullOrWhiteSpace(code.InputCode)))
        {
            throw new InvalidDataException("The disposable terrain handoff lost an ID65 comparison load code.");
        }
    }

    private static void VerifyStagedArtifactNames(
        TerrainPatchResult terrain,
        string checklistPath,
        string? helperPath,
        string terrainEditsSnapshotPath,
        string receiptPath,
        string testKey)
    {
        string prefix = Path.Combine(Path.GetDirectoryName(terrain.OutputImagePath)!, testKey);
        List<string> expected =
        [
            prefix + ".bin",
            prefix + ".cue",
            prefix + ".terrain-patch-plan.json",
            prefix + "-terrain-edits-snapshot.json",
            prefix + "-runtime-checklist.md",
            prefix + "-static-readback-receipt.json"
        ];
        List<string> actual =
        [
            terrain.OutputImagePath,
            terrain.OutputCuePath,
            terrain.OutputPlanPath,
            terrainEditsSnapshotPath,
            checklistPath,
            receiptPath
        ];
        if (helperPath != null)
        {
            expected.Add(prefix + "-Reveal-in-Finder.command");
            actual.Add(helperPath);
        }
        if (!actual.Zip(expected).All(pair => PathsEqual(pair.First, pair.Second)) ||
            actual.Any(path => !File.Exists(path)))
        {
            throw new InvalidDataException("The staged terrain-test artifact set is incomplete or misnamed.");
        }
    }

    private static void BeginCandidatePublication(
        CandidatePublication publication,
        string stagedDirectory,
        bool replaceExisting)
    {
        if (Directory.Exists(publication.DestinationDirectory))
        {
            if (!replaceExisting)
                throw new IOException("The disposable terrain-test key already exists.");
            Directory.Move(publication.DestinationDirectory, publication.BackupDirectory);
            publication.HadPreviousCandidate = true;
            WriteCandidateOperationJournal(publication, OperationPhasePreviousBackedUp);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(publication.DestinationDirectory)!);
        Directory.Move(stagedDirectory, publication.DestinationDirectory);
        publication.NewCandidatePublished = true;
        WriteCandidateOperationJournal(publication, OperationPhaseCandidatePublished);
    }

    private static void RollBackCandidatePublication(
        CandidatePublication publication,
        Exception publicationFailure,
        Action<string>? testStageHook)
    {
        List<Exception> rollbackFailures = [];
        if (publication.NewCandidatePublished && Directory.Exists(publication.DestinationDirectory))
        {
            try
            {
                Directory.Delete(publication.DestinationDirectory, recursive: true);
            }
            catch (Exception rollbackFailure)
            {
                rollbackFailures.Add(rollbackFailure);
            }
        }
        if (rollbackFailures.Count == 0)
            WriteCandidateOperationJournal(publication, OperationPhaseNewCandidateRemoved);

        if (rollbackFailures.Count == 0 && publication.HadPreviousCandidate)
        {
            try
            {
                if (Directory.Exists(publication.DestinationDirectory))
                    throw new IOException("The failed new candidate still occupies the rollback destination.");
                if (!Directory.Exists(publication.BackupDirectory))
                    throw new IOException("The prior candidate backup is missing during rollback.");
                testStageHook?.Invoke("before-previous-candidate-restore");
                Directory.Move(publication.BackupDirectory, publication.DestinationDirectory);
            }
            catch (Exception rollbackFailure)
            {
                rollbackFailures.Add(rollbackFailure);
            }
        }
        if (rollbackFailures.Count > 0)
        {
            throw new IOException(
                "Disposable terrain-test publication failed and the previous candidate could not be restored exactly.",
                new AggregateException([publicationFailure, .. rollbackFailures]));
        }
        WriteCandidateOperationJournal(publication, OperationPhaseRollbackComplete);
        publication.RecoveryComplete = true;
    }

    private static void PrepareDestination(
        UnusedLevel65BlankLevelLabTerrainTestPaths paths,
        bool replaceExisting)
    {
        if (Directory.Exists(paths.TestDirectoryPath) && !replaceExisting)
        {
            throw new IOException(
                $"Disposable terrain test '{Path.GetFileName(paths.TestDirectoryPath)}' already exists; choose a new key or explicitly replace it.");
        }
        if (File.Exists(paths.TestDirectoryPath))
            throw new InvalidDataException("The requested terrain-test directory is occupied by a file.");
    }

    private static string RequireLabAuthoredEditsPath(string path, string authoredRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A lab-authored terrain edit file is required.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The lab-authored terrain edit file does not exist.", fullPath);
        EnsureDescendant(fullPath, authoredRoot, "terrain edit");
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Lab-authored terrain edits must be a JSON edit manifest.");
        RejectReparsePoints(fullPath, authoredRoot);
        return fullPath;
    }

    private static void RequireAllPathsInsideLab(UnusedLevel65BlankLevelLabTerrainTestPaths paths)
    {
        foreach (string path in new[]
        {
            paths.CacheDirectoryPath,
            paths.WadAnalysisPath,
            paths.SourceOverlayPath,
            paths.SourceSearchPath,
            paths.TestsDirectoryPath,
            paths.TestDirectoryPath,
            paths.OutputPrefix,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.OutputPlanPath,
            paths.TerrainEditsSnapshotPath,
            paths.RuntimeChecklistPath,
            paths.FinderHelperPath,
            paths.StaticReceiptPath,
            paths.OperationsDirectoryPath
        })
        {
            EnsureDescendant(path, paths.Lab.RootPath, "terrain-test artifact");
        }
    }

    private static void EnsureDescendant(string path, string root, string label)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        string relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == "." ||
            Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The ID65 lab {label} escapes its versioned root.");
        }
    }

    private static void RejectReparsePoints(string filePath, string rootPath)
    {
        string fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
        string? current = filePath;
        while (!string.IsNullOrWhiteSpace(current) && !PathsEqual(current, fullRoot))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Lab-authored terrain edits cannot traverse symbolic links.");
            current = Path.GetDirectoryName(current);
        }
    }

    private static string RequireTestKey(string value)
    {
        string key = (value ?? "").Trim();
        if (!TestKeyPattern.IsMatch(key))
        {
            throw new ArgumentException(
                "A terrain-test key must use 1-64 lowercase letters, digits, or interior hyphens.",
                nameof(value));
        }
        return key;
    }

    private static string RequireDisplayName(string value)
    {
        string name = (value ?? "").Trim();
        if (name.Length is < 1 or > 120 || name.Any(character => char.IsControl(character)))
            throw new ArgumentException("A short printable terrain-test display name is required.", nameof(value));
        return name;
    }

    private static IReadOnlyList<int> ReadIntArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();
        return element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    }

    private static IReadOnlyList<float> ReadFloatArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
            return Array.Empty<float>();
        return element.EnumerateArray().Select(value => value.GetSingle()).ToArray();
    }

    private static IReadOnlyList<(float X, float Y)> ReadPoints(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
            return Array.Empty<(float X, float Y)>();
        return element.EnumerateArray()
            .Select(point => (
                X: point.GetProperty("x").GetSingle(),
                Y: point.GetProperty("y").GetSingle()))
            .ToArray();
    }

    private static string GetString(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static int GetInt32(JsonElement parent, string property, int fallback) =>
        parent.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : fallback;

    private static byte[] ParseHexBytes(string value)
    {
        string normalized = new(value.Where(char.IsAsciiHexDigit).ToArray());
        if (normalized.Length % 2 != 0)
            throw new InvalidDataException("A terrain patch contains malformed hex bytes.");
        return Convert.FromHexString(normalized);
    }

    private static long ParseHexOffset(string value)
    {
        string text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
        return long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static UnixFileMode ExactFinderMode() =>
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute |
        UnixFileMode.GroupRead |
        UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead |
        UnixFileMode.OtherExecute;

    private static string ShellSingleQuote(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static FileStream CreateOperationLease(string operationRoot)
    {
        return new FileStream(
            Path.Combine(operationRoot, ".active-operation.lock"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    private static void WriteCandidateOperationJournal(
        CandidatePublication publication,
        string phase)
    {
        if (!KnownCandidateOperationPhases.Contains(phase, StringComparer.Ordinal))
            throw new InvalidOperationException($"Unknown terrain-test operation phase '{phase}'.");
        publication.Phase = phase;
        TerrainTestOperationJournal journal = new(
            OperationJournalSchemaVersion,
            OperationKind,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion,
            publication.TestKey,
            publication.DestinationDirectory,
            publication.BackupDirectory,
            publication.PreviousCandidateExistedAtStart,
            publication.HadPreviousCandidate,
            phase);
        string journalPath = Path.Combine(publication.OperationRoot, OperationJournalFileName);
        string temporaryPath = journalPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(journal, ReceiptJsonOptions) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, journalPath, overwrite: true);
    }

    private static void RecoverOwnedTerrainTestOperationDebris(string operationsRoot)
    {
        if (!Directory.Exists(operationsRoot))
            return;
        if ((File.GetAttributes(operationsRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The terrain-test operations root cannot be a symbolic link.");

        string[] entries = Directory.GetFileSystemEntries(operationsRoot);
        foreach (string entry in entries)
        {
            string name = Path.GetFileName(entry);
            if (!Directory.Exists(entry) ||
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 ||
                !IsExactOwnedOperationName(name, "terrain-test-"))
            {
                throw new InvalidDataException(
                    $"Refusing to clean non-owned terrain-test operation entry '{name}'.");
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
                        $"Terrain-test operation '{name}' is still active; refusing stale cleanup.",
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
                TerrainTestOperationJournal journal = ReadAndValidateCandidateOperationJournal(
                    entry,
                    operationsRoot,
                    journalPath);
                RecoverCandidateOperation(entry, journal);
            }
            else
            {
                if (Directory.Exists(Path.Combine(entry, "previous-candidate")))
                {
                    throw new InvalidDataException(
                        $"Terrain-test operation '{name}' contains an unjournaled prior-candidate backup; refusing automatic deletion.");
                }
                Directory.Delete(entry, recursive: true);
            }
        }

        TryDeleteEmptyDirectory(operationsRoot);
        if (Directory.Exists(operationsRoot))
            throw new IOException("Owned stale terrain-test operation debris could not be removed.");
    }

    private static TerrainTestOperationJournal ReadAndValidateCandidateOperationJournal(
        string operationRoot,
        string operationsRoot,
        string journalPath)
    {
        TerrainTestOperationJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<TerrainTestOperationJournal>(
                File.ReadAllText(journalPath),
                ReceiptJsonOptions) ?? throw new InvalidDataException("The terrain-test operation journal is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The terrain-test operation journal is malformed; preserving recovery data.", ex);
        }

        string labRoot = Path.GetDirectoryName(Path.GetFullPath(operationsRoot)) ?? "";
        string expectedDestination = Path.Combine(
            labRoot,
            "runtime-tests",
            RequireTestKey(journal.TestKey));
        string expectedBackup = Path.Combine(operationRoot, "previous-candidate");
        if (journal.SchemaVersion != OperationJournalSchemaVersion ||
            journal.OperationKind != OperationKind ||
            journal.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            journal.ProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            !PathsEqual(journal.DestinationDirectory, expectedDestination) ||
            !PathsEqual(journal.BackupDirectory, expectedBackup) ||
            !KnownCandidateOperationPhases.Contains(journal.Phase, StringComparer.Ordinal) ||
            (journal.HadPreviousCandidate && !journal.PreviousCandidateExistedAtStart))
        {
            throw new InvalidDataException(
                "The terrain-test operation journal has stale identity, paths, state, or profile data; preserving recovery data.");
        }
        EnsureDescendant(journal.DestinationDirectory, Path.Combine(labRoot, "runtime-tests"), "recovery destination");
        EnsureDescendant(journal.BackupDirectory, operationRoot, "recovery backup");
        RejectRecoveryReparsePoint(journal.DestinationDirectory, "terrain-test recovery destination");
        RejectRecoveryReparsePoint(journal.BackupDirectory, "terrain-test recovery backup");
        return journal;
    }

    private static void RecoverCandidateOperation(
        string operationRoot,
        TerrainTestOperationJournal journal)
    {
        bool destinationExists = Directory.Exists(journal.DestinationDirectory);
        bool backupExists = Directory.Exists(journal.BackupDirectory);
        if (journal.Phase == OperationPhaseCommitted)
        {
            if (!destinationExists)
            {
                throw new InvalidDataException(
                    "A committed terrain-test recovery journal has no published candidate; preserving its backup.");
            }
            Directory.Delete(operationRoot, recursive: true);
            return;
        }

        if (journal.Phase == OperationPhaseStaging)
        {
            if (journal.HadPreviousCandidate)
                throw new InvalidDataException("A staging terrain-test journal has impossible committed backup state.");
            if (backupExists)
            {
                if (!journal.PreviousCandidateExistedAtStart || destinationExists)
                {
                    throw new InvalidDataException(
                        "A staging terrain-test journal has ambiguous destination/backup state; preserving recovery data.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(journal.DestinationDirectory)!);
                Directory.Move(journal.BackupDirectory, journal.DestinationDirectory);
            }
            else if (!journal.PreviousCandidateExistedAtStart && destinationExists)
            {
                Directory.Delete(journal.DestinationDirectory, recursive: true);
            }
            Directory.Delete(operationRoot, recursive: true);
            return;
        }

        if (journal.HadPreviousCandidate)
        {
            if (backupExists)
            {
                if (destinationExists)
                    Directory.Delete(journal.DestinationDirectory, recursive: true);
                Directory.CreateDirectory(Path.GetDirectoryName(journal.DestinationDirectory)!);
                Directory.Move(journal.BackupDirectory, journal.DestinationDirectory);
            }
            else if (!((journal.Phase == OperationPhaseNewCandidateRemoved ||
                        journal.Phase == OperationPhaseRollbackComplete) &&
                       destinationExists))
            {
                throw new InvalidDataException(
                    "An incomplete terrain-test rollback lost its prior-candidate backup; preserving recovery evidence.");
            }
        }
        else
        {
            if (backupExists)
                throw new InvalidDataException("A terrain-test recovery journal has an unexpected prior backup.");
            if (destinationExists)
                Directory.Delete(journal.DestinationDirectory, recursive: true);
        }

        Directory.Delete(operationRoot, recursive: true);
    }

    private static void RejectRecoveryReparsePoint(string path, string label)
    {
        if ((Directory.Exists(path) || File.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The {label} cannot be a symbolic link.");
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteOwnedDirectory(string path, string ownerRoot)
    {
        try
        {
            EnsureDescendant(path, ownerRoot, "owned cleanup directory");
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record TerrainTestOperationJournal(
        int SchemaVersion,
        string OperationKind,
        string ProfileId,
        int ProfileVersion,
        string TestKey,
        string DestinationDirectory,
        string BackupDirectory,
        bool PreviousCandidateExistedAtStart,
        bool HadPreviousCandidate,
        string Phase);

    private sealed class CandidatePublication(
        string operationRoot,
        string testKey,
        string destinationDirectory,
        string backupDirectory)
    {
        public string OperationRoot { get; } = operationRoot;
        public string TestKey { get; } = testKey;
        public string DestinationDirectory { get; } = destinationDirectory;
        public string BackupDirectory { get; } = backupDirectory;
        public bool PreviousCandidateExistedAtStart { get; } = Directory.Exists(destinationDirectory);
        public bool HadPreviousCandidate { get; set; }
        public bool NewCandidatePublished { get; set; }
        public bool Committed { get; set; }
        public bool RecoveryComplete { get; set; }
        public string Phase { get; set; } = OperationPhaseStaging;
    }
}
