using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class CustomTerrainTexturePreview
{
    public static int Apply(GeometryCandidate? geometry, IReadOnlyList<CustomTerrainTextureImport> imports)
    {
        if (geometry == null || imports.Count == 0)
            return 0;

        Dictionary<int, ColorRgba> paletteColors = imports
            .Where(import => TryGetPreviewColor(import, out _))
            .GroupBy(import => import.TextureId)
            .ToDictionary(group => group.Key, group =>
            {
                TryGetPreviewColor(group.First(), out ColorRgba color);
                return color;
            });
        if (paletteColors.Count == 0)
            return 0;

        int applied = 0;
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (!paletteColors.TryGetValue(polygon.TextureId, out ColorRgba color))
                continue;

            polygon.SetSurfacePreviewColor(color, "custom palette");
            applied++;
        }

        return applied;
    }

    public static bool TryGetPreviewColor(CustomTerrainTextureImport import, out ColorRgba color)
    {
        color = default;
        if (TryGetPaletteRampPreviewColor(import, out color))
            return true;

        if (!ColorRgba.TryParseHex(import.PaletteLowHex, out ColorRgba low) ||
            !ColorRgba.TryParseHex(import.PaletteHighHex, out ColorRgba high))
        {
            return false;
        }

        color = ColorRgba.FromRgb(
            (low.R + high.R) / 2,
            (low.G + high.G) / 2,
            (low.B + high.B) / 2);
        return true;
    }

    private static bool TryGetPaletteRampPreviewColor(CustomTerrainTextureImport import, out ColorRgba color)
    {
        color = default;
        if (import.PaletteHexColors == null || import.PaletteHexColors.Count == 0)
            return false;

        List<ColorRgba> colors = new();
        foreach (string hex in import.PaletteHexColors)
        {
            if (ColorRgba.TryParseHex(hex, out ColorRgba parsed))
                colors.Add(parsed);
        }

        if (colors.Count == 0)
            return false;

        if (colors.Count % 2 == 1)
        {
            color = colors[colors.Count / 2];
            return true;
        }

        ColorRgba a = colors[(colors.Count / 2) - 1];
        ColorRgba b = colors[colors.Count / 2];
        color = ColorRgba.FromRgb(
            (a.R + b.R) / 2,
            (a.G + b.G) / 2,
            (a.B + b.B) / 2);
        return true;
    }
}
