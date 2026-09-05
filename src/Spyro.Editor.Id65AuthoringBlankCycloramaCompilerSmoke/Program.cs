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
    "Missing exact blank-cyclorama compiler fixture: " + lockedImagePath);

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
    v3Template.OutputDataSha256 ==
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
            .ExpectedOutputDataSha256 &&
    textureWitness.CanonicalSha256 ==
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
    grassBundle.CanonicalSha256 ==
        Id65RemotePadGrassT107Catalog.BundleCanonicalSha256,
    "The locked row, v3 provider, T66 witness, or Grass bundle pin changed.");
Require(
    Id65AuthoringBlankCycloramaCompiler.BlankSlateV1PresetId ==
        Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
    Id65AuthoringBlankCycloramaCompiler.BlankSlateV1ColorHex ==
        Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
    Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex == "#263447" &&
    (Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red,
     Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green,
     Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue) ==
        ((byte)0x26, (byte)0x34, (byte)0x47) &&
    !Id65AuthoringBlankCycloramaCatalog.ContainsNativePayloadBytes &&
    !Id65AuthoringBlankCycloramaCatalog.ContainsPhysicalAddresses &&
    !Id65AuthoringBlankCycloramaCatalog.WritesFileSystem &&
    !Id65AuthoringBlankCycloramaCatalog.WritesDiscImage &&
    !Id65AuthoringBlankCycloramaCatalog.NormalCreateBinEnabled &&
    !Id65AuthoringBlankCycloramaCatalog.RuntimeCandidateAuthorized &&
    !Id65AuthoringBlankCycloramaCatalog.ReleaseIntegrated &&
    !Id65AuthoringBlankCycloramaCatalog.Publishable,
    "The document-owned blank-slate-v1 catalog or blue-gray RGB pin changed.");

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
int forwardRejections = 0;
int rollbackRejections = 0;
object stateGate = new();
HashSet<string> stateKeys = new(StringComparer.Ordinal);
HashSet<string> documentHashes = new(StringComparer.Ordinal);
HashSet<string> deterministicHashes = new(StringComparer.Ordinal);
HashSet<string> baselineOutputHashes = new(StringComparer.Ordinal);
HashSet<string> outputHashes = new(StringComparer.Ordinal);
HashSet<string> outputModelHashes = new(StringComparer.Ordinal);
HashSet<string> cycloramaHashes = new(StringComparer.Ordinal);
HashSet<string> geometryHashes = new(StringComparer.Ordinal);
HashSet<string> commandHashes = new(StringComparer.Ordinal);
HashSet<int> cycloramaOffsets = [];
HashSet<long> cycloramaWadOffsets = [];
HashSet<int> cycloramaRgbChangedCounts = [];

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
                "A blank-cyclorama state key was compiled twice.");
        }

        Id65AuthoringEditorDocument document =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                    kind,
                    position.PositionId,
                    terrainIds);
        Id65CompiledAuthoringBlankCyclorama first = Compile(
            document,
            lockedSource,
            v3Template);
        Id65CompiledAuthoringBlankCyclorama repeat = Compile(
            Id65AuthoringEditorDocument.Parse(document.SerializeUtf8()),
            lockedSource,
            v3Template);

        int expectedModelOffset = terrainIds.Length switch
        {
            0 => Id65AuthoringBlankCycloramaCompiler
                .EmptyTerrainCycloramaModelOffset,
            1 => Id65AuthoringBlankCycloramaCompiler
                .OneTerrainCycloramaModelOffset,
            2 => Id65AuthoringBlankCycloramaCompiler
                .TwoTerrainCycloramaModelOffset,
            _ => throw new InvalidOperationException(
                "The smoke constructed an invalid terrain topology.")
        };
        long expectedWadOffset = terrainIds.Length switch
        {
            0 => Id65AuthoringBlankCycloramaCompiler
                .EmptyTerrainCycloramaWadOffset,
            1 => Id65AuthoringBlankCycloramaCompiler
                .OneTerrainCycloramaWadOffset,
            2 => Id65AuthoringBlankCycloramaCompiler
                .TwoTerrainCycloramaWadOffset,
            _ => throw new InvalidOperationException(
                "The smoke constructed an invalid terrain topology.")
        };
        Id65AuthoringEditorDocument schema5Projection =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetExact(
                    kind,
                    position.PositionId,
                    terrainIds);
        Require(
            first.ProfileId ==
                Id65AuthoringBlankCycloramaCompiler.ProfileId &&
            first.SchemaVersion ==
                Id65AuthoringBlankCycloramaCompiler.SchemaVersion &&
            first.InputDocumentSchemaVersion ==
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
            first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
            first.BaselineDocumentCanonicalSha256 ==
                schema5Projection.CanonicalSha256 &&
            first.Document.UsesBlankCyclorama &&
            first.Document.BlankCyclorama ==
                Id65AuthoringBlankCycloramaCatalog.CreateBlankSlate() &&
            first.PresetId ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
            first.ColorHex ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
            first.ActiveTerrainUnitIds.SequenceEqual(terrainIds) &&
            first.MobyKind == kind &&
            first.MobyPositionId == position.PositionId &&
            first.CycloramaModelOffset == expectedModelOffset &&
            first.CycloramaRow80Offset ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    expectedModelOffset &&
            first.CycloramaWadOffset ==
                Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
                    first.CycloramaRow80Offset &&
            first.CycloramaWadOffset == expectedWadOffset &&
            first.CycloramaWadOffset !=
                Id65AuthoringBlankCycloramaCompiler
                    .RetailTownSquareCycloramaWadOffset &&
            first.CycloramaWadOffset !=
                Id65AuthoringBlankCycloramaCompiler
                    .RetailArtisansLinkedCycloramaWadOffset &&
            first.CycloramaByteLength ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedCycloramaByteLength &&
            first.PaletteWordCount ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedPaletteWordCount &&
            first.PaletteOwnedRangeCount ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedPaletteWordCount &&
            first.BaselineLayout.PartCount == 35 &&
            first.BaselineLayout.VertexCount == 1915 &&
            first.BaselineLayout.ColorCount == 726 &&
            first.BaselineLayout.PolygonCount == 2808 &&
            first.BaselineLayout.TrailingBytes == 0 &&
            first.BaselineCycloramaSha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedSourceCycloramaSha256 &&
            first.BaselineLayout.GeometrySha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedGeometrySha256 &&
            first.GeometrySha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedGeometrySha256 &&
            first.BaselineRow80Sha256 != first.OutputRow80Sha256 &&
            first.BaselineModelSha256 != first.OutputModelSha256 &&
            first.CycloramaChangedRgbByteCount > 0 &&
            first.CycloramaChangedRgbByteCount <=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedPaletteWordCount * 3 &&
            first.VisibilityOcclusionModelOffset ==
                0x2B548 + terrainIds.Length * 0x20 &&
            first.VisibilityOcclusionRow80Offset ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    first.VisibilityOcclusionModelOffset &&
            first.VisibilityOcclusionWadOffset ==
                Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
                    first.VisibilityOcclusionRow80Offset &&
            first.VisibilityOcclusionByteLength ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOcclusionByteLength &&
            first.BaselineVisibilityOcclusionSha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedBaselineVisibilityOcclusionSha256 &&
            first.OutputVisibilityOcclusionSha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedOutputVisibilityOcclusionSha256 &&
            first.VisibilityOpaqueTailSha256 ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOpaqueTailSha256 &&
            first.InheritedVisibleSectorCount ==
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedInheritedVisibleSectorCount &&
            first.VisibleAuthoredSectorIndex == 216 &&
            first.VisibleSectorCount == 1 &&
            first.BlankVisibilityChangedByteCount > 0,
            "The exact blank-cyclorama layout, state, or relocation changed: " +
            stateKey);
        Require(
            first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
            first.OutputModelSha256 == repeat.OutputModelSha256 &&
            first.OutputCycloramaSha256 == repeat.OutputCycloramaSha256 &&
            first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
            first.TransactionSha256 == repeat.TransactionSha256 &&
            first.ProtectedComplementSha256 ==
                repeat.ProtectedComplementSha256 &&
            first.NonPaletteSha256 == repeat.NonPaletteSha256 &&
            first.CommandByteSha256 == repeat.CommandByteSha256 &&
            first.DeterministicPlanSha256 ==
                repeat.DeterministicPlanSha256 &&
            first.CopyOutputRow80().SequenceEqual(repeat.CopyOutputRow80()),
            "The blank-cyclorama compile was not deterministic: " + stateKey);
        Require(
            first.PureInMemory && first.ExactSchema6DocumentRequired &&
            first.ReadsBlankCycloramaIntentFromDocument &&
            first.CodeOwnedBlankSlateV1IntentRequired &&
            first.BuildsSchema5BaselineFromLockedInputs &&
            !first.CallerBaselineAccepted &&
            first.StandaloneSkyReparseVerified &&
            first.FrozenGeometryVerified &&
            first.EveryPaletteWordOwnedExactlyOnce &&
            first.PaletteCommandBytesPreserved &&
            first.EveryNonPaletteBytePreserved &&
            first.AuthoredOnlyVisibilityMapVerified &&
            first.InheritedEnvironmentVisibilitySuppressed &&
            first.VisibilityOpaqueTailPreserved &&
            !first.InheritedEnvironmentBytesRetired &&
            !first.InheritedCollisionLookupsRetired &&
            first.ExactInverseVerified && first.EveryChangedByteOwned &&
            first.ProtectedComplementVerified &&
            first.ContainsOutputRow80Bytes &&
            !first.CallsNativeSkyPatchExporter &&
            !first.LinkedPrimarySkyCopiesTouched &&
            !first.RetailArtisansTouched &&
            !first.RetailTownSquareTouched &&
            !first.WritesFileSystem && !first.WritesDiscImage &&
            !first.WritesCue && !first.AppIntegrated && !first.UiIntegrated &&
            !first.DocumentPersistenceIntegrated &&
            !first.MaterializerIntegrated && !first.CreateBinEnabled &&
            !first.NormalCreateBinEnabled &&
            !first.RuntimeCandidateAuthorized && !first.RuntimeProven &&
            !first.ReleaseIntegrated && !first.PromotionAuthorized &&
            !first.Publishable,
            "The blank-cyclorama compiler opened a file/app/runtime/release gate: " +
            stateKey);

        VerifyPaletteOnly(first);
        VerifyOwnedRanges(first);
        VerifyDefensiveCopies(first);
        byte[] source = first.CopySourceRow80();
        byte[] output = first.CopyOutputRow80();
        byte[] applied =
            Id65AuthoringBlankCycloramaCompiler.ApplyTransactional(
                first,
                source,
                reverse: false);
        byte[] rolledBack =
            Id65AuthoringBlankCycloramaCompiler.ApplyTransactional(
                first,
                applied,
                reverse: true);
        Require(applied.SequenceEqual(output) &&
            rolledBack.SequenceEqual(source),
            "The blank-cyclorama exact forward/inverse failed: " + stateKey);

        byte[] forgedPreimage = source.ToArray();
        forgedPreimage[0x400] ^= 1;
        Reject(stateKey + " forged preimage", () =>
            Id65AuthoringBlankCycloramaCompiler.ApplyTransactional(
                first,
                forgedPreimage,
                reverse: false));
        Interlocked.Increment(ref forwardRejections);
        byte[] forgedAfterimage = output.ToArray();
        forgedAfterimage[0x400] ^= 1;
        Reject(stateKey + " forged afterimage", () =>
            Id65AuthoringBlankCycloramaCompiler.ApplyTransactional(
                first,
                forgedAfterimage,
                reverse: true));
        Interlocked.Increment(ref rollbackRejections);

        lock (stateGate)
        {
            Require(documentHashes.Add(first.DocumentCanonicalSha256),
                "Blank-cyclorama document hashes are not unique: " + stateKey);
            Require(deterministicHashes.Add(first.DeterministicPlanSha256),
                "Blank-cyclorama deterministic plans are not unique: " +
                stateKey);
            baselineOutputHashes.Add(first.BaselineRow80Sha256);
            outputHashes.Add(first.OutputRow80Sha256);
            outputModelHashes.Add(first.OutputModelSha256);
            cycloramaHashes.Add(first.OutputCycloramaSha256);
            geometryHashes.Add(first.GeometrySha256);
            commandHashes.Add(first.CommandByteSha256);
            cycloramaOffsets.Add(first.CycloramaModelOffset);
            cycloramaWadOffsets.Add(first.CycloramaWadOffset);
            cycloramaRgbChangedCounts.Add(
                first.CycloramaChangedRgbByteCount);
        }
        Interlocked.Increment(ref states);
    });

