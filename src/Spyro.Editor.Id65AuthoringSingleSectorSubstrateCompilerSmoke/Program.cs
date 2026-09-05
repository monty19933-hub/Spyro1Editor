using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string ExpectedTile1OutputEnvironmentSha256 =
    "765ca3b55cb72e53305cee6d425802d998ad760661657a4bcc0a2769615ede07";

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
    "Missing exact single-sector substrate fixture: " + lockedImagePath);

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
    v3Template.SourceData.SequenceEqual(lockedSource.CopyRow80()) &&
    textureWitness.CanonicalSha256 ==
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
    grassBundle.CanonicalSha256 ==
        Id65RemotePadGrassT107Catalog.BundleCanonicalSha256,
    "The locked row, v3 provider, T66 witness, or Grass bundle pin changed.");
Require(
    Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ==
        "id65-authoring-schema7-single-sector-substrate-in-memory-p1" &&
    Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion == 1 &&
    Id65AuthoringSingleSectorSubstrateCompiler.ExpectedStateCount == 108 &&
    Id65AuthoringSingleSectorSubstrateCompiler.LogicalSourceSectorIndex == 216 &&
    Id65AuthoringSingleSectorSubstrateCompiler.PhysicalOutputSectorIndex == 0 &&
    Id65AuthoringSingleSectorSubstrateCompiler.SourceSectorRelativeOffset ==
        0x284A8 &&
    Id65AuthoringSingleSectorSubstrateCompiler.OutputSectorRelativeOffset ==
        0x0C &&
    Id65AuthoringSingleSectorSubstrateCompiler.EnvironmentZeroGapByteLength ==
        0x2849C,
    "The schema-7 profile or fixed single-sector layout pin changed.");

string[][] terrainStates =
[
    [],
    [Id65AuthoringTerrainSetCatalog.Tile1Id],
    [Id65AuthoringTerrainSetCatalog.Tile2Id],
    [Id65AuthoringTerrainSetCatalog.Tile1Id,
     Id65AuthoringTerrainSetCatalog.Tile2Id]
];
var stateInputs = terrainStates
    .SelectMany(terrainIds => Enum.GetValues<Id65RemotePadMobyKind>()
        .SelectMany(kind => Id65RemotePadGrassT107Catalog.Positions
            .Select(position =>
                (TerrainIds: terrainIds, Kind: kind, Position: position))))
    .ToArray();

int states = 0;
int directForwardRejections = 0;
int directRollbackRejections = 0;
int stageForwardRejections = 0;
int stageRollbackRejections = 0;
int occlusionCorruptionRejections = 0;
object stateGate = new();
HashSet<string> stateKeys = new(StringComparer.Ordinal);
HashSet<string> documentHashes = new(StringComparer.Ordinal);
HashSet<string> deterministicHashes = new(StringComparer.Ordinal);
HashSet<string> outputHashes = new(StringComparer.Ordinal);
HashSet<string> outputModelHashes = new(StringComparer.Ordinal);
HashSet<string> environmentHashes = new(StringComparer.Ordinal);
HashSet<string> retainedSectorHashes = new(StringComparer.Ordinal);
HashSet<string> collisionHashes = new(StringComparer.Ordinal);
HashSet<string> occlusionHashes = new(StringComparer.Ordinal);
HashSet<string> cycloramaHashes = new(StringComparer.Ordinal);
HashSet<string> stageTransactionHashes = new(StringComparer.Ordinal);
HashSet<string> componentMapHashes = new(StringComparer.Ordinal);
HashSet<string> handleMapHashes = new(StringComparer.Ordinal);
HashSet<int> environmentLengths = [];
HashSet<int> sectorLengths = [];
string? tile1GrassCenterOutputRowSha256 = null;

