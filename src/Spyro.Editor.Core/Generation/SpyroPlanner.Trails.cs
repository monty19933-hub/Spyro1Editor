namespace Spyro.Editor.Core.Generation;

/// <summary>Trodden paths across the rooms, as the game's levels have them (Artisans, Stone Hill, Town Square, Dry
/// Canyon): worn ground from every way in (a path's mouth, a doorway, where Spyro starts) to a meeting place near the
/// room's middle, carrying on the paths between rooms. They are painted on the room's floor in the look's path
/// surface (<see cref="TerrainPlan.Decals"/>: ledges, mounds, houses and water keep their own), and they are where the
/// level's gems lie in lines, as the game lays its gems along the way.</summary>
internal static partial class SpyroPlanner
{
    private static void Trails(PlannedLevel L, LevelRandom rnd)
    {
        var plan = L.Plan;
        double half = Range(rnd, 46, 62);
        int made = 0;
        foreach (var a in L.Areas)
        {
            if (a.Zone.Kind != "room" || a.Zone.Liquid != null || a.R < 260) continue;
            // Every room's ways in and meeting place are noted (Random objects' guards); only grass, sand or dirt is worn.
            bool worn = a.Zone.Floor is SurfaceRole.Ground or SurfaceRole.GroundAlt or SurfaceRole.Sand or SurfaceRole.Dirt;
            // The ways in: joins (the floor's edge towards the other room), doorways (the floor in front), the start.
            List<(double X, double Y)> mouths = [];
            foreach (var j in L.Joins)
            {
                if (j.A != a.Id && j.B != a.Id) continue;
                var o = L.Areas[j.A == a.Id ? j.B : j.A];
                var u = Unit(o.X - a.X, o.Y - a.Y);
                double at = -1;
                for (double t = 0; t < a.Reach + 200; t += 16)
                    if (a.Zone.Sdf(a.X + u.X * t, a.Y + u.Y * t, plan.Noise) > -30) { at = t; break; }
                if (at > 0.3 * a.R) mouths.Add((a.X + u.X * at, a.Y + u.Y * at));
            }
            HashSet<(double X, double Y)> doorMouths = [];
            foreach (var d in L.Doors.Where(d => d.Area == a.Id))
            {
                (double X, double Y) front = (d.X - d.Dx * Math.Min(220, 0.5 * d.Front), d.Y - d.Dy * Math.Min(220, 0.5 * d.Front));
                mouths.Add(front); doorMouths.Add(front);
            }
            if (a.Id == 0 && L.StartAt is { } start) mouths.Add(start);
            if (mouths.Count == 0) continue;
            // The meeting place: near the middle, on the room's own floor.
            (double X, double Y) hub = (a.X, a.Y);
            for (int k = 0; k < 8; k++)
            {
                var e = Dir(Range(rnd, 0, 6.2832));
                double r = Range(rnd, 0, 0.2 * a.R);
                if (plan.ZoneAt(a.X + e.X * r, a.Y + e.Y * r) == a.Zone.Id) { hub = (a.X + e.X * r, a.Y + e.Y * r); break; }
            }
            if (plan.ZoneAt(hub.X, hub.Y) != a.Zone.Id) continue;
            L.Spots.Add(("hub", hub.X, hub.Y, a.Zone.Id, hub.X, hub.Y, a.Id));
            foreach (var m in mouths)
                L.Spots.Add((doorMouths.Contains(m) ? "door" : a.Id == 0 && L.StartAt is { } s0 && m == s0 ? "start" : "mouth", m.X, m.Y, a.Zone.Id, hub.X, hub.Y, a.Id));
            foreach (var m in mouths)
            {
                if (!worn) break;
                double len = Dist(m.X, m.Y, hub.X, hub.Y);
                if (len < 160) continue;
                var u = Unit(hub.X - m.X, hub.Y - m.Y);
                // A gentle bend, one way or the other, round whatever stands in the way.
                List<(double X, double Y)>? best = null;
                foreach (double bend in rnd.Chance(0.5) ? new[] { 0.18, -0.18, 0.0 } : new[] { -0.18, 0.18, 0.0 })
                {
                    double bx = (m.X + hub.X) / 2 - u.Y * bend * len * Range(rnd, 0.6, 1.2), by = (m.Y + hub.Y) / 2 + u.X * bend * len * Range(rnd, 0.6, 1.2);
                    int n = Math.Max(4, (int)(len / 60));
                    List<(double X, double Y)> spine = [];
                    for (int q = 0; q <= n; q++)
                    {
                        double t = q / (double)n, s = 1 - t;
                        (double X, double Y) p = (s * s * m.X + 2 * s * t * bx + t * t * hub.X, s * s * m.Y + 2 * s * t * by + t * t * hub.Y);
                        // Stops short of anything built or wet (a house, a wall, a pool): the path ends at it.
                        int z = plan.ZoneAt(p.X, p.Y);
                        if (z >= 0 && (IsBuilt(plan.Zones[z].Kind) || plan.Zones[z].Liquid != null)) break;
                        spine.Add(p);
                    }
                    if (best == null || spine.Count > best.Count) best = spine;
                    if (spine.Count == n + 1) break;
                }
                if (best == null || best.Count < 4) continue;
                var trail = new TerrainZone { Kind = "trail", Area = a.Id, HalfWidth = half * Range(rnd, 0.85, 1.1), Wobble = 16, WobbleScale = 180, Blend = 0, Floor = SurfaceRole.Path };
                trail.Spine.AddRange(best);
                plan.Decals.Add(trail);
                made++;
                // Gems lie along it, as the game's lie along the way (not at its ends: the mouth and the meeting place).
                for (int q = 2; q < best.Count - 1; q += 2) L.Gems.Add((best[q].X, best[q].Y, a.Zone.Id));
            }
        }
        if (made > 0) L.Count("trodden path", made);
    }

    /// <summary>The tops of the level's climbs, for Random objects' rewards: every ledge, glide ledge, lookout, terrace,
    /// islet or rise that stands half a jump or more over its room's floor and has no higher step of its own climb on it,
    /// the summits, and the nooks' middles.</summary>
    private static void TopSpots(PlannedLevel L)
    {
        var plan = L.Plan;
        var steps = plan.Zones.Where(z => z.Kind is "ledge" or "glide ledge" or "lookout" or "terrace" or "islet" or "rise" && z.Discs.Count > 0 && z.Liquid == null &&
            z.Area >= 0 && z.Area < L.Areas.Count && z.Z - L.Areas[z.Area].Z >= 50).ToList();
        foreach (var z in steps)
        {
            var (x, y, r) = z.Discs[^1];
            // A step with a higher one of the same climb standing on it is on the way up, not the top.
            if (steps.Any(o => o != z && o.Area == z.Area && o.Z > z.Z && Dist(o.Discs[^1].X, o.Discs[^1].Y, x, y) < r + o.Discs[^1].R)) continue;
            if (plan.ZoneAt(x, y) != z.Id) continue;
            L.Spots.Add(("top", x, y, z.Id, x, y, z.Area));
        }
        foreach (var (x, y, zone, _) in L.Landmarks) L.Spots.Add(("top", x, y, zone, x, y, zone >= 0 && zone < plan.Zones.Count ? plan.Zones[zone].Area : -1));
        foreach (var a in L.Areas.Where(a => a.Kind == "nook")) L.Spots.Add(("nook", a.X, a.Y, a.Zone.Id, a.X, a.Y, a.Id));
    }
}
