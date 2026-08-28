using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const int Row80ByteLength =
    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Row80ByteLength;
const int T21RowOffset =
    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ResidentT21RowOffset;
const int T21CoordinateOffset =
    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ResidentT21CoordinateOffset;
const int T21CoordinateLength =
    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ResidentT21CoordinateByteLength;

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
    "Missing exact locked schema-4 physical compiler fixture: " + lockedImagePath);

FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);

Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
Id65AuthoringCompositeLockedSource lockedSource = boundSource.LockedSource;
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
    textureWitness.CanonicalSha256 ==
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
    grassBundle.CanonicalSha256 ==
        Id65RemotePadGrassT107Catalog.BundleCanonicalSha256,
    "The locked row-80 source, T66 witness, or Grass bundle pin changed.");

byte[] exactSourceT21Row =
    Convert.FromHexString(Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
Require(exactSourceT21Row.Length == Id65RemotePadRedGemT21Catalog.RowByteLength &&
    Hash(exactSourceT21Row) == Id65RemotePadRedGemT21Catalog.SourceRowSha256 &&
    lockedSource.CopyRow80().AsSpan(
            T21RowOffset,
            Id65RemotePadRedGemT21Catalog.RowByteLength)
        .SequenceEqual(exactSourceT21Row),
    "The full locked resident T21 source row does not match the exact catalog row.");

int states = 0;
int redGemStates = 0;
int redGemAdditionalRanges = 0;
int preimageRejections = 0;
int afterimageRejections = 0;
HashSet<string> stateKeys = new(StringComparer.Ordinal);
HashSet<string> documentHashes = new(StringComparer.Ordinal);
HashSet<string> outputHashes = new(StringComparer.Ordinal);
HashSet<string> deterministicHashes = new(StringComparer.Ordinal);
Dictionary<string, Id65CompiledAuthoringRemotePadMobyChoicePhysical> compiledByState =
    new(StringComparer.Ordinal);

foreach (bool terrainOn in new[] { false, true })
{
    foreach (Id65RemotePadMobyKind kind in Enum.GetValues<Id65RemotePadMobyKind>())
    {
        foreach (Id65RemotePadGrassT107Position position in
                 Id65RemotePadGrassT107Catalog.Positions)
        {
            string stateKey =
                (terrainOn ? "terrain-on" : "terrain-off") + "|" +
                Id65RemotePadMobyChoiceCatalog.GetKindId(kind) + "|" +
                position.PositionId;
            Require(stateKeys.Add(stateKey),
                "A schema-4 physical state key was compiled twice.");

            Id65AuthoringEditorDocument document =
                Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                    kind,
                    position.PositionId,
                    includeOptionalTerrain: terrainOn);
            Id65CompiledAuthoringRemotePadMobyChoicePhysical first =
                Compile(document);
            Id65CompiledAuthoringRemotePadMobyChoicePhysical repeat =
                Compile(Id65AuthoringEditorDocument.Parse(
                    document.SerializeUtf8()));

            bool surrogateGrass = kind == Id65RemotePadMobyKind.GrassT107;
            Id65AuthoringEditorDocument surrogate =
                Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                    position.PositionId,
                    includeOptionalTerrain: terrainOn,
                    placementIds: surrogateGrass
                        ? [Id65RemotePadGrassT107Catalog.PlacementId]
                        : [],
                    mandatoryBaseFaceTextureId: 66);
            Id65CompiledAuthoringRemotePadComposite directBaseline =
                Id65AuthoringRemotePadCompositeCompiler.Compile(new(
                    surrogate,
                    lockedSource,
                    textureWitness,
                    grassBundle));

            byte[] source = first.CopySourceRow80();
            byte[] output = first.CopyOutputRow80();
            byte[] repeatOutput = repeat.CopyOutputRow80();
            byte[] baselineOutput = directBaseline.CopyOutputRow80();
            int expectedAdditional = kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0;
            int expectedBaselineRanges = surrogateGrass
                ? Id65AuthoringRemotePadCompositeCompiler.ExpectedOwnedRangeCount
                : Id65AuthoringRemotePadCompositeCompiler
                    .ExpectedOwnedRangeCountWithoutT107;
            string expectedModelSha256 = terrainOn
                ? Id65AuthoringRemotePadCompositeCompiler.ExpectedOutputModelSha256
                : Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256;

            Require(
                first.ProfileId ==
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ProfileId &&
                first.SchemaVersion ==
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.SchemaVersion &&
                first.InputDocumentSchemaVersion ==
                    Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion &&
                first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
                first.Document.SchemaVersion == 4 &&
                first.MobyKind == kind &&
                first.StructuralTerrainPresent == terrainOn &&
                first.Schema3SurrogateGrassT107Present == surrogateGrass &&
                first.Schema3SurrogateCanonicalSha256 == surrogate.CanonicalSha256 &&
                first.CatalogCanonicalSha256 ==
                    Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 &&
                first.SourceRow80Sha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
                first.OutputModelSha256 == expectedModelSha256 &&
                first.OutputModelSha256 == directBaseline.OutputModelSha256 &&
                first.BaselineOutputRow80Sha256 ==
                    directBaseline.OutputRow80Sha256 &&
                first.BaselineOutputModelSha256 ==
                    directBaseline.OutputModelSha256 &&
                first.BaselineOwnedRangeMapSha256 ==
                    directBaseline.OwnedRangeMapSha256 &&
                first.BaselineTransactionSha256 ==
                    directBaseline.TransactionSha256 &&
                first.BaselineDeterministicPlanSha256 ==
                    directBaseline.DeterministicPlanSha256 &&
                first.BaselineOwnedRangeCount == expectedBaselineRanges &&
                first.OwnedRanges.Count == expectedBaselineRanges + expectedAdditional &&
                first.InverseRanges.Count == expectedBaselineRanges + expectedAdditional &&
                first.AdditionalOwnedRanges.Count == expectedAdditional &&
                first.TargetOverlaySha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
                first.GlobalExecutableSha256 ==
                    Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
                first.SourceT21RowSha256 ==
                    Id65RemotePadRedGemT21Catalog.SourceRowSha256 &&
                first.OutputRow80Sha256 == repeat.OutputRow80Sha256 &&
                first.OutputModelSha256 == repeat.OutputModelSha256 &&
                first.OwnedRangeMapSha256 == repeat.OwnedRangeMapSha256 &&
                first.TransactionSha256 == repeat.TransactionSha256 &&
                first.ProtectedComplementSha256 ==
                    repeat.ProtectedComplementSha256 &&
                first.DeterministicPlanSha256 ==
                    repeat.DeterministicPlanSha256 &&
                output.SequenceEqual(repeatOutput),
                "Schema-4 state did not compile deterministically from its exact schema-3 surrogate: " + stateKey);

            Require(
                first.PureInMemory &&
                first.BuildsFromLockedSourceThroughFrozenSchema3Baseline &&
                first.ExactInverseVerified &&
                first.EveryChangedByteOwned &&
                first.ProtectedComplementVerified &&
                first.OutputModelPreservedFromFrozenSchema3Baseline &&
                first.ContainsOutputRow80Bytes &&
                !first.AcceptsCallerCompiledAfterimages &&
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
                "Schema-4 physical compiler opened a materializer/Create BIN/runtime/release boundary: " + stateKey);

            VerifyPlacementReadback(first, kind, position);
            VerifyBaselineRangesPreserved(first, directBaseline, expectedAdditional);
            VerifyOrderedNonoverlapAndInverse(first);
            VerifyEveryChangedByteIsOwned(source, output, first.OwnedRanges);
            Require(
                HashProtectedComplement(source, first.OwnedRanges) ==
                    first.ProtectedComplementSha256 &&
                HashProtectedComplement(output, first.OwnedRanges) ==
                    first.ProtectedComplementSha256,
                "The protected complement was not preserved: " + stateKey);

            byte[] applied =
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ApplyTransactional(
                    first,
                    source.ToArray(),
                    reverse: false);
            byte[] rolledBack =
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ApplyTransactional(
                    first,
                    applied,
                    reverse: true);
            Require(applied.SequenceEqual(output) && rolledBack.SequenceEqual(source),
                "Exact schema-4 forward/inverse application failed: " + stateKey);

            byte[] forgedPreimage = source.ToArray();
            forgedPreimage[0x400] ^= 1;
            Reject(
                stateKey + " forged forward preimage",
                () => Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ApplyTransactional(first, forgedPreimage, reverse: false));
            preimageRejections++;
            byte[] forgedAfterimage = output.ToArray();
            forgedAfterimage[0x400] ^= 1;
            Reject(
                stateKey + " forged rollback afterimage",
                () => Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ApplyTransactional(first, forgedAfterimage, reverse: true));
            afterimageRejections++;

            VerifyDefensiveCopiesAndReadOnlyCollections(first);

            if (kind == Id65RemotePadMobyKind.RedGemT21)
            {
                redGemStates++;
                redGemAdditionalRanges += first.AdditionalOwnedRanges.Count;
                byte[] exactOutputRow =
                    Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
                        position.PositionId);
                Id65AuthoringCompositeOwnedRange redGemRange =
                    first.AdditionalOwnedRanges.Single();
                Require(
                    !surrogateGrass &&
                    directBaseline.OwnedRanges.All(item =>
                        item.StableId !=
                            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                                .RedGemOwnedRangeStableId) &&
                    redGemRange.StableId ==
                        Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                            .RedGemOwnedRangeStableId &&
                    redGemRange.OwnerId ==
                        Id65RemotePadRedGemT21Catalog.PlacementId &&
                    redGemRange.DataRelativeOffset == T21CoordinateOffset &&
                    redGemRange.ByteLength == T21CoordinateLength &&
                    redGemRange.CopyBefore().SequenceEqual(
                        exactSourceT21Row.AsSpan(
                            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                            T21CoordinateLength).ToArray()) &&
                    redGemRange.CopyAfter().SequenceEqual(
                        exactOutputRow.AsSpan(
                            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                            T21CoordinateLength).ToArray()) &&
                    output.AsSpan(
                            T21RowOffset,
                            Id65RemotePadRedGemT21Catalog.RowByteLength)
                        .SequenceEqual(exactOutputRow) &&
                    first.OutputT21RowSha256 == positionForGem(position).OutputRowSha256 &&
                    EqualOutsideSpan(
                        baselineOutput,
                        output,
                        T21CoordinateOffset,
                        T21CoordinateLength),
                    "Red Gem did not add exactly one resident-T21 XYZ range: " + stateKey);
            }
            else
            {
                Require(
                    first.AdditionalOwnedRanges.Count == 0 &&
                    output.SequenceEqual(baselineOutput) &&
                    output.AsSpan(
                            T21RowOffset,
                            Id65RemotePadRedGemT21Catalog.RowByteLength)
                        .SequenceEqual(exactSourceT21Row) &&
                    first.OutputT21RowSha256 ==
                        Id65RemotePadRedGemT21Catalog.SourceRowSha256,
                    "None/Grass changed the frozen schema-3 output or resident T21 row: " + stateKey);
            }

            Require(documentHashes.Add(first.DocumentCanonicalSha256),
                "Two schema-4 states shared a canonical document hash.");
            outputHashes.Add(first.OutputRow80Sha256);
            Require(deterministicHashes.Add(first.DeterministicPlanSha256),
                "Two schema-4 states shared a deterministic plan hash.");
            compiledByState.Add(stateKey, first);
            states++;
        }
    }
}