Parallel.ForEach(
    stateInputs,
    new ParallelOptions { MaxDegreeOfParallelism = 2 },
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
                "A single-sector state key was compiled twice.");
        }

        Id65AuthoringEditorDocument document =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                    kind,
                    position.PositionId,
                    terrainIds);
        Id65CompiledAuthoringBlankCyclorama schema6 =
            Id65AuthoringBlankCycloramaCompiler.Compile(new(
                Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
                lockedSource,
                v3Template,
                textureWitness,
                grassBundle));
        Id65CompiledAuthoringTerrainSet terrain =
            Id65AuthoringTerrainSetCompiler.CompileTarget(
                Id65AuthoringTerrainSetCatalog.Create(terrainIds),
                v3Template);
        Id65CompiledAuthoringSingleSectorSubstrate first =
            Id65AuthoringSingleSectorSubstrateCompiler.CompileFromSchema6(
                schema6,
                terrain);
        Id65CompiledAuthoringSingleSectorSubstrate repeat =
            Id65AuthoringSingleSectorSubstrateCompiler.CompileFromSchema6(
                schema6,
                terrain);

        int expectedEnvironmentLength = 0x28518 + terrainIds.Length * 0x20;
        int expectedSectorLength = 0x70 + terrainIds.Length * 0x20;
        Require(
            first.ProfileId ==
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId &&
            first.SchemaVersion ==
                Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion &&
            first.Schema6ProfileId ==
                Id65AuthoringBlankCycloramaCompiler.ProfileId &&
            first.InputDocumentSchemaVersion ==
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
            first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
            first.ActiveTerrainUnitIds.SequenceEqual(terrainIds) &&
            first.MobyKind == kind &&
            first.MobyPositionId == position.PositionId &&
            first.SourceRow80Sha256 == lockedSource.Row80Sha256 &&
            first.Schema6BaselineRow80Sha256 == schema6.OutputRow80Sha256 &&
            first.Schema6BaselineModelSha256 == schema6.OutputModelSha256 &&
            first.Schema6DeterministicPlanSha256 ==
                schema6.DeterministicPlanSha256 &&
            first.Row80WadOffset ==
                Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset &&
            first.Row80ByteLength ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .Row80ByteLength &&
            first.ModelRow80Offset ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset &&
            first.ModelByteLength ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength &&
            first.Environment.ComponentByteLength ==
                expectedEnvironmentLength &&
            first.Environment.SectorByteLength == expectedSectorLength,
            "The schema-7 state or exact p2 binding changed: " + stateKey);
        Require(
            first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
            first.OutputModelSha256 == repeat.OutputModelSha256 &&
            first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
            first.TransactionSha256 == repeat.TransactionSha256 &&
            first.ProtectedComplementSha256 ==
                repeat.ProtectedComplementSha256 &&
            first.StageOwnedRangeMapSha256 ==
                repeat.StageOwnedRangeMapSha256 &&
            first.StageTransactionSha256 ==
                repeat.StageTransactionSha256 &&
            first.StageProtectedComplementSha256 ==
                repeat.StageProtectedComplementSha256 &&
            first.ComponentRelocationMapSha256 ==
                repeat.ComponentRelocationMapSha256 &&
            first.HandleRebaseMapSha256 == repeat.HandleRebaseMapSha256 &&
            first.DeterministicPlanSha256 == repeat.DeterministicPlanSha256 &&
            first.CopySourceRow80().SequenceEqual(repeat.CopySourceRow80()) &&
            first.CopySchema6BaselineRow80().SequenceEqual(
                repeat.CopySchema6BaselineRow80()) &&
            first.CopyOutputRow80().SequenceEqual(repeat.CopyOutputRow80()),
            "The locked-input and precompiled-p2 paths diverged: " + stateKey);
        Require(
            first.PureInMemory && first.ExactSchema6DocumentRequired &&
            first.BuildsExactSchema6P2BaselineFromLockedInputs &&
            first.AcceptsExactSchema6P2CompiledInput &&
            !first.CallerSchema6AfterimageAccepted &&
            !first.CallerAfterimagesAccepted &&
            first.ProducesSchema7SingleSectorSubstrate &&
            first.FixedComponentCapacitiesPreserved &&
            first.DownstreamComponentOffsetsPreserved &&
            first.LogicalSector216RebasedToPhysicalSector0 &&
            first.EnvironmentSectorCountIsOne &&
            first.EnvironmentPointerGapZeroed &&
            first.AuthoredOnlyOcclusionRebasedToSector0 &&
            first.InheritedEnvironmentBytesRetired &&
            !first.InheritedCollisionLookupsRetired &&
            first.CollisionPreservedExactly &&
            first.NativeT66TexturePreserved &&
            first.MandatoryBaseFaceT66Preserved &&
            first.TerrainSectorBytesPreservedExactly &&
            first.CycloramaPreservedExactly &&
            first.SpawnAndMobyBytesPreservedExactly &&
            first.ExactInverseVerified &&
            first.ExactSchema6StageInverseVerified &&
            first.EveryChangedByteOwned &&
            first.ProtectedComplementVerified &&
            first.ContainsOutputRow80Bytes &&
            !first.WritesFileSystem && !first.WritesDiscImage &&
            !first.WritesCue && !first.AppIntegrated && !first.UiIntegrated &&
            !first.DocumentPersistenceIntegrated &&
            !first.MaterializerIntegrated && !first.CreateBinEnabled &&
            !first.NormalCreateBinEnabled &&
            !first.RuntimeCandidateAuthorized && !first.RuntimeProven &&
            !first.ReleaseIntegrated && !first.PromotionAuthorized &&
            !first.Publishable,
            "The pure schema-7 stage opened an app/runtime/release gate: " +
            stateKey);

        VerifyEnvironment(first);
        VerifyOcclusion(first);
        VerifyComponents(first);
        VerifyRebases(first);
        VerifyLedgers(first);
        VerifyDefensiveCopies(first);

        byte[] source = first.CopySourceRow80();
        byte[] schema6Row = first.CopySchema6BaselineRow80();
        byte[] output = first.CopyOutputRow80();
        byte[] directForward =
            Id65AuthoringSingleSectorSubstrateCompiler.ApplyTransactional(
                first,
                source,
                reverse: false);
        byte[] directRollback =
            Id65AuthoringSingleSectorSubstrateCompiler.ApplyTransactional(
                first,
                directForward,
                reverse: true);
        byte[] stageForward =
            Id65AuthoringSingleSectorSubstrateCompiler
                .ApplySchema6StageTransactional(
                    first,
                    schema6Row,
                    reverse: false);
        byte[] stageRollback =
            Id65AuthoringSingleSectorSubstrateCompiler
                .ApplySchema6StageTransactional(
                    first,
                    stageForward,
                    reverse: true);
        Require(
            directForward.SequenceEqual(output) &&
            directRollback.SequenceEqual(source) &&
            stageForward.SequenceEqual(output) &&
            stageRollback.SequenceEqual(schema6Row),
            "A schema-7 forward/inverse transaction failed: " + stateKey);

        byte[] forgedDirectPreimage = source.ToArray();
        forgedDirectPreimage[0x400] ^= 1;
        Reject(stateKey + " direct forged preimage", () =>
            Id65AuthoringSingleSectorSubstrateCompiler.ApplyTransactional(
                first,
                forgedDirectPreimage,
                reverse: false));
        Interlocked.Increment(ref directForwardRejections);
        byte[] forgedDirectAfterimage = output.ToArray();
        forgedDirectAfterimage[0x400] ^= 1;
        Reject(stateKey + " direct forged afterimage", () =>
            Id65AuthoringSingleSectorSubstrateCompiler.ApplyTransactional(
                first,
                forgedDirectAfterimage,
                reverse: true));
        Interlocked.Increment(ref directRollbackRejections);
        byte[] forgedStagePreimage = schema6Row.ToArray();
        forgedStagePreimage[0x400] ^= 1;
        Reject(stateKey + " stage forged preimage", () =>
            Id65AuthoringSingleSectorSubstrateCompiler
                .ApplySchema6StageTransactional(
                    first,
                    forgedStagePreimage,
                    reverse: false));
        Interlocked.Increment(ref stageForwardRejections);
        byte[] forgedStageAfterimage = output.ToArray();
        forgedStageAfterimage[0x400] ^= 1;
        Reject(stateKey + " stage forged afterimage", () =>
            Id65AuthoringSingleSectorSubstrateCompiler
                .ApplySchema6StageTransactional(
                    first,
                    forgedStageAfterimage,
                    reverse: true));
        Interlocked.Increment(ref stageRollbackRejections);
        byte[] forgedOcclusion = first.CopyOutputOcclusion();
        forgedOcclusion[
            Id65AuthoringSingleSectorSubstrateCompiler
                .OcclusionSectorByteOffset] = 1;
        Reject(stateKey + " forged sector-zero occlusion", () =>
            Id65AuthoringSingleSectorSubstrateCompiler
                .RequireSingleSectorOcclusion(forgedOcclusion));
        Interlocked.Increment(ref occlusionCorruptionRejections);

        lock (stateGate)
        {
            if (terrainIds.SequenceEqual(
                    [Id65AuthoringTerrainSetCatalog.Tile1Id]))
            {
                Require(first.Environment.OutputEnvironmentSha256 ==
                    ExpectedTile1OutputEnvironmentSha256,
                    "The prospective Tile1 single-sector environment SHA changed: " +
                    stateKey);
                if (kind == Id65RemotePadMobyKind.GrassT107 &&
                    position.PositionId ==
                        Id65RemotePadGrassT107Catalog.CenterPositionId)
                {
                    Require(tile1GrassCenterOutputRowSha256 is null,
                        "The focused Tile1 / Grass T107 / Center state was captured twice.");
                    tile1GrassCenterOutputRowSha256 =
                        first.OutputRow80Sha256;
                }
            }
            Require(documentHashes.Add(first.DocumentCanonicalSha256),
                "Schema-7 document hashes are not unique: " + stateKey);
            Require(deterministicHashes.Add(first.DeterministicPlanSha256),
                "Schema-7 deterministic plans are not unique: " + stateKey);
            outputHashes.Add(first.OutputRow80Sha256);
            outputModelHashes.Add(first.OutputModelSha256);
            environmentHashes.Add(first.Environment.OutputEnvironmentSha256);
            retainedSectorHashes.Add(first.Environment.RetainedSectorSha256);
            collisionHashes.Add(first.CollisionSha256);
            occlusionHashes.Add(first.Occlusion.OutputOcclusionSha256);
            cycloramaHashes.Add(first.OutputCycloramaSha256);
            stageTransactionHashes.Add(first.StageTransactionSha256);
            componentMapHashes.Add(first.ComponentRelocationMapSha256);
            handleMapHashes.Add(first.HandleRebaseMapSha256);
            environmentLengths.Add(first.Environment.ComponentByteLength);
            sectorLengths.Add(first.Environment.SectorByteLength);
        }
        Interlocked.Increment(ref states);
    });

