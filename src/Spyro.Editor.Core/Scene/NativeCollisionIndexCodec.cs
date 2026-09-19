using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace Spyro.Editor.Core.Scene;

internal readonly record struct NativeCollisionCell(int X, int Y, int Z);

internal sealed class NativeCollisionCellBinding
{
    internal NativeCollisionCellBinding(NativeCollisionCell cell, int fieldOffset,
        int blockWordOffset, IEnumerable<int> triangles)
    {
        Cell = cell;
        TreePointerByteOffset = fieldOffset;
        SourceBlockWordOffset = blockWordOffset;
        OrderedTriangleIndexes = Array.AsReadOnly(triangles.ToArray());
    }

    public NativeCollisionCell Cell { get; }
    public int TreePointerByteOffset { get; }
    public int SourceBlockWordOffset { get; }
    public IReadOnlyList<int> OrderedTriangleIndexes { get; }
}

/// <summary>
/// A decoded lookup index, not ownership or gameplay authority. Tree links use
/// byte offsets; leaves use block-word offsets. Triangle rows are separate data.
/// </summary>
internal sealed class NativeCollisionIndexSnapshot
{
    private readonly byte[] _tree;
    private readonly byte[] _blocks;

    internal NativeCollisionIndexSnapshot(byte[] tree, byte[] blocks,
        Dictionary<NativeCollisionCell, NativeCollisionCellBinding> cells,
        Dictionary<NativeCollisionCell, int> leaves, int triangleCount,
        int usedBytes, int groups, bool descending)
    {
        _tree = tree.ToArray();
        _blocks = blocks.ToArray();
        Cells = new ReadOnlyDictionary<NativeCollisionCell, NativeCollisionCellBinding>(
            cells.ToDictionary(p => p.Key, p => new NativeCollisionCellBinding(p.Value.Cell,
                p.Value.TreePointerByteOffset, p.Value.SourceBlockWordOffset, p.Value.OrderedTriangleIndexes)));
        LeafPointerOffsets = new ReadOnlyDictionary<NativeCollisionCell, int>(leaves.ToDictionary(p => p.Key, p => p.Value));
        TriangleCount = triangleCount;
        UsedBlockBytes = usedBytes;
        GroupStartCount = groups;
        RequiresDescendingOrder = descending;
    }

    public IReadOnlyDictionary<NativeCollisionCell, NativeCollisionCellBinding> Cells { get; }
    public IReadOnlyDictionary<NativeCollisionCell, int> LeafPointerOffsets { get; }
    public int TriangleCount { get; }
    public int UsedBlockBytes { get; }
    public int GroupStartCount { get; }
    public int TerminalSentinelWordOffset => UsedBlockBytes / 2 - 1;
    public bool RequiresDescendingOrder { get; }
    public byte[] CopyTree() => _tree.ToArray();
    public byte[] CopyBlocks() => _blocks.ToArray();
}

/// <summary>
/// Shared codec for native Z/Y/X collision lookup trees. Create allocates a new
/// lookup from explicit cell memberships. Rebuild remains fixed-capacity and
/// owns leaf words and block storage only. Neither operation infers triangle
/// geometry, relocates containing components, or grants ownership or gameplay authority.
/// </summary>
internal static class NativeCollisionIndexCodec
{
    private const int MaximumLogicalLeafCount = 1_048_576;
    private const int MaximumGeneratedTriangleReferenceCount = 1_048_576;

