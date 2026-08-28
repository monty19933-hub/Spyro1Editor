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

UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(baseImagePath);
Id65AuthoringTerrainSetManifest empty = Id65AuthoringTerrainSetCatalog.Create();
Id65AuthoringTerrainSetManifest tile1 = Id65AuthoringTerrainSetCatalog.Create(
    Id65AuthoringTerrainSetCatalog.Tile1Id);
Id65AuthoringTerrainSetManifest tile2 = Id65AuthoringTerrainSetCatalog.Create(
    Id65AuthoringTerrainSetCatalog.Tile2Id);
Id65AuthoringTerrainSetManifest both = Id65AuthoringTerrainSetCatalog.Create(
    Id65AuthoringTerrainSetCatalog.Tile2Id,
    Id65AuthoringTerrainSetCatalog.Tile1Id);

Require(empty.CanonicalSha256 == Id65AuthoringTerrainSetCompiler.ExpectedEmptyManifestSha256 &&
        tile1.CanonicalSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1ManifestSha256 &&
        tile2.CanonicalSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2ManifestSha256 &&
        both.CanonicalSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothManifestSha256 &&
        both.ActiveUnits.Select(item => item.Id).SequenceEqual(
            [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id]),
    "The exact four-state terrain-set catalog or canonical order changed.");

Id65TerrainTriangleIntent[] callerOwned =
[
    Id65AuthoringTerrainSetCatalog.Tile1,
    Id65AuthoringTerrainSetCatalog.Tile2
];
Id65AuthoringTerrainSetManifest defensive = new(
    Id65AuthoringTerrainSetCatalog.ProfileId,
    empty.LockedV3Base,
    callerOwned);
callerOwned[0] = callerOwned[0] with { Id = "tile.caller-mutated" };
Require(defensive.CanonicalSha256 == both.CanonicalSha256 &&
        defensive.ActiveUnits[0].Id == Id65AuthoringTerrainSetCatalog.Tile1Id,
    "The terrain-set manifest retained a mutable caller-owned collection.");
bool readOnlyObserved = false;
try
{
    ((IList<Id65TerrainTriangleIntent>)defensive.ActiveUnits)[0] = callerOwned[0];
}
catch (NotSupportedException)
{
    readOnlyObserved = true;
}
Require(readOnlyObserved, "The terrain-set manifest exposed a mutable active-unit collection.");

Id65CompiledAuthoringTerrainSet emptyState =
    Id65AuthoringTerrainSetCompiler.CompileTarget(empty, v3Template);
Id65CompiledAuthoringTerrainSet tile1State =
    Id65AuthoringTerrainSetCompiler.CompileTarget(tile1, v3Template);
Id65CompiledAuthoringTerrainSet tile2State =
    Id65AuthoringTerrainSetCompiler.CompileTarget(tile2, v3Template);
Id65CompiledAuthoringTerrainSet bothState =
    Id65AuthoringTerrainSetCompiler.CompileTarget(both, v3Template);
Id65CompiledAuthoringTerrainSet bothRepeat =
    Id65AuthoringTerrainSetCompiler.CompileTarget(both, v3Template);

Require(emptyState.ModelSha256 == Id65AuthoringTerrainSetCompiler.ExpectedV3ModelSha256 &&
        tile1State.ModelSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1ModelSha256 &&
        tile2State.ModelSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2ModelSha256 &&
        bothState.ModelSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothModelSha256 &&
        bothRepeat.ModelSha256 == bothState.ModelSha256 &&
        bothRepeat.DeterministicPlanSha256 == bothState.DeterministicPlanSha256 &&
        bothRepeat.CopyModel().SequenceEqual(bothState.CopyModel()),
    "A frozen terrain-set target model or deterministic rebuild changed.");
Require(tile1State.SectorSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1SectorSha256 &&
        tile1State.EnvironmentSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1EnvironmentSha256 &&
        tile1State.CollisionBlocksSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1BlocksSha256 &&
        tile1State.CollisionSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile1CollisionSha256 &&
        tile2State.SectorSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2SectorSha256 &&
        tile2State.EnvironmentSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2EnvironmentSha256 &&
        tile2State.CollisionBlocksSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2BlocksSha256 &&
        tile2State.CollisionSha256 == Id65AuthoringTerrainSetCompiler.ExpectedTile2CollisionSha256 &&
        bothState.SectorSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothSectorSha256 &&
        bothState.EnvironmentSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothEnvironmentSha256 &&
        bothState.CollisionBlocksSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothBlocksSha256 &&
        bothState.CollisionSha256 == Id65AuthoringTerrainSetCompiler.ExpectedBothCollisionSha256,
    "A frozen terrain-set sector/environment/collision pin changed.");

