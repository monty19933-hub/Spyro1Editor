using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

// Neutral decoded facts only. The original-source family issuer and the
// historical foundation use this same checked donor grammar/interpreter.
internal sealed record NativeMobyFamilyFaceStream(
    string Tier,
    int PackageRelativeOffset,
    int BodyByteLength,
    int TotalByteLength,
    int RecordCount,
    int UntexturedTriangleCount,
    int TexturedRecordCount,
    string Sha256,
    bool EndsExactlyUnderRetailRendererGrammar);

internal sealed record NativeMobyFamilyDispatchTrace(
    string LevelKey,
    string LevelName,
    int OverlayWadEntry,
    long OverlayWadOffset,
    int OverlayByteLength,
    string OverlaySha256,
    uint OverlayLoadAddress,
    ushort ActorId,
    uint ClassLoadAddress,
    uint ClassLoadWord,
    uint DispatchStartAddress,
    uint DecisionWindowAddress,
    int DecisionWindowByteLength,
    string DecisionWindowSha256,
    uint DefaultLoopExitAddress,
    string DefaultLoopExitSha256,
    IReadOnlyList<uint> ExecutedInstructionAddresses,
    IReadOnlyList<uint> ExecutedInstructionWords,
    string ExecutedTraceSha256,
    ushort? AdjacentDedicatedClassId,
    uint? AdjacentDedicatedHandlerAddress,
    int CallInstructionCount,
    int PropertiesReadCount,
    int RuntimeActorWriteCount,
    bool DedicatedHandlerPresent,
    bool ReachesDefaultLoopExit,
    bool ClassSpecificControllerPresent);

// Neutral executed facts, never an original-source or destination certificate.
internal sealed record NativeMobyFamilyPassiveDispatchTrace(
    IReadOnlyList<uint> ExecutedInstructionAddresses,
    IReadOnlyList<uint> ExecutedInstructionWords,
    string ExecutedTraceSha256,
    int CallInstructionCount,
    int PropertiesReadCount,
    int RuntimeActorWriteCount);

internal sealed record NativeGrassDonorDecoded(byte[] Row,byte[] Properties,byte[] Package,
    IReadOnlyList<int> SharingTrueIndices,int NextPropertiesPointer,byte[] Fixups,
    int PointerFieldOccurrenceCount,IReadOnlyList<int> InternalPropertiesFixups,
    int AnimationCount,int ModelDataRelative,int PackageEntryRelativeReferenceCount,
    NativeMobyFamilyFaceStream NormalFaces,NativeMobyFamilyFaceStream FarLodFaces);

