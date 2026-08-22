using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string baseImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
Require(File.Exists(baseImagePath), $"Missing exact locked display-name BIN: {baseImagePath}");
string sourceHashBefore = HashFile(baseImagePath);
long sourceLengthBefore = new FileInfo(baseImagePath).Length;
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(baseImagePath);
IReadOnlyDictionary<string, FileState> sourceDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(baseImagePath)!);

UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2Template =
    UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(baseImagePath);
Id65AuthoringManifest manifest = Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
Id65AuthoringManifest reordered = CopyManifest(
    manifest,
    sectors: manifest.Sectors.Reverse(),
    vertices: manifest.Vertices.Reverse(),
    renderFaces: manifest.RenderFaces.Reverse(),
    collision: manifest.Collision.Reverse(),
    occlusion: manifest.Occlusion.Reverse(),
    textures: manifest.Textures.Reverse(),
    mobys: manifest.Mobys.Reverse());
Require(manifest.CanonicalJson == reordered.CanonicalJson &&
        manifest.CanonicalSha256 == reordered.CanonicalSha256,
    "Manifest canonicalization depends on caller collection order.");

Id65TerrainVertexIntent[] externalVertices = manifest.Vertices.ToArray();
Id65AuthoringManifest immutableCopy = CopyManifest(manifest, vertices: externalVertices);
externalVertices[0] = externalVertices[0] with { Point = new(0, 0, 0) };
Require(immutableCopy.Vertices[0] == manifest.Vertices[0] &&
        immutableCopy.CanonicalSha256 == manifest.CanonicalSha256,
    "The manifest retained a mutable caller-owned collection.");
bool readOnlyCollectionObserved = false;
try
{
    ((IList<Id65TerrainVertexIntent>)immutableCopy.Vertices)[0] = externalVertices[0];
}
catch (NotSupportedException)
{
    readOnlyCollectionObserved = true;
}
Require(readOnlyCollectionObserved, "The manifest exposed a mutable vertex collection.");

Id65CompiledAuthoringModel first = Id65AuthoringModelCompiler.Compile(manifest, v2Template);
Id65CompiledAuthoringModel repeat = Id65AuthoringModelCompiler.Compile(reordered, v2Template);

Require(first.ProfileId == Id65AuthoringModelCompiler.ProfileId &&
        first.Manifest.ProfileId == Id65AuthoringModelCompiler.MinimalManifestProfileId &&
        first.ManifestSha256 == manifest.CanonicalSha256 &&
        first.ManifestSha256 == Id65AuthoringModelCompiler.ExpectedMinimalManifestSha256 &&
        first.SourceModelSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceModelSha256 &&
        first.OutputModelSha256 ==
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256,
    "The compiler/base/output identity changed.");
Require(first.ChangedModelByteCount == Id65AuthoringModelCompiler.ExpectedMinimalChangedModelByteCount &&
        first.DiffRangeCount == Id65AuthoringModelCompiler.ExpectedMinimalModelDiffRangeCount &&
        first.DiffManifestSha256 == Id65AuthoringModelCompiler.ExpectedMinimalModelDiffManifestSha256 &&
        first.RelocationMapSha256 == Id65AuthoringModelCompiler.ExpectedMinimalRelocationMapSha256 &&
        first.HandleRebaseMapSha256 == Id65AuthoringModelCompiler.ExpectedMinimalHandleRebaseMapSha256 &&
        first.DeterministicPlanSha256 == Id65AuthoringModelCompiler.ExpectedMinimalDeterministicPlanSha256,
    "A deterministic minimal-v2 compiler pin changed.");
Require(first.RenderFaceNormalZ == 50_176 && first.CollisionNormalZ == -50_176 &&
        first.Manifest.RenderFaces.Single().OrderPolicy ==
            Id65RenderFaceOrderPolicy.DeclaredAbcGpuOrder &&
        first.Manifest.Collision.Single().WindingPolicy ==
            Id65CollisionWindingPolicy.NegativeNormalZAcb &&
        first.Manifest.RenderFaces.Single().LowB != first.Manifest.Collision.Single().B &&
        first.NegativeCollisionWindingVerified && first.RenderAndCollisionOrderSeparated,
    "Render-face order was conflated with the repaired collision winding policy.");
