using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;

[System.Runtime.Versioning.SupportedOSPlatform("macos")]
internal static class Program
{
private static async Task Main(string[] args)
{
string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/" +
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local/v5-stone-hill-level-replacement/unused-level-65-full-authoring-foundation-native-membership/" +
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedImagePath) && File.Exists(lockedCuePath) && File.Exists(foundationImagePath),
    "The locked display-name BIN/CUE or foundation donor is missing.");

FileSnapshot lockedBefore = SnapshotFile(lockedImagePath);
FileSnapshot cueBefore = SnapshotFile(lockedCuePath);
FileSnapshot foundationBefore = SnapshotFile(foundationImagePath);
Id65RewardStagingCompileRequest compileRequest = BuildCompileRequest(lockedImagePath, foundationImagePath);

string smokeRoot = Path.Combine(
    "/private/tmp",
    "spyro-id65-core4-reward-publisher-smoke-" + Guid.NewGuid().ToString("N"));
string fixtureRoot = Path.Combine(smokeRoot, "full-readback-fixture");
string publicationWorkspace = Path.Combine(smokeRoot, "publication-workspace");
string outputParent = Path.Combine(publicationWorkspace, "publication-parent");
string outputDirectory = Path.Combine(
    outputParent,
    Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputDirectoryName);
string pendingProbeParent = Path.Combine(publicationWorkspace, "production-pending-probe");
string pendingProbeOutput = Path.Combine(
    pendingProbeParent,
    Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputDirectoryName);
bool keep = string.Equals(
    Environment.GetEnvironmentVariable("SPYRO_KEEP_CORE4_PUBLISHER_SMOKE"),
    "1",
    StringComparison.Ordinal);
if (!OperatingSystem.IsMacOS())
    throw new PlatformNotSupportedException("The Core4 reward publisher smoke requires macOS filesystem semantics.");
SmokeDirectoryIdentity smokeRootIdentity = CreatePrivateSmokeDirectory(
    smokeRoot,
    "publisher smoke anchor");

try
{
    SmokeDirectoryIdentity publicationWorkspaceIdentity = CreatePrivateSmokeDirectory(
        publicationWorkspace,
        "publisher publication workspace");
    Require(
        string.Equals(Path.GetDirectoryName(publicationWorkspace), smokeRoot, StringComparison.Ordinal) &&
        !string.Equals(smokeRoot, publicationWorkspace, StringComparison.Ordinal) &&
        !smokeRootIdentity.SameFile(publicationWorkspaceIdentity) &&
        string.Equals(Path.GetDirectoryName(fixtureRoot), smokeRoot, StringComparison.Ordinal) &&
        !IsPathWithin(fixtureRoot, publicationWorkspace) &&
        IsPathWithin(outputParent, publicationWorkspace) &&
        IsPathWithin(pendingProbeParent, publicationWorkspace),
        "The immutable evidence anchor and mutable publication workspace are not exact disjoint siblings.");
    VerifySmokeRootGuardMatrix(
        smokeRoot,
        smokeRootIdentity,
        publicationWorkspace,
        publicationWorkspaceIdentity);
    VerifyApiBoundary();
    VerifyFilesystemIdentityMatrix();
    VerifyJournalFingerprintProjectionMatrix();
    VerifyAtomicPrivateCreationMode(smokeRoot);
    VerifyCandidateDirectoryRenameFixture(smokeRoot);
    await VerifyRejectHelpersDoNotSelfPassAsync();
    VerifyPinnedEvidenceReaderGuards(smokeRoot);

    Id65AuthoringRewardStagingRuntimeCandidateRequest pendingRequest = new(
        repositoryRoot,
        lockedImagePath,
        lockedCuePath,
        pendingProbeOutput,
        compileRequest);
    await RejectAsync(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.CreateAsync(pendingRequest),
        "PENDING",
        "production pending evidence");
    await RejectAsync(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.CreateAsync(pendingRequest with
        {
            TestStageHook = _ => { }
        }),
        "forbidden",
        "production TestStageHook");
    Require(!Directory.Exists(pendingProbeParent) && !File.Exists(pendingProbeParent),
        "Production PENDING/hook rejection created a parent, lease, operation, stage, or output.");

    FullReadbackFixtureInput fullReadbackFixture = BuildExistingCompleteFullReadbackFixture(
        repositoryRoot,
        fixtureRoot);
    object token = CreateSyntheticToken(repositoryRoot, fullReadbackFixture);
    Id65AuthoringRewardStagingRuntimeCandidateRequest request = new(
        repositoryRoot,
        lockedImagePath,
        lockedCuePath,
        outputDirectory,
        compileRequest);
    await VerifyPublicationParentBoundaryAsync(
        request,
        token,
        repositoryRoot,
        fullReadbackFixture,
        smokeRoot,
        smokeRootIdentity,
        publicationWorkspace,
        publicationWorkspaceIdentity,
        outputParent);
    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string forbiddenHomeOutput = Path.Combine(
        userProfile,
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputDirectoryName);
    string forbiddenHomeLease = Path.Combine(userProfile, ".id65-core4-reward-v3-runtime-candidate-writer.lease");
    bool homeOutputBefore = Directory.Exists(forbiddenHomeOutput) || File.Exists(forbiddenHomeOutput);
    bool homeLeaseBefore = Directory.Exists(forbiddenHomeLease) || File.Exists(forbiddenHomeLease);
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with { OutputDirectoryPath = forbiddenHomeOutput }, token),
        "user profile",
        "user-profile output topology");
    Require(homeOutputBefore == (Directory.Exists(forbiddenHomeOutput) || File.Exists(forbiddenHomeOutput)) &&
            homeLeaseBefore == (Directory.Exists(forbiddenHomeLease) || File.Exists(forbiddenHomeLease)),
        "A forbidden user-profile output mutated its output or lease path.");
    int requestBoundaryRejects = await VerifyRequestBoundaryRejectsAsync(request, token, lockedCuePath);
    Require(!Directory.Exists(outputParent),
        "A wrong source/CUE/mobile request mutated the publication parent before the first valid publish.");

    CultureInfo originalCulture = CultureInfo.CurrentCulture;
    CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
    ushort originalUmask = NativeUmask(0);
    Id65AuthoringRewardStagingRuntimeCandidateResult first;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-EG");
        first = await InvokeVerifiedPublisherAsync(request, token);
    }
    finally
    {
        CultureInfo.CurrentCulture = originalCulture;
        CultureInfo.CurrentUICulture = originalUiCulture;
        _ = NativeUmask(originalUmask);
    }
    VerifyFrozenResult(first);
    VerifyPublishedFilesystemRoles(first.Paths, outputParent);
    VerifyCanonicalSidecarReader(smokeRoot, first.Paths.ConstructionPlanPath);
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot = SnapshotCandidate(first.Paths);
    Console.WriteLine("  checkpoint=initial-publication-and-canonical-readback");
    VerifyLiveFingerprintMutationRestorationMatrix(first.Paths, cleanSnapshot);

    ushort partialCopyUmask = NativeUmask(0);
    bool exactPartialImageHookReached = false;
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage != "after-staged-image-created-exact-mode")
                        return;
                    exactPartialImageHookReached = true;
                    throw new IOException("stop after exact partial image creation");
                }
            }, token),
            "stop after exact partial image creation",
            "umask000 exact partial-image rollback");
    }
    finally
    {
        _ = NativeUmask(partialCopyUmask);
    }
    Require(exactPartialImageHookReached &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "The umask000 partial-image fault left wrong-mode stage/operation debris or changed prior output.");

    bool stagedLeafModeTampered = false;
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "after-staged-candidate-verified")
                    return;
                string operationRoot = Directory.EnumerateDirectories(first.Paths.OperationsDirectoryPath).Single();
                string stagedChecklist = Path.Combine(
                    operationRoot,
                    "candidate",
                    Path.GetFileName(first.Paths.RuntimeChecklistPath));
                SetMacMode(
                    stagedChecklist,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
                stagedLeafModeTampered = true;
            }
        }, token),
        "staged data-leaf mode hook tamper");
    Require(stagedLeafModeTampered && Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "The staged data-leaf mode hook tamper was not preserved fail-closed.");
    string heldWrongModeOperation = Directory.EnumerateDirectories(first.Paths.OperationsDirectoryPath).Single();
    string heldWrongModeChecklist = Path.Combine(
        heldWrongModeOperation,
        "candidate",
        Path.GetFileName(first.Paths.RuntimeChecklistPath));
    File.SetUnixFileMode(
        heldWrongModeChecklist,
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    await VerifyPersistedJournalFingerprintGuardMatrixAsync(
        request,
        token,
        first.Paths,
        heldWrongModeOperation,
        cleanSnapshot,
        smokeRoot);
    await RecoverOnlyAsync(request, token, first.Paths, "restored staged mode-tamper recovery");
    Require(!Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "Restored staged mode tamper did not recover exact prior output.");

    bool publishedLeafModeTampered = false;
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "after-candidate-publication")
                    return;
                SetMacMode(
                    first.Paths.RuntimeChecklistPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
                publishedLeafModeTampered = true;
            }
        }, token),
        "published data-leaf mode hook tamper");
    Require(publishedLeafModeTampered && Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            File.GetUnixFileMode(first.Paths.RuntimeChecklistPath).HasFlag(UnixFileMode.OtherWrite),
        "The published data-leaf mode hook tamper was deleted instead of preserved.");
    File.SetUnixFileMode(
        first.Paths.RuntimeChecklistPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    await RecoverOnlyAsync(request, token, first.Paths, "restored published mode-tamper recovery");
    Require(!Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "Restored published mode tamper did not recover exact prior output.");

    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "before-operation-root-create")
                    throw new IOException("stop before operation-root create");
            }
        }, token),
        "stop before operation-root create",
        "pre-operation-root failure atomicity");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "A pre-operation-root creation failure left its newly created operations directory behind.");

    byte[] evidenceArtifactBefore = File.ReadAllBytes(fullReadbackFixture.FirstArtifactPath);
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage == "after-global-writer-lease-acquired")
                    {
                        byte[] changed = evidenceArtifactBefore.ToArray();
                        changed[0] ^= 1;
                        File.WriteAllBytes(fullReadbackFixture.FirstArtifactPath, changed);
                    }
                }
            }, token),
            "FullReadback FocusedRuntimePass",
            "evidence mutation after writer lease");
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
                !Directory.Exists(first.Paths.OperationsDirectoryPath),
            "Evidence freshness rejection changed output or created an operation.");
    }
    finally
    {
        File.WriteAllBytes(fullReadbackFixture.FirstArtifactPath, evidenceArtifactBefore);
    }

    string escapedOperationsTarget = Path.Combine(smokeRoot, "must-remain-empty-operations-target");
    Directory.CreateDirectory(escapedOperationsTarget);
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "after-owned-recovery")
                {
                    Directory.CreateSymbolicLink(
                        first.Paths.OperationsDirectoryPath,
                        escapedOperationsTarget);
                }
            }
        }, token),
        "symbolic",
        "post-recovery operation-parent symlink");
    Require(!Directory.EnumerateFileSystemEntries(escapedOperationsTarget).Any() &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "The post-recovery hook wrote through an out-of-tree operations symlink or changed output.");
    if (Directory.Exists(first.Paths.OperationsDirectoryPath))
        Directory.Delete(first.Paths.OperationsDirectoryPath);

    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request, token),
        "already exists",
        "replace-existing false");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "ReplaceExistingCandidate=false changed the exact prior output.");

    bool nestedWriterRejected = false;
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "after-global-writer-lease-acquired")
                    return;
                try
                {
                    _ = InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token)
                        .GetAwaiter().GetResult();
                }
                catch (Exception exception) when (
                    exception.ToString().Contains("writer is active", StringComparison.OrdinalIgnoreCase) ||
                    exception.ToString().Contains("lease file could not be opened", StringComparison.OrdinalIgnoreCase) ||
                    exception.ToString().Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
                {
                    nestedWriterRejected = true;
                }
                throw new IOException("stop after nested writer check");
            }
        }, token),
        "stop after nested writer check",
        "active global writer lease");
    Require(nestedWriterRejected && SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "The OS-backed global writer lease did not reject a concurrent publisher cleanly.");

    string replacedGlobalLease = Path.Combine(smokeRoot, "held-replaced-global-writer.lease");
    bool globalLeaseReplaced = false;
    try
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage != "after-created-journal" || globalLeaseReplaced)
                        return;
                    File.Move(first.Paths.GlobalWriterLeasePath, replacedGlobalLease);
                    File.WriteAllBytes(first.Paths.GlobalWriterLeasePath, []);
                    SetMacMode(
                        first.Paths.GlobalWriterLeasePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    globalLeaseReplaced = true;
                }
            }, token),
            "mid-session global writer lease replacement");
        Require(globalLeaseReplaced && File.Exists(first.Paths.GlobalWriterLeasePath) &&
                File.Exists(replacedGlobalLease) &&
                Directory.Exists(first.Paths.OperationsDirectoryPath),
            "Mid-session global-lease replacement was not preserved fail-closed.");
    }
    finally
    {
        if (globalLeaseReplaced)
        {
            File.Delete(first.Paths.GlobalWriterLeasePath);
            File.Move(replacedGlobalLease, first.Paths.GlobalWriterLeasePath);
        }
    }
    await RecoverOnlyAsync(request, token, first.Paths, "global-lease replacement recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "Restored global-lease replacement did not recover the exact prior output.");

    string committedCleanupReplacedGlobalLease = Path.Combine(
        smokeRoot,
        "held-committed-cleanup-global-writer.lease");
    bool committedCleanupLeaseReplaced = false;
    try
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage != "before-committed-backup-cleanup" ||
                        committedCleanupLeaseReplaced)
                    {
                        return;
                    }
                    File.Move(first.Paths.GlobalWriterLeasePath, committedCleanupReplacedGlobalLease);
                    File.WriteAllBytes(first.Paths.GlobalWriterLeasePath, []);
                    SetMacMode(
                        first.Paths.GlobalWriterLeasePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    committedCleanupLeaseReplaced = true;
                }
            }, token),
            "committed backup-cleanup global writer lease replacement");
        Require(committedCleanupLeaseReplaced &&
                File.Exists(first.Paths.GlobalWriterLeasePath) &&
                File.Exists(committedCleanupReplacedGlobalLease) &&
                Directory.Exists(first.Paths.OperationsDirectoryPath),
            "Committed cleanup global-lease replacement was not preserved fail-closed.");
    }
    finally
    {
        if (committedCleanupLeaseReplaced)
        {
            File.Delete(first.Paths.GlobalWriterLeasePath);
            File.Move(committedCleanupReplacedGlobalLease, first.Paths.GlobalWriterLeasePath);
        }
    }
    await RecoverOnlyAsync(request, token, first.Paths, "committed cleanup lease replacement recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "Restored committed-cleanup lease replacement did not retain the exact committed output.");

    string[] sevenPaths = CandidatePaths(first.Paths);
    int missingLeafRejects = 0;
    foreach (string leaf in sevenPaths)
    {
        string held = Path.Combine(smokeRoot, "held-missing-" + Path.GetFileName(leaf));
        File.Move(leaf, held);
        try
        {
            await RejectAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "seven-file",
                "missing leaf " + Path.GetFileName(leaf));
            Require(File.Exists(held) && !File.Exists(leaf),
                "A missing-leaf rejection recreated or deleted the held leaf.");
            missingLeafRejects++;
        }
        finally
        {
            if (File.Exists(held))
                File.Move(held, leaf);
        }
    }
    Require(missingLeafRejects == 7 && SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "The exact seven missing-leaf rejection matrix did not preserve candidate bytes/modes.");

    string extraPath = Path.Combine(first.Paths.OutputDirectoryPath, "foreign-extra.txt");
    File.WriteAllBytes(extraPath, Encoding.UTF8.GetBytes("foreign\n"));
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "seven-file",
            "foreign extra leaf");
        Require(File.Exists(extraPath), "The publisher deleted a foreign extra leaf on rejection.");
    }
    finally
    {
        if (File.Exists(extraPath))
            File.Delete(extraPath);
    }

    string renamedOriginal = first.Paths.LocationGuidePath;
    string renamedLeaf = Path.Combine(first.Paths.OutputDirectoryPath, "foreign-renamed-guide.svg");
    File.Move(renamedOriginal, renamedLeaf);
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "contains a foreign leaf: foreign-renamed-guide.svg",
            "renamed exact leaf");
        Require(File.Exists(renamedLeaf) && !File.Exists(renamedOriginal) &&
                !Directory.Exists(first.Paths.OperationsDirectoryPath),
            "The publisher moved or deleted a foreign renamed leaf on rejection.");
    }
    finally
    {
        if (File.Exists(renamedLeaf))
            File.Move(renamedLeaf, renamedOriginal);
    }

    string hardlinkHeld = Path.Combine(smokeRoot, "held-hardlink-checklist.md");
    File.Move(first.Paths.RuntimeChecklistPath, hardlinkHeld);
    Require(NativeLink(hardlinkHeld, first.Paths.RuntimeChecklistPath) == 0,
        "Could not create the output-leaf hardlink fixture.");
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "st_nlink",
            "hardlinked output leaf");
        Require(File.Exists(first.Paths.RuntimeChecklistPath) && File.Exists(hardlinkHeld),
            "The hardlinked output leaf was moved or deleted on rejection.");
    }
    finally
    {
        if (File.Exists(first.Paths.RuntimeChecklistPath))
            File.Delete(first.Paths.RuntimeChecklistPath);
        if (File.Exists(hardlinkHeld))
            File.Move(hardlinkHeld, first.Paths.RuntimeChecklistPath);
    }

    string fifoHeld = Path.Combine(smokeRoot, "held-fifo-guide.svg");
    File.Move(first.Paths.LocationGuidePath, fifoHeld);
    Require(NativeMkFifo(first.Paths.LocationGuidePath, Convert.ToUInt16("600", 8)) == 0,
        "Could not create the output-leaf FIFO fixture.");
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "regular file",
            "FIFO output leaf");
    }
    finally
    {
        File.Delete(first.Paths.LocationGuidePath);
        File.Move(fifoHeld, first.Paths.LocationGuidePath);
    }

    string leaseHeld = Path.Combine(smokeRoot, "held-global-writer.lease");
    File.Move(first.Paths.GlobalWriterLeasePath, leaseHeld);
    Require(NativeMkFifo(first.Paths.GlobalWriterLeasePath, Convert.ToUInt16("600", 8)) == 0,
        "Could not create the global-lease FIFO fixture.");
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "regular file",
            "FIFO global writer lease");
    }
    finally
    {
        File.Delete(first.Paths.GlobalWriterLeasePath);
        File.Move(leaseHeld, first.Paths.GlobalWriterLeasePath);
    }
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "A structural/lease rejection changed the exact prior candidate.");

    int artifactTamperRejects = await VerifyExistingCandidateTamperMatrixAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        smokeRoot);
    Console.WriteLine($"  checkpoint=artifact-tamper-matrix rejects={artifactTamperRejects}");
    int inputFreshnessRejects = await VerifyInputFreshnessHooksAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        new[]
        {
            (lockedImagePath, lockedBefore, "locked base BIN"),
            (lockedCuePath, cueBefore, "locked base CUE"),
            (foundationImagePath, foundationBefore, "foundation donor BIN")
        });
    Console.WriteLine($"  checkpoint=input-freshness-matrix rejects={inputFreshnessRejects}");

    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "before-initial-journal-write")
                    throw new IOException("synthetic initial journal fault");
            }
        }, token),
        "initial journal fault",
        "initial journal fault");
    Require(!Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "An initial-journal fault leaked an operation lease/root or changed the prior candidate.");

    string escapedInitialJournalTarget = Path.Combine(smokeRoot, "must-remain-empty-initial-journal-target");
    Directory.CreateDirectory(escapedInitialJournalTarget);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "before-initial-journal-write")
                    return;
                string operationRoot = Directory.EnumerateDirectories(first.Paths.OperationsDirectoryPath).Single();
                File.Delete(Path.Combine(operationRoot, "operation.lease"));
                Directory.Delete(operationRoot);
                Directory.CreateSymbolicLink(operationRoot, escapedInitialJournalTarget);
            }
        }, token),
        "initial-journal operation-root symlink replacement");
    Require(!Directory.EnumerateFileSystemEntries(escapedInitialJournalTarget).Any() &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "The initial-journal hook wrote through an out-of-tree operation-root symlink.");
    string escapedOperationLink = Directory.EnumerateFileSystemEntries(first.Paths.OperationsDirectoryPath).Single();
    Directory.Delete(escapedOperationLink);
    Directory.Delete(first.Paths.OperationsDirectoryPath);

    int postHookTopologyRejects = await VerifyPostHookTopologyGuardMatrixAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        smokeRoot);
    Console.WriteLine($"  checkpoint=post-hook-topology-guards rejects={postHookTopologyRejects}");

    CoordinatedTamper tamper = ApplyCoordinatedTamper(first.Paths);
    IReadOnlyDictionary<string, FileSnapshot> coordinatedTampered = SnapshotCandidate(first.Paths);
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "exact recomputation",
        "coordinated sidecar and receipt tamper");
    Require(SnapshotsEqual(coordinatedTampered, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "Coordinated tamper rejection moved, deleted, or rewrote the existing candidate.");
    tamper.Restore();
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "The coordinated-tamper fixture did not restore exact candidate bytes.");

    CoordinatedTamper? stagedTamper = null;
    IReadOnlyDictionary<string, FileSnapshot>? stagedTamperedSnapshot = null;
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "after-staged-candidate-verified")
                    return;
                stagedTamper = ApplyCoordinatedTamper(first.Paths);
                stagedTamperedSnapshot = SnapshotCandidate(first.Paths);
            }
        }, token),
        "exact recomputation",
        "replacement TOCTOU tamper");
    Require(stagedTamper is not null && stagedTamperedSnapshot is not null &&
            SnapshotsEqual(stagedTamperedSnapshot, SnapshotCandidate(first.Paths)) &&
            Directory.Exists(first.Paths.OperationsDirectoryPath),
        "Replacement TOCTOU rejection moved/deleted the prior output or discarded its audit state.");
    stagedTamper!.Restore();
    await RecoverOnlyAsync(request, token, first.Paths, "restored replacement-TOCTOU recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath),
        "The restored TOCTOU fixture did not recover the exact prior output and clean its stage.");
    Console.WriteLine("  checkpoint=coordinated-and-toctou-preservation");

    string[] precommitFaultStages =
    [
        "after-created-journal",
        "after-staged-candidate-verified",
        "after-backup-intent-journal",
        "after-previous-candidate-backup",
        "after-publish-intent-journal",
        "after-candidate-publication"
    ];
    int atomicPrecommitFaultRejects = 0;
    foreach (string faultStage in precommitFaultStages)
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage == faultStage)
                        throw new IOException("synthetic atomic phase fault: " + faultStage);
                }
            }, token),
            faultStage,
            "atomic phase " + faultStage);
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)) &&
                !Directory.Exists(first.Paths.OperationsDirectoryPath),
            $"Atomic precommit fault {faultStage} did not restore the exact old seven files.");
        atomicPrecommitFaultRejects++;
    }
    Require(atomicPrecommitFaultRejects == 6,
        "The atomic exception-rollback matrix did not cover all six journal phases.");
    Console.WriteLine($"  checkpoint=atomic-exception-rollback count={atomicPrecommitFaultRejects}");

    int recoveryGuardRejects = await VerifyRecoveryGuardMatrixAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        smokeRoot);
    (int preCommittedPhaseBoundaryRecoveries, int cleanupResidueRecoveries) =
        await VerifyPhaseBoundaryProcessCrashRecoveryMatrixAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        smokeRoot);
    Console.WriteLine(
        $"  checkpoint=startup-recovery guards={recoveryGuardRejects} " +
        $"phaseBoundaryRecovered={preCommittedPhaseBoundaryRecoveries} " +
        $"cleanupResidues={cleanupResidueRecoveries}");
    await VerifySameShapedForeignRollbackBoundaryAsync(
        request,
        token,
        first.Paths,
        cleanSnapshot,
        smokeRoot);

    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "after-committed-journal")
                    throw new IOException("synthetic committed cleanup fault");
            }
        }, token),
        "committed successfully",
        "postcommit preservation");
    Require(Directory.Exists(first.Paths.OutputDirectoryPath) &&
            Directory.Exists(first.Paths.OperationsDirectoryPath),
        "A postcommit fault removed the committed output or its recovery journal.");
    Id65AuthoringRewardStagingRuntimeCandidateResult recovered = await InvokeVerifiedPublisherAsync(
        request with { ReplaceExistingCandidate = true },
        token);
    Require(recovered.RollbackRecoveryVerified && !recovered.Receipt.RollbackRecoveryVerified &&
            !recovered.ProcessCrashRecoveryVerified &&
            recovered.PhaseBoundaryProcessCrashRecoveryVerified &&
            !recovered.MidMutationProcessCrashRecoveryVerified && !recovered.PowerLossDurabilityVerified &&
            !Directory.Exists(first.Paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(first.Paths)),
        "Committed startup recovery did not set only the in-memory recovery bit and preserve exact output.");
    int phaseBoundaryCrashRecoveries = preCommittedPhaseBoundaryRecoveries + 1;
    Require(phaseBoundaryCrashRecoveries == 9 && cleanupResidueRecoveries == 3,
        "The exact nine journal-phase recoveries or three conservative cleanup residues were not exercised.");

    Require(SnapshotFile(lockedImagePath) == lockedBefore && SnapshotFile(lockedCuePath) == cueBefore &&
            SnapshotFile(foundationImagePath) == foundationBefore,
        "The publisher smoke mutated a locked base, CUE, or foundation donor.");
    Console.WriteLine($"  temp-output={first.Paths.OutputDirectoryPath}");
    Console.WriteLine($"  bin={first.OutputImageSha256}; wad={first.OutputWadSha256}; row={first.OutputRow80Sha256}");
    Console.WriteLine($"  raw={first.RawSectorDiffSha256}; sectors={first.RebuiltRawSectorCount}; physical={first.ChangedPhysicalImageBytes}");
    Console.WriteLine(
        "PASS Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke: production PENDING zero-mutation gate, " +
        "fixture-derived opaque v3 capability, invariant Core4-171 seven-file transaction, exact 579-sector " +
        $"physical receipt, {requestBoundaryRejects} request-boundary rejects, {artifactTamperRejects} artifact " +
        $"tamper rejects, {inputFreshnessRejects} source-freshness rejects, " +
        $"{atomicPrecommitFaultRejects} atomic exception rollbacks, {postHookTopologyRejects} post-hook topology " +
        $"rejects, {recoveryGuardRejects} recovery guard rejects, {phaseBoundaryCrashRecoveries} exact journal-phase " +
        $"process-crash recoveries, {cleanupResidueRecoveries} conservative residue cleanups, foreign-tree " +
        "preservation, and committed recovery all passed; broad/mid-mutation " +
        "process-crash and power-loss durability remain explicitly false with manual-audit residue disclosed.");
}
finally
{
    if (keep)
        Console.WriteLine($"  retained-smoke-root={smokeRoot}");
    else if (Directory.Exists(smokeRoot))
    {
        DeleteOwnedSmokeRoot(smokeRoot, smokeRootIdentity);
        Require(!Directory.Exists(smokeRoot) && !File.Exists(smokeRoot),
            "The publisher smoke left owned temporary debris behind.");
    }
}

}