internal static class NativeGrassFamilySourceDecoder
{
    private const int RecordStride=0x58;
    private const uint OverlayLoadAddress=0x8007AA38;
    internal const int ArtisansOverlayEntry = 9;
    internal const long ArtisansOverlayWadOffset = 0x7F2800;
    internal const int ArtisansOverlayByteLength = 0xE000;
    internal const string ArtisansOverlaySha256 =
        "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55";
    internal const uint ArtisansClassLoadAddress = 0x8007DA74;
    internal const uint ArtisansClassLoadWord = 0x86630036;
    internal const uint ArtisansDispatchStartAddress = 0x8007DA9C;
    internal const uint ArtisansDecisionWindowAddress = 0x8007DBDC;
    internal const int ArtisansDecisionWindowByteLength = 0x128;
    internal const string ArtisansDecisionWindowSha256 =
        "ba55f492574c72c1eed3c21a4cf20c57dc49fe07136f9156b422ca4825455c40";
    internal const uint ArtisansDefaultLoopExitAddress = 0x80085780;
    internal const string ArtisansDefaultLoopExitSha256 =
        "427d89f344d0cc0fde0faa67e9ab199d67cf68510d3433478cdf646c50a4c61b";
    internal const string ArtisansTraceSha256 =
        "ffdacdf97f86f1aed4505e0869ee2a9bfbdaad92025704fd33d1d4bfb9ba6d88";
    internal const int ArtisansDataEntry = 10;
    internal const long ArtisansDataWadOffset = 0x800800;
    internal const int ArtisansDataByteLength = 0x383000;
    internal const string ArtisansDataSha256 =
        "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f";
    internal const int ArtisansActorSubfileOffset = 0x17C000;
    internal const int ArtisansActorSubfileByteLength = 0x4D800;
    internal const int ArtisansSceneOffset = 0x1C9800;
    internal const int ArtisansSceneByteLength = 0x14800;
    internal const int ArtisansObjectCount = 174;
    internal const int ArtisansObjectTableOffset = 0xA2AC;
    internal const int GrassTrueIndex = 121;
    internal const int GrassRowSceneOffset = 0xCC44;
    internal const string GrassRowSha256 =
        "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190";
    internal const uint GrassPropertiesOffset = 0x11ACC;
    internal const int GrassPropertiesByteLength = 8;
    internal const string GrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    internal const int ArtisansFixupCountOffset = 0x13ED8;
    internal const int ArtisansFixupCount = 0xC9;
    internal const int ArtisansFixupActiveByteLength = 4 + (ArtisansFixupCount * 4);
    internal const string ArtisansFixupActiveSha256 =
        "7d677b0b8d6ad795a6319bd31e781c4e5f52a76be67ed81ed1b1c6fbdb4775fb";
    internal const ushort GrassActorId = 0x01F5;
    internal const int GrassActorRootIndex = 22;
    internal const int GrassActorRoot = 0x1C1520;
    internal const int GrassActorNextRoot = 0x1C1694;
    internal const string GrassActorPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    internal const string GrassNormalFacesSha256 =
        "f06f9c3ff1ba9bebb7379507144b595b1c326681c0713063892ae15e16162b3b";
    internal const string GrassFarLodFacesSha256 =
        "b30459d48479636f8f1c385e3a5e86df21a24637806c5e5699553ec789e75c94";


