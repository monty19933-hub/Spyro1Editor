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
Id65TerrainAuthoringManifest substrate =
    Id65AuthoringTerrainMutationCompiler.CreateV2SubstrateManifest();
Id65TerrainTriangleIntent witness =
    Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();
Id65TerrainAuthoringManifest twoTile =
    Id65AuthoringTerrainMutationCompiler.AddTriangle(substrate, witness);
Id65TerrainAuthoringManifest removed =
    Id65AuthoringTerrainMutationCompiler.RemoveTriangle(twoTile, witness.Id);

Require(removed.CanonicalJson == substrate.CanonicalJson &&
        removed.CanonicalSha256 == substrate.CanonicalSha256,
    "Remove(Add(v2, triangle)) did not restore the exact canonical substrate manifest.");
Id65TerrainAuthoringManifest readded =
    Id65AuthoringTerrainMutationCompiler.AddTriangle(removed, witness);
Require(readded.CanonicalJson == twoTile.CanonicalJson &&
        readded.CanonicalSha256 == twoTile.CanonicalSha256,
    "Add(Remove(two-tile, triangle)) did not restore the exact canonical two-tile manifest.");

Id65TerrainTriangleIntent[] callerOwned = [witness];
Id65TerrainAuthoringManifest defensive = new(
    Id65AuthoringTerrainMutationCompiler.ProfileId,
    substrate.LockedV2Base,
    callerOwned);
callerOwned[0] = witness with { Id = "tile.caller-mutated" };
Require(defensive.OptionalTriangles.Single() == witness &&
        defensive.CanonicalSha256 == twoTile.CanonicalSha256,
    "The terrain manifest retained a mutable caller-owned collection.");
bool readOnlyObserved = false;
try
{
    ((IList<Id65TerrainTriangleIntent>)defensive.OptionalTriangles)[0] = callerOwned[0];
}
catch (NotSupportedException)
{
    readOnlyObserved = true;
}
Require(readOnlyObserved, "The terrain manifest exposed a mutable optional-triangle collection.");

Id65CompiledTerrainMutation add = Id65AuthoringTerrainMutationCompiler.CompileTransition(
    substrate,
    twoTile,
    v2Template);
Id65CompiledTerrainMutation repeat = Id65AuthoringTerrainMutationCompiler.CompileTransition(
    substrate,
    readded,
    v2Template);
Id65CompiledTerrainMutation remove = Id65AuthoringTerrainMutationCompiler.CompileTransition(
    twoTile,
    removed,
    v2Template);
Id65CompiledTerrainMutation inverted = Id65AuthoringTerrainMutationCompiler.Invert(add);
Id65CompiledTerrainMutation doubleInverted = Id65AuthoringTerrainMutationCompiler.Invert(inverted);

Require(add.Kind == Id65TerrainMutationKind.AddTriangle &&
        remove.Kind == Id65TerrainMutationKind.RemoveTriangle &&
        substrate.CanonicalSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedSubstrateManifestSha256 &&
        twoTile.CanonicalSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileManifestSha256 &&
        add.SourceModelSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedV2ModelSha256 &&
        add.OutputModelSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileModelSha256 &&
        remove.SourceModelSha256 == add.OutputModelSha256 &&
        remove.OutputModelSha256 == add.SourceModelSha256,
    "The add/remove model identities changed.");
Require(add.ChangedModelByteCount ==
            Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileChangedModelByteCount &&
        add.DiffRangeCount == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileDiffRangeCount &&
        add.ChangedModelByteCount == remove.ChangedModelByteCount &&
        add.DiffRangeCount == remove.DiffRangeCount &&
        add.DiffManifestSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedAddDiffManifestSha256 &&
        add.RelocationMapSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedAddRelocationMapSha256 &&
        add.HandleRebaseMapSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedAddHandleRebaseMapSha256 &&
        add.DeterministicPlanSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedAddDeterministicPlanSha256 &&
        remove.DiffManifestSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedRemoveDiffManifestSha256 &&
        remove.RelocationMapSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedRemoveRelocationMapSha256 &&
        remove.HandleRebaseMapSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedRemoveHandleRebaseMapSha256 &&
        remove.DeterministicPlanSha256 ==
            Id65AuthoringTerrainMutationCompiler.ExpectedRemoveDeterministicPlanSha256,
    "The exact add/remove model diff boundary changed.");
