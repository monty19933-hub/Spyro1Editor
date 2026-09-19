using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>Closed neutral content union. Exactly one variant owns geometry and common settings.</summary>
public abstract class NativeFreshLevelContent
{
    private protected NativeFreshLevelContent() { }
    public abstract int SpawnX { get; }
    public abstract int SpawnY { get; }
    public abstract int SpawnZ { get; }
    public abstract int Heading { get; }
    public abstract ColorRgba SkyColor { get; }
    public abstract int MusicTrackId { get; }
    /// <summary>Point containment only; no ground, body-footprint, route or gameplay proof.</summary>
    public abstract bool ContainsInteriorXY(int x, int y);
}

/// <summary>The unchanged legacy Base and nullable material pair remain the sole legacy authorities.</summary>
public sealed class NativeFreshLegacyLevelContent : NativeFreshLevelContent
{
    internal NativeFreshLegacyLevelContent(NativeId65FreshDocumentIntent @base, NativeFreshFloorMaterialsIntent? materials)
    { Base = @base; FloorMaterials = materials; }
    public NativeId65FreshDocumentIntent Base { get; }
    public NativeFreshFloorMaterialsIntent? FloorMaterials { get; }
    public override int SpawnX => Base.SpawnX;
    public override int SpawnY => Base.SpawnY;
    public override int SpawnZ => Base.SpawnZ;
    public override int Heading => Base.Heading;
    public override ColorRgba SkyColor => Base.SkyColor;
    public override int MusicTrackId => Base.MusicTrackId;
    public override bool ContainsInteriorXY(int x, int y) =>
        x > Base.FloorX && x < checked(Base.FloorX + Base.FloorWidth) &&
        y > Base.FloorY && y < checked(Base.FloorY + Base.FloorDepth);
}

/// <summary>One immutable ordered native flat tile and its two neutral material choices.</summary>
public sealed class NativeFreshFlatTileIntent
{
    private NativeFreshFlatTileIntent(string editorId, int x, int y, int z, int width, int depth,
        ColorRgba floorColor, NativeFreshFloorMaterialsIntent materials)
    { EditorId = editorId; X = x; Y = y; Z = z; Width = width; Depth = depth; FloorColor = floorColor; Materials = materials; }
    public string EditorId { get; }
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public int Width { get; }
    public int Depth { get; }
    public ColorRgba FloorColor { get; }
    public NativeFreshFloorMaterialsIntent Materials { get; }

    public static NativeFreshFlatTileIntent Create(string editorId, int x, int y, int z, int width, int depth,
        ColorRgba floorColor, NativeFreshFloorMaterialsIntent materials)
    {
        NativeFreshEditorId.Require(editorId);
        ArgumentNullException.ThrowIfNull(materials);
        var tile = new NativeFreshFlatTileIntent(editorId, x, y, z, width, depth, floorColor, materials);
        // The shared actual producer, not a parallel approximate geometry grammar.
        _ = NativeFlatTileSurfaceLayout.Compile([tile.ToNativeTile()], 0);
        return tile;
    }

    internal NativeFlatSurfaceTile ToNativeTile() => new(X, Y, Z, Width, Depth, ColorWord(FloorColor));
    internal static uint ColorWord(ColorRgba color)
    {
        if (color.A != 255) throw new ArgumentException("Native tiled display colors must be opaque.", nameof(color));
        return (uint)(color.R | color.G << 8 | color.B << 16);
    }
}

/// <summary>
/// Schema4's actual surface, spawn and common settings. Geometry is validated by
/// the shared native flat-tile producer; this conveys no source or export authority.
/// </summary>
public sealed class NativeFreshTiledLevelContent : NativeFreshLevelContent
{
    public const int MaximumTiles = 255;
    private NativeFreshTiledLevelContent(IReadOnlyList<NativeFreshFlatTileIntent> tiles,
        int spawnX, int spawnY, int spawnZ, int heading, ColorRgba skyColor, int musicTrackId)
    { Tiles = tiles; SpawnX = spawnX; SpawnY = spawnY; SpawnZ = spawnZ; Heading = heading; SkyColor = skyColor; MusicTrackId = musicTrackId; }
    public IReadOnlyList<NativeFreshFlatTileIntent> Tiles { get; }
    public override int SpawnX { get; }
    public override int SpawnY { get; }
    public override int SpawnZ { get; }
    public override int Heading { get; }
    public override ColorRgba SkyColor { get; }
    public override int MusicTrackId { get; }

