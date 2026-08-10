using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

internal enum UnusedLevel65ProgressionOwnershipState
{
    IndependentSlot,
    IndependentSlotWithInheritedValue,
    ValueAliasOnly,
    ByteCloneAlias,
    MissingBoundedRow,
    RuntimeProofRequired,
    UnsafeInIsolation
}

internal sealed record UnusedLevel65ProgressionByteRegion(
    string Name,
    string Container,
    long LogicalOffset,
    long ImageOffset,
    uint? RuntimeAddress,
    int ByteLength,
    string Hex,
    string Sha256,
    UnusedLevel65ProgressionOwnershipState State);

internal sealed record UnusedLevel65ProgressionCallEdge(
    string Name,
    long CallerFileOffset,
    uint CallerRuntimeAddress,
    uint CalleeRuntimeAddress,
    string InstructionHex,
    bool DirectJal);

internal sealed record UnusedLevel65ProgressionAlias(
    string Domain,
    string Id65Storage,
    string Id65Value,
    IReadOnlyList<int> RetailLevelIds,
    IReadOnlyList<string> RetailLevelNames,
    bool SharesPhysicalStorage,
    bool SharesOnlyValue,
    string Evidence);

internal sealed record UnusedLevel65ProgressionMusicOwnership(
    int ContinuousIndex,
    UnusedLevel65ProgressionByteRegion MappingTable,
    UnusedLevel65ProgressionByteRegion InitialSlot,
    int InitialTrackId,
    UnusedLevel65ProgressionByteRegion InitialConsumer,
    UnusedLevel65ProgressionByteRegion LateAlternateTable,
    int LateAlternateLevelRows,
    int LateAlternateValuesPerRow,
    UnusedLevel65ProgressionByteRegion LateConsumer,
    UnusedLevel65ProgressionByteRegion WouldBeRow35Collision,
    IReadOnlyList<int> WouldBeRow35PetexaLbas,
    bool InitialSlotIndependent,
    bool LongPlayRowPresent,
    bool FourByteInitialOnlyTransactionSafe,
    bool RuntimeOwnershipProven);

internal sealed record UnusedLevel65ProgressionTotalsOwnership(
    UnusedLevel65ProgressionByteRegion DragonTable,
    UnusedLevel65ProgressionByteRegion DragonSlot,
    int DragonTarget,
    UnusedLevel65ProgressionByteRegion DragonConsumer,
    UnusedLevel65ProgressionByteRegion TreasureTable,
    UnusedLevel65ProgressionByteRegion TreasureSlot,
    int TreasureTarget,
    UnusedLevel65ProgressionByteRegion TreasureConsumer,
    UnusedLevel65ProgressionByteRegion EggTable,
    int EggTargetRowCount,
    UnusedLevel65ProgressionByteRegion EggConsumerBounds,
    bool DragonSlotIndependent,
    bool TreasureSlotIndependent,
    bool EggSlotPresent,
    int SmallestDragonTreasurePatchBytes,
    bool ZeroEggProfileRequired,
    bool RemoteObjectCensusClosed,
    bool RuntimeOwnershipProven);

internal sealed record UnusedLevel65ProgressionExitOwnership(
    UnusedLevel65ProgressionByteRegion PortalGuardPath,
    long GuardInstructionFileOffset,
    string GuardInstructionHex,
    int CurrentUpperExclusive,
    int RequiredUpperExclusive,
    int GuardLogicalPatchBytes,
    UnusedLevel65ProgressionByteRegion SourcePortalTable,
    UnusedLevel65ProgressionByteRegion RemotePortalTable,
    int RemotePortalCount,
    UnusedLevel65ProgressionByteRegion TownSquareReturnHomeVisible,
    UnusedLevel65ProgressionByteRegion TownSquareReturnHomeHelper,
    UnusedLevel65ProgressionByteRegion RemoteReturnHomeVisible,
    UnusedLevel65ProgressionByteRegion RemoteReturnHomeHelper,
    double ReturnHomeSceneDistanceFromRemotePad,
    IReadOnlyList<int> GnastyWorldDestinationLevelIds,
    bool ReturnHomeRowsAreTownSquareByteClones,
    bool ReturnHomePairMetadataExistsForTownSquare,
    bool ReturnHomePairMetadataExistsForLab,
    bool GnastyWorldHasDestination65,
    bool OneByteGuardTransactionSafe,
    bool ExitRuntimeOwnershipProven,
    bool ReturnHomeRuntimeOwnershipProven);

internal sealed record UnusedLevel65ProgressionDeathOwnership(
    UnusedLevel65ProgressionByteRegion DeathLifeRoutine,
    IReadOnlyList<UnusedLevel65ProgressionCallEdge> DirectCallers,
    UnusedLevel65ProgressionByteRegion NewGameResetRoutine,
    IReadOnlyList<UnusedLevel65ProgressionCallEdge> NewGameResetDirectCallers,
    uint LivesRuntimeAddress,
    uint DeathStateRuntimeAddress,
    bool DeathCallsNewGameReset,
    bool DeathWritesPerLevelProgressArrays,
    bool StaticRetentionSupported,
    bool RemoteDeathPlaneRuntimeVerified,
    bool RemoteDeathRespawnRuntimeVerified);

