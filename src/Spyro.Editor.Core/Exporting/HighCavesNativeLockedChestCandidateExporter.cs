using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record HighCavesNativeLockedChestCandidateRequest(
    string SourceImagePath,
    string OutputImagePath,
    string WadAnalysisPath,
    string LevelCatalogRootPath,
    bool EnableFastEntry = true);

public sealed record HighCavesNativeLockedChestTextureRegionPlan(
    string Name,
    string SourceTpage,
    int SourceU,
    int SourceV,
    string SourceClut,
    string TargetTpage,
    int TargetU,
    int TargetV,
    string TargetClut,
    string TileSha256,
    string PaletteSha256);

public sealed record HighCavesNativeLockedChestCandidatePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string SourceImagePath,
    string OutputImagePath,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    string OriginalCopyBufferAddress,
    string RelocatedCopyBufferAddress,
    string HandlerPayloadAddress,
    int HandlerPayloadLength,
    string HandlerPayloadSha256,
    int KeyTrueIndex,
    int LockedChestTrueIndex,
    IReadOnlyList<int> RewardMarkerTrueIndices,
    int SourceCountBefore,
    int SourceCountAfter,
    int ScenePointerFixupCountBefore,
    int ScenePointerFixupCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter,
    string LockedChestPackageSha256,
    string RebasedLockedChestPackageSha256,
    int RebasedTexturedFaceCount,
    IReadOnlyList<HighCavesNativeLockedChestTextureRegionPlan> TextureRegions,
    string TextureAllocationProof,
    string FinalOverlaySha256,
    string FinalDataEntrySha256,
    string FinalExecutableSha256,
    string OutputImageSha256,
    bool FastEntryEnabled,
    string FastEntryInstructions,
    string Verification);

public sealed record HighCavesNativeLockedChestCandidateResult(
    string OutputImagePath,
    HighCavesNativeLockedChestCandidatePlan Plan,
    bool Verified,
    long OutputLength,
    string Verification);

/// <summary>
/// Research-only High Caves transplant of Peace Keepers' native Key + Locked Chest
/// source bundle. High Caves already owns the retail Key dispatch, class 0x000D
/// reward-marker behavior, and metal debris actors 0x0135-0x0137. This guarded
/// candidate therefore imports only the missing class 0x00AE handler/model,
/// its four private texture regions, and the seven native source rows required
/// for one Key/Chest pair and its one-shot +10 reward. It is intentionally not
/// registered as a normal editor profile until DuckStation runtime proof exists.
/// </summary>
public static class HighCavesNativeLockedChestCandidateExporter
{
    public const string RecipeId = "highcaves.peacekeepers.nativeLockedChest.disposable.v1";

    private const int SectorBytes = 2048;
    private const int WadLba = 37;
    private const int OriginalWadSize = 0x6927000;
    private const int ExpandedWadSize = 0x6929000;
    private const int OriginalExecutableLba = 53875;
    private const int RelocatedExecutableLba = 53879;
    private const int ExecutableSize = 0x66000;
    private const int NextFileLba = 60000;

    private const int HighCavesOverlayEntryIndex = 37;
    private const long HighCavesOverlayEntryOffset = 0x2CAC800;
    private const int OriginalOverlaySize = 0x12000;
    private const int ExpandedOverlaySize = 0x12800;
    private const uint OverlayLoadAddress = 0x8007AA38;
    private const uint OriginalCopyBufferAddress = 0x8008C73C;
    private const uint RelocatedCopyBufferAddress = 0x8008CE80;

    private const int HighCavesDataEntryIndex = 38;
    private const long HighCavesDataEntryOffset = 0x2CBE800;
    private const int OriginalDataEntrySize = 0x29C800;
    private const int ExpandedDataEntrySize = 0x29E000;
    private const int ActorSubfileIndex = 2;
    private const int ActorSubfileOffset = 0x17A800;
    private const int OriginalActorSubfileSize = 0x45000;
    private const int ActorSubfileGrowth = 0x1800;
    private const int AppendedLockedChestPackageOffset = 0x1BF800;
    private const int ExpandedSceneOffset = 0x1C1000;

    private const uint LockedHandlerAddress = OriginalCopyBufferAddress;
    private const int LockedHandlerLength = 0x740;
    private const int OverlayPayloadOffset = 0x11D04;
    private const int OverlayPayloadLength = LockedHandlerLength;
    private const uint DispatchHookAddress = 0x8007ACD0;
    private const uint NativeKeyDispatchAddress = 0x8007ACCC;
    private const uint NativeRewardDispatchAddress = 0x8007AA4C;
    private const uint HighCavesLoopExitAddress = 0x800887F0;

    private const long LockedSetupWadOffset = 0x188719C;
    private const uint LockedSetupSourceAddress = 0x80083BD4;
    private const int LockedSetupLength = 0x30;
    private const long LockedStateWadOffset = 0x1887330;
    private const uint LockedStateSourceAddress = 0x80083D68;
    private const int LockedStateLength = 0x70C;

    private const long PeaceKeepersDataEntryOffset = 0x1890800;
    private const int PeaceKeepersDataEntrySize = 0x29E800;
    private const int PeaceKeepersSceneOffset = 0x1C2800;
    private const int PeaceKeepersSourceTableRelativeOffset = 0x1CAA60;
    private const int PeaceKeepersLockedChestPackageOffset = 0x1B3978;
    private const int LockedChestPackageLength = 0x1008;

    private const int TexturePagesSubfileOffset = 0x800;
    private const int AddressableTexturePageBytes = 0x80000;
    private const int PackedVramRowBytes = 0x400;
    private const int FullRightHalfByteX = 0x400;

    private const int SourceCountRelativeOffset = 0xD67C;
    private const int SourceTableRelativeOffset = 0xD680;
    private const int RecordStride = 0x58;
    private const int SourceCountBefore = 148;
    private const int SourceCountAfter = 155;
    private const int KeyTrueIndex = 148;
    private const int LockedChestTrueIndex = 149;
    private const int KeyPropsRelativeOffset = 0x10BD0;
    private const int LockedChestPropsRelativeOffset = 0x10BD4;
    private const int RewardPropsRelativeOffset = 0x10BEC;
    private const int ScenePointerFixupCountRelativeOffset = 0x16F70;
    private const int ScenePointerFixupListRelativeOffset = 0x16F74;
    private const int ScenePointerFixupCountBefore = 199;
    private const int ScenePointerFixupCountAfter = 206;

    private const int KeyRawX = 33000;
    private const int KeyRawY = 58000;
    private const int KeyRawZ = 35840;
    private const int LockedChestRawX = 35048;
    private const int LockedChestRawY = 58000;
    private const int LockedChestRawZ = 35840;

    private const int CopyBufferHiFileOffset = 0x4B620;
    private const int CopyBufferLoFileOffset = 0x4B624;
    private const int HighCavesTreasureTableFileOffset = 0x5FC54;
    private const int TreasureTargetBefore = 500;
    private const int TreasureTargetAfter = 510;
    private const int FastEntryFileOffset = 0x1E034;

    private const string ExpectedWadHeaderSha256 = "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd";
    private const string ExpectedRetailBinSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string ExpectedRetailOverlaySha256 = "6c153366d9b3dd6d25a7aa8e8d1ea6708bdf97b04ee35912b11df0f8b9913140";
    private const string ExpectedRetailDataEntrySha256 = "5f8f3189f0695e8710f71cc4056fc5dd677758c0cf26fd291953a650a6ada16c";
    private const string ExpectedActorSubfileSha256 = "7dc68cfad82d49badb34a6bd23a3212b9fb019028c198ba9ea1f4b9b871f75ce";
    private const string ExpectedExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string ExpectedOverlayTailSha256 = "dfd859a4764ffd831a438f33b6be2cd7bc49577bf48b804d4af3fc99554e819a";
    private const string ExpectedLockedSetupSha256 = "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad";
    private const string ExpectedLockedStateSha256 = "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f";
    private const string ExpectedHandlerPayloadSha256 = "33c3a49ca369ce67ba367c4d7f1d018707d430bc3c5357ee7e7cdc4e356d44ac";
    private const string ExpectedLockedChestPackageSha256 = "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private const string ExpectedKeyRowSha256 = "db4fbcd35c285395e2be99ab6205061d9922ea58dccb849675d4c7f514081849";
    private const string ExpectedLockedChestRowSha256 = "84d9f7aeb6a59adeb7a450f673c11f8f65453dab24a34b0242575b5e3baa910a";
    private const string ExpectedExecutableCopyPairSha256 = "749cb9da84f4049e8ca1a962e2d03a85bfe5c0a0c9a75ef83763fadb71e8eb99";

