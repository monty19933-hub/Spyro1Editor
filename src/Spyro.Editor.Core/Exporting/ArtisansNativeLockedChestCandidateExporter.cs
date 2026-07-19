using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record ArtisansNativeLockedChestCandidateRequest(
    string SourceImagePath,
    string OutputImagePath,
    string WadAnalysisPath,
    bool AllowUnrelatedExecutableEdits = false);

public sealed record ArtisansNativeLockedChestCandidatePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string SourceImagePath,
    string OutputImagePath,
    int WadLba,
    int OriginalWadSize,
    int ExpandedWadSize,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    int OriginalOverlaySize,
    int ExpandedOverlaySize,
    string OriginalCopyBufferAddress,
    string RelocatedCopyBufferAddress,
    string HandlerPayloadAddress,
    int HandlerPayloadLength,
    string HandlerPayloadSha256,
    string FinalOverlaySha256,
    string KeyProxyPayloadAddress,
    string AppendedKeyRuntimeAddress,
    int KeyTrueIndex,
    int LockedChestTrueIndex,
    IReadOnlyList<int> RewardMarkerTrueIndices,
    int RetailSourceCount,
    int KeyProxyBaselineSourceCount,
    int FinalSourceCount,
    int RetailScenePointerFixupCount,
    int KeyProxyBaselineScenePointerFixupCount,
    int FinalScenePointerFixupCount,
    IReadOnlyList<string> ImportedActorIds,
    string ActorPackageBundleSha256,
    int ActorSubfileGrowthBytes,
    int OriginalTreasureTarget,
    int FinalTreasureTarget,
    string DebrisPolicy,
    string Verification);

public sealed record ArtisansNativeLockedChestCandidateResult(
    string OutputImagePath,
    ArtisansNativeLockedChestCandidatePlan Plan,
    bool Verified,
    long OutputLength,
    string Verification);

/// <summary>
/// Research-only, guarded Artisans transplant of Peace Keepers' native Locked
/// Chest behavior. The previously proven T174 green-gem Key proxy is retained;
/// T175 is a real class 0x00AE chest and T176-T180 are its native class 0x000D
/// delayed reward markers. The WAD is expanded once so the handler closure and
/// actor packages move together, and the executable is relocated through the
/// checked ISO gap rather than writing over live startup state.
/// </summary>
public static class ArtisansNativeLockedChestCandidateExporter
{
    public const string RecipeId = "artisans.peacekeepers.nativeLockedChest.handlerModelRewards.v1";

    private const int SectorBytes = 2048;
    private const int WadLba = 37;
    private const int OriginalWadSize = 0x6927000;
    private const int ExpandedWadSize = 0x6929000;
    private const int OriginalExecutableLba = 53875;
    private const int RelocatedExecutableLba = 53879;
    private const int ExecutableSize = 0x66000;
    private const int NextFileLba = 60000;

    private const int ArtisansOverlayEntryIndex = 9;
    private const long ArtisansOverlayEntryOffset = 0x7F2800;
    private const int OriginalOverlaySize = 0xE000;
    private const int ExpandedOverlaySize = 0xE800;
    private const uint OverlayLoadAddress = 0x8007AA38;
    private const uint OriginalCopyBufferAddress = 0x80088620;
    private const uint RelocatedCopyBufferAddress = 0x80089238;

    private const int ArtisansDataEntryIndex = 10;
    private const long ArtisansDataEntryOffset = 0x800800;
    private const int OriginalDataEntrySize = 0x383000;
    private const int ExpandedDataEntrySize = 0x384800;
    private const int ActorSubfileIndex = 2;
    private const int ActorSubfileOffset = 0x17C000;
    private const int OriginalActorSubfileSize = 0x4D800;
    private const int ActorSubfileGrowth = 0x1800;
    private const int ExpandedSceneOffset = 0x1CB000;

    private const uint LockedHandlerAddress = 0x80088620;
    private const uint RewardMarkerHandlerAddress = 0x80088D60;
    private const uint RewardMarkerDispatchAddress = 0x80089198;
    private const uint LockedChestDispatchAddress = 0x800891AC;
    private const int OverlayPayloadOffset = 0xDBE8;
    private const int OverlayPayloadLength = 0xBA0;

    private const uint Class0DDispatchAddress = 0x8007DAFC;
    private const uint ClassC2DispatchAddress = 0x8007DB98;
    private const uint ClassAeDefaultDispatchAddress = 0x8007DBB8;
    private const uint GemHandlerHookAddress = 0x8007F858;
    private const uint GemHandlerResumeAddress = 0x8007F860;
    private const uint NativeKeyHandlerAddress = 0x80082574;
    private const uint ArtisansLoopExitAddress = 0x80085780;

    private const uint ExecutableDestination = 0x80010000;
    private const uint KeyProxyPayloadAddress = 0x8007314C;
    private const int ExecutableCaveLength = 0x400;
    private const uint KnownPreLogoFailureAddress = 0x80073924;
    private const int KnownPreLogoFailureLength = 0x100;
    private const int CopyBufferHiFileOffset = 0x4AF18;
    private const int CopyBufferLoFileOffset = 0x4AF1C;
    private const int ArtisansTreasureTableFileOffset = 0x5FC38;

    private const uint AppendedKeyRuntimeAddress = 0x801733D0;
    private const int RetailSourceCount = 174;
    private const int KeyProxyBaselineSourceCount = 175;
    private const int FinalSourceCount = 181;
    private const int KeyTrueIndex = 174;
    private const int LockedChestTrueIndex = 175;
    private const int RecordStride = 0x58;
    private const int SourceCountEntryOffset = 0x1D52A8;
    private const int SourceTableEntryOffset = 0x1D52AC;
    private const int ScenePointerFixupCountEntryOffset = 0x1DEED8;
    private const int ScenePointerFixupListEntryOffset = 0x1DF200;
    private const int RetailScenePointerFixupCount = 0xC9;
    private const int KeyProxyBaselineScenePointerFixupCount = 0xCA;
    private const int FinalScenePointerFixupCount = 0xD0;

