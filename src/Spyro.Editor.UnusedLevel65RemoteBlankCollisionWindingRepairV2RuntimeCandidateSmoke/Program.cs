using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Spyro.Editor.Core.Exporting;

const string ExpectedBaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string ExpectedSourceDataSha256 =
    "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
const string ExpectedSourceModelSha256 =
    "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
const string ExpectedOutputModelSha256 =
    "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79";
const string ExpectedOutputCollisionTreeSha256 =
    "c77618c5a80fce590c802b197945348d0b4fc04ab666e9b0b911e3a6ff9b2cb9";
const string ExpectedOutputCollisionBlocksSha256 =
    "2d5838ffd9c991d017733f48cadbb9739131fbd6cec75f69a831c03442c1f34a";
const string ExpectedOutputImageSha256 =
    "23ce483a29187996827148d986fb12f483fc2caa82fc953106882568ea9254a6";
const string ExpectedFailedV1OutputImageSha256 =
    "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e";
const string ExpectedOutputDataSha256 =
    "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9";
const string ExpectedRawSectorDiffSha256 =
    "902a27be55ddb0cdfea96bde6b5c309c8e6fa4f7b587c27d45cf4afab3642c91";
const string ExpectedFailedV1ToV2RawSectorDiffSha256 =
    "2c3ef040a02c736258c72efade3337472bb4c106dac049e7b6c616cbc7ccaff2";
const long ExpectedChangedLogicalWadBytes = 484_336;
const long ExpectedChangedPhysicalImageBytes = 564_799;
const int ExpectedRebuiltRawSectorCount = 291;
const int ExpectedChangedRawSectorCount = 291;
int[] expectedChangedRawSectorLbas =
[
    .. Enumerable.Range(54_356, 267),
    54_624,
    54_625,
    .. Enumerable.Range(54_628, 20),
    54_834,
    54_838
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
string failedV1Prefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-remote-blank-isolation",
    "Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE");
string failedV1ImagePath = failedV1Prefix + ".bin";
string failedV1CuePath = failedV1Prefix + ".cue";
string outputDirectoryPath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.OutputDirectoryName);

Require(File.Exists(baseImagePath), $"Missing exact display-name base BIN: {baseImagePath}");
Require(File.Exists(baseCuePath), $"Missing exact display-name base CUE: {baseCuePath}");
Require(File.Exists(failedV1ImagePath), $"Missing frozen failed-v1 BIN: {failedV1ImagePath}");
Require(File.Exists(failedV1CuePath), $"Missing frozen failed-v1 CUE: {failedV1CuePath}");
Require(
    await HashFileAsync(baseImagePath) == ExpectedBaseImageSha256,
    "The remote-blank writer base is not the exact passed ID65 display-name BIN.");
ValidateCuePair(baseCuePath, baseImagePath);
Require(
    await HashFileAsync(failedV1ImagePath) == ExpectedFailedV1OutputImageSha256,
    "The frozen failed-v1 remote-blank BIN changed.");
ValidateCuePair(failedV1CuePath, failedV1ImagePath);
UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan directRepairPlan =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(baseImagePath);
VerifyDirectRepairTransactionMatrix(directRepairPlan);

string[] protectedIntegrationPaths =
[
    Path.Combine(repositoryRoot, "spyro-level-catalog.json"),
    Path.Combine(repositoryRoot, "Directory.Build.props"),
    Path.Combine(repositoryRoot, "src", "Spyro.Editor.App", "Spyro.Editor.App.csproj"),
    Path.Combine(repositoryRoot, "src", "Spyro.Editor.App", "AppReleaseIdentity.cs"),
    baseImagePath,
    baseCuePath,
    failedV1ImagePath,
    failedV1CuePath
];
IReadOnlyDictionary<string, string> protectedBefore = await HashFilesAsync(protectedIntegrationPaths);

UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRequest normalRequest = new(
    baseImagePath,
    baseCuePath,
    outputDirectoryPath,
    ReplaceExistingCandidate: true,
    RequestFinderReveal: true);
await RecoverCurrentOwnedCollisionIfPresentAsync(normalRequest);
UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult first =
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(first, requirePinnedOutput: false, expectRecoveredOperation: false);
IReadOnlyDictionary<string, string> firstSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireNoPublicationDebris(first.Paths);

