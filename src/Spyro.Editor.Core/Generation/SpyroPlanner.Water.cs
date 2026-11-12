namespace Spyro.Editor.Core.Generation;

/// <summary>Water on the land, the way the game's levels have it: a river (or a stream of ooze or lava) running across
/// a big room from bank to bank, a little below the floor, with a wooden bridge over it (Artisans, Cliff Town, Gnorc
/// Cove); and a marsh at a room's side with a boardwalk out to an islet (Beast Makers, Misty Bog).</summary>
internal static partial class SpyroPlanner
{
    /// <summary>Rivers made in the level so far.</summary>
    [ThreadStatic] private static int _rivers;

    /// <summary>A river across a big room: it runs between the room's ways in and out, from out in the bank on one side
    /// to the other, and Spyro crosses it by a bridge where it passes nearest the room's middle (a long one has two).</summary>
    private static bool River(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        var plan = L.Plan;
        if (a.R < 480 || plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky || a.Zone.Roof > 0) return false;
        var at = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
        // Parts on the same level that this room runs on into: the river may flow on across them (one floor).
        var level = L.Joins.Where(j => j.Kind == "open" && (j.A == a.Id || j.B == a.Id)).Select(j => j.A == a.Id ? j.B : j.A).Where(o => Math.Abs(L.Areas[o].Z - a.Z) < 1).ToHashSet();
        // A stream in a smaller room, a river in a big one: too wide to jump either way.
        double half = Math.Clamp(a.R * Range(rnd, 0.12, 0.16), 76, 122), beyond = Math.Min(150, plan.TrimDistance - 80);
        int zones = plan.Zones.Count;
        // What already stands that the river keeps off: everything but the room's own paths (where a path comes into
        // the room the river crosses it under a bridge, short of its mouth: see below).
        var own = Enumerable.Range(0, zones).Where(k => plan.Zones[k] is { IsPath: true, Kind: "path" or "charge" } z && (RoomGap(a, z.Spine[0].X, z.Spine[0].Y) < 0 || RoomGap(a, z.Spine[^1].X, z.Spine[^1].Y) < 0)).ToHashSet();
        bool Wet(double x, double y, double r)
        {
            for (int k = 0; k < zones; k++)
            {
                var z = plan.Zones[k];
                if (z.Kind == "room" || own.Contains(k)) continue;
                if (z.IsPath) { if (z.SpineDistance(x, y, out _) < z.HalfWidth + r) return true; continue; }
                foreach (var d in z.Discs) if (Dist(d.X, d.Y, x, y) < d.R + r) return true;
                foreach (var b in z.Boxes) if (Dist(b.X, b.Y, x, y) < Math.Sqrt(b.HalfX * b.HalfX + b.HalfY * b.HalfY) + r) return true;
            }
            return false;
        }
        for (int tries = 0; tries < 36; tries++)
        {
            var t = Dir(Range(rnd, 0, 6.2832));
            (double X, double Y) n = (-t.Y, t.X);
            // To one side of the room's middle (which stays dry), bending a little.
            double bow = Range(rnd, -0.12, 0.12) * a.R, least = half + 150 + Math.Abs(bow);
            if (least > 0.62 * a.R) continue;
            double off = (rnd.Chance(0.5) ? 1 : -1) * Range(rnd, least, 0.62 * a.R);
            var spine = new List<(double X, double Y)>();
            bool ok = true;
            for (int q = 0; q <= 14 && ok; q++)
            {
                double s = -1.35 * a.R + 2.7 * a.R * q / 14, side = off + bow * Sin(3.14159265358979 * q / 14);
                double x = a.X + t.X * s + n.X * side, y = a.Y + t.Y * s + n.Y * side;
                double gap = RoomGap(a, x, y);
                // Well out in the bank the river has gone under it; where it meets a part at another height it comes
                // out from under that part's wall (or runs over its edge).
                bool gone = gap > beyond || L.Areas.Any(o => o != a && !level.Contains(o.Id) && RoomGap(o, x, y) < half + 70);
                if (gone) { if (spine.Count > 0) break; continue; }
                spine.Add((x, y));
                // Off what already stands in the room (and the paths' ends in it), the middle and where Spyro arrives,
                // the doorways' floor (the shore dips to the water); out past the edge, clear of every other room and
                // path.
                if (Wet(x, y, half + 90)) ok = false;
                if (Dist(x, y, a.X, a.Y) < half + 140 || Dist(x, y, at.X, at.Y) < half + 380) ok = false;
                if (!DoorsDry(L, x, y, half + 260)) ok = false;
                foreach (var j in L.Joins)
                    if (j.Kind is "path" or "charge" && j.A != a.Id && j.B != a.Id &&
                        SegmentDistance(x, y, L.Areas[j.A].X, L.Areas[j.A].Y, L.Areas[j.B].X, L.Areas[j.B].Y) < half + 300) ok = false;
            }
            if (!ok || spine.Count < 7) continue;
            // Every way into the room (from its middle to the room it is joined to) that the river crosses gets a
            // bridge laid along it; where the way crosses near its end (a path's mouth, the steps up to the next
            // room), too steeply slanting or twice, the river is tried elsewhere.
            var bridges = new List<((double X, double Y) At, (double X, double Y) Along, double Reach)>();
            foreach (var j in L.Joins)
            {
                if (!ok) break;
                if (j.A != a.Id && j.B != a.Id) continue;
                var o = L.Areas[j.A == a.Id ? j.B : j.A];
                double length = Dist(a.X, a.Y, o.X, o.Y);
                var u = Unit(o.X - a.X, o.Y - a.Y);
                // How far along the way it may cross: well inside the room's own floor.
                double limit = j.Kind switch
                {
                    "path" or "charge" => a.R - 160 - half - 80,
                    "open" => length,
                    "flight" => length - o.R - FlightReach((int)Math.Round(Math.Abs(o.Z - a.Z) / SpyroMoves.DesignLedge)) - half - 90,
                    _ => length - o.R - half - 150,
                };
                var hits = new List<(double S, (double X, double Y) P, (double X, double Y) Dir)>();
                for (int k = 0; k + 1 < spine.Count; k++)
                    if (Cross(a.X, a.Y, o.X, o.Y, spine[k], spine[k + 1]) is { } hit) hits.Add(hit);
                // Near the way without crossing it is as bad: it would cut the way's side.
                bool near = Enumerable.Range(0, spine.Count).Any(k => SegmentDistance(spine[k].X, spine[k].Y, a.X, a.Y, a.X + u.X * Math.Min(limit + half + 60, length), a.Y + u.Y * Math.Min(limit + half + 60, length)) < half + 30);
                if (hits.Count == 0) { if (near && j.Kind != "open") ok = false; continue; }
                if (hits.Count > 1 || hits[0].S > limit) { ok = false; break; }
                double sin = Math.Abs(u.X * hits[0].Dir.Y - u.Y * hits[0].Dir.X);
                if (sin < 0.6) { ok = false; break; }
                bridges.Add((hits[0].P, u, half / sin + 130));
            }
            if (!ok) continue;
            var river = plan.Add(new TerrainZone
            {
                Kind = "pond", Z = a.Z - 30, Z1 = a.Z - 30, Area = a.Id, Priority = a.Priority + 6, HalfWidth = half, Wobble = 16, WobbleScale = 280,
                Crisp = true, Flat = true, Floor = plan.LiquidRole, Liquid = plan.LiquidKind,
            });
            river.Spine.AddRange(spine);
            // A way across where the river passes nearest the middle too, when no way into the room crosses it (to its
            // far bank) or the room is big.
            int mid = Enumerable.Range(0, spine.Count).MinBy(i => Dist(spine[i].X, spine[i].Y, a.X, a.Y));
            var pm = spine[mid];
            var tm = Unit(spine[Math.Min(mid + 1, spine.Count - 1)].X - spine[Math.Max(mid - 1, 0)].X, spine[Math.Min(mid + 1, spine.Count - 1)].Y - spine[Math.Max(mid - 1, 0)].Y);
            if (bridges.Count == 0 || a.R >= 850 && bridges.All(b => Dist(b.At.X, b.At.Y, pm.X, pm.Y) > 500))
                bridges.Add((pm, (-tm.Y, tm.X), half + 130));
            foreach (var (p, m, reach) in bridges)
            {
                // Its ends well out on the banks, past where the shore dips to the water.
                var bridge = plan.Add(new TerrainZone
                {
                    Kind = "bridge", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 7, HalfWidth = Range(rnd, 100, 118), Wobble = 0, Crisp = true, Flat = true,
                    Floor = SurfaceRole.Wood,
                });
                bridge.Spine.Add((p.X - m.X * reach, p.Y - m.Y * reach)); bridge.Spine.Add((p.X + m.X * reach, p.Y + m.Y * reach));
                L.Links.Add(new LevelLink(a.Id, a.Id, "bridge", P(p.X - m.X * (reach - 20), p.Y - m.Y * (reach - 20), a.Z), P(p.X + m.X * (reach - 20), p.Y + m.Y * (reach - 20), a.Z)));
                L.Gems.Add((p.X, p.Y, bridge.Id));
            }
            // Gems along the banks.
            for (int k = 2; k < spine.Count - 2; k += 3)
            {
                var p = spine[k];
                double side = rnd.Chance(0.5) ? 1 : -1, x = p.X + n.X * side * (half + 120), y = p.Y + n.Y * side * (half + 120);
                if (RoomGap(a, x, y) < -80 && plan.ZoneAt(x, y) == a.Zone.Id) L.Gems.Add((x, y, a.Zone.Id));
            }
            // (What comes after keeps off the river by its own zone, not by a way taken from the room.)
            a.Piece = a.Piece == "" ? "river" : a.Piece;
            L.Count("river with a bridge");
            return true;
        }
        return false;
    }

