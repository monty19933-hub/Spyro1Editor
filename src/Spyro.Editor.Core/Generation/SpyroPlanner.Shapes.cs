namespace Spyro.Editor.Core.Generation;

/// <summary>The outlines of the rooms. The game's floors are seldom round: rock shelves with flat sides and corners
/// (Peace Keepers, Dry Canyon, Ice Cavern), long terraces (Magic Crafters, Terrace Village), square yards and streets
/// (Town Square, Dark Hollow, Lofty Castle), meadows that wander (Artisans, Stone Hill). A room is planned as a round
/// one of radius R (everything in it is placed inside that); its outline is then drawn out round it as far as the
/// rooms and paths beside it allow: flat across every way in or out (so a path or a flight of steps meets a straight
/// edge exactly where it was planned), out into corners and long sides elsewhere.</summary>
internal static partial class SpyroPlanner
{
    /// <summary>The level's own kind of outline: round (meadows), rock (shelves with flat sides), slab (squared, long
    /// terraces and yards), box (a castle's own yards, drawn as squares in <see cref="Zones"/>).</summary>
    [ThreadStatic] private static string? _outline;
    /// <summary>The farthest a room's outline reaches, in its radii.</summary>
    private const double OutlineReach = 1.45;

    private static string OutlineFor(LevelStyle style, string layout, LevelRandom rnd) => (style, layout) switch
    {
        (_, "courts") => "slab",
        // A castle of one floor: squared yards drawn round their whole room (a doorway's open floor may run on from
        // one yard into the next).
        (LevelStyle.Castle, "terraces" or "field" or "plateaus") => "slab",
        (LevelStyle.Castle, _) => "box",
        (_, "terraces") => rnd.Chance(0.6) ? "slab" : "rock",
        (LevelStyle.Canyon or LevelStyle.Caverns, _) => rnd.Pick<string>([("rock", 6.0), ("slab", 2.0), ("round", 1.0)]),
        (LevelStyle.Islands or LevelStyle.SkyIslands, _) => rnd.Pick<string>([("rock", 3.0), ("round", 3.0), ("slab", 1.2)]),
        _ => rnd.Pick<string>([("rock", 3.0), ("round", 2.6), ("slab", 2.0)]),
    };

    /// <summary>The distance from a point to a room's floor as planned so far (its outline, else its round floor).</summary>
    private static double RoomGap(PlannedArea o, double x, double y) => o.Outline is { } v ? TerrainHull.Distance(v, x - o.X, y - o.Y) : Dist(o.X, o.Y, x, y) - o.R;

