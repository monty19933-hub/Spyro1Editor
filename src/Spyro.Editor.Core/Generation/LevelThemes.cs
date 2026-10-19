using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>An original level's terrain texture (level key, 0-based texture id) and the shade it is drawn with (128 = as
/// stored).</summary>
public sealed record ThemeTexture(string Level, int Id, byte Shade = 128)
{
    /// <summary>A shade per channel (lava's orange) instead of <see cref="Shade"/>.</summary>
    public ColorRgba? Tint { get; init; }
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
    /// <summary>Distant hills painted round the horizon (their colour; null: none).</summary>
    public ColorRgba? SkyHills { get; init; } = new(88, 120, 104);
}

public static class LevelThemes
{
    private static ThemeColors C(int r, int g, int b) => new(new((byte)r, (byte)g, (byte)b),
        new((byte)(r * 0.62), (byte)(g * 0.62), (byte)(b * 0.62)), new((byte)Math.Min(255, r * 1.25), (byte)Math.Min(255, g * 1.25), (byte)Math.Min(255, b * 1.25)));
    private static Dictionary<SurfaceRole, ThemeColors> Palette(ThemeColors ground, ThemeColors alt, ThemeColors path, ThemeColors dirt,
        ThemeColors sand, ThemeColors cliff, ThemeColors brick, ThemeColors roof, ThemeColors wood, ThemeColors liquid) => new Dictionary<SurfaceRole, ThemeColors>
    {
        [SurfaceRole.Ground] = ground, [SurfaceRole.GroundAlt] = alt, [SurfaceRole.Path] = path, [SurfaceRole.Dirt] = dirt,
        [SurfaceRole.Sand] = sand, [SurfaceRole.Cliff] = cliff, [SurfaceRole.CliffTop] = cliff, [SurfaceRole.Rock] = cliff,
        [SurfaceRole.CaveWall] = cliff, [SurfaceRole.Ceiling] = cliff, [SurfaceRole.Underside] = cliff, [SurfaceRole.Brick] = brick,
        [SurfaceRole.Paving] = path, [SurfaceRole.Roof] = roof, [SurfaceRole.Wood] = wood,
        [SurfaceRole.Water] = C(48, 112, 200), [SurfaceRole.Lava] = C(232, 96, 24), [SurfaceRole.Goo] = C(150, 48, 170),
        [SurfaceRole.Charge] = C(240, 200, 64),
    }.ToDictionary(p => p.Key, p => p.Key is SurfaceRole.Water && liquid != null ? liquid : p.Value);

    private static readonly ThemeTexture Lava = new("gnastygnorc", 14) { Tint = new ColorRgba(200, 92, 52) };
    private static readonly ThemeTexture Water = new("alpineridge", 36);
    private static readonly ThemeTexture Goo = new("drycanyon", 24);
    private static readonly ThemeTexture Chevrons = new("loftycastle", 22);

