using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest(
    UnusedLevel65BlankLevelLabWorkspacePaths Paths,
    UnusedLevel65BlankLevelLabManifest ExpectedManifest,
    string ExpectedEnvelopeCanonicalSha256);

internal sealed record Id65AuthoringBlankCycloramaAggregateCandidatePaths(
    string CandidateRootPath,
    string OperationsRootPath,
    string WriterLeasePath,
    string CandidateDirectoryPath,
    string CandidateImagePath,
    string CandidateCuePath,
    string CandidateReceiptPath);

internal sealed record Id65AuthoringBlankCycloramaAggregateCandidateReceipt(
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
    string StaticAuthoringProfileId,
    string StaticAuthoringCatalogSha256,
    string StaticAuthoringSceneGraphSha256,
    string StaticAuthoringProjectionSha256,
    string StaticAuthoringTerrainManifestSha256,
    string StaticAuthoringCycloramaCatalogSha256,
    string TerrainManifestSha256,
    string ActiveTerrainUnitIdsCanonical,
    string MobyKindId,
    string NativeInputSetSha256,
    string AggregatePlanSha256,
    string CompilerProfileId,
    int CompilerSchemaVersion,
    string BlankCycloramaCatalogCanonicalSha256,
    string PresetId,
    string ColorHex,
    string PositionId,
    int PositionOrdinal,
    int EditorX,
    int EditorY,
    int EditorZ,
    int RawX,
    int RawY,
    int RawZ,
    string? SelectedExactRowSha256,
    string SupportHandle,
    int SupportCollisionTrueIndex,
    string BaselineDocumentCanonicalSha256,
    string BaselineOutputRow80Sha256,
    string BaselineOutputModelSha256,
    string BaselineDeterministicPlanSha256,
    int CycloramaModelOffset,
    int CycloramaRow80Offset,
    long CycloramaWadOffset,
    int CycloramaByteLength,
    int PaletteWordCount,
    int PaletteOwnedRangeCount,
    string BaselineCycloramaSha256,
    string OutputCycloramaSha256,
    string GeometrySha256,
    string NonPaletteSha256,
    string CommandByteSha256,
    int CycloramaChangedRgbByteCount,
    string CompilerOwnedRangeMapSha256,
    string CompilerTransactionSha256,
    string CompilerProtectedComplementSha256,
    string CompilerDeterministicPlanSha256,
    string AggregateOwnedRangeMapSha256,
    string AggregateInverseRangeMapSha256,
    string AggregateProtectedRangeMapSha256,
    string AggregateProtectedComplementSha256,
    string AggregateAtomicGroupMapSha256,
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
    bool ExactStoreReloadCasVerifiedThreeTimes,
    bool ExactStaticAuthoringProjectionReloadVerifiedThreeTimes,
    bool ExactNativeInputGenerationRecapturedThreeTimes,
    bool ExactV3TemplateRecapturedThreeTimes,
    bool ExactLockedBaseRecapturedThreeTimes,
    bool Schema6CompilerPinsVerified,
    bool BlankCycloramaIntentReadbackVerified,
    bool CycloramaLayoutReadbackVerified,
    bool EveryPaletteWordReadbackVerified,
    bool CycloramaGeometryReadbackVerified,
    bool CycloramaCommandBytesReadbackVerified,
    bool CycloramaNonPaletteReadbackVerified,
    bool MobyChoicePositionReadbackVerified,
    bool FourTerrainTopologiesVerified,
    bool T88ExcludedVerified,
    bool RepairedT13995SupportVerified,
    bool ExactRow80ReadbackVerified,
    bool ExactInverseReadbackVerified,
    bool ProtectedComplementVerified,
    bool Wad79Preserved,
    bool RetailTownSquareWad15Preserved,
    bool RetailTownSquareWad16Preserved,
    bool RetailArtisansWad9Preserved,
    bool RetailArtisansWad10Preserved,
    bool RetailTownSquareSkyBlockPreserved,
    bool RetailArtisansLinkedSkyBlockPreserved,
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

internal sealed record Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult(
    Id65AuthoringBlankCycloramaAggregateCandidatePaths Paths,
    Id65AuthoringBlankCycloramaAggregateCandidateReceipt Receipt,
    Id65AuthoringBlankCycloramaAggregatePlan Plan,
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

internal enum Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
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
internal static class Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
{
    public const string Format =
        "spyro-editor-id65-authoring-blank-cyclorama-schema6-aggregate-candidate-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string ProfileId =
        "id65-authoring-blank-cyclorama-schema6-aggregate-candidate-darwin-anchored-workspace-v1";
    public const string CandidateImageFileName =
        "unusedlevel65blank-authoring-blank-cyclorama-schema6-candidate-v1.bin";
    public const string CandidateCueFileName =
        "unusedlevel65blank-authoring-blank-cyclorama-schema6-candidate-v1.cue";
    public const string CandidateReceiptFileName =
        "unusedlevel65blank-authoring-blank-cyclorama-schema6-candidate-v1.receipt.json";
    public const string ExpectedCandidateCueSha256 =
        "193044755328535922271038375117b3602a5f8029860f1d4f8e5cad2edf7a10";
    public const int WadLba = 37;
    public const int ExpectedWadByteLength = 0x6C18800;
    public const int ExecutableLba = 55_382;
    public const int ExecutableByteLength = 0x66000;
    public const long ExpectedSourceImageByteLength = 661_547_040;
    public const int TargetDataWadEntry = 80;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const int FirstRebuiltRawSectorLba = 53_906;
    public const int RebuiltRawSectorCount = 1_476;
    public const int LastRebuiltRawSectorLba = 55_381;
    public const long RetailTownSquareSkyBlockWadOffset = 0x13080A8;
    public const long RetailArtisansLinkedSkyBlockWadOffset = 0x96A7EC;
    public const int RetailSkyBlockByteLength = 0x84E4;
    public const string ExpectedRetailSkyBlockSha256 =
        "8E8C62273AB0D4EA691A40409D5E4BE77FE578CFEBCFB2E3C6BB374CAD7D40BB";
    private const string CandidateRootDirectoryName =
        "materialized-blank-cyclorama-schema6-candidates";
    private const string OperationsRootDirectoryName =
        ".blank-cyclorama-schema6-candidate-operations";
    private const string WriterLeaseFileName =
        ".unusedlevel65blank-blank-cyclorama-schema6-candidate-v1.writer.lock";
    private const string OperationPrefix = "materialize-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string OperationJournalTemporaryFileName = "operation-journal.json.tmp";
    private const string OperationLeaseFileName = "operation.lease";
    private const string StagedCandidateDirectoryName = "candidate";
    private const string OperationKind =
        "id65-authoring-blank-cyclorama-schema6-aggregate-candidate-materialization";
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

    public static Task<Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult>
        MaterializeAsync(
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request,
            CancellationToken cancellationToken = default) =>
        MaterializeCoreAsync(
            request,
            Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.None,
            beforePrepublishSmokeHook: null,
            cancellationToken: cancellationToken);

    internal static Task<Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult>
        MaterializeForSmokeAsync(
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request,
            Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint faultPoint,
            Func<Task>? beforePrepublishSmokeHook = null,
            CancellationToken cancellationToken = default)
    {
        if (faultPoint == Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.None)
            throw new InvalidOperationException(
                "The smoke-only materializer entrypoint requires an injected failure point.");
        if (beforePrepublishSmokeHook is not null &&
            faultPoint != Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
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

    private static async Task<Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult>
        MaterializeCoreAsync(
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request,
            Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint faultPoint,
            Func<Task>? beforePrepublishSmokeHook,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.ExpectedManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExpectedEnvelopeCanonicalSha256);
        if (!IsCanonicalSha256(request.ExpectedEnvelopeCanonicalSha256))
            throw new InvalidDataException(
                "Schema-6 candidate materialization requires one exact expected envelope SHA-256 CAS identity.");
        if (!Enum.IsDefined(faultPoint))
            throw new ArgumentOutOfRangeException(nameof(faultPoint));
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException(
                "The isolated aggregate candidate materializer currently requires macOS anchored directory publication.");
        cancellationToken.ThrowIfCancellationRequested();

        ProjectionCapture preflight = await CaptureFreshProjectionAsync(
            request,
            cancellationToken);
        Id65AuthoringBlankCycloramaAggregateCandidatePaths paths = CreatePaths(
            request.Paths,
            preflight.Plan);
        bool fixedRootsMutated = PrepareFixedRoots(request.Paths, paths);

        using WriterLease writerLease = AcquireWriterLease(paths.WriterLeasePath);
        bool fixedBoundaryMutation = fixedRootsMutated || writerLease.CreatedFile;
        ThrowIfFault(faultPoint, Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterLeaseAcquired);

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
            Id65AuthoringBlankCycloramaAggregateCandidateReceipt expectedReceipt = BuildReceipt(
                request,
                leased,
                existing);
            await RequireExactPublishedCandidateAsync(
                paths,
                expectedReceipt,
                cancellationToken);
            RecoveryResult recovery = RecoverOwnedOperations(paths);
            ProjectionCapture reuseFinal = await CaptureFreshProjectionAsync(
                request,
                cancellationToken);
            RequireEquivalentProjection(
                leased,
                reuseFinal,
                "immediately before returning the immutable candidate");
            Readback reuseFinalReadback = await VerifyCandidateAsync(
                request.Paths.LockedBaseImagePath,
                paths.CandidateImagePath,
                reuseFinal.Plan,
                cancellationToken);
            Id65AuthoringBlankCycloramaAggregateCandidateReceipt
                reuseFinalReceipt = BuildReceipt(
                    request,
                    reuseFinal,
                    reuseFinalReadback);
            if (reuseFinalReceipt != expectedReceipt)
                throw new InvalidDataException(
                    "The immutable aggregate candidate identity changed during final reuse audit.");
            await RequireExactPublishedCandidateAsync(
                paths,
                reuseFinalReceipt,
                cancellationToken);
            return BuildResult(
                paths,
                reuseFinalReceipt,
                reuseFinal.Plan,
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
                leased.Plan.MobyPositionId,
                leased.Plan.DeterministicAggregatePlanSha256,
                leased.Plan.DeterministicAggregatePlanSha256,
                OperationPhaseStaging);

            await CopyExactLockedBaseAsync(
                request.Paths.LockedBaseImagePath,
                stagedImage,
                cancellationToken);
            ThrowIfFault(faultPoint, Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterSourceCopied);

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
            ThrowIfFault(faultPoint, Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterStagedReadback);

            Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt = BuildReceipt(
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

            if (faultPoint == Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
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
                Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit);
            Readback prepublishReadback = await VerifyCandidateAsync(
                request.Paths.LockedBaseImagePath,
                stagedImage,
                prepublish.Plan,
                cancellationToken);
            Id65AuthoringBlankCycloramaAggregateCandidateReceipt prepublishReceipt = BuildReceipt(
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
                leased.Plan.MobyPositionId,
                leased.Plan.DeterministicAggregatePlanSha256,
                receipt.ReceiptContentSha256,
                OperationPhaseVerifiedReady);
            ThrowIfFault(faultPoint, Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.BeforeAtomicRename);

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
                Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterAtomicRenameBeforeCleanup);
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
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request,
        CancellationToken cancellationToken)
    {
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(request.Paths);
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

        Id65AuthoringEditorDocumentProjectionInputs projectionInputs = new(null);
        Id65AuthoringEditorDocumentStoreResult stored =
            await Id65AuthoringEditorDocumentStore.LoadAsync(
                request.Paths,
                request.ExpectedManifest,
                projectionInputs,
                cancellationToken) ?? throw new InvalidDataException(
                    "The aggregate candidate requires one persisted full ID65 editor document.");
        RequireExpectedEnvelopeIdentity(
            request.ExpectedEnvelopeCanonicalSha256,
            stored);
        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                request.Paths,
                request.ExpectedManifest,
                cancellationToken);
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                .BuildStaticPlan(request.Paths.LockedBaseImagePath);
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(request.Paths);
        Id65AuthoringBlankCycloramaAggregatePlan plan =
            Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
                stored,
                request.ExpectedEnvelopeCanonicalSha256,
                request.ExpectedManifest,
                nativeInputs,
                v3Template));
        return new(stored, plan, nativeInputs.LockedBaseImageSha256);
    }

    private static void RequireExpectedEnvelopeIdentity(
        string expectedEnvelopeCanonicalSha256,
        Id65AuthoringEditorDocumentStoreResult actual)
    {
        if (!IsCanonicalSha256(expectedEnvelopeCanonicalSha256) ||
            actual.EnvelopeCanonicalSha256 != expectedEnvelopeCanonicalSha256 ||
            actual.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            actual.Document.ProfileId !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId ||
            !actual.Document.UsesRemotePadTerrainSet ||
            !actual.Document.UsesRemotePadMobyChoice ||
            !actual.Document.UsesBlankCyclorama ||
            actual.Document.BlankCyclorama !=
                Id65AuthoringBlankCycloramaCatalog.CreateBlankSlate())
        {
            throw new InvalidDataException(
                "The persisted schema-6 ID65 editor envelope changed from the exact caller CAS identity.");
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
            expected.Stored.StaticAuthoringProfileId !=
                actual.Stored.StaticAuthoringProfileId ||
            expected.Stored.StaticAuthoringCatalogSha256 !=
                actual.Stored.StaticAuthoringCatalogSha256 ||
            expected.Stored.StaticAuthoringSceneGraphSha256 !=
                actual.Stored.StaticAuthoringSceneGraphSha256 ||
            expected.Stored.StaticAuthoringProjectionSha256 !=
                actual.Stored.StaticAuthoringProjectionSha256 ||
            expected.Plan.EnvelopeCanonicalSha256 !=
                actual.Plan.EnvelopeCanonicalSha256 ||
            expected.Plan.DocumentCanonicalSha256 !=
                actual.Plan.DocumentCanonicalSha256 ||
            expected.Plan.StaticAuthoringProfileId !=
                actual.Plan.StaticAuthoringProfileId ||
            expected.Plan.StaticAuthoringCatalogSha256 !=
                actual.Plan.StaticAuthoringCatalogSha256 ||
            expected.Plan.StaticAuthoringSceneGraphSha256 !=
                actual.Plan.StaticAuthoringSceneGraphSha256 ||
            expected.Plan.StaticAuthoringProjectionSha256 !=
                actual.Plan.StaticAuthoringProjectionSha256 ||
            expected.Plan.StaticAuthoringTerrainManifestSha256 !=
                actual.Plan.StaticAuthoringTerrainManifestSha256 ||
            expected.Plan.StaticAuthoringCycloramaCatalogSha256 !=
                actual.Plan.StaticAuthoringCycloramaCatalogSha256 ||
            expected.Plan.TerrainManifestSha256 != actual.Plan.TerrainManifestSha256 ||
            !expected.Plan.ActiveTerrainUnitIds.SequenceEqual(
                actual.Plan.ActiveTerrainUnitIds,
                StringComparer.Ordinal) ||
            expected.Plan.MobyKind != actual.Plan.MobyKind ||
            expected.Plan.MobyPositionId != actual.Plan.MobyPositionId ||
            expected.Plan.NativeInputSetSha256 != actual.Plan.NativeInputSetSha256 ||
            expected.Plan.V3DeterministicPlanSha256 !=
                actual.Plan.V3DeterministicPlanSha256 ||
            expected.Plan.Destination != actual.Plan.Destination ||
            expected.Plan.DeterministicAggregatePlanSha256 !=
                actual.Plan.DeterministicAggregatePlanSha256 ||
            expected.Plan.SourceRow80Sha256 != actual.Plan.SourceRow80Sha256 ||
            expected.Plan.BaselineRow80Sha256 != actual.Plan.BaselineRow80Sha256 ||
            expected.Plan.OutputRow80Sha256 != actual.Plan.OutputRow80Sha256 ||
            expected.Plan.BaselineModelSha256 != actual.Plan.BaselineModelSha256 ||
            expected.Plan.OutputModelSha256 != actual.Plan.OutputModelSha256 ||
            expected.Plan.BaselineDocumentCanonicalSha256 !=
                actual.Plan.BaselineDocumentCanonicalSha256 ||
            expected.Plan.BaselineDeterministicPlanSha256 !=
                actual.Plan.BaselineDeterministicPlanSha256 ||
            expected.Plan.PresetId != actual.Plan.PresetId ||
            expected.Plan.ColorHex != actual.Plan.ColorHex ||
            expected.Plan.CycloramaModelOffset != actual.Plan.CycloramaModelOffset ||
            expected.Plan.CycloramaRow80Offset != actual.Plan.CycloramaRow80Offset ||
            expected.Plan.CycloramaWadOffset != actual.Plan.CycloramaWadOffset ||
            expected.Plan.CycloramaByteLength != actual.Plan.CycloramaByteLength ||
            expected.Plan.PaletteWordCount != actual.Plan.PaletteWordCount ||
            expected.Plan.PaletteOwnedRangeCount !=
                actual.Plan.PaletteOwnedRangeCount ||
            expected.Plan.BaselineCycloramaSha256 !=
                actual.Plan.BaselineCycloramaSha256 ||
            expected.Plan.OutputCycloramaSha256 !=
                actual.Plan.OutputCycloramaSha256 ||
            expected.Plan.GeometrySha256 != actual.Plan.GeometrySha256 ||
            expected.Plan.NonPaletteSha256 != actual.Plan.NonPaletteSha256 ||
            expected.Plan.CommandByteSha256 != actual.Plan.CommandByteSha256 ||
            expected.Plan.CycloramaChangedRgbByteCount !=
                actual.Plan.CycloramaChangedRgbByteCount ||
            expected.Plan.CompilerOwnedRangeMapSha256 !=
                actual.Plan.CompilerOwnedRangeMapSha256 ||
            expected.Plan.OwnedRangeMapSha256 != actual.Plan.OwnedRangeMapSha256 ||
            expected.Plan.InverseRangeMapSha256 != actual.Plan.InverseRangeMapSha256 ||
            expected.Plan.CompilerTransactionSha256 !=
                actual.Plan.CompilerTransactionSha256 ||
            expected.Plan.CompilerProtectedComplementSha256 !=
                actual.Plan.CompilerProtectedComplementSha256 ||
            expected.Plan.CompilerDeterministicPlanSha256 !=
                actual.Plan.CompilerDeterministicPlanSha256 ||
            expected.Plan.ProtectedRangeMapSha256 != actual.Plan.ProtectedRangeMapSha256 ||
            expected.Plan.ProtectedComplementSha256 !=
                actual.Plan.ProtectedComplementSha256 ||
            expected.Plan.AtomicGroupMapSha256 != actual.Plan.AtomicGroupMapSha256 ||
            expected.Plan.OwnedRangeCount != actual.Plan.OwnedRangeCount ||
            expected.Plan.InverseRangeCount != actual.Plan.InverseRangeCount ||
            expected.Plan.ProtectedRangeCount != actual.Plan.ProtectedRangeCount ||
            expected.Plan.GuardedByteCount != actual.Plan.GuardedByteCount ||
            expected.Plan.ProtectedByteCount != actual.Plan.ProtectedByteCount ||
            expected.Plan.ChangedByteCount != actual.Plan.ChangedByteCount)
        {
            throw new InvalidDataException(
                $"The freshly reloaded aggregate projection changed {context}.");
        }
    }

    private static Id65AuthoringBlankCycloramaAggregateCandidatePaths CreatePaths(
        UnusedLevel65BlankLevelLabWorkspacePaths workspace,
        Id65AuthoringBlankCycloramaAggregatePlan plan)
    {
        string aggregatePlanSha256 = plan.DeterministicAggregatePlanSha256;
        if (!IsCanonicalSha256(aggregatePlanSha256))
            throw new InvalidDataException("The aggregate candidate plan hash is not canonical SHA-256.");
        _ = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(workspace);
        string root = Path.GetFullPath(workspace.RootPath);
        string candidateRoot = Path.GetFullPath(Path.Combine(root, CandidateRootDirectoryName));
        string operationsRoot = Path.GetFullPath(Path.Combine(root, OperationsRootDirectoryName));
        string lease = Path.GetFullPath(Path.Combine(root, WriterLeaseFileName));
        string candidateDirectory = Path.GetFullPath(Path.Combine(
            candidateRoot,
            CreateCandidateDirectoryName(
                plan.MobyPositionId,
                aggregatePlanSha256)));
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
        string positionId,
        string aggregatePlanSha256)
    {
        _ = Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        if (!IsCanonicalSha256(aggregatePlanSha256))
        {
            throw new InvalidDataException(
                "The aggregate candidate directory identity requires canonical SHA-256 pins.");
        }
        return $"{positionId}-schema6-blank-cyclorama-aggregate-{aggregatePlanSha256}";
    }

    private static bool PrepareFixedRoots(
        UnusedLevel65BlankLevelLabWorkspacePaths workspace,
        Id65AuthoringBlankCycloramaAggregateCandidatePaths paths)
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
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint faultPoint)
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
        byte[] outputRow = Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            plan,
            sourceRow,
            reverse: false);
        DiscImage.WriteFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            outputRow);
        ThrowIfFault(faultPoint, Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterRowWritten);
        int rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
            image,
            layout,
            WadLba,
            [(TargetDataWadOffset, TargetDataByteLength)]);
        ThrowIfFault(
            faultPoint,
            Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.AfterRawSectorsRebuilt);
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
        Id65AuthoringBlankCycloramaAggregatePlan plan,
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
        if (source.Length != ExpectedSourceImageByteLength ||
            source.Length != candidate.Length || source.Length % RawSectorByteLength != 0)
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
        byte[] restored = Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            plan,
            outputRow,
            reverse: true);
        if (!restored.SequenceEqual(sourceRow))
            throw new InvalidDataException("The aggregate candidate inverse readback changed.");
        RequireExactRowOwnershipReadback(plan, sourceRow, outputRow);

        RequireHash(
            Hash(DiscImage.ReadFileBytes(candidate, candidateLayout, WadLba, 80 * 8L, 8)),
            plan.Destination.TargetDataDirectoryRowSha256,
            "candidate WAD80 directory row");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(candidate, candidateLayout, WadLba, 16 * 8L, 8)),
            plan.Destination.ProtectedTownSquareDataDirectoryRowSha256,
            "candidate retail WAD16 directory row");
        foreach (int directoryRow in new[] { 9, 10, 15, 16, 79, 80 })
        {
            RequireEqualComponent(
                source,
                candidate,
                sourceLayout,
                WadLba,
                directoryRow * 8L,
                8,
                $"WAD{directoryRow} directory row");
        }
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.ProtectedTownSquareDataWadOffset,
            plan.Destination.ProtectedTownSquareDataByteLength,
            "retail Town Square WAD16");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.ProtectedTownSquareOverlayWadOffset,
            plan.Destination.ProtectedTownSquareOverlayByteLength,
            "retail Town Square WAD15 overlay");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.ProtectedId65OverlayWadOffset,
            plan.Destination.ProtectedId65OverlayByteLength,
            "ID65 WAD79 overlay");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.ProtectedArtisansOverlayWadOffset,
            plan.Destination.ProtectedArtisansOverlayByteLength,
            "retail Artisans WAD9 overlay");
        RequireEqualComponent(
            source,
            candidate,
            sourceLayout,
            WadLba,
            plan.Destination.ProtectedArtisansDataWadOffset,
            plan.Destination.ProtectedArtisansDataByteLength,
            "retail Artisans WAD10");
        RequirePreservedRetailSkyBlocks(source, candidate, sourceLayout, plan);
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
        return new(
            outputImageSha256,
            physical.ChangedPhysicalBytes,
            physical.ChangedRawSectorCount,
            physical.DiffSha256);
    }

    private static void RequireExactRowOwnershipReadback(
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        ReadOnlySpan<byte> sourceRow,
        ReadOnlySpan<byte> outputRow)
    {
        if (sourceRow.Length != TargetDataByteLength ||
            outputRow.Length != TargetDataByteLength ||
            plan.OwnedRangeCount != plan.InverseRangeCount ||
            plan.OwnedRanges.Count != plan.OwnedRangeCount ||
            plan.InverseRanges.Count != plan.InverseRangeCount ||
            plan.ProtectedRanges.Count != plan.ProtectedRangeCount)
        {
            throw new InvalidDataException(
                "The aggregate candidate ownership ledger cardinality changed.");
        }

        bool[] owned = new bool[TargetDataByteLength];
        bool[] protectedBytes = new bool[TargetDataByteLength];
        int guardedByteCount = 0;
        int protectedByteCount = 0;
        int changedByteCount = 0;
        for (int index = 0; index < plan.OwnedRanges.Count; index++)
        {
            Id65AuthoringBlankCycloramaAggregateOwnedRange range =
                plan.OwnedRanges[index];
            Id65AuthoringBlankCycloramaAggregateOwnedRange inverse =
                plan.InverseRanges[plan.InverseRanges.Count - 1 - index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            byte[] inverseBefore = inverse.CopyBefore();
            byte[] inverseAfter = inverse.CopyAfter();
            if (range.TargetWadEntry != TargetDataWadEntry ||
                range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset > TargetDataByteLength - range.ByteLength ||
                before.Length != range.ByteLength || after.Length != range.ByteLength ||
                range.BeforeSha256 != Hash(before) || range.AfterSha256 != Hash(after) ||
                !sourceRow.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !outputRow.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                inverse.StableId != range.StableId ||
                inverse.OwnerId != range.OwnerId || inverse.Domain != range.Domain ||
                inverse.DataRelativeOffset != range.DataRelativeOffset ||
                inverse.ByteLength != range.ByteLength ||
                inverse.BeforeSha256 != range.AfterSha256 ||
                inverse.AfterSha256 != range.BeforeSha256 ||
                !inverseBefore.SequenceEqual(after) ||
                !inverseAfter.SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "The aggregate candidate owned/inverse range readback changed.");
            }
            for (int offset = 0; offset < range.ByteLength; offset++)
            {
                int absolute = range.DataRelativeOffset + offset;
                if (owned[absolute])
                    throw new InvalidDataException(
                        "The aggregate candidate owned ranges overlap.");
                owned[absolute] = true;
                if (before[offset] != after[offset])
                    changedByteCount++;
            }
            guardedByteCount += range.ByteLength;
        }

        foreach (Id65AuthoringEditorAggregateProtectedRange range in
                 plan.ProtectedRanges)
        {
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset > TargetDataByteLength - range.ByteLength ||
                !sourceRow.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(outputRow.Slice(
                        range.DataRelativeOffset,
                        range.ByteLength)) ||
                Hash(sourceRow.Slice(range.DataRelativeOffset, range.ByteLength)) !=
                    range.SegmentSha256)
            {
                throw new InvalidDataException(
                    "The aggregate candidate changed its protected row-80 complement.");
            }
            for (int offset = 0; offset < range.ByteLength; offset++)
            {
                int absolute = range.DataRelativeOffset + offset;
                if (owned[absolute] || protectedBytes[absolute])
                    throw new InvalidDataException(
                        "The aggregate candidate protected ledger overlaps ownership.");
                protectedBytes[absolute] = true;
            }
            protectedByteCount += range.ByteLength;
        }

        if (guardedByteCount != plan.GuardedByteCount ||
            protectedByteCount != plan.ProtectedByteCount ||
            guardedByteCount + protectedByteCount != TargetDataByteLength ||
            changedByteCount != plan.ChangedByteCount ||
            Enumerable.Range(0, TargetDataByteLength).Any(index =>
                owned[index] == protectedBytes[index]))
        {
            throw new InvalidDataException(
                "The aggregate candidate ownership ledger does not partition row 80 exactly.");
        }
        RequireExactResidentT21Readback(plan, sourceRow, outputRow);
        RequireExactAuthoredVisibilityReadback(plan, outputRow);
        RequireExactCycloramaReadback(plan, sourceRow, outputRow);
    }

    private static void RequireExactAuthoredVisibilityReadback(
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        ReadOnlySpan<byte> outputRow)
    {
        int terrainCount = plan.ActiveTerrainUnitIds.Count;
        if (terrainCount is < 0 or > 2)
            throw new InvalidDataException(
                "The schema-6 visibility readback received an invalid terrain topology.");
        int modelOffset = checked(0x2B548 + terrainCount * 0x20);
        int rowOffset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            modelOffset);
        int byteLength = Id65AuthoringBlankCycloramaCompiler
            .ExpectedVisibilityOcclusionByteLength;
        if (rowOffset < 0 || rowOffset + byteLength > outputRow.Length)
            throw new InvalidDataException(
                "The schema-6 authored-only visibility component is outside WAD80.");
        Id65AuthoringBlankCycloramaCompiler
            .RequireAuthoredOnlyVisibilityOcclusion(
                outputRow.Slice(rowOffset, byteLength));
    }

    private static void RequireExactCycloramaReadback(
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        ReadOnlySpan<byte> sourceRow,
        ReadOnlySpan<byte> outputRow)
    {
        if (plan.PresetId !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
            plan.ColorHex !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex ||
            plan.CycloramaByteLength !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedCycloramaByteLength ||
            plan.PaletteWordCount !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedPaletteWordCount ||
            plan.PaletteOwnedRangeCount != plan.PaletteWordCount ||
            plan.CycloramaRow80Offset < 0 ||
            plan.CycloramaRow80Offset >
                sourceRow.Length - plan.CycloramaByteLength ||
            plan.CycloramaWadOffset !=
                TargetDataWadOffset + plan.CycloramaRow80Offset)
        {
            throw new InvalidDataException(
                "The schema-6 cyclorama intent, location, or palette cardinality changed.");
        }

        byte[] outputCyclorama = outputRow.Slice(
            plan.CycloramaRow80Offset,
            plan.CycloramaByteLength).ToArray();
        Spyro1SkyBlockLayout layout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(outputCyclorama);
        Spyro1SkyBlockLayout expectedLayout = plan.OutputCycloramaLayout;
        if (!string.Equals(
                Hash(outputCyclorama),
                plan.OutputCycloramaSha256,
                StringComparison.OrdinalIgnoreCase) ||
            layout.Sha256 != plan.OutputCycloramaSha256 ||
            layout.GeometrySha256 != plan.GeometrySha256 ||
            layout.GeometrySha256 !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256 ||
            layout.ByteLength != expectedLayout.ByteLength ||
            layout.PartCount != expectedLayout.PartCount ||
            layout.VertexCount != expectedLayout.VertexCount ||
            layout.ColorCount != expectedLayout.ColorCount ||
            layout.PolygonCount != expectedLayout.PolygonCount ||
            layout.PaletteWordCount != expectedLayout.PaletteWordCount ||
            layout.TrailingBytes != 0 ||
            !layout.Parts.SequenceEqual(expectedLayout.Parts))
        {
            throw new InvalidDataException(
                "The materialized schema-6 cyclorama layout or frozen geometry changed: " +
                $"block={Hash(outputCyclorama)}/{plan.OutputCycloramaSha256}/{layout.Sha256} " +
                $"geometry={layout.GeometrySha256}/{plan.GeometrySha256}/" +
                $"{Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256} " +
                $"bytes={layout.ByteLength}/{expectedLayout.ByteLength} " +
                $"parts={layout.PartCount}/{expectedLayout.PartCount}/" +
                $"{layout.Parts.SequenceEqual(expectedLayout.Parts)} " +
                $"vertices={layout.VertexCount}/{expectedLayout.VertexCount} " +
                $"colors={layout.ColorCount}/{expectedLayout.ColorCount} " +
                $"polygons={layout.PolygonCount}/{expectedLayout.PolygonCount} " +
                $"palette={layout.PaletteWordCount}/{expectedLayout.PaletteWordCount} " +
                $"trailing={layout.TrailingBytes}.");
        }

        int[] paletteLocalOffsets = new[] { layout.BackgroundColorOffset }
            .Concat(layout.Parts.SelectMany(part =>
                Enumerable.Range(0, part.ColorCount).Select(index =>
                    part.ColorTableOffset + (index * 4))))
            .Order()
            .ToArray();
        int[] paletteRowOffsets = paletteLocalOffsets
            .Select(offset => checked(plan.CycloramaRow80Offset + offset))
            .ToArray();
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] paletteRanges =
            plan.OwnedRanges.Where(item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .CycloramaPalette).ToArray();
        if (paletteLocalOffsets.Length != plan.PaletteWordCount ||
            paletteLocalOffsets.Distinct().Count() != plan.PaletteWordCount ||
            paletteRanges.Length != plan.PaletteWordCount ||
            !paletteRanges.Select(item => item.DataRelativeOffset)
                .SequenceEqual(paletteRowOffsets) ||
            !paletteRanges.Select(item => item.StableId).SequenceEqual(
                Enumerable.Range(0, plan.PaletteWordCount).Select(index =>
                    $"schema6.cyclorama.palette.{index:D4}")) ||
            paletteRanges.Any(item => item.ByteLength != 4))
        {
            throw new InvalidDataException(
                "The materialized cyclorama lost exact ownership of its 727 palette words.");
        }

        for (int index = 0; index < paletteRowOffsets.Length; index++)
        {
            int rowOffset = paletteRowOffsets[index];
            byte[] after = paletteRanges[index].CopyAfter();
            if (after.Length != 4 ||
                after[0] != 0x26 || after[1] != 0x34 || after[2] != 0x47 ||
                !outputRow.Slice(rowOffset, 4).SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "A materialized cyclorama palette RGB/command word changed from its exact contract.");
            }
        }
        if (HashOutsidePaletteRgb(outputRow, paletteRowOffsets) !=
                plan.NonPaletteSha256 ||
            HashPaletteCommands(outputCyclorama, paletteLocalOffsets) !=
                plan.CommandByteSha256)
        {
            throw new InvalidDataException(
                "The materialized cyclorama changed a command or nonpalette byte.");
        }
    }

    private static string HashOutsidePaletteRgb(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<int> paletteOffsets)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (int offset in paletteOffsets)
        {
            if (offset > cursor)
                hash.AppendData(bytes.Slice(cursor, offset - cursor));
            cursor = offset + 3;
        }
        if (cursor < bytes.Length)
            hash.AppendData(bytes[cursor..]);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string HashPaletteCommands(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<int> paletteOffsets)
    {
        byte[] commands = new byte[paletteOffsets.Count];
        for (int index = 0; index < paletteOffsets.Count; index++)
            commands[index] = bytes[paletteOffsets[index] + 3];
        return Hash(commands);
    }

    private static void RequireExactResidentT21Readback(
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        ReadOnlySpan<byte> sourceRow,
        ReadOnlySpan<byte> outputRow)
    {
        int t21RowOffset = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
            .ResidentT21RowOffset;
        int t21RowLength = Id65RemotePadRedGemT21Catalog.RowByteLength;
        int t21RowEnd = checked(t21RowOffset + t21RowLength);
        int xyzOffset = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
            .ResidentT21CoordinateOffset;
        int xyzLength = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
            .ResidentT21CoordinateByteLength;
        byte[] exactSourceRow = Convert.FromHexString(
            Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
        RequireHash(
            Hash(exactSourceRow),
            Id65RemotePadRedGemT21Catalog.SourceRowSha256,
            "catalog resident-T21 source row");
        if (!sourceRow.Slice(t21RowOffset, t21RowLength)
                .SequenceEqual(exactSourceRow))
        {
            throw new InvalidDataException(
                "The aggregate candidate locked source lost the full resident-T21 row.");
        }

        bool Related(
            Id65AuthoringBlankCycloramaAggregateOwnedRange range)
        {
            long end = range.DataRelativeOffset + (long)range.ByteLength;
            bool overlapsT21Row = range.DataRelativeOffset < t21RowEnd &&
                end > t21RowOffset;
            return overlapsT21Row ||
                range.StableId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                        .ResidentT21XyzOwnedRangeStableId ||
                range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
                range.Domain ==
                    Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem;
        }

        Id65AuthoringBlankCycloramaAggregateOwnedRange[] owned =
            plan.OwnedRanges.Where(Related).ToArray();
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] inverse =
            plan.InverseRanges.Where(Related).ToArray();
        if (plan.MobyKind != Id65RemotePadMobyKind.RedGemT21)
        {
            if (owned.Length != 0 || inverse.Length != 0 ||
                !outputRow.Slice(t21RowOffset, t21RowLength)
                    .SequenceEqual(exactSourceRow))
            {
                throw new InvalidDataException(
                    "A non-Red-Gem aggregate candidate changed or claimed the resident-T21 row.");
            }
            return;
        }

        byte[] exactOutputRow = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
            plan.MobyPositionId);
        string expectedOutputRowSha256 = Id65RemotePadRedGemT21Catalog
            .GetRequiredPosition(plan.MobyPositionId).OutputRowSha256;
        if (plan.MobyKind != Id65RemotePadMobyKind.RedGemT21 ||
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                plan.MobyKind,
                plan.MobyPositionId) != expectedOutputRowSha256 ||
            Hash(exactOutputRow) != expectedOutputRowSha256 ||
            owned.Length != 1 || inverse.Length != 1 ||
            !IsExactResidentT21XyzRange(owned[0], xyzOffset, xyzLength) ||
            !IsExactResidentT21XyzRange(inverse[0], xyzOffset, xyzLength) ||
            !owned[0].CopyBefore().AsSpan().SequenceEqual(exactSourceRow.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                xyzLength)) ||
            !owned[0].CopyAfter().AsSpan().SequenceEqual(exactOutputRow.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                xyzLength)) ||
            !inverse[0].CopyBefore().SequenceEqual(owned[0].CopyAfter()) ||
            !inverse[0].CopyAfter().SequenceEqual(owned[0].CopyBefore()) ||
            !outputRow.Slice(t21RowOffset, t21RowLength)
                .SequenceEqual(exactOutputRow))
        {
            throw new InvalidDataException(
                "The aggregate candidate lost exact resident-T21 XYZ ownership or full-row readback.");
        }
    }

    private static bool IsExactResidentT21XyzRange(
        Id65AuthoringBlankCycloramaAggregateOwnedRange range,
        int xyzOffset,
        int xyzLength) =>
        range.StableId ==
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                .ResidentT21XyzOwnedRangeStableId &&
        range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId &&
        range.Domain ==
            Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem &&
        range.TargetWadEntry == TargetDataWadEntry &&
        range.DataRelativeOffset == xyzOffset &&
        range.WadOffset == TargetDataWadOffset + xyzOffset &&
        range.ByteLength == xyzLength;

    private static async Task<PhysicalDiff> ComparePhysicalImagesAsync(
        FileStream source,
        FileStream candidate,
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        CancellationToken cancellationToken)
    {
        bool[] ownedMask = new bool[TargetDataByteLength];
        foreach (Id65AuthoringBlankCycloramaAggregateOwnedRange range in plan.OwnedRanges)
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

    private static Id65AuthoringBlankCycloramaAggregateCandidateReceipt BuildReceipt(
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request,
        ProjectionCapture projection,
        Readback readback)
    {
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                projection.Plan.MobyPositionId);
        string? selectedExactRowSha256 =
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                projection.Plan.MobyKind,
                projection.Plan.MobyPositionId);
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt = new(
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
            projection.Stored.StaticAuthoringProfileId,
            projection.Stored.StaticAuthoringCatalogSha256,
            projection.Stored.StaticAuthoringSceneGraphSha256,
            projection.Stored.StaticAuthoringProjectionSha256,
            projection.Plan.StaticAuthoringTerrainManifestSha256,
            projection.Plan.StaticAuthoringCycloramaCatalogSha256,
            projection.Plan.TerrainManifestSha256,
            CanonicalizeTerrainUnitIds(projection.Plan.ActiveTerrainUnitIds),
            Id65RemotePadMobyChoiceCatalog.GetKindId(projection.Plan.MobyKind),
            projection.Plan.NativeInputSetSha256,
            projection.Plan.DeterministicAggregatePlanSha256,
            Id65AuthoringBlankCycloramaCompiler.ProfileId,
            Id65AuthoringBlankCycloramaCompiler.SchemaVersion,
            Id65AuthoringBlankCycloramaCatalog.CanonicalSha256,
            projection.Plan.PresetId,
            projection.Plan.ColorHex,
            projection.Plan.MobyPositionId,
            position.Ordinal,
            position.EditorX,
            position.EditorY,
            position.EditorZ,
            position.RawX,
            position.RawY,
            position.RawZ,
            selectedExactRowSha256,
            Id65RemotePadGrassT107Catalog.SupportHandle,
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex,
            projection.Plan.BaselineDocumentCanonicalSha256,
            projection.Plan.BaselineRow80Sha256,
            projection.Plan.BaselineModelSha256,
            projection.Plan.BaselineDeterministicPlanSha256,
            projection.Plan.CycloramaModelOffset,
            projection.Plan.CycloramaRow80Offset,
            projection.Plan.CycloramaWadOffset,
            projection.Plan.CycloramaByteLength,
            projection.Plan.PaletteWordCount,
            projection.Plan.PaletteOwnedRangeCount,
            projection.Plan.BaselineCycloramaSha256,
            projection.Plan.OutputCycloramaSha256,
            projection.Plan.GeometrySha256,
            projection.Plan.NonPaletteSha256,
            projection.Plan.CommandByteSha256,
            projection.Plan.CycloramaChangedRgbByteCount,
            projection.Plan.CompilerOwnedRangeMapSha256,
            projection.Plan.CompilerTransactionSha256,
            projection.Plan.CompilerProtectedComplementSha256,
            projection.Plan.CompilerDeterministicPlanSha256,
            projection.Plan.OwnedRangeMapSha256,
            projection.Plan.InverseRangeMapSha256,
            projection.Plan.ProtectedRangeMapSha256,
            projection.Plan.ProtectedComplementSha256,
            projection.Plan.AtomicGroupMapSha256,
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
            ExactStoreReloadCasVerifiedThreeTimes: true,
            ExactStaticAuthoringProjectionReloadVerifiedThreeTimes: true,
            ExactNativeInputGenerationRecapturedThreeTimes: true,
            ExactV3TemplateRecapturedThreeTimes: true,
            ExactLockedBaseRecapturedThreeTimes: true,
            Schema6CompilerPinsVerified: true,
            BlankCycloramaIntentReadbackVerified: true,
            CycloramaLayoutReadbackVerified: true,
            EveryPaletteWordReadbackVerified: true,
            CycloramaGeometryReadbackVerified: true,
            CycloramaCommandBytesReadbackVerified: true,
            CycloramaNonPaletteReadbackVerified: true,
            MobyChoicePositionReadbackVerified: true,
            FourTerrainTopologiesVerified: true,
            T88ExcludedVerified: true,
            RepairedT13995SupportVerified: true,
            ExactRow80ReadbackVerified: true,
            ExactInverseReadbackVerified: true,
            ProtectedComplementVerified: true,
            Wad79Preserved: true,
            RetailTownSquareWad15Preserved: true,
            RetailTownSquareWad16Preserved: true,
            RetailArtisansWad9Preserved: true,
            RetailArtisansWad10Preserved: true,
            RetailTownSquareSkyBlockPreserved: true,
            RetailArtisansLinkedSkyBlockPreserved: true,
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
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt finalized = receipt with
        {
            ReceiptContentSha256 = ComputeReceiptContentSha256(receipt)
        };
        return finalized;
    }

    private static Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult BuildResult(
        Id65AuthoringBlankCycloramaAggregateCandidatePaths paths,
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt,
        Id65AuthoringBlankCycloramaAggregatePlan plan,
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
        Id65AuthoringBlankCycloramaAggregateCandidatePaths paths,
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt expectedReceipt,
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
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt expectedReceipt,
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
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt actualReceipt;
        try
        {
            actualReceipt =
                JsonSerializer.Deserialize<Id65AuthoringBlankCycloramaAggregateCandidateReceipt>(
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
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt) =>
        Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + "\n");

    private static string CanonicalizeTerrainUnitIds(
        IReadOnlyList<string> activeTerrainUnitIds)
    {
        ArgumentNullException.ThrowIfNull(activeTerrainUnitIds);
        if (activeTerrainUnitIds.Any(item =>
                string.IsNullOrWhiteSpace(item) || item.Contains('\n') ||
                item.Contains('\r')))
        {
            throw new InvalidDataException(
                "The schema-6 terrain-unit identity set is not canonical.");
        }
        return string.Join('\n', activeTerrainUnitIds);
    }

    private static string BuildCanonicalCandidateCue() =>
        $"FILE \"{CandidateImageFileName}\" BINARY\n" +
        "  TRACK 01 MODE2/2352\n" +
        "    INDEX 01 00:00:00\n";

    private static string ComputeReceiptContentSha256(
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt)
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
        Id65AuthoringBlankCycloramaAggregateCandidatePaths paths)
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
                        journal.PositionId,
                        journal.AggregatePlanSha256) ||
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
        string positionId,
        string aggregatePlanSha256,
        string receiptContentSha256,
        string phase)
    {
        if (phase is not OperationPhaseStaging and not OperationPhaseVerifiedReady)
            throw new ArgumentOutOfRangeException(nameof(phase));
        OperationJournal journal = new(
            OperationJournalSchemaVersion,
            OperationKind,
            positionId,
            aggregatePlanSha256,
            receiptContentSha256,
            CreateCandidateDirectoryName(
                positionId,
                aggregatePlanSha256),
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
            !IsKnownPosition(journal.PositionId) ||
            !IsCanonicalSha256(journal.AggregatePlanSha256) ||
            !IsCanonicalSha256(journal.CandidateReceiptContentSha256) ||
            journal.CandidateDirectoryName != CreateCandidateDirectoryName(
                journal.PositionId,
                journal.AggregatePlanSha256) ||
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

    private static void RequirePreservedRetailSkyBlocks(
        FileStream source,
        FileStream candidate,
        DiscLayout layout,
        Id65AuthoringBlankCycloramaAggregatePlan plan)
    {
        foreach ((long offset, string label) in new[]
        {
            (RetailTownSquareSkyBlockWadOffset, "retail Town Square sky block"),
            (RetailArtisansLinkedSkyBlockWadOffset,
                "retail Artisans linked sky block")
        })
        {
            byte[] before = DiscImage.ReadFileBytes(
                source,
                layout,
                WadLba,
                offset,
                RetailSkyBlockByteLength);
            byte[] after = DiscImage.ReadFileBytes(
                candidate,
                layout,
                WadLba,
                offset,
                RetailSkyBlockByteLength);
            if (!before.SequenceEqual(after) ||
                !string.Equals(
                    Convert.ToHexString(SHA256.HashData(before)),
                    ExpectedRetailSkyBlockSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Convert.ToHexString(SHA256.HashData(after)),
                    ExpectedRetailSkyBlockSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The aggregate candidate changed or lost the exact {label} identity.");
            }

            Spyro1SkyBlockLayout skyLayout =
                Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(after);
            int[] paletteOffsets = new[] { skyLayout.BackgroundColorOffset }
                .Concat(skyLayout.Parts.SelectMany(part =>
                    Enumerable.Range(0, part.ColorCount).Select(index =>
                        part.ColorTableOffset + index * 4)))
                .Order()
                .ToArray();
            if (!string.Equals(
                    skyLayout.Sha256,
                    plan.BaselineCycloramaSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                skyLayout.GeometrySha256 != plan.GeometrySha256 ||
                paletteOffsets.Length != plan.PaletteWordCount ||
                paletteOffsets.Any(paletteOffset =>
                    (after[paletteOffset], after[paletteOffset + 1],
                        after[paletteOffset + 2]) ==
                    ((byte)0x26, (byte)0x34, (byte)0x47)) ||
                HashPaletteCommands(after, paletteOffsets) !=
                    plan.CommandByteSha256)
            {
                throw new InvalidDataException(
                    $"The exact {label} no longer corroborates the schema-6 baseline palette, geometry, or command contract.");
            }
        }
    }

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

    private static bool IsKnownPosition(string value) =>
        Id65RemotePadGrassT107Catalog.Positions.Any(item =>
            item.PositionId == value);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void ThrowIfFault(
        Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint selected,
        Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint current)
    {
        if (selected == current)
            throw new InvalidOperationException(
                $"Injected aggregate candidate materialization fault at {current}.");
    }

    private sealed record ProjectionCapture(
        Id65AuthoringEditorDocumentStoreResult Stored,
        Id65AuthoringBlankCycloramaAggregatePlan Plan,
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
        string PositionId,
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