    private static readonly int[] RewardMarkerTrueIndices = [150, 151, 152, 153, 154];

    private static readonly RewardMarkerPlacement[] RewardMarkers =
    [
        new(118, 150, 0x10BEC, -410, 1229, 512, "adeea114e991c3f418677f74ad63db11c026c68a393944b1855fc91bbd518e92"),
        new(119, 151, 0x10C14, 0, 1229, 512, "1ba45fde32011f52fbdf5e8b790142ff675f10703bbd92c0570de30da9500d00"),
        new(120, 152, 0x10C3C, -615, 205, 512, "f631f41d5115bf6c37b345afd651fbb2a83d7cbb881ed063f63c1d687f424b9d"),
        new(121, 153, 0x10C64, -410, 615, 512, "fb1e813085977d7506314bf576c969da306292545747534c72cb73d3427755f3"),
        new(122, 154, 0x10C8C, 204, 819, 512, "2ba846af549e6b158c6a0f0a98fe506b184c88b8c6fbc3ea24140be82e1dc1d8")
    ];

    private static readonly TextureRegion[] TextureRegions =
    [
        new("upper-lock", 0x000E, 192, 192, 0x4A2A, 0x001E, 128, 160, 0x1120,
            "ed3d07124975005d58fc35ed56f3f028e990f8042bb04b5512b5fe2236840c7e",
            "f7fd1c75393d4a46437006d96d9defbfb48eca2b98ab6b561ebab4c12dfef561"),
        new("lower-lock", 0x000E, 192, 224, 0x4C2A, 0x001E, 160, 160, 0x1121,
            "8db3cf16c367cca355e9579e2a4edaf81b39a2b39fab50129e5671ef2f304851",
            "a115024fd9c4050aaf5dd2d5c82ac58351fd4cfa3ec4ff5544a39bbb386f35c7"),
        new("chest-body-a", 0x000F, 0, 160, 0x5C2A, 0x001E, 192, 160, 0x1122,
            "320dd914cf4999c4c49e89b55d49042a2539765acffcd370c14758790a1c27c1",
            "ecb01bf085a6a398dc794f4406dad68cdae42426bea5a8ffa778c5aa079c3f7a"),
        new("chest-body-b", 0x000F, 0, 192, 0x5E2A, 0x001E, 224, 160, 0x1123,
            "c2d356f4ba0f73e9e27dfafe87c2b03f5d6619b30c6bbb5f4b748c926c38d430",
            "fa257923dafd18a44a0bad1f95158cee3ac0eeb98707486144af215568d249db")
    ];

    private static readonly byte[] FastEntryBefore = [0x37, 0xB6, 0x00, 0x08, 0, 0, 0, 0];
    private static readonly byte[] FastEntryAfter = [0x1C, 0x06, 0x84, 0xAF, 0x88, 0x06, 0x80, 0xAF];
    private static readonly byte[] FastEntryFallthrough =
    [
        0x04, 0x00, 0x02, 0x24,
        0x24, 0x00, 0x82, 0x10,
        0x63, 0x00, 0x02, 0x24,
        0x37, 0xB6, 0x00, 0x08
    ];

