using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedImagePath), $"Missing exact aggregate locked image: {lockedImagePath}");
Require(File.Exists(foundationImagePath), $"Missing exact aggregate donor image: {foundationImagePath}");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
UnusedLevel65NativeTextureStaticPlan oldTextureProof =
    UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
UnusedLevel65MobyDependencyBundleContract mobyContract =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
byte[] package = ReadRootFile(foundationImagePath, "WAD.WAD", 0x9C1D20, 0x174);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(mobyContract, package);
Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.CreateExact();
UnusedLevel65BlankLevelLabWorkspacePaths labPaths =
    UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
        Path.Combine(Path.GetTempPath(), "id65-aggregate-projector-smoke-logical-workspace"));
UnusedLevel65BlankLevelLabManifest labManifest =
    UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(labPaths);

Id65AuthoringEditorProjection storedProjection =
    Id65AuthoringEditorDocumentProjector.Project(new(
        document,
        boundSource.LockedSource,
        textureWitness,
        grassBundle));
Id65CompiledAuthoringComposite storedCompiled = storedProjection.Compiled;
Id65AuthoringEditorDocumentStoreResult stored = new(
    Path.Combine(labPaths.AuthoredEditsDirectoryPath, Id65AuthoringEditorDocumentStore.FileName),
    document,
    Hash(Encoding.UTF8.GetBytes("aggregate-smoke-validated-store-envelope")),
    Id65AuthoringEditorDocumentStore.ProjectionContractId,
    storedCompiled.ProfileId,
    storedCompiled.ManifestSha256,
    storedCompiled.OutputRow80Sha256,
    storedCompiled.DeterministicPlanSha256);

int positivePlans = 0;
int identityRejects = 0;
int partitionRejects = 0;
int aliasCopyChecks = 0;

Id65AuthoringEditorAggregatePlan Project(Id65AuthoringEditorDocumentStoreResult selectedStored) =>
    Id65AuthoringEditorAggregatePlanProjector.Project(new(
        selectedStored,
        labManifest,
        boundSource,
        textureWitness,
        grassBundle));

Id65AuthoringEditorAggregatePlan first = Project(stored);
positivePlans++;
Id65AuthoringEditorDocument reparsedDocument =
    Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
Id65AuthoringEditorDocumentStoreResult pathAlias = stored with
{
    Path = Path.Combine(Path.GetTempPath(), "ignored-store-path-alias", Id65AuthoringEditorDocumentStore.FileName),
    Document = reparsedDocument
};
Id65AuthoringEditorAggregatePlan repeat = Project(pathAlias);
positivePlans++;
Require(Equivalent(first, repeat), "Repeated/reparsed/path-aliased aggregate projection was not deterministic.");

byte[] sourceRow = boundSource.LockedSource.CopyRow80();
byte[] outputRow = Id65AuthoringEditorAggregatePlanProjector.ApplyExact(first, sourceRow, reverse: false);
byte[] restoredRow = Id65AuthoringEditorAggregatePlanProjector.ApplyExact(first, outputRow, reverse: true);
Require(Hash(outputRow) == first.OutputRow80Sha256 && restoredRow.SequenceEqual(sourceRow),
    "The aggregate all-ranges-only transaction did not round-trip exactly.");
Id65AuthoringEditorAggregatePlanProjector.ValidatePartitionForSmoke(
    sourceRow,
    outputRow,
    first.OwnedRanges);

