using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65SaveDeathRespawnRegion(
    string Name,
    string Container,
    long LogicalOffset,
    long ImageOffset,
    uint? RuntimeAddress,
    int ByteLength,
    string Hex,
    string Sha256);

internal sealed record UnusedLevel65SaveDeathRespawnCallEdge(
    string Name,
    string Container,
    long LogicalOffset,
    long ImageOffset,
    uint CallerRuntimeAddress,
    uint CalleeRuntimeAddress,
    string InstructionHex,
    bool DirectJal);

internal sealed record UnusedLevel65MemoryCardDeserializeDispatchProof(
    int WadEntryIndex,
    UnusedLevel65SaveDeathRespawnRegion DirectoryRow,
    UnusedLevel65SaveDeathRespawnRegion Overlay,
    UnusedLevel65SaveDeathRespawnRegion Header,
    uint HeaderRuntimeBase,
    UnusedLevel65SaveDeathRespawnRegion StatePointerTable,
    uint StatePointerTableRuntimeAddress,
    IReadOnlyList<uint> StateTargets,
    uint MainMenuStateRuntimeAddress,
    int StateCount,
    UnusedLevel65SaveDeathRespawnRegion IndirectDispatcher,
    int SelectedLoadState,
    uint SelectedLoadHandlerRuntimeAddress,
    UnusedLevel65SaveDeathRespawnRegion SelectedLoadHandler,
    uint SelectedSlotIndexRuntimeAddress,
    UnusedLevel65SaveDeathRespawnRegion SelectedSlotCallWindow,
    UnusedLevel65SaveDeathRespawnCallEdge DeserializeCall,
    int ResidentDeserializeDirectCallerCount,
    int OverlayDeserializeDirectCallerCount,
    int ExactRemoteRawJalBytePatternCount,
    bool DispatcherBoundsStateBelow16,
    bool DispatcherUsesHeaderPointerTable,
    bool DispatcherJumpsThroughSelectedPointer,
    bool SelectedSlotBufferPointerLoadedBeforeCall,
    bool IndirectDeserializeClosureResolved);

internal sealed record UnusedLevel65SaveFieldClosure(
    string Name,
    int SaveOffset,
    int ByteLength,
    int RowCount,
    int? Index35SaveOffset,
    uint? Index35RuntimeAddress,
    bool Index35StoragePresent,
    string Policy);

internal sealed record UnusedLevel65SaveSchemaClosureProof(
    string ProgressionProfileId,
    int SaveStructByteLength,
    int ChecksummedByteLength,
    int ChecksumFieldOffset,
    UnusedLevel65SaveDeathRespawnRegion DeserializeRoutine,
    UnusedLevel65SaveDeathRespawnRegion SerializeRoutine,
    UnusedLevel65SaveDeathRespawnRegion DeserializeLivesClampWindow,
    UnusedLevel65SaveDeathRespawnRegion SerializeLivesWindow,
    UnusedLevel65SaveDeathRespawnRegion NewGameLivesInitializeWindow,
    int LivesSaveOffset,
    uint LivesRuntimeAddress,
    int MinimumLivesAfterDeserialize,
    int NewGameInitialLives,
    IReadOnlyList<UnusedLevel65SaveFieldClosure> Fields,
    int RetirementBitsPerLevel,
    bool LevelId65EncodesInCurrentLevelByte,
    bool AllIndex35NonEggRowsRoundTrip,
    bool EggIndex35RowPresent,
    bool OpaqueSecondPerLevelBytePreservedWithoutSemanticClaim,
    bool DeserializeRestoresAtLeastFourLives,
    bool SerializePersistsLivesByte,
    bool NewGamePathInitializesFourLives,
    bool MemoryCardDeserializeCallgraphClosed,
    bool PhysicalMemoryCardRuntimeAccepted);

internal sealed record UnusedLevel65PassiveMobySaveProfileProof(
    string MobyProfileId,
    string MobyContractSha256,
    string FoundationImageSha256,
    string DonorId,
    string DonorLevel,
    int DonorTrueIndex,
    string DonorIdentity,
    ushort ActorId,
    int ExistingDestinationObjectCount,
    int WitnessDestinationTrueIndex,
    int WitnessDestinationObjectCount,
    int CompleteAppendRowCapacity,
    int HighestCompleteAppendTrueIndex,
    int RetirementBitmapBits,
    string DonorDispatchTraceSha256,
    string TargetDispatchTraceSha256,
    bool DonorIsDragon,
    bool DonorIsPortal,
    bool DonorIsThief,
    bool DonorIsCollectible,
    bool DonorIsTotalsLinked,
    bool DonorHasRewardDrop,
    bool DonorHasLegacySpecialData,
    bool ActorPackageIsUntextured,
    bool ClassControllerRequired,
    bool SoundRequired,
    bool ParticleRequired,
    bool DynamicSpawnRequired,
    bool DynamicNativeLinkRequired,
    bool WitnessFitsRetirementBitmap,
    bool WholePinnedAppendRowAllocatorFitsRetirementBitmap,
    bool NoEggPassiveSaveProfileStaticallyClosed,
    bool SupportsOnlyPinnedPassiveWitness,
    bool SupportsEggs,
    bool SupportsCollectiblesOrRewards,
    bool SupportsAllGameMobys,
    bool PassiveMobyRuntimeAccepted);

internal sealed record UnusedLevel65DeathRespawnStaticProof(
    UnusedLevel65SaveDeathRespawnRegion DeathLifeRoutine,
    IReadOnlyList<UnusedLevel65SaveDeathRespawnCallEdge> DeathDirectCallers,
    UnusedLevel65SaveDeathRespawnRegion CommonDeathPlaneWindow,
    uint PlayerZRuntimeAddress,
    int DeathPlaneZThreshold,
    UnusedLevel65SaveDeathRespawnRegion MainStateDispatch,
    UnusedLevel65SaveDeathRespawnCallEdge StateFourAndFiveHandlerCall,
    UnusedLevel65SaveDeathRespawnRegion StateFourAndFiveHandlerPrefix,
    int StateFourRespawnDelayTicks,
    UnusedLevel65SaveDeathRespawnCallEdge StateFourResetCall,
    UnusedLevel65SaveDeathRespawnCallEdge StateFourReloadCall,
    UnusedLevel65SaveDeathRespawnRegion RespawnResetRoutine,
    IReadOnlyList<UnusedLevel65SaveDeathRespawnCallEdge> RespawnResetDirectCallers,
    UnusedLevel65SaveDeathRespawnRegion SameLevelReloadRoutine,
    UnusedLevel65SaveDeathRespawnCallEdge CommonLevelLoaderCall,
    uint ContinuousLevelIndexRuntimeAddress,
    int PositiveLivesDeathState,
    int ZeroLivesDeathState,
    bool PositiveLivesAreDecremented,
    bool ZeroLivesSelectsGameOver,
    bool DeathClearsStateTimers,
    bool DeathWritesPerLevelProgress,
    bool RespawnResetWritesPerLevelProgress,
    bool StateFourReloadUsesContinuousLevelIndex,
    bool StaticDeathRespawnCallgraphClosed,
    bool RemoteSpawnLandingRuntimeAccepted,
    bool RemoteDeathRespawnRuntimeAccepted);

