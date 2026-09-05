using System.Reflection;
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
    "Missing exact schema-5 aggregate-plan locked fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringRemotePadTerrainSetAggregatePlanProjectorSmoke-" +
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
        "The schema-5 aggregate provider generation or v3 plan changed.");

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
                        .CreateRemotePadMobyChoiceTerrainSetExact(
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
                        "The saved schema-5 document disappeared.");
                Require(StoreEquivalent(saved, loaded),
                    "Schema-5 save/load readback changed for " + stateKey + ".");
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
            Id65AuthoringRemotePadTerrainSetAggregatePlanProjector
                .ExpectedStateCount &&
        storeReadbacks == 108,
        "The schema-5 aggregate store matrix cardinality changed.");
    StateInput oldestState = inputs[0];
    StateInput lastState = inputs[^1];
    object resultGate = new();
    HashSet<string> stateKeys = new(StringComparer.Ordinal);
    HashSet<string> documentHashes = new(StringComparer.Ordinal);
    HashSet<string> envelopeHashes = new(StringComparer.Ordinal);
    HashSet<string> staticProjectionHashes = new(StringComparer.Ordinal);
    HashSet<string> outputHashes = new(StringComparer.Ordinal);
    HashSet<string> modelHashes = new(StringComparer.Ordinal);
    HashSet<string> aggregateHashes = new(StringComparer.Ordinal);
    int states = 0;
    int exactOutputMatches = 0;
    int inverseMatches = 0;
    Id65AuthoringRemotePadTerrainSetAggregatePlan? retainedPlan = null;

    Parallel.ForEach(
        inputs,
        new ParallelOptions { MaxDegreeOfParallelism = 4 },
        state =>
        {
            Id65AuthoringRemotePadTerrainSetAggregatePlan plan =
                Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Project(new(
                    state.Stored,
                    state.Stored.EnvelopeCanonicalSha256,
                    manifest,
                    nativeInputs,
                    v3Template));
            Id65CompiledAuthoringRemotePadTerrainSetPhysical physical =
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
                    state.Document,
                    nativeInputs.LockedSource,
                    v3Template,
                    nativeInputs.PrivateTextureWitness,
                    nativeInputs.GrassBundle));
            byte[] source = nativeInputs.LockedSource.CopyRow80();
            byte[] output =
                Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ApplyExact(
                    plan,
                    source,
                    reverse: false);
            byte[] restored =
                Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ApplyExact(
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
                "Aggregate output did not exactly match the schema-5 physical compiler for " +
                state.StateKey + ".");
            Require(restored.SequenceEqual(source),
                "Aggregate inverse did not restore the locked row for " +
                state.StateKey + ".");
            RequirePlan(plan, state, manifest);

            lock (resultGate)
            {
                Require(stateKeys.Add(state.StateKey),
                    "A schema-5 aggregate state key was projected twice.");
                Require(documentHashes.Add(plan.DocumentCanonicalSha256),
                    "Two schema-5 states shared a document identity.");
                Require(envelopeHashes.Add(plan.EnvelopeCanonicalSha256),
                    "Two schema-5 states shared an envelope identity.");
                Require(staticProjectionHashes.Add(
                        plan.StaticAuthoringProjectionSha256),
                    "Two schema-5 states shared a static projection identity.");
                outputHashes.Add(plan.OutputRow80Sha256);
                modelHashes.Add(plan.OutputModelSha256);
                Require(aggregateHashes.Add(
                        plan.DeterministicAggregatePlanSha256),
                    "Two schema-5 states shared an aggregate identity.");
                if (state.StateKey == lastState.StateKey)
                    retainedPlan = plan;
            }
            Interlocked.Increment(ref states);
            Interlocked.Increment(ref exactOutputMatches);
            Interlocked.Increment(ref inverseMatches);
        });

    Require(states == 108 && exactOutputMatches == 108 &&
            inverseMatches == 108 && stateKeys.Count == 108 &&
            documentHashes.Count == 108 && envelopeHashes.Count == 108 &&
            staticProjectionHashes.Count == 108 && outputHashes.Count ==
                Id65AuthoringRemotePadTerrainSetAggregatePlanProjector
                    .ExpectedPhysicalOutputHashCount &&
            modelHashes.Count == 4 && aggregateHashes.Count == 108,
        "The complete schema-5 aggregate matrix cardinality changed.");
    Id65AuthoringRemotePadTerrainSetAggregatePlan lastPlan = retainedPlan
        ?? throw new InvalidOperationException(
            "The schema-5 aggregate matrix did not retain its selected plan.");

    Id65AuthoringRemotePadTerrainSetAggregatePlan repeat =
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Project(new(
            lastState.Stored with
            {
                Path = Path.Combine(
                    Path.GetTempPath(),
                    "ignored-schema5-store-path",
                    Id65AuthoringEditorDocumentStore.FileName),
                Document = Id65AuthoringEditorDocument.Parse(
                    lastState.Document.SerializeUtf8())
            },
            lastState.Stored.EnvelopeCanonicalSha256,
            manifest,
            nativeInputs,
            v3Template));
    Require(Equivalent(lastPlan, repeat),
        "Repeated schema-5 aggregate projection changed.");
    int deterministicRepeats = 1;

    Id65AuthoringRemotePadTerrainSetAggregatePlan replayedSuppliedIdentity =
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Project(new(
            oldestState.Stored,
            oldestState.Stored.EnvelopeCanonicalSha256,
            manifest,
            nativeInputs,
            v3Template));
    Require(
        replayedSuppliedIdentity
            .SuppliedStoreResultMatchesExpectedEnvelopeIdentity &&
        !replayedSuppliedIdentity.CurrentOnDiskFreshnessVerified,
        "A supplied historical schema-5 envelope was mislabeled as current disk state.");
    int suppliedIdentityReplayChecks = 1;

    int identityRejects = 0;
    void RejectIdentity(
        string label,
        Id65AuthoringEditorDocumentStoreResult? selectedStored = null,
        string? selectedExpectedEnvelope = null,
        UnusedLevel65BlankLevelLabManifest? selectedManifest = null,
        Id65AuthoringEditorNativeInputProvider.InputSet? selectedInputs = null,
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan?
            selectedV3 = null)
    {
        Reject(label, () =>
            Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Project(new(
                selectedStored ?? lastState.Stored,
                selectedExpectedEnvelope ??
                    lastState.Stored.EnvelopeCanonicalSha256,
                selectedManifest ?? manifest,
                selectedInputs ?? nativeInputs,
                selectedV3 ?? v3Template)));
        identityRejects++;
    }

    RejectIdentity(
        "mismatched store result and expected envelope",
        oldestState.Stored,
        lastState.Stored.EnvelopeCanonicalSha256);
    RejectIdentity("forged expected envelope",
        selectedExpectedEnvelope: new string('0', 64));
    RejectIdentity("forged envelope",
        selectedStored: lastState.Stored with
        {
            EnvelopeCanonicalSha256 = new string('0', 64)
        });
    RejectIdentity(
        "jointly forged store and expected envelope",
        lastState.Stored with
        {
            EnvelopeCanonicalSha256 = new string('0', 64)
        },
        new string('0', 64));
    RejectIdentity("forged projection contract",
        selectedStored: lastState.Stored with
        {
            ProjectionContractId = "forged"
        });
    RejectIdentity("forged static profile",
        selectedStored: lastState.Stored with
        {
            StaticAuthoringProfileId = "forged"
        });
    RejectIdentity("forged static catalog",
        selectedStored: lastState.Stored with
        {
            StaticAuthoringCatalogSha256 = new string('0', 64)
        });
    RejectIdentity("forged static scene",
        selectedStored: lastState.Stored with
        {
            StaticAuthoringSceneGraphSha256 = new string('0', 64)
        });
    RejectIdentity("forged static projection",
        selectedStored: lastState.Stored with
        {
            StaticAuthoringProjectionSha256 = new string('0', 64)
        });
    RejectIdentity("forged terrain manifest projection",
        selectedStored: lastState.Stored with
        {
            StaticAuthoringTerrainManifestSha256 = new string('0', 64)
        });
    RejectIdentity("document/envelope cross-pair",
        selectedStored: lastState.Stored with
        {
            Document = oldestState.Document
        });
    RejectIdentity("forged workspace key",
        selectedManifest: manifest with { WorkspaceKey = "forged" });
    RejectIdentity("normal Create BIN manifest",
        selectedManifest: manifest with { NormalCreateBinEnabled = true });
    Id65AuthoringEditorNativeInputProvider.InputSet forgedInputs =
        ForgeProviderInputSetForNegativeTest(
            nativeInputs.BoundSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle,
            new string('0', 64));
    RejectIdentity("forged provider generation", selectedInputs: forgedInputs);
    byte[] forgedV3Source = v3Template.SourceData.ToArray();
    forgedV3Source[0x400] ^= 1;
    RejectIdentity("forged v3 preimage",
        selectedV3: v3Template with { SourceData = forgedV3Source });
    byte[] forgedV3Output = v3Template.OutputData.ToArray();
    forgedV3Output[0x400] ^= 1;
    RejectIdentity("forged v3 afterimage",
        selectedV3: v3Template with { OutputData = forgedV3Output });

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
            includeOptionalTerrain: false)
    ];
    int schema1To4Rejections = 0;
    foreach (Id65AuthoringEditorDocument legacy in legacyDocuments)
    {
        int schema = legacy.SchemaVersion;
        RejectIdentity(
            "schema-" + schema + " document",
            selectedStored: lastState.Stored with { Document = legacy });
        schema1To4Rejections++;
    }

    byte[] selectedSource = nativeInputs.LockedSource.CopyRow80();
    byte[] selectedOutput =
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ApplyExact(
            lastPlan,
            selectedSource,
            reverse: false);
    int partitionRejects = 0;
    byte[] badForward = selectedSource.ToArray();
    badForward[0x400] ^= 1;
    Reject("forged whole-row forward preimage", () =>
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ApplyExact(
            lastPlan,
            badForward,
            reverse: false));
    partitionRejects++;
    byte[] badReverse = selectedOutput.ToArray();
    badReverse[0x400] ^= 1;
    Reject("forged whole-row rollback afterimage", () =>
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ApplyExact(
            lastPlan,
            badReverse,
            reverse: true));
    partitionRejects++;

    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] exactOwned =
        lastPlan.OwnedRanges.Select(item => item.DeepCopy()).ToArray();
    void RejectPartition(
        string label,
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] selected,
        byte[]? selectedPartitionOutput = null)
    {
        Reject(label, () =>
            Id65AuthoringRemotePadTerrainSetAggregatePlanProjector
                .ValidatePartitionForSmoke(
                    selectedSource,
                    selectedPartitionOutput ?? selectedOutput,
                    selected,
                    lastPlan.MobyKind));
        partitionRejects++;
    }
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] duplicate =
        CopyOwned(exactOwned);
    duplicate[1] = CloneRange(duplicate[1], stableId: duplicate[0].StableId);
    RejectPartition("duplicate stable ID", duplicate);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] overlap =
        CopyOwned(exactOwned);
    overlap[1] = CloneRange(
        overlap[1],
        dataRelativeOffset: overlap[0].DataRelativeOffset);
    RejectPartition("overlapping owned range", overlap);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badBefore =
        CopyOwned(exactOwned);
    badBefore[0] = CloneRange(
        badBefore[0],
        before: Flip(badBefore[0].CopyBefore()));
    RejectPartition("forged owned preimage", badBefore);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badAfter =
        CopyOwned(exactOwned);
    badAfter[0] = CloneRange(
        badAfter[0],
        after: Flip(badAfter[0].CopyAfter()));
    RejectPartition("forged owned afterimage", badAfter);

    int redGemIndex = Array.FindIndex(exactOwned, range => range.Domain ==
        Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem);
    Require(redGemIndex >= 0 &&
            HasExactResidentT21XyzOwnership(lastPlan),
        "The retained schema-5 plan did not expose one exact resident-T21 XYZ range.");
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badRedGemStableId =
        CopyOwned(exactOwned);
    badRedGemStableId[redGemIndex] = CloneRange(
        badRedGemStableId[redGemIndex],
        stableId: "schema5.row80.forged");
    RejectPartition("forged Red Gem stable ID", badRedGemStableId);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badRedGemOffset =
        CopyOwned(exactOwned);
    badRedGemOffset[redGemIndex] = CloneRange(
        badRedGemOffset[redGemIndex],
        dataRelativeOffset: badRedGemOffset[redGemIndex].DataRelativeOffset + 1);
    RejectPartition("forged Red Gem XYZ offset", badRedGemOffset);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badRedGemLength =
        CopyOwned(exactOwned);
    badRedGemLength[redGemIndex] = CloneRange(
        badRedGemLength[redGemIndex],
        before: badRedGemLength[redGemIndex].CopyBefore()[..^1],
        after: badRedGemLength[redGemIndex].CopyAfter()[..^1]);
    RejectPartition("forged Red Gem XYZ length", badRedGemLength);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badRedGemOwner =
        CopyOwned(exactOwned);
    badRedGemOwner[redGemIndex] = CloneRange(
        badRedGemOwner[redGemIndex],
        ownerId: "forged.red-gem.owner");
    RejectPartition("forged Red Gem owner", badRedGemOwner);
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] badRedGemDomain =
        CopyOwned(exactOwned);
    badRedGemDomain[redGemIndex] = CloneRange(
        badRedGemDomain[redGemIndex],
        domain: Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Grass);
    RejectPartition("forged Red Gem domain", badRedGemDomain);

    int aliasChecks = 0;
    byte[] ownedCopy = lastPlan.OwnedRanges[0].CopyBefore();
    ownedCopy[0] ^= 1;
    Require(Hash(lastPlan.OwnedRanges[0].CopyBefore()) ==
            lastPlan.OwnedRanges[0].BeforeSha256,
        "The schema-5 aggregate leaked mutable owned bytes.");
    aliasChecks++;
    byte[] inverseCopy = lastPlan.InverseRanges[0].CopyAfter();
    inverseCopy[0] ^= 1;
    Require(Hash(lastPlan.InverseRanges[0].CopyAfter()) ==
            lastPlan.InverseRanges[0].AfterSha256,
        "The schema-5 aggregate leaked mutable inverse bytes.");
    aliasChecks++;
    byte[] documentCopy = lastPlan.Document.SerializeUtf8();
    documentCopy[0] ^= 1;
    Require(lastPlan.Document.CanonicalSha256 ==
            lastPlan.DocumentCanonicalSha256,
        "The schema-5 aggregate leaked mutable document bytes.");
    aliasChecks++;
    string[] copiedTerrainIds = lastPlan.ActiveTerrainUnitIds.ToArray();
    copiedTerrainIds[0] = "forged";
    Require(lastPlan.ActiveTerrainUnitIds.SequenceEqual(
            lastState.TerrainIds),
        "The schema-5 aggregate leaked its active terrain-unit list.");
    aliasChecks++;

    string[] falseProperties =
    [
        "StaticAuthoringProjectionRecomputedFromStoreBytes",
        "ContainsCompleteCompositeAfterimage",
        "ContainsDiscImageBytes",
        "ContainsCueBytes",
        "PartialRangeApplicationAuthorized",
        "CrossSliceCompositionSupported",
        "AfterimageStackingAuthorized",
        "GenericExporterCompatible",
        "RetailWriterCompatible",
        "RetailLevelMutationAuthorized",
        "PersistenceEnabled",
        "WriterAuthorized",
        "AggregateMaterializerEnabled",
        "CandidateMaterializerEnabled",
        "PhysicalMaterializationEnabled",
        "WritesFileSystem",
        "WritesDiscImage",
        "WritesBin",
        "WritesCue",
        "AppIntegrated",
        "UiIntegrated",
        "CreateBinEnabled",
        "NormalCreateBinEnabled",
        "RuntimeCandidateAuthorized",
        "RuntimeProven",
        "SaveAuthorized",
        "SavePersistenceProven",
        "TreasureTotal200Proven",
        "MusicLongPlayProven",
        "ExitDestination65Proven",
        "ReleaseIntegrated",
        "ReleaseAuthorized",
        "PromotionAuthorized",
        "Publishable",
        "CurrentOnDiskFreshnessVerified",
        "HostileFilesystemRaceSafe",
        "CommonWorkspaceLeaseOwned"
    ];
    foreach (string propertyName in falseProperties)
    {
        PropertyInfo property =
            typeof(Id65AuthoringRemotePadTerrainSetAggregatePlan)
                .GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                "Missing schema-5 fail-closed property " + propertyName + ".");
        Require(property.PropertyType == typeof(bool) &&
                property.GetValue(lastPlan) is false,
            "The schema-5 aggregate plan enabled " + propertyName + ".");
    }
    Require(typeof(Id65AuthoringRemotePadTerrainSetAggregatePlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.Name != "Path" &&
                property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType)),
        "The schema-5 aggregate plan exposed a path, stream, or byte array.");
    Require(typeof(Id65AuthoringRemotePadTerrainSetAggregatePlanRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
                !typeof(Delegate).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType !=
                    typeof(Id65CompiledAuthoringRemotePadTerrainSetPhysical) &&
                property.PropertyType !=
                    typeof(Id65AuthoringRemotePadTerrainSetAggregatePlan)),
        "The schema-5 aggregate request gained caller afterimages or plans.");
    Require(identityRejects == 20 && schema1To4Rejections == 4 &&
            suppliedIdentityReplayChecks == 1 && partitionRejects == 11 &&
            aliasChecks == 4,
        "The schema-5 aggregate negative/copy matrix changed.");

    Console.WriteLine(
        "ID65_AUTHORING_REMOTE_PAD_TERRAIN_SET_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
        "states=108 terrain_states=4 choices=3 positions=9 " +
        "store_readbacks=108 exact_output_matches=108 inverse_matches=108 " +
        "output_hashes=76 model_hashes=4 aggregate_hashes=108 " +
        "deterministic_repeats=" + deterministicRepeats + " " +
        "identity_rejects=" + identityRejects + " " +
        "schema1_4_rejections=" + schema1To4Rejections + " " +
        "supplied_identity_replay_checks=" + suppliedIdentityReplayChecks + " " +
        "partition_rejects=" + partitionRejects + " " +
        "alias_checks=" + aliasChecks + " " +
        "locked_source=true overlay_preserved=true executable_preserved=true " +
        "retail_wad16_rejected=true plan_filesystem=false materializer=false " +
        "candidate=false create_bin=false runtime=false release=false");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore,
    "The schema-5 aggregate smoke changed its locked fixture.");

