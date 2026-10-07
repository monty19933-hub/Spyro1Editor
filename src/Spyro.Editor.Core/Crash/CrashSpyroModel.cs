using System.Buffers.Binary;

namespace Spyro.Editor.Core.Crash;

/// <summary>Converts a Crash 3 model and some of its animations into a Spyro 1 animated object model package
/// (the format of the game's own enemies) plus a 512x512 texture image holding the art it uses.
/// Package: animation count, 16 sound slots, collision block pointer (0x14, high bit = package-relative), model
/// data offset (0x34), one animation header per animation (0x38 + 4i). Animation header: frame count, colour count,
/// vertex delta shift - 1 (+6), vertex counts (+8, +9), default speed (+0xC, 64 = one frame per tick, 32 = every
/// other tick with interpolation), near/far face tables and palettes (+0x14..+0x20, relative to the model data) and
/// eight bytes per frame: vertex data offset (low 21 bits) with a sound slot in the top byte (0xFF none), and the
/// absolute vertex count (top byte) with the frame's bounding radius / 8 (third byte). Vertices are 32-bit absolute
/// words (x 11 bits, y 11 bits, z/2 12 bits; bit 0 says the next vertex is a 16-bit delta of 5-bit steps).
/// Faces: a triangle is word (vertices << 22/13/4, bit 1 textured), word (colours << 19/10/1) and, when textured,
/// a 12-byte GPU texture field (u, v of corners a, b, c with the palette and page words).</summary>
public static class CrashSpyroModel
{
    /// <param name="Sounds">(frame, package sound slot) pairs the game plays when the frame comes up.</param>
    public sealed record Animation(string Name, bool Interpolated, IReadOnlyList<(int Frame, int Slot)>? Sounds = null);
    /// <param name="UnitsPerSpyroUnit">Crash model-space units per Spyro unit.</param>
    /// <param name="Yaw">Quarter turns about the vertical axis (Crash models face -y).</param>
    /// <param name="Mirror">Mirror the model left to right (faces keep their outward winding).</param>
    public sealed record Options(double UnitsPerSpyroUnit, int Yaw = 1, bool Mirror = false);
    public sealed record Result(byte[] Package, byte[] Vram, int VertexCount, int FaceCount, int Height, int Radius,
        IReadOnlyList<int> FrameCounts, int ScaleShift);

    private const int ClutRow = 256;   // palettes from VRAM row 256 down; art in the top half (rows 0..255)