Require(
    states == Id65AuthoringSingleSectorSubstrateCompiler.ExpectedStateCount &&
    stateKeys.Count == 108 && documentHashes.Count == 108 &&
    deterministicHashes.Count == 108 && outputHashes.Count == 76 &&
    outputModelHashes.Count == 4 && environmentHashes.Count == 4 &&
    retainedSectorHashes.Count == 4 && collisionHashes.Count == 4 &&
    occlusionHashes.SetEquals(
        [Id65AuthoringSingleSectorSubstrateCompiler
            .ExpectedOutputOcclusionSha256]) &&
    cycloramaHashes.Count == 1 && stageTransactionHashes.Count == 4 &&
    componentMapHashes.Count == 4 && handleMapHashes.Count == 4 &&
    environmentLengths.SetEquals([0x28518, 0x28538, 0x28558]) &&
    sectorLengths.SetEquals([0x70, 0x90, 0xB0]) &&
    tile1GrassCenterOutputRowSha256 is not null &&
    directForwardRejections == 108 &&
    directRollbackRejections == 108 &&
    stageForwardRejections == 108 &&
    stageRollbackRejections == 108 &&
    occlusionCorruptionRejections == 108,
    "The closed 108-state schema-7 single-sector matrix changed.");

Id65CompiledAuthoringSingleSectorSubstrate directTile1GrassCenter =
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.GrassT107,
                Id65RemotePadGrassT107Catalog.CenterPositionId,
                [Id65AuthoringTerrainSetCatalog.Tile1Id]),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));
