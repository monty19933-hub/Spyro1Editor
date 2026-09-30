using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>First-fit allocator over the right-half 512x512 VRAM image (1024 bytes per row).</summary>
public sealed class UniversalVramAllocator
{
    public const int RowBytes = 1024, Rows = 512, ImageBytes = RowBytes * Rows;
    private readonly bool[] _used = new bool[ImageBytes];
    private readonly short[] _run = new short[ImageBytes]; // free bytes from x to the end of its 128-byte page column

    public UniversalVramAllocator(IEnumerable<(long Offset, int Length)> protectedRanges)
    {
        foreach (var (o, l) in protectedRanges)
            for (long i = Math.Max(0, o); i < Math.Min(ImageBytes, o + l); i++) _used[i] = true;
        for (int y = 0; y < Rows; y++) RebuildRow(y);
    }

    public UniversalVramAllocator Clone()
    {
        var c = new UniversalVramAllocator([]);
        Array.Copy(_used, c._used, ImageBytes); Array.Copy(_run, c._run, ImageBytes);
        return c;
    }

    public int FreeBytes => _used.Count(u => !u);

    private void RebuildRow(int y)
    {
        int b = y * RowBytes;
        for (int col = 0; col < 8; col++)
        {
            short run = 0;
            for (int x = col * 128 + 127; x >= col * 128; x--)
            {
                run = _used[b + x] ? (short)0 : (short)(run + 1);
                _run[b + x] = run;
            }
        }
    }

    private void Mark(int x, int y, int w, int h)
    {
        for (int yy = y; yy < y + h; yy++) { Array.Fill(_used, true, yy * RowBytes + x, w); RebuildRow(yy); }
    }

    /// <summary>Allocates a w-byte by h-row rectangle entirely inside one 128-byte by 256-row texture page.</summary>
    public (int X, int Y)? Rectangle(int w, int h)
    {
        if (w <= 0 || h <= 0 || w > 128 || h > 256) return null;
        (int X, int Y, int Score)? best = null;
        for (int prow = 0; prow < 2; prow++)
            for (int pcol = 0; pcol < 8; pcol++)
                for (int x = pcol * 128; x + w <= pcol * 128 + 128; x += 2)
                {
                    int streak = 0;
                    for (int y = prow * 256; y < prow * 256 + 256; y++)
                    {
                        int run = _run[y * RowBytes + x];
                        streak = run >= w ? streak + 1 : 0;
                        if (streak >= h)
                        {
                            // Best fit: prefer the narrowest free strip, so wide free areas stay available.
                            int top = y - h + 1, score = 0;
                            for (int yy = top; yy <= y; yy++) score += _run[yy * RowBytes + x] - w;
                            bool leftEdge = x == pcol * 128 || _used[top * RowBytes + x - 1];
                            if (!leftEdge) score += 64 * h;
                            if (best == null || score < best.Value.Score) best = (x, top, score);
                            if (score == 0) break;
                        }
                    }
                }
        if (best == null) return null;
        Mark(best.Value.X, best.Value.Y, w, h);
        return (best.Value.X, best.Value.Y);
    }

    public (int X, int Y)? Clut()
    {
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < RowBytes; x += 32)
                if (_run[y * RowBytes + x] >= 32) { Mark(x, y, 32, 1); return (x, y); }
        return null;
    }
}

public sealed record UniversalTextureReport(int Groups, int PixelBytes, int Cluts, int Reused);

public sealed record UniversalSoundReport(int SlotsBound, int SlotsReused, int DescriptorsAppended, int SampleBytesAppended, int SlotsSilenced);

