using System.Buffers.Binary;
using Spyro.Editor.Core.Crash;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>Converts a Spyro: Year of the Dragon animated object model into a Spyro the Dragon object package
/// (<see cref="CrashSpyroModel.Package"/>, the format of the game's own enemies) plus the texture image it uses.
/// Both games keep object models in the same axes (x forward, z down) and world units, so vertices only take the
/// object's size; quads become two triangles.</summary>
public static class YearOfTheDragonSpyroModel
{
    /// <summary>An animation to convert: the source animation's index, then (as for Crash models) whether frames
    /// blend, the frame step, and <paramref name="Repeat"/>: each kept frame shown that many times (a short source
    /// animation stretched to the frame numbers a behaviour reads).</summary>
    public sealed record Animation(int Index, bool Interpolated, int Step = 1, int Repeat = 1);

    /// <summary>Stored coordinates stay within this (the model is stored at a larger scale step otherwise): the object
    /// renderer's per-object depth table has no bound check (<see cref="CrashSpyroModel.Package"/>).</summary>
    public const int StoredLimit = 512;

    /// <param name="soundSlots">The model's sound slots carried over, in package order: package slot i plays the
    /// model's slot soundSlots[i]; frames that play one of them keep doing so (others are dropped).</param>
    public static CrashSpyroModel.Result Build(YearOfTheDragonModels level, int set, string name, IReadOnlyList<Animation> animations,
        double size, bool collision = true, Art? art = null, IReadOnlyList<int>? soundSlots = null)
    {
        var source = level.Animated(set);
        if (animations.Count == 0 || animations.Any(a => a.Index < 0 || a.Index >= source.Count || a.Step is < 1 or > 4 || a.Repeat is < 1 or > 4))
            throw new ArgumentException("Choose animations of the model.");
        var faces = source[animations[0].Index].Faces;
        if (animations.Any(a => !SameFaces(source[a.Index].Faces, faces)))
            throw new InvalidDataException($"{name}'s animations draw different faces; a Spyro object draws one set.");
        art ??= new Art(level);
        if (!ReferenceEquals(art.Level, level)) throw new ArgumentException("A shared texture image belongs to one level.");

        // Feet on the ground: the lowest point of the first frame (z down) is the origin's height or above.
        var first = source[animations[0].Index].Frames[0];
        double ground = Math.Max(0, first.Max(p => p.Z));
        var world = animations.Select(a => source[a.Index].Frames.Where((_, i) => i % a.Step == 0)
            .SelectMany(f => Enumerable.Repeat(f, a.Repeat))
            .Select(f => f.Select(p => (X: p.X * size, Y: p.Y * size, Z: (p.Z - ground) * size)).ToArray()).ToArray()).ToArray();

        List<CrashSpyroModel.Triangle> triangles = [];
        foreach (var face in faces)
        {
            if (face.Texture is { SeeThrough: true }) continue;   // Spyro objects draw opaque
            int[][] parts = face.Vertices.Length == 3 ? [[0, 1, 2]] : [[0, 1, 2], [0, 2, 3]];
            foreach (var part in parts)
            {
                // Year of the Dragon turns a face's front counter-clockwise outward; Spyro the Dragon's object renderer
                // keeps the other turn as the front (as with Crash's models): one-sided triangles are reversed.
                int[] k = face.TwoSided ? part : [part[0], part[2], part[1]];
                byte[]? field = null; (byte, byte, byte) average = (128, 128, 128);
                if (face.Texture is { } t) (field, average) = art.Field(t, k);
                triangles.Add(new([.. k.Select(i => face.Vertices[i])], [.. k.Select(i => face.Colours[i])], face.TwoSided, field, average));
            }
        }
        var keep = (soundSlots ?? []).ToList();
        if (keep.Count > 16 || keep.Distinct().Count() != keep.Count) throw new ArgumentException("Up to 16 distinct sound slots.");
        // A frame's sound plays on the first copy of a repeated frame; the kept frames are every Step-th.
        var meta = animations.Select(a => new CrashSpyroModel.Animation($"{name} {a.Index}", a.Interpolated,
            source[a.Index].FrameSounds.Select((slot, k) => (Frame: k, Slot: keep.IndexOf(slot)))
                .Where(x => x.Slot >= 0 && x.Frame % a.Step == 0).Select(x => (x.Frame / a.Step * a.Repeat, x.Slot)).ToArray())).ToArray();
        byte[] slots = [.. Enumerable.Range(0, keep.Count).Select(i => (byte)i)];
        return CrashSpyroModel.Package(name, meta, world, triangles, collision, slots, art.Vram, StoredLimit);
    }

    private static bool SameFaces(IReadOnlyList<YotdFace> a, IReadOnlyList<YotdFace> b) =>
        ReferenceEquals(a, b) || a.Count == b.Count && a.Zip(b).All(p => p.First.Vertices.SequenceEqual(p.Second.Vertices) && p.First.Texture?.Page == p.Second.Texture?.Page);

    /// <summary>The textures a level's models use, in a Spyro-style texture image (x 512..1023): each texture area at
    /// the place it has in the source level (areas from one level never overlap) and each palette, with the 7 rows
    /// of distance shades the game reads below it (here the palette repeated), where the source level's texture
    /// memory is empty (so no texture area can be copied there later) and nothing was copied.</summary>
    public sealed class Art(YearOfTheDragonModels level)
    {
        public YearOfTheDragonModels Level { get; } = level;
        public byte[] Vram { get; } = new byte[YearOfTheDragonModels.VramBytes];
        private readonly List<(int X0, int Y0, int X1, int Y1)> _areas = [];
        private readonly Dictionary<(int X, int Y, int Count), (int X, int Y)> _cluts = [];
        private readonly List<(int X0, int Y0, int X1, int Y1)> _palettes = [];

