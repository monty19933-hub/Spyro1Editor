namespace Spyro.Editor.Core.Generation;

/// <summary>What is built on the floor: houses with their backs to a room's edge and their doors to its middle, a few
/// together like a village (Town Square, Cliff Town, Terrace Village, Gnorc Cove), and castle walls with towers along
/// a room's edge (Artisans, Dark Hollow, Lofty Castle). Both are solid blocks of the land (Spyro walks round them, and
/// nothing is put inside them); <see cref="SpyroStructures"/> puts the roofs, doors, windows and battlements on.</summary>
internal static partial class SpyroPlanner
{
    /// <summary>Zone kinds that are built things, not floor Spyro must reach.</summary>
    internal static bool IsBuilt(string kind) => kind is "house" or "rampart";

    /// <summary>Houses round the level: a village in one or two rooms, a house or two elsewhere.</summary>
    private static void Houses(PlannedLevel L, LevelStyle style, LevelRandom rnd)
    {
        var plan = L.Plan;
        if (plan.Kind is EnclosureKind.Sky && rnd.Chance(0.5)) return;
        int want = style switch
        {
            LevelStyle.Castle => rnd.Next(4, 9), LevelStyle.Valley => rnd.Next(3, 8), LevelStyle.Canyon => rnd.Next(3, 7),
            LevelStyle.Islands => rnd.Next(2, 5), LevelStyle.SkyIslands => rnd.Next(1, 4), _ => rnd.Next(0, 2),
        };
        // The village rooms first (big, not the start), then any room with the floor for one.
        var rooms = L.Areas.Where(a => a.Kind is "start" or "clearing" && a.R >= 460 && a.Zone.Roof <= 0 && a.Piece is not ("mount" or "lookout")).ToList();
        rnd.Shuffle(rooms);
        rooms = [.. rooms.OrderByDescending(a => a.Id == 0 ? 0 : a.R * (0.6 + rnd.NextDouble()))];
        int made = 0;
        foreach (var a in rooms)
        {
            if (made >= want) break;
            int here = Math.Min(want - made, a.R >= 850 ? rnd.Next(2, 5) : rnd.Next(1, 3));
            for (int k = 0, tries = 0; k < here && tries < 40; tries++)
                if (House(L, a, rnd)) { k++; made++; }
        }
        if (made > 0) L.Count("house", made);
    }