RequireCapacity(emptyState.Capacity, 0x9457C, 0x284, 0x28518, 0x70, 3, 1, 0x5FAE8, 19_808, 0x17960);
RequireCapacity(tile1State.Capacity, 0x945AC, 0x254, 0x28538, 0x90, 4, 2, 0x5FAF8, 19_809, 0x17962);
RequireCapacity(tile2State.Capacity, 0x945AC, 0x254, 0x28538, 0x90, 4, 2, 0x5FAF8, 19_809, 0x17962);
RequireCapacity(bothState.Capacity, 0x945D8, 0x228, 0x28558, 0xB0, 5, 3, 0x5FB04, 19_810, 0x17964);
Require(emptyState.TargetCellSequence.SequenceEqual([13_995]) &&
        tile1State.TargetCellSequence.SequenceEqual([19_808, 13_995]) &&
        tile2State.TargetCellSequence.SequenceEqual([19_808, 13_995]) &&
        bothState.TargetCellSequence.SequenceEqual([19_809, 19_808, 13_995]),
    "The terrain-set collision cell sequences changed.");

Require(tile1State.Units.Single() is var firstOnly &&
        firstOnly.UnitId == Id65AuthoringTerrainSetCatalog.Tile1Id &&
        firstOnly.LowDetailVertexIndex == 3 && firstOnly.HighDetailVertexIndex == 3 &&
        firstOnly.LowDetailFaceIndex == 1 && firstOnly.HighDetailFaceIndex == 1 &&
        firstOnly.CollisionTriangleIndex == 19_808 &&
        firstOnly.AddedVertexWordHex == Id65AuthoringTerrainMutationCompiler.ExpectedAddedVertexWordHex &&
        firstOnly.LowDetailFaceHex == Id65AuthoringTerrainMutationCompiler.ExpectedLowDetailFaceHex &&
        firstOnly.HighDetailFaceHex == Id65AuthoringTerrainMutationCompiler.ExpectedHighDetailFaceHex &&
        firstOnly.CollisionTriangleHex == Id65AuthoringTerrainMutationCompiler.ExpectedCollisionTriangleHex &&
        firstOnly.RenderNormalZ == 25_088 && firstOnly.CollisionNormalZ == -25_088 &&
        firstOnly.CollisionAssignment == 0 && firstOnly.OcclusionGroupIndex == 0,
    "Tile 1 stopped matching the byte-exact schema-3 witness.");
Require(tile2State.Units.Single() is var secondOnly &&
        secondOnly.UnitId == Id65AuthoringTerrainSetCatalog.Tile2Id &&
        secondOnly.AddedVertexHandle == Id65AuthoringTerrainSetCatalog.Tile2VertexHandle &&
        secondOnly.LowDetailVertexIndex == 3 && secondOnly.LowDetailFaceIndex == 1 &&
        secondOnly.CollisionTriangleIndex == 19_808 &&
        secondOnly.AddedVertexWordHex == "00C00302" &&
        secondOnly.LowDetailFaceHex == "00C3200000821000" &&
        secondOnly.HighDetailFaceHex == "000002030101000219024001080A1000" &&
        secondOnly.CollisionTriangleHex == "100100381001387000020000" &&
        secondOnly.RenderNormalZ == 25_088 && secondOnly.CollisionNormalZ == -25_088,
    "Tile 2 exact HP/LP/collision readback changed.");
Require(bothState.Units.Count == 2 &&
        bothState.Units[0].UnitId == Id65AuthoringTerrainSetCatalog.Tile1Id &&
        bothState.Units[0].LowDetailVertexIndex == 3 &&
        bothState.Units[0].LowDetailFaceIndex == 1 &&
        bothState.Units[0].CollisionTriangleIndex == 19_808 &&
        bothState.Units[1].UnitId == Id65AuthoringTerrainSetCatalog.Tile2Id &&
        bothState.Units[1].LowDetailVertexIndex == 4 &&
        bothState.Units[1].LowDetailFaceIndex == 2 &&
        bothState.Units[1].CollisionTriangleIndex == 19_809 &&
        bothState.Units[1].LowDetailFaceHex == "0004210000821000" &&
        bothState.Units[1].HighDetailFaceHex == "000002040101000219024001080A1000",
    "The two-unit canonical HP/LP/collision ordinals changed.");

