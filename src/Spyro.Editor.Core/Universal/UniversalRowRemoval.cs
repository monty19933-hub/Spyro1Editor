using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalRowAllocation(IReadOnlyList<int> NewRows, int FinalRowCount,
    IReadOnlyList<int> UnoccupiedRows, int ReusedRows);

/// <summary>Reuses deleted physical rows without moving any native survivor. Persistent
/// collectible identities are supplied separately by the composer/runtime save map.</summary>
public static class UniversalRowRemoval
{
    public static UniversalRowAllocation Allocate(UniversalLevelData data, IReadOnlyCollection<int>? removed, int incoming)
    {
        var scene = data.Scene();
        HashSet<int> gone = Normalize(scene.RowCount, removed);
        int[] holes = gone.Order().ToArray();
        int[] rows = Enumerable.Range(0, incoming).Select(i => i < holes.Length ? holes[i] : scene.RowCount + i - holes.Length).ToArray();
        int final = Enumerable.Range(0, scene.RowCount).Where(i => !gone.Contains(i)).Concat(rows).DefaultIfEmpty(-1).Max() + 1;
        if (final > 256 || scene.RowsOffset + final * UniversalLevelData.RowStride > scene.ArenaEnd)
            throw new InvalidDataException("The level's Moby arena has no room for the active placed rows after permanent removal.");
        return new(rows, final, gone.Except(rows).Where(i => i < final).Order().ToArray(), Math.Min(incoming, holes.Length));
    }

    public static HashSet<int> Normalize(int rowCount, IReadOnlyCollection<int>? removed)
    {
        HashSet<int> result = removed == null ? [] : removed.ToHashSet();
        if (result.Any(i => i < 0 || i >= rowCount))
            throw new InvalidDataException("A permanent removal names a row outside the staged native scene.");
        return result;
    }

