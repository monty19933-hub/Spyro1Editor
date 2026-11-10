using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>A part of the level Spyro must reach: a room, with the zones that make it up.</summary>
internal sealed class PlannedArea
{
    public int Id;
    public double X, Y, R, Z;
    public string Kind = "clearing";
    public int Depth;
    public int Priority = 1;
    public TerrainZone Zone = null!;
    /// <summary>Directions (unit vectors) already taken: joins to other rooms, ledges, ponds.</summary>
    public readonly List<(double X, double Y)> Taken = [];
}

/// <summary>How two rooms are joined: a sloping path between them, or (rooms that touch) a step Spyro jumps or an
/// overlook he can only drop or glide from.</summary>
internal sealed record PlannedJoin(int A, int B, string Kind);

/// <summary>A level laid out the way the game's own levels are: areas strung along a route that winds round on itself,
/// each a little higher or lower than the last, joined by sloping paths, with ledges to jump up, high places to glide
/// from and ways back to the start.</summary>
internal sealed class PlannedLevel
{
    public required TerrainPlan Plan;
    public string Shape = "";
    /// <summary>The name of the level the look is taken from, for the report.</summary>
    public string Look = "";
    public readonly List<PlannedArea> Areas = [];
    public readonly List<PlannedJoin> Joins = [];
    public readonly List<LevelLink> Links = [];
    public readonly List<(double X, double Y, int Zone)> Gems = [];
    /// <summary>Glides the plan relies on: from a zone's floor to another's.</summary>
    public readonly List<(int From, int To)> Glides = [];
    public readonly List<string> Notes = [];
    public readonly Dictionary<string, int> Counts = [];
    /// <summary>A homeworld's doorways: where each stands, the way Spyro walks into it (unit), and its room.</summary>
    public readonly List<(double X, double Y, double Dx, double Dy, int Area)> Doors = [];
    public (double X, double Y) Start, Facing;
    public int StartZone;
    public double Spacing = 128;
    public void Count(string what, int n = 1) => Counts[what] = (Counts.TryGetValue(what, out int c) ? c : 0) + n;
}

internal static class SpyroPlanner
{
    /// <summary>The ridge of land kept between rooms that are not joined, and between a room and a path that passes it.</summary>
    private const double Ridge = 560, PathClear = 470;
    /// <summary>This level's room size (1 = a valley's), the gaps its joins cross, and the space kept between rooms that
    /// are not joined. Set per level in <see cref="Build"/> (one level is built at a time per thread).</summary>
    [ThreadStatic] private static double _scale, _gapFrom, _gapTo, _ridge;
    /// <summary>The smallest room of a homeworld (0: any size): a doorway's room holds the doorway and the floor in
    /// front of it that Spyro comes back out onto.</summary>
    [ThreadStatic] private static double _minRoom;
    /// <summary>How far inside its room's edge a doorway stands, and the floor kept open in front of it: coming back
    /// from a level Spyro lands some 650 to 700 units in front of the doorway.</summary>
    internal const double DoorInset = 170, DoorWalk = 860;

    /// <summary>What a difficulty changes: how wide paths are, how much of the walkable slope a ramp uses, how big
    /// stepping stones are and how far apart, how often islands get a bridge and how wide it is, how many ledges a room
    /// has, how often a room has a lookout, how much of a glide is left to spare, how often rooms have pools, whether
    /// liquid costs a life at once, and how big rooms are.</summary>
    private sealed record Tuning(double PathFrom, double PathTo, double Climb, double StoneRadius, double StoneGap, double BridgeChance, double BridgeHalf,
        int LedgesFrom, int LedgesTo, double LookoutChance, double GlideSpare, double PoolChance, bool Deadly, double RoomScale);
    private static Tuning For(LevelDifficulty difficulty) => difficulty switch
    {
        LevelDifficulty.Easy => new(190, 230, 0.16, 165, 70, 0.9, 118, 0, 2, 0.4, 170, 0.2, false, 1.05),
        LevelDifficulty.Hard => new(128, 160, 0.24, 138, 100, 0.3, 96, 1, 4, 0.9, 88, 0.7, true, 0.94),
        LevelDifficulty.Kaizo => new(114, 132, 0.25, 128, 112, 0.0, 92, 2, 4, 1.0, 62, 0.9, true, 0.88),
        _ => new(150, 200, 0.22, 150, 88, 0.6, 104, 1, 3, 0.7, 115, 0.45, false, 1.0),
    };
    [ThreadStatic] private static Tuning? _tuning;
    private static Tuning Tune => _tuning ??= For(LevelDifficulty.Medium);
    private static double Gap(LevelRandom rnd) => Range(rnd, _gapFrom, _gapTo);