Require(first.Destination.SourceSnapshotProfileId == Id65RewardCensusContract.ProfileId &&
        first.Destination.SourceImageSha256 == Id65RewardCensusContract.LockedSourceImageSha256 &&
        first.Destination.TargetDataWadEntry == 80 &&
        first.Destination.TargetDataWadOffset == 0x6936800 &&
        first.Destination.TargetDataByteLength == 0x2E2000 &&
        first.Destination.TargetDataDirectoryRowSha256 ==
            "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a" &&
        first.Destination.RejectedRetailDataWadEntry == 16 &&
        first.Destination.RejectedRetailDataWadOffset == 0x119E000 &&
        first.Destination.RejectedRetailDataDirectoryRowSha256 ==
            "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b" &&
        first.Destination.TargetDataWadOffset != first.Destination.RejectedRetailDataWadOffset &&
        first.Destination.PhysicalSnapshotVerified &&
        first.Destination.RetailTownSquareDestinationRejected &&
        first.Destination.DonorLocationsReadOnlyProvenance &&
        first.Destination.PhysicalDestinationRevalidationRequired &&
        first.OwnedRanges.All(item =>
            item.TargetWadEntry == 80 && item.WadOffset == 0x6936800L + item.DataRelativeOffset),
    "The aggregate transaction lost its physical WAD80 destination or retail-WAD16 exclusion.");
Require(first.OwnedRangeCount == 7_961 && first.InverseRangeCount == 7_961 &&
        first.OwnedRanges.Count(item =>
            item.Domain == Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages) == 7_949 &&
        first.OwnedRanges.Count(item =>
            item.Domain == Id65AuthoringEditorAggregateRangeDomain.CompositeModel) == 1 &&
        first.OwnedRanges.Count(item =>
            item.Domain == Id65AuthoringEditorAggregateRangeDomain.Spawn) == 2 &&
        first.OwnedRanges.Count(item =>
            item.Domain == Id65AuthoringEditorAggregateRangeDomain.Grass) == 9 &&
        first.GuardedByteCount == 1_011_040 && first.ProtectedByteCount == 2_011_808 &&
        first.GuardedByteCount + first.ProtectedByteCount == 0x2E2000 &&
        first.ProtectedRangeCount ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedProtectedRangeCount &&
        first.ProtectedRanges.Sum(item => item.ByteLength) == first.ProtectedByteCount &&
        first.InverseRangeMapSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedInverseRangeMapSha256 &&
        first.ProtectedRangeMapSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedProtectedRangeMapSha256 &&
        first.ProtectedComplementSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedProtectedComplementSha256 &&
        first.ProtectedModelComponentMapSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedProtectedModelComponentMapSha256 &&
        first.SemanticReadbackSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedSemanticReadbackSha256 &&
        first.BaselineOwnershipSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedBaselineOwnershipSha256 &&
        first.AtomicGroupMapSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedAtomicGroupMapSha256 &&
        first.DeterministicAggregatePlanSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedDeterministicAggregatePlanSha256 &&
        first.ProtectedModelComponents.Select(item => item.StableId).SequenceEqual(new[]
        {
            "component.special-surface",
            "component.cyclorama",
            "component.portal-table",
            "component.particles",
            "component.sound"
        }),
    "The aggregate owned/protected/inner-model partition changed.");
Require(first.AtomicGroups.Select(item => item.OwnedRangeCount).SequenceEqual([7_950, 2, 9]) &&
        first.AtomicGroups.All(item => item.AllRangesRequired && !item.IndependentlyApplicable) &&
        first.BaselineOwnership.Count(item =>
            item.CompiledTreatment == Id65AuthoringEditorAggregateCompiledTreatment.PreserveLockedBase &&
            item.EditorEditPolicy == Id65AuthoringEditorAggregateEditPolicy.Blocked) == 4 &&
        first.BaselineOwnership.Single(item => item.StableId == "spawn.remote-pad").CompiledTreatment ==
            Id65AuthoringEditorAggregateCompiledTreatment.AuthorExact &&
        first.BaselineOwnership.Where(item => item.StableId != "spawn.remote-pad")
            .Select(item => item.StableId)
            .SequenceEqual(["music.slot35", "totals.slot65", "exit.slot65", "save.slot65"]),
    "The aggregate atomic groups or spawn/preserved-baseline policy changed.");
