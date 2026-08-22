using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string root = FindRoot(args.ElementAtOrDefault(0));
string lockedBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string wrongBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedBase), "The exact locked display-name BIN is missing.");
Require(File.Exists(wrongBase), "The wrong-hash negative fixture is missing.");

string hashBefore = HashFile(lockedBase);
long lengthBefore = new FileInfo(lockedBase).Length;
DateTime writeBefore = File.GetLastWriteTimeUtc(lockedBase);
DirectorySnapshot directoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedBase)!);

UnusedLevel65ReturnHomeOwnershipContract first =
    UnusedLevel65ReturnHomeOwnershipInspector.Inspect(root, lockedBase);
UnusedLevel65ReturnHomeOwnershipContract second =
    UnusedLevel65ReturnHomeOwnershipInspector.Inspect(root, lockedBase);
JsonSerializerOptions json = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
Require(
    JsonSerializer.Serialize(first, json) == JsonSerializer.Serialize(second, json),
    "Two Return Home ownership inspections were not deterministic.");

Require(
    first.ProfileId == UnusedLevel65ReturnHomeOwnershipInspector.ProfileId &&
    first.LockedBaseImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.RemoteBlankProfileId == UnusedLevel65RemoteBlankIsolationConstruction.ProfileId &&
    first.RemoteBlankOutputDataSha256 == "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061" &&
    first.ExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
    first.OverlaySha256 == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
    "The exact locked/remote/executable substrate changed.");

Require(
    first.ScenePointerFixups.LogicalOffset == 0x6B0E728 &&
    first.ScenePointerFixups.ByteLength == 0x208 &&
    first.ScenePointerFixups.Sha256 == "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2" &&
    first.ScenePointerFixups.ImageOffset == -1,
    "The exact remote scene pointer-fixup component changed.");

UnusedLevel65ReturnHomeRowClosure visible = first.Visible;
Require(
    visible.TrueIndex == 96 && visible.Role == "visible Return Home controller" &&
    visible.ActorClass == 0x0009 &&
    visible.TownSquareRow.LogicalOffset == 0x1370270 && visible.TownSquareRow.ImageOffset == 0x1668078 &&
    visible.TownSquareRow.Sha256 == "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4" &&
    visible.RemoteBeforeRow.LogicalOffset == 0x6B08A70 && visible.RemoteBeforeRow.ImageOffset == -1 &&
    visible.RemoteBeforeRow.Sha256 == visible.TownSquareRow.Sha256 &&
    visible.ProposedAfterRow.Sha256 == "6e6bd67a7c8bc6ae46dcb003729f02bdf836ecd86075f2f48b1c6931fab0b721" &&
    visible.BeforeRawX == 141_220 && visible.BeforeRawY == 136_827 && visible.BeforeRawZ == 11_776 &&
    visible.ProposedRawX == 6_144 && visible.ProposedRawY == 7_296 && visible.ProposedRawZ == 8_192 &&
    !visible.TownSquareAndRemoteArePhysicalStorageAliases && visible.RowBytesOutsideXyzPreserved,
    "The exact T96 before/after row closure changed.");
Require(
    visible.Properties.SceneRelativePointer == 0x5CCC &&
    visible.Properties.WadOffset == 0x6B0C4CC && visible.Properties.ByteLength == 0x10 &&
    visible.Properties.Bytes.Hex == "FFFFFFFF000000000000000000000000" &&
    visible.Properties.Bytes.Sha256 == "231f5cecc61699ca90f5e9acdbc4c47552f66daeb7ef55327cb7574cda9f29e5" &&
    visible.Properties.NextDistinctPropertiesSceneOffset == 0x5CDC &&
    visible.Properties.RowPointerFieldSceneOffset == 0x2270 &&
    visible.Properties.RowPointerFixupOccurrenceCount == 1 &&
    visible.Properties.InternalPointerFixupFields.Count == 0 &&
    visible.Properties.ExactOwnedExtent && visible.Properties.PreservedByProposal,
    "The exact T96 property/fixup closure changed.");

