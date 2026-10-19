using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Generation;

/// <summary>Whether a generated level's textures fit the built level's texture memory, decided as Create BIN decides it
/// (<see cref="NativeAuthoredPictureVram"/>, with the replaced level's dragon rescue scenes for a level that takes a game
/// level's place). Read-only.</summary>
public static class RandomLevelTextureCheck
{
    /// <summary>Texture memory kept free for the objects the user adds afterwards.</summary>
    public const int ObjectReserve = 96 * 1024;

    /// <summary>Null when the materials fit with <see cref="ObjectReserve"/> left; otherwise why not.</summary>
    public static async Task<string?> CheckAsync(string spyroDisc, LevelCatalog catalog, IReadOnlyList<AuthoredMaterial> materials,
        string? replacedLevelKey, CancellationToken token = default)
    {
        if (materials.Count == 0) return null;
        byte[]? replacedVram = null;
        if (replacedLevelKey != null)
        {
            var lib = new UniversalSourceLibrary(spyroDisc, catalog);
            replacedVram = lib.Get(replacedLevelKey).Data.Vram().ToArray();
        }
        var vram = await NativeAuthoredPictureVram.CaptureAsync(spyroDisc, materials[0], replacedVram, token);
        var (fits, free, reason) = vram.Test(materials);
        if (!fits) return reason ?? "the textures do not fit the level's texture memory";
        return free < ObjectReserve ? $"only {free:N0} bytes of texture memory would be left for objects" : null;
    }
}
