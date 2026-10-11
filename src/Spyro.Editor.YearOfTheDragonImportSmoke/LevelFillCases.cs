using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

/// <summary>Fill In What the Level Needs (<see cref="LevelFill"/>) on a small built level with no disc: three floors
/// joined by a ramp, a ledge Spyro can only glide down to over lava, and a high block he can't climb onto.</summary>
static class LevelFillCases
{
    static int checks;
    static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("check failed: " + what); checks++; }

    public static int Run()
    {
        // Start floor A (0..1000 x 0..1000 at 500); ramp up to floor B (1000..2000 at 560); far floor C (2600..3600 at 300)
        // reached by gliding over lava (2000..2600 at 100); block D (400..600, 400..600, top at 800) too high to climb.
        List<AuthoredSurface> s = [];
        void Quad(float x0, float y0, float x1, float y1, float z0, float z1, AuthoredSurfaceKind? kind = null, bool alongX = true)
        {
            for (float x = x0; x < x1; x += 100)
                for (float y = y0; y < y1; y += 100)
                {
                    float Z(float px, float py) => alongX ? z0 + (z1 - z0) * (px - x0) / (x1 - x0) : z0;
                    s.Add(new(Guid.NewGuid().ToString("N"), [new(x, y, Z(x, y)), new(x + 100, y, Z(x + 100, y)), new(x + 100, y + 100, Z(x + 100, y + 100)), new(x, y + 100, Z(x, y + 100))],
                        new(128, 128, 128), null) { Kind = kind });
                }
        }
        Quad(0, 0, 1000, 1000, 500, 500);
        Quad(1000, 0, 1100, 1000, 500, 560);           // ramp, 60 up over 100
        Quad(1100, 0, 2000, 1000, 560, 560);
        Quad(2000, 0, 2600, 1000, 100, 100, AuthoredSurfaceKind.DeadlyLava);
        Quad(2600, 0, 3600, 1000, 300, 300);
        Quad(400, 400, 600, 600, 800, 800);           // block top, 300 above the start floor
        var scene = SceneDocument.New("FILL TEST") with { Spawn = new(100, 500, 520), Surfaces = s };

        var grid = new LevelFill.FloorGrid(scene);
        var walk = grid.Walk(scene.Spawn);
        bool Reached(float x, float y, float z) => walk.Keys.Any(c => Math.Abs(grid.Point(c).X - x) < 32 && Math.Abs(grid.Point(c).Y - y) < 32 && Math.Abs(grid.Point(c).Z - z) < 8);
        Check(Reached(1500, 500, 560), "up the ramp");
        Check(Reached(3000, 500, 300), "a glide over the lava to the far floor");
        Check(!Reached(500, 500, 800), "not onto the block 300 above");
        Check(!walk.Keys.Any(c => grid.Point(c).Z < 200), "never onto the lava");

        var plan = LevelFill.Make(scene, 3, [8], true, 200);
        Check(plan.Dragons.Count == 3 && plan.Thieves.Count == 1 && plan.Exit != null, "three dragons, a thief and Return Home placed");
        Check(plan.Exit!.Position.X > 2600, "Return Home at the far end (the far floor)");
        Check(plan.Dragons.All(d => plan.Dragons.All(e => d == e || Math.Abs(d.Position.X - e.Position.X) + Math.Abs(d.Position.Y - e.Position.Y) >= 360)), "dragons spread apart");
        Check(plan.Thieves[0].Route.Count == 8 && plan.Thieves[0].Route.All(p => grid.Floors(p.X, p.Y).Any(f => f.Plain && Math.Abs(f.Z - p.Z) < 1)),
            "the thief's run: 8 points, each on plain floor");
        Check(plan.Gems.Sum(g => g.Value) == 200 && plan.Gems.All(g => g.Value is 1 or 2 or 5 or 10 or 25), "gems worth exactly 200 in the game's own colours");
        Check(LevelFill.Make(scene, 0, [], false, 200, maxGems: 3).Gems is { Count: 3 } few && few.Sum(g => g.Value) == 200 && few.Any(g => g.Value >= 50),
            "three gems carry 200 with a teal or rainbow gem");

        // A walking route shrinks until it fits the floor; a flying one keeps its height over the floor.
        var home = new Vector3f(0, 0, 0);
        var wide = new[] { new Vector3f(-600, 0, 0), new Vector3f(600, 0, 0) };
        var fitted = LevelFill.FitRoute(grid, home, wide, new(500, 500, 500));
        Check(fitted != null && fitted.All(p => p.X >= 0 && p.X <= 1000 && Math.Abs(p.Z - 500) < 1), "a wide patrol shrinks onto the start floor");
        var flight = LevelFill.FitRoute(grid, home, [new(-100, 0, 400), new(100, 0, 400)], new(500, 500, 500));
        Check(flight != null && flight.All(p => Math.Abs(p.Z - 900) < 1), "a flying route keeps its height above its owner");
        Console.WriteLine($"LEVEL_FILL_FREE_OK checks={checks}");
        return checks;
    }
}
