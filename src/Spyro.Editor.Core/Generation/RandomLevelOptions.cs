namespace Spyro.Editor.Core.Generation;

/// <summary>The kind of level the generator lays out.</summary>
public enum LevelStyle
{
    /// <summary>Clearings among steep green banks, joined by sloping paths (Artisans, Stone Hill, Dark Hollow).</summary>
    Valley,
    /// <summary>Islands in water, joined by bridges, stepping stones and glides (Twilight Harbor, Cliff Town's water).</summary>
    Islands,
    /// <summary>Islands floating in the sky with nothing below (Lofty Castle, Dream Weavers).</summary>
    SkyIslands,
    /// <summary>Caves under rock joined by tunnels, with an open entrance (High Caves, Dark Passage).</summary>
    Caverns,
    /// <summary>Rooms and trails between tall rock walls (Dry Canyon, Peace Keepers).</summary>
    Canyon,
    /// <summary>Courtyards behind thick walls, towers and a moat (Town Square, Haunted Towers).</summary>
    Castle,
}

public enum LevelSize { Small, Medium, Large }

/// <summary>How hard the level is to get through. Every jump, glide and climb stays inside what Spyro can do; what
/// changes is how much room there is to spare: easy levels have wide paths, bridges and short hops; hard ones narrow
/// paths, stepping stones, more ledges and lookouts, and water that costs a life; Kaizo leaves next to nothing to spare.</summary>
public enum LevelDifficulty { Easy, Medium, Hard, Kaizo }

/// <summary>Where the level's pictures come from: the textures of the level its look is taken from (the ones that level
/// itself uses for its ground, banks, rock and paths), pictures the generator paints in the look's colours, or the user's
/// own pictures for some or all surfaces.</summary>
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
    public LevelDifficulty Difficulty { get; init; } = LevelDifficulty.Medium;
    /// <summary>How the level is laid out: "journey" (a way from room to room with a clear end), "loop" (a ring that
    /// comes back over its start), "hub" (a clearing with rooms off it all round), "arena" (one great room with a hill,
    /// a mound or a moat in its middle), "climb" (a hill of terraces), "gorge" (a winding gorge), "web" (rooms with
    /// more than one way round); null leaves it to the seed.</summary>
    public string? Layout { get; init; }
    /// <summary>A key of <see cref="LevelThemes.All"/>; null picks one that suits the style.</summary>
    public string? Theme { get; init; }
    public bool? Caves { get; init; }
    /// <summary>A ramp too steep to walk, climbed by charging, up to a hidden nook.</summary>
    public bool? ChargePaths { get; init; }
    /// <summary>Gaps and high ledges crossed by gliding.</summary>
    public bool? Glides { get; init; }
    /// <summary>Water, lava or ooze pools and moats.</summary>
    public bool? Hazards { get; init; }
    /// <summary>Bridges over water and chasms.</summary>
    public bool? Bridges { get; init; }
    /// <summary>Hidden nooks off the level's rooms, with room for treasure.</summary>
    public bool? Secrets { get; init; }
    /// <summary>Rivers (of the level's water, ooze or lava) across big rooms, with bridges.</summary>
    public bool? Rivers { get; init; }
    /// <summary>Houses round the rooms, a village or two.</summary>
    public bool? Houses { get; init; }
    /// <summary>Castle walls with towers along rooms' edges, and battlements along a castle's walls.</summary>
    public bool? Walls { get; init; }
    /// <summary>The liquid of the level's rivers, marshes, pools and moats (Water, Goo for ooze, Lava); null: the look's
    /// own as a rule, now and then another.</summary>
    public SurfaceRole? Liquid { get; init; }
    public LevelTextureMode Textures { get; init; } = LevelTextureMode.GameTextures;
    /// <summary>The user's own pictures (PNG or JPEG, any size) for roles, in place of the theme's.</summary>
    public IReadOnlyDictionary<SurfaceRole, string> Pictures { get; init; } = new Dictionary<SurfaceRole, string>();
    /// <summary>True: a sky painted in the look's colours; false: the sky of the level the look comes from.</summary>
    public bool PaintedSky { get; init; }
    /// <summary>The level's name (capital letters, spaces and apostrophes); null makes one up.</summary>
    public string? Name { get; init; }
    /// <summary>A homeworld: this many doorways (0: an ordinary level). The level is then a plaza where Spyro arrives
    /// with rooms off it all round, and a place for each doorway at a room's far side with open floor in front of it
    /// (<see cref="RandomLevelResult.Doorways"/>).</summary>
    public int Doorways { get; init; }
}
