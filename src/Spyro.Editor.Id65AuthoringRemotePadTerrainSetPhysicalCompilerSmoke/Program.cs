using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
Require(File.Exists(lockedImagePath),
    "Missing exact schema-5 physical compiler fixture: " + lockedImagePath);

FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);

Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
Id65AuthoringCompositeLockedSource lockedSource = boundSource.LockedSource;
UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
        .BuildStaticPlan(lockedImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler
        .DerivePinnedT66WitnessFromLockedBase(lockedImagePath);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler
        .CreateArtisansGrassBundleFromLockedBase(lockedImagePath);

Require(
    lockedSource.Row80Sha256 ==
        Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
    lockedSource.TargetOverlaySha256 ==
        Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
    lockedSource.GlobalExecutableSha256 ==
        Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
    v3Template.SourceData.SequenceEqual(lockedSource.CopyRow80()) &&
    v3Template.OutputDataSha256 ==
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
            .ExpectedOutputDataSha256 &&
    textureWitness.CanonicalSha256 ==
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
    grassBundle.CanonicalSha256 ==
        Id65RemotePadGrassT107Catalog.BundleCanonicalSha256,
    "The locked row, v3 provider, T66 witness, or Grass bundle pin changed.");

string[][] terrainStates =
[
    [],
    [Id65AuthoringTerrainSetCatalog.Tile1Id],
    [Id65AuthoringTerrainSetCatalog.Tile2Id],
    [Id65AuthoringTerrainSetCatalog.Tile1Id,
     Id65AuthoringTerrainSetCatalog.Tile2Id]
];
int states = 0;
int forwardRejections = 0;
int rollbackRejections = 0;
HashSet<string> stateKeys = new(StringComparer.Ordinal);
HashSet<string> documentHashes = new(StringComparer.Ordinal);
HashSet<string> planHashes = new(StringComparer.Ordinal);
HashSet<string> outputHashes = new(StringComparer.Ordinal);
HashSet<string> modelHashes = new(StringComparer.Ordinal);
object stateGate = new();

var stateInputs = terrainStates
    .SelectMany(terrainIds => Enum.GetValues<Id65RemotePadMobyKind>()
        .SelectMany(kind => Id65RemotePadGrassT107Catalog.Positions
            .Select(position => (TerrainIds: terrainIds, Kind: kind, Position: position))))
    .ToArray();
Parallel.ForEach(
    stateInputs,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    state =>
    {
            string[] terrainIds = state.TerrainIds;
            Id65RemotePadMobyKind kind = state.Kind;
            Id65RemotePadGrassT107Position position = state.Position;
            string stateKey =
                (terrainIds.Length == 0 ? "none" : string.Join('+', terrainIds)) +
                "|" + Id65RemotePadMobyChoiceCatalog.GetKindId(kind) +
                "|" + position.PositionId;
            lock (stateGate)
            {
                Require(stateKeys.Add(stateKey),
                    "A schema-5 physical state key was compiled twice.");
            }
            Id65AuthoringEditorDocument document =
                Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
                    kind,
                    position.PositionId,
                    terrainIds);
            Id65CompiledAuthoringRemotePadTerrainSetPhysical first =
                Compile(document, lockedSource, v3Template);
            Id65CompiledAuthoringRemotePadTerrainSetPhysical repeat =
                Compile(
                    Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
                    lockedSource,
                    v3Template);

            Require(
                first.ProfileId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ProfileId &&
                first.SchemaVersion ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.SchemaVersion &&
                first.InputDocumentSchemaVersion ==
                    Id65AuthoringEditorDocument
                        .RemotePadMobyChoiceTerrainSetSchemaVersion &&
                first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
                first.MobyKind == kind &&
                first.PositionId == position.PositionId &&
                first.ActiveTerrainUnitIds.SequenceEqual(terrainIds) &&
                first.Terrain.ActiveTerrainUnitIds.SequenceEqual(terrainIds) &&
                first.Terrain.TerrainManifestSha256 ==
                    document.TerrainSetManifestSha256 &&
                first.Terrain.Units.Count == terrainIds.Length &&
                first.Terrain.MandatoryBaseFaceTextureId == 66 &&
                first.Terrain.T66TextureComponentSha256 ==
                    Id65V2NativeTextureCompositionCompiler
                        .ExpectedOutputTextureComponentSha256 &&
                first.Terrain.V3TerrainEnvironmentSha256.Length == 64 &&
                first.Terrain.FinalT66EnvironmentSha256.Length == 64 &&
                first.Terrain.V3TerrainEnvironmentSha256 !=
                    first.Terrain.FinalT66EnvironmentSha256 &&
                first.Terrain.V3TerrainProtectedComplementSha256.Length == 64 &&
                first.Terrain.FinalT66ProtectedComponentSetSha256.Length == 64 &&
                first.Terrain.V3TerrainProtectedComplementSha256 !=
                    first.Terrain.FinalT66ProtectedComponentSetSha256 &&
                first.SourceRow80Sha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
                first.TargetOverlaySha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
                first.GlobalExecutableSha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
                first.Schema4BaselineOutputModelSha256 ==
                    Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256 &&
                first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
                first.OutputModelSha256 == repeat.OutputModelSha256 &&
                first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
                first.TransactionSha256 == repeat.TransactionSha256 &&
                first.ProtectedComplementSha256 ==
                    repeat.ProtectedComplementSha256 &&
                first.DeterministicPlanSha256 ==
                    repeat.DeterministicPlanSha256 &&
                first.CopyOutputRow80().SequenceEqual(repeat.CopyOutputRow80()),
                "Schema-5 state did not compile deterministically: " + stateKey);
            Require(
                first.PureInMemory &&
                first.ExactSchema5DocumentRequired &&
                first.BuildsEveryTargetFromLockedV3Source &&
                !first.CallerAfterimagesAccepted &&
                first.IndependentTerrainSemanticReadbackVerified &&
                first.MobyPlacementReadbackVerified &&
                first.NativeT66TexturePreserved &&
                first.ExactSpawnPreserved &&
                first.OverlayAndExecutablePreserved &&
                first.ExactInverseVerified &&
                first.EveryChangedByteOwned &&
                first.ProtectedComplementVerified &&
                first.ContainsOutputRow80Bytes &&
                !first.AcceptsAggregatePlans &&
                !first.AcceptsMaterializedCandidates &&
                !first.WritesFileSystem &&
                !first.WritesDiscImage &&
                !first.WritesCue &&
                !first.AppIntegrated &&
                !first.UiIntegrated &&
                !first.AggregateMaterializerEnabled &&
                !first.CandidateMaterializerEnabled &&
                !first.PhysicalMaterializationEnabled &&
                !first.CreateBinEnabled &&
                !first.NormalCreateBinEnabled &&
                !first.RuntimeCandidateAuthorized &&
                !first.RuntimeProven &&
                !first.ReleaseIntegrated &&
                !first.PromotionAuthorized &&
                !first.Publishable,
                "Schema-5 compiler opened a materializer/Create BIN/runtime/release gate: " +
                stateKey);
            Require(
                first.OutputTexturePagesSha256 ==
                    Id65V2NativeTextureCompositionCompiler
                        .ExpectedOutputTexturePagesSha256 &&
                first.LandingSha256 == v3Template.Spawn.OutputLandingSha256 &&
                first.PlayerAnchorSha256 ==
                    v3Template.Spawn.OutputPlayerAnchorSha256,
                "Schema-5 exact native textures or spawn preservation changed: " +
                stateKey);

            if (terrainIds.Length == 0 ||
                terrainIds.SequenceEqual([Id65AuthoringTerrainSetCatalog.Tile1Id]))
            {
                Id65CompiledAuthoringRemotePadMobyChoicePhysical schema4Parity =
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(new(
                        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                            kind,
                            position.PositionId,
                            includeOptionalTerrain: terrainIds.Length == 1),
                        lockedSource,
                        textureWitness,
                        grassBundle));
                Require(first.CopyOutputRow80().SequenceEqual(
                            schema4Parity.CopyOutputRow80()) &&
                        first.OutputRow80Sha256 == schema4Parity.OutputRow80Sha256,
                    "Schema-5 empty/tile1 row lost exact schema-4 full-row parity: " +
                    stateKey);
            }

            VerifyPlacement(first, kind, position);
            VerifyOwnedRanges(first);
            VerifyDefensiveCopies(first);
            byte[] source = first.CopySourceRow80();
            byte[] output = first.CopyOutputRow80();
            byte[] applied =
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ApplyTransactional(
                    first,
                    source,
                    reverse: false);
            byte[] rolledBack =
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ApplyTransactional(
                    first,
                    applied,
                    reverse: true);
            Require(applied.SequenceEqual(output) && rolledBack.SequenceEqual(source),
                "Schema-5 exact forward/inverse failed: " + stateKey);

            byte[] forgedPreimage = source.ToArray();
            forgedPreimage[0x400] ^= 1;
            Reject(stateKey + " forged preimage", () =>
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ApplyTransactional(
                    first,
                    forgedPreimage,
                    reverse: false));
            Interlocked.Increment(ref forwardRejections);
            byte[] forgedAfterimage = output.ToArray();
            forgedAfterimage[0x400] ^= 1;
            Reject(stateKey + " forged afterimage", () =>
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ApplyTransactional(
                    first,
                    forgedAfterimage,
                    reverse: true));
            Interlocked.Increment(ref rollbackRejections);

            lock (stateGate)
            {
                Require(documentHashes.Add(first.DocumentCanonicalSha256),
                    "Schema-5 canonical document hashes are not unique: " + stateKey);
                Require(planHashes.Add(first.DeterministicPlanSha256),
                    "Schema-5 deterministic plan hashes are not unique: " + stateKey);
                outputHashes.Add(first.OutputRow80Sha256);
                modelHashes.Add(first.OutputModelSha256);
            }
            Interlocked.Increment(ref states);
        }
    );

