namespace Spyro.Editor.Core.Generation;

/// <summary>The layouts: how a level's rooms lie and how they are joined. Measured over the game's own levels
/// (study/macro.py), no two are laid out alike: a journey with a clear end (Alpine Ridge, Ice Cavern), a ring that
/// comes back over its start (Dry Canyon), a meadow with ways off it all round (Artisans, Peace Keepers), one great
/// room with a hill or a tower in its middle (Stone Hill), a hill climbed terrace by terrace (Magic Crafters), a
/// winding gorge (Cliff Town's river), a web of rooms with more than one way round (Misty Bog, Gnasty's World). Rooms
/// run from landings a few steps across to fields, and a level's floors lie several storeys apart.</summary>
internal static partial class SpyroPlanner
{
    /// <summary>A size of room for this level's grain: 0 a landing, 1 small, 2 a clearing, 3 big, 4 a field.</summary>
    private static int Cls(LevelRandom rnd)
    {
        int cls = _grain switch
        {
            "grand" => rnd.Pick<int>([(1, 1.0), (2, 2.5), (3, 3.0), (4, 1.2)]),
            "fine" => rnd.Pick<int>([(0, 2.2), (1, 4.0), (2, 2.2), (3, 0.5)]),
            _ => rnd.Pick<int>([(0, 1.0), (1, 2.2), (2, 3.0), (3, 2.0), (4, 0.5)]),
        };
        // A homeworld's rooms hold doorways: most are clearings at least.
        if (_minRoom > 0 && cls < 2 && rnd.Chance(0.75)) cls = 2;
        return cls;
    }

    private static double Rad(LevelRandom rnd, int cls) => Math.Max(cls >= 2 ? _minRoom : 0,
        _scale * (cls switch { 0 => Range(rnd, 300, 370), 1 => Range(rnd, 400, 520), 2 => Range(rnd, 540, 690), 3 => Range(rnd, 710, 870), _ => Range(rnd, 940, 1150) }));

    /// <summary>How many rooms a level of this size and grain has: a few big ones, or many small.</summary>
    private static int Rooms(int weight, LevelRandom rnd, int min, int max)
    {
        double grain = _grain switch { "grand" => 0.68, "fine" => 1.5, _ => 1.0 };
        int count = Math.Clamp((int)Math.Round(weight * grain * Range(rnd, 0.8, 1.3)), min, max);
        // A homeworld: the plaza and a room for each doorway at least.
        return _minRoom > 0 ? Math.Max(count, 6) : count;
    }

    private static double Covered(PlannedLevel L) => L.Areas.Sum(a => 3.1416 * a.R * a.R);
    /// <summary>Whether the level has floor to spare for another room of this size (a homeworld always has for its
    /// first six).</summary>
    private static bool Spare(PlannedLevel L, double r) => Covered(L) + 3.1416 * r * r <= _land || _minRoom > 0 && L.Areas.Count < 6;

