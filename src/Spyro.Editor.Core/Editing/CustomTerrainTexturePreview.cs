using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class CustomTerrainTexturePreview
{
    private const int MaximumCachedPreviewColors = 512;
    private static readonly object PreviewColorCacheGate = new();
    private static readonly Dictionary<string, PreviewColorCacheEntry> PreviewColorCache =
        new(StringComparer.OrdinalIgnoreCase);

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
        bool imageFirst = string.Equals(import.SourceKind, "imported-image", StringComparison.OrdinalIgnoreCase) ||
            import.SourceKind.Contains("texture-art", StringComparison.OrdinalIgnoreCase) ||
            import.SourceKind.Contains("custom-art", StringComparison.OrdinalIgnoreCase);
        if (imageFirst && TryGetImagePreviewColor(import.SourceImagePath, out color))
            return true;
        if (TryGetPaletteRampPreviewColor(import, out color))
            return true;

        if (!ColorRgba.TryParseHex(import.PaletteLowHex, out ColorRgba low) ||
            !ColorRgba.TryParseHex(import.PaletteHighHex, out ColorRgba high))
        {
            return TryGetImagePreviewColor(import.SourceImagePath, out color);
        }

        color = ColorRgba.FromRgb(
            (low.R + high.R) / 2,
            (low.G + high.G) / 2,
            (low.B + high.B) / 2);
        return true;
    }

    public static int ApplyNativeRelocations(
        GeometryCandidate? geometry,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocations)
    {
        if (geometry == null || relocations.Count == 0)
            return 0;

        Dictionary<int, ColorRgba> previewColors = relocations
            .Where(relocation => TryGetImagePreviewColor(relocation.PreviewImagePath, out _))
            .GroupBy(relocation => relocation.TargetTextureId)
            .ToDictionary(group => group.Key, group =>
            {
                TryGetImagePreviewColor(group.First().PreviewImagePath, out ColorRgba color);
                return color;
            });
        int applied = 0;
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (!previewColors.TryGetValue(polygon.TextureId, out ColorRgba color))
                continue;

            polygon.SetSurfacePreviewColor(color, "native cross-level texture preview");
            applied++;
        }
        return applied;
    }

    private static bool TryGetImagePreviewColor(string path, out ColorRgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            string fullPath = Path.GetFullPath(path);
            FileInfo info = new(fullPath);
            lock (PreviewColorCacheGate)
            {
                if (PreviewColorCache.TryGetValue(fullPath, out PreviewColorCacheEntry? cached) &&
                    cached.Length == info.Length &&
                    cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks)
                {
                    color = cached.Color;
                    return cached.Found;
                }
            }

            Rgba32[] pixels = PngRgbaImage.ReadRgba(fullPath, out _, out _);
            Rgba32[] visible = pixels.Where(pixel => pixel.A >= 32).ToArray();
            IReadOnlyList<Rgba32> sample = visible.Length > 0 ? visible : pixels;
            if (sample.Count == 0)
            {
                CachePreviewColor(fullPath, info, found: false, default);
                return false;
            }

            long red = 0;
            long green = 0;
            long blue = 0;
            foreach (Rgba32 pixel in sample)
            {
                red += pixel.R;
                green += pixel.G;
                blue += pixel.B;
            }
            color = ColorRgba.FromRgb(
                (int)(red / sample.Count),
                (int)(green / sample.Count),
                (int)(blue / sample.Count));
            CachePreviewColor(fullPath, info, found: true, color);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void CachePreviewColor(
        string fullPath,
        FileInfo info,
        bool found,
        ColorRgba color)
    {
        lock (PreviewColorCacheGate)
        {
            if (PreviewColorCache.Count >= MaximumCachedPreviewColors &&
                !PreviewColorCache.ContainsKey(fullPath))
            {
                PreviewColorCache.Clear();
            }
            PreviewColorCache[fullPath] = new PreviewColorCacheEntry(
                info.Length,
                info.LastWriteTimeUtc.Ticks,
                found,
                color);
        }
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

    private sealed record PreviewColorCacheEntry(
        long Length,
        long LastWriteTimeUtcTicks,
        bool Found,
        ColorRgba Color);
}
