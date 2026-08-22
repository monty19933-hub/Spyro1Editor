using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.Retired)
{
    bool retiredBeforeInputInspection = false;
    try
    {
        await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(null!);
    }
    catch (InvalidOperationException ex) when (
        string.Equals(
            ex.Message,
            UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.RetirementReason,
            StringComparison.Ordinal))
    {
        retiredBeforeInputInspection = true;
    }

    if (!retiredBeforeInputInspection)
    {
        throw new InvalidOperationException(
            "The retired standalone-spawn publisher did not fail closed before input/filesystem inspection.");
    }

    Console.WriteLine(
        $"PASS standalone-spawn retired fail-close: {UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.RetirementReason}");
    return;
}

const string LockedBaseSha256 = "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string FoundationSha256 = "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
const string OutputBinSha256 = "f76765081433a8ce4e68eaffa833ede6970684431e2a430c1e8eb18be9752f0d";
const string OutputId65DataSha256 = "782672262a9961ea114ba676f691ed8d6d1f29dcbbfcd9f9d1071ed3836bd96e";
const string RawDiffSha256 = "ae1a93b94dc6485a312f29dce23d7981e3d164d8678dd68affc3998fb2ca6607";

// Pinned after the first deterministic clean publication. These cover every
// sidecar byte, including the absolute-path-bearing receipt.
const string ExpectedCueSha256 = "72b4ded8fd7c2ec683c39de2cc71ea8cb5bf0c07f838d53d704b143d099f17de";
const string ExpectedPlanSha256 = "ba70036336527fd6fd8274d0017a13694559184f5be8c1958560fb80d528328c";
const string ExpectedReceiptSha256 = "d26659144464ed57e38c5404abfe83f7e235458472afc7071d1ff3c00c185554";
const string ExpectedChecklistSha256 = "26bfc0739179a11cec6f434f4705abf57ee830da883bbf95ec1cc62d7a2a0d2b";
const string ExpectedGuideSha256 = "76e7cb5c8fc5e17a144f392416cdc22d7766b113554644d13bd826f516e50823";
const string ExpectedFinderHelperSha256 = "9a6c6c891c6d89e6dee493167bae873d945641cb0e5994d58ce16b8fa18fbb6f";

ExpectedLoadCode[] exactLoadCodes =
[
    new("ID65 candidate", 65,
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down"),
    new("Retail Town Square", 13,
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Triangle"),
    new("Gnasty's Loot", 64,
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Right"),
    new("Sunny Flight", 15,
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Down")
];

string root = Path.GetFullPath(args.FirstOrDefault() ?? Directory.GetCurrentDirectory());
string lockedBase = Path.Combine(
    root,
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundationDirectory = Path.Combine(
    root,
    "_local/v5-stone-hill-level-replacement/unused-level-65-full-authoring-foundation-native-membership");
string foundation = Path.Combine(
    foundationDirectory,
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
string foundationCue = Path.ChangeExtension(foundation, ".cue");
string outputDirectory = Path.Combine(
    root,
    "_local/v5-stone-hill-level-replacement",
    UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.OutputDirectoryName);

RequireFile(lockedBase, "locked base");
RequireFile(foundation, "foundation BIN");
RequireFile(foundationCue, "foundation CUE");
string foundationHashBefore = await HashFileAsync(foundation);
DateTime foundationTimeBefore = File.GetLastWriteTimeUtc(foundation);

UnusedLevel65StandaloneSpawnRuntimeCandidateRequest normalRequest = new(
    root,
    lockedBase,
    foundation,
    foundationCue,
    outputDirectory,
    ReplaceExistingCandidate: Directory.Exists(outputDirectory));

// Hold the exact output-scoped lease before recovery, operation creation, or
// output movement. The competing writer must refuse immediately on macOS.
TaskCompletionSource leaseHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);
TaskCompletionSource releaseLease = new(TaskCreationOptions.RunContinuationsAsynchronously);
DirectorySnapshot outputBeforeLease = SnapshotDirectory(outputDirectory);
UnusedLevel65StandaloneSpawnRuntimeCandidatePaths knownPaths =
    UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreatePaths(outputDirectory);
DirectorySnapshot operationsBeforeLease = SnapshotDirectory(knownPaths.OperationsDirectoryPath);
Task<UnusedLevel65StandaloneSpawnRuntimeCandidateResult> firstTask = Task.Run(async () =>
    await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage != "after-global-writer-lease-acquired")
                return;
            leaseHeld.TrySetResult();
            releaseLease.Task.GetAwaiter().GetResult();
        }
    }));
await leaseHeld.Task.WaitAsync(TimeSpan.FromSeconds(10));
Stopwatch contentionTimer = Stopwatch.StartNew();
bool contentionRejected = false;
try
{
    await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest);
}
catch (IOException ex) when (ex.Message.Contains("Another standalone-spawn writer is active", StringComparison.Ordinal))
{
    contentionRejected = true;
}
contentionTimer.Stop();
Require(contentionRejected && contentionTimer.Elapsed < TimeSpan.FromSeconds(3),
    "A competing spawn writer did not fail immediately on the nonblocking OS lease.");
