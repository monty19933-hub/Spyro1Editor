using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

bool emitStatePins = args.Contains("--emit-state-pins", StringComparer.Ordinal);
string repositoryRoot = FindRepositoryRoot(
    args.FirstOrDefault(item => !item.StartsWith("--", StringComparison.Ordinal)));
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
    "Missing exact schema-3 aggregate-plan locked fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> lockedDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringRemotePadAggregatePlanProjectorSmoke-" +
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
        DiscImage.BuildCueText(lockedCuePath, Path.GetFileName(paths.LockedBaseImagePath)),
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
    Dictionary<string, Id65AuthoringEditorDocumentStoreResult> stores =
        new(StringComparer.Ordinal);
    Dictionary<string, Id65AuthoringRemotePadAggregatePlan> plans =
        new(StringComparer.Ordinal);
    HashSet<string> outputHashes = new(StringComparer.Ordinal);
    HashSet<string> aggregateHashes = new(StringComparer.Ordinal);
    int positivePlans = 0;
    int matrixStates = 0;
    int matrixSupported = 0;
    int matrixRejected = 0;
    HashSet<string> matrixStateKeys = new(StringComparer.Ordinal);
    HashSet<string> matrixOutputHashes = new(StringComparer.Ordinal);
    HashSet<string> matrixAggregateHashes = new(StringComparer.Ordinal);
    int identityRejects = 0;
    int partitionRejects = 0;
    int aliasChecks = 0;

    foreach (Id65RemotePadGrassT107Position position in
             Id65RemotePadGrassT107Catalog.Positions)
    {
        Id65AuthoringEditorDocument document =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeOptionalTerrain: true,
                placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
                mandatoryBaseFaceTextureId: 66);
        Id65AuthoringEditorDocumentStoreResult stored =
            await Id65AuthoringEditorDocumentStore.SaveAsync(
                paths,
                manifest,
                document,
                new Id65AuthoringEditorDocumentProjectionInputs(null),
                expectedPriorEnvelope);
        expectedPriorEnvelope = stored.EnvelopeCanonicalSha256;
        Id65AuthoringRemotePadAggregatePlan plan =
            Id65AuthoringRemotePadAggregatePlanProjector.Project(new(
                stored,
                manifest,
                nativeInputs));
        byte[] source = nativeInputs.LockedSource.CopyRow80();
        byte[] output = Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
            plan,
            source,
            reverse: false);
        byte[] restored = Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
            plan,
            output,
            reverse: true);
        Require(
            Hash(output) == plan.OutputRow80Sha256 &&
            restored.SequenceEqual(source) &&
            plan.Placement.PositionId == position.PositionId &&
            plan.OwnedRangeCount == 7_960 &&
            plan.ProtectedRangeCount == 7_961 &&
            plan.GuardedByteCount == 1_010_952 &&
            plan.ProtectedByteCount == 2_011_896,
            "Schema-3 aggregate plan failed position " + position.PositionId + ".");
        Require(outputHashes.Add(plan.OutputRow80Sha256),
            "Two schema-3 positions shared an output hash.");
        Require(aggregateHashes.Add(plan.DeterministicAggregatePlanSha256),
            "Two schema-3 positions shared an aggregate-plan hash.");
        stores.Add(position.PositionId, stored);
        plans.Add(position.PositionId, plan);
        Id65AuthoringRemotePadAggregatePlan repeat =
            Id65AuthoringRemotePadAggregatePlanProjector.Project(new(
                stored with
                {
                    Path = Path.Combine(
                        Path.GetTempPath(),
                        "ignored-schema3-store-path",
                        Id65AuthoringEditorDocumentStore.FileName),
                    Document = Id65AuthoringEditorDocument.Parse(
                        document.SerializeUtf8())
                },
                manifest,
                nativeInputs));
        Require(Equivalent(plan, repeat),
            "Repeated schema-3 aggregate projection changed for " +
            position.PositionId + ".");
        positivePlans += 2;
    }

    Id65AuthoringRemotePadAggregatePlan center =
        plans[Id65RemotePadGrassT107Catalog.CenterPositionId];
    Require(stores.Count == 9 && plans.Count == 9 &&
            outputHashes.Count == 9 && aggregateHashes.Count == 9,
        "The schema-3 nine-position aggregate set changed.");

    foreach (bool terrainOn in new[] { false, true })
        foreach (bool t88On in new[] { false, true })
            foreach (bool t107On in new[] { false, true })
                foreach (int mandatoryTextureId in new[] { 25, 66 })
                    foreach (Id65RemotePadGrassT107Position position in
                             Id65RemotePadGrassT107Catalog.Positions)
                    {
                        matrixStates++;
                        List<string> placements = [];
                        if (t88On)
                            placements.Add(Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId);
                        if (t107On)
                            placements.Add(Id65RemotePadGrassT107Catalog.PlacementId);
                        Id65AuthoringEditorDocument matrixDocument =
                            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                                position.PositionId,
                                includeOptionalTerrain: terrainOn,
                                placementIds: placements,
                                mandatoryBaseFaceTextureId: mandatoryTextureId);
                        bool supported = !t88On && mandatoryTextureId == 66;
                        Id65AuthoringEditorDocumentStoreResult matrixStored;
                        if (supported)
                        {
                            matrixStored = await Id65AuthoringEditorDocumentStore.SaveAsync(
                                paths,
                                manifest,
                                matrixDocument,
                                new Id65AuthoringEditorDocumentProjectionInputs(null),
                                expectedPriorEnvelope);
                            expectedPriorEnvelope = matrixStored.EnvelopeCanonicalSha256;
                        }
                        else
                        {
                            matrixStored = stores[position.PositionId] with
                            {
                                Document = matrixDocument
                            };
                        }
                        try
                        {
                            Id65AuthoringRemotePadAggregatePlan matrixPlan =
                                Id65AuthoringRemotePadAggregatePlanProjector.Project(new(
                                    matrixStored,
                                    manifest,
                                    nativeInputs));
                            if (!supported)
                            {
                                throw new InvalidOperationException(
                                    "An unsupported schema-3 matrix state projected.");
                            }
                            Id65AuthoringRemotePadAggregatePlan repeat =
                                Id65AuthoringRemotePadAggregatePlanProjector.Project(new(
                                    matrixStored with
                                    {
                                        Path = Path.Combine(
                                            Path.GetTempPath(),
                                            "ignored-schema3-matrix-store-path",
                                            Id65AuthoringEditorDocumentStore.FileName),
                                        Document = Id65AuthoringEditorDocument.Parse(
                                            matrixDocument.SerializeUtf8())
                                    },
                                    manifest,
                                    nativeInputs));
                            int expectedOwnedRangeCount = t107On ? 7_960 : 7_956;
                            int expectedProtectedRangeCount = t107On ? 7_961 : 7_957;
                            int expectedGuardedByteCount = t107On ? 1_010_952 : 1_010_852;
                            int expectedProtectedByteCount = t107On ? 2_011_896 : 2_011_996;
                            Id65AuthoringRemotePadAggregatePlanStateFrozenPin stateFrozen =
                                Id65AuthoringRemotePadAggregatePlanProjector
                                    .GetRequiredStateFrozenPin(
                                        terrainOn,
                                        t107On,
                                        position.PositionId);
                            Require(
                                Equivalent(matrixPlan, repeat) &&
                                matrixPlan.StructuralTerrainPresent == terrainOn &&
                                matrixPlan.GrassT107Present == t107On &&
                                matrixPlan.Placement.Present == t107On &&
                                matrixPlan.Placement.PositionId == position.PositionId &&
                                matrixPlan.OwnedRangeCount == expectedOwnedRangeCount &&
                                matrixPlan.InverseRangeCount == expectedOwnedRangeCount &&
                                matrixPlan.ProtectedRangeCount == expectedProtectedRangeCount &&
                                matrixPlan.GuardedByteCount == expectedGuardedByteCount &&
                                matrixPlan.ProtectedByteCount == expectedProtectedByteCount &&
                                stateFrozen.StructuralTerrainPresent == terrainOn &&
                                stateFrozen.GrassT107Present == t107On &&
                                stateFrozen.PositionId == position.PositionId &&
                                stateFrozen.EnvelopeCanonicalSha256 ==
                                    matrixPlan.EnvelopeCanonicalSha256 &&
                                stateFrozen.StaticAuthoringProjectionSha256 ==
                                    matrixPlan.StaticAuthoringProjectionSha256 &&
                                stateFrozen.OutputRow80Sha256 == matrixPlan.OutputRow80Sha256 &&
                                stateFrozen.OwnedRangeMapSha256 ==
                                    matrixPlan.OwnedRangeMapSha256 &&
                                stateFrozen.InverseRangeMapSha256 ==
                                    matrixPlan.InverseRangeMapSha256 &&
                                stateFrozen.ProtectedRangeMapSha256 ==
                                    matrixPlan.ProtectedRangeMapSha256 &&
                                stateFrozen.AtomicGroupMapSha256 ==
                                    matrixPlan.AtomicGroupMapSha256 &&
                                stateFrozen.DeterministicAggregatePlanSha256 ==
                                    matrixPlan.DeterministicAggregatePlanSha256,
                                "A supported schema-3 aggregate state lost exact identity.");
                            if (terrainOn && t107On)
                            {
                                Require(
                                    matrixPlan.DeterministicAggregatePlanSha256 ==
                                        plans[position.PositionId]
                                            .DeterministicAggregatePlanSha256,
                                    "The original aggregate plan changed during expansion.");
                            }
                            byte[] matrixSource = nativeInputs.LockedSource.CopyRow80();
                            byte[] matrixOutput =
                                Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
                                    matrixPlan,
                                    matrixSource,
                                    reverse: false);
                            byte[] matrixRestored =
                                Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
                                    matrixPlan,
                                    matrixOutput,
                                    reverse: true);
                            Require(
                                Hash(matrixOutput) == matrixPlan.OutputRow80Sha256 &&
                                matrixRestored.SequenceEqual(matrixSource),
                                "A supported schema-3 aggregate state failed exact apply/rollback.");
                            string stateKey =
                                (terrainOn ? "terrain-on" : "terrain-off") + "|" +
                                (t107On ? "grass-on" : "grass-off") + "|" +
                                position.PositionId;
                            Require(matrixStateKeys.Add(stateKey),
                                "A supported aggregate state key appeared twice.");
                            matrixOutputHashes.Add(matrixPlan.OutputRow80Sha256);
                            Require(matrixAggregateHashes.Add(
                                    matrixPlan.DeterministicAggregatePlanSha256),
                                "Two supported aggregate states shared a deterministic plan.");
                            if (emitStatePins && !(terrainOn && t107On))
                            {
                                Console.WriteLine(string.Join('|',
                                [
                                    "STATE_PLAN_PIN",
                    terrainOn ? "true" : "false",
                    t107On ? "true" : "false",
                    position.PositionId,
                    matrixPlan.EnvelopeCanonicalSha256,
                    matrixPlan.StaticAuthoringProjectionSha256,
                    matrixPlan.OutputRow80Sha256,
                    matrixPlan.OwnedRangeMapSha256,
                    matrixPlan.InverseRangeMapSha256,
                    matrixPlan.ProtectedRangeMapSha256,
                    matrixPlan.AtomicGroupMapSha256,
                    matrixPlan.DeterministicAggregatePlanSha256
                                ]));
                            }
                            positivePlans += 2;
                            matrixSupported++;
                        }
                        catch (Exception exception) when (
                            !supported && exception is InvalidDataException or NotSupportedException)
                        {
                            matrixRejected++;
                        }
                    }
    Require(matrixStates == 144 && matrixSupported == 36 &&
            matrixRejected == 108 && matrixStateKeys.Count == 36 &&
            matrixOutputHashes.Count == 20 && matrixAggregateHashes.Count == 36,
        "The schema-3 aggregate matrix changed from 36 supported / 108 rejected.");

    Id65AuthoringEditorDocumentStoreResult centerStored =
        stores[Id65RemotePadGrassT107Catalog.CenterPositionId];
    void RejectIdentity(
        string label,
        Id65AuthoringEditorDocumentStoreResult? selectedStored = null,
        UnusedLevel65BlankLevelLabManifest? selectedManifest = null,
        Id65AuthoringEditorNativeInputProvider.InputSet? selectedInputs = null)
    {
        Reject(label, () =>
            _ = Id65AuthoringRemotePadAggregatePlanProjector.Project(new(
                selectedStored ?? centerStored,
                selectedManifest ?? manifest,
                selectedInputs ?? nativeInputs)));
        identityRejects++;
    }

    RejectIdentity("envelope identity",
        centerStored with { EnvelopeCanonicalSha256 = new string('0', 64) });
    RejectIdentity("projection contract",
        centerStored with { ProjectionContractId = "forged" });
    RejectIdentity("static profile",
        centerStored with { StaticAuthoringProfileId = "forged" });
    RejectIdentity("static catalog",
        centerStored with { StaticAuthoringCatalogSha256 = new string('0', 64) });
    RejectIdentity("static scene graph",
        centerStored with { StaticAuthoringSceneGraphSha256 = new string('0', 64) });
    RejectIdentity("static projection",
        centerStored with { StaticAuthoringProjectionSha256 = new string('0', 64) });
    RejectIdentity("forged composite profile",
        centerStored with { CompositeProfileId = "forged" });
    RejectIdentity("position/store cross-pair",
        centerStored with
        {
            Document = stores[Id65RemotePadGrassT107Catalog.NorthPositionId].Document
        });
    RejectIdentity("workspace key", selectedManifest:
        manifest with { WorkspaceKey = "forged" });
    RejectIdentity("normal Create BIN manifest", selectedManifest:
        manifest with { NormalCreateBinEnabled = true });
    Id65AuthoringEditorNativeInputProvider.InputSet forgedInputs =
        ForgeProviderInputSetForNegativeTest(
            nativeInputs.BoundSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle,
            new string('0', 64));
    RejectIdentity("native input generation", selectedInputs: forgedInputs);

    byte[] centerSource = nativeInputs.LockedSource.CopyRow80();
    byte[] centerOutput =
        Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
            center,
            centerSource,
            reverse: false);
    byte[] forgedForward = centerSource.ToArray();
    forgedForward[0x400] ^= 1;
    Reject("forged forward whole-row preimage", () =>
        Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
            center, forgedForward, reverse: false));
    partitionRejects++;
    byte[] forgedReverse = centerOutput.ToArray();
    forgedReverse[0x400] ^= 1;
    Reject("forged reverse whole-row afterimage", () =>
        Id65AuthoringRemotePadAggregatePlanProjector.ApplyExact(
            center, forgedReverse, reverse: true));
    partitionRejects++;

    Id65AuthoringEditorAggregateOwnedRange[] exactOwned =
        center.OwnedRanges.Select(item => item.DeepCopy()).ToArray();
    void RejectPartition(
        string label,
        Id65AuthoringEditorAggregateOwnedRange[] selected)
    {
        Reject(label, () =>
            Id65AuthoringRemotePadAggregatePlanProjector.ValidatePartitionForSmoke(
                centerSource,
                centerOutput,
                selected));
        partitionRejects++;
    }
    Id65AuthoringEditorAggregateOwnedRange[] duplicate = CopyOwned(exactOwned);
    duplicate[1] = CloneRange(duplicate[1], stableId: duplicate[0].StableId);
    RejectPartition("duplicate stable ID", duplicate);
    Id65AuthoringEditorAggregateOwnedRange[] overlap = CopyOwned(exactOwned);
    overlap[1] = CloneRange(
        overlap[1],
        dataRelativeOffset: overlap[0].DataRelativeOffset);
    RejectPartition("overlapping range", overlap);
    Id65AuthoringEditorAggregateOwnedRange[] badBefore = CopyOwned(exactOwned);
    badBefore[0] = CloneRange(
        badBefore[0],
        before: Flip(badBefore[0].CopyBefore()));
    RejectPartition("forged range preimage", badBefore);
    Id65AuthoringEditorAggregateOwnedRange[] badAfter = CopyOwned(exactOwned);
    badAfter[0] = CloneRange(
        badAfter[0],
        after: Flip(badAfter[0].CopyAfter()));
    RejectPartition("forged range afterimage", badAfter);

    byte[] ownedBeforeCopy = center.OwnedRanges[0].CopyBefore();
    ownedBeforeCopy[0] ^= 1;
    Require(Hash(center.OwnedRanges[0].CopyBefore()) ==
        center.OwnedRanges[0].BeforeSha256,
        "The aggregate plan leaked mutable owned preimage bytes.");
    aliasChecks++;
    byte[] inverseAfterCopy = center.InverseRanges[0].CopyAfter();
    inverseAfterCopy[0] ^= 1;
    Require(Hash(center.InverseRanges[0].CopyAfter()) ==
        center.InverseRanges[0].AfterSha256,
        "The aggregate plan leaked mutable inverse bytes.");
    aliasChecks++;
    byte[] documentCopy = center.Document.SerializeUtf8();
    documentCopy[0] ^= 1;
    Require(center.Document.CanonicalSha256 == center.DocumentCanonicalSha256,
        "The aggregate plan leaked mutable document bytes.");
    aliasChecks++;
    byte[] boundCopy = nativeInputs.LockedSource.CopyRow80();
    boundCopy[0] ^= 1;
    Require(nativeInputs.LockedSource.Row80Sha256 ==
        Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256,
        "The provider leaked mutable locked-source bytes.");
    aliasChecks++;

    string[] falseProperties =
    [
        "CurrentOnDiskFreshnessVerified",
        "StoreEnvelopeIdentityIndependentlyVerified",
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
        "HostileFilesystemRaceSafe",
        "CommonWorkspaceLeaseOwned"
    ];
    foreach (string propertyName in falseProperties)
    {
        PropertyInfo property =
            typeof(Id65AuthoringRemotePadAggregatePlan).GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                "Missing schema-3 fail-closed property " + propertyName + ".");
        Require(property.PropertyType == typeof(bool) &&
                property.GetValue(center) is false,
            "The schema-3 aggregate plan enabled " + propertyName + ".");
    }
    Require(typeof(Id65AuthoringRemotePadAggregatePlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.Name != "Path" &&
                property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType)),
        "The schema-3 aggregate plan exposed a path, stream, or byte array.");
    Require(typeof(Id65AuthoringRemotePadAggregatePlanRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
                !typeof(Delegate).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType !=
                    typeof(Id65CompiledAuthoringRemotePadComposite) &&
                property.PropertyType !=
                    typeof(Id65AuthoringRemotePadAggregatePlan)),
        "The schema-3 aggregate request gained caller afterimages or plans.");
    VerifySourceTopology(repositoryRoot);

    Require(positivePlans == 90 && identityRejects == 11 &&
            partitionRejects == 6 && aliasChecks == 4,
        "The schema-3 aggregate positive/rejection/copy matrix changed.");
    Console.WriteLine(
        "ID65_AUTHORING_REMOTE_PAD_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
        "positions=9 positive_plans=" + positivePlans + " " +
        "matrix_states=" + matrixStates + " " +
        "matrix_supported=" + matrixSupported + " " +
        "matrix_rejected=" + matrixRejected + " " +
        "matrix_output_hashes=" + matrixOutputHashes.Count + " " +
        "matrix_aggregate_hashes=" + matrixAggregateHashes.Count + " " +
        "identity_rejects=" + identityRejects + " " +
        "partition_rejects=" + partitionRejects + " " +
        "alias_checks=" + aliasChecks + " " +
        "owned_ranges=7956_or_7960 protected_ranges=7957_or_7961 " +
        "filesystem=false app=false create_bin=false runtime=false release=false");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

