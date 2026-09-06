using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral, immutable authoring intent for one untextured floor rectangle.
/// Validation is grammar and point containment, not player-footprint clearance,
/// native source authority, allocator safety, runtime proof or export approval.
/// This is a distinct document family, never a reinterpretation of schemas1..6.
/// </summary>
public sealed class NativeId65FreshDocumentIntent
{
    public const int SpawnHeightAboveFloor = 512;
    public const int NominalDynamicCapacity = 16;
    public const int PlacedMobyCount = 0;

    private NativeId65FreshDocumentIntent(int floorX, int floorY, int floorZ, int floorWidth, int floorDepth,
        int spawnX, int spawnY, int heading, ColorRgba floorColor, ColorRgba skyColor, int musicTrackId)
    {
        FloorX = floorX; FloorY = floorY; FloorZ = floorZ;
        FloorWidth = floorWidth; FloorDepth = floorDepth;
        SpawnX = spawnX; SpawnY = spawnY; Heading = heading;
        FloorColor = floorColor; SkyColor = skyColor; MusicTrackId = musicTrackId;
    }

    public int FloorX { get; }
    public int FloorY { get; }
    public int FloorZ { get; }
    public int FloorWidth { get; }
    public int FloorDepth { get; }
    public int SpawnX { get; }
    public int SpawnY { get; }
    public int SpawnZ => checked(FloorZ + SpawnHeightAboveFloor);
    public int Heading { get; }
    public ColorRgba FloorColor { get; }
    public ColorRgba SkyColor { get; }
    public int MusicTrackId { get; }

    public static NativeId65FreshDocumentIntent Create(int floorX, int floorY, int floorZ,
        int floorWidth, int floorDepth, int spawnX, int spawnY, int heading,
        ColorRgba floorColor, ColorRgba skyColor, int musicTrackId)
    {
        var floor = NativeId65AuthoredFloor.FromDisplayColors(floorX, floorY, floorZ,
            floorWidth, floorDepth, floorColor, skyColor);
        NativeId65AuthoredSurfaceLayout.ValidateIntent(floor);
        int spawnZ = checked(floorZ + SpawnHeightAboveFloor);
        NativeId65FreshSceneLayout.ValidateAuthoredInput(new(spawnX, spawnY, spawnZ, heading), NominalDynamicCapacity);
        if (spawnX <= floorX || spawnX >= checked(floorX + floorWidth) ||
            spawnY <= floorY || spawnY >= checked(floorY + floorDepth))
            throw new InvalidDataException("Fresh intent requires the spawn point strictly inside its floor rectangle; no player-radius clearance is implied.");
        if (MusicTrackCatalog.Find(musicTrackId)?.IsSelectable != true)
            throw new ArgumentOutOfRangeException(nameof(musicTrackId), "Unknown or reserved native music track.");
        return new(floorX, floorY, floorZ, floorWidth, floorDepth,
            spawnX, spawnY, heading, floorColor, skyColor, musicTrackId);
    }
}
