using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Spyro.Editor.Core.Exporting;

bool publishFinal = args.Contains("--publish-final", StringComparer.Ordinal);
string? requestedRoot = args.FirstOrDefault(value => value != "--publish-final");
string root = FindRoot(requestedRoot);
string baseDirectory = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name");
string basePrefix = Path.Combine(
    baseDirectory,
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";
string wrongBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");
Require(File.Exists(baseImage), "The exact locked display-name BIN is missing.");
Require(File.Exists(baseCue), "The exact locked display-name CUE is missing.");
Require(File.Exists(wrongBase), "The wrong-hash physical-clone fixture is missing.");

string baseHashBefore = await HashFileAsync(baseImage);
string cueHashBefore = await HashFileAsync(baseCue);
long baseLengthBefore = new FileInfo(baseImage).Length;
DateTime baseWriteBefore = File.GetLastWriteTimeUtc(baseImage);
DirectorySnapshot baseDirectoryBefore = await SnapshotDirectoryAsync(baseDirectory);
Require(
    baseHashBefore == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.BaseImageSha256 &&
    cueHashBefore == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.BaseCueSha256,
    "The locked display-name identity changed before the smoke.");

if (publishFinal)
{
    string finalOutput = Path.Combine(
        root,
        "_local",
        "v5-stone-hill-level-replacement",
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult published =
        await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(new(
            root,
            baseImage,
            baseCue,
            finalOutput,
            ReplaceExistingCandidate: Directory.Exists(finalOutput)));
    VerifyResult(published, expectRecovery: false);
    await VerifyExactSevenFilesAsync(published.Paths);
    await VerifyCanonicalReceiptAsync(published);
    Require(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedFinalReceiptSha256 == "PENDING" ||
        published.ArtifactHashes.ReceiptSha256 ==
            UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedFinalReceiptSha256,
        "The final-path canonical receipt hash changed.");
    Console.WriteLine("PUBLISHED ID65 Return Home native-apron runtime candidate");
    Console.WriteLine($"directory={published.Paths.OutputDirectoryPath}");
    Console.WriteLine($"cue={published.Paths.OutputCuePath}");
    Console.WriteLine($"binSha256={published.Receipt.OutputImageSha256}");
    Console.WriteLine($"receiptSha256={published.ArtifactHashes.ReceiptSha256}");
    return;
}

string physicalTemporaryRoot = Path.GetTempPath();
if (OperatingSystem.IsMacOS() && physicalTemporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
    physicalTemporaryRoot = "/private" + physicalTemporaryRoot;
string temporaryParent = Path.Combine(
    physicalTemporaryRoot,
    "spyro-id65-return-home-native-apron-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryParent);
try
{
    string invalidOutput = Path.Combine(temporaryParent, "wrong-name");
    bool wrongNameRejected = false;
    try
    {
        _ = UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreatePaths(invalidOutput);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("owns only", StringComparison.Ordinal))
    {
        wrongNameRejected = true;
    }
    Require(wrongNameRejected && !Directory.Exists(invalidOutput),
        "A wrong output-directory name reached the filesystem.");

    if (!OperatingSystem.IsWindows())
    {
        string physicalAliasTarget = Path.Combine(temporaryParent, "physical-alias-target");
        Directory.CreateDirectory(physicalAliasTarget);
        string aliasSentinel = Path.Combine(physicalAliasTarget, "no-write-sentinel.txt");
        await File.WriteAllTextAsync(aliasSentinel, "ancestor-alias-must-remain-untouched\n");
        string aliasSentinelHash = await HashFileAsync(aliasSentinel);
        string outputAncestorAlias = Path.Combine(temporaryParent, "output-ancestor-alias");
        Directory.CreateSymbolicLink(outputAncestorAlias, physicalAliasTarget);
        string aliasedOutput = Path.Combine(
            outputAncestorAlias,
            UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
        bool ancestorAliasRejected = false;
        try
        {
            _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(new(
                root,
                baseImage,
                baseCue,
                aliasedOutput));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("symlink/reparse", StringComparison.Ordinal))
        {
            ancestorAliasRejected = true;
        }
        finally
        {
            File.Delete(outputAncestorAlias);
        }
        Require(ancestorAliasRejected, "A symlinked output ancestor was accepted.");
        Require(await HashFileAsync(aliasSentinel) == aliasSentinelHash &&
                Directory.EnumerateFileSystemEntries(physicalAliasTarget).SequenceEqual(new[] { aliasSentinel }),
            "The rejected output-ancestor alias wrote through to its physical target.");

        string leaseSymlinkParent = Path.Combine(temporaryParent, "lease-symlink");
        Directory.CreateDirectory(leaseSymlinkParent);
        string leaseSymlinkOutput = Path.Combine(
            leaseSymlinkParent,
            UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths leaseSymlinkPaths =
            UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreatePaths(leaseSymlinkOutput);
        string leaseTarget = Path.Combine(temporaryParent, "lease-target-sentinel.txt");
        await File.WriteAllTextAsync(leaseTarget, "lease-symlink-target-must-not-be-opened\n");
        string leaseTargetHash = await HashFileAsync(leaseTarget);
        File.CreateSymbolicLink(leaseSymlinkPaths.WriterLeasePath, leaseTarget);
        bool leaseSymlinkRejected = false;
        try
        {
            _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(new(
                root,
                baseImage,
                baseCue,
                leaseSymlinkOutput));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("symlink/reparse", StringComparison.Ordinal))
        {
            leaseSymlinkRejected = true;
        }
        finally
        {
            File.Delete(leaseSymlinkPaths.WriterLeasePath);
        }
        Require(leaseSymlinkRejected && await HashFileAsync(leaseTarget) == leaseTargetHash,
            "A writer-lease symlink was accepted or its target bytes changed.");
        Require(!Directory.Exists(leaseSymlinkOutput) &&
                !Directory.Exists(leaseSymlinkPaths.OperationsDirectoryPath),
            "The rejected writer-lease symlink started a publication operation.");
    }

    string wrongParent = Path.Combine(temporaryParent, "wrong-hash");
    string wrongOutput = Path.Combine(
        wrongParent,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
    bool wrongHashRejected = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(new(
            root,
            wrongBase,
            baseCue,
            wrongOutput));
    }
    catch (InvalidDataException ex) when (ex.Message.Contains("SHA-256", StringComparison.Ordinal))
    {
        wrongHashRejected = true;
    }
    Require(wrongHashRejected && !Directory.Exists(wrongOutput),
        "A stale/wrong base produced a Return Home candidate.");
    RequireNoPublisherDebris(wrongParent);

    string publicationParent = Path.Combine(temporaryParent, "publication");
    string output = Path.Combine(
        publicationParent,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateRequest firstRequest = new(
        root,
        baseImage,
        baseCue,
        output,
        ReplaceExistingCandidate: false);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult first =
        await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(firstRequest);
    VerifyResult(first, expectRecovery: false);
    await VerifyExactSevenFilesAsync(first.Paths);
    DirectorySnapshot firstSnapshot = await SnapshotDirectoryAsync(output);
    Require(File.Exists(first.Paths.WriterLeasePath) &&
            (File.GetAttributes(first.Paths.WriterLeasePath) &
                (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0 &&
            new FileInfo(first.Paths.WriterLeasePath).Length == 0,
        "The first publication did not leave a safe persistent writer lease.");
    const string leaseSentinelText = "persistent-lease-handoff-sentinel\n";
    await File.WriteAllTextAsync(first.Paths.WriterLeasePath, leaseSentinelText);
    string persistentLeaseHash = await HashFileAsync(first.Paths.WriterLeasePath);

    ManualResetEventSlim leaseAcquired = new(initialState: false);
    ManualResetEventSlim releaseWriter = new(initialState: false);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateRequest deterministicRequest = new(
        root,
        baseImage,
        baseCue,
        output,
        ReplaceExistingCandidate: true,
        TestStageHook: stage =>
        {
            if (stage != "after-writer-lease-acquired")
                return;
            leaseAcquired.Set();
            if (!releaseWriter.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The smoke did not release the held Return Home writer.");
        });
    Task<UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult> replacementTask =
        Task.Run(() => UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(deterministicRequest));
    Require(leaseAcquired.Wait(TimeSpan.FromSeconds(30)), "The held writer did not acquire its lease.");
    bool contentionRejected = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(
            firstRequest with { ReplaceExistingCandidate = true });
    }
    catch (IOException ex) when (
        ex.Message == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ActiveWriterLeaseMessage)
    {
        contentionRejected = true;
    }
    finally
    {
        releaseWriter.Set();
    }
    Require(contentionRejected, "A concurrent Return Home writer was not rejected fail-closed.");
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult second = await replacementTask;
    VerifyResult(second, expectRecovery: false);
    await VerifyCanonicalReceiptAsync(second);
    DirectorySnapshot secondSnapshot = await SnapshotDirectoryAsync(output);
    Require(firstSnapshot == secondSnapshot, "A deterministic Return Home replacement changed any of seven files.");
    Require(File.Exists(second.Paths.WriterLeasePath) &&
            await HashFileAsync(second.Paths.WriterLeasePath) == persistentLeaseHash,
        "Contention truncated, replaced, or unlinked the persistent writer lease.");

    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult handoff =
        await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(
            firstRequest with { ReplaceExistingCandidate = true });
    VerifyResult(handoff, expectRecovery: false);
    Require(await SnapshotDirectoryAsync(output) == secondSnapshot &&
            File.Exists(handoff.Paths.WriterLeasePath) &&
            await HashFileAsync(handoff.Paths.WriterLeasePath) == persistentLeaseHash,
        "The released persistent lease could not hand off on the same preserved path.");
    RequireNoPublisherDebris(publicationParent);

    bool rollbackFaultObserved = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(
            deterministicRequest with
            {
                TestStageHook = stage =>
                {
                    if (stage == "after-candidate-published")
                        throw new SmokeFaultException("Injected after atomic publication.");
                }
            });
    }
    catch (SmokeFaultException)
    {
        rollbackFaultObserved = true;
    }
    Require(rollbackFaultObserved, "The injected full-directory rollback fault was not observed.");
    Require(await SnapshotDirectoryAsync(output) == secondSnapshot,
        "Full-directory rollback did not restore the exact prior seven-file candidate.");
    RequireNoPublisherDebris(publicationParent);

    string comparisonSentinel = Path.Combine(output, "prior-comparison-sentinel.txt");
    await File.WriteAllTextAsync(comparisonSentinel, "restore-prior-on-precommit-comparison-fault\n");
    DirectorySnapshot comparisonPrior = await SnapshotDirectoryAsync(output);
    bool comparisonFaultObserved = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(
            deterministicRequest with
            {
                TestStageHook = stage =>
                {
                    if (stage == "after-final-staged-published-comparison")
                        throw new SmokeFaultException("Injected after final comparison but before commit.");
                }
            });
    }
    catch (SmokeFaultException)
    {
        comparisonFaultObserved = true;
    }
    Require(comparisonFaultObserved && await SnapshotDirectoryAsync(output) == comparisonPrior,
        "A pre-commit comparison fault did not restore the exact prior directory.");
    File.Delete(comparisonSentinel);
    Require(await SnapshotDirectoryAsync(output) == secondSnapshot,
        "The comparison-fault fixture did not restore the clean seven-file baseline.");
    RequireNoPublisherDebris(publicationParent);

    string committedCleanupSentinel = Path.Combine(output, "prior-commit-sentinel.txt");
    await File.WriteAllTextAsync(committedCleanupSentinel, "must-not-return-after-durable-commit\n");
    bool committedCleanupFaultObserved = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreateAsync(
            deterministicRequest with
            {
                TestStageHook = stage =>
                {
                    if (stage == "after-committed-backup-cleaned")
                        throw new SmokeFaultException("Injected after durable commit and backup cleanup.");
                }
            });
    }
    catch (SmokeFaultException)
    {
        committedCleanupFaultObserved = true;
    }
    Require(committedCleanupFaultObserved && await SnapshotDirectoryAsync(output) == secondSnapshot,
        "A post-commit cleanup fault deleted or rolled back the committed seven-file candidate.");
    Require(Directory.Exists(second.Paths.OperationsDirectoryPath),
        "The post-commit cleanup fixture did not preserve its durable journal for recovery.");
    Require(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter
            .RecoverOwnedOperationsForSmoke(second.Paths),
        "Committed startup cleanup did not report its durable operation.");
    Require(await SnapshotDirectoryAsync(output) == secondSnapshot &&
            await HashFileAsync(second.Paths.WriterLeasePath) == persistentLeaseHash,
        "Committed startup cleanup changed the published candidate or persistent lease.");
    RequireNoPublisherDebris(publicationParent);

    string recoveryParent = Path.Combine(temporaryParent, "recovery");
    string recoveryOutput = Path.Combine(
        recoveryParent,
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths recoveryPaths =
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.CreatePaths(recoveryOutput);
    Directory.CreateDirectory(recoveryParent);
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter
        .SeedInterruptedRecoveryFixtureForSmoke(recoveryPaths);
    Require(
        UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter
            .RecoverOwnedOperationsForSmoke(recoveryPaths),
        "Startup recovery did not report the interrupted operation.");
    Require(File.ReadAllText(Path.Combine(recoveryOutput, "prior-candidate.txt")) == "prior-candidate\n",
        "Startup recovery did not restore the exact prior directory.");
    RequireNoPublisherDebris(recoveryParent);

    await ExpectVerificationFailureAsync(
        second.Paths.LabLinkMetadataPath,
        bytes => [.. bytes, (byte)' '],
        second.Paths,
        "link metadata tamper");
    await ExpectVerificationFailureAsync(
        second.Paths.StaticReadbackReceiptPath,
        bytes => [.. bytes, (byte)'\n'],
        second.Paths,
        "non-canonical receipt tamper");
    await ExpectVerificationFailureAsync(
        second.Paths.OutputCuePath,
        bytes => bytes.Select((value, index) => index == 0 ? (byte)(value ^ 1) : value).ToArray(),
        second.Paths,
        "CUE tamper");
    await ExpectVerificationFailureAsync(
        second.Paths.RuntimeChecklistPath,
        bytes => [.. bytes, (byte)' '],
        second.Paths,
        "runtime checklist tamper");
    await ExpectVerificationFailureAsync(
        second.Paths.LocationGuidePath,
        bytes => [.. bytes, (byte)' '],
        second.Paths,
        "SVG guide tamper");
    await ExpectVerificationFailureAsync(
        second.Paths.FinderHelperPath,
        bytes => bytes.Select((value, index) => index == 0 ? (byte)(value ^ 1) : value).ToArray(),
        second.Paths,
        "Finder helper content tamper");

    await ExpectReceiptMutationFailureAsync(
        second.Paths,
        receipt =>
        {
            receipt["rollbackRecoveryVerified"] = true;
            receipt["fullDirectoryRollbackVerified"] = true;
        },
        "coordinated recovery false-to-true tamper");
    await ExpectReceiptMutationFailureAsync(
        second.Paths,
        receipt => receipt["unknownProperty"] = "must-fail",
        "unknown receipt property");
    await ExpectReceiptMutationFailureAsync(
        second.Paths,
        receipt =>
        {
            JsonNode value = receipt["schemaVersion"]!.DeepClone();
            receipt.Remove("schemaVersion");
            receipt.Add("schemaVersion", value);
        },
        "receipt property-order drift");
    await ExpectReceiptMutationFailureAsync(
        second.Paths,
        receipt =>
        {
            string hash = receipt["outputImageSha256"]!.GetValue<string>();
            receipt["outputImageSha256"] = (hash[0] == '0' ? "1" : "0") + hash[1..];
        },
        "single-byte receipt drift");

    if (OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("The Finder handoff smoke requires a Unix file mode.");
    UnixFileMode expectedHelperMode = File.GetUnixFileMode(second.Paths.FinderHelperPath);
    File.SetUnixFileMode(second.Paths.FinderHelperPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    bool modeTamperRejected = false;
    try
    {
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.VerifyPublishedAsync(second.Paths);
    }
    catch (InvalidDataException ex) when (ex.Message.Contains("0755", StringComparison.Ordinal))
    {
        modeTamperRejected = true;
    }
    finally
    {
        File.SetUnixFileMode(second.Paths.FinderHelperPath, expectedHelperMode);
    }
    Require(modeTamperRejected, "A non-executable Finder helper was accepted.");

    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult finalReadback =
        await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.VerifyPublishedAsync(second.Paths);
    VerifyResult(finalReadback, expectRecovery: false);
    await VerifyCanonicalReceiptAsync(finalReadback);
    Require(await SnapshotDirectoryAsync(output) == secondSnapshot,
        "Tamper fixtures did not restore the exact published candidate.");

    Require(await HashFileAsync(baseImage) == baseHashBefore &&
            await HashFileAsync(baseCue) == cueHashBefore &&
            new FileInfo(baseImage).Length == baseLengthBefore &&
            File.GetLastWriteTimeUtc(baseImage) == baseWriteBefore &&
            await SnapshotDirectoryAsync(baseDirectory) == baseDirectoryBefore,
        "The locked display-name artifact family changed during publication tests.");

    Console.WriteLine(
        "PASS UnusedLevel65ReturnHomeNativeApronRuntimeCandidateSmoke: " +
        "direct locked-base T96/T97 native-T1353 placement; exact candidate-local link metadata; " +
        "one-sector MODE2 readback; deterministic seven-file publication; persistent OS-lease contention/handoff, " +
        "symlink refusal, pre-commit rollback, durable post-commit preservation, startup recovery, canonical receipt, " +
        "tamper, Finder, no-card/no-save guide, and fail-closed gates.");
    Console.WriteLine($"PIN outputImageSha256={finalReadback.Receipt.OutputImageSha256}");
    Console.WriteLine($"PIN changedPhysicalImageBytes={finalReadback.Receipt.ChangedPhysicalImageBytes}");
    Console.WriteLine($"PIN rawSectorDiffSha256={finalReadback.Receipt.RawSectorDiffSha256}");
    Console.WriteLine($"PIN receiptSha256={finalReadback.ArtifactHashes.ReceiptSha256}");
    Console.WriteLine($"PIN labLinkMetadataSha256={finalReadback.ArtifactHashes.LabLinkMetadataSha256}");
    Console.WriteLine($"PIN guideSha256={finalReadback.ArtifactHashes.LocationGuideSha256}");
}
finally
{
    if (Directory.Exists(temporaryParent))
        Directory.Delete(temporaryParent, recursive: true);
}

static void VerifyResult(
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult result,
    bool expectRecovery)
{
    UnusedLevel65ReturnHomeNativeApronPlan plan = result.Plan;
    UnusedLevel65ReturnHomeNativeApronReceipt receipt = result.Receipt;
    Require(plan.ProfileId == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ProfileId &&
            plan.StaticOwnershipProfileId == UnusedLevel65ReturnHomeOwnershipInspector.ProfileId &&
            plan.BaseImageSha256 == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.BaseImageSha256 &&
            plan.BaseCueSha256 == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.BaseCueSha256 &&
            plan.SourceDataSha256 == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.SourceDataSha256 &&
            plan.OutputDataSha256 == UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.OutputDataSha256,
        "The direct candidate identity changed.");
    Require(plan.RowPatches.Count == 2 &&
            plan.RowPatches.Select(row => row.TrueIndex).SequenceEqual(new[] { 96, 97 }) &&
            plan.RowPatches.All(row => row.OnlyXyzChanged) &&
            plan.RowPatches[0].After == new UnusedLevel65ReturnHomeNativeApronPoint(124_585, 102_954, 8_192) &&
            plan.RowPatches[1].After == new UnusedLevel65ReturnHomeNativeApronPoint(124_585, 102_954, 8_704) &&
            plan.RowPatches[0].AfterRowSha256 == "ae2c6cc3bfd5ace302022647c4649154af44e2fe99e7b1953ebee07274e0f1a3" &&
            plan.RowPatches[1].AfterRowSha256 == "e590d9a096f4adf37b0d84fc7a14299ea35a756d4c8bc1caffa69a67e1e0bd66",
        "The exact atomic T96/T97 row witness changed.");
    Require(plan.Support.CollisionComponentSha256 ==
                "84901b6b9faa2f7fb0fce1d3aaa7e00fadf49e2d4bd7b2bcb2a0bb9a77397e2f" &&
            plan.Support.CollisionTriangleIndex == 1_353 &&
            plan.Support.CollisionTriangleHex == "521E20004A1960C000020000" &&
            plan.Support.CollisionTrianglePoints.SequenceEqual(new[]
            {
                new UnusedLevel65ReturnHomeNativeApronPoint(7_762, 6_474, 512),
                new UnusedLevel65ReturnHomeNativeApronPoint(7_890, 6_346, 512),
                new UnusedLevel65ReturnHomeNativeApronPoint(7_762, 6_346, 512)
            }) &&
            plan.Support.CollisionLookupWadOffsets.SequenceEqual(new long[] { 0x6A51672, 0x6A51908 }) &&
            plan.Support.CollisionAssignment == 0 && plan.Support.NormalZ == -16_384 &&
            plan.Support.SurfaceRawZ == 8_192 && plan.Support.LeftMarginRaw == 393 &&
            plan.Support.DiagonalMarginRaw == 237 &&
            plan.Support.PointStrictlyInsideNativeTriangle &&
            plan.Support.NativeTriangleIsExposedTopmostAtPoint &&
            plan.Support.PositiveWindingTerrainExcluded,
        "The exact native T1353 support proof changed.");
    Require(plan.Landing == new UnusedLevel65ReturnHomeNativeApronPoint(125_225, 100_506, 8_550) &&
            plan.PlayerAnchor == new UnusedLevel65ReturnHomeNativeApronPoint(125_225, 100_506, 8_704) &&
            Math.Abs(plan.HorizontalDistanceFromPlayerWorld - 158.14234094637652) < 0.000000001 &&
            Math.Abs(plan.HorizontalNoImmediateTriggerMarginWorld - 94.14234094637652) < 0.000000001 &&
            plan.ComputedReturnHomeDestinationLevelId == 60 &&
            plan.ComputedPauseExitDestinationLevelId == 60 && plan.ComputedPauseExitRoute == -1,
        "The native-apron distance or separate exit destinations changed.");
    Require(plan.ChangedDataByteOffsets.SequenceEqual(new[]
            {
                0x1D227C, 0x1D227D, 0x1D227E, 0x1D2280, 0x1D2281, 0x1D2282, 0x1D2285,
                0x1D22D4, 0x1D22D5, 0x1D22D6, 0x1D22D8, 0x1D22D9, 0x1D22DA, 0x1D22DD
            }) &&
            plan.LogicalXyzPatchBytes == 24 && plan.ChangedLogicalWadBytes == 14 &&
            plan.ExecutablePatchBytes == 0 && plan.RetailWadPatchBytes == 0 &&
            plan.AffectedRawSectorLbas.SequenceEqual(new[] { 54_838 }) &&
            receipt.RebuiltRawSectorCount == 1 && receipt.ChangedRawSectorCount == 1 &&
            receipt.RawSectorDiffs.Count == 1 && receipt.RawSectorDiffs[0].RawSectorLba == 54_838 &&
            receipt.RawSectorDiffs[0].PayloadChangedBytes == 14 &&
            receipt.OutputImageSha256 ==
                UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedOutputImageSha256 &&
            receipt.ChangedPhysicalImageBytes ==
                UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedChangedPhysicalImageBytes &&
            receipt.RawSectorDiffSha256 ==
                UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedRawSectorDiffSha256 &&
            receipt.LabLinkMetadataSha256 ==
                UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedLabLinkMetadataSha256 &&
            (UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedLocationGuideSha256 == "PENDING" ||
             receipt.LocationGuideSha256 ==
                UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.ExpectedLocationGuideSha256),
        "The exact one-sector logical/physical diff boundary changed.");
    Require(plan.ExactT96T97AtomicRelocationVerified && plan.ExactLabLinkMetadataVerified &&
            plan.NativeSupportVerified && plan.LandingAndPlayerAnchorPreserved && plan.TerrainPreserved &&
            plan.ExecutablePreserved && plan.RetailLevelsPreserved && !plan.MemoryCardsRequired &&
            !plan.SavingAuthorized && !plan.RuntimeVerified && plan.DisposableRuntimeCandidateAuthorized &&
            !plan.AppIntegrated && !plan.NormalCreateBinEnabled && !plan.PromotionAuthorized && !plan.ReleaseAuthorized,
        "The fail-closed direct plan flags changed.");
    Require(receipt.RollbackRecoveryVerified == expectRecovery &&
            receipt.FullDirectoryRollbackVerified == expectRecovery &&
            receipt.ExactLogicalDiffBoundaryVerified && receipt.ExactPhysicalSectorBoundaryVerified &&
            receipt.CanonicalLabMetadataVerified && receipt.CanonicalReceiptVerified &&
            receipt.Mode2IntegrityVerified && receipt.BaseCandidatePreserved &&
            receipt.FullDirectoryPublicationVerified && receipt.FinderHandoffVerified &&
            !receipt.RuntimeVerified && receipt.DisposableRuntimeCandidateAuthorized &&
            !receipt.AppIntegrated && !receipt.NormalCreateBinEnabled &&
            !receipt.PromotionAuthorized && !receipt.ReleaseAuthorized,
        "The fail-closed receipt flags changed.");
    Require(plan.LoadCodes.Count == 5 &&
            plan.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 60, 13, 64, 15 }),
        "The exact ID65/destination/retail comparison codes changed.");
}

static async Task VerifyExactSevenFilesAsync(
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths)
{
    string[] expected =
    {
        Path.GetFileName(paths.OutputImagePath),
        Path.GetFileName(paths.OutputCuePath),
        Path.GetFileName(paths.LabLinkMetadataPath),
        Path.GetFileName(paths.StaticReadbackReceiptPath),
        Path.GetFileName(paths.RuntimeChecklistPath),
        Path.GetFileName(paths.LocationGuidePath),
        Path.GetFileName(paths.FinderHelperPath)
    };
    string[] actual = Directory.EnumerateFileSystemEntries(paths.OutputDirectoryPath)
        .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
    Require(actual.SequenceEqual(expected.Order(StringComparer.Ordinal)),
        "The published Return Home candidate is not exactly seven files.");
    string metadata = await File.ReadAllTextAsync(paths.LabLinkMetadataPath);
    using JsonDocument document = JsonDocument.Parse(metadata);
    JsonElement group = document.RootElement.GetProperty("linkGroups")[0];
    Require(group.GetProperty("trueIndexes").EnumerateArray().Select(value => value.GetInt32())
                .SequenceEqual(new[] { 97, 96 }) &&
            group.GetProperty("memberIds").EnumerateArray().Select(value => value.GetString())
                .SequenceEqual(new[] { "T97", "T96" }) &&
            group.GetProperty("linkedMove").GetBoolean(),
        "The published candidate-local linked group changed.");
    XDocument svg = XDocument.Load(paths.LocationGuidePath);
    Require(svg.Root?.Attribute("width")?.Value == "1200" &&
            svg.Root?.Attribute("height")?.Value == "1000" &&
            svg.Root?.Attribute("data-safe-left")?.Value == "60" &&
            svg.Root?.Attribute("data-safe-right")?.Value == "1140",
        "The SVG is not the exact 1200x1000 safe-area guide.");
}

static async Task ExpectVerificationFailureAsync(
    string path,
    Func<byte[], byte[]> mutate,
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
    string label)
{
    byte[] original = await File.ReadAllBytesAsync(path);
    UnixFileMode? mode = GetUnixMode(path);
    bool rejected = false;
    try
    {
        await File.WriteAllBytesAsync(path, mutate(original));
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    finally
    {
        await File.WriteAllBytesAsync(path, original);
        RestoreUnixMode(path, mode);
    }
    Require(rejected, $"The {label} was accepted.");
    Require((await File.ReadAllBytesAsync(path)).SequenceEqual(original),
        $"The {label} fixture did not restore its exact bytes.");
}

static async Task ExpectReceiptMutationFailureAsync(
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidatePaths paths,
    Action<JsonObject> mutate,
    string label)
{
    byte[] original = await File.ReadAllBytesAsync(paths.StaticReadbackReceiptPath);
    string text = Encoding.UTF8.GetString(original);
    JsonObject receipt = JsonNode.Parse(text)?.AsObject()
        ?? throw new InvalidOperationException("The smoke receipt did not parse as an object.");
    mutate(receipt);
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    byte[] changed = new UTF8Encoding(false).GetBytes(receipt.ToJsonString(options) + "\n");
    Require(!changed.SequenceEqual(original), $"The {label} fixture did not change the receipt.");
    bool rejected = false;
    try
    {
        await File.WriteAllBytesAsync(paths.StaticReadbackReceiptPath, changed);
        _ = await UnusedLevel65ReturnHomeNativeApronRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    finally
    {
        await File.WriteAllBytesAsync(paths.StaticReadbackReceiptPath, original);
    }
    Require(rejected, $"The {label} was accepted.");
    Require((await File.ReadAllBytesAsync(paths.StaticReadbackReceiptPath)).SequenceEqual(original),
        $"The {label} fixture did not restore the exact canonical receipt.");
}

static async Task VerifyCanonicalReceiptAsync(
    UnusedLevel65ReturnHomeNativeApronRuntimeCandidateResult result)
{
    string text = await File.ReadAllTextAsync(result.Paths.StaticReadbackReceiptPath);
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    UnusedLevel65ReturnHomeNativeApronReceipt receipt =
        JsonSerializer.Deserialize<UnusedLevel65ReturnHomeNativeApronReceipt>(text, options)
        ?? throw new InvalidOperationException("The canonical receipt is empty.");
    Require(text == JsonSerializer.Serialize(receipt, options) + "\n",
        "The receipt failed exact canonical reserialization.");
    Require(await HashFileAsync(result.Paths.StaticReadbackReceiptPath) ==
            result.ArtifactHashes.ReceiptSha256,
        "The canonical receipt hash does not match the verified result pin.");
    Require(!receipt.RollbackRecoveryVerified && !receipt.FullDirectoryRollbackVerified,
        "A clean publication receipt must pin recovery flags false/false.");
}

static void RequireNoPublisherDebris(string publicationParent)
{
    if (!Directory.Exists(publicationParent))
        return;
    const string leaseFileName = ".unused-level-65-return-home-native-apron-writer.lease";
    string[] ownedHiddenEntries = Directory.EnumerateFileSystemEntries(publicationParent)
        .Where(path => Path.GetFileName(path).StartsWith(
            ".unused-level-65-return-home-native-apron-", StringComparison.Ordinal))
        .ToArray();
    Require(ownedHiddenEntries.All(path => Path.GetFileName(path) == leaseFileName),
        "Return Home operation debris remained after a completed path.");
    string leasePath = Path.Combine(publicationParent, leaseFileName);
    if (!File.Exists(leasePath))
        return;
    FileAttributes attributes = File.GetAttributes(leasePath);
    Require((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0,
        "The persistent Return Home writer lease is not a regular file.");
}

static async Task<DirectorySnapshot> SnapshotDirectoryAsync(string path)
{
    List<FileSnapshot> files = [];
    foreach (string file in Directory.EnumerateFiles(path).Order(StringComparer.Ordinal))
    {
        files.Add(new(
            Path.GetFileName(file),
            new FileInfo(file).Length,
            await HashFileAsync(file),
            GetUnixMode(file)));
    }
    return new(files);
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
    byte[] hash = await SHA256.HashDataAsync(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static UnixFileMode? GetUnixMode(string path)
{
    if (OperatingSystem.IsWindows())
        return null;
    return File.GetUnixFileMode(path);
}

static void RestoreUnixMode(string path, UnixFileMode? mode)
{
    if (!mode.HasValue || OperatingSystem.IsWindows())
        return;
    File.SetUnixFileMode(path, mode.Value);
}

static string FindRoot(string? requested)
{
    string current = Path.GetFullPath(requested ?? Directory.GetCurrentDirectory());
    while (true)
    {
        if (File.Exists(Path.Combine(current, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current, "src", "Spyro.Editor.Core")))
        {
            return current;
        }
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor workspace root.");
        current = parent.FullName;
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record FileSnapshot(string Name, long Length, string Sha256, UnixFileMode? Mode);
sealed class DirectorySnapshot(IReadOnlyList<FileSnapshot> files) : IEquatable<DirectorySnapshot>
{
    public IReadOnlyList<FileSnapshot> Files { get; } = files;

    public bool Equals(DirectorySnapshot? other) =>
        other != null && Files.SequenceEqual(other.Files);

    public override bool Equals(object? obj) => Equals(obj as DirectorySnapshot);

    public override int GetHashCode() =>
        Files.Aggregate(17, (hash, file) => HashCode.Combine(hash, file));

    public static bool operator ==(DirectorySnapshot? left, DirectorySnapshot? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(DirectorySnapshot? left, DirectorySnapshot? right) =>
        !(left == right);
}

sealed class SmokeFaultException(string message) : Exception(message);
