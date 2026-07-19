using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

public sealed record ArtisansKeyGatedWoodenChestCandidateRequest(
    string SourceImagePath,
    string OutputImagePath,
    int KeyRawX = 0x10AEC,
    int KeyRawY = 0x15185,
    int KeyRawZ = 0x1800);

public sealed record ArtisansKeyGatedWoodenChestCandidatePatch(
    string Label,
    string Storage,
    string Address,
    long FileOffset,
    long ImageOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256,
    string BeforeHex,
    string AfterHex);

public sealed record ArtisansKeyGatedWoodenChestCandidatePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string SourceImagePath,
    string OutputImagePath,
    int SectorSize,
    int UserOffset,
    int WadLba,
    string ExecutableName,
    int ExecutableLba,
    string KeyDonorLevel,
    int KeyDonorTrueIndex,
    string KeyVisualDonorLevel,
    int KeyVisualDonorTrueIndex,
    string KeyVisualClass,
    int AppendedKeyTrueIndex,
    int GatedChestTrueIndex,
    int OriginalSourceCount,
    int FinalSourceCount,
    int KeyRawX,
    int KeyRawY,
    int KeyRawZ,
    string KeySpecialDataSourceRelativeOffset,
    bool ScenePointerFixupAppended,
    string SceneWadOffset,
    string ScenePointerFixupCountWadOffset,
    int OriginalScenePointerFixupCount,
    int FinalScenePointerFixupCount,
    string AppendedScenePointerFieldOffset,
    string PointerPolicy,
    string DispatchHookAddress,
    string DispatchDelaySlotWord,
    string GemHandlerHookAddress,
    string GemHandlerResumeAddress,
    string PayloadAddress,
    string KeyProxyPayloadAddress,
    string KeyFlagAddress,
    string NativeKeyHandlerAddress,
    string NativeWoodenChestHandlerAddress,
    IReadOnlyList<ArtisansKeyGatedWoodenChestCandidatePatch> Patches,
    IReadOnlyList<string> Assumptions);

public sealed record ArtisansKeyGatedWoodenChestCandidateResult(
    string OutputImagePath,
    ArtisansKeyGatedWoodenChestCandidatePlan Plan,
    bool Verified,
    long OutputLength,
    string Verification);

/// <summary>
/// Research-only Artisans candidate that appends a local green-gem visual proxy,
/// routes only that proxy through Artisans' native Key handler, and gates the
/// existing native T50 wooden chest behind the game's global Key flag. It does
/// not import a locked-chest actor package, alter any other C2 chest or gem, or
/// create a CUE.
/// </summary>
public static class ArtisansKeyGatedWoodenChestCandidateExporter
{
    public const string RecipeId = "artisans.nativeWoodenChest.greenGemKeyProxy.keyGatedT50.v4";

    private const int WadLba = 37;
    private const int RecordStride = 0x58;
    private const int OriginalSourceCount = 174;
    private const int FinalSourceCount = 175;
    private const int KeyDonorTrueIndex = 78;
    private const int KeyVisualDonorTrueIndex = 67;
    private const int AppendedKeyTrueIndex = 174;
    private const int GatedChestTrueIndex = 50;

    private const long ArtisansOverlayEntryOffset = 0x7F2800;
    private const int ArtisansOverlayEntrySize = 0xE000;
    private const long ArtisansDataEntryOffset = 0x800800;
    private const int ArtisansDataEntrySize = 0x383000;
    private const long ArtisansActorModelWadOffset = ArtisansDataEntryOffset + 0x17C000;
    private const int ArtisansActorModelLength = 0x4D800;
    private const long PeaceKeepersDataEntryOffset = 0x1890800;
    private const int PeaceKeepersDataEntrySize = 0x29E800;
    private const long SourceCountWadOffset = 0x9D42A8;
    private const long SourceTableWadOffset = 0x9D42AC;
    private const long AppendedKeyWadOffset = SourceTableWadOffset + (AppendedKeyTrueIndex * RecordStride);
    private const long KeyDonorWadOffset = 0x1A5CD30;
    private const uint KeyDonorSpecialDataSourceRelativeOffset = 0x0000EA94;
    private const long PeaceKeepersSceneWadOffset = 0x1A53000;
    private const long KeyDonorSpecialDataWadOffset = PeaceKeepersSceneWadOffset + KeyDonorSpecialDataSourceRelativeOffset;
    private const long KeyVisualDonorWadOffset = SourceTableWadOffset + (KeyVisualDonorTrueIndex * RecordStride);
    private const long GatedChestWadOffset = SourceTableWadOffset + (GatedChestTrueIndex * RecordStride);
    private const uint KeySpecialDataSourceRelativeOffset = 0x00011EDC;
    private const long KeySpecialDataWadOffset = ArtisansSceneWadOffset + KeySpecialDataSourceRelativeOffset;
    private const int KeySpecialDataLength = 0x04;
    private const long ArtisansSceneWadOffset = 0x9CA000;
    private const long ScenePointerFixupCountWadOffset = 0x9DDED8;
    private const long ScenePointerFixupPreviousWadOffset = 0x9DE1FC;
    private const long ScenePointerFixupAppendWadOffset = 0x9DE200;
    private const int OriginalScenePointerFixupCount = 0xC9;
    private const int FinalScenePointerFixupCount = 0xCA;
    private const uint PreviousScenePointerFieldOffset = 0x0000DE24;
    private const uint AppendedScenePointerFieldOffset = 0x0000DE7C;