RequireSnapshotsEqual(outputBeforeLease, SnapshotDirectory(outputDirectory), "Held-writer output boundary");
RequireSnapshotsEqual(operationsBeforeLease, SnapshotDirectory(knownPaths.OperationsDirectoryPath), "Held-writer operation boundary");
releaseLease.TrySetResult();
UnusedLevel65StandaloneSpawnRuntimeCandidateResult first = await firstTask;

VerifyResult(first, expectedStartupRecovery: false);
DirectorySnapshot authoritative = SnapshotDirectory(outputDirectory);
Require(authoritative.Files.Count == 7,
    "The standalone-spawn publication did not contain exactly seven handoff artifacts.");
EnsureNoOperationDebris(knownPaths);

bool replaceRefused = false;
try
{
    await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        ReplaceExistingCandidate = false
    });
}
catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.Ordinal))
{
    replaceRefused = true;
}
Require(replaceRefused, "The standalone-spawn writer did not refuse an unapproved replacement.");
RequireSnapshotsEqual(authoritative, SnapshotDirectory(outputDirectory), "Replace=false refusal");

await VerifyInProcessRollbackAsync("after-previous-candidate-backup", authoritative);
await VerifyInProcessRollbackAsync("after-candidate-publication", authoritative);

UnusedLevel65StandaloneSpawnRuntimeCandidateResult priorRecovered =
    await VerifyPriorCandidateCrashRestartAsync(authoritative);
VerifyResult(priorRecovered, expectedStartupRecovery: true);
EnsureNoOperationDebris(knownPaths);

await VerifyNoPreviousCandidateMoveBeforeJournalRestartAsync();

// A recovered receipt must say true; the next two clean operations must both
// say false and converge byte-for-byte on the original authoritative handoff.
UnusedLevel65StandaloneSpawnRuntimeCandidateResult cleanOne =
    await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        ReplaceExistingCandidate = true
    });
VerifyResult(cleanOne, expectedStartupRecovery: false);
DirectorySnapshot cleanSnapshot = SnapshotDirectory(outputDirectory);
RequireSnapshotsEqual(authoritative, cleanSnapshot, "Clean rerun after restart recovery");

UnusedLevel65StandaloneSpawnRuntimeCandidateResult cleanTwo =
    await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        ReplaceExistingCandidate = true
    });
VerifyResult(cleanTwo, expectedStartupRecovery: false);
RequireSnapshotsEqual(cleanSnapshot, SnapshotDirectory(outputDirectory), "Second deterministic clean rerun");
Require(first.Receipt.OutputImageSha256 == cleanTwo.Receipt.OutputImageSha256 &&
        first.Receipt.OutputId65DataSha256 == cleanTwo.Receipt.OutputId65DataSha256 &&
        first.Receipt.RawSectorDiffSha256 == cleanTwo.Receipt.RawSectorDiffSha256,
    "A deterministic standalone-spawn rebuild changed its BIN/data/raw-diff hashes.");

PinCleanHandoff(cleanTwo);
await VerifyStaleGuideRejectionAsync(cleanTwo);
EnsureNoOperationDebris(knownPaths);

Require(await HashFileAsync(foundation) == foundationHashBefore &&
        File.GetLastWriteTimeUtc(foundation) == foundationTimeBefore,
    "The standalone-spawn smoke modified the exact foundation source.");

Console.WriteLine(
    "PASS UnusedLevel65StandaloneSpawnRuntimeCandidateSmoke: exact foundation -> coupled landing/T92 native-apron candidate; " +
    $"BIN={cleanTwo.Receipt.OutputImageSha256}; data={cleanTwo.Receipt.OutputId65DataSha256}; " +
    $"logical={cleanTwo.Receipt.ChangedLogicalWadBytes}; physical={cleanTwo.Receipt.ChangedPhysicalImageBytes}; " +
    $"rawDiff={cleanTwo.Receipt.RawSectorDiffSha256}; nonblocking lease/refusal/in-process rollback/" +
    "prior-candidate restart/no-previous move-before-journal restart/clean determinism/full handoff binding/stale-guide rejection passed; runtime pending/unpromoted.");