static Id65RewardStagingCompileRequest BuildCompileRequest(string lockedImagePath, string foundationImagePath)
{
    Id65RewardCensusSourceSnapshot snapshot = Id65RewardCensusContract.ReadLockedSource(lockedImagePath);
    Id65RewardCensus census = Id65RewardCensusContract.Inspect(snapshot);
    Id65AuthoringSupportManifest empty = Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest();
    Id65AuthoringSupportManifest core = Id65AuthoringSupportReplacementCompiler.AddCoreSupport(empty);
    Id65AuthoringSupportManifest full = Id65AuthoringSupportReplacementCompiler.AddEnemyBay(core);
    Id65RewardSupportLayout layout = Id65RewardSupportLayoutContract.Build(census, core, full);
    UnusedLevel65NativeTextureStaticPlan oldTextureProof =
        UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
    Id65V2NativeTextureWitness textureWitness =
        Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
    UnusedLevel65MobyDependencyBundleContract grassContract =
        UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
    byte[] grassPackage = ReadWad(foundationImagePath, 0x9C1D20, 0x174);
    Id65MobyDependencyBundleDescriptor grassBundle =
        Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(grassContract, grassPackage);
    Id65RewardStagingLockedSource locked = Id65AuthoringRewardStagingCompiler.CaptureLockedSource(
        snapshot.Id65Data.Span,
        snapshot.Id65Overlay.Span,
        snapshot.Executable.Span,
        textureWitness);
    Id65RewardStagingManifest manifest = Id65AuthoringRewardStagingCompiler.CreateCore4Manifest(
        census,
        layout,
        core,
        full,
        grassBundle);
    return new(locked, manifest, census, layout, core, full, grassBundle);
}

static async Task<int> VerifyRequestBoundaryRejectsAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    string lockedCuePath)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The publisher artifact smoke requires macOS.");
    int rejects = 0;
    foreach ((string Label, Id65AuthoringRewardStagingRuntimeCandidateRequest Request) test in new[]
    {
        ("wrong base path", request with { BaseImagePath = lockedCuePath }),
        ("wrong CUE path", request with { BaseCuePath = request.BaseImagePath }),
        ("forbidden mobile request", request with
        {
            CompileRequest = request.CompileRequest with { AuthorMobileEnemies = true }
        })
    })
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(test.Request, token),
            test.Label);
        rejects++;
    }
    return rejects;
}

static async Task<int> VerifyExistingCandidateTamperMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    string smokeRoot)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The publisher artifact smoke requires macOS.");
    int rejects = 0;
    string[] candidatePaths = CandidatePaths(paths);
    foreach (string path in candidatePaths)
    {
        long offset = path == paths.OutputImagePath ? 53_906L * 2352 + 24 : 0;
        byte original = ToggleFileByte(path, offset);
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "byte-tampered leaf " + Path.GetFileName(path));
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)) &&
                    !Directory.Exists(paths.OperationsDirectoryPath),
                "A byte-tampered candidate leaf was moved, deleted, or rewritten on rejection.");
            rejects++;
        }
        finally
        {
            WriteFileByte(path, offset, original);
        }
    }

    (string Label, long Offset)[] physicalOffsets =
    [
        ("affected header", 53_906L * 2352 + 12),
        ("affected duplicated subheader", 53_906L * 2352 + 18),
        ("affected payload", 53_906L * 2352 + 24),
        ("affected EDC", 53_906L * 2352 + 2072),
        ("affected ECC-P/no-reserved-slot", 53_906L * 2352 + 2076),
        ("affected ECC-Q", 53_906L * 2352 + 2248),
        ("outside-affected executable control", 55_382L * 2352 + 24)
    ];
    foreach ((string label, long offset) in physicalOffsets)
    {
        byte original = ToggleFileByte(paths.OutputImagePath, offset);
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                label + " physical tamper");
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)) &&
                    !Directory.Exists(paths.OperationsDirectoryPath),
                $"The {label} physical tamper was not preserved fail-closed.");
            rejects++;
        }
        finally
        {
            WriteFileByte(paths.OutputImagePath, offset, original);
        }
    }

    JsonSerializerOptions options = PublisherJsonOptions();
    byte[] receiptBytes = File.ReadAllBytes(paths.StaticReadbackReceiptPath);
    Id65AuthoringRewardStagingRuntimeCandidateReceipt receipt =
        JsonSerializer.Deserialize<Id65AuthoringRewardStagingRuntimeCandidateReceipt>(receiptBytes, options) ??
        throw new InvalidDataException("Could not deserialize the physical receipt tamper fixture.");
    (string Label, Func<Id65AuthoringRewardStagingRawSectorDiff[],
        Id65AuthoringRewardStagingRawSectorDiff[]> Mutate)[] rawDiffTamperCases =
    {
        ("raw category", (Id65AuthoringRewardStagingRawSectorDiff[] diffs) =>
        {
            diffs[0] = diffs[0] with
            {
                PayloadChangedBytes = diffs[0].PayloadChangedBytes + 1,
                TotalChangedBytes = diffs[0].TotalChangedBytes + 1
            };
            return diffs;
        }),
        ("raw reserved", (Id65AuthoringRewardStagingRawSectorDiff[] diffs) =>
        {
            diffs[0] = diffs[0] with
            {
                ReservedChangedBytes = 1,
                TotalChangedBytes = diffs[0].TotalChangedBytes + 1
            };
            return diffs;
        }),
        ("raw order", (Id65AuthoringRewardStagingRawSectorDiff[] diffs) =>
        {
            (diffs[0], diffs[1]) = (diffs[1], diffs[0]);
            return diffs;
        })
    };
    foreach ((string Label, Func<Id65AuthoringRewardStagingRawSectorDiff[],
                 Id65AuthoringRewardStagingRawSectorDiff[]> Mutate) test in rawDiffTamperCases)
    {
        Id65AuthoringRewardStagingRawSectorDiff[] diffs = test.Mutate(receipt.RawSectorDiffs.ToArray());
        WriteCanonical(paths.StaticReadbackReceiptPath, receipt with { RawSectorDiffs = diffs }, options);
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                test.Label + " receipt tamper");
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)) &&
                    !Directory.Exists(paths.OperationsDirectoryPath),
                $"The {test.Label} receipt tamper was rewritten or moved on rejection.");
            rejects++;
        }
        finally
        {
            File.WriteAllBytes(paths.StaticReadbackReceiptPath, receiptBytes);
        }
    }

    byte[] planBytes = File.ReadAllBytes(paths.ConstructionPlanPath);
    Id65AuthoringRewardStagingRuntimeCandidatePlan plan =
        JsonSerializer.Deserialize<Id65AuthoringRewardStagingRuntimeCandidatePlan>(planBytes, options) ??
        throw new InvalidDataException("Could not deserialize the substrate-identity plan tamper fixture.");
    (string Label, Func<Id65AuthoringRewardStagingRuntimeCandidatePlan,
        Id65AuthoringRewardStagingRuntimeCandidatePlan> Mutate)[] planSubstrateTamperCases =
    [
        ("plan schema downgrade", value => value with { SchemaVersion = 1 }),
        ("plan collision-v2 data", value => value with { CollisionWindingRepairV2OutputDataSha256 = new string('0', 64) }),
        ("plan collision-v2 model", value => value with { CollisionWindingRepairV2OutputModelSha256 = new string('0', 64) }),
        ("plan header-v3 data", value => value with { EnvironmentHeaderRepairV3OutputDataSha256 = new string('0', 64) }),
        ("plan header-v3 model", value => value with { EnvironmentHeaderRepairV3OutputModelSha256 = new string('0', 64) }),
        ("plan stale output BIN", value => value with
        {
            OutputImageSha256 = "d983a328da549c40f5a1e3e8b4a708274700e740399d5a5e63a6ce3d73cfc6b5"
        }),
        ("plan stale output WAD", value => value with
        {
            OutputWadSha256 = "87f7e7c99452e904a767a7c1fb235ada175a2ff20929162bf78b7b269a5d553d"
        }),
        ("plan stale raw-diff manifest", value => value with
        {
            RawSectorDiffSha256 = "615cab2cc06a6ce1bd0638f9a98985dc9c30eebac39a6d036d3f84cdd7c127a5"
        }),
        ("plan stale changed-physical total", value => value with { ChangedPhysicalImageBytes = 1_132_460 }),
        ("plan stale affected-raw afterimage", value => value with
        {
            AffectedRawSectorsAfterSha256 =
                "fba72f9cd72e90086f2a22de679b298f1824dc9cdec281953dd751175e47e1aa"
        }),
        ("plan stale affected-payload afterimage", value => value with
        {
            AffectedPayloadsAfterSha256 =
                "ba6ab947e8b97047095ade33f6c894365d3ad33faeb730dd26d2ada046be9aba"
        }),
        ("plan stale aggregate ECC-Q", value => value with
        {
            PhysicalAggregate = value.PhysicalAggregate with { EccQChangedBytes = 59_634 }
        }),
        ("plan stale aggregate total", value => value with
        {
            PhysicalAggregate = value.PhysicalAggregate with { TotalChangedBytes = 1_132_460 }
        })
    ];
    foreach ((string label, Func<Id65AuthoringRewardStagingRuntimeCandidatePlan,
                 Id65AuthoringRewardStagingRuntimeCandidatePlan> mutate) in planSubstrateTamperCases)
    {
        WriteCanonical(paths.ConstructionPlanPath, mutate(plan), options);
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "exact recomputation",
                label + " tamper");
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)),
                $"The {label} tamper was rewritten or moved on rejection.");
            rejects++;
        }
        finally
        {
            File.WriteAllBytes(paths.ConstructionPlanPath, planBytes);
        }
    }

    File.WriteAllBytes(
        paths.ConstructionPlanPath,
        BuildOldSchemaWithoutRequiredProperty(planBytes, "environmentHeaderRepairV3OutputModelSha256"));
    IReadOnlyDictionary<string, FileSnapshot> rawOldPlan = SnapshotCandidate(paths);
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "missing required properties",
            "raw old-schema plan missing required V3 substrate field");
        Require(SnapshotsEqual(rawOldPlan, SnapshotCandidate(paths)),
            "The raw old-schema plan tamper was rewritten or moved on rejection.");
        rejects++;
    }
    finally
    {
        File.WriteAllBytes(paths.ConstructionPlanPath, planBytes);
    }

    (string Label, Func<Id65AuthoringRewardStagingRuntimeCandidateReceipt,
        Id65AuthoringRewardStagingRuntimeCandidateReceipt> Mutate)[] receiptSubstrateTamperCases =
    [
        ("receipt schema downgrade", value => value with { SchemaVersion = 1 }),
        ("receipt collision-v2 data", value => value with { CollisionWindingRepairV2OutputDataSha256 = new string('0', 64) }),
        ("receipt collision-v2 model", value => value with { CollisionWindingRepairV2OutputModelSha256 = new string('0', 64) }),
        ("receipt header-v3 data", value => value with { EnvironmentHeaderRepairV3OutputDataSha256 = new string('0', 64) }),
        ("receipt header-v3 model", value => value with { EnvironmentHeaderRepairV3OutputModelSha256 = new string('0', 64) }),
        ("receipt stale output BIN", value => value with
        {
            OutputImageSha256 = "d983a328da549c40f5a1e3e8b4a708274700e740399d5a5e63a6ce3d73cfc6b5"
        }),
        ("receipt stale output WAD", value => value with
        {
            OutputWadSha256 = "87f7e7c99452e904a767a7c1fb235ada175a2ff20929162bf78b7b269a5d553d"
        }),
        ("receipt stale raw-diff manifest", value => value with
        {
            RawSectorDiffSha256 = "615cab2cc06a6ce1bd0638f9a98985dc9c30eebac39a6d036d3f84cdd7c127a5"
        }),
        ("receipt stale changed-physical total", value => value with { ChangedPhysicalImageBytes = 1_132_460 }),
        ("receipt stale aggregate ECC-Q", value => value with
        {
            PhysicalAggregate = value.PhysicalAggregate with { EccQChangedBytes = 59_634 }
        }),
        ("receipt stale aggregate total", value => value with
        {
            PhysicalAggregate = value.PhysicalAggregate with { TotalChangedBytes = 1_132_460 }
        })
    ];
    foreach ((string label, Func<Id65AuthoringRewardStagingRuntimeCandidateReceipt,
                 Id65AuthoringRewardStagingRuntimeCandidateReceipt> mutate) in receiptSubstrateTamperCases)
    {
        WriteCanonical(paths.StaticReadbackReceiptPath, mutate(receipt), options);
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "exact recomputation",
                label + " tamper");
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)),
                $"The {label} tamper was rewritten or moved on rejection.");
            rejects++;
        }
        finally
        {
            File.WriteAllBytes(paths.StaticReadbackReceiptPath, receiptBytes);
        }
    }

    File.WriteAllBytes(
        paths.StaticReadbackReceiptPath,
        BuildOldSchemaWithoutRequiredProperty(receiptBytes, "environmentHeaderRepairV3OutputModelSha256"));
    IReadOnlyDictionary<string, FileSnapshot> rawOldReceipt = SnapshotCandidate(paths);
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "missing required properties",
            "raw old-schema receipt missing required V3 substrate field");
        Require(SnapshotsEqual(rawOldReceipt, SnapshotCandidate(paths)),
            "The raw old-schema receipt tamper was rewritten or moved on rejection.");
        rejects++;
    }
    finally
    {
        File.WriteAllBytes(paths.StaticReadbackReceiptPath, receiptBytes);
    }

    rejects += await VerifyV3OutputNameTamperMatrixAsync(
        request,
        token,
        paths,
        cleanSnapshot);

    UnixFileMode mode0600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    UnixFileMode mode0644 = mode0600 | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    UnixFileMode mode0666 = mode0644 | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    UnixFileMode mode0700 = mode0600 | UnixFileMode.UserExecute;
    UnixFileMode mode0755 = mode0700 |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    UnixFileMode mode0777 = mode0755 | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    string[] dataLeaves =
    [
        paths.OutputImagePath,
        paths.OutputCuePath,
        paths.ConstructionPlanPath,
        paths.StaticReadbackReceiptPath,
        paths.RuntimeChecklistPath,
        paths.LocationGuidePath
    ];
    foreach (string dataLeaf in dataLeaves)
    {
        File.SetUnixFileMode(dataLeaf, mode0600);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                $"data-leaf 0600 tamper {Path.GetFileName(dataLeaf)}");
            Require(File.GetUnixFileMode(dataLeaf) == mode0600,
                "Data-leaf mode rejection silently repaired an existing leaf.");
            rejects++;
        }
        finally
        {
            File.SetUnixFileMode(dataLeaf, mode0644);
        }
    }

    File.SetUnixFileMode(paths.RuntimeChecklistPath, mode0666);
    try
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "data-leaf 0666 tamper");
        Require(File.GetUnixFileMode(paths.RuntimeChecklistPath) == mode0666,
            "Writable data-leaf rejection silently repaired the existing leaf.");
        rejects++;
    }
    finally
    {
        File.SetUnixFileMode(paths.RuntimeChecklistPath, mode0644);
    }

    foreach (UnixFileMode wrongHelperMode in new[] { mode0644, mode0777 })
    {
        File.SetUnixFileMode(paths.FinderHelperPath, wrongHelperMode);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "Finder helper mode tamper");
            Require(File.GetUnixFileMode(paths.FinderHelperPath) == wrongHelperMode,
                "Finder mode rejection silently repaired the existing helper.");
            rejects++;
        }
        finally
        {
            File.SetUnixFileMode(paths.FinderHelperPath, mode0755);
        }
    }

    foreach (UnixFileMode wrongOutputMode in new[] { mode0700, mode0777 })
    {
        File.SetUnixFileMode(paths.OutputDirectoryPath, wrongOutputMode);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "output-directory mode tamper");
            Require(File.GetUnixFileMode(paths.OutputDirectoryPath) == wrongOutputMode,
                "Output-directory mode rejection silently repaired the existing directory.");
            rejects++;
        }
        finally
        {
            File.SetUnixFileMode(paths.OutputDirectoryPath, mode0755);
        }
    }

    foreach (UnixFileMode wrongLeaseMode in new[] { mode0644, mode0666 })
    {
        File.SetUnixFileMode(paths.GlobalWriterLeasePath, wrongLeaseMode);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                "global-writer-lease mode tamper");
            Require(File.GetUnixFileMode(paths.GlobalWriterLeasePath) == wrongLeaseMode,
                "Global-lease mode rejection silently repaired the existing lease.");
            rejects++;
        }
        finally
        {
            File.SetUnixFileMode(paths.GlobalWriterLeasePath, mode0600);
        }
    }

    string symlinkHeld = Path.Combine(smokeRoot, "held-symlink-cue");
    File.Move(paths.OutputCuePath, symlinkHeld);
    File.CreateSymbolicLink(paths.OutputCuePath, symlinkHeld);
    try
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "symlinked output leaf");
        Require((File.GetAttributes(paths.OutputCuePath) & FileAttributes.ReparsePoint) != 0,
            "The symlinked output leaf was removed on rejection.");
        rejects++;
    }
    finally
    {
        File.Delete(paths.OutputCuePath);
        File.Move(symlinkHeld, paths.OutputCuePath);
    }

    foreach ((string Label, string Path, int ByteLength) test in new[]
    {
        ("oversized plan", paths.ConstructionPlanPath, 4 * 1024 * 1024 + 1),
        ("oversized receipt", paths.StaticReadbackReceiptPath, 8 * 1024 * 1024 + 1),
        ("oversized checklist", paths.RuntimeChecklistPath, 1024 * 1024 + 1),
        ("oversized guide", paths.LocationGuidePath, 1024 * 1024 + 1)
    })
    {
        byte[] original = File.ReadAllBytes(test.Path);
        File.WriteAllBytes(test.Path, new byte[test.ByteLength]);
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                test.Label);
            Require(new FileInfo(test.Path).Length == test.ByteLength,
                $"The {test.Label} was replaced or truncated on rejection.");
            rejects++;
        }
        finally
        {
            File.WriteAllBytes(test.Path, original);
        }
    }

    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
            !Directory.Exists(paths.OperationsDirectoryPath),
        "The executable artifact-tamper matrix did not restore the clean exact candidate.");
    return rejects;
}

static async Task<int> VerifyV3OutputNameTamperMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot)
{
    const string v3Prefix =
        "Unused-Level-65-Authoring-Reward-Staging-Core4-171-V3-RUNTIME-CANDIDATE";
    const string legacyPrefix =
        "Unused-Level-65-Authoring-Reward-Staging-Core4-171-RUNTIME-CANDIDATE";
    byte[] exactV3Cue = ExactV3CueBytes();
    byte[] legacyCue = LegacyCueBytes();
    byte[] legacyHelper = LegacyFinderHelperBytes();
    Dictionary<string, byte[]> originals = new(StringComparer.Ordinal)
    {
        [paths.OutputCuePath] = File.ReadAllBytes(paths.OutputCuePath),
        [paths.FinderHelperPath] = File.ReadAllBytes(paths.FinderHelperPath),
        [paths.RuntimeChecklistPath] = File.ReadAllBytes(paths.RuntimeChecklistPath),
        [paths.StaticReadbackReceiptPath] = File.ReadAllBytes(paths.StaticReadbackReceiptPath)
    };
    JsonSerializerOptions options = PublisherJsonOptions();
    Id65AuthoringRewardStagingRuntimeCandidateReceipt receipt =
        JsonSerializer.Deserialize<Id65AuthoringRewardStagingRuntimeCandidateReceipt>(
            originals[paths.StaticReadbackReceiptPath],
            options) ?? throw new InvalidDataException("Could not deserialize the output-name receipt fixture.");
    string exactChecklist = Encoding.UTF8.GetString(originals[paths.RuntimeChecklistPath]);
    string legacyChecklist = exactChecklist.Replace(v3Prefix, legacyPrefix, StringComparison.Ordinal);
    Require(legacyChecklist != exactChecklist && !legacyChecklist.Contains(v3Prefix, StringComparison.Ordinal),
        "The mixed legacy checklist fixture did not replace every exact V3 handoff basename.");
    byte[] legacyChecklistBytes = Encoding.UTF8.GetBytes(legacyChecklist);
    int rejects = 0;

    async Task RejectTamperAsync(string label, string fragment, Action mutate)
    {
        mutate();
        IReadOnlyDictionary<string, FileSnapshot> tampered = SnapshotCandidate(paths);
        try
        {
            await RejectAsync(
                () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                fragment,
                label);
            Require(SnapshotsEqual(tampered, SnapshotCandidate(paths)) &&
                    !Directory.Exists(paths.OperationsDirectoryPath),
                $"The {label} was rewritten, moved, or advanced into an operation on rejection.");
            rejects++;
        }
        finally
        {
            foreach ((string path, byte[] bytes) in originals)
                File.WriteAllBytes(path, bytes);
        }
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
            $"The {label} did not restore the exact clean V3 candidate.");
    }

    await RejectTamperAsync(
        "legacy 131-byte CUE",
        "expected BIN",
        () => File.WriteAllBytes(paths.OutputCuePath, legacyCue));
    await RejectTamperAsync(
        "legacy 277-byte Finder helper",
        "Finder helper",
        () => File.WriteAllBytes(paths.FinderHelperPath, legacyHelper));
    await RejectTamperAsync(
        "mixed legacy helper cue_name and checklist",
        "checklist",
        () =>
        {
            File.WriteAllBytes(paths.FinderHelperPath, legacyHelper);
            File.WriteAllBytes(paths.RuntimeChecklistPath, legacyChecklistBytes);
        });
    await RejectTamperAsync(
        "self-consistent rechained legacy handoff receipt",
        "expected BIN",
        () =>
        {
            File.WriteAllBytes(paths.OutputCuePath, legacyCue);
            File.WriteAllBytes(paths.FinderHelperPath, legacyHelper);
            File.WriteAllBytes(paths.RuntimeChecklistPath, legacyChecklistBytes);
            Id65AuthoringRewardStagingRuntimeCandidateReceipt legacyReceipt = receipt with
            {
                OutputCueSha256 = HashBytes(legacyCue),
                RuntimeChecklistSha256 = HashBytes(legacyChecklistBytes),
                FinderHelperSha256 = HashBytes(legacyHelper)
            };
            WriteCanonical(paths.StaticReadbackReceiptPath, legacyReceipt, options);
            Require(
                legacyReceipt.OutputCueSha256 == HashFile(paths.OutputCuePath) &&
                legacyReceipt.RuntimeChecklistSha256 == HashFile(paths.RuntimeChecklistPath) &&
                legacyReceipt.FinderHelperSha256 == HashFile(paths.FinderHelperPath),
                "The coordinated legacy receipt was not fully rechained to its downgraded leaves.");
        });

    string exactCueText = Encoding.ASCII.GetString(exactV3Cue);
    await RejectTamperAsync(
        "CRLF V3 CUE",
        "CUE bytes changed",
        () => File.WriteAllBytes(
            paths.OutputCuePath,
            Encoding.ASCII.GetBytes(exactCueText.Replace("\n", "\r\n", StringComparison.Ordinal))));
    await RejectTamperAsync(
        "UTF8-BOM V3 CUE",
        "The owned Core4 reward CUE bytes changed.",
        () => File.WriteAllBytes(paths.OutputCuePath, [0xEF, 0xBB, 0xBF, .. exactV3Cue]));
    await RejectTamperAsync(
        "extra FILE V3 CUE",
        "one MODE2/2352 BIN track",
        () => File.WriteAllBytes(
            paths.OutputCuePath,
            Encoding.ASCII.GetBytes(
                $"FILE \"{v3Prefix}.bin\" BINARY\n" + exactCueText)));
    await RejectTamperAsync(
        "absolute-path V3 CUE",
        "expected BIN",
        () => File.WriteAllBytes(
            paths.OutputCuePath,
            Encoding.ASCII.GetBytes(
                "FILE \"/private/tmp/foreign-core4.bin\" BINARY\n" +
                "  TRACK 01 MODE2/2352\n" +
                "    INDEX 01 00:00:00\n")));
    await RejectTamperAsync(
        "parent-traversal V3 CUE",
        "expected BIN",
        () => File.WriteAllBytes(
            paths.OutputCuePath,
            Encoding.ASCII.GetBytes(
                $"FILE \"../{v3Prefix}.bin\" BINARY\n" +
                "  TRACK 01 MODE2/2352\n" +
                "    INDEX 01 00:00:00\n")));

    return rejects;
}

