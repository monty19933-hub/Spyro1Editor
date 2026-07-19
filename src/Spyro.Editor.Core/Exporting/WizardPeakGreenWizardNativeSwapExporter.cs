using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Installs the native Wizard Peak T6 Green Wizard instance shape into the
/// checked Elder Wizard T24 slot. Wizard Peak already owns the complete actor
/// 0x011B/0x0026 runtime closure, so this candidate changes only the target row,
/// its unique private-properties extent, and one scene pointer-fixup entry.
/// </summary>
public static class WizardPeakGreenWizardNativeSwapExporter
{
    public const string RecipeId = "wizardpeak.greenWizard.nativeRuntime.inPlaceT24Props.podMatched.v3";
    public const string RetiredV2RecipeId = "wizardpeak.greenWizard.nativeRuntime.inPlaceT10Props.replaceStaleFixup.donorPodGroup.v2";
    public const string RetiredV2Reason =
        "Retired by live DuckStation evidence: the T10 replacement remained visible, killable, and gem-paying after switching to detached Green Wizard pod/group 0xFF, but it still never used the lightning attack.";
    public const string RetiredV1RecipeId = "wizardpeak.greenWizard.nativeRuntime.inPlaceT10Props.replaceStaleFixup.v1";
    public const string RetiredV1Reason =
        "Retired by live DuckStation evidence: the T10 replacement was visible, killable, and dropped its gem, but never used the lightning attack. V1 preserved Elder Wizard group 0x03 even though the proven T6 Green Wizard template uses detached pod/group 0xFF.";

    private const int WadLba = 37;
    private const int OverlayEntry = 39;
    private const int DataEntry = 40;
    private const int ExpectedOverlayBytes = 0x10000;
    private const int ExpectedDataBytes = 0x28A000;
    private const int ExpectedSceneBase = 0x1CE800;
    private const int ExpectedSceneBytes = 0x11800;
    private const int ExpectedSourceRecordCount = 165;

    private const int RecordStride = 0x58;
    private const int ActorIdOffset = 0x36;
    private const int XOffset = 0x0C;
    private const int YOffset = 0x10;
    private const int ZOffset = 0x14;
    private const int YawMatrixOffset = 0x20;
    private const int YawMatrixBytes = 0x12;
    private const int PodOrGroupOffset = 0x43;
    private const int YawByteOffset = 0x46;
    private const int CullingSectorOffset = 0x4A;
    private const int RendererDistanceOffset = 0x4B;
    private const int SourceVariantOffset = 0x4F;
    private const int StateOffset = 0x51;
    private const int RewardOwnerOffset = 0x52;
    private const int RewardOffset = 0x53;

    private const int DonorTrueIndex = 6;
    private const int TargetTrueIndex = 24;
    private const ushort GreenWizardActorId = 0x011B;
    private const ushort LightningActorId = 0x0026;
    private const ushort TargetActorId = 0x011D;
    private const int DonorPropertiesOffset = 0xC350;
    private const int TargetPropertiesOffset = 0xCAC0;
    private const int PropertiesBytes = 0x50;
    private const int TargetPropertiesExtentBytes = 0x74;
    private const int ExpectedTargetPointerFieldSceneOffset = 0x69B8;
    private const int ExpectedTargetX = 39670;
    private const int ExpectedTargetY = 64655;
    private const int ExpectedTargetZ = 27648;
    private const byte ExpectedTargetPodOrGroup = 0xFF;
    private const byte ExpectedTargetYaw = 0x2B;
    private const byte ExpectedTargetReward = 0x56;
    private const int InternalRouteAnchorOffset = 0x28;
    private const int FirstRoutePointOffset = 0x30;
    private const int RoutePointStride = 0x10;
    private const int RoutePointCount = 2;
    private const int PropertiesComponentHeaderOffset = 0xC230;
    private const int PropertiesComponentBytes = 0x19D4;
    private const int PointerFixupListOffset = 0xFC3C;
    private const int PointerFixupCount = 0xBF;
    private const int StaleTargetFixup = 0xCACC;
    private const int ReplacementTargetFixup = TargetPropertiesOffset;
    private const int PointerFixupFootprintBytes = sizeof(uint) + (PointerFixupCount * sizeof(uint));

