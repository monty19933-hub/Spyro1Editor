using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    bool RequestFinderReveal = true,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths(
    string OutputDirectoryPath,
    string OutputPrefix,
    string OutputImagePath,
    string OutputCuePath,
    string ConstructionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string OperationsDirectoryPath);

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string ConstructionProfileId,
    string BaseImageSha256,
    string SourceDataSha256,
    string OutputDataSha256,
    string SourceModelSha256,
    string OutputModelSha256,
    string OutputCollisionTreeSha256,
    string OutputCollisionBlocksSha256,
    string StaticDiffManifestSha256,
    string DeterministicStaticPlanSha256,
    int WadLba,
    long DataWadOffset,
    int DataByteLength,
    long ModelSubfileWadOffset,
    int ModelSubfileByteLength,
    int SourceSectorCount,
    int OutputSectorCount,
    int NewSectorIndex,
    int EnvironmentGrowthBytes,
    int NewSectorByteLength,
    int MaterialTextureId,
    int OcclusionGroupCount,
    int OcclusionAssignment,
    int SourceOcclusionGroupZeroCount,
    int OutputOcclusionGroupZeroCount,
    int ReusedCollisionTriangleIndex,
    int SourceCollisionAssignment,
    int OutputCollisionAssignment,
    UnusedLevel65RemoteBlankCollisionCell TargetCollisionCell,
    int CollisionBlocksCapacityBytes,
    int OutputCollisionBlocksUsedBytes,
    int LandingRawX,
    int LandingRawY,
    int LandingRawZ,
    int PlayerRawX,
    int PlayerRawY,
    int PlayerRawZ,
    int InheritedSectorCount,
    int InheritedMobyCount,
    int StaticChangedDataByteCount,
    int StaticDiffRangeCount,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool Sector216ExactHpLpPairingVerified,
    bool FixedOcclusionOwnershipVerified,
    bool Triangle13995OrderedRepackVerified,
    bool CoupledLandingAndPlayerAnchorVerified,
    bool InheritedScaffoldingPreserved,
    bool BlankLookingNotByteEmpty,
    bool FarLowDetailRuntimePending,
    bool DeathPlaneRuntimePending,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string ConstructionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string? FinderHelperPath,
    string OutputCueSha256,
    string ConstructionPlanSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string? FinderHelperSha256,
    string BaseCueSha256,
    string BaseImageSha256,
    string OutputImageSha256,
    string OutputDataSha256,
    string OutputModelSha256,
    string OutputCollisionTreeSha256,
    string OutputCollisionBlocksSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool StaticPlanPinsVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool Sector216ExactHpLpPairingVerified,
    bool FixedOcclusionOwnershipVerified,
    bool Triangle13995OrderedRepackVerified,
    bool ProtectedSubfilesPreserved,
    bool CoupledLandingAndPlayerAnchorVerified,
    bool InheritedScaffoldingPreserved,
    bool ExecutablePreserved,
    bool RetailControlLevelsPreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65RemoteBlankIsolationRuntimeCandidateResult(
    UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths Paths,
    UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan Plan,
    UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal? FinderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    string OutputImageSha256,
    string OutputDataSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool StaticPlanPinsVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool Sector216ExactHpLpPairingVerified,
    bool FixedOcclusionOwnershipVerified,
    bool Triangle13995OrderedRepackVerified,
    bool ProtectedSubfilesPreserved,
    bool CoupledLandingAndPlayerAnchorVerified,
    bool InheritedScaffoldingPreserved,
    bool ExecutablePreserved,
    bool RetailControlLevelsPreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal static class UnusedLevel65RemoteBlankIsolationRuntimeCandidateExporter
{
    public static bool Retired => true;
    public static string RetirementReason =>
        "RETIRED: exact remote-blank v1 BIN 8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e failed to establish a landing and reached GAME OVER; preserve it only as rejected evidence and do not publish, load, or retest it. Use the isolated collision-winding-repair v2 discriminator.";

    public const string ProfileId =
        "unused-level-65-remote-blank-isolation-sector216-clean-usa-disposable-v1";
    public const string OutputDirectoryName =
        "unused-level-65-remote-blank-isolation";
    public const string OutputPrefix =
        "Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 2;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-remote-blank-isolation-runtime-candidate";
    private const string OperationDirectoryPrefix = "remote-blank-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-remote-blank-isolation-writer.lease";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const string ActiveWriterLeaseMessage =
        "Another remote-blank runtime candidate writer is active for this publication parent. " +
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
    private const int WadLba = UnusedLevel65RemoteBlankIsolationConstruction.WadLba;
    private const int WadByteLength = UnusedLevel65RemoteBlankIsolationConstruction.WadByteLength;
    private const long DataWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.DataWadOffset;
    private const int DataByteLength = UnusedLevel65RemoteBlankIsolationConstruction.DataByteLength;
    private const long ModelWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.ModelWadOffset;
    private const int ModelByteLength = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength;
    private const string BaseImageSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceImageSha256;
    private const string SourceDataSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceDataSha256;
    private const string SourceModelSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceModelSha256;
    private const string OutputModelSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputModelSha256;
    private const string OutputCollisionTreeSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputTreeSha256;
    private const string OutputCollisionBlocksSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256;
    private const string ExpectedOutputImageSha256 =
        "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e";
    private const string ExpectedOutputDataSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256;
    private const long ExpectedChangedLogicalWadBytes = 484_336;
    private const long ExpectedChangedPhysicalImageBytes = 564_801;
    private const int ExpectedChangedRawSectorCount = 291;
    public const string ExpectedRawSectorDiffSha256 =
        "235a4a68367e684c4306feeb2061b343bd9581015c5c3204577b0b8fc80a58c3";
    private const int CollisionTreeInComponentOffset = 4 + 0x1C;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksInComponentOffset = 4 + 0x6A7C;
    private const int CollisionBlocksByteLength = 0x17984;
    private static readonly int[] ExpectedChangedRawSectorLbas =
    [
        .. Enumerable.Range(54_356, 267),
        54_624,
        54_625,
        .. Enumerable.Range(54_628, 20),
        54_834,
        54_838
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UnusedLevel65RemoteBlankIsolationRuntimeCandidateResult> CreateAsync(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Retired)
            throw new InvalidOperationException(RetirementReason);

        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "exact locked ID65 display-name BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "exact locked ID65 display-name CUE");
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths = CreatePaths(
            request.OutputDirectoryPath);
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
            "locked ID65 display-name BIN");

        UnusedLevel65RemoteBlankStaticPlan construction =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(baseImage);
        UnusedLevel65RemoteBlankStaticPlan constructionRepeated =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(baseImage);
        ValidateConstructionGate(construction);
        ValidateConstructionGate(constructionRepeated);
        ValidateConstructionDeterminism(construction, constructionRepeated);
        if (!IsExactDisposableRuntimeCandidateAuthorized(construction))
        {
            throw new InvalidDataException(
                "The writer-local disposable remote-blank runtime authorization predicate failed.");
        }
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "remote-blank operation");
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

        UnusedLevel65RemoteBlankIsolationRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                baseImage,
                baseCue,
                paths,
                stagedDirectory,
                construction,
                loadCodes,
                request.RequestFinderReveal,
                rollbackRecoveryVerified,
                cancellationToken);
            request.TestStageHook?.Invoke("after-staged-candidate-verified");
            cancellationToken.ThrowIfCancellationRequested();
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);

            RequireHash(
                await HashFileAsync(baseImage, cancellationToken),
                BaseImageSha256,
                "locked ID65 display-name BIN after publication");
            WriteOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            RuntimeCandidateFinderReveal? finalReveal = staged.FinderReveal == null
                ? null
                : BuildFinalFinderReveal(paths);
            UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt receipt =
                staged.Receipt;
            result = new(
                paths,
                staged.Plan,
                receipt,
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
                StaticPlanPinsVerified: true,
                ModelReadbackVerified: true,
                CollisionReadbackVerified: true,
                Sector216ExactHpLpPairingVerified: true,
                FixedOcclusionOwnershipVerified: true,
                Triangle13995OrderedRepackVerified: true,
                ProtectedSubfilesPreserved: true,
                CoupledLandingAndPlayerAnchorVerified: true,
                InheritedScaffoldingPreserved: true,
                ExecutablePreserved: true,
                RetailControlLevelsPreserved: true,
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
                if (!publication.PreviousBackedUp && !publication.CandidatePublished)
                {
                    // The authoritative output has not moved. Staging failures and cancellation
                    // need only discard this owned operation; no previous backup exists to restore.
                    publication.RecoveryComplete = true;
                    recoveryComplete = true;
                }
                else
                {
                    recoveryComplete = false;
                    RollBackPublication(publication, publicationFailure, request.TestStageHook);
                    recoveryComplete = publication.RecoveryComplete;
                }
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
            throw new IOException("The completed remote-blank operation directory could not be removed.");
        return result ?? throw new InvalidOperationException(
            "The remote-blank runtime writer completed without a result.");
    }

    internal static async Task<UnusedLevel65RemoteBlankIsolationRuntimeCandidateResult> VerifyPublishedAsync(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        cancellationToken.ThrowIfCancellationRequested();
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths exactPaths =
            CreatePaths(paths.OutputDirectoryPath);
        if (paths != exactPaths)
            throw new InvalidDataException("The remote-blank verification paths are not the exact owned final paths.");
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new DirectoryNotFoundException("The published remote-blank candidate directory is missing.");
        RejectReparsePoint(paths.OutputDirectoryPath, "published remote-blank candidate directory");

        UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt receipt =
            await ReadCanonicalJsonAsync<UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt>(
                paths.StaticReadbackReceiptPath,
                "published remote-blank receipt",
                cancellationToken);
        bool includeFinderReveal = receipt.FinderHandoffVerified;
        ValidatePublishedReceiptIdentity(receipt, paths, includeFinderReveal);
        VerifyStagedCandidateArtifacts(paths.OutputDirectoryPath, paths, includeFinderReveal);
        foreach (string artifact in EnumeratePublishedArtifactPaths(paths, includeFinderReveal))
            _ = RequireExistingFile(artifact, "published remote-blank artifact");

        RequireHash(
            await HashFileAsync(paths.OutputCuePath, cancellationToken),
            receipt.OutputCueSha256,
            "published remote-blank CUE");
        RequireHash(
            await HashFileAsync(paths.ConstructionPlanPath, cancellationToken),
            receipt.ConstructionPlanSha256,
            "published remote-blank construction plan");
        RequireHash(
            await HashFileAsync(paths.RuntimeChecklistPath, cancellationToken),
            receipt.RuntimeChecklistSha256,
            "published remote-blank runtime checklist");
        RequireHash(
            await HashFileAsync(paths.LocationGuidePath, cancellationToken),
            receipt.LocationGuideSha256,
            "published remote-blank location guide");
        if (includeFinderReveal)
        {
            RequireHash(
                await HashFileAsync(paths.FinderHelperPath, cancellationToken),
                receipt.FinderHelperSha256!,
                "published remote-blank Finder helper");
        }

        string baseImage = RequireExistingFile(receipt.BaseImagePath, "receipt-bound display-name base BIN");
        string baseCue = RequireExistingFile(receipt.BaseCuePath, "receipt-bound display-name base CUE");
        RequireSafeRoles(baseImage, baseCue, paths);
        RequireHash(
            await HashFileAsync(baseImage, cancellationToken),
            BaseImageSha256,
            "receipt-bound display-name base BIN");
        RequireHash(
            await HashFileAsync(baseCue, cancellationToken),
            receipt.BaseCueSha256,
            "receipt-bound display-name base CUE");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        VerifyTextReadback(
            paths.OutputCuePath,
            DiscImage.BuildCueText(baseCue, Path.GetFileName(paths.OutputImagePath)),
            "published remote-blank CUE");

        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan plan =
            await ReadCanonicalJsonAsync<UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan>(
                paths.ConstructionPlanPath,
                "published remote-blank construction plan",
                cancellationToken);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        if (!plan.LoadCodes.SequenceEqual(loadCodes))
            throw new InvalidDataException("The published remote-blank plan lost the exact four load codes.");

        UnusedLevel65RemoteBlankStaticPlan construction =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(baseImage);
        ValidateConstructionGate(construction);
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImage);
        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            paths.OutputImagePath,
            baseLayout,
            construction,
            ExpectedChangedRawSectorLbas,
            cancellationToken);
        ValidatePublishedReadback(receipt, readback);

        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan expectedPlan =
            BuildPublishedPlan(construction, readback, loadCodes);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            JsonSerializer.Serialize(expectedPlan, JsonOptions) + "\n",
            "published remote-blank construction plan pins");

        RuntimeCandidateFinderReveal? finderReveal = includeFinderReveal
            ? BuildFinalFinderReveal(paths)
            : null;
        if (finderReveal != null)
        {
            if (!OperatingSystem.IsMacOS() ||
                File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
            {
                throw new InvalidDataException("The published remote-blank Finder helper is not exact 0755.");
            }
            VerifyTextReadback(
                paths.FinderHelperPath,
                BuildExpectedFinderHelper(paths.OutputCuePath),
                "published remote-blank Finder helper content");
        }

        string checklist = await File.ReadAllTextAsync(paths.RuntimeChecklistPath, cancellationToken);
        string expectedChecklist = BuildRuntimeChecklist(paths, finderReveal, loadCodes, readback);
        if (!string.Equals(checklist, expectedChecklist, StringComparison.Ordinal))
            throw new InvalidDataException("The published remote-blank runtime checklist is not exact.");
        VerifyChecklist(checklist, finderReveal, loadCodes, paths);
        string locationGuide = await File.ReadAllTextAsync(paths.LocationGuidePath, cancellationToken);
        if (!string.Equals(locationGuide, BuildLocationGuideSvg(loadCodes), StringComparison.Ordinal))
            throw new InvalidDataException("The published remote-blank location guide is not exact.");
        VerifyLocationGuide(locationGuide);

        return new(
            paths,
            plan,
            receipt,
            finderReveal,
            loadCodes,
            readback.OutputImageSha256,
            readback.OutputDataSha256,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            receipt.RebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs,
            HashRawSectorDiffs(readback.RawSectorDiffs),
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            StaticPlanPinsVerified: true,
            ModelReadbackVerified: true,
            CollisionReadbackVerified: true,
            Sector216ExactHpLpPairingVerified: true,
            FixedOcclusionOwnershipVerified: true,
            Triangle13995OrderedRepackVerified: true,
            ProtectedSubfilesPreserved: true,
            CoupledLandingAndPlayerAnchorVerified: true,
            InheritedScaffoldingPreserved: true,
            ExecutablePreserved: true,
            RetailControlLevelsPreserved: true,
            RawSectorIntegrityVerified: true,
            BaseCandidatePreserved: true,
            FullDirectoryPublicationVerified: true,
            DisposableRuntimeCandidateAuthorized: true,
            RollbackRecoveryVerified: receipt.RollbackRecoveryVerified,
            FullDirectoryRollbackVerified: receipt.FullDirectoryRollbackVerified,
            FinderHandoffVerified: includeFinderReveal,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    public static UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string root = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, root) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException("The remote-blank candidate output cannot be a filesystem or home root.");
        }
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The remote-blank candidate output has no parent directory.");
        if (!string.Equals(Path.GetFileName(outputDirectory), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The remote-blank writer owns only a directory named '{OutputDirectoryName}'.");
        }
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths result = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-construction-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-remote-blank-isolation-operations"));
        RequirePathsInsideOutput(result);
        return result;
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string baseImage,
        string baseCue,
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        UnusedLevel65RemoteBlankStaticPlan construction,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        bool requestFinderReveal,
        bool rollbackRecoveryVerified,
        CancellationToken cancellationToken)
    {
        string stagedImage = StagedPath(stagedDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stagedDirectory, finalPaths.OutputCuePath);
        string stagedPlan = StagedPath(stagedDirectory, finalPaths.ConstructionPlanPath);
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
            throw new InvalidDataException("The staged remote-blank image is not MODE2/2352 with user offset 24.");

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
                throw new InvalidDataException("The staged remote-blank WAD extent changed.");
            byte[] dataPreimage = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                DataWadOffset,
                DataByteLength);
            if (!dataPreimage.SequenceEqual(construction.SourceData))
                throw new InvalidDataException("The staged row-80 preimage changed after static-plan construction.");
            RequireHash(Hash(dataPreimage), SourceDataSha256, "staged row-80 preimage");
            IReadOnlyList<(long Offset, int ByteLength)> changedDataRuns =
                BuildChangedLogicalRuns(dataPreimage, construction.OutputData, DataWadOffset);
            if (changedDataRuns.Count == 0)
                throw new InvalidDataException("The remote-blank construction produced no logical row-80 changes.");
            rebuiltRawSectorLbas = RawSectorLbasForFileRanges(WadLba, changedDataRuns);
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                DataWadOffset,
                construction.OutputData);
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                WadLba,
                changedDataRuns);
            int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                CoalesceRawSectorRanges(rebuiltRawSectorLbas));
            if (rebuiltRawSectorCount != rebuiltRawSectorLbas.Count ||
                verifiedRawSectors != rebuiltRawSectorLbas.Count)
            {
                throw new InvalidDataException(
                    $"The diff-derived row-80 write rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectors} sectors, " +
                    $"expected {rebuiltRawSectorLbas.Count}.");
            }
            await image.FlushAsync(cancellationToken);
            image.Flush(flushToDisk: true);
        }

        string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(stagedImage));
        await WriteTextAsync(stagedCue, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");
        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            stagedImage,
            layout,
            construction,
            rebuiltRawSectorLbas,
            cancellationToken);
        RequireOptionalPin(readback.OutputImageSha256, ExpectedOutputImageSha256, "remote-blank output BIN");
        RequireOptionalPin(readback.OutputDataSha256, ExpectedOutputDataSha256, "remote-blank output ID65 data");
        if ((ExpectedChangedLogicalWadBytes >= 0 &&
             readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes) ||
            (ExpectedChangedPhysicalImageBytes >= 0 &&
             readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes) ||
            (ExpectedChangedRawSectorCount >= 0 &&
             readback.ChangedRawSectorCount != ExpectedChangedRawSectorCount) ||
            (ExpectedChangedRawSectorLbas.Length > 0 &&
             !readback.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                .SequenceEqual(ExpectedChangedRawSectorLbas)) ||
            (!IsPendingHash(ExpectedRawSectorDiffSha256) &&
             HashRawSectorDiffs(readback.RawSectorDiffs) != ExpectedRawSectorDiffSha256))
        {
            throw new InvalidDataException(
                $"The pinned remote-blank diff changed: logical={readback.ChangedLogicalWadBytes}, " +
                $"physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
        }

        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan plan =
            BuildPublishedPlan(construction, readback, loadCodes);
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
        string locationGuide = BuildLocationGuideSvg(loadCodes);
        await WriteTextAsync(
            stagedGuide,
            locationGuide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        string outputCueSha256 = await HashFileAsync(stagedCue, cancellationToken);
        string constructionPlanSha256 = await HashFileAsync(stagedPlan, cancellationToken);
        string runtimeChecklistSha256 = await HashFileAsync(stagedChecklist, cancellationToken);
        string locationGuideSha256 = await HashFileAsync(stagedGuide, cancellationToken);
        string? finderHelperSha256 = stagedReveal == null
            ? null
            : await HashFileAsync(stagedReveal.HelperPath, cancellationToken);
        string baseCueSha256 = await HashFileAsync(baseCue, cancellationToken);

        UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            baseImage,
            baseCue,
            finalPaths.OutputDirectoryPath,
            finalPaths.OutputImagePath,
            finalPaths.OutputCuePath,
            finalPaths.ConstructionPlanPath,
            finalPaths.StaticReadbackReceiptPath,
            finalPaths.RuntimeChecklistPath,
            finalPaths.LocationGuidePath,
            finalReveal?.HelperPath,
            outputCueSha256,
            constructionPlanSha256,
            runtimeChecklistSha256,
            locationGuideSha256,
            finderHelperSha256,
            baseCueSha256,
            BaseImageSha256,
            readback.OutputImageSha256,
            readback.OutputDataSha256,
            OutputModelSha256,
            OutputCollisionTreeSha256,
            OutputCollisionBlocksSha256,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs,
            HashRawSectorDiffs(readback.RawSectorDiffs),
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            StaticPlanPinsVerified: true,
            ModelReadbackVerified: true,
            CollisionReadbackVerified: true,
            Sector216ExactHpLpPairingVerified: true,
            FixedOcclusionOwnershipVerified: true,
            Triangle13995OrderedRepackVerified: true,
            ProtectedSubfilesPreserved: true,
            CoupledLandingAndPlayerAnchorVerified: true,
            InheritedScaffoldingPreserved: true,
            ExecutablePreserved: true,
            RetailControlLevelsPreserved: true,
            RawSectorIntegrityVerified: true,
            BaseCandidatePreserved: true,
            FullDirectoryPublicationVerified: true,
            DisposableRuntimeCandidateAuthorized: true,
            RollbackRecoveryVerified: rollbackRecoveryVerified,
            FullDirectoryRollbackVerified: rollbackRecoveryVerified,
            FinderHandoffVerified: finalReveal != null,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);
        VerifyStagedCandidateArtifacts(stagedDirectory, finalPaths, includeFinderReveal);
        VerifyTextReadback(stagedPlan, JsonSerializer.Serialize(plan, JsonOptions) + "\n", "composition plan");
        VerifyTextReadback(stagedReceipt, JsonSerializer.Serialize(receipt, JsonOptions) + "\n", "static receipt");
        VerifyChecklist(checklist, finalReveal, loadCodes, finalPaths);
        VerifyLocationGuide(locationGuide);
        return new(
            plan,
            receipt,
            stagedReveal,
            readback,
            rebuiltRawSectorCount);
    }

    private static async Task<CandidateReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        DiscLayout expectedLayout,
        UnusedLevel65RemoteBlankStaticPlan construction,
        IReadOnlyList<int> rebuiltRawSectorLbas,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (outputLayout != expectedLayout ||
            outputLayout.SectorSize != RawSectorByteLength ||
            outputLayout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException("The remote-blank output changed the exact MODE2/2352 disc layout.");
        }

        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The remote-blank output changed the disc-image length.");
        DiscFileRecord baseWad = DiscImage.FindRootFileRecord(
            baseline,
            expectedLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (baseWad != outputWad || outputWad.Lba != WadLba || outputWad.Size != WadByteLength)
            throw new InvalidDataException("The remote-blank output moved or resized WAD.WAD.");

        byte[] outputData = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            DataWadOffset,
            DataByteLength);
        if (!outputData.SequenceEqual(construction.OutputData))
            throw new InvalidDataException("The remote-blank row-80 data failed exact static-plan readback.");
        RequireHash(Hash(outputData), ExpectedOutputDataSha256, "remote-blank row-80 data readback");
        byte[] reapplied = UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(
            construction,
            construction.SourceData,
            reverse: false);
        byte[] inverse = UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(
            construction,
            outputData,
            reverse: true);
        if (!reapplied.SequenceEqual(outputData) || !inverse.SequenceEqual(construction.SourceData))
            throw new InvalidDataException("The runtime writer lost exact forward/inverse static-plan identity.");

        byte[] outputModel = outputData.AsSpan(
            UnusedLevel65RemoteBlankIsolationConstruction.ModelDataRelativeOffset,
            ModelByteLength).ToArray();
        RequireHash(Hash(outputModel), OutputModelSha256, "remote-blank model readback");
        UnusedLevel65RemoteBlankComponentProof collisionComponent =
            construction.Components.Single(component => component.Name == "collision");
        RequireHash(
            Hash(outputModel.AsSpan(
                collisionComponent.OutputRelativeOffset + CollisionTreeInComponentOffset,
                CollisionTreeByteLength)),
            OutputCollisionTreeSha256,
            "remote-blank collision tree readback");
        RequireHash(
            Hash(outputModel.AsSpan(
                collisionComponent.OutputRelativeOffset + CollisionBlocksInComponentOffset,
                CollisionBlocksByteLength)),
            OutputCollisionBlocksSha256,
            "remote-blank collision blocks readback");
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in construction.StructuralPatches)
        {
            if (!outputData.AsSpan(patch.DataRelativeOffset, patch.After.Length).SequenceEqual(patch.After))
                throw new InvalidDataException($"The output lost exact {patch.Kind} structural-patch readback.");
        }

        if (!construction.ProtectedSubfilesPreserved ||
            !construction.InheritedObjectRowsPreserved ||
            !construction.RetailWadEntriesExcluded ||
            !construction.ExecutableExcluded ||
            !construction.Scene.AllInheritedSectorPayloadsPreserved ||
            !construction.Occlusion.OtherGroupsPreserved ||
            !construction.Collision.NativeCellSequencesPreserved ||
            !construction.Collision.NativeOrderingPreserved)
        {
            throw new InvalidDataException("The static plan no longer proves inherited scaffolding preservation.");
        }
        if (!construction.Spawn.AtomicXyOnly ||
            construction.Spawn.LandingRawX != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX ||
            construction.Spawn.LandingRawY != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY ||
            construction.Spawn.PlayerRawX != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX ||
            construction.Spawn.PlayerRawY != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY ||
            construction.Spawn.PlayerRawZ - construction.Spawn.LandingRawZ != 154)
        {
            throw new InvalidDataException("The coupled landing/T92 readback changed.");
        }

        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        byte[] executableBytes = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            executable.Lba,
            0,
            executable.Size);
        RequireHash(Hash(executableBytes), construction.ExecutableSha256, "remote-blank executable preservation");

        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            expectedLayout,
            outputWad,
            cancellationToken);
        if (logical.ChangedBytes != construction.ChangedDataByteCount || logical.OutsideDataBytes != 0)
        {
            throw new InvalidDataException(
                $"The remote-blank logical diff changed {logical.ChangedBytes} WAD bytes, " +
                $"including {logical.OutsideDataBytes} outside exact row-80 data.");
        }
        if (!logical.ChangedRawSectorLbas.SequenceEqual(rebuiltRawSectorLbas))
        {
            throw new InvalidDataException(
                "The diff-derived MODE2 rebuild sector set does not exactly match row-80 readback.");
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
                $"The remote-blank physical diff changed {physical.ChangedRawSectorCount} sectors; " +
                $"header={physical.RawSectorDiffs.Sum(diff => diff.HeaderChangedBytes)}, " +
                $"subheader={physical.RawSectorDiffs.Sum(diff => diff.SubheaderChangedBytes)}, " +
                $"reserved={physical.RawSectorDiffs.Sum(diff => diff.ReservedChangedBytes)}.");
        }
        VerifyMode2Form1Classification(output, logical.ChangedRawSectorLbas);
        if (RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                output,
                outputLayout,
                CoalesceRawSectorRanges(rebuiltRawSectorLbas)) != rebuiltRawSectorLbas.Count)
        {
            throw new InvalidDataException("The remote-blank rebuilt-sector integrity verification count changed.");
        }

        return new(
            await HashFileAsync(outputImagePath, cancellationToken),
            Hash(outputData),
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
            physical.RawSectorDiffs);
    }

    private static UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan BuildPublishedPlan(
        UnusedLevel65RemoteBlankStaticPlan construction,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes) =>
        new(
            PlanSchemaVersion,
            ProfileId,
            construction.ProfileId,
            BaseImageSha256,
            construction.SourceDataSha256,
            construction.OutputDataSha256,
            construction.SourceModelSha256,
            construction.OutputModelSha256,
            construction.Collision.OutputTreeSha256,
            construction.Collision.OutputBlocksSha256,
            construction.DiffManifestSha256,
            construction.DeterministicPlanSha256,
            WadLba,
            DataWadOffset,
            DataByteLength,
            ModelWadOffset,
            ModelByteLength,
            construction.Scene.SourceSectorCount,
            construction.Scene.OutputSectorCount,
            construction.Scene.NewSectorIndex,
            construction.Scene.EnvironmentGrowthBytes,
            construction.Scene.NewSectorByteLength,
            construction.Scene.MaterialTextureId,
            construction.Occlusion.GroupCount,
            construction.Occlusion.Assignment,
            construction.Occlusion.SourceGroupZeroCount,
            construction.Occlusion.OutputGroupZeroCount,
            construction.Collision.ReusedTriangleIndex,
            construction.Collision.SourceAssignment,
            construction.Collision.OutputAssignment,
            construction.Collision.TargetCell,
            construction.Collision.BlocksCapacityBytes,
            construction.Collision.OutputBlocksUsedBytes,
            construction.Spawn.LandingRawX,
            construction.Spawn.LandingRawY,
            construction.Spawn.LandingRawZ,
            construction.Spawn.PlayerRawX,
            construction.Spawn.PlayerRawY,
            construction.Spawn.PlayerRawZ,
            construction.Isolation.ScannedInheritedSectorCount,
            construction.Isolation.InheritedMobyCount,
            construction.ChangedDataByteCount,
            construction.DiffRanges.Count,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            HashRawSectorDiffs(readback.RawSectorDiffs),
            loadCodes,
            Sector216ExactHpLpPairingVerified: true,
            FixedOcclusionOwnershipVerified: true,
            Triangle13995OrderedRepackVerified: true,
            CoupledLandingAndPlayerAnchorVerified: true,
            InheritedScaffoldingPreserved: true,
            BlankLookingNotByteEmpty: true,
            FarLowDetailRuntimePending: true,
            DeathPlaneRuntimePending: true,
            RequiresDuckStationRuntimeProof: true,
            DisposableRuntimeCandidateAuthorized: true,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);

    private static void ValidateConstructionGate(UnusedLevel65RemoteBlankStaticPlan construction)
    {
        if (construction.ProfileId != UnusedLevel65RemoteBlankIsolationConstruction.ProfileId ||
            construction.SourceImageSha256 != BaseImageSha256 ||
            construction.SourceDataSha256 != SourceDataSha256 ||
            construction.OutputDataSha256 != ExpectedOutputDataSha256 ||
            construction.SourceModelSha256 != SourceModelSha256 ||
            construction.OutputModelSha256 != OutputModelSha256 ||
            construction.DiffManifestSha256 != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDiffManifestSha256 ||
            construction.DeterministicPlanSha256 != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDeterministicPlanSha256 ||
            construction.WadLba != WadLba ||
            construction.DataWadOffset != DataWadOffset ||
            construction.DataByteLength != DataByteLength ||
            construction.ModelWadOffset != ModelWadOffset ||
            construction.ModelByteLength != ModelByteLength ||
            construction.Scene.SourceSectorCount != 216 ||
            construction.Scene.OutputSectorCount != 217 ||
            construction.Scene.NewSectorIndex != 216 ||
            construction.Scene.EnvironmentGrowthBytes != 0x74 ||
            construction.Scene.NewSectorByteLength != 0x70 ||
            construction.Scene.NewSectorSha256 != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNewSectorSha256 ||
            construction.Scene.MaterialTextureId != 25 ||
            !construction.Scene.AllInheritedSectorPayloadsPreserved ||
            !construction.Scene.ExactHpLpPairing ||
            !construction.Scene.OrdinaryCullRoute ||
            construction.Occlusion.Assignment != 0 ||
            !construction.Occlusion.GroupZeroInheritedOrderPreserved ||
            !construction.Occlusion.OtherGroupsPreserved ||
            !construction.Occlusion.FixedComponentLength ||
            !construction.Occlusion.TargetSectorOwned ||
            construction.Collision.ReusedTriangleIndex != 13_995 ||
            construction.Collision.OutputAssignment != 0 ||
            construction.Collision.TargetCell != new UnusedLevel65RemoteBlankCollisionCell(1, 1, 2) ||
            construction.Collision.TargetTreePointerRelativeOffset != 0x1254 ||
            construction.Collision.BlocksCapacityBytes != 0x17984 ||
            construction.Collision.OutputBlocksUsedBytes != 0x17960 ||
            construction.Collision.OutputTreeSha256 != OutputCollisionTreeSha256 ||
            construction.Collision.OutputBlocksSha256 != OutputCollisionBlocksSha256 ||
            !construction.Collision.TargetLeafWasVacant ||
            !construction.Collision.NativeCellSequencesPreserved ||
            !construction.Collision.NativeOrderingPreserved ||
            !construction.Collision.UpwardWinding ||
            !construction.Collision.FixedComponentLength ||
            construction.Spawn.LandingRawX != 6_153 ||
            construction.Spawn.LandingRawY != 6_154 ||
            construction.Spawn.LandingRawZ != 8_550 ||
            construction.Spawn.PlayerRawX != 6_153 ||
            construction.Spawn.PlayerRawY != 6_154 ||
            construction.Spawn.PlayerRawZ != 8_704 ||
            !construction.Spawn.AtomicXyOnly ||
            construction.Spawn.DeathPlaneRuntimeVerified ||
            construction.Spawn.DeathRespawnRuntimeVerified ||
            construction.Isolation.NativeLowDetailInteriorOverlapCount != 0 ||
            construction.Isolation.NativeHighDetailInteriorOverlapCount != 0 ||
            construction.Isolation.NativeCollisionInteriorOverlapCount != 0 ||
            !construction.Isolation.AuthoredCollisionIsOnlyTargetCellSurface ||
            !construction.Isolation.NativeMobysPhysicallyDisconnected ||
            construction.Isolation.NativeMobyRuntimeInactivityVerified ||
            construction.Isolation.FarLodRuntimeRouteVerified ||
            construction.ChangedDataByteCount != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedChangedDataByteCount ||
            construction.DiffRanges.Count != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDiffRangeCount ||
            !construction.PatchPreimagesVerified ||
            !construction.PatchAllowlistComplete ||
            !construction.ByteInverseVerified ||
            !construction.ProtectedSubfilesPreserved ||
            !construction.InheritedObjectRowsPreserved ||
            !construction.RetailWadEntriesExcluded ||
            !construction.ExecutableExcluded ||
            construction.SourceImageMutationPossible ||
            construction.DisposableRuntimeCandidateAuthorized ||
            construction.PromotionAuthorized ||
            construction.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact static ID65 remote blank-isolation gate is not satisfied.");
        }
        RequireHash(Hash(construction.OutputData), ExpectedOutputDataSha256, "planned remote-blank row-80 data");
        byte[] model = construction.OutputData.AsSpan(
            UnusedLevel65RemoteBlankIsolationConstruction.ModelDataRelativeOffset,
            ModelByteLength).ToArray();
        RequireHash(Hash(model), OutputModelSha256, "planned remote-blank model");
    }

    private static void ValidateConstructionDeterminism(
        UnusedLevel65RemoteBlankStaticPlan first,
        UnusedLevel65RemoteBlankStaticPlan second)
    {
        if (first.SourceImageSha256 != second.SourceImageSha256 ||
            first.SourceDataSha256 != second.SourceDataSha256 ||
            first.OutputDataSha256 != second.OutputDataSha256 ||
            first.SourceModelSha256 != second.SourceModelSha256 ||
            first.OutputModelSha256 != second.OutputModelSha256 ||
            first.DiffManifestSha256 != second.DiffManifestSha256 ||
            first.DeterministicPlanSha256 != second.DeterministicPlanSha256 ||
            first.ChangedDataByteCount != second.ChangedDataByteCount ||
            first.DiffRanges.Count != second.DiffRanges.Count ||
            !first.SourceData.SequenceEqual(second.SourceData) ||
            !first.OutputData.SequenceEqual(second.OutputData) ||
            !first.StructuralPatches.Select(patch => (
                    patch.Kind,
                    patch.DataRelativeOffset,
                    Convert.ToHexString(patch.Before),
                    Convert.ToHexString(patch.After)))
                .SequenceEqual(second.StructuralPatches.Select(patch => (
                    patch.Kind,
                    patch.DataRelativeOffset,
                    Convert.ToHexString(patch.Before),
                    Convert.ToHexString(patch.After)))))
        {
            throw new InvalidDataException("Two independent remote blank-isolation plans were not byte-deterministic.");
        }
    }

    private static bool IsExactDisposableRuntimeCandidateAuthorized(
        UnusedLevel65RemoteBlankStaticPlan construction) =>
        construction.ProfileId == UnusedLevel65RemoteBlankIsolationConstruction.ProfileId &&
        construction.SourceImageSha256 == BaseImageSha256 &&
        construction.OutputDataSha256 == ExpectedOutputDataSha256 &&
        construction.Scene.NewSectorIndex == 216 &&
        construction.Scene.ExactHpLpPairing &&
        construction.Occlusion.TargetSectorOwned &&
        construction.Collision.ReusedTriangleIndex == 13_995 &&
        construction.Collision.NativeOrderingPreserved &&
        construction.Spawn.AtomicXyOnly &&
        construction.ProtectedSubfilesPreserved &&
        construction.InheritedObjectRowsPreserved &&
        construction.RetailWadEntriesExcluded &&
        construction.ExecutableExcluded &&
        !construction.DisposableRuntimeCandidateAuthorized &&
        !construction.PromotionAuthorized &&
        !construction.NormalCreateBinEnabled;

    private static LogicalDiff CompareLogicalWad(
        FileStream baseline,
        FileStream output,
        DiscLayout layout,
        DiscFileRecord wad,
        CancellationToken cancellationToken)
    {
        const int chunkByteLength = 64 * 1024;
        long changed = 0;
        long outsideData = 0;
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
                if (wadOffset < DataWadOffset || wadOffset >= DataWadOffset + DataByteLength)
                    outsideData++;
                changedRawSectorLbas.Add(checked(wad.Lba + (int)(wadOffset / LogicalSectorByteLength)));
            }
        }
        return new(changed, outsideData, changedRawSectorLbas.ToArray());
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The remote-blank physical image comparison has incompatible lengths.");
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        List<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> diffs = [];
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
                throw new InvalidDataException($"Physical image bytes changed outside a logical row-80 sector at LBA {lba}.");

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

    private static void VerifyMode2Form1Classification(
        FileStream output,
        IReadOnlyList<int> changedRawSectorLbas)
    {
        byte[] raw = new byte[RawSectorByteLength];
        foreach (int lba in changedRawSectorLbas)
        {
            ReadRawSectorExactly(output, raw, lba, "remote-blank output");
            if (raw[15] != 0x02 ||
                !raw.AsSpan(16, 4).SequenceEqual(raw.AsSpan(20, 4)) ||
                (raw[18] & 0x08) == 0 ||
                (raw[18] & 0x20) != 0)
            {
                throw new InvalidDataException(
                    $"Raw LBA {lba} is not exact MODE2 Form1 data with duplicated subheader; reserved classification must stay zero.");
            }
        }
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
        IReadOnlyList<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff diff in diffs)
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
            throw new InvalidDataException("The remote-blank row-80 preimage/composition lengths differ.");
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
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        CandidateReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Remote Blank-Looking Pad — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This candidate moves ID65 gameplay to one isolated authored HP+LP pad at sector 216. " +
            "It is blank-looking, not byte-empty: inherited boot scaffolding remains locked and physically remote.");
        builder.AppendLine(
            "This is a disposable DuckStation acceptance gate only. Runtime is pending, the candidate is unpromoted, " +
            "and normal Create BIN remains disabled.");
        builder.AppendLine();
        if (finderReveal != null)
        {
            RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        }
        else
        {
            builder.AppendLine("## Candidate disc");
            builder.AppendLine();
            builder.AppendLine($"- CUE: `{Path.GetFileName(paths.OutputCuePath)}`");
            builder.AppendLine("- DuckStation: load the **CUE**, not the BIN.");
            builder.AppendLine();
        }
        builder.AppendLine("## Exact static identity");
        builder.AppendLine();
        builder.AppendLine($"- Base BIN SHA-256: `{BaseImageSha256}`");
        builder.AppendLine($"- Candidate BIN SHA-256: `{readback.OutputImageSha256}`");
        builder.AppendLine($"- ID65 row-80 data SHA-256: `{readback.OutputDataSha256}`");
        builder.AppendLine($"- Output model SHA-256: `{OutputModelSha256}`");
        builder.AppendLine($"- Output collision tree SHA-256: `{OutputCollisionTreeSha256}`");
        builder.AppendLine($"- Static construction plan: `{Path.GetFileName(paths.ConstructionPlanPath)}`");
        builder.AppendLine(
            $"- Logical/physical diff: `{readback.ChangedLogicalWadBytes}` WAD bytes / " +
            $"`{readback.ChangedPhysicalImageBytes}` raw-image bytes across " +
            $"`{readback.ChangedRawSectorCount}` changed raw sectors.");
        builder.AppendLine($"- Location guide: `{Path.GetFileName(paths.LocationGuidePath)}`");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## Clean-run controls");
        builder.AppendLine();
        builder.AppendLine("1. Quit DuckStation completely. Disable Moon Jump and every other cheat.");
        builder.AppendLine("2. Memory Card 1: **None**. Memory Card 2: **None**. **Do not save.**");
        builder.AppendLine("3. Do not load a save state. Cold boot the candidate CUE.");
        builder.AppendLine("4. Reach controllable gameplay, enter the complete ID65 load code once, then do not warp again during the candidate run.");
        builder.AppendLine();
        builder.AppendLine("## Candidate gates — report every checkbox");
        builder.AppendLine();
        builder.AppendLine("- [ ] **Lone-pad visibility:** Spyro appears on one small triangular pad; no inherited terrain or Mobys are visibly connected to it.");
        builder.AppendLine("- [ ] **Full 360 camera:** rotate the camera completely around Spyro twice; the same lone pad remains visible with no nearby native surface popping into view.");
        builder.AppendLine("- [ ] **Walk:** traverse the pad center and perimeter in both directions without pass-through, snag, hover, or hidden support.");
        builder.AppendLine("- [ ] **Charge:** charge across the center toward all three edges; movement follows the visible pad and never catches an invisible wall.");
        builder.AppendLine("- [ ] **Jump/land:** jump at the center and near every corner; each landing rests on the visible surface.");
        builder.AppendLine("- [ ] **Edge falls:** walk and charge off each of the three edges; Spyro leaves the visible surface cleanly instead of standing on an invisible extension.");
        builder.AppendLine("- [ ] **Death + same respawn:** complete a fall/death and confirm the next playable spawn is the same isolated pad, at the same orientation, without a warp or save state.");
        builder.AppendLine("- [ ] **Cold reset:** quit or hard-reset, cold boot the CUE again, enter the complete ID65 code, and confirm the same pad/spawn behavior.");
        builder.AppendLine();
        builder.AppendLine("## Runtime-pending boundaries");
        builder.AppendLine();
        builder.AppendLine("- **Far LP is runtime-pending:** back the camera away as far as ordinary play permits and report any pad disappearance, seam, or inherited-terrain pop-in.");
        builder.AppendLine("- **Death plane is runtime-pending:** the static plan does not claim the fall/death threshold or respawn loop until the death + same-respawn checkbox passes.");
        builder.AppendLine("- A blank-looking pass does not mean byte-empty. The inherited ID65 sectors, objects, music/totals/exit/save scaffolding remain present and locked.");
        builder.AppendLine();
        builder.AppendLine("## Retail control gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold boot/reset, enter the complete Retail Town Square code, and confirm normal terrain, collision, Mobys, camera, death/respawn, music, exit, and totals.");
        builder.AppendLine("- [ ] Cold boot/reset, enter the complete Gnasty's Loot code, and confirm normal gameplay and exit behavior.");
        builder.AppendLine("- [ ] Cold boot/reset, enter the complete Sunny Flight code, and confirm normal gameplay, timer, and exit behavior.");
        builder.AppendLine();
        builder.AppendLine("## Report boundary");
        builder.AppendLine();
        builder.AppendLine(
            "Report each checkbox separately, including failures. A boot, loading screen, still image, or one short walk is not a pass. " +
            "Do not use this candidate for normal editor output, saves, or release packaging.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        VerifyLoadCodes(loadCodes);
        string[] codeRows = loadCodes.Select(code =>
            XmlEscape($"{code.TestName} (ID {code.LevelId}): {code.InputCode}")).ToArray();
        return
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\" data-safe-left=\"60\" data-safe-right=\"1140\">\n" +
        "  <rect width=\"1200\" height=\"1000\" fill=\"#0d1320\"/>\n" +
        "  <text x=\"60\" y=\"58\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"31\" font-weight=\"700\">ID65 Remote Blank-Looking Pad Runtime Gate</text>\n" +
        "  <text x=\"60\" y=\"94\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\">Sector 216 • one isolated HP + LP pad • inherited boot scaffolding remains remote</text>\n" +
        "  <rect x=\"60\" y=\"125\" width=\"1080\" height=\"430\" rx=\"22\" fill=\"#18233a\" stroke=\"#657da8\" stroke-width=\"3\"/>\n" +
        "  <circle cx=\"600\" cy=\"336\" r=\"154\" fill=\"#101827\" stroke=\"#4f668c\" stroke-width=\"2\" stroke-dasharray=\"9 11\"/>\n" +
        "  <polygon points=\"600,190 460,448 740,448\" fill=\"#4fd7a0\" stroke=\"#e3fff4\" stroke-width=\"6\"/>\n" +
        "  <circle cx=\"600\" cy=\"350\" r=\"24\" fill=\"#8d68ff\" stroke=\"#ffffff\" stroke-width=\"4\"/>\n" +
        "  <text x=\"557\" y=\"357\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"15\" font-weight=\"700\">SPYRO</text>\n" +
        "  <text x=\"90\" y=\"174\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Expected view</text>\n" +
        "  <text x=\"90\" y=\"205\" fill=\"#d8e3fa\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">One lone triangle only.</text>\n" +
        "  <text x=\"90\" y=\"232\" fill=\"#d8e3fa\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Rotate camera 360° twice.</text>\n" +
        "  <text x=\"820\" y=\"174\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Test all three edges</text>\n" +
        "  <text x=\"820\" y=\"205\" fill=\"#d8e3fa\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Walk • charge • jump</text>\n" +
        "  <text x=\"820\" y=\"232\" fill=\"#d8e3fa\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Fall • death • same respawn</text>\n" +
        "  <text x=\"90\" y=\"510\" fill=\"#9fb3da\" font-family=\"Menlo,monospace\" font-size=\"13\">Pad points: (272,272,512)  (496,272,512)  (384,496,512)</text>\n" +
        "  <text x=\"60\" y=\"590\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Cards 1/2: None • Do not save • Cold boot • Cheats off • Load the paired CUE</text>\n" +
        "  <text x=\"60\" y=\"625\" fill=\"#ff9c9c\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Far LP and death plane are RUNTIME-PENDING. Blank-looking does not mean byte-empty.</text>\n" +
        "  <text x=\"60\" y=\"666\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">Pinned candidate identity</text>\n" +
        $"  <text x=\"60\" y=\"690\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">Profile: {ProfileId}</text>\n" +
        $"  <text x=\"60\" y=\"712\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">CUE: {XmlEscape(OutputPrefix + ".cue")}</text>\n" +
        $"  <text x=\"60\" y=\"734\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"10\">BIN SHA-256: {ExpectedOutputImageSha256}</text>\n" +
        "  <text x=\"850\" y=\"734\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"11\" font-weight=\"700\">UNPROMOTED</text>\n" +
        "  <text x=\"60\" y=\"770\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Full comparison load codes</text>\n" +
        $"  <text x=\"80\" y=\"800\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[0]}</text>\n" +
        $"  <text x=\"80\" y=\"832\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[1]}</text>\n" +
        $"  <text x=\"80\" y=\"864\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[2]}</text>\n" +
        $"  <text x=\"80\" y=\"896\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[3]}</text>\n" +
        "  <text x=\"60\" y=\"952\" fill=\"#aebfe1\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"12\">Report lone-pad visibility, 360°, movement, all edge falls, death/respawn, cold reset, and retail controls separately.</text>\n" +
        "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths)
    {
        string[] required =
        [
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.LocationGuidePath),
            Path.GetFileName(paths.ConstructionPlanPath),
            BaseImageSha256,
            OutputModelSha256,
            OutputCollisionTreeSha256,
            "load the **CUE**, not the BIN",
            "Memory Card 1: **None**",
            "Memory Card 2: **None**",
            "**Do not save.**",
            "**Lone-pad visibility:**",
            "**Full 360 camera:**",
            "**Walk:**",
            "**Charge:**",
            "**Jump/land:**",
            "**Edge falls:**",
            "**Death + same respawn:**",
            "**Cold reset:**",
            "**Far LP is runtime-pending:**",
            "**Death plane is runtime-pending:**",
            "blank-looking, not byte-empty",
            "Retail Town Square",
            "Gnasty's Loot",
            "Sunny Flight",
            "normal Create BIN remains disabled"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The remote-blank runtime checklist omitted an exact required gate.");
        foreach (RuntimeCandidateLoadCode loadCode in loadCodes)
        {
            if (!checklist.Contains(loadCode.InputCode, StringComparison.Ordinal))
                throw new InvalidDataException($"The checklist omitted the {loadCode.TestName} input code.");
        }
        if (finderReveal != null)
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finderReveal, loadCodes);
    }

    private static void VerifyLocationGuide(string svg)
    {
        string[] required =
        [
            "ID65 Remote Blank-Looking Pad Runtime Gate",
            "Sector 216",
            "one isolated HP + LP pad",
            "Rotate camera 360° twice",
            "Walk • charge • jump",
            "Fall • death • same respawn",
            "Cards 1/2: None",
            "Do not save",
            "Far LP and death plane are RUNTIME-PENDING",
            "Blank-looking does not mean byte-empty",
            "width=\"1200\" height=\"1000\"",
            "data-safe-left=\"60\" data-safe-right=\"1140\"",
            "272,272,512",
            "496,272,512",
            "384,496,512",
            "Pinned candidate identity",
            ProfileId,
            OutputPrefix + ".cue",
            ExpectedOutputImageSha256,
            "UNPROMOTED",
            "Full comparison load codes",
            "font-size=\"10\""
        ];
        if (!svg.StartsWith("<?xml", StringComparison.Ordinal) ||
            required.Any(value => !svg.Contains(value, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The remote-blank SVG location guide failed deterministic readback.");
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
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        bool includeFinderReveal)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            Path.GetFileName(paths.OutputImagePath),
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.ConstructionPlanPath),
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
                $"The staged remote-blank artifact set is [{string.Join(',', actual)}], expected [{string.Join(',', expected.Order())}].");
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
                throw new IOException("The remote-blank destination changed before replacement publication.");
            if (Directory.Exists(publication.BackupDirectoryPath))
                throw new IOException("The remote-blank operation backup path already exists.");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            publication.PreviousBackedUp = true;
            WriteOperationJournal(publication, OperationPhasePreviousBackedUp);
            testStageHook?.Invoke("after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The remote-blank destination appeared during staging.");
        }

        if (!Directory.Exists(publication.StagedDirectoryPath))
            throw new IOException("The staged remote-blank candidate disappeared before publication.");
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
                if (publication.PreviousBackedUp)
                {
                    testStageHook?.Invoke("before-previous-candidate-restore");
                    if (!Directory.Exists(publication.BackupDirectoryPath))
                        throw new IOException("The previous remote-blank candidate backup is missing.");
                    if (Directory.Exists(publication.OutputDirectoryPath))
                        throw new IOException("The remote-blank destination is occupied before backup restoration.");
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
                "Remote-blank publication failed and full-directory recovery is incomplete; " +
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
        RejectReparsePoint(operationsDirectoryPath, "remote-blank operations directory");
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
                "stale remote-blank publication output");
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
                $"The remote-blank operations directory contains a non-owned entry: {operationRoot}");
        }
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale remote-blank operation");
        RejectReparsePoint(operationRoot, "stale remote-blank operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale remote-blank operation lease");
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
                throw new IOException("A remote-blank candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned remote-blank operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale remote-blank operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A remote-blank operation journal is unreadable.");
        ValidateOperationJournal(
            journal,
            operationRoot,
            operationsDirectoryPath,
            expectedOutputDirectoryPath);

        RejectUnexpectedDirectoryRoleType(
            journal.StagedDirectoryPath,
            "stale remote-blank staged candidate");
        RejectUnexpectedDirectoryRoleType(
            journal.BackupDirectoryPath,
            "stale remote-blank previous-candidate backup");
        bool stagedExists = Directory.Exists(journal.StagedDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (stagedExists)
        {
            RejectTreeReparsePoints(
                journal.StagedDirectoryPath,
                "stale remote-blank staged candidate");
        }
        if (backupExists)
        {
            RejectTreeReparsePoints(
                journal.BackupDirectoryPath,
                "stale remote-blank previous-candidate backup");
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
                "Multiple previous remote-blank candidate backups exist for the same publication target; " +
                "recovery was preserved for audit.");
        }
        if (committed.Length > 1)
        {
            throw new IOException(
                "Multiple committed remote-blank operations claim the same publication target; " +
                "recovery was preserved for audit.");
        }
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A remote-blank operation without a previous candidate owns an unexpected backup.");
            }
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp &&
                !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A remote-blank operation reports a previous-candidate backup without a previous candidate.");
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
                    "A committed remote-blank operation is missing its candidate directory; " +
                    "recovery was preserved for audit.");
            }
            if (backups.Length == 1 && !ReferenceEquals(backups[0], committedOperation))
            {
                throw new IOException(
                    "An uncommitted remote-blank backup competes with a committed publication; " +
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
                    "A rollback-complete remote-blank operation still owns a backup; " +
                    "recovery was preserved for audit.");
            }
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
            {
                throw new IOException(
                    "A previous remote-blank candidate is missing and no unique backup can restore it; " +
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
                        "A stale remote-blank operation cannot prove whether the published directory is the " +
                        "previous candidate; recovery was preserved for audit.");
                }

                bool uncommittedCandidateIsAuthoritative = operations.Any(operation =>
                    operation.Journal.Phase == OperationPhaseCandidatePublished ||
                    (operation.Journal.Phase == OperationPhaseStaging && !operation.StagedExists));
                if (!uncommittedCandidateIsAuthoritative)
                {
                    throw new IOException(
                        "A no-previous remote-blank recovery cannot prove ownership of the published directory; " +
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
                throw new IOException("The remote-blank destination is occupied before authoritative backup restoration.");
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
                "A remote-blank recovery refused to discard an operation while its backup still exists.");
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
            RejectReparsePoint(entry, "stale remote-blank operation entry");
            string name = Path.GetFileName(entry);
            if (!allowedNames.Contains(name))
            {
                throw new InvalidDataException(
                    $"A stale remote-blank operation contains an unowned entry: {entry}");
            }
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
            {
                throw new InvalidDataException(
                    $"A stale remote-blank operation entry has the wrong filesystem role: {entry}");
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
            throw new InvalidOperationException("The remote-blank publication output has no parent.");
        if (!Directory.Exists(parent))
            return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, outputDirectoryPath))
                continue;
            RejectReparsePoint(entry, "remote-blank publication output path");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    "The remote-blank publication output path is not a directory.");
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
            throw new InvalidOperationException("A remote-blank recovery target escaped the exact requested output path.");
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
            throw new InvalidDataException("A stale remote-blank operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale remote-blank journal points outside its publication parent.");
    }

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

    private static IReadOnlyList<string> EnumeratePublishedArtifactPaths(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        bool includeFinderReveal) =>
        includeFinderReveal
            ?
            [
                paths.OutputImagePath,
                paths.OutputCuePath,
                paths.ConstructionPlanPath,
                paths.StaticReadbackReceiptPath,
                paths.RuntimeChecklistPath,
                paths.LocationGuidePath,
                paths.FinderHelperPath
            ]
            :
            [
                paths.OutputImagePath,
                paths.OutputCuePath,
                paths.ConstructionPlanPath,
                paths.StaticReadbackReceiptPath,
                paths.RuntimeChecklistPath,
                paths.LocationGuidePath
            ];

    private static void ValidatePublishedReceiptIdentity(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt receipt,
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        bool includeFinderReveal)
    {
        bool exactBasePaths =
            !string.IsNullOrWhiteSpace(receipt.BaseImagePath) &&
            !string.IsNullOrWhiteSpace(receipt.BaseCuePath) &&
            string.Equals(receipt.BaseImagePath, Path.GetFullPath(receipt.BaseImagePath), PathComparison) &&
            string.Equals(receipt.BaseCuePath, Path.GetFullPath(receipt.BaseCuePath), PathComparison);
        bool exactFinalPaths =
            string.Equals(receipt.OutputDirectoryPath, paths.OutputDirectoryPath, PathComparison) &&
            string.Equals(receipt.OutputImagePath, paths.OutputImagePath, PathComparison) &&
            string.Equals(receipt.OutputCuePath, paths.OutputCuePath, PathComparison) &&
            string.Equals(receipt.ConstructionPlanPath, paths.ConstructionPlanPath, PathComparison) &&
            string.Equals(receipt.StaticReadbackReceiptPath, paths.StaticReadbackReceiptPath, PathComparison) &&
            string.Equals(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath, PathComparison) &&
            string.Equals(receipt.LocationGuidePath, paths.LocationGuidePath, PathComparison);
        bool exactFinderFields = includeFinderReveal
            ? string.Equals(receipt.FinderHelperPath, paths.FinderHelperPath, PathComparison) &&
              IsExactSha256(receipt.FinderHelperSha256)
            : receipt.FinderHelperPath == null && receipt.FinderHelperSha256 == null;
        bool exactHashes =
            IsExactSha256(receipt.OutputCueSha256) &&
            IsExactSha256(receipt.ConstructionPlanSha256) &&
            IsExactSha256(receipt.RuntimeChecklistSha256) &&
            IsExactSha256(receipt.LocationGuideSha256) &&
            IsExactSha256(receipt.BaseCueSha256);
        bool exactRawDiffs =
            receipt.RawSectorDiffs.Count == ExpectedChangedRawSectorCount &&
            receipt.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                .SequenceEqual(ExpectedChangedRawSectorLbas) &&
            HashRawSectorDiffs(receipt.RawSectorDiffs) == ExpectedRawSectorDiffSha256 &&
            receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256;
        bool exactFlags =
            receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified &&
            receipt.StaticPlanPinsVerified &&
            receipt.ModelReadbackVerified &&
            receipt.CollisionReadbackVerified &&
            receipt.Sector216ExactHpLpPairingVerified &&
            receipt.FixedOcclusionOwnershipVerified &&
            receipt.Triangle13995OrderedRepackVerified &&
            receipt.ProtectedSubfilesPreserved &&
            receipt.CoupledLandingAndPlayerAnchorVerified &&
            receipt.InheritedScaffoldingPreserved &&
            receipt.ExecutablePreserved &&
            receipt.RetailControlLevelsPreserved &&
            receipt.RawSectorIntegrityVerified &&
            receipt.BaseCandidatePreserved &&
            receipt.FullDirectoryPublicationVerified &&
            receipt.DisposableRuntimeCandidateAuthorized &&
            !receipt.RollbackRecoveryVerified &&
            !receipt.FullDirectoryRollbackVerified &&
            receipt.FinderHandoffVerified == includeFinderReveal &&
            !receipt.PromotionAuthorized &&
            !receipt.NormalCreateBinEnabled;
        if (receipt.SchemaVersion != ReceiptSchemaVersion ||
            receipt.ProfileId != ProfileId ||
            !exactBasePaths ||
            !exactFinalPaths ||
            !exactFinderFields ||
            !exactHashes ||
            receipt.BaseImageSha256 != BaseImageSha256 ||
            receipt.OutputImageSha256 != ExpectedOutputImageSha256 ||
            receipt.OutputDataSha256 != ExpectedOutputDataSha256 ||
            receipt.OutputModelSha256 != OutputModelSha256 ||
            receipt.OutputCollisionTreeSha256 != OutputCollisionTreeSha256 ||
            receipt.OutputCollisionBlocksSha256 != OutputCollisionBlocksSha256 ||
            receipt.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
            receipt.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
            receipt.RebuiltRawSectorCount != ExpectedChangedRawSectorCount ||
            receipt.ChangedRawSectorCount != ExpectedChangedRawSectorCount ||
            !exactRawDiffs ||
            !exactFlags)
        {
            throw new InvalidDataException(
                "The published remote-blank receipt lost an exact path, pin, diff, or fail-closed flag.");
        }
    }

    private static void ValidatePublishedReadback(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt receipt,
        CandidateReadback readback)
    {
        string rawSectorDiffSha256 = HashRawSectorDiffs(readback.RawSectorDiffs);
        if (readback.OutputImageSha256 != ExpectedOutputImageSha256 ||
            readback.OutputImageSha256 != receipt.OutputImageSha256 ||
            readback.OutputDataSha256 != ExpectedOutputDataSha256 ||
            readback.OutputDataSha256 != receipt.OutputDataSha256 ||
            readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
            readback.ChangedLogicalWadBytes != receipt.ChangedLogicalWadBytes ||
            readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
            readback.ChangedPhysicalImageBytes != receipt.ChangedPhysicalImageBytes ||
            readback.ChangedRawSectorCount != ExpectedChangedRawSectorCount ||
            readback.ChangedRawSectorCount != receipt.ChangedRawSectorCount ||
            !readback.RawSectorDiffs.SequenceEqual(receipt.RawSectorDiffs) ||
            rawSectorDiffSha256 != ExpectedRawSectorDiffSha256 ||
            rawSectorDiffSha256 != receipt.RawSectorDiffSha256)
        {
            throw new InvalidDataException(
                "The published remote-blank candidate no longer matches its frozen receipt/readback pins.");
        }
    }

    private static bool IsExactSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void VerifyPublishedCandidate(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published remote-blank candidate directory is missing.");
        VerifyStagedCandidateArtifacts(
            paths.OutputDirectoryPath,
            paths,
            staged.FinderReveal != null);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        RequireHash(HashFile(paths.OutputImagePath), staged.Readback.OutputImageSha256, "published remote-blank BIN");
        RequireHash(HashFile(paths.OutputCuePath), staged.Receipt.OutputCueSha256, "published remote-blank CUE");
        RequireHash(
            HashFile(paths.ConstructionPlanPath),
            staged.Receipt.ConstructionPlanSha256,
            "published remote-blank construction plan");
        RequireHash(
            HashFile(paths.RuntimeChecklistPath),
            staged.Receipt.RuntimeChecklistSha256,
            "published remote-blank runtime checklist");
        RequireHash(
            HashFile(paths.LocationGuidePath),
            staged.Receipt.LocationGuideSha256,
            "published remote-blank location guide");
        VerifyTextReadback(
            paths.ConstructionPlanPath,
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
        VerifyLocationGuide(File.ReadAllText(paths.LocationGuidePath));
        if (finalReveal != null)
        {
            if (!OperatingSystem.IsMacOS() ||
                !File.Exists(paths.FinderHelperPath) ||
                File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
            {
                throw new InvalidDataException("The published remote-blank Finder helper is not exact 0755.");
            }
            RequireHash(
                HashFile(paths.FinderHelperPath),
                staged.Receipt.FinderHelperSha256!,
                "published remote-blank Finder helper");
            VerifyTextReadback(
                paths.FinderHelperPath,
                BuildExpectedFinderHelper(paths.OutputCuePath),
                "published remote-blank Finder helper content");
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(
                checklist,
                finalReveal,
                staged.Plan.LoadCodes);
        }
        else if (File.Exists(paths.FinderHelperPath))
        {
            throw new InvalidDataException("A remote-blank Finder helper was published while handoff was disabled.");
        }
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static string BuildExpectedFinderHelper(string cuePath)
    {
        string cueName = Path.GetFileName(Path.GetFullPath(cuePath));
        return
            "#!/bin/zsh\n" +
            "set -euo pipefail\n" +
            "\n" +
            $"cue_name={ShellSingleQuote(cueName)}\n" +
            "cue_path=\"${0:A:h}/${cue_name}\"\n" +
            "if [[ ! -f \"$cue_path\" ]]; then\n" +
            "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
            "  exit 1\n" +
            "fi\n" +
            "/usr/bin/open -R \"$cue_path\"\n";
    }

    private static void PrepareDestination(string outputDirectoryPath, bool replaceExistingCandidate)
    {
        if (!Directory.Exists(outputDirectoryPath))
            return;
        RejectReparsePoint(outputDirectoryPath, "remote-blank output directory");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The remote-blank candidate directory already exists. " +
                "Set ReplaceExistingCandidate only for an intentional atomic replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths)
    {
        if (IsDescendantOrEqual(baseImage, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseImage) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseCue))
        {
            throw new InvalidOperationException(
                "The remote-blank output directory and locked base files must have distinct roles.");
        }
        if (PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The remote-blank output and operations directories overlap.");
    }

    private static void RequirePathsInsideOutput(
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths)
    {
        string[] artifacts =
        [
            paths.OutputPrefix,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.ConstructionPlanPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath
        ];
        foreach (string artifact in artifacts)
            EnsureStrictDescendant(artifact, paths.OutputDirectoryPath, "remote-blank artifact");
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent))
            throw new InvalidOperationException("The remote-blank operations directory escaped the output parent.");
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
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The remote-blank operations directory has no parent.");
        Directory.CreateDirectory(parent);
        string leasePath = Path.Combine(parent, GlobalWriterLeaseFileName);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "remote-blank global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The remote-blank writer lease path is a directory.");
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
                "The remote-blank global writer lease file could not be opened.",
                ex);
        }

        try
        {
            RejectReparsePoint(leasePath, "remote-blank global writer lease");
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
                        "The remote-blank global writer OS range lease could not be acquired.",
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
                    $"The remote-blank global writer OS lease failed (errno {error}: {nativeError.Message}).",
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
        if (!IsPendingHash(expected))
            RequireHash(actual, expected, label);
    }

    private static bool IsPendingHash(string value) =>
        string.IsNullOrEmpty(value) || string.Equals(value, "PENDING", StringComparison.Ordinal);

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
            throw new InvalidOperationException("A remote-blank rollback target escaped its exact publication path.");
    }

    private static void EnsureJournalPublicationPath(
        string path,
        CandidateOperationJournal journal)
    {
        if (!PathEquals(path, journal.OutputDirectoryPath))
            throw new InvalidOperationException("A stale remote-blank rollback target escaped its journal path.");
    }

    private static void TryDeleteOwnedOperation(string operationRoot, string operationsDirectoryPath)
    {
        if (!Directory.Exists(operationRoot))
            return;
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed remote-blank operation");
        if (!Path.GetFileName(operationRoot).StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-owned remote-blank operation directory.");
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
        UnusedLevel65RemoteBlankIsolationRuntimeCandidatePlan Plan,
        UnusedLevel65RemoteBlankIsolationRuntimeCandidateReceipt Receipt,
        RuntimeCandidateFinderReveal? FinderReveal,
        CandidateReadback Readback,
        int RebuiltRawSectorCount);

    private sealed record CandidateReadback(
        string OutputImageSha256,
        string OutputDataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> RawSectorDiffs);

    private sealed record LogicalDiff(
        long ChangedBytes,
        long OutsideDataBytes,
        IReadOnlyList<int> ChangedRawSectorLbas);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65RemoteBlankIsolationRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