Require(
    states == Id65AuthoringBlankCycloramaCompiler.ExpectedStateCount &&
    stateKeys.Count == 108 && documentHashes.Count == 108 &&
    deterministicHashes.Count == 108 &&
    baselineOutputHashes.Count == 76 && outputHashes.Count == 76 &&
    outputModelHashes.Count == 4 && cycloramaHashes.Count == 1 &&
    geometryHashes.SetEquals(
        [Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256]) &&
    commandHashes.Count == 1 && cycloramaOffsets.SetEquals(
        [
            Id65AuthoringBlankCycloramaCompiler
                .EmptyTerrainCycloramaModelOffset,
            Id65AuthoringBlankCycloramaCompiler
                .OneTerrainCycloramaModelOffset,
            Id65AuthoringBlankCycloramaCompiler
                .TwoTerrainCycloramaModelOffset
        ]) &&
    cycloramaWadOffsets.SetEquals(
        [
            Id65AuthoringBlankCycloramaCompiler
                .EmptyTerrainCycloramaWadOffset,
            Id65AuthoringBlankCycloramaCompiler
                .OneTerrainCycloramaWadOffset,
            Id65AuthoringBlankCycloramaCompiler
                .TwoTerrainCycloramaWadOffset
        ]) &&
    cycloramaRgbChangedCounts.Count == 1 &&
    forwardRejections == 108 && rollbackRejections == 108,
    "The closed 108-state blank-cyclorama matrix changed.");