static void RequirePlan(
    Id65AuthoringRemotePadTerrainSetAggregatePlan plan,
    StateInput state,
    UnusedLevel65BlankLevelLabManifest manifest)
{
    Require(
        plan.ProfileId ==
            Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ProfileId &&
        plan.SchemaVersion ==
            Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.SchemaVersion &&
        plan.DocumentCanonicalSha256 == state.Document.CanonicalSha256 &&
        plan.EnvelopeCanonicalSha256 ==
            state.Stored.EnvelopeCanonicalSha256 &&
        plan.StaticAuthoringProfileId == Id65AuthoringEditorDocumentStore
            .RemotePadMobyChoiceTerrainSetStaticAuthoringProfileId &&
        plan.StaticAuthoringProjectionSha256 ==
            state.Stored.StaticAuthoringProjectionSha256 &&
        plan.StaticAuthoringTerrainManifestSha256 ==
            state.Document.TerrainSetManifestSha256 &&
        plan.TerrainManifestSha256 ==
            state.Document.TerrainSetManifestSha256 &&
        plan.ActiveTerrainUnitIds.SequenceEqual(state.TerrainIds) &&
        plan.Terrain.ActiveTerrainUnitIds.SequenceEqual(state.TerrainIds) &&
        plan.Terrain.Units.Select(item => item.UnitId)
            .SequenceEqual(state.TerrainIds) &&
        plan.MobyKind == state.Kind &&
        plan.Placement.PositionId == state.Position.PositionId &&
        plan.Placement.Present !=
            (state.Kind == Id65RemotePadMobyKind.None) &&
        plan.OutputModelSha256 == plan.Terrain.T66TerrainModelSha256 &&
        plan.Terrain.T66TextureComponentSha256 ==
            Id65V2NativeTextureCompositionCompiler
                .ExpectedOutputTextureComponentSha256 &&
        plan.OutputTexturePagesSha256 ==
            Id65V2NativeTextureCompositionCompiler
                .ExpectedOutputTexturePagesSha256 &&
        plan.SourceRow80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        plan.TargetOverlaySha256 ==
            Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
        plan.GlobalExecutableSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
        plan.NativeInputSetSha256 ==
            Id65AuthoringEditorNativeInputProvider
                .ExpectedCanonicalInputSetSha256 &&
        plan.V3DeterministicPlanSha256 ==
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                .ExpectedDeterministicPlanSha256 &&
        plan.OwnedRangeCount == plan.InverseRangeCount &&
        plan.OwnedRangeCount > 0 && plan.ProtectedRangeCount > 0 &&
        plan.GuardedByteCount >= plan.ChangedByteCount &&
        CountChangedBytes(plan.OwnedRanges) == plan.ChangedByteCount &&
        plan.GuardedByteCount - plan.ChangedByteCount ==
            CountResidentT21UnchangedGuardedBytes(plan.OwnedRanges) &&
        HasExactResidentT21XyzOwnership(plan) &&
        plan.GuardedByteCount + plan.ProtectedByteCount == 0x2E2000 &&
        plan.AtomicGroups.Count == 4 &&
        plan.Destination.TargetDataWadEntry == 80 &&
        plan.Destination.TargetDataWadOffset == 0x6936800 &&
        plan.Destination.RejectedRetailDataWadEntry == 16 &&
        plan.Destination.RejectedRetailDataWadOffset == 0x119E000 &&
        plan.Destination.TargetDataWadOffset !=
            plan.Destination.RejectedRetailDataWadOffset &&
        plan.Destination.SourceImageSha256 == manifest.LockedBaseImageSha256 &&
        plan.PureInMemory && plan.FullExactSchema5DocumentRequired &&
        plan.SuppliedStoreResultMatchesExpectedEnvelopeIdentity &&
        plan.StaticAuthoringProjectionRecomputedFromCanonicalDocument &&
        plan.StoreEnvelopeIdentityIndependentlyVerified &&
        plan.NativeInputGenerationReverified &&
        plan.LockedSourceOverlayExecutableIdentityPreserved &&
        plan.ExactPhysicalDestinationBindingPinned &&
        plan.PhysicalDestinationReadbackVerified &&
        plan.FourTopologyTerrainSetBindingVerified &&
        plan.FinalT66TerrainSemanticReadbackVerified &&
        plan.ProtectedComplementVerified && plan.ExactInverseVerified &&
        plan.OneAtomicTransactionRequired &&
        plan.ContainsOwnedAfterimageBytes &&
        plan.RequiresTrustedExclusiveWorkspaceDirectory,
        "Schema-5 aggregate identity, topology, destination, or authority changed for " +
        state.StateKey + ".");
}