async Task VerifyInProcessRollbackAsync(string injectedStage, DirectorySnapshot expected)
{
    bool injected = false;
    try
    {
        await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == injectedStage)
                    throw new InvalidOperationException("Injected standalone-spawn rollback smoke at " + stage);
            }
        });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Injected standalone-spawn rollback smoke", StringComparison.Ordinal))
    {
        injected = true;
    }
    Require(injected, $"The writer did not reach rollback hook {injectedStage}.");
    RequireSnapshotsEqual(expected, SnapshotDirectory(outputDirectory), $"Rollback {injectedStage}");
    EnsureNoOperationDebris(knownPaths);
}

async Task<UnusedLevel65StandaloneSpawnRuntimeCandidateResult> VerifyPriorCandidateCrashRestartAsync(
    DirectorySnapshot expectedPrior)
{
    EnsureNoOperationDebris(knownPaths);
    string operationId = Guid.NewGuid().ToString("N");
    string stage = Path.Combine(knownPaths.OperationsDirectoryPath, "spawn-stage-" + operationId);
    string backup = Path.Combine(knownPaths.OperationsDirectoryPath, "spawn-backup-" + operationId);
    Directory.CreateDirectory(knownPaths.OperationsDirectoryPath);
    CloneDirectory(outputDirectory, stage);

    // Make the staged candidate independently valid but observably different
    // from the authoritative prior candidate. A recovery hook must see the
    // exact prior snapshot, proving backup restoration rather than retention
    // of the just-published candidate.
    string stagedReceipt = Path.Combine(stage, Path.GetFileName(knownPaths.ReceiptPath));
    string stagedReceiptText = File.ReadAllText(stagedReceipt);
    const string cleanRecoveryFlag = "\"startupRecoveryPerformed\": false";
    const string recoveredRecoveryFlag = "\"startupRecoveryPerformed\": true";
    Require(stagedReceiptText.Contains(cleanRecoveryFlag, StringComparison.Ordinal) &&
            stagedReceiptText.IndexOf(cleanRecoveryFlag, StringComparison.Ordinal) ==
            stagedReceiptText.LastIndexOf(cleanRecoveryFlag, StringComparison.Ordinal),
        "The staged restart fixture could not toggle its exact recovery receipt flag.");
    File.WriteAllText(
        stagedReceipt,
        stagedReceiptText.Replace(cleanRecoveryFlag, recoveredRecoveryFlag, StringComparison.Ordinal),
        new UTF8Encoding(false));
    FlushFile(stagedReceipt);
    DirectorySnapshot publishedCrashCandidate = SnapshotDirectory(stage);
    RequireSnapshotsDiffer(expectedPrior, publishedCrashCandidate,
        "The prior-candidate crash fixture was not observably distinct.");

    // Reproduce the writer's real ordering, then stop after the stage->output
    // move and before the post-move PhasePublished journal write.
    WriteRecoveryJournal(knownPaths, operationId, "staged", hadPreviousCandidate: true);
    WriteRecoveryJournal(knownPaths, operationId, "backup-intent", hadPreviousCandidate: true);
    Directory.Move(outputDirectory, backup);
    WriteRecoveryJournal(knownPaths, operationId, "previous-backed-up", hadPreviousCandidate: true);
    WriteRecoveryJournal(knownPaths, operationId, "publish-intent", hadPreviousCandidate: true);
    Directory.Move(stage, outputDirectory);
    RequireSnapshotsEqual(publishedCrashCandidate, SnapshotDirectory(outputDirectory),
        "Prior-candidate crash fixture publication move");

    bool recoveryHookSeen = false;
    UnusedLevel65StandaloneSpawnRuntimeCandidateResult recovered =
        await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(normalRequest with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = hook =>
            {
                if (hook != "after-startup-recovery")
                    return;
                recoveryHookSeen = true;
                RequireSnapshotsEqual(expectedPrior, SnapshotDirectory(outputDirectory),
                    "Prior-candidate authoritative restart restoration");
            }
        });
    Require(recoveryHookSeen, "The prior-candidate restart did not cross the startup-recovery hook.");
    Require(recovered.Receipt.StartupRecoveryPerformed,
        "The prior-candidate recovered receipt did not record startupRecoveryPerformed=true.");
    return recovered;
}

