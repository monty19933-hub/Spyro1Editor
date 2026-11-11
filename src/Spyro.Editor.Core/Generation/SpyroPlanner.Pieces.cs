using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>What stands in the rooms: the set pieces the game's own levels are built round. A hill of terraces with a
/// ramp winding up it, a stepped mound, a raised half of a room up a flight of steps, a lookout with a glide, ledges
/// round the edge, a hollow, a pool with an islet, a moat round an island.</summary>
internal static partial class SpyroPlanner
{
    private static void Furnish(PlannedLevel L, LevelStyle style, FeatureSet features, LevelRandom rnd)
    {
        bool castle = style == LevelStyle.Castle;
        // The level's own leanings: every level makes much of some things and little of others.
        var lean = new Dictionary<string, double>();
        foreach (string piece in new[] { "lookout", "mound", "mount", "pond", "ledges", "dell", "terrace" })
            lean[piece] = rnd.Pick<double>([(0.35, 1.0), (1.0, 2.0), (2.4, 1.0)]);
        foreach (var a in L.Areas)
        {
            bool door = L.Doors.Any(d => d.Area == a.Id), first = a.Id == 0;
            bool big = a.R >= 600, grand = a.R >= 780;
            // What the layout asked for in this room.
            if (a.Want is "mount" or "mount+" && Mount(L, a, rnd, a.Want == "mount+", castle) > 0) continue;
            if (a.Want is "mount" or "mount+" or "mound" && Mound(L, a, rnd, features)) { EdgeThings(a, 0.5); continue; }
            if (a.Want == "lake" && Moat(L, a, rnd)) { EdgeThings(a, 0.5); continue; }
            // One big thing to a room, by its size and the level's leanings.
            var options = new List<(string, double)> { ("none", 1.3) };
            if (features.Glides && big) options.Add(("lookout", 2.4 * Tune.LookoutChance * lean["lookout"]));
            if (big && !door && !first) options.Add(("mound", 1.2 * lean["mound"]));
            if (grand && !door && !first) options.Add(("mount", 3.0 * lean["mount"]));
            if (a.R >= 500) options.Add(("terrace", 1.6 * lean["terrace"]));
            string choice = rnd.Pick<string>(options);
            bool built = choice switch
            {
                "lookout" => Lookout(L, a, rnd) || Lookout(L, a, rnd) || Lookout(L, a, rnd),
                "mound" => Mound(L, a, rnd, features),
                "mount" => Mount(L, a, rnd, false, castle) > 0,
                "terrace" => Terrace(L, a, rnd, features) || Terrace(L, a, rnd, features),
                _ => false,
            };
            if (built && choice is "lookout" or "mount") continue;
            EdgeThings(a, built ? 0.6 : 1);
        }

        // Round a room's edge: ledges to jump up, a hollow, a pool.
        void EdgeThings(PlannedArea a, double share)
        {
            int ledges = (int)Math.Round(rnd.Next(Tune.LedgesFrom, Tune.LedgesTo) * Math.Min(1.6, lean["ledges"]) * share + (a.R >= 880 ? 1 : 0));
            if (a.R < 420) ledges = Math.Min(ledges, 1);
            for (int k = 0; k < ledges; k++) Ledges(L, a, rnd, rnd.Chance(0.4) ? 2 : 1);
            if (a.R >= 430 && rnd.Chance(0.3 * lean["dell"])) Dell(L, a, rnd);
            if (features.Hazards && a.R >= 600 && rnd.Chance(Math.Min(0.95, Tune.PoolChance * lean["pond"]))) Pond(L, a, rnd);
        }
    }

    private static TerrainZone Step(PlannedLevel L, PlannedArea a, double x, double y, double r, double z, int priority, string kind = "ledge")
    {
        var zone = L.Plan.Add(new TerrainZone { Kind = kind, Z = z, Z1 = z, Area = a.Id, Priority = priority, Blend = 0, Wobble = 14, WobbleScale = 240, Crisp = true, Floor = a.Zone.Floor });
        zone.Discs.Add((x, y, r));
        return zone;
    }

    private static Vector3f P(double x, double y, double z) => new((float)x, (float)y, (float)z);

