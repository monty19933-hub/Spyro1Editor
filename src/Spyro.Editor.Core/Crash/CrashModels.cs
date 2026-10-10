using System.Buffers.Binary;

namespace Spyro.Editor.Core.Crash;

/// <summary>Crash 2/3 models (entry type 2, TGEO) and animation frames (entry type 1, SVTX), read the way the
/// community editor CrashEdit reads them (cbhacks/CrashEdit: ModelEntry, ModelStruct, ModelTexture, Frame).
/// Model item 0: scale x/y/z, eight texture-page IDs at 0xC, struct count 0x2C, texture count 0x34, vertex count
/// 0x38, colour count 0x3C, page count 0x40, special vertices 0x4C. Item 1 holds triangle and colour-pair words,
/// item 2 RGBX colours, item 3 12-byte texture records, item 5 (compressed models) per-vertex delta widths.
/// A Crash 3 frame: int16 x/y/z offsets, vertex count at 16, collision count at 20, header size at 24, 40-byte
/// collision boxes from 28, then 3-byte special vertices and the vertex bytes (or a bit stream when compressed).
/// Model space: (byte * 8 + offset / 4) * scale on each axis, with the stored y and z bytes swapped.</summary>
public sealed class CrashModel
{
    public sealed record Texture(byte[] U, byte[] V, int ClutX, int ClutY, int Segment, int Blend, int Mode, int Page);
    public sealed record Triangle(int[] Vertices, int[] Colours, int Texture, int Type, int Subtype);
    private sealed record Delta(int X, int Y, int Z, int XBits, int YBits, int ZBits);

    private readonly Delta[]? _deltas;

