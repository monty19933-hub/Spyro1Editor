using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record CrossLevelActorPackageSubfileLayout(
    int SubfileIndex,
    uint Start,
    uint EndExclusive,
    IReadOnlyList<uint> NativeRoots,
    IReadOnlyList<ushort> NativeActorIds)
{
    public uint Length => EndExclusive - Start;
    public int NativeRootCount => NativeRoots.Count(root => root != 0);
    public uint LastNativeRoot => NativeRoots.LastOrDefault(root => root != 0);
}

internal sealed record CrossLevelActorPackageRecipeSafety(
    bool Safe,
    string Reason,
    CrossLevelActorPackageSubfileLayout? Layout)
{
    public static CrossLevelActorPackageRecipeSafety Blocked(string reason, CrossLevelActorPackageSubfileLayout? layout = null) =>
        new(false, reason, layout);

    public static CrossLevelActorPackageRecipeSafety Allowed(CrossLevelActorPackageSubfileLayout layout) =>
        new(
            true,
            $"Actor-package ranges stay inside native subfile {layout.SubfileIndex} " +
            $"[0x{layout.Start:X},0x{layout.EndExclusive:X}), and the proposed root table remains contiguous and strictly ascending.",
            layout);
}

/// <summary>
/// Guards cross-level actor-package writes against zero-filled space that belongs to a
/// different nested level subfile. The game consumes actor roots as an ordered table,
/// so a byte-for-byte package copy is unsafe unless both its storage and its proposed
/// root-table shape agree with the target level's native actor/model subfile.
/// </summary>
internal static class CrossLevelActorPackageLayoutSafety
{
    private const int NestedHeaderBytes = 0x800;
    private const int FirstNestedSubfileOffset = 0x800;
    private const int RootTableOffset = 0x50;
    private const int ActorIdTableOffset = 0x150;
    private const int RootSlotCount = 64;

    public static bool TryReadLayout(
        FileStream stream,
        DiscLayout discLayout,
        int wadLba,
        long entryBase,
        out CrossLevelActorPackageSubfileLayout? layout,
        out string reason)
    {
        layout = null;
        reason = "";

        byte[] header;
        try
        {
            header = DiscImage.ReadFileBytes(stream, discLayout, wadLba, entryBase, NestedHeaderBytes);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            reason = $"Could not read the nested level header: {exception.Message}";
            return false;
        }

        List<NestedSubfile> subfiles = [];
        uint expectedOffset = FirstNestedSubfileOffset;
        // The root table begins at 0x50, so only the ten 8-byte pairs before it can be
        // nested-subfile descriptors. Never reinterpret actor roots as more subfiles.
        for (int index = 0; index < RootTableOffset / 8; index++)
        {
            int descriptorOffset = index * 8;
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(descriptorOffset, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(descriptorOffset + 4, 4));
            if (offset == 0 && size == 0)
                break;
            if (offset != expectedOffset || size == 0)
                break;

            uint end;
            try
            {
                end = checked(offset + size);
            }
            catch (OverflowException)
            {
                reason = $"Nested subfile {index} overflows its 32-bit level-relative range.";
                return false;
            }

            subfiles.Add(new NestedSubfile(index, offset, end));
            expectedOffset = end;
        }

        if (subfiles.Count == 0 || subfiles[0].Start != FirstNestedSubfileOffset)
        {
            reason = "The target does not have a packed nested level-subfile header beginning at 0x800.";
            return false;
        }

        uint[] roots = new uint[RootSlotCount];
        ushort[] actorIds = new ushort[RootSlotCount];
        bool reachedTerminator = false;
        uint previousRoot = 0;
        NestedSubfile? actorSubfile = null;
        for (int index = 0; index < RootSlotCount; index++)
        {
            uint root = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(RootTableOffset + (index * 4), 4));
            ushort actorId = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(ActorIdTableOffset + (index * 2), 2));
            roots[index] = root;
            actorIds[index] = actorId;

            if (root == 0)
            {
                reachedTerminator = true;
                continue;
            }

            if (reachedTerminator)
            {
                reason = $"The native actor-root table is non-contiguous: index {index} is populated after its first zero terminator.";
                return false;
            }
            if (previousRoot != 0 && root <= previousRoot)
            {
                reason = $"The native actor-root table is not strictly ascending at index {index}: 0x{root:X} follows 0x{previousRoot:X}.";
                return false;
            }

            NestedSubfile? containingSubfile = subfiles.FirstOrDefault(subfile => root >= subfile.Start && root < subfile.EndExclusive);
            if (containingSubfile == null)
            {
                reason = $"Native actor root 0x{root:X} at index {index} is outside every packed nested subfile.";
                return false;
            }
            if (actorSubfile != null && containingSubfile.Index != actorSubfile.Index)
            {
                reason = $"Native actor roots span nested subfiles {actorSubfile.Index} and {containingSubfile.Index}; actor-package ownership cannot be inferred safely.";
                return false;
            }