    internal static NativeGrassDonorDecoded DecodeDonor(byte[] artisansData)
    {
        ArgumentNullException.ThrowIfNull(artisansData);
        if(artisansData.Length!=ArtisansDataByteLength)
            throw new InvalidDataException("Grass donor requires its complete original Artisans data allocation.");
        RequireHash(Hash(artisansData),ArtisansDataSha256,"Artisans donor data entry preimage");
        RequireNestedSubfile(artisansData,2,ArtisansActorSubfileOffset,ArtisansActorSubfileByteLength);
        RequireNestedSubfile(artisansData,3,ArtisansSceneOffset,ArtisansSceneByteLength);
        RequireUInt32(artisansData.AsSpan(ArtisansSceneOffset+ArtisansObjectTableOffset-4,4),
            ArtisansObjectCount,"Artisans source-record count");
        byte[] grassRow = artisansData
            .AsSpan(ArtisansSceneOffset + GrassRowSceneOffset, RecordStride)
            .ToArray();
        RequireHash(Hash(grassRow), GrassRowSha256, "Artisans Grass T121 row");
        if (GrassRowSceneOffset != ArtisansObjectTableOffset + (GrassTrueIndex * RecordStride))
            throw new InvalidDataException("The pinned Artisans T121 table arithmetic changed.");
        RequireUInt32(grassRow.AsSpan(0, 4), GrassPropertiesOffset, "Artisans Grass properties pointer");
        RequireUInt32(grassRow.AsSpan(8, 4), 0, "Artisans Grass legacy special-data pointer");
        RequireUInt16(grassRow.AsSpan(0x36, 2), GrassActorId, "Artisans Grass actor id");
        if (grassRow[0x4F] != 0 || grassRow[0x50] != 0x20 || grassRow[0x51] != 0x00 ||
            grassRow[0x52] != 0x10 || grassRow[0x53] != 0xFF || grassRow[0x43] != 0xFF ||
            grassRow[0x4A] != 0xFF || grassRow[0x4B] != 0x0C)
        {
            throw new InvalidDataException("The pinned Artisans Grass scenery fingerprint changed.");
        }

        byte[] properties = artisansData
            .AsSpan(
                ArtisansSceneOffset + checked((int)GrassPropertiesOffset),
                GrassPropertiesByteLength)
            .ToArray();
        RequireHash(Hash(properties), GrassPropertiesSha256, "Artisans Grass properties");
        if (!properties.AsSpan().SequenceEqual(Convert.FromHexString("040000008A000000")))
            throw new InvalidDataException("Artisans Grass properties are not the pinned two-word block.");

        byte[] artisansRows = artisansData
            .AsSpan(
                ArtisansSceneOffset + ArtisansObjectTableOffset,
                ArtisansObjectCount * RecordStride)
            .ToArray();
        List<int> sharingTrueIndices = [];
        SortedSet<int> positivePropertiesPointers = [];
        for (int index = 0; index < ArtisansObjectCount; index++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(
                artisansRows.AsSpan(index * RecordStride, 4));
            if (pointer == GrassPropertiesOffset)
                sharingTrueIndices.Add(index);
            if (pointer > 0)
                positivePropertiesPointers.Add(checked((int)pointer));
        }
        int[] expectedSharingIndices =
            [118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133];
        if (!sharingTrueIndices.SequenceEqual(expectedSharingIndices))
            throw new InvalidDataException("The exact Artisans shared Grass-properties cluster changed.");
        int nextPropertiesPointer = positivePropertiesPointers.First(pointer => pointer > GrassPropertiesOffset);
        if (nextPropertiesPointer != GrassPropertiesOffset + GrassPropertiesByteLength)
            throw new InvalidDataException("The exact Artisans Grass properties extent changed.");

        byte[] artisansFixups = artisansData
            .AsSpan(
                ArtisansSceneOffset + ArtisansFixupCountOffset,
                ArtisansFixupActiveByteLength)
            .ToArray();
        RequireHash(Hash(artisansFixups), ArtisansFixupActiveSha256, "Artisans pointer-fixup component");
        RequireUInt32(artisansFixups.AsSpan(0, 4), ArtisansFixupCount, "Artisans pointer-fixup count");
        int[] sourceFixupFields = ReadFixupFields(artisansFixups, ArtisansFixupCount);
        int pointerFieldOccurrenceCount = sourceFixupFields.Count(offset => offset == GrassRowSceneOffset);
        if (pointerFieldOccurrenceCount != 1)
            throw new InvalidDataException("Artisans T121's properties field is not registered exactly once in the scene fixup list.");
        int[] internalPropertiesFixups = sourceFixupFields
            .Where(offset => offset >= GrassPropertiesOffset && offset < GrassPropertiesOffset + GrassPropertiesByteLength)
            .ToArray();
        if (internalPropertiesFixups.Length != 0)
            throw new InvalidDataException("The pinned Grass properties unexpectedly gained an internal scene pointer.");

        byte[] artisansHeader = artisansData.AsSpan(0, 0x200).ToArray();
        ActorRootTable artisansRoots = ReadActorRootTable(artisansHeader);
        if (artisansRoots.PopulatedCount != 35 ||
            artisansRoots.Roots[GrassActorRootIndex] != GrassActorRoot ||
            artisansRoots.ActorIds[GrassActorRootIndex] != GrassActorId ||
            artisansRoots.Roots[GrassActorRootIndex + 1] != GrassActorNextRoot)
        {
            throw new InvalidDataException("The Artisans Grass actor-root envelope changed.");
        }
        byte[] grassPackage = artisansData
            .AsSpan(GrassActorRoot, GrassActorNextRoot - GrassActorRoot)
            .ToArray();
        RequireHash(Hash(grassPackage), GrassActorPackageSha256, "Artisans Grass actor package");
        int animationCount = ReadInt32(grassPackage, 0);
        int modelDataRelative = ReadInt32(grassPackage, 0x34);
        if (animationCount != 1 || modelDataRelative != 0x68 || ReadInt32(grassPackage, 0x38) != 0x3C)
            throw new InvalidDataException("The Artisans Grass animation header changed.");
        int animationOffset = ReadInt32(grassPackage, 0x38);
        int normalOffset = checked(modelDataRelative + ReadInt32(grassPackage, animationOffset + 0x14));
        int farLodOffset = checked(modelDataRelative + ReadInt32(grassPackage, animationOffset + 0x1C));
        NativeMobyFamilyFaceStream normalFaces = ParseFaceStream(grassPackage, normalOffset, "normal");
        NativeMobyFamilyFaceStream farLodFaces = ParseFaceStream(grassPackage, farLodOffset, "far-LOD");
        RequireHash(normalFaces.Sha256, GrassNormalFacesSha256, "Artisans Grass normal face table");
        RequireHash(farLodFaces.Sha256, GrassFarLodFacesSha256, "Artisans Grass far-LOD face table");
        if (normalFaces.RecordCount != 9 || normalFaces.UntexturedTriangleCount != 9 || normalFaces.TexturedRecordCount != 0 ||
            farLodFaces.RecordCount != 9 || farLodFaces.UntexturedTriangleCount != 9 || farLodFaces.TexturedRecordCount != 0)
        {
            throw new InvalidDataException("The Artisans Grass actor package is no longer entirely untextured at both LOD tiers.");
        }
        int packageEntryRelativeReferenceCount = CountAlignedValuesInRange(
            grassPackage,
            ArtisansActorSubfileOffset,
            ArtisansActorSubfileOffset + ArtisansActorSubfileByteLength);
        if (packageEntryRelativeReferenceCount != 0)
            throw new InvalidDataException("The Artisans Grass package gained an entry-relative actor-subfile pointer requiring rebasing.");


        return new(grassRow,properties,grassPackage,sharingTrueIndices.AsReadOnly(),nextPropertiesPointer,
            artisansFixups,pointerFieldOccurrenceCount,Array.AsReadOnly(internalPropertiesFixups),
            animationCount,modelDataRelative,packageEntryRelativeReferenceCount,normalFaces,farLodFaces);
    }

