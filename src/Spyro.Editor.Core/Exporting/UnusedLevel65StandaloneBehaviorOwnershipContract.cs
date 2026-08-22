using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal enum UnusedLevel65BehaviorOwnershipState
{
    IndependentStorageInheritedValue,
    IndependentReservedValue,
    MissingIndexedStructure,
    MissingLinkedDestination,
    EditorFailClosed,
    RuntimeProofRequired
}

internal sealed record UnusedLevel65BehaviorByteInvariant(
    string Name,
    string Container,
    long LogicalOffset,
    long ImageOffset,
    int ByteLength,
    string Hex,
    string Sha256,
    UnusedLevel65BehaviorOwnershipState OwnershipState);

internal sealed record UnusedLevel65SpawnOwnership(
    UnusedLevel65BehaviorByteInvariant Landing,
    UnusedLevel65BehaviorByteInvariant PlayerAnchor,
    int PlayerAnchorTrueIndex,
    int LandingRawX,
    int LandingRawY,
    int LandingRawZ,
    int LandingYawByte,
    int PlayerRawX,
    int PlayerRawY,
    int PlayerRawZ,
    bool LandingAndPlayerShareXy,
    int PlayerZAboveLanding,
    bool GenericFlyInWriterCouplesPlayerAnchor,
    bool DeathRespawnRuntimeVerified,
    bool ResetColdBootRestorationRuntimeVerified);

internal sealed record UnusedLevel65BootstrapOwnership(
    UnusedLevel65BehaviorByteInvariant WarpDispatchPointer,
    uint WarpDispatchRuntimeAddress,
    UnusedLevel65BehaviorByteInvariant WarpTableUpperBound,
    int WarpTableUpperBoundImmediate,
    UnusedLevel65BehaviorByteInvariant HiddenWarpBootstrapStores,
    UnusedLevel65BehaviorByteInvariant NonFlightLevelIdentity,
    bool ExistingBootstrapAcceptsId65,
    bool ExistingBootstrapIsStandaloneBehaviorOwnership);

internal sealed record UnusedLevel65MusicOwnership(
    UnusedLevel65BehaviorByteInvariant MappingTable,
    UnusedLevel65BehaviorByteInvariant InitialSlot,
    int ContinuousLevelIndex,
    int CurrentTrackId,
    int TownSquareTrackId,
    int LateAlternateRowCount,
    UnusedLevel65BehaviorByteInvariant LateAlternateTable,
    bool HasLateAlternateRow,
    bool GenericEditorAcceptsInitialAndLongPlayOwnership,
    bool ExtendedSessionRuntimeVerified);

internal sealed record UnusedLevel65TotalsOwnership(
    UnusedLevel65BehaviorByteInvariant DragonTable,
    UnusedLevel65BehaviorByteInvariant DragonSlot,
    int DragonTarget,
    UnusedLevel65BehaviorByteInvariant TreasureTable,
    UnusedLevel65BehaviorByteInvariant TreasureSlot,
    int TreasureTarget,
    UnusedLevel65BehaviorByteInvariant EggTable,
    int EggTableRowCount,
    bool HasEggSlot,
    long LegacyRetailTreasureTableImageOffset,
    long LegacyRetailTreasureSlot35ImageOffset,
    long RelocatedExecutableTreasureSlot35ImageOffset,
    bool GenericTreasureWriterTargetsRelocatedExecutable,
    bool InitialProfileMustForbidEggs,
    bool AuthoredTotalsRuntimeVerified);

internal sealed record UnusedLevel65ExitOwnership(
    UnusedLevel65BehaviorByteInvariant PortalIdGuard,
    int CurrentPortalUpperExclusive,
    int RequiredPortalUpperExclusive,
    UnusedLevel65BehaviorByteInvariant ReturnHomeVisibleRow,
    UnusedLevel65BehaviorByteInvariant ReturnHomeHelperRow,
    bool ReturnHomeBehaviorLinkLoadedForLabKey,
    long LockedBasePortalTableWadOffset,
    long Id65PortalTableWadOffset,
    UnusedLevel65BehaviorByteInvariant Id65PortalTable,
    int Id65PortalCount,
    long GnastyWorldPortalTableWadOffset,
    IReadOnlyList<int> GnastyWorldDestinationLevelIds,
    IReadOnlyList<int> CheckedInGnastyWorldDestinationLevelIds,
    bool GnastyWorldHasDestination65,
    bool RetailGnastyWorldMutationAuthorized,
    bool ExitLevelRuntimeVerified,
    bool ReturnHomeRuntimeVerified);

