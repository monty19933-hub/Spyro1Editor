using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string root = FindRoot(args.ElementAtOrDefault(0));
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
string remoteProof = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-remote-blank-isolation",
    "Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE.bin");

Require(OperatingSystem.IsMacOS(), "The exact seven-file Finder handoff smoke requires macOS.");
Require(File.Exists(baseImage), "The exact locked display-name BIN is missing.");
Require(File.Exists(baseCue), "The exact locked display-name CUE is missing.");
Require(File.Exists(remoteProof), "The exact remote-blank proof BIN is missing.");
FileStamp baseImageBefore = Stamp(baseImage);
FileStamp baseCueBefore = Stamp(baseCue);
FileStamp remoteProofBefore = Stamp(remoteProof);

string physicalTemporaryRoot = Path.GetTempPath();
if (OperatingSystem.IsMacOS() && physicalTemporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
    physicalTemporaryRoot = "/private" + physicalTemporaryRoot;
string smokeRoot = Path.Combine(
    physicalTemporaryRoot,
    $"spyro-id65-music-runtime-candidate-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(smokeRoot);
try
{
    string outputDirectory = Path.Combine(
        smokeRoot,
        UnusedLevel65MusicRuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65MusicRuntimeCandidatePaths paths =
        UnusedLevel65MusicRuntimeCandidateExporter.CreatePaths(outputDirectory);
    UnusedLevel65MusicRuntimeCandidateRequest request = new(
        root,
        baseImage,
        baseCue,
        remoteProof,
        outputDirectory);

    bool wrongDirectoryRejected = false;
    try
    {
        _ = UnusedLevel65MusicRuntimeCandidateExporter.CreatePaths(
            Path.Combine(smokeRoot, "not-the-owned-music-directory"));
    }
    catch (InvalidOperationException)
    {
        wrongDirectoryRejected = true;
    }
    Require(wrongDirectoryRejected, "The writer accepted a non-owned output directory name.");

    string missingProof = Path.Combine(smokeRoot, "missing-proof.bin");
    bool missingProofRejected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with { RemoteBlankProofImagePath = missingProof });
    }
    catch (FileNotFoundException)
    {
        missingProofRejected = true;
    }
    Require(missingProofRejected && !Directory.Exists(outputDirectory),
        "A missing proof BIN did not fail closed before publication.");

    using ManualResetEventSlim writerHoldingLease = new(false);
    using ManualResetEventSlim releaseWriter = new(false);
    Task<UnusedLevel65MusicRuntimeCandidateResult> firstWriter = Task.Run(async () =>
        await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with
            {
                TestStageHook = stage =>
                {
                    if (stage != "after-global-writer-lease-acquired")
                        return;
                    writerHoldingLease.Set();
                    if (!releaseWriter.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("The contention smoke did not release the first writer.");
                }
            }));
    Require(writerHoldingLease.Wait(TimeSpan.FromSeconds(10)),
        "The first writer did not expose its global lease checkpoint.");
    bool contentionRejected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(request);
    }
    catch (IOException ex) when (ex.Message.Contains("Another ID65 music runtime candidate writer", StringComparison.Ordinal))
    {
        contentionRejected = true;
    }
    finally
    {
        releaseWriter.Set();
    }
    Require(contentionRejected, "A concurrent music publisher was not rejected by the global lease.");
    UnusedLevel65MusicRuntimeCandidateResult first = await firstWriter;
    VerifyResult(first, paths, expectRecovery: false);
    VerifySevenFiles(paths);
    VerifyGuideAndChecklist(paths);
    UnusedLevel65MusicRuntimeCandidateResult verifiedFirst =
        await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    VerifyResult(verifiedFirst, paths, expectRecovery: false);
    DirectorySnapshot firstSnapshot = SnapshotDirectory(outputDirectory);

    bool existingRejected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(request);
    }
    catch (IOException ex) when (ex.Message.Contains("already exists", StringComparison.Ordinal))
    {
        existingRejected = true;
    }
    Require(existingRejected && SnapshotDirectory(outputDirectory) == firstSnapshot,
        "An accidental non-replacement call changed the published candidate.");

    UnusedLevel65MusicRuntimeCandidateResult deterministicReplacement =
        await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(deterministicReplacement, paths, expectRecovery: false);
    Require(SnapshotDirectory(outputDirectory) == firstSnapshot,
        "Two clean publications at the same paths were not byte-deterministic.");

    DirectorySnapshot beforeRollback = SnapshotDirectory(outputDirectory);
    bool rollbackInjected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage == "after-candidate-publication")
                        throw new InjectedFailureException("after-candidate-publication");
                }
            });
    }
    catch (InjectedFailureException ex) when (ex.Message == "after-candidate-publication")
    {
        rollbackInjected = true;
    }
    Require(rollbackInjected, "The rollback fault was not injected.");
    Require(Directory.Exists(outputDirectory) && SnapshotDirectory(outputDirectory) == beforeRollback,
        "A publication failure did not restore the complete previous seven-file directory.");
    Require(!Directory.Exists(paths.OperationsDirectoryPath),
        "A completed rollback left operation debris.");

    bool staleRecoveryInjected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with
            {
                ReplaceExistingCandidate = true,
                TestStageHook = stage =>
                {
                    if (stage is "after-candidate-publication" or "before-previous-candidate-restore")
                        throw new InjectedFailureException(stage);
                }
            });
    }
    catch (IOException ex) when (ex.Message.Contains("recovery is incomplete", StringComparison.Ordinal))
    {
        staleRecoveryInjected = true;
    }
    Require(staleRecoveryInjected, "The stale recovery fixture was not created.");
    Require(!Directory.Exists(outputDirectory) && Directory.Exists(paths.OperationsDirectoryPath),
        "The stale recovery fixture did not preserve its authoritative backup/journal boundary.");

    UnusedLevel65MusicRuntimeCandidateResult recovered =
        await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(recovered, paths, expectRecovery: true);
    Require(!Directory.Exists(paths.OperationsDirectoryPath),
        "Successful recovery/publication left operation debris.");
    await RequireVerificationRejectedAsync(
        paths,
        "a recovery-flagged receipt was accepted as the canonical clean publication");

    UnusedLevel65MusicRuntimeCandidateResult cleanAfterRecovery =
        await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(cleanAfterRecovery, paths, expectRecovery: false);
    _ = await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);

    await ExerciseReceiptTamperMatrixAsync(paths);
    await ExerciseSidecarTamperMatrixAsync(paths);
    UnusedLevel65MusicRuntimeCandidateResult verifiedClean =
        await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    VerifyResult(verifiedClean, paths, expectRecovery: false);

    Require(Stamp(baseImage) == baseImageBefore, "The locked display-name BIN changed.");
    Require(Stamp(baseCue) == baseCueBefore, "The locked display-name CUE changed.");
    Require(Stamp(remoteProof) == remoteProofBefore, "The remote-blank proof BIN changed.");

}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

