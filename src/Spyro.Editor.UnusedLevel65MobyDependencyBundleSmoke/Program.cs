using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
Require(File.Exists(foundationImagePath), $"Missing exact foundation BIN: {foundationImagePath}");

string sourceHashBefore = HashFile(foundationImagePath);
long sourceLengthBefore = new FileInfo(foundationImagePath).Length;
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(foundationImagePath);
IReadOnlyDictionary<string, FileState> directoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);

UnusedLevel65MobyDependencyBundleContract first =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);
UnusedLevel65MobyDependencyBundleContract second =
    UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationImagePath);

Require(first.ProfileId == UnusedLevel65MobyDependencyBundleFoundation.ProfileId &&
        first.SchemaVersion == 2 &&
        first.FoundationImageSha256 == UnusedLevel65MobyDependencyBundleFoundation.ExpectedFoundationImageSha256 &&
        first.LockedIndependentBaseImageSha256 ==
            UnusedLevel65MobyDependencyBundleFoundation.LockedIndependentBaseImageSha256 &&
        first.LockedIndependentObjectTableSha256 ==
            "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d" &&
        first.DonorId == UnusedLevel65MobyDependencyBundleFoundation.DonorId &&
        first.DonorLevelKey == "artisans" && first.DonorLevelName == "Artisans",
    "The exact foundation, independent ID65 table, or donor identity pin changed.");

UnusedLevel65MobySourceRecordManifest row = first.SourceRecord;
Require(row.TrueIndex == 121 && row.WadOffset == 0x9D6C44 && row.SceneRelativeOffset == 0xCC44 &&
        row.ByteLength == 0x58 &&
        row.Sha256 == "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190" &&
        row.SceneRelativePropertiesPointer == 0x11ACC && row.LegacySpecialDataPointer == 0 &&
        row.ActorId == 0x01F5 && row.RenderType == 0x20 && row.RuntimeState == 0 &&
        row.SpecularType == 0 && row.UpdateDistance == 0x10 && row.DropActorOrClass == 0xFF &&
        row.PodOrGroup == 0xFF && row.CullingSector == 0xFF && row.RendererDistance == 0x0C &&
        row.Identity == "Grass" && !string.IsNullOrWhiteSpace(row.IdentityEvidence) &&
        !row.IsDragon && !row.IsPortal && !row.IsThief && !row.IsCollectible &&
        !row.IsTotalsLinked && !row.HasRewardDrop && !row.HasLegacySpecialDataPointer,
    "The safest structural donor is no longer exact Artisans T121 Grass actor 0x01F5.");

UnusedLevel65MobyScenePropertiesManifest props = first.Properties;
Require(props.SceneWadOffset == 0x9CA000 && props.SceneByteLength == 0x14800 &&
        props.ObjectCount == 174 && props.ObjectTableSceneOffset == 0xA2AC &&
        props.PropertiesSceneOffset == 0x11ACC && props.PropertiesByteLength == 8 &&
        props.PropertiesHex == "040000008A000000" &&
        props.PropertiesSha256 == "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b" &&
        props.SharingTrueIndices.SequenceEqual(
            new[] { 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133 }) &&
        props.NextDistinctPropertiesSceneOffset == 0x11AD4 &&
        props.SourcePointerFieldSceneOffset == 0xCC44 && props.SourcePointerFixupCount == 0xC9 &&
        props.SourcePointerFixupListSceneOffset == 0x13EDC && props.SourcePointerFixupActiveByteLength == 0x328 &&
        props.SourcePointerFixupActiveSha256 == "7d677b0b8d6ad795a6319bd31e781c4e5f52a76be67ed81ed1b1c6fbdb4775fb" &&
        props.PointerFieldOccurrenceCount == 1 && props.InternalPropertiesPointerFixups.Count == 0 &&
        props.PropertiesPointerIsSceneRelative && !props.LegacySpecialDataPointerIsPropertiesPointer,
    "The exact scene-relative Grass properties/fixup closure changed.");