async Task VerifyNoPreviousCandidateMoveBeforeJournalRestartAsync()
{
    string temporaryParent = Directory.CreateTempSubdirectory("spyro-id65-spawn-no-previous-recovery-").FullName;
    try
    {
        string isolatedOutput = Path.Combine(
            temporaryParent,
            UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.OutputDirectoryName);
        UnusedLevel65StandaloneSpawnRuntimeCandidateRequest isolatedRequest = normalRequest with
        {
            OutputDirectoryPath = isolatedOutput,
            ReplaceExistingCandidate = false,
            TestStageHook = null
        };
        UnusedLevel65StandaloneSpawnRuntimeCandidateResult seed =
            await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(isolatedRequest);
        VerifyResult(seed, expectedStartupRecovery: false);

        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths isolatedPaths =
            UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreatePaths(isolatedOutput);
        EnsureNoOperationDebris(isolatedPaths);
        string operationId = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(isolatedPaths.OperationsDirectoryPath, "spawn-stage-" + operationId);
        Directory.CreateDirectory(isolatedPaths.OperationsDirectoryPath);

        // Put the already verified seed at the exact path where BuildStageAsync
        // would have produced it, write the real pre-move journal, perform the
        // atomic move, and omit only the post-move journal update.
        Directory.Move(isolatedOutput, stage);
        WriteRecoveryJournal(isolatedPaths, operationId, "staged", hadPreviousCandidate: false);
        WriteRecoveryJournal(isolatedPaths, operationId, "publish-intent", hadPreviousCandidate: false);
        Directory.Move(stage, isolatedOutput);

        bool recoveryHookSeen = false;
        UnusedLevel65StandaloneSpawnRuntimeCandidateResult recovered =
            await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.CreateAsync(isolatedRequest with
            {
                TestStageHook = hook =>
                {
                    if (hook != "after-startup-recovery")
                        return;
                    recoveryHookSeen = true;
                    Require(!Directory.Exists(isolatedOutput),
                        "No-previous restart did not discard the uncommitted moved candidate.");
                }
            });
        Require(recoveryHookSeen, "The no-previous restart did not cross the startup-recovery hook.");
        VerifyResult(recovered, expectedStartupRecovery: true);
        Require(recovered.Receipt.OutputImageSha256 == OutputBinSha256 &&
                recovered.Receipt.OutputId65DataSha256 == OutputId65DataSha256,
            "The no-previous recovered publication did not deterministically rebuild the candidate.");
        EnsureNoOperationDebris(isolatedPaths);
    }
    finally
    {
        if (Directory.Exists(temporaryParent))
            Directory.Delete(temporaryParent, recursive: true);
    }
}

async Task VerifyStaleGuideRejectionAsync(UnusedLevel65StandaloneSpawnRuntimeCandidateResult expected)
{
    byte[] exactGuide = await File.ReadAllBytesAsync(expected.Paths.GuidePath);
    DirectorySnapshot exactSnapshot = SnapshotDirectory(expected.Paths.OutputDirectoryPath);
    bool staleRejected = false;
    try
    {
        await File.AppendAllTextAsync(
            expected.Paths.GuidePath,
            "\n<!-- stale standalone-spawn guide smoke -->\n",
            new UTF8Encoding(false));
        try
        {
            await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.VerifyPublishedAsync(
                expected.Paths.OutputDirectoryPath);
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("guide", StringComparison.OrdinalIgnoreCase))
        {
            staleRejected = true;
        }
    }
    finally
    {
        await File.WriteAllBytesAsync(expected.Paths.GuidePath, exactGuide);
        FlushFile(expected.Paths.GuidePath);
    }
    Require(staleRejected, "Published readback accepted a stale or tampered guide.");
    RequireSnapshotsEqual(exactSnapshot, SnapshotDirectory(expected.Paths.OutputDirectoryPath),
        "Stale-guide restoration");
    UnusedLevel65StandaloneSpawnRuntimeCandidateResult readback =
        await UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.VerifyPublishedAsync(
            expected.Paths.OutputDirectoryPath);
    VerifyResult(readback, expectedStartupRecovery: false);
    PinCleanHandoff(readback);
}

