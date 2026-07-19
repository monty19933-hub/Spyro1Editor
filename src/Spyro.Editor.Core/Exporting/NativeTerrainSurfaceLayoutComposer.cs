using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainSurfaceDescriptorImport(
    NativeTerrainSurfaceSignature Signature,
    byte[] RawRecord,
    string SourceLevelKey,
    int SourceSurfaceIndex,
    string RawSha256);

public sealed record NativeTerrainSurfaceBehaviorAssignment(
    int TriangleIndex,
    NativeTerrainSurfaceSignature Signature,
    NativeTerrainSurfaceDescriptorImport? Donor,
    string RuntimeKey,
    string SurfaceLabel);

public sealed record NativeTerrainSurfaceResolvedDescriptor(
    NativeTerrainSurfaceSignature Signature,
    int SurfaceIndex,
    bool Appended,
    int RawByteLength,
    string RawSha256,
    string SourceLevelKey,
    int SourceSurfaceIndex);

public sealed record NativeTerrainSurfaceLayoutPlan(
    long WadOffset,
    byte[] Before,
    byte[] After,
    int OldSurfaceCount,
    int NewSurfaceCount,
    int DescriptorGrowthBytes,
    long OldCollisionWadOffset,
    long NewCollisionWadOffset,
    int OldFlagCount,
    int NewFlagCount,
    int FlagGrowthBytes,
    int ZeroTailBytesBefore,
    int ZeroTailBytesAfter,
    long OldUsedLevelEndWadOffset,
    long NewUsedLevelEndWadOffset,
    IReadOnlyList<NativeTerrainSurfaceResolvedDescriptor> ResolvedDescriptors,
    IReadOnlyList<NativeTerrainSurfaceTriangleRemap> TriangleRemaps,
    IReadOnlyList<long> ConsumedPatchWadOffsets,
    IReadOnlyList<int> AssignedTriangleIndexes)
{
    public bool HasChanges => !Before.AsSpan().SequenceEqual(After);
}

/// <summary>
/// Resolves native terrain-surface signatures against the destination table,
/// appends missing portable descriptors without changing existing indexes, and
/// composes the relocated collision layout with bounded flag promotion.
/// </summary>
public static class NativeTerrainSurfaceLayoutComposer
{
    private const int CollisionHeaderLength = 0x1C;
    private const int OrdinarySurfaceIndex = 0x3F;
    private const int MaximumDescriptorCount = 63;