UnusedLevel65MobyActorPackageManifest package = first.ActorPackage;
Require(package.DonorWadEntry == 10 && package.DataEntryWadOffset == 0x800800 &&
        package.DataEntryByteLength == 0x383000 && package.ActorSubfileRelativeOffset == 0x17C000 &&
        package.ActorSubfileByteLength == 0x4D800 && package.ActorRootIndex == 22 &&
        package.ActorRootEntryRelativeOffset == 0x1C1520 && package.ActorPackageByteLength == 0x174 &&
        package.ActorPackageSha256 == "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3" &&
        package.AnimationCount == 1 && package.ModelDataRelativeOffset == 0x68 &&
        package.PackageInternalEntryRelativeReferenceCount == 0 && package.TotalTexturedFaceCount == 0 &&
        !package.TexturePixelsRequired && !package.ClutsRequired && !package.PackageRebaseRequired,
    "The exact Artisans Grass actor package or rebase boundary changed.");
RequireFaceStream(
    package.NormalFaces,
    "normal",
    0x68,
    "f06f9c3ff1ba9bebb7379507144b595b1c326681c0713063892ae15e16162b3b");
RequireFaceStream(
    package.FarLodFaces,
    "far-LOD",
    0xB4,
    "b30459d48479636f8f1c385e3a5e86df21a24637806c5e5699553ec789e75c94");

UnusedLevel65MobyDestinationAllocator destination = first.Destination;
Require(destination.TargetWadEntry == 80 && destination.DataEntryWadOffset == 0x6936800 &&
        destination.DataEntryByteLength == 0x2E2000 && destination.SceneWadOffset == 0x6B06800 &&
        destination.SceneByteLength == 0x8800 && destination.ExistingObjectCount == 107 &&
        destination.ObjectTableSceneOffset == 0x170 && destination.ObjectTableWadOffset == 0x6B06970 &&
        destination.ExistingObjectTableSha256 ==
            "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d" &&
        destination.FirstAppendTrueIndex == 107 && destination.FirstAppendRowSceneOffset == 0x2638 &&
        destination.FirstAppendRowWadOffset == 0x6B08E38 &&
        destination.FirstAppendRowSha256 == "10eef285deef7a4b7c82b22aa53589b7833df29de3814649c772bbd5c832f365" &&
        destination.ContiguousZeroRowBytes == 0x2800 && destination.CompleteAppendRowCapacity == 116 &&
        destination.RemainingBytesAfterCompleteRows == 32 &&
        destination.ContiguousZeroRowsSha256 ==
            "84ff92691f909a05b224e1c56abb4864f01b4f8e3c854e4bb4c7baf1d3f6d652" &&
        destination.PlayerAnchorTrueIndex == 92 &&
        destination.PlayerAnchorSha256 == "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2",
    "The exact independent ID65 107-row object table or first row allocation changed.");
Require(destination.ActorSubfileRelativeOffset == 0x173000 && destination.ActorSubfileByteLength == 0x5D000 &&
        destination.ExistingActorRootCount == 37 && destination.NewActorRootIndex == 37 &&
        destination.NewActorRootSlotEntryRelativeOffset == 0xE4 &&
        destination.NewActorIdSlotEntryRelativeOffset == 0x19A &&
        destination.LastActorRootEntryRelativeOffset == 0x1CF994 && destination.LastActorUsedByteLength == 0xB0 &&
        destination.LastActorUsedSha256 == "d80ee82b365a3a29b84227e9cd63b3ea909e32c4113597200d9f53a70fb80b9e" &&
        destination.ActorTailEntryRelativeOffset == 0x1CFA44 && destination.ActorTailByteLength == 0x5BC &&
        destination.ActorTailSha256 == "bed95ae176cbd1efa4cfc7f400fc1eb294bc107dabf29d77e8174471a789cca2" &&
        destination.NewActorRootEntryRelativeOffset == 0x1CFA44 &&
        destination.NewActorPackageEndEntryRelativeOffset == 0x1CFBB8 && destination.ActorTailBytesRemaining == 0x448,
    "The exact ID65 actor-root or actor-tail allocation changed.");