Require(first.Terrain.OptionalTrianglePresent && first.Terrain.OptionalTriangle is not null &&
        first.Terrain.SectorIndex == 216 && first.Terrain.LowDetailVertexCount == 4 &&
        first.Terrain.LowDetailFaceCount == 2 && first.Terrain.HighDetailVertexCount == 4 &&
        first.Terrain.HighDetailFaceCount == 2 &&
        first.Terrain.OptionalTriangle.CollisionTriangleIndex == 19_808 &&
        first.Terrain.OcclusionGroupIndex == 0 && first.Terrain.OcclusionContainsSector &&
        first.Texture.PrivateTextureId == 66 && first.Texture.OutputTextureId == 66 &&
        first.Texture.DonorWadEntry == 70 && first.Texture.DonorTextureId == 12 &&
        first.Texture.AddedTerrainFacePreservedT25 &&
        first.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual([88, 107]) &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 88).RowSha256 ==
            "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f" &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 107).RowSha256 ==
            "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb",
    "The aggregate HP/LP/collision/occlusion/T66/T88/T107 semantic readback changed.");

void RejectIdentity(
    string label,
    Id65AuthoringEditorDocumentStoreResult selectedStored,
    UnusedLevel65BlankLevelLabManifest? selectedLab = null,
    Id65AuthoringEditorAggregateBoundSource? selectedSource = null,
    Id65V2NativeTextureWitness? selectedWitness = null,
    Id65MobyDependencyBundleDescriptor? selectedBundle = null)
{
    ExpectRejected(label, () => _ = Id65AuthoringEditorAggregatePlanProjector.Project(new(
        selectedStored,
        selectedLab ?? labManifest,
        selectedSource ?? boundSource,
        selectedWitness ?? textureWitness,
        selectedBundle ?? grassBundle)));
    identityRejects++;
}

RejectIdentity("projection contract", stored with { ProjectionContractId = "forged" });
RejectIdentity("composite profile", stored with { CompositeProfileId = "forged" });
RejectIdentity("composite manifest", stored with { CompositeManifestSha256 = new string('0', 64) });
RejectIdentity("composite output", stored with { CompositeOutputRow80Sha256 = new string('0', 64) });
RejectIdentity("composite deterministic plan",
    stored with { CompositeDeterministicPlanSha256 = new string('0', 64) });
RejectIdentity("noncanonical envelope hash",
    stored with { EnvelopeCanonicalSha256 = stored.EnvelopeCanonicalSha256.ToUpperInvariant() });
RejectIdentity("missing terrain", stored with
{
    Document = Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false)
});
RejectIdentity("T88-only document", stored with
{
    Document = Id65AuthoringEditorDocument.CreateExact(
        placementIds: [Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId])
});
RejectIdentity("T107-only document", stored with
{
    Document = Id65AuthoringEditorDocument.CreateExact(
        placementIds: [Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId])
});

RejectIdentity("lab profile", stored, labManifest with { ProfileId = "forged" });
RejectIdentity("lab version", stored, labManifest with { ProfileVersion = 2 });
RejectIdentity("workspace key", stored, labManifest with { WorkspaceKey = "forged" });
RejectIdentity("layout version", stored, labManifest with { WorkspaceLayoutVersion = 2 });
RejectIdentity("layout root", stored, labManifest with { WorkspaceLayoutRelativeRoot = "forged" });
RejectIdentity("level id", stored, labManifest with { LevelId = 16 });
RejectIdentity("target WAD entry", stored, labManifest with { DataWadEntry = 16 });
RejectIdentity("locked profile", stored, labManifest with { LockedBaseProfileId = "forged" });
RejectIdentity("locked image", stored, labManifest with { LockedBaseImageSha256 = new string('0', 64) });
RejectIdentity("lab capability", stored, labManifest with
{
    Capabilities = labManifest.Capabilities with
    {
        NormalCreateBin = UnusedLevel65BlankLevelLabCapabilityState.ResearchOnly
    }
});
RejectIdentity("lab promotion", stored, labManifest with { PromotionAuthorized = true });
RejectIdentity("lab Create BIN", stored, labManifest with { NormalCreateBinEnabled = true });