UnusedLevel65ReturnHomeRowClosure helper = first.Helper;
Require(
    helper.TrueIndex == 97 && helper.ActorClass == 0x011E &&
    helper.TownSquareRow.LogicalOffset == 0x13702C8 && helper.TownSquareRow.ImageOffset == 0x16680D0 &&
    helper.TownSquareRow.Sha256 == "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc" &&
    helper.RemoteBeforeRow.LogicalOffset == 0x6B08AC8 && helper.RemoteBeforeRow.Sha256 == helper.TownSquareRow.Sha256 &&
    helper.ProposedAfterRow.Sha256 == "e7b1b6d84197094fd59b59dc3aaa773a7d05113c4574bd1e9b468c16a05ca355" &&
    helper.BeforeRawX == 141_220 && helper.BeforeRawY == 136_827 && helper.BeforeRawZ == 12_288 &&
    helper.ProposedRawX == 6_144 && helper.ProposedRawY == 7_296 && helper.ProposedRawZ == 8_704 &&
    !helper.TownSquareAndRemoteArePhysicalStorageAliases && helper.RowBytesOutsideXyzPreserved,
    "The exact T97 before/after row closure changed.");
Require(
    helper.Properties.SceneRelativePointer == 0x5CDC &&
    helper.Properties.WadOffset == 0x6B0C4DC && helper.Properties.ByteLength == 0x60 &&
    helper.Properties.Bytes.Sha256 == "10539eeae9088ffaa94a6199b044c5478b8602c4144144f8b581514fa71415c1" &&
    helper.Properties.NextDistinctPropertiesSceneOffset == 0x5D3C &&
    helper.Properties.RowPointerFieldSceneOffset == 0x22C8 &&
    helper.Properties.RowPointerFixupOccurrenceCount == 1 &&
    helper.Properties.InternalPointerFixupFields.SequenceEqual(new[] { 0x5CE4 }) &&
    helper.Properties.ExactOwnedExtent && helper.Properties.PreservedByProposal,
    "The exact T97 nested property/fixup closure changed.");

UnusedLevel65ReturnHomeActorClosure actor = first.Actor;
Require(
    actor.ActorId == 0x0009 && actor.ActorRootIndex == 15 &&
    actor.ActorRootDataRelativeOffset == 0x1C4F28 && actor.NextActorRootDataRelativeOffset == 0x1C5790 &&
    actor.ActorPackageByteLength == 0x868 && actor.ActorPackage.LogicalOffset == 0x6AFB728 &&
    actor.ActorPackage.Sha256 == "cb95609bfa6cec1ced0428d83b33cad96b59c9e803133e7002f2a9c776bdb182" &&
    actor.PackageAlreadyOwnedById65 && actor.PackagePreservedByRemoteBlank && actor.PackagePreservedByProposal &&
    !actor.ActorCopyRequired,
    "The actor-0x0009 model/package ownership changed.");

UnusedLevel65ReturnHomeNativeClosure native = first.Native;
Require(
    native.Overlay.LogicalOffset == 0x6927000 && native.Overlay.ByteLength == 0xF800 &&
    native.T96ClassDispatch.RuntimeAddress == 0x8007DB74 && native.T96ClassDispatch.Hex == "0900092487008910" &&
    native.T96Handler.RuntimeAddress == 0x8007DD98 && native.T96Handler.ByteLength == 0x304 &&
    native.T96Handler.Sha256 == "bfbf78f6edea427384ee73e874cfba832dfdc73013f46dbe96532fc09dbc632e" &&
    native.T96CommitPath.RuntimeAddress == 0x8007DF98 && native.T96CommitPath.ByteLength == 0x104 &&
    native.T96CommitPath.Sha256 == "ec08bdc5a28197aed9c8fc9b4740b4c8d917b7fd8e2221145912b538edf04fbc" &&
    native.T96TransitionCall.CallerRuntimeAddress == 0x8007E07C &&
    native.T96TransitionCall.CalleeRuntimeAddress == 0x8004AC24 &&
    native.T96TransitionCall.InstructionHex == "092B010C" &&
    native.T96ComputesHomeworldFromCurrentLevel && native.T96BypassesNormalPortalGuard,
    "The exact T96 native transition closure changed.");
