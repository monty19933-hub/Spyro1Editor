using System.Buffers.Binary;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Route-profile resource limits, not changes to the shared native codec. The
/// preflight counts logical expansion before Decode allocates per-cell copies.
/// Unsupported complexity refuses an audit; it never produces partial success.
/// </summary>
internal static class NativeRouteSurfaceBudget
{
    internal const int MaximumDescriptorBytes = 4096;
    internal const int MaximumAggregateDescriptorBytes = 65536;
    internal const int MaximumLogicalCells = 65536;
    internal const int MaximumExpandedReferences = 2_000_000;
    internal const int MaximumUniqueSegments = 2048;
    internal const int MaximumSegmentVisits = 65536;
    public static void ValidateIndexExpansion(ReadOnlySpan<byte> tree, ReadOnlySpan<byte> blocks, int usedBytes)
    {
        if (tree.Length is < 2 or > 65536 || (tree.Length & 1) != 0 || usedBytes is < 2 or > 131072 || (usedBytes & 1) != 0 || usedBytes > blocks.Length)
            throw new InvalidDataException("Native route index storage budget or bounds exceeded.");
        // Only group lengths are retained. No expanded triangle arrays or
        // logical cell objects are constructed by this preflight.
        Dictionary<int, int> groupLengths = [];
        int previousStart = -1;
        for (int word = 0; word < usedBytes / 2; word++)
        {
            if ((Word(blocks, word * 2) & 0x8000) == 0)
                continue;
            if (previousStart >= 0)
                groupLengths.Add(previousStart, word - previousStart);
            else if (word != 0)
                throw new InvalidDataException("Native route index groups lack a leading marker.");
            previousStart = word;
        }

        if (previousStart != usedBytes / 2 - 1 || Word(blocks, usedBytes - 2) != 0x8000)
            throw new InvalidDataException("Native route index groups lack the final sentinel.");
        // Depth/partial-overlap validation remains the shared codec's job.
        // Unique offsets are bounded here because its ownership check is
        // pairwise even when every referenced X segment has zero leaves.
        HashSet<int> segments = [];
        int visits = 0;
        int cells = 0;
        long references = 0;
        int zLength = Segment(tree, 0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = Word(tree, (z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = Segment(tree, yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = Word(tree, yOffset + (y + 1) * 2);
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = Segment(tree, xOffset);
                if (cells > MaximumLogicalCells - xLength)
                    throw new InvalidDataException("Native route logical-cell budget exceeded before decode.");
                cells += xLength;
                for (int x = 0; x < xLength; x++)
                {
                    int group = Word(tree, xOffset + (x + 1) * 2);
                    if (group == 0xFFFF)
                        continue;
                    if (!groupLengths.TryGetValue(group, out int length))
                        throw new InvalidDataException("Native route index leaf does not reference a real group.");
                    references += length;
                    if (references > MaximumExpandedReferences)
                        throw new InvalidDataException("Native route expanded-reference budget exceeded before decode.");
                }
            }
        }

        int Segment(ReadOnlySpan<byte> bytes, int offset)
        {
            if (++visits > MaximumSegmentVisits)
                throw new InvalidDataException("Native route segment-visit budget exceeded before decode.");
            if (segments.Add(offset) && segments.Count > MaximumUniqueSegments)
                throw new InvalidDataException("Native route unique segment budget exceeded before decode.");
            int length = Word(bytes, offset);
            if (length > 256 || offset + (length + 1L) * 2 > bytes.Length)
                throw new InvalidDataException("Native route index segment exceeds source bounds.");
            return length;
        }
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || (offset & 1) != 0 || offset > bytes.Length - 2)
            throw new InvalidDataException("Native route index word exceeds source bounds.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }
}

/// <summary>One budget shared across every segment in a complete route audit.</summary>
internal sealed class NativeRouteSurfaceWorkBudget
{
    private int _clips;
    private int _pairs;
    private int _issues;
    private int _bindings;
    private int _membershipEntries;
    public void Clip(int vertexCount)
    {
        if (vertexCount > 64 || ++_clips > 100000)
            Exceeded("exact clipping");
    }

    public void Pair()
    {
        if (++_pairs > 20000)
            Exceeded("support-pair");
    }

    public void Issue()
    {
        if (++_issues > 4096)
            Exceeded("issue output");
    }

    public void Binding()
    {
        if (++_bindings > 16384)
            Exceeded("binding output");
    }

    public void Membership(int entryCount)
    {
        if (entryCount < 0 || entryCount > 2_000_000 - _membershipEntries)
            Exceeded("membership lookup");
        _membershipEntries += entryCount;
    }

    private static void Exceeded(string operation) => throw new InvalidDataException($"Native route total {operation} budget exceeded; no certificate issued.");
}