    public static NativeCollisionIndexSnapshot Create(
        IEnumerable<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>> desiredCells,
        int triangleCount, bool requireDescendingOrder = true)
    {
        ArgumentNullException.ThrowIfNull(desiredCells);
        if (triangleCount is < 0 or > 0x8000)
            throw new InvalidDataException("Invalid native collision triangle count.");

        // Do not trust Count, ToArray, or collection-copy shortcuts on caller
        // collections. Bound both levels of enumeration before growing buffers.
        Dictionary<NativeCollisionCell, int[]> desired = [];
        Dictionary<int, int> yLengths = [];
        Dictionary<(int Z, int Y), int> xLengths = [];
        int zLength = 0, treeWords = 1, logicalLeaves = 0, references = 0;
        foreach (var (cell, sequence) in desiredCells)
        {
            if (desired.Count >= MaximumLogicalLeafCount ||
                cell.X is < 0 or > 255 || cell.Y is < 0 or > 255 || cell.Z is < 0 or > 255 ||
                sequence == null || desired.ContainsKey(cell))
                throw new InvalidDataException("Invalid, repeated, or excessive generated collision cell.");

            int nextZLength = Math.Max(zLength, cell.Z + 1);
            bool hasY = yLengths.TryGetValue(cell.Z, out int oldYLength);
            int nextYLength = Math.Max(oldYLength, cell.Y + 1);
            bool hasX = xLengths.TryGetValue((cell.Z, cell.Y), out int oldXLength);
            int nextXLength = Math.Max(oldXLength, cell.X + 1);
            int nextTreeWords = treeWords + nextZLength - zLength +
                nextYLength - oldYLength + (hasY ? 0 : 1) +
                nextXLength - oldXLength + (hasX ? 0 : 1);
            int nextLogicalLeaves = logicalLeaves + nextXLength - oldXLength;
            if (nextTreeWords > 0x8000 || nextLogicalLeaves > MaximumLogicalLeafCount)
                throw new InvalidDataException("Generated collision tree exceeds native or logical-cell capacity.");

            List<int> copy = [];
            foreach (int triangle in sequence)
            {
                if (references >= MaximumGeneratedTriangleReferenceCount)
                    throw new InvalidDataException("Generated collision input exceeds the bounded triangle-reference limit.");
                if (triangle < 0 || triangle >= triangleCount ||
                    requireDescendingOrder && copy.Count > 0 && copy[^1] <= triangle)
                    throw new InvalidDataException("Requested collision sequence has invalid native triangle references.");
                references++;
                copy.Add(triangle);
            }
            if (copy.Count == 0)
                throw new InvalidDataException("Omit empty cells instead of emitting empty collision groups.");
            desired.Add(cell, copy.ToArray());
            zLength = nextZLength;
            yLengths[cell.Z] = nextYLength;
            xLengths[(cell.Z, cell.Y)] = nextXLength;
            treeWords = nextTreeWords;
            logicalLeaves = nextLogicalLeaves;
        }

        // Stable cell order determines group order. Equal owned sequences share
        // blocks, but each generated logical leaf has an independent tree field.
        var ordered = desired.OrderBy(p => p.Key.Z).ThenBy(p => p.Key.Y)
            .ThenBy(p => p.Key.X).ToArray();
        Dictionary<int[], int> groups = new(TriangleSequenceComparer.Instance);
        Dictionary<NativeCollisionCell, int> groupOffsets = [];
        List<ushort> words = [];
        foreach (var (cell, sequence) in ordered)
        {
            if (!groups.TryGetValue(sequence, out int offset))
            {
                offset = words.Count;
                if (offset >= 0xFFFF)
                    throw new InvalidDataException("Collision group pointer collides with the native empty-leaf sentinel.");
                for (int i = 0; i < sequence.Length; i++)
                    words.Add((ushort)(sequence[i] | (i == 0 ? 0x8000 : 0)));
                groups.Add(sequence, offset);
            }
            groupOffsets.Add(cell, offset);
        }
        words.Add(0x8000);
        int usedBlockBytes = words.Count * 2;
        byte[] blocks = new byte[(usedBlockBytes + 3) & ~3];
        for (int i = 0; i < words.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(blocks.AsSpan(i * 2), words[i]);

        byte[] tree = new byte[(treeWords * 2 + 3) & ~3];
        void Write(int offset, int value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(offset), (ushort)value);
        void InitializeSegment(int offset, int length)
        {
            Write(offset, length);
            for (int i = 0; i < length; i++) Write(offset + (i + 1) * 2, 0xFFFF);
        }
        void Link(int field, int offset)
        {
            if (offset < 0 || offset >= 0xFFFF || (offset & 1) != 0)
                throw new InvalidDataException("Generated collision segment has no native byte pointer.");
            Write(field, offset);
        }

        InitializeSegment(0, zLength);
        int nextOffset = (zLength + 1) * 2;
        Dictionary<int, int> yOffsets = [];
        foreach (var (z, length) in yLengths.OrderBy(p => p.Key))
        {
            Link((z + 1) * 2, nextOffset);
            yOffsets.Add(z, nextOffset);
            InitializeSegment(nextOffset, length);
            nextOffset += (length + 1) * 2;
        }
        Dictionary<(int Z, int Y), int> xOffsets = [];
        foreach (var (row, length) in xLengths.OrderBy(p => p.Key.Z).ThenBy(p => p.Key.Y))
        {
            Link(yOffsets[row.Z] + (row.Y + 1) * 2, nextOffset);
            xOffsets.Add(row, nextOffset);
            InitializeSegment(nextOffset, length);
            nextOffset += (length + 1) * 2;
        }
        foreach (var (cell, offset) in groupOffsets)
            Write(xOffsets[(cell.Z, cell.Y)] + (cell.X + 1) * 2, offset);
        if (nextOffset != treeWords * 2)
            throw new InvalidDataException("Generated collision segment extents do not match their measured capacity.");

        NativeCollisionIndexSnapshot result = Decode(tree, blocks, usedBlockBytes,
            triangleCount, requireDescendingOrder);
        if (result.LeafPointerOffsets.Count != logicalLeaves || result.Cells.Count != desired.Count ||
            desired.Any(p => !result.Cells.TryGetValue(p.Key, out var actual) ||
                !actual.OrderedTriangleIndexes.SequenceEqual(p.Value)))
            throw new InvalidDataException("Generated collision lookup failed complete semantic readback.");
        return result;
    }

    public static NativeCollisionIndexSnapshot Decode(ReadOnlySpan<byte> sourceTree,
        ReadOnlySpan<byte> sourceBlocks, int usedBlockBytes, int triangleCount,
        bool requireDescendingOrder = true)
    {
        if (sourceTree.Length < 2 || sourceTree.Length > 0x10000 ||
            (sourceTree.Length & 1) != 0 || (sourceBlocks.Length & 1) != 0 ||
            usedBlockBytes < 2 || (usedBlockBytes & 1) != 0 ||
            usedBlockBytes > sourceBlocks.Length || triangleCount is < 0 or > 0x8000)
            throw new InvalidDataException("Invalid native collision tree, block, or triangle bounds.");
        if (sourceBlocks[usedBlockBytes..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Collision block capacity after its used extent must be zero.");
        byte[] tree = sourceTree.ToArray();
        byte[] blocks = sourceBlocks.ToArray();
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int i = 0; i < usedWords; i++)
            if ((Word(blocks, i * 2) & 0x8000) != 0)
                starts.Add(i);
        if (starts.Count == 0 || starts[0] != 0 || starts[^1] != usedWords - 1 ||
            Word(blocks, usedBlockBytes - 2) != 0x8000)
            throw new InvalidDataException("Collision groups require a leading marker and exact final sentinel.");

        Dictionary<int, int[]> groups = [];
        for (int i = 0; i + 1 < starts.Count; i++)
        {
            int start = starts[i];
            int[] triangles = new int[starts[i + 1] - start];
            for (int j = 0; j < triangles.Length; j++)
            {
                triangles[j] = Word(blocks, (start + j) * 2) & 0x7FFF;
                if (triangles[j] >= triangleCount ||
                    requireDescendingOrder && j > 0 && triangles[j - 1] <= triangles[j])
                    throw new InvalidDataException("Collision triangle references are out of range or not strictly descending.");
            }
            groups.Add(start, triangles);
        }

        // Exact aliases at the same tree depth are native sharing. Partial
        // overlap or interpreting a segment at a different depth is ambiguous.
        Dictionary<int, (int End, int Depth)> segments = [];
        int Segment(int offset, int depth)
        {
            int length = Word(tree, offset);
            long end = offset + (length + 1L) * 2;
            if (length > 256 || end > tree.Length)
                throw new InvalidDataException("Collision tree segment exceeds its capacity.");
            if (segments.TryGetValue(offset, out var existing))
            {
                if (existing.End != end || existing.Depth != depth)
                    throw new InvalidDataException("Collision tree segment has incompatible ownership aliases.");
            }
            else
            {
                foreach (var (otherStart, other) in segments)
                    if (offset < other.End && otherStart < end)
                        throw new InvalidDataException("Collision tree segments partially overlap.");
                segments.Add(offset, ((int)end, depth));
            }
            return length;
        }

        Dictionary<NativeCollisionCell, NativeCollisionCellBinding> cells = [];
        Dictionary<NativeCollisionCell, int> leaves = [];
        int zLength = Segment(0, 0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = Word(tree, (z + 1) * 2);
            if (yOffset == 0xFFFF) continue;
            int yLength = Segment(yOffset, 1);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = Word(tree, yOffset + (y + 1) * 2);
                if (xOffset == 0xFFFF) continue;
                int xLength = Segment(xOffset, 2);
                for (int x = 0; x < xLength; x++)
                {
                    if (leaves.Count >= MaximumLogicalLeafCount)
                        throw new InvalidDataException("Collision tree exceeds the bounded logical-cell limit.");
                    int field = xOffset + (x + 1) * 2;
                    int group = Word(tree, field);
                    NativeCollisionCell cell = new(x, y, z);
                    leaves.Add(cell, field);
                    if (group == 0xFFFF) continue;
                    if (!groups.TryGetValue(group, out int[]? triangles))
                        throw new InvalidDataException("Collision leaf does not reference the start of a real block group.");
                    cells.Add(cell, new(cell, field, group, triangles));
                }
            }
        }
        if (!cells.Values.Select(c => c.SourceBlockWordOffset).Distinct().Order()
                .SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("Collision block storage contains unreferenced groups.");
        return new(tree, blocks, cells, leaves, triangleCount, usedBlockBytes,
            starts.Count, requireDescendingOrder);
    }

    public static NativeCollisionIndexSnapshot Rebuild(NativeCollisionIndexSnapshot source,
        IReadOnlyDictionary<NativeCollisionCell, IReadOnlyList<int>> desiredCells)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(desiredCells);
        Dictionary<NativeCollisionCell, int[]> desired = [];
        foreach (var (cell, sequence) in desiredCells)
        {
            if (sequence == null || !source.LeafPointerOffsets.ContainsKey(cell))
                throw new InvalidDataException("A requested collision cell has no native tree leaf.");
            int[] copy = sequence.ToArray();
            if (copy.Length == 0)
                throw new InvalidDataException("Omit empty cells instead of emitting empty collision groups.");
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] < 0 || copy[i] >= source.TriangleCount ||
                    source.RequiresDescendingOrder && i > 0 && copy[i - 1] <= copy[i])
                    throw new InvalidDataException("Requested collision sequence has invalid native triangle references.");
            desired.Add(cell, copy);
        }