    /// <summary>Whether a point is at least <paramref name="clear"/> from the floor in front of and behind every doorway.</summary>
    private static bool DoorsDry(PlannedLevel L, double x, double y, double clear) =>
        L.Doors.All(d => SegmentDistance(x, y, d.X + d.Dx * d.Behind, d.Y + d.Dy * d.Behind, d.X - d.Dx * d.Front, d.Y - d.Dy * d.Front) >= clear);

    /// <summary>Where the line from (ax, ay) to (bx, by) crosses a stretch of a river's middle line: how far along the
    /// line, the point, and the river's way there; null when it does not.</summary>
    private static (double S, (double X, double Y) P, (double X, double Y) Dir)? Cross(double ax, double ay, double bx, double by, (double X, double Y) p, (double X, double Y) q)
    {
        double rx = bx - ax, ry = by - ay, sx = q.X - p.X, sy = q.Y - p.Y, d = rx * sy - ry * sx;
        if (Math.Abs(d) < 1e-9) return null;
        double t = ((p.X - ax) * sy - (p.Y - ay) * sx) / d, u = ((p.X - ax) * ry - (p.Y - ay) * rx) / d;
        if (t < 0 || t > 1 || u < 0 || u > 1) return null;
        double length = Math.Sqrt(rx * rx + ry * ry);
        return (t * length, (ax + rx * t, ay + ry * t), Unit(sx, sy));
    }