internal sealed record UnusedLevel65RemoteBlankRuntimeFailureObservation(
    string EvidenceArtifactRelativePath,
    string EvidenceArtifactSha256,
    string Date,
    string ImageSha256,
    string BootProfile,
    string VisibleResult,
    string TerminalResult,
    int ApproximateSecondsToGameOver,
    bool CardsDisabled,
    bool CheatsDisabled,
    bool SaveStateDisabled,
    bool SpyroLanded,
    bool SpawnRuntimePassed,
    bool DeathRespawnRuntimePassed,
    bool InitialLivesRuntimeValueCaptured,
    bool DeathInvocationCountCaptured,
    bool RuntimeRootCauseDiscriminated,
    bool ObservationHasMachineReadableEvidenceArtifact,
    bool FoundationControlUsedSameSelectorProcedure,
    int FoundationControlLivesDisplayed,
    bool FoundationControlPlayerStayedAlive,
    bool RepeatedFailedRespawnsCouldExhaustLives,
    string StaticExplanationBoundary);

internal sealed record UnusedLevel65SaveDeathRespawnOwnershipClosureContract(
    string ProfileId,
    string LockedDisplayNameImageSha256,
    string RemoteBlankImageSha256,
    string FoundationImageSha256,
    string ExecutableSha256,
    string RemoteRow80DataSha256,
    string RemoteObjectTableSha256,
    bool RemoteRow80MatchesFrozenConstruction,
    bool RemoteExecutableMatchesLockedDisplayNameExecutable,
    UnusedLevel65MemoryCardDeserializeDispatchProof MemoryCardDispatch,
    UnusedLevel65SaveSchemaClosureProof SaveSchema,
    UnusedLevel65PassiveMobySaveProfileProof PassiveMobyProfile,
    UnusedLevel65DeathRespawnStaticProof DeathRespawn,
    UnusedLevel65RemoteBlankRuntimeFailureObservation RuntimeFailure,
    IReadOnlyList<string> RemainingRuntimeBlockers,
    bool StaticReadbackOnly,
    bool WritesBin,
    bool WritesCue,
    bool WriterAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool ReleasePublicationAuthorized,
    bool RuntimePromotionAuthorized);

/// <summary>
/// Read-only closure of the ID65 memory-card deserialize dispatch, the exact
/// non-egg index-35 save rows, the first pinned passive-Moby witness, and the
/// common death/respawn call graph. It deliberately records the failed remote
/// spawn/death run and cannot write or promote any disc artifact.
/// </summary>
internal static class UnusedLevel65SaveDeathRespawnOwnershipClosure
{
    public const string ProfileId =
        "unused-level-65-save-death-respawn-no-egg-passive-static-closure-v1";
    public const string LockedDisplayNameImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string RemoteBlankImageSha256 =
        "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e";
    public const string FoundationImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string RemoteRow80DataSha256 =
        "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061";
    public const string RemoteObjectTableSha256 =
        "f89347baf4f14739030499b21210b8ea46b44f9c4df02a1b58d39fde6ca1e64a";
    public const string RuntimeFailureEvidenceSha256 =
        "08de9812ccecf5512e7d1453ad009a6303cd90da9e0ab82e9e132d9a15fcf9c3";

    private const string RuntimeFailureEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-remote-blank-isolation-no-landing-game-over-2026-08-10.json";

    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const uint ExecutableRuntimeBias = 0x8000F800;
    private const long Row80WadOffset = 0x6936800;
    private const int Row80ByteLength = 0x2E2000;
    private const int ObjectTableDataOffset = 0x1D0170;
    private const int ObjectRecordLength = 0x58;
    private const int ObjectRecordCount = 107;

    private const int MemoryCardOverlayEntry = 2;
    private const long MemoryCardOverlayWadOffset = 0x5B800;
    private const int MemoryCardOverlayByteLength = 0x3800;
    private const int MemoryCardHeaderByteLength = 0x9C;
    private const uint MemoryCardHeaderRuntimeBase = 0x8007AA38;
    private const int StatePointerTableRelativeOffset = 0x1C;
    private const uint StatePointerTableRuntimeAddress = 0x8007AA54;
    private const int StatePointerCount = 16;
    private const int DispatcherRelativeOffset = 0x680;
    private const int DispatcherByteLength = 0x30;
    private const uint OverlayCodeRuntimeBase = 0x8007B0E8;
    private const int SelectedLoadState = 14;
    private const uint SelectedLoadHandlerRuntimeAddress = 0x8007CC48;
    private const int SelectedLoadHandlerRelativeOffset = 0x1BFC;
    private const int SelectedLoadHandlerByteLength = 0x704;
    private const int SelectedSlotCallWindowRelativeOffset = 0x1FC4;
    private const int SelectedSlotCallWindowByteLength = 0x40;
    private const int DeserializeCallRelativeOffset = 0x1FFC;
    private const uint DeserializeRuntimeAddress = 0x80059594;
    private const uint MainMenuStateRuntimeAddress = 0x80078D88;
    private const uint SelectedSlotIndexRuntimeAddress = 0x80078D8C;

    public static UnusedLevel65SaveDeathRespawnOwnershipClosureContract Inspect(
        string workspaceRoot,
        string lockedDisplayNameImagePath,
        string remoteBlankImagePath,
        string foundationImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedDisplayNameImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteBlankImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(foundationImagePath);

        string root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string lockedPath = RequireFile(lockedDisplayNameImagePath, "locked ID65 display-name BIN");
        string remotePath = RequireFile(remoteBlankImagePath, "remote-blank ID65 BIN");
        string foundationPath = RequireFile(foundationImagePath, "ID65 full-authoring foundation BIN");

        UnusedLevel65ProgressionOwnershipContract progression =
            UnusedLevel65ProgressionOwnershipInspector.Inspect(root, lockedPath);
        ValidateProgressionSubstrate(progression);

        UnusedLevel65MobyDependencyBundleContract moby =
            UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundationPath);
        ValidatePassiveMobySubstrate(moby);

        FilePatternScan remoteScan = ScanFileAndCountPattern(
            remotePath,
            Convert.FromHexString("6565010C"));
        RequireHash(remoteScan.Sha256, RemoteBlankImageSha256, "remote-blank ID65 BIN");
        if (remoteScan.PatternCount != 1)
            throw new InvalidDataException("The exact remote BIN does not contain one deserialize JAL byte pattern.");

