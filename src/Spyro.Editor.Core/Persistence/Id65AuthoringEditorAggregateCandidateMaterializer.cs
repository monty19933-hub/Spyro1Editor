using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringEditorAggregateCandidateMaterializationRequest(
    UnusedLevel65BlankLevelLabWorkspacePaths Paths,
    UnusedLevel65BlankLevelLabManifest ExpectedManifest,
    Id65AuthoringEditorDocumentStoreResult ExpectedStoredDocument);

internal sealed record Id65AuthoringEditorAggregateCandidatePaths(
    string CandidateRootPath,
    string OperationsRootPath,
    string WriterLeasePath,
    string CandidateDirectoryPath,
    string CandidateImagePath,
    string CandidateCuePath,
    string CandidateReceiptPath);

internal sealed record Id65AuthoringEditorAggregateCandidateReceipt(
    string Format,
    int SchemaVersion,
    string ProfileId,
    string WorkspaceKey,
    int WorkspaceLayoutVersion,
    string LabProfileId,
    int LabProfileVersion,
    string LockedBaseProfileId,
    string SourceImageSha256,
    string DocumentEnvelopeSha256,
    string DocumentCanonicalSha256,
    string AggregatePlanSha256,
    string CompositeManifestSha256,
    string OutputRow80Sha256,
    string OutputImageSha256,
    string CandidateImageFileName,
    string CandidateCueFileName,
    string CandidateCueSha256,
    int TargetDataWadEntry,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    int FirstRebuiltRawSectorLba,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    string PhysicalDiffSha256,
    bool ExactStoreReloadVerified,
    bool ExactLockedBaseRecaptured,
    bool ExactRow80ReadbackVerified,
    bool ExactInverseReadbackVerified,
    bool ProtectedComplementVerified,
    bool RetailTownSquarePreserved,
    bool TargetOverlayPreserved,
    bool ExecutablePreserved,
    bool RawSectorIntegrityVerified,
    bool PhysicalComplementVerified,
    bool SourceLockedBasePreserved,
    bool AtomicNoReplaceRenameCompleted,
    bool ContainsFullDiscImage,
    bool ContainsCue,
    bool WorkspaceCandidateWriterAuthorized,
    bool RuntimeCandidateAuthorized,
    bool RuntimeProven,
    bool SaveAuthorized,
    bool SavePersistenceProven,
    bool TreasureTotal200Proven,
    bool MusicLongPlayProven,
    bool ExitDestination65Proven,
    bool AppIntegrated,
    bool UiIntegrated,
    bool CreateBinEnabled,
    bool NormalCreateBinEnabled,
    bool RetailLevelMutationAuthorized,
    bool ReleaseIntegrated,
    bool ReleaseAuthorized,
    bool PromotionAuthorized,
    bool Publishable,
    bool CurrentOnDiskFreshnessVerified,
    bool HostileFilesystemRaceSafe,
    bool RequiresTrustedExclusiveWorkspaceDirectory,
    bool CommonWorkspaceLeaseOwned,
    string ReceiptContentSha256);

internal sealed record Id65AuthoringEditorAggregateCandidateMaterializationResult(
    Id65AuthoringEditorAggregateCandidatePaths Paths,
    Id65AuthoringEditorAggregateCandidateReceipt Receipt,
    Id65AuthoringEditorAggregatePlan Plan,
    bool ReusedExistingCandidate,
    bool CandidateArtifactsWrittenThisCall,
    bool FileSystemMutationPerformed,
    bool WritesDiscImage,
    bool WritesBin,
    bool WritesCue,
    bool AtomicNoReplaceRenameCompletedThisCall,
    bool VerifiedAtomicNoReplacePublicationProvenance,
    bool OwnedTemporaryResidueRemoved,
    bool FinderHandoffPerformed,
    bool AppIntegrated,
    bool UiIntegrated,
    bool CreateBinEnabled,
    bool NormalCreateBinEnabled,
    bool RuntimeCandidateAuthorized,
    bool RuntimeProven,
    bool ReleaseIntegrated,
    bool PromotionAuthorized,
    bool Publishable,
    bool CurrentOnDiskFreshnessVerified,
    bool HostileFilesystemRaceSafe,
    bool RequiresTrustedExclusiveWorkspaceDirectory,
    bool CommonWorkspaceLeaseOwned);

internal enum Id65AuthoringEditorAggregateCandidateFaultPoint
{
    None,
    AfterLeaseAcquired,
    AfterSourceCopied,
    AfterRowWritten,
    AfterRawSectorsRebuilt,
    AfterStagedReadback,
    AfterStagedReadbackBeforePrepublishAudit,
    BeforeAtomicRename,
    AfterAtomicRenameBeforeCleanup
}

/// <summary>
/// Materializes exactly one content-addressed, workspace-local BIN/CUE from a
/// freshly reloaded full ID65 editor document. This is not normal Create BIN,
/// App integration, runtime acceptance, or release promotion.
/// </summary>
internal static class Id65AuthoringEditorAggregateCandidateMaterializer
{
    public const string Format =
        "spyro-editor-id65-authoring-editor-aggregate-candidate-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string ProfileId =
        "id65-authoring-editor-aggregate-candidate-darwin-anchored-workspace-v1";
    public const string CandidateImageFileName =
        "unusedlevel65blank-authoring-aggregate-candidate-v1.bin";
    public const string CandidateCueFileName =
        "unusedlevel65blank-authoring-aggregate-candidate-v1.cue";
    public const string CandidateReceiptFileName =
        "unusedlevel65blank-authoring-aggregate-candidate-v1.receipt.json";
    public const string ExpectedCandidateCueSha256 =
        "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8";
    public const int WadLba = 37;
    public const int ExpectedWadByteLength = 0x6C18800;
    public const int ExecutableLba = 55_382;
    public const int ExecutableByteLength = 0x66000;
    public const int TargetDataWadEntry = 80;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const int FirstRebuiltRawSectorLba = 53_906;
    public const int RebuiltRawSectorCount = 1_476;
    public const int LastRebuiltRawSectorLba = 55_381;
    public const string ExpectedOutputImageSha256 =
        "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647";
    public const long ExpectedChangedPhysicalImageBytes = 1_075_276;
    public const int ExpectedChangedRawSectorCount = 558;
    public const string ExpectedPhysicalDiffSha256 =
        "443bfdeb7727b2a1877eef60027cc1fdb9b1b1224cb1165c4a9e26b33b480211";
    public const string ExpectedReceiptContentSha256 =
        "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f";

    private const string CandidateRootDirectoryName = "materialized-candidates";
    private const string OperationsRootDirectoryName = ".aggregate-candidate-operations";
    private const string WriterLeaseFileName =
        ".unusedlevel65blank-aggregate-candidate-v1.writer.lock";
    private const string OperationPrefix = "materialize-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string OperationJournalTemporaryFileName = "operation-journal.json.tmp";
    private const string OperationLeaseFileName = "operation.lease";
    private const string StagedCandidateDirectoryName = "candidate";
    private const string OperationKind =
        "id65-authoring-editor-aggregate-candidate-materialization";
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhaseVerifiedReady = "verified-ready";
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const byte XaFinalDataSubmode = 0x89;
    private const long MaximumReceiptByteCount = 128 * 1024;
    private const ushort DarwinFileTypeMask = 0xF000;
    private const ushort DarwinDirectory = 0x4000;
    private const int DarwinGroupOrOtherWrite = 0x12;
    private const int DarwinOpenNoFollow = 0x00000100;
    private const int DarwinOpenDirectory = 0x00100000;
    private const int DarwinOpenCloseOnExec = 0x01000000;
    private const int DarwinAtSymlinkNoFollow = 0x0020;
    private const int DarwinNoEntry = 2;
    private const uint DarwinRenameExclusive = 0x00000004;

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    public static Task<Id65AuthoringEditorAggregateCandidateMaterializationResult>
        MaterializeAsync(
            Id65AuthoringEditorAggregateCandidateMaterializationRequest request,
            CancellationToken cancellationToken = default) =>
        MaterializeCoreAsync(
            request,
            Id65AuthoringEditorAggregateCandidateFaultPoint.None,
            beforePrepublishSmokeHook: null,
            cancellationToken: cancellationToken);