Require(
    states == 54 &&
    stateKeys.Count == 54 &&
    documentHashes.Count == 54 &&
    deterministicHashes.Count == 54 &&
    outputHashes.Count == 38 &&
    redGemStates == 18 &&
    redGemAdditionalRanges == 18 &&
    preimageRejections == 54 &&
    afterimageRejections == 54,
    "The complete schema-4 physical state matrix changed from 54 deterministic states / 38 output rows / 18 Red Gem XYZ ranges.");

Id65CompiledAuthoringRemotePadMobyChoicePhysical firstState =
    compiledByState.Values.First();
Id65CompiledAuthoringRemotePadMobyChoicePhysical lastState =
    compiledByState.Values.Last();
Reject(
    "cross-state rollback afterimage",
    () => Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ApplyTransactional(
        firstState,
        lastState.CopyOutputRow80(),
        reverse: true));

string center = Id65RemotePadMobyChoiceCatalog.DefaultPositionId;
RejectDocument(
    "legacy schema-1 document",
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 66));
RejectDocument(
    "legacy schema-2 document",
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds:
        [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
        mandatoryBaseFaceTextureId: 25));
RejectDocument(
    "schema-3 document",
    Id65AuthoringEditorDocument.CreateRemotePadGridExact(
        center,
        includeOptionalTerrain: true,
        placementIds: [],
        mandatoryBaseFaceTextureId: 66));