        DiscLayout lockedLayout = DiscImage.DetectLayout(lockedPath);
        RequireMode2(lockedLayout, "locked display-name BIN");
        byte[] executable;
        byte[] memoryCardOverlay;
        byte[] directoryRow;
        using (FileStream lockedImage = File.OpenRead(lockedPath))
        {
            _ = RequireRootFile(lockedImage, lockedLayout, "WAD.WAD", WadLba, WadByteLength);
            DiscFileRecord exe = RequireRootFile(
                lockedImage, lockedLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
            executable = DiscImage.ReadFileBytes(lockedImage, lockedLayout, exe.Lba, 0, exe.Size);
            RequireHash(Hash(executable), ExecutableSha256, "locked executable");
            directoryRow = DiscImage.ReadFileBytes(
                lockedImage, lockedLayout, WadLba, MemoryCardOverlayEntry * 8L, 8);
            RequireHex(directoryRow, "00B8050000380000", "memory-card overlay directory row");
            memoryCardOverlay = DiscImage.ReadFileBytes(
                lockedImage,
                lockedLayout,
                WadLba,
                MemoryCardOverlayWadOffset,
                MemoryCardOverlayByteLength);
        }

        UnusedLevel65RemoteBlankStaticPlan remotePlan =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(lockedPath);
        byte[] remoteRow80;
        byte[] remoteExecutable;
        DiscLayout remoteLayout = DiscImage.DetectLayout(remotePath);
        RequireMode2(remoteLayout, "remote-blank BIN");
        using (FileStream remoteImage = File.OpenRead(remotePath))
        {
            _ = RequireRootFile(remoteImage, remoteLayout, "WAD.WAD", WadLba, WadByteLength);
            DiscFileRecord exe = RequireRootFile(
                remoteImage, remoteLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
            remoteExecutable = DiscImage.ReadFileBytes(remoteImage, remoteLayout, exe.Lba, 0, exe.Size);
            remoteRow80 = DiscImage.ReadFileBytes(
                remoteImage, remoteLayout, WadLba, Row80WadOffset, Row80ByteLength);
        }
        RequireHash(Hash(remoteExecutable), ExecutableSha256, "remote executable");
        RequireHash(Hash(remoteRow80), RemoteRow80DataSha256, "remote full row-80 data");
        if (!remoteExecutable.SequenceEqual(executable))
            throw new InvalidDataException("The remote BIN executable differs from the locked display-name executable.");
        if (!remoteRow80.SequenceEqual(remotePlan.OutputData))
            throw new InvalidDataException("The physical remote row 80 differs from the frozen construction plan.");
        byte[] remoteObjectTable = remoteRow80.AsSpan(
            ObjectTableDataOffset,
            ObjectRecordCount * ObjectRecordLength).ToArray();
        RequireHash(Hash(remoteObjectTable), RemoteObjectTableSha256, "remote object table");

        UnusedLevel65MemoryCardDeserializeDispatchProof dispatch = BuildMemoryCardDispatch(
            memoryCardOverlay,
            directoryRow,
            executable,
            lockedLayout,
            remoteScan.PatternCount);
        UnusedLevel65SaveSchemaClosureProof save = BuildSaveSchema(progression, executable, lockedLayout, dispatch);
        UnusedLevel65PassiveMobySaveProfileProof passive = BuildPassiveProfile(moby, save);
        UnusedLevel65DeathRespawnStaticProof death = BuildDeathRespawn(executable, lockedLayout);

        UnusedLevel65RemoteBlankRuntimeFailureObservation runtimeFailure =
            LoadRuntimeFailureEvidence(root);

        string[] blockers =
        [
            "The exact remote-blank runtime failed to land and reached GAME OVER; sector-216 spawn/collision is not accepted.",
            "No captured RAM value proves the initial lives count, and no death counter distinguishes first-death state 5 from repeated state-4 exhaustion.",
            "Fresh, existing, full, corrupt, and removed memory-card save/load behavior remains physically untested for ID65.",
            "Cold reset and death/respawn persistence remain physically unaccepted on the remote construction.",
            "Only the pinned passive Artisans Grass witness is statically closed; eggs, rewards, collectibles, and arbitrary game Mobys are excluded."
        ];

        return new(
            ProfileId,
            LockedDisplayNameImageSha256,
            RemoteBlankImageSha256,
            FoundationImageSha256,
            ExecutableSha256,
            RemoteRow80DataSha256,
            RemoteObjectTableSha256,
            RemoteRow80MatchesFrozenConstruction: true,
            RemoteExecutableMatchesLockedDisplayNameExecutable: true,
            dispatch,
            save,
            passive,
            death,
            runtimeFailure,
            blockers,
            StaticReadbackOnly: true,
            WritesBin: false,
            WritesCue: false,
            WriterAuthorized: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            ReleasePublicationAuthorized: false,
            RuntimePromotionAuthorized: false);
    }

    private static UnusedLevel65MemoryCardDeserializeDispatchProof BuildMemoryCardDispatch(
        byte[] overlay,
        byte[] directoryRow,
        byte[] executable,
        DiscLayout layout,
        int rawPatternCount)
    {
        RequireHash(Hash(overlay), "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c",
            "memory-card overlay");
        byte[] header = overlay.AsSpan(0, MemoryCardHeaderByteLength).ToArray();
        RequireHash(Hash(header), "79e96efbe41a96e329c572ebb7fa6c68d72ac6f030ede8783a0a3be952cdc513",
            "memory-card overlay header");
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x30 ||
            Encoding.ASCII.GetString(header, 4, 17) != "BASCUS-94228SPYRO" ||
            header[21] != 0)
        {
            throw new InvalidDataException("The exact memory-card overlay header identity changed.");
        }

        byte[] pointerBytes = overlay.AsSpan(
            StatePointerTableRelativeOffset,
            StatePointerCount * sizeof(uint)).ToArray();
        RequireHash(Hash(pointerBytes), "76076ce45bcfadceb8278173107cccc0e1cc15d89fd7d25ada68f692e5a8c049",
            "memory-card state pointer table");
        uint[] pointers = Enumerable.Range(0, StatePointerCount)
            .Select(index => BinaryPrimitives.ReadUInt32LittleEndian(pointerBytes.AsSpan(index * 4, 4)))
            .ToArray();
        uint[] expectedPointers =
        [
            0x8007B0E8, 0x8007B314, 0x8007B558, 0x8007B64C,
            0x8007B7AC, 0x8007B928, 0x8007B928, 0x8007BA80,
            0x8007BAFC, 0x8007BBD4, 0x8007BCB4, 0x8007BDA0,
            0x8007C0E0, 0x8007C1BC, 0x8007CC48, 0x8007C304
        ];
        if (!pointers.SequenceEqual(expectedPointers))
            throw new InvalidDataException("The exact memory-card state target table changed.");

        UnusedLevel65SaveDeathRespawnRegion dispatcher = OverlayRegion(
            overlay,
            layout,
            DispatcherRelativeOffset,
            DispatcherByteLength,
            "bounded main-menu state dispatcher",
            OverlayRuntimeAddress(DispatcherRelativeOffset),
            expectedHex: "0880033C888D638C000000001000622CDF064010801003000880013C2108220054AA228C000000000800400000000000",
            expectedSha256: "21adc7ed8b750d5e61a24d098b5e0e10ffcae4fbed1e6d0917fc12541ff5c421");
        UnusedLevel65SaveDeathRespawnRegion handler = OverlayRegion(
            overlay,
            layout,
            SelectedLoadHandlerRelativeOffset,
            SelectedLoadHandlerByteLength,
            "state-14 selected-slot load handler",
            SelectedLoadHandlerRuntimeAddress,
            expectedSha256: "c23198765beacadbee0c4d88d2f30aab061aa66c06fa3fdb285c8599a5bdf060");
        UnusedLevel65SaveDeathRespawnRegion callWindow = OverlayRegion(
            overlay,
            layout,
            SelectedSlotCallWindowRelativeOffset,
            SelectedSlotCallWindowByteLength,
            "selected-slot buffer and deserialize call window",
            OverlayRuntimeAddress(SelectedSlotCallWindowRelativeOffset),
            expectedHex: "92004010000000000880023C8C8D428C0880013CBC8B33AC0780013CE85820AC80100200211022024C00448CFFFF02240780013C0C5822AC6565010C00000000",
            expectedSha256: "ac1d42cb6d9de6a930823ba5fdf1017cb81aa769001e04351cb68eaefdc1a07d");

        long[] residentCallers = FindDirectJalOffsets(
            executable, 0x800, ExecutableRuntimeBias, DeserializeRuntimeAddress);
        long[] overlayCallers = FindDirectJalOffsets(
            overlay, MemoryCardHeaderByteLength, OverlayCodeRuntimeBase - MemoryCardHeaderByteLength,
            DeserializeRuntimeAddress);
        if (residentCallers.Length != 0 ||
            !overlayCallers.SequenceEqual(new long[] { DeserializeCallRelativeOffset }))
        {
            throw new InvalidDataException("The exact resident/overlay deserialize direct-call closure changed.");
        }

        UnusedLevel65SaveDeathRespawnCallEdge call = OverlayJalEdge(
            overlay,
            layout,
            DeserializeCallRelativeOffset,
            DeserializeRuntimeAddress,
            "state-14 selected-slot deserialize");

        return new(
            MemoryCardOverlayEntry,
            WadRegion(
                "WAD entry-2 directory row",
                MemoryCardOverlayEntry * 8L,
                directoryRow,
                layout,
                runtimeAddress: null,
                expectedSha256: "40b57312b49d05375186e2579bfa1e0248464826d59b234403be0f0cbdf810a1"),
            WadRegion(
                "memory-card overlay entry 2",
                MemoryCardOverlayWadOffset,
                overlay,
                layout,
                runtimeAddress: null,
                expectedSha256: "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c"),
            WadRegion(
                "memory-card overlay header",
                MemoryCardOverlayWadOffset,
                header,
                layout,
                MemoryCardHeaderRuntimeBase,
                expectedSha256: "79e96efbe41a96e329c572ebb7fa6c68d72ac6f030ede8783a0a3be952cdc513"),
            MemoryCardHeaderRuntimeBase,
            WadRegion(
                "memory-card state pointer table",
                MemoryCardOverlayWadOffset + StatePointerTableRelativeOffset,
                pointerBytes,
                layout,
                StatePointerTableRuntimeAddress,
                expectedSha256: "76076ce45bcfadceb8278173107cccc0e1cc15d89fd7d25ada68f692e5a8c049"),
            StatePointerTableRuntimeAddress,
            pointers,
            MainMenuStateRuntimeAddress,
            StatePointerCount,
            dispatcher,
            SelectedLoadState,
            pointers[SelectedLoadState],
            handler,
            SelectedSlotIndexRuntimeAddress,
            callWindow,
            call,
            residentCallers.Length,
            overlayCallers.Length,
            rawPatternCount,
            DispatcherBoundsStateBelow16: true,
            DispatcherUsesHeaderPointerTable: true,
            DispatcherJumpsThroughSelectedPointer: true,
            SelectedSlotBufferPointerLoadedBeforeCall: true,
            IndirectDeserializeClosureResolved: true);
    }