    private const int GreenWizardRootSlot = 0x64;
    private const int LightningRootSlot = 0xB4;
    private const uint GreenWizardRoot = 0x19B518;
    private const uint LightningRoot = 0x1CAC88;

    private const string OverlaySha256 = "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1";
    private const string DonorRecordSha256 = "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82";
    private const string TargetRecordSha256 = "a1329f4083a39c77093444bdf04a2ac6ff2cddbc9d419a0bfcd95170ac67efa8";
    private const string DonorPropertiesSha256 = "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b";
    private const string TargetExtentPreimageSha256 = "65c13bb4a309a704fc8a25dbe937c28d0703b435b450a1fc547129fc64238d9c";
    private const string TargetTailSha256 = "61d27d96f7207afaf4be5485b50b01c928fb8bacfd929e7ec7f0438dfc0d2543";
    private const string PropertiesComponentSha256 = "7365fe2ed03d5fef789421da434d680261e0d448d357eca1506a805df9a839b6";
    private const string PointerFixupsPreimageSha256 = "32c53f20808f766116691be735b377f8dd83ae3a626800a9433ea181c583d8bd";
    private const string InstalledPropertiesSha256 = "52f747a5aa5c2c4beff2813aa8722c876d881dff77a564b669ffb24d8fef732f";
    private const string InstalledExtentSha256 = "0aef88ee09413e5ef08f685049698cca6b3e07846d1bafaf34cbf8627936ad73";
    private const string PointerFixupsInstalledSha256 = "24a9a1e0d22e8b10b480ad1f71fd3b92c234533e43daf0b1d4d238430ea87e5f";
    private const string InstalledRecordSha256 = "4316b75d1f06c73b8a49b545fcfe838dcf91e1b9a38a8cd16936c4371a6a2177";