    public static bool TryBuild(
        byte[] sourceLevelData,
        NativeTerrainSurfaceSourceData source,
        IReadOnlyList<NativeTerrainSurfaceBehaviorAssignment> assignments,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        out NativeTerrainSurfaceLayoutPlan? plan,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(sourceLevelData);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(existingPatches);
        plan = null;
        reason = "";

        if (sourceLevelData.Length != source.LevelDataByteLength)
        {
            reason = $"The supplied level-data block is {sourceLevelData.Length} bytes, expected {source.LevelDataByteLength}.";
            return false;
        }
        if (assignments.Count == 0)
        {
            reason = "No native surface assignments were requested.";
            return false;
        }

        if (!TryRelativeOffset(source.SpecialSurfaceComponentWadOffset, source.LevelDataWadOffset, sourceLevelData.Length, out int specialStart) ||
            !TryRelativeOffset(source.CollisionComponentWadOffset, source.LevelDataWadOffset, sourceLevelData.Length, out int collisionStart) ||
            collisionStart <= specialStart)
        {
            reason = "The native special-surface or collision component is outside the level-data block.";
            return false;
        }

        int specialLength = ReadInt32(sourceLevelData, specialStart);
        if (specialLength < 8 || specialStart + specialLength != collisionStart)
        {
            reason = "The special-surface component does not end at the decoded collision component.";
            return false;
        }
        if (ReadInt32(sourceLevelData, specialStart + 4) != source.SpecialSurfaces.Count)
        {
            reason = "The special-surface count no longer matches the decoded source table.";
            return false;
        }

        if (!TryNormalizeAssignments(
                assignments,
                source.Collision.TriangleCount,
                out Dictionary<int, NativeTerrainSurfaceBehaviorAssignment>? normalized,
                out reason) ||
            normalized == null)
        {
            return false;
        }

        if (!TryResolveDescriptors(
                source,
                normalized.Values,
                out Dictionary<NativeTerrainSurfaceSignature, NativeTerrainSurfaceResolvedDescriptor>? descriptorsBySignature,
                out NativeTerrainSurfaceDescriptorImport[]? appendedImports,
                out reason) ||
            descriptorsBySignature == null ||
            appendedImports == null)
        {
            return false;
        }

        NativeTerrainSurfaceFlagAssignment[] resolvedAssignments = normalized.Values
            .Select(assignment => new NativeTerrainSurfaceFlagAssignment(
                assignment.TriangleIndex,
                descriptorsBySignature[assignment.Signature].SurfaceIndex,
                assignment.RuntimeKey,
                assignment.SurfaceLabel))
            .OrderBy(assignment => assignment.TriangleIndex)
            .ToArray();

        int descriptorGrowthBytes = checked(
            (appendedImports.Length * 4) +
            appendedImports.Sum(import => import.RawRecord.Length));
        if (descriptorGrowthBytes == 0)
        {
            return TryBuildReuseOnly(
                sourceLevelData,
                source,
                resolvedAssignments,
                existingPatches,
                descriptorsBySignature.Values,
                out plan,
                out reason);
        }

        int oldUsedLevelEnd = checked(sourceLevelData.Length - source.FlagPromotionCapacity.VerifiedZeroTailBytes);
        if (oldUsedLevelEnd < collisionStart || oldUsedLevelEnd > sourceLevelData.Length)
        {
            reason = "The decoded zero-tail capacity does not resolve to a valid used level-data end.";
            return false;
        }
        if (sourceLevelData.AsSpan(oldUsedLevelEnd).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The bytes after the decoded used level-data end are no longer all zero.";
            return false;
        }

        int promotedAssignmentCount = normalized.Keys.Count(index => index >= source.Collision.FlagCount);
        int predictedNewFlagCount = checked(source.Collision.FlagCount + promotedAssignmentCount);
        if (predictedNewFlagCount > source.Collision.TriangleCount)
        {
            reason = $"The requested assignments need flag count {predictedNewFlagCount}, beyond triangle count {source.Collision.TriangleCount}.";
            return false;
        }
        int predictedFlagGrowth = checked(Align4(predictedNewFlagCount) - Align4(source.Collision.FlagCount));
        int predictedTotalGrowth = checked(descriptorGrowthBytes + predictedFlagGrowth);
        if (predictedTotalGrowth > source.FlagPromotionCapacity.VerifiedZeroTailBytes)
        {
            reason = $"Descriptor and collision growth need {predictedTotalGrowth} bytes, but only {source.FlagPromotionCapacity.VerifiedZeroTailBytes} verified zero-tail bytes remain.";
            return false;
        }

        byte[] working = sourceLevelData.ToArray();
        int predictedOutputEnd = checked(oldUsedLevelEnd + predictedTotalGrowth);
        if (!TryComposeRelocatedPatches(
                sourceLevelData,
                working,
                source.LevelDataWadOffset,
                specialStart,
                collisionStart,
                oldUsedLevelEnd,
                predictedOutputEnd,
                source.Collision.FlagsWadOffset,
                existingPatches,
                out long[]? consumedPatchOffsets,
                out reason) ||
            consumedPatchOffsets == null)
        {
            return false;
        }

        if (!TryBuildExpandedSpecialSurfaceComponent(
                working,
                specialStart,
                collisionStart,
                appendedImports,
                out byte[]? expandedComponent,
                out reason) ||
            expandedComponent == null)
        {
            return false;
        }

        byte[] collisionAndSuffix = working
            .AsSpan(collisionStart, oldUsedLevelEnd - collisionStart)
            .ToArray();
        int relocatedCollisionStart = checked(collisionStart + descriptorGrowthBytes);
        int descriptorShiftedUsedEnd = checked(oldUsedLevelEnd + descriptorGrowthBytes);
        Array.Clear(working, specialStart, descriptorShiftedUsedEnd - specialStart);
        expandedComponent.CopyTo(working, specialStart);
        collisionAndSuffix.CopyTo(working, relocatedCollisionStart);

        if (!NativeTerrainSurfaceFlagPromoter.TryBuild(
                working,
                source.LevelDataWadOffset,
                source.LevelDataWadOffset + relocatedCollisionStart,
                resolvedAssignments,
                Array.Empty<NativeTerrainSurfaceExistingPatch>(),
                out NativeTerrainSurfaceFlagPromotionPlan? promotion,
                out reason) ||
            promotion == null)
        {
            reason = $"The relocated descriptor table was built, but collision flag composition failed: {reason}";
            return false;
        }
        if (!TryApplyPromotion(working, source.LevelDataWadOffset, promotion, out reason))
            return false;

        int finalUsedLevelEnd = checked((int)(promotion.NewUsedLevelEndWadOffset - source.LevelDataWadOffset));
        if (finalUsedLevelEnd != predictedOutputEnd)
        {
            reason = $"The composed layout ended at level-data +0x{finalUsedLevelEnd:X}, expected +0x{predictedOutputEnd:X}.";
            return false;
        }

        NativeTerrainSurfaceResolvedDescriptor[] resolvedDescriptors = descriptorsBySignature.Values
            .OrderBy(descriptor => descriptor.SurfaceIndex)
            .ToArray();
        if (!TryVerifyFinalLayout(
                working,
                source,
                normalized.Values,
                resolvedDescriptors,
                promotion,
                descriptorGrowthBytes,
                out reason))
        {
            return false;
        }

        byte[] before = sourceLevelData
            .AsSpan(specialStart, finalUsedLevelEnd - specialStart)
            .ToArray();
        byte[] after = working
            .AsSpan(specialStart, finalUsedLevelEnd - specialStart)
            .ToArray();
        plan = new NativeTerrainSurfaceLayoutPlan(
            source.LevelDataWadOffset + specialStart,
            before,
            after,
            source.SpecialSurfaces.Count,
            source.SpecialSurfaces.Count + appendedImports.Length,
            descriptorGrowthBytes,
            source.CollisionComponentWadOffset,
            source.LevelDataWadOffset + relocatedCollisionStart,
            source.Collision.FlagCount,
            promotion.NewFlagCount,
            promotion.ShiftBytes,
            source.FlagPromotionCapacity.VerifiedZeroTailBytes,
            source.FlagPromotionCapacity.VerifiedZeroTailBytes - descriptorGrowthBytes - promotion.ShiftBytes,
            source.LevelDataWadOffset + oldUsedLevelEnd,
            source.LevelDataWadOffset + finalUsedLevelEnd,
            resolvedDescriptors,
            promotion.TriangleRemaps,
            consumedPatchOffsets,
            promotion.AssignedTriangleIndexes);
        return true;
    }

