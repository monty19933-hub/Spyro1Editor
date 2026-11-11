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
    /// <summary>Rooms of one group stand together as one place (a winding gorge, a flight of terraces): no ridge of
    /// land is kept between them. -1: a room on its own.</summary>
    public int Group = -1;
    /// <summary>The big thing the layout wants built in this room ("mount": a hill of terraces), or "".</summary>
    public string Want = "";
    /// <summary>The big thing that was built in it ("" none): a room takes one.</summary>
    public string Piece = "";
}

/// <summary>How two rooms are joined: a sloping path between them ("path", "charge"), or (rooms that touch) the same
/// floor running on ("open"), a step Spyro jumps ("step"), a flight of two or three such steps ("flight") or an
/// overlook he can only drop or glide from ("overlook": A is the higher).</summary>
internal sealed record PlannedJoin(int A, int B, string Kind);

/// <summary>A level laid out the way the game's own levels are: areas strung along a route that winds round on itself,
/// each a little higher or lower than the last, joined by sloping paths, with ledges to jump up, high places to glide
/// from and ways back to the start.</summary>
internal sealed class PlannedLevel
{
    public required TerrainPlan Plan;
    /// <summary>The layout: journey, loop, hub, arena, climb, gorge, web (or trail, the plain fallback).</summary>
    public string Shape = "";
    /// <summary>How big the level's rooms are: grand (a few big ones), mixed, fine (many small ones).</summary>
    public string Grain = "";
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
    /// <summary>A homeworld's doorways: where each stands, the way Spyro walks into it (unit), its room, and how far the
    /// floor is kept open in front of it and behind it.</summary>
    public readonly List<(double X, double Y, double Dx, double Dy, int Area, double Front, double Behind)> Doors = [];
    /// <summary>High places that want something tall standing on them (a tower on the hill): where, on which zone's
    /// floor, and how much room there is round the middle.</summary>
    public readonly List<(double X, double Y, int Zone, double Room)> Landmarks = [];
    public (double X, double Y) Start, Facing;
    /// <summary>Where Spyro starts when that is not the middle of the first room (a room with a hill in its middle).</summary>
    public (double X, double Y)? StartAt;
    public int StartZone;
    public double Spacing = 128;
    public void Count(string what, int n = 1) => Counts[what] = (Counts.TryGetValue(what, out int c) ? c : 0) + n;
}

internal static partial class SpyroPlanner
{
    /// <summary>The ridge of land kept between rooms that are not joined, and between a room and a path that passes it.</summary>
    private const double Ridge = 560, PathClear = 470;
    /// <summary>This level's room size (1 = a valley's), the gaps its joins cross, and the space kept between rooms that
    /// are not joined. Set per level in <see cref="Build"/> (one level is built at a time per thread).</summary>
    [ThreadStatic] private static double _scale, _gapFrom, _gapTo, _ridge;
    /// <summary>The smallest room of a homeworld (0: any size): a doorway's room holds the doorway and the floor in
    /// front of it that Spyro comes back out onto.</summary>
    [ThreadStatic] private static double _minRoom;
    /// <summary>The level's grain (<see cref="PlannedLevel.Grain"/>) and the floor its rooms may cover in all (square units).</summary>
    [ThreadStatic] private static string? _grain;
    [ThreadStatic] private static double _land;
    /// <summary>The floor kept open in front of a doorway: coming back from a level Spyro lands up to 700 units in
    /// front of it (Artisans' doorways; measured in the game). Some homeworlds' doorways (three of Beast Makers')
    /// set him down a little way behind it instead, so a doorway stands in the middle of its room with open floor
    /// both ways.</summary>
    internal const double DoorWalk = 860;

