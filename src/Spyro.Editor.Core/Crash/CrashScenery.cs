using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Crash;

/// <summary>A point of a Crash 3 level in world units (x across, y up, z along the level; Crash runs towards -z).</summary>
public readonly record struct CrashPoint(int X, int Y, int Z);

/// <summary>A face of a scenery entry: three or four corners (indices into <see cref="CrashScenery.Vertices"/>; a quad's
/// corners are in the GPU's Z order, so its outline is 0, 1, 3, 2), the raw texture field and the full-size texture it
/// draws (null: untextured). Crash draws every face from both sides, so the corner order says nothing about the front.</summary>
public sealed record CrashSceneryFace(int[] Corners, bool Quad, int Field, bool UsesSlot, int? Texture)
{
    /// <summary>The corners in perimeter order (a quad's Z order turned into its outline).</summary>
    public int[] Outline => Quad ? [Corners[0], Corners[1], Corners[3], Corners[2]] : Corners;
}

/// <summary>Crash Bandicoot: Warped scenery (NSF entry type 3, "WGEO", seven items), decoded as found by probing the
/// USA release (2026-10-10) and consistent with the community editor CrashEdit's scenery loader:
/// <list type="bullet">
/// <item>item 0, a 76-byte header: int32 origin x, y, z; +12 1 = sky (drawn around the camera); counts at +16 (vertices),
/// +20 (triangles), +24 (quads), +28 (texture infos), +32 (colours), +36 (texture slots), +40 (texture pages); up to
/// eight texture page IDs from +44;</item>
/// <item>item 1, vertices: n 32-bit xy words stored in REVERSE order, then n 16-bit z words in order; each axis is
/// origin + (value &gt;&gt; 4) * 16, the value an unsigned 12-bit step; the colour index is
/// (z &amp; 15) | (x &amp; 15) &lt;&lt; 4 | (y &amp; 3) &lt;&lt; 8;</item>
/// <item>item 2, triangles: n 32-bit words in REVERSE order (a), then n 16-bit words in order (b); corners a &gt;&gt; 8,
/// a &gt;&gt; 20 and b &gt;&gt; 4 (12 bits each);</item>
/// <item>item 3, quads: 8 bytes each (wa, wb); corners wa &gt;&gt; 8, wa &gt;&gt; 20, wb &gt;&gt; 8, wb &gt;&gt; 20 in Z order;</item>
/// <item>texture field = (word0 &amp; 0x7F) &lt;&lt; 4 | (b or wb) &amp; 15: with bit 7 of word0 it indexes the slot table
/// (item 6; a slot's low 10 bits name a 1-based texture info, bit 31 marks a level-of-detail chain whose full-size
/// texture is that one; otherwise an animation whose first frame it is), else it is a 1-based texture info (0: none);</item>
/// <item>item 4, 12-byte GPU texture infos (as models store them, <see cref="CrashModel.Texture"/>); item 5, RGBX
/// colours (128 = full brightness, the GPU multiplies texels by colour / 128).</item>
/// </list></summary>
public sealed class CrashScenery
{
    public const int EntryType = 3, HeaderBytes = 76;

    private CrashScenery(string name) { Name = name; }

    public string Name { get; }
    public CrashPoint Origin { get; private init; }
    /// <summary>Sky scenery: drawn around the camera, not part of the level's ground.</summary>
    public bool IsSky { get; private init; }
    public IReadOnlyList<string> Pages { get; private init; } = [];
    public IReadOnlyList<ColorRgba> Colours { get; private init; } = [];
    public IReadOnlyList<CrashModel.Texture> Textures { get; private init; } = [];
    public IReadOnlyList<uint> Slots { get; private init; } = [];
    public IReadOnlyList<CrashPoint> Vertices { get; private init; } = [];
    /// <summary>Each vertex's colour (a colour index past the table reads as full brightness).</summary>
    public IReadOnlyList<ColorRgba> VertexColours { get; private init; } = [];
    public IReadOnlyList<CrashSceneryFace> Faces { get; private init; } = [];

    /// <summary>The page name of a texture info (null when it names a page the header does not list).</summary>
    public string? PageOf(CrashModel.Texture t) => t.Page < Pages.Count ? Pages[t.Page] : null;

