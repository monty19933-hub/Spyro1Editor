using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>One of a terrain texture's six pictures as Year of the Dragon stores it (8 bytes): its left and right texel
/// columns, top and bottom rows, palette (CLUT) and page words, and a flags byte (bits 4-5 turn the picture, bit 6
/// mirrors it, bit 7 draws it see-through).</summary>
public sealed record YotdTextureQuad(byte Left, byte Top, ushort Clut, byte Right, byte Bottom, byte Page, byte Flags)
{
    public int Depth => (Page & 0x80) != 0 ? 8 : 4;
    public bool SeeThrough => (Flags & 0x80) != 0;
    /// <summary>The four corners' texel positions on the page in the order Spyro Scope draws them (bottom-left,
    /// bottom-right, top-right, top-left before turning and mirroring).</summary>
    public (int U, int V)[] Corners()
    {
        int l = Left, r = Right + 1, t = Top, b = Bottom + 1;
        (int, int)[] basis = [(l, b), (r, b), (r, t), (l, t)];
        int turn = (Flags >> 4) & 3;
        var q = new (int U, int V)[4];
        for (int i = 0; i < 4; i++) q[i] = basis[(i - turn) & 3];
        if ((Flags & 0x40) != 0) (q[0], q[2]) = (q[2], q[0]);
        return q;
    }
}

/// <summary>A terrain texture: six pictures (far, whole, then the four quarters the game uses close up).</summary>
public sealed record YotdTerrainTexture(IReadOnlyList<YotdTextureQuad> Quads)
{
    public bool SeeThrough => Quads[1].SeeThrough;
}

/// <summary>A near (detailed) face of a sector: corners in editor units (16 world units; a triangle repeats its first
/// corner, as the game stores it: <paramref name="Triangle"/>), the near and far colour of each corner, its texture and its flags byte (bit 2: the
/// front is the clockwise side; bit 3: drawn from both sides; bits 4-5: which corner of the texture a triangle leaves out).</summary>
public sealed record YotdNearFace(int Sector, Vector3f[] Corners, ColorRgba[] Near, ColorRgba[] Far, int Texture, int Flags, bool Triangle)
{
    public bool FrontReversed => (Flags & 0x04) != 0;
    public bool TwoSided => (Flags & 0x08) != 0;
    public int Rotation => (Flags >> 4) & 3;

    /// <summary>The corners the face draws, in its perimeter order, without repeats: a triangle drops its first
    /// corner, a corner equal to the one before it is dropped (also across the wrap). Empty when the remaining corners
    /// are not all different or fewer than three (the face draws nothing).</summary>
    public int[] DrawnCorners()
    {
        List<int> keep = [];
        for (int k = Triangle ? 1 : 0; k < Corners.Length; k++)
            if (keep.Count == 0 || Corners[keep[^1]] != Corners[k]) keep.Add(k);
        if (keep.Count > 1 && Corners[keep[0]] == Corners[keep[^1]]) keep.RemoveAt(keep.Count - 1);
        return keep.Count >= 3 && keep.Select(k => Corners[k]).Distinct().Count() == keep.Count ? [.. keep] : [];
    }
}

/// <summary>A terrain sector (the game's "region"): its number in the level's sector list, its near faces and its own
/// distant mesh (null when it has none). A far-only sector is distant scenery with no near faces.</summary>
public sealed record YotdSector(int Number, IReadOnlyList<YotdNearFace> NearFaces, AuthoredFarMesh? Far)
{
    public bool FarOnly => NearFaces.Count == 0;
}

/// <summary>A collision triangle in editor units, its ground kind (an index into the level's ground-kind list, 0x3F none),
/// its landing sound (0-3) and whether the game treats it as a water surface (bit 14 of its height word).</summary>
public sealed record YotdCollisionTriangle(Vector3f A, Vector3f B, Vector3f C, int Kind, int Sound, bool Water);

/// <summary>A ground kind of the level: its type (0 sink, 1 hot, 2 supercharge, 3 trigger, 4 ice, 5 barrier, 6 doorway,
/// 7 electric, 8 ladder, 9 ramp, 10 slip ...) and its parameter words.</summary>
public sealed record YotdGroundKind(int Type, IReadOnlyList<int> Parameters)
{
    public string Label => Type switch
    {
        0 => Parameters.Count > 0 && Parameters[0] != 0 ? "deadly sink" : "sink",
        1 => "hot (burns)", 2 => "supercharge", 3 => "trigger", 4 => "ice", 5 => "barrier", 6 => "doorway", 7 => "electric",
        8 => "ladder", 9 => "ramp", 10 => "slip", _ => $"type {Type}",
    } + (Parameters.Count > 0 ? $" ({string.Join(",", Parameters)})" : "");
}

