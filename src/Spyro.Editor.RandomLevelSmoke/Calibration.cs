using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

/// <summary>Lab only: a level for measuring Spyro's movement in the emulator (run and charge on a plain, steps of known
/// heights, ramps of known angles, a tower to glide from, a supercharge strip and a steep supercharge ramp).</summary>
internal static class Calibration
{
    public const float O = 2048, Plain = 600, Tile = 256;
    public static readonly int[] StepHeights = [16, 24, 32, 40, 48, 56, 64, 80];
    public static readonly int[] RampAngles = [10, 15, 20, 25, 30, 35, 40, 50];

    public static SceneDocument Build(LevelCatalog catalog)
    {
        List<AuthoredSurface> faces = [];
        AuthoredSurface Face(GroundType g, params Vector3f[] v) => g.Apply(new(Guid.NewGuid().ToString("N"), v, new(128, 128, 128), null), catalog);
        void Floor(GroundType g, float x0, float y0, float x1, float y1, float z) => faces.Add(Face(g, new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)));
        void Wall(float ax, float ay, float bx, float by, float zb, float zt) => faces.Add(Face(GroundTypes.Stone, new(ax, ay, zb), new(bx, by, zb), new(bx, by, zt), new(ax, ay, zt)) with { Color = new(96, 96, 96) });
        void Block(float x0, float y0, float x1, float y1, float zb, float zt, GroundType top)
        {
            Floor(top, x0, y0, x1, y1, zt);
            Wall(x0, y0, x1, y0, zb, zt); Wall(x1, y1, x0, y1, zb, zt); Wall(x0, y1, x0, y0, zb, zt); Wall(x1, y0, x1, y1, zb, zt);
        }
        // Plain, with a supercharge strip (sand) along x tiles 12-13, y tiles 2-13.
        for (int j = 0; j < 16; j++)
            for (int i = 0; i < 20; i++)
            {
                bool strip = i is 12 or 13 && j is >= 2 and <= 13;
                var g = strip ? GroundTypes.Sand : GroundTypes.Grass;
                var f = Face(g, new(O + i * Tile, O + j * Tile, Plain), new(O + (i + 1) * Tile, O + j * Tile, Plain), new(O + (i + 1) * Tile, O + (j + 1) * Tile, Plain), new(O + i * Tile, O + (j + 1) * Tile, Plain));
                faces.Add(strip ? f with { Kind = AuthoredSurfaceKind.Supercharge } : f);
            }
        // Steps: 256 x 256 blocks, 128 apart, along y 1536-1792.
        for (int k = 0; k < StepHeights.Length; k++)
        {
            float x0 = O + 256 + k * 384;
            Block(x0, O + 1536, x0 + 256, O + 1792, Plain, Plain + StepHeights[k], GroundTypes.Stone);
        }
        // Ramps rising 128 along +y from y 2304, each with a 256-deep landing at its top.
        for (int k = 0; k < RampAngles.Length; k++)
        {
            float x0 = O + 256 + k * 384, x1 = x0 + 256, ya = O + 2304;
            float run = MathF.Round(128 / MathF.Tan(RampAngles[k] * MathF.PI / 180));
            float yb = ya + run, top = Plain + 128;
            faces.Add(Face(GroundTypes.Dirt, new(x0, ya, Plain), new(x1, ya, Plain), new(x1, yb, top), new(x0, yb, top)));
            // Ramp sides: west walks north to south, east walks south to north.
            faces.Add(Face(GroundTypes.Stone, new(x0, yb, Plain), new(x0, ya, Plain), new(x0, yb, top)) with { Color = new(96, 96, 96) });
            faces.Add(Face(GroundTypes.Stone, new(x1, ya, Plain), new(x1, yb, Plain), new(x1, yb, top)) with { Color = new(96, 96, 96) });
            Block(x0, yb, x1, yb + 256, Plain, top, GroundTypes.Stone);
        }
        // Glide tower: 500 above the plain, near the south-east.
        Block(O + 3328, O + 256, O + 3584, O + 512, Plain, Plain + 500, GroundTypes.Stone);
        // A second, lower tower to glide off in the other direction: 250 up.
        Block(O + 256, O + 256, O + 512, O + 512, Plain, Plain + 250, GroundTypes.Stone);
        // Steep supercharge ramp (45°, rising 160) at the strip's north end, then a landing.
        {
            float x0 = O + 12 * Tile, x1 = O + 14 * Tile, ya = O + 14 * Tile, yb = ya + 160, top = Plain + 160;
            faces.Add(Face(GroundTypes.Sand, new(x0, ya, Plain), new(x1, ya, Plain), new(x1, yb, top), new(x0, yb, top)) with { Kind = AuthoredSurfaceKind.Supercharge });
            faces.Add(Face(GroundTypes.Stone, new(x0, yb, Plain), new(x0, ya, Plain), new(x0, yb, top)) with { Color = new(96, 96, 96) });
            faces.Add(Face(GroundTypes.Stone, new(x1, ya, Plain), new(x1, yb, Plain), new(x1, yb, top)) with { Color = new(96, 96, 96) });
            Block(x0, yb, x1, O + 16 * Tile, Plain, top, GroundTypes.Stone);
        }
        // Round 2: small and tall steps along y 3584-3840.
        int[] more = [4, 8, 12, 96, 112, 128];
        for (int k = 0; k < more.Length; k++)
        {
            float x0 = O + 256 + k * 384;
            Block(x0, O + 3584, x0 + 256, O + 3840, Plain, Plain + more[k], GroundTypes.Stone);
        }
        // Downhill ramps from platforms 200 up (x 4224 and 4736 from O), 15°, north from y 512 to the plain: the first
        // supercharge, the second plain dirt.
        for (int k = 0; k < 2; k++)
        {
            float x0 = O + 4224 + k * 512, x1 = x0 + 256, ya = O + 512, top = Plain + 200;
            float yb = ya + MathF.Round(200 / MathF.Tan(15 * MathF.PI / 180));
            Block(x0, O + 256, x1, ya, Plain, top, GroundTypes.Stone);
            var ramp = Face(k == 0 ? GroundTypes.Sand : GroundTypes.Dirt, new(x0, ya, top), new(x1, ya, top), new(x1, yb, Plain), new(x0, yb, Plain));
            faces.Add(k == 0 ? ramp with { Kind = AuthoredSurfaceKind.Supercharge } : ramp);
            faces.Add(Face(GroundTypes.Stone, new(x0, ya, Plain), new(x0, ya, top), new(x0, yb, Plain)) with { Color = new(96, 96, 96) });
            faces.Add(Face(GroundTypes.Stone, new(x1, yb, Plain), new(x1, ya, top), new(x1, ya, Plain)) with { Color = new(96, 96, 96) });
        }
        var artisans = catalog.FindByKey("artisans");
        return SceneDocument.New("CALIBRATION") with
        {
            Spawn = new(O + 640, O + 640, Plain + 32), Heading = 64, Surfaces = faces,
            SkyColor = new(104, 152, 208), SkySourceWadEntry = artisans?.SourceWadEntry
        };
    }
}
