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
    public IReadOnlyList<(int Offset, int SourceRow, int RowOffset)> ExternalRowPointers { get; init; } = [];
    public IReadOnlyList<(int Offset, int PlacementIndex, int RowOffset)> ImportedRowPointers { get; init; } = [];
    public string? LinkGroupId { get; init; }
    public UniversalBoarPlacementCertificate? BoarPlacementCertificate {get;init;}
    public IReadOnlyList<UniversalSourceRowIndex> SourceRowIndices { get; init; } = [];
    public IReadOnlyList<UniversalSourcePrerequisite> SourcePrerequisiteRows { get; init; } = [];
    public IReadOnlyList<UniversalImportedRowIndex> ImportedRowIndices { get; init; } = [];
    internal UniversalDarkPassageLanternContext? DarkLanternContext {get;init;}
    internal UniversalGreenDruidNativeReferences? GreenDruidNativeReferences {get;init;}
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
    /// zero tail padding beyond the caller-certified owned byte range is consumed; otherwise the subfile grows by a sector.</summary>
    public void InsertWithinSubfile(int index, int relativeOffset, ReadOnlySpan<byte> bytes, int usedByteLength)
    {
        if (bytes.Length == 0) return;
        var sf = Subfiles()[index];
        if (usedByteLength < 0 || usedByteLength > sf.Length) throw new ArgumentOutOfRangeException(nameof(usedByteLength));
        if (relativeOffset < 0 || relativeOffset > usedByteLength) throw new ArgumentOutOfRangeException(nameof(relativeOffset));
        int padding = Math.Min(bytes.Length, sf.Length - usedByteLength);
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

    /// <summary>Moves the packages of <paramref name="classIds"/>, all located after <paramref name="anchorClassId"/>'s
    /// package, to just before it, in their current order. Every package keeps its bytes and its alignment mod 8; the
    /// model subfile grows by whole sectors only if realignment needs room, and registrations stay in root order.
    /// Returns the bytes the level data grew by.</summary>
    public int MoveModelsBefore(int anchorClassId, IReadOnlyCollection<int> classIds)
    {
        if (classIds.Count == 0) return 0;
        var regs = Registrations();
        var (mo, ml) = Subfiles()[2];
        var anchor = regs.Single(r => r.ClassId == anchorClassId);
        var tail = regs.Where(r => r.Root >= anchor.Root).OrderBy(r => r.Root).ToList();
        if (classIds.Contains(anchorClassId) || classIds.Any(c => !tail.Any(r => r.ClassId == c)))
            throw new InvalidDataException("Only packages after the anchor model can move before it.");
        using MemoryStream body = new();
        Dictionary<int, int> newRoot = [];
        foreach (var r in tail.Where(r => classIds.Contains(r.ClassId)).Concat(tail.Where(r => !classIds.Contains(r.ClassId))))
        {
            int i = tail.IndexOf(r), end = i + 1 < tail.Count ? tail[i + 1].Root : mo + ml;
            int pos = anchor.Root + (int)body.Length;
            body.Write(new byte[((r.Root - pos) % 8 + 8) % 8]);
            newRoot[r.Slot] = anchor.Root + (int)body.Length;
            body.Write(_data, r.Root, end - r.Root);
        }
        int growth = Align(Math.Max(0, anchor.Root + (int)body.Length - (mo + ml)), 0x800);
        if (growth > 0) GrowSubfile(2, growth);
        _data.AsSpan(anchor.Root, mo + ml + growth - anchor.Root).Clear();
        body.ToArray().CopyTo(_data, anchor.Root);
        var ordered = regs.Select(r => (r.ClassId, Root: newRoot.TryGetValue(r.Slot, out int moved) ? moved : r.Root)).OrderBy(r => r.Root).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            Put32(0x50 + 4 * (i + 1), ordered[i].Root);
            BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0x150 + 2 * (i + 1)), (ushort)ordered[i].ClassId);
        }
        return growth;
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
        List<(int Offset, int SourceRow, int RowOffset)> rowPointers = [];
        foreach (var b in blocks)
        {
            s[b.Start..b.End].CopyTo(packed.AsSpan(newOffset[b.Start]));
            foreach (int f in sc.Fixups.Where(f => f >= b.Start && f < b.End))
            {
                int t = BinaryPrimitives.ReadInt32LittleEndian(s[f..]);
                var owner = blocks.FirstOrDefault(x => t >= x.Start && t < x.End);
                int at = newOffset[b.Start] + (f - b.Start);
                if (owner != default) internalPtrs.Add((at, newOffset[owner.Start] + (t - owner.Start)));
                else
                {
                    BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(at), 0); dropped++;
                    if (t >= sc.RowsOffset && t < sc.RowsOffset + sc.RowCount * RowStride)
                        rowPointers.Add((at, (t - sc.RowsOffset) / RowStride, (t - sc.RowsOffset) % RowStride));
                }
            }
        }
        return new(levelKey, index, row, packed, internalPtrs, dropped) { ExternalRowPointers = rowPointers };
    }

    /// <summary>Appends rows into the Moby arena and their properties at the end of the properties component.</summary>
    public (int PropertiesBytes, int SceneGrowth, IReadOnlyList<int> NewRowIndices) AppendRows(IReadOnlyList<(byte[] Row, UniversalRowTemplate Template)> rows,
        IReadOnlyCollection<int>? removedRows = null)
    {
        if (rows.Count == 0 && removedRows is not { Count: > 0 }) return (0, 0, []);
        var (so, sl) = Subfiles()[3];
        var sc = Scene();
        byte[] s = _data.AsSpan(so, sl).ToArray();
        var removed = UniversalRowRemoval.Normalize(sc.RowCount, removedRows);
        foreach (int row in removed)
        {
            int start = sc.RowsOffset + row * RowStride;
            if (BinaryPrimitives.ReadInt32LittleEndian(s.AsSpan(start)) != 0 ||
                BinaryPrimitives.ReadUInt16LittleEndian(s.AsSpan(start + 0x36)) != UniversalMobyComposer.InertClass ||
                sc.Fixups.Any(f => f >= start && f < start + RowStride))
                throw new InvalidDataException("Deleted rows must pass owned property/link removal before their physical slots can be reused.");
        }
        int insert = sc.PropertiesEnd;
        int total = rows.Sum(r => Align(r.Template.Properties.Length, 4));
        var allocation = UniversalRowRemoval.Allocate(this, removedRows, rows.Count);
        List<int> newFix = [], newIdx = [];
        var activeTemplates = rows.Select(r => r.Template).ToArray();
        using MemoryStream blob = new();
        List<int> propStarts = [];
        int nextStart=insert; foreach(var row in rows){propStarts.Add(row.Template.Properties.Length==0?0:nextStart);nextStart=checked(nextStart+Align(row.Template.Properties.Length,4));}
        foreach (var (_, t) in rows)
        {
            int start = insert + (int)blob.Length;
            byte[] p = new byte[Align(t.Properties.Length, 4)];
            t.Properties.CopyTo(p, 0);
            foreach (var (off, target) in t.InternalPointers)
            {
                BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(off), start + target);
                newFix.Add(start + off);
            }
            foreach (var (off, placement, rowOffset) in t.ImportedRowPointers)
            {
                UniversalObjectRowLinks.RequireTarget(t, activeTemplates, off, placement, rowOffset);
                BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(off), sc.RowsOffset + allocation.NewRows[placement] * RowStride + rowOffset);
                newFix.Add(start + off);
            }
            UniversalIndexedRows.Apply(t, rows, allocation, p);
            UniversalBeastMistyBoarPlacement.ApplyChargePointer(t,rows,propStarts,p);
            blob.Write(p);
        }
        List<int> fixups = sc.Fixups.Select(f => f >= insert ? f + total : f).ToList();
        int addedFixupBytes = 4 * (newFix.Count + propStarts.Count(p => p != 0));
        byte[] body = [.. s.AsSpan(0, insert), .. blob.ToArray(), .. s.AsSpan(insert, sc.FixupsOffset - insert)];
        foreach (int f in fixups)
        {
            int v = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(f));
            if (v >= insert) BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(f), v + total + (removed.Count > 0 && v >= sc.UsedEnd ? addedFixupBytes : 0));
        }
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(sc.PropertiesComponent), sc.PropertiesSize + total);
        for (int i = 0; i < rows.Count; i++)
        {
            int ro = sc.RowsOffset + allocation.NewRows[i] * RowStride;
            if (allocation.NewRows[i] >= sc.RowCount && body.AsSpan(ro, RowStride).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("The Moby arena slot for an appended row is not empty.");
            byte[] r = rows[i].Row.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(r, propStarts[i]);
            r.CopyTo(body, ro);
            if (propStarts[i] != 0) fixups.Add(ro);
            newIdx.Add(allocation.NewRows[i]);
        }
        // Trimmed rows become native dynamic arena space; stale descriptors cannot survive there.
        if (allocation.FinalRowCount < sc.RowCount)
            body.AsSpan(sc.RowsOffset + allocation.FinalRowCount * RowStride, (sc.RowCount - allocation.FinalRowCount) * RowStride).Clear();
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(sc.MobyComponent + 4), allocation.FinalRowCount);
        fixups.AddRange(newFix);
        using MemoryStream outp = new();
        outp.Write(body);
        Span<byte> w = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(w, fixups.Count); outp.Write(w);
        foreach (int f in fixups) { BinaryPrimitives.WriteInt32LittleEndian(w, f); outp.Write(w); }
        byte[] scene = outp.ToArray();
        if (removed.Count > 0)
        {
            ReplaceScene(scene);
            return (total, Subfiles()[3].Length - sl, newIdx);
        }
        int grow = Align(Math.Max(0, scene.Length - sl), 0x800);
        if (grow > 0) GrowSubfile(3, grow);
        Array.Clear(_data, so, sl + grow);
        scene.CopyTo(_data, so);
        return (total, grow, newIdx);
    }

    /// <summary>Bytes one dynamic object takes from the Moby arena: a 0x58-byte row growing up from the placed rows and
    /// a 0x18-byte record growing down from the arena end (allocator 0x800524C4).</summary>
    public const int ArenaSlotBytes = RowStride + 0x18;

    /// <summary>Grows the Moby component's dynamic arena by <paramref name="slots"/> zeroed slots inserted at its end.
    /// The properties, pod, collision and fixup components move up with every fixed-up pointer at or past that point:
    /// the native scene loader (0x80013F24) derives each component's base from the lengths before it and the arena's
    /// capacity from the Moby component's length. Rows keep their offsets.</summary>
    public void GrowArena(int slots)
    {
        if (slots <= 0) return;
        var sc = Scene(); var sf = Subfiles()[3];
        byte[] original = _data.AsSpan(sf.Offset, sf.Length).ToArray();
        int growth = checked(slots * ArenaSlotBytes), insert = sc.ArenaEnd;
        int Move(int v) => v >= insert ? checked(v + growth) : v;
        byte[] scene = new byte[checked(sc.UsedEnd + growth)];
        original.AsSpan(0, insert).CopyTo(scene);
        original.AsSpan(insert, sc.UsedEnd - insert).CopyTo(scene.AsSpan(insert + growth));
        BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(sc.MobyComponent), checked(sc.MobyComponentSize + growth));
        foreach (int f in sc.Fixups)
            BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(Move(f)), Move(BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(f))));
        int table = Move(sc.FixupsOffset);
        for (int i = 0; i < sc.Fixups.Count; i++) BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(table + 4 + 4 * i), Move(sc.Fixups[i]));
        ReplaceScene(scene);
        var after = Scene();
        if (after.RowCount != sc.RowCount || after.ArenaEnd != sc.ArenaEnd + growth || after.PropertiesComponent != sc.PropertiesComponent + growth ||
            after.PodComponent != sc.PodComponent + growth || after.CollisionComponent != sc.CollisionComponent + growth ||
            after.FixupsOffset != sc.FixupsOffset + growth || after.Fixups.Count != sc.Fixups.Count)
            throw new InvalidDataException("Growing the spare object slots lost a scene component.");
    }

    internal void ReplaceScene(ReadOnlySpan<byte> scene)
    {
        var sf = Subfiles(); var old = sf[3];
        var layout = Scene(); int used = layout.UsedEnd;
        if (used > old.Length) throw new InvalidDataException("Permanent removal has an invalid native scene tail.");
        var oldTail = _data.AsSpan(old.Offset + used, old.Length - used);
        bool preserveTail = oldTail.IndexOfAnyExcept((byte)0) >= 0 || layout.Fixups.Any(f => U32(old.Offset + f) >= used);
        int tailBytes = preserveTail ? oldTail.Length : 0;
        int length = Align(scene.Length + tailBytes, 0x800), delta = length - old.Length;
        byte[] next = new byte[_data.Length + delta];
        _data.AsSpan(0, old.Offset).CopyTo(next);
        scene.CopyTo(next.AsSpan(old.Offset));
        if (preserveTail) oldTail.CopyTo(next.AsSpan(old.Offset + scene.Length));
        _data.AsSpan(old.Offset + old.Length).CopyTo(next.AsSpan(old.Offset + length));
        _data = next; Put32(8 * 3 + 4, length);
        for (int i = 4; i < sf.Count; i++) Put32(8 * i, sf[i].Offset + delta);
    }

    public Span<byte> Vram() { var (o, _) = Subfiles()[0]; return _data.AsSpan(o, 0x80000); }

    public static int Align(int v, int a) => (v + a - 1) / a * a;
}
