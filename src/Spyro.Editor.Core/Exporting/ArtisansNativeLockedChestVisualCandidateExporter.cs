using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

public sealed record ArtisansNativeLockedChestVisualCandidateRequest(
    string SourceImagePath,
    string OutputImagePath,
    string WadAnalysisPath,
    bool AllowUnrelatedExecutableEdits = false);

public sealed record ArtisansNativeLockedChestTextureRegionPlan(
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

public sealed record ArtisansNativeLockedChestVisualCandidatePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string BehaviorBaselineRecipeId,
    string SourceImagePath,
    string OutputImagePath,
    int KeyTrueIndex,
    string KeyClassBefore,
    string KeyClassAfter,
    string KeyModelSource,
    bool KeyModelIsUntextured,
    int LockedChestTrueIndex,
    int RebasedTexturedFaceCount,
    int TextureTileCount,
    int PaletteCount,
    int TexturePayloadBytes,
    IReadOnlyList<ArtisansNativeLockedChestTextureRegionPlan> TextureRegions,
    string TextureAllocationProof,
    string FinalTexturePagesSha256,
    string FinalLockedChestPackageSha256,
    string FinalDataEntrySha256,
    string FinalOverlaySha256,
    string FinalExecutableSha256,
    string Verification);

public sealed record ArtisansNativeLockedChestVisualCandidateResult(
    string OutputImagePath,
    ArtisansNativeLockedChestVisualCandidatePlan Plan,
    bool Verified,
    long OutputLength,
    string Verification);

/// <summary>
/// Visual-only follow-up to the runtime-proven native Locked Chest bundle.
/// The baseline behavior exporter remains the source of the handler, model,
/// reward, persistence, and ISO-relocation changes. This layer changes only:
/// (1) T174 from the resident green-gem visual class to the shared PETE.WAD
/// Key SimpleModel while retaining the loader-transformed 0x18/0x40 runtime
/// envelope, and (2) the imported Locked Chest's four 32x32 4-bpp tiles and
/// four CLUTs, rebased into the four source-proven private Artisans slots.
/// </summary>
public static class ArtisansNativeLockedChestVisualCandidateExporter
{
    public const string RecipeId = "artisans.peacekeepers.nativeLockedChest.nativeKey.rebasedPrivateTextures.v2";

    private const int SectorBytes = 2048;
    private const int WadLba = 37;
    private const int ExpandedWadSize = 0x6929000;
    private const int RelocatedExecutableLba = 53879;
    private const int ExecutableSize = 0x66000;
    private const int ArtisansOverlayEntryIndex = 9;
    private const int ArtisansDataEntryIndex = 10;
    private const int PeaceKeepersDataEntryIndex = 22;
    private const int PeteEntryIndex = 8;
    private const long PeteEntryOffset = 0x7B8800;
    private const int PeteEntrySize = 0x3A000;
    private const int PeteKeyModelOffset = 0x34A8C;
    private const int PeteNextModelOffset = 0x34CA8;
    private const long ExpandedArtisansOverlayOffset = 0x7F2800;
    private const int ExpandedArtisansOverlaySize = 0xE800;
    private const long ExpandedArtisansDataOffset = 0x801000;
    private const int ExpandedArtisansDataSize = 0x384800;
    private const long RetailPeaceKeepersDataOffset = 0x1890800;
    private const int RetailPeaceKeepersDataSize = 0x29E800;
    private const int TexturePagesSubfileOffset = 0x800;
    private const int AddressableTexturePageBytes = 0x80000;
    private const int PackedVramRowBytes = 0x400;
    private const int FullRightHalfByteX = 0x400;

    private const int SourceTableEntryOffset = 0x1D52AC;
    private const int RecordStride = 0x58;
    private const int KeyTrueIndex = 174;
    private const int LockedChestTrueIndex = 175;
    private const int LockedChestPackageOffset = 0x1C9800;
    private const int LockedChestPackageLength = 0x1008;

    private const string ExpectedBehaviorOverlaySha256 = "c93581c05a4c70c1803ece5268ebfada34034ce76e663dcaf19da8fe0faa0459";
    private const string ExpectedBehaviorDataSha256 = "7789d13ae752441d2708bda41f7b9b40ebaed11208bb148d829b30b8afbe3c8f";
    private const string ExpectedBehaviorExecutableSha256 = "63a4bb3ff992362fb852af6efd34621f57f5606df89c8da4ffc746f07f7be129";
    private const string ExpectedArtisansTexturePagesSha256 = "a96e0f2471697a5744bede4cc0d1c7090bf14cb47362f6e58b1e9daca1235851";
    private const string ExpectedBehaviorKeyRowSha256 = "32491ba78d2d9d38e5a8eb4bb24a9e8250c489eb4f028b1b370cbd173528fb1a";
    private const string ExpectedPeteKeyModelSha256 = "72a5a017f8696edfc2d600b093c1b05c29ba3a423b29ad42b838a3385f5298e2";
    private const string ExpectedLockedChestPackageSha256 = "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private const string ExpectedBlankTileSha256 = "076a27c79e5ace2a3d47f9dd2e83e4ff6ea8872b3c2218f66c92b89b55f36560";
    private const string ExpectedBlankPaletteSha256 = "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925";