Require(
    directTile1GrassCenter.Environment.OutputEnvironmentSha256 ==
        ExpectedTile1OutputEnvironmentSha256 &&
    directTile1GrassCenter.OutputRow80Sha256 ==
        tile1GrassCenterOutputRowSha256,
    "The locked-input compiler path diverged from the 108-state p2 stage for Tile1 / Grass T107 / Center.");

Id65AuthoringEditorDocument exactDocument =
    Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
            []);
Reject("null request", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(null!));
Reject("schema-5 document", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetExact(
                Id65RemotePadMobyKind.None,
                Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
                []),
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle)));
Reject("null schema-6 p2 compiled input", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.CompileFromSchema6(
        null!,
        Id65AuthoringTerrainSetCompiler.CompileTarget(
            Id65AuthoringTerrainSetCatalog.Create([]),
            v3Template)));
Id65CompiledAuthoringBlankCyclorama exactSchema6 =
    Id65AuthoringBlankCycloramaCompiler.Compile(new(
        exactDocument,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));
Reject("null semantic terrain provider", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.CompileFromSchema6(
        exactSchema6,
        null!));
Reject("mismatched semantic terrain provider", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.CompileFromSchema6(
        exactSchema6,
        Id65AuthoringTerrainSetCompiler.CompileTarget(
            Id65AuthoringTerrainSetCatalog.Create(
                [Id65AuthoringTerrainSetCatalog.Tile1Id]),
            v3Template)));
