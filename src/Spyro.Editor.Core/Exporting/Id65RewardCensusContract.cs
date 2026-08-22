using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65RewardEncoding
{
    LooseGem,
    NativeDrop
}

internal enum Id65RewardOwnerSemantics
{
    StationaryLooseGem,
    StationaryChestCarrier,
    BullEnemy,
    TorroEnemy
}

internal sealed record Id65RewardCensusSourceSnapshot(
    string SourceImagePath,
    string SourceImageSha256,
    int SectorSize,
    int UserDataOffset,
    int WadLba,
    int WadByteLength,
    int ExecutableLba,
    ReadOnlyMemory<byte> Executable,
    ReadOnlyMemory<byte> Id65Overlay,
    ReadOnlyMemory<byte> Id65Data,
    ReadOnlyMemory<byte> RetailTownSquareOverlay,
    ReadOnlyMemory<byte> RetailTownSquareData);

internal sealed record Id65RewardCensusRow(
    int TrueIndex,
    long RowWadOffset,
    string RowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256,
    ushort ActorClass,
    byte RenderType,
    byte ValueEncodingByte,
    byte UpdateDistance,
    byte DropClass,
    Id65RewardEncoding Encoding,
    Id65RewardOwnerSemantics OwnerSemantics,
    int RewardValue,
    int RetirementByteIndex,
    byte RetirementBitMask,
    bool AuthoredOwnerRow,
    bool TransientRewardChildAddsAuthoredRow,
    bool TransientRewardChildOwnsRetirementBit,
    bool TransientRewardChildRuntimeProofVerified);

internal sealed record Id65RewardClassValueLedgerEntry(
    ushort ActorClass,
    int RewardValue,
    IReadOnlyList<int> TrueIndexes);

internal sealed record Id65RewardRetirementOwnerCandidate(
    int TrueIndex,
    ushort ActorClass,
    int RewardValue,
    int RetirementByteIndex,
    byte RetirementBitMask,
    bool RetirementAuthorized,
    string Boundary);

internal sealed record Id65RewardOwnerAggregate(
    int StationaryOwnerRowCount,
    int StationaryOwnerRewardTotal,
    int BehaviorEnemyOwnerRowCount,
    int BehaviorEnemyOwnerRewardTotal,
    int BullOwnerRowCount,
    int BullOwnerRewardTotal,
    IReadOnlyList<int> BullTrueIndexes,
    int TorroOwnerRowCount,
    int TorroOwnerRewardTotal,
    IReadOnlyList<int> TorroTrueIndexes,
    int StationaryLooseGemOwnerRowCount,
    int StationaryLooseGemRewardTotal,
    int StationaryChestCarrierRowCount,
    int StationaryChestCarrierRewardTotal,
    IReadOnlyList<ushort> StationaryChestCarrierClasses,
    bool ChestCarrierRowOwnsRewardValue,
    bool ChestCarrierRowOwnsRetirementBit,
    bool TransientChildrenCountedAsAdditionalRows,
    bool TransientChildrenOwnAdditionalRetirementBits,
    bool RuntimeDuplicateAndChildBitProofVerified);

internal sealed record Id65RewardActorPackageDependency(
    ushort ActorClass,
    int ActorRootIndex,
    int EntryRelativeOffset,
    int ByteLength,
    long WadOffset,
    string Sha256,
    string Role);

internal sealed record Id65RewardDragonBundle(
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    string PedestalRowSha256,
    string DragonRowSha256,
    string ContainerRowSha256,
    string ThreeRowBundleSha256,
    long PedestalPropertiesWadOffset,
    int PedestalPropertiesByteLength,
    string PedestalPropertiesSha256,
    long CameraDataWadOffset,
    int CameraDataByteLength,
    string CameraDataSha256,
    int CameraRawX,
    int CameraRawY,
    int CameraRawZ,
    int CutsceneIndex,
    int DragonNameIndex,
    int RunToAngle,
    int RunToRadius,
    int RunToAuxiliary,
    long SceneLinkWadOffset,
    int SceneLinkByteLength,
    string SceneLinkSha256,
    long CameraTrackWadOffset,
    int CameraTrackByteLength,
    int CameraTrackFrameCount,
    string CameraTrackSha256,
    bool CameraPlacementResolved,
    bool RoutePlacementResolved,
    bool DestinationSupportResolved);

internal sealed record Id65RewardZeroEggRequirement(
    int RequiredEggTarget,
    int EggThiefTrueIndex,
    ushort EggThiefClass,
    byte CarriedEggClass,
    string EggThiefRowSha256,
    uint EggThiefPropertiesSceneOffset,
    int EggThiefPropertiesByteLength,
    string EggThiefPropertiesSha256,
    int PropertiesPointerFixupCount,
    int InternalPropertiesFixupCount,
    bool LockedSourceAlreadySatisfiesZeroEgg,
    bool T88ReplacementRequired,
    string RequiredReplacement,
    bool WriterAuthorized);

internal sealed record Id65RewardUnresolvedRetirementCandidate(
    int TrueIndex,
    ushort ActorClass,
    string RowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256,
    string ObservedRole,
    bool RetirementResolved,
    bool RetirementAuthorized);

internal sealed record Id65RewardRejectedPlacement(
    int X,
    int Y,
    string CoordinateSpace,
    bool Rejected,
    string Reason);

internal sealed record Id65RewardResidentDependencyClosure(
    string Id65DataSha256,
    string RetailTownSquareDataSha256,
    bool Id65DataIsExactTownSquareClone,
    string ObjectTableSha256,
    string RewardRowsSha256,
    int RewardPropertiesByteLength,
    string RewardPropertiesSha256,
    int RewardPropertiesInternalFixupCount,
    string SceneSha256,
    int ScenePointerFixupCount,
    string ScenePointerFixupSha256,
    int ObjectRowPointerFixupCount,
    int RewardRowPointerFixupCount,
    int InternalScenePointerFixupCount,
    string ActorSubfileSha256,
    string ActorRootHeaderSha256,
    IReadOnlyList<Id65RewardActorPackageDependency> ActorPackages,
    IReadOnlyList<ushort> GlobalOrOverlayResidentClassesWithoutActorRoots,
    string Id65OverlaySha256,
    string RetailTownSquareOverlaySha256,
    bool Id65OverlayIsExactTownSquareClone,
    int NativeTextureRecordCount,
    string NativeTexturePagesSha256,
    int NativeTextureComponentByteLength,
    string NativeTextureComponentSha256,
    string TerrainModelSha256,
    string ExecutableSha256,
    bool PackageClosurePinned,
    bool PropertyClosurePinned,
    bool FixupClosurePinned,
    bool OverlayClosurePinned,
    bool TextureClosurePinned,
    bool ExecutableClosurePinned);