            actorSubfile = containingSubfile;
            previousRoot = root;
        }

        if (actorSubfile == null || previousRoot == 0)
        {
            reason = "The target has no native actor roots from which to infer its actor/model subfile.";
            return false;
        }

        layout = new CrossLevelActorPackageSubfileLayout(
            actorSubfile.Index,
            actorSubfile.Start,
            actorSubfile.EndExclusive,
            roots,
            actorIds);
        return true;
    }

    public static CrossLevelActorPackageRecipeSafety ValidateRecipe(
        FileStream stream,
        DiscLayout discLayout,
        int wadLba,
        long entryBase,
        CrossLevelActorPackageRecipe recipe)
    {
        if (!TryReadLayout(stream, discLayout, wadLba, entryBase, out CrossLevelActorPackageSubfileLayout? layout, out string layoutReason) || layout == null)
            return CrossLevelActorPackageRecipeSafety.Blocked(layoutReason);

        if (!TargetRangesStayInsideActorSubfile(layout, recipe.CopySegments, out string copyReason))
            return CrossLevelActorPackageRecipeSafety.Blocked(copyReason, layout);
        if (!ProposedRootTableIsSafe(layout, recipe.RootEntries, recipe.ReplaceRootEntries, out string rootReason))
            return CrossLevelActorPackageRecipeSafety.Blocked(rootReason, layout);

        return CrossLevelActorPackageRecipeSafety.Allowed(layout);
    }

    public static bool TargetRangesStayInsideActorSubfile(
        CrossLevelActorPackageSubfileLayout layout,
        IReadOnlyList<CrossLevelActorPackageCopySegment> segments,
        out string reason)
    {
        foreach (CrossLevelActorPackageCopySegment segment in segments)
        {
            if (!TryParseNumber(segment.TargetStart, out ulong targetStart) ||
                !TryParseNumber(segment.Length, out ulong length) ||
                length == 0)
            {
                reason = $"Actor-package segment {segment.TargetStart}+{segment.Length} has an invalid target range.";
                return false;
            }

            ulong targetEnd = targetStart + length;
            if (targetEnd < targetStart ||
                targetStart < layout.Start ||
                targetEnd > layout.EndExclusive)
            {
                reason = $"Actor-package segment {segment.TargetStart}+{segment.Length} is outside native actor/model " +
                    $"subfile {layout.SubfileIndex} [0x{layout.Start:X},0x{layout.EndExclusive:X}).";
                return false;
            }
        }

        reason = "";
        return true;
    }

    public static bool ProposedRootTableIsSafe(
        CrossLevelActorPackageSubfileLayout layout,
        IReadOnlyList<CrossLevelActorPackageRootEntry> rootEntries,
        IReadOnlyList<CrossLevelActorPackageRootEntry> replaceRootEntries,
        out string reason)
    {
        uint[] proposedRoots = layout.NativeRoots.ToArray();
        HashSet<int> changedIndices = [];

        foreach ((CrossLevelActorPackageRootEntry entry, bool expectEmpty) in
                 rootEntries.Select(entry => (entry, true))
                     .Concat(replaceRootEntries.Select(entry => (entry, false))))
        {
            if (!TryParseNumber(entry.TargetRootSlot, out ulong rootSlotValue) ||
                rootSlotValue < RootTableOffset ||
                rootSlotValue > RootTableOffset + ((RootSlotCount - 1) * 4) ||
                ((rootSlotValue - RootTableOffset) % 4) != 0)
            {
                reason = $"Actor root slot {entry.TargetRootSlot} is outside or misaligned for the native 64-slot table.";
                return false;
            }
            if (!TryParseNumber(entry.TargetRoot, out ulong targetRootValue) || targetRootValue > uint.MaxValue)
            {
                reason = $"Actor root {entry.TargetRoot} is not a valid 32-bit level-relative address.";
                return false;
            }

            int rootIndex = checked((int)((rootSlotValue - RootTableOffset) / 4));
            uint targetRoot = checked((uint)targetRootValue);
            if (!changedIndices.Add(rootIndex))
            {
                reason = $"Actor root index {rootIndex} is modified more than once by the same recipe.";
                return false;
            }
            if (expectEmpty && (proposedRoots[rootIndex] != 0 || layout.NativeActorIds[rootIndex] != 0))
            {
                reason = $"Actor root registration at index {rootIndex} does not target an empty native root/actor-id pair.";
                return false;
            }
            if (!expectEmpty && proposedRoots[rootIndex] == 0)
            {
                reason = $"Actor root replacement at index {rootIndex} targets the native zero terminator instead of an existing root.";
                return false;
            }
            if (targetRoot < layout.Start || targetRoot >= layout.EndExclusive)
            {
                reason = $"Proposed actor root 0x{targetRoot:X} at index {rootIndex} is outside native actor/model " +
                    $"subfile {layout.SubfileIndex} [0x{layout.Start:X},0x{layout.EndExclusive:X}).";
                return false;
            }

            proposedRoots[rootIndex] = targetRoot;
        }

        bool reachedTerminator = false;
        uint previousRoot = 0;
        for (int index = 0; index < proposedRoots.Length; index++)
        {
            uint root = proposedRoots[index];
            if (root == 0)
            {
                reachedTerminator = true;
                continue;
            }
            if (reachedTerminator)
            {
                reason = $"Proposed actor-root table is non-contiguous: index {index} is populated after the first zero terminator.";
                return false;
            }
            if (previousRoot != 0 && root <= previousRoot)
            {
                reason = $"Proposed actor-root table is not strictly ascending at index {index}: 0x{root:X} follows 0x{previousRoot:X}.";
                return false;
            }

            previousRoot = root;
        }

        reason = "";
        return true;
    }

    private static bool TryParseNumber(string value, out ulong parsed)
    {
        string text = (value ?? "").Trim();
        try
        {
            parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt64(text[2..], 16)
                : Convert.ToUInt64(text);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            parsed = 0;
            return false;
        }
    }

    private sealed record NestedSubfile(int Index, uint Start, uint EndExclusive);
}
