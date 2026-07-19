using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

internal static class MobyGemIconCatalog
{
    private const double MinimumIconHeight = 24;
    private const double MaximumIconHeight = 58;

    internal static IReadOnlyList<MobyGemIconAsset> Assets { get; } =
    [
        new(GemValue.Red, "gem-red.png", "34d6f350ecc0a6adf8892a47b0864ebc40eba7bb598786e18b592abeabbb629b"),
        new(GemValue.Green, "gem-green.png", "0d23f02fda93626fd48791955d06e9b7c5c942f560805251c6cf98a0fd5ea867"),
        new(GemValue.Blue, "gem-blue.png", "6ce0f7fe9eed673e6b1fc886410e8100e05958b38beb48160ff6d452e0ce74dc"),
        new(GemValue.Yellow, "gem-yellow.png", "6ae19b7aed5e6028dcb1e64f6445edf55e9a6b1641bc4331866b5904802c74c3"),
        new(GemValue.Purple, "gem-purple.png", "49ceadac27cd587c673c3a6e4d5ac23050844c5356f4c4abb3a78ae1d2ec6b86")
    ];

    private static readonly IReadOnlyDictionary<int, MobyGemIconAsset> AssetsByValue =
        Assets.ToDictionary(asset => asset.Gem.Value);

    internal static bool TryDraw(
        DrawingContext context,
        Point point,
        double markerSize,
        Pen selectionRim,
        GemValue gem)
    {
        if (!AssetsByValue.TryGetValue(gem.Value, out MobyGemIconAsset? asset))
            return false;

        Bitmap? bitmap = asset.Bitmap.Value;
        if (bitmap == null)
            return false;

        double iconHeight = IconHeight(markerSize);
        double sourceHeight = Math.Max(1, bitmap.PixelSize.Height);
        double iconWidth = iconHeight * Math.Max(1, bitmap.PixelSize.Width) / sourceHeight;
        var destination = new Rect(
            point.X - (iconWidth * 0.5),
            point.Y - (iconHeight * 0.5),
            iconWidth,
            iconHeight);
        context.DrawImage(bitmap, destination);

        if (selectionRim.Thickness > 1.8)
        {
            context.DrawEllipse(
                null,
                selectionRim,
                point,
                Math.Max(markerSize * 1.05, iconWidth * 0.46),
                Math.Max(markerSize * 1.05, iconHeight * 0.46));
        }

        return true;
    }

    internal static double HitRadius(double markerSize, GemValue gem)
    {
        return AssetsByValue.ContainsKey(gem.Value)
            ? IconHeight(markerSize) * 0.5
            : 12;
    }

    private static double IconHeight(double markerSize)
    {
        return Math.Clamp(markerSize * 3.6, MinimumIconHeight, MaximumIconHeight);
    }

    private static Bitmap? LoadBitmap(string fileName)
    {
        foreach (string candidatePath in CandidatePaths(fileName))
        {
            try
            {
                if (File.Exists(candidatePath))
                    return new Bitmap(candidatePath);
            }
            catch
            {
                // The procedural marker remains available when an asset cannot load.
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidatePaths(string fileName)
    {
        string baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, "Assets", "MobyIcons", fileName);
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "../../../Assets/MobyIcons", fileName));
    }

    internal sealed class MobyGemIconAsset
    {
        internal MobyGemIconAsset(GemValue gem, string fileName, string sourceSha256)
        {
            Gem = gem;
            FileName = fileName;
            SourceSha256 = sourceSha256;
            Bitmap = new Lazy<Bitmap?>(() => LoadBitmap(fileName));
        }

        internal GemValue Gem { get; }
        internal string FileName { get; }
        internal string SourceSha256 { get; }
        internal Lazy<Bitmap?> Bitmap { get; }
    }
}