    public static LevelTheme Meadow { get; } = new("meadow", "Spring Meadow", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("artisans", 30), [SurfaceRole.GroundAlt] = new("artisans", 43), [SurfaceRole.Path] = new("darkhollow", 15),
        [SurfaceRole.Dirt] = new("stonehill", 11), [SurfaceRole.Sand] = new("stonehill", 30), [SurfaceRole.Cliff] = new("artisans", 58),
        [SurfaceRole.CliffTop] = new("artisans", 47), [SurfaceRole.Rock] = new("artisans", 40), [SurfaceRole.CaveWall] = new("artisans", 60),
        [SurfaceRole.Ceiling] = new("artisans", 61), [SurfaceRole.Brick] = new("artisans", 42), [SurfaceRole.Paving] = new("artisans", 8),
        [SurfaceRole.Roof] = new("artisans", 19), [SurfaceRole.Wood] = new("artisans", 24), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("artisans", 59), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(70, 140, 56), C(96, 160, 72), C(150, 146, 140), C(120, 92, 64), C(212, 184, 120), C(132, 124, 132), C(160, 150, 150),
        C(72, 120, 190), C(130, 88, 52), null!), "artisans", [0, 2, 26, 17], ["SUNNY", "GREEN", "BREEZY", "MEADOW", "CLOVER", "GOLDEN"],
        ["HILLS", "FIELDS", "GLEN", "DOWNS", "GARDENS", "VALE"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Valley] = 3, [LevelStyle.Islands] = 2, [LevelStyle.Canyon] = 2, [LevelStyle.Castle] = 1, [LevelStyle.Caverns] = 1, [LevelStyle.SkyIslands] = 1 },
    };

    public static LevelTheme Highland { get; } = new("highland", "Misty Highland", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("darkhollow", 17), [SurfaceRole.GroundAlt] = new("darkhollow", 10), [SurfaceRole.Path] = new("darkhollow", 14),
        [SurfaceRole.Dirt] = new("stonehill", 7), [SurfaceRole.Sand] = new("stonehill", 30), [SurfaceRole.Cliff] = new("darkhollow", 18),
        [SurfaceRole.CliffTop] = new("darkhollow", 19), [SurfaceRole.Rock] = new("darkhollow", 30), [SurfaceRole.CaveWall] = new("darkhollow", 18),
        [SurfaceRole.Ceiling] = new("artisans", 57), [SurfaceRole.Brick] = new("darkhollow", 24), [SurfaceRole.Paving] = new("stonehill", 21),
        [SurfaceRole.Roof] = new("darkhollow", 28), [SurfaceRole.Wood] = new("artisans", 24), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("darkhollow", 18), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(52, 120, 44), C(72, 136, 52), C(150, 150, 140), C(104, 80, 60), C(200, 176, 120), C(124, 116, 120), C(140, 136, 136),
        C(96, 128, 96), C(110, 76, 48), C(56, 96, 168)), "darkhollow", [32, 2, 22], ["MISTY", "STONY", "WINDY", "HIGH", "OLD", "MOSSY"],
        ["MOOR", "CRAGS", "HOLLOW", "TORS", "RIDGE", "HEIGHTS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Valley] = 3, [LevelStyle.Canyon] = 3, [LevelStyle.Caverns] = 2, [LevelStyle.Castle] = 2, [LevelStyle.Islands] = 1 },
        SunElevation = 38, Ambient = 0.58, SkyTop = new(80, 100, 150), SkyHorizon = new(176, 188, 200), SkyClouds = new(220, 224, 232), CloudAmount = 0.6,
        SkyHills = new(72, 96, 88),
    };

    public static LevelTheme Sky { get; } = new("sky", "Cloud Kingdom", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("loftycastle", 27), [SurfaceRole.GroundAlt] = new("loftycastle", 65), [SurfaceRole.Path] = new("loftycastle", 15),
        [SurfaceRole.Dirt] = new("loftycastle", 26), [SurfaceRole.Sand] = new("loftycastle", 28), [SurfaceRole.Cliff] = new("loftycastle", 56),
        [SurfaceRole.CliffTop] = new("loftycastle", 58), [SurfaceRole.Rock] = new("loftycastle", 57), [SurfaceRole.CaveWall] = new("loftycastle", 57),
        [SurfaceRole.Ceiling] = new("loftycastle", 56), [SurfaceRole.Brick] = new("loftycastle", 53), [SurfaceRole.Paving] = new("loftycastle", 16),
        [SurfaceRole.Roof] = new("dreamweavers", 40), [SurfaceRole.Wood] = new("treetops", 40), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("loftycastle", 57), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(96, 192, 150), C(120, 210, 170), C(236, 228, 200), C(176, 160, 200), C(220, 210, 236), C(118, 100, 140), C(232, 200, 210),
        C(72, 160, 168), C(140, 96, 56), null!), "loftycastle", [14, 34, 9], ["LOFTY", "CLOUDY", "SKY", "AIRY", "HIGH", "DREAMY"],
        ["SPIRES", "HEIGHTS", "ISLES", "REACH", "TOWERS", "HAVEN"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.SkyIslands] = 5, [LevelStyle.Castle] = 2, [LevelStyle.Islands] = 1, [LevelStyle.Valley] = 1 },
        SunElevation = 60, Ambient = 0.66, SkyTop = new(72, 136, 232), SkyHorizon = new(200, 224, 252), SkyBelow = new(232, 236, 248), CloudAmount = 0.7,
        SkyHills = null,
    };

    public static LevelTheme Desert { get; } = new("desert", "Sun-Baked Desert", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("drycanyon", 45), [SurfaceRole.GroundAlt] = new("drycanyon", 43), [SurfaceRole.Path] = new("drycanyon", 16),
        [SurfaceRole.Dirt] = new("drycanyon", 27), [SurfaceRole.Sand] = new("drycanyon", 20), [SurfaceRole.Cliff] = new("drycanyon", 39),
        [SurfaceRole.CliffTop] = new("drycanyon", 36), [SurfaceRole.Rock] = new("drycanyon", 22), [SurfaceRole.CaveWall] = new("drycanyon", 40),
        [SurfaceRole.Ceiling] = new("drycanyon", 41), [SurfaceRole.Brick] = new("drycanyon", 29), [SurfaceRole.Paving] = new("clifftown", 2),
        [SurfaceRole.Roof] = new("clifftown", 13), [SurfaceRole.Wood] = new("clifftown", 15), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("drycanyon", 38), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(200, 168, 104), C(212, 180, 112), C(184, 150, 96), C(176, 128, 72), C(224, 156, 88), C(196, 120, 72), C(184, 96, 64),
        C(160, 56, 48), C(132, 92, 56), null!), "drycanyon", [3, 27, 17], ["DUSTY", "SCORCHED", "SANDY", "RED", "DRY", "AMBER"],
        ["MESA", "DUNES", "GULCH", "BLUFFS", "CANYON", "OASIS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Canyon] = 4, [LevelStyle.Valley] = 2, [LevelStyle.Castle] = 1, [LevelStyle.Caverns] = 1, [LevelStyle.Islands] = 1 },
        Liquid = SurfaceRole.Goo, SunElevation = 64, Ambient = 0.66, Light = new(136, 128, 116), SkyTop = new(96, 136, 216), SkyHorizon = new(240, 216, 176),
        SkyBelow = new(200, 150, 96), CloudAmount = 0.2, SkyHills = new(184, 112, 72),
    };

    public static LevelTheme Winter { get; } = new("winter", "Frozen Peaks", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("icecavern", 0), [SurfaceRole.GroundAlt] = new("icecavern", 34), [SurfaceRole.Path] = new("highcaves", 33),
        [SurfaceRole.Dirt] = new("highcaves", 31), [SurfaceRole.Sand] = new("icecavern", 10), [SurfaceRole.Cliff] = new("highcaves", 36),
        [SurfaceRole.CliffTop] = new("highcaves", 12), [SurfaceRole.Rock] = new("highcaves", 37), [SurfaceRole.CaveWall] = new("icecavern", 9),
        [SurfaceRole.Ceiling] = new("highcaves", 36), [SurfaceRole.Brick] = new("icecavern", 27), [SurfaceRole.Paving] = new("icecavern", 30),
        [SurfaceRole.Roof] = new("dreamweavers", 41), [SurfaceRole.Wood] = new("treetops", 40), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("highcaves", 37), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(224, 228, 236), C(200, 220, 236), C(136, 140, 140), C(120, 124, 120), C(232, 236, 240), C(120, 124, 132), C(160, 192, 216),
        C(96, 128, 160), C(120, 88, 56), C(80, 140, 200)), "icecavern", [13, 10, 22], ["FROZEN", "ICY", "SNOWY", "FROSTY", "CHILLY", "WHITE"],
        ["PEAKS", "GLACIER", "DRIFTS", "SUMMIT", "FJORD", "PASS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Valley] = 3, [LevelStyle.Canyon] = 2, [LevelStyle.Caverns] = 3, [LevelStyle.Islands] = 1, [LevelStyle.Castle] = 1 },
        SunElevation = 30, Ambient = 0.7, Light = new(124, 128, 136), SkyTop = new(96, 128, 196), SkyHorizon = new(212, 224, 240),
        SkyBelow = new(224, 232, 240), CloudAmount = 0.5, SkyHills = new(196, 208, 228),
    };

    public static LevelTheme Swamp { get; } = new("swamp", "Murky Swamp", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("mistybog", 27), [SurfaceRole.GroundAlt] = new("mistybog", 30), [SurfaceRole.Path] = new("mistybog", 15),
        [SurfaceRole.Dirt] = new("mistybog", 16), [SurfaceRole.Sand] = new("mistybog", 17), [SurfaceRole.Cliff] = new("mistybog", 11),
        [SurfaceRole.CliffTop] = new("mistybog", 36), [SurfaceRole.Rock] = new("mistybog", 23), [SurfaceRole.CaveWall] = new("mistybog", 12),
        [SurfaceRole.Ceiling] = new("mistybog", 10), [SurfaceRole.Brick] = new("mistybog", 45), [SurfaceRole.Paving] = new("mistybog", 24),
        [SurfaceRole.Roof] = new("beastmakers", 7), [SurfaceRole.Wood] = new("mistybog", 61), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("mistybog", 11), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(88, 128, 56), C(104, 120, 56), C(112, 96, 64), C(96, 76, 48), C(120, 104, 72), C(84, 72, 48), C(120, 116, 124),
        C(140, 120, 64), C(110, 76, 44), C(64, 96, 72)), "mistybog", [21, 15, 7], ["MURKY", "FOGGY", "MOSSY", "SOGGY", "GLOOMY", "DAMP"],
        ["BOG", "MARSH", "FEN", "MIRE", "BAYOU", "WETLANDS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Islands] = 3, [LevelStyle.Valley] = 2, [LevelStyle.Canyon] = 1, [LevelStyle.Caverns] = 1 },
        Liquid = SurfaceRole.Goo, SunElevation = 42, Ambient = 0.55, Light = new(120, 128, 112), SkyTop = new(88, 104, 112), SkyHorizon = new(168, 180, 160),
        SkyBelow = new(72, 96, 64), SkyClouds = new(176, 184, 176), CloudAmount = 0.75, SkyHills = new(56, 80, 56),
    };

    public static LevelTheme Volcano { get; } = new("volcano", "Fiery Volcano", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("nightflight", 19), [SurfaceRole.GroundAlt] = new("gnastygnorc", 27), [SurfaceRole.Path] = new("gnastysworld", 31),
        [SurfaceRole.Dirt] = new("gnastygnorc", 37), [SurfaceRole.Sand] = new("gnastygnorc", 36), [SurfaceRole.Cliff] = new("darkpassage", 3),
        [SurfaceRole.CliffTop] = new("darkpassage", 12), [SurfaceRole.Rock] = new("darkpassage", 29), [SurfaceRole.CaveWall] = new("darkpassage", 3),
        [SurfaceRole.Ceiling] = new("darkpassage", 29), [SurfaceRole.Brick] = new("gnastysworld", 9), [SurfaceRole.Paving] = new("gnastysworld", 18),
        [SurfaceRole.Roof] = new("gnastysworld", 23), [SurfaceRole.Wood] = new("clifftown", 15), [SurfaceRole.Water] = Lava,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("darkpassage", 3), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(80, 72, 72), C(96, 88, 80), C(128, 116, 104), C(104, 72, 56), C(120, 96, 80), C(72, 64, 64), C(150, 64, 48),
        C(96, 96, 104), C(104, 72, 48), C(232, 96, 24)), "gnastysworld", [25, 33, 23, 19], ["FIERY", "SMOKY", "MOLTEN", "BURNING", "ASHEN", "SCALDING"],
        ["CRATER", "FORGE", "CINDERS", "CALDERA", "FURNACE", "EMBERS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Caverns] = 3, [LevelStyle.Islands] = 3, [LevelStyle.Canyon] = 2, [LevelStyle.Valley] = 1, [LevelStyle.Castle] = 1 },
        Liquid = SurfaceRole.Lava, SunElevation = 35, Ambient = 0.6, Light = new(144, 120, 104), SkyTop = new(40, 24, 32), SkyHorizon = new(200, 88, 40),
        SkyBelow = new(96, 32, 16), SkySun = new(255, 160, 64), SkyClouds = new(72, 56, 56), CloudAmount = 0.55, SkyHills = new(48, 32, 32),
    };

    public static LevelTheme Castle { get; } = new("castle", "Royal Castle", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("toasty", 28), [SurfaceRole.GroundAlt] = new("artisans", 39), [SurfaceRole.Path] = new("hauntedtowers", 58),
        [SurfaceRole.Dirt] = new("stonehill", 11), [SurfaceRole.Sand] = new("stonehill", 30), [SurfaceRole.Cliff] = new("hauntedtowers", 64),
        [SurfaceRole.CliffTop] = new("hauntedtowers", 7), [SurfaceRole.Rock] = new("hauntedtowers", 59), [SurfaceRole.CaveWall] = new("hauntedtowers", 64),
        [SurfaceRole.Ceiling] = new("hauntedtowers", 59), [SurfaceRole.Brick] = new("hauntedtowers", 16), [SurfaceRole.Paving] = new("stonehill", 21),
        [SurfaceRole.Roof] = new("toasty", 68), [SurfaceRole.Wood] = new("artisans", 24), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("hauntedtowers", 64), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(72, 148, 64), C(96, 160, 80), C(176, 172, 164), C(120, 92, 64), C(212, 192, 140), C(170, 168, 160), C(216, 220, 216),
        C(176, 48, 40), C(130, 88, 52), null!), "townsquare", [26, 4, 31], ["ROYAL", "GRAND", "KINGS", "NOBLE", "SILVER", "CROWN"],
        ["KEEP", "CITADEL", "BASTION", "RAMPARTS", "COURT", "FORTRESS"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Castle] = 5, [LevelStyle.Valley] = 1, [LevelStyle.SkyIslands] = 1 },
        SkyTop = new(80, 128, 216), SkyHorizon = new(200, 220, 244),
    };

    public static LevelTheme Night { get; } = new("night", "Moonlit Night", new Dictionary<SurfaceRole, ThemeTexture>
    {
        [SurfaceRole.Ground] = new("darkpassage", 23), [SurfaceRole.GroundAlt] = new("darkpassage", 36), [SurfaceRole.Path] = new("darkpassage", 37),
        [SurfaceRole.Dirt] = new("darkpassage", 35), [SurfaceRole.Sand] = new("darkpassage", 38), [SurfaceRole.Cliff] = new("darkpassage", 3),
        [SurfaceRole.CliffTop] = new("darkpassage", 29), [SurfaceRole.Rock] = new("darkpassage", 12), [SurfaceRole.CaveWall] = new("darkpassage", 3),
        [SurfaceRole.Ceiling] = new("darkpassage", 12), [SurfaceRole.Brick] = new("darkpassage", 18), [SurfaceRole.Paving] = new("darkpassage", 34),
        [SurfaceRole.Roof] = new("dreamweavers", 40), [SurfaceRole.Wood] = new("treetops", 48), [SurfaceRole.Water] = Water,
        [SurfaceRole.Lava] = Lava, [SurfaceRole.Goo] = Goo, [SurfaceRole.Underside] = new("darkpassage", 3), [SurfaceRole.Charge] = Chevrons,
    }, Palette(C(120, 152, 128), C(132, 160, 140), C(176, 180, 168), C(104, 100, 96), C(184, 184, 176), C(88, 92, 104), C(200, 176, 192),
        C(64, 112, 136), C(104, 76, 56), C(40, 120, 200)), "darkpassage", [12, 4, 29, 28], ["MOONLIT", "SILENT", "SHADOW", "STARRY", "GHOSTLY", "MIDNIGHT"],
        ["PASSAGE", "HOLLOW", "TOWERS", "GROTTO", "RUINS", "CRYPT"])
    {
        Styles = new Dictionary<LevelStyle, double> { [LevelStyle.Caverns] = 3, [LevelStyle.Castle] = 3, [LevelStyle.Valley] = 2, [LevelStyle.SkyIslands] = 1 },
        SunElevation = 55, Ambient = 0.5, Light = new(104, 112, 144), SkyTop = new(8, 12, 40), SkyHorizon = new(48, 64, 112), SkyBelow = new(16, 24, 48),
        SkySun = new(232, 236, 255), SkyClouds = new(56, 64, 96), CloudAmount = 0.3, Stars = true, SkyHills = new(20, 28, 48),
    };

    public static IReadOnlyList<LevelTheme> All { get; } = [Meadow, Highland, Sky, Desert, Winter, Swamp, Volcano, Castle, Night];

    public static LevelTheme? Find(string? key) => All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>A theme for <paramref name="style"/>, by the themes' weights for it.</summary>
    public static LevelTheme For(LevelStyle style, LevelRandom random) =>
        random.Pick<LevelTheme>([.. All.Select(t => (t, t.Styles.TryGetValue(style, out double w) ? w : 0.25))]);
}