RejectDocument(
    "schema-5 terrain-set document",
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.RedGemT21,
        center,
        [
            Id65AuthoringTerrainSetCatalog.Tile1Id,
            Id65AuthoringTerrainSetCatalog.Tile2Id
        ]));
int schema5DocumentRejections = 1;

Id65AuthoringEditorDocument forgedDocument =
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
        Id65RemotePadMobyKind.RedGemT21,
        center,
        includeOptionalTerrain: true);
FieldInfo canonicalField = typeof(Id65AuthoringEditorDocument).GetField(
    "_canonicalUtf8",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException(
        "Missing canonical document field for schema-4 forged-identity smoke.");
byte[] forgedCanonical = (byte[])(canonicalField.GetValue(forgedDocument)
    ?? throw new InvalidOperationException(
        "Missing canonical schema-4 document bytes."));
forgedCanonical[0] ^= 1;
RejectDocument("forged schema-4 canonical identity", forgedDocument);

Id65AuthoringEditorDocument validCenter =
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
        Id65RemotePadMobyKind.RedGemT21,
        center,
        includeOptionalTerrain: true);
byte[] forgedRow80 = lockedSource.CopyRow80();
forgedRow80[0x400] ^= 1;
Id65AuthoringCompositeLockedSource forgedSource = new(
    forgedRow80,
    lockedSource.CopyTargetOverlay(),
    lockedSource.CopyGlobalExecutable());
