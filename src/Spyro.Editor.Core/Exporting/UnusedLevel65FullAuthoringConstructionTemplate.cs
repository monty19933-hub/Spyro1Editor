using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

internal enum UnusedLevel65ConstructionComponentPolicy
{
    PreserveContainer,
    RebuildAtomically,
    ReplaceAfterFocusedRuntimeGate
}

internal sealed record UnusedLevel65ConstructionComponentLayout(
    string Name,
    long WadOffset,
    int ByteLength,
    string Sha256,
    UnusedLevel65ConstructionComponentPolicy Policy);

internal sealed record UnusedLevel65ConstructionDataSubfileLayout(
    int Index,
    int RelativeOffset,
    long WadOffset,
    int ByteLength,
    string Sha256,
    bool MustRemainByteIdenticalInFirstTerrainGate);

internal sealed record UnusedLevel65ConstructionSceneSectorLayout(
    int SectorIndex,
    long WadOffset,
    int ByteLength,
    int GapBytesAfter,
    int LowDetailVertexCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailFaceCount);

internal sealed record UnusedLevel65ConstructionLandingInvariant(
    long WadOffset,
    int ByteLength,
    string Hex,
    string Sha256,
    int RawX,
    int RawY,
    int RawZ,
    int YawByte);

internal sealed record UnusedLevel65ConstructionPlayerAnchorInvariant(
    int TrueIndex,
    long WadOffset,
    int ByteLength,
    string Sha256,
    int RawX,
    int RawY,
    int RawZ);

internal sealed record UnusedLevel65ConstructionDisplayNameInvariant(
    string ExecutableName,
    int ExecutableLba,
    int ExecutableByteLength,
    long PointerTableFileOffset,
    int SlotIndex,
    long PointerFileOffset,
    string PointerHex,
    int ResolvedStringFileOffset,
    string ResolvedString);