    public static HighCavesNativeLockedChestCandidateResult Export(HighCavesNativeLockedChestCandidateRequest request)
    {
        ValidateRequest(request);
        string sourcePath = ResolveFilePath(request.SourceImagePath);
        string outputPath = Path.GetFullPath(request.OutputImagePath);
        string analysisPath = Path.GetFullPath(request.WadAnalysisPath);
        string catalogRoot = Path.GetFullPath(request.LevelCatalogRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        try
        {
            DiscLayout layout = DiscImage.DetectLayout(sourcePath);
            if (layout.SectorSize != 2352 || layout.UserOffset != 24)
                throw new InvalidDataException($"{RecipeId} requires the retail MODE2/2352 layout (2352/24).");

            WadArchiveLayout wad = LoadAndGuardWadAnalysis(analysisPath);
            GuardRetailSource(sourcePath, layout, wad);
            string textureProof = GuardTextureAllocation(sourcePath, catalogRoot);

            byte[] finalOverlay;
            byte[] finalData;
            byte[] finalExecutable;
            byte[] handlerPayload;
            byte[] rebasedPackage;
            int rebasedFaces;
            List<HighCavesNativeLockedChestTextureRegionPlan> regionPlans = [];
            IReadOnlyDictionary<int, long> relocatedEntryOffsets;

            using (FileStream retail = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                handlerPayload = BuildHandlerPayload(retail, layout);
                finalOverlay = BuildFinalOverlay(retail, layout, handlerPayload);
                (finalData, rebasedPackage, rebasedFaces) = BuildFinalDataEntry(retail, layout, regionPlans);
                finalExecutable = BuildFinalExecutable(retail, layout, request.EnableFastEntry);
                relocatedEntryOffsets = BuildRelocatedEntryOffsets(wad);
            }

            WriteExpandedImage(
                sourcePath,
                outputPath,
                layout,
                wad,
                relocatedEntryOffsets,
                finalOverlay,
                finalData,
                finalExecutable);

            string verification = VerifyOutput(
                sourcePath,
                outputPath,
                layout,
                wad,
                relocatedEntryOffsets,
                finalOverlay,
                finalData,
                finalExecutable,
                handlerPayload,
                rebasedPackage,
                request.EnableFastEntry);

            HighCavesNativeLockedChestCandidatePlan plan = new(
                GeneratedAt: DateTimeOffset.UtcNow,
                RecipeId: RecipeId,
                SourceImagePath: sourcePath,
                OutputImagePath: outputPath,
                WadGrowthBytes: ExpandedWadSize - OriginalWadSize,
                OriginalExecutableLba: OriginalExecutableLba,
                RelocatedExecutableLba: RelocatedExecutableLba,
                OriginalCopyBufferAddress: $"0x{OriginalCopyBufferAddress:X8}",
                RelocatedCopyBufferAddress: $"0x{RelocatedCopyBufferAddress:X8}",
                HandlerPayloadAddress: $"0x{LockedHandlerAddress:X8}",
                HandlerPayloadLength: handlerPayload.Length,
                HandlerPayloadSha256: Sha256(handlerPayload),
                KeyTrueIndex: KeyTrueIndex,
                LockedChestTrueIndex: LockedChestTrueIndex,
                RewardMarkerTrueIndices: RewardMarkerTrueIndices,
                SourceCountBefore: SourceCountBefore,
                SourceCountAfter: SourceCountAfter,
                ScenePointerFixupCountBefore: ScenePointerFixupCountBefore,
                ScenePointerFixupCountAfter: ScenePointerFixupCountAfter,
                TreasureTargetBefore: TreasureTargetBefore,
                TreasureTargetAfter: TreasureTargetAfter,
                LockedChestPackageSha256: ExpectedLockedChestPackageSha256,
                RebasedLockedChestPackageSha256: Sha256(rebasedPackage),
                RebasedTexturedFaceCount: rebasedFaces,
                TextureRegions: regionPlans,
                TextureAllocationProof: textureProof,
                FinalOverlaySha256: Sha256(finalOverlay),
                FinalDataEntrySha256: Sha256(finalData),
                FinalExecutableSha256: Sha256(finalExecutable),
                OutputImageSha256: Sha256File(outputPath),
                FastEntryEnabled: request.EnableFastEntry,
                FastEntryInstructions: request.EnableFastEntry
                    ? $"Inventory: {TestLevelWarpPatch.ActivationSequence}; then {TestLevelWarpPatch.TargetSelectionText(32)}."
                    : "Disabled.",
                Verification: verification);

            return new HighCavesNativeLockedChestCandidateResult(
                outputPath,
                plan,
                Verified: true,
                OutputLength: new FileInfo(outputPath).Length,
                Verification: verification);
        }
        catch
        {
            DeleteIfExists(outputPath);
            throw;
        }
    }

    private static void GuardRetailSource(string sourcePath, DiscLayout layout, WadArchiveLayout wad)
    {
        string retailSha256 = Sha256File(sourcePath);
        if (!string.Equals(retailSha256, ExpectedRetailBinSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Retail BIN SHA-256 changed: expected {ExpectedRetailBinSha256}, found {retailSha256}.");

        using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(source, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
        DiscFileRecord nextFile = DiscImage.FindRootFileRecord(source, layout, name =>
            name.StartsWith("PETEXA0", StringComparison.OrdinalIgnoreCase));
        if (wadFile.Lba != WadLba || wadFile.Size != OriginalWadSize ||
            executable.Lba != OriginalExecutableLba || executable.Size != ExecutableSize ||
            nextFile.Lba != NextFileLba)
        {
            throw new InvalidDataException("The retail WAD/executable/next-file ISO extents changed.");
        }
        if (RelocatedExecutableLba + DivideRoundUp(ExecutableSize, SectorBytes) > nextFile.Lba)
            throw new InvalidDataException("The relocated executable would overlap the next ISO file.");

        GuardSha256(ReadWad(source, layout, 0, SectorBytes), ExpectedWadHeaderSha256, "retail WAD header");
        GuardSha256(ReadWad(source, layout, HighCavesOverlayEntryOffset, OriginalOverlaySize), ExpectedRetailOverlaySha256, "retail HighCaves overlay");
        GuardSha256(ReadWad(source, layout, HighCavesDataEntryOffset, OriginalDataEntrySize), ExpectedRetailDataEntrySha256, "retail HighCaves data entry");
        GuardSha256(ReadWad(source, layout, HighCavesDataEntryOffset + ActorSubfileOffset, OriginalActorSubfileSize), ExpectedActorSubfileSha256, "retail HighCaves actor/model subfile");
        GuardSha256(DiscImage.ReadFileBytes(source, layout, executable.Lba, 0, executable.Size), ExpectedExecutableSha256, "retail executable");
        GuardSha256(ReadWad(source, layout, HighCavesOverlayEntryOffset + OverlayPayloadOffset, OriginalOverlaySize - OverlayPayloadOffset), ExpectedOverlayTailSha256, "retail HighCaves overlay tail/copy-buffer marker");

        GuardWadWord(source, layout, DispatchHookAddress, HighCavesLoopExitAddress, "retail High Caves class 0xAE table default");
        GuardWadWord(source, layout, NativeKeyDispatchAddress, 0x80083134, "retail High Caves Key table entry");
        GuardWadWord(source, layout, NativeRewardDispatchAddress, 0x8007BA48, "retail High Caves reward table entry");
        GuardWadWord(source, layout, HighCavesLoopExitAddress, 0x8FAA0270, "retail High Caves actor-loop exit");

        byte[] copyPair = DiscImage.ReadFileBytes(source, layout, executable.Lba, CopyBufferHiFileOffset, 8);
        GuardSha256(copyPair, ExpectedExecutableCopyPairSha256, "retail HighCaves copy-buffer pair");
        GuardWord(copyPair, 0, 0x3C028009, "retail HighCaves copy-buffer HI16");
        GuardWord(copyPair, 4, 0x2442C73C, "retail High Caves copy-buffer LO16");
        if (BinaryPrimitives.ReadUInt16LittleEndian(
                DiscImage.ReadFileBytes(source, layout, executable.Lba, HighCavesTreasureTableFileOffset, 2)) != TreasureTargetBefore)
        {
            throw new InvalidDataException("The High Caves treasure target is no longer the retail value 500.");
        }

        WadEntryLayout overlay = wad.ByIndex[HighCavesOverlayEntryIndex];
        WadEntryLayout data = wad.ByIndex[HighCavesDataEntryIndex];
        if (overlay.Offset != HighCavesOverlayEntryOffset || overlay.Size != OriginalOverlaySize ||
            data.Offset != HighCavesDataEntryOffset || data.Size != OriginalDataEntrySize)
        {
            throw new InvalidDataException("The WAD analysis no longer identifies the exact HighCaves overlay/data entries.");
        }
    }

    private static string GuardTextureAllocation(string sourcePath, string catalogRoot)
    {
        LevelCatalog catalog = LevelCatalog.Load(catalogRoot);
        LevelDefinition level = catalog.FindByKey("highcaves") ??
            throw new InvalidDataException("The level catalog does not contain High Caves.");
        if (level.SourceWadEntry != HighCavesDataEntryIndex || level.LevelId != 32)
            throw new InvalidDataException("The High Caves level catalog mapping changed.");

        NativeTexturePageOwnershipReport report = NativeTexturePageOwnershipScanner.Scan(sourcePath, level);
        if (!report.AllConsumerClosureComplete || !report.ProvablyPrivatePixelAndClutSpace ||
            report.AddressableTexturePageByteLength != AddressableTexturePageBytes ||
            report.TexturePagesWadOffset != HighCavesDataEntryOffset + TexturePagesSubfileOffset)
        {
            throw new InvalidDataException(
                $"HighCaves texture ownership is not closed for private allocation: {string.Join("; ", report.SafetyBlockers)}");
        }

        HashSet<int> selected = [];
        foreach (TextureRegion region in TextureRegions)
        {
            foreach (int offset in BuildTileOffsets(region.TargetTpage, region.TargetU, region.TargetV)
                .Concat(BuildPaletteOffsets(region.TargetClut)))
            {
                if (!selected.Add(offset))
                    throw new InvalidDataException($"Private HighCaves texture allocation overlaps itself at 0x{offset:X}.");
                NativeTexturePageOwnedRange? protectedRange = report.ProtectedRanges.FirstOrDefault(range =>
                    offset >= range.Offset && offset < range.Offset + range.Length);
                if (protectedRange != null)
                {
                    throw new InvalidDataException(
                        $"Private HighCaves texture allocation touches protected {protectedRange.Owner} at 0x{offset:X}.");
                }
            }
        }
        return $"Source-bound ownership closure is complete; {selected.Count:N0} distinct target bytes are outside every protected range and are guarded zero before write.";
    }

    private static byte[] BuildHandlerPayload(FileStream retail, DiscLayout layout)
    {
        byte[] setup = ReadWad(retail, layout, LockedSetupWadOffset, LockedSetupLength);
        byte[] state = ReadWad(retail, layout, LockedStateWadOffset, LockedStateLength);
        GuardSha256(setup, ExpectedLockedSetupSha256, "Peace Keepers Locked Chest setup");
        GuardSha256(state, ExpectedLockedStateSha256, "Peace Keepers Locked Chest state/open path");

        CodeSegment[] lockedSegments =
        [
            new(LockedSetupSourceAddress, LockedHandlerAddress + 4, setup),
            new(LockedStateSourceAddress, LockedHandlerAddress + 4 + LockedSetupLength, state)
        ];
        byte[] relocatedBody = RelocateCode(
            lockedSegments,
            new Dictionary<uint, uint> { [0x8008A20C] = HighCavesLoopExitAddress });
        byte[] lockedPayload = Concat(Words(0x0260A021), relocatedBody);
        if (lockedPayload.Length != LockedHandlerLength)
            throw new InvalidDataException($"Relocated Locked Chest payload is 0x{lockedPayload.Length:X}, expected 0x{LockedHandlerLength:X}.");

        if (lockedPayload.Length != OverlayPayloadLength)
            throw new InvalidDataException($"High Caves handler payload is 0x{lockedPayload.Length:X}, expected 0x{OverlayPayloadLength:X}.");
        GuardSha256(lockedPayload, ExpectedHandlerPayloadSha256, "High Caves handler payload");
        return lockedPayload;
    }

    private static byte[] BuildFinalOverlay(FileStream retail, DiscLayout layout, byte[] handlerPayload)
    {
        byte[] original = ReadWad(retail, layout, HighCavesOverlayEntryOffset, OriginalOverlaySize);
        byte[] expanded = new byte[ExpandedOverlaySize];
        original.CopyTo(expanded, 0);
        Array.Clear(expanded, OverlayPayloadOffset, expanded.Length - OverlayPayloadOffset);
        handlerPayload.CopyTo(expanded, OverlayPayloadOffset);

        WriteWord(expanded, OverlayRelativeOffset(DispatchHookAddress), LockedHandlerAddress);
        GuardWord(expanded, OverlayRelativeOffset(NativeKeyDispatchAddress), 0x80083134, "preserved High Caves Key table entry");
        GuardWord(expanded, OverlayRelativeOffset(NativeRewardDispatchAddress), 0x8007BA48, "preserved High Caves reward table entry");
        if (expanded.AsSpan(OverlayPayloadOffset + handlerPayload.Length).ToArray().Any(value => value != 0))
            throw new InvalidDataException("Expanded HighCaves overlay padding is not zero.");
        return expanded;
    }

    private static (byte[] Entry, byte[] RebasedPackage, int RebasedFaces) BuildFinalDataEntry(
        FileStream retail,
        DiscLayout layout,
        List<HighCavesNativeLockedChestTextureRegionPlan> regionPlans)
    {
        byte[] original = ReadWad(retail, layout, HighCavesDataEntryOffset, OriginalDataEntrySize);
        GuardUInt32(original, SourceCountRelativeOffset + 0x1BF800, SourceCountBefore, "retail High Caves source count");

        byte[] donorPackage = ReadWad(
            retail,
            layout,
            PeaceKeepersDataEntryOffset + PeaceKeepersLockedChestPackageOffset,
            LockedChestPackageLength);
        GuardSha256(donorPackage, ExpectedLockedChestPackageSha256, "Peace Keepers actor 0x00AE package");
        byte[] rebasedPackage = donorPackage.ToArray();
        int rebasedFaces = RebaseLockedChestDescriptors(rebasedPackage);
        if (rebasedFaces != 146)
            throw new InvalidDataException($"Rebased {rebasedFaces} Locked Chest faces, expected exactly 146.");

        byte[] paddedPackage = new byte[ActorSubfileGrowth];
        rebasedPackage.CopyTo(paddedPackage, 0);
        NativeSkyRelocationPayload payload = new(
            PatchIndex: 0,
            WadLba: WadLba,
            OriginalWadOffset: HighCavesDataEntryOffset + ActorSubfileOffset + OriginalActorSubfileSize,
            StorageWadEntry: HighCavesDataEntryIndex,
            ModelBlockOffset: OriginalActorSubfileSize,
            OriginalLength: 0,
            Bytes: paddedPackage,
            SubfileIndex: ActorSubfileIndex,
            RequireLengthPrefix: false);
        byte[] expanded = NativeSkyWadRelocator.ExpandNestedLevelEntry(original, [payload], ActorSubfileGrowth);
        if (expanded.Length != ExpandedDataEntrySize)
            throw new InvalidDataException($"Expanded HighCaves data entry is 0x{expanded.Length:X}, expected 0x{ExpandedDataEntrySize:X}.");
        GuardUInt32(expanded, (ActorSubfileIndex * 8) + 0, ActorSubfileOffset, "expanded actor/model subfile offset");
        GuardUInt32(expanded, (ActorSubfileIndex * 8) + 4, OriginalActorSubfileSize + ActorSubfileGrowth, "expanded actor/model subfile size");
        GuardUInt32(expanded, ((ActorSubfileIndex + 1) * 8) + 0, ExpandedSceneOffset, "expanded scene subfile offset");
        GuardEqual(expanded.AsSpan(AppendedLockedChestPackageOffset, ActorSubfileGrowth).ToArray(), paddedPackage, "appended Locked Chest package/padding");

        GuardBlank(expanded.AsSpan(0xF4, 4).ToArray(), "High Caves actor root slot 0xF4");
        GuardBlank(expanded.AsSpan(0x1A2, 2).ToArray(), "High Caves actor id slot 0x1A2");
        WriteUInt32(expanded, 0xF4, AppendedLockedChestPackageOffset);
        WriteUInt16(expanded, 0x1A2, 0x00AE);

        ImportSourceRowsAndProps(retail, layout, expanded);
        ImportPrivateTextures(retail, layout, expanded, regionPlans);
        rebasedPackage.CopyTo(expanded, AppendedLockedChestPackageOffset);
        return (expanded, rebasedPackage, rebasedFaces);
    }

    private static void ImportSourceRowsAndProps(FileStream retail, DiscLayout layout, byte[] expanded)
    {
        int sourceCountOffset = ExpandedSceneOffset + SourceCountRelativeOffset;
        int sourceTableOffset = ExpandedSceneOffset + SourceTableRelativeOffset;
        int pointerFixupCountOffset = ExpandedSceneOffset + ScenePointerFixupCountRelativeOffset;
        int pointerFixupListOffset = ExpandedSceneOffset + ScenePointerFixupListRelativeOffset;

        GuardUInt32(expanded, sourceCountOffset, SourceCountBefore, "expanded HighCaves source count");
        GuardBlank(expanded.AsSpan(sourceTableOffset + (KeyTrueIndex * RecordStride),
            (SourceCountAfter - SourceCountBefore) * RecordStride).ToArray(), "High Caves T148-T154 source rows");

        byte[] keyRow = ReadPeaceSourceRow(retail, layout, 78, ExpectedKeyRowSha256, "Peace Keepers T78 Key");
        byte[] chestRow = ReadPeaceSourceRow(retail, layout, 79, ExpectedLockedChestRowSha256, "Peace Keepers T79 Locked Chest");
        PatchSourceRow(keyRow, KeyPropsRelativeOffset, KeyRawX, KeyRawY, KeyRawZ);
        PatchSourceRow(chestRow, LockedChestPropsRelativeOffset, LockedChestRawX, LockedChestRawY, LockedChestRawZ);
        keyRow.CopyTo(expanded, sourceTableOffset + (KeyTrueIndex * RecordStride));
        chestRow.CopyTo(expanded, sourceTableOffset + (LockedChestTrueIndex * RecordStride));

        byte[] keyProps = ReadPeaceProps(retail, layout, 0xEA94, 4);
        byte[] chestProps = ReadPeaceProps(retail, layout, 0xEA98, 0x18);
        GuardBlank(keyProps, "Peace Keepers Key props");
        GuardUInt32(chestProps, 0, 78, "Peace Keepers Locked Chest owner");
        WriteUInt32(chestProps, 0, KeyTrueIndex);
        CopyGuardedBlank(expanded, ExpandedSceneOffset + KeyPropsRelativeOffset, keyProps, "HighCaves Key props");
        CopyGuardedBlank(expanded, ExpandedSceneOffset + LockedChestPropsRelativeOffset, chestProps, "HighCaves Locked Chest props");

        foreach (RewardMarkerPlacement marker in RewardMarkers)
        {
            byte[] row = ReadPeaceSourceRow(retail, layout, marker.DonorTrueIndex, marker.DonorRowSha256,
                $"Peace Keepers T{marker.DonorTrueIndex} reward marker");
            int x = LockedChestRawX + marker.DeltaX;
            int y = LockedChestRawY + marker.DeltaY;
            int z = LockedChestRawZ + marker.DeltaZ;
            PatchSourceRow(row, marker.PropsRelativeOffset, x, y, z);
            row.CopyTo(expanded, sourceTableOffset + (marker.OutputTrueIndex * RecordStride));

            int donorPropsPointer = marker.DonorTrueIndex switch
            {
                118 => 0xEF9C,
                119 => 0xEFC4,
                120 => 0xEFEC,
                121 => 0xF014,
                122 => 0xF03C,
                _ => throw new InvalidDataException("Unexpected reward marker donor.")
            };
            byte[] props = ReadPeaceProps(retail, layout, donorPropsPointer, 0x28);
            GuardUInt32(props, 0, 79, $"Peace Keepers T{marker.DonorTrueIndex} reward owner");
            WriteUInt32(props, 0, LockedChestTrueIndex);
            WriteInt32(props, 4, x);
            WriteInt32(props, 8, y);
            WriteInt32(props, 12, z);
            CopyGuardedBlank(expanded, ExpandedSceneOffset + marker.PropsRelativeOffset, props,
                $"HighCaves T{marker.OutputTrueIndex} reward props");
        }

        WriteUInt32(expanded, sourceCountOffset, SourceCountAfter);
        GuardUInt32(expanded, pointerFixupCountOffset, ScenePointerFixupCountBefore, "retail HighCaves scene pointer-fixup count");
        int appendFixupOffset = pointerFixupListOffset + (ScenePointerFixupCountBefore * 4);
        GuardBlank(expanded.AsSpan(appendFixupOffset, (ScenePointerFixupCountAfter - ScenePointerFixupCountBefore) * 4).ToArray(),
            "HighCaves appended scene pointer-fixup slots");
        for (int index = KeyTrueIndex; index < SourceCountAfter; index++)
        {
            WriteUInt32(expanded, appendFixupOffset + ((index - KeyTrueIndex) * 4),
                checked((uint)(SourceTableRelativeOffset + (index * RecordStride))));
        }
        WriteUInt32(expanded, pointerFixupCountOffset, ScenePointerFixupCountAfter);
    }

    private static void ImportPrivateTextures(
        FileStream retail,
        DiscLayout layout,
        byte[] expanded,
        List<HighCavesNativeLockedChestTextureRegionPlan> regionPlans)
    {
        byte[] donorTexturePages = ReadWad(
            retail,
            layout,
            PeaceKeepersDataEntryOffset + TexturePagesSubfileOffset,
            AddressableTexturePageBytes);
        foreach (TextureRegion region in TextureRegions)
        {
            int[] sourceTile = BuildTileOffsets(region.SourceTpage, region.SourceU, region.SourceV);
            int[] targetTile = BuildTileOffsets(region.TargetTpage, region.TargetU, region.TargetV);
            int[] sourcePalette = BuildPaletteOffsets(region.SourceClut);
            int[] targetPalette = BuildPaletteOffsets(region.TargetClut);
            byte[] donorTile = sourceTile.Select(offset => donorTexturePages[offset]).ToArray();
            byte[] donorPalette = sourcePalette.Select(offset => donorTexturePages[offset]).ToArray();
            GuardSha256(donorTile, region.TileSha256, $"{region.Name} donor tile");
            GuardSha256(donorPalette, region.PaletteSha256, $"{region.Name} donor palette");
            GuardBlank(targetTile.Select(offset => expanded[TexturePagesSubfileOffset + offset]).ToArray(), $"{region.Name} private target tile");
            GuardBlank(targetPalette.Select(offset => expanded[TexturePagesSubfileOffset + offset]).ToArray(), $"{region.Name} private target palette");
            for (int index = 0; index < sourceTile.Length; index++)
                expanded[TexturePagesSubfileOffset + targetTile[index]] = donorTexturePages[sourceTile[index]];
            for (int index = 0; index < sourcePalette.Length; index++)
                expanded[TexturePagesSubfileOffset + targetPalette[index]] = donorTexturePages[sourcePalette[index]];

            regionPlans.Add(new HighCavesNativeLockedChestTextureRegionPlan(
                region.Name,
                $"0x{region.SourceTpage:X4}", region.SourceU, region.SourceV, $"0x{region.SourceClut:X4}",
                $"0x{region.TargetTpage:X4}", region.TargetU, region.TargetV, $"0x{region.TargetClut:X4}",
                region.TileSha256, region.PaletteSha256));
        }
    }

    private static byte[] BuildFinalExecutable(FileStream retail, DiscLayout layout, bool enableFastEntry)
    {
        DiscFileRecord executable = DiscImage.FindRootFileRecord(retail, layout, IsExecutableName);
        byte[] bytes = DiscImage.ReadFileBytes(retail, layout, executable.Lba, 0, executable.Size);
        GuardWord(bytes, CopyBufferHiFileOffset, 0x3C028009, "retail HighCaves copy-buffer HI16");
        GuardWord(bytes, CopyBufferLoFileOffset, 0x2442C73C, "retail High Caves copy-buffer LO16");
        (uint high, uint low) = EncodeAddiuAddressPair(2, RelocatedCopyBufferAddress);
        WriteWord(bytes, CopyBufferHiFileOffset, high);
        WriteWord(bytes, CopyBufferLoFileOffset, low);

        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(HighCavesTreasureTableFileOffset, 2)) != TreasureTargetBefore)
            throw new InvalidDataException("The High Caves treasure target is no longer 500.");
        WriteUInt16(bytes, HighCavesTreasureTableFileOffset, TreasureTargetAfter);

        GuardEqual(bytes.AsSpan(FastEntryFileOffset, FastEntryBefore.Length).ToArray(), FastEntryBefore, "fast-entry preimage");
        GuardEqual(bytes.AsSpan(FastEntryFileOffset + FastEntryBefore.Length, FastEntryFallthrough.Length).ToArray(), FastEntryFallthrough, "fast-entry fallthrough");
        if (enableFastEntry)
            FastEntryAfter.CopyTo(bytes, FastEntryFileOffset);
        return bytes;
    }

    private static IReadOnlyDictionary<int, long> BuildRelocatedEntryOffsets(WadArchiveLayout wad)
    {
        Dictionary<int, long> result = new();
        long originalCursor = SectorBytes;
        long relocatedCursor = SectorBytes;
        foreach (WadEntryLayout entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            if (entry.Offset != originalCursor)
                throw new InvalidDataException($"WAD entry {entry.Index} is not packed directly after its predecessor.");
            result[entry.Index] = relocatedCursor;
            originalCursor = checked(originalCursor + entry.Size);
            int growth = entry.Index switch
            {
                HighCavesOverlayEntryIndex => ExpandedOverlaySize - OriginalOverlaySize,
                HighCavesDataEntryIndex => ExpandedDataEntrySize - OriginalDataEntrySize,
                _ => 0
            };
            relocatedCursor = checked(relocatedCursor + entry.Size + growth);
        }
        if (originalCursor != OriginalWadSize || relocatedCursor != ExpandedWadSize)
            throw new InvalidDataException($"Combined WAD relocation balanced to 0x{relocatedCursor:X}, expected 0x{ExpandedWadSize:X}.");
        if (result[HighCavesOverlayEntryIndex] != HighCavesOverlayEntryOffset ||
            result[HighCavesDataEntryIndex] != HighCavesDataEntryOffset + (ExpandedOverlaySize - OriginalOverlaySize))
        {
            throw new InvalidDataException("HighCaves entry relocation offsets changed unexpectedly.");
        }
        return result;
    }

    private static void WriteExpandedImage(
        string sourcePath,
        string outputPath,
        DiscLayout layout,
        WadArchiveLayout wad,
        IReadOnlyDictionary<int, long> relocatedEntryOffsets,
        byte[] finalOverlay,
        byte[] finalData,
        byte[] finalExecutable)
    {
        File.Copy(sourcePath, outputPath, true);
        using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(source, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
        byte[] wadHeader = ReadWad(source, layout, 0, SectorBytes);

        foreach (WadEntryLayout entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            byte[] bytes = entry.Index switch
            {
                HighCavesOverlayEntryIndex => finalOverlay,
                HighCavesDataEntryIndex => finalData,
                _ => ReadWad(source, layout, entry.Offset, entry.Size)
            };
            long relocatedOffset = relocatedEntryOffsets[entry.Index];
            WriteUInt32(wadHeader, entry.Index * 8, checked((uint)relocatedOffset));
            WriteUInt32(wadHeader, (entry.Index * 8) + 4, checked((uint)bytes.Length));
            DiscImage.WriteFileBytes(output, layout, WadLba, relocatedOffset, bytes);
        }
        DiscImage.WriteFileBytes(output, layout, WadLba, 0, wadHeader);
        DiscImage.WriteFileBytes(output, layout, RelocatedExecutableLba, 0, finalExecutable);

        byte[] rootDirectory = DiscImage.ReadFileBytes(output, layout, layout.RootExtent, 0, layout.RootLength);
        PatchRootRecord(rootDirectory, wadFile.Name, WadLba, ExpandedWadSize);
        PatchRootRecord(rootDirectory, executable.Name, RelocatedExecutableLba, ExecutableSize);
        DiscImage.WriteFileBytes(output, layout, layout.RootExtent, 0, rootDirectory);
        output.Flush();
    }

    private static string VerifyOutput(
        string sourcePath,
        string outputPath,
        DiscLayout layout,
        WadArchiveLayout wad,
        IReadOnlyDictionary<int, long> relocatedEntryOffsets,
        byte[] finalOverlay,
        byte[] finalData,
        byte[] finalExecutable,
        byte[] handlerPayload,
        byte[] rebasedPackage,
        bool fastEntryEnabled)
    {
        using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(output, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        if (wadFile.Lba != WadLba || wadFile.Size != ExpandedWadSize ||
            executable.Lba != RelocatedExecutableLba || executable.Size != ExecutableSize)
        {
            throw new InvalidDataException("Expanded WAD/relocated executable ISO records failed readback.");
        }
        if (new FileInfo(sourcePath).Length != new FileInfo(outputPath).Length)
            throw new InvalidDataException("The HighCaves candidate changed the raw disc-image length.");

        byte[] header = ReadWad(output, layout, 0, SectorBytes);
        foreach (WadEntryLayout entry in wad.Entries)
        {
            long expectedOffset = relocatedEntryOffsets[entry.Index];
            int expectedSize = entry.Index switch
            {
                HighCavesOverlayEntryIndex => ExpandedOverlaySize,
                HighCavesDataEntryIndex => ExpandedDataEntrySize,
                _ => entry.Size
            };
            if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(entry.Index * 8, 4)) != expectedOffset ||
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan((entry.Index * 8) + 4, 4)) != expectedSize)
            {
                throw new InvalidDataException($"Relocated WAD header entry {entry.Index} failed readback.");
            }
        }

        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[HighCavesOverlayEntryIndex], finalOverlay.Length), finalOverlay, "expanded HighCaves overlay readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[HighCavesDataEntryIndex], finalData.Length), finalData, "expanded HighCaves data readback");
        GuardEqual(DiscImage.ReadFileBytes(output, layout, executable.Lba, 0, executable.Size), finalExecutable, "relocated executable readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[HighCavesOverlayEntryIndex] + OverlayPayloadOffset, handlerPayload.Length), handlerPayload, "Locked Chest handler payload readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[HighCavesDataEntryIndex] + AppendedLockedChestPackageOffset, rebasedPackage.Length), rebasedPackage, "Locked Chest package readback");

