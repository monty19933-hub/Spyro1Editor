using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>The built look of the houses and castle walls the planner stood on the floor
/// (<see cref="SpyroPlanner.IsBuilt"/>): a pitched roof with gables, a door and windows on a house's front; battlements
/// along a castle wall and a round tower at each end; battlements along the top of a castle's rock walls.</summary>
internal static partial class SpyroStructures
{
    /// <summary>Most battlements a level gets along its castle walls' tops (each four faces).</summary>
    private const int MostMerlons = 70;

    private static List<TerrainPiece> Buildings(PlannedLevel L, LevelStyle style, LevelRandom rnd, FeatureSet? features, List<(double X, double Y, double R)> placed, ref int towers)
    {
        var plan = L.Plan; var pieces = new List<TerrainPiece>();
        int merlons = 0;
        foreach (var z in plan.Zones)
        {
            if (z.Kind == "house" && z.Boxes.Count > 0)
            {
                var b = z.Boxes[0];
                double floor = L.Areas[z.Area].Z;
                pieces.AddRange(House(b.X, b.Y, b.HalfX, b.HalfY, b.Cos, b.Sin, floor, z.Z, rnd));
                placed.Add((b.X, b.Y, Math.Sqrt(b.HalfX * b.HalfX + b.HalfY * b.HalfY) + 40));
            }
            else if (z.Kind == "rampart" && z.Spine.Count >= 2)
            {
                var a = L.Areas[z.Area];
                var (p, q) = (z.Spine[0], z.Spine[^1]);
                double len = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
                (double X, double Y) u = ((q.X - p.X) / len, (q.Y - p.Y) / len), n = (-u.Y, u.X);
                // The battlements on the side away from the room's middle.
                if ((a.X - (p.X + q.X) / 2) * n.X + (a.Y - (p.Y + q.Y) / 2) * n.Y > 0) n = (-n.X, -n.Y);
                for (double t = 50; t + 50 <= len && merlons < MostMerlons; t += 115, merlons++)
                    pieces.AddRange(Merlon(p.X + u.X * t + n.X * (z.HalfWidth - 20), p.Y + u.Y * t + n.Y * (z.HalfWidth - 20), u, z.Z, 30, 64, 40, 48));
                for (double t = 0; t <= len; t += 120) placed.Add((p.X + u.X * t, p.Y + u.Y * t, z.HalfWidth + 30));
                foreach (var e in new[] { p, q })
                {
                    pieces.AddRange(Tower(e.X, e.Y, a.Z - 30, z.Z + Range(rnd, 110, 170), z.HalfWidth + 30, style == LevelStyle.Castle ? 150 : 190));
                    placed.Add((e.X, e.Y, z.HalfWidth + 60));
                    towers++;
                }
            }
        }
        // A castle's own rock walls (brick) get battlements along their tops.
        if (features?.Walls == true && style == LevelStyle.Castle && plan.Kind == EnclosureKind.Cliff)
            pieces.AddRange(RimMerlons(L, ref merlons));
        if (merlons > 0) L.Count("battlement", merlons);
        return pieces;
    }

    /// <summary>A house's roof (two slopes and the gables under them, a little over the walls), a door in the middle
    /// of its front and a window either side. <paramref name="cos"/>/<paramref name="sin"/>: the way the front runs;
    /// the front faces the +across side.</summary>
    private static IEnumerable<TerrainPiece> House(double x, double y, double hl, double hw, double cos, double sin, double floor, double top, LevelRandom rnd)
    {
        (double X, double Y) u = (cos, sin), v = (-sin, cos);
        double rise = Range(rnd, 0.55, 0.85) * hw + 30, over = 22;
        Vector3f Q(double a, double b, double z) => new((float)Math.Round(x + u.X * a + v.X * b), (float)Math.Round(y + u.Y * a + v.Y * b), (float)Math.Round(z));
        TerrainPiece? Face(Vector3f[] c, SurfaceRole role, double light, bool collides = true) =>
            TerrainMesher.Clean(c, role, [light, light, light * 0.85, light * 0.85], null, TerrainPlan.None, true) is { } f ? f with { Collides = collides } : null;
        double L = hl + over, W = hw + over, ridge = top + rise, eave = top - 12;
        var parts = new List<TerrainPiece?>
        {
            // The front slope (+across) and the back slope.
            Face([Q(-L, 0, ridge), Q(L, 0, ridge), Q(L, W, eave), Q(-L, W, eave)], SurfaceRole.Roof, 0.92),
            Face([Q(L, 0, ridge), Q(-L, 0, ridge), Q(-L, -W, eave), Q(L, -W, eave)], SurfaceRole.Roof, 0.62),
        };
        // The gables: from the wall's top up under the ridge at each end.
        foreach (var (a, b0, b1) in new[] { (hl, -hw, hw), (-hl, hw, -hw) })
            parts.Add(TerrainMesher.Clean([Q(a, 0, ridge - 4), Q(a, b0, top), Q(a, b1, top)], SurfaceRole.Brick, [0.8, 0.7, 0.7], null, TerrainPlan.None, true));
        // The door, a little proud of the front wall, and the windows either side of it.
        double front = hw + 2, d = Range(rnd, -0.25, 0.25) * hl, half = Range(rnd, 34, 42), tall = Math.Min(top - floor - 30, Range(rnd, 110, 130));
        parts.Add(Face([Q(d - half, front, floor + tall), Q(d + half, front, floor + tall), Q(d + half, front, floor + 2), Q(d - half, front, floor + 2)], SurfaceRole.Wood, 0.42, false));
        if (hl >= 140 && top - floor >= 170)
            foreach (double w in new[] { -0.65, 0.65 })
            {
                double c = w * hl;
                if (Math.Abs(c - d) < half + 60) continue;
                parts.Add(Face([Q(c - 26, front, floor + 160), Q(c + 26, front, floor + 160), Q(c + 26, front, floor + 104), Q(c - 26, front, floor + 104)], SurfaceRole.Wood, 0.22, false));
            }
        foreach (var p in parts) if (p != null) yield return p;
    }