internal sealed record UnusedLevel65SaveOwnership(
    UnusedLevel65BehaviorByteInvariant NewGameResetRoutine,
    UnusedLevel65BehaviorByteInvariant DeathLifeLossRoutine,
    uint DeathLifeLossRuntimeAddress,
    uint DeathCallSiteRuntimeAddress,
    uint LivesRuntimeAddress,
    uint DeathStateRuntimeAddress,
    bool DeathCallsFullNewGameReset,
    bool DeathClearsIndex35PerLevelState,
    bool DeathStateRetentionStaticallySupported,
    UnusedLevel65BehaviorByteInvariant ChecksumRoutine,
    UnusedLevel65BehaviorByteInvariant DeserializeRoutine,
    UnusedLevel65BehaviorByteInvariant SerializeRoutine,
    UnusedLevel65BehaviorByteInvariant CombinedSaveCode,
    int SaveStructByteLength,
    int ChecksummedByteLength,
    int ChecksumFieldOffset,
    uint ResetObjectRetirementRuntimeAddress,
    int ResetObjectRetirementByteLength,
    IReadOnlyList<UnusedLevel65SaveSchemaField> Schema,
    IReadOnlyList<uint> CurrentLevelIdRuntimeAddresses,
    uint VisitedTableRuntimeAddress,
    int ContinuousLevelIndex,
    uint VisitedSlotRuntimeAddress,
    bool SameSessionLooseGemRetirementRuntimeVerified,
    bool NoCardColdBootLooseGemRestorationRuntimeVerified,
    bool MemoryCardInsertionRuntimeVerified,
    bool SaveReloadRuntimeVerified,
    bool DeathRespawnRuntimeVerified,
    bool Index35SchemaRoundTripStaticallyVerified,
    bool Index35EggOwnershipPresent,
    bool IndependentSerializedSlotOwnershipProven);

internal sealed record UnusedLevel65SaveSchemaField(
    string Name,
    int RowCount,
    int SaveOffset,
    int ElementByteLength,
    uint RuntimeBaseAddress,
    int RuntimeStride,
    int? Index35SaveOffset,
    uint? Index35RuntimeAddress,
    bool HasIndex35Storage,
    string OwnershipNote);

internal sealed record UnusedLevel65BehaviorEditorBoundary(
    bool LabCatalogIsMemoryOnly,
    bool LabObjectMutationAvailable,
    bool LabPortalRoutingAvailable,
    bool LabSaveOwnershipAvailable,
    bool LabMusicMutationAvailable,
    bool LabNormalCreateBinAvailable,
    bool RetailAllSavedEditsSkipsLab,
    bool LabMobyMetadataUsesTownSquareBehaviorLinks,
    bool LabLoadsFlyInLandingEditorControl,
    IReadOnlyList<string> ExactGenericBlockers);