FileState lockedAfter = SnapshotFile(lockedImagePath);
IReadOnlyList<string> lockedDirectoryAfter =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
Require(lockedBefore == lockedAfter &&
        lockedDirectoryBefore.SequenceEqual(lockedDirectoryAfter),
    "The schema-3 aggregate-plan smoke changed its locked fixture.");

static bool Equivalent(
    Id65AuthoringRemotePadAggregatePlan left,
    Id65AuthoringRemotePadAggregatePlan right) =>
    left.ProfileId == right.ProfileId &&
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
    left.StaticAuthoringProjectionSha256 ==
        right.StaticAuthoringProjectionSha256 &&
    left.NativeInputSetSha256 == right.NativeInputSetSha256 &&
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
    left.Destination == right.Destination &&
    left.ProtectedRanges.SequenceEqual(right.ProtectedRanges) &&
    left.AtomicGroups.SequenceEqual(right.AtomicGroups) &&
    left.OwnedRanges.Select(ToRangeTuple)
        .SequenceEqual(right.OwnedRanges.Select(ToRangeTuple)) &&
    left.InverseRanges.Select(ToRangeTuple)
        .SequenceEqual(right.InverseRanges.Select(ToRangeTuple));

static (string, string, Id65AuthoringEditorAggregateRangeDomain, int, int, long,
    int, string, string) ToRangeTuple(
        Id65AuthoringEditorAggregateOwnedRange range) =>
    (range.StableId, range.OwnerId, range.Domain, range.TargetWadEntry,
        range.DataRelativeOffset, range.WadOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static Id65AuthoringEditorAggregateOwnedRange[] CopyOwned(
    IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ranges) =>
    ranges.Select(item => item.DeepCopy()).ToArray();

static Id65AuthoringEditorAggregateOwnedRange CloneRange(
    Id65AuthoringEditorAggregateOwnedRange source,
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
        "Expected fail-closed schema-3 aggregate rejection: " + label + ".");
}

