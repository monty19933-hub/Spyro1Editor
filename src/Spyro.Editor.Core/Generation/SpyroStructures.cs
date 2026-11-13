using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>What is built on the land rather than being land: the round towers with pointed roofs that stand at the
/// mouths of paths and round courtyards in the game's own levels.</summary>
internal static partial class SpyroStructures
{
    /// <summary>A round tower: <paramref name="sides"/> upright faces in bands, a cone roof wider than the wall. Its
    /// foot is sunk into the ground it stands on.</summary>
    public static IEnumerable<TerrainPiece> Tower(double cx, double cy, double foot, double top, double radius, double roof, int sides = 8)
    {
        var ring = new (double X, double Y)[sides];
        for (int k = 0; k < sides; k++)
        {
            double a = 2 * 3.14159265358979 * (k + 0.5) / sides;
            ring[k] = (SpyroPlanner.Cos(a), SpyroPlanner.Sin(a));
        }
        Vector3f P(int k, double r, double z) => new((float)Math.Round(cx + ring[k % sides].X * r), (float)Math.Round(cy + ring[k % sides].Y * r), (float)Math.Round(z));
        int bands = Math.Max(1, (int)Math.Round((top - foot) / 170));
        for (int b = 0; b < bands; b++)
        {
            double z0 = foot + (top - foot) * b / bands, z1 = foot + (top - foot) * (b + 1) / bands;
            double l0 = 0.25 + 0.5 * b / bands, l1 = 0.25 + 0.5 * (b + 1) / bands;
            for (int k = 0; k < sides; k++)
            {
                // Seen from outside the corners turn anticlockwise: top right is the earlier corner of the ring.
                double side = 0.82 + 0.18 * ring[k].X;
                var piece = TerrainMesher.Clean([P(k + 1, radius, z1), P(k, radius, z1), P(k, radius, z0), P(k + 1, radius, z0)], SurfaceRole.Brick,
                    [l1 * side, l1 * side, l0 * side, l0 * side], null, TerrainPlan.None, true);
                if (piece != null) yield return piece;
            }
        }
        var tip = new Vector3f((float)Math.Round(cx), (float)Math.Round(cy), (float)Math.Round(top + roof));
        for (int k = 0; k < sides; k++)
        {
            double side = 0.75 + 0.25 * ring[k].X;
            var piece = TerrainMesher.Clean([tip, P(k, radius * 1.22, top - 8), P(k + 1, radius * 1.22, top - 8)], SurfaceRole.Roof, [0.95 * side, 0.55 * side, 0.55 * side], null, TerrainPlan.None, true);
            if (piece != null) yield return piece with { Collides = false };
        }
    }

    /// <summary>The report's count of things lifted from the look's level.</summary>
    public const string OwnThings = "own";

    /// <summary>Most of the level's faces that may be things rather than land, and most textures those things may add.</summary>
    public const int PropFaces = 420, PropTextures = 8;

