using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Universal;

/// <summary>The splash objects Spyro's water, lava and ooze need. When Spyro lands in a liquid the executable
/// (0x8003FB04) spawns an object of class table[liquid] (0x8006EA34: water 0x1DD, lava 0x16B, ooze 0x1FB; the second
/// half of the table, 0x195 / 0x16C / 0x1FC, goes with them in every level that has them) through the level's own
/// spawn routine. A level without that liquid's models spawns an object with no model, and the object renderer
/// (0x8001F158 / 0x8001F798) crashes the game as soon as the splash is in view. Levels cleared with Start Blank drop
/// the native models, so a built level with water needs them imported.</summary>
public static class UniversalLiquidSplashes
{
    /// <summary>Classes per liquid (the surface record's third value: 0 water, 1 lava, 2 ooze).</summary>
    public static readonly IReadOnlyList<(int Splash, int Ripple)> Classes = [(0x1DD, 0x195), (0x16B, 0x16C), (0x1FB, 0x1FC)];
    /// <summary>Retail levels that carry each liquid's models and behaviour (Artisans, Dry Canyon, Toasty).</summary>
    public static readonly IReadOnlyList<string> Donors = ["artisans", "drycanyon", "toasty"];

    /// <summary>The liquids (0..2) a set of surface kinds uses.</summary>
    public static IReadOnlyList<int> Liquids(IEnumerable<AuthoredSurfaceKind?> kinds) =>
        [.. kinds.OfType<AuthoredSurfaceKind>().Where(k => k.Type == 0 && k.Param2 is >= 0 and <= 2).Select(k => k.Param2).Distinct().Order()];

    /// <summary>The liquids (0..2) the tiles of one of the game's own levels are given in its saved terrain edits (Tile
    /// Type…, the Ground card, or a texture that brought its donor's kind): the level needs their splash models too.</summary>
    public static IReadOnlyList<int> LiquidsInTerrainEdits(string terrainEditsPath) => Liquids(KindsInTerrainEdits(terrainEditsPath));

    /// <summary>The ground kinds the tiles of one of the game's own levels are given in its saved terrain edits (removed
    /// tiles left out), however they were given: Tile Type…, the Ground card, or a texture that brought its donor's kind.</summary>
    public static IReadOnlyList<AuthoredSurfaceKind> KindsInTerrainEdits(string terrainEditsPath)
    {
        if (!File.Exists(terrainEditsPath))
            return [];
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(terrainEditsPath));
        if (!document.RootElement.TryGetProperty("edits", out var edits) || edits.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];
        List<AuthoredSurfaceKind> kinds = [];
        foreach (var edit in edits.EnumerateArray())
        {
            if (!JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit") ||
                string.Equals(JsonValue.GetString(edit, "structureEditMode"), "remove-face", StringComparison.OrdinalIgnoreCase))
                continue;
            kinds.Add(new AuthoredSurfaceKind(JsonValue.GetInt32(edit, "nativeSurfaceType", -1),
                JsonValue.GetInt32(edit, "nativeSurfaceParam1"), JsonValue.GetInt32(edit, "nativeSurfaceParam2")));
        }
        return kinds;
    }

    /// <summary>Dependency-only imports that give a level the splash models of the liquids it uses: from the level's
    /// own original when it has both classes (its own handlers already run them), otherwise from the donor level.</summary>
    public static IReadOnlyList<(int Class, string SourceKey)> Dependencies(IEnumerable<int> liquids, string? destinationKey,
        Func<string, UniversalLevelData> source)
    {
        List<(int, string)> result = [];
        foreach (int liquid in liquids.Distinct().Order())
        {
            var (splash, ripple) = Classes[liquid];
            string key = Donors[liquid];
            if (destinationKey != null)
            {
                try
                {
                    var own = source(destinationKey).Registrations().Select(r => r.ClassId).ToHashSet();
                    if (own.Contains(splash) && own.Contains(ripple)) key = destinationKey;
                }
                catch (InvalidDataException) { }
            }
            result.Add((splash, key)); result.Add((ripple, key));
        }
        return result;
    }
}
