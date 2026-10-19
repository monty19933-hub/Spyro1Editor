using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Sky;

namespace Spyro.Editor.Core.Generation;

/// <summary>A sky painted in a theme's colours (a 360° panorama: gradient to the horizon, sun or moon with its glow,
/// clouds, distant hills, stars at night) and built into a native sky like Import Sky builds one.</summary>
public static class ProceduralSky
{
    public const int Width = 1024, Height = 512;

    public static SkyImage Paint(LevelTheme theme, LevelRandom rnd)
    {
        var px = new Rgba32[Width * Height];
        // Clouds and hills: tileable noise round the horizon.
        var cloudLattice = Enumerable.Range(0, 64 * 16).Select(_ => (float)rnd.NextDouble()).ToArray();
        float Cloud(double u, double e)
        {
            float sum = 0, amp = 1, total = 0;
            for (int o = 0, cx = 8, cy = 4; o < 4; o++, cx *= 2, cy *= 2, amp *= 0.5f)
            {
                double fx = u * cx, fy = e / (Math.PI / 2) * cy * 2;
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                float tx = (float)(fx - x0), ty = (float)(fy - y0);
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                float L(int a, int b) => cloudLattice[((b % 16 + 16) % 16) * 64 + ((a % cx + cx) % cx) % 64];
                sum += amp * ((L(x0, y0) * (1 - tx) + L(x0 + 1, y0) * tx) * (1 - ty) + (L(x0, y0 + 1) * (1 - tx) + L(x0 + 1, y0 + 1) * tx) * ty);
                total += amp;
            }
            return sum / total;
        }
        var hills = new double[Width];
        double h1 = rnd.NextDouble() * 6, h2 = rnd.NextDouble() * 6, h3 = rnd.NextDouble() * 6;
        for (int x = 0; x < Width; x++)
        {
            double u = x / (double)Width * Math.PI * 2;
            hills[x] = 0.035 + 0.025 * Math.Sin(3 * u + h1) + 0.018 * Math.Sin(7 * u + h2) + 0.01 * Math.Sin(17 * u + h3);
        }
        // The sun: the panorama's middle column faces +X; turning right (clockwise) moves right in the picture.
        double sunU = ((0.5 - theme.SunAzimuth / 360.0) % 1 + 1) % 1, sunE = Math.Min(theme.SunElevation, 40) * Math.PI / 180;
        var stars = new HashSet<int>();
        if (theme.Stars) for (int k = 0; k < 900; k++) stars.Add(rnd.Next(Width) + rnd.Next(Height * 2 / 5) * Width);
        for (int y = 0; y < Height; y++)
        {
            double e = (0.5 - (y + 0.5) / Height) * Math.PI;
            for (int x = 0; x < Width; x++)
            {
                double u = (x + 0.5) / Width;
                (double R, double G, double B) c;
                if (e >= 0)
                {
                    double t = Math.Pow(e / (Math.PI / 2), 0.55);
                    c = Lerp(theme.SkyHorizon, theme.SkyTop, t);
                    // Sun glow and disc.
                    double du = Math.Min(Math.Abs(u - sunU), 1 - Math.Abs(u - sunU)) * Math.PI * 2 * Math.Cos(e);
                    double d = Math.Sqrt(du * du + (e - sunE) * (e - sunE));
                    double glow = Math.Exp(-d * d / 0.05) * 0.55;
                    c = Lerp(c, theme.SkySun, glow);
                    if (d < 0.06) c = Lerp(c, theme.SkySun, 1);
                    if (stars.Contains(y * Width + x) && d > 0.2) c = Lerp(c, new ColorRgba(255, 255, 255), 0.85);
                    // Clouds, thinning towards the top.
                    float cl = Cloud(u, e);
                    double amount = Math.Clamp((cl - (1 - theme.CloudAmount) * 0.85) * 3, 0, 1) * Math.Clamp(1.4 - e * 1.1, 0, 1);
                    var shaded = Lerp(theme.SkyClouds, theme.SkyTop, 0.25 * (1 - cl));
                    c = (c.R * (1 - amount) + shaded.R * amount, c.G * (1 - amount) + shaded.G * amount, c.B * (1 - amount) + shaded.B * amount);
                    if (theme.SkyHills is { } hill && e < hills[x]) c = Lerp(hill, theme.SkyHorizon, Math.Clamp(1 - e / hills[x], 0, 1) * 0.35);
                }
                else
                {
                    double t = Math.Pow(-e / (Math.PI / 2), 0.4);
                    c = Lerp(theme.SkyHorizon, theme.SkyBelow, t);
                }
                px[y * Width + x] = new Rgba32((byte)Math.Clamp(c.R, 0, 255), (byte)Math.Clamp(c.G, 0, 255), (byte)Math.Clamp(c.B, 0, 255), 255);
            }
        }
        return new SkyImage(Width, Height, px);
    }

    /// <summary>The painted sky as a native sky block a built level can keep (<see cref="SceneDocument.CustomSky"/>).</summary>
    public static byte[] Build(LevelTheme theme, LevelRandom rnd)
    {
        var panorama = SkyPanorama.FromImage(Paint(theme, rnd));
        foreach (int triangles in new[] { 2600, 1800, 1200 })
        {
            var built = NativeSkyBuilder.Build(panorama, new SkyBuildOptions(triangles));
            if (built.Parts <= 254) return built.Block;
        }
        throw new InvalidDataException("the sky needs more parts than a built level's view group lists");
    }

    private static (double R, double G, double B) Lerp(ColorRgba a, ColorRgba b, double t) =>
        (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    private static (double R, double G, double B) Lerp((double R, double G, double B) a, ColorRgba b, double t) =>
        (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
}
