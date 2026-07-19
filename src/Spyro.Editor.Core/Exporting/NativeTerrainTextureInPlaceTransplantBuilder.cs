using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTextureInPlaceTransplantRequest(
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId);

/// <summary>
/// Immutable, source-bound evidence required before an in-place texture transplant can be planned.
/// The builder re-reads and compares every binding; this record is not trusted as an assertion.
/// </summary>
public sealed record NativeTerrainTextureInPlaceTransplantSourceProof(
    int TargetWadEntry,
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    int TargetTexturePagesByteLength,
    string TargetTexturePagesSha256,
    int TargetDescriptorTableByteLength,
    string TargetDescriptorTableSha256,
    int DonorTexturePagesByteLength,
    string DonorTexturePagesSha256,
    int DonorDescriptorTableByteLength,
    string DonorDescriptorTableSha256,
    NativeTexturePageTargetStorageIsolationReport TargetStorageIsolation,
    NativeTerrainTextureRuntimeControlAudit TargetRuntimeControlAudit);

/// <summary>
/// WAD-relative patch with byte-exact preconditions. The target page and descriptor-table
/// hashes make an individual patch rejectable when it is applied to a different retail source.
/// </summary>
public sealed record NativeTerrainTextureInPlaceTransplantPatch(
    int TargetWadEntry,
    int TargetTextureId,
    string ExpectedTargetTexturePagesSha256,
    string ExpectedTargetDescriptorTableSha256,
    long WadOffset,
    int TexturePagesRelativeOffset,
    int ByteLength,
    byte[] Before,
    byte[] After,
    string Kind,
    string Description);

public sealed record NativeTerrainTextureInPlaceTransplantPlan(
    int TargetWadEntry,
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    int TargetTexturePagesByteLength,
    string TargetTexturePagesSha256,
    int TargetDescriptorTableByteLength,
    string TargetDescriptorTableSha256,
    int DonorTexturePagesByteLength,
    string DonorTexturePagesSha256,
    int DonorDescriptorTableByteLength,
    string DonorDescriptorTableSha256,
    int CompleteDescriptorCount,
    int LowDetailDescriptorCount,
    int LeadingLowDetailAliasDescriptorCount,
    int NormalHighDetailDescriptorCount,
    int CloseHighDetailDescriptorCount,
    int TargetOwnedByteCount,
    int TargetExclusiveByteCount,
    int PlannedPhysicalByteCount,
    int ChangedByteCount,
    int ConsistentPhysicalAliasAssignmentCount,
    int PhysicalAliasConflictCount,
    int OutOfOwnershipWriteCount,
    bool SourceBindingVerified,
    bool RuntimeControlClearanceVerified,
    bool CompleteOwnershipClosureVerified,
    bool DecodedAndExternalExclusivityVerified,
    bool ExactDonorIndexedPixelsVerified,
    bool ExactDonorPalettesVerified,
    bool LowDetailAliasPreserved,
    bool TargetDescriptorTableUnchanged,
    bool TargetTextureIdPreserved,
    bool LogicalReadbackVerified,
    IReadOnlyList<NativeTerrainTextureInPlaceTransplantPatch> Patches,
    IReadOnlyList<string> Notes);

/// <summary>
/// Copies a complete native terrain texture record's indexed art into the unchanged physical
/// storage addressed by another record. No descriptor byte is rewritten and no new storage is
/// allocated. This is intentionally narrower than relocation: it requires an all-consumer-
/// exclusive target footprint and exact donor/target descriptor format and side compatibility.
/// </summary>
public static class NativeTerrainTextureInPlaceTransplantBuilder
{
    private const int WadLba = 37;
    private const int TexturePagesSubfileIndex = 0;
    private const int ModelSubfileIndex = 1;
    private const int PackedVramRowBytes = 0x400;
    private const int FullVramTextureByteX = 0x400;
    private const int TexturePageMaxRows = 512;
    private const int AddressableTexturePageBytes = PackedVramRowBytes * TexturePageMaxRows;
    private const int LowDetailRecordBytes = 16;
    private const int HighDetailRecordBytes = 168;
    private const int IndexedTileSize = 32;
    private const int HqPaletteByteCount = 256 * 2;
    private const int LqPaletteRowByteCount = 16 * 2;
    private const int LqPaletteRowCount = 16;
    private const int CompleteDescriptorCount = 23;

    private static readonly int[][] TextureDescriptorMatrices =
    [
        [ 1,  0,  0,  1],
        [ 0,  1,  1,  0],
        [-1,  0,  0, -1],
        [ 0, -1,  1,  0],
        [ 0,  1,  1,  0],
        [-1,  0,  0,  1],
        [ 0, -1, -1,  0],
        [ 1,  0,  0, -1]
    ];

    public static NativeTerrainTextureInPlaceTransplantSourceProof InspectSourceProof(
        string sourceImagePath,
        LevelDefinition targetLevel,
        NativeTerrainTextureInPlaceTransplantRequest request)
    {
        ValidateArguments(sourceImagePath, targetLevel, request);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.OpenRead(sourceImagePath);
        TextureAsset target = LoadTextureAsset(image, layout, targetLevel.SourceWadEntry);
        TextureAsset donor = LoadTextureAsset(image, layout, request.DonorWadEntry);
        ValidateTextureId(target, request.TargetTextureId, "Target");
        ValidateTextureId(donor, request.DonorTextureId, "Donor");

        NativeTexturePageTargetStorageIsolationReport isolation =
            NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(
                sourceImagePath,
                targetLevel,
                request.TargetTextureId);
        NativeTerrainTextureRuntimeControlAudit runtime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, targetLevel);

