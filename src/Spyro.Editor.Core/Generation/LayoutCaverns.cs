namespace Spyro.Editor.Core.Generation;

/// <summary>Caverns: solid rock with chambers carved out of it and two-cell tunnels between them. The start chamber is an
/// open sinkhole under the sky (a grassy crater rim round it); the other chambers have rock ceilings well above Spyro's
/// jumps, sometimes a skylight. Tunnels climb no steeper than a walking ramp.</summary>
internal static class LayoutCaverns
{
    public static void Build(Layout L, int n)
    {
        var rnd = L.Random; var grid = L.Grid;
        int count = n / 6 + rnd.Next(1, 3);
        var reserved = new HashSet<(int I, int J)>();
        for (int k = 0; k < count * 8 && L.Areas.Count < count; k++)
        {
            float r = L.Areas.Count == 0 ? 4.4f + (float)rnd.NextDouble() : 2.6f + (float)rnd.NextDouble() * 2.8f;
            float ci = 4 + r + (float)rnd.NextDouble() * (n - 8 - 2 * r), cj = 4 + r + (float)rnd.NextDouble() * (n - 8 - 2 * r);
            if (L.Areas.Count == 0) { ci = 5 + r + rnd.Next(0, 3); cj = n / 2f + rnd.Next(-4, 5); }
            var cells = L.Blob(ci, cj, r, 0.3f).Where(c => c.I >= 3 && c.J >= 3 && c.I < n - 3 && c.J < n - 3).ToList();
            if (cells.Count < (L.Areas.Count == 0 ? 40 : 14) || cells.Any(reserved.Contains)) continue;
            var a = L.NewArea(L.Areas.Count == 0 ? "start" : "chamber", LayoutValley.Ground0);
            a.Cells.AddRange(cells); a.Changed();
            a.Seed = cells.MinBy(c => Math.Pow(c.I + 0.5 - ci, 2) + Math.Pow(c.J + 0.5 - cj, 2));
            foreach (var c in cells) for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++) reserved.Add((c.I + x, c.J + y));
        }
        var start = L.Areas[0];
        L.StartArea = start.Id;
        var pad = Layout.Square(start.Set, 4, start.Centre) ?? start.Seed;
        L.StartCell = (pad.I + 2, pad.J + 2);
        for (int a = -1; a <= 4; a++) for (int b = -1; b <= 4; b++) L.Reserved.Add((pad.I + a, pad.J + b));