void VerifyResult(
    UnusedLevel65StandaloneSpawnRuntimeCandidateResult result,
    bool expectedStartupRecovery)
{
    UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan = result.Plan;
    UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt receipt = result.Receipt;
    Require(plan.ProfileId == UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.ProfileId &&
            plan.FoundationImageSha256 == FoundationSha256 &&
            plan.LockedBaseImageSha256 == LockedBaseSha256 &&
            plan.LevelId == 65 && plan.ContinuousLevelIndex == 35,
        "The standalone-spawn substrate/profile boundary changed.");
    Require(plan.BeforeLandingRawX == 125_225 && plan.BeforeLandingRawY == 100_506 && plan.BeforeLandingRawZ == 8_550 &&
            plan.BeforePlayerRawX == 125_225 && plan.BeforePlayerRawY == 100_506 && plan.BeforePlayerRawZ == 8_704 &&
            plan.AuthoredLandingRawX == 124_585 && plan.AuthoredLandingRawY == 102_954 && plan.AuthoredLandingRawZ == 8_550 &&
            plan.AuthoredPlayerRawX == 124_585 && plan.AuthoredPlayerRawY == 102_954 && plan.AuthoredPlayerRawZ == 8_704 &&
            plan.YawByte == 0x40 && plan.DeltaRawX == -640 && plan.DeltaRawY == 2_448 && plan.DeltaRawZ == 0,
        "The exact coupled landing/T92 authored coordinates changed.");
    Require(plan.Patches.Count == 2 && plan.Patches.Sum(patch => patch.ChangedByteCount) == 8 &&
            plan.AffectedRawSectorLbas.SequenceEqual(new[] { 54_834, 54_838 }) &&
            plan.LandingAndPlayerAnchorCoupledAtomically,
        "The exact two-record/two-sector patch boundary changed.");
    Require(plan.Support.TerrainSectorIndex == 213 && plan.Support.TerrainFaceIndex == 37 &&
            plan.Support.CollisionTriangleIndex == 1_353 &&
            plan.Support.CollisionTriangleHex == "521E20004A1960C000020000" &&
            plan.Support.CollisionAssignment == 0 && plan.Support.CollisionFlags == 0 &&
            plan.Support.CollisionNormalZ == -16_384 &&
            plan.Support.DestinationCell == new UnusedLevel65StandaloneSpawnCollisionCell(30, 25, 2) &&
            plan.Support.NativeLeftEdgeMarginRaw == 393 &&
            plan.Support.NativeDiagonalMarginRaw == 237 &&
            plan.Support.FoundationTriangleSeparationRaw == 316 &&
            plan.Support.NativeGroundRawZ == 8_192 &&
            plan.Support.LandingClearanceRaw == 358 &&
            plan.Support.PlayerAnchorClearanceRaw == 512 &&
            plan.Support.DestinationStrictlyInsideNativeCollision &&
            plan.Support.DestinationStrictlyOutsideFoundationTriangle &&
            plan.Support.SupportIsTopmostAtDestination &&
            plan.Support.TerrainFaceReadbackVerified &&
            plan.Support.RuntimeEvidenceBoundToExactNativeFan,
        "The exact native-apron destination support proof changed.");
    Require(plan.MusicPreserved && plan.TotalsPreserved && plan.ExitPreserved && plan.SaveCodePreserved &&
            plan.RetailLevelsPreserved && plan.RequiresDuckStationRuntimeProof && !plan.RuntimePassed &&
            !plan.NormalCreateBinEnabled && !plan.PromotionAuthorized,
        "The standalone-spawn behavior/promotion boundary changed.");
    VerifyExactLoadCodes(plan.LoadCodes);
    Require(plan.LoadCodes.SequenceEqual(receipt.LoadCodes),
        "The standalone-spawn plan and receipt load codes differ.");

    Require(receipt.ProfileId == UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.ProfileId &&
            receipt.LockedBaseImageSha256 == LockedBaseSha256 &&
            receipt.FoundationImageSha256 == FoundationSha256 &&
            receipt.OutputImageSha256 == OutputBinSha256 &&
            receipt.OutputId65DataSha256 == OutputId65DataSha256 &&
            receipt.RawSectorDiffSha256 == RawDiffSha256,
        "The receipt lost an exact base/foundation/output/data/raw-diff hash.");
    RequirePath(receipt.LockedBaseImagePath, lockedBase, "receipt locked-base path");
    RequirePath(receipt.FoundationImagePath, foundation, "receipt foundation path");
    RequirePath(receipt.FoundationCuePath, foundationCue, "receipt foundation CUE path");
    RequirePath(receipt.OutputDirectoryPath, result.Paths.OutputDirectoryPath, "receipt output-directory path");
    RequirePath(receipt.OutputImagePath, result.Paths.ImagePath, "receipt BIN path");
    RequirePath(receipt.OutputCuePath, result.Paths.CuePath, "receipt CUE path");
    RequirePath(receipt.PlanPath, result.Paths.PlanPath, "receipt plan path");
    RequirePath(receipt.ReceiptPath, result.Paths.ReceiptPath, "receipt self path");
    RequirePath(receipt.ChecklistPath, result.Paths.ChecklistPath, "receipt checklist path");
    RequirePath(receipt.GuidePath, result.Paths.GuidePath, "receipt guide path");
    Require(!string.IsNullOrWhiteSpace(receipt.FinderHelperPath), "The receipt lost its Finder helper path.");
    RequirePath(receipt.FinderHelperPath!, result.Paths.RevealHelperPath, "receipt Finder helper path");
    Require(receipt.ChangedLogicalWadBytes == 8 && receipt.ChangedPhysicalImageBytes == 116 &&
            receipt.RawSectorDiffs.Count == 2 &&
            receipt.RawSectorDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(new[] { 54_834, 54_838 }) &&
            receipt.RawSectorDiffs.Select(diff => diff.PayloadChangedBytes).SequenceEqual(new[] { 4, 4 }) &&
            receipt.RawSectorDiffs.Select(diff => diff.EdcChangedBytes).SequenceEqual(new[] { 4, 4 }) &&
            receipt.RawSectorDiffs.Select(diff => diff.EccPChangedBytes).SequenceEqual(new[] { 16, 16 }) &&
            receipt.RawSectorDiffs.Select(diff => diff.EccQChangedBytes).SequenceEqual(new[] { 36, 32 }) &&
            receipt.RawSectorDiffs.Select(diff => diff.TotalChangedBytes).SequenceEqual(new[] { 60, 56 }) &&
            receipt.RawSectorDiffs.All(diff => diff.HeaderChangedBytes == 0 &&
                                               diff.SubheaderChangedBytes == 0 &&
                                               diff.ReservedChangedBytes == 0) &&
            receipt.ExactPatchReadbackVerified && receipt.CoupledLandingAndPlayerAnchorVerified &&
            receipt.DestinationSupportVerified && receipt.FoundationPreserved && receipt.ExecutablePreserved &&
            receipt.RetailTownSquarePreserved && receipt.MusicTotalsExitSavePreserved &&
            receipt.RawSectorIntegrityVerified && receipt.AtomicDirectoryPublicationCompleted && receipt.RollbackGuardsEnabled &&
            receipt.StartupRecoveryPerformed == expectedStartupRecovery &&
            receipt.RuntimePending && !receipt.NormalCreateBinEnabled && !receipt.PromotionAuthorized,
        "The standalone-spawn receipt/readback/recovery boundary changed.");

    Require(receipt.OutputCueSha256 == result.ArtifactHashes.CueSha256 &&
            receipt.PlanSha256 == result.ArtifactHashes.PlanSha256 &&
            receipt.ChecklistSha256 == result.ArtifactHashes.ChecklistSha256 &&
            receipt.GuideSha256 == result.ArtifactHashes.GuideSha256 &&
            receipt.FinderHelperSha256 == result.ArtifactHashes.FinderHelperSha256,
        "Published readback did not bind the receipt to every handoff sidecar hash.");
    Require(result.ArtifactHashes.CueSha256 == HashFile(result.Paths.CuePath) &&
            result.ArtifactHashes.PlanSha256 == HashFile(result.Paths.PlanPath) &&
            result.ArtifactHashes.ReceiptSha256 == HashFile(result.Paths.ReceiptPath) &&
            result.ArtifactHashes.ChecklistSha256 == HashFile(result.Paths.ChecklistPath) &&
            result.ArtifactHashes.GuideSha256 == HashFile(result.Paths.GuidePath) &&
            result.ArtifactHashes.FinderHelperSha256 == HashFile(result.Paths.RevealHelperPath),
        "Published readback did not return the exact plan/receipt/checklist/guide/helper hashes.");

    string cueName = Path.GetFileName(result.Paths.CuePath);
    string binName = Path.GetFileName(result.Paths.ImagePath);
    string cue = File.ReadAllText(result.Paths.CuePath);
    string planJson = File.ReadAllText(result.Paths.PlanPath);
    string receiptJson = File.ReadAllText(result.Paths.ReceiptPath);
    string checklist = File.ReadAllText(result.Paths.ChecklistPath);
    string guide = File.ReadAllText(result.Paths.GuidePath);
    string helper = File.ReadAllText(result.Paths.RevealHelperPath);
    Require(cue.Contains($"FILE \"{binName}\" BINARY", StringComparison.Ordinal) &&
            !cue.Contains(result.Paths.OutputDirectoryPath, StringComparison.Ordinal),
        "The CUE lost exact relative BIN pairing.");
    Require(planJson.Contains("\"loadCodes\"", StringComparison.Ordinal) &&
            receiptJson.Contains("\"lockedBaseImageSha256\"", StringComparison.Ordinal) &&
            receiptJson.Contains("\"planSha256\"", StringComparison.Ordinal) &&
            receiptJson.Contains("\"loadCodes\"", StringComparison.Ordinal),
        "The plan/receipt JSON lost required handoff fields.");
    Require(checklist.Contains(UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.ProfileId, StringComparison.Ordinal) &&
            checklist.Contains(cueName, StringComparison.Ordinal) &&
            checklist.Contains(OutputBinSha256, StringComparison.Ordinal) &&
            checklist.Contains("Runtime status: **pending / unpromoted**", StringComparison.Ordinal) &&
            checklist.Contains("death", StringComparison.OrdinalIgnoreCase) &&
            checklist.Contains("reset/cold-boot", StringComparison.OrdinalIgnoreCase),
        "The checklist lost its identity or death/reset pending boundary.");
    Require(guide.Contains("width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\"", StringComparison.Ordinal) &&
            guide.Contains(UnusedLevel65StandaloneSpawnRuntimeCandidateExporter.ProfileId, StringComparison.Ordinal) &&
            guide.Contains(cueName, StringComparison.Ordinal) &&
            guide.Contains(OutputBinSha256, StringComparison.Ordinal) &&
            guide.Contains("<rect x=\"770\" y=\"25\" width=\"370\" height=\"48\"", StringComparison.Ordinal) &&
            guide.Contains("<text x=\"955\" y=\"55\" text-anchor=\"middle\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"14\" font-weight=\"700\">RUNTIME PENDING / UNPROMOTED</text>", StringComparison.Ordinal) &&
            guide.Contains("<text x=\"720\" y=\"428\" fill=\"#f0dcff\" font-family=\"sans-serif\" font-size=\"14\">Raw (124585, 102954)</text>", StringComparison.Ordinal) &&
            guide.Contains("<text x=\"720\" y=\"452\" fill=\"#f0dcff\" font-family=\"sans-serif\" font-size=\"14\">19.75 scene units outside foundation triangle</text>", StringComparison.Ordinal) &&
            !guide.Contains("<text x=\"735\" y=\"445\"", StringComparison.Ordinal) &&
            guide.Contains("AUTHORED START", StringComparison.Ordinal) &&
            guide.Contains("death respawn", StringComparison.OrdinalIgnoreCase) &&
            guide.Contains("reset/cold-boot", StringComparison.OrdinalIgnoreCase),
        "The 1200x1000 guide lost its safe badge/wrapped-coordinate layout or identity/runtime/death/reset stamps.");
    foreach (ExpectedLoadCode exact in exactLoadCodes)
    {
        Require(checklist.Contains($"| {exact.TestName} | {exact.LevelId} | `{exact.InputCode}` |", StringComparison.Ordinal),
            $"The checklist omitted the exact full {exact.TestName} load-code row.");
        string guideRow = $"{exact.TestName} (ID{exact.LevelId}): {exact.InputCode}";
        Require(guide.Contains(XmlEscape(guideRow), StringComparison.Ordinal),
            $"The guide omitted the exact full {exact.TestName} load code.");
    }
    Require(helper.Contains($"cue_name='{cueName}'", StringComparison.Ordinal) &&
            helper.Contains("cue_path=\"${0:A:h}/${cue_name}\"", StringComparison.Ordinal) &&
            helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal) &&
            !helper.Contains(result.Paths.OutputDirectoryPath, StringComparison.Ordinal),
        "The Finder helper lost exact relative CUE targeting.");
    if (!OperatingSystem.IsWindows())
    {
        Require(File.GetUnixFileMode(result.Paths.RevealHelperPath) ==
                (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                 UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead | UnixFileMode.OtherExecute),
            "The standalone-spawn Finder helper is not exact 0755.");
    }
}