byte[] forgedLockedRow = lockedSource.CopyRow80();
forgedLockedRow[0x400] ^= 1;
Reject("forged locked source", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        exactDocument,
        new Id65AuthoringCompositeLockedSource(
            forgedLockedRow,
            lockedSource.CopyTargetOverlay(),
            lockedSource.CopyGlobalExecutable()),
        v3Template,
        textureWitness,
        grassBundle)));
byte[] forgedV3Output = v3Template.OutputData.ToArray();
forgedV3Output[0x400] ^= 1;
Reject("forged v3 afterimage", () =>
    Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
        exactDocument,
        lockedSource,
        v3Template with { OutputData = forgedV3Output },
        textureWitness,
        grassBundle)));

Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The pure single-sector compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure single-sector compiler changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_SINGLE_SECTOR_SUBSTRATE_COMPILER_SMOKE_OK " +
    "states=108 rowHashes=76 modelHashes=4 environmentHashes=4 " +
    "sectorHashes=4 collisionHashes=4 occlusionHashes=1 " +
    "deterministicHashes=108 directForwardRejections=108 " +
    "directRollbackRejections=108 stageForwardRejections=108 " +
    "stageRollbackRejections=108 occlusionCorruptionRejections=108 " +
    "fixedCapacities=1 downstreamOffsets=1 collisionPreserved=1 " +
    $"tile1EnvironmentSha={ExpectedTile1OutputEnvironmentSha256} " +
    $"tile1GrassCenterOutputRowSha={tile1GrassCenterOutputRowSha256} " +
    "writes=0 runtime=0 release=0");
return;

static void VerifyEnvironment(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    Id65AuthoringSingleSectorEnvironmentReadback readback =
        compiled.Environment;
    byte[] baseline = compiled.CopySchema6BaselineEnvironment();
    byte[] output = compiled.CopyOutputEnvironment();
    int sourceSectorOffset =
        Id65AuthoringSingleSectorSubstrateCompiler.SourceSectorRelativeOffset;
    int outputSectorOffset =
        Id65AuthoringSingleSectorSubstrateCompiler.OutputSectorRelativeOffset;
    ReadOnlySpan<byte> sourceSector = baseline.AsSpan(
        sourceSectorOffset,
        readback.SectorByteLength);
    ReadOnlySpan<byte> outputSector = output.AsSpan(
        outputSectorOffset,
        readback.SectorByteLength);
    ReadOnlySpan<byte> zeroGap = output.AsSpan(
        outputSectorOffset + readback.SectorByteLength);
    Require(
        readback.ModelOffset ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .EnvironmentModelOffset &&
        readback.Row80Offset == compiled.ModelRow80Offset +
            readback.ModelOffset &&
        readback.WadOffset == compiled.Row80WadOffset +
            readback.Row80Offset &&
        readback.LogicalSourceSectorIndex == 216 &&
        readback.PhysicalOutputSectorIndex == 0 &&
        readback.SourceSectorRelativeOffset == sourceSectorOffset &&
        readback.OutputSectorRelativeOffset == outputSectorOffset &&
        readback.PointerGapByteLength == 0x2849C &&
        ReadInt32(baseline, 0) == baseline.Length &&
        ReadInt32(baseline, 4) == 217 &&
        ReadInt32(baseline, 8 + 216 * 4) == 0x284A4 &&
        sourceSectorOffset + readback.SectorByteLength == baseline.Length &&
        ReadInt32(output, 0) == output.Length &&
        ReadInt32(output, 4) == 1 &&
        ReadInt32(output, 8) == 8 &&
        sourceSector.SequenceEqual(outputSector) &&
        zeroGap.Length == 0x2849C &&
        zeroGap.IndexOfAnyExcept((byte)0) < 0 &&
        Hash(baseline) == readback.BaselineEnvironmentSha256 &&
        Hash(output) == readback.OutputEnvironmentSha256 &&
        Hash(sourceSector) == readback.RetainedSectorSha256 &&
        Hash(zeroGap) == readback.ZeroGapSha256 &&
        readback.ZeroGapSha256 ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedZeroGapSha256 &&
        readback.HighDetailFaceCount > 0,
        "The exact sector-216 to sector-0 environment copy changed.");
}

