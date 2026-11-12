namespace Spyro.Editor.Core.Generation;

/// <summary>Layouts of one floor of many parts, the way most of the game's levels are built: parts of every size run
/// into each other, some on the level, some a step or a flight of steps up or down, now and then a path out to a part
/// further off; no ring of round rooms on the ends of paths.
/// <list type="bullet">
/// <item><c>field</c>: a wide land round a great middle, mostly on the level, with raised and sunken parts (Artisans,
/// Stone Hill, Peace Keepers).</item>
/// <item><c>plateaus</c>: a low floor with plateaus standing round and in it, two to four steps up, some joined to each
/// other (Dry Canyon, Toasty, Gnorc Cove).</item>
/// <item><c>terraces</c>: long terraces climbing a hillside one over the other, zigzagging up (Magic Crafters, Terrace
/// Village, Alpine Ridge).</item>
/// <item><c>courts</c>: squared yards side by side on a grid, each at its own height (Town Square, Dark Hollow, Lofty
/// Castle).</item>
/// </list>
/// A homeworld built this way holds its doorways round the edges of its big parts, backs to the banks, facing in.</summary>
internal static partial class SpyroPlanner
{
    private static bool Cluster(PlannedLevel L, int weight, LevelRandom rnd, string kind)
    {
        bool terraces = kind == "terraces", courts = kind == "courts", plateaus = kind == "plateaus";
        // Floating land and islands: from a few big islands to a whole archipelago.
        int count = Rooms(weight, rnd, terraces ? 4 : 5, terraces ? 9 : 12) + (_gapTo < 500 ? rnd.Pick<int>([(-2, 1.0), (0, 1.0), (2, 1.0), (5, 1.0)]) : 0);
        double grid = Range(rnd, 0, 6.2832);
        var climb = Dir(Range(rnd, 0, 6.2832));
        (double X, double Y) across = (-climb.Y, climb.X);
        double coreR = terraces ? Rad(rnd, rnd.Chance(0.5) ? 2 : 3) : courts ? Rad(rnd, rnd.Chance(0.6) ? 2 : 3) : Math.Max(_scale * Range(rnd, 860, 1100), Rad(rnd, 3));
        // A homeworld's middle is a plaza big enough for two or three of its doorways round its edge.
        if (_minRoom > 0 && !terraces) coreR = Math.Max(coreR, Range(rnd, 900, 1060));
        var core = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, coreR), 0, "start");
        core.Group = 0;
        if (terraces) { core.Long = across; core.Stretch = Range(rnd, 1.25, 1.45); }
        if (courts) core.Long = Dir(grid);
        // A plateau level's low floor: two or three big parts on the level before the plateaus.
        int low = plateaus ? rnd.Next(2, 4) : 0;
        int side = rnd.Chance(0.5) ? 1 : -1, made = 1;
        // Which way the level leans: every level of a kind is not alike.
        double upward = terraces ? 1 : rnd.Pick<double>([(0.75, 3.0), (0.5, 1.5), (0.3, 1.0)]);
        var top = core;
        // Islands and floating land: the parts mostly meet at other heights (one floor on the level is one island).
        bool apart = _gapTo < 500;
        // A homeworld goes on until its parts hold its doorways (a big part two or three, a clearing one).
        bool More() => L.Areas.Count < count || _minRoom > 0 && L.Areas.Count < count + 4 &&
            L.Areas.Where(a => a.Kind is "start" or "clearing").Sum(a => a.R >= 900 ? 3 : a.R >= 700 ? 2 : a.R >= 580 ? 1 : 0) < 7;
        for (int tries = 0; More() && tries < 70 * count; tries++)
        {
            // The part this one runs on from.
            PlannedArea p;
            if (terraces) p = rnd.Chance(0.8) ? top : L.Areas[rnd.Next(L.Areas.Count)];
            else
            {
                var options = L.Areas.Where(a => a.Kind is "start" or "clearing" && a.Taken.Count < 4)
                    // A land that climbs builds on its high parts: plateaus on plateaus, a shelf over a shelf.
                    .Select(a => (a, Weight: a.R / 600 * (a.Taken.Count < 3 ? 1.0 : 0.3) * (a.Id == 0 && kind == "field" ? 1.6 : 1) *
                        (plateaus && a.Z > core.Z + 40 ? 0.75 : 1) * (1 + upward * Math.Max(0, a.Z - core.Z) / 220))).ToList();
                if (options.Count == 0) break;
                p = rnd.Pick<PlannedArea>([.. options]);
            }
            if (p.Group < 0) continue;
            // A yard beside a yard; the next terrace up and to one side, or a landing off a terrace's end; anywhere.
            double end = rnd.Chance(0.5) ? 1 : -1;
            (double X, double Y) d = courts ? Dir(grid + 1.5708 * rnd.Next(4))
                : terraces && p == top ? Turn(climb, side * Range(rnd, 0.3, 0.85))
                : terraces ? (across.X * end, across.Y * end)
                : Dir(Range(rnd, 0, 6.2832));
            if (p.Taken.Any(t => t.X * d.X + t.Y * d.Y > (courts ? 0.8 : 0.45))) continue;
            // How it meets the part it runs on from.
            string how;
            if (plateaus && made < low) how = "open";
            else if (terraces && p == top) how = rnd.Pick<string>([("flight", 3.2), ("step", 0.8), ("path", 1.4)]);
            else if (terraces) how = rnd.Pick<string>([("open", 2.0), ("step", 1.0)]);
            // On a plateau: its own further parts, one more plateau stood on it, or a way down off it.
            else if (plateaus) how = p.Z > core.Z + 40 ? rnd.Pick<string>([("flight", 2.4), ("open", 0.7), ("step", 1.0), ("path", 0.8)]) : rnd.Pick<string>([("flight", 3.4), ("open", 0.8), ("step", 0.5), ("path", 0.7)]);
            else if (courts) how = rnd.Pick<string>([("open", 2.2), ("flight", 2.0), ("step", 1.6), ("path", 0.3)]);
            else how = rnd.Pick<string>([("open", apart && _minRoom <= 0 ? 1.2 : 3.0), ("flight", 2.4), ("step", 1.6), ("path", apart ? 1.2 : 0.6)]);
            int cls = terraces ? rnd.Pick<int>([(1, 2.0), (2, 3.0), (3, 1.5)]) : plateaus && made >= low ? rnd.Pick<int>([(1, 2.5), (2, 2.5), (3, 1.0)]) : Cls(rnd);
            // An island that stands over the next, up a flight of steps, is big enough to hold the steps.
            if (apart && how == "flight" && cls < 3) cls++;
            double r = Rad(rnd, cls);
            if (!Spare(L, r)) { if (L.Areas.Count >= Math.Min(count, 4) && !(_minRoom > 0 && More())) break; r = Rad(rnd, 1); }
            // Up or down: a terrace and a plateau go up; elsewhere the level leans one way; nothing climbs for ever.
            bool up = how != "open" && (terraces && p == top || plateaus && (p.Z <= core.Z + 40 || how == "flight" && rnd.Chance(0.8)) || rnd.Chance(upward));
            if (p.Z - core.Z > 1000) up = false;
            if (p.Z - core.Z < -340) up = true;
            PlannedArea? a = null;
            switch (how)
            {
                case "flight":
                    a = Flight(L, p, d, r, up, rnd, apart ? 3 : 2);
                    break;
                case "open":
                case "step":
                    a = Beside(L, p, d, r, how, up, rnd);
                    break;
                default:
                {
                    // A path out to a part further off (a terrace's ramp up to the next).
                    double gap = Gap(rnd), x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
                    if (!Fits(L, p, x, y, r)) break;
                    double rise = Climb(gap, 190) * Range(rnd, terraces ? 0.8 : 0.5, 1);
                    a = Room(L, x, y, r, p.Z + (up ? rise : -rise));
                    Join(L, p, a, "path");
                    // Still the one land (a terrace up its ramp stands with the others).
                    if (terraces) Unite(L, p, a);
                    break;
                }
            }
            if (a == null) continue;
            made++;
            if (terraces)
            {
                a.Long = across; a.Stretch = Range(rnd, 1.25, 1.5);
                if (p == top && a.Z > top.Z) { top = a; side = -side; }
            }
            if (courts) a.Long = Dir(grid);
        }
        if (L.Areas.Count < 3) return false;
        // A level (not a homeworld) has a side room or two up or down a path, where treasure waits.
        if (_minRoom <= 0 && !courts) Spurs(L, rnd, rnd.Next(0, 2));
        return Clear(L);
    }

    /// <summary>A part right against another, overlapping it: the same floor running on ("open"), or a step Spyro
    /// jumps up or down ("step"). Null when it does not fit there.</summary>
    private static PlannedArea? Beside(PlannedLevel L, PlannedArea p, (double X, double Y) d, double r, string how, bool up, LevelRandom rnd, double most = double.MaxValue)
    {
        double small = Math.Min(p.R, r);
        double overlap = Math.Min(most, how == "open" ? small * Range(rnd, 0.32, 0.55) : Math.Max(230, small * Range(rnd, 0.42, 0.62)));
        double x = p.X + d.X * (p.R + r - overlap), y = p.Y + d.Y * (p.R + r - overlap);
        if (!Fits(L, p, x, y, r, p.Group, path: false)) return null;
        // Not out of the part right beside another of its ways.
        if (p.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.45)) return null;
        // Spyro arrives in the middle of the first part: a step up keeps well off him.
        if (how == "step" && p.Id == 0 && up && p.R - overlap < 300) return null;
        var a = Room(L, x, y, r, p.Z + (how == "step" ? (up ? 1 : -1) * SpyroMoves.DesignLedge : 0));
        Join(L, p, a, how);
        Unite(L, p, a);
        return a;
    }

    /// <summary>A doorway near the edge of a part, its back to the bank and its face to the land: Spyro walks in from
    /// the land and comes back out onto it. The open floor in front of it may run on into a part on the same level;
    /// a big part takes two or three, well round from each other.</summary>
    private static bool EdgeDoor(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (a.R < 560) return false;
        // The parts this one runs on into on the level: the floor in front of a doorway may go on across them.
        var level = L.Joins.Where(j => (j.A == a.Id || j.B == a.Id) && j.Kind == "open").Select(j => L.Areas[j.A == a.Id ? j.B : j.A]).Where(o => Math.Abs(o.Z - a.Z) < 1).Prepend(a).ToList();
        double start = Range(rnd, 0, 6.2832);
        foreach (double inset in new[] { 330.0, 400.0, 480.0 })
            for (int k = 0; k < 24; k++)
            {
                var e = Dir(start + 6.2832 * k / 24);
                double at = a.R - inset;
                if (at < 120) continue;
                // Its back to the bank: no way in or out of the part behind it.
                if (a.Taken.Any(t => t.X * e.X + t.Y * e.Y > 0.5)) continue;
                double x = a.X + e.X * at, y = a.Y + e.Y * at;
                if (L.Doors.Any(o => Dist(o.X, o.Y, x, y) < 650 || SegmentDistance(x, y, o.X + o.Dx * o.Behind, o.Y + o.Dy * o.Behind, o.X - o.Dx * o.Front, o.Y - o.Dy * o.Front) < 360)) continue;
                foreach (double turn in new[] { 0, 0.4, -0.4, 0.8, -0.8 })
                {
                    // The way Spyro walks into it: out towards the bank, straight or slanting.
                    var d = Turn(e, turn);
                    if (a.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.5)) continue;
                    double behind = Math.Min(260, inset - 110);
                    foreach (double front in new[] { DoorWalk, 740.0 })
                    {
                        if (!Corridor(L, a, level, x, y, d, front, behind)) continue;
                        a.Taken.Add(d);
                        L.Doors.Add((x, y, d.X, d.Y, a.Id, front, behind));
                        return true;
                    }
                }
            }
        return false;
    }

    /// <summary>Whether the floor in front of a doorway (and behind it) is level floor all the way: on the part or a
    /// part on the same level it runs on into, clear of parts at other heights and the steps up to them, of the paths
    /// coming in and of other doorways.</summary>
    private static bool Corridor(PlannedLevel L, PlannedArea a, List<PlannedArea> level, double x, double y, (double X, double Y) d, double front, double behind)
    {
        // The flights of steps that stand on these floors: from the higher part's edge out over the lower floor, the
        // widest step this far either side.
        var steps = new List<(double X0, double Y0, double X1, double Y1, double Half)>();
        foreach (var j in L.Joins)
        {
            if (j.Kind != "flight") continue;
            var p = L.Areas[j.A]; var q = L.Areas[j.B];
            var (low, high) = p.Z <= q.Z ? (p, q) : (q, p);
            if (!level.Contains(low)) continue;
            var e = Unit(high.X - low.X, high.Y - low.Y);
            int n = (int)Math.Round((high.Z - low.Z) / SpyroMoves.DesignLedge);
            double ex = high.X - e.X * high.R, ey = high.Y - e.Y * high.R, reach = FlightReach(n);
            steps.Add((ex, ey, ex - e.X * reach, ey - e.Y * reach, n == 2 ? 170 : 160 + 70 * (n - 2)));
        }
        for (double s = -behind; s <= front + 45; s += 45)
        {
            double t = Math.Min(s, front), px = x - d.X * t, py = y - d.Y * t;
            if (!level.Any(o => Dist(px, py, o.X, o.Y) <= o.R - 75)) return false;
            if (t < 0 && Dist(px, py, a.X, a.Y) > a.R - 75) return false;
            // A part at another height that runs into these, and the steps up to one.
            foreach (var o in L.Areas)
                if (!level.Contains(o) && Math.Abs(o.Z - a.Z) >= 1 && Dist(px, py, o.X, o.Y) < o.R + 130) return false;
            foreach (var st in steps)
                if (SegmentDistance(px, py, st.X0, st.Y0, st.X1, st.Y1) < st.Half + 150) return false;
            foreach (var j in L.Joins)
            {
                if (j.Kind is not ("path" or "charge")) continue;
                var p = L.Areas[j.A]; var q = L.Areas[j.B];
                if (!level.Contains(p) && !level.Contains(q)) continue;
                var (from, to) = level.Contains(p) ? (p, q) : (q, p);
                var u = Unit(to.X - from.X, to.Y - from.Y);
                if (SegmentDistance(px, py, from.X + u.X * 0.5 * from.R, from.Y + u.Y * 0.5 * from.R, to.X, to.Y) < 250) return false;
            }
            foreach (var o in L.Doors)
                if (Dist(px, py, o.X, o.Y) < 360) return false;
        }
        return true;
    }

    /// <summary>A one-floor homeworld's doorways: one to each part first (round the edge of a big part, in the middle
    /// of a smaller one), then more round the edges of the big parts.</summary>
    private static void ClusterDoors(PlannedLevel L, int doors, LevelRandom rnd)
    {
        var plaza = L.Areas[0];
        var parts = L.Areas.Where(a => a.Kind is "start" or "clearing" && a.Want == "").ToList();
        rnd.Shuffle(parts);
        // The far parts first, so the doorways are spread over the land.
        parts = [.. parts.OrderByDescending(a => a.Depth + (a == plaza ? -1 : 0) + 0.5 * rnd.NextDouble())];
        var middle = new HashSet<int>();
        foreach (var a in parts)
        {
            if (L.Doors.Count >= doors) break;
            if (EdgeDoor(L, a, rnd)) continue;
            if (Door(L, a, rnd) && a != plaza) middle.Add(a.Id);
        }
        foreach (var a in parts.Where(a => a.R >= 700 && !middle.Contains(a.Id)).OrderByDescending(a => a.R))
            while (L.Doors.Count < doors && EdgeDoor(L, a, rnd)) { }
    }
}