static bool StoreEquivalent(
    Id65AuthoringEditorDocumentStoreResult left,
    Id65AuthoringEditorDocumentStoreResult right) =>
    left.Path == right.Path &&
    left.Document.SerializeUtf8().SequenceEqual(right.Document.SerializeUtf8()) &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.ProjectionContractId == right.ProjectionContractId &&
    left.CompositeProfileId == right.CompositeProfileId &&
    left.CompositeManifestSha256 == right.CompositeManifestSha256 &&
    left.CompositeOutputRow80Sha256 == right.CompositeOutputRow80Sha256 &&
    left.CompositeDeterministicPlanSha256 ==
        right.CompositeDeterministicPlanSha256 &&
    left.StaticAuthoringProfileId == right.StaticAuthoringProfileId &&
    left.StaticAuthoringCatalogSha256 == right.StaticAuthoringCatalogSha256 &&
    left.StaticAuthoringSceneGraphSha256 ==
        right.StaticAuthoringSceneGraphSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.StaticAuthoringTerrainManifestSha256 ==
        right.StaticAuthoringTerrainManifestSha256;

static bool Equivalent(
    Id65AuthoringRemotePadTerrainSetAggregatePlan left,
    Id65AuthoringRemotePadTerrainSetAggregatePlan right) =>
    left.ProfileId == right.ProfileId &&
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.StaticAuthoringTerrainManifestSha256 ==
        right.StaticAuthoringTerrainManifestSha256 &&
    left.NativeInputSetSha256 == right.NativeInputSetSha256 &&
    left.V3DeterministicPlanSha256 == right.V3DeterministicPlanSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.CompilerOwnedRangeMapSha256 == right.CompilerOwnedRangeMapSha256 &&
    left.CompilerTransactionSha256 == right.CompilerTransactionSha256 &&
    left.CompilerDeterministicPlanSha256 ==
        right.CompilerDeterministicPlanSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.InverseRangeMapSha256 == right.InverseRangeMapSha256 &&
    left.ProtectedRangeMapSha256 == right.ProtectedRangeMapSha256 &&
    left.ProtectedComplementSha256 == right.ProtectedComplementSha256 &&
    left.AtomicGroupMapSha256 == right.AtomicGroupMapSha256 &&
    left.DeterministicAggregatePlanSha256 ==
        right.DeterministicAggregatePlanSha256 &&
    left.Placement == right.Placement &&
    TerrainEquivalent(left.Terrain, right.Terrain) &&
    left.ActiveTerrainUnitIds.SequenceEqual(right.ActiveTerrainUnitIds) &&
    left.Destination == right.Destination &&
    left.ProtectedRanges.SequenceEqual(right.ProtectedRanges) &&
    left.AtomicGroups.SequenceEqual(right.AtomicGroups) &&
    left.OwnedRanges.Select(ToRangeTuple)
        .SequenceEqual(right.OwnedRanges.Select(ToRangeTuple)) &&
    left.InverseRanges.Select(ToRangeTuple)
        .SequenceEqual(right.InverseRanges.Select(ToRangeTuple));