    private const long PeaceOverlayEntryOffset = 0x187E000;
    private const long LockedSetupWadOffset = 0x188719C;
    private const uint LockedSetupSourceAddress = 0x80083BD4;
    private const int LockedSetupLength = 0x30;
    private const long LockedStateWadOffset = 0x1887330;
    private const uint LockedStateSourceAddress = 0x80083D68;
    private const int LockedStateLength = 0x70C;
    private const long RewardMarkerHandlerWadOffset = 0x1881B64;
    private const uint RewardMarkerSourceAddress = 0x8007E59C;
    private const int RewardMarkerHandlerLength = 0x434;

    private const long LockedChestPackageWadOffset = 0x1A44178;
    private const int LockedChestPackageLength = 0x1008;
    private const long Debris135PackageWadOffset = 0x1A500E0;
    private const int Debris135PackageLength = 0x224;
    private const long Debris136PackageWadOffset = 0x1A50304;
    private const int Debris136PackageLength = 0x1D4;
    private const long Debris137PackageWadOffset = 0x1A504D8;
    private const int Debris137PackageLength = 0x2D8;

    private const string ExpectedWadHeaderSha256 = "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd";
    private const string ExpectedRetailOverlaySha256 = "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55";
    private const string ExpectedRetailDataEntrySha256 = "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f";
    private const string ExpectedActorSubfileSha256 = "71aeec6c579796aae1ac4928faa85c98f7dd2ab5251761db7e72eb43a8a6758b";
    private const string ExpectedExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string ExpectedOverlayTailSha256 = "a738435279196111779497181333d6f70aecc2a4a941530f9324efcebf9e897e";
    private const string ExpectedLockedSetupSha256 = "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad";
    private const string ExpectedLockedStateSha256 = "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f";
    private const string ExpectedRewardMarkerSourceSha256 = "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056";
    private const string ExpectedLockedPayloadSha256 = "b5e94881bd9d7ee967c71447767a573bcd7b5faeefadf74051dd7ace724fa3cf";
    private const string ExpectedRewardMarkerPayloadSha256 = "ac057a5b8085e798d47e5e4e31f0fa323c7f552cb5bf8a7bc8d929050535fe25";
    private const string ExpectedHandlerPayloadSha256 = "2ddbdb449132ead02cabd8032089539ad28a2a33fdf4d030838c988c20726325";
    private const string ExpectedFinalOverlaySha256 = "c93581c05a4c70c1803ece5268ebfada34034ce76e663dcaf19da8fe0faa0459";
    private const string ExpectedLockedChestPackageSha256 = "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private const string ExpectedDebris135PackageSha256 = "af88a6d52fbd76fc5694314849016ce17a886cda9bdc187bc028f50ee1845ff4";
    private const string ExpectedDebris136PackageSha256 = "5f3e9ea885c38577cf0aa7ff27d0e1aa92634663dfcddbb254215e331f5c7b07";
    private const string ExpectedDebris137PackageSha256 = "7d4edcdc31c8231265b1c0297b8b9019ef6893d9ae415799db5228ae6b8248dc";
    private const string ExpectedActorBundleSha256 = "0ad9ff0039d980e49c8da2a4797daf24564f983182f36e573ccc615159345438";
    private const string ExpectedPaddedActorBundleSha256 = "5cfbf9af06636adf894922170ab3a7f23c26c6c1151d91fa3497d38ab30561fd";

    private static readonly byte[][] AppendedRows =
    [
        Hex("e01e01000000000000000000ec1401008551010000180000000000000000000000000000000000000000000000000000000000000000ae0000007d0000000000002000ff0000dc040000ff981886a1002000ff547f100000"),
        Hex("f81e010000000000000000005213010052560100001a00000000000000000000000000000000000000000000000000000000000000000d0000007d0000000000000000ff000000040000ff20000000000000ff547f100000"),
        Hex("201f01000000000000000000ec14010052560100001a00000000000000000000000000000000000000000000000000000000000000000d0000007d0000000000000000ff000000040000ff20000000000000ff537f100000"),
        Hex("481f010000000000000000008512010052520100001a00000000000000000000000000000000000000000000000000000000000000000d0000007d0000000000000000ff000000040000ff20000000000000ff547f100000"),
        Hex("701f0100000000000000000052130100ec530100001a00000000000000000000000000000000000000000000000000000000000000000d0000007d0000000000000000ff000000040000ff20000000000000ff537f100000"),
        Hex("981f01000000000000000000b8150100b8540100001a00000000000000000000000000000000000000000000000000000000000000000d0000007d0000000000000000ff000000040000ff20000000000000ff547f100000")
    ];

    private static readonly (int Offset, byte[] Bytes)[] AppendedProps =
    [
        (0x11EE0, Hex("ae0000000000000000000000000000000000000000000000")),
        (0x11EF8, Hex("af0000005213010052560100001a0000000000000000000001000000000000000000000000000000")),
        (0x11F20, Hex("af000000ec14010052560100001a0000000000000000000001000000000000000000000000000000")),
        (0x11F48, Hex("af0000008512010052520100001a0000000000000000000001000000000000000000000000000000")),
        (0x11F70, Hex("af00000052130100ec530100001a0000000000000000000001000000000000000000000000000000")),
        (0x11F98, Hex("af000000b8150100b8540100001a0000000000000000000001000000000000000000000000000000"))
    ];

    private static readonly uint[] AppendedFixups = [0xDED4, 0xDF2C, 0xDF84, 0xDFDC, 0xE034, 0xE08C];

