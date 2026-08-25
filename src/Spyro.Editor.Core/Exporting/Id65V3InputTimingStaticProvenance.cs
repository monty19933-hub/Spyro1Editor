using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65V3InputTimingStaticProvenanceValidation(
    string ArtifactSha256,
    long ArtifactByteLength,
    string CandidateCueSha256,
    string CandidateBinSha256,
    string DiscExecutableSha256,
    string DuckStationExecutableSha256,
    uint PadBufferAddress,
    uint VsyncCounterAddress,
    bool LiveProcessUseProven,
    bool FourBytePadReadAtomicityProven,
    bool OneRenderedFrameCadenceProven,
    bool GloballyMonotonicVblankClockProven,
    bool RuntimeAccepted,
    bool NormalCreateBinAuthorized,
    bool ReleaseAccepted);

internal static partial class Id65V3RuntimeEvidenceContract
{
    internal const string InputTimingStaticProvenanceRelativePath =
        "docs/research/unused-level-65-v3-input-timing-static-provenance.json";
    internal const string ExpectedInputTimingStaticProvenanceSha256 =
        "e60a6e63aa013d1f08ab594052efc83ff6401841203fcacf2d1c2f81167b3e32";

    private const long InputTimingStaticProvenanceByteLength = 8_382;
    private const string InputTimingStaticProvenanceFormat =
        "spyro-id65-v3-input-timing-static-provenance";
    private const int InputTimingStaticProvenanceFormatVersion = 1;
    private const string InputTimingCandidateDirectory =
        "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-environment-header-repair-v3";
    private const string InputTimingCandidateLeaf =
        "Unused-Level-65-Remote-Blank-Environment-Header-Repair-v3-Sector216-RUNTIME-CANDIDATE";
    private const string InputTimingCandidateBinRelativePath =
        InputTimingCandidateDirectory + "/" + InputTimingCandidateLeaf + ".bin";
    private const string InputTimingCandidateCueRelativePath =
        InputTimingCandidateDirectory + "/" + InputTimingCandidateLeaf + ".cue";
    private const long InputTimingCandidateBinByteLength = 661_547_040;
    private const string InputTimingCandidateBinSha256 =
        "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45";
    private const int InputTimingCandidateCueByteLength = 148;
    private const string InputTimingCandidateCueSha256 =
        "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df";
    private const string InputTimingScusIsoIdentifier = "SCUS_942.28;1";
    private const string InputTimingScusLogicalName = "SCUS_942.28";
    private const int InputTimingScusLba = 55_382;
    private const int InputTimingScusByteLength = 0x66000;
    private const string InputTimingScusSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    private const int InputTimingPsxExeHeaderByteLength = 0x800;
    private const uint InputTimingPsxExeEntryPoint = 0x8005B8E0;
    private const uint InputTimingPsxExeLoadAddress = 0x80010000;
    private const int InputTimingPsxExeTextByteLength = 0x65800;
    private const string InputTimingDuckStationRelativePath =
        "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1/" +
        "DuckStation-ID65-Unattended-v1.app/Contents/MacOS/DuckStation";
    private const long InputTimingDuckStationByteLength = 22_354_208;
    private const string InputTimingDuckStationSha256 =
        "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645";
    private const string InputTimingDuckStationVersion = "0.1-11580-ge7f2f101c";
    private const string InputTimingDuckStationSourceCommit =
        "e7f2f101c2489e68446deff6dae9083e7a2114ea";
    private const string InputTimingProfileSettingsRelativePath =
        "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1/home/" +
        "Library/Application Support/DuckStation/settings.ini";
    private const int InputTimingProfileSettingsByteLength = 2_143;
    private const string InputTimingProfileSettingsSha256 =
        "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9";
    private const uint InputTimingPadBufferAddress = 0x800786A0;
    private const uint InputTimingSecondaryPadBufferAddress = 0x80078E50;
    private const uint InputTimingVsyncCounterAddress = 0x800758C8;

    private readonly record struct InputTimingButtonSpec(
        string Action,
        int KeyCode,
        string Macro,
        string Keyboard,
        string Target,
        int RawBit,
        string RawMask,
        int GameLogicalBit);

    private readonly record struct InputTimingInstructionSpec(uint Address, uint Word);

    private sealed record InputTimingWitnessSpec(
        string Id,
        uint StartVirtualAddress,
        int ByteLength,
        string Sha256,
        InputTimingInstructionSpec[] Instructions);

    internal readonly record struct InputTimingIsoRoot(int Extent, int ByteLength, int VolumeBlocks);

    private sealed record InputTimingBinInspection(string CandidateSha256, byte[] Executable);

    private static readonly InputTimingButtonSpec[] InputTimingButtons =
    [
        new("PAD_R1_TOGGLE", 18, "Macro1", "Keyboard/1", "R1", 11, "0x0800", 3),
        new("PAD_R2_TOGGLE", 19, "Macro2", "Keyboard/2", "R2", 9, "0x0200", 1),
        new("PAD_L1_TOGGLE", 20, "Macro3", "Keyboard/3", "L1", 10, "0x0400", 2),
        new("PAD_L2_TOGGLE", 21, "Macro4", "Keyboard/4", "L2", 8, "0x0100", 0),
        new("PAD_LEFT_TOGGLE", 23, "Macro5", "Keyboard/5", "Left", 7, "0x0080", 15),
        new("PAD_DOWN_TOGGLE", 22, "Macro6", "Keyboard/6", "Down", 6, "0x0040", 14)
    ];

    private static readonly InputTimingWitnessSpec[] InputTimingWitnesses =
    [
        new(
            "pad-initializer",
            0x800123C8,
            132,
            "a310596376bacd387e196afe5295562ab0283ffcb1c290b5439c3142c8c6be8e",
            [
                new(0x800123CC, 0x3C048008), new(0x800123D0, 0x248486A0),
                new(0x800123D4, 0x3C058008), new(0x800123D8, 0x24A58E50),
                new(0x800123E0, 0x0C01AC04), new(0x8001242C, 0x0C01A418),
                new(0x80012434, 0x0C014F1A), new(0x8001243C, 0x3C048005),
                new(0x80012440, 0x24843C68), new(0x80012444, 0x0C017796)
            ]),
        new(
            "pad-init-direct",
            0x8006B010,
            352,
            "05e7f29edce802e27b998d33bf0de7c8fe874a7d5e19a31fc37fe510895695fe",
            [
                new(0x8006B018, 0x00808821), new(0x8006B03C, 0x00A09021),
                new(0x8006B0E8, 0xAE110030), new(0x8006B0EC, 0xAE120120)
            ]),
        new(
            "vsync-callback-wrapper",
            0x8005DE58,
            52,
            "ceae3d1addb804daa1f2d456420c64049c4c00b3b5b8f0c9db72eb9853d8c7d3",
            [
                new(0x8005DE5C, 0x3C028007), new(0x8005DE60, 0x8C4249AC),
                new(0x8005DE64, 0x00802821), new(0x8005DE6C, 0x8C420014),
                new(0x8005DE74, 0x0040F809), new(0x8005DE78, 0x24040004)
            ]),
        new(
            "vsync-counter-prefix",
            0x80053C68,
            44,
            "fe52b87b3a506bc8730f42279fb78b70d378d2132b17009f59f14c0cd690bec2",
            [
                new(0x80053C68, 0x3C028007), new(0x80053C6C, 0x8C4258C8),
                new(0x80053C88, 0x24420001), new(0x80053C8C, 0x3C018007),
                new(0x80053C90, 0xAC2258C8)
            ]),
        new(
            "pad-consumer",
            0x80053EFC,
            200,
            "6b09cc3b11aeb129090bbfd8032060055afc45d8263b327b5f6413254232498e",
            [
                new(0x80053EFC, 0x3C028008), new(0x80053F00, 0x904286A0),
                new(0x80053F08, 0x10400005), new(0x80053F20, 0x3C038008),
                new(0x80053F24, 0x906386A1), new(0x80053F38, 0x24020041),
                new(0x80053F3C, 0x10620015), new(0x80053FA8, 0x3C028008),
                new(0x80053FAC, 0x904286A2), new(0x80053FB0, 0x3C038008),
                new(0x80053FB4, 0x906386A3), new(0x80053FB8, 0x00021200),
                new(0x80053FBC, 0x00628027)
            ]),
        new(
            "vsync-counter-reset",
            0x80013694,
            8,
            "8251d8de5f6dfe846c8b2fb7ef8aba4d722a5c636eedfe3d025d58f4c6fae211",
            [new(0x80013694, 0x3C018007), new(0x80013698, 0xAC2058C8)])
    ];

