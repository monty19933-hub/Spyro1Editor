using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Writes new images into a level's terrain textures in place, in the packed native layout
/// (Analysis.NativeStaticTerrainTextureLayout):
/// <list type="bullet">
/// <item>normal distance: 64x64 at 8 bpp as four 32x32 quarters (descriptors 3-6: top-left, bottom-left,
/// top-right, bottom-right), each with a 256-colour palette;</item>
/// <item>close up (descriptors 7-22): sixteen tiles of a 4x4 grid, either 16x16 windows onto the normal pixels or
/// separate 32x32 pieces of a 128x128 image; tile k sits at column (k&amp;1)|(k&gt;&gt;3&amp;1)&lt;&lt;1 and row
/// (k&gt;&gt;2&amp;1)|(k&gt;&gt;1&amp;1)&lt;&lt;1;</item>
/// <item>far away (descriptors 0-2, one shared rectangle): 32x32 at 4 bpp with sixteen distance palettes, the
/// fade toward the level's fog colour. New palettes keep each row's fade (a per-channel linear map from the
/// first row fitted on the old colours).</item>
/// </list>
/// Textures whose pixels or palettes overlap are written together with one palette; every one of them needs an
/// image, otherwise writing one would change the other.</summary>
public static class NativeTerrainTextureImageWriter
{
    public const int NormalSide = 64, CloseSide = 128;
    public sealed record Image(Rgba32[] Normal, Rgba32[] Close);

    public static (int X, int Y) CloseCell(int k) => ((k & 1) | (k >> 3 & 1) << 1, (k >> 2 & 1) | (k >> 1 & 1) << 1);

    /// <summary>One texture's 23 descriptors: far (0-2), normal (3-6), close (7-22).</summary>
    public sealed record Texture(IReadOnlyList<Tile> Descriptors);
    /// <summary>A descriptor's pixel rectangle (byte offset, row bytes, side) and palette (offset, rows of 1024).
    /// Mirrored tiles (orientation bits in the eighth byte) show the same pixels flipped.</summary>
    public sealed record Tile(int PixelByteOffset, int PixelRowByteLength, int TileSide, int PaletteByteOffset, int PaletteRowCount, bool Mirrored);

    /// <summary>The level's texture records from the start of its model subfile (count, then LQ and HQ blocks). A
    /// record that is not 4 bpp far / 8 bpp normal and close in the static page layout is null: it cannot take an
    /// image, but <see cref="Footprint"/> still knows the bytes it uses.</summary>
    public static IReadOnlyList<Texture?> Records(ReadOnlySpan<byte> textureList) => RawRecords(textureList).Select(Decode).ToArray();

    private static Texture? Decode(byte[] record)
    {
        if (!record.AsSpan(0, 8).SequenceEqual(record.AsSpan(8, 8)) || !record.AsSpan(0, 8).SequenceEqual(record.AsSpan(16, 8))) return null;
        var tiles = new Tile[23];
        for (int i = 0; i < 23; i++)
        {
            var d = record.AsSpan(i * 8, 8);
            bool low = i < 3;
            int side = low ? 32 : d[4] - d[0] + 1;
            if (d[5] != d[1] || d[4] < d[0] || d[1] + side > 256) return null;
            if (low ? d[0] % 2 != 0 || side != 32 || (d[6] & 0x88) != 0x08 : (d[6] & 0x88) != 0x88 || (i < 7 ? side != 32 : side is not (16 or 32)))
                return null;
            int x = low ? (d[6] & 7) * 128 + d[0] / 2 : (d[6] & 15) * 128 + d[0] - 1024;
            int y = ((d[6] & 16) != 0 ? 256 : 0) + d[1];
            int width = low ? 16 : side, paletteX, paletteY, rows;
            if (low) { paletteX = d[2] * 32 - 1024; paletteY = d[3] * 4; rows = 16; }
            else
            {
                ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(d[2..]);
                if ((clut & 0x8000) != 0) return null;
                paletteX = ((clut & 63) * 16 - 512) * 2; paletteY = clut >> 6; rows = 1;
            }
            int paletteWidth = low ? 32 : 512;
            if (x < 0 || x + width > 1024 || y + side > 512 || paletteX < 0 || paletteX + paletteWidth > 1024 || paletteY + rows > 512) return null;
            tiles[i] = new(y * 1024 + x, width, side, paletteY * 1024 + paletteX, rows, (d[7] & 0x7F) != 0);
        }
        return new(tiles);
    }