    /// <summary>Draws every room's outline (biggest first: they take the room there is).</summary>
    private static void Outlines(PlannedLevel L, LevelRandom rnd)
    {
        string level = _outline ?? "round";
        // Rooms that overlap where they are joined (the same floor running on, a step, a flight of steps, an overlook).
        var touching = new HashSet<(int, int)>();
        foreach (var j in L.Joins) if (j.Kind is not ("path" or "charge")) { touching.Add((j.A, j.B)); touching.Add((j.B, j.A)); }
        var shaped = new HashSet<int>();
        foreach (var a in L.Areas.OrderByDescending(a => a.R).ThenBy(a => a.Id).ToList())
        {
            string look = a.Look != "" ? a.Look : level == "box" ? "box" : rnd.Chance(0.82) ? level : rnd.Pick<string>([("round", 1.0), ("rock", 1.0), ("slab", 0.7)]);
            a.Look = look;
            if (look is "round" or "box") { shaped.Add(a.Id); continue; }
            var sides = new List<(double Nx, double Ny, double Off)>();
            (double X, double Y) u = a.Long ?? (a.Taken.Count > 0 ? a.Taken[0] : Dir(Range(rnd, 0, 6.2832)));
            (double X, double Y) v = (-u.Y, u.X);
            double along = a.Long != null ? a.Stretch : Range(rnd, 1.05, 1.32), across = a.Long != null ? Range(rnd, 1.0, 1.06) : Range(rnd, 1.0, 1.12);
            if (look == "slab")
            {
                sides.Add((u.X, u.Y, a.R * Math.Max(1, along * Range(rnd, 0.92, 1.0)))); sides.Add((-u.X, -u.Y, a.R * Math.Max(1, along * Range(rnd, 0.92, 1.0))));
                sides.Add((v.X, v.Y, a.R * across)); sides.Add((-v.X, -v.Y, a.R * across));
                // Most yards and terraces have their corners cut.
                if (rnd.Chance(0.6))
                    foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                    {
                        var n = Unit(u.X * sx + v.X * sy, u.Y * sx + v.Y * sy);
                        sides.Add((n.X, n.Y, a.R * Range(rnd, 1.2, 1.36)));
                    }
            }
            else
            {
                // A rock shelf: five to eight flat sides, some well out, some close in, longer one way.
                int n = rnd.Next(5, 9);
                double t0 = Range(rnd, 0, 6.2832);
                for (int k = 0; k < n; k++)
                {
                    var e = Dir(t0 + 6.2832 * (k + Range(rnd, -0.3, 0.3)) / n);
                    double lengthwise = Math.Abs(e.X * u.X + e.Y * u.Y);
                    double off = Range(rnd, 1.0, 1.3) * (1 + (along - 1) * lengthwise * lengthwise);
                    sides.Add((e.X, e.Y, a.R * Math.Max(1, off)));
                }
            }
            // Flat across every way in or out, a doorway's back and front, at the round room's own edge.
            foreach (var t in a.Taken) sides.Add((t.X, t.Y, a.R));
            foreach (var door in L.Doors.Where(d => d.Area == a.Id)) { sides.Add((door.Dx, door.Dy, a.R)); sides.Add((-door.Dx, -door.Dy, a.R)); }
            var outline = Clip(sides, a.R);
            // Drawn back wherever it would come too near another room or a path, or reach too far.
            for (int pass = 0; pass < 10; pass++)
            {
                bool cut = false;
                foreach (var (px, py) in Probe(outline))
                {
                    double reach = Math.Sqrt(px * px + py * py);
                    if (reach <= a.R + 1) continue;
                    if (reach <= OutlineReach * a.R + 1 && Clearing(L, a, a.X + px, a.Y + py, touching, shaped)) continue;
                    // Out along this way only as far as it stays clear.
                    double ex = px / reach, ey = py / reach, t = a.R;
                    for (double s = a.R + 20; s <= Math.Min(reach, OutlineReach * a.R); s += 20)
                    {
                        if (!Clearing(L, a, a.X + ex * s, a.Y + ey * s, touching, shaped)) break;
                        t = s;
                    }
                    sides.Add((ex, ey, Math.Max(a.R, t - 10)));
                    cut = true;
                }
                if (!cut) break;
                outline = Clip(sides, a.R);
            }
            a.Outline = outline;
            a.Out = outline.Max(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
            shaped.Add(a.Id);
        }
    }

    /// <summary>The corners of an outline and the middles and thirds of its sides.</summary>
    private static IEnumerable<(double X, double Y)> Probe((double X, double Y)[] outline)
    {
        for (int k = 0; k < outline.Length; k++)
        {
            var p = outline[k]; var q = outline[(k + 1) % outline.Length];
            yield return p;
            for (int s = 1; s <= 3; s++) yield return (p.X + (q.X - p.X) * s / 4, p.Y + (q.Y - p.Y) * s / 4);
        }
    }

    /// <summary>Whether a room's floor may reach a point beyond its round floor: clear of every other room (a room it
    /// is joined to or stands with, by a little; any other by most of a ridge), of the paths, and of a room joined to it
    /// where the two floors meet at another height.</summary>
    private static bool Clearing(PlannedLevel L, PlannedArea a, double x, double y, HashSet<(int, int)> touching, HashSet<int> shaped)
    {
        foreach (var o in L.Areas)
        {
            if (o == a) continue;
            bool near = Together(a, o);
            // A room not drawn yet may still reach out as far as an outline can.
            double gap = shaped.Contains(o.Id) ? RoomGap(o, x, y) : Dist(o.X, o.Y, x, y) - OutlineReach * o.R;
            if (touching.Contains((a.Id, o.Id)))
            {
                // Rooms that overlap where they are joined: the meeting stays as it was planned (a step or a flight of
                // steps stands there), so neither reaches further into the other's round floor.
                if (Dist(o.X, o.Y, x, y) < o.R + 70) return false;
                continue;
            }
            if (gap < (near ? 70 : Math.Max(160, 0.55 * _ridge))) return false;
        }
        // Nor (at another height) over the open floor in front of and behind a doorway.
        foreach (var door in L.Doors)
        {
            if (Math.Abs(L.Areas[door.Area].Z - a.Z) < 1) continue;
            if (SegmentDistance(x, y, door.X + door.Dx * door.Behind, door.Y + door.Dy * door.Behind, door.X - door.Dx * door.Front, door.Y - door.Dy * door.Front) < 190) return false;
        }
        foreach (var j in L.Joins)
        {
            if (j.Kind is not ("path" or "charge")) continue;
            var p = L.Areas[j.A]; var q = L.Areas[j.B];
            double beside = SegmentDistance(x, y, p.X, p.Y, q.X, q.Y);
            if (j.A == a.Id || j.B == a.Id)
            {
                // Its own path: not out along beside it (the path's ramp starts at the round floor's edge).
                var o = j.A == a.Id ? q : p;
                var e = Unit(o.X - a.X, o.Y - a.Y);
                if ((x - a.X) * e.X + (y - a.Y) * e.Y > a.R - 40 && beside < 330) return false;
                continue;
            }
            if (beside < 380) return false;
        }
        return true;
    }

    /// <summary>The polygon inside every side (each a direction out and how far): a square well round the room cut
    /// down by each in turn. Anticlockwise.</summary>
    private static (double X, double Y)[] Clip(List<(double Nx, double Ny, double Off)> sides, double r)
    {
        double big = 4 * r;
        var poly = new List<(double X, double Y)> { (-big, -big), (big, -big), (big, big), (-big, big) };
        foreach (var (nx, ny, off) in sides)
        {
            var next = new List<(double X, double Y)>(poly.Count + 1);
            for (int k = 0; k < poly.Count; k++)
            {
                var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                double sp = p.X * nx + p.Y * ny - off, sq = q.X * nx + q.Y * ny - off;
                if (sp <= 0) next.Add(p);
                if (sp < 0 != sq < 0 && Math.Abs(sp - sq) > 1e-9)
                {
                    double t = sp / (sp - sq);
                    next.Add((p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t));
                }
            }
            if (next.Count >= 3) poly = next;
        }
        // Points closer than a few units run together (they make slivers of faces).
        var clean = new List<(double X, double Y)>();
        foreach (var p in poly)
            if (clean.Count == 0 || Dist(p.X, p.Y, clean[^1].X, clean[^1].Y) > 6) clean.Add(p);
        if (clean.Count > 3 && Dist(clean[0].X, clean[0].Y, clean[^1].X, clean[^1].Y) <= 6) clean.RemoveAt(clean.Count - 1);
        return [.. clean];
    }
}