internal sealed record UnusedLevel65ProgressionSaveField(
    string Name,
    int RowCount,
    int ElementByteLength,
    int SaveOffset,
    uint RuntimeBaseAddress,
    int RuntimeStride,
    int? Index35SaveOffset,
    uint? Index35RuntimeAddress,
    bool Index35StoragePresent,
    string Boundary);

internal sealed record UnusedLevel65ProgressionSaveOwnership(
    UnusedLevel65ProgressionByteRegion ChecksumRoutine,
    UnusedLevel65ProgressionByteRegion DeserializeRoutine,
    UnusedLevel65ProgressionByteRegion SerializeRoutine,
    UnusedLevel65ProgressionByteRegion DeserializePerLevelLoop,
    UnusedLevel65ProgressionByteRegion SerializePerLevelLoop,
    UnusedLevel65ProgressionByteRegion DeserializeRetirementCopy,
    UnusedLevel65ProgressionByteRegion SerializeRetirementCopy,
    IReadOnlyList<UnusedLevel65ProgressionCallEdge> SerializeDirectCallers,
    IReadOnlyList<UnusedLevel65ProgressionCallEdge> ChecksumDirectCallers,
    int DeserializeDirectCallerCount,
    int SaveStructByteLength,
    int ChecksummedByteLength,
    int ChecksumFieldOffset,
    int ObjectRetirementBitsPerLevel,
    int RemoteObjectRecordCount,
    IReadOnlyList<UnusedLevel65ProgressionSaveField> Fields,
    bool Index35NonEggRowsStaticallyRoundTrip,
    bool Index35EggRowPresent,
    bool CurrentRemoteObjectTableFitsRetirementBitmap,
    bool FullAuthoringObjectCapacityUnbounded,
    bool DeserializeCallerClosureResolved,
    bool MemoryCardRuntimeOwnershipProven);

