using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Completes one guarded Green Wizard existing-slot candidate in Magic Crafters.
/// Magic Crafters already owns native actor 0x011B and lightning actor 0x0026
/// roots, handlers, packages, textures, and particle dependencies. This recipe
/// reuses T27's guarded 0x74-byte private-properties extent for a T107-shaped
/// 0x50-byte Wizard block, translates both native route points, and removes the
/// one stale T27-only fixup that would otherwise relocate route padding. V2
/// deliberately avoids v1's scene-component growth after that candidate cast,
/// died, sounded normal, and rewarded correctly but never hit Spyro.
/// </summary>
public static class MagicCraftersGreenWizardResidentSwapExporter
{
    public const string RecipeId = "magiccrafters.greenWizard.residentRuntime.inPlaceTargetProps.removeStaleFixup.v2";
    public const string RetiredV1RecipeId = "magiccrafters.greenWizard.residentRuntime.expandedPropsComponent.v1";
    public const string RetiredV1Reason = "DuckStation v1 cast normally, was attackable, died normally, played normal sounds, and dropped its gem, but its lightning never hit Spyro. V2 reuses T27's existing private-properties extent and avoids shifting the scene's pod, collision, and fixup components.";

    private const int WadLba = 37;
    private const int MagicCraftersOverlayEntry = 33;
    private const int MagicCraftersDataEntry = 34;
    private const int RecordStride = 0x58;
    private const int ActorIdOffset = 0x36;
    private const int PodOrGroupOffset = 0x43;
    private const int RendererDistanceOffset = 0x4B;
    private const int SourceVariantOffset = 0x4F;
    private const int StateOffset = 0x51;
    private const int RewardOwnerOffset = 0x52;
    private const int RewardOffset = 0x53;
    private const int XOffset = 0x0C;
    private const int YOffset = 0x10;
    private const int ZOffset = 0x14;
    private const int YawMatrixOffset = 0x20;
    private const int YawMatrixBytes = 0x12;
    private const int YawByteOffset = 0x46;
    private const int CullingSectorOffset = 0x4A;

    private const int ExpectedOverlayBytes = 0x14000;
    private const int ExpectedDataBytes = 0x2BE800;
    private const int ExpectedSceneBase = 0x1CC800;
    private const int ExpectedSceneBytes = 0x12000;
    private const int ExpectedSourceRecordCount = 0x87;
    private const int FocusedTargetTrueIndex = 27;
    private const ushort FocusedTargetActorId = 0x010F;
    private const int NativeWizardTrueIndex = 107;
    private const int NativeWizardPropertiesOffset = 0xF28C;
    private const int NativeWizardPropertiesBytes = 0x50;
    private const int PropertiesComponentHeaderOffset = 0xDF54;
    private const int PropertiesComponentBytes = 0x18B4;
    private const int TargetPropertiesOffset = 0xE9BC;
    private const int TargetPropertiesExtentBytes = 0x74;
    private const int InternalRouteAnchorOffset = 0x28;
    private const int FirstRoutePointOffset = 0x30;
    private const int RoutePointStride = 0x10;
    private const int RoutePointCount = 2;
    private const int NativePodsOffset = 0xF808;
    private const int NativePodsBytes = 0x24;
    private const int NativeCollisionChainOffset = 0xF82C;
    private const int CollisionChainBytes = 0x2004;
    private const int PointerFixupListOffset = 0x11830;
    private const int PointerFixupCountBefore = 0xBE;
    private const int PointerFixupCountAfter = 0xBD;
    private const int RemovedTargetFixup = TargetPropertiesOffset + 0x3C;
    private const int PointerFixupFootprintBytes = sizeof(uint) + (PointerFixupCountBefore * sizeof(uint));
    private const int FinalUsedSceneEnd = PointerFixupListOffset + sizeof(uint) + (PointerFixupCountAfter * sizeof(uint));

    private const int GreenWizardRootSlot = 0x98;
    private const int LightningRootSlot = 0xBC;
    private const uint GreenWizardRoot = 0x1BAFDC;
    private const uint LightningRoot = 0x1C8A54;
    private const ushort GreenWizardActorId = 0x011B;
    private const ushort LightningActorId = 0x0026;
    private const byte NativeWizardRewardOwner = 0x10;
    private const int GreenWizardDispatchPointerOffset = 0x51C;
    private const int LightningDispatchPointerOffset = 0x148;
    private const uint GreenWizardHandlerAddress = 0x800883A0;
    private const uint LightningHandlerAddress = 0x800818FC;
    private const int GreenWizardHandlerOffset = 0xD968;
    private const int LightningHandlerOffset = 0x6EC4;
    private const int HandlerPrefixBytes = 0x18;