internal sealed record Id65RewardCensus(
    string ProfileId,
    string LockedSourceImageSha256,
    int ObjectRecordCount,
    IReadOnlyList<Id65RewardCensusRow> RewardRows,
    IReadOnlyList<Id65RewardClassValueLedgerEntry> ClassValueIndexLedger,
    int RewardRowCount,
    int RewardTotal,
    int LooseGemRowCount,
    int NativeDropRowCount,
    IReadOnlyDictionary<int, int> ValueHistogram,
    Id65RewardOwnerAggregate OwnerAggregate,
    IReadOnlyList<Id65RewardDragonBundle> DragonBundles,
    Id65RewardZeroEggRequirement ZeroEgg,
    Id65RewardResidentDependencyClosure Dependencies,
    IReadOnlyList<Id65RewardRetirementOwnerCandidate> RewardRetirementOwnerCandidates,
    IReadOnlyList<Id65RewardUnresolvedRetirementCandidate> NonProgressionRetirementCandidates,
    Id65RewardRejectedPlacement Rejected448By480Placement,
    IReadOnlyList<string> UnresolvedAuthoringBlockers,
    int RetirementOwnerMaximumTrueIndex,
    bool CurrentObjectTableWithinRetirementOwnerBound,
    bool EveryRewardOwnerWithinRetirementOwnerBound,
    bool RewardRetirementResolved,
    bool BullBehaviorResolved,
    bool TorroBehaviorResolved,
    bool DestinationSupportResolved,
    bool RoutePlacementResolved,
    bool DragonCameraPlacementResolved,
    bool CollectionRuntimeVerified,
    bool DeathRuntimeVerified,
    bool ReentryRuntimeVerified,
    bool MemoryCardMatrixVerified,
    bool NoCardPersistenceClaim,
    string DeterministicCensusSha256,
    bool LockedSourcePreserved,
    bool StaticReadOnly,
    bool ProducesPatches,
    bool WritesBin,
    bool WritesCue,
    bool WriterAuthorized,
    bool PublisherAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool RuntimeVerified,
    bool PromotionAuthorized,
    bool ReleaseAuthorized);

/// <summary>
/// Exact, read-only reward and dragon ownership census for the locked ID65
/// display-name image. The contract proves inherited Town Square facts and
/// dependencies only. It never creates a patch, image, CUE, publisher plan, or
/// editor integration and remains fail-closed while placement and retirement
/// semantics are unresolved.
/// </summary>
internal static class Id65RewardCensusContract
{
    public const string ProfileId =
        "id65-locked-town-square-reward-census-static-read-only-v1";
    public const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65RewardCensusContractSmoke";
    public const string LockedSourceImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string Id65OverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    public const string Id65DataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string ObjectTableSha256 =
        "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d";
    public const string RewardRowsSha256 =
        "a0078220e572b5ed440a447b5e2b20c62777541e8076cab3db66458c3d2d771f";
    public const string RewardPropertiesSha256 =
        "cca79363953ac546425ba5fc0431185037225252786e156e1b37a795973f01bf";
    public const string SceneSha256 =
        "63b5e699b3f175c0289a795c4f6f36786b9bc33ce608e1f1e9bd7bb4c04f09f6";
    public const string ScenePointerFixupSha256 =
        "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2";
    public const string ActorSubfileSha256 =
        "a37b7a8e5e5e660befed91e63c699a6c6d4b25ff8406f68b08e418c00d2a711d";
    public const string ActorRootHeaderSha256 =
        "f29e96e70f24dfb0cc0ceda02861f3832a918b74df6f8b6cb4cc5f8ff3720398";
    public const string NativeTexturePagesSha256 =
        "5fb81c4ac63eb235a172c4f16f83ef08efb148a6f543ef6100ba21359ade408f";
    public const string NativeTextureComponentSha256 =
        "f0ee8ff0d2e8554418b1bb17de860fada7af5106b07bf4908dfee179aaee6e92";
    public const string TerrainModelSha256 =
        "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
    public const string ExpectedDeterministicCensusSha256 =
        "b413e471d9fbfc13913bfc5948fefdd7316884a80be532bb635708f50063a491";

    public const int ExpectedRewardRowCount = 81;
    public const int ExpectedRewardTotal = 200;
    public const int ExpectedDragonBundleCount = 4;
    public const int RetirementOwnerMaximumTrueIndex = 255;

    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int Id65OverlayEntry = 79;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int Id65OverlayByteLength = 0xF800;
    private const int Id65DataEntry = 80;
    private const long Id65DataWadOffset = 0x6936800;
    private const int Id65DataByteLength = 0x2E2000;
    private const int RetailTownSquareOverlayEntry = 15;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareOverlayByteLength = 0xF800;
    private const int RetailTownSquareDataEntry = 16;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int RetailTownSquareDataByteLength = 0x2E2000;

    private const int TexturePagesOffset = 0x800;
    private const int TexturePagesByteLength = 0xDE000;
    private const int ModelOffset = 0xDE800;
    private const int ModelByteLength = 0x94800;
    private const int NativeTextureRecordCount = 66;
    private const int NativeTextureComponentByteLength = 0x2F78;
    private const int ActorSubfileOffset = 0x173000;
    private const int ActorSubfileByteLength = 0x5D000;
    private const int SceneOffset = 0x1D0000;
    private const int SceneByteLength = 0x8800;
    private const int ObjectCountOffset = SceneOffset + 0x16C;
    private const int ObjectTableSceneOffset = 0x170;
    private const int ObjectTableOffset = SceneOffset + ObjectTableSceneOffset;
    private const int ObjectRecordCount = 107;
    private const int ObjectRecordByteLength = 0x58;
    private const int FixupCountSceneOffset = 0x7F28;
    private const int FixupCount = 0x81;
    private const int FixupActiveByteLength = 4 + (FixupCount * 4);
    private const int ExpectedRewardPropertiesByteLength = 0xAF8;
    private const int ExpectedRewardPropertiesInternalFixups = 18;

    private static readonly LedgerSpec[] ExpectedLedger =
    [
        new(0x0017, 1, [7]),
        new(0x0017, 2, [0, 1, 5, 8, 11]),
        new(0x0017, 5, [9]),
        new(0x0017, 10, [2]),
        new(0x0053, 1, [21, 22, 24, 25, 28, 34, 35, 36, 37, 39, 41, 42, 43, 57, 58, 59, 71, 75, 76, 77, 86, 90]),
        new(0x0054, 2, [23, 29, 38, 47, 53, 54, 55, 60, 61, 72, 73, 74, 78, 87, 89, 93, 94, 95]),
        new(0x0055, 5, [56, 70, 91]),
        new(0x00C2, 1, [30, 32, 44, 45, 79]),
        new(0x00C2, 2, [31, 33, 46, 64]),
        new(0x00C2, 5, [40, 65, 81]),
        new(0x00C2, 10, [80]),
        new(0x00C3, 1, [68]),
        new(0x00C3, 2, [27, 50, 51, 63, 67, 83, 85]),
        new(0x00C3, 5, [26, 62, 66, 84]),
        new(0x00C3, 10, [52]),
        new(0x0149, 5, [82]),
        new(0x0186, 10, [69]),
        new(0x018B, 1, [10]),
        new(0x018B, 2, [6])
    ];

    private static readonly PackageSpec[] ExpectedPackages =
    [
        new(0x0017, 4, 0x185CEC, 0x23340, "3b5a1a3a1cedd3c67445f6dab9999287ce2f0c6d717a6a2473f9b7e28de4315c", "Bull reward owner"),
        new(0x014B, 5, 0x1A902C, 0x5B4, "e92ef8e3a4086c670d90e90871ce7eec2659dce00a2a3fcf3ce42319e3e35f66", "Dragon pedestal"),
        new(0x018B, 6, 0x1A95E0, 0xEF28, "288c02cbf9c1332e026822cd5ff2e1c1ab1bd2ba4756134697060398beb098ae", "Torro reward owner"),
        new(0x00C3, 10, 0x1BF038, 0x438, "b4df3f979bd8bbda73a94225a6a1202feb4942d3d823a2a86be0f2cea2b3942d", "Flame chest reward owner"),
        new(0x00C2, 11, 0x1BF470, 0x3E8, "06bcdfa6dbe9b1848b69e7fc14f7247d2ff2fb78e1e8203926eba7851f6e8da7", "Charge chest reward owner"),
        new(0x0186, 12, 0x1BF858, 0x8BC, "6ecb4e774fe27c79e2d27239a912a56de7b8c2ec3ba93d64fe9040208eae06d5", "Three-hit flame chest reward owner"),
        new(0x0149, 13, 0x1C0114, 0x528, "963d4758a0044d5bccbe0d2d137e7d152ebf35b1cbdf04e2cc6c02423eebb0a4", "Spring chest reward owner"),
        new(0x0021, 14, 0x1C063C, 0x48EC, "bdf32e953dd985712eef54e7f0fa4dcd1ab8fb7b336aa8ecedd73237092228a8", "Egg Thief requiring zero-egg replacement"),
        new(0x006E, 16, 0x1C5790, 0x2538, "9a8abc39810277ca58be7c2e59b6dcd215e066cef13872d38b84cb9e6790322f", "Dragon rescue link container"),
        new(0x00FA, 35, 0x1CE168, 0x182C, "bd1a8146c9484443d9efcb9c7e638e9fbc4f1e88e7fcaad48311cd965f73effb", "Dragon actor")
    ];

