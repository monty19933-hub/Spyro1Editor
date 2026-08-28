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
    "Missing exact schema-4 aggregate-plan locked fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringRemotePadMobyChoiceAggregatePlanProjectorSmoke-" +
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
    string? expectedPriorEnvelope = null;
    Id65AuthoringEditorDocumentStoreResult? firstStored = null;
    Id65AuthoringEditorDocumentStoreResult? lastStoredResult = null;
    Id65AuthoringRemotePadMobyChoiceAggregatePlan? lastProjectedPlan = null;
    HashSet<string> documentHashes = new(StringComparer.Ordinal);
    HashSet<string> envelopeHashes = new(StringComparer.Ordinal);
    HashSet<string> staticProjectionHashes = new(StringComparer.Ordinal);
    HashSet<string> outputHashes = new(StringComparer.Ordinal);
    HashSet<string> aggregateHashes = new(StringComparer.Ordinal);
    HashSet<string> stateKeys = new(StringComparer.Ordinal);
    int states = 0;
    int deterministicRepeats = 0;
    int storeReadbacks = 0;
    int exactOutputMatches = 0;
    int inverseMatches = 0;

    foreach (bool terrainPresent in new[] { false, true })
    {
        foreach (Id65RemotePadMobyChoice choice in
                 Id65RemotePadMobyChoiceCatalog.Choices)
        {
            foreach (Id65RemotePadGrassT107Position position in
                     Id65RemotePadGrassT107Catalog.Positions)
            {
                string stateKey = (terrainPresent ? "terrain-on" : "terrain-off") +
                    "|" + choice.KindId + "|" + position.PositionId;
                Require(stateKeys.Add(stateKey),
                    "Duplicate schema-4 aggregate state " + stateKey + ".");
                Id65AuthoringEditorDocument document =
                    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                        choice.Kind,
                        position.PositionId,
                        includeOptionalTerrain: terrainPresent);
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
                        "The saved schema-4 document disappeared.");
                Require(StoreEquivalent(saved, loaded),
                    "Schema-4 save/load readback changed for " + stateKey + ".");
                storeReadbacks++;

                Id65AuthoringRemotePadMobyChoiceAggregatePlan plan =
                    Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Project(new(
                        loaded,
                        saved.EnvelopeCanonicalSha256,
                        manifest,
                        nativeInputs));
                Id65CompiledAuthoringRemotePadMobyChoicePhysical physical =
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(new(
                        document,
                        nativeInputs.LockedSource,
                        nativeInputs.PrivateTextureWitness,
                        nativeInputs.GrassBundle));
                byte[] source = nativeInputs.LockedSource.CopyRow80();
                byte[] output =
                    Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ApplyExact(
                        plan,
                        source,
                        reverse: false);
                byte[] restored =
                    Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ApplyExact(
                        plan,
                        output,
                        reverse: true);
                Require(output.SequenceEqual(physical.CopyOutputRow80()) &&
                        Hash(output) == plan.OutputRow80Sha256,
                    "Aggregate output did not exactly match the physical compiler for " +
                    stateKey + ".");
                exactOutputMatches++;
                Require(restored.SequenceEqual(source),
                    "Aggregate inverse did not restore the locked row for " +
                    stateKey + ".");
                inverseMatches++;

                int expectedOwned = choice.Kind switch
                {
                    Id65RemotePadMobyKind.None => 7_956,
                    Id65RemotePadMobyKind.GrassT107 => 7_960,
                    Id65RemotePadMobyKind.RedGemT21 => 7_957,
                    _ => throw new ArgumentOutOfRangeException()
                };
                int expectedProtected = choice.Kind switch
                {
                    Id65RemotePadMobyKind.None => 7_957,
                    Id65RemotePadMobyKind.GrassT107 => 7_961,
                    Id65RemotePadMobyKind.RedGemT21 => 7_958,
                    _ => throw new ArgumentOutOfRangeException()
                };
                int expectedGuarded = choice.Kind switch
                {
                    Id65RemotePadMobyKind.None => 1_010_852,
                    Id65RemotePadMobyKind.GrassT107 => 1_010_952,
                    Id65RemotePadMobyKind.RedGemT21 => 1_010_864,
                    _ => throw new ArgumentOutOfRangeException()
                };
                string expectedT21 = choice.Kind == Id65RemotePadMobyKind.RedGemT21
                    ? Id65RemotePadRedGemT21Catalog
                        .GetRequiredPosition(position.PositionId).OutputRowSha256
                    : Id65RemotePadRedGemT21Catalog.SourceRowSha256;
                Require(
                    plan.DocumentCanonicalSha256 == document.CanonicalSha256 &&
                    plan.EnvelopeCanonicalSha256 == saved.EnvelopeCanonicalSha256 &&
                    plan.StaticAuthoringProjectionSha256 ==
                        saved.StaticAuthoringProjectionSha256 &&
                    plan.MobyKind == choice.Kind &&
                    plan.StructuralTerrainPresent == terrainPresent &&
                    plan.Placement.PositionId == position.PositionId &&
                    plan.Placement.Present ==
                        (choice.Kind != Id65RemotePadMobyKind.None) &&
                    plan.OwnedRangeCount == expectedOwned &&
                    plan.InverseRangeCount == expectedOwned &&
                    plan.ProtectedRangeCount == expectedProtected &&
                    plan.GuardedByteCount == expectedGuarded &&
                    plan.ProtectedByteCount == 0x2E2000 - expectedGuarded &&
                    plan.SourceT21RowSha256 ==
                        Id65RemotePadRedGemT21Catalog.SourceRowSha256 &&
                    plan.OutputT21RowSha256 == expectedT21 &&
                    plan.TargetOverlaySha256 ==
                        Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
                    plan.GlobalExecutableSha256 ==
                        Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 &&
                    plan.Destination.TargetDataWadEntry == 80 &&
                    plan.Destination.TargetDataWadOffset == 0x6936800 &&
                    plan.Destination.RejectedRetailDataWadEntry == 16 &&
                    plan.Destination.RejectedRetailDataWadOffset == 0x119E000 &&
                    plan.Destination.TargetDataWadOffset !=
                        plan.Destination.RejectedRetailDataWadOffset &&
                    plan.Destination.RetailTownSquareDestinationRejected,
                    "Schema-4 aggregate identity/routing changed for " + stateKey + ".");

                Id65AuthoringRemotePadMobyChoiceAggregatePlan repeat =
                    Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Project(new(
                        loaded with
                        {
                            Path = Path.Combine(
                                Path.GetTempPath(),
                                "ignored-schema4-store-path",
                                Id65AuthoringEditorDocumentStore.FileName),
                            Document = Id65AuthoringEditorDocument.Parse(
                                document.SerializeUtf8())
                        },
                        saved.EnvelopeCanonicalSha256,
                        manifest,
                        nativeInputs));
                Require(Equivalent(plan, repeat),
                    "Repeated schema-4 aggregate projection changed for " +
                    stateKey + ".");
                deterministicRepeats++;

                Require(documentHashes.Add(plan.DocumentCanonicalSha256),
                    "Two schema-4 states shared a document identity.");
                Require(envelopeHashes.Add(plan.EnvelopeCanonicalSha256),
                    "Two schema-4 states shared a store-envelope identity.");
                Require(staticProjectionHashes.Add(
                        plan.StaticAuthoringProjectionSha256),
                    "Two schema-4 states shared a static-projection identity.");
                outputHashes.Add(plan.OutputRow80Sha256);
                Require(aggregateHashes.Add(plan.DeterministicAggregatePlanSha256),
                    "Two schema-4 states shared an aggregate-plan identity.");
                firstStored ??= loaded;
                lastStoredResult = loaded;
                lastProjectedPlan = plan;
                states++;
            }
        }
    }

    Require(states == 54 && storeReadbacks == 54 &&
            deterministicRepeats == 54 && exactOutputMatches == 54 &&
            inverseMatches == 54 && stateKeys.Count == 54 &&
            documentHashes.Count == 54 && envelopeHashes.Count == 54 &&
            staticProjectionHashes.Count == 54 && outputHashes.Count == 38 &&
            aggregateHashes.Count == 54,
        "The complete schema-4 aggregate matrix cardinality changed.");
    Id65AuthoringEditorDocumentStoreResult oldestStored = firstStored
        ?? throw new InvalidOperationException(
            "The schema-4 aggregate matrix did not retain its first store state.");
    Id65AuthoringEditorDocumentStoreResult lastStored = lastStoredResult
        ?? throw new InvalidOperationException(
            "The schema-4 aggregate matrix did not retain its final store state.");
    Id65AuthoringRemotePadMobyChoiceAggregatePlan lastPlan = lastProjectedPlan
        ?? throw new InvalidOperationException(
            "The schema-4 aggregate matrix did not retain its final plan.");
    string lastSchema4ExpectedEnvelope = expectedPriorEnvelope
        ?? throw new InvalidOperationException(
            "The schema-4 aggregate matrix did not retain its final expected envelope identity.");

    int identityRejects = 0;
    void RejectIdentity(
        string label,
        Id65AuthoringEditorDocumentStoreResult? selectedStored = null,
        string? selectedExpectedEnvelope = null,
        UnusedLevel65BlankLevelLabManifest? selectedManifest = null,
        Id65AuthoringEditorNativeInputProvider.InputSet? selectedInputs = null)
    {
        Reject(label, () =>
            Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Project(new(
                selectedStored ?? lastStored,
                selectedExpectedEnvelope ?? lastSchema4ExpectedEnvelope,
                selectedManifest ?? manifest,
                selectedInputs ?? nativeInputs)));
        identityRejects++;
    }

    Id65AuthoringRemotePadMobyChoiceAggregatePlan replayedSuppliedIdentity =
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Project(new(
            oldestStored,
            oldestStored.EnvelopeCanonicalSha256,
            manifest,
            nativeInputs));
    Require(
        replayedSuppliedIdentity
            .SuppliedStoreResultMatchesExpectedEnvelopeIdentity &&
        !replayedSuppliedIdentity.CurrentOnDiskFreshnessVerified,
        "A replayed supplied schema-4 envelope identity was mislabeled as current on-disk state.");
    int suppliedIdentityReplayChecks = 1;

    RejectIdentity(
        "mismatched store result and expected envelope identity",
        oldestStored,
        lastSchema4ExpectedEnvelope);
    RejectIdentity(
        "forged expected envelope identity",
        lastStored,
        new string('0', 64));
    RejectIdentity(
        "forged envelope identity",
        lastStored with { EnvelopeCanonicalSha256 = new string('0', 64) });
    RejectIdentity(
        "jointly forged store and expected envelope identities",
        lastStored with { EnvelopeCanonicalSha256 = new string('0', 64) },
        new string('0', 64));
    RejectIdentity(
        "forged projection contract",
        lastStored with { ProjectionContractId = "forged" });
    RejectIdentity(
        "forged static profile",
        lastStored with { StaticAuthoringProfileId = "forged" });
    RejectIdentity(
        "forged static catalog",
        lastStored with
        {
            StaticAuthoringCatalogSha256 = new string('0', 64)
        });
    RejectIdentity(
        "forged static scene graph",
        lastStored with
        {
            StaticAuthoringSceneGraphSha256 = new string('0', 64)
        });
    RejectIdentity(
        "forged static projection",
        lastStored with
        {
            StaticAuthoringProjectionSha256 = new string('0', 64)
        });
    RejectIdentity(
        "stored document/static projection cross-pair",
        lastStored with { Document = oldestStored.Document });
    RejectIdentity(
        "forged workspace key",
        selectedManifest: manifest with { WorkspaceKey = "forged" });
    RejectIdentity(
        "normal Create BIN manifest",
        selectedManifest: manifest with { NormalCreateBinEnabled = true });
    Id65AuthoringEditorNativeInputProvider.InputSet forgedInputs =
        ForgeProviderInputSetForNegativeTest(
            nativeInputs.BoundSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle,
            new string('0', 64));
    RejectIdentity("forged provider generation", selectedInputs: forgedInputs);

    Id65AuthoringEditorDocument schema5Document =
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
            lastStored.Document.RemotePadMobyKind!.Value,
            lastStored.Document.RemotePadMobyPositionId!,
            lastStored.Document.Terrain.Count == 1
                ? [Id65AuthoringTerrainSetCatalog.Tile1Id]
                : []);
    Id65AuthoringEditorDocumentStoreResult schema5Stored =
        await Id65AuthoringEditorDocumentStore
            .MigrateRemotePadMobyChoiceToTerrainSetPreparedAsync(
                paths,
                manifest,
                schema5Document,
                new Id65AuthoringEditorDocumentProjectionInputs(null),
                static stored => stored,
                lastSchema4ExpectedEnvelope);
    RejectIdentity(
        "schema-5 terrain-set store result",
        schema5Stored,
        schema5Stored.EnvelopeCanonicalSha256);
    int schema5Rejections = 1;

    byte[] selectedSource = nativeInputs.LockedSource.CopyRow80();
    byte[] selectedOutput =
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ApplyExact(
            lastPlan,
            selectedSource,
            reverse: false);
    int partitionRejects = 0;
    byte[] badForward = selectedSource.ToArray();
    badForward[0x400] ^= 1;
    Reject("forged whole-row forward preimage", () =>
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ApplyExact(
            lastPlan,
            badForward,
            reverse: false));
    partitionRejects++;
    byte[] badReverse = selectedOutput.ToArray();
    badReverse[0x400] ^= 1;
    Reject("forged whole-row reverse afterimage", () =>
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ApplyExact(
            lastPlan,
            badReverse,
            reverse: true));
    partitionRejects++;

    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] exactOwned =
        lastPlan.OwnedRanges.Select(item => item.DeepCopy()).ToArray();
    void RejectPartition(
        string label,
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] selected)
    {
        Reject(label, () =>
            Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector
                .ValidatePartitionForSmoke(
                    selectedSource,
                    selectedOutput,
                    selected,
                    lastPlan.MobyKind));
        partitionRejects++;
    }
    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] duplicate =
        CopyOwned(exactOwned);
    duplicate[1] = CloneRange(duplicate[1], stableId: duplicate[0].StableId);
    RejectPartition("duplicate stable ID", duplicate);
    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] overlap =
        CopyOwned(exactOwned);
    overlap[1] = CloneRange(
        overlap[1],
        dataRelativeOffset: overlap[0].DataRelativeOffset);
    RejectPartition("overlapping owned range", overlap);
    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] badBefore =
        CopyOwned(exactOwned);
    badBefore[0] = CloneRange(
        badBefore[0],
        before: Flip(badBefore[0].CopyBefore()));
    RejectPartition("forged owned preimage", badBefore);
    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] badAfter =
        CopyOwned(exactOwned);
    badAfter[0] = CloneRange(
        badAfter[0],
        after: Flip(badAfter[0].CopyAfter()));
    RejectPartition("forged owned afterimage", badAfter);

    int aliasChecks = 0;
    byte[] ownedCopy = lastPlan.OwnedRanges[0].CopyBefore();
    ownedCopy[0] ^= 1;
    Require(Hash(lastPlan.OwnedRanges[0].CopyBefore()) ==
            lastPlan.OwnedRanges[0].BeforeSha256,
        "The schema-4 aggregate leaked mutable owned bytes.");
    aliasChecks++;
    byte[] inverseCopy = lastPlan.InverseRanges[0].CopyAfter();
    inverseCopy[0] ^= 1;
    Require(Hash(lastPlan.InverseRanges[0].CopyAfter()) ==
            lastPlan.InverseRanges[0].AfterSha256,
        "The schema-4 aggregate leaked mutable inverse bytes.");
    aliasChecks++;
    byte[] documentCopy = lastPlan.Document.SerializeUtf8();
    documentCopy[0] ^= 1;
    Require(lastPlan.Document.CanonicalSha256 ==
            lastPlan.DocumentCanonicalSha256,
        "The schema-4 aggregate leaked mutable document bytes.");
    aliasChecks++;
    byte[] lockedCopy = nativeInputs.LockedSource.CopyRow80();
    lockedCopy[0] ^= 1;
    Require(nativeInputs.LockedSource.Row80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256,
        "The provider leaked mutable locked-source bytes.");
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
            typeof(Id65AuthoringRemotePadMobyChoiceAggregatePlan)
                .GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                "Missing schema-4 fail-closed property " + propertyName + ".");
        Require(property.PropertyType == typeof(bool) &&
                property.GetValue(lastPlan) is false,
            "The schema-4 aggregate plan enabled " + propertyName + ".");
    }
    Require(typeof(Id65AuthoringRemotePadMobyChoiceAggregatePlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.Name != "Path" &&
                property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType)),
        "The schema-4 aggregate plan exposed a path, stream, or byte array.");
    Require(typeof(Id65AuthoringRemotePadMobyChoiceAggregatePlanRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
                !typeof(Delegate).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType !=
                    typeof(Id65CompiledAuthoringRemotePadMobyChoicePhysical) &&
                property.PropertyType !=
                    typeof(Id65AuthoringRemotePadMobyChoiceAggregatePlan)),
        "The schema-4 aggregate request gained caller afterimages or plans.");
    Require(identityRejects == 14 && schema5Rejections == 1 &&
            suppliedIdentityReplayChecks == 1 && partitionRejects == 6 &&
            aliasChecks == 4,
        "The schema-4 aggregate negative/copy matrix changed.");

    Console.WriteLine(
        "ID65_AUTHORING_REMOTE_PAD_MOBY_CHOICE_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
        "states=54 terrain_states=2 choices=3 positions=9 " +
        "store_readbacks=54 static_projection_recomputations=54 " +
        "exact_output_matches=54 inverse_matches=54 " +
        "output_hashes=38 aggregate_hashes=54 " +
        "identity_rejects=" + identityRejects + " " +
        "schema5_rejections=" + schema5Rejections + " " +
        "supplied_identity_replay_checks=" + suppliedIdentityReplayChecks + " " +
        "partition_rejects=" + partitionRejects + " " +
        "alias_checks=" + aliasChecks + " " +
        "locked_source=true overlay_preserved=true executable_preserved=true " +
        "retail_wad16_rejected=true filesystem=false materializer=false " +
        "candidate=false create_bin=false runtime=false release=false");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore,
    "The schema-4 aggregate smoke changed its locked fixture.");

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
    Id65AuthoringRemotePadMobyChoiceAggregatePlan left,
    Id65AuthoringRemotePadMobyChoiceAggregatePlan right) =>
    left.ProfileId == right.ProfileId &&
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.NativeInputSetSha256 == right.NativeInputSetSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.SourceT21RowSha256 == right.SourceT21RowSha256 &&
    left.OutputT21RowSha256 == right.OutputT21RowSha256 &&
    left.TargetOverlaySha256 == right.TargetOverlaySha256 &&
    left.GlobalExecutableSha256 == right.GlobalExecutableSha256 &&
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
    left.Destination == right.Destination &&
    left.ProtectedRanges.SequenceEqual(right.ProtectedRanges) &&
    left.AtomicGroups.SequenceEqual(right.AtomicGroups) &&
    left.OwnedRanges.Select(ToRangeTuple)
        .SequenceEqual(right.OwnedRanges.Select(ToRangeTuple)) &&
    left.InverseRanges.Select(ToRangeTuple)
        .SequenceEqual(right.InverseRanges.Select(ToRangeTuple));

static (string, string, Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain,
    int, int, long, int, string, string) ToRangeTuple(
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange range) =>
    (range.StableId, range.OwnerId, range.Domain, range.TargetWadEntry,
        range.DataRelativeOffset, range.WadOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] CopyOwned(
    IEnumerable<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges) =>
    ranges.Select(item => item.DeepCopy()).ToArray();

static Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange CloneRange(
    Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange source,
    string? stableId = null,
    int? dataRelativeOffset = null,
    byte[]? before = null,
    byte[]? after = null) =>
    new(
        stableId ?? source.StableId,
        source.OwnerId,
        source.Domain,
        dataRelativeOffset ?? source.DataRelativeOffset,
        before ?? source.CopyBefore(),
        after ?? source.CopyAfter());

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
        "Expected fail-closed schema-4 aggregate rejection: " + label + ".");
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

internal readonly record struct FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