static async Task VerifyPersistedJournalFingerprintGuardMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    string operationRoot,
    IReadOnlyDictionary<string, FileSnapshot> cleanOutputSnapshot,
    string smokeRoot)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The journal-fingerprint smoke requires macOS filesystem semantics.");
    string stage = Path.Combine(operationRoot, "candidate");
    string journal = Path.Combine(operationRoot, "operation-journal.json");
    Require(Directory.Exists(stage) && File.Exists(journal),
        "The persisted-fingerprint matrix did not receive one complete journaled stage.");
    using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(journal)))
    {
        JsonElement root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetInt32() == 2 &&
                root.GetProperty("phase").GetString() == "staged" &&
                root.GetProperty("stagedCandidateFingerprintSha256").GetString() is { Length: 64 },
            "The persisted-fingerprint matrix did not receive one canonical schema-v2 staged journal.");
    }
    FileSnapshot exactJournal = SnapshotFile(journal);
    SmokeVnodeIdentity exactJournalVnode = SnapshotSmokeVnode(journal, "journaled stage journal");
    SmokeDirectoryIdentity exactStageDirectory = RequireSmokeDirectory(stage, "journaled stage baseline");
    Require(exactStageDirectory.Permissions == Convert.ToUInt16("755", 8),
        "The journaled stage baseline is not exact0755.");
    IReadOnlyDictionary<string, FileSnapshot> exactStage = SnapshotFlatCandidateDirectory(stage);
    IReadOnlyDictionary<string, SmokeVnodeIdentity> exactStageVnodes = Directory.EnumerateFiles(stage)
        .ToDictionary(
            path => Path.GetFileName(path)!,
            path => SnapshotSmokeVnode(path, "journaled stage leaf"),
            StringComparer.Ordinal);
    string leafName = Path.GetFileName(paths.RuntimeChecklistPath);
    string leaf = Path.Combine(stage, leafName);
    byte[] exactLeafBytes = File.ReadAllBytes(leaf);
    UnixFileMode exactLeafMode = GetMacMode(leaf);
    int rejects = 0;

    async Task RejectPreservedAsync(
        string label,
        Action mutate,
        Func<bool> mutationStillPresent,
        Action restore,
        string? requiredFragment = null)
    {
        mutate();
        try
        {
            if (requiredFragment is null)
            {
                await RejectAnyAsync(
                    () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                    label);
            }
            else
            {
                await RejectAsync(
                    () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
                    requiredFragment,
                    label);
            }
            Require(mutationStillPresent() && File.Exists(journal) && SnapshotFile(journal) == exactJournal &&
                    SnapshotSmokeVnode(journal, label + " preserved journal") == exactJournalVnode &&
                    Directory.Exists(paths.OperationsDirectoryPath) &&
                    SnapshotsEqual(cleanOutputSnapshot, SnapshotCandidate(paths)),
                $"The {label} recovery refusal mutated its staged tree, journal, operation, or prior output.");
            rejects++;
        }
        finally
        {
            restore();
        }
        Require(Directory.Exists(stage) &&
                RequireSmokeDirectory(stage, label + " restored stage").SameFile(exactStageDirectory) &&
                SnapshotsEqual(exactStage, SnapshotFlatCandidateDirectory(stage)) &&
                exactStageVnodes.All(pair =>
                    SnapshotSmokeVnode(
                        Path.Combine(stage, pair.Key),
                        label + " restored stage leaf") == pair.Value) &&
                File.Exists(journal) && SnapshotFile(journal) == exactJournal &&
                SnapshotSmokeVnode(journal, label + " restored journal") == exactJournalVnode,
            $"The {label} fixture did not restore the exact same-vnode journaled stage.");
    }

    byte originalByte = 0;
    await RejectPreservedAsync(
        "journal fingerprint same-length byte tamper",
        () => originalByte = ToggleFileByte(leaf, 0),
        () => File.ReadAllBytes(leaf)[0] != originalByte &&
              GetMacMode(leaf) == exactLeafMode &&
              new FileInfo(leaf).Length == exactLeafBytes.Length,
        () => WriteFileByte(leaf, 0, originalByte),
        "journal identity SHA-256");

    UnixFileMode wrongLeafMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    await RejectPreservedAsync(
        "journal fingerprint leaf-mode tamper",
        () => SetMacMode(leaf, wrongLeafMode),
        () => GetMacMode(leaf) == wrongLeafMode,
        () => SetMacMode(leaf, exactLeafMode));

    string heldVnode = Path.Combine(smokeRoot, "held-journal-fingerprint-vnode-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint leaf-vnode replacement",
        () =>
        {
            File.Move(leaf, heldVnode);
            File.Copy(heldVnode, leaf);
            SetMacMode(leaf, exactLeafMode);
        },
        () => File.Exists(heldVnode) && File.Exists(leaf) && HashFile(heldVnode) == HashFile(leaf),
        () =>
        {
            File.Delete(leaf);
            File.Move(heldVnode, leaf);
        },
        "journal identity SHA-256");

    string heldHardlink = Path.Combine(smokeRoot, "held-journal-fingerprint-hardlink-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint hardlinked leaf",
        () =>
        {
            File.Move(leaf, heldHardlink);
            Require(NativeLink(heldHardlink, leaf) == 0,
                "Could not create the journal-fingerprint hardlink fixture.");
        },
        () => File.Exists(heldHardlink) && File.Exists(leaf),
        () =>
        {
            File.Delete(leaf);
            File.Move(heldHardlink, leaf);
        },
        "st_nlink");

    await RejectPreservedAsync(
        "journal fingerprint leaf-length tamper",
        () => File.WriteAllBytes(leaf, [.. exactLeafBytes, 0x00]),
        () => new FileInfo(leaf).Length == exactLeafBytes.Length + 1,
        () => File.WriteAllBytes(leaf, exactLeafBytes),
        "journal identity SHA-256");

    string renamedLeaf = Path.Combine(stage, "renamed-" + leafName);
    await RejectPreservedAsync(
        "journal fingerprint leaf rename",
        () => File.Move(leaf, renamedLeaf),
        () => !File.Exists(leaf) && File.Exists(renamedLeaf),
        () => File.Move(renamedLeaf, leaf));

    string heldMissing = Path.Combine(smokeRoot, "held-journal-fingerprint-missing-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint missing leaf",
        () => File.Move(leaf, heldMissing),
        () => !File.Exists(leaf) && File.Exists(heldMissing),
        () => File.Move(heldMissing, leaf));

    string extra = Path.Combine(stage, "foreign-journal-fingerprint-extra.bin");
    await RejectPreservedAsync(
        "journal fingerprint extra leaf",
        () =>
        {
            File.WriteAllBytes(extra, [1]);
            SetMacMode(extra, exactLeafMode);
        },
        () => File.Exists(extra),
        () => File.Delete(extra));

    string heldSymlink = Path.Combine(smokeRoot, "held-journal-fingerprint-symlink-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint symlinked leaf",
        () =>
        {
            File.Move(leaf, heldSymlink);
            File.CreateSymbolicLink(leaf, heldSymlink);
        },
        () => (File.GetAttributes(leaf) & FileAttributes.ReparsePoint) != 0 && File.Exists(heldSymlink),
        () =>
        {
            File.Delete(leaf);
            File.Move(heldSymlink, leaf);
        },
        "symbolic");

    UnixFileMode wrongStageMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    await RejectPreservedAsync(
        "journal fingerprint directory-mode tamper",
        () => SetMacMode(stage, wrongStageMode),
        () => GetMacMode(stage) == wrongStageMode,
        () => SetMacMode(
            stage,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute));

    string heldStage = Path.Combine(smokeRoot, "held-journal-fingerprint-stage-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint directory-vnode replacement",
        () =>
        {
            Directory.Move(stage, heldStage);
            Directory.CreateDirectory(stage);
            SetMacMode(
                stage,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            foreach (string source in Directory.EnumerateFiles(heldStage))
            {
                string destination = Path.Combine(stage, Path.GetFileName(source));
                File.Copy(source, destination);
                SetMacMode(destination, GetMacMode(source));
            }
        },
        () => Directory.Exists(heldStage) && Directory.Exists(stage) &&
              !RequireSmokeDirectory(stage, "replacement journal stage").SameFile(exactStageDirectory),
        () =>
        {
            DeleteTreeWithoutFollowingLinks(stage);
            Directory.Move(heldStage, stage);
        },
        "journal identity SHA-256");

    string heldStageSymlink = Path.Combine(
        smokeRoot,
        "held-journal-fingerprint-stage-symlink-" + Guid.NewGuid().ToString("N"));
    await RejectPreservedAsync(
        "journal fingerprint symlinked directory",
        () =>
        {
            Directory.Move(stage, heldStageSymlink);
            Directory.CreateSymbolicLink(stage, heldStageSymlink);
        },
        () => (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0 &&
              Directory.Exists(heldStageSymlink),
        () =>
        {
            Directory.Delete(stage);
            Directory.Move(heldStageSymlink, stage);
        },
        "symbolic");

    Require(rejects == 12 && Directory.Exists(operationRoot) && File.Exists(journal),
        "The persisted journal-fingerprint filesystem guard matrix is incomplete.");
}

static async Task<int> VerifyRecoveryGuardMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    string smokeRoot)
{
    int rejects = 0;

    string foreignOperationsEntry = Path.Combine(paths.OperationsDirectoryPath, "foreign-entry");
    Directory.CreateDirectory(paths.OperationsDirectoryPath);
    File.SetUnixFileMode(
        paths.OperationsDirectoryPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.WriteAllBytes(foreignOperationsEntry, Encoding.UTF8.GetBytes("foreign\n"));
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "foreign operations entry");
    Require(File.Exists(foreignOperationsEntry), "Foreign operations residue was deleted on rejection.");
    File.Delete(foreignOperationsEntry);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    UnixFileMode mode0600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    UnixFileMode mode0644 = mode0600 | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    UnixFileMode mode0666 = mode0644 | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    UnixFileMode mode0700 = mode0600 | UnixFileMode.UserExecute;
    UnixFileMode mode0755 = mode0700 |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    UnixFileMode mode0777 = mode0755 | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    string wrongJournalTemporaryOperation = CreateOperationFixture(paths);
    string wrongJournalTemporary = Path.Combine(wrongJournalTemporaryOperation, "operation-journal.json.tmp");
    File.WriteAllBytes(wrongJournalTemporary, [1, 2, 3]);
    File.SetUnixFileMode(wrongJournalTemporary, mode0644);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "journal-temporary mode tamper");
    Require(File.GetUnixFileMode(wrongJournalTemporary) == mode0644,
        "Wrong-mode journal temporary was repaired or removed on rejection.");
    File.SetUnixFileMode(wrongJournalTemporary, mode0600);
    DeleteTreeWithoutFollowingLinks(wrongJournalTemporaryOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    foreach (UnixFileMode wrongOperationsMode in new[] { mode0755, mode0777 })
    {
        string operation = CreateOperationFixture(paths);
        File.SetUnixFileMode(paths.OperationsDirectoryPath, wrongOperationsMode);
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "operations-directory mode tamper");
        Require(File.GetUnixFileMode(paths.OperationsDirectoryPath) == wrongOperationsMode &&
                Directory.Exists(operation),
            "Wrong-mode operations directory was repaired or removed on rejection.");
        File.SetUnixFileMode(paths.OperationsDirectoryPath, mode0700);
        DeleteTreeWithoutFollowingLinks(operation);
        Directory.Delete(paths.OperationsDirectoryPath);
        rejects++;
    }

    foreach (UnixFileMode wrongOperationRootMode in new[] { mode0755, mode0777 })
    {
        string operation = CreateOperationFixture(paths);
        File.SetUnixFileMode(operation, wrongOperationRootMode);
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "operation-root mode tamper");
        Require(File.GetUnixFileMode(operation) == wrongOperationRootMode,
            "Wrong-mode operation root was repaired or removed on rejection.");
        File.SetUnixFileMode(operation, mode0700);
        DeleteTreeWithoutFollowingLinks(operation);
        Directory.Delete(paths.OperationsDirectoryPath);
        rejects++;
    }

    foreach (UnixFileMode wrongOperationLeaseMode in new[] { mode0644, mode0666 })
    {
        string operation = CreateOperationFixture(paths);
        string lease = Path.Combine(operation, "operation.lease");
        File.SetUnixFileMode(lease, wrongOperationLeaseMode);
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "operation-lease mode tamper");
        Require(File.GetUnixFileMode(lease) == wrongOperationLeaseMode,
            "Wrong-mode operation lease was repaired or removed on rejection.");
        File.SetUnixFileMode(lease, mode0600);
        DeleteTreeWithoutFollowingLinks(operation);
        Directory.Delete(paths.OperationsDirectoryPath);
        rejects++;
    }

    string wrongJournalOperation = CreateOperationFixture(paths);
    WriteRecoveryJournalFixture(
        wrongJournalOperation,
        paths,
        "created",
        previousOutputExisted: true);
    string wrongJournal = Path.Combine(wrongJournalOperation, "operation-journal.json");
    File.SetUnixFileMode(wrongJournal, mode0644);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "journal mode tamper");
    Require(File.GetUnixFileMode(wrongJournal) == mode0644,
        "Wrong-mode operation journal was repaired or removed on rejection.");
    File.SetUnixFileMode(wrongJournal, mode0600);
    DeleteTreeWithoutFollowingLinks(wrongJournalOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string legacyJournalOperation = CreateOperationFixture(paths);
    WriteRecoveryJournalFixture(
        legacyJournalOperation,
        paths,
        "created",
        previousOutputExisted: true,
        schemaVersion: 1);
    string legacyJournal = Path.Combine(legacyJournalOperation, "operation-journal.json");
    FileSnapshot legacyJournalBefore = SnapshotFile(legacyJournal);
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "exact owned paths or phases",
        "legacy schema-v1 recovery journal");
    Require(Directory.Exists(legacyJournalOperation) && File.Exists(legacyJournal) &&
            SnapshotFile(legacyJournal) == legacyJournalBefore,
        "The rejected schema-v1 recovery journal was rewritten or removed.");
    DeleteTreeWithoutFollowingLinks(legacyJournalOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string wrongPartialStageOperation = CreateOperationFixture(paths);
    WriteRecoveryJournalFixture(
        wrongPartialStageOperation,
        paths,
        "created",
        previousOutputExisted: true);
    string wrongPartialStage = Path.Combine(wrongPartialStageOperation, "candidate");
    Directory.CreateDirectory(wrongPartialStage);
    File.SetUnixFileMode(wrongPartialStage, mode0777);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "partial-stage directory mode tamper");
    Require(File.GetUnixFileMode(wrongPartialStage) == mode0777,
        "Wrong-mode partial stage was repaired or removed on rejection.");
    File.SetUnixFileMode(wrongPartialStage, mode0755);
    DeleteTreeWithoutFollowingLinks(wrongPartialStageOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string firstCompeting = CreateOperationFixture(paths);
    string secondCompeting = CreateOperationFixture(paths);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "multiple competing operations");
    Require(Directory.Exists(firstCompeting) && Directory.Exists(secondCompeting),
        "Competing operation roots were deleted on rejection.");
    DeleteTreeWithoutFollowingLinks(firstCompeting);
    DeleteTreeWithoutFollowingLinks(secondCompeting);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string escaped = CreateOperationFixture(paths);
    WriteRecoveryJournalFixture(
        escaped,
        paths,
        "created",
        previousOutputExisted: true,
        operationRootOverride: smokeRoot);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "escaped recovery journal");
    Require(Directory.Exists(escaped), "An escaped recovery journal was deleted on rejection.");
    DeleteTreeWithoutFollowingLinks(escaped);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string symlinkJournalOperation = CreateOperationFixture(paths);
    string symlinkJournalTarget = Path.Combine(smokeRoot, "foreign-journal-target.json");
    File.WriteAllBytes(symlinkJournalTarget, Encoding.UTF8.GetBytes("{}\n"));
    File.CreateSymbolicLink(
        Path.Combine(symlinkJournalOperation, "operation-journal.json"),
        symlinkJournalTarget);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "symlink recovery journal");
    Require(Directory.Exists(symlinkJournalOperation), "A symlink recovery journal root was deleted.");
    DeleteTreeWithoutFollowingLinks(symlinkJournalOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    File.Delete(symlinkJournalTarget);
    rejects++;

    string hardlinkOperation = CreateOperationFixture(paths, createLease: false);
    string hardlinkSource = Path.Combine(smokeRoot, "hardlink-operation-lease-source");
    File.WriteAllBytes(hardlinkSource, []);
    File.SetUnixFileMode(hardlinkSource, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Require(NativeLink(hardlinkSource, Path.Combine(hardlinkOperation, "operation.lease")) == 0,
        "Could not create the hardlinked operation-lease fixture.");
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "st_nlink",
        "hardlinked operation lease");
    Require(Directory.Exists(hardlinkOperation), "A hardlinked operation-lease root was deleted.");
    DeleteTreeWithoutFollowingLinks(hardlinkOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    File.Delete(hardlinkSource);
    rejects++;

    string fifoOperation = CreateOperationFixture(paths, createLease: false);
    Require(NativeMkFifo(Path.Combine(fifoOperation, "operation.lease"), Convert.ToUInt16("600", 8)) == 0,
        "Could not create the FIFO operation-lease fixture.");
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "FIFO operation lease");
    Require(Directory.Exists(fifoOperation), "A FIFO operation-lease root was deleted.");
    DeleteTreeWithoutFollowingLinks(fifoOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string oversizedJournalOperation = CreateOperationFixture(paths);
    File.WriteAllBytes(
        Path.Combine(oversizedJournalOperation, "operation-journal.json"),
        new byte[64 * 1024 + 1]);
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
        "oversized recovery journal");
    Require(Directory.Exists(oversizedJournalOperation), "An oversized recovery journal was deleted.");
    DeleteTreeWithoutFollowingLinks(oversizedJournalOperation);
    Directory.Delete(paths.OperationsDirectoryPath);
    rejects++;

    string activeOperation = CreateOperationFixture(paths);
    WriteRecoveryJournalFixture(activeOperation, paths, "created", previousOutputExisted: true);
    await using (FileStream activeLease = new(
        Path.Combine(activeOperation, "operation.lease"),
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.None))
    {
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            "active operation lease");
        Require(Directory.Exists(activeOperation), "An active operation root was deleted.");
    }
    await RecoverOnlyAsync(request, token, paths, "active-lease released recovery");
    rejects++;

    foreach (bool useSubdirectory in new[] { false, true })
    {
        string partialOperation = CreateOperationFixture(paths);
        WriteRecoveryJournalFixture(partialOperation, paths, "created", previousOutputExisted: true);
        string partialStage = Path.Combine(partialOperation, "candidate");
        Directory.CreateDirectory(partialStage);
        if (useSubdirectory)
            Directory.CreateDirectory(Path.Combine(partialStage, "foreign-subdirectory"));
        else
            File.WriteAllBytes(Path.Combine(partialStage, "foreign-extra.bin"), [1]);
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with { ReplaceExistingCandidate = true }, token),
            useSubdirectory ? "partial-stage subdirectory" : "partial-stage foreign filename");
        Require(Directory.Exists(partialStage), "An ambiguous partial stage was recursively deleted.");
        DeleteTreeWithoutFollowingLinks(partialOperation);
        Directory.Delete(paths.OperationsDirectoryPath);
        rejects++;
    }

    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
            !Directory.Exists(paths.OperationsDirectoryPath),
        "The recovery-guard matrix changed the prior output or left owned operation residue.");
    return rejects;
}

static async Task<int> VerifyInputFreshnessHooksAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    IReadOnlyList<(string Path, FileSnapshot Snapshot, string Label)> inputs)
{
    int rejects = 0;
    foreach ((string inputPath, FileSnapshot snapshot, string label) in inputs)
    {
        bool mutated = false;
        byte original = 0;
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with
                {
                    ReplaceExistingCandidate = true,
                    TestStageHook = stage =>
                    {
                        if (stage != "after-staged-candidate-verified" || mutated)
                            return;
                        original = ToggleFileByte(inputPath, 0);
                        mutated = true;
                    }
                }, token),
                label + " mutation after staged verification");
            Require(mutated, $"The {label} freshness hook was not reached.");
        }
        finally
        {
            if (mutated)
            {
                WriteFileByte(inputPath, 0, original);
                File.SetLastWriteTimeUtc(inputPath, snapshot.LastWriteUtc);
                if (snapshot.UnixMode is UnixFileMode mode && OperatingSystem.IsMacOS())
                    File.SetUnixFileMode(inputPath, mode);
            }
        }
        if (Directory.Exists(paths.OperationsDirectoryPath))
            await RecoverOnlyAsync(request, token, paths, label + " post-restore recovery");
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
            $"The {label} freshness rejection changed the prior candidate or left stale state.");
        rejects++;
    }
    return rejects;
}

static async Task<int> VerifyPostHookTopologyGuardMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    string smokeRoot)
{
    (string Hook, bool RequiresRollback, string Label)[] cases =
    [
        ("after-created-journal", false, "created"),
        ("after-staged-candidate-verified", false, "staged"),
        ("after-previous-candidate-backup", false, "previous-backed-up"),
        ("after-candidate-publication", false, "published"),
        ("after-committed-backup-cleanup", false, "committed-cleanup"),
        ("before-previous-candidate-restore", true, "rollback-restore")
    ];
    int rejects = 0;
    foreach ((string hook, bool requiresRollback, string label) in cases)
    {
        string sentinel = Path.Combine(smokeRoot, "must-remain-empty-post-hook-" + label);
        Directory.CreateDirectory(sentinel);
        string? operationRoot = null;
        string? heldOperationRoot = null;
        bool injected = false;
        bool primaryRollbackFault = false;
        try
        {
            await RejectAnyAsync(
                () => InvokeVerifiedPublisherAsync(request with
                {
                    ReplaceExistingCandidate = true,
                    TestStageHook = stage =>
                    {
                        if (requiresRollback && stage == "after-candidate-publication" &&
                            !primaryRollbackFault)
                        {
                            primaryRollbackFault = true;
                            throw new IOException("enter rollback for post-hook topology guard");
                        }
                        if (stage != hook)
                            return;
                        operationRoot = Directory.EnumerateDirectories(paths.OperationsDirectoryPath).Single();
                        heldOperationRoot = Path.Combine(
                            smokeRoot,
                            "held-post-hook-operation-" + label + "-" + Guid.NewGuid().ToString("N"));
                        Directory.Move(operationRoot, heldOperationRoot);
                        Directory.CreateSymbolicLink(operationRoot, sentinel);
                        injected = true;
                    }
                }, token),
                "post-hook operation-root replacement at " + hook);
        }
        finally
        {
            if (operationRoot is not null && (Directory.Exists(operationRoot) || File.Exists(operationRoot)))
            {
                FileAttributes attributes = File.GetAttributes(operationRoot);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    Directory.Delete(operationRoot);
            }
            if (operationRoot is not null && heldOperationRoot is not null &&
                Directory.Exists(heldOperationRoot) && !Directory.Exists(operationRoot) && !File.Exists(operationRoot))
            {
                Directory.Move(heldOperationRoot, operationRoot);
            }
        }
        Require(injected && (!requiresRollback || primaryRollbackFault) &&
                !Directory.EnumerateFileSystemEntries(sentinel).Any(),
            $"The {hook} topology hook was not reached or wrote through its out-of-tree sentinel.");
        await RecoverOnlyAsync(request, token, paths, "post-hook topology recovery " + hook);
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
            $"The {hook} topology rejection did not preserve/recover the exact candidate.");
        Directory.Delete(sentinel);
        rejects++;
    }
    return rejects;
}

