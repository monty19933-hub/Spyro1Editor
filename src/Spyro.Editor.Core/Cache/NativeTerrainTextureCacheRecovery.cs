using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Cache;

public sealed record NativeTerrainTextureCacheRecoveryResult(bool Rebuilt, int TextureCount);

/// <summary>Repairs missing or invalid viewport materials without changing editable geometry.</summary>
public static class NativeTerrainTextureCacheRecovery
{
    public static async Task<NativeTerrainTextureCacheRecoveryResult> EnsureAsync(
        string workspaceRoot, string sourceImage, string sourceOverlay, LevelDefinition level,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(level);
        cancellationToken.ThrowIfCancellationRequested();
        if (PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(workspaceRoot, level.Key) &&
            PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(workspaceRoot, level.Key, out var existing) &&
            existing != null)
            return new(false, existing.TextureCount);

        if (string.IsNullOrWhiteSpace(sourceImage) || !File.Exists(sourceImage))
            throw new InvalidDataException($"{level.DisplayName}'s original terrain textures are unavailable. Select the source Spyro BIN/CUE to rebuild its texture cache.");
        if (string.IsNullOrWhiteSpace(sourceOverlay) || !File.Exists(sourceOverlay))
            throw new InvalidDataException($"{level.DisplayName}'s source terrain map is unavailable, so its original texture cache could not be prepared.");

        int count = await PortableEditorCacheBuilder.BuildTerrainTexturePreviewCacheFromSourceAsync(
            sourceImage, sourceOverlay, workspaceRoot, level, overwrite: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (count <= 0 || !PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(workspaceRoot, level.Key))
            throw new InvalidDataException($"{level.DisplayName}'s original terrain texture cache failed validation. The level was not presented as a flat texture substitute.");
        return new(true, count);
    }
}