Require(destination.ScenePointerFixupCountOffset == 0x7F28 && destination.ScenePointerFixupListOffset == 0x7F2C &&
        destination.ScenePointerFixupCountBefore == 0x81 && destination.ScenePointerFixupCountAfter == 0x82 &&
        destination.ScenePointerFixupActiveByteLength == 0x208 &&
        destination.ScenePointerFixupActiveSha256 == "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2" &&
        destination.ScenePointerFixupAppendOffset == 0x8130 &&
        destination.AppendedPointerFieldSceneOffset == 0x2638 &&
        destination.PropertiesAllocationSceneOffset == 0x8138 && destination.PropertiesAllocationByteLength == 8 &&
        destination.SceneTailBytesRemaining == 0x6C0 &&
        destination.SceneTailPreimageSha256 == "2df7244b4c2c10726911058b50c0602fdc399c69d26ca4551f1633ab02666068" &&
        destination.FuturePlacementMutationOffsets.SequenceEqual(new[] { 0x00, 0x0C, 0x10, 0x14, 0x20, 0x46, 0x4A }) &&
        destination.RequiresSourceCountIncrement && destination.RequiresRowPointerFixupAppend &&
        !destination.RequiresInternalPropertiesFixupAppend && !destination.RequiresActorPackageRebase &&
        !destination.RequiresTextureAllocation && !destination.RequiresNestedSubfileRelocation &&
        !destination.RequiresDataEntryGrowth && destination.StructuralAllocationComplete,
    "The exact ID65 scene props/fixup allocation or no-growth boundary changed.");

UnusedLevel65MobyBehaviorClosureManifest behavior = first.BehaviorClosure;
RequireDispatch(
    behavior.DonorDispatch,
    "artisans",
    "Artisans",
    9,
    0x7F2800,
    0xE000,
    "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55",
    0x8007DA74,
    0x86630036,
    0x8007DA9C,
    0x8007DBDC,
    "ba55f492574c72c1eed3c21a4cf20c57dc49fe07136f9156b422ca4825455c40",
    0x80085780,
    "427d89f344d0cc0fde0faa67e9ab199d67cf68510d3433478cdf646c50a4c61b",
    "ffdacdf97f86f1aed4505e0869ee2a9bfbdaad92025704fd33d1d4bfb9ba6d88",
    new uint[]
    {
        0x8007DA9C, 0x8007DAA0, 0x8007DAA4, 0x8007DBDC, 0x8007DBE0, 0x8007DBE4,
        0x8007DBE8, 0x8007DBEC, 0x8007DC7C, 0x8007DC80, 0x8007DC84, 0x8007DCD4,
        0x8007DCD8, 0x8007DCDC, 0x8007DCF0, 0x8007DCF4, 0x8007DCF8, 0x8007DCFC,
        0x8007DD00
    },
    new uint[]
    {
        0x28620101, 0x1040004E, 0x286200FF, 0x2402015E, 0x10621A8A, 0x2862015F,
        0x10400024, 0x2402012C, 0x286201A9, 0x10400014, 0x286201A7, 0x286201C4,
        0x10400005, 0x286201AA, 0x240201DD, 0x10621AA7, 0x00000000, 0x080215E0,
        0x00000000
    },
    adjacentClass: null,
    adjacentHandler: null);
