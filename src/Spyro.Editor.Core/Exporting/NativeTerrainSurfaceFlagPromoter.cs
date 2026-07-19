using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainSurfaceFlagAssignment(
    int TriangleIndex,
    int SurfaceIndex,
    string RuntimeKey,
    string SurfaceLabel);

public sealed record NativeTerrainSurfaceFlagCapacity(
    int TriangleCount,
    int FlagCount,
    int ImmediateAlignmentSlots,
    int VerifiedZeroTailBytes,
    int AdditionalFlagCapacity);

public sealed record NativeTerrainSurfaceExistingPatch(
    long WadOffset,
    byte[] Before,
    byte[] After,
    string Kind);

public sealed record NativeTerrainSurfaceTriangleRemap(
    int StagingTriangleIndex,
    int TargetTriangleIndex,
    int StagingReferenceCount,
    int TargetReferenceCount);

public sealed record NativeTerrainSurfaceFlagPromotionPlan(
    long WadOffset,
    byte[] Before,
    byte[] After,
    int TriangleCount,
    int OldFlagCount,
    int NewFlagCount,
    int ShiftBytes,
    int ZeroTailBytesBefore,
    int ZeroTailBytesAfter,
    long OldCollisionEndWadOffset,
    long NewCollisionEndWadOffset,
    long OldUsedLevelEndWadOffset,
    long NewUsedLevelEndWadOffset,
    IReadOnlyList<NativeTerrainSurfaceTriangleRemap> TriangleRemaps,
    IReadOnlyList<long> ConsumedPatchWadOffsets,
    IReadOnlyList<int> AssignedTriangleIndexes)
{
    public bool HasChanges => !Before.AsSpan().SequenceEqual(After);
}

/// <summary>
/// Promotes collision triangles beyond the native surface-flag count into a
/// small, explicit low-index flag range without changing the level-data block
/// length. The collision suffix is shifted only into verified zero padding.
/// </summary>
public static class NativeTerrainSurfaceFlagPromoter
{
    private const int CollisionHeaderLength = 0x1C;
    private const int MaxPortalCount = 6;
    private const int MaxPortalPointCount = 64;

    public static bool TryInspectCapacity(
        byte[] sourceLevelData,
        long levelDataWadOffset,
        long collisionComponentWadOffset,
        out NativeTerrainSurfaceFlagCapacity? capacity,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(sourceLevelData);
        capacity = null;
        reason = "";
        long collisionRelativeLong = collisionComponentWadOffset - levelDataWadOffset;
        if (collisionRelativeLong < 0 || collisionRelativeLong > int.MaxValue)
        {
            reason = "The collision component is outside the native level-data block.";
            return false;
        }

        if (!TryReadCollisionLayout(sourceLevelData, (int)collisionRelativeLong, out CollisionLayout? layout, out reason) || layout == null ||
            !TryParseSuffix(sourceLevelData, layout.CollisionEnd, out int usedLevelEnd, out reason))
        {
            return false;
        }

        int zeroTailBytes = sourceLevelData.Length - usedLevelEnd;
        if (zeroTailBytes < 0 || sourceLevelData.AsSpan(usedLevelEnd).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The bytes after the final native sound component are not verified zero padding.";
            return false;
        }

        int immediateSlots = layout.FlagStorageBytes - layout.FlagCount;
        int additionalCapacity = Math.Min(
            layout.TriangleCount - layout.FlagCount,
            immediateSlots + (zeroTailBytes & ~3));
        capacity = new NativeTerrainSurfaceFlagCapacity(
            layout.TriangleCount,
            layout.FlagCount,
            immediateSlots,
            zeroTailBytes,
            additionalCapacity);
        return true;
    }

