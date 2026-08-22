using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65ReturnHomeNativeApronRuntimeCandidateRequest(
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths(
    string PublicationParentPath,
    string OutputDirectoryPath,
    string OperationsDirectoryPath,
    string WriterLeasePath,
    string OutputImagePath,
    string OutputCuePath,
    string LabLinkMetadataPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath);

internal sealed record UnusedLevel65ReturnHomeNativeApronPoint(int X, int Y, int Z);

internal sealed record UnusedLevel65ReturnHomeNativeApronRowPatch(
    int TrueIndex,
    string Role,
    long RowWadOffset,
    long XyzWadOffset,
    UnusedLevel65ReturnHomeNativeApronPoint Before,
    UnusedLevel65ReturnHomeNativeApronPoint After,
    string BeforeRowSha256,
    string AfterRowSha256,
    bool OnlyXyzChanged);

internal sealed record UnusedLevel65ReturnHomeNativeApronSupportProof(
    long CollisionComponentWadOffset,
    int CollisionComponentByteLength,
    string CollisionComponentSha256,
    string CollisionHeaderSha256,
    string CollisionTreeSha256,
    string CollisionBlocksSha256,
    int CollisionTriangleIndex,
    string CollisionTriangleHex,
    IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> CollisionTrianglePoints,
    IReadOnlyList<long> CollisionLookupWadOffsets,
    int CollisionAssignment,
    long NormalZ,
    int SurfaceRawZ,
    int LeftMarginRaw,
    int DiagonalMarginRaw,
    string RuntimeEvidenceRelativePath,
    string RuntimeEvidenceSha256,
    bool PointStrictlyInsideNativeTriangle,
    bool NativeTriangleIsExposedTopmostAtPoint,
    bool PositiveWindingTerrainExcluded);

internal sealed record UnusedLevel65ReturnHomeNativeApronLabLinkSummary(
    int TotalGroups,
    int AtomicLinkedMoveGroups,
    int RuntimePendingGroups);

internal sealed record UnusedLevel65ReturnHomeNativeApronLabLinkGroup(
    string Key,
    string Name,
    string Kind,
    bool LinkedMove,
    string Confidence,
    string Basis,
    string Reason,
    IReadOnlyList<int> TrueIndexes,
    IReadOnlyList<string> MemberIds);

internal sealed record UnusedLevel65ReturnHomeNativeApronLabLinkDocument(
    int SchemaVersion,
    string ProfileId,
    string Purpose,
    UnusedLevel65ReturnHomeNativeApronLabLinkSummary Summary,
    IReadOnlyList<UnusedLevel65ReturnHomeNativeApronLabLinkGroup> LinkGroups,
    bool RuntimeVerified,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65ReturnHomeNativeApronRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65ReturnHomeNativeApronPlan(
    int SchemaVersion,
    string ProfileId,
    string StaticOwnershipProfileId,
    string BaseImageSha256,
    string BaseCueSha256,
    string SourceDataSha256,
    string OutputDataSha256,
    string ExecutableSha256,
    string Id65OverlaySha256,
    string RetailTownSquareOverlaySha256,
    string RetailTownSquareDataSha256,
    IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRowPatch> RowPatches,
    UnusedLevel65ReturnHomeNativeApronSupportProof Support,
    UnusedLevel65ReturnHomeNativeApronPoint Landing,
    UnusedLevel65ReturnHomeNativeApronPoint PlayerAnchor,
    double HorizontalDistanceFromPlayerWorld,
    double ThreeDimensionalDistanceFromPlayerWorld,
    int NativeNearTriggerRaw,
    double HorizontalNoImmediateTriggerMarginWorld,
    int ComputedReturnHomeDestinationLevelId,
    int ComputedPauseExitDestinationLevelId,
    int ComputedPauseExitRoute,
    string CanonicalLabGroupSha256,
    IReadOnlyList<int> ChangedDataByteOffsets,
    int LogicalXyzPatchBytes,
    int ChangedLogicalWadBytes,
    int ExecutablePatchBytes,
    int RetailWadPatchBytes,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool ExactT96T97AtomicRelocationVerified,
    bool ExactLabLinkMetadataVerified,
    bool NativeSupportVerified,
    bool LandingAndPlayerAnchorPreserved,
    bool TerrainPreserved,
    bool ExecutablePreserved,
    bool RetailLevelsPreserved,
    bool MemoryCardsRequired,
    bool SavingAuthorized,
    bool RuntimeVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized,
    bool ReleaseAuthorized);

internal sealed record UnusedLevel65ReturnHomeNativeApronReceipt(
    int SchemaVersion,
    string ProfileId,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string LabLinkMetadataPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string OutputImageSha256,
    string OutputCueSha256,
    string LabLinkMetadataSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string FinderHelperSha256,
    string BaseImageSha256,
    string BaseCueSha256,
    string OutputDataSha256,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    UnusedLevel65ReturnHomeNativeApronPlan Plan,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool CanonicalLabMetadataVerified,
    bool CanonicalReceiptVerified,
    bool Mode2IntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool RuntimeVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized,
    bool ReleaseAuthorized);

internal sealed record UnusedLevel65ReturnHomeNativeApronPublishedArtifactHashes(
    string ReceiptSha256,
    string OutputCueSha256,
    string LabLinkMetadataSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string FinderHelperSha256);

internal sealed record UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult(
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths Paths,
    UnusedLevel65ReturnHomeNativeApronPlan Plan,
    UnusedLevel65ReturnHomeNativeApronReceipt Receipt,
    UnusedLevel65ReturnHomeNativeApronPublishedArtifactHashes ArtifactHashes);

/// <summary>
/// Publishes an isolated, disposable Return Home / Pause Exit runtime gate
/// directly from the locked ID65 display-name BIN. Only the XYZ fields of the
/// inherited T96/T97 pair change. Their destination is an exposed native Town
/// Square entrance triangle already present in the locked BIN; no authored or
/// retired positive-winding terrain participates in this candidate.
/// </summary>
internal static class UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-return-home-native-apron-clean-usa-disposable-v1";
    public const string OutputDirectoryName = "unused-level-65-return-home-native-apron";
    public const string OutputPrefix =
        "Unused-Level-65-Return-Home-Native-Apron-RUNTIME-CANDIDATE";
    public const string LabLinkMetadataFileName = "unusedlevel65blank-behavior-links.json";
    public const string RequiredSmokeAssemblyName =
        "Spyro.Editor.UnusedLevel65ReturnHomeNativeApronRuntimeCandidateSmoke";

    public const string BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string BaseCueSha256 =
        "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2";
    public const string SourceDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string OutputDataSha256 =
        "285782fd753b7325035b29d391d3cb17d3b554dbd3c1e7d6a0c1f98608ab5676";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string Id65OverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    public const string RetailTownSquareDataSha256 = SourceDataSha256;
    public const string RetailTownSquareOverlaySha256 = Id65OverlaySha256;

    public const string ExpectedOutputImageSha256 =
        "0e355c94cdb29fd062fbbd0da8f0f69da2250a4f008d50c28a7a9b37ccb9d0f4";
    public const long ExpectedChangedPhysicalImageBytes = 92;
    public const string ExpectedRawSectorDiffSha256 =
        "2c4d4c9bb47285a7978d3d542511195152b56c07612e71990f50971e55eb3c17";
    public const string ExpectedLabLinkMetadataSha256 =
        "ea5bfffdbba7131c198a064d36dc0a5c215771e69759b72aa9ff9c85722821e8";
    public const string ExpectedLocationGuideSha256 =
        "e652f9444a8d9219600e7b558ba43e45d42e2fb8bea9dcf657944c1f91789867";
    public const string ExpectedFinalReceiptSha256 =
        "0015382dc83a0cb6fb19a0bcd3daa877e94c59da7e9f3accc473dd2afc3c1e4d";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int LinkSchemaVersion = 1;
    private const int JournalSchemaVersion = 1;
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const long DataWadOffset = 0x6936800;
    private const int DataByteLength = 0x2E2000;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int Id65OverlayByteLength = 0xF800;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareOverlayByteLength = 0xF800;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int RetailTownSquareDataByteLength = 0x2E2000;
    private const int ObjectRecordCount = 107;
    private const int ObjectRecordByteLength = 0x58;
    private const int ObjectTableDataRelativeOffset = 0x1D0170;
    private const long Id65ObjectTableWadOffset = 0x6B06970;
    private const int T96 = 96;
    private const int T97 = 97;
    private const long T96XyzWadOffset = 0x6B08A7C;
    private const long T97XyzWadOffset = 0x6B08AD4;
    private const int ChangedRawSectorLba = 54_838;
    private const int LogicalXyzPatchBytes = 24;
    private const int ChangedLogicalWadBytes = 14;

    private const int TargetRawX = 124_585;
    private const int TargetRawY = 102_954;
    private const int GroundRawZ = 8_192;
    private const int HelperRawZ = 8_704;
    private const int NativeNearTriggerRaw = 0x400;
    private const int LandingRawX = 125_225;
    private const int LandingRawY = 100_506;
    private const int LandingRawZ = 8_550;
    private const int PlayerRawZ = 8_704;

    private const long CollisionComponentWadOffset = 0x6A40DC0;
    private const int CollisionComponentByteLength = 0x5FAE8;
    private const int CollisionTriangleCount = 0x4D60;
    private const int CollisionHeaderRelativeOffset = 0;
    private const int CollisionHeaderByteLength = 0x20;
    private const int CollisionTreeRelativeOffset = 0x20;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksRelativeOffset = 0x6A80;
    private const int CollisionBlocksByteLength = 0x17984;
    private const int CollisionTrianglesRelativeOffset = 0x1E404;
    private const int CollisionAssignmentsRelativeOffset = 0x58484;
    private const int CollisionTriangleIndex = 1_353;
    private const string CollisionComponentSha256 =
        "84901b6b9faa2f7fb0fce1d3aaa7e00fadf49e2d4bd7b2bcb2a0bb9a77397e2f";
    private const string CollisionHeaderSha256 =
        "73d2390f99633c263650569efc56788d5bf918ff05b02f39bf7b378e255c6e98";
    private const string CollisionTreeSha256 =
        "ad6ce785e5d49ff97c5fb79c967f2d0bcff2f2e2d40ef1137cdce0114e4ea6ed";
    private const string CollisionBlocksSha256 =
        "c2e0d178d39cd8ce8439d44d64987c81e60c93e0eb50f43c4a7ce51a258250d5";
    private const string CollisionTriangleHex = "521E20004A1960C000020000";
    private static readonly long[] CollisionLookupWadOffsets = [0x6A51672, 0x6A51908];

    private const string RuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-focused-pass-2026-08-09.json";
    private const string RuntimeEvidenceSha256 =
        "3ddfb471fd316c68459c6c09f5d3caa85ab0b134b9720e2f86025aa398b1ea87";

    private const string CanonicalLabGroupJson =
        "{\"key\":\"unusedlevel65blank:control-role-proof:t97\",\"name\":\"ID65 Return Home exit bundle\",\"kind\":\"linked group\",\"linkedMove\":true,\"confidence\":\"native-static-closure-runtime-pending\",\"basis\":\"T96 is the visible class-0x0009 Return Home controller; T97 is its colocated class-0x011E selector-8 sound helper.\",\"reason\":\"Move, copy, paste, or delete T96 and T97 atomically in the ID65 Blank-Level Lab.\",\"trueIndexes\":[97,96],\"memberIds\":[\"T97\",\"T96\"]}";
    private const string CanonicalLabGroupSha256 =
        "207f316ef796c1830a558a805cfca82170dc2b6c81610923e90ede3ed1989e53";

    private const string OperationsDirectoryName =
        ".unused-level-65-return-home-native-apron-operations";
    private const string WriterLeaseFileName =
        ".unused-level-65-return-home-native-apron-writer.lease";
    private const string OperationPrefix = "return-home-operation-";
    private const string JournalFileName = "operation-journal.json";
    private const string PhaseCreated = "created";
    private const string PhaseStaged = "staged";
    private const string PhaseBackupIntent = "backup-intent";
    private const string PhasePreviousBackedUp = "previous-backed-up";
    private const string PhasePublishIntent = "publish-intent";
    private const string PhasePublished = "published";
    private const string PhaseCommitted = "committed";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    internal const string ActiveWriterLeaseMessage =
        "Another Return Home candidate writer is active; no publication operation was started.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            throw new ArgumentException("The Return Home output directory is missing.", nameof(outputDirectoryPath));
        string output = Path.GetFullPath(outputDirectoryPath);
        if (!string.Equals(Path.GetFileName(output), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Return Home writer owns only a directory named '{OutputDirectoryName}'.");
        }
        string parent = Path.GetDirectoryName(output)
            ?? throw new InvalidOperationException("The Return Home publication parent is missing.");
        string prefix = Path.Combine(output, OutputPrefix);
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths = new(
            parent,
            output,
            Path.Combine(parent, OperationsDirectoryName),
            Path.Combine(parent, WriterLeaseFileName),
            prefix + ".bin",
            prefix + ".cue",
            Path.Combine(output, LabLinkMetadataFileName),
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command");
        RequirePathsInsideOutput(paths);
        return paths;
    }

    public static async Task<UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult> CreateAsync(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string root = RequireDirectory(request.WorkspaceRoot, "Spyro Editor workspace");
        string baseImage = RequireFile(request.BaseImagePath, "locked display-name BIN");
        string baseCue = RequireFile(request.BaseCuePath, "locked display-name CUE");
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths =
            CreatePaths(request.OutputDirectoryPath);
        RequireSafeRoles(root, baseImage, baseCue, paths);
        Directory.CreateDirectory(paths.PublicationParentPath);

        using WriterLease lease = AcquireWriterLease(paths.WriterLeasePath);
        request.TestStageHook?.Invoke("after-writer-lease-acquired");
        _ = RecoverOwnedOperations(paths);
        request.TestStageHook?.Invoke("after-startup-recovery");

        if (Directory.Exists(paths.OutputDirectoryPath) && !request.ReplaceExistingCandidate)
        {
            throw new InvalidOperationException(
                "The Return Home runtime candidate already exists. Set ReplaceExistingCandidate only for an intentional deterministic rebuild.");
        }

        await RequireFileHashAsync(baseImage, BaseImageSha256, cancellationToken);
        await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        UnusedLevel65ReturnHomeNativeApronPlan staticPlan =
            await BuildStaticPlanAsync(root, baseImage, baseCue, cancellationToken);

        string operationId = Guid.NewGuid().ToString("N");
        string operationRoot = Path.Combine(paths.OperationsDirectoryPath, OperationPrefix + operationId);
        string stage = Path.Combine(operationRoot, "stage");
        string backup = Path.Combine(operationRoot, "backup");
        string journalPath = Path.Combine(operationRoot, JournalFileName);
        Directory.CreateDirectory(operationRoot);
        OperationJournal journal = new(
            JournalSchemaVersion,
            operationId,
            paths.OutputDirectoryPath,
            operationRoot,
            stage,
            backup,
            journalPath,
            PhaseCreated,
            PreviousOutputExisted: Directory.Exists(paths.OutputDirectoryPath));
        WriteJournal(journal);

        bool committed = false;
        try
        {
            Directory.CreateDirectory(stage);
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                root,
                baseImage,
                baseCue,
                paths,
                stage,
                staticPlan,
                cancellationToken);
            journal = journal with { Phase = PhaseStaged };
            WriteJournal(journal);
            request.TestStageHook?.Invoke("after-stage-verified");

            if (Directory.Exists(paths.OutputDirectoryPath))
            {
                journal = journal with { Phase = PhaseBackupIntent };
                WriteJournal(journal);
                request.TestStageHook?.Invoke("after-backup-intent");
                Directory.Move(paths.OutputDirectoryPath, backup);
                journal = journal with { Phase = PhasePreviousBackedUp };
                WriteJournal(journal);
                request.TestStageHook?.Invoke("after-previous-backed-up");
            }

            journal = journal with { Phase = PhasePublishIntent };
            WriteJournal(journal);
            request.TestStageHook?.Invoke("after-publish-intent");
            Directory.Move(stage, paths.OutputDirectoryPath);
            journal = journal with { Phase = PhasePublished };
            WriteJournal(journal);
            request.TestStageHook?.Invoke("after-candidate-published");

            UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult published =
                await VerifyPublishedAsync(paths, cancellationToken);
            request.TestStageHook?.Invoke("before-final-staged-published-comparison");
            if (JsonSerializer.Serialize(published.Plan, JsonOptions) !=
                    JsonSerializer.Serialize(staged.Plan, JsonOptions) ||
                published.Receipt.OutputImageSha256 != staged.Receipt.OutputImageSha256)
            {
                throw new InvalidDataException("Published Return Home readback differs from the verified stage.");
            }
            request.TestStageHook?.Invoke("after-final-staged-published-comparison");

            await RequireFileHashAsync(baseImage, BaseImageSha256, cancellationToken);
            await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
            OperationJournal committedJournal = journal with { Phase = PhaseCommitted };
            WriteJournal(committedJournal);
            journal = committedJournal;
            committed = true;
            request.TestStageHook?.Invoke("after-commit-durable");

            if (Directory.Exists(backup))
                DeleteOwnedDirectory(backup, operationRoot, "backup");
            request.TestStageHook?.Invoke("after-committed-backup-cleaned");
            DeleteJournal(journalPath);
            DeleteOwnedDirectory(operationRoot, paths.OperationsDirectoryPath, OperationPrefix);
            DeleteOperationsDirectoryIfEmpty(paths.OperationsDirectoryPath);
            request.TestStageHook?.Invoke("after-post-commit-cleanup");
            return published;
        }
        catch
        {
            if (!committed)
                RollBackOperation(journal, paths);
            throw;
        }
    }

    public static async Task<UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult> VerifyPublishedAsync(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths exact =
            CreatePaths(paths.OutputDirectoryPath);
        if (paths != exact)
            throw new InvalidDataException("The Return Home verification paths are not canonical.");
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new DirectoryNotFoundException("The published Return Home candidate is missing.");
        RejectReparsePoint(paths.OutputDirectoryPath, "published Return Home directory");
        VerifyExactPublishedFiles(paths);

        string receiptText = await File.ReadAllTextAsync(paths.StaticReadbackReceiptPath, cancellationToken);
        UnusedLevel65ReturnHomeNativeApronReceipt receipt =
            JsonSerializer.Deserialize<UnusedLevel65ReturnHomeNativeApronReceipt>(receiptText, JsonOptions)
            ?? throw new InvalidDataException("The Return Home receipt is empty.");
        string canonicalReceipt = JsonSerializer.Serialize(receipt, JsonOptions) + "\n";
        if (!string.Equals(receiptText, canonicalReceipt, StringComparison.Ordinal))
            throw new InvalidDataException("The Return Home receipt is not canonical JSON.");
        ValidateReceiptIdentity(receipt, paths);

        await RequireFileHashAsync(receipt.BaseImagePath, BaseImageSha256, cancellationToken);
        await RequireFileHashAsync(receipt.BaseCuePath, BaseCueSha256, cancellationToken);
        await RequireFileHashAsync(paths.OutputImagePath, receipt.OutputImageSha256, cancellationToken);
        await RequireFileHashAsync(paths.OutputCuePath, receipt.OutputCueSha256, cancellationToken);
        await RequireFileHashAsync(paths.LabLinkMetadataPath, receipt.LabLinkMetadataSha256, cancellationToken);
        await RequireFileHashAsync(paths.RuntimeChecklistPath, receipt.RuntimeChecklistSha256, cancellationToken);
        await RequireFileHashAsync(paths.LocationGuidePath, receipt.LocationGuideSha256, cancellationToken);
        await RequireFileHashAsync(paths.FinderHelperPath, receipt.FinderHelperSha256, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");

        VerifyCanonicalLabMetadata(
            await File.ReadAllTextAsync(paths.LabLinkMetadataPath, cancellationToken));
        VerifyChecklist(
            await File.ReadAllTextAsync(paths.RuntimeChecklistPath, cancellationToken),
            paths,
            receipt.Plan.LoadCodes,
            BuildFinalFinderReveal(paths));
        VerifyLocationGuide(
            await File.ReadAllTextAsync(paths.LocationGuidePath, cancellationToken),
            receipt.Plan.LoadCodes);

        CandidateReadback readback = await VerifyImageReadbackAsync(
            receipt.BaseImagePath,
            paths.OutputImagePath,
            cancellationToken);
        ValidateReadbackAgainstReceipt(readback, receipt);

        return new(
            paths,
            receipt.Plan,
            receipt,
            new(
                HashFile(paths.StaticReadbackReceiptPath),
                receipt.OutputCueSha256,
                receipt.LabLinkMetadataSha256,
                receipt.RuntimeChecklistSha256,
                receipt.LocationGuideSha256,
                receipt.FinderHelperSha256));
    }

    private static async Task<UnusedLevel65ReturnHomeNativeApronPlan> BuildStaticPlanAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        CancellationToken cancellationToken)
    {
        UnusedLevel65ReturnHomeOwnershipContract ownership =
            UnusedLevel65ReturnHomeOwnershipInspector.Inspect(workspaceRoot, baseImage);
        if (ownership.ProfileId != UnusedLevel65ReturnHomeOwnershipInspector.ProfileId ||
            ownership.LockedBaseImageSha256 != BaseImageSha256 ||
            !ownership.Native.T96ComputesHomeworldFromCurrentLevel ||
            !ownership.Native.T96BypassesNormalPortalGuard ||
            ownership.PauseExit.ComputedDestinationLevelId != 60 ||
            ownership.PauseExit.ComputedRoute != -1 ||
            ownership.PauseExit.UsesNormalPortalGuard ||
            ownership.CommonTransition.RoutineBytesPatchedByProposal ||
            ownership.Destinations.GuardPatchBytesInProposal != 0 ||
            ownership.Destinations.RetailGnastyWorldMutationRequired ||
            ownership.Destinations.NormalArrivalRouteTo65Owned ||
            ownership.Link.CanonicalLabGroupSha256 != CanonicalLabGroupSha256 ||
            ownership.Link.RequiredTrueIndexes.Count != 2 ||
            !ownership.Link.RequiredTrueIndexes.SequenceEqual(new[] { 97, 96 }) ||
            !ownership.Link.RequiredMemberIds.SequenceEqual(new[] { "T97", "T96" }) ||
            ownership.Transaction.ExecutablePatchBytes != 0 ||
            ownership.Transaction.RetailWadPatchBytes != 0 ||
            !ownership.Transaction.StaticDependencyClosureComplete ||
            !ownership.Transaction.DestinationSafe ||
            !ownership.Transaction.RetailLevelsPreserved ||
            ownership.Transaction.PromotionAuthorized)
        {
            throw new InvalidDataException("The exact static Return Home / Pause Exit ownership closure changed.");
        }

        await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        RequireMode2(layout, "locked display-name base");
        using FileStream image = File.OpenRead(baseImage);
        _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord executableRecord = RequireRootFile(
            image, layout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        byte[] executable = DiscImage.ReadFileBytes(
            image, layout, executableRecord.Lba, 0, executableRecord.Size);
        RequireHash(executable, ExecutableSha256, "locked executable");

        byte[] sourceData = DiscImage.ReadFileBytes(
            image, layout, WadLba, DataWadOffset, DataByteLength);
        RequireHash(sourceData, SourceDataSha256, "locked ID65 data");
        byte[] outputData = sourceData.ToArray();
        PatchXyz(outputData, T96, TargetRawX, TargetRawY, GroundRawZ);
        PatchXyz(outputData, T97, TargetRawX, TargetRawY, HelperRawZ);
        RequireHash(outputData, OutputDataSha256, "planned Return Home ID65 data");
        int[] changedOffsets = DifferentByteIndexes(sourceData, outputData);
        int[] expectedChangedOffsets =
        [
            0x1D227C, 0x1D227D, 0x1D227E, 0x1D2280, 0x1D2281, 0x1D2282, 0x1D2285,
            0x1D22D4, 0x1D22D5, 0x1D22D6, 0x1D22D8, 0x1D22D9, 0x1D22DA, 0x1D22DD
        ];
        if (!changedOffsets.SequenceEqual(expectedChangedOffsets))
            throw new InvalidDataException("The direct T96/T97 proposal changed an unexpected logical byte set.");

        UnusedLevel65ReturnHomeNativeApronRowPatch visible = BuildRowPatch(
            sourceData, outputData, T96, "visible Return Home controller", T96XyzWadOffset);
        UnusedLevel65ReturnHomeNativeApronRowPatch helper = BuildRowPatch(
            sourceData, outputData, T97, "selector-8 sound helper", T97XyzWadOffset);
        if (visible.BeforeRowSha256 != ownership.Visible.TownSquareRow.Sha256 ||
            helper.BeforeRowSha256 != ownership.Helper.TownSquareRow.Sha256)
        {
            throw new InvalidDataException("The locked T96/T97 preimages no longer match the ownership closure.");
        }

        byte[] collision = DiscImage.ReadFileBytes(
            image, layout, WadLba, CollisionComponentWadOffset, CollisionComponentByteLength);
        UnusedLevel65ReturnHomeNativeApronSupportProof support =
            await BuildSupportProofAsync(workspaceRoot, collision, cancellationToken);

        byte[] landingBytes = DiscImage.ReadFileBytes(image, layout, WadLba, 0x6B06800, 0x10);
        RequireHex(landingBytes, "29E901009A8801006621000000004000", "locked ID65 landing");
        byte[] playerRow = sourceData.AsSpan(
            ObjectTableDataRelativeOffset + 92 * ObjectRecordByteLength,
            ObjectRecordByteLength).ToArray();
        RequireHash(
            playerRow,
            "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2",
            "locked T92 player anchor");
        UnusedLevel65ReturnHomeNativeApronPoint landing = new(LandingRawX, LandingRawY, LandingRawZ);
        UnusedLevel65ReturnHomeNativeApronPoint player = new(LandingRawX, LandingRawY, PlayerRawZ);
        if (ReadPoint(playerRow, 0x0C) != player)
            throw new InvalidDataException("The locked T92 player anchor coordinates changed.");

        RequireHash(
            DiscImage.ReadFileBytes(image, layout, WadLba, Id65OverlayWadOffset, Id65OverlayByteLength),
            Id65OverlaySha256,
            "ID65 overlay");
        RequireHash(
            DiscImage.ReadFileBytes(
                image, layout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            RetailTownSquareOverlaySha256,
            "retail Town Square overlay");
        RequireHash(
            DiscImage.ReadFileBytes(
                image, layout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength),
            RetailTownSquareDataSha256,
            "retail Town Square data");

        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = BuildLoadCodes();
        double horizontalRaw = Math.Sqrt(
            Math.Pow(TargetRawX - LandingRawX, 2) + Math.Pow(TargetRawY - LandingRawY, 2));
        double threeDimensionalRaw = Math.Sqrt(
            Math.Pow(TargetRawX - LandingRawX, 2) +
            Math.Pow(TargetRawY - LandingRawY, 2) +
            Math.Pow(GroundRawZ - PlayerRawZ, 2));
        double horizontalWorld = horizontalRaw / 16.0;
        double triggerMarginWorld = (horizontalRaw - NativeNearTriggerRaw) / 16.0;
        if (triggerMarginWorld <= 0)
            throw new InvalidDataException("The native Return Home placement would trigger immediately at spawn.");

        return new(
            PlanSchemaVersion,
            ProfileId,
            ownership.ProfileId,
            BaseImageSha256,
            BaseCueSha256,
            SourceDataSha256,
            OutputDataSha256,
            ExecutableSha256,
            Id65OverlaySha256,
            RetailTownSquareOverlaySha256,
            RetailTownSquareDataSha256,
            [visible, helper],
            support,
            landing,
            player,
            horizontalWorld,
            threeDimensionalRaw / 16.0,
            NativeNearTriggerRaw,
            triggerMarginWorld,
            ownership.PauseExit.ComputedDestinationLevelId,
            ownership.PauseExit.ComputedDestinationLevelId,
            ownership.PauseExit.ComputedRoute,
            CanonicalLabGroupSha256,
            changedOffsets,
            LogicalXyzPatchBytes,
            ChangedLogicalWadBytes,
            ExecutablePatchBytes: 0,
            RetailWadPatchBytes: 0,
            AffectedRawSectorLbas: [ChangedRawSectorLba],
            RawSectorDiffSha256: ExpectedRawSectorDiffSha256,
            loadCodes,
            ExactT96T97AtomicRelocationVerified: true,
            ExactLabLinkMetadataVerified: true,
            NativeSupportVerified: true,
            LandingAndPlayerAnchorPreserved: true,
            TerrainPreserved: true,
            ExecutablePreserved: true,
            RetailLevelsPreserved: true,
            MemoryCardsRequired: false,
            SavingAuthorized: false,
            RuntimeVerified: false,
            DisposableRuntimeCandidateAuthorized: true,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false,
            ReleaseAuthorized: false);
    }

    private static async Task<UnusedLevel65ReturnHomeNativeApronSupportProof> BuildSupportProofAsync(
        string workspaceRoot,
        byte[] collision,
        CancellationToken cancellationToken)
    {
        RequireHash(collision, CollisionComponentSha256, "locked native collision component");
        RequireHash(
            collision.AsSpan(CollisionHeaderRelativeOffset, CollisionHeaderByteLength),
            CollisionHeaderSha256,
            "locked collision header");
        RequireHash(
            collision.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength),
            CollisionTreeSha256,
            "locked collision tree");
        RequireHash(
            collision.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength),
            CollisionBlocksSha256,
            "locked collision blocks");
        int[] expectedHeaderWords =
        [
            CollisionComponentByteLength,
            CollisionTriangleCount,
            0x2904,
            0x1C,
            0x6A7C,
            0x1E400,
            0x58480,
            0x5D1E0
        ];
        for (int index = 0; index < expectedHeaderWords.Length; index++)
        {
            int actual = BinaryPrimitives.ReadInt32LittleEndian(collision.AsSpan(index * 4, 4));
            if (actual != expectedHeaderWords[index])
            {
                throw new InvalidDataException(
                    $"The native collision header word {index} is 0x{actual:X}, expected 0x{expectedHeaderWords[index]:X}.");
            }
        }

        byte[] triangleBytes = collision.AsSpan(
            CollisionTrianglesRelativeOffset + CollisionTriangleIndex * 12,
            12).ToArray();
        RequireHex(triangleBytes, CollisionTriangleHex, "native support triangle T1353");
        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> points = DecodeCollisionTriangle(triangleBytes);
        UnusedLevel65ReturnHomeNativeApronPoint[] expectedPoints =
        [
            new(7_762, 6_474, 512),
            new(7_890, 6_346, 512),
            new(7_762, 6_346, 512)
        ];
        if (!points.SequenceEqual(expectedPoints))
            throw new InvalidDataException("Native support triangle T1353 geometry changed.");

        byte[] blocks = collision.AsSpan(
            CollisionBlocksRelativeOffset,
            CollisionBlocksByteLength).ToArray();
        long[] lookupOffsets = Enumerable.Range(0, blocks.Length / 2)
            .Where(index =>
                (BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(index * 2, 2)) & 0x7FFF) ==
                CollisionTriangleIndex)
            .Select(index => CollisionComponentWadOffset + CollisionBlocksRelativeOffset + index * 2L)
            .ToArray();
        if (!lookupOffsets.SequenceEqual(CollisionLookupWadOffsets))
        {
            throw new InvalidDataException(
                $"Native T1353 lookup refs changed to [{string.Join(',', lookupOffsets.Select(value => $"0x{value:X}"))}].");
        }
        int assignment = collision[CollisionAssignmentsRelativeOffset + CollisionTriangleIndex];
        if (assignment != 0)
            throw new InvalidDataException("Native support triangle T1353 is no longer ordinary assignment zero.");

        bool strictlyInside = PointInsideTriangle(points, TargetRawX, TargetRawY, strict: true);
        int rawZ = 0;
        long normalZ = 0;
        if (!strictlyInside ||
            !TryInterpolateRawZ(points, TargetRawX, TargetRawY, out rawZ, out normalZ) ||
            rawZ != GroundRawZ || normalZ != -16_384)
        {
            throw new InvalidDataException(
                $"The Return Home placement lost native T1353 support: inside={strictlyInside}, Z={rawZ}, N={normalZ}.");
        }
        int leftMarginRaw = TargetRawX - (7_762 * 16);
        int diagonalMarginRaw = (128 * 16) -
            ((TargetRawX - (7_762 * 16)) + (TargetRawY - (6_346 * 16)));
        if (leftMarginRaw != 393 || diagonalMarginRaw != 237)
            throw new InvalidDataException("The exact native T1353 interior margins changed.");

        List<SurfaceHit> hits = [];
        for (int triangleIndex = 0; triangleIndex < CollisionTriangleCount; triangleIndex++)
        {
            ReadOnlySpan<byte> bytes = collision.AsSpan(
                CollisionTrianglesRelativeOffset + triangleIndex * 12,
                12);
            IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> candidate = DecodeCollisionTriangle(bytes);
            if (!PointInsideTriangle(candidate, TargetRawX, TargetRawY, strict: false) ||
                !TryInterpolateRawZ(candidate, TargetRawX, TargetRawY, out int candidateZ, out long candidateNormal))
            {
                continue;
            }
            hits.Add(new(triangleIndex, candidateZ, candidateNormal));
        }
        SurfaceHit[] topOrHigher = hits
            .Where(hit => hit.RawZ >= GroundRawZ)
            .OrderByDescending(hit => hit.RawZ)
            .ThenBy(hit => hit.TriangleIndex)
            .ToArray();
        bool exposed = topOrHigher.Length > 0 &&
            topOrHigher[0].RawZ == GroundRawZ &&
            topOrHigher.All(hit => hit.RawZ == GroundRawZ) &&
            topOrHigher.Any(hit =>
                hit.TriangleIndex == CollisionTriangleIndex && hit.NormalZ == -16_384);
        if (!exposed)
        {
            throw new InvalidDataException(
                "The proposed Return Home location is not exposed topmost native T1353 support: " +
                string.Join(',', topOrHigher.Select(hit =>
                    $"T{hit.TriangleIndex}:Z{hit.RawZ}:N{hit.NormalZ}")));
        }

        string evidencePath = RequireFile(
            Path.Combine(workspaceRoot, RuntimeEvidenceRelativePath),
            "focused native entrance-fan runtime evidence");
        await RequireFileHashAsync(evidencePath, RuntimeEvidenceSha256, cancellationToken);
        using (JsonDocument evidence = JsonDocument.Parse(
                   await File.ReadAllTextAsync(evidencePath, cancellationToken)))
        {
            JsonElement root = evidence.RootElement;
            int[] central = root.GetProperty("exactCandidateStaticBinding")
                .GetProperty("centralCollisionFootprintTriangleIndexes")
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();
            if (root.GetProperty("evidenceStatus").GetString() != "focused-runtime-pass" ||
                root.GetProperty("evidenceScope").GetString() != "complete-edited-surface-solidity-only" ||
                !central.SequenceEqual(new[] { 1_353, 1_354 }) ||
                root.GetProperty("promotionAuthorized").GetBoolean() ||
                root.GetProperty("normalCreateBinIntegrationAuthorized").GetBoolean())
            {
                throw new InvalidDataException("The focused native entrance-fan runtime evidence boundary changed.");
            }
        }

        return new(
            CollisionComponentWadOffset,
            CollisionComponentByteLength,
            CollisionComponentSha256,
            CollisionHeaderSha256,
            CollisionTreeSha256,
            CollisionBlocksSha256,
            CollisionTriangleIndex,
            CollisionTriangleHex,
            points,
            lookupOffsets,
            assignment,
            normalZ,
            rawZ,
            leftMarginRaw,
            diagonalMarginRaw,
            RuntimeEvidenceRelativePath,
            RuntimeEvidenceSha256,
            PointStrictlyInsideNativeTriangle: true,
            NativeTriangleIsExposedTopmostAtPoint: true,
            PositiveWindingTerrainExcluded: true);
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths finalPaths,
        string stageDirectory,
        UnusedLevel65ReturnHomeNativeApronPlan staticPlan,
        CancellationToken cancellationToken)
    {
        _ = workspaceRoot;
        string stagedImage = StagedPath(stageDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stageDirectory, finalPaths.OutputCuePath);
        string stagedMetadata = StagedPath(stageDirectory, finalPaths.LabLinkMetadataPath);
        string stagedReceipt = StagedPath(stageDirectory, finalPaths.StaticReadbackReceiptPath);
        string stagedChecklist = StagedPath(stageDirectory, finalPaths.RuntimeChecklistPath);
        string stagedGuide = StagedPath(stageDirectory, finalPaths.LocationGuidePath);

        await DiscImageWorkingCopy.StageAsync(
            baseImage,
            stagedImage,
            consumeDisposableSource: false,
            cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(stagedImage);
        RequireMode2(layout, "staged Return Home image");
        int rebuiltRawSectorCount;
        await using (FileStream image = new(
                         stagedImage,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 128 * 1024,
                         FileOptions.Asynchronous))
        {
            _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);
            byte[] sourceData = DiscImage.ReadFileBytes(
                image, layout, WadLba, DataWadOffset, DataByteLength);
            RequireHash(sourceData, SourceDataSha256, "staged ID65 preimage");
            byte[] beforeT96 = sourceData.AsSpan(
                ObjectTableDataRelativeOffset + T96 * ObjectRecordByteLength + 0x0C,
                12).ToArray();
            byte[] beforeT97 = sourceData.AsSpan(
                ObjectTableDataRelativeOffset + T97 * ObjectRecordByteLength + 0x0C,
                12).ToArray();
            RequirePoint(beforeT96, new(141_220, 136_827, 11_776), "staged T96 XYZ");
            RequirePoint(beforeT97, new(141_220, 136_827, 12_288), "staged T97 XYZ");

            byte[] afterT96 = EncodePoint(new(TargetRawX, TargetRawY, GroundRawZ));
            byte[] afterT97 = EncodePoint(new(TargetRawX, TargetRawY, HelperRawZ));
            DiscImage.WriteFileBytes(image, layout, WadLba, T96XyzWadOffset, afterT96);
            DiscImage.WriteFileBytes(image, layout, WadLba, T97XyzWadOffset, afterT97);
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                WadLba,
                [(T96XyzWadOffset, 12), (T97XyzWadOffset, 12)]);
            int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                [(ChangedRawSectorLba, 1)]);
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                image,
                layout,
                ChangedRawSectorLba,
                XaDataSubmode);
            if (rebuiltRawSectorCount != 1 || verifiedRawSectors != 1)
                throw new InvalidDataException("The Return Home patch did not rebuild exactly one raw sector.");
            await image.FlushAsync(cancellationToken);
            image.Flush(flushToDisk: true);
        }

        string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(stagedImage));
        await WriteTextAsync(stagedCue, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");

        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            stagedImage,
            cancellationToken);
        RequireOptionalHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "Return Home output BIN");
        RequireOptionalCount(
            readback.ChangedPhysicalImageBytes,
            ExpectedChangedPhysicalImageBytes,
            "Return Home physical changed-byte count");
        RequireOptionalHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "Return Home raw diff");

        UnusedLevel65ReturnHomeNativeApronPlan plan = staticPlan with
        {
            RawSectorDiffSha256 = readback.RawSectorDiffSha256
        };
        string metadata = BuildCanonicalLabMetadata();
        VerifyCanonicalLabMetadata(metadata);
        await WriteTextAsync(
            stagedMetadata,
            metadata,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        RuntimeCandidateFinderReveal stagedReveal =
            await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(stagedCue, cancellationToken);
        RuntimeCandidateFinderReveal finalReveal = BuildFinalFinderReveal(finalPaths);
        string checklist = BuildRuntimeChecklist(finalPaths, finalReveal, plan, readback);
        await WriteTextAsync(
            stagedChecklist,
            checklist,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        string guide = BuildLocationGuideSvg(plan.LoadCodes, readback.OutputImageSha256);
        await WriteTextAsync(
            stagedGuide,
            guide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        string outputCueSha256 = HashFile(stagedCue);
        string metadataSha256 = HashFile(stagedMetadata);
        string checklistSha256 = HashFile(stagedChecklist);
        string guideSha256 = HashFile(stagedGuide);
        string helperSha256 = HashFile(stagedReveal.HelperPath);
        if (metadataSha256 != ExpectedLabLinkMetadataSha256 ||
            (ExpectedLocationGuideSha256 != "PENDING" &&
             guideSha256 != ExpectedLocationGuideSha256))
        {
            throw new InvalidDataException("The pinned Return Home metadata or 1200x1000 guide changed.");
        }
        UnusedLevel65ReturnHomeNativeApronReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            baseImage,
            baseCue,
            finalPaths.OutputDirectoryPath,
            finalPaths.OutputImagePath,
            finalPaths.OutputCuePath,
            finalPaths.LabLinkMetadataPath,
            finalPaths.StaticReadbackReceiptPath,
            finalPaths.RuntimeChecklistPath,
            finalPaths.LocationGuidePath,
            finalPaths.FinderHelperPath,
            readback.OutputImageSha256,
            outputCueSha256,
            metadataSha256,
            checklistSha256,
            guideSha256,
            helperSha256,
            BaseImageSha256,
            BaseCueSha256,
            readback.OutputDataSha256,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.RawSectorDiffs.Count,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            plan,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            CanonicalLabMetadataVerified: true,
            CanonicalReceiptVerified: true,
            Mode2IntegrityVerified: true,
            BaseCandidatePreserved: true,
            FullDirectoryPublicationVerified: true,
            RollbackRecoveryVerified: false,
            FullDirectoryRollbackVerified: false,
            FinderHandoffVerified: true,
            RuntimeVerified: false,
            DisposableRuntimeCandidateAuthorized: true,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false,
            ReleaseAuthorized: false);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);

        VerifyExactStagedFiles(stageDirectory, finalPaths);
        VerifyTextReadback(
            stagedReceipt,
            JsonSerializer.Serialize(receipt, JsonOptions) + "\n",
            "Return Home receipt");
        VerifyChecklist(checklist, finalPaths, plan.LoadCodes, finalReveal);
        VerifyLocationGuide(guide, plan.LoadCodes);
        return new(plan, receipt, readback);
    }

    private static async Task<CandidateReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        RequireMode2(baseLayout, "Return Home base readback");
        RequireMode2(outputLayout, "Return Home output readback");
        if (baseLayout != outputLayout)
            throw new InvalidDataException("The Return Home output changed the exact disc layout.");

        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The Return Home output changed the disc-image length.");
        _ = RequireRootFile(baseline, baseLayout, "WAD.WAD", WadLba, WadByteLength);
        _ = RequireRootFile(output, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord baseExecutable = RequireRootFile(
            baseline, baseLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        DiscFileRecord outputExecutable = RequireRootFile(
            output, outputLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        byte[] beforeExecutable = DiscImage.ReadFileBytes(
            baseline, baseLayout, baseExecutable.Lba, 0, baseExecutable.Size);
        byte[] afterExecutable = DiscImage.ReadFileBytes(
            output, outputLayout, outputExecutable.Lba, 0, outputExecutable.Size);
        RequireHash(beforeExecutable, ExecutableSha256, "base executable readback");
        if (!beforeExecutable.SequenceEqual(afterExecutable))
            throw new InvalidDataException("The Return Home candidate changed SCUS_942.28.");

        byte[] beforeData = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, DataWadOffset, DataByteLength);
        byte[] afterData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, DataWadOffset, DataByteLength);
        RequireHash(beforeData, SourceDataSha256, "base ID65 data readback");
        RequireHash(afterData, OutputDataSha256, "output ID65 data readback");
        int[] changedLogical = DifferentByteIndexes(beforeData, afterData);
        if (changedLogical.Length != ChangedLogicalWadBytes ||
            changedLogical.Any(index =>
                !((index >= 0x1D227C && index < 0x1D2288) ||
                  (index >= 0x1D22D4 && index < 0x1D22E0))))
        {
            throw new InvalidDataException("The output logical diff escaped T96/T97 XYZ.");
        }
        RequirePoint(
            afterData.AsSpan(ObjectTableDataRelativeOffset + T96 * ObjectRecordByteLength + 0x0C, 12),
            new(TargetRawX, TargetRawY, GroundRawZ),
            "output T96 XYZ");
        RequirePoint(
            afterData.AsSpan(ObjectTableDataRelativeOffset + T97 * ObjectRecordByteLength + 0x0C, 12),
            new(TargetRawX, TargetRawY, HelperRawZ),
            "output T97 XYZ");

        RequirePreservedWadRange(
            baseline, output, baseLayout, Id65OverlayWadOffset, Id65OverlayByteLength,
            Id65OverlaySha256, "ID65 overlay");
        RequirePreservedWadRange(
            baseline, output, baseLayout, CollisionComponentWadOffset, CollisionComponentByteLength,
            CollisionComponentSha256, "native collision / terrain");
        RequirePreservedWadRange(
            baseline, output, baseLayout, RetailTownSquareOverlayWadOffset,
            RetailTownSquareOverlayByteLength, RetailTownSquareOverlaySha256,
            "retail Town Square overlay");
        RequirePreservedWadRange(
            baseline, output, baseLayout, RetailTownSquareDataWadOffset,
            RetailTownSquareDataByteLength, RetailTownSquareDataSha256,
            "retail Town Square data");
        byte[] baseLanding = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, 0x6B06800, 0x10);
        byte[] outputLanding = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, 0x6B06800, 0x10);
        if (!baseLanding.SequenceEqual(outputLanding))
            throw new InvalidDataException("The Return Home candidate changed ID65 landing data.");
        byte[] basePlayer = beforeData.AsSpan(
            ObjectTableDataRelativeOffset + 92 * ObjectRecordByteLength,
            ObjectRecordByteLength).ToArray();
        byte[] outputPlayer = afterData.AsSpan(
            ObjectTableDataRelativeOffset + 92 * ObjectRecordByteLength,
            ObjectRecordByteLength).ToArray();
        if (!basePlayer.SequenceEqual(outputPlayer))
            throw new InvalidDataException("The Return Home candidate changed T92/player anchor.");

        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRawSectorDiff> rawDiffs =
            await CompareRawImagesAsync(baseline, output, cancellationToken);
        if (rawDiffs.Count != 1 || rawDiffs[0].RawSectorLba != ChangedRawSectorLba ||
            rawDiffs[0].HeaderChangedBytes != 0 || rawDiffs[0].SubheaderChangedBytes != 0 ||
            rawDiffs[0].PayloadChangedBytes != ChangedLogicalWadBytes ||
            rawDiffs[0].ReservedChangedBytes != 0)
        {
            throw new InvalidDataException("The Return Home physical diff escaped raw sector LBA 54838.");
        }
        int physicalChanged = rawDiffs.Sum(diff => diff.TotalChangedBytes);
        string rawDiffHash = HashRawSectorDiffs(rawDiffs);
        string outputImageSha256 = await HashFileAsync(outputImagePath, cancellationToken);
        RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            [(ChangedRawSectorLba, 1)]);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            outputLayout,
            ChangedRawSectorLba,
            XaDataSubmode);
        await RequireFileHashAsync(baseImagePath, BaseImageSha256, cancellationToken);

        return new(
            outputImageSha256,
            Hash(afterData),
            changedLogical.Length,
            physicalChanged,
            rawDiffs,
            rawDiffHash,
            ExecutablePreserved: true,
            LandingAndPlayerAnchorPreserved: true,
            TerrainPreserved: true,
            RetailTownSquarePreserved: true,
            Mode2IntegrityVerified: true);
    }

    private static string BuildCanonicalLabMetadata()
    {
        UnusedLevel65ReturnHomeNativeApronLabLinkGroup group = BuildLabGroup();
        UnusedLevel65ReturnHomeNativeApronLabLinkDocument document = new(
            LinkSchemaVersion,
            ProfileId,
            "Candidate-local ID65 Blank-Level Lab metadata. T96 and T97 must move, copy, paste, and delete atomically; runtime behavior is still pending this CUE gate.",
            new(TotalGroups: 1, AtomicLinkedMoveGroups: 1, RuntimePendingGroups: 1),
            [group],
            RuntimeVerified: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
        return JsonSerializer.Serialize(document, JsonOptions) + "\n";
    }

    private static UnusedLevel65ReturnHomeNativeApronLabLinkGroup BuildLabGroup() => new(
        "unusedlevel65blank:control-role-proof:t97",
        "ID65 Return Home exit bundle",
        "linked group",
        LinkedMove: true,
        "native-static-closure-runtime-pending",
        "T96 is the visible class-0x0009 Return Home controller; T97 is its colocated class-0x011E selector-8 sound helper.",
        "Move, copy, paste, or delete T96 and T97 atomically in the ID65 Blank-Level Lab.",
        [97, 96],
        ["T97", "T96"]);

    private static void VerifyCanonicalLabMetadata(string text)
    {
        UnusedLevel65ReturnHomeNativeApronLabLinkDocument document =
            JsonSerializer.Deserialize<UnusedLevel65ReturnHomeNativeApronLabLinkDocument>(text, JsonOptions)
            ?? throw new InvalidDataException("The candidate-local lab link metadata is empty.");
        if (!string.Equals(
                text,
                JsonSerializer.Serialize(document, JsonOptions) + "\n",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The lab link metadata is not canonical JSON.");
        }
        if (document.SchemaVersion != LinkSchemaVersion || document.ProfileId != ProfileId ||
            document.Summary != new UnusedLevel65ReturnHomeNativeApronLabLinkSummary(1, 1, 1) ||
            document.LinkGroups.Count != 1 || !LabGroupsEqual(document.LinkGroups[0], BuildLabGroup()) ||
            document.RuntimeVerified || document.AppIntegrated ||
            document.NormalCreateBinEnabled || document.PromotionAuthorized)
        {
            throw new InvalidDataException("The exact candidate-local T96/T97 linked group changed.");
        }
        JsonSerializerOptions compact = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        string groupJson = JsonSerializer.Serialize(document.LinkGroups[0], compact);
        if (groupJson != CanonicalLabGroupJson || Hash(Encoding.UTF8.GetBytes(groupJson)) != CanonicalLabGroupSha256)
            throw new InvalidDataException("The exact ownership-contract lab group hash changed.");
    }

    private static string BuildRuntimeChecklist(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal finderReveal,
        UnusedLevel65ReturnHomeNativeApronPlan plan,
        CandidateReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Return Home + Pause Exit Native-Apron Gate — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This candidate starts directly from the runtime-passed locked display-name BIN and atomically relocates only ID65 T96/T97 to exposed native collision T1353. It does not use the retired remote-v1 pad or any authored foundation.");
        builder.AppendLine(
            "Return Home and Pause → Exit Level are two separate runtime outcomes. Run each from a separate cold boot; a pass in one path does not prove the other.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        builder.AppendLine("## Immutable setup");
        builder.AppendLine();
        builder.AppendLine("- DuckStation Memory Card 1: **None**.");
        builder.AppendLine("- DuckStation Memory Card 2: **None**.");
        builder.AppendLine("- Disable cheats and do not resume a save state.");
        builder.AppendLine("- **Do not save.** Do not insert, create, format, or write a memory card.");
        builder.AppendLine("- Load the **CUE**, not the BIN.");
        builder.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`.");
        builder.AppendLine($"- Candidate-local link metadata: `{LabLinkMetadataFileName}` (T97 + T96 atomic linked move).");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, plan.LoadCodes);
        builder.AppendLine("## Gate A — visible Return Home controller (cold run)");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold boot, enter the complete ID65 candidate code, and wait for controllable gameplay with 4 lives.");
        builder.AppendLine("- [ ] Confirm normal native Town Square terrain at the entrance. No remote pad, blank construction, foundation triangle, or new terrain is part of this candidate.");
        builder.AppendLine("- [ ] From spawn, move about 153 scene units forward and 40 left to the marked native-apron point. T96 must be visibly present on the ground; T97 remains exactly 32 world units above it as its linked selector-8 sound helper.");
        builder.AppendLine("- [ ] Approach the visible Return Home controller normally. It must not trigger at spawn; the static margin beyond the native near radius is greater than 94 world units.");
        builder.AppendLine("- [ ] Activate/touch Return Home once. The result must be a clean transition to **Gnasty's World, level 60** with normal control. It must not target ID65, crash, hang, enter Game Over, or strand on a missing level-65 arrival portal.");
        builder.AppendLine("- [ ] Do not use Pause → Exit Level in this run. Record Return Home result separately as PASS or FAIL.");
        builder.AppendLine();
        builder.AppendLine("## Gate B — Pause → Exit Level (separate cold run)");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold boot again, enter the complete ID65 candidate code, and wait for controllable gameplay with 4 lives.");
        builder.AppendLine("- [ ] Before touching Return Home, open Pause and choose **Exit Level**, then confirm.");
        builder.AppendLine("- [ ] Pause Exit must independently transition to **Gnasty's World, level 60**, using route -1, with normal control.");
        builder.AppendLine("- [ ] Do not touch Return Home in this run. Record Pause Exit result separately as PASS or FAIL.");
        builder.AppendLine();
        builder.AppendLine("## Re-entry and isolation controls");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold boot/reset and re-enter ID65. Confirm the candidate still lands at the unchanged native spawn, T96/T97 remain at the marked apron point, and camera/movement/pause remain stable.");
        builder.AppendLine("- [ ] Cold-load the Gnasty's World destination control code and confirm the same retail hub loads normally without requiring the candidate transition.");
        builder.AppendLine("- [ ] Cold-load Retail Town Square and confirm its original Return Home pair/location, landing, terrain, and ordinary play are unchanged. Do not activate its Return Home controller in this control.");
        builder.AppendLine("- [ ] Cold-load Gnasty's Loot and confirm normal gameplay and exit behavior.");
        builder.AppendLine("- [ ] Cold-load Sunny Flight and confirm normal flight controls, timer, and exit behavior.");
        builder.AppendLine();
        builder.AppendLine("## Decision boundary");
        builder.AppendLine();
        builder.AppendLine("PASS requires both Gate A and Gate B in their separate cold runs, successful ID65 re-entry, and every retail/destination control. A visible controller alone is not a transition pass; a Pause Exit pass is not a Return Home pass.");
        builder.AppendLine("Any crash, wrong destination, immediate spawn trigger, missing/invisible controller, T96/T97 separation, bad sound, collision problem, card/save prompt, retail change, or re-entry failure leaves Return Home ownership runtime-pending and unpromoted.");
        builder.AppendLine("Normal Create BIN, app integration, release packaging, and promotion remain disabled.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        string outputImageSha256)
    {
        string[] codeRows = loadCodes.Select(code =>
            XmlEscape($"{code.TestName} ({code.LevelId}): {code.InputCode}")).ToArray();
        return
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\" data-safe-left=\"60\" data-safe-right=\"1140\">\n" +
            "  <rect width=\"1200\" height=\"1000\" fill=\"#07111f\"/>\n" +
            "  <rect x=\"40\" y=\"36\" width=\"1120\" height=\"928\" rx=\"24\" fill=\"#10233c\" stroke=\"#4f82bd\" stroke-width=\"2\"/>\n" +
            "  <text x=\"60\" y=\"82\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"28\" font-weight=\"700\">ID65 Return Home + Pause Exit — Native Apron Gate</text>\n" +
            "  <text x=\"60\" y=\"111\" fill=\"#b9cbed\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Locked display-name base • only T96/T97 XYZ • runtime pending</text>\n" +
            "  <rect x=\"60\" y=\"142\" width=\"620\" height=\"390\" rx=\"18\" fill=\"#0a192b\" stroke=\"#355b86\"/>\n" +
            "  <text x=\"84\" y=\"176\" fill=\"#dfeaff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Top-down entrance fan (native T1353)</text>\n" +
            "  <polygon points=\"170,450 590,190 170,190\" fill=\"#315d42\" stroke=\"#8bdf9d\" stroke-width=\"3\"/>\n" +
            "  <text x=\"190\" y=\"220\" fill=\"#c7f4d1\" font-family=\"Menlo,monospace\" font-size=\"13\">T1353 • N_z -16384 • assignment 0</text>\n" +
            "  <circle cx=\"250\" cy=\"375\" r=\"15\" fill=\"#63b7ff\" stroke=\"#d9efff\" stroke-width=\"3\"/>\n" +
            "  <text x=\"275\" y=\"380\" fill=\"#d9efff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"15\">unchanged spawn/T92</text>\n" +
            "  <circle cx=\"390\" cy=\"255\" r=\"18\" fill=\"#ffad47\" stroke=\"#fff0ce\" stroke-width=\"4\"/>\n" +
            "  <text x=\"418\" y=\"250\" fill=\"#fff0ce\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">T96 Return Home</text>\n" +
            "  <text x=\"418\" y=\"274\" fill=\"#ffd598\" font-family=\"Menlo,monospace\" font-size=\"12\">raw 124585,102954,8192</text>\n" +
            "  <path d=\"M265 365 Q320 320 380 266\" fill=\"none\" stroke=\"#ffcf70\" stroke-width=\"4\" stroke-dasharray=\"10 8\" marker-end=\"url(#arrow)\"/>\n" +
            "  <defs><marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#ffcf70\"/></marker></defs>\n" +
            "  <text x=\"94\" y=\"500\" fill=\"#bcd0ee\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">~153 forward + 40 left • 158.142 world horizontal • no spawn auto-trigger</text>\n" +
            "  <rect x=\"705\" y=\"142\" width=\"435\" height=\"390\" rx=\"18\" fill=\"#15192b\" stroke=\"#6e6ba5\"/>\n" +
            "  <text x=\"730\" y=\"178\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"19\" font-weight=\"700\">Two separate cold-run gates</text>\n" +
            "  <text x=\"735\" y=\"220\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">A • Touch Return Home</text>\n" +
            "  <text x=\"755\" y=\"248\" fill=\"#d9e3f7\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"14\">Must land in Gnasty's World (60)</text>\n" +
            "  <text x=\"735\" y=\"302\" fill=\"#c3baff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">B • Pause → Exit Level</text>\n" +
            "  <text x=\"755\" y=\"330\" fill=\"#d9e3f7\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"14\">Must independently land in level 60</text>\n" +
            "  <text x=\"730\" y=\"390\" fill=\"#ff9d9d\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Cards 1/2: None • Do not save</text>\n" +
            "  <text x=\"730\" y=\"420\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">No remote-v1 pad • no foundation</text>\n" +
            "  <text x=\"730\" y=\"448\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">No terrain edits • SCUS preserved</text>\n" +
            "  <text x=\"730\" y=\"476\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">Landing / T92 / retail preserved</text>\n" +
            "  <text x=\"60\" y=\"580\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Complete cold-load codes</text>\n" +
            string.Join("\n", codeRows.Select((row, index) =>
                $"  <text x=\"80\" y=\"{610 + index * 27}\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"11\">{row}</text>")) + "\n" +
            "  <rect x=\"60\" y=\"760\" width=\"1080\" height=\"155\" rx=\"14\" fill=\"#09182a\" stroke=\"#355b86\"/>\n" +
            "  <text x=\"82\" y=\"794\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">Pinned candidate identity</text>\n" +
            $"  <text x=\"82\" y=\"824\" fill=\"#b9cbed\" font-family=\"Menlo,monospace\" font-size=\"11\">Profile: {XmlEscape(ProfileId)}</text>\n" +
            $"  <text x=\"82\" y=\"850\" fill=\"#b9cbed\" font-family=\"Menlo,monospace\" font-size=\"11\">CUE: {XmlEscape(OutputPrefix + ".cue")}</text>\n" +
            $"  <text x=\"82\" y=\"876\" fill=\"#ffb7b7\" font-family=\"Menlo,monospace\" font-size=\"11\">BIN SHA-256: {XmlEscape(outputImageSha256)}</text>\n" +
            "  <text x=\"60\" y=\"945\" fill=\"#aebfe1\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"12\">Report Gate A, Gate B, re-entry, destination control, and all retail controls separately. Runtime pending • unpromoted.</text>\n" +
            "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        RuntimeCandidateFinderReveal finderReveal)
    {
        string[] required =
        [
            "Return Home and Pause → Exit Level are two separate runtime outcomes",
            "Memory Card 1: **None**",
            "Memory Card 2: **None**",
            "**Do not save.**",
            "load the **CUE**, not the BIN",
            "Gate A — visible Return Home controller",
            "Gate B — Pause → Exit Level",
            "Gnasty's World, level 60",
            "route -1",
            "Do not use Pause → Exit Level in this run",
            "Do not touch Return Home in this run",
            "retired remote-v1 pad",
            "T96/T97",
            LabLinkMetadataFileName,
            Path.GetFileName(paths.OutputCuePath),
            "Normal Create BIN, app integration, release packaging, and promotion remain disabled"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The Return Home runtime checklist omitted an exact required boundary.");
        RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finderReveal, loadCodes);
    }

    private static void VerifyLocationGuide(
        string svg,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        string[] required =
        [
            "width=\"1200\" height=\"1000\"",
            "data-safe-left=\"60\" data-safe-right=\"1140\"",
            "ID65 Return Home + Pause Exit",
            "native T1353",
            "T1353 • N_z -16384 • assignment 0",
            "raw 124585,102954,8192",
            "T96 Return Home",
            "A • Touch Return Home",
            "B • Pause → Exit Level",
            "Gnasty's World (60)",
            "Cards 1/2: None • Do not save",
            "No remote-v1 pad • no foundation",
            "No terrain edits • SCUS preserved",
            "Landing / T92 / retail preserved",
            ProfileId,
            OutputPrefix + ".cue",
            "Runtime pending • unpromoted"
        ];
        if (required.Any(value => !svg.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The Return Home 1200x1000 guide omitted an exact required gate.");
        foreach (RuntimeCandidateLoadCode code in loadCodes)
        {
            if (!svg.Contains(XmlEscape(code.InputCode), StringComparison.Ordinal))
                throw new InvalidDataException($"The guide omitted the {code.TestName} comparison code.");
        }
    }

    private static IReadOnlyList<RuntimeCandidateLoadCode> BuildLoadCodes()
    {
        RuntimeCandidateLoadCode[] codes =
        [
            new("ID65 candidate", 65, "Left, then Down"),
            new("Gnasty's World destination control", 60, TestLevelWarpPatch.TargetSelectionText(60)),
            new("Retail Town Square", 13, TestLevelWarpPatch.TargetSelectionText(13)),
            new("Gnasty's Loot", 64, TestLevelWarpPatch.TargetSelectionText(64)),
            new("Sunny Flight", 15, TestLevelWarpPatch.TargetSelectionText(15))
        ];
        if (codes.Any(code => string.IsNullOrWhiteSpace(code.InputCode)))
            throw new InvalidDataException("The Return Home comparison load codes are incomplete.");
        return codes;
    }

    private static UnusedLevel65ReturnHomeNativeApronRowPatch BuildRowPatch(
        byte[] beforeData,
        byte[] afterData,
        int trueIndex,
        string role,
        long xyzWadOffset)
    {
        int rowOffset = ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength;
        byte[] before = beforeData.AsSpan(rowOffset, ObjectRecordByteLength).ToArray();
        byte[] after = afterData.AsSpan(rowOffset, ObjectRecordByteLength).ToArray();
        bool onlyXyz = before.AsSpan(0, 0x0C).SequenceEqual(after.AsSpan(0, 0x0C)) &&
            before.AsSpan(0x18).SequenceEqual(after.AsSpan(0x18));
        string beforeHash = Hash(before);
        string afterHash = Hash(after);
        string expectedBefore = trueIndex == T96
            ? "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4"
            : "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc";
        string expectedAfter = trueIndex == T96
            ? "ae2c6cc3bfd5ace302022647c4649154af44e2fe99e7b1953ebee07274e0f1a3"
            : "e590d9a096f4adf37b0d84fc7a14299ea35a756d4c8bc1caffa69a67e1e0bd66";
        if (!onlyXyz || beforeHash != expectedBefore || afterHash != expectedAfter)
            throw new InvalidDataException($"The exact T{trueIndex} direct placement row witness changed.");
        return new(
            trueIndex,
            role,
            Id65ObjectTableWadOffset + trueIndex * ObjectRecordByteLength,
            xyzWadOffset,
            ReadPoint(before, 0x0C),
            ReadPoint(after, 0x0C),
            beforeHash,
            afterHash,
            OnlyXyzChanged: true);
    }

    private static IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> DecodeCollisionTriangle(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 12)
            throw new InvalidDataException("A collision triangle must contain exactly 12 bytes.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        int x1 = (int)(xWord & 0x3FFF);
        int y1 = (int)(yWord & 0x3FFF);
        int z1 = (int)(zWord & 0x3FFF);
        return
        [
            new(x1, y1, z1),
            new(
                x1 + Signed9((int)((xWord >> 14) & 0x1FF)),
                y1 + Signed9((int)((yWord >> 14) & 0x1FF)),
                z1 + (int)((zWord >> 16) & 0xFF)),
            new(
                x1 + Signed9((int)((xWord >> 23) & 0x1FF)),
                y1 + Signed9((int)((yWord >> 23) & 0x1FF)),
                z1 + (int)((zWord >> 24) & 0xFF))
        ];
    }

    private static bool PointInsideTriangle(
        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> points,
        int rawX,
        int rawY,
        bool strict)
    {
        if (points.Count != 3)
            return false;
        long Cross(UnusedLevel65ReturnHomeNativeApronPoint a, UnusedLevel65ReturnHomeNativeApronPoint b) =>
            ((long)((b.X * 16) - (a.X * 16)) * (rawY - (a.Y * 16))) -
            ((long)((b.Y * 16) - (a.Y * 16)) * (rawX - (a.X * 16)));
        long c1 = Cross(points[0], points[1]);
        long c2 = Cross(points[1], points[2]);
        long c3 = Cross(points[2], points[0]);
        return strict
            ? (c1 > 0 && c2 > 0 && c3 > 0) || (c1 < 0 && c2 < 0 && c3 < 0)
            : (c1 >= 0 && c2 >= 0 && c3 >= 0) || (c1 <= 0 && c2 <= 0 && c3 <= 0);
    }

    private static bool TryInterpolateRawZ(
        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronPoint> points,
        int rawX,
        int rawY,
        out int rawZ,
        out long normalZ)
    {
        UnusedLevel65ReturnHomeNativeApronPoint a = points[0];
        UnusedLevel65ReturnHomeNativeApronPoint b = points[1];
        UnusedLevel65ReturnHomeNativeApronPoint c = points[2];
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        long normalX = (abY * acZ) - (abZ * acY);
        long normalY = (abZ * acX) - (abX * acZ);
        normalZ = (abX * acY) - (abY * acX);
        if (normalZ == 0)
        {
            rawZ = 0;
            return false;
        }
        long numerator = ((long)a.Z * 16 * normalZ) -
                         (normalX * (rawX - (a.X * 16L))) -
                         (normalY * (rawY - (a.Y * 16L)));
        rawZ = checked((int)Math.Round((double)numerator / normalZ, MidpointRounding.AwayFromZero));
        return true;
    }

    private static async Task<IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRawSectorDiff>>
        CompareRawImagesAsync(
            FileStream baseline,
            FileStream output,
            CancellationToken cancellationToken)
    {
        baseline.Position = 0;
        output.Position = 0;
        const int bufferLength = 4 * 1024 * 1024;
        byte[] before = new byte[bufferLength];
        byte[] after = new byte[bufferLength];
        long absolute = 0;
        Dictionary<int, int[]> counts = [];
        while (true)
        {
            int beforeRead = await baseline.ReadAsync(before, cancellationToken);
            int afterRead = await output.ReadAsync(after.AsMemory(0, beforeRead), cancellationToken);
            if (beforeRead != afterRead)
                throw new InvalidDataException("The Return Home image comparison encountered mismatched reads.");
            if (beforeRead == 0)
                break;
            for (int index = 0; index < beforeRead; index++)
            {
                if (before[index] == after[index])
                    continue;
                long fileOffset = absolute + index;
                int lba = checked((int)(fileOffset / RawSectorByteLength));
                int sectorOffset = checked((int)(fileOffset % RawSectorByteLength));
                if (!counts.TryGetValue(lba, out int[]? values))
                    counts.Add(lba, values = new int[8]);
                int category = sectorOffset switch
                {
                    < 12 => 0,
                    < 24 => 1,
                    < 2072 => 2,
                    < 2076 => 3,
                    < 2084 => 4,
                    < 2244 => 5,
                    _ => 6
                };
                values[category]++;
                values[7]++;
            }
            absolute += beforeRead;
        }
        return counts.OrderBy(pair => pair.Key)
            .Select(pair => new UnusedLevel65ReturnHomeNativeApronRawSectorDiff(
                pair.Key,
                pair.Value[0],
                pair.Value[1],
                pair.Value[2],
                pair.Value[3],
                pair.Value[4],
                pair.Value[5],
                pair.Value[6],
                pair.Value[7]))
            .ToArray();
    }

    private static string HashRawSectorDiffs(
        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRawSectorDiff> diffs)
    {
        string canonical = string.Concat(diffs.Select(diff =>
            $"{diff.RawSectorLba}|{diff.HeaderChangedBytes}|{diff.SubheaderChangedBytes}|" +
            $"{diff.PayloadChangedBytes}|{diff.EdcChangedBytes}|{diff.ReservedChangedBytes}|" +
            $"{diff.EccPChangedBytes}|{diff.EccQChangedBytes}|{diff.TotalChangedBytes}\n"));
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }

    private static void ValidateReadbackAgainstReceipt(
        CandidateReadback readback,
        UnusedLevel65ReturnHomeNativeApronReceipt receipt)
    {
        if (readback.OutputImageSha256 != receipt.OutputImageSha256 ||
            readback.OutputDataSha256 != receipt.OutputDataSha256 ||
            readback.ChangedLogicalWadBytes != ChangedLogicalWadBytes ||
            readback.ChangedPhysicalImageBytes != receipt.ChangedPhysicalImageBytes ||
            !readback.RawSectorDiffs.SequenceEqual(receipt.RawSectorDiffs) ||
            readback.RawSectorDiffSha256 != receipt.RawSectorDiffSha256 ||
            !readback.ExecutablePreserved || !readback.LandingAndPlayerAnchorPreserved ||
            !readback.TerrainPreserved || !readback.RetailTownSquarePreserved ||
            !readback.Mode2IntegrityVerified)
        {
            throw new InvalidDataException("Published Return Home image readback differs from its receipt.");
        }
        RequireOptionalHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "published Return Home BIN");
        RequireOptionalCount(
            readback.ChangedPhysicalImageBytes,
            ExpectedChangedPhysicalImageBytes,
            "published Return Home physical diff");
        RequireOptionalHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "published Return Home raw diff");
    }

    private static void ValidateReceiptIdentity(
        UnusedLevel65ReturnHomeNativeApronReceipt receipt,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        UnusedLevel65ReturnHomeNativeApronPlan plan = receipt.Plan;
        bool exactPaths =
            PathEquals(receipt.OutputDirectoryPath, paths.OutputDirectoryPath) &&
            PathEquals(receipt.OutputImagePath, paths.OutputImagePath) &&
            PathEquals(receipt.OutputCuePath, paths.OutputCuePath) &&
            PathEquals(receipt.LabLinkMetadataPath, paths.LabLinkMetadataPath) &&
            PathEquals(receipt.StaticReadbackReceiptPath, paths.StaticReadbackReceiptPath) &&
            PathEquals(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath) &&
            PathEquals(receipt.LocationGuidePath, paths.LocationGuidePath) &&
            PathEquals(receipt.FinderHelperPath, paths.FinderHelperPath);
        bool exactPins = receipt.SchemaVersion == ReceiptSchemaVersion &&
            receipt.ProfileId == ProfileId &&
            receipt.BaseImageSha256 == BaseImageSha256 &&
            receipt.BaseCueSha256 == BaseCueSha256 &&
            receipt.OutputDataSha256 == OutputDataSha256 &&
            IsSha256(receipt.OutputImageSha256) && IsSha256(receipt.OutputCueSha256) &&
            IsSha256(receipt.LabLinkMetadataSha256) && IsSha256(receipt.RuntimeChecklistSha256) &&
            IsSha256(receipt.LocationGuideSha256) && IsSha256(receipt.FinderHelperSha256) &&
            receipt.RebuiltRawSectorCount == 1 && receipt.ChangedRawSectorCount == 1 &&
            receipt.RawSectorDiffs.Count == 1 &&
            receipt.RawSectorDiffs[0].RawSectorLba == ChangedRawSectorLba &&
            receipt.RawSectorDiffSha256 == HashRawSectorDiffs(receipt.RawSectorDiffs);
        exactPins = exactPins &&
            receipt.OutputImageSha256 == ExpectedOutputImageSha256 &&
            receipt.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes &&
            receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256 &&
            receipt.LabLinkMetadataSha256 == ExpectedLabLinkMetadataSha256 &&
            (ExpectedLocationGuideSha256 == "PENDING" ||
             receipt.LocationGuideSha256 == ExpectedLocationGuideSha256);
        bool exactPlan = plan.SchemaVersion == PlanSchemaVersion && plan.ProfileId == ProfileId &&
            plan.StaticOwnershipProfileId == UnusedLevel65ReturnHomeOwnershipInspector.ProfileId &&
            plan.BaseImageSha256 == BaseImageSha256 && plan.BaseCueSha256 == BaseCueSha256 &&
            plan.SourceDataSha256 == SourceDataSha256 && plan.OutputDataSha256 == OutputDataSha256 &&
            plan.ExecutableSha256 == ExecutableSha256 && plan.Id65OverlaySha256 == Id65OverlaySha256 &&
            plan.RowPatches.Select(row => row.TrueIndex).SequenceEqual(new[] { 96, 97 }) &&
            plan.Support.CollisionTriangleIndex == CollisionTriangleIndex &&
            plan.Support.CollisionTriangleHex == CollisionTriangleHex &&
            plan.Support.CollisionLookupWadOffsets.SequenceEqual(CollisionLookupWadOffsets) &&
            plan.Support.NormalZ == -16_384 && plan.Support.SurfaceRawZ == GroundRawZ &&
            plan.Support.PointStrictlyInsideNativeTriangle &&
            plan.Support.NativeTriangleIsExposedTopmostAtPoint &&
            plan.Support.PositiveWindingTerrainExcluded &&
            plan.ComputedReturnHomeDestinationLevelId == 60 &&
            plan.ComputedPauseExitDestinationLevelId == 60 && plan.ComputedPauseExitRoute == -1 &&
            plan.CanonicalLabGroupSha256 == CanonicalLabGroupSha256 &&
            plan.ChangedLogicalWadBytes == ChangedLogicalWadBytes &&
            plan.LogicalXyzPatchBytes == LogicalXyzPatchBytes &&
            plan.ExecutablePatchBytes == 0 && plan.RetailWadPatchBytes == 0 &&
            plan.AffectedRawSectorLbas.SequenceEqual(new[] { ChangedRawSectorLba }) &&
            plan.RawSectorDiffSha256 == receipt.RawSectorDiffSha256 &&
            plan.ExactT96T97AtomicRelocationVerified && plan.ExactLabLinkMetadataVerified &&
            plan.NativeSupportVerified && plan.LandingAndPlayerAnchorPreserved &&
            plan.TerrainPreserved && plan.ExecutablePreserved && plan.RetailLevelsPreserved &&
            !plan.MemoryCardsRequired && !plan.SavingAuthorized && !plan.RuntimeVerified &&
            plan.DisposableRuntimeCandidateAuthorized && !plan.AppIntegrated &&
            !plan.NormalCreateBinEnabled && !plan.PromotionAuthorized && !plan.ReleaseAuthorized;
        bool failClosed = receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified && receipt.CanonicalLabMetadataVerified &&
            receipt.CanonicalReceiptVerified && receipt.Mode2IntegrityVerified &&
            receipt.BaseCandidatePreserved && receipt.FullDirectoryPublicationVerified &&
            !receipt.RollbackRecoveryVerified && !receipt.FullDirectoryRollbackVerified &&
            receipt.FinderHandoffVerified && !receipt.RuntimeVerified &&
            receipt.DisposableRuntimeCandidateAuthorized && !receipt.AppIntegrated &&
            !receipt.NormalCreateBinEnabled && !receipt.PromotionAuthorized && !receipt.ReleaseAuthorized;
        if (!exactPaths || !exactPins || !exactPlan || !failClosed)
            throw new InvalidDataException("The Return Home receipt lost an exact path, pin, or fail-closed flag.");
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static void VerifyExactStagedFiles(
        string stageDirectory,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths finalPaths)
    {
        string[] expected = ExpectedPublishedFileNames(finalPaths);
        string[] actual = Directory.EnumerateFileSystemEntries(stageDirectory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The staged Return Home candidate is not exactly seven files.");
    }

    private static void VerifyExactPublishedFiles(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        string[] expected = ExpectedPublishedFileNames(paths);
        string[] actual = Directory.EnumerateFileSystemEntries(paths.OutputDirectoryPath)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The published Return Home candidate is not exactly seven files.");
        foreach (string file in Directory.EnumerateFiles(paths.OutputDirectoryPath))
            RejectReparsePoint(file, "published Return Home artifact");
        if (!OperatingSystem.IsMacOS() ||
            File.GetUnixFileMode(paths.FinderHelperPath) !=
            (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
             UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherExecute))
        {
            throw new InvalidDataException("The Finder helper lost exact 0755 permissions.");
        }
    }

    private static string[] ExpectedPublishedFileNames(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths) =>
    [
        Path.GetFileName(paths.OutputImagePath),
        Path.GetFileName(paths.OutputCuePath),
        Path.GetFileName(paths.LabLinkMetadataPath),
        Path.GetFileName(paths.StaticReadbackReceiptPath),
        Path.GetFileName(paths.RuntimeChecklistPath),
        Path.GetFileName(paths.LocationGuidePath),
        Path.GetFileName(paths.FinderHelperPath)
    ];

    private static WriterLease AcquireWriterLease(string path)
    {
        string parent = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The writer lease has no parent.");
        Directory.CreateDirectory(parent);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, path))
                continue;
            RejectReparsePoint(entry, "Return Home writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The Return Home writer lease path is a directory.");
            break;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException ex) when (
            !OperatingSystem.IsWindows() && IsUnixWouldBlock(ex.HResult & 0xFFFF))
        {
            throw CreateActiveWriterLeaseException(ex);
        }
        catch (IOException ex)
        {
            throw new IOException("The Return Home writer lease file could not be opened.", ex);
        }

        try
        {
            RejectReparsePoint(path, "Return Home writer lease");
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stream.Lock(0, 1);
                }
                catch (IOException ex) when (IsWindowsLockContention(ex))
                {
                    throw CreateActiveWriterLeaseException(ex);
                }
                catch (IOException ex)
                {
                    throw new IOException("The Return Home writer OS range lease could not be acquired.", ex);
                }
                return new WriterLease(stream);
            }

            int fileDescriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
            if (NativeFlock(fileDescriptor, LockExclusive | LockNonBlocking) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                Win32Exception nativeError = new(error);
                if (IsUnixWouldBlock(error))
                    throw CreateActiveWriterLeaseException(nativeError);
                throw new IOException(
                    $"The Return Home writer OS lease failed (errno {error}: {nativeError.Message}).",
                    nativeError);
            }
            return new WriterLease(stream);
        }
        catch
        {
            stream.Dispose();
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

    private static bool RecoverOwnedOperations(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        if (!Directory.Exists(paths.OperationsDirectoryPath))
            return false;
        RejectReparsePoint(paths.OperationsDirectoryPath, "Return Home operations directory");
        bool recovered = false;
        foreach (string operationRoot in Directory.EnumerateDirectories(paths.OperationsDirectoryPath)
                     .Order(StringComparer.Ordinal))
        {
            if (!Path.GetFileName(operationRoot).StartsWith(OperationPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("The Return Home operations directory contains a foreign entry.");
            RejectReparsePoint(operationRoot, "Return Home operation");
            string journalPath = Path.Combine(operationRoot, JournalFileName);
            if (!File.Exists(journalPath))
                throw new InvalidDataException("A Return Home operation is missing its journal.");
            OperationJournal journal = ReadJournal(journalPath);
            ValidateJournal(journal, paths, operationRoot);
            RecoverOperation(journal, paths);
            recovered = true;
        }
        DeleteOperationsDirectoryIfEmpty(paths.OperationsDirectoryPath);
        return recovered;
    }

    private static void RecoverOperation(
        OperationJournal journal,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        bool committed = journal.Phase == PhaseCommitted;
        bool outputExists = Directory.Exists(paths.OutputDirectoryPath);
        bool stageExists = Directory.Exists(journal.StageDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (committed)
        {
            if (!outputExists)
            {
                throw new IOException(
                    "A committed Return Home operation is missing its candidate; recovery was preserved for audit.");
            }
            VerifyExactPublishedFiles(paths);
            if (backupExists)
                DeleteOwnedDirectory(journal.BackupDirectoryPath, journal.OperationRootPath, "backup");
        }
        else
        {
            bool candidatePublished = journal.Phase == PhasePublished ||
                journal.Phase == PhasePublishIntent && outputExists && !stageExists;
            if (candidatePublished && journal.PreviousOutputExisted && !backupExists)
            {
                throw new IOException(
                    "An uncommitted Return Home publication lost its prior backup; the current output was preserved for audit.");
            }
            if (candidatePublished && outputExists)
            {
                DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
                outputExists = false;
            }
            if (backupExists)
            {
                if (outputExists)
                {
                    throw new IOException(
                        "Recovery cannot restore the prior Return Home candidate over an ambiguous existing directory.");
                }
                Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
            }
        }
        if (Directory.Exists(journal.StageDirectoryPath))
            DeleteOwnedDirectory(journal.StageDirectoryPath, journal.OperationRootPath, "stage");
        DeleteJournal(journal.JournalPath);
        DeleteOwnedDirectory(journal.OperationRootPath, paths.OperationsDirectoryPath, OperationPrefix);
    }

    private static void RollBackOperation(
        OperationJournal journal,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        if (journal.Phase == PhaseCommitted)
            throw new InvalidOperationException("A committed Return Home publication cannot enter rollback.");
        List<Exception> failures = [];
        bool outputExists = Directory.Exists(paths.OutputDirectoryPath);
        bool stageExists = Directory.Exists(journal.StageDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        bool candidatePublished = journal.Phase == PhasePublished ||
            journal.Phase == PhasePublishIntent && outputExists && !stageExists;
        try
        {
            if (candidatePublished && journal.PreviousOutputExisted && !backupExists)
            {
                throw new IOException(
                    "Rollback lost the prior Return Home backup; the current output was preserved for audit.");
            }
            if (candidatePublished && outputExists)
            {
                DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
                outputExists = false;
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        try
        {
            if (backupExists)
            {
                if (outputExists)
                    DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
                Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        try
        {
            if (Directory.Exists(journal.StageDirectoryPath))
                DeleteOwnedDirectory(journal.StageDirectoryPath, journal.OperationRootPath, "stage");
            DeleteJournal(journal.JournalPath);
            if (Directory.Exists(journal.OperationRootPath))
                DeleteOwnedDirectory(journal.OperationRootPath, paths.OperationsDirectoryPath, OperationPrefix);
            DeleteOperationsDirectoryIfEmpty(paths.OperationsDirectoryPath);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        if (failures.Count > 0)
            throw new IOException("Return Home publication failed and rollback was incomplete.", new AggregateException(failures));
    }

    private static void WriteJournal(OperationJournal journal)
    {
        Directory.CreateDirectory(journal.OperationRootPath);
        string temporary = journal.JournalPath + ".tmp";
        string text = JsonSerializer.Serialize(journal, JsonOptions) + "\n";
        File.WriteAllText(temporary, text, new UTF8Encoding(false));
        using (FileStream stream = new(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            stream.Flush(flushToDisk: true);
        File.Move(temporary, journal.JournalPath, overwrite: true);
    }

    private static OperationJournal ReadJournal(string path)
    {
        string text = File.ReadAllText(path);
        OperationJournal journal = JsonSerializer.Deserialize<OperationJournal>(text, JsonOptions)
            ?? throw new InvalidDataException("A Return Home operation journal is empty.");
        if (text != JsonSerializer.Serialize(journal, JsonOptions) + "\n")
            throw new InvalidDataException("A Return Home operation journal is not canonical JSON.");
        return journal;
    }

    private static void ValidateJournal(
        OperationJournal journal,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
        string operationRoot)
    {
        string[] phases =
        [
            PhaseCreated, PhaseStaged, PhaseBackupIntent, PhasePreviousBackedUp,
            PhasePublishIntent, PhasePublished, PhaseCommitted
        ];
        if (journal.SchemaVersion != JournalSchemaVersion ||
            !phases.Contains(journal.Phase, StringComparer.Ordinal) ||
            !PathEquals(journal.OperationRootPath, operationRoot) ||
            !PathEquals(journal.OutputDirectoryPath, paths.OutputDirectoryPath) ||
            !PathEquals(journal.StageDirectoryPath, Path.Combine(operationRoot, "stage")) ||
            !PathEquals(journal.BackupDirectoryPath, Path.Combine(operationRoot, "backup")) ||
            !PathEquals(journal.JournalPath, Path.Combine(operationRoot, JournalFileName)))
        {
            throw new InvalidDataException("A Return Home operation journal escaped its exact owned paths.");
        }
    }

    internal static void SeedInterruptedRecoveryFixtureForSmoke(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        if (Directory.Exists(paths.OutputDirectoryPath) || Directory.Exists(paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The recovery fixture requires an empty output/operations state.");
        string operationId = "smoke" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(paths.OperationsDirectoryPath, OperationPrefix + operationId);
        string stage = Path.Combine(root, "stage");
        string backup = Path.Combine(root, "backup");
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(stage, "stale-stage.txt"), "stale-stage\n");
        File.WriteAllText(Path.Combine(backup, "prior-candidate.txt"), "prior-candidate\n");
        OperationJournal journal = new(
            JournalSchemaVersion,
            operationId,
            paths.OutputDirectoryPath,
            root,
            stage,
            backup,
            Path.Combine(root, JournalFileName),
            PhasePublishIntent,
            PreviousOutputExisted: true);
        WriteJournal(journal);
    }

    internal static bool RecoverOwnedOperationsForSmoke(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths) =>
        RecoverOwnedOperations(paths);

    private static void RequireSafeRoles(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        if (!Path.IsPathFullyQualified(workspaceRoot) || !Path.IsPathFullyQualified(baseImage) ||
            !Path.IsPathFullyQualified(baseCue) || !Path.IsPathFullyQualified(paths.OutputDirectoryPath))
        {
            throw new InvalidOperationException("Return Home publication roles must use absolute paths.");
        }
        if (PathEquals(baseImage, baseCue) ||
            IsDescendantOrEqual(baseImage, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseImage) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseCue) ||
            IsDescendantOrEqual(baseImage, paths.OperationsDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OperationsDirectoryPath))
        {
            throw new InvalidOperationException("Return Home input/output roles overlap unsafely.");
        }
        RejectExistingAncestorReparsePoints(workspaceRoot, "Return Home workspace");
        RejectExistingAncestorReparsePoints(baseImage, "Return Home base BIN");
        RejectExistingAncestorReparsePoints(baseCue, "Return Home base CUE");
        RejectExistingAncestorReparsePoints(paths.OutputDirectoryPath, "Return Home output");
        RejectExistingAncestorReparsePoints(paths.OperationsDirectoryPath, "Return Home operations");
        RejectExistingAncestorReparsePoints(paths.WriterLeasePath, "Return Home writer lease");
        RejectReparsePoint(baseImage, "Return Home base BIN");
        RejectReparsePoint(baseCue, "Return Home base CUE");
        RejectReparsePoint(paths.PublicationParentPath, "Return Home publication parent");
        if (Directory.Exists(paths.OutputDirectoryPath))
            RejectReparsePoint(paths.OutputDirectoryPath, "existing Return Home output");
        if (Directory.Exists(paths.OperationsDirectoryPath))
            RejectReparsePoint(paths.OperationsDirectoryPath, "existing Return Home operations");
    }

    private static void RequirePathsInsideOutput(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
    {
        string[] artifacts =
        [
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.LabLinkMetadataPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath
        ];
        if (artifacts.Any(path => !IsStrictDescendant(path, paths.OutputDirectoryPath)) ||
            !PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), paths.PublicationParentPath) ||
            !PathEquals(Path.GetDirectoryName(paths.WriterLeasePath), paths.PublicationParentPath))
        {
            throw new InvalidOperationException("A Return Home artifact escaped its owned output directory.");
        }
    }

    private static void RequirePreservedWadRange(
        FileStream baseline,
        FileStream output,
        DiscLayout layout,
        long wadOffset,
        int byteLength,
        string expectedSha256,
        string label)
    {
        byte[] before = DiscImage.ReadFileBytes(baseline, layout, WadLba, wadOffset, byteLength);
        byte[] after = DiscImage.ReadFileBytes(output, layout, WadLba, wadOffset, byteLength);
        RequireHash(before, expectedSha256, $"base {label}");
        if (!before.SequenceEqual(after))
            throw new InvalidDataException($"The Return Home candidate changed {label}.");
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
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
        {
            throw new InvalidDataException(
                $"{name} resolved to LBA {record.Lba}/0x{record.Size:X}, expected LBA {expectedLba}/0x{expectedSize:X}.");
        }
        return record;
    }

    private static void PatchXyz(byte[] data, int trueIndex, int x, int y, int z)
    {
        int offset = ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength + 0x0C;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 4, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 8, 4), z);
    }

    private static byte[] EncodePoint(UnusedLevel65ReturnHomeNativeApronPoint point)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), point.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), point.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), point.Z);
        return bytes;
    }

    private static UnusedLevel65ReturnHomeNativeApronPoint ReadPoint(
        ReadOnlySpan<byte> bytes,
        int offset) =>
        new(
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8, 4)));

    private static void RequirePoint(
        ReadOnlySpan<byte> bytes,
        UnusedLevel65ReturnHomeNativeApronPoint expected,
        string label)
    {
        UnusedLevel65ReturnHomeNativeApronPoint actual = ReadPoint(bytes, 0);
        if (actual != expected)
            throw new InvalidDataException($"{label} changed to {actual}, expected {expected}.");
    }

    private static int[] DifferentByteIndexes(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The Return Home proposal changed ID65 data length.");
        return Enumerable.Range(0, before.Length).Where(index => before[index] != after[index]).ToArray();
    }

    private static int Signed9(int value) => value >= 0x100 ? value - 0x200 : value;

    private static bool LabGroupsEqual(
        UnusedLevel65ReturnHomeNativeApronLabLinkGroup first,
        UnusedLevel65ReturnHomeNativeApronLabLinkGroup second) =>
        first.Key == second.Key && first.Name == second.Name && first.Kind == second.Kind &&
        first.LinkedMove == second.LinkedMove && first.Confidence == second.Confidence &&
        first.Basis == second.Basis && first.Reason == second.Reason &&
        first.TrueIndexes.SequenceEqual(second.TrueIndexes) &&
        first.MemberIds.SequenceEqual(second.MemberIds);

    private static void RequireMode2(DiscLayout layout, string label)
    {
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException($"The {label} is not exact MODE2/2352 with user offset 24.");
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private static void RequireHex(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Convert.ToHexString(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} changed: expected {expected}, got {actual}.");
    }

    private static void RequireOptionalHash(string actual, string expected, string label)
    {
        if (expected != "PENDING" && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private static void RequireOptionalCount(long actual, long expected, string label)
    {
        if (expected >= 0 && actual != expected)
            throw new InvalidDataException($"{label} changed: expected {expected}, got {actual}.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task RequireFileHashAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        string actual = await HashFileAsync(path, cancellationToken);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{path} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private static async Task WriteTextAsync(
        string path,
        string text,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, text, encoding, cancellationToken);
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Flush(flushToDisk: true);
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
        await WriteTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions) + "\n",
            new UTF8Encoding(false),
            cancellationToken);

    private static void VerifyTextReadback(string path, string expected, string label)
    {
        string actual = File.ReadAllText(path);
        if (actual != expected)
            throw new InvalidDataException($"The {label} failed exact text readback.");
    }

    private static string StagedPath(string stageDirectory, string finalPath) =>
        Path.Combine(stageDirectory, Path.GetFileName(finalPath));

    private static string RequireDirectory(string path, string label)
    {
        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"The {label} is missing: {full}");
        return full;
    }

    private static string RequireFile(string path, string label)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"The {label} is missing.", full);
        return full;
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
            throw new InvalidOperationException($"The {label} cannot be a symlink/reparse point: {path}");
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

    private static void DeleteOwnedDirectory(string path, string parent, string exactNameOrPrefix)
    {
        string full = Path.GetFullPath(path);
        string expectedParent = Path.GetFullPath(parent);
        string? actualParent = Path.GetDirectoryName(full);
        string name = Path.GetFileName(full);
        bool exact = exactNameOrPrefix == OutputDirectoryName
            ? name == exactNameOrPrefix
            : name == exactNameOrPrefix || name.StartsWith(exactNameOrPrefix, StringComparison.Ordinal);
        if (!PathEquals(actualParent, expectedParent) || !exact)
            throw new InvalidOperationException("Refused to delete a directory outside Return Home ownership.");
        RejectReparsePoint(full, "owned Return Home directory");
        Directory.Delete(full, recursive: true);
    }

    private static void DeleteJournal(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        string temporary = path + ".tmp";
        if (File.Exists(temporary))
            File.Delete(temporary);
    }

    private static void DeleteOperationsDirectoryIfEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    private static bool IsStrictDescendant(string path, string parent) =>
        IsDescendantOrEqual(path, parent) && !PathEquals(path, parent);

    private static bool IsDescendantOrEqual(string path, string parent)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullParent, PathComparison);
    }

    private static bool PathEquals(string? first, string? second) =>
        first != null && second != null &&
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string ShellSingleQuote(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private sealed record CandidateReadback(
        string OutputImageSha256,
        string OutputDataSha256,
        int ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65ReturnHomeNativeApronRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256,
        bool ExecutablePreserved,
        bool LandingAndPlayerAnchorPreserved,
        bool TerrainPreserved,
        bool RetailTownSquarePreserved,
        bool Mode2IntegrityVerified);

    private sealed record StagedCandidate(
        UnusedLevel65ReturnHomeNativeApronPlan Plan,
        UnusedLevel65ReturnHomeNativeApronReceipt Receipt,
        CandidateReadback Readback);

    private sealed record SurfaceHit(int TriangleIndex, int RawZ, long NormalZ);

    private sealed record OperationJournal(
        int SchemaVersion,
        string OperationId,
        string OutputDirectoryPath,
        string OperationRootPath,
        string StageDirectoryPath,
        string BackupDirectoryPath,
        string JournalPath,
        string Phase,
        bool PreviousOutputExisted);

    private sealed class WriterLease(FileStream stream) : IDisposable
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
                // Closing the descriptor below releases the OS lock even if an explicit unlock
                // races with process shutdown or an external filesystem failure.
            }
            finally
            {
                ownedStream.Dispose();
            }
        }
    }
}
