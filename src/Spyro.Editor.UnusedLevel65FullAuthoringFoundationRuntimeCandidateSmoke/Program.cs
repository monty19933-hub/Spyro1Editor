using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Spyro.Editor.Core.Exporting;

if (UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.Retired)
{
    bool retiredBeforeInputInspection = false;
    try
    {
        await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(null!);
    }
    catch (InvalidOperationException ex) when (
        string.Equals(
            ex.Message,
            UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.RetirementReason,
            StringComparison.Ordinal))
    {
        retiredBeforeInputInspection = true;
    }

    if (!retiredBeforeInputInspection)
    {
        throw new InvalidOperationException(
            "The retired full-authoring foundation publisher did not fail closed before input/filesystem inspection.");
    }

    Console.WriteLine(
        $"PASS full-authoring foundation retired fail-close: {UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.RetirementReason}");
    return;
}

const string ExpectedBaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string ExpectedAuthoredModelSha256 =
    "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
const string ExpectedAuthoredCollisionSha256 =
    "5e7b4430c9bfbd2793df1d9833d8d3af005924d7c110b66f0b0e1f8bc818c056";
const string ExpectedAuthoredCollisionTreeSha256 =
    "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd";
const string ExpectedAuthoredCollisionBlocksSha256 =
    "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4";
const string ExpectedOutputImageSha256 =
    "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
const string ExpectedOutputDataSha256 =
    "eb5ca8459300392de12246e57770a57447f7bd64ccf3e978137d2362d7694160";
const string ExpectedRawSectorDiffSha256 =
    "85cee7ab1010bce03e8d533fadb2b2d8be7c448cc3254db2670bb2b576887baa";
const long ExpectedChangedLogicalWadBytes = 300_553;
const long ExpectedChangedPhysicalImageBytes = 359_151;
const int ExpectedRebuiltRawSectorCount = 213;
const int ExpectedChangedRawSectorCount = 213;
int[] expectedChangedRawSectorLbas =
[
    .. Enumerable.Range(54_356, 2),
    .. Enumerable.Range(54_434, 189),
    .. Enumerable.Range(54_624, 2),
    .. Enumerable.Range(54_628, 20)
];

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string basePrefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string baseImagePath = basePrefix + ".bin";
string baseCuePath = basePrefix + ".cue";
string outputDirectoryPath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership");

Require(File.Exists(baseImagePath), $"Missing exact display-name base BIN: {baseImagePath}");
Require(File.Exists(baseCuePath), $"Missing exact display-name base CUE: {baseCuePath}");
Require(
    await HashFileAsync(baseImagePath) == ExpectedBaseImageSha256,
    "The foundation writer base is not the exact passed ID65 display-name BIN.");
ValidateCuePair(baseCuePath, baseImagePath);

string[] protectedIntegrationPaths =
[
    Path.Combine(repositoryRoot, "spyro-level-catalog.json"),
    Path.Combine(repositoryRoot, "Directory.Build.props"),
    Path.Combine(repositoryRoot, "src", "Spyro.Editor.App", "Spyro.Editor.App.csproj"),
    Path.Combine(repositoryRoot, "src", "Spyro.Editor.App", "AppReleaseIdentity.cs"),
    baseImagePath,
    baseCuePath
];
IReadOnlyDictionary<string, string> protectedBefore = await HashFilesAsync(protectedIntegrationPaths);

UnusedLevel65FullAuthoringFoundationRuntimeCandidateRequest normalRequest = new(
    baseImagePath,
    baseCuePath,
    outputDirectoryPath,
    ReplaceExistingCandidate: true,
    RequestFinderReveal: true);