Require(new[] { emptyState, tile1State, tile2State, bothState }.All(state =>
            state.ProtectedComplementSha256 ==
                Id65AuthoringTerrainSetCompiler.ExpectedProtectedComplementSha256 &&
            state.ProtectedComplementPreserved && state.SingleOcclusionGroupOwnershipVerified &&
            state.Relocations.Count == 9 && state.HandleRebases.Count == 18 + (state.Units.Count * 6)),
    "The protected complement, occlusion owner, relocation map, or rebase cardinality changed.");
Require(bothState.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1").Output ==
            new Id65LogicalAddress("terrain-tile", 216, 1) &&
        bothState.HandleRebases.Single(item => item.StableId == "vertex.remote-pad.d:lp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 3) &&
        bothState.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1:collision").Output ==
            new Id65LogicalAddress("collision-triangle", 19_808, 0) &&
        bothState.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2").Output ==
            new Id65LogicalAddress("terrain-tile", 216, 2) &&
        bothState.HandleRebases.Single(item => item.StableId == "vertex.remote-pad.e:lp").Output ==
            new Id65LogicalAddress("scene-sector", 216, 4) &&
        bothState.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2:collision").Output ==
            new Id65LogicalAddress("collision-triangle", 19_809, 0),
    "The stable two-unit logical-address map changed.");

// Compatibility proof: the new {tile1} state is byte-identical to the retired
// one-optional-triangle schema-3 compiler, without changing that compiler.
Id65TerrainAuthoringManifest oldEmpty = Id65AuthoringTerrainMutationCompiler.CreateV3SubstrateManifest();
Id65TerrainAuthoringManifest oldTile1 = Id65AuthoringTerrainMutationCompiler.AddTriangle(
    oldEmpty,
    Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness());
Id65CompiledTerrainMutation oldAdd = Id65AuthoringTerrainMutationCompiler.CompileTransition(
    oldEmpty,
    oldTile1,
    v3Template);
Require(oldAdd.SourceModelSha256 == emptyState.ModelSha256 &&
        oldAdd.OutputModelSha256 == tile1State.ModelSha256 &&
        oldAdd.CopySourceModel().SequenceEqual(emptyState.CopyModel()) &&
        oldAdd.CopyOutputModel().SequenceEqual(tile1State.CopyModel()) &&
        oldEmpty.CanonicalSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedSubstrateManifestSha256 &&
        oldTile1.CanonicalSha256 == Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileManifestSha256,
    "The existing schema-3 compiler or hashes changed instead of remaining byte-compatible.");

Id65CompiledAuthoringTerrainSetTransition emptyTo1 = Transition(empty, tile1);
Id65CompiledAuthoringTerrainSetTransition oneToBoth = Transition(tile1, both);
Id65CompiledAuthoringTerrainSetTransition bothTo2 = Transition(both, tile2);
Id65CompiledAuthoringTerrainSetTransition twoToEmpty = Transition(tile2, empty);
Id65CompiledAuthoringTerrainSetTransition emptyTo2 = Transition(empty, tile2);
Id65CompiledAuthoringTerrainSetTransition twoToBoth = Transition(tile2, both);
Id65CompiledAuthoringTerrainSetTransition bothTo1 = Transition(both, tile1);
Id65CompiledAuthoringTerrainSetTransition oneToEmpty = Transition(tile1, empty);

RunOrder("tile1-first", emptyState.CopyModel(),
    [emptyTo1, oneToBoth, bothTo2, twoToEmpty], emptyState.CopyModel());
RunOrder("tile2-first", emptyState.CopyModel(),
    [emptyTo2, twoToBoth, bothTo1, oneToEmpty], emptyState.CopyModel());

foreach (Id65CompiledAuthoringTerrainSetTransition transition in new[]
         {
             emptyTo1, oneToBoth, bothTo2, twoToEmpty,
             emptyTo2, twoToBoth, bothTo1, oneToEmpty
         })
{
    Id65CompiledAuthoringTerrainSetTransition inverse =
        Id65AuthoringTerrainSetCompiler.Invert(transition);
    Id65CompiledAuthoringTerrainSetTransition doubleInverse =
        Id65AuthoringTerrainSetCompiler.Invert(inverse);
    Require(inverse.Source.ModelSha256 == transition.Output.ModelSha256 &&
            inverse.Output.ModelSha256 == transition.Source.ModelSha256 &&
            inverse.ChangedUnitId == transition.ChangedUnitId && inverse.Added != transition.Added &&
            inverse.Relocations.SequenceEqual(
                Id65AuthoringModelCompiler.InvertRelocations(transition.Relocations)) &&
            inverse.HandleRebases.SequenceEqual(
                Id65AuthoringModelCompiler.InvertHandleRebases(transition.HandleRebases)) &&
            doubleInverse.Source.ModelSha256 == transition.Source.ModelSha256 &&
            doubleInverse.Output.ModelSha256 == transition.Output.ModelSha256 &&
            doubleInverse.Relocations.SequenceEqual(transition.Relocations) &&
            doubleInverse.HandleRebases.SequenceEqual(transition.HandleRebases),
        $"Transition {transition.ChangedUnitId}/{transition.Added} lost its exact inverse.");
}

Require(bothTo2.HandleRebases.Single(item => item.StableId == "tile.remote-pad.1").Output is null &&
        bothTo2.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2").Source ==
            new Id65LogicalAddress("terrain-tile", 216, 2) &&
        bothTo2.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2").Output ==
            new Id65LogicalAddress("terrain-tile", 216, 1) &&
        bothTo2.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2:collision").Source ==
            new Id65LogicalAddress("collision-triangle", 19_809, 0) &&
        bothTo2.HandleRebases.Single(item => item.StableId == "tile.remote-pad.2:collision").Output ==
            new Id65LogicalAddress("collision-triangle", 19_808, 0),
    "Removing tile 1 did not deterministically rebase the independent tile 2 handles.");

byte[] tamperedPreimage = emptyState.CopyModel();
tamperedPreimage[0x2F78] ^= 1;
ExpectReject("tampered transition preimage",
    () => Id65AuthoringTerrainSetCompiler.ApplyTransactional(emptyTo1, tamperedPreimage),
    "preimage changed");
byte[] tamperedAfterimage = tile1State.CopyModel();
tamperedAfterimage[0x2F78] ^= 1;
ExpectReject("tampered transition afterimage",
    () => Id65AuthoringTerrainSetCompiler.ApplyTransactional(emptyTo1, tamperedAfterimage, reverse: true),
    "afterimage changed");

int rejectionCount = 2;
void Reject(string label, Action action, string text)
{
    ExpectReject(label, action, text);
    rejectionCount++;
}

IReadOnlyList<Id65AuthoringTerrainUnitReadback> independentlyDecoded =
    Id65AuthoringTerrainSetCompiler.DecodeOutputSemantics(both, bothState.CopyModel());
Require(independentlyDecoded.SequenceEqual(bothState.Units),
    "Independent emitted-byte decoding does not match compiled unit readback.");
const int bothSectorOffset = 0x2B420;
byte[] wrongLowFaceBytes = bothState.CopyModel();
wrongLowFaceBytes[bothSectorOffset + 0x4C + 3] ^= 0x04;
Reject("wrong encoded LP face slot",
    () => Id65AuthoringTerrainSetCompiler.DecodeOutputSemantics(both, wrongLowFaceBytes),
    "catalog render handles");
byte[] wrongHighFaceBytes = bothState.CopyModel();
wrongHighFaceBytes[bothSectorOffset + 0xA0] ^= 0x01;
Reject("wrong encoded HP face slot",
    () => Id65AuthoringTerrainSetCompiler.DecodeOutputSemantics(both, wrongHighFaceBytes),
    "repeated leading corner slot");
byte[] wrongCollisionCoordinates = bothState.CopyModel();
wrongCollisionCoordinates[0x84304] ^= 0x01;
Reject("wrong encoded collision coordinates",
    () => Id65AuthoringTerrainSetCompiler.DecodeOutputSemantics(both, wrongCollisionCoordinates),
    "catalog handle");

Reject("duplicate catalog request",
    () => Id65AuthoringTerrainSetCatalog.Create(
        Id65AuthoringTerrainSetCatalog.Tile1Id,
        Id65AuthoringTerrainSetCatalog.Tile1Id),
    "duplicate");
Reject("unknown catalog request",
    () => Id65AuthoringTerrainSetCatalog.Create("tile.remote-pad.unknown"),
    "unknown");
Reject("duplicate direct manifest",
    () => Id65AuthoringTerrainSetCompiler.CompileTarget(
        new(Id65AuthoringTerrainSetCatalog.ProfileId, empty.LockedV3Base,
            [Id65AuthoringTerrainSetCatalog.Tile1, Id65AuthoringTerrainSetCatalog.Tile1]),
        v3Template),
    "duplicate");
Reject("unknown direct manifest",
    () => Id65AuthoringTerrainSetCompiler.CompileTarget(
        new(Id65AuthoringTerrainSetCatalog.ProfileId, empty.LockedV3Base,
            [Id65AuthoringTerrainSetCatalog.Tile1 with { Id = "tile.remote-pad.unknown" }]),
        v3Template),
    "Unknown terrain-set unit ID");
Reject("point tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with
    {
        AddedVertex = Id65AuthoringTerrainSetCatalog.Tile2.AddedVertex with
        {
            Point = new(273, 496, 512)
        }
    }), "winding");