Require(Equivalent(remove, inverted) && Equivalent(add, doubleInverted),
    "Compiled removal or double inverse did not restore the exact terrain transaction.");
Require(Equivalent(add, repeat), "Repeated terrain compilation was not deterministic.");

Id65TerrainCapacityReadback capacity = add.Capacity;
Require(capacity.ModelByteCapacity == 0x94800 &&
        capacity.SourceUsedModelBytes == 0x9457C && capacity.OutputUsedModelBytes == 0x945AC &&
        capacity.SourceZeroTailBytes == 0x284 && capacity.OutputZeroTailBytes == 0x254 &&
        capacity.EnvironmentSourceBytes == 0x28518 && capacity.EnvironmentOutputBytes == 0x28538 &&
        capacity.CollisionSourceBytes == 0x5FAE8 && capacity.CollisionOutputBytes == 0x5FAF8 &&
        capacity.SourceLowDetailVertexCount == 3 && capacity.OutputLowDetailVertexCount == 4 &&
        capacity.SourceLowDetailFaceCount == 1 && capacity.OutputLowDetailFaceCount == 2 &&
        capacity.SourceHighDetailVertexCount == 3 && capacity.OutputHighDetailVertexCount == 4 &&
        capacity.SourceHighDetailFaceCount == 1 && capacity.OutputHighDetailFaceCount == 2 &&
        capacity.SourceCollisionTriangleCount == 19_808 && capacity.OutputCollisionTriangleCount == 19_809 &&
        capacity.SourceCollisionBlocksUsedBytes == 0x17960 &&
        capacity.OutputCollisionBlocksUsedBytes == 0x17962 &&
        capacity.CollisionTreeCapacityBytes == 0x6A60 && capacity.CollisionBlocksCapacityBytes == 0x17984,
    "The exact two-tile capacity readback changed.");
Require(remove.Capacity == capacity.Invert(),
    "The remove capacity readback is not the exact inverse of add.");

Id65TerrainTriangleReadback triangle = add.Triangle;
Require(triangle.TriangleId == "tile.remote-pad.1" &&
        triangle.AddedVertexHandle == "vertex.remote-pad.d" &&
        !triangle.PresentInSource && triangle.PresentInOutput &&
        triangle.SectorIndex == 216 &&
        triangle.LowDetailVertexIndex == 3 && triangle.HighDetailVertexIndex == 3 &&
        triangle.LowDetailFaceIndex == 1 && triangle.HighDetailFaceIndex == 1 &&
        triangle.CollisionTriangleIndex == 19_808 &&
        triangle.AddedVertexWordHex == "00C0031E" &&
        triangle.LowDetailFaceHex == "0082300400821000" &&
        triangle.HighDetailFaceHex == "010103020101000219024001080A1000" &&
        triangle.CollisionTriangleHex == "F00164001001387000020000" &&
        triangle.RenderNormalZ == 25_088 && triangle.CollisionNormalZ == -25_088 &&
        triangle.CollisionCell == new Id65AuthoringCollisionCell(1, 1, 2) &&
        triangle.TargetCellSequence.SequenceEqual([19_808, 13_995]) &&
        triangle.OcclusionGroupIndex == 0,
    "The concrete HP/LP/collision/cell/occlusion witness changed.");

Require(add.Relocations.Count == 9 &&
        add.Relocations.Single(item => item.StableId == "component.environment") is var environment &&
        environment.SourceRelativeOffset == 0x2F78 && environment.SourceByteLength == 0x28518 &&
        environment.OutputRelativeOffset == 0x2F78 && environment.OutputByteLength == 0x28538 &&
        add.Relocations.Single(item => item.StableId == "component.collision") is var collision &&
        collision.SourceRelativeOffset == 0x2BE34 && collision.SourceByteLength == 0x5FAE8 &&
        collision.OutputRelativeOffset == 0x2BE54 && collision.OutputByteLength == 0x5FAF8 &&
        add.Relocations.Single(item => item.StableId == "component.sound") is var sound &&
        sound.SourceRelativeOffset == 0x93E84 && sound.OutputRelativeOffset == 0x93EB4 &&
        sound.OutputRelativeOffset + sound.OutputByteLength == 0x945AC,
    "The exact terrain component relocation map changed.");