string frozenOutputDirectory = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    UnusedLevel65MusicRuntimeCandidateExporter.OutputDirectoryName);
UnusedLevel65MusicRuntimeCandidatePaths frozenPaths =
    UnusedLevel65MusicRuntimeCandidateExporter.CreatePaths(frozenOutputDirectory);
UnusedLevel65MusicRuntimeCandidateResult frozen =
    await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
        new(
            root,
            baseImage,
            baseCue,
            remoteProof,
            frozenOutputDirectory,
            ReplaceExistingCandidate: Directory.Exists(frozenOutputDirectory)));
VerifyResult(frozen, frozenPaths, expectRecovery: false);
VerifySevenFiles(frozenPaths);
VerifyGuideAndChecklist(frozenPaths);
UnusedLevel65MusicRuntimeCandidateResult verifiedFrozen =
    await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(frozenPaths);
VerifyResult(verifiedFrozen, frozenPaths, expectRecovery: false);
Require(!Directory.Exists(frozenPaths.OperationsDirectoryPath),
    "The frozen clean publication left operation debris.");
string frozenReceiptSha256 = HashFile(frozenPaths.StaticReadbackReceiptPath);
Require(
    frozenReceiptSha256 == UnusedLevel65MusicRuntimeCandidateExporter.ExpectedFrozenReceiptSha256,
    "The frozen canonical clean receipt hash changed.");