    internal static NativeMobyFamilyDispatchTrace DecodeDonorDispatch(byte[] artisansOverlay)
    {
        ArgumentNullException.ThrowIfNull(artisansOverlay);
        if(artisansOverlay.Length!=ArtisansOverlayByteLength)
            throw new InvalidDataException("Grass donor requires its complete original Artisans overlay allocation.");
        RequireHash(Hash(artisansOverlay),ArtisansOverlaySha256,"Artisans overlay preimage");
        return InspectDefaultDispatchTrace(
            "artisans",
            "Artisans",
            ArtisansOverlayEntry,
            ArtisansOverlayWadOffset,
            artisansOverlay,
            ArtisansOverlaySha256,
            classRegister: 3,
            ArtisansClassLoadAddress,
            ArtisansClassLoadWord,
            ArtisansDispatchStartAddress,
            ArtisansDecisionWindowAddress,
            ArtisansDecisionWindowByteLength,
            ArtisansDecisionWindowSha256,
            ArtisansDefaultLoopExitAddress,
            ArtisansDefaultLoopExitSha256,
            ArtisansTraceSha256,
            adjacentDedicatedClassId: null,
            adjacentDedicatedHandlerAddress: null);
    }

    internal static NativeMobyFamilyDispatchTrace InspectDefaultDispatchTrace(
        string levelKey,
        string levelName,
        int overlayWadEntry,
        long overlayWadOffset,
        byte[] overlay,
        string overlaySha256,
        int classRegister,
        uint classLoadAddress,
        uint classLoadWord,
        uint dispatchStartAddress,
        uint decisionWindowAddress,
        int decisionWindowByteLength,
        string decisionWindowSha256,
        uint defaultLoopExitAddress,
        string defaultLoopExitSha256,
        string expectedTraceSha256,
        ushort? adjacentDedicatedClassId,
        uint? adjacentDedicatedHandlerAddress)
    {
        RequireOverlayWord(overlay, classLoadAddress, classLoadWord, $"{levelName} class load");
        byte[] decisionWindow = ReadOverlayBytes(
            overlay,
            decisionWindowAddress,
            decisionWindowByteLength);
        RequireHash(Hash(decisionWindow), decisionWindowSha256, $"{levelName} high-class decision window");
        byte[] defaultLoopExit = ReadOverlayBytes(overlay, defaultLoopExitAddress, 0x18);
        RequireHash(Hash(defaultLoopExit), defaultLoopExitSha256, $"{levelName} default loop exit");
        if (CountDedicatedClassComparisons(overlay, classRegister, GrassActorId, expectedHandler: null) != 0)
            throw new InvalidDataException($"{levelName} unexpectedly gained a class-0x{GrassActorId:X4} handler comparison.");

        if (adjacentDedicatedClassId.HasValue != adjacentDedicatedHandlerAddress.HasValue)
            throw new InvalidDataException($"{levelName} adjacent-handler evidence is incomplete.");
        if (adjacentDedicatedClassId is ushort adjacentClass &&
            adjacentDedicatedHandlerAddress is uint adjacentHandler &&
            CountDedicatedClassComparisons(overlay, classRegister, adjacentClass, adjacentHandler) != 1)
        {
            throw new InvalidDataException(
                $"{levelName} does not contain exactly one class-0x{adjacentClass:X4} comparison to 0x{adjacentHandler:X8}.");
        }

        var (addresses, words, traceSha256) = ExecutePassiveDispatch(
            overlay, classRegister, dispatchStartAddress, defaultLoopExitAddress, levelName);
        RequireHash(traceSha256, expectedTraceSha256, $"{levelName} class-0x{GrassActorId:X4} executed trace");
        var (callCount, propertiesReadCount, actorWriteCount) = RequirePassiveEffects(words, defaultLoopExit, levelName);

        return new NativeMobyFamilyDispatchTrace(
            levelKey,
            levelName,
            overlayWadEntry,
            overlayWadOffset,
            overlay.Length,
            overlaySha256,
            OverlayLoadAddress,
            GrassActorId,
            classLoadAddress,
            classLoadWord,
            dispatchStartAddress,
            decisionWindowAddress,
            decisionWindowByteLength,
            Hash(decisionWindow),
            defaultLoopExitAddress,
            Hash(defaultLoopExit),
            addresses,
            words,
            traceSha256,
            adjacentDedicatedClassId,
            adjacentDedicatedHandlerAddress,
            callCount,
            propertiesReadCount,
            actorWriteCount,
            DedicatedHandlerPresent: false,
            ReachesDefaultLoopExit: true,
            ClassSpecificControllerPresent: false);
    }