static bool TerrainEquivalent(
    Id65AuthoringRemotePadTerrainSetPhysicalReadback left,
    Id65AuthoringRemotePadTerrainSetPhysicalReadback right) =>
    left.TerrainManifestSha256 == right.TerrainManifestSha256 &&
    left.ActiveTerrainUnitIds.SequenceEqual(right.ActiveTerrainUnitIds) &&
    left.V3TerrainModelSha256 == right.V3TerrainModelSha256 &&
    left.T66TerrainModelSha256 == right.T66TerrainModelSha256 &&
    left.T66TextureComponentSha256 == right.T66TextureComponentSha256 &&
    left.V3TerrainEnvironmentSha256 == right.V3TerrainEnvironmentSha256 &&
    left.FinalT66EnvironmentSha256 == right.FinalT66EnvironmentSha256 &&
    left.CollisionSha256 == right.CollisionSha256 &&
    left.V3TerrainProtectedComplementSha256 ==
        right.V3TerrainProtectedComplementSha256 &&
    left.FinalT66ProtectedComponentSetSha256 ==
        right.FinalT66ProtectedComponentSetSha256 &&
    left.UsedModelBytes == right.UsedModelBytes &&
    left.ZeroTailBytes == right.ZeroTailBytes &&
    left.MandatoryBaseFaceModelOffset == right.MandatoryBaseFaceModelOffset &&
    left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId &&
    left.Units.SequenceEqual(right.Units);