Reject(
    "forged locked source",
    () => Compile(validCenter, source: forgedSource));

Id65V2NativeTextureWitness forgedWitness = new(
    "forged.texture-witness",
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
    textureWitness.CanonicalSha256);
Reject(
    "forged T66 witness",
    () => Compile(validCenter, texture: forgedWitness));

Id65MobyDependencyBundleDescriptor forgedBundle = new(
    "forged.grass-bundle",
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
    grassBundle.CanonicalSha256);
Reject(
    "forged Grass bundle",
    () => Compile(validCenter, bundle: forgedBundle));
Reject(
    "insufficient model capacity",
    () => Compile(
        validCenter,
        limits: new Id65AuthoringCompositeCompilerLimits(
            ModelByteCapacity: 0x947FF)));
Reject(
    "null compile request",
    () => Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(null!));

FileState lockedAfter = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryAfter =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
Require(lockedBefore == lockedAfter && directoryBefore.SequenceEqual(directoryAfter),
    "The pure in-memory schema-4 physical compiler changed its locked fixture or directory.");

Console.WriteLine(
    "ID65_AUTHORING_REMOTE_PAD_MOBY_CHOICE_PHYSICAL_COMPILER_SMOKE_OK " +
    "states=54 " +
    "terrain_states=2 " +
    "choices=3 " +
    "positions=9 " +
    "output_hashes=" + outputHashes.Count + " " +
    "deterministic_hashes=" + deterministicHashes.Count + " " +
    "red_gem_states=" + redGemStates + " " +
    "red_gem_xyz_ranges=" + redGemAdditionalRanges + " " +
    "preimage_rejections=" + preimageRejections + " " +
    "afterimage_rejections=" + afterimageRejections + " " +
    "schema5_rejections=" + schema5DocumentRejections + " " +
    "source_t21=" + Id65RemotePadRedGemT21Catalog.SourceRowSha256 + " " +
    "row_offset=0x" + T21RowOffset.ToString("X", CultureInfo.InvariantCulture) + " " +
    "xyz_offset=0x" + T21CoordinateOffset.ToString("X", CultureInfo.InvariantCulture) + " " +
    "xyz_length=12 " +
    "schema3_unchanged=true " +
    "protected_complement=true " +
    "model_preserved=true " +
    "filesystem_writes=false " +
    "materializer=false " +
    "app=false " +
    "create_bin=false " +
    "runtime=false " +
    "release=false");