    private const string ExpectedFinalTexturePagesSha256 = "374d658026a23bd3ab1b929a832c1f61f6cd0fbca2e5117395f6594235a5fde7";
    private const string ExpectedFinalLockedChestPackageSha256 = "1915dffaf52f7c7d5cf005971eb9f680e1b2d5d4c280a7752868a67db2a99103";
    private const string ExpectedFinalDataSha256 = "255bc5318b54ffd24385c4653360e599815d8e0f1cf0d6905aa9890d23bf37f4";
    private const string ExpectedFinalOverlaySha256 = ExpectedBehaviorOverlaySha256;
    private const string ExpectedFinalExecutableSha256 = ExpectedBehaviorExecutableSha256;
    private const string ExpectedFinalKeyRowSha256 = "55173499459b41843724235b6b6179543e3a9e1335fec0d93ddf23f525859493";

    private static readonly TextureRegion[] TextureRegions =
    [
        new("upper-lock", 0x000E, 192, 192, 0x4A2A, 0x0008, 0, 0, 0x1024,
            "ed3d07124975005d58fc35ed56f3f028e990f8042bb04b5512b5fe2236840c7e",
            "f7fd1c75393d4a46437006d96d9defbfb48eca2b98ab6b561ebab4c12dfef561"),
        new("lower-lock", 0x000E, 192, 224, 0x4C2A, 0x0008, 0, 32, 0x1025,
            "8db3cf16c367cca355e9579e2a4edaf81b39a2b39fab50129e5671ef2f304851",
            "a115024fd9c4050aaf5dd2d5c82ac58351fd4cfa3ec4ff5544a39bbb386f35c7"),
        new("chest-body-a", 0x000F, 0, 160, 0x5C2A, 0x001F, 128, 128, 0x1026,
            "320dd914cf4999c4c49e89b55d49042a2539765acffcd370c14758790a1c27c1",
            "ecb01bf085a6a398dc794f4406dad68cdae42426bea5a8ffa778c5aa079c3f7a"),
        new("chest-body-b", 0x000F, 0, 192, 0x5E2A, 0x001C, 32, 224, 0x1027,
            "c2d356f4ba0f73e9e27dfafe87c2b03f5d6619b30c6bbb5f4b748c926c38d430",
            "fa257923dafd18a44a0bad1f95158cee3ac0eeb98707486144af215568d249db")
    ];