    /// <summary>What a difficulty changes: how wide paths are, how much of the walkable slope a ramp uses, how big
    /// stepping stones are and how far apart, how often islands get a bridge and how wide it is, how many ledges a room
    /// has, how often a room has a lookout, how much of a glide is left to spare, how often rooms have pools, whether
    /// liquid costs a life at once, and how big rooms are.</summary>
    private sealed record Tuning(double PathFrom, double PathTo, double Climb, double StoneRadius, double StoneGap, double BridgeChance, double BridgeHalf,
        int LedgesFrom, int LedgesTo, double LookoutChance, double GlideSpare, double PoolChance, bool Deadly, double RoomScale);
    private static Tuning For(LevelDifficulty difficulty) => difficulty switch
    {
        LevelDifficulty.Easy => new(190, 230, 0.18, 165, 70, 0.9, 118, 0, 2, 0.4, 170, 0.2, false, 1.05),
        LevelDifficulty.Hard => new(128, 160, 0.26, 138, 100, 0.3, 96, 1, 4, 0.9, 88, 0.7, true, 0.94),
        LevelDifficulty.Kaizo => new(114, 132, 0.27, 128, 112, 0.0, 92, 2, 4, 1.0, 62, 0.9, true, 0.88),
        _ => new(150, 200, 0.25, 150, 88, 0.6, 104, 1, 3, 0.7, 115, 0.45, false, 1.0),
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
    /// <summary>A direction turned anticlockwise by an angle.</summary>
    private static (double X, double Y) Turn((double X, double Y) d, double angle)
    {
        double s = Sin(angle), c = Cos(angle);
        return (d.X * c - d.Y * s, d.X * s + d.Y * c);
    }
    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        double sx = bx - ax, sy = by - ay, l2 = sx * sx + sy * sy;
        double t = l2 <= 0 ? 0 : Math.Clamp(((px - ax) * sx + (py - ay) * sy) / l2, 0, 1);
        return Dist(ax + sx * t, ay + sy * t, px, py);
    }

    /// <summary>The layouts a kind of level may take, with how often (a homeworld needs rooms for its doorways).</summary>
    internal static IReadOnlyList<(string Shape, double Weight)> Layouts(LevelStyle style, bool homeworld) => (style, homeworld) switch
    {
        (LevelStyle.Canyon, false) => [("gorge", 3), ("journey", 2), ("loop", 2), ("climb", 2), ("web", 1.5), ("arena", 1)],
        (LevelStyle.Caverns, false) => [("journey", 2.5), ("web", 2), ("gorge", 2), ("hub", 1.5), ("arena", 1)],
        (LevelStyle.Castle, false) => [("hub", 2), ("loop", 2), ("web", 2), ("arena", 2), ("climb", 2), ("journey", 1.5)],
        (LevelStyle.Islands or LevelStyle.SkyIslands, false) => [("journey", 2), ("loop", 2), ("hub", 2), ("web", 2.5), ("climb", 2), ("arena", 1)],
        (_, false) => [("journey", 2), ("loop", 2), ("hub", 1.5), ("arena", 2), ("climb", 2), ("web", 2), ("gorge", 1.5)],
        (LevelStyle.Caverns, true) => [("hub", 3), ("web", 2), ("journey", 1.5)],
        (_, true) => [("hub", 3), ("arena", 2), ("web", 2.5), ("loop", 2), ("journey", 1.5), ("climb", 1.5)],
    };

    /// <summary>Every layout's name, for an explicit choice.</summary>
    internal static readonly string[] AllLayouts = ["journey", "loop", "hub", "arena", "climb", "gorge", "web"];