    private const string ExpectedOverlaySha256 = "98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d";
    private const string FocusedTargetRecordSha256 = "7e54f1df23673304757bd2b9a7a4e17212b4ca568495a461db45e0e888b1a5e0";
    private const string NativeWizardRecordSha256 = "49b1a1774c189824e8d124dccc677c6228a4da4c307c63346a4efc90ecdc5091";
    private const string NativeWizardPropertiesSha256 = "9f5aea230eb998c43ea354fa74fb189ea0950759decc75c565b6e44e255ce5a5";
    private const string PropertiesComponentPreimageSha256 = "8d78147d04d39c3e2633d0467774fc4212edb9ae14588bac7bd1fbe373027ea2";
    private const string TargetPropertiesExtentPreimageSha256 = "d7e56d7467104802bc4226a20399a8732644881105b51502fdeea744f57f38ee";
    private const string NativePodsSha256 = "1c95086075b3348996304448eda22f71e5ef1117e7d28eb6ccb7a9b158529af9";
    private const string NativeCollisionChainSha256 = "e5b0e80369ae6416ae4a4e144b3b3627142b3a273296da54e595377627901944";
    private const string PointerFixupsPreimageSha256 = "689302cf7e18309f8f864bb36930bc532e4a0df10c9827e2dbdafcadd91e751c";
    private const string CompactedFixupsActiveSha256 = "2fac0d970350db7a3f8436c4531fa4adadcf63a8e36d226441049d83ea9c8b6c";
    private const string CompactedFixupsFootprintSha256 = "48893f21d2748a3d2b77143240538936958d446a74605b119f0dca4041f42ba3";
    private const string FinalZeroTailSha256 = "be35dae855452c2ddbc5fb9264fd7325e2d0fa3392fdf5ef30ac94b9439e7532";
    private const string GreenWizardHandlerPrefixSha256 = "e0e46c281d5ab2b60efaedb59520c39fcf8bac168d5ad1adf038634aea26d6e6";
    private const string LightningHandlerPrefixSha256 = "e786baa4f19b44004f2aef29d9e71cf3a3da2ccdef05ee78918c0a652891e1e0";