Require(states == Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ExpectedStateCount &&
    stateKeys.Count == 108 &&
    documentHashes.Count == 108 &&
    planHashes.Count == 108 &&
    outputHashes.Count == 76 &&
    modelHashes.Count == 4 &&
    forwardRejections == 108 &&
    rollbackRejections == 108,
    "The closed schema-5 4 x 3 x 9 state matrix changed.");

Reject("schema-1 document", () => Compile(
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: false,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 66),
    lockedSource,
    v3Template));
Reject("schema-2 document", () => Compile(
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: false,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 25),
    lockedSource,
    v3Template));
Reject("schema-3 document", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadGridExact(
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        includeOptionalTerrain: false,
        placementIds: [],
        mandatoryBaseFaceTextureId: 66),
    lockedSource,
    v3Template));
Reject("schema-4 document", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        includeOptionalTerrain: false),
    lockedSource,
    v3Template));

byte[] forgedLockedRow = lockedSource.CopyRow80();
forgedLockedRow[0x400] ^= 1;
Id65AuthoringCompositeLockedSource forgedLockedSource = new(
    forgedLockedRow,
    lockedSource.CopyTargetOverlay(),
    lockedSource.CopyGlobalExecutable());
Reject("forged locked source", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []),
    forgedLockedSource,
    v3Template));