RequireDispatch(
    behavior.TargetDispatch,
    "id65-town-square",
    "ID65 / Town Square overlay",
    79,
    0x6927000,
    0xF800,
    "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
    0x8007DB0C,
    0x86640036,
    0x8007DB34,
    0x8007DC70,
    "fc27e64af0f02e3617fd9b791759ea4b2663b9bfc6aeb19abb885f68d41e5b44",
    0x80087258,
    "edb2f325f44eafb8149e0f2b93912a9009e7aea08c77d17bdeb71bb6af20e372",
    "cb3c5bf3677e27833294c86b83ea12e6a1d5a91fd8cd155aa548f1f59f02809f",
    new uint[]
    {
        0x8007DB34, 0x8007DB38, 0x8007DB3C, 0x8007DC70, 0x8007DC74, 0x8007DC78,
        0x8007DC7C, 0x8007DC80, 0x8007DD10, 0x8007DD14, 0x8007DD18, 0x8007DD60,
        0x8007DD64, 0x8007DD68, 0x8007DD7C, 0x8007DD80, 0x8007DD84, 0x8007DD88,
        0x8007DD8C, 0x8007DD90, 0x8007DD94
    },
    new uint[]
    {
        0x28820101, 0x1040004D, 0x288200FF, 0x24020188, 0x10822054, 0x28820189,
        0x10400024, 0x28820138, 0x288201A9, 0x10400012, 0x288201A7, 0x288201C4,
        0x10400005, 0x288201AA, 0x240201DD, 0x10821F35, 0x240201F6, 0x108224D9,
        0x00000000, 0x08021C96, 0x00000000
    },
    adjacentClass: 0x01F6,
    adjacentHandler: 0x800870F0);

UnusedLevel65MobyGlobalExecutableManifest executable = behavior.GlobalExecutable;
Require(executable.Lba == 55_382 && executable.ByteLength == 0x66000 &&
        executable.Sha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
        executable.HeaderByteLength == 0x800 && executable.InitialProgramCounter == 0x8005B8E0 &&
        executable.TextLoadAddress == 0x80010000 && executable.TextByteLength == 0x65800 &&
        executable.TextSha256 == "94eb7b3679075fdd2a856316abe1ac1278819a36a4e12584149d683715e70c5a" &&
        executable.ActorId == 0x01F5 && executable.AlignedITypeImmediateOccurrenceCount == 0,
    "The exact relocated global executable or no-class-0x01F5 immediate census changed.");
Require(behavior.ExactPropertiesWords.SequenceEqual(new uint[] { 0x00000004, 0x0000008A }) &&
        !behavior.PropertiesContainPointers && !behavior.PropertiesControllerSemanticsRequired &&
        !behavior.SoundDependencyRequired && !behavior.ParticleOrEffectDependencyRequired &&
        !behavior.DynamicSpawnDependencyRequired && !behavior.DynamicNativeLinkDependencyRequired &&
        behavior.StaticRuntimeDependencyClosureComplete,
    "The passive Grass properties/controller/sound/particle/spawn/link closure changed.");

string[] expectedDependencyIds =
[
    "source-row",
    "scene-properties",
    "scene-pointer-fixups",
    "actor-model-package",
    "normal-and-far-lod-animation",
    "texture-pixels-and-cluts",
    "destination-object-row",
    "destination-actor-root",
    "destination-scene-storage",
    "reward-totals-persistence",
    "overlay-dispatch-controller",
    "controller-property-semantics",
    "sound-particle-spawn-closure",
    "native-dynamic-link-closure",
    "runtime-acceptance"
];
Require(first.Dependencies.Select(dependency => dependency.Id).SequenceEqual(expectedDependencyIds),
    "The dependency-bundle schema or its deterministic order changed.");
Require(first.Dependencies.Single(dependency => dependency.Id == "texture-pixels-and-cluts").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar &&
        first.Dependencies.Single(dependency => dependency.Id == "overlay-dispatch-controller").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar &&
        first.Dependencies.Single(dependency => dependency.Id == "controller-property-semantics").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar &&
        first.Dependencies.Single(dependency => dependency.Id == "sound-particle-spawn-closure").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar &&
        first.Dependencies.Single(dependency => dependency.Id == "native-dynamic-link-closure").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.NotRequiredByDecodedGrammar &&
        first.Dependencies.Single(dependency => dependency.Id == "reward-totals-persistence").EvidenceState ==
            UnusedLevel65MobyDependencyEvidenceState.ExcludedByPinnedDonorIdentity,
    "The zero-texture, no-handler, or excluded reward/totals closure changed.");
