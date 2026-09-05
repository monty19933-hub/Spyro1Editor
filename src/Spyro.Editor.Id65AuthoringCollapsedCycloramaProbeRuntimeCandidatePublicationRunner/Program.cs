using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

if (args.Length is < 2 or > 3)
{
    throw new ArgumentException(
        "Usage: <workspace-container> <exact-schema7-candidate-directory> [collapsed-probe-output-root]");
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
        "materialized-collapsed-35-part-cyclorama-probe-schema7-candidates");

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

Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(new(
        stored.Document,
        nativeInputs.LockedSource,
        v3Template,
        nativeInputs.PrivateTextureWitness,
        nativeInputs.GrassBundle));
Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
    .ValidateCompiledProbeForSmoke(compiled);
Require(
    stored.Document.SchemaVersion ==
        Id65AuthoringEditorDocument
            .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
    stored.Document.ActiveTerrainUnitIds.SequenceEqual(
        [Id65AuthoringTerrainSetCatalog.Tile1Id]) &&
    stored.Document.RemotePadMobyKind ==
        Id65RemotePadMobyKind.GrassT107 &&
    stored.Document.RemotePadMobyPositionId ==
        Id65RemotePadGrassT107Catalog.CenterPositionId,
    "The real-workspace publication runner requires current Tile1 / Grass T107 / Center state.");
Require(
    compiled.Schema7BaselineRow80Sha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedSchema7Tile1GrassCenterRow80Sha256 &&
    compiled.OutputRow80Sha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedOutputRow80Sha256 &&
    compiled.BaselineCycloramaSha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedSchema7CycloramaSha256 &&
    compiled.OutputCycloramaSha256 ==
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .ExpectedOutputCycloramaSha256,
    "The reconstructed collapsed-cyclorama probe lost its exact hash pins.");

string parentImagePath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
        .CandidateImageFileName);
string parentCuePath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
        .CandidateCueFileName);
string parentReceiptPath = Path.Combine(
    parentCandidateDirectory,
    Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
        .CandidateReceiptFileName);
FileSnapshot[] parentBefore = await Task.WhenAll(
    new[] { parentImagePath, parentCuePath, parentReceiptPath }
        .Select(SnapshotAsync));

Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateResult first =
    await Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .PublishAsync(new(
            parentCandidateDirectory,
            outputRoot,
            compiled));
Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateResult repeated =
    await Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .PublishAsync(new(
            parentCandidateDirectory,
            outputRoot,
            compiled));

FileSnapshot[] parentAfter = await Task.WhenAll(
    new[] { parentImagePath, parentCuePath, parentReceiptPath }
        .Select(SnapshotAsync));
Require(
    parentBefore.SequenceEqual(parentAfter),
    "Collapsed-cyclorama probe publication changed its immutable schema-7 p1 parent artifacts.");
Require(
    first.Paths == repeated.Paths &&
    first.Receipt == repeated.Receipt &&
    first.ReceiptFileSha256 == repeated.ReceiptFileSha256 &&
    !repeated.CandidateArtifactsWrittenThisCall &&
    repeated.ReusedExistingCandidate &&
    first.Receipt.ParentCandidateImageSha256 == parentBefore[0].Sha256 &&
    first.Receipt.ParentCandidateCueSha256 == parentBefore[1].Sha256 &&
    first.Receipt.ParentCandidateReceiptSha256 == parentBefore[2].Sha256 &&
    first.Receipt.DocumentCanonicalSha256 ==
        compiled.DocumentCanonicalSha256 &&
    first.Receipt.CompilerDeterministicPlanSha256 ==
        compiled.DeterministicPlanSha256 &&
    first.Receipt.Schema7CompilerDeterministicPlanSha256 ==
        compiled.Schema7DeterministicPlanSha256 &&
    first.Receipt.BaselineRow80Sha256 ==
        compiled.Schema7BaselineRow80Sha256 &&
    first.Receipt.OutputRow80Sha256 == compiled.OutputRow80Sha256 &&
    first.Receipt.BaselineCycloramaSha256 ==
        compiled.BaselineCycloramaSha256 &&
    first.Receipt.OutputCycloramaSha256 ==
        compiled.OutputCycloramaSha256 &&
    first.Receipt.OutputGeometrySha256 == compiled.OutputGeometrySha256 &&
    first.Receipt.CandidateCollapsedVertexWordCount == 1_880 &&
    first.Receipt.ChangedVertexWordCount == 1_879 &&
    first.Receipt.ChangedLogicalByteCount == 7_367 &&
    first.Receipt.PolygonChangedByteCount == 0 &&
    first.Receipt.OcclusionChangedByteCount == 0 &&
    first.Receipt.OcclusionTailChangedByteCount == 0 &&
    first.Receipt.ExactParentReceiptVerified &&
    first.Receipt.ExactParentImageVerified &&
    first.Receipt.ExactSchema7ProbePreimageVerified &&
    first.Receipt.ExactSchema7ProbeInverseVerified &&
    first.Receipt.ExactOutputRow80ReadbackVerified &&
    first.Receipt.ExactOutputCycloramaReadbackVerified &&
    first.Receipt.AllThirtyFivePartPointersPreservedExactly &&
    first.Receipt.AllThirtyFivePartHeadersPreservedExactly &&
    first.Receipt.PartCountsPreservedExactly &&
    first.Receipt.ColorTablesPreservedExactly &&
    first.Receipt.PolygonTablesPreservedExactly &&
    first.Receipt.FirstVertexWordsPreservedExactly &&
    first.Receipt.RemainingVertexWordsCollapsedPerPart &&
    first.Receipt.EntireOcclusionComponentPreservedExactly &&
    first.Receipt.OcclusionOpaqueTailPreservedExactly &&
    first.Receipt.CollisionPreservedExactly &&
    first.Receipt.EnvironmentPreservedExactly &&
    first.Receipt.TexturePreservedExactly &&
    first.Receipt.SpawnAndMobyBytesPreservedExactly &&
    first.Receipt.OnlySkyIntersectingRawSectorsChanged &&
    first.Receipt.RawSectorIntegrityVerified &&
    first.Receipt.BcdMsfVerified &&
    first.Receipt.DuplicatedSubheadersVerified &&
    first.Receipt.PhysicalComplementVerified &&
    first.Receipt.ParentSchema7CandidatePreserved &&
    first.Receipt.ContainsFullDiscImage && first.Receipt.ContainsCue &&
    first.Receipt.TransitionalProbeOnly &&
    !first.Receipt.FullyOriginalCyclorama &&
    first.Receipt.SeparateGuardedCandidatePublisherAuthorized &&
    !first.Receipt.RuntimeCandidateAuthorized &&
    !first.Receipt.RuntimeProven && !first.Receipt.AppIntegrated &&
    !first.Receipt.UiIntegrated &&
    !first.Receipt.DocumentPersistenceIntegrated &&
    !first.Receipt.CreateBinEnabled &&
    !first.Receipt.NormalCreateBinEnabled &&
    !first.Receipt.ReleaseIntegrated &&
    !first.Receipt.PromotionAuthorized && !first.Receipt.Publishable &&
    !first.RuntimeProven && !first.NormalCreateBinEnabled &&
    !first.ReleaseIntegrated && !first.Publishable,
    "The published collapsed-cyclorama probe or its authority boundary changed.");