    /// <summary>Sine by its series (the library's last digit differs between machines; the level must not).</summary>
    internal static double Sin(double a)
    {
        const double pi = 3.14159265358979323846;
        a -= 2 * pi * Math.Round(a / (2 * pi));
        if (a > pi / 2) a = pi - a; else if (a < -pi / 2) a = -pi - a;
        double a2 = a * a, term = a, sum = a;
        for (int k = 1; k <= 9; k++) { term *= -a2 / ((2 * k) * (2 * k + 1)); sum += term; }
        return sum;
    }
    internal static double Cos(double a) => Sin(a + 3.14159265358979323846 / 2);
    private static (double X, double Y) Dir(double angle) => (Cos(angle), Sin(angle));
    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        double sx = bx - ax, sy = by - ay, l2 = sx * sx + sy * sy;
        double t = l2 <= 0 ? 0 : Math.Clamp(((px - ax) * sx + (py - ay) * sy) / l2, 0, 1);
        return Dist(ax + sx * t, ay + sy * t, px, py);
    }

    public static PlannedLevel Build(LevelStyle style, LevelSize size, FeatureSet features, LevelTheme theme, LevelRandom rnd, int shrink,
        LevelDifficulty difficulty = LevelDifficulty.Medium, int doorways = 0)
    {
        _tuning = For(difficulty);
        _minRoom = doorways > 0 ? 600 : 0;
        for (int attempt = 0; ; attempt++)
        {
            var r = rnd.Fork("try" + attempt);
            var plan = new TerrainPlan { Noise = new TerrainNoise(r.Fork("noise")) };
            var L = new PlannedLevel { Plan = plan };
            plan.LiquidRole = theme.Liquid;
            plan.LiquidKind = (theme.Liquid, Tune.Deadly) switch
            {
                (SurfaceRole.Lava, false) => AuthoredSurfaceKind.Lava, (SurfaceRole.Lava, true) => AuthoredSurfaceKind.DeadlyLava,
                (SurfaceRole.Goo, false) => AuthoredSurfaceKind.Ooze, (SurfaceRole.Goo, true) => AuthoredSurfaceKind.DeadlyOoze,
                (_, true) => AuthoredSurfaceKind.DeadlyWater, _ => AuthoredSurfaceKind.Water,
            };
            switch (style)
            {
                case LevelStyle.Canyon:
                    plan.Kind = EnclosureKind.Cliff; plan.Rise = Range(r, 330, 450); plan.CapWidth = 140; plan.Rough = 44; plan.Fillet = 40;
                    break;
                case LevelStyle.Caverns:
                    plan.Kind = EnclosureKind.Cliff; plan.Rise = Range(r, 430, 520); plan.CapWidth = 110; plan.Rough = 30; plan.Fillet = 40;
                    plan.WallFace = plan.WallTop = SurfaceRole.CaveWall;
                    break;
                case LevelStyle.Islands:
                    plan.Kind = EnclosureKind.Sea; plan.Ripple = 10; plan.Beach = 0; plan.SeaWidth = 620;
                    break;
                case LevelStyle.SkyIslands:
                    plan.Kind = EnclosureKind.Sky; plan.Ripple = 10;
                    break;
                case LevelStyle.Castle:
                    plan.Kind = EnclosureKind.Cliff; plan.Rise = Range(r, 250, 310); plan.CapWidth = 120; plan.Rough = 0; plan.Fillet = 16; plan.Ripple = 3;
                    plan.WallFace = plan.WallTop = SurfaceRole.Brick; plan.CapRole = SurfaceRole.Paving;
                    break;
                default:
                    plan.Kind = theme.Peaks ? EnclosureKind.Peaks : EnclosureKind.Bank;
                    plan.Rise = Range(r, 250, 310); plan.BankWidth = Range(r, 215, 250); plan.CapWidth = theme.Peaks ? 180 : 150;
                    plan.Spikes = theme.Peaks ? 260 : 0;
                    break;
            }
            int count = Math.Max(3, size switch { LevelSize.Small => 4, LevelSize.Large => 8, _ => 6 } - shrink);
            string shape = style switch
            {
                LevelStyle.Canyon => r.Pick<string>([("trail", 3), ("loop", 2)]),
                LevelStyle.Caverns => r.Pick<string>([("trail", 2), ("hub", 1)]),
                LevelStyle.Castle => r.Pick<string>([("hub", 2), ("loop", 2)]),
                _ => r.Pick<string>([("loop", 3), ("hub", 3), ("trail", 1)]),
            };
            // A homeworld: a plaza with a room for each doorway while they fit round it.
            if (doorways > 0) { shape = "hub"; count = Math.Max(count, Math.Min(doorways, 5) + 1); }
            bool apart = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky;
            if (apart) { _scale = 0.72; _gapFrom = 250; _gapTo = 430; count += plan.Kind == EnclosureKind.Sky ? 2 : 1; } else { _scale = style == LevelStyle.Caverns ? 0.8 : 1; _gapFrom = 520; _gapTo = 800; }
            _scale *= Tune.RoomScale;
            _ridge = apart ? 240 : Ridge;
            bool ok = shape switch { "loop" => Loop(L, count, r), "hub" => Hub(L, count, r), _ => Trail(L, count, r) };
            if (!ok || !Clear(L))
            {
                if (attempt < 40) continue;
                // A plain trail always fits.
                L = new PlannedLevel { Plan = new TerrainPlan { Noise = plan.Noise, Kind = plan.Kind, Rise = plan.Rise, BankWidth = plan.BankWidth, CapWidth = plan.CapWidth, Rough = plan.Rough, Fillet = plan.Fillet, Ripple = plan.Ripple, Spikes = plan.Spikes, LiquidRole = plan.LiquidRole, LiquidKind = plan.LiquidKind } };
                Trail(L, Math.Min(count, 4), r);
                shape = "trail";
            }
            L.Shape = shape;
            if (doorways > 0) Doors(L, doorways, r);
            if (features.Secrets && !apart) Nooks(L, features, r);
            Zones(L, style, features, r);
            return L;
        }
    }

    private static PlannedArea Room(PlannedLevel L, double x, double y, double r, double z, string kind = "clearing")
    {
        var a = new PlannedArea { Id = L.Areas.Count, X = x, Y = y, R = r, Z = z, Kind = kind };
        L.Areas.Add(a);
        return a;
    }

    private static void Join(PlannedLevel L, PlannedArea a, PlannedArea b, string kind)
    {
        L.Joins.Add(new(a.Id, b.Id, kind));
        double d = Dist(a.X, a.Y, b.X, b.Y);
        a.Taken.Add(((b.X - a.X) / d, (b.Y - a.Y) / d)); b.Taken.Add(((a.X - b.X) / d, (a.Y - b.Y) / d));
        b.Depth = Math.Max(b.Depth, a.Depth + 1);
    }

    /// <summary>The climb a path across a gap of this width may make: its ramp (which starts and ends a little outside the
    /// rooms) is gentle enough to walk at its steepest.</summary>
    private static double Climb(double gap) => Math.Clamp(Tune.Climb * (gap - 90), 0, 130);

    /// <summary>Rooms round a ring, each a little higher than the last; the last one stands over the first, so the way
    /// round ends with a drop or a glide back to the start.</summary>
    private static bool Loop(PlannedLevel L, int count, LevelRandom rnd)
    {
        count = Math.Max(4, count);
        var radii = new double[count]; var gaps = new double[count];
        for (int k = 0; k < count; k++) { radii[k] = _scale * (k == 0 ? Range(rnd, 600, 700) : rnd.Chance(0.25) ? Range(rnd, 450, 540) : Range(rnd, 580, 820)); gaps[k] = Gap(rnd); }
        const double overlap = 150;
        double around = 0;
        for (int k = 0; k < count; k++) around += radii[k] + radii[(k + 1) % count] + (k == count - 1 ? -overlap : gaps[k]);
        double ring = around / (2 * 3.14159265358979) * 1.06, turn = rnd.Chance(0.5) ? 1 : -1, angle = Range(rnd, 0, 6.28);
        double z = 0, run = 0;
        for (int k = 0; k < count; k++)
        {
            var d = Dir(angle + turn * 2 * 3.14159265358979 * run / around);
            double rho = ring * (k == 0 || k == count - 1 ? 1 : Range(rnd, 0.88, 1.14));
            var a = Room(L, d.X * rho, d.Y * rho, radii[k], 0, k == 0 ? "start" : "clearing");
            run += radii[k] + radii[(k + 1) % count] + (k == count - 1 ? -overlap : gaps[k]);
            if (k == 0) continue;
            var p = L.Areas[k - 1];
            double length = Dist(a.X, a.Y, p.X, p.Y) - a.R - p.R + 240;
            if (length < _gapFrom + 40) return false;
            z += Climb(length - 240) * Range(rnd, 0.45, 1);
            a.Z = z;
            Join(L, p, a, "path");
        }
        // The last room is moved to stand over the first.
        var first = L.Areas[0]; var last = L.Areas[^1];
        // Too low to be an overlook: the ring is tried again (its ramps may not be made steeper).
        if (last.Z < 110) return false;
        if (last.Z > 400) { double drop = 380 / last.Z; foreach (var a in L.Areas) a.Z *= drop; }
        double dx = last.X - first.X, dy = last.Y - first.Y, dd = Math.Sqrt(dx * dx + dy * dy), want = first.R + last.R - overlap;
        last.X = first.X + dx / dd * want; last.Y = first.Y + dy / dd * want;
        last.Kind = "overlook"; last.Priority = first.Priority + 1;
        Join(L, last, first, "overlook");
        return true;
    }

    /// <summary>A big clearing with rooms off it all round, some of them leading further.</summary>
    private static bool Hub(PlannedLevel L, int count, LevelRandom rnd)
    {
        var hub = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, _scale * Range(rnd, 760, 920)), 0, "start");
        int spokes = Math.Clamp(count - 1 - (count > 5 ? rnd.Next(1, 3) : count > 4 ? rnd.Next(0, 2) : 0), 2, 5);
        double angle = Range(rnd, 0, 6.28);
        for (int k = 0; k < spokes; k++)
        {
            var d = Dir(angle + 2 * 3.14159265358979 * (k + Range(rnd, -0.22, 0.22)) / spokes);
            double r = Math.Max(_minRoom, _scale * (rnd.Chance(0.3) ? Range(rnd, 450, 540) : Range(rnd, 560, 780))), gap = Gap(rnd);
            var a = Room(L, d.X * (hub.R + r + gap), d.Y * (hub.R + r + gap), r, 0);
            double climb = Climb(gap);
            a.Z = rnd.Chance(0.7) ? climb * Range(rnd, 0.4, 1) : -climb * Range(rnd, 0.3, 0.8);
            Join(L, hub, a, "path");
        }
        // Further rooms lead on from the outer ones.
        for (int k = L.Areas.Count; k < count; k++)
        {
            bool made = false;
            for (int attempt = 0; attempt < 30 && !made; attempt++)
            {
                var p = L.Areas[rnd.Next(1, L.Areas.Count)];
                if (p.Taken.Count > 2) continue;
                double px = p.X - hub.X, py = p.Y - hub.Y, pd = Math.Sqrt(px * px + py * py), s = Range(rnd, -0.75, 0.75), c = Math.Sqrt(1 - s * s);
                (double X, double Y) d = (px / pd * c - py / pd * s, px / pd * s + py / pd * c);
                double r = Math.Max(_minRoom, _scale * Range(rnd, 500, 720)), gap = Gap(rnd);
                double x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
                if (L.Areas.Any(o => o != p && Dist(o.X, o.Y, x, y) < o.R + r + _ridge + 40)) continue;
                var a = Room(L, x, y, r, 0);
                double climb = Climb(gap);
                a.Z = p.Z + (rnd.Chance(0.75) ? climb * Range(rnd, 0.5, 1) : -climb * Range(rnd, 0.3, 0.8));
                Join(L, p, a, "path");
                made = true;
            }
        }
        return L.Areas.Count >= 3;
    }

    /// <summary>A winding chain of rooms, curling one way.</summary>
    private static bool Trail(PlannedLevel L, int count, LevelRandom rnd)
    {
        double heading = Range(rnd, 0, 6.28), curl = rnd.Chance(0.5) ? 1 : -1;
        var first = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, _scale * Range(rnd, 560, 660)), 0, "start");
        for (int k = 1; k < count; k++)
        {
            bool made = false;
            for (int attempt = 0; attempt < 40 && !made; attempt++)
            {
                var p = L.Areas[^1];
                double a = heading + curl * Range(rnd, -0.3, 1.0);
                var d = Dir(a);
                double r = Math.Max(_minRoom, _scale * (k == count - 1 ? Range(rnd, 580, 740) : rnd.Chance(0.3) ? Range(rnd, 440, 520) : Range(rnd, 540, 780))), gap = Gap(rnd);
                double x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
                if (L.Areas.Any(o => o != p && (Dist(o.X, o.Y, x, y) < o.R + r + _ridge + 40 || SegmentDistance(o.X, o.Y, p.X, p.Y, x, y) < o.R + Math.Min(_ridge, PathClear) + 60))) continue;
                var room = Room(L, x, y, r, 0);
                double climb = Climb(gap);
                room.Z = p.Z + (rnd.Chance(0.7) ? climb * Range(rnd, 0.4, 1) : -climb * Range(rnd, 0.3, 0.9));
                Join(L, p, room, "path");
                heading = a; made = true;
            }
            if (!made) return L.Areas.Count >= 3;
        }
        return true;
    }

    /// <summary>Rooms that are not joined keep a ridge between them; paths pass other rooms and each other well clear; the
    /// whole level fits the built level's coordinates.</summary>
    private static bool Clear(PlannedLevel L)
    {
        var joined = new HashSet<(int, int)>();
        foreach (var j in L.Joins) { joined.Add((j.A, j.B)); joined.Add((j.B, j.A)); }
        for (int i = 0; i < L.Areas.Count; i++)
            for (int k = i + 1; k < L.Areas.Count; k++)
            {
                var a = L.Areas[i]; var b = L.Areas[k];
                if (!joined.Contains((i, k)) && Dist(a.X, a.Y, b.X, b.Y) < a.R + b.R + _ridge) return false;
            }
        var paths = L.Joins.Where(j => j.Kind is "path" or "charge").ToList();
        foreach (var j in paths)
        {
            var a = L.Areas[j.A]; var b = L.Areas[j.B];
            foreach (var o in L.Areas)
                if (o != a && o != b && SegmentDistance(o.X, o.Y, a.X, a.Y, b.X, b.Y) < o.R + Math.Min(_ridge, PathClear)) return false;
            foreach (var other in paths)
            {
                if (other == j || other.A == j.A || other.A == j.B || other.B == j.A || other.B == j.B) continue;
                var c = L.Areas[other.A]; var e = L.Areas[other.B];
                for (int q = 0; q <= 8; q++)
                {
                    double t = q / 8.0;
                    if (SegmentDistance(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, c.X, c.Y, e.X, e.Y) < 2 * Math.Min(_ridge, PathClear)) return false;
                }
            }
        }
        double x0 = L.Areas.Min(a => a.X - a.R), x1 = L.Areas.Max(a => a.X + a.R), y0 = L.Areas.Min(a => a.Y - a.R), y1 = L.Areas.Max(a => a.Y + a.R);
        return x1 - x0 < 9000 && y1 - y0 < 9000;
    }

    /// <summary>Whether a disc beside room <paramref name="a"/> keeps clear of every other room and of paths that are not
    /// the room's own.</summary>
    private static bool Free(PlannedLevel L, PlannedArea a, double x, double y, double r, double ridge = Ridge, bool floor = false)
    {
        // Spyro arrives in the middle of the first room: plain floor all round him.
        var start = L.Areas[0];
        if (!(floor && a == start) && Dist(start.X, start.Y, x, y) < r + 400) return false;
        ridge = Math.Min(ridge, _ridge);
        foreach (var o in L.Areas)
            if (o != a && Dist(o.X, o.Y, x, y) < o.R * 1.25 + r + ridge) return false;
        foreach (var j in L.Joins)
        {
            if (j.Kind is not ("path" or "charge") || j.A == a.Id || j.B == a.Id) continue;
            var p = L.Areas[j.A]; var q = L.Areas[j.B];
            if (SegmentDistance(x, y, p.X, p.Y, q.X, q.Y) < r + Math.Min(ridge, PathClear)) return false;
        }
        return true;
    }

    /// <summary>Whether a disc in room <paramref name="a"/> leaves the room's joins alone: clear of the line from the
    /// room's middle to each room it is joined to, so nothing stands where a path, bridge or stones arrive.</summary>
    private static bool OffJoins(PlannedLevel L, PlannedArea a, double x, double y, double r)
    {
        foreach (var j in L.Joins)
        {
            if (j.A != a.Id && j.B != a.Id) continue;
            var o = L.Areas[j.A == a.Id ? j.B : j.A];
            // From halfway out of the room along the join (the room's middle is open floor either way) to the other room.
            double dx = o.X - a.X, dy = o.Y - a.Y, d = Math.Sqrt(dx * dx + dy * dy), from = 0.5 * a.R;
            if (SegmentDistance(x, y, a.X + dx / d * from, a.Y + dy / d * from, o.X, o.Y) < r + 230) return false;
        }
        // Nor in front of a doorway, where Spyro walks in and comes back out.
        foreach (var door in L.Doors)
            if (door.Area == a.Id && SegmentDistance(x, y, door.X + door.Dx * 120, door.Y + door.Dy * 120, door.X - door.Dx * DoorWalk, door.Y - door.Dy * DoorWalk) < r + 150) return false;
        return true;
    }

    /// <summary>A homeworld's doorways: one to a room, the rooms at the ends of the level first, each on the far side
    /// from the way in with open floor in front of it. With more doorways than rooms the plaza takes one, then the
    /// biggest rooms a second.</summary>
    private static void Doors(PlannedLevel L, int doors, LevelRandom rnd)
    {
        var plaza = L.Areas[0];
        var rooms = L.Areas.Skip(1).Where(a => a.Kind != "overlook").OrderBy(a => a.Taken.Count).ThenByDescending(a => a.Depth).ThenBy(a => a.Id).ToList();
        foreach (var a in rooms.Append(plaza).Concat(rooms.OrderByDescending(a => a.R).ThenBy(a => a.Id)).Append(plaza))
        {
            if (L.Doors.Count >= doors) break;
            Door(L, a, rnd);
        }
        if (L.Doors.Count > 0) L.Count("doorway", L.Doors.Count);
    }

    private static bool Door(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        // The far side from the way in (the plaza: any side), then round from it.
        (double X, double Y) from = a.Id == 0 || a.Taken.Count == 0 ? Dir(Range(rnd, 0, 6.2832)) : (-a.Taken[0].X, -a.Taken[0].Y);
        double jitter = Range(rnd, -0.2, 0.2), at = a.R - DoorInset;
        // The floor Spyro comes back out onto is this room's, well inside its edge.
        if (at - DoorWalk < -(a.R - 110)) return false;
        foreach (double turn in new[] { 0, 0.5, -0.5, 1.0, -1.0, 1.5, -1.5, 2.0, -2.0, 2.5, -2.5, 3.0 })
        {
            double s = Sin(turn + jitter), c = Cos(turn + jitter);
            (double X, double Y) d = (from.X * c - from.Y * s, from.X * s + from.Y * c);
            if (a.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.5)) continue;
            double x = a.X + d.X * at, y = a.Y + d.Y * at;
            if (!Free(L, a, x + d.X * 200, y + d.Y * 200, 120, floor: true) || !OffJoins(L, a, x, y, 110)) continue;
            // Not in the way out of another doorway of this room (their ways may cross in the room's middle).
            if (L.Doors.Any(o => o.Area == a.Id && SegmentDistance(x, y, o.X, o.Y, o.X - o.Dx * DoorWalk, o.Y - o.Dy * DoorWalk) < 330)) continue;
            a.Taken.Add(d);
            L.Doors.Add((x, y, d.X, d.Y, a.Id));
            return true;
        }
        return false;
    }

    /// <summary>A direction out of a room at least <paramref name="apart"/> (cosine) from everything already there.</summary>
    private static (double X, double Y)? FreeDirection(PlannedArea a, LevelRandom rnd, double apart = 0.42)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var d = Dir(Range(rnd, 0, 6.2832));
            if (a.Taken.All(t => t.X * d.X + t.Y * d.Y < apart)) return d;
        }
        return null;
    }

    /// <summary>Hidden nooks: a small room off one of the level's rooms, up a short path; with charge ramps asked for, a
    /// ramp too steep to walk that Spyro climbs by charging.</summary>
    private static void Nooks(PlannedLevel L, FeatureSet features, LevelRandom rnd)
    {
        int want = rnd.Next(1, 3), made = 0;
        var rooms = L.Areas.Where(a => a.Kind != "overlook").ToList();
        rnd.Shuffle(rooms);
        foreach (var a in rooms)
        {
            if (made >= want) break;
            if (FreeDirection(a, rnd, 0.3) is not { } d) continue;
            bool charge = features.ChargePaths && made == 0;
            double r = Range(rnd, 300, 360), gap = charge ? Range(rnd, 340, 400) : Range(rnd, 420, 520);
            double x = a.X + d.X * (a.R + gap + r), y = a.Y + d.Y * (a.R + gap + r);
            if (!Free(L, a, x, y, r + 60)) continue;
            if (L.Areas.Any(o => o != a && SegmentDistance(o.X, o.Y, a.X, a.Y, x, y) < o.R + PathClear)) continue;
            var nook = Room(L, x, y, r, 0, "nook");
            // A charge ramp is steeper than Spyro can walk (22 degrees) and well inside what he climbs charging (30).
            nook.Z = a.Z + (charge ? 0.41 * (gap - 90) : Climb(gap) * Range(rnd, 0.5, 1));
            Join(L, a, nook, charge ? "charge" : "path");
            L.Count(charge ? "nook up a charge ramp" : "hidden nook");
            made++;
        }
    }

    /// <summary>The rooms and joins as zones, then what stands in the rooms.</summary>
    private static void Zones(PlannedLevel L, LevelStyle style, FeatureSet features, LevelRandom rnd)
    {
        var plan = L.Plan;
        bool rocky = plan.Kind == EnclosureKind.Cliff, apart = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky;
        bool castle = style == LevelStyle.Castle, caves = style == LevelStyle.Caverns;
        if (plan.Kind == EnclosureKind.Sea)
        {
            double low = L.Areas.Min(a => a.Z);
            foreach (var a in L.Areas) a.Z = (a.Z - low) * 0.6 + 64;
        }
        foreach (var a in L.Areas)
        {
            a.Zone = plan.Add(new TerrainZone
            {
                Kind = "room", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority, Blend = 0.3 * a.R, Wobble = castle ? 16 : apart ? 30 : rocky ? 60 : 45, Crisp = a.Priority > 1,
                Floor = castle ? SurfaceRole.Paving : SurfaceRole.Ground,
                // Caves: every room but the first is under rock (the first is the way in, open to the sky).
                Roof = caves && a.Id != 0 && (a.Id == 1 || !features.Caves || rnd.Chance(0.8)) ? Range(rnd, 190, 230) : 0,
            });
            // A doorway stands taller than a cave's roof is high: its room's roof is raised over it.
            if (a.Zone.Roof > 0 && L.Doors.Any(door => door.Area == a.Id)) a.Zone.Roof += 150;
            a.Zone.Discs.Add((a.X, a.Y, a.R));
        }
        // Lobes: a room is a blob, not a disc.
        foreach (var a in L.Areas)
        {
            int lobes = rnd.Next(1, 4);
            for (int k = 0; k < lobes; k++)
            {
                if (FreeDirection(a, rnd, 0.75) is not { } d) continue;
                double r = a.R * Range(rnd, 0.5, 0.75), off = a.R * Range(rnd, 0.5, 0.85);
                if (!Free(L, a, a.X + d.X * off, a.Y + d.Y * off, r, floor: true)) continue;
                a.Zone.Discs.Add((a.X + d.X * off, a.Y + d.Y * off, r));
            }
        }
        foreach (var j in L.Joins)
        {
            var p = L.Areas[j.A]; var a = L.Areas[j.B];
            bool charge = j.Kind == "charge";
            if (j.Kind is not ("path" or "charge"))
            {
                L.Links.Add(new LevelLink(p.Id, a.Id, "overlook", new((float)p.X, (float)p.Y, (float)p.Z), new((float)a.X, (float)a.Y, (float)a.Z)));
                L.Count("overlook back over the start");
                continue;
            }
            double dx = a.X - p.X, dy = a.Y - p.Y, d = Math.Sqrt(dx * dx + dy * dy), ux = dx / d, uy = dy / d;
            if (apart && !(features.Bridges && rnd.Chance(Tune.BridgeChance)) && Stones(L, p, a, rnd)) continue;
            // From well inside one room to well inside the other, bowed to one side.
            double inset = apart ? 110 : 160;
            double sx = p.X + ux * (p.R - inset), sy = p.Y + uy * (p.R - inset), ex = a.X - ux * (a.R - inset), ey = a.Y - uy * (a.R - inset);
            double len = Dist(sx, sy, ex, ey), bow = apart ? 0 : Range(rnd, -0.16, 0.16) * len;
            double mx = (sx + ex) / 2 - uy * bow, my = (sy + ey) / 2 + ux * bow;
            var path = plan.Add(new TerrainZone
            {
                Kind = apart ? "bridge" : charge ? "charge" : "path", Z = p.Z, Z1 = a.Z, Area = a.Id, Priority = 0, HalfWidth = apart ? Tune.BridgeHalf : Range(rnd, Tune.PathFrom, Tune.PathTo), Wobble = apart ? 0 : 22, WobbleScale = 260,
                Floor = apart ? SurfaceRole.Wood : charge ? SurfaceRole.Charge : castle ? SurfaceRole.Paving : SurfaceRole.Path, Flat = apart,
                // Under rock: every path to or from a cave; with caves asked for, some paths between rock walls are tunnels.
                Roof = p.Zone.Roof > 0 || a.Zone.Roof > 0 || rocky && !caves && !charge && features.Caves && rnd.Chance(0.5) ? 175 : 0,
            });
            for (int q = 0; q <= 6; q++)
            {
                double t = q / 6.0, u = 1 - t;
                path.Spine.Add((u * u * sx + 2 * u * t * mx + t * t * ex, u * u * sy + 2 * u * t * my + t * t * ey));
            }
            // The ramp lies between the rooms, not inside them.
            path.RampFrom = Math.Min(0.4, (inset + 40) / path.Length); path.RampTo = 1 - path.RampFrom;
            bool level = Math.Abs(a.Z - p.Z) < 6;
            string kind = apart ? "bridge" : charge ? "charge ramp" : path.Roof > 0 && !caves ? "tunnel" : level ? "path" : "ramp";
            L.Links.Add(new LevelLink(p.Id, a.Id, kind, new((float)sx, (float)sy, (float)p.Z), new((float)ex, (float)ey, (float)a.Z)));
            if (!charge) L.Count(kind);
            for (int q = 1; q < 6; q++) L.Gems.Add((path.Spine[q].X, path.Spine[q].Y, path.Id));
        }
        // What stands in the rooms.
        foreach (var a in L.Areas)
        {
            bool big = a.R >= 600;
            if (features.Glides && big && rnd.Chance(Tune.LookoutChance) && (Lookout(L, a, rnd) || Lookout(L, a, rnd) || Lookout(L, a, rnd))) continue;
            int ledges = rnd.Next(Tune.LedgesFrom, Tune.LedgesTo);
            for (int k = 0; k < ledges; k++) Ledges(L, a, rnd, rnd.Chance(0.4) ? 2 : 1);
            if (features.Hazards && big && rnd.Chance(Tune.PoolChance)) Pond(L, a, rnd);
        }
        L.Start = (L.Areas[0].X, L.Areas[0].Y);
        var next = L.Joins.Where(j => j.A == 0 && j.Kind == "path").Select(j => L.Areas[j.B]).FirstOrDefault() ?? L.Areas[Math.Min(1, L.Areas.Count - 1)];
        L.Facing = (next.X, next.Y);
        L.StartZone = L.Areas[0].Zone.Id;
        foreach (var a in L.Areas)
            for (int q = 0; q < 5; q++)
            {
                var d = Dir(Range(rnd, 0, 6.2832));
                L.Gems.Add((a.X + d.X * a.R * 0.5, a.Y + d.Y * a.R * 0.5, a.Zone.Id));
            }
    }

    /// <summary>A chain of stepping stones between two rooms' edges, each within an easy jump of the next.</summary>
    private static bool Stones(PlannedLevel L, PlannedArea p, PlannedArea a, LevelRandom rnd)
    {
        var plan = L.Plan;
        double dx = a.X - p.X, dy = a.Y - p.Y, d = Math.Sqrt(dx * dx + dy * dy), ux = dx / d, uy = dy / d;
        // Where the line between the rooms leaves the one and enters the other.
        double from = 0, to = d;
        for (double t = 0; t < d; t += 8) { if (p.Zone.Sdf(p.X + ux * t, p.Y + uy * t, plan.Noise) > 0) { from = t; break; } }
        for (double t = d; t > 0; t -= 8) { if (a.Zone.Sdf(p.X + ux * t, p.Y + uy * t, plan.Noise) > 0) { to = t; break; } }
        double r = Tune.StoneRadius, jump = Tune.StoneGap;
        double span = to - from;
        if (span < 150 || Math.Abs(a.Z - p.Z) > 150) return false;
        int n = Math.Max(1, (int)Math.Ceiling((span - jump) / (2 * r + jump)));
        double gap = (span - n * 2 * r) / (n + 1);
        if (gap < 30) { n--; if (n < 1) return false; gap = (span - n * 2 * r) / (n + 1); }
        if (Math.Abs(a.Z - p.Z) / (n + 1) > 40) return false;
        var hops = new List<(double X, double Y, double Z)> { (p.X + ux * (from - 110), p.Y + uy * (from - 110), p.Z) };
        for (int k = 0; k < n; k++)
        {
            double t = from + gap + r + k * (2 * r + gap), z = p.Z + (a.Z - p.Z) * (k + 1) / (n + 1.0);
            double side = Range(rnd, -26, 26);
            var stone = plan.Add(new TerrainZone { Kind = "stone", Z = z, Z1 = z, Area = a.Id, Priority = 0, Blend = 0, Wobble = 6, WobbleScale = 150, Flat = true, Floor = SurfaceRole.Rock });
            stone.Discs.Add((p.X + ux * t - uy * side, p.Y + uy * t + ux * side, r));
            L.Gems.Add((p.X + ux * t - uy * side, p.Y + uy * t + ux * side, stone.Id));
            hops.Add((p.X + ux * t - uy * side, p.Y + uy * t + ux * side, z));
        }
        hops.Add((p.X + ux * (to + 110), p.Y + uy * (to + 110), a.Z));
        // Each hop, for the in-game route checks: from a floor's middle to the next, jumping where the floor ends.
        for (int k = 0; k + 1 < hops.Count; k++)
            L.Links.Add(new LevelLink(p.Id, a.Id, "hop", new((float)hops[k].X, (float)hops[k].Y, (float)hops[k].Z), new((float)hops[k + 1].X, (float)hops[k + 1].Y, (float)hops[k + 1].Z))
            { JumpAt = (float)(k == 0 ? 110 : r) });
        L.Count("chain of stepping stones");
        return true;
    }

    private static TerrainZone Step(PlannedLevel L, PlannedArea a, double x, double y, double r, double z, int priority, string kind = "ledge")
    {
        var zone = L.Plan.Add(new TerrainZone { Kind = kind, Z = z, Z1 = z, Area = a.Id, Priority = priority, Blend = 0, Wobble = 14, WobbleScale = 240, Crisp = true });
        zone.Discs.Add((x, y, r));
        return zone;
    }

    /// <summary>One or two steps up at a room's edge, each a jump high.</summary>
    private static bool Ledges(PlannedLevel L, PlannedArea a, LevelRandom rnd, int steps)
    {
        if (FreeDirection(a, rnd) is not { } d) return false;
        double r1 = Math.Clamp(a.R * Range(rnd, 0.38, 0.5), 240, 400), off = a.R * Range(rnd, 0.72, 0.9);
        double x = a.X + d.X * off, y = a.Y + d.Y * off;
        if (!Free(L, a, x, y, r1) || !OffJoins(L, a, x, y, r1)) return false;
        a.Taken.Add(d);
        var first = Step(L, a, x, y, r1, a.Z + SpyroMoves.DesignLedge, a.Priority + 1);
        L.Count("ledge");
        // The jump up: from the room's floor in front of the step (towards the room's middle) onto it.
        L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", new((float)(x - d.X * (r1 + 90)), (float)(y - d.Y * (r1 + 90)), (float)a.Z),
            new((float)(x - d.X * (r1 - 110)), (float)(y - d.Y * (r1 - 110)), (float)(a.Z + SpyroMoves.DesignLedge))) { JumpAt = 60 });
        L.Gems.Add((x + d.X * r1 * 0.3, y + d.Y * r1 * 0.3, first.Id));
        if (steps > 1 && r1 >= 300)
        {
            double r2 = r1 * 0.66, x2 = x + d.X * r1 * 0.5, y2 = y + d.Y * r1 * 0.5;
            if (Free(L, a, x2, y2, r2))
            {
                var second = Step(L, a, x2, y2, r2, a.Z + 2 * SpyroMoves.DesignLedge, a.Priority + 2);
                L.Count("ledge");
                L.Gems.Add((x2 + d.X * r2 * 0.3, y2 + d.Y * r2 * 0.3, second.Id));
            }
        }
        return true;
    }

    /// <summary>Three steps up to a high place at a room's edge, and across the room from it a ledge too high to jump
    /// onto: Spyro reaches it by gliding from the top.</summary>
    private static bool Lookout(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (FreeDirection(a, rnd) is not { } d) return false;
        double step = SpyroMoves.DesignLedge;
        // The top is wide enough to stand and take a run on once it is made of faces (a disc this size comes out as a
        // polygon a little smaller).
        double r1 = Math.Clamp(0.5 * a.R, 340, 420), r2 = r1 * 0.76, r3 = Math.Max(200, r2 * 0.7);
        double o1 = 0.8 * a.R, o2 = o1 + 0.55 * r1, o3 = o2 + 0.5 * r2;
        (double X, double Y) c1 = (a.X + d.X * o1, a.Y + d.Y * o1), c2 = (a.X + d.X * o2, a.Y + d.Y * o2), c3 = (a.X + d.X * o3, a.Y + d.Y * o3);
        if (!Free(L, a, c1.X, c1.Y, r1) || !Free(L, a, c3.X, c3.Y, r3) || !OffJoins(L, a, c1.X, c1.Y, r1)) return false;
        // The ledge to glide to: round the room's edge from the steps, too high to jump onto from the floor or the first
        // step, and as far from the top as a glide carries with room to spare.
        double top = 3 * step, target = top - Range(rnd, 30, 42), tr = Range(rnd, 200, 240);
        double reach = SpyroMoves.GlideReach((float)(top - target));
        (double X, double Y)? landing = null; (double X, double Y) e = default;
        double side = rnd.Chance(0.5) ? 1 : -1;
        for (int tries = 0; tries < 2 && landing == null; tries++, side = -side)
            foreach (double out_ in new[] { 1.15, 1.0, 1.28 })
                for (double angle = 0.56; angle <= 1.5 && landing == null; angle += 0.04)
                {
                    double s = Sin(angle) * side, c = Cos(angle);
                    e = (d.X * c - d.Y * s, d.X * s + d.Y * c);
                    double x = a.X + e.X * a.R * out_, y = a.Y + e.Y * a.R * out_;
                    double gap = Dist(x, y, c3.X, c3.Y) - r3 - tr;
                    // Spyro may leave the top a little before its edge and must clear the ledge's edge: well inside the glide.
                    if (gap < 170 || gap > reach - Tune.GlideSpare) continue;
                    // Clear of the second step, so it is the glide from the top that reaches it (the first step is
                    // too far below it to jump from).
                    if (Dist(x, y, c2.X, c2.Y) < r2 + tr + 120) continue;
                    if (a.Taken.Any(t => t.X * e.X + t.Y * e.Y > 0.55) || !Free(L, a, x, y, tr) || !OffJoins(L, a, x, y, tr)) continue;
                    landing = (x, y);
                }
        if (landing == null) return false;
        a.Taken.Add(d); a.Taken.Add(e);
        Step(L, a, c1.X, c1.Y, r1, a.Z + step, a.Priority + 1);
        Step(L, a, c2.X, c2.Y, r2, a.Z + 2 * step, a.Priority + 2);
        var summit = Step(L, a, c3.X, c3.Y, r3, a.Z + top, a.Priority + 3, "lookout");
        var ledge = Step(L, a, landing.Value.X, landing.Value.Y, tr, a.Z + target, a.Priority + 1, "glide ledge");
        L.Count("lookout with a glide");
        L.Glides.Add((summit.Id, ledge.Id));
        L.Links.Add(new LevelLink(a.Id, a.Id, "glide", new((float)c3.X, (float)c3.Y, (float)(a.Z + top)), new((float)landing.Value.X, (float)landing.Value.Y, (float)(a.Z + target))) { JumpAt = (float)(r3 - 45) });
        L.Gems.Add((c3.X, c3.Y, summit.Id));
        for (int q = -1; q <= 1; q++) L.Gems.Add((landing.Value.X + e.Y * q * 70, landing.Value.Y - e.X * q * 70, ledge.Id));
        return true;
    }

    /// <summary>A pool at the side of a room, its shore sloping in.</summary>
    private static bool Pond(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (FreeDirection(a, rnd, 0.3) is not { } d) return false;
        double r = Math.Clamp(a.R * 0.26, 150, 250), off = a.R * 0.56;
        if (Dist(L.Areas[0].X, L.Areas[0].Y, a.X + d.X * off, a.Y + d.Y * off) < 1.7 * r + 400) return false;
        if (!OffJoins(L, a, a.X + d.X * off, a.Y + d.Y * off, 1.4 * r - 60)) return false;
        a.Taken.Add(d);
        var pond = L.Plan.Add(new TerrainZone
        {
            Kind = "pond", Z = a.Z - 26, Z1 = a.Z - 26, Area = a.Id, Priority = a.Priority + 6, Blend = 0, Wobble = 26, WobbleScale = 200, Crisp = true, Flat = true,
            Floor = L.Plan.LiquidRole, Liquid = L.Plan.LiquidKind,
        });
        pond.Discs.Add((a.X + d.X * off, a.Y + d.Y * off, r));
        pond.Discs.Add((a.X + d.X * off - d.Y * r * 0.7, a.Y + d.Y * off + d.X * r * 0.7, r * 0.7));
        pond.Blend = r * 0.5;
        L.Count("pool");
        return true;
    }
}