        // Tunnels along a spanning tree (nearest chambers first), routes chosen before heights so heights fit the routes.
        var chambers = L.Areas.ToList();
        var inTree = new HashSet<int> { start.Id };
        var tunnels = new List<(Area A, Area B, List<(int I, int J)> Route)>();
        while (inTree.Count < chambers.Count)
        {
            var (from, to, d) = chambers.Where(x => inTree.Contains(x.Id)).SelectMany(x => chambers.Where(y => !inTree.Contains(y.Id))
                .Select(y => (x, y, Math.Abs(x.Centre.I - y.Centre.I) + Math.Abs(x.Centre.J - y.Centre.J)))).MinBy(t => t.Item3);
            inTree.Add(to.Id);
            tunnels.Add((from, to, Route(L, from, to)));
        }
        // Heights: each chamber within what its tunnel can climb from its parent.
        foreach (var (a, b, route) in tunnels)
        {
            int climb = (int)(Math.Max(1, route.Count - 2) * SpyroMoves.DesignRampRise * 0.9);
            int dz = 16 * rnd.Next(-8, 13);
            b.Z = Math.Clamp(a.Z + Math.Clamp(dz, -climb, climb), 480, 1100);
        }
        int rockTop = L.Areas.Max(a => a.Z) + 520;
        // Solid rock everywhere, then the chambers.
        foreach (var (i, j) in grid.AllCells())
        {
            var rock = L.GroundSlab(rockTop, null, SurfaceRole.Ground);
            rock.SideRole = SurfaceRole.CaveWall; rock.SideTopRole = null; rock.DrawTop = false;
            grid.Set(i, j, rock);
        }
        // Rock round open chambers is lower (a crater rim) and shows its grassy top.
        var open = new HashSet<int> { start.Id };
        foreach (var a in L.Areas.Skip(1)) if (rnd.Chance(0.25)) open.Add(a.Id);
        foreach (var a in L.Areas.Where(a => open.Contains(a.Id)))
        {
            var rim = new Dictionary<(int, int), int>();
            foreach (var c in a.Cells) for (int x = -4; x <= 4; x++) for (int y = -4; y <= 4; y++) { var k = (c.I + x, c.J + y); rim[k] = Math.Min(rim.GetValueOrDefault(k, 99), Math.Max(Math.Abs(x), Math.Abs(y))); }
            foreach (var (k, d) in rim)
            {
                if (!grid.Inside(k.Item1, k.Item2) || a.Set.Contains(k)) continue;
                var s = grid.At(k.Item1, k.Item2)[0];
                int z = a.Z + 360 + 48 * d + 16 * rnd.Next(0, 3);
                if (z < s.Top[0]) { s.SetTop(z); s.DrawTop = true; s.SideRole = SurfaceRole.Cliff; s.SideTopRole = SurfaceRole.CliffTop; }
            }
        }
        foreach (var a in L.Areas) Carve(L, a.Cells, a, a.Z, a.Z + 360 + 16 * rnd.Next(0, 8), !open.Contains(a.Id));
        foreach (var (a, b, route) in tunnels) Tunnel(L, a, b, route);
        Decorate(L, open);
    }

    /// <summary>An L-shaped two-cell route from chamber a to chamber b (the cells between them, outside both).</summary>
    private static List<(int I, int J)> Route(Layout L, Area a, Area b)
    {
        var p = a.Cells.MinBy(c => Math.Abs(c.I - b.Centre.I) + Math.Abs(c.J - b.Centre.J));
        var q = b.Cells.MinBy(c => Math.Abs(c.I - p.I) + Math.Abs(c.J - p.J));
        var route = new List<(int I, int J)>();
        bool xFirst = L.Random.Chance(0.5);
        int i = p.I, j = p.J;
        void Step() { if (!a.Set.Contains((i, j)) && !b.Set.Contains((i, j)) && !route.Contains((i, j))) route.Add((i, j)); }
        if (xFirst) { while (i != q.I) { i += Math.Sign(q.I - i); Step(); } while (j != q.J) { j += Math.Sign(q.J - j); Step(); } }
        else { while (j != q.J) { j += Math.Sign(q.J - j); Step(); } while (i != q.I) { i += Math.Sign(q.I - i); Step(); } }
        return route;
    }

    /// <summary>Opens cells to floor height <paramref name="floor"/> with (when <paramref name="roofed"/>) a ceiling at
    /// <paramref name="ceiling"/>; the rock above keeps its top.</summary>
    private static void Carve(Layout L, IEnumerable<(int I, int J)> cells, Area a, int floor, int ceiling, bool roofed)
    {
        foreach (var (i, j) in cells)
        {
            var rock = L.Grid.At(i, j)[0];
            int top = rock.Top[0];
            var g = L.GroundSlab(floor, a, L.Theme.Key is "meadow" or "highland" or "swamp" ? SurfaceRole.Dirt : SurfaceRole.Rock);
            g.SideRole = SurfaceRole.Rock; g.SideTopRole = null;
            L.Grid.Set(i, j, g);
            if (roofed && top > ceiling + 32)
            {
                var roof = new Slab { TopRole = SurfaceRole.Ground, SideRole = SurfaceRole.CaveWall, SideTopRole = null, BottomRole = SurfaceRole.Ceiling, DrawTop = rock.DrawTop };
                roof.SetBottom(ceiling); roof.SetTop(top);
                L.Grid.Add(i, j, roof);
            }
        }
        a.Covered = roofed;
        if (!roofed && a.Kind == "start") foreach (var (i, j) in cells) L.Grid.At(i, j)[0].TopRole = SurfaceRole.Ground;
    }

    /// <summary>A two-cell tunnel along the route, its floor sloping evenly from one chamber's height to the other's.</summary>
    private static void Tunnel(Layout L, Area a, Area b, List<(int I, int J)> route)
    {
        var t = L.NewArea("tunnel", (a.Z + b.Z) / 2);
        int len = route.Count;
        // Each route cell and the cell beside it (across the direction of travel) make the two-cell tunnel.
        var cells = new List<(int I, int J)>();
        for (int k = 0; k < len; k++)
        {
            var c = route[k];
            var prev = k > 0 ? route[k - 1] : Layout.Neighbours4(c).FirstOrDefault(a.Set.Contains, c);
            var dir = (c.I - prev.I, c.J - prev.J);
            var side = dir.Item1 != 0 ? (c.I, c.J + 1) : (c.I + 1, c.J);
            foreach (var x in new[] { c, side })
                if (!a.Set.Contains(x) && !b.Set.Contains(x) && !cells.Contains(x) && L.Areas.All(o => o == t || !o.Set.Contains(x))) cells.Add(x);
        }
        t.Cells.AddRange(cells); t.Changed();
        t.Seed = route.Count > 0 ? route[route.Count / 2] : a.Seed;
        // Heights: by distance along the route; tunnel cells are flat steps no more than a ramp's rise apart... as ramps.
        int dz = b.Z - a.Z;
        foreach (var c in cells)
        {
            int k = route.FindIndex(r => Math.Abs(r.I - c.I) + Math.Abs(r.J - c.J) <= 1);
            if (k < 0) k = 0;
            int z = a.Z + (int)Math.Round(dz * (k + 0.5) / Math.Max(1, len));
            int rockTop = L.Grid.At(c.I, c.J)[0].Top[0];
            Carve(L, [c], t, z, Math.Max(a.Z, b.Z) + 280, true);
        }
        // Smooth: each tunnel cell's corners take the average of the cell floors that share them, so the floor is a ramp
        // with no steps (heights along the route change at most a ramp's rise per cell).
        var floorOf = cells.ToDictionary(c => c, c => L.Grid.At(c.I, c.J)[0].Top[0]);
        foreach (var c in a.Cells.Concat(b.Cells)) floorOf.TryAdd(c, L.Grid.At(c.I, c.J)[0].Top[0]);
        int[] di = [0, 1, 1, 0], dj = [0, 0, 1, 1];
        foreach (var c in cells)
        {
            var s = L.Grid.At(c.I, c.J)[0];
            for (int k = 0; k < 4; k++)
            {
                var around = new[] { (c.I + di[k] - 1, c.J + dj[k] - 1), (c.I + di[k], c.J + dj[k] - 1), (c.I + di[k], c.J + dj[k]), (c.I + di[k] - 1, c.J + dj[k]) }
                    .Where(floorOf.ContainsKey).Select(x => floorOf[x]).ToList();
                // Chamber cells keep their own (flat) height: a corner touching one takes that height.
                var chamber = new[] { (c.I + di[k] - 1, c.J + dj[k] - 1), (c.I + di[k], c.J + dj[k] - 1), (c.I + di[k], c.J + dj[k]), (c.I + di[k] - 1, c.J + dj[k]) }
                    .Where(x => a.Set.Contains(x) || b.Set.Contains(x)).Select(x => floorOf[x]).ToList();
                s.Top[k] = chamber.Count > 0 ? chamber[0] : (int)Math.Round(around.Average());
            }
            s.TopRole = SurfaceRole.Path;
            L.Reserved.Add(c);
        }
        L.Links.Add(new(a.Id, b.Id, "tunnel", L.CellTop(route.Count > 0 ? route[0].I : a.Seed.I, route.Count > 0 ? route[0].J : a.Seed.J), L.CellTop(route.Count > 0 ? route[^1].I : b.Seed.I, route.Count > 0 ? route[^1].J : b.Seed.J)));
        L.Count("tunnel");
    }

    private static void Decorate(Layout L, HashSet<int> open)
    {
        var rnd = L.Random;
        var chambers = L.Areas.Where(a => a.Kind is "chamber" or "start").ToList();
        foreach (var a in chambers)
        {
            L.GemSpots.Add(L.CellTop(a.Seed.I, a.Seed.J));
            if (a.Kind == "start") continue;
            // Stalagmites: rock columns from floor to ceiling.
            var depth = Layout.EdgeDistance(a.Set);
            for (int k = rnd.Next(0, 4); k > 0; k--)
            {
                var spots = a.Cells.Where(c => depth[c] >= 3 && !L.Reserved.Contains(c)).ToList();
                if (spots.Count == 0) break;
                var c = rnd.Pick(spots);
                var col = L.GroundSlab(L.Grid.At(c.I, c.J)[^1].MaxTop, null, SurfaceRole.Rock);
                col.SideRole = SurfaceRole.Rock; col.SideTopRole = null; col.DrawTop = open.Contains(a.Id);
                L.Grid.Set(c.I, c.J, col);
                foreach (var nb in Layout.Neighbours4(c).Append(c)) L.Reserved.Add(nb);
                L.Count("stalagmite");
            }
        }
        var others = chambers.Where(a => a.Kind != "start").ToList();
        rnd.Shuffle(others);
        if (L.Features.Hazards) foreach (var a in others.Where(a => a.Cells.Count >= 30).Take(2)) LayoutValley.Pond(L, a);
        if (L.Features.Glides || L.Features.Bridges) foreach (var a in others.Where(a => a.Cells.Count >= 40).Take(1)) LayoutValley.Chasm(L, a);
        foreach (var a in others.Where(a => a.Cells.Count >= 20)) for (int k = rnd.Next(0, 2); k > 0; k--) LayoutValley.Pillar(L, a, rnd.Pick<int>([32, 48]));
    }
}