static (string, string,
    Id65AuthoringRemotePadTerrainSetAggregateRangeDomain,
    int, int, long, int, string, string) ToRangeTuple(
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range) =>
    (range.StableId, range.OwnerId, range.Domain, range.TargetWadEntry,
        range.DataRelativeOffset, range.WadOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] CopyOwned(
    IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges) =>
    ranges.Select(item => item.DeepCopy()).ToArray();

static Id65AuthoringRemotePadTerrainSetAggregateOwnedRange CloneRange(
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange source,
    string? stableId = null,
    string? ownerId = null,
    Id65AuthoringRemotePadTerrainSetAggregateRangeDomain? domain = null,
    int? dataRelativeOffset = null,
    byte[]? before = null,
    byte[]? after = null) =>
    new(
        stableId ?? source.StableId,
        ownerId ?? source.OwnerId,
        domain ?? source.Domain,
        dataRelativeOffset ?? source.DataRelativeOffset,
        before ?? source.CopyBefore(),
        after ?? source.CopyAfter());

static bool HasExactResidentT21XyzOwnership(
    Id65AuthoringRemotePadTerrainSetAggregatePlan plan)
{
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] related =
        plan.OwnedRanges.Where(range =>
            range.StableId ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .ResidentT21XyzOwnedRangeStableId ||
            range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
            range.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem)
        .ToArray();
    if (plan.MobyKind != Id65RemotePadMobyKind.RedGemT21)
        return related.Length == 0;
    if (related.Length != 1 ||
        related[0].StableId !=
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                .ResidentT21XyzOwnedRangeStableId ||
        related[0].OwnerId != Id65RemotePadRedGemT21Catalog.PlacementId ||
        related[0].Domain !=
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem ||
        related[0].DataRelativeOffset !=
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateOffset ||
        related[0].ByteLength !=
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateByteLength)
    {
        return false;
    }
    byte[] exactSourceT21 = Convert.FromHexString(
        Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
    byte[] exactOutputT21 = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
        plan.Placement.PositionId);
    return related[0].CopyBefore().AsSpan().SequenceEqual(exactSourceT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength)) &&
        related[0].CopyAfter().AsSpan().SequenceEqual(exactOutputT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength));
}