void PinCleanHandoff(UnusedLevel65StandaloneSpawnRuntimeCandidateResult result)
{
    Require(result.ArtifactHashes.CueSha256 == ExpectedCueSha256 &&
            result.ArtifactHashes.PlanSha256 == ExpectedPlanSha256 &&
            result.ArtifactHashes.ReceiptSha256 == ExpectedReceiptSha256 &&
            result.ArtifactHashes.ChecklistSha256 == ExpectedChecklistSha256 &&
            result.ArtifactHashes.GuideSha256 == ExpectedGuideSha256 &&
            result.ArtifactHashes.FinderHelperSha256 == ExpectedFinderHelperSha256,
        "A pinned clean handoff sidecar hash changed.");
}

void VerifyExactLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> actual)
{
    Require(actual.Count == exactLoadCodes.Length, "The exact four load-code count changed.");
    for (int index = 0; index < exactLoadCodes.Length; index++)
    {
        ExpectedLoadCode expected = exactLoadCodes[index];
        Require(actual[index].TestName == expected.TestName &&
                actual[index].LevelId == expected.LevelId &&
                actual[index].InputCode == expected.InputCode,
            $"The full load code at index {index} changed.");
    }
}

static void WriteRecoveryJournal(
    UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths,
    string operationId,
    string phase,
    bool hadPreviousCandidate)
{
    Directory.CreateDirectory(paths.OperationsDirectoryPath);
    string journalPath = Path.Combine(paths.OperationsDirectoryPath, "operation-journal.json");
    string temporaryPath = journalPath + ".fixture.tmp";
    var journal = new
    {
        SchemaVersion = 2,
        OperationKind = "unused-level-65-standalone-spawn-publication",
        OperationId = operationId,
        OutputDirectoryPath = Path.GetFullPath(paths.OutputDirectoryPath),
        Phase = phase,
        StageDirectoryPath = Path.Combine(paths.OperationsDirectoryPath, "spawn-stage-" + operationId),
        BackupDirectoryPath = Path.Combine(paths.OperationsDirectoryPath, "spawn-backup-" + operationId),
        HadPreviousCandidate = hadPreviousCandidate
    };
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    File.WriteAllText(
        temporaryPath,
        JsonSerializer.Serialize(journal, options) + "\n",
        new UTF8Encoding(false));
    FlushFile(temporaryPath);
    File.Move(temporaryPath, journalPath, overwrite: true);
}

