using System.Buffers.Binary;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Colour tables of an animated object model: each animation has a near (+0x18) and a far (+0x20) palette of
/// RGB words, both indexed by the same face colour index. Only the RGB bytes change; each word's fourth byte stays.</summary>
internal static class NativeModelPalettes
{
    internal sealed record Change(int Offset, byte[] Before, byte[] After);
    private sealed record Palette(int Near, int Far, int Count);

    private static int I32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b[at..]);

    private static IReadOnlyList<Palette>? Palettes(ReadOnlySpan<byte> package)
    {
        if (package.Length < 0x3C) return null;
        int animations = I32(package, 0), model = I32(package, 0x34);
        if (animations is < 1 or > 256 || 0x38L + animations * 4L > package.Length || model < 0 || model >= package.Length) return null;
        List<Palette> result = [];
        for (int i = 0; i < animations; i++)
        {
            int animation = I32(package, 0x38 + i * 4);
            if (animation == -1) continue;
            if (animation < 0 || animation + 0x24L > package.Length) return null;
            int count = BinaryPrimitives.ReadUInt16LittleEndian(package[(animation + 2)..]);
            long near = (long)model + I32(package, animation + 0x18), far = (long)model + I32(package, animation + 0x20);
            if (count is < 1 or > 256 || near < 0 || far < 0 || near + count * 4L > package.Length || far + count * 4L > package.Length) return null;
            var palette = new Palette((int)near, (int)far, count);
            if (!result.Contains(palette)) result.Add(palette);
        }
        return result.Count == 0 ? null : result;
    }

    private static double Luminance(int r, int g, int b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
    private static double Luminance(IReadOnlyList<double> c) => 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    private static double Greenness(ReadOnlySpan<byte> c) => Math.Clamp((c[1] - Math.Max(c[0], c[2]) - 8) / 24.0, 0, 1);

    /// <summary>Texture palette rows the object renderer can draw with: the palette and the seven distance-shade rows
    /// below it. Up close (0x80021730) a textured face uses row 0; further out (0x800215B0..0x800215E0) it uses row
    /// (1024 - v) / 128 while its colour words slide from the near table to the far one, and past that the untextured
    /// low-detail faces take over with the far table.</summary>
    internal const int ClutRows = 8;
    internal const int VramRowBytes = 1024;
    private const int ClutBytes = 32;

    /// <summary>Which near colour words of a static model light textured faces only (true), are plain colours of
    /// untextured faces (false), or are not used by its high-detail faces (null). Null when the face table is not
    /// the plain grammar this reads.</summary>
    private static bool?[]? NearWordRoles(ReadOnlySpan<byte> package, int animation, int count)
    {
        long table = (long)I32(package, 0x34) + I32(package, animation + 0x14);
        if (table < 0 || table + 4 > package.Length) return null;
        int length = I32(package, (int)table);
        long cursor = table + 4, end = cursor + length;
        if (length < 0 || end > package.Length) return null;
        bool?[] roles = new bool?[count];
        while (cursor < end)
        {
            if (cursor + 8 > end) return null;
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(package[(int)cursor..]), colour = BinaryPrimitives.ReadUInt32LittleEndian(package[(int)(cursor + 4)..]);
            bool quad = (word & 0x80000000) != 0, textured = (word & 2) != 0;
            if (quad && (word & 4) != 0) return null;   // alternate command: its colour fields are not read here
            int size = quad ? textured ? 24 : 12 : textured ? 20 : 8;
            if (cursor + size > end) return null;
            List<int> indices = [(int)(colour >> 19 & 511), (int)(colour >> 10 & 511), (int)(colour >> 1 & 511)];
            if (quad) indices.Add((int)(BinaryPrimitives.ReadUInt32LittleEndian(package[(int)(cursor + 8)..]) >> 11 & 511));
            foreach (int index in indices)
            {
                if (index >= count) return null;
                roles[index] = (roles[index] ?? true) && textured;
            }
            cursor += size;
        }
        return roles;
    }

    /// <summary>Trees and plants: a static model (one single-frame animation) whose colours are at least a quarter
    /// green. Their foliage turns snow white where the near palette is bright (lit) and frosty blue in shade, keeping
    /// each colour's own shading; trunks and other colours stay. A near word that only lights textured faces is not a
    /// colour but a multiplier of the texture (0x80 = as stored), and the snow is in the texture palette
    /// (<see cref="SnowClutRows"/>): such a word keeps its brightness and only takes the snow's cool tint, so close up
    /// the foliage stays shaded like the far colours instead of every texel washing out white. Null when the model
    /// is not a plant.</summary>
    internal static IReadOnlyList<Change>? Snow(byte[] package)
    {
        var palettes = Palettes(package);
        if (palettes is not { Count: 1 } || I32(package, 0) != 1 || BinaryPrimitives.ReadInt16LittleEndian(package.AsSpan(I32(package, 0x38))) != 1)
            return null;
        var (near, far, count) = palettes[0];
        byte[] after = package.ToArray();
        double[] weight = new double[count];
        for (int i = 0; i < count; i++)
            weight[i] = Math.Max(Greenness(package.AsSpan(near + i * 4, 3)), Greenness(package.AsSpan(far + i * 4, 3)));
        if (weight.Count(w => w > 0) * 4 < count) return null;
        bool?[]? roles = NearWordRoles(package, I32(package, 0x38), count);
        for (int i = 0; i < count; i++)
        {
            if (weight[i] == 0) continue;
            var lit = package.AsSpan(near + i * 4, 3);
            double light = Luminance(lit[0], lit[1], lit[2]);
            double t = Math.Clamp((light - 90) / 90, 0, 1);
            double[] snow = [168 + (242 - 168) * t, 196 + (246 - 196) * t, 218 + (252 - 218) * t];
            // Without a readable face table, a near word with no green of its own is taken to light a texture.
            bool lightsTexture = roles == null ? Greenness(lit) == 0 : roles[i] == true;
            foreach (int table in new[] { near, far })
            {
                int at = table + i * 4;
                double[] target = table == near && lightsTexture
                    ? snow.Select(c => c / Luminance(snow) * light).ToArray()
                    : snow.Select(c => c * Math.Clamp(0.6 + 0.5 * Luminance(package[at], package[at + 1], package[at + 2]) / 180, 0.6, 1.0)).ToArray();
                for (int c = 0; c < 3; c++)
                    after[at + c] = (byte)Math.Clamp(Math.Round(package[at + c] + (target[c] - package[at + c]) * weight[i]), 0, 255);
            }
        }
        return Changes(package, after, palettes);
    }

    /// <summary>Snow on an object's 16-colour texture palette at <paramref name="offset"/> of the level's texture pages
    /// and on its seven distance-shade rows below (see <see cref="ClutRows"/>): row 0 takes <see cref="SnowClut"/>;
    /// shade row k is the palette faded toward a grey and keeps (17 - 2k)/16 of row 0 (measured on the retail levels),
    /// so it moves by that share of row 0's change and keeps its own fade. Without the shade rows a snowy tree
    /// turned green again at middle distance. One change per row that changes; rows past the pages are absent.</summary>
    internal static IReadOnlyList<Change> SnowClutRows(ReadOnlySpan<byte> textures, int offset)
    {
        if (offset < 0 || offset + ClutBytes > textures.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        byte[] row0 = textures.Slice(offset, ClutBytes).ToArray(), snow0 = SnowClut(row0);
        List<Change> changes = [new(offset, row0, snow0)];
        static int[] Five(ushort c) => [c & 31, c >> 5 & 31, c >> 10 & 31];
        for (int k = 1; k < ClutRows && offset + k * VramRowBytes + ClutBytes <= textures.Length; k++)
        {
            int at = offset + k * VramRowBytes;
            byte[] before = textures.Slice(at, ClutBytes).ToArray(), after = before.ToArray();
            double keep = (17 - 2 * k) / 16.0;
            for (int i = 0; i < ClutBytes / 2; i++)
            {
                ushort original = BinaryPrimitives.ReadUInt16LittleEndian(row0.AsSpan(i * 2)), snowed = BinaryPrimitives.ReadUInt16LittleEndian(snow0.AsSpan(i * 2));
                ushort shade = BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(i * 2));
                if (original == snowed || (shade & 0x7FFF) == 0) continue;
                int[] from = Five(original), to = Five(snowed), own = Five(shade);
                int[] five = Enumerable.Range(0, 3).Select(c => Math.Clamp((int)Math.Round(own[c] + (to[c] - from[c]) * keep), 0, 31)).ToArray();
                ushort packed = (ushort)((shade & 0x8000) | five[0] | five[1] << 5 | five[2] << 10);
                if ((packed & 0x7FFF) == 0) packed |= 1;   // never become the transparent colour
                BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(i * 2), packed);
            }
            changes.Add(new(at, before, after));
        }
        return changes.Where(c => !c.Before.AsSpan().SequenceEqual(c.After)).ToArray();
    }

    /// <summary>A 16-colour texture palette (15-bit colours) with snow on its green entries: white for the brightest,
    /// frosty blue for the darkest. Transparent black and each colour's mask bit stay.</summary>
    internal static byte[] SnowClut(ReadOnlySpan<byte> clut)
    {
        byte[] after = clut.ToArray();
        var colours = Enumerable.Range(0, after.Length / 2).Select(i => BinaryPrimitives.ReadUInt16LittleEndian(after.AsSpan(i * 2))).ToArray();
        static (int R, int G, int B) Rgb(ushort c) => ((c & 31) << 3, (c >> 5 & 31) << 3, (c >> 10 & 31) << 3);
        double top = colours.Where(c => (c & 0x7FFF) != 0).Select(c => { var (r, g, b) = Rgb(c); return Luminance(r, g, b); }).DefaultIfEmpty(1).Max();
        for (int i = 0; i < colours.Length; i++)
        {
            if ((colours[i] & 0x7FFF) == 0) continue;
            var (r, g, b) = Rgb(colours[i]);
            double weight = Math.Clamp((g - Math.Max(r, b) - 8) / 24.0, 0, 1);
            if (weight == 0) continue;
            double t = Math.Clamp(Luminance(r, g, b) / Math.Max(top, 1), 0, 1);
            double[] snow = [150 + (238 - 150) * t, 180 + (243 - 180) * t, 205 + (252 - 205) * t];
            int[] rgb = [r, g, b];
            int[] five = Enumerable.Range(0, 3).Select(c => Math.Clamp((int)Math.Round((rgb[c] + (snow[c] - rgb[c]) * weight) / 8), 0, 31)).ToArray();
            ushort packed = (ushort)((colours[i] & 0x8000) | five[0] | five[1] << 5 | five[2] << 10);
            if ((packed & 0x7FFF) == 0) packed |= 1;   // never become the transparent colour
            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(i * 2), packed);
        }
        return after;
    }

    /// <summary>Every palette colour takes <paramref name="tint"/>'s hue at the colour's own brightness (its largest
    /// channel), the result the per-statue dragon renderer draws. The tint is scaled to full strength first, so that
    /// renderer, which keeps only each colour's brightness, still gives exactly the chosen colour.</summary>
    internal static IReadOnlyList<Change> Hue(byte[] package, ColorRgba tint)
    {
        var palettes = Palettes(package) ?? throw new InvalidDataException("The model has no colour palette.");
        int top = Math.Max(tint.R, Math.Max(tint.G, tint.B));
        double[] full = top == 0 ? [0, 0, 0] : [tint.R * 255.0 / top, tint.G * 255.0 / top, tint.B * 255.0 / top];
        byte[] after = package.ToArray();
        foreach (var (near, far, count) in palettes)
            foreach (int table in new[] { near, far })
                for (int i = 0; i < count; i++)
                {
                    int at = table + i * 4, bright = Math.Max(package[at], Math.Max(package[at + 1], package[at + 2]));
                    for (int c = 0; c < 3; c++) after[at + c] = (byte)Math.Clamp(Math.Round(full[c] * bright / 255), 0, 255);
                }
        return Changes(package, after, palettes);
    }

    private static IReadOnlyList<Change> Changes(byte[] before, byte[] after, IReadOnlyList<Palette> palettes) =>
        palettes.SelectMany(p => new[] { (p.Near, p.Count), (p.Far, p.Count) }).Distinct()
            .Select(t => new Change(t.Item1, before.AsSpan(t.Item1, t.Item2 * 4).ToArray(), after.AsSpan(t.Item1, t.Item2 * 4).ToArray()))
            .Where(c => !c.Before.AsSpan().SequenceEqual(c.After)).ToArray();
}