    internal static Id65V3InputTimingStaticProvenanceValidation
        VerifyPinnedInputTimingStaticProvenance(string sourceRepositoryRootPath)
    {
        string root = RequireInputTimingRepositoryRoot(sourceRepositoryRootPath);
        string artifactPath = InputTimingResolveRepositoryPath(root, InputTimingStaticProvenanceRelativePath);
        Id65V3RuntimeEvidenceFinalizer.StableFileRead artifactRead =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                artifactPath,
                64 * 1024,
                keepBytes: true,
                context: "ID65 input/timing static provenance");
        if (artifactRead.ByteLength != InputTimingStaticProvenanceByteLength ||
            artifactRead.Sha256 != ExpectedInputTimingStaticProvenanceSha256 ||
            artifactRead.Bytes is not byte[] artifactBytes)
        {
            throw new InvalidDataException("The pinned ID65 input/timing provenance identity changed.");
        }
        ValidateInputTimingStaticProvenanceArtifactForTest(artifactBytes);

        string cuePath = InputTimingResolveRepositoryPath(root, InputTimingCandidateCueRelativePath);
        Id65V3RuntimeEvidenceFinalizer.StableFileRead cueRead =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                cuePath,
                4 * 1024,
                keepBytes: true,
                context: "ID65 input/timing candidate CUE");
        if (cueRead.ByteLength != InputTimingCandidateCueByteLength ||
            cueRead.Sha256 != InputTimingCandidateCueSha256 || cueRead.Bytes is not byte[] cueBytes)
        {
            throw new InvalidDataException("The ID65 input/timing candidate CUE identity changed.");
        }
        ValidateInputTimingStaticProvenanceCueForTest(cueBytes, InputTimingCandidateLeaf + ".bin");

        string binPath = InputTimingResolveRepositoryPath(root, InputTimingCandidateBinRelativePath);
        InputTimingBinInspection binInspection =
            Id65V3RuntimeEvidenceFinalizer.InspectStableOwnedFileHandle(
                binPath,
                0x1A4,
                InputTimingCandidateBinByteLength,
                InspectInputTimingCandidateBin,
                "ID65 input/timing candidate BIN logical readback");
        ValidateInputTimingStaticProvenanceScusForTest(binInspection.Executable);

        string duckStationPath = InputTimingResolveRepositoryPath(root, InputTimingDuckStationRelativePath);
        Id65V3RuntimeEvidenceFinalizer.StableFileRead duckStationRead =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                duckStationPath,
                InputTimingDuckStationByteLength,
                keepBytes: false,
                context: "ID65 input/timing DuckStation executable");
        InputTimingRequireFileIdentity(
            duckStationRead,
            InputTimingDuckStationByteLength,
            InputTimingDuckStationSha256,
            "ID65 input/timing DuckStation executable");

        string settingsPath = InputTimingResolveRepositoryPath(root, InputTimingProfileSettingsRelativePath);
        Id65V3RuntimeEvidenceFinalizer.StableFileRead settingsRead =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                settingsPath,
                16 * 1024,
                keepBytes: true,
                context: "ID65 input/timing frozen profile settings");
        if (settingsRead.ByteLength != InputTimingProfileSettingsByteLength ||
            settingsRead.Sha256 != InputTimingProfileSettingsSha256 ||
            settingsRead.Bytes is not byte[] settingsBytes)
        {
            throw new InvalidDataException("The frozen ID65 input/timing profile settings identity changed.");
        }
        ValidateInputTimingProfileSettings(settingsBytes);

        return new(
            artifactRead.Sha256,
            artifactRead.ByteLength,
            cueRead.Sha256,
            binInspection.CandidateSha256,
            InputTimingScusSha256,
            duckStationRead.Sha256,
            InputTimingPadBufferAddress,
            InputTimingVsyncCounterAddress,
            LiveProcessUseProven: false,
            FourBytePadReadAtomicityProven: false,
            OneRenderedFrameCadenceProven: false,
            GloballyMonotonicVblankClockProven: false,
            RuntimeAccepted: false,
            NormalCreateBinAuthorized: false,
            ReleaseAccepted: false);
    }

    internal static void ValidateInputTimingStaticProvenanceArtifactForTest(
        ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length < 2 || utf8Json.Length > 64 * 1024 ||
            utf8Json[^1] != (byte)'\n' || utf8Json[^2] == (byte)'\n' ||
            utf8Json.Contains((byte)'\r') ||
            (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB &&
             utf8Json[2] == 0xBF))
        {
            throw new InvalidDataException(
                "ID65 input/timing provenance is not one exact LF-terminated UTF-8 JSON object.");
        }
        _ = DecodeStrictUtf8(utf8Json, "ID65 input/timing provenance");
        using JsonDocument document = JsonDocument.Parse(
            utf8Json[..^1].ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
        List<string> duplicateErrors = [];
        RejectDuplicateProperties(document.RootElement, "$", duplicateErrors);
        if (duplicateErrors.Count != 0)
            throw new InvalidDataException(duplicateErrors[0]);
        if (!utf8Json.SequenceEqual(SerializeVerifierCanonicalLf(document.RootElement)))
            throw new InvalidDataException("ID65 input/timing provenance is not strict canonical JSON.");
        ValidateInputTimingArtifactElement(document.RootElement);
    }

    internal static void ValidateInputTimingStaticProvenanceCueForTest(
        ReadOnlySpan<byte> cueBytes,
        string expectedBinLeaf)
    {
        if (string.IsNullOrEmpty(expectedBinLeaf) || expectedBinLeaf != Path.GetFileName(expectedBinLeaf) ||
            expectedBinLeaf.Any(character => character > 0x7F || character is '\r' or '\n' or '"'))
        {
            throw new InvalidDataException("The ID65 input/timing CUE leaf is invalid.");
        }
        string expected =
            $"FILE \"{expectedBinLeaf}\" BINARY\n" +
            "  TRACK 01 MODE2/2352\n" +
            "    INDEX 01 00:00:00\n";
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        if (!cueBytes.SequenceEqual(expectedBytes))
        {
            throw new InvalidDataException(
                "The ID65 input/timing CUE is not one exact ASCII FILE/TRACK/INDEX grammar.");
        }
    }

    internal static void ValidateInputTimingStaticProvenanceScusForTest(
        ReadOnlySpan<byte> executable)
    {
        if (executable.Length != InputTimingScusByteLength ||
            Hash(executable) != InputTimingScusSha256)
        {
            throw new InvalidDataException("The ID65 input/timing SCUS identity changed.");
        }
        if (!executable[..8].SequenceEqual("PS-X EXE"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable.Slice(0x10, 4)) !=
                InputTimingPsxExeEntryPoint ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable.Slice(0x18, 4)) !=
                InputTimingPsxExeLoadAddress ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable.Slice(0x1C, 4)) !=
                InputTimingPsxExeTextByteLength ||
            executable.Length != InputTimingPsxExeHeaderByteLength + InputTimingPsxExeTextByteLength)
        {
            throw new InvalidDataException("The ID65 input/timing PS-X EXE header changed.");
        }
        ValidateInputTimingWitnessReadback(executable);
        ValidateInputTimingMipsSemantics(executable);
    }

    private static InputTimingBinInspection InspectInputTimingCandidateBin(FileStream image)
    {
        if (image.Length != InputTimingCandidateBinByteLength)
            throw new InvalidDataException("The opened ID65 input/timing BIN length changed.");
        image.Position = 0;
        string beforeSha256 = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        if (beforeSha256 != InputTimingCandidateBinSha256 ||
            image.Position != InputTimingCandidateBinByteLength)
        {
            throw new InvalidDataException("The ID65 input/timing candidate BIN hash changed.");
        }

        DiscLayout sectorLayout = new(2352, 24, 0, 0);
        if (image.Length % sectorLayout.SectorSize != 0)
            throw new InvalidDataException("The ID65 input/timing BIN is not exact MODE2/2352 length.");
        byte[] pvd = InputTimingReadUserSector(image, sectorLayout, 16);
        InputTimingIsoRoot iso = ValidateInputTimingStaticProvenancePvdForTest(
            pvd,
            checked((int)(image.Length / sectorLayout.SectorSize)));
        DiscLayout layout = new(
            sectorLayout.SectorSize,
            sectorLayout.UserOffset,
            iso.Extent,
            iso.ByteLength);
        byte[] rootDirectory = DiscImage.ReadFileBytes(
            image, layout, iso.Extent, 0, iso.ByteLength);
        DiscFileRecord scus = ValidateInputTimingStaticProvenanceIsoRootForTest(rootDirectory);
        byte[] executable = DiscImage.ReadFileBytes(image, layout, scus.Lba, 0, scus.Size);

        image.Position = 0;
        string afterSha256 = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        if (afterSha256 != beforeSha256 || image.Position != InputTimingCandidateBinByteLength)
        {
            throw new InvalidDataException(
                "The ID65 input/timing candidate BIN changed during same-handle logical readback.");
        }
        return new(beforeSha256, executable);
    }

    internal static InputTimingIsoRoot ValidateInputTimingStaticProvenancePvdForTest(
        ReadOnlySpan<byte> pvd,
        int expectedVolumeBlocks)
    {
        if (pvd.Length != 2048 || pvd[0] != 1 || !pvd.Slice(1, 5).SequenceEqual("CD001"u8) ||
            pvd[6] != 1)
        {
            throw new InvalidDataException("The ID65 input/timing ISO primary volume descriptor is invalid.");
        }
        uint volumeLittle = BinaryPrimitives.ReadUInt32LittleEndian(pvd.Slice(80, 4));
        uint volumeBig = BinaryPrimitives.ReadUInt32BigEndian(pvd.Slice(84, 4));
        ushort blockLittle = BinaryPrimitives.ReadUInt16LittleEndian(pvd.Slice(128, 2));
        ushort blockBig = BinaryPrimitives.ReadUInt16BigEndian(pvd.Slice(130, 2));
        if (volumeLittle != volumeBig || volumeLittle != expectedVolumeBlocks ||
            blockLittle != blockBig || blockLittle != 2048)
        {
            throw new InvalidDataException("The ID65 input/timing PVD dual-endian fields disagree.");
        }
        int recordLength = pvd[156];
        if (recordLength < 34 || 156 + recordLength > pvd.Length || pvd[188] != 1 ||
            pvd[189] != 0 || (pvd[181] & 0x02) == 0)
        {
            throw new InvalidDataException("The ID65 input/timing PVD root directory record is invalid.");
        }
        uint extentLittle = BinaryPrimitives.ReadUInt32LittleEndian(pvd.Slice(158, 4));
        uint extentBig = BinaryPrimitives.ReadUInt32BigEndian(pvd.Slice(162, 4));
        uint lengthLittle = BinaryPrimitives.ReadUInt32LittleEndian(pvd.Slice(166, 4));
        uint lengthBig = BinaryPrimitives.ReadUInt32BigEndian(pvd.Slice(170, 4));
        if (extentLittle != extentBig || lengthLittle != lengthBig || extentLittle == 0 ||
            lengthLittle == 0 || lengthLittle > 4 * 1024 * 1024 ||
            extentLittle + ((lengthLittle + 2047) / 2048) > volumeLittle)
        {
            throw new InvalidDataException("The ID65 input/timing PVD root extent is invalid.");
        }
        return new(checked((int)extentLittle), checked((int)lengthLittle), checked((int)volumeLittle));
    }

    internal static DiscFileRecord ValidateInputTimingStaticProvenanceIsoRootForTest(
        ReadOnlySpan<byte> rootDirectory)
    {
        if (rootDirectory.Length == 0 || rootDirectory.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("The ID65 input/timing ISO root directory length is invalid.");
        DiscFileRecord? found = null;
        int offset = 0;
        while (offset < rootDirectory.Length)
        {
            int recordLength = rootDirectory[offset];
            if (recordLength == 0)
            {
                int nextSector = checked(((offset / 2048) + 1) * 2048);
                if (nextSector <= offset)
                    throw new InvalidDataException("The ID65 input/timing ISO root padding is invalid.");
                offset = Math.Min(nextSector, rootDirectory.Length);
                continue;
            }
            if (recordLength < 34 || offset + recordLength > rootDirectory.Length ||
                (offset / 2048) != ((offset + recordLength - 1) / 2048))
            {
                throw new InvalidDataException("The ID65 input/timing ISO root record is truncated.");
            }
            ReadOnlySpan<byte> record = rootDirectory.Slice(offset, recordLength);
            int nameLength = record[32];
            if (nameLength == 0 || 33 + nameLength > record.Length)
                throw new InvalidDataException("The ID65 input/timing ISO root name is truncated.");
            uint extentLittle = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(2, 4));
            uint extentBig = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(6, 4));
            uint lengthLittle = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(10, 4));
            uint lengthBig = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(14, 4));
            if (extentLittle != extentBig || lengthLittle != lengthBig)
                throw new InvalidDataException("An ID65 input/timing ISO root dual-endian record disagrees.");
            ReadOnlySpan<byte> nameBytes = record.Slice(33, nameLength);
            foreach (byte value in nameBytes)
            {
                if (value > 0x7F)
                    throw new InvalidDataException("The ID65 input/timing ISO root contains a non-ASCII name.");
            }
            string name = Encoding.ASCII.GetString(nameBytes);
            if (name == InputTimingScusIsoIdentifier)
            {
                if (found is not null)
                    throw new InvalidDataException("The ID65 input/timing ISO root duplicates SCUS_942.28;1.");
                if (extentLittle != InputTimingScusLba || lengthLittle != InputTimingScusByteLength ||
                    (record[25] & 0x02) != 0)
                {
                    throw new InvalidDataException("The ID65 input/timing SCUS ISO record identity changed.");
                }
                found = new(InputTimingScusLogicalName, checked((int)extentLittle), checked((int)lengthLittle));
            }
            offset += recordLength;
        }
        return found ?? throw new InvalidDataException(
            "The ID65 input/timing ISO root is missing exact SCUS_942.28;1.");
    }

    private static void ValidateInputTimingArtifactElement(JsonElement root)
    {
        InputTimingRequireObject(
            root,
            "$",
            "candidate", "discExecutable", "duckStation", "format", "formatVersion", "pad",
            "scope", "vsyncCounter", "witnesses");
        InputTimingRequireString(root, "format", InputTimingStaticProvenanceFormat);
        InputTimingRequireInt(root, "formatVersion", InputTimingStaticProvenanceFormatVersion);

        JsonElement candidate = InputTimingRequireProperty(root, "candidate");
        InputTimingRequireObject(candidate, "$.candidate", "bin", "cue");
        JsonElement bin = InputTimingRequireProperty(candidate, "bin");
        InputTimingRequireObject(bin, "$.candidate.bin", "byteLength", "relativePath", "sha256");
        InputTimingRequireLong(bin, "byteLength", InputTimingCandidateBinByteLength);
        InputTimingRequireString(bin, "relativePath", InputTimingCandidateBinRelativePath);
        InputTimingRequireString(bin, "sha256", InputTimingCandidateBinSha256);
        JsonElement cue = InputTimingRequireProperty(candidate, "cue");
        InputTimingRequireObject(cue, "$.candidate.cue", "byteLength", "relativePath", "sha256", "track");
        InputTimingRequireInt(cue, "byteLength", InputTimingCandidateCueByteLength);
        InputTimingRequireString(cue, "relativePath", InputTimingCandidateCueRelativePath);
        InputTimingRequireString(cue, "sha256", InputTimingCandidateCueSha256);
        JsonElement track = InputTimingRequireProperty(cue, "track");
        InputTimingRequireObject(
            track,
            "$.candidate.cue.track",
            "fileLeaf", "fileType", "indexNumber", "indexTime", "trackMode", "trackNumber");
        InputTimingRequireString(track, "fileLeaf", InputTimingCandidateLeaf + ".bin");
        InputTimingRequireString(track, "fileType", "BINARY");
        InputTimingRequireInt(track, "indexNumber", 1);
        InputTimingRequireString(track, "indexTime", "00:00:00");
        InputTimingRequireString(track, "trackMode", "MODE2/2352");
        InputTimingRequireInt(track, "trackNumber", 1);

        JsonElement discExecutable = InputTimingRequireProperty(root, "discExecutable");
        InputTimingRequireObject(
            discExecutable,
            "$.discExecutable",
            "byteLength", "isoFileIdentifier", "logicalBlockAddress", "logicalName", "psxExe", "sha256");
        InputTimingRequireInt(discExecutable, "byteLength", InputTimingScusByteLength);
        InputTimingRequireString(discExecutable, "isoFileIdentifier", InputTimingScusIsoIdentifier);
        InputTimingRequireInt(discExecutable, "logicalBlockAddress", InputTimingScusLba);
        InputTimingRequireString(discExecutable, "logicalName", InputTimingScusLogicalName);
        InputTimingRequireString(discExecutable, "sha256", InputTimingScusSha256);
        JsonElement psxExe = InputTimingRequireProperty(discExecutable, "psxExe");
        InputTimingRequireObject(
            psxExe,
            "$.discExecutable.psxExe",
            "entryPoint", "headerByteLength", "instructionSet", "loadAddress", "magic", "textByteLength");
        InputTimingRequireString(psxExe, "entryPoint", InputTimingHex(InputTimingPsxExeEntryPoint));
        InputTimingRequireInt(psxExe, "headerByteLength", InputTimingPsxExeHeaderByteLength);
        InputTimingRequireString(psxExe, "instructionSet", "mips-i-little-endian");
        InputTimingRequireString(psxExe, "loadAddress", InputTimingHex(InputTimingPsxExeLoadAddress));
        InputTimingRequireString(psxExe, "magic", "PS-X EXE");
        InputTimingRequireInt(psxExe, "textByteLength", InputTimingPsxExeTextByteLength);

        JsonElement duckStation = InputTimingRequireProperty(root, "duckStation");
        InputTimingRequireObject(
            duckStation,
            "$.duckStation",
            "executableByteLength", "executableSha256", "sourceCommit", "version");
        InputTimingRequireLong(duckStation, "executableByteLength", InputTimingDuckStationByteLength);
        InputTimingRequireString(duckStation, "executableSha256", InputTimingDuckStationSha256);
        InputTimingRequireString(duckStation, "sourceCommit", InputTimingDuckStationSourceCommit);
        InputTimingRequireString(duckStation, "version", InputTimingDuckStationVersion);

        ValidateInputTimingPadElement(InputTimingRequireProperty(root, "pad"));
        ValidateInputTimingScopeElement(InputTimingRequireProperty(root, "scope"));
        ValidateInputTimingCounterElement(InputTimingRequireProperty(root, "vsyncCounter"));
        ValidateInputTimingWitnessElement(InputTimingRequireProperty(root, "witnesses"));
    }

    private static void ValidateInputTimingPadElement(JsonElement pad)
    {
        InputTimingRequireObject(
            pad,
            "$.pad",
            "bufferAddress", "buttons", "consumer", "frame", "producer", "rspReadPacket");
        InputTimingRequireString(pad, "bufferAddress", InputTimingHex(InputTimingPadBufferAddress));
        InputTimingRequireString(
            pad,
            "rspReadPacket",
            BuildInputTimingRspReadPacket(InputTimingPadBufferAddress, 4));

        JsonElement frame = InputTimingRequireProperty(pad, "frame");
        InputTimingRequireObject(
            frame,
            "$.pad.frame",
            "activeLow", "byteLength", "firstInputByteOffset", "gameHeldExpression",
            "rawWordExpression", "requiredStatus", "requiredType", "secondInputByteOffset",
            "statusOffset", "typeOffset");
        InputTimingRequireBool(frame, "activeLow", true);
        InputTimingRequireInt(frame, "byteLength", 4);
        InputTimingRequireInt(frame, "firstInputByteOffset", 2);
        InputTimingRequireString(frame, "gameHeldExpression", "~rawWord & 0xffff");
        InputTimingRequireString(
            frame,
            "rawWordExpression",
            "(firstInputByte << 8) | secondInputByte");
        InputTimingRequireInt(frame, "requiredStatus", 0);
        InputTimingRequireInt(frame, "requiredType", 0x41);
        InputTimingRequireInt(frame, "secondInputByteOffset", 3);
        InputTimingRequireInt(frame, "statusOffset", 0);
        InputTimingRequireInt(frame, "typeOffset", 1);

        JsonElement buttons = InputTimingRequireProperty(pad, "buttons");
        if (buttons.ValueKind != JsonValueKind.Array ||
            buttons.GetArrayLength() != InputTimingButtons.Length)
        {
            throw new InvalidDataException("The ID65 input/timing button projection count changed.");
        }
        HashSet<int> rawBits = [];
        HashSet<int> gameBits = [];
        int buttonIndex = 0;
        foreach (JsonElement button in buttons.EnumerateArray())
        {
            InputTimingButtonSpec expected = InputTimingButtons[buttonIndex++];
            InputTimingRequireObject(
                button,
                "$.pad.buttons[]",
                "action", "actionSpecKeyCode", "actionSpecProfileBinding", "gameLogicalBit",
                "profileTargetBinding", "profileToggleBinding", "rawBit", "rawMask");
            InputTimingRequireString(button, "action", expected.Action);
            InputTimingRequireInt(button, "actionSpecKeyCode", expected.KeyCode);
            InputTimingRequireStringArray(
                button,
                "actionSpecProfileBinding",
                ["Pad1", expected.Macro, expected.Keyboard]);
            InputTimingRequireStringArray(
                button,
                "profileTargetBinding",
                ["Pad1", expected.Macro + "Binds", expected.Target]);
            InputTimingRequireStringArray(
                button,
                "profileToggleBinding",
                ["Pad1", expected.Macro + "Toggle", "true"]);
            InputTimingRequireInt(button, "rawBit", expected.RawBit);
            InputTimingRequireString(button, "rawMask", expected.RawMask);
            InputTimingRequireInt(button, "gameLogicalBit", expected.GameLogicalBit);
            ushort rawMask = checked((ushort)(1 << expected.RawBit));
            if ($"0x{rawMask:x4}" != expected.RawMask ||
                expected.GameLogicalBit != (expected.RawBit >= 8
                    ? expected.RawBit - 8
                    : expected.RawBit + 8) ||
                !rawBits.Add(expected.RawBit) || !gameBits.Add(expected.GameLogicalBit))
            {
                throw new InvalidDataException("The ID65 input/timing active-low button derivation is invalid.");
            }
        }

        JsonElement producer = InputTimingRequireProperty(pad, "producer");
        InputTimingRequireObject(
            producer,
            "$.pad.producer",
            "directPadVSync", "padInitDirect", "padStartCom", "vsyncCallback");
        InputTimingRequireCall(
            InputTimingRequireProperty(producer, "directPadVSync"),
            "$.pad.producer.directPadVSync",
            0x80012434,
            0x80053C68);
        InputTimingRequireCall(
            InputTimingRequireProperty(producer, "padStartCom"),
            "$.pad.producer.padStartCom",
            0x8001242C,
            0x80069060);
        JsonElement padInit = InputTimingRequireProperty(producer, "padInitDirect");
        InputTimingRequireObject(
            padInit,
            "$.pad.producer.padInitDirect",
            "argument0Address", "argument0PreserveAddress", "argument0StoreAddress",
            "argument1Address", "argument1PreserveAddress", "argument1StoreAddress",
            "callAddress", "targetAddress");
        InputTimingRequireAddress(padInit, "argument0Address", InputTimingPadBufferAddress);
        InputTimingRequireAddress(padInit, "argument0PreserveAddress", 0x8006B018);
        InputTimingRequireAddress(padInit, "argument0StoreAddress", 0x8006B0E8);
        InputTimingRequireAddress(padInit, "argument1Address", InputTimingSecondaryPadBufferAddress);
        InputTimingRequireAddress(padInit, "argument1PreserveAddress", 0x8006B03C);
        InputTimingRequireAddress(padInit, "argument1StoreAddress", 0x8006B0EC);
        InputTimingRequireAddress(padInit, "callAddress", 0x800123E0);
        InputTimingRequireAddress(padInit, "targetAddress", 0x8006B010);

        JsonElement callback = InputTimingRequireProperty(producer, "vsyncCallback");
        InputTimingRequireObject(
            callback,
            "$.pad.producer.vsyncCallback",
            "callAddress", "callbackAddress", "callbackLoadHighAddress", "callbackLoadLowAddress",
            "targetAddress", "wrapperArgumentMoveAddress", "wrapperDispatchAddress",
            "wrapperServiceSelector", "wrapperServiceSelectorAddress");
        InputTimingRequireAddress(callback, "callAddress", 0x80012444);
        InputTimingRequireAddress(callback, "callbackAddress", 0x80053C68);
        InputTimingRequireAddress(callback, "callbackLoadHighAddress", 0x8001243C);
        InputTimingRequireAddress(callback, "callbackLoadLowAddress", 0x80012440);
        InputTimingRequireAddress(callback, "targetAddress", 0x8005DE58);
        InputTimingRequireAddress(callback, "wrapperArgumentMoveAddress", 0x8005DE64);
        InputTimingRequireAddress(callback, "wrapperDispatchAddress", 0x8005DE74);
        InputTimingRequireInt(callback, "wrapperServiceSelector", 4);
        InputTimingRequireAddress(callback, "wrapperServiceSelectorAddress", 0x8005DE78);

        JsonElement consumer = InputTimingRequireProperty(pad, "consumer");
        InputTimingRequireObject(
            consumer,
            "$.pad.consumer",
            "activeLowCombineAddress", "firstInputByte", "firstInputByteShiftAddress",
            "secondInputByte", "status", "type");
        InputTimingRequireAddress(consumer, "activeLowCombineAddress", 0x80053FBC);
        InputTimingRequireAddress(consumer, "firstInputByteShiftAddress", 0x80053FB8);
        InputTimingRequireLoad(
            InputTimingRequireProperty(consumer, "status"),
            "$.pad.consumer.status",
            0x80053EFC,
            0x80053F00,
            0x800786A0);
        InputTimingRequireLoad(
            InputTimingRequireProperty(consumer, "type"),
            "$.pad.consumer.type",
            0x80053F20,
            0x80053F24,
            0x800786A1);
        InputTimingRequireLoad(
            InputTimingRequireProperty(consumer, "firstInputByte"),
            "$.pad.consumer.firstInputByte",
            0x80053FA8,
            0x80053FAC,
            0x800786A2);
        InputTimingRequireLoad(
            InputTimingRequireProperty(consumer, "secondInputByte"),
            "$.pad.consumer.secondInputByte",
            0x80053FB0,
            0x80053FB4,
            0x800786A3);
    }

    private static void ValidateInputTimingScopeElement(JsonElement scope)
    {
        InputTimingRequireObject(scope, "$.scope", "doesNotProve", "proves");
        InputTimingRequireDirectStringArray(
            InputTimingRequireProperty(scope, "doesNotProve"),
            [
                "live-process-use",
                "four-byte-pad-read-atomicity",
                "one-rendered-frame-cadence",
                "globally-monotonic-vblank-clock",
                "runtime-acceptance",
                "release-acceptance"
            ],
            "$.scope.doesNotProve");
        InputTimingRequireDirectStringArray(
            InputTimingRequireProperty(scope, "proves"),
            [
                "exact-candidate-bin-cue-identity",
                "exact-logical-scus-identity",
                "exact-duckstation-executable-identity",
                "static-pad-producer-consumer-address-chain",
                "static-active-low-digital-pad-bit-mapping",
                "static-vsync-callback-counter-increment-and-reset"
            ],
            "$.scope.proves");
    }

    private static void ValidateInputTimingCounterElement(JsonElement counter)
    {
        InputTimingRequireObject(
            counter,
            "$.vsyncCounter",
            "address", "callbackAddress", "increment", "reset", "rspReadPacket", "semantics");
        InputTimingRequireAddress(counter, "address", InputTimingVsyncCounterAddress);
        InputTimingRequireAddress(counter, "callbackAddress", 0x80053C68);
        InputTimingRequireString(
            counter,
            "rspReadPacket",
            BuildInputTimingRspReadPacket(InputTimingVsyncCounterAddress, 4));
        InputTimingRequireString(counter, "semantics", "callback-invocation-level-tick-counter");
        JsonElement increment = InputTimingRequireProperty(counter, "increment");
        InputTimingRequireObject(
            increment,
            "$.vsyncCounter.increment",
            "addAddress", "beforeFirstBranch", "delta", "loadAddress", "storeAddress");
        InputTimingRequireAddress(increment, "addAddress", 0x80053C88);
        InputTimingRequireBool(increment, "beforeFirstBranch", true);
        InputTimingRequireInt(increment, "delta", 1);
        InputTimingRequireAddress(increment, "loadAddress", 0x80053C6C);
        InputTimingRequireAddress(increment, "storeAddress", 0x80053C90);
        JsonElement reset = InputTimingRequireProperty(counter, "reset");
        InputTimingRequireObject(
            reset,
            "$.vsyncCounter.reset",
            "sequenceAddress", "storeAddress", "value");
        InputTimingRequireAddress(reset, "sequenceAddress", 0x80013694);
        InputTimingRequireAddress(reset, "storeAddress", 0x80013698);
        InputTimingRequireInt(reset, "value", 0);
    }

    private static void ValidateInputTimingWitnessElement(JsonElement witnesses)
    {
        if (witnesses.ValueKind != JsonValueKind.Array ||
            witnesses.GetArrayLength() != InputTimingWitnesses.Length)
        {
            throw new InvalidDataException("The ID65 input/timing witness count changed.");
        }
        int witnessIndex = 0;
        foreach (JsonElement witness in witnesses.EnumerateArray())
        {
            InputTimingWitnessSpec expected = InputTimingWitnesses[witnessIndex++];
            InputTimingRequireObject(
                witness,
                "$.witnesses[]",
                "byteLength", "id", "instructions", "sha256", "startVirtualAddress");
            InputTimingRequireInt(witness, "byteLength", expected.ByteLength);
            InputTimingRequireString(witness, "id", expected.Id);
            InputTimingRequireString(witness, "sha256", expected.Sha256);
            InputTimingRequireAddress(witness, "startVirtualAddress", expected.StartVirtualAddress);
            JsonElement instructions = InputTimingRequireProperty(witness, "instructions");
            if (instructions.ValueKind != JsonValueKind.Array ||
                instructions.GetArrayLength() != expected.Instructions.Length)
            {
                throw new InvalidDataException($"The ID65 input/timing {expected.Id} instruction count changed.");
            }
            int instructionIndex = 0;
            foreach (JsonElement instruction in instructions.EnumerateArray())
            {
                InputTimingInstructionSpec expectedInstruction = expected.Instructions[instructionIndex++];
                InputTimingRequireObject(instruction, "$.witnesses[].instructions[]", "address", "word");
                InputTimingRequireAddress(instruction, "address", expectedInstruction.Address);
                InputTimingRequireString(instruction, "word", InputTimingHex(expectedInstruction.Word));
            }
        }
    }

    private static void InputTimingRequireObject(
        JsonElement element,
        string path,
        params string[] exactPropertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{path} is not an object.");
        string[] actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (!actual.Order(StringComparer.Ordinal).SequenceEqual(
                exactPropertyNames.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{path} does not have its exact property set.");
        }
    }

    private static JsonElement InputTimingRequireProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value)
            ? value
            : throw new InvalidDataException($"Missing ID65 input/timing property '{propertyName}'.");

    private static void InputTimingRequireString(
        JsonElement element,
        string propertyName,
        string expected)
    {
        JsonElement value = InputTimingRequireProperty(element, propertyName);
        if (value.ValueKind != JsonValueKind.String || value.GetString() != expected)
            throw new InvalidDataException($"ID65 input/timing property '{propertyName}' changed.");
    }

    private static void InputTimingRequireLong(
        JsonElement element,
        string propertyName,
        long expected)
    {
        JsonElement value = InputTimingRequireProperty(element, propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long actual) ||
            actual != expected)
        {
            throw new InvalidDataException($"ID65 input/timing property '{propertyName}' changed.");
        }
    }

    private static void InputTimingRequireInt(
        JsonElement element,
        string propertyName,
        int expected) =>
        InputTimingRequireLong(element, propertyName, expected);

    private static void InputTimingRequireBool(
        JsonElement element,
        string propertyName,
        bool expected)
    {
        JsonElement value = InputTimingRequireProperty(element, propertyName);
        if ((expected && value.ValueKind != JsonValueKind.True) ||
            (!expected && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"ID65 input/timing property '{propertyName}' changed.");
        }
    }

    private static void InputTimingRequireStringArray(
        JsonElement element,
        string propertyName,
        string[] expected) =>
        InputTimingRequireDirectStringArray(
            InputTimingRequireProperty(element, propertyName),
            expected,
            propertyName);

    private static void InputTimingRequireDirectStringArray(
        JsonElement element,
        string[] expected,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != expected.Length)
            throw new InvalidDataException($"{path} is not its exact ordered string array.");
        int index = 0;
        foreach (JsonElement value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || value.GetString() != expected[index++])
                throw new InvalidDataException($"{path} is not its exact ordered string array.");
        }
    }

    private static void InputTimingRequireAddress(
        JsonElement element,
        string propertyName,
        uint expected)
    {
        InputTimingRequireString(element, propertyName, InputTimingHex(expected));
        _ = InputTimingParseHex(InputTimingRequireProperty(element, propertyName).GetString()!);
    }

    private static void InputTimingRequireCall(
        JsonElement element,
        string path,
        uint callAddress,
        uint targetAddress)
    {
        InputTimingRequireObject(element, path, "callAddress", "targetAddress");
        InputTimingRequireAddress(element, "callAddress", callAddress);
        InputTimingRequireAddress(element, "targetAddress", targetAddress);
    }

    private static void InputTimingRequireLoad(
        JsonElement element,
        string path,
        uint luiAddress,
        uint loadAddress,
        uint effectiveAddress)
    {
        InputTimingRequireObject(element, path, "effectiveAddress", "loadAddress", "luiAddress");
        InputTimingRequireAddress(element, "effectiveAddress", effectiveAddress);
        InputTimingRequireAddress(element, "loadAddress", loadAddress);
        InputTimingRequireAddress(element, "luiAddress", luiAddress);
    }

    private static string InputTimingHex(uint value) => $"0x{value:x8}";

    private static uint InputTimingParseHex(string value)
    {
        if (value.Length != 10 || !value.StartsWith("0x", StringComparison.Ordinal) ||
            !uint.TryParse(value.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                out uint parsed) || value != InputTimingHex(parsed))
        {
            throw new InvalidDataException("An ID65 input/timing address or word is not fixed lowercase hex.");
        }
        return parsed;
    }

    private static string BuildInputTimingRspReadPacket(uint address, int byteLength)
    {
        if (byteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        string payload = $"m{address:x},{byteLength:x}";
        int checksum = 0;
        foreach (char character in payload)
        {
            if (character > 0x7F)
                throw new InvalidDataException("The ID65 input/timing RSP payload is not ASCII.");
            checksum = (checksum + character) & 0xFF;
        }
        return $"${payload}#{checksum:x2}";
    }

    private static string RequireInputTimingRepositoryRoot(string sourceRepositoryRootPath)
    {
        if (string.IsNullOrWhiteSpace(sourceRepositoryRootPath) ||
            !Path.IsPathFullyQualified(sourceRepositoryRootPath))
        {
            throw new ArgumentException(
                "The ID65 input/timing verifier requires an absolute repository root.",
                nameof(sourceRepositoryRootPath));
        }
        string root = Path.GetFullPath(sourceRepositoryRootPath);
        if (!Directory.Exists(root) || new DirectoryInfo(root).LinkTarget is not null)
            throw new DirectoryNotFoundException("The ID65 input/timing repository root is missing or linked.");
        return root;
    }

    private static string InputTimingResolveRepositoryPath(string root, string relativePath)
    {
        GuardProfileManifestPath(relativePath);
        string full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("An ID65 input/timing repository path escaped its root.");
        return full;
    }

    private static void InputTimingRequireFileIdentity(
        Id65V3RuntimeEvidenceFinalizer.StableFileRead read,
        long byteLength,
        string sha256,
        string context)
    {
        if (read.ByteLength != byteLength || read.Sha256 != sha256 || read.LinkCount != 1)
            throw new InvalidDataException($"The {context} stable identity changed.");
    }

    private static byte[] InputTimingReadUserSector(FileStream image, DiscLayout layout, int lba)
    {
        if (lba < 0)
            throw new ArgumentOutOfRangeException(nameof(lba));
        long offset = checked(((long)lba * layout.SectorSize) + layout.UserOffset);
        if (offset < 0 || offset + 2048 > image.Length)
            throw new EndOfStreamException("The ID65 input/timing logical sector is truncated.");
        byte[] sector = new byte[2048];
        image.Position = offset;
        image.ReadExactly(sector);
        return sector;
    }

    private static void ValidateInputTimingProfileSettings(ReadOnlySpan<byte> settingsBytes)
    {
        if (settingsBytes.Length != InputTimingProfileSettingsByteLength ||
            settingsBytes.Contains((byte)'\r') || settingsBytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException("The frozen ID65 input/timing settings encoding changed.");
        }
        string text = DecodeStrictUtf8(settingsBytes, "frozen ID65 input/timing settings");
        Dictionary<(string Section, string Key), string> values = [];
        string section = string.Empty;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
                continue;
            if (line.StartsWith("[", StringComparison.Ordinal) &&
                line.EndsWith("]", StringComparison.Ordinal))
            {
                section = line[1..^1];
                continue;
            }
            int equals = line.IndexOf('=');
            if (equals <= 0 || section.Length == 0)
                throw new InvalidDataException("The frozen ID65 input/timing settings grammar changed.");
            string key = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();
            if (!values.TryAdd((section, key), value))
                throw new InvalidDataException("The frozen ID65 input/timing settings duplicate a key.");
        }
        foreach (InputTimingButtonSpec button in InputTimingButtons)
        {
            InputTimingRequireSetting(values, "Pad1", button.Macro, button.Keyboard);
            InputTimingRequireSetting(values, "Pad1", button.Macro + "Binds", button.Target);
            InputTimingRequireSetting(values, "Pad1", button.Macro + "Toggle", "true");
        }
    }

    private static void InputTimingRequireSetting(
        IReadOnlyDictionary<(string Section, string Key), string> values,
        string section,
        string key,
        string expected)
    {
        if (!values.TryGetValue((section, key), out string? value) || value != expected)
            throw new InvalidDataException($"The frozen ID65 input/timing setting {section}/{key} changed.");
    }

    private static void ValidateInputTimingWitnessReadback(ReadOnlySpan<byte> executable)
    {
        foreach (InputTimingWitnessSpec witness in InputTimingWitnesses)
        {
            ReadOnlySpan<byte> window = InputTimingVirtualBytes(
                executable, witness.StartVirtualAddress, witness.ByteLength);
            if (Hash(window) != witness.Sha256)
            {
                throw new InvalidDataException(
                    $"The ID65 input/timing {witness.Id} contiguous witness changed.");
            }
            foreach (InputTimingInstructionSpec instruction in witness.Instructions)
            {
                uint actual = InputTimingReadInstruction(executable, instruction.Address);
                if (actual != instruction.Word)
                {
                    throw new InvalidDataException(
                        $"The ID65 input/timing instruction at {InputTimingHex(instruction.Address)} changed.");
                }
            }
        }
    }

    private static void ValidateInputTimingMipsSemantics(ReadOnlySpan<byte> executable)
    {
        uint primary = InputTimingDecodeLuiAddiuAddress(
            executable, 0x800123CC, 0x800123D0, targetRegister: 4);
        uint secondary = InputTimingDecodeLuiAddiuAddress(
            executable, 0x800123D4, 0x800123D8, targetRegister: 5);
        if (primary != InputTimingPadBufferAddress || secondary != InputTimingSecondaryPadBufferAddress)
            throw new InvalidDataException("PadInitDirect caller buffer derivation changed.");
        InputTimingRequireJal(executable, 0x800123E0, 0x8006B010);
        InputTimingRequireJal(executable, 0x8001242C, 0x80069060);
        InputTimingRequireJal(executable, 0x80012434, 0x80053C68);
        uint callback = InputTimingDecodeLuiAddiuAddress(
            executable, 0x8001243C, 0x80012440, targetRegister: 4);
        if (callback != 0x80053C68)
            throw new InvalidDataException("The PadVSync callback pointer derivation changed.");
        InputTimingRequireJal(executable, 0x80012444, 0x8005DE58);

        InputTimingRequireAddu(
            InputTimingReadInstruction(executable, 0x8006B018),
            destination: 17,
            left: 4,
            right: 0,
            "PadInitDirect argument 0 preservation");
        InputTimingRequireAddu(
            InputTimingReadInstruction(executable, 0x8006B03C),
            destination: 18,
            left: 5,
            right: 0,
            "PadInitDirect argument 1 preservation");
        InputTimingRequireMemory(
            InputTimingReadInstruction(executable, 0x8006B0E8),
            opcode: 0x2B,
            register: 17,
            @base: 16,
            displacement: 0x30,
            "PadInitDirect primary communication-record store");
        InputTimingRequireMemory(
            InputTimingReadInstruction(executable, 0x8006B0EC),
            opcode: 0x2B,
            register: 18,
            @base: 16,
            displacement: 0x120,
            "PadInitDirect secondary communication-record store");

        InputTimingRequireAddu(
            InputTimingReadInstruction(executable, 0x8005DE64),
            destination: 5,
            left: 4,
            right: 0,
            "VSyncCallback wrapper argument forwarding");
        InputTimingRequireMemory(
            InputTimingReadInstruction(executable, 0x8005DE6C),
            opcode: 0x23,
            register: 2,
            @base: 2,
            displacement: 0x14,
            "VSyncCallback wrapper dispatch load");
        InputTimingRequireJalr(
            InputTimingReadInstruction(executable, 0x8005DE74),
            source: 2,
            destination: 31,
            "VSyncCallback wrapper dispatch");
        InputTimingRequireAddiu(
            InputTimingReadInstruction(executable, 0x8005DE78),
            source: 0,
            target: 4,
            immediate: 4,
            "VSyncCallback wrapper service selector");

        uint counterLoad = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053C68, 0x80053C6C, opcode: 0x23, targetRegister: 2);
        InputTimingRequireAddiu(
            InputTimingReadInstruction(executable, 0x80053C88),
            source: 2,
            target: 2,
            immediate: 1,
            "PadVSync level-tick increment");
        uint counterStore = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053C8C, 0x80053C90, opcode: 0x2B, targetRegister: 2);
        if (counterLoad != InputTimingVsyncCounterAddress || counterStore != counterLoad)
            throw new InvalidDataException("The PadVSync level-tick load/store address changed.");
        for (uint address = 0x80053C68; address <= 0x80053C90; address += 4)
        {
            if (InputTimingIsControlTransfer(InputTimingReadInstruction(executable, address)))
                throw new InvalidDataException("The PadVSync level-tick increment moved after a branch.");
        }

        uint resetStore = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80013694, 0x80013698, opcode: 0x2B, targetRegister: 0);
        if (resetStore != InputTimingVsyncCounterAddress)
            throw new InvalidDataException("The level-tick reset address changed.");

        uint status = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053EFC, 0x80053F00, opcode: 0x24, targetRegister: 2);
        InputTimingRequireBranchEqual(
            InputTimingReadInstruction(executable, 0x80053F08),
            left: 2,
            right: 0,
            "PadVSync status-zero check");
        uint type = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053F20, 0x80053F24, opcode: 0x24, targetRegister: 3);
        InputTimingRequireAddiu(
            InputTimingReadInstruction(executable, 0x80053F38),
            source: 0,
            target: 2,
            immediate: 0x41,
            "PadVSync digital-controller type constant");
        InputTimingRequireBranchEqual(
            InputTimingReadInstruction(executable, 0x80053F3C),
            left: 3,
            right: 2,
            "PadVSync digital-controller type check");
        uint first = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053FA8, 0x80053FAC, opcode: 0x24, targetRegister: 2);
        uint second = InputTimingDecodeLuiMemoryAddress(
            executable, 0x80053FB0, 0x80053FB4, opcode: 0x24, targetRegister: 3);
        InputTimingRequireSll(
            InputTimingReadInstruction(executable, 0x80053FB8),
            destination: 2,
            source: 2,
            shift: 8,
            "PadVSync first input-byte shift");
        InputTimingRequireNor(
            InputTimingReadInstruction(executable, 0x80053FBC),
            destination: 16,
            left: 3,
            right: 2,
            "PadVSync active-low combine");
        if (status != InputTimingPadBufferAddress || type != InputTimingPadBufferAddress + 1 ||
            first != InputTimingPadBufferAddress + 2 || second != InputTimingPadBufferAddress + 3)
        {
            throw new InvalidDataException("The PadVSync four-byte consumer address chain changed.");
        }
    }

    private static ReadOnlySpan<byte> InputTimingVirtualBytes(
        ReadOnlySpan<byte> executable,
        uint virtualAddress,
        int byteLength)
    {
        if (virtualAddress < InputTimingPsxExeLoadAddress || byteLength < 0)
            throw new InvalidDataException("An ID65 input/timing witness address is outside PS-X EXE text.");
        long offset = InputTimingPsxExeHeaderByteLength +
            ((long)virtualAddress - InputTimingPsxExeLoadAddress);
        if (offset < InputTimingPsxExeHeaderByteLength || offset + byteLength > executable.Length)
            throw new InvalidDataException("An ID65 input/timing witness range is outside SCUS.");
        return executable.Slice(checked((int)offset), byteLength);
    }

    private static uint InputTimingReadInstruction(ReadOnlySpan<byte> executable, uint virtualAddress) =>
        BinaryPrimitives.ReadUInt32LittleEndian(InputTimingVirtualBytes(executable, virtualAddress, 4));

    private static uint InputTimingDecodeLuiAddiuAddress(
        ReadOnlySpan<byte> executable,
        uint luiAddress,
        uint addiuAddress,
        int targetRegister)
    {
        uint lui = InputTimingReadInstruction(executable, luiAddress);
        InputTimingRequireLui(lui, targetRegister, "address high half");
        uint addiu = InputTimingReadInstruction(executable, addiuAddress);
        InputTimingRequireAddiu(
            addiu,
            source: targetRegister,
            target: targetRegister,
            immediate: unchecked((short)(addiu & 0xFFFF)),
            "address low half");
        return unchecked((uint)(((lui & 0xFFFF) << 16) + (short)(addiu & 0xFFFF)));
    }

    private static uint InputTimingDecodeLuiMemoryAddress(
        ReadOnlySpan<byte> executable,
        uint luiAddress,
        uint memoryAddress,
        int opcode,
        int targetRegister)
    {
        uint lui = InputTimingReadInstruction(executable, luiAddress);
        int baseRegister = checked((int)((lui >> 16) & 0x1F));
        InputTimingRequireLui(lui, baseRegister, "memory address high half");
        uint memory = InputTimingReadInstruction(executable, memoryAddress);
        InputTimingRequireMemory(
            memory,
            opcode,
            targetRegister,
            baseRegister,
            unchecked((short)(memory & 0xFFFF)),
            "absolute memory witness");
        return unchecked((uint)(((lui & 0xFFFF) << 16) + (short)(memory & 0xFFFF)));
    }

    private static void InputTimingRequireLui(uint word, int target, string context)
    {
        if ((word >> 26) != 0x0F || ((word >> 21) & 0x1F) != 0 ||
            ((word >> 16) & 0x1F) != target)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} LUI changed.");
        }
    }

    private static void InputTimingRequireAddiu(
        uint word,
        int source,
        int target,
        int immediate,
        string context)
    {
        if ((word >> 26) != 0x09 || ((word >> 21) & 0x1F) != source ||
            ((word >> 16) & 0x1F) != target || (short)(word & 0xFFFF) != immediate)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} ADDIU changed.");
        }
    }

    private static void InputTimingRequireMemory(
        uint word,
        int opcode,
        int register,
        int @base,
        int displacement,
        string context)
    {
        if ((word >> 26) != opcode || ((word >> 21) & 0x1F) != @base ||
            ((word >> 16) & 0x1F) != register || (short)(word & 0xFFFF) != displacement)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} memory instruction changed.");
        }
    }

    private static void InputTimingRequireJal(
        ReadOnlySpan<byte> executable,
        uint programCounter,
        uint expectedTarget)
    {
        uint word = InputTimingReadInstruction(executable, programCounter);
        if ((word >> 26) != 0x03)
            throw new InvalidDataException("An ID65 input/timing JAL opcode changed.");
        uint target = ((programCounter + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2);
        if (target != expectedTarget)
            throw new InvalidDataException("An ID65 input/timing JAL target changed.");
    }

    private static void InputTimingRequireAddu(
        uint word,
        int destination,
        int left,
        int right,
        string context)
    {
        if ((word >> 26) != 0 || (word & 0x3F) != 0x21 ||
            ((word >> 21) & 0x1F) != left || ((word >> 16) & 0x1F) != right ||
            ((word >> 11) & 0x1F) != destination || ((word >> 6) & 0x1F) != 0)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} ADDU changed.");
        }
    }

    private static void InputTimingRequireJalr(
        uint word,
        int source,
        int destination,
        string context)
    {
        if ((word >> 26) != 0 || (word & 0x3F) != 0x09 ||
            ((word >> 21) & 0x1F) != source || ((word >> 16) & 0x1F) != 0 ||
            ((word >> 11) & 0x1F) != destination || ((word >> 6) & 0x1F) != 0)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} JALR changed.");
        }
    }

    private static void InputTimingRequireBranchEqual(
        uint word,
        int left,
        int right,
        string context)
    {
        if ((word >> 26) != 0x04 || ((word >> 21) & 0x1F) != left ||
            ((word >> 16) & 0x1F) != right)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} BEQ changed.");
        }
    }

    private static void InputTimingRequireSll(
        uint word,
        int destination,
        int source,
        int shift,
        string context)
    {
        if ((word >> 26) != 0 || (word & 0x3F) != 0 || ((word >> 21) & 0x1F) != 0 ||
            ((word >> 16) & 0x1F) != source || ((word >> 11) & 0x1F) != destination ||
            ((word >> 6) & 0x1F) != shift)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} SLL changed.");
        }
    }

    private static void InputTimingRequireNor(
        uint word,
        int destination,
        int left,
        int right,
        string context)
    {
        if ((word >> 26) != 0 || (word & 0x3F) != 0x27 ||
            ((word >> 21) & 0x1F) != left || ((word >> 16) & 0x1F) != right ||
            ((word >> 11) & 0x1F) != destination || ((word >> 6) & 0x1F) != 0)
        {
            throw new InvalidDataException($"The ID65 input/timing {context} NOR changed.");
        }
    }

    private static bool InputTimingIsControlTransfer(uint word)
    {
        uint opcode = word >> 26;
        if (opcode is >= 1 and <= 7)
            return true;
        if (opcode != 0)
            return false;
        uint function = word & 0x3F;
        return function is 0x08 or 0x09;
    }
}