internal sealed record UnusedLevel65BehaviorOwnershipStage(
    int Order,
    string Id,
    string CurrentState,
    IReadOnlyList<string> ExactDependencies,
    IReadOnlyList<string> RequiredRuntimeGates,
    bool MutationWriterAuthorized,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65StandaloneBehaviorOwnershipContract(
    string ProfileId,
    string LockedBaseImageSha256,
    string FoundationImageSha256,
    string ExecutableSha256,
    UnusedLevel65BehaviorByteInvariant DirectoryRow79And80,
    UnusedLevel65BehaviorByteInvariant Overlay,
    UnusedLevel65BehaviorByteInvariant LockedBaseData,
    UnusedLevel65BehaviorByteInvariant FoundationData,
    UnusedLevel65BehaviorByteInvariant ObjectTable,
    UnusedLevel65BehaviorByteInvariant DisplayNameSlot,
    UnusedLevel65BootstrapOwnership Bootstrap,
    UnusedLevel65SpawnOwnership Spawn,
    UnusedLevel65MusicOwnership Music,
    UnusedLevel65TotalsOwnership Totals,
    UnusedLevel65ExitOwnership Exit,
    UnusedLevel65SaveOwnership Save,
    UnusedLevel65BehaviorEditorBoundary EditorBoundary,
    IReadOnlyList<UnusedLevel65BehaviorOwnershipStage> PromotionOrder,
    bool LockedBasePreserved,
    bool FoundationPreserved,
    bool StaticReadbackOnly,
    bool WritesBin,
    bool WritesCue,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

/// <summary>
/// Read-only ownership ledger for behavior that a truly independent ID65 level
/// still needs. It pins the exact passed display-name base and the current
/// static-only foundation candidate, but deliberately cannot build or publish a
/// candidate. A matching byte is evidence of the current inherited/empty state,
/// never evidence that runtime ownership has been promoted.
/// </summary>
internal static class UnusedLevel65StandaloneBehaviorOwnershipInspector
{
    public const string ProfileId =
        "unused-level-65-standalone-behavior-ownership-static-contract-clean-usa-v1";
    public const string LockedBaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string FoundationImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";

    private const int WadLba = 37;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const long DirectoryRow79And80WadOffset = 79 * 8L;
    private const long OverlayWadOffset = 0x6927000;
    private const int OverlayByteLength = 0xF800;
    private const long DataWadOffset = 0x6936800;
    private const int DataByteLength = 0x2E2000;
    private const long ObjectTableWadOffset = 0x6B06970;
    private const int ObjectRecordCount = 107;
    private const int ObjectRecordByteLength = 0x58;
    private const long LandingWadOffset = 0x6B06800;
    private const long BasePortalTableWadOffset = 0x6AA8D8C;
    private const long FoundationPortalTableWadOffset = BasePortalTableWadOffset + 0x30;

    private const long PortalGuardFileOffset = 0x47910;
    private const long WarpDispatchPointerFileOffset = 0x1CA8;
    private const long WarpTableUpperBoundFileOffset = 0x1DF44;
    private const long HiddenWarpBootstrapStoresFileOffset = 0x1E034;
    private const long NonFlightLevelIdentityFileOffset = 0x40C0;
    private const long MusicMappingFileOffset = 0x5F79C;
    private const int MusicMappingByteLength = 48 * sizeof(int);
    private const long LateAlternateFileOffset = 0x5F85C;
    private const int LateAlternateRowCount = 35;
    private const int LateAlternateByteLength = LateAlternateRowCount * 3 * sizeof(int);
    private const long DragonTotalsFileOffset = 0x5FC14;
    private const int DragonTotalsByteLength = 36;
    private const long TreasureTotalsFileOffset = 0x5FC38;
    private const int TreasureTotalsByteLength = 36 * sizeof(ushort);
    private const long EggTotalsFileOffset = 0x5FC80;
    private const int EggTotalsRowCount = 20;
    private const long DisplayNameSlotFileOffset = 0x6007C;
    private const int ContinuousLevelIndex = 35;
    private const long LegacyRetailTreasureTableImageOffset = 0x7945FB0;
    private const long NewGameResetRoutineFileOffset = 0x2E04;
    private const int NewGameResetRoutineByteLength = 0x178;
    private const long DeathLifeLossRoutineFileOffset = 0x1D05C;
    private const int DeathLifeLossRoutineByteLength = 0x48;
    private const long SaveChecksumRoutineFileOffset = 0x49D6C;
    private const int SaveChecksumRoutineByteLength = 0x28;
    private const long SaveDeserializeRoutineFileOffset = 0x49D94;
    private const int SaveDeserializeRoutineByteLength = 0x2D0;
    private const long SaveSerializeRoutineFileOffset = 0x4A064;
    private const int SaveSerializeRoutineByteLength = 0x1E4;
    private const int SaveCodeByteLength = 0x4DC;

    public static async Task<UnusedLevel65StandaloneBehaviorOwnershipContract> InspectAsync(
        string workspaceRoot,
        string lockedBaseImagePath,
        string foundationImagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string lockedBasePath = RequireFile(lockedBaseImagePath, "exact ID65 locked base");
        string foundationPath = RequireFile(foundationImagePath, "exact ID65 foundation image");
        await RequireFileHashAsync(lockedBasePath, LockedBaseImageSha256, cancellationToken);
        await RequireFileHashAsync(foundationPath, FoundationImageSha256, cancellationToken);

        DiscLayout lockedLayout = DiscImage.DetectLayout(lockedBasePath);
        DiscLayout foundationLayout = DiscImage.DetectLayout(foundationPath);
        RequireMode2(lockedLayout, "locked base");
        RequireMode2(foundationLayout, "foundation");

        using FileStream lockedBase = File.OpenRead(lockedBasePath);
        using FileStream foundation = File.OpenRead(foundationPath);
        DiscFileRecord lockedWad = RequireFileRecord(lockedBase, lockedLayout, "WAD.WAD", WadLba, 0x6C18800);
        DiscFileRecord foundationWad = RequireFileRecord(foundation, foundationLayout, "WAD.WAD", WadLba, 0x6C18800);
        _ = lockedWad;
        _ = foundationWad;
        DiscFileRecord lockedExecutable = RequireFileRecord(
            lockedBase,
            lockedLayout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);
        DiscFileRecord foundationExecutable = RequireFileRecord(
            foundation,
            foundationLayout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);

        byte[] lockedExecutableBytes = DiscImage.ReadFileBytes(
            lockedBase,
            lockedLayout,
            lockedExecutable.Lba,
            0,
            lockedExecutable.Size);
        byte[] foundationExecutableBytes = DiscImage.ReadFileBytes(
            foundation,
            foundationLayout,
            foundationExecutable.Lba,
            0,
            foundationExecutable.Size);
        RequireHash(lockedExecutableBytes, ExecutableSha256, "locked-base executable");
        RequireHash(foundationExecutableBytes, ExecutableSha256, "foundation executable");
        if (!lockedExecutableBytes.SequenceEqual(foundationExecutableBytes))
            throw new InvalidDataException("The foundation changed the exact behavior-owning executable.");

        byte[] directoryRow = ReadMatchingWadBytes(
            lockedBase,
            lockedLayout,
            foundation,
            foundationLayout,
            DirectoryRow79And80WadOffset,
            16,
            "ID65 directory row 79/80");
        RequireHex(directoryRow, "0070920600F800000068930600202E00", "ID65 directory row 79/80");

        byte[] lockedOverlay = ReadWadBytes(lockedBase, lockedLayout, OverlayWadOffset, OverlayByteLength);
        byte[] foundationOverlay = ReadWadBytes(foundation, foundationLayout, OverlayWadOffset, OverlayByteLength);
        RequireHash(lockedOverlay, "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5", "locked ID65 overlay");
        if (!lockedOverlay.SequenceEqual(foundationOverlay))
            throw new InvalidDataException("The foundation changed the ID65 overlay.");

        byte[] lockedData = ReadWadBytes(lockedBase, lockedLayout, DataWadOffset, DataByteLength);
        byte[] foundationData = ReadWadBytes(foundation, foundationLayout, DataWadOffset, DataByteLength);
        RequireHash(lockedData, "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0", "locked ID65 data");
        RequireHash(foundationData, "eb5ca8459300392de12246e57770a57447f7bd64ccf3e978137d2362d7694160", "foundation ID65 data");

        byte[] landing = ReadMatchingWadBytes(
            lockedBase,
            lockedLayout,
            foundation,
            foundationLayout,
            LandingWadOffset,
            0x10,
            "fly-in landing");
        RequireHex(landing, "29E901009A8801006621000000004000", "fly-in landing");

        byte[] objectTable = ReadMatchingWadBytes(
            lockedBase,
            lockedLayout,
            foundation,
            foundationLayout,
            ObjectTableWadOffset,
            ObjectRecordCount * ObjectRecordByteLength,
            "ID65 object table");
        RequireHash(objectTable, "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d", "ID65 object table");
        byte[] playerAnchor = SliceObject(objectTable, 92);
        byte[] returnHomeVisible = SliceObject(objectTable, 96);
        byte[] returnHomeHelper = SliceObject(objectTable, 97);
        RequireHash(playerAnchor, "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2", "T92 player anchor");
        RequireHash(returnHomeVisible, "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4", "T96 Return Home row");
        RequireHash(returnHomeHelper, "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc", "T97 Return Home helper row");

        byte[] basePortalTable = ReadWadBytes(lockedBase, lockedLayout, BasePortalTableWadOffset, sizeof(int));
        byte[] foundationPortalTable = ReadWadBytes(foundation, foundationLayout, FoundationPortalTableWadOffset, sizeof(int));
        RequireHex(basePortalTable, "00000000", "locked-base zero portal table");
        RequireHex(foundationPortalTable, "00000000", "foundation zero portal table");

        UnusedLevel65BehaviorByteInvariant warpDispatchPointer = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            WarpDispatchPointerFileOffset,
            4,
            "ID65 hidden-warp dispatch pointer",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "98A80580");
        UnusedLevel65BehaviorByteInvariant warpTableUpperBound = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            WarpTableUpperBoundFileOffset,
            4,
            "hidden-warp table upper bound",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "3800422C");
        UnusedLevel65BehaviorByteInvariant hiddenWarpBootstrapStores = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            HiddenWarpBootstrapStoresFileOffset,
            8,
            "ID65 hidden-warp bootstrap stores",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "1C0684AF880680AF");
        UnusedLevel65BehaviorByteInvariant nonFlightLevelIdentity = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            NonFlightLevelIdentityFileOffset,
            4,
            "ID65 non-flight level identity",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "005E023C");

        UnusedLevel65BehaviorByteInvariant portalGuard = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            PortalGuardFileOffset,
            4,
            "normal portal level-ID guard",
            UnusedLevel65BehaviorOwnershipState.MissingLinkedDestination,
            "41004228");
        UnusedLevel65BehaviorByteInvariant mappingTable = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            MusicMappingFileOffset,
            MusicMappingByteLength,
            "initial music mapping table",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            expectedSha256: "a4be62ad38b6c793831d9cdcc3d92d24278e18df4fab19ddc678ed29e191e5f5");
        UnusedLevel65BehaviorByteInvariant initialMusicSlot = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            MusicMappingFileOffset + ContinuousLevelIndex * sizeof(int),
            sizeof(int),
            "ID65 initial music slot",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "13000000");
        UnusedLevel65BehaviorByteInvariant lateAlternateTable = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            LateAlternateFileOffset,
            LateAlternateByteLength,
            "late alternate music table",
            UnusedLevel65BehaviorOwnershipState.MissingIndexedStructure,
            expectedSha256: "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c");

        UnusedLevel65BehaviorByteInvariant dragonTable = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            DragonTotalsFileOffset,
            DragonTotalsByteLength,
            "dragon totals table",
            UnusedLevel65BehaviorOwnershipState.IndependentReservedValue,
            expectedSha256: "369c57dff04b1b608adb95b2b42688a0380f61ccf5445446e423d1badbc03699");
        UnusedLevel65BehaviorByteInvariant dragonSlot = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            DragonTotalsFileOffset + ContinuousLevelIndex,
            1,
            "ID65 dragon total",
            UnusedLevel65BehaviorOwnershipState.IndependentReservedValue,
            "00");
        UnusedLevel65BehaviorByteInvariant treasureTable = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            TreasureTotalsFileOffset,
            TreasureTotalsByteLength,
            "treasure totals table",
            UnusedLevel65BehaviorOwnershipState.IndependentReservedValue,
            expectedSha256: "641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6");
        UnusedLevel65BehaviorByteInvariant treasureSlot = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            TreasureTotalsFileOffset + ContinuousLevelIndex * sizeof(ushort),
            sizeof(ushort),
            "ID65 treasure total",
            UnusedLevel65BehaviorOwnershipState.IndependentReservedValue,
            "0000");
        UnusedLevel65BehaviorByteInvariant eggTable = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            EggTotalsFileOffset,
            EggTotalsRowCount,
            "bounded egg totals table",
            UnusedLevel65BehaviorOwnershipState.MissingIndexedStructure,
            expectedSha256: "6d4de8ed9543f9dc758c3824ac0e4bebcf75f681de15770355b7b8820e6fd41b");
        byte[] eggTableAndFollowing = foundationExecutableBytes
            .AsSpan((int)EggTotalsFileOffset, 36)
            .ToArray();
        RequireHash(eggTableAndFollowing, "34674fe7999120ea5042bb7b73ed08c30e81fdd68a2e78673ee442d22916ecd7", "egg-table boundary");

        UnusedLevel65BehaviorByteInvariant displayName = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            DisplayNameSlotFileOffset,
            sizeof(uint),
            "ID65 display-name pointer",
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue,
            "E4010180");

        LevelCatalog retailCatalog = LevelCatalog.Load(workspaceRoot);
        if (retailCatalog.Levels.Count != 35)
            throw new InvalidDataException("The behavior ownership contract requires the exact 35-level retail catalog.");
        LevelDefinition gnastyWorld = retailCatalog.FindByKey("gnastysworld")
            ?? throw new InvalidDataException("The retail Gnasty's World definition is missing.");
        PortalSourceLevelData gnastyPortals = PortalSourceDataLocator.Locate(foundationPath, gnastyWorld);
        int[] gnastyDestinationIds = gnastyPortals.Portals.Select(portal => portal.DestinationLevelId).ToArray();
        if (gnastyPortals.PortalTableWadOffset != 0x52F280C ||
            !gnastyDestinationIds.SequenceEqual(new[] { 62, 63, 64, 61 }))
        {
            throw new InvalidDataException("Gnasty's World no longer has the exact four retail portal destinations 62/63/64/61.");
        }
        int[] checkedInDestinationIds = HomeworldPortalControlCatalog.ForLevel("gnastysworld")
            .Select(portal => portal.DestinationLevelId)
            .ToArray();
        if (!checkedInDestinationIds.SequenceEqual(new[] { 61, 62, 63, 64 }))
            throw new InvalidDataException("The checked-in Gnasty's World portal controls changed.");

        int landingX = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(0, 4));
        int landingY = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(4, 4));
        int landingZ = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(8, 4));
        int playerX = BinaryPrimitives.ReadInt32LittleEndian(playerAnchor.AsSpan(0x0C, 4));
        int playerY = BinaryPrimitives.ReadInt32LittleEndian(playerAnchor.AsSpan(0x10, 4));
        int playerZ = BinaryPrimitives.ReadInt32LittleEndian(playerAnchor.AsSpan(0x14, 4));
        if (landingX != 125_225 || landingY != 100_506 || landingZ != 8_550 ||
            landing[0x0E] != 64 || playerX != landingX || playerY != landingY || playerZ - landingZ != 154)
        {
            throw new InvalidDataException("The inherited ID65 landing/T92 spawn relationship changed.");
        }

        int currentTrack = BinaryPrimitives.ReadInt32LittleEndian(initialMusicSlotBytes(foundationExecutableBytes));
        int expectedNativeTrack = MusicTrackCatalog.GetNativeTrackId(UnusedLevel65BlankLevelLabProfileRegistry.Definition);
        if (currentTrack != 19 || expectedNativeTrack != currentTrack)
            throw new InvalidDataException("The ID65 initial music ownership changed.");

        long relocatedTreasureSlotImageOffset = DiscImage.ConvertFileOffsetToImageOffset(
            foundationLayout,
            foundationExecutable.Lba,
            TreasureTotalsFileOffset + ContinuousLevelIndex * sizeof(ushort));
        long legacySlot35ImageOffset = LegacyRetailTreasureTableImageOffset + ContinuousLevelIndex * sizeof(ushort);
        if (relocatedTreasureSlotImageOffset != 0x7CA7586 || legacySlot35ImageOffset != 0x7945FF6)
            throw new InvalidDataException("The pinned retail/relocated treasure physical offsets changed.");

        UnusedLevel65BehaviorByteInvariant newGameResetRoutine = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            NewGameResetRoutineFileOffset,
            NewGameResetRoutineByteLength,
            "new-game/no-save reset routine",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "57e807bf638fe6394f46cc7c673f5fc27e8910fc8fce0acee60761c51548538f");
        UnusedLevel65BehaviorByteInvariant deathLifeLossRoutine = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            DeathLifeLossRoutineFileOffset,
            DeathLifeLossRoutineByteLength,
            "death/life-loss state routine",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad");
        UnusedLevel65BehaviorByteInvariant checksumRoutine = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            SaveChecksumRoutineFileOffset,
            SaveChecksumRoutineByteLength,
            "save checksum routine",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db");
        UnusedLevel65BehaviorByteInvariant deserializeRoutine = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            SaveDeserializeRoutineFileOffset,
            SaveDeserializeRoutineByteLength,
            "save deserialize routine",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6");
        UnusedLevel65BehaviorByteInvariant serializeRoutine = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            SaveSerializeRoutineFileOffset,
            SaveSerializeRoutineByteLength,
            "save serialize routine",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056");
        UnusedLevel65BehaviorByteInvariant combinedSaveCode = ExeInvariant(
            foundationExecutableBytes,
            foundationLayout,
            foundationExecutable.Lba,
            SaveChecksumRoutineFileOffset,
            SaveCodeByteLength,
            "checksum + deserialize + serialize code",
            UnusedLevel65BehaviorOwnershipState.RuntimeProofRequired,
            expectedSha256: "34acb0e3b5a518dc4b9f0147a60a4018288ae16fda5a3fa56db6cfb89aac999a");

        UnusedLevel65SpawnOwnership spawn = new(
            WadInvariant(
                "fly-in landing",
                LandingWadOffset,
                landing,
                foundationLayout,
                UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue),
            WadInvariant(
                "T92 player anchor",
                ObjectTableWadOffset + 92L * ObjectRecordByteLength,
                playerAnchor,
                foundationLayout,
                UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue),
            92,
            landingX,
            landingY,
            landingZ,
            landing[0x0E],
            playerX,
            playerY,
            playerZ,
            LandingAndPlayerShareXy: true,
            PlayerZAboveLanding: 154,
            GenericFlyInWriterCouplesPlayerAnchor: false,
            DeathRespawnRuntimeVerified: false,
            ResetColdBootRestorationRuntimeVerified: true);

        UnusedLevel65BootstrapOwnership bootstrap = new(
            warpDispatchPointer,
            WarpDispatchRuntimeAddress: 0x8005A898,
            warpTableUpperBound,
            WarpTableUpperBoundImmediate: 0x38,
            hiddenWarpBootstrapStores,
            nonFlightLevelIdentity,
            ExistingBootstrapAcceptsId65: true,
            ExistingBootstrapIsStandaloneBehaviorOwnership: false);

        UnusedLevel65MusicOwnership music = new(
            mappingTable,
            initialMusicSlot,
            ContinuousLevelIndex,
            currentTrack,
            26,
            LateAlternateRowCount,
            lateAlternateTable,
            HasLateAlternateRow: false,
            GenericEditorAcceptsInitialAndLongPlayOwnership: false,
            ExtendedSessionRuntimeVerified: false);

        UnusedLevel65TotalsOwnership totals = new(
            dragonTable,
            dragonSlot,
            DragonTarget: 0,
            treasureTable,
            treasureSlot,
            TreasureTarget: 0,
            eggTable,
            EggTotalsRowCount,
            HasEggSlot: false,
            LegacyRetailTreasureTableImageOffset,
            legacySlot35ImageOffset,
            relocatedTreasureSlotImageOffset,
            GenericTreasureWriterTargetsRelocatedExecutable: false,
            InitialProfileMustForbidEggs: true,
            AuthoredTotalsRuntimeVerified: false);

        UnusedLevel65ExitOwnership exit = new(
            portalGuard,
            CurrentPortalUpperExclusive: 65,
            RequiredPortalUpperExclusive: 66,
            WadInvariant(
                "T96 Return Home",
                ObjectTableWadOffset + 96L * ObjectRecordByteLength,
                returnHomeVisible,
                foundationLayout,
                UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue),
            WadInvariant(
                "T97 Return Home helper",
                ObjectTableWadOffset + 97L * ObjectRecordByteLength,
                returnHomeHelper,
                foundationLayout,
                UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue),
            ReturnHomeBehaviorLinkLoadedForLabKey: false,
            BasePortalTableWadOffset,
            FoundationPortalTableWadOffset,
            WadInvariant(
                "foundation ID65 zero portal table",
                FoundationPortalTableWadOffset,
                foundationPortalTable,
                foundationLayout,
                UnusedLevel65BehaviorOwnershipState.MissingLinkedDestination),
            Id65PortalCount: 0,
            gnastyPortals.PortalTableWadOffset,
            gnastyDestinationIds,
            checkedInDestinationIds,
            GnastyWorldHasDestination65: false,
            RetailGnastyWorldMutationAuthorized: false,
            ExitLevelRuntimeVerified: false,
            ReturnHomeRuntimeVerified: false);

        IReadOnlyList<UnusedLevel65SaveSchemaField> saveSchema =
        [
            new(
                "current-level-id",
                1,
                0x000,
                1,
                0x8007596C,
                0,
                0x000,
                0x8007596C,
                HasIndex35Storage: true,
                "The serialized byte can encode runtime level ID 65; this is one global current-level field, not a per-level row."),
            new(
                "visited",
                36,
                0x040,
                1,
                0x80078E78,
                1,
                0x063,
                0x80078E9B,
                HasIndex35Storage: true,
                "Continuous index 35 owns the final byte of the exact 36-row visited array."),
            new(
                "unresolved-second-per-level-byte-table",
                36,
                0x064,
                1,
                0x8007A6A8,
                1,
                0x087,
                0x8007A6CB,
                HasIndex35Storage: true,
                "The serializer round-trips this exact 36-row table, but its gameplay meaning is not yet identified."),
            new(
                "dragons",
                36,
                0x088,
                1,
                0x800772D8,
                4,
                0x0AB,
                0x80077364,
                HasIndex35Storage: true,
                "Only the low byte of each four-byte runtime dragon counter is serialized."),
            new(
                "treasure",
                36,
                0x0AC,
                2,
                0x80077420,
                4,
                0x0F2,
                0x800774AC,
                HasIndex35Storage: true,
                "One UInt16 per level is serialized from the low half of each four-byte runtime treasure counter."),
            new(
                "eggs",
                18,
                0x0F4,
                1,
                0,
                0,
                null,
                null,
                HasIndex35Storage: false,
                "The save schema serializes only 18 egg rows; ID65 has no egg persistence slot."),
            new(
                "global-progression-bytes",
                6,
                0x106,
                1,
                0,
                1,
                null,
                null,
                HasIndex35Storage: false,
                "Six global bytes follow the bounded egg rows and are not ID65-owned per-level fields."),
            new(
                "object-retirement-bitmaps",
                36,
                0x10C,
                0x20,
                0x80077908,
                0x20,
                0x56C,
                0x80077D68,
                HasIndex35Storage: true,
                "ID65 owns save+0x56C..0x58B and RAM 0x80077D68..0x80077D87 for 256 object-retirement bits."
            )
        ];
        if (saveSchema.Where(field => field.HasIndex35Storage).Any(field => field.Index35SaveOffset == null) ||
            saveSchema.Single(field => field.Name == "object-retirement-bitmaps").Index35SaveOffset != 0x56C)
        {
            throw new InvalidDataException("The static ID65 save schema is inconsistent.");
        }

        UnusedLevel65SaveOwnership save = new(
            newGameResetRoutine,
            deathLifeLossRoutine,
            DeathLifeLossRuntimeAddress: 0x8002C85C,
            DeathCallSiteRuntimeAddress: 0x8004A4D8,
            LivesRuntimeAddress: 0x8007582C,
            DeathStateRuntimeAddress: 0x800757D8,
            DeathCallsFullNewGameReset: false,
            DeathClearsIndex35PerLevelState: false,
            DeathStateRetentionStaticallySupported: true,
            checksumRoutine,
            deserializeRoutine,
            serializeRoutine,
            combinedSaveCode,
            SaveStructByteLength: 0x600,
            ChecksummedByteLength: 0x58C,
            ChecksumFieldOffset: 0x58C,
            ResetObjectRetirementRuntimeAddress: 0x80077908,
            ResetObjectRetirementByteLength: 0x480,
            saveSchema,
            CurrentLevelIdRuntimeAddresses: [0x800758B4, 0x8007596C],
            VisitedTableRuntimeAddress: 0x80078E78,
            ContinuousLevelIndex,
            VisitedSlotRuntimeAddress: 0x80078E78 + ContinuousLevelIndex,
            SameSessionLooseGemRetirementRuntimeVerified: true,
            NoCardColdBootLooseGemRestorationRuntimeVerified: true,
            MemoryCardInsertionRuntimeVerified: false,
            SaveReloadRuntimeVerified: false,
            DeathRespawnRuntimeVerified: false,
            Index35SchemaRoundTripStaticallyVerified: true,
            Index35EggOwnershipPresent: false,
            IndependentSerializedSlotOwnershipProven: false);

        UnusedLevel65BehaviorEditorBoundary editorBoundary = new(
            LabCatalogIsMemoryOnly: true,
            LabObjectMutationAvailable: false,
            LabPortalRoutingAvailable: false,
            LabSaveOwnershipAvailable: false,
            LabMusicMutationAvailable: false,
            LabNormalCreateBinAvailable: false,
            RetailAllSavedEditsSkipsLab: true,
            LabMobyMetadataUsesTownSquareBehaviorLinks: false,
            LabLoadsFlyInLandingEditorControl: false,
            ExactGenericBlockers:
            [
                "LevelMusicPatchExporter rejects continuous index 35 because the late-alternate table has only 35 rows.",
                "MobySourcePatchExporter treasure totals use retail physical image base 0x7945FB0; relocated SCUS slot 35 is 0x7CA7586.",
                "LoadId65BlankLabMobyData applies metadata key unusedlevel65blank, so townsquare-behavior-links.json T96/T97 is not loaded.",
                "The ID65-specific Moby loader returns before TryAddFlyInLandingEditorControl and object mutation remains inspection-only.",
                "FindEditedLevelExportTargets skips the Lab and normal Create BIN is fail-closed while ID65 is active or authored artifacts exist."
            ]);

        UnusedLevel65BehaviorByteInvariant directoryInvariant = WadInvariant(
            "ID65 rows 79/80",
            DirectoryRow79And80WadOffset,
            directoryRow,
            foundationLayout,
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue);
        UnusedLevel65BehaviorByteInvariant overlayInvariant = WadInvariant(
            "ID65 overlay",
            OverlayWadOffset,
            lockedOverlay,
            foundationLayout,
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue);
        UnusedLevel65BehaviorByteInvariant lockedDataInvariant = WadInvariant(
            "locked ID65 data",
            DataWadOffset,
            lockedData,
            lockedLayout,
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue);
        UnusedLevel65BehaviorByteInvariant foundationDataInvariant = WadInvariant(
            "foundation ID65 data",
            DataWadOffset,
            foundationData,
            foundationLayout,
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue);
        UnusedLevel65BehaviorByteInvariant objectTableInvariant = WadInvariant(
            "ID65 107-row object table",
            ObjectTableWadOffset,
            objectTable,
            foundationLayout,
            UnusedLevel65BehaviorOwnershipState.IndependentStorageInheritedValue);

        IReadOnlyList<UnusedLevel65BehaviorOwnershipStage> promotionOrder =
        [
            Stage(
                1,
                "spawn-death-reset",
                "Exact independent landing/T92 bytes exist, but both are Town Square-derived and the generic landing writer does not couple T92.",
                ["One atomic authored landing + T92 transaction", "No retail WAD mutation", "Exact reset preimages/readback"],
                ["cold entry", "death and respawn", "reset/cold boot", "camera and collision at authored spawn"]),
            Stage(
                2,
                "authored-totals",
                "Dragon and treasure slot 35 exist at zero; the egg total and save structures have no effective row 35, so the initial profile must forbid eggs.",
                ["Authored object census", "dragon+treasure slot-35 writer", "zero-egg profile until both egg structures are deliberately extended", "relocated-SCUS physical addressing"],
                ["0/0 baseline", "collect/rescue/egg once", "death/re-entry", "no duplicate rewards"]),
            Stage(
                3,
                "owned-music",
                "Initial slot 35 exists as Gnasty's Loot track 19; no late-alternate row 35 exists.",
                ["Authored initial track", "safe row-35 long-play special case or table extension", "PETEXA table adjacency preserved"],
                ["initial playback", "8-12 minute alternate boundary", "pause/death/re-entry music"]),
            Stage(
                4,
                "exit-return-home",
                "T96/T97 exist, but normal transitions reject 65 and Gnasty's World has no destination-65 route/landing.",
                ["guarded <66 transition", "explicit non-retail-mutating Gnasty arrival fallback", "T96/T97 linked ownership"],
                ["Return Home", "Pause Exit Level", "re-enter ID65", "all retail Gnasty portals unchanged"]),
            Stage(
                5,
                "save-persistence",
                "The serializer/deserializer statically round-trip index 35 for visited, dragons, treasure, and object retirement, but no egg row exists and memory-card behavior is untested.",
                ["Bounded egg persistence ownership or explicit no-egg policy", "fresh/corrupt/old-card behavior", "exit/death dependencies closed"],
                ["fresh card save/load", "leave/re-enter", "death/reload", "cold boot", "retail save compatibility"]),
            Stage(
                6,
                "editor-and-promotion",
                "Every current App path remains fail-closed for these domains.",
                ["isolated authored manifest", "atomic combined writer", "normal Create BIN rebasing", "all earlier stages runtime-passed"],
                ["full focused checklist", "retail comparison levels", "long-session soak", "release package no-game-data audit"])
        ];

        return new UnusedLevel65StandaloneBehaviorOwnershipContract(
            ProfileId,
            LockedBaseImageSha256,
            FoundationImageSha256,
            ExecutableSha256,
            directoryInvariant,
            overlayInvariant,
            lockedDataInvariant,
            foundationDataInvariant,
            objectTableInvariant,
            displayName,
            bootstrap,
            spawn,
            music,
            totals,
            exit,
            save,
            editorBoundary,
            promotionOrder,
            LockedBasePreserved: true,
            FoundationPreserved: true,
            StaticReadbackOnly: true,
            WritesBin: false,
            WritesCue: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
    }

    private static UnusedLevel65BehaviorOwnershipStage Stage(
        int order,
        string id,
        string current,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string> runtimeGates) =>
        new(order, id, current, dependencies, runtimeGates, MutationWriterAuthorized: false, PromotionAuthorized: false);

    private static ReadOnlySpan<byte> initialMusicSlotBytes(byte[] executable) =>
        executable.AsSpan((int)(MusicMappingFileOffset + ContinuousLevelIndex * sizeof(int)), sizeof(int));

    private static UnusedLevel65BehaviorByteInvariant ExeInvariant(
        byte[] executable,
        DiscLayout layout,
        int executableLba,
        long fileOffset,
        int byteLength,
        string name,
        UnusedLevel65BehaviorOwnershipState ownershipState,
        string? expectedHex = null,
        string? expectedSha256 = null)
    {
        byte[] bytes = executable.AsSpan(checked((int)fileOffset), byteLength).ToArray();
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        if (expectedSha256 != null)
            RequireHash(bytes, expectedSha256, name);
        return new(
            name,
            "SCUS_942.28",
            fileOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, executableLba, fileOffset),
            byteLength,
            Convert.ToHexString(bytes),
            Hash(bytes),
            ownershipState);
    }

    private static UnusedLevel65BehaviorByteInvariant WadInvariant(
        string name,
        long wadOffset,
        byte[] bytes,
        DiscLayout layout,
        UnusedLevel65BehaviorOwnershipState ownershipState) =>
        new(
            name,
            "WAD.WAD",
            wadOffset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset),
            bytes.Length,
            Convert.ToHexString(bytes),
            Hash(bytes),
            ownershipState);

    private static byte[] SliceObject(byte[] table, int trueIndex) =>
        table.AsSpan(trueIndex * ObjectRecordByteLength, ObjectRecordByteLength).ToArray();

    private static byte[] ReadMatchingWadBytes(
        FileStream first,
        DiscLayout firstLayout,
        FileStream second,
        DiscLayout secondLayout,
        long wadOffset,
        int byteLength,
        string label)
    {
        byte[] left = ReadWadBytes(first, firstLayout, wadOffset, byteLength);
        byte[] right = ReadWadBytes(second, secondLayout, wadOffset, byteLength);
        if (!left.SequenceEqual(right))
            throw new InvalidDataException($"The foundation changed {label}.");
        return left;
    }

    private static byte[] ReadWadBytes(
        FileStream image,
        DiscLayout layout,
        long wadOffset,
        int byteLength) =>
        DiscImage.ReadFileBytes(image, layout, WadLba, wadOffset, byteLength);

    private static DiscFileRecord RequireFileRecord(
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

    private static void RequireMode2(DiscLayout layout, string label)
    {
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException($"The {label} is not the exact MODE2/2352 layout.");
    }

    private static string RequireFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"The {label} is missing.", path);
        return Path.GetFullPath(path);
    }

    private static async Task RequireFileHashAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{Path.GetFileName(path)} SHA-256 was {actual}, expected {expected}.");
    }

    private static void RequireHex(byte[] bytes, string expected, string label)
    {
        if (!bytes.SequenceEqual(Convert.FromHexString(expected)))
            throw new InvalidDataException($"The exact {label} bytes changed.");
    }

    private static void RequireHash(byte[] bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 was {actual}, expected {expected}.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
