using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>Pictures the generator paints for a level's roles: 64 x 64, seamless (each repeats edge to edge like the
/// game's own textures), in the theme's colours, with few shades as the game's textures have. Row 0 is the top (a wall's
/// top edge).</summary>
public static class ProceduralPictures
{
    public const int Size = 64;

    public static Rgba32[] Paint(SurfaceRole role, LevelTheme theme, LevelRandom rnd)
    {
        var c = theme.Colors[role];
        float[] v = role switch
        {
            SurfaceRole.Ground => Grass(rnd),
            SurfaceRole.GroundAlt => Grass(rnd),
            SurfaceRole.Path or SurfaceRole.Paving => role == SurfaceRole.Paving ? Tiles(rnd) : Cobbles(rnd, 11),
            SurfaceRole.Dirt => Mix(Noise(rnd, 4, 0.55f), Cobbles(rnd, 28), 0.25f),
            SurfaceRole.Sand => Sand(rnd),
            SurfaceRole.Cliff or SurfaceRole.Rock or SurfaceRole.CaveWall or SurfaceRole.Ceiling or SurfaceRole.Underside => Strata(rnd),
            SurfaceRole.CliffTop => Strata(rnd),
            SurfaceRole.Brick => Bricks(rnd),
            SurfaceRole.Roof => Shingles(rnd),
            SurfaceRole.Wood => Planks(rnd),
            SurfaceRole.Water => Water(rnd),
            SurfaceRole.Lava => Lava(rnd),
            SurfaceRole.Goo => Goo(rnd),
            SurfaceRole.Charge => Chevrons(),
            _ => Noise(rnd, 4, 0.5f),
        };
        var pixels = Colour(v, c);
        if (role == SurfaceRole.GroundAlt) Flowers(pixels, rnd, theme);
        if (role == SurfaceRole.CliffTop) Fringe(pixels, rnd, theme.Colors[SurfaceRole.Ground]);
        return pixels;
    }

