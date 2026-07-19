using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Completes one guarded Green Wizard existing-slot candidate in Blowhard.
/// Blowhard already owns the 0x011B/0x0026 runtime handlers and actor packages,
/// but a copied 0x58-byte row is not sufficient. V2 grows the native Moby
/// properties component for a private Wizard block, moves the later scene
/// components intact, translates the donor route, preserves the target pod,
/// and appends the properties-internal pointer fixup.
/// </summary>
public static class BlowhardGreenWizardResidentSwapExporter
{
    public const string RecipeId = "blowhard.greenWizard.residentRuntime.expandedPropsComponent.v2";

    private const int WadLba = 37;
    private const int BlowhardOverlayEntry = 41;
    private const int BlowhardDataEntry = 42;
    private const int RecordStride = 0x58;
    private const int ActorIdOffset = 0x36;
    private const int PodOrGroupOffset = 0x43;
    private const int RendererDistanceOffset = 0x4B;
    private const int RewardOwnerOffset = 0x52;
    private const int RewardOffset = 0x53;
    private const int XOffset = 0x0C;
    private const int YOffset = 0x10;
    private const int ZOffset = 0x14;
    private const int YawMatrixOffset = 0x20;
    private const int YawMatrixBytes = 0x12;
    private const int YawByteOffset = 0x46;
    private const int CullingSectorOffset = 0x4A;

    private const int ExpectedOverlayBytes = 0xD000;
    private const int ExpectedDataBytes = 0x1DD800;
    private const int ExpectedSceneBase = 0x16B000;
    private const int ExpectedSceneBytes = 0x27800;
    private const int NativeWizardTrueIndex = 0;
    private const int NativeWizardPropertiesOffset = 0x243A8;
    private const int NativeWizardPropertiesBytes = 0x40;
    private const int PropertiesComponentHeaderOffset = 0x243A4;
    private const int PropertiesComponentBytesBefore = 0x0AC4;
    private const int PropertiesComponentBytesAfter = 0x0B04;
    private const int TargetPropertiesOffset = 0x24E68;
    private const int InternalRouteAnchorOffset = 0x28;
    private const int FirstRoutePointOffset = 0x30;
    private const int SceneInsertBytes = NativeWizardPropertiesBytes;
    private const int SceneShiftSourceOffset = 0x24E68;
    private const int SceneShiftDestinationOffset = SceneShiftSourceOffset + SceneInsertBytes;
    private const int SceneShiftBytes = 0x21C4;
    private const int NativePodsOffset = 0x24E68;
    private const int ShiftedPodsOffset = NativePodsOffset + SceneInsertBytes;
    private const int NativeCollisionChainOffset = 0x24E90;
    private const int ShiftedCollisionChainOffset = NativeCollisionChainOffset + SceneInsertBytes;
    private const int CollisionChainBytes = 0x2004;
    private const int PointerFixupListOffsetBefore = 0x26E94;
    private const int PointerFixupListOffsetAfter = PointerFixupListOffsetBefore + SceneInsertBytes;
    private const int PointerFixupCountBefore = 0x65;
    private const int PointerFixupCountAfter = 0x66;
    private const int PointerFixupAppendOffset = 0x2706C;
    private const int FinalUsedSceneEnd = 0x27070;

    private const int GreenWizardRootSlot = 0x5C;
    private const int LightningRootSlot = 0xA4;
    private const uint GreenWizardRoot = 0x133AAC;
    private const uint LightningRoot = 0x1658B4;
    private const ushort GreenWizardActorId = 0x011B;
    private const ushort LightningActorId = 0x0026;
    private const int GreenWizardDispatchOffset = 0x758;
    private const int LightningDispatchOffset = 0x63C;
    private const uint GreenWizardDispatchWord = 0x2402011B;
    private const uint LightningDispatchWord = 0x24020026;

