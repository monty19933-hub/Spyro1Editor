using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>A decoded picture (rows top to bottom, RGBA): a 360° panorama or a model's texture.</summary>
public sealed class SkyImage
{
    public SkyImage(int width, int height, Rgba32[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
            throw new InvalidDataException("The picture's size does not match its pixels.");
        Width = width; Height = height; Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public Rgba32[] Pixels { get; }

    /// <summary>The colour at (u, v), 0-1 each, v down from the top row; bilinear, wrapping in u and clamping in v.</summary>
    public (float R, float G, float B, float A) Sample(double u, double v)
    {
        double x = (u - Math.Floor(u)) * Width - 0.5, y = Math.Clamp(v, 0, 1) * Height - 0.5;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = (float)(x - x0), fy = (float)(y - y0);
        Rgba32 Px(int px, int py) => Pixels[Math.Clamp(py, 0, Height - 1) * Width + ((px % Width) + Width) % Width];
        Rgba32 a = Px(x0, y0), b = Px(x0 + 1, y0), c = Px(x0, y0 + 1), d = Px(x0 + 1, y0 + 1);
        float L(byte p, byte q, byte r, byte s) => (p * (1 - fx) + q * fx) * (1 - fy) + (r * (1 - fx) + s * fx) * fy;
        return (L(a.R, b.R, c.R, d.R), L(a.G, b.G, c.G, d.G), L(a.B, b.B, c.B, d.B), L(a.A, b.A, c.A, d.A));
    }
}

/// <summary>Turns an image file's bytes into a picture (PNG, JPEG…). The app supplies one that reads every common format;
/// <paramref name="maxWidth"/> lets it decode a very large picture at a smaller size.</summary>
public delegate SkyImage SkyImageDecoder(byte[] bytes, int maxWidth);

/// <summary>Which of the file's axes points up. Models are turned so it becomes the game's up (+Z), keeping their
/// handedness: Y-up files (OBJ's usual axes) map x → X, -z → Y, y → Z.</summary>
public enum SkyModelUpAxis { Y, Z }

/// <summary>One corner of a model triangle, in the file's own axes and units.</summary>
public readonly record struct SkyModelCorner(double X, double Y, double Z, double U, double V, Rgba32? Color);

/// <summary>A model triangle: three corners and the picture it is painted with (an index into
/// <see cref="SkyModel.Textures"/>, or -1 for none: then the corners' colours, else the material colour).</summary>
public readonly record struct SkyModelTriangle(SkyModelCorner A, SkyModelCorner B, SkyModelCorner C, int Texture, Rgba32 MaterialColor);

/// <summary>A model read from an OBJ or FBX file for a sky: triangles in the file's axes (already placed by the file's
/// own object transforms), the pictures they use, and the file's up axis. The viewer stands at the origin.</summary>
public sealed record SkyModel(IReadOnlyList<SkyModelTriangle> Triangles, IReadOnlyList<SkyImage> Textures, SkyModelUpAxis UpAxis, IReadOnlyList<string> Notes);
