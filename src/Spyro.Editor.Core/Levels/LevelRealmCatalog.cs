namespace Spyro.Editor.Core.Levels;

public sealed record LevelRealmDefinition(
    int RealmNumber,
    string Key,
    string DisplayName,
    int Order);

public sealed record LevelRealmPosition(
    LevelRealmDefinition Realm,
    int LevelSlot,
    int GlobalOrder);

public static class LevelRealmCatalog
{
    private static readonly IReadOnlyList<LevelRealmDefinition> RealmDefinitions =
    [
        new(1, "artisans", "Artisans", 0),
        new(2, "peacekeepers", "Peace Keepers", 1),
        new(3, "magiccrafters", "Magic Crafters", 2),
        new(4, "beastmakers", "Beast Makers", 3),
        new(5, "dreamweavers", "Dream Weavers", 4),
        new(6, "gnastysworld", "Gnasty's World", 5)
    ];

    private static readonly IReadOnlyDictionary<int, LevelRealmDefinition> ByRealmNumber =
        RealmDefinitions.ToDictionary(realm => realm.RealmNumber);

    public static IReadOnlyList<LevelRealmDefinition> Realms => RealmDefinitions;

    public static LevelRealmDefinition? FindByRealmNumber(int realmNumber)
    {
        return ByRealmNumber.TryGetValue(realmNumber, out LevelRealmDefinition? realm)
            ? realm
            : null;
    }

    public static bool TryGetPosition(LevelDefinition? level, out LevelRealmPosition position)
    {
        position = null!;
        if (level == null || level.LevelId < 0)
            return false;

        int realmNumber = level.LevelId / 10;
        int levelSlot = level.LevelId % 10;
        if (levelSlot is < 0 or > 5 || !ByRealmNumber.TryGetValue(realmNumber, out LevelRealmDefinition? realm))
            return false;

        position = new LevelRealmPosition(
            realm,
            levelSlot,
            checked((realm.Order * 6) + levelSlot));
        return true;
    }

    public static IReadOnlyList<LevelDefinition> OrderLevels(IEnumerable<LevelDefinition> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        return levels
            .OrderBy(level => TryGetPosition(level, out LevelRealmPosition position) ? position.GlobalOrder : int.MaxValue)
            .ThenBy(level => level.LevelId)
            .ThenBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(level => level.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