    public static MagicCraftersGreenWizardResidentSwapPlan ApplyAndVerify(
        string imagePath,
        LevelDefinition level,
        MobySourcePatchPlan sourcePatchPlan,
        string outputPlanPath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The Magic Crafters Green Wizard base candidate image is missing.", imagePath);
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "magiccrafters", StringComparison.OrdinalIgnoreCase) ||
            level.SourceWadEntry != MagicCraftersDataEntry ||
            level.SourceRecordCount != ExpectedSourceRecordCount)
        {
            throw new InvalidOperationException("The resident Green Wizard completion recipe is mapped only for the checked Magic Crafters source layout.");
        }

        RuntimeBundleCompatibilityResult compatibility = GreenWizardRuntimeBundleCatalog.Evaluate(level.Key);
        LevelRuntimeBundleProfile profile = GreenWizardRuntimeBundleCatalog.FindProfile(level.Key) ??
            throw new InvalidOperationException("The Magic Crafters Green Wizard runtime-bundle profile is missing.");
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
            !string.Equals(LevelCatalog.NormalizeKey(profile.InstanceLayout.PropertiesDonorLevelKey), "magiccrafters", StringComparison.OrdinalIgnoreCase) ||
            profile.InstanceLayout.PropertiesDonorTrueIndex != NativeWizardTrueIndex ||
            profile.InstanceLayout.PropertiesBytes != NativeWizardPropertiesBytes ||
            profile.InstanceLayout.RoutePointCount != RoutePointCount ||
            profile.InstanceLayout.SwapPointerFixupDelta != -1)
        {
            throw new InvalidOperationException("The Magic Crafters Green Wizard profile is not a writable T107-shaped, target-pod-preserving resident recipe.");
        }

        RuntimeBundleResidentPropertiesLayout residentLayout = profile.ResidentDetection?.PropertiesLayout ??
            throw new InvalidOperationException("The Magic Crafters Green Wizard profile is missing its native properties-component evidence.");
        if (residentLayout.ExampleTrueIndex != NativeWizardTrueIndex ||
            residentLayout.PropertiesSceneOffset != NativeWizardPropertiesOffset ||
            residentLayout.PropertiesBytes != NativeWizardPropertiesBytes ||
            residentLayout.InternalRoutePointer != NativeWizardPropertiesOffset + InternalRouteAnchorOffset ||
            residentLayout.RoutePointCount != RoutePointCount ||
            residentLayout.PropertiesComponentHeaderSceneOffset != PropertiesComponentHeaderOffset ||
            residentLayout.PropertiesComponentBytes != PropertiesComponentBytes ||
            residentLayout.PointerFixupListSceneOffset != PointerFixupListOffset ||
            residentLayout.PointerFixupCount != PointerFixupCountBefore ||
            residentLayout.TrailingScenePaddingBytes != ExpectedSceneBytes - (PointerFixupListOffset + sizeof(uint) + (PointerFixupCountBefore * sizeof(uint))))
        {
            throw new InvalidOperationException("The Magic Crafters profile no longer matches the checked in-place properties contract.");
        }

        MobySourcePatch wizardPatch = FindSingleWizardSwap(sourcePatchPlan);
        byte[] beforeRecord = ParseHex(wizardPatch.BeforeHexPreview, RecordStride, "Wizard target record preimage");
        byte[] candidateRecord = ParseHex(wizardPatch.AfterHexPreview, RecordStride, "Wizard candidate record");
        ushort beforeActor = ReadActorId(beforeRecord);
        ushort afterActor = ReadActorId(candidateRecord);
        ValidateSha256(beforeRecord, FocusedTargetRecordSha256, "Magic Crafters focused T27 target row");
        if (wizardPatch.TrueIndex != FocusedTargetTrueIndex ||
            beforeActor != FocusedTargetActorId ||
            afterActor != GreenWizardActorId)
        {
            throw new InvalidDataException(
                $"The first Magic Crafters resident recipe is frozen to T{FocusedTargetTrueIndex} " +
                $"actor 0x{FocusedTargetActorId:X4}->0x{GreenWizardActorId:X4}; " +
                $"the selected T{wizardPatch.TrueIndex} transition was 0x{beforeActor:X4}->0x{afterActor:X4}.");
        }
        if (candidateRecord[RewardOffset] != beforeRecord[RewardOffset])
            throw new InvalidDataException("The Green Wizard candidate did not preserve the selected Magic Crafters enemy's reward byte.");
        foreach (int offset in new[] { XOffset, YOffset, ZOffset })
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(offset, 4)) !=
                BinaryPrimitives.ReadInt32LittleEndian(beforeRecord.AsSpan(offset, 4)))
            {
                throw new InvalidDataException($"The Green Wizard candidate changed the selected Magic Crafters enemy's placement at +0x{offset:X2}.");
            }
        }
        if (!candidateRecord.AsSpan(YawMatrixOffset, YawMatrixBytes).SequenceEqual(beforeRecord.AsSpan(YawMatrixOffset, YawMatrixBytes)) ||
            candidateRecord[YawByteOffset] != beforeRecord[YawByteOffset] ||
            candidateRecord[CullingSectorOffset] != beforeRecord[CullingSectorOffset])
        {
            throw new InvalidDataException("The Green Wizard candidate did not preserve the selected Magic Crafters enemy's yaw and culling sector.");
        }

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        (long overlayBase, int overlayBytes) = ReadWadEntry(stream, layout, MagicCraftersOverlayEntry);
        (long dataBase, int dataBytes) = ReadWadEntry(stream, layout, MagicCraftersDataEntry);
        if (overlayBytes != ExpectedOverlayBytes || dataBytes != ExpectedDataBytes)
        {
            throw new InvalidDataException(
                $"Magic Crafters WAD entry sizes changed: overlay=0x{overlayBytes:X}, data=0x{dataBytes:X}.");
        }

        byte[] overlay = DiscImage.ReadFileBytes(stream, layout, WadLba, overlayBase, overlayBytes);
        ValidateSha256(overlay, ExpectedOverlaySha256, "Magic Crafters native overlay");
        byte[] dataHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, dataBase, 0x20);
        int sceneBase = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x18, 4)));
        int sceneBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x1C, 4)));
        if (sceneBase != ExpectedSceneBase || sceneBytes != ExpectedSceneBytes)
            throw new InvalidDataException($"Magic Crafters scene layout changed: base=0x{sceneBase:X}, size=0x{sceneBytes:X}.");

        long sceneWadOffset = checked(dataBase + sceneBase);
        long tableWadOffset = ParseHexOffset(level.SourceTableWadOffset, "Magic Crafters source table");
        int tableSceneOffset = checked((int)(tableWadOffset - sceneWadOffset));
        int targetPointerFieldSceneOffset = checked(tableSceneOffset + (wizardPatch.TrueIndex * RecordStride));
        if (targetPointerFieldSceneOffset < 0 || targetPointerFieldSceneOffset > sceneBytes - RecordStride)
            throw new InvalidDataException("The selected Magic Crafters source record is outside the checked scene block.");

        ValidateResidentRuntime(stream, layout, overlayBase, dataBase);
        byte[] nativeWizardRecord = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            tableWadOffset + (NativeWizardTrueIndex * RecordStride),
            RecordStride);
        ValidateSha256(nativeWizardRecord, NativeWizardRecordSha256, "Magic Crafters native T107 Green Wizard row");
        if (ReadActorId(nativeWizardRecord) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeWizardRecord) != NativeWizardPropertiesOffset ||
            nativeWizardRecord[RewardOwnerOffset] != NativeWizardRewardOwner)
        {
            throw new InvalidDataException("The checked Magic Crafters T107 native Wizard row changed.");
        }

        byte[] nativeProperties = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + NativeWizardPropertiesOffset,
            NativeWizardPropertiesBytes);
        ValidateSha256(nativeProperties, NativeWizardPropertiesSha256, "Magic Crafters native T107 Green Wizard properties");
        if (BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties) != NativeWizardPropertiesOffset + InternalRouteAnchorOffset ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties.AsSpan(InternalRouteAnchorOffset, 4)) != RoutePointCount ||
            BinaryPrimitives.ReadUInt32LittleEndian(nativeProperties.AsSpan(InternalRouteAnchorOffset + 4, 4)) != 0xFFFF0000)
        {
            throw new InvalidDataException("The checked Magic Crafters T107 Green Wizard properties layout changed.");
        }

        byte[] propertiesComponentBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PropertiesComponentHeaderOffset,
            PropertiesComponentBytes);
        ValidateSha256(propertiesComponentBefore, PropertiesComponentPreimageSha256, "Magic Crafters Moby properties component");
        if (BinaryPrimitives.ReadUInt32LittleEndian(propertiesComponentBefore) != PropertiesComponentBytes)
            throw new InvalidDataException("The Magic Crafters Moby properties component size changed.");

        byte[] targetPropertiesExtentBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + TargetPropertiesOffset,
            TargetPropertiesExtentBytes);
        ValidateSha256(targetPropertiesExtentBefore, TargetPropertiesExtentPreimageSha256, "Magic Crafters T27 private-properties extent");
        if (BinaryPrimitives.ReadUInt32LittleEndian(beforeRecord) != TargetPropertiesOffset ||
            TargetPropertiesExtentBytes < NativeWizardPropertiesBytes)
        {
            throw new InvalidDataException("The checked T27 row no longer owns the required in-place private-properties extent.");
        }

        byte[] nativePods = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + NativePodsOffset,
            NativePodsBytes);
        byte[] nativeCollisionChain = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + NativeCollisionChainOffset,
            CollisionChainBytes);
        ValidateSha256(nativePods, NativePodsSha256, "Magic Crafters pod component");
        ValidateSha256(nativeCollisionChain, NativeCollisionChainSha256, "Magic Crafters collision-chain component");

        byte[] pointerFixupsBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffset,
            PointerFixupFootprintBytes);
        ValidateSha256(pointerFixupsBefore, PointerFixupsPreimageSha256, "Magic Crafters scene pointer-fixup list");
        if (BinaryPrimitives.ReadUInt32LittleEndian(pointerFixupsBefore) != PointerFixupCountBefore)
            throw new InvalidDataException("The Magic Crafters scene pointer-fixup count changed.");
        uint[] fixups = new uint[PointerFixupCountBefore];
        for (int i = 0; i < fixups.Length; i++)
            fixups[i] = BinaryPrimitives.ReadUInt32LittleEndian(pointerFixupsBefore.AsSpan(4 + (i * 4), 4));
        uint[] targetExtentFixups = fixups
            .Where(value => value >= checked((uint)TargetPropertiesOffset) && value < checked((uint)(TargetPropertiesOffset + TargetPropertiesExtentBytes)))
            .ToArray();
        int removedFixupIndex = Array.IndexOf(fixups, checked((uint)RemovedTargetFixup));
        if (fixups.Count(value => value == checked((uint)targetPointerFieldSceneOffset)) != 1 ||
            fixups.Count(value => value == checked((uint)TargetPropertiesOffset)) != 1 ||
            fixups.Count(value => value == checked((uint)RemovedTargetFixup)) != 1 ||
            targetExtentFixups.Length != 2 ||
            !targetExtentFixups.Contains(checked((uint)TargetPropertiesOffset)) ||
            !targetExtentFixups.Contains(checked((uint)RemovedTargetFixup)) ||
            removedFixupIndex < 0)
        {
            throw new InvalidDataException("The T27 row/internal/stale properties fixups no longer match the checked in-place replacement contract.");
        }

        uint[] compactedFixups = fixups.Where((_, index) => index != removedFixupIndex).ToArray();
        if (compactedFixups.Length != PointerFixupCountAfter)
            throw new InvalidDataException("The Magic Crafters stale-fixup removal produced an unexpected count.");
        byte[] pointerFixupsAfter = new byte[PointerFixupFootprintBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(pointerFixupsAfter, PointerFixupCountAfter);
        for (int i = 0; i < compactedFixups.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(pointerFixupsAfter.AsSpan(sizeof(uint) + (i * sizeof(uint)), sizeof(uint)), compactedFixups[i]);
        ValidateSha256(pointerFixupsAfter.AsSpan(0, sizeof(uint) + (PointerFixupCountAfter * sizeof(uint))), CompactedFixupsActiveSha256, "compacted Magic Crafters active pointer-fixup list");
        ValidateSha256(pointerFixupsAfter, CompactedFixupsFootprintSha256, "compacted Magic Crafters pointer-fixup footprint");

        byte[] installedProperties = nativeProperties.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            installedProperties.AsSpan(0, 4),
            TargetPropertiesOffset + InternalRouteAnchorOffset);
        int rawX = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(XOffset, 4));
        int rawY = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(YOffset, 4));
        int rawZ = BinaryPrimitives.ReadInt32LittleEndian(candidateRecord.AsSpan(ZOffset, 4));
        int nativeX = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(XOffset, 4));
        int nativeY = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(YOffset, 4));
        int nativeZ = BinaryPrimitives.ReadInt32LittleEndian(nativeWizardRecord.AsSpan(ZOffset, 4));
        MagicCraftersGreenWizardRoutePoint[] nativeRouteDeltas = new MagicCraftersGreenWizardRoutePoint[RoutePointCount];
        MagicCraftersGreenWizardRoutePoint[] translatedRoutes = new MagicCraftersGreenWizardRoutePoint[RoutePointCount];
        for (int i = 0; i < RoutePointCount; i++)
        {
            int routeOffset = FirstRoutePointOffset + (i * RoutePointStride);
            int nativeRouteX = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(routeOffset, 4));
            int nativeRouteY = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(routeOffset + 4, 4));
            int nativeRouteZ = BinaryPrimitives.ReadInt32LittleEndian(nativeProperties.AsSpan(routeOffset + 8, 4));
            nativeRouteDeltas[i] = new(
                checked(nativeRouteX - nativeX),
                checked(nativeRouteY - nativeY),
                checked(nativeRouteZ - nativeZ));
            translatedRoutes[i] = new(
                checked(rawX + nativeRouteDeltas[i].X),
                checked(rawY + nativeRouteDeltas[i].Y),
                checked(rawZ + nativeRouteDeltas[i].Z));
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(routeOffset, 4), translatedRoutes[i].X);
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(routeOffset + 4, 4), translatedRoutes[i].Y);
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.AsSpan(routeOffset + 8, 4), translatedRoutes[i].Z);
            BinaryPrimitives.WriteUInt32LittleEndian(installedProperties.AsSpan(routeOffset + 12, 4), 0);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(candidateRecord.AsSpan(0, 4), TargetPropertiesOffset);
        candidateRecord[PodOrGroupOffset] = beforeRecord[PodOrGroupOffset];
        candidateRecord[RendererDistanceOffset] = nativeWizardRecord[RendererDistanceOffset];
        candidateRecord[SourceVariantOffset] = nativeWizardRecord[SourceVariantOffset];
        candidateRecord[StateOffset] = nativeWizardRecord[StateOffset];
        candidateRecord[RewardOwnerOffset] = NativeWizardRewardOwner;
        candidateRecord[RewardOffset] = beforeRecord[RewardOffset];
        candidateRecord[YawByteOffset] = beforeRecord[YawByteOffset];

        byte[] targetPropertiesExtentAfter = targetPropertiesExtentBefore.ToArray();
        installedProperties.CopyTo(targetPropertiesExtentAfter, 0);
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
        DiscImage.WriteFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffset,
            pointerFixupsAfter);
        stream.Flush(true);

        VerifyReadback(
            stream,
            layout,
            overlayBase,
            dataBase,
            tableWadOffset,
            sceneWadOffset,
            wizardPatch.TrueIndex,
            beforeRecord[RewardOffset],
            beforeRecord[PodOrGroupOffset],
            beforeRecord[YawByteOffset],
            translatedRoutes,
            installedProperties,
            targetPropertiesExtentBefore,
            nativePods,
            nativeCollisionChain,
            pointerFixupsAfter);

        MagicCraftersGreenWizardResidentSwapPlan plan = new(
            RecipeId,
            RetiredV1RecipeId,
            RetiredV1Reason,
            GreenWizardRuntimeBundleCatalog.Manifest.BundleId,
            GreenWizardRuntimeBundleCatalog.Manifest.Fingerprint,
            profile.Fingerprint,
            imagePath,
            Sha256File(imagePath),
            level.Key,
            wizardPatch.TrueIndex,
            beforeActor,
            beforeRecord[RewardOffset],
            beforeRecord[YawByteOffset],
            tableWadOffset,
            sceneBase,
            sceneBytes,
            targetPointerFieldSceneOffset,
            PropertiesComponentHeaderOffset,
            PropertiesComponentBytes,
            PropertiesComponentPreimageSha256,
            TargetPropertiesOffset,
            TargetPropertiesExtentBytes,
            TargetPropertiesExtentPreimageSha256,
            Sha256(targetPropertiesExtentAfter),
            NativeWizardPropertiesBytes,
            NativeWizardPropertiesSha256,
            Sha256(installedProperties),
            Sha256(targetPropertiesExtentBefore.AsSpan(NativeWizardPropertiesBytes)),
            beforeRecord[PodOrGroupOffset],
            nativeRouteDeltas,
            translatedRoutes,
            PointerFixupListOffset,
            PointerFixupCountBefore,
            PointerFixupCountAfter,
            RemovedTargetFixup,
            removedFixupIndex,
            PointerFixupsPreimageSha256,
            CompactedFixupsActiveSha256,
            CompactedFixupsFootprintSha256,
            GreenWizardRoot,
            LightningRoot,
            GreenWizardHandlerAddress,
            LightningHandlerAddress,
            [
                "Magic Crafters' native actor 0x011B and lightning actor 0x0026 roots, dispatch pointers, and handler prefixes remain unchanged.",
                "The selected enemy's source row, placement, pod/group, culling sector, yaw, and reward remain in the same source slot.",
                "T27's unique checked 0x74-byte properties extent at 0xE9BC receives the exact 0x50-byte T107 Wizard layout in place; its trailing 0x24 bytes remain byte-identical.",
                "Both T107 route points are translated from T27's spawn, while the scene properties size, pod component, collision-chain component, and every later scene offset remain unchanged.",
                "The obsolete T27-only fixup at 0xE9F8 is removed because that word becomes Wizard route padding; the existing row-pointer and internal-route-pointer fixups remain.",
                "Final image readback verifies the row, in-place private properties, translated routes, byte-identical pod/collision payloads, native runtime dependencies, compacted 0xBE-to-0xBD fixup list, and zero tail."
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
                $"The guarded Magic Crafters resident recipe supports exactly one Green Wizard slot swap per candidate; found {matches.Length}.");
        }

        return matches[0];
    }

    private static void ValidateResidentRuntime(FileStream stream, DiscLayout layout, long overlayBase, long dataBase)
    {
        ValidateWord(stream, layout, overlayBase + GreenWizardDispatchPointerOffset, GreenWizardHandlerAddress, "Magic Crafters Green Wizard dispatch pointer");
        ValidateWord(stream, layout, overlayBase + LightningDispatchPointerOffset, LightningHandlerAddress, "Magic Crafters lightning dispatch pointer");
        ValidateWord(stream, layout, dataBase + GreenWizardRootSlot, GreenWizardRoot, "Magic Crafters Green Wizard actor root");
        ValidateWord(stream, layout, dataBase + LightningRootSlot, LightningRoot, "Magic Crafters lightning actor root");
        ValidateActorId(stream, layout, dataBase, GreenWizardRootSlot, GreenWizardActorId);
        ValidateActorId(stream, layout, dataBase, LightningRootSlot, LightningActorId);
        ValidateSha256(
            DiscImage.ReadFileBytes(stream, layout, WadLba, overlayBase + GreenWizardHandlerOffset, HandlerPrefixBytes),
            GreenWizardHandlerPrefixSha256,
            "Magic Crafters Green Wizard handler prefix");
        ValidateSha256(
            DiscImage.ReadFileBytes(stream, layout, WadLba, overlayBase + LightningHandlerOffset, HandlerPrefixBytes),
            LightningHandlerPrefixSha256,
            "Magic Crafters lightning handler prefix");
    }

    private static void VerifyReadback(
        FileStream stream,
        DiscLayout layout,
        long overlayBase,
        long dataBase,
        long tableWadOffset,
        long sceneWadOffset,
        int trueIndex,
        byte reward,
        byte podOrGroup,
        byte yaw,
        IReadOnlyList<MagicCraftersGreenWizardRoutePoint> translatedRoutes,
        byte[] expectedProperties,
        byte[] originalTargetPropertiesExtent,
        byte[] expectedPods,
        byte[] expectedCollisionChain,
        byte[] expectedPointerFixups)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
        if (ReadActorId(row) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(row) != TargetPropertiesOffset ||
            row[PodOrGroupOffset] != podOrGroup ||
            row[RendererDistanceOffset] != 0x09 ||
            row[SourceVariantOffset] != 0x00 ||
            row[StateOffset] != 0x00 ||
            row[YawByteOffset] != yaw ||
            row[RewardOwnerOffset] != NativeWizardRewardOwner ||
            row[RewardOffset] != reward)
        {
            throw new InvalidDataException("The final Magic Crafters Green Wizard source row failed readback.");
        }

        byte[] targetPropertiesExtent = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + TargetPropertiesOffset,
            TargetPropertiesExtentBytes);
        byte[] properties = targetPropertiesExtent.AsSpan(0, NativeWizardPropertiesBytes).ToArray();
        if (!properties.AsSpan().SequenceEqual(expectedProperties) ||
            BinaryPrimitives.ReadUInt32LittleEndian(properties) != TargetPropertiesOffset + InternalRouteAnchorOffset ||
            !targetPropertiesExtent.AsSpan(NativeWizardPropertiesBytes)
                .SequenceEqual(originalTargetPropertiesExtent.AsSpan(NativeWizardPropertiesBytes)))
        {
            throw new InvalidDataException("The final in-place Magic Crafters Green Wizard properties or preserved tail failed readback.");
        }
        for (int i = 0; i < translatedRoutes.Count; i++)
        {
            int routeOffset = FirstRoutePointOffset + (i * RoutePointStride);
            MagicCraftersGreenWizardRoutePoint route = translatedRoutes[i];
            if (BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(routeOffset, 4)) != route.X ||
                BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(routeOffset + 4, 4)) != route.Y ||
                BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(routeOffset + 8, 4)) != route.Z ||
                BinaryPrimitives.ReadUInt32LittleEndian(properties.AsSpan(routeOffset + 12, 4)) != 0)
            {
                throw new InvalidDataException($"The final Magic Crafters Green Wizard route {i} failed readback.");
            }
        }

        uint propertiesComponentBytes = BinaryPrimitives.ReadUInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + PropertiesComponentHeaderOffset, sizeof(uint)));
        byte[] finalPods = DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + NativePodsOffset, expectedPods.Length);
        byte[] finalCollisionChain = DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + NativeCollisionChainOffset, expectedCollisionChain.Length);
        if (propertiesComponentBytes != PropertiesComponentBytes ||
            !finalPods.AsSpan().SequenceEqual(expectedPods) ||
            !finalCollisionChain.AsSpan().SequenceEqual(expectedCollisionChain))
        {
            throw new InvalidDataException("The in-place Magic Crafters recipe changed the properties size or native pod/collision components.");
        }

        byte[] finalFixups = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffset,
            PointerFixupFootprintBytes);
        ValidateSha256(finalFixups.AsSpan(0, sizeof(uint) + (PointerFixupCountAfter * sizeof(uint))), CompactedFixupsActiveSha256, "final Magic Crafters active pointer-fixup list");
        ValidateSha256(finalFixups, CompactedFixupsFootprintSha256, "final Magic Crafters pointer-fixup footprint");
        uint fixupCount = BinaryPrimitives.ReadUInt32LittleEndian(finalFixups);
        uint[] activeFixups = new uint[PointerFixupCountAfter];
        for (int i = 0; i < activeFixups.Length; i++)
            activeFixups[i] = BinaryPrimitives.ReadUInt32LittleEndian(finalFixups.AsSpan(sizeof(uint) + (i * sizeof(uint)), sizeof(uint)));
        if (!finalFixups.AsSpan().SequenceEqual(expectedPointerFixups) ||
            fixupCount != PointerFixupCountAfter ||
            activeFixups.Contains(checked((uint)RemovedTargetFixup)) ||
            activeFixups.Count(value => value == checked((uint)TargetPropertiesOffset)) != 1)
        {
            throw new InvalidDataException("The final Magic Crafters stale-fixup removal failed readback.");
        }

        byte[] finalZeroTail = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + FinalUsedSceneEnd,
            ExpectedSceneBytes - FinalUsedSceneEnd);
        ValidateSha256(finalZeroTail, FinalZeroTailSha256, "Magic Crafters zero tail after in-place fixup compaction");
        ValidateResidentRuntime(stream, layout, overlayBase, dataBase);
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
            throw new InvalidDataException($"Magic Crafters root slot 0x{rootSlot:X} expected actor 0x{expected:X4}, found 0x{actual:X4}.");
    }

    private static void ValidateWord(FileStream stream, DiscLayout layout, long wadOffset, uint expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, sizeof(uint)));
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

