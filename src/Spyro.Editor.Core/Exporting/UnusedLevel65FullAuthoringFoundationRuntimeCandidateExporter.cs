using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    bool RequestFinderReveal = true,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths(
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

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string ConstructionProfileId,
    string BaseImageSha256,
    string ModelPreimageSha256,
    string AuthoredModelSha256,
    string AuthoredCollisionSha256,
    string AuthoredCollisionTreeSha256,
    string AuthoredCollisionBlocksSha256,
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
    IReadOnlyList<UnusedLevel65ConstructionCollisionCell> ChangedCollisionCells,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool FullExposureProofVerified,
    bool SpawnAndPlayerAnchorVerified,
    bool CycloramaVerified,
    bool DisplayNameVerified,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidateReceipt(
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
    string BaseImageSha256,
    string OutputImageSha256,
    string OutputDataSha256,
    string AuthoredModelSha256,
    string AuthoredCollisionSha256,
    string AuthoredCollisionTreeSha256,
    string AuthoredCollisionBlocksSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FoundationCompositionVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool CollisionSemanticDeltaVerified,
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

internal sealed record UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult(
    UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths Paths,
    UnusedLevel65FullAuthoringFoundationRuntimeCandidatePlan Plan,
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal? FinderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    string OutputImageSha256,
    string OutputDataSha256,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FoundationCompositionVerified,
    bool ModelReadbackVerified,
    bool CollisionReadbackVerified,
    bool CollisionSemanticDeltaVerified,
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

internal static class UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter
{
    public static bool Retired => true;
    public static string RetirementReason =>
        "RETIRED: this candidate is stacked on the positive-wound ID65 foundation collision convention, which is not runtime-proven and matches the failed remote-blank v1 sign. Preserve frozen artifacts only as historical evidence; do not republish or load them. Use the isolated collision-winding-repair v2 discriminator until DuckStation establishes the playable convention.";

    public const string ProfileId =
        "unused-level-65-full-authoring-foundation-native-membership-clean-usa-disposable-v1";
    public const string OutputPrefix =
        "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-full-authoring-foundation-runtime-candidate";
    private const string OperationDirectoryPrefix = "foundation-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-full-authoring-foundation-writer.lease";
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const string ActiveWriterLeaseMessage =
        "Another foundation runtime candidate writer is active for this publication parent. " +
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
    private const long DataWadOffset = UnusedLevel65FullAuthoringConstructionTemplate.DataEntryWadOffset;
    private const int DataByteLength = UnusedLevel65FullAuthoringConstructionTemplate.DataEntryByteLength;
    private const long ModelWadOffset = UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset;
    private const int ModelByteLength = UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileByteLength;
    private const string BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    private const string ModelPreimageSha256 =
        "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
    private const string AuthoredModelSha256 =
        "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
    private const string AuthoredCollisionSha256 =
        "5e7b4430c9bfbd2793df1d9833d8d3af005924d7c110b66f0b0e1f8bc818c056";
    private const string AuthoredCollisionTreeSha256 =
        "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd";
    private const string AuthoredCollisionBlocksSha256 =
        "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4";
    private const string ExpectedOutputImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    private const string ExpectedOutputDataSha256 =
        "eb5ca8459300392de12246e57770a57447f7bd64ccf3e978137d2362d7694160";
    private const long ExpectedChangedLogicalWadBytes = 300_553;
    private const long ExpectedChangedPhysicalImageBytes = 359_151;
    private const int ExpectedChangedRawSectorCount = 213;
    public const string ExpectedRawSectorDiffSha256 =
        "85cee7ab1010bce03e8d533fadb2b2d8be7c448cc3254db2670bb2b576887baa";
    private const int AuthoredCollisionRelativeOffset = 0x2BDF0;
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

    public static async Task<UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult> CreateAsync(
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Retired)
            throw new InvalidOperationException(RetirementReason);

        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "ID65 construction baseline BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "ID65 construction baseline CUE");
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths = CreatePaths(
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
            "ID65 construction baseline BIN");

        UnusedLevel65FullAuthoringConstructionContract contract =
            await UnusedLevel65FullAuthoringConstructionTemplate.InspectAsync(
                baseImage,
                cancellationToken);
        UnusedLevel65ConstructionCollidableTerrainPlan construction =
            await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
                baseImage,
                cancellationToken);
        UnusedLevel65ConstructionCollidableTerrainPlan constructionRepeated =
            await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
                baseImage,
                cancellationToken);
        ValidateConstructionGate(contract, construction);
        ValidateConstructionGate(contract, constructionRepeated);
        ValidateConstructionDeterminism(construction, constructionRepeated);
        if (!IsExactDisposableFoundationRuntimeCandidateAuthorized(contract, construction))
        {
            throw new InvalidDataException(
                "The writer-local disposable foundation runtime authorization predicate failed.");
        }
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "foundation operation");
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

        UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                baseImage,
                baseCue,
                paths,
                stagedDirectory,
                contract,
                construction,
                loadCodes,
                request.RequestFinderReveal,
                rollbackRecoveryVerified,
                cancellationToken);
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);

            RequireHash(
                await HashFileAsync(baseImage, cancellationToken),
                BaseImageSha256,
                "ID65 construction baseline after publication");
            WriteOperationJournal(publication, OperationPhaseCommitted);
            publication.Committed = true;
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            RuntimeCandidateFinderReveal? finalReveal = staged.FinderReveal == null
                ? null
                : BuildFinalFinderReveal(paths);
            UnusedLevel65FullAuthoringFoundationRuntimeCandidateReceipt receipt =
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
                ExpectedRawSectorDiffSha256,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                FoundationCompositionVerified: true,
                ModelReadbackVerified: true,
                CollisionReadbackVerified: true,
                CollisionSemanticDeltaVerified: true,
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
            throw new IOException("The completed foundation operation directory could not be removed.");
        return result ?? throw new InvalidOperationException(
            "The foundation runtime writer completed without a result.");
    }

    public static UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths CreatePaths(
        string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string root = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, root) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException("The foundation candidate output cannot be a filesystem or home root.");
        }
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The foundation candidate output has no parent directory.");
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths result = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-foundation-composition-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-full-authoring-foundation-operations"));
        RequirePathsInsideOutput(result);
        return result;
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string baseImage,
        string baseCue,
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        UnusedLevel65FullAuthoringConstructionContract contract,
        UnusedLevel65ConstructionCollidableTerrainPlan construction,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        bool requestFinderReveal,
        bool rollbackRecoveryVerified,
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
            throw new InvalidDataException("The staged foundation image is not MODE2/2352 with user offset 24.");

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
                throw new InvalidDataException("The staged foundation WAD extent changed.");
            byte[] modelPreimage = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                ModelWadOffset,
                ModelByteLength);
            RequireHash(Hash(modelPreimage), ModelPreimageSha256, "staged model preimage");
            IReadOnlyList<(long Offset, int ByteLength)> changedModelRuns =
                BuildChangedLogicalRuns(modelPreimage, construction.AfterModelBytes, ModelWadOffset);
            if (changedModelRuns.Count == 0)
                throw new InvalidDataException("The foundation construction produced no logical model changes.");
            rebuiltRawSectorLbas = RawSectorLbasForFileRanges(WadLba, changedModelRuns);
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                ModelWadOffset,
                construction.AfterModelBytes);
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
                    $"The diff-derived model write rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectors} sectors, " +
                    $"expected {rebuiltRawSectorLbas.Count}.");
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
            contract,
            construction,
            rebuiltRawSectorLbas,
            cancellationToken);
        RequireOptionalPin(readback.OutputImageSha256, ExpectedOutputImageSha256, "foundation output BIN");
        RequireOptionalPin(readback.OutputDataSha256, ExpectedOutputDataSha256, "foundation output ID65 data");
        if ((ExpectedChangedLogicalWadBytes >= 0 &&
             readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes) ||
            (ExpectedChangedPhysicalImageBytes >= 0 &&
             readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes) ||
            (ExpectedChangedRawSectorCount >= 0 &&
             readback.ChangedRawSectorCount != ExpectedChangedRawSectorCount) ||
            !readback.RawSectorDiffs.Select(diff => diff.RawSectorLba)
                .SequenceEqual(ExpectedChangedRawSectorLbas) ||
            HashRawSectorDiffs(readback.RawSectorDiffs) != ExpectedRawSectorDiffSha256)
        {
            throw new InvalidDataException(
                $"The pinned foundation diff changed: logical={readback.ChangedLogicalWadBytes}, " +
                $"physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
        }

        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePlan plan = new(
            PlanSchemaVersion,
            ProfileId,
            UnusedLevel65FullAuthoringConstructionTemplate.ProfileId,
            BaseImageSha256,
            ModelPreimageSha256,
            AuthoredModelSha256,
            AuthoredCollisionSha256,
            AuthoredCollisionTreeSha256,
            AuthoredCollisionBlocksSha256,
            WadLba,
            ModelWadOffset,
            ModelByteLength,
            construction.VisualTerrain.SectorIndex,
            construction.VisualTerrain.AddedLowDetailVertexCount,
            construction.VisualTerrain.AddedLowDetailFaceCount,
            construction.VisualTerrain.AddedHighDetailVertexCount,
            construction.VisualTerrain.AddedHighDetailFaceCount,
            construction.CollisionBinding.ReusedTriangleIndex,
            construction.NativeCollisionCellCount,
            construction.AuthoredCollisionCellCount,
            construction.ChangedCollisionCells,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            ExpectedRawSectorDiffSha256,
            loadCodes,
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
        string locationGuide = BuildLocationGuideSvg(loadCodes);
        await WriteTextAsync(
            stagedGuide,
            locationGuide,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        UnusedLevel65FullAuthoringFoundationRuntimeCandidateReceipt receipt = new(
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
            BaseImageSha256,
            readback.OutputImageSha256,
            readback.OutputDataSha256,
            AuthoredModelSha256,
            AuthoredCollisionSha256,
            AuthoredCollisionTreeSha256,
            AuthoredCollisionBlocksSha256,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.ChangedRawSectorCount,
            readback.RawSectorDiffs,
            ExpectedRawSectorDiffSha256,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FoundationCompositionVerified: true,
            ModelReadbackVerified: true,
            CollisionReadbackVerified: true,
            CollisionSemanticDeltaVerified: true,
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

    private static async Task<FoundationReadback> VerifyImageReadbackAsync(
        string baseImagePath,
        string outputImagePath,
        DiscLayout expectedLayout,
        UnusedLevel65FullAuthoringConstructionContract contract,
        UnusedLevel65ConstructionCollidableTerrainPlan construction,
        IReadOnlyList<int> rebuiltRawSectorLbas,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (outputLayout != expectedLayout)
            throw new InvalidDataException("The foundation output changed the raw disc layout.");
        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The foundation output changed the disc-image length.");
        DiscFileRecord baseWad = DiscImage.FindRootFileRecord(
            baseline,
            expectedLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (baseWad != outputWad || outputWad.Lba != WadLba || outputWad.Size != WadByteLength)
            throw new InvalidDataException("The foundation output moved or resized WAD.WAD.");

        byte[] outputModel = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            ModelWadOffset,
            ModelByteLength);
        if (!outputModel.SequenceEqual(construction.AfterModelBytes))
            throw new InvalidDataException("The foundation model failed exact byte-for-byte plan readback.");
        RequireHash(Hash(outputModel), AuthoredModelSha256, "foundation model readback");
        RequireHash(
            Hash(outputModel.AsSpan(AuthoredCollisionRelativeOffset, 0x5FAE8)),
            AuthoredCollisionSha256,
            "foundation collision readback");
        RequireHash(
            Hash(outputModel.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength)),
            AuthoredCollisionTreeSha256,
            "foundation collision tree readback");
        RequireHash(
            Hash(outputModel.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength)),
            AuthoredCollisionBlocksSha256,
            "foundation collision blocks readback");

        foreach (UnusedLevel65ConstructionDataSubfileLayout subfile in contract.DataSubfiles)
        {
            string actual = Hash(DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                subfile.WadOffset,
                subfile.ByteLength));
            string expected = subfile.Index == 1 ? AuthoredModelSha256 : subfile.Sha256;
            RequireHash(actual, expected, $"row-80 subfile {subfile.Index} readback");
        }
        VerifyOutputScaffolding(output, outputLayout, contract, outputModel);

        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            expectedLayout,
            outputWad,
            cancellationToken);
        if (logical.ChangedBytes <= 0 || logical.OutsideModelBytes != 0)
        {
            throw new InvalidDataException(
                $"The foundation logical diff changed {logical.ChangedBytes} WAD bytes, " +
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
                $"The foundation physical diff changed {physical.ChangedRawSectorCount} sectors; " +
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

    private static void ValidateConstructionGate(
        UnusedLevel65FullAuthoringConstructionContract contract,
        UnusedLevel65ConstructionCollidableTerrainPlan construction)
    {
        if (contract.ProfileId != UnusedLevel65FullAuthoringConstructionTemplate.ProfileId ||
            contract.BaselineImageSha256 != BaseImageSha256 ||
            contract.PromotionAuthorized ||
            contract.NormalCreateBinEnabled ||
            construction.ProfileId != UnusedLevel65FullAuthoringConstructionTemplate.ProfileId ||
            construction.BaselineImageSha256 != BaseImageSha256 ||
            construction.AfterModelSha256 != AuthoredModelSha256 ||
            construction.AfterCollisionSha256 != AuthoredCollisionSha256 ||
            construction.VisualTerrain.SectorIndex != 213 ||
            construction.VisualTerrain.AddedLowDetailVertexCount != 3 ||
            construction.VisualTerrain.AddedLowDetailFaceCount != 1 ||
            construction.VisualTerrain.AddedHighDetailVertexCount != 3 ||
            construction.VisualTerrain.AddedHighDetailFaceCount != 1 ||
            construction.CollisionBinding.ReusedTriangleIndex != 13_995 ||
            construction.NativeCollisionCellCount != 4_252 ||
            construction.AuthoredCollisionCellCount != 4_252 ||
            !construction.ChangedCollisionCells.SequenceEqual(new[]
            {
                new UnusedLevel65ConstructionCollisionCell(34, 35, 1),
                new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
                new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
            }) ||
            !construction.UnchangedCollisionCellSequencesPreserved ||
            !construction.IntendedCollisionCellSequencesVerified ||
            !construction.NativeCollisionOrderingPreserved ||
            !construction.ExposureProof.AuthoredSurfaceTopmostOverOpenInterior ||
            !construction.ExposureProof.CloseHighDetailFarLowDetailRuntimeGuideSupported ||
            !construction.CollisionIndexRepacked ||
            !construction.CollisionComposed ||
            !construction.OcclusionOwnershipVerified ||
            construction.DisposableRuntimeCandidateAuthorized ||
            construction.Publishable ||
            construction.PromotionAuthorized ||
            construction.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The exact static ID65 foundation construction gate is not satisfied.");
        }
        RequireHash(Hash(construction.AfterModelBytes), AuthoredModelSha256, "planned foundation model");
        RequireHash(
            Hash(construction.AfterModelBytes.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength)),
            AuthoredCollisionTreeSha256,
            "planned foundation collision tree");
        RequireHash(
            Hash(construction.AfterModelBytes.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength)),
            AuthoredCollisionBlocksSha256,
            "planned foundation collision blocks");
    }

    private static void ValidateConstructionDeterminism(
        UnusedLevel65ConstructionCollidableTerrainPlan first,
        UnusedLevel65ConstructionCollidableTerrainPlan second)
    {
        bool visualEqual =
            first.VisualTerrain.ProfileId == second.VisualTerrain.ProfileId &&
            first.VisualTerrain.BaselineImageSha256 == second.VisualTerrain.BaselineImageSha256 &&
            first.VisualTerrain.SectorIndex == second.VisualTerrain.SectorIndex &&
            first.VisualTerrain.AddedLowDetailVertexCount == second.VisualTerrain.AddedLowDetailVertexCount &&
            first.VisualTerrain.AddedLowDetailFaceCount == second.VisualTerrain.AddedLowDetailFaceCount &&
            first.VisualTerrain.AddedHighDetailVertexCount == second.VisualTerrain.AddedHighDetailVertexCount &&
            first.VisualTerrain.AddedHighDetailFaceCount == second.VisualTerrain.AddedHighDetailFaceCount &&
            first.VisualTerrain.Points.SequenceEqual(second.VisualTerrain.Points) &&
            first.VisualTerrain.SectorWadOffset == second.VisualTerrain.SectorWadOffset &&
            first.VisualTerrain.OldSectorByteLength == second.VisualTerrain.OldSectorByteLength &&
            first.VisualTerrain.NewSectorByteLength == second.VisualTerrain.NewSectorByteLength &&
            first.VisualTerrain.OldOcclusionWadOffset == second.VisualTerrain.OldOcclusionWadOffset &&
            first.VisualTerrain.NewOcclusionWadOffset == second.VisualTerrain.NewOcclusionWadOffset &&
            first.VisualTerrain.OldCollisionWadOffset == second.VisualTerrain.OldCollisionWadOffset &&
            first.VisualTerrain.NewCollisionWadOffset == second.VisualTerrain.NewCollisionWadOffset &&
            first.VisualTerrain.ZeroTailBytesAfter == second.VisualTerrain.ZeroTailBytesAfter &&
            first.VisualTerrain.BeforeModelSha256 == second.VisualTerrain.BeforeModelSha256 &&
            first.VisualTerrain.AfterModelSha256 == second.VisualTerrain.AfterModelSha256 &&
            first.VisualTerrain.AfterModelBytes.SequenceEqual(second.VisualTerrain.AfterModelBytes) &&
            first.VisualTerrain.HighAndLowDetailAllocated == second.VisualTerrain.HighAndLowDetailAllocated &&
            first.VisualTerrain.SectorCullBoundsPreserved == second.VisualTerrain.SectorCullBoundsPreserved &&
            first.VisualTerrain.OcclusionContainerPreserved == second.VisualTerrain.OcclusionContainerPreserved &&
            first.VisualTerrain.CollisionComposed == second.VisualTerrain.CollisionComposed &&
            first.VisualTerrain.Publishable == second.VisualTerrain.Publishable &&
            first.VisualTerrain.PromotionAuthorized == second.VisualTerrain.PromotionAuthorized &&
            first.VisualTerrain.NormalCreateBinEnabled == second.VisualTerrain.NormalCreateBinEnabled;
        bool collisionBindingEqual =
            first.CollisionBinding.ReusedTriangleIndex == second.CollisionBinding.ReusedTriangleIndex &&
            first.CollisionBinding.SourceTriangleWadOffset == second.CollisionBinding.SourceTriangleWadOffset &&
            first.CollisionBinding.AuthoredTriangleWadOffset == second.CollisionBinding.AuthoredTriangleWadOffset &&
            first.CollisionBinding.BeforeHex == second.CollisionBinding.BeforeHex &&
            first.CollisionBinding.AfterHex == second.CollisionBinding.AfterHex &&
            first.CollisionBinding.SourcePoints.SequenceEqual(second.CollisionBinding.SourcePoints) &&
            first.CollisionBinding.TargetPoints.SequenceEqual(second.CollisionBinding.TargetPoints) &&
            first.CollisionBinding.SourceAssignment == second.CollisionBinding.SourceAssignment &&
            first.CollisionBinding.TargetAssignment == second.CollisionBinding.TargetAssignment &&
            first.CollisionBinding.SourceLookupReferenceCount == second.CollisionBinding.SourceLookupReferenceCount &&
            first.CollisionBinding.AuthoredLookupReferenceCount == second.CollisionBinding.AuthoredLookupReferenceCount &&
            first.CollisionBinding.AuthoredLookupWadOffsets.SequenceEqual(second.CollisionBinding.AuthoredLookupWadOffsets) &&
            first.CollisionBinding.TargetCells.SequenceEqual(second.CollisionBinding.TargetCells) &&
            first.CollisionBinding.SourceWasZeroArea == second.CollisionBinding.SourceWasZeroArea &&
            first.CollisionBinding.UpwardWinding == second.CollisionBinding.UpwardWinding &&
            first.CollisionBinding.OrdinaryCollisionFlags == second.CollisionBinding.OrdinaryCollisionFlags;
        bool preservedDegeneratesEqual = first.PreservedDegenerateBindings.Count == second.PreservedDegenerateBindings.Count &&
            first.PreservedDegenerateBindings.Zip(second.PreservedDegenerateBindings).All(pair =>
                pair.First.TriangleIndex == pair.Second.TriangleIndex &&
                pair.First.TriangleHex == pair.Second.TriangleHex &&
                pair.First.Assignment == pair.Second.Assignment &&
                pair.First.CollisionFlags == pair.Second.CollisionFlags &&
                pair.First.NativeCells.SequenceEqual(pair.Second.NativeCells) &&
                pair.First.NativeLookupWadOffsets.SequenceEqual(pair.Second.NativeLookupWadOffsets) &&
                pair.First.AuthoredCells.SequenceEqual(pair.Second.AuthoredCells) &&
                pair.First.AuthoredLookupWadOffsets.SequenceEqual(pair.Second.AuthoredLookupWadOffsets));
        UnusedLevel65ConstructionExposureProof a = first.ExposureProof;
        UnusedLevel65ConstructionExposureProof b = second.ExposureProof;
        bool exposureEqual =
            a.ScannedSectorCount == b.ScannedSectorCount &&
            a.ScannedLowDetailFaceCount == b.ScannedLowDetailFaceCount &&
            a.ScannedHighDetailFaceCount == b.ScannedHighDetailFaceCount &&
            a.ScannedCollisionTriangleCount == b.ScannedCollisionTriangleCount &&
            a.NativeLowDetailInteriorOverlaps.SequenceEqual(b.NativeLowDetailInteriorOverlaps) &&
            a.NativeHighDetailInteriorOverlaps.SequenceEqual(b.NativeHighDetailInteriorOverlaps) &&
            a.NativeCollisionInteriorOverlaps.SequenceEqual(b.NativeCollisionInteriorOverlaps) &&
            a.NativeLowDetailTopmostAtAuthoredCentroid.SequenceEqual(b.NativeLowDetailTopmostAtAuthoredCentroid) &&
            a.NativeHighDetailTopmostAtAuthoredCentroid.SequenceEqual(b.NativeHighDetailTopmostAtAuthoredCentroid) &&
            a.AuthoredLowDetailFaceIndex == b.AuthoredLowDetailFaceIndex &&
            a.AuthoredHighDetailFaceIndex == b.AuthoredHighDetailFaceIndex &&
            a.ExactUnderlyingFoundationIdentified == b.ExactUnderlyingFoundationIdentified &&
            a.NoHigherNativeLowDetailFace == b.NoHigherNativeLowDetailFace &&
            a.NoHigherNativeHighDetailFace == b.NoHigherNativeHighDetailFace &&
            a.NoHigherNativeCollisionTriangle == b.NoHigherNativeCollisionTriangle &&
            a.AuthoredSurfaceTopmostOverOpenInterior == b.AuthoredSurfaceTopmostOverOpenInterior &&
            a.CloseHighDetailFarLowDetailRuntimeGuideSupported == b.CloseHighDetailFarLowDetailRuntimeGuideSupported;
        bool planEqual =
            first.ProfileId == second.ProfileId &&
            first.BaselineImageSha256 == second.BaselineImageSha256 &&
            first.CollisionTreeCapacityBytes == second.CollisionTreeCapacityBytes &&
            first.CollisionTreeUsedBytes == second.CollisionTreeUsedBytes &&
            first.CollisionBlocksCapacityBytes == second.CollisionBlocksCapacityBytes &&
            first.CollisionBlocksUsedBytes == second.CollisionBlocksUsedBytes &&
            first.BeforeCollisionSha256 == second.BeforeCollisionSha256 &&
            first.AfterCollisionSha256 == second.AfterCollisionSha256 &&
            first.AfterModelSha256 == second.AfterModelSha256 &&
            first.AfterModelBytes.SequenceEqual(second.AfterModelBytes) &&
            first.NativeCollisionCellCount == second.NativeCollisionCellCount &&
            first.AuthoredCollisionCellCount == second.AuthoredCollisionCellCount &&
            first.ChangedCollisionCells.SequenceEqual(second.ChangedCollisionCells) &&
            first.UnchangedCollisionCellSequencesPreserved == second.UnchangedCollisionCellSequencesPreserved &&
            first.IntendedCollisionCellSequencesVerified == second.IntendedCollisionCellSequencesVerified &&
            first.NativeCollisionOrderingPreserved == second.NativeCollisionOrderingPreserved &&
            first.OcclusionGroupCount == second.OcclusionGroupCount &&
            first.OcclusionAssignment == second.OcclusionAssignment &&
            first.AssignedOcclusionGroupContainsSector == second.AssignedOcclusionGroupContainsSector &&
            first.CollisionIndexRepacked == second.CollisionIndexRepacked &&
            first.CollisionComposed == second.CollisionComposed &&
            first.OcclusionOwnershipVerified == second.OcclusionOwnershipVerified &&
            first.DisposableRuntimeCandidateAuthorized == second.DisposableRuntimeCandidateAuthorized &&
            first.Publishable == second.Publishable &&
            first.PromotionAuthorized == second.PromotionAuthorized &&
            first.NormalCreateBinEnabled == second.NormalCreateBinEnabled;
        if (!visualEqual || !collisionBindingEqual || !preservedDegeneratesEqual || !exposureEqual || !planEqual)
        {
            throw new InvalidDataException(
                "Two independent foundation construction plans did not produce exact byte/record determinism.");
        }
    }

    private static bool IsExactDisposableFoundationRuntimeCandidateAuthorized(
        UnusedLevel65FullAuthoringConstructionContract contract,
        UnusedLevel65ConstructionCollidableTerrainPlan construction) =>
        contract.ProfileId == UnusedLevel65FullAuthoringConstructionTemplate.ProfileId &&
        contract.BaselineImageSha256 == BaseImageSha256 &&
        contract.DataSubfiles.Count == 8 &&
        contract.DataSubfiles.Single(subfile => subfile.Index == 1).Sha256 == ModelPreimageSha256 &&
        contract.DataSubfiles.Where(subfile => subfile.Index != 1)
            .All(subfile => subfile.MustRemainByteIdenticalInFirstTerrainGate) &&
        contract.Landing.WadOffset == 0x6B06800 &&
        contract.Landing.Sha256 ==
            "ac6446f7f11382b1f5d9c4f73c17281cda8db6251b14ace4e237c3c3d9f1896e" &&
        contract.PlayerAnchor.TrueIndex == 92 &&
        contract.PlayerAnchor.Sha256 ==
            "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2" &&
        contract.PlayerAnchor.RawX == contract.Landing.RawX &&
        contract.PlayerAnchor.RawY == contract.Landing.RawY &&
        contract.PlayerAnchor.RawZ - contract.Landing.RawZ == 154 &&
        contract.DisplayName.SlotIndex == 35 &&
        contract.DisplayName.PointerHex == "E4010180" &&
        contract.DisplayName.ResolvedString == "TOWN SQUARE" &&
        contract.Components.Single(component => component.Name == "cyclorama").Sha256 ==
            UnusedLevel65FullAuthoringConstructionTemplate.CycloramaComponentSha256 &&
        construction.AfterModelSha256 == AuthoredModelSha256 &&
        construction.AfterCollisionSha256 == AuthoredCollisionSha256 &&
        Hash(construction.AfterModelBytes.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength)) ==
            AuthoredCollisionTreeSha256 &&
        Hash(construction.AfterModelBytes.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength)) ==
            AuthoredCollisionBlocksSha256 &&
        construction.NativeCollisionCellCount == 4_252 &&
        construction.AuthoredCollisionCellCount == 4_252 &&
        construction.ChangedCollisionCells.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionCollisionCell(34, 35, 1),
            new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
            new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
        }) &&
        construction.ExposureProof.ExactUnderlyingFoundationIdentified &&
        construction.ExposureProof.AuthoredSurfaceTopmostOverOpenInterior &&
        construction.OcclusionOwnershipVerified &&
        !construction.DisposableRuntimeCandidateAuthorized &&
        !construction.Publishable &&
        !construction.PromotionAuthorized &&
        !construction.NormalCreateBinEnabled &&
        !contract.PromotionAuthorized &&
        !contract.NormalCreateBinEnabled;

    private static void VerifyOutputScaffolding(
        FileStream output,
        DiscLayout layout,
        UnusedLevel65FullAuthoringConstructionContract contract,
        byte[] outputModel)
    {
        byte[] landing = DiscImage.ReadFileBytes(
            output,
            layout,
            WadLba,
            contract.Landing.WadOffset,
            contract.Landing.ByteLength);
        if (!landing.SequenceEqual(Convert.FromHexString(contract.Landing.Hex)))
            throw new InvalidDataException("The foundation output changed the exact fly-in landing bytes.");
        RequireHash(Hash(landing), contract.Landing.Sha256, "foundation fly-in landing");

        byte[] playerAnchor = DiscImage.ReadFileBytes(
            output,
            layout,
            WadLba,
            contract.PlayerAnchor.WadOffset,
            contract.PlayerAnchor.ByteLength);
        RequireHash(Hash(playerAnchor), contract.PlayerAnchor.Sha256, "foundation T92 player anchor");
        int playerX = BitConverter.ToInt32(playerAnchor, 0x0C);
        int playerY = BitConverter.ToInt32(playerAnchor, 0x10);
        int playerZ = BitConverter.ToInt32(playerAnchor, 0x14);
        if (playerX != contract.Landing.RawX ||
            playerY != contract.Landing.RawY ||
            playerZ - contract.Landing.RawZ != 154)
        {
            throw new InvalidDataException("The foundation output changed the landing/T92 spatial invariant.");
        }

        const long authoredCycloramaWadOffset =
            UnusedLevel65FullAuthoringConstructionTemplate.CycloramaComponentWadOffset + 48;
        int cycloramaRelativeOffset = checked((int)(
            authoredCycloramaWadOffset - ModelWadOffset));
        RequireHash(
            Hash(outputModel.AsSpan(
                cycloramaRelativeOffset,
                UnusedLevel65FullAuthoringConstructionTemplate.CycloramaComponentByteLength)),
            UnusedLevel65FullAuthoringConstructionTemplate.CycloramaComponentSha256,
            "foundation relocated cyclorama");

        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            output,
            layout,
            name => string.Equals(name, contract.DisplayName.ExecutableName, StringComparison.OrdinalIgnoreCase));
        if (executable.Lba != contract.DisplayName.ExecutableLba ||
            executable.Size != contract.DisplayName.ExecutableByteLength)
        {
            throw new InvalidDataException("The foundation output moved the pinned executable.");
        }
        byte[] pointer = DiscImage.ReadFileBytes(
            output,
            layout,
            executable.Lba,
            contract.DisplayName.PointerFileOffset,
            4);
        if (!pointer.SequenceEqual(Convert.FromHexString(contract.DisplayName.PointerHex)))
            throw new InvalidDataException("The foundation output changed the slot-35 display-name pointer.");
        byte[] displayName = DiscImage.ReadFileBytes(
            output,
            layout,
            executable.Lba,
            contract.DisplayName.ResolvedStringFileOffset,
            Encoding.ASCII.GetByteCount(contract.DisplayName.ResolvedString) + 1);
        byte[] expectedName = Encoding.ASCII.GetBytes(contract.DisplayName.ResolvedString + "\0");
        if (!displayName.SequenceEqual(expectedName))
            throw new InvalidDataException("The foundation output changed the TOWN SQUARE display name.");
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
        List<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> diffs = [];
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
        IReadOnlyList<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff diff in diffs)
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        FoundationReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Full-Authoring Foundation — Disposable Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine(
            "This is the first static-composed 48-byte HP+LP foundation with native-ordered collision membership. " +
            "It is a disposable DuckStation discriminator, not normal Create BIN output.");
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
        builder.AppendLine($"- Authored model SHA-256: `{AuthoredModelSha256}`");
        builder.AppendLine($"- Authored collision SHA-256: `{AuthoredCollisionSha256}`");
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
        builder.AppendLine("2. Do not use a save state. Cold boot the candidate CUE and use a disposable memory card.");
        builder.AppendLine("3. Enter the ID65 load code once and confirm the Inventory still labels it `TOWN SQUARE`.");
        builder.AppendLine("4. Stop at the fly-in landing. Do not wander before locating the marked foundation triangle.");
        builder.AppendLine();
        builder.AppendLine("## Candidate gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] **Close HP visibility:** the new 45-degree triangular foundation is visible at close range as HP terrain.");
        builder.AppendLine("- [ ] **Solid surface / no hidden plane:** walking, charging, jumping, and landing follow the visible surface; Spyro never contacts a hidden flat plane below.");
        builder.AppendLine("- [ ] **45-degree walkability:** walk uphill and downhill in both directions and cross both edges without pass-through, snag, launch, or hover.");
        builder.AppendLine("- [ ] **Far LP visibility:** back the camera away along the entry route to camera depth >=2780; the same footprint remains visible at far range as LP terrain without reverting to the old plane.");
        builder.AppendLine("- [ ] Jump and land on the high tip. Spyro rests at the visible height with collision aligned to the authored slope.");
        builder.AppendLine("- [ ] Reset and repeat once without a save state; visibility and collision remain identical.");
        builder.AppendLine();
        builder.AppendLine("## Comparison gates");
        builder.AppendLine();
        builder.AppendLine("- [ ] Retail Town Square is byte-independent in behavior and has no added triangle.");
        builder.AppendLine("- [ ] Gnasty's Loot loads and plays normally.");
        builder.AppendLine("- [ ] Sunny Flight loads and plays normally.");
        builder.AppendLine();
        builder.AppendLine("## Report boundary");
        builder.AppendLine();
        builder.AppendLine(
            "Report each checkbox separately. A loading screen, still image, or one-direction traversal is not a full pass. " +
            "Do not use this candidate for normal editor output or release packaging.");
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
        "  <text x=\"60\" y=\"58\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"31\" font-weight=\"700\">ID65 Full-Authoring Foundation Runtime Location</text>\n" +
        "  <text x=\"60\" y=\"96\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\">Stop at the fly-in landing. Locate the isolated 45-degree triangle</text>\n" +
        "  <text x=\"60\" y=\"121\" fill=\"#b9c7e8\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"18\">over the entrance foundation before moving Spyro.</text>\n" +
        "  <rect x=\"60\" y=\"145\" width=\"1080\" height=\"440\" rx=\"22\" fill=\"#1b2437\" stroke=\"#6d7fa8\" stroke-width=\"3\"/>\n" +
        "  <circle cx=\"265\" cy=\"360\" r=\"34\" fill=\"#7f5cff\"/>\n" +
        "  <text x=\"155\" y=\"425\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"21\">SPAWN / fly-in landing</text>\n" +
        "  <path d=\"M320 360 L510 360\" stroke=\"#f8d05c\" stroke-width=\"12\" marker-end=\"url(#arrow)\"/>\n" +
        "  <defs><marker id=\"arrow\" markerWidth=\"12\" markerHeight=\"12\" refX=\"10\" refY=\"6\" orient=\"auto\"><path d=\"M0,0 L12,6 L0,12 Z\" fill=\"#f8d05c\"/></marker></defs>\n" +
        "  <polygon points=\"585,470 920,470 752,215\" fill=\"#4fd7a0\" stroke=\"#d9fff0\" stroke-width=\"5\"/>\n" +
        "  <text x=\"545\" y=\"506\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"20\" font-weight=\"700\">Authored 45-degree</text>\n" +
        "  <text x=\"545\" y=\"532\" fill=\"#ffffff\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"20\" font-weight=\"700\">HP + LP foundation</text>\n" +
        "  <text x=\"825\" y=\"506\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P1 (7762,6346,512)</text>\n" +
        "  <text x=\"825\" y=\"528\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P2 (7890,6346,512)</text>\n" +
        "  <text x=\"825\" y=\"550\" fill=\"#b9c7e8\" font-family=\"Menlo,monospace\" font-size=\"13\">P3 (7826,6474,640)</text>\n" +
        "  <text x=\"60\" y=\"615\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Close HP route: inspect and traverse the marked triangle from SPAWN.</text>\n" +
        "  <text x=\"60\" y=\"642\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Walk uphill, downhill, and across both edges; then jump and land on the high tip.</text>\n" +
        "  <text x=\"60\" y=\"677\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Far-LOD route: back the camera away along the entry route to camera depth &gt;= 2780.</text>\n" +
        "  <text x=\"60\" y=\"704\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Confirm the same LP footprint stays visible and never reverts to the old flat plane.</text>\n" +
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
        "  <text x=\"60\" y=\"976\" fill=\"#ffcf70\" font-family=\"Helvetica,Arial,sans-serif\" font-size=\"17\">Load the paired CUE. Cheats off; cold boot; no save state.</text>\n" +
        "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal? finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
    {
        string[] required =
        [
            Path.GetFileName(paths.OutputCuePath),
            Path.GetFileName(paths.LocationGuidePath),
            BaseImageSha256,
            AuthoredModelSha256,
            AuthoredCollisionSha256,
            "load the **CUE**, not the BIN",
            "HP terrain",
            "LP terrain",
            "hidden flat plane",
            "Retail Town Square",
            "Gnasty's Loot",
            "Sunny Flight"
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The foundation runtime checklist omitted an exact required gate.");
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
            "ID65 Full-Authoring Foundation Runtime Location",
            "SPAWN / fly-in landing",
            "Authored 45-degree",
            "HP + LP foundation",
            "7762,6346,512",
            "7890,6346,512",
            "7826,6474,640",
            "Load the paired CUE",
            "width=\"1200\" height=\"1000\"",
            "data-safe-left=\"60\" data-safe-right=\"1140\"",
            "Close HP route:",
            "Walk uphill, downhill, and across both edges",
            "Far-LOD route:",
            "camera depth &gt;= 2780",
            "Confirm the same LP footprint stays visible",
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
            throw new InvalidDataException("The foundation SVG location guide failed deterministic readback.");
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths,
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
                throw new IOException("The foundation operation backup path already exists.");
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
            throw new IOException("The staged foundation candidate disappeared before publication.");
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
                        throw new IOException("The previous foundation candidate backup is missing.");
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
        RejectReparsePoint(operationsDirectoryPath, "foundation operations directory");
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
                $"The foundation operations directory contains a non-owned entry: {operationRoot}");
        }
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale foundation operation");
        RejectReparsePoint(operationRoot, "stale foundation operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale foundation operation lease");
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
                throw new IOException("A foundation candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned foundation operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale foundation operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A foundation operation journal is unreadable.");
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
                "Multiple previous foundation candidate backups exist for the same publication target; " +
                "recovery was preserved for audit.");
        }
        if (committed.Length > 1)
        {
            throw new IOException(
                "Multiple committed foundation operations claim the same publication target; " +
                "recovery was preserved for audit.");
        }
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A foundation operation without a previous candidate owns an unexpected backup.");
            }
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp &&
                !operation.Journal.HadPreviousCandidate)
            {
                throw new InvalidDataException(
                    "A foundation operation reports a previous-candidate backup without a previous candidate.");
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
                    "A committed foundation operation is missing its candidate directory; " +
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
                    "A rollback-complete foundation operation still owns a backup; " +
                    "recovery was preserved for audit.");
            }
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
            {
                throw new IOException(
                    "A previous foundation candidate is missing and no unique backup can restore it; " +
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
                        "A stale foundation operation cannot prove whether the published directory is the " +
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
            RejectReparsePoint(entry, "stale foundation operation entry");
            string name = Path.GetFileName(entry);
            if (!allowedNames.Contains(name))
            {
                throw new InvalidDataException(
                    $"A stale foundation operation contains an unowned entry: {entry}");
            }
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
            {
                throw new InvalidDataException(
                    $"A stale foundation operation entry has the wrong filesystem role: {entry}");
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
            throw new InvalidDataException("A stale foundation operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale foundation journal points outside its publication parent.");
    }

    private static void VerifyPublishedCandidate(
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published foundation candidate directory is missing.");
        VerifyStagedCandidateArtifacts(
            paths.OutputDirectoryPath,
            paths,
            staged.FinderReveal != null);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        RequireHash(HashFile(paths.OutputImagePath), staged.Readback.OutputImageSha256, "published foundation BIN");
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
        VerifyLocationGuide(File.ReadAllText(paths.LocationGuidePath));
        if (finalReveal != null)
        {
            if (!OperatingSystem.IsMacOS() ||
                !File.Exists(paths.FinderHelperPath) ||
                File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
            {
                throw new InvalidDataException("The published foundation Finder helper is not exact 0755.");
            }
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths) =>
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
                "The foundation candidate directory already exists. " +
                "Set ReplaceExistingCandidate only for an intentional atomic replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
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
            throw new InvalidOperationException("The foundation operations directory escaped the output parent.");
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The foundation operations directory has no parent.");
        Directory.CreateDirectory(parent);
        string leasePath = Path.Combine(parent, GlobalWriterLeaseFileName);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "foundation global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The foundation writer lease path is a directory.");
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
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed foundation operation");
        if (!Path.GetFileName(operationRoot).StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-owned foundation operation directory.");
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
        UnusedLevel65FullAuthoringFoundationRuntimeCandidatePlan Plan,
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateReceipt Receipt,
        RuntimeCandidateFinderReveal? FinderReveal,
        FoundationReadback Readback,
        int RebuiltRawSectorCount);

    private sealed record FoundationReadback(
        string OutputImageSha256,
        string OutputDataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> RawSectorDiffs);

    private sealed record LogicalDiff(
        long ChangedBytes,
        long OutsideModelBytes,
        IReadOnlyList<int> ChangedRawSectorLbas);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
