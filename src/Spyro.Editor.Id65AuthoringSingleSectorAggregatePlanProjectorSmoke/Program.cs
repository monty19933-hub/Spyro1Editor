using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.FirstOrDefault());
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
Require(File.Exists(lockedImagePath) && File.Exists(lockedCuePath),
    "Missing exact single-sector aggregate locked fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringSingleSectorAggregatePlanProjectorSmoke-" +
        Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            Path.Combine(smokeRoot, "workspace-container"));
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(lockedImagePath, paths.LockedBaseImagePath, overwrite: false);
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        DiscImage.BuildCueText(
            lockedCuePath,
            Path.GetFileName(paths.LockedBaseImagePath)),
        Encoding.ASCII);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
        await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
            .BuildStaticPlan(paths.LockedBaseImagePath);
    Id65AuthoringEditorDocument document =
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.RedGemT21,
                Id65RemotePadGrassT107Catalog.Positions[^1].PositionId,
                [
                    Id65AuthoringTerrainSetCatalog.Tile1Id,
                    Id65AuthoringTerrainSetCatalog.Tile2Id
                ]);
    Id65AuthoringEditorDocumentStoreResult saved =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            document,
            new Id65AuthoringEditorDocumentProjectionInputs(null),
            expectedPriorEnvelopeCanonicalSha256: null);
    Id65AuthoringEditorDocumentStoreResult stored =
        await Id65AuthoringEditorDocumentStore.LoadAsync(
            paths,
            manifest,
            new Id65AuthoringEditorDocumentProjectionInputs(null))
        ?? throw new InvalidOperationException(
            "The saved schema-6 input document disappeared.");
    Require(saved.EnvelopeCanonicalSha256 == stored.EnvelopeCanonicalSha256 &&
            saved.Document.SerializeUtf8().SequenceEqual(
                stored.Document.SerializeUtf8()),
        "The schema-6 store round trip changed.");

    Id65AuthoringSingleSectorAggregatePlanRequest request = new(
        stored,
        stored.EnvelopeCanonicalSha256,
        manifest,
        nativeInputs,
        v3Template);
    Id65CompiledAuthoringSingleSectorSubstrate direct =
        Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
            document,
            nativeInputs.LockedSource,
            v3Template,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle));
    Id65AuthoringSingleSectorAggregatePlan first =
        Id65AuthoringSingleSectorAggregatePlanProjector.Project(request);
    Id65AuthoringSingleSectorAggregatePlan repeat =
        Id65AuthoringSingleSectorAggregatePlanProjector.Project(request);

    byte[] source = first.CopySourceRow80();
    byte[] schema6 = first.CopySchema6BaselineRow80();
    byte[] output = first.CopyOutputRow80();
    byte[] forward =
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyExact(
            first,
            source,
            reverse: false);
    byte[] restored =
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyExact(
            first,
            forward,
            reverse: true);
    byte[] stageForward =
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyStageExact(
            first,
            schema6,
            reverse: false);
    byte[] stageRestored =
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyStageExact(
            first,
            stageForward,
            reverse: true);

    Require(
        first.ProfileId ==
            Id65AuthoringSingleSectorAggregatePlanProjector.ProfileId &&
        first.SchemaVersion == 1 && first.DocumentCanonicalSha256 ==
            document.CanonicalSha256 &&
        first.ActiveTerrainUnitIds.SequenceEqual(
            document.ActiveTerrainUnitIds) &&
        first.MobyKind == Id65RemotePadMobyKind.RedGemT21 &&
        first.MobyPositionId ==
            Id65RemotePadGrassT107Catalog.Positions[^1].PositionId &&
        first.SourceRow80Sha256 == direct.SourceRow80Sha256 &&
        first.Schema6BaselineRow80Sha256 ==
            direct.Schema6BaselineRow80Sha256 &&
        first.OutputRow80Sha256 == direct.OutputRow80Sha256 &&
        first.OutputModelSha256 == direct.OutputModelSha256 &&
        first.CompilerOwnedRangeMapSha256 == direct.OwnedRangeMapSha256 &&
        first.CompilerTransactionSha256 == direct.TransactionSha256 &&
        first.CompilerStageOwnedRangeMapSha256 ==
            direct.StageOwnedRangeMapSha256 &&
        first.CompilerStageTransactionSha256 ==
            direct.StageTransactionSha256 &&
        first.CompilerHandleRebaseMapSha256 == direct.HandleRebaseMapSha256 &&
        first.CompilerDeterministicPlanSha256 ==
            direct.DeterministicPlanSha256 &&
        first.Environment.LogicalSourceSectorIndex == 216 &&
        first.Environment.PhysicalOutputSectorIndex == 0 &&
        first.Environment.ZeroGapSha256 ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedZeroGapSha256 &&
        first.Occlusion.OutputOcclusionSha256 ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedOutputOcclusionSha256 &&
        first.OwnedRanges.Any(item => item.Domain ==
            Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorEnvironment) &&
        first.OwnedRanges.Any(item => item.Domain ==
            Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorOcclusion) &&
        first.StageOwnedRanges.All(item => item.Domain is
            Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorEnvironment or
            Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorOcclusion) &&
        first.GuardedByteCount + first.ProtectedByteCount == 0x2E2000 &&
        first.AtomicGroups.Count == 1 &&
        first.AtomicGroups[0].OwnedRangeCount == first.OwnedRangeCount &&
        forward.SequenceEqual(output) && output.SequenceEqual(
            direct.CopyOutputRow80()) && restored.SequenceEqual(source) &&
        stageForward.SequenceEqual(output) &&
        stageRestored.SequenceEqual(schema6) &&
        Equivalent(first, repeat) &&
        first.PureInMemory && first.CompleteLockedSourceTransactionCarried &&
        first.ExactSchema6StageDeltaCarried &&
        first.ContainsSourceRow80Bytes && first.ContainsOutputRow80Bytes &&
        !first.PartialRangeApplicationAuthorized &&
        !first.InheritedCollisionLookupsRetired &&
        first.CollisionPreservedExactly && !first.WritesFileSystem &&
        !first.MaterializerIntegrated && !first.CreateBinEnabled &&
        !first.RuntimeCandidateAuthorized && !first.RuntimeProven &&
        !first.ReleaseIntegrated && !first.Publishable,
        "The schema-7 aggregate identity, ledgers, inverse, or authority changed.");

    byte[] leakedOutput = first.CopyOutputRow80();
    leakedOutput[0] ^= 0xFF;
    byte[] leakedBefore = first.OwnedRanges[0].CopyBefore();
    leakedBefore[0] ^= 0xFF;
    Require(first.CopyOutputRow80().SequenceEqual(output) &&
            first.OwnedRanges[0].BeforeSha256 ==
                Id65AuthoringSingleSectorAggregatePlanProjector.Hash(
                    first.OwnedRanges[0].CopyBefore()),
        "The schema-7 aggregate leaked mutable row or range bytes.");

    byte[] wrongSource = source.ToArray();
    wrongSource[0] ^= 1;
    Reject("wrong locked-source row", () =>
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyExact(
            first,
            wrongSource,
            reverse: false));
    byte[] wrongStage = schema6.ToArray();
    wrongStage[0] ^= 1;
    Reject("wrong schema-6 stage row", () =>
        Id65AuthoringSingleSectorAggregatePlanProjector.ApplyStageExact(
            first,
            wrongStage,
            reverse: false));
    Reject("wrong stored envelope", () =>
        Id65AuthoringSingleSectorAggregatePlanProjector.Project(new(
            stored,
            new string('0', 64),
            manifest,
            nativeInputs,
            v3Template)));

    Console.WriteLine(
        "ID65_AUTHORING_SINGLE_SECTOR_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
        $"owned={first.OwnedRangeCount} stage_owned={first.StageOwnedRangeCount} " +
        $"protected={first.ProtectedRangeCount} changed={first.ChangedByteCount} " +
        $"stage_changed={first.StageChangedByteCount} " +
        $"output={first.OutputRow80Sha256} " +
        $"aggregate={first.DeterministicAggregatePlanSha256} " +
        "source_copy=true output_copy=true inverse=true stage_inverse=true " +
        "environment_sector=216->0 collision_preserved=true filesystem=false " +
        "materializer=false candidate=false runtime=false release=false");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore,
    "The single-sector aggregate smoke changed its locked fixture.");