static void CloneDirectory(string source, string destination)
{
    if (Directory.Exists(destination))
        throw new InvalidOperationException("The crash-fixture stage already exists.");
    Directory.CreateDirectory(destination);
    foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string target = Path.Combine(destination, Path.GetRelativePath(source, file));
        File.Copy(file, target, overwrite: false);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(target, File.GetUnixFileMode(file));
    }
}

static void EnsureNoOperationDebris(UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
{
    if (!Directory.Exists(paths.OperationsDirectoryPath))
        return;
    string[] debris = Directory.EnumerateFileSystemEntries(paths.OperationsDirectoryPath).ToArray();
    Require(debris.Length == 0,
        "The standalone-spawn writer left operation journal/stage/backup/temp debris: " +
        string.Join(", ", debris.Select(Path.GetFileName)));
}

static DirectorySnapshot SnapshotDirectory(string path)
{
    if (!Directory.Exists(path))
        return new(false, new Dictionary<string, FileSnapshot>(StringComparer.Ordinal));
    Dictionary<string, FileSnapshot> files = new(StringComparer.Ordinal);
    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        string relative = Path.GetRelativePath(path, file);
        FileInfo info = new(file);
        files.Add(relative, new(info.Length, HashFile(file)));
    }
    return new(true, files);
}