byte[] forgedV3Source = v3Template.SourceData.ToArray();
forgedV3Source[0x400] ^= 1;
Reject("forged v3 preimage", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []),
    lockedSource,
    v3Template with { SourceData = forgedV3Source }));
byte[] forgedV3Output = v3Template.OutputData.ToArray();
forgedV3Output[0x400] ^= 1;
Reject("forged v3 afterimage", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []),
    lockedSource,
    v3Template with { OutputData = forgedV3Output }));

Id65AuthoringEditorDocument rejectionDocument =
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.GrassT107,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []);
Id65V2NativeTextureWitness forgedTextureWitness = new(
    textureWitness.ProfileId,
    textureWitness.DonorManifestId,
    textureWitness.DonorCompleteRecordSha256,
    textureWitness.SourceTexturePagesSha256,
    textureWitness.OutputTexturePagesSha256,
    textureWitness.SourceTextureComponentSha256,
    textureWitness.OutputTextureComponentSha256,
    textureWitness.SourceTextureCount,
    textureWitness.OutputTextureCount,
    textureWitness.PrivateTextureId,
    textureWitness.DonorWadEntry,
    textureWitness.DonorTextureId,
    textureWitness.MaterialTemplateTextureId,
    textureWitness.CopyPackedRows(),
    textureWitness.CopyPagePatches(),
    new string('0', 64));