    public CrashModel(IReadOnlyList<byte[]> items)
    {
        byte[] info = items[0];
        int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(info.AsSpan(o));
        Scale = (I(0), I(4), I(8));
        int structs = I(0x2C), pages = I(0x40);
        VertexCount = I(0x38); SpecialVertexCount = I(0x4C);
        Pages = Enumerable.Range(0, pages).Select(k => CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(0xC + 4 * k)))).ToArray();
        Colours = Enumerable.Range(0, items[2].Length / 4).Select(i => (items[2][4 * i], items[2][4 * i + 1], items[2][4 * i + 2])).ToArray();
        Textures = Enumerable.Range(0, items[3].Length / 12).Select(i =>
        {
            var b = items[3].AsSpan(12 * i, 12);
            return new Texture([b[0], b[4], b[8], b[10]], [b[1], b[5], b[9], b[11]], (b[2] & 15) * 16, b[3] << 2 | b[2] >> 6 & 3,
                b[6] & 15, b[6] >> 5 & 3, (b[6] >> 7 | b[7] << 1 & 2) & 3, b[7] >> 2 & 63);
        }).ToArray();
        if (items.Count == 6)
            _deltas = Enumerable.Range(0, items[5].Length / 4).Select(k =>
            {
                int s = BinaryPrimitives.ReadInt32LittleEndian(items[5].AsSpan(4 * k));
                return new Delta(s >> 25 & 0x7F, s >> 9 & 0xFF, s >> 17 & 0xFF, s >> 6 & 7, s & 7, s >> 3 & 7);
            }).ToArray();
        uint[] words = Enumerable.Range(0, structs).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(items[1].AsSpan(4 * i))).ToArray();
        // Item 4: animated textures (Crash's blinking eyes). A triangle with bit 8 set names one of these (from 0)
        // instead of a texture; each word's low 12 bits are the texture (from 1) of its first frame.
        uint[] animated = items.Count > 4 ? Enumerable.Range(0, I(0x48)).Where(k => 4 * k + 4 <= items[4].Length)
            .Select(k => BinaryPrimitives.ReadUInt32LittleEndian(items[4].AsSpan(4 * k))).ToArray() : [];
        Triangles = Convert(words, _deltas != null ? SpecialVertexCount : 0, animated);
    }

    public (int X, int Y, int Z) Scale { get; }
    public int VertexCount { get; }
    public int SpecialVertexCount { get; }
    public bool Compressed => _deltas != null;
    public IReadOnlyList<string> Pages { get; }
    public IReadOnlyList<(byte R, byte G, byte B)> Colours { get; }
    public IReadOnlyList<Texture> Textures { get; }
    /// <summary>Triangles with vertex indices relative to the frame's first non-special vertex.</summary>
    public IReadOnlyList<Triangle> Triangles { get; }

    private sealed record Item(bool IsColour, int C1, int C2, int Tex, int Col, int Key, bool Duplicate, int Sub, int Type);

    private static IReadOnlyList<Triangle> Convert(uint[] words, int special, IReadOnlyList<uint> animated)
    {
        List<Item> items = [];
        foreach (uint w in words)
        {
            if (w == 0xFFFFFFFF) break;
            int tex = (int)(w & 0xFF);
            // An animated texture becomes the texture of its first frame (Spyro objects have no texture animation).
            if ((w >> 8 & 1) != 0) tex = tex < animated.Count ? (int)(animated[tex] & 0xFFF) : 0;
            items.Add((w & 0xFFFF0000) != 0
                ? new Item(false, 0, 0, tex, (int)(w >> 9 & 0x7F), (int)(w >> 16 & 0xFF), (w >> 26 & 1) != 0, (int)(w >> 28 & 3), (int)(w >> 30 & 3))
                : new Item(true, (int)(w >> 2 & 0x7F), (int)(w >> 9 & 0x7F), 0, 0, 0, false, 0, 0));
        }
        Dictionary<int, int> keys = []; List<int> vtx = []; int v = -special;
        foreach (var s in items.Where(s => !s.IsColour))
        {
            if (!s.Duplicate) { if (s.Key != 0x57) keys[s.Key] = v; vtx.Add(v++); }
            else vtx.Add(keys.TryGetValue(s.Key, out int k) ? k : throw new InvalidDataException("A Crash model reuses an unknown vertex."));
        }
        List<Triangle> result = [];
        int lastValidCc = -3, lastCc = -1, lastAa = -1, lastColour = -1, lastNonBb = -1, cur = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var s = items[i];
            if (s.IsColour) { lastColour = i; continue; }
            switch (s.Type)
            {
                case 0:
                {
                    lastAa = cur; lastNonBb = i;
                    int c2 = lastColour + 1 == i ? items[lastColour].C1 : items[i - 1].Col;
                    int c3 = lastColour + 1 == i ? items[lastColour].C2 : lastColour + 2 == i ? items[lastColour].C1 : items[i - 2].Col;
                    if (cur >= 2) result.Add(new([vtx[cur], vtx[cur - 1], vtx[cur - 2]], [s.Col, c2, c3], s.Tex, 0, s.Sub));
                    break;
                }
                case 1:
                {
                    int ci = lastColour < lastNonBb - 2 ? items[lastNonBb - 2].Col
                        : lastColour == lastNonBb - 2 ? items[lastColour].C1 : items[lastColour].C2;
                    int c2 = lastColour + 1 == i ? items[lastColour].C1 : items[i - 1].Col;
                    int third = lastCc > lastAa ? vtx[lastCc] : vtx[lastAa - 2];
                    result.Add(new([vtx[cur], vtx[cur - 1], third], [s.Col, c2, ci], s.Tex, 1, s.Sub));
                    break;
                }
                case 2:
                    if (i + 2 < items.Count && lastValidCc + 2 < i)
                    {
                        lastValidCc = i;
                        result.Add(new([vtx[cur], vtx[cur + 1], vtx[cur + 2]], [s.Col, items[i + 1].Col, items[i + 2].Col], s.Tex, 2, s.Sub));
                    }
                    lastCc = cur; lastNonBb = i;
                    break;
            }
            cur++;
        }
        return result;
    }

    /// <summary>Model-space vertex positions of a Crash 3 frame (special vertices first): z up, facing -y.</summary>
    public IReadOnlyList<(double X, double Y, double Z)> Positions(byte[] frame)
    {
        short ox = BinaryPrimitives.ReadInt16LittleEndian(frame), oy = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(2)), oz = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(16)), collisions = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(20));
        int header = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(24)), fake = 28 + collisions * 0x28, special = (header - fake) / 3;
        List<(int, int, int)> raw = [];
        for (int i = 0; i < special; i++) raw.Add((frame[fake + 3 * i], frame[fake + 3 * i + 1], frame[fake + 3 * i + 2]));
        if (_deltas == null)
            for (int i = special, b = fake + 3 * special; i < count; i++, b += 3) raw.Add((frame[b], frame[b + 1], frame[b + 2]));
        else
        {
            int bit = special * 24, xa = 0, ya = 0, za = 0;
            int Bit() { int w = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(fake + bit / 32 * 4)); return w >> (31 - bit++ % 32) & 1; }
            int Read(int n) { int value = Bit() != 0 ? -1 << n : 0; for (int b = 0; b < n; b++) value |= Bit() << (n - 1 - b); return value; }
            raw.Clear();
            foreach (var d in _deltas)
            {
                if (d.XBits == 7) xa = 0;
                if (d.YBits == 7) ya = 0;
                if (d.ZBits == 7) za = 0;
                int x = Read(d.XBits), z = Read(d.ZBits), y = Read(d.YBits);
                xa = xa + x + (d.X << 1) & 0xFF; ya = ya + y + d.Y & 0xFF; za = za + z + d.Z & 0xFF;
                raw.Add((xa, ya, za));
            }
        }
        // CrashEdit (AnimationRenderer) reads the stored bytes as (x, z, y) against the (x, y, z) offsets and scales,
        // with y up; returned as (x, -z, y) so z is up and the axes stay right-handed. Models face -y.
        return raw.Select(p => ((p.Item1 * 8 + ox / 4.0) * Scale.X, -(p.Item2 * 8 + oz / 4.0) * Scale.Z, (p.Item3 * 8 + oy / 4.0) * Scale.Y)).ToArray();
    }

    public static int FrameSpecialVertexCount(byte[] frame)
    {
        int collisions = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(20)), header = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(24));
        return (header - 28 - collisions * 0x28) / 3;
    }

    /// <summary>15-bit colour of a texture record's texel (u, v) on its 64 KB page; 0 when transparent.</summary>
    public static ushort Texel(byte[] page, Texture t, int u, int v)
    {
        ushort Half(int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan((y & 127) * 512 + (x & 255) * 2));
        return t.Mode switch
        {
            0 => Half(t.ClutX + (Half(t.Segment * 64 + u / 4, v) >> (u % 4 * 4) & 15), t.ClutY),
            1 => Half(t.ClutX + (Half(t.Segment * 64 + u / 2, v) >> (u % 2 * 8) & 255), t.ClutY),
            _ => Half(t.Segment * 64 + u, v),
        };
    }
}

/// <summary>A Crash 3 object's animations: (animation entry, model entry, frames shown, interpolated) from the
/// type-1 frame groups of its GOOL entry (item 5).</summary>
public static class CrashGool
{
    public sealed record Group(string Animation, string Model, int Frames, bool Interpolated);

    public static IReadOnlyList<Group> VertexGroups(CrashNsf nsf, string gool)
    {
        var entry = nsf.Entry(gool);
        if (entry.Type != 11 || entry.Items.Count < 6) return [];
        byte[] d = entry.Items[5];
        List<Group> result = [];
        for (int o = 0; o + 20 <= d.Length; o += 2)
        {
            if (BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(o)) != 1) continue;
            string model = CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o + 12)));
            string animation = CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o + 16)));
            if (!nsf.Has(model, 2) || !nsf.Has(animation, 1)) continue;
            result.Add(new(animation, model, BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(o + 2)), BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o + 4)) != 0));
        }
        return result;
    }
}