static Id65RemotePadRedGemT21Position positionForGem(
    Id65RemotePadGrassT107Position position) =>
    Id65RemotePadRedGemT21Catalog.GetRequiredPosition(position.PositionId);

Id65CompiledAuthoringRemotePadMobyChoicePhysical Compile(
    Id65AuthoringEditorDocument document,
    Id65AuthoringCompositeLockedSource? source = null,
    Id65V2NativeTextureWitness? texture = null,
    Id65MobyDependencyBundleDescriptor? bundle = null,
    Id65AuthoringCompositeCompilerLimits? limits = null) =>
    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(new(
        document,
        source ?? lockedSource,
        texture ?? textureWitness,
        bundle ?? grassBundle,
        limits));

void RejectDocument(
    string label,
    Id65AuthoringEditorDocument document) =>
    Reject(label, () => Compile(document));

static void VerifyPlacementReadback(
    Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled,
    Id65RemotePadMobyKind kind,
    Id65RemotePadGrassT107Position position)
{
    Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback readback =
        compiled.Placement;
    bool common =
        readback.Kind == kind &&
        readback.PositionId == position.PositionId &&
        readback.PositionOrdinal == position.Ordinal &&
        readback.EditorX == position.EditorX &&
        readback.EditorY == position.EditorY &&
        readback.EditorZ == position.EditorZ &&
        readback.RawX == position.RawX &&
        readback.RawY == position.RawY &&
        readback.RawZ == position.RawZ &&
        readback.SupportHandle == Id65RemotePadGrassT107Catalog.SupportHandle &&
        readback.SupportCollisionTrueIndex ==
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex;
    bool exactChoice = kind switch
    {
        Id65RemotePadMobyKind.None =>
            !readback.Present &&
            !readback.ReusesResidentRow &&
            readback.TargetTrueIndex is null &&
            readback.ActorId is null &&
            readback.YawByte is null &&
            readback.ExactRowSha256 is null,
        Id65RemotePadMobyKind.GrassT107 =>
            readback.Present &&
            !readback.ReusesResidentRow &&
            readback.TargetTrueIndex == Id65RemotePadGrassT107Catalog.TargetTrueIndex &&
            readback.ActorId == Id65RemotePadGrassT107Catalog.ActorId &&
            readback.YawByte == Id65RemotePadGrassT107Catalog.YawByte &&
            readback.ExactRowSha256 == position.RowSha256,
        Id65RemotePadMobyKind.RedGemT21 =>
            readback.Present &&
            readback.ReusesResidentRow &&
            readback.TargetTrueIndex == Id65RemotePadRedGemT21Catalog.TargetTrueIndex &&
            readback.ActorId == Id65RemotePadRedGemT21Catalog.ActorClass &&
            readback.YawByte is null &&
            readback.ExactRowSha256 ==
                Id65RemotePadRedGemT21Catalog
                    .GetRequiredPosition(position.PositionId).OutputRowSha256,
        _ => false
    };
    Require(common && exactChoice,
        "The schema-4 physical placement readback changed for " + kind + "/" + position.PositionId + ".");
}