    /// <summary>A way from room to room with a clear end: short ways through gaps, long winding climbs, rooms of every
    /// size, side rooms off it, and at the end (where the land lets it) a high place over a room passed long before.</summary>
    private static bool Journey(PlannedLevel L, int weight, LevelRandom rnd)
    {
        int count = Rooms(weight, rnd, 4, 12);
        double heading = Range(rnd, 0, 6.28), curl = rnd.Chance(0.5) ? 1 : -1, bend = Range(rnd, 0.45, 1.15);
        // Most journeys climb to their end; some wander up and down, some go down into the land.
        double up = rnd.Pick<double>([(0.88, 5.0), (0.5, 2.0), (0.15, 1.5)]);
        Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, Rad(rnd, Math.Clamp(Cls(rnd), 1, 3))), 0, "start");
        for (int k = 1; k < count; k++)
        {
            bool made = false, last = k == count - 1;
            for (int attempt = 0; attempt < 40 && !made; attempt++)
            {
                var p = L.Areas[^1];
                double a = heading + curl * Range(rnd, -0.35, bend) - (rnd.Chance(0.1) ? curl * 1.1 : 0);
                var d = Dir(a);
                // The journey ends somewhere worth reaching.
                double r = Rad(rnd, last ? Math.Max(2, Cls(rnd)) : Cls(rnd));
                if (!Spare(L, r)) { if (k >= 3) return Finish(); r = Rad(rnd, 0); }
                // A short way through a gap, the usual way, or a long winding one that climbs a good deal.
                double roll = rnd.NextDouble();
                double gap = roll < 0.22 ? Range(rnd, 0.72 * _gapFrom, _gapFrom) : roll < 0.74 ? Gap(rnd) : Range(rnd, _gapTo, 1.75 * _gapTo);
                // Or no way between at all: the next room stands right against this one, a flight of steps up or down.
                if (rnd.Chance(0.3) && Flight(L, p, d, r, rnd.Chance(up), rnd) != null) { heading = a; made = true; continue; }
                double x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
                if (!Fits(L, p, x, y, r)) continue;
                var room = Room(L, x, y, r, 0);
                double climb = Climb(gap, gap > _gapTo ? 290 : 150);
                room.Z = p.Z + (rnd.Chance(up) ? climb * Range(rnd, 0.65, 1) : -climb * Range(rnd, 0.3, 0.9));
                Join(L, p, room, "path");
                heading = a; made = true;
            }
            if (!made) break;
        }
        return Finish();

        bool Finish()
        {
            if (L.Areas.Count < 3) return false;
            var end = L.Areas[^1];
            Spurs(L, rnd, rnd.Next(0, 2 + L.Areas.Count / 4));
            OverlookBack(L, end, rnd);
            return true;
        }
    }

    /// <summary>A room right against another, two or three steps higher or lower: the two floors meet in a flight of
    /// steps on the lower one. Null when it does not fit there.</summary>
    private static PlannedArea? Flight(PlannedLevel L, PlannedArea p, (double X, double Y) d, double r, bool upward, LevelRandom rnd)
    {
        const double overlap = 150;
        // The steps stand on the lower room's floor, which must hold them.
        var lowR = upward ? p.R : r;
        if (lowR < 400 || (upward ? r : p.R) < 330) return null;
        int steps = lowR >= 540 && rnd.Chance(0.6) ? 3 : 2;
        // Spyro arrives in the middle of the first room: the steps keep well off it.
        if (upward && p.Id == 0 && p.R - overlap - (steps == 3 ? 290 : 160) < 280) { if (p.R - overlap - 160 < 280) return null; steps = 2; }
        double x = p.X + d.X * (p.R + r - overlap), y = p.Y + d.Y * (p.R + r - overlap);
        if (!Fits(L, p, x, y, r, p.Group, path: false)) return null;
        // Not out of the room right beside another of its ways.
        if (p.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.3)) return null;
        var room = Room(L, x, y, r, p.Z + (upward ? 1 : -1) * steps * SpyroMoves.DesignLedge);
        Join(L, p, room, "flight");
        Unite(L, p, room);
        return room;
    }

    /// <summary>A winding chain of rooms, curling one way: the plain layout that always fits.</summary>
    private static bool Trail(PlannedLevel L, int count, LevelRandom rnd)
    {
        double heading = Range(rnd, 0, 6.28), curl = rnd.Chance(0.5) ? 1 : -1;
        Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, _scale * Range(rnd, 560, 660)), 0, "start");
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
                if (!Fits(L, p, x, y, r)) continue;
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

    /// <summary>Side rooms off the way: dead ends up or down a path of their own, where treasure waits.</summary>
    private static void Spurs(PlannedLevel L, LevelRandom rnd, int want)
    {
        for (int n = 0, tries = 0; n < want && tries < 12 * want; tries++)
        {
            var p = L.Areas[rnd.Next(L.Areas.Count)];
            if (p.Taken.Count > 2 || p.Kind == "overlook" || FreeDirection(p, rnd, 0.2) is not { } d) continue;
            double r = Rad(rnd, rnd.Chance(0.6) ? 1 : rnd.Chance(0.5) ? 0 : 2);
            if (!Spare(L, r)) return;
            double gap = Range(rnd, 0.8 * _gapFrom, _gapTo), x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
            if (!Fits(L, p, x, y, r)) continue;
            var room = Room(L, x, y, r, 0);
            double climb = Climb(gap, 150);
            room.Z = p.Z + (rnd.Chance(0.65) ? climb * Range(rnd, 0.5, 1) : -climb * Range(rnd, 0.4, 0.9));
            Join(L, p, room, "path");
            n++;
        }
    }

    /// <summary>The way's end comes out high over a room passed long before: a short path on from the last room to a
    /// ledge that stands over the earlier room's floor, so the journey ends with a drop or a glide back.</summary>
    private static bool OverlookBack(PlannedLevel L, PlannedArea end, LevelRandom rnd)
    {
        if (_minRoom > 0) return false;
        foreach (var e in L.Areas.Where(e => e.Id < end.Id - 1 && e.Group < 0 && e.Kind != "overlook").OrderBy(e => Dist(e.X, e.Y, end.X, end.Y)))
        {
            double dx = end.X - e.X, dy = end.Y - e.Y, d = Math.Sqrt(dx * dx + dy * dy), r = Rad(rnd, rnd.Chance(0.5) ? 0 : 1);
            const double overlap = 150;
            double x = e.X + dx / d * (e.R + r - overlap), y = e.Y + dy / d * (e.R + r - overlap);
            double gap = Dist(end.X, end.Y, x, y) - end.R - r;
            if (gap < 0.7 * _gapFrom || gap > 1.75 * _gapTo) continue;
            // High enough over the earlier room that the way down is a drop, by a ramp Spyro can walk.
            double climb = Climb(gap, 290), z = Math.Max(end.Z - 0.5 * climb, e.Z + 130);
            if (z > end.Z + climb || z - e.Z > 430) continue;
            bool clear = true;
            foreach (var o in L.Areas)
            {
                if (o == e || o == end) continue;
                if (Dist(o.X, o.Y, x, y) < o.R + r + _ridge + 40 || SegmentDistance(o.X, o.Y, end.X, end.Y, x, y) < o.R + Math.Min(_ridge, PathClear) + 60) clear = false;
            }
            // The way to it must not run through the earlier room either.
            if (!clear || SegmentDistance(e.X, e.Y, end.X, end.Y, x, y) < e.R - overlap - 10) continue;
            foreach (var j in L.Joins)
            {
                if (j.Kind is not ("path" or "charge") || j.A == end.Id || j.B == end.Id) continue;
                var p = L.Areas[j.A]; var q = L.Areas[j.B];
                // Paths into the earlier room come in on their own sides: the ledge keeps off them.
                if (SegmentDistance(x, y, p.X, p.Y, q.X, q.Y) < r + (j.A == e.Id || j.B == e.Id ? 260 : Math.Min(_ridge, PathClear) + 40)) clear = false;
            }
            if (!clear) continue;
            var over = Room(L, x, y, r, z, "overlook");
            Join(L, end, over, "path");
            Join(L, over, e, "overlook");
            Unite(L, over, e);
            return true;
        }
        return false;
    }

    /// <summary>Rooms round a ring, each a little higher than the last; the last one stands over the first, so the way
    /// round ends with a drop or a glide back to the start.</summary>
    private static bool Loop(PlannedLevel L, int weight, LevelRandom rnd)
    {
        int count = Rooms(weight, rnd, 4, 9);
        var radii = new double[count]; var gaps = new double[count];
        double covered = 0;
        for (int k = 0; k < count; k++)
        {
            radii[k] = k == 0 ? Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, Rad(rnd, rnd.Chance(0.5) ? 2 : 3)) : Rad(rnd, Cls(rnd));
            // Round a ring some ways are long: they climb more.
            gaps[k] = rnd.Chance(0.2) ? Range(rnd, _gapTo, 1.5 * _gapTo) : Gap(rnd);
            covered += 3.1416 * radii[k] * radii[k];
            if (covered > _land && k >= 3 && _minRoom <= 0) { count = k + 1; break; }
        }
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
            z += Climb(length - 240, length - 240 > _gapTo ? 240 : 130) * Range(rnd, 0.45, 1);
            a.Z = z;
            Join(L, p, a, "path");
        }
        // The last room is moved to stand over the first.
        var first = L.Areas[0]; var last = L.Areas[^1];
        // Too low to be an overlook: the ring is tried again (its ramps may not be made steeper).
        if (last.Z < 110) return false;
        if (last.Z > 460) { double drop = 440 / last.Z; foreach (var a in L.Areas) a.Z *= drop; }
        double dx = last.X - first.X, dy = last.Y - first.Y, dd = Math.Sqrt(dx * dx + dy * dy), want = first.R + last.R - overlap;
        last.X = first.X + dx / dd * want; last.Y = first.Y + dy / dd * want;
        last.Kind = "overlook"; last.Priority = first.Priority + 1;
        Join(L, last, first, "overlook");
        Unite(L, last, first);
        if (count >= 5 && _minRoom <= 0) Spurs(L, rnd, rnd.Next(0, 3));
        return true;
    }

    /// <summary>A big clearing with rooms off it all round, some of them leading further. <paramref name="arena"/>: the
    /// clearing is a field with a hill, a mound or a lake in its middle (Spyro starts at its edge).</summary>
    private static bool Hub(PlannedLevel L, int weight, LevelRandom rnd, bool arena)
    {
        int count = Rooms(weight, rnd, arena ? 3 : 4, 10);
        double radius = arena ? Math.Max(_scale, 0.9) * Range(rnd, 960, 1180) : Math.Max(Rad(rnd, rnd.Chance(0.55) ? 3 : 4), _scale * Range(rnd, 760, 920));
        var hub = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, radius), 0, "start");
        if (arena) hub.Want = rnd.Pick<string>([("mount", 5.0), ("mound", 2.5), ("lake", 2.0)]);
        int spokes = Math.Clamp(count - 1 - (count > 5 ? rnd.Next(1, 3) : count > 4 ? rnd.Next(0, 2) : 0), 2, _minRoom > 0 ? 5 : 6);
        double angle = Range(rnd, 0, 6.28);
        for (int k = 0; k < spokes; k++)
        {
            var d = Dir(angle + 2 * 3.14159265358979 * (k + Range(rnd, -0.22, 0.22)) / spokes);
            double r = Rad(rnd, Cls(rnd));
            if (!Spare(L, r)) { if (k >= 2) break; r = Rad(rnd, 1); }
            // A room right against the middle one, up a flight of steps.
            if (rnd.Chance(arena ? 0.15 : 0.28) && Flight(L, hub, d, r, rnd.Chance(0.75), rnd) != null) continue;
            double gap = rnd.Chance(0.2) ? Range(rnd, _gapTo, 1.5 * _gapTo) : Gap(rnd);
            var a = Room(L, d.X * (hub.R + r + gap), d.Y * (hub.R + r + gap), r, 0);
            double climb = Climb(gap, gap > _gapTo ? 240 : 130);
            a.Z = rnd.Chance(0.7) ? climb * Range(rnd, 0.4, 1) : -climb * Range(rnd, 0.3, 0.8);
            Join(L, hub, a, "path");
        }
        Further(L, hub, count, rnd);
        return L.Areas.Count >= 3;
    }

    /// <summary>Further rooms lead on from the ones round the middle.</summary>
    private static void Further(PlannedLevel L, PlannedArea hub, int count, LevelRandom rnd)
    {
        for (int k = L.Areas.Count; k < count; k++)
        {
            bool made = false;
            for (int attempt = 0; attempt < 30 && !made; attempt++)
            {
                var p = L.Areas[rnd.Next(1, L.Areas.Count)];
                if (p == hub || p.Taken.Count > 2) continue;
                double px = p.X - hub.X, py = p.Y - hub.Y, pd = Math.Sqrt(px * px + py * py), s = Range(rnd, -0.75, 0.75), c = Math.Sqrt(1 - s * s);
                (double X, double Y) d = (px / pd * c - py / pd * s, px / pd * s + py / pd * c);
                double r = Rad(rnd, Cls(rnd));
                if (!Spare(L, r)) return;
                double gap = Gap(rnd);
                double x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
                if (!Fits(L, p, x, y, r)) continue;
                var a = Room(L, x, y, r, 0);
                double climb = Climb(gap);
                a.Z = p.Z + (rnd.Chance(0.75) ? climb * Range(rnd, 0.5, 1) : -climb * Range(rnd, 0.3, 0.8));
                Join(L, p, a, "path");
                made = true;
            }
        }
    }

    /// <summary>The level is a hill: terraces one over the other with a ramp winding up round them to the top, where
    /// the best of the treasure is and the glide back down begins. Spyro starts at its foot, in a room of his own or on
    /// the hill's lowest ground, and a few rooms lie off the foot.</summary>
    private static bool Climb(PlannedLevel L, int weight, LevelRandom rnd)
    {
        double radius = Math.Max(_scale, 0.92) * Range(rnd, 1040, 1220);
        PlannedArea hill;
        if (rnd.Chance(0.55))
        {
            var first = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, Rad(rnd, rnd.Chance(0.5) ? 1 : 2)), 0, "start");
            var d = Dir(Range(rnd, 0, 6.28));
            double gap = Gap(rnd);
            hill = Room(L, d.X * (first.R + radius + gap), d.Y * (first.R + radius + gap), radius, Climb(gap) * Range(rnd, 0.3, 1));
            Join(L, first, hill, "path");
        }
        else hill = Room(L, 0, 0, radius, 0, "start");
        hill.Want = "mount+";
        // Rooms off the foot of the hill, some leading further.
        int count = Math.Max(L.Areas.Count + 1, Rooms(weight, rnd, 3, 7) - 1);
        int spokes = Math.Clamp(count - L.Areas.Count - (count > 5 ? 1 : 0), 1, 4);
        double angle = Range(rnd, 0, 6.28);
        for (int k = 0, tries = 0; k < spokes && tries < 40; tries++)
        {
            var d = Dir(angle + 2 * 3.14159265358979 * (k + Range(rnd, -0.25, 0.25)) / Math.Max(3, spokes));
            double r = Rad(rnd, Cls(rnd));
            if (!Spare(L, r)) { if (k >= 1) break; r = Rad(rnd, 1); }
            double gap = Gap(rnd), x = hill.X + d.X * (hill.R + r + gap), y = hill.Y + d.Y * (hill.R + r + gap);
            if (!Fits(L, hill, x, y, r)) { angle += 0.5; continue; }
            var a = Room(L, x, y, r, 0);
            double climb = Climb(gap);
            a.Z = hill.Z + (rnd.Chance(0.5) ? climb * Range(rnd, 0.4, 1) : -climb * Range(rnd, 0.3, 0.8));
            Join(L, hill, a, "path");
            k++;
        }
        Further(L, hill, count, rnd);
        return L.Areas.Count >= 2;
    }

    /// <summary>A winding gorge: its floor runs on from room to room without a break (now and then up or down a step
    /// Spyro jumps), widening into a clearing here and there; where the land rises a good deal a path climbs to the
    /// gorge's next reach.</summary>
    private static bool Gorge(PlannedLevel L, int weight, LevelRandom rnd)
    {
        int rooms = Math.Clamp((int)Math.Round(weight * Range(rnd, 1.25, 1.9)), 5, 14), reaches = rooms >= 9 ? rnd.Next(2, 4) : rnd.Next(1, 3);
        double heading = Range(rnd, 0, 6.28), drift = Range(rnd, -0.22, 0.22);
        double up = rnd.Chance(0.75) ? 0.8 : 0.25;
        PlannedArea? prev = null;
        int made = 0, group = 0;
        for (int reach = 0; reach < reaches; reach++)
        {
            int n = Math.Max(2, (rooms - made) / (reaches - reach) + (reach + 1 < reaches ? rnd.Next(-1, 2) : 0));
            for (int i = 0; i < n; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 30 && !placed; attempt++)
                {
                    // Narrow for the most part; now and then it opens out.
                    int cls = prev == null ? rnd.Next(1, 3) : rnd.Pick<int>([(0, 3.0), (1, 4.0), (2, 2.0), (3, 0.7)]);
                    double r = Math.Max(cls >= 2 ? _minRoom : 0, _scale * (cls switch { 0 => Range(rnd, 310, 380), 1 => Range(rnd, 400, 500), 2 => Range(rnd, 540, 660), _ => Range(rnd, 700, 820) }));
                    if (prev == null) { prev = Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, r), 0, "start"); prev.Group = 0; placed = true; break; }
                    if (!Spare(L, r) && made >= 5) return L.Areas.Count >= 4;
                    if (i == 0)
                    {
                        // The next reach: up (or down) a path from the last.
                        double a = heading + Range(rnd, -0.7, 0.7), gap = rnd.Chance(0.4) ? Range(rnd, _gapTo, 1.6 * _gapTo) : Gap(rnd);
                        var d = Dir(a);
                        if (rnd.Chance(0.35) && Flight(L, prev, d, r, rnd.Chance(up), rnd) is { } above)
                        {
                            // Still the one gorge, a storey up or down.
                            group = above.Group; prev = above; heading = a; placed = true;
                            continue;
                        }
                        double x = prev.X + d.X * (prev.R + r + gap), y = prev.Y + d.Y * (prev.R + r + gap);
                        if (!Fits(L, prev, x, y, r)) continue;
                        double climb = Climb(gap, gap > _gapTo ? 290 : 150);
                        var room = Room(L, x, y, r, prev.Z + (rnd.Chance(up) ? climb * Range(rnd, 0.6, 1) : -climb * Range(rnd, 0.4, 0.9)));
                        group = reach;
                        room.Group = group;
                        Join(L, prev, room, "path");
                        prev = room; heading = a; placed = true;
                    }
                    else
                    {
                        // The same floor running on: the rooms overlap by a good part of the smaller one.
                        double a = heading + drift + Range(rnd, -0.5, 0.5);
                        var d = Dir(a);
                        double reachOut = prev.R + r - Math.Min(prev.R, r) * Range(rnd, 0.5, 0.8);
                        double x = prev.X + d.X * reachOut, y = prev.Y + d.Y * reachOut;
                        if (!Fits(L, prev, x, y, r, group, path: false)) continue;
                        // Now and then a step up or down that Spyro jumps.
                        bool step = rnd.Chance(0.24) && prev.Id != 0;
                        double z = prev.Z + (step ? (rnd.Chance(up) ? 1 : -1) * SpyroMoves.DesignLedge : 0);
                        var room = Room(L, x, y, r, z);
                        room.Group = group;
                        Join(L, prev, room, step ? "step" : "open");
                        prev = room; heading = a; placed = true;
                    }
                }
                if (!placed) return L.Areas.Count >= 4;
                made++;
            }
        }
        Spurs(L, rnd, rnd.Next(0, 3));
        return L.Areas.Count >= 4;
    }

    /// <summary>A web: rooms grown one off another in a cluster, then joined across wherever two lie a path's length
    /// apart at heights a path can join, so there is more than one way round.</summary>
    private static bool Web(PlannedLevel L, int weight, LevelRandom rnd)
    {
        int count = Rooms(weight, rnd, 5, 12);
        Room(L, 0, 0, Math.Max(_minRoom > 0 ? _minRoom + 60 : 0, Rad(rnd, Math.Clamp(Cls(rnd), 2, 3))), 0, "start");
        for (int k = 1, tries = 0; k < count && tries < 60 * count; tries++)
        {
            // Off a room chosen from the newer ones as a rule, so the web spreads; a room has four ways out at most.
            var p = L.Areas[Math.Max(0, L.Areas.Count - 1 - rnd.Next(Math.Min(L.Areas.Count, 4)))];
            if (p.Taken.Count >= 4) continue;
            var d = Dir(Range(rnd, 0, 6.2832));
            if (p.Taken.Any(t => t.X * d.X + t.Y * d.Y > 0.5)) continue;
            double r = Rad(rnd, Cls(rnd));
            if (!Spare(L, r)) { if (k >= 5) break; r = Rad(rnd, 0); }
            double gap = Gap(rnd), x = p.X + d.X * (p.R + r + gap), y = p.Y + d.Y * (p.R + r + gap);
            if (!Fits(L, p, x, y, r)) continue;
            var a = Room(L, x, y, r, 0);
            double climb = Climb(gap);
            a.Z = p.Z + (rnd.Chance(0.6) ? climb * Range(rnd, 0.3, 1) : -climb * Range(rnd, 0.3, 0.9));
            Join(L, p, a, "path");
            k++;
        }
        if (L.Areas.Count < 4) return false;
        // Ways across: the nearest pairs first.
        var joined = new HashSet<(int, int)>();
        foreach (var j in L.Joins) { joined.Add((j.A, j.B)); joined.Add((j.B, j.A)); }
        var pairs = new List<(double Gap, int A, int B)>();
        for (int i = 0; i < L.Areas.Count; i++)
            for (int k = i + 1; k < L.Areas.Count; k++)
            {
                var a = L.Areas[i]; var b = L.Areas[k];
                double gap = Dist(a.X, a.Y, b.X, b.Y) - a.R - b.R;
                if (!joined.Contains((i, k)) && gap >= _gapFrom && gap <= 1.6 * _gapTo && Math.Abs(a.Z - b.Z) <= Climb(gap, 290)) pairs.Add((gap, i, k));
            }
        int extra = 0, most = Math.Max(1, L.Areas.Count / 3);
        foreach (var (gap, i, k) in pairs.OrderBy(q => q.Gap).ThenBy(q => q.A).ThenBy(q => q.B))
        {
            if (extra >= most) break;
            var a = L.Areas[i]; var b = L.Areas[k];
            if (a.Taken.Count >= 4 || b.Taken.Count >= 4) continue;
            double dx = (b.X - a.X), dy = (b.Y - a.Y), dl = Math.Sqrt(dx * dx + dy * dy);
            // Not out of a room right beside another of its ways.
            if (a.Taken.Any(t => t.X * dx / dl + t.Y * dy / dl > 0.55) || b.Taken.Any(t => -t.X * dx / dl - t.Y * dy / dl > 0.55)) continue;
            L.Joins.Add(new(i, k, "path"));
            if (Clear(L)) { a.Taken.Add((dx / dl, dy / dl)); b.Taken.Add((-dx / dl, -dy / dl)); extra++; }
            else L.Joins.RemoveAt(L.Joins.Count - 1);
        }
        return true;
    }
}