    private const string NativeWizardPropertiesSha256 = "5e293d4f8ef7271be46d29c9763bd3ebab45648224a29f02e51f4fe13b47f82a";
    private const string PropertiesComponentPreimageSha256 = "7ed95e862acb5a520c9c2002f1b1fa477fbacab61583b4c9c986ab747ca57851";
    private const string SceneShiftPreimageSha256 = "abfeca3758007d64cd00968da21b54b11fe2be4b1fe0951c21332c877a2dcd11";
    private const string SceneTailCapacityPreimageSha256 = "f5a5fd42d16a20302798ef6ed309979b43003d2320d9f0e8ea9831a92759fb4b";
    private const string NativePodsSha256 = "2dacf76cdbc92e25244d63e3142553fe6982d9034ec0e14f5a61e6c0809d87f4";
    private const string NativeCollisionChainSha256 = "e5b0e80369ae6416ae4a4e144b3b3627142b3a273296da54e595377627901944";
    private const string PointerFixupsPreimageSha256 = "992ed2add8c565f7ef59457d40749e5dff79a0203206f01097c6e31e422e86a9";
    private const string ShiftedFixupsSha256 = "89fb6d325bc753b493b4125fc62423698bd909d70958f9385b7e41d7fbdbdf87";
    private const string FinalZeroTailSha256 = "3fcb1e1041f63f752bdebe8f875700003bd95e3fc507c2143555443c145c9769";