    /// <summary>Whether a point is within <paramref name="clear"/> of a way into room <paramref name="a"/> (the line from
    /// its middle to the middle of a room it is joined to).</summary>
    internal static bool OnWay(PlannedLevel L, PlannedArea a, double x, double y, double clear)
    {
        foreach (var j in L.Joins)
        {
            if (j.A != a.Id && j.B != a.Id) continue;
            var o = L.Areas[j.A == a.Id ? j.B : j.A];
            if (SegmentDistance(x, y, a.X, a.Y, o.X, o.Y) < clear) return true;
        }
        return false;
    }

    /// <summary>Whether a point is at least <paramref name="clear"/> from every way into a room: the line from its
    /// middle to the middle of each room it is joined to (Spyro comes in that way and walks to the middle).</summary>
    private static bool JoinsDry(PlannedLevel L, PlannedArea a, double x, double y, double clear)
    {
        foreach (var j in L.Joins)
        {
            if (j.A != a.Id && j.B != a.Id) continue;
            var o = L.Areas[j.A == a.Id ? j.B : j.A];
            if (SegmentDistance(x, y, a.X, a.Y, o.X, o.Y) < clear) return false;
        }
        return Dist(x, y, a.X, a.Y) >= clear;
    }

    /// <summary>A marsh at a room's side: a wide pool of the level's water, ooze or lava with an islet in it, and a
    /// boardwalk out to the islet (or, on a hard level, stepping stones), with treasure there.</summary>
    private static bool Marsh(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        var plan = L.Plan;
        if (a.R < 540 || plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky) return false;
        if (FreeDirection(a, rnd, 0.5) is not { } d) return false;
        // Out at the room's side, its far shore running into the bank like a lake against a hillside (where the land
        // ends in a bank; in a canyon or castle it stays on the floor).
        double beyond = Math.Min(220, plan.TrimDistance - 90);
        double r = Math.Clamp(a.R * Range(rnd, 0.28, 0.38), 200, 380), off = a.R * (beyond > 100 ? Range(rnd, 0.55, 0.7) : Range(rnd, 0.38, 0.48));
        (double X, double Y) c = (a.X + d.X * off, a.Y + d.Y * off), side = (-d.Y, d.X);
        var at = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
        if (Dist(at.X, at.Y, c.X, c.Y) < 1.25 * r + 380) return false;
        if (!OffJoins(L, a, c.X, c.Y, Math.Max(30, 1.15 * r - 170)) || Overlaps(L, plan.Zones.Count, c.X, c.Y, 1.15 * r)) return false;
        if (L.Areas.Any(o => o != a && RoomGap(o, c.X, c.Y) < 1.2 * r + 140)) return false;
        if (!DoorsDry(L, c.X, c.Y, 1.25 * r + 260) || !JoinsDry(L, a, c.X, c.Y, 1.2 * r + 60)) return false;
        // All of it on the room's floor or just into its bank.
        for (int k = 0; k < 16; k++)
        {
            var e = Dir(6.2832 * k / 16);
            if (RoomGap(a, c.X + e.X * 1.2 * r, c.Y + e.Y * 1.2 * r) > beyond) return false;
        }
        // The near shore, where the way out starts, is the room's own floor.
        if (RoomGap(a, c.X - d.X * (r + 150), c.Y - d.Y * (r + 150)) > -60) return false;
        a.Taken.Add(d);
        var marsh = plan.Add(new TerrainZone
        {
            Kind = "pond", Z = a.Z - 28, Z1 = a.Z - 28, Area = a.Id, Priority = a.Priority + 6, Blend = 0.45 * r, Wobble = 24, WobbleScale = 220, Crisp = true, Flat = true,
            Floor = plan.LiquidRole, Liquid = plan.LiquidKind,
        });
        marsh.Discs.Add((c.X, c.Y, r));
        double s = rnd.Chance(0.5) ? 1 : -1;
        marsh.Discs.Add((c.X + side.X * s * 0.55 * r, c.Y + side.Y * s * 0.55 * r, 0.7 * r));
        marsh.Discs.Add((c.X - side.X * s * 0.4 * r + d.X * 0.3 * r, c.Y - side.Y * s * 0.4 * r + d.Y * 0.3 * r, 0.6 * r));
        // The islet towards the far side, with treasure; the way out to it from the near shore.
        double ir = Math.Max(150, 0.38 * r);
        (double X, double Y) isle = (c.X + d.X * 0.1 * r, c.Y + d.Y * 0.1 * r);
        var islet = plan.Add(new TerrainZone { Kind = "islet", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 7, Blend = 0, Wobble = 10, WobbleScale = 200, Crisp = true, Floor = a.Zone.Floor });
        islet.Discs.Add((isle.X, isle.Y, ir));
        (double X, double Y) shore = (c.X - d.X * (r + 150), c.Y - d.Y * (r + 150));
        if (Tune.Deadly && rnd.Chance(0.6))
        {
            // Stepping stones: each an easy jump from the last.
            double span = Dist(shore.X, shore.Y, isle.X, isle.Y) - ir - 150, rs = Tune.StoneRadius - 20;
            int stones = Math.Max(1, (int)Math.Ceiling((span - Tune.StoneGap) / (2 * rs + Tune.StoneGap)));
            for (int k = 0; k < stones; k++)
            {
                double f = (k + 1.0) / (stones + 1);
                double x = shore.X + (isle.X - shore.X) * f, y = shore.Y + (isle.Y - shore.Y) * f;
                var stone = plan.Add(new TerrainZone { Kind = "stone", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 7, Blend = 0, Wobble = 6, WobbleScale = 150, Flat = true, Floor = SurfaceRole.Rock });
                stone.Discs.Add((x, y, rs));
            }
            L.Links.Add(new LevelLink(a.Id, a.Id, "hop", P(shore.X, shore.Y, a.Z), P(isle.X, isle.Y, a.Z)) { JumpAt = 110 });
            L.Count("marsh with stepping stones");
        }
        else
        {
            var walk = plan.Add(new TerrainZone
            {
                Kind = "bridge", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 8, HalfWidth = Range(rnd, 84, 100), Wobble = 0, Crisp = true, Flat = true,
                Floor = SurfaceRole.Wood,
            });
            walk.Spine.Add(shore); walk.Spine.Add(isle);
            L.Links.Add(new LevelLink(a.Id, a.Id, "bridge", P(shore.X, shore.Y, a.Z), P(isle.X, isle.Y, a.Z)));
            L.Gems.Add(((shore.X + isle.X) / 2, (shore.Y + isle.Y) / 2, walk.Id));
            L.Count("marsh with a boardwalk");
        }
        for (int q = 0; q < 3; q++) { var e = Dir(Range(rnd, 0, 6.2832)); L.Gems.Add((isle.X + e.X * ir * 0.5, isle.Y + e.Y * ir * 0.5, islet.Id)); }
        return true;
    }
}