    private static bool TryBuildReuseOnly(
        byte[] sourceLevelData,
        NativeTerrainSurfaceSourceData source,
        IReadOnlyList<NativeTerrainSurfaceFlagAssignment> assignments,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        IEnumerable<NativeTerrainSurfaceResolvedDescriptor> resolvedDescriptors,
        out NativeTerrainSurfaceLayoutPlan? plan,
        out string reason)
    {
        plan = null;
        if (!NativeTerrainSurfaceFlagPromoter.TryBuild(
                sourceLevelData,
                source.LevelDataWadOffset,
                source.CollisionComponentWadOffset,
                assignments,
                existingPatches,
                out NativeTerrainSurfaceFlagPromotionPlan? promotion,
                out reason) ||
            promotion == null)
        {
            return false;
        }

        byte[] final = sourceLevelData.ToArray();
        if (!TryApplyPromotion(final, source.LevelDataWadOffset, promotion, out reason))
            return false;
        NativeTerrainSurfaceResolvedDescriptor[] orderedDescriptors = resolvedDescriptors
            .OrderBy(descriptor => descriptor.SurfaceIndex)
            .ToArray();
        NativeTerrainSurfaceBehaviorAssignment[] semanticAssignments = assignments
            .Select(assignment => new NativeTerrainSurfaceBehaviorAssignment(
                assignment.TriangleIndex,
                orderedDescriptors.Single(descriptor => descriptor.SurfaceIndex == assignment.SurfaceIndex).Signature,
                null,
                assignment.RuntimeKey,
                assignment.SurfaceLabel))
            .ToArray();
        if (!TryVerifyFinalLayout(
                final,
                source,
                semanticAssignments,
                orderedDescriptors,
                promotion,
                descriptorGrowthBytes: 0,
                out reason))
        {
            return false;
        }

        plan = new NativeTerrainSurfaceLayoutPlan(
            promotion.WadOffset,
            promotion.Before,
            promotion.After,
            source.SpecialSurfaces.Count,
            source.SpecialSurfaces.Count,
            0,
            source.CollisionComponentWadOffset,
            source.CollisionComponentWadOffset,
            promotion.OldFlagCount,
            promotion.NewFlagCount,
            promotion.ShiftBytes,
            promotion.ZeroTailBytesBefore,
            promotion.ZeroTailBytesAfter,
            promotion.OldUsedLevelEndWadOffset,
            promotion.NewUsedLevelEndWadOffset,
            orderedDescriptors,
            promotion.TriangleRemaps,
            promotion.ConsumedPatchWadOffsets,
            promotion.AssignedTriangleIndexes);
        return true;
    }

