using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>A sky as a 360° panorama (see <see cref="SkyDirections"/>), kept at several sizes so a sky corner can take
/// the average colour of the area around it: from a panorama picture, or from a sky model seen from its centre.</summary>
public sealed class SkyPanorama
{
    private const int BaseWidth = 2048;
    private readonly List<(int Width, int Height, float[] Rgb)> _levels = [];

    private SkyPanorama(int width, int height, float[] rgb, IReadOnlyList<string> notes)
    {
        _levels.Add((width, height, rgb));
        while (width > 16)
        {
            int w = width / 2, h = Math.Max(1, height / 2);
            var next = new float[w * h * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    for (int k = 0; k < 3; k++)
                    {
                        int y2 = Math.Min(height - 1, 2 * y + 1);
                        next[(y * w + x) * 3 + k] = (rgb[(2 * y * width + 2 * x) * 3 + k] + rgb[(2 * y * width + 2 * x + 1) * 3 + k] +
                            rgb[(y2 * width + 2 * x) * 3 + k] + rgb[(y2 * width + 2 * x + 1) * 3 + k]) / 4;
                    }
            _levels.Add((w, h, next));
            width = w; height = h; rgb = next;
        }
        Notes = notes;
    }

    /// <summary>Plain-English remarks about the source (not a 2:1 picture, a model without a floor…).</summary>
    public IReadOnlyList<string> Notes { get; }

    public int Width => _levels[0].Width;
    public int Height => _levels[0].Height;

    /// <summary>A panorama picture (equirectangular: twice as wide as tall). Transparent parts count as black.</summary>
    public static SkyPanorama FromImage(SkyImage image)
    {
        List<string> notes = [];
        double aspect = image.Width / (double)image.Height;
        if (aspect < 1.9 || aspect > 2.1)
            notes.Add($"The picture is {image.Width}×{image.Height}, not twice as wide as it is tall, so it may not be a 360° panorama. It is stretched to wrap all the way around.");
        int width = Math.Min(BaseWidth, Math.Max(64, image.Width)), height = width / 2;
        var rgb = new float[width * height * 3];
        if (image.Width >= width)
        {
            // Area average: each panorama pixel is the mean of the picture's pixels inside it.
            var sum = new double[width * height * 3]; var count = new int[width * height];
            for (int y = 0; y < image.Height; y++)
            {
                int ty = Math.Min(height - 1, y * height / image.Height);
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 p = image.Pixels[y * image.Width + x];
                    int t = ty * width + Math.Min(width - 1, (int)((long)x * width / image.Width));
                    double a = p.A / 255.0;
                    sum[t * 3] += p.R * a; sum[t * 3 + 1] += p.G * a; sum[t * 3 + 2] += p.B * a; count[t]++;
                }
            }
            for (int t = 0; t < width * height; t++)
                if (count[t] > 0) for (int k = 0; k < 3; k++) rgb[t * 3 + k] = (float)(sum[t * 3 + k] / count[t]);
                else { var (r, g, b, a) = image.Sample((t % width + 0.5) / width, (t / width + 0.5) / height); rgb[t * 3] = r * a / 255; rgb[t * 3 + 1] = g * a / 255; rgb[t * 3 + 2] = b * a / 255; }
        }
        else
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b, a) = image.Sample((x + 0.5) / width, (y + 0.5) / height);
                    int t = y * width + x;
                    rgb[t * 3] = r * a / 255; rgb[t * 3 + 1] = g * a / 255; rgb[t * 3 + 2] = b * a / 255;
                }
        return new SkyPanorama(width, height, rgb, notes);
    }

    /// <summary>A sky model seen from where the viewer stands: the origin, or the middle of the model when the origin is
    /// outside it. Each direction shows the nearest part of the model; directions it leaves open take the nearest
    /// colours it shows.</summary>
    public static SkyPanorama FromModel(SkyModel model)
    {
        if (model.Triangles.Count == 0) throw new InvalidDataException("The model has no triangles.");
        List<string> notes = [.. model.Notes];
        SkyVector Game(SkyModelCorner c) => model.UpAxis == SkyModelUpAxis.Y ? new(c.X, -c.Z, c.Y) : new(c.X, c.Y, c.Z);
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var t in model.Triangles)
            foreach (var p in new[] { Game(t.A), Game(t.B), Game(t.C) })
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
            }
        SkyVector centre = new(0, 0, 0);
        if (minX > 0 || minY > 0 || minZ > 0 || maxX < 0 || maxY < 0 || maxZ < 0)
        {
            centre = new((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            notes.Add("The model is not around its origin, so the sky is seen from the middle of the model.");
        }
        var canvas = new SkyCanvas(BaseWidth, BaseWidth / 2);
        foreach (var t in model.Triangles)
        {
            SkyModelCorner a = t.A, b = t.B, c = t.C;
            SkyImage? texture = t.Texture >= 0 && t.Texture < model.Textures.Count ? model.Textures[t.Texture] : null;
            Func<double, double, double, (float, float, float)> shade;
            if (texture != null)
                shade = (wa, wb, wc) =>
                {
                    var (r, g, bl, _) = texture.Sample(a.U * wa + b.U * wb + c.U * wc, a.V * wa + b.V * wb + c.V * wc);
                    return (r, g, bl);
                };
            else if (a.Color is { } ca && b.Color is { } cb && c.Color is { } cc)
                shade = (wa, wb, wc) => ((float)(ca.R * wa + cb.R * wb + cc.R * wc), (float)(ca.G * wa + cb.G * wb + cc.G * wc), (float)(ca.B * wa + cb.B * wb + cc.B * wc));
            else
            {
                Rgba32 m = t.MaterialColor;
                shade = (_, _, _) => (m.R, m.G, m.B);
            }
            canvas.Draw(Game(a) - centre, Game(b) - centre, Game(c) - centre, shade);
        }
        int covered = Enumerable.Range(0, canvas.Width * canvas.Height).Count(canvas.Covered);
        if (covered < canvas.Width * canvas.Height * 98L / 100)
            notes.Add($"The model leaves {100 - covered * 100L / (canvas.Width * canvas.Height)}% of the view open (no floor or gaps); those directions continue the nearest colours.");
        canvas.FillGaps();
        return new SkyPanorama(canvas.Width, canvas.Height, canvas.Color, notes);
    }

    /// <summary>The colour seen in a direction, averaged over about <paramref name="footprint"/> radians around it.</summary>
    internal (float R, float G, float B) Sample(SkyVector direction, double footprint, double turnDegrees)
    {
        var (u, v) = SkyDirections.ToUv(direction, turnDegrees);
        double level = Math.Clamp(Math.Log2(Math.Max(footprint, 1e-9) / (2 * Math.PI / _levels[0].Width)), 0, _levels.Count - 1);
        int l0 = (int)Math.Floor(level), l1 = Math.Min(_levels.Count - 1, l0 + 1);
        float f = (float)(level - l0);
        var a = Bilinear(_levels[l0], u, v);
        if (f == 0 || l1 == l0) return a;
        var b = Bilinear(_levels[l1], u, v);
        return (a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
    }

    private static (float R, float G, float B) Bilinear((int Width, int Height, float[] Rgb) level, double u, double v)
    {
        double x = u * level.Width - 0.5, y = Math.Clamp(v * level.Height - 0.5, 0, level.Height - 1);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y), y1 = Math.Min(level.Height - 1, y0 + 1);
        float fx = (float)(x - x0), fy = (float)(y - y0);
        int xa = ((x0 % level.Width) + level.Width) % level.Width, xb = (xa + 1) % level.Width;
        float L(int k) =>
            (level.Rgb[(y0 * level.Width + xa) * 3 + k] * (1 - fx) + level.Rgb[(y0 * level.Width + xb) * 3 + k] * fx) * (1 - fy) +
            (level.Rgb[(y1 * level.Width + xa) * 3 + k] * (1 - fx) + level.Rgb[(y1 * level.Width + xb) * 3 + k] * fx) * fy;
        return (L(0), L(1), L(2));
    }

    /// <summary>The mean colour below the horizon, each row weighted by the share of the sky it covers.</summary>
    internal (byte R, byte G, byte B) LowerAverage()
    {
        var (width, height, rgb) = LevelNear(64);
        double r = 0, g = 0, b = 0, total = 0;
        for (int y = height / 2; y < height; y++)
        {
            double weight = Math.Cos(Math.PI / 2 - Math.PI * (y + 0.5) / height);
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 3;
                r += rgb[i] * weight; g += rgb[i + 1] * weight; b += rgb[i + 2] * weight; total += weight;
            }
        }
        return (Byte((float)(r / total)), Byte((float)(g / total)), Byte((float)(b / total)));
    }

    /// <summary>The level nearest <paramref name="width"/> pixels wide (for measuring where the sky changes).</summary>
    internal (int Width, int Height, float[] Rgb) LevelNear(int width) => _levels.OrderBy(l => Math.Abs(l.Width - width)).First();

    /// <summary>The panorama as a picture <paramref name="width"/> pixels wide (for showing the source).</summary>
    public SkyImage ToImage(int width)
    {
        int height = width / 2;
        var pixels = new Rgba32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var (r, g, b) = Sample(SkyDirections.FromUv((x + 0.5) / width, (y + 0.5) / height), 2 * Math.PI / width, 0);
                pixels[y * width + x] = new(Byte(r), Byte(g), Byte(b), 255);
            }
        return new SkyImage(width, height, pixels);
    }

    internal static byte Byte(float value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