Require(
    native.T97ClassDispatch.RuntimeAddress == 0x8007DCB4 && native.T97ClassDispatch.Hex == "1E010224111B8210" &&
    native.T97SelectorJumpTable.RuntimeAddress == 0x8007AC34 && native.T97SelectorJumpTable.ByteLength == 40 &&
    native.T97SelectorJumpTable.Sha256 == "d9dceaafe1dcf71968345751fb2b797d4ee79cdcf18296d9066d935e63b9e695" &&
    native.T97SelectorJumpTargets.SequenceEqual(new uint[]
    {
        0x80084944, 0x8008494C, 0x80084988, 0x80084C40, 0x80084C7C,
        0x80084CD0, 0x80084CD8, 0x80084CE0, 0x80084CE8, 0x80084CF0
    }) &&
    native.T97Handler.RuntimeAddress == 0x80084900 && native.T97Handler.ByteLength == 0x420 &&
    native.T97Handler.Sha256 == "15de22eddf6b203f0de586af8fc62954bf53f3d38fc65f3510d68884fbe16ce5" &&
    native.T97Selector8SoundTail.RuntimeAddress == 0x80084CE8 && native.T97Selector == 8 && native.T97SoundIndex == 0x1A &&
    native.T97SoundCall.CallerRuntimeAddress == 0x80084D10 && native.T97SoundCall.CalleeRuntimeAddress == 0x80055A78 &&
    native.T97SoundCall.InstructionHex == "9E56010C" &&
    native.RemoteParticleComponent.LogicalOffset == 0x6AA8E04 && native.RemoteParticleComponent.ByteLength == 0x80 &&
    native.RemoteParticleComponent.Sha256 == "e4c92864f220a33d8c5d2fc158806fc0822dd01d3eb95ef8932505d72570a0ce" &&
    native.RemoteSoundComponent.LogicalOffset == 0x6AA8E84 && native.RemoteSoundComponent.ByteLength == 0x6F8 &&
    native.RemoteSoundComponent.Sha256 == "51eebb756d1ad97f8904da785a4f4585f353259f73f20037d6c5694833fab4fa" &&
    native.T97UsesPreservedPerLevelSoundMapping && !native.RuntimeSoundIdentityVerified,
    "The exact T97 native/sound closure changed.");

UnusedLevel65ReturnHomePauseExitClosure pause = first.PauseExit;
Require(
    pause.ExitLevelLabel.LogicalOffset == 0x1384 && pause.ExitLevelLabel.Hex == "45584954204C4556454C00" &&
    pause.ExitQuitDrawBranch.LogicalOffset == 0xBD1C &&
    pause.ExitQuitDrawBranch.Sha256 == "b96bd20547cfccbef821cb2e39002d57173324dc4f71ccd90eb0d839e15e155a" &&
    pause.SelectionThreeBranch.LogicalOffset == 0x1F244 &&
    pause.SelectionThreeBranch.Sha256 == "6872e3cda7a45306b46c7459895b7e1859a3c8c423dd5afe05e00514fbcc5247" &&
    pause.ConfirmState.LogicalOffset == 0x1E884 &&
    pause.ConfirmState.Sha256 == "20f73ff46edd435081cd989cc8726328ff63c02abd0b46387840793c3262a0cb" &&
    pause.ExitSetupRoutine.LogicalOffset == 0x1CE64 && pause.ExitSetupRoutine.RuntimeAddress == 0x8002C664 &&
    pause.ExitSetupRoutine.Sha256 == "17c22be3523fbd4e6730d25d24d5ad80530185d67d2fc4fc9ccceabd5385f423" &&
    pause.ConfirmToExitSetupCall.CallerLogicalOffset == 0x1E8E8 &&
    pause.ConfirmToExitSetupCall.InstructionHex == "99B1000C" &&
    pause.CurrentLevelRuntimeAddress == 0x8007596C && pause.TransitionTargetRuntimeAddress == 0x800758B4 &&
    pause.RouteRuntimeAddress == 0x8007576C && pause.SourceLevelId == 65 &&
    pause.ComputedDestinationLevelId == 60 && pause.ComputedRoute == -1 &&
    pause.ExitItemAvailableForId65 && !pause.UsesNormalPortalGuard && !pause.RuntimeVerified,
    "The exact Pause Exit Level closure changed.");