Require(
    File.Exists(first.Paths.CandidateImagePath) &&
    File.Exists(first.Paths.CandidateCuePath) &&
    File.Exists(first.Paths.CandidateReceiptPath),
    "The published collapsed-cyclorama probe artifact set is incomplete.");

Console.WriteLine(
    "ID65_AUTHORING_COLLAPSED_CYCLORAMA_PROBE_RUNTIME_CANDIDATE_PUBLICATION_OK");
Console.WriteLine($"candidateDirectory={first.Paths.CandidateDirectoryPath}");
Console.WriteLine($"candidateImage={first.Paths.CandidateImagePath}");
Console.WriteLine($"candidateCue={first.Paths.CandidateCuePath}");
Console.WriteLine($"candidateReceipt={first.Paths.CandidateReceiptPath}");
Console.WriteLine($"parentImageSha256={first.Receipt.ParentCandidateImageSha256}");
Console.WriteLine($"outputImageSha256={first.Receipt.OutputImageSha256}");
Console.WriteLine($"receiptFileSha256={first.ReceiptFileSha256}");
Console.WriteLine($"baselineRow80Sha256={first.Receipt.BaselineRow80Sha256}");
Console.WriteLine($"outputRow80Sha256={first.Receipt.OutputRow80Sha256}");
Console.WriteLine(
    $"outputCycloramaSha256={first.Receipt.OutputCycloramaSha256}");
Console.WriteLine($"outputGeometrySha256={first.Receipt.OutputGeometrySha256}");
Console.WriteLine($"changedVertexWords={first.Receipt.ChangedVertexWordCount}");
Console.WriteLine($"changedLogicalBytes={first.Receipt.ChangedLogicalByteCount}");
Console.WriteLine($"changedRawSectors={first.Receipt.ChangedRawSectorCount}");
Console.WriteLine(
    $"changedPhysicalImageBytes={first.Receipt.ChangedPhysicalImageBytes}");
Console.WriteLine($"physicalDiffSha256={first.Receipt.PhysicalDiffSha256}");
Console.WriteLine($"reusedOnSecondCall={repeated.ReusedExistingCandidate}");
Console.WriteLine("transitionalProbeOnly=true");
Console.WriteLine("fullyOriginalCyclorama=false");
Console.WriteLine("runtimeProven=false");
Console.WriteLine("normalCreateBinEnabled=false");
Console.WriteLine("releaseIntegrated=false");

static async Task<FileSnapshot> SnapshotAsync(string path)
{
    FileInfo info = new(path);
    if (!info.Exists)
        throw new FileNotFoundException(
            "A required schema-7 p1 parent artifact is missing.", path);
    await using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        1 << 20,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
    string sha256 = Convert.ToHexStringLower(
        await SHA256.HashDataAsync(stream));
    info.Refresh();
    return new(
        Path.GetFullPath(path),
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        sha256);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

internal sealed record FileSnapshot(
    string Path,
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
