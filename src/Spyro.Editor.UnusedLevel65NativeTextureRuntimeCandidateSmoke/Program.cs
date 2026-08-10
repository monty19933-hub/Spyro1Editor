using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Spyro.Editor.Core.Exporting;

const string ExpectedBaseImageSha256 =
    "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
const string ExpectedOutputTexturePagesSha256 =
    "34a6d81c740c1e2494ede138e45556941e6daf59d21668ca2b594055f61ac47a";
const string ExpectedOutputModelSha256 =
    "2c0cbdc33a902730760a957847e82be09d391fe2cc4bb7821c651bfb7ebf3d75";
const string ExpectedLogicalDiffManifestSha256 =
    "b42f7efdea04a2f6a3233b7ca657a247d2836292187ba78153872bee16abd068";
const string ExpectedDeterministicStaticPlanSha256 =
    "3557d1ee1b5874b764e0ce9742dd04b1281c6f5d762f59fce09f9a3d3da0315b";
const string ExpectedOutputImageSha256 =
    "776bc14042e2a740a107e5367fd350c1ce7f08b3753282369181bdee4dd79bf6";
const string ExpectedOutputDataSha256 =
    "de2e712372cc84207724ca4405776a0b65bd53a548adac3cc0c1f99e36aad9e9";
const string ExpectedRawSectorDiffSha256 =
    "64f8d44b275e118cca9925191c9fcd231dabb07b987b5472b41f1de33019d155";
const long ExpectedChangedLogicalWadBytes = 919_925;
const long ExpectedChangedPhysicalImageBytes = 1_073_071;
const int ExpectedRebuiltRawSectorCount = 550;
const int ExpectedChangedRawSectorCount = 550;
int[] expectedChangedRawSectorLbas =
[
    .. Enumerable.Range(53_907, 256),
    .. Enumerable.Range(54_351, 272),
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
    "unused-level-65-native-texture-t66-gnastys-world-t12");

Require(File.Exists(baseImagePath), $"Missing exact foundation base BIN: {baseImagePath}");
Require(File.Exists(baseCuePath), $"Missing exact foundation base CUE: {baseCuePath}");
Require(
    await HashFileAsync(baseImagePath) == ExpectedBaseImageSha256,
    "The native-texture writer base is not the exact passed ID65 foundation BIN.");
ValidateCuePair(baseCuePath, baseImagePath);
long baseImageLengthBefore = new FileInfo(baseImagePath).Length;
DateTime baseImageWriteBefore = File.GetLastWriteTimeUtc(baseImagePath);
long baseCueLengthBefore = new FileInfo(baseCuePath).Length;
DateTime baseCueWriteBefore = File.GetLastWriteTimeUtc(baseCuePath);

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

UnusedLevel65NativeTextureRuntimeCandidateRequest normalRequest = new(
    baseImagePath,
    baseCuePath,
    outputDirectoryPath,
    ReplaceExistingCandidate: true,
    RequestFinderReveal: true);