static async Task<(int PhaseBoundaryRecoveries, int CleanupResidueRecoveries)>
    VerifyPhaseBoundaryProcessCrashRecoveryMatrixAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    string smokeRoot)
{
    int phaseBoundaryRecoveries = 0;
    int cleanupResidueRecoveries = 0;
    bool mtimeOnlyRestartRecoveryExercised = false;
    string[] crashStages =
    [
        "after-created-journal",
        "after-staged-candidate-verified",
        "after-backup-intent-journal",
        "after-previous-candidate-backup",
        "after-publish-intent-journal",
        "after-candidate-publication"
    ];
    foreach (string crashStage in crashStages)
    {
        bool primaryFaultObserved = false;
        await RejectAnyAsync(
            () => InvokeVerifiedPublisherAsync(request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage == crashStage)
                    {
                        primaryFaultObserved = true;
                        throw new IOException("simulated phase-boundary process interruption at " + crashStage);
                    }
                    if (primaryFaultObserved && stage == "before-rollback-start")
                        throw new IOException("simulated process terminated before in-process rollback");
                }
            }, token),
            "persisted phase-boundary process-crash state " + crashStage);
        Require(primaryFaultObserved && Directory.Exists(paths.OperationsDirectoryPath),
            $"The {crashStage} phase-boundary crash fixture did not preserve its journaled operation.");
        if (crashStage == "after-staged-candidate-verified")
        {
            string operationRoot = Directory.EnumerateDirectories(paths.OperationsDirectoryPath).Single();
            string stagedChecklist = Path.Combine(
                operationRoot,
                "candidate",
                Path.GetFileName(paths.RuntimeChecklistPath));
            FileSnapshot beforeMtime = SnapshotFile(stagedChecklist);
            Require(NativeLStat(stagedChecklist, out SmokeDarwinStat beforeStatus) == 0,
                "Could not lstat the mtime-only restart fixture before mutation.");
            File.SetLastWriteTimeUtc(stagedChecklist, beforeMtime.LastWriteUtc.AddSeconds(3));
            FileSnapshot afterMtime = SnapshotFile(stagedChecklist);
            Require(NativeLStat(stagedChecklist, out SmokeDarwinStat afterStatus) == 0 &&
                    beforeStatus.Device == afterStatus.Device && beforeStatus.Inode == afterStatus.Inode &&
                    beforeMtime.ByteLength == afterMtime.ByteLength &&
                    beforeMtime.Sha256 == afterMtime.Sha256 &&
                    beforeMtime.UnixMode == afterMtime.UnixMode &&
                    beforeMtime.LastWriteUtc != afterMtime.LastWriteUtc,
                "The mtime-only restart fixture changed more than the same-vnode timestamps.");
            mtimeOnlyRestartRecoveryExercised = true;
        }
        await RecoverOnlyAsync(request, token, paths, "startup recovery " + crashStage);
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
            $"Startup recovery from {crashStage} did not restore exact prior output.");
        phaseBoundaryRecoveries++;
    }

    bool rollbackIntentFaultObserved = false;
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "after-candidate-publication")
                {
                    rollbackIntentFaultObserved = true;
                    throw new IOException("simulated primary failure before rollback-restore intent");
                }
                if (rollbackIntentFaultObserved && stage == "before-previous-candidate-restore")
                    throw new IOException("simulated process death at rollback-restore-intent");
            }
        }, token),
        "rollback-restore-intent phase-boundary process-crash residue");
    Require(rollbackIntentFaultObserved && Directory.Exists(paths.OperationsDirectoryPath),
        "The rollback-restore-intent crash fixture did not preserve its exact journaled operation.");
    await RecoverOnlyAsync(request, token, paths, "rollback-restore-intent startup recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
            !Directory.Exists(paths.OperationsDirectoryPath),
        "Rollback-restore-intent startup recovery did not restore exact prior output.");
    phaseBoundaryRecoveries++;

    bool publishedFaultObserved = false;
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "after-candidate-publication")
                {
                    publishedFaultObserved = true;
                    throw new IOException("simulated primary failure before rollback restore");
                }
                if (publishedFaultObserved && stage == "after-rollback-restored")
                    throw new IOException("simulated process death after prior-output restore");
            }
        }, token),
        "rollback-restored phase-boundary process-crash residue");
    Require(Directory.Exists(paths.OperationsDirectoryPath) &&
            SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)),
        "The rollback-restored crash fixture did not preserve the exact restored output and journal.");
    await RecoverOnlyAsync(request, token, paths, "rollback-restored startup recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
            !Directory.Exists(paths.OperationsDirectoryPath),
        "Rollback-restored startup recovery did not complete idempotently.");
    phaseBoundaryRecoveries++;

    foreach (string residue in new[] { "lease-only", "temporary-only", "journal-and-temporary" })
    {
        string operation = CreateOperationFixture(paths);
        if (residue == "temporary-only")
        {
            File.WriteAllBytes(Path.Combine(operation, "operation-journal.json.tmp"), [1, 2, 3]);
            File.SetUnixFileMode(
                Path.Combine(operation, "operation-journal.json.tmp"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        else if (residue == "journal-and-temporary")
        {
            WriteRecoveryJournalFixture(operation, paths, "created", previousOutputExisted: true);
            File.WriteAllBytes(Path.Combine(operation, "operation-journal.json.tmp"), [4, 5, 6]);
            File.SetUnixFileMode(
                Path.Combine(operation, "operation-journal.json.tmp"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        await RecoverOnlyAsync(request, token, paths, residue + " startup cleanup");
        Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
            $"The {residue} phase-boundary process-crash residue was not cleaned conservatively.");
        cleanupResidueRecoveries++;
    }

    Require(phaseBoundaryRecoveries == 8 && cleanupResidueRecoveries == 3 &&
            mtimeOnlyRestartRecoveryExercised,
        "The pre-committed journal-phase recovery or cleanup-residue matrix is incomplete.");
    return (phaseBoundaryRecoveries, cleanupResidueRecoveries);
}

static async Task VerifySameShapedForeignRollbackBoundaryAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot,
    string smokeRoot)
{
    string heldPublished = Path.Combine(smokeRoot, "held-valid-published-tree");
    bool replacementInstalled = false;
    await RejectAnyAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage != "after-candidate-publication")
                    return;
                Directory.Move(paths.OutputDirectoryPath, heldPublished);
                Directory.CreateDirectory(paths.OutputDirectoryPath);
                foreach (string candidatePath in CandidatePaths(paths))
                {
                    File.WriteAllBytes(
                        candidatePath,
                        Encoding.UTF8.GetBytes("foreign same-shaped tree\n"));
                }
                replacementInstalled = true;
                throw new IOException("same-shaped foreign output installed at rollback boundary");
            }
        }, token),
        "same-shaped foreign rollback boundary");
    Require(replacementInstalled && Directory.Exists(heldPublished) &&
            Directory.EnumerateFiles(paths.OutputDirectoryPath).Count() == 7 &&
            Directory.Exists(paths.OperationsDirectoryPath),
        "The same-shaped foreign rollback fixture was deleted or not preserved fail-closed.");

    DeleteTreeWithoutFollowingLinks(paths.OutputDirectoryPath);
    Directory.Move(heldPublished, paths.OutputDirectoryPath);
    await RecoverOnlyAsync(request, token, paths, "same-shaped foreign boundary recovery");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)) &&
            !Directory.Exists(paths.OperationsDirectoryPath),
        "Recovery after restoring the exact journal-bound published tree did not restore the prior output.");
}

static FullReadbackFixtureInput BuildExistingCompleteFullReadbackFixture(
    string repositoryRoot,
    string fixtureRoot)
{
    Id65V3RuntimeEvidencePackage pending = Id65V3RuntimeEvidenceContract.CreatePending();
    Id65V3RuntimeEvidenceProfile completeSafeProfile = pending.RuntimeProfile with
    {
        SettingsBeforeSha256 = pending.RuntimeProfile.ExternalHomeSettingsSha256,
        SettingsAfterSha256 = pending.RuntimeProfile.ExternalHomeSettingsSha256,
        NormalUserSettingsBeforeSha256 = pending.RuntimeProfile.HostNormalSettingsSha256,
        NormalUserSettingsAfterSha256 = pending.RuntimeProfile.HostNormalSettingsSha256,
        CheatFileCountBefore = 0,
        CheatFileCountAfter = 0,
        GameSettingsFileCountBefore = 0,
        GameSettingsFileCountAfter = 0,
        MemoryCardFileCountBefore = 0,
        MemoryCardFileCountAfter = 0,
        SaveStateFileCountBefore = 0,
        SaveStateFileCountAfter = 0,
        PatchFileCountBefore = 0,
        PatchFileCountAfter = 0,
        ChangedProfilePaths = [],
        SaveStateLoaded = false,
        SaveStateCreated = false,
        InGameSaveUsed = false,
        ActiveCheatCount = 0
    };
    Assembly fixtureAssembly = Assembly.Load("Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke");
    MethodInfo builder = fixtureAssembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
        .Single(method => method.Name.Contains("BuildCompleteFixture", StringComparison.Ordinal));
    ParameterInfo[] builderParameters = builder.GetParameters();
    Require(builderParameters.Length >= 4 &&
            builderParameters[0].ParameterType == typeof(string) &&
            builderParameters[1].ParameterType == typeof(string) &&
            builderParameters[2].ParameterType == typeof(Id65V3RuntimeEvidencePackage) &&
            builderParameters[3].ParameterType == typeof(Id65V3RuntimeEvidenceProfile) &&
            builderParameters.Skip(4).All(parameter => parameter.HasDefaultValue),
        "The reused complete-fixture builder no longer exposes four exact required " +
        "inputs followed only by optional controls.");
    object?[] builderArguments = new object?[builderParameters.Length];
    builderArguments[0] = repositoryRoot;
    builderArguments[1] = fixtureRoot;
    builderArguments[2] = pending;
    builderArguments[3] = completeSafeProfile;
    for (int index = 4; index < builderParameters.Length; index++)
        builderArguments[index] = builderParameters[index].DefaultValue;
    object fixture = InvokeReflection(
        builder,
        null,
        builderArguments);
    Type fixtureType = fixture.GetType();
    Id65V3RuntimeEvidencePackage package = (Id65V3RuntimeEvidencePackage)(
        fixtureType.GetProperty("Package")?.GetValue(fixture) ??
        throw new InvalidOperationException("The existing complete fixture did not expose its package."));
    Id65V3RuntimeEvidenceReadbackContext context = (Id65V3RuntimeEvidenceReadbackContext)(
        fixtureType.GetProperty("ReadbackContext")?.GetValue(fixture) ??
        throw new InvalidOperationException("The existing complete fixture did not expose its readback context."));
    IReadOnlyDictionary<string, string> artifactPaths = (IReadOnlyDictionary<string, string>)(
        fixtureType.GetProperty("ArtifactPaths")?.GetValue(fixture) ??
        throw new InvalidOperationException("The existing complete fixture did not expose its artifact paths."));
    Id65V3RuntimeEvidenceValidation validation = Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
        fixtureRoot,
        Id65V3RuntimeEvidenceValidationMode.FullReadback,
        context);
    Require(validation.StructurallyValid && validation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass &&
            validation.Errors.Length == 0,
        "The reused seven-session FullReadback fixture did not validate as FocusedRuntimePass: " +
        string.Join(" | ", validation.Errors));
    return new(
        fixtureRoot,
        Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
        context,
        artifactPaths.OrderBy(pair => pair.Key, StringComparer.Ordinal).First().Value);
}

static object CreateSyntheticToken(
    string repositoryRoot,
    FullReadbackFixtureInput fixture)
{
    MethodInfo factory = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
        "CreateSyntheticFocusedRuntimePassTokenForRewardSmoke",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException("The private publisher-smoke token factory is missing.");
    Require(factory.IsPrivate && factory.GetParameters().Length == 4 &&
            factory.GetParameters().All(parameter =>
                parameter.ParameterType != typeof(Id65V3RuntimeEvidenceValidation)),
        "The synthetic publisher-smoke token issuer is not one exact private fixture-validated method.");
    return InvokeReflection(
        factory,
        null,
        [repositoryRoot, fixture.CanonicalEvidenceUtf8, fixture.RepositoryRoot, fixture.ReadbackContext]);
}