    internal static Task<Id65AuthoringEditorAggregateCandidateMaterializationResult>
        MaterializeForSmokeAsync(
            Id65AuthoringEditorAggregateCandidateMaterializationRequest request,
            Id65AuthoringEditorAggregateCandidateFaultPoint faultPoint,
            Func<Task>? beforePrepublishSmokeHook = null,
            CancellationToken cancellationToken = default)
    {
        if (faultPoint == Id65AuthoringEditorAggregateCandidateFaultPoint.None)
            throw new InvalidOperationException(
                "The smoke-only materializer entrypoint requires an injected failure point.");
        if (beforePrepublishSmokeHook is not null &&
            faultPoint != Id65AuthoringEditorAggregateCandidateFaultPoint
                .AfterStagedReadbackBeforePrepublishAudit)
        {
            throw new InvalidOperationException(
                "The smoke-only prepublish hook requires its exact non-publishing fault point.");
        }
        return MaterializeCoreAsync(
            request,
            faultPoint,
            beforePrepublishSmokeHook,
            cancellationToken);
    }

    private static async Task<Id65AuthoringEditorAggregateCandidateMaterializationResult>
        MaterializeCoreAsync(
            Id65AuthoringEditorAggregateCandidateMaterializationRequest request,
            Id65AuthoringEditorAggregateCandidateFaultPoint faultPoint,
            Func<Task>? beforePrepublishSmokeHook,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.ExpectedManifest);
        ArgumentNullException.ThrowIfNull(request.ExpectedStoredDocument);
        if (!Enum.IsDefined(faultPoint))
            throw new ArgumentOutOfRangeException(nameof(faultPoint));
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException(
                "The isolated aggregate candidate materializer currently requires macOS anchored directory publication.");
        cancellationToken.ThrowIfCancellationRequested();

        ProjectionCapture preflight = await CaptureFreshProjectionAsync(
            request,
            cancellationToken);
        Id65AuthoringEditorAggregateCandidatePaths paths = CreatePaths(
            request.Paths,
            preflight.Plan.DeterministicAggregatePlanSha256);
        bool fixedRootsMutated = PrepareFixedRoots(request.Paths, paths);

        using WriterLease writerLease = AcquireWriterLease(paths.WriterLeasePath);
        bool fixedBoundaryMutation = fixedRootsMutated || writerLease.CreatedFile;
        ThrowIfFault(faultPoint, Id65AuthoringEditorAggregateCandidateFaultPoint.AfterLeaseAcquired);

        ProjectionCapture leased = await CaptureFreshProjectionAsync(
            request,
            cancellationToken);
        RequireEquivalentProjection(preflight, leased, "after acquiring the materializer lease");

        if (Directory.Exists(paths.CandidateDirectoryPath) ||
            File.Exists(paths.CandidateDirectoryPath))
        {
            if (!Directory.Exists(paths.CandidateDirectoryPath))
                throw new InvalidDataException(
                    "The content-addressed aggregate candidate destination is not a directory.");
            RefuseReparsePoint(
                paths.CandidateDirectoryPath,
                "content-addressed aggregate candidate directory");
            RefuseReparsePoint(paths.CandidateImagePath, "content-addressed aggregate image");
            RefuseReparsePoint(paths.CandidateCuePath, "content-addressed aggregate cue");
            RefuseReparsePoint(paths.CandidateReceiptPath, "content-addressed aggregate receipt");
            Readback existing = await VerifyCandidateAsync(
                request.Paths.LockedBaseImagePath,
                paths.CandidateImagePath,
                leased.Plan,
                cancellationToken);
            Id65AuthoringEditorAggregateCandidateReceipt expectedReceipt = BuildReceipt(
                request,
                leased,
                existing);
            await RequireExactPublishedCandidateAsync(
                paths,
                expectedReceipt,
                cancellationToken);
            RecoveryResult recovery = RecoverOwnedOperations(paths);
            return BuildResult(
                paths,
                expectedReceipt,
                leased.Plan,
                reusedExistingCandidate: true,
                candidateArtifactsWrittenThisCall: false,
                fileSystemMutationPerformed: fixedBoundaryMutation || recovery.MutationPerformed,
                atomicNoReplaceRenameCompletedThisCall: false,
                verifiedAtomicNoReplacePublicationProvenance: true,
                recovery.AllOwnedResidueRemoved);
        }

        RecoveryResult recovered = RecoverOwnedOperations(paths);
        string operationRoot = Path.Combine(
            paths.OperationsRootPath,
            OperationPrefix + Guid.NewGuid().ToString("N"));
        RequireExactOperationPath(operationRoot, paths.OperationsRootPath);
        string operationLeasePath = Path.Combine(operationRoot, OperationLeaseFileName);
        string stagedDirectory = Path.Combine(operationRoot, StagedCandidateDirectoryName);
        string stagedImage = Path.Combine(stagedDirectory, CandidateImageFileName);
        string stagedCue = Path.Combine(stagedDirectory, CandidateCueFileName);
        string stagedReceipt = Path.Combine(stagedDirectory, CandidateReceiptFileName);

        bool renamed = false;
        bool operationRootCreated = false;
        FileStream? operationLease = null;
        bool operationLeaseDisposed = false;
        bool residueRemovedAfter = recovered.AllOwnedResidueRemoved;
        try
        {
            Directory.CreateDirectory(operationRoot);
            operationRootCreated = true;
            RefuseReparsePoint(operationRoot, "aggregate materialization operation root");
            operationLease = new(
                operationLeasePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            Directory.CreateDirectory(stagedDirectory);
            RefuseReparsePoint(stagedDirectory, "aggregate staged candidate root");
            WriteOperationJournal(
                operationRoot,
                leased.Plan.DeterministicAggregatePlanSha256,
                OperationPhaseStaging);

            await CopyExactLockedBaseAsync(
                request.Paths.LockedBaseImagePath,
                stagedImage,
                cancellationToken);
            ThrowIfFault(faultPoint, Id65AuthoringEditorAggregateCandidateFaultPoint.AfterSourceCopied);

            int rebuilt = ApplyAndRebuildExactRow80(
                stagedImage,
                leased.Plan,
                faultPoint);
            if (rebuilt != RebuiltRawSectorCount)
                throw new InvalidDataException(
                    $"The aggregate candidate rebuilt {rebuilt} raw sectors, expected {RebuiltRawSectorCount}.");

            Readback stagedReadback = await VerifyCandidateAsync(
                request.Paths.LockedBaseImagePath,
                stagedImage,
                leased.Plan,
                cancellationToken);
            ThrowIfFault(faultPoint, Id65AuthoringEditorAggregateCandidateFaultPoint.AfterStagedReadback);

            Id65AuthoringEditorAggregateCandidateReceipt receipt = BuildReceipt(
                request,
                leased,
                stagedReadback);
            string cueText = BuildCanonicalCandidateCue();
            await WriteNewFileDurablyAsync(
                stagedCue,
                Encoding.ASCII.GetBytes(cueText),
                cancellationToken);
            await WriteNewFileDurablyAsync(
                stagedReceipt,
                SerializeReceipt(receipt),
                cancellationToken);
            await RequireExactStagedCandidateAsync(
                stagedDirectory,
                stagedImage,
                stagedCue,
                stagedReceipt,
                cueText,
                receipt,
                cancellationToken);

            if (faultPoint == Id65AuthoringEditorAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit &&
                beforePrepublishSmokeHook is not null)
            {
                await beforePrepublishSmokeHook();
            }
            ProjectionCapture prepublish = await CaptureFreshProjectionAsync(
                request,
                cancellationToken);
            RequireEquivalentProjection(leased, prepublish, "immediately before candidate publication");
            ThrowIfFault(
                faultPoint,
                Id65AuthoringEditorAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit);
            Readback prepublishReadback = await VerifyCandidateAsync(
                request.Paths.LockedBaseImagePath,
                stagedImage,
                prepublish.Plan,
                cancellationToken);
            Id65AuthoringEditorAggregateCandidateReceipt prepublishReceipt = BuildReceipt(
                request,
                prepublish,
                prepublishReadback);
            if (prepublishReceipt != receipt)
                throw new InvalidDataException(
                    "The staged aggregate candidate identity changed before publication.");
            await RequireExactStagedCandidateAsync(
                stagedDirectory,
                stagedImage,
                stagedCue,
                stagedReceipt,
                cueText,
                receipt,
                cancellationToken);
            WriteOperationJournal(
                operationRoot,
                leased.Plan.DeterministicAggregatePlanSha256,
                OperationPhaseVerifiedReady);
            ThrowIfFault(faultPoint, Id65AuthoringEditorAggregateCandidateFaultPoint.BeforeAtomicRename);

            await operationLease.DisposeAsync();
            operationLeaseDisposed = true;

            RefuseReparsePoint(paths.CandidateRootPath, "aggregate candidate root");
            RefuseReparsePoint(paths.OperationsRootPath, "aggregate operation root");
            RefuseReparsePoint(operationRoot, "aggregate materialization operation root");
            // Commit point: renameatx_np(RENAME_EXCL) moves the complete verified
            // directory between stable parent handles, verifies the moved inode,
            // and fsyncs both parent directories.
            PublishDirectoryAtomicallyNoReplace(
                stagedDirectory,
                paths.CandidateDirectoryPath,
                onRenamed: () => renamed = true);
            ThrowIfFault(
                faultPoint,
                Id65AuthoringEditorAggregateCandidateFaultPoint.AfterAtomicRenameBeforeCleanup);
            BestEffortDeleteOwnedOperation(operationRoot, paths.OperationsRootPath);
            residueRemovedAfter = !Directory.Exists(operationRoot);
            return BuildResult(
                paths,
                receipt,
                leased.Plan,
                reusedExistingCandidate: false,
                candidateArtifactsWrittenThisCall: true,
                fileSystemMutationPerformed: true,
                atomicNoReplaceRenameCompletedThisCall: true,
                verifiedAtomicNoReplacePublicationProvenance: true,
                residueRemovedAfter);
        }
        finally
        {
            if (!renamed && operationRootCreated)
            {
                if (!operationLeaseDisposed && operationLease is not null)
                {
                    try
                    {
                        await operationLease.DisposeAsync();
                        operationLeaseDisposed = true;
                    }
                    catch
                    {
                        // Cleanup below remains best effort; an unverifiable
                        // operation tree is preserved for the next leased audit.
                    }
                }
                BestEffortDeleteOwnedOperation(operationRoot, paths.OperationsRootPath);
                residueRemovedAfter = !Directory.Exists(operationRoot);
            }
        }
    }