static void VerifyOcclusion(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    Id65AuthoringSingleSectorOcclusionReadback readback = compiled.Occlusion;
    byte[] baseline = compiled.CopySchema6BaselineOcclusion();
    byte[] output = compiled.CopyOutputOcclusion();
    int changed = CountChangedBytes(baseline, output);
    Require(
        readback.Row80Offset == compiled.ModelRow80Offset +
            readback.ModelOffset &&
        readback.WadOffset == compiled.Row80WadOffset +
            readback.Row80Offset &&
        readback.ComponentByteLength == 0x974 &&
        readback.GroupCount == 16 && readback.OwnerGroupIndex == 0 &&
        readback.LogicalSourceSectorIndex == 216 &&
        readback.PhysicalOutputSectorIndex == 0 &&
        readback.ChangedByteCount == 1 && changed == 1 &&
        baseline[
            Id65AuthoringSingleSectorSubstrateCompiler
                .OcclusionSectorByteOffset] == 216 &&
        output[
            Id65AuthoringSingleSectorSubstrateCompiler
                .OcclusionSectorByteOffset] == 0 &&
        Hash(baseline) ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedSchema6OcclusionSha256 &&
        Hash(output) ==
            Id65AuthoringSingleSectorSubstrateCompiler
                .ExpectedOutputOcclusionSha256 &&
        Hash(output.AsSpan(
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
            Id65AuthoringBlankCycloramaCompiler
                .VisibilityOpaqueTailByteLength)) ==
            readback.OpaqueTailSha256,
        "The exact p2 [216] to schema-7 [0] occlusion rebase changed.");
    Id65AuthoringSingleSectorSubstrateCompiler
        .RequireSingleSectorOcclusion(output);
}

static void VerifyComponents(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    byte[] before = compiled.CopySchema6BaselineModel();
    byte[] after = compiled.CopyOutputModel();
    Require(before.Length == compiled.ModelByteLength &&
        after.Length == compiled.ModelByteLength &&
        compiled.ComponentRelocations.Count == 9 &&
        compiled.ComponentRelocations.All(item =>
            item.SourceRelativeOffset == item.OutputRelativeOffset &&
            item.SourceByteLength == item.OutputByteLength) &&
        compiled.ComponentRelocations.Where(item => !item.ContentsPreserved)
            .Select(item => item.StableId).SequenceEqual(
                ["component.environment", "component.occlusion"]),
        "The fixed component capacity or downstream offsets changed.");
    foreach (Id65ModelComponentRelocation component in
             compiled.ComponentRelocations)
    {
        Require(
            Hash(before.AsSpan(
                component.SourceRelativeOffset,
                component.SourceByteLength)) == component.SourceSha256 &&
            Hash(after.AsSpan(
                component.OutputRelativeOffset,
                component.OutputByteLength)) == component.OutputSha256,
            "A component relocation hash lost its exact byte binding.");
    }
    Id65ModelComponentRelocation collision =
        compiled.ComponentRelocations.Single(item =>
            item.StableId == "component.collision");
    Require(
        collision.ContentsPreserved &&
        collision.SourceSha256 == collision.OutputSha256 &&
        collision.OutputSha256 == compiled.CollisionSha256 &&
        before.AsSpan(collision.SourceRelativeOffset, collision.SourceByteLength)
            .SequenceEqual(after.AsSpan(
                collision.OutputRelativeOffset,
                collision.OutputByteLength)) &&
        compiled.ComponentRelocations.Single(item =>
            item.StableId == "component.texture").OutputSha256 ==
                compiled.OutputTextureComponentSha256 &&
        compiled.ComponentRelocations.Single(item =>
            item.StableId == "component.cyclorama").OutputSha256 ==
                compiled.OutputCycloramaSha256,
        "Collision, T66 texture, or blank cyclorama was not byte-preserved.");
}