static async Task<Id65AuthoringRewardStagingRuntimeCandidateResult> InvokeVerifiedPublisherAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token)
{
    MethodInfo core = typeof(Id65AuthoringRewardStagingRuntimeCandidatePublisher).GetMethod(
        "CreateVerifiedAsync",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException("The private fixture-authorized publisher core is missing.");
    object taskObject = InvokeReflection(core, null, [request, token, CancellationToken.None]);
    return await (Task<Id65AuthoringRewardStagingRuntimeCandidateResult>)taskObject;
}

static void VerifyFrozenResult(Id65AuthoringRewardStagingRuntimeCandidateResult result)
{
    string[] expectedNames =
    [
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + ".bin",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + ".cue",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + "-construction-plan.json",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + "-static-readback-receipt.json",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + "-runtime-checklist.md",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + "-location-guide.svg",
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix + "-Reveal-in-Finder.command"
    ];
    string[] actualNames = Directory.EnumerateFiles(result.Paths.OutputDirectoryPath)
        .Select(Path.GetFileName)
        .Order(StringComparer.Ordinal)
        .ToArray()!;
    Require(actualNames.SequenceEqual(expectedNames.Order(StringComparer.Ordinal)) &&
            result.OutputImageSha256 == Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputImageSha256 &&
            result.OutputWadSha256 == Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputWadSha256 &&
            result.OutputRow80Sha256 == Id65AuthoringRewardStagingCompiler.ExpectedOutputRow80Sha256 &&
            result.RawSectorDiffSha256 == Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedRawSectorDiffSha256 &&
            result.RebuiltRawSectorCount == 579 && result.VerifiedRawSectorCount == 579 &&
            result.ChangedLogicalBytes == 972_516 && result.ChangedPhysicalImageBytes == 1_132_455 &&
            result.RawSectorDiffs.Count == 579 && result.Receipt.RawSectorDiffs.Length == 579 &&
            result.Plan.SchemaVersion == 2 && result.Receipt.SchemaVersion == 2 &&
            result.Plan.CollisionWindingRepairV2OutputDataSha256 ==
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputDataSha256 &&
            result.Plan.CollisionWindingRepairV2OutputModelSha256 ==
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputModelSha256 &&
            result.Plan.EnvironmentHeaderRepairV3OutputDataSha256 ==
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputDataSha256 &&
            result.Plan.EnvironmentHeaderRepairV3OutputModelSha256 ==
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputModelSha256 &&
            result.Receipt.CollisionWindingRepairV2OutputDataSha256 ==
                result.Plan.CollisionWindingRepairV2OutputDataSha256 &&
            result.Receipt.CollisionWindingRepairV2OutputModelSha256 ==
                result.Plan.CollisionWindingRepairV2OutputModelSha256 &&
            result.Receipt.EnvironmentHeaderRepairV3OutputDataSha256 ==
                result.Plan.EnvironmentHeaderRepairV3OutputDataSha256 &&
            result.Receipt.EnvironmentHeaderRepairV3OutputModelSha256 ==
                result.Plan.EnvironmentHeaderRepairV3OutputModelSha256 &&
            result.Plan.OutputModelSha256 == Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 &&
            result.Plan.ObjectCount == 107 && result.Plan.ActorRootCount == 38 &&
            result.Plan.FixupCount == 129 && result.Plan.CollisionBlocksUsedBytes == 130 &&
            result.Receipt.OutputModelSha256 ==
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 &&
            result.Receipt.ObjectCount == 107 && result.Receipt.ActorRootCount == 38 &&
            result.Receipt.FixupCount == 129 && result.Receipt.CollisionBlocksUsedBytes == 130 &&
            result.Plan.StationaryOwnerCount == 71 && result.Plan.StationaryRewardTotal == 171 &&
            result.Plan.DragonCount == 4 && result.Plan.RequiredEggTarget == 0 &&
            result.Plan.CompilerInvocationCount == 2 && result.Plan.LockedBinCopyCount == 1 &&
            result.Plan.CompleteRow80WriteCount == 1 && result.Plan.Core4RuntimePending &&
            !result.Plan.Total200RuntimeAccepted && !result.Plan.SaveRuntimeAccepted &&
            !result.Plan.AppIntegrated && !result.Plan.NormalCreateBinEnabled &&
            !result.Plan.PromotionAuthorized && !result.Plan.ReleaseAuthorized &&
            !result.Plan.ProcessCrashRecoveryVerified &&
            result.Plan.PhaseBoundaryProcessCrashRecoveryVerified &&
            !result.Plan.MidMutationProcessCrashRecoveryVerified &&
            !result.Plan.PowerLossDurabilityVerified &&
            !result.Receipt.RollbackRecoveryVerified && !result.Receipt.ProcessCrashRecoveryVerified &&
            result.Receipt.PhaseBoundaryProcessCrashRecoveryVerified &&
            !result.Receipt.MidMutationProcessCrashRecoveryVerified &&
            !result.Receipt.PowerLossDurabilityVerified && !result.ProcessCrashRecoveryVerified &&
            result.PhaseBoundaryProcessCrashRecoveryVerified &&
            !result.MidMutationProcessCrashRecoveryVerified && !result.PowerLossDurabilityVerified &&
            result.Core4RuntimePending &&
            !result.NormalCreateBinEnabled,
        "The frozen Core4-171 result, physical receipt, semantic readback, or closed gates changed.");
    VerifyExactV3PhysicalOracle(result);
    VerifyExactV3HandoffBytes(result.Paths);
}

static void VerifyExactV3PhysicalOracle(Id65AuthoringRewardStagingRuntimeCandidateResult result)
{
    const string outputImageSha256 =
        "85114fecd967cae824ecac0fdf4c890e704b728be56bd81e0665e5988e4d7863";
    const string outputWadSha256 =
        "bea717f496d84607e51fc6a7940364099455759fe1e06b012de5e941211734d1";
    const string outputRow80Sha256 =
        "f3fd8c343781a18cb394d03bc218b6e94541a3a8d9e6c442cbbe5cf6b8d0f947";
    const string rawSectorDiffSha256 =
        "5b21133bdb75b33ebe25cb33cfe0e43a223d2dff6a3d22e28cf7a7b15b438121";
    const string affectedRawAfterSha256 =
        "212938b3f515476dbc784109f6c1ee98a0fca06cff3258904ed527aa262460d1";
    const string affectedPayloadAfterSha256 =
        "d8fe86c546fd3924cdebde46c74a1113ed62fc0dbce634598dc9d2598502288e";
    const long changedPhysicalBytes = 1_132_455;
    const int eccQChangedBytes = 59_633;

    string[] currentHashes =
    [
        outputImageSha256,
        outputWadSha256,
        outputRow80Sha256,
        rawSectorDiffSha256,
        affectedRawAfterSha256,
        affectedPayloadAfterSha256
    ];
    string[] staleHashes =
    [
        "d983a328da549c40f5a1e3e8b4a708274700e740399d5a5e63a6ce3d73cfc6b5",
        "87f7e7c99452e904a767a7c1fb235ada175a2ff20929162bf78b7b269a5d553d",
        "749267fc4635b25111b68a7c1b58282d4787efc83e2ddf3413e31fa3385f2755",
        "615cab2cc06a6ce1bd0638f9a98985dc9c30eebac39a6d036d3f84cdd7c127a5",
        "fba72f9cd72e90086f2a22de679b298f1824dc9cdec281953dd751175e47e1aa",
        "ba6ab947e8b97047095ade33f6c894365d3ad33faeb730dd26d2ada046be9aba"
    ];
    Require(currentHashes.Length == 6 && currentHashes.Concat(staleHashes).All(value =>
                value.Length == 64 && value.All(character =>
                    character is >= '0' and <= '9' or >= 'a' and <= 'f')) &&
            currentHashes.Zip(staleHashes, (current, stale) => current != stale).All(changed => changed) &&
            currentHashes.Distinct(StringComparer.Ordinal).Count() == currentHashes.Length,
        "The independent V3 physical-oracle literals are not six distinct rotations.");
    Require(
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputImageSha256 == outputImageSha256 &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputWadSha256 == outputWadSha256 &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedRawSectorDiffSha256 == rawSectorDiffSha256 &&
        Id65AuthoringRewardStagingCompiler.ExpectedOutputRow80Sha256 == outputRow80Sha256 &&
        result.OutputImageSha256 == outputImageSha256 &&
        result.OutputWadSha256 == outputWadSha256 &&
        result.OutputRow80Sha256 == outputRow80Sha256 &&
        result.RawSectorDiffSha256 == rawSectorDiffSha256 &&
        result.Plan.OutputImageSha256 == outputImageSha256 &&
        result.Plan.OutputWadSha256 == outputWadSha256 &&
        result.Plan.OutputRow80Sha256 == outputRow80Sha256 &&
        result.Plan.RawSectorDiffSha256 == rawSectorDiffSha256 &&
        result.Plan.AffectedRawSectorsAfterSha256 == affectedRawAfterSha256 &&
        result.Plan.AffectedPayloadsAfterSha256 == affectedPayloadAfterSha256 &&
        result.Plan.ChangedPhysicalImageBytes == changedPhysicalBytes &&
        result.Plan.PhysicalAggregate.EccQChangedBytes == eccQChangedBytes &&
        result.Plan.PhysicalAggregate.TotalChangedBytes == changedPhysicalBytes &&
        result.Receipt.OutputImageSha256 == outputImageSha256 &&
        result.Receipt.OutputWadSha256 == outputWadSha256 &&
        result.Receipt.OutputRow80Sha256 == outputRow80Sha256 &&
        result.Receipt.RawSectorDiffSha256 == rawSectorDiffSha256 &&
        result.Receipt.ChangedPhysicalImageBytes == changedPhysicalBytes &&
        result.Receipt.PhysicalAggregate.EccQChangedBytes == eccQChangedBytes &&
        result.Receipt.PhysicalAggregate.TotalChangedBytes == changedPhysicalBytes &&
        result.ChangedPhysicalImageBytes == changedPhysicalBytes &&
        changedPhysicalBytes != 1_132_460 && eccQChangedBytes != 59_634,
        "The exact independently derived V3 physical afterimage or stale-value inequality changed.");
}

static void VerifyExactV3HandoffBytes(Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
{
    const string expectedCueSha256 =
        "a490b43e1e823835a5d2a7fb3b056e8142e3147354fa394715ea4ec431804e1f";
    const string expectedHelperSha256 =
        "c460504293476b06edcf31e973dbb04562302af4f40631937d32b2100546f0bd";
    const string legacyCueSha256 =
        "12c802df5e6faaa132c06cb30de676ca5e2c23d6366382caf5730c72e1e9da21";
    const string legacyHelperSha256 =
        "e614cd3a92b32e07bee86f96d94f7759aad3f6ddf82de911445cdc7767a27a09";
    byte[] expectedCue = ExactV3CueBytes();
    byte[] expectedHelper = ExactV3FinderHelperBytes();
    byte[] legacyCue = LegacyCueBytes();
    byte[] legacyHelper = LegacyFinderHelperBytes();
    byte[] actualCue = File.ReadAllBytes(paths.OutputCuePath);
    byte[] actualHelper = File.ReadAllBytes(paths.FinderHelperPath);

    Require(expectedCue.Length == 134 && HashBytes(expectedCue) == expectedCueSha256 &&
            expectedHelper.Length == 280 && HashBytes(expectedHelper) == expectedHelperSha256 &&
            legacyCue.Length == 131 && HashBytes(legacyCue) == legacyCueSha256 &&
            legacyHelper.Length == 277 && HashBytes(legacyHelper) == legacyHelperSha256 &&
            !expectedCue.SequenceEqual(legacyCue) && !expectedHelper.SequenceEqual(legacyHelper),
        "The independent V3/legacy CUE or Finder-helper literal oracle changed.");
    Require(
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputCueByteLength == expectedCue.Length &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedOutputCueSha256 == expectedCueSha256 &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedFinderHelperByteLength == expectedHelper.Length &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.ExpectedFinderHelperSha256 == expectedHelperSha256 &&
        actualCue.SequenceEqual(expectedCue) && actualHelper.SequenceEqual(expectedHelper) &&
        !actualCue.SequenceEqual(legacyCue) && !actualHelper.SequenceEqual(legacyHelper),
        "The exact Core4-171 V3 CUE or Finder-helper byte identity changed.");
}

static byte[] ExactV3CueBytes() => Encoding.ASCII.GetBytes(
    "FILE \"Unused-Level-65-Authoring-Reward-Staging-Core4-171-V3-RUNTIME-CANDIDATE.bin\" BINARY\n" +
    "  TRACK 01 MODE2/2352\n" +
    "    INDEX 01 00:00:00\n");

static byte[] LegacyCueBytes() => Encoding.ASCII.GetBytes(
    "FILE \"Unused-Level-65-Authoring-Reward-Staging-Core4-171-RUNTIME-CANDIDATE.bin\" BINARY\n" +
    "  TRACK 01 MODE2/2352\n" +
    "    INDEX 01 00:00:00\n");

static byte[] ExactV3FinderHelperBytes() => Encoding.UTF8.GetBytes(
    "#!/bin/zsh\n" +
    "set -euo pipefail\n" +
    "\n" +
    "cue_name='Unused-Level-65-Authoring-Reward-Staging-Core4-171-V3-RUNTIME-CANDIDATE.cue'\n" +
    "cue_path=\"${0:A:h}/${cue_name}\"\n" +
    "if [[ ! -f \"$cue_path\" ]]; then\n" +
    "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
    "  exit 1\n" +
    "fi\n" +
    "/usr/bin/open -R \"$cue_path\"\n");

static byte[] LegacyFinderHelperBytes() => Encoding.UTF8.GetBytes(
    "#!/bin/zsh\n" +
    "set -euo pipefail\n" +
    "\n" +
    "cue_name='Unused-Level-65-Authoring-Reward-Staging-Core4-171-RUNTIME-CANDIDATE.cue'\n" +
    "cue_path=\"${0:A:h}/${cue_name}\"\n" +
    "if [[ ! -f \"$cue_path\" ]]; then\n" +
    "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
    "  exit 1\n" +
    "fi\n" +
    "/usr/bin/open -R \"$cue_path\"\n");

static void VerifyPublishedFilesystemRoles(
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    string publicationParent)
{
    uint userId = Id65AuthoringRewardStagingRuntimeCandidatePublisher.FilesystemUserIdForSmoke();
    UnixFileMode mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    UnixFileMode mode0755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    UnixFileMode mode0600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    RequireOwnedMode(publicationParent, userId, mode0755, directory: true, "created publication parent");
    RequireOwnedMode(paths.OutputDirectoryPath, userId, mode0755, directory: true, "published output");
    foreach (string path in CandidatePaths(paths))
    {
        RequireOwnedMode(
            path,
            userId,
            path == paths.FinderHelperPath ? mode0755 : mode0644,
            directory: false,
            Path.GetFileName(path));
    }
    RequireOwnedMode(paths.GlobalWriterLeasePath, userId, mode0600, directory: false, "global writer lease");
    Require(!Directory.Exists(paths.OperationsDirectoryPath),
        "A successful Core4 reward publication left its private operations directory behind.");
}

static void RequireOwnedMode(
    string path,
    uint expectedUserId,
    UnixFileMode expectedMode,
    bool directory,
    string label)
{
    if (NativeLStat(path, out SmokeDarwinStat status) != 0)
        throw new IOException($"Could not lstat the {label} smoke fixture (errno {Marshal.GetLastPInvokeError()}).");
    ushort expectedType = directory ? (ushort)0x4000 : (ushort)0x8000;
    Require((status.Mode & 0xF000) == expectedType && status.UserId == expectedUserId &&
            (status.Mode & 0x0FFF) == checked((ushort)expectedMode) &&
            (directory || status.LinkCount == 1),
        $"The {label} owner/type/link/mode is not exact.");
}

static CoordinatedTamper ApplyCoordinatedTamper(
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
{
    JsonSerializerOptions options = PublisherJsonOptions();
    Dictionary<string, byte[]> originals = new(StringComparer.Ordinal)
    {
        [paths.ConstructionPlanPath] = File.ReadAllBytes(paths.ConstructionPlanPath),
        [paths.StaticReadbackReceiptPath] = File.ReadAllBytes(paths.StaticReadbackReceiptPath),
        [paths.RuntimeChecklistPath] = File.ReadAllBytes(paths.RuntimeChecklistPath),
        [paths.LocationGuidePath] = File.ReadAllBytes(paths.LocationGuidePath)
    };
    Id65AuthoringRewardStagingRuntimeCandidatePlan plan =
        JsonSerializer.Deserialize<Id65AuthoringRewardStagingRuntimeCandidatePlan>(
            originals[paths.ConstructionPlanPath],
            options) ?? throw new InvalidDataException("Could not read the smoke plan.");
    WriteCanonical(paths.ConstructionPlanPath, plan with { CoreSectorCount = plan.CoreSectorCount + 1 }, options);
    File.WriteAllBytes(
        paths.RuntimeChecklistPath,
        [.. originals[paths.RuntimeChecklistPath], .. Encoding.UTF8.GetBytes("foreign coordinated line\n")]);
    byte[] guide = originals[paths.LocationGuidePath];
    File.WriteAllBytes(
        paths.LocationGuidePath,
        [.. guide.AsSpan(0, guide.Length - 7), .. Encoding.UTF8.GetBytes("<!-- foreign -->\n</svg>\n")]);
    Id65AuthoringRewardStagingRuntimeCandidateReceipt receipt =
        JsonSerializer.Deserialize<Id65AuthoringRewardStagingRuntimeCandidateReceipt>(
            originals[paths.StaticReadbackReceiptPath],
            options) ?? throw new InvalidDataException("Could not read the smoke receipt.");
    WriteCanonical(
        paths.StaticReadbackReceiptPath,
        receipt with
        {
            ConstructionPlanSha256 = HashFile(paths.ConstructionPlanPath),
            RuntimeChecklistSha256 = HashFile(paths.RuntimeChecklistPath),
            LocationGuideSha256 = HashFile(paths.LocationGuidePath)
        },
        options);
    return new(originals);
}

static void VerifyApiBoundary()
{
    string[] requestProperties = typeof(Id65AuthoringRewardStagingRuntimeCandidateRequest)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => property.Name)
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expected =
    [
        "BaseCuePath",
        "BaseImagePath",
        "CompileRequest",
        "OutputDirectoryPath",
        "ReplaceExistingCandidate",
        "TestStageHook",
        "WorkspaceRoot"
    ];
    Require(requestProperties.SequenceEqual(expected) &&
            typeof(Id65AuthoringRewardStagingRuntimeCandidateRequest).GetProperties().All(property =>
                !property.Name.Contains("Evidence", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Contains("Authorized", StringComparison.OrdinalIgnoreCase) &&
                !property.PropertyType.Name.StartsWith("Id65Compiled", StringComparison.Ordinal)),
        "The publisher request gained an evidence/token/authorization/compiled-afterimage surface.");
    Type tokenType = typeof(Id65V3RuntimeEvidenceContract).GetNestedType(
        "Id65V3FocusedRuntimePassToken",
        BindingFlags.NonPublic) ?? throw new MissingMemberException("The opaque token type is missing.");
    Require(tokenType.IsSealed && tokenType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .All(constructor => constructor.IsPrivate) &&
            tokenType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
                .All(property => property.GetMethod?.IsPrivate == true) &&
            !tokenType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => method.Name.Contains("Mint", StringComparison.Ordinal)),
        "The focused-runtime capability construction or state is not fully private.");
    MethodInfo[] createMethods = typeof(Id65AuthoringRewardStagingRuntimeCandidatePublisher)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(method => method.Name.Contains("Create", StringComparison.Ordinal))
        .ToArray();
    Require(createMethods.Single(method => method.Name == "CreateAsync").GetParameters()
            .All(parameter => parameter.ParameterType != tokenType) &&
            createMethods.Single(method => method.Name == "CreateVerifiedAsync").IsPrivate &&
            Id65V3RuntimeEvidenceContract.ExpectedFocusedRuntimeEvidenceSha256 == "PENDING",
        "A production publisher bypass opened or the real evidence pin is no longer PENDING.");
}

static void VerifyPinnedEvidenceReaderGuards(string smokeRoot)
{
    MethodInfo reader = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
        "ReadPinnedFocusedRuntimeEvidence",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException("The private pinned-evidence reader is missing.");
    string root = Path.Combine(smokeRoot, "pinned-reader");
    string evidence = Path.Combine(
        root,
        Id65V3RuntimeEvidenceContract.FocusedRuntimeEvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
    File.WriteAllBytes(evidence, Encoding.UTF8.GetBytes("{}"));
    _ = InvokeReflection(reader, null, [root]);

    string hardlink = evidence + ".hardlink";
    Require(NativeLink(evidence, hardlink) == 0, "Could not create the pinned-reader hard-link smoke fixture.");
    RejectReflection(() => InvokeReflection(reader, null, [root]), "st_nlink", "pinned evidence hard link");
    File.Delete(hardlink);

    string target = evidence + ".target";
    File.Move(evidence, target);
    File.CreateSymbolicLink(evidence, target);
    RejectReflection(() => InvokeReflection(reader, null, [root]), "symbolic", "pinned evidence leaf symlink");
    File.Delete(evidence);
    File.Move(target, evidence);

    File.Delete(evidence);
    Require(NativeMkFifo(evidence, Convert.ToUInt16("600", 8)) == 0,
        "Could not create the pinned-reader FIFO smoke fixture.");
    RejectReflection(() => InvokeReflection(reader, null, [root]), "regular file", "pinned evidence FIFO");
    File.Delete(evidence);

    string ancestorRoot = Path.Combine(smokeRoot, "pinned-reader-ancestor");
    string realDocs = Path.Combine(smokeRoot, "pinned-reader-real-docs");
    string realEvidence = Path.Combine(
        realDocs,
        "runtime-evidence",
        Path.GetFileName(evidence));
    Directory.CreateDirectory(Path.GetDirectoryName(realEvidence)!);
    File.WriteAllBytes(realEvidence, Encoding.UTF8.GetBytes("{}"));
    Directory.CreateDirectory(ancestorRoot);
    Directory.CreateSymbolicLink(Path.Combine(ancestorRoot, "docs"), realDocs);
    RejectReflection(
        () => InvokeReflection(reader, null, [ancestorRoot]),
        "symbolic",
        "pinned evidence ancestor symlink");
}

static void VerifyCanonicalSidecarReader(string smokeRoot, string originalPlan)
{
    MethodInfo openGeneric = typeof(Id65AuthoringRewardStagingRuntimeCandidatePublisher).GetMethod(
        "ReadCanonicalJson",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException("The private canonical sidecar reader is missing.");
    MethodInfo reader = openGeneric.MakeGenericMethod(typeof(Id65AuthoringRewardStagingRuntimeCandidatePlan));
    byte[] canonical = File.ReadAllBytes(originalPlan);
    string path = Path.Combine(smokeRoot, "canonical-sidecar-matrix.json");
    foreach ((string Label, byte[] Bytes, string Fragment) test in new[]
    {
        ("bom", new byte[] { 0xEF, 0xBB, 0xBF }.Concat(canonical).ToArray(), "BOM"),
        ("invalid utf8", canonical.Take(Math.Max(1, canonical.Length / 2)).Append((byte)0xFF)
            .Concat(canonical.Skip(Math.Max(1, canonical.Length / 2))).ToArray(), "Unable to translate"),
        ("duplicate", Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canonical)
            .Insert(1, "\"schemaVersion\":2,")), "deterministic JSON"),
        ("crlf", Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canonical).Replace("\n", "\r\n")),
            "deterministic JSON"),
        ("trailing", canonical.Concat(new byte[] { 0 }).ToArray(), "invalid after")
    })
    {
        File.WriteAllBytes(path, test.Bytes);
        RejectReflection(
            () => InvokeReflection(reader, null, [path, "publisher plan smoke"]),
            test.Fragment,
            "canonical sidecar " + test.Label);
    }
}

static JsonSerializerOptions PublisherJsonOptions() => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true
};

static void WriteCanonical<T>(string path, T value, JsonSerializerOptions options) =>
    File.WriteAllBytes(path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options) + "\n"));

