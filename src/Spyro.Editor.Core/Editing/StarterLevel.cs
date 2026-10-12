using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>What File ▾ › New Level and Start Blank begin with: a meadow that already looks like a Spyro level — 12 × 12
/// tiles of grass (128 units each) with a stone path from the start northwards, a stone ramp up to a raised grassy ledge
/// with stone sides, and a pond with a sand rim; Artisans' sky. Every face is an ordinary built-level face: the Terrain
/// tab's Ground card repaints it, and it can be moved or removed like any other.</summary>
public static class StarterLevel
{
    public const float Tile = 128, Floor = 544, Ledge = Floor + 120;
    /// <summary>The ramp's first row; it climbs 40 units a row over three rows (about 17°: Spyro stops at the foot of a 32°
    /// ramp). Corners must sit on whole editor units (the game's 16-unit grid).</summary>
    public const int RampStart = 7;
    public const int Size = 12;
    /// <summary>The meadow's south-west corner: away from the map's edge (coordinates cannot go below 0), so tiles can
    /// be added on every side.</summary>
    public const float Origin = 2048;

    public static SceneDocument Create(string name, LevelCatalog catalog)
    {
        // Without the original levels in the catalog (a bare test workspace) the meadow is drawn in plain colours.
        bool textured = GroundTypes.All.All(g => catalog.FindByKey(g.LevelKey) != null);
        AuthoredSurface Face(GroundType ground, params Vector3f[] corners) => textured
            ? ground.Apply(new(Guid.NewGuid().ToString("N"), corners, new(128, 128, 128), null), catalog)
            : new(Guid.NewGuid().ToString("N"), corners, ground.Plain, null) { Kind = ground.Kind };
        AuthoredSurface Wall(Vector3f[] corners) =>
            Face(GroundTypes.Stone, corners) with { Color = new(96, 96, 96) };
        List<AuthoredSurface> faces = [];
        static bool Path(int i, int j) => i is 5 or 6 && j is >= 1 and < RampStart;
        static bool Ramp(int i, int j) => i is 5 or 6 && j is >= RampStart and < 10;
        static bool OnLedge(int i, int j) => i is >= 3 and <= 8 && j >= 10;
        static bool Pond(int i, int j) => i is 8 or 9 && j is 3 or 4;
        static bool Rim(int i, int j) => i is >= 7 and <= 10 && j is >= 2 and <= 5 && !Pond(i, j);
        float RampZ(float y) => Floor + (Ledge - Floor) * Math.Clamp((y - Origin - RampStart * Tile) / ((10 - RampStart) * Tile), 0, 1);
        for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
            {
                float x0 = Origin + i * Tile, y0 = Origin + j * Tile, x1 = x0 + Tile, y1 = y0 + Tile;
                if (Ramp(i, j))
                {
                    faces.Add(Face(GroundTypes.Stone, new(x0, y0, RampZ(y0)), new(x1, y0, RampZ(y0)), new(x1, y1, RampZ(y1)), new(x0, y1, RampZ(y1))));
                    continue;
                }
                float z = OnLedge(i, j) ? Ledge : Floor;
                var ground = Pond(i, j) ? GroundTypes.Water : Rim(i, j) ? GroundTypes.Sand : Path(i, j) ? GroundTypes.Stone : GroundTypes.Grass;
                faces.Add(Face(ground, new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)));
            }
        // The ledge's and the ramp's sides. A wall is solid and drawn from the side its corners turn anticlockwise from:
        // walk its bottom edge with that side (the outside) on your right.
        static Vector3f P(float x, float y, float z) => new(x, y, z);
        float south = Origin + 10 * Tile, west = Origin + 3 * Tile, east = Origin + 9 * Tile;
        void Side(float ax, float ay, float bx, float by) =>
            faces.Add(Wall([P(ax, ay, Floor), P(bx, by, Floor), P(bx, by, Ledge), P(ax, ay, Ledge)]));
        for (int i = 3; i <= 8; i++)
            if (i is not (5 or 6)) Side(Origin + i * Tile, south, Origin + (i + 1) * Tile, south);   // faces south: walk west to east
        for (int j = 10; j < Size; j++)
        {
            Side(west, Origin + (j + 1) * Tile, west, Origin + j * Tile);   // faces west: walk north to south
            Side(east, Origin + j * Tile, east, Origin + (j + 1) * Tile);   // faces east: walk south to north
        }
        // The ramp's sides, row by row: west (x 640, walk north to south) and east (x 896, south to north); the first
        // row, where the ramp leaves the floor, is a triangle.
        for (int j = RampStart; j < 10; j++)
        {
            float ya = Origin + j * Tile, yb = Origin + (j + 1) * Tile, za = RampZ(ya), zb = RampZ(yb);
            float xw = Origin + 5 * Tile, xe = Origin + 7 * Tile;
            bool first = j == RampStart;
            faces.Add(Wall(first ? [P(xw, yb, Floor), P(xw, ya, Floor), P(xw, yb, zb)]
                : [P(xw, yb, Floor), P(xw, ya, Floor), P(xw, ya, za), P(xw, yb, zb)]));
            faces.Add(Wall(first ? [P(xe, ya, Floor), P(xe, yb, Floor), P(xe, yb, zb)]
                : [P(xe, ya, Floor), P(xe, yb, Floor), P(xe, yb, zb), P(xe, ya, za)]));
        }
        var artisans = catalog.FindByKey("artisans");
        return SceneDocument.New(name) with
        {
            Spawn = new(Origin + 6 * Tile, Origin + 2 * Tile, Floor + 32), Heading = 64, Surfaces = faces,
            SkyColor = new(104, 152, 208), SkySourceWadEntry = artisans?.SourceWadEntry
        };
    }
}