        foreach (WadEntryLayout entry in wad.Entries.Where(entry => entry.Index is not HighCavesOverlayEntryIndex and not HighCavesDataEntryIndex))
        {
            byte[] before = ReadWad(source, layout, entry.Offset, entry.Size);
            byte[] after = ReadWad(output, layout, relocatedEntryOffsets[entry.Index], entry.Size);
            GuardEqual(after, before, $"unrelated WAD entry {entry.Index}");
        }

        if (!CrossLevelActorPackageLayoutSafety.TryReadLayout(
                output,
                layout,
                WadLba,
                relocatedEntryOffsets[HighCavesDataEntryIndex],
                out CrossLevelActorPackageSubfileLayout? actorLayout,
                out string actorReason) ||
            actorLayout == null || actorLayout.SubfileIndex != ActorSubfileIndex ||
            actorLayout.Start != ActorSubfileOffset || actorLayout.EndExclusive != ExpandedSceneOffset ||
            actorLayout.NativeRootCount != 42 || actorLayout.LastNativeRoot != AppendedLockedChestPackageOffset)
        {
            throw new InvalidDataException($"Expanded HighCaves actor/model layout failed validation: {actorReason}");
        }

        GuardWord(finalExecutable, CopyBufferHiFileOffset, 0x3C028009, "relocated copy-buffer HI16 readback");
        GuardWord(finalExecutable, CopyBufferLoFileOffset, 0x2442CE80, "relocated copy-buffer LO16 readback");
        if (BinaryPrimitives.ReadUInt16LittleEndian(finalExecutable.AsSpan(HighCavesTreasureTableFileOffset, 2)) != TreasureTargetAfter)
            throw new InvalidDataException("High Caves treasure target did not read back as 510.");
        GuardEqual(
            finalExecutable.AsSpan(FastEntryFileOffset, FastEntryBefore.Length).ToArray(),
            fastEntryEnabled ? FastEntryAfter : FastEntryBefore,
            "fast-entry readback");
        GuardEqual(finalExecutable.AsSpan(FastEntryFileOffset + FastEntryBefore.Length, FastEntryFallthrough.Length).ToArray(), FastEntryFallthrough, "fast-entry fallthrough readback");