public sealed record MagicCraftersGreenWizardResidentSwapPlan(
    string RecipeId,
    string RetiredRecipeId,
    string RetiredRecipeReason,
    string BundleId,
    string BundleFingerprint,
    string ProfileFingerprint,
    string OutputImagePath,
    string OutputImageSha256,
    string TargetLevelKey,
    int TargetTrueIndex,
    ushort ReplacedActorId,
    int PreservedReward,
    byte PreservedYaw,
    long SourceTableWadOffset,
    int SceneBaseOffset,
    int SceneBytes,
    int PropertiesPointerFieldSceneOffset,
    int PropertiesComponentHeaderSceneOffset,
    int PropertiesComponentBytes,
    string PropertiesComponentPreimageSha256,
    int PropertiesSceneOffset,
    int PropertiesExtentBytes,
    string PropertiesExtentPreimageSha256,
    string PropertiesExtentInstalledSha256,
    int PropertiesBytes,
    string NativePropertiesSha256,
    string InstalledPropertiesSha256,
    string PreservedPropertiesTailSha256,
    byte PreservedPodOrGroup,
    IReadOnlyList<MagicCraftersGreenWizardRoutePoint> NativeRouteDeltas,
    IReadOnlyList<MagicCraftersGreenWizardRoutePoint> RoutePoints,
    int PointerFixupListSceneOffset,
    int PointerFixupCountBefore,
    int PointerFixupCountAfter,
    int RemovedFixupSceneOffset,
    int RemovedFixupIndex,
    string PointerFixupsPreimageSha256,
    string PointerFixupsActiveSha256,
    string PointerFixupsFootprintSha256,
    uint GreenWizardRoot,
    uint LightningRoot,
    uint GreenWizardHandlerAddress,
    uint LightningHandlerAddress,
    IReadOnlyList<string> FocusedChecks);

public sealed record MagicCraftersGreenWizardRoutePoint(int X, int Y, int Z);