    private static readonly DragonSpec[] ExpectedDragons =
    [
        new(3, 12, 102,
            "5bd2a82c756ee8635d13a8c5739d5d028ec23d8da02df8c2c7ec8d040fa3edc2",
            "199a3afa3f15bd9f5774b9f40b99b7e917eb351980daaa63f9d73129ec7be645",
            "ab956691c7d67801b8136e5d3397db22b501a5c05b25967936003db1e2eb7fff",
            "a20104eaf3d0492b9b06f46e8192fe64c82a019fd6622a736ee5bd1e1bc44a3d",
            0x6B0BB68, "337089cca99f7cdb7b185dab6a976915275a6f6c0a5e2a3caa9c314f804010c4",
            0x6B0C654, "938be65ab4af758a0e697963eaf301a641c98f6d9b58983f9ee0e20c6009bbe2",
            0x6B5656C, 0x2220, "badcecba3c34777d781e3416b78fecf508edb5da108649c631f0a81670e71757",
            0, 11, 3072, 2560, 3072),
        new(4, 13, 103,
            "3d6ce61cb8424306c0299225fad11a5040148896a754136e52627d92e3987aad",
            "3b80d755dfd8127798ae0458f6231f88eb83b5d7c38cadba9052405ad51aa529",
            "23cb67bf0d84db362d2bd8c83c90f6e105b5c69400e33b5dacaf4b2505c90db0",
            "0b77a05dbab761c08fb893162f663e8e0f97675d576108f861c3b3e302f51385",
            0x6B0BBBC, "14ffe69959cb0bbc459786fa231a3c6fbb3131e7a342a0bb3886f824ec091f6a",
            0x6B0C67C, "54a3b76ada73128be8a4f9bcc4fde587f9dbb1d776ffdcc3163a6e836465f14c",
            0x6B99060, 0x1EF0, "438f71d29ee0d046a6a024af622dd0d1347099a2c4be40400714a48b5d842053",
            1, 12, 3584, 3276, 3584),
        new(48, 49, 104,
            "176fb57cbaf1f3d28f1ac6cfe3b5e6867ab73845247271383688e136ec266e90",
            "d6543bdf99fcdd7fe8d44686024a7b02c04ca17a9f636a13904dcfd84d5c6d43",
            "db23db1db8fb65e7200624762b371cf30539d67a796b747ace6adbe7236c8fab",
            "90b47f739f009eeedf53d808b203f80c5aeaf219dc56124e0b9e382ea279ceaa",
            0x6B0BF6C, "0617613f84e44d2f6972e848b63744c232f4b350b480fabe265b43ad621574d1",
            0x6B0C6A4, "efd9553bc155eb898b149a4429abe1640142253b6b01c1bc7d008a0108859185",
            0x6BF4CE8, 0x2B68, "a3b707b312d131ba405d92d82fe9dd240975f6bdeb6416051eb0b8061f861c4e",
            2, 14, 2048, 2457, 2048),
        new(100, 101, 105,
            "d1122aff9484123daf548a02a91bd28c9ebce478b247c5332cc2471789168bd2",
            "4d95e3ba2eb85b808471c9c4cdeb3ec674d7cdb5eeabeb32344eec0b374f251a",
            "0cd91a6fe6e505da2488490b89977a72dcc3b5c28071872100011c2c0083a4a4",
            "3c2fd7a3b926de40facc05932b50e1ddaa95d5810a5ee1243b0a0c3216ae6298",
            0x6B0C600, "443ab95f3cbcbbfcc2f0918c4556e3c306d20ceafa7f882b76b150e30828348e",
            0x6B0C6CC, "5fd648b140288a52b3e5518683576d49f5f3bee0502557898a013b4eebfa83d8",
            0x6C17D68, 0xA80, "a61145a17e20759a292fc508fb43453728341c5bb2c65f7f286c294349895156",
            3, 23, 3072, 3481, 3072)
    ];