    public static ArtisansNativeLockedChestVisualCandidateResult Export(
        ArtisansNativeLockedChestVisualCandidateRequest request)
    {
        ValidateRequest(request);
        string sourcePath = Path.GetFullPath(request.SourceImagePath);
        string outputPath = Path.GetFullPath(request.OutputImagePath);
        string analysisPath = Path.GetFullPath(request.WadAnalysisPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        string behaviorStagePath = outputPath + $".behavior-v1-{Guid.NewGuid():N}.tmp";

        try
        {
            ArtisansNativeLockedChestCandidateResult behavior =
                ArtisansNativeLockedChestCandidateExporter.Export(
                    new ArtisansNativeLockedChestCandidateRequest(
                        sourcePath,
                        behaviorStagePath,
                        analysisPath,
                        request.AllowUnrelatedExecutableEdits));
            if (!behavior.Verified ||
                !string.Equals(
                    behavior.Plan.RecipeId,
                    ArtisansNativeLockedChestCandidateExporter.RecipeId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The runtime-proven native Locked Chest behavior baseline failed its contract.");
            }

            DiscLayout layout = DiscImage.DetectLayout(sourcePath);
            if (layout.SectorSize != 2352 || layout.UserOffset != 24)
                throw new InvalidDataException($"{RecipeId} requires the retail MODE2/2352 layout (2352/24).");

            File.Copy(behaviorStagePath, outputPath, true);
            int rebasedFaceCount;
            byte[] finalOverlay;
            byte[] finalData;
            byte[] finalExecutable;
            List<ArtisansNativeLockedChestTextureRegionPlan> regionPlans = [];

            using (FileStream retail = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                DiscFileRecord wad = DiscImage.FindRootFileRecord(output, layout, IsWadName);
                DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
                if (wad.Lba != WadLba || wad.Size != ExpandedWadSize ||
                    executable.Lba != RelocatedExecutableLba || executable.Size != ExecutableSize)
                {
                    throw new InvalidDataException("The visual candidate did not inherit the proven expanded-WAD/relocated-executable layout.");
                }

                byte[] expandedHeader = ReadWad(output, layout, 0, SectorBytes);
                GuardEntry(expandedHeader, ArtisansOverlayEntryIndex, ExpandedArtisansOverlayOffset, ExpandedArtisansOverlaySize, "expanded Artisans overlay");
                GuardEntry(expandedHeader, ArtisansDataEntryIndex, ExpandedArtisansDataOffset, ExpandedArtisansDataSize, "expanded Artisans data");
                byte[] retailHeader = ReadWad(retail, layout, 0, SectorBytes);
                GuardEntry(retailHeader, PeaceKeepersDataEntryIndex, RetailPeaceKeepersDataOffset, RetailPeaceKeepersDataSize, "retail Peace Keepers data");
                GuardSharedKeyModel(retail, layout, retailHeader);

                finalOverlay = ReadWad(output, layout, ExpandedArtisansOverlayOffset, ExpandedArtisansOverlaySize);
                finalData = ReadWad(output, layout, ExpandedArtisansDataOffset, ExpandedArtisansDataSize);
                finalExecutable = DiscImage.ReadFileBytes(output, layout, executable.Lba, 0, executable.Size);
                GuardSha256(finalOverlay, ExpectedBehaviorOverlaySha256, "behavior-baseline overlay");
                GuardSha256(finalData, ExpectedBehaviorDataSha256, "behavior-baseline data entry");
                if (!request.AllowUnrelatedExecutableEdits)
                    GuardSha256(finalExecutable, ExpectedBehaviorExecutableSha256, "behavior-baseline executable");
                GuardSha256(finalData.AsSpan(TexturePagesSubfileOffset, AddressableTexturePageBytes).ToArray(), ExpectedArtisansTexturePagesSha256, "behavior-baseline Artisans texture pages");

                PatchNativeKeyVisual(finalData);

                byte[] donorTexturePages = ReadWad(
                    retail,
                    layout,
                    RetailPeaceKeepersDataOffset + TexturePagesSubfileOffset,
                    AddressableTexturePageBytes);
                foreach (TextureRegion region in TextureRegions)
                {
                    CopyTextureRegion(finalData, donorTexturePages, region);
                    regionPlans.Add(new ArtisansNativeLockedChestTextureRegionPlan(
                        region.Name,
                        $"0x{region.SourceTpage:X4}",
                        region.SourceU,
                        region.SourceV,
                        $"0x{region.SourceClut:X4}",
                        $"0x{region.TargetTpage:X4}",
                        region.TargetU,
                        region.TargetV,
                        $"0x{region.TargetClut:X4}",
                        region.TileSha256,
                        region.PaletteSha256));
                }

                byte[] chestPackage = finalData.AsSpan(LockedChestPackageOffset, LockedChestPackageLength).ToArray();
                GuardSha256(chestPackage, ExpectedLockedChestPackageSha256, "behavior-baseline Locked Chest package");
                rebasedFaceCount = RebaseLockedChestDescriptors(chestPackage);
                if (rebasedFaceCount != 146)
                    throw new InvalidDataException($"Rebased {rebasedFaceCount} Locked Chest faces, expected exactly 146.");
                GuardSha256(chestPackage, ExpectedFinalLockedChestPackageSha256, "rebased Locked Chest package");
                chestPackage.CopyTo(finalData, LockedChestPackageOffset);

                GuardSha256(finalData.AsSpan(TexturePagesSubfileOffset, AddressableTexturePageBytes).ToArray(), ExpectedFinalTexturePagesSha256, "final Artisans texture pages");
                GuardSha256(finalData, ExpectedFinalDataSha256, "final Artisans data entry");
                GuardSha256(finalOverlay, ExpectedFinalOverlaySha256, "final Artisans overlay");
                if (!request.AllowUnrelatedExecutableEdits)
                    GuardSha256(finalExecutable, ExpectedFinalExecutableSha256, "final executable");

                DiscImage.WriteFileBytes(output, layout, WadLba, ExpandedArtisansDataOffset, finalData);
                output.Flush();
            }

            string verification = VerifyOutput(
                behaviorStagePath,
                outputPath,
                layout,
                finalOverlay,
                finalData,
                finalExecutable,
                rebasedFaceCount);
            ArtisansNativeLockedChestVisualCandidatePlan plan = new(
                GeneratedAt: DateTimeOffset.UtcNow,
                RecipeId: RecipeId,
                BehaviorBaselineRecipeId: ArtisansNativeLockedChestCandidateExporter.RecipeId,
                SourceImagePath: sourcePath,
                OutputImagePath: outputPath,
                KeyTrueIndex: KeyTrueIndex,
                KeyClassBefore: "0x0054 green-gem visual proxy",
                KeyClassAfter: "0x00AD native Key",
                KeyModelSource: "Shared PETE.WAD actor 0x00AD SimpleModel (48 untextured faces)",
                KeyModelIsUntextured: true,
                LockedChestTrueIndex: LockedChestTrueIndex,
                RebasedTexturedFaceCount: rebasedFaceCount,
                TextureTileCount: TextureRegions.Length,
                PaletteCount: TextureRegions.Length,
                TexturePayloadBytes: TextureRegions.Length * (512 + 32),
                TextureRegions: regionPlans,
                TextureAllocationProof: "All four 32x32 4-bpp destinations and four 32-byte CLUT destinations are zero in the guarded retail Artisans texture-page SHA and are the four exact all-consumer-private tile slots found by the complete native texture ownership scan. No existing Artisans texture coordinate is overwritten.",
                FinalTexturePagesSha256: ExpectedFinalTexturePagesSha256,
                FinalLockedChestPackageSha256: ExpectedFinalLockedChestPackageSha256,
                FinalDataEntrySha256: ExpectedFinalDataSha256,
                FinalOverlaySha256: ExpectedFinalOverlaySha256,
                FinalExecutableSha256: Sha256(finalExecutable),
                Verification: verification);
            return new ArtisansNativeLockedChestVisualCandidateResult(
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
            DeleteIfExists(behaviorStagePath);
        }
    }

    private static void PatchNativeKeyVisual(byte[] data)
    {
        int offset = SourceTableEntryOffset + (KeyTrueIndex * RecordStride);
        byte[] row = data.AsSpan(offset, RecordStride).ToArray();
        GuardSha256(row, ExpectedBehaviorKeyRowSha256, "behavior-baseline T174 Key-proxy row");
        if (BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36, 2)) != 0x0054 ||
            row[0x3A] != 0x7F || row[0x50] != 0x18 || row[0x52] != 0x40)
        {
            throw new InvalidDataException("T174 no longer has the proven visible Key-proxy runtime envelope.");
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 0x36, 2), 0x00AD);
        GuardSha256(data.AsSpan(offset, RecordStride).ToArray(), ExpectedFinalKeyRowSha256, "native-Key T174 row");
    }

