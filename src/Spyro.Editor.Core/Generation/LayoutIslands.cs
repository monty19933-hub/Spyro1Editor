using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Generation;

/// <summary>Islands in water, or islands floating in the sky: islands of different heights joined along a spanning tree
/// by bridges (straight, never steeper than a walking ramp) or chains of stepping stones within a jump of each other,
/// plus one-way glides from higher islands. Sky islands hang over nothing, with rocky undersides.</summary>
internal static class LayoutIslands
{
    public const int Sea = 512;

    public static void Build(Layout L, int n)
    {
        var rnd = L.Random; var grid = L.Grid;
        bool sky = L.Style == LevelStyle.SkyIslands;
        // The sea (or nothing).
        if (!sky)
            foreach (var (i, j) in grid.AllCells())
            {
                var water = new Slab { TopRole = L.Liquid == SurfaceRole.Goo ? SurfaceRole.Goo : L.Liquid == SurfaceRole.Lava ? SurfaceRole.Lava : SurfaceRole.Water,
                    Kind = L.Liquid == SurfaceRole.Lava ? AuthoredSurfaceKind.DeadlyLava : L.Liquid == SurfaceRole.Goo ? AuthoredSurfaceKind.DeadlyOoze : AuthoredSurfaceKind.DeadlyWater,
                    SideRole = SurfaceRole.Sand, SideTopRole = null };
                water.SetTop(Sea);
                grid.Set(i, j, water);
            }
        // Islands: the start island first and largest, the others spread out with gaps between.
        int count = n / 5 + rnd.Next(0, 3) + (sky ? 2 : 0);
        var taken = new HashSet<(int I, int J)>();
        var margin = new HashSet<(int I, int J)>();
        for (int k = 0; k < count * 30 && L.Areas.Count < count; k++)
        {
            float r = L.Areas.Count == 0 ? 4.6f + (float)rnd.NextDouble() * 1.2f : 1.8f + (float)rnd.NextDouble() * (sky ? 2.4f : 3.2f);
            float ci = 4 + r + (float)rnd.NextDouble() * (n - 8 - 2 * r), cj = 4 + r + (float)rnd.NextDouble() * (n - 8 - 2 * r);
            if (L.Areas.Count == 0) { ci = n / 2f + rnd.Next(-3, 4); cj = 6 + r; }
            var cells = L.Blob(ci, cj, r, 0.22f).Where(c => c.I >= 3 && c.J >= 3 && c.I < n - 3 && c.J < n - 3).ToList();
            if (cells.Count < (L.Areas.Count == 0 ? 36 : 6) || cells.Any(margin.Contains)) continue;
            var a = L.NewArea(L.Areas.Count == 0 ? "start" : "island", sky ? 1500 : Sea + 64);
            a.Cells.AddRange(cells); a.Changed();
            a.Seed = cells.MinBy(c => Math.Pow(c.I + 0.5 - ci, 2) + Math.Pow(c.J + 0.5 - cj, 2));
            foreach (var c in cells) taken.Add(c);
            // Keep at least two cells of water (or sky) round each island.
            foreach (var c in cells) for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) margin.Add((c.I + x, c.J + y));
        }
        var start = L.Areas[0];
        L.StartArea = start.Id;
        var pad = Layout.Square(start.Set, 4, start.Centre) ?? start.Seed;
        L.StartCell = (pad.I + 2, pad.J + 2);
        for (int a = -1; a <= 4; a++) for (int b = -1; b <= 4; b++) L.Reserved.Add((pad.I + a, pad.J + b));