Require(add.HandleRebases.Count == 24 &&
        add.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1").Source is null &&
        add.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1").Output ==
            new Id65LogicalAddress("terrain-tile", 216, 1) &&
        add.HandleRebases.Single(item => item.StableId == "vertex.remote-pad.d:lp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 3) &&
        add.HandleRebases.Single(item => item.StableId == "vertex.remote-pad.d:hp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 3) &&
        add.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1:lp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 1) &&
        add.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1:hp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 1) &&
        add.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1:collision").Output ==
            new Id65LogicalAddress("collision-triangle", 19_808, 0),
    "The exact terrain stable-handle rebase map changed.");
Require(Id65AuthoringModelCompiler.InvertRelocations(
            Id65AuthoringModelCompiler.InvertRelocations(add.Relocations)).SequenceEqual(add.Relocations) &&
        Id65AuthoringModelCompiler.InvertHandleRebases(
            Id65AuthoringModelCompiler.InvertHandleRebases(add.HandleRebases)).SequenceEqual(add.HandleRebases),
    "A terrain relocation/rebase double inverse did not restore the exact map.");

byte[] v2Model = add.CopySourceModel();
byte[] twoTileModel = Id65AuthoringTerrainMutationCompiler.ApplyTransactional(add, v2Model, reverse: false);
byte[] v2RoundTrip = Id65AuthoringTerrainMutationCompiler.ApplyTransactional(add, twoTileModel, reverse: true);
Require(twoTileModel.SequenceEqual(add.CopyOutputModel()) && v2RoundTrip.SequenceEqual(v2Model),
    "The terrain model transaction did not round-trip exactly.");
byte[] tamperedV2 = v2Model.ToArray();
tamperedV2[0x2F78] ^= 0x01;
ExpectReject("forward preimage tamper",
    () => Id65AuthoringTerrainMutationCompiler.ApplyTransactional(add, tamperedV2, reverse: false),
    "preimage changed");
byte[] tamperedTwoTile = twoTileModel.ToArray();
tamperedTwoTile[0x2F78] ^= 0x01;
ExpectReject("reverse afterimage tamper",
    () => Id65AuthoringTerrainMutationCompiler.ApplyTransactional(add, tamperedTwoTile, reverse: true),
    "afterimage changed");

int rejectionCount = 2;
void Reject(string label, Action action, string text)
{
    ExpectReject(label, action, text);
    rejectionCount++;
}

Reject("identity transition",
    () => Id65AuthoringTerrainMutationCompiler.CompileTransition(substrate, substrate, v2Template),
    "add or remove exactly one");
Reject("duplicate add",
    () => Id65AuthoringTerrainMutationCompiler.AddTriangle(twoTile, witness),
    "already owns");
Reject("missing remove",
    () => Id65AuthoringTerrainMutationCompiler.RemoveTriangle(substrate, witness.Id),
    "not active exactly once");
Reject("wrong remove id",
    () => Id65AuthoringTerrainMutationCompiler.RemoveTriangle(twoTile, "tile.remote-pad.missing"),
    "not active exactly once");
Reject("multiple optional triangles",
    () => Id65AuthoringTerrainMutationCompiler.CompileTransition(
        substrate,
        new(Id65AuthoringTerrainMutationCompiler.ProfileId, substrate.LockedV2Base,
            [witness, witness with { Id = "tile.remote-pad.2" }]),
        v2Template),
    "at most one");
Reject("layer profile drift",
    () => Id65AuthoringTerrainMutationCompiler.CompileTransition(
        new("wrong-profile", substrate.LockedV2Base, []), twoTile, v2Template),
    "minimal-v2 terrain base");
Reject("locked base drift",
    () => Id65AuthoringTerrainMutationCompiler.CompileTransition(
        new(Id65AuthoringTerrainMutationCompiler.ProfileId,
            CopyBase(substrate.LockedV2Base, profileId: "changed-base"), []),
        twoTile,
        v2Template),
    "minimal-v2 terrain base");