    private const uint ExeDestination = 0x80010000;
    private const uint DispatchHookAddress = 0x8007DB98;
    private const uint GemHandlerHookAddress = 0x8007F858;
    private const uint GemHandlerResumeAddress = 0x8007F860;
    // 0x8007314C is the separate 0x400-byte helper region exercised by the
    // runtime-proven Spring Chest v159 route. Do not move this back to
    // 0x80073924: retail code uses that address as PsyQ interrupt-system state,
    // and both the earlier entry-hook experiment and Key v2 failed before the
    // game logos after replacing its initial zero halfword with instructions.
    private const uint PayloadAddress = 0x8007314C;
    private const uint KeyProxyPayloadAddress = PayloadAddress + (22 * 4);
    private const uint KnownPreLogoFailurePayloadAddress = 0x80073924;
    private const uint KeyFlagAddress = 0x80075830;
    private const uint GatedChestRuntimeAddress = 0x8016E518;
    private const uint AppendedKeyRuntimeAddress = 0x80170FB8;
    private const uint NativeKeyHandlerAddress = 0x80082574;
    private const uint NativeWoodenChestHandlerAddress = 0x800829FC;
    private const int CaveGuardLength = 0x400;
    private const int KnownPreLogoFailureCaveGuardLength = 0x100;
    private const uint ExpectedDispatchWord = 0x10621398;
    private const uint ExpectedDispatchDelaySlotWord = 0x286200C3;
    private const uint CandidateDispatchWord = 0x1062D56C;
    private const uint ExpectedGemHandlerWord0 = 0x3C058008;
    private const uint ExpectedGemHandlerWord1 = 0x24A58A58;
    private const uint CandidateGemHandlerWord0 = 0x0801CC69;
    private const uint CandidateGemHandlerWord1 = 0x00000000;

    private const string ExpectedKeyDonorSha256 = "db4fbcd35c285395e2be99ab6205061d9922ea58dccb849675d4c7f514081849";
    private const string ExpectedKeySpecialDataSha256 = "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119";
    private const string ExpectedKeyVisualDonorSha256 = "11fb550961a34d86c5fe023316f29a7822f332f248bd8f8d6fbf6e27a67bbf35";
    private const string ExpectedGatedChestSha256 = "f9590fda039363e5af9466630121efb876ce0a202226706d030f1ab503418988";

