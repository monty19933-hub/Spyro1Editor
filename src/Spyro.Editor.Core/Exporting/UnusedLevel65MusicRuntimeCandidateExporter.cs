using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65MusicRuntimeCandidateRequest(
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string RemoteBlankProofImagePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65MusicRuntimeCandidatePaths(
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

internal sealed record UnusedLevel65MusicRuntimeCandidateRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65MusicRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string ClosureProfileId,
    string BaseImageSha256,
    string BaseCueSha256,
    string RemoteBlankProofImageSha256,
    string SourceExecutableSha256,
    string OutputExecutableSha256,
    string SourceId65DataSha256,
    string OutputId65DataSha256,
    string SourceWadSha256,
    string OutputWadSha256,
    int ExecutableLba,
    int ExecutableByteLength,
    int ContinuousLevelIndex,
    int SelectedTrackId,
    string SelectedTrackName,
    IReadOnlyList<UnusedLevel65MusicClosurePatch> ClosurePatches,
    int LogicalPatchWindowBytes,
    int ChangedLogicalExecutableBytes,
    IReadOnlyList<int> AffectedRawSectorLbas,
    string RawSectorDiffSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool ExactClosureConsumed,
    bool RetailInitialAndLongPlayParityStaticallyVerified,
    bool Id65InitialAndLongPlaySlot35StaticallyVerified,
    bool OnlyExecutableBytesChanged,
    bool Id65DataPreserved,
    bool WadPreserved,
    bool PeteXaDirectoryAndAudioPreserved,
    bool Mode2EdcEccRebuilt,
    bool RequiresDuckStationRuntimeProof,
    bool DisposableRuntimeCandidateAuthorized,
    bool RuntimeProofComplete,
    bool AppEnabled,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65MusicRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string RemoteBlankProofImagePath,
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
    string RemoteBlankProofImageSha256,
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
    int ChangedLogicalExecutableBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactClosureConsumed,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FullImageReadbackVerified,
    bool FullScusReadbackVerified,
    bool Id65DataReadbackVerified,
    bool FullWadReadbackVerified,
    bool RetailParityStaticallyVerified,
    bool Id65Slot35StaticallyVerified,
    bool PeteXaDirectoryAndAudioPreserved,
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

internal sealed record UnusedLevel65MusicRuntimeCandidateResult(
    UnusedLevel65MusicRuntimeCandidatePaths Paths,
    UnusedLevel65MusicRuntimeCandidatePlan Plan,
    UnusedLevel65MusicRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal FinderReveal,
    string OutputImageSha256,
    string OutputExecutableSha256,
    string OutputId65DataSha256,
    string OutputWadSha256,
    int ChangedLogicalExecutableBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactClosureConsumed,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool FullImageReadbackVerified,
    bool FullScusReadbackVerified,
    bool Id65DataReadbackVerified,
    bool FullWadReadbackVerified,
    bool RetailParityStaticallyVerified,
    bool Id65Slot35StaticallyVerified,
    bool PeteXaDirectoryAndAudioPreserved,
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
/// Publishes one disposable, unpromoted ID65 music-ownership runtime candidate.
/// The writer consumes the exact static closure and changes only its five
/// guarded SCUS patches. WAD.WAD, PETEXA0-5, every retail level, the source
/// image, App integration, normal Create BIN, and release promotion remain
/// outside this writer's authority.
/// </summary>
internal static class UnusedLevel65MusicRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-music-slot35-town-square-runtime-candidate-usa-v1";
    public const string OutputDirectoryName =
        "unused-level-65-music-ownership-runtime-candidate";
    public const string OutputPrefix =
        "Unused-Level-65-Music-Ownership-Town-Square-Track26-RUNTIME-CANDIDATE";
    public const string ExpectedOutputImageSha256 =
        "facc61da6c1438d4abfd0ed43afd2e1805aafe07233ab60fc25eb0e38394347d";
    public const string ExpectedRawSectorDiffSha256 =
        "ea615011803acb8ae534a361eae56c2646e7c4b967fc0e7d73055f6c8d264a97";
    public const string ExpectedWadSha256 =
        "df09cdcfabd89eaad92deaab87ae467840330851906afa5924f19f36ced47bf8";
    public const string ExpectedFrozenReceiptSha256 =
        "8930b2118fd5f0142c63d81ecb58c007c35a5fa4646ca345d8bfa94621e73511";

    private const string BaseImageSha256 =
        UnusedLevel65MusicOwnershipClosure.LockedDisplayNameImageSha256;
    private const string BaseCueSha256 =
        "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2";
    private const string RemoteBlankProofImageSha256 =
        UnusedLevel65MusicOwnershipClosure.RemoteBlankImageSha256;
    private const string SourceExecutableSha256 =
        UnusedLevel65MusicOwnershipClosure.ExecutableSha256;
    private const string OutputExecutableSha256 =
        UnusedLevel65MusicOwnershipClosure.WitnessOutputExecutableSha256;
    private const string SourceId65DataSha256 =
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceDataSha256;
    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int OperationJournalSchemaVersion = 1;
    private const string OperationKind = "unused-level-65-music-runtime-candidate";
    private const string OperationDirectoryPrefix = "music-runtime-candidate-";
    private const string OperationJournalFileName = "operation-journal.json";
    private const string GlobalWriterLeaseFileName =
        ".unused-level-65-music-runtime-candidate-writer.lease";
    private const string OperationPhaseStaging = "staging";
    private const string OperationPhasePreviousBackedUp = "previous-candidate-backed-up";
    private const string OperationPhaseCandidatePublished = "candidate-published";
    private const string OperationPhaseNewCandidateRemoved = "new-candidate-removed";
    private const string OperationPhaseRollbackComplete = "rollback-complete";
    private const string OperationPhaseCommitted = "committed";
    private const string ActiveWriterLeaseMessage =
        "Another ID65 music runtime candidate writer is active for this publication parent.";
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
    private const int ExpectedChangedLogicalExecutableBytes = 40;
    private const int ExpectedLogicalPatchWindowBytes = 52;
    private const int ExpectedChangedRawSectorCount = 2;
    private const int ExpectedChangedPhysicalImageBytes = 260;
    private static readonly int[] ExpectedChangedRawSectorLbas = [55_438, 55_573];
    private static readonly UnusedLevel65MusicRuntimeCandidateRawSectorDiff[] ExpectedRawSectorDiffs =
    [
        new(55_438, 0, 0, 39, 4, 0, 78, 102, 223),
        new(55_573, 0, 0, 1, 4, 0, 10, 22, 37)
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UnusedLevel65MusicRuntimeCandidateResult> CreateAsync(
        UnusedLevel65MusicRuntimeCandidateRequest request,
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
        string remoteProof = RequireExistingFile(request.RemoteBlankProofImagePath, "exact remote-blank proof BIN");
        UnusedLevel65MusicRuntimeCandidatePaths paths = CreatePaths(request.OutputDirectoryPath);
        RejectUnsafePathTopologyBeforeMutation(root, baseImage, baseCue, remoteProof, paths);
        RequireSafeRoles(baseImage, baseCue, remoteProof, paths);
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
        RequireHash(await HashFileAsync(remoteProof, cancellationToken), RemoteBlankProofImageSha256, "remote-blank proof BIN");

        UnusedLevel65MusicOwnershipClosureContract closure =
            UnusedLevel65MusicOwnershipClosure.Inspect(root, baseImage, remoteProof);
        ValidateClosure(closure);
        UnusedLevel65MusicOwnershipClosureContract repeated =
            UnusedLevel65MusicOwnershipClosure.Inspect(root, baseImage, remoteProof);
        ValidateClosure(repeated);
        if (JsonSerializer.Serialize(closure, JsonOptions) != JsonSerializer.Serialize(repeated, JsonOptions))
            throw new InvalidDataException("Two independent music closure inspections were not deterministic.");
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"{OperationDirectoryPrefix}{Guid.NewGuid():N}");
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "music operation");
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

        UnusedLevel65MusicRuntimeCandidateResult? result = null;
        bool recoveryComplete = true;
        try
        {
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                root,
                baseImage,
                baseCue,
                remoteProof,
                paths,
                stagedDirectory,
                closure,
                loadCodes,
                rollbackRecoveryVerified,
                cancellationToken);
            request.TestStageHook?.Invoke("after-staged-candidate-verified");
            cancellationToken.ThrowIfCancellationRequested();
            BeginPublication(publication, request.ReplaceExistingCandidate, request.TestStageHook);
            VerifyPublishedCandidate(paths, staged, cancellationToken);
            RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "base BIN after publication");
            RequireHash(await HashFileAsync(baseCue, cancellationToken), BaseCueSha256, "base CUE after publication");
            RequireHash(await HashFileAsync(remoteProof, cancellationToken), RemoteBlankProofImageSha256, "proof BIN after publication");
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
            throw new IOException("The completed ID65 music operation directory could not be removed.");
        return result ?? throw new InvalidOperationException("The ID65 music writer completed without a result.");
    }

    internal static async Task<UnusedLevel65MusicRuntimeCandidateResult> VerifyPublishedAsync(
        UnusedLevel65MusicRuntimeCandidatePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths != CreatePaths(paths.OutputDirectoryPath))
            throw new InvalidDataException("The music verification paths are not the exact owned paths.");
        RejectPublicationPathAncestorReparsePoints(paths);
        RejectTreeReparsePoints(paths.OutputDirectoryPath, "published music candidate");
        VerifySevenFiles(paths.OutputDirectoryPath, paths);
        UnusedLevel65MusicRuntimeCandidateReceipt receipt =
            await ReadCanonicalJsonAsync<UnusedLevel65MusicRuntimeCandidateReceipt>(
                paths.StaticReadbackReceiptPath,
                "published music receipt",
                cancellationToken);
        ValidateReceiptIdentity(receipt, paths);
        bool frozenPublication = TryGetFrozenPublicationWorkspaceRoot(
            paths,
            out string frozenWorkspaceRoot);
        if (frozenPublication && !PathEquals(receipt.WorkspaceRoot, frozenWorkspaceRoot))
        {
            throw new InvalidDataException(
                "The frozen music receipt workspace root does not match its owned _local publication path.");
        }
        string root = receipt.WorkspaceRoot;
        string baseImage = RequireExistingFile(receipt.BaseImagePath, "receipt-bound base BIN");
        string baseCue = RequireExistingFile(receipt.BaseCuePath, "receipt-bound base CUE");
        string remoteProof = RequireExistingFile(receipt.RemoteBlankProofImagePath, "receipt-bound proof BIN");
        RejectUnsafePathTopologyBeforeMutation(root, baseImage, baseCue, remoteProof, paths);
        RequireSafeRoles(baseImage, baseCue, remoteProof, paths);
        RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "receipt-bound base BIN");
        RequireHash(await HashFileAsync(baseCue, cancellationToken), BaseCueSha256, "receipt-bound base CUE");
        RequireHash(await HashFileAsync(remoteProof, cancellationToken), RemoteBlankProofImageSha256, "receipt-bound proof BIN");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.OutputCuePath, paths.OutputImagePath, "MODE2/2352");

        UnusedLevel65MusicOwnershipClosureContract closure =
            UnusedLevel65MusicOwnershipClosure.Inspect(root, baseImage, remoteProof);
        ValidateClosure(closure);
        CandidateReadback readback = await VerifyImageReadbackAsync(
            baseImage,
            paths.OutputImagePath,
            closure,
            cancellationToken);
        ValidateReceiptReadback(receipt, readback);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        UnusedLevel65MusicRuntimeCandidatePlan expectedPlan = BuildPlan(
            closure,
            receipt.BaseCueSha256,
            readback,
            loadCodes);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            JsonSerializer.Serialize(expectedPlan, JsonOptions) + "\n",
            "published music plan");
        VerifyTextReadback(
            paths.OutputCuePath,
            DiscImage.BuildCueText(baseCue, Path.GetFileName(paths.OutputImagePath)),
            "published music CUE");
        RuntimeCandidateFinderReveal reveal = BuildFinalFinderReveal(paths);
        string checklist = BuildRuntimeChecklist(paths, reveal, loadCodes, readback);
        VerifyTextReadback(paths.RuntimeChecklistPath, checklist, "published music checklist");
        VerifyChecklist(checklist, reveal, loadCodes, paths);
        string guide = BuildLocationGuideSvg(loadCodes);
        VerifyTextReadback(paths.LocationGuidePath, guide, "published music guide");
        VerifyLocationGuide(guide);
        VerifyFinderHelper(paths, receipt.FinderHelperSha256);
        BindSidecarHashes(receipt, paths);
        UnusedLevel65MusicRuntimeCandidateReceipt expectedReceipt = BuildReceipt(
            root,
            baseImage,
            baseCue,
            remoteProof,
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
            "published canonical clean music receipt");
        string canonicalReceiptSha256 = Hash(Encoding.UTF8.GetBytes(expectedReceiptText));
        if (frozenPublication)
        {
            RequireHash(
                canonicalReceiptSha256,
                ExpectedFrozenReceiptSha256,
                "frozen canonical clean music receipt");
        }

        StagedCandidate staged = new(expectedPlan, receipt, reveal, readback, receipt.RebuiltRawSectorCount);
        return BuildResult(paths, staged, reveal, rollbackRecoveryVerified: false);
    }

    public static UnusedLevel65MusicRuntimeCandidatePaths CreatePaths(string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string outputDirectory = Path.GetFullPath(outputDirectoryPath);
        string filesystemRoot = Path.GetPathRoot(outputDirectory) ?? "";
        if (PathEquals(outputDirectory, filesystemRoot) ||
            PathEquals(outputDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException("The music candidate output cannot be a filesystem or home root.");
        }
        if (!string.Equals(Path.GetFileName(outputDirectory), OutputDirectoryName, StringComparison.Ordinal))
            throw new InvalidOperationException($"The music writer owns only a directory named '{OutputDirectoryName}'.");
        string parent = Path.GetDirectoryName(outputDirectory) ??
            throw new InvalidOperationException("The music candidate output has no parent.");
        string prefix = Path.Combine(outputDirectory, OutputPrefix);
        UnusedLevel65MusicRuntimeCandidatePaths paths = new(
            outputDirectory,
            prefix,
            prefix + ".bin",
            prefix + ".cue",
            prefix + "-construction-plan.json",
            prefix + "-static-readback-receipt.json",
            prefix + "-runtime-checklist.md",
            prefix + "-location-guide.svg",
            prefix + "-Reveal-in-Finder.command",
            Path.Combine(parent, ".unused-level-65-music-runtime-candidate-operations"));
        RequirePathsInsideOutput(paths);
        return paths;
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        string remoteProof,
        UnusedLevel65MusicRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        UnusedLevel65MusicOwnershipClosureContract closure,
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
        RequireMode2Layout(layout, "staged music candidate");

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
            byte[] sourceExecutable = DiscImage.ReadFileBytes(
                image,
                layout,
                executableRecord.Lba,
                0,
                executableRecord.Size);
            RequireHash(Hash(sourceExecutable), SourceExecutableSha256, "staged source SCUS");
            byte[] outputExecutable = ApplyClosurePatches(sourceExecutable, closure.WitnessPatches, reverse: false);
            RequireHash(Hash(outputExecutable), OutputExecutableSha256, "planned output SCUS");

            foreach (UnusedLevel65MusicClosurePatch patch in closure.WitnessPatches)
            {
                DiscImage.WriteFileBytes(
                    image,
                    layout,
                    executableRecord.Lba,
                    patch.FileOffset,
                    outputExecutable.AsSpan(patch.FileOffset, patch.ByteLength).ToArray());
            }
            IReadOnlyList<(long Offset, int ByteLength)> patchRanges = closure.WitnessPatches
                .Select(patch => ((long)patch.FileOffset, patch.ByteLength))
                .ToArray();
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                executableRecord.Lba,
                patchRanges);
            int verifiedRawSectorCount = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                ExpectedChangedRawSectorLbas.Select(lba => (lba, 1)));
            if (rebuiltRawSectorCount != ExpectedChangedRawSectorCount ||
                verifiedRawSectorCount != ExpectedChangedRawSectorCount)
            {
                throw new InvalidDataException(
                    $"The music transaction rebuilt/verified {rebuiltRawSectorCount}/{verifiedRawSectorCount} sectors, expected 2/2.");
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
            closure,
            cancellationToken);
        RequireHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "music output BIN");
        RequireHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "music raw-sector diff");

        string baseCueHash = await HashFileAsync(baseCue, cancellationToken);
        UnusedLevel65MusicRuntimeCandidatePlan plan = BuildPlan(
            closure,
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
        UnusedLevel65MusicRuntimeCandidateReceipt receipt = BuildReceipt(
            workspaceRoot,
            baseImage,
            baseCue,
            remoteProof,
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
        VerifyTextReadback(stagedPlan, JsonSerializer.Serialize(plan, JsonOptions) + "\n", "staged music plan");
        VerifyTextReadback(stagedReceipt, JsonSerializer.Serialize(receipt, JsonOptions) + "\n", "staged music receipt");
        VerifyChecklist(checklist, finalReveal, loadCodes, finalPaths);
        VerifyLocationGuide(guide);
        return new(plan, receipt, stagedReveal, readback, rebuiltRawSectorCount);
    }

    private static UnusedLevel65MusicRuntimeCandidateReceipt BuildReceipt(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        string remoteProof,
        UnusedLevel65MusicRuntimeCandidatePaths finalPaths,
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
            remoteProof,
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
            RemoteBlankProofImageSha256,
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
            readback.ChangedLogicalExecutableBytes,
            readback.ChangedPhysicalImageBytes,
            rebuiltRawSectorCount,
            readback.RawSectorDiffs.Count,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            ExactClosureConsumed: true,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FullImageReadbackVerified: true,
            FullScusReadbackVerified: true,
            Id65DataReadbackVerified: true,
            FullWadReadbackVerified: true,
            RetailParityStaticallyVerified: true,
            Id65Slot35StaticallyVerified: true,
            PeteXaDirectoryAndAudioPreserved: true,
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
        UnusedLevel65MusicOwnershipClosureContract closure,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        RequireMode2Layout(baseLayout, "music base image");
        RequireMode2Layout(outputLayout, "music output image");
        if (baseLayout != outputLayout)
            throw new InvalidDataException("The music output changed the exact disc layout.");

        await using FileStream baseline = File.OpenRead(baseImagePath);
        await using FileStream output = File.OpenRead(outputImagePath);
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The music output changed the image length.");
        DiscFileRecord baseExe = RequireRootFile(
            baseline, baseLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        DiscFileRecord outputExeRecord = RequireRootFile(
            output, outputLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        if (baseExe != outputExeRecord)
            throw new InvalidDataException("The music transaction moved or resized SCUS_942.28.");
        DiscFileRecord baseWad = RequireRootFile(
            baseline, baseLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord outputWad = RequireRootFile(
            output, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        if (baseWad != outputWad)
            throw new InvalidDataException("The music transaction moved or resized WAD.WAD.");
        foreach (UnusedLevel65MusicClosureFile peteXa in closure.PeteXaFiles)
        {
            DiscFileRecord baseRecord = RequireRootFile(
                baseline, baseLayout, peteXa.Name, peteXa.Lba, peteXa.ByteLength);
            DiscFileRecord outputRecord = RequireRootFile(
                output, outputLayout, peteXa.Name, peteXa.Lba, peteXa.ByteLength);
            if (baseRecord != outputRecord)
                throw new InvalidDataException($"The music transaction changed {peteXa.Name}'s directory record.");
        }

        byte[] sourceExecutable = DiscImage.ReadFileBytes(
            baseline, baseLayout, ExecutableLba, 0, ExecutableByteLength);
        byte[] outputExecutable = DiscImage.ReadFileBytes(
            output, outputLayout, ExecutableLba, 0, ExecutableByteLength);
        RequireHash(Hash(sourceExecutable), SourceExecutableSha256, "source SCUS readback");
        RequireHash(Hash(outputExecutable), OutputExecutableSha256, "output SCUS readback");
        byte[] expectedExecutable = ApplyClosurePatches(sourceExecutable, closure.WitnessPatches, reverse: false);
        byte[] inverseExecutable = ApplyClosurePatches(outputExecutable, closure.WitnessPatches, reverse: true);
        if (!outputExecutable.SequenceEqual(expectedExecutable) || !inverseExecutable.SequenceEqual(sourceExecutable))
            throw new InvalidDataException("The music output lost exact forward/inverse closure identity.");
        int changedLogicalBytes = CompareExecutableLogicalDiff(
            sourceExecutable,
            outputExecutable,
            closure.WitnessPatches);

        byte[] sourceData = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, DataWadOffset, DataByteLength);
        byte[] outputData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, DataWadOffset, DataByteLength);
        string sourceDataHash = Hash(sourceData);
        string outputDataHash = Hash(outputData);
        RequireHash(sourceDataHash, SourceId65DataSha256, "source ID65 data readback");
        RequireHash(outputDataHash, SourceId65DataSha256, "output ID65 data readback");
        if (!sourceData.SequenceEqual(outputData))
            throw new InvalidDataException("The SCUS-only music transaction changed ID65 data.");

        string sourceWadHash = HashDiscFile(
            baseline, baseLayout, baseWad, cancellationToken);
        string outputWadHash = HashDiscFile(
            output, outputLayout, outputWad, cancellationToken);
        if (sourceWadHash != outputWadHash)
            throw new InvalidDataException("The SCUS-only music transaction changed WAD.WAD.");
        RequireHash(sourceWadHash, ExpectedWadSha256, "source WAD.WAD readback");
        RequireHash(outputWadHash, ExpectedWadSha256, "output WAD.WAD readback");

        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            ExpectedChangedRawSectorLbas,
            cancellationToken);
        if (physical.ChangedBytes != ExpectedChangedPhysicalImageBytes ||
            physical.RawSectorDiffs.Count != ExpectedChangedRawSectorCount ||
            !physical.RawSectorDiffs.SequenceEqual(ExpectedRawSectorDiffs) ||
            physical.RawSectorDiffs.Any(diff =>
                diff.HeaderChangedBytes != 0 ||
                diff.SubheaderChangedBytes != 0 ||
                diff.ReservedChangedBytes != 0 ||
                diff.PayloadChangedBytes == 0))
        {
            throw new InvalidDataException("The music physical diff escaped exact MODE2 payload/integrity fields.");
        }
        VerifyMode2Form1Classification(output, ExpectedChangedRawSectorLbas);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            ExpectedChangedRawSectorLbas.Select(lba => (lba, 1)));
        if (verified != ExpectedChangedRawSectorCount)
            throw new InvalidDataException("The music output failed exact rebuilt-sector integrity verification.");
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
            changedLogicalBytes,
            physical.ChangedBytes,
            physical.RawSectorDiffs,
            rawDiffHash);
    }

    private static UnusedLevel65MusicRuntimeCandidatePlan BuildPlan(
        UnusedLevel65MusicOwnershipClosureContract closure,
        string baseCueSha256,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes) =>
        new(
            PlanSchemaVersion,
            ProfileId,
            closure.ProfileId,
            BaseImageSha256,
            baseCueSha256,
            RemoteBlankProofImageSha256,
            readback.SourceExecutableSha256,
            readback.OutputExecutableSha256,
            readback.SourceId65DataSha256,
            readback.OutputId65DataSha256,
            readback.SourceWadSha256,
            readback.OutputWadSha256,
            closure.Executable.Lba,
            closure.Executable.ByteLength,
            closure.ContinuousLevelIndex,
            closure.WitnessTrackId,
            closure.WitnessTrackName,
            closure.WitnessPatches,
            closure.Mode2Impact.LogicalPatchWindowBytes,
            readback.ChangedLogicalExecutableBytes,
            readback.RawSectorDiffs.Select(diff => diff.RawSectorLba).ToArray(),
            readback.RawSectorDiffSha256,
            loadCodes,
            ExactClosureConsumed: true,
            RetailInitialAndLongPlayParityStaticallyVerified: true,
            Id65InitialAndLongPlaySlot35StaticallyVerified: true,
            OnlyExecutableBytesChanged: true,
            Id65DataPreserved: true,
            WadPreserved: true,
            PeteXaDirectoryAndAudioPreserved: true,
            Mode2EdcEccRebuilt: true,
            RequiresDuckStationRuntimeProof: true,
            DisposableRuntimeCandidateAuthorized: true,
            RuntimeProofComplete: false,
            AppEnabled: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);

    private static void ValidateClosure(UnusedLevel65MusicOwnershipClosureContract closure)
    {
        if (closure.ProfileId != UnusedLevel65MusicOwnershipClosure.ProfileId ||
            closure.LockedDisplayNameImageSha256 != BaseImageSha256 ||
            closure.RemoteBlankImageSha256 != RemoteBlankProofImageSha256 ||
            closure.ExecutableSha256 != SourceExecutableSha256 ||
            closure.OutputExecutableSha256 != OutputExecutableSha256 ||
            closure.RemoteBlankOutputDataSha256 !=
                UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256 ||
            closure.Executable.Name != "SCUS_942.28" ||
            closure.Executable.Lba != ExecutableLba ||
            closure.Executable.ByteLength != ExecutableByteLength ||
            closure.ContinuousLevelIndex != 35 ||
            closure.WitnessTrackId != 26 ||
            closure.WitnessTrackName != "Town Square" ||
            closure.WitnessPatches.Count != 5 ||
            !closure.WitnessPatches.Select(patch => patch.FileOffset)
                .SequenceEqual(new[] { 0x1C57C, 0x1C584, 0x1C594, 0x1C5D8, 0x5F828 }) ||
            !closure.WitnessPatches.Select(patch => patch.ByteLength)
                .SequenceEqual(new[] { 4, 4, 32, 8, 4 }) ||
            closure.WitnessPatches.Sum(patch => patch.ByteLength) != ExpectedLogicalPatchWindowBytes ||
            closure.WitnessPatches.Sum(patch => patch.ChangedByteCount) != ExpectedChangedLogicalExecutableBytes ||
            !closure.WitnessPatches.Select(patch => patch.RawSectorLba).Distinct().Order()
                .SequenceEqual(ExpectedChangedRawSectorLbas) ||
            closure.Mode2Impact.LogicalPatchWindowBytes != ExpectedLogicalPatchWindowBytes ||
            closure.Mode2Impact.ChangedLogicalBytes != ExpectedChangedLogicalExecutableBytes ||
            closure.Mode2Impact.MinimumRawSectorCount != ExpectedChangedRawSectorCount ||
            !closure.Mode2Impact.AffectedRawSectorLbas.SequenceEqual(ExpectedChangedRawSectorLbas) ||
            !closure.Mode2Impact.AllAffectedSectorsAreMode2Form1 ||
            !closure.Mode2Impact.EdcEccRebuildRequired ||
            !closure.Mode2Impact.FileExtentUnchanged ||
            !closure.Mode2Impact.IsoDirectoryUnchanged ||
            !closure.Mode2Impact.XaAudioSectorsUnchanged ||
            closure.SelectableTrackProofs.Count != 46 ||
            !closure.SelectableTrackProofs.All(proof =>
                proof.Selectable && proof.InitialAndLongPlayMatch &&
                proof.InitialResolvedTrackId == proof.TrackId &&
                proof.LongPlayResolvedTrackIds.SequenceEqual(new[] { proof.TrackId, proof.TrackId, proof.TrackId })) ||
            !closure.ReservedTrackIds.SequenceEqual(new[] { 30, 35 }) ||
            !closure.LockedAndRemoteExecutablesIdentical ||
            !closure.RemoteBlankOutputMatchesStaticPlan ||
            !closure.RemoteBlankConstructionExcludesExecutable ||
            !closure.InitialSlotIndependent ||
            !closure.NoLateTableExtension ||
            !closure.PeteXaLbaTablePreserved ||
            !closure.PeteXaDirectoryPreserved ||
            !closure.RandomCalleePreservesT0AndT1 ||
            !closure.RetailInitialAndLongPlayOutcomesPreserved ||
            !closure.Id65InitialAndLongPlayResolveThroughSlot35 ||
            !closure.AllSelectableBuiltInTracksStaticallyClosed ||
            !closure.MinimumTwoSectorTransactionAchieved ||
            !closure.StaticTransactionClosed ||
            !closure.GenericLevelMusicExporterStillRejectsId65 ||
            closure.WritesBin || closure.WritesCue || closure.WriterAuthorized ||
            closure.RuntimeProofComplete || closure.AppEnabled ||
            closure.NormalCreateBinEnabled || closure.PromotionAuthorized)
        {
            throw new InvalidDataException("The exact fail-closed music ownership closure is not satisfied.");
        }
    }

    private static byte[] ApplyClosurePatches(
        byte[] executable,
        IReadOnlyList<UnusedLevel65MusicClosurePatch> patches,
        bool reverse)
    {
        byte[] output = executable.ToArray();
        foreach (UnusedLevel65MusicClosurePatch patch in patches)
        {
            byte[] before = Convert.FromHexString(reverse ? patch.AfterHex : patch.BeforeHex);
            byte[] after = Convert.FromHexString(reverse ? patch.BeforeHex : patch.AfterHex);
            if (before.Length != patch.ByteLength || after.Length != patch.ByteLength ||
                patch.FileOffset < 0 || patch.FileOffset + patch.ByteLength > output.Length)
            {
                throw new InvalidDataException($"Closure patch {patch.Name} has an invalid executable range.");
            }
            if (!output.AsSpan(patch.FileOffset, patch.ByteLength).SequenceEqual(before))
                throw new InvalidDataException($"Closure patch {patch.Name} failed its exact preimage guard.");
            after.CopyTo(output, patch.FileOffset);
        }
        return output;
    }

    private static int CompareExecutableLogicalDiff(
        ReadOnlySpan<byte> baseline,
        ReadOnlySpan<byte> output,
        IReadOnlyList<UnusedLevel65MusicClosurePatch> patches)
    {
        if (baseline.Length != output.Length || baseline.Length != ExecutableByteLength)
            throw new InvalidDataException("The SCUS logical comparison has incompatible lengths.");
        Dictionary<int, (byte Before, byte After)> allowed = [];
        foreach (UnusedLevel65MusicClosurePatch patch in patches)
        {
            byte[] before = Convert.FromHexString(patch.BeforeHex);
            byte[] after = Convert.FromHexString(patch.AfterHex);
            for (int index = 0; index < patch.ByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                int absolute = checked(patch.FileOffset + index);
                if (!allowed.TryAdd(absolute, (before[index], after[index])))
                    throw new InvalidDataException("The closure contains overlapping changed SCUS bytes.");
            }
        }
        int changed = 0;
        for (int index = 0; index < baseline.Length; index++)
        {
            if (baseline[index] == output[index])
                continue;
            changed++;
            if (!allowed.TryGetValue(index, out (byte Before, byte After) expected) ||
                baseline[index] != expected.Before || output[index] != expected.After)
            {
                throw new InvalidDataException($"The music transaction changed an unauthorized SCUS byte at 0x{index:X}.");
            }
        }
        if (changed != ExpectedChangedLogicalExecutableBytes || changed != allowed.Count)
            throw new InvalidDataException($"The music SCUS diff changed {changed} bytes, expected 40.");
        return changed;
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
            throw new InvalidDataException("The music physical image comparison has incompatible lengths.");
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        List<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> diffs = [];
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
            ReadRawSectorExactly(baseline, before, lba, "music baseline");
            ReadRawSectorExactly(output, after, lba, "music output");
            if (before.AsSpan().SequenceEqual(after))
                continue;
            if (!allowed.Contains(lba))
                throw new InvalidDataException($"Physical image bytes changed outside the two SCUS sectors at LBA {lba}.");

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
            throw new InvalidDataException("The physical changed-sector set does not exactly equal the closure sector set.");
        return new(changedBytes, diffs);
    }

    private static void VerifyMode2Form1Classification(
        FileStream output,
        IReadOnlyList<int> changedRawSectorLbas)
    {
        byte[] raw = new byte[RawSectorByteLength];
        foreach (int lba in changedRawSectorLbas)
        {
            ReadRawSectorExactly(output, raw, lba, "music output");
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
        IReadOnlyList<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> diffs)
    {
        StringBuilder builder = new();
        foreach (UnusedLevel65MusicRuntimeCandidateRawSectorDiff diff in diffs)
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
        UnusedLevel65MusicRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        CandidateReadback readback)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Music Ownership Runtime Checklist");
        builder.AppendLine();
        builder.AppendLine("Disposable candidate only — runtime pending, unpromoted, and excluded from App/Create BIN/release.");
        builder.AppendLine();
        builder.AppendLine("## Frozen candidate");
        builder.AppendLine();
        builder.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`");
        builder.AppendLine($"- SCUS SHA-256: `{readback.OutputExecutableSha256}`");
        builder.AppendLine($"- ID65 DATA SHA-256 (preserved): `{readback.OutputId65DataSha256}`");
        builder.AppendLine($"- WAD.WAD SHA-256 (preserved): `{readback.OutputWadSha256}`");
        builder.AppendLine($"- Raw-sector diff SHA-256: `{readback.RawSectorDiffSha256}`");
        builder.AppendLine("- Exact write boundary: 40 logical SCUS bytes in raw LBAs 55438 and 55573; MODE2 EDC/ECC rebuilt.");
        builder.AppendLine("- Selected ID65 track: Town Square, native track ID 26, initial slot 35 and long-play slot-35 bridge.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, finderReveal);
        builder.AppendLine("## Emulator isolation — required");
        builder.AppendLine();
        builder.AppendLine("- Use the isolated DuckStation profile only.");
        builder.AppendLine("- Memory Card 1: None.");
        builder.AppendLine("- Memory Card 2: None.");
        builder.AppendLine("- Do not save, load, create, or format a memory card.");
        builder.AppendLine("- Load the CUE, never the BIN. Do not use the retired positive-winding/foundation candidates.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## ID65 acceptance");
        builder.AppendLine();
        builder.AppendLine("1. Cold boot the exact CUE, load ID65, and confirm Town Square music begins immediately.");
        builder.AppendLine("2. Hold controllable ID65 for at least 12 minutes; music must remain Town Square with no revert, silence, hang, or XA corruption.");
        builder.AppendLine("3. Repeat the 12-minute cold run three times to exercise all native modulo-3 late choices; every run must remain Town Square.");
        builder.AppendLine("4. Pause/unpause, die/respawn once, reset, and re-enter ID65; the owned track must restart/remain correctly each time.");
        builder.AppendLine();
        builder.AppendLine("## Retail controls");
        builder.AppendLine();
        builder.AppendLine("Cold boot each comparison code separately. Town Square, Gnasty's Loot, and Sunny Flight must retain their locked-image initial and long-play outcomes. Record audio/video and elapsed time.");
        builder.AppendLine();
        builder.AppendLine("## Promotion boundary");
        builder.AppendLine();
        builder.AppendLine("Runtime proof is incomplete. Do not copy this transaction into the App, normal Create BIN, or release paths until every ID65 and retail control gate passes and a separate promotion is authorized.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        VerifyLoadCodes(loadCodes);
        return """
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1000" viewBox="0 0 1200 1000">
  <rect width="1200" height="1000" fill="#07101f"/>
  <rect x="44" y="40" width="1112" height="920" rx="28" fill="#101c32" stroke="#62d9ff" stroke-width="3"/>
  <text x="82" y="102" fill="#f8fbff" font-family="Helvetica,Arial,sans-serif" font-size="38" font-weight="700">ID65 MUSIC OWNERSHIP — DISPOSABLE RUNTIME GATE</text>
  <text x="82" y="143" fill="#9fe8ff" font-family="Helvetica,Arial,sans-serif" font-size="22">Town Square • native track 26 • slot 35 initial + long-play bridge</text>
  <rect x="82" y="178" width="1036" height="126" rx="18" fill="#172845" stroke="#ffcf5a" stroke-width="2"/>
  <text x="112" y="221" fill="#ffdf83" font-family="Helvetica,Arial,sans-serif" font-size="25" font-weight="700">NO-CARD / NO-SAVE ISOLATION</text>
  <text x="112" y="260" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Memory Card 1: None   •   Memory Card 2: None   •   Do not save, load, create, or format</text>
  <text x="112" y="289" fill="#b9cbe9" font-family="Helvetica,Arial,sans-serif" font-size="19">Load the CUE, never the BIN. Use only the isolated DuckStation profile.</text>
  <text x="82" y="359" fill="#62d9ff" font-family="Helvetica,Arial,sans-serif" font-size="28" font-weight="700">ID65 acceptance</text>
  <circle cx="105" cy="406" r="18" fill="#62d9ff"/><text x="99" y="414" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">1</text>
  <text x="143" y="414" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Cold boot ID65: Town Square music begins immediately.</text>
  <circle cx="105" cy="462" r="18" fill="#62d9ff"/><text x="99" y="470" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">2</text>
  <text x="143" y="470" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Hold at least 12 minutes; no revert, silence, hang, or XA corruption.</text>
  <circle cx="105" cy="518" r="18" fill="#62d9ff"/><text x="99" y="526" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">3</text>
  <text x="143" y="526" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Repeat three cold long-play runs for all modulo-3 late choices.</text>
  <circle cx="105" cy="574" r="18" fill="#62d9ff"/><text x="99" y="582" fill="#07101f" font-family="Helvetica,Arial,sans-serif" font-size="20" font-weight="700">4</text>
  <text x="143" y="582" fill="#ffffff" font-family="Helvetica,Arial,sans-serif" font-size="22">Pause, respawn, reset, re-enter: track ownership remains correct.</text>
  <text x="82" y="650" fill="#62d9ff" font-family="Helvetica,Arial,sans-serif" font-size="28" font-weight="700">Exact comparison codes — separate cold runs</text>
  <rect x="82" y="678" width="1036" height="156" rx="16" fill="#0b1629" stroke="#375a86" stroke-width="2"/>
  <text x="110" y="718" fill="#ffffff" font-family="Menlo,monospace" font-size="19">ID65 candidate       • Level 65 • Left, then Down</text>
  <text x="110" y="752" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Retail Town Square   • Level 13 • exact catalog selection</text>
  <text x="110" y="786" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Gnasty's Loot        • Level 64 • exact catalog selection</text>
  <text x="110" y="820" fill="#ffffff" font-family="Menlo,monospace" font-size="19">Sunny Flight         • Level 15 • exact catalog selection</text>
  <rect x="82" y="866" width="1036" height="62" rx="14" fill="#481b2a" stroke="#ff6d91" stroke-width="2"/>
  <text x="110" y="905" fill="#ffd7e1" font-family="Helvetica,Arial,sans-serif" font-size="22" font-weight="700">RUNTIME PENDING • UNPROMOTED • NO APP / CREATE BIN / RELEASE INTEGRATION</text>
</svg>
""";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal reveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, reveal, loadCodes);
        string[] required =
        [
            "Memory Card 1: None",
            "Memory Card 2: None",
            "Do not save, load, create, or format",
            "at least 12 minutes",
            "Repeat the 12-minute cold run three times",
            "Runtime proof is incomplete",
            "normal Create BIN",
            Path.GetFileName(paths.OutputCuePath)
        ];
        if (required.Any(text => !checklist.Contains(text, StringComparison.Ordinal)))
            throw new InvalidDataException("The music runtime checklist lost a required no-card or acceptance gate.");
    }

    private static void VerifyLocationGuide(string svg)
    {
        string[] required =
        [
            "width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\"",
            "Memory Card 1: None",
            "Memory Card 2: None",
            "Do not save, load, create, or format",
            "Hold at least 12 minutes",
            "Repeat three cold long-play runs",
            "Level 65",
            "Level 13",
            "Level 64",
            "Level 15",
            "RUNTIME PENDING",
            "NO APP / CREATE BIN / RELEASE INTEGRATION"
        ];
        if (required.Any(text => !svg.Contains(text, StringComparison.Ordinal)))
            throw new InvalidDataException("The 1200x1000 music guide lost a required visual gate.");
    }

    private static UnusedLevel65MusicRuntimeCandidateResult BuildResult(
        UnusedLevel65MusicRuntimeCandidatePaths paths,
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
            staged.Readback.ChangedLogicalExecutableBytes,
            staged.Readback.ChangedPhysicalImageBytes,
            staged.RebuiltRawSectorCount,
            staged.Readback.RawSectorDiffs.Count,
            staged.Readback.RawSectorDiffs,
            staged.Readback.RawSectorDiffSha256,
            ExactClosureConsumed: true,
            ExactLogicalDiffBoundaryVerified: true,
            ExactPhysicalSectorBoundaryVerified: true,
            FullImageReadbackVerified: true,
            FullScusReadbackVerified: true,
            Id65DataReadbackVerified: true,
            FullWadReadbackVerified: true,
            RetailParityStaticallyVerified: true,
            Id65Slot35StaticallyVerified: true,
            PeteXaDirectoryAndAudioPreserved: true,
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
        UnusedLevel65MusicRuntimeCandidateReceipt receipt,
        UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        bool exactSourcePaths =
            !string.IsNullOrWhiteSpace(receipt.WorkspaceRoot) &&
            !string.IsNullOrWhiteSpace(receipt.BaseImagePath) &&
            !string.IsNullOrWhiteSpace(receipt.BaseCuePath) &&
            !string.IsNullOrWhiteSpace(receipt.RemoteBlankProofImagePath) &&
            PathEquals(receipt.WorkspaceRoot, Path.GetFullPath(receipt.WorkspaceRoot)) &&
            PathEquals(receipt.BaseImagePath, Path.GetFullPath(receipt.BaseImagePath)) &&
            PathEquals(receipt.BaseCuePath, Path.GetFullPath(receipt.BaseCuePath)) &&
            PathEquals(receipt.RemoteBlankProofImagePath, Path.GetFullPath(receipt.RemoteBlankProofImagePath));
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
            receipt.RemoteBlankProofImageSha256,
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
            receipt.RawSectorDiffSha256
        }.All(IsExactSha256);
        bool exactRawDiff =
            receipt.RawSectorDiffs.Count == ExpectedChangedRawSectorCount &&
            receipt.RawSectorDiffs.SequenceEqual(ExpectedRawSectorDiffs) &&
            HashRawSectorDiffs(receipt.RawSectorDiffs) == ExpectedRawSectorDiffSha256 &&
            receipt.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256;
        bool exactFlags =
            receipt.ExactClosureConsumed &&
            receipt.ExactLogicalDiffBoundaryVerified &&
            receipt.ExactPhysicalSectorBoundaryVerified &&
            receipt.FullImageReadbackVerified &&
            receipt.FullScusReadbackVerified &&
            receipt.Id65DataReadbackVerified &&
            receipt.FullWadReadbackVerified &&
            receipt.RetailParityStaticallyVerified &&
            receipt.Id65Slot35StaticallyVerified &&
            receipt.PeteXaDirectoryAndAudioPreserved &&
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
            receipt.RemoteBlankProofImageSha256 != RemoteBlankProofImageSha256 ||
            receipt.SourceExecutableSha256 != SourceExecutableSha256 ||
            receipt.OutputExecutableSha256 != OutputExecutableSha256 ||
            receipt.SourceId65DataSha256 != SourceId65DataSha256 ||
            receipt.OutputId65DataSha256 != SourceId65DataSha256 ||
            receipt.SourceWadSha256 != ExpectedWadSha256 ||
            receipt.OutputWadSha256 != ExpectedWadSha256 ||
            receipt.ChangedLogicalExecutableBytes != ExpectedChangedLogicalExecutableBytes ||
            receipt.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
            receipt.RebuiltRawSectorCount != ExpectedChangedRawSectorCount ||
            receipt.ChangedRawSectorCount != ExpectedChangedRawSectorCount)
        {
            throw new InvalidDataException("The published music receipt lost an exact path, pin, diff, or fail-closed flag.");
        }
        RequireHash(receipt.OutputImageSha256, ExpectedOutputImageSha256, "receipt output BIN");
        RequireHash(receipt.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "receipt raw-sector diff");
    }

    private static void ValidateReceiptReadback(
        UnusedLevel65MusicRuntimeCandidateReceipt receipt,
        CandidateReadback readback)
    {
        if (receipt.OutputImageSha256 != readback.OutputImageSha256 ||
            receipt.SourceExecutableSha256 != readback.SourceExecutableSha256 ||
            receipt.OutputExecutableSha256 != readback.OutputExecutableSha256 ||
            receipt.SourceId65DataSha256 != readback.SourceId65DataSha256 ||
            receipt.OutputId65DataSha256 != readback.OutputId65DataSha256 ||
            receipt.SourceWadSha256 != readback.SourceWadSha256 ||
            receipt.OutputWadSha256 != readback.OutputWadSha256 ||
            receipt.ChangedLogicalExecutableBytes != readback.ChangedLogicalExecutableBytes ||
            receipt.ChangedPhysicalImageBytes != readback.ChangedPhysicalImageBytes ||
            receipt.ChangedRawSectorCount != readback.RawSectorDiffs.Count ||
            !receipt.RawSectorDiffs.SequenceEqual(readback.RawSectorDiffs) ||
            receipt.RawSectorDiffSha256 != readback.RawSectorDiffSha256)
        {
            throw new InvalidDataException("The published music candidate no longer matches its receipt readback.");
        }
    }

    private static void BindSidecarHashes(
        UnusedLevel65MusicRuntimeCandidateReceipt receipt,
        UnusedLevel65MusicRuntimeCandidatePaths paths)
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
        UnusedLevel65MusicRuntimeCandidatePaths paths)
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
                $"The music candidate artifact set is [{string.Join(',', actual)}], expected exactly seven files.");
        }
    }

    private static void VerifyPublishedCandidate(
        UnusedLevel65MusicRuntimeCandidatePaths paths,
        StagedCandidate staged,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The published music candidate directory is missing.");
        VerifySevenFiles(paths.OutputDirectoryPath, paths);
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.OutputCuePath, paths.OutputImagePath, "MODE2/2352");
        BindSidecarHashes(staged.Receipt, paths);
        VerifyTextReadback(
            paths.ConstructionPlanPath,
            JsonSerializer.Serialize(staged.Plan, JsonOptions) + "\n",
            "published music plan");
        VerifyTextReadback(
            paths.StaticReadbackReceiptPath,
            JsonSerializer.Serialize(staged.Receipt, JsonOptions) + "\n",
            "published music receipt");
        RuntimeCandidateFinderReveal reveal = BuildFinalFinderReveal(paths);
        string checklist = File.ReadAllText(paths.RuntimeChecklistPath);
        VerifyChecklist(checklist, reveal, staged.Plan.LoadCodes, paths);
        VerifyLocationGuide(File.ReadAllText(paths.LocationGuidePath));
        VerifyFinderHelper(paths, staged.Receipt.FinderHelperSha256);
    }

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        UnusedLevel65MusicRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static void VerifyFinderHelper(
        UnusedLevel65MusicRuntimeCandidatePaths paths,
        string expectedSha256)
    {
        if (!OperatingSystem.IsMacOS() ||
            File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode())
        {
            throw new InvalidDataException("The published music Finder helper is not exact 0755.");
        }
        RequireHash(HashFile(paths.FinderHelperPath), expectedSha256, "published music Finder helper");
        VerifyTextReadback(
            paths.FinderHelperPath,
            BuildExpectedFinderHelper(paths.OutputCuePath),
            "published music Finder helper content");
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
                throw new IOException("The music destination changed before replacement publication.");
            if (Directory.Exists(publication.BackupDirectoryPath))
                throw new IOException("The music operation backup path already exists.");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            publication.PreviousBackedUp = true;
            WriteOperationJournal(publication, OperationPhasePreviousBackedUp);
            testStageHook?.Invoke("after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The music destination appeared during staging.");
        }

        if (!Directory.Exists(publication.StagedDirectoryPath))
            throw new IOException("The staged music candidate disappeared before publication.");
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
                        throw new IOException("The previous music candidate backup is missing.");
                    if (Directory.Exists(publication.OutputDirectoryPath))
                        throw new IOException("The music destination is occupied before backup restoration.");
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
                "Music publication failed and full-directory recovery is incomplete; " +
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
            "music operations directory");
        RejectExistingAncestorReparsePoints(
            expectedOutputDirectoryPath,
            "music publication output");
        if (!Directory.Exists(operationsDirectoryPath))
            return false;
        RejectReparsePoint(operationsDirectoryPath, "music operations directory");
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
            RejectTreeReparsePoints(expectedOutputDirectoryPath, "stale music publication output");
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
            throw new InvalidDataException($"The music operations directory contains a non-owned entry: {operationRoot}");
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "stale music operation");
        RejectReparsePoint(operationRoot, "stale music operation");
        string leasePath = Path.Combine(operationRoot, "operation.lease");
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "stale music operation lease");
            try
            {
                using FileStream lease = new(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException("A music candidate operation is still active.", ex);
            }
        }

        ValidateOperationRootEntries(operationRoot);
        string journalPath = Path.Combine(operationRoot, OperationJournalFileName);
        if (!File.Exists(journalPath))
            throw new InvalidDataException("An owned music operation is missing its journal.");
        RejectReparsePoint(journalPath, "stale music operation journal");
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                File.ReadAllText(journalPath),
                JsonOptions)
            ?? throw new InvalidDataException("A music operation journal is unreadable.");
        ValidateOperationJournal(journal, operationRoot, operationsDirectoryPath, expectedOutputDirectoryPath);
        RejectUnexpectedDirectoryRoleType(journal.StagedDirectoryPath, "stale music staged candidate");
        RejectUnexpectedDirectoryRoleType(journal.BackupDirectoryPath, "stale music backup");
        bool stagedExists = Directory.Exists(journal.StagedDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (stagedExists)
            RejectTreeReparsePoints(journal.StagedDirectoryPath, "stale music staged candidate");
        if (backupExists)
            RejectTreeReparsePoints(journal.BackupDirectoryPath, "stale music backup");
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
            throw new IOException("Multiple previous music candidate backups exist; recovery was preserved for audit.");
        if (committed.Length > 1)
            throw new IOException("Multiple committed music operations claim one target; recovery was preserved for audit.");
        foreach (RecoverableOperation operation in operations)
        {
            if (operation.BackupExists && !operation.Journal.HadPreviousCandidate)
                throw new InvalidDataException("A no-previous music operation owns an unexpected backup.");
            if (operation.Journal.Phase == OperationPhasePreviousBackedUp && !operation.Journal.HadPreviousCandidate)
                throw new InvalidDataException("A music operation reports a backup without a previous candidate.");
        }

        RecoverableOperation? backupToRestore = null;
        RecoverableOperation? committedBackupToDiscard = null;
        bool deleteOutput = false;
        if (committed.Length == 1)
        {
            RecoverableOperation committedOperation = committed[0];
            if (!outputExists)
                throw new IOException("A committed music operation is missing its candidate directory.");
            if (backups.Length == 1 && !ReferenceEquals(backups[0], committedOperation))
                throw new IOException("An uncommitted music backup competes with a committed publication.");
            committedBackupToDiscard = backups.SingleOrDefault();
        }
        else if (backups.Length == 1)
        {
            backupToRestore = backups[0];
            if (backupToRestore.Journal.Phase == OperationPhaseRollbackComplete)
                throw new IOException("A rollback-complete music operation still owns a backup.");
            deleteOutput = outputExists;
        }
        else if (!outputExists)
        {
            if (operations.Any(operation => operation.Journal.HadPreviousCandidate))
                throw new IOException("A previous music candidate is missing and no unique backup can restore it.");
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
                    throw new IOException("A stale music operation cannot prove which published directory is authoritative.");
                bool uncommittedIsAuthoritative = operations.Any(operation =>
                    operation.Journal.Phase == OperationPhaseCandidatePublished ||
                    (operation.Journal.Phase == OperationPhaseStaging && !operation.StagedExists));
                if (!uncommittedIsAuthoritative)
                    throw new IOException("A no-previous music recovery cannot prove ownership of the output.");
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
                throw new IOException("The music destination is occupied before backup restoration.");
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
            throw new IOException("Music recovery refused to discard an operation while its backup exists.");
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
            RejectReparsePoint(entry, "stale music operation entry");
            string name = Path.GetFileName(entry);
            if (!allowed.Contains(name))
                throw new InvalidDataException($"A stale music operation contains an unowned entry: {entry}");
            bool isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
                throw new InvalidDataException($"A stale music operation entry has the wrong role: {entry}");
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
            throw new InvalidOperationException("The music output has no parent.");
        if (!Directory.Exists(parent))
            return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, outputDirectoryPath))
                continue;
            RejectReparsePoint(entry, "music publication output");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) == 0)
                throw new InvalidDataException("The music output path is not a directory.");
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
            throw new InvalidOperationException("A music recovery target escaped the requested output path.");
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
            throw new InvalidDataException("A stale music operation journal failed ownership validation.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, operationsDirectoryPath, "journal music operation");
        EnsureStrictDescendant(journal.StagedDirectoryPath, journal.OperationRootPath, "journal staging directory");
        EnsureStrictDescendant(journal.BackupDirectoryPath, journal.OperationRootPath, "journal backup directory");
        string parent = Path.GetDirectoryName(operationsDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(expectedOutputDirectoryPath), parent))
            throw new InvalidDataException("A stale music journal points outside its publication parent.");
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
        RejectReparsePoint(outputDirectoryPath, "music output directory");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The music candidate directory already exists. Set ReplaceExistingCandidate only for an intentional replacement.");
        }
    }

    private static void RequireSafeRoles(
        string baseImage,
        string baseCue,
        string remoteProof,
        UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        string[] sources = [baseImage, baseCue, remoteProof];
        if (sources.Any(source =>
                IsDescendantOrEqual(source, paths.OutputDirectoryPath) ||
                IsDescendantOrEqual(paths.OutputDirectoryPath, source)))
        {
            throw new InvalidOperationException("The music output and proof/source files must have distinct roles.");
        }
        if (PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath))
            throw new InvalidOperationException("The music output and operations directories overlap.");
    }

    private static void RejectUnsafePathTopologyBeforeMutation(
        string workspaceRoot,
        string baseImage,
        string baseCue,
        string remoteProof,
        UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        RejectExistingAncestorReparsePoints(workspaceRoot, "music workspace root");
        RejectExistingAncestorReparsePoints(baseImage, "music base BIN");
        RejectExistingAncestorReparsePoints(baseCue, "music base CUE");
        RejectExistingAncestorReparsePoints(remoteProof, "music remote proof BIN");
        RejectPublicationPathAncestorReparsePoints(paths);
    }

    private static void RejectPublicationPathAncestorReparsePoints(
        UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ??
            throw new InvalidOperationException("The music output has no parent.");
        RejectExistingAncestorReparsePoints(outputParent, "music publication parent");
        RejectExistingAncestorReparsePoints(paths.OutputDirectoryPath, "music publication output");
        RejectExistingAncestorReparsePoints(paths.OperationsDirectoryPath, "music operations directory");
        RejectExistingAncestorReparsePoints(GlobalWriterLeasePath(paths), "music global writer lease");
    }

    private static void RequirePathsInsideOutput(UnusedLevel65MusicRuntimeCandidatePaths paths)
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
            EnsureStrictDescendant(artifact, paths.OutputDirectoryPath, "music artifact");
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ?? "";
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent))
            throw new InvalidOperationException("The music operations directory escaped the output parent.");
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

    private static GlobalWriterLease AcquireGlobalWriterLease(UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The music operations directory has no parent.");
        RejectPublicationPathAncestorReparsePoints(paths);
        Directory.CreateDirectory(parent);
        string leasePath = GlobalWriterLeasePath(paths);
        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (!PathEquals(entry, leasePath))
                continue;
            RejectReparsePoint(entry, "music global writer lease");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new InvalidDataException("The music writer lease path is a directory.");
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
            throw new IOException("The music global writer lease file could not be opened.", ex);
        }

        try
        {
            RejectReparsePoint(leasePath, "music global writer lease");
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
                throw new IOException($"The music global writer OS lease failed (errno {error}: {nativeError.Message}).", nativeError);
            }
            return new GlobalWriterLease(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static string GlobalWriterLeasePath(UnusedLevel65MusicRuntimeCandidatePaths paths)
    {
        string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
            throw new InvalidOperationException("The music operations directory has no parent.");
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

    private static bool IsExactSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TryGetFrozenPublicationWorkspaceRoot(
        UnusedLevel65MusicRuntimeCandidatePaths paths,
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
            throw new InvalidOperationException("A music rollback target escaped its publication path.");
    }

    private static void TryDeleteOwnedOperation(string operationRoot, string operationsDirectoryPath)
    {
        if (!Directory.Exists(operationRoot))
            return;
        EnsureStrictDescendant(operationRoot, operationsDirectoryPath, "completed music operation");
        if (!IsOwnedOperationDirectory(operationRoot))
            throw new InvalidOperationException("Refusing to remove a non-owned music operation directory.");
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
        UnusedLevel65MusicRuntimeCandidatePlan Plan,
        UnusedLevel65MusicRuntimeCandidateReceipt Receipt,
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
        int ChangedLogicalExecutableBytes,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        IReadOnlyList<UnusedLevel65MusicRuntimeCandidateRawSectorDiff> RawSectorDiffs);
}