void RejectPhysicalCapture(string label, string selectedPath)
{
    ExpectRejected(label, () => _ = Id65AuthoringEditorAggregateBoundSource.Capture(selectedPath));
    identityRejects++;
}

RejectPhysicalCapture("missing absolute bound source",
    Path.Combine(Path.GetTempPath(), "id65-aggregate-missing-physical-source.bin"));
RejectPhysicalCapture("wrong physical image", foundationImagePath);

RejectIdentity("T66 declared identity", stored, selectedWitness:
    CopyWitness(textureWitness, canonicalSha256: new string('0', 64)));
RejectIdentity("Grass declared identity", stored, selectedBundle:
    CopyBundle(grassBundle, canonicalSha256: new string('0', 64)));
IReadOnlyList<Id65V2NativeTexturePagePatch> pagePatches = textureWitness.CopyPagePatches();
byte[] badPageAfter = Flip(pagePatches[0].CopyAfter(), 0);
Id65V2NativeTexturePagePatch badPage = new(
    pagePatches[0].RelativeOffset,
    pagePatches[0].CopyBefore(),
    badPageAfter,
    pagePatches[0].BeforeSha256,
    Hash(badPageAfter),
    pagePatches[0].Owner);
RejectIdentity("T66 full-field page tamper", stored, selectedWitness:
    CopyWitness(textureWitness,
        pagePatches: [badPage, .. pagePatches.Skip(1)],
        canonicalSha256: textureWitness.CanonicalSha256));
RejectIdentity("Grass full-field canonical JSON tamper", stored, selectedBundle:
    CopyBundle(grassBundle,
        canonicalJson: "{}",
        canonicalSha256: grassBundle.CanonicalSha256));

Id65AuthoringEditorAggregateOwnedRange[] exactOwned =
    first.OwnedRanges.Select(item => item.DeepCopy()).ToArray();
void RejectPartition(
    string label,
    byte[]? selectedSource = null,
    byte[]? selectedOutput = null,
    IEnumerable<Id65AuthoringEditorAggregateOwnedRange>? selectedOwned = null)
{
    ExpectRejected(label, () =>
        Id65AuthoringEditorAggregatePlanProjector.ValidatePartitionForSmoke(
            selectedSource ?? sourceRow,
            selectedOutput ?? outputRow,
            selectedOwned ?? exactOwned));
    partitionRejects++;
}

RejectPartition("source length", selectedSource: sourceRow[..^1]);
RejectPartition("output length", selectedOutput: outputRow[..^1]);
RejectPartition("missing owned range", selectedOwned: exactOwned[..^1]);
Id65AuthoringEditorAggregateOwnedRange[] duplicate = CopyOwned(exactOwned);
duplicate[1] = CloneRange(duplicate[1], stableId: duplicate[0].StableId);
RejectPartition("duplicate stable ID", selectedOwned: duplicate);
Id65AuthoringEditorAggregateOwnedRange[] overlap = CopyOwned(exactOwned);
overlap[1] = CloneRange(overlap[1], dataRelativeOffset: overlap[0].DataRelativeOffset);
RejectPartition("overlap", selectedOwned: overlap);
Id65AuthoringEditorAggregateOwnedRange[] empty = CopyOwned(exactOwned);
empty[0] = new(
    empty[0].StableId,
    empty[0].OwnerId,
    empty[0].Domain,
    empty[0].DataRelativeOffset,
    [],
    []);
RejectPartition("zero-length range", selectedOwned: empty);
Id65AuthoringEditorAggregateOwnedRange[] shifted = CopyOwned(exactOwned);
shifted[0] = CloneRange(shifted[0], dataRelativeOffset: shifted[0].DataRelativeOffset + 1);
RejectPartition("shifted range", selectedOwned: shifted);
Id65AuthoringEditorAggregateOwnedRange[] ownerDrift = CopyOwned(exactOwned);
ownerDrift[0] = CloneRange(ownerDrift[0], ownerId: "forged-owner");
RejectPartition("owner drift", selectedOwned: ownerDrift);
Id65AuthoringEditorAggregateOwnedRange[] domainDrift = CopyOwned(exactOwned);
int textureRangeIndex = Array.FindIndex(domainDrift, item =>
    item.Domain == Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages);