    /// <summary>Tileable value noise, summed over octaves from <paramref name="cells"/> lattice cells across.</summary>
    private static float[] Noise(LevelRandom rnd, int cells, float persistence, int octaves = 4, float stretchY = 1)
    {
        var v = new float[Size * Size];
        float amp = 1, total = 0;
        for (int o = 0; o < octaves; o++, cells *= 2, amp *= persistence)
        {
            int cx = cells, cy = Math.Max(1, (int)(cells * stretchY));
            var lattice = new float[cx * cy];
            for (int k = 0; k < lattice.Length; k++) lattice[k] = (float)rnd.NextDouble();
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float fx = x * cx / (float)Size, fy = y * cy / (float)Size;
                    int x0 = (int)fx, y0 = (int)fy; float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                    float L(int a, int b) => lattice[(b % cy) * cx + a % cx];
                    v[y * Size + x] += amp * ((L(x0, y0) * (1 - tx) + L(x0 + 1, y0) * tx) * (1 - ty) + (L(x0, y0 + 1) * (1 - tx) + L(x0 + 1, y0 + 1) * tx) * ty);
                }
            total += amp;
        }
        for (int k = 0; k < v.Length; k++) v[k] /= total;
        return v;
    }
    private static float Smooth(float t) => t * t * (3 - 2 * t);
    private static float[] Mix(float[] a, float[] b, float t) => [.. a.Select((x, k) => x * (1 - t) + b[k] * t)];

    /// <summary>Distance to the nearest and second nearest of <paramref name="count"/> random points (wrapping), and the
    /// nearest point's index.</summary>
    private static (float D1, float D2, int Id)[] Cells(LevelRandom rnd, int count)
    {
        var pts = Enumerable.Range(0, count).Select(_ => ((float)rnd.NextDouble() * Size, (float)rnd.NextDouble() * Size)).ToArray();
        var r = new (float, float, int)[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d1 = float.MaxValue, d2 = float.MaxValue; int id = 0;
                for (int k = 0; k < pts.Length; k++)
                {
                    float dx = Math.Abs(x - pts[k].Item1), dy = Math.Abs(y - pts[k].Item2);
                    dx = Math.Min(dx, Size - dx); dy = Math.Min(dy, Size - dy);
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < d1) { d2 = d1; d1 = d; id = k; } else if (d < d2) d2 = d;
                }
                r[y * Size + x] = (d1, d2, id);
            }
        return r;
    }

    private static float[] Grass(LevelRandom rnd)
    {
        var a = Noise(rnd, 4, 0.5f);
        var blades = Noise(rnd, 16, 0.45f, 2, 0.25f);
        return [.. a.Select((x, k) => 0.55f * x + 0.45f * blades[k])];
    }

    private static float[] Cobbles(LevelRandom rnd, int stones)
    {
        var c = Cells(rnd, stones);
        var n = Noise(rnd, 8, 0.5f, 3);
        var shade = Enumerable.Range(0, stones).Select(_ => (float)rnd.NextDouble()).ToArray();
        return [.. c.Select((p, k) => p.D2 - p.D1 < 1.6f ? 0.05f : 0.35f + 0.35f * shade[p.Id] + 0.3f * n[k] - 0.15f * Math.Max(0, 3 - (p.D2 - p.D1)) / 3)];
    }

    private static float[] Tiles(LevelRandom rnd)
    {
        var n = Noise(rnd, 8, 0.5f, 3);
        var shade = Enumerable.Range(0, 4).Select(_ => 0.45f + 0.3f * (float)rnd.NextDouble()).ToArray();
        var v = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int tx = x / 32, ty = y / 32, lx = x % 32, ly = y % 32;
                float edge = lx == 0 || ly == 0 ? 0.08f : lx == 1 || ly == 1 ? 0.85f : lx == 31 || ly == 31 ? 0.25f : shade[ty * 2 + tx];
                v[y * Size + x] = edge * 0.8f + 0.2f * n[y * Size + x];
            }
        return v;
    }

    private static float[] Sand(LevelRandom rnd)
    {
        var n = Noise(rnd, 8, 0.6f, 3);
        double p = rnd.NextDouble() * 6;
        return [.. n.Select((x, k) => 0.7f * x + 0.3f * (float)(0.5 + 0.5 * Math.Sin((k / Size) * Math.PI * 2 / 16 * 3 + Math.Sin((k % Size) * Math.PI * 2 / Size) * 1.5 + p)))];
    }

    private static float[] Strata(LevelRandom rnd)
    {
        var n = Noise(rnd, 4, 0.55f);
        var band = Noise(rnd, 2, 0.5f, 3, 4);
        var cracks = Cells(rnd, 7);
        return [.. n.Select((x, k) =>
        {
            int y = k / Size;
            float layer = 0.5f + 0.5f * MathF.Sin(y * MathF.PI * 2 / 64 * 4 + band[k] * 6);
            float v = 0.55f * x + 0.35f * layer + 0.1f;
            return cracks[k].D2 - cracks[k].D1 < 1.2f ? v * 0.45f : v;
        })];
    }

    private static float[] Bricks(LevelRandom rnd)
    {
        var n = Noise(rnd, 8, 0.5f, 3);
        var shade = Enumerable.Range(0, 64).Select(_ => 0.45f + 0.35f * (float)rnd.NextDouble()).ToArray();
        var v = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int row = y / 8, ox = row % 2 == 0 ? 0 : 8;
                int bx = ((x + ox) % Size) / 16, lx = (x + ox) % 16, ly = y % 8;
                bool mortar = ly == 7 || lx == 15;
                float b = shade[(row * 4 + bx) % shade.Length];
                v[y * Size + x] = mortar ? 0.15f : (ly == 0 ? b * 1.25f : b) * 0.85f + 0.15f * n[y * Size + x];
            }
        return v;
    }

    private static float[] Shingles(LevelRandom rnd)
    {
        var n = Noise(rnd, 8, 0.5f, 3);
        var v = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int row = y / 8, ox = row % 2 == 0 ? 0 : 4;
                float lx = ((x + ox) % 8) - 3.5f, ly = y % 8;
                // Scalloped: each shingle's lower edge is a half circle.
                float edge = 8 - MathF.Sqrt(Math.Max(0, 16 - lx * lx)) * 1.5f;
                float s = ly > edge ? 0.2f : 0.45f + 0.4f * (1 - ly / 8f);
                v[y * Size + x] = s * 0.85f + 0.15f * n[y * Size + x];
            }
        return v;
    }

    private static float[] Planks(LevelRandom rnd)
    {
        var grain = Noise(rnd, 2, 0.6f, 4, 8);
        var shade = Enumerable.Range(0, 4).Select(_ => 0.45f + 0.3f * (float)rnd.NextDouble()).ToArray();
        var v = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int plank = x / 16, lx = x % 16;
                float s = lx == 0 ? 0.08f : lx == 15 ? 0.3f : shade[plank] + 0.35f * (grain[(y * Size + x)] - 0.5f);
                if ((y == 6 || y == 57) && (lx == 4 || lx == 11)) s = 0.95f;   // nails
                v[y * Size + x] = s;
            }
        return v;
    }

    private static float[] Water(LevelRandom rnd)
    {
        var n = Noise(rnd, 4, 0.55f);
        var c = Cells(rnd, 12);
        return [.. n.Select((x, k) => 0.6f * x + (c[k].D2 - c[k].D1 < 2 ? 0.35f : 0.1f))];
    }

    private static float[] Lava(LevelRandom rnd)
    {
        var n = Noise(rnd, 4, 0.6f);
        var c = Cells(rnd, 9);
        return [.. n.Select((x, k) => c[k].D2 - c[k].D1 < 2.5f ? 0.15f + 0.2f * x : 0.55f + 0.45f * x)];
    }

    private static float[] Goo(LevelRandom rnd)
    {
        var c = Cells(rnd, 14);
        var n = Noise(rnd, 8, 0.5f, 3);
        return [.. c.Select((p, k) => p.D1 < 5 ? 0.85f - p.D1 * 0.08f : 0.35f + 0.25f * n[k])];
    }

    /// <summary>Arrows pointing up the picture (a supercharge runway's direction, when the face is laid that way).</summary>
    private static float[] Chevrons()
    {
        var v = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float t = (y + Math.Abs(x - 31.5f) * 0.75f) % 32;
                v[y * Size + x] = t < 10 ? 0.95f : t < 12 ? 0.5f : 0.15f;
            }
        return v;
    }

    /// <summary>Values (0-1) to the role's dark, base and light colours, in 12 shades.</summary>
    private static Rgba32[] Colour(float[] v, ThemeColors c)
    {
        float lo = v.Min(), hi = v.Max(), span = Math.Max(0.001f, hi - lo);
        var px = new Rgba32[v.Length];
        for (int k = 0; k < v.Length; k++)
        {
            float t = MathF.Round((v[k] - lo) / span * 11) / 11;
            ColorRgba a = t < 0.5f ? c.Dark : c.Base, b = t < 0.5f ? c.Base : c.Light;
            float f = t < 0.5f ? t * 2 : (t - 0.5f) * 2;
            px[k] = new Rgba32((byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f), 255);
        }
        return px;
    }

    private static void Flowers(Rgba32[] px, LevelRandom rnd, LevelTheme theme)
    {
        Rgba32[] petals = [new(250, 240, 120, 255), new(250, 250, 250, 255), new(240, 140, 200, 255), new(150, 190, 250, 255)];
        for (int k = 0; k < 18; k++)
        {
            int x = rnd.Next(Size), y = rnd.Next(Size);
            var p = rnd.Pick(petals);
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
                px[((y + dy + Size) % Size) * Size + (x + dx + Size) % Size] = dx == 0 && dy == 0 ? new(255, 220, 80, 255) : p;
        }
    }

    /// <summary>Grass over the top of a cliff picture, hanging down in tufts.</summary>
    private static void Fringe(Rgba32[] px, LevelRandom rnd, ThemeColors grass)
    {
        var g = Colour(Grass(rnd), grass);
        var drip = Noise(rnd, 8, 0.5f, 2);
        for (int x = 0; x < Size; x++)
        {
            int depth = 14 + (int)(drip[x] * 16) + ((x * 7) % 5 == 0 ? 6 : 0);
            for (int y = 0; y < depth; y++)
            {
                var c = g[y * Size + x];
                px[y * Size + x] = y >= depth - 2 ? new Rgba32((byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6), 255) : c;
            }
        }
    }
}