    /// <summary>Deletes only property blocks positively reachable exclusively from removed rows.
    /// Unowned blocks and blocks rooted by another scene component are preserved. Every fixed
    /// pointer is relocated; a retained pointer into a removed actor is an explicit refusal.</summary>
    public static int RemoveOwnedPropertiesAndClearRows(UniversalLevelData data, IReadOnlyCollection<int>? removed)
    {
        var sc = data.Scene(); var gone = Normalize(sc.RowCount, removed);
        if (gone.Count == 0) return 0;
        var sf = data.Subfiles()[3]; byte[] scene = data.Bytes.AsSpan(sf.Offset, sf.Length).ToArray();
        if (sc.PropertiesSize < 4 || sc.Fixups.Any(f => f < 0 || f + 4 > sc.FixupsOffset || f % 4 != 0))
            throw new InvalidDataException("Permanent removal requires complete aligned native scene-fixup ownership.");
        int U(int at) => BinaryPrimitives.ReadInt32LittleEndian(scene.AsSpan(at));
        bool RowLocation(int at) => at >= sc.RowsOffset && at < sc.RowsOffset + sc.RowCount * UniversalLevelData.RowStride;
        bool RemovedRowLocation(int at) => RowLocation(at) && gone.Contains((at - sc.RowsOffset) / UniversalLevelData.RowStride);
        bool PropertyLocation(int at) => at >= sc.PropertiesComponent + 4 && at < sc.PropertiesEnd;
        SortedSet<int> bounds = [sc.PropertiesComponent + 4, sc.PropertiesEnd];
        for (int r = 0; r < sc.RowCount; r++) if (PropertyLocation(U(sc.RowsOffset + r * UniversalLevelData.RowStride))) bounds.Add(U(sc.RowsOffset + r * UniversalLevelData.RowStride));
        foreach (int f in sc.Fixups) if (PropertyLocation(U(f))) bounds.Add(U(f));
        int[] starts = bounds.ToArray();
        if (starts.Any(p => p % 4 != 0)) throw new InvalidDataException("Permanent removal cannot split unaligned native property ownership.");
        var blocks = Enumerable.Range(0, starts.Length - 1).Select(i => (Start: starts[i], End: starts[i + 1])).ToArray();
        int Block(int at) => Array.FindIndex(blocks, b => at >= b.Start && at < b.End);
        var owners = blocks.Select(_ => new HashSet<int>()).ToArray();
        Queue<(int Block, int Owner)> work = new();
        for (int r = 0; r < sc.RowCount; r++)
        {
            int block = Block(U(sc.RowsOffset + r * UniversalLevelData.RowStride));
            if (block >= 0) work.Enqueue((block, r));
        }
        foreach (int f in sc.Fixups.Where(f => !RowLocation(f) && !PropertyLocation(f)))
        {
            int block = Block(U(f)); if (block >= 0) work.Enqueue((block, -1));
        }
        while (work.Count > 0)
        {
            var (block, owner) = work.Dequeue(); if (!owners[block].Add(owner)) continue;
            foreach (int f in sc.Fixups.Where(f => f >= blocks[block].Start && f < blocks[block].End))
            {
                int target = Block(U(f)); if (target >= 0) work.Enqueue((target, owner));
            }
        }
        bool[] dead = owners.Select(o => o.Count > 0 && o.All(gone.Contains)).ToArray();
        bool DeletedLocation(int at) => RemovedRowLocation(at) || PropertyLocation(at) && dead[Block(at)];
        foreach (int f in sc.Fixups.Where(f => !DeletedLocation(f)))
            if (RemovedRowLocation(U(f)))
                throw new InvalidDataException($"Permanent removal of row {(U(f) - sc.RowsOffset) / UniversalLevelData.RowStride} would leave a surviving scene link at 0x{f:X}; remove its owned referencing object too. A new actor cannot inherit that link.");
        int freed = blocks.Where((_, i) => dead[i]).Sum(b => b.End - b.Start);
        int removedFixupBytes = 4 * sc.Fixups.Count(DeletedLocation);
        int[] destinations = new int[blocks.Length]; int next = sc.PropertiesComponent + 4;
        for (int i = 0; i < blocks.Length; i++) { destinations[i] = next; if (!dead[i]) next += blocks[i].End - blocks[i].Start; }
        int Move(int at)
        {
            if (at >= sc.UsedEnd) return at - freed - removedFixupBytes;
            if (at >= sc.PropertiesEnd) return at - freed;
            int block = Block(at);
            if (block < 0) return at;
            if (dead[block]) throw new InvalidDataException("A surviving fixed pointer escaped property-block ownership during permanent removal.");
            return destinations[block] + at - blocks[block].Start;
        }
        byte[] body = new byte[sc.FixupsOffset - freed];
        scene.AsSpan(0, sc.PropertiesComponent + 4).CopyTo(body);
        for (int i = 0; i < blocks.Length; i++) if (!dead[i]) scene.AsSpan(blocks[i].Start, blocks[i].End - blocks[i].Start).CopyTo(body.AsSpan(destinations[i]));
        scene.AsSpan(sc.PropertiesEnd, sc.FixupsOffset - sc.PropertiesEnd).CopyTo(body.AsSpan(sc.PropertiesEnd - freed));
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(sc.PropertiesComponent), sc.PropertiesSize - freed);
        List<int> fixups = [];
        foreach (int f in sc.Fixups.Where(f => !DeletedLocation(f)))
        {
            int at = Move(f), value = Move(U(f));
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(at), value); fixups.Add(at);
        }
        foreach (int r in gone)
        {
            var row = body.AsSpan(sc.RowsOffset + r * UniversalLevelData.RowStride, UniversalLevelData.RowStride);
            row.Clear();
            // An unoccupied save-map row skips both native constructors. Keep their
            // unlinked collision-chain sentinel: KillMoby searches group zero otherwise.
            BinaryPrimitives.WriteInt16LittleEndian(row[0x34..], -1);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x36..], UniversalMobyComposer.InertClass); row[0x53] = 0xFF;
        }
        byte[] packed = new byte[body.Length + 4 + fixups.Count * 4]; body.CopyTo(packed, 0);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(body.Length), fixups.Count);
        for (int i = 0; i < fixups.Count; i++) BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(body.Length + 4 + i * 4), fixups[i]);
        data.ReplaceScene(packed);
        return freed;
    }
}
