using System.Collections.ObjectModel;
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
    "Missing exact schema-6 aggregate-plan locked fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringBlankCycloramaAggregatePlanProjectorSmoke-" +
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
    Require(
        nativeInputs.CanonicalInputSetSha256 ==
            Id65AuthoringEditorNativeInputProvider
                .ExpectedCanonicalInputSetSha256 &&
        nativeInputs.LockedSource.Row80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        v3Template.DeterministicPlanSha256 ==
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                .ExpectedDeterministicPlanSha256 &&
        v3Template.SourceData.SequenceEqual(
            nativeInputs.LockedSource.CopyRow80()),
        "The schema-6 aggregate provider generation or v3 plan changed.");

    string[][] terrainStates =
    [
        [],
        [Id65AuthoringTerrainSetCatalog.Tile1Id],
        [Id65AuthoringTerrainSetCatalog.Tile2Id],
        [Id65AuthoringTerrainSetCatalog.Tile1Id,
         Id65AuthoringTerrainSetCatalog.Tile2Id]
    ];
    List<StateInput> inputs = [];
    string? expectedPriorEnvelope = null;
    int storeReadbacks = 0;
    foreach (string[] terrainIds in terrainStates)
    {
        foreach (Id65RemotePadMobyChoice choice in
                 Id65RemotePadMobyChoiceCatalog.Choices)
        {
            foreach (Id65RemotePadGrassT107Position position in
                     Id65RemotePadGrassT107Catalog.Positions)
            {
                string stateKey =
                    (terrainIds.Length == 0
                        ? "zero"
                        : string.Join('+', terrainIds)) +
                    "|" + choice.KindId + "|" + position.PositionId;
                Id65AuthoringEditorDocument document =
                    Id65AuthoringEditorDocument
                        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                            choice.Kind,
                            position.PositionId,
                            terrainIds);
                Id65AuthoringEditorDocumentStoreResult saved =
                    await Id65AuthoringEditorDocumentStore.SaveAsync(
                        paths,
                        manifest,
                        document,
                        new Id65AuthoringEditorDocumentProjectionInputs(null),
                        expectedPriorEnvelope);
                expectedPriorEnvelope = saved.EnvelopeCanonicalSha256;
                Id65AuthoringEditorDocumentStoreResult loaded =
                    await Id65AuthoringEditorDocumentStore.LoadAsync(
                        paths,
                        manifest,
                        new Id65AuthoringEditorDocumentProjectionInputs(null))
                    ?? throw new InvalidOperationException(
                        "The saved schema-6 document disappeared.");
                Require(StoreEquivalent(saved, loaded),
                    "Schema-6 save/load readback changed for " + stateKey + ".");
                inputs.Add(new(
                    stateKey,
                    terrainIds.ToArray(),
                    choice.Kind,
                    position,
                    document,
                    loaded));
                storeReadbacks++;
            }
        }
    }

    Require(inputs.Count ==
            Id65AuthoringBlankCycloramaAggregatePlanProjector
                .ExpectedStateCount &&
        storeReadbacks == 108,
        "The schema-6 aggregate store matrix cardinality changed.");

    object resultGate = new();
    HashSet<string> stateKeys = new(StringComparer.Ordinal);
    HashSet<string> documentHashes = new(StringComparer.Ordinal);
    HashSet<string> envelopeHashes = new(StringComparer.Ordinal);
    HashSet<string> projectionHashes = new(StringComparer.Ordinal);
    HashSet<string> outputHashes = new(StringComparer.Ordinal);
    HashSet<string> modelHashes = new(StringComparer.Ordinal);
    HashSet<string> cycloramaHashes = new(StringComparer.Ordinal);
    HashSet<string> geometryHashes = new(StringComparer.Ordinal);
    HashSet<string> aggregateHashes = new(StringComparer.Ordinal);
    HashSet<int> cycloramaModelOffsets = [];
    HashSet<int> cycloramaRowOffsets = [];
    HashSet<long> cycloramaWadOffsets = [];
    int states = 0;
    int exactOutputMatches = 0;
    int inverseMatches = 0;
    Id65AuthoringBlankCycloramaAggregatePlan? retainedPlan = null;

    StateInput selectedState = inputs.Single(state =>
        state.TerrainIds.Length == 2 &&
        state.Kind == Id65RemotePadMobyKind.RedGemT21 &&
        state.Position.PositionId ==
            Id65RemotePadGrassT107Catalog.Positions[^1].PositionId);

    Parallel.ForEach(
        inputs,
        new ParallelOptions { MaxDegreeOfParallelism = 4 },
        state =>
        {
            Id65AuthoringBlankCycloramaAggregatePlan plan =
                Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
                    state.Stored,
                    state.Stored.EnvelopeCanonicalSha256,
                    manifest,
                    nativeInputs,
                    v3Template));
            Id65CompiledAuthoringBlankCyclorama physical =
                Id65AuthoringBlankCycloramaCompiler.Compile(new(
                    state.Document,
                    nativeInputs.LockedSource,
                    v3Template,
                    nativeInputs.PrivateTextureWitness,
                    nativeInputs.GrassBundle));
            byte[] source = nativeInputs.LockedSource.CopyRow80();
            byte[] output =
                Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
                    plan,
                    source,
                    reverse: false);
            byte[] restored =
                Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
                    plan,
                    output,
                    reverse: true);
            Require(output.SequenceEqual(physical.CopyOutputRow80()) &&
                    Hash(output) == plan.OutputRow80Sha256 &&
                    plan.CompilerOwnedRangeMapSha256 ==
                        physical.OwnedRangeMapSha256 &&
                    plan.CompilerTransactionSha256 ==
                        physical.TransactionSha256 &&
                    plan.CompilerDeterministicPlanSha256 ==
                        physical.DeterministicPlanSha256,
                "Aggregate output did not exactly match the schema-6 compiler for " +
                state.StateKey + ".");
            Require(restored.SequenceEqual(source),
                "Aggregate inverse did not restore the locked row for " +
                state.StateKey + ".");
            RequirePlan(plan, state, manifest);

            lock (resultGate)
            {
                Require(stateKeys.Add(state.StateKey),
                    "A schema-6 aggregate state key was projected twice.");
                Require(documentHashes.Add(plan.DocumentCanonicalSha256),
                    "Two schema-6 states shared a document identity.");
                Require(envelopeHashes.Add(plan.EnvelopeCanonicalSha256),
                    "Two schema-6 states shared an envelope identity.");
                Require(projectionHashes.Add(
                        plan.StaticAuthoringProjectionSha256),
                    "Two schema-6 states shared a projection identity.");
                outputHashes.Add(plan.OutputRow80Sha256);
                modelHashes.Add(plan.OutputModelSha256);
                cycloramaHashes.Add(plan.OutputCycloramaSha256);
                geometryHashes.Add(plan.GeometrySha256);
                Require(aggregateHashes.Add(
                        plan.DeterministicAggregatePlanSha256),
                    "Two schema-6 states shared an aggregate identity.");
                cycloramaModelOffsets.Add(plan.CycloramaModelOffset);
                cycloramaRowOffsets.Add(plan.CycloramaRow80Offset);
                cycloramaWadOffsets.Add(plan.CycloramaWadOffset);
                if (state.StateKey == selectedState.StateKey)
                    retainedPlan = plan;
            }
            Interlocked.Increment(ref states);
            Interlocked.Increment(ref exactOutputMatches);
            Interlocked.Increment(ref inverseMatches);
        });

    Require(states == 108 && exactOutputMatches == 108 &&
            inverseMatches == 108 && stateKeys.Count == 108 &&
            documentHashes.Count == 108 && envelopeHashes.Count == 108 &&
            projectionHashes.Count == 108 && outputHashes.Count ==
                Id65AuthoringBlankCycloramaAggregatePlanProjector
                    .ExpectedPhysicalOutputHashCount &&
            modelHashes.Count == 4 && cycloramaHashes.Count == 1 &&
            geometryHashes.SetEquals(
                [Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256]) &&
            aggregateHashes.Count == 108 &&
            cycloramaModelOffsets.SetEquals([0x8B9D4, 0x8BA04, 0x8BA30]) &&
            cycloramaRowOffsets.SetEquals([0x16A1D4, 0x16A204, 0x16A230]) &&
            cycloramaWadOffsets.SetEquals(
                [0x6AA09D4L, 0x6AA0A04L, 0x6AA0A30L]),
        "The complete schema-6 aggregate matrix or corrected physical pins changed.");

    Id65AuthoringBlankCycloramaAggregatePlan selectedPlan = retainedPlan
        ?? throw new InvalidOperationException(
            "The schema-6 aggregate matrix did not retain its selected plan.");
    Id65AuthoringBlankCycloramaAggregatePlan DirectConstruct(
        object? constructionAuthority) => new(
            constructionAuthority,
            selectedPlan.Destination,
            selectedPlan.Document,
            selectedPlan.EnvelopeCanonicalSha256,
            selectedPlan.StaticAuthoringProfileId,
            selectedPlan.StaticAuthoringCatalogSha256,
            selectedPlan.StaticAuthoringSceneGraphSha256,
            selectedPlan.StaticAuthoringProjectionSha256,
            selectedPlan.StaticAuthoringTerrainManifestSha256,
            selectedPlan.StaticAuthoringCycloramaCatalogSha256,
            selectedPlan.NativeInputSetSha256,
            selectedPlan.V3DeterministicPlanSha256,
            selectedPlan.SourceRow80Sha256,
            selectedPlan.BaselineRow80Sha256,
            selectedPlan.OutputRow80Sha256,
            selectedPlan.BaselineModelSha256,
            selectedPlan.OutputModelSha256,
            selectedPlan.BaselineDocumentCanonicalSha256,
            selectedPlan.BaselineDeterministicPlanSha256,
            selectedPlan.PresetId,
            selectedPlan.ColorHex,
            selectedPlan.CycloramaModelOffset,
            selectedPlan.CycloramaRow80Offset,
            selectedPlan.OutputCycloramaLayout,
            selectedPlan.BaselineCycloramaSha256,
            selectedPlan.OutputCycloramaSha256,
            selectedPlan.GeometrySha256,
            selectedPlan.NonPaletteSha256,
            selectedPlan.CommandByteSha256,
            selectedPlan.CycloramaChangedRgbByteCount,
            selectedPlan.CompilerOwnedRangeMapSha256,
            selectedPlan.CompilerTransactionSha256,
            selectedPlan.CompilerProtectedComplementSha256,
            selectedPlan.CompilerDeterministicPlanSha256,
            selectedPlan.OwnedRanges,
            selectedPlan.InverseRanges,
            selectedPlan.ProtectedRanges,
            selectedPlan.AtomicGroups,
            selectedPlan.GuardedByteCount,
            selectedPlan.ProtectedByteCount,
            selectedPlan.ChangedByteCount,
            selectedPlan.ProtectedComplementSha256);
    int constructionAuthorityRejects = 0;
    Reject("null aggregate-plan construction authority", () =>
        _ = DirectConstruct(null));
    constructionAuthorityRejects++;
    Reject("forged aggregate-plan construction authority", () =>
        _ = DirectConstruct(new object()));
    constructionAuthorityRejects++;
    int offsetRejects = 0;
    Reject("forged cyclorama model offset", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector
            .ValidatePinnedCycloramaOffsetsForSmoke(
                selectedPlan.ActiveTerrainUnitIds,
                checked(selectedPlan.CycloramaModelOffset + 4),
                selectedPlan.CycloramaRow80Offset,
                selectedPlan.CycloramaWadOffset));
    offsetRejects++;
    Id65AuthoringBlankCycloramaAggregatePlan repeat =
        Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
            selectedState.Stored with
            {
                Path = Path.Combine(
                    Path.GetTempPath(),
                    "ignored-schema6-store-path",
                    Id65AuthoringEditorDocumentStore.FileName),
                Document = Id65AuthoringEditorDocument.Parse(
                    selectedState.Document.SerializeUtf8())
            },
            selectedState.Stored.EnvelopeCanonicalSha256,
            manifest,
            nativeInputs,
            v3Template));
    Require(Equivalent(selectedPlan, repeat),
        "Repeated schema-6 aggregate projection changed.");

    int manifestRejects = 0;
    Reject("shifted object-table manifest offsets", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
            selectedState.Stored,
            selectedState.Stored.EnvelopeCanonicalSha256,
            manifest with
            {
                ObjectTableWadOffset = checked(
                    manifest.ObjectTableWadOffset + 4),
                ObjectTableRelativeOffset = checked(
                    manifest.ObjectTableRelativeOffset + 4)
            },
            nativeInputs,
            v3Template)));
    manifestRejects++;
    Reject("forged locked-base manifest profile", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
            selectedState.Stored,
            selectedState.Stored.EnvelopeCanonicalSha256,
            manifest with
            {
                LockedBaseProfileId = "forged-locked-base-profile"
            },
            nativeInputs,
            v3Template)));
    manifestRejects++;
    Reject("promoted lab manifest", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
            selectedState.Stored,
            selectedState.Stored.EnvelopeCanonicalSha256,
            manifest with { PromotionAuthorized = true },
            nativeInputs,
            v3Template)));
    manifestRejects++;

    int schemaRejects = 0;
    Id65AuthoringEditorDocument[] legacyDocuments =
    [
        Id65AuthoringEditorDocument.CreateExact(
            includeOptionalTerrain: false,
            placementIds:
            [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
            mandatoryBaseFaceTextureId: 66),
        Id65AuthoringEditorDocument.CreateExact(
            includeOptionalTerrain: false,
            placementIds:
            [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId],
            mandatoryBaseFaceTextureId: 25),
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
            includeOptionalTerrain: false,
            placementIds: [],
            mandatoryBaseFaceTextureId: 66),
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
            includeOptionalTerrain: false),
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
            [])
    ];
    foreach (Id65AuthoringEditorDocument legacy in legacyDocuments)
    {
        Reject("schema-" + legacy.SchemaVersion + " document", () =>
            Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
                selectedState.Stored with { Document = legacy },
                selectedState.Stored.EnvelopeCanonicalSha256,
                manifest,
                nativeInputs,
                v3Template)));
        schemaRejects++;
    }

    byte[] selectedSource = nativeInputs.LockedSource.CopyRow80();
    byte[] selectedOutput =
        Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            selectedPlan,
            selectedSource,
            reverse: false);
    byte[] forgedForward = selectedSource.ToArray();
    forgedForward[0x400] ^= 1;
    Reject("forged whole-row forward preimage", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            selectedPlan,
            forgedForward,
            reverse: false));
    byte[] forgedReverse = selectedOutput.ToArray();
    forgedReverse[0x400] ^= 1;
    Reject("forged whole-row rollback afterimage", () =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            selectedPlan,
            forgedReverse,
            reverse: true));

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] exactOwned =
        selectedPlan.OwnedRanges.Select(item => item.DeepCopy()).ToArray();
    int paletteIndex = Array.FindIndex(exactOwned, item => item.Domain ==
        Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette);
    Require(paletteIndex > 0,
        "The retained aggregate did not expose its cyclorama palette group.");
    int partitionRejects = 0;
    void RejectPartition(
        string label,
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] selected)
    {
        Reject(label, () =>
            Id65AuthoringBlankCycloramaAggregatePlanProjector
                .ValidatePartitionForSmoke(
                    selectedSource,
                    selectedOutput,
                    selected,
                    selectedPlan.MobyKind,
                    selectedPlan.OutputCycloramaLayout,
                    selectedPlan.CycloramaRow80Offset,
                    selectedPlan.CommandByteSha256));
        partitionRejects++;
    }

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badStable =
        CopyOwned(exactOwned);
    badStable[paletteIndex] = CloneRange(
        badStable[paletteIndex],
        stableId: "schema6.cyclorama.palette.forged");
    RejectPartition("forged palette stable ID", badStable);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badOwner =
        CopyOwned(exactOwned);
    badOwner[paletteIndex] = CloneRange(
        badOwner[paletteIndex],
        ownerId: "schema6.cyclorama.forged");
    RejectPartition("forged palette owner", badOwner);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badDomain =
        CopyOwned(exactOwned);
    badDomain[paletteIndex] = CloneRange(
        badDomain[paletteIndex],
        domain: Id65AuthoringBlankCycloramaAggregateRangeDomain.TerrainSetModel);
    RejectPartition("forged palette domain", badDomain);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badAddress =
        CopyOwned(exactOwned);
    badAddress[paletteIndex] = CloneRange(
        badAddress[paletteIndex],
        dataRelativeOffset: badAddress[paletteIndex].DataRelativeOffset + 1);
    RejectPartition("forged palette address", badAddress);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badLength =
        CopyOwned(exactOwned);
    badLength[paletteIndex] = CloneRange(
        badLength[paletteIndex],
        before: badLength[paletteIndex].CopyBefore()[..3],
        after: badLength[paletteIndex].CopyAfter()[..3]);
    RejectPartition("forged palette length", badLength);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badRgb =
        CopyOwned(exactOwned);
    byte[] forgedRgb = badRgb[paletteIndex].CopyAfter();
    forgedRgb[0] ^= 1;
    badRgb[paletteIndex] = CloneRange(
        badRgb[paletteIndex],
        after: forgedRgb);
    RejectPartition("forged palette RGB", badRgb);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] badCommand =
        CopyOwned(exactOwned);
    byte[] forgedCommand = badCommand[paletteIndex].CopyAfter();
    forgedCommand[3] ^= 1;
    badCommand[paletteIndex] = CloneRange(
        badCommand[paletteIndex],
        after: forgedCommand);
    RejectPartition("forged palette command", badCommand);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] overlap =
        CopyOwned(exactOwned);
    overlap[paletteIndex] = CloneRange(
        overlap[paletteIndex],
        dataRelativeOffset: exactOwned[paletteIndex - 1].DataRelativeOffset);
    RejectPartition("overlapping palette leaf", overlap);

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] gap = exactOwned
        .Where((_, index) => index != paletteIndex)
        .Select(item => item.DeepCopy())
        .ToArray();
    RejectPartition("missing palette leaf gap", gap);

    Require(schemaRejects == 5 && partitionRejects == 9 &&
            constructionAuthorityRejects == 2 && offsetRejects == 1 &&
            manifestRejects == 3,
        "The schema, construction-authority, offset, manifest, or forged " +
        "palette rejection matrix changed.");
    Require(selectedPlan.OwnedRanges is
            ReadOnlyCollection<Id65AuthoringBlankCycloramaAggregateOwnedRange> &&
        selectedPlan.InverseRanges is
            ReadOnlyCollection<Id65AuthoringBlankCycloramaAggregateOwnedRange> &&
        selectedPlan.ProtectedRanges is
            ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange> &&
        selectedPlan.AtomicGroups is
            ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup> &&
        selectedPlan.ActiveTerrainUnitIds is ReadOnlyCollection<string> &&
        selectedPlan.OutputCycloramaLayout.Parts is
            ReadOnlyCollection<Spyro1SkyPartLayout>,
        "The schema-6 aggregate leaked mutable collections.");

    Console.WriteLine(
        "ID65_AUTHORING_BLANK_CYCLORAMA_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
        "states=108 terrain_states=4 choices=3 positions=9 " +
        "store_readbacks=108 exact_output_matches=108 inverse_matches=108 " +
        "output_hashes=76 model_hashes=4 cyclorama_hashes=1 " +
        "aggregate_hashes=108 palette_leaves=727 atomic_groups=5 " +
        "schema1_5_rejections=5 construction_authority_rejects=2 " +
        "offset_rejects=1 manifest_rejects=3 partition_rejects=9 " +
        "two_unit_model_offset=0x8BA30 two_unit_row_offset=0x16A230 " +
        "two_unit_wad_offset=0x6AA0A30 wad80_only=true " +
        "wad79_protected=true town_square_wad15_16_protected=true " +
        "artisans_wad9_10_protected=true executable_protected=true " +
        "linked_copies=false native_sky_exporter=false filesystem=false " +
        "materializer=false create_bin=false runtime=false release=false");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore,
    "The schema-6 aggregate smoke changed its locked fixture.");

