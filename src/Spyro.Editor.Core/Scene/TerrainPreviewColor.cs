using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class TerrainPreviewColor
{
    public static ColorRgba ApplyHeightTint(ColorRgba source, double normalizedHeight)
    {
        double shade = Math.Clamp((normalizedHeight - 0.5) * 0.12, -0.06, 0.06);
        return shade >= 0
            ? Blend(source, ColorRgba.FromArgb(source.A, 255, 255, 255), shade)
            : Blend(source, ColorRgba.FromArgb(source.A, 0, 0, 0), -shade);
    }

    private static ColorRgba Blend(ColorRgba from, ColorRgba to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return ColorRgba.FromArgb(
            (int)Math.Round(from.A + ((to.A - from.A) * amount)),
            (int)Math.Round(from.R + ((to.R - from.R) * amount)),
            (int)Math.Round(from.G + ((to.G - from.G) * amount)),
            (int)Math.Round(from.B + ((to.B - from.B) * amount)));
    }
}