    private static UnusedLevel65SaveSchemaClosureProof BuildSaveSchema(
        UnusedLevel65ProgressionOwnershipContract progression,
        byte[] executable,
        DiscLayout layout,
        UnusedLevel65MemoryCardDeserializeDispatchProof dispatch)
    {
        UnusedLevel65SaveDeathRespawnRegion deserialize = ExeRegion(
            executable, layout, 0x49D94, 0x2D0, "save deserialize routine",
            expectedSha256: "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6");
        UnusedLevel65SaveDeathRespawnRegion serialize = ExeRegion(
            executable, layout, 0x4A064, 0x1E4, "save serialize routine",
            expectedSha256: "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056");
        UnusedLevel65SaveDeathRespawnRegion deserializeLives = ExeRegion(
            executable, layout, 0x49E90, 0x24, "deserialize lives byte and clamp to four",
            expectedHex: "0B0002920780013C2C5822AC040042280400401021880002040002240780013C2C5822AC",
            expectedSha256: "096384065349a0ba4a9bf7ed181b87a41e86664fbb41606b32fb950bf2e8ea05");
        UnusedLevel65SaveDeathRespawnRegion serializeLives = ExeRegion(
            executable, layout, 0x4A108, 0x14, "serialize runtime lives into save byte 0x0B",
            expectedHex: "0780023C2C58428C14000624565A000C0B0002A2",
            expectedSha256: "4f2da0c0f44e8e0803d5b24f0453b72f586c0a49115d68c4c4996abadb204735");
        UnusedLevel65SaveDeathRespawnRegion newGameLives = ExeRegion(
            executable, layout, 0x2EF0, 0x0C, "new-game initialize four lives",
            expectedHex: "040002240780013C2C5822AC",
            expectedSha256: "00eefa408983e286a787d801292126d65cca60e1abc82a09234bd19e81d4c0e9");

        RequireProgressionField(progression, "current-level-id", 1, 0x000, 0x8007596C, true);
        RequireProgressionField(progression, "visited", 36, 0x063, 0x80078E9B, true);
        RequireProgressionField(
            progression, "unresolved-second-per-level-byte-table", 36, 0x087, 0x8007A6CB, true);
        RequireProgressionField(progression, "dragons", 36, 0x0AB, 0x80077364, true);
        RequireProgressionField(progression, "treasure", 36, 0x0F2, 0x800774AC, true);
        RequireProgressionField(progression, "eggs", 18, null, null, false);
        RequireProgressionField(
            progression, "object-retirement-bitmaps", 36, 0x56C, 0x80077D68, true);

        UnusedLevel65SaveFieldClosure[] fields =
        [
            new("current-level-id", 0x000, 1, 1, 0x000, 0x8007596C, true,
                "The global UInt8 encodes level ID 65; this is not a per-level row."),
            new("lives", 0x00B, 1, 1, 0x00B, 0x8007582C, true,
                "Serialize stores the runtime lives byte; deserialize clamps values below four to four."),
            new("visited", 0x040, 1, 36, 0x063, 0x80078E9B, true,
                "Index 35 is the final independent byte."),
            new("opaque-second-per-level-byte", 0x064, 1, 36, 0x087, 0x8007A6CB, true,
                "Index 35 round-trips exactly; authoring must preserve it and must not assign gameplay semantics."),
            new("dragons", 0x088, 1, 36, 0x0AB, 0x80077364, true,
                "The no-reward profile preserves the independent zero index-35 byte."),
            new("treasure", 0x0AC, 2, 36, 0x0F2, 0x800774AC, true,
                "The no-reward profile preserves the independent zero index-35 UInt16."),
            new("eggs", 0x0F4, 1, 18, null, null, false,
                "There is no index-35 row; eggs and egg-like authored objects are forbidden."),
            new("object-retirement-bitmap", 0x10C, 0x20, 36, 0x56C, 0x80077D68, true,
                "Index 35 owns exactly 256 retirement bits; authored mutable true indices must remain 0..255.")
        ];

        return new(
            progression.ProfileId,
            progression.Save.SaveStructByteLength,
            progression.Save.ChecksummedByteLength,
            progression.Save.ChecksumFieldOffset,
            deserialize,
            serialize,
            deserializeLives,
            serializeLives,
            newGameLives,
            LivesSaveOffset: 0x0B,
            LivesRuntimeAddress: 0x8007582C,
            MinimumLivesAfterDeserialize: 4,
            NewGameInitialLives: 4,
            fields,
            RetirementBitsPerLevel: progression.Save.ObjectRetirementBitsPerLevel,
            LevelId65EncodesInCurrentLevelByte: true,
            AllIndex35NonEggRowsRoundTrip: progression.Save.Index35NonEggRowsStaticallyRoundTrip,
            EggIndex35RowPresent: false,
            OpaqueSecondPerLevelBytePreservedWithoutSemanticClaim: true,
            DeserializeRestoresAtLeastFourLives: true,
            SerializePersistsLivesByte: true,
            NewGamePathInitializesFourLives: true,
            MemoryCardDeserializeCallgraphClosed: dispatch.IndirectDeserializeClosureResolved,
            PhysicalMemoryCardRuntimeAccepted: false);
    }