UnusedLevel65ReturnHomeBootstrapClosure bootstrap = first.Bootstrap;
Require(
    bootstrap.WarpDispatchPointer.LogicalOffset == 0x1CA8 && bootstrap.WarpDispatchPointer.Hex == "98A80580" &&
    bootstrap.WarpDispatchRuntimeTarget == 0x8005A898 &&
    bootstrap.HiddenWarpSelectionPath.LogicalOffset == 0x1DD80 && bootstrap.HiddenWarpSelectionPath.ByteLength == 0x280 &&
    bootstrap.HiddenWarpSelectionPath.Sha256 == "d548119aafb2f4396cdaa3b924d21b38158d02c9239116ac7237ead626b2ddf3" &&
    bootstrap.WarpTableUpperBound.LogicalOffset == 0x1DF44 && bootstrap.WarpTableUpperBound.Hex == "3800422C" &&
    bootstrap.WarpTableUpperBoundImmediate == 0x38 && bootstrap.MaximumAcceptedLevelId == 65 &&
    bootstrap.BootstrapStores.LogicalOffset == 0x1E034 && bootstrap.BootstrapStores.Hex == "1C0684AF880680AF" &&
    bootstrap.NonFlightIdentity.LogicalOffset == 0x40C0 && bootstrap.NonFlightIdentity.Hex == "005E023C" &&
    bootstrap.CurrentLevelRuntimeAddress == 0x8007596C && bootstrap.TransitionTargetRuntimeAddress == 0x800758B4 &&
    bootstrap.HiddenWarpCanSelect65 && bootstrap.ReentryIsTestOnly &&
    !bootstrap.ThisContractReverifiedPhysicalBootstrapRuntime,
    "The exact ID65 bootstrap/re-entry static boundary changed.");

UnusedLevel65ReturnHomeDestinationClosure destinations = first.Destinations;
Require(
    destinations.PortalEventJumpTable.LogicalOffset == 0x1B80 &&
    destinations.PortalEventJumpTable.Sha256 == "4e0ffe15100a8c84ba612ef624f4ec78eba729bb6a4a9a14b5443c796ef705af" &&
    destinations.PortalEventType6Entry.LogicalOffset == 0x1B98 && destinations.PortalEventType6Entry.Hex == "EC700580" &&
    destinations.PortalDispatcherEntry.LogicalOffset == 0x47764 && destinations.PortalDispatcherEntry.RuntimeAddress == 0x80056F64 &&
    destinations.PortalDispatcherDirectCallers.Select(edge => edge.CallerLogicalOffset).SequenceEqual(new long[] { 0x394F4 }) &&
    destinations.PortalDispatcherDirectCallers[0].InstructionHex == "D95B010C" &&
    destinations.PortalGuardPath.LogicalOffset == 0x47904 &&
    destinations.PortalGuardPath.Sha256 == "32b07aa096fba6f638f262fdda1ce37e9d903ca73382953025030f70f0ef0eb8" &&
    destinations.GuardInstructionFileOffset == 0x47910 && destinations.GuardInstructionHex == "41004228" &&
    destinations.NormalPortalUpperExclusive == 65 && destinations.GuardPatchBytesInProposal == 0,
    "The exact normal-portal dispatcher/guard closure changed.");
Require(
    destinations.SourceId65PortalTable.LogicalOffset == 0x6AA8D8C && destinations.SourceId65PortalTable.Hex == "00000000" &&
    destinations.RemoteId65PortalTable.LogicalOffset == 0x6AA8E00 && destinations.RemoteId65PortalTable.Hex == "00000000" &&
    destinations.Id65PortalCount == 0 && destinations.Id65PortalAppendCapacityWithoutRelocation == 0 &&
    destinations.GnastyWorldPortalTable.LogicalOffset == 0x52F280C &&
    destinations.GnastyWorldPortalTable.ByteLength == 0x2B910 &&
    destinations.GnastyWorldPortalTable.Sha256 == "4da8159eb38b8b77d48b9ce8ddcec54c2fd7389a79ae88d97c892d43da70e12d" &&
    destinations.GnastyWorldPortalCount == 4 && destinations.GnastyWorldPortalUsedByteLength == 0x2B910 &&
    destinations.GnastyWorldPortalAppendCapacityWithoutRelocation == 0 &&
    destinations.GnastyWorldDestinationLevelIds.SequenceEqual(new[] { 62, 63, 64, 61 }) &&
    destinations.CheckedInGnastyWorldDestinationLevelIds.SequenceEqual(new[] { 61, 62, 63, 64 }) &&
    !destinations.GnastyWorldHasDestination65 && !destinations.RetailGnastyWorldMutationRequired &&
    !destinations.NormalArrivalRouteTo65Owned,
    "The exact ID65/Gnasty destination-table capacity boundary changed.");