Reject("triangle id drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { Id = "tile.remote-pad.changed" }), "exact negative-winding");
Reject("vertex handle drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { AddedVertex = witness.AddedVertex with { Handle = "vertex.remote-pad.e" } }),
    "exact negative-winding");
Reject("point/cull/packing drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { AddedVertex = witness.AddedVertex with { Point = new(512, 496, 512) } }),
    "exact negative-winding");
Reject("LP/HP render topology drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { RenderB = witness.RenderC, RenderC = witness.RenderB }),
    "exact negative-winding");
Reject("positive collision winding", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { CollisionB = witness.RenderB, CollisionC = witness.RenderC }),
    "exact negative-winding");
Reject("texture drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { TextureIntentId = "texture.private.record66" }),
    "exact negative-winding");
Reject("assignment drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { CollisionAssignment = 1 }), "exact negative-winding");
Reject("occlusion drift", () => Id65AuthoringTerrainMutationCompiler.AddTriangle(
        substrate, witness with { OcclusionGroupIndex = 1 }), "exact negative-winding");

void CapacityReject(Id65TerrainCompilerLimits limits, string text)
{
    Reject(text,
        () => Id65AuthoringTerrainMutationCompiler.CompileTransition(substrate, twoTile, v2Template, limits),
        text);
}
CapacityReject(new(ModelByteCapacity: 0x947FF), "model byte capacity");
CapacityReject(new(AvailableV2TailBytes: 0x2F), "v2 tail capacity");
CapacityReject(new(MaximumOutputUsedModelBytes: 0x945AB), "used-model capacity");
CapacityReject(new(MaximumLowDetailVertexCount: 3), "LP vertex capacity");
CapacityReject(new(MaximumHighDetailVertexCount: 3), "HP vertex capacity");
CapacityReject(new(MaximumSectorFaceCount: 1), "sector face capacity");
CapacityReject(new(MaximumCollisionTriangleCount: 19_808), "collision triangle capacity");
CapacityReject(new(CollisionTreeCapacityBytes: 0x6A5F), "collision tree capacity");
CapacityReject(new(CollisionBlockCapacityBytes: 0x17961), "collision block capacity");

UnusedLevel65RemoteBlankStructuralPatch[] positivePatches = v2Template.StructuralPatches
    .Select(patch => patch.Kind == "id65-model"
        ? patch with
        {
            After = v2Template.FailedV1Plan.StructuralPatches
                .Single(item => item.Kind == "id65-model").After.ToArray()
        }
        : patch)
    .ToArray();
Reject("positive-winding substrate",
    () => Id65AuthoringTerrainMutationCompiler.CompileTransition(
        substrate, twoTile, v2Template with { StructuralPatches = positivePatches }),
    "v2 model");

Require(rejectionCount == 27, $"The full terrain rejection matrix ran {rejectionCount}, expected 27.");
Require(add.ExactV2SubstrateVerified && add.ExactHpLpPairingVerified &&
        add.NegativeCollisionWindingVerified && add.CollisionCellTreeReadbackVerified &&
        add.OcclusionOwnershipVerified && add.CapacityAndRebaseVerified &&
        add.ExactInverseVerified && add.DeterministicReadbackRequired &&
        !add.WritesFileSystem && !add.WritesDiscImage && !add.WritesCue && !add.AppIntegrated &&
        !add.CreateBinEnabled && !add.NormalCreateBinEnabled && !add.ReleaseIntegrated &&
        !add.RuntimeCandidateAuthorized && !add.RetiredComposerCalled &&
        !add.CrossSliceCompositionAuthorized && !add.SinglePassCompositeRebuildOwned &&
        !add.PromotionAuthorized && !add.Publishable,
    "A terrain static-only/no-writer/no-composition/no-promotion flag changed.");

Require(sourceHashBefore == HashFile(baseImagePath) &&
        sourceLengthBefore == new FileInfo(baseImagePath).Length &&
        sourceWriteBefore == File.GetLastWriteTimeUtc(baseImagePath) &&
        SnapshotsEqual(sourceDirectoryBefore, SnapshotDirectory(Path.GetDirectoryName(baseImagePath)!)),
    "The pure in-memory terrain compiler changed or published beside the locked base.");

Console.WriteLine(
    "PASS Id65AuthoringTerrainMutationCompilerSmoke: " +
    $"baseManifest={substrate.CanonicalSha256}; twoTileManifest={twoTile.CanonicalSha256}; " +
    $"v2={add.SourceModelSha256}; twoTile={add.OutputModelSha256}; " +
    $"changed={add.ChangedModelByteCount}; ranges={add.DiffRangeCount}; diff={add.DiffManifestSha256}; " +
    $"relocations={add.RelocationMapSha256}; rebases={add.HandleRebaseMapSha256}; " +
    $"plan={add.DeterministicPlanSha256}; removeDiff={remove.DiffManifestSha256}; " +
    $"removeRelocations={remove.RelocationMapSha256}; removeRebases={remove.HandleRebaseMapSha256}; " +
    $"removePlan={remove.DeterministicPlanSha256}; rejects={rejectionCount}; " +
    "pure in-memory exact add/remove inverse; cross-slice composition remains unauthorized pending one composite rebuild; " +
    "no writer/CUE/App/CreateBIN/runtime/release/promotion.");

static bool Equivalent(Id65CompiledTerrainMutation left, Id65CompiledTerrainMutation right) =>
    left.Kind == right.Kind &&
    left.SourceManifestSha256 == right.SourceManifestSha256 &&
    left.OutputManifestSha256 == right.OutputManifestSha256 &&
    left.SourceModelSha256 == right.SourceModelSha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.ChangedModelByteCount == right.ChangedModelByteCount &&
    left.DiffRangeCount == right.DiffRangeCount &&
    left.DiffManifestSha256 == right.DiffManifestSha256 &&
    left.Relocations.SequenceEqual(right.Relocations) &&
    left.RelocationMapSha256 == right.RelocationMapSha256 &&
    left.HandleRebases.SequenceEqual(right.HandleRebases) &&
    left.HandleRebaseMapSha256 == right.HandleRebaseMapSha256 &&
    left.Capacity == right.Capacity &&
    TriangleEquivalent(left.Triangle, right.Triangle) &&
    left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
    left.CopySourceModel().SequenceEqual(right.CopySourceModel()) &&
    left.CopyOutputModel().SequenceEqual(right.CopyOutputModel());

static bool TriangleEquivalent(
    Id65TerrainTriangleReadback left,
    Id65TerrainTriangleReadback right) =>
    left.TriangleId == right.TriangleId &&
    left.AddedVertexHandle == right.AddedVertexHandle &&
    left.PresentInSource == right.PresentInSource &&
    left.PresentInOutput == right.PresentInOutput &&
    left.SectorIndex == right.SectorIndex &&
    left.LowDetailVertexIndex == right.LowDetailVertexIndex &&
    left.HighDetailVertexIndex == right.HighDetailVertexIndex &&
    left.LowDetailFaceIndex == right.LowDetailFaceIndex &&
    left.HighDetailFaceIndex == right.HighDetailFaceIndex &&
    left.CollisionTriangleIndex == right.CollisionTriangleIndex &&
    left.AddedVertexWordHex == right.AddedVertexWordHex &&
    left.LowDetailFaceHex == right.LowDetailFaceHex &&
    left.HighDetailFaceHex == right.HighDetailFaceHex &&
    left.CollisionTriangleHex == right.CollisionTriangleHex &&
    left.RenderNormalZ == right.RenderNormalZ &&
    left.CollisionNormalZ == right.CollisionNormalZ &&
    left.CollisionCell == right.CollisionCell &&
    left.TargetCellSequence.SequenceEqual(right.TargetCellSequence) &&
    left.OcclusionGroupIndex == right.OcclusionGroupIndex;

static Id65AuthoringManifest CopyBase(Id65AuthoringManifest source, string? profileId = null) =>
    new(
        profileId ?? source.ProfileId,
        source.LockedBase,
        source.Sectors,
        source.Vertices,
        source.RenderFaces,
        source.Collision,
        source.Occlusion,
        source.Textures,
        source.Mobys,
        source.Spawn,
        source.Music,
        source.Totals,
        source.Exit,
        source.Save);

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