bool replaceRefusalObserved = false;
try
{
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(
        normalRequest with { ReplaceExistingCandidate = false });
}
catch (IOException ex) when (
    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
{
    replaceRefusalObserved = true;
}
Require(replaceRefusalObserved, "The remote-blank writer did not refuse an unapproved existing-directory replacement.");
RequireSnapshotsEqual(firstSnapshot, SnapshotDirectory(outputDirectoryPath), "Replacement refusal");
RequireNoPublicationDebris(first.Paths);

await VerifyPrePublicationAbortCleanupAsync(normalRequest, first, firstSnapshot);
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
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-previous-candidate-backup")
                throw new IOException("Injected remote-blank post-backup publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected remote-blank post-backup publication failure", StringComparison.Ordinal))
{
    backupFailureObserved = true;
}
Require(backupFailureObserved, "The remote-blank writer did not expose its post-backup rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Post-backup rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool rollbackFailureObserved = false;
try
{
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected remote-blank full-directory publication failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected remote-blank full-directory publication failure", StringComparison.Ordinal))
{
    rollbackFailureObserved = true;
}
Require(rollbackFailureObserved, "The remote-blank writer did not expose its post-publication rollback stage.");
RequireSnapshot(outputDirectoryPath, rollbackSnapshot);
Require(File.Exists(rollbackMarkerPath), "Full-directory rollback did not restore the prior marker.");
RequireNoPublicationDebris(first.Paths);

bool incompleteRollbackObserved = false;
try
{
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest with
    {
        TestStageHook = stage =>
        {
            if (stage == "after-candidate-publication")
                throw new IOException("Injected remote-blank incomplete-rollback publication failure.");
            if (stage == "before-previous-candidate-restore")
                throw new IOException("Injected remote-blank prior-directory restore failure.");
        }
    });
}
catch (IOException ex) when (
    ex.ToString().Contains("Injected remote-blank prior-directory restore failure", StringComparison.Ordinal))
{
    incompleteRollbackObserved = true;
}
Require(incompleteRollbackObserved, "The remote-blank writer did not preserve an injected incomplete rollback.");
Require(
    Directory.Exists(first.Paths.OperationsDirectoryPath),
    "The remote-blank writer removed the operation needed for restart recovery.");
string[] recoveryOperations = Directory.GetDirectories(first.Paths.OperationsDirectoryPath);
Require(recoveryOperations.Length == 1, "Incomplete remote-blank rollback retained an unexpected operation count.");
Require(
    Directory.GetFiles(recoveryOperations[0], "operation-journal.json", SearchOption.AllDirectories).Length == 1 &&
    Directory.GetDirectories(recoveryOperations[0], "previous-candidate", SearchOption.AllDirectories).Length == 1,
    "Incomplete remote-blank rollback did not retain its journal and prior full-directory backup.");

UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult recovered =
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(recovered, requirePinnedOutput: false, expectRecoveredOperation: true);
Require(!File.Exists(rollbackMarkerPath), "Restart recovery/final publication retained the rollback marker.");
RequireNoPublicationDebris(recovered.Paths);

UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult repeat =
    await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(normalRequest);
await VerifyResultAsync(repeat, requirePinnedOutput: true, expectRecoveredOperation: false);
await VerifyDurableSidecarBindingAsync(repeat);
IReadOnlyDictionary<string, string> repeatSnapshot = SnapshotDirectory(outputDirectoryPath);
RequireSnapshotsEqual(firstSnapshot, repeatSnapshot, "Deterministic clean remote-blank rerun");
IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff>
    failedV1ToV2RawDiffs = CompareRawImages(failedV1ImagePath, repeat.Paths.OutputImagePath);
Require(
    failedV1ToV2RawDiffs.Count == 1 &&
    failedV1ToV2RawDiffs[0] == new UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff(
        54_581,
        HeaderChangedBytes: 0,
        SubheaderChangedBytes: 0,
        PayloadChangedBytes: 4,
        EdcChangedBytes: 4,
        ReservedChangedBytes: 0,
        EccPChangedBytes: 16,
        EccQChangedBytes: 40,
        TotalChangedBytes: 64) &&
    HashRawSectorDiffs(failedV1ToV2RawDiffs) == ExpectedFailedV1ToV2RawSectorDiffSha256,
    "The v2 candidate differs from frozen failed v1 by more than exact raw sector LBA 54581.");
Require(
    first.OutputImageSha256 == recovered.OutputImageSha256 &&
    recovered.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == recovered.OutputDataSha256 &&
    recovered.OutputDataSha256 == repeat.OutputDataSha256 &&
    first.ChangedLogicalWadBytes == repeat.ChangedLogicalWadBytes &&
    first.ChangedPhysicalImageBytes == repeat.ChangedPhysicalImageBytes &&
    first.RawSectorDiffs.SequenceEqual(repeat.RawSectorDiffs),
    "The remote-blank writer did not reproduce its exact candidate/diff boundary.");
Require(
    await HashFilesAsync(protectedIntegrationPaths) is { } protectedAfter &&
    protectedBefore.OrderBy(pair => pair.Key).SequenceEqual(protectedAfter.OrderBy(pair => pair.Key)),
    "The disposable remote-blank writer changed a protected base/integration file.");
RequireNoPublicationDebris(repeat.Paths);

Console.WriteLine(
    "PASS UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateSmoke: deterministic full-directory publication, " +
    "journaled rollback/restart recovery, receipt-bound sidecar tamper rejection, exact static/readback handoff, " +
    "and no normal integration passed.");
Console.WriteLine($"CUE: {repeat.Paths.OutputCuePath}");
Console.WriteLine($"Reveal in Finder: {repeat.Paths.FinderHelperPath}");
Console.WriteLine($"Guide: {repeat.Paths.LocationGuidePath}");
Console.WriteLine($"Checklist: {repeat.Paths.RuntimeChecklistPath}");
Console.WriteLine($"Receipt: {repeat.Paths.StaticReadbackReceiptPath}");
Console.WriteLine($"Receipt SHA-256: {await HashFileAsync(repeat.Paths.StaticReadbackReceiptPath)}");
Console.WriteLine(
    $"Sidecar SHA-256: CUE={repeat.Receipt.OutputCueSha256}, plan={repeat.Receipt.ConstructionPlanSha256}, " +
    $"checklist={repeat.Receipt.RuntimeChecklistSha256}, guide={repeat.Receipt.LocationGuideSha256}, " +
    $"helper={repeat.Receipt.FinderHelperSha256}.");
Console.WriteLine($"BIN SHA-256: {repeat.OutputImageSha256}");
Console.WriteLine($"ID65 data SHA-256: {repeat.OutputDataSha256}");
Console.WriteLine(
    $"Failed-v1 → v2 raw delta: LBA {failedV1ToV2RawDiffs.Single().RawSectorLba}, " +
    $"bytes={failedV1ToV2RawDiffs.Single().TotalChangedBytes}, " +
    $"SHA-256={HashRawSectorDiffs(failedV1ToV2RawDiffs)}.");
Console.WriteLine(
    $"Logical/physical changed bytes: {repeat.ChangedLogicalWadBytes}/{repeat.ChangedPhysicalImageBytes}; " +
    $"rebuilt/changed raw sectors: {repeat.RebuiltRawSectorCount}/{repeat.ChangedRawSectorCount}; " +
    $"LBAs: [{string.Join(',', repeat.Plan.AffectedRawSectorLbas)}].");

async Task VerifyResultAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult result,
    bool requirePinnedOutput,
    bool expectRecoveredOperation)
{
    string expectedPrefix = Path.Combine(
        outputDirectoryPath,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.OutputPrefix);
    Require(
        result.Paths.OutputDirectoryPath == Path.GetFullPath(outputDirectoryPath) &&
        result.Paths.OutputPrefix == expectedPrefix &&
        result.Paths.OutputImagePath == expectedPrefix + ".bin" &&
        result.Paths.OutputCuePath == expectedPrefix + ".cue" &&
        result.Paths.ConstructionPlanPath == expectedPrefix + "-construction-plan.json" &&
        result.Paths.StaticReadbackReceiptPath == expectedPrefix + "-static-readback-receipt.json" &&
        result.Paths.RuntimeChecklistPath == expectedPrefix + "-runtime-checklist.md" &&
        result.Paths.LocationGuidePath == expectedPrefix + "-location-guide.svg" &&
        result.Paths.FinderHelperPath == expectedPrefix + "-Reveal-in-Finder.command",
        "The remote-blank writer artifact paths drifted from its exact isolated profile.");
    string[] requiredFiles =
    [
        result.Paths.OutputImagePath,
        result.Paths.OutputCuePath,
        result.Paths.ConstructionPlanPath,
        result.Paths.StaticReadbackReceiptPath,
        result.Paths.RuntimeChecklistPath,
        result.Paths.LocationGuidePath,
        result.Paths.FinderHelperPath
    ];
    Require(requiredFiles.All(File.Exists), "The remote-blank writer omitted a required candidate/handoff artifact.");
    Require(
        Directory.GetFiles(outputDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(requiredFiles.Select(Path.GetFullPath).Order(StringComparer.Ordinal)),
        "The final remote-blank candidate directory contains an unexpected top-level artifact.");
    ValidateCuePair(result.Paths.OutputCuePath, result.Paths.OutputImagePath);

    Require(
        result.Plan.SchemaVersion == 2 &&
        result.Plan.ProfileId == UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.ProfileId &&
        result.Plan.ConstructionProfileId == UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId &&
        result.Plan.BaseImageSha256 == ExpectedBaseImageSha256 &&
        result.Plan.SourceDataSha256 == ExpectedSourceDataSha256 &&
        result.Plan.OutputDataSha256 == ExpectedOutputDataSha256 &&
        result.Plan.SourceModelSha256 == ExpectedSourceModelSha256 &&
        result.Plan.OutputModelSha256 == ExpectedOutputModelSha256 &&
        result.Plan.OutputCollisionTreeSha256 == ExpectedOutputCollisionTreeSha256 &&
        result.Plan.OutputCollisionBlocksSha256 == ExpectedOutputCollisionBlocksSha256 &&
        result.Plan.StaticDiffManifestSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedDiffManifestSha256 &&
        result.Plan.DeterministicStaticPlanSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedDeterministicPlanSha256 &&
        result.Plan.CollisionWindingRepair.FailedV1ProfileId ==
            UnusedLevel65RemoteBlankIsolationConstruction.ProfileId &&
        result.Plan.CollisionWindingRepair.FailedV1OutputDataSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputDataSha256 &&
        result.Plan.CollisionWindingRepair.FailedV1OutputModelSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputModelSha256 &&
        result.Plan.CollisionWindingRepair.CollisionTriangleIndex == 13_995 &&
        result.Plan.CollisionWindingRepair.TriangleDataRelativeOffset == 0x151A3C &&
        result.Plan.CollisionWindingRepair.TriangleModelRelativeOffset == 0x7323C &&
        result.Plan.CollisionWindingRepair.TriangleWadOffset == 0x6A8823C &&
        result.Plan.CollisionWindingRepair.OnlyAffectedRawSectorLba == 54_581 &&
        result.Plan.CollisionWindingRepair.FailedV1TriangleHex == "100138381001007000020000" &&
        result.Plan.CollisionWindingRepair.RepairedTriangleHex == "10011C701001380000020000" &&
        result.Plan.CollisionWindingRepair.FailedV1NormalZ == 50_176 &&
        result.Plan.CollisionWindingRepair.RepairedNormalZ == -50_176 &&
        result.Plan.CollisionWindingRepair.ChangedByteCount == 4 &&
        result.Plan.CollisionWindingRepair.RepairRangeCount == 2 &&
        result.Plan.CollisionWindingRepair.RepairDiffManifestSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedRepairDiffManifestSha256 &&
        result.Plan.CollisionWindingRepair.ExactFailedV1OutputConsumed &&
        result.Plan.CollisionWindingRepair.FourByteAllowlistComplete &&
        result.Plan.CollisionWindingRepair.ByteInverseVerified &&
        result.Plan.CollisionWindingRepair.PointSetIdentical &&
        result.Plan.CollisionWindingRepair.AcbOrderVerified &&
        result.Plan.CollisionWindingRepair.CollisionTreeIdentical &&
        result.Plan.CollisionWindingRepair.CollisionBlocksIdentical &&
        result.Plan.CollisionWindingRepair.CollisionAssignmentIdentical &&
        result.Plan.CollisionWindingRepair.OcclusionIdentical &&
        result.Plan.CollisionWindingRepair.SpawnIdentical &&
        result.Plan.CollisionWindingRepair.ProtectedDataIdentical &&
        !result.Plan.CollisionWindingRepair.FailedV1ArtifactMutationPossible &&
        result.Plan.CollisionWindingRepair.RequiresDuckStationRuntimeProof &&
        !result.Plan.CollisionWindingRepair.PromotionAuthorized &&
        !result.Plan.CollisionWindingRepair.NormalCreateBinEnabled &&
        result.Plan.WadLba == 37 &&
        result.Plan.DataWadOffset == 0x6936800 &&
        result.Plan.DataByteLength == 0x2E2000 &&
        result.Plan.ModelSubfileWadOffset == 0x6A15000 &&
        result.Plan.ModelSubfileByteLength == 0x94800 &&
        result.Plan.SourceSectorCount == 216 &&
        result.Plan.OutputSectorCount == 217 &&
        result.Plan.NewSectorIndex == 216 &&
        result.Plan.EnvironmentGrowthBytes == 0x74 &&
        result.Plan.NewSectorByteLength == 0x70 &&
        result.Plan.MaterialTextureId == 25 &&
        result.Plan.OcclusionGroupCount == 16 &&
        result.Plan.OcclusionAssignment == 0 &&
        result.Plan.SourceOcclusionGroupZeroCount == 124 &&
        result.Plan.OutputOcclusionGroupZeroCount == 125 &&
        result.Plan.ReusedCollisionTriangleIndex == 13_995 &&
        result.Plan.SourceCollisionAssignment == 0xFF &&
        result.Plan.OutputCollisionAssignment == 0 &&
        result.Plan.TargetCollisionCell == new UnusedLevel65RemoteBlankCollisionCell(1, 1, 2) &&
        result.Plan.CollisionBlocksCapacityBytes == 0x17984 &&
        result.Plan.OutputCollisionBlocksUsedBytes == 0x17960 &&
        result.Plan.LandingRawX == 6_153 &&
        result.Plan.LandingRawY == 6_154 &&
        result.Plan.LandingRawZ == 8_550 &&
        result.Plan.PlayerRawX == 6_153 &&
        result.Plan.PlayerRawY == 6_154 &&
        result.Plan.PlayerRawZ == 8_704 &&
        result.Plan.InheritedSectorCount == 216 &&
        result.Plan.InheritedMobyCount == 106 &&
        result.Plan.StaticChangedDataByteCount == 484_336 &&
        result.Plan.StaticDiffRangeCount == 55_386 &&
        result.Plan.Sector216ExactHpLpPairingVerified &&
        result.Plan.FixedOcclusionOwnershipVerified &&
        result.Plan.Triangle13995OrderedRepackVerified &&
        result.Plan.CoupledLandingAndPlayerAnchorVerified &&
        result.Plan.InheritedScaffoldingPreserved &&
        result.Plan.BlankLookingNotByteEmpty &&
        result.Plan.FarLowDetailRuntimePending &&
        result.Plan.DeathPlaneRuntimePending &&
        result.Plan.RequiresDuckStationRuntimeProof &&
        result.Plan.DisposableRuntimeCandidateAuthorized &&
        !result.Plan.PromotionAuthorized &&
        !result.Plan.NormalCreateBinEnabled,
        "The remote-blank construction plan lost an exact sector/occlusion/collision/spawn/isolation pin.");
    VerifyLoadCodes(result.LoadCodes);
    Require(
        result.Plan.LoadCodes.SequenceEqual(result.LoadCodes),
        "The serialized remote-blank plan and result disagree on the four test codes.");

    Require(
        result.OutputImageSha256 == await HashFileAsync(result.Paths.OutputImagePath) &&
        result.OutputDataSha256 == ExpectedOutputDataSha256 &&
        result.Receipt.SchemaVersion == 3 &&
        result.Receipt.OutputDirectoryPath == result.Paths.OutputDirectoryPath &&
        result.Receipt.OutputImagePath == result.Paths.OutputImagePath &&
        result.Receipt.OutputCuePath == result.Paths.OutputCuePath &&
        result.Receipt.ConstructionPlanPath == result.Paths.ConstructionPlanPath &&
        result.Receipt.StaticReadbackReceiptPath == result.Paths.StaticReadbackReceiptPath &&
        result.Receipt.RuntimeChecklistPath == result.Paths.RuntimeChecklistPath &&
        result.Receipt.LocationGuidePath == result.Paths.LocationGuidePath &&
        result.Receipt.FinderHelperPath == result.Paths.FinderHelperPath &&
        result.Receipt.BaseCueSha256 == await HashFileAsync(baseCuePath) &&
        result.Receipt.OutputCueSha256 == await HashFileAsync(result.Paths.OutputCuePath) &&
        result.Receipt.ConstructionPlanSha256 == await HashFileAsync(result.Paths.ConstructionPlanPath) &&
        result.Receipt.RuntimeChecklistSha256 == await HashFileAsync(result.Paths.RuntimeChecklistPath) &&
        result.Receipt.LocationGuideSha256 == await HashFileAsync(result.Paths.LocationGuidePath) &&
        result.Receipt.FinderHelperSha256 == await HashFileAsync(result.Paths.FinderHelperPath) &&
        result.Receipt.OutputImageSha256 == result.OutputImageSha256 &&
        result.Receipt.OutputDataSha256 == result.OutputDataSha256 &&
        result.Receipt.OutputModelSha256 == ExpectedOutputModelSha256 &&
        result.Receipt.OutputCollisionTreeSha256 == ExpectedOutputCollisionTreeSha256 &&
        result.Receipt.OutputCollisionBlocksSha256 == ExpectedOutputCollisionBlocksSha256 &&
        result.Receipt.FailedV1OutputDataSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputDataSha256 &&
        result.Receipt.FailedV1OutputModelSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputModelSha256 &&
        result.Receipt.RepairDiffManifestSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedRepairDiffManifestSha256 &&
        result.Receipt.RepairChangedByteCount == 4 &&
        result.Receipt.FailedV1OnlyAffectedRawSectorLba == 54_581 &&
        result.Receipt.FailedV1ToV2FourByteInverseVerified &&
        result.Receipt.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        result.Receipt.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes &&
        result.Receipt.RebuiltRawSectorCount == result.RebuiltRawSectorCount &&
        result.Receipt.ChangedRawSectorCount == result.ChangedRawSectorCount &&
        result.Receipt.RawSectorDiffs.SequenceEqual(result.RawSectorDiffs) &&
        result.Receipt.RawSectorDiffSha256 == result.RawSectorDiffSha256,
        "The remote-blank static receipt does not bind the exact candidate/diff hashes.");

    RequireRuntimeGuards(
        result.ExactLogicalDiffBoundaryVerified,
        result.ExactPhysicalSectorBoundaryVerified,
        result.StaticPlanPinsVerified,
        result.ModelReadbackVerified,
        result.CollisionReadbackVerified,
        result.Sector216ExactHpLpPairingVerified,
        result.FixedOcclusionOwnershipVerified,
        result.Triangle13995OrderedRepackVerified,
        result.ProtectedSubfilesPreserved,
        result.CoupledLandingAndPlayerAnchorVerified,
        result.InheritedScaffoldingPreserved,
        result.ExecutablePreserved,
        result.RetailControlLevelsPreserved,
        result.RawSectorIntegrityVerified,
        result.BaseCandidatePreserved,
        result.FullDirectoryPublicationVerified,
        result.DisposableRuntimeCandidateAuthorized,
        result.RollbackRecoveryVerified,
        result.FullDirectoryRollbackVerified,
        result.FinderHandoffVerified,
        result.PromotionAuthorized,
        result.NormalCreateBinEnabled,
        expectRecoveredOperation,
        "result");
    RequireRuntimeGuards(
        result.Receipt.ExactLogicalDiffBoundaryVerified,
        result.Receipt.ExactPhysicalSectorBoundaryVerified,
        result.Receipt.StaticPlanPinsVerified,
        result.Receipt.ModelReadbackVerified,
        result.Receipt.CollisionReadbackVerified,
        result.Receipt.Sector216ExactHpLpPairingVerified,
        result.Receipt.FixedOcclusionOwnershipVerified,
        result.Receipt.Triangle13995OrderedRepackVerified,
        result.Receipt.ProtectedSubfilesPreserved,
        result.Receipt.CoupledLandingAndPlayerAnchorVerified,
        result.Receipt.InheritedScaffoldingPreserved,
        result.Receipt.ExecutablePreserved,
        result.Receipt.RetailControlLevelsPreserved,
        result.Receipt.RawSectorIntegrityVerified,
        result.Receipt.BaseCandidatePreserved,
        result.Receipt.FullDirectoryPublicationVerified,
        result.Receipt.DisposableRuntimeCandidateAuthorized,
        result.Receipt.RollbackRecoveryVerified,
        result.Receipt.FullDirectoryRollbackVerified,
        result.Receipt.FinderHandoffVerified,
        result.Receipt.PromotionAuthorized,
        result.Receipt.NormalCreateBinEnabled,
        expectRecoveredOperation,
        "receipt");

    VerifyRawSectorBoundary(result);
    await VerifySidecarsAsync(result);
    if (requirePinnedOutput && !IsPendingHash(ExpectedOutputImageSha256))
    {
        Require(
            result.OutputImageSha256 == ExpectedOutputImageSha256 &&
            result.OutputDataSha256 == ExpectedOutputDataSha256 &&
            result.ChangedLogicalWadBytes == ExpectedChangedLogicalWadBytes &&
            result.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes &&
            result.RebuiltRawSectorCount == ExpectedRebuiltRawSectorCount &&
            result.ChangedRawSectorCount == ExpectedChangedRawSectorCount,
            "The exact remote-blank output hash or logical/raw-sector diff pin drifted: " +
            $"BIN={result.OutputImageSha256}, data={result.OutputDataSha256}, " +
            $"logical={result.ChangedLogicalWadBytes}, physical={result.ChangedPhysicalImageBytes}, " +
            $"rebuilt={result.RebuiltRawSectorCount}, changed={result.ChangedRawSectorCount}.");
    }
}

void RequireRuntimeGuards(
    bool exactLogical,
    bool exactPhysical,
    bool staticPins,
    bool model,
    bool collision,
    bool hpLp,
    bool occlusion,
    bool repack,
    bool protectedSubfiles,
    bool spawn,
    bool scaffolding,
    bool executable,
    bool retail,
    bool raw,
    bool basePreserved,
    bool publication,
    bool disposable,
    bool recovery,
    bool rollback,
    bool finder,
    bool promotion,
    bool normalCreateBin,
    bool expectRecovered,
    string label)
{
    Require(
        exactLogical && exactPhysical && staticPins && model && collision && hpLp && occlusion && repack &&
        protectedSubfiles && spawn && scaffolding && executable && retail && raw && basePreserved && publication &&
        disposable && recovery == expectRecovered && rollback == expectRecovered && finder &&
        !promotion && !normalCreateBin,
        $"The remote-blank {label} weakened a static/readback/preservation/publication guard.");
}

void VerifyRawSectorBoundary(UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult result)
{
    bool pinned = !IsPendingHash(ExpectedRawSectorDiffSha256);
    Require(
        result.RebuiltRawSectorCount == result.ChangedRawSectorCount &&
        result.ChangedRawSectorCount == result.RawSectorDiffs.Count &&
        result.RawSectorDiffs.Select(diff => diff.RawSectorLba).SequenceEqual(
            result.Plan.AffectedRawSectorLbas) &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(
            result.Plan.AffectedRawSectorLbas.Distinct().Order()) &&
        result.RawSectorDiffs.Sum(diff => (long)diff.TotalChangedBytes) == result.ChangedPhysicalImageBytes &&
        result.RawSectorDiffSha256 == HashRawSectorDiffs(result.RawSectorDiffs) &&
        (!pinned || (
            result.Plan.AffectedRawSectorLbas.SequenceEqual(expectedChangedRawSectorLbas) &&
            result.RawSectorDiffSha256 == ExpectedRawSectorDiffSha256)),
        "The remote-blank raw-sector count/LBA/physical-byte boundary is inconsistent.");
    foreach (UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff diff in result.RawSectorDiffs)
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
                diff.PayloadChangedBytes + diff.EdcChangedBytes +
                diff.ReservedChangedBytes + diff.EccPChangedBytes + diff.EccQChangedBytes,
            $"Raw sector {diff.RawSectorLba} has an invalid exact MODE2 Form1 diff classification.");
    }
}