string[] expectedBlockerIds =
[
    "runtime-acceptance"
];
Require(first.Dependencies
        .Where(dependency => dependency.EvidenceState == UnusedLevel65MobyDependencyEvidenceState.UnresolvedHardBlocker)
        .Select(dependency => dependency.Id)
        .SequenceEqual(expectedBlockerIds),
    "The exact hard runtime-publication blocker set changed.");
Require(first.HardRuntimePublicationBlockers.Count == expectedBlockerIds.Length &&
        expectedBlockerIds.Zip(first.HardRuntimePublicationBlockers)
            .All(pair => pair.Second.StartsWith(pair.First + ": ", StringComparison.Ordinal)),
    "The hard blocker explanations are incomplete or out of order.");

UnusedLevel65MobyExporterCompatibilityBoundary boundary = first.ExporterBoundary;
Require(boundary.PropertiesPointerPolicy.Contains("scene-relative", StringComparison.Ordinal) &&
        boundary.LegacySpecialDataPointerPolicy.Contains("never", StringComparison.Ordinal) &&
        !boundary.UsesSharedMobySourcePatchExporter && !boundary.UsesSharedCrossLevelRecipeRegistry &&
        !boundary.ExistingActorPackageRecipeIsSufficient && boundary.ExactReasons.Count == 4 &&
        boundary.ExactReasons.Any(reason => reason.Contains("entry-relative", StringComparison.Ordinal)) &&
        boundary.ExactReasons.Any(reason => reason.Contains("legacy +0x08", StringComparison.Ordinal)) &&
        boundary.ExactReasons.Any(reason => reason.Contains("no-handler", StringComparison.Ordinal)),
    "The fail-closed generic exporter/recipe compatibility boundary changed.");

Require(first.StructurallyCompleteAllocation && first.StaticRuntimeDependencyClosureComplete &&
        !first.RunnableBundleSupport && first.StaticInspectionOnly &&
        !first.ProducesPatches && !first.WritesBin && !first.WritesCue &&
        !first.DisposableRuntimeCandidateAuthorized && !first.AppIntegrated &&
        !first.NormalCreateBinEnabled && !first.ReleasePublicationAuthorized,
    "Structural allocation was confused with runnable support or a publication gate was enabled.");
Require(first.DeterministicContractSha256 ==
            UnusedLevel65MobyDependencyBundleFoundation.ExpectedContractSha256 &&
        first.DeterministicContractSha256 == second.DeterministicContractSha256 &&
        first.SourceRecord == second.SourceRecord && first.ActorPackage == second.ActorPackage &&
        first.Properties.PropertiesSha256 == second.Properties.PropertiesSha256 &&
        first.Properties.SharingTrueIndices.SequenceEqual(second.Properties.SharingTrueIndices) &&
        first.Destination.ExistingObjectTableSha256 == second.Destination.ExistingObjectTableSha256 &&
        first.Destination.ActorTailSha256 == second.Destination.ActorTailSha256 &&
        first.Destination.ScenePointerFixupActiveSha256 == second.Destination.ScenePointerFixupActiveSha256 &&
        first.Destination.SceneTailPreimageSha256 == second.Destination.SceneTailPreimageSha256 &&
        first.Destination.FuturePlacementMutationOffsets.SequenceEqual(second.Destination.FuturePlacementMutationOffsets) &&
        DispatchEqual(first.BehaviorClosure.DonorDispatch, second.BehaviorClosure.DonorDispatch) &&
        DispatchEqual(first.BehaviorClosure.TargetDispatch, second.BehaviorClosure.TargetDispatch) &&
        first.BehaviorClosure.GlobalExecutable == second.BehaviorClosure.GlobalExecutable &&
        first.BehaviorClosure.ExactPropertiesWords.SequenceEqual(second.BehaviorClosure.ExactPropertiesWords) &&
        first.BehaviorClosure.StaticRuntimeDependencyClosureComplete ==
            second.BehaviorClosure.StaticRuntimeDependencyClosureComplete &&
        DependenciesEqual(first.Dependencies, second.Dependencies) &&
        first.HardRuntimePublicationBlockers.SequenceEqual(second.HardRuntimePublicationBlockers) &&
        first.ExporterBoundary.PropertiesPointerPolicy == second.ExporterBoundary.PropertiesPointerPolicy &&
        first.ExporterBoundary.LegacySpecialDataPointerPolicy == second.ExporterBoundary.LegacySpecialDataPointerPolicy &&
        first.ExporterBoundary.ExactReasons.SequenceEqual(second.ExporterBoundary.ExactReasons),
    "Two read-only dependency-bundle inspections were not deterministic.");

