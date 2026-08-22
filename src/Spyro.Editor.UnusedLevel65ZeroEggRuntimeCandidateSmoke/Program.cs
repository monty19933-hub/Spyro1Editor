using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string root = FindRoot(args.ElementAtOrDefault(0));
VerifyExporterSourceTerminology(root);
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
Require(OperatingSystem.IsMacOS(), "The exact seven-file Finder handoff smoke requires macOS.");
Require(File.Exists(baseImage), "The exact locked display-name BIN is missing.");
Require(File.Exists(baseCue), "The exact locked display-name CUE is missing.");
FileStamp baseImageBefore = Stamp(baseImage);
FileStamp baseCueBefore = Stamp(baseCue);

string physicalTemporaryRoot = Path.GetTempPath();
if (OperatingSystem.IsMacOS() && physicalTemporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
    physicalTemporaryRoot = "/private" + physicalTemporaryRoot;
string smokeRoot = Path.Combine(
    physicalTemporaryRoot,
    $"spyro-id65-zero-egg-runtime-candidate-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(smokeRoot);
try
{
    string outputDirectory = Path.Combine(
        smokeRoot,
        UnusedLevel65ZeroEggRuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths =
        UnusedLevel65ZeroEggRuntimeCandidateExporter.CreatePaths(outputDirectory);
    UnusedLevel65ZeroEggRuntimeCandidateRequest request = new(
        root,
        baseImage,
        baseCue,
        outputDirectory);

    bool wrongDirectoryRejected = false;
    try
    {
        _ = UnusedLevel65ZeroEggRuntimeCandidateExporter.CreatePaths(
            Path.Combine(smokeRoot, "not-the-owned-zero-egg-directory"));
    }
    catch (InvalidOperationException)
    {
        wrongDirectoryRejected = true;
    }
    Require(wrongDirectoryRejected, "The writer accepted a non-owned output directory name.");

    string missingBase = Path.Combine(smokeRoot, "missing-base.bin");
    bool missingBaseRejected = false;
    try
    {
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
            request with { BaseImagePath = missingBase });
    }
    catch (FileNotFoundException)
    {
        missingBaseRejected = true;
    }
    Require(missingBaseRejected && !Directory.Exists(outputDirectory),
        "A missing base BIN did not fail closed before publication.");

    using ManualResetEventSlim writerHoldingLease = new(false);
    using ManualResetEventSlim releaseWriter = new(false);
    Task<UnusedLevel65ZeroEggRuntimeCandidateResult> firstWriter = Task.Run(async () =>
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
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
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(request);
    }
    catch (IOException ex) when (ex.Message.Contains("Another ID65 zero-egg runtime candidate writer", StringComparison.Ordinal))
    {
        contentionRejected = true;
    }
    finally
    {
        releaseWriter.Set();
    }
    Require(contentionRejected, "A concurrent zero-egg publisher was not rejected by the global lease.");
    UnusedLevel65ZeroEggRuntimeCandidateResult first = await firstWriter;
    VerifyResult(first, paths, expectRecovery: false);
    VerifySevenFiles(paths);
    VerifyGuideAndChecklist(paths);
    UnusedLevel65ZeroEggRuntimeCandidateResult verifiedFirst =
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    VerifyResult(verifiedFirst, paths, expectRecovery: false);
    DirectorySnapshot firstSnapshot = SnapshotDirectory(outputDirectory);

    bool existingRejected = false;
    try
    {
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(request);
    }
    catch (IOException ex) when (ex.Message.Contains("already exists", StringComparison.Ordinal))
    {
        existingRejected = true;
    }
    Require(existingRejected && SnapshotDirectory(outputDirectory) == firstSnapshot,
        "An accidental non-replacement call changed the published candidate.");

    UnusedLevel65ZeroEggRuntimeCandidateResult deterministicReplacement =
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(deterministicReplacement, paths, expectRecovery: false);
    Require(SnapshotDirectory(outputDirectory) == firstSnapshot,
        "Two clean publications at the same paths were not byte-deterministic.");

    DirectorySnapshot beforeRollback = SnapshotDirectory(outputDirectory);
    bool rollbackInjected = false;
    try
    {
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
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
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
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

    UnusedLevel65ZeroEggRuntimeCandidateResult recovered =
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(recovered, paths, expectRecovery: true);
    Require(!Directory.Exists(paths.OperationsDirectoryPath),
        "Successful recovery/publication left operation debris.");
    await RequireVerificationRejectedAsync(
        paths,
        "a recovery-flagged receipt was accepted as the canonical clean publication");

    UnusedLevel65ZeroEggRuntimeCandidateResult cleanAfterRecovery =
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
            request with { ReplaceExistingCandidate = true });
    VerifyResult(cleanAfterRecovery, paths, expectRecovery: false);
    _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);

    await ExerciseReceiptTamperMatrixAsync(paths);
    await ExerciseSidecarTamperMatrixAsync(paths);
    UnusedLevel65ZeroEggRuntimeCandidateResult verifiedClean =
        await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    VerifyResult(verifiedClean, paths, expectRecovery: false);

    Require(Stamp(baseImage) == baseImageBefore, "The locked display-name BIN changed.");
    Require(Stamp(baseCue) == baseCueBefore, "The locked display-name CUE changed.");

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
    UnusedLevel65ZeroEggRuntimeCandidateExporter.OutputDirectoryName);
UnusedLevel65ZeroEggRuntimeCandidatePaths frozenPaths =
    UnusedLevel65ZeroEggRuntimeCandidateExporter.CreatePaths(frozenOutputDirectory);
UnusedLevel65ZeroEggRuntimeCandidateResult frozen =
    await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
        new(
            root,
            baseImage,
            baseCue,
            frozenOutputDirectory,
            ReplaceExistingCandidate: Directory.Exists(frozenOutputDirectory)));