    public static ArtisansNativeLockedChestCandidateResult Export(
        ArtisansNativeLockedChestCandidateRequest request)
    {
        ValidateRequest(request);
        string sourcePath = Path.GetFullPath(request.SourceImagePath);
        string outputPath = Path.GetFullPath(request.OutputImagePath);
        string analysisPath = Path.GetFullPath(request.WadAnalysisPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        string stagePath = outputPath + $".v4-stage-{Guid.NewGuid():N}.tmp";
        try
        {
            DiscLayout layout = DiscImage.DetectLayout(sourcePath);
            if (layout.SectorSize != 2352 || layout.UserOffset != 24)
                throw new InvalidDataException($"{RecipeId} requires the retail MODE2/2352 layout (2352/24).");

            WadArchiveLayout wad = LoadAndGuardWadAnalysis(analysisPath);
            GuardRetailSource(sourcePath, layout, wad, request.AllowUnrelatedExecutableEdits);

            ArtisansKeyGatedWoodenChestCandidateResult staged =
                ArtisansKeyGatedWoodenChestCandidateExporter.Export(
                    new ArtisansKeyGatedWoodenChestCandidateRequest(sourcePath, stagePath));
            if (!staged.Verified ||
                !string.Equals(staged.Plan.RecipeId, ArtisansKeyGatedWoodenChestCandidateExporter.RecipeId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The runtime-proven v4 Key proxy stage did not pass its structural contract.");
            }

            byte[] finalOverlay;
            byte[] finalDataEntry;
            byte[] finalExecutable;
            byte[] handlerPayload;
            byte[] actorBundle;
            IReadOnlyDictionary<int, long> relocatedEntryOffsets;

            using (FileStream retail = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream stage = File.Open(stagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                handlerPayload = BuildHandlerPayload(retail, layout);
                finalOverlay = BuildFinalOverlay(stage, layout, handlerPayload);
                (finalDataEntry, actorBundle) = BuildFinalDataEntry(retail, stage, layout);
                finalExecutable = BuildFinalExecutable(stage, layout);
                relocatedEntryOffsets = BuildRelocatedEntryOffsets(wad);
            }

            WriteExpandedImage(
                stagePath,
                outputPath,
                layout,
                wad,
                relocatedEntryOffsets,
                finalOverlay,
                finalDataEntry,
                finalExecutable);

            string verification = VerifyOutput(
                stagePath,
                outputPath,
                layout,
                wad,
                relocatedEntryOffsets,
                finalOverlay,
                finalDataEntry,
                finalExecutable,
                handlerPayload,
                actorBundle);

            ArtisansNativeLockedChestCandidatePlan plan = new(
                GeneratedAt: DateTimeOffset.UtcNow,
                RecipeId: RecipeId,
                SourceImagePath: sourcePath,
                OutputImagePath: outputPath,
                WadLba: WadLba,
                OriginalWadSize: OriginalWadSize,
                ExpandedWadSize: ExpandedWadSize,
                WadGrowthBytes: ExpandedWadSize - OriginalWadSize,
                OriginalExecutableLba: OriginalExecutableLba,
                RelocatedExecutableLba: RelocatedExecutableLba,
                OriginalOverlaySize: OriginalOverlaySize,
                ExpandedOverlaySize: ExpandedOverlaySize,
                OriginalCopyBufferAddress: $"0x{OriginalCopyBufferAddress:X8}",
                RelocatedCopyBufferAddress: $"0x{RelocatedCopyBufferAddress:X8}",
                HandlerPayloadAddress: $"0x{LockedHandlerAddress:X8}",
                HandlerPayloadLength: handlerPayload.Length,
                HandlerPayloadSha256: Sha256(handlerPayload),
                FinalOverlaySha256: Sha256(finalOverlay),
                KeyProxyPayloadAddress: $"0x{KeyProxyPayloadAddress:X8}",
                AppendedKeyRuntimeAddress: $"0x{AppendedKeyRuntimeAddress:X8}",
                KeyTrueIndex: KeyTrueIndex,
                LockedChestTrueIndex: LockedChestTrueIndex,
                RewardMarkerTrueIndices: [176, 177, 178, 179, 180],
                RetailSourceCount: RetailSourceCount,
                KeyProxyBaselineSourceCount: KeyProxyBaselineSourceCount,
                FinalSourceCount: FinalSourceCount,
                RetailScenePointerFixupCount: RetailScenePointerFixupCount,
                KeyProxyBaselineScenePointerFixupCount: KeyProxyBaselineScenePointerFixupCount,
                FinalScenePointerFixupCount: FinalScenePointerFixupCount,
                ImportedActorIds: ["0x00AE", "0x0135", "0x0136", "0x0137"],
                ActorPackageBundleSha256: Sha256(actorBundle),
                ActorSubfileGrowthBytes: ActorSubfileGrowth,
                OriginalTreasureTarget: 100,
                FinalTreasureTarget: 110,
                DebrisPolicy: "The native 0x00AE chest model and 0x0135-0x0137 packages are present, but this first behavior proof aliases the three spawn IDs to Artisans' resident 0x00FF-0x0101 wooden fragments because Artisans does not yet own the metal-fragment initializer/update closure.",
                Verification: verification);
            return new ArtisansNativeLockedChestCandidateResult(
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
        finally
        {
            DeleteIfExists(stagePath);
        }
    }

    private static void GuardRetailSource(
        string sourcePath,
        DiscLayout layout,
        WadArchiveLayout wad,
        bool allowUnrelatedExecutableEdits)
    {
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
        GuardSha256(ReadWad(source, layout, ArtisansOverlayEntryOffset, OriginalOverlaySize), ExpectedRetailOverlaySha256, "retail Artisans overlay");
        GuardSha256(ReadWad(source, layout, ArtisansDataEntryOffset, OriginalDataEntrySize), ExpectedRetailDataEntrySha256, "retail Artisans data entry");
        GuardSha256(ReadWad(source, layout, ArtisansDataEntryOffset + ActorSubfileOffset, OriginalActorSubfileSize), ExpectedActorSubfileSha256, "retail Artisans actor/model subfile");
        if (!allowUnrelatedExecutableEdits)
            GuardSha256(DiscImage.ReadFileBytes(source, layout, executable.Lba, 0, executable.Size), ExpectedExecutableSha256, "retail executable");
        GuardSha256(ReadWad(source, layout, ArtisansOverlayEntryOffset + OverlayPayloadOffset, OriginalOverlaySize - OverlayPayloadOffset), ExpectedOverlayTailSha256, "retail Artisans overlay tail/copy-buffer marker");

        GuardWadWord(source, layout, Class0DDispatchAddress, 0x080215E0, "retail class 0x0D dispatch");
        GuardWadWord(source, layout, Class0DDispatchAddress + 4, 0, "retail class 0x0D dispatch delay");
        GuardWadWord(source, layout, ClassC2DispatchAddress, 0x10621398, "retail class 0xC2 dispatch");
        GuardWadWord(source, layout, ClassC2DispatchAddress + 4, 0x286200C3, "retail class 0xC2 dispatch delay");
        GuardWadWord(source, layout, ClassAeDefaultDispatchAddress, 0x080215E0, "retail class 0xAE default dispatch");
        GuardWadWord(source, layout, ClassAeDefaultDispatchAddress + 4, 0, "retail class 0xAE default dispatch delay");
        GuardWadWord(source, layout, GemHandlerHookAddress, 0x3C058008, "retail gem handler word 0");
        GuardWadWord(source, layout, GemHandlerHookAddress + 4, 0x24A58A58, "retail gem handler word 1");

        byte[] copyBufferPair = DiscImage.ReadFileBytes(source, layout, executable.Lba, CopyBufferHiFileOffset, 8);
        GuardWord(copyBufferPair, 0, 0x3C028009, "retail Artisans copy-buffer HI16");
        GuardWord(copyBufferPair, 4, 0x24428620, "retail Artisans copy-buffer LO16");
        if (allowUnrelatedExecutableEdits)
        {
            byte[] artisansTreasure = DiscImage.ReadFileBytes(source, layout, executable.Lba, ArtisansTreasureTableFileOffset, 2);
            if (BinaryPrimitives.ReadUInt16LittleEndian(artisansTreasure) != 100)
                throw new InvalidDataException("The Artisans treasure target is no longer the required retail value 100.");
        }
        else
        {
            byte[] treasureTable = DiscImage.ReadFileBytes(source, layout, executable.Lba, ArtisansTreasureTableFileOffset - 8, 0x20);
            GuardSha256(treasureTable, "268d0ca44fab21afa7e2adc9f7ab83d6f24376d3e9661ed6f4c6ef1e39cf0b22", "retail level treasure table prefix");
        }

        WadEntryLayout overlay = wad.ByIndex[ArtisansOverlayEntryIndex];
        WadEntryLayout data = wad.ByIndex[ArtisansDataEntryIndex];
        if (overlay.Offset != ArtisansOverlayEntryOffset || overlay.Size != OriginalOverlaySize ||
            data.Offset != ArtisansDataEntryOffset || data.Size != OriginalDataEntrySize)
        {
            throw new InvalidDataException("The WAD analysis no longer identifies the exact Artisans overlay/data entries.");
        }
    }

    private static byte[] BuildHandlerPayload(FileStream retail, DiscLayout layout)
    {
        byte[] setup = ReadWad(retail, layout, LockedSetupWadOffset, LockedSetupLength);
        byte[] state = ReadWad(retail, layout, LockedStateWadOffset, LockedStateLength);
        byte[] marker = ReadWad(retail, layout, RewardMarkerHandlerWadOffset, RewardMarkerHandlerLength);
        GuardSha256(setup, ExpectedLockedSetupSha256, "Peace Keepers Locked Chest setup");
        GuardSha256(state, ExpectedLockedStateSha256, "Peace Keepers Locked Chest state/open path");
        GuardSha256(marker, ExpectedRewardMarkerSourceSha256, "Peace Keepers class 0x0D reward-marker handler");

        CodeSegment[] lockedSegments =
        [
            new(LockedSetupSourceAddress, LockedHandlerAddress + 4, setup),
            new(LockedStateSourceAddress, LockedHandlerAddress + 4 + LockedSetupLength, state)
        ];
        byte[] relocatedLockedBody = RelocateCode(
            lockedSegments,
            new Dictionary<uint, uint> { [0x8008A20C] = ArtisansLoopExitAddress });
        byte[] lockedPayload = Concat(Words(0x0260A021), relocatedLockedBody);
        PatchExpectedWord(lockedPayload, checked((int)(0x80088BDC - LockedHandlerAddress)), 0x24040136, 0x240400FF, "Locked Chest debris 0x0136 -> resident 0x00FF");
        PatchExpectedWord(lockedPayload, checked((int)(0x80088BF8 - LockedHandlerAddress)), 0x24040137, 0x24040100, "Locked Chest debris 0x0137 -> resident 0x0100");
        PatchExpectedWord(lockedPayload, checked((int)(0x80088C44 - LockedHandlerAddress)), 0x24040135, 0x24040101, "Locked Chest debris 0x0135 -> resident 0x0101");
        if (lockedPayload.Length != 0x740)
            throw new InvalidDataException($"Relocated Locked Chest payload is 0x{lockedPayload.Length:X}, expected 0x740.");
        GuardSha256(lockedPayload, ExpectedLockedPayloadSha256, "relocated Locked Chest payload");

        CodeSegment[] markerSegments = [new(RewardMarkerSourceAddress, RewardMarkerHandlerAddress + 4, marker)];
        Dictionary<uint, uint> markerExternalTargets = new()
        {
            [0x8008A20C] = ArtisansLoopExitAddress,
            [0x80088CF4] = 0x8007EBE8,
            [0x8008A204] = 0x80085438,
            [0x800880FC] = 0x800846FC
        };
        byte[] relocatedMarkerBody = RelocateCode(markerSegments, markerExternalTargets);
        byte[] markerDispatch = Words(
            0x2402000D,
            EncodeBranch(0x04, 3, 2, RewardMarkerDispatchAddress + 4, RewardMarkerHandlerAddress),
            0,
            EncodeJump(ArtisansLoopExitAddress),
            0);
        byte[] markerPayload = Concat(Words(0x0260A021), relocatedMarkerBody, markerDispatch);
        if (markerPayload.Length != 0x44C)
            throw new InvalidDataException($"Relocated reward-marker payload is 0x{markerPayload.Length:X}, expected 0x44C.");
        GuardSha256(markerPayload, ExpectedRewardMarkerPayloadSha256, "relocated reward-marker payload");

        byte[] lockedDispatch = Words(
            0x240200AE,
            EncodeBranch(0x04, 3, 2, LockedChestDispatchAddress + 4, LockedHandlerAddress),
            0,
            EncodeJump(ArtisansLoopExitAddress),
            0);
        byte[] payload = Concat(lockedPayload, markerPayload, lockedDispatch);
        if (payload.Length != OverlayPayloadLength)
            throw new InvalidDataException($"Combined native handler payload is 0x{payload.Length:X}, expected 0x{OverlayPayloadLength:X}.");
        GuardSha256(payload, ExpectedHandlerPayloadSha256, "combined Locked Chest/reward-marker payload");
        return payload;
    }

    private static byte[] BuildFinalOverlay(FileStream stage, DiscLayout layout, byte[] handlerPayload)
    {
        byte[] staged = ReadWad(stage, layout, ArtisansOverlayEntryOffset, OriginalOverlaySize);
        GuardWord(staged, OverlayRelativeOffset(ClassC2DispatchAddress), 0x1062D56C, "v4 staged C2 dispatch");
        GuardWord(staged, OverlayRelativeOffset(ClassC2DispatchAddress) + 4, 0x286200C3, "v4 staged C2 dispatch delay");
        GuardWord(staged, OverlayRelativeOffset(GemHandlerHookAddress), 0x0801CC69, "v4 staged Key-proxy gem hook");
        GuardWord(staged, OverlayRelativeOffset(GemHandlerHookAddress) + 4, 0, "v4 staged Key-proxy gem-hook delay");

        byte[] expanded = new byte[ExpandedOverlaySize];
        staged.CopyTo(expanded, 0);
        Array.Clear(expanded, OverlayPayloadOffset, expanded.Length - OverlayPayloadOffset);
        handlerPayload.CopyTo(expanded, OverlayPayloadOffset);

        WriteWord(expanded, OverlayRelativeOffset(Class0DDispatchAddress), EncodeJump(RewardMarkerDispatchAddress));
        WriteWord(expanded, OverlayRelativeOffset(Class0DDispatchAddress) + 4, 0);
        WriteWord(expanded, OverlayRelativeOffset(ClassC2DispatchAddress), 0x10621398);
        WriteWord(expanded, OverlayRelativeOffset(ClassAeDefaultDispatchAddress), EncodeJump(LockedChestDispatchAddress));
        WriteWord(expanded, OverlayRelativeOffset(ClassAeDefaultDispatchAddress) + 4, 0);
        WriteWord(expanded, OverlayRelativeOffset(GemHandlerHookAddress), EncodeJump(KeyProxyPayloadAddress));
        WriteWord(expanded, OverlayRelativeOffset(GemHandlerHookAddress) + 4, 0);

        if (expanded.AsSpan(OverlayPayloadOffset + handlerPayload.Length).ToArray().Any(value => value != 0))
            throw new InvalidDataException("Expanded Artisans overlay padding is not zero.");
        GuardSha256(expanded, ExpectedFinalOverlaySha256, "final expanded Artisans overlay");
        return expanded;
    }

    private static (byte[] Entry, byte[] Bundle) BuildFinalDataEntry(
        FileStream retail,
        FileStream stage,
        DiscLayout layout)
    {
        byte[] chest = ReadWad(retail, layout, LockedChestPackageWadOffset, LockedChestPackageLength);
        byte[] debris135 = ReadWad(retail, layout, Debris135PackageWadOffset, Debris135PackageLength);
        byte[] debris136 = ReadWad(retail, layout, Debris136PackageWadOffset, Debris136PackageLength);
        byte[] debris137 = ReadWad(retail, layout, Debris137PackageWadOffset, Debris137PackageLength);
        GuardSha256(chest, ExpectedLockedChestPackageSha256, "Peace Keepers actor 0x00AE package");
        GuardSha256(debris135, ExpectedDebris135PackageSha256, "Peace Keepers actor 0x0135 package");
        GuardSha256(debris136, ExpectedDebris136PackageSha256, "Peace Keepers actor 0x0136 package");
        GuardSha256(debris137, ExpectedDebris137PackageSha256, "Peace Keepers actor 0x0137 package");
        byte[] bundle = Concat(chest, debris135, debris136, debris137);
        GuardSha256(bundle, ExpectedActorBundleSha256, "combined Locked Chest actor package bundle");
        byte[] paddedBundle = new byte[ActorSubfileGrowth];
        bundle.CopyTo(paddedBundle, 0);
        GuardSha256(paddedBundle, ExpectedPaddedActorBundleSha256, "sector-padded Locked Chest actor package bundle");

        byte[] stagedEntry = ReadWad(stage, layout, ArtisansDataEntryOffset, OriginalDataEntrySize);
        GuardUInt32(stagedEntry, SourceCountEntryOffset - ActorSubfileGrowth, KeyProxyBaselineSourceCount, "v4 staged source count");
        GuardUInt32(stagedEntry, ScenePointerFixupCountEntryOffset - ActorSubfileGrowth, KeyProxyBaselineScenePointerFixupCount, "v4 staged scene pointer-fixup count");

        NativeSkyRelocationPayload payload = new(
            PatchIndex: 0,
            WadLba: WadLba,
            OriginalWadOffset: ArtisansDataEntryOffset + ActorSubfileOffset + OriginalActorSubfileSize,
            StorageWadEntry: ArtisansDataEntryIndex,
            ModelBlockOffset: OriginalActorSubfileSize,
            OriginalLength: 0,
            Bytes: paddedBundle,
            SubfileIndex: ActorSubfileIndex,
            RequireLengthPrefix: false);
        byte[] expanded = NativeSkyWadRelocator.ExpandNestedLevelEntry(stagedEntry, [payload], ActorSubfileGrowth);
        if (expanded.Length != ExpandedDataEntrySize)
            throw new InvalidDataException($"Expanded Artisans data entry is 0x{expanded.Length:X}, expected 0x{ExpandedDataEntrySize:X}.");
        GuardUInt32(expanded, (ActorSubfileIndex * 8) + 0, ActorSubfileOffset, "expanded actor/model subfile offset");
        GuardUInt32(expanded, (ActorSubfileIndex * 8) + 4, OriginalActorSubfileSize + ActorSubfileGrowth, "expanded actor/model subfile size");
        GuardUInt32(expanded, ((ActorSubfileIndex + 1) * 8) + 0, ExpandedSceneOffset, "expanded scene subfile offset");
        GuardEqual(expanded.AsSpan(ActorSubfileOffset + OriginalActorSubfileSize, ActorSubfileGrowth).ToArray(), paddedBundle, "appended actor package bundle");

        (int RootSlot, int ActorSlot, uint Root, ushort Actor)[] roots =
        [
            (0xDC, 0x196, 0x1C9800, 0x00AE),
            (0xE0, 0x198, 0x1CA808, 0x0135),
            (0xE4, 0x19A, 0x1CAA2C, 0x0136),
            (0xE8, 0x19C, 0x1CAC00, 0x0137)
        ];
        foreach ((int rootSlot, int actorSlot, uint root, ushort actor) in roots)
        {
            GuardBlank(expanded.AsSpan(rootSlot, 4).ToArray(), $"actor root slot 0x{rootSlot:X}");
            GuardBlank(expanded.AsSpan(actorSlot, 2).ToArray(), $"actor id slot 0x{actorSlot:X}");
            WriteUInt32(expanded, rootSlot, root);
            WriteUInt16(expanded, actorSlot, actor);
        }

        int keyRowOffset = SourceTableEntryOffset + (KeyTrueIndex * RecordStride);
        GuardUInt32(expanded, keyRowOffset, 0x11EDC, "shifted T174 Key-proxy props pointer");
        if (expanded[keyRowOffset + 0x3A] != 0x7F || expanded[keyRowOffset + 0x50] != 0x18 || expanded[keyRowOffset + 0x52] != 0x40)
            throw new InvalidDataException("The proven T174 Key-proxy source envelope changed during actor-subfile expansion.");

        int appendRowsOffset = SourceTableEntryOffset + (LockedChestTrueIndex * RecordStride);
        GuardBlank(expanded.AsSpan(appendRowsOffset, AppendedRows.Length * RecordStride).ToArray(), "T175-T180 source-row slots");
        for (int i = 0; i < AppendedRows.Length; i++)
        {
            if (AppendedRows[i].Length != RecordStride)
                throw new InvalidDataException($"T{LockedChestTrueIndex + i} source row is not 0x58 bytes.");
            AppendedRows[i].CopyTo(expanded, appendRowsOffset + (i * RecordStride));
        }

        foreach ((int sceneOffset, byte[] props) in AppendedProps)
        {
            int entryOffset = ExpandedSceneOffset + sceneOffset;
            GuardBlank(expanded.AsSpan(entryOffset, props.Length).ToArray(), $"scene props slot 0x{sceneOffset:X}");
            props.CopyTo(expanded, entryOffset);
        }

        GuardUInt32(expanded, SourceCountEntryOffset, KeyProxyBaselineSourceCount, "expanded staged source count");
        WriteUInt32(expanded, SourceCountEntryOffset, FinalSourceCount);
        GuardUInt32(expanded, ScenePointerFixupCountEntryOffset, KeyProxyBaselineScenePointerFixupCount, "expanded staged pointer-fixup count");
        GuardUInt32(expanded, ScenePointerFixupListEntryOffset, 0xDE7C, "existing T174 pointer fixup");
        GuardBlank(expanded.AsSpan(ScenePointerFixupListEntryOffset + 4, AppendedFixups.Length * 4).ToArray(), "T175-T180 pointer-fixup slots");
        for (int i = 0; i < AppendedFixups.Length; i++)
            WriteUInt32(expanded, ScenePointerFixupListEntryOffset + 4 + (i * 4), AppendedFixups[i]);
        WriteUInt32(expanded, ScenePointerFixupCountEntryOffset, FinalScenePointerFixupCount);

        return (expanded, bundle);
    }

    private static byte[] BuildFinalExecutable(FileStream stage, DiscLayout layout)
    {
        DiscFileRecord executable = DiscImage.FindRootFileRecord(stage, layout, IsExecutableName);
        if (executable.Lba != OriginalExecutableLba || executable.Size != ExecutableSize)
            throw new InvalidDataException("The v4 staging executable extent changed.");
        byte[] bytes = DiscImage.ReadFileBytes(stage, layout, executable.Lba, 0, executable.Size);

        int caveOffset = RuntimeAddressToExecutableOffset(KeyProxyPayloadAddress);
        GuardWord(bytes, caveOffset, 0x3C028016, "v4 staged gate payload word 0");
        GuardWord(bytes, caveOffset + (22 * 4), 0x3C028017, "v4 staged Key-proxy payload word 0");
        GuardBlank(bytes.AsSpan(caveOffset + (32 * 4), ExecutableCaveLength - (32 * 4)).ToArray(), "v4 staged helper-cave tail");
        Array.Clear(bytes, caveOffset, ExecutableCaveLength);
        byte[] keyProxyPayload = BuildKeyProxyPayload();
        keyProxyPayload.CopyTo(bytes, caveOffset);

        int failedCaveOffset = RuntimeAddressToExecutableOffset(KnownPreLogoFailureAddress);
        GuardBlank(bytes.AsSpan(failedCaveOffset, KnownPreLogoFailureLength).ToArray(), "known pre-logo failure region");

        GuardWord(bytes, CopyBufferHiFileOffset, 0x3C028009, "staged Artisans copy-buffer HI16");
        GuardWord(bytes, CopyBufferLoFileOffset, 0x24428620, "staged Artisans copy-buffer LO16");
        (uint high, uint low) = EncodeAddiuAddressPair(2, RelocatedCopyBufferAddress);
        WriteWord(bytes, CopyBufferHiFileOffset, high);
        WriteWord(bytes, CopyBufferLoFileOffset, low);

        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(ArtisansTreasureTableFileOffset, 2)) != 100)
            throw new InvalidDataException("The Artisans treasure target is no longer 100 in the retail level-total table.");
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(ArtisansTreasureTableFileOffset, 2), 110);
        return bytes;
    }

    private static byte[] BuildKeyProxyPayload() => Words(
        0x3C028017,
        0x344233D0,
        0x16620003,
        0,
        EncodeJump(NativeKeyHandlerAddress),
        0,
        0x3C058008,
        0x24A58A58,
        EncodeJump(GemHandlerResumeAddress),
        0);

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
                ArtisansOverlayEntryIndex => ExpandedOverlaySize - OriginalOverlaySize,
                ArtisansDataEntryIndex => ExpandedDataEntrySize - OriginalDataEntrySize,
                _ => 0
            };
            relocatedCursor = checked(relocatedCursor + entry.Size + growth);
        }
        if (originalCursor != OriginalWadSize || relocatedCursor != ExpandedWadSize)
            throw new InvalidDataException($"Combined WAD relocation balanced to 0x{relocatedCursor:X}, expected 0x{ExpandedWadSize:X}.");
        if (result[ArtisansOverlayEntryIndex] != ArtisansOverlayEntryOffset ||
            result[ArtisansDataEntryIndex] != ArtisansDataEntryOffset + (ExpandedOverlaySize - OriginalOverlaySize))
        {
            throw new InvalidDataException("Artisans entry relocation offsets changed unexpectedly.");
        }
        return result;
    }

    private static void WriteExpandedImage(
        string stagePath,
        string outputPath,
        DiscLayout layout,
        WadArchiveLayout wad,
        IReadOnlyDictionary<int, long> relocatedEntryOffsets,
        byte[] finalOverlay,
        byte[] finalDataEntry,
        byte[] finalExecutable)
    {
        File.Copy(stagePath, outputPath, true);
        using FileStream stage = File.Open(stagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(stage, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(stage, layout, IsExecutableName);
        byte[] wadHeader = ReadWad(stage, layout, 0, SectorBytes);

        foreach (WadEntryLayout entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            byte[] bytes = entry.Index switch
            {
                ArtisansOverlayEntryIndex => finalOverlay,
                ArtisansDataEntryIndex => finalDataEntry,
                _ => ReadWad(stage, layout, entry.Offset, entry.Size)
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
        string stagePath,
        string outputPath,
        DiscLayout layout,
        WadArchiveLayout wad,
        IReadOnlyDictionary<int, long> relocatedEntryOffsets,
        byte[] finalOverlay,
        byte[] finalDataEntry,
        byte[] finalExecutable,
        byte[] handlerPayload,
        byte[] actorBundle)
    {
        using FileStream stage = File.Open(stagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(output, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        if (wadFile.Lba != WadLba || wadFile.Size != ExpandedWadSize ||
            executable.Lba != RelocatedExecutableLba || executable.Size != ExecutableSize)
        {
            throw new InvalidDataException("Expanded WAD/relocated executable ISO records failed readback.");
        }
        if (new FileInfo(outputPath).Length != new FileInfo(stagePath).Length)
            throw new InvalidDataException("Expanded candidate changed the raw disc-image length.");

        byte[] header = ReadWad(output, layout, 0, SectorBytes);
        foreach (WadEntryLayout entry in wad.Entries)
        {
            long expectedOffset = relocatedEntryOffsets[entry.Index];
            int expectedSize = entry.Index switch
            {
                ArtisansOverlayEntryIndex => ExpandedOverlaySize,
                ArtisansDataEntryIndex => ExpandedDataEntrySize,
                _ => entry.Size
            };
            if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(entry.Index * 8, 4)) != expectedOffset ||
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan((entry.Index * 8) + 4, 4)) != expectedSize)
            {
                throw new InvalidDataException($"Relocated WAD header entry {entry.Index} failed readback.");
            }
        }

        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[ArtisansOverlayEntryIndex], finalOverlay.Length), finalOverlay, "expanded Artisans overlay readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[ArtisansDataEntryIndex], finalDataEntry.Length), finalDataEntry, "expanded Artisans data-entry readback");
        GuardEqual(DiscImage.ReadFileBytes(output, layout, executable.Lba, 0, executable.Size), finalExecutable, "relocated executable readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[ArtisansOverlayEntryIndex] + OverlayPayloadOffset, handlerPayload.Length), handlerPayload, "native handler payload readback");
        GuardEqual(ReadWad(output, layout, relocatedEntryOffsets[ArtisansDataEntryIndex] + 0x1C9800, actorBundle.Length), actorBundle, "actor package bundle readback");

        foreach (WadEntryLayout entry in wad.Entries.Where(entry => entry.Index is not ArtisansOverlayEntryIndex and not ArtisansDataEntryIndex))
        {
            byte[] before = ReadWad(stage, layout, entry.Offset, entry.Size);
            byte[] after = ReadWad(output, layout, relocatedEntryOffsets[entry.Index], entry.Size);
            if (!after.AsSpan().SequenceEqual(before))
                throw new InvalidDataException($"Unrelated WAD entry {entry.Index} changed during combined relocation.");
        }

        if (!CrossLevelActorPackageLayoutSafety.TryReadLayout(
                output,
                layout,
                WadLba,
                relocatedEntryOffsets[ArtisansDataEntryIndex],
                out CrossLevelActorPackageSubfileLayout? actorLayout,
                out string actorReason) ||
            actorLayout == null || actorLayout.SubfileIndex != ActorSubfileIndex ||
            actorLayout.Start != ActorSubfileOffset || actorLayout.EndExclusive != 0x1CB000 ||
            actorLayout.NativeRootCount != 39 || actorLayout.LastNativeRoot != 0x1CAC00)
        {
            throw new InvalidDataException($"Expanded Artisans actor/model layout failed validation: {actorReason}");
        }

        int caveOffset = RuntimeAddressToExecutableOffset(KeyProxyPayloadAddress);
        GuardEqual(finalExecutable.AsSpan(caveOffset, BuildKeyProxyPayload().Length).ToArray(), BuildKeyProxyPayload(), "Key-proxy payload readback");
        GuardBlank(finalExecutable.AsSpan(caveOffset + BuildKeyProxyPayload().Length, ExecutableCaveLength - BuildKeyProxyPayload().Length).ToArray(), "unused executable helper-cave tail");
        GuardWord(finalExecutable, CopyBufferHiFileOffset, 0x3C028009, "relocated copy-buffer HI16 readback");
        GuardWord(finalExecutable, CopyBufferLoFileOffset, 0x24429238, "relocated copy-buffer LO16 readback");
        if (BinaryPrimitives.ReadUInt16LittleEndian(finalExecutable.AsSpan(ArtisansTreasureTableFileOffset, 2)) != 110)
            throw new InvalidDataException("Artisans treasure target did not read back as 110.");

        return "Verified one 0x2000-byte WAD growth (0x800 overlay + 0x1800 actor/model), SCUS relocation 53875->53879, exact native 0x00AE/0x000D handler payload and dispatch guards, T174 Key proxy plus T175-T180 chest/reward rows, four contiguous actor roots, source/fixup counts, treasure 100->110, copy-buffer relocation, untouched unrelated WAD entries, and byte-identical readback.";
    }

    private static byte[] RelocateCode(
        IReadOnlyList<CodeSegment> segments,
        IReadOnlyDictionary<uint, uint> externalTargets)
    {
        List<byte> relocated = new();
        foreach (CodeSegment segment in segments)
        {
            if ((segment.Bytes.Length & 3) != 0)
                throw new InvalidDataException("A MIPS code segment is not word aligned.");
            byte[] output = segment.Bytes.ToArray();
            for (int offset = 0; offset < output.Length; offset += 4)
            {
                uint sourcePc = segment.SourceAddress + checked((uint)offset);
                uint targetPc = segment.TargetAddress + checked((uint)offset);
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(segment.Bytes.AsSpan(offset, 4));
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
                    // External JALs target common resident engine functions and
                    // deliberately retain their original absolute target.
                }
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset, 4), word);
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

    private static bool IsBranchOpcode(uint opcode) => opcode is 0x01 or 0x04 or 0x05 or 0x06 or 0x07 or 0x14 or 0x15 or 0x16 or 0x17;

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

    private static int RuntimeAddressToExecutableOffset(uint address) =>
        checked((int)(address - ExecutableDestination + 0x800));

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length);

    private static void GuardWadWord(FileStream source, DiscLayout layout, uint runtimeAddress, uint expected, string label) =>
        GuardWord(ReadWad(source, layout, ArtisansOverlayEntryOffset + OverlayRelativeOffset(runtimeAddress), 4), 0, expected, label);

    private static void PatchExpectedWord(byte[] bytes, int offset, uint expected, uint replacement, string label)
    {
        GuardWord(bytes, offset, expected, label);
        WriteWord(bytes, offset, replacement);
    }

    private static void GuardUInt32(byte[] bytes, int offset, uint expected, string label) => GuardWord(bytes, offset, expected, label);

    private static void GuardWord(byte[] bytes, int offset, uint expected, string label)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException($"{label} is outside its guarded buffer.");
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
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

    private static void WriteWord(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

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
        for (int i = 0; i < values.Length; i++)
            WriteWord(bytes, i * 4, values[i]);
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

    private static byte[] Hex(string value) => Convert.FromHexString(value);
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);
    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);
    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequest(ArtisansNativeLockedChestCandidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceImagePath) || !File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("The clean retail source image was not found.", request.SourceImagePath);
        if (string.IsNullOrWhiteSpace(request.WadAnalysisPath) || !File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("The guarded WAD analysis was not found.", request.WadAnalysisPath);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record CodeSegment(uint SourceAddress, uint TargetAddress, byte[] Bytes);
    private sealed record WadEntryLayout(int Index, long Offset, int Size);
    private sealed record WadArchiveLayout(
        int Lba,
        int Size,
        IReadOnlyList<WadEntryLayout> Entries,
        IReadOnlyDictionary<int, WadEntryLayout> ByIndex);
}