Require(HashFile(foundationImagePath) == sourceHashBefore &&
        new FileInfo(foundationImagePath).Length == sourceLengthBefore &&
        File.GetLastWriteTimeUtc(foundationImagePath) == sourceWriteBefore &&
        DirectorySnapshotsEqual(directoryBefore, SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!)),
    "The read-only Moby dependency contract changed its source BIN/CUE directory.");

Console.WriteLine("ID65 Moby dependency-bundle foundation:");
Console.WriteLine($"  foundation: {first.FoundationImageSha256}");
Console.WriteLine($"  donor: {first.DonorLevelName} T{row.TrueIndex} {row.Identity}, actor 0x{row.ActorId:X4}, row={row.Sha256}");
Console.WriteLine($"  props: scene+0x{props.PropertiesSceneOffset:X}/0x{props.PropertiesByteLength:X}, sha={props.PropertiesSha256}, source fixup occurrence={props.PointerFieldOccurrenceCount}");
Console.WriteLine($"  actor: root 0x{package.ActorRootEntryRelativeOffset:X}, bytes=0x{package.ActorPackageByteLength:X}, sha={package.ActorPackageSha256}");
Console.WriteLine($"  faces: normal={package.NormalFaces.RecordCount}/{package.NormalFaces.TexturedRecordCount} textured; far-LOD={package.FarLodFaces.RecordCount}/{package.FarLodFaces.TexturedRecordCount} textured");
Console.WriteLine($"  rows: T{destination.FirstAppendTrueIndex} scene+0x{destination.FirstAppendRowSceneOffset:X}; complete capacity={destination.CompleteAppendRowCapacity}");
Console.WriteLine($"  actor allocation: slot={destination.NewActorRootIndex}, root=0x{destination.NewActorRootEntryRelativeOffset:X}, end=0x{destination.NewActorPackageEndEntryRelativeOffset:X}, remaining=0x{destination.ActorTailBytesRemaining:X}");
Console.WriteLine($"  scene allocation: fixup 0x{destination.ScenePointerFixupCountBefore:X}->0x{destination.ScenePointerFixupCountAfter:X} append=0x{destination.ScenePointerFixupAppendOffset:X}; props=0x{destination.PropertiesAllocationSceneOffset:X}; remaining=0x{destination.SceneTailBytesRemaining:X}");
Console.WriteLine($"  donor dispatch: 0x{behavior.DonorDispatch.DispatchStartAddress:X8} -> default 0x{behavior.DonorDispatch.DefaultLoopExitAddress:X8}, trace={behavior.DonorDispatch.ExecutedTraceSha256}");
Console.WriteLine($"  target dispatch: 0x{behavior.TargetDispatch.DispatchStartAddress:X8} -> default 0x{behavior.TargetDispatch.DefaultLoopExitAddress:X8}, trace={behavior.TargetDispatch.ExecutedTraceSha256}");
Console.WriteLine($"  global executable: {executable.Sha256}, class-0x{executable.ActorId:X4} I-type immediates={executable.AlignedITypeImmediateOccurrenceCount}");
foreach (string blocker in first.HardRuntimePublicationBlockers)
    Console.WriteLine($"  HARD BLOCKER: {blocker}");