await ExerciseAncestorAliasNoWriteFixtureAsync(
    physicalTemporaryRoot,
    root,
    baseImage,
    baseCue,
    remoteProof,
    frozenPaths);

Console.WriteLine("PASS: isolated failure/tamper matrix completed, then the clean seven-file ID65 music candidate was frozen.");
Console.WriteLine($"Frozen directory: {frozenPaths.OutputDirectoryPath}");
Console.WriteLine($"Output BIN SHA-256: {frozen.OutputImageSha256}");
Console.WriteLine($"Output SCUS SHA-256: {frozen.OutputExecutableSha256}");
Console.WriteLine($"Preserved ID65 DATA SHA-256: {frozen.OutputId65DataSha256}");
Console.WriteLine($"Preserved WAD.WAD SHA-256: {frozen.OutputWadSha256}");
Console.WriteLine($"Raw-sector diff SHA-256: {frozen.RawSectorDiffSha256}");
Console.WriteLine($"Canonical clean receipt SHA-256: {frozenReceiptSha256}");
foreach (UnusedLevel65MusicRuntimeCandidateRawSectorDiff diff in frozen.RawSectorDiffs)
{
    Console.WriteLine(
        $"LBA {diff.RawSectorLba}: payload={diff.PayloadChangedBytes}, edc={diff.EdcChangedBytes}, " +
        $"eccP={diff.EccPChangedBytes}, eccQ={diff.EccQChangedBytes}, total={diff.TotalChangedBytes}");
}
Console.WriteLine("Receipt matrix: coordinated recovery flags, unknown property, whitespace, property order, and one-byte changes rejected.");
Console.WriteLine("Every receipt-bound sidecar tamper was rejected and restored before the frozen publication.");
Console.WriteLine("Ancestor-alias replacement and verification were rejected with output, base, and receipt untouched.");
Console.WriteLine("Runtime remains pending/unpromoted; App, normal Create BIN, and release integration remain false.");

static async Task ExerciseAncestorAliasNoWriteFixtureAsync(
    string physicalTemporaryRoot,
    string workspaceRoot,
    string baseImage,
    string baseCue,
    string remoteProof,
    UnusedLevel65MusicRuntimeCandidatePaths frozenPaths)
{
    DirectorySnapshot outputBefore = SnapshotDirectory(frozenPaths.OutputDirectoryPath);
    FileStamp baseBefore = Stamp(baseImage);
    FileStamp receiptBefore = Stamp(frozenPaths.StaticReadbackReceiptPath);
    string aliasFixtureRoot = Path.Combine(
        physicalTemporaryRoot,
        $"spyro-id65-music-runtime-candidate-alias-{Guid.NewGuid():N}");
    Directory.CreateDirectory(aliasFixtureRoot);
    string aliasParent = Path.Combine(aliasFixtureRoot, "alias-parent");
    string frozenParent = Path.GetDirectoryName(frozenPaths.OutputDirectoryPath) ??
        throw new InvalidOperationException("The frozen music output has no parent.");
    Directory.CreateSymbolicLink(aliasParent, frozenParent);
    try
    {
        string aliasedOutput = Path.Combine(
            aliasParent,
            UnusedLevel65MusicRuntimeCandidateExporter.OutputDirectoryName);
        UnusedLevel65MusicRuntimeCandidatePaths aliasedPaths =
            UnusedLevel65MusicRuntimeCandidateExporter.CreatePaths(aliasedOutput);
        bool createRejected = false;
        try
        {
            _ = await UnusedLevel65MusicRuntimeCandidateExporter.CreateAsync(
                new(
                    workspaceRoot,
                    baseImage,
                    baseCue,
                    remoteProof,
                    aliasedOutput,
                    ReplaceExistingCandidate: true));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("symbolic link or reparse point", StringComparison.Ordinal))
        {
            createRejected = true;
        }

        bool verificationRejected = false;
        try
        {
            _ = await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(aliasedPaths);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("symbolic link or reparse point", StringComparison.Ordinal))
        {
            verificationRejected = true;
        }

        Require(createRejected, "A symlinked output ancestor reached music replacement publication.");
        Require(verificationRejected, "A symlinked output ancestor bypassed frozen receipt verification.");
        Require(
            SnapshotDirectory(frozenPaths.OutputDirectoryPath) == outputBefore &&
            Stamp(baseImage) == baseBefore &&
            Stamp(frozenPaths.StaticReadbackReceiptPath) == receiptBefore,
            "The rejected ancestor-alias fixture changed the output, base BIN, or receipt.");
        Require(
            !Directory.Exists(frozenPaths.OperationsDirectoryPath),
            "The rejected ancestor-alias fixture created recovery-operation debris.");
    }
    finally
    {
        if (Directory.Exists(aliasParent) || File.Exists(aliasParent))
            File.Delete(aliasParent);
        if (Directory.Exists(aliasFixtureRoot))
            Directory.Delete(aliasFixtureRoot);
    }
}