    public static Id65RewardCensusSourceSnapshot ReadLockedSource(string sourceImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        string path = Path.GetFullPath(sourceImagePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The exact locked ID65 display-name BIN is missing.", path);

        string imageSha256 = HashFile(path);
        RequireHash(imageSha256, LockedSourceImageSha256, "locked ID65 display-name BIN");
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The ID65 reward census accepts only the locked MODE2/2352 image.");

        using FileStream image = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => name.StartsWith("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != WadByteLength)
            throw new InvalidDataException("The locked WAD.WAD LBA/length envelope changed.");
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The locked SCUS_942.28 LBA/length envelope changed.");

        RequireWadEntry(image, layout, wad.Lba, Id65OverlayEntry, Id65OverlayWadOffset, Id65OverlayByteLength);
        RequireWadEntry(image, layout, wad.Lba, Id65DataEntry, Id65DataWadOffset, Id65DataByteLength);
        RequireWadEntry(image, layout, wad.Lba, RetailTownSquareOverlayEntry,
            RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength);
        RequireWadEntry(image, layout, wad.Lba, RetailTownSquareDataEntry,
            RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength);

        return new Id65RewardCensusSourceSnapshot(
            path,
            imageSha256,
            layout.SectorSize,
            layout.UserOffset,
            wad.Lba,
            wad.Size,
            executable.Lba,
            DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size),
            DiscImage.ReadFileBytes(image, layout, wad.Lba, Id65OverlayWadOffset, Id65OverlayByteLength),
            DiscImage.ReadFileBytes(image, layout, wad.Lba, Id65DataWadOffset, Id65DataByteLength),
            DiscImage.ReadFileBytes(image, layout, wad.Lba,
                RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            DiscImage.ReadFileBytes(image, layout, wad.Lba,
                RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength));
    }

    public static Id65RewardCensus Inspect(string sourceImagePath) =>
        Inspect(ReadLockedSource(sourceImagePath));

    public static Id65RewardCensus Inspect(Id65RewardCensusSourceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireSource(source);
        byte[] data = source.Id65Data.ToArray();
        byte[] scene = data.AsSpan(SceneOffset, SceneByteLength).ToArray();
        if (ReadInt32(data, ObjectCountOffset) != ObjectRecordCount)
            throw new InvalidDataException("The locked ID65 object count is not 107.");

        byte[][] rows = Enumerable.Range(0, ObjectRecordCount)
            .Select(index => data.AsSpan(ObjectTableOffset + (index * ObjectRecordByteLength), ObjectRecordByteLength).ToArray())
            .ToArray();
        RequireHash(Hash(Join(rows)), ObjectTableSha256, "ID65 object table");

        int[] propertyPointers = rows
            .Select(row => checked((int)ReadUInt32(row, 0)))
            .Distinct()
            .Order()
            .ToArray();
        if (propertyPointers.Length != ObjectRecordCount ||
            propertyPointers[0] < ObjectTableSceneOffset + (ObjectRecordCount * ObjectRecordByteLength) ||
            propertyPointers[^1] >= FixupCountSceneOffset)
        {
            throw new InvalidDataException("The exact ID65 object properties pointer ordering changed.");
        }

        byte[] activeFixups = scene.AsSpan(FixupCountSceneOffset, FixupActiveByteLength).ToArray();
        RequireHash(Hash(activeFixups), ScenePointerFixupSha256, "ID65 scene pointer fixups");
        if (ReadInt32(activeFixups, 0) != FixupCount)
            throw new InvalidDataException("The ID65 scene pointer-fixup count is not 129.");
        int[] fixupFields = Enumerable.Range(0, FixupCount)
            .Select(index => ReadInt32(activeFixups, 4 + (index * 4)))
            .ToArray();
        int objectRowPointerFixups = Enumerable.Range(0, ObjectRecordCount)
            .Count(index => fixupFields.Count(value => value == ObjectTableSceneOffset + (index * ObjectRecordByteLength)) == 1);
        if (objectRowPointerFixups != ObjectRecordCount)
            throw new InvalidDataException("Every ID65 object properties pointer is not fixed up exactly once.");

        List<Id65RewardCensusRow> rewards = [];
        List<byte[]> rewardRowBytes = [];
        List<byte[]> rewardPropertyBytes = [];
        foreach ((byte[] row, int trueIndex) in rows.Select((row, index) => (row, index)))
        {
            RewardDecode? decoded = DecodeReward(row);
            if (decoded == null)
                continue;
            int pointer = checked((int)ReadUInt32(row, 0));
            int nextPointer = NextPropertyPointer(propertyPointers, pointer);
            byte[] properties = scene.AsSpan(pointer, nextPointer - pointer).ToArray();
            ushort actorClass = ReadUInt16(row, 0x36);
            Id65RewardOwnerSemantics ownerSemantics = actorClass switch
            {
                0x0017 => Id65RewardOwnerSemantics.BullEnemy,
                0x018B => Id65RewardOwnerSemantics.TorroEnemy,
                0x00C2 or 0x00C3 or 0x0149 or 0x0186 =>
                    Id65RewardOwnerSemantics.StationaryChestCarrier,
                0x0053 or 0x0054 or 0x0055 => Id65RewardOwnerSemantics.StationaryLooseGem,
                _ => throw new InvalidDataException(
                    $"Reward T{trueIndex} class 0x{actorClass:X4} has no pinned owner semantics.")
            };
            bool chestCarrier = ownerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier;
            rewards.Add(new(
                trueIndex,
                Id65DataWadOffset + ObjectTableOffset + (trueIndex * ObjectRecordByteLength),
                Hash(row),
                checked((uint)pointer),
                properties.Length,
                Hash(properties),
                actorClass,
                row[0x50],
                row[0x4F],
                row[0x52],
                row[0x53],
                decoded.Encoding,
                ownerSemantics,
                decoded.Value,
                trueIndex / 8,
                checked((byte)(1 << (trueIndex % 8))),
                AuthoredOwnerRow: true,
                TransientRewardChildAddsAuthoredRow: false,
                TransientRewardChildOwnsRetirementBit: false,
                TransientRewardChildRuntimeProofVerified: !chestCarrier));
            rewardRowBytes.Add(row);
            rewardPropertyBytes.Add(properties);
        }
        RequireHash(Hash(Join(rewardRowBytes)), RewardRowsSha256, "ID65 reward rows");
        byte[] rewardProperties = Join(rewardPropertyBytes);
        RequireHash(Hash(rewardProperties), RewardPropertiesSha256, "ID65 reward properties");
        if (rewardProperties.Length != ExpectedRewardPropertiesByteLength)
            throw new InvalidDataException("The ID65 reward-property closure is not 0xAF8 bytes.");

        Id65RewardClassValueLedgerEntry[] ledger = rewards
            .GroupBy(row => (row.ActorClass, row.RewardValue))
            .OrderBy(group => group.Key.ActorClass)
            .ThenBy(group => group.Key.RewardValue)
            .Select(group => new Id65RewardClassValueLedgerEntry(
                group.Key.ActorClass,
                group.Key.RewardValue,
                Array.AsReadOnly(group.Select(row => row.TrueIndex).Order().ToArray())))
            .ToArray();
        RequireLedger(ledger);

        int rewardTotal = rewards.Sum(row => row.RewardValue);
        int looseCount = rewards.Count(row => row.Encoding == Id65RewardEncoding.LooseGem);
        int dropCount = rewards.Count - looseCount;
        Dictionary<int, int> histogram = rewards
            .GroupBy(row => row.RewardValue)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());
        if (rewards.Count != ExpectedRewardRowCount || rewardTotal != ExpectedRewardTotal ||
            looseCount != 43 || dropCount != 38 ||
            !histogram.OrderBy(pair => pair.Key).SequenceEqual(
                new Dictionary<int, int> { [1] = 30, [2] = 35, [5] = 12, [10] = 4 }))
        {
            throw new InvalidDataException("The locked ID65 reward count/value histogram changed.");
        }

        Id65RewardCensusRow[] stationary = rewards
            .Where(row => row.OwnerSemantics is Id65RewardOwnerSemantics.StationaryLooseGem or
                Id65RewardOwnerSemantics.StationaryChestCarrier)
            .ToArray();
        Id65RewardCensusRow[] enemies = rewards
            .Where(row => row.OwnerSemantics is Id65RewardOwnerSemantics.BullEnemy or
                Id65RewardOwnerSemantics.TorroEnemy)
            .ToArray();
        Id65RewardCensusRow[] bulls = rewards
            .Where(row => row.OwnerSemantics == Id65RewardOwnerSemantics.BullEnemy)
            .ToArray();
        Id65RewardCensusRow[] torros = rewards
            .Where(row => row.OwnerSemantics == Id65RewardOwnerSemantics.TorroEnemy)
            .ToArray();
        Id65RewardCensusRow[] looseStationary = rewards
            .Where(row => row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryLooseGem)
            .ToArray();
        Id65RewardCensusRow[] chestCarriers = rewards
            .Where(row => row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier)
            .ToArray();
        Id65RewardOwnerAggregate ownerAggregate = new(
            stationary.Length,
            stationary.Sum(row => row.RewardValue),
            enemies.Length,
            enemies.Sum(row => row.RewardValue),
            bulls.Length,
            bulls.Sum(row => row.RewardValue),
            Array.AsReadOnly(bulls.Select(row => row.TrueIndex).ToArray()),
            torros.Length,
            torros.Sum(row => row.RewardValue),
            Array.AsReadOnly(torros.Select(row => row.TrueIndex).ToArray()),
            looseStationary.Length,
            looseStationary.Sum(row => row.RewardValue),
            chestCarriers.Length,
            chestCarriers.Sum(row => row.RewardValue),
            Array.AsReadOnly<ushort>([0x00C2, 0x00C3, 0x0149, 0x0186]),
            ChestCarrierRowOwnsRewardValue: true,
            ChestCarrierRowOwnsRetirementBit: true,
            TransientChildrenCountedAsAdditionalRows: false,
            TransientChildrenOwnAdditionalRetirementBits: false,
            RuntimeDuplicateAndChildBitProofVerified: false);
        if (ownerAggregate.StationaryOwnerRowCount != 71 ||
            ownerAggregate.StationaryOwnerRewardTotal != 171 ||
            ownerAggregate.BehaviorEnemyOwnerRowCount != 10 ||
            ownerAggregate.BehaviorEnemyOwnerRewardTotal != 29 ||
            ownerAggregate.BullOwnerRowCount != 8 || ownerAggregate.BullOwnerRewardTotal != 26 ||
            !ownerAggregate.BullTrueIndexes.SequenceEqual(new[] { 0, 1, 2, 5, 7, 8, 9, 11 }) ||
            ownerAggregate.TorroOwnerRowCount != 2 || ownerAggregate.TorroOwnerRewardTotal != 3 ||
            !ownerAggregate.TorroTrueIndexes.SequenceEqual(new[] { 6, 10 }) ||
            ownerAggregate.StationaryLooseGemOwnerRowCount != 43 ||
            ownerAggregate.StationaryLooseGemRewardTotal != 73 ||
            ownerAggregate.StationaryChestCarrierRowCount != 28 ||
            ownerAggregate.StationaryChestCarrierRewardTotal != 98)
        {
            throw new InvalidDataException("The stationary/enemy/chest reward-owner accounting changed.");
        }

        int rewardPointerFixups = rewards.Count(row =>
            fixupFields.Count(value => value == ObjectTableSceneOffset + (row.TrueIndex * ObjectRecordByteLength)) == 1);
        int rewardInternalFixups = fixupFields.Count(value => rewards.Any(row =>
            value >= row.PropertiesSceneOffset && value < row.PropertiesSceneOffset + row.PropertiesByteLength));
        if (rewardPointerFixups != ExpectedRewardRowCount ||
            rewardInternalFixups != ExpectedRewardPropertiesInternalFixups)
        {
            throw new InvalidDataException("The reward row/property fixup closure changed.");
        }

        Id65RewardActorPackageDependency[] packages = BuildPackageDependencies(data);
        Id65RewardDragonBundle[] dragons = ExpectedDragons
            .Select(spec => BuildDragonBundle(spec, data, scene, rows, propertyPointers, fixupFields))
            .ToArray();
        if (dragons.Length != ExpectedDragonBundleCount)
            throw new InvalidDataException("The ID65 dragon bundle count changed.");

        Id65RewardZeroEggRequirement zeroEgg = BuildZeroEgg(rows, scene, propertyPointers, fixupFields);
        Id65RewardUnresolvedRetirementCandidate[] nonProgression =
        [
            BuildUnresolvedRetirement(98, "Town Square ambient sound emitter near a dragon scene", rows, scene, propertyPointers),
            BuildUnresolvedRetirement(99, "Town Square ambient sound emitter near reward/actor clusters", rows, scene, propertyPointers)
        ];

        byte[] model = data.AsSpan(ModelOffset, ModelByteLength).ToArray();
        byte[] texturePages = data.AsSpan(TexturePagesOffset, TexturePagesByteLength).ToArray();
        byte[] textureComponent = model.AsSpan(0, NativeTextureComponentByteLength).ToArray();
        if (ReadInt32(model, 0) != NativeTextureComponentByteLength ||
            ReadInt32(model, 4) != NativeTextureRecordCount)
        {
            throw new InvalidDataException("The ID65 native texture component/count changed.");
        }
        Id65RewardResidentDependencyClosure dependencies = new(
            Hash(data),
            Hash(source.RetailTownSquareData.Span),
            source.Id65Data.Span.SequenceEqual(source.RetailTownSquareData.Span),
            Hash(Join(rows)),
            Hash(Join(rewardRowBytes)),
            rewardProperties.Length,
            Hash(rewardProperties),
            rewardInternalFixups,
            Hash(scene),
            FixupCount,
            Hash(activeFixups),
            objectRowPointerFixups,
            rewardPointerFixups,
            FixupCount - objectRowPointerFixups,
            Hash(data.AsSpan(ActorSubfileOffset, ActorSubfileByteLength)),
            Hash(data.AsSpan(0, 0x200)),
            Array.AsReadOnly(packages),
            Array.AsReadOnly<ushort>([0x0053, 0x0054, 0x0055, 0x011E]),
            Hash(source.Id65Overlay.Span),
            Hash(source.RetailTownSquareOverlay.Span),
            source.Id65Overlay.Span.SequenceEqual(source.RetailTownSquareOverlay.Span),
            NativeTextureRecordCount,
            Hash(texturePages),
            NativeTextureComponentByteLength,
            Hash(textureComponent),
            Hash(model),
            Hash(source.Executable.Span),
            PackageClosurePinned: true,
            PropertyClosurePinned: true,
            FixupClosurePinned: true,
            OverlayClosurePinned: true,
            TextureClosurePinned: true,
            ExecutableClosurePinned: true);
        RequireDependencies(dependencies);

        Id65RewardRetirementOwnerCandidate[] retirementOwners = rewards
            .Select(row => new Id65RewardRetirementOwnerCandidate(
                row.TrueIndex,
                row.ActorClass,
                row.RewardValue,
                row.RetirementByteIndex,
                row.RetirementBitMask,
                RetirementAuthorized: false,
                "Candidate-only owner mapping; remove/collect semantics and runtime persistence are not authorized."))
            .ToArray();
        if (retirementOwners.Any(owner => owner.TrueIndex > RetirementOwnerMaximumTrueIndex))
            throw new InvalidDataException("A reward retirement-owner candidate exceeds true index 255.");

        string[] blockers =
        [
            "Destination support under authored reward and actor placement is unresolved.",
            "Destination route/link placement for behavior-owning Mobys is unresolved.",
            "All four dragon bundles have exact camera/link/container/track fingerprints, but authored camera placement is unresolved.",
            "The proposed 448x480 destination placement is rejected and cannot be used as support evidence.",
            "Bull class 0x0017 and Torro class 0x018B behavior/retirement semantics are unresolved.",
            "Nonprogression Town Square T98/T99 retirement is unresolved.",
            "A zero-egg profile requires replacement of T88 Egg Thief before a zero target can be claimed.",
            "Reward retirement, save persistence, and collection/death/re-entry runtime matrices are not verified.",
            "Chest carriers own the authored reward row and retirement bit, but transient-child duplicate and child-bit runtime proof is pending.",
            "The physical memory-card fresh/old/corrupt-card persistence matrix is not verified; this census makes no card-persistence claim."
        ];
        string deterministicHash = ComputeDeterministicHash(
            ledger,
            dragons,
            packages,
            dependencies,
            zeroEgg,
            nonProgression,
            blockers);
        RequireHash(
            deterministicHash,
            ExpectedDeterministicCensusSha256,
            "deterministic ID65 reward census");

        return new Id65RewardCensus(
            ProfileId,
            source.SourceImageSha256,
            ObjectRecordCount,
            Array.AsReadOnly(rewards.ToArray()),
            Array.AsReadOnly(ledger),
            rewards.Count,
            rewardTotal,
            looseCount,
            dropCount,
            new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(histogram),
            ownerAggregate,
            Array.AsReadOnly(dragons),
            zeroEgg,
            dependencies,
            Array.AsReadOnly(retirementOwners),
            Array.AsReadOnly(nonProgression),
            new(448, 480, "rejected destination authoring coordinates", true,
                "No support, route, or camera acceptance evidence exists at 448x480."),
            Array.AsReadOnly(blockers),
            RetirementOwnerMaximumTrueIndex,
            CurrentObjectTableWithinRetirementOwnerBound: ObjectRecordCount - 1 <= RetirementOwnerMaximumTrueIndex,
            EveryRewardOwnerWithinRetirementOwnerBound: true,
            RewardRetirementResolved: false,
            BullBehaviorResolved: false,
            TorroBehaviorResolved: false,
            DestinationSupportResolved: false,
            RoutePlacementResolved: false,
            DragonCameraPlacementResolved: false,
            CollectionRuntimeVerified: false,
            DeathRuntimeVerified: false,
            ReentryRuntimeVerified: false,
            MemoryCardMatrixVerified: false,
            NoCardPersistenceClaim: true,
            deterministicHash,
            LockedSourcePreserved: true,
            StaticReadOnly: true,
            ProducesPatches: false,
            WritesBin: false,
            WritesCue: false,
            WriterAuthorized: false,
            PublisherAuthorized: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            RuntimeVerified: false,
            PromotionAuthorized: false,
            ReleaseAuthorized: false);
    }

    private static void RequireSource(Id65RewardCensusSourceSnapshot source)
    {
        RequireHash(source.SourceImageSha256, LockedSourceImageSha256, "source snapshot image identity");
        if (source.SectorSize != 2352 || source.UserDataOffset != 24 ||
            source.WadLba != WadLba || source.WadByteLength != WadByteLength ||
            source.ExecutableLba != ExecutableLba || source.Executable.Length != ExecutableByteLength ||
            source.Id65Overlay.Length != Id65OverlayByteLength || source.Id65Data.Length != Id65DataByteLength ||
            source.RetailTownSquareOverlay.Length != RetailTownSquareOverlayByteLength ||
            source.RetailTownSquareData.Length != RetailTownSquareDataByteLength)
        {
            throw new InvalidDataException("The reward census source snapshot envelope changed.");
        }
        RequireHash(Hash(source.Executable.Span), ExecutableSha256, "SCUS_942.28");
        RequireHash(Hash(source.Id65Overlay.Span), Id65OverlaySha256, "ID65 overlay");
        RequireHash(Hash(source.Id65Data.Span), Id65DataSha256, "ID65 data");
        RequireHash(Hash(source.RetailTownSquareOverlay.Span), Id65OverlaySha256, "retail Town Square overlay");
        RequireHash(Hash(source.RetailTownSquareData.Span), Id65DataSha256, "retail Town Square data");
        if (!source.Id65Overlay.Span.SequenceEqual(source.RetailTownSquareOverlay.Span) ||
            !source.Id65Data.Span.SequenceEqual(source.RetailTownSquareData.Span))
        {
            throw new InvalidDataException("The locked ID65 overlay/data are not exact Town Square clones.");
        }
    }

    private static void RequireWadEntry(
        FileStream image,
        DiscLayout layout,
        int wadLba,
        int entry,
        long expectedOffset,
        int expectedLength)
    {
        byte[] row = DiscImage.ReadFileBytes(image, layout, wadLba, entry * 8L, 8);
        long offset = ReadUInt32(row, 0);
        int length = checked((int)ReadUInt32(row, 4));
        if (offset != expectedOffset || length != expectedLength)
            throw new InvalidDataException($"WAD row {entry} changed from 0x{expectedOffset:X}/0x{expectedLength:X}.");
    }

    private static RewardDecode? DecodeReward(byte[] row)
    {
        int looseValue = row[0x36] switch
        {
            0x53 when row[0x4F] == 0x01 => 1,
            0x54 when row[0x4F] == 0x02 => 2,
            0x55 when row[0x4F] == 0x03 => 5,
            0x56 when row[0x4F] == 0x04 => 10,
            0x57 when row[0x4F] == 0x05 => 25,
            _ => 0
        };
        if (row[0x50] == 0x18 && looseValue > 0)
            return new(Id65RewardEncoding.LooseGem, looseValue);
        int dropValue = row[0x53] switch
        {
            0x53 => 1,
            0x54 => 2,
            0x55 => 5,
            0x56 => 10,
            0x57 => 25,
            _ => 0
        };
        return dropValue == 0 ? null : new(Id65RewardEncoding.NativeDrop, dropValue);
    }

    private static void RequireLedger(IReadOnlyList<Id65RewardClassValueLedgerEntry> ledger)
    {
        if (ledger.Count != ExpectedLedger.Length)
            throw new InvalidDataException("The reward class/value ledger group count changed.");
        for (int index = 0; index < ExpectedLedger.Length; index++)
        {
            LedgerSpec expected = ExpectedLedger[index];
            Id65RewardClassValueLedgerEntry actual = ledger[index];
            if (actual.ActorClass != expected.ActorClass || actual.RewardValue != expected.Value ||
                !actual.TrueIndexes.SequenceEqual(expected.TrueIndexes))
            {
                throw new InvalidDataException($"The reward class/value/index ledger changed at group {index}.");
            }
        }
    }

    private static Id65RewardActorPackageDependency[] BuildPackageDependencies(byte[] data)
    {
        int[] roots = Enumerable.Range(0, 64).Select(index => ReadInt32(data, 0x50 + (index * 4))).ToArray();
        ushort[] actorIds = Enumerable.Range(0, 64).Select(index => ReadUInt16(data, 0x150 + (index * 2))).ToArray();
        int populated = Array.IndexOf(roots, 0);
        if (populated != 37)
            throw new InvalidDataException("The ID65 actor-root count is not 37.");
        for (int index = 0; index < populated; index++)
        {
            if (roots[index] < ActorSubfileOffset || roots[index] >= SceneOffset ||
                index > 0 && roots[index] <= roots[index - 1])
            {
                throw new InvalidDataException("The ID65 actor-root table is not strictly ordered inside its subfile.");
            }
        }
        List<Id65RewardActorPackageDependency> result = [];
        foreach (PackageSpec expected in ExpectedPackages)
        {
            if (expected.RootIndex >= populated || actorIds[expected.RootIndex] != expected.ActorClass ||
                roots[expected.RootIndex] != expected.Offset)
            {
                throw new InvalidDataException($"Actor package root 0x{expected.ActorClass:X4} changed.");
            }
            int next = expected.RootIndex + 1 < populated ? roots[expected.RootIndex + 1] : SceneOffset;
            int length = next - expected.Offset;
            byte[] package = data.AsSpan(expected.Offset, length).ToArray();
            if (length != expected.Length)
                throw new InvalidDataException($"Actor package 0x{expected.ActorClass:X4} length changed.");
            RequireHash(Hash(package), expected.Sha256, $"actor package 0x{expected.ActorClass:X4}");
            result.Add(new(
                expected.ActorClass,
                expected.RootIndex,
                expected.Offset,
                length,
                Id65DataWadOffset + expected.Offset,
                Hash(package),
                expected.Role));
        }
        ushort[] rootless = [0x0053, 0x0054, 0x0055, 0x011E];
        if (rootless.Any(actorId => actorIds.Take(populated).Contains(actorId)))
            throw new InvalidDataException("A pinned global/overlay-resident class unexpectedly gained an actor root.");
        return result.ToArray();
    }

    private static Id65RewardDragonBundle BuildDragonBundle(
        DragonSpec expected,
        byte[] data,
        byte[] scene,
        IReadOnlyList<byte[]> rows,
        int[] propertyPointers,
        int[] fixupFields)
    {
        byte[] pedestal = rows[expected.PedestalTrueIndex];
        byte[] dragon = rows[expected.DragonTrueIndex];
        byte[] container = rows[expected.ContainerTrueIndex];
        RequireHash(Hash(pedestal), expected.PedestalRowSha256, $"dragon T{expected.DragonTrueIndex} pedestal row");
        RequireHash(Hash(dragon), expected.DragonRowSha256, $"dragon T{expected.DragonTrueIndex} actor row");
        RequireHash(Hash(container), expected.ContainerRowSha256, $"dragon T{expected.DragonTrueIndex} container row");
        RequireHash(Hash(Join([pedestal, dragon, container])), expected.BundleSha256,
            $"dragon T{expected.DragonTrueIndex} three-row bundle");
        if (ReadUInt16(pedestal, 0x36) != 0x014B || ReadUInt16(dragon, 0x36) != 0x00FA ||
            ReadUInt16(container, 0x36) != 0x006E)
        {
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} class triple changed.");
        }

        int pedestalPointer = checked((int)ReadUInt32(pedestal, 0));
        int pedestalNext = NextPropertyPointer(propertyPointers, pedestalPointer);
        byte[] pedestalProperties = scene.AsSpan(pedestalPointer, pedestalNext - pedestalPointer).ToArray();
        int cameraPointer = checked((int)ReadUInt32(dragon, 0));
        long cameraWadOffset = Id65DataWadOffset + SceneOffset + cameraPointer;
        byte[] camera = scene.AsSpan(cameraPointer, 0x44).ToArray();
        int linkPointer = checked((int)ReadUInt32(container, 0));
        long linkWadOffset = Id65DataWadOffset + SceneOffset + linkPointer;
        byte[] link = scene.AsSpan(linkPointer, 0x28).ToArray();
        if (cameraWadOffset != expected.CameraWadOffset || linkWadOffset != expected.LinkWadOffset)
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} camera/link pointers changed.");
        RequireHash(Hash(camera), expected.CameraSha256, $"dragon T{expected.DragonTrueIndex} camera data");
        RequireHash(Hash(link), expected.LinkSha256, $"dragon T{expected.DragonTrueIndex} scene link");
        if (ReadInt32(link, 0) != expected.DragonTrueIndex || ReadInt32(link, 4) != expected.PedestalTrueIndex ||
            link.AsSpan(8).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} scene-link grammar changed.");
        }
        int cutscene = ReadInt32(camera, 0x18);
        if (ReadInt32(camera, 0x20) != expected.PedestalTrueIndex || ReadInt32(camera, 0x24) != -1 ||
            cutscene != expected.CutsceneIndex || ReadInt32(camera, 0x28) != expected.RunToAngle ||
            ReadInt32(camera, 0x2C) != expected.RunToRadius || ReadInt32(camera, 0x30) != expected.RunToAuxiliary ||
            ReadInt32(camera, 0x34) != 0 || ReadInt32(camera, 0x38) != expected.DragonNameIndex ||
            ReadInt32(camera, 0x3C) != 0x5622 || ReadInt32(camera, 0x40) != 40)
        {
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} camera/link/container fingerprint changed.");
        }

        int nestedHeaderOffset = 0x20 + (cutscene * 8);
        int cutsceneOffset = checked((int)ReadUInt32(data, nestedHeaderOffset));
        int cutsceneLength = checked((int)ReadUInt32(data, nestedHeaderOffset + 4));
        if (cutsceneOffset <= 0 || cutsceneLength <= 0 || cutsceneOffset + cutsceneLength > data.Length ||
            ReadInt32(data, cutsceneOffset + 4) != 0x24)
        {
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} cutscene envelope changed.");
        }
        int trackRelative = checked((int)ReadUInt32(data, cutsceneOffset + 0x1C));
        int trackLength = checked((int)ReadUInt32(data, cutsceneOffset + 0x20));
        int trackOffset = checked(cutsceneOffset + trackRelative);
        long trackWadOffset = Id65DataWadOffset + trackOffset;
        if (trackWadOffset != expected.TrackWadOffset || trackLength != expected.TrackByteLength ||
            trackLength % 0x18 != 0 || trackRelative < 0x24 || trackRelative + trackLength > cutsceneLength)
        {
            throw new InvalidDataException($"Dragon T{expected.DragonTrueIndex} camera-track envelope changed.");
        }
        byte[] track = data.AsSpan(trackOffset, trackLength).ToArray();
        RequireHash(Hash(track), expected.TrackSha256, $"dragon T{expected.DragonTrueIndex} camera track");

        foreach (int trueIndex in new[] { expected.PedestalTrueIndex, expected.DragonTrueIndex, expected.ContainerTrueIndex })
        {
            if (fixupFields.Count(value => value == ObjectTableSceneOffset + (trueIndex * ObjectRecordByteLength)) != 1)
                throw new InvalidDataException($"Dragon bundle row T{trueIndex} lost its properties pointer fixup.");
        }
        return new(
            expected.PedestalTrueIndex,
            expected.DragonTrueIndex,
            expected.ContainerTrueIndex,
            Hash(pedestal),
            Hash(dragon),
            Hash(container),
            Hash(Join([pedestal, dragon, container])),
            Id65DataWadOffset + SceneOffset + pedestalPointer,
            pedestalProperties.Length,
            Hash(pedestalProperties),
            cameraWadOffset,
            camera.Length,
            Hash(camera),
            ReadInt32(camera, 0),
            ReadInt32(camera, 4),
            ReadInt32(camera, 8),
            cutscene,
            ReadInt32(camera, 0x38),
            ReadInt32(camera, 0x28),
            ReadInt32(camera, 0x2C),
            ReadInt32(camera, 0x30),
            linkWadOffset,
            link.Length,
            Hash(link),
            trackWadOffset,
            trackLength,
            trackLength / 0x18,
            Hash(track),
            CameraPlacementResolved: false,
            RoutePlacementResolved: false,
            DestinationSupportResolved: false);
    }

    private static Id65RewardZeroEggRequirement BuildZeroEgg(
        IReadOnlyList<byte[]> rows,
        byte[] scene,
        int[] propertyPointers,
        int[] fixupFields)
    {
        const int trueIndex = 88;
        byte[] row = rows[trueIndex];
        const string rowSha256 = "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a";
        RequireHash(Hash(row), rowSha256, "ID65 T88 Egg Thief row");
        int pointer = checked((int)ReadUInt32(row, 0));
        int next = NextPropertyPointer(propertyPointers, pointer);
        byte[] properties = scene.AsSpan(pointer, next - pointer).ToArray();
        const string propertiesSha256 = "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136";
        RequireHash(Hash(properties), propertiesSha256, "ID65 T88 Egg Thief properties");
        int pointerFixups = fixupFields.Count(value => value == ObjectTableSceneOffset + (trueIndex * ObjectRecordByteLength));
        int internalFixups = fixupFields.Count(value => value >= pointer && value < next);
        if (ReadUInt16(row, 0x36) != 0x0021 || row[0x50] != 0x20 || row[0x52] != 0x30 ||
            row[0x53] != 0x22 || pointer != 0x5AFC || properties.Length != 0x134 ||
            pointerFixups != 1 || internalFixups != 1)
        {
            throw new InvalidDataException("The exact T88 Egg Thief/carried-egg dependency changed.");
        }
        return new(
            RequiredEggTarget: 0,
            EggThiefTrueIndex: trueIndex,
            EggThiefClass: 0x0021,
            CarriedEggClass: 0x22,
            EggThiefRowSha256: Hash(row),
            EggThiefPropertiesSceneOffset: checked((uint)pointer),
            EggThiefPropertiesByteLength: properties.Length,
            EggThiefPropertiesSha256: Hash(properties),
            PropertiesPointerFixupCount: pointerFixups,
            InternalPropertiesFixupCount: internalFixups,
            LockedSourceAlreadySatisfiesZeroEgg: false,
            T88ReplacementRequired: true,
            RequiredReplacement: "Replace T88 with a verified controller-free nonprogression bundle while preserving the zero-egg profile; the current locked source remains an Egg Thief.",
            WriterAuthorized: false);
    }

    private static Id65RewardUnresolvedRetirementCandidate BuildUnresolvedRetirement(
        int trueIndex,
        string role,
        IReadOnlyList<byte[]> rows,
        byte[] scene,
        int[] propertyPointers)
    {
        string expectedRow = trueIndex switch
        {
            98 => "ace3bdc7adeb3bedbf004b82d08479e3a7b6ffd81b678aa37d28bb3147932e64",
            99 => "96fa444f14942c9f7131166c33dee94af9480dd0d25e696d7a74a44d62f891c3",
            _ => throw new InvalidOperationException("Unexpected unresolved retirement candidate.")
        };
        string expectedProperties = trueIndex switch
        {
            98 => "39fcbc267bb7db079ed61072b0be4c8d01912e623758010e4cb2462cc422c339",
            99 => "093cd37e3985cb80ec66fe80963805e1a423c43a2486a113bb8d70b2a6081c5f",
            _ => throw new InvalidOperationException("Unexpected unresolved retirement candidate.")
        };
        byte[] row = rows[trueIndex];
        int pointer = checked((int)ReadUInt32(row, 0));
        int next = NextPropertyPointer(propertyPointers, pointer);
        byte[] properties = scene.AsSpan(pointer, next - pointer).ToArray();
        RequireHash(Hash(row), expectedRow, $"ID65 T{trueIndex} nonprogression row");
        RequireHash(Hash(properties), expectedProperties, $"ID65 T{trueIndex} nonprogression properties");
        if (ReadUInt16(row, 0x36) != 0x011E || properties.Length != 0x60)
            throw new InvalidDataException($"ID65 T{trueIndex} nonprogression fingerprint changed.");
        return new(
            trueIndex,
            0x011E,
            Hash(row),
            checked((uint)pointer),
            properties.Length,
            Hash(properties),
            role,
            RetirementResolved: false,
            RetirementAuthorized: false);
    }

    private static void RequireDependencies(Id65RewardResidentDependencyClosure value)
    {
        RequireHash(value.Id65DataSha256, Id65DataSha256, "dependency ID65 data");
        RequireHash(value.RetailTownSquareDataSha256, Id65DataSha256, "dependency Town Square data");
        RequireHash(value.ObjectTableSha256, ObjectTableSha256, "dependency object table");
        RequireHash(value.RewardRowsSha256, RewardRowsSha256, "dependency reward rows");
        RequireHash(value.RewardPropertiesSha256, RewardPropertiesSha256, "dependency reward properties");
        RequireHash(value.SceneSha256, SceneSha256, "dependency scene");
        RequireHash(value.ScenePointerFixupSha256, ScenePointerFixupSha256, "dependency fixups");
        RequireHash(value.ActorSubfileSha256, ActorSubfileSha256, "dependency actor subfile");
        RequireHash(value.ActorRootHeaderSha256, ActorRootHeaderSha256, "dependency actor roots");
        RequireHash(value.Id65OverlaySha256, Id65OverlaySha256, "dependency overlay");
        RequireHash(value.RetailTownSquareOverlaySha256, Id65OverlaySha256, "dependency Town Square overlay");
        RequireHash(value.NativeTexturePagesSha256, NativeTexturePagesSha256, "dependency texture pages");
        RequireHash(value.NativeTextureComponentSha256, NativeTextureComponentSha256, "dependency texture component");
        RequireHash(value.TerrainModelSha256, TerrainModelSha256, "dependency terrain model");
        RequireHash(value.ExecutableSha256, ExecutableSha256, "dependency executable");
        if (!value.Id65DataIsExactTownSquareClone || !value.Id65OverlayIsExactTownSquareClone ||
            value.RewardPropertiesByteLength != ExpectedRewardPropertiesByteLength ||
            value.RewardPropertiesInternalFixupCount != ExpectedRewardPropertiesInternalFixups ||
            value.ScenePointerFixupCount != FixupCount || value.ObjectRowPointerFixupCount != ObjectRecordCount ||
            value.RewardRowPointerFixupCount != ExpectedRewardRowCount || value.InternalScenePointerFixupCount != 22 ||
            value.ActorPackages.Count != ExpectedPackages.Length || value.NativeTextureRecordCount != NativeTextureRecordCount ||
            value.NativeTextureComponentByteLength != NativeTextureComponentByteLength ||
            !value.PackageClosurePinned || !value.PropertyClosurePinned || !value.FixupClosurePinned ||
            !value.OverlayClosurePinned || !value.TextureClosurePinned || !value.ExecutableClosurePinned)
        {
            throw new InvalidDataException("The resident reward dependency closure changed.");
        }
    }

    private static string ComputeDeterministicHash(
        IReadOnlyList<Id65RewardClassValueLedgerEntry> ledger,
        IReadOnlyList<Id65RewardDragonBundle> dragons,
        IReadOnlyList<Id65RewardActorPackageDependency> packages,
        Id65RewardResidentDependencyClosure dependencies,
        Id65RewardZeroEggRequirement zeroEgg,
        IReadOnlyList<Id65RewardUnresolvedRetirementCandidate> nonProgression,
        IReadOnlyList<string> blockers)
    {
        StringBuilder value = new();
        Add(ProfileId);
        Add(LockedSourceImageSha256);
        Add(ObjectTableSha256);
        Add(RewardRowsSha256);
        Add(RewardPropertiesSha256);
        Add(ExpectedRewardRowCount);
        Add(ExpectedRewardTotal);
        foreach (Id65RewardClassValueLedgerEntry item in ledger)
            Add($"{item.ActorClass:X4}|{item.RewardValue}|{string.Join(',', item.TrueIndexes)}");
        foreach (Id65RewardDragonBundle item in dragons)
            Add($"{item.PedestalTrueIndex}|{item.DragonTrueIndex}|{item.ContainerTrueIndex}|{item.ThreeRowBundleSha256}|{item.CameraDataSha256}|{item.SceneLinkSha256}|{item.CameraTrackSha256}");
        foreach (Id65RewardActorPackageDependency item in packages)
            Add($"{item.ActorClass:X4}|{item.ActorRootIndex}|{item.EntryRelativeOffset:X}|{item.ByteLength:X}|{item.Sha256}");
        Add(dependencies.ScenePointerFixupSha256);
        Add(dependencies.NativeTexturePagesSha256);
        Add(dependencies.NativeTextureComponentSha256);
        Add(dependencies.Id65OverlaySha256);
        Add(dependencies.ExecutableSha256);
        Add($"T{zeroEgg.EggThiefTrueIndex}|{zeroEgg.EggThiefRowSha256}|{zeroEgg.EggThiefPropertiesSha256}|{zeroEgg.RequiredEggTarget}");
        foreach (Id65RewardUnresolvedRetirementCandidate item in nonProgression)
            Add($"T{item.TrueIndex}|{item.RowSha256}|{item.PropertiesSha256}|false");
        Add("448x480|rejected");
        Add("retirement-max|255");
        foreach (string blocker in blockers)
            Add(blocker);
        return Hash(Encoding.UTF8.GetBytes(value.ToString()));

        void Add(object item) => value.Append(item).Append('\n');
    }

    private static int NextPropertyPointer(int[] pointers, int pointer)
    {
        int index = Array.BinarySearch(pointers, pointer);
        if (index < 0 || index + 1 >= pointers.Length)
            return FixupCountSceneOffset;
        return pointers[index + 1];
    }

    private static byte[] Join(IEnumerable<byte[]> values)
    {
        byte[][] arrays = values.ToArray();
        int length = checked(arrays.Sum(array => array.Length));
        byte[] result = new byte[length];
        int cursor = 0;
        foreach (byte[] array in arrays)
        {
            array.CopyTo(result, cursor);
            cursor += array.Length;
        }
        return result;
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static int ReadInt32(ReadOnlySpan<byte> value, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(value.Slice(offset, 4));

    private static uint ReadUInt32(ReadOnlySpan<byte> value, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(value.Slice(offset, 4));

    private static ushort ReadUInt16(ReadOnlySpan<byte> value, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(value.Slice(offset, 2));

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private sealed record RewardDecode(Id65RewardEncoding Encoding, int Value);
    private sealed record LedgerSpec(ushort ActorClass, int Value, int[] TrueIndexes);
    private sealed record PackageSpec(
        ushort ActorClass,
        int RootIndex,
        int Offset,
        int Length,
        string Sha256,
        string Role);
    private sealed record DragonSpec(
        int PedestalTrueIndex,
        int DragonTrueIndex,
        int ContainerTrueIndex,
        string PedestalRowSha256,
        string DragonRowSha256,
        string ContainerRowSha256,
        string BundleSha256,
        long CameraWadOffset,
        string CameraSha256,
        long LinkWadOffset,
        string LinkSha256,
        long TrackWadOffset,
        int TrackByteLength,
        string TrackSha256,
        int CutsceneIndex,
        int DragonNameIndex,
        int RunToAngle,
        int RunToRadius,
        int RunToAuxiliary);
}