        Dictionary<string, int> deduplicated = new(StringComparer.Ordinal);
        Dictionary<NativeCollisionCell, int> offsets = [];
        List<ushort> words = [];
        foreach (var (cell, sequence) in desired
                     .OrderBy(p => source.Cells.TryGetValue(p.Key, out var old)
                         ? old.SourceBlockWordOffset : int.MaxValue)
                     .ThenBy(p => p.Key.Z).ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X))
        {
            string identity = string.Join(',', sequence);
            if (!deduplicated.TryGetValue(identity, out int offset))
            {
                offset = words.Count;
                if (offset >= 0xFFFF)
                    throw new InvalidDataException("Collision group pointer collides with the native empty-leaf sentinel.");
                for (int i = 0; i < sequence.Length; i++)
                    words.Add((ushort)(sequence[i] | (i == 0 ? 0x8000 : 0)));
                deduplicated.Add(identity, offset);
            }
            offsets.Add(cell, offset);
        }
        words.Add(0x8000);
        byte[] blocks = source.CopyBlocks();
        int used = checked(words.Count * 2);
        if (used > blocks.Length)
            throw new InvalidDataException("Collision groups exceed fixed native block capacity.");
        Array.Clear(blocks);
        for (int i = 0; i < words.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(blocks.AsSpan(i * 2), words[i]);

        byte[] tree = source.CopyTree();
        Dictionary<int, ushort> fields = [];
        foreach (var (cell, field) in source.LeafPointerOffsets)
        {
            ushort value = offsets.TryGetValue(cell, out int offset) ? (ushort)offset : (ushort)0xFFFF;
            if (fields.TryGetValue(field, out ushort prior) && prior != value)
                throw new InvalidDataException("Shared collision tree leaves require conflicting cell contents; split the tree before editing them.");
            fields[field] = value;
        }
        foreach (var (field, value) in fields)
            BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(field), value);
        NativeCollisionIndexSnapshot result = Decode(tree, blocks, used,
            source.TriangleCount, source.RequiresDescendingOrder);
        if (result.Cells.Count != desired.Count || desired.Any(p =>
                !result.Cells.TryGetValue(p.Key, out var actual) ||
                !actual.OrderedTriangleIndexes.SequenceEqual(p.Value)))
            throw new InvalidDataException("Rebuilt collision lookup failed complete semantic readback.");
        return result;
    }

    public static NativeCollisionIndexSnapshot RetainTriangles(
        NativeCollisionIndexSnapshot source, IReadOnlySet<int> retainedTriangles)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(retainedTriangles);
        HashSet<int> retained = retainedTriangles.ToHashSet();
        HashSet<int> referenced = source.Cells.Values
            .SelectMany(c => c.OrderedTriangleIndexes).ToHashSet();
        if (retained.Any(t => t < 0 || t >= source.TriangleCount || !referenced.Contains(t)))
            throw new InvalidDataException("A retained collision triangle is not reachable in the native source index.");
        Dictionary<NativeCollisionCell, IReadOnlyList<int>> desired = [];
        foreach (var (cell, binding) in source.Cells)
        {
            int[] sequence = binding.OrderedTriangleIndexes.Where(retained.Contains).ToArray();
            if (sequence.Length > 0) desired.Add(cell, sequence);
        }
        return Rebuild(source, desired);
    }

    private static ushort Word(byte[] bytes, int offset)
    {
        if (offset < 0 || (offset & 1) != 0 || offset > bytes.Length - 2)
            throw new InvalidDataException("Collision index word is outside its exact buffer.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    }

    private sealed class TriangleSequenceComparer : IEqualityComparer<int[]>
    {
        internal static readonly TriangleSequenceComparer Instance = new();
        public bool Equals(int[]? x, int[]? y) => ReferenceEquals(x, y) ||
            x != null && y != null && x.AsSpan().SequenceEqual(y);
        public int GetHashCode(int[] sequence)
        {
            HashCode hash = new();
            foreach (int triangle in sequence) hash.Add(triangle);
            return hash.ToHashCode();
        }
    }
}