static async Task ExerciseReceiptTamperMatrixAsync(
    UnusedLevel65MusicRuntimeCandidatePaths paths)
{
    string receiptPath = paths.StaticReadbackReceiptPath;
    byte[] originalBytes = await File.ReadAllBytesAsync(receiptPath);
    string canonical = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
        .GetString(originalBytes);
    UnixFileMode originalMode = File.GetUnixFileMode(receiptPath);

    await RequireReceiptMutationRejectedAsync(
        paths,
        originalBytes,
        originalMode,
        canonical
            .Replace("\"rollbackRecoveryVerified\": false", "\"rollbackRecoveryVerified\": true", StringComparison.Ordinal)
            .Replace("\"fullDirectoryRollbackVerified\": false", "\"fullDirectoryRollbackVerified\": true", StringComparison.Ordinal),
        "coordinated false-to-true recovery flags");

    int closingBrace = canonical.LastIndexOf("\n}", StringComparison.Ordinal);
    Require(closingBrace > 0, "The canonical receipt has no final object brace.");
    string unknownProperty = canonical.Insert(
        closingBrace,
        ",\n  \"unknownAuditProperty\": true");
    await RequireReceiptMutationRejectedAsync(
        paths,
        originalBytes,
        originalMode,
        unknownProperty,
        "unknown receipt property");

    await RequireReceiptMutationRejectedAsync(
        paths,
        originalBytes,
        originalMode,
        " " + canonical,
        "leading-whitespace receipt drift");

    string[] reorderedLines = canonical.Split('\n');
    Require(reorderedLines.Length > 3 &&
            reorderedLines[1].Contains("\"schemaVersion\"", StringComparison.Ordinal) &&
            reorderedLines[2].Contains("\"profileId\"", StringComparison.Ordinal),
        "The receipt property-order fixture changed.");
    (reorderedLines[1], reorderedLines[2]) = (reorderedLines[2], reorderedLines[1]);
    await RequireReceiptMutationRejectedAsync(
        paths,
        originalBytes,
        originalMode,
        string.Join('\n', reorderedLines),
        "receipt property-order drift");

    string changedHash = "e" + UnusedLevel65MusicRuntimeCandidateExporter.ExpectedOutputImageSha256[1..];
    string oneByteChange = canonical.Replace(
        UnusedLevel65MusicRuntimeCandidateExporter.ExpectedOutputImageSha256,
        changedHash,
        StringComparison.Ordinal);
    Require(oneByteChange != canonical, "The single-byte receipt mutation fixture did not change the receipt.");
    await RequireReceiptMutationRejectedAsync(
        paths,
        originalBytes,
        originalMode,
        oneByteChange,
        "single-byte receipt hash change");
}

static async Task RequireReceiptMutationRejectedAsync(
    UnusedLevel65MusicRuntimeCandidatePaths paths,
    byte[] originalBytes,
    UnixFileMode originalMode,
    string mutatedText,
    string label)
{
    string receiptPath = paths.StaticReadbackReceiptPath;
    try
    {
        await File.WriteAllTextAsync(
            receiptPath,
            mutatedText,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(receiptPath, originalMode);
        await RequireVerificationRejectedAsync(paths, $"{label} was accepted");
    }
    finally
    {
        await File.WriteAllBytesAsync(receiptPath, originalBytes);
        File.SetUnixFileMode(receiptPath, originalMode);
    }
    _ = await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);
}