Id65AuthoringEditorDocument exactDocument =
    Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []);
Reject("null request", () =>
    Id65AuthoringBlankCycloramaCompiler.Compile(null!));
Reject("wrong preset", () =>
    Id65AuthoringBlankCycloramaCatalog.RequireExact(
        new("town-square", "#263447")));
Reject("black color", () =>
    Id65AuthoringBlankCycloramaCatalog.RequireExact(
        new("blank-slate-v1", "#000000")));
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
Reject("schema-5 document", () => Compile(
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        []),
    lockedSource,
    v3Template));

byte[] forgedLockedRow = lockedSource.CopyRow80();
forgedLockedRow[0x400] ^= 1;
Id65AuthoringCompositeLockedSource forgedLockedSource = new(
    forgedLockedRow,
    lockedSource.CopyTargetOverlay(),
    lockedSource.CopyGlobalExecutable());
Reject("forged locked source", () => Compile(
    exactDocument,
    forgedLockedSource,
    v3Template));
byte[] forgedV3Source = v3Template.SourceData.ToArray();
forgedV3Source[0x400] ^= 1;
Reject("forged v3 preimage", () => Compile(
    exactDocument,
    lockedSource,
    v3Template with { SourceData = forgedV3Source }));
byte[] forgedV3Output = v3Template.OutputData.ToArray();
forgedV3Output[0x400] ^= 1;
Reject("forged v3 afterimage", () => Compile(
    exactDocument,
    lockedSource,
    v3Template with { OutputData = forgedV3Output }));

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
    Id65AuthoringBlankCycloramaCompiler.Compile(new(
        exactDocument,
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
    Id65AuthoringBlankCycloramaCompiler.Compile(new(
        exactDocument,
        lockedSource,
        v3Template,
        textureWitness,
        forgedGrassBundle)));
Reject("both-terrain final model limit", () =>
    Id65AuthoringBlankCycloramaCompiler.Compile(new(
        Id65AuthoringEditorDocument
            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
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
                Id65V2NativeTextureCompositionCompiler
                    .OutputUsedModelBytes))));