    /// <summary>What stands on the land: the look's own towers, trees, rocks and arches where its level was read
    /// (<paramref name="props"/>), else round towers built here. Tall ones stand either side of where paths leave rooms
    /// (round a castle's yards, at both ends); small ones on the banks and rims round the rooms and at the rooms' edges.</summary>
    public static List<TerrainPiece> Build(PlannedLevel L, LevelStyle style, LevelRandom rnd, IReadOnlyList<LevelProp>? props = null, FeatureSet? features = null)
    {
        var plan = L.Plan; var pieces = new List<TerrainPiece>();
        bool castle = style == LevelStyle.Castle, free = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky;
        var tall = props?.Where(p => p.Tall && p.Radius <= 320).ToList() ?? [];
        var small = props?.Where(p => !p.Tall && p.Radius <= 190).ToList() ?? [];
        var textures = new HashSet<int>();
        int faces = 0, towers = 0, things = 0;
        var placed = new List<(double X, double Y, double R)>();
        // Nothing stands at a doorway, behind it, or on the floor in front of it where Spyro comes back out.
        foreach (var door in L.Doors)
            for (double t = -door.Behind; t <= door.Front; t += 130)
                placed.Add((door.X - door.Dx * t, door.Y - door.Dy * t, Math.Abs(t) < 200 ? 200 : 130));
        // Roofs, doors and windows on the houses, battlements and towers on the castle walls; nothing else stands there.
        pieces.AddRange(Buildings(L, style, rnd, features, placed, ref towers));
        bool Room(double x, double y, double r) => placed.All(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) >= (p.R + r + 60) * (p.R + r + 60));
        // A prop is used only while the level has room for its faces and its textures.
        LevelProp? Pick(List<LevelProp> from)
        {
            var fits = from.Where(p => faces + p.Faces.Count <= PropFaces && textures.Union(p.Textures).Count() <= PropTextures).ToList();
            return fits.Count == 0 ? null : rnd.Pick(fits);
        }
        void Use(LevelProp prop, double x, double y, double ground, double sink)
        {
            pieces.AddRange(LevelProps.Place(prop, x, y, ground, Range(rnd, 0, 6.2832), sink));
            faces += prop.Faces.Count; textures.UnionWith(prop.Textures); placed.Add((x, y, prop.Radius)); things++;
        }
        void Stand(double x, double y, double height)
        {
            if (plan.Land(x, y) < 70) return;
            double ground = plan.Height(TerrainPlan.Enclosure, x, y), floor = plan.Base(x, y);
            if (Pick(tall) is { } prop && Room(x, y, prop.Radius)) { Use(prop, x, y, ground, 50); return; }
            if (!Room(x, y, 110) || tall.Count > 0 && rnd.Chance(0.5)) return;
            placed.Add((x, y, 110)); towers++;
            pieces.AddRange(Tower(x, y, ground - 60, Math.Max(ground + 150, floor + height), castle ? 112 : 100, castle ? 150 : 200));
        }
        // Landmarks first: on a hilltop, a mound or an island that asked for one, the look's own tall thing where it has
        // one that fits there, else a round tower.
        foreach (var mark in L.Landmarks)
        {
            double ground = plan.Height(mark.Zone, mark.X, mark.Y);
            var fitting = tall.Where(p => p.Radius <= mark.Room - 95).ToList();
            if (Pick(fitting) is { } prop && Room(mark.X, mark.Y, prop.Radius)) { Use(prop, mark.X, mark.Y, ground, 20); continue; }
            if (mark.Room < 190 || style == LevelStyle.Caverns || !Room(mark.X, mark.Y, 100)) continue;
            placed.Add((mark.X, mark.Y, 100)); towers++;
            pieces.AddRange(Tower(mark.X, mark.Y, ground - 20, ground + Range(rnd, 300, 430), Math.Min(104, mark.Room - 96), castle ? 150 : 200));
        }
        // Every level of the game has a centrepiece seen from afar that the way leads towards (Stone Hill's tower, Dark
        // Hollow's castle, Artisans' keep). Where the plan asked for none, the deepest big room gets one at its back,
        // against its edge and away from its ways in: the look's own tallest thing that fits, else a keep of towers.
        if (L.Landmarks.Count == 0 && L.Cuts.Count == 0 && style != LevelStyle.Caverns) Centrepiece();
        void Centrepiece()
        {
            var rooms = L.Areas.Where(a => a.Kind is "clearing" or "start" && a.Zone.Kind == "room" && a.Zone.Roof <= 0 && a.R >= 440)
                .OrderByDescending(a => a.Depth).ThenByDescending(a => a.R).Take(3).ToList();
            foreach (var a in rooms)
                for (int k = 0; k < 16; k++)
                {
                    var (dx, dy) = (SpyroPlanner.Cos(6.2832 * k / 16), SpyroPlanner.Sin(6.2832 * k / 16));
                    // Away from every way in and out of the room.
                    if (a.Taken.Any(t => t.X * dx + t.Y * dy > 0.35)) continue;
                    double at = a.R * 0.72, x = a.X + dx * at, y = a.Y + dy * at;
                    int zone = plan.ZoneAt(x, y);
                    if (zone != a.Zone.Id) continue;
                    double floor = plan.Height(zone, x, y);
                    var fitting = tall.Where(p => p.Radius <= a.R * 0.3 && p.Radius >= 90 && p.Height >= 380).OrderByDescending(p => p.Height).ToList();
                    if (fitting.Count > 0 && Pick(fitting.Take(2).ToList()) is { } prop && Room(x, y, prop.Radius))
                    {
                        if (plan.Zones.Any(z => z.Kind != "room" && z.Sdf(x, y, plan.Noise) < prop.Radius + 80) || SpyroPlanner.OnWay(L, a, x, y, prop.Radius + 140)) continue;
                        Use(prop, x, y, floor, 20); L.Count("centrepiece"); L.Spots.Add(("landmark", x, y, zone, x, y, a.Id)); return;
                    }
                    // A keep: a tall tower with three lower ones round it.
                    double keep = 210;
                    if (!Room(x, y, keep) || plan.Zones.Any(z => z.Kind != "room" && z.Sdf(x, y, plan.Noise) < keep + 80) || SpyroPlanner.OnWay(L, a, x, y, keep + 140)) continue;
                    pieces.AddRange(Tower(x, y, floor - 30, floor + Range(rnd, 420, 520), 112, castle ? 160 : 210));
                    double turn = Range(rnd, 0, 6.2832);
                    for (int n = 0; n < 3; n++)
                    {
                        double ang = turn + 6.2832 * n / 3;
                        pieces.AddRange(Tower(x + SpyroPlanner.Cos(ang) * 150, y + SpyroPlanner.Sin(ang) * 150, floor - 30, floor + Range(rnd, 250, 320), 64, castle ? 110 : 150, 6));
                    }
                    placed.Add((x, y, keep)); towers += 4; L.Count("centrepiece"); L.Spots.Add(("landmark", x, y, zone, x, y, a.Id)); return;
                }
        }
        if (!free && style is not (LevelStyle.Canyon or LevelStyle.Caverns))
            foreach (var zone in plan.Zones)
            {
                if (zone.Kind != "path" || zone.Roof > 0) continue;
                int count = 0;
                foreach (bool end in new[] { false, true })
                {
                    if (!castle && count > 0) break;
                    if (!castle && !rnd.Chance(0.7)) continue;
                    var s = zone.Spine;
                    for (int k = 1; k < s.Count; k++)
                    {
                        int i = end ? s.Count - 1 - k : k, before = end ? i + 1 : i - 1;
                        if (plan.Zones.Any(z => z.Kind == "room" && z.Sdf(s[i].X, s[i].Y, plan.Noise) < 0)) continue;
                        double tx = s[i].X - s[before].X, ty = s[i].Y - s[before].Y, tl = Math.Sqrt(tx * tx + ty * ty);
                        if (tl <= 0) break;
                        double off = zone.HalfWidth + (castle ? 150 : 170), height = plan.Rise + Range(rnd, 110, 200);
                        Stand(s[i].X - ty / tl * off, s[i].Y + tx / tl * off, height);
                        Stand(s[i].X + ty / tl * off, s[i].Y - tx / tl * off, height);
                        count++;
                        break;
                    }
                }
            }
        // Small things: round each room, on the land behind its edge (banks and rims; none where the land ends in
        // water or air) and just inside it.
        if (small.Count > 0)
            foreach (var a in L.Areas)
            {
                int want = rnd.Next(2, 5);
                for (int k = 0; k < want; k++)
                {
                    if (Pick(small) is not { } prop) break;
                    double angle = Range(rnd, 0, 6.2832), dx = SpyroPlanner.Cos(angle), dy = SpyroPlanner.Sin(angle);
                    bool inside = free || rnd.Chance(0.35);
                    double target = inside ? -(prop.Radius + 70) : plan.Kind is EnclosureKind.Cliff ? plan.CapWidth * 0.5 : plan.BankWidth + 50;
                    // Out from the room's middle to where the land is that far inside (or outside) its edge.
                    double at = -1;
                    for (double t = a.R * 0.3; t < a.R * 2.2; t += 16)
                    {
                        double land = plan.Land(a.X + dx * t, a.Y + dy * t);
                        if (inside ? land > target : land >= target) { at = inside ? t - 16 : t; break; }
                    }
                    if (at < 0) continue;
                    double x = a.X + dx * at, y = a.Y + dy * at;
                    if (!Room(x, y, prop.Radius)) continue;
                    int zone = plan.ZoneAt(x, y);
                    if (inside)
                    {
                        // On a room's own floor, clear of paths, ledges and pools, and of where Spyro arrives.
                        if (zone < 0 || plan.Zones[zone].Kind != "room" || plan.Zones[zone].Roof > 0) continue;
                        if (plan.Zones.Any(z => z.Kind != "room" && z.Sdf(x, y, plan.Noise) < prop.Radius + 150)) continue;
                        if ((x - L.Start.X) * (x - L.Start.X) + (y - L.Start.Y) * (y - L.Start.Y) < 420 * 420) continue;
                        // Nor on the way from the room's middle to a room it runs on into.
                        if (SpyroPlanner.OnWay(L, a, x, y, prop.Radius + 110)) continue;
                        Use(prop, x, y, plan.Height(zone, x, y), 14);
                    }
                    else
                    {
                        if (zone >= 0 || plan.Land(x, y) <= 0) continue;
                        Use(prop, x, y, plan.Height(TerrainPlan.Enclosure, x, y), 30);
                    }
                }
            }
        if (towers > 0) L.Count("tower", towers);
        if (things > 0) L.Count(OwnThings, things);
        return pieces;
    }

    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
}
