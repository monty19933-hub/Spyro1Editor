using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>The game draws a textured face only near the camera; farther away it draws the face untextured in its far
/// colours. Retail far colours are the texture's own colour times the face's light, so the change is hard to see. A
/// face whose far colours were never chosen (missing, or the same as its near colours, which only modulate the
/// texture: 128 draws it as stored) would draw its shading values instead, and the edge where the texture starts
/// visibly travels with Spyro. Those faces get far = texture average x near / 128.</summary>
public static class NativeAuthoredFarColors
{
    public static SceneDocument Derive(SceneDocument document, Func<AuthoredMaterial, ColorRgba?> average)
    {
        Dictionary<AuthoredMaterial, ColorRgba?> cache = [];
        // Sectors with their own distant mesh drawn always keep their faces' far colours (AuthoredFarMesh.Always).
        var own = document.FarMeshes?.Where(m => m.Always).Select(m => m.Sector).ToHashSet() ?? [];
        var surfaces = document.Surfaces.Select(s =>
        {
            if (s.Material is not { } m) return s;
            if (s.Sector is int sector && own.Contains(sector)) return s;
            var near = s.NearColors ?? Enumerable.Repeat(s.Color, s.Vertices.Length).ToArray();
            if (s.FarColors != null && !s.FarColors.SequenceEqual(near)) return s;
            if (!cache.TryGetValue(m, out var avg)) cache[m] = avg = average(m);
            if (avg is not { } a) return s;
            static byte Mix(byte shade, byte texture) => (byte)Math.Clamp((shade * texture + 64) / 128, 0, 255);
            var far = near.Select(c => new ColorRgba(Mix(c.R, a.R), Mix(c.G, a.G), Mix(c.B, a.B), c.A)).ToArray();
            return s with { FarColors = far };
        }).ToArray();
        return document with { Surfaces = surfaces };
    }

    /// <summary>The average opaque colour of a material: its own picture, or the retail texture's far (whole-tile) image.</summary>
    public static ColorRgba? Average(string originalUSA, AuthoredMaterial m)
    {
        long r = 0, g = 0, b = 0, n = 0;
        void Add(int cr, int cg, int cb) { r += cr; g += cg; b += cb; n++; }
        if (m.Image is { } path)
        {
            if (!File.Exists(path)) return null;
            foreach (var p in PngRgbaImage.ReadResizedRgba(path, 32, 32)) if (p.A >= 128) Add(p.R, p.G, p.B);
        }
        else
        {
            var source = NativeStaticTerrainTextureSource.Capture(originalUSA, m.SourceWadEntry, m.TextureId);
            var d = source.Layout.Descriptors[0];
            byte[] vram = source.CopySourceVram(), pixels = d.CopyPixels(vram), palette = d.BitsPerPixel < 16 ? d.CopyPalette(vram) : [];
            void Colour(int c) { if ((c & 0x7FFF) != 0) Add((c & 31) << 3, (c >> 5 & 31) << 3, (c >> 10 & 31) << 3); }
            int Entry(int i) => 2 * i + 1 < palette.Length ? palette[2 * i] | palette[2 * i + 1] << 8 : 0;
            for (int row = 0; row < d.PixelRowCount; row++)
                for (int k = 0; k < d.PixelRowByteLength; k++)
                {
                    int at = row * d.PixelRowByteLength + k, v = pixels[at];
                    switch (d.BitsPerPixel)
                    {
                        case 4: Colour(Entry(v & 15)); Colour(Entry(v >> 4)); break;
                        case 8: Colour(Entry(v)); break;
                        default: if (k % 2 == 0 && at + 1 < pixels.Length) Colour(v | pixels[at + 1] << 8); break;
                    }
                }
        }
        return n == 0 ? null : new ColorRgba((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
    }
}