Require(first.Manifest.Sectors.Single().Id == "sector.remote-pad.216" &&
        first.Manifest.Sectors.Single().SectorIndex == 216 &&
        first.Manifest.RenderFaces.Single().Id == "terrain.remote-pad.face.0" &&
        first.Manifest.Collision.Single().Id == "collision.remote-pad.t13995" &&
        first.Manifest.Occlusion.Single().Id == "occlusion.remote-pad.group0" &&
        first.Manifest.Textures.Single().Id == "texture.locked.record25" &&
        first.Manifest.Mobys.Single().Id == "moby.player-anchor.t92" &&
        first.Manifest.Spawn.Id == "spawn.remote-pad" &&
        first.Manifest.Music.Id == "music.slot35" &&
        first.Manifest.Totals.Id == "totals.slot65" &&
        first.Manifest.Exit.Id == "exit.slot65" &&
        first.Manifest.Save.Id == "save.slot65",
    "A stable authoring intent ID changed.");

Id65AuthoringModelCapacityReadback capacity = first.Capacity;
Require(capacity.ModelByteCapacity == 0x94800 &&
        capacity.SourceUsedModelBytes == 0x94508 && capacity.OutputUsedModelBytes == 0x9457C &&
        capacity.EnvironmentGrowthBytes == 0x74 &&
        capacity.SourceZeroTailBytes == 0x2F8 && capacity.OutputZeroTailBytes == 0x284 &&
        capacity.SceneSectorCount == 217 && capacity.CollisionTriangleCount == 19_808 &&
        capacity.CollisionBlocksUsedBytes == 0x17960 &&
        capacity.CollisionBlocksCapacityBytes == 0x17984 &&
        capacity.HighestTextureRecordIndex == 25 && capacity.HighestMobyTrueIndex == 92,
    "The exact minimal-v2 capacity readback changed.");

Require(first.Relocations.Count == 9 &&
        first.Relocations[0].StableId == "component.texture" &&
        first.Relocations[0].SourceRelativeOffset == 0 &&
        first.Relocations[0].OutputRelativeOffset == 0 &&
        first.Relocations.Single(item => item.StableId == "component.environment") is var environment &&
        environment.SourceRelativeOffset == 0x2F78 && environment.SourceByteLength == 0x284A4 &&
        environment.OutputRelativeOffset == 0x2F78 && environment.OutputByteLength == 0x28518 &&
        first.Relocations.Single(item => item.StableId == "component.collision") is var collisionComponent &&
        collisionComponent.SourceRelativeOffset == 0x2BDC0 &&
        collisionComponent.OutputRelativeOffset == 0x2BE34 &&
        collisionComponent.SourceByteLength == 0x5FAE8 && collisionComponent.OutputByteLength == 0x5FAE8 &&
        first.Relocations[^1].StableId == "component.sound" &&
        first.Relocations[^1].OutputRelativeOffset + first.Relocations[^1].OutputByteLength == 0x9457C,
    "The exact component relocation map changed.");
Require(first.HandleRebases.Count == 18 &&
        first.HandleRebases.Single(item => item.StableId == "sector.remote-pad.216").Source is null &&
        first.HandleRebases.Single(item => item.StableId == "sector.remote-pad.216").Output ==
            new Id65LogicalAddress("scene-sector", 216, 0) &&
        first.HandleRebases.Single(item => item.StableId == "collision.remote-pad.t13995").Source ==
            new Id65LogicalAddress("collision-triangle", 13_995, 0) &&
        first.HandleRebases.Single(item => item.StableId == "collision.remote-pad.t13995").Output ==
            new Id65LogicalAddress("collision-triangle", 13_995, 0),
    "The exact stable-handle rebase map changed.");
Require(Id65AuthoringModelCompiler.InvertRelocations(
            Id65AuthoringModelCompiler.InvertRelocations(first.Relocations)).SequenceEqual(first.Relocations) &&
        Id65AuthoringModelCompiler.InvertHandleRebases(
            Id65AuthoringModelCompiler.InvertHandleRebases(first.HandleRebases)).SequenceEqual(first.HandleRebases),
    "A relocation/rebase double inverse did not restore the exact map.");