/// <summary>An object of the level's object list (class, position in editor units, heading byte).</summary>
public sealed record YotdObject(int Index, int Class, Vector3f Position, int Heading);

/// <summary>The main area of a Spyro: Year of the Dragon level as its WAD entry stores it (USA Rev 1):
/// <list type="bullet">
/// <item>sub-file 0: the level's half of texture memory (x 512-1023, 512 rows, 0x80000 bytes), then its sounds;</item>
/// <item>sub-file 1: length-prefixed blocks: the terrain textures (count, then six 8-byte pictures each), the sectors
/// (header length H, H-4 more header bytes, the sector count, one offset per sector from the block's second word),
/// the ground kinds (count, one offset per kind from the block's second word, then type and parameter words);</item>
/// <item>sub-file 1 or 2: the collision block (length, triangle count, count of triangles with a ground kind, a word,
/// then the offsets of the cell tree (0x24), cells, triangles and kind bytes from the block's second word);</item>
/// <item>sub-file 3: the object list (length, count, 0x58 bytes per object as Spyro the Dragon's).</item>
/// </list>
/// A sector is a 0x1C-byte header (centre, flags at +4 with 0x1000 = heights in steps of 2 world units instead of 16,
/// origin y/x/z at +8/+10/+14, far and near counts at +0x10 and +0x14), its far data (vertices, colours, 8-byte faces)
/// and its near data (vertices packed x&lt;&lt;21|y&lt;&lt;10|z/2, near then far colours, 16-byte faces: four corner
/// indices, four colour indices, a word, then texture and flags bytes). Read-only.</summary>
public sealed class YearOfTheDragonLevelData
{
    public const int Subfiles = 18, VramBytes = 0x80000, TextureRecordBytes = 48, FineHeights = 0x1000;
    public YearOfTheDragonLevel Level { get; }
    public byte[] Vram { get; }
    public IReadOnlyList<YotdTerrainTexture> Textures { get; }
    public IReadOnlyList<YotdSector> Sectors { get; }
    public IReadOnlyList<YotdGroundKind> GroundKinds { get; }
    public IReadOnlyList<YotdCollisionTriangle> Collision { get; }
    public IReadOnlyList<YotdObject> Objects { get; }
    /// <summary>The sky's background colour (what the game clears the screen to behind the sky), null when the level
    /// file has no sky block.</summary>
    public ColorRgba? SkyBackground { get; }

    private YearOfTheDragonLevelData(YearOfTheDragonLevel level, byte[] vram, IReadOnlyList<YotdTerrainTexture> textures, IReadOnlyList<YotdSector> sectors,
        IReadOnlyList<YotdGroundKind> kinds, IReadOnlyList<YotdCollisionTriangle> collision, IReadOnlyList<YotdObject> objects, ColorRgba? sky)
    { Level = level; Vram = vram; Textures = textures; Sectors = sectors; GroundKinds = kinds; Collision = collision; Objects = objects; SkyBackground = sky; }

    /// <summary>Reads the level from the user's disc (checked against the release).</summary>
    public static YearOfTheDragonLevelData Read(YearOfTheDragonDisc disc, YearOfTheDragonLevel level) => Parse(level, YearOfTheDragonLevels.ReadEntry(disc, level));

