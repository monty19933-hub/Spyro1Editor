namespace Spyro.Editor.Core.Scene;

public sealed record HomeworldPortalControlDefinition(
    string HomeworldKey,
    int DestinationLevelId,
    string DestinationName,
    int PathTrueIndex,
    int LetteringTrueIndex,
    int CompanionTrueIndex)
{
    public IReadOnlyList<int> TrueIndexes => [PathTrueIndex, LetteringTrueIndex, CompanionTrueIndex];
}

public static class HomeworldPortalControlCatalog
{
    public static IReadOnlyList<HomeworldPortalControlDefinition> All { get; } =
    [
        new("artisans", 11, "Stone Hill", 38, 144, 157),
        new("artisans", 12, "Dark Hollow", 41, 147, 155),
        new("artisans", 13, "Town Square", 39, 145, 158),
        new("artisans", 14, "Toasty", 40, 146, 159),
        new("artisans", 15, "Sunny Flight", 141, 148, 156),

        new("peacekeepers", 21, "Dry Canyon", 47, 108, 54),
        new("peacekeepers", 22, "Cliff Town", 48, 106, 111),
        new("peacekeepers", 23, "Ice Cavern", 51, 107, 112),
        new("peacekeepers", 24, "Doctor Shemp", 50, 109, 55),
        new("peacekeepers", 25, "Night Flight", 49, 110, 56),

        new("magiccrafters", 31, "Alpine Ridge", 84, 96, 123),
        new("magiccrafters", 32, "High Caves", 85, 97, 119),
        new("magiccrafters", 33, "Wizard Peak", 88, 100, 120),
        new("magiccrafters", 34, "Blowhard", 87, 99, 121),
        new("magiccrafters", 35, "Crystal Flight", 86, 98, 122),

        new("beastmakers", 41, "Terrace Village", 74, 73, 75),
        new("beastmakers", 42, "Misty Bog", 77, 76, 78),
        new("beastmakers", 43, "Tree Tops", 177, 178, 179),
        new("beastmakers", 44, "Metalhead", 181, 180, 182),
        new("beastmakers", 45, "Wild Flight", 183, 184, 185),

        new("dreamweavers", 51, "Dark Passage", 34, 35, 36),
        new("dreamweavers", 52, "Lofty Castle", 37, 38, 42),
        new("dreamweavers", 53, "Haunted Towers", 43, 44, 45),
        new("dreamweavers", 54, "Jacques", 39, 40, 41),
        new("dreamweavers", 55, "Icy Flight", 48, 49, 50),

        new("gnastysworld", 61, "Gnorc Cove", 4, 5, 6),
        new("gnastysworld", 62, "Twilight Harbor", 7, 8, 9),
        new("gnastysworld", 63, "Gnasty Gnorc", 10, 11, 12),
        new("gnastysworld", 64, "Gnasty's Loot", 13, 14, 15)
    ];

    public static IReadOnlyList<HomeworldPortalControlDefinition> ForLevel(string levelKey)
    {
        string normalized = NormalizeKey(levelKey);
        return All.Where(definition => definition.HomeworldKey == normalized).ToArray();
    }

    private static string NormalizeKey(string value) =>
        (value ?? "")
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
}