static void VerifySourceTopology(string repositoryRoot)
{
    string[] files = Directory.EnumerateFiles(
            Path.Combine(repositoryRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .ToArray();
    Regex declaration = new(
        @"\binternal\s+static\s+class\s+Id65AuthoringRemotePadAggregatePlanProjector\b",
        RegexOptions.CultureInvariant);
    Regex call = new(
        @"\bId65AuthoringRemotePadAggregatePlanProjector\s*\.\s*Project\s*\(",
        RegexOptions.CultureInvariant);
    string[] declarations = files.Where(path => declaration.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] calls = files.Where(path => call.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(declarations.SequenceEqual(
        [
            Path.Combine(
                "src",
                "Spyro.Editor.Core",
                "Persistence",
                "Id65AuthoringRemotePadAggregatePlanProjector.cs")
        ]) &&
        calls.SequenceEqual(
        [
            Path.Combine(
                "src",
                "Spyro.Editor.Core",
                "Persistence",
                "Id65AuthoringRemotePadAggregateCandidateMaterializer.cs"),
            Path.Combine(
                "src",
                "Spyro.Editor.Id65AuthoringRemotePadAggregatePlanProjectorSmoke",
                "Program.cs")
        ]),
        "The schema-3 aggregate projector declaration/call topology changed.");
}

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

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
    throw new DirectoryNotFoundException("Could not locate the repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    return new(file.Length, file.LastWriteTimeUtc.Ticks, Hash(File.ReadAllBytes(path)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