Reject("forged T66 witness", () =>
    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
        rejectionDocument,
        lockedSource,
        v3Template,
        forgedTextureWitness,
        grassBundle)));
Id65MobyDependencyBundleDescriptor forgedGrassBundle = new(
    grassBundle.Id,
    grassBundle.SchemaVersion,
    grassBundle.FoundationContractSha256,
    grassBundle.Rows,
    grassBundle.Properties,
    grassBundle.ActorPackages,
    grassBundle.ActorRoots,
    grassBundle.Fixups,
    grassBundle.ExternalDependencies,
    grassBundle.BehaviorClosure,
    grassBundle.RuntimeDependencies,
    grassBundle.CanonicalJson,
    new string('0', 64));
Reject("forged Grass bundle", () =>
    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
        rejectionDocument,
        lockedSource,
        v3Template,
        textureWitness,
        forgedGrassBundle)));
Reject("both-terrain final composite used-model limit", () =>
    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
            [Id65AuthoringTerrainSetCatalog.Tile1Id,
             Id65AuthoringTerrainSetCatalog.Tile2Id]),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle,
        CompositeLimits: new(
            MaximumOutputUsedModelBytes:
                Id65V2NativeTextureCompositionCompiler.OutputUsedModelBytes))));

Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The pure schema-5 compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure schema-5 compiler changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_REMOTE_PAD_TERRAIN_SET_PHYSICAL_COMPILER_SMOKE_OK " +
    "states=108 outputHashes=76 modelHashes=4 deterministicHashes=108 " +
    "forwardRejections=108 rollbackRejections=108 schemaRejections=4 " +
    "schema4ParityStates=54 forgedProviderRejections=5 " +
    "writes=0 runtime=0 release=0");
return;

Id65CompiledAuthoringRemotePadTerrainSetPhysical Compile(
    Id65AuthoringEditorDocument document,
    Id65AuthoringCompositeLockedSource source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3) =>
    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
        document,
        source,
        v3,
        textureWitness,
        grassBundle));

static void VerifyPlacement(
    Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled,
    Id65RemotePadMobyKind kind,
    Id65RemotePadGrassT107Position position)
{
    Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback placement =
        compiled.Placement;
    Require(placement.Kind == kind &&
        placement.PositionId == position.PositionId &&
        placement.PositionOrdinal == position.Ordinal &&
        placement.EditorX == position.EditorX &&
        placement.EditorY == position.EditorY &&
        placement.EditorZ == position.EditorZ &&
        placement.RawX == position.RawX &&
        placement.RawY == position.RawY &&
        placement.RawZ == position.RawZ &&
        placement.Present == (kind != Id65RemotePadMobyKind.None) &&
        placement.ReusesResidentRow == (kind == Id65RemotePadMobyKind.RedGemT21) &&
        placement.ExactRowSha256 ==
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                kind,
                position.PositionId),
        "Schema-5 Moby placement readback changed.");
}