Console.WriteLine($"  deterministic contract: {first.DeterministicContractSha256}");
Console.WriteLine("PASS UnusedLevel65MobyDependencyBundleSmoke: exact retail/foundation preimages, scene-relative props and fixups, untextured normal/far-LOD package, independent ID65 row/root/tail allocation, exact donor+target no-update dispatch traces, closed controller/sound/particle/spawn/link dependencies, sole fail-closed runtime gate, and no-write/no-CUE/no-Create-BIN gates passed.");

static void RequireDispatch(
    UnusedLevel65MobyDispatchTraceManifest dispatch,
    string levelKey,
    string levelName,
    int wadEntry,
    long wadOffset,
    int byteLength,
    string overlaySha256,
    uint classLoadAddress,
    uint classLoadWord,
    uint dispatchStartAddress,
    uint decisionWindowAddress,
    string decisionWindowSha256,
    uint defaultLoopExitAddress,
    string defaultLoopExitSha256,
    string traceSha256,
    IReadOnlyList<uint> traceAddresses,
    IReadOnlyList<uint> traceWords,
    ushort? adjacentClass,
    uint? adjacentHandler)
{
    Require(dispatch.LevelKey == levelKey && dispatch.LevelName == levelName &&
            dispatch.OverlayWadEntry == wadEntry && dispatch.OverlayWadOffset == wadOffset &&
            dispatch.OverlayByteLength == byteLength && dispatch.OverlaySha256 == overlaySha256 &&
            dispatch.OverlayLoadAddress == 0x8007AA38 && dispatch.ActorId == 0x01F5 &&
            dispatch.ClassLoadAddress == classLoadAddress && dispatch.ClassLoadWord == classLoadWord &&
            dispatch.DispatchStartAddress == dispatchStartAddress &&
            dispatch.DecisionWindowAddress == decisionWindowAddress &&
            dispatch.DecisionWindowByteLength == 0x128 &&
            dispatch.DecisionWindowSha256 == decisionWindowSha256 &&
            dispatch.DefaultLoopExitAddress == defaultLoopExitAddress &&
            dispatch.DefaultLoopExitSha256 == defaultLoopExitSha256 &&
            dispatch.ExecutedTraceSha256 == traceSha256 &&
            dispatch.ExecutedInstructionAddresses.SequenceEqual(traceAddresses) &&
            dispatch.ExecutedInstructionWords.SequenceEqual(traceWords) &&
            dispatch.AdjacentDedicatedClassId == adjacentClass &&
            dispatch.AdjacentDedicatedHandlerAddress == adjacentHandler &&
            dispatch.CallInstructionCount == 0 && dispatch.PropertiesReadCount == 0 &&
            dispatch.RuntimeActorWriteCount == 0 && !dispatch.DedicatedHandlerPresent &&
            dispatch.ReachesDefaultLoopExit && !dispatch.ClassSpecificControllerPresent,
        $"The exact {levelName} class-0x01F5 default-dispatch trace changed.");
}

static void RequireFaceStream(
    UnusedLevel65MobyFaceStreamManifest stream,
    string tier,
    int offset,
    string sha256)
{
    Require(stream.Tier == tier && stream.PackageRelativeOffset == offset && stream.BodyByteLength == 0x48 &&
            stream.TotalByteLength == 0x4C && stream.RecordCount == 9 &&
            stream.UntexturedTriangleCount == 9 && stream.TexturedRecordCount == 0 &&
            stream.Sha256 == sha256 && stream.EndsExactlyUnderRetailRendererGrammar,
        $"The exact {tier} untextured face-stream grammar changed.");
}

