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
    private static double Greenness(ReadOnlySpan<byte> c) => Math.Clamp((c[1] - Math.Max(c[0], c[2]) - 8) / 24.0, 0, 1);

    /// <summary>Trees and plants: a static model (one single-frame animation) whose colours are at least a quarter
    /// green. Their foliage turns snow white where the near palette is bright (lit) and frosty blue in shade, keeping
    /// each colour's own shading; trunks and other colours stay. Null when the model is not a plant.</summary>
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
        for (int i = 0; i < count; i++)
        {
            if (weight[i] == 0) continue;
            var lit = package.AsSpan(near + i * 4, 3);
            double t = Math.Clamp((Luminance(lit[0], lit[1], lit[2]) - 90) / 90, 0, 1);
            double[] snow = [168 + (242 - 168) * t, 196 + (246 - 196) * t, 218 + (252 - 218) * t];
            foreach (int table in new[] { near, far })
            {
                int at = table + i * 4;
                double shade = Math.Clamp(0.6 + 0.5 * Luminance(package[at], package[at + 1], package[at + 2]) / 180, 0.6, 1.0);
                for (int c = 0; c < 3; c++)
                    after[at + c] = (byte)Math.Clamp(Math.Round(package[at + c] + (snow[c] * shade - package[at + c]) * weight[i]), 0, 255);
            }
        }
        return Changes(package, after, palettes);
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
