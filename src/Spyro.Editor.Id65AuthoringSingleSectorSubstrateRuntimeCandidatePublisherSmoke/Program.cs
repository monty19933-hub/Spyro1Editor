using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string ExpectedTile1OutputEnvironmentSha256 =
    "765ca3b55cb72e53305cee6d425802d998ad760661657a4bcc0a2769615ede07";

if (args.Length is < 2 or > 3)
{
    throw new ArgumentException(
        "Usage: <workspace-container> <exact-schema6-p2-candidate-directory> [schema7-output-root]");
}

string workspaceContainer = Path.GetFullPath(args[0]);
string parentCandidateDirectory = Path.GetFullPath(args[1]);
UnusedLevel65BlankLevelLabWorkspacePaths paths =
    UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
        workspaceContainer);
string outputRoot = args.Length == 3
    ? Path.GetFullPath(args[2])
    : Path.Combine(
        paths.RootPath,
        "materialized-single-sector-substrate-schema7-candidates");

UnusedLevel65BlankLevelLabManifest manifest =
    await UnusedLevel65BlankLevelLabBootstrapper
        .ValidatePublishedWorkspaceReadOnlyAsync(paths);
Id65AuthoringEditorAggregateCandidateWorkspaceGuard
    .RequireExactCanonicalAuthoredLayer(paths);
Id65AuthoringEditorDocumentStoreResult stored =
    await Id65AuthoringEditorDocumentStore.LoadAsync(
        paths,
        manifest,
        new Id65AuthoringEditorDocumentProjectionInputs(null)) ??
    throw new InvalidDataException(
        "The current ID65 workspace has no persisted schema-6 document.");
Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
    await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
        .BuildStaticPlan(paths.LockedBaseImagePath);

Id65AuthoringSingleSectorAggregatePlan aggregatePlan =
    Id65AuthoringSingleSectorAggregatePlanProjector.Project(new(
        stored,
        stored.EnvelopeCanonicalSha256,
        manifest,
        nativeInputs,
        v3Template));
Id65CompiledAuthoringSingleSectorSubstrate compiled =
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        aggregatePlan.Document,
        nativeInputs.LockedSource,
        v3Template,
        nativeInputs.PrivateTextureWitness,
        nativeInputs.GrassBundle));

Require(
    stored.Document.SchemaVersion ==
        Id65AuthoringEditorDocument
            .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
    aggregatePlan.ActiveTerrainUnitIds.SequenceEqual(
        [Id65AuthoringTerrainSetCatalog.Tile1Id]) &&
    aggregatePlan.MobyKind == Id65RemotePadMobyKind.GrassT107 &&
    aggregatePlan.MobyPositionId ==
        Id65RemotePadGrassT107Catalog.CenterPositionId,
    "The focused real-workspace publisher requires current Tile1 / Grass T107 / Center state.");
Require(
    aggregatePlan.DocumentCanonicalSha256 ==
        compiled.DocumentCanonicalSha256 &&
    aggregatePlan.Schema6BaselineRow80Sha256 ==
        compiled.Schema6BaselineRow80Sha256 &&
    aggregatePlan.OutputRow80Sha256 == compiled.OutputRow80Sha256 &&
    aggregatePlan.CompilerDeterministicPlanSha256 ==
        compiled.DeterministicPlanSha256 &&
    aggregatePlan.CollisionSha256 == compiled.CollisionSha256 &&
    aggregatePlan.Environment.OutputEnvironmentSha256 ==
        compiled.Environment.OutputEnvironmentSha256 &&
    aggregatePlan.Occlusion.OutputOcclusionSha256 ==
        compiled.Occlusion.OutputOcclusionSha256,
    "The independent aggregate and compiler captures diverged.");

string parentImagePath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
        .CandidateImageFileName);
string parentCuePath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
        .CandidateCueFileName);
string parentReceiptPath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
        .CandidateReceiptFileName);
FileSnapshot[] parentBefore = await Task.WhenAll(
    new[] { parentImagePath, parentCuePath, parentReceiptPath }
        .Select(SnapshotAsync));

Id65AuthoringSingleSectorSubstrateRuntimeCandidateResult first =
    await Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
        .PublishAsync(new(
            parentCandidateDirectory,
            outputRoot,
            compiled,
            aggregatePlan));
Id65AuthoringSingleSectorSubstrateRuntimeCandidateResult repeated =
    await Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
        .PublishAsync(new(
            parentCandidateDirectory,
            outputRoot,
            compiled,
            aggregatePlan));

FileSnapshot[] parentAfter = await Task.WhenAll(
    new[] { parentImagePath, parentCuePath, parentReceiptPath }
        .Select(SnapshotAsync));
Require(
    parentBefore.SequenceEqual(parentAfter),
    "The schema-7 publication changed its immutable schema-6 parent artifacts.");