bool IsPendingHash(string value) =>
    string.IsNullOrWhiteSpace(value) || string.Equals(value, "PENDING", StringComparison.Ordinal);

string HashRawSectorDiffs(
    IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff> diffs)
{
    StringBuilder builder = new();
    foreach (UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff diff in diffs)
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

IReadOnlyList<UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff>
    CompareRawImages(string failedV1Image, string repairedV2Image)
{
    const int rawSectorBytes = 2352;
    using FileStream before = new(
        failedV1Image,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 1024 * 1024,
        FileOptions.SequentialScan);
    using FileStream after = new(
        repairedV2Image,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 1024 * 1024,
        FileOptions.SequentialScan);
    Require(
        before.Length == after.Length && before.Length % rawSectorBytes == 0,
        "The frozen failed-v1 and v2 raw images have incompatible lengths.");
    byte[] failed = new byte[rawSectorBytes];
    byte[] repaired = new byte[rawSectorBytes];
    List<UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRawSectorDiff> diffs = [];
    int sectorCount = checked((int)(before.Length / rawSectorBytes));
    for (int lba = 0; lba < sectorCount; lba++)
    {
        before.ReadExactly(failed);
        after.ReadExactly(repaired);
        if (failed.AsSpan().SequenceEqual(repaired))
            continue;
        int header = 0;
        int subheader = 0;
        int payload = 0;
        int edc = 0;
        int reserved = 0;
        int eccP = 0;
        int eccQ = 0;
        int total = 0;
        for (int index = 0; index < rawSectorBytes; index++)
        {
            if (failed[index] == repaired[index])
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
        diffs.Add(new(
            lba,
            header,
            subheader,
            payload,
            edc,
            reserved,
            eccP,
            eccQ,
            total));
    }
    return diffs;
}

void VerifyDirectRepairTransactionMatrix(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan)
{
    Require(
        plan.Repair.RepairRanges.Count == 2 &&
        plan.Repair.RepairRanges[0].DataRelativeOffset == 0x151A3E &&
        plan.Repair.RepairRanges[0].ModelRelativeOffset == 0x7323E &&
        plan.Repair.RepairRanges[0].WadOffset == 0x6A8823E &&
        plan.Repair.RepairRanges[0].RawSectorLba == 54_581 &&
        plan.Repair.RepairRanges[0].RawSectorUserDataOffset == 574 &&
        plan.Repair.RepairRanges[1].DataRelativeOffset == 0x151A42 &&
        plan.Repair.RepairRanges[1].ModelRelativeOffset == 0x73242 &&
        plan.Repair.RepairRanges[1].WadOffset == 0x6A88242 &&
        plan.Repair.RepairRanges[1].RawSectorLba == 54_581 &&
        plan.Repair.RepairRanges[1].RawSectorUserDataOffset == 578,
        "The direct v1-to-v2 two-range/four-byte repair allowlist changed.");
    byte[] forward =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ApplyRepairTransactional(
            plan.FailedV1OutputData,
            plan.Repair.RepairRanges,
            reverse: false);
    byte[] inverse =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ApplyRepairTransactional(
            plan.OutputData,
            plan.Repair.RepairRanges,
            reverse: true);
    Require(
        forward.SequenceEqual(plan.OutputData) && inverse.SequenceEqual(plan.FailedV1OutputData),
        "The direct v1-to-v2 repair forward/inverse identity failed.");

    int[] guardedBytes = [0x151A3E, 0x151A3F, 0x151A42, 0x151A43];
    foreach (int dataRelativeOffset in guardedBytes)
    {
        byte[] tamperedPreimage = plan.FailedV1OutputData.ToArray();
        tamperedPreimage[dataRelativeOffset] ^= 0x01;
        ExpectInvalidData(
            () => UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ApplyRepairTransactional(
                tamperedPreimage,
                plan.Repair.RepairRanges,
                reverse: false),
            $"tampered failed-v1 repair byte 0x{dataRelativeOffset:X}");

        byte[] tamperedAfterimage = plan.OutputData.ToArray();
        tamperedAfterimage[dataRelativeOffset] ^= 0x01;
        ExpectInvalidData(
            () => UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ApplyRepairTransactional(
                tamperedAfterimage,
                plan.Repair.RepairRanges,
                reverse: true),
            $"tampered v2 repair byte 0x{dataRelativeOffset:X}");
    }
    byte[] outsideAllowlist = plan.FailedV1OutputData.ToArray();
    outsideAllowlist[0x151A40] ^= 0x01;
    ExpectInvalidData(
        () => UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ApplyRepairTransactional(
            outsideAllowlist,
            plan.Repair.RepairRanges,
            reverse: false),
        "tampered byte outside the four-byte repair allowlist");
}

void ExpectInvalidData(Action action, string label)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Require(rejected, $"The v2 repair accepted {label}.");
}

async Task RecoverCurrentOwnedCollisionIfPresentAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRequest request)
{
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidatePaths paths =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreatePaths(
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
            Path.GetFileName(path).StartsWith("remote-blank-winding-repair-v2-candidate-", StringComparison.Ordinal))
        .ToArray();
    if (operations.Length == 0)
    {
        Require(topLevelEntries.Length == 0, "The remote-blank operations parent contains a non-owned entry.");
        return;
    }
    Require(
        topLevelEntries.Length == operations.Length &&
        operations.Length == 2 &&
        !Directory.Exists(paths.OutputDirectoryPath),
        "The preflight found stale remote-blank state other than the exact observed two-operation collision.");
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
        await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(request with
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

async Task VerifyPrePublicationAbortCleanupAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRequest request,
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult reference,
    IReadOnlyDictionary<string, string> expectedSnapshot)
{
    await VerifyExistingAbortAsync(cancel: false);
    await VerifyExistingAbortAsync(cancel: true);
    await VerifyNoPreviousAbortAsync(cancel: false);
    await VerifyNoPreviousAbortAsync(cancel: true);

    async Task VerifyExistingAbortAsync(bool cancel)
    {
        using CancellationTokenSource cancellation = new();
        bool observed = false;
        try
        {
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(
                request with
                {
                    TestStageHook = stage =>
                    {
                        if (stage != "after-staged-candidate-verified")
                            return;
                        if (cancel)
                            cancellation.Cancel();
                        else
                            throw new IOException("Injected remote-blank pre-publication staging failure.");
                    }
                },
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancel)
        {
            observed = true;
        }
        catch (IOException ex) when (
            !cancel && ex.ToString().Contains(
                "Injected remote-blank pre-publication staging failure",
                StringComparison.Ordinal))
        {
            observed = true;
        }
        Require(observed, $"The existing-candidate pre-publication {(cancel ? "cancellation" : "failure")} was not observed.");
        RequireSnapshotsEqual(
            expectedSnapshot,
            SnapshotDirectory(reference.Paths.OutputDirectoryPath),
            $"Existing-candidate pre-publication {(cancel ? "cancellation" : "failure")}");
        RequireNoPublicationDebris(reference.Paths);

        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult clean =
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(request);
        await VerifyResultAsync(clean, requirePinnedOutput: false, expectRecoveredOperation: false);
        RequireSnapshotsEqual(
            expectedSnapshot,
            SnapshotDirectory(reference.Paths.OutputDirectoryPath),
            $"Clean run after existing-candidate pre-publication {(cancel ? "cancellation" : "failure")}");
        RequireNoPublicationDebris(clean.Paths);
    }

    async Task VerifyNoPreviousAbortAsync(bool cancel)
    {
        string sandbox = Path.Combine(
            Path.GetTempPath(),
            $"spyro-id65-remote-blank-prepublication-{(cancel ? "cancel" : "failure")}-{Guid.NewGuid():N}");
        string isolatedOutput = Path.Combine(
            sandbox,
            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.OutputDirectoryName);
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidatePaths paths =
            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreatePaths(isolatedOutput);
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRequest isolatedRequest = new(
            baseImagePath,
            baseCuePath,
            paths.OutputDirectoryPath,
            ReplaceExistingCandidate: false,
            RequestFinderReveal: false);
        try
        {
            using CancellationTokenSource cancellation = new();
            bool observed = false;
            try
            {
                await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(
                    isolatedRequest with
                    {
                        TestStageHook = stage =>
                        {
                            if (stage != "after-staged-candidate-verified")
                                return;
                            if (cancel)
                                cancellation.Cancel();
                            else
                                throw new IOException(
                                    "Injected no-previous remote-blank pre-publication staging failure.");
                        }
                    },
                    cancellation.Token);
            }
            catch (OperationCanceledException) when (cancel)
            {
                observed = true;
            }
            catch (IOException ex) when (
                !cancel && ex.ToString().Contains(
                    "Injected no-previous remote-blank pre-publication staging failure",
                    StringComparison.Ordinal))
            {
                observed = true;
            }
            Require(observed, $"The no-previous pre-publication {(cancel ? "cancellation" : "failure")} was not observed.");
            Require(
                !Directory.Exists(paths.OutputDirectoryPath) &&
                !Directory.Exists(paths.OperationsDirectoryPath),
                "A no-previous pre-publication abort published output or retained an owned operation.");
            RequireNoPublicationDebris(paths);

            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult clean =
                await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(isolatedRequest);
            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult durable =
                await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.VerifyPublishedAsync(clean.Paths);
            Require(
                clean.OutputImageSha256 == reference.OutputImageSha256 &&
                durable.OutputImageSha256 == reference.OutputImageSha256 &&
                !clean.RollbackRecoveryVerified && !clean.FullDirectoryRollbackVerified &&
                !durable.RollbackRecoveryVerified && !durable.FullDirectoryRollbackVerified &&
                clean.FinderReveal == null && durable.FinderReveal == null,
                "The clean no-previous run after a pre-publication abort was not exact and unrecovered.");
            RequireNoPublicationDebris(clean.Paths);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, recursive: true);
        }
        Require(!Directory.Exists(sandbox), "The no-previous pre-publication abort smoke left sandbox debris.");
    }
}

async Task VerifyConcurrentWriterRefusalAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateRequest request,
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult reference,
    IReadOnlyDictionary<string, string> expectedSnapshot)
{
    TaskCompletionSource<bool> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult> active =
        Task.Run(async () =>
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(request with
            {
                TestStageHook = stage =>
                {
                    if (stage != "after-global-writer-lease-acquired")
                        return;
                    held.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                }
            }));
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult? completed = null;
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
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(request)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (IOException ex) when (
            ex.ToString().Contains(
                "Another remote-blank v2 winding-repair runtime candidate writer is active",
                StringComparison.Ordinal))
        {
            contentionRefused = true;
        }
        Require(contentionRefused, "A concurrent remote-blank writer was not refused by the per-output lease.");
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

    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult completedResult = completed ??
        throw new InvalidOperationException(
            "The serialized remote-blank writer did not complete after lease release.");
    await VerifyResultAsync(completedResult, requirePinnedOutput: false, expectRecoveredOperation: false);
    RequireSnapshotsEqual(expectedSnapshot, SnapshotDirectory(outputDirectoryPath), "Serialized writer contention");
    RequireNoPublicationDebris(completedResult.Paths);
}

