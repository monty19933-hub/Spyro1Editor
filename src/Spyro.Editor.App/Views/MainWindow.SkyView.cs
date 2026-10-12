using System.Security.Cryptography;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Sky;

namespace Spyro.Editor.App.Views;

/// <summary>The 3D view shows the sky the game will draw in the open level (its original, a recoloured or borrowed one, or
/// one imported), refreshed when a level opens and whenever its sky changes.</summary>
public sealed partial class MainWindow
{
    private int _viewportSkyRevision;
    private static readonly Dictionary<string, SkyImage> ViewportSkyCache = new(StringComparer.Ordinal);

    /// <summary>The latest sky refresh, for tests.</summary>
    internal Task? ViewportSkyTaskForTesting { get; private set; }

    private void RefreshViewportSky() => ViewportSkyTaskForTesting = RefreshViewportSkyAsync();

    private async Task RefreshViewportSkyAsync()
    {
        int request = ++_viewportSkyRevision;
        var level = _currentLevel; var scene = _freshSceneDocument; string root = _workspace.RootPath;
        if (level == null) { _viewport.SetSkyPanorama(null); return; }
        string disc;
        try { disc = ResolveUniversalSourceDisc(); }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { disc = ""; }
        SkyImage? panorama = null;
        try
        {
            panorama = await Task.Run(() =>
            {
                bool haveDisc = disc.Length > 0 && File.Exists(disc);
                byte[]? block = haveDisc || scene?.CustomSky != null ? LevelSkyResolver.Resolve(disc, root, _retailCatalog, level, scene) : null;
                if (block == null) return null;
                string key = Convert.ToHexString(SHA256.HashData(block));
                lock (ViewportSkyCache)
                    if (ViewportSkyCache.TryGetValue(key, out SkyImage? known)) return known;
                SkyImage made = NativeSkyPreview.Render(block, 2048);
                lock (ViewportSkyCache)
                {
                    if (ViewportSkyCache.Count >= 8) ViewportSkyCache.Clear();
                    ViewportSkyCache[key] = made;
                }
                return made;
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            // The view keeps its plain backdrop; the level and its export are unaffected.
            EditorDiagnostics.RecordWarning("Level sky in the 3D view", e.Message);
        }
        if (request != _viewportSkyRevision) return;
        _viewport.SetSkyPanorama(panorama);
    }
}