Require(
    first.Paths == repeated.Paths &&
    first.Receipt == repeated.Receipt &&
    first.ReceiptFileSha256 == repeated.ReceiptFileSha256 &&
    !repeated.CandidateArtifactsWrittenThisCall &&
    repeated.ReusedExistingCandidate &&
    first.Receipt.ParentAggregatePlanSha256 ==
        aggregatePlan.Schema6AggregatePlanSha256 &&
    first.Receipt.AggregatePlanSha256 ==
        aggregatePlan.DeterministicAggregatePlanSha256 &&
    first.Receipt.CompilerDeterministicPlanSha256 ==
        compiled.DeterministicPlanSha256 &&
    first.Receipt.Schema6BaselineRow80Sha256 ==
        compiled.Schema6BaselineRow80Sha256 &&
    first.Receipt.OutputRow80Sha256 == compiled.OutputRow80Sha256 &&
    first.Receipt.OutputEnvironmentSha256 ==
        ExpectedTile1OutputEnvironmentSha256 &&
    first.Receipt.OutputOcclusionSha256 ==
        Id65AuthoringSingleSectorSubstrateCompiler
            .ExpectedOutputOcclusionSha256 &&
    first.Receipt.CollisionSha256 == compiled.CollisionSha256 &&
    first.Receipt.StageChangedLogicalByteCount ==
        compiled.StageChangedByteCount &&
    first.Receipt.ExactParentReceiptVerified &&
    first.Receipt.ExactParentImageVerified &&
    first.Receipt.ExactSchema6StagePreimageVerified &&
    first.Receipt.ExactSchema6StageInverseVerified &&
    first.Receipt.ExactOutputRow80ReadbackVerified &&
    first.Receipt.SingleSectorEnvironmentReadbackVerified &&
    first.Receipt.SingleSectorOcclusionReadbackVerified &&
    first.Receipt.CollisionPreservedExactly &&
    first.Receipt.CycloramaPreservedExactly &&
    first.Receipt.SpawnAndMobyBytesPreservedExactly &&
    first.Receipt.RawSectorIntegrityVerified &&
    first.Receipt.PhysicalComplementVerified &&
    first.Receipt.ParentSchema6CandidatePreserved &&
    first.Receipt.ContainsFullDiscImage && first.Receipt.ContainsCue &&
    first.Receipt.SeparateGuardedCandidatePublisherAuthorized &&
    !first.Receipt.RuntimeCandidateAuthorized &&
    !first.Receipt.RuntimeProven && !first.Receipt.AppIntegrated &&
    !first.Receipt.UiIntegrated && !first.Receipt.CreateBinEnabled &&
    !first.Receipt.NormalCreateBinEnabled &&
    !first.Receipt.DocumentPersistenceIntegrated &&
    !first.Receipt.ReleaseIntegrated &&
    !first.Receipt.PromotionAuthorized && !first.Receipt.Publishable &&
    !first.RuntimeProven && !first.NormalCreateBinEnabled &&
    !first.ReleaseIntegrated && !first.Publishable,
    "The published schema-7 candidate or its authority boundary changed.");
Require(
    File.Exists(first.Paths.CandidateImagePath) &&
    File.Exists(first.Paths.CandidateCuePath) &&
    File.Exists(first.Paths.CandidateReceiptPath),
    "The published schema-7 candidate artifact set is incomplete.");

Console.WriteLine(
    "ID65_AUTHORING_SINGLE_SECTOR_SUBSTRATE_RUNTIME_CANDIDATE_PUBLISHER_SMOKE_OK");
Console.WriteLine($"candidateDirectory={first.Paths.CandidateDirectoryPath}");
Console.WriteLine($"candidateImage={first.Paths.CandidateImagePath}");
Console.WriteLine($"candidateCue={first.Paths.CandidateCuePath}");
Console.WriteLine($"candidateReceipt={first.Paths.CandidateReceiptPath}");
Console.WriteLine($"parentImageSha256={first.Receipt.ParentCandidateImageSha256}");
Console.WriteLine($"outputImageSha256={first.Receipt.OutputImageSha256}");
Console.WriteLine($"receiptFileSha256={first.ReceiptFileSha256}");
Console.WriteLine($"aggregatePlanSha256={first.Receipt.AggregatePlanSha256}");
Console.WriteLine($"outputRow80Sha256={first.Receipt.OutputRow80Sha256}");
Console.WriteLine(
    $"outputEnvironmentSha256={first.Receipt.OutputEnvironmentSha256}");
Console.WriteLine($"outputOcclusionSha256={first.Receipt.OutputOcclusionSha256}");
Console.WriteLine($"collisionSha256={first.Receipt.CollisionSha256}");
Console.WriteLine(
    $"stageChangedLogicalBytes={first.Receipt.StageChangedLogicalByteCount}");
Console.WriteLine($"changedRawSectors={first.Receipt.ChangedRawSectorCount}");
Console.WriteLine(
    $"changedPhysicalImageBytes={first.Receipt.ChangedPhysicalImageBytes}");
Console.WriteLine($"reusedOnSecondCall={repeated.ReusedExistingCandidate}");
Console.WriteLine("runtimeProven=false");
Console.WriteLine("normalCreateBinEnabled=false");
Console.WriteLine("releaseIntegrated=false");

static async Task<FileSnapshot> SnapshotAsync(string path)
{
    FileInfo info = new(path);
    if (!info.Exists)
        throw new FileNotFoundException("A required parent candidate artifact is missing.", path);
    await using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        1 << 20,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
    string sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    info.Refresh();
    return new(
        Path.GetFullPath(path),
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        sha256);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

internal sealed record FileSnapshot(
    string Path,
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