static void RequireSnapshotsEqual(DirectorySnapshot expected, DirectorySnapshot actual, string label)
{
    Require(expected.Exists == actual.Exists && expected.Files.Count == actual.Files.Count,
        $"{label} changed directory existence/count.");
    foreach ((string path, FileSnapshot expectedFile) in expected.Files)
    {
        Require(actual.Files.TryGetValue(path, out FileSnapshot? actualFile) && expectedFile == actualFile,
            $"{label} changed '{path}'.");
    }
}

static void RequireSnapshotsDiffer(DirectorySnapshot left, DirectorySnapshot right, string label)
{
    bool equal = left.Exists == right.Exists && left.Files.Count == right.Files.Count &&
                 left.Files.All(pair => right.Files.TryGetValue(pair.Key, out FileSnapshot? other) && pair.Value == other);
    Require(!equal, label);
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static void FlushFile(string path)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    stream.Flush(flushToDisk: true);
}

static void RequirePath(string actual, string expected, string label)
{
    StringComparison comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    Require(string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), comparison),
        $"The {label} changed.");
}

static string XmlEscape(string value) => value
    .Replace("&", "&amp;", StringComparison.Ordinal)
    .Replace("<", "&lt;", StringComparison.Ordinal)
    .Replace(">", "&gt;", StringComparison.Ordinal)
    .Replace("\"", "&quot;", StringComparison.Ordinal)
    .Replace("'", "&apos;", StringComparison.Ordinal);

static void RequireFile(string path, string label)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"Missing {label}.", path);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record ExpectedLoadCode(string TestName, int LevelId, string InputCode);
sealed record DirectorySnapshot(bool Exists, IReadOnlyDictionary<string, FileSnapshot> Files);
sealed record FileSnapshot(long ByteLength, string Sha256);
