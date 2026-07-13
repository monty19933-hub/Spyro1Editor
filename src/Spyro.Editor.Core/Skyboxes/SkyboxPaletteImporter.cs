using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Skyboxes;

public sealed record SkyboxImagePalette(
    string SourceName,
    int SourceColorCount,
    IReadOnlyList<string> GradientColors)
{
    public string PaletteText => string.Join(' ', GradientColors);
}

public static class SkyboxPaletteImporter
{
    public static SkyboxImagePalette ImportPng(string path, int gradientStopCount = 6)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Sky color image was not found.", path);

        gradientStopCount = Math.Clamp(gradientStopCount, 2, 8);
        Rgba32[] pixels = PngRgbaImage.ReadRgba(path, out _, out _);
        ColorRgba[] colors = pixels
            .Where(pixel => pixel.A >= 32)
            .Select(pixel => ColorRgba.FromRgb(Quantize(pixel.R), Quantize(pixel.G), Quantize(pixel.B)))
            .Distinct()
            .OrderBy(Luminance)
            .ToArray();
        if (colors.Length < 2)
            throw new InvalidOperationException("Sky color PNG needs at least two visible colors.");

        int stopCount = Math.Min(gradientStopCount, colors.Length);
        List<ColorRgba> stops = new(stopCount);
        for (int index = 0; index < stopCount; index++)
        {
            int sourceIndex = stopCount == 1
                ? 0
                : (int)Math.Round(index * (colors.Length - 1) / (double)(stopCount - 1));
            ColorRgba color = colors[sourceIndex];
            if (!stops.Contains(color))
                stops.Add(color);
        }
        if (stops.Count < 2)
            throw new InvalidOperationException("Sky color PNG did not produce a usable gradient.");

        return new SkyboxImagePalette(
            SourceName: Path.GetFileName(path),
            SourceColorCount: colors.Length,
            GradientColors: stops.Select(ToHex).ToArray());
    }

    private static byte Quantize(byte value) => (byte)Math.Clamp((int)Math.Round(value / 8.0) * 8, 0, 255);

    private static double Luminance(ColorRgba color) =>
        (color.R * 0.2126) + (color.G * 0.7152) + (color.B * 0.0722);

    private static string ToHex(ColorRgba color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