    public static WizardPeakGreenWizardNativeSwapPlan ApplyAndVerify(
        string imagePath,
        LevelDefinition level,
        MobySourcePatchPlan sourcePatchPlan,
        string outputPlanPath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The Wizard Peak Green Wizard candidate image is missing.", imagePath);
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "wizardpeak", StringComparison.OrdinalIgnoreCase) ||
            level.SourceWadEntry != DataEntry ||
            level.SourceRecordCount != ExpectedSourceRecordCount)
        {
            throw new InvalidOperationException("The native Green Wizard in-place recipe is mapped only for the checked Wizard Peak source layout.");
        }

        LevelRuntimeBundleProfile profile = GreenWizardRuntimeBundleCatalog.FindProfile(level.Key) ??
            throw new InvalidOperationException("Wizard Peak has no Green Wizard runtime profile.");
        RuntimeBundleCompatibilityResult compatibility = GreenWizardRuntimeBundleCatalog.Evaluate(level.Key);
        if (profile.Deployment != RuntimeBundleDeploymentKind.Native ||
            profile.Evidence != RuntimeBundleEvidenceKind.RuntimeProven ||
            compatibility.Status != RuntimeBundleCompatibilityStatus.Ready ||
            !compatibility.NormalCreateBinReady ||
            compatibility.RequiresRuntimeSmoke ||
            !string.Equals(profile.RuntimeProofRecipeId, RecipeId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(profile.RuntimeProofOutputSha256) ||
            profile.InstanceLayout.PropertiesDonorTrueIndex != DonorTrueIndex ||
            profile.InstanceLayout.PropertiesBytes != PropertiesBytes ||
            profile.InstanceLayout.RoutePointCount != RoutePointCount ||
            profile.InstanceLayout.SwapPointerFixupDelta != 0 ||
            profile.InstanceLayout.PreserveTargetPodOrGroup ||
            !profile.InstanceLayout.TranslateDonorRouteFromSpawn)
        {
            throw new InvalidOperationException("Wizard Peak's native runtime profile no longer matches the runtime-proven T6-to-T24 pod-matched contract.");
        }

        MobySourcePatch patch = FindSingleWizardSwap(sourcePatchPlan);
        byte[] beforeRecord = ParseHex(patch.BeforeHexPreview, RecordStride, "Wizard Peak T24 preimage");
        byte[] candidateRecord = ParseHex(patch.AfterHexPreview, RecordStride, "Wizard Peak T24 candidate row");
        ValidateSha256(beforeRecord, TargetRecordSha256, "Wizard Peak T24 Elder Wizard row");
        if (patch.TrueIndex != TargetTrueIndex || ReadActorId(beforeRecord) != TargetActorId || ReadActorId(candidateRecord) != GreenWizardActorId)
        {
            throw new InvalidDataException(
                $"The first Wizard Peak native Green Wizard recipe is frozen to T{TargetTrueIndex} actor 0x{TargetActorId:X4}->0x{GreenWizardActorId:X4}.");
        }
        if (BinaryPrimitives.ReadInt32LittleEndian(beforeRecord.AsSpan(XOffset, 4)) != ExpectedTargetX ||
            BinaryPrimitives.ReadInt32LittleEndian(beforeRecord.AsSpan(YOffset, 4)) != ExpectedTargetY ||
            BinaryPrimitives.ReadInt32LittleEndian(beforeRecord.AsSpan(ZOffset, 4)) != ExpectedTargetZ ||
            beforeRecord[PodOrGroupOffset] != ExpectedTargetPodOrGroup ||
            beforeRecord[YawByteOffset] != ExpectedTargetYaw ||
            beforeRecord[RewardOffset] != ExpectedTargetReward)
        {
            throw new InvalidDataException("Wizard Peak T24 placement, native pod/group, yaw, or reward changed from the checked fallback target.");
        }
        ValidatePreservedTargetRowFields(beforeRecord, candidateRecord);

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        (long overlayBase, int overlayBytes) = ReadWadEntry(stream, layout, OverlayEntry);
        (long dataBase, int dataBytes) = ReadWadEntry(stream, layout, DataEntry);
        if (overlayBytes != ExpectedOverlayBytes || dataBytes != ExpectedDataBytes)
            throw new InvalidDataException($"Wizard Peak WAD entry sizes changed: overlay=0x{overlayBytes:X}, data=0x{dataBytes:X}.");
        ValidateSha256(
            DiscImage.ReadFileBytes(stream, layout, WadLba, overlayBase, overlayBytes),
            OverlaySha256,
            "Wizard Peak native behavior overlay");

        byte[] dataHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, dataBase, 0x20);
        int sceneBase = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x18, 4)));
        int sceneBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dataHeader.AsSpan(0x1C, 4)));
        if (sceneBase != ExpectedSceneBase || sceneBytes != ExpectedSceneBytes)
            throw new InvalidDataException($"Wizard Peak scene layout changed: base=0x{sceneBase:X}, size=0x{sceneBytes:X}.");

        long sceneWadOffset = checked(dataBase + sceneBase);
        long tableWadOffset = ParseHexOffset(level.SourceTableWadOffset, "Wizard Peak source table");
        int tableSceneOffset = checked((int)(tableWadOffset - sceneWadOffset));
        int targetPointerFieldSceneOffset = checked(tableSceneOffset + (TargetTrueIndex * RecordStride));
        if (targetPointerFieldSceneOffset != ExpectedTargetPointerFieldSceneOffset)
            throw new InvalidDataException($"Wizard Peak T24 row pointer field expected scene offset 0x{ExpectedTargetPointerFieldSceneOffset:X}, found 0x{targetPointerFieldSceneOffset:X}.");
        ValidateNativeRoots(stream, layout, dataBase);

        byte[] donorRecord = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            tableWadOffset + (DonorTrueIndex * RecordStride),
            RecordStride);
        ValidateSha256(donorRecord, DonorRecordSha256, "Wizard Peak T6 native Green Wizard row");
        if (ReadActorId(donorRecord) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(donorRecord) != DonorPropertiesOffset)
        {
            throw new InvalidDataException("Wizard Peak T6 is no longer the checked native Green Wizard donor.");
        }

        byte[] donorProperties = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + DonorPropertiesOffset,
            PropertiesBytes);
        ValidateSha256(donorProperties, DonorPropertiesSha256, "Wizard Peak T6 Green Wizard properties");
        if (BinaryPrimitives.ReadUInt32LittleEndian(donorProperties) != DonorPropertiesOffset + InternalRouteAnchorOffset ||
            BinaryPrimitives.ReadUInt32LittleEndian(donorProperties.AsSpan(InternalRouteAnchorOffset, 4)) != RoutePointCount)
        {
            throw new InvalidDataException("Wizard Peak T6 properties no longer use the checked two-point route layout.");
        }

        byte[] propertiesComponent = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PropertiesComponentHeaderOffset,
            PropertiesComponentBytes);
        ValidateSha256(propertiesComponent, PropertiesComponentSha256, "Wizard Peak properties component");
        if (BinaryPrimitives.ReadUInt32LittleEndian(propertiesComponent) != PropertiesComponentBytes)
            throw new InvalidDataException("Wizard Peak properties-component size changed.");

        byte[] targetExtentBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + TargetPropertiesOffset,
            TargetPropertiesExtentBytes);
        ValidateSha256(targetExtentBefore, TargetExtentPreimageSha256, "Wizard Peak T24 private-properties extent");
        ValidateSha256(targetExtentBefore.AsSpan(PropertiesBytes), TargetTailSha256, "Wizard Peak T24 preserved properties tail");
        if (BinaryPrimitives.ReadUInt32LittleEndian(beforeRecord) != TargetPropertiesOffset)
            throw new InvalidDataException("Wizard Peak T24 no longer points at the checked private-properties extent.");

        byte[] fixupsBefore = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            sceneWadOffset + PointerFixupListOffset,
            PointerFixupFootprintBytes);
        ValidateSha256(fixupsBefore, PointerFixupsPreimageSha256, "Wizard Peak pointer-fixup list");
        if (BinaryPrimitives.ReadUInt32LittleEndian(fixupsBefore) != PointerFixupCount)
            throw new InvalidDataException("Wizard Peak pointer-fixup count changed.");
        uint[] fixups = ReadFixups(fixupsBefore, PointerFixupCount);
        int staleFixupIndex = Array.IndexOf(fixups, checked((uint)StaleTargetFixup));
        if (staleFixupIndex != 37 ||
            fixups.Count(value => value == checked((uint)StaleTargetFixup)) != 1 ||
            fixups.Contains(checked((uint)ReplacementTargetFixup)) ||
            fixups.Count(value => value == checked((uint)targetPointerFieldSceneOffset)) != 1)
        {
            throw new InvalidDataException("Wizard Peak T24's row, stale private pointer, or replacement fixup contract changed.");
        }

        byte[] installedProperties = donorProperties.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(installedProperties, TargetPropertiesOffset + InternalRouteAnchorOffset);
        WizardPeakGreenWizardRoutePoint[] translatedRoutes = TranslateRoutes(
            donorRecord,
            donorProperties,
            candidateRecord,
            installedProperties);
        ValidateSha256(installedProperties, InstalledPropertiesSha256, "Wizard Peak installed T24 Green Wizard properties");

        byte[] installedExtent = targetExtentBefore.ToArray();
        installedProperties.CopyTo(installedExtent, 0);
        ValidateSha256(installedExtent, InstalledExtentSha256, "Wizard Peak installed T24 properties extent");

        byte[] fixupsAfter = fixupsBefore.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            fixupsAfter.AsSpan(sizeof(uint) + (staleFixupIndex * sizeof(uint)), sizeof(uint)),
            ReplacementTargetFixup);
        ValidateSha256(fixupsAfter, PointerFixupsInstalledSha256, "Wizard Peak installed pointer-fixup list");

        BinaryPrimitives.WriteUInt32LittleEndian(candidateRecord, TargetPropertiesOffset);
        byte donorPodOrGroup = GreenWizardRuntimeBundleCatalog.Manifest.InstanceSchema.PodOrGroupValue;
        if (donorRecord[PodOrGroupOffset] != donorPodOrGroup)
            throw new InvalidDataException($"Wizard Peak T6 pod/group expected 0x{donorPodOrGroup:X2}, found 0x{donorRecord[PodOrGroupOffset]:X2}.");
        candidateRecord[PodOrGroupOffset] = donorPodOrGroup;
        candidateRecord[RendererDistanceOffset] = donorRecord[RendererDistanceOffset];
        candidateRecord[SourceVariantOffset] = donorRecord[SourceVariantOffset];
        candidateRecord[StateOffset] = donorRecord[StateOffset];
        candidateRecord[RewardOwnerOffset] = donorRecord[RewardOwnerOffset];
        candidateRecord[RewardOffset] = beforeRecord[RewardOffset];
        candidateRecord[YawByteOffset] = beforeRecord[YawByteOffset];
        ValidateSha256(candidateRecord, InstalledRecordSha256, "Wizard Peak installed T24 Green Wizard row");

        DiscImage.WriteFileBytes(stream, layout, WadLba, sceneWadOffset + TargetPropertiesOffset, installedProperties);
        DiscImage.WriteFileBytes(stream, layout, WadLba, tableWadOffset + (TargetTrueIndex * RecordStride), candidateRecord);
        DiscImage.WriteFileBytes(stream, layout, WadLba, sceneWadOffset + PointerFixupListOffset, fixupsAfter);
        stream.Flush(true);

        VerifyReadback(
            stream,
            layout,
            dataBase,
            sceneWadOffset,
            tableWadOffset,
            beforeRecord,
            candidateRecord,
            installedExtent,
            fixupsAfter,
            translatedRoutes);

        WizardPeakGreenWizardNativeSwapPlan plan = new(
            RecipeId,
            RetiredV2RecipeId,
            RetiredV2Reason,
            GreenWizardRuntimeBundleCatalog.Manifest.BundleId,
            GreenWizardRuntimeBundleCatalog.Manifest.Fingerprint,
            profile.Fingerprint,
            imagePath,
            Sha256File(imagePath),
            level.Key,
            DonorTrueIndex,
            TargetTrueIndex,
            TargetActorId,
            beforeRecord[PodOrGroupOffset],
            donorPodOrGroup,
            beforeRecord[YawByteOffset],
            beforeRecord[RewardOffset],
            sceneBase,
            sceneBytes,
            TargetPropertiesOffset,
            TargetPropertiesExtentBytes,
            Sha256(installedProperties),
            Sha256(installedExtent),
            translatedRoutes,
            PointerFixupListOffset,
            PointerFixupCount,
            StaleTargetFixup,
            ReplacementTargetFixup,
            staleFixupIndex,
            Sha256(fixupsAfter),
            [
                new(RetiredV1RecipeId, RetiredV1Reason, "Visible, killable, one gem, no lightning attack."),
                new(RetiredV2RecipeId, RetiredV2Reason, "Visible, killable, one gem, no lightning attack after pod/group 0xFF change.")
            ],
            [
                "Wizard Peak's native actor 0x011B Green Wizard, actor 0x0026 lightning, overlay, packages, particles, and textures remain unchanged.",
                "T24 retains its placement, yaw matrix, culling sector, pod/group 0xFF, and gem reward; unlike retired T10 candidates, its native pod/group already matches every detached Wizard row.",
                "T6's 0x50-byte two-point Wizard properties are installed inside T24's unique 0x74-byte extent; the trailing 0x24 bytes remain unchanged.",
                "The stale T24 +0x0C fixup is replaced in place by the new internal route-pointer fixup, keeping the 0xBF count and every scene component offset unchanged.",
                "Final-image readback verifies the row, properties, translated routes, fixed pointer list, and native runtime roots."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        return plan;
    }

    private static void ValidatePreservedTargetRowFields(ReadOnlySpan<byte> before, ReadOnlySpan<byte> candidate)
    {
        foreach (int offset in new[] { XOffset, YOffset, ZOffset })
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(candidate.Slice(offset, 4)) !=
                BinaryPrimitives.ReadInt32LittleEndian(before.Slice(offset, 4)))
            {
                throw new InvalidDataException($"The Wizard Peak candidate changed T24 placement at +0x{offset:X2}.");
            }
        }
        if (!candidate.Slice(YawMatrixOffset, YawMatrixBytes).SequenceEqual(before.Slice(YawMatrixOffset, YawMatrixBytes)) ||
            candidate[YawByteOffset] != before[YawByteOffset] ||
            candidate[CullingSectorOffset] != before[CullingSectorOffset] ||
            candidate[RewardOffset] != before[RewardOffset])
        {
            throw new InvalidDataException("The Wizard Peak candidate did not preserve T24 yaw, culling sector, or reward.");
        }
    }

    private static WizardPeakGreenWizardRoutePoint[] TranslateRoutes(
        ReadOnlySpan<byte> donorRecord,
        ReadOnlySpan<byte> donorProperties,
        ReadOnlySpan<byte> targetRecord,
        Span<byte> installedProperties)
    {
        int donorX = BinaryPrimitives.ReadInt32LittleEndian(donorRecord.Slice(XOffset, 4));
        int donorY = BinaryPrimitives.ReadInt32LittleEndian(donorRecord.Slice(YOffset, 4));
        int donorZ = BinaryPrimitives.ReadInt32LittleEndian(donorRecord.Slice(ZOffset, 4));
        int targetX = BinaryPrimitives.ReadInt32LittleEndian(targetRecord.Slice(XOffset, 4));
        int targetY = BinaryPrimitives.ReadInt32LittleEndian(targetRecord.Slice(YOffset, 4));
        int targetZ = BinaryPrimitives.ReadInt32LittleEndian(targetRecord.Slice(ZOffset, 4));
        WizardPeakGreenWizardRoutePoint[] result = new WizardPeakGreenWizardRoutePoint[RoutePointCount];
        for (int i = 0; i < result.Length; i++)
        {
            int offset = FirstRoutePointOffset + (i * RoutePointStride);
            int x = checked(targetX + BinaryPrimitives.ReadInt32LittleEndian(donorProperties.Slice(offset, 4)) - donorX);
            int y = checked(targetY + BinaryPrimitives.ReadInt32LittleEndian(donorProperties.Slice(offset + 4, 4)) - donorY);
            int z = checked(targetZ + BinaryPrimitives.ReadInt32LittleEndian(donorProperties.Slice(offset + 8, 4)) - donorZ);
            result[i] = new(x, y, z);
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.Slice(offset, 4), x);
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.Slice(offset + 4, 4), y);
            BinaryPrimitives.WriteInt32LittleEndian(installedProperties.Slice(offset + 8, 4), z);
            BinaryPrimitives.WriteUInt32LittleEndian(installedProperties.Slice(offset + 12, 4), 0);
        }
        return result;
    }

    private static void VerifyReadback(
        FileStream stream,
        DiscLayout layout,
        long dataBase,
        long sceneWadOffset,
        long tableWadOffset,
        ReadOnlySpan<byte> originalTargetRecord,
        ReadOnlySpan<byte> expectedRow,
        ReadOnlySpan<byte> expectedExtent,
        ReadOnlySpan<byte> expectedFixups,
        IReadOnlyList<WizardPeakGreenWizardRoutePoint> routes)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + (TargetTrueIndex * RecordStride), RecordStride);
        byte[] extent = DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + TargetPropertiesOffset, TargetPropertiesExtentBytes);
        byte[] fixups = DiscImage.ReadFileBytes(stream, layout, WadLba, sceneWadOffset + PointerFixupListOffset, PointerFixupFootprintBytes);
        if (!row.AsSpan().SequenceEqual(expectedRow) ||
            !extent.AsSpan().SequenceEqual(expectedExtent) ||
            !fixups.AsSpan().SequenceEqual(expectedFixups) ||
            ReadActorId(row) != GreenWizardActorId ||
            BinaryPrimitives.ReadUInt32LittleEndian(row) != TargetPropertiesOffset ||
            row[PodOrGroupOffset] != GreenWizardRuntimeBundleCatalog.Manifest.InstanceSchema.PodOrGroupValue ||
            row[YawByteOffset] != originalTargetRecord[YawByteOffset] ||
            row[RewardOffset] != originalTargetRecord[RewardOffset])
        {
            throw new InvalidDataException("The final Wizard Peak T24 Green Wizard row, properties extent, or fixup list failed readback.");
        }
        for (int i = 0; i < routes.Count; i++)
        {
            int offset = FirstRoutePointOffset + (i * RoutePointStride);
            if (BinaryPrimitives.ReadInt32LittleEndian(extent.AsSpan(offset, 4)) != routes[i].X ||
                BinaryPrimitives.ReadInt32LittleEndian(extent.AsSpan(offset + 4, 4)) != routes[i].Y ||
                BinaryPrimitives.ReadInt32LittleEndian(extent.AsSpan(offset + 8, 4)) != routes[i].Z)
            {
                throw new InvalidDataException($"The final Wizard Peak T24 Green Wizard route {i + 1} failed readback.");
            }
        }
        ValidateNativeRoots(stream, layout, dataBase);
    }

    private static MobySourcePatch FindSingleWizardSwap(MobySourcePatchPlan plan)
    {
        MobySourcePatch[] patches = plan.Patches
            .Where(item =>
                string.Equals(item.Kind, "cross-level-existing-slot-candidate", StringComparison.OrdinalIgnoreCase) &&
                TryReadActorId(item.AfterHexPreview, out ushort actorId) &&
                actorId == GreenWizardActorId)
            .ToArray();
        if (patches.Length != 1)
            throw new InvalidOperationException($"The Wizard Peak Green Wizard candidate requires exactly one existing-slot replacement; found {patches.Length}.");
        return patches[0];
    }

    private static void ValidateNativeRoots(FileStream stream, DiscLayout layout, long dataBase)
    {
        ValidateWord(stream, layout, dataBase + GreenWizardRootSlot, GreenWizardRoot, "Wizard Peak Green Wizard root");
        ValidateWord(stream, layout, dataBase + LightningRootSlot, LightningRoot, "Wizard Peak lightning root");
        ValidateActorId(stream, layout, dataBase, GreenWizardRootSlot, GreenWizardActorId);
        ValidateActorId(stream, layout, dataBase, LightningRootSlot, LightningActorId);
    }

    private static uint[] ReadFixups(ReadOnlySpan<byte> bytes, int count)
    {
        uint[] result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(sizeof(uint) + (i * sizeof(uint)), sizeof(uint)));
        return result;
    }

    private static (long Offset, int Bytes) ReadWadEntry(FileStream stream, DiscLayout layout, int index)
    {
        byte[] header = DiscImage.ReadFileBytes(stream, layout, WadLba, index * 8L, 8);
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
            throw new InvalidDataException($"Wizard Peak root slot 0x{rootSlot:X} expected actor 0x{expected:X4}, found 0x{actual:X4}.");
    }

    private static void ValidateWord(FileStream stream, DiscLayout layout, long offset, uint expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(DiscImage.ReadFileBytes(stream, layout, WadLba, offset, sizeof(uint)));
        if (actual != expected)
            throw new InvalidDataException($"{label} expected 0x{expected:X8}, found 0x{actual:X8}.");
    }

    private static void ValidateSha256(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Sha256(bytes);
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
        try
        {
            actorId = ReadActorId(ParseHex(hex, RecordStride, "Wizard Peak candidate row"));
            return true;
        }
        catch (InvalidDataException)
        {
            actorId = 0;
            return false;
        }
    }

    private static ushort ReadActorId(ReadOnlySpan<byte> row) =>
        (ushort)(row[ActorIdOffset] | (row[ActorIdOffset + 1] << 8));
}

