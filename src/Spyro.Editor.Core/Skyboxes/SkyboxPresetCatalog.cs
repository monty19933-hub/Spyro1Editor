using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Skyboxes;

public sealed class SkyboxPresetCatalog
{
    public static IReadOnlyList<SkyboxPreset> NativePresets { get; } =
    [
        new("night", "Night", "A cool night gradient that preserves the original sky shading.", ["*"]),
        new("dusk", "Dusk", "A violet and warm dusk gradient that preserves the original sky shading.", ["*"]),
        new("custom", "Custom Colors", "Use the custom gradient colors below.", ["*"])
    ];

    public static IReadOnlyList<OriginalSkyboxPreset> OriginalPresets { get; } =
    [
        new(
            "stormy-spring",
            "Stormy Spring",
            "A cool spring storm split by fresh turquoise light and one warm sunbreak.",
            ["darkhollow", "stonehill", "nightflight"],
            "#10283D #294B5D #477383 #70A8A5 #B9D4B8 #F1D48D",
            "Assets/SkyPresets/stormy-spring.png"),
        new(
            "blazing-desert",
            "Blazing Desert",
            "A white-hot desert horizon under coral, vermilion, amber, and magenta bands.",
            ["drycanyon", "clifftown", "toasty"],
            "#53183F #9D2940 #E34736 #FF7839 #FFB54E #FFF0A6",
            "Assets/SkyPresets/blazing-desert.png"),
        new(
            "aurora-dream",
            "Aurora Dream",
            "A deep indigo night crossed by emerald, cyan, lavender, and rose light ribbons.",
            ["dreamweavers", "loftycastle", "wizardpeak"],
            "#070D32 #16236A #244BB1 #20A6B0 #54DCA4 #B46DE0 #F3B9F0",
            "Assets/SkyPresets/aurora-dream.png")
    ];

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

    public static SkyboxPreset NativeDefault => NativePresets[0];

    public static SkyboxPreset FindNative(string? presetId)
    {
        string normalized = NormalizeNativePresetId(presetId);
        return NativePresets.FirstOrDefault(preset => string.Equals(preset.Id, normalized, StringComparison.OrdinalIgnoreCase))
            ?? NativeDefault;
    }

    public static OriginalSkyboxPreset FindOriginal(string? presetId)
    {
        return TryFindOriginal(presetId, out OriginalSkyboxPreset? preset)
            ? preset!
            : OriginalPresets[0];
    }

    public static bool TryFindOriginal(string? presetId, out OriginalSkyboxPreset? preset)
    {
        string value = (presetId ?? "").Trim();
        preset = OriginalPresets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, value, StringComparison.OrdinalIgnoreCase));
        return preset != null;
    }

    public static string NormalizeNativePresetId(string? presetId)
    {
        string value = (presetId ?? "").Trim();
        if (TryFindOriginal(value, out OriginalSkyboxPreset? original))
            return original!.Id;
        if (value.Contains("custom", StringComparison.OrdinalIgnoreCase))
            return "custom";
        if (value.Contains("dusk", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("sunset", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("darkhollow", StringComparison.OrdinalIgnoreCase))
        {
            return "dusk";
        }
        return "night";
    }
}

public sealed record OriginalSkyboxPreset(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<string> DonorLevelKeys,
    string PaletteHex,
    string PreviewAssetPath)
{
    public LevelDefinition ResolveDonor(LevelCatalog catalog, string targetLevelKey)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        return DonorLevelKeys
            .Select(catalog.FindByKey)
            .FirstOrDefault(level => level != null &&
                !string.Equals(LevelCatalog.NormalizeKey(level.Key), target, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{DisplayName} has no available source geometry for this level.");
    }

    public override string ToString() => DisplayName;
}

public sealed record SkyboxPreset(string Id, string DisplayName, string Description, IReadOnlyList<string> SupportedLevelKeys)
{
    public bool SupportsLevel(string levelKey)
    {
        string normalized = LevelCatalog.NormalizeKey(levelKey);
        return SupportedLevelKeys.Any(key => key == "*" || string.Equals(LevelCatalog.NormalizeKey(key), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => DisplayName;
}
