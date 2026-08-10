using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Spyro.Editor.Core.Exporting;

const string ExpectedBaseImageSha256 =
    "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
const string ExpectedAuthoredModelSha256 =
    "8422c32ca6b8555bc6dd5f8d0f266e4be90c8e4648c6facbeaef165f0a75a02b";
const string ExpectedAuthoredCollisionSha256 =
    "59aa6b65712fbcdb96358c7ea2854e1eda18cc0115f34d793f131bd8dec0dd1f";
const string ExpectedAuthoredCollisionTreeSha256 =
    "0ebefed842c3ebb7e3a5cafdfffa7968befc9caebf0999e8787322c4e34b2b61";
const string ExpectedAuthoredCollisionBlocksSha256 =
    "372ad0bb9d0ef51b2c7b9a01acf9a8aada3049b1caefdba597f67d54cb084d26";
const string ExpectedInverseModelSha256 =
    "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
const string ExpectedComposerPlanSha256 =
    "7db62a3d9a388c7e2388a6f77969d0511dab6e3ff6f01ccc9ab863e5fff81ced";
const string ExpectedOutputImageSha256 =
    "c33f83a0a5d64f58094c3a10487396903ebb2534b2438433552bbed6a8bbce15";
const string ExpectedOutputDataSha256 =
    "29697a69061dd7ca32fd0627ffb343368420803f369c741b03f313ea0c44597b";
const string ExpectedRawSectorDiffSha256 =
    "b28a166aa81b2cf6648348b829b659078390a2c5ec92c40782b0b32af85feb14";
const string ExpectedCueSha256 =
    "b4a2f391873bf7decdaaf6075f7587096c6990f2422503c2fa0e40ccd27b4f96";
const string ExpectedPlanFileSha256 =
    "0eba87b13f3c5ec1edf85bc5d7beec717540052d9b82b258f79807a2c240d13b";
const string ExpectedReceiptFileSha256 =
    "46fbc04b5d9a385abf9913f19f5bfe219c916492ac357e3f5efe861cc44465f1";
const string ExpectedChecklistSha256 =
    "320c52f38fbcc7af6b2068316d09d063c8ef4d96ed3893dfafe57c822c8fbdaa";
const string ExpectedGuideSha256 =
    "86af1a715d13171e12e342c3d2ed69c0a1bdb5088a12e3b4224ab3136e1b6a34";
const string ExpectedFinderHelperSha256 =
    "91878694fda4e399cfb7f5e00f7fcc223f2abe7ad6e966789118de950760584f";
const long ExpectedChangedLogicalWadBytes = 348_741;
const long ExpectedChangedPhysicalImageBytes = 407_354;
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
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE");
string baseImagePath = basePrefix + ".bin";
string baseCuePath = basePrefix + ".cue";
string outputDirectoryPath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-foundation-terrain-add-remove-second-tile");

Require(File.Exists(baseImagePath), $"Missing exact foundation BIN: {baseImagePath}");
Require(File.Exists(baseCuePath), $"Missing exact foundation CUE: {baseCuePath}");
Require(
    await HashFileAsync(baseImagePath) == ExpectedBaseImageSha256,
    "The terrain add/remove writer base is not the exact ID65 foundation BIN.");
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

UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest normalRequest = new(
    baseImagePath,
    baseCuePath,
    outputDirectoryPath,
    ReplaceExistingCandidate: true,
    RequestFinderReveal: true);