    private static void GuardSharedKeyModel(FileStream retail, DiscLayout layout, byte[] wadHeader)
    {
        GuardEntry(wadHeader, PeteEntryIndex, PeteEntryOffset, PeteEntrySize, "retail PETE.WAD");
        byte[] peteHeader = ReadWad(retail, layout, PeteEntryOffset, 0x800);
        GuardWord(peteHeader, 4 + (10 * 8), PeteKeyModelOffset, "PETE.WAD shared Key model offset");
        GuardWord(peteHeader, 8 + (10 * 8), 0x000000AD, "PETE.WAD shared Key actor id");
        GuardWord(peteHeader, 4 + (11 * 8), PeteNextModelOffset, "PETE.WAD model following the shared Key");
        byte[] keyModel = ReadWad(
            retail,
            layout,
            PeteEntryOffset + PeteKeyModelOffset,
            PeteNextModelOffset - PeteKeyModelOffset);
        GuardSha256(keyModel, ExpectedPeteKeyModelSha256, "shared PETE.WAD Key SimpleModel");
        if (ReadInt32(keyModel, 0) >= 0 ||
            keyModel[1] != 48 ||
            ReadUInt32(keyModel, 0x04) != 0x10 ||
            ReadUInt32(keyModel, 0x08) != 0x98 ||
            ReadUInt32(keyModel, 0x0C) != 0x9C)
        {
            throw new InvalidDataException("The shared actor 0x00AD Key is no longer the expected 48-face untextured SimpleModel.");
        }
    }