    /// <summary>Decodes a level's WAD entry (no release check: for tests and research).</summary>
    public static YearOfTheDragonLevelData Parse(YearOfTheDragonLevel level, byte[] entry)
    {
        byte[] Sub(int k)
        {
            if (entry.Length < 8 * Subfiles) throw new InvalidDataException($"{level.Name}'s level file is too short.");
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(8 * k)), size = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(8 * k + 4));
            if (size == 0) return [];
            if (offset < 8 * Subfiles || size < 0 || (long)offset + size > entry.Length) throw new InvalidDataException($"{level.Name}'s level file is not laid out as the game's.");
            return entry.AsSpan(offset, size).ToArray();
        }
        byte[] vramFile = Sub(0), terrain = Sub(1);
        if (vramFile.Length < VramBytes) throw new InvalidDataException($"{level.Name}'s texture memory is missing.");
        byte[] vram = vramFile.AsSpan(0, VramBytes).ToArray();

        // Block 0: textures.
        int textureBlock = Block(terrain, 0, level);
        int textureCount = I32(terrain, 4);
        if (textureCount is < 1 or > 128 || 8 + textureCount * TextureRecordBytes > textureBlock) throw new InvalidDataException($"{level.Name}'s texture list is malformed.");
        List<YotdTerrainTexture> textures = [];
        for (int t = 0; t < textureCount; t++)
        {
            var quads = new YotdTextureQuad[6];
            for (int k = 0; k < 6; k++)
            {
                int at = 8 + t * TextureRecordBytes + k * 8;
                quads[k] = new(terrain[at], terrain[at + 1], BinaryPrimitives.ReadUInt16LittleEndian(terrain.AsSpan(at + 2)), terrain[at + 4], terrain[at + 5], terrain[at + 6], terrain[at + 7]);
            }
            textures.Add(new(quads));
        }

        // Block 1: sectors.
        int sectorBlock = textureBlock, sectorBytes = Block(terrain, sectorBlock, level);
        int headerBytes = I32(terrain, sectorBlock + 4);
        if (headerBytes < 4 || headerBytes % 4 != 0 || headerBytes > 0x100) throw new InvalidDataException($"{level.Name}'s sector list is malformed.");
        int sectorCount = I32(terrain, sectorBlock + 4 + headerBytes);
        if (sectorCount is < 1 or > 1024 || headerBytes + 4 + 4 * sectorCount > sectorBytes) throw new InvalidDataException($"{level.Name}'s sector list is malformed.");
        var sectorAt = new int[sectorCount];
        for (int s = 0; s < sectorCount; s++)
        {
            sectorAt[s] = sectorBlock + 4 + I32(terrain, sectorBlock + 8 + headerBytes + 4 * s);
            if (sectorAt[s] < sectorBlock || sectorAt[s] + 0x1C > sectorBlock + sectorBytes) throw new InvalidDataException($"{level.Name}'s sector {s} lies outside its list.");
        }
        List<YotdSector> sectors = [];
        for (int s = 0; s < sectorCount; s++)
        {
            int end = s + 1 < sectorCount ? sectorAt[s + 1] : sectorBlock + sectorBytes;
            sectors.Add(DecodeSector(terrain, sectorAt[s], end, s, textureCount, level));
        }

        // Block 2: ground kinds.
        int kindBlock = sectorBlock + sectorBytes, kindBytes = Block(terrain, kindBlock, level);
        int kindCount = I32(terrain, kindBlock + 4);
        if (kindCount is < 0 or > 63 || 8 + 4 * kindCount > kindBytes) throw new InvalidDataException($"{level.Name}'s ground kinds are malformed.");
        List<YotdGroundKind> kinds = [];
        for (int k = 0; k < kindCount; k++)
        {
            int start = kindBlock + 4 + I32(terrain, kindBlock + 8 + 4 * k);
            int stop = k + 1 < kindCount ? kindBlock + 4 + I32(terrain, kindBlock + 12 + 4 * k) : kindBlock + kindBytes;
            if (start < kindBlock + 8 || stop > kindBlock + kindBytes || stop < start + 4 || (stop - start) % 4 != 0) throw new InvalidDataException($"{level.Name}'s ground kind {k} is malformed.");
            kinds.Add(new(I32(terrain, start), [.. Enumerable.Range(1, (stop - start) / 4 - 1).Select(w => I32(terrain, start + 4 * w))]));
        }

        // Collision: sub-file 1 or 2.
        IReadOnlyList<YotdCollisionTriangle>? collision = null;
        foreach (int k in new[] { 1, 2 })
            if (FindCollision(Sub(k)) is { } c) { collision = c; break; }
        if (collision == null) throw new InvalidDataException($"{level.Name}'s collision is not where the game keeps it.");

        return new(level, vram, textures, sectors, kinds, collision, ReadObjects(Sub(3)), FindSky(terrain, kindBlock + kindBytes));
    }

    private static int I32(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static int U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));

    /// <summary>A length-prefixed block's length (checked to lie inside the file).</summary>
    private static int Block(byte[] b, int at, YearOfTheDragonLevel level)
    {
        if (at < 0 || at + 8 > b.Length) throw new InvalidDataException($"{level.Name}'s terrain file ends early.");
        int length = I32(b, at);
        if (length < 8 || at + length > b.Length) throw new InvalidDataException($"{level.Name}'s terrain file is not laid out as the game's.");
        return length;
    }

    private static YotdSector DecodeSector(byte[] b, int a, int end, int number, int textureCount, YearOfTheDragonLevel level)
    {
        int flags = U16(b, a + 4), originY = U16(b, a + 8), originX = U16(b, a + 10), originZ = U16(b, a + 14);
        int fv = b[a + 0x10], fc = b[a + 0x11], ff = b[a + 0x12], nv = b[a + 0x14], nc = b[a + 0x15], nf = b[a + 0x16], ns = b[a + 0x17];
        int farWords = fv + fc + 2 * ff;
        if (nf > 0 && ns != farWords) throw new InvalidDataException($"{level.Name}'s sector {number} is not laid out as the game's.");
        if (a + 0x1C + 4 * (farWords + nv + 2 * nc + 4 * nf) > end) throw new InvalidDataException($"{level.Name}'s sector {number} overruns its list.");
        int heightStep = (flags & FineHeights) != 0 ? 2 : 16;
        var far = FarMesh(b, a, number, originX, originY, originZ, heightStep, fv, fc, ff, level);
        List<YotdNearFace> faces = [];
        if (nf > 0)
        {
            int d = a + 0x1C + 4 * ns;
            var v = new Vector3f[nv];
            for (int i = 0; i < nv; i++, d += 4)
            {
                uint p = U32(b, d);
                v[i] = new(originX + (p >> 21), originY + (p >> 10 & 0x7FF), (float)Math.Round(originZ + ((p & 0x3FF) << 1) * heightStep / 16.0));
            }
            var colours = new ColorRgba[2 * nc];
            for (int i = 0; i < 2 * nc; i++, d += 4) colours[i] = new(b[d], b[d + 1], b[d + 2]);
            for (int i = 0; i < nf; i++, d += 16)
            {
                int[] vi = [b[d], b[d + 1], b[d + 2], b[d + 3]], ci = [b[d + 4], b[d + 5], b[d + 6], b[d + 7]];
                int texture = b[d + 12] & 0x7F, faceFlags = b[d + 13];
                bool triangle = vi[0] == vi[1];
                if (vi.Any(k => k >= nv) || texture >= textureCount) throw new InvalidDataException($"{level.Name}'s sector {number} has a face outside its tables.");
                // The game repeats colour slot 1 where a triangle's first corner or an unused slot stands (s3level.near_faces).
                int[] fix = [.. ci.Select((k, j) => k < nc && !(triangle && j == 0) ? k : ci[1])];
                if (fix.Any(k => k >= nc)) throw new InvalidDataException($"{level.Name}'s sector {number} has a face colour outside its table.");
                faces.Add(new(number, [.. vi.Select(k => v[k])], [.. fix.Select(k => colours[k])], [.. fix.Select(k => colours[nc + k])], texture, faceFlags, triangle));
            }
        }
        return new(number, faces, far);
    }

    /// <summary>A sector's far mesh in editor units in the scene's corner convention (see <see cref="YearOfTheDragonTerrain"/>).</summary>
    private static AuthoredFarMesh? FarMesh(byte[] b, int a, int sector, int originX, int originY, int originZ, int heightStep, int vertices, int colours, int faces, YearOfTheDragonLevel level)
    {
        if (faces == 0 || vertices == 0 || colours == 0) return null;
        int at = a + 0x1C;
        var v = new Vector3f[vertices];
        for (int i = 0; i < vertices; i++, at += 4)
        {
            uint p = U32(b, at);
            v[i] = new(originX + (p >> 21), originY + (p >> 10 & 0x7FF), MathF.Round(originZ + ((p & 0x3FF) << 1) * heightStep / 16f));
        }
        var c = new ColorRgba[colours];
        for (int i = 0; i < colours; i++, at += 4) c[i] = new(b[at], b[at + 1], b[at + 2]);
        List<AuthoredFarFace> list = [];
        for (int i = 0; i < faces; i++, at += 8)
        {
            uint pv = U32(b, at), pc = U32(b, at + 4);
            int[] vi = [(int)(pv >> 10 & 0x7F), (int)(pv >> 17 & 0x7F), (int)(pv >> 24 & 0x7F), (int)(pv >> 3 & 0x7F)];
            int[] ci = [(int)(pc >> 11 & 0x7F), (int)(pc >> 18 & 0x7F), (int)(pc >> 25 & 0x7F), (int)(pc >> 4 & 0x7F)];
            int[] order = vi[0] == vi[3] ? [0, 1, 2] : [0, 3, 1, 2];
            int[] corners = [.. order.Select(k => vi[k])], cols = [.. order.Select(k => ci[k])];
            if (corners.Any(k => k >= vertices) || cols.Any(k => k >= colours))
                throw new InvalidDataException($"A far face of {level.Name}'s sector {sector} points outside its tables.");
            if (corners.Distinct().Count() == corners.Length) list.Add(new(corners, cols));
        }
        return list.Count > 0 ? new(sector, v, c, list) : null;
    }

    /// <summary>The collision block: [length][triangles][kind bytes][word][0x24][cells][triangle offset][kind offset],
    /// offsets from the second word; triangles are 12 bytes (x, y, z words: 14-bit origin, two deltas), followed by one
    /// kind byte per triangle with a ground kind. Null when the file has none.</summary>
    internal static IReadOnlyList<YotdCollisionTriangle>? FindCollision(byte[] b)
    {
        for (int o = 0; o + 32 <= b.Length; o += 4)
        {
            int length = I32(b, o), count = I32(b, o + 4), special = I32(b, o + 8), tree = I32(b, o + 16), cells = I32(b, o + 20), triangles = I32(b, o + 24), kinds = I32(b, o + 28);
            if (count is < 1 or > 0x8000 || special < 0 || special > count || tree != 0x24 || cells <= tree || triangles <= cells ||
                (long)triangles + 12L * count != kinds || length < 4 + kinds + special || (long)o + length > b.Length) continue;
            int start = o + 4 + triangles, flags = o + 4 + kinds;
            List<YotdCollisionTriangle> list = new(count);
            for (int i = 0; i < count; i++)
            {
                int x = I32(b, start + 12 * i), y = I32(b, start + 12 * i + 4), z = I32(b, start + 12 * i + 8);
                int x0 = x & 0x3FFF, y0 = y & 0x3FFF, z0 = z & 0x3FFF;
                Vector3f a = new(x0, y0, z0);
                Vector3f p = new(x0 + ((x << 9) >> 23), y0 + ((y << 9) >> 23), z0 + (int)((uint)(z << 8) >> 24));
                Vector3f q = new(x0 + (x >> 23), y0 + (y >> 23), z0 + (int)((uint)z >> 24));
                int flag = i < special ? b[flags + i] : 0x3F;
                list.Add(new(a, p, q, flag & 0x3F, flag >> 6, (z & 0x4000) != 0));
            }
            return list;
        }
        return null;
    }

    /// <summary>The sky block after the ground kinds in sub-file 1: [length][background RGB0][part count n][n offsets from
    /// the second word, the first 8 + 4n][parts: a 0x14-byte header (vertex and colour counts at +12/+13, strip stream and
    /// head byte counts at +16/+18), vertices, colours, heads, stream] (see the Lost Fleet sky converter, s3sky.py).</summary>
    private static ColorRgba? FindSky(byte[] b, int from)
    {
        for (int o = Math.Max(0, from); o + 16 <= b.Length; o += 4)
        {
            int size = I32(b, o), n = I32(b, o + 8), first = I32(b, o + 12);
            uint background = U32(b, o + 4);
            if (n is < 1 or > 255 || first != 8 + 4 * n || size is < 32 or > 0x20000 || size % 4 != 0 || (long)o + size > b.Length || background >> 24 != 0) continue;
            bool ok = true;
            for (int i = 0; i < n && ok; i++)
            {
                int offset = I32(b, o + 12 + 4 * i), next = i + 1 < n ? I32(b, o + 16 + 4 * i) : size - 4;
                int a = o + 4 + offset;
                if (offset < first || next <= offset || offset + 0x14 > size - 4) { ok = false; break; }
                int vertices = b[a + 12], colours = b[a + 13], stream = U16(b, a + 16), heads = U16(b, a + 18);
                ok = vertices > 0 && colours > 0 && heads % 4 == 0 && offset + 0x14 + 4 * (vertices + colours) + heads + stream <= next;
            }
            if (ok) return new((byte)background, (byte)(background >> 8), (byte)(background >> 16));
        }
        return null;
    }

    /// <summary>The object list (first block of 0x58-byte objects whose positions all lie in the level's range).</summary>
    private static IReadOnlyList<YotdObject> ReadObjects(byte[] b)
    {
        for (int o = 0; o + 16 <= b.Length; o += 4)
        {
            int length = I32(b, o), count = I32(b, o + 4);
            if (count is < 5 or > 1500 || length < 8 + count * 0x58 || (long)o + length > b.Length) continue;
            bool ok = true;
            for (int i = 0; i < count && ok; i++)
            {
                int r = o + 8 + i * 0x58, x = I32(b, r + 0xC), y = I32(b, r + 0x10), z = I32(b, r + 0x14);
                ok = x is > 0 and < 0x40000 && y is > 0 and < 0x40000 && z is > -0x10000 and < 0x40000;
            }
            if (!ok) continue;
            return [.. Enumerable.Range(0, count).Select(i =>
            {
                int r = o + 8 + i * 0x58;
                return new YotdObject(i, U16(b, r + 0x36), new(I32(b, r + 0xC) / 16f, I32(b, r + 0x10) / 16f, I32(b, r + 0x14) / 16f), b[r + 0x46]);
            })];
        }
        return [];
    }

    private ushort Pixel(int x, int y) => x is >= 512 and < 1024 && y is >= 0 and < 512 ? BinaryPrimitives.ReadUInt16LittleEndian(Vram.AsSpan(2 * (y * 512 + x - 512))) : (ushort)0;
    private ushort Texel(YotdTextureQuad quad, int u, int v)
    {
        int px = (quad.Page & 15) * 64, py = ((quad.Page >> 4) & 1) * 256, cx = (quad.Clut & 0x3F) << 4, cy = quad.Clut >> 6;
        int index = quad.Depth == 4 ? (Pixel(px + u / 4, py + v) >> ((u & 3) * 4)) & 15 : (Pixel(px + u / 2, py + v) >> ((u & 1) * 8)) & 255;
        return Pixel(cx + index, cy);
    }
    private static Rgba32 Rgb(ushort c) => new((byte)((c & 31) * 255 / 31), (byte)(((c >> 5) & 31) * 255 / 31), (byte)(((c >> 10) & 31) * 255 / 31), 255);

    /// <summary>One texture picture (side x side, top row first, as in texture memory), sampled between its corners as
    /// the game maps it (s3level.Vram.image).</summary>
    public Rgba32[] Picture(YotdTextureQuad quad, int side)
    {
        var uv = quad.Corners();
        var (tl, tr, bl, br) = (uv[3], uv[2], uv[0], uv[1]);
        var result = new Rgba32[side * side];
        for (int j = 0; j < side; j++)
            for (int i = 0; i < side; i++)
            {
                double fu = (i + 0.5) / side, fv = (j + 0.5) / side;
                double x = (tl.U * (1 - fu) + tr.U * fu) * (1 - fv) + (bl.U * (1 - fu) + br.U * fu) * fv;
                double y = (tl.V * (1 - fu) + tr.V * fu) * (1 - fv) + (bl.V * (1 - fu) + br.V * fu) * fv;
                result[j * side + i] = Rgb(Texel(quad, (int)x, (int)y));
            }
        return result;
    }

    /// <summary>A texture's full-detail picture (64x64): its four close-up quarters (pictures 2-5) as the top-left,
    /// top-right, bottom-left and bottom-right quarters, in texture-memory orientation, as the Lost Fleet build used
    /// (export_scene.picture). Opaque: Spyro the Dragon's terrain has no see-through texels.</summary>
    public Rgba32[] Picture(int texture)
    {
        const int Side = 64, Half = 32;
        var quads = Textures[texture].Quads;
        var result = new Rgba32[Side * Side];
        (int X, int Y)[] spots = [(0, 0), (Half, 0), (0, Half), (Half, Half)];
        for (int k = 0; k < 4; k++)
        {
            var part = Picture(quads[2 + k], Half);
            for (int j = 0; j < Half; j++)
                part.AsSpan(j * Half, Half).CopyTo(result.AsSpan((spots[k].Y + j) * Side + spots[k].X, Half));
        }
        return result;
    }
}