    /// <summary>One house near a room's edge, its front (and door) to the room's middle: walls a storey high, too
    /// high to jump onto, a pitched roof over them.</summary>
    private static bool House(PlannedLevel L, PlannedArea a, LevelRandom rnd)
    {
        var plan = L.Plan;
        double hl = Range(rnd, 120, 195), hw = Range(rnd, 95, 140), tall = Range(rnd, 160, 230);
        double reach = Math.Sqrt(hl * hl + hw * hw);
        var e = Dir(Range(rnd, 0, 6.2832));
        if (a.Taken.Any(t => t.X * e.X + t.Y * e.Y > 0.6)) return false;
        // Its back a little in from the floor's edge that way.
        double edge = a.R;
        for (double s = a.R * 0.5; s <= a.R * 1.5; s += 16) { if (RoomGap(a, a.X + e.X * s, a.Y + e.Y * s) > 0) { edge = s; break; } }
        double at = edge - hw - Range(rnd, 60, 120);
        if (at < 0.25 * a.R) return false;
        double x = a.X + e.X * at, y = a.Y + e.Y * at;
        // The front faces the middle: the long side runs across the way out.
        (double X, double Y) along = (-e.Y, e.X);
        var start = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
        if (Dist(x, y, start.X, start.Y) < reach + 420) return false;
        if (!OffJoins(L, a, x, y, reach + 30) || Overlaps(L, plan.Zones.Count, x, y, reach + 60)) return false;
        if (L.Areas.Any(o => o != a && RoomGap(o, x, y) < reach + 80)) return false;
        // Every corner on this room's own floor (nothing else there), with a margin to the edge.
        foreach (var (u, v) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1), (0, 0) })
        {
            double cx = x + along.X * hl * u + e.X * hw * v, cy = y + along.Y * hl * u + e.Y * hw * v;
            if (RoomGap(a, cx, cy) > -40 || plan.ZoneAt(cx, cy) != a.Zone.Id) return false;
        }
        // Other houses: a lane between them.
        foreach (var z in plan.Zones)
            if (z.Kind == "house" && Dist(z.Boxes[0].X, z.Boxes[0].Y, x, y) < reach + Math.Sqrt(z.Boxes[0].HalfX * z.Boxes[0].HalfX + z.Boxes[0].HalfY * z.Boxes[0].HalfY) + 110) return false;
        var house = plan.Add(new TerrainZone
        {
            Kind = "house", Z = a.Z + tall, Z1 = a.Z + tall, Area = a.Id, Priority = a.Priority + 9, Blend = 0, Wobble = 0, Crisp = true, Flat = true,
            Floor = SurfaceRole.Roof,
        });
        house.Boxes.Add((x, y, hl, hw, along.X, along.Y, 6));
        a.Taken.Add(e);
        // Gems round the front.
        L.Gems.Add((x - e.X * (hw + 120), y - e.Y * (hw + 120), a.Zone.Id));
        return true;
    }

    /// <summary>Castle walls along a room's straight edges, a storey high and thick enough to walk along (from a
    /// glide), with towers at their ends; not across a way in or out, a doorway or anything that stands there.</summary>
    private static void Ramparts(PlannedLevel L, LevelStyle style, LevelRandom rnd)
    {
        var plan = L.Plan;
        int want = style switch { LevelStyle.Castle => rnd.Next(2, 5), LevelStyle.SkyIslands => rnd.Next(1, 3), _ => rnd.Next(1, 4) };
        var rooms = L.Areas.Where(a => a.Kind is "start" or "clearing" && a.R >= 560 && a.Zone.Roof <= 0).ToList();
        rnd.Shuffle(rooms);
        int made = 0;
        foreach (var a in rooms)
        {
            if (made >= want) break;
            // The room's straight edges (an outline's sides), else a chord near its rim.
            var sides = new List<((double X, double Y) P, (double X, double Y) Q)>();
            if (a.Outline is { } v)
                for (int k = 0; k < v.Length; k++) sides.Add(((a.X + v[k].X, a.Y + v[k].Y), (a.X + v[(k + 1) % v.Length].X, a.Y + v[(k + 1) % v.Length].Y)));
            else
                for (int k = 0; k < 6; k++)
                {
                    double t0 = Range(rnd, 0, 6.2832);
                    var p = Dir(t0); var q = Dir(t0 + Range(rnd, 0.8, 1.3));
                    sides.Add(((a.X + p.X * a.R, a.Y + p.Y * a.R), (a.X + q.X * a.R, a.Y + q.Y * a.R)));
                }
            rnd.Shuffle(sides);
            foreach (var (p, q) in sides)
            {
                if (made >= want) break;
                double len = Dist(p.X, p.Y, q.X, q.Y);
                if (len < 520) continue;
                // In from the edge by half the wall and a little more, and short of the corners.
                var u = Unit(q.X - p.X, q.Y - p.Y);
                var inward = Unit(a.X - (p.X + q.X) / 2, a.Y - (p.Y + q.Y) / 2);
                double half = Range(rnd, 74, 90), shrink = Math.Max(150, 0.12 * len);
                (double X, double Y) s0 = (p.X + u.X * shrink + inward.X * (half + 70), p.Y + u.Y * shrink + inward.Y * (half + 70));
                (double X, double Y) s1 = (q.X - u.X * shrink + inward.X * (half + 70), q.Y - u.Y * shrink + inward.Y * (half + 70));
                double length = Dist(s0.X, s0.Y, s1.X, s1.Y);
                if (length < 360) continue;
                if (length > 1400) { s1 = (s0.X + u.X * 1400, s0.Y + u.Y * 1400); length = 1400; }
                bool ok = true;
                var start = L.StartAt ?? (L.Areas[0].X, L.Areas[0].Y);
                for (double t = 0; t <= length + 1 && ok; t += 60)
                {
                    double x = s0.X + u.X * t, y = s0.Y + u.Y * t;
                    if (RoomGap(a, x, y) > -half - 20 || plan.ZoneAt(x, y) != a.Zone.Id) ok = false;
                    else if (!OffJoins(L, a, x, y, half + 40) || Overlaps(L, plan.Zones.Count, x, y, half + 80)) ok = false;
                    else if (Dist(x, y, start.X, start.Y) < half + 380) ok = false;
                    else if (plan.Zones.Any(z => z.Kind == "house" && z.Sdf(x, y, plan.Noise) < half + 110)) ok = false;
                }
                if (!ok) continue;
                var wall = plan.Add(new TerrainZone
                {
                    Kind = "rampart", Z = a.Z + Range(rnd, 170, 230), Area = a.Id, Priority = a.Priority + 9, HalfWidth = half, Wobble = 0, Crisp = true, Flat = true,
                    Floor = SurfaceRole.Paving,
                });
                wall.Z1 = wall.Z;
                wall.Spine.Add(s0); wall.Spine.Add(s1);
                made++;
            }
        }
        if (made > 0) L.Count("castle wall", made);
    }
}