byte[] sourceModel = first.CopySourceModel();
byte[] outputModel = Id65AuthoringModelCompiler.ApplyModelTransactional(first, sourceModel, reverse: false);
byte[] inverseModel = Id65AuthoringModelCompiler.ApplyModelTransactional(first, outputModel, reverse: true);
Require(outputModel.SequenceEqual(first.CopyOutputModel()) && inverseModel.SequenceEqual(sourceModel),
    "The compiled minimal-v2 model did not round-trip exactly.");
byte[] tamperedSource = sourceModel.ToArray();
tamperedSource[0x2F78] ^= 0x01;
ExpectReject(
    "transactional model preimage",
    () => Id65AuthoringModelCompiler.ApplyModelTransactional(first, tamperedSource, reverse: false),
    "preimage changed");
byte[] tamperedOutput = outputModel.ToArray();
tamperedOutput[UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.TriangleModelRelativeOffset + 2] ^= 0x01;
ExpectReject(
    "transactional model afterimage",
    () => Id65AuthoringModelCompiler.ApplyModelTransactional(first, tamperedOutput, reverse: true),
    "afterimage changed");

ExpectReject(
    "locked base conflict",
    () => Id65AuthoringModelCompiler.Compile(
        CopyManifest(manifest, lockedBase: manifest.LockedBase with { ModelSha256 = new string('0', 64) }),
        v2Template),
    "locked display-name base");
ExpectReject(
    "global stable ID conflict",
    () => Id65AuthoringModelCompiler.Compile(
        CopyManifest(manifest, vertices:
        [manifest.Vertices[0] with { Handle = manifest.Sectors[0].Id }, .. manifest.Vertices.Skip(1)]),
        v2Template),
    "globally unique");
Id65CollisionIntent wrongWinding = manifest.Collision.Single() with
{
    B = manifest.RenderFaces.Single().LowB,
    C = manifest.RenderFaces.Single().LowC
};
ExpectReject(
    "render/collision order conflict",
    () => Id65AuthoringModelCompiler.Compile(CopyManifest(manifest, collision: [wrongWinding]), v2Template),
    "collision intents");
ExpectReject(
    "unproven music authorship conflict",
    () => Id65AuthoringModelCompiler.Compile(
        CopyManifest(manifest, music: manifest.Music with
        {
            Disposition = Id65AuthoringIntentDisposition.AuthorExact,
            TrackId = 0
        }),
        v2Template),
    "ownership intent");

int capacityRejectionCount = 0;
ExpectCapacityReject(new(ModelByteCapacity: 0x947FF), "model byte capacity");
ExpectCapacityReject(new(AvailableModelTailBytes: 0x73), "model tail capacity");
ExpectCapacityReject(new(MaximumOutputUsedModelBytes: 0x9457B), "used-model capacity");
ExpectCapacityReject(new(MaximumSceneSectorCount: 216), "scene-sector capacity");
ExpectCapacityReject(new(CollisionBlockCapacityBytes: 0x1795F), "collision-block capacity");
ExpectCapacityReject(new(MaximumCollisionTriangleCount: 19_807), "collision-triangle capacity");
ExpectCapacityReject(new(MaximumTextureRecordIndex: 24), "texture-record capacity");
ExpectCapacityReject(new(MaximumMobyTrueIndex: 91), "Moby-row capacity");
Require(capacityRejectionCount == 8, "The full capacity rejection matrix did not run.");

Require(first.ManifestSha256 == repeat.ManifestSha256 &&
        first.OutputModelSha256 == repeat.OutputModelSha256 &&
        first.ChangedModelByteCount == repeat.ChangedModelByteCount &&
        first.DiffRangeCount == repeat.DiffRangeCount &&
        first.DiffManifestSha256 == repeat.DiffManifestSha256 &&
        first.Relocations.SequenceEqual(repeat.Relocations) &&
        first.RelocationMapSha256 == repeat.RelocationMapSha256 &&
        first.HandleRebases.SequenceEqual(repeat.HandleRebases) &&
        first.HandleRebaseMapSha256 == repeat.HandleRebaseMapSha256 &&
        first.DeterministicPlanSha256 == repeat.DeterministicPlanSha256,
    "Two in-memory compiles were not deterministic.");