    public static bool TryBuild(
        byte[] sourceLevelData,
        long levelDataWadOffset,
        long collisionComponentWadOffset,
        IReadOnlyList<NativeTerrainSurfaceFlagAssignment> assignments,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        out NativeTerrainSurfaceFlagPromotionPlan? plan,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(sourceLevelData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(existingPatches);
        plan = null;
        reason = "";

        if (assignments.Count == 0)
        {
            reason = "No native surface assignments were requested.";
            return false;
        }

        long collisionRelativeLong = collisionComponentWadOffset - levelDataWadOffset;
        if (collisionRelativeLong < 0 || collisionRelativeLong > int.MaxValue)
        {
            reason = "The collision component is outside the native level-data block.";
            return false;
        }

        int collisionStart = (int)collisionRelativeLong;
        if (!TryReadCollisionLayout(sourceLevelData, collisionStart, out CollisionLayout? layout, out reason) || layout == null)
            return false;
        if (!TryParseSuffix(sourceLevelData, layout.CollisionEnd, out int usedLevelEnd, out reason))
            return false;

        int zeroTailBytes = sourceLevelData.Length - usedLevelEnd;
        if (zeroTailBytes < 0 || sourceLevelData.AsSpan(usedLevelEnd).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The bytes after the final native sound component are not verified zero padding.";
            return false;
        }

        if (!TryNormalizeAssignments(assignments, layout.TriangleCount, out Dictionary<int, NativeTerrainSurfaceFlagAssignment>? normalized, out reason) || normalized == null)
            return false;

        int[] beyondTriangleIndexes = normalized.Keys
            .Where(index => index >= layout.FlagCount)
            .Order()
            .ToArray();
        if (beyondTriangleIndexes.Length == 0)
        {
            return TryBuildDirectFlagPlan(
                sourceLevelData,
                levelDataWadOffset,
                layout,
                normalized,
                existingPatches,
                zeroTailBytes,
                usedLevelEnd,
                out plan,
                out reason);
        }

        int newFlagCount = checked(layout.FlagCount + beyondTriangleIndexes.Length);
        if (newFlagCount > layout.TriangleCount)
        {
            reason = $"The requested promotion needs flag count {newFlagCount}, beyond triangle count {layout.TriangleCount}.";
            return false;
        }

        int newFlagStorage = Align4(newFlagCount);
        int shiftBytes = newFlagStorage - layout.FlagStorageBytes;
        if (shiftBytes < 0 || shiftBytes > zeroTailBytes)
        {
            reason = $"The requested promotion needs {Math.Max(shiftBytes, 0)} suffix-shift bytes, but only {zeroTailBytes} verified zero bytes remain in this level-data block.";
            return false;
        }

        int sourceSemanticEnd = shiftBytes > 0 ? usedLevelEnd : layout.CollisionEnd;
        int outputRegionEnd = shiftBytes > 0 ? checked(usedLevelEnd + shiftBytes) : layout.CollisionEnd;
        if (!TryComposeExistingPatches(
                sourceLevelData,
                levelDataWadOffset,
                collisionStart,
                sourceSemanticEnd,
                outputRegionEnd,
                layout,
                existingPatches,
                out byte[]? working,
                out long[]? consumedPatchOffsets,
                out reason) ||
            working == null ||
            consumedPatchOffsets == null)
        {
            return false;
        }

        byte[] expectedSuffix = working
            .AsSpan(layout.CollisionEnd, usedLevelEnd - layout.CollisionEnd)
            .ToArray();
        if (shiftBytes > 0)
        {
            Array.Copy(
                working,
                layout.CollisionEnd,
                working,
                layout.CollisionEnd + shiftBytes,
                usedLevelEnd - layout.CollisionEnd);
            Array.Clear(working, layout.CollisionEnd, shiftBytes);
        }

        int newCollisionEnd = checked(layout.CollisionEnd + shiftBytes);
        int newUsedLevelEnd = checked(usedLevelEnd + shiftBytes);
        BinaryPrimitives.WriteInt32LittleEndian(
            working.AsSpan(layout.CollisionStart, 4),
            checked(layout.CollisionLength + shiftBytes));
        BinaryPrimitives.WriteInt32LittleEndian(
            working.AsSpan(layout.CollisionBody + 4, 4),
            newFlagCount);

        for (int index = layout.FlagCount; index < newFlagCount; index++)
            working[layout.FlagsStart + index] = 0xFF;
        if (newFlagStorage > newFlagCount)
            Array.Clear(working, layout.FlagsStart + newFlagCount, newFlagStorage - newFlagCount);

        int[] stagingIndexes = Enumerable.Range(layout.FlagCount, beyondTriangleIndexes.Length).ToArray();
        HashSet<int> directlyPromoted = stagingIndexes.Intersect(beyondTriangleIndexes).ToHashSet();
        int[] freeStagingIndexes = stagingIndexes.Where(index => !directlyPromoted.Contains(index)).ToArray();
        int[] remappedTargetIndexes = beyondTriangleIndexes.Where(index => !directlyPromoted.Contains(index)).ToArray();
        if (freeStagingIndexes.Length != remappedTargetIndexes.Length)
        {
            reason = "The native surface staging permutation could not be balanced.";
            return false;
        }

        Dictionary<int, int> lookupPermutation = new();
        List<NativeTerrainSurfaceTriangleRemap> remaps = [];
        for (int i = 0; i < freeStagingIndexes.Length; i++)
        {
            int stagingIndex = freeStagingIndexes[i];
            int targetIndex = remappedTargetIndexes[i];
            if (targetIndex < newFlagCount)
            {
                reason = $"Collision triangle {targetIndex} unexpectedly overlaps the promoted staging range.";
                return false;
            }

            SwapBytes(
                working,
                layout.TrianglesStart + (stagingIndex * 12),
                layout.TrianglesStart + (targetIndex * 12),
                12);
            SwapBytes(
                working,
                layout.OcclusionStart + stagingIndex,
                layout.OcclusionStart + targetIndex,
                1);
            lookupPermutation[stagingIndex] = targetIndex;
            lookupPermutation[targetIndex] = stagingIndex;
        }

        Dictionary<int, int> referenceCounts = lookupPermutation.Keys.ToDictionary(index => index, _ => 0);
        for (int offset = layout.BlocksStart; offset < layout.TrianglesStart; offset += 2)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(working.AsSpan(offset, 2));
            int triangleIndex = word & 0x7FFF;
            if (triangleIndex >= layout.TriangleCount)
            {
                reason = $"Collision block-list word at level-data +0x{offset:X} references out-of-range triangle {triangleIndex}.";
                return false;
            }

            if (!lookupPermutation.TryGetValue(triangleIndex, out int replacementIndex))
                continue;

            referenceCounts[triangleIndex]++;
            ushort replacementWord = (ushort)((word & 0x8000) | replacementIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(working.AsSpan(offset, 2), replacementWord);
        }

        for (int i = 0; i < freeStagingIndexes.Length; i++)
        {
            int stagingIndex = freeStagingIndexes[i];
            int targetIndex = remappedTargetIndexes[i];
            int stagingReferences = referenceCounts[stagingIndex];
            int targetReferences = referenceCounts[targetIndex];
            if (stagingReferences == 0 || targetReferences == 0)
            {
                reason = $"Collision triangle remap {stagingIndex}<->{targetIndex} is not fully represented in the native block list ({stagingReferences}/{targetReferences} references).";
                return false;
            }

            remaps.Add(new NativeTerrainSurfaceTriangleRemap(
                stagingIndex,
                targetIndex,
                stagingReferences,
                targetReferences));
        }

        foreach ((int originalTriangleIndex, NativeTerrainSurfaceFlagAssignment assignment) in normalized)
        {
            int writableIndex;
            if (originalTriangleIndex < layout.FlagCount || directlyPromoted.Contains(originalTriangleIndex))
            {
                writableIndex = originalTriangleIndex;
            }
            else
            {
                writableIndex = lookupPermutation[originalTriangleIndex];
            }

            byte highBits = originalTriangleIndex < layout.FlagCount
                ? (byte)(working[layout.FlagsStart + writableIndex] & 0xC0)
                : (byte)0xC0;
            working[layout.FlagsStart + writableIndex] = (byte)(highBits | assignment.SurfaceIndex);
        }

        if (!ValidatePromotedLayout(
                working,
                layout,
                newFlagCount,
                shiftBytes,
                usedLevelEnd,
                expectedSuffix,
                out reason))
        {
            return false;
        }

        byte[] before = sourceLevelData.AsSpan(collisionStart, outputRegionEnd - collisionStart).ToArray();
        byte[] after = working.AsSpan(collisionStart, outputRegionEnd - collisionStart).ToArray();
        plan = new NativeTerrainSurfaceFlagPromotionPlan(
            levelDataWadOffset + collisionStart,
            before,
            after,
            layout.TriangleCount,
            layout.FlagCount,
            newFlagCount,
            shiftBytes,
            zeroTailBytes,
            zeroTailBytes - shiftBytes,
            levelDataWadOffset + layout.CollisionEnd,
            levelDataWadOffset + newCollisionEnd,
            levelDataWadOffset + usedLevelEnd,
            levelDataWadOffset + newUsedLevelEnd,
            remaps.ToArray(),
            consumedPatchOffsets,
            normalized.Keys.Order().ToArray());
        return true;
    }

    private static bool TryBuildDirectFlagPlan(
        byte[] sourceLevelData,
        long levelDataWadOffset,
        CollisionLayout layout,
        IReadOnlyDictionary<int, NativeTerrainSurfaceFlagAssignment> assignments,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        int zeroTailBytes,
        int usedLevelEnd,
        out NativeTerrainSurfaceFlagPromotionPlan? plan,
        out string reason)
    {
        plan = null;
        reason = "";
        int firstFlagIndex = assignments.Keys.Min();
        int lastFlagIndex = assignments.Keys.Max();
        int patchStart = layout.FlagsStart + firstFlagIndex;
        int patchEnd = layout.FlagsStart + lastFlagIndex + 1;
        long patchWadStart = levelDataWadOffset + patchStart;
        long patchWadEnd = levelDataWadOffset + patchEnd;
        foreach (NativeTerrainSurfaceExistingPatch existing in existingPatches)
        {
            if (!TryValidateExistingPatch(existing, sourceLevelData, levelDataWadOffset, out int existingStart, out int existingEnd, out reason))
                return false;
            if (!RangesOverlap(existingStart, existingEnd, patchStart, patchEnd))
                continue;

            reason = $"Existing {existing.Kind} patch at WAD 0x{existing.WadOffset:X} overlaps native surface flags 0x{patchWadStart:X}-0x{patchWadEnd:X}.";
            return false;
        }

        byte[] before = sourceLevelData.AsSpan(patchStart, patchEnd - patchStart).ToArray();
        byte[] after = before.ToArray();
        foreach ((int triangleIndex, NativeTerrainSurfaceFlagAssignment assignment) in assignments)
        {
            int offset = triangleIndex - firstFlagIndex;
            after[offset] = (byte)((after[offset] & 0xC0) | assignment.SurfaceIndex);
        }

        plan = new NativeTerrainSurfaceFlagPromotionPlan(
            patchWadStart,
            before,
            after,
            layout.TriangleCount,
            layout.FlagCount,
            layout.FlagCount,
            0,
            zeroTailBytes,
            zeroTailBytes,
            levelDataWadOffset + layout.CollisionEnd,
            levelDataWadOffset + layout.CollisionEnd,
            levelDataWadOffset + usedLevelEnd,
            levelDataWadOffset + usedLevelEnd,
            Array.Empty<NativeTerrainSurfaceTriangleRemap>(),
            Array.Empty<long>(),
            assignments.Keys.Order().ToArray());
        return true;
    }

    private static bool TryComposeExistingPatches(
        byte[] sourceLevelData,
        long levelDataWadOffset,
        int collisionStart,
        int sourceSemanticEnd,
        int outputRegionEnd,
        CollisionLayout layout,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        out byte[]? working,
        out long[]? consumedPatchOffsets,
        out string reason)
    {
        working = sourceLevelData.ToArray();
        consumedPatchOffsets = null;
        reason = "";
        List<(NativeTerrainSurfaceExistingPatch Patch, int Start, int End)> consumed = [];
        foreach (NativeTerrainSurfaceExistingPatch existing in existingPatches)
        {
            if (!TryValidateExistingPatch(existing, sourceLevelData, levelDataWadOffset, out int start, out int end, out reason))
                return false;
            if (!RangesOverlap(start, end, collisionStart, outputRegionEnd))
                continue;
            if (start < collisionStart || end > sourceSemanticEnd)
            {
                reason = $"Existing {existing.Kind} patch at WAD 0x{existing.WadOffset:X} intersects padding consumed by native surface flag growth.";
                return false;
            }
            if (RangesOverlap(start, end, layout.CollisionStart, layout.CollisionBody + CollisionHeaderLength) ||
                RangesOverlap(start, end, layout.FlagsStart, layout.CollisionEnd))
            {
                reason = $"Existing {existing.Kind} patch at WAD 0x{existing.WadOffset:X} conflicts with the collision header or native flag table.";
                return false;
            }

            consumed.Add((existing, start, end));
        }

        (NativeTerrainSurfaceExistingPatch Patch, int Start, int End)[] ordered = consumed.OrderBy(item => item.Start).ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Start < ordered[i - 1].End)
            {
                reason = $"Existing {ordered[i - 1].Patch.Kind} and {ordered[i].Patch.Kind} patches overlap inside the collision promotion region.";
                return false;
            }
        }

