using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65ZeroEggRuntimeCandidateRequest(
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65ZeroEggRuntimeCandidatePaths(
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

internal sealed record UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65ZeroEggRuntimeCandidatePatch(
    string Name,
    long WadOffset,
    int ByteLength,
    string BeforeHex,
    string AfterHex,
    int ChangedByteCount,
    string BeforeSha256,
    string AfterSha256);

internal sealed record UnusedLevel65ZeroEggRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string BaseImageSha256,
    string BaseCueSha256,
    string SourceExecutableSha256,
    string OutputExecutableSha256,
    string SourceId65DataSha256,
    string OutputId65DataSha256,
    string SourceWadSha256,
    string OutputWadSha256,
    string OutputHeaderSha256,
    string OutputActorSubfileSha256,
    string OutputSceneSubfileSha256,
    string OutputObjectTableSha256,
    string SourceT88RowSha256,
    string OutputT88RowSha256,
    string SourceGrassRowSha256,
    string SourceGrassPackageSha256,
    string SourceGrassPropertiesSha256,
    string PreservedThiefPrivateBlockSha256,
    string PreservedThiefPathSha256,
    string PreservedFixupComponentSha256,
    int LevelId,
    int ContinuousLevelIndex,
    int TargetTrueIndex,
    int DonorTrueIndex,
    int TargetActorRootIndex,
    int ActorId,
    int PreservedRawX,
    int PreservedRawY,
    int PreservedRawZ,
    int ObjectCount,
    IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> Patches,
    string OrderedTransactionSha256,
    int GuardedLogicalWadBytes,
    int ChangedLogicalWadBytes,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool ExactFiveRangeTransactionVerified,
    bool ObjectCountPreserved,
    bool T88XyzPreserved,
    bool AllOtherObjectRowsPreserved,
    bool ThiefPrivateBlockPathAndFixupsPreserved,
    bool Class21And22ObjectRowsAbsent,
    bool ExecutablePreserved,
    bool RetailTerrainCollisionTextureMusicAndSpawnPreserved,
    bool NoMemoryCardOrSaveAuthorized,
    bool Mode2EdcEccRebuilt,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool RuntimeProofComplete,
    bool AppEnabled,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65ZeroEggRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string ConstructionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string BaseImageSha256,
    string BaseCueSha256,
    string OutputImageSha256,
    string OutputCueSha256,
    string ConstructionPlanSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string FinderHelperSha256,
    string SourceExecutableSha256,
    string OutputExecutableSha256,
    string SourceId65DataSha256,
    string OutputId65DataSha256,
    string SourceWadSha256,
    string OutputWadSha256,
    string OutputHeaderSha256,
    string OutputActorSubfileSha256,
    string OutputSceneSubfileSha256,
    string OutputObjectTableSha256,
    string OrderedTransactionSha256,
    int GuardedLogicalWadBytes,
    int ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactFiveRangeTransactionVerified,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FullImageReadbackVerified,
    bool ExecutableReadbackVerified,
    bool Id65DataReadbackVerified,
    bool FullWadReadbackVerified,
    bool ObjectCountAndAllOtherRowsPreserved,
    bool ThiefPrivateBlockPathAndFixupsPreserved,
    bool Class21And22ObjectRowsAbsent,
    bool RetailTerrainCollisionTextureMusicAndSpawnPreserved,
    bool NoMemoryCardOrSaveAuthorized,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool CanonicalReceiptVerified,
    bool SevenFilePublicationVerified,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RuntimeProofComplete,
    bool AppEnabled,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65ZeroEggRuntimeCandidateResult(
    UnusedLevel65ZeroEggRuntimeCandidatePaths Paths,
    UnusedLevel65ZeroEggRuntimeCandidatePlan Plan,
    UnusedLevel65ZeroEggRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal FinderReveal,
    string OutputImageSha256,
    string OutputExecutableSha256,
    string OutputId65DataSha256,
    string OutputWadSha256,
    string OutputHeaderSha256,
    string OutputActorSubfileSha256,
    string OutputSceneSubfileSha256,
    string OutputObjectTableSha256,
    string OrderedTransactionSha256,
    int GuardedLogicalWadBytes,
    int ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactFiveRangeTransactionVerified,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FullImageReadbackVerified,
    bool ExecutableReadbackVerified,
    bool Id65DataReadbackVerified,
    bool FullWadReadbackVerified,
    bool ObjectCountAndAllOtherRowsPreserved,
    bool ThiefPrivateBlockPathAndFixupsPreserved,
    bool Class21And22ObjectRowsAbsent,
    bool RetailTerrainCollisionTextureMusicAndSpawnPreserved,
    bool NoMemoryCardOrSaveAuthorized,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool CanonicalReceiptVerified,
    bool SevenFilePublicationVerified,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RuntimeProofComplete,
    bool AppEnabled,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

/// <summary>
/// Publishes one disposable, unpromoted ID65 zero-egg discriminator directly
/// from the locked display-name image. It replaces Town Square T88 with the
/// exact controller-free Artisans Grass bundle while preserving T88 XYZ,
/// object count, every other row, the retired thief private block/path/fixups,
/// SCUS, retail content, terrain, collision, textures, music, spawn, and save
/// code. App integration, normal Create BIN, and promotion remain forbidden.
/// </summary>
internal static class UnusedLevel65ZeroEggRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-zero-egg-t88-to-artisans-grass-clean-usa-v1";
    public const string OutputDirectoryName =
        "unused-level-65-zero-egg-t88-artisans-grass-runtime-candidate";
    public const string OutputPrefix =
        "Unused-Level-65-Zero-Egg-T88-Artisans-Grass-RUNTIME-CANDIDATE";
    public const string ExpectedOutputImageSha256 =
        "8f2801a25361b470ab5916db991d9fd4be71b9be2acbe114d275fb292e4fbee7";
    public const string ExpectedRawSectorDiffSha256 =
        "f699732129a48103132f7f4e2f26ae50e007cff1621f99b1dee7e1bde4c6ea82";
    public const string ExpectedOutputWadSha256 =
        "a287850382cc025ed80e7b72222aad6dbd7bdcceb709a801fead7eb6f440bdec";
    public const string ExpectedOrderedTransactionSha256 =
        "3b8862a8cfb5e43cedea093f8157b182b401762c48a8860b2656127f0734b0e4";
    public const string ExpectedSourceWadSha256 =
        "df09cdcfabd89eaad92deaab87ae467840330851906afa5924f19f36ced47bf8";
    public const string ExpectedFrozenReceiptSha256 =
        "6a37fadfda48ef93c5037f48eabd95df462a586edeec48126935b5fed6bee7e0";

    private const string BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    private const string BaseCueSha256 =
        "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2";
    private const string SourceExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    private const string OutputExecutableSha256 =
        SourceExecutableSha256;
    private const string SourceId65DataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private const string OutputId65DataSha256 =
        "676a27bc39bb3b26b1fac84361b8633f5c9b75b926f49b5c963336f2fc7d9eff";
    private const string OutputHeaderSha256 =
        "6157dc52dbf53f744378aa5577209751dc75eb397401504ba23a28f1d5dabd71";
    private const string OutputActorSubfileSha256 =
        "e5e0f898a2d9487d56da9b28c1fb53706df219380ce5831f79334b06ebd1118f";
    private const string OutputSceneSubfileSha256 =
        "e3d726e1cdf95dc511bca50a30de837d26fc452c1e5cc3ca4581eb457c84799b";
    private const string OutputObjectTableSha256 =
        "20e8e5c41f127f898ccfa4801969fabc3ca835d7e32944cc7002311ef479b37b";
    private const string SourceObjectTableSha256 =
        "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d";
    private const string SourceT88RowSha256 =
        "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a";
    private const string OutputT88RowSha256 =
        "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f";
    private const string SourceGrassRowSha256 =
        "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190";
    private const string SourceGrassPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    private const string SourceGrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    private const string ThiefPrivateBlockSha256 =
        "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136";
    private const string ThiefPathSha256 =
        "b5bb29f426682bc52dfac7d9eb3df1674c0456bcd645ada497beaa6a9a3f984c";
    private const string FixupComponentSha256 =
        "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2";
    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-zero-egg-runtime-candidate";
    private const string OperationDirectoryPrefix = "zero-egg-runtime-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-zero-egg-runtime-candidate-writer.lease";
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhasePreviousBackedUp = "previous-candidate-backed-up";
    private const string OperationPhaseCandidatePublished = "candidate-published";
    private const string OperationPhaseNewCandidateRemoved = "new-candidate-removed";
    private const string OperationPhaseRollbackComplete = "rollback-complete";
    private const string OperationPhaseCommitted = "committed";
    private const string ActiveWriterLeaseMessage =
        "Another ID65 zero-egg runtime candidate writer is active for this publication parent.";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int WadLba = UnusedLevel65RemoteBlankIsolationConstruction.WadLba;
    private const int WadByteLength = UnusedLevel65RemoteBlankIsolationConstruction.WadByteLength;
    private const long DataWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.DataWadOffset;
    private const int DataByteLength = UnusedLevel65RemoteBlankIsolationConstruction.DataByteLength;
    private const int HeaderByteLength = 0x200;
    private const int ActorSubfileRelativeOffset = 0x173000;
    private const int ActorSubfileByteLength = 0x5D000;
    private const int SceneSubfileRelativeOffset = 0x1D0000;
    private const int SceneSubfileByteLength = 0x8800;
    private const int ObjectTableSceneOffset = 0x170;
    private const int ObjectCount = 107;
    private const int RecordByteLength = 0x58;
    private const int TargetTrueIndex = 88;
    private const int DonorTrueIndex = 121;
    private const int TargetActorRootIndex = 37;
    private const ushort GrassActorId = 0x01F5;
    private const uint GrassActorPackageRelativeOffset = 0x1CFA44;
    private const uint GrassPropertiesSceneOffset = 0x8138;
    private const long ActorRootSlotWadOffset = 0x69368E4;
    private const long ActorIdSlotWadOffset = 0x693699A;
    private const long GrassPackageTargetWadOffset = 0x6B06244;
    private const long T88RowWadOffset = 0x6B087B0;
    private const long GrassPropertiesTargetWadOffset = 0x6B0E938;
    private const long GrassPackageSourceWadOffset = 0x9C1D20;
    private const long GrassRowSourceWadOffset = 0x9D6C44;
    private const long GrassPropertiesSourceWadOffset = 0x9DBACC;
    private const long ThiefPrivateBlockWadOffset = 0x6B0C2FC;
    private const int ThiefPrivateBlockByteLength = 0x134;
    private const long ThiefPathWadOffset = 0x6B0C358;
    private const int ThiefPathByteLength = 0xD8;
    private const long FixupComponentWadOffset = 0x6B0E728;
    private const int FixupComponentByteLength = 0x208;
    private const int ExpectedGuardedLogicalWadBytes = 474;
    private const int ExpectedChangedLogicalWadBytes = 276;
    private const int ExpectedChangedRawSectorCount = 5;
    private const int ExpectedChangedPhysicalImageBytes = 778;
    private static readonly int[] ExpectedChangedRawSectorLbas = [53_906, 54_833, 54_837, 54_838, 54_850];
    private static readonly UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff[] ExpectedRawSectorDiffs =
    [
        new(53_906, 0, 0, 5, 4, 0, 18, 40, 67),
        new(54_833, 0, 0, 260, 4, 0, 172, 104, 540),
        new(54_837, 0, 0, 6, 4, 0, 20, 44, 74),
        new(54_838, 0, 0, 3, 4, 0, 14, 30, 51),
        new(54_850, 0, 0, 2, 4, 0, 12, 28, 46)
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UnusedLevel65ZeroEggRuntimeCandidateResult> CreateAsync(
        UnusedLevel65ZeroEggRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The exact seven-file candidate requires its macOS Finder handoff.");
        string root = Path.GetFullPath(request.WorkspaceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string baseImage = RequireExistingFile(request.BaseImagePath, "exact locked display-name BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "exact locked display-name CUE");
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths = CreatePaths(request.OutputDirectoryPath);
        RejectUnsafePathTopologyBeforeMutation(root, baseImage, baseCue, paths);
        RequireSafeRoles(baseImage, baseCue, paths);
        cancellationToken.ThrowIfCancellationRequested();
        using GlobalWriterLease globalWriterLease = AcquireGlobalWriterLease(paths);
        request.TestStageHook?.Invoke("after-global-writer-lease-acquired");
        bool rollbackRecoveryVerified = RecoverOwnedOperations(
            paths.OperationsDirectoryPath,
            paths.OutputDirectoryPath);
        PrepareDestination(paths.OutputDirectoryPath, request.ReplaceExistingCandidate);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "locked display-name BIN");
        RequireHash(await HashFileAsync(baseCue, cancellationToken), BaseCueSha256, "locked display-name CUE");
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches = BuildPatches(baseImage);
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> repeated = BuildPatches(baseImage);
        if (!patches.SequenceEqual(repeated))
            throw new InvalidDataException("Two independent zero-egg patch derivations were not deterministic.");
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "zero-egg operation");
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

        UnusedLevel65ZeroEggRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                root,
                baseImage,
                baseCue,
                paths,
                stagedDirectory,
                patches,
                loadCodes,
                rollbackRecoveryVerified,
                cancellationToken);
            request.TestStageHook?.Invoke("after-staged-candidate-verified");
            cancellationToken.ThrowIfCancellationRequested();
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);
            RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "base BIN after publication");
            RequireHash(await HashFileAsync(baseCue, cancellationToken), BaseCueSha256, "base CUE after publication");
            WriteOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            RuntimeCandidateFinderReveal finalReveal = BuildFinalFinderReveal(paths);
            result = BuildResult(paths, staged, finalReveal, rollbackRecoveryVerified);
        }
        catch (Exception publicationFailure)
        {
            if (!publication.Committed)
            {
                if (!publication.PreviousBackedUp && !publication.CandidatePublished)
                {
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
            throw new IOException("The completed ID65 zero-egg operation directory could not be removed.");
        return result ?? throw new InvalidOperationException("The ID65 zero-egg writer completed without a result.");
    }

    internal static async Task<UnusedLevel65ZeroEggRuntimeCandidateResult> VerifyPublishedAsync(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths != CreatePaths(paths.OutputDirectoryPath))
            throw new InvalidDataException("The zero-egg verification paths are not the exact owned paths.");
        RejectPublicationPathAncestorReparsePoints(paths);
        RejectTreeReparsePoints(paths.OutputDirectoryPath, "published zero-egg candidate");
        VerifySevenFiles(paths.OutputDirectoryPath, paths);
        UnusedLevel65ZeroEggRuntimeCandidateReceipt receipt =
            await ReadCanonicalJsonAsync<UnusedLevel65ZeroEggRuntimeCandidateReceipt>(
                paths.StaticReadbackReceiptPath,
                "published zero-egg receipt",
                cancellationToken);
        ValidateReceiptIdentity(receipt, paths);
        bool frozenPublication = TryGetFrozenPublicationWorkspaceRoot(
            paths,
            out string frozenWorkspaceRoot);
        if (frozenPublication && !PathEquals(receipt.WorkspaceRoot, frozenWorkspaceRoot))
        {
            throw new InvalidDataException(
                "The frozen zero-egg receipt workspace root does not match its owned _local publication path.");
        }
        string root = receipt.WorkspaceRoot;
        string baseImage = RequireExistingFile(receipt.BaseImagePath, "receipt-bound base BIN");
        string baseCue = RequireExistingFile(receipt.BaseCuePath, "receipt-bound base CUE");
        RejectUnsafePathTopologyBeforeMutation(root, baseImage, baseCue, paths);
        RequireSafeRoles(baseImage, baseCue, paths);
        RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "receipt-bound base BIN");
        RequireHash(await HashFileAsync(baseCue, cancellationToken), BaseCueSha256, "receipt-bound base CUE");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.OutputCuePath, paths.OutputImagePath, "MODE2/2352");

        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches = BuildPatches(baseImage);
        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            paths.OutputImagePath,
            patches,
            cancellationToken);
        ValidateReceiptReadback(receipt, readback);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        UnusedLevel65ZeroEggRuntimeCandidatePlan expectedPlan = BuildPlan(
            patches,
            receipt.BaseCueSha256,
            readback,
            loadCodes);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            JsonSerializer.Serialize(expectedPlan, JsonOptions) + "\n",
            "published zero-egg plan");
        VerifyTextReadback(
            paths.OutputCuePath,
            DiscImage.BuildCueText(baseCue, Path.GetFileName(paths.OutputImagePath)),
            "published zero-egg CUE");
        RuntimeCandidateFinderReveal reveal = BuildFinalFinderReveal(paths);
        string checklist = BuildRuntimeChecklist(paths, reveal, loadCodes, readback);
        VerifyTextReadback(paths.RuntimeChecklistPath, checklist, "published zero-egg checklist");
        VerifyChecklist(checklist, reveal, loadCodes, paths);
        string guide = BuildLocationGuideSvg(loadCodes);
        VerifyTextReadback(paths.LocationGuidePath, guide, "published zero-egg guide");
        VerifyLocationGuide(guide);
        VerifyFinderHelper(paths, receipt.FinderHelperSha256);
        BindSidecarHashes(receipt, paths);
        UnusedLevel65ZeroEggRuntimeCandidateReceipt expectedReceipt = BuildReceipt(
            root,
            baseImage,
            baseCue,
            paths,
            readback,
            ExpectedChangedRawSectorCount,
            HashFile(paths.OutputCuePath),
            HashFile(paths.ConstructionPlanPath),
            HashFile(paths.RuntimeChecklistPath),
            HashFile(paths.LocationGuidePath),
            HashFile(paths.FinderHelperPath),
            rollbackRecoveryVerified: false);
        string expectedReceiptText = JsonSerializer.Serialize(expectedReceipt, JsonOptions) + "\n";
        VerifyTextReadback(
            paths.StaticReadbackReceiptPath,
            expectedReceiptText,
            "published canonical clean zero-egg receipt");
        string canonicalReceiptSha256 = Hash(Encoding.UTF8.GetBytes(expectedReceiptText));
        if (frozenPublication)
        {
            RequireOptionalHash(
                canonicalReceiptSha256,
                ExpectedFrozenReceiptSha256,
                "frozen canonical clean zero-egg receipt");
        }

        StagedCandidate staged = new(expectedPlan, receipt, reveal, readback, receipt.RebuiltRawSectorCount);
        return BuildResult(paths, staged, reveal, rollbackRecoveryVerified: false);
    }

    public static UnusedLevel65ZeroEggRuntimeCandidatePaths CreatePaths(string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string filesystemRoot = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, filesystemRoot) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException("The zero-egg candidate output cannot be a filesystem or home root.");
        }
        if (!string.Equals(Path.GetFileName(outputDirectory), OutputDirectoryName, StringComparison.Ordinal))
            throw new InvalidOperationException($"The zero-egg writer owns only a directory named '{OutputDirectoryName}'.");
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The zero-egg candidate output has no parent.");
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-construction-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-zero-egg-runtime-candidate-operations"));
        RequirePathsInsideOutput(paths);
        return paths;
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65ZeroEggRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
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
        RequireMode2Layout(layout, "staged zero-egg candidate");

        int rebuiltRawSectorCount;
        await using (FileStream image = new(
                         stagedImage,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 128 * 1024,
                         FileOptions.Asynchronous))
        {
            DiscFileRecord executableRecord = RequireRootFile(
                image,
                layout,
                "SCUS_942.28",
                ExecutableLba,
                ExecutableByteLength);
            RequireHash(
                Hash(DiscImage.ReadFileBytes(image, layout, executableRecord.Lba, 0, executableRecord.Size)),
                SourceExecutableSha256,
                "staged source SCUS");
            DiscFileRecord wadRecord = RequireRootFile(
                image,
                layout,
                "WAD.WAD",
                WadLba,
                WadByteLength);
            foreach (UnusedLevel65ZeroEggRuntimeCandidatePatch patch in patches)
            {
                byte[] before = Convert.FromHexString(patch.BeforeHex);
                byte[] actual = DiscImage.ReadFileBytes(
                    image, layout, wadRecord.Lba, patch.WadOffset, patch.ByteLength);
                if (!actual.SequenceEqual(before))
                    throw new InvalidDataException($"Zero-egg patch '{patch.Name}' failed its exact staged preimage guard.");
                DiscImage.WriteFileBytes(
                    image,
                    layout,
                    wadRecord.Lba,
                    patch.WadOffset,
                    Convert.FromHexString(patch.AfterHex));
            }
            IReadOnlyList<(long Offset, int ByteLength)> patchRanges = patches
                .Select(patch => (patch.WadOffset, patch.ByteLength))
                .ToArray();
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                wadRecord.Lba,
                patchRanges);
            int verifiedRawSectorCount = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                ExpectedChangedRawSectorLbas.Select(lba => (lba, 1)));
            if (rebuiltRawSectorCount != ExpectedChangedRawSectorCount ||
                verifiedRawSectorCount != ExpectedChangedRawSectorCount)
            {
                throw new InvalidDataException(
                    $"The zero-egg transaction rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectorCount} sectors, expected 5/5.");
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
            patches,
            cancellationToken);
        RequireOptionalHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "zero-egg output BIN");
        RequireOptionalHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "zero-egg raw-sector diff");
        RequireOptionalHash(readback.OutputWadSha256, ExpectedOutputWadSha256, "zero-egg output WAD.WAD");
        RequireOptionalHash(readback.OrderedTransactionSha256, ExpectedOrderedTransactionSha256, "zero-egg ordered transaction");

        string baseCueHash = await HashFileAsync(baseCue, cancellationToken);
        UnusedLevel65ZeroEggRuntimeCandidatePlan plan = BuildPlan(
            patches,
            baseCueHash,
            readback,
            loadCodes);
        await WriteJsonAsync(stagedPlan, plan, cancellationToken);

        RuntimeCandidateFinderReveal stagedReveal =
            await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(stagedCue, cancellationToken);
        RuntimeCandidateFinderReveal finalReveal = BuildFinalFinderReveal(finalPaths);
        string checklist = BuildRuntimeChecklist(finalPaths, finalReveal, loadCodes, readback);
        await WriteTextAsync(
            stagedChecklist,
            checklist,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        string guide = BuildLocationGuideSvg(loadCodes);
        await WriteTextAsync(
            stagedGuide,
            guide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        string outputCueHash = await HashFileAsync(stagedCue, cancellationToken);
        string planHash = await HashFileAsync(stagedPlan, cancellationToken);
        string checklistHash = await HashFileAsync(stagedChecklist, cancellationToken);
        string guideHash = await HashFileAsync(stagedGuide, cancellationToken);
        string finderHash = await HashFileAsync(stagedReveal.HelperPath, cancellationToken);
        UnusedLevel65ZeroEggRuntimeCandidateReceipt receipt = BuildReceipt(
            workspaceRoot,
            baseImage,
            baseCue,
            finalPaths,
            readback,
            rebuiltRawSectorCount,
            outputCueHash,
            planHash,
            checklistHash,
            guideHash,
            finderHash,
            rollbackRecoveryVerified);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);

        VerifySevenFiles(stagedDirectory, finalPaths);
        VerifyTextReadback(stagedPlan, JsonSerializer.Serialize(plan, JsonOptions) + "\n", "staged zero-egg plan");
        VerifyTextReadback(stagedReceipt, JsonSerializer.Serialize(receipt, JsonOptions) + "\n", "staged zero-egg receipt");
        VerifyChecklist(checklist, finalReveal, loadCodes, finalPaths);
        VerifyLocationGuide(guide);
        return new(plan, receipt, stagedReveal, readback, rebuiltRawSectorCount);
    }

    private static UnusedLevel65ZeroEggRuntimeCandidateReceipt BuildReceipt(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65ZeroEggRuntimeCandidatePaths finalPaths,
        CandidateReadback readback,
        int rebuiltRawSectorCount,
        string outputCueHash,
        string planHash,
        string checklistHash,
        string guideHash,
        string finderHash,
        bool rollbackRecoveryVerified) =>
        new(
            ReceiptSchemaVersion,
            ProfileId,
            workspaceRoot,
            baseImage,
            baseCue,
            finalPaths.OutputDirectoryPath,
            finalPaths.OutputImagePath,
            finalPaths.OutputCuePath,
            finalPaths.ConstructionPlanPath,
            finalPaths.StaticReadbackReceiptPath,
            finalPaths.RuntimeChecklistPath,
            finalPaths.LocationGuidePath,
            finalPaths.FinderHelperPath,
            BaseImageSha256,
            BaseCueSha256,
            readback.OutputImageSha256,
            outputCueHash,
            planHash,
            checklistHash,
            guideHash,
            finderHash,
            readback.SourceExecutableSha256,
            readback.OutputExecutableSha256,
            readback.SourceId65DataSha256,
            readback.OutputId65DataSha256,
            readback.SourceWadSha256,
            readback.OutputWadSha256,
            readback.OutputHeaderSha256,
            readback.OutputActorSubfileSha256,
            readback.OutputSceneSubfileSha256,
            readback.OutputObjectTableSha256,
            readback.OrderedTransactionSha256,
            readback.GuardedLogicalWadBytes,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.RawSectorDiffs.Count,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            ExactFiveRangeTransactionVerified: true,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FullImageReadbackVerified: true,
            ExecutableReadbackVerified: true,
            Id65DataReadbackVerified: true,
            FullWadReadbackVerified: true,
            ObjectCountAndAllOtherRowsPreserved: true,
            ThiefPrivateBlockPathAndFixupsPreserved: true,
            Class21And22ObjectRowsAbsent: true,
            RetailTerrainCollisionTextureMusicAndSpawnPreserved: true,
            NoMemoryCardOrSaveAuthorized: true,
            RawSectorIntegrityVerified: true,
            BaseCandidatePreserved: true,
            CanonicalReceiptVerified: true,
            SevenFilePublicationVerified: true,
            RollbackRecoveryVerified: rollbackRecoveryVerified,
            FullDirectoryRollbackVerified: rollbackRecoveryVerified,
            FinderHandoffVerified: true,
            DisposableRuntimeCandidateAuthorized: true,
            RuntimeProofComplete: false,
            AppEnabled: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);

    private static async Task<CandidateReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        RequireMode2Layout(baseLayout, "zero-egg base image");
        RequireMode2Layout(outputLayout, "zero-egg output image");
        if (baseLayout != outputLayout)
            throw new InvalidDataException("The zero-egg output changed the exact disc layout.");

        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The zero-egg output changed the image length.");

        DiscFileRecord baseExecutable = RequireRootFile(
            baseline, baseLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        DiscFileRecord outputExecutableRecord = RequireRootFile(
            output, outputLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        DiscFileRecord baseWad = RequireRootFile(
            baseline, baseLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord outputWad = RequireRootFile(
            output, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        if (baseExecutable != outputExecutableRecord || baseWad != outputWad)
            throw new InvalidDataException("The zero-egg transaction moved or resized SCUS_942.28 or WAD.WAD.");

        byte[] sourceExecutable = DiscImage.ReadFileBytes(
            baseline, baseLayout, baseExecutable.Lba, 0, baseExecutable.Size);
        byte[] outputExecutable = DiscImage.ReadFileBytes(
            output, outputLayout, outputExecutableRecord.Lba, 0, outputExecutableRecord.Size);
        RequireHash(Hash(sourceExecutable), SourceExecutableSha256, "source SCUS readback");
        RequireHash(Hash(outputExecutable), OutputExecutableSha256, "output SCUS readback");
        if (!sourceExecutable.SequenceEqual(outputExecutable))
            throw new InvalidDataException("The zero-egg WAD-only transaction changed SCUS_942.28.");

        byte[] sourceData = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, DataWadOffset, DataByteLength);
        byte[] outputData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, DataWadOffset, DataByteLength);
        string sourceDataHash = Hash(sourceData);
        string outputDataHash = Hash(outputData);
        RequireHash(sourceDataHash, SourceId65DataSha256, "source ID65 row-80 readback");
        RequireHash(outputDataHash, OutputId65DataSha256, "output ID65 row-80 readback");

        byte[] expectedOutputData = ApplyWadPatches(sourceData, patches, reverse: false);
        byte[] inverseSourceData = ApplyWadPatches(outputData, patches, reverse: true);
        if (!outputData.SequenceEqual(expectedOutputData) || !inverseSourceData.SequenceEqual(sourceData))
            throw new InvalidDataException("The zero-egg output lost exact forward/inverse five-range identity.");
        int changedLogicalBytes = CompareId65LogicalDiff(sourceData, outputData, patches);

        string outputHeaderHash = Hash(outputData.AsSpan(0, HeaderByteLength));
        string outputActorHash = Hash(outputData.AsSpan(ActorSubfileRelativeOffset, ActorSubfileByteLength));
        string outputSceneHash = Hash(outputData.AsSpan(SceneSubfileRelativeOffset, SceneSubfileByteLength));
        int objectTableRelativeOffset = SceneSubfileRelativeOffset + ObjectTableSceneOffset;
        int objectTableByteLength = ObjectCount * RecordByteLength;
        byte[] sourceObjectTable = sourceData.AsSpan(objectTableRelativeOffset, objectTableByteLength).ToArray();
        byte[] outputObjectTable = outputData.AsSpan(objectTableRelativeOffset, objectTableByteLength).ToArray();
        string outputObjectTableHash = Hash(outputObjectTable);
        RequireHash(outputHeaderHash, OutputHeaderSha256, "output ID65 0x200-byte header");
        RequireHash(outputActorHash, OutputActorSubfileSha256, "output ID65 actor subfile");
        RequireHash(outputSceneHash, OutputSceneSubfileSha256, "output ID65 scene subfile");
        RequireHash(Hash(sourceObjectTable), SourceObjectTableSha256, "source ID65 object table");
        RequireHash(outputObjectTableHash, OutputObjectTableSha256, "output ID65 object table");

        int sourceCount = BinaryPrimitives.ReadInt32LittleEndian(
            sourceData.AsSpan(SceneSubfileRelativeOffset + ObjectTableSceneOffset - 4, 4));
        int outputCount = BinaryPrimitives.ReadInt32LittleEndian(
            outputData.AsSpan(SceneSubfileRelativeOffset + ObjectTableSceneOffset - 4, 4));
        if (sourceCount != ObjectCount || outputCount != ObjectCount)
            throw new InvalidDataException("The zero-egg transaction changed the exact 107-row object count.");
        for (int index = 0; index < ObjectCount; index++)
        {
            ReadOnlySpan<byte> before = sourceObjectTable.AsSpan(index * RecordByteLength, RecordByteLength);
            ReadOnlySpan<byte> after = outputObjectTable.AsSpan(index * RecordByteLength, RecordByteLength);
            if (index == TargetTrueIndex)
            {
                RequireHash(Hash(before), SourceT88RowSha256, "source T88 row");
                RequireHash(Hash(after), OutputT88RowSha256, "output T88 row");
                if (!before.Slice(0x0C, 12).SequenceEqual(after.Slice(0x0C, 12)))
                    throw new InvalidDataException("The zero-egg T88 replacement changed XYZ.");
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException($"The zero-egg transaction changed protected object row T{index}.");
            }

            ushort actorId = BinaryPrimitives.ReadUInt16LittleEndian(after.Slice(0x36, 2));
            if (actorId is 0x0021 or 0x0022)
                throw new InvalidDataException($"Output object row T{index} still contains class 0x{actorId:X4}.");
        }

        VerifyProtectedThiefStorage(sourceData, outputData);

        string sourceWadHash = HashDiscFile(
            baseline, baseLayout, baseWad, cancellationToken);
        string outputWadHash = HashDiscFile(
            output, outputLayout, outputWad, cancellationToken);
        RequireHash(sourceWadHash, ExpectedSourceWadSha256, "source WAD.WAD readback");
        RequireOptionalHash(outputWadHash, ExpectedOutputWadSha256, "output WAD.WAD readback");

        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            ExpectedChangedRawSectorLbas,
            cancellationToken);
        if (physical.RawSectorDiffs.Count != ExpectedChangedRawSectorCount ||
            (ExpectedChangedPhysicalImageBytes >= 0 &&
             physical.ChangedBytes != ExpectedChangedPhysicalImageBytes) ||
            (ExpectedRawSectorDiffs.Length != 0 &&
             !physical.RawSectorDiffs.SequenceEqual(ExpectedRawSectorDiffs)) ||
            physical.RawSectorDiffs.Any(diff =>
                diff.HeaderChangedBytes != 0 ||
                diff.SubheaderChangedBytes != 0 ||
                diff.ReservedChangedBytes != 0 ||
                diff.PayloadChangedBytes == 0))
        {
            throw new InvalidDataException("The zero-egg physical diff escaped exact MODE2 payload/integrity fields.");
        }
        VerifyMode2Form1Classification(output, ExpectedChangedRawSectorLbas);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            ExpectedChangedRawSectorLbas.Select(lba => (lba, 1)));
        if (verified != ExpectedChangedRawSectorCount)
            throw new InvalidDataException("The zero-egg output failed exact rebuilt-sector integrity verification.");

        string transactionHash = HashOrderedTransaction(patches);
        RequireOptionalHash(
            transactionHash,
            ExpectedOrderedTransactionSha256,
            "ordered zero-egg transaction");
        string rawDiffHash = HashRawSectorDiffs(physical.RawSectorDiffs);
        string outputImageHash = await HashFileAsync(outputImagePath, cancellationToken);

        return new(
            outputImageHash,
            Hash(sourceExecutable),
            Hash(outputExecutable),
            sourceDataHash,
            outputDataHash,
            sourceWadHash,
            outputWadHash,
            outputHeaderHash,
            outputActorHash,
            outputSceneHash,
            outputObjectTableHash,
            transactionHash,
            patches.Sum(patch => patch.ByteLength),
            changedLogicalBytes,
            physical.ChangedBytes,
            physical.RawSectorDiffs,
            rawDiffHash);
    }

    private static UnusedLevel65ZeroEggRuntimeCandidatePlan BuildPlan(
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches,
        string baseCueSha256,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        byte[] sourceT88 = Convert.FromHexString(patches[3].BeforeHex);
        return new(
            PlanSchemaVersion,
            ProfileId,
            BaseImageSha256,
            baseCueSha256,
            readback.SourceExecutableSha256,
            readback.OutputExecutableSha256,
            readback.SourceId65DataSha256,
            readback.OutputId65DataSha256,
            readback.SourceWadSha256,
            readback.OutputWadSha256,
            readback.OutputHeaderSha256,
            readback.OutputActorSubfileSha256,
            readback.OutputSceneSubfileSha256,
            readback.OutputObjectTableSha256,
            SourceT88RowSha256,
            OutputT88RowSha256,
            SourceGrassRowSha256,
            SourceGrassPackageSha256,
            SourceGrassPropertiesSha256,
            ThiefPrivateBlockSha256,
            ThiefPathSha256,
            FixupComponentSha256,
            LevelId: 65,
            ContinuousLevelIndex: 35,
            TargetTrueIndex,
            DonorTrueIndex,
            TargetActorRootIndex,
            ActorId: GrassActorId,
            PreservedRawX: BinaryPrimitives.ReadInt32LittleEndian(sourceT88.AsSpan(0x0C, 4)),
            PreservedRawY: BinaryPrimitives.ReadInt32LittleEndian(sourceT88.AsSpan(0x10, 4)),
            PreservedRawZ: BinaryPrimitives.ReadInt32LittleEndian(sourceT88.AsSpan(0x14, 4)),
            ObjectCount,
            patches,
            readback.OrderedTransactionSha256,
            readback.GuardedLogicalWadBytes,
            readback.ChangedLogicalWadBytes,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            readback.RawSectorDiffSha256,
            loadCodes,
            ExactFiveRangeTransactionVerified: true,
            ObjectCountPreserved: true,
            T88XyzPreserved: true,
            AllOtherObjectRowsPreserved: true,
            ThiefPrivateBlockPathAndFixupsPreserved: true,
            Class21And22ObjectRowsAbsent: true,
            ExecutablePreserved: true,
            RetailTerrainCollisionTextureMusicAndSpawnPreserved: true,
            NoMemoryCardOrSaveAuthorized: true,
            Mode2EdcEccRebuilt: true,
            RequiresDuckStationRuntimeProof: true,
            DisposableRuntimeCandidateAuthorized: true,
            RuntimeProofComplete: false,
            AppEnabled: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
    }

    private static IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> BuildPatches(
        string baseImagePath)
    {
        DiscLayout layout = DiscImage.DetectLayout(baseImagePath);
        RequireMode2Layout(layout, "zero-egg patch source");
        using FileStream image = File.OpenRead(baseImagePath);
        _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);

        byte[] beforeRoot = ReadWad(image, layout, ActorRootSlotWadOffset, 4);
        byte[] beforeActorId = ReadWad(image, layout, ActorIdSlotWadOffset, 2);
        byte[] beforePackage = ReadWad(image, layout, GrassPackageTargetWadOffset, 0x174);
        byte[] beforeT88 = ReadWad(image, layout, T88RowWadOffset, RecordByteLength);
        byte[] beforeProperties = ReadWad(image, layout, GrassPropertiesTargetWadOffset, 8);
        byte[] sourcePackage = ReadWad(image, layout, GrassPackageSourceWadOffset, 0x174);
        byte[] sourceRow = ReadWad(image, layout, GrassRowSourceWadOffset, RecordByteLength);
        byte[] sourceProperties = ReadWad(image, layout, GrassPropertiesSourceWadOffset, 8);

        RequireHash(Hash(beforeRoot), "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119", "root-37 preimage");
        RequireHash(Hash(beforeActorId), "96a296d224f285c67bee93c30f8a309157f0daa35dc5b87e410b78630a09cfc7", "actor-id-37 preimage");
        RequireHash(Hash(beforePackage), "3efddf6dfe905d7626ce129093eba0c053415057485f175ae47fbd9f5781644b", "actor-package tail preimage");
        RequireHash(Hash(beforeT88), SourceT88RowSha256, "Town Square T88 preimage");
        RequireHash(Hash(beforeProperties), "af5570f5a1810b7af78caf4bc70a660f0df51e42baf91d4de5b2328de0e83dfc", "Grass-properties tail preimage");
        RequireHash(Hash(sourcePackage), SourceGrassPackageSha256, "Artisans Grass package donor");
        RequireHash(Hash(sourceRow), SourceGrassRowSha256, "Artisans Grass row donor");
        RequireHash(Hash(sourceProperties), SourceGrassPropertiesSha256, "Artisans Grass properties donor");

        if (BinaryPrimitives.ReadUInt32LittleEndian(beforeT88) != 0x5AFC ||
            BinaryPrimitives.ReadUInt16LittleEndian(beforeT88.AsSpan(0x36, 2)) != 0x0021 ||
            beforeT88[0x50] != 0x20 || beforeT88[0x52] != 0x30 || beforeT88[0x53] != 0x22 ||
            BinaryPrimitives.ReadUInt32LittleEndian(sourceRow) != 0x11ACC ||
            BinaryPrimitives.ReadUInt16LittleEndian(sourceRow.AsSpan(0x36, 2)) != GrassActorId ||
            sourceRow[0x50] != 0x20 || sourceRow[0x52] != 0x10 || sourceRow[0x53] != 0xFF)
        {
            throw new InvalidDataException("The exact T88 thief or Artisans Grass source fingerprint changed.");
        }

        byte[] outputT88 = sourceRow.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outputT88, GrassPropertiesSceneOffset);
        beforeT88.AsSpan(0x0C, 12).CopyTo(outputT88.AsSpan(0x0C, 12));
        RequireHash(Hash(outputT88), OutputT88RowSha256, "derived output T88 Grass row");

        UnusedLevel65ZeroEggRuntimeCandidatePatch[] patches =
        [
            Patch("ID65 actor root 37", ActorRootSlotWadOffset, beforeRoot, UInt32Bytes(GrassActorPackageRelativeOffset)),
            Patch("ID65 actor id 37", ActorIdSlotWadOffset, beforeActorId, UInt16Bytes(GrassActorId)),
            Patch("ID65 actor 0x01F5 package", GrassPackageTargetWadOffset, beforePackage, sourcePackage),
            Patch("ID65 T88 Egg Thief to Artisans Grass preserving XYZ", T88RowWadOffset, beforeT88, outputT88),
            Patch("ID65 exact Grass properties", GrassPropertiesTargetWadOffset, beforeProperties, sourceProperties)
        ];
        if (patches.Sum(patch => patch.ByteLength) != ExpectedGuardedLogicalWadBytes ||
            patches.Sum(patch => patch.ChangedByteCount) != ExpectedChangedLogicalWadBytes ||
            !patches.Select(patch => patch.WadOffset).SequenceEqual(
                new[] { ActorRootSlotWadOffset, ActorIdSlotWadOffset, GrassPackageTargetWadOffset, T88RowWadOffset, GrassPropertiesTargetWadOffset }) ||
            !patches.Select(patch => patch.ChangedByteCount).SequenceEqual(new[] { 3, 2, 260, 9, 2 }))
        {
            throw new InvalidDataException("The exact five-range zero-egg transaction shape changed.");
        }

        byte[] row80 = ReadWad(image, layout, DataWadOffset, DataByteLength);
        VerifyProtectedThiefStorage(row80, row80);
        return patches;
    }

    private static UnusedLevel65ZeroEggRuntimeCandidatePatch Patch(
        string name,
        long wadOffset,
        byte[] before,
        byte[] after)
    {
        if (before.Length != after.Length || before.SequenceEqual(after))
            throw new InvalidDataException($"Zero-egg patch '{name}' has an invalid or redundant range.");
        return new(
            name,
            wadOffset,
            before.Length,
            Convert.ToHexString(before),
            Convert.ToHexString(after),
            before.Zip(after).Count(pair => pair.First != pair.Second),
            Hash(before),
            Hash(after));
    }

    private static byte[] ApplyWadPatches(
        byte[] data,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches,
        bool reverse)
    {
        byte[] result = data.ToArray();
        foreach (UnusedLevel65ZeroEggRuntimeCandidatePatch patch in patches)
        {
            int relativeOffset = checked((int)(patch.WadOffset - DataWadOffset));
            byte[] before = Convert.FromHexString(reverse ? patch.AfterHex : patch.BeforeHex);
            byte[] after = Convert.FromHexString(reverse ? patch.BeforeHex : patch.AfterHex);
            if (relativeOffset < 0 || relativeOffset + patch.ByteLength > result.Length ||
                before.Length != patch.ByteLength || after.Length != patch.ByteLength ||
                !result.AsSpan(relativeOffset, patch.ByteLength).SequenceEqual(before))
            {
                throw new InvalidDataException($"Zero-egg patch '{patch.Name}' failed exact in-memory preimage validation.");
            }
            after.CopyTo(result, relativeOffset);
        }
        return result;
    }

    private static int CompareId65LogicalDiff(
        ReadOnlySpan<byte> baseline,
        ReadOnlySpan<byte> output,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches)
    {
        if (baseline.Length != DataByteLength || output.Length != DataByteLength)
            throw new InvalidDataException("The ID65 row-80 logical comparison has incompatible lengths.");
        Dictionary<int, (byte Before, byte After)> allowed = [];
        foreach (UnusedLevel65ZeroEggRuntimeCandidatePatch patch in patches)
        {
            int start = checked((int)(patch.WadOffset - DataWadOffset));
            byte[] before = Convert.FromHexString(patch.BeforeHex);
            byte[] after = Convert.FromHexString(patch.AfterHex);
            for (int index = 0; index < patch.ByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                if (!allowed.TryAdd(start + index, (before[index], after[index])))
                    throw new InvalidDataException("The zero-egg transaction contains overlapping changed bytes.");
            }
        }

        int changed = 0;
        for (int index = 0; index < baseline.Length; index++)
        {
            if (baseline[index] == output[index])
                continue;
            changed++;
            if (!allowed.TryGetValue(index, out (byte Before, byte After) expected) ||
                baseline[index] != expected.Before ||
                output[index] != expected.After)
            {
                throw new InvalidDataException($"The zero-egg transaction changed unauthorized ID65 byte +0x{index:X}.");
            }
        }
        if (changed != ExpectedChangedLogicalWadBytes || changed != allowed.Count)
            throw new InvalidDataException($"The zero-egg WAD diff changed {changed} bytes, expected 276.");
        return changed;
    }

    private static void VerifyProtectedThiefStorage(
        ReadOnlySpan<byte> baselineData,
        ReadOnlySpan<byte> outputData)
    {
        int privateOffset = checked((int)(ThiefPrivateBlockWadOffset - DataWadOffset));
        int pathOffset = checked((int)(ThiefPathWadOffset - DataWadOffset));
        int fixupOffset = checked((int)(FixupComponentWadOffset - DataWadOffset));
        ReadOnlySpan<byte> beforePrivate = baselineData.Slice(privateOffset, ThiefPrivateBlockByteLength);
        ReadOnlySpan<byte> afterPrivate = outputData.Slice(privateOffset, ThiefPrivateBlockByteLength);
        ReadOnlySpan<byte> beforePath = baselineData.Slice(pathOffset, ThiefPathByteLength);
        ReadOnlySpan<byte> afterPath = outputData.Slice(pathOffset, ThiefPathByteLength);
        ReadOnlySpan<byte> beforeFixups = baselineData.Slice(fixupOffset, FixupComponentByteLength);
        ReadOnlySpan<byte> afterFixups = outputData.Slice(fixupOffset, FixupComponentByteLength);
        RequireHash(Hash(beforePrivate), ThiefPrivateBlockSha256, "source thief private block");
        RequireHash(Hash(beforePath), ThiefPathSha256, "source thief path");
        RequireHash(Hash(beforeFixups), FixupComponentSha256, "source scene fixup component");
        if (!beforePrivate.SequenceEqual(afterPrivate) ||
            !beforePath.SequenceEqual(afterPath) ||
            !beforeFixups.SequenceEqual(afterFixups))
        {
            throw new InvalidDataException("The zero-egg transaction changed the retired thief private block, path, or fixups.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(beforePrivate) != 0x5B58 ||
            beforePath[0] != 13 ||
            BinaryPrimitives.ReadUInt32LittleEndian(beforeFixups) != 0x81)
        {
            throw new InvalidDataException("The pinned thief private-path/fixup grammar changed.");
        }
        int t88PointerField = ObjectTableSceneOffset + (TargetTrueIndex * RecordByteLength);
        int t88Fixups = 0;
        int privateFixups = 0;
        for (int index = 0; index < 0x81; index++)
        {
            int field = BinaryPrimitives.ReadInt32LittleEndian(beforeFixups.Slice(4 + (index * 4), 4));
            if (field == t88PointerField)
                t88Fixups++;
            if (field == 0x5AFC)
                privateFixups++;
        }
        if (t88Fixups != 1 || privateFixups != 1)
            throw new InvalidDataException("T88 and its retired thief-private pointer are not each represented by one preserved fixup.");
    }

    private static byte[] ReadWad(
        FileStream image,
        DiscLayout layout,
        long wadOffset,
        int byteLength) =>
        DiscImage.ReadFileBytes(image, layout, WadLba, wadOffset, byteLength);

    private static byte[] UInt32Bytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt16Bytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static string HashOrderedTransaction(
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidatePatch> patches)
    {
        StringBuilder canonical = new("ID65-ZERO-EGG-WAD-DIFF-v1\n");
        foreach (UnusedLevel65ZeroEggRuntimeCandidatePatch patch in patches)
        {
            canonical.Append(patch.Name).Append('\n')
                .Append(patch.WadOffset.ToString("X")).Append('\n')
                .Append(patch.ByteLength).Append('\n')
                .Append(patch.BeforeSha256).Append('\n')
                .Append(patch.AfterSha256).Append('\n')
                .Append(patch.ChangedByteCount).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static string HashDiscFile(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord record,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        const int chunkBytes = 128 * 1024;
        for (long offset = 0; offset < record.Size; offset += chunkBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int take = checked((int)Math.Min(chunkBytes, record.Size - offset));
            hash.AppendData(DiscImage.ReadFileBytes(image, layout, record.Lba, offset, take));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The zero-egg physical image comparison has incompatible lengths.");
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        List<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> diffs = [];
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
            ReadRawSectorExactly(baseline, before, lba, "zero-egg baseline");
            ReadRawSectorExactly(output, after, lba, "zero-egg output");
            if (before.AsSpan().SequenceEqual(after))
                continue;
            if (!allowed.Contains(lba))
                throw new InvalidDataException($"Physical image bytes changed outside the exact five zero-egg WAD sectors at LBA {lba}.");

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
            changedBytes += total;
            diffs.Add(new(lba, header, subheader, payload, edc, reserved, eccP, eccQ, total));
        }
        if (!diffs.Select(diff => diff.RawSectorLba).SequenceEqual(allowedRawSectorLbas))
            throw new InvalidDataException("The physical changed-sector set does not exactly equal the five-range zero-egg sector set.");
        return new(changedBytes, diffs);
    }

    private static void VerifyMode2Form1Classification(
        FileStream output,
        IReadOnlyList<int> changedRawSectorLbas)
    {
        byte[] raw = new byte[RawSectorByteLength];
        foreach (int lba in changedRawSectorLbas)
        {
            ReadRawSectorExactly(output, raw, lba, "zero-egg output");
            if (raw[15] != 0x02 ||
                !raw.AsSpan(16, 4).SequenceEqual(raw.AsSpan(20, 4)) ||
                (raw[18] & 0x08) == 0 ||
                (raw[18] & 0x20) != 0)
            {
                throw new InvalidDataException($"Raw LBA {lba} is not exact MODE2 Form 1 data.");
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
        if (offset + RawSectorByteLength > stream.Length)
            throw new EndOfStreamException($"The {label} raw-sector read exceeds LBA {lba}.");
        stream.Position = offset;
        stream.ReadExactly(destination);
    }

    private static string HashRawSectorDiffs(
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff diff in diffs)
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

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        if (loadCodes.Count != 4 ||
            !loadCodes.Select(code => code.TestName).SequenceEqual(
                new[] { "ID65 candidate", "Retail Town Square", "Gnasty's Loot", "Sunny Flight" }) ||
            !loadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }) ||
            loadCodes[0].InputCode != "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down")
        {
            throw new InvalidDataException("The exact four ID65 comparison load codes changed.");
        }
    }

    private static string BuildRuntimeChecklist(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        CandidateReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Zero-Egg T88 Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine("Disposable candidate only — runtime pending, unpromoted, and excluded from App/Create BIN/release.");
        builder.AppendLine();
        builder.AppendLine("## Frozen candidate");
        builder.AppendLine();
        builder.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`");
        builder.AppendLine($"- SCUS SHA-256 (preserved): `{readback.OutputExecutableSha256}`");
        builder.AppendLine($"- ID65 row-80 SHA-256: `{readback.OutputId65DataSha256}`");
        builder.AppendLine($"- WAD.WAD SHA-256: `{readback.OutputWadSha256}`");
        builder.AppendLine($"- Ordered five-range transaction SHA-256: `{readback.OrderedTransactionSha256}`");
        builder.AppendLine($"- Raw-sector diff SHA-256: `{readback.RawSectorDiffSha256}`");
        builder.AppendLine("- Exact logical boundary: five guarded WAD ranges, 474 guarded bytes, 276 changed bytes.");
        builder.AppendLine("- Exact physical boundary: raw LBAs 53906, 54833, 54837, 54838, and 54850; MODE2 EDC/ECC rebuilt.");
        builder.AppendLine("- T88 preserves raw XYZ (96850, 141732, 12248) while changing class 0x0021 Egg Thief to controller-free class 0x01F5 Artisans Grass.");
        builder.AppendLine("- Object count stays 107; all other rows, the old thief private block/path/fixups, SCUS, retail data, terrain, collision, textures, music, spawn, and save code remain byte-identical.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        builder.AppendLine("## Emulator isolation — required");
        builder.AppendLine();
        builder.AppendLine("- Use the isolated DuckStation profile only.");
        builder.AppendLine("- Memory Card 1: None.");
        builder.AppendLine("- Memory Card 2: None.");
        builder.AppendLine("- Do not save, load, create, or format a memory card.");
        builder.AppendLine("- Cold boot the CUE, never the BIN. Do not use a save state or cheats.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## ID65 zero-egg acceptance");
        builder.AppendLine();
        builder.AppendLine("1. Cold boot the exact candidate CUE with both card slots None. Load ID65 and verify normal control, camera, audio, terrain, collision, and spawn.");
        builder.AppendLine("2. Travel to the former T88 Egg Thief location. Confirm one Artisans Grass object is visible at the native T88 XYZ, remains stationary, and does not chase, flee, drop, or spawn an egg.");
        builder.AppendLine("3. Circle the Grass at close and medium range, turn the camera away, leave its culling range, and return. The normal and far-LOD Grass must redraw stably with no flicker, corruption, hang, or crash.");
        builder.AppendLine("4. Traverse the old thief route and inspect the scene from several angles. No live class-0x0021 thief and no loader-created/carried class-0x0022 egg may be visible or behave at any point.");
        builder.AppendLine("5. Pause and open Inventory before and after visiting T88. The egg total/collected state must remain zero with no hidden increment, pickup prompt, or completion side effect.");
        builder.AppendLine("6. Die once and respawn. Control, Grass rendering/culling, zero-egg Inventory state, terrain, collision, music, and spawn must remain stable.");
        builder.AppendLine("7. Leave and re-enter ID65 in the same session, then repeat the T88/Inventory check. Class 0x0021/0x0022 behavior must remain absent and Grass must return at the same XYZ.");
        builder.AppendLine("8. Cold reset and repeat the short T88 check. Do not save or insert a card.");
        builder.AppendLine();
        builder.AppendLine("## Retail controls");
        builder.AppendLine();
        builder.AppendLine("Cold boot Retail Town Square separately and confirm its native Egg Thief and carried egg still render and behave. Gnasty's Loot and Sunny Flight must also remain independently loadable. Do not reuse the ID65 run or a save state.");
        builder.AppendLine();
        builder.AppendLine("## Promotion boundary");
        builder.AppendLine();
        builder.AppendLine("Runtime proof is incomplete. Do not copy this transaction into the App, normal Create BIN, release paths, totals, or save ownership until every ID65 and retail control gate passes and a separate promotion is authorized.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        VerifyLoadCodes(loadCodes);
        return """
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1000" viewBox="0 0 1200 1000">
  <rect width="1200" height="1000" fill="#07101f"/>
  <rect x="44" y="40" width="1112" height="920" rx="28" fill="#101c32" stroke="#73df9b" stroke-width="3"/>
  <text x="82" y="102" fill="#f8fbff" font-family="Helvetica,Arial,sans-serif" font-size="36" font-weight="700">ID65 ZERO-EGG T88 — DISPOSABLE RUNTIME GATE</text>
  <text x="82" y="143" fill="#a9f2c4" font-family="Helvetica,Arial,sans-serif" font-size="21">Egg Thief class 0x0021 → Artisans Grass class 0x01F5 • T88 XYZ preserved</text>
  <rect x="82" y="178" width="1036" height="126" rx="18" fill="#172845" stroke="#ffcf5a" stroke-width="2"/>
  <text x="112" y="221" fill="#ffdf83" font-family="Helvetica,Arial,sans-serif" font-size="25" font-weight="700">NO-CARD / NO-SAVE ISOLATION</text>
  <text x="112" y="260" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Memory Card 1: None   •   Memory Card 2: None   •   Do not save, load, create, or format</text>
  <text x="112" y="289" fill="#b9cbe9" font-family="Helvetica,Arial,sans-serif" font-size="19">Cold boot the CUE. No save states or cheats. Inventory is observation-only.</text>
  <text x="82" y="352" fill="#73df9b" font-family="Helvetica,Arial,sans-serif" font-size="28" font-weight="700">T88 runtime-visible boundaries</text>
  <circle cx="105" cy="399" r="18" fill="#73df9b"/><text x="99" y="407" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">1</text>
  <text x="143" y="407" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="21">Grass at raw XYZ (96850, 141732, 12248); stable normal + far-LOD culling.</text>
  <circle cx="105" cy="455" r="18" fill="#73df9b"/><text x="99" y="463" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">2</text>
  <text x="143" y="463" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="21">No thief chase/flee behavior and no visible or carried class-0x0022 egg.</text>
  <circle cx="105" cy="511" r="18" fill="#73df9b"/><text x="99" y="519" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">3</text>
  <text x="143" y="519" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="21">Pause + Inventory: zero egg state; no hidden pickup or completion side effect.</text>
  <circle cx="105" cy="567" r="18" fill="#73df9b"/><text x="99" y="575" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">4</text>
  <text x="143" y="575" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="21">Die/respawn, leave/re-enter, cold reset: Grass and zero-egg state stay stable.</text>
  <text x="82" y="644" fill="#73df9b" font-family="Helvetica,Arial,sans-serif" font-size="28" font-weight="700">Exact comparison codes — separate cold runs</text>
  <rect x="82" y="674" width="1036" height="166" rx="16" fill="#0b1629" stroke="#375a86" stroke-width="2"/>
  <text x="110" y="716" fill="#ffffff" font-family="Menlo,monospace" font-size="19">ID65 candidate       • Level 65 • Left, then Down</text>
  <text x="110" y="752" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Retail Town Square   • Level 13 • thief + carried egg must remain native</text>
  <text x="110" y="788" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Gnasty's Loot        • Level 64 • independent cold-load control</text>
  <text x="110" y="824" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Sunny Flight         • Level 15 • independent cold-load control</text>
  <rect x="82" y="872" width="1036" height="56" rx="14" fill="#481b2a" stroke="#ff6d91" stroke-width="2"/>
  <text x="110" y="907" fill="#ffd7e1" font-family="Helvetica,Arial,sans-serif" font-size="21" font-weight="700">RUNTIME PENDING • UNPROMOTED • NO APP / CREATE BIN / RELEASE INTEGRATION</text>
</svg>
""";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal reveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, reveal, loadCodes);
        string[] required =
        [
            "Memory Card 1: None",
            "Memory Card 2: None",
            "Do not save, load, create, or format",
            "Grass object is visible",
            "class-0x0021 thief",
            "class-0x0022 egg",
            "Pause and open Inventory",
            "Die once and respawn",
            "Leave and re-enter ID65",
            "Retail Town Square",
            "Runtime proof is incomplete",
            "normal Create BIN",
            Path.GetFileName(paths.OutputCuePath)
        ];
        if (required.Any(text => !checklist.Contains(text, StringComparison.Ordinal)))
            throw new InvalidDataException("The zero-egg runtime checklist lost a required no-card or acceptance gate.");
    }

    private static void VerifyLocationGuide(string svg)
    {
        string[] required =
        [
            "width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\"",
            "Memory Card 1: None",
            "Memory Card 2: None",
            "Do not save, load, create, or format",
            "raw XYZ (96850, 141732, 12248)",
            "class-0x0022 egg",
            "Pause + Inventory",
            "Die/respawn, leave/re-enter, cold reset",
            "Level 65",
            "Level 13",
            "Level 64",
            "Level 15",
            "RUNTIME PENDING",
            "NO APP / CREATE BIN / RELEASE INTEGRATION"
        ];
        if (required.Any(text => !svg.Contains(text, StringComparison.Ordinal)))
            throw new InvalidDataException("The 1200x1000 zero-egg guide lost a required visual gate.");
    }

    private static UnusedLevel65ZeroEggRuntimeCandidateResult BuildResult(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        StagedCandidate staged,
        RuntimeCandidateFinderReveal reveal,
        bool rollbackRecoveryVerified) =>
        new(
            paths,
            staged.Plan,
            staged.Receipt,
            reveal,
            staged.Readback.OutputImageSha256,
            staged.Readback.OutputExecutableSha256,
            staged.Readback.OutputId65DataSha256,
            staged.Readback.OutputWadSha256,
            staged.Readback.OutputHeaderSha256,
            staged.Readback.OutputActorSubfileSha256,
            staged.Readback.OutputSceneSubfileSha256,
            staged.Readback.OutputObjectTableSha256,
            staged.Readback.OrderedTransactionSha256,
            staged.Readback.GuardedLogicalWadBytes,
            staged.Readback.ChangedLogicalWadBytes,
            staged.Readback.ChangedPhysicalImageBytes,
            staged.RebuiltRawSectorCount,
            staged.Readback.RawSectorDiffs.Count,
            staged.Readback.RawSectorDiffs,
            staged.Readback.RawSectorDiffSha256,
            ExactFiveRangeTransactionVerified: true,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FullImageReadbackVerified: true,
            ExecutableReadbackVerified: true,
            Id65DataReadbackVerified: true,
            FullWadReadbackVerified: true,
            ObjectCountAndAllOtherRowsPreserved: true,
            ThiefPrivateBlockPathAndFixupsPreserved: true,
            Class21And22ObjectRowsAbsent: true,
            RetailTerrainCollisionTextureMusicAndSpawnPreserved: true,
            NoMemoryCardOrSaveAuthorized: true,
            RawSectorIntegrityVerified: true,
            BaseCandidatePreserved: true,
            CanonicalReceiptVerified: true,
            SevenFilePublicationVerified: true,
            RollbackRecoveryVerified: rollbackRecoveryVerified,
            FullDirectoryRollbackVerified: rollbackRecoveryVerified,
            FinderHandoffVerified: true,
            DisposableRuntimeCandidateAuthorized: true,
            RuntimeProofComplete: false,
            AppEnabled: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);

    private static void ValidateReceiptIdentity(
        UnusedLevel65ZeroEggRuntimeCandidateReceipt receipt,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        bool exactSourcePaths =
            !string.IsNullOrWhiteSpace(receipt.WorkspaceRoot) &&
            !string.IsNullOrWhiteSpace(receipt.BaseImagePath) &&
            !string.IsNullOrWhiteSpace(receipt.BaseCuePath) &&
            PathEquals(receipt.WorkspaceRoot, Path.GetFullPath(receipt.WorkspaceRoot)) &&
            PathEquals(receipt.BaseImagePath, Path.GetFullPath(receipt.BaseImagePath)) &&
            PathEquals(receipt.BaseCuePath, Path.GetFullPath(receipt.BaseCuePath));
        bool exactFinalPaths =
            PathEquals(receipt.OutputDirectoryPath, paths.OutputDirectoryPath) &&
            PathEquals(receipt.OutputImagePath, paths.OutputImagePath) &&
            PathEquals(receipt.OutputCuePath, paths.OutputCuePath) &&
            PathEquals(receipt.ConstructionPlanPath, paths.ConstructionPlanPath) &&
            PathEquals(receipt.StaticReadbackReceiptPath, paths.StaticReadbackReceiptPath) &&
            PathEquals(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath) &&
            PathEquals(receipt.LocationGuidePath, paths.LocationGuidePath) &&
            PathEquals(receipt.FinderHelperPath, paths.FinderHelperPath);
        bool exactHashes = new[]
        {
            receipt.BaseImageSha256,
            receipt.BaseCueSha256,
            receipt.OutputImageSha256,
            receipt.OutputCueSha256,
            receipt.ConstructionPlanSha256,
            receipt.RuntimeChecklistSha256,
            receipt.LocationGuideSha256,
            receipt.FinderHelperSha256,
            receipt.SourceExecutableSha256,
            receipt.OutputExecutableSha256,
            receipt.SourceId65DataSha256,
            receipt.OutputId65DataSha256,
            receipt.SourceWadSha256,
            receipt.OutputWadSha256,
            receipt.OutputHeaderSha256,
            receipt.OutputActorSubfileSha256,
            receipt.OutputSceneSubfileSha256,
            receipt.OutputObjectTableSha256,
            receipt.OrderedTransactionSha256,
            receipt.RawSectorDiffSha256
        }.All(IsExactSha256);
        bool exactRawDiff =
            receipt.RawSectorDiffs.Count == ExpectedChangedRawSectorCount &&
            receipt.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                .SequenceEqual(ExpectedChangedRawSectorLbas) &&
            (ExpectedRawSectorDiffs.Length == 0 ||
             receipt.RawSectorDiffs.SequenceEqual(ExpectedRawSectorDiffs)) &&
            HashRawSectorDiffs(receipt.RawSectorDiffs) == receipt.RawSectorDiffSha256 &&
            (IsPendingHash(ExpectedRawSectorDiffSha256) ||
             receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256);
        bool exactFlags =
            receipt.ExactFiveRangeTransactionVerified &&
            receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified &&
            receipt.FullImageReadbackVerified &&
            receipt.ExecutableReadbackVerified &&
            receipt.Id65DataReadbackVerified &&
            receipt.FullWadReadbackVerified &&
            receipt.ObjectCountAndAllOtherRowsPreserved &&
            receipt.ThiefPrivateBlockPathAndFixupsPreserved &&
            receipt.Class21And22ObjectRowsAbsent &&
            receipt.RetailTerrainCollisionTextureMusicAndSpawnPreserved &&
            receipt.NoMemoryCardOrSaveAuthorized &&
            receipt.RawSectorIntegrityVerified &&
            receipt.BaseCandidatePreserved &&
            receipt.CanonicalReceiptVerified &&
            receipt.SevenFilePublicationVerified &&
            !receipt.RollbackRecoveryVerified &&
            !receipt.FullDirectoryRollbackVerified &&
            receipt.FinderHandoffVerified &&
            receipt.DisposableRuntimeCandidateAuthorized &&
            !receipt.RuntimeProofComplete &&
            !receipt.AppEnabled &&
            !receipt.NormalCreateBinEnabled &&
            !receipt.PromotionAuthorized;
        if (receipt.SchemaVersion != ReceiptSchemaVersion ||
            receipt.ProfileId != ProfileId ||
            !exactSourcePaths || !exactFinalPaths || !exactHashes || !exactRawDiff || !exactFlags ||
            receipt.BaseImageSha256 != BaseImageSha256 ||
            receipt.BaseCueSha256 != BaseCueSha256 ||
            receipt.SourceExecutableSha256 != SourceExecutableSha256 ||
            receipt.OutputExecutableSha256 != OutputExecutableSha256 ||
            receipt.SourceId65DataSha256 != SourceId65DataSha256 ||
            receipt.OutputId65DataSha256 != OutputId65DataSha256 ||
            receipt.SourceWadSha256 != ExpectedSourceWadSha256 ||
            receipt.OutputHeaderSha256 != OutputHeaderSha256 ||
            receipt.OutputActorSubfileSha256 != OutputActorSubfileSha256 ||
            receipt.OutputSceneSubfileSha256 != OutputSceneSubfileSha256 ||
            receipt.OutputObjectTableSha256 != OutputObjectTableSha256 ||
            receipt.GuardedLogicalWadBytes != ExpectedGuardedLogicalWadBytes ||
            receipt.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
            (ExpectedChangedPhysicalImageBytes >= 0 &&
             receipt.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes) ||
            receipt.RebuiltRawSectorCount != ExpectedChangedRawSectorCount ||
            receipt.ChangedRawSectorCount != ExpectedChangedRawSectorCount)
        {
            throw new InvalidDataException("The published zero-egg receipt lost an exact path, pin, diff, or fail-closed flag.");
        }
        RequireOptionalHash(receipt.OutputImageSha256, ExpectedOutputImageSha256, "receipt output BIN");
        RequireOptionalHash(receipt.OutputWadSha256, ExpectedOutputWadSha256, "receipt output WAD.WAD");
        RequireOptionalHash(receipt.OrderedTransactionSha256, ExpectedOrderedTransactionSha256, "receipt ordered transaction");
        RequireOptionalHash(receipt.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "receipt raw-sector diff");
    }

    private static void ValidateReceiptReadback(
        UnusedLevel65ZeroEggRuntimeCandidateReceipt receipt,
        CandidateReadback readback)
    {
        if (receipt.OutputImageSha256 != readback.OutputImageSha256 ||
            receipt.SourceExecutableSha256 != readback.SourceExecutableSha256 ||
            receipt.OutputExecutableSha256 != readback.OutputExecutableSha256 ||
            receipt.SourceId65DataSha256 != readback.SourceId65DataSha256 ||
            receipt.OutputId65DataSha256 != readback.OutputId65DataSha256 ||
            receipt.SourceWadSha256 != readback.SourceWadSha256 ||
            receipt.OutputWadSha256 != readback.OutputWadSha256 ||
            receipt.OutputHeaderSha256 != readback.OutputHeaderSha256 ||
            receipt.OutputActorSubfileSha256 != readback.OutputActorSubfileSha256 ||
            receipt.OutputSceneSubfileSha256 != readback.OutputSceneSubfileSha256 ||
            receipt.OutputObjectTableSha256 != readback.OutputObjectTableSha256 ||
            receipt.OrderedTransactionSha256 != readback.OrderedTransactionSha256 ||
            receipt.GuardedLogicalWadBytes != readback.GuardedLogicalWadBytes ||
            receipt.ChangedLogicalWadBytes != readback.ChangedLogicalWadBytes ||
            receipt.ChangedPhysicalImageBytes != readback.ChangedPhysicalImageBytes ||
            receipt.ChangedRawSectorCount != readback.RawSectorDiffs.Count ||
            !receipt.RawSectorDiffs.SequenceEqual(readback.RawSectorDiffs) ||
            receipt.RawSectorDiffSha256 != readback.RawSectorDiffSha256)
        {
            throw new InvalidDataException("The published zero-egg candidate no longer matches its receipt readback.");
        }
    }

    private static void BindSidecarHashes(
        UnusedLevel65ZeroEggRuntimeCandidateReceipt receipt,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        RequireHash(HashFile(paths.OutputImagePath), receipt.OutputImageSha256, "receipt-bound BIN");
        RequireHash(HashFile(paths.OutputCuePath), receipt.OutputCueSha256, "receipt-bound CUE");
        RequireHash(HashFile(paths.ConstructionPlanPath), receipt.ConstructionPlanSha256, "receipt-bound plan");
        RequireHash(HashFile(paths.RuntimeChecklistPath), receipt.RuntimeChecklistSha256, "receipt-bound checklist");
        RequireHash(HashFile(paths.LocationGuidePath), receipt.LocationGuideSha256, "receipt-bound guide");
        RequireHash(HashFile(paths.FinderHelperPath), receipt.FinderHelperSha256, "receipt-bound Finder helper");
    }

    private static void VerifySevenFiles(
        string directory,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            Path.GetFileName(paths.OutputImagePath),
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.ConstructionPlanPath),
            Path.GetFileName(paths.StaticReadbackReceiptPath),
            Path.GetFileName(paths.RuntimeChecklistPath),
            Path.GetFileName(paths.LocationGuidePath),
            Path.GetFileName(paths.FinderHelperPath)
        };
        string[] actual = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (Directory.EnumerateDirectories(directory).Any() ||
            actual.Length != 7 ||
            !actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"The zero-egg candidate artifact set is [{string.Join(',', actual)}], expected exactly seven files.");
        }
    }

    private static void VerifyPublishedCandidate(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published zero-egg candidate directory is missing.");
        VerifySevenFiles(paths.OutputDirectoryPath, paths);
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.OutputCuePath, paths.OutputImagePath, "MODE2/2352");
        BindSidecarHashes(staged.Receipt, paths);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            JsonSerializer.Serialize(staged.Plan, JsonOptions) + "\n",
            "published zero-egg plan");
        VerifyTextReadback(
            paths.StaticReadbackReceiptPath,
            JsonSerializer.Serialize(staged.Receipt, JsonOptions) + "\n",
            "published zero-egg receipt");
        RuntimeCandidateFinderReveal reveal = BuildFinalFinderReveal(paths);
        string checklist = File.ReadAllText(paths.RuntimeChecklistPath);
        VerifyChecklist(checklist, reveal, staged.Plan.LoadCodes, paths);
        VerifyLocationGuide(File.ReadAllText(paths.LocationGuidePath));
        VerifyFinderHelper(paths, staged.Receipt.FinderHelperSha256);
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static void VerifyFinderHelper(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        string expectedSha256)
    {
        if (!OperatingSystem.IsMacOS() ||
            File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
        {
            throw new InvalidDataException("The published zero-egg Finder helper is not exact 0755.");
        }
        RequireHash(HashFile(paths.FinderHelperPath), expectedSha256, "published zero-egg Finder helper");
        VerifyTextReadback(
            paths.FinderHelperPath,
            BuildExpectedFinderHelper(paths.OutputCuePath),
            "published zero-egg Finder helper content");
    }

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

    private static void BeginPublication(
        CandidatePublication publication,
        bool replaceExistingCandidate,
        Action<string>? testStageHook)
    {
        if (publication.HadPreviousCandidate)
        {
            if (!replaceExistingCandidate || !Directory.Exists(publication.OutputDirectoryPath))
                throw new IOException("The zero-egg destination changed before replacement publication.");
            if (Directory.Exists(publication.BackupDirectoryPath))
                throw new IOException("The zero-egg operation backup path already exists.");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            publication.PreviousBackedUp = true;
            WriteOperationJournal(publication, OperationPhasePreviousBackedUp);
            testStageHook?.Invoke("after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The zero-egg destination appeared during staging.");
        }

        if (!Directory.Exists(publication.StagedDirectoryPath))
            throw new IOException("The staged zero-egg candidate disappeared before publication.");
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
                        throw new IOException("The previous zero-egg candidate backup is missing.");
                    if (Directory.Exists(publication.OutputDirectoryPath))
                        throw new IOException("The zero-egg destination is occupied before backup restoration.");
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
                "Zero-egg publication failed and full-directory recovery is incomplete; " +
                "the operation journal and backup were preserved for the next call.",
                new AggregateException([publicationFailure, .. recoveryFailures]));
        }
    }

    private static bool RecoverOwnedOperations(
        string operationsDirectoryPath,
        string expectedOutputDirectoryPath)
    {
        RejectExistingAncestorReparsePoints(
            operationsDirectoryPath,
            "zero-egg operations directory");
        RejectExistingAncestorReparsePoints(
            expectedOutputDirectoryPath,
            "zero-egg publication output");
        if (!Directory.Exists(operationsDirectoryPath))
            return false;
        RejectReparsePoint(operationsDirectoryPath, "zero-egg operations directory");
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
            RejectTreeReparsePoints(expectedOutputDirectoryPath, "stale zero-egg publication output");
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
            throw new InvalidDataException($"The zero-egg operations directory contains a non-owned entry: {operationRoot}");
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale zero-egg operation");
        RejectReparsePoint(operationRoot, "stale zero-egg operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale zero-egg operation lease");
            try
            {
                using FileStream lease = new(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException("A zero-egg candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned zero-egg operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale zero-egg operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A zero-egg operation journal is unreadable.");
        ValidateOperationJournal(journal, operationRoot, operationsDirectoryPath, expectedOutputDirectoryPath);
        RejectUnexpectedDirectoryRoleType(journal.StagedDirectoryPath, "stale zero-egg staged candidate");
        RejectUnexpectedDirectoryRoleType(journal.BackupDirectoryPath, "stale zero-egg backup");
        bool stagedExists = Directory.Exists(journal.StagedDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (stagedExists)
            RejectTreeReparsePoints(journal.StagedDirectoryPath, "stale zero-egg staged candidate");
        if (backupExists)
            RejectTreeReparsePoints(journal.BackupDirectoryPath, "stale zero-egg backup");
        return new(operationRoot, leasePath, journalPath, journal, stagedExists, backupExists);
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
            throw new IOException("Multiple previous zero-egg candidate backups exist; recovery was preserved for audit.");
        if (committed.Length > 1)
            throw new IOException("Multiple committed zero-egg operations claim one target; recovery was preserved for audit.");
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
                throw new InvalidDataException("A no-previous zero-egg operation owns an unexpected backup.");
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp && !operation.Journal.HadPreviousCandidate)
                throw new InvalidDataException("A zero-egg operation reports a backup without a previous candidate.");
        }

        RecoverableOperation? backupToRestore = null;
        RecoverableOperation? committedBackupToDiscard = null;
        bool deleteOutput = false;
        if (committed.Length == 1)
        {
            RecoverableOperation committedOperation = committed[0];
            if (!outputExists)
                throw new IOException("A committed zero-egg operation is missing its candidate directory.");
            if (backups.Length == 1 && !ReferenceEquals(backups[0], committedOperation))
                throw new IOException("An uncommitted zero-egg backup competes with a committed publication.");
            committedBackupToDiscard = backups.SingleOrDefault();
        }
        else if (backups.Length == 1)
        {
            backupToRestore = backups[0];
            if (backupToRestore.Journal.Phase == OperationPhaseRollbackComplete)
                throw new IOException("A rollback-complete zero-egg operation still owns a backup.");
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
                throw new IOException("A previous zero-egg candidate is missing and no unique backup can restore it.");
        }
        else
        {
            bool previousIsAuthoritative = operations.Any(operation =>
                operation.Journal.HadPreviousCandidate &&
                (operation.Journal.Phase == OperationPhaseNewCandidateRemoved ||
                 operation.Journal.Phase == OperationPhaseRollbackComplete ||
                 (operation.Journal.Phase == OperationPhaseStaging && operation.StagedExists)));
            if (!previousIsAuthoritative)
            {
                if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
                    throw new IOException("A stale zero-egg operation cannot prove which published directory is authoritative.");
                bool uncommittedIsAuthoritative = operations.Any(operation =>
                    operation.Journal.Phase == OperationPhaseCandidatePublished ||
                    (operation.Journal.Phase == OperationPhaseStaging && !operation.StagedExists));
                if (!uncommittedIsAuthoritative)
                    throw new IOException("A no-previous zero-egg recovery cannot prove ownership of the output.");
                deleteOutput = true;
            }
        }

        // No filesystem mutation occurs above this line. All journals, roles, and
        // competing backups have been validated as one exact-target recovery group.
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
                throw new IOException("The zero-egg destination is occupied before backup restoration.");
            Directory.Move(backupToRestore.Journal.BackupDirectoryPath, expectedOutputDirectoryPath);
        }
        else if (committedBackupToDiscard != null)
        {
            Directory.Delete(committedBackupToDiscard.Journal.BackupDirectoryPath, recursive: true);
        }
        else if (deleteOutput)
        {
            EnsureExpectedRecoveryOutputPath(expectedOutputDirectoryPath, operations[0].Journal.OutputDirectoryPath);
            Directory.Delete(expectedOutputDirectoryPath, recursive: true);
        }

        foreach (RecoverableOperation operation in operations)
            DeleteRecoveredOperation(operation);
    }

    private static void WriteRecoveredOperationPhase(RecoverableOperation operation, string phase)
    {
        CandidateOperationJournal journal = operation.Journal with { Phase = phase };
        WriteTextDurably(operation.JournalPath, JsonSerializer.Serialize(journal, JsonOptions) + "\n");
    }

    private static void DeleteRecoveredOperation(RecoverableOperation operation)
    {
        if (Directory.Exists(operation.Journal.BackupDirectoryPath))
            throw new IOException("Zero-egg recovery refused to discard an operation while its backup exists.");
        ValidateOperationRootEntries(operation.OperationRootPath);
        Directory.Delete(operation.OperationRootPath, recursive: true);
    }

    private static void ValidateOperationRootEntries(string operationRoot)
    {
        HashSet<string> allowed = new(StringComparer.Ordinal)
        {
            "candidate",
            "previous-candidate",
            "operation.lease",
            OperationJournalFileName
        };
        foreach (string entry in Directory.EnumerateFileSystemEntries(operationRoot))
        {
            RejectReparsePoint(entry, "stale zero-egg operation entry");
            string name = Path.GetFileName(entry);
            if (!allowed.Contains(name))
                throw new InvalidDataException($"A stale zero-egg operation contains an unowned entry: {entry}");
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
                throw new InvalidDataException($"A stale zero-egg operation entry has the wrong role: {entry}");
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
            throw new InvalidOperationException("The zero-egg output has no parent.");
        if (!Directory.Exists(parent))
            return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, outputDirectoryPath))
                continue;
            RejectReparsePoint(entry, "zero-egg publication output");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) == 0)
                throw new InvalidDataException("The zero-egg output path is not a directory.");
            return;
        }
    }

    private static bool IsOwnedOperationDirectory(string operationRoot)
    {
        string name = Path.GetFileName(operationRoot);
        if (!name.StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            return false;
        return Guid.TryParseExact(name[OperationDirectoryPrefix.Length..], "N", out _);
    }

    private static void EnsureExpectedRecoveryOutputPath(string actual, string expected)
    {
        if (!PathEquals(actual, expected))
            throw new InvalidOperationException("A zero-egg recovery target escaped the requested output path.");
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
            throw new InvalidDataException("A stale zero-egg operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal zero-egg operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale zero-egg journal points outside its publication parent.");
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
            throw new InvalidDataException($"The {label} is not in exact canonical form.");
        return value;
    }

    private static void PrepareDestination(string outputDirectoryPath, bool replaceExistingCandidate)
    {
        if (!Directory.Exists(outputDirectoryPath))
            return;
        RejectReparsePoint(outputDirectoryPath, "zero-egg output directory");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The zero-egg candidate directory already exists. Set ReplaceExistingCandidate only for an intentional replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        string[] sources = [baseImage, baseCue];
        if (sources.Any(source =>
                IsDescendantOrEqual(source, paths.OutputDirectoryPath) ||
                IsDescendantOrEqual(paths.OutputDirectoryPath, source)))
        {
            throw new InvalidOperationException("The zero-egg output and source files must have distinct roles.");
        }
        if (PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The zero-egg output and operations directories overlap.");
    }

    private static void RejectUnsafePathTopologyBeforeMutation(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        RejectExistingAncestorReparsePoints(workspaceRoot, "zero-egg workspace root");
        RejectExistingAncestorReparsePoints(baseImage, "zero-egg base BIN");
        RejectExistingAncestorReparsePoints(baseCue, "zero-egg base CUE");
        RejectPublicationPathAncestorReparsePoints(paths);
    }

    private static void RejectPublicationPathAncestorReparsePoints(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ??
            throw new InvalidOperationException("The zero-egg output has no parent.");
        RejectExistingAncestorReparsePoints(outputParent, "zero-egg publication parent");
        RejectExistingAncestorReparsePoints(paths.OutputDirectoryPath, "zero-egg publication output");
        RejectExistingAncestorReparsePoints(paths.OperationsDirectoryPath, "zero-egg operations directory");
        RejectExistingAncestorReparsePoints(GlobalWriterLeasePath(paths), "zero-egg global writer lease");
    }

    private static void RequirePathsInsideOutput(UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
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
            EnsureStrictDescendant(artifact, paths.OutputDirectoryPath, "zero-egg artifact");
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent))
            throw new InvalidOperationException("The zero-egg operations directory escaped the output parent.");
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

    private static GlobalWriterLease AcquireGlobalWriterLease(UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The zero-egg operations directory has no parent.");
        RejectPublicationPathAncestorReparsePoints(paths);
        Directory.CreateDirectory(parent);
        string leasePath = GlobalWriterLeasePath(paths);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "zero-egg global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The zero-egg writer lease path is a directory.");
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
            throw new IOException("The zero-egg global writer lease file could not be opened.", ex);
        }

        try
        {
            RejectReparsePoint(leasePath, "zero-egg global writer lease");
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
                return new GlobalWriterLease(lease);
            }
            int descriptor = checked((int)lease.SafeFileHandle.DangerousGetHandle());
            if (NativeFlock(descriptor, LockExclusive | LockNonBlocking) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                Win32Exception nativeError = new(error);
                if (IsUnixWouldBlock(error))
                    throw CreateActiveWriterLeaseException(nativeError);
                throw new IOException($"The zero-egg global writer OS lease failed (errno {error}: {nativeError.Message}).", nativeError);
            }
            return new GlobalWriterLease(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static string GlobalWriterLeasePath(UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The zero-egg operations directory has no parent.");
        return Path.Combine(parent, GlobalWriterLeaseFileName);
    }

    private static IOException CreateActiveWriterLeaseException(Exception inner) =>
        new(ActiveWriterLeaseMessage, inner);

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
            File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            using (FileStream stream = new(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(flushToDisk: true);
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

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
            throw new InvalidDataException($"The exact {name} ISO extent changed.");
        return record;
    }

    private static void RequireMode2Layout(DiscLayout layout, string label)
    {
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException($"The {label} is not exact MODE2/2352 with user offset 24.");
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

    private static void RequireOptionalHash(string actual, string expected, string label)
    {
        if (!IsPendingHash(expected))
            RequireHash(actual, expected, label);
    }

    private static bool IsPendingHash(string value) =>
        string.Equals(value, "PENDING", StringComparison.Ordinal);

    private static bool IsExactSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TryGetFrozenPublicationWorkspaceRoot(
        UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
        out string workspaceRoot)
    {
        workspaceRoot = "";
        DirectoryInfo output = new(paths.OutputDirectoryPath);
        DirectoryInfo? publicationParent = output.Parent;
        DirectoryInfo? local = publicationParent?.Parent;
        DirectoryInfo? root = local?.Parent;
        if (publicationParent == null || local == null || root == null ||
            !string.Equals(output.Name, OutputDirectoryName, StringComparison.Ordinal) ||
            !string.Equals(publicationParent.Name, "v5-stone-hill-level-replacement", StringComparison.Ordinal) ||
            !string.Equals(local.Name, "_local", StringComparison.Ordinal))
        {
            return false;
        }
        workspaceRoot = root.FullName;
        return true;
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
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
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

    private static void EnsureExactPublicationPath(string path, CandidatePublication publication)
    {
        if (!PathEquals(path, publication.OutputDirectoryPath))
            throw new InvalidOperationException("A zero-egg rollback target escaped its publication path.");
    }

    private static void TryDeleteOwnedOperation(string operationRoot, string operationsDirectoryPath)
    {
        if (!Directory.Exists(operationRoot))
            return;
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed zero-egg operation");
        if (!IsOwnedOperationDirectory(operationRoot))
            throw new InvalidOperationException("Refusing to remove a non-owned zero-egg operation directory.");
        Directory.Delete(operationRoot, recursive: true);
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    private static void RejectReparsePoint(string path, string label)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"The {label} cannot be a symbolic link or reparse point.");
    }

    private static void RejectExistingAncestorReparsePoints(string path, string label)
    {
        string? current = Path.GetFullPath(path);
        while (current != null)
        {
            RejectReparsePoint(current, label);
            current = Path.GetDirectoryName(current);
        }
    }

    private static void EnsureStrictDescendant(string candidate, string parent, string label)
    {
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        if (!fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, PathComparison))
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
            FileStream? owned = Interlocked.Exchange(ref _stream, null);
            if (owned == null)
                return;
            try
            {
                if (OperatingSystem.IsWindows())
                    owned.Unlock(0, 1);
                else
                    _ = NativeFlock(checked((int)owned.SafeFileHandle.DangerousGetHandle()), LockUnlock);
            }
            catch (IOException)
            {
                // Closing the descriptor below still releases either platform lease.
            }
            finally
            {
                owned.Dispose();
            }
        }
    }

    private sealed record StagedCandidate(
        UnusedLevel65ZeroEggRuntimeCandidatePlan Plan,
        UnusedLevel65ZeroEggRuntimeCandidateReceipt Receipt,
        RuntimeCandidateFinderReveal FinderReveal,
        CandidateReadback Readback,
        int RebuiltRawSectorCount);

    private sealed record CandidateReadback(
        string OutputImageSha256,
        string SourceExecutableSha256,
        string OutputExecutableSha256,
        string SourceId65DataSha256,
        string OutputId65DataSha256,
        string SourceWadSha256,
        string OutputWadSha256,
        string OutputHeaderSha256,
        string OutputActorSubfileSha256,
        string OutputSceneSubfileSha256,
        string OutputObjectTableSha256,
        string OrderedTransactionSha256,
        int GuardedLogicalWadBytes,
        int ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        IReadOnlyList<UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