Reject("vertex handle tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with
    {
        AddedVertex = Id65AuthoringTerrainSetCatalog.Tile2.AddedVertex with
        {
            Handle = "vertex.remote-pad.changed"
        }
    }), "winding");
Reject("texture tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with { TextureIntentId = "texture.locked.record24" }),
    "catalog witness");
Reject("render topology tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with { RenderC = "vertex.remote-pad.c" }),
    "winding");
Reject("positive collision winding", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with
    {
        CollisionB = Id65AuthoringTerrainSetCatalog.Tile2.CollisionC,
        CollisionC = Id65AuthoringTerrainSetCatalog.Tile2.CollisionB
    }), "negative collision winding");
Reject("collision assignment tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with { CollisionAssignment = 1 }),
    "catalog witness");
Reject("occlusion group tamper", () => CompileTampered(
    Id65AuthoringTerrainSetCatalog.Tile2 with { OcclusionGroupIndex = 1 }),
    "catalog witness");
Reject("identity transition",
    () => Id65AuthoringTerrainSetCompiler.CompileTransition(empty, empty, v3Template),
    "add or remove exactly one");
Reject("two-unit transition",
    () => Id65AuthoringTerrainSetCompiler.CompileTransition(empty, both, v3Template),
    "add or remove exactly one");