        foreach ((NativeTerrainSurfaceExistingPatch patch, int start, _) in ordered)
            patch.After.CopyTo(working, start);

        consumedPatchOffsets = ordered.Select(item => item.Patch.WadOffset).ToArray();
        return true;
    }

    private static bool TryValidateExistingPatch(
        NativeTerrainSurfaceExistingPatch patch,
        byte[] sourceLevelData,
        long levelDataWadOffset,
        out int start,
        out int end,
        out string reason)
    {
        start = -1;
        end = -1;
        reason = "";
        if (patch.Before == null || patch.After == null || patch.Before.Length == 0 || patch.Before.Length != patch.After.Length)
        {
            reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} does not have equal non-empty before/after bytes.";
            return false;
        }

        long relative = patch.WadOffset - levelDataWadOffset;
        if (relative < 0 || relative > int.MaxValue || relative + patch.Before.Length > sourceLevelData.Length)
            return true; // A valid patch outside this level-data block cannot overlap the promotion range.

        start = (int)relative;
        end = checked(start + patch.Before.Length);
        if (!sourceLevelData.AsSpan(start, patch.Before.Length).SequenceEqual(patch.Before))
        {
            reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} does not match the source level-data bytes.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeAssignments(
        IReadOnlyList<NativeTerrainSurfaceFlagAssignment> assignments,
        int triangleCount,
        out Dictionary<int, NativeTerrainSurfaceFlagAssignment>? normalized,
        out string reason)
    {
        normalized = new Dictionary<int, NativeTerrainSurfaceFlagAssignment>();
        reason = "";
        foreach (NativeTerrainSurfaceFlagAssignment assignment in assignments)
        {
            if (assignment.TriangleIndex < 0 || assignment.TriangleIndex >= triangleCount || assignment.TriangleIndex > 0x7FFF)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} is outside the native 15-bit triangle table.";
                return false;
            }
            if (assignment.SurfaceIndex is < 0 or > 0x3F)
            {
                reason = $"Native surface index {assignment.SurfaceIndex} is outside 0..63.";
                return false;
            }

            if (normalized.TryGetValue(assignment.TriangleIndex, out NativeTerrainSurfaceFlagAssignment? existing) &&
                existing.SurfaceIndex != assignment.SurfaceIndex)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} was assigned both {existing.SurfaceLabel} and {assignment.SurfaceLabel}.";
                return false;
            }

            normalized[assignment.TriangleIndex] = assignment;
        }

        return true;
    }

    private static bool TryReadCollisionLayout(
        byte[] bytes,
        int collisionStart,
        out CollisionLayout? layout,
        out string reason)
    {
        layout = null;
        reason = "";
        if (collisionStart < 0 || collisionStart + 4 + CollisionHeaderLength > bytes.Length)
        {
            reason = "The collision component header is truncated.";
            return false;
        }

        int collisionLength = ReadInt32(bytes, collisionStart);
        int collisionEnd;
        try
        {
            collisionEnd = checked(collisionStart + collisionLength);
        }
        catch (OverflowException)
        {
            reason = "The collision component length overflows its level-data block.";
            return false;
        }
        if (collisionLength < 4 + CollisionHeaderLength || collisionEnd > bytes.Length)
        {
            reason = $"The collision component has invalid length 0x{collisionLength:X}.";
            return false;
        }

        int body = collisionStart + 4;
        int triangleCount = ReadInt32(bytes, body);
        int flagCount = ReadInt32(bytes, body + 4);
        if (triangleCount <= 0 || triangleCount > 0x8000 || flagCount < 0 || flagCount > triangleCount)
        {
            reason = $"The collision component has invalid triangle/flag counts {triangleCount}/{flagCount}.";
            return false;
        }

        int treeRelative = ReadInt32(bytes, body + 0x08);
        int blocksRelative = ReadInt32(bytes, body + 0x0C);
        int trianglesRelative = ReadInt32(bytes, body + 0x10);
        int occlusionRelative = ReadInt32(bytes, body + 0x14);
        int flagsRelative = ReadInt32(bytes, body + 0x18);
        int treeStart;
        int blocksStart;
        int trianglesStart;
        int occlusionStart;
        int flagsStart;
        try
        {
            treeStart = checked(body + treeRelative);
            blocksStart = checked(body + blocksRelative);
            trianglesStart = checked(body + trianglesRelative);
            occlusionStart = checked(body + occlusionRelative);
            flagsStart = checked(body + flagsRelative);
        }
        catch (OverflowException)
        {
            reason = "A collision component pointer overflows the level-data block.";
            return false;
        }

        if (treeStart != body + CollisionHeaderLength ||
            blocksStart <= treeStart ||
            trianglesStart <= blocksStart ||
            ((trianglesStart - blocksStart) & 1) != 0 ||
            trianglesStart + ((long)triangleCount * 12) != occlusionStart ||
            occlusionStart + triangleCount > flagsStart ||
            flagsStart > collisionEnd)
        {
            reason = "The collision component is not in the verified native tree/blocks/triangles/occlusion/flags layout.";
            return false;
        }

        int flagStorageBytes = collisionEnd - flagsStart;
        if (flagStorageBytes != Align4(flagCount) ||
            flagsStart + flagCount > collisionEnd ||
            bytes.AsSpan(flagsStart + flagCount, flagStorageBytes - flagCount).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The collision flag table is not the verified final Align4(flagCount) table with zero alignment bytes.";
            return false;
        }

        layout = new CollisionLayout(
            collisionStart,
            collisionLength,
            collisionEnd,
            body,
            triangleCount,
            flagCount,
            treeStart,
            blocksStart,
            trianglesStart,
            occlusionStart,
            flagsStart,
            flagStorageBytes);
        return true;
    }

    private static bool TryParseSuffix(byte[] bytes, int collisionEnd, out int usedLevelEnd, out string reason)
    {
        usedLevelEnd = -1;
        reason = "";
        if (!TryAdvanceComponent(bytes, collisionEnd, "cyclorama", out int offset, out reason))
            return false;
        if (!TryReadInt32(bytes, offset, out int portalCount) || portalCount is < 0 or > MaxPortalCount)
        {
            reason = "The native portal count after the cyclorama is invalid.";
            return false;
        }
        offset += 4;

        for (int portalIndex = 0; portalIndex < portalCount; portalIndex++)
        {
            if (!TryReadInt32(bytes, offset + 4, out int pointCount) || pointCount is < 1 or > MaxPortalPointCount)
            {
                reason = $"Portal {portalIndex} has an invalid native point count.";
                return false;
            }

            long skyboxStartLong = (long)offset + 0x40 + ((pointCount - 1L) * 12L);
            if (skyboxStartLong < 0 || skyboxStartLong > int.MaxValue)
            {
                reason = $"Portal {portalIndex}'s embedded skybox offset overflows the level-data block.";
                return false;
            }
            int skyboxStart = (int)skyboxStartLong;
            if (!TryAdvanceComponent(bytes, skyboxStart, $"portal {portalIndex} skybox", out offset, out reason))
                return false;
        }

        if (!TryAdvanceComponent(bytes, offset, "particle textures", out offset, out reason) ||
            !TryAdvanceComponent(bytes, offset, "sound table", out offset, out reason))
        {
            return false;
        }

        usedLevelEnd = offset;
        return true;
    }

    private static bool ValidatePromotedLayout(
        byte[] working,
        CollisionLayout oldLayout,
        int newFlagCount,
        int shiftBytes,
        int oldUsedLevelEnd,
        byte[] expectedSuffix,
        out string reason)
    {
        reason = "";
        if (!TryReadCollisionLayout(working, oldLayout.CollisionStart, out CollisionLayout? promoted, out reason) || promoted == null)
            return false;
        if (promoted.TriangleCount != oldLayout.TriangleCount ||
            promoted.FlagCount != newFlagCount ||
            promoted.CollisionEnd != oldLayout.CollisionEnd + shiftBytes ||
            promoted.TreeStart != oldLayout.TreeStart ||
            promoted.BlocksStart != oldLayout.BlocksStart ||
            promoted.TrianglesStart != oldLayout.TrianglesStart ||
            promoted.OcclusionStart != oldLayout.OcclusionStart ||
            promoted.FlagsStart != oldLayout.FlagsStart)
        {
            reason = "Collision flag promotion changed a native collision pointer or an unexpected count.";
            return false;
        }
        if (!TryParseSuffix(working, promoted.CollisionEnd, out int promotedUsedLevelEnd, out reason))
            return false;
        if (promotedUsedLevelEnd != oldUsedLevelEnd + shiftBytes)
        {
            reason = "The shifted suffix no longer ends at the expected level-data offset.";
            return false;
        }
        if (!working.AsSpan(promoted.CollisionEnd, expectedSuffix.Length).SequenceEqual(expectedSuffix))
        {
            reason = "The cyclorama/portal/particle/sound suffix was not preserved byte-for-byte during flag-table growth.";
            return false;
        }
        if (working.AsSpan(promotedUsedLevelEnd).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The promoted level-data block no longer has an all-zero tail.";
            return false;
        }

        return true;
    }

    private static bool TryAdvanceComponent(byte[] bytes, int start, string name, out int end, out string reason)
    {
        end = -1;
        reason = "";
        if (!TryReadInt32(bytes, start, out int length) || length < 4 || (long)start + length > bytes.Length)
        {
            reason = $"The native {name} component at level-data +0x{start:X} has an invalid length.";
            return false;
        }

        end = start + length;
        return true;
    }

    private static bool TryReadInt32(byte[] bytes, int offset, out int value)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        return true;
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static bool RangesOverlap(int firstStart, int firstEnd, int secondStart, int secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    private static void SwapBytes(byte[] bytes, int first, int second, int length)
    {
        for (int i = 0; i < length; i++)
            (bytes[first + i], bytes[second + i]) = (bytes[second + i], bytes[first + i]);
    }

    private sealed record CollisionLayout(
        int CollisionStart,
        int CollisionLength,
        int CollisionEnd,
        int CollisionBody,
        int TriangleCount,
        int FlagCount,
        int TreeStart,
        int BlocksStart,
        int TrianglesStart,
        int OcclusionStart,
        int FlagsStart,
        int FlagStorageBytes);
}