    private static UnusedLevel65PassiveMobySaveProfileProof BuildPassiveProfile(
        UnusedLevel65MobyDependencyBundleContract moby,
        UnusedLevel65SaveSchemaClosureProof save)
    {
        int witnessCount = moby.Destination.ExistingObjectCount + 1;
        int highestAppendIndex = moby.Destination.FirstAppendTrueIndex +
            moby.Destination.CompleteAppendRowCapacity - 1;
        if (witnessCount != 108 || highestAppendIndex != 222 ||
            highestAppendIndex >= save.RetirementBitsPerLevel)
        {
            throw new InvalidDataException("The passive witness no longer fits the index-35 retirement bitmap.");
        }

        return new(
            moby.ProfileId,
            moby.DeterministicContractSha256,
            moby.FoundationImageSha256,
            moby.DonorId,
            moby.DonorLevelName,
            moby.SourceRecord.TrueIndex,
            moby.SourceRecord.Identity,
            moby.SourceRecord.ActorId,
            moby.Destination.ExistingObjectCount,
            moby.Destination.FirstAppendTrueIndex,
            witnessCount,
            moby.Destination.CompleteAppendRowCapacity,
            highestAppendIndex,
            save.RetirementBitsPerLevel,
            moby.BehaviorClosure.DonorDispatch.ExecutedTraceSha256,
            moby.BehaviorClosure.TargetDispatch.ExecutedTraceSha256,
            moby.SourceRecord.IsDragon,
            moby.SourceRecord.IsPortal,
            moby.SourceRecord.IsThief,
            moby.SourceRecord.IsCollectible,
            moby.SourceRecord.IsTotalsLinked,
            moby.SourceRecord.HasRewardDrop,
            moby.SourceRecord.HasLegacySpecialDataPointer,
            ActorPackageIsUntextured: moby.ActorPackage.TotalTexturedFaceCount == 0 &&
                !moby.ActorPackage.TexturePixelsRequired && !moby.ActorPackage.ClutsRequired,
            ClassControllerRequired: moby.BehaviorClosure.PropertiesControllerSemanticsRequired,
            SoundRequired: moby.BehaviorClosure.SoundDependencyRequired,
            ParticleRequired: moby.BehaviorClosure.ParticleOrEffectDependencyRequired,
            DynamicSpawnRequired: moby.BehaviorClosure.DynamicSpawnDependencyRequired,
            DynamicNativeLinkRequired: moby.BehaviorClosure.DynamicNativeLinkDependencyRequired,
            WitnessFitsRetirementBitmap: true,
            WholePinnedAppendRowAllocatorFitsRetirementBitmap: true,
            NoEggPassiveSaveProfileStaticallyClosed: true,
            SupportsOnlyPinnedPassiveWitness: true,
            SupportsEggs: false,
            SupportsCollectiblesOrRewards: false,
            SupportsAllGameMobys: false,
            PassiveMobyRuntimeAccepted: false);
    }