static string FindRepositoryRoot(string? supplied)
{
    string current = string.IsNullOrWhiteSpace(supplied)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(supplied);
    while (true)
    {
        if (File.Exists(Path.Combine(current, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current, "src", "Spyro.Editor.Core")))
        {
            return current;
        }
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
        current = parent.FullName;
    }
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static IReadOnlyDictionary<string, FileState> SnapshotDirectory(string path) =>
    Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
        .OrderBy(file => file, StringComparer.Ordinal)
        .ToDictionary(
            file => Path.GetFileName(file),
            file => new FileState(new FileInfo(file).Length, File.GetLastWriteTimeUtc(file)),
            StringComparer.Ordinal);

static bool DirectorySnapshotsEqual(
    IReadOnlyDictionary<string, FileState> left,
    IReadOnlyDictionary<string, FileState> right) =>
    left.Count == right.Count &&
    left.All(pair => right.TryGetValue(pair.Key, out FileState? value) && value == pair.Value);

static bool DependenciesEqual(
    IReadOnlyList<UnusedLevel65MobyDependencyRequirement> left,
    IReadOnlyList<UnusedLevel65MobyDependencyRequirement> right) =>
    left.Count == right.Count && left.Zip(right).All(pair =>
        pair.First.Id == pair.Second.Id &&
        pair.First.Kind == pair.Second.Kind &&
        pair.First.RequiredForRunnableBundle == pair.Second.RequiredForRunnableBundle &&
        pair.First.EvidenceState == pair.Second.EvidenceState &&
        pair.First.DependsOn.SequenceEqual(pair.Second.DependsOn) &&
        pair.First.Evidence == pair.Second.Evidence);

static bool DispatchEqual(
    UnusedLevel65MobyDispatchTraceManifest left,
    UnusedLevel65MobyDispatchTraceManifest right) =>
    left.LevelKey == right.LevelKey && left.LevelName == right.LevelName &&
    left.OverlayWadEntry == right.OverlayWadEntry && left.OverlayWadOffset == right.OverlayWadOffset &&
    left.OverlayByteLength == right.OverlayByteLength && left.OverlaySha256 == right.OverlaySha256 &&
    left.OverlayLoadAddress == right.OverlayLoadAddress && left.ActorId == right.ActorId &&
    left.ClassLoadAddress == right.ClassLoadAddress && left.ClassLoadWord == right.ClassLoadWord &&
    left.DispatchStartAddress == right.DispatchStartAddress &&
    left.DecisionWindowAddress == right.DecisionWindowAddress &&
    left.DecisionWindowByteLength == right.DecisionWindowByteLength &&
    left.DecisionWindowSha256 == right.DecisionWindowSha256 &&
    left.DefaultLoopExitAddress == right.DefaultLoopExitAddress &&
    left.DefaultLoopExitSha256 == right.DefaultLoopExitSha256 &&
    left.ExecutedInstructionAddresses.SequenceEqual(right.ExecutedInstructionAddresses) &&
    left.ExecutedInstructionWords.SequenceEqual(right.ExecutedInstructionWords) &&
    left.ExecutedTraceSha256 == right.ExecutedTraceSha256 &&
    left.AdjacentDedicatedClassId == right.AdjacentDedicatedClassId &&
    left.AdjacentDedicatedHandlerAddress == right.AdjacentDedicatedHandlerAddress &&
    left.CallInstructionCount == right.CallInstructionCount &&
    left.PropertiesReadCount == right.PropertiesReadCount &&
    left.RuntimeActorWriteCount == right.RuntimeActorWriteCount &&
    left.DedicatedHandlerPresent == right.DedicatedHandlerPresent &&
    left.ReachesDefaultLoopExit == right.ReachesDefaultLoopExit &&
    left.ClassSpecificControllerPresent == right.ClassSpecificControllerPresent;

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record FileState(long ByteLength, DateTime LastWriteUtc);
