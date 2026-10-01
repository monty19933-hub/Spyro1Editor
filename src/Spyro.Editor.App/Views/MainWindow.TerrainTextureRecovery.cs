using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private LevelDefinition? _retailTerrainTextureWarningLevel;
    private string _retailTerrainTextureWarningRoot = "";
    private string? _retailTerrainTextureWarning;

    private string AppendRetailTerrainTextureWarning(string status)
    {
        if (_retailTerrainTextureWarning == null ||
            !ReferenceEquals(_currentLevel, _retailTerrainTextureWarningLevel) ||
            !PathsEqual(_workspace.RootPath, _retailTerrainTextureWarningRoot) ||
            status.Contains(_retailTerrainTextureWarning, StringComparison.Ordinal))
            return status;
        return status + " " + _retailTerrainTextureWarning;
    }

    private void RememberRetailTerrainTextureWarning(LevelDefinition level, string root, string? warning)
    {
        _retailTerrainTextureWarning = warning;
        _retailTerrainTextureWarningLevel = warning == null ? null : level;
        _retailTerrainTextureWarningRoot = warning == null ? "" : root;
    }

    // Call after loading/recovering ordinary source geometry, before committing
    // it to the viewport. The locked ID65 lab retains its existing loader fence.
    private sealed record RetailTerrainTextureLoadPreparation(bool OwnerCurrent, string? Warning = null);

    private async Task<RetailTerrainTextureLoadPreparation> EnsureRetailTerrainTexturesForLoadAsync(LevelDefinition level)
    {
        if (IsId65BlankLabKey(level.Key) || level.LevelId == 65)
            return new(true);
        var captured = CaptureSourceDiscOperationIdentity();
        bool allowOwnedPortableBuild = _buildingPortableCache;
        string root = captured.WorkspaceRoot;
        string source = ResolveTerrainTextureSourceImage(level);
        string overlay = FindCachedTerrainOverlayPath(level.Key) ?? "";
        bool Current() => IsCurrentSourceDiscOperationIdentity(captured,
            allowPortableCacheBuild: allowOwnedPortableBuild) &&
            string.Equals(source, ResolveTerrainTextureSourceImage(level), StringComparison.OrdinalIgnoreCase);
        if (!Current()) return new(false);
        if (PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(root, level.Key))
        {
            bool ownerCurrent = Current();
            if (ownerCurrent) RememberRetailTerrainTextureWarning(level, root, null);
            return new(ownerCurrent);
        }
        string? warning = null;
        try
        {
            _statusText.Text = $"Preparing {level.DisplayName}'s original terrain textures...";
            await Task.Run(() => NativeTerrainTextureCacheRecovery.EnsureAsync(root, source, overlay, level));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Offline geometry editing remains supported. Make the missing
            // materials explicit rather than calling flat fallback normal.
            warning = "Original terrain textures unavailable: " + ex.Message;
        }
        if (!Current()) return new(false);
        _terrainTexturePreviewBundleCache.Clear();
        RememberRetailTerrainTextureWarning(level, root, warning);
        return new(true, warning);
    }
}