static byte[] BuildOldSchemaWithoutRequiredProperty(byte[] canonicalBytes, string propertyName)
{
    string canonical = Encoding.UTF8.GetString(canonicalBytes);
    string oldSchema = canonical.Replace(
        "\"schemaVersion\": 2",
        "\"schemaVersion\": 1",
        StringComparison.Ordinal);
    Require(oldSchema != canonical, "The old-schema fixture did not find the schemaVersion 2 field.");

    string needle = $"\"{propertyName}\"";
    string[] lines = oldSchema.Split('\n');
    int[] matchingLines = lines
        .Select((line, index) => (line, index))
        .Where(item => item.line.Contains(needle, StringComparison.Ordinal))
        .Select(item => item.index)
        .ToArray();
    Require(matchingLines.Length == 1,
        $"The old-schema fixture found {matchingLines.Length} `{propertyName}` fields, expected one.");
    return Encoding.UTF8.GetBytes(string.Join(
        "\n",
        lines.Where((_, index) => index != matchingLines[0])));
}

static byte ToggleFileByte(string path, long offset)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Require(offset >= 0 && offset < stream.Length, "The smoke byte-mutation offset escaped its file.");
    stream.Position = offset;
    int current = stream.ReadByte();
    Require(current >= 0, "The smoke byte-mutation read reached EOF.");
    stream.Position = offset;
    stream.WriteByte((byte)(current ^ 0x01));
    stream.Flush(flushToDisk: true);
    return (byte)current;
}

static void WriteFileByte(string path, long offset, byte value)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
    Require(offset >= 0 && offset < stream.Length, "The smoke byte-restore offset escaped its file.");
    stream.Position = offset;
    stream.WriteByte(value);
    stream.Flush(flushToDisk: true);
}

static string CreateOperationFixture(
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    bool createLease = true)
{
    Directory.CreateDirectory(paths.OperationsDirectoryPath);
    File.SetUnixFileMode(
        paths.OperationsDirectoryPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    string operation = Path.Combine(
        paths.OperationsDirectoryPath,
        "core4-reward-v3-runtime-candidate-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(operation);
    File.SetUnixFileMode(
        operation,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    if (createLease)
    {
        File.WriteAllBytes(Path.Combine(operation, "operation.lease"), []);
        File.SetUnixFileMode(
            Path.Combine(operation, "operation.lease"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    return operation;
}

static void WriteRecoveryJournalFixture(
    string operationRoot,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    string phase,
    bool previousOutputExisted,
    string? stagedCandidateFingerprintSha256 = null,
    string? previousCandidateFingerprintSha256 = null,
    string? operationRootOverride = null,
    int schemaVersion = 2)
{
    var journal = new
    {
        SchemaVersion = schemaVersion,
        OperationKind = "id65-authoring-reward-staging-core4-v3-runtime-candidate",
        Phase = phase,
        OperationRootPath = operationRootOverride ?? operationRoot,
        OutputDirectoryPath = paths.OutputDirectoryPath,
        StagedDirectoryPath = Path.Combine(operationRoot, "candidate"),
        BackupDirectoryPath = Path.Combine(operationRoot, "previous-candidate"),
        PreviousOutputExisted = previousOutputExisted,
        StagedCandidateFingerprintSha256 = stagedCandidateFingerprintSha256,
        PreviousCandidateFingerprintSha256 = previousCandidateFingerprintSha256
    };
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
    string journalPath = Path.Combine(operationRoot, "operation-journal.json");
    File.WriteAllBytes(journalPath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(journal, options) + "\n"));
    File.SetUnixFileMode(journalPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}

static async Task RecoverOnlyAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    string label)
{
    const string marker = "stop after startup recovery fixture";
    await RejectAsync(
        () => InvokeVerifiedPublisherAsync(request with
        {
            ReplaceExistingCandidate = true,
            TestStageHook = stage =>
            {
                if (stage == "after-owned-recovery")
                    throw new IOException(marker);
            }
        }, token),
        marker,
        label);
    Require(!Directory.Exists(paths.OperationsDirectoryPath),
        $"The {label} left its recovered operations directory behind.");
}

static void VerifyJournalFingerprintProjectionMatrix()
{
    Id65AuthoringRewardStagingJournalFingerprintSmokeEntry first = new(
        "a.bin",
        new string('a', 64),
        11,
        12,
        501,
        0x81A4,
        1,
        661_547_040,
        101,
        102,
        103,
        104,
        21,
        22,
        501,
        0x41ED);
    Id65AuthoringRewardStagingJournalFingerprintSmokeEntry second = first with
    {
        FileName = "b.cue",
        Sha256 = new string('b', 64),
        Inode = 13,
        ByteLength = 134
    };
    Id65AuthoringRewardStagingJournalFingerprintSmokeEntry[] baselineEntries = [first, second];
    string baseline = Id65AuthoringRewardStagingRuntimeCandidatePublisher
        .HashCandidateTreeJournalFingerprintForSmoke(baselineEntries);
    string independentManifest =
        "id65-core4-reward-candidate-tree-journal-fingerprint-v2\n" +
        string.Create(
            CultureInfo.InvariantCulture,
            $"a.bin|{new string('a', 64)}|11|12|501|33188|1|661547040|21|22|501|16877\n") +
        string.Create(
            CultureInfo.InvariantCulture,
            $"b.cue|{new string('b', 64)}|11|13|501|33188|1|134|21|22|501|16877\n");
    Require(baseline == HashBytes(Encoding.UTF8.GetBytes(independentManifest)),
        "The journal fingerprint v2 projection did not match its independent exact manifest oracle.");

    foreach ((string Label, Id65AuthoringRewardStagingJournalFingerprintSmokeEntry Value) excluded in new[]
    {
        ("mtime seconds", first with { ModifiedSeconds = 201 }),
        ("mtime nanoseconds", first with { ModifiedNanoseconds = 202 }),
        ("ctime seconds", first with { ChangedSeconds = 203 }),
        ("ctime nanoseconds", first with { ChangedNanoseconds = 204 })
    })
    {
        Require(
            Id65AuthoringRewardStagingRuntimeCandidatePublisher
                .HashCandidateTreeJournalFingerprintForSmoke([excluded.Value, second]) == baseline,
            $"The journal fingerprint v2 unexpectedly persisted excluded {excluded.Label}.");
    }

    foreach ((string Label, Id65AuthoringRewardStagingJournalFingerprintSmokeEntry Value) bound in new[]
    {
        ("filename", first with { FileName = "z.bin" }),
        ("SHA-256", first with { Sha256 = new string('c', 64) }),
        ("leaf device", first with { Device = 31 }),
        ("leaf inode", first with { Inode = 32 }),
        ("leaf uid", first with { UserId = 502 }),
        ("leaf mode/type", first with { Mode = 0x8180 }),
        ("leaf link count", first with { LinkCount = 2 }),
        ("leaf length", first with { ByteLength = 661_547_039 }),
        ("directory device", first with { DirectoryDevice = 41 }),
        ("directory inode", first with { DirectoryInode = 42 }),
        ("directory uid", first with { DirectoryUserId = 502 }),
        ("directory mode/type", first with { DirectoryMode = 0x41C0 })
    })
    {
        Require(
            Id65AuthoringRewardStagingRuntimeCandidatePublisher
                .HashCandidateTreeJournalFingerprintForSmoke([bound.Value, second]) != baseline,
            $"The journal fingerprint v2 failed to bind {bound.Label}.");
    }
    Require(
        Id65AuthoringRewardStagingRuntimeCandidatePublisher
            .HashCandidateTreeJournalFingerprintForSmoke([second, first]) != baseline &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher
            .HashCandidateTreeJournalFingerprintForSmoke([first]) != baseline &&
        Id65AuthoringRewardStagingRuntimeCandidatePublisher
            .HashCandidateTreeJournalFingerprintForSmoke([first, second, second]) != baseline,
        "The journal fingerprint v2 failed to bind exact entry order/count.");
}

static void VerifyLiveFingerprintMutationRestorationMatrix(
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
    IReadOnlyDictionary<string, FileSnapshot> cleanSnapshot)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The live fingerprint smoke requires macOS filesystem semantics.");
    string leaf = paths.RuntimeChecklistPath;
    UnixFileMode exactMode = GetMacMode(leaf);
    RejectReflection(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.VerifyLiveCandidateFingerprintForSmoke(
            paths.OutputDirectoryPath,
            () =>
            {
                SetMacMode(
                    leaf,
                    exactMode | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);
                SetMacMode(leaf, exactMode);
            }),
        "exact verified identity",
        "live same-vnode mode mutate-and-restore");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)),
        "The live mode mutate-and-restore fixture did not restore semantic candidate state.");

    RejectReflection(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.VerifyLiveCandidateFingerprintForSmoke(
            paths.OutputDirectoryPath,
            () =>
            {
                byte original = ToggleFileByte(leaf, 0);
                WriteFileByte(leaf, 0, original);
            }),
        "exact verified identity",
        "live same-vnode content mutate-and-restore");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)),
        "The live content mutate-and-restore fixture did not restore semantic candidate state.");

    DateTime exactMtime = File.GetLastWriteTimeUtc(leaf);
    RejectReflection(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.VerifyLiveCandidateFingerprintForSmoke(
            paths.OutputDirectoryPath,
            () =>
            {
                File.SetLastWriteTimeUtc(leaf, exactMtime.AddSeconds(2));
                File.SetLastWriteTimeUtc(leaf, exactMtime);
            }),
        "exact verified identity",
        "live timestamp mutate-and-restore");
    Require(SnapshotsEqual(cleanSnapshot, SnapshotCandidate(paths)),
        "The live timestamp mutate-and-restore fixture did not restore semantic candidate state.");
}

static void VerifyFilesystemIdentityMatrix()
{
    uint userId = Id65AuthoringRewardStagingRuntimeCandidatePublisher.FilesystemUserIdForSmoke();
    UnixFileMode mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    UnixFileMode mode0755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    UnixFileMode mode0700 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateUserIdentityForSmoke(userId, userId);
    Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
        userId, 1, 0x8000, mode0644, mode0644);
    Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateDirectoryIdentityForSmoke(
        userId, 0x4000, mode0755, mode0755);

    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateUserIdentityForSmoke(
            userId,
            checked(userId + 1)),
        "uid/euid drift");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            checked(userId + 1), 1, 0x8000, mode0644, mode0644),
        "foreign regular owner");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            userId, 2, 0x8000, mode0644, mode0644),
        "regular hard link");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            userId, 1, 0x1000, mode0644, mode0644),
        "FIFO file type");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            userId, 1, 0x8000, mode0644 | (UnixFileMode)0x0800, mode0644),
        "regular special bit");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            userId, 1, 0x8000, mode0644 & ~UnixFileMode.OtherRead, mode0644),
        "regular mode minus one bit");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateRegularIdentityForSmoke(
            userId, 1, 0x8000, mode0644 | UnixFileMode.OtherWrite, mode0644),
        "regular mode plus one bit");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateDirectoryIdentityForSmoke(
            checked(userId + 1), 0x4000, mode0755, mode0755),
        "foreign directory owner");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateDirectoryIdentityForSmoke(
            userId, 0x8000, mode0755, mode0755),
        "non-directory type");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateDirectoryIdentityForSmoke(
            userId, 0x4000, mode0700, mode0755),
        "directory role mode swap");
    ExpectInvalid(
        () => Id65AuthoringRewardStagingRuntimeCandidatePublisher.ValidateDirectoryIdentityForSmoke(
            userId, 0x4000, mode0755 | UnixFileMode.OtherWrite, mode0755),
        "writable directory");
}

static void VerifyCandidateDirectoryRenameFixture(string smokeRoot)
{
    string stage = Path.Combine(smokeRoot, "rename-stage");
    string output = Path.Combine(smokeRoot, "rename-output");
    string backup = Path.Combine(smokeRoot, "rename-backup");
    UnixFileMode mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    UnixFileMode mode0755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    Directory.CreateDirectory(stage);
    File.SetUnixFileMode(stage, mode0755);
    string prefix = Id65AuthoringRewardStagingRuntimeCandidatePublisher.OutputPrefix;
    string[] names =
    [
        prefix + ".bin",
        prefix + ".cue",
        prefix + "-construction-plan.json",
        prefix + "-static-readback-receipt.json",
        prefix + "-runtime-checklist.md",
        prefix + "-location-guide.svg",
        prefix + "-Reveal-in-Finder.command"
    ];
    foreach (string name in names)
    {
        string path = Path.Combine(stage, name);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(name + "\n"));
        File.SetUnixFileMode(path, name.EndsWith(".command", StringComparison.Ordinal) ? mode0755 : mode0644);
    }
    Id65AuthoringRewardStagingRuntimeCandidatePublisher.VerifyCandidateDirectoryRenameForSmoke(
        stage,
        output,
        backup);
    Require(!Directory.Exists(stage) && !Directory.Exists(output) && Directory.Exists(backup),
        "The stage-to-output/output-to-backup vnode rename fixture did not complete exactly.");
    DeleteTreeWithoutFollowingLinks(backup);
}

static void VerifyAtomicPrivateCreationMode(string smokeRoot)
{
    string probe = Path.Combine(smokeRoot, "atomic-private-create-mode-probe");
    UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    ushort previousUmask = NativeUmask(0);
    try
    {
        UnixFileMode actual =
            Id65AuthoringRewardStagingRuntimeCandidatePublisher
                .ObserveAtomicPrivateFileCreationModeForSmoke(probe);
        Require(actual == expected && File.GetUnixFileMode(probe) == expected,
            "Atomic private-file creation did not request exact 0600 under umask 000.");
    }
    finally
    {
        _ = NativeUmask(previousUmask);
        if (File.Exists(probe))
            File.Delete(probe);
    }
}

static async Task VerifyPublicationParentBoundaryAsync(
    Id65AuthoringRewardStagingRuntimeCandidateRequest request,
    object token,
    string repositoryRoot,
    FullReadbackFixtureInput fullReadbackFixture,
    string smokeRoot,
    SmokeDirectoryIdentity smokeRootIdentity,
    string publicationWorkspace,
    SmokeDirectoryIdentity publicationWorkspaceIdentity,
    string outputParent)
{
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths =
        Id65AuthoringRewardStagingRuntimeCandidatePublisher.CreatePaths(request.OutputDirectoryPath);
    UnixFileMode mode0700 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    UnixFileMode mode0777 =
        mode0700 |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    Directory.CreateDirectory(outputParent);
    File.SetUnixFileMode(outputParent, mode0777);
    try
    {
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request, token),
            "group- or other-writable",
            "unsafe publication parent");
        Require(!Directory.Exists(paths.OutputDirectoryPath) &&
                !Directory.Exists(paths.OperationsDirectoryPath) &&
                !File.Exists(paths.GlobalWriterLeasePath),
            "Unsafe publication-parent rejection created publisher state.");
    }
    finally
    {
        File.SetUnixFileMode(outputParent, mode0700);
        Directory.Delete(outputParent);
    }

    RequireSamePrivateSmokeDirectory(smokeRoot, smokeRootIdentity, "publisher smoke anchor before ancestor negative");
    RequireSamePrivateSmokeDirectory(
        publicationWorkspace,
        publicationWorkspaceIdentity,
        "publisher publication workspace before ancestor negative");
    RequireSyntheticTokenFresh(token, repositoryRoot, fullReadbackFixture, "before unsafe publication ancestor");
    File.SetUnixFileMode(publicationWorkspace, mode0777);
    try
    {
        RequireSyntheticTokenFresh(token, repositoryRoot, fullReadbackFixture, "during unsafe publication ancestor");
        await RejectAsync(
            () => InvokeVerifiedPublisherAsync(request, token),
            "group- or other-writable",
            "unsafe publication ancestor");
        RequireSyntheticTokenFresh(token, repositoryRoot, fullReadbackFixture, "after unsafe publication ancestor");
        Require(!Directory.Exists(outputParent) &&
                !Directory.Exists(paths.OperationsDirectoryPath) &&
                !File.Exists(paths.GlobalWriterLeasePath),
            "Unsafe ancestor rejection created a publication parent, operation, or lease.");
    }
    finally
    {
        SmokeDirectoryIdentity during = RequireSmokeDirectory(
            publicationWorkspace,
            "publisher publication workspace before mode restore");
        Require(during.SameFile(publicationWorkspaceIdentity) &&
                during.UserId == publicationWorkspaceIdentity.UserId &&
                during.Permissions == Convert.ToUInt16("777", 8),
            "The publication workspace changed identity during the unsafe-ancestor fixture.");
        File.SetUnixFileMode(publicationWorkspace, mode0700);
    }
    RequireSamePrivateSmokeDirectory(smokeRoot, smokeRootIdentity, "publisher smoke anchor after ancestor negative");
    RequireSamePrivateSmokeDirectory(
        publicationWorkspace,
        publicationWorkspaceIdentity,
        "publisher publication workspace after ancestor negative");
    RequireSyntheticTokenFresh(token, repositoryRoot, fullReadbackFixture, "after workspace mode restoration");
}

static void RequireSyntheticTokenFresh(
    object token,
    string repositoryRoot,
    FullReadbackFixtureInput fixture,
    string boundary)
{
    MethodInfo validator = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
        "RequireFreshFocusedRuntimePassToken",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException("The private V3 token freshness validator is missing.");
    Require(validator.ReturnType == typeof(void) && validator.GetParameters().Length == 2,
        "The private V3 token freshness validator is not the exact void two-argument boundary.");
    try
    {
        _ = validator.Invoke(null, [token, repositoryRoot]);
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
    Id65V3RuntimeEvidenceValidation validation = Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        fixture.CanonicalEvidenceUtf8,
        fixture.RepositoryRoot,
        Id65V3RuntimeEvidenceValidationMode.FullReadback,
        fixture.ReadbackContext);
    Require(validation.StructurallyValid &&
            validation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass &&
            validation.Errors.Length == 0,
        $"The synthetic FullReadback token was not fresh {boundary}: " +
        string.Join(" | ", validation.Errors));
}

static void ExpectInvalid(Action action, string label)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException($"The {label} filesystem identity fixture was accepted.");
}

