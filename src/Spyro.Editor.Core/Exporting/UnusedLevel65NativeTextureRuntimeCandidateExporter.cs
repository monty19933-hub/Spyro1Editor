using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65NativeTextureRuntimeCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    bool RequestFinderReveal = true,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65NativeTextureRuntimeCandidatePaths(
    string OutputDirectoryPath,
    string OutputPrefix,
    string OutputImagePath,
    string OutputCuePath,
    string TextureCompositionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string OperationsDirectoryPath);

internal sealed record UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65NativeTextureRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string TextureAuthoringProfileId,
    string BaseImageSha256,
    string OutputImageSha256,
    string SourceDataSha256,
    string OutputDataSha256,
    string SourceTexturePagesSha256,
    string OutputTexturePagesSha256,
    string SourceModelSha256,
    string OutputModelSha256,
    string DonorManifestId,
    string DonorCompleteRecordSha256,
    int WadLba,
    int TargetWadEntry,
    long DataWadOffset,
    int DataByteLength,
    long TexturePagesWadOffset,
    int TexturePagesByteLength,
    long ModelWadOffset,
    int ModelByteLength,
    int SourceTextureCount,
    int OutputTextureCount,
    int PrivateTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    int TargetSectorIndex,
    int TargetHighDetailFaceIndex,
    int TargetLowDetailFaceIndex,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    string LogicalDiffManifestSha256,
    string DeterministicStaticPlanSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool DestinationOwnershipClosureVerified,
    bool DonorDependencyClosureVerified,
    bool PageAndClutRelocationVerified,
    bool PrivateFaceBindingVerified,
    bool HpLpPairingVerified,
    bool CollisionAndOcclusionPreserved,
    bool ProtectedSubfilesPreserved,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65NativeTextureRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string TextureCompositionPlanPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string? FinderHelperPath,
    string BaseImageSha256,
    string OutputImageSha256,
    string OutputDataSha256,
    string SourceDataSha256,
    string OutputTexturePagesSha256,
    string OutputModelSha256,
    string LogicalDiffManifestSha256,
    string DeterministicStaticPlanSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool TextureCompositionVerified,
    bool TexturePagesReadbackVerified,
    bool ModelReadbackVerified,
    bool PrivateFaceBindingVerified,
    bool HpLpPairingVerified,
    bool CollisionAndOcclusionPreserved,
    bool ProtectedSubfilesPreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65NativeTextureRuntimeCandidateResult(
    UnusedLevel65NativeTextureRuntimeCandidatePaths Paths,
    UnusedLevel65NativeTextureRuntimeCandidatePlan Plan,
    UnusedLevel65NativeTextureRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal? FinderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    string OutputImageSha256,
    string OutputDataSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool TextureCompositionVerified,
    bool TexturePagesReadbackVerified,
    bool ModelReadbackVerified,
    bool PrivateFaceBindingVerified,
    bool HpLpPairingVerified,
    bool CollisionAndOcclusionPreserved,
    bool ProtectedSubfilesPreserved,
    bool RawSectorIntegrityVerified,
    bool BaseCandidatePreserved,
    bool FullDirectoryPublicationVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool RollbackRecoveryVerified,
    bool FullDirectoryRollbackVerified,
    bool FinderHandoffVerified,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal static class UnusedLevel65NativeTextureRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-native-texture-gnastys-world-t12-private-t66-disposable-runtime-v1";
    public const string OutputPrefix =
        "Unused-Level-65-Native-Texture-T66-Gnastys-World-T12-RUNTIME-CANDIDATE";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-native-texture-runtime-candidate";
    private const string OperationDirectoryPrefix = "native-texture-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-native-texture-writer.lease";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const string ActiveWriterLeaseMessage =
        "Another native-texture runtime candidate writer is active for this publication parent. " +
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
    private const int WadLba = UnusedLevel65FullAuthoringConstructionTemplate.WadLba;
    private const int WadByteLength = UnusedLevel65FullAuthoringConstructionTemplate.ExpectedWadByteLength;
    private const long DataWadOffset = 0x6936800;
    private const int DataByteLength = UnusedLevel65NativeTextureAuthoringContract.TargetDataByteLength;
    private const long TexturePagesWadOffset = 0x6937000;
    private const int TexturePagesByteLength = UnusedLevel65NativeTextureAuthoringContract.TargetTexturePagesByteLength;
    private const long ModelWadOffset = 0x6A15000;
    private const int ModelByteLength = UnusedLevel65NativeTextureAuthoringContract.TargetModelByteLength;
    private const string BaseImageSha256 =
        UnusedLevel65NativeTextureAuthoringContract.ExpectedFoundationImageSha256;
    public const string ExpectedOutputImageSha256 =
        "776bc14042e2a740a107e5367fd350c1ce7f08b3753282369181bdee4dd79bf6";
    private const string ExpectedOutputDataSha256 =
        UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputDataSha256;
    private const long ExpectedChangedLogicalWadBytes =
        UnusedLevel65NativeTextureAuthoringContract.ExpectedChangedDataByteCount;
    public const long ExpectedChangedPhysicalImageBytes = 1_073_071;
    public const int ExpectedChangedRawSectorCount = 550;
    public const string ExpectedRawSectorDiffSha256 =
        "64f8d44b275e118cca9925191c9fcd231dabb07b987b5472b41f1de33019d155";
    private static readonly int[] ExpectedChangedRawSectorLbas =
    [
        .. Enumerable.Range(53_907, 256),
        .. Enumerable.Range(54_351, 272),
        .. Enumerable.Range(54_624, 2),
        .. Enumerable.Range(54_628, 20)
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UnusedLevel65NativeTextureRuntimeCandidateResult> CreateAsync(
        UnusedLevel65NativeTextureRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "exact ID65 foundation BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "exact ID65 foundation CUE");
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths = CreatePaths(
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
            "ID65 foundation BIN");

        UnusedLevel65NativeTextureStaticPlan texturePlan =
            UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(baseImage);
        UnusedLevel65NativeTextureStaticPlan texturePlanRepeated =
            UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(baseImage);
        ValidateTextureGate(texturePlan);
        ValidateTextureGate(texturePlanRepeated);
        ValidateTextureDeterminism(texturePlan, texturePlanRepeated);
        if (!IsExactDisposableNativeTextureRuntimeCandidateAuthorized(texturePlan))
        {
            throw new InvalidDataException(
                "The writer-local disposable native-texture runtime authorization predicate failed.");
        }
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "native-texture operation");
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

        UnusedLevel65NativeTextureRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                baseImage,
                baseCue,
                paths,
                stagedDirectory,
                texturePlan,
                loadCodes,
                request.RequestFinderReveal,
                rollbackRecoveryVerified,
                cancellationToken);
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);

            RequireHash(
                await HashFileAsync(baseImage, cancellationToken),
                BaseImageSha256,
                "ID65 foundation BIN after native-texture publication");
            WriteOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            RuntimeCandidateFinderReveal? finalReveal = staged.FinderReveal == null
                ? null
                : BuildFinalFinderReveal(paths);
            UnusedLevel65NativeTextureRuntimeCandidateReceipt receipt =
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
                staged.Readback.RawSectorDiffSha256,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                TextureCompositionVerified: true,
                TexturePagesReadbackVerified: true,
                ModelReadbackVerified: true,
                PrivateFaceBindingVerified: true,
                HpLpPairingVerified: true,
                CollisionAndOcclusionPreserved: true,
                ProtectedSubfilesPreserved: true,
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
            throw new IOException("The completed native-texture operation directory could not be removed.");
        return result ?? throw new InvalidOperationException(
            "The native-texture runtime writer completed without a result.");
    }

    public static UnusedLevel65NativeTextureRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string root = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, root) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException("The native-texture candidate output cannot be a filesystem or home root.");
        }
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The native-texture candidate output has no parent directory.");
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65NativeTextureRuntimeCandidatePaths result = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-texture-composition-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-native-texture-operations"));
        RequirePathsInsideOutput(result);
        return result;
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string baseImage,
        string baseCue,
        UnusedLevel65NativeTextureRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        UnusedLevel65NativeTextureStaticPlan texturePlan,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        bool requestFinderReveal,
        bool rollbackRecoveryVerified,
        CancellationToken cancellationToken)
    {
        string stagedImage = StagedPath(stagedDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stagedDirectory, finalPaths.OutputCuePath);
        string stagedPlan = StagedPath(stagedDirectory, finalPaths.TextureCompositionPlanPath);
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
            throw new InvalidDataException("The staged native-texture image is not MODE2/2352 with user offset 24.");

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
                throw new InvalidDataException("The staged native-texture WAD extent changed.");
            byte[] dataPreimage = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                DataWadOffset,
                DataByteLength);
            RequireHash(Hash(dataPreimage), texturePlan.SourceDataSha256, "staged row80 data preimage");
            byte[] dataAfter = ComposeOutputData(dataPreimage, texturePlan);
            RequireHash(Hash(dataAfter), texturePlan.OutputDataSha256, "staged row80 data composition");
            IReadOnlyList<(long Offset, int ByteLength)> changedDataRuns =
                BuildChangedLogicalRuns(dataPreimage, dataAfter, DataWadOffset);
            if (changedDataRuns.Count == 0)
                throw new InvalidDataException("The native-texture composition produced no logical row80 changes.");
            rebuiltRawSectorLbas = RawSectorLbasForFileRanges(WadLba, changedDataRuns);
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                DataWadOffset,
                dataAfter);
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
                    $"The diff-derived texture write rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectors} sectors, " +
                    $"expected {rebuiltRawSectorLbas.Count}.");
            }
            await image.FlushAsync(cancellationToken);
            image.Flush(flushToDisk: true);
        }

        string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(stagedImage));
        await WriteTextAsync(stagedCue, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");
        TextureReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            stagedImage,
            layout,
            texturePlan,
            rebuiltRawSectorLbas,
            cancellationToken);
        RequireOptionalPin(readback.OutputImageSha256, ExpectedOutputImageSha256, "native-texture output BIN");
        RequireOptionalPin(readback.OutputDataSha256, ExpectedOutputDataSha256, "native-texture output ID65 data");
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
             readback.RawSectorDiffSha256 != ExpectedRawSectorDiffSha256))
        {
            throw new InvalidDataException(
                $"The pinned native-texture diff changed: logical={readback.ChangedLogicalWadBytes}, " +
                $"physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
        }

        UnusedLevel65NativeTextureRuntimeCandidatePlan plan = new(
            PlanSchemaVersion,
            ProfileId,
            UnusedLevel65NativeTextureAuthoringContract.ProfileId,
            BaseImageSha256,
            readback.OutputImageSha256,
            texturePlan.SourceDataSha256,
            texturePlan.OutputDataSha256,
            texturePlan.SourceTexturePagesSha256,
            texturePlan.OutputTexturePagesSha256,
            texturePlan.SourceModelSha256,
            texturePlan.OutputModelSha256,
            texturePlan.Donor.ManifestId,
            texturePlan.Donor.CompleteRecordSha256,
            WadLba,
            texturePlan.TargetWadEntry,
            texturePlan.TargetDataWadOffset,
            texturePlan.TargetDataByteLength,
            texturePlan.TargetTexturePagesWadOffset,
            texturePlan.TargetTexturePagesByteLength,
            texturePlan.TargetModelWadOffset,
            texturePlan.TargetModelByteLength,
            texturePlan.SourceTextureCount,
            texturePlan.OutputTextureCount,
            texturePlan.PrivateTextureId,
            texturePlan.Donor.DonorWadEntry,
            texturePlan.Donor.DonorTextureId,
            texturePlan.FaceBinding.SectorIndex,
            texturePlan.FaceBinding.SourceHighDetailFaceIndex,
            texturePlan.FaceBinding.SourceLowDetailFaceIndex,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            readback.RawSectorDiffSha256,
            texturePlan.DiffManifestSha256,
            texturePlan.DeterministicPlanSha256,
            loadCodes,
            DestinationOwnershipClosureVerified: true,
            DonorDependencyClosureVerified: true,
            PageAndClutRelocationVerified: true,
            PrivateFaceBindingVerified: true,
            HpLpPairingVerified: true,
            CollisionAndOcclusionPreserved: true,
            ProtectedSubfilesPreserved: true,
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
        string locationGuide = BuildLocationGuideSvg(loadCodes);
        await WriteTextAsync(
            stagedGuide,
            locationGuide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        UnusedLevel65NativeTextureRuntimeCandidateReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            baseImage,
            baseCue,
            finalPaths.OutputDirectoryPath,
            finalPaths.OutputImagePath,
            finalPaths.OutputCuePath,
            finalPaths.TextureCompositionPlanPath,
            finalPaths.RuntimeChecklistPath,
            finalPaths.LocationGuidePath,
            finalReveal?.HelperPath,
            BaseImageSha256,
            readback.OutputImageSha256,
            readback.OutputDataSha256,
            texturePlan.SourceDataSha256,
            texturePlan.OutputTexturePagesSha256,
            texturePlan.OutputModelSha256,
            texturePlan.DiffManifestSha256,
            texturePlan.DeterministicPlanSha256,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            TextureCompositionVerified: true,
            TexturePagesReadbackVerified: true,
            ModelReadbackVerified: true,
            PrivateFaceBindingVerified: true,
            HpLpPairingVerified: true,
            CollisionAndOcclusionPreserved: true,
            ProtectedSubfilesPreserved: true,
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
        VerifyTextReadback(stagedPlan, JsonSerializer.Serialize(plan, JsonOptions) + "\n", "texture composition plan");
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

    private static async Task<TextureReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        DiscLayout expectedLayout,
        UnusedLevel65NativeTextureStaticPlan texturePlan,
        IReadOnlyList<int> rebuiltRawSectorLbas,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (outputLayout != expectedLayout)
            throw new InvalidDataException("The native-texture output changed the raw disc layout.");
        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The native-texture output changed the disc-image length.");
        DiscFileRecord baseWad = DiscImage.FindRootFileRecord(
            baseline,
            expectedLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (baseWad != outputWad || outputWad.Lba != WadLba || outputWad.Size != WadByteLength)
            throw new InvalidDataException("The native-texture output moved or resized WAD.WAD.");

        byte[] sourceData = DiscImage.ReadFileBytes(
            baseline,
            expectedLayout,
            WadLba,
            DataWadOffset,
            DataByteLength);
        byte[] expectedData = ComposeOutputData(sourceData, texturePlan);
        byte[] outputData = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            DataWadOffset,
            DataByteLength);
        if (!outputData.SequenceEqual(expectedData))
            throw new InvalidDataException("The native-texture row80 data failed exact plan readback.");
        RequireHash(Hash(sourceData), texturePlan.SourceDataSha256, "source row80 data readback");
        RequireHash(Hash(outputData), texturePlan.OutputDataSha256, "output row80 data readback");

        byte[] outputPages = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            TexturePagesWadOffset,
            TexturePagesByteLength);
        byte[] outputModel = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            ModelWadOffset,
            ModelByteLength);
        RequireHash(Hash(outputPages), texturePlan.OutputTexturePagesSha256, "texture-pages readback");
        RequireHash(Hash(outputModel), texturePlan.OutputModelSha256, "texture-authored model readback");
        if (!sourceData.AsSpan(0, 0x800).SequenceEqual(outputData.AsSpan(0, 0x800)))
            throw new InvalidDataException("The row80 entry header changed during native-texture publication.");
        VerifyProtectedSubfiles(sourceData, outputData);
        VerifyPrivateFaceReadback(outputData, texturePlan);

        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            expectedLayout,
            outputWad,
            cancellationToken);
        if (logical.ChangedBytes <= 0 || logical.OutsideTargetDataBytes != 0)
        {
            throw new InvalidDataException(
                $"The native-texture logical diff changed {logical.ChangedBytes} WAD bytes, " +
                $"including {logical.OutsideTargetDataBytes} outside the fixed row80 data entry.");
        }
        if (!logical.ChangedRawSectorLbas.SequenceEqual(rebuiltRawSectorLbas))
        {
            throw new InvalidDataException(
                "The diff-derived MODE2 rebuild sector set does not exactly match native-texture readback.");
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
                $"The native-texture physical diff changed {physical.ChangedRawSectorCount} sectors; " +
                $"header={physical.RawSectorDiffs.Sum(diff => diff.HeaderChangedBytes)}, " +
                $"subheader={physical.RawSectorDiffs.Sum(diff => diff.SubheaderChangedBytes)}, " +
                $"reserved={physical.RawSectorDiffs.Sum(diff => diff.ReservedChangedBytes)}.");
        }
        if (RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                output,
                outputLayout,
                CoalesceRawSectorRanges(rebuiltRawSectorLbas)) != rebuiltRawSectorLbas.Count)
        {
            throw new InvalidDataException("The published diff-derived sector integrity verification count changed.");
        }

        string rawSectorDiffSha256 = HashRawSectorDiffs(physical.RawSectorDiffs);
        return new(
            await HashFileAsync(outputImagePath, cancellationToken),
            Hash(outputData),
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
            physical.RawSectorDiffs,
            rawSectorDiffSha256);
    }

    private static byte[] ComposeOutputData(
        byte[] sourceData,
        UnusedLevel65NativeTextureStaticPlan texturePlan)
    {
        if (sourceData.Length != texturePlan.TargetDataByteLength ||
            texturePlan.TargetDataWadOffset != DataWadOffset)
        {
            throw new InvalidDataException("The native-texture row80 data boundary changed.");
        }
        byte[] output = sourceData.ToArray();
        foreach (NativeTerrainTexturePrivateStructuralPatch patch in texturePlan.Composition.CombinedPatches)
        {
            int relative = checked((int)(patch.WadOffset - DataWadOffset));
            if (relative < 0 || relative + patch.ByteLength > output.Length ||
                patch.Before.Length != patch.ByteLength ||
                patch.After.Length != patch.ByteLength ||
                !output.AsSpan(relative, patch.ByteLength).SequenceEqual(patch.Before))
            {
                throw new InvalidDataException(
                    $"Native-texture patch '{patch.Kind}' escaped row80 or lost its exact preimage.");
            }
            patch.After.CopyTo(output, relative);
        }
        return output;
    }

    private static void VerifyProtectedSubfiles(byte[] sourceData, byte[] outputData)
    {
        (int Offset, int Length)[] protectedSubfiles =
        [
            (0x173000, 0x05D000),
            (0x1D0000, 0x008800),
            (0x1D8800, 0x049800),
            (0x222000, 0x042800),
            (0x264800, 0x05D000),
            (0x2C1800, 0x020800)
        ];
        foreach ((int offset, int length) in protectedSubfiles)
        {
            if (!sourceData.AsSpan(offset, length).SequenceEqual(outputData.AsSpan(offset, length)))
                throw new InvalidDataException($"Protected row80 subfile at 0x{offset:X} changed.");
        }
    }

    private static void VerifyPrivateFaceReadback(
        byte[] outputData,
        UnusedLevel65NativeTextureStaticPlan texturePlan)
    {
        UnusedLevel65NativeTextureFaceBinding face = texturePlan.FaceBinding;
        int hpRelative = checked((int)(face.OutputHighDetailFaceWadOffset - DataWadOffset));
        int lpRelative = checked((int)(face.OutputLowDetailFaceWadOffset - DataWadOffset));
        string hpHex = Convert.ToHexString(outputData.AsSpan(hpRelative, 16));
        string lpHex = Convert.ToHexString(outputData.AsSpan(lpRelative, 8));
        if (!string.Equals(hpHex, face.OutputHighDetailFaceHex, StringComparison.Ordinal) ||
            !string.Equals(lpHex, face.OutputLowDetailFaceHex, StringComparison.Ordinal) ||
            face.OutputTextureId != UnusedLevel65NativeTextureAuthoringContract.PrivateTextureId ||
            !face.HighDetailTextureIdOnlyChanged ||
            !face.LowDetailFacePreserved ||
            !face.LowDetailGeometryIsColorOnly)
        {
            throw new InvalidDataException("The private T66 HP face or paired LP face failed exact readback.");
        }
    }

    private static void ValidateTextureGate(UnusedLevel65NativeTextureStaticPlan plan)
    {
        if (plan.ProfileId != UnusedLevel65NativeTextureAuthoringContract.ProfileId ||
            plan.SourceImageSha256 != BaseImageSha256 ||
            plan.TargetWadEntry != UnusedLevel65NativeTextureAuthoringContract.TargetWadEntry ||
            plan.TargetDataWadOffset != DataWadOffset ||
            plan.TargetDataByteLength != DataByteLength ||
            plan.TargetTexturePagesWadOffset != TexturePagesWadOffset ||
            plan.TargetTexturePagesByteLength != TexturePagesByteLength ||
            plan.TargetModelWadOffset != ModelWadOffset ||
            plan.TargetModelByteLength != ModelByteLength ||
            plan.SourceDataSha256 != UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceDataSha256 ||
            plan.OutputDataSha256 != ExpectedOutputDataSha256 ||
            plan.OutputTexturePagesSha256 != UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputTexturePagesSha256 ||
            plan.OutputModelSha256 != UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputModelSha256 ||
            plan.DiffManifestSha256 != UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffManifestSha256 ||
            plan.DeterministicPlanSha256 != UnusedLevel65NativeTextureAuthoringContract.ExpectedDeterministicPlanSha256 ||
            plan.ChangedDataByteCount != ExpectedChangedLogicalWadBytes ||
            plan.DiffRanges.Count != UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffRangeCount ||
            plan.Composition.TexturePagePatchCount != UnusedLevel65NativeTextureAuthoringContract.ExpectedTexturePagePatchCount ||
            plan.FaceBinding.SectorIndex != UnusedLevel65NativeTextureAuthoringContract.TargetSectorIndex ||
            plan.FaceBinding.SourceHighDetailFaceIndex != UnusedLevel65NativeTextureAuthoringContract.TargetHighDetailFaceIndex ||
            plan.FaceBinding.SourceLowDetailFaceIndex != UnusedLevel65NativeTextureAuthoringContract.TargetLowDetailFaceIndex)
        {
            throw new InvalidDataException("The pinned native-texture static contract changed.");
        }
        if (!plan.DestinationOwnershipClosureComplete ||
            !plan.DestinationRuntimeControlAuditComplete ||
            !plan.PrivateRecordRuntimePersistent ||
            !plan.DonorDependencyClosureVerified ||
            !plan.PageAndClutRelocationVerified ||
            !plan.PrivatePerFaceStorageVerified ||
            !plan.HpLpPairingVerified ||
            !plan.CollisionAndOcclusionPreserved ||
            !plan.TargetEntryHeaderPreserved ||
            !plan.ProtectedSubfilesPreserved ||
            !plan.RetailWadEntriesExcluded ||
            !plan.ExecutableExcluded ||
            !plan.DeterministicReadbackRequired ||
            plan.DisposableRuntimeCandidateAuthorized ||
            plan.PromotionAuthorized ||
            plan.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The native-texture static safety/non-promotion gate changed.");
        }
    }

    private static void ValidateTextureDeterminism(
        UnusedLevel65NativeTextureStaticPlan first,
        UnusedLevel65NativeTextureStaticPlan second)
    {
        if (first.SourceImageSha256 != second.SourceImageSha256 ||
            first.OutputDataSha256 != second.OutputDataSha256 ||
            first.OutputTexturePagesSha256 != second.OutputTexturePagesSha256 ||
            first.OutputModelSha256 != second.OutputModelSha256 ||
            first.DiffManifestSha256 != second.DiffManifestSha256 ||
            first.DeterministicPlanSha256 != second.DeterministicPlanSha256 ||
            !first.DiffRanges.SequenceEqual(second.DiffRanges) ||
            first.FaceBinding != second.FaceBinding ||
            first.Donor.ManifestId != second.Donor.ManifestId ||
            first.Donor.CompleteRecordSha256 != second.Donor.CompleteRecordSha256 ||
            first.Composition.TexturePagePatchCount != second.Composition.TexturePagePatchCount ||
            first.Composition.CombinedPatches.Count != second.Composition.CombinedPatches.Count)
        {
            throw new InvalidDataException("Two native-texture static plans were not deterministic.");
        }
    }

    private static bool IsExactDisposableNativeTextureRuntimeCandidateAuthorized(
        UnusedLevel65NativeTextureStaticPlan plan) =>
        plan.ProfileId == UnusedLevel65NativeTextureAuthoringContract.ProfileId &&
        plan.SourceImageSha256 == BaseImageSha256 &&
        plan.OutputDataSha256 == ExpectedOutputDataSha256 &&
        plan.DestinationOwnershipClosureComplete &&
        plan.DestinationRuntimeControlAuditComplete &&
        plan.PrivateRecordRuntimePersistent &&
        plan.DonorDependencyClosureVerified &&
        plan.PageAndClutRelocationVerified &&
        plan.PrivatePerFaceStorageVerified &&
        plan.HpLpPairingVerified &&
        plan.CollisionAndOcclusionPreserved &&
        plan.TargetEntryHeaderPreserved &&
        plan.ProtectedSubfilesPreserved &&
        plan.RetailWadEntriesExcluded &&
        plan.ExecutableExcluded &&
        plan.DeterministicReadbackRequired &&
        !plan.DisposableRuntimeCandidateAuthorized &&
        !plan.PromotionAuthorized &&
        !plan.NormalCreateBinEnabled;

    private static LogicalDiff CompareLogicalWad(
        FileStream baseline,
        FileStream output,
        DiscLayout layout,
        DiscFileRecord wad,
        CancellationToken cancellationToken)
    {
        const int chunkByteLength = 64 * 1024;
        long changed = 0;
        long outsideTargetData = 0;
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
                    outsideTargetData++;
                changedRawSectorLbas.Add(checked(wad.Lba + (int)(wadOffset / LogicalSectorByteLength)));
            }
        }
        return new(changed, outsideTargetData, changedRawSectorLbas.ToArray());
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The native-texture physical image comparison has incompatible lengths.");
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        List<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> diffs = [];
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
        IReadOnlyList<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff diff in diffs)
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
            throw new InvalidDataException("The native-texture model preimage/composition lengths differ.");
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
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        TextureReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Native Texture T66 — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This candidate layers one complete static Gnasty's World T12 native texture record into ID65-private T66 " +
            "and binds it only to the authored sector-213 HP triangle. The paired LP triangle remains native color-only. " +
            "This is a disposable DuckStation discriminator, not normal Create BIN or release output.");
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
        builder.AppendLine($"- Texture pages SHA-256: `{UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputTexturePagesSha256}`");
        builder.AppendLine($"- Texture-authored model SHA-256: `{UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputModelSha256}`");
        builder.AppendLine($"- Logical diff manifest SHA-256: `{UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffManifestSha256}`");
        builder.AppendLine($"- Raw-sector diff SHA-256: `{readback.RawSectorDiffSha256}`");
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
        builder.AppendLine(
            "2. Do not use a save state. Set **Memory Card 1 = None** and **Memory Card 2 = None**, " +
            "cold boot the candidate CUE, and **do not save**.");
        builder.AppendLine("3. Enter the ID65 load code once and confirm the Inventory still labels it `TOWN SQUARE`.");
        builder.AppendLine("4. Stop at the fly-in landing. Locate the isolated authored triangle before moving Spyro.");
        builder.AppendLine();
        builder.AppendLine("## Candidate gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] **Close HP texture:** the authored triangle shows the imported native T66 material, not its former T25 material, a flat fallback, black pixels, or scrambled pixels.");
        builder.AppendLine("- [ ] **Page/CLUT stability:** circle the triangle and move the camera across all sides; color and texels remain coherent without flicker, palette corruption, seams, or adjacent-surface contamination.");
        builder.AppendLine("- [ ] **Solid surface preserved:** walk, charge, jump, and land across the triangle in both directions; collision follows the visible slope with no pass-through or hidden plane.");
        builder.AppendLine("- [ ] **Far LP transition:** back the camera along the entry route to camera depth >=2780; the paired color-only LP footprint stays visible and stable. It is not expected to retain the HP texture.");
        builder.AppendLine("- [ ] **Near return:** bring the camera close again; the same private T66 HP texture returns on exactly one authored face.");
        builder.AppendLine("- [ ] Reset and cold-enter ID65 once more without a save state; texture, LP transition, and solidity are identical.");
        builder.AppendLine();
        builder.AppendLine("## Comparison gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] Retail Town Square loads normally and has no ID65 private T66 texture or authored triangle.");
        builder.AppendLine("- [ ] Gnasty's Loot loads and plays normally.");
        builder.AppendLine("- [ ] Sunny Flight loads and plays normally.");
        builder.AppendLine();
        builder.AppendLine("## Report boundary");
        builder.AppendLine();
        builder.AppendLine(
            "Report the close texture, all-side page/CLUT behavior, far LP transition, return-to-near texture, solidity, reset, " +
            "and each retail control separately. A loading screen or still image is not a pass. RUNTIME PENDING / UNPROMOTED; " +
            "do not use this candidate for normal editor output or release packaging.");
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
        "  <rect width=\"1200\" height=\"1000\" fill=\"#101522\"/>\n" +
        "  <text x=\"60\" y=\"58\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"31\" font-weight=\"700\">ID65 Private T66 Native Texture Runtime Location</text>\n" +
        "  <text x=\"60\" y=\"96\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\">Stop at the fly-in landing. Locate the isolated authored triangle.</text>\n" +
        "  <text x=\"60\" y=\"121\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\">Close HP uses imported T66; far LP is intentionally color-only.</text>\n" +
        "  <rect x=\"60\" y=\"145\" width=\"1080\" height=\"440\" rx=\"22\" fill=\"#1b2437\" stroke=\"#6d7fa8\" stroke-width=\"3\"/>\n" +
        "  <circle cx=\"265\" cy=\"360\" r=\"34\" fill=\"#7f5cff\"/>\n" +
        "  <text x=\"155\" y=\"425\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"21\">SPAWN / fly-in landing</text>\n" +
        "  <path d=\"M320 360 L510 360\" stroke=\"#f8d05c\" stroke-width=\"12\" marker-end=\"url(#arrow)\"/>\n" +
        "  <defs><marker id=\"arrow\" markerWidth=\"12\" markerHeight=\"12\" refX=\"10\" refY=\"6\" orient=\"auto\"><path d=\"M0,0 L12,6 L0,12 Z\" fill=\"#f8d05c\"/></marker></defs>\n" +
        "  <polygon points=\"585,470 920,470 752,215\" fill=\"#4fd7a0\" stroke=\"#d9fff0\" stroke-width=\"5\"/>\n" +
        "  <text x=\"545\" y=\"506\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"20\" font-weight=\"700\">Authored triangle</text>\n" +
        "  <text x=\"545\" y=\"532\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"20\" font-weight=\"700\">close HP = private T66</text>\n" +
        "  <text x=\"825\" y=\"506\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P1 (7762,6346,512)</text>\n" +
        "  <text x=\"825\" y=\"528\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P2 (7890,6346,512)</text>\n" +
        "  <text x=\"825\" y=\"550\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P3 (7826,6474,640)</text>\n" +
        "  <text x=\"60\" y=\"615\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Close HP: circle all sides; check coherent T66 texels and palette with no bleed.</text>\n" +
        "  <text x=\"60\" y=\"642\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Traverse, charge, jump, and land; visible slope and collision must remain aligned.</text>\n" +
        "  <text x=\"60\" y=\"677\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Far-LOD route: back the camera away along the entry route to camera depth &gt;= 2780.</text>\n" +
        "  <text x=\"60\" y=\"704\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Far LP stays color-only and stable; return near and confirm private T66 restores.</text>\n" +
        "  <text x=\"60\" y=\"742\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"16\" font-weight=\"700\">Pinned candidate identity</text>\n" +
        $"  <text x=\"60\" y=\"766\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">Profile: {ProfileId}</text>\n" +
        $"  <text x=\"60\" y=\"786\" fill=\"#8fa8d8\" font-family=\"Menlo,monospace\" font-size=\"10\">CUE: {XmlEscape(OutputPrefix + ".cue")}</text>\n" +
        $"  <text x=\"60\" y=\"806\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"10\">BIN SHA-256: {ExpectedOutputImageSha256}</text>\n" +
        "  <text x=\"730\" y=\"806\" fill=\"#ff9c9c\" font-family=\"Menlo,monospace\" font-size=\"11\" font-weight=\"700\">RUNTIME PENDING / UNPROMOTED</text>\n" +
        "  <text x=\"60\" y=\"838\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\" font-weight=\"700\">Full comparison load codes</text>\n" +
        $"  <text x=\"80\" y=\"864\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[0]}</text>\n" +
        $"  <text x=\"80\" y=\"888\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[1]}</text>\n" +
        $"  <text x=\"80\" y=\"912\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[2]}</text>\n" +
        $"  <text x=\"80\" y=\"936\" fill=\"#c8d6f4\" font-family=\"Menlo,monospace\" font-size=\"10\">{codeRows[3]}</text>\n" +
        "  <text x=\"60\" y=\"976\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Load paired CUE. Cheats off; cold boot; Memory Cards 1/2=None; no save state.</text>\n" +
        "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
    {
        string[] required =
        [
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.LocationGuidePath),
            BaseImageSha256,
            UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputTexturePagesSha256,
            UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputModelSha256,
            UnusedLevel65NativeTextureAuthoringContract.ExpectedDiffManifestSha256,
            "load the **CUE**, not the BIN",
            "Close HP texture",
            "Page/CLUT stability",
            "Far LP transition",
            "Near return",
            "Solid surface preserved",
            "Memory Card 1 = None",
            "Memory Card 2 = None",
            "do not save",
            "RUNTIME PENDING / UNPROMOTED",
            "Retail Town Square",
            "Gnasty's Loot",
            "Sunny Flight"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The native-texture runtime checklist omitted an exact required gate.");
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
            "ID65 Private T66 Native Texture Runtime Location",
            "SPAWN / fly-in landing",
            "Authored triangle",
            "close HP = private T66",
            "7762,6346,512",
            "7890,6346,512",
            "7826,6474,640",
            "Load paired CUE",
            "width=\"1200\" height=\"1000\"",
            "data-safe-left=\"60\" data-safe-right=\"1140\"",
            "Close HP: circle all sides",
            "Traverse, charge, jump, and land",
            "Far-LOD route:",
            "camera depth &gt;= 2780",
            "Far LP stays color-only and stable",
            "return near and confirm private T66 restores",
            "Memory Cards 1/2=None",
            "Pinned candidate identity",
            ProfileId,
            OutputPrefix + ".cue",
            ExpectedOutputImageSha256,
            "RUNTIME PENDING / UNPROMOTED",
            "font-size=\"10\""
        ];
        if (!svg.StartsWith("<?xml", StringComparison.Ordinal) ||
            required.Any(value => !svg.Contains(value, StringComparison.Ordinal)) ||
            svg.Contains("Profile: " + ProfileId + " | CUE:", StringComparison.Ordinal) ||
            svg.Contains("(7762,6346,512)  (7890,6346,512)", StringComparison.Ordinal) ||
            svg.Contains("confirm the same LP footprint remains visible.</text>", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The native-texture SVG location guide failed deterministic readback.");
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
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths,
        bool includeFinderReveal)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            Path.GetFileName(paths.OutputImagePath),
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.TextureCompositionPlanPath),
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
                $"The staged native-texture artifact set is [{string.Join(',', actual)}], expected [{string.Join(',', expected.Order())}].");
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
                throw new IOException("The native-texture destination changed before replacement publication.");
            if (Directory.Exists(publication.BackupDirectoryPath))
                throw new IOException("The native-texture operation backup path already exists.");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            publication.PreviousBackedUp = true;
            WriteOperationJournal(publication, OperationPhasePreviousBackedUp);
            testStageHook?.Invoke("after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The native-texture destination appeared during staging.");
        }

        if (!Directory.Exists(publication.StagedDirectoryPath))
            throw new IOException("The staged native-texture candidate disappeared before publication.");
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
                if (publication.HadPreviousCandidate)
                {
                    testStageHook?.Invoke("before-previous-candidate-restore");
                    if (!Directory.Exists(publication.BackupDirectoryPath))
                        throw new IOException("The previous native-texture candidate backup is missing.");
                    if (Directory.Exists(publication.OutputDirectoryPath))
                        throw new IOException("The native-texture destination is occupied before backup restoration.");
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
                "Native-texture publication failed and full-directory recovery is incomplete; " +
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
        RejectReparsePoint(operationsDirectoryPath, "native-texture operations directory");
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
                "stale native-texture publication output");
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
                $"The native-texture operations directory contains a non-owned entry: {operationRoot}");
        }
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale native-texture operation");
        RejectReparsePoint(operationRoot, "stale native-texture operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale native-texture operation lease");
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
                throw new IOException("A native-texture candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned native-texture operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale native-texture operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A native-texture operation journal is unreadable.");
        ValidateOperationJournal(
            journal,
            operationRoot,
            operationsDirectoryPath,
            expectedOutputDirectoryPath);

        RejectUnexpectedDirectoryRoleType(
            journal.StagedDirectoryPath,
            "stale native-texture staged candidate");
        RejectUnexpectedDirectoryRoleType(
            journal.BackupDirectoryPath,
            "stale native-texture previous-candidate backup");
        bool stagedExists = Directory.Exists(journal.StagedDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (stagedExists)
        {
            RejectTreeReparsePoints(
                journal.StagedDirectoryPath,
                "stale native-texture staged candidate");
        }
        if (backupExists)
        {
            RejectTreeReparsePoints(
                journal.BackupDirectoryPath,
                "stale native-texture previous-candidate backup");
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
                "Multiple previous native-texture candidate backups exist for the same publication target; " +
                "recovery was preserved for audit.");
        }
        if (committed.Length > 1)
        {
            throw new IOException(
                "Multiple committed native-texture operations claim the same publication target; " +
                "recovery was preserved for audit.");
        }
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A native-texture operation without a previous candidate owns an unexpected backup.");
            }
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp &&
                !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A native-texture operation reports a previous-candidate backup without a previous candidate.");
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
                    "A committed native-texture operation is missing its candidate directory; " +
                    "recovery was preserved for audit.");
            }
            if (backups.Length == 1 && !ReferenceEquals(backups[0], committedOperation))
            {
                throw new IOException(
                    "An uncommitted native-texture backup competes with a committed publication; " +
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
                    "A rollback-complete native-texture operation still owns a backup; " +
                    "recovery was preserved for audit.");
            }
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
            {
                throw new IOException(
                    "A previous native-texture candidate is missing and no unique backup can restore it; " +
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
                        "A stale native-texture operation cannot prove whether the published directory is the " +
                        "previous candidate; recovery was preserved for audit.");
                }

                bool uncommittedCandidateIsAuthoritative = operations.Any(operation =>
                    operation.Journal.Phase == OperationPhaseCandidatePublished ||
                    (operation.Journal.Phase == OperationPhaseStaging && !operation.StagedExists));
                if (!uncommittedCandidateIsAuthoritative)
                {
                    throw new IOException(
                        "A no-previous native-texture recovery cannot prove ownership of the published directory; " +
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
                throw new IOException("The native-texture destination is occupied before authoritative backup restoration.");
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
                "A native-texture recovery refused to discard an operation while its backup still exists.");
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
            RejectReparsePoint(entry, "stale native-texture operation entry");
            string name = Path.GetFileName(entry);
            if (!allowedNames.Contains(name))
            {
                throw new InvalidDataException(
                    $"A stale native-texture operation contains an unowned entry: {entry}");
            }
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
            {
                throw new InvalidDataException(
                    $"A stale native-texture operation entry has the wrong filesystem role: {entry}");
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
            throw new InvalidOperationException("The native-texture publication output has no parent.");
        if (!Directory.Exists(parent))
            return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, outputDirectoryPath))
                continue;
            RejectReparsePoint(entry, "native-texture publication output path");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    "The native-texture publication output path is not a directory.");
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
            throw new InvalidOperationException("A native-texture recovery target escaped the exact requested output path.");
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
            throw new InvalidDataException("A stale native-texture operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale native-texture journal points outside its publication parent.");
    }

    private static void VerifyPublishedCandidate(
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published native-texture candidate directory is missing.");
        VerifyStagedCandidateArtifacts(
            paths.OutputDirectoryPath,
            paths,
            staged.FinderReveal != null);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        RequireHash(HashFile(paths.OutputImagePath), staged.Readback.OutputImageSha256, "published native-texture BIN");
        VerifyTextReadback(
            paths.TextureCompositionPlanPath,
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
                throw new InvalidDataException("The published native-texture Finder helper is not exact 0755.");
            }
            RuntimeCandidateTestHandoff.VerifyChecklistReadback(
                checklist,
                finalReveal,
                staged.Plan.LoadCodes);
        }
        else if (File.Exists(paths.FinderHelperPath))
        {
            throw new InvalidDataException("A native-texture Finder helper was published while handoff was disabled.");
        }
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths) =>
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
        RejectReparsePoint(outputDirectoryPath, "native-texture output directory");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The native-texture candidate directory already exists. " +
                "Set ReplaceExistingCandidate only for an intentional atomic replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
    {
        if (IsDescendantOrEqual(baseImage, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(baseCue, paths.OutputDirectoryPath) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseImage) ||
            IsDescendantOrEqual(paths.OutputDirectoryPath, baseCue))
        {
            throw new InvalidOperationException(
                "The native-texture output directory and locked base files must have distinct roles.");
        }
        if (PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The native-texture output and operations directories overlap.");
    }

    private static void RequirePathsInsideOutput(
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
    {
        string[] artifacts =
        [
            paths.OutputPrefix,
            paths.OutputImagePath,
            paths.OutputCuePath,
            paths.TextureCompositionPlanPath,
            paths.StaticReadbackReceiptPath,
            paths.RuntimeChecklistPath,
            paths.LocationGuidePath,
            paths.FinderHelperPath
        ];
        foreach (string artifact in artifacts)
            EnsureStrictDescendant(artifact, paths.OutputDirectoryPath, "native-texture artifact");
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent))
            throw new InvalidOperationException("The native-texture operations directory escaped the output parent.");
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
        UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The native-texture operations directory has no parent.");
        Directory.CreateDirectory(parent);
        string leasePath = Path.Combine(parent, GlobalWriterLeaseFileName);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "native-texture global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The native-texture writer lease path is a directory.");
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
                "The native-texture global writer lease file could not be opened.",
                ex);
        }

        try
        {
            RejectReparsePoint(leasePath, "native-texture global writer lease");
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
                        "The native-texture global writer OS range lease could not be acquired.",
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
                    $"The native-texture global writer OS lease failed (errno {error}: {nativeError.Message}).",
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
            throw new InvalidOperationException("A native-texture rollback target escaped its exact publication path.");
    }

    private static void EnsureJournalPublicationPath(
        string path,
        CandidateOperationJournal journal)
    {
        if (!PathEquals(path, journal.OutputDirectoryPath))
            throw new InvalidOperationException("A stale native-texture rollback target escaped its journal path.");
    }

    private static void TryDeleteOwnedOperation(string operationRoot, string operationsDirectoryPath)
    {
        if (!Directory.Exists(operationRoot))
            return;
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed native-texture operation");
        if (!Path.GetFileName(operationRoot).StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-owned native-texture operation directory.");
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
        UnusedLevel65NativeTextureRuntimeCandidatePlan Plan,
        UnusedLevel65NativeTextureRuntimeCandidateReceipt Receipt,
        RuntimeCandidateFinderReveal? FinderReveal,
        TextureReadback Readback,
        int RebuiltRawSectorCount);

    private sealed record TextureReadback(
        string OutputImageSha256,
        string OutputDataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256);

    private sealed record LogicalDiff(
        long ChangedBytes,
        long OutsideTargetDataBytes,
        IReadOnlyList<int> ChangedRawSectorLbas);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