    /// <param name="layout">The layout to build (<see cref="AllLayouts"/>), or null for the seed's own choice.</param>
    /// <param name="squeeze">Builds the same level with smaller rooms (0: full size): the level came out with more
    /// faces than its size allows.</param>
    public static PlannedLevel Build(LevelStyle style, LevelSize size, FeatureSet features, LevelTheme theme, LevelRandom rnd, int shrink,
        LevelDifficulty difficulty = LevelDifficulty.Medium, int doorways = 0, string? layout = null, int squeeze = 0)
    {
        _tuning = For(difficulty);
        _minRoom = doorways > 0 ? 600 : 0;
        // The level's own layout and grain: the same for every try at placing it.
        var own = rnd.Fork("layout");
        string wanted = layout != null && Array.IndexOf(AllLayouts, layout) >= 0 ? layout : own.Pick<string>(Layouts(style, doorways > 0));
        string grain = own.Pick<string>([("grand", 2.0), ("mixed", 4.0), ("fine", 2.2)]);
        // A homeworld's doorways each want a room of their own size: no crowd of little rooms.
        if (doorways > 0 && grain == "fine") grain = "mixed";
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
                    plan.Kind = EnclosureKind.Cliff; plan.Rise = Range(r, 320, 500); plan.CapWidth = 140; plan.Rough = 44; plan.Fillet = 40;
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
                    plan.Kind = EnclosureKind.Cliff; plan.Rise = Range(r, 250, 330); plan.CapWidth = 120; plan.Rough = 0; plan.Fillet = 16; plan.Ripple = 3;
                    plan.WallFace = plan.WallTop = SurfaceRole.Brick; plan.CapRole = SurfaceRole.Paving;
                    break;
                default:
                    plan.Kind = theme.Peaks ? EnclosureKind.Peaks : EnclosureKind.Bank;
                    plan.Rise = Range(r, 240, 330); plan.BankWidth = Range(r, 205, 250); plan.CapWidth = theme.Peaks ? 180 : 140;
                    plan.Spikes = theme.Peaks ? 260 : 0;
                    break;
            }
            int weight = Math.Max(3, size switch { LevelSize.Small => 5, LevelSize.Large => 11, _ => 8 } - shrink);
            bool apart = plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky;
            if (apart) { _scale = 0.72; _gapFrom = 250; _gapTo = 430; weight += plan.Kind == EnclosureKind.Sky ? 2 : 1; } else { _scale = style == LevelStyle.Caverns ? 0.8 : 1; _gapFrom = 520; _gapTo = 800; }
            _scale *= Tune.RoomScale;
            for (int k = 0; k < squeeze; k++) _scale *= 0.92;
            _ridge = apart ? 240 : Ridge;
            // The layout asked for while it can be placed; then the plain layouts that always could; a trail at the last.
            string shape = attempt < 24 ? wanted : doorways > 0 ? "hub" : style switch
            {
                LevelStyle.Canyon or LevelStyle.Caverns => "journey", LevelStyle.Castle => "hub", _ => attempt % 2 == 0 ? "loop" : "hub",
            };
            _grain = attempt < 24 ? grain : "mixed";
            // The floor the rooms may cover: what the game can hold and draw of this kind of level.
            _land = (size switch { LevelSize.Small => 6.5e6, LevelSize.Large => 15.5e6, _ => 11e6 }) * (apart ? 0.62 : style == LevelStyle.Caverns ? 0.8 : 1);
            for (int k = 0; k < shrink + squeeze; k++) _land *= 0.86;
            bool ok = shape switch
            {
                "loop" => Loop(L, weight, r), "hub" => Hub(L, weight, r, false), "arena" => Hub(L, weight, r, true), "climb" => Climb(L, weight, r),
                "gorge" => Gorge(L, weight, r), "web" => Web(L, weight, r), _ => Journey(L, weight, r),
            };
            ok = ok && Clear(L);
            if (ok && doorways > 0)
            {
                Doors(L, doorways, r);
                // Every doorway has its room, or the layout is tried again (the plain plaza at the last takes what fits).
                if (L.Doors.Count < Math.Min(doorways, 5) && attempt < 40) ok = false;
            }
            if (!ok)
            {
                if (attempt < 40) continue;
                // A plain trail always fits.
                L = new PlannedLevel { Plan = new TerrainPlan { Noise = plan.Noise, Kind = plan.Kind, Rise = plan.Rise, BankWidth = plan.BankWidth, CapWidth = plan.CapWidth, Rough = plan.Rough, Fillet = plan.Fillet, Ripple = plan.Ripple, Spikes = plan.Spikes, LiquidRole = plan.LiquidRole, LiquidKind = plan.LiquidKind, WallFace = plan.WallFace, WallTop = plan.WallTop, CapRole = plan.CapRole, Beach = plan.Beach, SeaWidth = plan.SeaWidth } };
                plan = L.Plan;
                _grain = "mixed";
                Trail(L, Math.Min(weight, 4), r);
                shape = "trail";
                if (doorways > 0) Doors(L, doorways, r);
            }
            L.Shape = shape; L.Grain = _grain ?? "mixed";
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
    private static double Climb(double gap, double most = 130) => Math.Clamp(Tune.Climb * (gap - 90), 0, most);

    /// <summary>Whether two rooms may stand as close as they like: they belong to one group.</summary>
    private static bool Together(PlannedArea a, PlannedArea b) => a.Group >= 0 && a.Group == b.Group;
    /// <summary>How far from a room's edge a path out of a room of its own group passes it.</summary>
    private const double PassBeside = 330;