static async Task ExerciseSidecarTamperMatrixAsync(
    UnusedLevel65MusicRuntimeCandidatePaths paths)
{
    string[] sidecars =
    [
        paths.OutputCuePath,
        paths.ConstructionPlanPath,
        paths.RuntimeChecklistPath,
        paths.LocationGuidePath,
        paths.FinderHelperPath
    ];
    foreach (string sidecar in sidecars)
    {
        byte[] originalBytes = await File.ReadAllBytesAsync(sidecar);
        UnixFileMode originalMode = File.GetUnixFileMode(sidecar);
        try
        {
            await using (FileStream stream = new(
                             sidecar,
                             FileMode.Append,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 1,
                             FileOptions.Asynchronous))
            {
                await stream.WriteAsync(new byte[] { (byte)' ' });
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }
            File.SetUnixFileMode(sidecar, originalMode);
            await RequireVerificationRejectedAsync(
                paths,
                $"receipt-bound sidecar tamper was accepted for {Path.GetFileName(sidecar)}");
        }
        finally
        {
            await File.WriteAllBytesAsync(sidecar, originalBytes);
            File.SetUnixFileMode(sidecar, originalMode);
        }
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
}

static async Task RequireVerificationRejectedAsync(
    UnusedLevel65MusicRuntimeCandidatePaths paths,
    string failureMessage)
{
    bool rejected = false;
    try
    {
        _ = await UnusedLevel65MusicRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Require(rejected, failureMessage);
}

static void VerifyResult(
    UnusedLevel65MusicRuntimeCandidateResult result,
    UnusedLevel65MusicRuntimeCandidatePaths paths,
    bool expectRecovery)
{
    Require(result.Paths == paths, "Result paths changed.");
    Require(
        result.Plan.ProfileId == UnusedLevel65MusicRuntimeCandidateExporter.ProfileId &&
        result.Plan.ClosureProfileId == UnusedLevel65MusicOwnershipClosure.ProfileId &&
        result.Plan.BaseImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
        result.Plan.BaseCueSha256 == "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2" &&
        result.Plan.RemoteBlankProofImageSha256 == "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e" &&
        result.Plan.SourceExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
        result.Plan.OutputExecutableSha256 == "30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c" &&
        result.Plan.SourceId65DataSha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
        result.Plan.OutputId65DataSha256 == result.Plan.SourceId65DataSha256 &&
        result.Plan.SourceWadSha256 == result.Plan.OutputWadSha256 &&
        result.Plan.ExecutableLba == 55_382 && result.Plan.ExecutableByteLength == 0x66000 &&
        result.Plan.ContinuousLevelIndex == 35 && result.Plan.SelectedTrackId == 26 &&
        result.Plan.SelectedTrackName == "Town Square" &&
        result.Plan.ClosurePatches.Count == 5 && result.Plan.LogicalPatchWindowBytes == 52 &&
        result.Plan.ChangedLogicalExecutableBytes == 40 &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(new[] { 55_438, 55_573 }) &&
        result.Plan.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }),
        "Frozen music plan identity changed.");
    Require(
        result.ExactClosureConsumed && result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified && result.FullImageReadbackVerified &&
        result.FullScusReadbackVerified && result.Id65DataReadbackVerified &&
        result.FullWadReadbackVerified && result.RetailParityStaticallyVerified &&
        result.Id65Slot35StaticallyVerified && result.PeteXaDirectoryAndAudioPreserved &&
        result.RawSectorIntegrityVerified && result.BaseCandidatePreserved &&
        result.CanonicalReceiptVerified && result.SevenFilePublicationVerified &&
        result.RollbackRecoveryVerified == expectRecovery &&
        result.FullDirectoryRollbackVerified == expectRecovery &&
        result.FinderHandoffVerified && result.DisposableRuntimeCandidateAuthorized &&
        !result.RuntimeProofComplete && !result.AppEnabled &&
        !result.NormalCreateBinEnabled && !result.PromotionAuthorized,
        "Result proof or fail-closed flags changed.");
    Require(
        result.OutputExecutableSha256 == "30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c" &&
        result.OutputId65DataSha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
        result.OutputWadSha256 == "df09cdcfabd89eaad92deaab87ae467840330851906afa5924f19f36ced47bf8" &&
        result.OutputImageSha256 == "facc61da6c1438d4abfd0ed43afd2e1805aafe07233ab60fc25eb0e38394347d" &&
        result.ChangedLogicalExecutableBytes == 40 && result.RebuiltRawSectorCount == 2 &&
        result.ChangedPhysicalImageBytes == 260 && result.ChangedRawSectorCount == 2 &&
        result.RawSectorDiffSha256 == "ea615011803acb8ae534a361eae56c2646e7c4b967fc0e7d73055f6c8d264a97" &&
        result.RawSectorDiffs.SequenceEqual(new[]
        {
            new UnusedLevel65MusicRuntimeCandidateRawSectorDiff(55_438, 0, 0, 39, 4, 0, 78, 102, 223),
            new UnusedLevel65MusicRuntimeCandidateRawSectorDiff(55_573, 0, 0, 1, 4, 0, 10, 22, 37)
        }) &&
        result.Receipt.OutputImageSha256 == result.OutputImageSha256 &&
        result.Receipt.RawSectorDiffSha256 == result.RawSectorDiffSha256,
        "Exact image/readback pins changed.");
    Require(result.OutputImageSha256 == UnusedLevel65MusicRuntimeCandidateExporter.ExpectedOutputImageSha256,
        "Pinned music output BIN hash changed.");
    Require(result.RawSectorDiffSha256 == UnusedLevel65MusicRuntimeCandidateExporter.ExpectedRawSectorDiffSha256,
        "Pinned music raw-sector diff changed.");
}