static void VerifyRebases(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    Require(compiled.HandleRebases.Count > 0 &&
        compiled.HandleRebases.Select(item => item.StableId)
            .Distinct(StringComparer.Ordinal).Count() ==
                compiled.HandleRebases.Count,
        "The schema-7 stable-handle ledger is empty or duplicated.");
    bool sawSceneSector = false;
    bool sawOcclusionGroup = false;
    foreach (Id65StableHandleRebase rebase in compiled.HandleRebases)
    {
        Require(rebase.Source.HasValue && rebase.Output.HasValue,
            "A schema-7 stable handle became unresolved.");
        Id65LogicalAddress source = rebase.Source ?? throw new(
            "A schema-7 stable-handle source became unresolved.");
        Id65LogicalAddress output = rebase.Output ?? throw new(
            "A schema-7 stable-handle output became unresolved.");
        Id65LogicalAddress expected = source switch
        {
            { Space: "scene-sector", Primary: 216 } =>
                source with { Primary = 0 },
            { Space: "terrain-tile", Primary: 216 } =>
                source with { Primary = 0 },
            { Space: "occlusion-group", Secondary: 216 } =>
                source with { Secondary = 0 },
            _ => source
        };
        Require(output == expected,
            "A stable handle has an incorrect schema-7 rebase.");
        sawSceneSector |= source.Space == "scene-sector" &&
            source.Primary == 216 && output.Primary == 0;
        sawOcclusionGroup |= source.Space == "occlusion-group" &&
            source.Secondary == 216 && output.Secondary == 0;
    }
    Require(sawSceneSector && sawOcclusionGroup,
        "The stable handles did not prove both scene and occlusion rebases.");
}

static void VerifyLedgers(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    byte[] source = compiled.CopySourceRow80();
    byte[] baseline = compiled.CopySchema6BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    VerifyLedger(
        source,
        output,
        compiled.OwnedRanges,
        compiled.InverseRanges,
        compiled.ChangedByteCount,
        compiled.ProtectedComplementSha256);
    VerifyLedger(
        baseline,
        output,
        compiled.StageOwnedRanges,
        compiled.StageInverseRanges,
        compiled.StageChangedByteCount,
        compiled.StageProtectedComplementSha256);
    Require(
        compiled.StageOwnedRanges.All(item =>
            item.OwnerId is
                Id65AuthoringSingleSectorSubstrateCompiler.EnvironmentOwnerId or
                Id65AuthoringSingleSectorSubstrateCompiler.OcclusionOwnerId) &&
        compiled.StageOwnedRanges.Any(item =>
            item.StableId.StartsWith(
                "schema7.stage.environment.",
                StringComparison.Ordinal) &&
            item.OwnerId ==
                Id65AuthoringSingleSectorSubstrateCompiler
                    .EnvironmentOwnerId) &&
        compiled.StageOwnedRanges.Any(item =>
            item.StableId.StartsWith(
                "schema7.stage.occlusion.",
                StringComparison.Ordinal) &&
            item.OwnerId ==
                Id65AuthoringSingleSectorSubstrateCompiler
                    .OcclusionOwnerId) &&
        compiled.OwnedRanges.Any(item =>
            item.StableId.StartsWith(
                "schema7.row80.environment.",
                StringComparison.Ordinal)) &&
        compiled.OwnedRanges.Any(item =>
            item.StableId.StartsWith(
                "schema7.row80.occlusion.",
                StringComparison.Ordinal)),
        "The schema-7 direct/stage ownership domains changed.");
}