    public static BlowhardGreenWizardResidentSwapPlan ApplyAndVerify(
        string imagePath,
        LevelDefinition level,
        MobySourcePatchPlan sourcePatchPlan,
        string outputPlanPath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The Blowhard Green Wizard base candidate image is missing.", imagePath);
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "blowhard", StringComparison.OrdinalIgnoreCase) ||
            level.SourceWadEntry != BlowhardDataEntry)
        {
            throw new InvalidOperationException("The resident Green Wizard completion recipe is mapped only for Blowhard.");
        }

        RuntimeBundleCompatibilityResult compatibility = GreenWizardRuntimeBundleCatalog.Evaluate(level.Key);
        LevelRuntimeBundleProfile profile = GreenWizardRuntimeBundleCatalog.FindProfile(level.Key) ??
            throw new InvalidOperationException("The Blowhard Green Wizard runtime-bundle profile is missing.");
        bool recognizedResidentStatus =
            compatibility.Status is RuntimeBundleCompatibilityStatus.Candidate or RuntimeBundleCompatibilityStatus.Ready;
        bool verifiedProfileIsWritable = compatibility.Status != RuntimeBundleCompatibilityStatus.Ready ||
            (compatibility.NormalCreateBinReady &&
             !compatibility.RequiresRuntimeSmoke &&
             profile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
             string.Equals(profile.RuntimeProofRecipeId, RecipeId, StringComparison.Ordinal) &&
             !string.IsNullOrWhiteSpace(profile.RuntimeProofOutputSha256));
        if (!recognizedResidentStatus ||
            !verifiedProfileIsWritable ||
            compatibility.Deployment != RuntimeBundleDeploymentKind.ResidentActor ||
            !compatibility.CanStageInstance ||
            profile.Deployment != RuntimeBundleDeploymentKind.ResidentActor ||
            !profile.InstanceLayout.PreserveTargetPodOrGroup ||
            !profile.InstanceLayout.TranslateDonorRouteFromSpawn ||
            !string.Equals(LevelCatalog.NormalizeKey(profile.InstanceLayout.PropertiesDonorLevelKey), "blowhard", StringComparison.OrdinalIgnoreCase) ||
            profile.InstanceLayout.PropertiesDonorTrueIndex != NativeWizardTrueIndex ||
            profile.InstanceLayout.PropertiesBytes != NativeWizardPropertiesBytes)
        {
            throw new InvalidOperationException("The Blowhard Green Wizard runtime-bundle profile is not a writable target-pod-preserving, route-translating resident recipe.");
        }

        RuntimeBundleResidentPropertiesLayout residentLayout = profile.ResidentDetection?.PropertiesLayout ??
            throw new InvalidOperationException("The Blowhard Green Wizard profile is missing its native properties-component evidence.");
        if (residentLayout.PropertiesComponentHeaderSceneOffset != PropertiesComponentHeaderOffset ||
            residentLayout.PropertiesComponentBytes != PropertiesComponentBytesBefore ||
            residentLayout.PointerFixupListSceneOffset != PointerFixupListOffsetBefore ||
            residentLayout.PointerFixupCount != PointerFixupCountBefore ||
            residentLayout.TrailingScenePaddingBytes != ExpectedSceneBytes - (PointerFixupListOffsetBefore + sizeof(uint) + (PointerFixupCountBefore * sizeof(uint))))
        {
            throw new InvalidOperationException("The Blowhard Green Wizard profile no longer matches the checked structural scene-growth contract.");
        }

        MobySourcePatch wizardPatch = FindSingleWizardSwap(sourcePatchPlan);
        byte[] beforeRecord = ParseHex(wizardPatch.BeforeHexPreview, RecordStride, "Wizard target record preimage");
        byte[] candidateRecord = ParseHex(wizardPatch.AfterHexPreview, RecordStride, "Wizard candidate record");
        ushort beforeActor = ReadActorId(beforeRecord);
        ushort afterActor = ReadActorId(candidateRecord);
        if (beforeActor == GreenWizardActorId || afterActor != GreenWizardActorId)
        {
            throw new InvalidDataException(
                $"The Blowhard resident recipe expects one non-Wizard slot to become actor 0x011B; " +
                $"the selected T{wizardPatch.TrueIndex} transition was 0x{beforeActor:X4}->0x{afterActor:X4}.");
        }
        if (candidateRecord[RewardOffset] != beforeRecord[RewardOffset])
            throw new InvalidDataException("The Green Wizard candidate did not preserve the selected Blowhard enemy's reward byte.");
        foreach (int offset in new[] { XOffset, YOffset, ZOffset })
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(offset, 4)) !=
                BinaryPrimitives.ReadInt32LittleEndian(beforeRecord.AsSpan(offset, 4)))
            {
                throw new InvalidDataException($"The Green Wizard candidate changed the selected Blowhard enemy's placement at +0x{offset:X2}.");
            }
        }
        if (!candidateRecord.AsSpan(YawMatrixOffset, YawMatrixBytes).SequenceEqual(beforeRecord.AsSpan(YawMatrixOffset, YawMatrixBytes)) ||
            candidateRecord[YawByteOffset] != beforeRecord[YawByteOffset] ||
            candidateRecord[CullingSectorOffset] != beforeRecord[CullingSectorOffset])
        {
            throw new InvalidDataException("The Green Wizard candidate did not preserve the selected Blowhard enemy's yaw and culling sector.");
        }

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        (long overlayBase, int overlayBytes) = ReadWadEntry(stream, layout, BlowhardOverlayEntry);
        (long dataBase, int dataBytes) = ReadWadEntry(stream, layout, BlowhardDataEntry);
        if (overlayBytes != ExpectedOverlayBytes || dataBytes != ExpectedDataBytes)
        {
            throw new InvalidDataException(
                $"Blowhard WAD entry sizes changed: overlay=0x{overlayBytes:X}, data=0x{dataBytes:X}.");
        }

        byte[] dataHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, dataBase, 0x20);
        int sceneBase = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x18, 4)));
        int sceneBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x1C, 4)));
        if (sceneBase != ExpectedSceneBase || sceneBytes != ExpectedSceneBytes)
            throw new InvalidDataException($"Blowhard scene layout changed: base=0x{sceneBase:X}, size=0x{sceneBytes:X}.");

        long sceneWadOffset = checked(dataBase + sceneBase);
        long tableWadOffset = ParseHexOffset(level.SourceTableWadOffset, "Blowhard source table");
        int tableSceneOffset = checked((int)(tableWadOffset - sceneWadOffset));
        int targetPointerFieldSceneOffset = checked(tableSceneOffset + (wizardPatch.TrueIndex * RecordStride));
        if (targetPointerFieldSceneOffset < 0 || targetPointerFieldSceneOffset > sceneBytes - RecordStride)
            throw new InvalidDataException("The selected Blowhard source record is outside the checked scene block.");

        ValidateResidentRuntime(stream, layout, overlayBase, dataBase);
        byte[] nativeProperties = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + NativeWizardPropertiesOffset,
            NativeWizardPropertiesBytes);
        ValidateSha256(nativeProperties, NativeWizardPropertiesSha256, "Blowhard native Green Wizard properties");
        if (BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties) != NativeWizardPropertiesOffset + InternalRouteAnchorOffset ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties.AsSpan(InternalRouteAnchorOffset, 4)) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties.AsSpan(InternalRouteAnchorOffset + 4, 4)) != 0x00010000)
        {
            throw new InvalidDataException("The checked Blowhard native Green Wizard properties layout changed.");
        }

        byte[] nativeWizardRecord = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            tableWadOffset + (NativeWizardTrueIndex * RecordStride),
            RecordStride);
        if (ReadActorId(nativeWizardRecord) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeWizardRecord) != NativeWizardPropertiesOffset)
        {
            throw new InvalidDataException("The checked Blowhard T0 native Wizard row changed.");
        }

        byte[] propertiesComponentBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PropertiesComponentHeaderOffset,
            PropertiesComponentBytesBefore);
        ValidateSha256(propertiesComponentBefore, PropertiesComponentPreimageSha256, "Blowhard Moby properties component");
        if (BinaryPrimitives.ReadUInt32LittleEndian(propertiesComponentBefore) != PropertiesComponentBytesBefore)
            throw new InvalidDataException("The Blowhard Moby properties component size changed.");

        byte[] sceneShiftBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + SceneShiftSourceOffset,
            SceneShiftBytes);
        ValidateSha256(sceneShiftBefore, SceneShiftPreimageSha256, "Blowhard post-properties scene components");
        byte[] nativePods = sceneShiftBefore.AsSpan(0, NativeCollisionChainOffset - NativePodsOffset).ToArray();
        byte[] nativeCollisionChain = sceneShiftBefore.AsSpan(
            NativeCollisionChainOffset - SceneShiftSourceOffset,
            CollisionChainBytes).ToArray();
        ValidateSha256(nativePods, NativePodsSha256, "Blowhard pod component");
        ValidateSha256(nativeCollisionChain, NativeCollisionChainSha256, "Blowhard collision-chain component");

        byte[] sceneTailCapacityBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + SceneShiftSourceOffset + SceneShiftBytes,
            SceneInsertBytes);
        ValidateSha256(sceneTailCapacityBefore, SceneTailCapacityPreimageSha256, "Blowhard scene structural-growth capacity");

        int pointerFixupBytes = checked(sizeof(uint) + (PointerFixupCountBefore * sizeof(uint)));
        byte[] pointerFixupsBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffsetBefore,
            pointerFixupBytes);
        ValidateSha256(pointerFixupsBefore, PointerFixupsPreimageSha256, "Blowhard scene pointer-fixup list");
        if (BinaryPrimitives.ReadUInt32LittleEndian(pointerFixupsBefore) != PointerFixupCountBefore)
            throw new InvalidDataException("The Blowhard scene pointer-fixup count changed.");
        uint[] fixups = new uint[PointerFixupCountBefore];
        for (int i = 0; i < fixups.Length; i++)
            fixups[i] = BinaryPrimitives.ReadUInt32LittleEndian(pointerFixupsBefore.AsSpan(4 + (i * 4), 4));
        if (!fixups.Contains(checked((uint)targetPointerFieldSceneOffset)))
            throw new InvalidDataException("The selected Blowhard source-record properties pointer is not covered by the native fixup list.");
        if (fixups.Any(value => value >= SceneShiftSourceOffset))
            throw new InvalidDataException("The Blowhard scene shift would move an existing pointer field; this v2 recipe requires every native fixup field to remain before the insertion.");

        byte[] installedProperties = nativeProperties.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            installedProperties.AsSpan(0, 4),
            checked((uint)(TargetPropertiesOffset + InternalRouteAnchorOffset)));
        BinaryPrimitives.WriteUInt32LittleEndian(installedProperties.AsSpan(0x28, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(installedProperties.AsSpan(0x2C, 4), 0x00010000);
        int rawX = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(XOffset, 4));
        int rawY = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(YOffset, 4));
        int rawZ = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(ZOffset, 4));
        int nativeX = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(XOffset, 4));
        int nativeY = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(YOffset, 4));
        int nativeZ = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(ZOffset, 4));
        int nativeRouteX = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(FirstRoutePointOffset, 4));
        int nativeRouteY = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(FirstRoutePointOffset + 4, 4));
        int nativeRouteZ = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(FirstRoutePointOffset + 8, 4));
        BlowhardGreenWizardRoutePoint nativeRouteDelta = new(
            checked(nativeRouteX - nativeX),
            checked(nativeRouteY - nativeY),
            checked(nativeRouteZ - nativeZ));
        BlowhardGreenWizardRoutePoint translatedRoute = new(
            checked(rawX + nativeRouteDelta.X),
            checked(rawY + nativeRouteDelta.Y),
            checked(rawZ + nativeRouteDelta.Z));
        BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(FirstRoutePointOffset, 4), translatedRoute.X);
        BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(FirstRoutePointOffset + 4, 4), translatedRoute.Y);
        BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(FirstRoutePointOffset + 8, 4), translatedRoute.Z);
        BinaryPrimitives.WriteUInt32LittleEndian(installedProperties.AsSpan(FirstRoutePointOffset + 12, 4), 0);

        BinaryPrimitives.WriteUInt32LittleEndian(candidateRecord.AsSpan(0, 4), TargetPropertiesOffset);
        candidateRecord[PodOrGroupOffset] = beforeRecord[PodOrGroupOffset];
        candidateRecord[RendererDistanceOffset] = 0x09;
        candidateRecord[RewardOwnerOffset] = 0xFF;
        candidateRecord[RewardOffset] = beforeRecord[RewardOffset];

        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + SceneShiftDestinationOffset,
            sceneShiftBefore);
        byte[] word = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(word, PropertiesComponentBytesAfter);
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PropertiesComponentHeaderOffset,
            word);
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + TargetPropertiesOffset,
            installedProperties);
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            tableWadOffset + ((long)wizardPatch.TrueIndex * RecordStride),
            candidateRecord);
        BinaryPrimitives.WriteUInt32LittleEndian(word, PointerFixupCountAfter);
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffsetAfter,
            word);
        BinaryPrimitives.WriteUInt32LittleEndian(word, TargetPropertiesOffset);
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupAppendOffset,
            word);
        stream.Flush(true);

        VerifyReadback(
            stream,
            layout,
            tableWadOffset,
            sceneWadOffset,
            wizardPatch.TrueIndex,
            beforeRecord[RewardOffset],
            beforeRecord[PodOrGroupOffset],
            translatedRoute,
            installedProperties,
            nativePods,
            nativeCollisionChain,
            pointerFixupsBefore);

        string installedPropertiesSha256 = Sha256(installedProperties);
        string outputImageSha256 = Sha256File(imagePath);
        BlowhardGreenWizardResidentSwapPlan plan = new(
            RecipeId,
            GreenWizardRuntimeBundleCatalog.Manifest.BundleId,
            GreenWizardRuntimeBundleCatalog.Manifest.Fingerprint,
            profile.Fingerprint,
            imagePath,
            outputImageSha256,
            level.Key,
            wizardPatch.TrueIndex,
            beforeActor,
            beforeRecord[RewardOffset],
            tableWadOffset,
            sceneBase,
            sceneBytes,
            targetPointerFieldSceneOffset,
            PropertiesComponentHeaderOffset,
            PropertiesComponentBytesBefore,
            PropertiesComponentBytesAfter,
            TargetPropertiesOffset,
            NativeWizardPropertiesBytes,
            NativeWizardPropertiesSha256,
            PropertiesComponentPreimageSha256,
            SceneShiftSourceOffset,
            SceneShiftBytes,
            SceneShiftPreimageSha256,
            SceneTailCapacityPreimageSha256,
            installedPropertiesSha256,
            beforeRecord[PodOrGroupOffset],
            nativeRouteDelta,
            translatedRoute,
            PointerFixupListOffsetBefore,
            PointerFixupListOffsetAfter,
            PointerFixupCountBefore,
            PointerFixupCountAfter,
            PointerFixupAppendOffset,
            GreenWizardRoot,
            LightningRoot,
            [
                "Actor 0x011B and lightning actor 0x0026 remain on Blowhard's native roots and dispatch cases.",
                "The selected enemy's source row, placement, pod/group, culling sector, yaw, and reward remain in the same source slot.",
                "The actual Blowhard Moby-properties component grows by 0x40; the following pod, collision-chain, and pointer-fixup components move intact instead of aliasing runtime collision workspace.",
                "A native-shaped 0x40 Blowhard Wizard properties block receives T0's one-point route translated from the selected destination spawn.",
                "The moved properties-internal route pointer is covered by a checked 0x65-to-0x66 scene fixup-list append.",
                "Final image readback verifies the row, expanded properties component, translated route, byte-identical pod/collision payloads, resident roots, dispatch words, and moved fixup list."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        return plan;
    }

    private static MobySourcePatch FindSingleWizardSwap(MobySourcePatchPlan plan)
    {
        MobySourcePatch[] matches = plan.Patches
            .Where(patch =>
                string.Equals(patch.Kind, "cross-level-existing-slot-candidate", StringComparison.OrdinalIgnoreCase) &&
                TryReadActorId(patch.AfterHexPreview, out ushort actorId) &&
                actorId == GreenWizardActorId)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"The guarded Blowhard resident recipe supports exactly one Green Wizard slot swap per candidate; found {matches.Length}.");
        }

        return matches[0];
    }

    private static void ValidateResidentRuntime(FileStream stream, DiscLayout layout, long overlayBase, long dataBase)
    {
        ValidateWord(stream, layout, overlayBase + GreenWizardDispatchOffset, GreenWizardDispatchWord, "Blowhard Green Wizard dispatch");
        ValidateWord(stream, layout, overlayBase + LightningDispatchOffset, LightningDispatchWord, "Blowhard lightning dispatch");
        ValidateWord(stream, layout, dataBase + GreenWizardRootSlot, GreenWizardRoot, "Blowhard Green Wizard actor root");
        ValidateWord(stream, layout, dataBase + LightningRootSlot, LightningRoot, "Blowhard lightning actor root");
        ValidateActorId(stream, layout, dataBase, GreenWizardRootSlot, GreenWizardActorId);
        ValidateActorId(stream, layout, dataBase, LightningRootSlot, LightningActorId);
    }

    private static void VerifyReadback(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        long sceneWadOffset,
        int trueIndex,
        byte reward,
        byte podOrGroup,
        BlowhardGreenWizardRoutePoint translatedRoute,
        byte[] expectedProperties,
        byte[] expectedPods,
        byte[] expectedCollisionChain,
        byte[] originalPointerFixups)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
        if (ReadActorId(row) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(row) != TargetPropertiesOffset ||
            row[PodOrGroupOffset] != podOrGroup ||
            row[RendererDistanceOffset] != 0x09 ||
            row[RewardOwnerOffset] != 0xFF ||
            row[RewardOffset] != reward)
        {
            throw new InvalidDataException("The final Blowhard Green Wizard source row failed readback.");
        }

        byte[] properties = DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + TargetPropertiesOffset, NativeWizardPropertiesBytes);
        if (!properties.AsSpan().SequenceEqual(expectedProperties) ||
            BinaryPrimitives.ReadUInt32LittleEndian(properties) != TargetPropertiesOffset + InternalRouteAnchorOffset ||
            BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(FirstRoutePointOffset, 4)) != translatedRoute.X ||
            BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(FirstRoutePointOffset + 4, 4)) != translatedRoute.Y ||
            BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(FirstRoutePointOffset + 8, 4)) != translatedRoute.Z)
        {
            throw new InvalidDataException("The final Blowhard Green Wizard properties/route failed readback.");
        }

        uint propertiesComponentBytes = BinaryPrimitives.ReadUInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + PropertiesComponentHeaderOffset, sizeof(uint)));
        byte[] shiftedPods = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + ShiftedPodsOffset,
            expectedPods.Length);
        byte[] shiftedCollisionChain = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + ShiftedCollisionChainOffset,
            expectedCollisionChain.Length);
        if (propertiesComponentBytes != PropertiesComponentBytesAfter ||
            !shiftedPods.AsSpan().SequenceEqual(expectedPods) ||
            !shiftedCollisionChain.AsSpan().SequenceEqual(expectedCollisionChain))
        {
            throw new InvalidDataException("The final Blowhard properties growth did not preserve the shifted pod/collision components byte-for-byte.");
        }

        int finalFixupBytes = checked(sizeof(uint) + (PointerFixupCountAfter * sizeof(uint)));
        byte[] shiftedFixups = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffsetAfter,
            finalFixupBytes);
        ValidateSha256(shiftedFixups, ShiftedFixupsSha256, "shifted Blowhard pointer-fixup list");
        uint fixupCount = BinaryPrimitives.ReadUInt32LittleEndian(shiftedFixups);
        uint appendedFixup = BinaryPrimitives.ReadUInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + PointerFixupAppendOffset, 4));
        if (fixupCount != PointerFixupCountAfter ||
            appendedFixup != TargetPropertiesOffset ||
            !shiftedFixups.AsSpan(sizeof(uint), originalPointerFixups.Length - sizeof(uint))
                .SequenceEqual(originalPointerFixups.AsSpan(sizeof(uint))))
        {
            throw new InvalidDataException("The final Blowhard Green Wizard pointer-fixup append failed readback.");
        }

        byte[] finalZeroTail = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + FinalUsedSceneEnd,
            ExpectedSceneBytes - FinalUsedSceneEnd);
        ValidateSha256(finalZeroTail, FinalZeroTailSha256, "Blowhard scene tail after structural growth");
    }

    private static (long Offset, int Bytes) ReadWadEntry(FileStream stream, DiscLayout layout, int entryIndex)
    {
        byte[] header = DiscImage.ReadFileBytes(stream, layout, WadLba, entryIndex * 8L, 8);
        return (
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)),
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4))));
    }

    private static void ValidateActorId(FileStream stream, DiscLayout layout, long dataBase, int rootSlot, ushort expected)
    {
        int rootIndex = (rootSlot - 0x50) / 4;
        ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, dataBase + 0x150 + (rootIndex * 2L), 2));
        if (actual != expected)
            throw new InvalidDataException($"Blowhard root slot 0x{rootSlot:X} expected actor 0x{expected:X4}, found 0x{actual:X4}.");
    }

    private static void ValidateWord(FileStream stream, DiscLayout layout, long wadOffset, uint expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, 4));
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X8}, found 0x{actual:X8}.");
    }

    private static void ValidateSha256(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 expected {expected}, found {actual}.");
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static long ParseHexOffset(string value, string label)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        if (!long.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long result))
            throw new InvalidDataException($"{label} is not a hexadecimal offset: {value}.");
        return result;
    }

    private static byte[] ParseHex(string value, int requiredBytes, string label)
    {
        string normalized = new((value ?? "").Where(Uri.IsHexDigit).ToArray());
        if (normalized.Length != requiredBytes * 2)
            throw new InvalidDataException($"{label} expected {requiredBytes} bytes, found {normalized.Length / 2}.");
        return Convert.FromHexString(normalized);
    }

    private static bool TryReadActorId(string hex, out ushort actorId)
    {
        actorId = 0;
        try
        {
            byte[] bytes = ParseHex(hex, RecordStride, "candidate record");
            actorId = ReadActorId(bytes);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static ushort ReadActorId(ReadOnlySpan<byte> record) =>
        (ushort)(record[ActorIdOffset] | (record[ActorIdOffset + 1] << 8));
}

public sealed record BlowhardGreenWizardResidentSwapPlan(
    string RecipeId,
    string BundleId,
    string BundleFingerprint,
    string ProfileFingerprint,
    string OutputImagePath,
    string OutputImageSha256,
    string TargetLevelKey,
    int TargetTrueIndex,
    ushort ReplacedActorId,
    int PreservedReward,
    long SourceTableWadOffset,
    int SceneBaseOffset,
    int SceneBytes,
    int PropertiesPointerFieldSceneOffset,
    int PropertiesComponentHeaderSceneOffset,
    int PropertiesComponentBytesBefore,
    int PropertiesComponentBytesAfter,
    int PropertiesSceneOffset,
    int PropertiesBytes,
    string NativePropertiesSha256,
    string PropertiesComponentPreimageSha256,
    int SceneShiftSourceOffset,
    int SceneShiftBytes,
    string SceneShiftPreimageSha256,
    string SceneTailCapacityPreimageSha256,
    string InstalledPropertiesSha256,
    byte PreservedPodOrGroup,
    BlowhardGreenWizardRoutePoint NativeRouteDelta,
    BlowhardGreenWizardRoutePoint RoutePoint,
    int PointerFixupListSceneOffsetBefore,
    int PointerFixupListSceneOffsetAfter,
    int PointerFixupCountBefore,
    int PointerFixupCountAfter,
    int PointerFixupAppendSceneOffset,
    uint GreenWizardRoot,
    uint LightningRoot,
    IReadOnlyList<string> FocusedChecks);

public sealed record BlowhardGreenWizardRoutePoint(int X, int Y, int Z);