UnusedLevel65ReturnHomeCommonTransitionClosure common = first.CommonTransition;
Require(
    common.Routine.LogicalOffset == 0x3B424 && common.Routine.RuntimeAddress == 0x8004AC24 &&
    common.Routine.ByteLength == 0x210 &&
    common.Routine.Sha256 == "8d9ece151795f729d59997279a51b0a68af001319d8ba0c09157633e9b7190e3" &&
    common.ExecutableDirectCallers.Select(edge => edge.CallerLogicalOffset).SequenceEqual(new long[]
    {
        0x40B0, 0x1CE44, 0x1D3D0, 0x1D578, 0x1D864, 0x1DA8C,
        0x1DF9C, 0x1F7D4, 0x1FF64, 0x23464, 0x23958
    }) &&
    common.ExecutableDirectCallers.All(edge => edge.InstructionHex == "092B010C") &&
    common.CurrentLevelRuntimeAddress == 0x8007596C && common.TransitionTargetRuntimeAddress == 0x800758B4 &&
    !common.RoutineBytesPatchedByProposal,
    "The common transition function/call graph changed.");

UnusedLevel65ReturnHomeLinkClosure link = first.Link;
Require(
    link.TownSquareMetadataPath == "townsquare-behavior-links.json" &&
    link.TownSquareMetadataSha256 == "b1d824d2846188983029b61fc1303eb76336c8bc5926526359987b4672bcf93f" &&
    link.TownSquareGroupKey == "townsquare:control-role-proof:t97" &&
    link.TownSquareTrueIndexes.SequenceEqual(new[] { 97, 96 }) &&
    link.LabMetadataFileName == "unusedlevel65blank-behavior-links.json" &&
    link.RequiredLabGroupKey == "unusedlevel65blank:control-role-proof:t97" &&
    link.CanonicalLabGroupSha256 == "207f316ef796c1830a558a805cfca82170dc2b6c81610923e90ede3ed1989e53" &&
    link.RequiredTrueIndexes.SequenceEqual(new[] { 97, 96 }) &&
    link.RequiredMemberIds.SequenceEqual(new[] { "T97", "T96" }) &&
    link.TownSquarePairLinkPresent && !link.LabPairLinkPresent && link.LabMetadataInstallRequired &&
    !link.LinkedEditorMoveRuntimeVerified,
    "The exact native/editor linked-pair boundary changed.");

UnusedLevel65ReturnHomePlacementClosure placement = first.Placement;
Require(
    placement.GroundRawZ == 8_192 &&
    placement.PadPoints.SequenceEqual(new[]
    {
        new UnusedLevel65RemoteBlankPoint(272, 272, 512),
        new UnusedLevel65RemoteBlankPoint(496, 272, 512),
        new UnusedLevel65RemoteBlankPoint(384, 496, 512)
    }) &&
    placement.SpawnRawX == 6_153 && placement.SpawnRawY == 6_154 && placement.SpawnRawZ == 8_704 &&
    placement.T96RawX == 6_144 && placement.T96RawY == 7_296 && placement.T96RawZ == 8_192 &&
    placement.T97RawX == 6_144 && placement.T97RawY == 7_296 && placement.T97RawZ == 8_704 &&
    placement.T97AboveT96Raw == 512 &&
    Math.Abs(placement.HorizontalSpawnDistanceWorld - 71.37721647171456) < 0.000000001 &&
    Math.Abs(placement.ThreeDimensionalSpawnDistanceWorld - 78.22216457788674) < 0.000000001 &&
    Math.Abs(placement.NearestPadEdgeMarginWorld - 17.88854381999832) < 0.000000001 &&
    placement.NativeNearTriggerRaw == 0x400 && placement.NativeNearTriggerWorld == 64 &&
    Math.Abs(placement.HorizontalNoImmediateTriggerMarginWorld - 7.37721647171456) < 0.000000001 &&
    placement.ChangedDataByteOffsets.SequenceEqual(new[]
    {
        0x1D227C, 0x1D227D, 0x1D227E, 0x1D2280, 0x1D2281, 0x1D2282, 0x1D2285,
        0x1D22D4, 0x1D22D5, 0x1D22D6, 0x1D22D8, 0x1D22D9, 0x1D22DA, 0x1D22DD
    }) &&
    placement.LogicalXyzPatchBytes == 24 && placement.ChangedDataByteCount == 14 &&
    placement.XyzPatchManifestHex == "00180000801C00000020000000180000801C000000220000" &&
    placement.XyzPatchManifestSha256 == "de79cdfb280010a1fc3f64e46bb0b58d61169a4a0be0e5744a0e2b912a24307d" &&
    placement.ProposedObjectTableSha256 == "f385b221a54cd92c59c2dc7ca60670956c463093d98768e663a52edbb3572a58" &&
    placement.ProposedDataSha256 == "62ce6b2c39ae13236a44e05b89baa305fedeaf51b09b7abf8b9e863b2910a879" &&
    placement.T96InsideAuthoredPad && placement.T97InsideAuthoredPad && placement.SpawnOutsideNativeNearTrigger &&
    !placement.VisualFootprintRuntimeVerified && !placement.ActivationRuntimeVerified,
    "The exact atomic T96/T97 remote-pad placement changed.");