await RecoverCurrentOwnedCollisionIfPresentAsync(normalRequest);
UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult first =
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(first, requirePinnedOutput: false, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> firstSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireNoPublicationDebris(first.Paths);

bool replaceRefusalObserved = false;
try
{
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(
        normalRequest with { ReplaceExistingCandidate = false });
}
catch (IOException ex) when (
    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
{
    replaceRefusalObserved = true;
}
Require(replaceRefusalObserved, "The foundation writer did not refuse an unapproved existing-directory replacement.");
RequireSnapshotsEqual(firstSnapshot, SnapshotDirectory(outputDirectoryPath), "Replacement refusal");
RequireNoPublicationDebris(first.Paths);

await VerifyConcurrentWriterRefusalAsync(normalRequest, first, firstSnapshot);
await VerifyNoPreviousPublishedCandidateRecoveryAsync(first);

string rollbackMarkerPath = Path.Combine(outputDirectoryPath, "smoke-full-directory-rollback-marker.txt");
await File.WriteAllTextAsync(
    rollbackMarkerPath,
    "This marker must survive injected full-directory rollback.\n",
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
IReadOnlyDictionary<string, string> rollbackSnapshot = SnapshotDirectory(outputDirectoryPath);
bool backupFailureObserved = false;
try
{
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-previous-candidate-backup")
                throw new IOException("Injected foundation post-backup publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected foundation post-backup publication failure", StringComparison.Ordinal))
{
    backupFailureObserved = true;
}
Require(backupFailureObserved, "The foundation writer did not expose its post-backup rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Post-backup rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool rollbackFailureObserved = false;
try
{
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected foundation full-directory publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected foundation full-directory publication failure", StringComparison.Ordinal))
{
    rollbackFailureObserved = true;
}
Require(rollbackFailureObserved, "The foundation writer did not expose its post-publication rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Full-directory rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool incompleteRollbackObserved = false;
try
{
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected foundation incomplete-rollback publication failure.");
            if (stage == "before-previous-candidate-restore")
                throw new IOException("Injected foundation prior-directory restore failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected foundation prior-directory restore failure", StringComparison.Ordinal))
{
    incompleteRollbackObserved = true;
}
Require(incompleteRollbackObserved, "The foundation writer did not preserve an injected incomplete rollback.");
Require(
    Directory.Exists(first.Paths.OperationsDirectoryPath),
    "The foundation writer removed the operation needed for restart recovery.");
string[] recoveryOperations = Directory.GetDirectories(first.Paths.OperationsDirectoryPath);
Require(recoveryOperations.Length == 1, "Incomplete foundation rollback retained an unexpected operation count.");
Require(
    Directory.GetFiles(recoveryOperations[0], "operation-journal.json", SearchOption.AllDirectories).Length == 1 &&
    Directory.GetDirectories(recoveryOperations[0], "previous-candidate", SearchOption.AllDirectories).Length == 1,
    "Incomplete foundation rollback did not retain its journal and prior full-directory backup.");

UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult recovered =
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(recovered, requirePinnedOutput: false, expectRecoveredOperation: true);
Require(!File.Exists(rollbackMarkerPath), "Restart recovery/final publication retained the rollback marker.");
RequireNoPublicationDebris(recovered.Paths);

UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult repeat =
    await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(repeat, requirePinnedOutput: true, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> repeatSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireSnapshotsEqual(firstSnapshot, repeatSnapshot, "Deterministic clean foundation rerun");
Require(
    first.OutputImageSha256 == recovered.OutputImageSha256 &&
    recovered.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == recovered.OutputDataSha256 &&
    recovered.OutputDataSha256 == repeat.OutputDataSha256 &&
    first.ChangedLogicalWadBytes == repeat.ChangedLogicalWadBytes &&
    first.ChangedPhysicalImageBytes == repeat.ChangedPhysicalImageBytes &&
    first.RawSectorDiffs.SequenceEqual(repeat.RawSectorDiffs),
    "The foundation writer did not reproduce its exact candidate/diff boundary.");
Require(
    await HashFilesAsync(protectedIntegrationPaths) is { } protectedAfter &&
    protectedBefore.OrderBy(pair => pair.Key).SequenceEqual(protectedAfter.OrderBy(pair => pair.Key)),
    "The disposable foundation writer changed a protected base/integration file.");
RequireNoPublicationDebris(repeat.Paths);

Console.WriteLine(
    "PASS UnusedLevel65FullAuthoringFoundationRuntimeCandidateSmoke: deterministic full-directory publication, " +
    "journaled rollback/restart recovery, exact static/readback handoff, and no normal integration passed.");
Console.WriteLine($"CUE: {repeat.Paths.OutputCuePath}");
Console.WriteLine($"Reveal in Finder: {repeat.Paths.FinderHelperPath}");
Console.WriteLine($"Guide: {repeat.Paths.LocationGuidePath}");
Console.WriteLine($"Checklist: {repeat.Paths.RuntimeChecklistPath}");
Console.WriteLine($"Receipt: {repeat.Paths.StaticReadbackReceiptPath}");
Console.WriteLine($"BIN SHA-256: {repeat.OutputImageSha256}");
Console.WriteLine($"ID65 data SHA-256: {repeat.OutputDataSha256}");
Console.WriteLine(
    $"Logical/physical changed bytes: {repeat.ChangedLogicalWadBytes}/{repeat.ChangedPhysicalImageBytes}; " +
    $"rebuilt/changed raw sectors: {repeat.RebuiltRawSectorCount}/{repeat.ChangedRawSectorCount}; " +
    $"LBAs: [{string.Join(',', repeat.Plan.AffectedRawSectorLbas)}].");

async Task VerifyResultAsync(
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult result,
    bool requirePinnedOutput,
    bool expectRecoveredOperation)
{
    string expectedPrefix = Path.Combine(
        outputDirectoryPath,
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.OutputPrefix);
    Require(
        result.Paths.OutputDirectoryPath == Path.GetFullPath(outputDirectoryPath) &&
        result.Paths.OutputPrefix == expectedPrefix &&
        result.Paths.OutputImagePath == expectedPrefix + ".bin" &&
        result.Paths.OutputCuePath == expectedPrefix + ".cue" &&
        result.Paths.FoundationCompositionPlanPath == expectedPrefix + "-foundation-composition-plan.json" &&
        result.Paths.StaticReadbackReceiptPath == expectedPrefix + "-static-readback-receipt.json" &&
        result.Paths.RuntimeChecklistPath == expectedPrefix + "-runtime-checklist.md" &&
        result.Paths.LocationGuidePath == expectedPrefix + "-location-guide.svg" &&
        result.Paths.FinderHelperPath == expectedPrefix + "-Reveal-in-Finder.command",
        "The foundation writer artifact paths drifted from the approved isolated profile.");
    string[] requiredFiles =
    [
        result.Paths.OutputImagePath,
        result.Paths.OutputCuePath,
        result.Paths.FoundationCompositionPlanPath,
        result.Paths.StaticReadbackReceiptPath,
        result.Paths.RuntimeChecklistPath,
        result.Paths.LocationGuidePath,
        result.Paths.FinderHelperPath
    ];
    Require(requiredFiles.All(File.Exists), "The foundation writer omitted a required candidate/handoff artifact.");
    Require(
        Directory.GetFiles(outputDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(requiredFiles.Select(Path.GetFullPath).Order(StringComparer.Ordinal)),
        "The final foundation candidate directory contains an unexpected top-level artifact.");
    ValidateCuePair(result.Paths.OutputCuePath, result.Paths.OutputImagePath);

    Require(
        result.Plan.SchemaVersion == 1 &&
        result.Plan.ProfileId == UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.ProfileId &&
        result.Plan.ConstructionProfileId == UnusedLevel65FullAuthoringConstructionTemplate.ProfileId &&
        result.Plan.BaseImageSha256 == ExpectedBaseImageSha256 &&
        result.Plan.ModelPreimageSha256 ==
            "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47" &&
        result.Plan.AuthoredModelSha256 == ExpectedAuthoredModelSha256 &&
        result.Plan.AuthoredCollisionSha256 == ExpectedAuthoredCollisionSha256 &&
        result.Plan.AuthoredCollisionTreeSha256 == ExpectedAuthoredCollisionTreeSha256 &&
        result.Plan.AuthoredCollisionBlocksSha256 == ExpectedAuthoredCollisionBlocksSha256 &&
        result.Plan.WadLba == 37 &&
        result.Plan.ModelSubfileWadOffset == 0x6A15000 &&
        result.Plan.ModelSubfileByteLength == 0x94800 &&
        result.Plan.TargetSectorIndex == 213 &&
        result.Plan.AddedLowDetailVertexCount == 3 &&
        result.Plan.AddedLowDetailFaceCount == 1 &&
        result.Plan.AddedHighDetailVertexCount == 3 &&
        result.Plan.AddedHighDetailFaceCount == 1 &&
        result.Plan.CollisionTriangleIndex == 13_995 &&
        result.Plan.NativeCollisionCellCount == 4_252 &&
        result.Plan.AuthoredCollisionCellCount == 4_252 &&
        result.Plan.ChangedCollisionCells.SequenceEqual(new[]
        {
            new UnusedLevel65ConstructionCollisionCell(34, 35, 1),
            new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
            new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
        }) &&
        result.Plan.FullExposureProofVerified &&
        result.Plan.SpawnAndPlayerAnchorVerified &&
        result.Plan.CycloramaVerified &&
        result.Plan.DisplayNameVerified &&
        result.Plan.RequiresDuckStationRuntimeProof &&
        !result.Plan.PromotionAuthorized &&
        !result.Plan.NormalCreateBinEnabled,
        "The foundation composition plan lost its exact HP+LP/collision/provenance boundary.");
    VerifyLoadCodes(result.LoadCodes);
    Require(
        result.Plan.LoadCodes.SequenceEqual(result.LoadCodes),
        "The serialized foundation plan and result disagree on the four test codes.");

    Require(
        result.OutputImageSha256 == await HashFileAsync(result.Paths.OutputImagePath) &&
        result.Receipt.OutputImageSha256 == result.OutputImageSha256 &&
        result.Receipt.OutputDataSha256 == result.OutputDataSha256 &&
        result.Receipt.AuthoredModelSha256 == ExpectedAuthoredModelSha256 &&
        result.Receipt.AuthoredCollisionSha256 == ExpectedAuthoredCollisionSha256 &&
        result.Receipt.AuthoredCollisionTreeSha256 == ExpectedAuthoredCollisionTreeSha256 &&
        result.Receipt.AuthoredCollisionBlocksSha256 == ExpectedAuthoredCollisionBlocksSha256 &&
        result.Receipt.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        result.Receipt.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes &&
        result.Receipt.RebuiltRawSectorCount == result.RebuiltRawSectorCount &&
        result.Receipt.ChangedRawSectorCount == result.ChangedRawSectorCount &&
        result.Receipt.RawSectorDiffs.SequenceEqual(result.RawSectorDiffs),
        "The foundation static receipt does not bind the exact candidate/diff hashes.");
    Require(
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.FoundationCompositionVerified &&
        result.ModelReadbackVerified &&
        result.CollisionReadbackVerified &&
        result.CollisionSemanticDeltaVerified &&
        result.OcclusionOwnershipVerified &&
        result.ProtectedSubfilesPreserved &&
        result.SpawnAndPlayerAnchorPreserved &&
        result.CycloramaPreserved &&
        result.DisplayNamePreserved &&
        result.RawSectorIntegrityVerified &&
        result.BaseCandidatePreserved &&
        result.FullDirectoryPublicationVerified &&
        result.RollbackRecoveryVerified == expectRecoveredOperation &&
        result.FullDirectoryRollbackVerified == expectRecoveredOperation &&
        result.FinderHandoffVerified &&
        !result.PromotionAuthorized &&
        !result.NormalCreateBinEnabled,
        "The foundation result weakened a composition/readback/preservation/publication guard.");
    Require(
        result.Receipt.ExactLogicalDiffBoundaryVerified &&
        result.Receipt.ExactPhysicalSectorBoundaryVerified &&
        result.Receipt.FoundationCompositionVerified &&
        result.Receipt.ModelReadbackVerified &&
        result.Receipt.CollisionReadbackVerified &&
        result.Receipt.CollisionSemanticDeltaVerified &&
        result.Receipt.OcclusionOwnershipVerified &&
        result.Receipt.ProtectedSubfilesPreserved &&
        result.Receipt.SpawnAndPlayerAnchorPreserved &&
        result.Receipt.CycloramaPreserved &&
        result.Receipt.DisplayNamePreserved &&
        result.Receipt.RawSectorIntegrityVerified &&
        result.Receipt.BaseCandidatePreserved &&
        result.Receipt.FullDirectoryPublicationVerified &&
        result.Receipt.RollbackRecoveryVerified == expectRecoveredOperation &&
        result.Receipt.FullDirectoryRollbackVerified == expectRecoveredOperation &&
        result.Receipt.FinderHandoffVerified &&
        !result.Receipt.PromotionAuthorized &&
        !result.Receipt.NormalCreateBinEnabled,
        "The durable foundation receipt weakened a static/readback/publication guard.");
    VerifyRawSectorBoundary(result);
    await VerifySidecarsAsync(result);

    if (requirePinnedOutput)
    {
        Require(
            result.OutputImageSha256 == ExpectedOutputImageSha256 &&
            result.OutputDataSha256 == ExpectedOutputDataSha256 &&
            result.ChangedLogicalWadBytes == ExpectedChangedLogicalWadBytes &&
            result.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes &&
            result.RebuiltRawSectorCount == ExpectedRebuiltRawSectorCount &&
            result.ChangedRawSectorCount == ExpectedChangedRawSectorCount,
            "The exact foundation output hash or logical/raw-sector diff pin drifted: " +
            $"BIN={result.OutputImageSha256}, data={result.OutputDataSha256}, " +
            $"logical={result.ChangedLogicalWadBytes}, physical={result.ChangedPhysicalImageBytes}, " +
            $"rebuilt={result.RebuiltRawSectorCount}, changed={result.ChangedRawSectorCount}.");
    }
}

void VerifyRawSectorBoundary(UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult result)
{
    Require(
        result.RebuiltRawSectorCount == result.ChangedRawSectorCount &&
        result.ChangedRawSectorCount == result.RawSectorDiffs.Count &&
        result.RawSectorDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(
            result.Plan.AffectedRawSectorLbas) &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(expectedChangedRawSectorLbas) &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(
            result.Plan.AffectedRawSectorLbas.Distinct().Order()) &&
        result.RawSectorDiffs.Sum(diff => (long)diff.TotalChangedBytes) == result.ChangedPhysicalImageBytes &&
        HashRawSectorDiffs(result.RawSectorDiffs) == ExpectedRawSectorDiffSha256,
        "The foundation raw-sector count/LBA/physical-byte boundary is inconsistent.");
    foreach (UnusedLevel65FullAuthoringFoundationRuntimeCandidateRawSectorDiff diff in result.RawSectorDiffs)
    {
        Require(
            diff.HeaderChangedBytes == 0 &&
            diff.SubheaderChangedBytes == 0 &&
            diff.PayloadChangedBytes > 0 &&
            diff.PayloadChangedBytes <= 2048 &&
            diff.EdcChangedBytes is >= 0 and <= 4 &&
            diff.ReservedChangedBytes == 0 &&
            diff.EccPChangedBytes is >= 0 and <= 172 &&
            diff.EccQChangedBytes is >= 0 and <= 104 &&
            diff.TotalChangedBytes ==
                diff.HeaderChangedBytes + diff.SubheaderChangedBytes + diff.PayloadChangedBytes + diff.EdcChangedBytes +
                diff.ReservedChangedBytes + diff.EccPChangedBytes + diff.EccQChangedBytes,
            $"Raw sector {diff.RawSectorLba} has an invalid MODE2/2352 diff classification.");
    }
}

string HashRawSectorDiffs(
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
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
        .ToLowerInvariant();
}

async Task RecoverCurrentOwnedCollisionIfPresentAsync(
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateRequest request)
{
    UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths =
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreatePaths(
            request.OutputDirectoryPath);
    if (!Directory.Exists(paths.OperationsDirectoryPath))
        return;
    string[] topLevelEntries = Directory.GetFileSystemEntries(
        paths.OperationsDirectoryPath,
        "*",
        SearchOption.TopDirectoryOnly);
    string[] operations = topLevelEntries
        .Where(path =>
            Directory.Exists(path) &&
            Path.GetFileName(path).StartsWith("foundation-candidate-", StringComparison.Ordinal))
        .ToArray();
    if (operations.Length == 0)
    {
        Require(topLevelEntries.Length == 0, "The foundation operations parent contains a non-owned entry.");
        return;
    }
    Require(
        topLevelEntries.Length == operations.Length &&
        operations.Length == 2 &&
        !Directory.Exists(paths.OutputDirectoryPath),
        "The preflight found stale foundation state other than the exact observed two-operation collision.");
    string[] backups = operations.SelectMany(operation =>
            Directory.GetDirectories(operation, "previous-candidate", SearchOption.TopDirectoryOnly))
        .ToArray();
    Require(backups.Length == 1, "The exact collision recovery requires one authoritative previous directory.");
    IReadOnlyDictionary<string, string> backupSnapshot = SnapshotDirectory(backups[0]);
    Require(
        backupSnapshot.ContainsKey("F:smoke-full-directory-rollback-marker.txt"),
        "The collision backup is missing the exact prior-directory marker.");

    bool recoveryProbeObserved = false;
    try
    {
        await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(request with
        {
            TestStageHook = stage =>
            {
                if (stage == "after-previous-candidate-backup")
                {
                    throw new IOException(
                        "Injected post-collision recovery probe after previous-candidate backup.");
                }
            }
        });
    }
    catch (IOException ex) when (
        ex.ToString().Contains(
            "Injected post-collision recovery probe after previous-candidate backup",
            StringComparison.Ordinal))
    {
        recoveryProbeObserved = true;
    }
    Require(recoveryProbeObserved, "The writer did not consume the exact owned two-operation collision.");
    RequireSnapshotsEqual(backupSnapshot, SnapshotDirectory(paths.OutputDirectoryPath), "Collision recovery");
    Require(
        File.Exists(Path.Combine(paths.OutputDirectoryPath, "smoke-full-directory-rollback-marker.txt")),
        "Collision recovery did not restore the authoritative prior-directory marker.");
    RequireNoPublicationDebris(paths);
}

async Task VerifyConcurrentWriterRefusalAsync(
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateRequest request,
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult reference,
    IReadOnlyDictionary<string, string> expectedSnapshot)
{
    TaskCompletionSource<bool> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult> active =
        Task.Run(async () =>
            await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(request with
            {
                TestStageHook = stage =>
                {
                    if (stage != "after-global-writer-lease-acquired")
                        return;
                    held.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                }
            }));
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult? completed = null;
    try
    {
        await held.Task.WaitAsync(TimeSpan.FromSeconds(30));
        string[] ownedBefore = GetOwnedOperationDirectories(reference.Paths.OperationsDirectoryPath);
        Require(
            ownedBefore.Length == 0 &&
            Directory.Exists(reference.Paths.OutputDirectoryPath),
            "The lease-held writer created an operation or moved the output before contention.");
        RequireSnapshotsEqual(
            expectedSnapshot,
            SnapshotDirectory(reference.Paths.OutputDirectoryPath),
            "Lease-held pre-operation state");

        bool contentionRefused = false;
        try
        {
            await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(request)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (IOException ex) when (
            ex.ToString().Contains(
                "Another foundation runtime candidate writer is active",
                StringComparison.Ordinal))
        {
            contentionRefused = true;
        }
        Require(contentionRefused, "A concurrent foundation writer was not refused by the per-output lease.");
        string[] ownedAfterRefusal = GetOwnedOperationDirectories(
            reference.Paths.OperationsDirectoryPath);
        Require(
            ownedAfterRefusal.SequenceEqual(ownedBefore, StringComparer.Ordinal),
            "The refused concurrent writer created or changed an owned operation.");
        RequireSnapshotsEqual(
            expectedSnapshot,
            SnapshotDirectory(reference.Paths.OutputDirectoryPath),
            "Refused writer pre-operation state");
    }
    finally
    {
        release.TrySetResult(true);
        completed = await active.WaitAsync(TimeSpan.FromSeconds(30));
    }

    UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult completedResult = completed ??
        throw new InvalidOperationException(
            "The serialized foundation writer did not complete after lease release.");
    await VerifyResultAsync(completedResult, requirePinnedOutput: false, expectRecoveredOperation: false);
    RequireSnapshotsEqual(expectedSnapshot, SnapshotDirectory(outputDirectoryPath), "Serialized writer contention");
    RequireNoPublicationDebris(completedResult.Paths);
}

async Task VerifyNoPreviousPublishedCandidateRecoveryAsync(
    UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult reference)
{
    string sandbox = Path.Combine(
        Path.GetTempPath(),
        $"spyro-id65-foundation-no-previous-{Guid.NewGuid():N}");
    string isolatedOutput = Path.Combine(sandbox, "candidate");
    UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths =
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreatePaths(isolatedOutput);
    try
    {
        UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult stagedSeed =
            await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(new(
                baseImagePath,
                baseCuePath,
                paths.OutputDirectoryPath,
                ReplaceExistingCandidate: false,
                RequestFinderReveal: false));
        Require(
            !stagedSeed.RollbackRecoveryVerified &&
            !stagedSeed.FullDirectoryRollbackVerified &&
            stagedSeed.OutputImageSha256 == reference.OutputImageSha256 &&
            stagedSeed.OutputDataSha256 == reference.OutputDataSha256 &&
            !stagedSeed.FinderHandoffVerified &&
            stagedSeed.FinderReveal == null,
            "The isolated no-previous recovery seed was not an exact clean candidate.");
        RequireNoPublicationDebris(stagedSeed.Paths);

        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            $"foundation-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        string backupDirectory = Path.Combine(operationRoot, "previous-candidate");
        Directory.Move(paths.OutputDirectoryPath, stagedDirectory);
        await File.WriteAllBytesAsync(Path.Combine(operationRoot, "operation.lease"), []);
        string journal = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                operationKind = "unused-level-65-full-authoring-foundation-runtime-candidate",
                phase = "staging",
                operationRootPath = operationRoot,
                outputDirectoryPath = paths.OutputDirectoryPath,
                stagedDirectoryPath = stagedDirectory,
                backupDirectoryPath = backupDirectory,
                hadPreviousCandidate = false
            },
            new JsonSerializerOptions { WriteIndented = true }) + "\n";
        await File.WriteAllTextAsync(
            Path.Combine(operationRoot, "operation-journal.json"),
            journal,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Directory.Move(stagedDirectory, paths.OutputDirectoryPath);

        UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult recoveredNoPrevious =
            await UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.CreateAsync(new(
                baseImagePath,
                baseCuePath,
                paths.OutputDirectoryPath,
                ReplaceExistingCandidate: false,
                RequestFinderReveal: false));
        Require(
            recoveredNoPrevious.RollbackRecoveryVerified &&
            recoveredNoPrevious.FullDirectoryRollbackVerified &&
            recoveredNoPrevious.Receipt.RollbackRecoveryVerified &&
            recoveredNoPrevious.Receipt.FullDirectoryRollbackVerified &&
            recoveredNoPrevious.OutputImageSha256 == reference.OutputImageSha256 &&
            recoveredNoPrevious.OutputDataSha256 == reference.OutputDataSha256 &&
            !recoveredNoPrevious.FinderHandoffVerified &&
            recoveredNoPrevious.FinderReveal == null &&
            !File.Exists(recoveredNoPrevious.Paths.FinderHelperPath),
            "The no-previous move-before-journal crash window did not recover and republish exactly.");
        RequireNoPublicationDebris(recoveredNoPrevious.Paths);
    }
    finally
    {
        if (Directory.Exists(sandbox))
            Directory.Delete(sandbox, recursive: true);
    }
    Require(!Directory.Exists(sandbox), "The isolated no-previous recovery smoke left debris.");
}

async Task VerifySidecarsAsync(UnusedLevel65FullAuthoringFoundationRuntimeCandidateResult result)
{
    using JsonDocument planDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.FoundationCompositionPlanPath));
    using JsonDocument receiptDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.StaticReadbackReceiptPath));
    JsonElement serializedPlan = planDocument.RootElement;
    JsonElement serializedReceipt = receiptDocument.RootElement;
    Require(
        serializedPlan.GetProperty("profileId").GetString() ==
            UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.ProfileId &&
        serializedPlan.GetProperty("authoredModelSha256").GetString() == ExpectedAuthoredModelSha256 &&
        serializedPlan.GetProperty("authoredCollisionSha256").GetString() == ExpectedAuthoredCollisionSha256 &&
        serializedPlan.GetProperty("nativeCollisionCellCount").GetInt32() == 4_252 &&
        serializedPlan.GetProperty("authoredCollisionCellCount").GetInt32() == 4_252 &&
        serializedPlan.GetProperty("changedCollisionCells").GetArrayLength() == 3 &&
        serializedPlan.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        !serializedPlan.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedPlan.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The foundation composition-plan JSON lost its exact static-only contract.");
    Require(
        serializedReceipt.GetProperty("profileId").GetString() ==
            UnusedLevel65FullAuthoringFoundationRuntimeCandidateExporter.ProfileId &&
        serializedReceipt.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        serializedReceipt.GetProperty("outputDataSha256").GetString() == result.OutputDataSha256 &&
        serializedReceipt.GetProperty("rawSectorDiffs").GetArrayLength() == result.RawSectorDiffs.Count &&
        serializedReceipt.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        serializedReceipt.GetProperty("baseCandidatePreserved").GetBoolean() &&
        !serializedReceipt.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedReceipt.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The foundation static receipt JSON lost its exact readback/nonintegration contract.");

    RuntimeCandidateFinderReveal finder = result.FinderReveal ??
        throw new InvalidOperationException("The macOS foundation handoff omitted Finder reveal metadata.");
    Require(
        finder.CuePath == result.Paths.OutputCuePath &&
        finder.PairedBinPath == result.Paths.OutputImagePath &&
        finder.HelperPath == result.Paths.FinderHelperPath &&
        finder.CuePairingVerified &&
        finder.HelperIsExecutable &&
        File.GetUnixFileMode(finder.HelperPath) == ExactFinderMode(),
        "The foundation Finder helper lost exact CUE pairing or 0755 permissions.");
    string helper = await File.ReadAllTextAsync(finder.HelperPath);
    Require(
        helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal) &&
        helper.Contains(Path.GetFileName(result.Paths.OutputCuePath), StringComparison.Ordinal),
        "The foundation Finder helper does not reveal its exact CUE.");

    string checklist = await File.ReadAllTextAsync(result.Paths.RuntimeChecklistPath);
    RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finder, result.LoadCodes);
    Require(
        checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) &&
        checklist.Contains("45", StringComparison.Ordinal) &&
        checklist.Contains("close range as HP terrain", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("far range as LP terrain", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("collision", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("reset", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("runtime", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("not", StringComparison.OrdinalIgnoreCase) &&
        result.LoadCodes.All(code => checklist.Contains(code.InputCode, StringComparison.Ordinal)),
        "The foundation checklist lost its near-HP/far-LP/collision/reset/runtime boundary or four codes.");

    string svgText = await File.ReadAllTextAsync(result.Paths.LocationGuidePath);
    XDocument svg = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
    XElement svgRoot = svg.Root ?? throw new InvalidDataException("The foundation guide SVG has no root.");
    Require(
        svgRoot.Name.LocalName == "svg" &&
        svgRoot.Attribute("width")?.Value == "1200" &&
        svgRoot.Attribute("height")?.Value == "1000" &&
        svgText.Contains("HP", StringComparison.Ordinal) &&
        svgText.Contains("LP", StringComparison.Ordinal) &&
        svgText.Contains("45", StringComparison.Ordinal) &&
        svgText.Contains("SPAWN", StringComparison.OrdinalIgnoreCase) &&
        result.LoadCodes.All(code => svgText.Contains(code.InputCode, StringComparison.Ordinal)),
        "The foundation location guide is malformed or omits its HP/LP/45-degree/spawn/four-code handoff.");
    VerifySvgTextBounds(svgRoot);
}

void VerifySvgTextBounds(XElement svgRoot)
{
    const double safeLeft = 40;
    const double safeRight = 1_120;
    const double safeBottom = 995;
    IEnumerable<XElement> textElements = svgRoot.Descendants()
        .Where(element => element.Name.LocalName == "text");
    int fragmentCount = 0;
    foreach (XElement textElement in textElements)
    {
        XElement[] tspans = textElement.Descendants()
            .Where(element => element.Name.LocalName == "tspan")
            .ToArray();
        IEnumerable<XElement> fragments = tspans.Length == 0 ? [textElement] : tspans;
        foreach (XElement fragment in fragments)
        {
            fragmentCount++;
            string text = string.Join(
                ' ',
                fragment.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            double x = ParseSvgNumber(fragment, textElement, "x");
            double y = ParseSvgNumber(fragment, textElement, "y");
            double fontSize = ParseSvgNumber(fragment, textElement, "font-size");
            double estimatedWidth = text.Length * fontSize * 0.60;
            string anchor = fragment.Attribute("text-anchor")?.Value ??
                textElement.Attribute("text-anchor")?.Value ?? "start";
            double left = anchor switch
            {
                "middle" => x - (estimatedWidth / 2),
                "end" => x - estimatedWidth,
                _ => x
            };
            double right = anchor switch
            {
                "middle" => x + (estimatedWidth / 2),
                "end" => x,
                _ => x + estimatedWidth
            };
            Require(
                text.Length > 0 &&
                fontSize is >= 10 and <= 40 &&
                left >= safeLeft &&
                right <= safeRight &&
                y > 0 &&
                y + fontSize <= safeBottom,
                $"The foundation guide text can clip: `{text}` bounds={left:F1}..{right:F1}, " +
                $"baseline/font={y:F1}/{fontSize:F1}.");
        }
    }
    Require(fragmentCount >= 15, "The foundation guide lost its expected wrapped identity/route/code text.");
}

double ParseSvgNumber(XElement fragment, XElement parent, string attributeName)
{
    string? value = fragment.Attribute(attributeName)?.Value ?? parent.Attribute(attributeName)?.Value;
    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
        ? parsed
        : throw new InvalidDataException(
            $"The foundation guide text omitted numeric `{attributeName}` positioning.");
}

void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
{
    (string Name, int Id, string Selection)[] expected =
    [
        ("ID65 candidate", 65, "Left, then Down"),
        ("Retail Town Square", 13, "Cross, then Triangle"),
        ("Gnasty's Loot", 64, "Left, then Right"),
        ("Sunny Flight", 15, "Cross, then Down")
    ];
    Require(loadCodes.Count == expected.Length, "The foundation handoff must contain exactly four load codes.");
    for (int index = 0; index < expected.Length; index++)
    {
        RuntimeCandidateLoadCode actual = loadCodes[index];
        string exactInput =
            $"Select; then {TestLevelWarpPatch.ActivationSequence}; then {expected[index].Selection}";
        Require(
            actual.TestName == expected[index].Name &&
            actual.LevelId == expected[index].Id &&
            actual.TargetSelection == expected[index].Selection &&
            actual.InputCode == exactInput,
            $"The complete foundation load code for {expected[index].Name} changed.");
    }
}

void ValidateCuePair(string cuePath, string binPath)
{
    string cue = File.ReadAllText(cuePath);
    Require(
        cue.Contains($"FILE \"{Path.GetFileName(binPath)}\" BINARY", StringComparison.OrdinalIgnoreCase) &&
        cue.Contains("TRACK 01 MODE2/2352", StringComparison.OrdinalIgnoreCase),
        $"CUE does not reference its exact MODE2/2352 BIN: {cuePath}");
}

void RequireNoPublicationDebris(UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
{
    Require(
        !Directory.Exists(paths.OperationsDirectoryPath),
        "The foundation writer retained operation/backup debris.");
    string[] debris = Directory.Exists(paths.OutputDirectoryPath)
        ? Directory.GetFileSystemEntries(paths.OutputDirectoryPath, "*", SearchOption.AllDirectories)
            .Where(path =>
                Path.GetFileName(path).Contains(".tmp", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains(".bak", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains("previous-candidate", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("operation-journal.json", StringComparison.OrdinalIgnoreCase))
            .ToArray()
        : [];
    Require(debris.Length == 0, $"Foundation publication debris remained: {string.Join(',', debris)}");
    VerifyWriterLeaseReleased(paths);
}

void VerifyWriterLeaseReleased(UnusedLevel65FullAuthoringFoundationRuntimeCandidatePaths paths)
{
    string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
        throw new InvalidOperationException("The foundation operations path has no parent.");
    string leasePath = Path.Combine(
        parent,
        ".unused-level-65-full-authoring-foundation-writer.lease");
    Require(
        File.Exists(leasePath) &&
        !Directory.Exists(leasePath) &&
        (File.GetAttributes(leasePath) & FileAttributes.ReparsePoint) == 0 &&
        new FileInfo(leasePath).Length == 0,
        "The persistent foundation writer lease is missing, nonempty, or an unsafe path type.");
    using FileStream lease = new(
        leasePath,
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);
    Require(lease.Length == 0, "The released foundation writer lease changed during the native probe.");
    if (OperatingSystem.IsWindows())
    {
        lease.Lock(0, 1);
        lease.Unlock(0, 1);
        return;
    }

    int fileDescriptor = checked((int)lease.SafeFileHandle.DangerousGetHandle());
    int lockResult = NativeLeaseProbe.Flock(fileDescriptor, NativeLeaseProbe.LockExclusiveNonBlocking);
    int lockError = lockResult == 0 ? 0 : Marshal.GetLastPInvokeError();
    Require(
        lockResult == 0,
        $"The persistent foundation writer lease remained natively locked (errno {lockError}).");
    Require(
        NativeLeaseProbe.Flock(fileDescriptor, NativeLeaseProbe.LockUnlock) == 0,
        "The foundation smoke could not release its native lease probe.");
}

string[] GetOwnedOperationDirectories(string operationsDirectoryPath) =>
    Directory.Exists(operationsDirectoryPath)
        ? Directory.GetDirectories(
            operationsDirectoryPath,
            "foundation-candidate-*",
            SearchOption.TopDirectoryOnly)
        : [];

IReadOnlyDictionary<string, string> SnapshotDirectory(string directory)
{
    Require(Directory.Exists(directory), $"Snapshot directory is missing: {directory}");
    Dictionary<string, string> snapshot = new(StringComparer.Ordinal)
    {
        ["D:."] = SnapshotUnixMode(directory)
    };
    foreach (string path in Directory.GetFileSystemEntries(
                 directory,
                 "*",
                 SearchOption.AllDirectories))
    {
        FileAttributes attributes = File.GetAttributes(path);
        Require(
            (attributes & FileAttributes.ReparsePoint) == 0,
            $"Snapshot refused a symbolic link/reparse point: {path}");
        string relative = Path.GetRelativePath(directory, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        if ((attributes & FileAttributes.Directory) != 0)
        {
            snapshot[$"D:{relative}"] = SnapshotUnixMode(path);
            continue;
        }

        using FileStream input = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        snapshot[$"F:{relative}"] =
            $"{SnapshotUnixMode(path)}:{input.Length}:{hash}";
    }
    return snapshot;
}

string SnapshotUnixMode(string path) => OperatingSystem.IsWindows()
    ? "windows"
    : Convert.ToString((int)File.GetUnixFileMode(path), 8);

void RequireSnapshot(string directory, IReadOnlyDictionary<string, string> expected) =>
    RequireSnapshotsEqual(expected, SnapshotDirectory(directory), "Full-directory rollback");

void RequireSnapshotsEqual(
    IReadOnlyDictionary<string, string> expected,
    IReadOnlyDictionary<string, string> actual,
    string label)
{
    Require(
        expected.Count == actual.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out string? hash) && hash == pair.Value),
        $"{label} changed the candidate directory: expected [{string.Join(',', expected.Keys.Order())}], " +
        $"actual [{string.Join(',', actual.Keys.Order())}].");
}

async Task<IReadOnlyDictionary<string, string>> HashFilesAsync(IEnumerable<string> paths)
{
    Dictionary<string, string> result = new(StringComparer.Ordinal);
    foreach (string path in paths)
    {
        Require(File.Exists(path), $"Protected foundation path is missing: {path}");
        result[path] = await HashFileAsync(path);
    }
    return result;
}

async Task<string> HashFileAsync(string path)
{
    await using FileStream input = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();
}

string FindRepositoryRoot(string? preferred)
{
    if (!string.IsNullOrWhiteSpace(preferred))
    {
        string candidate = Path.GetFullPath(preferred);
        if (File.Exists(Path.Combine(candidate, "spyro-level-catalog.json")))
            return candidate;
    }
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static UnixFileMode ExactFinderMode() =>
    UnixFileMode.UserRead |
    UnixFileMode.UserWrite |
    UnixFileMode.UserExecute |
    UnixFileMode.GroupRead |
    UnixFileMode.GroupExecute |
    UnixFileMode.OtherRead |
    UnixFileMode.OtherExecute;

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal static class NativeLeaseProbe
{
    internal const int LockExclusiveNonBlocking = 2 | 4;
    internal const int LockUnlock = 8;

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    internal static extern int Flock(int fileDescriptor, int operation);
}