Reject("profile tamper",
    () => Id65AuthoringTerrainSetCompiler.CompileTarget(
        new("wrong-profile", empty.LockedV3Base, []),
        v3Template),
    "minimal-v3 terrain-set base");
Reject("vertex capacity",
    () => Id65AuthoringTerrainSetCompiler.CompileTarget(
        both,
        v3Template,
        new(MaximumLowDetailVertexCount: 4)),
    "vertex capacity");

UnusedLevel65RemoteBlankStructuralPatch[] positivePatches = v3Template.StructuralPatches
    .Select(patch => patch.Kind == "id65-model"
        ? patch with
        {
            After = v3Template.SupersededV2Plan.FailedV1Plan.StructuralPatches
                .Single(item => item.Kind == "id65-model").After.ToArray()
        }
        : patch)
    .ToArray();
Reject("positive-winding v3 template",
    () => Id65AuthoringTerrainSetCompiler.CompileTarget(
        both,
        v3Template with { StructuralPatches = positivePatches }),
    "v3 header-repaired model");

Require(new[] { emptyState, tile1State, tile2State, bothState }.All(state =>
            state.ImmutableLockedV3SourceVerified && state.SinglePassTargetRebuildVerified &&
            state.ExactHpLpRowsVerified && state.NegativeCollisionWindingVerified &&
            state.CollisionCellTreeReadbackVerified && state.StableIdsAndRebaseMapVerified &&
            !state.WritesFileSystem && !state.WritesDiscImage && !state.WritesCue &&
            !state.AppIntegrated && !state.DocumentIntegrated && !state.PersistenceIntegrated &&
            !state.MaterializerIntegrated && !state.CreateBinEnabled &&
            !state.NormalCreateBinEnabled && !state.ReleaseIntegrated &&
            !state.RuntimeCandidateAuthorized && !state.PhysicalAcceptanceComplete &&
            !state.PromotionAuthorized && !state.Publishable) &&
        new[] { emptyTo1, oneToBoth, bothTo2, twoToEmpty }.All(transition =>
            transition.ExactPreimageRequired && transition.ExactAfterimageRequired &&
            transition.ExactInverseVerified && !transition.WritesFileSystem &&
            !transition.WritesDiscImage && !transition.AppIntegrated &&
            !transition.CreateBinEnabled && !transition.ReleaseIntegrated &&
            !transition.RuntimeCandidateAuthorized && !transition.PhysicalAcceptanceComplete &&
            !transition.Publishable),
    "A terrain-set runtime/CreateBIN/release/materializer gate opened before physical acceptance.");

