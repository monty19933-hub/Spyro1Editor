using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>First-fit allocator over the right-half 512x512 VRAM image (1024 bytes per row).</summary>
public sealed class UniversalVramAllocator
{
    public const int RowBytes = 1024, Rows = 512, ImageBytes = RowBytes * Rows;
    private readonly bool[] _used = new bool[ImageBytes];
    private readonly List<NativeTexturePageOwnedRange> _reservations = [];
    public IReadOnlyList<NativeTexturePageOwnedRange> ImportedReservations => _reservations.ToArray();
    private readonly short[] _runFull = new short[ImageBytes];
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
        Array.Copy(_used, c._used, ImageBytes); Array.Copy(_run, c._run, ImageBytes); Array.Copy(_runFull, c._runFull, ImageBytes);
        c._reservations.AddRange(_reservations);
        return c;
    }

    public int FreeBytes => _used.Count(u => !u);

    /// <summary>Checks a candidate range without reserving or copying the VRAM ownership map.</summary>
    public bool IsFree(int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > ImageBytes)
            throw new InvalidDataException("Texture query is outside level VRAM.");
        return !_used.AsSpan(offset, length).Contains(true);
    }

    private void RebuildRow(int y)
    {
        int b = y * RowBytes;
        short fullRun = 0;
        for (int x = RowBytes - 1; x >= 0; x--) { fullRun = _used[b + x] ? (short)0 : (short)(fullRun + 1); _runFull[b + x] = fullRun; }
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
        for (int yy = y; yy < y + h; yy++) { Array.Fill(_used, true, yy * RowBytes + x, w); RebuildRow(yy); _reservations.Add(new(yy * RowBytes + x,w,"universal imported pixel/palette")); }
    }

    /// <summary>Allocates a w-byte by h-row rectangle entirely inside one 128-byte by 256-row texture page.</summary>
    public (int X, int Y)? Rectangle(int w, int h, int pageWidth = 128)
    {
        if (pageWidth is not (128 or 256 or 512)) throw new ArgumentOutOfRangeException(nameof(pageWidth));
        if (w <= 0 || h <= 0 || w > pageWidth || h > 256) return null;
        (int X, int Y, int Score)? best = null;
        for (int prow = 0; prow < 2; prow++)
            for (int pcol = 0; pcol < 8; pcol++)
                for (int x = pcol * 128; x < pcol * 128 + 128 && x + w <= Math.Min(RowBytes, pcol * 128 + pageWidth); x += 2)
                {
                    int streak = 0;
                    for (int y = prow * 256; y < prow * 256 + 256; y++)
                    {
                        int run = Math.Min(_runFull[y * RowBytes + x], pcol * 128 + pageWidth - x);
                        streak = run >= w ? streak + 1 : 0;
                        if (streak >= h)
                        {
                            // Best fit: prefer the narrowest free strip, so wide free areas stay available.
                            int top = y - h + 1, score = 0;
                            for (int yy = top; yy <= y; yy++) score += Math.Min(_runFull[yy * RowBytes + x], pcol * 128 + pageWidth - x) - w;
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

    /// <summary>Protects bytes used in place by an identical imported texture, including zero pixels.</summary>
    public void Reserve(int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > ImageBytes)
            throw new InvalidDataException("Texture reservation is outside level VRAM.");
        if (length > 0) _reservations.Add(new(offset,length,"universal imported texture used in place"));
        for (int i = offset; i < offset + length; i++) _used[i] = true;
        if (length > 0) for (int y = offset / RowBytes; y <= (offset + length - 1) / RowBytes; y++) RebuildRow(y);
    }

    /// <summary>A palette slot of <paramref name="rows"/> rows (an object palette and its distance shades).</summary>
    public (int X, int Y)? Clut(int bytes = 32, int rows = 1)
    {
        if (bytes is not (32 or 512)) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (rows is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(rows));
        for (int y = 0; y + rows <= Rows; y++)
            for (int x = 0; x + bytes <= RowBytes; x += 32)
            {
                bool free = true;
                for (int k = 0; k < rows && free; k++) free = _runFull[(y + k) * RowBytes + x] >= bytes;
                if (free) { Mark(x, y, bytes, rows); return (x, y); }
            }
        return null;
    }
}

public sealed record UniversalTextureReport(int Groups, int PixelBytes, int Cluts, int Reused);

public sealed record UniversalSoundReport(int SlotsBound, int SlotsReused, int DescriptorsAppended, int SampleBytesAppended, int SlotsSilenced)
{
    public int BankBytesReclaimed { get; init; }
}

/// <summary>Texture and sound dependencies of a copied Moby model package.</summary>
public static class UniversalAssetImport
{
    public static List<int> TextureDescriptorOffsets(ReadOnlySpan<byte> pkg)
    {
        List<int> refs = [];
        int n = BinaryPrimitives.ReadInt32LittleEndian(pkg);
        if (n <= 0) return refs;
        if (n > 1024 || pkg.Length < 0x38 + 4 * n) throw new InvalidDataException("Model animation roots exceed their package.");
        int md = BinaryPrimitives.ReadInt32LittleEndian(pkg[0x34..]);
        SortedSet<int> faces = [];
        for (int i = 0; i < n; i++)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(pkg[(0x38 + 4 * i)..]);
            if (raw == uint.MaxValue) continue;
            if ((long)raw + 0x24 > pkg.Length) throw new InvalidDataException("Model animation root is outside its package.");
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
                if (end - cur < 4) throw new InvalidDataException("Model face command is truncated.");
                uint cmd = BinaryPrimitives.ReadUInt32LittleEndian(pkg[cur..]);
                int size, field;
                if ((cmd & 0x80000000) == 0) { size = (cmd & 2) != 0 ? 20 : 8; field = (cmd & 2) != 0 ? cur + 8 : -1; }
                else if ((cmd & 4) != 0) { size = 20; field = cur + 8; }
                else { size = (cmd & 2) != 0 ? 24 : 12; field = (cmd & 2) != 0 ? cur + 12 : -1; }
                if (cur + size > end) throw new InvalidDataException("Model face command exceeds its face table.");
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
                int depth = (page >> 7) & 3;
                boxes.Add((PixelByte(u0, depth), v0, PixelByte(u1, depth) + (depth == 2 ? 1 : 0), v1, [f]));
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
        int depth = (BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(f + 6)) >> 7) & 3;
        return (PixelByte(u0, depth), v0, PixelByte(u1, depth) + (depth == 2 ? 1 : 0), v1);
    }

    static int PixelByte(int u, int depth) => depth == 0 ? u / 2 : u << (depth - 1);
    /// <summary>An object palette is followed by rows of the same colours shaded toward the level's distance colour;
    /// the game draws far objects with a lower row (Artisans' gnorc: rows 472-479). An imported palette brings them.</summary>
    public const int ShadeRows = 8;
    static int ShadeRowsAt(int pal, int vramLength) => Math.Clamp((vramLength - ClutOffset(pal)) / 1024, 1, ShadeRows);
    static int PixelOffset(int page, int byteU, int v) => ((page & 16) != 0 ? 256 + v : v) * 1024 + (page & 15) * 128 + byteU - 1024;
    static int PaletteBytes(int depth) => depth == 0 ? 32 : depth == 1 ? 512 : 0;
    static int ClutOffset(int pal) => (pal >> 6) * 1024 + (pal & 63) * 32 - 1024;

    /// <summary>Copies every textured region the package references from the source VRAM into free
    /// destination VRAM and rewrites the package's texture descriptors in place.</summary>
    public static UniversalTextureReport ImportTextures(byte[] pkg, ReadOnlySpan<byte> sourceVram, Span<byte> destVram,
        UniversalVramAllocator alloc) => ImportTexturesBatch([(pkg, sourceVram.ToArray(), "model")], destVram, alloc)[0];

    private sealed class TexRequest
    {
        public int Item, Page, Pal, Depth, Bx0, MinV, W, H;
        public List<int> Refs = [];
        public byte[] Rect = [];
    }

    /// <summary>Plans all texture regions of several packages first, then allocates them largest-first
    /// so small regions cannot fragment the space a larger region needs.</summary>
    public static IReadOnlyList<UniversalTextureReport> ImportTexturesBatch(IReadOnlyList<(byte[] Pkg, byte[] SourceVram, string Label)> items,
        Span<byte> destVram, UniversalVramAllocator alloc,
        IReadOnlyDictionary<int, UniversalMetalModelCertificate>? metalCertificates = null)
    {
        if (metalCertificates?.Keys.Any(i => i < 0 || i >= items.Count) == true)
            throw new InvalidDataException("Metal texture authority references an absent import package.");
        var groupCount = new int[items.Count]; var pixelBytes = new int[items.Count]; var cluts = new int[items.Count]; var reused = new int[items.Count];
        List<TexRequest> pending = [];
        for (int it = 0; it < items.Count; it++)
        {
            var (pkg, src, _) = items[it];
            Dictionary<(int Page, int Pal), List<int>> groups = [];
            var fields = metalCertificates != null && metalCertificates.TryGetValue(it, out var metal)
                ? metal.TextureDescriptorOffsets(pkg) : TextureDescriptorOffsets(pkg);
            foreach (int f in fields)
            {
                int pal = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(f + 2));
                int page = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(f + 6));
                if (((page >> 7) & 3) == 3) throw new InvalidDataException("Model uses a reserved texture depth; only 4-bpp, 8-bpp and 16-bpp pages relocate.");
                if ((page & 15) < 8) throw new InvalidDataException("Model texture lives outside the level-owned VRAM half.");
                if (!groups.TryGetValue((page & 0x19F, pal), out var list)) groups[(page & 0x19F, pal)] = list = [];
                list.Add(f);
            }
            groupCount[it] = groups.Count;
            foreach (var (page, pal, refs) in SplitClusters(pkg, groups))
            {
                var r = MakeRequest(it, pkg, src, page, pal, refs);
                bool same = true;
                for (int row = 0; row < r.H && same; row++)
                    same = destVram.Slice(PixelOffset(page, r.Bx0, r.MinV + row), r.W).SequenceEqual(r.Rect.AsSpan(row * r.W, r.W));
                int paletteBytes = PaletteBytes(r.Depth), shades = ShadeRowsAt(pal, src.Length);
                for (int k = 0; k < shades && same && paletteBytes > 0; k++)
                    same = destVram.Slice(ClutOffset(pal) + k * 1024, paletteBytes).SequenceEqual(src.AsSpan(ClutOffset(pal) + k * 1024, paletteBytes));
                if (same)
                {
                    for (int row = 0; row < r.H; row++) alloc.Reserve(PixelOffset(page, r.Bx0, r.MinV + row), r.W);
                    if (paletteBytes > 0) for (int k = 0; k < shades; k++) alloc.Reserve(ClutOffset(pal) + k * 1024, paletteBytes);
                    reused[it]++; continue;
                }
                pending.Add(r);
            }
        }
        Dictionary<string, int> clutByContent = [];
        List<TexRequest> placed = [];
        // Packages that share one source VRAM image (the editor's Crash boxes) reuse a region already copied
        // from the same source rectangle and palette instead of copying it again.
        Dictionary<(byte[] Source, int Page, int Pal, int Bx0, int MinV, int W, int H), (int X, int Y)> sharedRegions =
            new(new SharedRegionComparer());
        var queue = new List<TexRequest>(pending.OrderByDescending(r => r.W * r.H).ThenByDescending(r => r.H));
        while (queue.Count > 0)
        {
            var r = queue[0]; queue.RemoveAt(0);
            var regionKey = (items[r.Item].SourceVram, r.Page, r.Pal, r.Bx0, r.MinV, r.W, r.H);
            bool shared = sharedRegions.TryGetValue(regionKey, out var sharedAt);
            var found = shared ? sharedAt : alloc.Rectangle(r.W, r.H, 128 << r.Depth);
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
                throw new InvalidDataException($"Not enough free texture memory in the destination level for a {(r.Depth == 0 ? r.W * 2 : r.W >> (r.Depth - 1))}x{r.H} texture region ({items[r.Item].Label}).");
            }
            var (x, y) = found.Value;
            if (shared) reused[r.Item]++;
            else
            {
                for (int row = 0; row < r.H; row++) r.Rect.AsSpan(row * r.W, r.W).CopyTo(destVram.Slice((y + row) * 1024 + x, r.W));
                pixelBytes[r.Item] += r.W * r.H;
                if (items.Count(i => ReferenceEquals(i.SourceVram, items[r.Item].SourceVram)) > 1) sharedRegions[regionKey] = (x, y);
            }
            int newPal = r.Pal, paletteBytes = PaletteBytes(r.Depth);
            if (paletteBytes > 0)
            {
                // The palette with the rows of distance shades below it, which the game reads for far objects.
                var source = items[r.Item].SourceVram;
                int shades = ShadeRowsAt(r.Pal, source.Length);
                byte[][] clut = Enumerable.Range(0, shades).Select(k => source.AsSpan(ClutOffset(r.Pal) + k * 1024, paletteBytes).ToArray()).ToArray();
                string ck = string.Concat(clut.Select(Convert.ToHexString));
                if (!clutByContent.TryGetValue(ck, out newPal))
                {
                    var c = alloc.Clut(paletteBytes, ShadeRows) ?? throw new InvalidDataException("No free CLUT slot remains in the destination level.");
                    for (int k = 0; k < ShadeRows; k++) clut[Math.Min(k, shades - 1)].CopyTo(destVram.Slice((c.Y + k) * 1024 + c.X, paletteBytes));
                    newPal = (c.Y << 6) | ((c.X + 1024) / 32);
                    clutByContent[ck] = newPal; cluts[r.Item]++;
                }
            }
            int pcol = x / 128;
            int newPage = (8 + pcol) | (y >= 256 ? 16 : 0);
            int du = r.Depth == 0 ? (x - pcol * 128 - r.Bx0) * 2 : (x - pcol * 128 - r.Bx0) >> (r.Depth - 1);
            int dv = (y % 256) - r.MinV;
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
        if (metalCertificates != null)
            foreach (var (index, certificate) in metalCertificates) certificate.RequireShaderFields(items[index].Pkg);
        return Enumerable.Range(0, items.Count).Select(i => new UniversalTextureReport(groupCount[i], pixelBytes[i], cluts[i], reused[i])).ToList();
    }

    private sealed class SharedRegionComparer : IEqualityComparer<(byte[] Source, int Page, int Pal, int Bx0, int MinV, int W, int H)>
    {
        public bool Equals((byte[] Source, int Page, int Pal, int Bx0, int MinV, int W, int H) a, (byte[] Source, int Page, int Pal, int Bx0, int MinV, int W, int H) b) =>
            ReferenceEquals(a.Source, b.Source) && (a.Page, a.Pal, a.Bx0, a.MinV, a.W, a.H) == (b.Page, b.Pal, b.Bx0, b.MinV, b.W, b.H);
        public int GetHashCode((byte[] Source, int Page, int Pal, int Bx0, int MinV, int W, int H) k) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(k.Source), k.Page, k.Pal, k.Bx0, k.MinV, k.W, k.H);
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
        int depth = (page >> 7) & 3;
        int bx0 = PixelByte(minU, depth), bx1 = PixelByte(maxU, depth) + (depth == 2 ? 1 : 0);
        var r = new TexRequest { Item = item, Page = page, Pal = pal, Depth = depth, Refs = refs, Bx0 = bx0, MinV = minV, W = bx1 - bx0 + 1, H = maxV - minV + 1 };
        int sourceX = (page & 15) * 128 - 1024 + r.Bx0;
        if (sourceX < 0 || sourceX + r.W > UniversalVramAllocator.RowBytes)
            throw new InvalidDataException("Source model texture crosses the level-owned VRAM half; relocation is not proven.");
        int paletteBytes = PaletteBytes(depth), paletteX = (pal & 63) * 32 - 1024;
        if (paletteBytes > 0 && (paletteX < 0 || paletteX + paletteBytes > UniversalVramAllocator.RowBytes ||
            ClutOffset(pal) < 0 || ClutOffset(pal) + paletteBytes > src.Length))
            throw new InvalidDataException("Source model CLUT crosses the level-owned VRAM half; relocation is not proven.");
        r.Rect = new byte[r.W * r.H];
        for (int row = 0; row < r.H; row++) src.AsSpan(PixelOffset(page, r.Bx0, minV + row), r.W).CopyTo(r.Rect.AsSpan(row * r.W));
        return r;
    }

    /// <summary>Rebinds the package's 16 local sound slots (bytes 4..19) to equivalent destination sounds,
    /// appending descriptors and ADPCM samples to the destination bank when absent.</summary>
    public static UniversalSoundReport ImportSounds(byte[] pkg, UniversalLevelData source, UniversalLevelData dest, byte[]? nativeExecutable = null)
    {
        if (BinaryPrimitives.ReadInt32LittleEndian(pkg) <= 0) return new(0, 0, 0, 0, 0);
        var src = new NativeLevelSoundLayout(source.Bytes);
        int bound = 0, reused = 0, appended = 0, sampleBytes = 0, silenced = 0, reclaimed = 0;
        for (int slot = 0; slot < 16; slot++)
        {
            byte b = pkg[4 + slot];
            if (b == 0xFF) continue;
            if (b >= src.Sound.Descriptors.Count)
                throw new InvalidDataException($"Model sound slot {slot} references missing source descriptor {b}; sound dependency cannot be imported.");
            var d = src.Sound.Descriptors[b];
            var dst = new NativeLevelSoundLayout(dest.Bytes);
            byte[] want = d.CopyDescriptor();
            int match = -1;
            foreach (var e in dst.Sound.Descriptors)
                if (e.Sample.Sha256 == d.Sample.Sha256 && e.CopyDescriptor().AsSpan(4).SequenceEqual(want.AsSpan(4))) { match = e.Index; break; }
            if (match >= 0) { pkg[4 + slot] = (byte)match; reused++; bound++; continue; }
            if (dst.Sound.Descriptors.Count >= 0xFE)
                throw new InvalidDataException("The destination has no free sound descriptor slot; import would otherwise silence a required sound.");
            int? sampleOffset = dst.Sound.Descriptors.Where(e => e.Sample.Sha256 == d.Sample.Sha256)
                .Select(e => (int?)e.Sample.BankOffset).FirstOrDefault();
            if (sampleOffset == null)
            {
                byte[] sample = d.Sample.CopyBytes();
                int usedEnd = dst.Sound.Descriptors.Select(e => e.Sample.BankOffset + e.Sample.ByteLength).DefaultIfEmpty(0).Max();
                ReadOnlySpan<byte> bank = dest.Bytes.AsSpan(dst.BankOffset, NativeSoundBankTail(dst));
                usedEnd = UniversalLevelData.Align(Math.Max(usedEnd, bank.LastIndexOfAnyExcept((byte)0) + 1), 16);
                int storage = NativeSoundBankTail(dst);
                int growth = UniversalLevelData.Align(Math.Max(0, usedEnd + sample.Length - storage), 0x800);
                bool certified = false;
                if (usedEnd + sample.Length > Analysis.NativeSoundBankCodec.SpuUploadByteLength || storage + growth > Analysis.NativeSoundBankCodec.SpuUploadByteLength)
                {
                    if (nativeExecutable == null)
                        throw new InvalidDataException("The destination sound bank cannot fit a required sample; import would otherwise silence it.");
                    UniversalSoundBankStorage.RequireExecutable(nativeExecutable); certified = true;
                    int previousEnd = usedEnd;
                    usedEnd = UniversalSoundBankStorage.Compact(dest, dst);
                    reclaimed += previousEnd - usedEnd;
                    dst = new NativeLevelSoundLayout(dest.Bytes);
                    growth = UniversalLevelData.Align(Math.Max(0, usedEnd + sample.Length - storage), 0x800);
                }
                int maximumStorage = certified ? UniversalSoundBankStorage.MaximumStorageBytes : Analysis.NativeSoundBankCodec.SpuUploadByteLength;
                if (usedEnd + sample.Length > Analysis.NativeSoundBankCodec.SpuUploadByteLength || storage + growth > maximumStorage)
                    throw new InvalidDataException("The destination sound bank cannot fit a required sample; import would otherwise silence it.");
                if (growth > 0) { dest.GrowSubfile(0, growth); dst = new NativeLevelSoundLayout(dest.Bytes); }
                sample.CopyTo(dest.Bytes, dst.BankOffset + usedEnd);
                sampleOffset = usedEnd; sampleBytes += sample.Length;
            }
            BinaryPrimitives.WriteInt32LittleEndian(want, sampleOffset.Value);
            int newIndex = AppendDescriptor(dest, want);
            pkg[4 + slot] = (byte)newIndex; appended++; bound++;
        }
        return new(bound, reused, appended, sampleBytes, silenced) { BankBytesReclaimed = reclaimed };
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