static void RequirePlan(
    Id65AuthoringBlankCycloramaAggregatePlan plan,
    StateInput state,
    UnusedLevel65BlankLevelLabManifest manifest)
{
    int expectedModelOffset = state.TerrainIds.Length switch
    {
        0 => 0x8B9D4,
        1 => 0x8BA04,
        2 => 0x8BA30,
        _ => -1
    };
    Require(
        plan.ProfileId ==
            Id65AuthoringBlankCycloramaAggregatePlanProjector.ProfileId &&
        plan.SchemaVersion ==
            Id65AuthoringBlankCycloramaAggregatePlanProjector.SchemaVersion &&
        plan.DocumentCanonicalSha256 == state.Document.CanonicalSha256 &&
        plan.EnvelopeCanonicalSha256 ==
            state.Stored.EnvelopeCanonicalSha256 &&
        plan.StaticAuthoringProfileId == Id65AuthoringEditorDocumentStore
            .RemotePadMobyChoiceTerrainSetBlankCycloramaStaticAuthoringProfileId &&
        plan.StaticAuthoringProjectionSha256 ==
            state.Stored.StaticAuthoringProjectionSha256 &&
        plan.StaticAuthoringTerrainManifestSha256 ==
            state.Document.TerrainSetManifestSha256 &&
        plan.StaticAuthoringCycloramaCatalogSha256 ==
            Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 &&
        plan.ActiveTerrainUnitIds.SequenceEqual(state.TerrainIds) &&
        plan.MobyKind == state.Kind &&
        plan.MobyPositionId == state.Position.PositionId &&
        plan.PresetId == Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
        plan.ColorHex == Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
        plan.CycloramaModelOffset == expectedModelOffset &&
        plan.CycloramaRow80Offset ==
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                expectedModelOffset &&
        plan.CycloramaWadOffset == 0x6936800L +
            plan.CycloramaRow80Offset &&
        plan.PaletteOwnedRangeCount == 727 &&
        plan.AtomicGroups.Count == 5 &&
        plan.AtomicGroups[4].GroupId == "blank-cyclorama-palette" &&
        plan.AtomicGroups[4].OwnedRangeCount == 727 &&
        plan.GuardedByteCount + plan.ProtectedByteCount == 0x2E2000 &&
        plan.ChangedByteCount == CountChangedBytes(plan.OwnedRanges) &&
        plan.Destination.LockedBaseProfileId ==
            UnusedLevel65BlankLevelLabProfileRegistry.Profile
                .LockedBaseProfileId &&
        plan.Destination.TargetDataWadEntry == 80 &&
        plan.Destination.TargetDataWadOffset == 0x6936800 &&
        plan.Destination.TargetObjectTableWadOffset ==
            UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableWadOffset &&
        plan.Destination.TargetObjectTableRelativeOffset ==
            UnusedLevel65BlankLevelLabProfileRegistry
                .ObjectTableRelativeOffset &&
        plan.Destination.ProtectedId65OverlayWadEntry == 79 &&
        plan.Destination.ProtectedTownSquareOverlayWadEntry == 15 &&
        plan.Destination.ProtectedTownSquareDataWadEntry == 16 &&
        plan.Destination.ProtectedArtisansOverlayWadEntry == 9 &&
        plan.Destination.ProtectedArtisansDataWadEntry == 10 &&
        plan.Destination.ProtectedExecutableLba == 55_382 &&
        plan.Destination.SourceImageSha256 == manifest.LockedBaseImageSha256 &&
        plan.Destination.Wad80SoleWritableDestination &&
        plan.Destination.Wad79Protected &&
        plan.Destination.RetailTownSquareWad15Protected &&
        plan.Destination.RetailTownSquareWad16Protected &&
        plan.Destination.RetailArtisansWad9Protected &&
        plan.Destination.RetailArtisansWad10Protected &&
        plan.Destination.ExecutableProtected &&
        plan.Destination.LinkedPrimarySkyCopiesNotFollowed &&
        plan.Destination.NativeSkyPatchExporterNotUsed &&
        plan.PureInMemory && plan.FullExactSchema6DocumentRequired &&
        plan.CompilesBlankCycloramaFromLockedInputs &&
        plan.EveryPaletteWordOwnedExactlyOnce &&
        plan.PaletteCommandBytesPreserved &&
        plan.CompleteRowOwnershipPartitionVerified &&
        plan.ExactWholeRowInverseVerified &&
        plan.Wad80SoleWritableDestination && plan.Wad79Protected &&
        plan.RetailTownSquareWad15And16Protected &&
        plan.RetailArtisansWad9And10Protected &&
        plan.ExecutableProtected && !plan.CallsNativeSkyPatchExporter &&
        !plan.LinkedPrimarySkyCopiesTouched &&
        !plan.GenericExporterCompatible &&
        !plan.PartialRangeApplicationAuthorized &&
        !plan.WritesFileSystem && !plan.WritesDiscImage &&
        !plan.MaterializerIntegrated && !plan.CreateBinEnabled &&
        !plan.RuntimeCandidateAuthorized && !plan.ReleaseIntegrated &&
        plan.RequiresTrustedExclusiveWorkspaceDirectory,
        "Schema-6 aggregate identity, destination, or authority changed for " +
        state.StateKey + ".");
}