static void VerifyBaselineRangesPreserved(
    Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled,
    Id65CompiledAuthoringRemotePadComposite baseline,
    int expectedAdditional)
{
    Require(compiled.OwnedRanges.Count == baseline.OwnedRanges.Count + expectedAdditional,
        "The schema-4 ledger count does not preserve the schema-3 baseline.");
    Dictionary<string, Id65AuthoringCompositeOwnedRange> finalById =
        compiled.OwnedRanges.ToDictionary(item => item.StableId, StringComparer.Ordinal);
    foreach (Id65AuthoringCompositeOwnedRange expected in baseline.OwnedRanges)
    {
        Require(finalById.TryGetValue(
                expected.StableId,
                out Id65AuthoringCompositeOwnedRange? actual) &&
            actual.OwnerId == expected.OwnerId &&
            actual.DataRelativeOffset == expected.DataRelativeOffset &&
            actual.ByteLength == expected.ByteLength &&
            actual.BeforeSha256 == expected.BeforeSha256 &&
            actual.AfterSha256 == expected.AfterSha256 &&
            actual.CopyBefore().SequenceEqual(expected.CopyBefore()) &&
            actual.CopyAfter().SequenceEqual(expected.CopyAfter()),
            "A frozen schema-3 ownership range changed in the schema-4 compiler: " + expected.StableId + ".");
    }
}

static void VerifyOrderedNonoverlapAndInverse(
    Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled)
{
    for (int index = 0; index < compiled.OwnedRanges.Count; index++)
    {
        Id65AuthoringCompositeOwnedRange forward = compiled.OwnedRanges[index];
        Id65AuthoringCompositeOwnedRange inverse =
            compiled.InverseRanges[compiled.InverseRanges.Count - 1 - index];
        Require(
            forward.DataRelativeOffset >= 0 &&
            forward.ByteLength > 0 &&
            forward.DataRelativeOffset + (long)forward.ByteLength <= Row80ByteLength &&
            (index == 0 ||
                compiled.OwnedRanges[index - 1].DataRelativeOffset +
                    compiled.OwnedRanges[index - 1].ByteLength <=
                        forward.DataRelativeOffset),
            "Schema-4 owned ranges are out of bounds, unordered, or overlapping.");
        Require(
            inverse.StableId == forward.StableId &&
            inverse.OwnerId == forward.OwnerId &&
            inverse.DataRelativeOffset == forward.DataRelativeOffset &&
            inverse.ByteLength == forward.ByteLength &&
            inverse.BeforeSha256 == forward.AfterSha256 &&
            inverse.AfterSha256 == forward.BeforeSha256 &&
            inverse.CopyBefore().SequenceEqual(forward.CopyAfter()) &&
            inverse.CopyAfter().SequenceEqual(forward.CopyBefore()),
            "The schema-4 inverse ownership range is not exact.");
    }
}