    private static UnusedLevel65DeathRespawnStaticProof BuildDeathRespawn(
        byte[] executable,
        DiscLayout layout)
    {
        UnusedLevel65SaveDeathRespawnRegion death = ExeRegion(
            executable, layout, 0x1D05C, 0x48, "death/life-loss routine",
            expectedSha256: "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad");
        UnusedLevel65SaveDeathRespawnRegion deathPlane = ExeRegion(
            executable, layout, 0x3ACD8, 0x28, "common Z death-plane branch window",
            expectedHex: "17B2000C000000008E290108000000000880023C608A428C0000000000044228F7FF401406000224",
            expectedSha256: "03faf5a908bea39386de4d8ae5786e2588864c6def27ac903e55dfcc083c9474");
        UnusedLevel65SaveDeathRespawnRegion stateDispatch = ExeRegion(
            executable, layout, 0x24084, 0x7C, "main game-state dispatch",
            expectedSha256: "f3198ff1e90533ccfdfb2bcfc50250dbdf2f07665d2679f51ed933a447918034");
        UnusedLevel65SaveDeathRespawnRegion stateHandler = ExeRegion(
            executable, layout, 0x1F5F0, 0xA0, "shared state-4/state-5 handler prefix",
            expectedSha256: "a2b26dd0e3f148fbbf47af89e391ed3089c162e02998ad12eb1d9d5cc6f963de");
        UnusedLevel65SaveDeathRespawnRegion reset = ExeRegion(
            executable, layout, 0x1D0A4, 0x6C, "respawn checkpoint reset routine",
            expectedSha256: "940ee2a2e463342503bb1990c3cb7159d655939d8c5f7041bcb7f7b6c39b5a92");
        UnusedLevel65SaveDeathRespawnRegion reload = ExeRegion(
            executable, layout, 0x4CC8, 0x9C, "same-level WAD-row reload routine",
            expectedSha256: "fae8117d233b5838e56ab55c326c6d86e6a078a9642ef252bb395e6edaec8990");

        RequireWord(executable, 0x1D060, 0x8C42582C, "death lives load");
        RequireWord(executable, 0x1D068, 0x10400005, "zero-lives branch");
        RequireWord(executable, 0x1D06C, 0x2442FFFF, "positive-lives decrement");
        RequireWord(executable, 0x1D074, 0xAC22582C, "decremented lives store");
        RequireWord(executable, 0x1D080, 0x24020005, "zero-lives state 5");
        RequireWord(executable, 0x1D088, 0xAC2257D8, "death state store");
        RequireWord(executable, 0x1D090, 0xAC205940, "death timer zero 1");
        RequireWord(executable, 0x1D098, 0xAC20593C, "death timer zero 2");
        RequireWord(executable, 0x3ACF4, 0x28420400, "death-plane Z<1024 comparison");
        RequireWord(executable, 0x3ACF8, 0x1440FFF7, "death-plane backward branch");
        RequireWord(executable, 0x240E0, 0x10620003, "state-4 shared-handler branch");
        RequireWord(executable, 0x240E8, 0x14620005, "state-5 shared-handler fallthrough");
        RequireWord(executable, 0x1F65C, 0x28420010, "state-handler 16-tick threshold");
        RequireWord(executable, 0x1F674, 0x14620007, "state-4-only reset/reload branch");
        RequireWord(executable, 0x4CD8, 0x3C028007, "continuous-index base");
        RequireWord(executable, 0x4CDC, 0x8C425964, "continuous-index load");

        UnusedLevel65SaveDeathRespawnCallEdge[] deathCallers = FindExecutableCallEdges(
            executable, layout, 0x8002C85C, "death/life-loss");
        UnusedLevel65SaveDeathRespawnCallEdge[] resetCallers = FindExecutableCallEdges(
            executable, layout, 0x8002C8A4, "respawn checkpoint reset");
        if (!deathCallers.Select(edge => edge.LogicalOffset).SequenceEqual(new long[] { 0x33710, 0x3ACD8 }) ||
            !resetCallers.Select(edge => edge.LogicalOffset).SequenceEqual(new long[] { 0x1F67C, 0x1FB9C }))
        {
            throw new InvalidDataException("The exact death/respawn reset caller set changed.");
        }
        UnusedLevel65SaveDeathRespawnCallEdge stateHandlerCall = ExeJalEdge(
            executable, layout, 0x240F0, 0x8002EDF0, "state 4/5 shared handler");
        UnusedLevel65SaveDeathRespawnCallEdge resetCall = ExeJalEdge(
            executable, layout, 0x1F67C, 0x8002C8A4, "state-4 checkpoint reset");
        UnusedLevel65SaveDeathRespawnCallEdge reloadCall = ExeJalEdge(
            executable, layout, 0x1F684, 0x800144C8, "state-4 same-level reload");
        UnusedLevel65SaveDeathRespawnCallEdge commonLoaderCall = ExeJalEdge(
            executable, layout, 0x4D20, 0x8001364C, "reload common level loader");

        return new(
            death,
            deathCallers,
            deathPlane,
            PlayerZRuntimeAddress: 0x80078A60,
            DeathPlaneZThreshold: 0x400,
            stateDispatch,
            stateHandlerCall,
            stateHandler,
            StateFourRespawnDelayTicks: 16,
            resetCall,
            reloadCall,
            reset,
            resetCallers,
            reload,
            commonLoaderCall,
            ContinuousLevelIndexRuntimeAddress: 0x80075964,
            PositiveLivesDeathState: 4,
            ZeroLivesDeathState: 5,
            PositiveLivesAreDecremented: true,
            ZeroLivesSelectsGameOver: true,
            DeathClearsStateTimers: true,
            DeathWritesPerLevelProgress: false,
            RespawnResetWritesPerLevelProgress: false,
            StateFourReloadUsesContinuousLevelIndex: true,
            StaticDeathRespawnCallgraphClosed: true,
            RemoteSpawnLandingRuntimeAccepted: false,
            RemoteDeathRespawnRuntimeAccepted: false);
    }

    private static void ValidateProgressionSubstrate(UnusedLevel65ProgressionOwnershipContract value)
    {
        if (value.ProfileId != UnusedLevel65ProgressionOwnershipInspector.ProfileId ||
            value.LockedBaseImageSha256 != LockedDisplayNameImageSha256 ||
            value.ExecutableSha256 != ExecutableSha256 ||
            value.RemoteBlankOutputDataSha256 != RemoteRow80DataSha256 ||
            value.RemoteBlankOutputObjectTableSha256 != RemoteObjectTableSha256 ||
            value.Save.SaveStructByteLength != 0x600 ||
            value.Save.ChecksummedByteLength != 0x58C ||
            value.Save.ChecksumFieldOffset != 0x58C ||
            value.Save.ObjectRetirementBitsPerLevel != 256 ||
            !value.Save.Index35NonEggRowsStaticallyRoundTrip ||
            value.Save.Index35EggRowPresent ||
            value.Save.MemoryCardRuntimeOwnershipProven ||
            !value.StaticReadbackOnly || value.WritesBin || value.WritesCue || value.PromotionAuthorized)
        {
            throw new InvalidDataException("The pinned progression substrate changed.");
        }
    }