static bool StoreEquivalent(
    Id65AuthoringEditorDocumentStoreResult left,
    Id65AuthoringEditorDocumentStoreResult right) =>
    left.Path == right.Path &&
    left.Document.SerializeUtf8().SequenceEqual(right.Document.SerializeUtf8()) &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.ProjectionContractId == right.ProjectionContractId &&
    left.StaticAuthoringProfileId == right.StaticAuthoringProfileId &&
    left.StaticAuthoringCatalogSha256 == right.StaticAuthoringCatalogSha256 &&
    left.StaticAuthoringSceneGraphSha256 ==
        right.StaticAuthoringSceneGraphSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.StaticAuthoringTerrainManifestSha256 ==
        right.StaticAuthoringTerrainManifestSha256 &&
    left.StaticAuthoringCycloramaCatalogSha256 ==
        right.StaticAuthoringCycloramaCatalogSha256;

static bool Equivalent(
    Id65AuthoringBlankCycloramaAggregatePlan left,
    Id65AuthoringBlankCycloramaAggregatePlan right) =>
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.OutputCycloramaSha256 == right.OutputCycloramaSha256 &&
    left.CompilerDeterministicPlanSha256 ==
        right.CompilerDeterministicPlanSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.InverseRangeMapSha256 == right.InverseRangeMapSha256 &&
    left.ProtectedRangeMapSha256 == right.ProtectedRangeMapSha256 &&
    left.AtomicGroupMapSha256 == right.AtomicGroupMapSha256 &&
    left.DeterministicAggregatePlanSha256 ==
        right.DeterministicAggregatePlanSha256 &&
    left.Destination == right.Destination &&
    left.ProtectedRanges.SequenceEqual(right.ProtectedRanges) &&
    left.AtomicGroups.SequenceEqual(right.AtomicGroups) &&
    left.OwnedRanges.Select(ToRangeTuple)
        .SequenceEqual(right.OwnedRanges.Select(ToRangeTuple)) &&
    left.InverseRanges.Select(ToRangeTuple)
        .SequenceEqual(right.InverseRanges.Select(ToRangeTuple));