    private static void CopyTextureRegion(byte[] data, byte[] donorTexturePages, TextureRegion region)
    {
        int[] sourceTile = BuildTileOffsets(region.SourceTpage, region.SourceU, region.SourceV);
        int[] targetTile = BuildTileOffsets(region.TargetTpage, region.TargetU, region.TargetV);
        int[] sourcePalette = BuildPaletteOffsets(region.SourceClut);
        int[] targetPalette = BuildPaletteOffsets(region.TargetClut);
        byte[] donorTile = sourceTile.Select(offset => donorTexturePages[offset]).ToArray();
        byte[] donorPalette = sourcePalette.Select(offset => donorTexturePages[offset]).ToArray();
        byte[] targetTileBefore = targetTile.Select(offset => data[TexturePagesSubfileOffset + offset]).ToArray();
        byte[] targetPaletteBefore = targetPalette.Select(offset => data[TexturePagesSubfileOffset + offset]).ToArray();
        GuardSha256(donorTile, region.TileSha256, $"{region.Name} donor tile");
        GuardSha256(donorPalette, region.PaletteSha256, $"{region.Name} donor palette");
        GuardSha256(targetTileBefore, ExpectedBlankTileSha256, $"{region.Name} private target tile");
        GuardSha256(targetPaletteBefore, ExpectedBlankPaletteSha256, $"{region.Name} private target palette");
        for (int i = 0; i < sourceTile.Length; i++)
            data[TexturePagesSubfileOffset + targetTile[i]] = donorTexturePages[sourceTile[i]];
        for (int i = 0; i < sourcePalette.Length; i++)
            data[TexturePagesSubfileOffset + targetPalette[i]] = donorTexturePages[sourcePalette[i]];
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
            int animationStart = animationRelative;
            if (animationStart < 0 || animationStart + 0x24 > package.Length)
                throw new InvalidDataException($"Locked Chest animation {animation} points outside its package.");
            patched += RebaseFaceTable(package, modelData, animationStart, 0x14, faceTables, required: true);
            patched += RebaseFaceTable(package, modelData, animationStart, 0x1C, faceTables, required: false);
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

    private static string VerifyOutput(
        string behaviorStagePath,
        string outputPath,
        DiscLayout layout,
        byte[] finalOverlay,
        byte[] finalData,
        byte[] finalExecutable,
        int rebasedFaceCount)
    {
        using FileStream stage = File.Open(behaviorStagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (new FileInfo(stage.Name).Length != new FileInfo(outputPath).Length)
            throw new InvalidDataException("The visual layer changed the raw disc-image length.");
        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        GuardEqual(ReadWad(output, layout, ExpandedArtisansOverlayOffset, finalOverlay.Length), finalOverlay, "final overlay readback");
        GuardEqual(ReadWad(output, layout, ExpandedArtisansDataOffset, finalData.Length), finalData, "final data readback");
        GuardEqual(DiscImage.ReadFileBytes(output, layout, executable.Lba, 0, executable.Size), finalExecutable, "final executable readback");

        byte[] stageHeader = ReadWad(stage, layout, 0, SectorBytes);
        byte[] outputHeader = ReadWad(output, layout, 0, SectorBytes);
        GuardEqual(outputHeader, stageHeader, "expanded WAD header");
        for (int index = 0; index < 64; index++)
        {
            if (index is ArtisansOverlayEntryIndex or ArtisansDataEntryIndex)
                continue;
            long offset = ReadUInt32(stageHeader, index * 8);
            int length = checked((int)ReadUInt32(stageHeader, (index * 8) + 4));
            if (offset <= 0 || length <= 0)
                continue;
            GuardEqual(ReadWad(output, layout, offset, length), ReadWad(stage, layout, offset, length), $"unrelated WAD entry {index}");
        }
        return $"Preserved the user-proven v1 handler/model/reward/persistence bundle byte-for-byte; promoted T174 from class 0x0054 to shared untextured Key class 0x00AD while retaining visible 0x18/0x40 state; rebased {rebasedFaceCount} Locked Chest textured faces across four private 32x32 4-bpp tiles and four private CLUTs (2176 bytes); left the now-unreachable green-gem hook/cave unchanged for this isolated visual test; verified exact final hashes, untouched unrelated WAD entries, byte-identical readback, and unchanged image length.";
    }

    private static void GuardEntry(byte[] header, int index, long expectedOffset, int expectedSize, string label)
    {
        long offset = ReadUInt32(header, index * 8);
        int size = checked((int)ReadUInt32(header, (index * 8) + 4));
        if (offset != expectedOffset || size != expectedSize)
            throw new InvalidDataException($"{label} changed: 0x{offset:X}/0x{size:X}.");
    }

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length);

    private static int ReadInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void WriteWord(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void GuardWord(byte[] bytes, int offset, uint expected, string label)
    {
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

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);
    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequest(ArtisansNativeLockedChestVisualCandidateRequest request)
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
}