static void VerifyEveryChangedByteIsOwned(
    byte[] source,
    byte[] output,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
{
    Require(source.Length == Row80ByteLength && output.Length == Row80ByteLength,
        "Schema-4 changed-byte ownership input length changed.");
    int rangeIndex = 0;
    for (int offset = 0; offset < source.Length; offset++)
    {
        while (rangeIndex < ranges.Count &&
               ranges[rangeIndex].DataRelativeOffset +
                    ranges[rangeIndex].ByteLength <= offset)
        {
            rangeIndex++;
        }
        bool owned = rangeIndex < ranges.Count &&
            ranges[rangeIndex].DataRelativeOffset <= offset &&
            offset < ranges[rangeIndex].DataRelativeOffset +
                ranges[rangeIndex].ByteLength;
        if (source[offset] != output[offset] && !owned)
        {
            throw new InvalidOperationException(
                "Schema-4 changed byte 0x" +
                offset.ToString("X", CultureInfo.InvariantCulture) +
                " escaped ownership.");
        }
    }
}

static void VerifyDefensiveCopiesAndReadOnlyCollections(
    Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled)
{
    byte[] output = compiled.CopyOutputRow80();
    output[0] ^= 1;
    byte[] source = compiled.CopySourceRow80();
    source[0] ^= 1;
    Id65AuthoringCompositeOwnedRange firstRange = compiled.OwnedRanges[0];
    byte[] before = firstRange.CopyBefore();
    byte[] after = firstRange.CopyAfter();
    before[0] ^= 1;
    after[0] ^= 1;
    Require(
        Hash(compiled.CopyOutputRow80()) == compiled.OutputRow80Sha256 &&
        Hash(compiled.CopySourceRow80()) == compiled.SourceRow80Sha256 &&
        Hash(firstRange.CopyBefore()) == firstRange.BeforeSha256 &&
        Hash(firstRange.CopyAfter()) == firstRange.AfterSha256 &&
        compiled.OwnedRanges is ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.InverseRanges is ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> &&
        compiled.AdditionalOwnedRanges is ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>,
        "Schema-4 result leaked mutable bytes or collections.");
    RejectReadOnlyMutation(
        "owned ranges",
        (IList<Id65AuthoringCompositeOwnedRange>)compiled.OwnedRanges,
        firstRange);
    RejectReadOnlyMutation(
        "inverse ranges",
        (IList<Id65AuthoringCompositeOwnedRange>)compiled.InverseRanges,
        compiled.InverseRanges[0]);
    RejectReadOnlyMutation(
        "additional ranges",
        (IList<Id65AuthoringCompositeOwnedRange>)compiled.AdditionalOwnedRanges,
        firstRange);
}

static void RejectReadOnlyMutation(
    string label,
    IList<Id65AuthoringCompositeOwnedRange> list,
    Id65AuthoringCompositeOwnedRange item)
{
    try
    {
        list.Add(item);
    }
    catch (NotSupportedException)
    {
        return;
    }
    throw new InvalidOperationException(
        "Expected immutable schema-4 " + label + ".");
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
        if (cursor < range.DataRelativeOffset)
        {
            hash.AppendData(bytes.AsSpan(
                cursor,
                range.DataRelativeOffset - cursor));
        }
        cursor = checked(range.DataRelativeOffset + range.ByteLength);
    }
    if (cursor < bytes.Length)
        hash.AppendData(bytes.AsSpan(cursor));
    return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
}

static bool EqualOutsideSpan(
    byte[] left,
    byte[] right,
    int offset,
    int length) =>
    left.Length == right.Length &&
    offset >= 0 &&
    length >= 0 &&
    offset + (long)length <= left.Length &&
    left.AsSpan(0, offset).SequenceEqual(right.AsSpan(0, offset)) &&
    left.AsSpan(offset + length).SequenceEqual(
        right.AsSpan(offset + length));

static void Reject(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or
        ArgumentException or
        KeyNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException(
        "Expected fail-closed rejection: " + label + ".");
}

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
    {
        string full = Path.GetFullPath(explicitRoot);
        if (Directory.Exists(Path.Combine(full, ".git")))
            return full;
        throw new DirectoryNotFoundException(
            "Explicit repository root is invalid: " + full);
    }

    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    return new(
        file.Length,
        file.LastWriteTimeUtc.Ticks,
        Hash(File.ReadAllBytes(path)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