static int CountChangedBytes(
    IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges)
{
    int changed = 0;
    foreach (Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range in ranges)
    {
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        Require(before.Length == after.Length,
            "Changed-byte comparison received different lengths.");
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) changed++;
        }
    }
    return changed;
}

static int CountResidentT21UnchangedGuardedBytes(
    IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges) =>
    ranges.Where(range => range.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem)
        .Sum(range => range.ByteLength - CountChangedBytes([range]));

static byte[] Flip(byte[] bytes)
{
    byte[] changed = bytes.ToArray();
    changed[0] ^= 1;
    return changed;
}

static Id65AuthoringEditorNativeInputProvider.InputSet
    ForgeProviderInputSetForNegativeTest(
        Id65AuthoringEditorAggregateBoundSource boundSource,
        Id65V2NativeTextureWitness textureWitness,
        Id65MobyDependencyBundleDescriptor grassBundle,
        string canonicalInputSetSha256)
{
    Type implementation = typeof(Id65AuthoringEditorNativeInputProvider)
        .GetNestedType("ProviderInputSet", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "The provider-owned native-input implementation is missing.");
    ConstructorInfo constructor = implementation.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single();
    Require(implementation.IsNestedPrivate && implementation.IsSealed &&
        implementation.BaseType ==
            typeof(Id65AuthoringEditorNativeInputProvider.InputSet) &&
        constructor.GetParameters().Select(item => item.ParameterType).SequenceEqual(
        [
            typeof(Id65AuthoringEditorAggregateBoundSource),
            typeof(Id65V2NativeTextureWitness),
            typeof(Id65MobyDependencyBundleDescriptor),
            typeof(string)
        ]),
        "The provider-owned native-input implementation topology changed.");
    return (Id65AuthoringEditorNativeInputProvider.InputSet)(
        Activator.CreateInstance(
            implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                boundSource,
                textureWitness,
                grassBundle,
                canonicalInputSetSha256
            ],
            culture: null)
        ?? throw new InvalidOperationException(
            "The forged provider input could not be created."));
}

static void Reject(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or
        NotSupportedException or
        ArgumentException or
        KeyNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException(
        "Expected fail-closed schema-5 aggregate rejection: " + label + ".");
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
    throw new DirectoryNotFoundException("Could not find the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    using FileStream stream = File.OpenRead(path);
    return new(
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record StateInput(
    string StateKey,
    string[] TerrainIds,
    Id65RemotePadMobyKind Kind,
    Id65RemotePadGrassT107Position Position,
    Id65AuthoringEditorDocument Document,
    Id65AuthoringEditorDocumentStoreResult Stored);

internal readonly record struct FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