internal sealed record UnusedLevel65FullAuthoringConstructionContract(
    string ProfileId,
    string BaselineProfileId,
    string BaselineImageSha256,
    int WadLba,
    int WadByteLength,
    long DataEntryWadOffset,
    int DataEntryByteLength,
    long ModelSubfileWadOffset,
    int ModelSubfileByteLength,
    int NativeTextureRecordCount,
    int SceneSectorCount,
    int LowDetailVertexCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailFaceCount,
    int SpecialSurfaceCount,
    int CollisionTriangleCount,
    int PortalCount,
    long ObjectTableWadOffset,
    int ObjectRecordCount,
    UnusedLevel65ConstructionLandingInvariant Landing,
    UnusedLevel65ConstructionPlayerAnchorInvariant PlayerAnchor,
    UnusedLevel65ConstructionDisplayNameInvariant DisplayName,
    long UsedModelEndWadOffset,
    int VerifiedZeroTailBytes,
    IReadOnlyList<UnusedLevel65ConstructionDataSubfileLayout> DataSubfiles,
    IReadOnlyList<UnusedLevel65ConstructionComponentLayout> Components,
    IReadOnlyList<UnusedLevel65ConstructionSceneSectorLayout> SceneSectors,
    bool RequiresSpawnSafeHighAndLowDetailFoundation,
    bool RequiresCompleteCollisionAndOcclusionOwnership,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal sealed record UnusedLevel65ConstructionComponentRelocation(
    string Name,
    long OldWadOffset,
    long NewWadOffset,
    int ByteLength,
    string ContentSha256);

internal sealed record UnusedLevel65SceneSectorCapacityPlan(
    string ProfileId,
    string BaselineImageSha256,
    int TargetSectorIndex,
    int ReservedByteCount,
    long ReservationWadOffset,
    int OldEnvironmentByteLength,
    int NewEnvironmentByteLength,
    long OldUsedModelEndWadOffset,
    long NewUsedModelEndWadOffset,
    int ZeroTailBytesBefore,
    int ZeroTailBytesAfter,
    string BeforeModelSha256,
    string AfterModelSha256,
    byte[] BeforeModelBytes,
    byte[] AfterModelBytes,
    IReadOnlyList<UnusedLevel65ConstructionComponentRelocation> RelocatedComponents,
    IReadOnlyList<UnusedLevel65ConstructionSceneSectorLayout> SceneSectors,
    bool ComponentContentsPreserved,
    bool RetailDataTouched,
    bool Publishable,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal readonly record struct UnusedLevel65ConstructionPoint(int X, int Y, int Z);

internal sealed record UnusedLevel65ConstructionVisualTerrainPlan(
    string ProfileId,
    string BaselineImageSha256,
    int SectorIndex,
    int AddedLowDetailVertexCount,
    int AddedLowDetailFaceCount,
    int AddedHighDetailVertexCount,
    int AddedHighDetailFaceCount,
    IReadOnlyList<UnusedLevel65ConstructionPoint> Points,
    long SectorWadOffset,
    int OldSectorByteLength,
    int NewSectorByteLength,
    long OldOcclusionWadOffset,
    long NewOcclusionWadOffset,
    long OldCollisionWadOffset,
    long NewCollisionWadOffset,
    int ZeroTailBytesAfter,
    string BeforeModelSha256,
    string AfterModelSha256,
    byte[] AfterModelBytes,
    bool HighAndLowDetailAllocated,
    bool SectorCullBoundsPreserved,
    bool OcclusionContainerPreserved,
    bool CollisionComposed,
    bool Publishable,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

internal readonly record struct UnusedLevel65ConstructionCollisionCell(int X, int Y, int Z);

internal sealed record UnusedLevel65ConstructionCollisionBinding(
    int ReusedTriangleIndex,
    long SourceTriangleWadOffset,
    long AuthoredTriangleWadOffset,
    string BeforeHex,
    string AfterHex,
    IReadOnlyList<UnusedLevel65ConstructionPoint> SourcePoints,
    IReadOnlyList<UnusedLevel65ConstructionPoint> TargetPoints,
    int SourceAssignment,
    int TargetAssignment,
    int SourceLookupReferenceCount,
    int AuthoredLookupReferenceCount,
    IReadOnlyList<long> AuthoredLookupWadOffsets,
    IReadOnlyList<UnusedLevel65ConstructionCollisionCell> TargetCells,
    bool SourceWasZeroArea,
    bool UpwardWinding,
    bool OrdinaryCollisionFlags);

internal sealed record UnusedLevel65ConstructionPreservedDegenerateBinding(
    int TriangleIndex,
    string TriangleHex,
    int Assignment,
    int CollisionFlags,
    IReadOnlyList<UnusedLevel65ConstructionCollisionCell> NativeCells,
    IReadOnlyList<long> NativeLookupWadOffsets,
    IReadOnlyList<UnusedLevel65ConstructionCollisionCell> AuthoredCells,
    IReadOnlyList<long> AuthoredLookupWadOffsets);

internal readonly record struct UnusedLevel65ConstructionTerrainOverlap(
    int SectorIndex,
    int FaceIndex,
    int MinimumZ,
    int MaximumZ);

internal readonly record struct UnusedLevel65ConstructionCollisionOverlap(
    int TriangleIndex,
    int MinimumZ,
    int MaximumZ);

internal sealed record UnusedLevel65ConstructionExposureProof(
    int ScannedSectorCount,
    int ScannedLowDetailFaceCount,
    int ScannedHighDetailFaceCount,
    int ScannedCollisionTriangleCount,
    IReadOnlyList<UnusedLevel65ConstructionTerrainOverlap> NativeLowDetailInteriorOverlaps,
    IReadOnlyList<UnusedLevel65ConstructionTerrainOverlap> NativeHighDetailInteriorOverlaps,
    IReadOnlyList<UnusedLevel65ConstructionCollisionOverlap> NativeCollisionInteriorOverlaps,
    IReadOnlyList<UnusedLevel65ConstructionTerrainOverlap> NativeLowDetailTopmostAtAuthoredCentroid,
    IReadOnlyList<UnusedLevel65ConstructionTerrainOverlap> NativeHighDetailTopmostAtAuthoredCentroid,
    int AuthoredLowDetailFaceIndex,
    int AuthoredHighDetailFaceIndex,
    bool ExactUnderlyingFoundationIdentified,
    bool NoHigherNativeLowDetailFace,
    bool NoHigherNativeHighDetailFace,
    bool NoHigherNativeCollisionTriangle,
    bool AuthoredSurfaceTopmostOverOpenInterior,
    bool CloseHighDetailFarLowDetailRuntimeGuideSupported);

internal sealed record UnusedLevel65ConstructionCollidableTerrainPlan(
    string ProfileId,
    string BaselineImageSha256,
    UnusedLevel65ConstructionVisualTerrainPlan VisualTerrain,
    UnusedLevel65ConstructionCollisionBinding CollisionBinding,
    int CollisionTreeCapacityBytes,
    int CollisionTreeUsedBytes,
    int CollisionBlocksCapacityBytes,
    int CollisionBlocksUsedBytes,
    string BeforeCollisionSha256,
    string AfterCollisionSha256,
    string AfterModelSha256,
    byte[] AfterModelBytes,
    IReadOnlyList<UnusedLevel65ConstructionPreservedDegenerateBinding> PreservedDegenerateBindings,
    int NativeCollisionCellCount,
    int AuthoredCollisionCellCount,
    IReadOnlyList<UnusedLevel65ConstructionCollisionCell> ChangedCollisionCells,
    bool UnchangedCollisionCellSequencesPreserved,
    bool IntendedCollisionCellSequencesVerified,
    bool NativeCollisionOrderingPreserved,
    UnusedLevel65ConstructionExposureProof ExposureProof,
    int OcclusionGroupCount,
    int OcclusionAssignment,
    bool AssignedOcclusionGroupContainsSector,
    bool CollisionIndexRepacked,
    bool CollisionComposed,
    bool OcclusionOwnershipVerified,
    bool DisposableRuntimeCandidateAuthorized,
    bool Publishable,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

/// <summary>
/// Pins the exact V5 ID65 substrate and provides the first structural primitive
/// for full authoring: reserve aligned bytes in one native scene sector while
/// growing the environment component, updating every later sector pointer, and
/// relocating every following model component together into verified zero tail.
/// The reservation is deliberately not publishable until a later composer fills
/// it with valid HP/LP records and matching collision/occlusion changes.
/// </summary>
internal static class UnusedLevel65FullAuthoringConstructionTemplate
{
    public const string ProfileId =
        "unused-level-65-full-authoring-construction-template-clean-usa-research-v1";
    public const string BaselineImageSha256 =
        UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256;
    public const int WadLba = 37;
    public const int ExpectedWadByteLength = 0x6C18800;
    public const long DataEntryWadOffset = 0x6936800;
    public const int DataEntryByteLength = 0x2E2000;
    public const long ModelSubfileWadOffset = 0x6A15000;
    public const int ModelSubfileByteLength = 0x94800;
    public const long TextureComponentWadOffset = 0x6A15000;
    public const int TextureComponentByteLength = 0x2F78;
    public const long EnvironmentComponentWadOffset = 0x6A17F78;
    public const int EnvironmentComponentByteLength = 0x284A4;
    public const long OcclusionComponentWadOffset = 0x6A4041C;
    public const int OcclusionComponentByteLength = 0x974;
    public const long SpecialSurfaceComponentWadOffset = 0x6A40D90;
    public const int SpecialSurfaceComponentByteLength = 0x30;
    public const long CollisionComponentWadOffset = 0x6A40DC0;
    public const int CollisionComponentByteLength = 0x5FAE8;
    public const long CycloramaComponentWadOffset = 0x6AA08A8;
    public const int CycloramaComponentByteLength = 0x84E4;
    public const string CycloramaComponentSha256 =
        "8e8c62273ab0d4ea691a40409d5e4be77fe578cfebcfb2e3c6bb374cad7d40bb";
    public const long PortalTableWadOffset = 0x6AA8D8C;
    public const int PortalTableByteLength = 4;
    public const long ParticleComponentWadOffset = 0x6AA8D90;
    public const int ParticleComponentByteLength = 0x80;
    public const long SoundComponentWadOffset = 0x6AA8E10;
    public const int SoundComponentByteLength = 0x6F8;
    public const long UsedModelEndWadOffset = 0x6AA9508;
    public const int VerifiedZeroTailBytes = 0x2F8;
    public const long ObjectTableWadOffset = 0x6B06970;
    public const int ObjectRecordCount = 107;
    public const long LandingWadOffset = 0x6B06800;
    public const int LandingByteLength = 0x10;
    public const string LandingHex = "29E901009A8801006621000000004000";
    public const string LandingSha256 =
        "ac6446f7f11382b1f5d9c4f73c17281cda8db6251b14ace4e237c3c3d9f1896e";
    public const int PlayerAnchorTrueIndex = 92;
    public const long PlayerAnchorWadOffset = 0x6B08910;
    public const int PlayerAnchorByteLength = 0x58;
    public const string PlayerAnchorSha256 =
        "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2";
    public const int NativeTextureRecordCount = 66;
    public const int SceneSectorCount = 216;
    public const int TotalLowDetailVertexCount = 2_853;
    public const int TotalLowDetailFaceCount = 1_437;
    public const int TotalHighDetailVertexCount = 5_863;
    public const int TotalHighDetailFaceCount = 3_887;
    public const int SpecialSurfaceCount = 3;
    public const int CollisionTriangleCount = 19_808;
    public const int PortalCount = 0;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const long NamePointerTableFileOffset = 0x5FFF0;
    private const int DisplayNameSlotIndex = 35;
    private const long DisplayNamePointerFileOffset =
        NamePointerTableFileOffset + (DisplayNameSlotIndex * sizeof(uint));
    private const uint ExecutableLoadAddress = 0x80010000;
    private const int PsxExeHeaderByteLength = 0x800;
    private const int TownSquareStringFileOffset = 0x9E4;
    private static readonly byte[] TownSquareNamePointer = Convert.FromHexString("E4010180");
    private static readonly byte[] TownSquareNameBytes = Convert.FromHexString("544F574E2053515541524500");
    private const int CollisionFlagsByteLength = 0x2904;
    private const int CollisionBlockTreeRelativeOffset = 0x1C;
    private const int CollisionBlocksRelativeOffset = 0x6A7C;
    private const int CollisionTrianglesRelativeOffset = 0x1E400;
    private const int CollisionAssignmentsRelativeOffset = 0x58480;
    private const int CollisionFlagsRelativeOffset = 0x5D1E0;
    private const int ConstructionCollisionTriangleIndex = 13_995;
    private static readonly (int RelativeOffset, int ByteLength)[] ExpectedDataSubfiles =
    [
        (0x000800, 0x0DE000),
        (0x0DE800, 0x094800),
        (0x173000, 0x05D000),
        (0x1D0000, 0x008800),
        (0x1D8800, 0x049800),
        (0x222000, 0x042800),
        (0x264800, 0x05D000),
        (0x2C1800, 0x020800)
    ];

    public static async Task<UnusedLevel65FullAuthoringConstructionContract> InspectAsync(
        string baselineImagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baselineImagePath) || !File.Exists(baselineImagePath))
            throw new FileNotFoundException("The exact ID65 full-authoring baseline BIN is missing.", baselineImagePath);

        string imagePath = Path.GetFullPath(baselineImagePath);
        string imageHash = await HashFileAsync(imagePath, cancellationToken);
        if (!string.Equals(imageHash, BaselineImageSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The full-authoring construction contract accepts only ID65 baseline {BaselineImageSha256}; selected SHA-256 was {imageHash}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The ID65 full-authoring baseline is not a MODE2/2352 image.");

        using FileStream image = File.OpenRead(imagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
        {
            throw new InvalidDataException(
                $"The ID65 full-authoring baseline maps WAD.WAD to LBA {wad.Lba}/0x{wad.Size:X}, expected LBA {WadLba}/0x{ExpectedWadByteLength:X}.");
        }

        byte[] entryRow = DiscImage.ReadFileBytes(image, layout, wad.Lba, 80L * 8, 8);
        RequireUInt32(entryRow, 0, DataEntryWadOffset, "row-80 data offset");
        RequireUInt32(entryRow, 4, DataEntryByteLength, "row-80 data length");
        byte[] entryHeader = DiscImage.ReadFileBytes(image, layout, wad.Lba, DataEntryWadOffset, 0x40);
        RequireUInt32(entryHeader, 0x08, ModelSubfileWadOffset - DataEntryWadOffset, "row-80 model relative offset");
        RequireUInt32(entryHeader, 0x0C, ModelSubfileByteLength, "row-80 model length");

        List<UnusedLevel65ConstructionDataSubfileLayout> dataSubfiles = new(ExpectedDataSubfiles.Length);
        int expectedCursor = 0x800;
        for (int index = 0; index < ExpectedDataSubfiles.Length; index++)
        {
            (int expectedRelativeOffset, int expectedByteLength) = ExpectedDataSubfiles[index];
            if (expectedRelativeOffset != expectedCursor)
                throw new InvalidDataException($"The pinned row-80 subfile {index} layout is not contiguous.");
            RequireUInt32(entryHeader, index * 8, expectedRelativeOffset, $"row-80 subfile {index} relative offset");
            RequireUInt32(entryHeader, (index * 8) + 4, expectedByteLength, $"row-80 subfile {index} length");
            byte[] subfileBytes = DiscImage.ReadFileBytes(
                image,
                layout,
                wad.Lba,
                DataEntryWadOffset + expectedRelativeOffset,
                expectedByteLength);
            dataSubfiles.Add(new(
                index,
                expectedRelativeOffset,
                DataEntryWadOffset + expectedRelativeOffset,
                expectedByteLength,
                Hash(subfileBytes),
                MustRemainByteIdenticalInFirstTerrainGate: index != 1));
            expectedCursor = checked(expectedRelativeOffset + expectedByteLength);
        }
        if (expectedCursor != DataEntryByteLength)
            throw new InvalidDataException("The eight pinned row-80 subfiles do not end at the exact entry boundary.");

        byte[] model = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            ModelSubfileWadOffset,
            ModelSubfileByteLength);
        ParsedModel parsed = ParseModel(model, ModelSubfileWadOffset);
        RequireExpectedBaseline(parsed);

        byte[] landingBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            LandingWadOffset,
            LandingByteLength);
        string landingHash = Hash(landingBytes);
        if (!landingBytes.SequenceEqual(Convert.FromHexString(LandingHex)) ||
            !string.Equals(landingHash, LandingSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The exact ID65 fly-in landing record changed.");
        }
        UnusedLevel65ConstructionLandingInvariant landing = new(
            LandingWadOffset,
            LandingByteLength,
            Convert.ToHexString(landingBytes),
            landingHash,
            ReadInt32(landingBytes, 0x00),
            ReadInt32(landingBytes, 0x04),
            ReadInt32(landingBytes, 0x08),
            landingBytes[0x0E]);
        if (landing.RawX != 125_225 || landing.RawY != 100_506 || landing.RawZ != 8_550 || landing.YawByte != 64)
            throw new InvalidDataException("The exact ID65 fly-in landing coordinates or heading changed.");

        byte[] objectCountBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            ObjectTableWadOffset - 4,
            4);
        RequireUInt32(objectCountBytes, 0, ObjectRecordCount, "row-80 object count");
        byte[] objectTableBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            ObjectTableWadOffset,
            checked(ObjectRecordCount * 0x58));

        int playerAnchorRelativeOffset = checked(PlayerAnchorTrueIndex * PlayerAnchorByteLength);
        if (ObjectTableWadOffset + playerAnchorRelativeOffset != PlayerAnchorWadOffset)
            throw new InvalidDataException("The ID65 T92 player-anchor row no longer resolves from the pinned object table.");
        byte[] playerAnchorBytes = objectTableBytes.AsSpan(
            playerAnchorRelativeOffset,
            PlayerAnchorByteLength).ToArray();
        string playerAnchorHash = Hash(playerAnchorBytes);
        if (!string.Equals(playerAnchorHash, PlayerAnchorSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The exact ID65 T92 player-anchor row changed.");
        UnusedLevel65ConstructionPlayerAnchorInvariant playerAnchor = new(
            PlayerAnchorTrueIndex,
            PlayerAnchorWadOffset,
            PlayerAnchorByteLength,
            playerAnchorHash,
            ReadInt32(playerAnchorBytes, 0x0C),
            ReadInt32(playerAnchorBytes, 0x10),
            ReadInt32(playerAnchorBytes, 0x14));
        if (playerAnchor.RawX != 125_225 ||
            playerAnchor.RawY != 100_506 ||
            playerAnchor.RawZ != 8_704 ||
            playerAnchor.RawX != landing.RawX ||
            playerAnchor.RawY != landing.RawY ||
            playerAnchor.RawZ - landing.RawZ != 154)
        {
            throw new InvalidDataException("The ID65 fly-in landing and T92 player-anchor spatial invariant changed.");
        }

        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The exact ID65 relocated executable extent changed.");
        byte[] displayNamePointer = DiscImage.ReadFileBytes(
            image,
            layout,
            executable.Lba,
            DisplayNamePointerFileOffset,
            sizeof(uint));
        if (!displayNamePointer.SequenceEqual(TownSquareNamePointer))
            throw new InvalidDataException("The exact ID65 slot-35 display-name pointer changed.");
        uint displayNameAddress = BinaryPrimitives.ReadUInt32LittleEndian(displayNamePointer);
        if (displayNameAddress < ExecutableLoadAddress)
            throw new InvalidDataException("The ID65 slot-35 display-name pointer precedes the executable load address.");
        int resolvedStringFileOffset = checked((int)(displayNameAddress - ExecutableLoadAddress + PsxExeHeaderByteLength));
        if (resolvedStringFileOffset != TownSquareStringFileOffset)
            throw new InvalidDataException("The ID65 slot-35 display-name pointer no longer resolves to the pinned Town Square string offset.");
        byte[] displayNameBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            executable.Lba,
            resolvedStringFileOffset,
            TownSquareNameBytes.Length);
        if (!displayNameBytes.SequenceEqual(TownSquareNameBytes))
            throw new InvalidDataException("The ID65 slot-35 display-name pointer no longer resolves to TOWN SQUARE.");
        UnusedLevel65ConstructionDisplayNameInvariant displayName = new(
            executable.Name,
            executable.Lba,
            executable.Size,
            NamePointerTableFileOffset,
            DisplayNameSlotIndex,
            DisplayNamePointerFileOffset,
            Convert.ToHexString(displayNamePointer),
            resolvedStringFileOffset,
            "TOWN SQUARE");

        return ToContract(wad.Size, parsed, dataSubfiles, landing, playerAnchor, displayName);
    }

    public static async Task<UnusedLevel65SceneSectorCapacityPlan> BuildSectorCapacityPlanAsync(
        string baselineImagePath,
        int targetSectorIndex,
        int reserveByteCount,
        CancellationToken cancellationToken = default)
    {
        UnusedLevel65FullAuthoringConstructionContract contract =
            await InspectAsync(baselineImagePath, cancellationToken);
        if (targetSectorIndex < 0 || targetSectorIndex >= contract.SceneSectors.Count)
            throw new ArgumentOutOfRangeException(nameof(targetSectorIndex));
        if (reserveByteCount <= 0 || (reserveByteCount & 3) != 0)
            throw new ArgumentOutOfRangeException(nameof(reserveByteCount), "Scene-sector capacity must be a positive four-byte-aligned size.");
        if (reserveByteCount > contract.VerifiedZeroTailBytes)
        {
            throw new InvalidDataException(
                $"Scene-sector capacity needs {reserveByteCount} bytes, but ID65 has only {contract.VerifiedZeroTailBytes} verified model-tail bytes.");
        }

        string imagePath = Path.GetFullPath(baselineImagePath);
        DiscLayout discLayout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        byte[] before = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            ModelSubfileWadOffset,
            ModelSubfileByteLength);
        ParsedModel source = ParseModel(before, ModelSubfileWadOffset);
        RequireExpectedBaseline(source);

        ParsedSector target = source.Sectors[targetSectorIndex];
        int insertion = checked((int)((target.WadOffset + target.ByteLength) - ModelSubfileWadOffset));
        int oldUsedEnd = checked((int)(source.UsedEndWadOffset - ModelSubfileWadOffset));
        if (insertion <= 0 || insertion > oldUsedEnd)
            throw new InvalidDataException("The requested ID65 scene-sector reservation resolves outside the used model data.");

        byte[] after = new byte[before.Length];
        Array.Copy(before, 0, after, 0, insertion);
        Array.Copy(before, insertion, after, insertion + reserveByteCount, oldUsedEnd - insertion);

        int environmentOffset = checked((int)(EnvironmentComponentWadOffset - ModelSubfileWadOffset));
        WriteInt32(after, environmentOffset, checked(source.Environment.ByteLength + reserveByteCount));
        for (int index = targetSectorIndex + 1; index < source.Sectors.Count; index++)
        {
            int pointerOffset = checked(environmentOffset + 8 + (index * 4));
            int pointer = ReadInt32(before, pointerOffset);
            WriteInt32(after, pointerOffset, checked(pointer + reserveByteCount));
        }

        ParsedModel reserved = ParseModel(after, ModelSubfileWadOffset);
        if (reserved.UsedEndWadOffset != source.UsedEndWadOffset + reserveByteCount ||
            reserved.ZeroTailBytes != source.ZeroTailBytes - reserveByteCount ||
            reserved.Environment.ByteLength != source.Environment.ByteLength + reserveByteCount ||
            reserved.Sectors[targetSectorIndex].GapBytesAfter != reserveByteCount)
        {
            throw new InvalidDataException("The ID65 component-aware scene-sector reservation did not reparse to its exact predicted layout.");
        }

        for (int index = 0; index < source.Sectors.Count; index++)
        {
            long expectedOffset = index <= targetSectorIndex
                ? source.Sectors[index].WadOffset
                : source.Sectors[index].WadOffset + reserveByteCount;
            if (reserved.Sectors[index].WadOffset != expectedOffset ||
                reserved.Sectors[index].ByteLength != source.Sectors[index].ByteLength)
            {
                throw new InvalidDataException($"Scene sector {index} did not relocate by the exact component-aware delta.");
            }
        }

        string[] relocatedNames = ["occlusion", "special-surface", "collision", "cyclorama", "portal-table", "particles", "sound"];
        List<UnusedLevel65ConstructionComponentRelocation> relocations = [];
        bool contentsPreserved = true;
        foreach (string name in relocatedNames)
        {
            ParsedComponent oldComponent = source.Components.Single(component => component.Name == name);
            ParsedComponent newComponent = reserved.Components.Single(component => component.Name == name);
            if (newComponent.WadOffset != oldComponent.WadOffset + reserveByteCount ||
                newComponent.ByteLength != oldComponent.ByteLength ||
                !string.Equals(newComponent.Sha256, oldComponent.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                contentsPreserved = false;
            }
            relocations.Add(new(
                name,
                oldComponent.WadOffset,
                newComponent.WadOffset,
                oldComponent.ByteLength,
                oldComponent.Sha256));
        }
        if (!contentsPreserved)
            throw new InvalidDataException("A following ID65 model component changed while reserving scene-sector capacity.");

        if (after.AsSpan(insertion, reserveByteCount).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The reserved ID65 scene-sector capacity is not zero-initialized.");
        if (before.AsSpan(oldUsedEnd).IndexOfAnyExcept((byte)0) >= 0 ||
            after.AsSpan(oldUsedEnd + reserveByteCount).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("ID65 model-tail capacity is not bounded by exact zero bytes.");
        }

        return new UnusedLevel65SceneSectorCapacityPlan(
            ProfileId,
            BaselineImageSha256,
            targetSectorIndex,
            reserveByteCount,
            ModelSubfileWadOffset + insertion,
            source.Environment.ByteLength,
            reserved.Environment.ByteLength,
            source.UsedEndWadOffset,
            reserved.UsedEndWadOffset,
            source.ZeroTailBytes,
            reserved.ZeroTailBytes,
            Hash(before),
            Hash(after),
            before,
            after,
            relocations,
            reserved.Sectors.Select(ToPublicSector).ToArray(),
            ComponentContentsPreserved: true,
            RetailDataTouched: false,
            Publishable: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    public static async Task<UnusedLevel65ConstructionVisualTerrainPlan> BuildFirstVisualTerrainPlanAsync(
        string baselineImagePath,
        CancellationToken cancellationToken = default)
    {
        const int sectorIndex = 213;
        const int lpVertexGrowth = 3;
        const int lpFaceGrowth = 1;
        const int hpVertexGrowth = 3;
        const int hpFaceGrowth = 1;
        const int growthBytes = (lpVertexGrowth * 4) + (lpFaceGrowth * 8) +
                                (hpVertexGrowth * 4) + (hpFaceGrowth * 16);
        UnusedLevel65SceneSectorCapacityPlan capacity =
            await BuildSectorCapacityPlanAsync(
                baselineImagePath,
                sectorIndex,
                growthBytes,
                cancellationToken);
        byte[] beforeModel = capacity.BeforeModelBytes;
        ParsedModel beforeLayout = ParseModel(beforeModel, ModelSubfileWadOffset);
        RequireExpectedBaseline(beforeLayout);
        ParsedSector sourceSector = beforeLayout.Sectors[sectorIndex];
        int sectorOffset = checked((int)(sourceSector.WadOffset - ModelSubfileWadOffset));
        byte[] sourceSectorBytes = beforeModel.AsSpan(sectorOffset, sourceSector.ByteLength).ToArray();

        byte lpVertices = sourceSectorBytes[16];
        byte lpColours = sourceSectorBytes[17];
        byte lpFaces = sourceSectorBytes[18];
        byte hpVertices = sourceSectorBytes[20];
        byte hpColours = sourceSectorBytes[21];
        byte hpFaces = sourceSectorBytes[22];
        if (lpVertices != 35 || lpColours != 48 || lpFaces != 21 ||
            hpVertices != 140 || hpColours != 185 || hpFaces != 113)
        {
            throw new InvalidDataException("The sector-213 HP/LP source counts changed before first visual terrain allocation.");
        }
        if (lpVertices + lpVertexGrowth > 63 || hpVertices + hpVertexGrowth > 255 ||
            lpFaces + lpFaceGrowth > 255 || hpFaces + hpFaceGrowth > 255)
        {
            throw new InvalidDataException("The first ID65 visual terrain allocation exceeds native sector index/count capacity.");
        }

        UnusedLevel65ConstructionPoint[] points =
        [
            new(7762, 6346, 512),
            new(7890, 6346, 512),
            new(7826, 6474, 640)
        ];
        uint[] vertexWords = points.Select(point => EncodeSceneVertex(sourceSectorBytes, point)).ToArray();
        if (!vertexWords.SequenceEqual(new uint[] { 0x29E5D820, 0x39E5D820, 0x31E7D8A0 }))
            throw new InvalidDataException("The first ID65 terrain triangle no longer encodes to its exact sector-213 vertex words.");

        int sourceLpVertexStart = 28;
        int sourceLpColorStart = checked(sourceLpVertexStart + (lpVertices * 4));
        int sourceLpFaceStart = checked(sourceLpColorStart + (lpColours * 4));
        int sourceHpVertexStart = checked(sourceLpFaceStart + (lpFaces * 8));
        int sourceHpColorStart = checked(sourceHpVertexStart + (hpVertices * 4));
        int sourceHpFaceStart = checked(sourceHpColorStart + (hpColours * 8));
        if (sourceHpFaceStart + (hpFaces * 16) != sourceSectorBytes.Length)
            throw new InvalidDataException("The sector-213 source tables do not end at the exact native sector boundary.");

        byte[] sourceLpFace = sourceSectorBytes.AsSpan(sourceLpFaceStart, 8).ToArray();
        byte[] sourceHpFace = sourceSectorBytes.AsSpan(sourceHpFaceStart, 16).ToArray();
        if (!sourceLpFace.SequenceEqual(Convert.FromHexString("0082100000821000")) ||
            !sourceHpFace.SequenceEqual(Convert.FromHexString("010100020101000219024001080A1000")))
        {
            throw new InvalidDataException("The native LP/HP material donors for the first ID65 terrain tile changed.");
        }

        byte[] newLpFace = sourceLpFace.ToArray();
        uint lpWord = BinaryPrimitives.ReadUInt32LittleEndian(newLpFace.AsSpan(0, 4));
        uint lpFlags = lpWord & 0xFFu;
        uint newLpWord = ((uint)lpVertices << 26) |
                         ((uint)(lpVertices + 1) << 20) |
                         ((uint)(lpVertices + 2) << 14) |
                         ((uint)(lpVertices + 2) << 8) |
                         lpFlags;
        BinaryPrimitives.WriteUInt32LittleEndian(newLpFace.AsSpan(0, 4), newLpWord);
        byte[] newHpFace = sourceHpFace.ToArray();
        newHpFace[0] = hpVertices;
        newHpFace[1] = hpVertices;
        newHpFace[2] = checked((byte)(hpVertices + 1));
        newHpFace[3] = checked((byte)(hpVertices + 2));

        byte[] newSector = new byte[sourceSectorBytes.Length + growthBytes];
        sourceSectorBytes.AsSpan(0, 28).CopyTo(newSector);
        newSector[16] = checked((byte)(lpVertices + lpVertexGrowth));
        newSector[18] = checked((byte)(lpFaces + lpFaceGrowth));
        newSector[20] = checked((byte)(hpVertices + hpVertexGrowth));
        newSector[22] = checked((byte)(hpFaces + hpFaceGrowth));
        int output = 28;
        sourceSectorBytes.AsSpan(sourceLpVertexStart, lpVertices * 4).CopyTo(newSector.AsSpan(output));
        output += lpVertices * 4;
        WriteVertexWords(newSector, ref output, vertexWords);
        sourceSectorBytes.AsSpan(sourceLpColorStart, lpColours * 4).CopyTo(newSector.AsSpan(output));
        output += lpColours * 4;
        sourceSectorBytes.AsSpan(sourceLpFaceStart, lpFaces * 8).CopyTo(newSector.AsSpan(output));
        output += lpFaces * 8;
        newLpFace.CopyTo(newSector, output);
        output += newLpFace.Length;
        sourceSectorBytes.AsSpan(sourceHpVertexStart, hpVertices * 4).CopyTo(newSector.AsSpan(output));
        output += hpVertices * 4;
        WriteVertexWords(newSector, ref output, vertexWords);
        sourceSectorBytes.AsSpan(sourceHpColorStart, hpColours * 8).CopyTo(newSector.AsSpan(output));
        output += hpColours * 8;
        sourceSectorBytes.AsSpan(sourceHpFaceStart, hpFaces * 16).CopyTo(newSector.AsSpan(output));
        output += hpFaces * 16;
        newHpFace.CopyTo(newSector, output);
        output += newHpFace.Length;
        if (output != newSector.Length)
            throw new InvalidDataException("The first ID65 HP/LP terrain allocation did not consume its exact sector capacity.");

        byte[] afterModel = capacity.AfterModelBytes.ToArray();
        newSector.CopyTo(afterModel, sectorOffset);
        ParsedModel afterLayout = ParseModel(afterModel, ModelSubfileWadOffset);
        ParsedSector authoredSector = afterLayout.Sectors[sectorIndex];
        if (authoredSector.GapBytesAfter != 0 ||
            authoredSector.ByteLength != sourceSector.ByteLength + growthBytes ||
            authoredSector.LowDetailVertexCount != lpVertices + lpVertexGrowth ||
            authoredSector.LowDetailFaceCount != lpFaces + lpFaceGrowth ||
            authoredSector.HighDetailVertexCount != hpVertices + hpVertexGrowth ||
            authoredSector.HighDetailFaceCount != hpFaces + hpFaceGrowth ||
            afterLayout.Sectors.Sum(sector => sector.LowDetailVertexCount) != TotalLowDetailVertexCount + lpVertexGrowth ||
            afterLayout.Sectors.Sum(sector => sector.LowDetailFaceCount) != TotalLowDetailFaceCount + lpFaceGrowth ||
            afterLayout.Sectors.Sum(sector => sector.HighDetailVertexCount) != TotalHighDetailVertexCount + hpVertexGrowth ||
            afterLayout.Sectors.Sum(sector => sector.HighDetailFaceCount) != TotalHighDetailFaceCount + hpFaceGrowth)
        {
            throw new InvalidDataException("The first ID65 HP/LP terrain allocation did not reparse with its exact new counts.");
        }

        byte[] authoredSectorBytes = afterModel.AsSpan(sectorOffset, authoredSector.ByteLength).ToArray();
        int authoredLpVertexStart = 28;
        for (int index = 0; index < vertexWords.Length; index++)
        {
            uint lpReadback = BinaryPrimitives.ReadUInt32LittleEndian(
                authoredSectorBytes.AsSpan(authoredLpVertexStart + ((lpVertices + index) * 4), 4));
            if (lpReadback != vertexWords[index])
                throw new InvalidDataException($"The appended LP construction vertex {index} failed exact word readback.");
        }
        int authoredLpColorStart = checked(authoredLpVertexStart + ((lpVertices + lpVertexGrowth) * 4));
        int authoredLpFaceStart = checked(authoredLpColorStart + (lpColours * 4));
        byte[] lpFaceReadback = authoredSectorBytes.AsSpan(
            authoredLpFaceStart + (lpFaces * 8),
            8).ToArray();
        int[] lpSlots = ReadPackedSixBitSlots(BinaryPrimitives.ReadUInt32LittleEndian(lpFaceReadback.AsSpan(0, 4)));
        int[] lpColorSlots = ReadPackedSixBitSlots(BinaryPrimitives.ReadUInt32LittleEndian(lpFaceReadback.AsSpan(4, 4)));
        if (!lpFaceReadback.SequenceEqual(newLpFace) ||
            !lpSlots.SequenceEqual(new[] { 35, 36, 37, 37 }) ||
            lpColorSlots.Any(index => index < 0 || index >= lpColours))
        {
            throw new InvalidDataException("The appended LP construction face failed exact slot/color readback.");
        }

        int authoredHpVertexStart = checked(authoredLpFaceStart + ((lpFaces + lpFaceGrowth) * 8));
        for (int index = 0; index < vertexWords.Length; index++)
        {
            uint hpReadback = BinaryPrimitives.ReadUInt32LittleEndian(
                authoredSectorBytes.AsSpan(authoredHpVertexStart + ((hpVertices + index) * 4), 4));
            if (hpReadback != vertexWords[index])
                throw new InvalidDataException($"The appended HP construction vertex {index} failed exact word readback.");
        }
        int authoredHpColorStart = checked(authoredHpVertexStart + ((hpVertices + hpVertexGrowth) * 4));
        int authoredHpFaceStart = checked(authoredHpColorStart + (hpColours * 8));
        byte[] hpFaceReadback = authoredSectorBytes.AsSpan(
            authoredHpFaceStart + (hpFaces * 16),
            16).ToArray();
        if (!hpFaceReadback.SequenceEqual(newHpFace) ||
            !hpFaceReadback.AsSpan(0, 4).SequenceEqual(new byte[] { 140, 140, 141, 142 }) ||
            hpFaceReadback.AsSpan(4, 4).ToArray().Any(index => index >= hpColours) ||
            hpFaceReadback[8] != 25)
        {
            throw new InvalidDataException("The appended HP construction face failed exact slot/color/material readback.");
        }

        ParsedComponent oldOcclusion = beforeLayout.Components.Single(component => component.Name == "occlusion");
        ParsedComponent newOcclusion = afterLayout.Components.Single(component => component.Name == "occlusion");
        ParsedComponent oldCollision = beforeLayout.Components.Single(component => component.Name == "collision");
        ParsedComponent newCollision = afterLayout.Components.Single(component => component.Name == "collision");
        bool occlusionPreserved = newOcclusion.WadOffset == oldOcclusion.WadOffset + growthBytes &&
                                  newOcclusion.Sha256 == oldOcclusion.Sha256;
        bool cullBoundsPreserved = points.All(point => PointInsideSectorCullSphere(sourceSectorBytes, point));
        if (!occlusionPreserved || !cullBoundsPreserved ||
            newCollision.WadOffset != oldCollision.WadOffset + growthBytes ||
            newCollision.Sha256 != oldCollision.Sha256)
        {
            throw new InvalidDataException("The first ID65 visual terrain allocation escaped its existing cull container or changed following components.");
        }

        return new(
            ProfileId,
            BaselineImageSha256,
            sectorIndex,
            lpVertexGrowth,
            lpFaceGrowth,
            hpVertexGrowth,
            hpFaceGrowth,
            points,
            sourceSector.WadOffset,
            sourceSector.ByteLength,
            authoredSector.ByteLength,
            oldOcclusion.WadOffset,
            newOcclusion.WadOffset,
            oldCollision.WadOffset,
            newCollision.WadOffset,
            afterLayout.ZeroTailBytes,
            Hash(beforeModel),
            Hash(afterModel),
            afterModel,
            HighAndLowDetailAllocated: true,
            SectorCullBoundsPreserved: true,
            OcclusionContainerPreserved: true,
            CollisionComposed: false,
            Publishable: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    public static async Task<UnusedLevel65ConstructionCollidableTerrainPlan> BuildFirstCollidableTerrainPlanAsync(
        string baselineImagePath,
        CancellationToken cancellationToken = default)
    {
        UnusedLevel65ConstructionVisualTerrainPlan visual =
            await BuildFirstVisualTerrainPlanAsync(baselineImagePath, cancellationToken);
        string imagePath = Path.GetFullPath(baselineImagePath);
        DiscLayout discLayout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        byte[] beforeModel = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            ModelSubfileWadOffset,
            ModelSubfileByteLength);
        ParsedModel beforeLayout = ParseModel(beforeModel, ModelSubfileWadOffset);
        RequireExpectedBaseline(beforeLayout);

        byte[] afterModel = visual.AfterModelBytes.ToArray();
        ParsedModel visualLayout = ParseModel(afterModel, ModelSubfileWadOffset);
        ParsedComponent beforeCollision = beforeLayout.Components.Single(component => component.Name == "collision");
        ParsedComponent visualCollision = visualLayout.Components.Single(component => component.Name == "collision");
        if (visualCollision.WadOffset != beforeCollision.WadOffset + 48 ||
            visualCollision.ByteLength != beforeCollision.ByteLength ||
            visualCollision.Sha256 != beforeCollision.Sha256)
        {
            throw new InvalidDataException("The HP/LP construction tile did not relocate the native collision component byte-for-byte before composition.");
        }

        int beforeCollisionOffset = checked((int)(beforeCollision.WadOffset - ModelSubfileWadOffset));
        int visualCollisionOffset = checked((int)(visualCollision.WadOffset - ModelSubfileWadOffset));
        byte[] sourceCollisionBytes = beforeModel.AsSpan(beforeCollisionOffset, beforeCollision.ByteLength).ToArray();
        byte[] uncomposedCollisionBytes = afterModel.AsSpan(visualCollisionOffset, visualCollision.ByteLength).ToArray();
        if (!sourceCollisionBytes.SequenceEqual(uncomposedCollisionBytes))
            throw new InvalidDataException("The relocated construction collision component does not match its exact source bytes.");

        ParsedCollisionLayout sourceNative = ParseCollisionLayout(beforeModel, beforeCollisionOffset, beforeCollision.WadOffset);
        ParsedCollisionLayout authoredNative = ParseCollisionLayout(afterModel, visualCollisionOffset, visualCollision.WadOffset);
        int[] zeroAreaIndexes = Enumerable.Range(0, sourceNative.TriangleCount)
            .Where(index => IsZeroAreaCollisionTriangle(
                DecodeCollisionTriangle(
                    beforeModel.AsSpan(sourceNative.TriangleTableOffset + (index * 12), 12),
                    index)))
            .ToArray();
        if (!zeroAreaIndexes.SequenceEqual(new[] { 1_295, 1_298, 11_184, ConstructionCollisionTriangleIndex }))
        {
            throw new InvalidDataException(
                $"The native collision table's exact zero-area inventory changed: [{string.Join(',', zeroAreaIndexes)}].");
        }

        int sourceTriangleOffset = checked(sourceNative.TriangleTableOffset + (ConstructionCollisionTriangleIndex * 12));
        int authoredTriangleOffset = checked(authoredNative.TriangleTableOffset + (ConstructionCollisionTriangleIndex * 12));
        byte[] sourceTriangleBytes = beforeModel.AsSpan(sourceTriangleOffset, 12).ToArray();
        byte[] expectedSourceTriangle = Convert.FromHexString("A7A2140044631900E0010000");
        if (!sourceTriangleBytes.SequenceEqual(expectedSourceTriangle))
            throw new InvalidDataException("The selected native zero-area collision slot changed before construction composition.");
        DecodedCollisionTriangle sourceTriangle = DecodeCollisionTriangle(sourceTriangleBytes, ConstructionCollisionTriangleIndex);
        UnusedLevel65ConstructionPoint[] sourcePoints = sourceTriangle.Points.ToArray();
        if (!sourcePoints.SequenceEqual(new[]
            {
                new UnusedLevel65ConstructionPoint(8871, 9028, 480),
                new UnusedLevel65ConstructionPoint(8953, 9129, 480),
                new UnusedLevel65ConstructionPoint(8871, 9028, 480)
            }) ||
            sourceTriangle.ZFlags != 0 ||
            !IsZeroAreaCollisionTriangle(sourceTriangle))
        {
            throw new InvalidDataException("The selected native collision slot is no longer the exact assignment-255 zero-area placeholder.");
        }

        int sourceAssignmentOffset = checked(sourceNative.AssignmentsOffset + ConstructionCollisionTriangleIndex);
        int authoredAssignmentOffset = checked(authoredNative.AssignmentsOffset + ConstructionCollisionTriangleIndex);
        int sourceAssignment = beforeModel[sourceAssignmentOffset];
        if (sourceAssignment != 0xFF || afterModel[authoredAssignmentOffset] != 0xFF)
            throw new InvalidDataException("The selected zero-area collision slot no longer has its exact sentinel assignment 255.");
        int sourceLookupReferences = CountCollisionTriangleReferences(
            beforeModel.AsSpan(sourceNative.BlocksOffset, sourceNative.BlocksCapacityBytes),
            ConstructionCollisionTriangleIndex);
        if (sourceLookupReferences != 1)
        {
            throw new InvalidDataException(
                $"The selected zero-area collision slot has {sourceLookupReferences} native lookup references, expected one.");
        }

        UnusedLevel65ConstructionPoint[] targetPoints = visual.Points.ToArray();
        byte[] authoredTriangleBytes = EncodeCollisionTriangle(targetPoints, zFlags: 0);
        if (!authoredTriangleBytes.SequenceEqual(Convert.FromHexString("521E2020CA18004000020080")))
            throw new InvalidDataException("The first construction tile collision no longer encodes to its exact native words.");
        authoredTriangleBytes.CopyTo(afterModel, authoredTriangleOffset);
        afterModel[authoredAssignmentOffset] = 0;

        IReadOnlyDictionary<int, IReadOnlyList<UnusedLevel65ConstructionCollisionCell>> preservedNativeCells =
            new Dictionary<int, IReadOnlyList<UnusedLevel65ConstructionCollisionCell>>
            {
                [1_295] =
                [
                    new UnusedLevel65ConstructionCollisionCell(25, 25, 2),
                    new UnusedLevel65ConstructionCollisionCell(25, 26, 2)
                ],
                [1_298] = [new UnusedLevel65ConstructionCollisionCell(25, 25, 2)],
                [11_184] = [new UnusedLevel65ConstructionCollisionCell(34, 35, 1)]
            };
        UnusedLevel65ConstructionCollisionCell[] targetCells = CollisionTouchedCells(targetPoints).ToArray();
        if (!targetCells.SequenceEqual(new[]
            {
                new UnusedLevel65ConstructionCollisionCell(30, 24, 2),
                new UnusedLevel65ConstructionCollisionCell(30, 25, 2)
            }))
        {
            throw new InvalidDataException(
                $"The first construction triangle touches unexpected collision cells: {string.Join(',', targetCells)}.");
        }

        UnusedLevel65ConstructionExposureProof exposureProof = AnalyzeConstructionExposure(
            beforeModel,
            beforeLayout,
            sourceNative,
            targetPoints);

        NativeCollisionIndexSnapshot nativeIndex = DecodeNativeCollisionIndex(
            beforeModel.AsSpan(sourceNative.TreeOffset, sourceNative.TreeCapacityBytes),
            beforeModel.AsSpan(sourceNative.BlocksOffset, sourceNative.BlocksCapacityBytes),
            sourceNative.BlocksCapacityBytes,
            sourceNative.TriangleCount);
        if (nativeIndex.Cells.Count != 4_252 ||
            nativeIndex.GroupStartCount != 4_253 ||
            nativeIndex.TerminalSentinelWordOffset != (sourceNative.BlocksCapacityBytes / 2) - 1 ||
            Hash(nativeIndex.TreeBytes) != "ad6ce785e5d49ff97c5fb79c967f2d0bcff2f2e2d40ef1137cdce0114e4ea6ed" ||
            Hash(nativeIndex.BlockBytes) != "c2e0d178d39cd8ce8439d44d64987c81e60c93e0eb50f43c4a7ce51a258250d5")
        {
            throw new InvalidDataException("The exact native collision lookup substrate changed before ordered repacking.");
        }

        NativeCollisionIndexRepack repackedIndex = RepackConstructionCollisionIndex(
            nativeIndex,
            targetCells,
            ConstructionCollisionTriangleIndex,
            sourceNative.TriangleCount);
        repackedIndex.TreeBytes.CopyTo(afterModel, authoredNative.TreeOffset);
        repackedIndex.BlockBytes.CopyTo(afterModel, authoredNative.BlocksOffset);

        int[] lookupByteOffsets = FindCollisionTriangleReferenceByteOffsets(
            repackedIndex.BlockBytes,
            ConstructionCollisionTriangleIndex);
        if (lookupByteOffsets.Length != targetCells.Length)
            throw new InvalidDataException("The ordered collision repack did not emit exactly one physical reference per authored target cell.");
        long[] lookupWadOffsets = lookupByteOffsets
            .Select(offset => authoredNative.BlocksWadOffset + offset)
            .ToArray();
        UnusedLevel65ConstructionCollisionCell[] rebuiltTargetCells = FindCollisionIndexCells(
            repackedIndex.AuthoredIndex,
            ConstructionCollisionTriangleIndex);
        if (!rebuiltTargetCells.SequenceEqual(targetCells) || rebuiltTargetCells.Contains(new(34, 35, 1)))
        {
            throw new InvalidDataException(
                $"The repacked construction triangle lookup cells are [{string.Join(',', rebuiltTargetCells)}], expected only its authored footprint.");
        }

        UnusedLevel65ConstructionPreservedDegenerateBinding[] preservedDegenerateBindings =
            BuildPreservedDegenerateBindings(
                beforeModel,
                afterModel,
                sourceNative,
                authoredNative,
                repackedIndex.AuthoredIndex,
                preservedNativeCells);

        ParsedModel composedLayout = ParseModel(afterModel, ModelSubfileWadOffset);
        ParsedComponent composedCollision = composedLayout.Components.Single(component => component.Name == "collision");
        ParsedCollisionLayout composedNative = ParseCollisionLayout(afterModel, visualCollisionOffset, visualCollision.WadOffset);
        if (composedCollision.WadOffset != visualCollision.WadOffset ||
            composedCollision.ByteLength != visualCollision.ByteLength ||
            composedNative.TriangleCount != CollisionTriangleCount)
        {
            throw new InvalidDataException("Collision composition changed the fixed native component/table layout.");
        }

        NativeCollisionIndexSnapshot readbackIndex = DecodeNativeCollisionIndex(
            afterModel.AsSpan(composedNative.TreeOffset, composedNative.TreeCapacityBytes),
            afterModel.AsSpan(composedNative.BlocksOffset, composedNative.BlocksCapacityBytes),
            repackedIndex.UsedBlockBytes,
            composedNative.TriangleCount);
        if (!readbackIndex.TreeBytes.SequenceEqual(repackedIndex.TreeBytes) ||
            !readbackIndex.BlockBytes.SequenceEqual(repackedIndex.BlockBytes) ||
            !CollisionCellSequencesEqual(readbackIndex.Cells, repackedIndex.AuthoredIndex.Cells) ||
            afterModel.AsSpan(composedNative.BlocksOffset + repackedIndex.UsedBlockBytes,
                composedNative.BlocksCapacityBytes - repackedIndex.UsedBlockBytes).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The native-ordered collision repack failed exact structural/semantic readback or zero-tail validation.");
        }

        RequireCollisionCompositionDiff(
            uncomposedCollisionBytes,
            afterModel.AsSpan(visualCollisionOffset, visualCollision.ByteLength),
            authoredNative,
            ConstructionCollisionTriangleIndex,
            repackedIndex);
        IReadOnlyList<IReadOnlyList<int>> occlusionGroups = ParseOcclusionGroups(
            afterModel,
            checked((int)(composedLayout.Components.Single(component => component.Name == "occlusion").WadOffset - ModelSubfileWadOffset)),
            composedLayout.Components.Single(component => component.Name == "occlusion").ByteLength,
            composedLayout.Sectors.Count);
        if (occlusionGroups.Count != 16 || !occlusionGroups[0].Contains(visual.SectorIndex))
            throw new InvalidDataException("The construction triangle's ordinary assignment 0 does not own sector 213 in native occlusion data.");

        (long NormalX, long NormalY, long NormalZ) = CollisionNormal(targetPoints[0], targetPoints[1], targetPoints[2]);
        if (NormalZ <= 0)
            throw new InvalidDataException("The first construction collision triangle does not retain upward winding.");

        return new(
            ProfileId,
            BaselineImageSha256,
            visual,
            new UnusedLevel65ConstructionCollisionBinding(
                ConstructionCollisionTriangleIndex,
                beforeCollision.WadOffset + (sourceTriangleOffset - beforeCollisionOffset),
                visualCollision.WadOffset + (authoredTriangleOffset - visualCollisionOffset),
                Convert.ToHexString(sourceTriangleBytes),
                Convert.ToHexString(authoredTriangleBytes),
                sourcePoints,
                targetPoints,
                sourceAssignment,
                TargetAssignment: 0,
                sourceLookupReferences,
                lookupByteOffsets.Length,
                lookupWadOffsets,
                targetCells,
                SourceWasZeroArea: true,
                UpwardWinding: true,
                OrdinaryCollisionFlags: true),
            authoredNative.TreeCapacityBytes,
            repackedIndex.TreeBytes.Length,
            authoredNative.BlocksCapacityBytes,
            repackedIndex.UsedBlockBytes,
            Hash(uncomposedCollisionBytes),
            composedCollision.Sha256,
            Hash(afterModel),
            afterModel,
            preservedDegenerateBindings,
            nativeIndex.Cells.Count,
            repackedIndex.AuthoredIndex.Cells.Count,
            repackedIndex.ChangedCells,
            repackedIndex.UnchangedCellSequencesPreserved,
            repackedIndex.IntendedCellSequencesVerified,
            repackedIndex.NativeOrderingPreserved,
            exposureProof,
            occlusionGroups.Count,
            OcclusionAssignment: 0,
            AssignedOcclusionGroupContainsSector: true,
            CollisionIndexRepacked: true,
            CollisionComposed: true,
            OcclusionOwnershipVerified: true,
            DisposableRuntimeCandidateAuthorized: false,
            Publishable: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    private static UnusedLevel65FullAuthoringConstructionContract ToContract(
        int wadByteLength,
        ParsedModel parsed,
        IReadOnlyList<UnusedLevel65ConstructionDataSubfileLayout> dataSubfiles,
        UnusedLevel65ConstructionLandingInvariant landing,
        UnusedLevel65ConstructionPlayerAnchorInvariant playerAnchor,
        UnusedLevel65ConstructionDisplayNameInvariant displayName)
    {
        return new(
            ProfileId,
            UnusedLevel65BlankLevelLabProfileRegistry.ProfileId,
            BaselineImageSha256,
            WadLba,
            wadByteLength,
            DataEntryWadOffset,
            DataEntryByteLength,
            ModelSubfileWadOffset,
            ModelSubfileByteLength,
            parsed.TextureCount,
            parsed.Sectors.Count,
            parsed.Sectors.Sum(sector => sector.LowDetailVertexCount),
            parsed.Sectors.Sum(sector => sector.LowDetailFaceCount),
            parsed.Sectors.Sum(sector => sector.HighDetailVertexCount),
            parsed.Sectors.Sum(sector => sector.HighDetailFaceCount),
            parsed.SpecialSurfaceCount,
            parsed.CollisionTriangleCount,
            parsed.PortalCount,
            ObjectTableWadOffset,
            ObjectRecordCount,
            landing,
            playerAnchor,
            displayName,
            parsed.UsedEndWadOffset,
            parsed.ZeroTailBytes,
            dataSubfiles,
            parsed.Components.Select(component => new UnusedLevel65ConstructionComponentLayout(
                component.Name,
                component.WadOffset,
                component.ByteLength,
                component.Sha256,
                ComponentPolicy(component.Name))).ToArray(),
            parsed.Sectors.Select(ToPublicSector).ToArray(),
            RequiresSpawnSafeHighAndLowDetailFoundation: true,
            RequiresCompleteCollisionAndOcclusionOwnership: true,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);
    }

    private static UnusedLevel65ConstructionSceneSectorLayout ToPublicSector(ParsedSector sector) =>
        new(
            sector.Index,
            sector.WadOffset,
            sector.ByteLength,
            sector.GapBytesAfter,
            sector.LowDetailVertexCount,
            sector.LowDetailFaceCount,
            sector.HighDetailVertexCount,
            sector.HighDetailFaceCount);

    private static UnusedLevel65ConstructionComponentPolicy ComponentPolicy(string name) => name switch
    {
        "environment" or "occlusion" or "collision" =>
            UnusedLevel65ConstructionComponentPolicy.RebuildAtomically,
        "special-surface" or "portal-table" =>
            UnusedLevel65ConstructionComponentPolicy.ReplaceAfterFocusedRuntimeGate,
        _ => UnusedLevel65ConstructionComponentPolicy.PreserveContainer
    };

    private static ParsedModel ParseModel(byte[] model, long modelWadOffset)
    {
        if (model.Length != ModelSubfileByteLength)
            throw new InvalidDataException($"ID65 model data is {model.Length} bytes, expected {ModelSubfileByteLength}.");

        int cursor = 0;
        ParsedComponent texture = ReadComponent(model, modelWadOffset, ref cursor, "texture");
        ParsedComponent environment = ReadComponent(model, modelWadOffset, ref cursor, "environment");
        ParsedComponent occlusion = ReadComponent(model, modelWadOffset, ref cursor, "occlusion");
        ParsedComponent special = ReadComponent(model, modelWadOffset, ref cursor, "special-surface");
        ParsedComponent collision = ReadComponent(model, modelWadOffset, ref cursor, "collision");
        ParsedComponent cyclorama = ReadComponent(model, modelWadOffset, ref cursor, "cyclorama");

        int portalStart = cursor;
        RequireRange(model, portalStart, 4, "portal count");
        int portalCount = ReadInt32(model, portalStart);
        if (portalCount != 0)
            throw new InvalidDataException("The current ID65 construction substrate unexpectedly contains native portal records.");
        cursor += 4;
        ParsedComponent portal = new(
            "portal-table",
            modelWadOffset + portalStart,
            4,
            Hash(model.AsSpan(portalStart, 4)));
        ParsedComponent particles = ReadComponent(model, modelWadOffset, ref cursor, "particles");
        ParsedComponent sound = ReadComponent(model, modelWadOffset, ref cursor, "sound");
        if (model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The ID65 model suffix after the sound component is not exact zero tail.");

        int textureCount = ReadInt32(model, checked((int)(texture.WadOffset - modelWadOffset) + 4));
        int specialCount = ReadInt32(model, checked((int)(special.WadOffset - modelWadOffset) + 4));
        int collisionTriangleCount = ReadInt32(model, checked((int)(collision.WadOffset - modelWadOffset) + 4));
        int environmentOffset = checked((int)(environment.WadOffset - modelWadOffset));
        int sectorCount = ReadInt32(model, environmentOffset + 4);
        if (sectorCount <= 0 || sectorCount > 4096 || 8L + (sectorCount * 4L) > environment.ByteLength)
            throw new InvalidDataException("The ID65 environment sector-pointer table is invalid.");

        List<ParsedSector> sectors = new(sectorCount);
        for (int index = 0; index < sectorCount; index++)
        {
            int pointer = ReadInt32(model, environmentOffset + 8 + (index * 4));
            int sectorOffset = checked(environmentOffset + 4 + pointer);
            int nextOffset = index + 1 < sectorCount
                ? checked(environmentOffset + 4 + ReadInt32(model, environmentOffset + 8 + ((index + 1) * 4)))
                : checked(environmentOffset + environment.ByteLength);
            if (sectorOffset < environmentOffset + 8 + (sectorCount * 4) || sectorOffset >= nextOffset)
                throw new InvalidDataException($"ID65 scene-sector pointer {index} is not strictly ordered inside the environment component.");

            RequireRange(model, sectorOffset, 28, $"scene sector {index} header");
            int lpVertices = model[sectorOffset + 16];
            int lpColours = model[sectorOffset + 17];
            int lpFaces = model[sectorOffset + 18];
            int hpVertices = model[sectorOffset + 20];
            int hpColours = model[sectorOffset + 21];
            int hpFaces = model[sectorOffset + 22];
            int size = checked((7 + lpVertices + lpColours + (lpFaces * 2) + hpVertices + (hpColours * 2) + (hpFaces * 4)) * 4);
            if (size < 28 || sectorOffset + size > nextOffset)
                throw new InvalidDataException($"ID65 scene sector {index} overruns its next native pointer.");
            int gap = nextOffset - (sectorOffset + size);
            if (gap > 0 && model.AsSpan(sectorOffset + size, gap).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException($"ID65 scene sector {index} has nonzero bytes in its pointer-bounded gap.");
            sectors.Add(new(
                index,
                modelWadOffset + sectorOffset,
                size,
                gap,
                lpVertices,
                lpFaces,
                hpVertices,
                hpFaces));
        }

        return new ParsedModel(
            textureCount,
            specialCount,
            collisionTriangleCount,
            portalCount,
            modelWadOffset + cursor,
            model.Length - cursor,
            environment,
            [texture, environment, occlusion, special, collision, cyclorama, portal, particles, sound],
            sectors);
    }

    private static ParsedComponent ReadComponent(
        byte[] model,
        long modelWadOffset,
        ref int cursor,
        string name)
    {
        RequireRange(model, cursor, 4, $"{name} component length");
        int length = ReadInt32(model, cursor);
        if (length < 4 || (length & 3) != 0)
            throw new InvalidDataException($"The ID65 {name} component has invalid length 0x{length:X}.");
        RequireRange(model, cursor, length, $"{name} component");
        ParsedComponent component = new(
            name,
            modelWadOffset + cursor,
            length,
            Hash(model.AsSpan(cursor, length)));
        cursor = checked(cursor + length);
        return component;
    }

    private static void RequireExpectedBaseline(ParsedModel parsed)
    {
        RequireComponent(parsed, "texture", TextureComponentWadOffset, TextureComponentByteLength);
        RequireComponent(parsed, "environment", EnvironmentComponentWadOffset, EnvironmentComponentByteLength);
        RequireComponent(parsed, "occlusion", OcclusionComponentWadOffset, OcclusionComponentByteLength);
        RequireComponent(parsed, "special-surface", SpecialSurfaceComponentWadOffset, SpecialSurfaceComponentByteLength);
        RequireComponent(parsed, "collision", CollisionComponentWadOffset, CollisionComponentByteLength);
        RequireComponent(parsed, "cyclorama", CycloramaComponentWadOffset, CycloramaComponentByteLength);
        RequireComponent(parsed, "portal-table", PortalTableWadOffset, PortalTableByteLength);
        RequireComponent(parsed, "particles", ParticleComponentWadOffset, ParticleComponentByteLength);
        RequireComponent(parsed, "sound", SoundComponentWadOffset, SoundComponentByteLength);
        if (!string.Equals(
                parsed.Components.Single(component => component.Name == "cyclorama").Sha256,
                CycloramaComponentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The exact ID65 cyclorama component changed.");
        }
        if (parsed.TextureCount != NativeTextureRecordCount ||
            parsed.Sectors.Count != SceneSectorCount ||
            parsed.Sectors.Sum(sector => sector.LowDetailVertexCount) != TotalLowDetailVertexCount ||
            parsed.Sectors.Sum(sector => sector.LowDetailFaceCount) != TotalLowDetailFaceCount ||
            parsed.Sectors.Sum(sector => sector.HighDetailVertexCount) != TotalHighDetailVertexCount ||
            parsed.Sectors.Sum(sector => sector.HighDetailFaceCount) != TotalHighDetailFaceCount ||
            parsed.SpecialSurfaceCount != SpecialSurfaceCount ||
            parsed.CollisionTriangleCount != CollisionTriangleCount ||
            parsed.PortalCount != PortalCount ||
            parsed.UsedEndWadOffset != UsedModelEndWadOffset ||
            parsed.ZeroTailBytes != VerifiedZeroTailBytes ||
            parsed.Sectors.Any(sector => sector.GapBytesAfter != 0))
        {
            throw new InvalidDataException("The V5 ID65 model layout no longer matches the full-authoring construction contract.");
        }
    }

    private static void RequireComponent(ParsedModel parsed, string name, long wadOffset, int byteLength)
    {
        ParsedComponent component = parsed.Components.Single(item => item.Name == name);
        if (component.WadOffset != wadOffset || component.ByteLength != byteLength)
        {
            throw new InvalidDataException(
                $"The ID65 {name} component resolved to WAD 0x{component.WadOffset:X}/0x{component.ByteLength:X}, expected 0x{wadOffset:X}/0x{byteLength:X}.");
        }
    }

    private static uint EncodeSceneVertex(
        byte[] sector,
        UnusedLevel65ConstructionPoint point)
    {
        uint centreRadiusAndFlags = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2));
        uint xy = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(8, 4));
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(12, 4));
        int sectorX = (int)((xy >> 16) & 0xFFFF);
        int sectorY = (int)(xy & 0xFFFF);
        int sectorZ = (int)(((z >> 14) & 0xFFFF) >> 2);
        bool flat = ((centreRadiusAndFlags >> 12) & 1) == 1;
        int encodedX = point.X - sectorX;
        int encodedY = point.Y - sectorY;
        int encodedZ = flat ? (point.Z * 8) - sectorZ : point.Z - sectorZ;
        if (encodedX < 0 || encodedX > 2047 ||
            encodedY < 0 || encodedY > 2047 ||
            encodedZ < 0 || encodedZ > 1023)
        {
            throw new InvalidDataException($"Construction point {point} is outside sector-213 vertex encoding range.");
        }
        return ((uint)encodedX << 21) | ((uint)encodedY << 10) | (uint)encodedZ;
    }

    private static bool PointInsideSectorCullSphere(
        byte[] sector,
        UnusedLevel65ConstructionPoint point)
    {
        uint xy = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(0, 4));
        int centerX = (int)((xy >> 16) & 0xFFFF);
        int centerY = (int)(xy & 0xFFFF);
        int centerZ = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(6, 2));
        int radius = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2)) & 0x0FFF;
        long dx = point.X - centerX;
        long dy = point.Y - centerY;
        long dz = point.Z - centerZ;
        return (dx * dx) + (dy * dy) + (dz * dz) <= (long)radius * radius;
    }

    private static ParsedCollisionLayout ParseCollisionLayout(
        byte[] model,
        int componentOffset,
        long componentWadOffset)
    {
        RequireRange(model, componentOffset, CollisionComponentByteLength, "collision component");
        if (ReadInt32(model, componentOffset) != CollisionComponentByteLength)
            throw new InvalidDataException("The construction collision component length changed.");
        int headerOffset = checked(componentOffset + 4);
        int triangleCount = ReadInt32(model, headerOffset);
        int flagsByteLength = ReadInt32(model, headerOffset + 4);
        int treeRelative = ReadInt32(model, headerOffset + 8);
        int blocksRelative = ReadInt32(model, headerOffset + 12);
        int trianglesRelative = ReadInt32(model, headerOffset + 16);
        int assignmentsRelative = ReadInt32(model, headerOffset + 20);
        int flagsRelative = ReadInt32(model, headerOffset + 24);
        if (triangleCount != CollisionTriangleCount ||
            flagsByteLength != CollisionFlagsByteLength ||
            treeRelative != CollisionBlockTreeRelativeOffset ||
            blocksRelative != CollisionBlocksRelativeOffset ||
            trianglesRelative != CollisionTrianglesRelativeOffset ||
            assignmentsRelative != CollisionAssignmentsRelativeOffset ||
            flagsRelative != CollisionFlagsRelativeOffset)
        {
            throw new InvalidDataException("The construction collision header no longer matches its exact native layout.");
        }

        int treeOffset = checked(headerOffset + treeRelative);
        int blocksOffset = checked(headerOffset + blocksRelative);
        int triangleOffset = checked(headerOffset + trianglesRelative);
        int assignmentsOffset = checked(headerOffset + assignmentsRelative);
        int flagsOffset = checked(headerOffset + flagsRelative);
        int componentEnd = checked(componentOffset + CollisionComponentByteLength);
        if (treeOffset != componentOffset + 0x20 ||
            triangleOffset + checked(triangleCount * 12) != assignmentsOffset ||
            assignmentsOffset + triangleCount > flagsOffset ||
            flagsOffset + flagsByteLength != componentEnd)
        {
            throw new InvalidDataException("The construction collision tables do not exactly partition the fixed component.");
        }

        return new(
            componentOffset,
            componentWadOffset,
            triangleCount,
            treeOffset,
            blocksOffset,
            triangleOffset,
            assignmentsOffset,
            flagsOffset,
            blocksOffset - treeOffset,
            triangleOffset - blocksOffset,
            componentWadOffset + (blocksOffset - componentOffset));
    }

    private static DecodedCollisionTriangle DecodeCollisionTriangle(ReadOnlySpan<byte> bytes, int index)
    {
        if (bytes.Length < 12)
            throw new InvalidDataException("A construction collision triangle is truncated.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65ConstructionPoint p1 = new(
            (int)(xWord & 0x3FFF),
            (int)(yWord & 0x3FFF),
            (int)(zWord & 0x3FFF));
        UnusedLevel65ConstructionPoint p2 = new(
            p1.X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 16) & 0xFF));
        UnusedLevel65ConstructionPoint p3 = new(
            p1.X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 24) & 0xFF));
        return new(index, xWord, yWord, zWord, zWord & 0xC000u, [p1, p2, p3]);
    }

    private static byte[] EncodeCollisionTriangle(
        IReadOnlyList<UnusedLevel65ConstructionPoint> points,
        uint zFlags)
    {
        if (points.Count != 3)
            throw new InvalidDataException("A construction collision triangle must have exactly three points.");
        UnusedLevel65ConstructionPoint p1 = points[0];
        UnusedLevel65ConstructionPoint p2 = points[1];
        UnusedLevel65ConstructionPoint p3 = points[2];
        if (!TryEncodeSigned9(p2.X - p1.X, out uint p2Dx) ||
            !TryEncodeSigned9(p3.X - p1.X, out uint p3Dx) ||
            !TryEncodeSigned9(p2.Y - p1.Y, out uint p2Dy) ||
            !TryEncodeSigned9(p3.Y - p1.Y, out uint p3Dy) ||
            p1.X < 0 || p1.X > 0x3FFF ||
            p1.Y < 0 || p1.Y > 0x3FFF ||
            p1.Z < 0 || p1.Z > 0x3FFF ||
            p2.Z - p1.Z < 0 || p2.Z - p1.Z > 0xFF ||
            p3.Z - p1.Z < 0 || p3.Z - p1.Z > 0xFF)
        {
            throw new InvalidDataException("The construction collision triangle cannot be packed in Spyro's native triangle format.");
        }

        uint xWord = (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23);
        uint yWord = (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23);
        uint zWord = (zFlags & 0xC000u) |
                     (uint)(p1.Z & 0x3FFF) |
                     ((uint)(p2.Z - p1.Z) << 16) |
                     ((uint)(p3.Z - p1.Z) << 24);
        byte[] result = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), xWord);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), yWord);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), zWord);
        return result;
    }

    private static bool TryEncodeSigned9(int value, out uint encoded)
    {
        if (value < -256 || value > 255)
        {
            encoded = 0;
            return false;
        }
        encoded = (uint)(value & 0x1FF);
        return true;
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        int mask = 1 << bits;
        return (value & sign) != 0 ? value - mask : value;
    }

    private static bool IsZeroAreaCollisionTriangle(DecodedCollisionTriangle triangle)
    {
        (long x, long y, long z) = CollisionNormal(
            triangle.Points[0],
            triangle.Points[1],
            triangle.Points[2]);
        return x == 0 && y == 0 && z == 0;
    }

    private static (long X, long Y, long Z) CollisionNormal(
        UnusedLevel65ConstructionPoint a,
        UnusedLevel65ConstructionPoint b,
        UnusedLevel65ConstructionPoint c)
    {
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        return (
            (abY * acZ) - (abZ * acY),
            (abZ * acX) - (abX * acZ),
            (abX * acY) - (abY * acX));
    }

    private static int CountCollisionTriangleReferences(ReadOnlySpan<byte> blockBytes, int triangleIndex) =>
        FindCollisionTriangleReferenceByteOffsets(blockBytes, triangleIndex).Length;

    private static int[] FindCollisionTriangleReferenceByteOffsets(ReadOnlySpan<byte> blockBytes, int triangleIndex)
    {
        List<int> result = [];
        for (int offset = 0; offset + 2 <= blockBytes.Length; offset += 2)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.Slice(offset, 2));
            if ((word & 0x7FFF) == triangleIndex)
                result.Add(offset);
        }
        return result.ToArray();
    }

    private static UnusedLevel65ConstructionCollisionCell[] FindCollisionIndexCells(
        NativeCollisionIndexSnapshot index,
        int triangleIndex) =>
        index.Cells
            .Where(pair => pair.Value.OrderedTriangleIndexes.Contains(triangleIndex))
            .Select(pair => pair.Key)
            .OrderBy(cell => cell.Z)
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();

    private static NativeCollisionIndexSnapshot DecodeNativeCollisionIndex(
        ReadOnlySpan<byte> treeCapacity,
        ReadOnlySpan<byte> blockCapacity,
        int usedBlockBytes,
        int triangleCount)
    {
        if ((treeCapacity.Length & 1) != 0 ||
            (blockCapacity.Length & 1) != 0 ||
            usedBlockBytes <= 0 ||
            (usedBlockBytes & 1) != 0 ||
            usedBlockBytes > blockCapacity.Length ||
            triangleCount <= 0 ||
            triangleCount > 0x8000)
        {
            throw new InvalidDataException("The native collision lookup capacities or triangle count are invalid.");
        }

        byte[] treeBytes = treeCapacity.ToArray();
        byte[] blockBytes = blockCapacity.ToArray();
        int usedBlockWords = usedBlockBytes / 2;
        List<int> groupStarts = [];
        for (int wordOffset = 0; wordOffset < usedBlockWords; wordOffset++)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(
                blockBytes.AsSpan(wordOffset * 2, 2));
            if ((word & 0x8000) != 0)
                groupStarts.Add(wordOffset);
        }
        if (groupStarts.Count < 2 ||
            groupStarts[^1] != usedBlockWords - 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(groupStarts[^1] * 2, 2)) != 0x8000)
        {
            throw new InvalidDataException("The native collision blocks do not end at their exact unreferenced 0x8000 sentinel group.");
        }

        int terminalSentinelWordOffset = groupStarts[^1];
        Dictionary<int, int[]> orderedGroupsByWordOffset = [];
        for (int groupIndex = 0; groupIndex < groupStarts.Count - 1; groupIndex++)
        {
            int start = groupStarts[groupIndex];
            int end = groupStarts[groupIndex + 1];
            if (start >= end)
                throw new InvalidDataException("A native collision lookup group is empty or overlaps its successor.");
            int[] triangleIndexes = new int[end - start];
            for (int index = 0; index < triangleIndexes.Length; index++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(
                    blockBytes.AsSpan((start + index) * 2, 2));
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A native collision lookup group has an invalid start marker.");
                int triangleIndex = word & 0x7FFF;
                if (triangleIndex >= triangleCount)
                    throw new InvalidDataException($"A native collision lookup group references triangle {triangleIndex} outside the table.");
                triangleIndexes[index] = triangleIndex;
            }
            orderedGroupsByWordOffset.Add(start, triangleIndexes);
        }

        int ReadTreeWord(int byteOffset, string label)
        {
            if (byteOffset < 0 || (byteOffset & 1) != 0 || byteOffset + 2 > treeBytes.Length)
                throw new InvalidDataException($"The native collision {label} word is outside the tree capacity.");
            return BinaryPrimitives.ReadUInt16LittleEndian(treeBytes.AsSpan(byteOffset, 2));
        }

        int ReadSegmentLength(int byteOffset, string label)
        {
            int length = ReadTreeWord(byteOffset, label);
            if (length < 0 || length > 256 || byteOffset + ((length + 1) * 2L) > treeBytes.Length)
                throw new InvalidDataException($"The native collision {label} segment length {length} is invalid.");
            return length;
        }

        Dictionary<UnusedLevel65ConstructionCollisionCell, NativeCollisionCellBinding> cells = [];
        int zLength = ReadSegmentLength(0, "Z-root");
        for (int z = 0; z < zLength; z++)
        {
            int ySegmentOffset = ReadTreeWord((z + 1) * 2, $"Z[{z}] pointer");
            if (ySegmentOffset == 0xFFFF)
                continue;
            int yLength = ReadSegmentLength(ySegmentOffset, $"Y[{z}]");
            for (int y = 0; y < yLength; y++)
            {
                int xSegmentOffset = ReadTreeWord(ySegmentOffset + ((y + 1) * 2), $"Y[{z},{y}] pointer");
                if (xSegmentOffset == 0xFFFF)
                    continue;
                int xLength = ReadSegmentLength(xSegmentOffset, $"X[{z},{y}]");
                for (int x = 0; x < xLength; x++)
                {
                    int treePointerByteOffset = xSegmentOffset + ((x + 1) * 2);
                    int blockWordOffset = ReadTreeWord(treePointerByteOffset, $"X[{z},{y},{x}] pointer");
                    if (blockWordOffset == 0xFFFF)
                        continue;
                    if (blockWordOffset == terminalSentinelWordOffset ||
                        !orderedGroupsByWordOffset.TryGetValue(blockWordOffset, out int[]? orderedTriangleIndexes))
                    {
                        throw new InvalidDataException(
                            $"Native collision cell ({x},{y},{z}) points to non-group block word 0x{blockWordOffset:X}.");
                    }

                    UnusedLevel65ConstructionCollisionCell cell = new(x, y, z);
                    if (!cells.TryAdd(
                            cell,
                            new NativeCollisionCellBinding(
                                cell,
                                treePointerByteOffset,
                                blockWordOffset,
                                orderedTriangleIndexes.ToArray())))
                    {
                        throw new InvalidDataException($"Native collision cell {cell} is represented more than once in the tree.");
                    }
                }
            }
        }

        int[] referencedGroupStarts = cells.Values
            .Select(binding => binding.SourceBlockWordOffset)
            .Distinct()
            .Order()
            .ToArray();
        if (!referencedGroupStarts.SequenceEqual(orderedGroupsByWordOffset.Keys.Order()))
            throw new InvalidDataException("The native collision blocks contain an unreferenced non-sentinel group or omit a referenced group.");

        return new NativeCollisionIndexSnapshot(
            treeBytes,
            blockBytes,
            cells,
            groupStarts.Count,
            terminalSentinelWordOffset,
            usedBlockBytes);
    }

    private static NativeCollisionIndexRepack RepackConstructionCollisionIndex(
        NativeCollisionIndexSnapshot nativeIndex,
        IReadOnlyList<UnusedLevel65ConstructionCollisionCell> targetCells,
        int triangleIndex,
        int triangleCount)
    {
        UnusedLevel65ConstructionCollisionCell sourceCell = new(34, 35, 1);
        UnusedLevel65ConstructionCollisionCell firstTargetCell = new(30, 24, 2);
        UnusedLevel65ConstructionCollisionCell secondTargetCell = new(30, 25, 2);
        if (!targetCells.SequenceEqual(new[] { firstTargetCell, secondTargetCell }))
            throw new InvalidDataException("The construction collision transaction received an unexpected target-cell footprint.");

        RequireCollisionCellSequence(
            nativeIndex,
            sourceCell,
            [14_001, 14_000, 13_999, 13_998, 13_995, 11_276, 11_234, 11_197, 11_196, 11_191, 11_190, 11_184, 3_130, 3_129, 3_128, 3_066, 3_065, 3_064, 3_060, 3_058, 3_057, 3_056, 3_055]);
        RequireCollisionCellSequence(
            nativeIndex,
            firstTargetCell,
            [1_403, 1_402, 1_400, 1_396, 1_392, 1_391, 1_390, 1_363, 1_362, 1_359, 1_354, 1_353, 1_351, 1_350, 1_349, 1_348, 1_347]);
        RequireCollisionCellSequence(
            nativeIndex,
            secondTargetCell,
            [1_401, 1_400, 1_395, 1_374, 1_373, 1_372, 1_371, 1_370, 1_369, 1_367, 1_366, 1_363, 1_362, 1_361, 1_360, 1_354, 1_353]);
        UnusedLevel65ConstructionCollisionCell[] nativeTriangleCells = FindCollisionIndexCells(nativeIndex, triangleIndex);
        if (!nativeTriangleCells.SequenceEqual(new[] { sourceCell }))
            throw new InvalidDataException("The repurposed collision row no longer has its exact single native source-cell ownership.");

        Dictionary<UnusedLevel65ConstructionCollisionCell, int[]> desiredSequences = nativeIndex.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desiredSequences[sourceCell] = desiredSequences[sourceCell]
            .Where(index => index != triangleIndex)
            .ToArray();
        foreach (UnusedLevel65ConstructionCollisionCell targetCell in targetCells)
        {
            if (desiredSequences[targetCell].Contains(triangleIndex))
                throw new InvalidDataException($"Target collision cell {targetCell} already contains row {triangleIndex}.");
            desiredSequences[targetCell] = [triangleIndex, .. desiredSequences[targetCell]];
        }

        Dictionary<string, int> emittedOffsetsBySequence = new(StringComparer.Ordinal);
        Dictionary<UnusedLevel65ConstructionCollisionCell, int> blockWordOffsetsByCell = [];
        List<ushort> packedWords = [];
        foreach (NativeCollisionCellBinding binding in nativeIndex.Cells.Values
                     .OrderBy(binding => binding.SourceBlockWordOffset)
                     .ThenBy(binding => binding.Cell.Z)
                     .ThenBy(binding => binding.Cell.Y)
                     .ThenBy(binding => binding.Cell.X))
        {
            int[] sequence = desiredSequences[binding.Cell];
            if (sequence.Length == 0 || sequence.Any(index => index < 0 || index >= triangleCount || index > 0x7FFF))
                throw new InvalidDataException($"Authored collision cell {binding.Cell} has an invalid ordered triangle sequence.");
            string key = string.Join(',', sequence);
            if (!emittedOffsetsBySequence.TryGetValue(key, out int blockWordOffset))
            {
                blockWordOffset = packedWords.Count;
                for (int index = 0; index < sequence.Length; index++)
                {
                    ushort word = checked((ushort)sequence[index]);
                    if (index == 0)
                        word |= 0x8000;
                    packedWords.Add(word);
                }
                emittedOffsetsBySequence.Add(key, blockWordOffset);
            }
            blockWordOffsetsByCell.Add(binding.Cell, blockWordOffset);
        }
        packedWords.Add(0x8000);
        int usedBlockBytes = checked(packedWords.Count * 2);
        if (usedBlockBytes > nativeIndex.BlockBytes.Length)
        {
            throw new InvalidDataException(
                $"The native-semantic collision repack needs 0x{usedBlockBytes:X} block bytes; capacity is 0x{nativeIndex.BlockBytes.Length:X}.");
        }

        byte[] treeBytes = nativeIndex.TreeBytes.ToArray();
        Dictionary<int, int> pointerValues = [];
        foreach (NativeCollisionCellBinding binding in nativeIndex.Cells.Values)
        {
            int pointerValue = blockWordOffsetsByCell[binding.Cell];
            if (pointerValue > ushort.MaxValue)
                throw new InvalidDataException("A repacked collision block pointer exceeds its native 16-bit word field.");
            if (pointerValues.TryGetValue(binding.TreePointerByteOffset, out int existing) && existing != pointerValue)
            {
                throw new InvalidDataException(
                    $"Shared native tree pointer 0x{binding.TreePointerByteOffset:X} would require incompatible authored groups.");
            }
            pointerValues[binding.TreePointerByteOffset] = pointerValue;
        }
        foreach ((int pointerByteOffset, int pointerValue) in pointerValues)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                treeBytes.AsSpan(pointerByteOffset, 2),
                checked((ushort)pointerValue));
        }

        byte[] blockBytes = new byte[nativeIndex.BlockBytes.Length];
        for (int index = 0; index < packedWords.Count; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(blockBytes.AsSpan(index * 2, 2), packedWords[index]);
        NativeCollisionIndexSnapshot authoredIndex = DecodeNativeCollisionIndex(
            treeBytes,
            blockBytes,
            usedBlockBytes,
            triangleCount);

        UnusedLevel65ConstructionCollisionCell[] changedCells = nativeIndex.Cells.Keys
            .Where(cell => !nativeIndex.Cells[cell].OrderedTriangleIndexes.SequenceEqual(
                authoredIndex.Cells[cell].OrderedTriangleIndexes))
            .OrderBy(cell => cell.Z)
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();
        UnusedLevel65ConstructionCollisionCell[] expectedChangedCells =
            [sourceCell, firstTargetCell, secondTargetCell];
        bool unchangedPreserved = nativeIndex.Cells.Keys
            .Except(expectedChangedCells)
            .All(cell => nativeIndex.Cells[cell].OrderedTriangleIndexes.SequenceEqual(
                authoredIndex.Cells[cell].OrderedTriangleIndexes));
        bool intendedVerified = changedCells.SequenceEqual(expectedChangedCells) &&
                                authoredIndex.Cells[sourceCell].OrderedTriangleIndexes.SequenceEqual(
                                    nativeIndex.Cells[sourceCell].OrderedTriangleIndexes.Where(index => index != triangleIndex)) &&
                                authoredIndex.Cells[firstTargetCell].OrderedTriangleIndexes.SequenceEqual(
                                    new[] { triangleIndex }.Concat(nativeIndex.Cells[firstTargetCell].OrderedTriangleIndexes)) &&
                                authoredIndex.Cells[secondTargetCell].OrderedTriangleIndexes.SequenceEqual(
                                    new[] { triangleIndex }.Concat(nativeIndex.Cells[secondTargetCell].OrderedTriangleIndexes));
        bool nativeOrderingPreserved = unchangedPreserved &&
                                       IsStrictlyDescending(authoredIndex.Cells[sourceCell].OrderedTriangleIndexes) &&
                                       IsStrictlyDescending(authoredIndex.Cells[firstTargetCell].OrderedTriangleIndexes) &&
                                       IsStrictlyDescending(authoredIndex.Cells[secondTargetCell].OrderedTriangleIndexes);
        if (authoredIndex.Cells.Count != nativeIndex.Cells.Count ||
            !unchangedPreserved ||
            !intendedVerified ||
            !nativeOrderingPreserved ||
            usedBlockBytes != 0x17962 ||
            Hash(treeBytes) != "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd" ||
            Hash(blockBytes) != "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4")
        {
            throw new InvalidDataException("The native-semantic collision repack failed its exact three-cell parity, ordering, capacity, or hash contract.");
        }

        return new NativeCollisionIndexRepack(
            treeBytes,
            blockBytes,
            usedBlockBytes,
            authoredIndex,
            changedCells,
            unchangedPreserved,
            intendedVerified,
            nativeOrderingPreserved);
    }

    private static void RequireCollisionCellSequence(
        NativeCollisionIndexSnapshot index,
        UnusedLevel65ConstructionCollisionCell cell,
        IReadOnlyList<int> expected)
    {
        if (!index.Cells.TryGetValue(cell, out NativeCollisionCellBinding? binding) ||
            !binding.OrderedTriangleIndexes.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Native collision cell {cell} no longer has its exact ordered group.");
        }
    }

    private static bool CollisionCellSequencesEqual(
        IReadOnlyDictionary<UnusedLevel65ConstructionCollisionCell, NativeCollisionCellBinding> left,
        IReadOnlyDictionary<UnusedLevel65ConstructionCollisionCell, NativeCollisionCellBinding> right) =>
        left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out NativeCollisionCellBinding? other) &&
                         pair.Value.OrderedTriangleIndexes.SequenceEqual(other.OrderedTriangleIndexes));

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1] <= values[index])
                return false;
        }
        return true;
    }

    private static UnusedLevel65ConstructionExposureProof AnalyzeConstructionExposure(
        byte[] model,
        ParsedModel parsed,
        ParsedCollisionLayout collision,
        IReadOnlyList<UnusedLevel65ConstructionPoint> authoredPoints)
    {
        if (authoredPoints.Count != 3 ||
            !authoredPoints.SequenceEqual(new[]
            {
                new UnusedLevel65ConstructionPoint(7762, 6346, 512),
                new UnusedLevel65ConstructionPoint(7890, 6346, 512),
                new UnusedLevel65ConstructionPoint(7826, 6474, 640)
            }))
        {
            throw new InvalidDataException("The construction exposure scan received an unexpected authored surface.");
        }

        List<UnusedLevel65ConstructionTerrainOverlap> lowDetailOverlaps = [];
        List<UnusedLevel65ConstructionTerrainOverlap> highDetailOverlaps = [];
        List<UnusedLevel65ConstructionTerrainOverlap> lowDetailCentroidOverlaps = [];
        List<UnusedLevel65ConstructionTerrainOverlap> highDetailCentroidOverlaps = [];
        List<string> lowDetailOverlapDiagnostics = [];
        List<string> highDetailOverlapDiagnostics = [];
        XyPoint authoredCentroid = new(
            authoredPoints.Sum(point => point.X) / 3.0,
            authoredPoints.Sum(point => point.Y) / 3.0);
        int scannedLowDetailFaces = 0;
        int scannedHighDetailFaces = 0;
        foreach (ParsedSector sector in parsed.Sectors)
        {
            int sectorOffset = checked((int)(sector.WadOffset - ModelSubfileWadOffset));
            byte[] sectorBytes = model.AsSpan(sectorOffset, sector.ByteLength).ToArray();
            int lowDetailVertexCount = sectorBytes[16];
            int lowDetailColorCount = sectorBytes[17];
            int lowDetailFaceCount = sectorBytes[18];
            int highDetailVertexCount = sectorBytes[20];
            int highDetailColorCount = sectorBytes[21];
            int highDetailFaceCount = sectorBytes[22];
            if (lowDetailVertexCount != sector.LowDetailVertexCount ||
                lowDetailFaceCount != sector.LowDetailFaceCount ||
                highDetailVertexCount != sector.HighDetailVertexCount ||
                highDetailFaceCount != sector.HighDetailFaceCount)
            {
                throw new InvalidDataException($"Construction exposure scan sector {sector.Index} count metadata drifted.");
            }

            int lowDetailVertexStart = 28;
            int lowDetailColorStart = checked(lowDetailVertexStart + (lowDetailVertexCount * 4));
            int lowDetailFaceStart = checked(lowDetailColorStart + (lowDetailColorCount * 4));
            int highDetailVertexStart = checked(lowDetailFaceStart + (lowDetailFaceCount * 8));
            int highDetailColorStart = checked(highDetailVertexStart + (highDetailVertexCount * 4));
            int highDetailFaceStart = checked(highDetailColorStart + (highDetailColorCount * 8));
            if (highDetailFaceStart + (highDetailFaceCount * 16) != sectorBytes.Length)
                throw new InvalidDataException($"Construction exposure scan sector {sector.Index} tables do not exactly partition the sector.");

            UnusedLevel65ConstructionPoint[] lowDetailVertices = Enumerable.Range(0, lowDetailVertexCount)
                .Select(index => DecodeSceneVertex(
                    sectorBytes,
                    BinaryPrimitives.ReadUInt32LittleEndian(sectorBytes.AsSpan(lowDetailVertexStart + (index * 4), 4))))
                .ToArray();
            UnusedLevel65ConstructionPoint[] highDetailVertices = Enumerable.Range(0, highDetailVertexCount)
                .Select(index => DecodeSceneVertex(
                    sectorBytes,
                    BinaryPrimitives.ReadUInt32LittleEndian(sectorBytes.AsSpan(highDetailVertexStart + (index * 4), 4))))
                .ToArray();

            for (int faceIndex = 0; faceIndex < lowDetailFaceCount; faceIndex++)
            {
                int faceOffset = lowDetailFaceStart + (faceIndex * 8);
                int[] indexes = ReadPackedSixBitSlots(
                    BinaryPrimitives.ReadUInt32LittleEndian(sectorBytes.AsSpan(faceOffset, 4)));
                UnusedLevel65ConstructionPoint[] facePoints = ResolveOrderedFacePoints(
                    lowDetailVertices,
                    indexes,
                    $"LP {sector.Index}:{faceIndex}");
                if (facePoints.Length >= 3 && HasPositiveAreaXyInteriorOverlap(authoredPoints, facePoints))
                {
                    UnusedLevel65ConstructionTerrainOverlap overlap = new(
                        sector.Index,
                        faceIndex,
                        facePoints.Min(point => point.Z),
                        facePoints.Max(point => point.Z));
                    lowDetailOverlaps.Add(overlap);
                    if (ContainsXyPoint(facePoints, authoredCentroid))
                        lowDetailCentroidOverlaps.Add(overlap);
                    lowDetailOverlapDiagnostics.Add($"{sector.Index}:{faceIndex}={string.Join(';', facePoints)}");
                }
                scannedLowDetailFaces++;
            }

            for (int faceIndex = 0; faceIndex < highDetailFaceCount; faceIndex++)
            {
                int faceOffset = highDetailFaceStart + (faceIndex * 16);
                int[] indexes =
                [
                    sectorBytes[faceOffset],
                    sectorBytes[faceOffset + 1],
                    sectorBytes[faceOffset + 2],
                    sectorBytes[faceOffset + 3]
                ];
                UnusedLevel65ConstructionPoint[] facePoints = ResolveOrderedFacePoints(
                    highDetailVertices,
                    indexes,
                    $"HP {sector.Index}:{faceIndex}");
                if (facePoints.Length >= 3 && HasPositiveAreaXyInteriorOverlap(authoredPoints, facePoints))
                {
                    UnusedLevel65ConstructionTerrainOverlap overlap = new(
                        sector.Index,
                        faceIndex,
                        facePoints.Min(point => point.Z),
                        facePoints.Max(point => point.Z));
                    highDetailOverlaps.Add(overlap);
                    if (ContainsXyPoint(facePoints, authoredCentroid))
                        highDetailCentroidOverlaps.Add(overlap);
                    highDetailOverlapDiagnostics.Add($"{sector.Index}:{faceIndex}={string.Join(';', facePoints)}");
                }
                scannedHighDetailFaces++;
            }
        }

        List<UnusedLevel65ConstructionCollisionOverlap> collisionOverlaps = [];
        for (int triangleIndex = 0; triangleIndex < collision.TriangleCount; triangleIndex++)
        {
            DecodedCollisionTriangle triangle = DecodeCollisionTriangle(
                model.AsSpan(collision.TriangleTableOffset + (triangleIndex * 12), 12),
                triangleIndex);
            if (!IsZeroAreaCollisionTriangle(triangle) &&
                HasPositiveAreaXyInteriorOverlap(authoredPoints, triangle.Points))
            {
                collisionOverlaps.Add(new(
                    triangleIndex,
                    triangle.Points.Min(point => point.Z),
                    triangle.Points.Max(point => point.Z)));
            }
        }

        UnusedLevel65ConstructionTerrainOverlap[] expectedLowDetail =
            [new(124, 0, 480, 480), new(213, 1, 512, 512), new(213, 3, 512, 512)];
        UnusedLevel65ConstructionTerrainOverlap[] expectedHighDetail = [new(213, 37, 512, 512)];
        UnusedLevel65ConstructionCollisionOverlap[] expectedCollision =
            [new(1_353, 512, 512), new(1_354, 512, 512)];
        int lowDetailCentroidTopZ = lowDetailCentroidOverlaps.Count == 0
            ? int.MinValue
            : lowDetailCentroidOverlaps.Max(overlap => overlap.MaximumZ);
        int highDetailCentroidTopZ = highDetailCentroidOverlaps.Count == 0
            ? int.MinValue
            : highDetailCentroidOverlaps.Max(overlap => overlap.MaximumZ);
        UnusedLevel65ConstructionTerrainOverlap[] lowDetailTopmostAtCentroid = lowDetailCentroidOverlaps
            .Where(overlap => overlap.MaximumZ == lowDetailCentroidTopZ)
            .ToArray();
        UnusedLevel65ConstructionTerrainOverlap[] highDetailTopmostAtCentroid = highDetailCentroidOverlaps
            .Where(overlap => overlap.MaximumZ == highDetailCentroidTopZ)
            .ToArray();
        bool exactFoundation = lowDetailOverlaps.SequenceEqual(expectedLowDetail) &&
                               highDetailOverlaps.SequenceEqual(expectedHighDetail) &&
                               collisionOverlaps.SequenceEqual(expectedCollision) &&
                               lowDetailTopmostAtCentroid.SequenceEqual(new[] { new UnusedLevel65ConstructionTerrainOverlap(213, 1, 512, 512) }) &&
                               highDetailTopmostAtCentroid.SequenceEqual(expectedHighDetail);
        bool noHigherLowDetail = lowDetailOverlaps.All(overlap => overlap.MaximumZ <= 512);
        bool noHigherHighDetail = highDetailOverlaps.All(overlap => overlap.MaximumZ <= 512);
        bool noHigherCollision = collisionOverlaps.All(overlap => overlap.MaximumZ <= 512);
        bool authoredTopmost = exactFoundation &&
                               noHigherLowDetail &&
                               noHigherHighDetail &&
                               noHigherCollision;
        int sector213Offset = checked((int)(parsed.Sectors[213].WadOffset - ModelSubfileWadOffset));
        int sector213RadiusAndFlags = BinaryPrimitives.ReadUInt16LittleEndian(model.AsSpan(sector213Offset + 4, 2));
        bool runtimeGuideSupported = (sector213RadiusAndFlags & 0xE000) == 0;
        if (parsed.Sectors.Count != SceneSectorCount ||
            scannedLowDetailFaces != TotalLowDetailFaceCount ||
            scannedHighDetailFaces != TotalHighDetailFaceCount ||
            collision.TriangleCount != CollisionTriangleCount ||
            !authoredTopmost ||
            !runtimeGuideSupported)
        {
            throw new InvalidDataException(
                "The full native exposure scan failed: " +
                $"sectors={parsed.Sectors.Count}, LP={scannedLowDetailFaces} [{string.Join(',', lowDetailOverlapDiagnostics)}], " +
                $"HP={scannedHighDetailFaces} [{string.Join(',', highDetailOverlapDiagnostics)}], " +
                $"collision={collision.TriangleCount} [{string.Join(',', collisionOverlaps)}], " +
                $"sector213Flags=0x{sector213RadiusAndFlags:X4}.");
        }

        return new UnusedLevel65ConstructionExposureProof(
            parsed.Sectors.Count,
            scannedLowDetailFaces,
            scannedHighDetailFaces,
            collision.TriangleCount,
            lowDetailOverlaps,
            highDetailOverlaps,
            collisionOverlaps,
            lowDetailTopmostAtCentroid,
            highDetailTopmostAtCentroid,
            AuthoredLowDetailFaceIndex: 21,
            AuthoredHighDetailFaceIndex: 113,
            ExactUnderlyingFoundationIdentified: true,
            NoHigherNativeLowDetailFace: true,
            NoHigherNativeHighDetailFace: true,
            NoHigherNativeCollisionTriangle: true,
            AuthoredSurfaceTopmostOverOpenInterior: true,
            CloseHighDetailFarLowDetailRuntimeGuideSupported: true);
    }

    private static UnusedLevel65ConstructionPoint[] ResolveOrderedFacePoints(
        IReadOnlyList<UnusedLevel65ConstructionPoint> vertices,
        IReadOnlyList<int> indexes,
        string label)
    {
        if (indexes.Any(index => index < 0 || index >= vertices.Count))
            throw new InvalidDataException($"Construction exposure scan {label} has an invalid vertex index.");
        List<UnusedLevel65ConstructionPoint> points = [];
        HashSet<int> seen = [];
        foreach (int index in indexes)
        {
            if (seen.Add(index))
                points.Add(vertices[index]);
        }
        return points.ToArray();
    }

    private static UnusedLevel65ConstructionPoint DecodeSceneVertex(byte[] sector, uint word)
    {
        uint xyPosition = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(8, 4));
        uint zPosition = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(12, 4));
        int sectorX = (int)(xyPosition >> 16);
        int sectorY = (int)(xyPosition & 0xFFFF);
        int sectorZ = (int)((zPosition >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        bool flat = ((BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2)) >> 12) & 1) == 1;
        if (flat)
            z >>= 3;
        return new(x, y, z);
    }

    private static bool ContainsXyPoint(
        IReadOnlyList<UnusedLevel65ConstructionPoint> polygon,
        XyPoint point)
    {
        for (int index = 1; index + 1 < polygon.Count; index++)
        {
            XyPoint a = new(polygon[0].X, polygon[0].Y);
            XyPoint b = new(polygon[index].X, polygon[index].Y);
            XyPoint c = new(polygon[index + 1].X, polygon[index + 1].Y);
            double orientation = Cross(a, b, c);
            if (Math.Abs(orientation) < 0.000001)
                continue;
            double ab = Cross(a, b, point);
            double bc = Cross(b, c, point);
            double ca = Cross(c, a, point);
            if (orientation > 0
                    ? ab >= 0 && bc >= 0 && ca >= 0
                    : ab <= 0 && bc <= 0 && ca <= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasPositiveAreaXyInteriorOverlap(
        IReadOnlyList<UnusedLevel65ConstructionPoint> targetTriangle,
        IReadOnlyList<UnusedLevel65ConstructionPoint> candidatePolygon)
    {
        if (targetTriangle.Count != 3 || candidatePolygon.Count < 3)
            return false;
        XyPoint[] target = targetTriangle.Select(point => new XyPoint(point.X, point.Y)).ToArray();
        for (int index = 1; index + 1 < candidatePolygon.Count; index++)
        {
            XyPoint[] candidate =
            [
                new(candidatePolygon[0].X, candidatePolygon[0].Y),
                new(candidatePolygon[index].X, candidatePolygon[index].Y),
                new(candidatePolygon[index + 1].X, candidatePolygon[index + 1].Y)
            ];
            if (TriangleIntersectionArea(target, candidate) > 0.000001)
                return true;
        }
        return false;
    }

    private static double TriangleIntersectionArea(
        IReadOnlyList<XyPoint> subjectTriangle,
        IReadOnlyList<XyPoint> clipTriangle)
    {
        List<XyPoint> polygon = subjectTriangle.ToList();
        double clipOrientation = Cross(clipTriangle[0], clipTriangle[1], clipTriangle[2]);
        if (Math.Abs(clipOrientation) < 0.000001)
            return 0;
        for (int edge = 0; edge < 3 && polygon.Count > 0; edge++)
        {
            XyPoint clipA = clipTriangle[edge];
            XyPoint clipB = clipTriangle[(edge + 1) % 3];
            List<XyPoint> input = polygon;
            polygon = [];
            XyPoint previous = input[^1];
            double previousSide = Cross(clipA, clipB, previous);
            bool previousInside = clipOrientation > 0 ? previousSide >= 0 : previousSide <= 0;
            foreach (XyPoint current in input)
            {
                double currentSide = Cross(clipA, clipB, current);
                bool currentInside = clipOrientation > 0 ? currentSide >= 0 : currentSide <= 0;
                if (currentInside != previousInside)
                {
                    double denominator = previousSide - currentSide;
                    if (Math.Abs(denominator) > 0.000001)
                    {
                        double t = previousSide / denominator;
                        polygon.Add(new(
                            previous.X + ((current.X - previous.X) * t),
                            previous.Y + ((current.Y - previous.Y) * t)));
                    }
                }
                if (currentInside)
                    polygon.Add(current);
                previous = current;
                previousSide = currentSide;
                previousInside = currentInside;
            }
        }
        if (polygon.Count < 3)
            return 0;
        double twiceArea = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            XyPoint a = polygon[index];
            XyPoint b = polygon[(index + 1) % polygon.Count];
            twiceArea += (a.X * b.Y) - (a.Y * b.X);
        }
        return Math.Abs(twiceArea) * 0.5;
    }

    private static double Cross(XyPoint a, XyPoint b, XyPoint point) =>
        ((b.X - a.X) * (point.Y - a.Y)) - ((b.Y - a.Y) * (point.X - a.X));

    private static UnusedLevel65ConstructionPreservedDegenerateBinding[] BuildPreservedDegenerateBindings(
        byte[] beforeModel,
        byte[] afterModel,
        ParsedCollisionLayout sourceNative,
        ParsedCollisionLayout authoredNative,
        NativeCollisionIndexSnapshot rebuiltIndex,
        IReadOnlyDictionary<int, IReadOnlyList<UnusedLevel65ConstructionCollisionCell>> preservedNativeCells)
    {
        List<UnusedLevel65ConstructionPreservedDegenerateBinding> result = [];
        foreach ((int triangleIndex, IReadOnlyList<UnusedLevel65ConstructionCollisionCell> expectedCells) in
                 preservedNativeCells.OrderBy(pair => pair.Key))
        {
            (string ExpectedHex, int ExpectedAssignment, int ExpectedFlags, long[] ExpectedLookupWadOffsets, long[] ExpectedAuthoredLookupWadOffsets) expected =
                triangleIndex switch
                {
                    1_295 => (
                        "70D91400F919050060020000",
                        13,
                        0x3F,
                        [0x6A51838, 0x6A51AEC],
                        [0x6A51868, 0x6A51B1E]),
                    1_298 => (
                        "3C190D1AAF99122560020000",
                        13,
                        0x3F,
                        [0x6A51832],
                        [0x6A51862]),
                    11_184 => (
                        "1722244872A374E9E0010000",
                        0xFF,
                        0xFF,
                        [0x6A4D5F8],
                        [0x6A4D626]),
                    _ => throw new InvalidDataException(
                        $"Construction collision preservation has no exact native contract for triangle {triangleIndex}.")
                };

            int sourceTriangleOffset = checked(sourceNative.TriangleTableOffset + (triangleIndex * 12));
            int authoredTriangleOffset = checked(authoredNative.TriangleTableOffset + (triangleIndex * 12));
            byte[] sourceTriangleBytes = beforeModel.AsSpan(sourceTriangleOffset, 12).ToArray();
            byte[] authoredTriangleBytes = afterModel.AsSpan(authoredTriangleOffset, 12).ToArray();
            int sourceAssignment = beforeModel[sourceNative.AssignmentsOffset + triangleIndex];
            int authoredAssignment = afterModel[authoredNative.AssignmentsOffset + triangleIndex];
            int sourceFlags = triangleIndex < CollisionFlagsByteLength
                ? beforeModel[sourceNative.FlagsOffset + triangleIndex]
                : 0xFF;
            int authoredFlags = triangleIndex < CollisionFlagsByteLength
                ? afterModel[authoredNative.FlagsOffset + triangleIndex]
                : 0xFF;
            long[] sourceLookupWadOffsets = FindCollisionTriangleReferenceByteOffsets(
                    beforeModel.AsSpan(sourceNative.BlocksOffset, sourceNative.BlocksCapacityBytes),
                    triangleIndex)
                .Select(offset => sourceNative.BlocksWadOffset + offset)
                .ToArray();
            UnusedLevel65ConstructionCollisionCell[] authoredCells = FindCollisionIndexCells(
                rebuiltIndex,
                triangleIndex);
            long[] authoredLookupWadOffsets = FindCollisionTriangleReferenceByteOffsets(
                    rebuiltIndex.BlockBytes,
                    triangleIndex)
                .Select(offset => authoredNative.BlocksWadOffset + offset)
                .ToArray();

            if (!Convert.ToHexString(sourceTriangleBytes).Equals(expected.ExpectedHex, StringComparison.Ordinal) ||
                !authoredTriangleBytes.SequenceEqual(sourceTriangleBytes) ||
                sourceAssignment != expected.ExpectedAssignment ||
                authoredAssignment != sourceAssignment ||
                sourceFlags != expected.ExpectedFlags ||
                authoredFlags != sourceFlags ||
                !sourceLookupWadOffsets.SequenceEqual(expected.ExpectedLookupWadOffsets) ||
                !authoredCells.SequenceEqual(expectedCells) ||
                !authoredLookupWadOffsets.SequenceEqual(expected.ExpectedAuthoredLookupWadOffsets))
            {
                throw new InvalidDataException(
                    $"Native degenerate collision triangle {triangleIndex} was not preserved exactly through the ordered construction index repack.");
            }

            result.Add(new UnusedLevel65ConstructionPreservedDegenerateBinding(
                triangleIndex,
                expected.ExpectedHex,
                sourceAssignment,
                sourceFlags,
                expectedCells.ToArray(),
                sourceLookupWadOffsets,
                authoredCells,
                authoredLookupWadOffsets));
        }

        return result.ToArray();
    }

    private static IReadOnlyList<UnusedLevel65ConstructionCollisionCell> CollisionTouchedCells(
        IReadOnlyList<UnusedLevel65ConstructionPoint> points)
    {
        int minX = points.Min(point => point.X) >> 8;
        int maxX = points.Max(point => point.X) >> 8;
        int minY = points.Min(point => point.Y) >> 8;
        int maxY = points.Max(point => point.Y) >> 8;
        int minZ = points.Min(point => point.Z) >> 8;
        int maxZ = points.Max(point => point.Z) >> 8;
        List<UnusedLevel65ConstructionCollisionCell> result = [];
        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (CollisionTriangleTouchesBlock(points, x, y, z))
                        result.Add(new(x, y, z));
                }
            }
        }
        return result;
    }

    private static bool CollisionTriangleTouchesBlock(
        IReadOnlyList<UnusedLevel65ConstructionPoint> points,
        int x,
        int y,
        int z)
    {
        int blockX1 = x << 8;
        int blockX2 = (x + 1) << 8;
        int blockY1 = y << 8;
        int blockY2 = (y + 1) << 8;
        int blockZ1 = z << 8;
        int blockZ2 = (z + 1) << 8;
        foreach (UnusedLevel65ConstructionPoint point in points)
        {
            if (point.X >= blockX1 && point.X < blockX2 &&
                point.Y >= blockY1 && point.Y < blockY2 &&
                point.Z >= blockZ1 && point.Z < blockZ2)
            {
                return true;
            }
        }

        for (int p = 0; p < 3; p++)
        {
            UnusedLevel65ConstructionPoint current = points[p];
            UnusedLevel65ConstructionPoint next = points[(p + 1) % 3];
            if (next.X != current.X)
            {
                foreach (int testPlane in new[] { blockX1, blockX2 })
                {
                    if ((current.X >= testPlane && next.X <= testPlane) ||
                        (current.X <= testPlane && next.X >= testPlane))
                    {
                        int testY = current.Y + (((next.Y - current.Y) * (testPlane - current.X)) / (next.X - current.X));
                        int testZ = current.Z + (((next.Z - current.Z) * (testPlane - current.X)) / (next.X - current.X));
                        if (testY >= blockY1 && testY < blockY2 && testZ >= blockZ1 && testZ < blockZ2)
                            return true;
                    }
                }
            }
            if (next.Y != current.Y)
            {
                foreach (int testPlane in new[] { blockY1, blockY2 })
                {
                    if ((current.Y >= testPlane && next.Y <= testPlane) ||
                        (current.Y <= testPlane && next.Y >= testPlane))
                    {
                        int testX = current.X + (((next.X - current.X) * (testPlane - current.Y)) / (next.Y - current.Y));
                        int testZ = current.Z + (((next.Z - current.Z) * (testPlane - current.Y)) / (next.Y - current.Y));
                        if (testX >= blockX1 && testX < blockX2 && testZ >= blockZ1 && testZ < blockZ2)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static IReadOnlyList<IReadOnlyList<int>> ParseOcclusionGroups(
        byte[] model,
        int componentOffset,
        int componentByteLength,
        int sceneSectorCount)
    {
        RequireRange(model, componentOffset, componentByteLength, "occlusion component");
        if (ReadInt32(model, componentOffset) != componentByteLength)
            throw new InvalidDataException("The construction occlusion component length changed.");
        int environmentPortionLength = ReadInt32(model, componentOffset + 4);
        int environmentPortionEnd = checked(componentOffset + 4 + environmentPortionLength);
        int groupCount = ReadInt32(model, componentOffset + 8);
        int pointerTableStart = checked(componentOffset + 12);
        if (environmentPortionLength < 8 ||
            environmentPortionEnd > componentOffset + componentByteLength ||
            groupCount < 0 || groupCount > 256 ||
            pointerTableStart + (groupCount * 4L) > environmentPortionEnd)
        {
            throw new InvalidDataException("The construction occlusion group directory is invalid.");
        }

        List<IReadOnlyList<int>> groups = new(groupCount);
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            int relative = ReadInt32(model, pointerTableStart + (groupIndex * 4));
            int groupStart = checked(componentOffset + 4 + relative);
            if (groupStart < pointerTableStart + (groupCount * 4) || groupStart >= environmentPortionEnd)
                throw new InvalidDataException($"Construction occlusion group {groupIndex} points outside its component.");
            List<int> sectors = [];
            bool terminated = false;
            for (int offset = groupStart; offset < environmentPortionEnd; offset++)
            {
                int sectorIndex = model[offset];
                if (sectorIndex == 0xFF)
                {
                    terminated = true;
                    break;
                }
                if (sectorIndex >= sceneSectorCount)
                    throw new InvalidDataException($"Construction occlusion group {groupIndex} references sector {sectorIndex}.");
                sectors.Add(sectorIndex);
            }
            if (!terminated)
                throw new InvalidDataException($"Construction occlusion group {groupIndex} has no terminator.");
            groups.Add(sectors);
        }
        return groups;
    }

    private static void RequireCollisionCompositionDiff(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after,
        ParsedCollisionLayout layout,
        int triangleIndex,
        NativeCollisionIndexRepack repackedIndex)
    {
        if (before.Length != after.Length || before.Length != CollisionComponentByteLength)
            throw new InvalidDataException("The collision composition changed the fixed component length.");
        int componentOffset = layout.ComponentOffset;
        int treeStart = layout.TreeOffset - componentOffset;
        int treeEnd = treeStart + layout.TreeCapacityBytes;
        int blocksStart = layout.BlocksOffset - componentOffset;
        int blocksEnd = blocksStart + layout.BlocksCapacityBytes;
        int triangleStart = (layout.TriangleTableOffset - componentOffset) + (triangleIndex * 12);
        int triangleEnd = triangleStart + 12;
        int assignment = (layout.AssignmentsOffset - componentOffset) + triangleIndex;
        for (int index = 0; index < before.Length; index++)
        {
            bool allowed = (index >= treeStart && index < treeEnd) ||
                           (index >= blocksStart && index < blocksEnd) ||
                           (index >= triangleStart && index < triangleEnd) ||
                           index == assignment;
            if (!allowed && before[index] != after[index])
            {
                throw new InvalidDataException(
                    $"Collision composition changed an unowned byte at component offset 0x{index:X}.");
            }
        }
        if (repackedIndex.TreeBytes.Length != layout.TreeCapacityBytes ||
            repackedIndex.BlockBytes.Length != layout.BlocksCapacityBytes ||
            repackedIndex.UsedBlockBytes > layout.BlocksCapacityBytes)
            throw new InvalidDataException("The ordered collision repack escaped its exact fixed native capacities.");
    }

    private static void WriteVertexWords(byte[] destination, ref int offset, IReadOnlyList<uint> words)
    {
        foreach (uint word in words)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), word);
            offset += 4;
        }
    }

    private static int[] ReadPackedSixBitSlots(uint word) =>
    [
        (int)((word >> 26) & 0x3F),
        (int)((word >> 20) & 0x3F),
        (int)((word >> 14) & 0x3F),
        (int)((word >> 8) & 0x3F)
    ];

    private static void RequireUInt32(byte[] bytes, int offset, long expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        if (actual != expected)
            throw new InvalidDataException($"The ID65 {label} is 0x{actual:X}, expected 0x{expected:X}.");
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void RequireRange(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || (long)offset + length > bytes.Length)
            throw new InvalidDataException($"The ID65 {label} is outside the fixed model subfile.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsExecutableName(string name) =>
        name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
    }

    private sealed record ParsedComponent(
        string Name,
        long WadOffset,
        int ByteLength,
        string Sha256);

    private sealed record ParsedSector(
        int Index,
        long WadOffset,
        int ByteLength,
        int GapBytesAfter,
        int LowDetailVertexCount,
        int LowDetailFaceCount,
        int HighDetailVertexCount,
        int HighDetailFaceCount);

    private sealed record ParsedCollisionLayout(
        int ComponentOffset,
        long ComponentWadOffset,
        int TriangleCount,
        int TreeOffset,
        int BlocksOffset,
        int TriangleTableOffset,
        int AssignmentsOffset,
        int FlagsOffset,
        int TreeCapacityBytes,
        int BlocksCapacityBytes,
        long BlocksWadOffset);

    private sealed record NativeCollisionCellBinding(
        UnusedLevel65ConstructionCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record NativeCollisionIndexSnapshot(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<UnusedLevel65ConstructionCollisionCell, NativeCollisionCellBinding> Cells,
        int GroupStartCount,
        int TerminalSentinelWordOffset,
        int UsedBlockBytes);

    private sealed record NativeCollisionIndexRepack(
        byte[] TreeBytes,
        byte[] BlockBytes,
        int UsedBlockBytes,
        NativeCollisionIndexSnapshot AuthoredIndex,
        IReadOnlyList<UnusedLevel65ConstructionCollisionCell> ChangedCells,
        bool UnchangedCellSequencesPreserved,
        bool IntendedCellSequencesVerified,
        bool NativeOrderingPreserved);

    private readonly record struct XyPoint(double X, double Y);

    private sealed record DecodedCollisionTriangle(
        int Index,
        uint XWord,
        uint YWord,
        uint ZWord,
        uint ZFlags,
        IReadOnlyList<UnusedLevel65ConstructionPoint> Points);

    private sealed record ParsedModel(
        int TextureCount,
        int SpecialSurfaceCount,
        int CollisionTriangleCount,
        int PortalCount,
        long UsedEndWadOffset,
        int ZeroTailBytes,
        ParsedComponent Environment,
        IReadOnlyList<ParsedComponent> Components,
        IReadOnlyList<ParsedSector> Sectors);
}