    public static CrashScenery Read(CrashEntry entry)
    {
        if (entry.Type != EntryType || entry.Items.Count != 7) throw new InvalidDataException($"{entry.Name} is not Crash scenery.");
        var items = entry.Items;
        byte[] h = items[0];
        if (h.Length < HeaderBytes) throw new InvalidDataException($"{entry.Name}: scenery header too short.");
        int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(h.AsSpan(o));
        int ox = I(0), oy = I(4), oz = I(8), sky = I(12), nv = I(16), nt = I(20), nq = I(24), ntex = I(28), ncol = I(32), nslot = I(36), npg = I(40);
        if (nv < 0 || nt < 0 || nq < 0 || ntex < 0 || ncol < 0 || nslot < 0 || npg is < 0 or > 8 ||
            items[1].Length < nv * 6 || items[2].Length < nt * 6 || items[3].Length < nq * 8 || items[4].Length < ntex * 12 ||
            items[5].Length < ncol * 4 || items[6].Length < nslot * 4)
            throw new InvalidDataException($"{entry.Name}: scenery counts do not match its items.");
        var pages = Enumerable.Range(0, npg).Select(k => CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(44 + 4 * k)))).ToArray();
        var colours = Enumerable.Range(0, ncol).Select(k => new ColorRgba(items[5][4 * k], items[5][4 * k + 1], items[5][4 * k + 2])).ToArray();
        var textures = Enumerable.Range(0, ntex).Select(k => ReadTexture(items[4].AsSpan(12 * k, 12))).ToArray();
        var slots = Enumerable.Range(0, nslot).Select(k => BinaryPrimitives.ReadUInt32LittleEndian(items[6].AsSpan(4 * k))).ToArray();
        var vertices = new CrashPoint[nv];
        var vertexColours = new ColorRgba[nv];
        for (int i = 0; i < nv; i++)
        {
            uint xy = BinaryPrimitives.ReadUInt32LittleEndian(items[1].AsSpan((nv - 1 - i) * 4));
            int z = BinaryPrimitives.ReadUInt16LittleEndian(items[1].AsSpan(nv * 4 + 2 * i));
            int x = (int)(xy & 0xFFFF), y = (int)(xy >> 16);
            vertices[i] = new(ox + (x >> 4) * 16, oy + (y >> 4) * 16, oz + (z >> 4) * 16);
            int ci = (z & 15) | (x & 15) << 4 | (y & 3) << 8;
            vertexColours[i] = ci < ncol ? colours[ci] : new ColorRgba(128, 128, 128);
        }
        var faces = new List<CrashSceneryFace>(nt + nq);
        int? Resolve(int field, bool slot)
        {
            int k;
            if (slot)
            {
                if (field >= nslot) return null;
                k = (int)(slots[field] & 0x3FF) - 1;
            }
            else
            {
                if (field == 0) return null;
                k = field - 1;
            }
            return k >= 0 && k < ntex ? k : null;
        }
        for (int i = 0; i < nt; i++)
        {
            uint a = BinaryPrimitives.ReadUInt32LittleEndian(items[2].AsSpan((nt - 1 - i) * 4));
            int b = BinaryPrimitives.ReadUInt16LittleEndian(items[2].AsSpan(nt * 4 + 2 * i));
            int field = (int)(a & 0x7F) << 4 | b & 15; bool slot = (a & 0x80) != 0;
            faces.Add(new([(int)(a >> 8 & 0xFFF), (int)(a >> 20 & 0xFFF), b >> 4 & 0xFFF], false, field, slot, Resolve(field, slot)));
        }
        for (int i = 0; i < nq; i++)
        {
            uint wa = BinaryPrimitives.ReadUInt32LittleEndian(items[3].AsSpan(8 * i)), wb = BinaryPrimitives.ReadUInt32LittleEndian(items[3].AsSpan(8 * i + 4));
            int field = (int)(wa & 0x7F) << 4 | (int)(wb & 15); bool slot = (wa & 0x80) != 0;
            faces.Add(new([(int)(wa >> 8 & 0xFFF), (int)(wa >> 20 & 0xFFF), (int)(wb >> 8 & 0xFFF), (int)(wb >> 20 & 0xFFF)], true, field, slot, Resolve(field, slot)));
        }
        foreach (var f in faces)
            if (f.Corners.Any(c => c >= nv)) throw new InvalidDataException($"{entry.Name}: a face names a vertex past the table.");
        return new CrashScenery(entry.Name)
        {
            Origin = new(ox, oy, oz), IsSky = sky == 1, Pages = pages, Colours = colours, Textures = textures, Slots = slots,
            Vertices = vertices, VertexColours = vertexColours, Faces = faces,
        };
    }

    /// <summary>A 12-byte GPU texture record (u/v per corner at 0/1, 4/5, 8/9, 10/11; palette at 2; segment, blend and
    /// colour mode at 6; page index at 7), as <see cref="CrashModel"/> reads it.</summary>
    public static CrashModel.Texture ReadTexture(ReadOnlySpan<byte> b) =>
        new([b[0], b[4], b[8], b[10]], [b[1], b[5], b[9], b[11]], (b[2] & 15) * 16, b[3] << 2 | b[2] >> 6 & 3,
            b[6] & 15, b[6] >> 5 & 3, (b[6] >> 7 | b[7] << 1 & 2) & 3, b[7] >> 2 & 63);
}

/// <summary>A texture's whole region on its page as RGBA (top row first, as on the page): colour 0 is transparent (alpha
/// 0), as the PlayStation draws it.</summary>
public sealed record CrashTextureImage(int Width, int Height, Rgba32[] Pixels)
{
    public static CrashTextureImage Read(byte[] page, CrashModel.Texture t)
    {
        int u0 = t.U.Min(), u1 = t.U.Max(), v0 = t.V.Min(), v1 = t.V.Max();
        int w = u1 - u0 + 1, h = v1 - v0 + 1;
        var pixels = new Rgba32[w * h];
        for (int v = 0; v < h; v++)
            for (int u = 0; u < w; u++)
            {
                ushort c = CrashModel.Texel(page, t, u0 + u, v0 + v);
                pixels[v * w + u] = c == 0 ? new(0, 0, 0, 0)
                    : new((byte)((c & 31) * 255 / 31), (byte)((c >> 5 & 31) * 255 / 31), (byte)((c >> 10 & 31) * 255 / 31), 255);
            }
        return new(w, h, pixels);
    }

    /// <summary>Share of transparent texels (0-1).</summary>
    public double Transparent => Pixels.Count(p => p.A == 0) / (double)Math.Max(1, Pixels.Length);

    /// <summary>Average colour of the opaque texels (black when there are none).</summary>
    public ColorRgba Average()
    {
        long r = 0, g = 0, b = 0, n = 0;
        foreach (var p in Pixels) if (p.A != 0) { r += p.R; g += p.G; b += p.B; n++; }
        return n == 0 ? new(0, 0, 0) : new((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }
}
