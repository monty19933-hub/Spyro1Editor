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

    /// <summary>Most faces of made scenery in a level, and how many trees and rocks a room of a size gets.</summary>
    public const int SceneryFaces = 300;

    /// <summary>A cone tree: a flared skirt and a pointed top, <paramref name="sides"/> round.</summary>
    public static IEnumerable<TerrainPiece> ConeTree(double cx, double cy, double foot, double height, double radius, double turn, int sides = 5)
    {
        var ring = Ring(sides, turn);
        Vector3f P(int k, double r, double z) => new((float)Math.Round(cx + ring[k % sides].X * r), (float)Math.Round(cy + ring[k % sides].Y * r), (float)Math.Round(z));
        double waist = foot + height * 0.42, mid = radius * 0.5;
        var tip = new Vector3f((float)Math.Round(cx), (float)Math.Round(cy), (float)Math.Round(foot + height));
        for (int k = 0; k < sides; k++)
        {
            // Lit from one side, darker at the foot (under its own boughs).
            double side = 0.78 + 0.22 * ring[k].X;
            var skirt = TerrainMesher.Clean([P(k + 1, mid, waist), P(k, mid, waist), P(k, radius, foot - 6), P(k + 1, radius, foot - 6)], SurfaceRole.GroundAlt,
                [0.62 * side, 0.62 * side, 0.3 * side, 0.3 * side], null, TerrainPlan.None, true);
            if (skirt != null) yield return skirt;
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
    /// already stands, with its clearance).</summary>
    private static void Scenery(PlannedLevel L, LevelStyle style, string? look, LevelRandom rnd, List<TerrainPiece> pieces, List<(double X, double Y, double R)> placed)
    {
        var plan = L.Plan;
        if (style == LevelStyle.Caverns) return;
        bool free = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky, castle = style == LevelStyle.Castle;
        bool trees = !castle && (look == null || !Barren.Contains(look));
        var rock = plan.Kind == EnclosureKind.Cliff && !castle ? SurfaceRole.Cliff : SurfaceRole.Rock;
        int faces = 0, treeCount = 0, rockCount = 0;
        bool Room(double x, double y, double r) => placed.All(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) >= (p.R + r + 40) * (p.R + r + 40));
        // On a room's own floor, clear of what is laid on it (paths, ledges, pools, houses), of where Spyro arrives and
        // of the ways through the room.
        bool OnFloor(PlannedArea a, double x, double y, double clear)
        {
            int zone = plan.ZoneAt(x, y);
            if (zone < 0 || zone != a.Zone.Id || plan.Zones[zone].Roof > 0) return false;
            if (plan.Zones.Any(z => z.Kind != "room" && z.Sdf(x, y, plan.Noise) < clear + 90)) return false;
            if (plan.Decals.Any(d => d.Sdf(x, y, plan.Noise) < clear + 30)) return false;
            if ((x - L.Start.X) * (x - L.Start.X) + (y - L.Start.Y) * (y - L.Start.Y) < 380 * 380) return false;
            return !SpyroPlanner.OnWay(L, a, x, y, clear + 100);
        }
        // Out from a room's middle along a direction to where the land is a distance inside its edge (negative) or
        // beyond it (positive); null when the room has no such place that way.
        (double X, double Y)? At(PlannedArea a, double dx, double dy, double target)
        {
            for (double t = a.R * 0.25; t < a.R * 2.3; t += 14)
            {
                double land = plan.Land(a.X + dx * t, a.Y + dy * t);
                if (target < 0 ? land > target : land >= target) return target < 0 ? (a.X + dx * (t - 14), a.Y + dy * (t - 14)) : (a.X + dx * t, a.Y + dy * t);
            }
            return null;
        }
        foreach (var a in L.Areas)
        {
            if (a.Zone == null || a.Zone.Kind != "room" || a.Zone.Roof > 0 || a.Want == "cut") continue;
            // Trees: a group every so often round the room, more round a big one; two in three just inside its edge, the
            // rest up on the land round it (none there on islands: the land ends).
            int groups = trees ? (int)Math.Round(a.R / 210 * Range(rnd, 0.7, 1.3)) : 0;
            for (int g = 0; g < groups && faces < SceneryFaces; g++)
            {
                double angle = Range(rnd, 0, 6.2832);
                bool inside = free || rnd.Chance(0.66);
                int size = rnd.Pick<int>([(1, 3.0), (2, 4.0), (3, 2.5), (4, 1.0)]);
                for (int n = 0; n < size && faces < SceneryFaces; n++)
                {
                    double at = angle + n * Range(rnd, 0.1, 0.2) * 420 / Math.Max(260, a.R), dx = SpyroPlanner.Cos(at), dy = SpyroPlanner.Sin(at);
                    double radius = Range(rnd, 50, 78), height = radius * Range(rnd, 1.9, 2.6);
                    double target = inside ? -Range(rnd, radius + 40, radius + 150) : Range(rnd, 0.35, 0.95) * (plan.Kind == EnclosureKind.Cliff ? plan.CapWidth : plan.BankWidth + plan.CapWidth * 0.5);
                    if (At(a, dx, dy, target) is not { } p || !Room(p.X, p.Y, radius)) continue;
                    double ground;
                    if (inside)
                    {
                        if (!OnFloor(a, p.X, p.Y, radius)) continue;
                        ground = plan.Height(a.Zone.Id, p.X, p.Y);
                    }
                    else
                    {
                        if (plan.ZoneAt(p.X, p.Y) >= 0 || plan.Land(p.X, p.Y) <= 30) continue;
                        ground = plan.Height(TerrainPlan.Enclosure, p.X, p.Y);
                    }
                    var made = ConeTree(p.X, p.Y, ground, height, radius, Range(rnd, 0, 6.2832)).ToList();
                    pieces.AddRange(made); faces += made.Count; treeCount++;
                    placed.Add((p.X, p.Y, radius));
                }
            }
            // Boulders and spires at the foot of the room's edge, one or two together; in a barren look more of them,
            // some tall.
            int rocks = (int)Math.Round(a.R / (trees ? 420 : 240) * Range(rnd, 0.6, 1.4));
            for (int g = 0; g < rocks && faces < SceneryFaces; g++)
            {
                double angle = Range(rnd, 0, 6.2832);
                int size = rnd.Chance(0.4) ? 2 : 1;
                for (int n = 0; n < size && faces < SceneryFaces; n++)
                {
                    double at = angle + n * 0.22 * 420 / Math.Max(260, a.R), dx = SpyroPlanner.Cos(at), dy = SpyroPlanner.Sin(at);
                    bool spire = !trees && rnd.Chance(0.45);
                    double radius = spire ? Range(rnd, 42, 60) : Range(rnd, 38, 72) * (n == 1 ? 0.7 : 1), height = spire ? Range(rnd, 190, 300) : radius * Range(rnd, 0.8, 1.5);
                    if (At(a, dx, dy, -(radius + Range(rnd, 16, 60))) is not { } p || !Room(p.X, p.Y, radius) || !OnFloor(a, p.X, p.Y, radius)) continue;
                    var made = Boulder(p.X, p.Y, plan.Height(a.Zone.Id, p.X, p.Y), height, radius, Range(rnd, 0, 6.2832), rnd, rock).ToList();
                    pieces.AddRange(made); faces += made.Count; rockCount++;
                    placed.Add((p.X, p.Y, radius));
                }
            }
        }
        if (treeCount > 0) L.Count("tree", treeCount);
        if (rockCount > 0) L.Count("rock", rockCount);
    }
}