static void VerifyOwnedRanges(
    Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled)
{
    byte[] source = compiled.CopySourceRow80();
    byte[] output = compiled.CopyOutputRow80();
    Id65AuthoringCompositeOwnedRange[] residentT21Xyz = compiled.OwnedRanges
        .Where(item =>
            item.StableId ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .ResidentT21XyzOwnedRangeStableId ||
            item.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId)
        .ToArray();
    byte[] exactSourceT21 = Convert.FromHexString(
        Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
    byte[] exactOutputT21 =
        Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(compiled.PositionId);
    Require(
        residentT21Xyz.Length ==
            (compiled.MobyKind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0) &&
        residentT21Xyz.All(item =>
            item.StableId ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .ResidentT21XyzOwnedRangeStableId &&
            item.DataRelativeOffset ==
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ResidentT21CoordinateOffset &&
            item.ByteLength ==
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ResidentT21CoordinateByteLength &&
            item.CopyBefore().AsSpan().SequenceEqual(exactSourceT21.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength)) &&
            item.CopyAfter().AsSpan().SequenceEqual(exactOutputT21.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength))),
        "Schema-5 physical ownership lost its single exact resident-T21 XYZ range.");
    int changed = 0;
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange range = compiled.OwnedRanges[index];
        Require(range.ByteLength > 0 &&
            range.DataRelativeOffset >= 0 &&
            range.DataRelativeOffset + range.ByteLength <= source.Length &&
            source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyBefore()) &&
            output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyAfter()) &&
            !range.CopyBefore().SequenceEqual(range.CopyAfter()) &&
            (index == 0 ||
             compiled.OwnedRanges[index - 1].DataRelativeOffset +
                compiled.OwnedRanges[index - 1].ByteLength <=
                    range.DataRelativeOffset),
            "A schema-5 owned range lost exact source/output/nonoverlap identity.");
        int rangeChanged = CountChangedBytes(
            range.CopyBefore(),
            range.CopyAfter());
        Require(rangeChanged > 0 &&
                (range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
                 rangeChanged == range.ByteLength),
            "A schema-5 owned range gained padding outside resident-T21 XYZ.");
        changed += rangeChanged;
    }
    Require(changed == compiled.ChangedByteCount &&
        compiled.InverseRanges.Count == compiled.OwnedRanges.Count,
        "Schema-5 changed-byte ownership or inverse cardinality changed.");
}

static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
{
    Require(before.Length == after.Length,
        "Changed-byte comparison received different lengths.");
    int changed = 0;
    for (int index = 0; index < before.Length; index++)
    {
        if (before[index] != after[index]) changed++;
    }
    return changed;
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled)
{
    string sourceHash = Hash(compiled.CopySourceRow80());
    string outputHash = Hash(compiled.CopyOutputRow80());
    byte[] sourceCopy = compiled.CopySourceRow80();
    byte[] outputCopy = compiled.CopyOutputRow80();
    sourceCopy[0] ^= 1;
    outputCopy[0] ^= 1;
    byte[] beforeCopy = compiled.OwnedRanges[0].CopyBefore();
    byte[] afterCopy = compiled.OwnedRanges[0].CopyAfter();
    beforeCopy[0] ^= 1;
    afterCopy[0] ^= 1;
    Require(Hash(compiled.CopySourceRow80()) == sourceHash &&
        Hash(compiled.CopyOutputRow80()) == outputHash &&
        Hash(compiled.OwnedRanges[0].CopyBefore()) ==
            compiled.OwnedRanges[0].BeforeSha256 &&
        Hash(compiled.OwnedRanges[0].CopyAfter()) ==
            compiled.OwnedRanges[0].AfterSha256 &&
        compiled.OwnedRanges is ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.InverseRanges is ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.ActiveTerrainUnitIds is ReadOnlyCollection<string> &&
        compiled.Terrain.ActiveTerrainUnitIds is ReadOnlyCollection<string> &&
        compiled.Terrain.Units is ReadOnlyCollection<Id65AuthoringTerrainUnitReadback>,
        "Schema-5 result leaked mutable output or collection state.");
}

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
    throw new DirectoryNotFoundException("Could not locate the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(info.Length, info.LastWriteTimeUtc, Hash(File.ReadAllBytes(path)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Order(StringComparer.Ordinal)
        .ToArray()!;

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

internal sealed record FileState(long Length, DateTime LastWriteUtc, string Sha256);