    /// <param name="art">The texture image to add this model's art to (shared by models from one level file, so
    /// art they have in common is stored and imported once); a new one when null.</param>
    public static Result Build(CrashNsf nsf, string modelName, IReadOnlyList<Animation> animations, Options options,
        IReadOnlyList<byte>? sounds = null, Art? art = null)
    {
        var model = new CrashModel(nsf.Entry(modelName).Items);
        var frames = animations.Select(a => nsf.Entry(a.Name).Items).ToArray();
        if (frames.Any(f => f.Count == 0)) throw new InvalidDataException("A Crash animation has no frames.");
        int special = CrashModel.FrameSpecialVertexCount(frames[0][0]);

        // Ground: the model's origin, or the lowest point of the first frame when that is below it.
        var first = model.Positions(frames[0][0]);
        double ground = Math.Min(0, first.Min(p => p.Z)), k = 1 / options.UnitsPerSpyroUnit;
        // Spyro model space: x, y and z down (a half turn about x keeps the axes right-handed); mirrored, y keeps its sign.
        (double X, double Y, double Z) ToSpyro((double X, double Y, double Z) p)
        {
            double x = p.X * k, y = p.Y * k, z = (p.Z - ground) * k;
            for (int q = 0; q < (options.Yaw & 3); q++) (x, y) = (-y, x);
            return (x, options.Mirror ? y : -y, -z);
        }
        var world = frames.Select(fr => fr.Select(f => model.Positions(f).Select(ToSpyro).ToArray()).ToArray()).ToArray();
        int vertexCount = world[0][0].Length;
        if (vertexCount > 255) throw new InvalidDataException($"{modelName} has {vertexCount} vertices; Spyro objects allow 255.");
        if (world.SelectMany(a => a).Any(f => f.Length != vertexCount)) throw new InvalidDataException($"{modelName}'s frames disagree on their vertex count.");
        // Header +5 draws the model 2^n times larger than its stored coordinates (the game's big enemies use 1 and 2).
        // Every stored coordinate stays within +-1000 like the game's own models: the renderer files faces into
        // depth buckets in stored units with no upper bound check, so a model reaching further overruns the
        // ordering table and crashes the game.
        bool Fits(int n) => world.SelectMany(a => a).SelectMany(f => f).All(v =>
            Math.Abs(v.X) / (1 << n) <= 1000 && Math.Abs(v.Y) / (1 << n) <= 1000 && Math.Abs(v.Z) / (1 << n) <= 1000);
        int scaleShift = Enumerable.Range(0, 3).FirstOrDefault(Fits, -1);
        if (scaleShift < 0) throw new InvalidDataException($"{modelName} is too large for a Spyro object at this size.");
        var spyroFrames = world.Select(a => a.Select(f => f.Select(v => (X: (int)Math.Round(v.X / (1 << scaleShift)),
            Y: (int)Math.Round(v.Y / (1 << scaleShift)), Z: (int)Math.Round(v.Z / (1 << scaleShift)))).ToArray()).ToArray()).ToArray();

        // Faces.
        List<(byte R, byte G, byte B)> near = [], far = [];
        int Near((byte, byte, byte) c) { int i = near.IndexOf(c); if (i < 0) { i = near.Count; near.Add(c); } return i; }
        int Far((byte, byte, byte) c) { int i = far.IndexOf(c); if (i < 0) { i = far.Count; far.Add(c); } return i; }
        art ??= new Art(nsf);
        if (!ReferenceEquals(art.Nsf, nsf)) throw new ArgumentException("A shared Crash texture image belongs to one level file.");
        using var nearFaces = new MemoryStream();
        using var farFaces = new MemoryStream();
        void Word(Stream s, uint w) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, w); s.Write(b); }
        int faces = 0;
        foreach (var t in model.Triangles)
        {
            var tex = t.Texture > 0 ? model.Textures[t.Texture - 1] : null;
            // Crash's subtractive drop shadows (blend 2) and additive motion blurs (blend 1, the knight's sword
            // sweep) have no Spyro equivalent: Spyro objects draw opaque.
            if (tex != null && tex.Blend is 1 or 2) continue;
            bool nocull = t.Subtype is 0 or 2, flip = (t.Type == 2) != (t.Subtype == 3) && !nocull;
            // CrashEdit's corner order (front = counter-clockwise); mirrored, b and c swap so the front stays outside.
            int[] order = options.Mirror ? [0, 2, 1] : [0, 1, 2];
            int[] slot = order.Select(i => flip ? 2 - i : i).ToArray();
            int[] v = slot.Select(i => t.Vertices[i] + special).ToArray();
            if (v.Any(i => i < 0 || i >= vertexCount)) continue;
            var colours = slot.Select(i => t.Colours[i] < model.Colours.Count ? model.Colours[t.Colours[i]] : ((byte)128, (byte)128, (byte)128)).ToArray();
            uint vertices = (uint)(v[0] << 22 | v[1] << 13 | v[2] << 4);
            int[] ni = colours.Select(Near).ToArray();
            byte[]? field = null; (byte, byte, byte) average = (128, 128, 128);
            if (tex != null)
            {
                bool reverse = (t.Type != 2 && !flip) || (t.Type == 2 && t.Subtype == 1);
                int[] corner = order.Select(i => reverse ? 2 - i : i).ToArray();
                (field, average) = art.Field(model.Pages[tex.Page], tex, corner);
            }
            // Bit 0: drawn from both sides (Crash's no-cull triangles).
            uint sided = nocull ? 1u : 0u;
            Word(nearFaces, vertices | sided | (field != null ? 2u : 0u));
            Word(nearFaces, (uint)(ni[0] << 19 | ni[1] << 10 | ni[2] << 1));
            if (field != null) nearFaces.Write(field);
            int[] fi = colours.Select(c => Far(field == null ? c : Modulate(average, c))).ToArray();
            Word(farFaces, vertices | sided);
            Word(farFaces, (uint)(fi[0] << 19 | fi[1] << 10 | fi[2] << 1));
            faces++;
        }
        if (faces == 0) throw new InvalidDataException($"{modelName} has no drawable faces.");
        int colourCount = Math.Max(near.Count, far.Count);
        if (colourCount > 511) throw new InvalidDataException($"{modelName} uses more colours than a Spyro model allows.");

        // Vertex data: the delta step (2, 4 or 8 units) that stores the frames in the fewest bytes.
        var body = new MemoryStream();
        int offNear = (int)body.Length; Word(body, (uint)nearFaces.Length); nearFaces.WriteTo(body);
        int offFar = (int)body.Length; Word(body, (uint)farFaces.Length); farFaces.WriteTo(body);
        int offNearPalette = (int)body.Length;
        for (int i = 0; i < colourCount; i++) { var c = i < near.Count ? near[i] : near[^1]; body.Write([c.R, c.G, c.B, 0xFF]); }
        int offFarPalette = (int)body.Length;
        for (int i = 0; i < colourCount; i++) { var c = i < far.Count ? far[i] : far[^1]; body.Write([c.R, c.G, c.B, 0]); }
        int shift = Enumerable.Range(1, 3).MinBy(s => spyroFrames.SelectMany(a => a).Sum(f => Encode(f, s).Bytes.Length));
        var frameEntries = new List<List<(int Offset, int Absolute, int Radius)>>();
        foreach (var anim in spyroFrames)
        {
            List<(int, int, int)> list = [];
            foreach (var f in anim)
            {
                var (bytes, absolute) = Encode(f, shift);
                int offset = (int)body.Length;
                body.Write(bytes);
                int radius = (int)Math.Ceiling(f.Max(p => Math.Sqrt((double)p.X * p.X + (double)p.Y * p.Y + (double)p.Z * p.Z)) / 8);
                list.Add((offset, absolute, Math.Clamp(radius, 1, 255)));
            }
            frameEntries.Add(list);
        }

        // Collision block (world units, up positive): one vertical capsule around the body, the shape Spyro's flame,
        // charge and contact tests read: count 1, type 2, radius, the capsule's low and high centres, then the radius
        // again and a zero damage word (Dark Hollow's basket uses the same block). Radius: the body's typical
        // horizontal reach (outstretched swords and legs left out).
        var rest = world[0][0];
        int height = (int)Math.Round(-rest.Min(v => v.Z));
        var spread = rest.Select(v => Math.Sqrt(v.X * v.X + v.Y * v.Y)).Order().ToArray();
        int radius0 = Math.Clamp((int)Math.Round(spread[spread.Length * 3 / 5]), 160, 600);
        int zLow = Math.Min(radius0, height / 2), zHigh = Math.Max(zLow, height - radius0);
        byte[] trailer = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(trailer, (ushort)Math.Min(0x7FFF, zHigh + radius0));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(4), (uint)(radius0 << 16 | 0x0201));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(8), (uint)(zHigh << 16 | zLow));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(12), (uint)(zHigh << 16 | zLow));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(16), (uint)radius0);
        // Header +7: the view-culling radius in 64-unit steps (Dark Hollow's basket: 9 for a 595-unit model).
        double extent = world.SelectMany(a => a).SelectMany(f => f).Max(v => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z));
        int cullRadius = Math.Clamp((int)Math.Ceiling(extent / 64) + 1, 1, 255);

        // Package.
        int headerBytes = 0x38 + 4 * animations.Count;
        int[] animOffsets = new int[animations.Count];
        int cursor = headerBytes;
        for (int a = 0; a < animations.Count; a++) { animOffsets[a] = cursor; cursor += 0x24 + 8 * frameEntries[a].Count; }
        int md = Align(cursor, 8), trailerAt = Align(md + (int)body.Length, 4);
        byte[] p = new byte[trailerAt + trailer.Length];
        BinaryPrimitives.WriteInt32LittleEndian(p, animations.Count);
        p.AsSpan(4, 16).Fill(0xFF);
        if (sounds != null) for (int i = 0; i < Math.Min(16, sounds.Count); i++) p[4 + i] = sounds[i];
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0x14), 0x80000000u | (uint)trailerAt);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x34), md);
        for (int a = 0; a < animations.Count; a++)
        {
            int h = animOffsets[a];
            BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x38 + 4 * a), h);
            BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(h), (ushort)frameEntries[a].Count);
            BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(h + 2), (ushort)colourCount);
            p[h + 5] = (byte)scaleShift; p[h + 6] = (byte)(shift - 1); p[h + 7] = (byte)cullRadius;
            p[h + 8] = (byte)vertexCount; p[h + 9] = (byte)vertexCount;
            p[h + 0xC] = (byte)(animations[a].Interpolated ? 32 : 64);
            BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(h + 0x14), offNear);
            BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(h + 0x18), offNearPalette);
            BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(h + 0x1C), offFar);
            BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(h + 0x20), offFarPalette);
            for (int f = 0; f < frameEntries[a].Count; f++)
            {
                var (offset, absolute, radius) = frameEntries[a][f];
                int sound = animations[a].Sounds?.FirstOrDefault(x => x.Frame == f) is { Slot: >= 0 } hit && animations[a].Sounds!.Any(x => x.Frame == f) ? hit.Slot : 0xFF;
                BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(h + 0x24 + 8 * f), (uint)(sound << 24 | offset));
                BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(h + 0x28 + 8 * f), (uint)(absolute << 24 | radius << 16));
            }
        }
        body.ToArray().CopyTo(p, md);
        trailer.CopyTo(p, trailerAt);
        return new(p, art.Vram, vertexCount, faces, height, radius0, frameEntries.Select(f => f.Count).ToArray(), scaleShift);
    }

    private static (byte, byte, byte) Modulate((byte R, byte G, byte B) texture, (byte R, byte G, byte B) colour) =>
        ((byte)Math.Min(255, texture.R * colour.R / 128), (byte)Math.Min(255, texture.G * colour.G / 128), (byte)Math.Min(255, texture.B * colour.B / 128));

    private static int Align(int v, int a) => (v + a - 1) / a * a;

    /// <summary>One frame: absolute words first, then 16-bit deltas, in the order the decoder reads them.</summary>
    internal static (byte[] Bytes, int Absolute) Encode(IReadOnlyList<(int X, int Y, int Z)> vertices, int shift)
    {
        List<uint> absolute = []; List<ushort> deltas = [];
        int step = 1 << shift;
        (int X, int Y, int Z) previous = default;
        bool nextIsDelta = false;
        for (int i = 0; i < vertices.Count; i++)
        {
            var target = vertices[i];
            // Decide whether the following vertex can be a delta from this one's decoded position.
            (int X, int Y, int Z) decoded;
            int dx = 0, dy = 0, dz = 0;
            if (nextIsDelta)
            {
                dx = Math.Clamp((int)Math.Round((target.X - previous.X) / (double)step), -16, 15);
                dy = Math.Clamp((int)Math.Round((previous.Y - target.Y) / (double)step), -16, 15);
                dz = Math.Clamp((int)Math.Round((previous.Z - target.Z) / (double)step), -16, 15);
                decoded = (previous.X + (dx << shift), previous.Y - (dy << shift), previous.Z - (dz << shift));
            }
            else decoded = Absolute(target, false).Decoded;
            bool following = false;
            if (i + 1 < vertices.Count)
            {
                var n = vertices[i + 1];
                int ex = (int)Math.Round((n.X - decoded.X) / (double)step), ey = (int)Math.Round((decoded.Y - n.Y) / (double)step), ez = (int)Math.Round((decoded.Z - n.Z) / (double)step);
                following = ex is >= -16 and <= 15 && ey is >= -16 and <= 15 && ez is >= -16 and <= 15;
            }
            if (nextIsDelta)
                deltas.Add((ushort)((dx & 31) << 11 | (dy & 31) << 6 | (dz & 31) << 1 | (following ? 1 : 0)));
            else
            {
                var (word, d) = Absolute(target, following);
                absolute.Add(word); decoded = d;
            }
            previous = decoded; nextIsDelta = following;
        }
        byte[] bytes = new byte[Align(absolute.Count * 4 + deltas.Count * 2, 4)];
        for (int i = 0; i < absolute.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * i), absolute[i]);
        for (int i = 0; i < deltas.Count; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(absolute.Count * 4 + 2 * i), deltas[i]);
        return (bytes, absolute.Count);
    }

    /// <summary>An absolute vertex word: x (bits 21-31), y (11-21), z/2 (0-11); x's low bit is y's sign, y's low bit
    /// is z's sign and z/2's low bit is the next-is-delta flag, so the stored point can move by a unit or two.</summary>
    internal static (uint Word, (int X, int Y, int Z) Decoded) Absolute((int X, int Y, int Z) v, bool flag)
    {
        int half = Math.Clamp(v.Z >> 1, -2048, 2047);
        half = (half & ~1) | (flag ? 1 : 0);
        int y = Math.Clamp(v.Y, -1024, 1023);
        y = (y & ~1) | (half < 0 ? 1 : 0);
        int x = Math.Clamp(v.X, -1024, 1023);
        x = (x & ~1) | (y < 0 ? 1 : 0);
        uint w = (uint)((x & 0x7FF) << 21 | (y & 0x7FF) << 11 | (half & 0xFFF));
        int dx = (int)w >> 21, dy = (int)(w << 10) >> 21, dz = (int)(w << 20) >> 19;
        return (w, (dx, dy, dz));
    }

    /// <summary>The Crash texture pages a model uses, laid into a Spyro-style VRAM image: each 64-halfword column
    /// ("segment") of a Crash page a texture reads (two for 8-bit textures) goes into a free 64-wide, 128-high slot of
    /// the top half, and each palette into a row from 256 down.</summary>
    public sealed class Art(CrashNsf nsf)
    {
        public CrashNsf Nsf { get; } = nsf;
        public byte[] Vram { get; } = new byte[0x80000];
        private readonly Dictionary<(string Page, int Segment, int Columns), (int X, int Y)> _segments = [];
        private readonly Dictionary<(string Page, int X, int Y, int Count), (int X, int Y)> _cluts = [];
        private readonly bool[,] _used = new bool[2, 8];
        private int _clutX, _clutY = ClutRow;

        internal (byte[] Field, (byte, byte, byte) Average) Field(string pageName, CrashModel.Texture t, int[] corners)
        {
            byte[] page = nsf.Page(pageName);
            if (t.Mode > 1) throw new InvalidDataException("Direct-colour Crash textures are not supported.");
            int columns = t.Mode == 0 ? 1 : 2;
            if (t.Segment + columns > 4) throw new InvalidDataException("A Crash texture runs off its page.");
            if (!_segments.TryGetValue((pageName, t.Segment, columns), out var at))
            {
                var free = Enumerable.Range(0, 2).SelectMany(half => Enumerable.Range(0, 9 - columns).Select(column => (half, column)))
                    .Where(c => Enumerable.Range(c.column, columns).All(x => !_used[c.half, x])).Cast<(int Half, int Column)?>().FirstOrDefault()
                    ?? throw new InvalidDataException("This Crash model uses too many texture areas.");
                for (int x = 0; x < columns; x++) _used[free.Half, free.Column + x] = true;
                at = (free.Column * 64, free.Half * 128);
                for (int row = 0; row < 128; row++)
                    page.AsSpan(row * 512 + t.Segment * 128, 128 * columns).CopyTo(Vram.AsSpan((at.Y + row) * 1024 + at.X * 2));
                _segments[(pageName, t.Segment, columns)] = at;
            }
            int count = t.Mode == 0 ? 16 : 256;
            if (!_cluts.TryGetValue((pageName, t.ClutX, t.ClutY, count), out var clut))
            {
                if (_clutX + count > 512) { _clutX = 0; _clutY++; }
                clut = (_clutX, _clutY);
                page.AsSpan(t.ClutY * 512 + t.ClutX * 2, count * 2).CopyTo(Vram.AsSpan(clut.Y * 1024 + clut.X * 2));
                _clutX += count;
                _cluts[(pageName, t.ClutX, t.ClutY, count)] = clut;
            }
            byte[] f = new byte[12];
            int[] uvOffsets = [0, 4, 8];
            for (int k = 0; k < 3; k++) { f[uvOffsets[k]] = t.U[corners[k]]; f[uvOffsets[k] + 1] = (byte)(t.V[corners[k]] + at.Y); }
            f[10] = f[8]; f[11] = f[9];
            BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(2), (ushort)(clut.Y << 6 | (512 + clut.X) / 16));
            BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(6), (ushort)(8 + at.X / 64 | t.Mode << 7));
            // Average colour of the texture area (for the far, untextured faces).
            long r = 0, g = 0, b = 0, n2 = 0;
            for (int v = t.V.Take(3).Min(); v <= t.V.Take(3).Max(); v++)
                for (int u = t.U.Take(3).Min(); u <= t.U.Take(3).Max(); u++)
                {
                    ushort c = CrashModel.Texel(page, t, u, v);
                    if ((c & 0x7FFF) == 0) continue;
                    r += (c & 31) << 3; g += (c >> 5 & 31) << 3; b += (c >> 10 & 31) << 3; n2++;
                }
            return (f, n2 == 0 ? ((byte)128, (byte)128, (byte)128) : ((byte)(r / n2), (byte)(g / n2), (byte)(b / n2)));
        }
    }
}
