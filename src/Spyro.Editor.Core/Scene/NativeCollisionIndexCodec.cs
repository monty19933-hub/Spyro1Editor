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
/// Shared fixed-capacity codec for native Z/Y/X collision lookup trees. Rebuild
/// owns leaf words and block storage only; it cannot relocate components, infer
/// triangle geometry, allocate missing tree segments, or authorize retirement.
/// </summary>
internal static class NativeCollisionIndexCodec
{
    private const int MaximumLogicalLeafCount = 1_048_576;

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
}