UnusedLevel65ReturnHomeTransaction transaction = first.Transaction;
Require(
    transaction.Id == "id65:t96-t97:return-home-pad-relocation-and-lab-link-v1" &&
    transaction.Id65RowCount == 2 && transaction.Id65TrueIndexes.SequenceEqual(new[] { 96, 97 }) &&
    transaction.LogicalWadPatchBytes == 24 && transaction.ExecutablePatchBytes == 0 &&
    transaction.RetailWadPatchBytes == 0 && transaction.ChangedDataBytes == 14 &&
    transaction.RequiredSidecars.SequenceEqual(new[] { "unusedlevel65blank-behavior-links.json" }) &&
    transaction.DeterministicManifestSha256 == "e97285d63c700172caf343e96de532ecc4605043dde24fa8c6940cb7e8ddf381" &&
    transaction.StaticDependencyClosureComplete && transaction.DestinationSafe && transaction.RetailLevelsPreserved &&
    !transaction.WriterAuthorized && !transaction.RuntimeCandidateAuthorized && !transaction.PromotionAuthorized &&
    transaction.RuntimeOnlyGates.Count == 8 &&
    first.RuntimeGates.Count == 8 && first.RuntimeGates.Select(gate => gate.Order).SequenceEqual(Enumerable.Range(1, 8)) &&
    first.RuntimeGates.All(gate => !gate.Passed),
    "The smallest transaction or runtime-only gate boundary changed.");
Require(
    first.LockedImagePreserved && first.RemotePlanInMemoryOnly && first.StaticReadbackOnly &&
    !first.WritesBin && !first.WritesCue && !first.WritesMetadata && !first.AppIntegrated &&
    !first.NormalCreateBinEnabled && !first.ReleaseAuthorized,
    "The no-writer/no-app/no-release boundary changed.");

bool wrongHashRejected = false;
try
{
    _ = UnusedLevel65ReturnHomeOwnershipInspector.Inspect(root, wrongBase);
}
catch (InvalidDataException ex) when (ex.Message.Contains("SHA-256", StringComparison.Ordinal))
{
    wrongHashRejected = true;
}
Require(wrongHashRejected, "A stale/wrong ID65 base was accepted.");

Require(
    HashFile(lockedBase) == hashBefore &&
    new FileInfo(lockedBase).Length == lengthBefore &&
    File.GetLastWriteTimeUtc(lockedBase) == writeBefore &&
    directoryBefore == SnapshotDirectory(Path.GetDirectoryName(lockedBase)!),
    "The Return Home ownership smoke changed the locked candidate or its directory.");

Console.WriteLine("PASS: ID65 T96/T97 Return Home ownership is statically closed on the exact remote blank pad.");
Console.WriteLine("Destination: native Return Home and Pause Exit compute 65 -> 60; normal portal <65 guard is untouched.");
Console.WriteLine("Bundle: T96 row/props/actor-0x0009 plus T97 row/props/selector-8/sound are exact and preserved.");
Console.WriteLine("Transaction: two ID65 XYZ spans (24 logical bytes, 14 changed bytes) plus one lab-key linked-move sidecar.");
Console.WriteLine("Retail impact: 0 executable bytes, 0 retail WAD bytes, Gnasty destinations remain 62/63/64/61.");
Console.WriteLine("Artifacts written: 0 BIN, 0 CUE, 0 metadata; eight runtime gates remain false.");

static string FindRoot(string? requested)
{
    string current = Path.GetFullPath(requested ?? Directory.GetCurrentDirectory());
    while (!File.Exists(Path.Combine(current, "spyro-level-catalog.json")))
    {
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not find the Spyro Editor repository root.");
        current = parent.FullName;
    }
    return current;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static DirectorySnapshot SnapshotDirectory(string path)
{
    string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .Select(file => $"{Path.GetRelativePath(path, file)}|{new FileInfo(file).Length}|{HashFile(file)}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    string[] directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
        .Select(directory => Path.GetRelativePath(path, directory))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return new(string.Join('\n', files), string.Join('\n', directories));
}

internal sealed record DirectorySnapshot(string Files, string Directories);