    private static async Task<ProjectionCapture> CaptureFreshProjectionAsync(
        Id65AuthoringEditorAggregateCandidateMaterializationRequest request,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(request.Paths.RootPath, "ID65 lab workspace root");
        RefuseReparsePoint(request.Paths.ManifestPath, "ID65 lab manifest");
        RefuseReparsePoint(request.Paths.LockedBaseDirectoryPath, "ID65 lab locked-base root");
        RefuseReparsePoint(request.Paths.LockedBaseImagePath, "ID65 lab locked-base image");
        RefuseReparsePoint(request.Paths.LockedBaseCuePath, "ID65 lab locked-base cue");
        RefuseReparsePoint(request.Paths.AuthoredEditsDirectoryPath, "ID65 lab authored-edits root");
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            request.ExpectedManifest,
            request.Paths);
        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceReadOnlyAsync(
                request.Paths,
                cancellationToken);
        if (published != request.ExpectedManifest)
            throw new InvalidDataException(
                "The aggregate candidate workspace manifest changed from the expected exact lab manifest.");

        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                request.Paths,
                request.ExpectedManifest,
                cancellationToken);
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs = new(
            nativeInputs);
        Id65AuthoringEditorDocumentStoreResult stored =
            await Id65AuthoringEditorDocumentStore.LoadAsync(
                request.Paths,
                request.ExpectedManifest,
                projectionInputs,
                cancellationToken) ?? throw new InvalidDataException(
                    "The aggregate candidate requires one persisted full ID65 editor document.");
        RequireExpectedStoredDocument(request.ExpectedStoredDocument, stored);
        Id65AuthoringEditorAggregatePlan plan =
            Id65AuthoringEditorAggregatePlanProjector.Project(new(
                stored,
                request.ExpectedManifest,
                nativeInputs));
        return new(stored, plan, nativeInputs.LockedBaseImageSha256);
    }

    private static void RequireExpectedStoredDocument(
        Id65AuthoringEditorDocumentStoreResult expected,
        Id65AuthoringEditorDocumentStoreResult actual)
    {
        if (!PathsEqual(expected.Path, actual.Path) ||
            expected.EnvelopeCanonicalSha256 != actual.EnvelopeCanonicalSha256 ||
            expected.ProjectionContractId != actual.ProjectionContractId ||
            expected.CompositeProfileId != actual.CompositeProfileId ||
            expected.CompositeManifestSha256 != actual.CompositeManifestSha256 ||
            expected.CompositeOutputRow80Sha256 != actual.CompositeOutputRow80Sha256 ||
            expected.CompositeDeterministicPlanSha256 !=
                actual.CompositeDeterministicPlanSha256 ||
            !expected.Document.SerializeUtf8().SequenceEqual(actual.Document.SerializeUtf8()))
        {
            throw new InvalidDataException(
                "The persisted ID65 editor document changed after its expected store readback was captured.");
        }
    }

    private static void RequireEquivalentProjection(
        ProjectionCapture expected,
        ProjectionCapture actual,
        string context)
    {
        if (expected.SourceImageSha256 != actual.SourceImageSha256 ||
            expected.Stored.EnvelopeCanonicalSha256 != actual.Stored.EnvelopeCanonicalSha256 ||
            expected.Stored.DocumentCanonicalSha256 != actual.Stored.DocumentCanonicalSha256 ||
            expected.Plan.DeterministicAggregatePlanSha256 !=
                actual.Plan.DeterministicAggregatePlanSha256 ||
            expected.Plan.OutputRow80Sha256 != actual.Plan.OutputRow80Sha256 ||
            expected.Plan.OwnedRangeMapSha256 != actual.Plan.OwnedRangeMapSha256 ||
            expected.Plan.ProtectedRangeMapSha256 != actual.Plan.ProtectedRangeMapSha256)
        {
            throw new InvalidDataException(
                $"The freshly reloaded aggregate projection changed {context}.");
        }
    }

    private static Id65AuthoringEditorAggregateCandidatePaths CreatePaths(
        UnusedLevel65BlankLevelLabWorkspacePaths workspace,
        string aggregatePlanSha256)
    {
        if (!IsCanonicalSha256(aggregatePlanSha256))
            throw new InvalidDataException("The aggregate candidate plan hash is not canonical SHA-256.");
        _ = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(workspace);
        string root = Path.GetFullPath(workspace.RootPath);
        string candidateRoot = Path.GetFullPath(Path.Combine(root, CandidateRootDirectoryName));
        string operationsRoot = Path.GetFullPath(Path.Combine(root, OperationsRootDirectoryName));
        string lease = Path.GetFullPath(Path.Combine(root, WriterLeaseFileName));
        string candidateDirectory = Path.GetFullPath(Path.Combine(
            candidateRoot,
            CreateCandidateDirectoryName(aggregatePlanSha256, ExpectedReceiptContentSha256)));
        PersistencePathSafety.EnsureWritePathWithinRoot(root, candidateRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(root, operationsRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(root, lease);
        PersistencePathSafety.EnsureWritePathWithinRoot(candidateRoot, candidateDirectory);
        return new(
            candidateRoot,
            operationsRoot,
            lease,
            candidateDirectory,
            Path.Combine(candidateDirectory, CandidateImageFileName),
            Path.Combine(candidateDirectory, CandidateCueFileName),
            Path.Combine(candidateDirectory, CandidateReceiptFileName));
    }

    private static string CreateCandidateDirectoryName(
        string aggregatePlanSha256,
        string receiptContentSha256)
    {
        if (!IsCanonicalSha256(aggregatePlanSha256) ||
            !IsCanonicalSha256(receiptContentSha256))
        {
            throw new InvalidDataException(
                "The aggregate candidate directory identity requires canonical SHA-256 pins.");
        }
        return $"aggregate-{aggregatePlanSha256}-receipt-{receiptContentSha256}";
    }

    private static bool PrepareFixedRoots(
        UnusedLevel65BlankLevelLabWorkspacePaths workspace,
        Id65AuthoringEditorAggregateCandidatePaths paths)
    {
        RefuseReparsePoint(workspace.RootPath, "ID65 lab workspace root");
        bool candidateRootCreated = !Directory.Exists(paths.CandidateRootPath);
        Directory.CreateDirectory(paths.CandidateRootPath);
        bool operationsRootCreated = !Directory.Exists(paths.OperationsRootPath);
        Directory.CreateDirectory(paths.OperationsRootPath);
        RefuseReparsePoint(paths.CandidateRootPath, "aggregate candidate root");
        RefuseReparsePoint(paths.OperationsRootPath, "aggregate operation root");
        PersistencePathSafety.EnsureWritePathWithinRoot(
            workspace.RootPath,
            paths.CandidateRootPath);
        PersistencePathSafety.EnsureWritePathWithinRoot(
            workspace.RootPath,
            paths.OperationsRootPath);
        return candidateRootCreated || operationsRootCreated;
    }

    private static WriterLease AcquireWriterLease(string path)
    {
        RefuseReparsePoint(path, "aggregate candidate writer lease");
        if (Directory.Exists(path))
            throw new InvalidDataException("The aggregate candidate writer lease is a directory.");
        bool created = false;
        try
        {
            FileStream lease;
            try
            {
                lease = new(
                    path,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
                created = true;
            }
            catch (IOException) when (File.Exists(path))
            {
                lease = new(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
            }
            try
            {
                RefuseReparsePoint(path, "aggregate candidate writer lease");
                if (Directory.Exists(path))
                    throw new InvalidDataException(
                        "The aggregate candidate writer lease became a directory.");
                return new(lease, created);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Another aggregate candidate materializer owns the workspace writer lease.",
                exception);
        }
    }

    private static async Task CopyExactLockedBaseAsync(
        string sourceImagePath,
        string stagedImagePath,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourceImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream destination = new(
            stagedImagePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, 1 << 20, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        destination.Flush(flushToDisk: true);
        if (destination.Length != source.Length)
            throw new InvalidDataException("The staged aggregate candidate source copy was truncated.");
        destination.Position = 0;
        string stagedHash = Convert.ToHexString(
            await SHA256.HashDataAsync(destination, cancellationToken)).ToLowerInvariant();
        if (stagedHash != Id65RewardCensusContract.LockedSourceImageSha256)
            throw new InvalidDataException(
                "The staged aggregate candidate is not an exact copy of the locked base.");
    }

    private static int ApplyAndRebuildExactRow80(
        string stagedImagePath,
        Id65AuthoringEditorAggregatePlan plan,
        Id65AuthoringEditorAggregateCandidateFaultPoint faultPoint)
    {
        DiscLayout layout = DiscImage.DetectLayout(stagedImagePath);
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException(
                "The aggregate candidate requires the exact MODE2/2352 locked-base layout.");
        using FileStream image = new(
            stagedImagePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        DiscFileRecord wad = FindWad(image, layout);
        DiscFileRecord executable = FindExecutable(image, layout);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength ||
            executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "The staged aggregate candidate WAD or executable extent changed.");
        }
        byte[] sourceRow = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        byte[] outputRow = Id65AuthoringEditorAggregatePlanProjector.ApplyExact(
            plan,
            sourceRow,
            reverse: false);
        DiscImage.WriteFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            outputRow);
        ThrowIfFault(faultPoint, Id65AuthoringEditorAggregateCandidateFaultPoint.AfterRowWritten);
        int rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
            image,
            layout,
            WadLba,
            [(TargetDataWadOffset, TargetDataByteLength)]);
        ThrowIfFault(
            faultPoint,
            Id65AuthoringEditorAggregateCandidateFaultPoint.AfterRawSectorsRebuilt);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            image,
            layout,
            [(FirstRebuiltRawSectorLba, RebuiltRawSectorCount)]);
        if (rebuilt != RebuiltRawSectorCount || verified != RebuiltRawSectorCount)
            throw new InvalidDataException(
                "The aggregate candidate raw-sector rebuild envelope changed.");
        for (int lba = FirstRebuiltRawSectorLba; lba <= LastRebuiltRawSectorLba; lba++)
        {
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                image,
                layout,
                lba,
                lba == LastRebuiltRawSectorLba ? XaFinalDataSubmode : XaDataSubmode);
        }
        image.Flush(flushToDisk: true);
        return rebuilt;
    }

    private static async Task<Readback> VerifyCandidateAsync(
        string sourceImagePath,
        string candidateImagePath,
        Id65AuthoringEditorAggregatePlan plan,
        CancellationToken cancellationToken)
    {
        DiscLayout sourceLayout = DiscImage.DetectLayout(sourceImagePath);
        DiscLayout candidateLayout = DiscImage.DetectLayout(candidateImagePath);
        if (sourceLayout != candidateLayout || sourceLayout.SectorSize != RawSectorByteLength ||
            sourceLayout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException("The aggregate candidate changed the raw disc layout.");
        }
        await using FileStream source = new(
            sourceImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream candidate = new(
            candidateImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length != candidate.Length || source.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The aggregate candidate changed the raw image length.");

        DiscFileRecord sourceWad = FindWad(source, sourceLayout);
        DiscFileRecord candidateWad = FindWad(candidate, candidateLayout);
        DiscFileRecord sourceExecutable = FindExecutable(source, sourceLayout);
        DiscFileRecord candidateExecutable = FindExecutable(candidate, candidateLayout);
        if (sourceWad != candidateWad || sourceExecutable != candidateExecutable ||
            sourceWad.Lba != WadLba || sourceWad.Size != ExpectedWadByteLength ||
            sourceExecutable.Lba != ExecutableLba ||
            sourceExecutable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "The aggregate candidate moved or resized WAD.WAD or SCUS_942.28.");
        }

        byte[] sourceRow = DiscImage.ReadFileBytes(
            source,
            sourceLayout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        byte[] outputRow = DiscImage.ReadFileBytes(
            candidate,
            candidateLayout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        if (Hash(sourceRow) != plan.SourceRow80Sha256 ||
            Hash(outputRow) != plan.OutputRow80Sha256)
            throw new InvalidDataException("The aggregate candidate row-80 readback changed.");
        byte[] restored = Id65AuthoringEditorAggregatePlanProjector.ApplyExact(
            plan,
            outputRow,
            reverse: true);
        if (!restored.SequenceEqual(sourceRow))
            throw new InvalidDataException("The aggregate candidate inverse readback changed.");
        foreach (Id65AuthoringEditorAggregateProtectedRange range in plan.ProtectedRanges)
        {
            if (!sourceRow.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(outputRow.AsSpan(range.DataRelativeOffset, range.ByteLength)) ||
                Hash(sourceRow.AsSpan(range.DataRelativeOffset, range.ByteLength)) !=
                    range.SegmentSha256)
            {
                throw new InvalidDataException(
                    "The aggregate candidate changed its protected row-80 complement.");
            }
        }

        RequireHash(
            Hash(DiscImage.ReadFileBytes(candidate, candidateLayout, WadLba, 80 * 8L, 8)),
            plan.Destination.TargetDataDirectoryRowSha256,
            "candidate WAD80 directory row");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(candidate, candidateLayout, WadLba, 16 * 8L, 8)),
            plan.Destination.RejectedRetailDataDirectoryRowSha256,
            "candidate retail WAD16 directory row");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.RejectedRetailDataWadOffset,
            TargetDataByteLength,
            "retail Town Square WAD16");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.RejectedRetailOverlayWadOffset,
            plan.Destination.TargetOverlayByteLength,
            "retail Town Square WAD15 overlay");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.TargetOverlayWadOffset,
            plan.Destination.TargetOverlayByteLength,
            "ID65 WAD79 overlay");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            sourceExecutable.Lba,
            0,
            sourceExecutable.Size,
            "SCUS_942.28 executable");

        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            candidate,
            candidateLayout,
            [(FirstRebuiltRawSectorLba, RebuiltRawSectorCount)]);
        if (verified != RebuiltRawSectorCount)
            throw new InvalidDataException(
                "The aggregate candidate final raw-sector verification count changed.");
        for (int lba = FirstRebuiltRawSectorLba; lba <= LastRebuiltRawSectorLba; lba++)
        {
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                candidate,
                candidateLayout,
                lba,
                lba == LastRebuiltRawSectorLba ? XaFinalDataSubmode : XaDataSubmode);
        }

        PhysicalDiff physical = await ComparePhysicalImagesAsync(
            source,
            candidate,
            plan,
            cancellationToken);
        string outputImageSha256 = await HashFileAsync(candidateImagePath, cancellationToken);
        if (outputImageSha256 != ExpectedOutputImageSha256 ||
            physical.ChangedPhysicalBytes != ExpectedChangedPhysicalImageBytes ||
            physical.ChangedRawSectorCount != ExpectedChangedRawSectorCount ||
            physical.DiffSha256 != ExpectedPhysicalDiffSha256)
        {
            throw new InvalidDataException(
                "The aggregate candidate frozen whole-image or physical-diff identity changed.");
        }
        return new(
            outputImageSha256,
            physical.ChangedPhysicalBytes,
            physical.ChangedRawSectorCount,
            physical.DiffSha256);
    }

    private static async Task<PhysicalDiff> ComparePhysicalImagesAsync(
        FileStream source,
        FileStream candidate,
        Id65AuthoringEditorAggregatePlan plan,
        CancellationToken cancellationToken)
    {
        bool[] ownedMask = new bool[TargetDataByteLength];
        foreach (Id65AuthoringEditorAggregateOwnedRange range in plan.OwnedRanges)
            Array.Fill(ownedMask, true, range.DataRelativeOffset, range.ByteLength);

        source.Position = 0;
        candidate.Position = 0;
        byte[] before = new byte[RawSectorByteLength];
        byte[] after = new byte[RawSectorByteLength];
        long changedBytes = 0;
        long changedLogicalBytes = 0;
        int changedSectors = 0;
        using IncrementalHash diffHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] diffIdentity = new byte[10];
        long sectorCount = source.Length / RawSectorByteLength;
        for (int lba = 0; lba < sectorCount; lba++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await source.ReadExactlyAsync(before, cancellationToken);
            await candidate.ReadExactlyAsync(after, cancellationToken);
            if (before.AsSpan().SequenceEqual(after))
                continue;
            changedSectors++;
            if (lba < FirstRebuiltRawSectorLba || lba > LastRebuiltRawSectorLba)
                throw new InvalidDataException(
                    $"The aggregate candidate changed raw sector {lba} outside WAD80.");
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                changedBytes++;
                if (index < UserDataOffset)
                    throw new InvalidDataException(
                        $"The aggregate candidate changed raw-sector header/subheader byte {lba}:{index}.");
                if (index < UserDataOffset + LogicalSectorByteLength)
                {
                    changedLogicalBytes++;
                    long wadOffset = ((long)lba - WadLba) * LogicalSectorByteLength +
                        (index - UserDataOffset);
                    long rowRelative = wadOffset - TargetDataWadOffset;
                    if (rowRelative < 0 || rowRelative >= ownedMask.Length ||
                        !ownedMask[checked((int)rowRelative)])
                    {
                        throw new InvalidDataException(
                            $"The aggregate candidate changed an unowned logical byte at WAD+0x{wadOffset:X}.");
                    }
                }
                BinaryPrimitives.WriteInt32LittleEndian(diffIdentity.AsSpan(0, 4), lba);
                BinaryPrimitives.WriteInt32LittleEndian(diffIdentity.AsSpan(4, 4), index);
                diffIdentity[8] = before[index];
                diffIdentity[9] = after[index];
                diffHash.AppendData(diffIdentity);
            }
        }
        if (source.Position != source.Length || candidate.Position != candidate.Length ||
            changedBytes <= 0 || changedSectors <= 0 ||
            changedLogicalBytes != plan.ChangedByteCount)
        {
            throw new InvalidDataException("The aggregate candidate physical diff is incomplete or empty.");
        }
        return new(
            changedBytes,
            changedSectors,
            Convert.ToHexStringLower(diffHash.GetHashAndReset()));
    }

    private static Id65AuthoringEditorAggregateCandidateReceipt BuildReceipt(
        Id65AuthoringEditorAggregateCandidateMaterializationRequest request,
        ProjectionCapture projection,
        Readback readback)
    {
        Id65AuthoringEditorAggregateCandidateReceipt receipt = new(
            Format,
            ReceiptSchemaVersion,
            ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.Key,
            UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion,
            request.ExpectedManifest.LockedBaseProfileId,
            projection.SourceImageSha256,
            projection.Stored.EnvelopeCanonicalSha256,
            projection.Stored.DocumentCanonicalSha256,
            projection.Plan.DeterministicAggregatePlanSha256,
            projection.Plan.CompositeManifestSha256,
            projection.Plan.OutputRow80Sha256,
            readback.OutputImageSha256,
            CandidateImageFileName,
            CandidateCueFileName,
            ExpectedCandidateCueSha256,
            TargetDataWadEntry,
            TargetDataWadOffset,
            TargetDataByteLength,
            FirstRebuiltRawSectorLba,
            RebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            projection.Plan.ChangedByteCount,
            readback.ChangedPhysicalBytes,
            readback.PhysicalDiffSha256,
            ExactStoreReloadVerified: true,
            ExactLockedBaseRecaptured: true,
            ExactRow80ReadbackVerified: true,
            ExactInverseReadbackVerified: true,
            ProtectedComplementVerified: true,
            RetailTownSquarePreserved: true,
            TargetOverlayPreserved: true,
            ExecutablePreserved: true,
            RawSectorIntegrityVerified: true,
            PhysicalComplementVerified: true,
            SourceLockedBasePreserved: true,
            AtomicNoReplaceRenameCompleted: true,
            ContainsFullDiscImage: true,
            ContainsCue: true,
            WorkspaceCandidateWriterAuthorized: true,
            RuntimeCandidateAuthorized: false,
            RuntimeProven: false,
            SaveAuthorized: false,
            SavePersistenceProven: false,
            TreasureTotal200Proven: false,
            MusicLongPlayProven: false,
            ExitDestination65Proven: false,
            AppIntegrated: false,
            UiIntegrated: false,
            CreateBinEnabled: false,
            NormalCreateBinEnabled: false,
            RetailLevelMutationAuthorized: false,
            ReleaseIntegrated: false,
            ReleaseAuthorized: false,
            PromotionAuthorized: false,
            Publishable: false,
            CurrentOnDiskFreshnessVerified: false,
            HostileFilesystemRaceSafe: false,
            RequiresTrustedExclusiveWorkspaceDirectory: true,
            CommonWorkspaceLeaseOwned: false,
            ReceiptContentSha256: "");
        Id65AuthoringEditorAggregateCandidateReceipt finalized = receipt with
        {
            ReceiptContentSha256 = ComputeReceiptContentSha256(receipt)
        };
        if (finalized.ReceiptContentSha256 != ExpectedReceiptContentSha256)
            throw new InvalidDataException(
                $"The aggregate candidate frozen receipt identity changed to " +
                $"{finalized.ReceiptContentSha256}, expected {ExpectedReceiptContentSha256}.");
        return finalized;
    }

    private static Id65AuthoringEditorAggregateCandidateMaterializationResult BuildResult(
        Id65AuthoringEditorAggregateCandidatePaths paths,
        Id65AuthoringEditorAggregateCandidateReceipt receipt,
        Id65AuthoringEditorAggregatePlan plan,
        bool reusedExistingCandidate,
        bool candidateArtifactsWrittenThisCall,
        bool fileSystemMutationPerformed,
        bool atomicNoReplaceRenameCompletedThisCall,
        bool verifiedAtomicNoReplacePublicationProvenance,
        bool residueRemoved) =>
        new(
            paths,
            receipt,
            plan,
            reusedExistingCandidate,
            candidateArtifactsWrittenThisCall,
            fileSystemMutationPerformed,
            WritesDiscImage: candidateArtifactsWrittenThisCall,
            WritesBin: candidateArtifactsWrittenThisCall,
            WritesCue: candidateArtifactsWrittenThisCall,
            AtomicNoReplaceRenameCompletedThisCall: atomicNoReplaceRenameCompletedThisCall,
            VerifiedAtomicNoReplacePublicationProvenance:
                verifiedAtomicNoReplacePublicationProvenance,
            OwnedTemporaryResidueRemoved: residueRemoved,
            FinderHandoffPerformed: false,
            AppIntegrated: false,
            UiIntegrated: false,
            CreateBinEnabled: false,
            NormalCreateBinEnabled: false,
            RuntimeCandidateAuthorized: false,
            RuntimeProven: false,
            ReleaseIntegrated: false,
            PromotionAuthorized: false,
            Publishable: false,
            CurrentOnDiskFreshnessVerified: false,
            HostileFilesystemRaceSafe: false,
            RequiresTrustedExclusiveWorkspaceDirectory: true,
            CommonWorkspaceLeaseOwned: false);

    private static async Task RequireExactPublishedCandidateAsync(
        Id65AuthoringEditorAggregateCandidatePaths paths,
        Id65AuthoringEditorAggregateCandidateReceipt expectedReceipt,
        CancellationToken cancellationToken)
    {
        await RequireExactStagedCandidateAsync(
            paths.CandidateDirectoryPath,
            paths.CandidateImagePath,
            paths.CandidateCuePath,
            paths.CandidateReceiptPath,
            BuildCanonicalCandidateCue(),
            expectedReceipt,
            cancellationToken);
    }

    private static async Task RequireExactStagedCandidateAsync(
        string directory,
        string imagePath,
        string cuePath,
        string receiptPath,
        string expectedCue,
        Id65AuthoringEditorAggregateCandidateReceipt expectedReceipt,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(directory, "aggregate candidate directory");
        RefuseReparsePoint(imagePath, "aggregate candidate image");
        RefuseReparsePoint(cuePath, "aggregate candidate cue");
        RefuseReparsePoint(receiptPath, "aggregate candidate receipt");
        string[] names = Directory.EnumerateFileSystemEntries(directory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        string[] expectedNames =
        [
            CandidateCueFileName,
            CandidateImageFileName,
            CandidateReceiptFileName
        ];
        Array.Sort(expectedNames, StringComparer.Ordinal);
        if (!names.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new InvalidDataException(
                "The aggregate candidate directory contains an unexpected entry set.");
        string actualCue = await File.ReadAllTextAsync(cuePath, Encoding.ASCII, cancellationToken);
        if (actualCue != expectedCue ||
            Hash(Encoding.ASCII.GetBytes(actualCue)) != expectedReceipt.CandidateCueSha256 ||
            expectedReceipt.CandidateCueSha256 != ExpectedCandidateCueSha256)
            throw new InvalidDataException("The aggregate candidate CUE readback changed.");
        byte[] receiptBytes = await ReadBoundedFileAsync(
            receiptPath,
            MaximumReceiptByteCount,
            cancellationToken);
        Id65AuthoringEditorAggregateCandidateReceipt actualReceipt;
        try
        {
            actualReceipt =
                JsonSerializer.Deserialize<Id65AuthoringEditorAggregateCandidateReceipt>(
                    receiptBytes,
                    ReceiptJsonOptions) ?? throw new InvalidDataException(
                        "The aggregate candidate receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The aggregate candidate receipt is not valid canonical JSON.",
                exception);
        }
        if (actualReceipt != expectedReceipt ||
            !receiptBytes.SequenceEqual(SerializeReceipt(actualReceipt)) ||
            actualReceipt.ReceiptContentSha256 != ComputeReceiptContentSha256(actualReceipt))
        {
            throw new InvalidDataException(
                "The aggregate candidate receipt failed exact canonical readback.");
        }
        string imageHash = await HashFileAsync(imagePath, cancellationToken);
        if (imageHash != expectedReceipt.OutputImageSha256)
            throw new InvalidDataException("The aggregate candidate image hash changed after readback.");
    }

    private static byte[] SerializeReceipt(
        Id65AuthoringEditorAggregateCandidateReceipt receipt) =>
        Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + "\n");

    private static string BuildCanonicalCandidateCue() =>
        $"FILE \"{CandidateImageFileName}\" BINARY\n" +
        "  TRACK 01 MODE2/2352\n" +
        "    INDEX 01 00:00:00\n";

    private static string ComputeReceiptContentSha256(
        Id65AuthoringEditorAggregateCandidateReceipt receipt)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            receipt with { ReceiptContentSha256 = "" },
            CompactJsonOptions);
        return Hash(bytes);
    }

    private static async Task WriteNewFileDurablyAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > maximumBytes)
            throw new InvalidDataException("The aggregate candidate receipt has an invalid byte length.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("The aggregate candidate receipt readback was incomplete.");
        return bytes;
    }

    private static RecoveryResult RecoverOwnedOperations(
        Id65AuthoringEditorAggregateCandidatePaths paths)
    {
        RefuseReparsePoint(paths.OperationsRootPath, "aggregate operation root");
        bool mutationPerformed = false;
        foreach (string entry in Directory.EnumerateFileSystemEntries(paths.OperationsRootPath)
                     .Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(entry);
            if (!Directory.Exists(entry) || !IsExactOperationName(name))
                throw new InvalidDataException(
                    $"The aggregate operation root contains unknown reserved entry '{name}'.");
            RefuseReparsePoint(entry, "stale aggregate materialization operation");
            string journalPath = Path.Combine(entry, OperationJournalFileName);
            string temporaryJournalPath = Path.Combine(entry, OperationJournalTemporaryFileName);
            string leasePath = Path.Combine(entry, OperationLeaseFileName);
            string staged = Path.Combine(entry, StagedCandidateDirectoryName);
            string[] allowedOperationEntries =
            [
                OperationJournalFileName,
                OperationJournalTemporaryFileName,
                OperationLeaseFileName,
                StagedCandidateDirectoryName
            ];
            string[] actualOperationEntries = Directory.EnumerateFileSystemEntries(entry)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray()!;
            if (actualOperationEntries.Any(name =>
                    !allowedOperationEntries.Contains(name, StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    "A stale aggregate operation contains an unknown entry and was preserved.");
            }

            RequireRegularFileOrMissing(journalPath, "stale aggregate operation journal");
            RequireRegularFileOrMissing(
                temporaryJournalPath,
                "stale aggregate temporary operation journal");
            RequireRegularFileOrMissing(leasePath, "stale aggregate operation lease");
            RequireDirectoryOrMissing(staged, "stale aggregate staged candidate");
            if (File.Exists(leasePath))
            {
                try
                {
                    using FileStream staleLease = new(
                        leasePath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidDataException(
                        "An active aggregate materialization operation blocks recovery.",
                        exception);
                }
            }
            if (File.Exists(journalPath))
            {
                OperationJournal journal = ReadOperationJournal(entry);
                if (journal.CandidateDirectoryName != CreateCandidateDirectoryName(
                        journal.AggregatePlanSha256,
                        journal.CandidateReceiptContentSha256) ||
                    journal.Phase is not OperationPhaseStaging and not OperationPhaseVerifiedReady)
                {
                    throw new InvalidDataException(
                        "A stale aggregate materialization journal has a divergent candidate identity.");
                }
            }
            if (Directory.Exists(staged))
            {
                string[] allowedStagedEntries =
                [
                    CandidateImageFileName,
                    CandidateCueFileName,
                    CandidateReceiptFileName
                ];
                foreach (string stagedEntry in Directory.EnumerateFileSystemEntries(staged))
                {
                    string stagedName = Path.GetFileName(stagedEntry);
                    if (!allowedStagedEntries.Contains(stagedName, StringComparer.Ordinal) ||
                        Directory.Exists(stagedEntry) || !File.Exists(stagedEntry))
                    {
                        throw new InvalidDataException(
                            "A stale aggregate staged candidate contains an unknown entry and was preserved.");
                    }
                    RefuseReparsePoint(stagedEntry, "stale aggregate staged candidate file");
                }
            }
            try
            {
                Directory.Delete(entry, recursive: true);
                mutationPerformed = true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException(
                    "Owned aggregate operation residue could not be recovered under the writer lease.",
                    exception);
            }
        }
        return new(AllOwnedResidueRemoved: true, MutationPerformed: mutationPerformed);
    }

    private static void WriteOperationJournal(
        string operationRoot,
        string aggregatePlanSha256,
        string phase)
    {
        if (phase is not OperationPhaseStaging and not OperationPhaseVerifiedReady)
            throw new ArgumentOutOfRangeException(nameof(phase));
        OperationJournal journal = new(
            OperationJournalSchemaVersion,
            OperationKind,
            aggregatePlanSha256,
            ExpectedReceiptContentSha256,
            CreateCandidateDirectoryName(aggregatePlanSha256, ExpectedReceiptContentSha256),
            phase);
        string path = Path.Combine(operationRoot, OperationJournalFileName);
        string temporary = Path.Combine(operationRoot, OperationJournalTemporaryFileName);
        try
        {
            RefuseReparsePoint(temporary, "aggregate temporary operation journal");
            byte[] bytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(journal, ReceiptJsonOptions) + "\n");
            using (FileStream stream = new(
                       temporary,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
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

    private static OperationJournal ReadOperationJournal(string operationRoot)
    {
        string path = Path.Combine(operationRoot, OperationJournalFileName);
        RefuseReparsePoint(path, "aggregate operation journal");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length <= 0 || bytes.Length > MaximumReceiptByteCount)
            throw new InvalidDataException("The aggregate operation journal has an invalid length.");
        OperationJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<OperationJournal>(
                bytes,
                ReceiptJsonOptions) ?? throw new InvalidDataException(
                    "The aggregate operation journal is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The aggregate operation journal is not valid canonical JSON.",
                exception);
        }
        byte[] canonical = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(journal, ReceiptJsonOptions) + "\n");
        if (!bytes.SequenceEqual(canonical) ||
            journal.SchemaVersion != OperationJournalSchemaVersion ||
            journal.OperationKind != OperationKind ||
            !IsCanonicalSha256(journal.AggregatePlanSha256) ||
            !IsCanonicalSha256(journal.CandidateReceiptContentSha256) ||
            journal.CandidateDirectoryName != CreateCandidateDirectoryName(
                journal.AggregatePlanSha256,
                journal.CandidateReceiptContentSha256) ||
            journal.Phase is not OperationPhaseStaging and not OperationPhaseVerifiedReady)
        {
            throw new InvalidDataException(
                "The aggregate operation journal failed exact canonical validation.");
        }
        return journal;
    }

    private static void BestEffortDeleteOwnedOperation(
        string operationRoot,
        string operationsRoot)
    {
        try
        {
            RequireExactOperationPath(operationRoot, operationsRoot);
            if (Directory.Exists(operationRoot))
                Directory.Delete(operationRoot, recursive: true);
        }
        catch
        {
            // A successful no-replace publication remains authoritative. Exact
            // owned residue is recovered under the persistent writer lease.
        }
    }

    private static void RequireExactOperationPath(string operationRoot, string operationsRoot)
    {
        string full = Path.GetFullPath(operationRoot);
        string parent = Path.GetDirectoryName(full) ?? "";
        if (!PathsEqual(parent, operationsRoot) || !IsExactOperationName(Path.GetFileName(full)))
            throw new InvalidDataException("An aggregate operation path escaped its exact owned root.");
    }

    private static bool IsExactOperationName(string name)
    {
        if (!name.StartsWith(OperationPrefix, StringComparison.Ordinal) ||
            name.Length != OperationPrefix.Length + 32)
            return false;
        return name.AsSpan(OperationPrefix.Length).ToArray().All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void RefuseReparsePoint(string path, string context)
    {
        FileSystemInfo? entry = FindExistingEntry(path);
        if (entry is null)
            return;
        string? linkTarget;
        FileAttributes attributes;
        try
        {
            linkTarget = entry.LinkTarget;
            attributes = entry.Attributes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"The {context} could not be inspected safely.", exception);
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            !string.IsNullOrWhiteSpace(linkTarget))
        {
            throw new InvalidDataException($"The {context} is a symbolic link or reparse point.");
        }
    }

    private static FileSystemInfo? FindExistingEntry(string path)
    {
        DirectoryInfo directory = new(path);
        try
        {
            if (directory.Exists || !string.IsNullOrWhiteSpace(directory.LinkTarget))
                return directory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"A directory entry could not be inspected safely: {path}",
                exception);
        }
        FileInfo file = new(path);
        try
        {
            if (file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget))
                return file;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"A file entry could not be inspected safely: {path}",
                exception);
        }
        return null;
    }

    private static void RequireRegularFileOrMissing(string path, string context)
    {
        if (FindExistingEntry(path) is null)
            return;
        RefuseReparsePoint(path, context);
        if (!File.Exists(path) || Directory.Exists(path))
            throw new InvalidDataException($"The {context} is not a regular file.");
    }

    private static void RequireDirectoryOrMissing(string path, string context)
    {
        if (FindExistingEntry(path) is null)
            return;
        RefuseReparsePoint(path, context);
        if (!Directory.Exists(path) || File.Exists(path))
            throw new InvalidDataException($"The {context} is not a directory.");
    }

    private static void PublishDirectoryAtomicallyNoReplace(
        string sourceDirectory,
        string destinationDirectory,
        Action onRenamed)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException(
                "Anchored aggregate candidate directory publication requires macOS.");
        string source = Path.GetFullPath(sourceDirectory);
        string destination = Path.GetFullPath(destinationDirectory);
        string sourceParent = Path.GetDirectoryName(source) ??
            throw new InvalidDataException("The aggregate staged directory has no parent.");
        string destinationParent = Path.GetDirectoryName(destination) ??
            throw new InvalidDataException("The aggregate candidate directory has no parent.");
        string sourceLeaf = Path.GetFileName(source);
        string destinationLeaf = Path.GetFileName(destination);
        if (string.IsNullOrWhiteSpace(sourceLeaf) || string.IsNullOrWhiteSpace(destinationLeaf) ||
            PathsEqual(source, destination))
        {
            throw new InvalidDataException("The aggregate candidate publication roles alias or lack a leaf.");
        }
        RefuseReparsePoint(sourceParent, "aggregate publication source parent");
        RefuseReparsePoint(destinationParent, "aggregate publication destination parent");
        RefuseReparsePoint(source, "aggregate staged publication directory");
        if (FindExistingEntry(destination) is not null)
        {
            RefuseReparsePoint(destination, "aggregate candidate publication destination");
            throw new IOException(
                "The content-addressed aggregate candidate appeared before exclusive publication.");
        }

        int sourceParentDescriptor = OpenDarwin(
            sourceParent,
            DarwinOpenNoFollow | DarwinOpenDirectory | DarwinOpenCloseOnExec,
            0);
        if (sourceParentDescriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException(
                $"Could not open the aggregate source parent directory (errno {error}).");
        }
        int destinationParentDescriptor = -1;
        try
        {
            destinationParentDescriptor = OpenDarwin(
                destinationParent,
                DarwinOpenNoFollow | DarwinOpenDirectory | DarwinOpenCloseOnExec,
                0);
            if (destinationParentDescriptor < 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Could not open the aggregate destination parent directory (errno {error}).");
            }
            DarwinIdentity sourceParentIdentity = RequireAnchoredDarwinDirectory(
                sourceParent,
                sourceParentDescriptor,
                "aggregate publication source parent");
            DarwinIdentity destinationParentIdentity = RequireAnchoredDarwinDirectory(
                destinationParent,
                destinationParentDescriptor,
                "aggregate publication destination parent");
            if (!TryReadDarwinAtIdentity(
                    sourceParentDescriptor,
                    sourceLeaf,
                    out DarwinIdentity? stagedIdentity,
                    out int stagedError))
            {
                throw new IOException(
                    $"The staged aggregate directory vanished before publication (errno {stagedError}).");
            }
            DarwinIdentity staged = stagedIdentity!;
            RequireTrustedDarwinDirectory(staged, "aggregate staged publication directory");
            int stagedDirectoryDescriptor = OpenDarwin(
                source,
                DarwinOpenNoFollow | DarwinOpenDirectory | DarwinOpenCloseOnExec,
                0);
            if (stagedDirectoryDescriptor < 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Could not open the staged aggregate directory for fsync (errno {error}).");
            }
            try
            {
                DarwinIdentity stagedHandle = ReadDarwinDescriptorIdentity(
                    stagedDirectoryDescriptor,
                    "staged aggregate directory handle");
                RequireTrustedDarwinDirectory(stagedHandle, "staged aggregate directory handle");
                if (!staged.SameFile(stagedHandle))
                    throw new InvalidDataException(
                        "The staged aggregate directory changed before its durable fsync.");
                FsyncDarwinDirectory(
                    stagedDirectoryDescriptor,
                    "staged aggregate candidate directory");
            }
            finally
            {
                _ = CloseDarwin(stagedDirectoryDescriptor);
            }
            if (!TryReadDarwinAtIdentity(
                    sourceParentDescriptor,
                    sourceLeaf,
                    out DarwinIdentity? stagedAfterFsync,
                    out int stagedAfterFsyncError) ||
                !staged.SameFile(stagedAfterFsync!))
            {
                throw new IOException(
                    "The staged aggregate directory identity changed after fsync " +
                    $"(errno {stagedAfterFsyncError}).");
            }
            if (TryReadDarwinAtIdentity(
                    destinationParentDescriptor,
                    destinationLeaf,
                    out _,
                    out int destinationError) || destinationError != DarwinNoEntry)
            {
                throw new IOException(
                    "The aggregate candidate destination exists or could not be proven absent " +
                    $"(errno {destinationError}).");
            }
            RequireDarwinParentStillAnchored(
                sourceParent,
                sourceParentDescriptor,
                sourceParentIdentity,
                "aggregate publication source parent");
            RequireDarwinParentStillAnchored(
                destinationParent,
                destinationParentDescriptor,
                destinationParentIdentity,
                "aggregate publication destination parent");
            if (RenameAtExclusiveDarwin(
                    sourceParentDescriptor,
                    sourceLeaf,
                    destinationParentDescriptor,
                    destinationLeaf,
                    DarwinRenameExclusive) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Atomic exclusive aggregate candidate publication failed (errno {error}).");
            }
            onRenamed();
            if (!TryReadDarwinAtIdentity(
                    destinationParentDescriptor,
                    destinationLeaf,
                    out DarwinIdentity? publishedIdentity,
                    out int publishedError) ||
                !staged.SameFile(publishedIdentity!))
            {
                throw new IOException(
                    "The aggregate candidate destination did not retain the staged directory inode " +
                    $"after rename (errno {publishedError}).");
            }
            RequireTrustedDarwinDirectory(
                publishedIdentity!,
                "published aggregate candidate directory");
            if (TryReadDarwinAtIdentity(
                    sourceParentDescriptor,
                    sourceLeaf,
                    out _,
                    out int sourceAfterError) || sourceAfterError != DarwinNoEntry)
            {
                throw new IOException(
                    "The staged aggregate directory still exists after publication " +
                    $"(errno {sourceAfterError}).");
            }
            FsyncDarwinDirectory(
                destinationParentDescriptor,
                "aggregate candidate destination parent");
            if (!sourceParentIdentity.SameFile(destinationParentIdentity))
            {
                FsyncDarwinDirectory(
                    sourceParentDescriptor,
                    "aggregate candidate source parent");
            }
            RequireDarwinParentStillAnchored(
                sourceParent,
                sourceParentDescriptor,
                sourceParentIdentity,
                "aggregate publication source parent after fsync");
            RequireDarwinParentStillAnchored(
                destinationParent,
                destinationParentDescriptor,
                destinationParentIdentity,
                "aggregate publication destination parent after fsync");
        }
        finally
        {
            if (destinationParentDescriptor >= 0)
                _ = CloseDarwin(destinationParentDescriptor);
            _ = CloseDarwin(sourceParentDescriptor);
        }
    }

    private static DarwinIdentity RequireAnchoredDarwinDirectory(
        string path,
        int descriptor,
        string context)
    {
        DarwinIdentity handle = ReadDarwinDescriptorIdentity(descriptor, context + " handle");
        DarwinIdentity pathIdentity = ReadDarwinPathIdentity(path, context + " path");
        RequireTrustedDarwinDirectory(handle, context + " handle");
        RequireTrustedDarwinDirectory(pathIdentity, context + " path");
        if (!handle.SameFile(pathIdentity))
            throw new InvalidDataException($"The {context} changed between path and handle validation.");
        return handle;
    }

    private static void RequireDarwinParentStillAnchored(
        string path,
        int descriptor,
        DarwinIdentity expected,
        string context)
    {
        DarwinIdentity actual = RequireAnchoredDarwinDirectory(path, descriptor, context);
        if (!expected.SameFile(actual))
            throw new InvalidDataException($"The {context} directory identity changed.");
    }

    private static void RequireTrustedDarwinDirectory(DarwinIdentity identity, string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUidDarwin() ||
            (identity.Mode & DarwinGroupOrOtherWrite) != 0)
        {
            throw new InvalidDataException(
                $"The {context} must be a current-user directory without group/other write permission.");
        }
    }

    private static DarwinIdentity ReadDarwinPathIdentity(string path, string context)
    {
        if (LStatDarwin(path, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not lstat the {context} (errno {error}).");
        }
        return ToDarwinIdentity(status);
    }

    private static DarwinIdentity ReadDarwinDescriptorIdentity(int descriptor, string context)
    {
        if (FStatDarwin(descriptor, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fstat the {context} (errno {error}).");
        }
        return ToDarwinIdentity(status);
    }

    private static bool TryReadDarwinAtIdentity(
        int descriptor,
        string leaf,
        out DarwinIdentity? identity,
        out int error)
    {
        if (FStatAtDarwin(descriptor, leaf, out DarwinStat status, DarwinAtSymlinkNoFollow) == 0)
        {
            identity = ToDarwinIdentity(status);
            error = 0;
            return true;
        }
        identity = null;
        error = Marshal.GetLastPInvokeError();
        return false;
    }

    private static DarwinIdentity ToDarwinIdentity(DarwinStat status) =>
        new(status.Device, status.Inode, status.Mode, status.UserId);

    private static void FsyncDarwinDirectory(int descriptor, string context)
    {
        if (FsyncDarwin(descriptor) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fsync the {context} (errno {error}).");
        }
    }

    private static DiscFileRecord FindWad(FileStream stream, DiscLayout layout) =>
        DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));

    private static DiscFileRecord FindExecutable(FileStream stream, DiscLayout layout) =>
        DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => name.StartsWith("SCUS_942.28", StringComparison.OrdinalIgnoreCase));

    private static void RequireEqualComponent(
        FileStream source,
        FileStream candidate,
        DiscLayout layout,
        int fileLba,
        long offset,
        int byteLength,
        string label)
    {
        byte[] before = DiscImage.ReadFileBytes(source, layout, fileLba, offset, byteLength);
        byte[] after = DiscImage.ReadFileBytes(candidate, layout, fileLba, offset, byteLength);
        if (!before.SequenceEqual(after))
            throw new InvalidDataException($"The aggregate candidate changed {label}.");
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void RequireHash(string actual, string expected, string label)
    {
        if (actual != expected)
            throw new InvalidDataException(
                $"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void ThrowIfFault(
        Id65AuthoringEditorAggregateCandidateFaultPoint selected,
        Id65AuthoringEditorAggregateCandidateFaultPoint current)
    {
        if (selected == current)
            throw new InvalidOperationException(
                $"Injected aggregate candidate materialization fault at {current}.");
    }

    private sealed record ProjectionCapture(
        Id65AuthoringEditorDocumentStoreResult Stored,
        Id65AuthoringEditorAggregatePlan Plan,
        string SourceImageSha256);

    private sealed record Readback(
        string OutputImageSha256,
        long ChangedPhysicalBytes,
        int ChangedRawSectorCount,
        string PhysicalDiffSha256);

    private sealed record PhysicalDiff(
        long ChangedPhysicalBytes,
        int ChangedRawSectorCount,
        string DiffSha256);

    private sealed record OperationJournal(
        int SchemaVersion,
        string OperationKind,
        string AggregatePlanSha256,
        string CandidateReceiptContentSha256,
        string CandidateDirectoryName,
        string Phase);

    private sealed record RecoveryResult(
        bool AllOwnedResidueRemoved,
        bool MutationPerformed);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)] internal int Device;
        [FieldOffset(4)] internal ushort Mode;
        [FieldOffset(8)] internal ulong Inode;
        [FieldOffset(16)] internal uint UserId;
    }

    private sealed record DarwinIdentity(
        int Device,
        ulong Inode,
        ushort Mode,
        uint UserId)
    {
        internal bool SameFile(DarwinIdentity other) =>
            Device == other.Device && Inode == other.Inode;
    }

    private sealed class WriterLease(FileStream stream, bool createdFile) : IDisposable
    {
        private FileStream? _stream = stream;

        internal bool CreatedFile { get; } = createdFile;

        public void Dispose()
        {
            FileStream? owned = Interlocked.Exchange(ref _stream, null);
            if (owned is null)
                return;
            try
            {
                owned.Dispose();
            }
            catch
            {
                // A completed atomic no-replace rename must not become an
                // ambiguous failure because an advisory lease close reports late.
            }
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDarwin(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        int mode);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDarwin(int descriptor);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStatDarwin(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out DarwinStat status);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStatDarwin(int descriptor, out DarwinStat status);

    [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
    private static extern int FStatAtDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out DarwinStat status,
        int flags);

    [DllImport("libc", EntryPoint = "renameatx_np", SetLastError = true)]
    private static extern int RenameAtExclusiveDarwin(
        int sourceDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sourcePath,
        int destinationDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destinationPath,
        uint flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int FsyncDarwin(int descriptor);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUidDarwin();
}