    public static ArtisansKeyGatedWoodenChestCandidateResult Export(
        ArtisansKeyGatedWoodenChestCandidateRequest request)
    {
        ValidateRequest(request);

        string sourcePath = Path.GetFullPath(request.SourceImagePath);
        string outputPath = Path.GetFullPath(request.OutputImagePath);
        DiscLayout layout = DiscImage.DetectLayout(sourcePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
        {
            throw new InvalidDataException(
                $"{RecipeId} is guarded for the retail MODE2/2352 layout (2352/24), not {layout.SectorSize}/{layout.UserOffset}.");
        }

        byte[] sourceCountBefore;
        byte[] keyDonor;
        byte[] keyVisualDonor;
        byte[] keySpecialData;
        byte[] appendedKeyBefore;
        byte[] keySpecialDestinationBefore;
        byte[] gatedChest;
        byte[] scenePointerFixupCountBefore;
        byte[] scenePointerFixupPrevious;
        byte[] scenePointerFixupAppendBefore;
        byte[] actorModelBefore;
        byte[] dispatchBefore;
        byte[] dispatchDelaySlot;
        byte[] gemHandlerBefore;
        byte[] caveBefore;
        byte[] knownPreLogoFailureCaveBefore;
        DiscFileRecord executable;

        using (FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            GuardWadEntry(source, layout, 9, ArtisansOverlayEntryOffset, ArtisansOverlayEntrySize, "Artisans overlay");
            GuardWadEntry(source, layout, 10, ArtisansDataEntryOffset, ArtisansDataEntrySize, "Artisans data");
            GuardWadEntry(source, layout, 22, PeaceKeepersDataEntryOffset, PeaceKeepersDataEntrySize, "Peace Keepers data");

            sourceCountBefore = ReadWad(source, layout, SourceCountWadOffset, 4);
            GuardInt32(sourceCountBefore, OriginalSourceCount, "Artisans source count");

            keyDonor = ReadWad(source, layout, KeyDonorWadOffset, RecordStride);
            GuardSha256(keyDonor, ExpectedKeyDonorSha256, "Peace Keepers native Key T78");
            GuardUInt32(keyDonor.AsSpan(0, 4).ToArray(), KeyDonorSpecialDataSourceRelativeOffset, "Peace Keepers T78 special-data pointer");
            keySpecialData = ReadWad(source, layout, KeyDonorSpecialDataWadOffset, KeySpecialDataLength);
            GuardSha256(keySpecialData, ExpectedKeySpecialDataSha256, "Peace Keepers native Key T78 four-byte scene props");

            keyVisualDonor = ReadWad(source, layout, KeyVisualDonorWadOffset, RecordStride);
            GuardSha256(keyVisualDonor, ExpectedKeyVisualDonorSha256, "Artisans native green gem T67 visual envelope");
            GuardUInt16(keyVisualDonor.AsSpan(0x36, 2).ToArray(), 0x0054, "Artisans T67 green-gem class");
            GuardByte(keyVisualDonor, 0x3A, 0x7D, "Artisans T67 loose-gem drop/checkpoint byte");
            GuardByte(keyVisualDonor, 0x50, 0x18, "Artisans T67 visible type byte");
            GuardByte(keyVisualDonor, 0x52, 0x40, "Artisans T67 visible update byte");
            GuardByte(keyDonor, 0x3A, 0x7F, "Peace Keepers T78 Key drop/checkpoint byte");

            appendedKeyBefore = ReadWad(source, layout, AppendedKeyWadOffset, RecordStride);
            GuardBlank(appendedKeyBefore, "Artisans appended Key slot T174");
            keySpecialDestinationBefore = ReadWad(source, layout, KeySpecialDataWadOffset, KeySpecialDataLength);
            GuardBlank(keySpecialDestinationBefore, "Artisans appended Key four-byte scene-props slot 0x11EDC");

            gatedChest = ReadWad(source, layout, GatedChestWadOffset, RecordStride);
            GuardSha256(gatedChest, ExpectedGatedChestSha256, "Artisans native wooden chest T50");
            actorModelBefore = ReadWad(source, layout, ArtisansActorModelWadOffset, ArtisansActorModelLength);

            scenePointerFixupCountBefore = ReadWad(source, layout, ScenePointerFixupCountWadOffset, 4);
            GuardInt32(scenePointerFixupCountBefore, OriginalScenePointerFixupCount, "Artisans scene pointer-fixup count");
            scenePointerFixupPrevious = ReadWad(source, layout, ScenePointerFixupPreviousWadOffset, 4);
            GuardUInt32(scenePointerFixupPrevious, PreviousScenePointerFieldOffset, "Artisans final native scene pointer-fixup");
            scenePointerFixupAppendBefore = ReadWad(source, layout, ScenePointerFixupAppendWadOffset, 4);
            GuardBlank(scenePointerFixupAppendBefore, "Artisans appended scene pointer-fixup slot");

            dispatchBefore = ReadWad(source, layout, DispatchHookAddressToWadOffset(), 4);
            GuardUInt32(dispatchBefore, ExpectedDispatchWord, "Artisans C2 dispatch branch");
            dispatchDelaySlot = ReadWad(source, layout, DispatchHookAddressToWadOffset() + 4, 4);
            GuardUInt32(dispatchDelaySlot, ExpectedDispatchDelaySlotWord, "Artisans C2 dispatch delay slot");

            gemHandlerBefore = ReadWad(source, layout, OverlayAddressToWadOffset(GemHandlerHookAddress), 8);
            GuardUInt32(gemHandlerBefore[..4], ExpectedGemHandlerWord0, "Artisans shared gem-handler prologue word 0");
            GuardUInt32(gemHandlerBefore[4..], ExpectedGemHandlerWord1, "Artisans shared gem-handler prologue word 1");

            executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
            ExecutablePatchSafety.GuardPatchRange(PayloadAddress, CaveGuardLength, "Artisans Key-gate payload");
            long caveFileOffset = ExeFileOffset(PayloadAddress);
            if (caveFileOffset < 0x800 || caveFileOffset + CaveGuardLength > executable.Size)
                throw new InvalidDataException("The guarded Artisans Key-gate code cave is outside the executable body.");
            caveBefore = DiscImage.ReadFileBytes(source, layout, executable.Lba, caveFileOffset, CaveGuardLength);
            GuardBlank(caveBefore, $"runtime-proven helper cave 0x{PayloadAddress:X8}");

            long knownFailedCaveFileOffset = ExeFileOffset(KnownPreLogoFailurePayloadAddress);
            knownPreLogoFailureCaveBefore = DiscImage.ReadFileBytes(
                source,
                layout,
                executable.Lba,
                knownFailedCaveFileOffset,
                KnownPreLogoFailureCaveGuardLength);
            GuardBlank(
                knownPreLogoFailureCaveBefore,
                $"known pre-logo failure region 0x{KnownPreLogoFailurePayloadAddress:X8}");
        }

        byte[] appendedKeyAfter = BuildKeyProxyRecord(
            keyDonor,
            keyVisualDonor,
            request.KeyRawX,
            request.KeyRawY,
            request.KeyRawZ);
        byte[] sourceCountAfter = LittleEndian(FinalSourceCount);
        byte[] scenePointerFixupCountAfter = LittleEndian(FinalScenePointerFixupCount);
        byte[] scenePointerFixupAppendAfter = LittleEndian(AppendedScenePointerFieldOffset);
        byte[] dispatchAfter = LittleEndian(CandidateDispatchWord);
        byte[] gemHandlerAfter = LittleEndian(CandidateGemHandlerWord0)
            .Concat(LittleEndian(CandidateGemHandlerWord1))
            .ToArray();
        byte[] payload = BuildPayload();

        List<ArtisansKeyGatedWoodenChestCandidatePatch> patches =
        [
            BuildWadPatch(layout, "Reserve exact Peace Keepers T78 four-byte Key props in the Artisans scene", KeySpecialDataWadOffset, keySpecialDestinationBefore, keySpecialData),
            BuildWadPatch(layout, "Append Artisans T67 green-gem visual / Peace Keepers T78 Key semantic proxy as T174", AppendedKeyWadOffset, appendedKeyBefore, appendedKeyAfter),
            BuildWadPatch(layout, "Register T174 first-word scene pointer field", ScenePointerFixupAppendWadOffset, scenePointerFixupAppendBefore, scenePointerFixupAppendAfter),
            BuildWadPatch(layout, "Increase Artisans scene pointer-fixup count", ScenePointerFixupCountWadOffset, scenePointerFixupCountBefore, scenePointerFixupCountAfter),
            BuildExePatch(layout, executable, "Install T50 Key-gate payload", PayloadAddress, caveBefore[..payload.Length], payload),
            BuildWadPatch(layout, "Redirect only actor 0x00C2 dispatch through guarded shim", DispatchHookAddressToWadOffset(), dispatchBefore, dispatchAfter),
            BuildWadPatch(layout, "Route only T174 from the shared gem handler into Artisans' native Key handler", OverlayAddressToWadOffset(GemHandlerHookAddress), gemHandlerBefore, gemHandlerAfter),
            BuildWadPatch(layout, "Increase Artisans source count", SourceCountWadOffset, sourceCountBefore, sourceCountAfter)
        ];

        ArtisansKeyGatedWoodenChestCandidatePlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            RecipeId: RecipeId,
            SourceImagePath: sourcePath,
            OutputImagePath: outputPath,
            SectorSize: layout.SectorSize,
            UserOffset: layout.UserOffset,
            WadLba: WadLba,
            ExecutableName: executable.Name,
            ExecutableLba: executable.Lba,
            KeyDonorLevel: "Peace Keepers",
            KeyDonorTrueIndex: KeyDonorTrueIndex,
            KeyVisualDonorLevel: "Artisans",
            KeyVisualDonorTrueIndex: KeyVisualDonorTrueIndex,
            KeyVisualClass: "0x0054 (resident green gem)",
            AppendedKeyTrueIndex: AppendedKeyTrueIndex,
            GatedChestTrueIndex: GatedChestTrueIndex,
            OriginalSourceCount: OriginalSourceCount,
            FinalSourceCount: FinalSourceCount,
            KeyRawX: request.KeyRawX,
            KeyRawY: request.KeyRawY,
            KeyRawZ: request.KeyRawZ,
            KeySpecialDataSourceRelativeOffset: $"0x{KeySpecialDataSourceRelativeOffset:X}",
            ScenePointerFixupAppended: true,
            SceneWadOffset: $"0x{ArtisansSceneWadOffset:X}",
            ScenePointerFixupCountWadOffset: $"0x{ScenePointerFixupCountWadOffset:X}",
            OriginalScenePointerFixupCount: OriginalScenePointerFixupCount,
            FinalScenePointerFixupCount: FinalScenePointerFixupCount,
            AppendedScenePointerFieldOffset: $"0x{AppendedScenePointerFieldOffset:X}",
            PointerPolicy: "T174 uses source-relative scene-props pointer 0x11EDC, whose true WAD target is Artisans scene 0x9CA000 + 0x11EDC = 0x9DBEDC. The native Peace Keepers T78 pointer is likewise scene-relative (0x1A53000 + 0xEA94 = 0x1A61A94) and addresses exactly four zero-initialized Key props bytes. Artisans' relocation list natively ends at T173, so this candidate appends T174 field offset 0xDE7C and raises the scene fixup count from 0xC9 to 0xCA.",
            DispatchHookAddress: $"0x{DispatchHookAddress:X8}",
            DispatchDelaySlotWord: $"0x{ExpectedDispatchDelaySlotWord:X8} (preserved in place and still executes when the branch is taken)",
            GemHandlerHookAddress: $"0x{GemHandlerHookAddress:X8}",
            GemHandlerResumeAddress: $"0x{GemHandlerResumeAddress:X8}",
            PayloadAddress: $"0x{PayloadAddress:X8}",
            KeyProxyPayloadAddress: $"0x{KeyProxyPayloadAddress:X8}",
            KeyFlagAddress: $"0x{KeyFlagAddress:X8}",
            NativeKeyHandlerAddress: $"0x{NativeKeyHandlerAddress:X8}",
            NativeWoodenChestHandlerAddress: $"0x{NativeWoodenChestHandlerAddress:X8}",
            Patches: patches,
            Assumptions:
            [
                "Exact USA retail Artisans overlay/data layout and retail preimages are required.",
                "T174 is a deliberate hybrid: Artisans T67 supplies resident green-gem class 0x0054 plus visible type/update bytes 0x18/0x40; Peace Keepers T78 supplies the native Key's 0x7F drop/checkpoint byte, four-byte props contract, and native handler semantics.",
                "The prior v3 candidate copied 24 bytes from data-entry-relative WAD 0x8126DC. That address was structurally wrong: Moby props pointers are scene-relative. V4 instead guards the exact native T78 scene target at 0x1A61A94 and the exact Artisans T174 scene target at 0x9DBEDC.",
                "Artisans scene base 0x9CA000 and its exact C9-entry relocation-list tail are required; T174's nonzero first word is explicitly registered as scene field 0xDE7C.",
                "Only runtime T174 is diverted at the shared gem-handler prologue. Every other Artisans gem replays the two overwritten retail instructions and resumes at 0x8007F860.",
                "Runtime T174 enters Artisans' already-resident native Key handler at 0x80082574, which owns pickup distance, Key sound/particles, g_KeyFlag=1, and one-shot retirement.",
                "T50 remains byte-identical in the source table; the shim only filters its runtime damage bits until g_KeyFlag is set.",
                "All other actor 0x00C2 objects jump directly to the original 0x800829FC native handler.",
                "On a real T50 hit while the Key flag is set, the shim clears g_KeyFlag and then enters the unmodified native wooden-chest handler.",
                "The shim uses the separately runtime-proven 0x8007314C helper region; PsyQ interrupt-system state at the known pre-logo failure address 0x80073924 remains byte-identical to retail.",
                "The collectible intentionally looks like a green gem rather than a Key. This is a behavior-first resident-visual proxy, not a Key or locked-chest actor/model import.",
                "The exporter writes one BIN only and never creates or edits a CUE."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        try
        {
            File.Copy(sourcePath, outputPath, true);
            using (FileStream output = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                // Install dependencies first and publish the count/hook last. The output is
                // deleted on any exception, but this order also keeps partial writes inert.
                DiscImage.WriteFileBytes(output, layout, WadLba, KeySpecialDataWadOffset, keySpecialData);
                DiscImage.WriteFileBytes(output, layout, WadLba, AppendedKeyWadOffset, appendedKeyAfter);
                DiscImage.WriteFileBytes(output, layout, WadLba, ScenePointerFixupAppendWadOffset, scenePointerFixupAppendAfter);
                DiscImage.WriteFileBytes(output, layout, WadLba, ScenePointerFixupCountWadOffset, scenePointerFixupCountAfter);
                DiscImage.WriteFileBytes(output, layout, executable.Lba, ExeFileOffset(PayloadAddress), payload);
                DiscImage.WriteFileBytes(output, layout, WadLba, DispatchHookAddressToWadOffset(), dispatchAfter);
                DiscImage.WriteFileBytes(output, layout, WadLba, OverlayAddressToWadOffset(GemHandlerHookAddress), gemHandlerAfter);
                DiscImage.WriteFileBytes(output, layout, WadLba, SourceCountWadOffset, sourceCountAfter);
                output.Flush(flushToDisk: true);

                VerifyReadback(
                    output,
                    layout,
                    executable,
                    sourcePath,
                    keySpecialData,
                    appendedKeyAfter,
                    scenePointerFixupAppendAfter,
                    scenePointerFixupCountAfter,
                    payload,
                    dispatchAfter,
                    gemHandlerAfter,
                    sourceCountAfter,
                    dispatchDelaySlot,
                    gatedChest,
                    actorModelBefore,
                    knownPreLogoFailureCaveBefore);
            }

            long outputLength = new FileInfo(outputPath).Length;
            return new ArtisansKeyGatedWoodenChestCandidateResult(
                OutputImagePath: outputPath,
                Plan: plan,
                Verified: true,
                OutputLength: outputLength,
                Verification: "All eight guarded ranges passed byte-for-byte readback; the prior scene-fixup tail, C2 dispatch delay slot, native T50 source record, complete actor/model subfile, unused safe-cave tail, and known-failed 0x80073924 region remained exact; every non-T174 gem replays the two displaced retail prologue instructions; image length remained unchanged.");
        }
        catch
        {
            DeleteFailedOutput(outputPath);
            throw;
        }
    }

    private static byte[] BuildKeyProxyRecord(
        byte[] keyDonor,
        byte[] visualDonor,
        int rawX,
        int rawY,
        int rawZ)
    {
        byte[] record = visualDonor.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x00, 4), KeySpecialDataSourceRelativeOffset);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x0C, 4), rawX);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x10, 4), rawY);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x14, 4), rawZ);
        record[0x3A] = keyDonor[0x3A]; // Preserve native Key no-drop/checkpoint semantics (0x7F).
        return record;
    }

    private static byte[] BuildPayload()
    {
        // The dispatch branch's original slti instruction remains at 0x8007DB9C
        // and is still its delay slot. The lw at word 5 uses the independent
        // addiu at word 6 as its load-delay instruction; the branch at word 7
        // loads T50 +0x18 in its delay slot for both successor paths.
        uint[] words =
        [
            0x3C028016, // lui   v0, 0x8016
            0x3442E518, // ori   v0, v0, 0xE518       (Artisans T50 runtime row)
            0x1662000B, // bne   s3, v0, native       (all other C2 chests)
            0x00000000, // nop
            0x3C028007, // lui   v0, 0x8007
            0x8C425830, // lw    v0, 0x5830(v0)       (g_KeyFlag)
            0x24030001, // addiu v1, zero, 1          (independent load-delay slot)
            0x14430008, // bne   v0, v1, no_key
            0x8E630018, // lw    v1, 0x18(s3)         (branch delay slot)
            0x3C02000B, // lui   v0, 0x000B
            0x00621824, // and   v1, v1, v0
            0x10600002, // beq   v1, zero, native
            0x3C028007, // lui   v0, 0x8007            (branch delay slot)
            0xAC405830, // sw    zero, 0x5830(v0)      (consume Key on real hit)
            0x08020A7F, // native: j 0x800829FC
            0x00000000, // nop
            0x3C02000B, // no_key: lui v0, 0x000B
            0x00401027, // nor   v0, v0, zero          (~0x000B0000)
            0x00621824, // and   v1, v1, v0
            0xAE630018, // sw    v1, 0x18(s3)          (filter hit bits only)
            0x08020A7F, // j     0x800829FC
            0x00000000, // nop

            // 0x800731A4: route only the appended T174 green-gem proxy into
            // Artisans' already-resident native Key handler. All other gems
            // replay the two instructions displaced at 0x8007F858 and resume.
            0x3C028017, // lui   v0, 0x8017
            0x34420FB8, // ori   v0, v0, 0x0FB8       (Artisans T174 runtime row)
            0x16620003, // bne   s3, v0, normal_gem
            0x00000000, // nop
            0x0802095D, // j     0x80082574            (native Key handler)
            0x00000000, // nop
            0x3C058008, // normal_gem: lui a1, 0x8008  (displaced retail word 0)
            0x24A58A58, // addiu a1, a1, 0x8A58       (displaced retail word 1)
            0x0801FE18, // j     0x8007F860            (resume shared gem handler)
            0x00000000  // nop
        ];

        byte[] payload = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(i * 4, 4), words[i]);
        return payload;
    }

    private static void VerifyReadback(
        FileStream output,
        DiscLayout layout,
        DiscFileRecord executable,
        string sourcePath,
        byte[] keySpecialData,
        byte[] appendedKey,
        byte[] scenePointerFixupAppend,
        byte[] scenePointerFixupCount,
        byte[] payload,
        byte[] dispatch,
        byte[] gemHandler,
        byte[] sourceCount,
        byte[] expectedDelaySlot,
        byte[] expectedGatedChest,
        byte[] expectedActorModel,
        byte[] expectedKnownPreLogoFailureCave)
    {
        GuardEqual(ReadWad(output, layout, KeySpecialDataWadOffset, keySpecialData.Length), keySpecialData, "Key scene-props readback");
        GuardEqual(ReadWad(output, layout, AppendedKeyWadOffset, appendedKey.Length), appendedKey, "T174 green-gem Key proxy readback");
        GuardEqual(ReadWad(output, layout, ScenePointerFixupAppendWadOffset, scenePointerFixupAppend.Length), scenePointerFixupAppend, "T174 scene pointer-fixup readback");
        GuardEqual(ReadWad(output, layout, ScenePointerFixupCountWadOffset, scenePointerFixupCount.Length), scenePointerFixupCount, "scene pointer-fixup count readback");
        GuardEqual(ReadWad(output, layout, ScenePointerFixupPreviousWadOffset, 4), LittleEndian(PreviousScenePointerFieldOffset), "preserved prior scene pointer-fixup tail");
        GuardEqual(
            DiscImage.ReadFileBytes(output, layout, executable.Lba, ExeFileOffset(PayloadAddress), payload.Length),
            payload,
            "Key-gate payload readback");
        GuardEqual(ReadWad(output, layout, DispatchHookAddressToWadOffset(), dispatch.Length), dispatch, "C2 dispatch readback");
        GuardEqual(ReadWad(output, layout, OverlayAddressToWadOffset(GemHandlerHookAddress), gemHandler.Length), gemHandler, "T174 gem-to-Key handler route readback");
        GuardEqual(ReadWad(output, layout, SourceCountWadOffset, sourceCount.Length), sourceCount, "source-count readback");
        GuardEqual(ReadWad(output, layout, DispatchHookAddressToWadOffset() + 4, 4), expectedDelaySlot, "preserved dispatch delay slot");
        GuardEqual(ReadWad(output, layout, GatedChestWadOffset, RecordStride), expectedGatedChest, "preserved native T50 source row");
        GuardEqual(ReadWad(output, layout, ArtisansActorModelWadOffset, expectedActorModel.Length), expectedActorModel, "preserved Artisans actor/model subfile");
        GuardBlank(
            DiscImage.ReadFileBytes(
                output,
                layout,
                executable.Lba,
                ExeFileOffset(PayloadAddress) + payload.Length,
                CaveGuardLength - payload.Length),
            "unused Key-gate cave tail");
        GuardEqual(
            DiscImage.ReadFileBytes(
                output,
                layout,
                executable.Lba,
                ExeFileOffset(KnownPreLogoFailurePayloadAddress),
                expectedKnownPreLogoFailureCave.Length),
            expectedKnownPreLogoFailureCave,
            $"preserved known pre-logo failure region 0x{KnownPreLogoFailurePayloadAddress:X8}");

        long sourceLength = new FileInfo(sourcePath).Length;
        if (output.Length != sourceLength)
            throw new InvalidDataException($"Output image length changed from {sourceLength} to {output.Length} bytes.");
    }

    private static ArtisansKeyGatedWoodenChestCandidatePatch BuildWadPatch(
        DiscLayout layout,
        string label,
        long wadOffset,
        byte[] before,
        byte[] after) =>
        new(
            Label: label,
            Storage: "WAD.WAD",
            Address: $"wad:0x{wadOffset:X}",
            FileOffset: wadOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, WadLba, wadOffset),
            ByteLength: after.Length,
            BeforeSha256: Sha256(before),
            AfterSha256: Sha256(after),
            BeforeHex: Convert.ToHexString(before).ToLowerInvariant(),
            AfterHex: Convert.ToHexString(after).ToLowerInvariant());

    private static ArtisansKeyGatedWoodenChestCandidatePatch BuildExePatch(
        DiscLayout layout,
        DiscFileRecord executable,
        string label,
        uint runtimeAddress,
        byte[] before,
        byte[] after)
    {
        long fileOffset = ExeFileOffset(runtimeAddress);
        return new ArtisansKeyGatedWoodenChestCandidatePatch(
            Label: label,
            Storage: executable.Name,
            Address: $"exe:0x{runtimeAddress:X8}",
            FileOffset: fileOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, executable.Lba, fileOffset),
            ByteLength: after.Length,
            BeforeSha256: Sha256(before),
            AfterSha256: Sha256(after),
            BeforeHex: Convert.ToHexString(before).ToLowerInvariant(),
            AfterHex: Convert.ToHexString(after).ToLowerInvariant());
    }

    private static void GuardWadEntry(
        FileStream source,
        DiscLayout layout,
        int entryIndex,
        long expectedOffset,
        int expectedSize,
        string label)
    {
        byte[] row = ReadWad(source, layout, entryIndex * 8L, 8);
        uint actualOffset = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(0, 4));
        uint actualSize = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4, 4));
        if (actualOffset != expectedOffset || actualSize != expectedSize)
        {
            throw new InvalidDataException(
                $"{label} entry {entryIndex} expected offset/size 0x{expectedOffset:X}/0x{expectedSize:X}, " +
                $"got 0x{actualOffset:X}/0x{actualSize:X}.");
        }
    }

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long wadOffset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, length);

    private static long DispatchHookAddressToWadOffset() =>
        OverlayAddressToWadOffset(DispatchHookAddress);

    private static long OverlayAddressToWadOffset(uint runtimeAddress) =>
        ArtisansOverlayEntryOffset + (runtimeAddress - 0x8007AA38);

    private static long ExeFileOffset(uint runtimeAddress) =>
        0x800L + runtimeAddress - ExeDestination;

    private static byte[] LittleEndian(int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static void GuardInt32(byte[] bytes, int expected, string label)
    {
        int actual = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} expected {expected}, got {actual}.");
    }

    private static void GuardUInt32(byte[] bytes, uint expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X8}, got 0x{actual:X8}.");
    }

    private static void GuardUInt16(byte[] bytes, ushort expected, string label)
    {
        ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X4}, got 0x{actual:X4}.");
    }

    private static void GuardByte(byte[] bytes, int offset, byte expected, string label)
    {
        byte actual = bytes[offset];
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X2}, got 0x{actual:X2}.");
    }

    private static void GuardSha256(byte[] bytes, string expected, string label)
    {
        string actual = Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 expected {expected}, got {actual}.");
    }

    private static void GuardBlank(byte[] bytes, string label)
    {
        if (bytes.Any(value => value != 0))
            throw new InvalidDataException($"{label} is not blank; refusing to overwrite it.");
    }

    private static void GuardEqual(byte[] actual, byte[] expected, string label)
    {
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException($"{label} did not match the checked write payload.");
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLPS", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequest(ArtisansKeyGatedWoodenChestCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourceImagePath))
            throw new ArgumentException("A source disc image path is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.OutputImagePath))
            throw new ArgumentException("An output disc image path is required.", nameof(request));
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);

        string source = Path.GetFullPath(request.SourceImagePath);
        string output = Path.GetFullPath(request.OutputImagePath);
        if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The research candidate output must not overwrite its source image.", nameof(request));
    }

    private static void DeleteFailedOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