Require(textureRangeIndex >= 0, "The aggregate smoke could not locate a texture range for domain tampering.");
domainDrift[textureRangeIndex] = CloneRange(
    domainDrift[textureRangeIndex],
    domain: Id65AuthoringEditorAggregateRangeDomain.Grass);
RejectPartition("domain drift", selectedOwned: domainDrift);
Id65AuthoringEditorAggregateOwnedRange[] afterDrift = CopyOwned(exactOwned);
afterDrift[0] = CloneRange(afterDrift[0], after: Flip(afterDrift[0].CopyAfter(), 0));
RejectPartition("owned afterimage drift", selectedOwned: afterDrift);
byte[] protectedDrift = outputRow.ToArray();
protectedDrift[first.ProtectedRanges[0].DataRelativeOffset] ^= 1;
RejectPartition("protected complement drift", selectedOutput: protectedDrift);

byte[] boundRowCopy = boundSource.LockedSource.CopyRow80();
byte[] boundOverlayCopy = boundSource.LockedSource.CopyTargetOverlay();
byte[] boundExecutableCopy = boundSource.LockedSource.CopyGlobalExecutable();
boundRowCopy[0] ^= 1;
boundOverlayCopy[0] ^= 1;
boundExecutableCopy[0] ^= 1;
Require(Hash(boundSource.LockedSource.CopyRow80()) ==
            Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        Hash(boundSource.LockedSource.CopyTargetOverlay()) ==
            Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
        Hash(boundSource.LockedSource.CopyGlobalExecutable()) ==
            Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256,
    "The aggregate bound source exposed a mutable physical snapshot alias.");
aliasCopyChecks++;

byte[] ownedBeforeCopy = first.OwnedRanges[0].CopyBefore();
ownedBeforeCopy[0] ^= 1;
Require(Hash(first.OwnedRanges[0].CopyBefore()) == first.OwnedRanges[0].BeforeSha256,
    "An aggregate owned preimage copy aliased plan state.");
aliasCopyChecks++;
byte[] ownedAfterCopy = first.OwnedRanges[0].CopyAfter();
ownedAfterCopy[0] ^= 1;
Require(Hash(first.OwnedRanges[0].CopyAfter()) == first.OwnedRanges[0].AfterSha256,
    "An aggregate owned afterimage copy aliased plan state.");
aliasCopyChecks++;
byte[] inverseBeforeCopy = first.InverseRanges[0].CopyBefore();
inverseBeforeCopy[0] ^= 1;
Require(Hash(first.InverseRanges[0].CopyBefore()) == first.InverseRanges[0].BeforeSha256,
    "An aggregate inverse preimage copy aliased plan state.");
aliasCopyChecks++;
byte[] serializedCopy = document.SerializeUtf8();
serializedCopy[0] ^= 1;
Require(document.CanonicalSha256 == Id65AuthoringEditorAggregatePlanProjector.ExpectedFullDocumentCanonicalSha256,
    "The aggregate document serialization retained a mutable alias.");
aliasCopyChecks++;
Require(!ReferenceEquals(first.OwnedRanges[0], repeat.OwnedRanges[0]) &&
        !ReferenceEquals(first.InverseRanges[0], repeat.InverseRanges[0]),
    "Repeated aggregate plans reused owned-range objects.");
aliasCopyChecks++;
Require(first.HandleRebases.Single(item => item.StableId == "terrain.remote-pad.face.0:hp").Output ==
            first.HandleRebases.Single(item => item.StableId == "terrain.remote-pad.face.0:lp").Output &&
        first.HandleRebases.Any(item => item.StableId == "terrain.remote-pad.face.0:hp") &&
        first.HandleRebases.Any(item => item.StableId == "terrain.remote-pad.face.0:lp"),
    "HP/LP handles collapsed despite their intentionally shared logical address.");