Require(first.ImmutableLockedDisplayNameBaseVerified && first.StableIntentIdsVerified &&
        first.ExplicitHpLpPairingVerified && first.CapacityAndConflictValidationComplete &&
        first.RelocationMapExactInverseVerified && first.HandleRebaseMapExactInverseVerified &&
        first.ModelExactInverseVerified && first.DeterministicReadbackRequired &&
        !first.WritesFileSystem && !first.WritesDiscImage && !first.WritesCue && !first.AppIntegrated &&
        !first.CreateBinEnabled && !first.NormalCreateBinEnabled && !first.ReleaseIntegrated &&
        !first.RuntimeCandidateAuthorized && !first.RetiredPublisherCalled &&
        !first.PromotionAuthorized && !first.Publishable,
    "A static-only/no-writer/no-promotion safety flag changed.");

Require(sourceHashBefore == HashFile(baseImagePath) &&
        sourceLengthBefore == new FileInfo(baseImagePath).Length &&
        sourceWriteBefore == File.GetLastWriteTimeUtc(baseImagePath) &&
        SnapshotsEqual(sourceDirectoryBefore, SnapshotDirectory(Path.GetDirectoryName(baseImagePath)!)),
    "The in-memory authoring compiler changed or published beside the locked base.");

Console.WriteLine(
    "PASS Id65AuthoringManifestCompilerSmoke: " +
    $"manifest={first.ManifestSha256}; model={first.OutputModelSha256}; " +
    $"changed={first.ChangedModelByteCount}; ranges={first.DiffRangeCount}; diff={first.DiffManifestSha256}; " +
    $"relocations={first.RelocationMapSha256}; rebases={first.HandleRebaseMapSha256}; " +
    $"plan={first.DeterministicPlanSha256}; rejects={capacityRejectionCount + 6}; " +
    "pure in-memory, exact inverse, no writer/CUE/App/CreateBIN/release/promotion.");

void ExpectCapacityReject(Id65AuthoringCompilerLimits limits, string requiredText)
{
    ExpectReject(
        requiredText,
        () => Id65AuthoringModelCompiler.Compile(manifest, v2Template, limits),
        requiredText);
    capacityRejectionCount++;
}

static Id65AuthoringManifest CopyManifest(
    Id65AuthoringManifest source,
    string? profileId = null,
    Id65LockedDisplayNameBase? lockedBase = null,
    IEnumerable<Id65SectorIntent>? sectors = null,
    IEnumerable<Id65TerrainVertexIntent>? vertices = null,
    IEnumerable<Id65RenderFaceIntent>? renderFaces = null,
    IEnumerable<Id65CollisionIntent>? collision = null,
    IEnumerable<Id65OcclusionIntent>? occlusion = null,
    IEnumerable<Id65TextureIntent>? textures = null,
    IEnumerable<Id65MobyIntent>? mobys = null,
    Id65SpawnIntent? spawn = null,
    Id65MusicIntent? music = null,
    Id65TotalsIntent? totals = null,
    Id65ExitIntent? exit = null,
    Id65SaveIntent? save = null) =>
    new(
        profileId ?? source.ProfileId,
        lockedBase ?? source.LockedBase,
        sectors ?? source.Sectors,
        vertices ?? source.Vertices,
        renderFaces ?? source.RenderFaces,
        collision ?? source.Collision,
        occlusion ?? source.Occlusion,
        textures ?? source.Textures,
        mobys ?? source.Mobys,
        spawn ?? source.Spawn,
        music ?? source.Music,
        totals ?? source.Totals,
        exit ?? source.Exit,
        save ?? source.Save);

static void ExpectReject(string label, Action action, string requiredText)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (Exception ex) when (ex.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase))
    {
        rejected = true;
    }
    Require(rejected, $"Atomic rejection `{label}` was not observed with text `{requiredText}`.");
}

static IReadOnlyDictionary<string, FileState> SnapshotDirectory(string path) =>
    Directory.GetFiles(path)
        .Order(StringComparer.Ordinal)
        .ToDictionary(
            file => Path.GetFileName(file),
            file => new FileState(
                new FileInfo(file).Length,
                File.GetLastWriteTimeUtc(file),
                HashFile(file)),
            StringComparer.Ordinal);

static bool SnapshotsEqual(
    IReadOnlyDictionary<string, FileState> left,
    IReadOnlyDictionary<string, FileState> right) =>
    left.Count == right.Count && left.All(pair =>
        right.TryGetValue(pair.Key, out FileState? value) && value == pair.Value);

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);