/// <summary>Texture and sound dependencies of a copied Moby model package.</summary>
public static class UniversalAssetImport
{
    public static List<int> TextureDescriptorOffsets(ReadOnlySpan<byte> pkg)
    {
        List<int> refs = [];
        int n = BinaryPrimitives.ReadInt32LittleEndian(pkg);
        if (n <= 0 || n > 1024 || pkg.Length < 0x38 + 4 * n) return refs;
        int md = BinaryPrimitives.ReadInt32LittleEndian(pkg[0x34..]);
        SortedSet<int> faces = [];
        for (int i = 0; i < n; i++)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(pkg[(0x38 + 4 * i)..]);
            if (raw == uint.MaxValue || raw + 0x24 > pkg.Length) continue;
            faces.Add(md + BinaryPrimitives.ReadInt32LittleEndian(pkg[((int)raw + 0x14)..]));
            int low = BinaryPrimitives.ReadInt32LittleEndian(pkg[((int)raw + 0x1C)..]);
            if (low != 0) faces.Add(md + low);
        }
        foreach (int st in faces)
        {
            if (st < 0 || st + 4 > pkg.Length) throw new InvalidDataException("Model face table is outside its package.");
            int len = BinaryPrimitives.ReadInt32LittleEndian(pkg[st..]);
            int cur = st + 4, end = st + 4 + len;
            if (len < 0 || end > pkg.Length) throw new InvalidDataException("Model face table exceeds its package.");
            while (cur < end)
            {
                uint cmd = BinaryPrimitives.ReadUInt32LittleEndian(pkg[cur..]);
                int size, field;
                if ((cmd & 0x80000000) == 0) { size = (cmd & 2) != 0 ? 20 : 8; field = (cmd & 2) != 0 ? cur + 8 : -1; }
                else if ((cmd & 4) != 0) { size = 20; field = cur + 8; }
                else { size = (cmd & 2) != 0 ? 24 : 12; field = (cmd & 2) != 0 ? cur + 12 : -1; }
                if (field >= 0) refs.Add(field);
                cur += size;
            }
        }
        return refs;
    }

    /// <summary>Splits each page/palette group into independent regions: faces whose UV boxes overlap
    /// stay together, disjoint regions are allocated separately (smaller VRAM rectangles).</summary>
    static IEnumerable<(int Page, int Pal, List<int> Refs)> SplitClusters(byte[] pkg, Dictionary<(int Page, int Pal), List<int>> groups)
    {
        foreach (var ((page, pal), refs) in groups)
        {
            List<(int X0, int Y0, int X1, int Y1, List<int> Refs)> boxes = [];
            foreach (int f in refs)
            {
                int u0 = 255, u1 = 0, v0 = 255, v1 = 0;
                foreach (int k in new[] { 0, 4, 8, 10 })
                { u0 = Math.Min(u0, pkg[f + k]); u1 = Math.Max(u1, pkg[f + k]); v0 = Math.Min(v0, pkg[f + k + 1]); v1 = Math.Max(v1, pkg[f + k + 1]); }
                boxes.Add((u0 / 2, v0, u1 / 2, v1, [f]));
            }
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < boxes.Count && !merged; i++)
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        var a = boxes[i]; var b = boxes[j];
                        if (a.X0 <= b.X1 + 1 && b.X0 <= a.X1 + 1 && a.Y0 <= b.Y1 + 1 && b.Y0 <= a.Y1 + 1)
                        {
                            boxes[i] = (Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1), [.. a.Refs, .. b.Refs]);
                            boxes.RemoveAt(j); merged = true; break;
                        }
                    }
            }
            foreach (var b in boxes) yield return (page, pal, b.Refs);
        }
    }

    static (int, int, int, int) FaceBox(byte[] pkg, int f)
    {
        int u0 = 255, u1 = 0, v0 = 255, v1 = 0;
        foreach (int k in new[] { 0, 4, 8, 10 })
        { u0 = Math.Min(u0, pkg[f + k]); u1 = Math.Max(u1, pkg[f + k]); v0 = Math.Min(v0, pkg[f + k + 1]); v1 = Math.Max(v1, pkg[f + k + 1]); }
        return (u0 / 2, v0, u1 / 2, v1);
    }

    static int PixelOffset(int page, int u, int v) => ((page & 16) != 0 ? 256 + v : v) * 1024 + (page & 15) * 128 + u / 2 - 1024;
    static int ClutOffset(int pal) => (pal >> 6) * 1024 + (pal & 63) * 32 - 1024;

    /// <summary>Copies every textured region the package references from the source VRAM into free
    /// destination VRAM and rewrites the package's texture descriptors in place.</summary>
    public static UniversalTextureReport ImportTextures(byte[] pkg, ReadOnlySpan<byte> sourceVram, Span<byte> destVram,
        UniversalVramAllocator alloc) => ImportTexturesBatch([(pkg, sourceVram.ToArray(), "model")], destVram, alloc)[0];

    private sealed class TexRequest
    {
        public int Item, Page, Pal, Bx0, MinV, W, H;
        public List<int> Refs = [];
        public byte[] Rect = [];
    }

    /// <summary>Plans all texture regions of several packages first, then allocates them largest-first
    /// so small regions cannot fragment the space a larger region needs.</summary>
    public static IReadOnlyList<UniversalTextureReport> ImportTexturesBatch(IReadOnlyList<(byte[] Pkg, byte[] SourceVram, string Label)> items,
        Span<byte> destVram, UniversalVramAllocator alloc)
    {
        var groupCount = new int[items.Count]; var pixelBytes = new int[items.Count]; var cluts = new int[items.Count]; var reused = new int[items.Count];
        List<TexRequest> pending = [];
        for (int it = 0; it < items.Count; it++)
        {
            var (pkg, src, _) = items[it];
            Dictionary<(int Page, int Pal), List<int>> groups = [];
            foreach (int f in TextureDescriptorOffsets(pkg))
            {
                int pal = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(f + 2));
                int page = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(f + 6));
                if (((page >> 7) & 3) != 0) throw new InvalidDataException("Model uses a non-4bpp texture page; only 16-colour textures relocate.");
                if ((page & 15) < 8) throw new InvalidDataException("Model texture lives outside the level-owned VRAM half.");
                if (!groups.TryGetValue((page & 0x1F, pal), out var list)) groups[(page & 0x1F, pal)] = list = [];
                list.Add(f);
            }
            groupCount[it] = groups.Count;
            foreach (var (page, pal, refs) in SplitClusters(pkg, groups))
            {
                var r = MakeRequest(it, pkg, src, page, pal, refs);
                bool same = true;
                for (int row = 0; row < r.H && same; row++)
                    same = destVram.Slice(PixelOffset(page, r.Bx0 * 2, r.MinV + row), r.W).SequenceEqual(r.Rect.AsSpan(row * r.W, r.W));
                if (same && destVram.Slice(ClutOffset(pal), 32).SequenceEqual(src.AsSpan(ClutOffset(pal), 32))) { reused[it]++; continue; }
                pending.Add(r);
            }
        }
        Dictionary<string, int> clutByContent = [];
        List<TexRequest> placed = [];
        var queue = new List<TexRequest>(pending.OrderByDescending(r => r.W * r.H).ThenByDescending(r => r.H));
        while (queue.Count > 0)
        {
            var r = queue[0]; queue.RemoveAt(0);
            var found = alloc.Rectangle(r.W, r.H);
            if (found == null)
            {
                var pkg = items[r.Item].Pkg;
                var byBox = r.Refs.GroupBy(f => FaceBox(pkg, f)).ToList();
                if (byBox.Count > 1)
                {
                    foreach (var g in byBox) queue.Add(MakeRequest(r.Item, pkg, items[r.Item].SourceVram, r.Page, r.Pal, g.ToList()));
                    queue = queue.OrderByDescending(x => x.W * x.H).ThenByDescending(x => x.H).ToList();
                    continue;
                }
                throw new InvalidDataException($"Not enough free texture memory in the destination level for a {r.W * 2}x{r.H} texture region ({items[r.Item].Label}).");
            }
            var (x, y) = found.Value;
            for (int row = 0; row < r.H; row++) r.Rect.AsSpan(row * r.W, r.W).CopyTo(destVram.Slice((y + row) * 1024 + x, r.W));
            pixelBytes[r.Item] += r.W * r.H;
            byte[] clut = items[r.Item].SourceVram.AsSpan(ClutOffset(r.Pal), 32).ToArray();
            string ck = Convert.ToHexString(clut);
            if (!clutByContent.TryGetValue(ck, out int newPal))
            {
                var c = alloc.Clut() ?? throw new InvalidDataException("No free CLUT slot remains in the destination level.");
                clut.CopyTo(destVram.Slice(c.Y * 1024 + c.X, 32));
                newPal = (c.Y << 6) | ((c.X + 1024) / 32);
                clutByContent[ck] = newPal; cluts[r.Item]++;
            }
            int pcol = x / 128;
            int newPage = (8 + pcol) | (y >= 256 ? 16 : 0);
            int du = (x - pcol * 128) * 2 - r.Bx0 * 2, dv = (y % 256) - r.MinV;
            var p = items[r.Item].Pkg;
            foreach (int f in r.Refs)
            {
                foreach (int k in new[] { 0, 4, 8, 10 })
                {
                    p[f + k] = checked((byte)(p[f + k] + du));
                    p[f + k + 1] = checked((byte)(p[f + k + 1] + dv));
                }
                BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(f + 2), (ushort)newPal);
                int op = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(f + 6));
                BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(f + 6), (ushort)((op & ~0x1F) | newPage));
            }
        }
        return Enumerable.Range(0, items.Count).Select(i => new UniversalTextureReport(groupCount[i], pixelBytes[i], cluts[i], reused[i])).ToList();
    }

    private static TexRequest MakeRequest(int item, byte[] pkg, byte[] src, int page, int pal, List<int> refs)
    {
        int minU = 255, maxU = 0, minV = 255, maxV = 0;
        foreach (int f in refs)
            foreach (int k in new[] { 0, 4, 8, 10 })
            {
                minU = Math.Min(minU, pkg[f + k]); maxU = Math.Max(maxU, pkg[f + k]);
                minV = Math.Min(minV, pkg[f + k + 1]); maxV = Math.Max(maxV, pkg[f + k + 1]);
            }
        var r = new TexRequest { Item = item, Page = page, Pal = pal, Refs = refs, Bx0 = minU / 2, MinV = minV, W = maxU / 2 - minU / 2 + 1, H = maxV - minV + 1 };
        r.Rect = new byte[r.W * r.H];
        for (int row = 0; row < r.H; row++) src.AsSpan(PixelOffset(page, r.Bx0 * 2, minV + row), r.W).CopyTo(r.Rect.AsSpan(row * r.W));
        return r;
    }

    /// <summary>Rebinds the package's 16 local sound slots (bytes 4..19) to equivalent destination sounds,
    /// appending descriptors and ADPCM samples to the destination bank when absent.</summary>
    public static UniversalSoundReport ImportSounds(byte[] pkg, UniversalLevelData source, UniversalLevelData dest)
    {
        if (BinaryPrimitives.ReadInt32LittleEndian(pkg) <= 0) return new(0, 0, 0, 0, 0);
        var src = new NativeLevelSoundLayout(source.Bytes);
        int bound = 0, reused = 0, appended = 0, sampleBytes = 0, silenced = 0;
        for (int slot = 0; slot < 16; slot++)
        {
            byte b = pkg[4 + slot];
            if (b == 0xFF) continue;
            if (b >= src.Sound.Descriptors.Count) { pkg[4 + slot] = 0xFF; silenced++; continue; }
            var d = src.Sound.Descriptors[b];
            var dst = new NativeLevelSoundLayout(dest.Bytes);
            byte[] want = d.CopyDescriptor();
            int match = -1;
            foreach (var e in dst.Sound.Descriptors)
                if (e.Sample.Sha256 == d.Sample.Sha256 && e.CopyDescriptor().AsSpan(4).SequenceEqual(want.AsSpan(4))) { match = e.Index; break; }
            if (match >= 0) { pkg[4 + slot] = (byte)match; reused++; bound++; continue; }
            if (dst.Sound.Descriptors.Count >= 0xFE) { pkg[4 + slot] = 0xFF; silenced++; continue; }
            int? sampleOffset = dst.Sound.Descriptors.Where(e => e.Sample.Sha256 == d.Sample.Sha256)
                .Select(e => (int?)e.Sample.BankOffset).FirstOrDefault();
            if (sampleOffset == null)
            {
                byte[] sample = d.Sample.CopyBytes();
                int usedEnd = dst.Sound.Descriptors.Max(e => e.Sample.BankOffset + e.Sample.ByteLength);
                ReadOnlySpan<byte> bank = dest.Bytes.AsSpan(dst.BankOffset, NativeSoundBankTail(dst));
                usedEnd = UniversalLevelData.Align(Math.Max(usedEnd, bank.LastIndexOfAnyExcept((byte)0) + 1), 16);
                if (usedEnd + sample.Length > NativeSoundBankTail(dst))
                {
                    int need = usedEnd + sample.Length - NativeSoundBankTail(dst);
                    if (dst.VramLength - 0x80000 + UniversalLevelData.Align(need, 0x800) > 0x7EFF0)
                    { pkg[4 + slot] = 0xFF; silenced++; continue; }
                    dest.GrowSubfile(0, UniversalLevelData.Align(need, 0x800));
                    dst = new NativeLevelSoundLayout(dest.Bytes);
                }
                sample.CopyTo(dest.Bytes, dst.BankOffset + usedEnd);
                sampleOffset = usedEnd; sampleBytes += sample.Length;
            }
            BinaryPrimitives.WriteInt32LittleEndian(want, sampleOffset.Value);
            int newIndex = AppendDescriptor(dest, want);
            pkg[4 + slot] = (byte)newIndex; appended++; bound++;
        }
        return new(bound, reused, appended, sampleBytes, silenced);
    }

    static int NativeSoundBankTail(NativeLevelSoundLayout l) => l.VramLength - 0x80000;

    private static int AppendDescriptor(UniversalLevelData dest, byte[] descriptor)
    {
        var l = new NativeLevelSoundLayout(dest.Bytes);
        if (l.SoundEnd + 20 > l.DataOffset + l.DataLength) { dest.GrowSubfile(1, 0x800); l = new NativeLevelSoundLayout(dest.Bytes); }
        byte[] d = dest.Bytes;
        int comp = l.SoundOffset, len = l.Sound.ComponentByteLength, count = l.Sound.Descriptors.Count;
        if (d.AsSpan(comp + len, 20).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The destination sound component is not followed by free padding.");
        descriptor.CopyTo(d, comp + len);
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(comp), len + 20);
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(comp + 0x104), count + 1);
        return count;
    }
}
