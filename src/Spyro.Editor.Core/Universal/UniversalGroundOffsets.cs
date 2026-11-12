namespace Spyro.Editor.Core.Universal;

/// <summary>How high above the floor the game stands objects that float by nature, so a copy brought into another level
/// floats as it does in its own. Measured over the game's own levels (each copy's height above the terrain beneath it,
/// editor cache): the key floats 18.6-19.9 units up in all 13 levels that have one (its locked chest stands on the floor),
/// Gnasty's thief key the same. A key put on the floor is half sunk in it, and the player does not find it.
/// Other classes that stand above the terrain in the game do so on pedestals, platforms or walls that are not terrain,
/// so they are not listed.</summary>
public static class UniversalGroundOffsets
{
    private static readonly Dictionary<int, float> Heights = new()
    {
        [0x0AD] = 19f, // key (median 19.2 over 13 levels)
        [0x0B5] = 19f, // thief key (Gnasty Gnorc, Gnasty's Loot)
    };

    /// <summary>Units above the floor the game stands this class (null: on the floor).</summary>
    public static float? For(int classId) => Heights.TryGetValue(classId, out float h) ? h : null;
}