static bool Equivalent(
    Id65AuthoringSingleSectorAggregatePlan left,
    Id65AuthoringSingleSectorAggregatePlan right) =>
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.Schema6BaselineRow80Sha256 == right.Schema6BaselineRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.InverseRangeMapSha256 == right.InverseRangeMapSha256 &&
    left.StageOwnedRangeMapSha256 == right.StageOwnedRangeMapSha256 &&
    left.StageInverseRangeMapSha256 == right.StageInverseRangeMapSha256 &&
    left.ProtectedRangeMapSha256 == right.ProtectedRangeMapSha256 &&
    left.AtomicGroupMapSha256 == right.AtomicGroupMapSha256 &&
    left.HandleRebaseMapSha256 == right.HandleRebaseMapSha256 &&
    left.DeterministicAggregatePlanSha256 ==
        right.DeterministicAggregatePlanSha256 &&
    left.CopySourceRow80().SequenceEqual(right.CopySourceRow80()) &&
    left.CopySchema6BaselineRow80().SequenceEqual(
        right.CopySchema6BaselineRow80()) &&
    left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80()) &&
    left.OwnedRanges.Select(RangeIdentity)
        .SequenceEqual(right.OwnedRanges.Select(RangeIdentity)) &&
    left.StageOwnedRanges.Select(RangeIdentity)
        .SequenceEqual(right.StageOwnedRanges.Select(RangeIdentity));

static string RangeIdentity(
    Id65AuthoringSingleSectorAggregateOwnedRange range) => string.Join('|',
[
    range.StableId,
    range.OwnerId,
    range.Domain.ToString(),
    range.DataRelativeOffset.ToString(),
    range.ByteLength.ToString(),
    range.BeforeSha256,
    range.AfterSha256
]);

static void Reject(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (
        exception is InvalidDataException or ArgumentException or
        KeyNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException(label + " was not rejected.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(
                current.FullName,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(info.Length, info.LastWriteTimeUtc, Hash(File.ReadAllBytes(path)));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

internal sealed record FileState(
    long Length,
    DateTime LastWriteUtc,
    string Sha256);