        return new NativeTerrainTextureInPlaceTransplantSourceProof(
            TargetWadEntry: target.WadEntry,
            TargetTextureId: request.TargetTextureId,
            DonorWadEntry: donor.WadEntry,
            DonorTextureId: request.DonorTextureId,
            TargetTexturePagesByteLength: target.TexturePages.Length,
            TargetTexturePagesSha256: target.TexturePagesSha256,
            TargetDescriptorTableByteLength: target.DescriptorTableLength,
            TargetDescriptorTableSha256: target.DescriptorTableSha256,
            DonorTexturePagesByteLength: donor.TexturePages.Length,
            DonorTexturePagesSha256: donor.TexturePagesSha256,
            DonorDescriptorTableByteLength: donor.DescriptorTableLength,
            DonorDescriptorTableSha256: donor.DescriptorTableSha256,
            TargetStorageIsolation: isolation,
            TargetRuntimeControlAudit: runtime);
    }

    public static bool TryBuild(
        string sourceImagePath,
        LevelDefinition targetLevel,
        NativeTerrainTextureInPlaceTransplantRequest request,
        NativeTerrainTextureInPlaceTransplantSourceProof sourceProof,
        out NativeTerrainTextureInPlaceTransplantPlan? plan,
        out string failureReason)
    {
        plan = null;
        try
        {
            ValidateArguments(sourceImagePath, targetLevel, request);
            ArgumentNullException.ThrowIfNull(sourceProof);

            DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
            using FileStream image = File.OpenRead(sourceImagePath);
            TextureAsset target = LoadTextureAsset(image, layout, targetLevel.SourceWadEntry);
            TextureAsset donor = LoadTextureAsset(image, layout, request.DonorWadEntry);
            ValidateTextureId(target, request.TargetTextureId, "Target");
            ValidateTextureId(donor, request.DonorTextureId, "Donor");

            NativeTexturePageTargetStorageIsolationReport currentIsolation =
                NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(
                    sourceImagePath,
                    targetLevel,
                    request.TargetTextureId);
            NativeTerrainTextureRuntimeControlAudit currentRuntime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, targetLevel);

            if (!ValidateSourceProof(
                    target,
                    donor,
                    request,
                    sourceProof,
                    currentIsolation,
                    currentRuntime,
                    out failureReason))
            {
                return false;
            }

            TextureRecord targetRecord = target.Index.Records[request.TargetTextureId];
            TextureRecord donorRecord = donor.Index.Records[request.DonorTextureId];
            if (!HasRetailLowDetailAlias(targetRecord))
            {
                failureReason =
                    $"Target WAD entry {target.WadEntry} texture {request.TargetTextureId} does not preserve the byte-identical TexLq0/TexLq1/leading alias.";
                return false;
            }
            if (!HasRetailLowDetailAlias(donorRecord))
            {
                failureReason =
                    $"Donor WAD entry {donor.WadEntry} texture {request.DonorTextureId} does not preserve the byte-identical TexLq0/TexLq1/leading alias.";
                return false;
            }

            TextureDescriptor[] targetDescriptors = EnumerateCompleteRecord(targetRecord).ToArray();
            TextureDescriptor[] donorDescriptors = EnumerateCompleteRecord(donorRecord).ToArray();
            if (targetDescriptors.Length != CompleteDescriptorCount || donorDescriptors.Length != CompleteDescriptorCount)
            {
                failureReason =
                    $"A complete in-place transplant requires {CompleteDescriptorCount} descriptors; decoded {targetDescriptors.Length} target and {donorDescriptors.Length} donor descriptors.";
                return false;
            }

            for (int index = 0; index < CompleteDescriptorCount; index++)
            {
                TextureDescriptor targetDescriptor = targetDescriptors[index];
                TextureDescriptor donorDescriptor = donorDescriptors[index];
                if (targetDescriptor.Format != donorDescriptor.Format)
                {
                    failureReason =
                        $"Descriptor {targetDescriptor.Label} format mismatch: target {targetDescriptor.Format}, donor {donorDescriptor.Format}.";
                    return false;
                }
                if (targetDescriptor.TileSize != donorDescriptor.TileSize)
                {
                    failureReason =
                        $"Descriptor {targetDescriptor.Label} side mismatch: target {targetDescriptor.TileSize}, donor {donorDescriptor.TileSize}.";
                    return false;
                }
                if (!CanReadDescriptor(targetDescriptor, target.TexturePages.Length) ||
                    !CanReadDescriptor(donorDescriptor, donor.TexturePages.Length))
                {
                    failureReason =
                        $"Descriptor {targetDescriptor.Label} is not fully readable in both source-bound texture-page subfiles.";
                    return false;
                }
            }

            bool[] exclusiveMask = BuildExclusiveMask(
                target.TexturePages.Length,
                currentIsolation.TargetExclusiveRanges,
                out int exclusiveRangeByteCount,
                out failureReason);
            if (!string.IsNullOrEmpty(failureReason))
                return false;
            if (exclusiveRangeByteCount != currentIsolation.TargetExclusiveByteCount)
            {
                failureReason =
                    $"The target-exclusive range union contains {exclusiveRangeByteCount:N0} bytes, but the source proof reports {currentIsolation.TargetExclusiveByteCount:N0}.";
                return false;
            }

            HashSet<int> descriptorPhysicalBytes = [];
            foreach (TextureDescriptor descriptor in targetDescriptors)
            {
                foreach (long offset in EnumeratePaletteByteOffsets(descriptor))
                    descriptorPhysicalBytes.Add(checked((int)offset));
                for (int y = 0; y < descriptor.TileSize; y++)
                {
                    for (int x = 0; x < descriptor.TileSize; x++)
                    {
                        if (!TryGetTextureSampleAddress(
                                descriptor,
                                x,
                                y,
                                target.TexturePages.Length,
                                out long offset,
                                out _))
                        {
                            failureReason = $"Target descriptor {descriptor.Label} became unreadable while enumerating its physical footprint.";
                            return false;
                        }
                        descriptorPhysicalBytes.Add(checked((int)offset));
                    }
                }
            }
            if (descriptorPhysicalBytes.Count != currentIsolation.TargetOwnedByteCount ||
                descriptorPhysicalBytes.Any(offset => !exclusiveMask[offset]))
            {
                failureReason =
                    $"The decoded target record footprint ({descriptorPhysicalBytes.Count:N0} bytes) does not exactly fit its {currentIsolation.TargetOwnedByteCount:N0}-byte all-consumer-exclusive ownership proof.";
                return false;
            }

            Dictionary<int, PlannedByteWrite> writes = [];
            int consistentAliasAssignments = 0;
            int conflictCount = 0;
            int outOfOwnershipWriteCount = 0;
            for (int index = 0; index < CompleteDescriptorCount; index++)
            {
                TextureDescriptor targetDescriptor = targetDescriptors[index];
                TextureDescriptor donorDescriptor = donorDescriptors[index];
                long[] targetPaletteOffsets = EnumeratePaletteByteOffsets(targetDescriptor).ToArray();
                long[] donorPaletteOffsets = EnumeratePaletteByteOffsets(donorDescriptor).ToArray();
                if (targetPaletteOffsets.Length != donorPaletteOffsets.Length)
                {
                    failureReason = $"Descriptor {targetDescriptor.Label} palette byte count mismatch.";
                    return false;
                }
                for (int paletteByte = 0; paletteByte < targetPaletteOffsets.Length; paletteByte++)
                {
                    if (!TryAssignByteBits(
                            writes,
                            target.TexturePages,
                            exclusiveMask,
                            checked((int)targetPaletteOffsets[paletteByte]),
                            0xFF,
                            donor.TexturePages[checked((int)donorPaletteOffsets[paletteByte])],
                            $"{targetDescriptor.Label} palette byte {paletteByte}",
                            ref consistentAliasAssignments,
                            ref conflictCount,
                            ref outOfOwnershipWriteCount,
                            out failureReason))
                    {
                        return false;
                    }
                }

                for (int y = 0; y < targetDescriptor.TileSize; y++)
                {
                    for (int x = 0; x < targetDescriptor.TileSize; x++)
                    {
                        byte donorIndex = ReadLogicalIndex(donor.TexturePages, donorDescriptor, x, y);
                        if (!TryGetTextureSampleAddress(
                                targetDescriptor,
                                x,
                                y,
                                target.TexturePages.Length,
                                out long targetOffset,
                                out int targetNibble))
                        {
                            failureReason = $"Target descriptor {targetDescriptor.Label} became unreadable during indexed-pixel planning.";
                            return false;
                        }
                        byte mask = targetDescriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
                            ? (byte)(targetNibble == 0 ? 0x0F : 0xF0)
                            : (byte)0xFF;
                        byte desiredBits = targetDescriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
                            ? (byte)(donorIndex << (targetNibble * 4))
                            : donorIndex;
                        if (!TryAssignByteBits(
                                writes,
                                target.TexturePages,
                                exclusiveMask,
                                checked((int)targetOffset),
                                mask,
                                desiredBits,
                                $"{targetDescriptor.Label} pixel ({x},{y})",
                                ref consistentAliasAssignments,
                                ref conflictCount,
                                ref outOfOwnershipWriteCount,
                                out failureReason))
                        {
                            return false;
                        }
                    }
                }
            }

            if (writes.Count != descriptorPhysicalBytes.Count || writes.Keys.Any(offset => !descriptorPhysicalBytes.Contains(offset)))
            {
                failureReason =
                    $"The complete-record write union ({writes.Count:N0} bytes) does not equal the target descriptor footprint ({descriptorPhysicalBytes.Count:N0} bytes).";
                return false;
            }

            byte[] afterPages = target.TexturePages.ToArray();
            foreach ((int offset, PlannedByteWrite write) in writes)
            {
                afterPages[offset] = (byte)((afterPages[offset] & ~write.Mask) | (write.Value & write.Mask));
            }

            bool palettesVerified = true;
            bool pixelsVerified = true;
            for (int index = 0; index < CompleteDescriptorCount; index++)
            {
                TextureDescriptor targetDescriptor = targetDescriptors[index];
                TextureDescriptor donorDescriptor = donorDescriptors[index];
                if (!VerifyPaletteReadback(
                        donor.TexturePages,
                        donorDescriptor,
                        afterPages,
                        targetDescriptor))
                {
                    palettesVerified = false;
                    failureReason = $"Exact donor palette readback failed for descriptor {targetDescriptor.Label}.";
                    return false;
                }
                if (!VerifyIndexedPixelReadback(
                        donor.TexturePages,
                        donorDescriptor,
                        afterPages,
                        targetDescriptor))
                {
                    pixelsVerified = false;
                    failureReason = $"Exact donor indexed-pixel readback failed for descriptor {targetDescriptor.Label}.";
                    return false;
                }
            }

            NativeTerrainTextureInPlaceTransplantPatch[] patches = BuildDiffPatches(
                target,
                request.TargetTextureId,
                afterPages);
            int changedByteCount = patches.Sum(patch => patch.ByteLength);
            if (patches.Any(patch =>
                    patch.TexturePagesRelativeOffset < 0 ||
                    patch.TexturePagesRelativeOffset + patch.ByteLength > exclusiveMask.Length ||
                    Enumerable.Range(patch.TexturePagesRelativeOffset, patch.ByteLength)
                        .Any(offset => !exclusiveMask[offset])))
            {
                failureReason = "A generated patch escaped the target-owned, all-consumer-exclusive source range.";
                return false;
            }

            plan = new NativeTerrainTextureInPlaceTransplantPlan(
                TargetWadEntry: target.WadEntry,
                TargetTextureId: request.TargetTextureId,
                DonorWadEntry: donor.WadEntry,
                DonorTextureId: request.DonorTextureId,
                TargetTexturePagesByteLength: target.TexturePages.Length,
                TargetTexturePagesSha256: target.TexturePagesSha256,
                TargetDescriptorTableByteLength: target.DescriptorTableLength,
                TargetDescriptorTableSha256: target.DescriptorTableSha256,
                DonorTexturePagesByteLength: donor.TexturePages.Length,
                DonorTexturePagesSha256: donor.TexturePagesSha256,
                DonorDescriptorTableByteLength: donor.DescriptorTableLength,
                DonorDescriptorTableSha256: donor.DescriptorTableSha256,
                CompleteDescriptorCount: targetDescriptors.Length,
                LowDetailDescriptorCount: targetRecord.LowDetailDescriptors.Count,
                LeadingLowDetailAliasDescriptorCount: 1,
                NormalHighDetailDescriptorCount: targetRecord.NormalDescriptors.Count,
                CloseHighDetailDescriptorCount: targetRecord.CloseDescriptors.Count,
                TargetOwnedByteCount: currentIsolation.TargetOwnedByteCount,
                TargetExclusiveByteCount: currentIsolation.TargetExclusiveByteCount,
                PlannedPhysicalByteCount: writes.Count,
                ChangedByteCount: changedByteCount,
                ConsistentPhysicalAliasAssignmentCount: consistentAliasAssignments,
                PhysicalAliasConflictCount: conflictCount,
                OutOfOwnershipWriteCount: outOfOwnershipWriteCount,
                SourceBindingVerified: true,
                RuntimeControlClearanceVerified: true,
                CompleteOwnershipClosureVerified: true,
                DecodedAndExternalExclusivityVerified: true,
                ExactDonorIndexedPixelsVerified: pixelsVerified,
                ExactDonorPalettesVerified: palettesVerified,
                LowDetailAliasPreserved: HasRetailLowDetailAlias(targetRecord),
                TargetDescriptorTableUnchanged: true,
                TargetTextureIdPreserved: targetRecord.TextureId == request.TargetTextureId,
                LogicalReadbackVerified: pixelsVerified && palettesVerified,
                Patches: patches,
                Notes:
                [
                    "The patch set is WAD-relative and carries byte-exact Before/After data plus full target page/table hash preconditions.",
                    "All two LQ, one byte-identical leading LQ alias, four normal HQ, and sixteen close HQ descriptors were copied through their unchanged target descriptors.",
                    "Only the target record's all-consumer-exclusive physical pixel and CLUT union is writable; no model or descriptor-table patch is emitted.",
                    "Runtime texture-animation and scrolling controls were re-read from the same source and the target texture id is absent from both sets."
                ]);
            failureReason = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static bool ValidateSourceProof(
        TextureAsset target,
        TextureAsset donor,
        NativeTerrainTextureInPlaceTransplantRequest request,
        NativeTerrainTextureInPlaceTransplantSourceProof proof,
        NativeTexturePageTargetStorageIsolationReport currentIsolation,
        NativeTerrainTextureRuntimeControlAudit currentRuntime,
        out string failureReason)
    {
        if (proof.TargetWadEntry != target.WadEntry ||
            proof.TargetTextureId != request.TargetTextureId ||
            proof.DonorWadEntry != donor.WadEntry ||
            proof.DonorTextureId != request.DonorTextureId)
        {
            failureReason = "The source proof is bound to a different target or donor WAD/texture identity.";
            return false;
        }
        if (proof.TargetTexturePagesByteLength != target.TexturePages.Length ||
            !HashEquals(proof.TargetTexturePagesSha256, target.TexturePagesSha256) ||
            proof.TargetDescriptorTableByteLength != target.DescriptorTableLength ||
            !HashEquals(proof.TargetDescriptorTableSha256, target.DescriptorTableSha256))
        {
            failureReason = "The target texture-page or complete descriptor-table source hash does not match the current image.";
            return false;
        }
        if (proof.DonorTexturePagesByteLength != donor.TexturePages.Length ||
            !HashEquals(proof.DonorTexturePagesSha256, donor.TexturePagesSha256) ||
            proof.DonorDescriptorTableByteLength != donor.DescriptorTableLength ||
            !HashEquals(proof.DonorDescriptorTableSha256, donor.DescriptorTableSha256))
        {
            failureReason = "The donor texture-page or complete descriptor-table source hash does not match the current image.";
            return false;
        }

        NativeTexturePageTargetStorageIsolationReport suppliedIsolation = proof.TargetStorageIsolation;
        if (suppliedIsolation.WadEntry != target.WadEntry ||
            suppliedIsolation.TargetTextureId != request.TargetTextureId ||
            suppliedIsolation.TextureCount != currentIsolation.TextureCount ||
            suppliedIsolation.TargetOwnedByteCount != currentIsolation.TargetOwnedByteCount ||
            suppliedIsolation.TargetExclusiveByteCount != currentIsolation.TargetExclusiveByteCount ||
            suppliedIsolation.TargetPixelAndClutByteCount != currentIsolation.TargetPixelAndClutByteCount ||
            !HashEquals(suppliedIsolation.TexturePagesSubfileSha256, currentIsolation.TexturePagesSubfileSha256) ||
            suppliedIsolation.TerrainDescriptorTableByteLength != currentIsolation.TerrainDescriptorTableByteLength ||
            !HashEquals(suppliedIsolation.TerrainDescriptorTableSha256, currentIsolation.TerrainDescriptorTableSha256) ||
            !RangesEqual(suppliedIsolation.TargetOwnedRanges, currentIsolation.TargetOwnedRanges) ||
            !RangesEqual(suppliedIsolation.TargetExclusiveRanges, currentIsolation.TargetExclusiveRanges))
        {
            failureReason = "The target-storage isolation proof does not match a fresh scan of the current source.";
            return false;
        }
        if (!suppliedIsolation.AllConsumerClosureComplete ||
            !currentIsolation.AllConsumerClosureComplete ||
            suppliedIsolation.SafetyBlockers.Count > 0 ||
            currentIsolation.SafetyBlockers.Count > 0)
        {
            failureReason = "The all-consumer ownership closure is incomplete for the target texture footprint.";
            return false;
        }
        if (!suppliedIsolation.DecodedConsumerExclusive ||
            !currentIsolation.DecodedConsumerExclusive ||
            HasAnyOverlap(suppliedIsolation) ||
            HasAnyOverlap(currentIsolation) ||
            suppliedIsolation.TargetOwnedByteCount <= 0 ||
            suppliedIsolation.TargetOwnedByteCount != suppliedIsolation.TargetExclusiveByteCount)
        {
            failureReason = "The target texture has a decoded terrain or external-consumer overlap; in-place writeback is blocked.";
            return false;
        }

        NativeTerrainTextureRuntimeControlAudit suppliedRuntime = proof.TargetRuntimeControlAudit;
        if (suppliedRuntime.TargetWadEntry != target.WadEntry ||
            suppliedRuntime.TextureCount != currentRuntime.TextureCount ||
            suppliedRuntime.SceneByteLength != currentRuntime.SceneByteLength ||
            !HashEquals(suppliedRuntime.SceneSha256, currentRuntime.SceneSha256) ||
            !ControlsEqual(suppliedRuntime.Controls, currentRuntime.Controls))
        {
            failureReason = "The runtime texture-control proof does not match a fresh scan of the current source.";
            return false;
        }
        if (!suppliedRuntime.Complete || !currentRuntime.Complete ||
            suppliedRuntime.SafetyBlockers.Count > 0 || currentRuntime.SafetyBlockers.Count > 0)
        {
            failureReason = "Runtime texture-control inspection is incomplete.";
            return false;
        }
        if (!suppliedRuntime.IsRuntimePersistentTarget(request.TargetTextureId) ||
            !currentRuntime.IsRuntimePersistentTarget(request.TargetTextureId))
        {
            failureReason = suppliedRuntime.TargetReadinessNote(request.TargetTextureId);
            return false;
        }

        failureReason = "";
        return true;
    }

    private static bool HasAnyOverlap(NativeTexturePageTargetStorageIsolationReport report) =>
        report.OtherTerrainOverlapByteCount != 0 ||
        report.ParticleOverlapByteCount != 0 ||
        report.ResidentActorOverlapByteCount != 0 ||
        report.PlayerOverlapByteCount != 0 ||
        report.HudGlobalOverlapByteCount != 0 ||
        report.OtherDecodedOverlapByteCount != 0 ||
        report.AnyDecodedConsumerOverlapByteCount != 0;

    private static bool RangesEqual(
        IReadOnlyList<NativeTexturePageOwnedRange> left,
        IReadOnlyList<NativeTexturePageOwnedRange> right)
    {
        NativeTexturePageOwnedRange[] orderedLeft = left
            .OrderBy(range => range.Offset)
            .ThenBy(range => range.Length)
            .ThenBy(range => range.Owner, StringComparer.Ordinal)
            .ToArray();
        NativeTexturePageOwnedRange[] orderedRight = right
            .OrderBy(range => range.Offset)
            .ThenBy(range => range.Length)
            .ThenBy(range => range.Owner, StringComparer.Ordinal)
            .ToArray();
        return orderedLeft.Length == orderedRight.Length &&
            orderedLeft.Zip(orderedRight).All(pair =>
                pair.First.Offset == pair.Second.Offset &&
                pair.First.Length == pair.Second.Length &&
                string.Equals(pair.First.Owner, pair.Second.Owner, StringComparison.Ordinal));
    }

    private static bool ControlsEqual(
        IReadOnlyList<NativeTerrainTextureRuntimeControl> left,
        IReadOnlyList<NativeTerrainTextureRuntimeControl> right)
    {
        static string Key(NativeTerrainTextureRuntimeControl control) =>
            $"{control.TextureId}|{control.Kind}|{control.PointerIndex}|{control.SceneRelativeStructureOffset}|{control.Description}";
        return left.Select(Key).Order(StringComparer.Ordinal)
            .SequenceEqual(right.Select(Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static bool[] BuildExclusiveMask(
        int texturePagesLength,
        IReadOnlyList<NativeTexturePageOwnedRange> ranges,
        out int ownedByteCount,
        out string failureReason)
    {
        bool[] mask = new bool[texturePagesLength];
        foreach (NativeTexturePageOwnedRange range in ranges)
        {
            if (range.Offset < 0 || range.Length <= 0 || range.Offset + range.Length > texturePagesLength ||
                range.Offset + range.Length > AddressableTexturePageBytes)
            {
                ownedByteCount = 0;
                failureReason =
                    $"Target-exclusive range '{range.Owner}' at 0x{range.Offset:X}+0x{range.Length:X} is outside the source-bound texture-page prefix.";
                return mask;
            }
            for (long offset = range.Offset; offset < range.Offset + range.Length; offset++)
                mask[checked((int)offset)] = true;
        }
        ownedByteCount = mask.Count(value => value);
        failureReason = "";
        return mask;
    }

    private static bool TryAssignByteBits(
        Dictionary<int, PlannedByteWrite> writes,
        byte[] targetPages,
        bool[] exclusiveMask,
        int offset,
        byte mask,
        byte desiredBits,
        string semantic,
        ref int consistentAliasAssignments,
        ref int conflictCount,
        ref int outOfOwnershipWriteCount,
        out string failureReason)
    {
        if (offset < 0 || offset >= targetPages.Length || !exclusiveMask[offset])
        {
            outOfOwnershipWriteCount++;
            failureReason =
                $"Planned {semantic} write at texture-page byte 0x{offset:X} is outside the target-owned, all-consumer-exclusive range.";
            return false;
        }

        if (!writes.TryGetValue(offset, out PlannedByteWrite? existing))
        {
            writes[offset] = new PlannedByteWrite(mask, (byte)(desiredBits & mask), semantic);
            failureReason = "";
            return true;
        }

        byte overlap = (byte)(existing.Mask & mask);
        if (overlap != 0)
        {
            if ((existing.Value & overlap) != (desiredBits & overlap))
            {
                conflictCount++;
                failureReason =
                    $"Physical byte/nibble alias conflict at target texture-page byte 0x{offset:X}: '{existing.FirstSemantic}' and '{semantic}' request different values under mask 0x{overlap:X2}.";
                return false;
            }
            consistentAliasAssignments++;
        }

        byte combinedMask = (byte)(existing.Mask | mask);
        byte combinedValue = (byte)((existing.Value & existing.Mask) | (desiredBits & mask));
        writes[offset] = existing with { Mask = combinedMask, Value = combinedValue };
        failureReason = "";
        return true;
    }

    private static NativeTerrainTextureInPlaceTransplantPatch[] BuildDiffPatches(
        TextureAsset target,
        int targetTextureId,
        byte[] after)
    {
        List<NativeTerrainTextureInPlaceTransplantPatch> patches = [];
        int index = 0;
        while (index < target.TexturePages.Length)
        {
            if (target.TexturePages[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index;
            while (index < target.TexturePages.Length && target.TexturePages[index] != after[index])
                index++;
            int length = index - start;
            patches.Add(new NativeTerrainTextureInPlaceTransplantPatch(
                TargetWadEntry: target.WadEntry,
                TargetTextureId: targetTextureId,
                ExpectedTargetTexturePagesSha256: target.TexturePagesSha256,
                ExpectedTargetDescriptorTableSha256: target.DescriptorTableSha256,
                WadOffset: target.TexturePagesWadOffset + start,
                TexturePagesRelativeOffset: start,
                ByteLength: length,
                Before: target.TexturePages.AsSpan(start, length).ToArray(),
                After: after.AsSpan(start, length).ToArray(),
                Kind: "terrain-texture-in-place-data",
                Description: $"Copy exact donor indexed pixels and palettes into unchanged target texture {targetTextureId} storage."));
        }
        return patches.ToArray();
    }

    private static bool VerifyPaletteReadback(
        byte[] donorPages,
        TextureDescriptor donor,
        byte[] targetPages,
        TextureDescriptor target)
    {
        long[] donorOffsets = EnumeratePaletteByteOffsets(donor).ToArray();
        long[] targetOffsets = EnumeratePaletteByteOffsets(target).ToArray();
        return donorOffsets.Length == targetOffsets.Length &&
            donorOffsets.Zip(targetOffsets).All(pair =>
                donorPages[checked((int)pair.First)] == targetPages[checked((int)pair.Second)]);
    }

    private static bool VerifyIndexedPixelReadback(
        byte[] donorPages,
        TextureDescriptor donor,
        byte[] targetPages,
        TextureDescriptor target)
    {
        for (int y = 0; y < donor.TileSize; y++)
        {
            for (int x = 0; x < donor.TileSize; x++)
            {
                if (ReadLogicalIndex(donorPages, donor, x, y) !=
                    ReadLogicalIndex(targetPages, target, x, y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static byte ReadLogicalIndex(byte[] pages, TextureDescriptor descriptor, int x, int y)
    {
        if (!TryGetTextureSampleAddress(descriptor, x, y, pages.Length, out long offset, out int nibble))
            throw new InvalidOperationException($"Descriptor {descriptor.Label} sample ({x},{y}) is outside the texture-pages subfile.");
        byte packed = pages[checked((int)offset)];
        return descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
            ? (byte)((packed >> (nibble * 4)) & 0x0F)
            : packed;
    }

    private static bool CanReadDescriptor(TextureDescriptor descriptor, int texturePagesLength)
    {
        foreach (long offset in EnumeratePaletteByteOffsets(descriptor))
        {
            if (offset < 0 || offset >= texturePagesLength || offset >= AddressableTexturePageBytes)
                return false;
        }
        for (int y = 0; y < descriptor.TileSize; y++)
        {
            for (int x = 0; x < descriptor.TileSize; x++)
            {
                if (!TryGetTextureSampleAddress(descriptor, x, y, texturePagesLength, out _, out _))
                    return false;
            }
        }
        return true;
    }

    private static bool TryGetTextureSampleAddress(
        TextureDescriptor descriptor,
        int x,
        int y,
        int texturePagesLength,
        out long relativeOffset,
        out int nibble)
    {
        if (x < 0 || x >= descriptor.TileSize || y < 0 || y >= descriptor.TileSize)
        {
            relativeOffset = -1;
            nibble = 0;
            return false;
        }

        if (descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp)
        {
            int packedX = descriptor.PackedPixelXMin + (x / 2);
            int sampleY = descriptor.VramYMin + y;
            nibble = x & 1;
            relativeOffset = (sampleY * (long)PackedVramRowBytes) + packedX;
            return packedX >= 0 && packedX < PackedVramRowBytes &&
                sampleY >= 0 && sampleY < TexturePageMaxRows &&
                relativeOffset >= 0 && relativeOffset < texturePagesLength &&
                relativeOffset < AddressableTexturePageBytes;
        }

        int[] matrix = TextureDescriptorMatrices[Math.Clamp(
            descriptor.Orientation,
            0,
            TextureDescriptorMatrices.Length - 1)];
        int xx = matrix[0];
        int xy = matrix[1];
        int yx = matrix[2];
        int yy = matrix[3];
        int edge = descriptor.TileSize - 1;
        int startY = descriptor.VramYMin + ((yx < 0 || yy < 0) ? edge : 0);
        int sampleYHq = startY + (x * yx) + (y * yy);
        int startX = descriptor.PackedPixelXMin + ((xx < 0 || xy < 0) ? edge : 0);
        int sampleX = startX + (x * xx) + (y * xy);
        nibble = 0;
        relativeOffset = (sampleYHq * (long)PackedVramRowBytes) + sampleX;
        return sampleX >= 0 && sampleX < PackedVramRowBytes &&
            sampleYHq >= 0 && sampleYHq < TexturePageMaxRows &&
            relativeOffset >= 0 && relativeOffset < texturePagesLength &&
            relativeOffset < AddressableTexturePageBytes;
    }

    private static IEnumerable<long> EnumeratePaletteByteOffsets(TextureDescriptor descriptor)
    {
        if (descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp)
        {
            for (int row = 0; row < LqPaletteRowCount; row++)
            {
                long rowStart = descriptor.PaletteByteStart + (row * PackedVramRowBytes);
                for (int index = 0; index < LqPaletteRowByteCount; index++)
                    yield return rowStart + index;
            }
            yield break;
        }

        for (int index = 0; index < HqPaletteByteCount; index++)
            yield return descriptor.PaletteByteStart + index;
    }

    private static IEnumerable<TextureDescriptor> EnumerateCompleteRecord(TextureRecord record)
    {
        foreach (TextureDescriptor descriptor in record.LowDetailDescriptors)
            yield return descriptor;
        yield return record.HighDetailLeadingDescriptor;
        foreach (TextureDescriptor descriptor in record.NormalDescriptors)
            yield return descriptor;
        foreach (TextureDescriptor descriptor in record.CloseDescriptors)
            yield return descriptor;
    }

    private static bool HasRetailLowDetailAlias(TextureRecord record) =>
        record.LowDetailDescriptors.Count == 2 &&
        record.LowDetailDescriptors[0].Raw.SequenceEqual(record.LowDetailDescriptors[1].Raw) &&
        record.LowDetailDescriptors[0].Raw.SequenceEqual(record.HighDetailLeadingDescriptor.Raw);

    private static void ValidateArguments(
        string sourceImagePath,
        LevelDefinition targetLevel,
        NativeTerrainTextureInPlaceTransplantRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("The retail source image is required for native texture transplant planning.", sourceImagePath);
        ArgumentNullException.ThrowIfNull(targetLevel);
        ArgumentNullException.ThrowIfNull(request);
        if (targetLevel.SourceWadEntry < 0)
            throw new ArgumentOutOfRangeException(nameof(targetLevel), "The target level has no mapped source WAD entry.");
        if (request.TargetTextureId < 0)
            throw new ArgumentOutOfRangeException(nameof(request.TargetTextureId));
        if (request.DonorWadEntry < 0)
            throw new ArgumentOutOfRangeException(nameof(request.DonorWadEntry));
        if (request.DonorTextureId < 0)
            throw new ArgumentOutOfRangeException(nameof(request.DonorTextureId));
    }

    private static void ValidateTextureId(TextureAsset asset, int textureId, string label)
    {
        if (textureId < 0 || textureId >= asset.Index.TextureCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(textureId),
                $"{label} texture {textureId} is outside WAD entry {asset.WadEntry}'s 0..{asset.Index.TextureCount - 1} table.");
        }
    }

    private static TextureAsset LoadTextureAsset(FileStream image, DiscLayout layout, int wadEntry)
    {
        AssetSubfileInfo pagesInfo = GetAssetSubfileInfo(image, layout, wadEntry, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(image, layout, wadEntry, ModelSubfileIndex);
        byte[] pages = ReadWadBytes(image, layout, pagesInfo.AbsoluteWadOffset, checked((int)pagesInfo.SubfileSize));
        byte[] model = ReadWadBytes(image, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex index = DecodeTextureRecords(model, out int descriptorTableLength);
        return new TextureAsset(
            WadEntry: wadEntry,
            TexturePagesWadOffset: pagesInfo.AbsoluteWadOffset,
            TexturePages: pages,
            Model: model,
            TexturePagesSha256: Sha256(pages),
            DescriptorTableLength: descriptorTableLength,
            DescriptorTableSha256: Sha256(model.AsSpan(0, descriptorTableLength)),
            Index: index);
    }

    private static AssetSubfileInfo GetAssetSubfileInfo(
        FileStream image,
        DiscLayout layout,
        int wadEntry,
        int subfileIndex)
    {
        byte[] wadHeader = ReadWadBytes(image, layout, 0, 4096);
        ArchiveEntry asset = ParseArchiveHeader(wadHeader, 200_000_000)
            .FirstOrDefault(entry => entry.Index == wadEntry)
            ?? throw new InvalidOperationException($"Could not find WAD entry {wadEntry}.");
        byte[] assetHeader = ReadWadBytes(image, layout, asset.Offset, 4096);
        ArchiveEntry subfile = ParseArchiveHeader(assetHeader, asset.Size)
            .FirstOrDefault(entry => entry.Index == subfileIndex)
            ?? throw new InvalidOperationException($"Could not find subfile {subfileIndex} in WAD entry {wadEntry}.");
        return new AssetSubfileInfo(subfile.Size, asset.Offset + subfile.Offset);
    }

    private static IReadOnlyList<ArchiveEntry> ParseArchiveHeader(byte[] bytes, long archiveSize)
    {
        List<ArchiveEntry> entries = [];
        long firstDataOffset = ReadUInt32(bytes, 0);
        if (firstDataOffset <= 0 || firstDataOffset > bytes.Length)
            firstDataOffset = bytes.Length;
        for (int offset = 0; offset <= Math.Min(bytes.Length, firstDataOffset) - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(bytes, offset);
            long fileSize = ReadUInt32(bytes, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            if (fileOffset < 0 || fileSize <= 0 || fileOffset + fileSize > archiveSize)
                continue;
            entries.Add(new ArchiveEntry(offset / 8, fileOffset, fileSize));
        }
        return entries;
    }

    private static TextureRecordIndex DecodeTextureRecords(byte[] model, out int descriptorTableLength)
    {
        if (model.Length < 8)
            throw new InvalidDataException("Model subfile is too short for a native terrain texture table.");
        descriptorTableLength = checked((int)ReadUInt32(model, 0));
        int textureCount = checked((int)ReadUInt32(model, 4));
        int expected = checked(8 + (textureCount * (LowDetailRecordBytes + HighDetailRecordBytes)));
        if (textureCount <= 0 || descriptorTableLength != expected || expected > model.Length)
        {
            throw new InvalidDataException(
                $"Native texture table length 0x{descriptorTableLength:X} does not match {textureCount} complete records (expected 0x{expected:X}).");
        }

        int highTableOffset = checked(8 + (textureCount * LowDetailRecordBytes));
        List<TextureRecord> records = new(textureCount);
        for (int textureId = 0; textureId < textureCount; textureId++)
        {
            int lowOffset = checked(8 + (textureId * LowDetailRecordBytes));
            int highOffset = checked(highTableOffset + (textureId * HighDetailRecordBytes));
            TextureDescriptor[] low =
            [
                DecodeLowDetailDescriptor(model, lowOffset, "lq-0"),
                DecodeLowDetailDescriptor(model, lowOffset + 8, "lq-1")
            ];
            TextureDescriptor leading = DecodeLowDetailDescriptor(model, highOffset, "leading-lq-alias");
            TextureDescriptor[] normal = Enumerable.Range(0, 4)
                .Select(index => DecodeHighDetailDescriptor(model, highOffset + 8 + (index * 8), $"normal-{index}"))
                .ToArray();
            TextureDescriptor[] close = Enumerable.Range(0, 16)
                .Select(index => DecodeHighDetailDescriptor(model, highOffset + 40 + (index * 8), $"close-{index}"))
                .ToArray();
            records.Add(new TextureRecord(textureId, low, leading, normal, close));
        }
        return new TextureRecordIndex(textureCount, records);
    }

    private static TextureDescriptor DecodeLowDetailDescriptor(byte[] model, int offset, string label)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        return new TextureDescriptor(
            Label: label,
            ModelOffset: offset,
            Raw: raw,
            Format: TextureDescriptorFormat.LowDetail4Bpp,
            TileSize: IndexedTileSize,
            PaletteByteStart: checked((raw[3] * 4L * PackedVramRowBytes) +
                (raw[2] * (long)LqPaletteRowByteCount) - FullVramTextureByteX),
            Orientation: 0,
            PackedPixelXMin: (((region * 256) % 2048) + raw[0]) / 2,
            VramYMin: GetTextureY(region, raw[1]));
    }

    private static TextureDescriptor DecodeHighDetailDescriptor(byte[] model, int offset, string label)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        int side = checked(Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1);
        if (side is < 1 or > 256)
            throw new InvalidDataException($"Descriptor {label} has invalid side {side}.");
        return new TextureDescriptor(
            Label: label,
            ModelOffset: offset,
            Raw: raw,
            Format: TextureDescriptorFormat.HighDetail8Bpp,
            TileSize: side,
            PaletteByteStart: DecodePackedClutByteStart(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2))),
            Orientation: (raw[7] >> 4) & 7,
            PackedPixelXMin: GetTextureX(region, raw[0]) - FullVramTextureByteX,
            VramYMin: GetTextureY(region, raw[1]));
    }

    private static int GetTextureX(int region, int value) => ((region * 128) % 2048) + value;

    private static int GetTextureY(int region, int value) => ((region & 0x10) != 0 ? 256 : 0) + value;

    private static int DecodePackedClutByteStart(int code)
    {
        int clutX = (code & 0x3F) * 16;
        int clutY = (code >> 6) & 0x1FF;
        return checked((clutY * PackedVramRowBytes) + ((clutX - 512) * 2));
    }

    private static byte[] ReadWadBytes(FileStream stream, DiscLayout layout, long wadOffset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, length);

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static bool HashEquals(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record ArchiveEntry(int Index, long Offset, long Size);

    private sealed record AssetSubfileInfo(long SubfileSize, long AbsoluteWadOffset);

    private sealed record TextureAsset(
        int WadEntry,
        long TexturePagesWadOffset,
        byte[] TexturePages,
        byte[] Model,
        string TexturePagesSha256,
        int DescriptorTableLength,
        string DescriptorTableSha256,
        TextureRecordIndex Index);

    private sealed record TextureRecordIndex(int TextureCount, IReadOnlyList<TextureRecord> Records);

    private sealed record TextureRecord(
        int TextureId,
        IReadOnlyList<TextureDescriptor> LowDetailDescriptors,
        TextureDescriptor HighDetailLeadingDescriptor,
        IReadOnlyList<TextureDescriptor> NormalDescriptors,
        IReadOnlyList<TextureDescriptor> CloseDescriptors);

    private sealed record TextureDescriptor(
        string Label,
        int ModelOffset,
        byte[] Raw,
        TextureDescriptorFormat Format,
        int TileSize,
        long PaletteByteStart,
        int Orientation,
        int PackedPixelXMin,
        int VramYMin);

    private enum TextureDescriptorFormat
    {
        LowDetail4Bpp,
        HighDetail8Bpp
    }

    private sealed record PlannedByteWrite(byte Mask, byte Value, string FirstSemantic);
}