aliasCopyChecks++;
Require(first.BaselineOwnership.Where(item => item.Output.Space == "level-slot" && item.Output.Primary == 65)
            .Select(item => (item.StableId, item.Kind)).Distinct().Count() == 3,
    "Totals/exit/save aliases collapsed at shared level slot 65.");
aliasCopyChecks++;
Require(first.MobyPlacements.Select(item => item.TargetTrueIndex).Distinct().Count() == 2 &&
        first.MobyPlacements.Select(item => item.PlacementId).Distinct(StringComparer.Ordinal).Count() == 2,
    "T88/T107 aliases collapsed across their shared Grass bundle.");
aliasCopyChecks++;
Require(typeof(Id65AuthoringEditorAggregatePlan)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .All(property => property.Name != "Path" && property.PropertyType != typeof(Stream) &&
            property.PropertyType != typeof(byte[])),
    "The aggregate plan exposed a path, stream, or whole mutable byte-array property.");
aliasCopyChecks++;

string[] falseProperties =
[
    "StoreEnvelopeIdentityIndependentlyVerified",
    "CurrentOnDiskFreshnessVerified",
    "PartialRangeApplicationAuthorized",
    "CrossSliceCompositionSupported",
    "AfterimageStackingAuthorized",
    "GenericExporterCompatible",
    "RetailWriterCompatible",
    "RetailLevelMutationAuthorized",
    "RuntimeMobyAcceptanceProven",
    "RuntimeProven",
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
    PropertyInfo property = typeof(Id65AuthoringEditorAggregatePlan).GetProperty(propertyName) ??
        throw new InvalidOperationException($"Missing fail-closed aggregate property '{propertyName}'.");
    Require(property.PropertyType == typeof(bool) && property.GetValue(first) is false,
        $"The aggregate plan unexpectedly enabled '{propertyName}'.");
}

MethodInfo[] publicProjectorMethods = typeof(Id65AuthoringEditorAggregatePlanProjector)
    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
Require(publicProjectorMethods.Length == 1 && publicProjectorMethods[0].Name == "Project" &&
        publicProjectorMethods[0].GetParameters().Select(item => item.ParameterType)
            .SequenceEqual([typeof(Id65AuthoringEditorAggregatePlanRequest)]),
    "The aggregate projector gained a public method beyond Project(request).");
Require(typeof(Id65AuthoringEditorAggregatePlanRequest)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .All(property => property.PropertyType != typeof(byte[]) &&
            !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
            !typeof(Delegate).IsAssignableFrom(property.PropertyType) &&
            property.PropertyType != typeof(Id65CompiledAuthoringComposite) &&
            property.PropertyType != typeof(Id65AuthoringEditorProjection) &&
            property.PropertyType != typeof(Id65AuthoringCompositeRollbackPlan) &&
            property.PropertyType != typeof(Id65AuthoringCompositeCompilerLimits)),
    "The aggregate request gained raw bytes, streams, delegates, afterimages, or adjustable limits.");
MethodInfo[] publicBoundSourceFactories = typeof(Id65AuthoringEditorAggregateBoundSource)
    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
Require(publicBoundSourceFactories.Length == 1 && publicBoundSourceFactories[0].Name == "Capture" &&
        publicBoundSourceFactories[0].GetParameters().Select(item => item.ParameterType)
            .SequenceEqual([typeof(string)]),
    "The aggregate bound source gained a caller-constructible snapshot or raw-byte factory.");
VerifySourceTopology(repositoryRoot);