await RecoverCurrentOwnedCollisionIfPresentAsync(normalRequest);
UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult first =
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(first, requirePinnedOutput: false, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> firstSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireNoPublicationDebris(first.Paths);
await VerifyPrePublicationRollbackAsync(normalRequest, firstSnapshot);

bool replaceRefusalObserved = false;
try
{
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(
        normalRequest with { ReplaceExistingCandidate = false });
}
catch (IOException ex) when (
    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
{
    replaceRefusalObserved = true;
}
Require(replaceRefusalObserved, "The terrain add/remove writer did not refuse an unapproved existing-directory replacement.");
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
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest with
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
Require(backupFailureObserved, "The terrain add/remove writer did not expose its post-backup rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Post-backup rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool rollbackFailureObserved = false;
try
{
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest with
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
Require(rollbackFailureObserved, "The terrain add/remove writer did not expose its post-publication rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Full-directory rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool incompleteRollbackObserved = false;
try
{
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest with
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
Require(incompleteRollbackObserved, "The terrain add/remove writer did not preserve an injected incomplete rollback.");
Require(
    Directory.Exists(first.Paths.OperationsDirectoryPath),
    "The terrain add/remove writer removed the operation needed for restart recovery.");
string[] recoveryOperations = Directory.GetDirectories(first.Paths.OperationsDirectoryPath);
Require(recoveryOperations.Length == 1, "Incomplete foundation rollback retained an unexpected operation count.");
Require(
    Directory.GetFiles(recoveryOperations[0], "operation-journal.json", SearchOption.AllDirectories).Length == 1 &&
    Directory.GetDirectories(recoveryOperations[0], "previous-candidate", SearchOption.AllDirectories).Length == 1,
    "Incomplete foundation rollback did not retain its journal and prior full-directory backup.");

UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult recovered =
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(recovered, requirePinnedOutput: false, expectRecoveredOperation: true);
Require(!File.Exists(rollbackMarkerPath), "Restart recovery/final publication retained the rollback marker.");
RequireNoPublicationDebris(recovered.Paths);

UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult repeat =
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(normalRequest);
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
    "The terrain add/remove writer did not reproduce its exact candidate/diff boundary.");
UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult durableReadback =
    await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.VerifyPublishedAsync(
        outputDirectoryPath);
await VerifyResultAsync(durableReadback, requirePinnedOutput: true, expectRecoveredOperation: false);
await VerifyDurableSidecarTamperRejectionsAsync(repeat);
await VerifyDurableReceiptTamperRejectionsAsync(repeat);
RequireSnapshotsEqual(
    repeatSnapshot,
    SnapshotDirectory(outputDirectoryPath),
    "Durable sidecar tamper exact-byte restoration");
Require(
    await HashFilesAsync(protectedIntegrationPaths) is { } protectedAfter &&
    protectedBefore.OrderBy(pair => pair.Key).SequenceEqual(protectedAfter.OrderBy(pair => pair.Key)),
    "The disposable terrain add/remove writer changed a protected base/integration file.");
RequireNoPublicationDebris(repeat.Paths);

Console.WriteLine(
    "PASS UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateSmoke: deterministic full-directory publication, " +
    "pre-publication failure/cancellation cleanup, journaled rollback/restart recovery, canonical receipt/sidecar " +
    "tamper rejection, exact static/readback handoff, and no normal integration passed.");
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
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult result,
    bool requirePinnedOutput,
    bool expectRecoveredOperation)
{
    string expectedPrefix = Path.Combine(
        outputDirectoryPath,
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.OutputPrefix);
    Require(
        result.Paths.OutputDirectoryPath == Path.GetFullPath(outputDirectoryPath) &&
        result.Paths.OutputPrefix == expectedPrefix &&
        result.Paths.OutputImagePath == expectedPrefix + ".bin" &&
        result.Paths.OutputCuePath == expectedPrefix + ".cue" &&
        result.Paths.FoundationCompositionPlanPath == expectedPrefix + "-terrain-add-remove-plan.json" &&
        result.Paths.StaticReadbackReceiptPath == expectedPrefix + "-static-readback-receipt.json" &&
        result.Paths.RuntimeChecklistPath == expectedPrefix + "-runtime-checklist.md" &&
        result.Paths.LocationGuidePath == expectedPrefix + "-location-guide.svg" &&
        result.Paths.FinderHelperPath == expectedPrefix + "-Reveal-in-Finder.command",
        "The terrain add/remove writer artifact paths drifted from the approved isolated profile.");
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
    Require(requiredFiles.All(File.Exists), "The terrain add/remove writer omitted a required candidate/handoff artifact.");
    Require(
        Directory.GetFiles(outputDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(requiredFiles.Select(Path.GetFullPath).Order(StringComparer.Ordinal)),
        "The final terrain add/remove candidate directory contains an unexpected top-level artifact.");
    ValidateCuePair(result.Paths.OutputCuePath, result.Paths.OutputImagePath);

    Require(
        result.Plan.SchemaVersion == 1 &&
        result.Plan.ProfileId == UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId &&
        result.Plan.ComposerProfileId == UnusedLevel65FoundationTerrainComposer.ProfileId &&
        result.Plan.BaseImageSha256 == ExpectedBaseImageSha256 &&
        result.Plan.ModelPreimageSha256 == ExpectedInverseModelSha256 &&
        result.Plan.AuthoredModelSha256 == ExpectedAuthoredModelSha256 &&
        result.Plan.AuthoredCollisionSha256 == ExpectedAuthoredCollisionSha256 &&
        result.Plan.AuthoredCollisionTreeSha256 == ExpectedAuthoredCollisionTreeSha256 &&
        result.Plan.AuthoredCollisionBlocksSha256 == ExpectedAuthoredCollisionBlocksSha256 &&
        result.Plan.ExactInverseModelSha256 == ExpectedInverseModelSha256 &&
        result.Plan.ComposerPlanSha256 == ExpectedComposerPlanSha256 &&
        result.Plan.WadLba == 37 &&
        result.Plan.ModelSubfileWadOffset == 0x6A15000 &&
        result.Plan.ModelSubfileByteLength == 0x94800 &&
        result.Plan.TargetSectorIndex == 213 &&
        result.Plan.AddedLowDetailVertexCount == 1 &&
        result.Plan.AddedLowDetailFaceCount == 1 &&
        result.Plan.AddedHighDetailVertexCount == 1 &&
        result.Plan.AddedHighDetailFaceCount == 1 &&
        result.Plan.CollisionTriangleIndex == 19_808 &&
        result.Plan.NativeCollisionCellCount == 4_252 &&
        result.Plan.AuthoredCollisionCellCount == 4_252 &&
        result.Plan.ChangedCollisionCells.SequenceEqual(new[]
        {
            new UnusedLevel65FoundationCollisionCell(30, 24, 2),
            new UnusedLevel65FoundationCollisionCell(30, 25, 2),
            new UnusedLevel65FoundationCollisionCell(31, 25, 2)
        }) &&
        result.Plan.AtomicRejectMatrixCount == 17 &&
        result.Plan.ExactInverseVerified &&
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
        result.Receipt.ExactInverseModelSha256 == ExpectedInverseModelSha256 &&
        result.Receipt.ComposerPlanSha256 == ExpectedComposerPlanSha256 &&
        result.Receipt.OutputCueSha256 == ExpectedCueSha256 &&
        result.Receipt.TerrainPlanSha256 == ExpectedPlanFileSha256 &&
        result.Receipt.RuntimeChecklistSha256 == ExpectedChecklistSha256 &&
        result.Receipt.LocationGuideSha256 == ExpectedGuideSha256 &&
        result.Receipt.FinderHelperSha256 == ExpectedFinderHelperSha256 &&
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
        result.ExactInverseVerified &&
        result.AtomicRejectMatrixVerified &&
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
        result.Receipt.ExactInverseVerified &&
        result.Receipt.AtomicRejectMatrixVerified &&
        result.Receipt.OcclusionOwnershipVerified &&
        result.Receipt.ProtectedSubfilesPreserved &&
        result.Receipt.SpawnAndPlayerAnchorPreserved &&
        result.Receipt.CycloramaPreserved &&
        result.Receipt.DisplayNamePreserved &&
        result.Receipt.RawSectorIntegrityVerified &&
        result.Receipt.BaseCandidatePreserved &&
        result.Receipt.FullDirectoryPublicationVerified &&
        !result.Receipt.RollbackRecoveryVerified &&
        !result.Receipt.FullDirectoryRollbackVerified &&
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

void VerifyRawSectorBoundary(UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult result)
{
    Require(
        result.RebuiltRawSectorCount == result.ChangedRawSectorCount &&
        result.ChangedRawSectorCount == result.RawSectorDiffs.Count &&
        result.RawSectorDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(
            result.Plan.AffectedRawSectorLbas) &&
        (expectedChangedRawSectorLbas.Length == 0 ||
         result.Plan.AffectedRawSectorLbas.SequenceEqual(expectedChangedRawSectorLbas)) &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(
            result.Plan.AffectedRawSectorLbas.Distinct().Order()) &&
        result.RawSectorDiffs.Sum(diff => (long)diff.TotalChangedBytes) == result.ChangedPhysicalImageBytes &&
        (ExpectedRawSectorDiffSha256.Length == 0 ||
         HashRawSectorDiffs(result.RawSectorDiffs) == ExpectedRawSectorDiffSha256),
        "The foundation raw-sector count/LBA/physical-byte boundary is inconsistent.");
    foreach (UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff diff in result.RawSectorDiffs)
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
    IReadOnlyList<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff> diffs)
{
    StringBuilder builder = new();
    foreach (UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRawSectorDiff diff in diffs)
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
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest request)
{
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths =
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreatePaths(
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
            Path.GetFileName(path).StartsWith("terrain-add-remove-candidate-", StringComparison.Ordinal))
        .ToArray();
    if (operations.Length == 0)
    {
        Require(topLevelEntries.Length == 0, "The foundation operations parent contains a non-owned entry.");
        return;
    }
    if (topLevelEntries.Length == 1 && operations.Length == 1 &&
        Directory.Exists(paths.OutputDirectoryPath))
    {
        string journalPath = Path.Combine(operations[0], "operation-journal.json");
        Require(File.Exists(journalPath), "The single stale terrain operation is missing its journal.");
        using JsonDocument journal = JsonDocument.Parse(await File.ReadAllTextAsync(journalPath));
        string? stalePhase = journal.RootElement.GetProperty("phase").GetString();
        Require(
            stalePhase is "staging" or "new-candidate-removed" &&
            journal.RootElement.GetProperty("hadPreviousCandidate").GetBoolean() &&
            Directory.Exists(Path.Combine(operations[0], "candidate")) &&
            !Directory.Exists(Path.Combine(operations[0], "previous-candidate")),
            "The single stale terrain operation is not the exact pre-publication staging window.");
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult recoveredStaging =
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(request);
        Require(
            recoveredStaging.RollbackRecoveryVerified &&
            recoveredStaging.FullDirectoryRollbackVerified,
            "The exact pre-publication staging window was not recovered before clean smoke execution.");
        RequireNoPublicationDebris(recoveredStaging.Paths);
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
        await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(request with
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

async Task VerifyPrePublicationRollbackAsync(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest request,
    IReadOnlyDictionary<string, string> expectedPublishedSnapshot)
{
    const string stageName = "before-staged-candidate-build";
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths publishedPaths =
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreatePaths(
            request.OutputDirectoryPath);

    bool existingFailureObserved = false;
    try
    {
        await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(request with
        {
            TestStageHook = stage =>
            {
                if (stage == stageName)
                    throw new IOException("Injected existing-output pre-publication staging failure.");
            }
        });
    }
    catch (IOException ex) when (
        ex.ToString().Contains(
            "Injected existing-output pre-publication staging failure",
            StringComparison.Ordinal))
    {
        existingFailureObserved = true;
    }
    Require(existingFailureObserved, "The existing-output pre-publication failure hook was not observed.");
    RequireSnapshotsEqual(
        expectedPublishedSnapshot,
        SnapshotDirectory(publishedPaths.OutputDirectoryPath),
        "Existing-output pre-publication failure rollback");
    RequireNoPublicationDebris(publishedPaths);

    using (CancellationTokenSource cancellation = new())
    {
        bool existingCancellationStageReached = false;
        bool existingCancellationObserved = false;
        try
        {
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(
                request with
                {
                    TestStageHook = stage =>
                    {
                        if (stage != stageName)
                            return;
                        existingCancellationStageReached = true;
                        cancellation.Cancel();
                    }
                },
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            existingCancellationObserved = true;
        }
        Require(
            existingCancellationStageReached && existingCancellationObserved,
            "The existing-output pre-publication cancellation was not observed at the owned staging window.");
    }
    RequireSnapshotsEqual(
        expectedPublishedSnapshot,
        SnapshotDirectory(publishedPaths.OutputDirectoryPath),
        "Existing-output pre-publication cancellation rollback");
    RequireNoPublicationDebris(publishedPaths);

    string sandbox = Path.Combine(
        Path.GetTempPath(),
        $"spyro-id65-foundation-prepublication-{Guid.NewGuid():N}");
    string isolatedOutput = Path.Combine(
        sandbox,
        "unused-level-65-foundation-terrain-add-remove-second-tile");
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest noPreviousRequest = new(
        baseImagePath,
        baseCuePath,
        isolatedOutput,
        ReplaceExistingCandidate: false,
        RequestFinderReveal: false);
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths isolatedPaths =
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreatePaths(isolatedOutput);
    try
    {
        bool noPreviousFailureObserved = false;
        try
        {
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(
                noPreviousRequest with
                {
                    TestStageHook = stage =>
                    {
                        if (stage == stageName)
                            throw new IOException("Injected no-previous pre-publication staging failure.");
                    }
                });
        }
        catch (IOException ex) when (
            ex.ToString().Contains(
                "Injected no-previous pre-publication staging failure",
                StringComparison.Ordinal))
        {
            noPreviousFailureObserved = true;
        }
        Require(noPreviousFailureObserved, "The no-previous pre-publication failure hook was not observed.");
        Require(
            !Directory.Exists(isolatedPaths.OutputDirectoryPath),
            "The no-previous pre-publication failure published an output directory.");
        RequireNoPublicationDebris(isolatedPaths);

        using CancellationTokenSource cancellation = new();
        bool noPreviousCancellationStageReached = false;
        bool noPreviousCancellationObserved = false;
        try
        {
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(
                noPreviousRequest with
                {
                    TestStageHook = stage =>
                    {
                        if (stage != stageName)
                            return;
                        noPreviousCancellationStageReached = true;
                        cancellation.Cancel();
                    }
                },
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            noPreviousCancellationObserved = true;
        }
        Require(
            noPreviousCancellationStageReached && noPreviousCancellationObserved,
            "The no-previous pre-publication cancellation was not observed at the owned staging window.");
        Require(
            !Directory.Exists(isolatedPaths.OutputDirectoryPath),
            "The no-previous pre-publication cancellation published an output directory.");
        RequireNoPublicationDebris(isolatedPaths);
    }
    finally
    {
        if (Directory.Exists(sandbox))
            Directory.Delete(sandbox, recursive: true);
    }
    Require(!Directory.Exists(sandbox), "The pre-publication rollback smoke left its isolated sandbox.");
}

async Task VerifyConcurrentWriterRefusalAsync(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateRequest request,
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult reference,
    IReadOnlyDictionary<string, string> expectedSnapshot)
{
    TaskCompletionSource<bool> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult> active =
        Task.Run(async () =>
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(request with
            {
                TestStageHook = stage =>
                {
                    if (stage != "after-global-writer-lease-acquired")
                        return;
                    held.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                }
            }));
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult? completed = null;
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
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(request)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (IOException ex) when (
            ex.ToString().Contains(
                "Another terrain add/remove runtime candidate writer is active",
                StringComparison.Ordinal))
        {
            contentionRefused = true;
        }
        Require(contentionRefused, "A concurrent terrain add/remove writer was not refused by the per-output lease.");
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

    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult completedResult = completed ??
        throw new InvalidOperationException(
            "The serialized terrain add/remove writer did not complete after lease release.");
    await VerifyResultAsync(completedResult, requirePinnedOutput: false, expectRecoveredOperation: false);
    RequireSnapshotsEqual(expectedSnapshot, SnapshotDirectory(outputDirectoryPath), "Serialized writer contention");
    RequireNoPublicationDebris(completedResult.Paths);
}

async Task VerifyNoPreviousPublishedCandidateRecoveryAsync(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult reference)
{
    string sandbox = Path.Combine(
        Path.GetTempPath(),
        $"spyro-id65-foundation-no-previous-{Guid.NewGuid():N}");
    string isolatedOutput = Path.Combine(
        sandbox,
        "unused-level-65-foundation-terrain-add-remove-second-tile");
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths =
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreatePaths(isolatedOutput);
    try
    {
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult stagedSeed =
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(new(
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
            $"terrain-add-remove-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        string backupDirectory = Path.Combine(operationRoot, "previous-candidate");
        Directory.Move(paths.OutputDirectoryPath, stagedDirectory);
        await File.WriteAllBytesAsync(Path.Combine(operationRoot, "operation.lease"), []);
        string journal = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                operationKind = "unused-level-65-foundation-terrain-add-remove-runtime-candidate",
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

        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult recoveredNoPrevious =
            await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.CreateAsync(new(
                baseImagePath,
                baseCuePath,
                paths.OutputDirectoryPath,
                ReplaceExistingCandidate: false,
                RequestFinderReveal: false));
        Require(
            recoveredNoPrevious.RollbackRecoveryVerified &&
            recoveredNoPrevious.FullDirectoryRollbackVerified &&
            !recoveredNoPrevious.Receipt.RollbackRecoveryVerified &&
            !recoveredNoPrevious.Receipt.FullDirectoryRollbackVerified &&
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

async Task VerifyDurableSidecarTamperRejectionsAsync(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult reference)
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException("The frozen Finder-helper tamper gate requires macOS modes.");
    (string Label, string Path, string OriginalText, string TamperedText)[] cases =
    [
        (
            "CUE",
            reference.Paths.OutputCuePath,
            "TRACK 01 MODE2/2352",
            "TRACK 01 NODE2/2352"),
        (
            "terrain plan",
            reference.Paths.FoundationCompositionPlanPath,
            "\"composerProfileId\"",
            "\"xomposerProfileId\""),
        (
            "runtime checklist",
            reference.Paths.RuntimeChecklistPath,
            "camera depth >=2780",
            "camera depth <=2780"),
        (
            "location guide",
            reference.Paths.LocationGuidePath,
            "camera depth &gt;=2780",
            "camera depth &lt;=2780"),
        (
            "Finder helper",
            reference.Paths.FinderHelperPath,
            "/usr/bin/open -R",
            "/usr/bin/open -X")
    ];

    foreach ((string label, string path, string originalText, string tamperedText) in cases)
    {
        Require(
            originalText.Length == tamperedText.Length,
            $"The {label} tamper fixture must preserve text length.");
        string exactPath = Path.GetFullPath(path);
        byte[] originalBytes = await File.ReadAllBytesAsync(exactPath);
        string original = Encoding.UTF8.GetString(originalBytes);
        int first = original.IndexOf(originalText, StringComparison.Ordinal);
        Require(
            first >= 0 &&
            first == original.LastIndexOf(originalText, StringComparison.Ordinal),
            $"The {label} tamper fixture does not own one exact text occurrence.");
        string tampered = original[..first] + tamperedText + original[(first + originalText.Length)..];
        byte[] tamperedBytes = Encoding.UTF8.GetBytes(tampered);
        Require(
            tamperedBytes.Length == originalBytes.Length &&
            !tamperedBytes.SequenceEqual(originalBytes),
            $"The {label} tamper fixture did not produce one same-length content change.");
        UnixFileMode originalMode = File.GetUnixFileMode(exactPath);

        bool rejected = false;
        try
        {
            await File.WriteAllBytesAsync(exactPath, tamperedBytes);
            File.SetUnixFileMode(exactPath, originalMode);
            FlushTamperedFile(exactPath);
            Require(
                Path.GetFullPath(path) == exactPath &&
                File.GetUnixFileMode(exactPath) == originalMode,
                $"The {label} tamper changed its path or mode.");
            try
            {
                _ = await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter
                    .VerifyPublishedAsync(outputDirectoryPath);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
        }
        finally
        {
            await File.WriteAllBytesAsync(exactPath, originalBytes);
            File.SetUnixFileMode(exactPath, originalMode);
            FlushTamperedFile(exactPath);
        }

        Require(rejected, $"Durable reread accepted tampered {label} content.");
        Require(
            (await File.ReadAllBytesAsync(exactPath)).SequenceEqual(originalBytes) &&
            File.GetUnixFileMode(exactPath) == originalMode,
            $"The {label} tamper fixture did not restore exact bytes and mode.");
        _ = await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter
            .VerifyPublishedAsync(outputDirectoryPath);
    }

    static void FlushTamperedFile(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Flush(flushToDisk: true);
    }
}

async Task VerifyDurableReceiptTamperRejectionsAsync(
    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult reference)
{
    string receiptPath = Path.GetFullPath(reference.Paths.StaticReadbackReceiptPath);
    byte[] originalBytes = await File.ReadAllBytesAsync(receiptPath);
    string original = Encoding.UTF8.GetString(originalBytes);
    UnixFileMode originalMode = File.GetUnixFileMode(receiptPath);
    Require(
        await HashFileAsync(receiptPath) == ExpectedReceiptFileSha256 &&
        !reference.Receipt.RollbackRecoveryVerified &&
        !reference.Receipt.FullDirectoryRollbackVerified,
        "The durable receipt tamper matrix did not start from the exact clean frozen receipt.");

    string coordinatedRecoveryForgery = ReplaceExactlyOnce(
        ReplaceExactlyOnce(
            original,
            "  \"rollbackRecoveryVerified\": false,",
            "  \"rollbackRecoveryVerified\": true,"),
        "  \"fullDirectoryRollbackVerified\": false,",
        "  \"fullDirectoryRollbackVerified\": true,");
    string unknownProperty = ReplaceExactlyOnce(
        original,
        "  \"normalCreateBinEnabled\": false\n}",
        "  \"normalCreateBinEnabled\": false,\n  \"auditUnknownProperty\": true\n}");
    string whitespaceMutation = original + "\n";
    string canonicalPrefix =
        "{\n" +
        "  \"schemaVersion\": 1,\n" +
        $"  \"profileId\": \"{UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId}\",\n";
    string reorderedPrefix =
        "{\n" +
        $"  \"profileId\": \"{UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId}\",\n" +
        "  \"schemaVersion\": 1,\n";
    string propertyOrderDrift = ReplaceExactlyOnce(original, canonicalPrefix, reorderedPrefix);
    string profileLine =
        $"  \"profileId\": \"{UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId}\",";
    string mutatedProfileLine = "  \"profileId\": \"x" +
        UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId[1..] + "\",";
    string singleByteMutation = ReplaceExactlyOnce(original, profileLine, mutatedProfileLine);

    (string Label, string Bytes)[] cases =
    [
        ("coordinated recovery-flag forgery", coordinatedRecoveryForgery),
        ("unknown receipt property", unknownProperty),
        ("receipt whitespace mutation", whitespaceMutation),
        ("receipt property-order drift", propertyOrderDrift),
        ("receipt single-byte field mutation", singleByteMutation)
    ];

    foreach ((string label, string tampered) in cases)
    {
        byte[] tamperedBytes = Encoding.UTF8.GetBytes(tampered);
        Require(
            !tamperedBytes.SequenceEqual(originalBytes),
            $"The {label} fixture did not change exact receipt bytes.");
        bool rejected = false;
        try
        {
            await File.WriteAllBytesAsync(receiptPath, tamperedBytes);
            File.SetUnixFileMode(receiptPath, originalMode);
            FlushReceipt(receiptPath);
            try
            {
                _ = await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter
                    .VerifyPublishedAsync(outputDirectoryPath);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
        }
        finally
        {
            await File.WriteAllBytesAsync(receiptPath, originalBytes);
            File.SetUnixFileMode(receiptPath, originalMode);
            FlushReceipt(receiptPath);
        }

        Require(rejected, $"Durable reread accepted the {label}.");
        Require(
            (await File.ReadAllBytesAsync(receiptPath)).SequenceEqual(originalBytes) &&
            File.GetUnixFileMode(receiptPath) == originalMode &&
            await HashFileAsync(receiptPath) == ExpectedReceiptFileSha256,
            $"The {label} fixture did not restore exact frozen receipt bytes and mode.");
    }

    UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult restored =
        await UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter
            .VerifyPublishedAsync(outputDirectoryPath);
    Require(
        !restored.Receipt.RollbackRecoveryVerified &&
        !restored.Receipt.FullDirectoryRollbackVerified,
        "The restored canonical receipt did not preserve exact false recovery-history flags.");

    static string ReplaceExactlyOnce(string input, string before, string after)
    {
        int first = input.IndexOf(before, StringComparison.Ordinal);
        if (first < 0 || first != input.LastIndexOf(before, StringComparison.Ordinal))
            throw new InvalidOperationException("A receipt tamper fixture did not own one exact source occurrence.");
        return input[..first] + after + input[(first + before.Length)..];
    }

    static void FlushReceipt(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Flush(flushToDisk: true);
    }
}

async Task VerifySidecarsAsync(UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateResult result)
{
    Require(
        await HashFileAsync(result.Paths.OutputCuePath) == ExpectedCueSha256 &&
        await HashFileAsync(result.Paths.FoundationCompositionPlanPath) == ExpectedPlanFileSha256 &&
        await HashFileAsync(result.Paths.StaticReadbackReceiptPath) == ExpectedReceiptFileSha256 &&
        await HashFileAsync(result.Paths.RuntimeChecklistPath) == ExpectedChecklistSha256 &&
        await HashFileAsync(result.Paths.LocationGuidePath) == ExpectedGuideSha256 &&
        await HashFileAsync(result.Paths.FinderHelperPath) == ExpectedFinderHelperSha256,
        "A frozen terrain handoff sidecar hash changed.");
    using JsonDocument planDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.FoundationCompositionPlanPath));
    using JsonDocument receiptDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.StaticReadbackReceiptPath));
    JsonElement serializedPlan = planDocument.RootElement;
    JsonElement serializedReceipt = receiptDocument.RootElement;
    Require(
        serializedPlan.GetProperty("profileId").GetString() ==
            UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId &&
        serializedPlan.GetProperty("authoredModelSha256").GetString() == ExpectedAuthoredModelSha256 &&
        serializedPlan.GetProperty("authoredCollisionSha256").GetString() == ExpectedAuthoredCollisionSha256 &&
        serializedPlan.GetProperty("exactInverseModelSha256").GetString() == ExpectedInverseModelSha256 &&
        serializedPlan.GetProperty("composerPlanSha256").GetString() == ExpectedComposerPlanSha256 &&
        serializedPlan.GetProperty("atomicRejectMatrixCount").GetInt32() == 17 &&
        serializedPlan.GetProperty("exactInverseVerified").GetBoolean() &&
        serializedPlan.GetProperty("nativeCollisionCellCount").GetInt32() == 4_252 &&
        serializedPlan.GetProperty("authoredCollisionCellCount").GetInt32() == 4_252 &&
        serializedPlan.GetProperty("changedCollisionCells").GetArrayLength() == 3 &&
        serializedPlan.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        !serializedPlan.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedPlan.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The foundation composition-plan JSON lost its exact static-only contract.");
    Require(
        serializedReceipt.GetProperty("profileId").GetString() ==
            UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidateExporter.ProfileId &&
        serializedReceipt.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        serializedReceipt.GetProperty("outputDataSha256").GetString() == result.OutputDataSha256 &&
        serializedReceipt.GetProperty("outputCueSha256").GetString() == ExpectedCueSha256 &&
        serializedReceipt.GetProperty("terrainPlanSha256").GetString() == ExpectedPlanFileSha256 &&
        serializedReceipt.GetProperty("runtimeChecklistSha256").GetString() == ExpectedChecklistSha256 &&
        serializedReceipt.GetProperty("locationGuideSha256").GetString() == ExpectedGuideSha256 &&
        serializedReceipt.GetProperty("finderHelperSha256").GetString() == ExpectedFinderHelperSha256 &&
        serializedReceipt.GetProperty("rawSectorDiffs").GetArrayLength() == result.RawSectorDiffs.Count &&
        serializedReceipt.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        serializedReceipt.GetProperty("exactInverseVerified").GetBoolean() &&
        serializedReceipt.GetProperty("atomicRejectMatrixVerified").GetBoolean() &&
        serializedReceipt.GetProperty("baseCandidatePreserved").GetBoolean() &&
        !serializedReceipt.GetProperty("rollbackRecoveryVerified").GetBoolean() &&
        !serializedReceipt.GetProperty("fullDirectoryRollbackVerified").GetBoolean() &&
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
        checklist.Contains("Close HP — Tile 0", StringComparison.Ordinal) &&
        checklist.Contains("Close HP — Tile 1", StringComparison.Ordinal) &&
        checklist.Contains("Far LP — Tile 0", StringComparison.Ordinal) &&
        checklist.Contains("Far LP — Tile 1", StringComparison.Ordinal) &&
        checklist.Contains("camera depth >=2780", StringComparison.Ordinal) &&
        checklist.Contains("both LP footprints are visible simultaneously", StringComparison.Ordinal) &&
        checklist.Contains("Return near — both HP halves", StringComparison.Ordinal) &&
        checklist.Contains("Collision — Tile 0 half", StringComparison.Ordinal) &&
        checklist.Contains("Collision — Tile 1 half", StringComparison.Ordinal) &&
        checklist.Contains("Seam traversal", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 1 to **None**", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 2 to **None**", StringComparison.Ordinal) &&
        checklist.Contains("Cold boot repeat", StringComparison.Ordinal) &&
        checklist.Contains("runtime", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("not", StringComparison.OrdinalIgnoreCase) &&
        !checklist.Contains("until the low-detail switch", StringComparison.OrdinalIgnoreCase) &&
        result.LoadCodes.All(code => checklist.Contains(code.InputCode, StringComparison.Ordinal)),
        "The terrain checklist lost its per-half HP/LP/collision/seam/cold-boot/no-card boundary or four codes.");

    string svgText = await File.ReadAllTextAsync(result.Paths.LocationGuidePath);
    XDocument svg = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
    XElement svgRoot = svg.Root ?? throw new InvalidDataException("The foundation guide SVG has no root.");
    Require(
        svgRoot.Name.LocalName == "svg" &&
        svgRoot.Attribute("width")?.Value == "1200" &&
        svgRoot.Attribute("height")?.Value == "1000" &&
        svgText.Contains("HP", StringComparison.Ordinal) &&
        svgText.Contains("LP", StringComparison.Ordinal) &&
        svgText.Contains("Tile 0 LOCKED", StringComparison.Ordinal) &&
        svgText.Contains("Tile 1 REMOVABLE", StringComparison.Ordinal) &&
        svgText.Contains("B-C SEAM", StringComparison.Ordinal) &&
        svgText.Contains("Card 1 None + Card 2 None", StringComparison.Ordinal) &&
        svgText.Contains("camera depth &gt;=2780", StringComparison.Ordinal) &&
        svgText.Contains("confirm both LP footprints", StringComparison.Ordinal) &&
        svgText.Contains("Return near: re-confirm HP on both", StringComparison.Ordinal) &&
        !svgText.Contains("Back camera away; check far LP", StringComparison.Ordinal) &&
        svgText.Contains(result.OutputImageSha256, StringComparison.Ordinal) &&
        result.LoadCodes.All(code => svgText.Contains(code.InputCode, StringComparison.Ordinal)),
        "The terrain guide is malformed or omits its two-half HP/LP/seam/no-card/four-code handoff.");
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

void RequireNoPublicationDebris(UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
{
    Require(
        !Directory.Exists(paths.OperationsDirectoryPath),
        "The terrain add/remove writer retained operation/backup debris.");
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

void VerifyWriterLeaseReleased(UnusedLevel65FoundationTerrainAddRemoveRuntimeCandidatePaths paths)
{
    string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
        throw new InvalidOperationException("The foundation operations path has no parent.");
    string leasePath = Path.Combine(
        parent,
        ".unused-level-65-foundation-terrain-add-remove-writer.lease");
    Require(
        File.Exists(leasePath) &&
        !Directory.Exists(leasePath) &&
        (File.GetAttributes(leasePath) & FileAttributes.ReparsePoint) == 0 &&
        new FileInfo(leasePath).Length == 0,
        "The persistent terrain add/remove writer lease is missing, nonempty, or an unsafe path type.");
    using FileStream lease = new(
        leasePath,
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);
    Require(lease.Length == 0, "The released terrain add/remove writer lease changed during the native probe.");
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
        $"The persistent terrain add/remove writer lease remained natively locked (errno {lockError}).");
    Require(
        NativeLeaseProbe.Flock(fileDescriptor, NativeLeaseProbe.LockUnlock) == 0,
        "The foundation smoke could not release its native lease probe.");
}

string[] GetOwnedOperationDirectories(string operationsDirectoryPath) =>
    Directory.Exists(operationsDirectoryPath)
        ? Directory.GetDirectories(
            operationsDirectoryPath,
            "terrain-add-remove-candidate-*",
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
