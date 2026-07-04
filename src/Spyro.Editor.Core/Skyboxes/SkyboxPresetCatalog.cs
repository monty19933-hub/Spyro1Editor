using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Skyboxes;

public sealed class SkyboxPresetCatalog
{
    public static IReadOnlyList<SkyboxPreset> Presets { get; } =
    [
        new("StoneHillNightKeeper", "Stone Hill Night Keeper", "Current best safe Stone Hill night preset.", ["stonehill"]),
        new("StoneHillStableNight", "Stone Hill Stable Night", "Conservative stable night recolor for Stone Hill.", ["stonehill"]),
        new("StoneHillBestNight", "Stone Hill Best Night", "Earlier tested Stone Hill night palette.", ["stonehill"]),
        new("StoneHillNight", "Stone Hill Night", "Basic brightness-preserving night grade.", ["stonehill"]),
        new("StoneHillMoonAtmosphere", "Stone Hill Moon Atmosphere", "Experimental moonlit Stone Hill palette.", ["stonehill"]),
        new("DarkHollowMixed", "Dark Hollow Mixed", "Dark Hollow inspired sky color transfer.", ["stonehill", "darkhollow"]),
        new("Custom", "Custom Palette", "Use the custom colors below.", ["stonehill"])
    ];

    public static SkyboxPreset DefaultForLevel(string levelKey)
    {
        return Presets.FirstOrDefault(preset => preset.SupportsLevel(levelKey))
            ?? Presets[0];
    }
}

public sealed record SkyboxPreset(string Id, string DisplayName, string Description, IReadOnlyList<string> SupportedLevelKeys)
{
    public bool SupportsLevel(string levelKey)
    {
        string normalized = LevelCatalog.NormalizeKey(levelKey);
        return SupportedLevelKeys.Any(key => string.Equals(LevelCatalog.NormalizeKey(key), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => DisplayName;
}