Require(identityRejects == 27, $"The aggregate identity matrix ran {identityRejects}, expected 27.");
Require(partitionRejects == 11, $"The aggregate partition matrix ran {partitionRejects}, expected 11.");
Require(aliasCopyChecks == 10, $"The aggregate copy/alias matrix ran {aliasCopyChecks}, expected 10.");
Require(positivePlans == 2, $"The aggregate positive matrix ran {positivePlans}, expected 2.");
Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(foundationImagePath) == foundationBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
    "The aggregate projector changed or published beside a locked input/donor fixture.");

Console.WriteLine(
    "ID65_AUTHORING_EDITOR_AGGREGATE_PLAN_PROJECTOR_SMOKE_OK " +
    $"positivePlans={positivePlans} identityRejects={identityRejects} " +
    $"partitionRejects={partitionRejects} aliasCopyChecks={aliasCopyChecks} " +
    $"guardedBytes={first.GuardedByteCount} protectedBytes={first.ProtectedByteCount} " +
    $"protectedRanges={first.ProtectedRangeCount} inverse={first.InverseRangeMapSha256} " +
    $"protectedMap={first.ProtectedRangeMapSha256} complement={first.ProtectedComplementSha256} " +
    $"protectedModel={first.ProtectedModelComponentMapSha256} semantic={first.SemanticReadbackSha256} " +
    $"baseline={first.BaselineOwnershipSha256} atomic={first.AtomicGroupMapSha256} " +
    $"aggregate={first.DeterministicAggregatePlanSha256}");

static bool Equivalent(
    Id65AuthoringEditorAggregatePlan left,
    Id65AuthoringEditorAggregatePlan right) =>
    left.ProfileId == right.ProfileId && left.Destination == right.Destination &&
    left.DocumentCanonicalSha256 == right.DocumentCanonicalSha256 &&
    left.CompositeManifestSha256 == right.CompositeManifestSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.SourceModelSha256 == right.SourceModelSha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.InverseRangeMapSha256 == right.InverseRangeMapSha256 &&
    left.ProtectedRangeMapSha256 == right.ProtectedRangeMapSha256 &&
    left.ProtectedComplementSha256 == right.ProtectedComplementSha256 &&
    left.ProtectedModelComponentMapSha256 == right.ProtectedModelComponentMapSha256 &&
    left.SemanticReadbackSha256 == right.SemanticReadbackSha256 &&
    left.BaselineOwnershipSha256 == right.BaselineOwnershipSha256 &&
    left.AtomicGroupMapSha256 == right.AtomicGroupMapSha256 &&
    left.DeterministicAggregatePlanSha256 == right.DeterministicAggregatePlanSha256 &&
    left.ProtectedRanges.SequenceEqual(right.ProtectedRanges) &&
    left.ProtectedModelComponents.SequenceEqual(right.ProtectedModelComponents) &&
    left.HandleRebases.SequenceEqual(right.HandleRebases) &&
    left.MobyAssetRelocations.SequenceEqual(right.MobyAssetRelocations) &&
    left.MobyPlacements.SequenceEqual(right.MobyPlacements) &&
    left.BaselineOwnership.SequenceEqual(right.BaselineOwnership) &&
    left.AtomicGroups.SequenceEqual(right.AtomicGroups) &&
    left.OwnedRanges.Select(ToRangeTuple).SequenceEqual(right.OwnedRanges.Select(ToRangeTuple)) &&
    left.InverseRanges.Select(ToRangeTuple).SequenceEqual(right.InverseRanges.Select(ToRangeTuple));