    private static byte[][] RawRecords(ReadOnlySpan<byte> textureList)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(textureList[4..]);
        if (count is <= 0 or > 512 || 8 + count * 184L > textureList.Length) throw new InvalidDataException("The level's texture list is malformed.");
        var result = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            result[i] = new byte[184];
            textureList.Slice(8 + i * 16, 16).CopyTo(result[i]);
            textureList.Slice(8 + count * 16 + i * 168, 168).CopyTo(result[i].AsSpan(16));
        }
        return result;
    }

    /// <summary>Pixel and palette rectangles (byte offset, row bytes, rows) of every record, decoded leniently so
    /// non-canonical textures are included (rectangles clipped to the upload).</summary>
    public static IReadOnlyList<IReadOnlyList<(int Offset, int Width, int Rows)>> Footprint(ReadOnlySpan<byte> textureList)
    {
        var result = new List<IReadOnlyList<(int, int, int)>>();
        foreach (byte[] r in RawRecords(textureList))
        {
            List<(int, int, int)> rects = [];
            for (int i = 0; i < 23; i++)
            {
                var d = r.AsSpan(i * 8, 8);
                bool low = i < 3;
                int side = low ? 32 : Math.Max(1, d[4] - d[0] + 1);
                int x = low ? (d[6] & 7) * 128 + d[0] / 2 : (d[6] & 15) * 128 + d[0] - 1024;
                int y = ((d[6] & 16) != 0 ? 256 : 0) + d[1];
                int width = low ? 16 : (d[6] >> 7 & 3) switch { 0 => (side + 1) / 2, 1 => side, _ => side * 2 };
                rects.Add(Clip(x, y, width, side));
                if (low) rects.Add(Clip(d[2] * 32 - 1024, d[3] * 4, 32, 16));
                else
                {
                    ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(d[2..]);
                    int depth = d[6] >> 7 & 3;
                    rects.Add(Clip(((clut & 63) * 16 - 512) * 2, clut >> 6 & 0x1FF, depth == 0 ? 32 : depth == 1 ? 512 : 0, 1));
                }
            }
            result.Add(rects.Where(c => c.Item2 > 0 && c.Item3 > 0).ToArray());
        }
        return result;
        static (int, int, int) Clip(int x, int y, int w, int h)
        {
            int x0 = Math.Clamp(x, 0, 1024), y0 = Math.Clamp(y, 0, 512), x1 = Math.Clamp(x + w, 0, 1024), y1 = Math.Clamp(y + h, 0, 512);
            return (y0 * 1024 + x0, x1 - x0, y1 - y0);
        }
    }

    /// <summary>Textures merged into groups that share pixel or palette bytes.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> SharingGroups(IReadOnlyList<IReadOnlyList<(int Offset, int Width, int Rows)>> footprint)
    {
        int[] parent = Enumerable.Range(0, footprint.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        int[] owner = new int[0x80000];
        Array.Fill(owner, -1);
        for (int t = 0; t < footprint.Count; t++)
            foreach (var (offset, width, rows) in footprint[t])
                for (int r = 0; r < rows; r++)
                    for (int b = offset + r * 1024, end = b + width; b < end; b++)
                    {
                        if (owner[b] < 0) owner[b] = t;
                        else { int a = Find(owner[b]), c = Find(t); if (a != c) parent[Math.Max(a, c)] = Math.Min(a, c); }
                    }
        return Enumerable.Range(0, footprint.Count).GroupBy(Find).Select(g => (IReadOnlyList<int>)g.Order().ToArray()).ToArray();
    }

    /// <summary>The level's VRAM upload with <paramref name="images"/> written into their textures.
    /// <paramref name="textureList"/> is the start of the level's model subfile.</summary>
    public static byte[] Write(ReadOnlySpan<byte> vram, ReadOnlySpan<byte> textureList, IReadOnlyDictionary<int, Image> images)
    {
        if (vram.Length != 0x80000) throw new InvalidDataException("Terrain textures are written into the level's 0x80000-byte VRAM upload.");
        var records = Records(textureList);
        foreach (var (id, image) in images)
        {
            if (id < 0 || id >= records.Count || image.Normal.Length != NormalSide * NormalSide || image.Close.Length != CloseSide * CloseSide)
                throw new InvalidDataException($"Texture {id} is not in this level, or its image has the wrong size.");
            if (records[id] == null)
                throw new InvalidDataException($"Texture {id} is animated or uses an unusual layout, so it can't take an image.");
        }
        byte[] output = vram.ToArray();
        foreach (var group in SharingGroups(Footprint(textureList)).Where(g => g.Any(images.ContainsKey)))
        {
            var missing = group.Where(t => !images.ContainsKey(t)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Terrain textures {string.Join(", ", group)} share their pixels or palettes in this level; " +
                    $"give {string.Join(", ", missing)} a new image as well (or none of them).");
            WriteGroup(vram, output, records!, group, images);
        }
        return output;
    }

    private static void WriteGroup(ReadOnlySpan<byte> original, byte[] output, IReadOnlyList<Texture?> records,
        IReadOnlyList<int> group, IReadOnlyDictionary<int, Image> images)
    {
        // One 256-colour palette for the whole group (normal and close tiers).
        var colours = group.SelectMany(t => images[t].Normal.Concat(images[t].Close)).Select(To15).ToArray();
        ushort[] palette = Palette(colours, 256);
        var lookup = new Dictionary<ushort, byte>();
        byte Index(Rgba32 c)
        {
            ushort k = To15(c);
            if (!lookup.TryGetValue(k, out byte i)) lookup[k] = i = (byte)Nearest(palette, k);
            return i;
        }
        byte[] paletteBytes = new byte[512];
        for (int i = 0; i < palette.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(paletteBytes.AsSpan(i * 2), palette[i]);

        HashSet<int> written = [];
        List<(int Offset, int Width, int Rows)> normalRects = [];
        foreach (int t in group)
            foreach (var d in records[t]!.Descriptors.Skip(3).Take(4)) normalRects.Add((d.PixelByteOffset, d.PixelRowByteLength, d.TileSide));
        bool InsideNormal(Tile d) => normalRects.Any(n =>
            Overlaps(d.PixelByteOffset, d.PixelRowByteLength, d.TileSide, n.Offset, n.Width, n.Rows));

        foreach (int t in group)
        {
            var image = images[t];
            var d = records[t]!.Descriptors;
            for (int q = 0; q < 4; q++)
            {
                var quarter = d[3 + q];
                int qx = (q >> 1) * 32, qy = (q & 1) * 32;
                if (written.Add(quarter.PixelByteOffset))
                    for (int y = 0; y < 32; y++)
                        for (int x = 0; x < 32; x++)
                            output[quarter.PixelByteOffset + y * 1024 + x] = Index(image.Normal[(qy + y) * NormalSide + qx + x]);
                paletteBytes.CopyTo(output, quarter.PaletteByteOffset);
            }
            for (int k = 0; k < 16; k++)
            {
                var tile = d[7 + k];
                paletteBytes.CopyTo(output, tile.PaletteByteOffset);
                if (InsideNormal(tile) || !written.Add(tile.PixelByteOffset)) continue;   // a window onto the normal pixels
                var (cx, cy) = CloseCell(k);
                int side = tile.TileSide, step = 32 / side;
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                        output[tile.PixelByteOffset + y * 1024 + x] = Index(image.Close[(cy * 32 + y * step) * CloseSide + cx * 32 + x * step]);
            }
            WriteFar(original, output, d[0], image.Normal, written);
        }
    }

    private static void WriteFar(ReadOnlySpan<byte> original, byte[] output, Tile far, Rgba32[] normal, HashSet<int> written)
    {
        if (!written.Add(far.PixelByteOffset)) return;
        var small = new (int R, int G, int B)[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int r = 0, g = 0, b = 0;
                for (int j = 0; j < 2; j++)
                    for (int i = 0; i < 2; i++) { var c = normal[(y * 2 + j) * NormalSide + x * 2 + i]; r += c.R; g += c.G; b += c.B; }
                small[y * 32 + x] = (r / 4, g / 4, b / 4);
            }
        ushort[] palette = Palette(small.Select(c => To15(new Rgba32((byte)c.R, (byte)c.G, (byte)c.B, 255))).ToArray(), 16);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x += 2)
            {
                int a = Nearest(palette, To15(small[y * 32 + x])), b = Nearest(palette, To15(small[y * 32 + x + 1]));
                output[far.PixelByteOffset + y * 1024 + x / 2] = (byte)(a | b << 4);
            }
        // Distance rows: row 0 is the palette itself; row r applies the old row 0 -> row r fade, per channel.
        (double R, double G, double B) Rgb(ushort c) => ((c & 31) << 3, (c >> 5 & 31) << 3, (c >> 10 & 31) << 3);
        var fade = new ushort[far.PaletteRowCount, 16];
        for (int row = 0; row < far.PaletteRowCount; row++)
            for (int i = 0; i < 16; i++) fade[row, i] = BinaryPrimitives.ReadUInt16LittleEndian(original[(far.PaletteByteOffset + row * 1024 + i * 2)..]);
        ushort Old(int row, int i) => fade[row, i];
        for (int row = 0; row < far.PaletteRowCount; row++)
        {
            var fits = new (double A, double B)[3];
            for (int ch = 0; ch < 3; ch++)
            {
                double[] xs = Enumerable.Range(0, 16).Select(i => Channel(Rgb(Old(0, i)), ch)).ToArray();
                double[] ys = Enumerable.Range(0, 16).Select(i => Channel(Rgb(Old(row, i)), ch)).ToArray();
                double mx = xs.Average(), my = ys.Average(), vx = xs.Sum(v => (v - mx) * (v - mx));
                double slope = vx > 0 ? xs.Zip(ys).Sum(p => (p.First - mx) * (p.Second - my)) / vx : mx > 0 ? my / mx : 1;
                fits[ch] = (slope, my - slope * mx);
            }
            for (int i = 0; i < 16; i++)
            {
                var c = Rgb(palette[i]);
                byte F(int ch) => (byte)Math.Clamp(Math.Round(fits[ch].A * Channel(c, ch) + fits[ch].B), 0, 255);
                ushort faded = To15(new Rgba32(F(0), F(1), F(2), 255));
                BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(far.PaletteByteOffset + row * 1024 + i * 2), faded);
            }
        }
    }

    private static double Channel((double R, double G, double B) c, int ch) => ch == 0 ? c.R : ch == 1 ? c.G : c.B;

    private static bool Overlaps(int a, int aw, int ar, int b, int bw, int br)
    {
        int ay = a / 1024, ax = a % 1024, by = b / 1024, bx = b % 1024;
        return ax < bx + bw && bx < ax + aw && ay < by + br && by < ay + ar;
    }

    /// <summary>15-bit colour; black becomes the darkest non-transparent colour.</summary>
    public static ushort To15(Rgba32 c) => To15((c.R, c.G, c.B));
    private static ushort To15((int R, int G, int B) c)
    {
        static int Five(int v) => Math.Clamp((v + 4) >> 3, 0, 31);
        int value = Five(c.R) | Five(c.G) << 5 | Five(c.B) << 10;
        return (ushort)(value == 0 ? 0x0421 : value);
    }

    private static int Nearest(ushort[] palette, ushort colour)
    {
        int best = 0, bestDistance = int.MaxValue;
        for (int i = 0; i < palette.Length; i++)
        {
            int dr = (palette[i] & 31) - (colour & 31), dg = (palette[i] >> 5 & 31) - (colour >> 5 & 31), db = (palette[i] >> 10 & 31) - (colour >> 10 & 31);
            int distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance) { bestDistance = distance; best = i; if (distance == 0) break; }
        }
        return best;
    }

    /// <summary>Up to <paramref name="size"/> colours: the distinct colours when they fit, else median cut. Unused
    /// entries repeat the last colour.</summary>
    private static ushort[] Palette(ushort[] colours, int size)
    {
        var distinct = colours.Distinct().ToList();
        List<ushort> result;
        if (distinct.Count <= size) result = distinct;
        else
        {
            List<List<ushort>> boxes = [colours.ToList()];
            int Channel5(ushort c, int ch) => c >> (5 * ch) & 31;
            while (boxes.Count < size)
            {
                var box = boxes.OrderByDescending(b => b.Count).First();
                if (box.Count < 2) break;
                int ch = Enumerable.Range(0, 3).MaxBy(k => box.Max(c => Channel5(c, k)) - box.Min(c => Channel5(c, k)));
                box.Sort((p, q) => Channel5(p, ch).CompareTo(Channel5(q, ch)));
                boxes.Remove(box);
                boxes.Add(box.GetRange(0, box.Count / 2)); boxes.Add(box.GetRange(box.Count / 2, box.Count - box.Count / 2));
            }
            result = boxes.Where(b => b.Count > 0).Select(b => To15(((int)b.Average(c => (c & 31) << 3), (int)b.Average(c => (c >> 5 & 31) << 3), (int)b.Average(c => (c >> 10 & 31) << 3)))).ToList();
        }
        while (result.Count < size) result.Add(result[^1]);
        return [.. result];
    }
}