VerifyResult(frozen, frozenPaths, expectRecovery: false);
VerifySevenFiles(frozenPaths);
VerifyGuideAndChecklist(frozenPaths);
UnusedLevel65ZeroEggRuntimeCandidateResult verifiedFrozen =
    await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(frozenPaths);
VerifyResult(verifiedFrozen, frozenPaths, expectRecovery: false);
Require(!Directory.Exists(frozenPaths.OperationsDirectoryPath),
    "The frozen clean publication left operation debris.");
string frozenReceiptSha256 = HashFile(frozenPaths.StaticReadbackReceiptPath);
Require(
    IsPending(UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedFrozenReceiptSha256) ||
    frozenReceiptSha256 == UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedFrozenReceiptSha256,
    "The frozen canonical clean receipt hash changed.");
await ExerciseAncestorAliasNoWriteFixtureAsync(
    physicalTemporaryRoot,
    root,
    baseImage,
    baseCue,
    frozenPaths);

Console.WriteLine("PASS: isolated failure/tamper matrix completed, then the clean seven-file ID65 zero-egg candidate was frozen.");
Console.WriteLine($"Frozen directory: {frozenPaths.OutputDirectoryPath}");
Console.WriteLine($"Output BIN SHA-256: {frozen.OutputImageSha256}");
Console.WriteLine($"Output SCUS SHA-256: {frozen.OutputExecutableSha256}");
Console.WriteLine($"Output ID65 row-80 SHA-256: {frozen.OutputId65DataSha256}");
Console.WriteLine($"Output WAD.WAD SHA-256: {frozen.OutputWadSha256}");
Console.WriteLine($"Ordered transaction SHA-256: {frozen.OrderedTransactionSha256}");
Console.WriteLine($"Raw-sector diff SHA-256: {frozen.RawSectorDiffSha256}");
Console.WriteLine($"Canonical clean receipt SHA-256: {frozenReceiptSha256}");
foreach (UnusedLevel65ZeroEggRuntimeCandidateRawSectorDiff diff in frozen.RawSectorDiffs)
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
    UnusedLevel65ZeroEggRuntimeCandidatePaths frozenPaths)
{
    DirectorySnapshot outputBefore = SnapshotDirectory(frozenPaths.OutputDirectoryPath);
    FileStamp baseBefore = Stamp(baseImage);
    FileStamp receiptBefore = Stamp(frozenPaths.StaticReadbackReceiptPath);
    string aliasFixtureRoot = Path.Combine(
        physicalTemporaryRoot,
        $"spyro-id65-zero-egg-runtime-candidate-alias-{Guid.NewGuid():N}");
    Directory.CreateDirectory(aliasFixtureRoot);
    string aliasParent = Path.Combine(aliasFixtureRoot, "alias-parent");
    string frozenParent = Path.GetDirectoryName(frozenPaths.OutputDirectoryPath) ??
        throw new InvalidOperationException("The frozen zero-egg output has no parent.");
    Directory.CreateSymbolicLink(aliasParent, frozenParent);
    try
    {
        string aliasedOutput = Path.Combine(
            aliasParent,
            UnusedLevel65ZeroEggRuntimeCandidateExporter.OutputDirectoryName);
        UnusedLevel65ZeroEggRuntimeCandidatePaths aliasedPaths =
            UnusedLevel65ZeroEggRuntimeCandidateExporter.CreatePaths(aliasedOutput);
        bool createRejected = false;
        try
        {
            _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.CreateAsync(
                new(
                    workspaceRoot,
                    baseImage,
                    baseCue,
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
            _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(aliasedPaths);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("symbolic link or reparse point", StringComparison.Ordinal))
        {
            verificationRejected = true;
        }

        Require(createRejected, "A symlinked output ancestor reached zero-egg replacement publication.");
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
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
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

    using JsonDocument parsedReceipt = JsonDocument.Parse(canonical);
    string receiptOutputHash = parsedReceipt.RootElement
        .GetProperty("outputImageSha256")
        .GetString() ?? throw new InvalidDataException("The receipt output hash is missing.");
    string changedHash = (receiptOutputHash[0] == '0' ? "1" : "0") + receiptOutputHash[1..];
    string oneByteChange = canonical.Replace(
        receiptOutputHash,
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
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
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
    _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);
}

static async Task ExerciseSidecarTamperMatrixAsync(
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
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
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
}

static async Task RequireVerificationRejectedAsync(
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
    string failureMessage)
{
    bool rejected = false;
    try
    {
        _ = await UnusedLevel65ZeroEggRuntimeCandidateExporter.VerifyPublishedAsync(paths);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Require(rejected, failureMessage);
}

static void VerifyResult(
    UnusedLevel65ZeroEggRuntimeCandidateResult result,
    UnusedLevel65ZeroEggRuntimeCandidatePaths paths,
    bool expectRecovery)
{
    Require(result.Paths == paths, "Result paths changed.");
    UnusedLevel65ZeroEggRuntimeCandidatePlan plan = result.Plan;
    Require(
        plan.ProfileId == UnusedLevel65ZeroEggRuntimeCandidateExporter.ProfileId &&
        plan.BaseImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
        plan.BaseCueSha256 == "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2" &&
        plan.SourceExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
        plan.OutputExecutableSha256 == plan.SourceExecutableSha256 &&
        plan.SourceId65DataSha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
        plan.OutputId65DataSha256 == "676a27bc39bb3b26b1fac84361b8633f5c9b75b926f49b5c963336f2fc7d9eff" &&
        plan.OutputHeaderSha256 == "6157dc52dbf53f744378aa5577209751dc75eb397401504ba23a28f1d5dabd71" &&
        plan.OutputActorSubfileSha256 == "e5e0f898a2d9487d56da9b28c1fb53706df219380ce5831f79334b06ebd1118f" &&
        plan.OutputSceneSubfileSha256 == "e3d726e1cdf95dc511bca50a30de837d26fc452c1e5cc3ca4581eb457c84799b" &&
        plan.OutputObjectTableSha256 == "20e8e5c41f127f898ccfa4801969fabc3ca835d7e32944cc7002311ef479b37b" &&
        plan.SourceT88RowSha256 == "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a" &&
        plan.OutputT88RowSha256 == "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f" &&
        plan.SourceGrassRowSha256 == "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190" &&
        plan.SourceGrassPackageSha256 == "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3" &&
        plan.SourceGrassPropertiesSha256 == "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b" &&
        plan.PreservedThiefPrivateBlockSha256 == "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136" &&
        plan.PreservedThiefPathSha256 == "b5bb29f426682bc52dfac7d9eb3df1674c0456bcd645ada497beaa6a9a3f984c" &&
        plan.PreservedFixupComponentSha256 == "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2",
        "Frozen zero-egg hash identity changed.");
    Require(
        plan.LevelId == 65 && plan.ContinuousLevelIndex == 35 &&
        plan.TargetTrueIndex == 88 && plan.DonorTrueIndex == 121 &&
        plan.TargetActorRootIndex == 37 && plan.ActorId == 0x01F5 &&
        plan.PreservedRawX == 96_850 && plan.PreservedRawY == 141_732 && plan.PreservedRawZ == 12_248 &&
        plan.ObjectCount == 107 && plan.GuardedLogicalWadBytes == 474 &&
        plan.ChangedLogicalWadBytes == 276 &&
        plan.AffectedRawSectorLbas.SequenceEqual(new[] { 53_906, 54_833, 54_837, 54_838, 54_850 }) &&
        plan.LoadCodes.Select(code => code.LevelId).SequenceEqual(new[] { 65, 13, 64, 15 }),
        "Frozen zero-egg target/diff identity changed.");
    Require(
        plan.Patches.Count == 5 &&
        plan.Patches.Select(patch => patch.WadOffset).SequenceEqual(
            new long[] { 0x69368E4, 0x693699A, 0x6B06244, 0x6B087B0, 0x6B0E938 }) &&
        plan.Patches.Select(patch => patch.ByteLength).SequenceEqual(new[] { 4, 2, 0x174, 0x58, 8 }) &&
        plan.Patches.Select(patch => patch.ChangedByteCount).SequenceEqual(new[] { 3, 2, 260, 9, 2 }) &&
        plan.Patches[0].BeforeHex == "00000000" && plan.Patches[0].AfterHex == "44FA1C00" &&
        plan.Patches[1].BeforeHex == "0000" && plan.Patches[1].AfterHex == "F501" &&
        plan.Patches[4].BeforeHex == "0000000000000000" &&
        plan.Patches[4].AfterHex == "040000008A000000",
        "Exact five guarded zero-egg ranges changed.");
    byte[] beforeT88 = Convert.FromHexString(plan.Patches[3].BeforeHex);
    byte[] afterT88 = Convert.FromHexString(plan.Patches[3].AfterHex);
    Require(
        beforeT88.AsSpan(0x0C, 12).SequenceEqual(afterT88.AsSpan(0x0C, 12)) &&
        Convert.ToHexString(SHA256.HashData(afterT88)).ToLowerInvariant() == plan.OutputT88RowSha256,
        "T88 XYZ preservation or authored row identity changed.");
    Require(
        result.ExactFiveRangeTransactionVerified && result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified && result.FullImageReadbackVerified &&
        result.ExecutableReadbackVerified && result.Id65DataReadbackVerified &&
        result.FullWadReadbackVerified && result.ObjectCountAndAllOtherRowsPreserved &&
        result.ThiefPrivateBlockPathAndFixupsPreserved && result.Class21And22ObjectRowsAbsent &&
        result.RetailTerrainCollisionTextureMusicAndSpawnPreserved &&
        result.NoMemoryCardOrSaveAuthorized && result.RawSectorIntegrityVerified &&
        result.BaseCandidatePreserved && result.CanonicalReceiptVerified &&
        result.SevenFilePublicationVerified &&
        result.RollbackRecoveryVerified == expectRecovery &&
        result.FullDirectoryRollbackVerified == expectRecovery &&
        result.FinderHandoffVerified && result.DisposableRuntimeCandidateAuthorized &&
        !result.RuntimeProofComplete && !result.AppEnabled &&
        !result.NormalCreateBinEnabled && !result.PromotionAuthorized,
        "Result proof or fail-closed flags changed.");
    Require(
        result.OutputExecutableSha256 == plan.SourceExecutableSha256 &&
        result.OutputId65DataSha256 == plan.OutputId65DataSha256 &&
        result.OutputHeaderSha256 == plan.OutputHeaderSha256 &&
        result.OutputActorSubfileSha256 == plan.OutputActorSubfileSha256 &&
        result.OutputSceneSubfileSha256 == plan.OutputSceneSubfileSha256 &&
        result.OutputObjectTableSha256 == plan.OutputObjectTableSha256 &&
        result.OrderedTransactionSha256 == plan.OrderedTransactionSha256 &&
        result.GuardedLogicalWadBytes == 474 && result.ChangedLogicalWadBytes == 276 &&
        result.RebuiltRawSectorCount == 5 && result.ChangedRawSectorCount == 5 &&
        result.RawSectorDiffs.Select(diff => diff.RawSectorLba)
            .SequenceEqual(new[] { 53_906, 54_833, 54_837, 54_838, 54_850 }) &&
        result.RawSectorDiffs.All(diff =>
            diff.HeaderChangedBytes == 0 && diff.SubheaderChangedBytes == 0 &&
            diff.ReservedChangedBytes == 0 && diff.PayloadChangedBytes > 0) &&
        result.Receipt.OutputImageSha256 == result.OutputImageSha256 &&
        result.Receipt.RawSectorDiffSha256 == result.RawSectorDiffSha256,
        "Exact zero-egg image/readback boundary changed.");
    Require(
        IsPending(UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedOutputImageSha256) ||
        result.OutputImageSha256 == UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedOutputImageSha256,
        "Pinned zero-egg output BIN hash changed.");
    Require(
        IsPending(UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedRawSectorDiffSha256) ||
        result.RawSectorDiffSha256 == UnusedLevel65ZeroEggRuntimeCandidateExporter.ExpectedRawSectorDiffSha256,
        "Pinned zero-egg raw-sector diff changed.");
}

static void VerifySevenFiles(UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
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

static void VerifyGuideAndChecklist(UnusedLevel65ZeroEggRuntimeCandidatePaths paths)
{
    string guide = File.ReadAllText(paths.LocationGuidePath);
    string checklist = File.ReadAllText(paths.RuntimeChecklistPath);
    Require(
        guide.Contains("width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\"", StringComparison.Ordinal) &&
        guide.Contains("Memory Card 1: None", StringComparison.Ordinal) &&
        guide.Contains("Memory Card 2: None", StringComparison.Ordinal) &&
        guide.Contains("class-0x0022 egg", StringComparison.Ordinal) &&
        guide.Contains("Pause + Inventory", StringComparison.Ordinal) &&
        guide.Contains("NO APP / CREATE BIN / RELEASE INTEGRATION", StringComparison.Ordinal),
        "The exact 1200x1000 no-card guide changed.");
    Require(
        checklist.Contains("Memory Card 1: None", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 2: None", StringComparison.Ordinal) &&
        checklist.Contains("class-0x0021 thief", StringComparison.Ordinal) &&
        checklist.Contains("class-0x0022 egg", StringComparison.Ordinal) &&
        checklist.Contains("Die once and respawn", StringComparison.Ordinal) &&
        checklist.Contains("Leave and re-enter ID65", StringComparison.Ordinal) &&
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

static bool IsPending(string value) =>
    string.Equals(value, "PENDING", StringComparison.Ordinal);

static void VerifyExporterSourceTerminology(string root)
{
    string sourcePath = Path.Combine(
        root,
        "src",
        "Spyro.Editor.Core",
        "Exporting",
        "UnusedLevel65ZeroEggRuntimeCandidateExporter.cs");
    Require(File.Exists(sourcePath), "The zero-egg exporter source is missing.");
    string source = File.ReadAllText(sourcePath);
    string[] legitimateMusicFragments =
    [
        "RetailTerrainCollisionTextureMusicAndSpawnPreserved",
        "textures, music, spawn",
        "collision, music, and spawn"
    ];
    string[] misleadingMusicLines = File.ReadLines(sourcePath)
        .Where(line => line.Contains("music", StringComparison.OrdinalIgnoreCase))
        .Where(line => !legitimateMusicFragments.Any(fragment =>
            line.Contains(fragment, StringComparison.Ordinal)))
        .ToArray();
    Require(
        misleadingMusicLines.Length == 0,
        $"The zero-egg exporter still contains copied music diagnostics: {string.Join(" | ", misleadingMusicLines)}");
    string[] forbiddenDiagnostics =
    [
        "two SCUS sectors",
        "closure sector set",
        "published music",
        "stale music",
        "music operation",
        "music destination",
        "music recovery",
        "music writer",
        "music global writer",
        "music rollback",
        "music output",
        "music baseline",
        "music physical image comparison",
        "frozen music receipt"
    ];
    Require(
        forbiddenDiagnostics.All(phrase =>
            !source.Contains(phrase, StringComparison.OrdinalIgnoreCase)),
        "The zero-egg exporter contains a stale copied diagnostic phrase.");
    Require(
        source.Contains("exact five zero-egg WAD sectors", StringComparison.Ordinal) &&
        source.Contains("five-range zero-egg sector set", StringComparison.Ordinal),
        "The exact five-WAD-sector physical-boundary diagnostics are missing.");
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