    private static UnusedLevel65RemoteBlankRuntimeFailureObservation LoadRuntimeFailureEvidence(string root)
    {
        string path = Path.Combine(root, RuntimeFailureEvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException("The exact remote-blank runtime-failure evidence is missing.", path);
        RequireHash(HashFile(path), RuntimeFailureEvidenceSha256, "remote-blank runtime-failure evidence");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement evidence = document.RootElement;
        JsonElement environment = evidence.GetProperty("runtimeEnvironment");
        JsonElement observation = evidence.GetProperty("runtimeObservation");
        JsonElement foundation = evidence.GetProperty("foundationSelectorControl");
        JsonElement correlation = evidence.GetProperty("staticDeathPathCorrelation");
        if (evidence.GetProperty("format").GetString() != "spyro-editor-duckstation-runtime-evidence" ||
            evidence.GetProperty("formatVersion").GetInt32() != 1 ||
            evidence.GetProperty("recordedDate").GetString() != "2026-08-10" ||
            evidence.GetProperty("evidenceStatus").GetString() !=
                "runtime-rejected-no-landing-repeat-death-game-over" ||
            evidence.GetProperty("promotionAuthorized").GetBoolean() ||
            !evidence.GetProperty("doNotLoadOrRetest").GetBoolean() ||
            evidence.GetProperty("outputImageSha256").GetString() != RemoteBlankImageSha256 ||
            evidence.GetProperty("outputId65DataSha256").GetString() != RemoteRow80DataSha256 ||
            environment.GetProperty("card1Type").GetString() != "None" ||
            environment.GetProperty("card2Type").GetString() != "None" ||
            environment.GetProperty("perGameSettingsPresent").GetBoolean() ||
            environment.GetProperty("cheatOverlayObserved").GetBoolean() ||
            environment.GetProperty("saveStateResumeEnabled").GetBoolean() ||
            environment.GetProperty("normalSpeedPercent").GetInt32() != 100 ||
            !observation.GetProperty("coldBoot").GetBoolean() ||
            !observation.GetProperty("startedNewGameWithoutMemoryCard").GetBoolean() ||
            !observation.GetProperty("selectorAccepted").GetBoolean() ||
            !observation.GetProperty("id65FlyInObserved").GetBoolean() ||
            observation.GetProperty("playableLandingObserved").GetBoolean() ||
            !observation.GetProperty("terminalResult").GetString()!.Contains(
                "GAME OVER", StringComparison.Ordinal) ||
            foundation.GetProperty("outputImageSha256").GetString() != FoundationImageSha256 ||
            !foundation.GetProperty("sameColdBootNoCardNoCheatProcedure").GetBoolean() ||
            !foundation.GetProperty("sameHeldButtonSelectorProcedure").GetBoolean() ||
            foundation.GetProperty("livesDisplayedAfterEntry").GetInt32() != 4 ||
            !foundation.GetProperty("playerObservedAliveAfterEntry").GetBoolean() ||
            correlation.GetProperty("deathPlaneCheckExecutableFileOffset").GetString() != "0x3ACF4" ||
            correlation.GetProperty("deathPlaneCallExecutableFileOffset").GetString() != "0x3ACD8" ||
            correlation.GetProperty("playerZRuntimeAddress").GetString() != "0x80078A60" ||
            correlation.GetProperty("deathPlaneThreshold").GetInt32() != 1024 ||
            correlation.GetProperty("deathRoutineRuntimeAddress").GetString() != "0x8002C85C" ||
            correlation.GetProperty("livesRuntimeAddress").GetString() != "0x8007582C")
        {
            throw new InvalidDataException("The exact remote-blank failure evidence semantics changed.");
        }

        return new(
            RuntimeFailureEvidenceRelativePath,
            RuntimeFailureEvidenceSha256,
            "2026-08-10",
            RemoteBlankImageSha256,
            "isolated portable DuckStation; cold boot; Cards 1/2=None; no cheats; no save state; normal 100%; exact ID65 selector",
            "Fly-in showed Spyro against solid green and Spyro never landed.",
            observation.GetProperty("terminalResult").GetString()!,
            8,
            CardsDisabled: true,
            CheatsDisabled: true,
            SaveStateDisabled: true,
            SpyroLanded: false,
            SpawnRuntimePassed: false,
            DeathRespawnRuntimePassed: false,
            InitialLivesRuntimeValueCaptured: false,
            DeathInvocationCountCaptured: false,
            RuntimeRootCauseDiscriminated: false,
            ObservationHasMachineReadableEvidenceArtifact: true,
            FoundationControlUsedSameSelectorProcedure: true,
            FoundationControlLivesDisplayed: 4,
            FoundationControlPlayerStayedAlive: true,
            RepeatedFailedRespawnsCouldExhaustLives: true,
            "The exact code proves Z<1024 invokes death, positive lives decrement into state 4/reload, and zero lives selects state 5. The evidence supports repeated bad-landing deaths as an explanation, but no live-RAM capture proves the remote Z, initial lives value, or death count.");
    }

    private static void ValidatePassiveMobySubstrate(UnusedLevel65MobyDependencyBundleContract value)
    {
        if (value.ProfileId != UnusedLevel65MobyDependencyBundleFoundation.ProfileId ||
            value.FoundationImageSha256 != FoundationImageSha256 ||
            value.DeterministicContractSha256 != UnusedLevel65MobyDependencyBundleFoundation.ExpectedContractSha256 ||
            value.SourceRecord.TrueIndex != 121 || value.SourceRecord.ActorId != 0x01F5 ||
            value.SourceRecord.Identity != "Grass" || value.SourceRecord.IsDragon ||
            value.SourceRecord.IsPortal || value.SourceRecord.IsThief || value.SourceRecord.IsCollectible ||
            value.SourceRecord.IsTotalsLinked || value.SourceRecord.HasRewardDrop ||
            value.SourceRecord.HasLegacySpecialDataPointer || value.Destination.ExistingObjectCount != 107 ||
            value.Destination.FirstAppendTrueIndex != 107 || value.Destination.CompleteAppendRowCapacity != 116 ||
            !value.Destination.StructuralAllocationComplete || !value.StaticRuntimeDependencyClosureComplete ||
            value.BehaviorClosure.PropertiesControllerSemanticsRequired || value.BehaviorClosure.SoundDependencyRequired ||
            value.BehaviorClosure.ParticleOrEffectDependencyRequired || value.BehaviorClosure.DynamicSpawnDependencyRequired ||
            value.BehaviorClosure.DynamicNativeLinkDependencyRequired || value.RunnableBundleSupport ||
            !value.StaticInspectionOnly || value.WritesBin || value.WritesCue || value.ReleasePublicationAuthorized)
        {
            throw new InvalidDataException("The pinned passive-Moby substrate changed.");
        }
    }

    private static void RequireProgressionField(
        UnusedLevel65ProgressionOwnershipContract progression,
        string name,
        int rows,
        int? index35Offset,
        uint? index35Address,
        bool present)
    {
        UnusedLevel65ProgressionSaveField field = progression.Save.Fields.Single(item => item.Name == name);
        if (field.RowCount != rows || field.Index35SaveOffset != index35Offset ||
            field.Index35RuntimeAddress != index35Address || field.Index35StoragePresent != present)
        {
            throw new InvalidDataException($"The exact {name} save-field ownership changed.");
        }
    }

    private static UnusedLevel65SaveDeathRespawnCallEdge[] FindExecutableCallEdges(
        byte[] executable,
        DiscLayout layout,
        uint target,
        string name) =>
        FindDirectJalOffsets(executable, 0x800, ExecutableRuntimeBias, target)
            .Select(offset => ExeJalEdge(executable, layout, checked((int)offset), target, name))
            .ToArray();

    private static long[] FindDirectJalOffsets(
        byte[] bytes,
        int startOffset,
        uint runtimeBias,
        uint target)
    {
        List<long> offsets = [];
        for (int offset = startOffset; offset + 4 <= bytes.Length; offset += 4)
        {
            uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            if (instruction >> 26 != 3)
                continue;
            uint pc = runtimeBias + (uint)offset;
            uint resolved = ((pc + 4) & 0xF0000000) | ((instruction & 0x03FFFFFF) << 2);
            if (resolved == target)
                offsets.Add(offset);
        }
        return offsets.ToArray();
    }

    private static UnusedLevel65SaveDeathRespawnCallEdge ExeJalEdge(
        byte[] executable,
        DiscLayout layout,
        int offset,
        uint target,
        string name)
    {
        uint pc = ExecutableRuntimeBias + (uint)offset;
        RequireJal(executable, offset, pc, target, name);
        return new(
            name,
            "SCUS_942.28",
            offset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, offset),
            pc,
            target,
            Convert.ToHexString(executable.AsSpan(offset, 4)),
            DirectJal: true);
    }