byte[] validSky = exactDocument is not null
    ? Id65AuthoringBlankCycloramaCompiler.Compile(new(
        exactDocument,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle)).CopyOutputRow80()
    : [];
int validSkyOffset =
    Id65AuthoringBlankCycloramaCompiler.EmptyTerrainCycloramaRow80Offset;
byte[] standaloneSky = validSky.AsSpan(
    validSkyOffset,
    Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength).ToArray();
Spyro1SkyBlockLayout standaloneLayout =
    Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(standaloneSky);
Require(standaloneLayout.GeometrySha256 ==
    Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256,
    "The internal standalone-sky helper lost the compiled geometry identity.");
Reject("truncated standalone sky", () =>
    Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
        standaloneSky[..^4]));
Reject("appended standalone sky", () =>
    Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
        [.. standaloneSky, 0, 0, 0, 0]));

Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The pure blank-cyclorama compiler changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The pure blank-cyclorama compiler changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_BLANK_CYCLORAMA_COMPILER_SMOKE_OK " +
    "states=108 paletteWords=727 offsets=3 outputHashes=76 " +
    "modelHashes=4 deterministicHashes=108 " +
    $"rgbChanged={cycloramaRgbChangedCounts.Single()} " +
    $"cycloramaSha={cycloramaHashes.Single()} " +
    "forwardRejections=108 rollbackRejections=108 faults=16 " +
    "writes=0 linkedArtisans=0 runtime=0 release=0");