    /// <summary>One battlement: a block on a wall's top, <paramref name="length"/> along <paramref name="u"/>, its
    /// inner face, ends and top (the outer face is never seen).</summary>
    private static IEnumerable<TerrainPiece> Merlon(double x, double y, (double X, double Y) u, double z, double half, double length, double depth, double height)
    {
        (double X, double Y) n = (-u.Y, u.X);
        double l = length / 2, w = depth / 2;
        Vector3f C(double a, double b, double h) => new((float)Math.Round(x + u.X * a + n.X * b), (float)Math.Round(y + u.Y * a + n.Y * b), (float)Math.Round(h));
        // The footprint anticlockwise from above: (-l,-w), (l,-w), (l,w), (-l,w); each side [next top, this top, this
        // foot, next foot].
        var foot = new (double A, double B)[] { (-l, -w), (l, -w), (l, w), (-l, w) };
        double z0 = z - 6, z1 = z + height;
        for (int k = 0; k < 4; k++)
        {
            var a = foot[k]; var b = foot[(k + 1) % 4];
            double light = k switch { 0 => 0.78, 2 => 0.5, _ => 0.64 };
            if (TerrainMesher.Clean([C(b.A, b.B, z1), C(a.A, a.B, z1), C(a.A, a.B, z0), C(b.A, b.B, z0)], SurfaceRole.Brick, [light, light, light * 0.8, light * 0.8], null, TerrainPlan.None, true) is { } side)
                yield return side;
        }
        if (TerrainMesher.Clean([C(-l, -w, z1), C(l, -w, z1), C(l, w, z1), C(-l, w, z1)], SurfaceRole.Brick, [0.9, 0.9, 0.9, 0.9], null, TerrainPlan.None, false) is { } cap)
            yield return cap;
    }

    /// <summary>Battlements along the top of a castle's rock walls, round every yard, on the wall's top just behind its
    /// edge; none where a path leaves a yard.</summary>
    private static IEnumerable<TerrainPiece> RimMerlons(PlannedLevel L, ref int merlons)
    {
        var plan = L.Plan; var pieces = new List<TerrainPiece>();
        var stood = new List<(double X, double Y)>();
        double back = Math.Max(30, plan.CapWidth * 0.35);
        foreach (var a in L.Areas.OrderBy(a => a.Id))
        {
            if (a.Zone.Roof > 0) continue;
            double rim = a.Reach + back, step = 120 / rim;
            for (double angle = 0; angle < 6.2832 && merlons < MostMerlons; angle += step)
            {
                double dx = SpyroPlanner.Cos(angle), dy = SpyroPlanner.Sin(angle);
                // Out from the room's middle to just behind the wall's edge.
                double at = -1;
                for (double t = a.R * 0.6; t < a.Reach + plan.CapWidth; t += 12)
                    if (plan.Land(a.X + dx * t, a.Y + dy * t) >= back) { at = t; break; }
                if (at < 0) continue;
                double x = a.X + dx * at, y = a.Y + dy * at;
                if (plan.Classify(x, y) != TerrainPlan.Enclosure) continue;
                // Not by a path's mouth (where the wall is broken) nor beside another battlement.
                if (plan.Zones.Any(z => z.Kind is "path" or "charge" or "bridge" && z.Sdf(x, y, plan.Noise) < 200)) continue;
                if (stood.Any(s => (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) < 95 * 95)) continue;
                // Along the wall: across the way the land's edge runs.
                double gx = plan.Land(x + 8, y) - plan.Land(x - 8, y), gy = plan.Land(x, y + 8) - plan.Land(x, y - 8), gl = Math.Sqrt(gx * gx + gy * gy);
                if (gl <= 0) continue;
                (double X, double Y) u = (-gy / gl, gx / gl);
                double z = plan.Height(TerrainPlan.Enclosure, x, y);
                pieces.AddRange(Merlon(x, y, u, z, 0, 58, 40, 46));
                stood.Add((x, y));
                merlons++;
            }
        }
        return pieces;
    }
}
