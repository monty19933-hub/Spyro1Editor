using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65ReturnHomeByteRegion(
    string Name,
    string Container,
    long LogicalOffset,
    long ImageOffset,
    uint? RuntimeAddress,
    int ByteLength,
    string Hex,
    string Sha256);

internal sealed record UnusedLevel65ReturnHomeCallEdge(
    string Container,
    long CallerLogicalOffset,
    uint CallerRuntimeAddress,
    uint CalleeRuntimeAddress,
    string InstructionHex,
    bool DirectJal);

internal sealed record UnusedLevel65ReturnHomePropertiesClosure(
    int TrueIndex,
    uint SceneRelativePointer,
    int SceneRelativeOffset,
    long WadOffset,
    int ByteLength,
    UnusedLevel65ReturnHomeByteRegion Bytes,
    int NextDistinctPropertiesSceneOffset,
    int RowPointerFieldSceneOffset,
    int RowPointerFixupOccurrenceCount,
    IReadOnlyList<int> InternalPointerFixupFields,
    bool ExactOwnedExtent,
    bool PreservedByProposal);

internal sealed record UnusedLevel65ReturnHomeRowClosure(
    int TrueIndex,
    string Role,
    ushort ActorClass,
    UnusedLevel65ReturnHomeByteRegion TownSquareRow,
    UnusedLevel65ReturnHomeByteRegion RemoteBeforeRow,
    UnusedLevel65ReturnHomeByteRegion ProposedAfterRow,
    int BeforeRawX,
    int BeforeRawY,
    int BeforeRawZ,
    int ProposedRawX,
    int ProposedRawY,
    int ProposedRawZ,
    UnusedLevel65ReturnHomePropertiesClosure Properties,
    bool TownSquareAndRemoteArePhysicalStorageAliases,
    bool RowBytesOutsideXyzPreserved);

internal sealed record UnusedLevel65ReturnHomeActorClosure(
    ushort ActorId,
    int ActorRootIndex,
    int ActorRootDataRelativeOffset,
    int NextActorRootDataRelativeOffset,
    int ActorPackageByteLength,
    UnusedLevel65ReturnHomeByteRegion ActorPackage,
    bool PackageAlreadyOwnedById65,
    bool PackagePreservedByRemoteBlank,
    bool PackagePreservedByProposal,
    bool ActorCopyRequired);

internal sealed record UnusedLevel65ReturnHomeNativeClosure(
    UnusedLevel65ReturnHomeByteRegion Overlay,
    UnusedLevel65ReturnHomeByteRegion T96ClassDispatch,
    UnusedLevel65ReturnHomeByteRegion T96Handler,
    UnusedLevel65ReturnHomeByteRegion T96CommitPath,
    UnusedLevel65ReturnHomeCallEdge T96TransitionCall,
    UnusedLevel65ReturnHomeByteRegion T97ClassDispatch,
    UnusedLevel65ReturnHomeByteRegion T97SelectorJumpTable,
    IReadOnlyList<uint> T97SelectorJumpTargets,
    UnusedLevel65ReturnHomeByteRegion T97Handler,
    UnusedLevel65ReturnHomeByteRegion T97Selector8SoundTail,
    int T97Selector,
    int T97SoundIndex,
    UnusedLevel65ReturnHomeCallEdge T97SoundCall,
    UnusedLevel65ReturnHomeByteRegion RemoteParticleComponent,
    UnusedLevel65ReturnHomeByteRegion RemoteSoundComponent,
    bool T96ComputesHomeworldFromCurrentLevel,
    bool T96BypassesNormalPortalGuard,
    bool T97UsesPreservedPerLevelSoundMapping,
    bool RuntimeSoundIdentityVerified);

internal sealed record UnusedLevel65ReturnHomePauseExitClosure(
    UnusedLevel65ReturnHomeByteRegion ExitLevelLabel,
    UnusedLevel65ReturnHomeByteRegion ExitQuitDrawBranch,
    UnusedLevel65ReturnHomeByteRegion SelectionThreeBranch,
    UnusedLevel65ReturnHomeByteRegion ConfirmState,
    UnusedLevel65ReturnHomeByteRegion ExitSetupRoutine,
    UnusedLevel65ReturnHomeCallEdge ConfirmToExitSetupCall,
    uint CurrentLevelRuntimeAddress,
    uint TransitionTargetRuntimeAddress,
    uint RouteRuntimeAddress,
    int SourceLevelId,
    int ComputedDestinationLevelId,
    int ComputedRoute,
    bool ExitItemAvailableForId65,
    bool UsesNormalPortalGuard,
    bool RuntimeVerified);

internal sealed record UnusedLevel65ReturnHomeBootstrapClosure(
    UnusedLevel65ReturnHomeByteRegion WarpDispatchPointer,
    uint WarpDispatchRuntimeTarget,
    UnusedLevel65ReturnHomeByteRegion HiddenWarpSelectionPath,
    UnusedLevel65ReturnHomeByteRegion WarpTableUpperBound,
    int WarpTableUpperBoundImmediate,
    int MaximumAcceptedLevelId,
    UnusedLevel65ReturnHomeByteRegion BootstrapStores,
    UnusedLevel65ReturnHomeByteRegion NonFlightIdentity,
    uint CurrentLevelRuntimeAddress,
    uint TransitionTargetRuntimeAddress,
    bool HiddenWarpCanSelect65,
    bool ReentryIsTestOnly,
    bool ThisContractReverifiedPhysicalBootstrapRuntime);

internal sealed record UnusedLevel65ReturnHomeDestinationClosure(
    UnusedLevel65ReturnHomeByteRegion PortalEventJumpTable,
    UnusedLevel65ReturnHomeByteRegion PortalEventType6Entry,
    UnusedLevel65ReturnHomeByteRegion PortalDispatcherEntry,
    IReadOnlyList<UnusedLevel65ReturnHomeCallEdge> PortalDispatcherDirectCallers,
    UnusedLevel65ReturnHomeByteRegion PortalGuardPath,
    long GuardInstructionFileOffset,
    string GuardInstructionHex,
    int NormalPortalUpperExclusive,
    int GuardPatchBytesInProposal,
    UnusedLevel65ReturnHomeByteRegion SourceId65PortalTable,
    UnusedLevel65ReturnHomeByteRegion RemoteId65PortalTable,
    int Id65PortalCount,
    int Id65PortalAppendCapacityWithoutRelocation,
    UnusedLevel65ReturnHomeByteRegion GnastyWorldPortalTable,
    int GnastyWorldPortalCount,
    int GnastyWorldPortalUsedByteLength,
    int GnastyWorldPortalAppendCapacityWithoutRelocation,
    IReadOnlyList<int> GnastyWorldDestinationLevelIds,
    IReadOnlyList<int> CheckedInGnastyWorldDestinationLevelIds,
    bool GnastyWorldHasDestination65,
    bool RetailGnastyWorldMutationRequired,
    bool NormalArrivalRouteTo65Owned);

internal sealed record UnusedLevel65ReturnHomeCommonTransitionClosure(
    UnusedLevel65ReturnHomeByteRegion Routine,
    IReadOnlyList<UnusedLevel65ReturnHomeCallEdge> ExecutableDirectCallers,
    uint RuntimeAddress,
    uint CurrentLevelRuntimeAddress,
    uint TransitionTargetRuntimeAddress,
    bool RoutineBytesPatchedByProposal);

internal sealed record UnusedLevel65ReturnHomeLinkClosure(
    string TownSquareMetadataPath,
    string TownSquareMetadataSha256,
    string TownSquareGroupKey,
    IReadOnlyList<int> TownSquareTrueIndexes,
    string LabMetadataFileName,
    string RequiredLabGroupKey,
    string CanonicalLabGroupJson,
    string CanonicalLabGroupSha256,
    IReadOnlyList<int> RequiredTrueIndexes,
    IReadOnlyList<string> RequiredMemberIds,
    bool TownSquarePairLinkPresent,
    bool LabPairLinkPresent,
    bool LabMetadataInstallRequired,
    bool LinkedEditorMoveRuntimeVerified);

