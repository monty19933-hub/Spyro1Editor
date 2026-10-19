namespace Spyro.Editor.Core.Generation;

/// <summary>The kind of level the generator lays out.</summary>
public enum LevelStyle
{
    /// <summary>One big walled-in area of terraces, ramps and ledges (Dark Hollow, Artisans).</summary>
    Valley,
    /// <summary>Islands in water, joined by bridges, stepping stones and glides (Twilight Harbor, Cliff Town's water).</summary>
    Islands,
    /// <summary>Islands floating in the sky with nothing below (Lofty Castle, Dream Weavers).</summary>
    SkyIslands,
    /// <summary>Caves under rock joined by tunnels, with an open entrance (High Caves, Dark Passage).</summary>
    Caverns,
    /// <summary>A long winding trail between tall cliffs, with side areas (Stone Hill, Dry Canyon).</summary>
    Canyon,
    /// <summary>Courtyards behind thick walls, towers and a moat (Town Square, Haunted Towers).</summary>
    Castle,
}

public enum LevelSize { Small, Medium, Large }

/// <summary>Where the level's pictures come from: the original levels' textures chosen by role (grass on floors, rock
/// on cliffs, planks on bridges…), pictures the generator paints in the theme's colours, or the user's own pictures for
/// some or all roles.</summary>
public enum LevelTextureMode { GameTextures, GeneratedPictures }

/// <summary>What a face of the level is for; each theme gives every role a texture.</summary>
public enum SurfaceRole
{
    Ground, GroundAlt, Path, Dirt, Sand, Cliff, CliffTop, Rock, CaveWall, Ceiling, Brick, Paving, Roof, Wood,
    Water, Lava, Goo, Underside, Charge,
}

/// <summary>Everything the generator is asked for. A null choice is left to the seed ("surprise me").</summary>
public sealed record RandomLevelOptions
{
    public long Seed { get; init; }
    public LevelStyle? Style { get; init; }
    public LevelSize Size { get; init; } = LevelSize.Medium;
    /// <summary>A key of <see cref="LevelThemes.All"/>; null picks one that suits the style.</summary>
    public string? Theme { get; init; }
    public bool? Caves { get; init; }
    /// <summary>Supercharge paths: long straight runways and steep ramps Spyro climbs by charging.</summary>
    public bool? ChargePaths { get; init; }
    /// <summary>Gaps and high ledges crossed by gliding.</summary>
    public bool? Glides { get; init; }
    /// <summary>Water, lava or ooze pools and moats.</summary>
    public bool? Hazards { get; init; }
    /// <summary>Bridges over water and chasms.</summary>
    public bool? Bridges { get; init; }
    /// <summary>Hidden ledges and platforms with room for treasure.</summary>
    public bool? Secrets { get; init; }
    public LevelTextureMode Textures { get; init; } = LevelTextureMode.GameTextures;
    /// <summary>The user's own pictures (PNG or JPEG, any size) for roles, in place of the theme's.</summary>
    public IReadOnlyDictionary<SurfaceRole, string> Pictures { get; init; } = new Dictionary<SurfaceRole, string>();
    /// <summary>True: a sky painted in the theme's colours; false: the theme's original level's sky.</summary>
    public bool PaintedSky { get; init; } = true;
    /// <summary>The level's name (capital letters, spaces and apostrophes); null makes one up.</summary>
    public string? Name { get; init; }
}
