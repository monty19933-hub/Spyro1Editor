using System.Globalization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

public static class TerrainPaletteImporter
{
    private static readonly Regex HexColorPattern = new(@"(?<![0-9A-Fa-f])#?[0-9A-Fa-f]{6}(?![0-9A-Fa-f])", RegexOptions.Compiled);
    private static readonly Regex RgbLinePattern = new(@"^\s*(\d{1,3})\s+(\d{1,3})\s+(\d{1,3})(?:\s|$)", RegexOptions.Compiled);

    public static async Task<TerrainPaletteImport> ImportFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Palette file was not found.", path);

        string name = Path.GetFileNameWithoutExtension(path);
        if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            return ImportPng(path, name);

        string text = await File.ReadAllTextAsync(path, cancellationToken);
        return ImportText(text, name);
    }

    public static TerrainPaletteImport ImportText(string text, string name = "Imported palette")
    {
        List<ColorRgba> colors = ExtractColors(text).ToList();
        if (colors.Count < 2)
            throw new InvalidOperationException("Palette import needs at least two colors. Use #RRGGBB values, GIMP-style RGB rows, or a PNG palette image.");

        List<ColorRgba> ramp = colors.OrderBy(Luminance).ToList();
        ColorRgba low = ramp.First();
        ColorRgba high = ramp.Last();
        return new TerrainPaletteImport(
            string.IsNullOrWhiteSpace(name) ? "Imported palette" : name.Trim(),
            low,
            high,
            colors.Count,
            NormalizeHex(low),
            NormalizeHex(high),
            ramp);
    }

    public static TerrainPaletteImport ImportPng(string path, string name = "Imported image palette")
    {
        Rgba32[] pixels = PngRgbaImage.ReadRgba(path, out _, out _);
        List<ColorRgba> colors = ExtractImageColors(pixels).ToList();
        if (colors.Count < 2)
            throw new InvalidOperationException("PNG palette import needs at least two visible colors.");

        List<ColorRgba> colorCandidates = colors
            .Where(color => Chroma(color) >= 18 && Luminance(color) <= 248)
            .ToList();
        if (colorCandidates.Count < 2)
            colorCandidates = colors;

        List<ColorRgba> ramp = colorCandidates.OrderBy(Luminance).ToList();
        ColorRgba low = ramp.First();
        ColorRgba high = ramp.Last();
        return new TerrainPaletteImport(
            string.IsNullOrWhiteSpace(name) ? "Imported image palette" : name.Trim(),
            low,
            high,
            colors.Count,
            NormalizeHex(low),
            NormalizeHex(high),
            ramp);
    }

    private static IEnumerable<ColorRgba> ExtractColors(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in HexColorPattern.Matches(text))
        {
            if (!ColorRgba.TryParseHex(match.Value, out ColorRgba color))
                continue;

            string key = NormalizeHex(color);
            if (seen.Add(key))
                yield return color;
        }

        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            Match match = RgbLinePattern.Match(trimmed);
            if (!match.Success ||
                !TryParseByte(match.Groups[1].Value, out byte r) ||
                !TryParseByte(match.Groups[2].Value, out byte g) ||
                !TryParseByte(match.Groups[3].Value, out byte b))
            {
                continue;
            }

            ColorRgba color = ColorRgba.FromRgb(r, g, b);
            string key = NormalizeHex(color);
            if (seen.Add(key))
                yield return color;
        }
    }

    private static bool TryParseByte(string text, out byte value)
    {
        value = 0;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
            parsed is >= 0 and <= 255 &&
            byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static IEnumerable<ColorRgba> ExtractImageColors(IEnumerable<Rgba32> pixels)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Rgba32 pixel in pixels)
        {
            if (pixel.A < 32)
                continue;

            ColorRgba color = ColorRgba.FromRgb(Quantize(pixel.R), Quantize(pixel.G), Quantize(pixel.B));
            string key = NormalizeHex(color);
            if (seen.Add(key))
                yield return color;
        }
    }

    private static byte Quantize(byte value)
    {
        return (byte)Math.Clamp((int)Math.Round(value / 8.0) * 8, 0, 255);
    }

    private static double Luminance(ColorRgba color)
    {
        return (color.R * 0.2126) + (color.G * 0.7152) + (color.B * 0.0722);
    }

    private static int Chroma(ColorRgba color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max - min;
    }

    private static string NormalizeHex(ColorRgba color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}

public sealed record TerrainPaletteImport(
    string DisplayName,
    ColorRgba Low,
    ColorRgba High,
    int ColorCount,
    string LowHex,
    string HighHex,
    IReadOnlyList<ColorRgba> Colors);