        // A spanning tree by the gaps between islands (Prim), then one or two extra links for loops.
        var inTree = new HashSet<int> { start.Id };
        var edges = new List<(int A, int B)>();
        while (inTree.Count < L.Areas.Count)
        {
            var best = L.Areas.Where(a => inTree.Contains(a.Id)).SelectMany(a => L.Areas.Where(b => !inTree.Contains(b.Id)).Select(b => (a, b, d: Gap(a, b).D)))
                .MinBy(x => x.d);
            inTree.Add(best.b.Id); edges.Add((best.a.Id, best.b.Id));
            // Each island's height is one its chain of stones can climb from its parent: a stone every three cells, each
            // a jump up at most, both ways.
            int climb = (int)((best.d / 3 + 1) * (SpyroMoves.DesignLedge - 16) * 0.85);
            int dz = sky ? 16 * rnd.Next(-20, 31) : 16 * rnd.Next(-6, 12);
            best.b.Z = Math.Clamp(best.a.Z + Math.Clamp(dz, -climb, climb), sky ? 1100 : Sea + 32, sky ? 2300 : Sea + 320);
        }
        foreach (var a in L.Areas.ToList()) Raise(L, a, sky);
        foreach (var (x, y) in edges) Connect(L, L.Areas[x], L.Areas[y], sky);
        for (int k = rnd.Next(0, 3); k > 0; k--)
        {
            var a = rnd.Pick(L.Areas); var b = L.Areas.Where(o => o != a && !edges.Contains((a.Id, o.Id)) && !edges.Contains((o.Id, a.Id))).MinBy(o => Gap(a, o).D);
            if (b != null && Gap(a, b).D <= 10) Connect(L, a, b, sky);
        }
        // Glides from higher islands to lower ones in reach (extra, one way).
        foreach (var a in L.Areas)
            foreach (var b in L.Areas)
            {
                if (a == b || a.Z - b.Z < 48) continue;
                var (d, ca, cb) = Gap(a, b);
                if ((d + 1) * LevelGrid.Cell <= SpyroMoves.GlideReach(a.Z - b.Z, 0.3f) && !L.Links.Any(l => l.A == a.Id && l.B == b.Id))
                { L.Links.Add(new(a.Id, b.Id, "glide", L.CellTop(ca.I, ca.J), L.CellTop(cb.I, cb.J))); L.Count("glide"); }
            }
        Decorate(L, sky);
    }

    /// <summary>An island's cells raised to its height, with a sandy shore on low islands and a terrace on big ones; a sky
    /// island's underside hangs down deeper towards its middle.</summary>
    private static void Raise(Layout L, Area a, bool sky)
    {
        var depth = Layout.EdgeDistance(a.Set);
        bool beach = !sky && a.Z - Sea <= 64;
        foreach (var c in a.Cells)
        {
            var s = L.GroundSlab(a.Z, a, beach && depth[c] == 1 ? SurfaceRole.Sand : SurfaceRole.Ground);
            if (sky)
            {
                int d = depth[c];
                s.Bottom = [0, 0, 0, 0];
                for (int k = 0; k < 4; k++) s.Bottom[k] = a.Z - 72 - 56 * Math.Min(d, 6) - 8 * L.Random.Next(0, 4);
                s.BottomRole = SurfaceRole.Underside;
            }
            if (beach) { s.SideRole = SurfaceRole.Sand; s.SideTopRole = null; }
            L.Grid.Set(c.I, c.J, s);
        }
        // A sky island's underside corners are shared between neighbouring cells so it has no cracks: each corner takes
        // the shallowest of the cells round it.
        if (sky)
        {
            var corner = new Dictionary<(int, int), int>();
            foreach (var c in a.Cells)
            {
                var s = L.Grid.Ground(c.I, c.J) ?? L.Grid.At(c.I, c.J)[0];
                int[] di = [0, 1, 1, 0], dj = [0, 0, 1, 1];
                for (int k = 0; k < 4; k++)
                {
                    var key = (c.I + di[k], c.J + dj[k]);
                    corner[key] = Math.Max(corner.GetValueOrDefault(key, int.MinValue), s.Bottom![k]);
                }
            }
            foreach (var c in a.Cells)
            {
                var s = L.Grid.At(c.I, c.J)[0];
                int[] di = [0, 1, 1, 0], dj = [0, 0, 1, 1];
                for (int k = 0; k < 4; k++) s.Bottom![k] = corner[(c.I + di[k], c.J + dj[k])];
            }
        }
        // A terrace on big islands: a raised middle reached by a ramp.
        if (a.Cells.Count >= 40 && a.Kind != "start" && L.Random.Chance(0.6))
        {
            var inner = a.Cells.Where(c => depth[c] >= 3).ToList();
            if (inner.Count >= 9)
            {
                var top = L.NewArea("terrace", a.Z + L.Random.Pick<int>([96, 144]));
                top.Cells.AddRange(inner); top.Changed(); top.Seed = inner.MinBy(c => Math.Pow(c.I - a.Centre.I, 2) + Math.Pow(c.J - a.Centre.J, 2));
                foreach (var c in inner) { var s = L.Grid.At(c.I, c.J)[0]; s.SetTop(top.Z); s.Area = top.Id; }
                a.Cells.RemoveAll(top.Set.Contains); a.Changed();
                if (!L.TryRamp(top, a, (int)SpyroMoves.DesignRampRise, 2, SurfaceRole.Path, "ramp"))
                {
                    top.Z = a.Z + 48;
                    foreach (var c in inner) L.Grid.At(c.I, c.J)[0].SetTop(top.Z);
                    L.Links.Add(new(a.Id, top.Id, "ledge", L.CellTop(a.Seed.I, a.Seed.J), L.CellTop(top.Seed.I, top.Seed.J)));
                }
                if (L.Grid.At(top.Seed.I, top.Seed.J)[0].Bottom is { } && L.Style == LevelStyle.SkyIslands)
                    foreach (var c in inner) { var s = L.Grid.At(c.I, c.J)[0]; for (int k = 0; k < 4; k++) s.Bottom![k] = Math.Min(s.Bottom[k], top.Z - 200); }
            }
        }
    }

    /// <summary>The gap between two islands in cells (0: touching) and the nearest pair of their cells.</summary>
    private static (int D, (int I, int J) A, (int I, int J) B) Gap(Area a, Area b)
    {
        int best = int.MaxValue; (int, int) pa = default, pb = default;
        foreach (var x in a.Cells)
            foreach (var y in b.Cells)
            {
                int d = Math.Max(Math.Abs(x.I - y.I), Math.Abs(x.J - y.J)) - 1;
                if (d < best) { best = d; pa = x; pb = y; }
            }
        return (best, pa, pb);
    }

    /// <summary>Joins two islands both ways: a straight bridge where one fits (axis-aligned, sloping no more than a walking
    /// ramp), else stepping stones a jump apart.</summary>
    private static void Connect(Layout L, Area a, Area b, bool sky)
    {
        if (L.Features.Bridges && TryBridge(L, a, b, sky)) return;
        Stones(L, a, b, sky);
    }

    private static bool TryBridge(Layout L, Area a, Area b, bool sky)
    {
        // Aligned pairs: an edge cell of each with a straight run of open cells between them.
        List<((int I, int J) A, (int I, int J) B, int Di, int Dj, int Len)> runs = [];
        foreach (var x in a.Cells)
            foreach (var (di, dj) in Layout.Axes)
            {
                for (int t = 1; t <= 12; t++)
                {
                    var c = (x.I + di * t, x.J + dj * t);
                    if (b.Set.Contains(c)) { if (t > 1) runs.Add((x, c, di, dj, t - 1)); break; }
                    if (!L.Grid.Inside(c.Item1, c.Item2) || L.Areas.Any(o => o.Set.Contains(c)) || L.Reserved.Contains(c)) break;
                }
            }
        // Two cells wide: the run beside it must be open too.
        runs = [.. runs.Where(r => Math.Abs(a.Z - b.Z) <= r.Len * SpyroMoves.DesignRampRise)
            .Where(r => Enumerable.Range(1, r.Len).All(t => { var c = (r.A.I + r.Di * t + r.Dj, r.A.J + r.Dj * t + r.Di); return L.Grid.Inside(c.Item1, c.Item2) && !L.Areas.Any(o => o.Set.Contains(c)) && !L.Reserved.Contains(c); })
                && a.Set.Contains((r.A.I + r.Dj, r.A.J + r.Di)) && b.Set.Contains((r.B.I + r.Dj, r.B.J + r.Di)))];
        if (runs.Count == 0) return false;
        var run = runs.MinBy(r => r.Len + L.Random.NextDouble());
        // The deck slopes evenly from one island's height to the other's.
        int len = run.Len;
        for (int t = 1; t <= len; t++)
            for (int w = 0; w < 2; w++)
            {
                var c = (run.A.I + run.Di * t + run.Dj * w, run.A.J + run.Dj * t + run.Di * w);
                var deck = new Slab { TopRole = SurfaceRole.Wood, SideRole = SurfaceRole.Wood, BottomRole = SurfaceRole.Wood, SideTopRole = null, Required = true, Area = a.Id };
                // The deck meets island a's edge at a's height and island b's edge at b's: its cell t runs from (t-1)/len to t/len of the way.
                int near = (int)Math.Round(a.Z + (b.Z - a.Z) * (t - 1) / (float)len), far = (int)Math.Round(a.Z + (b.Z - a.Z) * t / (float)len);
                for (int k = 0; k < 4; k++)
                {
                    var (ci, cj) = (k is 1 or 2 ? 1 : 0, k is 2 or 3 ? 1 : 0);
                    int along = run.Di != 0 ? (run.Di > 0 ? ci : 1 - ci) : (run.Dj > 0 ? cj : 1 - cj);
                    deck.Top[k] = along == 0 ? near : far;
                }
                deck.Bottom = [.. deck.Top.Select(z => z - 24)];
                if (sky) L.Grid.Set(c.Item1, c.Item2, deck); else L.Grid.Add(c.Item1, c.Item2, deck);
                L.Reserved.Add(c);
            }
        L.Links.Add(new(a.Id, b.Id, "bridge", L.CellTop(run.A.I, run.A.J), L.CellTop(run.B.I, run.B.J)));
        L.Count("bridge");
        return true;
    }

    /// <summary>A chain of stepping stones (2 x 2 cells) from island a to island b in straight runs along x, then y (or y,
    /// then x): one cell of gap between stones, a held jump apart, each at most a ledge higher than the last, so Spyro
    /// can hop across either way.</summary>
    private static void Stones(Layout L, Area a, Area b, bool sky)
    {
        var (_, ca, cb) = Gap(a, b);
        bool Open((int I, int J) c) => L.Grid.Inside(c.I, c.J) && !L.Areas.Any(o => o.Set.Contains(c)) && !L.Reserved.Contains(c);
        static (int, int)[] Cover((int I, int J) s) => [(s.I, s.J), (s.I + 1, s.J), (s.I, s.J + 1), (s.I + 1, s.J + 1)];
        // Stones (lower-left cell; each covers 2 x 2 cells) on a lattice 3 cells apart: one cell of gap between stones.
        // The first is a gap away from island a's edge; each next one steps along the axis with the farther way to go,
        // until island b is within a cell. A chain is built whole or not at all (both first directions are tried).
        List<(int I, int J)>? Chain(bool alongX, int shift)
        {
            var stones = new List<(int I, int J)>();
            int sx = Math.Sign(cb.I - ca.I), sy = Math.Sign(cb.J - ca.J);
            if (alongX && sx == 0 || !alongX && sy == 0) return null;
            var cur = alongX ? (ca.I + (sx > 0 ? 2 : -3), ca.J - shift) : (ca.I - shift, ca.J + (sy > 0 ? 2 : -3));
            stones.Add(cur);
            for (int guard = 0; guard < 40; guard++)
            {
                double dx = cb.I + 0.5 - (cur.Item1 + 1), dy = cb.J + 0.5 - (cur.Item2 + 1);
                if (Math.Abs(dx) <= 2.5 && Math.Abs(dy) <= 2.5) break;
                bool stepX = Math.Abs(dx) > 2.5 && (Math.Abs(dx) >= Math.Abs(dy) || Math.Abs(dy) <= 2.5);
                cur = stepX ? (cur.Item1 + 3 * Math.Sign(dx), cur.Item2) : (cur.Item1, cur.Item2 + 3 * Math.Sign(dy));
                stones.Add(cur);
            }
            if (!stones.All(st => Cover(st).All(Open))) return null;
            // The last stone must be within a jump of island b (a cell of gap at most, edge to edge).
            var last = stones[^1];
            bool near = b.Cells.Any(c => Cover(last).Any(x => Math.Max(Math.Abs(x.Item1 - c.I), Math.Abs(x.Item2 - c.J)) <= 2 && (x.Item1 == c.I || x.Item2 == c.J)));
            return near ? stones : null;
        }
        bool first = Math.Abs(cb.I - ca.I) >= Math.Abs(cb.J - ca.J);
        var chain = Chain(first, 0) ?? Chain(first, 1) ?? Chain(!first, 0) ?? Chain(!first, 1);
        if (chain == null) { L.Notes.Add($"No stepping stones fitted between islands {a.Id} and {b.Id}."); return; }
        var placed = new List<((int I, int J) Corner, int Z)>();
        int count = chain.Count;
        int dz = b.Z - a.Z;
        for (int k = 0; k < count; k++)
        {
            var corner = chain[k];
            int z = (int)Math.Round(a.Z + dz * (k + 1) / (float)(count + 1));
            foreach (var c in Cover(corner))
            {
                var stone = L.GroundSlab(z, null, SurfaceRole.Rock);
                stone.Required = true; stone.Area = a.Id; stone.SideRole = SurfaceRole.Rock; stone.SideTopRole = null;
                if (sky) { stone.SetBottom(z - 80); stone.BottomRole = SurfaceRole.Underside; }
                L.Grid.Set(c.Item1, c.Item2, stone);
                L.Reserved.Add(c);
            }
            placed.Add((corner, z));
        }
        // Hops for the in-game checks: between the two cells that face each other across each one-cell gap (straight, 240
        // apart), island edge to first stone, stone to stone, last stone to island.
        (int I, int J)? Facing(IEnumerable<(int I, int J)> from, IEnumerable<(int I, int J)> to, out (int I, int J) other)
        {
            other = default;
            foreach (var c in from)
                foreach (var x in to)
                    if ((c.I == x.I && Math.Abs(c.J - x.J) == 2) || (c.J == x.J && Math.Abs(c.I - x.I) == 2)) { other = x; return c; }
            return null;
        }
        var floors = new List<IEnumerable<(int I, int J)>> { a.Cells };
        floors.AddRange(placed.Select(p => (IEnumerable<(int I, int J)>)Cover(p.Corner).Select(c => (c.Item1, c.Item2))));
        floors.Add(b.Cells);
        for (int k = 0; k + 1 < floors.Count; k++)
            if (Facing(floors[k], floors[k + 1], out var to) is { } from)
                L.Links.Add(new(a.Id, b.Id, "hop", L.CellTop(from.I, from.J), L.CellTop(to.I, to.J)));
        L.Links.Add(new(a.Id, b.Id, "stepping stones", L.CellTop(ca.I, ca.J), L.CellTop(cb.I, cb.J)));
        L.Count("stepping stones", placed.Count);
    }

    private static void Decorate(Layout L, bool sky)
    {
        var rnd = L.Random; var grid = L.Grid;
        double p1 = rnd.NextDouble() * 6, p2 = rnd.NextDouble() * 6;
        foreach (var a in L.Areas)
        {
            foreach (var c in a.Cells)
                if (grid.Floor(c.I, c.J) is { TopRole: SurfaceRole.Ground } g && Math.Sin(c.I * 0.8 + p1) * Math.Cos(c.J * 0.7 + p2) > 0.5) g.TopRole = SurfaceRole.GroundAlt;
            L.GemSpots.Add(L.CellTop(a.Seed.I, a.Seed.J));
            if (a.Kind == "start") continue;
            // A hut on a large island, or rocks to jump on.
            var free = a.Cells.Where(c => !L.Reserved.Contains(c) && grid.At(c.I, c.J).Count == 1 && grid.At(c.I, c.J)[0] is { Flat: true, Kind: null } s && s.Top[0] == a.Z).ToHashSet();
            var inner = Layout.EdgeDistance(a.Set).Where(p => p.Value >= 2 && free.Contains(p.Key)).Select(p => p.Key).ToList();
            if (inner.Count > 0 && rnd.Chance(0.6))
            {
                var c = rnd.Pick(inner);
                var s = grid.At(c.I, c.J)[0];
                int h = rnd.Chance(0.6) ? 48 : rnd.Next(3, 7) * 48;
                s.SetTop(a.Z + h); s.TopRole = h <= 64 ? SurfaceRole.Rock : SurfaceRole.Ground; s.SideRole = SurfaceRole.Rock; s.SideTopRole = h <= 64 ? null : SurfaceRole.CliffTop; s.Required = false;
                if (s.Bottom != null) for (int k = 0; k < 4; k++) s.Bottom[k] = Math.Min(s.Bottom[k], a.Z - 72);
                if (h <= 64) { L.GemSpots.Add(L.CellTop(c.I, c.J)); L.Count("jump-on rock"); } else L.Count("rock spire");
            }
        }
        // A secret high platform: floating above an island, reached by gliding from the highest island.
        if (L.Features.Secrets && L.Areas.Count >= 3)
        {
            var high = L.Areas.MaxBy(a => a.Z)!;
            var target = L.Areas.Where(a => a != high && high.Z - a.Z >= 160).MinBy(a => Gap(high, a).D);
            if (target != null)
            {
                var (d, ch, ct) = Gap(high, target);
                int z = high.Z - 64;
                // Halfway, over the gap or the target island's edge.
                var c = ((ch.I + ct.I) / 2, (ch.J + ct.J) / 2);
                if (grid.Inside(c.Item1, c.Item2) && !L.Reserved.Contains(c) && (d + 1) * LevelGrid.Cell / 2 <= SpyroMoves.GlideReach(64, 0.3f))
                {
                    var plat = new Slab { TopRole = SurfaceRole.Paving, SideRole = SurfaceRole.Rock, SideTopRole = null, BottomRole = SurfaceRole.Underside, Area = -1 };
                    plat.SetTop(z); plat.SetBottom(z - 80);
                    var under = grid.At(c.Item1, c.Item2);
                    if (under.Count == 0 || under[^1].MaxTop < z - 200)
                    {
                        grid.Add(c.Item1, c.Item2, plat);
                        L.GemSpots.Add(L.Point(c.Item1 + 0.5f, c.Item2 + 0.5f, z));
                        L.Links.Add(new(high.Id, target.Id, "glide to secret platform", L.CellTop(ch.I, ch.J), L.Point(c.Item1 + 0.5f, c.Item2 + 0.5f, z)));
                        L.Count("secret platform");
                    }
                }
            }
        }
    }
}