async Task VerifyNoPreviousPublishedCandidateRecoveryAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult reference)
{
    string sandbox = Path.Combine(
        Path.GetTempPath(),
        $"spyro-id65-remote-blank-no-previous-{Guid.NewGuid():N}");
    string isolatedOutput = Path.Combine(sandbox, UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.OutputDirectoryName);
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidatePaths paths =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreatePaths(isolatedOutput);
    try
    {
        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult stagedSeed =
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(new(
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
            $"remote-blank-winding-repair-v2-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string stagedDirectory = Path.Combine(operationRoot, "candidate");
        string backupDirectory = Path.Combine(operationRoot, "previous-candidate");
        Directory.Move(paths.OutputDirectoryPath, stagedDirectory);
        await File.WriteAllBytesAsync(Path.Combine(operationRoot, "operation.lease"), []);
        string journal = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                operationKind = "unused-level-65-remote-blank-collision-winding-repair-v2-runtime-candidate",
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

        UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult recoveredNoPrevious =
            await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.CreateAsync(new(
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

async Task VerifyDurableSidecarBindingAsync(
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult reference)
{
    IReadOnlyDictionary<string, string> frozenSnapshot =
        SnapshotDirectory(reference.Paths.OutputDirectoryPath);
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult verified =
        await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.VerifyPublishedAsync(
            reference.Paths);
    Require(
        verified.Paths == reference.Paths &&
        verified.OutputImageSha256 == reference.OutputImageSha256 &&
        verified.OutputDataSha256 == reference.OutputDataSha256 &&
        verified.RawSectorDiffSha256 == reference.RawSectorDiffSha256 &&
        verified.Plan.LoadCodes.SequenceEqual(reference.Plan.LoadCodes) &&
        verified.Receipt.OutputCueSha256 == reference.Receipt.OutputCueSha256 &&
        verified.Receipt.ConstructionPlanSha256 == reference.Receipt.ConstructionPlanSha256 &&
        verified.Receipt.RuntimeChecklistSha256 == reference.Receipt.RuntimeChecklistSha256 &&
        verified.Receipt.LocationGuideSha256 == reference.Receipt.LocationGuideSha256 &&
        verified.Receipt.FinderHelperSha256 == reference.Receipt.FinderHelperSha256 &&
        verified.FinderHandoffVerified && !verified.PromotionAuthorized && !verified.NormalCreateBinEnabled,
        "The read-only durable remote-blank verifier did not reproduce the frozen result.");

    await TamperAndRestoreAsync(
        reference.Paths.OutputCuePath,
        "output CUE",
        original => AppendBytes(original, Encoding.ASCII.GetBytes("REM durable-sidecar-tamper\n")),
        preserveExactMode: false);
    await TamperAndRestoreAsync(
        reference.Paths.ConstructionPlanPath,
        "construction plan",
        original => AppendBytes(original, Encoding.UTF8.GetBytes(" ")),
        preserveExactMode: false);
    await TamperAndRestoreAsync(
        reference.Paths.RuntimeChecklistPath,
        "runtime checklist",
        original => AppendBytes(original, Encoding.UTF8.GetBytes("\n<!-- durable-sidecar-tamper -->\n")),
        preserveExactMode: false);
    await TamperAndRestoreAsync(
        reference.Paths.LocationGuidePath,
        "location guide",
        original => AppendBytes(original, Encoding.UTF8.GetBytes("<!-- durable-sidecar-tamper -->\n")),
        preserveExactMode: false);
    await TamperAndRestoreAsync(
        reference.Paths.FinderHelperPath,
        "same-name 0755 Finder helper content",
        original => AppendBytes(original, Encoding.UTF8.GetBytes("# durable-sidecar-tamper\n")),
        preserveExactMode: true);

    string frozenReceipt = await File.ReadAllTextAsync(reference.Paths.StaticReadbackReceiptPath);
    string cueHashProperty =
        $"\"outputCueSha256\": \"{reference.Receipt.OutputCueSha256}\"";
    string changedCueHash =
        (reference.Receipt.OutputCueSha256[0] == '0' ? "1" : "0") +
        reference.Receipt.OutputCueSha256[1..];
    string tamperedCueHashProperty = $"\"outputCueSha256\": \"{changedCueHash}\"";
    string tamperedReceipt = frozenReceipt.Replace(
        cueHashProperty,
        tamperedCueHashProperty,
        StringComparison.Ordinal);
    Require(tamperedReceipt != frozenReceipt, "The frozen-receipt tamper fixture did not change its CUE hash.");
    await TamperAndRestoreAsync(
        reference.Paths.StaticReadbackReceiptPath,
        "receipt-bound CUE hash",
        _ => Encoding.UTF8.GetBytes(tamperedReceipt),
        preserveExactMode: false);

    string receiptWithUnknownField = frozenReceipt.Replace(
        "\n}\n",
        ",\n  \"forgedUnknownReceiptField\": true\n}\n",
        StringComparison.Ordinal);
    Require(
        receiptWithUnknownField != frozenReceipt,
        "The unknown-field receipt tamper fixture did not change exact bytes.");
    await TamperAndRestoreAsync(
        reference.Paths.StaticReadbackReceiptPath,
        "unknown receipt field",
        _ => Encoding.UTF8.GetBytes(receiptWithUnknownField),
        preserveExactMode: false);

    await TamperAndRestoreAsync(
        reference.Paths.StaticReadbackReceiptPath,
        "noncanonical receipt whitespace",
        original => AppendBytes(original, Encoding.UTF8.GetBytes(" ")),
        preserveExactMode: false);

    string coordinatedRecoveryForgery = frozenReceipt
        .Replace(
            "\"rollbackRecoveryVerified\": false",
            "\"rollbackRecoveryVerified\": true",
            StringComparison.Ordinal)
        .Replace(
            "\"fullDirectoryRollbackVerified\": false",
            "\"fullDirectoryRollbackVerified\": true",
            StringComparison.Ordinal);
    Require(
        coordinatedRecoveryForgery != frozenReceipt &&
        coordinatedRecoveryForgery.Contains("\"rollbackRecoveryVerified\": true", StringComparison.Ordinal) &&
        coordinatedRecoveryForgery.Contains("\"fullDirectoryRollbackVerified\": true", StringComparison.Ordinal),
        "The coordinated recovery-flag receipt forgery did not flip both exact flags.");
    await TamperAndRestoreAsync(
        reference.Paths.StaticReadbackReceiptPath,
        "coordinated recovery flags",
        _ => Encoding.UTF8.GetBytes(coordinatedRecoveryForgery),
        preserveExactMode: false);

    RequireSnapshotsEqual(
        frozenSnapshot,
        SnapshotDirectory(reference.Paths.OutputDirectoryPath),
        "Durable sidecar tamper restoration");
    UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult restored =
        await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.VerifyPublishedAsync(
            reference.Paths);
    Require(
        restored.OutputImageSha256 == reference.OutputImageSha256 &&
        restored.Receipt.OutputCueSha256 == reference.Receipt.OutputCueSha256 &&
        restored.Receipt.ConstructionPlanSha256 == reference.Receipt.ConstructionPlanSha256 &&
        restored.Receipt.RuntimeChecklistSha256 == reference.Receipt.RuntimeChecklistSha256 &&
        restored.Receipt.LocationGuideSha256 == reference.Receipt.LocationGuideSha256 &&
        restored.Receipt.FinderHelperSha256 == reference.Receipt.FinderHelperSha256 &&
        restored.Receipt.RawSectorDiffs.SequenceEqual(reference.Receipt.RawSectorDiffs),
        "The remote-blank durable verifier did not pass after exact sidecar restoration.");

    async Task TamperAndRestoreAsync(
        string path,
        string label,
        Func<byte[], byte[]> tamper,
        bool preserveExactMode)
    {
        byte[] original = await File.ReadAllBytesAsync(path);
        UnixFileMode originalMode = OperatingSystem.IsWindows()
            ? default
            : File.GetUnixFileMode(path);
        byte[] changed = tamper(original);
        Require(!changed.SequenceEqual(original), $"The {label} tamper fixture did not change exact bytes.");
        try
        {
            await File.WriteAllBytesAsync(path, changed);
            if (preserveExactMode && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, originalMode);
            Require(
                File.Exists(path) &&
                (!preserveExactMode || OperatingSystem.IsWindows() ||
                 File.GetUnixFileMode(path) == originalMode),
                $"The {label} tamper fixture did not preserve the same name/mode boundary.");
            bool rejected = false;
            try
            {
                _ = await UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.VerifyPublishedAsync(
                    reference.Paths);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Require(rejected, $"The durable verifier accepted tampered {label} bytes.");
        }
        finally
        {
            await File.WriteAllBytesAsync(path, original);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, originalMode);
        }
        Require(
            (await File.ReadAllBytesAsync(path)).SequenceEqual(original),
            $"The {label} tamper fixture did not restore exact bytes.");
    }

    static byte[] AppendBytes(byte[] original, byte[] suffix)
    {
        byte[] result = new byte[checked(original.Length + suffix.Length)];
        original.CopyTo(result, 0);
        suffix.CopyTo(result, original.Length);
        return result;
    }
}

async Task VerifySidecarsAsync(UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateResult result)
{
    using JsonDocument planDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.ConstructionPlanPath));
    using JsonDocument receiptDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(result.Paths.StaticReadbackReceiptPath));
    JsonElement serializedPlan = planDocument.RootElement;
    JsonElement serializedReceipt = receiptDocument.RootElement;
    Require(
        serializedPlan.GetProperty("profileId").GetString() ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.ProfileId &&
        serializedPlan.GetProperty("constructionProfileId").GetString() ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId &&
        serializedPlan.GetProperty("newSectorIndex").GetInt32() == 216 &&
        serializedPlan.GetProperty("environmentGrowthBytes").GetInt32() == 0x74 &&
        serializedPlan.GetProperty("occlusionAssignment").GetInt32() == 0 &&
        serializedPlan.GetProperty("reusedCollisionTriangleIndex").GetInt32() == 13_995 &&
        serializedPlan.GetProperty("schemaVersion").GetInt32() == 2 &&
        serializedPlan.GetProperty("collisionWindingRepair")
            .GetProperty("failedV1TriangleHex").GetString() == "100138381001007000020000" &&
        serializedPlan.GetProperty("collisionWindingRepair")
            .GetProperty("repairedTriangleHex").GetString() == "10011C701001380000020000" &&
        serializedPlan.GetProperty("collisionWindingRepair")
            .GetProperty("changedByteCount").GetInt32() == 4 &&
        serializedPlan.GetProperty("collisionWindingRepair")
            .GetProperty("onlyAffectedRawSectorLba").GetInt32() == 54_581 &&
        serializedPlan.GetProperty("collisionWindingRepair")
            .GetProperty("byteInverseVerified").GetBoolean() &&
        serializedPlan.GetProperty("outputCollisionBlocksUsedBytes").GetInt32() == 0x17960 &&
        serializedPlan.GetProperty("landingRawX").GetInt32() == 6_153 &&
        serializedPlan.GetProperty("playerRawX").GetInt32() == 6_153 &&
        serializedPlan.GetProperty("blankLookingNotByteEmpty").GetBoolean() &&
        serializedPlan.GetProperty("farLowDetailRuntimePending").GetBoolean() &&
        serializedPlan.GetProperty("deathPlaneRuntimePending").GetBoolean() &&
        serializedPlan.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        !serializedPlan.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedPlan.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The remote-blank construction-plan JSON lost its exact static-only contract.");
    Require(
        serializedReceipt.GetProperty("schemaVersion").GetInt32() == 3 &&
        serializedReceipt.GetProperty("profileId").GetString() ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidateExporter.ProfileId &&
        serializedReceipt.GetProperty("staticReadbackReceiptPath").GetString() ==
            result.Paths.StaticReadbackReceiptPath &&
        serializedReceipt.GetProperty("baseCueSha256").GetString() == result.Receipt.BaseCueSha256 &&
        serializedReceipt.GetProperty("outputCueSha256").GetString() == result.Receipt.OutputCueSha256 &&
        serializedReceipt.GetProperty("constructionPlanSha256").GetString() == result.Receipt.ConstructionPlanSha256 &&
        serializedReceipt.GetProperty("runtimeChecklistSha256").GetString() == result.Receipt.RuntimeChecklistSha256 &&
        serializedReceipt.GetProperty("locationGuideSha256").GetString() == result.Receipt.LocationGuideSha256 &&
        serializedReceipt.GetProperty("finderHelperSha256").GetString() == result.Receipt.FinderHelperSha256 &&
        serializedReceipt.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        serializedReceipt.GetProperty("outputDataSha256").GetString() == result.OutputDataSha256 &&
        serializedReceipt.GetProperty("failedV1OutputDataSha256").GetString() ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedFailedV1OutputDataSha256 &&
        serializedReceipt.GetProperty("repairDiffManifestSha256").GetString() ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedRepairDiffManifestSha256 &&
        serializedReceipt.GetProperty("repairChangedByteCount").GetInt32() == 4 &&
        serializedReceipt.GetProperty("failedV1OnlyAffectedRawSectorLba").GetInt32() == 54_581 &&
        serializedReceipt.GetProperty("failedV1ToV2FourByteInverseVerified").GetBoolean() &&
        serializedReceipt.GetProperty("rawSectorDiffs").GetArrayLength() == result.RawSectorDiffs.Count &&
        serializedReceipt.GetProperty("staticPlanPinsVerified").GetBoolean() &&
        serializedReceipt.GetProperty("sector216ExactHpLpPairingVerified").GetBoolean() &&
        serializedReceipt.GetProperty("fixedOcclusionOwnershipVerified").GetBoolean() &&
        serializedReceipt.GetProperty("triangle13995OrderedRepackVerified").GetBoolean() &&
        serializedReceipt.GetProperty("coupledLandingAndPlayerAnchorVerified").GetBoolean() &&
        serializedReceipt.GetProperty("inheritedScaffoldingPreserved").GetBoolean() &&
        serializedReceipt.GetProperty("executablePreserved").GetBoolean() &&
        serializedReceipt.GetProperty("retailControlLevelsPreserved").GetBoolean() &&
        serializedReceipt.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        serializedReceipt.GetProperty("baseCandidatePreserved").GetBoolean() &&
        !serializedReceipt.GetProperty("promotionAuthorized").GetBoolean() &&
        !serializedReceipt.GetProperty("normalCreateBinEnabled").GetBoolean(),
        "The remote-blank static receipt JSON lost its exact readback/nonintegration contract.");

    RuntimeCandidateFinderReveal finder = result.FinderReveal ??
        throw new InvalidOperationException("The macOS remote-blank handoff omitted Finder reveal metadata.");
    Require(
        finder.CuePath == result.Paths.OutputCuePath &&
        finder.PairedBinPath == result.Paths.OutputImagePath &&
        finder.HelperPath == result.Paths.FinderHelperPath &&
        finder.CuePairingVerified &&
        finder.HelperIsExecutable &&
        File.GetUnixFileMode(finder.HelperPath) == ExactFinderMode(),
        "The remote-blank Finder helper lost exact CUE pairing or 0755 permissions.");
    string helper = await File.ReadAllTextAsync(finder.HelperPath);
    Require(
        helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal) &&
        helper.Contains(Path.GetFileName(result.Paths.OutputCuePath), StringComparison.Ordinal),
        "The remote-blank Finder helper does not reveal its exact CUE.");

    string checklist = await File.ReadAllTextAsync(result.Paths.RuntimeChecklistPath);
    RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, finder, result.LoadCodes);
    string[] checklistPins =
    [
        result.OutputImageSha256,
        "Memory Card 1: **None**",
        "Memory Card 2: **None**",
        "**Do not save.**",
        "**DO NOT LOAD OR RETEST failed v1**",
        ExpectedFailedV1OutputImageSha256,
        "**Initial lives + completed landing:**",
        "exactly `4` lives",
        "**20-second hands-off stability:**",
        "at least `20` continuous real-time seconds",
        "**Lone-pad visibility:**",
        "**Full 360 camera:**",
        "**Walk:**",
        "**Charge:**",
        "**Jump/land:**",
        "**One deliberate fall + exact respawn:**",
        "lives exactly `4 → 3`",
        "same isolated pad at the same yaw/orientation",
        "**Remaining edge controls:**",
        "**Cold reset:**",
        "**Far LP is unclaimable in this discriminator:**",
        "Do not report an LP pass from this candidate",
        "**Death plane is runtime-pending:**",
        "blank-looking, not byte-empty",
        "Retail control gates",
        "normal Create BIN remains disabled"
    ];
    Require(
        checklistPins.All(pin => checklist.Contains(pin, StringComparison.Ordinal)) &&
        result.LoadCodes.All(code => checklist.Contains(code.InputCode, StringComparison.Ordinal)),
        "The remote-blank checklist lost a movement/death/reset/pending/control gate or complete load code.");

    string svgText = await File.ReadAllTextAsync(result.Paths.LocationGuidePath);
    XDocument svg = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
    XElement svgRoot = svg.Root ?? throw new InvalidDataException("The remote-blank guide SVG has no root.");
    Require(
        svgRoot.Name.LocalName == "svg" &&
        svgRoot.Attribute("width")?.Value == "1200" &&
        svgRoot.Attribute("height")?.Value == "1000" &&
        svgText.Contains("Sector 216", StringComparison.Ordinal) &&
        svgText.Contains("360°", StringComparison.Ordinal) &&
        svgText.Contains("Walk • charge • jump", StringComparison.Ordinal) &&
        svgText.Contains("HUD lives exactly 4", StringComparison.Ordinal) &&
        svgText.Contains("hands off ≥20 continuous seconds", StringComparison.Ordinal) &&
        svgText.Contains("one deliberate fall", StringComparison.Ordinal) &&
        svgText.Contains("lives 4→3", StringComparison.Ordinal) &&
        svgText.Contains("same pad + same yaw respawn", StringComparison.Ordinal) &&
        svgText.Contains("Cards 1/2: None", StringComparison.Ordinal) &&
        svgText.Contains("Far LP UNCLAIMABLE on this small pad", StringComparison.Ordinal) &&
        svgText.Contains("DO NOT LOAD/RETEST failed v1", StringComparison.Ordinal) &&
        svgText.Contains(ExpectedFailedV1OutputImageSha256, StringComparison.Ordinal) &&
        result.LoadCodes.All(code => svgText.Contains(code.InputCode, StringComparison.Ordinal)),
        "The remote-blank location guide is malformed or omits its isolated-pad/four-code handoff.");
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
                $"The remote-blank guide text can clip: `{text}` bounds={left:F1}..{right:F1}, " +
                $"baseline/font={y:F1}/{fontSize:F1}.");
        }
    }
    Require(fragmentCount >= 15, "The remote-blank guide lost its expected wrapped identity/route/code text.");
}

