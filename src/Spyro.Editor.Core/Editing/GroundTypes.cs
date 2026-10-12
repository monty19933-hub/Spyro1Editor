using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>A ground the Terrain tab offers for a level you build: an original level's texture (Create BIN copies it
/// into the level), the shade it is drawn with (128 = as in the original) and what Spyro finds on it (null: solid).</summary>
public sealed record GroundType(string Name, string LevelKey, int TextureId, ColorRgba Shade, AuthoredSurfaceKind? Kind, string Note)
{
    /// <summary>The ground's colour without its texture (an untextured stand-in).</summary>
    public ColorRgba Plain => Name switch
    {
        "Grass" => new(64, 128, 48), "Dirt" => new(112, 88, 64), "Stone" => new(120, 120, 128), "Sand" => new(200, 150, 88),
        "Snow" => new(220, 224, 232), "Wood" => new(120, 80, 48), "Water" => new(48, 120, 216), "Lava" => new(216, 88, 32),
        "Goo" => new(144, 48, 160), _ => new(128, 128, 128)
    };

    /// <summary>The texture painted in one of the game's own levels when it is not <see cref="LevelKey"/>/<see cref="TextureId"/>:
    /// those levels keep a tile's colours in tables shared with its neighbours, so <see cref="Shade"/> cannot be applied
    /// there, and a ground whose look comes from its shade needs a texture that has that colour itself.</summary>
    public string OriginalLevelKey { get; init; } = LevelKey;
    public int OriginalLevelTextureId { get; init; } = TextureId;

    public AuthoredMaterial Material(LevelCatalog catalog) =>
        new((catalog.FindByKey(LevelKey) ?? throw new InvalidDataException($"The level catalog has no {LevelKey}.")).SourceWadEntry, TextureId);

    /// <summary>The face painted with this ground: its texture, shade and ground kind; shape and sides are kept.</summary>
    public AuthoredSurface Apply(AuthoredSurface face, LevelCatalog catalog) =>
        face with { Material = Material(catalog), Color = Shade, NearColors = null, FarColors = null, Kind = Kind };
}

/// <summary>The grounds of <see cref="GroundType"/>, chosen from textures that copy cleanly into a built level (static
/// ones: animated water and lava records are refused by <c>NativeStaticTerrainTextureSource</c>).</summary>
public static class GroundTypes
{
    private static readonly ColorRgba Plain = new(128, 128, 128);

    public static GroundType Grass { get; } = new("Grass", "artisans", 30, Plain, null, "Artisans' grass.");
    public static GroundType Dirt { get; } = new("Dirt", "stonehill", 11, Plain, null, "Stone Hill's earth.");
    public static GroundType Stone { get; } = new("Stone", "stonehill", 21, Plain, null, "Stone Hill's paving stones.");
    public static GroundType Sand { get; } = new("Sand", "drycanyon", 20, Plain, null, "Dry Canyon's sand.");
    public static GroundType Snow { get; } = new("Snow", "icecavern", 0, Plain, null, "Ice Cavern's snow.");
    public static GroundType Wood { get; } = new("Wood", "treetops", 40, Plain, null, "Tree Tops' planks.");
    public static GroundType Water { get; } = new("Water", "alpineridge", 36, Plain, AuthoredSurfaceKind.Water,
        "Spyro sinks and loses a life, as in every Spyro the Dragon level: he cannot swim.");
    public static GroundType Lava { get; } = new("Lava", "gnastygnorc", 14, new(200, 92, 52), AuthoredSurfaceKind.Lava,
        "Spyro burns and loses a life.")
    {
        // Gnasty Gnorc's texture is lava only with the orange shade; in the game's own levels Night Flight's molten rock,
        // which is red-orange by itself, is painted instead.
        OriginalLevelKey = "nightflight", OriginalLevelTextureId = 6
    };
    public static GroundType Goo { get; } = new("Goo", "drycanyon", 24, Plain, AuthoredSurfaceKind.Ooze,
        "Spyro sinks into it and loses a life.");

    public static IReadOnlyList<GroundType> All { get; } = [Grass, Dirt, Stone, Sand, Snow, Wood, Water, Lava, Goo];

    public static GroundType? Find(string name) => All.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The ground a face shows, when it is one of these (same texture and kind).</summary>
    public static GroundType? Of(AuthoredSurface face, LevelCatalog catalog) =>
        face.Material is { } m ? All.FirstOrDefault(g => g.TextureId == m.TextureId && catalog.FindByKey(g.LevelKey)?.SourceWadEntry == m.SourceWadEntry && g.Kind == face.Kind) : null;
}