    private static UnusedLevel65SaveDeathRespawnCallEdge OverlayJalEdge(
        byte[] overlay,
        DiscLayout layout,
        int relativeOffset,
        uint target,
        string name)
    {
        uint pc = OverlayRuntimeAddress(relativeOffset);
        RequireJal(overlay, relativeOffset, pc, target, name);
        long wadOffset = MemoryCardOverlayWadOffset + relativeOffset;
        return new(
            name,
            "WAD.WAD entry 2",
            wadOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset),
            pc,
            target,
            Convert.ToHexString(overlay.AsSpan(relativeOffset, 4)),
            DirectJal: true);
    }

    private static void RequireJal(byte[] bytes, int offset, uint pc, uint target, string name)
    {
        uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        uint resolved = ((pc + 4) & 0xF0000000) | ((instruction & 0x03FFFFFF) << 2);
        if (instruction >> 26 != 3 || resolved != target)
            throw new InvalidDataException($"The exact {name} direct JAL changed.");
    }

    private static UnusedLevel65SaveDeathRespawnRegion ExeRegion(
        byte[] executable,
        DiscLayout layout,
        int offset,
        int length,
        string name,
        string? expectedHex = null,
        string? expectedSha256 = null)
    {
        byte[] bytes = executable.AsSpan(offset, length).ToArray();
        ValidateRegion(bytes, name, expectedHex, expectedSha256);
        return new(
            name,
            "SCUS_942.28",
            offset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, offset),
            ExecutableRuntimeBias + (uint)offset,
            length,
            Convert.ToHexString(bytes),
            Hash(bytes));
    }

    private static UnusedLevel65SaveDeathRespawnRegion OverlayRegion(
        byte[] overlay,
        DiscLayout layout,
        int relativeOffset,
        int length,
        string name,
        uint runtimeAddress,
        string? expectedHex = null,
        string? expectedSha256 = null)
    {
        byte[] bytes = overlay.AsSpan(relativeOffset, length).ToArray();
        ValidateRegion(bytes, name, expectedHex, expectedSha256);
        long wadOffset = MemoryCardOverlayWadOffset + relativeOffset;
        return new(
            name,
            "WAD.WAD entry 2",
            wadOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset),
            runtimeAddress,
            length,
            Convert.ToHexString(bytes),
            Hash(bytes));
    }

    private static UnusedLevel65SaveDeathRespawnRegion WadRegion(
        string name,
        long wadOffset,
        byte[] bytes,
        DiscLayout layout,
        uint? runtimeAddress,
        string? expectedSha256 = null)
    {
        ValidateRegion(bytes, name, expectedHex: null, expectedSha256);
        return new(
            name,
            "WAD.WAD",
            wadOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset),
            runtimeAddress,
            bytes.Length,
            Convert.ToHexString(bytes),
            Hash(bytes));
    }

    private static uint OverlayRuntimeAddress(int relativeOffset) =>
        OverlayCodeRuntimeBase + checked((uint)(relativeOffset - MemoryCardHeaderByteLength));

    private static void ValidateRegion(
        byte[] bytes,
        string name,
        string? expectedHex,
        string? expectedSha256)
    {
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        if (expectedSha256 != null)
            RequireHash(Hash(bytes), expectedSha256, name);
    }

    private static void RequireWord(byte[] executable, int offset, uint expected, string name)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(offset, 4));
        if (actual != expected)
            throw new InvalidDataException($"The exact {name} word was 0x{actual:X8}, expected 0x{expected:X8}.");
    }

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedLength)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedLength)
            throw new InvalidDataException($"The exact {name} mapping changed.");
        return record;
    }

    private static void RequireMode2(DiscLayout layout, string name)
    {
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException($"The {name} is not exact MODE2/2352 Form1 data.");
    }

    private static string RequireFile(string path, string name)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The exact {name} is missing.", fullPath);
        return fullPath;
    }

    private static void RequireHex(byte[] bytes, string expectedHex, string name)
    {
        if (!bytes.SequenceEqual(Convert.FromHexString(expectedHex)))
            throw new InvalidDataException($"The exact {name} bytes changed.");
    }

    private static void RequireHash(string actual, string expected, string name)
    {
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {name} SHA-256 was {actual}, expected {expected}.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static FilePatternScan ScanFileAndCountPattern(string path, byte[] pattern)
    {
        if (pattern.Length < 1)
            throw new ArgumentException("A non-empty byte pattern is required.", nameof(pattern));
        using FileStream stream = File.OpenRead(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024 + pattern.Length - 1];
        int carry = 0;
        int count = 0;
        while (true)
        {
            int read = stream.Read(buffer, carry, buffer.Length - carry);
            if (read == 0)
                break;
            hash.AppendData(buffer.AsSpan(carry, read));
            int total = carry + read;
            for (int index = 0; index + pattern.Length <= total; index++)
            {
                if (buffer.AsSpan(index, pattern.Length).SequenceEqual(pattern))
                    count++;
            }
            carry = Math.Min(pattern.Length - 1, total);
            buffer.AsSpan(total - carry, carry).CopyTo(buffer);
        }
        return new(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), count);
    }

    private sealed record FilePatternScan(string Sha256, int PatternCount);
}