internal sealed record UnusedLevel65ReturnHomePlacementClosure(
    int GroundRawZ,
    IReadOnlyList<UnusedLevel65RemoteBlankPoint> PadPoints,
    int SpawnRawX,
    int SpawnRawY,
    int SpawnRawZ,
    int T96RawX,
    int T96RawY,
    int T96RawZ,
    int T97RawX,
    int T97RawY,
    int T97RawZ,
    int T97AboveT96Raw,
    double HorizontalSpawnDistanceWorld,
    double ThreeDimensionalSpawnDistanceWorld,
    double NearestPadEdgeMarginWorld,
    int NativeNearTriggerRaw,
    double NativeNearTriggerWorld,
    double HorizontalNoImmediateTriggerMarginWorld,
    IReadOnlyList<int> ChangedDataByteOffsets,
    int LogicalXyzPatchBytes,
    int ChangedDataByteCount,
    string XyzPatchManifestHex,
    string XyzPatchManifestSha256,
    string ProposedObjectTableSha256,
    string ProposedDataSha256,
    bool T96InsideAuthoredPad,
    bool T97InsideAuthoredPad,
    bool SpawnOutsideNativeNearTrigger,
    bool VisualFootprintRuntimeVerified,
    bool ActivationRuntimeVerified);

internal sealed record UnusedLevel65ReturnHomeRuntimeGate(
    int Order,
    string Id,
    string Requirement,
    bool Passed);