return;

Id65CompiledAuthoringBlankCyclorama Compile(
    Id65AuthoringEditorDocument document,
    Id65AuthoringCompositeLockedSource source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3) =>
    Id65AuthoringBlankCycloramaCompiler.Compile(new(
        document,
        source,
        v3,
        textureWitness,
        grassBundle));

static void VerifyPaletteOnly(
    Id65CompiledAuthoringBlankCyclorama compiled)
{
    byte[] baseline = compiled.CopyBaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    int[] paletteOffsets = BuildPaletteOffsets(compiled);
    bool[] rgb = new bool[baseline.Length];
    Array.Fill(
        rgb,
        true,
        compiled.VisibilityOcclusionRow80Offset,
        compiled.VisibilityOcclusionByteLength);
    byte[] visibility = output.AsSpan(
        compiled.VisibilityOcclusionRow80Offset,
        compiled.VisibilityOcclusionByteLength).ToArray();
    Require(
        Id65AuthoringBlankCycloramaCompiler.Hash(visibility) ==
            Id65AuthoringBlankCycloramaCompiler
                .ExpectedOutputVisibilityOcclusionSha256,
        "The compiled authored-only visibility directory changed.");
    foreach (int offset in paletteOffsets)
    {
        rgb[offset] = true;
        rgb[offset + 1] = true;
        rgb[offset + 2] = true;
        Require(
            (baseline[offset], baseline[offset + 1], baseline[offset + 2]) !=
                (Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red,
                 Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green,
                 Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue) &&
            output[offset] ==
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red &&
            output[offset + 1] ==
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green &&
            output[offset + 2] ==
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue &&
            output[offset + 3] == baseline[offset + 3],
            "A compiled palette word lost RGB or command-byte identity.");
    }
    for (int index = 0; index < baseline.Length; index++)
    {
        Require(rgb[index] || baseline[index] == output[index],
            "The compiled blank cyclorama changed a nonpalette byte.");
    }
}