    public static NativeFreshTiledLevelContent Create(IEnumerable<NativeFreshFlatTileIntent> tiles,
        int spawnX, int spawnY, int spawnZ, int heading, ColorRgba skyColor, int musicTrackId)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        uint sky = NativeFreshFlatTileIntent.ColorWord(skyColor);
        NativeId65FreshSceneLayout.ValidateAuthoredInput(new(spawnX, spawnY, spawnZ, heading),
            NativeId65FreshDocumentIntent.NominalDynamicCapacity);
        if (MusicTrackCatalog.Find(musicTrackId)?.IsSelectable != true)
            throw new ArgumentOutOfRangeException(nameof(musicTrackId), "Unknown or reserved native music track.");
        List<NativeFreshFlatTileIntent> copy = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (var tile in tiles)
        {
            if (copy.Count >= MaximumTiles || tile == null || !ids.Add(tile.EditorId))
                throw new InvalidDataException("Tiled content requires at most255 non-null tiles with unique stable editor IDs.");
            copy.Add(tile);
        }
        _ = NativeFlatTileSurfaceLayout.Compile(copy.Select(t => t.ToNativeTile()), sky);
        var result = new NativeFreshTiledLevelContent(copy.AsReadOnly(), spawnX, spawnY, spawnZ, heading, skyColor, musicTrackId);
        if (!result.ContainsInteriorXY(spawnX, spawnY, spawnZ - NativeId65FreshDocumentIntent.SpawnHeightAboveFloor))
            throw new InvalidDataException("Tiled spawn must be exactly512 above a matching plane and strictly inside its surface union; no footprint clearance is implied.");
        return result;
    }

    public NativeFreshTiledLevelContent WithTiles(IEnumerable<NativeFreshFlatTileIntent> tiles) =>
        Create(tiles, SpawnX, SpawnY, SpawnZ, Heading, SkyColor, MusicTrackId);
    public NativeFreshTiledLevelContent WithSettings(int spawnX, int spawnY, int spawnZ, int heading,
        ColorRgba skyColor, int musicTrackId) => Create(Tiles, spawnX, spawnY, spawnZ, heading, skyColor, musicTrackId);

    public override bool ContainsInteriorXY(int x, int y) => ContainsInteriorXY(x, y, null);

    private bool ContainsInteriorXY(int x, int y, int? plane)
    {
        // A point is interior to a finite union of axis-aligned closed rectangles
        // exactly when all four local quadrants are covered. This admits internal
        // seams/T junctions but not outer edges, holes or corner-only contacts.
        int quadrants = 0;
        foreach (var tile in Tiles)
        {
            if (plane is { } z && tile.Z != z) continue;
            int right = tile.X + tile.Width, bottom = tile.Y + tile.Depth;
            bool leftSide = x > tile.X && x <= right, rightSide = x >= tile.X && x < right;
            bool topSide = y > tile.Y && y <= bottom, bottomSide = y >= tile.Y && y < bottom;
            if (leftSide && topSide) quadrants |= 1;
            if (rightSide && topSide) quadrants |= 2;
            if (leftSide && bottomSide) quadrants |= 4;
            if (rightSide && bottomSide) quadrants |= 8;
            if (quadrants == 15) return true;
        }
        return false;
    }
}

internal static class NativeFreshEditorId
{
    internal static void Require(string editorId)
    {
        ArgumentNullException.ThrowIfNull(editorId);
        if (editorId.Length != 32 || editorId.All(c => c == '0') ||
            editorId.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Editor ID requires exactly32 lowercase hex digits and a nonzero value.");
    }
}