static (string, string, Id65AuthoringEditorAggregateRangeDomain, int, int, long, int, string, string)
    ToRangeTuple(Id65AuthoringEditorAggregateOwnedRange range) =>
    (range.StableId, range.OwnerId, range.Domain, range.TargetWadEntry,
        range.DataRelativeOffset, range.WadOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static Id65AuthoringEditorAggregateOwnedRange[] CopyOwned(
    IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ranges) =>
    ranges.Select(item => item.DeepCopy()).ToArray();

static Id65AuthoringEditorAggregateOwnedRange CloneRange(
    Id65AuthoringEditorAggregateOwnedRange source,
    string? stableId = null,
    string? ownerId = null,
    Id65AuthoringEditorAggregateRangeDomain? domain = null,
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

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
    IReadOnlyList<Id65V2NativeTexturePackedRow>? packedRows = null,
    IReadOnlyList<Id65V2NativeTexturePagePatch>? pagePatches = null,
    string? canonicalSha256 = null) =>
    new(
        source.ProfileId,
        source.DonorManifestId,
        source.DonorCompleteRecordSha256,
        source.SourceTexturePagesSha256,
        source.OutputTexturePagesSha256,
        source.SourceTextureComponentSha256,
        source.OutputTextureComponentSha256,
        source.SourceTextureCount,
        source.OutputTextureCount,
        source.PrivateTextureId,
        source.DonorWadEntry,
        source.DonorTextureId,
        source.MaterialTemplateTextureId,
        packedRows ?? source.CopyPackedRows(),
        pagePatches ?? source.CopyPagePatches(),
        canonicalSha256 ?? source.CanonicalSha256);

static Id65MobyDependencyBundleDescriptor CopyBundle(
    Id65MobyDependencyBundleDescriptor source,
    string? canonicalJson = null,
    string? canonicalSha256 = null) =>
    new(
        source.Id,
        source.SchemaVersion,
        source.FoundationContractSha256,
        source.Rows,
        source.Properties,
        source.ActorPackages,
        source.ActorRoots,
        source.Fixups,
        source.ExternalDependencies,
        source.BehaviorClosure,
        source.RuntimeDependencies,
        canonicalJson ?? source.CanonicalJson,
        canonicalSha256 ?? source.CanonicalSha256);

static byte[] Flip(byte[] source, int offset)
{
    byte[] result = source.ToArray();
    result[offset] ^= 1;
    return result;
}

static void ExpectRejected(string label, Action action)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (Exception exception) when (exception is InvalidDataException or ArgumentException or IOException)
    {
        rejected = true;
    }
    Require(rejected, $"Aggregate negative case '{label}' was accepted.");
}

static void VerifySourceTopology(string repositoryRoot)
{
    string[] files = Directory
        .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .ToArray();
    Regex declaration = new(
        @"\binternal\s+static\s+class\s+Id65AuthoringEditorAggregatePlanProjector\b",
        RegexOptions.CultureInvariant);
    Regex call = new(
        @"\bId65AuthoringEditorAggregatePlanProjector\s*\.\s*Project\s*\(",
        RegexOptions.CultureInvariant);
    string[] declarations = files.Where(path => declaration.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] calls = files.Where(path => call.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(declarations.SequenceEqual(new[]
        {
            Path.Combine("src", "Spyro.Editor.Core", "Persistence",
                "Id65AuthoringEditorAggregatePlanProjector.cs")
        }) &&
        calls.SequenceEqual(new[]
        {
            Path.Combine("src", "Spyro.Editor.Id65AuthoringEditorAggregatePlanProjectorSmoke", "Program.cs")
        }),
        $"Aggregate projector declaration/call topology changed: defs={string.Join(",", declarations)}; " +
        $"calls={string.Join(",", calls)}.");
}

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

static byte[] ReadRootFile(
    string imagePath,
    string exactName,
    long relativeOffset,
    int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord file = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase));
    if (relativeOffset < 0 || relativeOffset + byteLength > file.Size)
        throw new InvalidDataException("An aggregate smoke source read escaped its root file.");
    return DiscImage.ReadFileBytes(image, layout, file.Lba, relativeOffset, byteLength);
}

static FileState SnapshotFile(string path) =>
    new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), HashFile(path));

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.GetFileSystemEntries(path)
        .Select(entry => $"{Path.GetFileName(entry)}|{new FileInfo(entry).Length}|{File.GetLastWriteTimeUtc(entry):O}")
        .Order(StringComparer.Ordinal)
        .ToArray();

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
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