    private static bool TryNormalizeAssignments(
        IReadOnlyList<NativeTerrainSurfaceBehaviorAssignment> assignments,
        int triangleCount,
        out Dictionary<int, NativeTerrainSurfaceBehaviorAssignment>? normalized,
        out string reason)
    {
        normalized = new Dictionary<int, NativeTerrainSurfaceBehaviorAssignment>();
        reason = "";
        foreach (NativeTerrainSurfaceBehaviorAssignment assignment in assignments)
        {
            if (assignment.Signature == null)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} has no native surface signature.";
                return false;
            }
            if (assignment.TriangleIndex < 0 || assignment.TriangleIndex >= triangleCount || assignment.TriangleIndex > 0x7FFF)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} is outside the native triangle table.";
                return false;
            }
            if (normalized.TryGetValue(assignment.TriangleIndex, out NativeTerrainSurfaceBehaviorAssignment? existing) &&
                existing.Signature != assignment.Signature)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} was assigned both {existing.SurfaceLabel} and {assignment.SurfaceLabel}.";
                return false;
            }

            normalized[assignment.TriangleIndex] = assignment;
        }

        return true;
    }

    private static bool TryResolveDescriptors(
        NativeTerrainSurfaceSourceData source,
        IEnumerable<NativeTerrainSurfaceBehaviorAssignment> assignments,
        out Dictionary<NativeTerrainSurfaceSignature, NativeTerrainSurfaceResolvedDescriptor>? resolved,
        out NativeTerrainSurfaceDescriptorImport[]? appended,
        out string reason)
    {
        resolved = new Dictionary<NativeTerrainSurfaceSignature, NativeTerrainSurfaceResolvedDescriptor>();
        appended = null;
        reason = "";
        List<NativeTerrainSurfaceDescriptorImport> imports = [];
        NativeTerrainSurfaceBehaviorAssignment[] assignmentArray = assignments.ToArray();
        NativeTerrainSurfaceSignature[] signatures = assignmentArray
            .Select(assignment => assignment.Signature)
            .Distinct()
            .OrderBy(signature => signature.SurfaceType)
            .ThenBy(signature => signature.Param1)
            .ThenBy(signature => signature.Param2)
            .ToArray();

        foreach (NativeTerrainSurfaceSignature signature in signatures)
        {
            if (signature.IsOrdinary)
            {
                resolved[signature] = new NativeTerrainSurfaceResolvedDescriptor(
                    signature,
                    OrdinarySurfaceIndex,
                    false,
                    0,
                    "",
                    source.LevelKey,
                    OrdinarySurfaceIndex);
                continue;
            }

            PortalSpecialSurfaceRecord? existing = source.SpecialSurfaces.FirstOrDefault(surface =>
                surface.Type == signature.SurfaceType &&
                surface.Param1 == signature.Param1 &&
                surface.Param2 == signature.Param2);
            if (existing != null)
            {
                resolved[signature] = new NativeTerrainSurfaceResolvedDescriptor(
                    signature,
                    existing.Index,
                    false,
                    existing.RawBytes.Length,
                    Sha256(existing.RawBytes),
                    source.LevelKey,
                    existing.Index);
                continue;
            }

            if (!signature.IsCrossLevelPortable)
            {
                reason = $"{signature.Label} is not a portable native descriptor and cannot be appended to {source.LevelName}.";
                return false;
            }

            NativeTerrainSurfaceDescriptorImport[] candidates = assignmentArray
                .Where(assignment => assignment.Signature == signature && assignment.Donor != null)
                .Select(assignment => assignment.Donor!)
                .ToArray();
            if (candidates.Length == 0)
            {
                reason = $"{source.LevelName} has no {signature.Label} descriptor, and the assignment does not carry one raw donor record.";
                return false;
            }
            foreach (NativeTerrainSurfaceDescriptorImport candidate in candidates)
            {
                if (!TryValidateImport(candidate, signature, out reason))
                    return false;
            }
            NativeTerrainSurfaceDescriptorImport donor = candidates[0];
            if (candidates.Skip(1).Any(candidate => !candidate.RawRecord.AsSpan().SequenceEqual(donor.RawRecord)))
            {
                reason = $"Assignments for {signature.Label} carry conflicting raw donor records.";
                return false;
            }

            imports.Add(new NativeTerrainSurfaceDescriptorImport(
                signature,
                donor.RawRecord.ToArray(),
                donor.SourceLevelKey,
                donor.SourceSurfaceIndex,
                Sha256(donor.RawRecord)));
        }

        if (source.SpecialSurfaces.Count + imports.Count > MaximumDescriptorCount)
        {
            reason = $"Appending {imports.Count} descriptor(s) would raise the native table from {source.SpecialSurfaces.Count} to {source.SpecialSurfaces.Count + imports.Count}; index 63 is reserved for ordinary terrain.";
            return false;
        }

        for (int importIndex = 0; importIndex < imports.Count; importIndex++)
        {
            NativeTerrainSurfaceDescriptorImport donor = imports[importIndex];
            int targetIndex = source.SpecialSurfaces.Count + importIndex;
            resolved[donor.Signature] = new NativeTerrainSurfaceResolvedDescriptor(
                donor.Signature,
                targetIndex,
                true,
                donor.RawRecord.Length,
                Sha256(donor.RawRecord),
                donor.SourceLevelKey,
                donor.SourceSurfaceIndex);
        }

        appended = imports.ToArray();
        return true;
    }

    private static bool TryValidateImport(
        NativeTerrainSurfaceDescriptorImport import,
        NativeTerrainSurfaceSignature expectedSignature,
        out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(import.SourceLevelKey) || import.SourceSurfaceIndex is < 0 or >= MaximumDescriptorCount)
        {
            reason = $"The raw donor record for {expectedSignature.Label} is missing valid source-level/index provenance.";
            return false;
        }
        if (import.Signature != expectedSignature)
        {
            reason = $"The donor signature {import.Signature.Label} does not match requested {expectedSignature.Label}.";
            return false;
        }
        if (import.RawRecord == null || import.RawRecord.Length < 4 || (import.RawRecord.Length & 3) != 0)
        {
            reason = $"The raw donor record for {expectedSignature.Label} must contain at least one aligned 4-byte word.";
            return false;
        }
        int expectedLength = expectedSignature.SurfaceType switch
        {
            0 => 12,
            4 or 5 => 4,
            _ => -1
        };
        if (expectedLength < 0)
        {
            reason = $"{expectedSignature.Label} does not have an approved portable raw-record layout.";
            return false;
        }
        if (import.RawRecord.Length != expectedLength)
        {
            reason = $"The raw donor record for {expectedSignature.Label} is {import.RawRecord.Length} bytes; the proven layout requires {expectedLength}.";
            return false;
        }

        int type = import.RawRecord[0];
        int param1 = import.RawRecord.Length >= 8 ? ReadInt32(import.RawRecord, 4) : 0;
        int param2 = import.RawRecord.Length >= 12 ? ReadInt32(import.RawRecord, 8) : 0;
        if (type != expectedSignature.SurfaceType || param1 != expectedSignature.Param1 || param2 != expectedSignature.Param2)
        {
            reason = $"The raw donor record decodes as native surface {type} ({param1}, {param2}), not {expectedSignature.SurfaceType} ({expectedSignature.Param1}, {expectedSignature.Param2}).";
            return false;
        }

        string actualSha = Sha256(import.RawRecord);
        if (!string.IsNullOrWhiteSpace(import.RawSha256) &&
            !string.Equals(actualSha, import.RawSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            reason = $"The raw donor record SHA-256 is {actualSha}, not the staged value {import.RawSha256}.";
            return false;
        }

        return true;
    }

    private static bool TryComposeRelocatedPatches(
        byte[] sourceLevelData,
        byte[] working,
        long levelDataWadOffset,
        int specialStart,
        int collisionStart,
        int usedLevelEnd,
        int outputEnd,
        long flagsWadOffset,
        IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
        out long[]? consumedPatchOffsets,
        out string reason)
    {
        consumedPatchOffsets = null;
        reason = "";
        int collisionHeaderEnd = checked(collisionStart + 4 + CollisionHeaderLength);
        long flagsRelativeLong = flagsWadOffset - levelDataWadOffset;
        if (flagsRelativeLong < collisionHeaderEnd || flagsRelativeLong > usedLevelEnd)
        {
            reason = "The decoded collision flag table is outside the relocatable collision component.";
            return false;
        }
        int flagsStart = (int)flagsRelativeLong;
        int collisionEnd = checked(collisionStart + ReadInt32(sourceLevelData, collisionStart));

        List<(NativeTerrainSurfaceExistingPatch Patch, int Start, int End)> consumed = [];
        foreach (NativeTerrainSurfaceExistingPatch patch in existingPatches)
        {
            if (patch.Before == null || patch.After == null || patch.Before.Length == 0 || patch.Before.Length != patch.After.Length)
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} does not have equal non-empty before/after bytes.";
                return false;
            }

            long startLong = patch.WadOffset - levelDataWadOffset;
            long endLong = startLong + patch.Before.Length;
            if (!RangesOverlap(startLong, endLong, specialStart, outputEnd))
                continue;
            if (startLong < 0 || endLong > sourceLevelData.Length)
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} crosses the native level-data boundary.";
                return false;
            }

            int start = (int)startLong;
            int end = (int)endLong;
            if (RangesOverlap(start, end, usedLevelEnd, outputEnd))
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} owns zero padding needed by native surface layout growth.";
                return false;
            }
            if (start < collisionStart || end > usedLevelEnd)
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} overlaps the special-surface layout or crosses its relocatable suffix.";
                return false;
            }
            if (RangesOverlap(start, end, collisionStart, collisionHeaderEnd) ||
                RangesOverlap(start, end, flagsStart, collisionEnd))
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} conflicts with the collision header or native flag table.";
                return false;
            }
            if (!sourceLevelData.AsSpan(start, patch.Before.Length).SequenceEqual(patch.Before))
            {
                reason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} does not match the source level-data bytes.";
                return false;
            }

            consumed.Add((patch, start, end));
        }

        (NativeTerrainSurfaceExistingPatch Patch, int Start, int End)[] ordered = consumed
            .OrderBy(item => item.Start)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Start < ordered[index - 1].End)
            {
                reason = $"Existing {ordered[index - 1].Patch.Kind} and {ordered[index].Patch.Kind} patches overlap inside the relocated level-data suffix.";
                return false;
            }
        }

        foreach ((NativeTerrainSurfaceExistingPatch patch, int start, _) in ordered)
            patch.After.CopyTo(working, start);
        consumedPatchOffsets = ordered.Select(item => item.Patch.WadOffset).ToArray();
        return true;
    }

    private static bool TryBuildExpandedSpecialSurfaceComponent(
        byte[] working,
        int specialStart,
        int collisionStart,
        IReadOnlyList<NativeTerrainSurfaceDescriptorImport> imports,
        out byte[]? expanded,
        out string reason)
    {
        expanded = null;
        reason = "";
        int oldLength = ReadInt32(working, specialStart);
        int oldCount = ReadInt32(working, specialStart + 4);
        int oldBodyLength = oldLength - 4;
        int oldTableEndRelative = checked(4 + (oldCount * 4));
        if (oldLength < 8 || specialStart + oldLength != collisionStart ||
            oldCount is < 0 or > MaximumDescriptorCount ||
            oldTableEndRelative > oldBodyLength)
        {
            reason = "The source special-surface component does not have one valid count/pointer/record layout.";
            return false;
        }

        int addedPointerBytes = checked(imports.Count * 4);
        int addedRecordBytes = imports.Sum(import => import.RawRecord.Length);
        int newLength = checked(oldLength + addedPointerBytes + addedRecordBytes);
        byte[] result = new byte[newLength];
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), newLength);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4, 4), oldCount + imports.Count);

        for (int index = 0; index < oldCount; index++)
        {
            int pointer = ReadInt32(working, specialStart + 8 + (index * 4));
            if (pointer < oldTableEndRelative || pointer >= oldBodyLength)
            {
                reason = $"Existing special-surface pointer {index} at body +0x{pointer:X} is outside the native record blob.";
                return false;
            }
            BinaryPrimitives.WriteInt32LittleEndian(
                result.AsSpan(8 + (index * 4), 4),
                checked(pointer + addedPointerBytes));
        }

        int oldBlobStart = checked(specialStart + 4 + oldTableEndRelative);
        int oldBlobLength = checked(oldBodyLength - oldTableEndRelative);
        int newBlobStart = checked(4 + oldTableEndRelative + addedPointerBytes);
        working.AsSpan(oldBlobStart, oldBlobLength).CopyTo(result.AsSpan(newBlobStart));

        int nextRecordRelative = checked(oldBodyLength + addedPointerBytes);
        int nextRecordOffset = checked(4 + nextRecordRelative);
        for (int importIndex = 0; importIndex < imports.Count; importIndex++)
        {
            int pointerSlot = checked(8 + ((oldCount + importIndex) * 4));
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(pointerSlot, 4), nextRecordRelative);
            NativeTerrainSurfaceDescriptorImport import = imports[importIndex];
            import.RawRecord.CopyTo(result, nextRecordOffset);
            nextRecordRelative = checked(nextRecordRelative + import.RawRecord.Length);
            nextRecordOffset = checked(nextRecordOffset + import.RawRecord.Length);
        }
        if (nextRecordOffset != result.Length)
        {
            reason = "The expanded special-surface record blob did not end at its component boundary.";
            return false;
        }

        expanded = result;
        return true;
    }

    private static bool TryApplyPromotion(
        byte[] working,
        long levelDataWadOffset,
        NativeTerrainSurfaceFlagPromotionPlan promotion,
        out string reason)
    {
        reason = "";
        long relativeLong = promotion.WadOffset - levelDataWadOffset;
        if (relativeLong < 0 || relativeLong > int.MaxValue || relativeLong + promotion.After.Length > working.Length)
        {
            reason = "The collision promotion patch is outside the prepared level-data block.";
            return false;
        }
        int relative = (int)relativeLong;
        if (!working.AsSpan(relative, promotion.Before.Length).SequenceEqual(promotion.Before))
        {
            reason = "The prepared collision bytes do not match the promotion plan's before-image.";
            return false;
        }
        promotion.After.CopyTo(working, relative);
        return true;
    }

    private static bool TryVerifyFinalLayout(
        byte[] finalLevelData,
        NativeTerrainSurfaceSourceData source,
        IEnumerable<NativeTerrainSurfaceBehaviorAssignment> assignments,
        IReadOnlyList<NativeTerrainSurfaceResolvedDescriptor> resolvedDescriptors,
        NativeTerrainSurfaceFlagPromotionPlan promotion,
        int descriptorGrowthBytes,
        out string reason)
    {
        reason = "";
        NativeTerrainSurfaceSourceData parsed;
        try
        {
            parsed = PortalSourceDataLocator.ParseTerrainSurfaces(
                finalLevelData,
                source.LevelDataWadOffset,
                source.LevelKey,
                source.LevelName);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or OverflowException)
        {
            reason = $"The composed native terrain-surface layout did not reparse: {ex.Message}";
            return false;
        }

        if (parsed.SpecialSurfaces.Count != source.SpecialSurfaces.Count + resolvedDescriptors.Count(descriptor => descriptor.Appended))
        {
            reason = "The reparsed special-surface count does not match the resolved descriptor plan.";
            return false;
        }
        int addedPointerBytes = checked(resolvedDescriptors.Count(descriptor => descriptor.Appended) * 4);
        foreach (PortalSpecialSurfaceRecord oldRecord in source.SpecialSurfaces)
        {
            PortalSpecialSurfaceRecord reparsed = parsed.SpecialSurfaces[oldRecord.Index];
            if (reparsed.RelativeOffset != oldRecord.RelativeOffset + addedPointerBytes ||
                !reparsed.RawBytes.AsSpan().SequenceEqual(oldRecord.RawBytes))
            {
                reason = $"Existing special-surface record {oldRecord.Index} changed while the descriptor table was composed.";
                return false;
            }
        }
        foreach (NativeTerrainSurfaceResolvedDescriptor descriptor in resolvedDescriptors.Where(descriptor => descriptor.Appended))
        {
            PortalSpecialSurfaceRecord reparsed = parsed.SpecialSurfaces[descriptor.SurfaceIndex];
            if (reparsed.Type != descriptor.Signature.SurfaceType ||
                reparsed.Param1 != descriptor.Signature.Param1 ||
                reparsed.Param2 != descriptor.Signature.Param2 ||
                reparsed.RawBytes.Length != descriptor.RawByteLength ||
                !string.Equals(Sha256(reparsed.RawBytes), descriptor.RawSha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Appended descriptor {descriptor.SurfaceIndex} did not survive raw semantic readback.";
                return false;
            }
        }

        if (parsed.CollisionComponentWadOffset != source.CollisionComponentWadOffset + descriptorGrowthBytes ||
            parsed.Collision.TriangleCount != source.Collision.TriangleCount ||
            parsed.Collision.FlagCount != promotion.NewFlagCount)
        {
            reason = "The reparsed collision component moved or changed counts differently than planned.";
            return false;
        }

        Dictionary<NativeTerrainSurfaceSignature, int> indexes = resolvedDescriptors
            .ToDictionary(descriptor => descriptor.Signature, descriptor => descriptor.SurfaceIndex);
        Dictionary<int, int> remappedTargets = promotion.TriangleRemaps
            .ToDictionary(remap => remap.TargetTriangleIndex, remap => remap.StagingTriangleIndex);
        foreach (NativeTerrainSurfaceBehaviorAssignment assignment in assignments
                     .GroupBy(assignment => assignment.TriangleIndex)
                     .Select(group => group.Last()))
        {
            int finalTriangleIndex = remappedTargets.TryGetValue(assignment.TriangleIndex, out int stagingIndex)
                ? stagingIndex
                : assignment.TriangleIndex;
            NativeCollisionSurfaceTriangle triangle = parsed.CollisionSurfaceTriangles[finalTriangleIndex];
            int expectedIndex = indexes[assignment.Signature];
            if (triangle.SurfaceIndex != expectedIndex)
            {
                reason = $"Collision triangle {assignment.TriangleIndex} resolved through final index {finalTriangleIndex} to surface {triangle.SurfaceIndex}, expected {expectedIndex}.";
                return false;
            }
            if (!assignment.Signature.IsOrdinary &&
                (triangle.SurfaceType != assignment.Signature.SurfaceType ||
                 triangle.Param1 != assignment.Signature.Param1 ||
                 triangle.Param2 != assignment.Signature.Param2))
            {
                reason = $"Collision triangle {assignment.TriangleIndex} did not reparse with {assignment.Signature.Label}.";
                return false;
            }
        }

        if (finalLevelData.AsSpan(finalLevelData.Length - parsed.FlagPromotionCapacity.VerifiedZeroTailBytes).IndexOfAnyExcept((byte)0) >= 0)
        {
            reason = "The composed level-data tail is not all zero after semantic readback.";
            return false;
        }

        return true;
    }

    private static bool TryRelativeOffset(long wadOffset, long baseWadOffset, int length, out int relative)
    {
        long value = wadOffset - baseWadOffset;
        if (value < 0 || value > int.MaxValue || value >= length)
        {
            relative = -1;
            return false;
        }
        relative = (int)value;
        return true;
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException($"Native surface layout word at +0x{offset:X} is truncated.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool RangesOverlap(long firstStart, long firstEnd, long secondStart, long secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;
}