Require(HashFile(baseImagePath) == sourceHashBefore &&
        new FileInfo(baseImagePath).Length == sourceLengthBefore &&
        File.GetLastWriteTimeUtc(baseImagePath) == sourceWriteBefore,
    "The pure terrain-set smoke changed the locked source BIN.");

Console.WriteLine(
    $"ID65_AUTHORING_TERRAIN_SET_COMPILER_SMOKE_OK states=4 transitions=8 " +
    $"rejections={rejectionCount} empty={emptyState.ModelSha256} " +
    $"tile1={tile1State.ModelSha256} tile2={tile2State.ModelSha256} both={bothState.ModelSha256} " +
    $"trees={emptyState.CollisionTreeSha256},{tile1State.CollisionTreeSha256}," +
    $"{tile2State.CollisionTreeSha256},{bothState.CollisionTreeSha256} " +
    $"targetMaps={emptyState.RelocationMapSha256}/{emptyState.HandleRebaseMapSha256}/{emptyState.DeterministicPlanSha256}," +
    $"{tile1State.RelocationMapSha256}/{tile1State.HandleRebaseMapSha256}/{tile1State.DeterministicPlanSha256}," +
    $"{tile2State.RelocationMapSha256}/{tile2State.HandleRebaseMapSha256}/{tile2State.DeterministicPlanSha256}," +
    $"{bothState.RelocationMapSha256}/{bothState.HandleRebaseMapSha256}/{bothState.DeterministicPlanSha256} " +
    $"protected={bothState.ProtectedComplementSha256} runtime=false createBin=false release=false");

Id65CompiledAuthoringTerrainSetTransition Transition(
    Id65AuthoringTerrainSetManifest source,
    Id65AuthoringTerrainSetManifest target) =>
    Id65AuthoringTerrainSetCompiler.CompileTransition(source, target, v3Template);

void CompileTampered(Id65TerrainTriangleIntent tampered) =>
    _ = Id65AuthoringTerrainSetCompiler.CompileTarget(
        new(Id65AuthoringTerrainSetCatalog.ProfileId, empty.LockedV3Base, [tampered]),
        v3Template);

static void RunOrder(
    string label,
    byte[] initial,
    IReadOnlyList<Id65CompiledAuthoringTerrainSetTransition> transitions,
    byte[] expected)
{
    byte[] current = initial;
    foreach (Id65CompiledAuthoringTerrainSetTransition transition in transitions)
        current = Id65AuthoringTerrainSetCompiler.ApplyTransactional(transition, current);
    Require(current.SequenceEqual(expected), $"Terrain-set {label} order did not restore exact v3 bytes.");
}

static void RequireCapacity(
    Id65AuthoringTerrainSetCapacityReadback actual,
    int used,
    int tail,
    int environment,
    int sector,
    int vertices,
    int faces,
    int collision,
    int triangles,
    int blocksUsed)
{
    Require(actual.ModelByteCapacity == 0x94800 &&
            actual.UsedModelBytes == used && actual.ZeroTailBytes == tail &&
            actual.EnvironmentBytes == environment && actual.SectorBytes == sector &&
            actual.LowDetailVertexCount == vertices && actual.HighDetailVertexCount == vertices &&
            actual.LowDetailFaceCount == faces && actual.HighDetailFaceCount == faces &&
            actual.CollisionBytes == collision && actual.CollisionTriangleCount == triangles &&
            actual.CollisionBlocksUsedBytes == blocksUsed &&
            actual.CollisionTreeCapacityBytes == 0x6A60 &&
            actual.CollisionBlockCapacityBytes == 0x17984,
        "A terrain-set exact capacity readback changed.");
}

static void ExpectReject(string label, Action action, string expectedText)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }
    throw new InvalidDataException($"{label} was not rejected with '{expectedText}'.");
}

static string FindRepositoryRoot(string? supplied)
{
    string current = Path.GetFullPath(supplied ?? Directory.GetCurrentDirectory());
    while (true)
    {
        if (Directory.Exists(Path.Combine(current, ".git")))
            return current;
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent is null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
        current = parent.FullName;
    }
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}
