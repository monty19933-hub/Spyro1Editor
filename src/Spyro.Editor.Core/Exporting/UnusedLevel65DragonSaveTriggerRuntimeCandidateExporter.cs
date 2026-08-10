using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65DragonSaveTriggerRuntimeCandidateRequest(
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths(
    string PublicationParentPath,
    string OutputDirectoryPath,
    string OperationsDirectoryPath,
    string WriterLeasePath,
    string OutputImagePath,
    string OutputCuePath,
    string ConstructionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath);

internal sealed record UnusedLevel65DragonSaveTriggerPoint(int X, int Y, int Z);

internal sealed record UnusedLevel65DragonSaveTriggerRowPatch(
    int TrueIndex,
    string Role,
    long RowWadOffset,
    long XyzWadOffset,
    UnusedLevel65DragonSaveTriggerPoint Before,
    UnusedLevel65DragonSaveTriggerPoint After,
    string BeforeRowSha256,
    string AfterRowSha256,
    bool OnlyXyzChanged);

internal sealed record UnusedLevel65DragonSaveTriggerCameraPatch(
    string Role,
    long WadOffset,
    int FrameCount,
    int FrameStride,
    int XyzByteLengthPerFrame,
    string BeforeSha256,
    string AfterSha256,
    int ChangedLogicalBytes,
    bool EveryXyzTranslated,
    bool NonXyzBytesPreserved);

internal sealed record UnusedLevel65DragonSaveTriggerRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65DragonSaveTriggerPlan(
    int SchemaVersion,
    string ProfileId,
    string BaseImageSha256,
    string BaseCueSha256,
    string SourceDataSha256,
    string OutputDataSha256,
    string SourceExecutableSha256,
    string OutputExecutableSha256,
    string Id65OverlaySha256,
    string RetailTownSquareOverlaySha256,
    string RetailTownSquareDataSha256,
    IReadOnlyList<UnusedLevel65DragonSaveTriggerRowPatch> RowPatches,
    UnusedLevel65DragonSaveTriggerCameraPatch CameraLeadPatch,
    UnusedLevel65DragonSaveTriggerCameraPatch CameraFrameTrackPatch,
    int TranslationRawX,
    int TranslationRawY,
    int TranslationRawZ,
    int DragonTargetSlotLogicalOffset,
    int DragonTargetBefore,
    int DragonTargetAfter,
    IReadOnlyList<int> ChangedDataByteOffsets,
    int LogicalPatchWindowBytes,
    int ChangedLogicalWadBytes,
    int ChangedLogicalExecutableBytes,
    int ChangedLogicalBytes,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool ExactT100T101T105AtomicRelocationVerified,
    bool ExactCameraLeadTranslationVerified,
    bool Exact112FrameCameraTrackTranslationVerified,
    bool AnglesLinksActorsPropertiesAndFixupsPreserved,
    bool OnlyDragonTargetExecutableByteChanged,
    bool UnprunedLockedBasePreserved,
    bool RetailLevelsPreserved,
    bool MemoryCardsRequired,
    bool SavingAuthorized,
    bool RuntimeVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized,
    bool ReleaseAuthorized);

internal sealed record UnusedLevel65DragonSaveTriggerReceipt(
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
    string FinderHelperPath,
    string OutputImageSha256,
    string OutputCueSha256,
    string ConstructionPlanSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string FinderHelperSha256,
    string BaseImageSha256,
    string BaseCueSha256,
    string OutputDataSha256,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65DragonSaveTriggerRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    UnusedLevel65DragonSaveTriggerPlan Plan,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool CanonicalConstructionPlanVerified,
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

internal sealed record UnusedLevel65DragonSaveTriggerPublishedArtifactHashes(
    string ReceiptSha256,
    string OutputCueSha256,
    string ConstructionPlanSha256,
    string RuntimeChecklistSha256,
    string LocationGuideSha256,
    string FinderHelperSha256);

internal sealed record UnusedLevel65DragonSaveTriggerRuntimeCandidateResult(
    UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths Paths,
    UnusedLevel65DragonSaveTriggerPlan Plan,
    UnusedLevel65DragonSaveTriggerReceipt Receipt,
    UnusedLevel65DragonSaveTriggerPublishedArtifactHashes ArtifactHashes);

/// <summary>
/// Publishes an isolated, disposable no-card dragon/save-trigger runtime gate
/// directly from the locked, unpruned ID65 display-name BIN. The transaction
/// translates only T100/T101/T105 XYZ, T101's rescue-camera lead and all 112
/// packed camera-frame XYZ triples, then changes only slot 35's dragon target
/// from zero to four. App, Create BIN, saving, and promotion remain disabled.
/// </summary>
internal static class UnusedLevel65DragonSaveTriggerRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-dragon-save-trigger-no-card-usa-disposable-v1";
    public const string OutputDirectoryName = "unused-level-65-dragon-save-trigger-runtime-candidate";
    public const string OutputPrefix =
        "Unused-Level-65-Dragon-Save-Trigger-No-Card-RUNTIME-CANDIDATE";
    public const string ConstructionPlanFileName = OutputPrefix + "-construction-plan.json";
    public const string RequiredSmokeAssemblyName =
        "Spyro.Editor.UnusedLevel65DragonSaveTriggerRuntimeCandidateSmoke";

    public const string BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string BaseCueSha256 =
        "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2";
    public const string SourceDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string OutputDataSha256 =
        "a1c77b435d2f2b92debcc8c9a2f43a7be9ae56d9a304baef11f3173daadf81b3";
    public const string SourceExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string OutputExecutableSha256 =
        "db3b4dab4430fb09e71d17af26248940f00c4a443fc2fed3297d759377647183";
    public const string Id65OverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    public const string RetailTownSquareDataSha256 = SourceDataSha256;
    public const string RetailTownSquareOverlaySha256 = Id65OverlaySha256;

    public const string ExpectedOutputImageSha256 =
        "9614f8caf17d20d3acae635d5d39abb3be37ccf13cac92e23b86f642e1959c88";
    public const long ExpectedChangedPhysicalImageBytes = 1_029;
    public const string ExpectedRawSectorDiffSha256 =
        "d0c838004604e4060b7b46d47d23a9421c0514272bff5d12f4a4e349bf1b4f47";
    public const string ExpectedConstructionPlanSha256 =
        "7d4088e21724ebc4ae85c894ebdd6223afca398e957ca0a99b0c4a8d92820074";
    public const string ExpectedLocationGuideSha256 =
        "7665dbcd6cccf59f447422bec39ddb54e743baafe601f50b1aca4b443c5ab166";
    public const string ExpectedFinalReceiptSha256 =
        "6bf2af021a7c5841ec498178173ab22e546841d410cb3b34d54c8f0b42a036c6";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int JournalSchemaVersion = 1;
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
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
    private const int T100 = 100;
    private const int T101 = 101;
    private const int T105 = 105;
    private const long T100XyzWadOffset = 0x6B08BDC;
    private const long T101XyzWadOffset = 0x6B08C34;
    private const long T105XyzWadOffset = 0x6B08D94;
    private const long T101CameraLeadWadOffset = 0x6B0C600;
    private const long T101CameraFramesWadOffset = 0x6C17D68;
    private const int CameraFrameCount = 112;
    private const int CameraFrameStride = 24;
    private const int CameraFrameXyzByteLength = 12;
    private const int CameraLeadSemanticByteLength = 0x44;
    private const int CameraFramesByteLength = CameraFrameCount * CameraFrameStride;
    private const int DragonTargetSlotLogicalOffset = 0x5FC37;
    private const byte DragonTargetBefore = 0;
    private const byte DragonTargetAfter = 4;
    private const int TranslationRawX = 256;
    private const int TranslationRawY = 256;
    private const int TranslationRawZ = -40;
    private const int LogicalPatchWindowBytes =
        (3 * 12) + 12 + (CameraFrameCount * CameraFrameXyzByteLength) + 1;
    private const int ChangedLogicalWadBytes = 349;
    private const int ChangedLogicalExecutableBytes = 1;
    private const int ChangedLogicalBytes = 350;
    private static readonly int[] AffectedRawSectorLbas = [54_838, 54_845, 55_380, 55_381, 55_573];
    private static readonly byte[] AffectedRawSectorSubmodes = [0x08, 0x08, 0x08, 0x89, 0x08];

    private const string T100RowSha256 =
        "d1122aff9484123daf548a02a91bd28c9ebce478b247c5332cc2471789168bd2";
    private const string T101RowSha256 =
        "4d95e3ba2eb85b808471c9c4cdeb3ec674d7cdb5eeabeb32344eec0b374f251a";
    private const string T105RowSha256 =
        "0cd91a6fe6e505da2488490b89977a72dcc3b5c28071872100011c2c0083a4a4";
    private const string T100OutputRowSha256 =
        "0f4f14e81252246c544afefafbe8ea5cc832f9f4b01d85a3e41285c0621e9d77";
    private const string T101OutputRowSha256 =
        "0efb8689056bc1d15332b53d7f971e359cc4bd108d9f035cb9cf201346340e53";
    private const string T105OutputRowSha256 =
        "a2cb3ceb6775f247ec39dffd4e83575df9ccc5fd758638e3179e73b289d812c6";
    private const string CameraLeadBeforeSha256 =
        "443ab95f3cbcbbfcc2f0918c4556e3c306d20ceafa7f882b76b150e30828348e";
    private const string CameraLeadAfterSha256 =
        "389080ee03c5d21945dc1b7be0a2aaff9be285919dc332817dc9b0a02ead8878";
    private const string CameraFramesBeforeSha256 =
        "a61145a17e20759a292fc508fb43453728341c5bb2c65f7f286c294349895156";
    private const string CameraFramesAfterSha256 =
        "7e1e185caa59623a08007bd684b27a5d9a1d0077a33520ef31bb9fdd5f1c75e6";

    private const string OperationsDirectoryName =
        ".unused-level-65-dragon-save-trigger-runtime-candidate-operations";
    private const string WriterLeaseFileName =
        ".unused-level-65-dragon-save-trigger-runtime-candidate-writer.lease";
    private const string OperationPrefix = "dragon-save-trigger-operation-";
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
        "Another ID65 dragon/save-trigger candidate writer is active; no publication operation was started.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            throw new ArgumentException("The dragon save-trigger output directory is missing.", nameof(outputDirectoryPath));
        string output = Path.GetFullPath(outputDirectoryPath);
        if (!string.Equals(Path.GetFileName(output), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The dragon save-trigger writer owns only a directory named '{OutputDirectoryName}'.");
        }
        string parent = Path.GetDirectoryName(output)
            ?? throw new InvalidOperationException("The dragon save-trigger publication parent is missing.");
        string prefix = Path.Combine(output, OutputPrefix);
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths = new(
            parent,
            output,
            Path.Combine(parent, OperationsDirectoryName),
            Path.Combine(parent, WriterLeaseFileName),
            prefix + ".bin",
            prefix + ".cue",
            Path.Combine(output, ConstructionPlanFileName),
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command");
        RequirePathsInsideOutput(paths);
        return paths;
    }

    public static async Task<UnusedLevel65DragonSaveTriggerRuntimeCandidateResult> CreateAsync(
        UnusedLevel65DragonSaveTriggerRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string root = RequireDirectory(request.WorkspaceRoot, "Spyro Editor workspace");
        string baseImage = RequireFile(request.BaseImagePath, "locked display-name BIN");
        string baseCue = RequireFile(request.BaseCuePath, "locked display-name CUE");
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths =
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
                "The dragon save-trigger runtime candidate already exists. Set ReplaceExistingCandidate only for an intentional deterministic rebuild.");
        }

        await RequireFileHashAsync(baseImage, BaseImageSha256, cancellationToken);
        await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        UnusedLevel65DragonSaveTriggerPlan staticPlan =
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

            UnusedLevel65DragonSaveTriggerRuntimeCandidateResult published =
                await VerifyPublishedAsync(paths, cancellationToken);
            request.TestStageHook?.Invoke("before-final-staged-published-comparison");
            if (JsonSerializer.Serialize(published.Plan, JsonOptions) !=
                    JsonSerializer.Serialize(staged.Plan, JsonOptions) ||
                published.Receipt.OutputImageSha256 != staged.Receipt.OutputImageSha256)
            {
                throw new InvalidDataException("Published dragon save-trigger readback differs from the verified stage.");
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

    public static async Task<UnusedLevel65DragonSaveTriggerRuntimeCandidateResult> VerifyPublishedAsync(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths exact =
            CreatePaths(paths.OutputDirectoryPath);
        if (paths != exact)
            throw new InvalidDataException("The dragon save-trigger verification paths are not canonical.");
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new DirectoryNotFoundException("The published dragon save-trigger candidate is missing.");
        RejectReparsePoint(paths.OutputDirectoryPath, "published dragon save-trigger directory");
        VerifyExactPublishedFiles(paths);

        string receiptText = await File.ReadAllTextAsync(paths.StaticReadbackReceiptPath, cancellationToken);
        UnusedLevel65DragonSaveTriggerReceipt receipt =
            JsonSerializer.Deserialize<UnusedLevel65DragonSaveTriggerReceipt>(receiptText, JsonOptions)
            ?? throw new InvalidDataException("The dragon save-trigger receipt is empty.");
        string canonicalReceipt = JsonSerializer.Serialize(receipt, JsonOptions) + "\n";
        if (!string.Equals(receiptText, canonicalReceipt, StringComparison.Ordinal))
            throw new InvalidDataException("The dragon save-trigger receipt is not canonical JSON.");
        ValidateReceiptIdentity(receipt, paths);

        string baseImage = RequireFile(receipt.BaseImagePath, "receipt-bound locked display-name BIN");
        string baseCue = RequireFile(receipt.BaseCuePath, "receipt-bound locked display-name CUE");
        await RequireFileHashAsync(baseImage, BaseImageSha256, cancellationToken);
        await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");

        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            paths.OutputImagePath,
            cancellationToken);
        ValidateReadbackAgainstReceipt(readback, receipt);

        UnusedLevel65DragonSaveTriggerPlan expectedPlan =
            (await BuildStaticPlanAsync(string.Empty, baseImage, baseCue, cancellationToken)) with
            {
                RawSectorDiffSha256 = readback.RawSectorDiffSha256
            };
        string expectedPlanText = BuildCanonicalConstructionPlan(expectedPlan);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            expectedPlanText,
            "published dragon save-trigger construction plan");
        VerifyCanonicalConstructionPlan(
            await File.ReadAllTextAsync(paths.ConstructionPlanPath, cancellationToken),
            expectedPlan);

        string expectedCueText = DiscImage.BuildCueText(
            baseCue,
            Path.GetFileName(paths.OutputImagePath));
        VerifyTextReadback(paths.OutputCuePath, expectedCueText, "published dragon save-trigger CUE");

        RuntimeCandidateFinderReveal finalReveal = BuildFinalFinderReveal(paths);
        string expectedChecklist = BuildRuntimeChecklist(paths, finalReveal, expectedPlan, readback);
        VerifyTextReadback(
            paths.RuntimeChecklistPath,
            expectedChecklist,
            "published dragon save-trigger checklist");
        VerifyChecklist(expectedChecklist, paths, expectedPlan.LoadCodes, finalReveal);

        string expectedGuide = BuildLocationGuideSvg(
            expectedPlan.LoadCodes,
            readback.OutputImageSha256);
        VerifyTextReadback(
            paths.LocationGuidePath,
            expectedGuide,
            "published dragon save-trigger location guide");
        VerifyLocationGuide(expectedGuide, expectedPlan.LoadCodes);

        VerifyTextReadback(
            paths.FinderHelperPath,
            BuildFinderHelperText(paths),
            "published dragon save-trigger Finder helper");

        string outputCueSha256 = HashFile(paths.OutputCuePath);
        string planSha256 = HashFile(paths.ConstructionPlanPath);
        string checklistSha256 = HashFile(paths.RuntimeChecklistPath);
        string guideSha256 = HashFile(paths.LocationGuidePath);
        string helperSha256 = HashFile(paths.FinderHelperPath);
        UnusedLevel65DragonSaveTriggerReceipt expectedReceipt = BuildReceipt(
            baseImage,
            baseCue,
            paths,
            readback,
            expectedPlan,
            rebuiltRawSectorCount: 5,
            outputCueSha256,
            planSha256,
            checklistSha256,
            guideSha256,
            helperSha256);
        string expectedReceiptText = JsonSerializer.Serialize(expectedReceipt, JsonOptions) + "\n";
        VerifyTextReadback(
            paths.StaticReadbackReceiptPath,
            expectedReceiptText,
            "published canonical dragon save-trigger receipt");
        string canonicalReceiptSha256 = Hash(Encoding.UTF8.GetBytes(expectedReceiptText));
        if (IsFrozenLocalPublication(paths))
        {
            RequireOptionalHash(
                canonicalReceiptSha256,
                ExpectedFinalReceiptSha256,
                "frozen canonical dragon save-trigger receipt");
        }

        return new(
            paths,
            expectedPlan,
            expectedReceipt,
            new(
                canonicalReceiptSha256,
                outputCueSha256,
                planSha256,
                checklistSha256,
                guideSha256,
                helperSha256));
    }

    private static async Task<UnusedLevel65DragonSaveTriggerPlan> BuildStaticPlanAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        CancellationToken cancellationToken)
    {
        _ = workspaceRoot;
        await RequireFileHashAsync(baseCue, BaseCueSha256, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        RequireMode2(layout, "locked display-name base");
        using FileStream image = File.OpenRead(baseImage);
        _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord executableRecord = RequireRootFile(
            image, layout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        byte[] executable = DiscImage.ReadFileBytes(
            image, layout, executableRecord.Lba, 0, executableRecord.Size);
        RequireHash(executable, SourceExecutableSha256, "locked executable");
        if (executable[DragonTargetSlotLogicalOffset] != DragonTargetBefore)
            throw new InvalidDataException("Locked slot-35 dragon target is no longer zero.");
        byte[] outputExecutable = executable.ToArray();
        outputExecutable[DragonTargetSlotLogicalOffset] = DragonTargetAfter;
        RequireOptionalHash(Hash(outputExecutable), OutputExecutableSha256, "planned executable");
        if (!DifferentByteIndexes(executable, outputExecutable)
                .SequenceEqual(new[] { DragonTargetSlotLogicalOffset }))
        {
            throw new InvalidDataException("The executable proposal escaped slot-35 dragon target 0x5FC37.");
        }

        byte[] sourceData = DiscImage.ReadFileBytes(
            image, layout, WadLba, DataWadOffset, DataByteLength);
        RequireHash(sourceData, SourceDataSha256, "locked ID65 data");
        byte[] outputData = sourceData.ToArray();
        ApplyDataTransaction(outputData);
        RequireOptionalHash(Hash(outputData), OutputDataSha256, "planned dragon/save-trigger ID65 data");
        int[] changedOffsets = DifferentByteIndexes(sourceData, outputData);
        ValidateDataTransaction(sourceData, outputData, changedOffsets);

        UnusedLevel65DragonSaveTriggerRowPatch t100 = BuildRowPatch(
            sourceData, outputData, T100, "dragon pedestal", T100XyzWadOffset);
        UnusedLevel65DragonSaveTriggerRowPatch t101 = BuildRowPatch(
            sourceData, outputData, T101, "dragon actor", T101XyzWadOffset);
        UnusedLevel65DragonSaveTriggerRowPatch t105 = BuildRowPatch(
            sourceData, outputData, T105, "dragon rescue/save-trigger control", T105XyzWadOffset);
        if (t100.BeforeRowSha256 != T100RowSha256 ||
            t101.BeforeRowSha256 != T101RowSha256 ||
            t105.BeforeRowSha256 != T105RowSha256)
        {
            throw new InvalidDataException("The locked T100/T101/T105 row preimages changed.");
        }

        int leadOffset = checked((int)(T101CameraLeadWadOffset - DataWadOffset));
        byte[] beforeLead = sourceData.AsSpan(leadOffset, CameraLeadSemanticByteLength).ToArray();
        byte[] afterLead = outputData.AsSpan(leadOffset, CameraLeadSemanticByteLength).ToArray();
        RequireHash(beforeLead, CameraLeadBeforeSha256, "locked T101 camera lead");
        RequireHash(afterLead, CameraLeadAfterSha256, "translated T101 camera lead");
        UnusedLevel65DragonSaveTriggerCameraPatch cameraLead = new(
            "T101 rescue-camera lead XYZ",
            T101CameraLeadWadOffset,
            1,
            CameraLeadSemanticByteLength,
            CameraFrameXyzByteLength,
            CameraLeadBeforeSha256,
            CameraLeadAfterSha256,
            DifferentByteIndexes(beforeLead, afterLead).Length,
            EveryXyzTranslated: true,
            NonXyzBytesPreserved: true);

        int framesOffset = checked((int)(T101CameraFramesWadOffset - DataWadOffset));
        byte[] beforeFrames = sourceData.AsSpan(framesOffset, CameraFramesByteLength).ToArray();
        byte[] afterFrames = outputData.AsSpan(framesOffset, CameraFramesByteLength).ToArray();
        RequireHash(beforeFrames, CameraFramesBeforeSha256, "locked T101 112-frame camera track");
        RequireHash(afterFrames, CameraFramesAfterSha256, "translated T101 112-frame camera track");
        UnusedLevel65DragonSaveTriggerCameraPatch cameraFrames = new(
            "T101 packed rescue-camera frame track",
            T101CameraFramesWadOffset,
            CameraFrameCount,
            CameraFrameStride,
            CameraFrameXyzByteLength,
            CameraFramesBeforeSha256,
            CameraFramesAfterSha256,
            DifferentByteIndexes(beforeFrames, afterFrames).Length,
            EveryXyzTranslated: true,
            NonXyzBytesPreserved: true);

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
        return new(
            PlanSchemaVersion,
            ProfileId,
            BaseImageSha256,
            BaseCueSha256,
            SourceDataSha256,
            Hash(outputData),
            SourceExecutableSha256,
            Hash(outputExecutable),
            Id65OverlaySha256,
            RetailTownSquareOverlaySha256,
            RetailTownSquareDataSha256,
            [t100, t101, t105],
            cameraLead,
            cameraFrames,
            TranslationRawX,
            TranslationRawY,
            TranslationRawZ,
            DragonTargetSlotLogicalOffset,
            DragonTargetBefore,
            DragonTargetAfter,
            changedOffsets,
            LogicalPatchWindowBytes,
            ChangedLogicalWadBytes,
            ChangedLogicalExecutableBytes,
            ChangedLogicalBytes,
            AffectedRawSectorLbas,
            RawSectorDiffSha256: ExpectedRawSectorDiffSha256,
            loadCodes,
            ExactT100T101T105AtomicRelocationVerified: true,
            ExactCameraLeadTranslationVerified: true,
            Exact112FrameCameraTrackTranslationVerified: true,
            AnglesLinksActorsPropertiesAndFixupsPreserved: true,
            OnlyDragonTargetExecutableByteChanged: true,
            UnprunedLockedBasePreserved: true,
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

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths finalPaths,
        string stageDirectory,
        UnusedLevel65DragonSaveTriggerPlan staticPlan,
        CancellationToken cancellationToken)
    {
        _ = workspaceRoot;
        string stagedImage = StagedPath(stageDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stageDirectory, finalPaths.OutputCuePath);
        string stagedMetadata = StagedPath(stageDirectory, finalPaths.ConstructionPlanPath);
        string stagedReceipt = StagedPath(stageDirectory, finalPaths.StaticReadbackReceiptPath);
        string stagedChecklist = StagedPath(stageDirectory, finalPaths.RuntimeChecklistPath);
        string stagedGuide = StagedPath(stageDirectory, finalPaths.LocationGuidePath);

        await DiscImageWorkingCopy.StageAsync(
            baseImage,
            stagedImage,
            consumeDisposableSource: false,
            cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(stagedImage);
        RequireMode2(layout, "staged dragon save-trigger image");
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
            DiscFileRecord executableRecord = RequireRootFile(
                image, layout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
            byte[] sourceData = DiscImage.ReadFileBytes(
                image, layout, WadLba, DataWadOffset, DataByteLength);
            RequireHash(sourceData, SourceDataSha256, "staged ID65 preimage");
            byte[] outputData = sourceData.ToArray();
            ApplyDataTransaction(outputData);
            ValidateDataTransaction(sourceData, outputData, DifferentByteIndexes(sourceData, outputData));
            RequireOptionalHash(Hash(outputData), OutputDataSha256, "staged output ID65 data");

            foreach (long xyzWadOffset in new[] { T100XyzWadOffset, T101XyzWadOffset, T105XyzWadOffset })
            {
                int relativeOffset = checked((int)(xyzWadOffset - DataWadOffset));
                DiscImage.WriteFileBytes(
                    image,
                    layout,
                    WadLba,
                    xyzWadOffset,
                    outputData.AsSpan(relativeOffset, CameraFrameXyzByteLength).ToArray());
            }
            int leadRelativeOffset = checked((int)(T101CameraLeadWadOffset - DataWadOffset));
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                T101CameraLeadWadOffset,
                outputData.AsSpan(leadRelativeOffset, CameraFrameXyzByteLength).ToArray());
            int framesRelativeOffset = checked((int)(T101CameraFramesWadOffset - DataWadOffset));
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                T101CameraFramesWadOffset,
                outputData.AsSpan(framesRelativeOffset, CameraFramesByteLength).ToArray());

            byte[] sourceExecutable = DiscImage.ReadFileBytes(
                image, layout, executableRecord.Lba, 0, executableRecord.Size);
            RequireHash(sourceExecutable, SourceExecutableSha256, "staged executable preimage");
            if (sourceExecutable[DragonTargetSlotLogicalOffset] != DragonTargetBefore)
                throw new InvalidDataException("Staged slot-35 dragon target is no longer zero.");
            DiscImage.WriteFileBytes(
                image,
                layout,
                executableRecord.Lba,
                DragonTargetSlotLogicalOffset,
                [DragonTargetAfter]);

            int rebuiltWadSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                WadLba,
                [
                    (T100XyzWadOffset, CameraFrameXyzByteLength),
                    (T101XyzWadOffset, CameraFrameXyzByteLength),
                    (T105XyzWadOffset, CameraFrameXyzByteLength),
                    (T101CameraLeadWadOffset, CameraFrameXyzByteLength),
                    (T101CameraFramesWadOffset, CameraFramesByteLength)
                ]);
            int rebuiltExecutableSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                executableRecord.Lba,
                [(DragonTargetSlotLogicalOffset, 1)]);
            rebuiltRawSectorCount = rebuiltWadSectors + rebuiltExecutableSectors;
            int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                AffectedRawSectorLbas.Select(lba => (lba, 1)));
            for (int index = 0; index < AffectedRawSectorLbas.Length; index++)
            {
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                    image,
                    layout,
                    AffectedRawSectorLbas[index],
                    AffectedRawSectorSubmodes[index]);
            }
            if (rebuiltWadSectors != 4 || rebuiltExecutableSectors != 1 ||
                rebuiltRawSectorCount != 5 || verifiedRawSectors != 5)
            {
                throw new InvalidDataException(
                    $"The dragon/save-trigger patch rebuilt/verified {rebuiltWadSectors}+{rebuiltExecutableSectors}/{verifiedRawSectors} sectors, expected 4+1/5.");
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
            cancellationToken);
        RequireOptionalHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "dragon save-trigger output BIN");
        RequireOptionalCount(
            readback.ChangedPhysicalImageBytes,
            ExpectedChangedPhysicalImageBytes,
            "dragon save-trigger physical changed-byte count");
        RequireOptionalHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "dragon save-trigger raw diff");

        UnusedLevel65DragonSaveTriggerPlan plan = staticPlan with
        {
            RawSectorDiffSha256 = readback.RawSectorDiffSha256
        };
        string metadata = BuildCanonicalConstructionPlan(plan);
        VerifyCanonicalConstructionPlan(metadata, plan);
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
        if ((ExpectedConstructionPlanSha256 != "PENDING" &&
             metadataSha256 != ExpectedConstructionPlanSha256) ||
            (ExpectedLocationGuideSha256 != "PENDING" &&
             guideSha256 != ExpectedLocationGuideSha256))
        {
            throw new InvalidDataException("The pinned dragon save-trigger metadata or 1200x1000 guide changed.");
        }
        UnusedLevel65DragonSaveTriggerReceipt receipt = BuildReceipt(
            baseImage,
            baseCue,
            finalPaths,
            readback,
            plan,
            rebuiltRawSectorCount,
            outputCueSha256,
            metadataSha256,
            checklistSha256,
            guideSha256,
            helperSha256);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);

        VerifyExactStagedFiles(stageDirectory, finalPaths);
        VerifyTextReadback(
            stagedReceipt,
            JsonSerializer.Serialize(receipt, JsonOptions) + "\n",
            "dragon save-trigger receipt");
        VerifyChecklist(checklist, finalPaths, plan.LoadCodes, finalReveal);
        VerifyLocationGuide(guide, plan.LoadCodes);
        return new(plan, receipt, readback);
    }

    private static UnusedLevel65DragonSaveTriggerReceipt BuildReceipt(
        string baseImage,
        string baseCue,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths,
        CandidateReadback readback,
        UnusedLevel65DragonSaveTriggerPlan plan,
        int rebuiltRawSectorCount,
        string outputCueSha256,
        string constructionPlanSha256,
        string runtimeChecklistSha256,
        string locationGuideSha256,
        string finderHelperSha256) =>
        new(
            ReceiptSchemaVersion,
            ProfileId,
            baseImage,
            baseCue,
            paths.OutputDirectoryPath,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.ConstructionPlanPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath,
            readback.OutputImageSha256,
            outputCueSha256,
            constructionPlanSha256,
            runtimeChecklistSha256,
            locationGuideSha256,
            finderHelperSha256,
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
            CanonicalConstructionPlanVerified: true,
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

    private static async Task<CandidateReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        RequireMode2(baseLayout, "dragon save-trigger base readback");
        RequireMode2(outputLayout, "dragon save-trigger output readback");
        if (baseLayout != outputLayout)
            throw new InvalidDataException("The dragon save-trigger output changed the exact disc layout.");

        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The dragon save-trigger output changed the disc-image length.");
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
        RequireHash(beforeExecutable, SourceExecutableSha256, "base executable readback");
        RequireOptionalHash(Hash(afterExecutable), OutputExecutableSha256, "output executable readback");
        int[] executableDiff = DifferentByteIndexes(beforeExecutable, afterExecutable);
        if (!executableDiff.SequenceEqual(new[] { DragonTargetSlotLogicalOffset }) ||
            beforeExecutable[DragonTargetSlotLogicalOffset] != DragonTargetBefore ||
            afterExecutable[DragonTargetSlotLogicalOffset] != DragonTargetAfter)
        {
            throw new InvalidDataException(
                "The output executable diff escaped slot-35 dragon target 0x5FC37 or is not 0->4.");
        }

        byte[] beforeData = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, DataWadOffset, DataByteLength);
        byte[] afterData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, DataWadOffset, DataByteLength);
        RequireHash(beforeData, SourceDataSha256, "base ID65 data readback");
        RequireOptionalHash(Hash(afterData), OutputDataSha256, "output ID65 data readback");
        int[] changedLogical = DifferentByteIndexes(beforeData, afterData);
        ValidateDataTransaction(beforeData, afterData, changedLogical);

        RequirePreservedWadRange(
            baseline, output, baseLayout, Id65OverlayWadOffset, Id65OverlayByteLength,
            Id65OverlaySha256, "ID65 overlay");
        RequirePreservedWadRange(
            baseline, output, baseLayout, RetailTownSquareOverlayWadOffset,
            RetailTownSquareOverlayByteLength, RetailTownSquareOverlaySha256,
            "retail Town Square overlay");
        RequirePreservedWadRange(
            baseline, output, baseLayout, RetailTownSquareDataWadOffset,
            RetailTownSquareDataByteLength, RetailTownSquareDataSha256,
            "retail Town Square data");
        IReadOnlyList<UnusedLevel65DragonSaveTriggerRawSectorDiff> rawDiffs =
            await CompareRawImagesAsync(baseline, output, cancellationToken);
        if (!rawDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(AffectedRawSectorLbas) ||
            rawDiffs.Any(diff =>
                diff.HeaderChangedBytes != 0 ||
                diff.SubheaderChangedBytes != 0 ||
                diff.PayloadChangedBytes <= 0) ||
            rawDiffs.Sum(diff => diff.PayloadChangedBytes) != ChangedLogicalBytes)
        {
            throw new InvalidDataException(
                "The dragon/save-trigger physical diff escaped LBAs 54838/54845/55380/55381/55573 or 350 payload bytes: " +
                string.Join(",", rawDiffs.Select(diff =>
                    $"{diff.RawSectorLba}:payload={diff.PayloadChangedBytes}:total={diff.TotalChangedBytes}:" +
                    $"header={diff.HeaderChangedBytes}:subheader={diff.SubheaderChangedBytes}:reserved={diff.ReservedChangedBytes}")));
        }
        int physicalChanged = rawDiffs.Sum(diff => diff.TotalChangedBytes);
        string rawDiffHash = HashRawSectorDiffs(rawDiffs);
        string outputImageSha256 = await HashFileAsync(outputImagePath, cancellationToken);
        RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            AffectedRawSectorLbas.Select(lba => (lba, 1)));
        for (int index = 0; index < AffectedRawSectorLbas.Length; index++)
        {
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                output,
                outputLayout,
                AffectedRawSectorLbas[index],
                AffectedRawSectorSubmodes[index]);
        }
        await RequireFileHashAsync(baseImagePath, BaseImageSha256, cancellationToken);

        return new(
            outputImageSha256,
            Hash(afterData),
            Hash(afterExecutable),
            changedLogical.Length,
            executableDiff.Length,
            changedLogical,
            physicalChanged,
            rawDiffs,
            rawDiffHash,
            DragonTargetPatchVerified: true,
            DragonSceneAndCameraTranslationVerified: true,
            RetailTownSquarePreserved: true,
            Mode2IntegrityVerified: true);
    }

    private static string BuildCanonicalConstructionPlan(UnusedLevel65DragonSaveTriggerPlan plan) =>
        JsonSerializer.Serialize(plan, JsonOptions) + "\n";

    private static void VerifyCanonicalConstructionPlan(
        string text,
        UnusedLevel65DragonSaveTriggerPlan expected)
    {
        UnusedLevel65DragonSaveTriggerPlan document =
            JsonSerializer.Deserialize<UnusedLevel65DragonSaveTriggerPlan>(text, JsonOptions)
            ?? throw new InvalidDataException("The candidate construction plan is empty.");
        string canonical = JsonSerializer.Serialize(document, JsonOptions) + "\n";
        string expectedText = JsonSerializer.Serialize(expected, JsonOptions) + "\n";
        if (!string.Equals(text, canonical, StringComparison.Ordinal) ||
            !string.Equals(text, expectedText, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The construction plan is not canonical or does not match the verified transaction.");
        }
        ValidatePlan(document);
    }

    private static string BuildRuntimeChecklist(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal finderReveal,
        UnusedLevel65DragonSaveTriggerPlan plan,
        CandidateReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Dragon / Save-Trigger No-Card Gate — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This candidate starts directly from the locked, unpruned display-name BIN. It translates only T100/T101/T105, T101's camera lead, and XYZ in all 112 camera frames by raw (+256,+256,-40), then changes only SCUS slot 35 dragon target 0→4.");
        builder.AppendLine("Angles, links, actors, properties, fixups, all non-XYZ camera bytes, terrain, and retail levels are preserved.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        builder.AppendLine("## Immutable setup");
        builder.AppendLine();
        builder.AppendLine("- DuckStation Memory Card 1: **None**.");
        builder.AppendLine("- DuckStation Memory Card 2: **None**.");
        builder.AppendLine("- Disable cheats and do not resume a save state.");
        builder.AppendLine("- **Do not save.** Never choose **Save** or **Format**; do not insert, create, or write a memory card.");
        builder.AppendLine("- Load the **CUE**, not the BIN.");
        builder.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`.");
        builder.AppendLine($"- Canonical construction plan: `{ConstructionPlanFileName}`.");
        builder.AppendLine("- This is not App integration, normal Create BIN, a release, or permission to save.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, plan.LoadCodes);
        builder.AppendLine("## Gate A — rescue and translated camera track");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold boot, enter the complete ID65 candidate code, and wait for controllable gameplay with 4 lives.");
        builder.AppendLine("- [ ] Open Inventory before rescue: dragons must read **0/4**.");
        builder.AppendLine("- [ ] Locate the translated T100 pedestal, T101 dragon, and T105 save-trigger/control together; nothing may remain at the old coordinates.");
        builder.AppendLine("- [ ] Rescue T101. The full cinematic must use the translated camera lead and translated **112-frame camera track**, without a snap to the old scene, bad angle, hang, crash, or displaced actor.");
        builder.AppendLine("- [ ] After control returns, Inventory must read **1/4**. The rescued dragon remains completed for this session.");
        builder.AppendLine();
        builder.AppendLine("## Gate B — pedestal prompt cancel/back");
        builder.AppendLine();
        builder.AppendLine("- [ ] Re-approach the rescued pedestal until its prompt/menu appears.");
        builder.AppendLine("- [ ] Cancel/back out. Control, camera, pedestal state, 1/4 count, and nearby objects must remain normal.");
        builder.AppendLine("- [ ] If a save choice appears, cancel/back only. Never choose Save or Format, and fail if a card-write path is required.");
        builder.AppendLine();
        builder.AppendLine("## Gate C — same-session re-entry and cold reset");
        builder.AppendLine();
        builder.AppendLine("- [ ] Leave ID65 and re-enter in the same session. Inventory must still read **1/4** and the rescued pedestal must remain completed.");
        builder.AppendLine("- [ ] Cold reset DuckStation with both cards still None, reload the CUE without a save state, and re-enter ID65.");
        builder.AppendLine("- [ ] After cold reset, Inventory must return to **0/4** and T101 must be rescuable again; this proves no-card session state resets cleanly.");
        builder.AppendLine("- [ ] Repeat the rescue once after cold reset and confirm the translated 112-frame track and **0/4→1/4** transition again.");
        builder.AppendLine();
        builder.AppendLine("## Retail isolation controls");
        builder.AppendLine();
        builder.AppendLine("- [ ] Cold-load Retail Town Square and rescue one native dragon; camera, pedestal prompt cancel/back, counts, and ordinary play must remain retail-normal.");
        builder.AppendLine("- [ ] Cold-load Gnasty's World, Gnasty's Loot, and Sunny Flight using their complete control codes; confirm normal control and no changed totals, actors, camera, terrain, or exits.");
        builder.AppendLine();
        builder.AppendLine("## Decision boundary");
        builder.AppendLine();
        builder.AppendLine("PASS requires rescue, the complete translated cinematic, 0/4→1/4, pedestal prompt cancel/back, same-session 1/4 persistence, cold-reset 0/4 restoration, repeat rescue, and every retail control.");
        builder.AppendLine("Any camera snap, old-position actor, bad angle, crash, hang, wrong count, failed cancel/back, card-write requirement, retail change, or re-entry/reset failure leaves dragon/save-trigger ownership runtime-pending and unpromoted.");
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
            "  <text x=\"60\" y=\"82\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"28\" font-weight=\"700\">ID65 Dragon / Save-Trigger — No-Card Gate</text>\n" +
            "  <text x=\"60\" y=\"111\" fill=\"#b9cbed\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\">Locked unpruned base • T100/T101/T105 + camera • runtime pending</text>\n" +
            "  <rect x=\"60\" y=\"142\" width=\"620\" height=\"390\" rx=\"18\" fill=\"#0a192b\" stroke=\"#355b86\"/>\n" +
            "  <text x=\"84\" y=\"176\" fill=\"#dfeaff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Exact transaction</text>\n" +
            "  <text x=\"90\" y=\"220\" fill=\"#c7f4d1\" font-family=\"Menlo,monospace\" font-size=\"14\">T100 pedestal XYZ</text>\n" +
            "  <text x=\"90\" y=\"252\" fill=\"#c7f4d1\" font-family=\"Menlo,monospace\" font-size=\"14\">T101 dragon XYZ + camera lead</text>\n" +
            "  <text x=\"90\" y=\"284\" fill=\"#c7f4d1\" font-family=\"Menlo,monospace\" font-size=\"14\">T105 rescue/save-trigger XYZ</text>\n" +
            "  <text x=\"90\" y=\"326\" fill=\"#ffcf70\" font-family=\"Menlo,monospace\" font-size=\"15\">raw delta (+256,+256,-40)</text>\n" +
            "  <text x=\"90\" y=\"365\" fill=\"#d9efff\" font-family=\"Menlo,monospace\" font-size=\"14\">112 frames • XYZ only • 24-byte stride</text>\n" +
            "  <text x=\"90\" y=\"405\" fill=\"#d9efff\" font-family=\"Menlo,monospace\" font-size=\"14\">SCUS 0x5FC37: dragon target 0 → 4</text>\n" +
            "  <text x=\"90\" y=\"454\" fill=\"#ffb7b7\" font-family=\"Menlo,monospace\" font-size=\"14\">350 logical bytes • five raw sectors</text>\n" +
            "  <text x=\"90\" y=\"490\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">angles • links • actors • properties • fixups preserved</text>\n" +
            "  <rect x=\"705\" y=\"142\" width=\"435\" height=\"390\" rx=\"18\" fill=\"#15192b\" stroke=\"#6e6ba5\"/>\n" +
            "  <text x=\"730\" y=\"178\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"19\" font-weight=\"700\">Runtime gates</text>\n" +
            "  <text x=\"735\" y=\"220\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">A • Rescue + translated camera</text>\n" +
            "  <text x=\"755\" y=\"248\" fill=\"#d9e3f7\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"14\">Inventory 0/4 → 1/4</text>\n" +
            "  <text x=\"735\" y=\"292\" fill=\"#c3baff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">B • Pedestal prompt cancel/back</text>\n" +
            "  <text x=\"735\" y=\"340\" fill=\"#8be0c1\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">C • Same session + cold reset</text>\n" +
            "  <text x=\"730\" y=\"390\" fill=\"#ff9d9d\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Cards 1/2: None • No Save or Format</text>\n" +
            "  <text x=\"730\" y=\"430\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">Same-session: 1/4 • cold reset: 0/4</text>\n" +
            "  <text x=\"730\" y=\"462\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">Retail controls unchanged</text>\n" +
            "  <text x=\"730\" y=\"494\" fill=\"#afc2e5\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"13\">App / Create BIN / promotion disabled</text>\n" +
            "  <text x=\"60\" y=\"580\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\" font-weight=\"700\">Complete cold-load codes</text>\n" +
            string.Join("\n", codeRows.Select((row, index) =>
                $"  <text x=\"80\" y=\"{610 + index * 27}\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"11\">{row}</text>")) + "\n" +
            "  <rect x=\"60\" y=\"760\" width=\"1080\" height=\"155\" rx=\"14\" fill=\"#09182a\" stroke=\"#355b86\"/>\n" +
            "  <text x=\"82\" y=\"794\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">Pinned candidate identity</text>\n" +
            $"  <text x=\"82\" y=\"824\" fill=\"#b9cbed\" font-family=\"Menlo,monospace\" font-size=\"11\">Profile: {XmlEscape(ProfileId)}</text>\n" +
            $"  <text x=\"82\" y=\"850\" fill=\"#b9cbed\" font-family=\"Menlo,monospace\" font-size=\"11\">CUE: {XmlEscape(OutputPrefix + ".cue")}</text>\n" +
            $"  <text x=\"82\" y=\"876\" fill=\"#ffb7b7\" font-family=\"Menlo,monospace\" font-size=\"11\">BIN SHA-256: {XmlEscape(outputImageSha256)}</text>\n" +
            "  <text x=\"60\" y=\"945\" fill=\"#aebfe1\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"12\">Report rescue, 112-frame camera, cancel/back, same-session re-entry, cold reset, and retail controls. Runtime pending • unpromoted.</text>\n" +
            "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        RuntimeCandidateFinderReveal finderReveal)
    {
        string[] required =
        [
            "T100/T101/T105",
            "112 camera frames",
            "slot 35 dragon target 0→4",
            "Memory Card 1: **None**",
            "Memory Card 2: **None**",
            "**Do not save.**",
            "Never choose **Save** or **Format**",
            "load the **CUE**, not the BIN",
            "Gate A — rescue and translated camera track",
            "Inventory before rescue: dragons must read **0/4**",
            "Inventory must read **1/4**",
            "Gate B — pedestal prompt cancel/back",
            "Gate C — same-session re-entry and cold reset",
            "same session",
            "cold reset",
            "Retail isolation controls",
            ConstructionPlanFileName,
            Path.GetFileName(paths.OutputCuePath),
            "Normal Create BIN, app integration, release packaging, and promotion remain disabled"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The dragon save-trigger runtime checklist omitted an exact required boundary.");
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
            "ID65 Dragon / Save-Trigger",
            "T100 pedestal XYZ",
            "T101 dragon XYZ + camera lead",
            "T105 rescue/save-trigger XYZ",
            "raw delta (+256,+256,-40)",
            "112 frames • XYZ only • 24-byte stride",
            "SCUS 0x5FC37: dragon target 0 → 4",
            "350 logical bytes • five raw sectors",
            "A • Rescue + translated camera",
            "Inventory 0/4 → 1/4",
            "B • Pedestal prompt cancel/back",
            "C • Same session + cold reset",
            "Cards 1/2: None • No Save or Format",
            "Retail controls unchanged",
            ProfileId,
            OutputPrefix + ".cue",
            "Runtime pending • unpromoted"
        ];
        if (required.Any(value => !svg.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The dragon save-trigger 1200x1000 guide omitted an exact required gate.");
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
            throw new InvalidDataException("The dragon save-trigger comparison load codes are incomplete.");
        return codes;
    }

    private static UnusedLevel65DragonSaveTriggerRowPatch BuildRowPatch(
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
        string expectedBefore = trueIndex switch
        {
            T100 => T100RowSha256,
            T101 => T101RowSha256,
            T105 => T105RowSha256,
            _ => throw new InvalidDataException($"T{trueIndex} is outside the owned dragon scene.")
        };
        string expectedAfter = trueIndex switch
        {
            T100 => T100OutputRowSha256,
            T101 => T101OutputRowSha256,
            T105 => T105OutputRowSha256,
            _ => throw new InvalidDataException($"T{trueIndex} is outside the owned dragon scene.")
        };
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

    private static async Task<IReadOnlyList<UnusedLevel65DragonSaveTriggerRawSectorDiff>>
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
                throw new InvalidDataException("The dragon save-trigger image comparison encountered mismatched reads.");
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
                    < 2248 => 5,
                    _ => 6
                };
                values[category]++;
                values[7]++;
            }
            absolute += beforeRead;
        }
        return counts.OrderBy(pair => pair.Key)
            .Select(pair => new UnusedLevel65DragonSaveTriggerRawSectorDiff(
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
        IReadOnlyList<UnusedLevel65DragonSaveTriggerRawSectorDiff> diffs)
    {
        string canonical = string.Concat(diffs.Select(diff =>
            $"{diff.RawSectorLba}|{diff.HeaderChangedBytes}|{diff.SubheaderChangedBytes}|" +
            $"{diff.PayloadChangedBytes}|{diff.EdcChangedBytes}|{diff.ReservedChangedBytes}|" +
            $"{diff.EccPChangedBytes}|{diff.EccQChangedBytes}|{diff.TotalChangedBytes}\n"));
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }

    private static void ValidateReadbackAgainstReceipt(
        CandidateReadback readback,
        UnusedLevel65DragonSaveTriggerReceipt receipt)
    {
        if (readback.OutputImageSha256 != receipt.OutputImageSha256 ||
            readback.OutputDataSha256 != receipt.OutputDataSha256 ||
            readback.OutputDataSha256 != receipt.Plan.OutputDataSha256 ||
            readback.OutputExecutableSha256 != receipt.Plan.OutputExecutableSha256 ||
            readback.ChangedLogicalWadBytes != ChangedLogicalWadBytes ||
            readback.ChangedLogicalExecutableBytes != ChangedLogicalExecutableBytes ||
            !readback.ChangedDataByteOffsets.SequenceEqual(receipt.Plan.ChangedDataByteOffsets) ||
            readback.ChangedPhysicalImageBytes != receipt.ChangedPhysicalImageBytes ||
            !readback.RawSectorDiffs.SequenceEqual(receipt.RawSectorDiffs) ||
            readback.RawSectorDiffSha256 != receipt.RawSectorDiffSha256 ||
            readback.RawSectorDiffs.Sum(diff => diff.PayloadChangedBytes) != ChangedLogicalBytes ||
            readback.RawSectorDiffs.Any(diff =>
                diff.HeaderChangedBytes != 0 || diff.SubheaderChangedBytes != 0 ||
                diff.ReservedChangedBytes != 0) ||
            !readback.RawSectorDiffs.Select(diff =>
                    (diff.EccPChangedBytes, diff.EccQChangedBytes))
                .SequenceEqual(new[] { (26, 48), (14, 34), (128, 103), (172, 104), (10, 20) }) ||
            !readback.DragonTargetPatchVerified ||
            !readback.DragonSceneAndCameraTranslationVerified ||
            !readback.RetailTownSquarePreserved ||
            !readback.Mode2IntegrityVerified)
        {
            throw new InvalidDataException("Published dragon save-trigger image readback differs from its receipt.");
        }
        RequireOptionalHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "published dragon save-trigger BIN");
        RequireOptionalCount(
            readback.ChangedPhysicalImageBytes,
            ExpectedChangedPhysicalImageBytes,
            "published dragon save-trigger physical diff");
        RequireOptionalHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "published dragon save-trigger raw diff");
    }

    private static void ValidateReceiptIdentity(
        UnusedLevel65DragonSaveTriggerReceipt receipt,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        UnusedLevel65DragonSaveTriggerPlan plan = receipt.Plan;
        bool exactPaths =
            PathEquals(receipt.OutputDirectoryPath, paths.OutputDirectoryPath) &&
            PathEquals(receipt.OutputImagePath, paths.OutputImagePath) &&
            PathEquals(receipt.OutputCuePath, paths.OutputCuePath) &&
            PathEquals(receipt.ConstructionPlanPath, paths.ConstructionPlanPath) &&
            PathEquals(receipt.StaticReadbackReceiptPath, paths.StaticReadbackReceiptPath) &&
            PathEquals(receipt.RuntimeChecklistPath, paths.RuntimeChecklistPath) &&
            PathEquals(receipt.LocationGuidePath, paths.LocationGuidePath) &&
            PathEquals(receipt.FinderHelperPath, paths.FinderHelperPath);
        bool exactPins = receipt.SchemaVersion == ReceiptSchemaVersion &&
            receipt.ProfileId == ProfileId &&
            receipt.BaseImageSha256 == BaseImageSha256 &&
            receipt.BaseCueSha256 == BaseCueSha256 &&
            receipt.OutputDataSha256 == plan.OutputDataSha256 &&
            IsSha256(receipt.OutputImageSha256) && IsSha256(receipt.OutputCueSha256) &&
            IsSha256(receipt.ConstructionPlanSha256) && IsSha256(receipt.RuntimeChecklistSha256) &&
            IsSha256(receipt.LocationGuideSha256) && IsSha256(receipt.FinderHelperSha256) &&
            receipt.RebuiltRawSectorCount == 5 && receipt.ChangedRawSectorCount == 5 &&
            receipt.RawSectorDiffs.Count == 5 &&
            receipt.RawSectorDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(AffectedRawSectorLbas) &&
            receipt.RawSectorDiffSha256 == HashRawSectorDiffs(receipt.RawSectorDiffs);
        exactPins = exactPins &&
            (ExpectedOutputImageSha256 == "PENDING" ||
             receipt.OutputImageSha256 == ExpectedOutputImageSha256) &&
            (ExpectedChangedPhysicalImageBytes < 0 ||
             receipt.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes) &&
            (ExpectedRawSectorDiffSha256 == "PENDING" ||
             receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256) &&
            (ExpectedConstructionPlanSha256 == "PENDING" ||
             receipt.ConstructionPlanSha256 == ExpectedConstructionPlanSha256) &&
            (ExpectedLocationGuideSha256 == "PENDING" ||
             receipt.LocationGuideSha256 == ExpectedLocationGuideSha256);
        bool exactPlan;
        try
        {
            ValidatePlan(plan);
            exactPlan = plan.RawSectorDiffSha256 == receipt.RawSectorDiffSha256;
        }
        catch (InvalidDataException)
        {
            exactPlan = false;
        }
        bool failClosed = receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified && receipt.CanonicalConstructionPlanVerified &&
            receipt.CanonicalReceiptVerified && receipt.Mode2IntegrityVerified &&
            receipt.BaseCandidatePreserved && receipt.FullDirectoryPublicationVerified &&
            !receipt.RollbackRecoveryVerified && !receipt.FullDirectoryRollbackVerified &&
            receipt.FinderHandoffVerified && !receipt.RuntimeVerified &&
            receipt.DisposableRuntimeCandidateAuthorized && !receipt.AppIntegrated &&
            !receipt.NormalCreateBinEnabled && !receipt.PromotionAuthorized && !receipt.ReleaseAuthorized;
        if (!exactPaths || !exactPins || !exactPlan || !failClosed)
            throw new InvalidDataException("The dragon save-trigger receipt lost an exact path, pin, or fail-closed flag.");
    }

    private static void ValidatePlan(UnusedLevel65DragonSaveTriggerPlan plan)
    {
        bool valid = plan.SchemaVersion == PlanSchemaVersion && plan.ProfileId == ProfileId &&
            plan.BaseImageSha256 == BaseImageSha256 && plan.BaseCueSha256 == BaseCueSha256 &&
            plan.SourceDataSha256 == SourceDataSha256 && IsSha256(plan.OutputDataSha256) &&
            plan.SourceExecutableSha256 == SourceExecutableSha256 &&
            IsSha256(plan.OutputExecutableSha256) &&
            plan.Id65OverlaySha256 == Id65OverlaySha256 &&
            plan.RetailTownSquareOverlaySha256 == RetailTownSquareOverlaySha256 &&
            plan.RetailTownSquareDataSha256 == RetailTownSquareDataSha256 &&
            plan.RowPatches.Select(row => row.TrueIndex).SequenceEqual(new[] { T100, T101, T105 }) &&
            plan.RowPatches.All(row => row.OnlyXyzChanged) &&
            plan.RowPatches[0].RowWadOffset == 0x6B08BD0 &&
            plan.RowPatches[0].XyzWadOffset == T100XyzWadOffset &&
            plan.RowPatches[0].BeforeRowSha256 == T100RowSha256 &&
            plan.RowPatches[0].AfterRowSha256 == T100OutputRowSha256 &&
            plan.RowPatches[1].RowWadOffset == 0x6B08C28 &&
            plan.RowPatches[1].XyzWadOffset == T101XyzWadOffset &&
            plan.RowPatches[1].BeforeRowSha256 == T101RowSha256 &&
            plan.RowPatches[1].AfterRowSha256 == T101OutputRowSha256 &&
            plan.RowPatches[2].RowWadOffset == 0x6B08D88 &&
            plan.RowPatches[2].XyzWadOffset == T105XyzWadOffset &&
            plan.RowPatches[2].BeforeRowSha256 == T105RowSha256 &&
            plan.RowPatches[2].AfterRowSha256 == T105OutputRowSha256 &&
            plan.CameraLeadPatch.WadOffset == T101CameraLeadWadOffset &&
            plan.CameraLeadPatch.FrameCount == 1 &&
            plan.CameraLeadPatch.FrameStride == CameraLeadSemanticByteLength &&
            plan.CameraLeadPatch.XyzByteLengthPerFrame == CameraFrameXyzByteLength &&
            plan.CameraLeadPatch.ChangedLogicalBytes == 3 &&
            plan.CameraLeadPatch.BeforeSha256 == CameraLeadBeforeSha256 &&
            plan.CameraLeadPatch.AfterSha256 == CameraLeadAfterSha256 &&
            plan.CameraLeadPatch.EveryXyzTranslated && plan.CameraLeadPatch.NonXyzBytesPreserved &&
            plan.CameraFrameTrackPatch.WadOffset == T101CameraFramesWadOffset &&
            plan.CameraFrameTrackPatch.FrameCount == CameraFrameCount &&
            plan.CameraFrameTrackPatch.FrameStride == CameraFrameStride &&
            plan.CameraFrameTrackPatch.ChangedLogicalBytes == 336 &&
            plan.CameraFrameTrackPatch.BeforeSha256 == CameraFramesBeforeSha256 &&
            plan.CameraFrameTrackPatch.AfterSha256 == CameraFramesAfterSha256 &&
            plan.CameraFrameTrackPatch.EveryXyzTranslated &&
            plan.CameraFrameTrackPatch.NonXyzBytesPreserved &&
            plan.TranslationRawX == TranslationRawX &&
            plan.TranslationRawY == TranslationRawY && plan.TranslationRawZ == TranslationRawZ &&
            plan.DragonTargetSlotLogicalOffset == DragonTargetSlotLogicalOffset &&
            plan.DragonTargetBefore == DragonTargetBefore &&
            plan.DragonTargetAfter == DragonTargetAfter &&
            plan.ChangedDataByteOffsets.Count == ChangedLogicalWadBytes &&
            plan.LogicalPatchWindowBytes == LogicalPatchWindowBytes &&
            plan.ChangedLogicalWadBytes == ChangedLogicalWadBytes &&
            plan.ChangedLogicalExecutableBytes == ChangedLogicalExecutableBytes &&
            plan.ChangedLogicalBytes == ChangedLogicalBytes &&
            plan.AffectedRawSectorLbas.SequenceEqual(AffectedRawSectorLbas) &&
            plan.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 60, 13, 64, 15 }) &&
            IsSha256(plan.RawSectorDiffSha256) &&
            plan.ExactT100T101T105AtomicRelocationVerified &&
            plan.ExactCameraLeadTranslationVerified &&
            plan.Exact112FrameCameraTrackTranslationVerified &&
            plan.AnglesLinksActorsPropertiesAndFixupsPreserved &&
            plan.OnlyDragonTargetExecutableByteChanged && plan.UnprunedLockedBasePreserved &&
            plan.RetailLevelsPreserved && !plan.MemoryCardsRequired && !plan.SavingAuthorized &&
            !plan.RuntimeVerified && plan.DisposableRuntimeCandidateAuthorized &&
            !plan.AppIntegrated && !plan.NormalCreateBinEnabled &&
            !plan.PromotionAuthorized && !plan.ReleaseAuthorized;
        if (!valid)
            throw new InvalidDataException("The exact dragon/save-trigger construction plan changed.");
        RequireOptionalHash(plan.OutputDataSha256, OutputDataSha256, "plan output ID65 data");
        RequireOptionalHash(plan.OutputExecutableSha256, OutputExecutableSha256, "plan output executable");
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static string BuildFinderHelperText(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        string cueName = Path.GetFileName(paths.OutputCuePath);
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

    private static bool IsFrozenLocalPublication(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        string? localDirectory = Path.GetDirectoryName(paths.PublicationParentPath);
        return string.Equals(
                   Path.GetFileName(paths.PublicationParentPath),
                   "v5-stone-hill-level-replacement",
                   StringComparison.Ordinal) &&
               localDirectory != null &&
               string.Equals(Path.GetFileName(localDirectory), "_local", StringComparison.Ordinal);
    }

    private static void VerifyExactStagedFiles(
        string stageDirectory,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths finalPaths)
    {
        string[] expected = ExpectedPublishedFileNames(finalPaths);
        string[] actual = Directory.EnumerateFileSystemEntries(stageDirectory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The staged dragon save-trigger candidate is not exactly seven files.");
    }

    private static void VerifyExactPublishedFiles(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        string[] expected = ExpectedPublishedFileNames(paths);
        string[] actual = Directory.EnumerateFileSystemEntries(paths.OutputDirectoryPath)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The published dragon save-trigger candidate is not exactly seven files.");
        foreach (string file in Directory.EnumerateFiles(paths.OutputDirectoryPath))
            RejectReparsePoint(file, "published dragon save-trigger artifact");
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
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths) =>
    [
        Path.GetFileName(paths.OutputImagePath),
        Path.GetFileName(paths.OutputCuePath),
        Path.GetFileName(paths.ConstructionPlanPath),
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
            RejectReparsePoint(entry, "dragon save-trigger writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The dragon save-trigger writer lease path is a directory.");
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
            throw new IOException("The dragon save-trigger writer lease file could not be opened.", ex);
        }

        try
        {
            RejectReparsePoint(path, "dragon save-trigger writer lease");
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
                    throw new IOException("The dragon save-trigger writer OS range lease could not be acquired.", ex);
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
                    $"The dragon save-trigger writer OS lease failed (errno {error}: {nativeError.Message}).",
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
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        if (!Directory.Exists(paths.OperationsDirectoryPath))
            return false;
        RejectReparsePoint(paths.OperationsDirectoryPath, "dragon save-trigger operations directory");
        bool recovered = false;
        foreach (string operationRoot in Directory.EnumerateDirectories(paths.OperationsDirectoryPath)
                     .Order(StringComparer.Ordinal))
        {
            if (!Path.GetFileName(operationRoot).StartsWith(OperationPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("The dragon save-trigger operations directory contains a foreign entry.");
            RejectReparsePoint(operationRoot, "dragon save-trigger operation");
            string journalPath = Path.Combine(operationRoot, JournalFileName);
            if (!File.Exists(journalPath))
                throw new InvalidDataException("A dragon save-trigger operation is missing its journal.");
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
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
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
                    "A committed dragon save-trigger operation is missing its candidate; recovery was preserved for audit.");
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
                    "An uncommitted dragon save-trigger publication lost its prior backup; the current output was preserved for audit.");
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
                        "Recovery cannot restore the prior dragon save-trigger candidate over an ambiguous existing directory.");
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
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        if (journal.Phase == PhaseCommitted)
            throw new InvalidOperationException("A committed dragon save-trigger publication cannot enter rollback.");
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
                    "Rollback lost the prior dragon save-trigger backup; the current output was preserved for audit.");
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
            throw new IOException("dragon save-trigger publication failed and rollback was incomplete.", new AggregateException(failures));
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
            ?? throw new InvalidDataException("A dragon save-trigger operation journal is empty.");
        if (text != JsonSerializer.Serialize(journal, JsonOptions) + "\n")
            throw new InvalidDataException("A dragon save-trigger operation journal is not canonical JSON.");
        return journal;
    }

    private static void ValidateJournal(
        OperationJournal journal,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths,
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
            throw new InvalidDataException("A dragon save-trigger operation journal escaped its exact owned paths.");
        }
    }

    internal static void SeedInterruptedRecoveryFixtureForSmoke(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
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
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths) =>
        RecoverOwnedOperations(paths);

    private static void RequireSafeRoles(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        if (!Path.IsPathFullyQualified(workspaceRoot) || !Path.IsPathFullyQualified(baseImage) ||
            !Path.IsPathFullyQualified(baseCue) || !Path.IsPathFullyQualified(paths.OutputDirectoryPath))
        {
            throw new InvalidOperationException("dragon save-trigger publication roles must use absolute paths.");
        }
        if (PathEquals(baseImage, baseCue) ||
            IsDescendantOrEqual(baseImage, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseImage) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseCue) ||
            IsDescendantOrEqual(baseImage, paths.OperationsDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OperationsDirectoryPath))
        {
            throw new InvalidOperationException("dragon save-trigger input/output roles overlap unsafely.");
        }
        RejectExistingAncestorReparsePoints(workspaceRoot, "dragon save-trigger workspace");
        RejectExistingAncestorReparsePoints(baseImage, "dragon save-trigger base BIN");
        RejectExistingAncestorReparsePoints(baseCue, "dragon save-trigger base CUE");
        RejectExistingAncestorReparsePoints(paths.OutputDirectoryPath, "dragon save-trigger output");
        RejectExistingAncestorReparsePoints(paths.OperationsDirectoryPath, "dragon save-trigger operations");
        RejectExistingAncestorReparsePoints(paths.WriterLeasePath, "dragon save-trigger writer lease");
        RejectReparsePoint(baseImage, "dragon save-trigger base BIN");
        RejectReparsePoint(baseCue, "dragon save-trigger base CUE");
        RejectReparsePoint(paths.PublicationParentPath, "dragon save-trigger publication parent");
        if (Directory.Exists(paths.OutputDirectoryPath))
            RejectReparsePoint(paths.OutputDirectoryPath, "existing dragon save-trigger output");
        if (Directory.Exists(paths.OperationsDirectoryPath))
            RejectReparsePoint(paths.OperationsDirectoryPath, "existing dragon save-trigger operations");
    }

    private static void RequirePathsInsideOutput(
        UnusedLevel65DragonSaveTriggerRuntimeCandidatePaths paths)
    {
        string[] artifacts =
        [
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.ConstructionPlanPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath
        ];
        if (artifacts.Any(path => !IsStrictDescendant(path, paths.OutputDirectoryPath)) ||
            !PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), paths.PublicationParentPath) ||
            !PathEquals(Path.GetDirectoryName(paths.WriterLeasePath), paths.PublicationParentPath))
        {
            throw new InvalidOperationException("A dragon save-trigger artifact escaped its owned output directory.");
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
            throw new InvalidDataException($"The dragon save-trigger candidate changed {label}.");
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

    private static void ApplyDataTransaction(byte[] data)
    {
        if (data.Length != DataByteLength)
            throw new InvalidDataException("The dragon/save-trigger transaction requires the exact ID65 data length.");
        foreach (int trueIndex in new[] { T100, T101, T105 })
        {
            TranslateXyz(
                data,
                ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength + 0x0C);
        }
        TranslateXyz(data, checked((int)(T101CameraLeadWadOffset - DataWadOffset)));
        int framesOffset = checked((int)(T101CameraFramesWadOffset - DataWadOffset));
        for (int frame = 0; frame < CameraFrameCount; frame++)
            TranslateXyz(data, framesOffset + frame * CameraFrameStride);
    }

    private static void TranslateXyz(byte[] data, int offset)
    {
        UnusedLevel65DragonSaveTriggerPoint point = ReadPoint(data, offset);
        BinaryPrimitives.WriteInt32LittleEndian(
            data.AsSpan(offset, 4), checked(point.X + TranslationRawX));
        BinaryPrimitives.WriteInt32LittleEndian(
            data.AsSpan(offset + 4, 4), checked(point.Y + TranslationRawY));
        BinaryPrimitives.WriteInt32LittleEndian(
            data.AsSpan(offset + 8, 4), checked(point.Z + TranslationRawZ));
    }

    private static void ValidateDataTransaction(
        byte[] before,
        byte[] after,
        IReadOnlyList<int> changedOffsets)
    {
        if (before.Length != DataByteLength || after.Length != DataByteLength)
            throw new InvalidDataException("The dragon/save-trigger data length changed.");
        byte[] expected = before.ToArray();
        ApplyDataTransaction(expected);
        if (!expected.SequenceEqual(after) ||
            changedOffsets.Count != ChangedLogicalWadBytes ||
            !changedOffsets.SequenceEqual(DifferentByteIndexes(before, expected)))
        {
            throw new InvalidDataException(
                "The ID65 logical diff escaped the exact T100/T101/T105 and camera XYZ transaction.");
        }

        foreach (int trueIndex in new[] { T100, T101, T105 })
        {
            int rowOffset = ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength;
            if (!before.AsSpan(rowOffset, 0x0C).SequenceEqual(after.AsSpan(rowOffset, 0x0C)) ||
                !before.AsSpan(rowOffset + 0x18, ObjectRecordByteLength - 0x18)
                    .SequenceEqual(after.AsSpan(rowOffset + 0x18, ObjectRecordByteLength - 0x18)))
            {
                throw new InvalidDataException($"T{trueIndex} changed outside XYZ; links/actors/properties/fixups are not preserved.");
            }
            RequireTranslatedPoint(before, after, rowOffset + 0x0C, $"T{trueIndex} XYZ");
        }

        int leadOffset = checked((int)(T101CameraLeadWadOffset - DataWadOffset));
        RequireTranslatedPoint(before, after, leadOffset, "T101 camera lead XYZ");
        if (!before.AsSpan(
                leadOffset + CameraFrameXyzByteLength,
                CameraLeadSemanticByteLength - CameraFrameXyzByteLength)
                .SequenceEqual(after.AsSpan(
                    leadOffset + CameraFrameXyzByteLength,
                    CameraLeadSemanticByteLength - CameraFrameXyzByteLength)))
        {
            throw new InvalidDataException(
                "T101 camera lead changed angles, cutscene/pedestal link, run-to fields, name index, or sentinel.");
        }
        int framesOffset = checked((int)(T101CameraFramesWadOffset - DataWadOffset));
        for (int frame = 0; frame < CameraFrameCount; frame++)
        {
            int offset = framesOffset + frame * CameraFrameStride;
            RequireTranslatedPoint(before, after, offset, $"T101 camera frame {frame} XYZ");
            if (!before.AsSpan(offset + CameraFrameXyzByteLength, CameraFrameStride - CameraFrameXyzByteLength)
                    .SequenceEqual(after.AsSpan(
                        offset + CameraFrameXyzByteLength,
                        CameraFrameStride - CameraFrameXyzByteLength)))
            {
                throw new InvalidDataException($"T101 camera frame {frame} changed angle/timing bytes.");
            }
        }
    }

    private static void RequireTranslatedPoint(byte[] before, byte[] after, int offset, string label)
    {
        UnusedLevel65DragonSaveTriggerPoint source = ReadPoint(before, offset);
        UnusedLevel65DragonSaveTriggerPoint output = ReadPoint(after, offset);
        UnusedLevel65DragonSaveTriggerPoint expected = new(
            checked(source.X + TranslationRawX),
            checked(source.Y + TranslationRawY),
            checked(source.Z + TranslationRawZ));
        if (output != expected)
            throw new InvalidDataException($"{label} translated to {output}, expected {expected}.");
    }

    private static UnusedLevel65DragonSaveTriggerPoint ReadPoint(
        ReadOnlySpan<byte> bytes,
        int offset) =>
        new(
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8, 4)));

    private static int[] DifferentByteIndexes(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The dragon save-trigger proposal changed ID65 data length.");
        return Enumerable.Range(0, before.Length).Where(index => before[index] != after[index]).ToArray();
    }

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
            throw new InvalidOperationException("Refused to delete a directory outside dragon save-trigger ownership.");
        RejectReparsePoint(full, "owned dragon save-trigger directory");
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
        string OutputExecutableSha256,
        int ChangedLogicalWadBytes,
        int ChangedLogicalExecutableBytes,
        IReadOnlyList<int> ChangedDataByteOffsets,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65DragonSaveTriggerRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256,
        bool DragonTargetPatchVerified,
        bool DragonSceneAndCameraTranslationVerified,
        bool RetailTownSquarePreserved,
        bool Mode2IntegrityVerified);

    private sealed record StagedCandidate(
        UnusedLevel65DragonSaveTriggerPlan Plan,
        UnusedLevel65DragonSaveTriggerReceipt Receipt,
        CandidateReadback Readback);

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