static void VerifyOwnedRanges(
    Id65CompiledAuthoringBlankCyclorama compiled)
{
    byte[] source = compiled.CopySourceRow80();
    byte[] output = compiled.CopyOutputRow80();
    int[] paletteOffsets = BuildPaletteOffsets(compiled);
    Id65AuthoringCompositeOwnedRange[] paletteRanges = compiled.OwnedRanges
        .Where(item => item.OwnerId ==
            Id65AuthoringBlankCycloramaCompiler.PaletteOwnerId)
        .ToArray();
    Require(
        paletteRanges.Length == 727 &&
        paletteRanges.Select(item => item.DataRelativeOffset)
            .SequenceEqual(paletteOffsets) &&
        paletteRanges.Select(item => item.StableId).SequenceEqual(
            Enumerable.Range(0, 727).Select(index =>
                $"schema6.cyclorama.palette.{index:D4}")) &&
        paletteRanges.All(item => item.ByteLength == 4),
        "The compiled ledger lost its 727 exact palette words.");
    int changed = 0;
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange range = compiled.OwnedRanges[index];
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        Require(range.ByteLength > 0 && range.DataRelativeOffset >= 0 &&
            range.DataRelativeOffset + range.ByteLength <= source.Length &&
            source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before) &&
            output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(after) &&
            !before.SequenceEqual(after) &&
            (index == 0 ||
             compiled.OwnedRanges[index - 1].DataRelativeOffset +
                compiled.OwnedRanges[index - 1].ByteLength <=
                range.DataRelativeOffset),
            "A blank-cyclorama range lost preimage/afterimage/nonoverlap.");
        changed += CountChangedBytes(before, after);
    }
    Require(changed == compiled.ChangedByteCount &&
        compiled.InverseRanges.Count == compiled.OwnedRanges.Count,
        "Blank-cyclorama changed-byte ownership or inverse cardinality changed.");
}

static int[] BuildPaletteOffsets(
    Id65CompiledAuthoringBlankCyclorama compiled)
{
    return new[]
        {
            compiled.CycloramaRow80Offset +
                compiled.OutputLayout.BackgroundColorOffset
        }
        .Concat(compiled.OutputLayout.Parts.SelectMany(part =>
            Enumerable.Range(0, part.ColorCount).Select(index =>
                compiled.CycloramaRow80Offset +
                    part.ColorTableOffset + (index * 4))))
        .Order()
        .ToArray();
}

static void VerifyDefensiveCopies(
    Id65CompiledAuthoringBlankCyclorama compiled)
{
    string sourceHash = Hash(compiled.CopySourceRow80());
    string baselineHash = Hash(compiled.CopyBaselineRow80());
    string outputHash = Hash(compiled.CopyOutputRow80());
    byte[] source = compiled.CopySourceRow80();
    byte[] baseline = compiled.CopyBaselineRow80();
    byte[] output = compiled.CopyOutputRow80();
    source[0] ^= 1;
    baseline[0] ^= 1;
    output[0] ^= 1;
    byte[] before = compiled.OwnedRanges[0].CopyBefore();
    byte[] after = compiled.OwnedRanges[0].CopyAfter();
    before[0] ^= 1;
    after[0] ^= 1;
    Require(
        Hash(compiled.CopySourceRow80()) == sourceHash &&
        Hash(compiled.CopyBaselineRow80()) == baselineHash &&
        Hash(compiled.CopyOutputRow80()) == outputHash &&
        Hash(compiled.OwnedRanges[0].CopyBefore()) ==
            compiled.OwnedRanges[0].BeforeSha256 &&
        Hash(compiled.OwnedRanges[0].CopyAfter()) ==
            compiled.OwnedRanges[0].AfterSha256 &&
        compiled.OwnedRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.InverseRanges is
            ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.ActiveTerrainUnitIds is ReadOnlyCollection<string> &&
        compiled.BaselineLayout.Parts is
            ReadOnlyCollection<Spyro1SkyPartLayout> &&
        compiled.OutputLayout.Parts is
            ReadOnlyCollection<Spyro1SkyPartLayout>,
        "The blank-cyclorama result leaked mutable bytes or collections.");
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