static void VerifyLedger(
    byte[] source,
    byte[] output,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> inverse,
    int expectedChanged,
    string expectedProtectedComplement)
{
    Require(ranges.Count > 0 && inverse.Count == ranges.Count &&
        ranges.Select(item => item.StableId)
            .Distinct(StringComparer.Ordinal).Count() == ranges.Count,
        "A schema-7 ledger is empty, duplicated, or lacks its inverse.");
    int changed = 0;
    for (int index = 0; index < ranges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange range = ranges[index];
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        Id65AuthoringCompositeOwnedRange rollback =
            inverse[inverse.Count - 1 - index];
        Require(
            range.DataRelativeOffset >= 0 && range.ByteLength > 0 &&
            range.DataRelativeOffset + range.ByteLength <= source.Length &&
            source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before) &&
            output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(after) &&
            !before.SequenceEqual(after) &&
            Hash(before) == range.BeforeSha256 &&
            Hash(after) == range.AfterSha256 &&
            (index == 0 ||
             ranges[index - 1].DataRelativeOffset +
                ranges[index - 1].ByteLength <= range.DataRelativeOffset) &&
            rollback.StableId == range.StableId &&
            rollback.OwnerId == range.OwnerId &&
            rollback.DataRelativeOffset == range.DataRelativeOffset &&
            rollback.CopyBefore().SequenceEqual(after) &&
            rollback.CopyAfter().SequenceEqual(before),
            "A schema-7 ledger lost bounds, hash, nonoverlap, or inverse.");
        changed += CountChangedBytes(before, after);
    }
    Require(changed == expectedChanged &&
        changed == CountChangedBytes(source, output) &&
        HashProtectedComplement(source, ranges) ==
            expectedProtectedComplement &&
        HashProtectedComplement(output, ranges) ==
            expectedProtectedComplement,
        "Changed-byte ownership or the protected complement changed.");
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringSingleSectorSubstrate compiled)
{
    string sourceHash = Hash(compiled.CopySourceRow80());
    string baselineHash = Hash(compiled.CopySchema6BaselineRow80());
    string outputHash = Hash(compiled.CopyOutputRow80());
    string environmentHash = Hash(compiled.CopyOutputEnvironment());
    string occlusionHash = Hash(compiled.CopyOutputOcclusion());
    byte[] source = compiled.CopySourceRow80();
    byte[] baseline = compiled.CopySchema6BaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    byte[] environment = compiled.CopyOutputEnvironment();
    byte[] occlusion = compiled.CopyOutputOcclusion();
    source[0] ^= 1;
    baseline[0] ^= 1;
    output[0] ^= 1;
    environment[0] ^= 1;
    occlusion[0] ^= 1;
    byte[] before = compiled.OwnedRanges[0].CopyBefore();
    before[0] ^= 1;
    Require(
        Hash(compiled.CopySourceRow80()) == sourceHash &&
        Hash(compiled.CopySchema6BaselineRow80()) == baselineHash &&
        Hash(compiled.CopyOutputRow80()) == outputHash &&
        Hash(compiled.CopyOutputEnvironment()) == environmentHash &&
        Hash(compiled.CopyOutputOcclusion()) == occlusionHash &&
        Hash(compiled.OwnedRanges[0].CopyBefore()) ==
            compiled.OwnedRanges[0].BeforeSha256 &&
        compiled.ActiveTerrainUnitIds is ReadOnlyCollection<string> &&
        compiled.OwnedRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.InverseRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.StageOwnedRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.StageInverseRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.ComponentRelocations is
            ReadOnlyCollection<Id65ModelComponentRelocation> &&
        compiled.HandleRebases is
            ReadOnlyCollection<Id65StableHandleRebase>,
        "The schema-7 result leaked mutable bytes or collections.");
}

static string HashProtectedComplement(
    byte[] bytes,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
{
    using IncrementalHash hash =
        IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    int cursor = 0;
    foreach (Id65AuthoringCompositeOwnedRange range in ranges)
    {
        if (range.DataRelativeOffset > cursor)
            hash.AppendData(bytes.AsSpan(
                cursor,
                range.DataRelativeOffset - cursor));
        cursor = range.DataRelativeOffset + range.ByteLength;
    }
    if (cursor < bytes.Length) hash.AppendData(bytes.AsSpan(cursor));
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}

static int CountChangedBytes(
    ReadOnlySpan<byte> before,
    ReadOnlySpan<byte> after)
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

static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
    BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));

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

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Order(StringComparer.Ordinal)
        .ToArray()!;

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

internal sealed record FileState(
    long Length,
    DateTime LastWriteUtc,
    string Sha256);
