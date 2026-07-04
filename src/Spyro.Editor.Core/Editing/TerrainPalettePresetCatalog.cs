using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

public static class TerrainPalettePresetCatalog
{
    public static IReadOnlyList<TerrainPalettePreset> Presets { get; } =
    [
        new("water-clear", "Clear water", "water", ColorRgba.FromRgb(20, 76, 142), ColorRgba.FromRgb(119, 194, 245)),
        new("water-deep", "Deep water", "water", ColorRgba.FromRgb(8, 36, 91), ColorRgba.FromRgb(56, 126, 202)),
        new("lava-hot", "Hot lava", "lava", ColorRgba.FromRgb(108, 18, 12), ColorRgba.FromRgb(255, 128, 36)),
        new("ooze-toxic", "Toxic ooze", "ooze", ColorRgba.FromRgb(18, 57, 18), ColorRgba.FromRgb(110, 182, 63)),
        new("grass-bright", "Bright grass", "grass", ColorRgba.FromRgb(42, 104, 46), ColorRgba.FromRgb(126, 188, 86)),
        new("stone-cool", "Cool stone", "stone", ColorRgba.FromRgb(64, 70, 80), ColorRgba.FromRgb(158, 164, 172)),
        new("cliff-tan", "Tan cliff", "cliff", ColorRgba.FromRgb(91, 84, 60), ColorRgba.FromRgb(166, 154, 108)),
        new("brick-warm", "Warm brick", "brick", ColorRgba.FromRgb(91, 48, 34), ColorRgba.FromRgb(183, 103, 65)),
        new("sand-warm", "Warm sand", "sand", ColorRgba.FromRgb(142, 108, 48), ColorRgba.FromRgb(230, 199, 115)),
        new("ground-dry", "Dry ground", "ground", ColorRgba.FromRgb(92, 64, 38), ColorRgba.FromRgb(174, 132, 75)),
        new("ice-clear", "Clear ice", "ice", ColorRgba.FromRgb(62, 105, 143), ColorRgba.FromRgb(174, 222, 244))
    ];

    public static TerrainPalettePreset DefaultForSurface(string surface)
    {
        surface = (surface ?? "").Trim().ToLowerInvariant();
        return Presets.FirstOrDefault(preset => string.Equals(preset.Surface, surface, StringComparison.OrdinalIgnoreCase))
            ?? Presets[0];
    }
}

public sealed record TerrainPalettePreset(
    string Id,
    string DisplayName,
    string Surface,
    ColorRgba Low,
    ColorRgba High)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