public sealed record WizardPeakGreenWizardNativeSwapPlan(
    string RecipeId,
    string RetiredRecipeId,
    string RetiredRecipeReason,
    string BundleId,
    string BundleFingerprint,
    string ProfileFingerprint,
    string OutputImagePath,
    string OutputImageSha256,
    string TargetLevelKey,
    int DonorTrueIndex,
    int TargetTrueIndex,
    ushort ReplacedActorId,
    byte ReplacedPodOrGroup,
    byte InstalledPodOrGroup,
    byte PreservedYaw,
    byte PreservedReward,
    int SceneBaseOffset,
    int SceneBytes,
    int PropertiesSceneOffset,
    int PropertiesExtentBytes,
    string InstalledPropertiesSha256,
    string InstalledPropertiesExtentSha256,
    IReadOnlyList<WizardPeakGreenWizardRoutePoint> RoutePoints,
    int PointerFixupListSceneOffset,
    int PointerFixupCount,
    int ReplacedFixupSceneOffset,
    int InstalledFixupSceneOffset,
    int ReplacedFixupIndex,
    string InstalledPointerFixupsSha256,
    IReadOnlyList<WizardPeakGreenWizardRetiredCandidate> RetiredCandidates,
    IReadOnlyList<string> FocusedChecks);

public sealed record WizardPeakGreenWizardRoutePoint(int X, int Y, int Z);

public sealed record WizardPeakGreenWizardRetiredCandidate(
    string RecipeId,
    string Reason,
    string RuntimeResult);