static object ToRangeTuple(
    Id65AuthoringBlankCycloramaAggregateOwnedRange item) => new
{
    item.StableId,
    item.OwnerId,
    item.Domain,
    item.DataRelativeOffset,
    item.ByteLength,
    item.BeforeSha256,
    item.AfterSha256
};

static int CountChangedBytes(
    IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges) =>
    ranges.Sum(range => range.CopyBefore()
        .Zip(range.CopyAfter())
        .Count(pair => pair.First != pair.Second));

static Id65AuthoringBlankCycloramaAggregateOwnedRange[] CopyOwned(
    IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges) =>
    ranges.Select(item => item.DeepCopy()).ToArray();

static Id65AuthoringBlankCycloramaAggregateOwnedRange CloneRange(
    Id65AuthoringBlankCycloramaAggregateOwnedRange source,
    string? stableId = null,
    string? ownerId = null,
    Id65AuthoringBlankCycloramaAggregateRangeDomain? domain = null,
    int? dataRelativeOffset = null,
    byte[]? before = null,
    byte[]? after = null) => new(
        stableId ?? source.StableId,
        ownerId ?? source.OwnerId,
        domain ?? source.Domain,
        dataRelativeOffset ?? source.DataRelativeOffset,
        before ?? source.CopyBefore(),
        after ?? source.CopyAfter());

static void Reject(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (
        exception is InvalidDataException or ArgumentException or
        NotSupportedException or KeyNotFoundException)
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

internal sealed record StateInput(
    string StateKey,
    string[] TerrainIds,
    Id65RemotePadMobyKind Kind,
    Id65RemotePadGrassT107Position Position,
    Id65AuthoringEditorDocument Document,
    Id65AuthoringEditorDocumentStoreResult Stored);

internal sealed record FileState(
    long Length,
    DateTime LastWriteUtc,
    string Sha256);