double ParseSvgNumber(XElement fragment, XElement parent, string attributeName)
{
    string? value = fragment.Attribute(attributeName)?.Value ?? parent.Attribute(attributeName)?.Value;
    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
        ? parsed
        : throw new InvalidDataException(
            $"The remote-blank guide text omitted numeric `{attributeName}` positioning.");
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
    Require(loadCodes.Count == expected.Length, "The remote-blank handoff must contain exactly four load codes.");
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
            $"The complete remote-blank load code for {expected[index].Name} changed.");
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

void RequireNoPublicationDebris(UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidatePaths paths)
{
    Require(
        !Directory.Exists(paths.OperationsDirectoryPath),
        "The remote-blank writer retained operation/backup debris.");
    string[] debris = Directory.Exists(paths.OutputDirectoryPath)
        ? Directory.GetFileSystemEntries(paths.OutputDirectoryPath, "*", SearchOption.AllDirectories)
            .Where(path =>
                Path.GetFileName(path).Contains(".tmp", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains(".bak", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Contains("previous-candidate", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("operation-journal.json", StringComparison.OrdinalIgnoreCase))
            .ToArray()
        : [];
    Require(debris.Length == 0, $"Remote-blank publication debris remained: {string.Join(',', debris)}");
    VerifyWriterLeaseReleased(paths);
}

void VerifyWriterLeaseReleased(UnusedLevel65RemoteBlankCollisionWindingRepairV2RuntimeCandidatePaths paths)
{
    string parent = Path.GetDirectoryName(paths.OperationsDirectoryPath) ??
        throw new InvalidOperationException("The remote-blank operations path has no parent.");
    string leasePath = Path.Combine(
        parent,
        ".unused-level-65-remote-blank-winding-repair-v2-writer.lease");
    Require(
        File.Exists(leasePath) &&
        !Directory.Exists(leasePath) &&
        (File.GetAttributes(leasePath) & FileAttributes.ReparsePoint) == 0 &&
        new FileInfo(leasePath).Length == 0,
        "The persistent remote-blank writer lease is missing, nonempty, or an unsafe path type.");
    using FileStream lease = new(
        leasePath,
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);
    Require(lease.Length == 0, "The released remote-blank writer lease changed during the native probe.");
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
        $"The persistent remote-blank writer lease remained natively locked (errno {lockError}).");
    Require(
        NativeLeaseProbe.Flock(fileDescriptor, NativeLeaseProbe.LockUnlock) == 0,
        "The remote-blank smoke could not release its native lease probe.");
}

string[] GetOwnedOperationDirectories(string operationsDirectoryPath) =>
    Directory.Exists(operationsDirectoryPath)
        ? Directory.GetDirectories(
            operationsDirectoryPath,
            "remote-blank-winding-repair-v2-candidate-*",
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
        Require(File.Exists(path), $"Protected remote-blank path is missing: {path}");
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