internal sealed record UnusedLevel65ReturnHomeTransaction(
    string Id,
    int Id65RowCount,
    IReadOnlyList<int> Id65TrueIndexes,
    int LogicalWadPatchBytes,
    int ExecutablePatchBytes,
    int RetailWadPatchBytes,
    int ChangedDataBytes,
    IReadOnlyList<string> RequiredSidecars,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> RuntimeOnlyGates,
    string DeterministicManifestSha256,
    bool StaticDependencyClosureComplete,
    bool DestinationSafe,
    bool RetailLevelsPreserved,
    bool WriterAuthorized,
    bool RuntimeCandidateAuthorized,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65ReturnHomeOwnershipContract(
    string ProfileId,
    string LockedBaseImageSha256,
    string RemoteBlankProfileId,
    string RemoteBlankOutputDataSha256,
    string ExecutableSha256,
    string OverlaySha256,
    UnusedLevel65ReturnHomeByteRegion ScenePointerFixups,
    UnusedLevel65ReturnHomeRowClosure Visible,
    UnusedLevel65ReturnHomeRowClosure Helper,
    UnusedLevel65ReturnHomeActorClosure Actor,
    UnusedLevel65ReturnHomeNativeClosure Native,
    UnusedLevel65ReturnHomePauseExitClosure PauseExit,
    UnusedLevel65ReturnHomeBootstrapClosure Bootstrap,
    UnusedLevel65ReturnHomeDestinationClosure Destinations,
    UnusedLevel65ReturnHomeCommonTransitionClosure CommonTransition,
    UnusedLevel65ReturnHomeLinkClosure Link,
    UnusedLevel65ReturnHomePlacementClosure Placement,
    UnusedLevel65ReturnHomeTransaction Transaction,
    IReadOnlyList<UnusedLevel65ReturnHomeRuntimeGate> RuntimeGates,
    bool LockedImagePreserved,
    bool RemotePlanInMemoryOnly,
    bool StaticReadbackOnly,
    bool WritesBin,
    bool WritesCue,
    bool WritesMetadata,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool ReleaseAuthorized);

/// <summary>
/// Exact read-only ownership closure for the first ID65 exit transaction. The
/// transaction moves the already-owned Town Square T96/T97 Return Home bundle
/// onto the remote blank pad and defines the lab-only linked-move sidecar. Both
/// native Return Home and Pause Exit calculate floor(currentLevel / 10) * 10,
/// so ID65 targets level 60 and does not require the normal portal &lt;65 guard,
/// a new Gnasty destination, or any retail-level mutation.
/// </summary>
internal static class UnusedLevel65ReturnHomeOwnershipInspector
{
    public const string ProfileId =
        "unused-level-65-return-home-exit-static-closure-clean-usa-v1";
    public const string LockedBaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string OverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    public const string RemoteBlankOutputDataSha256 =
        "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061";
    public const string ProposedOutputDataSha256 =
        "62ce6b2c39ae13236a44e05b89baa305fedeaf51b09b7abf8b9e863b2910a879";
    public const string ProposedObjectTableSha256 =
        "f385b221a54cd92c59c2dc7ca60670956c463093d98768e663a52edbb3572a58";
    public const string ExpectedTransactionManifestSha256 =
        "e97285d63c700172caf343e96de532ecc4605043dde24fa8c6940cb7e8ddf381";

    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const uint ExecutableRuntimeBias = 0x8000F800;
    private const long OverlayWadOffset = 0x6927000;
    private const int OverlayByteLength = 0xF800;
    private const uint OverlayRuntimeAddress = 0x8007AA38;
    private const long DataWadOffset = 0x6936800;
    private const int DataByteLength = 0x2E2000;
    private const int SceneDataRelativeOffset = 0x1D0000;
    private const long SceneWadOffset = 0x6B06800;
    private const int ObjectTableDataRelativeOffset = 0x1D0170;
    private const long Id65ObjectTableWadOffset = 0x6B06970;
    private const long TownSquareObjectTableWadOffset = 0x136E170;
    private const int ObjectRecordCount = 107;
    private const int ObjectRecordByteLength = 0x58;
    private const int T96 = 96;
    private const int T97 = 97;
    private const int T96PropsSceneOffset = 0x5CCC;
    private const int T97PropsSceneOffset = 0x5CDC;
    private const int NextPropsSceneOffset = 0x5D3C;
    private const int SceneFixupCountOffset = 0x7F28;
    private const int SceneFixupCount = 0x81;
    private const int ActorRootIndex = 15;
    private const int ActorRootOffset = 0x1C4F28;
    private const int NextActorRootOffset = 0x1C5790;
    private const int ProposedRawX = 6_144;
    private const int ProposedRawY = 7_296;
    private const int ProposedT96RawZ = 8_192;
    private const int ProposedT97RawZ = 8_704;
    private const int NativeNearTriggerRaw = 0x400;

    private const string CanonicalLabGroupJson =
        "{\"key\":\"unusedlevel65blank:control-role-proof:t97\",\"name\":\"ID65 Return Home exit bundle\",\"kind\":\"linked group\",\"linkedMove\":true,\"confidence\":\"native-static-closure-runtime-pending\",\"basis\":\"T96 is the visible class-0x0009 Return Home controller; T97 is its colocated class-0x011E selector-8 sound helper.\",\"reason\":\"Move, copy, paste, or delete T96 and T97 atomically in the ID65 Blank-Level Lab.\",\"trueIndexes\":[97,96],\"memberIds\":[\"T97\",\"T96\"]}";
    private const string CanonicalLabGroupSha256 =
        "207f316ef796c1830a558a805cfca82170dc2b6c81610923e90ede3ed1989e53";

    public static UnusedLevel65ReturnHomeOwnershipContract Inspect(
        string workspaceRoot,
        string lockedBaseImagePath)
    {
        string root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "spyro-level-catalog.json")))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string imagePath = Path.GetFullPath(lockedBaseImagePath);
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The exact locked ID65 display-name BIN is missing.", imagePath);
        RequireHash(HashFile(imagePath), LockedBaseImageSha256, "locked ID65 display-name BIN");

        UnusedLevel65RemoteBlankStaticPlan remote =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(imagePath);
        if (remote.ProfileId != UnusedLevel65RemoteBlankIsolationConstruction.ProfileId ||
            remote.OutputDataSha256 != RemoteBlankOutputDataSha256 ||
            remote.OutputDataSha256 != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256 ||
            remote.OutputData.Length != DataByteLength ||
            !remote.InheritedObjectRowsPreserved || !remote.ProtectedSubfilesPreserved ||
            !remote.RetailWadEntriesExcluded || !remote.ExecutableExcluded || remote.SourceImageMutationPossible)
        {
            throw new InvalidDataException("The exact in-memory remote-blank substrate changed.");
        }

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The locked ID65 image is not exact MODE2/2352.");
        using FileStream image = File.OpenRead(imagePath);
        _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord executableRecord = RequireRootFile(
            image, layout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        byte[] executable = DiscImage.ReadFileBytes(
            image, layout, executableRecord.Lba, 0, executableRecord.Size);
        RequireHash(Hash(executable), ExecutableSha256, "SCUS_942.28");
        byte[] overlay = DiscImage.ReadFileBytes(
            image, layout, WadLba, OverlayWadOffset, OverlayByteLength);
        RequireHash(Hash(overlay), OverlaySha256, "ID65 overlay");
        byte[] townSquareRows = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            TownSquareObjectTableWadOffset,
            ObjectRecordCount * ObjectRecordByteLength);
        RequireHash(
            Hash(townSquareRows),
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceObjectTableSha256,
            "Town Square object table");

        byte[] remoteRows = remote.OutputData.AsSpan(
            ObjectTableDataRelativeOffset,
            ObjectRecordCount * ObjectRecordByteLength).ToArray();
        RequireHash(
            Hash(remoteRows),
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputObjectTableSha256,
            "remote ID65 object table");
        byte[] townT96 = SliceRow(townSquareRows, T96);
        byte[] townT97 = SliceRow(townSquareRows, T97);
        byte[] remoteT96 = SliceRow(remoteRows, T96);
        byte[] remoteT97 = SliceRow(remoteRows, T97);
        RequireHash(Hash(townT96), "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4", "Town Square T96");
        RequireHash(Hash(townT97), "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc", "Town Square T97");
        if (!townT96.SequenceEqual(remoteT96) || !townT97.SequenceEqual(remoteT97))
            throw new InvalidDataException("The remote-blank construction changed the inherited T96/T97 bundle.");

        byte[] proposal = remote.OutputData.ToArray();
        PatchXyz(proposal, T96, ProposedRawX, ProposedRawY, ProposedT96RawZ);
        PatchXyz(proposal, T97, ProposedRawX, ProposedRawY, ProposedT97RawZ);
        byte[] proposedRows = proposal.AsSpan(
            ObjectTableDataRelativeOffset,
            ObjectRecordCount * ObjectRecordByteLength).ToArray();
        byte[] proposedT96 = SliceRow(proposedRows, T96);
        byte[] proposedT97 = SliceRow(proposedRows, T97);
        RequireHash(Hash(proposedT96), "6e6bd67a7c8bc6ae46dcb003729f02bdf836ecd86075f2f48b1c6931fab0b721", "proposed T96");
        RequireHash(Hash(proposedT97), "e7b1b6d84197094fd59b59dc3aaa773a7d05113c4574bd1e9b468c16a05ca355", "proposed T97");
        RequireHash(Hash(proposedRows), ProposedObjectTableSha256, "proposed ID65 object table");
        RequireHash(Hash(proposal), ProposedOutputDataSha256, "proposed ID65 row-80 data");
        RequireOnlyXyzChanged(remote.OutputData, proposal);

        uint t96PropsPointer = ReadUInt32(remoteT96, 0);
        uint t97PropsPointer = ReadUInt32(remoteT97, 0);
        if (t96PropsPointer != T96PropsSceneOffset || t97PropsPointer != T97PropsSceneOffset)
            throw new InvalidDataException("T96/T97 scene-property pointers changed.");
        byte[] t96Props = remote.OutputData.AsSpan(
            SceneDataRelativeOffset + T96PropsSceneOffset,
            T97PropsSceneOffset - T96PropsSceneOffset).ToArray();
        byte[] t97Props = remote.OutputData.AsSpan(
            SceneDataRelativeOffset + T97PropsSceneOffset,
            NextPropsSceneOffset - T97PropsSceneOffset).ToArray();
        RequireHash(Hash(t96Props), "231f5cecc61699ca90f5e9acdbc4c47552f66daeb7ef55327cb7574cda9f29e5", "T96 properties");
        RequireHash(Hash(t97Props), "10539eeae9088ffaa94a6199b044c5478b8602c4144144f8b581514fa71415c1", "T97 properties closure");
        RequireHex(t96Props, "FFFFFFFF000000000000000000000000", "T96 properties");
        RequireHex(
            t97Props,
            "0800000008000000045D000000010000000000000001000000000000000000000000000000000000030000000000FFFF660B02000A130200003000000000000048F601005C0B0200003000000000000048050200A4FE01000030000000000000",
            "T97 properties closure");

        int fixupLength = sizeof(uint) + SceneFixupCount * sizeof(uint);
        byte[] fixups = remote.OutputData.AsSpan(
            SceneDataRelativeOffset + SceneFixupCountOffset,
            fixupLength).ToArray();
        if (ReadInt32(fixups, 0) != SceneFixupCount)
            throw new InvalidDataException("The ID65 scene pointer-fixup count changed.");
        RequireHash(Hash(fixups), "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2", "scene pointer fixups");
        int[] fixupFields = Enumerable.Range(0, SceneFixupCount)
            .Select(index => ReadInt32(fixups, 4 + index * 4)).ToArray();
        int t96RowPointerField = 0x170 + T96 * ObjectRecordByteLength;
        int t97RowPointerField = 0x170 + T97 * ObjectRecordByteLength;
        int t97NestedPointerField = T97PropsSceneOffset + 8;
        if (fixupFields.Count(value => value == t96RowPointerField) != 1 ||
            fixupFields.Count(value => value == t97RowPointerField) != 1 ||
            fixupFields.Count(value => value == t97NestedPointerField) != 1)
        {
            throw new InvalidDataException("The T96/T97 row or nested-property fixup closure changed.");
        }

        ushort actorId = ReadUInt16(remoteT96, 0x36);
        int actorRoot = ReadInt32(remote.OutputData, 0x50 + ActorRootIndex * 4);
        int nextActorRoot = ReadInt32(remote.OutputData, 0x50 + (ActorRootIndex + 1) * 4);
        ushort rootActorId = ReadUInt16(remote.OutputData, 0x150 + ActorRootIndex * 2);
        if (actorId != 0x0009 || rootActorId != actorId ||
            actorRoot != ActorRootOffset || nextActorRoot != NextActorRootOffset)
        {
            throw new InvalidDataException("The ID65 actor-0x0009 root closure changed.");
        }
        byte[] actorPackage = remote.OutputData.AsSpan(actorRoot, nextActorRoot - actorRoot).ToArray();
        RequireHash(Hash(actorPackage), "cb95609bfa6cec1ced0428d83b33cad96b59c9e803133e7002f2a9c776bdb182", "actor 0x0009 package");

        UnusedLevel65RemoteBlankComponentProof portalComponent =
            remote.Components.Single(component => component.Name == "portal table");
        UnusedLevel65RemoteBlankComponentProof particleComponent =
            remote.Components.Single(component => component.Name == "particles");
        UnusedLevel65RemoteBlankComponentProof soundComponent =
            remote.Components.Single(component => component.Name == "sound");
        if (portalComponent.OutputRelativeOffset != 0x93E00 || portalComponent.ByteLength != 4 ||
            particleComponent.OutputRelativeOffset != 0x93E04 || particleComponent.ByteLength != 0x80 ||
            soundComponent.OutputRelativeOffset != 0x93E84 || soundComponent.ByteLength != 0x6F8 ||
            !portalComponent.ContentsPreserved || !particleComponent.ContentsPreserved || !soundComponent.ContentsPreserved)
        {
            throw new InvalidDataException("The remote portal/particle/sound component closure changed.");
        }
        byte[] remotePortalBytes = SliceRemoteComponent(remote, portalComponent);
        byte[] remoteParticleBytes = SliceRemoteComponent(remote, particleComponent);
        byte[] remoteSoundBytes = SliceRemoteComponent(remote, soundComponent);
        RequireHex(remotePortalBytes, "00000000", "remote zero portal table");
        RequireHash(Hash(remoteParticleBytes), "e4c92864f220a33d8c5d2fc158806fc0822dd01d3eb95ef8932505d72570a0ce", "remote particles");
        RequireHash(Hash(remoteSoundBytes), "51eebb756d1ad97f8904da785a4f4585f353259f73f20037d6c5694833fab4fa", "remote sound component");

        UnusedLevel65ReturnHomeByteRegion overlayRegion = WadRegion(
            "ID65 overlay", OverlayWadOffset, overlay, layout, OverlayRuntimeAddress, true, includeHex: false);
        UnusedLevel65ReturnHomeByteRegion t96Class = OverlayRegion(
            overlay, layout, 0x8007DB74, 8, "T96 class-0x0009 dispatch", "51941619d361ac6e7bbb5454ad5b1cafee6ac842e3caceaa20d9774658f78806", "0900092487008910");
        UnusedLevel65ReturnHomeByteRegion t96Handler = OverlayRegion(
            overlay, layout, 0x8007DD98, 0x304, "T96 Return Home handler", "bfbf78f6edea427384ee73e874cfba832dfdc73013f46dbe96532fc09dbc632e");
        UnusedLevel65ReturnHomeByteRegion t96Commit = OverlayRegion(
            overlay, layout, 0x8007DF98, 0x104, "T96 Return Home commit path", "ec08bdc5a28197aed9c8fc9b4740b4c8d917b7fd8e2221145912b538edf04fbc");
        UnusedLevel65ReturnHomeCallEdge t96TransitionCall = RequireCall(
            overlay,
            "ID65 overlay",
            OverlayWadOffset,
            OverlayRuntimeAddress,
            0x8007E07C,
            0x8004AC24,
            "092B010C");
        UnusedLevel65ReturnHomeByteRegion t97Class = OverlayRegion(
            overlay, layout, 0x8007DCB4, 8, "T97 class-0x011E dispatch", "123e342e594a68b4181ab12eafc7cf872cfee729b217c12168110c064d2f101c", "1E010224111B8210");
        UnusedLevel65ReturnHomeByteRegion t97JumpTable = OverlayRegion(
            overlay, layout, 0x8007AC34, 40, "T97 selector jump table", "d9dceaafe1dcf71968345751fb2b797d4ee79cdcf18296d9066d935e63b9e695");
        uint[] t97JumpTargets = Enumerable.Range(0, 10)
            .Select(index => ReadUInt32(overlay, checked((int)(0x8007AC34 - OverlayRuntimeAddress)) + index * 4))
            .ToArray();
        uint[] expectedJumpTargets =
        [
            0x80084944, 0x8008494C, 0x80084988, 0x80084C40, 0x80084C7C,
            0x80084CD0, 0x80084CD8, 0x80084CE0, 0x80084CE8, 0x80084CF0
        ];
        if (!t97JumpTargets.SequenceEqual(expectedJumpTargets))
            throw new InvalidDataException("The T97 selector jump targets changed.");
        UnusedLevel65ReturnHomeByteRegion t97Handler = OverlayRegion(
            overlay, layout, 0x80084900, 0x420, "T97 class-0x011E handler", "15de22eddf6b203f0de586af8fc62954bf53f3d38fc65f3510d68884fbe16ce5");
        UnusedLevel65ReturnHomeByteRegion t97SoundTail = OverlayRegion(
            overlay, layout, 0x80084CE8, 0x38, "T97 selector-8 sound tail", "82281fd28b200e4ecf2ddf808d8b0e8f6adfaf89c0fe8dfdcfa347022ce203e6");
        UnusedLevel65ReturnHomeCallEdge t97SoundCall = RequireCall(
            overlay,
            "ID65 overlay",
            OverlayWadOffset,
            OverlayRuntimeAddress,
            0x80084D10,
            0x80055A78,
            "9E56010C");

        UnusedLevel65ReturnHomeByteRegion commonTransition = ExeRegion(
            executable, layout, 0x3B424, 0x210, "common level transition", "8d9ece151795f729d59997279a51b0a68af001319d8ba0c09157633e9b7190e3");
        UnusedLevel65ReturnHomeCallEdge[] commonTransitionCallers =
            FindDirectJalCallers(executable, 0x8004AC24);
        long[] expectedCommonCallers =
        [
            0x40B0, 0x1CE44, 0x1D3D0, 0x1D578, 0x1D864, 0x1DA8C,
            0x1DF9C, 0x1F7D4, 0x1FF64, 0x23464, 0x23958
        ];
        if (!commonTransitionCallers.Select(edge => edge.CallerLogicalOffset).SequenceEqual(expectedCommonCallers))
            throw new InvalidDataException("The common transition direct-call graph changed.");

        UnusedLevel65ReturnHomeByteRegion pauseLabel = ExeRegion(
            executable, layout, 0x1384, 11, "EXIT LEVEL label", "702884200ffaca7c376437b5f3f3bd8b56dda87ba90464cf7486c1afe9e6e951", "45584954204C4556454C00");
        UnusedLevel65ReturnHomeByteRegion pauseDraw = ExeRegion(
            executable, layout, 0xBD1C, 0xCC, "pause EXIT LEVEL / QUIT GAME draw branch", "b96bd20547cfccbef821cb2e39002d57173324dc4f71ccd90eb0d839e15e155a");
        UnusedLevel65ReturnHomeByteRegion pauseSelection = ExeRegion(
            executable, layout, 0x1F244, 0x68, "pause selection-3 exit branch", "6872e3cda7a45306b46c7459895b7e1859a3c8c423dd5afe05e00514fbcc5247");
        UnusedLevel65ReturnHomeByteRegion pauseConfirm = ExeRegion(
            executable, layout, 0x1E884, 0xA8, "pause exit confirmation state", "20f73ff46edd435081cd989cc8726328ff63c02abd0b46387840793c3262a0cb");
        UnusedLevel65ReturnHomeByteRegion pauseSetup = ExeRegion(
            executable, layout, 0x1CE64, 0xB0, "pause exit destination setup", "17c22be3523fbd4e6730d25d24d5ad80530185d67d2fc4fc9ccceabd5385f423");
        UnusedLevel65ReturnHomeCallEdge pauseConfirmCall = RequireCall(
            executable,
            "SCUS_942.28",
            0,
            ExecutableRuntimeBias,
            0x8002E0E8,
            0x8002C664,
            "99B1000C");
        UnusedLevel65ReturnHomeCallEdge[] pauseSetupCallers =
            FindDirectJalCallers(executable, 0x8002C664);
        if (pauseSetupCallers.Length != 1 || pauseSetupCallers[0] != pauseConfirmCall)
            throw new InvalidDataException("The pause Exit Level setup call graph changed.");

        UnusedLevel65ReturnHomeByteRegion warpPointer = ExeRegion(
            executable, layout, 0x1CA8, 4, "ID65 hidden-warp dispatch pointer", "ccfb79978e5c783787b25a01861dbc27512e60dc5fa2e75e3a381ea4212a59e2", "98A80580");
        UnusedLevel65ReturnHomeByteRegion hiddenWarpSelection = ExeRegion(
            executable, layout, 0x1DD80, 0x280, "hidden-warp selection and transition path", "d548119aafb2f4396cdaa3b924d21b38158d02c9239116ac7237ead626b2ddf3");
        UnusedLevel65ReturnHomeByteRegion warpBound = ExeRegion(
            executable, layout, 0x1DF44, 4, "hidden-warp level upper bound", "f1e02d95226f9d58c7973c84dc607a610b1f7aa6071dd3c95111e5fa40f3be4a", "3800422C");
        UnusedLevel65ReturnHomeByteRegion bootstrapStores = ExeRegion(
            executable, layout, 0x1E034, 8, "ID65 bootstrap stores", "3bd88843c67ec5a70653f9d45e867ebd03924f5795a7edeef1af7b8eedb305df", "1C0684AF880680AF");
        UnusedLevel65ReturnHomeByteRegion nonFlightIdentity = ExeRegion(
            executable, layout, 0x40C0, 4, "ID65 non-flight identity", "9a40aeca0b14d040ab341eec02b0f7b3e37e4fdbb0c9120eaf573a2266b2e5c9", "005E023C");

        UnusedLevel65ReturnHomeByteRegion portalJumpTable = ExeRegion(
            executable, layout, 0x1B80, 0x28, "portal event jump table", "4e0ffe15100a8c84ba612ef624f4ec78eba729bb6a4a9a14b5443c796ef705af");
        UnusedLevel65ReturnHomeByteRegion portalType6 = ExeRegion(
            executable, layout, 0x1B98, 4, "portal event type-6 entry", "5157aa2886f5f1e8507612db705fa045490d35ef355d7541a270a2832f3fb045", "EC700580");
        UnusedLevel65ReturnHomeByteRegion portalDispatcher = ExeRegion(
            executable, layout, 0x47764, 0x20, "normal portal event dispatcher entry", "ec2a339f097bdb7241eab3cf1be7cffbf2857912d91c49eb11d9e754e5a6e8df");
        UnusedLevel65ReturnHomeCallEdge[] portalDispatcherCallers =
            FindDirectJalCallers(executable, 0x80056F64);
        if (!portalDispatcherCallers.Select(edge => edge.CallerLogicalOffset).SequenceEqual(new long[] { 0x394F4 }) ||
            portalDispatcherCallers[0].InstructionHex != "D95B010C")
        {
            throw new InvalidDataException("The normal portal dispatcher call graph changed.");
        }
        UnusedLevel65ReturnHomeByteRegion portalGuard = ExeRegion(
            executable, layout, 0x47904, 0x34, "normal portal type-6 guard path", "32b07aa096fba6f638f262fdda1ce37e9d903ca73382953025030f70f0ef0eb8");
        RequireHex(executable.AsSpan(0x47910, 4), "41004228", "normal portal <65 guard");

        LevelCatalog catalog = LevelCatalog.Load(root);
        if (catalog.Levels.Count != 35)
            throw new InvalidDataException("The exact 35-level retail catalog is required.");
        LevelDefinition gnastyWorld = catalog.FindByKey("gnastysworld")
            ?? throw new InvalidDataException("Gnasty's World is missing from the retail catalog.");
        PortalSourceLevelData gnasty = PortalSourceDataLocator.Locate(imagePath, gnastyWorld);
        int[] gnastyDestinations = gnasty.Portals.Select(portal => portal.DestinationLevelId).ToArray();
        if (gnasty.PortalTableWadOffset != 0x52F280C ||
            !gnastyDestinations.SequenceEqual(new[] { 62, 63, 64, 61 }))
        {
            throw new InvalidDataException("The exact Gnasty's World portal table changed.");
        }
        int gnastyPortalUsedLength = ReadPortalTableUsedLength(
            image,
            layout,
            gnasty.PortalTableWadOffset,
            expectedCount: 4);
        if (gnastyPortalUsedLength != 0x2B910)
            throw new InvalidDataException("The Gnasty's World portal-table extent changed.");
        byte[] gnastyPortalBytes = DiscImage.ReadFileBytes(
            image, layout, WadLba, gnasty.PortalTableWadOffset, gnastyPortalUsedLength);
        RequireHash(Hash(gnastyPortalBytes), "4da8159eb38b8b77d48b9ce8ddcec54c2fd7389a79ae88d97c892d43da70e12d", "Gnasty's World portal table");
        byte[] gnastyFollowingComponent = DiscImage.ReadFileBytes(
            image, layout, WadLba, gnasty.PortalTableWadOffset + gnastyPortalUsedLength, 4);
        RequireHex(gnastyFollowingComponent, "74000000", "component immediately following Gnasty portals");
        int[] checkedInGnastyDestinations = HomeworldPortalControlCatalog.ForLevel("gnastysworld")
            .Select(item => item.DestinationLevelId).ToArray();
        if (!checkedInGnastyDestinations.SequenceEqual(new[] { 61, 62, 63, 64 }))
            throw new InvalidDataException("The checked-in Gnasty portal controls changed.");

        string townMetadataPath = Path.Combine(root, "townsquare-behavior-links.json");
        if (!File.Exists(townMetadataPath))
            throw new FileNotFoundException("Town Square behavior-link metadata is missing.", townMetadataPath);
        const string townMetadataSha = "b1d824d2846188983029b61fc1303eb76336c8bc5926526359987b4672bcf93f";
        RequireHash(HashFile(townMetadataPath), townMetadataSha, "Town Square behavior-link metadata");
        bool townLink = HasLinkGroup(
            townMetadataPath,
            "townsquare:control-role-proof:t97",
            [97, 96],
            ["T97", "T96"]);
        string labMetadataPath = Path.Combine(root, "unusedlevel65blank-behavior-links.json");
        string promotedLabMetadataPath = Path.Combine(
            root,
            "_local",
            "control-role-proof-review",
            "promoted-behavior-links",
            "unusedlevel65blank-behavior-links.json");
        bool labLink = File.Exists(labMetadataPath) || File.Exists(promotedLabMetadataPath);
        RequireHash(Hash(Encoding.UTF8.GetBytes(CanonicalLabGroupJson)), CanonicalLabGroupSha256, "canonical ID65 lab link group");

        UnusedLevel65ReturnHomePropertiesClosure t96Properties = new(
            T96,
            t96PropsPointer,
            T96PropsSceneOffset,
            SceneWadOffset + T96PropsSceneOffset,
            t96Props.Length,
            MemoryWadRegion("remote T96 properties", SceneWadOffset + T96PropsSceneOffset, t96Props),
            T97PropsSceneOffset,
            t96RowPointerField,
            1,
            [],
            ExactOwnedExtent: true,
            PreservedByProposal: proposal.AsSpan(SceneDataRelativeOffset + T96PropsSceneOffset, t96Props.Length).SequenceEqual(t96Props));
        UnusedLevel65ReturnHomePropertiesClosure t97Properties = new(
            T97,
            t97PropsPointer,
            T97PropsSceneOffset,
            SceneWadOffset + T97PropsSceneOffset,
            t97Props.Length,
            MemoryWadRegion("remote T97 properties closure", SceneWadOffset + T97PropsSceneOffset, t97Props),
            NextPropsSceneOffset,
            t97RowPointerField,
            1,
            [t97NestedPointerField],
            ExactOwnedExtent: true,
            PreservedByProposal: proposal.AsSpan(SceneDataRelativeOffset + T97PropsSceneOffset, t97Props.Length).SequenceEqual(t97Props));

        UnusedLevel65ReturnHomeRowClosure visible = BuildRowClosure(
            T96,
            "visible Return Home controller",
            townT96,
            remoteT96,
            proposedT96,
            t96Properties,
            layout,
            expectedActorClass: 0x0009);
        UnusedLevel65ReturnHomeRowClosure helper = BuildRowClosure(
            T97,
            "selector-8 Return Home sound helper",
            townT97,
            remoteT97,
            proposedT97,
            t97Properties,
            layout,
            expectedActorClass: 0x011E);

        UnusedLevel65ReturnHomeActorClosure actor = new(
            actorId,
            ActorRootIndex,
            actorRoot,
            nextActorRoot,
            actorPackage.Length,
            MemoryWadRegion("ID65 actor-0x0009 package", DataWadOffset + actorRoot, actorPackage, includeHex: false),
            PackageAlreadyOwnedById65: true,
            PackagePreservedByRemoteBlank: remote.SourceData.AsSpan(actorRoot, actorPackage.Length).SequenceEqual(actorPackage),
            PackagePreservedByProposal: proposal.AsSpan(actorRoot, actorPackage.Length).SequenceEqual(actorPackage),
            ActorCopyRequired: false);

        UnusedLevel65ReturnHomeNativeClosure native = new(
            overlayRegion,
            t96Class,
            t96Handler,
            t96Commit,
            t96TransitionCall,
            t97Class,
            t97JumpTable,
            t97JumpTargets,
            t97Handler,
            t97SoundTail,
            T97Selector: 8,
            T97SoundIndex: 0x1A,
            t97SoundCall,
            MemoryWadRegion(
                "remote particles",
                remote.ModelWadOffset + particleComponent.OutputRelativeOffset,
                remoteParticleBytes,
                includeHex: false),
            MemoryWadRegion(
                "remote sound component",
                remote.ModelWadOffset + soundComponent.OutputRelativeOffset,
                remoteSoundBytes,
                includeHex: false),
            T96ComputesHomeworldFromCurrentLevel: true,
            T96BypassesNormalPortalGuard: true,
            T97UsesPreservedPerLevelSoundMapping: true,
            RuntimeSoundIdentityVerified: false);

        UnusedLevel65ReturnHomePauseExitClosure pauseExit = new(
            pauseLabel,
            pauseDraw,
            pauseSelection,
            pauseConfirm,
            pauseSetup,
            pauseConfirmCall,
            CurrentLevelRuntimeAddress: 0x8007596C,
            TransitionTargetRuntimeAddress: 0x800758B4,
            RouteRuntimeAddress: 0x8007576C,
            SourceLevelId: 65,
            ComputedDestinationLevelId: HomeworldFor(65),
            ComputedRoute: -1,
            ExitItemAvailableForId65: HomeworldFor(65) != 65,
            UsesNormalPortalGuard: false,
            RuntimeVerified: false);

        UnusedLevel65ReturnHomeBootstrapClosure bootstrap = new(
            warpPointer,
            WarpDispatchRuntimeTarget: ReadUInt32(executable, 0x1CA8),
            hiddenWarpSelection,
            warpBound,
            WarpTableUpperBoundImmediate: 0x38,
            MaximumAcceptedLevelId: 65,
            bootstrapStores,
            nonFlightIdentity,
            CurrentLevelRuntimeAddress: 0x8007596C,
            TransitionTargetRuntimeAddress: 0x800758B4,
            HiddenWarpCanSelect65: true,
            ReentryIsTestOnly: true,
            ThisContractReverifiedPhysicalBootstrapRuntime: false);
        if (bootstrap.WarpDispatchRuntimeTarget != 0x8005A898)
            throw new InvalidDataException("The ID65 hidden-warp dispatch target changed.");

        UnusedLevel65ReturnHomeDestinationClosure destinations = new(
            portalJumpTable,
            portalType6,
            portalDispatcher,
            portalDispatcherCallers,
            portalGuard,
            GuardInstructionFileOffset: 0x47910,
            GuardInstructionHex: "41004228",
            NormalPortalUpperExclusive: 65,
            GuardPatchBytesInProposal: 0,
            MemoryWadRegion(
                "source ID65 zero portal table",
                remote.ModelWadOffset + portalComponent.SourceRelativeOffset,
                remote.SourceData.AsSpan(
                    checked((int)(remote.ModelWadOffset + portalComponent.SourceRelativeOffset - remote.DataWadOffset)),
                    portalComponent.ByteLength).ToArray()),
            MemoryWadRegion(
                "remote ID65 zero portal table",
                remote.ModelWadOffset + portalComponent.OutputRelativeOffset,
                remotePortalBytes),
            Id65PortalCount: 0,
            Id65PortalAppendCapacityWithoutRelocation: 0,
            WadRegion(
                "Gnasty's World four-portal table",
                gnasty.PortalTableWadOffset,
                gnastyPortalBytes,
                layout,
                runtimeAddress: null,
                hasPhysicalImage: true,
                includeHex: false),
            GnastyWorldPortalCount: 4,
            GnastyWorldPortalUsedByteLength: gnastyPortalUsedLength,
            GnastyWorldPortalAppendCapacityWithoutRelocation: 0,
            gnastyDestinations,
            checkedInGnastyDestinations,
            GnastyWorldHasDestination65: false,
            RetailGnastyWorldMutationRequired: false,
            NormalArrivalRouteTo65Owned: false);

        UnusedLevel65ReturnHomeCommonTransitionClosure common = new(
            commonTransition,
            commonTransitionCallers,
            RuntimeAddress: 0x8004AC24,
            CurrentLevelRuntimeAddress: 0x8007596C,
            TransitionTargetRuntimeAddress: 0x800758B4,
            RoutineBytesPatchedByProposal: false);

        UnusedLevel65ReturnHomeLinkClosure link = new(
            Path.GetRelativePath(root, townMetadataPath),
            townMetadataSha,
            "townsquare:control-role-proof:t97",
            [97, 96],
            "unusedlevel65blank-behavior-links.json",
            "unusedlevel65blank:control-role-proof:t97",
            CanonicalLabGroupJson,
            CanonicalLabGroupSha256,
            [97, 96],
            ["T97", "T96"],
            TownSquarePairLinkPresent: townLink,
            LabPairLinkPresent: labLink,
            LabMetadataInstallRequired: !labLink,
            LinkedEditorMoveRuntimeVerified: false);
        if (!link.TownSquarePairLinkPresent || link.LabPairLinkPresent)
            throw new InvalidDataException("The expected Town Square-present / ID65-lab-absent link boundary changed.");

        int[] changedOffsets = DifferentByteIndexes(remote.OutputData, proposal);
        byte[] xyzManifest =
        [
            .. proposedT96.AsSpan(0x0C, 12),
            .. proposedT97.AsSpan(0x0C, 12)
        ];
        RequireHash(Hash(xyzManifest), "de79cdfb280010a1fc3f64e46bb0b58d61169a4a0be0e5744a0e2b912a24307d", "T96/T97 XYZ manifest");
        double horizontalDistance = Math.Sqrt(
            Math.Pow(remote.Spawn.PlayerRawX - ProposedRawX, 2) +
            Math.Pow(remote.Spawn.PlayerRawY - ProposedRawY, 2)) / 16.0;
        double threeDimensionalDistance = Math.Sqrt(
            Math.Pow(remote.Spawn.PlayerRawX - ProposedRawX, 2) +
            Math.Pow(remote.Spawn.PlayerRawY - ProposedRawY, 2) +
            Math.Pow(remote.Spawn.PlayerRawZ - ProposedT96RawZ, 2)) / 16.0;
        double padMargin = DistanceToTriangleEdges(
            ProposedRawX / 16.0,
            ProposedRawY / 16.0,
            remote.Scene.Points);
        bool t96Inside = PointInTriangle(
            ProposedRawX / 16.0,
            ProposedRawY / 16.0,
            remote.Scene.Points);
        bool t97Inside = t96Inside && ProposedT97RawZ >= ProposedT96RawZ;
        UnusedLevel65ReturnHomePlacementClosure placement = new(
            remote.Spawn.GroundRawZ,
            remote.Scene.Points,
            remote.Spawn.PlayerRawX,
            remote.Spawn.PlayerRawY,
            remote.Spawn.PlayerRawZ,
            ProposedRawX,
            ProposedRawY,
            ProposedT96RawZ,
            ProposedRawX,
            ProposedRawY,
            ProposedT97RawZ,
            ProposedT97RawZ - ProposedT96RawZ,
            horizontalDistance,
            threeDimensionalDistance,
            padMargin,
            NativeNearTriggerRaw,
            NativeNearTriggerRaw / 16.0,
            horizontalDistance - NativeNearTriggerRaw / 16.0,
            changedOffsets,
            LogicalXyzPatchBytes: 24,
            ChangedDataByteCount: changedOffsets.Length,
            XyzPatchManifestHex: Convert.ToHexString(xyzManifest),
            XyzPatchManifestSha256: Hash(xyzManifest),
            ProposedObjectTableSha256,
            ProposedOutputDataSha256,
            T96InsideAuthoredPad: t96Inside,
            T97InsideAuthoredPad: t97Inside,
            SpawnOutsideNativeNearTrigger: horizontalDistance > NativeNearTriggerRaw / 16.0,
            VisualFootprintRuntimeVerified: false,
            ActivationRuntimeVerified: false);
        if (!placement.T96InsideAuthoredPad || !placement.T97InsideAuthoredPad ||
            !placement.SpawnOutsideNativeNearTrigger ||
            Math.Abs(placement.NearestPadEdgeMarginWorld - 17.88854381999832) > 0.000000001 ||
            placement.ChangedDataByteCount != 14)
        {
            throw new InvalidDataException("The destination-safe pad placement geometry changed.");
        }

        string[] runtimeOnlyGateDescriptions =
        [
            "Cold-enter ID65 through the test-only hidden warp and prove the moved pair does not trigger at spawn.",
            "Prove T96 is visible, fully supported by the authored pad, reachable, and activates from the intended side.",
            "Prove T97 selector 8 plays the intended Return Home sound from the preserved ID65 sound component.",
            "Activate Return Home in ID65 and prove the transition lands safely in Gnasty's World level 60.",
            "Choose pause-menu Exit Level in ID65 and prove it lands safely in Gnasty's World level 60.",
            "Re-enter ID65 with the test warp and prove spawn, T96/T97 placement, camera, collision, and controls remain stable.",
            "Prove death/respawn and reset remain on the remote authored pad before and after one exit/re-entry cycle.",
            "Prove retail Town Square T96/T97 and all four Gnasty's World portals 62/63/64/61 are byte/runtime unchanged."
        ];
        string manifest = BuildTransactionManifest(
            visible,
            helper,
            actor,
            native,
            pauseExit,
            destinations,
            link,
            placement);
        string transactionManifestHash = Hash(Encoding.UTF8.GetBytes(manifest));
        RequireHash(transactionManifestHash, ExpectedTransactionManifestSha256, "Return Home transaction manifest");
        UnusedLevel65ReturnHomeTransaction transaction = new(
            "id65:t96-t97:return-home-pad-relocation-and-lab-link-v1",
            Id65RowCount: 2,
            Id65TrueIndexes: [96, 97],
            LogicalWadPatchBytes: 24,
            ExecutablePatchBytes: 0,
            RetailWadPatchBytes: 0,
            ChangedDataBytes: changedOffsets.Length,
            RequiredSidecars: ["unusedlevel65blank-behavior-links.json"],
            Preconditions:
            [
                "Exact locked display-name BIN and exact remote-blank in-memory plan",
                "Atomic T96/T97 XYZ update with every other row, property, fixup, actor, particle, sound, and executable byte preserved",
                "Lab-key control-role-proof sidecar containing the exact [97,96] linked-move group"
            ],
            RuntimeOnlyGates: runtimeOnlyGateDescriptions,
            DeterministicManifestSha256: transactionManifestHash,
            StaticDependencyClosureComplete: true,
            DestinationSafe: pauseExit.ComputedDestinationLevelId == 60 && destinations.GuardPatchBytesInProposal == 0,
            RetailLevelsPreserved: true,
            WriterAuthorized: false,
            RuntimeCandidateAuthorized: false,
            PromotionAuthorized: false);

        UnusedLevel65ReturnHomeRuntimeGate[] runtimeGates = runtimeOnlyGateDescriptions
            .Select((requirement, index) => new UnusedLevel65ReturnHomeRuntimeGate(
                index + 1,
                $"return-home-runtime-{index + 1}",
                requirement,
                Passed: false))
            .ToArray();

        return new UnusedLevel65ReturnHomeOwnershipContract(
            ProfileId,
            LockedBaseImageSha256,
            remote.ProfileId,
            remote.OutputDataSha256,
            ExecutableSha256,
            OverlaySha256,
            MemoryWadRegion(
                "remote scene pointer fixups",
                SceneWadOffset + SceneFixupCountOffset,
                fixups,
                includeHex: false),
            visible,
            helper,
            actor,
            native,
            pauseExit,
            bootstrap,
            destinations,
            common,
            link,
            placement,
            transaction,
            runtimeGates,
            LockedImagePreserved: true,
            RemotePlanInMemoryOnly: true,
            StaticReadbackOnly: true,
            WritesBin: false,
            WritesCue: false,
            WritesMetadata: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            ReleaseAuthorized: false);
    }

    private static UnusedLevel65ReturnHomeRowClosure BuildRowClosure(
        int trueIndex,
        string role,
        byte[] townRow,
        byte[] remoteRow,
        byte[] proposedRow,
        UnusedLevel65ReturnHomePropertiesClosure properties,
        DiscLayout layout,
        ushort expectedActorClass)
    {
        ushort actorClass = ReadUInt16(remoteRow, 0x36);
        if (actorClass != expectedActorClass)
            throw new InvalidDataException($"T{trueIndex} actor/class changed.");
        bool outsideXyzPreserved = remoteRow.AsSpan(0, 0x0C).SequenceEqual(proposedRow.AsSpan(0, 0x0C)) &&
            remoteRow.AsSpan(0x18).SequenceEqual(proposedRow.AsSpan(0x18));
        if (!outsideXyzPreserved)
            throw new InvalidDataException($"T{trueIndex} proposal changed bytes outside XYZ.");
        return new(
            trueIndex,
            role,
            actorClass,
            WadRegion(
                $"Town Square T{trueIndex}",
                TownSquareObjectTableWadOffset + trueIndex * ObjectRecordByteLength,
                townRow,
                layout,
                runtimeAddress: null,
                hasPhysicalImage: true),
            MemoryWadRegion(
                $"remote ID65 T{trueIndex} before",
                Id65ObjectTableWadOffset + trueIndex * ObjectRecordByteLength,
                remoteRow),
            MemoryWadRegion(
                $"proposed ID65 T{trueIndex} after",
                Id65ObjectTableWadOffset + trueIndex * ObjectRecordByteLength,
                proposedRow),
            ReadInt32(remoteRow, 0x0C),
            ReadInt32(remoteRow, 0x10),
            ReadInt32(remoteRow, 0x14),
            ReadInt32(proposedRow, 0x0C),
            ReadInt32(proposedRow, 0x10),
            ReadInt32(proposedRow, 0x14),
            properties,
            TownSquareAndRemoteArePhysicalStorageAliases: false,
            RowBytesOutsideXyzPreserved: outsideXyzPreserved);
    }

    private static byte[] SliceRemoteComponent(
        UnusedLevel65RemoteBlankStaticPlan remote,
        UnusedLevel65RemoteBlankComponentProof component)
    {
        int offset = checked((int)(
            remote.ModelWadOffset + component.OutputRelativeOffset - remote.DataWadOffset));
        return remote.OutputData.AsSpan(offset, component.ByteLength).ToArray();
    }

    private static void PatchXyz(byte[] data, int trueIndex, int x, int y, int z)
    {
        int offset = ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength + 0x0C;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 4, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 8, 4), z);
    }

    private static void RequireOnlyXyzChanged(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The proposal changed row-80 length.");
        HashSet<int> allow = [];
        foreach (int trueIndex in new[] { T96, T97 })
        {
            int start = ObjectTableDataRelativeOffset + trueIndex * ObjectRecordByteLength + 0x0C;
            for (int index = 0; index < 12; index++)
                allow.Add(start + index);
        }
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index] && !allow.Contains(index))
                throw new InvalidDataException($"The proposal changed byte 0x{index:X} outside T96/T97 XYZ.");
        }
    }

    private static int[] DifferentByteIndexes(byte[] before, byte[] after) =>
        Enumerable.Range(0, before.Length)
            .Where(index => before[index] != after[index])
            .ToArray();

    private static string BuildTransactionManifest(
        UnusedLevel65ReturnHomeRowClosure visible,
        UnusedLevel65ReturnHomeRowClosure helper,
        UnusedLevel65ReturnHomeActorClosure actor,
        UnusedLevel65ReturnHomeNativeClosure native,
        UnusedLevel65ReturnHomePauseExitClosure pause,
        UnusedLevel65ReturnHomeDestinationClosure destinations,
        UnusedLevel65ReturnHomeLinkClosure link,
        UnusedLevel65ReturnHomePlacementClosure placement)
    {
        string[] values =
        [
            ProfileId,
            LockedBaseImageSha256,
            RemoteBlankOutputDataSha256,
            visible.RemoteBeforeRow.Sha256,
            visible.ProposedAfterRow.Sha256,
            visible.Properties.Bytes.Sha256,
            helper.RemoteBeforeRow.Sha256,
            helper.ProposedAfterRow.Sha256,
            helper.Properties.Bytes.Sha256,
            actor.ActorPackage.Sha256,
            native.Overlay.Sha256,
            native.T96Handler.Sha256,
            native.T97Handler.Sha256,
            native.RemoteSoundComponent.Sha256,
            pause.ExitSetupRoutine.Sha256,
            pause.ComputedDestinationLevelId.ToString(),
            destinations.PortalGuardPath.Sha256,
            destinations.GuardPatchBytesInProposal.ToString(),
            destinations.GnastyWorldPortalTable.Sha256,
            string.Join(',', destinations.GnastyWorldDestinationLevelIds),
            link.CanonicalLabGroupSha256,
            placement.XyzPatchManifestSha256,
            placement.ProposedObjectTableSha256,
            placement.ProposedDataSha256,
            placement.LogicalXyzPatchBytes.ToString(),
            placement.ChangedDataByteCount.ToString(),
            "exe=0",
            "retail=0",
            "runtime=pending"
        ];
        return string.Join('|', values);
    }

    private static bool HasLinkGroup(
        string path,
        string key,
        IReadOnlyList<int> trueIndexes,
        IReadOnlyList<string> memberIds)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("linkGroups").EnumerateArray().Any(group =>
            group.GetProperty("key").GetString() == key &&
            group.GetProperty("linkedMove").GetBoolean() &&
            group.GetProperty("trueIndexes").EnumerateArray().Select(value => value.GetInt32())
                .SequenceEqual(trueIndexes) &&
            group.GetProperty("memberIds").EnumerateArray().Select(value => value.GetString() ?? "")
                .SequenceEqual(memberIds));
    }

    private static int ReadPortalTableUsedLength(
        FileStream image,
        DiscLayout layout,
        long tableWadOffset,
        int expectedCount)
    {
        int count = ReadWadInt32(image, layout, tableWadOffset);
        if (count != expectedCount)
            throw new InvalidDataException($"Portal table count {count} does not match {expectedCount}.");
        long cursor = tableWadOffset + 4;
        for (int index = 0; index < count; index++)
        {
            int pointCount = ReadWadInt32(image, layout, cursor + 4);
            if (pointCount is < 1 or > 64)
                throw new InvalidDataException($"Portal {index} point count {pointCount} is invalid.");
            long skybox = cursor + 0x2C + (pointCount - 1L) * 12 + 20;
            int skyboxLength = ReadWadInt32(image, layout, skybox);
            if (skyboxLength < 4 || (skyboxLength & 3) != 0)
                throw new InvalidDataException($"Portal {index} skybox length is invalid.");
            cursor = checked(skybox + skyboxLength);
        }
        return checked((int)(cursor - tableWadOffset));
    }

    private static int ReadWadInt32(FileStream image, DiscLayout layout, long wadOffset) =>
        BinaryPrimitives.ReadInt32LittleEndian(
            DiscImage.ReadFileBytes(image, layout, WadLba, wadOffset, 4));

    private static UnusedLevel65ReturnHomeCallEdge[] FindDirectJalCallers(
        byte[] executable,
        uint target)
    {
        List<UnusedLevel65ReturnHomeCallEdge> callers = [];
        for (int offset = 0x800; offset + 4 <= executable.Length; offset += 4)
        {
            uint instruction = ReadUInt32(executable, offset);
            if (instruction >> 26 != 3)
                continue;
            uint caller = ExecutableRuntimeBias + (uint)offset;
            uint resolved = ResolveJal(caller, instruction);
            if (resolved == target)
            {
                callers.Add(new(
                    "SCUS_942.28",
                    offset,
                    caller,
                    target,
                    Convert.ToHexString(executable.AsSpan(offset, 4)),
                    DirectJal: true));
            }
        }
        return callers.ToArray();
    }

    private static UnusedLevel65ReturnHomeCallEdge RequireCall(
        byte[] container,
        string containerName,
        long logicalBase,
        uint runtimeBase,
        uint callerRuntimeAddress,
        uint expectedTarget,
        string expectedHex)
    {
        int offset = checked((int)(callerRuntimeAddress - runtimeBase));
        byte[] instructionBytes = container.AsSpan(offset, 4).ToArray();
        RequireHex(instructionBytes, expectedHex, $"call at 0x{callerRuntimeAddress:X8}");
        uint instruction = ReadUInt32(instructionBytes, 0);
        if (instruction >> 26 != 3 || ResolveJal(callerRuntimeAddress, instruction) != expectedTarget)
            throw new InvalidDataException($"Call at 0x{callerRuntimeAddress:X8} changed target.");
        return new(
            containerName,
            logicalBase + offset,
            callerRuntimeAddress,
            expectedTarget,
            expectedHex,
            DirectJal: true);
    }

    private static uint ResolveJal(uint caller, uint instruction) =>
        ((caller + 4) & 0xF0000000) | ((instruction & 0x03FFFFFF) << 2);

    private static UnusedLevel65ReturnHomeByteRegion ExeRegion(
        byte[] executable,
        DiscLayout layout,
        int fileOffset,
        int byteLength,
        string name,
        string expectedSha256,
        string? expectedHex = null)
    {
        byte[] bytes = executable.AsSpan(fileOffset, byteLength).ToArray();
        RequireHash(Hash(bytes), expectedSha256, name);
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        return new(
            name,
            "SCUS_942.28",
            fileOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, fileOffset),
            ExecutableRuntimeBias + (uint)fileOffset,
            byteLength,
            byteLength <= 64 ? Convert.ToHexString(bytes) : "",
            Hash(bytes));
    }

    private static UnusedLevel65ReturnHomeByteRegion OverlayRegion(
        byte[] overlay,
        DiscLayout layout,
        uint runtimeAddress,
        int byteLength,
        string name,
        string expectedSha256,
        string? expectedHex = null)
    {
        int relativeOffset = checked((int)(runtimeAddress - OverlayRuntimeAddress));
        byte[] bytes = overlay.AsSpan(relativeOffset, byteLength).ToArray();
        RequireHash(Hash(bytes), expectedSha256, name);
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        return WadRegion(
            name,
            OverlayWadOffset + relativeOffset,
            bytes,
            layout,
            runtimeAddress,
            hasPhysicalImage: true,
            includeHex: byteLength <= 64);
    }

    private static UnusedLevel65ReturnHomeByteRegion WadRegion(
        string name,
        long wadOffset,
        byte[] bytes,
        DiscLayout layout,
        uint? runtimeAddress,
        bool hasPhysicalImage,
        bool includeHex = true) =>
        new(
            name,
            "WAD.WAD",
            wadOffset,
            hasPhysicalImage
                ? DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset)
                : -1,
            runtimeAddress,
            bytes.Length,
            includeHex ? Convert.ToHexString(bytes) : "",
            Hash(bytes));

    private static UnusedLevel65ReturnHomeByteRegion MemoryWadRegion(
        string name,
        long wadOffset,
        byte[] bytes,
        bool includeHex = true) =>
        new(
            name,
            "in-memory remote ID65 row 80",
            wadOffset,
            -1,
            null,
            bytes.Length,
            includeHex ? Convert.ToHexString(bytes) : "",
            Hash(bytes));

    private static byte[] SliceRow(byte[] table, int trueIndex) =>
        table.AsSpan(trueIndex * ObjectRecordByteLength, ObjectRecordByteLength).ToArray();

    private static int HomeworldFor(int levelId) => (levelId / 10) * 10;

    private static bool PointInTriangle(
        double x,
        double y,
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> points)
    {
        if (points.Count != 3)
            return false;
        double d1 = Sign(x, y, points[0], points[1]);
        double d2 = Sign(x, y, points[1], points[2]);
        double d3 = Sign(x, y, points[2], points[0]);
        bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNegative && hasPositive);
    }

    private static double Sign(
        double x,
        double y,
        UnusedLevel65RemoteBlankPoint first,
        UnusedLevel65RemoteBlankPoint second) =>
        (x - second.X) * (first.Y - second.Y) -
        (first.X - second.X) * (y - second.Y);

    private static double DistanceToTriangleEdges(
        double x,
        double y,
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> points)
    {
        if (points.Count != 3)
            return 0;
        return Enumerable.Range(0, 3)
            .Select(index => DistanceToLineSegment(
                x,
                y,
                points[index].X,
                points[index].Y,
                points[(index + 1) % 3].X,
                points[(index + 1) % 3].Y))
            .Min();
    }

    private static double DistanceToLineSegment(
        double x,
        double y,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double denominator = dx * dx + dy * dy;
        double t = denominator == 0
            ? 0
            : Math.Clamp(((x - x1) * dx + (y - y1) * dy) / denominator, 0, 1);
        return Math.Sqrt(
            Math.Pow(x - (x1 + t * dx), 2) +
            Math.Pow(y - (y1 + t * dy), 2));
    }

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
        {
            throw new InvalidDataException(
                $"{name} resolved to LBA {record.Lba}/0x{record.Size:X}, expected LBA {expectedLba}/0x{expectedSize:X}.");
        }
        return record;
    }

    private static int ReadInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void RequireHex(ReadOnlySpan<byte> bytes, string expectedHex, string label)
    {
        string actual = Convert.ToHexString(bytes);
        if (!string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} changed: expected {expectedHex}, got {actual}.");
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, got {actual}.");
    }
}