static string[] CandidatePaths(Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
[
    paths.OutputImagePath,
    paths.OutputCuePath,
    paths.ConstructionPlanPath,
    paths.StaticReadbackReceiptPath,
    paths.RuntimeChecklistPath,
    paths.LocationGuidePath,
    paths.FinderHelperPath
];

static SmokeDirectoryIdentity RequirePrivateSmokeDirectory(string path, string label)
{
    SmokeDirectoryIdentity identity = RequireSmokeDirectory(path, label);
    uint expectedUserId = Id65AuthoringRewardStagingRuntimeCandidatePublisher.FilesystemUserIdForSmoke();
    Require(identity.UserId == expectedUserId && identity.Permissions == Convert.ToUInt16("700", 8),
        $"The {label} must be a current-user exact0700 real directory.");
    return identity;
}

static SmokeDirectoryIdentity CreatePrivateSmokeDirectory(string path, string label)
{
    string fullPath = Path.GetFullPath(path);
    if (Directory.Exists(fullPath) || File.Exists(fullPath))
        throw new IOException($"The {label} already exists; refusing non-atomic fixture reuse.");
    ushort mode0700 = Convert.ToUInt16("700", 8);
    if (NativeMkdir(fullPath, mode0700) != 0)
    {
        throw new IOException(
            $"Could not atomically create the {label} (errno {Marshal.GetLastPInvokeError()}).");
    }
    SmokeDirectoryIdentity created = RequireSmokeDirectory(fullPath, label + " immediately after mkdir");
    uint expectedUserId = Id65AuthoringRewardStagingRuntimeCandidatePublisher.FilesystemUserIdForSmoke();
    Require(created.UserId == expectedUserId,
        $"The atomically created {label} is not owned by the current user.");
    if (created.Permissions != mode0700)
    {
        File.SetUnixFileMode(
            fullPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    SmokeDirectoryIdentity exact = RequirePrivateSmokeDirectory(fullPath, label);
    Require(exact.SameFile(created), $"The {label} changed identity during exact-mode normalization.");
    return exact;
}

static void RequireSamePrivateSmokeDirectory(
    string path,
    SmokeDirectoryIdentity expected,
    string label)
{
    SmokeDirectoryIdentity actual = RequirePrivateSmokeDirectory(path, label);
    Require(actual.SameFile(expected), $"The {label} changed device/inode identity.");
}

static SmokeDirectoryIdentity RequireSmokeDirectory(string path, string label)
{
    if (NativeLStat(path, out SmokeDarwinStat status) != 0)
        throw new IOException($"Could not lstat the {label} (errno {Marshal.GetLastPInvokeError()}).");
    Require((status.Mode & 0xF000) == 0x4000 &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
        $"The {label} must be one real directory.");
    return new(status.Device, status.Inode, status.UserId, checked((ushort)(status.Mode & 0x0FFF)));
}

static bool IsPathWithin(string candidate, string parent)
{
    string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
    string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
    return fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}

static void VerifySmokeRootGuardMatrix(
    string smokeRoot,
    SmokeDirectoryIdentity smokeRootIdentity,
    string publicationWorkspace,
    SmokeDirectoryIdentity publicationWorkspaceIdentity)
{
    const string prefix = "spyro-id65-core4-reward-publisher-smoke-";
    string sentinelDirectory = Path.Combine(smokeRoot, "cleanup-guard-external-sentinel");
    SmokeDirectoryIdentity sentinelDirectoryIdentity = CreatePrivateSmokeDirectory(
        sentinelDirectory,
        "cleanup-guard external sentinel directory");
    string sentinelFile = Path.Combine(sentinelDirectory, "must-survive.txt");
    File.WriteAllText(sentinelFile, "publisher smoke cleanup guard sentinel\n", new UTF8Encoding(false));
    File.SetUnixFileMode(sentinelFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    FileSnapshot sentinelSnapshot = SnapshotFile(sentinelFile);

    void AssertPrimaryGuards(string boundary)
    {
        RequireSamePrivateSmokeDirectory(smokeRoot, smokeRootIdentity, "smoke anchor " + boundary);
        RequireSamePrivateSmokeDirectory(
            publicationWorkspace,
            publicationWorkspaceIdentity,
            "publication workspace " + boundary);
        RequireSamePrivateSmokeDirectory(
            sentinelDirectory,
            sentinelDirectoryIdentity,
            "external sentinel directory " + boundary);
        Require(File.Exists(sentinelFile) && SnapshotFile(sentinelFile) == sentinelSnapshot,
            $"The external cleanup sentinel changed {boundary}.");
    }

    void ExpectGuardRefusal(Action action, string fragment, string label)
    {
        Exception? failure = null;
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        Require(failure is not null &&
                failure.ToString().Contains(fragment, StringComparison.OrdinalIgnoreCase),
            $"The {label} guard did not reject through `{fragment}`.");
        AssertPrimaryGuards("after " + label);
    }

    ExpectGuardRefusal(
        () => CreatePrivateSmokeDirectory(publicationWorkspace, "existing publication workspace"),
        "already exists",
        "atomic EEXIST");

    string wrongParent = Path.Combine(smokeRoot, prefix + Guid.NewGuid().ToString("N"));
    SmokeDirectoryIdentity wrongParentIdentity = CreatePrivateSmokeDirectory(
        wrongParent,
        "wrong-parent cleanup guard");
    ExpectGuardRefusal(
        () => DeleteOwnedSmokeRoot(wrongParent, wrongParentIdentity),
        "non-owned publisher-smoke root",
        "wrong-parent cleanup");
    DeleteEmptyPrivateSmokeDirectory(wrongParent, wrongParentIdentity, "wrong-parent cleanup guard");
    Require(!Directory.Exists(wrongParent) && !File.Exists(wrongParent),
        "The wrong-parent disposable guard was not removed exactly.");
    AssertPrimaryGuards("after wrong-parent disposable cleanup");

    string[] malformedLeaves =
    [
        "spyro-id65-core4-reward-publisher-smok-" + Guid.NewGuid().ToString("N"),
        prefix + Guid.NewGuid().ToString("N")[..31],
        prefix + "0000000000000000000000000000000A"
    ];
    foreach (string malformedLeaf in malformedLeaves)
    {
        string malformedPath = Path.Combine("/private/tmp", malformedLeaf);
        SmokeDirectoryIdentity malformedIdentity = CreatePrivateSmokeDirectory(
            malformedPath,
            "malformed-name cleanup guard");
        ExpectGuardRefusal(
            () => DeleteOwnedSmokeRoot(malformedPath, malformedIdentity),
            "non-owned publisher-smoke root",
            "malformed-name cleanup " + malformedLeaf);
        DeleteEmptyPrivateSmokeDirectory(
            malformedPath,
            malformedIdentity,
            "malformed-name cleanup guard");
        Require(!Directory.Exists(malformedPath) && !File.Exists(malformedPath),
            "A malformed-name disposable guard was not removed exactly.");
        AssertPrimaryGuards("after malformed-name disposable cleanup");
    }

    string identityPath = Path.Combine("/private/tmp", prefix + Guid.NewGuid().ToString("N"));
    SmokeDirectoryIdentity identity = CreatePrivateSmokeDirectory(identityPath, "identity cleanup guard");
    SmokeDirectoryIdentity wrongDevice = identity with
    {
        Device = identity.Device == uint.MaxValue ? identity.Device - 1 : identity.Device + 1
    };
    ExpectGuardRefusal(
        () => DeleteOwnedSmokeRoot(identityPath, wrongDevice),
        "changed device/inode",
        "wrong-device cleanup");
    RequireSamePrivateSmokeDirectory(identityPath, identity, "identity guard after wrong-device refusal");
    SmokeDirectoryIdentity wrongInode = identity with
    {
        Inode = identity.Inode == ulong.MaxValue ? identity.Inode - 1 : identity.Inode + 1
    };
    ExpectGuardRefusal(
        () => DeleteOwnedSmokeRoot(identityPath, wrongInode),
        "changed device/inode",
        "wrong-inode cleanup");
    RequireSamePrivateSmokeDirectory(identityPath, identity, "identity guard after wrong-inode refusal");
    DeleteOwnedSmokeRoot(identityPath, identity);
    Require(!Directory.Exists(identityPath) && !File.Exists(identityPath),
        "The valid identity disposable root was not removed exactly.");
    AssertPrimaryGuards("after identity disposable cleanup");

    string symlinkRoot = Path.Combine("/private/tmp", prefix + Guid.NewGuid().ToString("N"));
    Directory.CreateSymbolicLink(symlinkRoot, sentinelDirectory);
    ExpectGuardRefusal(
        () => DeleteOwnedSmokeRoot(symlinkRoot, sentinelDirectoryIdentity),
        "real directory",
        "symlink-root cleanup");
    Require((File.GetAttributes(symlinkRoot) & FileAttributes.ReparsePoint) != 0,
        "The symlink-root guard was changed on refusal.");
    Directory.Delete(symlinkRoot);
    Require(!Directory.Exists(symlinkRoot) && !File.Exists(symlinkRoot),
        "The symlink-root guard was not unlinked exactly.");
    AssertPrimaryGuards("after symlink-root unlink");

    string fileRoot = Path.Combine("/private/tmp", prefix + Guid.NewGuid().ToString("N"));
    File.WriteAllText(fileRoot, "not a smoke directory\n", new UTF8Encoding(false));
    File.SetUnixFileMode(fileRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    FileSnapshot fileRootSnapshot = SnapshotFile(fileRoot);
    ExpectGuardRefusal(
        () => DeleteOwnedSmokeRoot(fileRoot, sentinelDirectoryIdentity),
        "real directory",
        "non-directory-root cleanup");
    Require(File.Exists(fileRoot) && SnapshotFile(fileRoot) == fileRootSnapshot,
        "The non-directory cleanup guard changed on refusal.");
    File.Delete(fileRoot);
    Require(!File.Exists(fileRoot) && !Directory.Exists(fileRoot),
        "The non-directory cleanup guard was not removed exactly.");
    AssertPrimaryGuards("after non-directory guard cleanup");

    string noFollowRoot = Path.Combine("/private/tmp", prefix + Guid.NewGuid().ToString("N"));
    SmokeDirectoryIdentity noFollowIdentity = CreatePrivateSmokeDirectory(
        noFollowRoot,
        "no-follow cleanup guard");
    string externalLink = Path.Combine(noFollowRoot, "external-sentinel-link");
    Directory.CreateSymbolicLink(externalLink, sentinelDirectory);
    DeleteOwnedSmokeRoot(noFollowRoot, noFollowIdentity);
    Require(!Directory.Exists(noFollowRoot) && !File.Exists(noFollowRoot),
        "The no-follow disposable root was not removed exactly.");
    AssertPrimaryGuards("after no-follow symlink-child cleanup");

    Require(File.Exists(sentinelFile) && SnapshotFile(sentinelFile) == sentinelSnapshot,
        "The external sentinel changed before exact guard cleanup.");
    File.Delete(sentinelFile);
    DeleteEmptyPrivateSmokeDirectory(
        sentinelDirectory,
        sentinelDirectoryIdentity,
        "external sentinel directory");
    Require(!Directory.Exists(sentinelDirectory) && !File.Exists(sentinelDirectory),
        "The external sentinel directory was not removed exactly.");
    RequireSamePrivateSmokeDirectory(smokeRoot, smokeRootIdentity, "smoke anchor after guard matrix");
    RequireSamePrivateSmokeDirectory(
        publicationWorkspace,
        publicationWorkspaceIdentity,
        "publication workspace after guard matrix");
}

static void DeleteEmptyPrivateSmokeDirectory(
    string path,
    SmokeDirectoryIdentity expectedIdentity,
    string label)
{
    RequireSamePrivateSmokeDirectory(path, expectedIdentity, label + " before deletion");
    Require(!Directory.EnumerateFileSystemEntries(path).Any(),
        $"The {label} is not empty and was preserved.");
    Directory.Delete(path);
}

static void DeleteOwnedSmokeRoot(
    string smokeRoot,
    SmokeDirectoryIdentity expectedRootIdentity)
{
    string fullRoot = Path.GetFullPath(smokeRoot);
    string leaf = Path.GetFileName(fullRoot);
    string suffix = leaf.StartsWith(
            "spyro-id65-core4-reward-publisher-smoke-",
            StringComparison.Ordinal)
        ? leaf["spyro-id65-core4-reward-publisher-smoke-".Length..]
        : string.Empty;
    if (!string.Equals(Path.GetDirectoryName(fullRoot), "/private/tmp", StringComparison.Ordinal) ||
        suffix.Length != 32 || suffix.Any(character =>
            character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
    {
        throw new InvalidOperationException("Refusing to clean a non-owned publisher-smoke root.");
    }
    RequireSamePrivateSmokeDirectory(
        fullRoot,
        expectedRootIdentity,
        "owned publisher-smoke cleanup root");
    DeleteTreeWithoutFollowingLinks(fullRoot);
}

static void DeleteTreeWithoutFollowingLinks(string directory)
{
    foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
    {
        FileAttributes attributes = File.GetAttributes(entry);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            if ((attributes & FileAttributes.Directory) != 0)
                Directory.Delete(entry);
            else
                File.Delete(entry);
        }
        else if ((attributes & FileAttributes.Directory) != 0)
        {
            DeleteTreeWithoutFollowingLinks(entry);
        }
        else
        {
            File.Delete(entry);
        }
    }
    Directory.Delete(directory);
}

static IReadOnlyDictionary<string, FileSnapshot> SnapshotCandidate(
    Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
    SnapshotFlatCandidateDirectory(paths.OutputDirectoryPath);

static IReadOnlyDictionary<string, FileSnapshot> SnapshotFlatCandidateDirectory(string directory) =>
    Directory.EnumerateFiles(directory)
        .ToDictionary(path => Path.GetFileName(path)!, SnapshotFile, StringComparer.Ordinal);

static bool SnapshotsEqual(
    IReadOnlyDictionary<string, FileSnapshot> left,
    IReadOnlyDictionary<string, FileSnapshot> right) =>
    left.Count == right.Count && left.All(pair =>
        right.TryGetValue(pair.Key, out FileSnapshot? snapshot) &&
        pair.Value.ByteLength == snapshot.ByteLength && pair.Value.Sha256 == snapshot.Sha256 &&
        pair.Value.UnixMode == snapshot.UnixMode);

static FileSnapshot SnapshotFile(string path) =>
    new(
        new FileInfo(path).Length,
        HashFile(path),
        File.GetLastWriteTimeUtc(path),
        OperatingSystem.IsMacOS() ? File.GetUnixFileMode(path) : null);

static SmokeVnodeIdentity SnapshotSmokeVnode(string path, string label)
{
    if (NativeLStat(path, out SmokeDarwinStat status) != 0)
        throw new IOException($"Could not lstat the {label} (errno {Marshal.GetLastPInvokeError()}).");
    return new(status.Device, status.Inode, status.UserId, status.Mode, status.LinkCount);
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string HashBytes(byte[] bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static byte[] ReadWad(string imagePath, long wadOffset, int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord wad = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
    Require(wad.Lba == 37 && wad.Size == 0x6C18800, "Foundation WAD.WAD layout changed.");
    return DiscImage.ReadFileBytes(image, layout, wad.Lba, wadOffset, byteLength);
}

static object InvokeReflection(MethodInfo method, object? instance, object?[] arguments)
{
    try
    {
        return method.Invoke(instance, arguments) ??
            throw new InvalidOperationException($"Reflection method {method.Name} returned null.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

static async Task RejectAsync(Func<Task> action, string fragment, string label)
{
    Exception? failure = null;
    try
    {
        await action();
    }
    catch (Exception exception)
    {
        failure = exception;
    }
    if (failure is null)
        throw new InvalidOperationException($"The {label} case was accepted.");
    if (!failure.ToString().Contains(fragment, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"The {label} case rejected without required text `{fragment}`.",
            failure);
    }
}

static async Task RejectAnyAsync(Func<Task> action, string label)
{
    Exception? failure = null;
    try
    {
        await action();
    }
    catch (Exception exception)
    {
        failure = exception;
    }
    if (failure is null)
        throw new InvalidOperationException($"The {label} case was accepted.");
}

static void RejectReflection(Action action, string fragment, string label)
{
    Exception? failure = null;
    try
    {
        action();
    }
    catch (Exception exception)
    {
        failure = exception;
    }
    if (failure is null)
        throw new InvalidOperationException($"The {label} case was accepted.");
    if (!failure.ToString().Contains(fragment, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"The {label} case rejected without required text `{fragment}`.",
            failure);
    }
}

static async Task VerifyRejectHelpersDoNotSelfPassAsync()
{
    bool asyncSentinelObserved = false;
    try
    {
        await RejectAsync(() => Task.CompletedTask, "self-test-fragment", "async helper self-test");
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("was accepted", StringComparison.Ordinal))
    {
        asyncSentinelObserved = true;
    }
    bool reflectionSentinelObserved = false;
    try
    {
        RejectReflection(() => { }, "self-test-fragment", "reflection helper self-test");
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("was accepted", StringComparison.Ordinal))
    {
        reflectionSentinelObserved = true;
    }
    Require(asyncSentinelObserved && reflectionSentinelObserved,
        "A rejection helper can still swallow its own acceptance sentinel.");
}

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void SetMacMode(string path, UnixFileMode mode)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The Core4 reward publisher smoke requires macOS modes.");
    File.SetUnixFileMode(path, mode);
}

static UnixFileMode GetMacMode(string path)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The Core4 reward publisher smoke requires macOS modes.");
    return File.GetUnixFileMode(path);
}

[DllImport("libc", EntryPoint = "link", SetLastError = true)]
static extern int NativeLink(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);

[DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
static extern int NativeMkFifo(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
    ushort mode);

[DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
static extern int NativeMkdir(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
    ushort mode);

[DllImport("libc", EntryPoint = "umask")]
static extern ushort NativeUmask(ushort mode);

[DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
static extern int NativeLStat(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
    out SmokeDarwinStat status);

}

[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct SmokeDarwinStat
{
    [FieldOffset(0)] internal uint Device;
    [FieldOffset(4)] internal ushort Mode;
    [FieldOffset(6)] internal ushort LinkCount;
    [FieldOffset(8)] internal ulong Inode;
    [FieldOffset(16)] internal uint UserId;
}

internal readonly record struct SmokeDirectoryIdentity(
    uint Device,
    ulong Inode,
    uint UserId,
    ushort Permissions)
{
    internal bool SameFile(SmokeDirectoryIdentity other) =>
        Device == other.Device && Inode == other.Inode;
}

internal sealed record FileSnapshot(
    long ByteLength,
    string Sha256,
    DateTime LastWriteUtc,
    UnixFileMode? UnixMode);

internal readonly record struct SmokeVnodeIdentity(
    uint Device,
    ulong Inode,
    uint UserId,
    ushort Mode,
    ushort LinkCount);

internal sealed record FullReadbackFixtureInput(
    string RepositoryRoot,
    byte[] CanonicalEvidenceUtf8,
    Id65V3RuntimeEvidenceReadbackContext ReadbackContext,
    string FirstArtifactPath);

internal sealed class CoordinatedTamper(IReadOnlyDictionary<string, byte[]> originals)
{
    internal void Restore()
    {
        foreach ((string path, byte[] bytes) in originals)
            File.WriteAllBytes(path, bytes);
    }
}