await RecoverCurrentOwnedCollisionIfPresentAsync(normalRequest);
UnusedLevel65NativeTextureRuntimeCandidateResult first =
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(first, requirePinnedOutput: false, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> firstSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireNoPublicationDebris(first.Paths);

bool replaceRefusalObserved = false;
try
{
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(
        normalRequest with { ReplaceExistingCandidate = false });
}
catch (IOException ex) when (
    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
{
    replaceRefusalObserved = true;
}
Require(replaceRefusalObserved, "The native-texture writer did not refuse an unapproved existing-directory replacement.");
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
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-previous-candidate-backup")
                throw new IOException("Injected native-texture post-backup publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected native-texture post-backup publication failure", StringComparison.Ordinal))
{
    backupFailureObserved = true;
}
Require(backupFailureObserved, "The native-texture writer did not expose its post-backup rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Post-backup rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool rollbackFailureObserved = false;
try
{
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected native-texture full-directory publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected native-texture full-directory publication failure", StringComparison.Ordinal))
{
    rollbackFailureObserved = true;
}
Require(rollbackFailureObserved, "The native-texture writer did not expose its post-publication rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Full-directory rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool incompleteRollbackObserved = false;
try
{
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected native-texture incomplete-rollback publication failure.");
            if (stage == "before-previous-candidate-restore")
                throw new IOException("Injected native-texture prior-directory restore failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected native-texture prior-directory restore failure", StringComparison.Ordinal))
{
    incompleteRollbackObserved = true;
}
Require(incompleteRollbackObserved, "The native-texture writer did not preserve an injected incomplete rollback.");
Require(
    Directory.Exists(first.Paths.OperationsDirectoryPath),
    "The native-texture writer removed the operation needed for restart recovery.");
string[] recoveryOperations = Directory.GetDirectories(first.Paths.OperationsDirectoryPath);
Require(recoveryOperations.Length == 1, "Incomplete native-texture rollback retained an unexpected operation count.");
Require(
    Directory.GetFiles(recoveryOperations[0], "operation-journal.json", SearchOption.AllDirectories).Length == 1 &&
    Directory.GetDirectories(recoveryOperations[0], "previous-candidate", SearchOption.AllDirectories).Length == 1,
    "Incomplete native-texture rollback did not retain its journal and prior full-directory backup.");

UnusedLevel65NativeTextureRuntimeCandidateResult recovered =
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(recovered, requirePinnedOutput: false, expectRecoveredOperation: true);
Require(!File.Exists(rollbackMarkerPath), "Restart recovery/final publication retained the rollback marker.");
RequireNoPublicationDebris(recovered.Paths);

UnusedLevel65NativeTextureRuntimeCandidateResult repeat =
    await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(repeat, requirePinnedOutput: true, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> repeatSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireSnapshotsEqual(firstSnapshot, repeatSnapshot, "Deterministic clean native-texture rerun");
Require(
    first.OutputImageSha256 == recovered.OutputImageSha256 &&
    recovered.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == recovered.OutputDataSha256 &&
    recovered.OutputDataSha256 == repeat.OutputDataSha256 &&
    first.ChangedLogicalWadBytes == repeat.ChangedLogicalWadBytes &&
    first.ChangedPhysicalImageBytes == repeat.ChangedPhysicalImageBytes &&
    first.RawSectorDiffs.SequenceEqual(repeat.RawSectorDiffs),
    "The native-texture writer did not reproduce its exact candidate/diff boundary.");
Require(
    await HashFilesAsync(protectedIntegrationPaths) is { } protectedAfter &&
    protectedBefore.OrderBy(pair => pair.Key).SequenceEqual(protectedAfter.OrderBy(pair => pair.Key)),
    "The disposable native-texture writer changed a protected base/integration file.");
Require(
    new FileInfo(baseImagePath).Length == baseImageLengthBefore &&
    File.GetLastWriteTimeUtc(baseImagePath) == baseImageWriteBefore &&
    new FileInfo(baseCuePath).Length == baseCueLengthBefore &&
    File.GetLastWriteTimeUtc(baseCuePath) == baseCueWriteBefore,
    "The native-texture writer changed a source BIN/CUE length or timestamp.");
RequireNoPublicationDebris(repeat.Paths);

Console.WriteLine(
    "PASS UnusedLevel65NativeTextureRuntimeCandidateSmoke: deterministic full-directory publication, " +
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
    UnusedLevel65NativeTextureRuntimeCandidateResult result,
    bool requirePinnedOutput,
    bool expectRecoveredOperation)
{
    string expectedPrefix = Path.Combine(
        outputDirectoryPath,
        UnusedLevel65NativeTextureRuntimeCandidateExporter.OutputPrefix);
    Require(
        result.Paths.OutputDirectoryPath == Path.GetFullPath(outputDirectoryPath) &&
        result.Paths.OutputPrefix == expectedPrefix &&
        result.Paths.OutputImagePath == expectedPrefix + ".bin" &&
        result.Paths.OutputCuePath == expectedPrefix + ".cue" &&
        result.Paths.TextureCompositionPlanPath == expectedPrefix + "-texture-composition-plan.json" &&
        result.Paths.StaticReadbackReceiptPath == expectedPrefix + "-static-readback-receipt.json" &&
        result.Paths.RuntimeChecklistPath == expectedPrefix + "-runtime-checklist.md" &&
        result.Paths.LocationGuidePath == expectedPrefix + "-location-guide.svg" &&
        result.Paths.FinderHelperPath == expectedPrefix + "-Reveal-in-Finder.command",
        "The native-texture writer artifact paths drifted from the approved isolated profile.");
    string[] requiredFiles =
    [
        result.Paths.OutputImagePath,
        result.Paths.OutputCuePath,
        result.Paths.TextureCompositionPlanPath,
        result.Paths.StaticReadbackReceiptPath,
        result.Paths.RuntimeChecklistPath,
        result.Paths.LocationGuidePath,
        result.Paths.FinderHelperPath
    ];
    Require(requiredFiles.All(File.Exists), "The native-texture writer omitted a required candidate/handoff artifact.");
    Require(
        Directory.GetFiles(outputDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(requiredFiles.Select(Path.GetFullPath).Order(StringComparer.Ordinal)),
        "The final native-texture candidate directory contains an unexpected top-level artifact.");
    ValidateCuePair(result.Paths.OutputCuePath, result.Paths.OutputImagePath);

    Require(
        result.Plan.SchemaVersion == 1 &&
        result.Plan.ProfileId == UnusedLevel65NativeTextureRuntimeCandidateExporter.ProfileId &&
        result.Plan.TextureAuthoringProfileId == UnusedLevel65NativeTextureAuthoringContract.ProfileId &&
        result.Plan.BaseImageSha256 == ExpectedBaseImageSha256 &&
        result.Plan.OutputImageSha256 == result.OutputImageSha256 &&
        result.Plan.SourceDataSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceDataSha256 &&
        result.Plan.OutputDataSha256 == ExpectedOutputDataSha256 &&
        result.Plan.SourceTexturePagesSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceTexturePagesSha256 &&
        result.Plan.OutputTexturePagesSha256 == ExpectedOutputTexturePagesSha256 &&
        result.Plan.SourceModelSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceModelSha256 &&
        result.Plan.OutputModelSha256 == ExpectedOutputModelSha256 &&
        result.Plan.DonorManifestId == UnusedLevel65NativeTextureAuthoringContract.DonorManifestId &&
        result.Plan.DonorCompleteRecordSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256 &&
        result.Plan.WadLba == 37 &&
        result.Plan.TargetWadEntry == 80 &&
        result.Plan.DataWadOffset == 0x6936800 && result.Plan.DataByteLength == 0x2E2000 &&
        result.Plan.TexturePagesWadOffset == 0x6937000 && result.Plan.TexturePagesByteLength == 0xDE000 &&
        result.Plan.ModelWadOffset == 0x6A15000 && result.Plan.ModelByteLength == 0x94800 &&
        result.Plan.SourceTextureCount == 66 && result.Plan.OutputTextureCount == 67 &&
        result.Plan.PrivateTextureId == 66 &&
        result.Plan.DonorWadEntry == 70 && result.Plan.DonorTextureId == 12 &&
        result.Plan.TargetSectorIndex == 213 &&
        result.Plan.TargetHighDetailFaceIndex == 113 && result.Plan.TargetLowDetailFaceIndex == 21 &&
        result.Plan.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        result.Plan.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes &&
        result.Plan.RebuiltRawSectorCount == result.RebuiltRawSectorCount &&
        result.Plan.ChangedRawSectorCount == result.ChangedRawSectorCount &&
        result.Plan.RawSectorDiffSha256 == result.RawSectorDiffSha256 &&
        result.Plan.LogicalDiffManifestSha256 == ExpectedLogicalDiffManifestSha256 &&
        result.Plan.DeterministicStaticPlanSha256 == ExpectedDeterministicStaticPlanSha256 &&
        result.Plan.DestinationOwnershipClosureVerified &&
        result.Plan.DonorDependencyClosureVerified &&
        result.Plan.PageAndClutRelocationVerified &&
        result.Plan.PrivateFaceBindingVerified &&
        result.Plan.HpLpPairingVerified &&
        result.Plan.CollisionAndOcclusionPreserved &&
        result.Plan.ProtectedSubfilesPreserved &&
        result.Plan.RequiresDuckStationRuntimeProof &&
        result.Plan.DisposableRuntimeCandidateAuthorized &&
        !result.Plan.PromotionAuthorized &&
        !result.Plan.NormalCreateBinEnabled,
        "The native-texture composition plan lost its exact donor/private-face/readback boundary.");
    VerifyLoadCodes(result.LoadCodes);
    Require(
        result.Plan.LoadCodes.SequenceEqual(result.LoadCodes),
        "The serialized native-texture plan and result disagree on the four test codes.");

    Require(
        result.OutputImageSha256 == await HashFileAsync(result.Paths.OutputImagePath) &&
        result.Receipt.OutputImageSha256 == result.OutputImageSha256 &&
        result.Receipt.OutputDataSha256 == result.OutputDataSha256 &&
        result.Receipt.SourceDataSha256 == UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceDataSha256 &&
        result.Receipt.OutputTexturePagesSha256 == ExpectedOutputTexturePagesSha256 &&
        result.Receipt.OutputModelSha256 == ExpectedOutputModelSha256 &&
        result.Receipt.LogicalDiffManifestSha256 == ExpectedLogicalDiffManifestSha256 &&
        result.Receipt.DeterministicStaticPlanSha256 == ExpectedDeterministicStaticPlanSha256 &&
        result.Receipt.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        result.Receipt.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes &&
        result.Receipt.RebuiltRawSectorCount == result.RebuiltRawSectorCount &&
        result.Receipt.ChangedRawSectorCount == result.ChangedRawSectorCount &&
        result.Receipt.RawSectorDiffs.SequenceEqual(result.RawSectorDiffs) &&
        result.Receipt.RawSectorDiffSha256 == result.RawSectorDiffSha256,
        "The native-texture static receipt does not bind the exact candidate/diff hashes.");
    Require(
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.TextureCompositionVerified &&
        result.TexturePagesReadbackVerified &&
        result.ModelReadbackVerified &&
        result.PrivateFaceBindingVerified &&
        result.HpLpPairingVerified &&
        result.CollisionAndOcclusionPreserved &&
        result.ProtectedSubfilesPreserved &&
        result.RawSectorIntegrityVerified &&
        result.BaseCandidatePreserved &&
        result.FullDirectoryPublicationVerified &&
        result.RollbackRecoveryVerified == expectRecoveredOperation &&
        result.FullDirectoryRollbackVerified == expectRecoveredOperation &&
        result.FinderHandoffVerified &&
        result.DisposableRuntimeCandidateAuthorized &&
        !result.PromotionAuthorized &&
        !result.NormalCreateBinEnabled,
        "The native-texture result weakened a composition/readback/preservation/publication guard.");
    Require(
        result.Receipt.ExactLogicalDiffBoundaryVerified &&
        result.Receipt.ExactPhysicalSectorBoundaryVerified &&
        result.Receipt.TextureCompositionVerified &&
        result.Receipt.TexturePagesReadbackVerified &&
        result.Receipt.ModelReadbackVerified &&
        result.Receipt.PrivateFaceBindingVerified &&
        result.Receipt.HpLpPairingVerified &&
        result.Receipt.CollisionAndOcclusionPreserved &&
        result.Receipt.ProtectedSubfilesPreserved &&
        result.Receipt.RawSectorIntegrityVerified &&
        result.Receipt.BaseCandidatePreserved &&
        result.Receipt.FullDirectoryPublicationVerified &&
        result.Receipt.RollbackRecoveryVerified == expectRecoveredOperation &&
        result.Receipt.FullDirectoryRollbackVerified == expectRecoveredOperation &&
        result.Receipt.FinderHandoffVerified &&
        result.Receipt.DisposableRuntimeCandidateAuthorized &&
        !result.Receipt.PromotionAuthorized &&
        !result.Receipt.NormalCreateBinEnabled,
        "The durable native-texture receipt weakened a static/readback/publication guard.");
    VerifyRawSectorBoundary(result);
    await VerifySidecarsAsync(result);

    if (requirePinnedOutput)
    {
        Require(
            (string.IsNullOrEmpty(ExpectedOutputImageSha256) || result.OutputImageSha256 == ExpectedOutputImageSha256) &&
            result.OutputDataSha256 == ExpectedOutputDataSha256 &&
            result.ChangedLogicalWadBytes == ExpectedChangedLogicalWadBytes &&
            (ExpectedChangedPhysicalImageBytes < 0 || result.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes) &&
            (ExpectedRebuiltRawSectorCount < 0 || result.RebuiltRawSectorCount == ExpectedRebuiltRawSectorCount) &&
            (ExpectedChangedRawSectorCount < 0 || result.ChangedRawSectorCount == ExpectedChangedRawSectorCount),
            "The exact native-texture output hash or logical/raw-sector diff pin drifted: " +
            $"BIN={result.OutputImageSha256}, data={result.OutputDataSha256}, " +
            $"logical={result.ChangedLogicalWadBytes}, physical={result.ChangedPhysicalImageBytes}, " +
            $"rebuilt={result.RebuiltRawSectorCount}, changed={result.ChangedRawSectorCount}.");
    }
}

void VerifyRawSectorBoundary(UnusedLevel65NativeTextureRuntimeCandidateResult result)
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
        HashRawSectorDiffs(result.RawSectorDiffs) == result.RawSectorDiffSha256 &&
        (string.IsNullOrEmpty(ExpectedRawSectorDiffSha256) ||
         result.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256),
        "The native-texture raw-sector count/LBA/physical-byte boundary is inconsistent.");
    foreach (UnusedLevel65NativeTextureRuntimeCandidateRawSectorDiff diff in result.RawSectorDiffs)
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
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
        .ToLowerInvariant();
}

async Task RecoverCurrentOwnedCollisionIfPresentAsync(
    UnusedLevel65NativeTextureRuntimeCandidateRequest request)
{
    UnusedLevel65NativeTextureRuntimeCandidatePaths paths =
        UnusedLevel65NativeTextureRuntimeCandidateExporter.CreatePaths(
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
            Path.GetFileName(path).StartsWith("native-texture-candidate-", StringComparison.Ordinal))
        .ToArray();
    if (operations.Length == 0)
    {
        Require(topLevelEntries.Length == 0, "The native-texture operations parent contains a non-owned entry.");
        return;
    }
    Require(
        topLevelEntries.Length == operations.Length &&
        operations.Length == 2 &&
        !Directory.Exists(paths.OutputDirectoryPath),
        "The preflight found stale native-texture state other than the exact observed two-operation collision.");
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
        await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(request with
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
    UnusedLevel65NativeTextureRuntimeCandidateRequest request,
    UnusedLevel65NativeTextureRuntimeCandidateResult reference,
    IReadOnlyDictionary<string, string> expectedSnapshot)
{
    TaskCompletionSource<bool> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<UnusedLevel65NativeTextureRuntimeCandidateResult> active =
        Task.Run(async () =>
            await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(request with
            {
                TestStageHook = stage =>
                {
                    if (stage != "after-global-writer-lease-acquired")
                        return;
                    held.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                }
            }));
    UnusedLevel65NativeTextureRuntimeCandidateResult? completed = null;
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
            await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(request)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (IOException ex) when (
            ex.ToString().Contains(
                "Another native-texture runtime candidate writer is active",
                StringComparison.Ordinal))
        {
            contentionRefused = true;
        }
        Require(contentionRefused, "A concurrent native-texture writer was not refused by the per-output lease.");
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

    UnusedLevel65NativeTextureRuntimeCandidateResult completedResult = completed ??
        throw new InvalidOperationException(
            "The serialized native-texture writer did not complete after lease release.");
    await VerifyResultAsync(completedResult, requirePinnedOutput: false, expectRecoveredOperation: false);
    RequireSnapshotsEqual(expectedSnapshot, SnapshotDirectory(outputDirectoryPath), "Serialized writer contention");
    RequireNoPublicationDebris(completedResult.Paths);
}

async Task VerifyNoPreviousPublishedCandidateRecoveryAsync(
    UnusedLevel65NativeTextureRuntimeCandidateResult reference)
{
    string sandbox = Path.Combine(
        Path.GetTempPath(),
        $"spyro-id65-native-texture-no-previous-{Guid.NewGuid():N}");
    string isolatedOutput = Path.Combine(sandbox, "candidate");
    UnusedLevel65NativeTextureRuntimeCandidatePaths paths =
        UnusedLevel65NativeTextureRuntimeCandidateExporter.CreatePaths(isolatedOutput);
    try
    {
        UnusedLevel65NativeTextureRuntimeCandidateResult stagedSeed =
            await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(new(
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
            $"native-texture-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        string backupDirectory = Path.Combine(operationRoot, "previous-candidate");
        Directory.Move(paths.OutputDirectoryPath, stagedDirectory);
        await File.WriteAllBytesAsync(Path.Combine(operationRoot, "operation.lease"), []);
        string journal = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                operationKind = "unused-level-65-native-texture-runtime-candidate",
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

        UnusedLevel65NativeTextureRuntimeCandidateResult recoveredNoPrevious =
            await UnusedLevel65NativeTextureRuntimeCandidateExporter.CreateAsync(new(
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

async Task VerifySidecarsAsync(UnusedLevel65NativeTextureRuntimeCandidateResult result)
{
    using JsonDocument planDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.TextureCompositionPlanPath));
    using JsonDocument receiptDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.StaticReadbackReceiptPath));
    JsonElement serializedPlan = planDocument.RootElement;
    JsonElement serializedReceipt = receiptDocument.RootElement;
    Require(
        serializedPlan.GetProperty("profileId").GetString() ==
            UnusedLevel65NativeTextureRuntimeCandidateExporter.ProfileId &&
        serializedPlan.GetProperty("textureAuthoringProfileId").GetString() ==
            UnusedLevel65NativeTextureAuthoringContract.ProfileId &&
        serializedPlan.GetProperty("outputTexturePagesSha256").GetString() == ExpectedOutputTexturePagesSha256 &&
        serializedPlan.GetProperty("outputModelSha256").GetString() == ExpectedOutputModelSha256 &&
        serializedPlan.GetProperty("logicalDiffManifestSha256").GetString() == ExpectedLogicalDiffManifestSha256 &&
        serializedPlan.GetProperty("privateTextureId").GetInt32() == 66 &&
        serializedPlan.GetProperty("targetHighDetailFaceIndex").GetInt32() == 113 &&
        serializedPlan.GetProperty("disposableRuntimeCandidateAuthorized").GetBoolean() &&
        serializedPlan.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        !serializedPlan.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedPlan.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The native-texture composition-plan JSON lost its exact static-only contract.");
    Require(
        serializedReceipt.GetProperty("profileId").GetString() ==
            UnusedLevel65NativeTextureRuntimeCandidateExporter.ProfileId &&
        serializedReceipt.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        serializedReceipt.GetProperty("outputDataSha256").GetString() == result.OutputDataSha256 &&
        serializedReceipt.GetProperty("rawSectorDiffs").GetArrayLength() == result.RawSectorDiffs.Count &&
        serializedReceipt.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        serializedReceipt.GetProperty("textureCompositionVerified").GetBoolean() &&
        serializedReceipt.GetProperty("privateFaceBindingVerified").GetBoolean() &&
        serializedReceipt.GetProperty("hpLpPairingVerified").GetBoolean() &&
        serializedReceipt.GetProperty("disposableRuntimeCandidateAuthorized").GetBoolean() &&
        serializedReceipt.GetProperty("baseCandidatePreserved").GetBoolean() &&
        !serializedReceipt.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedReceipt.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The native-texture static receipt JSON lost its exact readback/nonintegration contract.");

    RuntimeCandidateFinderReveal finder = result.FinderReveal ??
        throw new InvalidOperationException("The macOS native-texture handoff omitted Finder reveal metadata.");
    Require(
        finder.CuePath == result.Paths.OutputCuePath &&
        finder.PairedBinPath == result.Paths.OutputImagePath &&
        finder.HelperPath == result.Paths.FinderHelperPath &&
        finder.CuePairingVerified &&
        finder.HelperIsExecutable &&
        File.GetUnixFileMode(finder.HelperPath) == ExactFinderMode(),
        "The native-texture Finder helper lost exact CUE pairing or 0755 permissions.");
    string helper = await File.ReadAllTextAsync(finder.HelperPath);
    Require(
        helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal) &&
        helper.Contains(Path.GetFileName(result.Paths.OutputCuePath), StringComparison.Ordinal),
        "The native-texture Finder helper does not reveal its exact CUE.");

    string checklist = await File.ReadAllTextAsync(result.Paths.RuntimeChecklistPath);
    RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finder, result.LoadCodes);
    Require(
        checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) &&
        checklist.Contains("private T66", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("Close HP texture", StringComparison.Ordinal) &&
        checklist.Contains("Page/CLUT stability", StringComparison.Ordinal) &&
        checklist.Contains("Far LP transition", StringComparison.Ordinal) &&
        checklist.Contains("Near return", StringComparison.Ordinal) &&
        checklist.Contains("Solid surface preserved", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 1 = None", StringComparison.Ordinal) &&
        checklist.Contains("Memory Card 2 = None", StringComparison.Ordinal) &&
        checklist.Contains("do not save", StringComparison.OrdinalIgnoreCase) &&
        !checklist.Contains("disposable memory card", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("reset", StringComparison.OrdinalIgnoreCase) &&
        checklist.Contains("RUNTIME PENDING / UNPROMOTED", StringComparison.Ordinal) &&
        result.LoadCodes.All(code => checklist.Contains(code.InputCode, StringComparison.Ordinal)),
        "The native-texture checklist lost its near-HP/far-LP/page/CLUT/solidity/reset boundary or four codes.");

    string svgText = await File.ReadAllTextAsync(result.Paths.LocationGuidePath);
    XDocument svg = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
    XElement svgRoot = svg.Root ?? throw new InvalidDataException("The native-texture guide SVG has no root.");
    Require(
        svgRoot.Name.LocalName == "svg" &&
        svgRoot.Attribute("width")?.Value == "1200" &&
        svgRoot.Attribute("height")?.Value == "1000" &&
        svgText.Contains("HP", StringComparison.Ordinal) &&
        svgText.Contains("LP", StringComparison.Ordinal) &&
        svgText.Contains("private T66", StringComparison.OrdinalIgnoreCase) &&
        svgText.Contains("color-only", StringComparison.OrdinalIgnoreCase) &&
        svgText.Contains("Memory Cards 1/2=None", StringComparison.Ordinal) &&
        svgText.Contains("RUNTIME PENDING / UNPROMOTED", StringComparison.Ordinal) &&
        svgText.Contains("SPAWN", StringComparison.OrdinalIgnoreCase) &&
        result.LoadCodes.All(code => svgText.Contains(code.InputCode, StringComparison.Ordinal)),
        "The native-texture location guide is malformed or omits its HP/LP/T66/spawn/four-code handoff.");
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
                $"The native-texture guide text can clip: `{text}` bounds={left:F1}..{right:F1}, " +
                $"baseline/font={y:F1}/{fontSize:F1}.");
        }
    }
    Require(fragmentCount >= 15, "The native-texture guide lost its expected wrapped identity/route/code text.");
}

double ParseSvgNumber(XElement fragment, XElement parent, string attributeName)
{
    string? value = fragment.Attribute(attributeName)?.Value ?? parent.Attribute(attributeName)?.Value;
    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
        ? parsed
        : throw new InvalidDataException(
            $"The native-texture guide text omitted numeric `{attributeName}` positioning.");
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
    Require(loadCodes.Count == expected.Length, "The native-texture handoff must contain exactly four load codes.");
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
            $"The complete native-texture load code for {expected[index].Name} changed.");
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

void RequireNoPublicationDebris(UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
{
    Require(
        !Directory.Exists(paths.OperationsDirectoryPath),
        "The native-texture writer retained operation/backup debris.");
    string[] debris = Directory.Exists(paths.OutputDirectoryPath)
        ? Directory.GetFileSystemEntries(paths.OutputDirectoryPath, "*", SearchOption.AllDirectories)
            .Where(path =>
                Path.GetFileName(path).Contains(".tmp", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains(".bak", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains("previous-candidate", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("operation-journal.json", StringComparison.OrdinalIgnoreCase))
            .ToArray()
        : [];
    Require(debris.Length == 0, $"Native-texture publication debris remained: {string.Join(',', debris)}");
    VerifyWriterLeaseReleased(paths);
}

void VerifyWriterLeaseReleased(UnusedLevel65NativeTextureRuntimeCandidatePaths paths)
{
    string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
        throw new InvalidOperationException("The native-texture operations path has no parent.");
    string leasePath = Path.Combine(
        parent,
        ".unused-level-65-native-texture-writer.lease");
    Require(
        File.Exists(leasePath) &&
        !Directory.Exists(leasePath) &&
        (File.GetAttributes(leasePath) & FileAttributes.ReparsePoint) == 0 &&
        new FileInfo(leasePath).Length == 0,
        "The persistent native-texture writer lease is missing, nonempty, or an unsafe path type.");
    using FileStream lease = new(
        leasePath,
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);
    Require(lease.Length == 0, "The released native-texture writer lease changed during the native probe.");
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
        $"The persistent native-texture writer lease remained natively locked (errno {lockError}).");
    Require(
        NativeLeaseProbe.Flock(fileDescriptor, NativeLeaseProbe.LockUnlock) == 0,
        "The native-texture smoke could not release its native lease probe.");
}

string[] GetOwnedOperationDirectories(string operationsDirectoryPath) =>
    Directory.Exists(operationsDirectoryPath)
        ? Directory.GetDirectories(
            operationsDirectoryPath,
            "native-texture-candidate-*",
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
        Require(File.Exists(path), $"Protected native-texture path is missing: {path}");
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