        int scene = ExpandedSceneOffset;
        GuardUInt32(finalData, scene + SourceCountRelativeOffset, SourceCountAfter, "final HighCaves source count");
        GuardUInt32(finalData, scene + ScenePointerFixupCountRelativeOffset, ScenePointerFixupCountAfter, "final HighCaves pointer-fixup count");
        GuardUInt32(finalData, 0xF4, AppendedLockedChestPackageOffset, "final High Caves 0x00AE root");
        if (BinaryPrimitives.ReadUInt16LittleEndian(finalData.AsSpan(0x1A2, 2)) != 0x00AE)
            throw new InvalidDataException("Final High Caves actor ID slot does not contain 0x00AE.");

        return "Verified exact-USA retail preimages; one 0x2000-byte WAD growth (0x800 overlay + 0x1800 actor/model); copy buffer moved to 0x8008CE80; SCUS relocation 53875->53879; native Peace Keepers 0x00AE handler/model with High Caves-resident 0x00AD/0x000D/debris dependencies; T148-T154 source rows/props/fixups; four ownership-proven private texture/CLUT regions; treasure 500->510; optional guarded fast entry; untouched unrelated WAD entries; and byte-identical final readback. Runtime evidence is still required before editor promotion.";
    }

    private static byte[] ReadPeaceSourceRow(FileStream retail, DiscLayout layout, int trueIndex, string sha256, string label)
    {
        byte[] row = ReadWad(
            retail,
            layout,
            PeaceKeepersDataEntryOffset + PeaceKeepersSourceTableRelativeOffset + (trueIndex * RecordStride),
            RecordStride);
        GuardSha256(row, sha256, label);
        return row;
    }

    private static byte[] ReadPeaceProps(FileStream retail, DiscLayout layout, int pointer, int length) =>
        ReadWad(retail, layout, PeaceKeepersDataEntryOffset + PeaceKeepersSceneOffset + pointer, length);

    private static void PatchSourceRow(byte[] row, int propsPointer, int x, int y, int z)
    {
        if (row.Length != RecordStride)
            throw new InvalidDataException("A donor source row is not 0x58 bytes.");
        WriteUInt32(row, 0, checked((uint)propsPointer));
        WriteInt32(row, 0x0C, x);
        WriteInt32(row, 0x10, y);
        WriteInt32(row, 0x14, z);
    }

    private static void CopyGuardedBlank(byte[] target, int offset, byte[] value, string label)
    {
        GuardBlank(target.AsSpan(offset, value.Length).ToArray(), label);
        value.CopyTo(target, offset);
    }

    private static int RebaseLockedChestDescriptors(byte[] package)
    {
        int animationCount = ReadInt32(package, 0);
        if (animationCount <= 0 || animationCount > 1024)
            throw new InvalidDataException($"Locked Chest package has invalid animation count {animationCount}.");
        int modelData = checked((int)ReadUInt32(package, 0x34));
        HashSet<int> faceTables = [];
        int patched = 0;
        for (int animation = 0; animation < animationCount; animation++)
        {
            int animationRelative = ReadInt32(package, 0x38 + (animation * 4));
            if (animationRelative == -1)
                continue;
            if (animationRelative < 0 || animationRelative + 0x24 > package.Length)
                throw new InvalidDataException($"Locked Chest animation {animation} points outside its package.");
            patched += RebaseFaceTable(package, modelData, animationRelative, 0x14, faceTables, required: true);
            patched += RebaseFaceTable(package, modelData, animationRelative, 0x1C, faceTables, required: false);
        }
        return patched;
    }

    private static int RebaseFaceTable(
        byte[] package,
        int modelData,
        int animationStart,
        int pointerOffset,
        HashSet<int> faceTables,
        bool required)
    {
        int faceRelative = checked((int)ReadUInt32(package, animationStart + pointerOffset));
        if (!required && faceRelative == 0)
            return 0;
        int start = checked(modelData + faceRelative);
        if (!faceTables.Add(start))
            return 0;
        if (start < 0 || start + 4 > package.Length)
            throw new InvalidDataException("A Locked Chest face table points outside its package.");
        int bodyLength = checked((int)ReadUInt32(package, start));
        int cursor = start + 4;
        int end = checked(cursor + bodyLength);
        if ((bodyLength & 3) != 0 || bodyLength < 0 || bodyLength > 0x20000 || end > package.Length)
            throw new InvalidDataException("A Locked Chest face table has an invalid byte length.");

        int patched = 0;
        while (cursor < end)
        {
            if (end - cursor < 8)
                throw new InvalidDataException("A Locked Chest face table ends inside a renderer command.");
            uint control = ReadUInt32(package, cursor);
            int recordBytes;
            int descriptorOffset = -1;
            if ((control & 0x80000000u) == 0)
            {
                bool textured = (control & 0x2u) != 0;
                recordBytes = textured ? 20 : 8;
                if (textured)
                    descriptorOffset = cursor + 8;
            }
            else if ((control & 0x4u) != 0)
            {
                recordBytes = 20;
                descriptorOffset = cursor + 8;
            }
            else
            {
                bool textured = (control & 0x2u) != 0;
                recordBytes = textured ? 24 : 12;
                if (textured)
                    descriptorOffset = cursor + 12;
            }
            if (cursor + recordBytes > end)
                throw new InvalidDataException("A Locked Chest renderer command overruns its face table.");
            if (descriptorOffset >= 0)
            {
                RebaseDescriptor(package, descriptorOffset);
                patched++;
            }
            cursor += recordBytes;
        }
        if (cursor != end)
            throw new InvalidDataException("A Locked Chest face table did not end exactly.");
        return patched;
    }

    private static void RebaseDescriptor(byte[] package, int offset)
    {
        uint word2 = ReadUInt32(package, offset);
        uint word3 = ReadUInt32(package, offset + 4);
        uint word4 = ReadUInt32(package, offset + 8);
        ushort clut = (ushort)(word2 >> 16);
        ushort tpage = (ushort)(word3 >> 16);
        TextureRegion region = TextureRegions.FirstOrDefault(candidate =>
            candidate.SourceTpage == tpage && candidate.SourceClut == clut)
            ?? throw new InvalidDataException($"Locked Chest face uses unexpected TPAGE/CLUT 0x{tpage:X4}/0x{clut:X4}.");
        int deltaU = region.TargetU - region.SourceU;
        int deltaV = region.TargetV - region.SourceV;
        WriteWord(package, offset, RebaseUvWord(word2, deltaU, deltaV, region.TargetClut));
        WriteWord(package, offset + 4, RebaseUvWord(word3, deltaU, deltaV, region.TargetTpage));
        int u2 = CheckedUv((byte)word4 + deltaU);
        int v2 = CheckedUv((byte)(word4 >> 8) + deltaV);
        int u3 = CheckedUv((byte)(word4 >> 16) + deltaU);
        int v3 = CheckedUv((byte)(word4 >> 24) + deltaV);
        WriteWord(package, offset + 8, (uint)(u2 | (v2 << 8) | (u3 << 16) | (v3 << 24)));
    }

    private static uint RebaseUvWord(uint word, int deltaU, int deltaV, ushort descriptor) =>
        (uint)(CheckedUv((byte)word + deltaU) |
            (CheckedUv((byte)(word >> 8) + deltaV) << 8) |
            (descriptor << 16));

    private static int CheckedUv(int value) => value is >= 0 and <= 255
        ? value
        : throw new InvalidDataException($"Rebased Locked Chest UV {value} is outside 0..255.");

    private static int[] BuildTileOffsets(ushort tpage, int u, int v)
    {
        if (((tpage >> 7) & 3) != 0 || u < 0 || v < 0 || u + 31 > 255 || v + 31 > 255)
            throw new InvalidDataException("Locked Chest texture plan must use a 32x32 4-bpp in-page tile.");
        int pageWordX = (tpage & 0x0F) * 64;
        int pageY = (tpage & 0x10) != 0 ? 256 : 0;
        int[] offsets = new int[512];
        int cursor = 0;
        for (int row = 0; row < 32; row++)
        {
            int packedX = (pageWordX * 2) + (u / 2) - FullRightHalfByteX;
            int packedY = pageY + v + row;
            if (packedX < 0 || packedX + 16 > PackedVramRowBytes || packedY < 0 || packedY >= 512)
                throw new InvalidDataException("Locked Chest texture tile falls outside the addressable right-half VRAM image.");
            for (int column = 0; column < 16; column++)
                offsets[cursor++] = (packedY * PackedVramRowBytes) + packedX + column;
        }
        return offsets;
    }

    private static int[] BuildPaletteOffsets(ushort clut)
    {
        int xWord = (clut & 0x3F) * 16;
        int y = (clut >> 6) & 0x1FF;
        int packedX = (xWord * 2) - FullRightHalfByteX;
        if (packedX < 0 || packedX + 32 > PackedVramRowBytes || y < 0 || y >= 512)
            throw new InvalidDataException("Locked Chest CLUT falls outside the addressable right-half VRAM image.");
        return Enumerable.Range((y * PackedVramRowBytes) + packedX, 32).ToArray();
    }

    private static byte[] RelocateCode(IReadOnlyList<CodeSegment> segments, IReadOnlyDictionary<uint, uint> externalTargets)
    {
        List<byte> relocated = [];
        foreach (CodeSegment segment in segments)
        {
            if ((segment.Bytes.Length & 3) != 0)
                throw new InvalidDataException("A MIPS code segment is not word aligned.");
            byte[] output = segment.Bytes.ToArray();
            for (int offset = 0; offset < output.Length; offset += 4)
            {
                uint sourcePc = segment.SourceAddress + checked((uint)offset);
                uint targetPc = segment.TargetAddress + checked((uint)offset);
                uint word = ReadUInt32(segment.Bytes, offset);
                uint opcode = word >> 26;
                if (IsBranchOpcode(opcode))
                {
                    short immediate = unchecked((short)(word & 0xFFFF));
                    uint sourceTarget = unchecked((uint)((int)(sourcePc + 4) + (immediate << 2)));
                    uint target = MapRequiredTarget(sourceTarget, segments, externalTargets, $"branch at 0x{sourcePc:X8}");
                    int delta = unchecked((int)target - (int)(targetPc + 4));
                    if ((delta & 3) != 0 || delta / 4 is < short.MinValue or > short.MaxValue)
                        throw new InvalidDataException($"Relocated branch at 0x{sourcePc:X8} cannot reach 0x{target:X8}.");
                    word = (word & 0xFFFF0000u) | unchecked((ushort)(short)(delta / 4));
                }
                else if (opcode is 0x02 or 0x03)
                {
                    uint sourceTarget = ((sourcePc + 4) & 0xF0000000u) | ((word & 0x03FFFFFFu) << 2);
                    if (TryMapTarget(sourceTarget, segments, externalTargets, out uint target))
                    {
                        if (((targetPc + 4) & 0xF0000000u) != (target & 0xF0000000u))
                            throw new InvalidDataException($"Relocated jump at 0x{sourcePc:X8} crosses a 256 MB region.");
                        word = (word & 0xFC000000u) | ((target >> 2) & 0x03FFFFFFu);
                    }
                    else if (opcode == 0x02)
                    {
                        throw new InvalidDataException($"Unmapped external J target 0x{sourceTarget:X8} at donor PC 0x{sourcePc:X8}.");
                    }
                }
                WriteWord(output, offset, word);
            }
            relocated.AddRange(output);
        }
        return relocated.ToArray();
    }

    private static uint MapRequiredTarget(
        uint sourceTarget,
        IReadOnlyList<CodeSegment> segments,
        IReadOnlyDictionary<uint, uint> externalTargets,
        string label)
    {
        if (TryMapTarget(sourceTarget, segments, externalTargets, out uint target))
            return target;
        throw new InvalidDataException($"Unmapped {label} target 0x{sourceTarget:X8}.");
    }

    private static bool TryMapTarget(
        uint sourceTarget,
        IReadOnlyList<CodeSegment> segments,
        IReadOnlyDictionary<uint, uint> externalTargets,
        out uint target)
    {
        foreach (CodeSegment segment in segments)
        {
            uint end = checked(segment.SourceAddress + (uint)segment.Bytes.Length);
            if (sourceTarget >= segment.SourceAddress && sourceTarget < end)
            {
                target = checked(segment.TargetAddress + (sourceTarget - segment.SourceAddress));
                return true;
            }
        }
        return externalTargets.TryGetValue(sourceTarget, out target);
    }

    private static bool IsBranchOpcode(uint opcode) =>
        opcode is 0x01 or 0x04 or 0x05 or 0x06 or 0x07 or 0x14 or 0x15 or 0x16 or 0x17;

    private static uint EncodeBranch(uint opcode, int rs, int rt, uint branchPc, uint target)
    {
        int delta = unchecked((int)target - (int)(branchPc + 4));
        if ((delta & 3) != 0 || delta / 4 is < short.MinValue or > short.MaxValue)
            throw new InvalidDataException($"Branch at 0x{branchPc:X8} cannot reach 0x{target:X8}.");
        return (opcode << 26) | ((uint)rs << 21) | ((uint)rt << 16) | unchecked((ushort)(short)(delta / 4));
    }

    private static uint EncodeJump(uint target) => 0x08000000u | ((target >> 2) & 0x03FFFFFFu);

    private static (uint High, uint Low) EncodeAddiuAddressPair(int register, uint address)
    {
        uint low = address & 0xFFFF;
        uint high = (address + (low >= 0x8000 ? 0x10000u : 0u)) >> 16;
        return (
            0x3C000000u | ((uint)register << 16) | (high & 0xFFFF),
            0x24000000u | ((uint)register << 21) | ((uint)register << 16) | low);
    }

    private static WadArchiveLayout LoadAndGuardWadAnalysis(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        JsonElement wadElement = root.GetProperty("wad");
        int lba = wadElement.GetProperty("lba").GetInt32();
        int size = wadElement.GetProperty("size").GetInt32();
        WadEntryLayout[] entries = root.GetProperty("entries")
            .EnumerateArray()
            .Select(entry => new WadEntryLayout(
                entry.GetProperty("index").GetInt32(),
                entry.GetProperty("offset").GetInt64(),
                entry.GetProperty("size").GetInt32()))
            .Where(entry => entry.Offset >= SectorBytes && entry.Size > 0)
            .OrderBy(entry => entry.Offset)
            .ToArray();
        if (lba != WadLba || size != OriginalWadSize || entries.Length == 0 ||
            entries[^1].Offset + entries[^1].Size != OriginalWadSize)
        {
            throw new InvalidDataException("The selected WAD analysis is not the guarded retail Spyro archive.");
        }
        return new WadArchiveLayout(lba, size, entries, entries.ToDictionary(entry => entry.Index));
    }

    private static void PatchRootRecord(byte[] directory, string expectedName, int lba, int size)
    {
        for (int offset = 0; offset < directory.Length;)
        {
            int recordLength = directory[offset];
            if (recordLength == 0)
            {
                offset = ((offset / SectorBytes) + 1) * SectorBytes;
                continue;
            }
            if (recordLength < 34 || offset + recordLength > directory.Length)
                break;
            int nameLength = directory[offset + 32];
            string name = Encoding.ASCII.GetString(directory, offset + 33, nameLength)
                .Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                WriteBothEndianUInt32(directory, offset + 2, checked((uint)lba));
                WriteBothEndianUInt32(directory, offset + 10, checked((uint)size));
                return;
            }
            offset += recordLength;
        }
        throw new InvalidDataException($"ISO root record '{expectedName}' was not found.");
    }

    private static int OverlayRelativeOffset(uint runtimeAddress)
    {
        if (runtimeAddress < OverlayLoadAddress || runtimeAddress >= OverlayLoadAddress + ExpandedOverlaySize)
            throw new ArgumentOutOfRangeException(nameof(runtimeAddress));
        return checked((int)(runtimeAddress - OverlayLoadAddress));
    }

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length);

    private static void GuardWadWord(FileStream source, DiscLayout layout, uint runtimeAddress, uint expected, string label) =>
        GuardWord(ReadWad(source, layout, HighCavesOverlayEntryOffset + OverlayRelativeOffset(runtimeAddress), 4), 0, expected, label);

    private static void GuardUInt32(byte[] bytes, int offset, uint expected, string label) =>
        GuardWord(bytes, offset, expected, label);

    private static void GuardWord(byte[] bytes, int offset, uint expected, string label)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException($"{label} is outside its guarded buffer.");
        uint actual = ReadUInt32(bytes, offset);
        if (actual != expected)
            throw new InvalidDataException($"{label} changed: expected 0x{expected:X8}, found 0x{actual:X8}.");
    }

    private static void GuardSha256(byte[] bytes, string expected, string label)
    {
        string actual = Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, found {actual}.");
    }

    private static void GuardBlank(byte[] bytes, string label)
    {
        if (bytes.Any(value => value != 0))
            throw new InvalidDataException($"{label} is not blank.");
    }

    private static void GuardEqual(byte[] actual, byte[] expected, string label)
    {
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException($"{label} failed byte-for-byte comparison.");
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static void WriteWord(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static void WriteBothEndianUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), value);
    }

    private static byte[] Words(params uint[] values)
    {
        byte[] bytes = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++)
            WriteWord(bytes, index * 4, values[index]);
        return bytes;
    }

    private static byte[] Concat(params byte[][] blocks)
    {
        byte[] bytes = new byte[blocks.Sum(block => block.Length)];
        int offset = 0;
        foreach (byte[] block in blocks)
        {
            block.CopyTo(bytes, offset);
            offset += block.Length;
        }
        return bytes;
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ResolveFilePath(string path)
    {
        FileInfo file = new(Path.GetFullPath(path));
        FileSystemInfo? target = file.ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName ?? file.FullName;
    }

    private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);

    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequest(HighCavesNativeLockedChestCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourceImagePath) || !File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("The clean retail source image was not found.", request.SourceImagePath);
        if (string.IsNullOrWhiteSpace(request.WadAnalysisPath) || !File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("The guarded WAD analysis was not found.", request.WadAnalysisPath);
        if (string.IsNullOrWhiteSpace(request.LevelCatalogRootPath) ||
            !File.Exists(Path.Combine(request.LevelCatalogRootPath, "spyro-level-catalog.json")))
        {
            throw new FileNotFoundException("The level catalog root was not found.", request.LevelCatalogRootPath);
        }
        if (string.IsNullOrWhiteSpace(request.OutputImagePath))
            throw new ArgumentException("An output image path is required.", nameof(request));
        if (string.Equals(Path.GetFullPath(request.SourceImagePath), Path.GetFullPath(request.OutputImagePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The research candidate must not overwrite its source image.", nameof(request));
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record RewardMarkerPlacement(
        int DonorTrueIndex,
        int OutputTrueIndex,
        int PropsRelativeOffset,
        int DeltaX,
        int DeltaY,
        int DeltaZ,
        string DonorRowSha256);

    private sealed record TextureRegion(
        string Name,
        ushort SourceTpage,
        int SourceU,
        int SourceV,
        ushort SourceClut,
        ushort TargetTpage,
        int TargetU,
        int TargetV,
        ushort TargetClut,
        string TileSha256,
        string PaletteSha256);

    private sealed record CodeSegment(uint SourceAddress, uint TargetAddress, byte[] Bytes);
    private sealed record WadEntryLayout(int Index, long Offset, int Size);
    private sealed record WadArchiveLayout(
        int Lba,
        int Size,
        IReadOnlyList<WadEntryLayout> Entries,
        IReadOnlyDictionary<int, WadEntryLayout> ByIndex);
}