    /// <summary>Puts two rooms that touch in one group (and with them the rooms already grouped with either).</summary>
    private static void Unite(PlannedLevel L, PlannedArea a, PlannedArea b)
    {
        if (a.Group < 0 && b.Group < 0) a.Group = b.Group = L.Areas.Max(o => o.Group) + 1;
        else if (a.Group < 0) a.Group = b.Group;
        else if (b.Group < 0) b.Group = a.Group;
        else if (a.Group != b.Group) { int from = b.Group; foreach (var o in L.Areas) if (o.Group == from) o.Group = a.Group; }
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
                if (joined.Contains((i, k))) continue;
                // Rooms of one group may stand side by side, but not on top of each other.
                if (Dist(a.X, a.Y, b.X, b.Y) < a.R + b.R + (Together(a, b) ? 30 : _ridge)) return false;
            }
        var paths = L.Joins.Where(j => j.Kind is "path" or "charge").ToList();
        foreach (var j in paths)
        {
            var a = L.Areas[j.A]; var b = L.Areas[j.B];
            foreach (var o in L.Areas)
            {
                if (o == a || o == b) continue;
                // A room that stands together with the path's end: the path leads away from it or passes beside it,
                // not across it.
                double beside = SegmentDistance(o.X, o.Y, a.X, a.Y, b.X, b.Y);
                if (Together(o, a)) { if (Cosine(o.X - a.X, o.Y - a.Y, b.X - a.X, b.Y - a.Y) > 0.25 && beside < o.R + PassBeside) return false; continue; }
                if (Together(o, b)) { if (Cosine(o.X - b.X, o.Y - b.Y, a.X - b.X, a.Y - b.Y) > 0.25 && beside < o.R + PassBeside) return false; continue; }
                if (beside < o.R + Math.Min(_ridge, PathClear)) return false;
            }
            foreach (var other in paths)
            {
                if (other == j || other.A == j.A || other.A == j.B || other.B == j.A || other.B == j.B) continue;
                var c = L.Areas[other.A]; var e = L.Areas[other.B];
                // Paths out of one floor (a gorge's rooms) leave it side by side, as paths out of one room do.
                if (Together(a, c) || Together(a, e) || Together(b, c) || Together(b, e)) continue;
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

    /// <summary>How far a flight of so many steps reaches out over the lower room's floor from the higher room's edge.</summary>
    internal static double FlightReach(int steps) => steps <= 2 ? 160 : 140 * (steps - 1) + 10;

    private static double Cosine(double ax, double ay, double bx, double by)
    {
        double l = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
        return l <= 0 ? 1 : (ax * bx + ay * by) / l;
    }

    /// <summary>Whether a new room at a place keeps clear of every room but <paramref name="from"/> (the one it is
    /// joined to) and of the paths, and whether the way to it from that room passes the other rooms clear.</summary>
    private static bool Fits(PlannedLevel L, PlannedArea? from, double x, double y, double r, int group = -1, bool path = true)
    {
        foreach (var o in L.Areas)
        {
            if (o == from) continue;
            bool same = group >= 0 && o.Group == group;
            if (Dist(o.X, o.Y, x, y) < o.R + r + (same ? 40 : _ridge + 40)) return false;
            if (!path || from == null) continue;
            // A room that stands together with the room the way leaves: the way leads away from it or passes beside it.
            double beside = SegmentDistance(o.X, o.Y, from.X, from.Y, x, y);
            if (Together(o, from)) { if (Cosine(o.X - from.X, o.Y - from.Y, x - from.X, y - from.Y) > 0.25 && beside < o.R + PassBeside + 30) return false; continue; }
            if (beside < o.R + Math.Min(_ridge, PathClear) + 60) return false;
        }
        foreach (var j in L.Joins)
        {
            if (j.Kind is not ("path" or "charge")) continue;
            var p = L.Areas[j.A]; var q = L.Areas[j.B];
            if (p == from || q == from) continue;
            if (SegmentDistance(x, y, p.X, p.Y, q.X, q.Y) < r + Math.Min(_ridge, PathClear) + 40) return false;
        }
        return true;
    }

    /// <summary>Whether a disc beside room <paramref name="a"/> keeps clear of every other room and of paths that are not
    /// the room's own.</summary>
    private static bool Free(PlannedLevel L, PlannedArea a, double x, double y, double r, double ridge = Ridge, bool floor = false)
    {
        // Spyro arrives on plain floor: nothing is built round him.
        var start = L.Areas[0];
        var at = L.StartAt ?? (start.X, start.Y);
        if (!(floor && a == start) && Dist(at.X, at.Y, x, y) < r + 400) return false;
        ridge = Math.Min(ridge, _ridge);
        foreach (var o in L.Areas)
        {
            if (o == a) continue;
            // A room of the same group stands right beside this one: the disc keeps off its floor, no more.
            if (Together(o, a)) { if (Dist(o.X, o.Y, x, y) < o.R + r + 50) return false; continue; }
            if (Dist(o.X, o.Y, x, y) < o.R * 1.25 + r + ridge) return false;
        }
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
            if (door.Area == a.Id && (SegmentDistance(x, y, door.X + door.Dx * door.Behind, door.Y + door.Dy * door.Behind, door.X - door.Dx * door.Front, door.Y - door.Dy * door.Front) < r + 150 ||
                // Nor beside the doorway itself, where its frame stands across the way in (up to 250 out either side).
                Dist(x, y, door.X, door.Y) < r + 300)) return false;
        return true;
    }

    /// <summary>A homeworld's doorways: one to a room, the rooms at the ends of the level first, each in the middle of
    /// its room with open floor in front and behind. With more doorways than rooms the plaza takes up to two.</summary>
    private static void Doors(PlannedLevel L, int doors, LevelRandom rnd)
    {
        var plaza = L.Areas[0];
        var rooms = L.Areas.Skip(1).Where(a => a.Kind != "overlook" && a.Want == "").OrderBy(a => a.Taken.Count).ThenByDescending(a => a.Depth).ThenBy(a => a.Id).ToList();
        IEnumerable<PlannedArea> order = plaza.Want == "" ? rooms.Append(plaza).Append(plaza) : rooms;
        foreach (var a in order)
        {
            if (L.Doors.Count >= doors) break;
            Door(L, a, rnd);
        }
        if (L.Doors.Count > 0) L.Count("doorway", L.Doors.Count);
    }

    private static bool Door(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        bool plaza = a.Id == 0;
        // A room takes one doorway, about its middle (a little towards the back, so the floor in front is long enough
        // for the longest way out of a doorway): at the end of the level facing away from the way in; in a room the way
        // leads on through, turned across it so it does not stand in the way. The plaza (Spyro arrives in its middle)
        // takes them halfway out, facing outwards, up to two well apart.
        if (L.Doors.Count(o => o.Area == a.Id) >= (plaza ? 2 : 1)) return false;
        (double X, double Y) from = plaza || a.Taken.Count == 0 ? Dir(Range(rnd, 0, 6.2832)) : (-a.Taken[0].X, -a.Taken[0].Y);
        double jitter = plaza ? Range(rnd, -0.2, 0.2) : 0, at = plaza ? 0.5 * a.R : Math.Max(0, 800 - a.R);
        double front = plaza ? Math.Min(DoorWalk, at + a.R - 130) : at + a.R - 90, behind = a.R - at - 120;
        if (behind < 200 || front < 700) return false;
        double side = rnd.Chance(0.5) ? 1 : -1;
        double[] turns = plaza ? [0, 0.5, -0.5, 1.0, -1.0, 1.5, -1.5, 2.0, -2.0, 2.5, -2.5, 3.0]
            : a.Taken.Count <= 1 ? [0, 0.35, -0.35, 0.7, -0.7] : [1.571 * side, -1.571 * side, 1.2 * side, -1.2 * side, 1.95 * side, -1.95 * side, 0, 0.5, -0.5];
        foreach (double turn in turns)
        {
            double s = Sin(turn + jitter), c = Cos(turn + jitter);
            (double X, double Y) d = (from.X * c - from.Y * s, from.X * s + from.Y * c);
            // No way in or out of the room straight behind it, nor (but for the way in, which it may face) in front.
            if (a.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.6) || a.Taken.Skip(plaza ? 0 : 1).Any(t => t.X * d.X + t.Y * d.Y < -0.6)) continue;
            double x = a.X + d.X * at, y = a.Y + d.Y * at;
            if (plaza && (!OffJoins(L, a, x, y, 110) || L.Doors.Any(o => o.Area == a.Id && o.Dx * d.X + o.Dy * d.Y > -0.2))) continue;
            // Clear of the steps of a flight that stand on this room's floor, all along the way in and out.
            bool steps = false;
            foreach (var j in L.Joins)
            {
                if (j.Kind is not ("flight" or "step") || j.A != a.Id && j.B != a.Id) continue;
                var o = L.Areas[j.A == a.Id ? j.B : j.A];
                if (o.Z < a.Z) continue;
                var e = Unit(o.X - a.X, o.Y - a.Y);
                // Where the higher room's edge crosses this floor, and the steps in front of it.
                double edge = Dist(a.X, a.Y, o.X, o.Y) - o.R;
                double reach = FlightReach((int)Math.Round((o.Z - a.Z) / SpyroMoves.DesignLedge));
                if (SegmentDistance(a.X + e.X * (edge - reach / 2), a.Y + e.Y * (edge - reach / 2), x + d.X * behind, y + d.Y * behind, x - d.X * front, y - d.Y * front) < reach / 2 + 110 + 170) steps = true;
            }
            if (steps) continue;
            a.Taken.Add(d);
            L.Doors.Add((x, y, d.X, d.Y, a.Id, front, behind));
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
        int want = rnd.Next(1, 4), made = 0;
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
        // Where rooms touch at different heights the higher one's floor lies over the lower one's.
        for (int pass = 0; pass < L.Areas.Count; pass++)
        {
            bool changed = false;
            foreach (var j in L.Joins)
            {
                if (j.Kind is "path" or "charge") continue;
                var p = L.Areas[j.A]; var q = L.Areas[j.B];
                var (low, high) = p.Z <= q.Z ? (p, q) : (q, p);
                // Over a flight of steps too: each step lies over the one below.
                int over = j.Kind == "flight" ? (int)Math.Round((high.Z - low.Z) / SpyroMoves.DesignLedge) : 1;
                if (high.Z - low.Z > 1 && high.Priority < low.Priority + over) { high.Priority = low.Priority + over; changed = true; }
            }
            if (!changed) break;
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
            // A small room's outline wanders less, so it keeps its size; so does one whose edge is a flight of steps.
            if (a.R < 420) a.Zone.Wobble = Math.Min(a.Zone.Wobble, 0.09 * a.R);
            if (L.Joins.Any(j => j.Kind is "flight" or "step" && (j.A == a.Id || j.B == a.Id))) a.Zone.Wobble = Math.Min(a.Zone.Wobble, 14);
            // A doorway's room keeps its size all round: the floor in front of the doorway runs to its edge.
            if (a.Id != 0 && L.Doors.Any(door => door.Area == a.Id)) a.Zone.Wobble = Math.Min(a.Zone.Wobble, 20);
            // A doorway stands taller than a cave's roof is high: its room's roof is raised over it.
            if (a.Zone.Roof > 0 && L.Doors.Any(door => door.Area == a.Id)) a.Zone.Roof += 150;
            // A hill of terraces stands in the open.
            if (a.Want is "mount" or "mount+") a.Zone.Roof = 0;
            if (castle && a.R >= 420 && a.Want == "")
            {
                // A castle's yards are square-cornered, each turned to face the way into it (the first: any way).
                var along = a.Taken.Count > 0 ? a.Taken[0] : Dir(Range(rnd, 0, 6.2832));
                // A doorway's yard is no smaller any way than the round room its place was measured in.
                bool doorway = L.Doors.Any(door => door.Area == a.Id);
                a.Zone.Boxes.Add((a.X, a.Y, a.R * (doorway ? Range(rnd, 1.0, 1.06) : Range(rnd, 0.84, 0.98)), a.R * (doorway ? Range(rnd, 1.0, 1.06) : Range(rnd, 0.8, 0.96)), along.X, along.Y, Range(rnd, 50, 110)));
                a.Zone.Blend = 60;
            }
            else a.Zone.Discs.Add((a.X, a.Y, a.R));
        }
        L.Start = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
        // Lobes: a room is a blob, not a disc.
        foreach (var a in L.Areas)
        {
            int lobes = rnd.Next(1, 4);
            // Well away from where rooms touch: a lobe would bury the steps between them.
            bool touching = L.Joins.Any(j => j.Kind is not ("path" or "charge") && (j.A == a.Id || j.B == a.Id));
            for (int k = 0; k < lobes; k++)
            {
                if (FreeDirection(a, rnd, touching ? 0.2 : 0.75) is not { } d) continue;
                double r = a.R * Range(rnd, 0.5, 0.75), off = a.R * Range(rnd, 0.5, 0.85);
                if (!Free(L, a, a.X + d.X * off, a.Y + d.Y * off, r, floor: true)) continue;
                a.Zone.Discs.Add((a.X + d.X * off, a.Y + d.Y * off, r));
            }
        }
        foreach (var j in L.Joins)
        {
            var p = L.Areas[j.A]; var a = L.Areas[j.B];
            bool charge = j.Kind == "charge";
            double dx = a.X - p.X, dy = a.Y - p.Y, d = Math.Sqrt(dx * dx + dy * dy), ux = dx / d, uy = dy / d;
            if (j.Kind == "open")
            {
                // One floor: walked from the middle of the one to the middle of the other.
                L.Links.Add(new LevelLink(p.Id, a.Id, "path", new((float)p.X, (float)p.Y, (float)p.Z), new((float)a.X, (float)a.Y, (float)a.Z)));
                L.Gems.Add(((p.X + a.X) / 2, (p.Y + a.Y) / 2, p.Zone.Id));
                continue;
            }
            if (j.Kind == "step")
            {
                // The higher room's edge is a step in the lower room's floor: jumped from in front of it.
                var (low, high) = p.Z <= a.Z ? (p, a) : (a, p);
                double sx0 = low.X - high.X, sy0 = low.Y - high.Y, sl = Math.Sqrt(sx0 * sx0 + sy0 * sy0);
                double ex0 = high.X + sx0 / sl * high.R, ey0 = high.Y + sy0 / sl * high.R;
                L.Links.Add(new LevelLink(low.Id, high.Id, "ledge", new((float)(ex0 + sx0 / sl * 100), (float)(ey0 + sy0 / sl * 100), (float)low.Z),
                    new((float)(ex0 - sx0 / sl * 120), (float)(ey0 - sy0 / sl * 120), (float)high.Z)) { JumpAt = 60 });
                L.Count("step between rooms");
                continue;
            }
            if (j.Kind == "flight")
            {
                // Two or three steps up from the lower room's floor to the higher room's edge, each a jump.
                var (low, high) = p.Z <= a.Z ? (p, a) : (a, p);
                var e = Unit(high.X - low.X, high.Y - low.Y);
                (double X, double Y) q = (high.X - e.X * high.R, high.Y - e.Y * high.R);
                int steps = (int)Math.Round((high.Z - low.Z) / SpyroMoves.DesignLedge);
                double out1 = FlightReach(steps);
                // Each step a disc let into the next: the lowest reaches furthest out, every tread about 140 deep.
                for (int k = 1; k < steps; k++)
                {
                    double reach = k == steps - 1 && steps == 2 ? 160 : 140 * (steps - k) + 10, radius = steps == 2 ? 170 : 160 + 70 * (steps - 1 - k);
                    Step(L, low, q.X - e.X * (reach - radius), q.Y - e.Y * (reach - radius), radius, low.Z + k * SpyroMoves.DesignLedge, low.Priority + k);
                }
                L.Links.Add(new LevelLink(low.Id, high.Id, "ledge", new((float)(q.X - e.X * (out1 + 90)), (float)(q.Y - e.Y * (out1 + 90)), (float)low.Z),
                    new((float)(q.X - e.X * (out1 - 100)), (float)(q.Y - e.Y * (out1 - 100)), (float)(low.Z + SpyroMoves.DesignLedge))) { JumpAt = 60 });
                L.Count("flight of steps");
                continue;
            }
            if (j.Kind is not ("path" or "charge"))
            {
                L.Links.Add(new LevelLink(p.Id, a.Id, "overlook", new((float)p.X, (float)p.Y, (float)p.Z), new((float)a.X, (float)a.Y, (float)a.Z)));
                L.Count(a.Id == 0 ? "overlook back over the start" : "overlook over an earlier room");
                continue;
            }
            if (apart && !(features.Bridges && rnd.Chance(Tune.BridgeChance)) && Stones(L, p, a, rnd)) continue;
            // From well inside one room to well inside the other, bowed to one side (a long way winds both ways). Into a
            // room that is to have a hill of terraces the path ends just inside its edge, leaving the floor to the hill.
            double inset = apart ? 110 : Math.Min(160, 0.45 * Math.Min(p.R, a.R));
            double insetP = p.Want.StartsWith("mount") && !apart ? 70 : inset, insetA = a.Want.StartsWith("mount") && !apart ? 70 : inset;
            double sx = p.X + ux * (p.R - insetP), sy = p.Y + uy * (p.R - insetP), ex = a.X - ux * (a.R - insetA), ey = a.Y - uy * (a.R - insetA);
            double len = Dist(sx, sy, ex, ey), bow = apart ? 0 : Range(rnd, -0.16, 0.16) * len;
            bool winding = !apart && !charge && len > 1250;
            if (winding) bow = (rnd.Chance(0.5) ? 1 : -1) * Range(rnd, 0.09, 0.13) * len;
            double mx = (sx + ex) / 2 - uy * bow, my = (sy + ey) / 2 + ux * bow;
            var path = plan.Add(new TerrainZone
            {
                Kind = apart ? "bridge" : charge ? "charge" : "path", Z = p.Z, Z1 = a.Z, Area = a.Id, Priority = 0, HalfWidth = apart ? Tune.BridgeHalf : Range(rnd, Tune.PathFrom, Tune.PathTo), Wobble = apart ? 0 : 22, WobbleScale = 260,
                Floor = apart ? SurfaceRole.Wood : charge ? SurfaceRole.Charge : castle ? SurfaceRole.Paving : SurfaceRole.Path, Flat = apart,
                // Under rock: every path to or from a cave; with caves asked for, some paths between rock walls are tunnels.
                Roof = p.Zone.Roof > 0 || a.Zone.Roof > 0 || rocky && !caves && !charge && features.Caves && rnd.Chance(0.5) ? 175 : 0,
            });
            if (winding)
            {
                // An S: out to one side over the first half, to the other over the second.
                double q1x = sx + (ex - sx) * 0.25 - uy * bow, q1y = sy + (ey - sy) * 0.25 + ux * bow, q2x = sx + (ex - sx) * 0.75 + uy * bow, q2y = sy + (ey - sy) * 0.75 - ux * bow;
                for (int q = 0; q <= 10; q++)
                {
                    double t = q / 10.0, u = 1 - t;
                    path.Spine.Add((u * u * u * sx + 3 * u * u * t * q1x + 3 * u * t * t * q2x + t * t * t * ex, u * u * u * sy + 3 * u * u * t * q1y + 3 * u * t * t * q2y + t * t * t * ey));
                }
            }
            else
                for (int q = 0; q <= 6; q++)
                {
                    double t = q / 6.0, u = 1 - t;
                    path.Spine.Add((u * u * sx + 2 * u * t * mx + t * t * ex, u * u * sy + 2 * u * t * my + t * t * ey));
                }
            // The ramp lies between the rooms, not inside them.
            path.RampFrom = Math.Min(0.4, (insetP + 40) / path.Length); path.RampTo = 1 - Math.Min(0.4, (insetA + 40) / path.Length);
            bool level = Math.Abs(a.Z - p.Z) < 6;
            string kind = apart ? "bridge" : charge ? "charge ramp" : path.Roof > 0 && !caves ? "tunnel" : level ? "path" : "ramp";
            if (winding)
            {
                // Walked in three stretches, as it winds.
                int n = path.Spine.Count - 1;
                for (int part = 0; part < 3; part++)
                {
                    var from = path.Spine[n * part / 3]; var to = path.Spine[n * (part + 1) / 3];
                    L.Links.Add(new LevelLink(p.Id, a.Id, kind, new((float)from.X, (float)from.Y, (float)path.FloorAt(from.X, from.Y)), new((float)to.X, (float)to.Y, (float)path.FloorAt(to.X, to.Y))));
                }
            }
            else L.Links.Add(new LevelLink(p.Id, a.Id, kind, new((float)sx, (float)sy, (float)p.Z), new((float)ex, (float)ey, (float)a.Z)));
            if (!charge) L.Count(kind);
            for (int q = 1; q < path.Spine.Count - 1; q++) L.Gems.Add((path.Spine[q].X, path.Spine[q].Y, path.Id));
        }
        Furnish(L, style, features, rnd);
        var first = L.Areas[0];
        var next = L.Joins.Where(j => j.A == 0 && j.Kind is "path" or "open" or "step" or "flight").Select(j => L.Areas[j.B]).FirstOrDefault() ?? L.Areas[Math.Min(1, L.Areas.Count - 1)];
        // Facing the way on; from a start at a room's edge, the room's middle.
        L.Facing = L.StartAt != null ? (first.X, first.Y) : (next.X, next.Y);
        L.StartZone = plan.ZoneAt(L.Start.X, L.Start.Y) is int under and >= 0 ? under : first.Zone.Id;
        foreach (var a in L.Areas)
            for (int q = 0; q < (a.R >= 800 ? 8 : a.R < 420 ? 3 : 5); q++)
            {
                var d = Dir(Range(rnd, 0, 6.2832));
                double x = a.X + d.X * a.R * Range(rnd, 0.35, 0.7), y = a.Y + d.Y * a.R * Range(rnd, 0.35, 0.7);
                int zone = plan.ZoneAt(x, y);
                if (zone >= 0 && plan.Zones[zone].Liquid == null) L.Gems.Add((x, y, zone));
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
}