    internal static NativeMobyFamilyPassiveDispatchTrace InspectPassiveDispatch(
        byte[] overlay, int classRegister, uint dispatchStartAddress, uint defaultLoopExitAddress, string levelName)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        if (classRegister is < 1 or > 31)
            throw new InvalidDataException("Passive dispatch requires one nonzero class register.");
        byte[] defaultLoopExit = ReadOverlayBytes(overlay, defaultLoopExitAddress, 0x18);
        var (addresses, words, traceSha256) = ExecutePassiveDispatch(
            overlay, classRegister, dispatchStartAddress, defaultLoopExitAddress, levelName);
        var (callCount, propertiesReadCount, actorWriteCount) = RequirePassiveEffects(words, defaultLoopExit, levelName);
        return new(Array.AsReadOnly(addresses.ToArray()), Array.AsReadOnly(words.ToArray()), traceSha256,
            callCount, propertiesReadCount, actorWriteCount);
    }

    // Shared unchanged scalar loop. Historical callers still validate their
    // own complete preimages/windows, then the old trace hash, then effects.
    private static (List<uint> Addresses, List<uint> Words, string Sha256) ExecutePassiveDispatch(
        byte[] overlay, int classRegister, uint dispatchStartAddress, uint defaultLoopExitAddress, string levelName)
    {
        uint[] registers = new uint[32];
        registers[classRegister] = GrassActorId;
        List<uint> addresses = [];
        List<uint> words = [];
        uint pc = dispatchStartAddress;
        bool reachedDefaultExit = false;
        for (int step = 0; step < 64 && !reachedDefaultExit; step++)
        {
            uint word = ReadOverlayWord(overlay, pc);
            addresses.Add(pc);
            words.Add(word);
            uint opcode = word >> 26;
            switch (opcode)
            {
                case 0 when word == 0:
                    pc += 4;
                    break;
                case 9: // ADDIU
                case 10: // SLTI
                    ExecuteSimpleInstruction(registers, word, levelName);
                    pc += 4;
                    break;
                case 4: // BEQ
                case 5: // BNE
                {
                    int rs = checked((int)((word >> 21) & 0x1F));
                    int rt = checked((int)((word >> 16) & 0x1F));
                    bool equal = registers[rs] == registers[rt];
                    bool take = opcode == 4 ? equal : !equal;
                    uint delayAddress = pc + 4;
                    uint delayWord = ReadOverlayWord(overlay, delayAddress);
                    addresses.Add(delayAddress);
                    words.Add(delayWord);
                    ExecuteSimpleInstruction(registers, delayWord, levelName);
                    int branchWords = (short)(word & 0xFFFF);
                    uint branchTarget = unchecked(pc + 4 + (uint)(branchWords * 4));
                    pc = take ? branchTarget : pc + 8;
                    break;
                }
                case 2: // J
                {
                    uint delayAddress = pc + 4;
                    uint delayWord = ReadOverlayWord(overlay, delayAddress);
                    addresses.Add(delayAddress);
                    words.Add(delayWord);
                    ExecuteSimpleInstruction(registers, delayWord, levelName);
                    uint target = ((pc + 4) & 0xF0000000u) | ((word & 0x03FFFFFFu) << 2);
                    reachedDefaultExit = target == defaultLoopExitAddress;
                    pc = target;
                    break;
                }
                default:
                    throw new InvalidDataException(
                        $"{levelName} class-0x{GrassActorId:X4} trace reached unsupported word 0x{word:X8} at 0x{pc:X8}.");
            }
        }
        if (!reachedDefaultExit || pc != defaultLoopExitAddress)
            throw new InvalidDataException($"{levelName} class-0x{GrassActorId:X4} did not reach its default loop exit.");

        byte[] traceBytes = new byte[words.Count * sizeof(uint)];
        for (int index = 0; index < words.Count; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(traceBytes.AsSpan(index * 4, 4), words[index]);
        string traceSha256 = Hash(traceBytes);
        return (addresses, words, traceSha256);
    }

    private static (int Calls, int PropertiesReads, int ActorWrites) RequirePassiveEffects(
        List<uint> words, byte[] defaultLoopExit, string levelName)
    {
        uint[] defaultExitWords = Enumerable.Range(0, defaultLoopExit.Length / 4)
            .Select(index => ReadUInt32(defaultLoopExit, index * 4))
            .ToArray();
        int callCount = words.Count(IsCallInstruction) + defaultExitWords.Count(IsCallInstruction);
        int propertiesReadCount = words.Count(word => IsPropertiesRead(word, runtimeActorRegister: 19)) +
                                  defaultExitWords.Count(word => IsPropertiesRead(word, runtimeActorRegister: 19));
        int actorWriteCount = words.Count(word => IsRuntimeActorWrite(word, runtimeActorRegister: 19)) +
                              defaultExitWords.Count(word => IsRuntimeActorWrite(word, runtimeActorRegister: 19));
        if (callCount != 0 || propertiesReadCount != 0 || actorWriteCount != 0)
            throw new InvalidDataException($"{levelName} passive class trace gained a call, m_Props read, or actor write.");
        return (callCount, propertiesReadCount, actorWriteCount);
    }

    private static void ExecuteSimpleInstruction(uint[] registers, uint word, string levelName)
    {
        if (word == 0)
            return;
        uint opcode = word >> 26;
        int rs = checked((int)((word >> 21) & 0x1F));
        int rt = checked((int)((word >> 16) & 0x1F));
        int immediate = (short)(word & 0xFFFF);
        switch (opcode)
        {
            case 9: // ADDIU
                registers[rt] = unchecked(registers[rs] + (uint)immediate);
                break;
            case 10: // SLTI
                registers[rt] = (int)registers[rs] < immediate ? 1u : 0u;
                break;
            default:
                throw new InvalidDataException(
                    $"{levelName} trace delay/simple slot contains unsupported word 0x{word:X8}.");
        }
        registers[0] = 0;
    }

    private static int CountDedicatedClassComparisons(
        byte[] overlay,
        int classRegister,
        ushort actorId,
        uint? expectedHandler)
    {
        int count = 0;
        for (int offset = 0; offset <= overlay.Length - 8; offset += 4)
        {
            uint load = ReadUInt32(overlay, offset);
            if ((load >> 26) != 9 || ((load >> 21) & 0x1F) != 0 ||
                (load & 0xFFFF) != actorId)
            {
                continue;
            }
            int temporaryRegister = checked((int)((load >> 16) & 0x1F));
            uint branch = ReadUInt32(overlay, offset + 4);
            if ((branch >> 26) != 4)
                continue;
            int branchRs = checked((int)((branch >> 21) & 0x1F));
            int branchRt = checked((int)((branch >> 16) & 0x1F));
            if (!((branchRs == classRegister && branchRt == temporaryRegister) ||
                  (branchRt == classRegister && branchRs == temporaryRegister)))
            {
                continue;
            }
            uint branchAddress = OverlayLoadAddress + checked((uint)(offset + 4));
            int branchWords = (short)(branch & 0xFFFF);
            uint target = unchecked(branchAddress + 4 + (uint)(branchWords * 4));
            if (!expectedHandler.HasValue || target == expectedHandler.Value)
                count++;
        }
        return count;
    }

    private static bool IsCallInstruction(uint word) =>
        (word >> 26) == 3 || ((word >> 26) == 0 && (word & 0x3F) == 9);

    private static bool IsPropertiesRead(uint word, int runtimeActorRegister)
    {
        uint opcode = word >> 26;
        bool isLoad = opcode is >= 0x20 and <= 0x27 or 0x30 or 0x31 or 0x32 or 0x33;
        return isLoad && ((word >> 21) & 0x1F) == runtimeActorRegister && (word & 0xFFFF) == 0;
    }

    private static bool IsRuntimeActorWrite(uint word, int runtimeActorRegister)
    {
        uint opcode = word >> 26;
        bool isStore = opcode is >= 0x28 and <= 0x2E or 0x38 or 0x39 or 0x3A or 0x3B;
        return isStore && ((word >> 21) & 0x1F) == runtimeActorRegister;
    }

    private static byte[] ReadOverlayBytes(byte[] overlay, uint address, int byteLength)
    {
        int offset = checked((int)(address - OverlayLoadAddress));
        if (offset < 0 || byteLength < 0 || offset > overlay.Length - byteLength)
            throw new InvalidDataException($"Overlay address 0x{address:X8}/0x{byteLength:X} is out of range.");
        return overlay.AsSpan(offset, byteLength).ToArray();
    }

    private static uint ReadOverlayWord(byte[] overlay, uint address) =>
        BinaryPrimitives.ReadUInt32LittleEndian(ReadOverlayBytes(overlay, address, 4));

    private static void RequireOverlayWord(byte[] overlay, uint address, uint expected, string label)
    {
        uint actual = ReadOverlayWord(overlay, address);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X8}, expected 0x{expected:X8}.");
    }

    private static NativeMobyFamilyFaceStream ParseFaceStream(
        byte[] package,
        int start,
        string tier)
    {
        if (start < 0 || start + 4 > package.Length)
            throw new InvalidDataException($"The Grass {tier} face table starts outside its package.");
        int bodyLength = ReadInt32(package, start);
        int end = checked(start + 4 + bodyLength);
        if (bodyLength < 0 || (bodyLength & 3) != 0 || end > package.Length)
            throw new InvalidDataException($"The Grass {tier} face table has invalid length 0x{bodyLength:X}.");
        int cursor = start + 4;
        int records = 0;
        int untexturedTriangles = 0;
        int textured = 0;
        while (cursor < end)
        {
            if (cursor + 8 > end)
                throw new InvalidDataException($"The Grass {tier} face table ends inside a renderer command.");
            uint control = ReadUInt32(package, cursor);
            int recordBytes;
            bool isTextured;
            bool isUntexturedTriangle = false;
            if ((control & 0x80000000u) == 0)
            {
                isTextured = (control & 0x2u) != 0;
                recordBytes = isTextured ? 20 : 8;
                isUntexturedTriangle = !isTextured;
            }
            else if ((control & 0x4u) != 0)
            {
                isTextured = true;
                recordBytes = 20;
            }
            else
            {
                isTextured = (control & 0x2u) != 0;
                recordBytes = isTextured ? 24 : 12;
            }
            if (cursor + recordBytes > end)
                throw new InvalidDataException($"The Grass {tier} renderer command overruns its face table.");
            records++;
            if (isTextured)
                textured++;
            if (isUntexturedTriangle)
                untexturedTriangles++;
            cursor += recordBytes;
        }
        byte[] table = package.AsSpan(start, 4 + bodyLength).ToArray();
        return new NativeMobyFamilyFaceStream(
            tier,
            start,
            bodyLength,
            table.Length,
            records,
            untexturedTriangles,
            textured,
            Hash(table),
            EndsExactlyUnderRetailRendererGrammar: cursor == end);
    }

    internal static ActorRootTable ReadActorRootTable(byte[] header)
    {
        const int rootCount = 64;
        int[] roots = new int[rootCount];
        ushort[] actorIds = new ushort[rootCount];
        bool sawZero = false;
        int populated = 0;
        int previous = 0;
        for (int index = 0; index < rootCount; index++)
        {
            int root = checked((int)ReadUInt32(header, 0x50 + (index * 4)));
            ushort actorId = ReadUInt16(header, 0x150 + (index * 2));
            roots[index] = root;
            actorIds[index] = actorId;
            if (root == 0)
            {
                sawZero = true;
                continue;
            }
            if (sawZero || root <= previous)
                throw new InvalidDataException("An actor-root table is not contiguous and strictly ascending.");
            populated++;
            previous = root;
        }
        return new ActorRootTable(roots, actorIds, populated);
    }

    private static int[] ReadFixupFields(byte[] component, int expectedCount)
    {
        int[] result = new int[expectedCount];
        for (int index = 0; index < expectedCount; index++)
            result[index] = checked((int)ReadUInt32(component, 4 + (index * 4)));
        return result;
    }

    private static int CountAlignedValuesInRange(byte[] bytes, int minimumInclusive, int maximumExclusive)
    {
        int count = 0;
        for (int offset = 0; offset <= bytes.Length - 4; offset += 4)
        {
            uint value = ReadUInt32(bytes, offset);
            if (value >= minimumInclusive && value < maximumExclusive)
                count++;
        }
        return count;
    }

    private static void RequireNestedSubfile(
        ReadOnlySpan<byte> entry,
        int subfileIndex,
        int expectedOffset,
        int expectedLength)
    {
        ReadOnlySpan<byte> descriptor = entry.Slice(subfileIndex * 8, 8);
        RequireUInt32(descriptor.Slice(0, 4), checked((uint)expectedOffset), $"nested subfile {subfileIndex} offset");
        RequireUInt32(descriptor.Slice(4, 4), checked((uint)expectedLength), $"nested subfile {subfileIndex} length");
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireZero(ReadOnlySpan<byte> bytes, string label)
    {
        if (bytes.ContainsAnyExcept((byte)0))
            throw new InvalidDataException($"{label} is no longer zero-filled.");
    }

    private static void RequireUInt32(ReadOnlySpan<byte> bytes, long expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X}, expected 0x{expected:X}.");
    }

    private static void RequireUInt16(ReadOnlySpan<byte> bytes, ushort expected, string label)
    {
        ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X4}, expected 0x{expected:X4}.");
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    internal sealed record ActorRootTable(int[] Roots,ushort[] ActorIds,int PopulatedCount);
}