static void VerifySevenFiles(UnusedLevel65MusicRuntimeCandidatePaths paths)
{
    string[] expected =
    [
        paths.OutputImagePath,
        paths.OutputCuePath,
        paths.ConstructionPlanPath,
        paths.StaticReadbackReceiptPath,
        paths.RuntimeChecklistPath,
        paths.LocationGuidePath,
        paths.FinderHelperPath
    ];
    Require(expected.All(File.Exists), "One of the exact seven published files is missing.");
    Require(Directory.GetFiles(paths.OutputDirectoryPath).Length == 7 &&
            !Directory.EnumerateDirectories(paths.OutputDirectoryPath).Any(),
        "The output directory is not exactly seven top-level files.");
}

static void VerifyGuideAndChecklist(UnusedLevel65MusicRuntimeCandidatePaths paths)
{
    string guide = File.ReadAllText(paths.LocationGuidePath);
    string checklist = File.ReadAllText(paths.RuntimeChecklistPath);
    Require(
        guide.Contains("width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\"", StringComparison.Ordinal) &&
        guide.Contains("Memory Card 1: None", StringComparison.Ordinal) &&
        guide.Contains("Memory Card 2: None", StringComparison.Ordinal) &&
        guide.Contains("NO APP / CREATE BIN / RELEASE INTEGRATION", StringComparison.Ordinal),
        "The exact 1200x1000 no-card guide changed.");
    Require(
        checklist.Contains("Memory Card 1: None", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 2: None", StringComparison.Ordinal) &&
        checklist.Contains("Repeat the 12-minute cold run three times", StringComparison.Ordinal) &&
        checklist.Contains("Runtime proof is incomplete", StringComparison.Ordinal),
        "The runtime checklist lost its isolation or promotion boundary.");
}

static DirectorySnapshot SnapshotDirectory(string directory)
{
    string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        .Select(path => $"{Path.GetRelativePath(directory, path)}|{new FileInfo(path).Length}|{HashFile(path)}|{Mode(path)}")
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(directory, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    return new(string.Join('\n', files), string.Join('\n', directories));
}

static string Mode(string path) =>
    OperatingSystem.IsWindows() ? "windows" : Convert.ToString((int)File.GetUnixFileMode(path), 8);

static FileStamp Stamp(string path)
{
    FileInfo file = new(path);
    return new(file.Length, file.LastWriteTimeUtc, HashFile(path));
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string FindRoot(string? requested)
{
    string current = Path.GetFullPath(requested ?? Directory.GetCurrentDirectory());
    while (!File.Exists(Path.Combine(current, "spyro-level-catalog.json")))
    {
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not find the Spyro Editor repository root.");
        current = parent.FullName;
    }
    return current;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed class InjectedFailureException(string message) : Exception(message);
internal readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc, string Sha256);
internal readonly record struct DirectorySnapshot(string Files, string Directories);