    /// <summary>Whether a disc would lie on a floor already planned (a path's strip, the steps of a flight, a ledge, a
    /// pool): of the zones made before <paramref name="before"/>, any but the rooms (other rooms are kept apart by
    /// <see cref="Free"/>).</summary>
    /// <param name="pathR">The clearance from a path's strip, when not <paramref name="r"/>.</param>
    private static bool Overlaps(PlannedLevel L, int before, double x, double y, double r, double? pathR = null)
    {
        for (int k = 0; k < before && k < L.Plan.Zones.Count; k++)
        {
            var z = L.Plan.Zones[k];
            if (z.Kind == "room") continue;
            if (z.IsPath) { if (z.SpineDistance(x, y, out _) < z.HalfWidth + (pathR ?? r)) return true; continue; }
            foreach (var d in z.Discs) if (Dist(d.X, d.Y, x, y) < d.R + r) return true;
        }
        return false;
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
        L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", P(x - d.X * (r1 + 90), y - d.Y * (r1 + 90), a.Z),
            P(x - d.X * (r1 - 110), y - d.Y * (r1 - 110), a.Z + SpyroMoves.DesignLedge)) { JumpAt = 60 });
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

    /// <summary>A ledge to glide to from a high place in a room: at the room's edge, too high to jump onto, and as far
    /// from the top as a glide from it carries with room to spare (and no nearer than a glide can come down on).</summary>
    /// <param name="c">The high place's middle, <paramref name="rTop"/> its radius, <paramref name="top"/> its height
    /// over the room's floor.</param>
    /// <param name="d">The way from the room's middle to the high place.</param>
    /// <param name="keepOff">A disc the ledge keeps clear of (a step on the way up), or null.</param>
    private static bool GlideLedge(PlannedLevel L, PlannedArea a, TerrainZone summit, (double X, double Y) c, double rTop, double top, (double X, double Y) d,
        (double X, double Y, double R)? keepOff, LevelRandom rnd, bool round)
    {
        // A low top: a glide almost on the level. A high one: down a good way, and so further.
        double drop = top > 230 ? Range(rnd, 70, Math.Min(top - 120, 250)) : Range(rnd, 30, 42);
        double target = top - drop, tr = Range(rnd, 200, 240);
        double far = SpyroMoves.GlideReach((float)drop) - Tune.GlideSpare, near = Math.Max(170, SpyroMoves.GlideReach((float)drop, 0) - 330);
        if (far < near + 40) return false;
        double side = rnd.Chance(0.5) ? 1 : -1;
        for (int tries = 0; tries < 2; tries++, side = -side)
            foreach (double reach in round ? new[] { 1.22, 1.3, 1.12 } : [1.15, 1.0, 1.28])
                for (double angle = round ? 0.2 : 0.56; angle <= (round ? 3.1 : 1.5); angle += 0.04)
                {
                    var e = Turn(d, angle * side);
                    double x = a.X + e.X * a.R * reach, y = a.Y + e.Y * a.R * reach;
                    double gap = Dist(x, y, c.X, c.Y) - rTop - tr;
                    // Spyro may leave the top a little before its edge and must clear the ledge's edge: well inside the glide.
                    if (gap < near || gap > far) continue;
                    // Clear of the step below the top, so it is the glide from the top that reaches it.
                    if (keepOff is { } k && Dist(x, y, k.X, k.Y) < k.R + tr + 120) continue;
                    if (a.Taken.Any(t => t.X * e.X + t.Y * e.Y > 0.55) || !Free(L, a, x, y, tr) || !OffJoins(L, a, x, y, tr)) continue;
                    a.Taken.Add(e);
                    var ledge = Step(L, a, x, y, tr, a.Z + target, summit.Priority, "glide ledge");
                    L.Glides.Add((summit.Id, ledge.Id));
                    // From the top's middle towards the ledge; the jump where the top ends.
                    L.Links.Add(new LevelLink(a.Id, a.Id, "glide", P(c.X, c.Y, a.Z + top), P(x, y, a.Z + target)) { JumpAt = (float)(rTop - 45) });
                    for (int q = -1; q <= 1; q++) L.Gems.Add((x + e.Y * q * 70, y - e.X * q * 70, ledge.Id));
                    return true;
                }
        return false;
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
        // The zones are made only when the ledge has its place: tried on a copy of the room's taken ways.
        int zones = L.Plan.Zones.Count, links = L.Links.Count, gems = L.Gems.Count, glides = L.Glides.Count, taken = a.Taken.Count;
        Step(L, a, c1.X, c1.Y, r1, a.Z + step, a.Priority + 1);
        Step(L, a, c2.X, c2.Y, r2, a.Z + 2 * step, a.Priority + 2);
        var summit = Step(L, a, c3.X, c3.Y, r3, a.Z + 3 * step, a.Priority + 3, "lookout");
        if (!GlideLedge(L, a, summit, c3, r3, 3 * step, d, (c2.X, c2.Y, r2), rnd, round: false))
        {
            L.Plan.Zones.RemoveRange(zones, L.Plan.Zones.Count - zones);
            L.Links.RemoveRange(links, L.Links.Count - links); L.Gems.RemoveRange(gems, L.Gems.Count - gems);
            L.Glides.RemoveRange(glides, L.Glides.Count - glides); a.Taken.RemoveRange(taken, a.Taken.Count - taken);
            return false;
        }
        a.Taken.Add(d);
        a.Piece = "lookout";
        L.Count("lookout with a glide");
        L.Gems.Add((c3.X, c3.Y, summit.Id));
        return true;
    }

    /// <summary>A pool at the side of a room, its shore sloping in; a big one may have an islet a jump from the shore.</summary>
    private static bool Pond(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (FreeDirection(a, rnd, 0.3) is not { } d) return false;
        double r = Math.Clamp(a.R * 0.26, 150, 250), off = a.R * 0.56;
        var at = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
        if (Dist(at.X, at.Y, a.X + d.X * off, a.Y + d.Y * off) < 1.7 * r + 400) return false;
        if (!OffJoins(L, a, a.X + d.X * off, a.Y + d.Y * off, 1.4 * r - 60)) return false;
        // Not where another room of its group has its floor.
        if (L.Areas.Any(o => o != a && Together(o, a) && Dist(o.X, o.Y, a.X + d.X * off, a.Y + d.Y * off) < o.R + 1.5 * r)) return false;
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
        if (r >= 225 && rnd.Chance(0.5))
        {
            // An islet: a jump from the shore, with something on it worth the jump.
            pond.Wobble = 12;
            var islet = L.Plan.Add(new TerrainZone { Kind = "islet", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 7, Blend = 0, Wobble = 5, WobbleScale = 200, Crisp = true, Floor = SurfaceRole.Rock });
            islet.Discs.Add((a.X + d.X * off, a.Y + d.Y * off, r - 96));
            L.Gems.Add((a.X + d.X * off, a.Y + d.Y * off, islet.Id));
            L.Links.Add(new LevelLink(a.Id, a.Id, "hop", P(a.X + d.X * (off - r - 130), a.Y + d.Y * (off - r - 130), a.Z), P(a.X + d.X * off, a.Y + d.Y * off, a.Z)) { JumpAt = 110 });
            L.Count("islet in a pool");
        }
        return true;
    }

    /// <summary>A hollow in a room's floor: a step down, with something in it.</summary>
    private static bool Dell(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (FreeDirection(a, rnd, 0.3) is not { } d) return false;
        double r = Math.Clamp(a.R * Range(rnd, 0.26, 0.34), 170, 290), off = a.R * Range(rnd, 0.3, 0.55);
        double x = a.X + d.X * off, y = a.Y + d.Y * off;
        if (!Free(L, a, x, y, r) || !OffJoins(L, a, x, y, r)) return false;
        if (L.Plan.Zones.Any(z => z.Area == a.Id && z.Kind != "room" && z.Kind != "path" && z.Sdf(x, y, L.Plan.Noise) < r + 90)) return false;
        a.Taken.Add(d);
        var dell = Step(L, a, x, y, r, a.Z - SpyroMoves.DesignLedge, a.Priority + 1, "dell");
        L.Gems.Add((x, y, dell.Id));
        L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", P(x + d.X * (r - 90), y + d.Y * (r - 90), a.Z - SpyroMoves.DesignLedge), P(x + d.X * (r + 110), y + d.Y * (r + 110), a.Z)) { JumpAt = 60 });
        L.Count("hollow");
        return true;
    }

    /// <summary>A raised part of a room: a wide floor two or three steps up, with its flight of steps facing the
    /// room's middle.</summary>
    private static bool Terrace(PlannedLevel L, PlannedArea a, LevelRandom rnd, FeatureSet features)
    {
        if (FreeDirection(a, rnd, 0.25) is not { } d) return false;
        double step = SpyroMoves.DesignLedge;
        double r = a.R * Range(rnd, 0.36, 0.52), off = a.R * Range(rnd, 0.7, 0.9);
        (double X, double Y) c = (a.X + d.X * off, a.Y + d.Y * off);
        // The foot of the flight: on the terrace's edge towards the room's middle. Three steps where the room has
        // the floor for them, else two.
        (double X, double Y) q = (c.X - d.X * r, c.Y - d.Y * r);
        int steps = off - r - 290 >= 0.12 * a.R && rnd.Chance(0.7) ? 3 : 2;
        double out1 = steps == 3 ? 290 : 160;
        if (off - r - out1 < 0.1 * a.R) return false;
        if (!Free(L, a, c.X, c.Y, r) || !OffJoins(L, a, c.X, c.Y, r) || !OffJoins(L, a, q.X - d.X * 120, q.Y - d.Y * 120, 190)) return false;
        if (Overlaps(L, L.Plan.Zones.Count, c.X, c.Y, r + 40) || Overlaps(L, L.Plan.Zones.Count, q.X - d.X * 120, q.Y - d.Y * 120, 230)) return false;
        a.Taken.Add(d);
        if (steps == 3)
        {
            Step(L, a, q.X - d.X * 60, q.Y - d.Y * 60, 230, a.Z + step, a.Priority + 1);
            Step(L, a, q.X + d.X * 20, q.Y + d.Y * 20, 160, a.Z + 2 * step, a.Priority + 2);
        }
        else Step(L, a, q.X - d.X * 10, q.Y - d.Y * 10, 170, a.Z + step, a.Priority + 1);
        var top = Step(L, a, c.X, c.Y, r, a.Z + steps * step, a.Priority + steps, "terrace");
        // The first jump of the flight.
        L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", P(q.X - d.X * (out1 + 90), q.Y - d.Y * (out1 + 90), a.Z), P(q.X - d.X * (out1 - 100), q.Y - d.Y * (out1 - 100), a.Z + step)) { JumpAt = 60 });
        for (int k = 0; k < 3; k++)
        {
            var e = Turn(d, Range(rnd, -1.2, 1.2));
            L.Gems.Add((c.X + e.X * r * 0.45, c.Y + e.Y * r * 0.45, top.Id));
        }
        a.Piece = "terrace";
        L.Count("raised terrace");
        // From a high one, a glide to a perch round the room's edge.
        if (steps == 3 && features.Glides && a.R >= 600 && rnd.Chance(0.6) && GlideLedge(L, a, top, c, r, steps * step, d, (q.X - d.X * 60, q.Y - d.Y * 60, 230), rnd, round: false))
            L.Count("glide from a high place");
        return true;
    }

    /// <summary>Where Spyro starts in a room with something in its middle: on a porch of its own, a round floor let into
    /// the room's edge on a side with no way in or out. False when there is no side it fits.</summary>
    private static bool StartPorch(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        const double porch = 340;
        (double X, double Y)? best = null; double clear = -2;
        for (int k = 0; k < 20; k++)
        {
            var e = Dir(2 * 3.14159265358979 * (k + rnd.NextDouble()) / 20);
            double x = a.X + e.X * (a.R + 70), y = a.Y + e.Y * (a.R + 70);
            double off = a.Taken.Count == 0 ? 1 : a.Taken.Min(t => 1 - (t.X * e.X + t.Y * e.Y));
            if (off < 0.35 || off <= clear || !Free(L, a, x, y, porch, floor: true)) continue;
            clear = off; best = (x, y);
        }
        if (best is not { } at) return false;
        a.Zone.Discs.Add((at.X, at.Y, porch));
        a.Taken.Add(Unit(at.X - a.X, at.Y - a.Y));
        L.StartAt = at; L.Start = at;
        return true;
    }

    /// <summary>A stepped mound in the middle of a room: two to four tiers, each a jump high, and on its top a thing
    /// worth the climb; from a high one, a glide to a ledge at the room's edge.</summary>
    private static bool Mound(PlannedLevel L, PlannedArea a, LevelRandom rnd, FeatureSet features)
    {
        if (L.Doors.Any(d => d.Area == a.Id)) return false;
        int tiers = a.R >= 760 ? rnd.Next(2, 5) : rnd.Next(2, 4);
        double top = Range(rnd, 160, 215), tread = Range(rnd, 150, 185), step = SpyroMoves.DesignLedge;
        while (tiers > 2 && top + tread * (tiers - 1) > 0.5 * a.R - 20) tiers--;
        double foot = top + tread * (tiers - 1);
        if (foot > 0.54 * a.R) return false;
        var j = Dir(Range(rnd, 0, 6.2832));
        double shift = Range(rnd, 0, 0.1) * a.R;
        (double X, double Y) c = (a.X + j.X * shift, a.Y + j.Y * shift);
        if (Overlaps(L, L.Plan.Zones.Count, c.X, c.Y, foot + 40)) return false;
        if (a.Id == 0 && !StartPorch(L, a, rnd)) return false;
        TerrainZone? summit = null;
        // The jumps up it: on the side Spyro comes from.
        (double X, double Y) f = a.Id == 0 && L.StartAt is { } s ? Unit(s.X - c.X, s.Y - c.Y) : a.Taken.Count > 0 ? a.Taken[0] : Dir(Range(rnd, 0, 6.2832));
        for (int k = 1; k <= tiers; k++)
        {
            double r = foot - tread * (k - 1);
            summit = Step(L, a, c.X, c.Y, r, a.Z + step * k, a.Priority + k, k == tiers ? "mound top" : "mound");
            L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", P(c.X + f.X * (r + 90), c.Y + f.Y * (r + 90), a.Z + step * (k - 1)), P(c.X + f.X * (r - 75), c.Y + f.Y * (r - 75), a.Z + step * k)) { JumpAt = 60 });
        }
        L.Gems.Add((c.X, c.Y, summit!.Id));
        for (int q = 0; q < 3; q++) { var e = Dir(Range(rnd, 0, 6.2832)); L.Gems.Add((c.X + e.X * (foot - tread * 0.5), c.Y + e.Y * (foot - tread * 0.5), L.Plan.Zones[summit.Id - tiers + 1].Id)); }
        if (top >= 185) L.Landmarks.Add((c.X, c.Y, summit.Id, top));
        if (tiers >= 3 && features.Glides && top < 185)
        {
            var away = shift > 20 ? j : Dir(Range(rnd, 0, 6.2832));
            if (GlideLedge(L, a, summit, c, top, step * tiers, away, null, rnd, round: true)) L.Count("glide from a high place");
        }
        a.Piece = "mound";
        L.Count("stepped mound");
        return true;
    }

    private static (double X, double Y) Unit(double x, double y) { double l = Math.Sqrt(x * x + y * y); return l <= 0 ? (1, 0) : (x / l, y / l); }

    /// <summary>A moat: water all round an island in the middle of a room, a jump across, with something on the island
    /// worth the jump.</summary>
    private static bool Moat(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        if (L.Doors.Any(d => d.Area == a.Id)) return false;
        double water = Math.Clamp(a.R * Range(rnd, 0.3, 0.38), 300, 430), island = water - 100;
        var j = Dir(Range(rnd, 0, 6.2832));
        double shift = Range(rnd, 0, 0.08) * a.R;
        (double X, double Y) c = (a.X + j.X * shift, a.Y + j.Y * shift);
        if (Overlaps(L, L.Plan.Zones.Count, c.X, c.Y, water + 60)) return false;
        if (a.Id == 0 && !StartPorch(L, a, rnd)) return false;
        var moat = L.Plan.Add(new TerrainZone
        {
            Kind = "pond", Z = a.Z - 26, Z1 = a.Z - 26, Area = a.Id, Priority = a.Priority + 6, Blend = 0, Wobble = 10, WobbleScale = 260, Crisp = true, Flat = true,
            Floor = L.Plan.LiquidRole, Liquid = L.Plan.LiquidKind,
        });
        moat.Discs.Add((c.X, c.Y, water));
        var land = L.Plan.Add(new TerrainZone { Kind = "islet", Z = a.Z, Z1 = a.Z, Area = a.Id, Priority = a.Priority + 7, Blend = 0, Wobble = 5, WobbleScale = 200, Crisp = true, Floor = a.Zone.Floor });
        land.Discs.Add((c.X, c.Y, island));
        (double X, double Y) f = a.Id == 0 && L.StartAt is { } s ? Unit(s.X - c.X, s.Y - c.Y) : a.Taken.Count > 0 ? a.Taken[0] : Dir(Range(rnd, 0, 6.2832));
        L.Links.Add(new LevelLink(a.Id, a.Id, "hop", P(c.X + f.X * (water + 130), c.Y + f.Y * (water + 130), a.Z), P(c.X + f.X * (island - 110), c.Y + f.Y * (island - 110), a.Z)) { JumpAt = 110 });
        if (island >= 230) L.Landmarks.Add((c.X, c.Y, land.Id, island));
        for (int q = 0; q < 4; q++) { var e = Dir(Range(rnd, 0, 6.2832)); L.Gems.Add((c.X + e.X * island * 0.6, c.Y + e.Y * island * 0.6, land.Id)); }
        a.Piece = "moat";
        L.Count("moat round an island");
        return true;
    }

    /// <summary>A hill of terraces in a big room: each stands a storey over the one below, off to one side of it so the
    /// lower floor runs wide round the other side, and there a ramp climbs round the terrace's wall onto it. On the top
    /// one, where there is room, a step or two more to the summit. Returns how many terraces were built.</summary>
    /// <param name="tall">The hill is the level: as many terraces as the room holds, and high ones.</param>
    private static int Mount(PlannedLevel L, PlannedArea a, LevelRandom rnd, bool tall, bool castle)
    {
        if (L.Doors.Any(d => d.Area == a.Id)) return 0;
        var plan = L.Plan;
        // The ramp: its width as it shows (the rest lies under the terrace it climbs onto), and its slope.
        const double half = 190, under = 100, show = 2 * half - under;
        double slope = Math.Min(0.25, Tune.Climb + 0.03);
        (double X, double Y) c = (a.X, a.Y); double r = a.R, z = a.Z; int priority = a.Priority;
        // The first terrace leans to the side with no way in or out: the lower floor is narrow there and wide opposite.
        (double X, double Y) u = Dir(Range(rnd, 0, 6.2832));
        double bestClear = -2;
        for (int k = 0; k < 24; k++)
        {
            var e = Dir(2 * 3.14159265358979 * (k + rnd.NextDouble()) / 24);
            // Clear of the ways in on its own side, and best with none right opposite either (where the ramp runs).
            double clear = a.Taken.Count == 0 ? 1 : a.Taken.Min(t => Math.Min(1 - (t.X * e.X + t.Y * e.Y), 1.6 + (t.X * e.X + t.Y * e.Y)));
            if (clear > bestClear) { bestClear = clear; u = e; }
        }
        int zones0 = plan.Zones.Count;
        // Spyro starts on a porch at the room's edge.
        if (a.Id == 0 && !StartPorch(L, a, rnd)) return 0;
        int built = 0;
        TerrainZone? summit = null; double summitR = 0;
        for (int level = 1; level <= (tall ? 4 : 3); level++)
        {
            double ring = level == 1 ? Range(rnd, 370, 430) : Range(rnd, 335, 390), lean = Range(rnd, 150, 195);
            double rk = r - ring;
            if (rk < 300) break;
            (double X, double Y) ck = (c.X + u.X * lean, c.Y + u.Y * lean);
            bool made = false;
            foreach (double h in tall ? new[] { Math.Round(Range(rnd, 185, 240)), 168.0, 140.0, 112.0 } : [Math.Round(Range(rnd, 150, 205)), 140.0, 112.0])
            {
                double rho = rk + half - under, run = h / slope, length = run + 110 + 170, sweep = length / rho;
                if (sweep > 2.5) continue;
                // The ramp runs round the wide side, either way.
                double way = rnd.Chance(0.5) ? 1 : -1;
                var spine = new List<(double X, double Y)>();
                bool fits = true;
                for (int q = 0; q <= 12 && fits; q++)
                {
                    var e = Turn((-u.X, -u.Y), way * sweep * (q / 12.0 - 0.5));
                    var p = (X: ck.X + e.X * rho, Y: ck.Y + e.Y * rho);
                    spine.Add(p);
                    // Its outer edge on the floor below, with room to walk past it.
                    if (Dist(p.X, p.Y, c.X, c.Y) + half + (level == 1 ? 190 : 130) > r) fits = false;
                }
                // Clear of the floors already planned in the room: the paths that come into it, the steps of a flight,
                // a ledge (the ramp and the terrace would bury them).
                if (!fits || spine.Any(p => Overlaps(L, zones0, p.X, p.Y, half + 40, half + 10)) || Overlaps(L, zones0, ck.X, ck.Y, rk + 40, rk + 10)) continue;
                var terrace = plan.Add(new TerrainZone { Kind = "terrace", Z = z + h, Z1 = z + h, Area = a.Id, Priority = priority + 2, Blend = 0, Wobble = 12, WobbleScale = 260, Crisp = true, Floor = a.Zone.Floor });
                terrace.Discs.Add((ck.X, ck.Y, rk));
                var ramp = plan.Add(new TerrainZone
                {
                    Kind = "rise", Z = z, Z1 = z + h, Area = a.Id, Priority = priority + 1, HalfWidth = half, Wobble = 0, Crisp = true,
                    Floor = castle ? SurfaceRole.Paving : SurfaceRole.Path,
                });
                ramp.Spine.AddRange(spine);
                double total = ramp.Length;
                ramp.RampFrom = 110 / total; ramp.RampTo = 1 - 170 / total;
                // Walked in four stretches along the middle of the part that shows.
                for (int part = 0; part < 4; part++)
                {
                    (double X, double Y) from = Mid(part * 3), to = Mid(part * 3 + 3);
                    L.Links.Add(new LevelLink(a.Id, a.Id, "ramp", P(from.X, from.Y, ramp.FloorAt(from.X, from.Y)), P(to.X, to.Y, ramp.FloorAt(to.X, to.Y))));
                }
                (double X, double Y) Mid(int q)
                {
                    var e = Unit(spine[q].X - ck.X, spine[q].Y - ck.Y);
                    return (ck.X + e.X * (rk + show / 2 + 10), ck.Y + e.Y * (rk + show / 2 + 10));
                }
                L.Gems.Add((Mid(4).X, Mid(4).Y, ramp.Id)); L.Gems.Add((Mid(8).X, Mid(8).Y, ramp.Id));
                for (int q = 0; q < 3; q++) { var e = Dir(Range(rnd, 0, 6.2832)); L.Gems.Add((ck.X + e.X * rk * 0.8, ck.Y + e.Y * rk * 0.8, terrace.Id)); }
                c = ck; r = rk; z += h; priority += 2; built++;
                summit = terrace; summitR = rk;
                made = true;
                break;
            }
            if (!made) break;
            // The next terrace leans another way, so the climb winds round the hill.
            u = Turn(u, Range(rnd, 1.9, 4.4));
        }
        if (built == 0)
        {
            // No hill after all: the start goes back to the room's middle (the porch stays, a bay in the room's edge).
            if (a.Id == 0) { L.StartAt = null; L.Start = (a.X, a.Y); }
            return 0;
        }
        L.Count("terrace up a ramp", built);
        // The summit: a step or two more where the top terrace has the room, each a jump.
        double step = SpyroMoves.DesignLedge;
        int caps = summitR >= 520 ? 2 : summitR >= 380 ? 1 : 0;
        (double X, double Y) f = Dir(Range(rnd, 0, 6.2832));
        for (int k = 1; k <= caps; k++)
        {
            double rc = summitR - 170 * k - (k == 1 ? 20 : 0);
            if (rc < 150) break;
            summit = Step(L, a, c.X, c.Y, rc, z + step * k, priority + k, "summit");
            L.Links.Add(new LevelLink(a.Id, a.Id, "ledge", P(c.X + f.X * (rc + 90), c.Y + f.Y * (rc + 90), z + step * (k - 1)), P(c.X + f.X * (rc - 75), c.Y + f.Y * (rc - 75), z + step * k)) { JumpAt = 60 });
            summitR = rc;
        }
        double height = summit!.Z - a.Z;
        L.Gems.Add((c.X + f.X * summitR * 0.5, c.Y + f.Y * summitR * 0.5, summit.Id));
        L.Gems.Add((c.X - f.X * summitR * 0.5, c.Y - f.Y * summitR * 0.5, summit.Id));
        if (summitR >= 200) L.Landmarks.Add((c.X, c.Y, summit.Id, summitR));
        // From the top, a glide down to a ledge at the room's edge.
        var out_ = Unit(c.X - a.X, c.Y - a.Y);
        if (GlideLedge(L, a, summit, c, summitR, height, out_, null, rnd, round: true)) L.Count("glide from a high place");
        a.Piece = "mount";
        return built;
    }
}
