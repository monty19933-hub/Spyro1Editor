using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>An original level's terrain texture (level key, 0-based texture id) and the shade it is drawn with (128 = as
/// stored).</summary>
public sealed record ThemeTexture(string Level, int Id, byte Shade = 128)
{
    /// <summary>A shade per channel (lava's orange) instead of <see cref="Shade"/>.</summary>
    public ColorRgba? Tint { get; init; }
    /// <summary>The colours the original level itself paints this texture with, darkest to lightest, when measured
    /// (<see cref="LevelDnaTable"/>); the generator's faces take their colours from it.</summary>
    public IReadOnlyList<ColorRgba>? Ramp => Tint == null ? LevelDna.Ramp(Level, Id) : null;
}

/// <summary>What was measured from the original levels (<see cref="LevelDnaTable"/>), by level key and texture id.</summary>
public static class LevelDna
{
    private static readonly Dictionary<(string, int), (ColorRgba Average, ColorRgba[] Ramp)> Entries = Load();

    private static Dictionary<(string, int), (ColorRgba, ColorRgba[])> Load()
    {
        static ColorRgba C(uint v) => new((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        var map = new Dictionary<(string, int), (ColorRgba, ColorRgba[])>();
        foreach (var (level, textures) in LevelDnaTable.Levels)
            foreach (var (id, average, ramp) in textures) map[(level, id)] = (C(average), [.. ramp.Select(C)]);
        return map;
    }

    public static IReadOnlyList<ColorRgba>? Ramp(string level, int id) => Entries.TryGetValue((level, id), out var e) ? e.Ramp : null;
    public static ColorRgba? Average(string level, int id) => Entries.TryGetValue((level, id), out var e) ? e.Average : null;
    /// <summary>Whether a built level can carry the level's texture (a still picture in the game's usual layout).</summary>
    public static bool Static(string level, int id) => LevelDnaTable.Static.TryGetValue(level, out var ids) && Array.IndexOf(ids, id) >= 0;
}

/// <summary>The colours a role is painted in when the generator paints its own pictures: base, dark and light.</summary>
public sealed record ThemeColors(ColorRgba Base, ColorRgba Dark, ColorRgba Light);

/// <summary>A look for a generated level: a texture and colours for every role, the light, the sky, the music and the
/// words its name is made from.</summary>
public sealed record LevelTheme(string Key, string Name, IReadOnlyDictionary<SurfaceRole, ThemeTexture> Textures,
    IReadOnlyDictionary<SurfaceRole, ThemeColors> Colors, string SkyLevel, IReadOnlyList<int> Music,
    IReadOnlyList<string> Adjectives, IReadOnlyList<string> Nouns)
{
    /// <summary>Styles this theme suits, with weights (themes are chosen for a style by these).</summary>
    public IReadOnlyDictionary<LevelStyle, double> Styles { get; init; } = new Dictionary<LevelStyle, double>();
    /// <summary>The liquid of the theme's pools and moats.</summary>
    public SurfaceRole Liquid { get; init; } = SurfaceRole.Water;
    /// <summary>Sun direction (degrees round from east, degrees up), how bright faces turned away from it stay (0-1), and
    /// the light's colour (128 = white).</summary>
    public double SunAzimuth { get; init; } = 225;
    public double SunElevation { get; init; } = 50;
    public double Ambient { get; init; } = 0.62;
    public ColorRgba Light { get; init; } = new(128, 128, 128);
    /// <summary>The painted sky: top, horizon, below the horizon, the sun (or moon), clouds; stars for a night sky.</summary>
    public ColorRgba SkyTop { get; init; } = new(64, 112, 200);
    public ColorRgba SkyHorizon { get; init; } = new(176, 208, 240);
    public ColorRgba SkyBelow { get; init; } = new(96, 128, 112);
    public ColorRgba SkySun { get; init; } = new(255, 244, 200);
    public ColorRgba SkyClouds { get; init; } = new(244, 244, 250);
    public double CloudAmount { get; init; } = 0.45;
    public bool Stars { get; init; }
    /// <summary>The land round the level runs up into jagged peaks (Magic Crafters) instead of rounded hilltops.</summary>
    public bool Peaks { get; init; }
    /// <summary>Distant hills painted round the horizon (their colour; null: none).</summary>
    public ColorRgba? SkyHills { get; init; } = new(88, 120, 104);
}

public static class LevelThemes
{
    private static readonly ThemeTexture Lava = new("gnastygnorc", 14) { Tint = new ColorRgba(200, 92, 52) };
    private static readonly ThemeTexture Water = new("alpineridge", 36);
    private static readonly ThemeTexture Goo = new("drycanyon", 24);
    private static readonly ThemeTexture Chevrons = new("loftycastle", 22);

    /// <summary>Roles a look does not name take another role's texture, in this order.</summary>
    private static readonly (SurfaceRole Role, SurfaceRole From)[] Fallbacks =
    [
        (SurfaceRole.GroundAlt, SurfaceRole.Ground), (SurfaceRole.Path, SurfaceRole.Ground), (SurfaceRole.Paving, SurfaceRole.Path),
        (SurfaceRole.Dirt, SurfaceRole.Path), (SurfaceRole.Sand, SurfaceRole.Path), (SurfaceRole.CliffTop, SurfaceRole.Cliff),
        (SurfaceRole.Rock, SurfaceRole.Cliff), (SurfaceRole.CaveWall, SurfaceRole.Cliff), (SurfaceRole.Ceiling, SurfaceRole.CaveWall),
        (SurfaceRole.Underside, SurfaceRole.Rock), (SurfaceRole.Brick, SurfaceRole.Cliff), (SurfaceRole.Roof, SurfaceRole.Brick),
        (SurfaceRole.Wood, SurfaceRole.Paving),
    ];

    /// <summary>A look taken from one of the game's levels: the textures that level itself uses for its ground, banks,
    /// rock and paths (by how much of each the level is made of), painted with the level's own colours
    /// (<see cref="LevelDnaTable"/>), under its sky, to its music.</summary>
    private static LevelTheme Look(string level, string name, int[] music, string[] adjectives, string[] nouns,
        (LevelStyle Style, double Weight)[] styles, params (SurfaceRole Role, int Id)[] textures)
    {
        var map = textures.ToDictionary(t => t.Role, t => new ThemeTexture(level, t.Id));
        foreach (var (role, from) in Fallbacks) if (!map.ContainsKey(role)) map[role] = map[from];
        // A look's own water where the level has still water; else the game's plainest.
        map.TryAdd(SurfaceRole.Water, Water);
        map[SurfaceRole.Lava] = Lava; map[SurfaceRole.Goo] = Goo; map[SurfaceRole.Charge] = Chevrons;
        // The colours a painted picture of each surface is made in: the texture's own colour under the level's paint.
        var colours = new Dictionary<SurfaceRole, ThemeColors>();
        foreach (var (role, t) in map)
        {
            var average = LevelDna.Average(t.Level, t.Id) ?? new ColorRgba(128, 128, 128);
            var ramp = t.Ramp ?? [new(72, 72, 72), new(128, 128, 128), new(160, 160, 160)];
            ColorRgba Lit(ColorRgba paint) => new((byte)Math.Min(255, average.R * paint.R / 128), (byte)Math.Min(255, average.G * paint.G / 128), (byte)Math.Min(255, average.B * paint.B / 128));
            colours[role] = new(Lit(ramp[ramp.Count / 2]), Lit(ramp[ramp.Count / 5]), Lit(ramp[ramp.Count - 2]));
        }
        if (map[SurfaceRole.Water] == Water) colours[SurfaceRole.Water] = new(new(48, 112, 200), new(30, 70, 124), new(60, 140, 250));
        colours[SurfaceRole.Lava] = new(new(232, 96, 24), new(144, 60, 15), new(255, 120, 30));
        colours[SurfaceRole.Goo] = new(new(150, 48, 170), new(93, 30, 105), new(188, 60, 212));
        colours[SurfaceRole.Charge] = new(new(240, 200, 64), new(149, 124, 40), new(255, 250, 80));
        return new LevelTheme(level, name, map, colours, level, music, adjectives, nouns) { Styles = styles.ToDictionary(x => x.Style, x => x.Weight) };
    }

    private static readonly string[] MeadowAdjectives = ["SUNNY", "GREEN", "BREEZY", "MEADOW", "CLOVER", "GOLDEN"], MeadowNouns = ["HILLS", "FIELDS", "GLEN", "DOWNS", "GARDENS", "VALE"];
    private static readonly string[] StoneAdjectives = ["STONY", "WINDY", "OLD", "MOSSY", "SHEPHERDS", "QUIET"], StoneNouns = ["HILL", "KEEP", "WELLS", "FOLD", "RIDGE", "HEIGHTS"];
    private static readonly string[] DuskAdjectives = ["MISTY", "DUSKY", "SILENT", "SHADOW", "STARRY", "MOONLIT"], DuskNouns = ["HOLLOW", "GROVE", "MOOR", "GLADE", "RUINS", "DELL"];
    private static readonly string[] DesertAdjectives = ["DUSTY", "SCORCHED", "SANDY", "RED", "DRY", "AMBER"], DesertNouns = ["MESA", "DUNES", "GULCH", "BLUFFS", "CANYON", "OASIS"];
    private static readonly string[] IceAdjectives = ["FROZEN", "ICY", "SNOWY", "FROSTY", "CHILLY", "WHITE"], IceNouns = ["PEAKS", "GLACIER", "DRIFTS", "SUMMIT", "FJORD", "PASS"];
    private static readonly string[] MagicAdjectives = ["CRYSTAL", "MYSTIC", "WIZARDS", "CHARMED", "ARCANE", "SPELL"], MagicNouns = ["PEAKS", "CRAGS", "SPIRES", "RIDGE", "CAVES", "PASS"];
    private static readonly string[] SwampAdjectives = ["MURKY", "FOGGY", "MOSSY", "SOGGY", "GLOOMY", "DAMP"], SwampNouns = ["BOG", "MARSH", "FEN", "MIRE", "BAYOU", "WETLANDS"];
    private static readonly string[] SkyAdjectives = ["LOFTY", "CLOUDY", "SKY", "AIRY", "HIGH", "DREAMY"], SkyNouns = ["SPIRES", "HEIGHTS", "ISLES", "REACH", "TOWERS", "HAVEN"];
    private static readonly string[] CastleAdjectives = ["ROYAL", "GRAND", "KINGS", "NOBLE", "SILVER", "HAUNTED"], CastleNouns = ["KEEP", "CITADEL", "BASTION", "RAMPARTS", "COURT", "TOWERS"];
    private static readonly string[] HarbourAdjectives = ["RUSTY", "IRON", "TWILIGHT", "SALTY", "GNORC", "SMOKY"], HarbourNouns = ["HARBOR", "DOCKS", "COVE", "WHARF", "QUAY", "YARD"];

    public static LevelTheme Artisans { get; } = Look("artisans", "Artisans", [0, 2, 26], MeadowAdjectives, MeadowNouns,
        [(LevelStyle.Valley, 4), (LevelStyle.Castle, 2), (LevelStyle.Islands, 2), (LevelStyle.Canyon, 1), (LevelStyle.SkyIslands, 1), (LevelStyle.Caverns, 1)],
        (SurfaceRole.Ground, 39), (SurfaceRole.GroundAlt, 64), (SurfaceRole.Path, 44), (SurfaceRole.Cliff, 61), (SurfaceRole.CliffTop, 47),
        (SurfaceRole.Rock, 59), (SurfaceRole.Brick, 21), (SurfaceRole.Paving, 17), (SurfaceRole.Roof, 22), (SurfaceRole.Wood, 24), (SurfaceRole.Ceiling, 57),
        (SurfaceRole.Water, 27));

    public static LevelTheme StoneHill { get; } = Look("stonehill", "Stone Hill", [2, 0], StoneAdjectives, StoneNouns,
        [(LevelStyle.Valley, 4), (LevelStyle.Islands, 3), (LevelStyle.Castle, 2), (LevelStyle.Canyon, 1)],
        (SurfaceRole.Ground, 40), (SurfaceRole.GroundAlt, 40), (SurfaceRole.Path, 13), (SurfaceRole.Cliff, 11), (SurfaceRole.CliffTop, 12),
        (SurfaceRole.Rock, 19), (SurfaceRole.Brick, 21), (SurfaceRole.Paving, 18), (SurfaceRole.Roof, 20), (SurfaceRole.Wood, 29), (SurfaceRole.Sand, 30),
        (SurfaceRole.Water, 32));

    public static LevelTheme DarkHollow { get; } = Look("darkhollow", "Dark Hollow", [32], DuskAdjectives, DuskNouns,
        [(LevelStyle.Valley, 3), (LevelStyle.Castle, 3), (LevelStyle.Caverns, 1), (LevelStyle.SkyIslands, 1)],
        (SurfaceRole.Ground, 34), (SurfaceRole.GroundAlt, 9), (SurfaceRole.Path, 30), (SurfaceRole.Cliff, 24), (SurfaceRole.CliffTop, 8),
        (SurfaceRole.Rock, 25), (SurfaceRole.Brick, 22), (SurfaceRole.Paving, 1), (SurfaceRole.Roof, 28)) with
    {
        SkyTop = new(24, 28, 72), SkyHorizon = new(120, 96, 140), SkyBelow = new(28, 36, 56), SkySun = new(232, 236, 255), SkyClouds = new(72, 72, 110),
        CloudAmount = 0.3, Stars = true, SkyHills = new(24, 40, 48),
    };

    public static LevelTheme PeaceKeepers { get; } = Look("peacekeepers", "Peace Keepers", [17, 3], DesertAdjectives, DesertNouns,
        [(LevelStyle.Canyon, 4), (LevelStyle.Valley, 1), (LevelStyle.Caverns, 1), (LevelStyle.Islands, 1)],
        (SurfaceRole.Ground, 36), (SurfaceRole.GroundAlt, 15), (SurfaceRole.Path, 17), (SurfaceRole.Cliff, 35), (SurfaceRole.CliffTop, 20),
        (SurfaceRole.Rock, 30), (SurfaceRole.Brick, 27), (SurfaceRole.Paving, 18), (SurfaceRole.Wood, 12), (SurfaceRole.CaveWall, 33),
        (SurfaceRole.Ceiling, 7), (SurfaceRole.Underside, 34)) with
    {
        Liquid = SurfaceRole.Goo, SkyTop = new(96, 136, 216), SkyHorizon = new(240, 216, 176), SkyBelow = new(200, 150, 96), CloudAmount = 0.2, SkyHills = new(184, 112, 72),
    };

    public static LevelTheme DryCanyon { get; } = Look("drycanyon", "Dry Canyon", [3, 27], DesertAdjectives, DesertNouns,
        [(LevelStyle.Canyon, 5), (LevelStyle.Caverns, 2), (LevelStyle.Islands, 1)],
        (SurfaceRole.Ground, 45), (SurfaceRole.GroundAlt, 43), (SurfaceRole.Path, 28), (SurfaceRole.Cliff, 34), (SurfaceRole.CliffTop, 37),
        (SurfaceRole.Rock, 35), (SurfaceRole.Brick, 33), (SurfaceRole.Wood, 21), (SurfaceRole.CaveWall, 41), (SurfaceRole.Ceiling, 40),
        (SurfaceRole.Underside, 38), (SurfaceRole.Sand, 27)) with
    {
        Liquid = SurfaceRole.Goo, SkyTop = new(80, 120, 208), SkyHorizon = new(244, 200, 150), SkyBelow = new(190, 120, 80), CloudAmount = 0.2, SkyHills = new(170, 96, 60),
    };

    public static LevelTheme IceCavern { get; } = Look("icecavern", "Ice Cavern", [13], IceAdjectives, IceNouns,
        [(LevelStyle.Caverns, 4), (LevelStyle.Canyon, 3), (LevelStyle.Islands, 1), (LevelStyle.SkyIslands, 1)],
        (SurfaceRole.Ground, 34), (SurfaceRole.GroundAlt, 32), (SurfaceRole.Path, 30), (SurfaceRole.Cliff, 21), (SurfaceRole.CliffTop, 54),
        (SurfaceRole.Rock, 8), (SurfaceRole.Brick, 31), (SurfaceRole.CaveWall, 49), (SurfaceRole.Ceiling, 8), (SurfaceRole.Underside, 15)) with
    {
        SkyTop = new(96, 128, 196), SkyHorizon = new(212, 224, 240), SkyBelow = new(224, 232, 240), CloudAmount = 0.5, SkyHills = new(196, 208, 228),
    };

    public static LevelTheme MagicCrafters { get; } = Look("magiccrafters", "Magic Crafters", [31, 22], MagicAdjectives, MagicNouns,
        [(LevelStyle.Valley, 4), (LevelStyle.Canyon, 2), (LevelStyle.SkyIslands, 2), (LevelStyle.Caverns, 1)],
        (SurfaceRole.Ground, 17), (SurfaceRole.GroundAlt, 17), (SurfaceRole.Path, 37), (SurfaceRole.Cliff, 28), (SurfaceRole.CliffTop, 47),
        (SurfaceRole.Rock, 16), (SurfaceRole.CaveWall, 27), (SurfaceRole.Ceiling, 45), (SurfaceRole.Underside, 45)) with
    {
        Peaks = true, SkyTop = new(70, 90, 190), SkyHorizon = new(200, 180, 235), SkyBelow = new(150, 140, 200), SkyHills = new(170, 150, 210),
    };

    public static LevelTheme WizardPeak { get; } = Look("wizardpeak", "Wizard Peak", [9], MagicAdjectives, IceNouns,
        [(LevelStyle.Canyon, 3), (LevelStyle.Valley, 2), (LevelStyle.SkyIslands, 2)],
        (SurfaceRole.Ground, 31), (SurfaceRole.Path, 69), (SurfaceRole.Cliff, 60), (SurfaceRole.CliffTop, 68), (SurfaceRole.Rock, 38),
        (SurfaceRole.Brick, 61), (SurfaceRole.Underside, 26)) with
    {
        Peaks = true, SkyTop = new(50, 80, 180), SkyHorizon = new(190, 210, 245), SkyBelow = new(200, 210, 235), SkyHills = new(190, 200, 235),
    };

    public static LevelTheme MistyBog { get; } = Look("mistybog", "Misty Bog", [21, 15], SwampAdjectives, SwampNouns,
        [(LevelStyle.Islands, 3), (LevelStyle.Valley, 3), (LevelStyle.Canyon, 1)],
        (SurfaceRole.Ground, 38), (SurfaceRole.GroundAlt, 27), (SurfaceRole.Path, 52), (SurfaceRole.Cliff, 7), (SurfaceRole.CliffTop, 37),
        (SurfaceRole.Rock, 35), (SurfaceRole.Brick, 25), (SurfaceRole.Paving, 23), (SurfaceRole.Wood, 56), (SurfaceRole.Ceiling, 12),
        (SurfaceRole.Underside, 12), (SurfaceRole.Sand, 16)) with
    {
        Liquid = SurfaceRole.Goo, SkyTop = new(88, 104, 112), SkyHorizon = new(168, 180, 160), SkyBelow = new(72, 96, 64), SkyClouds = new(176, 184, 176),
        CloudAmount = 0.75, SkyHills = new(56, 80, 56),
    };

    public static LevelTheme TreeTops { get; } = Look("treetops", "Tree Tops", [29], SwampAdjectives, ["TREETOPS", "BOUGHS", "CANOPY", "PERCH", "NESTS", "HEIGHTS"],
        [(LevelStyle.SkyIslands, 5), (LevelStyle.Islands, 1), (LevelStyle.Valley, 1)],
        (SurfaceRole.Ground, 21), (SurfaceRole.GroundAlt, 19), (SurfaceRole.Path, 35), (SurfaceRole.Cliff, 80), (SurfaceRole.Rock, 54),
        (SurfaceRole.Brick, 12), (SurfaceRole.Wood, 68), (SurfaceRole.Underside, 60)) with
    {
        SkyTop = new(90, 110, 90), SkyHorizon = new(190, 200, 150), SkyBelow = new(60, 80, 50), CloudAmount = 0.5, SkyHills = new(50, 80, 50),
    };

    public static LevelTheme DreamWeavers { get; } = Look("dreamweavers", "Dream Weavers", [34], SkyAdjectives, SkyNouns,
        [(LevelStyle.SkyIslands, 5), (LevelStyle.Castle, 2)],
        (SurfaceRole.Ground, 43), (SurfaceRole.GroundAlt, 10), (SurfaceRole.Path, 35), (SurfaceRole.Cliff, 37), (SurfaceRole.CliffTop, 39),
        (SurfaceRole.Rock, 30), (SurfaceRole.Roof, 41), (SurfaceRole.Underside, 31)) with
    {
        SkyTop = new(40, 60, 150), SkyHorizon = new(190, 150, 220), SkyBelow = new(120, 100, 180), CloudAmount = 0.7, SkyHills = null,
    };

    public static LevelTheme LoftyCastle { get; } = Look("loftycastle", "Lofty Castle", [14, 34], SkyAdjectives, SkyNouns,
        [(LevelStyle.SkyIslands, 5), (LevelStyle.Castle, 2), (LevelStyle.Islands, 1)],
        (SurfaceRole.Ground, 65), (SurfaceRole.Path, 15), (SurfaceRole.Cliff, 25), (SurfaceRole.CliffTop, 19), (SurfaceRole.Rock, 57),
        (SurfaceRole.Paving, 30), (SurfaceRole.Roof, 23), (SurfaceRole.Wood, 24)) with
    {
        SkyTop = new(72, 136, 232), SkyHorizon = new(200, 224, 252), SkyBelow = new(232, 236, 248), CloudAmount = 0.7, SkyHills = null,
    };

    public static LevelTheme HauntedTowers { get; } = Look("hauntedtowers", "Haunted Towers", [4, 12], CastleAdjectives, CastleNouns,
        [(LevelStyle.Castle, 5), (LevelStyle.Valley, 1), (LevelStyle.SkyIslands, 1)],
        (SurfaceRole.Ground, 82), (SurfaceRole.GroundAlt, 80), (SurfaceRole.Path, 78), (SurfaceRole.Cliff, 69), (SurfaceRole.CliffTop, 73),
        (SurfaceRole.Rock, 0), (SurfaceRole.Brick, 16)) with
    {
        SkyTop = new(16, 20, 60), SkyHorizon = new(70, 80, 140), SkyBelow = new(20, 28, 56), SkySun = new(232, 236, 255), SkyClouds = new(64, 72, 110),
        CloudAmount = 0.35, Stars = true, SkyHills = new(24, 32, 56),
    };

    public static LevelTheme TwilightHarbor { get; } = Look("twilightharbor", "Twilight Harbor", [24, 5], HarbourAdjectives, HarbourNouns,
        [(LevelStyle.Islands, 4), (LevelStyle.Canyon, 2), (LevelStyle.Castle, 1)],
        (SurfaceRole.Ground, 47), (SurfaceRole.Path, 38), (SurfaceRole.Cliff, 29), (SurfaceRole.CliffTop, 49), (SurfaceRole.Rock, 0),
        (SurfaceRole.Brick, 14), (SurfaceRole.Wood, 15), (SurfaceRole.Underside, 10)) with
    {
        SkyTop = new(40, 40, 90), SkyHorizon = new(230, 130, 60), SkyBelow = new(70, 50, 60), SkySun = new(255, 190, 110), SkyClouds = new(120, 80, 90),
        CloudAmount = 0.5, SkyHills = new(60, 44, 56),
    };

    public static LevelTheme TownSquare { get; } = Look("townsquare", "Town Square", [26, 0], CastleAdjectives, ["SQUARE", "TOWN", "MARKET", "COURT", "ROOFTOPS", "PLAZA"],
        [(LevelStyle.Castle, 5), (LevelStyle.SkyIslands, 2), (LevelStyle.Valley, 1)],
        (SurfaceRole.Ground, 6), (SurfaceRole.GroundAlt, 5), (SurfaceRole.Path, 50), (SurfaceRole.Cliff, 16), (SurfaceRole.CliffTop, 32),
        (SurfaceRole.Rock, 36), (SurfaceRole.Brick, 15), (SurfaceRole.Paving, 28), (SurfaceRole.Roof, 3));

    public static LevelTheme CliffTown { get; } = Look("clifftown", "Cliff Town", [27, 3], DesertAdjectives, ["TOWN", "PUEBLO", "BLUFFS", "MESA", "TERRACES", "CLIFFS"],
        [(LevelStyle.Canyon, 4), (LevelStyle.Castle, 2), (LevelStyle.Islands, 1), (LevelStyle.Caverns, 1)],
        (SurfaceRole.Ground, 41), (SurfaceRole.GroundAlt, 1), (SurfaceRole.Path, 30), (SurfaceRole.Cliff, 29), (SurfaceRole.CliffTop, 35),
        (SurfaceRole.Rock, 27), (SurfaceRole.Brick, 26), (SurfaceRole.Paving, 14), (SurfaceRole.Wood, 15), (SurfaceRole.CaveWall, 36),
        (SurfaceRole.Underside, 37)) with
    {
        Liquid = SurfaceRole.Goo, SkyTop = new(90, 110, 200), SkyHorizon = new(250, 180, 110), SkyBelow = new(200, 120, 70), CloudAmount = 0.25, SkyHills = new(180, 100, 60),
    };

    public static LevelTheme HighCaves { get; } = Look("highcaves", "High Caves", [46, 31], MagicAdjectives, MagicNouns,
        [(LevelStyle.Caverns, 5), (LevelStyle.Valley, 2), (LevelStyle.Canyon, 2)],
        (SurfaceRole.Ground, 37), (SurfaceRole.GroundAlt, 69), (SurfaceRole.Path, 86), (SurfaceRole.Cliff, 85), (SurfaceRole.CliffTop, 16),
        (SurfaceRole.Rock, 28), (SurfaceRole.Brick, 64), (SurfaceRole.CaveWall, 84), (SurfaceRole.Ceiling, 23), (SurfaceRole.Underside, 24)) with
    {
        Peaks = true, SkyTop = new(70, 90, 190), SkyHorizon = new(200, 180, 235), SkyBelow = new(150, 140, 200), SkyHills = new(170, 150, 210),
    };

    public static LevelTheme DarkPassage { get; } = Look("darkpassage", "Dark Passage", [12, 34], DuskAdjectives, ["PASSAGE", "GROTTO", "CRYPT", "HOLLOW", "DEPTHS", "RUINS"],
        [(LevelStyle.Caverns, 3), (LevelStyle.Castle, 3), (LevelStyle.Valley, 2), (LevelStyle.SkyIslands, 1)],
        (SurfaceRole.Ground, 23), (SurfaceRole.GroundAlt, 31), (SurfaceRole.Path, 38), (SurfaceRole.Cliff, 21), (SurfaceRole.CliffTop, 13),
        (SurfaceRole.Rock, 29), (SurfaceRole.Brick, 18), (SurfaceRole.CaveWall, 12), (SurfaceRole.Ceiling, 29)) with
    {
        Peaks = true, SkyTop = new(8, 12, 40), SkyHorizon = new(48, 64, 112), SkyBelow = new(16, 24, 48), SkySun = new(232, 236, 255), SkyClouds = new(56, 64, 96),
        CloudAmount = 0.3, Stars = true, SkyHills = new(20, 28, 48),
    };

    public static IReadOnlyList<LevelTheme> All { get; } =
    [
        Artisans, StoneHill, DarkHollow, TownSquare, PeaceKeepers, DryCanyon, CliffTown, IceCavern, MagicCrafters, HighCaves, WizardPeak,
        MistyBog, TreeTops, DreamWeavers, DarkPassage, LoftyCastle, HauntedTowers, TwilightHarbor,
    ];

    /// <summary>The looks of the first generator, by the level that now stands for each.</summary>
    private static readonly Dictionary<string, string> Earlier = new(StringComparer.OrdinalIgnoreCase)
    {
        ["meadow"] = "artisans", ["highland"] = "stonehill", ["sky"] = "loftycastle", ["desert"] = "drycanyon", ["winter"] = "icecavern",
        ["swamp"] = "mistybog", ["volcano"] = "peacekeepers", ["castle"] = "hauntedtowers", ["night"] = "darkpassage",
    };

    /// <summary>The doorway shape that suits a look (<see cref="Editing.DoorwayShapes"/>): the round stone arch of the
    /// Artisans' world, square gates among the Peace Keepers' rocks, pointed arches for the Magic Crafters, six-sided
    /// gates for the Beast Makers, rings for the Dream Weavers, wide arches in Gnasty's harbour.</summary>
    public static Editing.DoorwayShape DoorwayShape(string? key) => Find(key)?.Key switch
    {
        "peacekeepers" or "drycanyon" or "clifftown" or "icecavern" => Editing.DoorwayShapes.SquareGate,
        "magiccrafters" or "highcaves" or "wizardpeak" => Editing.DoorwayShapes.PointedArch,
        "mistybog" or "treetops" => Editing.DoorwayShapes.HexGate,
        "dreamweavers" or "darkpassage" or "loftycastle" or "hauntedtowers" => Editing.DoorwayShapes.RoundRing,
        "twilightharbor" => Editing.DoorwayShapes.WideArch,
        _ => Editing.DoorwayShapes.StoneArch,
    };

    public static LevelTheme? Find(string? key) => key == null ? null
        : All.FirstOrDefault(t => string.Equals(t.Key, Earlier.GetValueOrDefault(key, key), StringComparison.OrdinalIgnoreCase));

    /// <summary>A theme for <paramref name="style"/>, by the themes' weights for it.</summary>
    public static LevelTheme For(LevelStyle style, LevelRandom random) =>
        random.Pick<LevelTheme>([.. All.Select(t => (t, t.Styles.TryGetValue(style, out double w) ? w : 0.15))]);
}
