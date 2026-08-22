using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    bool RequestFinderReveal = true,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths(
    string OutputDirectoryPath,
    string OutputPrefix,
    string OutputImagePath,
    string OutputCuePath,
    string FoundationCompositionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string OperationsDirectoryPath);

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string ComposerProfileId,
    string BaseImageSha256,
    string ModelPreimageSha256,
    string AuthoredModelSha256,
    string AuthoredCollisionSha256,
    string AuthoredCollisionTreeSha256,
    string AuthoredCollisionBlocksSha256,
    string ExactInverseModelSha256,
    string ComposerPlanSha256,
    int WadLba,
    long ModelSubfileWadOffset,
    int ModelSubfileByteLength,
    int TargetSectorIndex,
    int AddedLowDetailVertexCount,
    int AddedLowDetailFaceCount,
    int AddedHighDetailVertexCount,
    int AddedHighDetailFaceCount,
    int CollisionTriangleIndex,
    int NativeCollisionCellCount,
    int AuthoredCollisionCellCount,
    IReadOnlyList<UnusedLevel65FoundationCollisionCell> ChangedCollisionCells,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    int AtomicRejectMatrixCount,
    bool ExactInverseVerified,
    bool FullExposureProofVerified,
    bool SpawnAndPlayerAnchorVerified,
    bool CycloramaVerified,
    bool DisplayNameVerified,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string FoundationCompositionPlanPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string? FinderHelperPath,
    string OutputCueSha256,
    string TerrainPlanSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string? FinderHelperSha256,
    string BaseImageSha256,
    string OutputImageSha256,
    string OutputDataSha256,
    string AuthoredModelSha256,
    string AuthoredCollisionSha256,
    string AuthoredCollisionTreeSha256,
    string AuthoredCollisionBlocksSha256,
    string ExactInverseModelSha256,
    string ComposerPlanSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FoundationCompositionVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool CollisionSemanticDeltaVerified,
    bool ExactInverseVerified,
    bool AtomicRejectMatrixVerified,
    bool OcclusionOwnershipVerified,
    bool ProtectedSubfilesPreserved,
    bool SpawnAndPlayerAnchorPreserved,
    bool CycloramaPreserved,
    bool DisplayNamePreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths Paths,
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan Plan,
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal? FinderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    string OutputImageSha256,
    string OutputDataSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FoundationCompositionVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool CollisionSemanticDeltaVerified,
    bool ExactInverseVerified,
    bool AtomicRejectMatrixVerified,
    bool OcclusionOwnershipVerified,
    bool ProtectedSubfilesPreserved,
    bool SpawnAndPlayerAnchorPreserved,
    bool CycloramaPreserved,
    bool DisplayNamePreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal static class UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter
{
    public static bool Retired => true;
    public static string RetirementReason =>
        "RETIRED: this candidate is stacked on the positive-wound ID65 foundation collision convention, which is not runtime-proven and matches the failed remote-blank v1 sign. Preserve frozen artifacts only as historical evidence; do not republish or load them. Use the isolated collision-winding-repair v2 discriminator until DuckStation establishes the playable convention.";

    public const string ProfileId =
        "unused-level-65-foundation-terrain-add-remove-native-membership-clean-usa-disposable-v1";
    public const string OutputPrefix =
        "Unused-Level-65-Foundation-Terrain-ADD-REMOVE-Second-HP-LP-Tile-RUNTIME-CANDIDATE";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-foundation-terrain-add-remove-runtime-candidate";
    private const string OperationDirectoryPrefix = "terrain-add-remove-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-foundation-terrain-add-remove-writer.lease";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const string ActiveWriterLeaseMessage =
        "Another terrain add/remove runtime candidate writer is active for this publication parent. " +
        "Concurrent CreateAsync calls are refused until that writer finishes.";
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhasePreviousBackedUp = "previous-candidate-backed-up";
    private const string OperationPhaseCandidatePublished = "candidate-published";
    private const string OperationPhaseNewCandidateRemoved = "new-candidate-removed";
    private const string OperationPhaseRollbackComplete = "rollback-complete";
    private const string OperationPhaseCommitted = "committed";
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const long DataWadOffset = 0x6936800;
    private const int DataByteLength = 0x2E2000;
    private const long ModelWadOffset = 0x6A15000;
    private const int ModelByteLength = 0x94800;
    private const string BaseImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    private const string ModelPreimageSha256 =
        "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
    private const string AuthoredModelSha256 =
        "8422c32ca6b8555bc6dd5f8d0f266e4be90c8e4648c6facbeaef165f0a75a02b";
    private const string AuthoredCollisionSha256 =
        "59aa6b65712fbcdb96358c7ea2854e1eda18cc0115f34d793f131bd8dec0dd1f";
    private const string AuthoredCollisionTreeSha256 =
        "0ebefed842c3ebb7e3a5cafdfffa7968befc9caebf0999e8787322c4e34b2b61";
    private const string AuthoredCollisionBlocksSha256 =
        "372ad0bb9d0ef51b2c7b9a01acf9a8aada3049b1caefdba597f67d54cb084d26";
    private const string ExactInverseModelSha256 = ModelPreimageSha256;
    private const string ComposerPlanSha256 =
        "7db62a3d9a388c7e2388a6f77969d0511dab6e3ff6f01ccc9ab863e5fff81ced";
    private const string ExpectedOutputImageSha256 =
        "c33f83a0a5d64f58094c3a10487396903ebb2534b2438433552bbed6a8bbce15";
    private const string ExpectedOutputDataSha256 =
        "29697a69061dd7ca32fd0627ffb343368420803f369c741b03f313ea0c44597b";
    private const long ExpectedChangedLogicalWadBytes = 348_741;
    private const long ExpectedChangedPhysicalImageBytes = 407_354;
    private const int ExpectedChangedRawSectorCount = 213;
    public const string ExpectedRawSectorDiffSha256 =
        "b28a166aa81b2cf6648348b829b659078390a2c5ec92c40782b0b32af85feb14";
    public const string ExpectedOutputCueSha256 =
        "b4a2f391873bf7decdaaf6075f7587096c6990f2422503c2fa0e40ccd27b4f96";
    public const string ExpectedTerrainPlanSha256 =
        "0eba87b13f3c5ec1edf85bc5d7beec717540052d9b82b258f79807a2c240d13b";
    public const string ExpectedRuntimeChecklistSha256 =
        "320c52f38fbcc7af6b2068316d09d063c8ef4d96ed3893dfafe57c822c8fbdaa";
    public const string ExpectedLocationGuideSha256 =
        "86af1a715d13171e12e342c3d2ed69c0a1bdb5088a12e3b4224ab3136e1b6a34";
    public const string ExpectedFinderHelperSha256 =
        "91878694fda4e399cfb7f5e00f7fcc223f2abe7ad6e966789118de950760584f";
    public const string ExpectedStaticReadbackReceiptSha256 =
        "46fbc04b5d9a385abf9913f19f5bfe219c916492ac357e3f5efe861cc44465f1";
    private const int AuthoredCollisionRelativeOffset = 0x2BE10;
    private const int CollisionTreeRelativeOffset = AuthoredCollisionRelativeOffset + 4 + 0x1C;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksRelativeOffset = AuthoredCollisionRelativeOffset + 4 + 0x6A7C;
    private const int CollisionBlocksByteLength = 0x17984;
    private static readonly int[] ExpectedChangedRawSectorLbas =
    [
        .. Enumerable.Range(54_356, 2),
        .. Enumerable.Range(54_434, 189),
        .. Enumerable.Range(54_624, 2),
        .. Enumerable.Range(54_628, 20)
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult> CreateAsync(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Retired)
            throw new InvalidOperationException(RetirementReason);

        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "exact ID65 foundation BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "exact ID65 foundation CUE");
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths =
            CreatePaths(request.OutputDirectoryPath);
        RequireSafeRoles(baseImage, baseCue, paths);
        cancellationToken.ThrowIfCancellationRequested();

        using GlobalWriterLease globalWriterLease = AcquireGlobalWriterLease(paths);
        request.TestStageHook?.Invoke("after-global-writer-lease-acquired");
        bool rollbackRecoveryVerified = RecoverOwnedOperations(
            paths.OperationsDirectoryPath,
            paths.OutputDirectoryPath);
        PrepareDestination(paths.OutputDirectoryPath, request.ReplaceExistingCandidate);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        RequireHash(
            await HashFileAsync(baseImage, cancellationToken),
            BaseImageSha256,
            "exact ID65 foundation BIN");

        UnusedLevel65FoundationTerrainManifest foundationManifest =
            UnusedLevel65FoundationTerrainComposer.CreateManifest(includeOptionalSecondTile: false);
        UnusedLevel65FoundationTerrainManifest twoTileManifest =
            UnusedLevel65FoundationTerrainComposer.CreateManifest(includeOptionalSecondTile: true);
        UnusedLevel65FoundationTerrainStaticPlan foundation =
            UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(baseImage, foundationManifest);
        UnusedLevel65FoundationTerrainStaticPlan added =
            UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(baseImage, twoTileManifest);
        UnusedLevel65FoundationTerrainStaticPlan repeated =
            UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(baseImage, twoTileManifest);
        UnusedLevel65FoundationTerrainManifest removedManifest =
            UnusedLevel65FoundationTerrainComposer.RemoveOptionalSecondTile(twoTileManifest);
        UnusedLevel65FoundationTerrainStaticPlan inverse =
            UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(baseImage, removedManifest);
        ValidateComposerGate(foundation, added, repeated, inverse);
        int rejectionCount = VerifyAtomicRejectMatrix(baseImage, foundationManifest, twoTileManifest);
        if (rejectionCount != 17)
            throw new InvalidDataException($"The exact terrain composer rejection matrix changed to {rejectionCount} rows.");
        RequireHash(
            await HashFileAsync(baseImage, cancellationToken),
            BaseImageSha256,
            "foundation BIN after static composer validation");

        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "terrain add/remove operation");
        Directory.CreateDirectory(operationRoot);
        await using FileStream operationLease = CreateOperationLease(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        string backupDirectory = Path.Combine(operationRoot, "previous-candidate");
        Directory.CreateDirectory(stagedDirectory);
        CandidatePublication publication = new(
            operationRoot,
            paths.OutputDirectoryPath,
            stagedDirectory,
            backupDirectory,
            Directory.Exists(paths.OutputDirectoryPath));
        WriteOperationJournal(publication, OperationPhaseStaging);

        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            request.TestStageHook?.Invoke("before-staged-candidate-build");
            cancellationToken.ThrowIfCancellationRequested();
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                baseImage,
                baseCue,
                paths,
                stagedDirectory,
                added,
                inverse,
                rejectionCount,
                loadCodes,
                request.RequestFinderReveal,
                cancellationToken);
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);

            RequireHash(
                await HashFileAsync(baseImage, cancellationToken),
                BaseImageSha256,
                "foundation BIN after publication");
            WriteOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            RuntimeCandidateFinderReveal? finalReveal = staged.FinderReveal == null
                ? null
                : BuildFinalFinderReveal(paths);
            result = new(
                paths,
                staged.Plan,
                staged.Receipt,
                finalReveal,
                loadCodes,
                staged.Readback.OutputImageSha256,
                staged.Readback.OutputDataSha256,
                staged.Readback.ChangedLogicalWadBytes,
                staged.Readback.ChangedPhysicalImageBytes,
                staged.RebuiltRawSectorCount,
                staged.Readback.ChangedRawSectorCount,
                staged.Readback.RawSectorDiffs,
                HashRawSectorDiffs(staged.Readback.RawSectorDiffs),
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                FoundationCompositionVerified: true,
                ModelReadbackVerified: true,
                CollisionReadbackVerified: true,
                CollisionSemanticDeltaVerified: true,
                ExactInverseVerified: true,
                AtomicRejectMatrixVerified: true,
                OcclusionOwnershipVerified: true,
                ProtectedSubfilesPreserved: true,
                SpawnAndPlayerAnchorPreserved: true,
                CycloramaPreserved: true,
                DisplayNamePreserved: true,
                RawSectorIntegrityVerified: true,
                BaseCandidatePreserved: true,
                FullDirectoryPublicationVerified: true,
                DisposableRuntimeCandidateAuthorized: true,
                RollbackRecoveryVerified: rollbackRecoveryVerified,
                FullDirectoryRollbackVerified: rollbackRecoveryVerified,
                FinderHandoffVerified: finalReveal != null,
                PromotionAuthorized: false,
                NormalCreateBinEnabled: false);
        }
        catch (Exception publicationFailure)
        {
            if (!publication.Committed)
            {
                recoveryComplete = false;
                RollBackPublication(publication, publicationFailure, request.TestStageHook);
                recoveryComplete = publication.RecoveryComplete;
            }
            throw;
        }
        finally
        {
            await operationLease.DisposeAsync();
            if (recoveryComplete)
            {
                TryDeleteOwnedOperation(operationRoot, paths.OperationsDirectoryPath);
                TryDeleteEmptyDirectory(paths.OperationsDirectoryPath);
            }
        }

        if (Directory.Exists(operationRoot))
            throw new IOException("The completed terrain add/remove operation directory could not be removed.");
        return result ?? throw new InvalidOperationException(
            "The terrain add/remove runtime writer completed without a result.");
    }

    public static UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string root = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, root) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException(
                "The terrain add/remove candidate output cannot be a filesystem or home root.");
        }
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The terrain add/remove candidate output has no parent directory.");
        if (!string.Equals(
                Path.GetFileName(outputDirectory),
                "unused-level-65-foundation-terrain-add-remove-second-tile",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The terrain add/remove writer owns only the exact isolated output directory " +
                "'unused-level-65-foundation-terrain-add-remove-second-tile'.");
        }
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths result = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-terrain-add-remove-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-foundation-terrain-add-remove-operations"));
        RequirePathsInsideOutput(result);
        return result;
    }

    internal static async Task<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult>
        VerifyPublishedAsync(
            string outputDirectoryPath,
            CancellationToken cancellationToken = default)
    {
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths =
            CreatePaths(outputDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new DirectoryNotFoundException("The published terrain add/remove directory is missing.");
        RejectTreeReparsePoints(paths.OutputDirectoryPath, "published terrain add/remove directory");
        VerifyStagedCandidateArtifacts(paths.OutputDirectoryPath, paths, includeFinderReveal: true);

        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt receipt =
            await ReadCanonicalJsonAsync<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt>(
                paths.StaticReadbackReceiptPath,
                "published terrain receipt",
                cancellationToken);
        RequireHash(
            await HashFileAsync(paths.StaticReadbackReceiptPath, cancellationToken),
            ExpectedStaticReadbackReceiptSha256,
            "published terrain receipt");
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan plan =
            JsonSerializer.Deserialize<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan>(
                await File.ReadAllTextAsync(paths.FoundationCompositionPlanPath, cancellationToken),
                JsonOptions)
            ?? throw new InvalidDataException("The published terrain plan is unreadable.");

        bool receiptPathsExact =
            PathEquals(receipt.OutputDirectoryPath, paths.OutputDirectoryPath) &&
            PathEquals(receipt.OutputImagePath, paths.OutputImagePath) &&
            PathEquals(receipt.OutputCuePath, paths.OutputCuePath) &&
            PathEquals(receipt.FoundationCompositionPlanPath, paths.FoundationCompositionPlanPath) &&
            PathEquals(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath) &&
            PathEquals(receipt.LocationGuidePath, paths.LocationGuidePath) &&
            PathEquals(receipt.FinderHelperPath, paths.FinderHelperPath);
        bool receiptContractExact =
            receipt.SchemaVersion == ReceiptSchemaVersion &&
            receipt.ProfileId == ProfileId &&
            receiptPathsExact &&
            receipt.BaseImageSha256 == BaseImageSha256 &&
            receipt.OutputImageSha256 == ExpectedOutputImageSha256 &&
            receipt.OutputDataSha256 == ExpectedOutputDataSha256 &&
            receipt.AuthoredModelSha256 == AuthoredModelSha256 &&
            receipt.AuthoredCollisionSha256 == AuthoredCollisionSha256 &&
            receipt.AuthoredCollisionTreeSha256 == AuthoredCollisionTreeSha256 &&
            receipt.AuthoredCollisionBlocksSha256 == AuthoredCollisionBlocksSha256 &&
            receipt.ExactInverseModelSha256 == ExactInverseModelSha256 &&
            receipt.ComposerPlanSha256 == ComposerPlanSha256 &&
            receipt.OutputCueSha256 == ExpectedOutputCueSha256 &&
            receipt.TerrainPlanSha256 == ExpectedTerrainPlanSha256 &&
            receipt.RuntimeChecklistSha256 == ExpectedRuntimeChecklistSha256 &&
            receipt.LocationGuideSha256 == ExpectedLocationGuideSha256 &&
            receipt.FinderHelperSha256 == ExpectedFinderHelperSha256 &&
            receipt.ChangedLogicalWadBytes == ExpectedChangedLogicalWadBytes &&
            receipt.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes &&
            receipt.RebuiltRawSectorCount == ExpectedChangedRawSectorCount &&
            receipt.ChangedRawSectorCount == ExpectedChangedRawSectorCount &&
            receipt.RawSectorDiffs.Count == ExpectedChangedRawSectorCount &&
            receipt.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                .SequenceEqual(ExpectedChangedRawSectorLbas) &&
            receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256 &&
            HashRawSectorDiffs(receipt.RawSectorDiffs) == ExpectedRawSectorDiffSha256 &&
            receipt.RawSectorDiffs.Sum(diff => (long)diff.TotalChangedBytes) ==
                ExpectedChangedPhysicalImageBytes &&
            receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified &&
            receipt.FoundationCompositionVerified &&
            receipt.ModelReadbackVerified &&
            receipt.CollisionReadbackVerified &&
            receipt.CollisionSemanticDeltaVerified &&
            receipt.ExactInverseVerified &&
            receipt.AtomicRejectMatrixVerified &&
            receipt.OcclusionOwnershipVerified &&
            receipt.ProtectedSubfilesPreserved &&
            receipt.SpawnAndPlayerAnchorPreserved &&
            receipt.CycloramaPreserved &&
            receipt.DisplayNamePreserved &&
            receipt.RawSectorIntegrityVerified &&
            receipt.BaseCandidatePreserved &&
            receipt.FullDirectoryPublicationVerified &&
            receipt.DisposableRuntimeCandidateAuthorized &&
            !receipt.RollbackRecoveryVerified &&
            !receipt.FullDirectoryRollbackVerified &&
            receipt.FinderHandoffVerified &&
            !receipt.PromotionAuthorized &&
            !receipt.NormalCreateBinEnabled;
        if (!receiptContractExact)
            throw new InvalidDataException("The published terrain receipt lost its frozen path/hash/flag contract.");

        bool planContractExact =
            plan.SchemaVersion == PlanSchemaVersion &&
            plan.ProfileId == ProfileId &&
            plan.ComposerProfileId == UnusedLevel65FoundationTerrainComposer.ProfileId &&
            plan.BaseImageSha256 == BaseImageSha256 &&
            plan.ModelPreimageSha256 == ModelPreimageSha256 &&
            plan.AuthoredModelSha256 == AuthoredModelSha256 &&
            plan.AuthoredCollisionSha256 == AuthoredCollisionSha256 &&
            plan.AuthoredCollisionTreeSha256 == AuthoredCollisionTreeSha256 &&
            plan.AuthoredCollisionBlocksSha256 == AuthoredCollisionBlocksSha256 &&
            plan.ExactInverseModelSha256 == ExactInverseModelSha256 &&
            plan.ComposerPlanSha256 == ComposerPlanSha256 &&
            plan.WadLba == WadLba &&
            plan.ModelSubfileWadOffset == ModelWadOffset &&
            plan.ModelSubfileByteLength == ModelByteLength &&
            plan.TargetSectorIndex == 213 &&
            plan.AddedLowDetailVertexCount == 1 &&
            plan.AddedLowDetailFaceCount == 1 &&
            plan.AddedHighDetailVertexCount == 1 &&
            plan.AddedHighDetailFaceCount == 1 &&
            plan.CollisionTriangleIndex == 19_808 &&
            plan.NativeCollisionCellCount == 4_252 &&
            plan.AuthoredCollisionCellCount == 4_252 &&
            plan.ChangedCollisionCells.SequenceEqual(new[]
            {
                new UnusedLevel65FoundationCollisionCell(30, 24, 2),
                new UnusedLevel65FoundationCollisionCell(30, 25, 2),
                new UnusedLevel65FoundationCollisionCell(31, 25, 2)
            }) &&
            plan.AffectedRawSectorLbas.SequenceEqual(ExpectedChangedRawSectorLbas) &&
            plan.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256 &&
            plan.AtomicRejectMatrixCount == 17 &&
            plan.ExactInverseVerified &&
            plan.FullExposureProofVerified &&
            plan.SpawnAndPlayerAnchorVerified &&
            plan.CycloramaVerified &&
            plan.DisplayNameVerified &&
            plan.RequiresDuckStationRuntimeProof &&
            plan.DisposableRuntimeCandidateAuthorized &&
            !plan.PromotionAuthorized &&
            !plan.NormalCreateBinEnabled;
        if (!planContractExact)
            throw new InvalidDataException("The published terrain plan lost its frozen composer/runtime contract.");
        VerifyLoadCodes(plan.LoadCodes);

        string baseImage = RequireExistingFile(receipt.BaseImagePath, "receipt foundation BIN");
        string baseCue = RequireExistingFile(receipt.BaseCuePath, "receipt foundation CUE");
        RequireSafeRoles(baseImage, baseCue, paths);
        RequireHash(
            await HashFileAsync(baseImage, cancellationToken),
            BaseImageSha256,
            "receipt foundation BIN");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");

        string outputImageSha256 = await HashFileAsync(paths.OutputImagePath, cancellationToken);
        RequireHash(outputImageSha256, ExpectedOutputImageSha256, "published terrain BIN");
        RequireHash(
            await HashFileAsync(paths.OutputCuePath, cancellationToken),
            ExpectedOutputCueSha256,
            "published terrain CUE");
        RequireHash(
            await HashFileAsync(paths.FoundationCompositionPlanPath, cancellationToken),
            ExpectedTerrainPlanSha256,
            "published terrain plan");
        RequireHash(
            await HashFileAsync(paths.RuntimeChecklistPath, cancellationToken),
            ExpectedRuntimeChecklistSha256,
            "published terrain checklist");
        RequireHash(
            await HashFileAsync(paths.LocationGuidePath, cancellationToken),
            ExpectedLocationGuideSha256,
            "published terrain guide");
        RequireHash(
            await HashFileAsync(paths.FinderHelperPath, cancellationToken),
            ExpectedFinderHelperSha256,
            "published terrain Finder helper");
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");

        RuntimeCandidateFinderReveal finderReveal = BuildFinalFinderReveal(paths);
        if (!OperatingSystem.IsMacOS() ||
            File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
        {
            throw new InvalidDataException("The frozen terrain Finder helper is not exact macOS 0755.");
        }
        string helperText = await File.ReadAllTextAsync(paths.FinderHelperPath, cancellationToken);
        if (!helperText.StartsWith("#!/bin/zsh\nset -euo pipefail\n", StringComparison.Ordinal) ||
            !helperText.Contains(
                $"cue_name='{Path.GetFileName(paths.OutputCuePath)}'",
                StringComparison.Ordinal) ||
            !helperText.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The frozen terrain Finder helper lost exact CUE reveal semantics.");
        }
        string checklist = await File.ReadAllTextAsync(paths.RuntimeChecklistPath, cancellationToken);
        VerifyChecklist(checklist, finderReveal, plan.LoadCodes, paths);
        string guide = await File.ReadAllTextAsync(paths.LocationGuidePath, cancellationToken);
        VerifyLocationGuide(guide, outputImageSha256);

        DiscLayout layout = DiscImage.DetectLayout(paths.OutputImagePath);
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException("The published terrain BIN is not MODE2/2352.");
        await using FileStream image = File.OpenRead(paths.OutputImagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != WadByteLength)
            throw new InvalidDataException("The published terrain WAD extent changed.");
        byte[] fullData = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            DataWadOffset,
            DataByteLength);
        string outputDataSha256 = Hash(fullData);
        RequireHash(outputDataSha256, ExpectedOutputDataSha256, "published full ID65 row-80 data");
        byte[] model = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            ModelWadOffset,
            ModelByteLength);
        RequireHash(Hash(model), AuthoredModelSha256, "published two-tile model");
        RequireHash(
            Hash(model.AsSpan(AuthoredCollisionRelativeOffset, 0x5FAF8)),
            AuthoredCollisionSha256,
            "published two-tile collision");
        RequireHash(
            Hash(model.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength)),
            AuthoredCollisionTreeSha256,
            "published collision tree");
        RequireHash(
            Hash(model.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength)),
            AuthoredCollisionBlocksSha256,
            "published collision blocks");
        int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            image,
            layout,
            CoalesceRawSectorRanges(ExpectedChangedRawSectorLbas));
        if (verifiedRawSectors != ExpectedChangedRawSectorCount)
            throw new InvalidDataException("The frozen terrain raw-sector integrity count changed.");
        foreach (int lba in ExpectedChangedRawSectorLbas)
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(image, layout, lba, 0x08);

        return new(
            paths,
            plan,
            receipt,
            finderReveal,
            plan.LoadCodes,
            outputImageSha256,
            outputDataSha256,
            receipt.ChangedLogicalWadBytes,
            receipt.ChangedPhysicalImageBytes,
            receipt.RebuiltRawSectorCount,
            receipt.ChangedRawSectorCount,
            receipt.RawSectorDiffs,
            receipt.RawSectorDiffSha256,
            receipt.ExactLogicalDiffBoundaryVerified,
            receipt.ExactPhysicalSectorBoundaryVerified,
            receipt.FoundationCompositionVerified,
            receipt.ModelReadbackVerified,
            receipt.CollisionReadbackVerified,
            receipt.CollisionSemanticDeltaVerified,
            receipt.ExactInverseVerified,
            receipt.AtomicRejectMatrixVerified,
            receipt.OcclusionOwnershipVerified,
            receipt.ProtectedSubfilesPreserved,
            receipt.SpawnAndPlayerAnchorPreserved,
            receipt.CycloramaPreserved,
            receipt.DisplayNamePreserved,
            receipt.RawSectorIntegrityVerified,
            receipt.BaseCandidatePreserved,
            receipt.FullDirectoryPublicationVerified,
            receipt.DisposableRuntimeCandidateAuthorized,
            receipt.RollbackRecoveryVerified,
            receipt.FullDirectoryRollbackVerified,
            receipt.FinderHandoffVerified,
            receipt.PromotionAuthorized,
            receipt.NormalCreateBinEnabled);
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string baseImage,
        string baseCue,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        UnusedLevel65FoundationTerrainStaticPlan added,
        UnusedLevel65FoundationTerrainStaticPlan inverse,
        int rejectionCount,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        bool requestFinderReveal,
        CancellationToken cancellationToken)
    {
        string stagedImage = StagedPath(stagedDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stagedDirectory, finalPaths.OutputCuePath);
        string stagedPlan = StagedPath(stagedDirectory, finalPaths.FoundationCompositionPlanPath);
        string stagedReceipt = StagedPath(stagedDirectory, finalPaths.StaticReadbackReceiptPath);
        string stagedChecklist = StagedPath(stagedDirectory, finalPaths.RuntimeChecklistPath);
        string stagedGuide = StagedPath(stagedDirectory, finalPaths.LocationGuidePath);

        await DiscImageWorkingCopy.StageAsync(
            baseImage,
            stagedImage,
            consumeDisposableSource: false,
            cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(stagedImage);
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException("The staged terrain add/remove image is not MODE2/2352.");

        int rebuiltRawSectorCount;
        IReadOnlyList<int> rebuiltRawSectorLbas;
        await using (FileStream image = new(
                         stagedImage,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 128 * 1024,
                         FileOptions.Asynchronous))
        {
            DiscFileRecord wad = DiscImage.FindRootFileRecord(
                image,
                layout,
                name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
            if (wad.Lba != WadLba || wad.Size != WadByteLength)
                throw new InvalidDataException("The staged terrain add/remove WAD extent changed.");
            byte[] modelPreimage = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                ModelWadOffset,
                ModelByteLength);
            RequireHash(Hash(modelPreimage), ModelPreimageSha256, "staged foundation model preimage");
            if (!modelPreimage.SequenceEqual(added.SourceModelBytes))
                throw new InvalidDataException("The staged model differs from the immutable composer source.");
            IReadOnlyList<(long Offset, int ByteLength)> changedModelRuns =
                BuildChangedLogicalRuns(modelPreimage, added.OutputModelBytes, ModelWadOffset);
            if (changedModelRuns.Count == 0)
                throw new InvalidDataException("The second terrain tile produced no logical model changes.");
            rebuiltRawSectorLbas = RawSectorLbasForFileRanges(WadLba, changedModelRuns);
            DiscImage.WriteFileBytes(image, layout, WadLba, ModelWadOffset, added.OutputModelBytes);
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                WadLba,
                changedModelRuns);
            int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                CoalesceRawSectorRanges(rebuiltRawSectorLbas));
            if (rebuiltRawSectorCount != rebuiltRawSectorLbas.Count ||
                verifiedRawSectors != rebuiltRawSectorLbas.Count)
            {
                throw new InvalidDataException(
                    $"The terrain diff rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectors} " +
                    $"sectors, expected {rebuiltRawSectorLbas.Count}.");
            }
            await image.FlushAsync(cancellationToken);
            image.Flush(flushToDisk: true);
        }

        string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(stagedImage));
        await WriteTextAsync(stagedCue, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");
        FoundationReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            stagedImage,
            layout,
            added,
            inverse,
            rebuiltRawSectorLbas,
            cancellationToken);
        RequireOptionalPin(readback.OutputImageSha256, ExpectedOutputImageSha256, "terrain output BIN");
        RequireOptionalPin(readback.OutputDataSha256, ExpectedOutputDataSha256, "terrain output ID65 data");
        string rawDiff = HashRawSectorDiffs(readback.RawSectorDiffs);
        if ((ExpectedChangedLogicalWadBytes >= 0 &&
             readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes) ||
            (ExpectedChangedPhysicalImageBytes >= 0 &&
             readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes) ||
            (ExpectedChangedRawSectorCount >= 0 &&
             readback.ChangedRawSectorCount != ExpectedChangedRawSectorCount) ||
            (ExpectedChangedRawSectorLbas.Length > 0 &&
             !readback.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                 .SequenceEqual(ExpectedChangedRawSectorLbas)) ||
            (!string.IsNullOrEmpty(ExpectedRawSectorDiffSha256) &&
             rawDiff != ExpectedRawSectorDiffSha256))
        {
            throw new InvalidDataException(
                $"The pinned terrain diff changed: logical={readback.ChangedLogicalWadBytes}, " +
                $"physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}, raw={rawDiff}.");
        }

        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan plan = new(
            PlanSchemaVersion,
            ProfileId,
            UnusedLevel65FoundationTerrainComposer.ProfileId,
            BaseImageSha256,
            ModelPreimageSha256,
            AuthoredModelSha256,
            AuthoredCollisionSha256,
            AuthoredCollisionTreeSha256,
            AuthoredCollisionBlocksSha256,
            ExactInverseModelSha256,
            ComposerPlanSha256,
            WadLba,
            ModelWadOffset,
            ModelByteLength,
            added.Sector.SectorIndex,
            AddedLowDetailVertexCount: 1,
            AddedLowDetailFaceCount: 1,
            AddedHighDetailVertexCount: 1,
            AddedHighDetailFaceCount: 1,
            added.Collision.AppendedTriangleIndex,
            NativeCollisionCellCount: 4_252,
            AuthoredCollisionCellCount: added.Collision.NativeCellCount,
            added.Collision.ChangedCells,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            rawDiff,
            loadCodes,
            AtomicRejectMatrixCount: rejectionCount,
            ExactInverseVerified: true,
            FullExposureProofVerified: true,
            SpawnAndPlayerAnchorVerified: true,
            CycloramaVerified: true,
            DisplayNameVerified: true,
            RequiresDuckStationRuntimeProof: true,
            DisposableRuntimeCandidateAuthorized: true,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
        await WriteJsonAsync(stagedPlan, plan, cancellationToken);

        bool includeFinderReveal = requestFinderReveal && OperatingSystem.IsMacOS();
        RuntimeCandidateFinderReveal? stagedReveal = includeFinderReveal
            ? await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(stagedCue, cancellationToken)
            : null;
        RuntimeCandidateFinderReveal? finalReveal = stagedReveal == null
            ? null
            : BuildFinalFinderReveal(finalPaths);
        string checklist = BuildRuntimeChecklist(finalPaths, finalReveal, loadCodes, readback);
        await WriteTextAsync(
            stagedChecklist,
            checklist,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        string locationGuide = BuildLocationGuideSvg(loadCodes, readback.OutputImageSha256);
        await WriteTextAsync(
            stagedGuide,
            locationGuide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        string outputCueSha256 = await HashFileAsync(stagedCue, cancellationToken);
        string terrainPlanSha256 = await HashFileAsync(stagedPlan, cancellationToken);
        string runtimeChecklistSha256 = await HashFileAsync(stagedChecklist, cancellationToken);
        string locationGuideSha256 = await HashFileAsync(stagedGuide, cancellationToken);
        string? finderHelperSha256 = stagedReveal == null
            ? null
            : await HashFileAsync(stagedReveal.HelperPath, cancellationToken);

        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            baseImage,
            baseCue,
            finalPaths.OutputDirectoryPath,
            finalPaths.OutputImagePath,
            finalPaths.OutputCuePath,
            finalPaths.FoundationCompositionPlanPath,
            finalPaths.RuntimeChecklistPath,
            finalPaths.LocationGuidePath,
            finalReveal?.HelperPath,
            outputCueSha256,
            terrainPlanSha256,
            runtimeChecklistSha256,
            locationGuideSha256,
            finderHelperSha256,
            BaseImageSha256,
            readback.OutputImageSha256,
            readback.OutputDataSha256,
            AuthoredModelSha256,
            AuthoredCollisionSha256,
            AuthoredCollisionTreeSha256,
            AuthoredCollisionBlocksSha256,
            ExactInverseModelSha256,
            ComposerPlanSha256,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs,
            rawDiff,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FoundationCompositionVerified: true,
            ModelReadbackVerified: true,
            CollisionReadbackVerified: true,
            CollisionSemanticDeltaVerified: true,
            ExactInverseVerified: true,
            AtomicRejectMatrixVerified: true,
            OcclusionOwnershipVerified: true,
            ProtectedSubfilesPreserved: true,
            SpawnAndPlayerAnchorPreserved: true,
            CycloramaPreserved: true,
            DisplayNamePreserved: true,
            RawSectorIntegrityVerified: true,
            BaseCandidatePreserved: true,
            FullDirectoryPublicationVerified: true,
            DisposableRuntimeCandidateAuthorized: true,
            // Recovery history belongs to the immediate CreateAsync result. The durable
            // artifact is a deterministic clean publication and must never persist a
            // forgeable historical claim.
            RollbackRecoveryVerified: false,
            FullDirectoryRollbackVerified: false,
            FinderHandoffVerified: finalReveal != null,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);
        VerifyStagedCandidateArtifacts(stagedDirectory, finalPaths, includeFinderReveal);
        VerifyTextReadback(stagedPlan, JsonSerializer.Serialize(plan, JsonOptions) + "\n", "terrain plan");
        VerifyTextReadback(stagedReceipt, JsonSerializer.Serialize(receipt, JsonOptions) + "\n", "static receipt");
        VerifyChecklist(checklist, finalReveal, loadCodes, finalPaths);
        VerifyLocationGuide(locationGuide, readback.OutputImageSha256);
        return new(plan, receipt, stagedReveal, readback, rebuiltRawSectorCount);
    }

    private static async Task<FoundationReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        DiscLayout expectedLayout,
        UnusedLevel65FoundationTerrainStaticPlan added,
        UnusedLevel65FoundationTerrainStaticPlan inverse,
        IReadOnlyList<int> rebuiltRawSectorLbas,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (outputLayout != expectedLayout)
            throw new InvalidDataException("The terrain add/remove output changed the raw disc layout.");
        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The terrain add/remove output changed the disc-image length.");
        DiscFileRecord baseWad = DiscImage.FindRootFileRecord(
            baseline,
            expectedLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (baseWad != outputWad || outputWad.Lba != WadLba || outputWad.Size != WadByteLength)
            throw new InvalidDataException("The terrain add/remove output moved or resized WAD.WAD.");

        byte[] outputModel = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            ModelWadOffset,
            ModelByteLength);
        if (!outputModel.SequenceEqual(added.OutputModelBytes))
            throw new InvalidDataException("The two-tile model failed exact byte-for-byte composer readback.");
        RequireHash(Hash(outputModel), AuthoredModelSha256, "two-tile model readback");
        RequireHash(
            Hash(outputModel.AsSpan(AuthoredCollisionRelativeOffset, 0x5FAF8)),
            AuthoredCollisionSha256,
            "two-tile collision readback");
        RequireHash(
            Hash(outputModel.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength)),
            AuthoredCollisionTreeSha256,
            "two-tile collision tree readback");
        RequireHash(
            Hash(outputModel.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength)),
            AuthoredCollisionBlocksSha256,
            "two-tile collision blocks readback");
        if (!inverse.OutputModelBytes.SequenceEqual(added.SourceModelBytes) ||
            inverse.OutputModelSha256 != ExactInverseModelSha256)
        {
            throw new InvalidDataException("The exact inverse did not byte-restore the foundation model.");
        }

        foreach (UnusedLevel65FoundationTerrainComponentReadback component in added.Components)
        {
            int relative = checked((int)(component.WadOffset - ModelWadOffset));
            string actual = Hash(outputModel.AsSpan(relative, component.ByteLength));
            RequireHash(actual, component.Sha256, $"two-tile {component.Name} component");
        }

        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            expectedLayout,
            outputWad,
            cancellationToken);
        if (logical.ChangedBytes <= 0 || logical.OutsideModelBytes != 0)
        {
            throw new InvalidDataException(
                $"The terrain logical diff changed {logical.ChangedBytes} WAD bytes, " +
                $"including {logical.OutsideModelBytes} outside the fixed model subfile.");
        }
        if (!logical.ChangedRawSectorLbas.SequenceEqual(rebuiltRawSectorLbas))
        {
            throw new InvalidDataException(
                "The diff-derived MODE2 rebuild sector set does not exactly match logical model readback.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            logical.ChangedRawSectorLbas,
            cancellationToken);
        if (physical.ChangedRawSectorCount <= 0 ||
            physical.RawSectorDiffs.Any(diff =>
                diff.HeaderChangedBytes != 0 ||
                diff.SubheaderChangedBytes != 0 ||
                diff.ReservedChangedBytes != 0))
        {
            throw new InvalidDataException(
                $"The terrain physical diff changed {physical.ChangedRawSectorCount} sectors; " +
                $"header={physical.RawSectorDiffs.Sum(diff => diff.HeaderChangedBytes)}, " +
                $"subheader={physical.RawSectorDiffs.Sum(diff => diff.SubheaderChangedBytes)}, " +
                $"reserved={physical.RawSectorDiffs.Sum(diff => diff.ReservedChangedBytes)}.");
        }
        if (RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                output,
                outputLayout,
                CoalesceRawSectorRanges(rebuiltRawSectorLbas)) != rebuiltRawSectorLbas.Count)
        {
            throw new InvalidDataException("The published terrain sector integrity verification count changed.");
        }
        foreach (int lba in rebuiltRawSectorLbas)
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(output, outputLayout, lba, 0x08);

        byte[] outputData = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            DataWadOffset,
            DataByteLength);
        return new(
            await HashFileAsync(outputImagePath, cancellationToken),
            Hash(outputData),
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
            physical.RawSectorDiffs);
    }

    private static void ValidateComposerGate(
        UnusedLevel65FoundationTerrainStaticPlan foundation,
        UnusedLevel65FoundationTerrainStaticPlan added,
        UnusedLevel65FoundationTerrainStaticPlan repeated,
        UnusedLevel65FoundationTerrainStaticPlan inverse)
    {
        bool exact =
            foundation.ProfileId == UnusedLevel65FoundationTerrainComposer.ProfileId &&
            added.ProfileId == foundation.ProfileId &&
            repeated.ProfileId == foundation.ProfileId &&
            inverse.ProfileId == foundation.ProfileId &&
            foundation.SourceImageSha256 == BaseImageSha256 &&
            added.SourceImageSha256 == BaseImageSha256 &&
            foundation.SourceModelSha256 == ModelPreimageSha256 &&
            foundation.OutputModelSha256 == ModelPreimageSha256 &&
            added.SourceModelSha256 == ModelPreimageSha256 &&
            added.OutputModelSha256 == AuthoredModelSha256 &&
            repeated.OutputModelSha256 == AuthoredModelSha256 &&
            inverse.OutputModelSha256 == ExactInverseModelSha256 &&
            added.DeterministicPlanSha256 == ComposerPlanSha256 &&
            repeated.DeterministicPlanSha256 == ComposerPlanSha256 &&
            foundation.OptionalTileCount == 0 &&
            added.OptionalTileCount == 1 &&
            repeated.OptionalTileCount == 1 &&
            inverse.OptionalTileCount == 0 &&
            added.Manifest.OptionalTiles.Count == 1 &&
            added.Manifest.OptionalTiles[0].TileId == UnusedLevel65FoundationTerrainComposer.OptionalTileId &&
            added.Sector.SectorIndex == 213 &&
            added.Sector.LowDetailVertexCount == 39 &&
            added.Sector.LowDetailFaceCount == 23 &&
            added.Sector.HighDetailVertexCount == 144 &&
            added.Sector.HighDetailFaceCount == 115 &&
            added.Collision.TriangleCount == 19_809 &&
            added.Collision.AppendedTriangleIndex == 19_808 &&
            added.Collision.NativeCellCount == 4_252 &&
            added.Collision.ChangedCells.SequenceEqual(new[]
            {
                new UnusedLevel65FoundationCollisionCell(30, 24, 2),
                new UnusedLevel65FoundationCollisionCell(30, 25, 2),
                new UnusedLevel65FoundationCollisionCell(31, 25, 2)
            }) &&
            added.Collision.TreeSha256 == AuthoredCollisionTreeSha256 &&
            added.Collision.BlocksSha256 == AuthoredCollisionBlocksSha256 &&
            added.Collision.CollisionSha256 == AuthoredCollisionSha256 &&
            added.Collision.AllUnchangedCellSequencesPreserved &&
            added.Collision.NativeDescendingOrderPreserved &&
            added.Collision.AssignmentZeroVerified &&
            added.Collision.ExistingFlagsPreserved &&
            added.Collision.OrdinarySurfaceVerified &&
            added.LockedFoundationTilePreserved &&
            added.ExplicitVertexHandlesVerified &&
            added.HpLpPairingVerified &&
            added.CollisionIndexRepacked &&
            added.OcclusionOwnershipVerified &&
            added.ExactInverseVerified &&
            added.SourceModelPreserved &&
            added.DeterministicReadbackRequired &&
            !added.WritesDiscImage && !added.WritesCue && !added.AppIntegrated &&
            !added.CreateBinEnabled && !added.ReleaseIntegrated &&
            !added.PromotionAuthorized && !added.Publishable &&
            added.OutputModelBytes.SequenceEqual(repeated.OutputModelBytes) &&
            inverse.OutputModelBytes.SequenceEqual(foundation.SourceModelBytes) &&
            inverse.OutputModelBytes.SequenceEqual(added.SourceModelBytes);
        if (!exact)
            throw new InvalidDataException("The exact committed two-tile/inverse composer gate changed.");
    }

    private static int VerifyAtomicRejectMatrix(
        string foundationImagePath,
        UnusedLevel65FoundationTerrainManifest foundationManifest,
        UnusedLevel65FoundationTerrainManifest twoTileManifest)
    {
        int count = 0;
        void ExpectReject(
            UnusedLevel65FoundationTerrainManifest manifest,
            string text,
            UnusedLevel65FoundationTerrainSafetyLimits? limits = null)
        {
            bool rejected = false;
            try
            {
                _ = limits == null
                    ? UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, manifest)
                    : UnusedLevel65FoundationTerrainComposer.BuildStaticPlanForSmoke(
                        foundationImagePath, manifest, limits);
            }
            catch (Exception ex) when (ex.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                rejected = true;
            }
            if (!rejected)
                throw new InvalidDataException($"The terrain composer did not reject matrix row {count + 1}: {text}.");
            count++;
        }

        UnusedLevel65FoundationTerrainTile exactTile = twoTileManifest.OptionalTiles[0];
        UnusedLevel65FoundationTerrainManifest WithOptional(
            IReadOnlyList<UnusedLevel65FoundationTerrainVertex> vertices,
            IReadOnlyList<UnusedLevel65FoundationTerrainTile> tiles) =>
            new(
                twoTileManifest.LockedFoundationTileId,
                vertices,
                twoTileManifest.LockedFoundationTile,
                tiles);

        ExpectReject(WithOptional(twoTileManifest.Vertices,
            [exactTile with { HighDetailVertexHandles = ["foundation-b", "foundation-c", "foundation-d"] }]),
            "LP and HP topology");
        ExpectReject(WithOptional(twoTileManifest.Vertices, [exactTile with { SectorIndex = 212 }]),
            "only scene sector 213");
        ExpectReject(WithOptional(twoTileManifest.Vertices,
            [exactTile with
            {
                LowDetailVertexHandles = ["foundation-b", "foundation-c", "foundation-d"],
                HighDetailVertexHandles = ["foundation-b", "foundation-c", "foundation-d"]
            }]), "upward winding");

        List<UnusedLevel65FoundationTerrainVertex> zero =
            [.. twoTileManifest.Vertices, new("zero-copy", new(7890, 6346, 512))];
        ExpectReject(WithOptional(zero,
            [exactTile with
            {
                LowDetailVertexHandles = ["foundation-b", "zero-copy", "foundation-c"],
                HighDetailVertexHandles = ["foundation-b", "zero-copy", "foundation-c"]
            }]), "zero area");
        List<UnusedLevel65FoundationTerrainVertex> cull =
            [.. twoTileManifest.Vertices.Where(v => v.Handle != "foundation-d"),
                new("foundation-d", new(9000, 6474, 640))];
        ExpectReject(WithOptional(cull, [exactTile]), "cull sphere");
        List<UnusedLevel65FoundationTerrainVertex> pack =
            [.. twoTileManifest.Vertices.Where(v => v.Handle != "foundation-d"),
                new("foundation-d", new(7954, 6474, 800))];
        ExpectReject(WithOptional(pack, [exactTile]), "cannot pack into the native row");

        List<UnusedLevel65FoundationTerrainVertex> absent =
        [
            .. foundationManifest.Vertices,
            new("absent-e", new(8448, 6400, 512)),
            new("absent-f", new(8500, 6400, 512)),
            new("absent-g", new(8448, 6450, 512))
        ];
        UnusedLevel65FoundationTerrainTile absentTile = exactTile with
        {
            LowDetailVertexHandles = ["absent-e", "absent-f", "absent-g"],
            HighDetailVertexHandles = ["absent-e", "absent-f", "absent-g"]
        };
        ExpectReject(WithOptional(absent, [absentTile]), "absent from the native tree");
        ExpectReject(WithOptional(twoTileManifest.Vertices,
            [exactTile with { OrdinaryCollision = false, CollisionSurfaceIndex = 1 }]),
            "Nonordinary collision flags");
        ExpectReject(WithOptional(twoTileManifest.Vertices,
            [exactTile with { OcclusionAssignment = 3 }]), "does not own sector");
        ExpectReject(twoTileManifest, "model-tail bytes",
            new(AvailableModelTailBytes: 0x2F));
        ExpectReject(twoTileManifest, "block bytes",
            new(CollisionBlockCapacityBytes: 0x17966));
        ExpectReject(twoTileManifest, "15-bit triangle",
            new(MaximumTriangleCount: 19_808));

        UnusedLevel65FoundationTerrainTile[] tooManyFaces = Enumerable.Range(0, 142)
            .Select(index => exactTile with { TileId = $"capacity-face-{index}" })
            .ToArray();
        ExpectReject(WithOptional(twoTileManifest.Vertices, tooManyFaces), "face-count byte");

        List<UnusedLevel65FoundationTerrainVertex> tooManyVertices = [.. foundationManifest.Vertices];
        List<UnusedLevel65FoundationTerrainTile> vertexCapacityTiles = [];
        for (int tileIndex = 0; tileIndex < 8; tileIndex++)
        {
            string[] handles = new string[3];
            for (int pointIndex = 0; pointIndex < 3; pointIndex++)
            {
                string handle = $"capacity-v-{tileIndex * 3 + pointIndex}";
                handles[pointIndex] = handle;
                int baseX = 7700 + (tileIndex * 5);
                int baseY = 6200 + (tileIndex * 5);
                UnusedLevel65FoundationTerrainPoint point = pointIndex switch
                {
                    0 => new(baseX, baseY, 512),
                    1 => new(baseX + 4, baseY, 512),
                    _ => new(baseX, baseY + 4, 512)
                };
                tooManyVertices.Add(new(handle, point));
            }
            vertexCapacityTiles.Add(exactTile with
            {
                TileId = $"capacity-vertex-{tileIndex}",
                LowDetailVertexHandles = handles,
                HighDetailVertexHandles = handles
            });
        }
        tooManyVertices.Add(new("capacity-v-24", new(7740, 6240, 512)));
        tooManyVertices.Add(new("capacity-v-25", new(7744, 6240, 512)));
        vertexCapacityTiles.Add(exactTile with
        {
            TileId = "capacity-vertex-8",
            LowDetailVertexHandles = ["capacity-v-25", "capacity-v-24", "capacity-v-0"],
            HighDetailVertexHandles = ["capacity-v-25", "capacity-v-24", "capacity-v-0"]
        });
        ExpectReject(WithOptional(tooManyVertices, vertexCapacityTiles), "LP six-bit vertex");

        bool removeRejected = false;
        try
        {
            _ = UnusedLevel65FoundationTerrainComposer.RemoveOptionalSecondTile(foundationManifest);
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("not active", StringComparison.OrdinalIgnoreCase))
        {
            removeRejected = true;
        }
        if (!removeRejected)
            throw new InvalidDataException("Removing an inactive optional tile was not rejected.");
        count++;

        ExpectReject(WithOptional(twoTileManifest.Vertices,
            [exactTile with { TileId = "tampered-id" }]), "two-tile oracle");

        byte[] mutated = UnusedLevel65FoundationTerrainComposer
            .BuildStaticPlan(foundationImagePath, foundationManifest).SourceModelBytes.ToArray();
        mutated[^1] = 1;
        bool modelRejected = false;
        try
        {
            _ = UnusedLevel65FoundationTerrainComposer.BuildStaticPlanFromModelForSmoke(
                mutated, twoTileManifest);
        }
        catch (InvalidDataException ex) when (
            ex.Message.Contains("accepts only exact foundation model", StringComparison.OrdinalIgnoreCase))
        {
            modelRejected = true;
        }
        if (!modelRejected)
            throw new InvalidDataException("A mutated foundation model was not rejected.");
        count++;
        return count;
    }

    private static LogicalDiff CompareLogicalWad(
        FileStream baseline,
        FileStream output,
        DiscLayout layout,
        DiscFileRecord wad,
        CancellationToken cancellationToken)
    {
        const int chunkByteLength = 64 * 1024;
        long changed = 0;
        long outsideModel = 0;
        SortedSet<int> changedRawSectorLbas = [];
        for (long offset = 0; offset < wad.Size; offset += chunkByteLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int take = checked((int)Math.Min(chunkByteLength, wad.Size - offset));
            byte[] before = DiscImage.ReadFileBytes(baseline, layout, wad.Lba, offset, take);
            byte[] after = DiscImage.ReadFileBytes(output, layout, wad.Lba, offset, take);
            for (int index = 0; index < take; index++)
            {
                if (before[index] == after[index])
                    continue;
                long wadOffset = offset + index;
                changed++;
                if (wadOffset < ModelWadOffset || wadOffset >= ModelWadOffset + ModelByteLength)
                    outsideModel++;
                changedRawSectorLbas.Add(checked(wad.Lba + (int)(wadOffset / LogicalSectorByteLength)));
            }
        }
        return new(changed, outsideModel, changedRawSectorLbas.ToArray());
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The foundation physical image comparison has incompatible lengths.");
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        List<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> diffs = [];
        long changedBytes = 0;
        byte[] before = new byte[RawSectorByteLength];
        byte[] after = new byte[RawSectorByteLength];
        int sectorCount = checked((int)(baseline.Length / RawSectorByteLength));
        baseline.Position = 0;
        output.Position = 0;
        for (int lba = 0; lba < sectorCount; lba++)
        {
            if ((lba & 0x3FF) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            ReadRawSectorExactly(baseline, before, lba, "baseline");
            ReadRawSectorExactly(output, after, lba, "output");
            if (before.AsSpan().SequenceEqual(after))
                continue;
            if (!allowed.Contains(lba))
                throw new InvalidDataException($"Physical image bytes changed outside a logical model sector at LBA {lba}.");

            int header = 0;
            int subheader = 0;
            int payload = 0;
            int edc = 0;
            int reserved = 0;
            int eccP = 0;
            int eccQ = 0;
            int total = 0;
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                total++;
                if (index < 16)
                    header++;
                else if (index < 24)
                    subheader++;
                else if (index < 2072)
                    payload++;
                else if (index < 2076)
                    edc++;
                else if (index < 2248)
                    eccP++;
                else
                    eccQ++;
            }
            if (payload == 0)
                throw new InvalidDataException($"Raw sector {lba} changed without a logical payload delta.");
            changedBytes += total;
            diffs.Add(new(lba, header, subheader, payload, edc, reserved, eccP, eccQ, total));
        }
        if (!diffs.Select(diff => diff.RawSectorLba).SequenceEqual(allowedRawSectorLbas))
        {
            throw new InvalidDataException(
                "The physical changed-sector set does not exactly equal the logical changed-sector set.");
        }
        return new(changedBytes, diffs.Count, diffs);
    }

    private static void ReadRawSectorExactly(
        FileStream stream,
        byte[] destination,
        int lba,
        string label)
    {
        long offset = checked((long)lba * RawSectorByteLength);
        long requiredEnd = checked(offset + RawSectorByteLength);
        if (requiredEnd > stream.Length)
        {
            throw new EndOfStreamException(
                $"The {label} raw-sector read for LBA {lba} needs bytes {offset}..{requiredEnd - 1}, " +
                $"but stream length is {stream.Length}.");
        }
        stream.Position = offset;
        int total = 0;
        while (total < RawSectorByteLength)
        {
            int read = stream.Read(destination, total, RawSectorByteLength - total);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"The {label} raw-sector read stopped at LBA {lba}, byte {total}/2352; " +
                    $"stream position={stream.Position}, length={stream.Length}.");
            }
            total += read;
        }
    }

    private static string HashRawSectorDiffs(
        IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff diff in diffs)
        {
            builder.Append(diff.RawSectorLba).Append(':')
                .Append(diff.HeaderChangedBytes).Append(':')
                .Append(diff.SubheaderChangedBytes).Append(':')
                .Append(diff.PayloadChangedBytes).Append(':')
                .Append(diff.EdcChangedBytes).Append(':')
                .Append(diff.ReservedChangedBytes).Append(':')
                .Append(diff.EccPChangedBytes).Append(':')
                .Append(diff.EccQChangedBytes).Append(':')
                .Append(diff.TotalChangedBytes).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static IReadOnlyList<(long Offset, int ByteLength)> BuildChangedLogicalRuns(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after,
        long baseWadOffset)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The foundation model preimage/composition lengths differ.");
        List<(long Offset, int ByteLength)> runs = [];
        int cursor = 0;
        while (cursor < before.Length)
        {
            while (cursor < before.Length && before[cursor] == after[cursor])
                cursor++;
            if (cursor == before.Length)
                break;
            int start = cursor;
            while (cursor < before.Length && before[cursor] != after[cursor])
                cursor++;
            runs.Add((checked(baseWadOffset + start), checked(cursor - start)));
        }
        return runs;
    }

    private static IReadOnlyList<int> RawSectorLbasForFileRanges(
        int fileLba,
        IReadOnlyList<(long Offset, int ByteLength)> ranges)
    {
        SortedSet<int> sectors = [];
        foreach ((long offset, int byteLength) in ranges)
        {
            int first = checked(fileLba + (int)(offset / LogicalSectorByteLength));
            int last = checked(fileLba + (int)((offset + byteLength - 1) / LogicalSectorByteLength));
            for (int lba = first; lba <= last; lba++)
                sectors.Add(lba);
        }
        return sectors.ToArray();
    }

    private static IReadOnlyList<(int Lba, int SectorCount)> CoalesceRawSectorRanges(
        IReadOnlyList<int> lbas)
    {
        if (lbas.Count == 0)
            throw new ArgumentException("At least one raw sector is required.", nameof(lbas));
        List<(int Lba, int SectorCount)> ranges = [];
        int start = lbas[0];
        int previous = start;
        for (int index = 1; index < lbas.Count; index++)
        {
            int current = lbas[index];
            if (current <= previous)
                throw new InvalidDataException("The derived raw-sector set is not strictly increasing.");
            if (current == previous + 1)
            {
                previous = current;
                continue;
            }
            ranges.Add((start, checked(previous - start + 1)));
            start = previous = current;
        }
        ranges.Add((start, checked(previous - start + 1)));
        return ranges;
    }

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        if (loadCodes.Count != 4 ||
            loadCodes[0].TestName != "ID65 candidate" || loadCodes[0].LevelId != 65 ||
            loadCodes[1].TestName != "Retail Town Square" || loadCodes[1].LevelId != 13 ||
            loadCodes[2].TestName != "Gnasty's Loot" || loadCodes[2].LevelId != 64 ||
            loadCodes[3].TestName != "Sunny Flight" || loadCodes[3].LevelId != 15 ||
            loadCodes.Any(code => string.IsNullOrWhiteSpace(code.InputCode)))
        {
            throw new InvalidDataException("The exact four ID65 comparison load codes changed.");
        }
    }

    private static string BuildRuntimeChecklist(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        FoundationReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Second Terrain Tile Add/Remove — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This candidate keeps the locked foundation triangle and adds exactly one removable HP+LP triangle " +
            "with matching ordinary collision. It is runtime-pending and is not normal Create BIN or release output.");
        builder.AppendLine();
        if (finderReveal != null)
        {
            RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        }
        else
        {
            builder.AppendLine("## Candidate disc");
            builder.AppendLine();
            builder.AppendLine($"- CUE: **{Path.GetFileName(paths.OutputCuePath)}**");
            builder.AppendLine("- DuckStation: load the **CUE**, not the BIN.");
            builder.AppendLine();
        }
        builder.AppendLine("## Exact static identity");
        builder.AppendLine();
        builder.AppendLine($"- Foundation BIN SHA-256: {BaseImageSha256}");
        builder.AppendLine($"- Candidate BIN SHA-256: {readback.OutputImageSha256}");
        builder.AppendLine($"- ID65 row-80 data SHA-256: {readback.OutputDataSha256}");
        builder.AppendLine($"- Foundation model SHA-256: {ModelPreimageSha256}");
        builder.AppendLine($"- Two-tile model SHA-256: {AuthoredModelSha256}");
        builder.AppendLine($"- Exact remove/inverse model SHA-256: {ExactInverseModelSha256}");
        builder.AppendLine($"- Composer plan SHA-256: {ComposerPlanSha256}");
        builder.AppendLine($"- Two-tile collision/tree/blocks: {AuthoredCollisionSha256} / {AuthoredCollisionTreeSha256} / {AuthoredCollisionBlocksSha256}");
        builder.AppendLine(
            $"- Logical/physical diff: {readback.ChangedLogicalWadBytes} WAD bytes / " +
            $"{readback.ChangedPhysicalImageBytes} raw-image bytes across " +
            $"{readback.ChangedRawSectorCount} changed raw sectors.");
        builder.AppendLine("- Static reject matrix: 17/17; runtime status: **PENDING / UNPROMOTED**.");
        builder.AppendLine($"- Location guide: **{Path.GetFileName(paths.LocationGuidePath)}**");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## Clean-run controls");
        builder.AppendLine();
        builder.AppendLine("1. Quit DuckStation completely. Disable Moon Jump and every other cheat.");
        builder.AppendLine("2. Set Memory Card 1 to **None** and Memory Card 2 to **None**. Do not save.");
        builder.AppendLine("3. Do not use a save state. Cold boot the candidate CUE, then enter the ID65 load code once.");
        builder.AppendLine("4. Use the location guide to identify locked Tile 0 (A-B-C) and removable Tile 1 (B-D-C).");
        builder.AppendLine();
        builder.AppendLine("## Candidate gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] **Two distinct halves:** both triangular halves are visible and meet at the B-C seam; Tile 0 remains unchanged and Tile 1 fills the adjacent half.");
        builder.AppendLine("- [ ] **Close HP — Tile 0:** inspect locked Tile 0 at close range; its original HP face is visible and stable.");
        builder.AppendLine("- [ ] **Close HP — Tile 1:** inspect removable Tile 1 at close range; its new HP face is visible and stable.");
        builder.AppendLine("- [ ] **Far LP — Tile 0:** back the camera away along the entry route to camera depth >=2780; locked Tile 0 remains visible as LP terrain.");
        builder.AppendLine("- [ ] **Far LP — Tile 1:** at that same >=2780 view, removable Tile 1 remains visible and both LP footprints are visible simultaneously.");
        builder.AppendLine("- [ ] **Return near — both HP halves:** return along the entry route to close range and re-confirm HP faces on Tile 0 and Tile 1.");
        builder.AppendLine("- [ ] **Collision — Tile 0 half:** walk, charge, jump, and land across the interior of Tile 0; the visible slope is solid on both sides.");
        builder.AppendLine("- [ ] **Collision — Tile 1 half:** repeat across the interior of Tile 1; the new visible slope is solid on both sides.");
        builder.AppendLine("- [ ] **Seam traversal:** cross B-C in both directions while walking and charging, then jump and land astride the seam; no pass-through, snag, launch, hover, or hidden plane.");
        builder.AppendLine("- [ ] **Outer-edge traversal:** cross the exposed outer edges of both halves in both directions; collision remains aligned to the visible terrain.");
        builder.AppendLine("- [ ] **Reset:** use DuckStation Reset, reload ID65 without a save state, and repeat both-half collision plus seam traversal.");
        builder.AppendLine("- [ ] **Cold boot repeat:** quit DuckStation, cold boot the CUE again with both memory-card slots None, and confirm close HP, far LP, both collision halves, and the seam.");
        builder.AppendLine();
        builder.AppendLine("## Retail comparison controls");
        builder.AppendLine();
        builder.AppendLine("- [ ] Retail Town Square loads normally and has no added second ID65 tile.");
        builder.AppendLine("- [ ] Gnasty's Loot loads and plays normally.");
        builder.AppendLine("- [ ] Sunny Flight loads and plays normally.");
        builder.AppendLine();
        builder.AppendLine("## Exact remove boundary");
        builder.AppendLine();
        builder.AppendLine(
            "The committed inverse is a static byte-restore oracle, not a promoted second disc: removing Tile 1 " +
            $"returns the model exactly to {ExactInverseModelSha256}. Do not treat this candidate as editor integration.");
        builder.AppendLine();
        builder.AppendLine("## Report boundary");
        builder.AppendLine();
        builder.AppendLine(
            "Report each checkbox separately. A load screen, still image, one camera distance, or one traversal " +
            "direction is not a pass. Keep both card slots None and do not package this candidate for release.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        string outputImageSha256)
    {
        VerifyLoadCodes(loadCodes);
        if (outputImageSha256.Length != 64)
            throw new InvalidDataException("The guide requires the exact candidate BIN hash.");
        string[] codeRows = loadCodes.Select(code =>
            XmlEscape($"{code.TestName} (ID {code.LevelId}): {code.InputCode}")).ToArray();
        return
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\" data-safe-left=\"60\" data-safe-right=\"1140\">\n" +
        "  <rect width=\"1200\" height=\"1000\" fill=\"#101522\"/>\n" +
        "  <text x=\"60\" y=\"55\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"30\" font-weight=\"700\">ID65 Terrain Add/Remove Runtime Gate</text>\n" +
        "  <text x=\"60\" y=\"88\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Compare the locked foundation half with the new removable half at close HP and far LP.</text>\n" +
        "  <rect x=\"60\" y=\"112\" width=\"1080\" height=\"430\" rx=\"22\" fill=\"#1b2437\" stroke=\"#6d7fa8\" stroke-width=\"3\"/>\n" +
        "  <polygon points=\"255,455 520,455 390,205\" fill=\"#7459d9\" stroke=\"#e3dcff\" stroke-width=\"5\"/>\n" +
        "  <polygon points=\"520,455 785,455 390,205\" fill=\"#35b985\" stroke=\"#d9fff0\" stroke-width=\"5\"/>\n" +
        "  <line x1=\"520\" y1=\"455\" x2=\"390\" y2=\"205\" stroke=\"#ffd45e\" stroke-width=\"8\"/>\n" +
        "  <text x=\"220\" y=\"492\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"19\" font-weight=\"700\">Tile 0 LOCKED (A-B-C)</text>\n" +
        "  <text x=\"590\" y=\"492\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"19\" font-weight=\"700\">Tile 1 REMOVABLE (B-D-C)</text>\n" +
        "  <text x=\"470\" y=\"330\" fill=\"#101522\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">B-C SEAM</text>\n" +
        "  <text x=\"835\" y=\"205\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"13\">A  (7762,6346,512)</text>\n" +
        "  <text x=\"835\" y=\"232\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"13\">B  (7890,6346,512)</text>\n" +
        "  <text x=\"835\" y=\"259\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"13\">C  (7826,6474,640)</text>\n" +
        "  <text x=\"835\" y=\"286\" fill=\"#8ff0c8\" font-family=\"Menlo,monospace\" font-size=\"13\">D  (7954,6474,640)</text>\n" +
        "  <text x=\"805\" y=\"336\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"15\">Close: inspect HP on each half.</text>\n" +
        "  <text x=\"805\" y=\"366\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"15\">Far: confirm LP on each half.</text>\n" +
        "  <text x=\"805\" y=\"396\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"15\">Cross seam both directions.</text>\n" +
        "  <text x=\"60\" y=\"575\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Runtime order</text>\n" +
        "  <text x=\"80\" y=\"607\" fill=\"#c8d6f4\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">1  Card 1 None + Card 2 None; cheats off; cold boot; no save state.</text>\n" +
        "  <text x=\"80\" y=\"635\" fill=\"#c8d6f4\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">2  Check close HP on Tile 0, Tile 1, then collision on both halves and B-C seam.</text>\n" +
        "  <text x=\"80\" y=\"663\" fill=\"#c8d6f4\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">3  Back along entry route to camera depth &gt;=2780; confirm both LP footprints.</text>\n" +
        "  <text x=\"80\" y=\"691\" fill=\"#c8d6f4\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">4  Return near: re-confirm HP on both; reset; then cold boot and repeat.</text>\n" +
        "  <text x=\"60\" y=\"730\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">Pinned candidate identity</text>\n" +
        $"  <text x=\"60\" y=\"754\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">Profile: {ProfileId}</text>\n" +
        $"  <text x=\"60\" y=\"774\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">CUE: {XmlEscape(OutputPrefix + ".cue")}</text>\n" +
        $"  <text x=\"60\" y=\"794\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"10\">BIN SHA-256: {outputImageSha256}</text>\n" +
        "  <text x=\"760\" y=\"794\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"11\" font-weight=\"700\">RUNTIME PENDING / UNPROMOTED</text>\n" +
        "  <text x=\"60\" y=\"830\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Full comparison load codes</text>\n" +
        $"  <text x=\"80\" y=\"855\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[0]}</text>\n" +
        $"  <text x=\"80\" y=\"879\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[1]}</text>\n" +
        $"  <text x=\"80\" y=\"903\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[2]}</text>\n" +
        $"  <text x=\"80\" y=\"927\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[3]}</text>\n" +
        "  <text x=\"60\" y=\"970\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Load the paired CUE. Both card slots None. Runtime pending; do not release.</text>\n" +
        "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
    {
        string[] required =
        [
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.LocationGuidePath),
            BaseImageSha256,
            ModelPreimageSha256,
            AuthoredModelSha256,
            ExactInverseModelSha256,
            ComposerPlanSha256,
            "load the **CUE**, not the BIN",
            "Memory Card 1 to **None**",
            "Memory Card 2 to **None**",
            "Close HP — Tile 0",
            "Close HP — Tile 1",
            "Far LP — Tile 0",
            "Far LP — Tile 1",
            "camera depth >=2780",
            "both LP footprints are visible simultaneously",
            "Return near — both HP halves",
            "Collision — Tile 0 half",
            "Collision — Tile 1 half",
            "Seam traversal",
            "Reset",
            "Cold boot repeat",
            "Retail Town Square",
            "Gnasty's Loot",
            "Sunny Flight",
            "PENDING / UNPROMOTED"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)) ||
            checklist.Contains("use a disposable memory card", StringComparison.OrdinalIgnoreCase) ||
            checklist.Contains("until the low-detail switch", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The terrain add/remove runtime checklist omitted an exact required gate.");
        }
        foreach (RuntimeCandidateLoadCode loadCode in loadCodes)
        {
            if (!checklist.Contains(loadCode.InputCode, StringComparison.Ordinal))
                throw new InvalidDataException($"The checklist omitted the {loadCode.TestName} input code.");
        }
        if (finderReveal != null)
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finderReveal, loadCodes);
    }

    private static void VerifyLocationGuide(string svg, string outputImageSha256)
    {
        string[] required =
        [
            "ID65 Terrain Add/Remove Runtime Gate",
            "Tile 0 LOCKED (A-B-C)",
            "Tile 1 REMOVABLE (B-D-C)",
            "B-C SEAM",
            "A  (7762,6346,512)",
            "B  (7890,6346,512)",
            "C  (7826,6474,640)",
            "D  (7954,6474,640)",
            "close HP on Tile 0, Tile 1",
            "camera depth &gt;=2780",
            "confirm both LP footprints",
            "Return near: re-confirm HP on both",
            "Card 1 None + Card 2 None",
            "reset; then cold boot and repeat",
            "width=\"1200\" height=\"1000\"",
            "data-safe-left=\"60\" data-safe-right=\"1140\"",
            "Pinned candidate identity",
            ProfileId,
            OutputPrefix + ".cue",
            outputImageSha256,
            "RUNTIME PENDING / UNPROMOTED",
            "font-size=\"10\""
        ];
        if (!svg.StartsWith("<?xml", StringComparison.Ordinal) ||
            outputImageSha256.Length != 64 ||
            required.Any(value => !svg.Contains(value, StringComparison.Ordinal)) ||
            svg.Contains("Back camera away; check far LP", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The terrain add/remove SVG guide failed deterministic readback.");
        }
    }

    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static void VerifyStagedCandidateArtifacts(
        string stagedDirectory,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths,
        bool includeFinderReveal)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            Path.GetFileName(paths.OutputImagePath),
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.FoundationCompositionPlanPath),
            Path.GetFileName(paths.StaticReadbackReceiptPath),
            Path.GetFileName(paths.RuntimeChecklistPath),
            Path.GetFileName(paths.LocationGuidePath)
        };
        if (includeFinderReveal)
            expected.Add(Path.GetFileName(paths.FinderHelperPath));
        string[] actual = Directory.EnumerateFiles(stagedDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (Directory.EnumerateDirectories(stagedDirectory).Any() ||
            !actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"The staged foundation artifact set is [{string.Join(',', actual)}], expected [{string.Join(',', expected.Order())}].");
        }
    }

    private static void BeginPublication(
        CandidatePublication publication,
        bool replaceExistingCandidate,
        Action<string>? testStageHook)
    {
        if (publication.HadPreviousCandidate)
        {
            if (!replaceExistingCandidate || !Directory.Exists(publication.OutputDirectoryPath))
                throw new IOException("The foundation destination changed before replacement publication.");
            if (Directory.Exists(publication.BackupDirectoryPath))
                throw new IOException("The terrain add/remove operation backup path already exists.");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            publication.PreviousBackedUp = true;
            WriteOperationJournal(publication, OperationPhasePreviousBackedUp);
            testStageHook?.Invoke("after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The foundation destination appeared during staging.");
        }

        if (!Directory.Exists(publication.StagedDirectoryPath))
            throw new IOException("The staged terrain add/remove candidate disappeared before publication.");
        Directory.Move(publication.StagedDirectoryPath, publication.OutputDirectoryPath);
        publication.CandidatePublished = true;
        WriteOperationJournal(publication, OperationPhaseCandidatePublished);
        testStageHook?.Invoke("after-candidate-publication");
    }

    private static void RollBackPublication(
        CandidatePublication publication,
        Exception publicationFailure,
        Action<string>? testStageHook)
    {
        if (!publication.PreviousBackedUp && !publication.CandidatePublished)
        {
            // A staging/readback failure happens before either publication move.
            // The existing output remains authoritative, so no backup restoration
            // is required even when the operation began with a previous candidate.
            WriteOperationJournal(publication, OperationPhaseRollbackComplete);
            publication.RecoveryComplete = true;
            return;
        }

        List<Exception> recoveryFailures = [];
        try
        {
            if (publication.CandidatePublished && Directory.Exists(publication.OutputDirectoryPath))
            {
                EnsureExactPublicationPath(publication.OutputDirectoryPath, publication);
                Directory.Delete(publication.OutputDirectoryPath, recursive: true);
            }
            publication.CandidatePublished = false;
            WriteOperationJournal(publication, OperationPhaseNewCandidateRemoved);
            testStageHook?.Invoke("after-new-candidate-removed");
        }
        catch (Exception ex)
        {
            recoveryFailures.Add(ex);
        }

        if (recoveryFailures.Count == 0)
        {
            try
            {
                if (publication.HadPreviousCandidate)
                {
                    testStageHook?.Invoke("before-previous-candidate-restore");
                    if (!Directory.Exists(publication.BackupDirectoryPath))
                        throw new IOException("The previous terrain add/remove candidate backup is missing.");
                    if (Directory.Exists(publication.OutputDirectoryPath))
                        throw new IOException("The foundation destination is occupied before backup restoration.");
                    Directory.Move(publication.BackupDirectoryPath, publication.OutputDirectoryPath);
                }
                publication.PreviousBackedUp = false;
                WriteOperationJournal(publication, OperationPhaseRollbackComplete);
                publication.RecoveryComplete = true;
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
        }

        if (recoveryFailures.Count > 0)
        {
            throw new IOException(
                "Foundation publication failed and full-directory recovery is incomplete; " +
                "the operation journal and any previous-candidate backup were preserved for the next call.",
                new AggregateException([publicationFailure, .. recoveryFailures]));
        }
    }

    private static bool RecoverOwnedOperations(
        string operationsDirectoryPath,
        string expectedOutputDirectoryPath)
    {
        if (!Directory.Exists(operationsDirectoryPath))
            return false;
        RejectReparsePoint(operationsDirectoryPath, "terrain add/remove operations directory");
        string[] entries = Directory.EnumerateFileSystemEntries(operationsDirectoryPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (entries.Length == 0)
        {
            TryDeleteEmptyDirectory(operationsDirectoryPath);
            return false;
        }

        List<RecoverableOperation> operations = new(entries.Length);
        foreach (string entry in entries)
        {
            operations.Add(InspectRecoverableOperation(
                entry,
                operationsDirectoryPath,
                expectedOutputDirectoryPath));
        }

        RejectUnexpectedOutputPathType(expectedOutputDirectoryPath);
        if (Directory.Exists(expectedOutputDirectoryPath))
        {
            RejectTreeReparsePoints(
                expectedOutputDirectoryPath,
                "stale foundation publication output");
        }

        RecoverValidatedOperationGroup(operations, expectedOutputDirectoryPath);
        TryDeleteEmptyDirectory(operationsDirectoryPath);
        return true;
    }

    private static RecoverableOperation InspectRecoverableOperation(
        string operationRoot,
        string operationsDirectoryPath,
        string expectedOutputDirectoryPath)
    {
        if (!Directory.Exists(operationRoot) || !IsOwnedOperationDirectory(operationRoot))
        {
            throw new InvalidDataException(
                $"The terrain add/remove operations directory contains a non-owned entry: {operationRoot}");
        }
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale terrain add/remove operation");
        RejectReparsePoint(operationRoot, "stale terrain add/remove operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale terrain add/remove operation lease");
            try
            {
                using FileStream lease = new(
                    leasePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException("A terrain add/remove candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned terrain add/remove operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale terrain add/remove operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A terrain add/remove operation journal is unreadable.");
        ValidateOperationJournal(
            journal,
            operationRoot,
            operationsDirectoryPath,
            expectedOutputDirectoryPath);

        RejectUnexpectedDirectoryRoleType(
            journal.StagedDirectoryPath,
            "stale foundation staged candidate");
        RejectUnexpectedDirectoryRoleType(
            journal.BackupDirectoryPath,
            "stale foundation previous-candidate backup");
        bool stagedExists = Directory.Exists(journal.StagedDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (stagedExists)
        {
            RejectTreeReparsePoints(
                journal.StagedDirectoryPath,
                "stale foundation staged candidate");
        }
        if (backupExists)
        {
            RejectTreeReparsePoints(
                journal.BackupDirectoryPath,
                "stale foundation previous-candidate backup");
        }

        return new RecoverableOperation(
            operationRoot,
            leasePath,
            journalPath,
            journal,
            stagedExists,
            backupExists);
    }

    private static void RecoverValidatedOperationGroup(
        IReadOnlyList<RecoverableOperation> operations,
        string expectedOutputDirectoryPath)
    {
        bool outputExists = Directory.Exists(expectedOutputDirectoryPath);
        RecoverableOperation[] backups = operations.Where(operation => operation.BackupExists).ToArray();
        RecoverableOperation[] committed = operations
            .Where(operation => operation.Journal.Phase == OperationPhaseCommitted)
            .ToArray();

        if (backups.Length > 1)
        {
            throw new IOException(
                "Multiple previous terrain add/remove candidate backups exist for the same publication target; " +
                "recovery was preserved for audit.");
        }
        if (committed.Length > 1)
        {
            throw new IOException(
                "Multiple committed terrain add/remove operations claim the same publication target; " +
                "recovery was preserved for audit.");
        }
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A terrain add/remove operation without a previous candidate owns an unexpected backup.");
            }
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp &&
                !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A terrain add/remove operation reports a previous-candidate backup without a previous candidate.");
            }
        }

        RecoverableOperation? backupToRestore = null;
        RecoverableOperation? committedBackupToDiscard = null;
        bool deleteOutput = false;

        if (committed.Length == 1)
        {
            RecoverableOperation committedOperation = committed[0];
            if (!outputExists)
            {
                throw new IOException(
                    "A committed terrain add/remove operation is missing its candidate directory; " +
                    "recovery was preserved for audit.");
            }
            if (backups.Length == 1 && !ReferenceEquals(backups[0], committedOperation))
            {
                throw new IOException(
                    "An uncommitted foundation backup competes with a committed publication; " +
                    "recovery was preserved for audit.");
            }
            committedBackupToDiscard = backups.SingleOrDefault();
        }
        else if (backups.Length == 1)
        {
            backupToRestore = backups[0];
            if (backupToRestore.Journal.Phase == OperationPhaseRollbackComplete)
            {
                throw new IOException(
                    "A rollback-complete terrain add/remove operation still owns a backup; " +
                    "recovery was preserved for audit.");
            }
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
            {
                throw new IOException(
                    "A previous terrain add/remove candidate is missing and no unique backup can restore it; " +
                    "recovery was preserved for audit.");
            }
        }
        else
        {
            bool previousCandidateIsAuthoritative = operations.Any(operation =>
                operation.Journal.HadPreviousCandidate &&
                (operation.Journal.Phase == OperationPhaseNewCandidateRemoved ||
                 operation.Journal.Phase == OperationPhaseRollbackComplete ||
                 (operation.Journal.Phase == OperationPhaseStaging && operation.StagedExists)));
            if (!previousCandidateIsAuthoritative)
            {
                if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
                {
                    throw new IOException(
                        "A stale terrain add/remove operation cannot prove whether the published directory is the " +
                        "previous candidate; recovery was preserved for audit.");
                }

                bool uncommittedCandidateIsAuthoritative = operations.Any(operation =>
                    operation.Journal.Phase == OperationPhaseCandidatePublished ||
                    (operation.Journal.Phase == OperationPhaseStaging && !operation.StagedExists));
                if (!uncommittedCandidateIsAuthoritative)
                {
                    throw new IOException(
                        "A no-previous foundation recovery cannot prove ownership of the published directory; " +
                        "recovery was preserved for audit.");
                }
                deleteOutput = true;
            }
        }

        // No filesystem mutation occurs above this line. Every operation, target, lease, tree,
        // journal, and competing backup has been validated as one exact-target recovery group.
        if (backupToRestore != null)
        {
            if (deleteOutput)
            {
                EnsureExpectedRecoveryOutputPath(
                    expectedOutputDirectoryPath,
                    backupToRestore.Journal.OutputDirectoryPath);
                Directory.Delete(expectedOutputDirectoryPath, recursive: true);
            }

            WriteRecoveredOperationPhase(backupToRestore, OperationPhaseNewCandidateRemoved);
            if (Directory.Exists(expectedOutputDirectoryPath))
                throw new IOException("The foundation destination is occupied before authoritative backup restoration.");
            Directory.Move(
                backupToRestore.Journal.BackupDirectoryPath,
                expectedOutputDirectoryPath);
        }
        else if (committedBackupToDiscard != null)
        {
            Directory.Delete(
                committedBackupToDiscard.Journal.BackupDirectoryPath,
                recursive: true);
        }
        else if (deleteOutput)
        {
            EnsureExpectedRecoveryOutputPath(
                expectedOutputDirectoryPath,
                operations[0].Journal.OutputDirectoryPath);
            Directory.Delete(expectedOutputDirectoryPath, recursive: true);
        }

        foreach (RecoverableOperation operation in operations)
            DeleteRecoveredOperation(operation);
    }

    private static void WriteRecoveredOperationPhase(
        RecoverableOperation operation,
        string phase)
    {
        CandidateOperationJournal recoveredJournal = operation.Journal with { Phase = phase };
        WriteTextDurably(
            operation.JournalPath,
            JsonSerializer.Serialize(recoveredJournal, JsonOptions) + "\n");
    }

    private static void DeleteRecoveredOperation(RecoverableOperation operation)
    {
        if (Directory.Exists(operation.Journal.BackupDirectoryPath))
        {
            throw new IOException(
                "A foundation recovery refused to discard an operation while its backup still exists.");
        }
        ValidateOperationRootEntries(operation.OperationRootPath);
        Directory.Delete(operation.OperationRootPath, recursive: true);
    }

    private static void ValidateOperationRootEntries(string operationRoot)
    {
        HashSet<string> allowedNames = new(StringComparer.Ordinal)
        {
            "candidate",
            "previous-candidate",
            "operation.lease",
            OperationJournalFileName
        };
        foreach (string entry in Directory.EnumerateFileSystemEntries(operationRoot))
        {
            RejectReparsePoint(entry, "stale terrain add/remove operation entry");
            string name = Path.GetFileName(entry);
            if (!allowedNames.Contains(name))
            {
                throw new InvalidDataException(
                    $"A stale terrain add/remove operation contains an unowned entry: {entry}");
            }
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
            {
                throw new InvalidDataException(
                    $"A stale terrain add/remove operation entry has the wrong filesystem role: {entry}");
            }
        }
    }

    private static void RejectTreeReparsePoints(string root, string label)
    {
        RejectReparsePoint(root, label);
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectReparsePoint(entry, label);
                if (Directory.Exists(entry))
                    pending.Push(entry);
            }
        }
    }

    private static void RejectUnexpectedDirectoryRoleType(string path, string label)
    {
        if (File.Exists(path))
            throw new InvalidDataException($"The {label} is a file instead of an owned directory.");
    }

    private static void RejectUnexpectedOutputPathType(string outputDirectoryPath)
    {
        string parent = Path.GetDirectoryName(outputDirectoryPath) ??
            throw new InvalidOperationException("The foundation publication output has no parent.");
        if (!Directory.Exists(parent))
            return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, outputDirectoryPath))
                continue;
            RejectReparsePoint(entry, "foundation publication output path");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    "The foundation publication output path is not a directory.");
            }
            return;
        }
    }

    private static bool IsOwnedOperationDirectory(string operationRoot)
    {
        string name = Path.GetFileName(operationRoot);
        if (!name.StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            return false;
        string operationId = name[OperationDirectoryPrefix.Length..];
        return Guid.TryParseExact(operationId, "N", out _);
    }

    private static void EnsureExpectedRecoveryOutputPath(string actual, string expected)
    {
        if (!PathEquals(actual, expected))
            throw new InvalidOperationException("A foundation recovery target escaped the exact requested output path.");
    }

    private static void WriteOperationJournal(CandidatePublication publication, string phase)
    {
        CandidateOperationJournal journal = new(
            OperationJournalSchemaVersion,
            OperationKind,
            phase,
            publication.OperationRootPath,
            publication.OutputDirectoryPath,
            publication.StagedDirectoryPath,
            publication.BackupDirectoryPath,
            publication.HadPreviousCandidate);
        string path = Path.Combine(publication.OperationRootPath, OperationJournalFileName);
        WriteTextDurably(path, JsonSerializer.Serialize(journal, JsonOptions) + "\n");
    }

    private static void ValidateOperationJournal(
        CandidateOperationJournal journal,
        string operationRoot,
        string operationsDirectoryPath,
        string expectedOutputDirectoryPath)
    {
        string[] phases =
        [
            OperationPhaseStaging,
            OperationPhasePreviousBackedUp,
            OperationPhaseCandidatePublished,
            OperationPhaseNewCandidateRemoved,
            OperationPhaseRollbackComplete,
            OperationPhaseCommitted
        ];
        if (journal.SchemaVersion != OperationJournalSchemaVersion ||
            journal.OperationKind != OperationKind ||
            !phases.Contains(journal.Phase, StringComparer.Ordinal) ||
            !PathEquals(journal.OperationRootPath, operationRoot) ||
            !PathEquals(journal.OutputDirectoryPath, expectedOutputDirectoryPath) ||
            !PathEquals(journal.StagedDirectoryPath, Path.Combine(operationRoot, "candidate")) ||
            !PathEquals(journal.BackupDirectoryPath, Path.Combine(operationRoot, "previous-candidate")))
        {
            throw new InvalidDataException("A stale terrain add/remove operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale foundation journal points outside its publication parent.");
    }

    private static void VerifyPublishedCandidate(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published terrain add/remove candidate directory is missing.");
        VerifyStagedCandidateArtifacts(
            paths.OutputDirectoryPath,
            paths,
            staged.FinderReveal != null);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        RequireHash(HashFile(paths.OutputImagePath), staged.Readback.OutputImageSha256, "published foundation BIN");
        RequireHash(HashFile(paths.OutputCuePath), staged.Receipt.OutputCueSha256, "published CUE sidecar");
        RequireHash(
            HashFile(paths.FoundationCompositionPlanPath),
            staged.Receipt.TerrainPlanSha256,
            "published terrain-plan sidecar");
        RequireHash(
            HashFile(paths.RuntimeChecklistPath),
            staged.Receipt.RuntimeChecklistSha256,
            "published checklist sidecar");
        RequireHash(
            HashFile(paths.LocationGuidePath),
            staged.Receipt.LocationGuideSha256,
            "published guide sidecar");
        VerifyTextReadback(
            paths.FoundationCompositionPlanPath,
            JsonSerializer.Serialize(staged.Plan, JsonOptions) + "\n",
            "published composition plan");
        VerifyTextReadback(
            paths.StaticReadbackReceiptPath,
            JsonSerializer.Serialize(staged.Receipt, JsonOptions) + "\n",
            "published static receipt");
        string checklist = File.ReadAllText(paths.RuntimeChecklistPath);
        RuntimeCandidateFinderReveal? finalReveal = staged.FinderReveal == null
            ? null
            : BuildFinalFinderReveal(paths);
        VerifyChecklist(checklist, finalReveal, staged.Plan.LoadCodes, paths);
        VerifyLocationGuide(File.ReadAllText(paths.LocationGuidePath), staged.Readback.OutputImageSha256);
        if (finalReveal != null)
        {
            if (!OperatingSystem.IsMacOS() ||
                !File.Exists(paths.FinderHelperPath) ||
                File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
            {
                throw new InvalidDataException("The published foundation Finder helper is not exact 0755.");
            }
            RequireHash(
                HashFile(paths.FinderHelperPath),
                staged.Receipt.FinderHelperSha256 ?? "",
                "published Finder-helper sidecar");
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(
                checklist,
                finalReveal,
                staged.Plan.LoadCodes);
        }
        else if (File.Exists(paths.FinderHelperPath))
        {
            throw new InvalidDataException("A foundation Finder helper was published while handoff was disabled.");
        }
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static void PrepareDestination(string outputDirectoryPath, bool replaceExistingCandidate)
    {
        if (!Directory.Exists(outputDirectoryPath))
            return;
        RejectReparsePoint(outputDirectoryPath, "foundation output directory");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The terrain add/remove candidate directory already exists. " +
                "Set ReplaceExistingCandidate only for an intentional atomic replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
    {
        if (IsDescendantOrEqual(baseImage, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseImage) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseCue))
        {
            throw new InvalidOperationException(
                "The foundation output directory and locked base files must have distinct roles.");
        }
        if (PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The foundation output and operations directories overlap.");
    }

    private static void RequirePathsInsideOutput(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
    {
        string[] artifacts =
        [
            paths.OutputPrefix,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.FoundationCompositionPlanPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath
        ];
        foreach (string artifact in artifacts)
            EnsureStrictDescendant(artifact, paths.OutputDirectoryPath, "foundation artifact");
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent))
            throw new InvalidOperationException("The terrain add/remove operations directory escaped the output parent.");
    }

    private static FileStream CreateOperationLease(string operationRoot)
    {
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        return new FileStream(
            leasePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    private static GlobalWriterLease AcquireGlobalWriterLease(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The terrain add/remove operations directory has no parent.");
        Directory.CreateDirectory(parent);
        string leasePath = Path.Combine(parent, GlobalWriterLeaseFileName);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "foundation global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The terrain add/remove writer lease path is a directory.");
            break;
        }

        FileStream lease;
        try
        {
            lease = new FileStream(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException ex) when (
            !OperatingSystem.IsWindows() &&
            IsUnixWouldBlock(ex.HResult & 0xFFFF))
        {
            throw CreateActiveWriterLeaseException(ex);
        }
        catch (IOException ex)
        {
            throw new IOException(
                "The foundation global writer lease file could not be opened.",
                ex);
        }

        try
        {
            RejectReparsePoint(leasePath, "foundation global writer lease");
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    lease.Lock(0, 1);
                }
                catch (IOException ex) when (IsWindowsLockContention(ex))
                {
                    throw CreateActiveWriterLeaseException(ex);
                }
                catch (IOException ex)
                {
                    throw new IOException(
                        "The foundation global writer OS range lease could not be acquired.",
                        ex);
                }
                return new GlobalWriterLease(lease);
            }

            int fileDescriptor = checked((int)lease.SafeFileHandle.DangerousGetHandle());
            if (NativeFlock(fileDescriptor, LockExclusive | LockNonBlocking) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                Win32Exception nativeError = new(error);
                if (IsUnixWouldBlock(error))
                    throw CreateActiveWriterLeaseException(nativeError);
                throw new IOException(
                    $"The foundation global writer OS lease failed (errno {error}: {nativeError.Message}).",
                    nativeError);
            }
            return new GlobalWriterLease(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static IOException CreateActiveWriterLeaseException(Exception innerException) =>
        new(ActiveWriterLeaseMessage, innerException);

    private static bool IsUnixWouldBlock(int error)
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            return error == 35;
        if (OperatingSystem.IsLinux())
            return error == 11;
        return error is 11 or 35;
    }

    private static bool IsWindowsLockContention(IOException exception) =>
        (exception.HResult & 0xFFFF) == WindowsErrorLockViolation;

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int NativeFlock(int fileDescriptor, int operation);

    private static async Task<T> ReadCanonicalJsonAsync<T>(
        string path,
        string label,
        CancellationToken cancellationToken)
        where T : class
    {
        string exactPath = RequireExistingFile(path, label);
        string text = await File.ReadAllTextAsync(exactPath, cancellationToken);
        T value;
        try
        {
            value = JsonSerializer.Deserialize<T>(text, JsonOptions) ??
                throw new InvalidDataException($"The {label} deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The {label} is not valid exact JSON.", ex);
        }

        string canonical = JsonSerializer.Serialize(value, JsonOptions) + "\n";
        if (!string.Equals(text, canonical, StringComparison.Ordinal))
            throw new InvalidDataException($"The {label} is not in its exact canonical form.");
        return value;
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
        await WriteTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

    private static async Task WriteTextAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, content, encoding, cancellationToken);
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteTextDurably(string path, string content)
    {
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(
                temporary,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            using (FileStream stream = new(
                       temporary,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void VerifyTextReadback(string path, string expected, string label)
    {
        string actual = File.ReadAllText(path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The {label} failed exact deterministic text readback.");
    }

    private static string StagedPath(string stagedDirectory, string finalPath) =>
        Path.Combine(stagedDirectory, Path.GetFileName(finalPath));

    private static string RequireExistingFile(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} is missing.", fullPath);
        RejectReparsePoint(fullPath, label);
        return fullPath;
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireOptionalPin(string actual, string expected, string label)
    {
        if (!string.IsNullOrEmpty(expected))
            RequireHash(actual, expected, label);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static UnixFileMode ExactFinderMode() =>
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute |
        UnixFileMode.GroupRead |
        UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead |
        UnixFileMode.OtherExecute;

    private static string ShellSingleQuote(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static void EnsureExactPublicationPath(
        string path,
        CandidatePublication publication)
    {
        if (!PathEquals(path, publication.OutputDirectoryPath))
            throw new InvalidOperationException("A foundation rollback target escaped its exact publication path.");
    }

    private static void EnsureJournalPublicationPath(
        string path,
        CandidateOperationJournal journal)
    {
        if (!PathEquals(path, journal.OutputDirectoryPath))
            throw new InvalidOperationException("A stale foundation rollback target escaped its journal path.");
    }

    private static void TryDeleteOwnedOperation(string operationRoot, string operationsDirectoryPath)
    {
        if (!Directory.Exists(operationRoot))
            return;
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed terrain add/remove operation");
        if (!Path.GetFileName(operationRoot).StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-owned terrain add/remove operation directory.");
        Directory.Delete(operationRoot, recursive: true);
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    private static void RejectReparsePoint(string path, string label)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"The {label} cannot be a symbolic link or reparse point.");
    }

    private static void EnsureStrictDescendant(string candidate, string parent, string label)
    {
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        string prefix = fullParent + Path.DirectorySeparatorChar;
        if (!fullCandidate.StartsWith(prefix, PathComparison))
            throw new InvalidOperationException($"The {label} escaped its owned parent directory.");
    }

    private static bool IsDescendantOrEqual(string candidate, string parent)
    {
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return string.Equals(fullCandidate, fullParent, PathComparison) ||
               fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, PathComparison);
    }

    private static bool PathEquals(string? left, string? right) =>
        left != null && right != null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed class CandidatePublication(
        string operationRootPath,
        string outputDirectoryPath,
        string stagedDirectoryPath,
        string backupDirectoryPath,
        bool hadPreviousCandidate)
    {
        public string OperationRootPath { get; } = operationRootPath;
        public string OutputDirectoryPath { get; } = outputDirectoryPath;
        public string StagedDirectoryPath { get; } = stagedDirectoryPath;
        public string BackupDirectoryPath { get; } = backupDirectoryPath;
        public bool HadPreviousCandidate { get; } = hadPreviousCandidate;
        public bool PreviousBackedUp { get; set; }
        public bool CandidatePublished { get; set; }
        public bool RecoveryComplete { get; set; } = true;
        public bool Committed { get; set; }
    }

    private sealed record CandidateOperationJournal(
        int SchemaVersion,
        string OperationKind,
        string Phase,
        string OperationRootPath,
        string OutputDirectoryPath,
        string StagedDirectoryPath,
        string BackupDirectoryPath,
        bool HadPreviousCandidate);

    private sealed record RecoverableOperation(
        string OperationRootPath,
        string LeasePath,
        string JournalPath,
        CandidateOperationJournal Journal,
        bool StagedExists,
        bool BackupExists);

    private sealed class GlobalWriterLease(FileStream stream) : IDisposable
    {
        private FileStream? _stream = stream;

        public void Dispose()
        {
            FileStream? ownedStream = Interlocked.Exchange(ref _stream, null);
            if (ownedStream == null)
                return;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    ownedStream.Unlock(0, 1);
                }
                else
                {
                    int fileDescriptor = checked((int)ownedStream.SafeFileHandle.DangerousGetHandle());
                    _ = NativeFlock(fileDescriptor, LockUnlock);
                }
            }
            catch (IOException)
            {
                // Closing the underlying descriptor below releases either lock even if an
                // explicit unlock races with process shutdown or external filesystem failure.
            }
            finally
            {
                ownedStream.Dispose();
            }
        }
    }

    private sealed record StagedCandidate(
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePlan Plan,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateReceipt Receipt,
        RuntimeCandidateFinderReveal? FinderReveal,
        FoundationReadback Readback,
        int RebuiltRawSectorCount);

    private sealed record FoundationReadback(
        string OutputImageSha256,
        string OutputDataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> RawSectorDiffs);

    private sealed record LogicalDiff(
        long ChangedBytes,
        long OutsideModelBytes,
        IReadOnlyList<int> ChangedRawSectorLbas);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
