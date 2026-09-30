using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalRegistration(int Slot, int ClassId, int Root);

public sealed record UniversalSceneLayout(int MobyComponent, int MobyComponentSize, int RowCount, int RowsOffset,
    int PropertiesComponent, int PropertiesSize, int PodComponent, int CollisionComponent, int FixupsOffset,
    IReadOnlyList<int> Fixups, int UsedEnd)
{
    public int PropertiesEnd => PropertiesComponent + PropertiesSize;
    public int ArenaEnd => MobyComponent + MobyComponentSize;
}

/// <summary>A copied source row plus its private properties blocks.</summary>
public sealed record UniversalRowTemplate(string SourceLevelKey, int SourceRowIndex, byte[] Row, byte[] Properties,
    IReadOnlyList<(int Offset, int Target)> InternalPointers, int DroppedExternalPointers)
{
    public int ClassId => BinaryPrimitives.ReadUInt16LittleEndian(Row.AsSpan(0x36));
}

/// <summary>Mutable view of one retail level-data WAD entry (0x800-byte header + subfiles).</summary>
public sealed class UniversalLevelData
{
    public const int RowStride = 0x58;
    private byte[] _data;

    public UniversalLevelData(byte[] data) { _data = data; }
    public byte[] Bytes => _data;

    public int U32(int o) => (int)BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(o));
    public void Put32(int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(o), v);

    public IReadOnlyList<(int Offset, int Length)> Subfiles()
    {
        List<(int, int)> list = [];
        for (int i = 0; i < 10; i++)
        {
            int o = U32(8 * i), l = U32(8 * i + 4);
            if (o == 0 && l == 0) break;
            list.Add((o, l));
        }
        return list;
    }

    public IReadOnlyList<UniversalRegistration> Registrations()
    {
        List<UniversalRegistration> regs = [];
        for (int slot = 1; slot < 64; slot++)
        {
            int root = U32(0x50 + 4 * slot);
            if (root <= 0) break;
            regs.Add(new(slot, BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(0x150 + 2 * slot)), root));
        }
        return regs;
    }

    public byte[] Package(int classId)
    {
        var regs = Registrations();
        var reg = regs.FirstOrDefault(r => r.ClassId == classId)
            ?? throw new InvalidDataException($"Class 0x{classId:X3} has no local model registration.");
        var (mo, ml) = Subfiles()[2];
        int next = regs.Where(r => r.Root > reg.Root).Select(r => r.Root).DefaultIfEmpty(mo + ml).Min();
        return _data.AsSpan(reg.Root, next - reg.Root).ToArray();
    }

    /// <summary>Inserts <paramref name="bytes"/> zeros at the end of subfile <paramref name="index"/>; later subfiles shift.</summary>
    public int GrowSubfile(int index, int bytes)
    {
        if (bytes % 0x800 != 0) throw new ArgumentException("Subfile growth must be sector aligned.");
        var sf = Subfiles();
        var (o, l) = sf[index];
        int insert = o + l;
        byte[] grown = new byte[_data.Length + bytes];
        _data.AsSpan(0, insert).CopyTo(grown);
        _data.AsSpan(insert).CopyTo(grown.AsSpan(insert + bytes));
        _data = grown;
        Put32(8 * index + 4, l + bytes);
        for (int j = index + 1; j < sf.Count; j++) Put32(8 * j, sf[j].Offset + bytes);
        // Model roots are level-entry offsets: shift every root located after the insertion point.
        for (int slot = 0; slot < 64; slot++)
        {
            int root = U32(0x50 + 4 * slot);
            if (root > 0 && root >= insert) Put32(0x50 + 4 * slot, root + bytes);
        }
        return insert;
    }

    /// <summary>Inserts a component record within a subfile, preserving every following byte. Only
    /// verified zero tail padding is consumed; otherwise the subfile grows by a sector.</summary>
    public void InsertWithinSubfile(int index, int relativeOffset, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        var sf = Subfiles()[index];
        if (relativeOffset < 0 || relativeOffset > sf.Length) throw new ArgumentOutOfRangeException(nameof(relativeOffset));
        int padding = Math.Min(bytes.Length, sf.Length - relativeOffset);
        bool usePadding = padding == bytes.Length && _data.AsSpan(sf.Offset + sf.Length - padding, padding).IndexOfAnyExcept((byte)0) < 0;
        int move = sf.Length - relativeOffset - (usePadding ? bytes.Length : 0);
        if (!usePadding) GrowSubfile(index, Align(bytes.Length, 0x800));
        _data.AsSpan(sf.Offset + relativeOffset, move).CopyTo(_data.AsSpan(sf.Offset + relativeOffset + bytes.Length));
        bytes.CopyTo(_data.AsSpan(sf.Offset + relativeOffset));
    }

    public int AppendModel(int classId, ReadOnlySpan<byte> package)
    {
        var regs = Registrations();
        int slot = regs.Count + 1;
        if (slot >= 64) throw new InvalidDataException("The level has no free model registration slot (63 used).");
        if (regs.Any(r => r.ClassId == classId)) throw new InvalidDataException($"Class 0x{classId:X3} is already registered.");
        var (mo, ml) = Subfiles()[2];
        int root = mo + ml;
        GrowSubfile(2, Align(package.Length, 0x800));
        package.CopyTo(_data.AsSpan(root));
        Put32(0x50 + 4 * slot, root);
        BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0x150 + 2 * slot), (ushort)classId);
        return slot;
    }

    /// <summary>Appends several packages back to back (8-byte aligned) with one sector-rounded growth.</summary>
    public IReadOnlyList<int> AppendModels(IReadOnlyList<(int ClassId, byte[] Package)> packages)
    {
        var regs = Registrations();
        if (packages.Select(p => p.ClassId).Distinct().Count() != packages.Count)
            throw new InvalidDataException("An import batch contains duplicate model class identities.");
        if (packages.Any(p => p.ClassId <= 0 || p.ClassId >= 0x200))
            throw new InvalidDataException("An imported model class is outside the supported class range.");
        if (regs.Count + packages.Count > 63) throw new InvalidDataException("The level has no free model registration slots left (63 max).");
        foreach (var (c, _) in packages)
            if (regs.Any(r => r.ClassId == c)) throw new InvalidDataException($"Class 0x{c:X3} is already registered.");
        var (mo, ml) = Subfiles()[2];
        int start = mo + ml, cursor = 0;
        List<int> offsets = [];
        foreach (var (_, p) in packages) { offsets.Add(cursor); cursor = Align(cursor + p.Length, 8); }
        GrowSubfile(2, Align(cursor, 0x800));
        List<int> slots = [];
        for (int i = 0; i < packages.Count; i++)
        {
            packages[i].Package.CopyTo(_data, start + offsets[i]);
            int slot = regs.Count + 1 + i;
            Put32(0x50 + 4 * slot, start + offsets[i]);
            BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0x150 + 2 * slot), (ushort)packages[i].ClassId);
            slots.Add(slot);
        }
        return slots;
    }

    /// <summary>Removes the model packages of <paramref name="classIds"/>: later packages move down (keeping
    /// their alignment mod 8), the model subfile shrinks by whole sectors, later subfiles and roots shift,
    /// and the registration table is compacted. Returns the bytes the level data shrank by.</summary>
    public int RemoveModels(IReadOnlyCollection<int> classIds)
    {
        if (classIds.Count == 0) return 0;
        var regs = Registrations();
        var sf = Subfiles();
        var (mo, ml) = sf[2];
        var byRoot = regs.OrderBy(r => r.Root).ToList();
        int firstRoot = byRoot.Count > 0 ? byRoot[0].Root : mo + ml;
        using MemoryStream body = new();
        body.Write(_data, mo, firstRoot - mo);                       // anything before the first package stays put
        Dictionary<int, int> newRoot = [];
        for (int i = 0; i < byRoot.Count; i++)
        {
            var r = byRoot[i];
            int end = i + 1 < byRoot.Count ? byRoot[i + 1].Root : mo + ml;
            if (classIds.Contains(r.ClassId)) continue;
            int pos = mo + (int)body.Length;
            int pad = ((r.Root - pos) % 8 + 8) % 8;                  // keep the package's alignment mod 8
            body.Write(new byte[pad]);
            newRoot[r.Slot] = mo + (int)body.Length;
            body.Write(_data, r.Root, end - r.Root);
        }
        int newLength = Align((int)body.Length, 0x800);
        int shrink = ml - newLength;
        if (shrink < 0) throw new InvalidDataException("Model removal unexpectedly grew the model subfile.");
        byte[] next = new byte[_data.Length - shrink];
        _data.AsSpan(0, mo).CopyTo(next);
        body.ToArray().CopyTo(next, mo);
        _data.AsSpan(mo + ml).CopyTo(next.AsSpan(mo + newLength));
        _data = next;
        Put32(8 * 2 + 4, newLength);
        for (int j = 3; j < sf.Count; j++) Put32(8 * j, sf[j].Offset - shrink);
        var kept = regs.Where(r => !classIds.Contains(r.ClassId)).ToList();
        for (int slot = 1; slot < 64; slot++) { Put32(0x50 + 4 * slot, 0); BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0x150 + 2 * slot), 0); }
        for (int i = 0; i < kept.Count; i++)
        {
            Put32(0x50 + 4 * (i + 1), newRoot[kept[i].Slot]);
            BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0x150 + 2 * (i + 1)), (ushort)kept[i].ClassId);
        }
        return shrink;
    }

    public UniversalSceneLayout Scene()
    {
        var (so, _) = Subfiles()[3];
        return ParseScene(_data.AsSpan(so));
    }

    public static UniversalSceneLayout ParseScene(ReadOnlySpan<byte> span)
    {
        byte[] s = span.ToArray();
        int U(int o) => (int)BinaryPrimitives.ReadUInt32LittleEndian(s.AsSpan(o));
        int p = 0x88;
        for (int i = 0; i < 7; i++) p += U(p);
        int mob = p, mobSize = U(p), count = U(p + 4), rows = p + 8;
        p += mobSize;
        int prop = p, propSize = U(p); p += propSize;
        int pod = p; p += U(p);
        int col = p; p += U(p);
        int fix = p, n = U(p);
        int[] fixups = new int[n];
        for (int i = 0; i < n; i++) fixups[i] = U(p + 4 + 4 * i);
        return new(mob, mobSize, count, rows, prop, propSize, pod, col, fix, fixups, p + 4 + 4 * n);
    }

    public void SetRowClass(int index, int classId)
    {
        var sc = Scene(); var (so, _) = Subfiles()[3];
        if (index < 0 || index >= sc.RowCount) throw new ArgumentOutOfRangeException(nameof(index));
        BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(so + sc.RowsOffset + index * RowStride + 0x36), (ushort)classId);
    }

    public byte[] Row(int index)
    {
        var sc = Scene(); var (so, _) = Subfiles()[3];
        if (index < 0 || index >= sc.RowCount) throw new ArgumentOutOfRangeException(nameof(index));
        return _data.AsSpan(so + sc.RowsOffset + index * RowStride, RowStride).ToArray();
    }

    /// <summary>Copies a source row with its properties block and every properties block it points to.</summary>
    public UniversalRowTemplate CaptureRow(string levelKey, int index)
    {
        var sc = Scene(); var (so, _) = Subfiles()[3];
        ReadOnlySpan<byte> s = _data.AsSpan(so);
        byte[] row = Row(index);
        int props = BinaryPrimitives.ReadInt32LittleEndian(row);
        HashSet<int> fixups = [.. sc.Fixups];
        SortedSet<int> bounds = [sc.PropertiesEnd];
        for (int i = 0; i < sc.RowCount; i++) bounds.Add(BinaryPrimitives.ReadInt32LittleEndian(s[(sc.RowsOffset + i * RowStride)..]));
        foreach (int f in sc.Fixups)
        {
            int t = BinaryPrimitives.ReadInt32LittleEndian(s[f..]);
            if (t >= sc.PropertiesComponent + 4 && t < sc.PropertiesEnd) bounds.Add(t);
        }
        int Next(int start) => bounds.GetViewBetween(start + 1, int.MaxValue).Min;
        if (props < sc.PropertiesComponent + 4 || props >= sc.PropertiesEnd)
            return new(levelKey, index, row, new byte[16], [], 0); // native rows here point at an empty tail; keep a valid private block
        // Transitive closure of properties blocks reachable through fixed-up pointers.
        List<(int Start, int End)> blocks = [];
        Queue<int> work = new([props]);
        HashSet<int> seenStarts = [];
        while (work.Count > 0)
        {
            int start = work.Dequeue();
            if (!seenStarts.Add(start)) continue;
            int end = Next(start);
            blocks.Add((start, end));
            foreach (int f in sc.Fixups.Where(f => f >= start && f < end))
            {
                int t = BinaryPrimitives.ReadInt32LittleEndian(s[f..]);
                if (t >= sc.PropertiesComponent + 4 && t < sc.PropertiesEnd && !blocks.Any(b => t >= b.Start && t < b.End))
                    work.Enqueue(bounds.Contains(t) ? t : bounds.GetViewBetween(int.MinValue, t).Max);
            }
        }
        blocks.Sort();
        // Pack blocks; the first block must be the row's own block.
        blocks.Remove(blocks.First(b => b.Start == props));
        blocks.Insert(0, (props, Next(props)));
        Dictionary<int, int> newOffset = [];
        int size = 0;
        foreach (var b in blocks) { newOffset[b.Start] = size; size += Align(b.End - b.Start, 4); }
        byte[] packed = new byte[size];
        List<(int, int)> internalPtrs = [];
        int dropped = 0;
        foreach (var b in blocks)
        {
            s[b.Start..b.End].CopyTo(packed.AsSpan(newOffset[b.Start]));
            foreach (int f in sc.Fixups.Where(f => f >= b.Start && f < b.End))
            {
                int t = BinaryPrimitives.ReadInt32LittleEndian(s[f..]);
                var owner = blocks.FirstOrDefault(x => t >= x.Start && t < x.End);
                int at = newOffset[b.Start] + (f - b.Start);
                if (owner != default) internalPtrs.Add((at, newOffset[owner.Start] + (t - owner.Start)));
                else { BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(at), 0); dropped++; }
            }
        }
        return new(levelKey, index, row, packed, internalPtrs, dropped);
    }

    /// <summary>Appends rows into the Moby arena and their properties at the end of the properties component.</summary>
    public (int PropertiesBytes, int SceneGrowth, IReadOnlyList<int> NewRowIndices) AppendRows(IReadOnlyList<(byte[] Row, UniversalRowTemplate Template)> rows)
    {
        var (so, sl) = Subfiles()[3];
        var sc = Scene();
        byte[] s = _data.AsSpan(so, sl).ToArray();
        int insert = sc.PropertiesEnd;
        int total = rows.Sum(r => Align(r.Template.Properties.Length, 4));
        if (sc.RowsOffset + (sc.RowCount + rows.Count) * RowStride > sc.ArenaEnd)
            throw new InvalidDataException("The level's Moby arena has no room for more placed rows.");
        List<int> newFix = [], newIdx = [];
        using MemoryStream blob = new();
        List<int> propStarts = [];
        foreach (var (_, t) in rows)
        {
            int start = insert + (int)blob.Length; propStarts.Add(t.Properties.Length == 0 ? 0 : start);
            byte[] p = new byte[Align(t.Properties.Length, 4)];
            t.Properties.CopyTo(p, 0);
            foreach (var (off, target) in t.InternalPointers)
            {
                BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(off), start + target);
                newFix.Add(start + off);
            }
            blob.Write(p);
        }
        List<int> fixups = sc.Fixups.Select(f => f >= insert ? f + total : f).ToList();
        byte[] body = [.. s.AsSpan(0, insert), .. blob.ToArray(), .. s.AsSpan(insert, sc.FixupsOffset - insert)];
        foreach (int f in fixups)
        {
            int v = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(f));
            if (v >= insert) BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(f), v + total);
        }
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(sc.PropertiesComponent), sc.PropertiesSize + total);
        for (int i = 0; i < rows.Count; i++)
        {
            int ro = sc.RowsOffset + (sc.RowCount + i) * RowStride;
            if (body.AsSpan(ro, RowStride).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("The Moby arena slot for an appended row is not empty.");
            byte[] r = rows[i].Row.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(r, propStarts[i]);
            r.CopyTo(body, ro);
            if (propStarts[i] != 0) fixups.Add(ro);
            newIdx.Add(sc.RowCount + i);
        }
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(sc.MobyComponent + 4), sc.RowCount + rows.Count);
        fixups.AddRange(newFix);
        using MemoryStream outp = new();
        outp.Write(body);
        Span<byte> w = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(w, fixups.Count); outp.Write(w);
        foreach (int f in fixups) { BinaryPrimitives.WriteInt32LittleEndian(w, f); outp.Write(w); }
        byte[] scene = outp.ToArray();
        int grow = Align(Math.Max(0, scene.Length - sl), 0x800);
        if (grow > 0) GrowSubfile(3, grow);
        Array.Clear(_data, so, sl + grow);
        scene.CopyTo(_data, so);
        return (total, grow, newIdx);
    }

    public Span<byte> Vram() { var (o, _) = Subfiles()[0]; return _data.AsSpan(o, 0x80000); }

    public static int Align(int v, int a) => (v + a - 1) / a * a;
}
