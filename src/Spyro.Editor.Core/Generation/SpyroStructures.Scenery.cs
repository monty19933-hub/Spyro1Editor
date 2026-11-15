using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>What grows and lies on the land: little cone trees (as Stone Hill's meadow has them: nine or ten faces
/// each), boulders at the feet of the banks, rock spires where nothing grows. Made here in the look's own ground and
/// rock, so every look has them (most of the game's levels have no free-standing trees to lift). They stand the way the
/// game sets its scenery: trees in twos and threes along the inside of a room's edge and up on the hills round the
/// level, never on a way through; boulders in the corners at the walls' feet.</summary>
internal static partial class SpyroStructures
{
    /// <summary>Looks whose ground nothing green grows on (sand, rock, metal): they get spires and boulders, no trees.</summary>
    private static readonly HashSet<string> Barren = new(StringComparer.OrdinalIgnoreCase) { "peacekeepers", "drycanyon", "clifftown", "twilightharbor" };

    /// <summary>Most faces of made scenery in a level (a small level has fewer: by its ground).</summary>
    public const int SceneryFaces = 640;

    /// <summary>A cone tree: a short trunk, a flared skirt of boughs and a pointed top, <paramref name="sides"/> round.</summary>
    public static IEnumerable<TerrainPiece> ConeTree(double cx, double cy, double foot, double height, double radius, double turn, int sides = 5)
    {
        var ring = Ring(sides, turn);
        Vector3f P(int k, double r, double z) => new((float)Math.Round(cx + ring[k % sides].X * r), (float)Math.Round(cy + ring[k % sides].Y * r), (float)Math.Round(z));
        double boughs = foot + height * 0.24, waist = foot + height * 0.55, mid = radius * 0.5;
        var tip = new Vector3f((float)Math.Round(cx), (float)Math.Round(cy), (float)Math.Round(foot + height));
        // The trunk: three faces, wider at the foot, going up into the boughs.
        var stem = Ring(3, turn);
        Vector3f T(int k, double r, double z) => new((float)Math.Round(cx + stem[k % 3].X * r), (float)Math.Round(cy + stem[k % 3].Y * r), (float)Math.Round(z));
        for (int k = 0; k < 3; k++)
        {
            double side = 0.7 + 0.3 * stem[k].X;
            var bark = TerrainMesher.Clean([T(k + 1, radius * 0.2, boughs + 14), T(k, radius * 0.2, boughs + 14), T(k, radius * 0.3, foot - 8), T(k + 1, radius * 0.3, foot - 8)], SurfaceRole.Wood,
                [0.5 * side, 0.5 * side, 0.62 * side, 0.62 * side], null, TerrainPlan.None, true);
            if (bark != null) yield return bark;
        }
        for (int k = 0; k < sides; k++)
        {
            // Lit from one side, darker underneath (under its own boughs).
            double side = 0.78 + 0.22 * ring[k].X;
            var skirt = TerrainMesher.Clean([P(k + 1, mid, waist), P(k, mid, waist), P(k, radius, boughs), P(k + 1, radius, boughs)], SurfaceRole.GroundAlt,
                [0.62 * side, 0.62 * side, 0.3 * side, 0.3 * side], null, TerrainPlan.None, true);
            if (skirt != null) yield return skirt with { Collides = false };
            var top = TerrainMesher.Clean([tip, P(k, mid * 1.25, waist - 10), P(k + 1, mid * 1.25, waist - 10)], SurfaceRole.GroundAlt, [0.95 * side, 0.55 * side, 0.55 * side], null, TerrainPlan.None, true);
            if (top != null) yield return top with { Collides = false };
        }
    }

    /// <summary>A boulder (or, tall and narrow, a rock spire): an uneven ring of faces leaning in to a small top.</summary>
    public static IEnumerable<TerrainPiece> Boulder(double cx, double cy, double foot, double height, double radius, double turn, LevelRandom rnd, SurfaceRole role, int sides = 5)
    {
        var ring = Ring(sides, turn);
        var low = new double[sides]; var high = new double[sides];
        for (int k = 0; k < sides; k++) { low[k] = radius * Range(rnd, 0.8, 1.15); high[k] = radius * Range(rnd, 0.3, 0.55); }
        double lean = Range(rnd, -0.2, 0.2) * radius;
        Vector3f Low(int k) => new((float)Math.Round(cx + ring[k % sides].X * low[k % sides]), (float)Math.Round(cy + ring[k % sides].Y * low[k % sides]), (float)Math.Round(foot - 10));
        Vector3f High(int k) => new((float)Math.Round(cx + lean + ring[k % sides].X * high[k % sides]), (float)Math.Round(cy + ring[k % sides].Y * high[k % sides]), (float)Math.Round(foot + height * (0.86 + 0.14 * ring[k % sides].Y)));
        var top = new Vector3f((float)Math.Round(cx + lean), (float)Math.Round(cy), (float)Math.Round(foot + height * 1.06));
        for (int k = 0; k < sides; k++)
        {
            double side = 0.72 + 0.28 * ring[k].X;
            var face = TerrainMesher.Clean([High(k + 1), High(k), Low(k), Low(k + 1)], role, [0.7 * side, 0.7 * side, 0.3 * side, 0.3 * side], null, TerrainPlan.None, true);
            if (face != null) yield return face;
            var cap = TerrainMesher.Clean([top, High(k), High(k + 1)], role, [0.9 * side, 0.75 * side, 0.75 * side], null, TerrainPlan.None, true);
            if (cap != null) yield return cap;
        }
    }

    private static (double X, double Y)[] Ring(int sides, double turn)
    {
        var ring = new (double X, double Y)[sides];
        for (int k = 0; k < sides; k++)
        {
            double a = turn + 2 * 3.14159265358979 * (k + 0.5) / sides;
            ring[k] = (SpyroPlanner.Cos(a), SpyroPlanner.Sin(a));
        }
        return ring;
    }

    /// <summary>Trees and rocks for the whole level, where there is room for them (<paramref name="placed"/>: what
    /// already stands, with its clearance). The game stands its trees in twos and threes just inside a field's edge, a
    /// few out in a big field, and more up on the hills round it; its boulders lie at the walls' feet.</summary>
    private static void Scenery(PlannedLevel L, LevelStyle style, string? look, LevelRandom rnd, List<TerrainPiece> pieces, List<(double X, double Y, double R)> placed)
    {
        var plan = L.Plan;
        if (style == LevelStyle.Caverns) return;
        bool free = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky, castle = style == LevelStyle.Castle;
        bool trees = !castle && (look == null || !Barren.Contains(look));
        var rock = plan.Kind == EnclosureKind.Cliff && !castle ? SurfaceRole.Cliff : SurfaceRole.Rock;
        int faces = 0, treeCount = 0, rockCount = 0;
        bool debug = Environment.GetEnvironmentVariable("SPYRO_RANDOM_LEVEL_DEBUG") == "scenery";
        var areaOf = new Dictionary<int, PlannedArea>();
        foreach (var a in L.Areas) if (a.Zone != null) areaOf.TryAdd(a.Zone.Id, a);
        bool Room(double x, double y, double r) => placed.All(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) >= (p.R + r + 40) * (p.R + r + 40));
        // On a room's own open floor, clear of what is laid on it (paths, ledges, pools, houses), of where Spyro arrives
        // and of the ways through the room. The room it is in, or null.
        PlannedArea? OnFloor(double x, double y, double clear)
        {
            int zone = plan.ZoneAt(x, y);
            if (zone < 0 || !areaOf.TryGetValue(zone, out var a) || a.Zone.Kind != "room" || a.Zone.Roof > 0 || a.Want == "cut") return null;
            if (plan.Zones.Any(z => z.Kind != "room" && z.Sdf(x, y, plan.Noise) < clear + 70)) return null;
            if (plan.Decals.Any(d => d.Sdf(x, y, plan.Noise) < clear + 30)) return null;
            if ((x - L.Start.X) * (x - L.Start.X) + (y - L.Start.Y) * (y - L.Start.Y) < 380 * 380) return null;
            return SpyroPlanner.OnWay(L, a, x, y, clear + 90) ? null : a;
        }
        // Every place of the land on a grid, with how far inside its edge (negative) or out on the hills round it.
        double x0 = L.Areas.Min(a => a.X - a.R) - 500, x1 = L.Areas.Max(a => a.X + a.R) + 500, y0 = L.Areas.Min(a => a.Y - a.R) - 500, y1 = L.Areas.Max(a => a.Y + a.R) + 500;
        double hillFrom = plan.Kind == EnclosureKind.Cliff ? plan.CapWidth * 0.35 : plan.BankWidth * 0.45, hillTo = plan.Kind == EnclosureKind.Cliff ? plan.CapWidth * 0.95 : plan.BankWidth + plan.CapWidth * 0.6;
        var edge = new List<(double X, double Y)>(); var open = new List<(double X, double Y)>(); var hill = new List<(double X, double Y)>(); var foot = new List<(double X, double Y)>();
        const double step = 64;
        for (double gy = y0; gy <= y1; gy += step)
            for (double gx = x0; gx <= x1; gx += step)
            {
                double x = gx + Range(rnd, -22, 22), y = gy + Range(rnd, -22, 22), land = plan.Land(x, y);
                if (land > 0) { if (!free && land >= hillFrom && land <= hillTo) hill.Add((x, y)); continue; }
                if (land > -64) continue;
                if (land > -140) foot.Add((x, y));
                if (land > -110) continue;
                (land > -330 ? edge : open).Add((x, y));
            }
        rnd.Shuffle(edge); rnd.Shuffle(open); rnd.Shuffle(hill); rnd.Shuffle(foot);
        // A small level has less of it than a big one (as it has less of everything).
        int most = (int)Math.Clamp((edge.Count + open.Count) * 0.22, 100, SceneryFaces);
        var groups = new List<(double X, double Y)>();
        bool Apart(double x, double y, double d) => groups.All(g => (g.X - x) * (g.X - x) + (g.Y - y) * (g.Y - y) >= d * d);
        bool Tree(double x, double y, bool onHill)
        {
            double radius = Range(rnd, 56, 92), height = radius * Range(rnd, 2.1, 2.9), ground;
            if (!Room(x, y, radius)) return false;
            if (onHill)
            {
                double land = plan.Land(x, y);
                if (plan.ZoneAt(x, y) >= 0 || land < hillFrom || land > hillTo) return false;
                ground = plan.Height(TerrainPlan.Enclosure, x, y);
            }
            else
            {
                if (OnFloor(x, y, radius) is not { } a || plan.Land(x, y) > -(radius + 36)) return false;
                ground = plan.Height(a.Zone.Id, x, y);
            }
            var made = ConeTree(x, y, ground, height, radius, Range(rnd, 0, 6.2832)).ToList();
            pieces.AddRange(made); faces += made.Count; treeCount++;
            placed.Add((x, y, radius));
            return true;
        }
        // A group: one tree and up to three more a few steps from it.
        void Grove(List<(double X, double Y)> from, int want, double apart, bool onHill, int budget)
        {
            int made = 0;
            foreach (var (x, y) in from)
            {
                if (made >= want || faces >= budget) break;
                if (!Apart(x, y, apart) || !Tree(x, y, onHill)) continue;
                groups.Add((x, y)); made++;
                int more = rnd.Pick<int>([(0, 2.5), (1, 4.0), (2, 3.0), (3, 1.0)]);
                double px = x, py = y;
                for (int n = 0, tries = 0; n < more && tries < 8 && faces < budget; tries++)
                {
                    double at = Range(rnd, 0, 6.2832), far = Range(rnd, 120, 190), qx = px + SpyroPlanner.Cos(at) * far, qy = py + SpyroPlanner.Sin(at) * far;
                    if (!Tree(qx, qy, onHill)) continue;
                    n++; if (rnd.Chance(0.5)) { px = qx; py = qy; }
                }
            }
        }
        if (trees)
        {
            // Just inside the edges: a group about every 420 along them. Out in the open: one for a big field's worth
            // of ground. The rest of what the level can draw goes up on the hills round it.
            int within = (int)Math.Round(edge.Count * step * step / (420.0 * 220) * Range(rnd, 0.8, 1.15));
            Grove(edge, within, 330, false, most * 5 / 10);
            Grove(open, (int)Math.Round(open.Count * step * step / (1000.0 * 1000) * Range(rnd, 0.6, 1.3)), 520, false, most * 6 / 10);
            Grove(hill, (int)Math.Round(hill.Count * step * step / (460.0 * 260) * Range(rnd, 0.8, 1.15)), 300, true, most * 82 / 100);
        }
        // Boulders and spires at the foot of the land's edge, one or two together; in a barren look more of them, some
        // tall, and some out in the open.
        int rocks = (int)Math.Round(foot.Count * step * step / ((castle ? 1500.0 : trees ? 900 : 420) * 76) * Range(rnd, 0.7, 1.3)), rocksMade = 0;
        var stones = new List<(double X, double Y)>();
        foreach (var (x, y) in trees || castle ? foot : [.. foot, .. open.Take(open.Count / 6)])
        {
            if (rocksMade >= rocks || faces >= most) break;
            if (stones.Any(g => (g.X - x) * (g.X - x) + (g.Y - y) * (g.Y - y) < 300 * 300)) continue;
            int size = rnd.Chance(0.4) ? 2 : 1; bool any = false;
            for (int n = 0; n < size && faces < most; n++)
            {
                bool spire = !trees && !castle && rnd.Chance(0.45);
                double radius = spire ? Range(rnd, 42, 60) : Range(rnd, 38, 72) * (n == 1 ? 0.7 : 1), height = spire ? Range(rnd, 190, 300) : radius * Range(rnd, 0.8, 1.5);
                double px = x + (n == 0 ? 0 : Range(rnd, -1, 1) * 120), py = y + (n == 0 ? 0 : Range(rnd, -1, 1) * 120);
                if (!Room(px, py, radius) || OnFloor(px, py, radius) is not { } a || plan.Land(px, py) > -(radius + 14)) continue;
                var made = Boulder(px, py, plan.Height(a.Zone.Id, px, py), height, radius, Range(rnd, 0, 6.2832), rnd, rock).ToList();
                pieces.AddRange(made); faces += made.Count; rockCount++; any = true;
                placed.Add((px, py, radius));
            }
            if (any) { stones.Add((x, y)); rocksMade++; }
        }
        if (debug) Console.Error.WriteLine($"scenery: {edge.Count} edge, {open.Count} open, {hill.Count} hill, {foot.Count} foot places; {treeCount} trees, {rockCount} rocks, {faces} faces");
        if (treeCount > 0) L.Count("tree", treeCount);
        if (rockCount > 0) L.Count("rock", rockCount);
    }
}