internal sealed record UnusedLevel65ProgressionTransactionAssessment(
    string Domain,
    int MinimumLogicalPatchBytes,
    string SmallestCandidate,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Blockers,
    bool StructurallyClosed,
    bool WriterAuthorized,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65ProgressionOwnershipContract(
    string ProfileId,
    string LockedBaseImageSha256,
    string ExecutableSha256,
    string RemoteBlankProfileId,
    string RemoteBlankOutputDataSha256,
    string RemoteBlankOutputObjectTableSha256,
    UnusedLevel65ProgressionByteRegion Executable,
    UnusedLevel65ProgressionByteRegion LockedRow80Data,
    UnusedLevel65ProgressionByteRegion RemoteRow80Data,
    UnusedLevel65ProgressionByteRegion TownSquareObjectTable,
    UnusedLevel65ProgressionByteRegion RemoteObjectTable,
    UnusedLevel65ProgressionMusicOwnership Music,
    UnusedLevel65ProgressionTotalsOwnership Totals,
    UnusedLevel65ProgressionExitOwnership Exit,
    UnusedLevel65ProgressionDeathOwnership Death,
    UnusedLevel65ProgressionSaveOwnership Save,
    IReadOnlyList<UnusedLevel65ProgressionAlias> CrossLevelAliases,
    IReadOnlyList<UnusedLevel65ProgressionTransactionAssessment> Transactions,
    IReadOnlyList<string> CombinedTransactionBlockers,
    bool LockedImagePreserved,
    bool RemotePlanIsInMemoryOnly,
    bool ExecutableExcludedFromRemotePlan,
    bool StaticReadbackOnly,
    bool WritesBin,
    bool WritesCue,
    bool StandaloneCombinedWriterAuthorized,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

/// <summary>
/// Static ownership closure for ID65 progression and lifecycle systems. This
/// contract starts from the exact runtime-passed display-name BIN and composes
/// the exact remote-blank row-80 result in memory. It maps the executable
/// consumers and aliases that remain outside row 80, but cannot write or
/// promote a candidate while any runtime or bounded-table dependency is open.
/// </summary>
internal static class UnusedLevel65ProgressionOwnershipInspector
{
    public const string ProfileId =
        "unused-level-65-progression-ownership-remote-blank-static-clean-usa-v1";
    public const string LockedBaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string RemoteBlankOutputDataSha256 =
        "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061";
    public const string RemoteBlankOutputObjectTableSha256 =
        "f89347baf4f14739030499b21210b8ea46b44f9c4df02a1b58d39fde6ca1e64a";

    private const int WadLba = 37;
    private const int ExecutableLba = 55_382;
    private const int ExecutableLength = 0x66000;
    private const uint ExecutableRuntimeBias = 0x8000F800;
    private const long DataWadOffset = 0x6936800;
    private const int DataLength = 0x2E2000;
    private const long TownSquareObjectTableWadOffset = 0x136E170;
    private const long Id65ObjectTableWadOffset = 0x6B06970;
    private const int ObjectRecordCount = 107;
    private const int ObjectRecordLength = 0x58;
    private const int ObjectTableDataOffset = 0x1D0170;
    private const int ContinuousIndex = 35;

    private const int MusicMappingOffset = 0x5F79C;
    private const int MusicMappingLength = 48 * 4;
    private const int MusicSlotOffset = MusicMappingOffset + ContinuousIndex * 4;
    private const int LateMusicOffset = 0x5F85C;
    private const int LateMusicRows = 35;
    private const int LateMusicLength = LateMusicRows * 3 * 4;
    private const int PetexaStartOffset = 0x5FA00;
    private const int DragonTableOffset = 0x5FC14;
    private const int DragonTableLength = 36;
    private const int TreasureTableOffset = 0x5FC38;
    private const int TreasureTableLength = 36 * 2;
    private const int EggTableOffset = 0x5FC80;
    private const int EggTableLength = 20;
    private const int PortalGuardInstructionOffset = 0x47910;

    public static UnusedLevel65ProgressionOwnershipContract Inspect(
        string workspaceRoot,
        string lockedBaseImagePath)
    {
        string root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string imagePath = Path.GetFullPath(lockedBaseImagePath);
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The exact ID65 display-name BIN is missing.", imagePath);
        RequireHash(HashFile(imagePath), LockedBaseImageSha256, "locked ID65 display-name BIN");

        UnusedLevel65RemoteBlankStaticPlan remote =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(imagePath);
        if (remote.ProfileId != UnusedLevel65RemoteBlankIsolationConstruction.ProfileId ||
            remote.OutputDataSha256 != RemoteBlankOutputDataSha256 ||
            !remote.ExecutableExcluded || !remote.ProtectedSubfilesPreserved ||
            !remote.InheritedObjectRowsPreserved || remote.SourceImageMutationPossible)
        {
            throw new InvalidDataException("The exact remote-blank in-memory construction boundary changed.");
        }

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The locked ID65 image is not exact MODE2/2352.");
        using FileStream image = File.OpenRead(imagePath);
        DiscFileRecord exeRecord = RequireRootFile(image, layout, "SCUS_942.28", ExecutableLba, ExecutableLength);
        _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, 0x6C18800);
        byte[] executable = DiscImage.ReadFileBytes(image, layout, exeRecord.Lba, 0, exeRecord.Size);
        RequireHash(Hash(executable), ExecutableSha256, "relocated executable");

        byte[] lockedData = DiscImage.ReadFileBytes(image, layout, WadLba, DataWadOffset, DataLength);
        RequireHash(Hash(lockedData), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceDataSha256, "locked row-80 data");
        if (!lockedData.SequenceEqual(remote.SourceData))
            throw new InvalidDataException("The remote plan does not consume the exact locked row-80 data.");
        byte[] townSquareObjects = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            TownSquareObjectTableWadOffset,
            ObjectRecordCount * ObjectRecordLength);
        byte[] sourceObjects = lockedData.AsSpan(ObjectTableDataOffset, ObjectRecordCount * ObjectRecordLength).ToArray();
        byte[] remoteObjects = remote.OutputData.AsSpan(ObjectTableDataOffset, ObjectRecordCount * ObjectRecordLength).ToArray();
        RequireHash(Hash(townSquareObjects), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceObjectTableSha256, "Town Square object table");
        if (!sourceObjects.SequenceEqual(townSquareObjects))
            throw new InvalidDataException("ID65 no longer starts from the exact Town Square object-table clone.");
        RequireHash(Hash(remoteObjects), RemoteBlankOutputObjectTableSha256, "remote ID65 object table");

        byte[] townReturnVisible = SliceObject(townSquareObjects, 96);
        byte[] townReturnHelper = SliceObject(townSquareObjects, 97);
        byte[] remoteReturnVisible = SliceObject(remoteObjects, 96);
        byte[] remoteReturnHelper = SliceObject(remoteObjects, 97);
        if (!townReturnVisible.SequenceEqual(remoteReturnVisible) || !townReturnHelper.SequenceEqual(remoteReturnHelper))
            throw new InvalidDataException("Remote construction changed the inherited Return Home pair.");

        LevelCatalog catalog = LevelCatalog.Load(root);
        if (catalog.Levels.Count != 35)
            throw new InvalidDataException("The exact 35-level retail catalog is required.");
        Dictionary<int, LevelDefinition> byIndex = catalog.Levels.ToDictionary(LevelIndex);
        if (byIndex.Count != 35 || byIndex.ContainsKey(ContinuousIndex))
            throw new InvalidDataException("The retail catalog unexpectedly owns continuous index 35.");

        UnusedLevel65ProgressionByteRegion mapping = ExeRegion(
            executable, layout, MusicMappingOffset, MusicMappingLength, "music mapping table",
            UnusedLevel65ProgressionOwnershipState.IndependentSlotWithInheritedValue,
            expectedSha256: "a4be62ad38b6c793831d9cdcc3d92d24278e18df4fab19ddc678ed29e191e5f5");
        UnusedLevel65ProgressionByteRegion musicSlot = ExeRegion(
            executable, layout, MusicSlotOffset, 4, "ID65 initial music slot",
            UnusedLevel65ProgressionOwnershipState.IndependentSlotWithInheritedValue,
            expectedHex: "13000000");
        UnusedLevel65ProgressionByteRegion initialConsumer = ExeRegion(
            executable, layout, 0x6504, 0x24, "initial music index consumer",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "7698774b00c290b327fe980ea76a6aec22c36ada856351a3f85b32368529d106");
        UnusedLevel65ProgressionByteRegion lateTable = ExeRegion(
            executable, layout, LateMusicOffset, LateMusicLength, "35-row late music source table",
            UnusedLevel65ProgressionOwnershipState.MissingBoundedRow,
            expectedSha256: "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c");
        UnusedLevel65ProgressionByteRegion lateConsumer = ExeRegion(
            executable, layout, 0x1C594, 0x70, "late music index consumer",
            UnusedLevel65ProgressionOwnershipState.UnsafeInIsolation,
            expectedSha256: "646ab5899fbbee768024bbb6be81a72bafa8d0c2c43483dad5958aa97d184de2");
        UnusedLevel65ProgressionByteRegion wouldBeLateRow = ExeRegion(
            executable, layout, PetexaStartOffset, 12, "would-be late row 35 / PETEXA0-2 LBA collision",
            UnusedLevel65ProgressionOwnershipState.UnsafeInIsolation,
            expectedHex: "60EA0000684A010028BB0100");
        int[] petexaCollision = Enumerable.Range(0, 3)
            .Select(index => ReadInt32(executable, PetexaStartOffset + index * 4)).ToArray();
        if (!petexaCollision.SequenceEqual(new[] { 60_000, 84_584, 113_448 }))
            throw new InvalidDataException("The late-music/PETEXA adjacency changed.");

        UnusedLevel65ProgressionMusicOwnership music = new(
            ContinuousIndex, mapping, musicSlot, 19, initialConsumer, lateTable, LateMusicRows, 3,
            lateConsumer, wouldBeLateRow, petexaCollision,
            InitialSlotIndependent: true, LongPlayRowPresent: false,
            FourByteInitialOnlyTransactionSafe: false, RuntimeOwnershipProven: false);

        UnusedLevel65ProgressionByteRegion dragonTable = ExeRegion(
            executable, layout, DragonTableOffset, DragonTableLength, "dragon targets",
            UnusedLevel65ProgressionOwnershipState.IndependentSlot,
            expectedSha256: "369c57dff04b1b608adb95b2b42688a0380f61ccf5445446e423d1badbc03699");
        UnusedLevel65ProgressionByteRegion dragonSlot = ExeRegion(
            executable, layout, DragonTableOffset + ContinuousIndex, 1, "ID65 dragon target",
            UnusedLevel65ProgressionOwnershipState.IndependentSlot,
            expectedHex: "00");
        UnusedLevel65ProgressionByteRegion dragonConsumer = ExeRegion(
            executable, layout, 0xC918, 0x24, "dragon target index consumer",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "385b1ef8ebe1d06a65fec4aaa97a4ee9382120c21ebec946c71dd9473f68aac4");
        UnusedLevel65ProgressionByteRegion treasureTable = ExeRegion(
            executable, layout, TreasureTableOffset, TreasureTableLength, "treasure targets",
            UnusedLevel65ProgressionOwnershipState.IndependentSlot,
            expectedSha256: "641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6");
        UnusedLevel65ProgressionByteRegion treasureSlot = ExeRegion(
            executable, layout, TreasureTableOffset + ContinuousIndex * 2, 2, "ID65 treasure target",
            UnusedLevel65ProgressionOwnershipState.IndependentSlot,
            expectedHex: "0000");
        UnusedLevel65ProgressionByteRegion treasureConsumer = ExeRegion(
            executable, layout, 0xC8CC, 0x20, "treasure target index consumer",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "151c3b09d17ae38712c9ce5b7709632b1cf41f1d9056ea1977204469d20a44dd");
        UnusedLevel65ProgressionByteRegion eggTable = ExeRegion(
            executable, layout, EggTableOffset, EggTableLength, "bounded egg targets",
            UnusedLevel65ProgressionOwnershipState.MissingBoundedRow,
            expectedSha256: "6d4de8ed9543f9dc758c3824ac0e4bebcf75f681de15770355b7b8820e6fd41b");
        UnusedLevel65ProgressionByteRegion eggBounds = ExeRegion(
            executable, layout, 0xCB18, 0x40, "egg target bounded consumer",
            UnusedLevel65ProgressionOwnershipState.MissingBoundedRow,
            expectedSha256: "91d7b6d8160054b1a9cfd539a378ede8e047f60e0d1ebde8ef4f890e4073d28c");
        UnusedLevel65ProgressionTotalsOwnership totals = new(
            dragonTable, dragonSlot, 0, dragonConsumer,
            treasureTable, treasureSlot, 0, treasureConsumer,
            eggTable, EggTableLength, eggBounds,
            DragonSlotIndependent: true, TreasureSlotIndependent: true, EggSlotPresent: false,
            SmallestDragonTreasurePatchBytes: 3, ZeroEggProfileRequired: true,
            RemoteObjectCensusClosed: false, RuntimeOwnershipProven: false);

        UnusedLevel65RemoteBlankComponentProof sourcePortal = remote.Components.Single(component => component.Name == "portal table");
        long sourcePortalWadOffset = remote.ModelWadOffset + sourcePortal.SourceRelativeOffset;
        long remotePortalWadOffset = remote.ModelWadOffset + sourcePortal.OutputRelativeOffset;
        byte[] sourcePortalBytes = remote.SourceData.AsSpan(
            checked((int)(sourcePortalWadOffset - remote.DataWadOffset)), sourcePortal.ByteLength).ToArray();
        byte[] remotePortalBytes = remote.OutputData.AsSpan(
            checked((int)(remotePortalWadOffset - remote.DataWadOffset)), sourcePortal.ByteLength).ToArray();
        RequireHex(sourcePortalBytes, "00000000", "source ID65 portal table");
        RequireHex(remotePortalBytes, "00000000", "remote ID65 portal table");
        UnusedLevel65ProgressionByteRegion portalGuard = ExeRegion(
            executable, layout, 0x47904, 0x34, "normal portal transition guard path",
            UnusedLevel65ProgressionOwnershipState.UnsafeInIsolation,
            expectedSha256: "32b07aa096fba6f638f262fdda1ce37e9d903ca73382953025030f70f0ef0eb8");
        RequireHex(executable.AsSpan(PortalGuardInstructionOffset, 4).ToArray(), "41004228", "portal <65 guard");

        LevelDefinition gnastyWorld = catalog.FindByKey("gnastysworld")
            ?? throw new InvalidDataException("Gnasty's World is missing from the retail catalog.");
        int[] gnastyDestinations = PortalSourceDataLocator.Locate(imagePath, gnastyWorld)
            .Portals.Select(portal => portal.DestinationLevelId).ToArray();
        if (!gnastyDestinations.SequenceEqual(new[] { 62, 63, 64, 61 }))
            throw new InvalidDataException("Gnasty's World portal destinations changed.");
        bool townLink = VerifyTownSquareReturnHomeLink(root);
        bool labLink = File.Exists(Path.Combine(root, "unusedlevel65blank-behavior-links.json")) ||
            File.Exists(Path.Combine(
                root,
                "_local",
                "control-role-proof-review",
                "promoted-behavior-links",
                "unusedlevel65blank-behavior-links.json"));
        int returnX = ReadInt32(remoteReturnVisible, 0x0C);
        int returnY = ReadInt32(remoteReturnVisible, 0x10);
        double returnDistance = Math.Sqrt(
            Math.Pow((returnX / 16.0) - remote.Scene.CullCenter.X, 2) +
            Math.Pow((returnY / 16.0) - remote.Scene.CullCenter.Y, 2));

        UnusedLevel65ProgressionExitOwnership exit = new(
            portalGuard, PortalGuardInstructionOffset, "41004228", 65, 66, 1,
            WadRegion("source zero portal table", sourcePortalWadOffset, sourcePortalBytes, layout,
                UnusedLevel65ProgressionOwnershipState.MissingBoundedRow, hasPhysicalImage: true),
            WadRegion("remote zero portal table", remotePortalWadOffset, remotePortalBytes, layout,
                UnusedLevel65ProgressionOwnershipState.MissingBoundedRow, hasPhysicalImage: false),
            0,
            WadRegion("Town Square T96 Return Home", TownSquareObjectTableWadOffset + 96L * ObjectRecordLength,
                townReturnVisible, layout, UnusedLevel65ProgressionOwnershipState.ByteCloneAlias, true),
            WadRegion("Town Square T97 Return Home helper", TownSquareObjectTableWadOffset + 97L * ObjectRecordLength,
                townReturnHelper, layout, UnusedLevel65ProgressionOwnershipState.ByteCloneAlias, true),
            WadRegion("remote ID65 T96 Return Home", Id65ObjectTableWadOffset + 96L * ObjectRecordLength,
                remoteReturnVisible, layout, UnusedLevel65ProgressionOwnershipState.ByteCloneAlias, false),
            WadRegion("remote ID65 T97 Return Home helper", Id65ObjectTableWadOffset + 97L * ObjectRecordLength,
                remoteReturnHelper, layout, UnusedLevel65ProgressionOwnershipState.ByteCloneAlias, false),
            returnDistance, gnastyDestinations,
            ReturnHomeRowsAreTownSquareByteClones: true,
            ReturnHomePairMetadataExistsForTownSquare: townLink,
            ReturnHomePairMetadataExistsForLab: labLink,
            GnastyWorldHasDestination65: false,
            OneByteGuardTransactionSafe: false,
            ExitRuntimeOwnershipProven: false,
            ReturnHomeRuntimeOwnershipProven: false);

        UnusedLevel65ProgressionByteRegion deathRoutine = ExeRegion(
            executable, layout, 0x1D05C, 0x48, "death/life-loss routine",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad");
        UnusedLevel65ProgressionByteRegion resetRoutine = ExeRegion(
            executable, layout, 0x2E04, 0x178, "new-game/no-save reset routine",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "57e807bf638fe6394f46cc7c673f5fc27e8910fc8fce0acee60761c51548538f");
        UnusedLevel65ProgressionCallEdge[] deathCallers = FindDirectJalCallers(executable, 0x8002C85C, "death/life-loss");
        UnusedLevel65ProgressionCallEdge[] resetCallers = FindDirectJalCallers(executable, 0x80012604, "new-game reset");
        if (!deathCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x33710, 0x3ACD8 }) ||
            !resetCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x2FA8 }))
        {
            throw new InvalidDataException("The exact death/reset direct call graph changed.");
        }
        UnusedLevel65ProgressionDeathOwnership death = new(
            deathRoutine, deathCallers, resetRoutine, resetCallers,
            LivesRuntimeAddress: 0x8007582C, DeathStateRuntimeAddress: 0x800757D8,
            DeathCallsNewGameReset: false, DeathWritesPerLevelProgressArrays: false,
            StaticRetentionSupported: true,
            RemoteDeathPlaneRuntimeVerified: remote.Spawn.DeathPlaneRuntimeVerified,
            RemoteDeathRespawnRuntimeVerified: remote.Spawn.DeathRespawnRuntimeVerified);

        UnusedLevel65ProgressionByteRegion checksum = ExeRegion(
            executable, layout, 0x49D6C, 0x28, "save checksum routine",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db");
        UnusedLevel65ProgressionByteRegion deserialize = ExeRegion(
            executable, layout, 0x49D94, 0x2D0, "save deserialize routine",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6");
        UnusedLevel65ProgressionByteRegion serialize = ExeRegion(
            executable, layout, 0x4A064, 0x1E4, "save serialize routine",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056");
        UnusedLevel65ProgressionByteRegion deserializeLoop = ExeRegion(
            executable, layout, 0x49F3C, 0xBC, "deserialize 36-row/18-egg loop",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "69bea210fee604fb283fd4043153ce6439865b22d0040006111d74fd9722800a");
        UnusedLevel65ProgressionByteRegion serializeLoop = ExeRegion(
            executable, layout, 0x4A190, 0x58, "serialize 36-row loop",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "404cbf4ee735b108193c9ea301e3ebceac4671e18e752af36d0b21a8e65d031d");
        UnusedLevel65ProgressionByteRegion deserializeRetirement = ExeRegion(
            executable, layout, 0x4A020, 0x18, "deserialize retirement bitmap copy",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "a631d097ec4d486cec949da6e64eaeb4ec37aeb9236ba206de6d3d16cda24a6c");
        UnusedLevel65ProgressionByteRegion serializeRetirement = ExeRegion(
            executable, layout, 0x4A210, 0x18, "serialize retirement bitmap copy",
            UnusedLevel65ProgressionOwnershipState.RuntimeProofRequired,
            expectedSha256: "383479d8e0ef9e122c71ce5b936ab77b13eaa522622d0785a250c90ef6c46ab1");
        UnusedLevel65ProgressionCallEdge[] serializeCallers = FindDirectJalCallers(executable, 0x80059864, "save serialize");
        UnusedLevel65ProgressionCallEdge[] checksumCallers = FindDirectJalCallers(executable, 0x8005956C, "save checksum");
        UnusedLevel65ProgressionCallEdge[] deserializeCallers = FindDirectJalCallers(executable, 0x80059594, "save deserialize");
        if (!serializeCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x229F4 }) ||
            !checksumCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x22AF8, 0x4A034, 0x4A224 }) ||
            deserializeCallers.Length != 0)
        {
            throw new InvalidDataException("The exact resident save direct-call graph changed.");
        }

        UnusedLevel65ProgressionSaveField[] saveFields =
        [
            new("current-level-id", 1, 1, 0x000, 0x8007596C, 0, 0x000, 0x8007596C, true,
                "One global byte can encode level ID 65; it is not a per-level slot."),
            new("visited", 36, 1, 0x040, 0x80078E78, 1, 0x063, 0x80078E9B, true,
                "Index 35 is the final distinct byte."),
            new("unresolved-second-per-level-byte-table", 36, 1, 0x064, 0x8007A6A8, 1, 0x087, 0x8007A6CB, true,
                "The row round-trips, but its gameplay semantics remain unresolved."),
            new("dragons", 36, 1, 0x088, 0x800772D8, 4, 0x0AB, 0x80077364, true,
                "The low byte of each four-byte runtime counter is serialized."),
            new("treasure", 36, 2, 0x0AC, 0x80077420, 4, 0x0F2, 0x800774AC, true,
                "One UInt16 is serialized from each four-byte runtime counter."),
            new("eggs", 18, 1, 0x0F4, 0x80076FE8, 4, null, null, false,
                "The exact loop bound is 18; index 35 has no egg persistence row."),
            new("object-retirement-bitmaps", 36, 0x20, 0x10C, 0x80077908, 0x20, 0x56C, 0x80077D68, true,
                "Index 35 owns exactly 256 retirement bits at save+0x56C..0x58B.")
        ];
        UnusedLevel65ProgressionSaveOwnership save = new(
            checksum, deserialize, serialize, deserializeLoop, serializeLoop,
            deserializeRetirement, serializeRetirement, serializeCallers, checksumCallers,
            DeserializeDirectCallerCount: 0,
            SaveStructByteLength: 0x600, ChecksummedByteLength: 0x58C, ChecksumFieldOffset: 0x58C,
            ObjectRetirementBitsPerLevel: 256, RemoteObjectRecordCount: ObjectRecordCount,
            saveFields,
            Index35NonEggRowsStaticallyRoundTrip: true,
            Index35EggRowPresent: false,
            CurrentRemoteObjectTableFitsRetirementBitmap: ObjectRecordCount <= 256,
            FullAuthoringObjectCapacityUnbounded: false,
            DeserializeCallerClosureResolved: false,
            MemoryCardRuntimeOwnershipProven: false);

        int[] musicAliases = FindRetailValueAliases(executable, MusicMappingOffset, 36, 4, 19, byIndex);
        int[] dragonAliases = FindRetailValueAliases(executable, DragonTableOffset, 36, 1, 0, byIndex);
        int[] treasureAliases = FindRetailValueAliases(executable, TreasureTableOffset, 36, 2, 0, byIndex);
        UnusedLevel65ProgressionAlias[] aliases =
        [
            Alias("initial-music", "SCUS slot 35 @ 0x5F828", "track 19", musicAliases, byIndex,
                "Slot 35 is physically distinct but its inherited value equals Gnasty's Loot."),
            Alias("dragon-target", "SCUS slot 35 @ 0x5FC37", "0", dragonAliases, byIndex,
                "Slot 35 is physically distinct; zero is also used by flights, Gnasty Gnorc, and Gnasty's Loot."),
            Alias("treasure-target", "SCUS slot 35 @ 0x5FC7E", "0", treasureAliases, byIndex,
                "No retail level has the zero treasure target; storage and value are both ID65-specific."),
            new("return-home-visible", "row-80 T96 @ WAD 0x6B08A70", Hash(remoteReturnVisible), [13], ["Town Square"],
                SharesPhysicalStorage: false, SharesOnlyValue: false,
                "The complete 0x58-byte row is a byte-for-byte Town Square T96 clone."),
            new("return-home-helper", "row-80 T97 @ WAD 0x6B08AC8", Hash(remoteReturnHelper), [13], ["Town Square"],
                SharesPhysicalStorage: false, SharesOnlyValue: false,
                "The complete 0x58-byte row is a byte-for-byte Town Square T97 clone."),
            new("portal-table", $"remote row-80 portal count @ WAD 0x{remotePortalWadOffset:X}", "zero portals", [13], ["Town Square"],
                SharesPhysicalStorage: false, SharesOnlyValue: false,
                "The complete four-byte zero portal table remains a byte-for-byte Town Square clone after remote construction."),
            new("save-index-35", "save offsets 0x63/0x87/0xAB/0xF2/0x56C", "distinct final rows", [], [],
                SharesPhysicalStorage: false, SharesOnlyValue: false,
                "Visited, unresolved byte, dragons, treasure, and retirement each have unique index-35 storage; eggs do not.")
        ];

        UnusedLevel65ProgressionTransactionAssessment[] transactions =
        [
            new("music", 4, "patch initial slot 35",
                ["exact slot-35 preimage", "chosen built-in track"],
                ["late consumer indexes a missing row 35", "naive row extension overwrites PETEXA0-2 LBAs", "8-12 minute/death/re-entry runtime gate"],
                StructurallyClosed: false, WriterAuthorized: false, PromotionAuthorized: false),
            new("totals", 3, "patch dragon byte plus treasure UInt16 for slot 35",
                ["complete authored reward census", "zero-egg profile", "relocated SCUS addressing"],
                ["remote blank retains 106 inherited Mobys", "egg target and save schemas have no index-35 row", "collection/death/re-entry runtime gate"],
                StructurallyClosed: false, WriterAuthorized: false, PromotionAuthorized: false),
            new("exit-return-home", 1, "change portal guard immediate from <65 to <66",
                ["atomic T96/T97 authored placement and link", "non-retail arrival route for ID65"],
                ["guard alone strands transition semantics", "Gnasty's World has no destination 65", "Lab metadata does not load the Town Square T96/T97 link", "Return Home pair remains far outside the remote pad"],
                StructurallyClosed: false, WriterAuthorized: false, PromotionAuthorized: false),
            new("death-respawn", 0, "reuse the retail common death routine and remote landing/T92",
                ["remote death-plane trigger", "remote landing collision", "cold death/respawn runtime gate"],
                ["remote static plan explicitly has no death-plane or death-respawn runtime proof"],
                StructurallyClosed: true, WriterAuthorized: false, PromotionAuthorized: false),
            new("save", 0, "reuse existing index-35 rows under an explicit no-egg and <=256-retirement-ID policy",
                ["fresh/old/corrupt card compatibility", "resolved deserialize caller closure", "exit and death gates"],
                ["no egg row", "unresolved second per-level byte", "full-authoring object count must remain <=256 retirement IDs", "memory-card runtime proof absent"],
                StructurallyClosed: false, WriterAuthorized: false, PromotionAuthorized: false)
        ];
        string[] combinedBlockers =
        [
            "No safe music transaction exists until row-35 long-play handling avoids the adjacent PETEXA table.",
            "No safe exit transaction exists until ID65 has a non-retail destination/arrival contract and an authored linked T96/T97 pair.",
            "Totals require a closed reward census and explicit zero-egg policy.",
            "Save ownership requires deserialize call closure, <=256 mutable retirement IDs, and physical memory-card gates.",
            "Death/respawn is structurally compatible but remains runtime-only on the remote pad."
        ];

        return new UnusedLevel65ProgressionOwnershipContract(
            ProfileId, LockedBaseImageSha256, ExecutableSha256,
            remote.ProfileId, remote.OutputDataSha256, RemoteBlankOutputObjectTableSha256,
            ExeRegion(executable, layout, 0, executable.Length, "relocated SCUS_942.28",
                UnusedLevel65ProgressionOwnershipState.IndependentSlot, expectedSha256: ExecutableSha256) with
                { RuntimeAddress = null },
            WadRegion("locked row-80 data", DataWadOffset, lockedData, layout,
                UnusedLevel65ProgressionOwnershipState.IndependentSlot, true),
            WadRegion("remote in-memory row-80 data", DataWadOffset, remote.OutputData, layout,
                UnusedLevel65ProgressionOwnershipState.IndependentSlot, false),
            WadRegion("Town Square 107-row object table", TownSquareObjectTableWadOffset, townSquareObjects, layout,
                UnusedLevel65ProgressionOwnershipState.ByteCloneAlias, true),
            WadRegion("remote ID65 107-row object table", Id65ObjectTableWadOffset, remoteObjects, layout,
                UnusedLevel65ProgressionOwnershipState.IndependentSlotWithInheritedValue, false),
            music, totals, exit, death, save, aliases, transactions, combinedBlockers,
            LockedImagePreserved: true,
            RemotePlanIsInMemoryOnly: true,
            ExecutableExcludedFromRemotePlan: true,
            StaticReadbackOnly: true,
            WritesBin: false,
            WritesCue: false,
            StandaloneCombinedWriterAuthorized: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
    }

    private static UnusedLevel65ProgressionAlias Alias(
        string domain,
        string storage,
        string value,
        int[] aliases,
        IReadOnlyDictionary<int, LevelDefinition> byIndex,
        string evidence) =>
        new(domain, storage, value,
            aliases.Select(index => byIndex[index].LevelId).ToArray(),
            aliases.Select(index => byIndex[index].DisplayName).ToArray(),
            SharesPhysicalStorage: false,
            SharesOnlyValue: aliases.Length > 0,
            evidence);

    private static int[] FindRetailValueAliases(
        byte[] executable,
        int tableOffset,
        int rows,
        int width,
        int target,
        IReadOnlyDictionary<int, LevelDefinition> byIndex)
    {
        List<int> result = [];
        for (int index = 0; index < rows; index++)
        {
            int value = width switch
            {
                1 => executable[tableOffset + index],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(tableOffset + index * 2, 2)),
                4 => ReadInt32(executable, tableOffset + index * 4),
                _ => throw new InvalidOperationException("Unsupported alias width.")
            };
            if (value == target && byIndex.ContainsKey(index))
                result.Add(index);
        }
        return result.ToArray();
    }

    private static bool VerifyTownSquareReturnHomeLink(string root)
    {
        string path = Path.Combine(root, "townsquare-behavior-links.json");
        if (!File.Exists(path))
            return false;
        RequireHash(HashFile(path), "b1d824d2846188983029b61fc1303eb76336c8bc5926526359987b4672bcf93f",
            "Town Square behavior-link metadata");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("linkGroups").EnumerateArray().Any(group =>
            group.GetProperty("key").GetString() == "townsquare:control-role-proof:t97" &&
            group.GetProperty("linkedMove").GetBoolean() &&
            group.GetProperty("trueIndexes").EnumerateArray().Select(value => value.GetInt32())
                .SequenceEqual(new[] { 97, 96 }));
    }

    private static UnusedLevel65ProgressionCallEdge[] FindDirectJalCallers(
        byte[] executable,
        uint target,
        string name)
    {
        List<UnusedLevel65ProgressionCallEdge> edges = [];
        for (int offset = 0x800; offset + 4 <= executable.Length; offset += 4)
        {
            uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(offset, 4));
            if (instruction >> 26 != 3)
                continue;
            uint pc = ExecutableRuntimeBias + (uint)offset;
            uint resolved = ((pc + 4) & 0xF0000000) | ((instruction & 0x03FFFFFF) << 2);
            if (resolved == target)
            {
                edges.Add(new(
                    name,
                    offset,
                    pc,
                    target,
                    Convert.ToHexString(executable.AsSpan(offset, 4)),
                    DirectJal: true));
            }
        }
        return edges.ToArray();
    }

    private static UnusedLevel65ProgressionByteRegion ExeRegion(
        byte[] executable,
        DiscLayout layout,
        int offset,
        int length,
        string name,
        UnusedLevel65ProgressionOwnershipState state,
        string? expectedHex = null,
        string? expectedSha256 = null)
    {
        byte[] bytes = executable.AsSpan(offset, length).ToArray();
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        if (expectedSha256 != null)
            RequireHash(Hash(bytes), expectedSha256, name);
        return new(
            name,
            "SCUS_942.28",
            offset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, offset),
            ExecutableRuntimeBias + (uint)offset,
            length,
            Convert.ToHexString(bytes),
            Hash(bytes),
            state);
    }

    private static UnusedLevel65ProgressionByteRegion WadRegion(
        string name,
        long wadOffset,
        byte[] bytes,
        DiscLayout layout,
        UnusedLevel65ProgressionOwnershipState state,
        bool hasPhysicalImage) =>
        new(
            name,
            hasPhysicalImage ? "WAD.WAD" : "in-memory WAD.WAD row 80",
            wadOffset,
            hasPhysicalImage ? DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset) : -1,
            null,
            bytes.Length,
            Convert.ToHexString(bytes),
            Hash(bytes),
            state);

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedLength)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image, layout, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedLength)
            throw new InvalidDataException($"{name} mapping changed.");
        return record;
    }

    private static int LevelIndex(LevelDefinition level) =>
        ((level.LevelId / 10) - 1) * 6 + (level.LevelId % 10);

    private static byte[] SliceObject(byte[] table, int trueIndex) =>
        table.AsSpan(trueIndex * ObjectRecordLength, ObjectRecordLength).ToArray();

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));

    private static void RequireHex(byte[] bytes, string expected, string name)
    {
        if (!bytes.SequenceEqual(Convert.FromHexString(expected)))
            throw new InvalidDataException($"The exact {name} bytes changed.");
    }

    private static void RequireHash(string actual, string expected, string name)
    {
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {name} SHA-256 was {actual}, expected {expected}.");
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