        internal (byte[] Field, (byte, byte, byte) Average) Field(YotdTexture t, int[] corners)
        {
            int depth = t.Depth;
            if (depth > 2) throw new InvalidDataException("A texture uses a reserved colour depth.");
            int pageX = (t.Page & 15) * 64 - 512, pageY = (t.Page >> 4 & 1) * 256;
            if (pageX < 0) throw new InvalidDataException("A model texture lies outside the level's half of texture memory.");
            int Column(int u) => depth == 0 ? u / 4 : depth == 1 ? u / 2 : u;
            int u0 = t.Uv.Min(p => p.U), u1 = t.Uv.Max(p => p.U), v0 = t.Uv.Min(p => p.V), v1 = t.Uv.Max(p => p.V);
            int x0 = pageX + Column(u0), x1 = pageX + Column(u1), y0 = pageY + v0, y1 = pageY + v1;
            if (_palettes.Any(r => x0 <= r.X1 && r.X0 <= x1 && y0 <= r.Y1 && r.Y0 <= y1))
                throw new InvalidDataException("A model texture overlaps a palette placed for another one.");
            for (int y = y0; y <= y1; y++) Level.Vram.AsSpan(y * 1024 + x0 * 2, (x1 - x0 + 1) * 2).CopyTo(Vram.AsSpan(y * 1024 + x0 * 2));
            _areas.Add((x0, y0, x1, y1));
            int clutWord = 0;
            if (depth < 2)
            {
                int count = depth == 0 ? 16 : 256;
                if (t.ClutX < 512) throw new InvalidDataException("A model palette lies outside the level's half of texture memory.");
                if (!_cluts.TryGetValue((t.ClutX, t.ClutY, count), out var at))
                {
                    at = PlacePalette(count);
                    int shades = Spyro.Editor.Core.Universal.UniversalAssetImport.ShadeRows;
                    for (int k = 0; k < shades; k++)
                        Level.Vram.AsSpan(t.ClutY * 1024 + (t.ClutX - 512) * 2, count * 2).CopyTo(Vram.AsSpan((at.Y + k) * 1024 + at.X * 2));
                    _cluts[(t.ClutX, t.ClutY, count)] = at;
                }
                clutWord = at.Y << 6 | (512 + at.X) / 16;
            }
            byte[] f = new byte[12];
            int[] uvOffsets = [0, 4, 8];
            for (int k = 0; k < 3; k++) { f[uvOffsets[k]] = (byte)t.Uv[corners[k]].U; f[uvOffsets[k] + 1] = (byte)t.Uv[corners[k]].V; }
            f[10] = f[8]; f[11] = f[9];
            BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(2), (ushort)clutWord);
            BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(6), (ushort)((t.Page & 0x1F) | depth << 7));
            // Average colour of the texture area (for the far, untextured copy).
            long r = 0, g = 0, b = 0, n = 0;
            for (int v = v0; v <= v1; v++)
                for (int u = u0; u <= u1; u++)
                {
                    ushort c = Texel(t, pageX, pageY, u, v);
                    if ((c & 0x7FFF) == 0) continue;
                    r += (c & 31) << 3; g += (c >> 5 & 31) << 3; b += (c >> 10 & 31) << 3; n++;
                }
            return (f, n == 0 ? ((byte)128, (byte)128, (byte)128) : ((byte)(r / n), (byte)(g / n), (byte)(b / n)));
        }

        /// <summary>An 8-row block for a palette (searched from the bottom).</summary>
        private (int X, int Y) PlacePalette(int count)
        {
            int shades = Spyro.Editor.Core.Universal.UniversalAssetImport.ShadeRows;
            bool Overlaps(List<(int X0, int Y0, int X1, int Y1)> rects, int x, int y) =>
                rects.Any(p => x <= p.X1 && p.X0 <= x + count - 1 && y <= p.Y1 && p.Y0 <= y + shades - 1);
            for (int y = 512 - shades; y >= 0; y -= shades)
                for (int x = 0; x + count <= 512; x += 16)
                {
                    if (Overlaps(_palettes, x, y) || Overlaps(_areas, x, y)) continue;
                    if (Enumerable.Range(y, shades).Any(r => Level.Vram.AsSpan(r * 1024 + x * 2, count * 2).ContainsAnyExcept((byte)0))) continue;
                    _palettes.Add((x, y, x + count - 1, y + shades - 1));
                    return (x, y);
                }
            throw new InvalidDataException("These models use more palettes than their texture image can hold.");
        }

        private ushort Texel(YotdTexture t, int pageX, int pageY, int u, int v)
        {
            ushort Half(int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(Level.Vram.AsSpan(y * 1024 + x * 2));
            int y = pageY + v;
            switch (t.Depth)
            {
                case 0: { int index = Half(pageX + u / 4, y) >> (u % 4 * 4) & 15; return Half(t.ClutX - 512 + index, t.ClutY); }
                case 1: { int index = Half(pageX + u / 2, y) >> (u % 2 * 8) & 255; return Half(t.ClutX - 512 + index, t.ClutY); }
                default: return Half(pageX + u, y);
            }
        }
    }
}
