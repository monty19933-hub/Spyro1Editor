namespace Spyro.Editor.Core.Primitives;

public readonly record struct ColorRgba(byte R, byte G, byte B, byte A = 255)
{
    public static ColorRgba FromArgb(int a, int r, int g, int b)
    {
        return new ColorRgba(Clamp(r), Clamp(g), Clamp(b), Clamp(a));
    }

    public static ColorRgba FromRgb(int r, int g, int b)
    {
        return new ColorRgba(Clamp(r), Clamp(g), Clamp(b));
    }

    public static bool TryParseHex(string? text, out ColorRgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
            text = text[1..];

        if (text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            return false;

        color = FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static byte Clamp(int value)
    {
        return (byte)Math.Max(0, Math.Min(255, value));
    }
}
