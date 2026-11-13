namespace Spyro.Editor.Core.Universal;

/// <summary>How high above the floor the game stands the objects whose middle is not at their feet (creatures, which
/// stand or hover; keys and fairies, which float; the balloon over its balloonist), so a copy brought into another level
/// stands as it does in its own instead of half sunk in the ground. Measured over the game's own levels (each copy's
/// height above the terrain beneath it, editor cache; study ground_all.py): the median, for classes whose copies mostly
/// stand at about that height (the lower quarter at least 60 percent of it). Linked objects that come with one (a Green
/// Wizard's spell target) keep their own height from it. Scenery and chests are not listed: where the game stands them
/// higher it is on pedestals and platforms that are objects of their own, not terrain; dragons come with their pedestal.
/// </summary>
public static class UniversalGroundOffsets
{
    private static readonly Dictionary<int, float> Heights = new()
    {
        [0x01C] = 6.5f, // Shielded Greenie (Green reward) (26 copies in 1 level)
        [0x02D] = 45.0f, // Metal Claw Monster (Blue reward) (7 copies in 1 level)
        [0x03E] = 33.0f, // Fat Bat (Green reward) (12 copies in 1 level)
        [0x058] = 32.0f, // Caged Fairy (14 copies in 1 level)
        [0x066] = 25.5f, // Armored Banana Boy (Blue reward) (21 copies in 1 level)
        [0x073] = 6.5f, // Large Gnorc (4 copies in 1 level)
        [0x08E] = 70.5f, // Temporary Superflame Fairy (4 copies in 1 level)
        [0x091] = 6.5f, // Auto-gunner Gnorc (13 copies in 1 level)
        [0x093] = 6.5f, // Gnorc Commando linked record (10 copies in 1 level)
        [0x0A5] = 25.5f, // Small Gnorc (10 copies in 1 level)
        [0x0A6] = 51.0f, // Big Armor Gnorc (2 copies in 1 level)
        [0x0AD] = 19.0f, // Key (12 copies in 12 levels)
        [0x0B5] = 19.0f, // Thief key (6 copies in 2 levels)
        [0x0BA] = 6.5f, // Gnorc w/ Gun (16 copies in 1 level)
        [0x0C6] = 6.5f, // Snow Gnorc (10 copies in 1 level)
        [0x0CB] = 54.5f, // Tin Soldier (Blue reward) (25 copies in 1 level)
        [0x0CC] = 13.0f, // Summoning Wizard (6 copies in 1 level)
        [0x0D3] = 6.5f, // Snowballer (6 copies in 1 level)
        [0x0D4] = 9.5f, // Agile Snowballer (3 copies in 1 level)
        [0x0E2] = 25.5f, // Wavy-shield Gnorc (10 copies in 1 level)
        [0x0E7] = 8.0f, // Armored Gnorc (4 copies in 1 level)
        [0x11B] = 13.0f, // Green Wizard (17 copies in 3 levels)
        [0x148] = 32.0f, // Green Druid spell target (23 copies in 4 levels)
        [0x1A0] = 96.0f, // Balloon (6 copies in 6 levels)
    };

    /// <summary>Units above the floor the game stands this class (null: on the floor).</summary>
    public static float? For(int classId) => Heights.TryGetValue(classId, out float h) ? h : null;
}
