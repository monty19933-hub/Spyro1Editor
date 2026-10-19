using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Generation;

/// <summary>Valley, Canyon and Castle levels: one walled-in playing field cut into areas at different heights. The areas
/// are joined along a random tree (open ground, a ledge Spyro jumps up, a ramp, or a steep ramp only charging climbs);
/// cliffs, castle walls or canyon walls surround the field.</summary>
internal static class LayoutValley
{
    public const int Ground0 = 640;

    public static void Build(Layout L, int n)
    {
        var rnd = L.Random;
        var field = L.Style switch
        {
            LevelStyle.Canyon => CanyonField(L, n, out var canyonAreas) is var f ? (f, canyonAreas) : default,
            LevelStyle.Castle => CastleField(L, n, out var courtyards) is var c ? (c, courtyards) : default,
            _ => (ValleyField(L, n), null),
        };
        var cells = field.Item1;
        var groups = field.Item2 ?? Partition(L, cells, Math.Max(4, n / 6 + rnd.Next(0, 3)));
        foreach (var g in groups.Where(g => g.Count > 0))
        {
            var a = L.NewArea(L.Style == LevelStyle.Castle ? "courtyard" : "field", Ground0);
            a.Cells.AddRange(Layout.Sorted(g)); a.Changed();
            a.Seed = a.Cells.MinBy(c => Math.Pow(c.I - a.Centre.I, 2) + Math.Pow(c.J - a.Centre.J, 2));
        }
        // Areas beside each other (sharing at least two cell sides).
        var owner = new Dictionary<(int, int), Area>();
        foreach (var a in L.Areas) foreach (var c in a.Cells) owner[c] = a;
        var border = new Dictionary<(int, int), int>();
        foreach (var a in L.Areas)
            foreach (var c in a.Cells)
                foreach (var nb in Layout.Neighbours4(c))
                    if (owner.TryGetValue(nb, out var b) && b.Id > a.Id) border[(a.Id, b.Id)] = border.GetValueOrDefault((a.Id, b.Id)) + 1;
        var adjacent = L.Areas.ToDictionary(a => a.Id, _ => new List<int>());
        foreach (var ((x, y), count) in border.Where(p => p.Value >= 2)) { adjacent[x].Add(y); adjacent[y].Add(x); }

        // The start: an area at the field's edge with room for the arrival pad (4 x 4 flat cells).
        var start = L.Style == LevelStyle.Canyon ? L.Areas[0]
            : L.Areas.Where(a => Layout.Square(a.Set, 5, a.Centre) != null).OrderBy(a => a.Centre.J + rnd.NextDouble() * n * 0.3).FirstOrDefault() ?? L.Areas.MaxBy(a => a.Cells.Count)!;
        L.StartArea = start.Id;
        start.Kind = "start";
        var pad = Layout.Square(start.Set, 4, start.Centre) ?? start.Seed;
        L.StartCell = (pad.I + 2, pad.J + 2);
        for (int a = -1; a <= 4; a++) for (int b = -1; b <= 4; b++) L.Reserved.Add((pad.I + a, pad.J + b));

        foreach (var a in L.Areas) foreach (var c in a.Cells) L.Grid.Set(c.I, c.J, L.GroundSlab(Ground0, a));

        // Heights and joins along a random tree from the start.
        var placed = new HashSet<int> { start.Id };
        var frontier = new List<(int Parent, int Child)>();
        foreach (int b in adjacent[start.Id]) frontier.Add((start.Id, b));
        start.Z = Ground0;
        int low = Ground0 - 192, high = Ground0 + 576;
        while (frontier.Count > 0)
        {
            int k = rnd.Next(frontier.Count);
            var (p, ch) = frontier[k]; frontier.RemoveAt(k);
            if (placed.Contains(ch)) continue;
            placed.Add(ch);
            var parent = L.Areas[p]; var child = L.Areas[ch];
            int dz = L.Style == LevelStyle.Canyon
                ? rnd.Pick<int>([(0, 1), (48, 1), (96, 2), (144, 2), (192, 1), (-96, 1.5), (-144, 1)])
                : rnd.Pick<int>([(0, 1.2), (48, 1), (96, 1.5), (144, 1.5), (192, 1), (240, 0.6), (-96, 1), (-48, 0.6), (-144, 0.6)]);
            child.Z = Math.Clamp(parent.Z + dz, low, high);
            foreach (var c in child.Cells) L.SetGround(c.I, c.J, child.Z);
            Join(L, parent, child);
            foreach (int b in adjacent[ch]) if (!placed.Contains(b)) frontier.Add((ch, b));
        }
        // Areas no tree reached (cut off by the partition) are joined to the nearest placed area's height.
        foreach (var a in L.Areas.Where(a => !placed.Contains(a.Id))) { a.Z = start.Z; foreach (var c in a.Cells) L.SetGround(c.I, c.J, a.Z); }
        // Every other pair of neighbours a jump apart counts as a ledge; the rest stay cliffs.
        foreach (var ((x, y), count) in border.Where(p => p.Value >= 2))
        {
            int d = Math.Abs(L.Areas[x].Z - L.Areas[y].Z);
            if (d > 0 && d <= SpyroMoves.DesignLedge && !L.Links.Any(l => l.A == x && l.B == y || l.A == y && l.B == x))
                AddLedgeLink(L, L.Areas[x], L.Areas[y]);
        }

        RimAround(L, cells);
        Decorate(L);
    }

    /// <summary>Joins a newly placed area to its parent: open ground, a ledge, a ramp, or (supercharge paths) a ramp only
    /// charging climbs. A height no ramp fits is turned into a ledge.</summary>
    private static void Join(Layout L, Area parent, Area child)
    {
        int dz = child.Z - parent.Z;
        if (dz == 0) { L.Links.Add(new(parent.Id, child.Id, "walk", L.CellTop(parent.Seed.I, parent.Seed.J), L.CellTop(child.Seed.I, child.Seed.J))); L.Count("open ground"); return; }
        if (Math.Abs(dz) <= SpyroMoves.DesignLedge) { AddLedgeLink(L, parent, child); return; }
        var upper = dz > 0 ? child : parent; var lower = dz > 0 ? parent : child;
        bool charge = L.Features.ChargePaths && Math.Abs(dz) <= 192 && L.Random.Chance(0.4);
        var role = charge ? SurfaceRole.Charge : L.Random.Chance(0.5) ? SurfaceRole.Path : SurfaceRole.Dirt;
        foreach (int width in new[] { L.Random.Next(2, 4), 2, 1 })
        {
            if (L.TryRamp(upper, lower, charge ? (int)SpyroMoves.DesignChargeRise : (int)SpyroMoves.DesignRampRise, width, role, charge ? "charge ramp" : "ramp"))
                return;
            if (charge && L.TryRamp(upper, lower, (int)SpyroMoves.DesignRampRise, width, SurfaceRole.Dirt, "ramp")) return;
        }
        // No room for a ramp: make the child a ledge above or below its parent.
        child.Z = parent.Z + Math.Sign(dz) * 48;
        foreach (var c in child.Cells) L.SetGround(c.I, c.J, child.Z);
        AddLedgeLink(L, parent, child);
        L.Notes.Add($"Area {child.Id} became a ledge: no ramp fitted.");
    }

    private static void AddLedgeLink(Layout L, Area a, Area b)
    {
        // A border cell pair: where Spyro jumps up or drops down.
        foreach (var c in a.Cells)
            foreach (var nb in Layout.Neighbours4(c))
                if (b.Set.Contains(nb) && !L.Reserved.Contains(c) && !L.Reserved.Contains(nb))
                {
                    L.Links.Add(new(a.Id, b.Id, "ledge", L.Point(c.I + 0.5f, c.J + 0.5f, a.Z), L.Point(nb.I + 0.5f, nb.J + 0.5f, b.Z)));
                    L.Reserved.Add(c); L.Reserved.Add(nb);
                    L.Count("ledge");
                    return;
                }
    }

    private static HashSet<(int I, int J)> ValleyField(Layout L, int n)
    {
        float c = n / 2f;
        var cells = L.Blob(c + L.Random.Next(-2, 3), c + L.Random.Next(-2, 3), n * 0.36f, 0.3f);
        // A second lobe or two makes the valley less round.
        for (int k = L.Random.Next(1, 3); k > 0; k--)
        {
            double t = L.Random.NextDouble() * Math.PI * 2;
            cells.AddRange(L.Blob(c + (float)Math.Cos(t) * n * 0.18f, c + (float)Math.Sin(t) * n * 0.18f, n * 0.22f, 0.3f));
        }
        return [.. cells.Where(x => x.I >= 4 && x.J >= 4 && x.I < n - 4 && x.J < n - 4)];
    }

    /// <summary>A winding trail from one side to the other, with side pockets; its areas run along it.</summary>
    private static HashSet<(int I, int J)> CanyonField(Layout L, int n, out List<List<(int I, int J)>> areas)
    {
        var rnd = L.Random;
        int m = 5;
        List<(float X, float Y)> points = [];
        for (int k = 0; k <= m; k++)
        {
            float t = k / (float)m;
            float y = 6 + t * (n - 12);
            float x = k == 0 || k == m ? n / 2f + rnd.Next(-n / 6, n / 6 + 1) : 7 + (float)rnd.NextDouble() * (n - 14);
            points.Add((x, y));
        }
        // Distance to the trail and how far along it each cell is.
        var cells = new HashSet<(int I, int J)>();
        var along = new Dictionary<(int, int), float>();
        float total = 0;
        var lengths = new List<float> { 0 };
        for (int k = 1; k < points.Count; k++) { total += Dist(points[k - 1], points[k]); lengths.Add(total); }
        for (int i = 3; i < n - 3; i++)
            for (int j = 3; j < n - 3; j++)
            {
                float best = float.MaxValue, at = 0;
                for (int k = 1; k < points.Count; k++)
                {
                    var (d, t) = SegmentDistance((i + 0.5f, j + 0.5f), points[k - 1], points[k]);
                    if (d < best) { best = d; at = lengths[k - 1] + t * Dist(points[k - 1], points[k]); }
                }
                float width = 2.6f + 1.3f * MathF.Sin(at * 0.35f + 1.7f) + 0.6f * MathF.Sin(at * 0.9f);
                if (best <= width) { cells.Add((i, j)); along[(i, j)] = at; }
            }
        int segments = Math.Max(4, (int)(total / 8));
        areas = [.. Enumerable.Range(0, segments).Select(_ => new List<(int I, int J)>())];
        foreach (var c in cells) areas[Math.Min(segments - 1, (int)(along[c] / total * segments))].Add(c);
        // Side pockets, each its own area.
        for (int k = rnd.Next(2, 5); k > 0; k--)
        {
            var on = rnd.Pick(Layout.Sorted(cells));
            double t = rnd.NextDouble() * Math.PI * 2;
            float r = 2 + (float)rnd.NextDouble() * 1.5f;
            var pocket = L.Blob(on.I + (float)Math.Cos(t) * (r + 2), on.J + (float)Math.Sin(t) * (r + 2), r, 0.25f)
                .Where(c => c.I >= 3 && c.J >= 3 && c.I < n - 3 && c.J < n - 3 && !cells.Contains(c)).ToList();
            if (pocket.Count < 8) continue;
            foreach (var c in pocket) cells.Add(c);
            areas.Add(pocket);
        }
        return cells;
    }

    /// <summary>Rectangular courtyards side by side.</summary>
    private static HashSet<(int I, int J)> CastleField(Layout L, int n, out List<List<(int I, int J)>> areas)
    {
        var rnd = L.Random;
        areas = [];
        var cells = new HashSet<(int I, int J)>();
        int x0 = 5, y0 = 5, x1 = n - 5, y1 = n - 5;
        // Split the inner rectangle recursively into courtyards of 5-12 cells a side.
        var queue = new List<(int X0, int Y0, int X1, int Y1)> { (x0, y0, x1, y1) };
        while (queue.Count > 0)
        {
            var r = queue[^1]; queue.RemoveAt(queue.Count - 1);
            int w = r.X1 - r.X0, h = r.Y1 - r.Y0;
            bool splitX = w >= h ? w > 12 || w > 8 && rnd.Chance(0.5) : false, splitY = !splitX && (h > 12 || h > 8 && rnd.Chance(0.5));
            if (splitX) { int at = r.X0 + rnd.Next(4, w - 3); queue.Add((r.X0, r.Y0, at, r.Y1)); queue.Add((at, r.Y0, r.X1, r.Y1)); continue; }
            if (splitY) { int at = r.Y0 + rnd.Next(4, h - 3); queue.Add((r.X0, r.Y0, r.X1, at)); queue.Add((r.X0, at, r.X1, r.Y1)); continue; }
            // Some courtyards are left out (their space becomes castle wall).
            if (areas.Count > 2 && rnd.Chance(0.15)) continue;
            var list = new List<(int I, int J)>();
            for (int i = r.X0; i < r.X1; i++) for (int j = r.Y0; j < r.Y1; j++) { list.Add((i, j)); cells.Add((i, j)); }
            areas.Add(list);
        }
        return cells;
    }

    /// <summary>Voronoi areas over the field, seeded far apart, with wavy borders; tiny or broken pieces are merged.</summary>
    internal static List<List<(int I, int J)>> Partition(Layout L, HashSet<(int I, int J)> cells, int count)
    {
        var rnd = L.Random;
        var list = Layout.Sorted(cells);
        var seeds = new List<(int I, int J)> { rnd.Pick(list) };
        while (seeds.Count < count)
            seeds.Add(list.MaxBy(c => seeds.Min(s => Math.Pow(s.I - c.I, 2) + Math.Pow(s.J - c.J, 2)) * (0.75 + 0.5 * rnd.NextDouble())));
        double p1 = rnd.NextDouble() * 6, p2 = rnd.NextDouble() * 6;
        var of = new Dictionary<(int, int), int>();
        foreach (var c in list)
        {
            double wave = 1.4 * Math.Sin(c.I * 0.45 + p1) * Math.Cos(c.J * 0.38 + p2);
            of[c] = Enumerable.Range(0, seeds.Count).MinBy(k => Math.Sqrt(Math.Pow(seeds[k].I - c.I, 2) + Math.Pow(seeds[k].J - c.J, 2)) + wave * (k % 2 == 0 ? 1 : -1));
        }
        for (int pass = 0; pass < 2; pass++)
            foreach (var c in list)
            {
                var votes = Layout.Neighbours4(c).Where(of.ContainsKey).GroupBy(nb => of[nb]).Select(g => (g.Key, g.Count())).ToList();
                if (votes.Count == 0) continue;
                var best = votes.MaxBy(v => v.Item2);
                if (best.Item2 >= 3 && best.Key != of[c]) of[c] = best.Key;
            }
        // Keep each area's largest connected piece; give the other pieces to a neighbour.
        for (int round = 0; round < 3; round++)
        {
            foreach (var group in of.GroupBy(p => p.Value).ToList())
            {
                var mine = group.Select(p => p.Key).ToHashSet();
                var pieces = new List<HashSet<(int, int)>>();
                var seen = new HashSet<(int, int)>();
                foreach (var c in Layout.Sorted(mine))
                {
                    if (seen.Contains(c)) continue;
                    var piece = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue(c); seen.Add(c);
                    while (q.Count > 0) { var x = q.Dequeue(); piece.Add(x); foreach (var nb in Layout.Neighbours4(x)) if (mine.Contains(nb) && seen.Add(nb)) q.Enqueue(nb); }
                    pieces.Add(piece);
                }
                var keep = pieces.MaxBy(p => p.Count)!;
                foreach (var piece in pieces.Where(p => p != keep || p.Count < 12))
                    foreach (var c in Layout.Sorted(piece))
                    {
                        var other = Layout.Neighbours4(c).Where(nb => of.ContainsKey(nb) && !piece.Contains(nb)).Select(nb => of[nb]).FirstOrDefault(-1);
                        if (other >= 0) of[c] = other;
                    }
            }
        }
        return [.. of.GroupBy(p => p.Value).Select(g => g.Select(p => p.Key).ToList()).Where(g => g.Count > 0)];
    }

    /// <summary>Cliffs (or castle walls) round the field: up to three cells deep, well above every area beside them (more
    /// than a held jump and glide could clear), with a broken skyline.</summary>
    private static void RimAround(Layout L, HashSet<(int I, int J)> field)
    {
        var grid = L.Grid;
        var depth = new Dictionary<(int, int), int>();
        var q = new Queue<(int, int)>();
        foreach (var c in field) { depth[c] = 0; q.Enqueue(c); }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (depth[c] >= 3) continue;
            foreach (var nb in new[] { (c.Item1 + 1, c.Item2), (c.Item1 - 1, c.Item2), (c.Item1, c.Item2 + 1), (c.Item1, c.Item2 - 1),
                (c.Item1 + 1, c.Item2 + 1), (c.Item1 - 1, c.Item2 - 1), (c.Item1 + 1, c.Item2 - 1), (c.Item1 - 1, c.Item2 + 1) })
                if (grid.Inside(nb.Item1, nb.Item2) && !depth.ContainsKey(nb)) { depth[nb] = depth[c] + 1; q.Enqueue(nb); }
        }
        int top = L.Areas.Max(a => a.Z) + 224;
        bool castle = L.Style == LevelStyle.Castle;
        double p = L.Random.NextDouble() * 6;
        foreach (var (c, d) in depth.Where(x => x.Value > 0))
        {
            int z = castle ? top + (d == 1 && (c.Item1 + c.Item2) % 2 == 0 ? 40 : 0)
                : top + 32 * (int)Math.Round(2 + 1.6 * Math.Sin(c.Item1 * 0.5 + p) + 1.3 * Math.Cos(c.Item2 * 0.42 + p * 0.7) + d);
            var s = L.GroundSlab(z, null, castle ? SurfaceRole.Paving : SurfaceRole.Ground);
            if (castle) { s.SideRole = SurfaceRole.Brick; s.SideTopRole = null; }
            if (L.Theme.Key is "desert" or "volcano" or "winter") s.TopRole = SurfaceRole.Rock;
            grid.Set(c.Item1, c.Item2, s);
        }
    }

    /// <summary>Paths, ground patches, ponds, chasms with a bridge or a glide gap, rock pillars, houses, a cave, secret
    /// ledges and gem spots.</summary>
    private static void Decorate(Layout L)
    {
        var rnd = L.Random; var grid = L.Grid;
        // Ground patches of the second ground texture.
        double p1 = rnd.NextDouble() * 6, p2 = rnd.NextDouble() * 6;
        foreach (var a in L.Areas)
            foreach (var c in a.Cells)
                if (grid.Ground(c.I, c.J) is { TopRole: SurfaceRole.Ground } g && Math.Sin(c.I * 0.7 + p1) * Math.Cos(c.J * 0.6 + p2) > 0.55)
                    g.TopRole = SurfaceRole.GroundAlt;
        if (L.Style == LevelStyle.Castle)
            foreach (var a in L.Areas.Where(a => a.Kind != "start" && rnd.Chance(0.6)))
                foreach (var c in a.Cells) if (grid.Ground(c.I, c.J) is { TopRole: SurfaceRole.Ground or SurfaceRole.GroundAlt } g) g.TopRole = SurfaceRole.Paving;
        // Paths from each area's centre to its links.
        foreach (var a in L.Areas)
            foreach (var link in L.Links.Where(l => l.A == a.Id || l.B == a.Id))
            {
                var p = link.A == a.Id ? link.From : link.To;
                var cell = ((int)((p.X - grid.X(0)) / LevelGrid.Cell), (int)((p.Y - grid.Y(0)) / LevelGrid.Cell));
                if (rnd.Chance(0.7)) L.PaintPath(a.Seed, cell, a, SurfaceRole.Path);
            }
        // Ramp ends and the start pad are kept clear; so are cells next to ramps.
        foreach (var c in L.Reserved.ToList()) foreach (var nb in Layout.Neighbours4(c)) L.Reserved.Add(nb);

        var others = L.Areas.Where(a => a.Kind != "start").ToList();
        rnd.Shuffle(others);
        // Chasms: a channel across an area, crossed by a bridge, stepping stones or a glide.
        if (L.Features.Hazards || L.Features.Glides || L.Features.Bridges)
            foreach (var a in others.Where(a => a.Cells.Count >= 40).Take(L.Features.Glides && L.Features.Bridges ? 2 : 1))
                Chasm(L, a);
        // Ponds of the theme's liquid.
        if (L.Features.Hazards)
            foreach (var a in others.Where(a => a.Cells.Count >= 30).Take(rnd.Next(1, 3)))
                Pond(L, a);
        // Rock pillars: low ones to jump on (gems on top), tall ones as scenery.
        foreach (var a in L.Areas.Where(a => a.Cells.Count >= 24))
            for (int k = rnd.Next(0, 3); k > 0; k--) Pillar(L, a, rnd.Chance(0.55) ? rnd.Pick<int>([32, 48]) : rnd.Next(4, 9) * 40);
        // Houses (gabled) in meadow, castle and swamp themes.
        if (L.Theme.Key is "meadow" or "castle" or "swamp" or "desert" || L.Style == LevelStyle.Castle)
            foreach (var a in others.Where(a => a.Cells.Count >= 36).Take(rnd.Next(1, 4)))
                House(L, a);
        // A cave over one area.
        if (L.Features.Caves)
        {
            var cave = others.Where(a => a.Cells.Count is >= 16 and <= 90 && a.Kind == "field").MinBy(_ => rnd.NextDouble());
            if (cave != null) Cover(L, cave);
        }
        // Secret ledges reached by gliding from higher ground.
        if (L.Features.Secrets || L.Features.Glides)
            for (int k = rnd.Next(1, 4); k > 0; k--) SecretLedge(L);
        // A supercharge leap: charge down the arrows, jump at the lip, land on a far platform out of a glide's reach.
        if (L.Features.ChargePaths)
            for (int k = 0; k < 2 && SuperchargeLeap(L); k++) { }
        // Gem spots: area centres and along the way.
        foreach (var a in L.Areas)
        {
            L.GemSpots.Add(L.CellTop(a.Seed.I, a.Seed.J));
            foreach (var c in a.Cells.Where((_, k) => k % 9 == 4).Take(5)) if (grid.Ground(c.I, c.J) is { Kind: null }) L.GemSpots.Add(L.CellTop(c.I, c.J));
        }
    }

    /// <summary>An area's cells a feature may change: flat ground at the area's height, nothing on it but a roof well
    /// overhead, not reserved.</summary>
    internal static List<(int I, int J)> Free(Layout L, Area a) =>
        [.. a.Cells.Where(c => !L.Reserved.Contains(c) && L.Grid.Ground(c.I, c.J) is { Flat: true, Kind: null } g && g.Top[0] == a.Z &&
            L.Grid.At(c.I, c.J).Skip(1).All(o => o.Bottom is { } b && b.Min() >= a.Z + 200))];

    internal static void Pond(Layout L, Area a)
    {
        var free = Free(L, a).ToHashSet();
        var inner = Layout.EdgeDistance(a.Set).Where(p => p.Value >= 3 && free.Contains(p.Key)).Select(p => p.Key).ToList();
        if (inner.Count == 0) return;
        var c = L.Random.Pick(inner);
        var pond = L.Blob(c.I + 0.5f, c.J + 0.5f, 1.2f + (float)L.Random.NextDouble() * 1.4f, 0.25f).Where(free.Contains).ToList();
        if (pond.Count < 2 || pond.Count > a.Cells.Count / 4) return;
        foreach (var p in pond)
        {
            var g = L.Grid.Ground(p.I, p.J)!;
            g.SetTop(a.Z - 40); g.TopRole = L.Liquid; g.Kind = L.LiquidKind; g.Required = false; g.SideRole = SurfaceRole.Dirt; g.SideTopRole = null;
            L.Reserved.Add(p);
        }
        // The bank round it is sand or dirt.
        foreach (var p in pond)
            foreach (var nb in Layout.Neighbours4(p))
                if (a.Set.Contains(nb) && !pond.Contains(nb) && L.Grid.Ground(nb.I, nb.J) is { Kind: null } bank && L.Grid.At(nb.I, nb.J).Count == 1)
                { bank.TopRole = SurfaceRole.Sand; bank.SideTopRole = null; bank.SideRole = SurfaceRole.Dirt; }
        L.Count(L.Liquid == SurfaceRole.Water ? "pond" : L.Liquid == SurfaceRole.Lava ? "lava pool" : "ooze pool");
    }

    /// <summary>A two-cell channel straight across an area (deadly liquid, or a deep chasm with deadly ground at the bottom)
    /// that splits it in two, crossed by a bridge, a stepping stone or a glide.</summary>
    internal static void Chasm(Layout L, Area a)
    {
        var rnd = L.Random;
        var free = Free(L, a).ToHashSet();
        var (ci, cj) = a.Centre;
        foreach (bool alongX in rnd.Chance(0.5) ? new[] { true, false } : [false, true])
        {
            // The channel's cells: two rows (or columns) through the centre, where the whole line inside the area is free.
            int at = (int)(alongX ? cj : ci);
            var line = a.Cells.Where(c => alongX ? c.J == at || c.J == at + 1 : c.I == at || c.I == at + 1).ToList();
            if (line.Count < 6 || line.Any(c => !free.Contains(c))) continue;
            var sideA = a.Cells.Where(c => alongX ? c.J < at : c.I < at).ToList();
            var sideB = a.Cells.Where(c => alongX ? c.J > at + 1 : c.I > at + 1).ToList();
            if (sideA.Count < a.Cells.Count / 4 || sideB.Count < a.Cells.Count / 4) continue;
            bool deep = rnd.Chance(0.5);
            foreach (var c in line)
            {
                var g = L.Grid.Ground(c.I, c.J)!;
                g.SetTop(deep ? Math.Max(a.Z - 360, 160) : a.Z - 48);
                g.TopRole = deep ? SurfaceRole.Rock : L.Liquid; g.Kind = deep ? AuthoredSurfaceKind.DeadlyLava : L.LiquidKind;
                if (deep && L.Liquid != SurfaceRole.Lava) { g.Kind = AuthoredSurfaceKind.DeadlyWater; g.TopRole = SurfaceRole.Water; }
                if (deep && L.Liquid == SurfaceRole.Lava) g.TopRole = SurfaceRole.Lava;
                g.Required = false; g.SideRole = SurfaceRole.Rock; g.SideTopRole = null;
                L.Reserved.Add(c);
            }
            // The crossing.
            var lineSorted = line.OrderBy(c => alongX ? c.I : c.J).ToList();
            var mid = lineSorted[lineSorted.Count / 2];
            string how = L.Features.Bridges && (rnd.Chance(0.6) || !L.Features.Glides) ? "bridge" : L.Features.Glides ? "glide gap" : "stepping stone";
            if (how == "bridge")
            {
                // A plank deck two cells wide over the channel, at the area's height.
                for (int w = 0; w < 2; w++)
                    for (int t = 0; t < 2; t++)
                    {
                        var c = alongX ? (mid.I + w, at + t) : (at + t, mid.J + w);
                        if (!line.Contains(c)) continue;
                        var deck = new Slab { TopRole = SurfaceRole.Wood, SideRole = SurfaceRole.Wood, BottomRole = SurfaceRole.Wood, SideTopRole = null, Required = true, Area = a.Id };
                        deck.SetTop(a.Z); deck.SetBottom(a.Z - 24);
                        L.Grid.Add(c.Item1, c.Item2, deck);
                    }
            }
            else if (how == "stepping stone")
            {
                // A pillar in the first row: a step out, then one cell to jump.
                var c = alongX ? (mid.I, at) : (at, mid.J);
                var g = L.Grid.Ground(c.Item1, c.Item2)!;
                g.SetTop(a.Z); g.Kind = null; g.TopRole = SurfaceRole.Rock; g.Required = true;
            }
            var near = alongX ? (mid.I, at - 1) : (at - 1, mid.J);
            var far = alongX ? (mid.I, at + 2) : (at + 2, mid.J);
            // The approaches on both sides stay plain floor (no pond or rock where Spyro steps on or jumps from).
            for (int w = -1; w <= 2; w++)
                for (int t = 1; t <= 2; t++)
                {
                    L.Reserved.Add(alongX ? (mid.I + w, at - t) : (at - t, mid.J + w));
                    L.Reserved.Add(alongX ? (mid.I + w, at + 1 + t) : (at + 1 + t, mid.J + w));
                }
            L.Links.Add(new(a.Id, a.Id, how, L.CellTop(near.Item1, near.Item2), L.CellTop(far.Item1, far.Item2)));
            L.Count(how);
            L.Count(deep ? "chasm" : "channel");
            return;
        }
    }

    internal static void Pillar(Layout L, Area a, int height)
    {
        var free = Free(L, a);
        var inner = Layout.EdgeDistance(a.Set).Where(p => p.Value >= 2).Select(p => p.Key).ToHashSet();
        var spots = free.Where(inner.Contains).ToList();
        if (spots.Count == 0) return;
        var c = L.Random.Pick(spots);
        int size = height > 64 && L.Random.Chance(0.4) ? 2 : 1;
        var cells = new List<(int I, int J)>();
        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) cells.Add((c.I + x, c.J + y));
        if (cells.Any(x => !spots.Contains(x))) return;
        foreach (var x in cells)
        {
            var g = L.Grid.Ground(x.I, x.J)!;
            g.SetTop(a.Z + height); g.TopRole = height <= 64 ? SurfaceRole.Rock : SurfaceRole.Ground; g.SideRole = SurfaceRole.Rock;
            g.SideTopRole = height <= 64 ? null : SurfaceRole.CliffTop; g.Required = false;
            L.Reserved.Add(x);
            foreach (var nb in Layout.Neighbours4(x)) L.Reserved.Add(nb);
        }
        if (height <= 64) { L.GemSpots.Add(L.CellTop(c.I, c.J)); L.Count("jump-on rock"); } else L.Count("rock spire");
    }

    private static void House(Layout L, Area a)
    {
        var free = Free(L, a).ToHashSet();
        int w = 2, h = L.Random.Next(2, 4);
        bool turn = L.Random.Chance(0.5);
        if (turn) (w, h) = (h, w);
        var spots = free.Where(c => Enumerable.Range(-1, w + 2).All(x => Enumerable.Range(-1, h + 2).All(y => free.Contains((c.I + x, c.J + y))))).ToList();
        if (spots.Count == 0) return;
        var o = L.Random.Pick(spots);
        int wall = a.Z + L.Random.Next(4, 7) * 32, ridge = 64;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var g = L.Grid.Ground(o.I + x, o.J + y)!;
                g.SetTop(wall); g.TopRole = SurfaceRole.Roof; g.SideRole = SurfaceRole.Brick; g.SideTopRole = null; g.Required = false;
                // A gable along the long side: the roof rises from both eaves to the ridge.
                if (!turn) { g.Top[0] = g.Top[3] = x == 0 ? wall : wall + ridge; g.Top[1] = g.Top[2] = x == 0 ? wall + ridge : wall; }
                else { g.Top[0] = g.Top[1] = y == 0 ? wall : wall + ridge; g.Top[2] = g.Top[3] = y == 0 ? wall + ridge : wall; }
                L.Reserved.Add((o.I + x, o.J + y));
            }
        L.Count("house");
    }

    /// <summary>A rock roof over an area, high enough over its floor and ramps: the area becomes a cave you walk into from
    /// the areas round it, its roof a grassy hill.</summary>
    private static void Cover(Layout L, Area a)
    {
        int highest = a.Cells.Max(c => L.Grid.Ground(c.I, c.J)!.MaxTop);
        int ceiling = Math.Max(a.Z + 320, highest + 200);
        foreach (var c in a.Cells)
        {
            var roof = new Slab { TopRole = SurfaceRole.Ground, SideRole = SurfaceRole.Cliff, SideTopRole = SurfaceRole.CliffTop, BottomRole = SurfaceRole.Ceiling, Area = -1 };
            roof.SetBottom(ceiling); roof.SetTop(ceiling + 96);
            L.Grid.Add(c.I, c.J, roof);
            var g = L.Grid.Ground(c.I, c.J)!;
            if (g.TopRole is SurfaceRole.Ground or SurfaceRole.GroundAlt) g.TopRole = SurfaceRole.Dirt;
            g.SideRole = SurfaceRole.CaveWall;
        }
        a.Covered = true; a.Kind = "cave";
        L.GemSpots.Add(L.CellTop(a.Seed.I, a.Seed.J));
        L.Count("cave");
    }

    /// <summary>A small ledge on a cliff, too high to jump to from below but within a glide from the higher area across the
    /// cliff; Spyro drops back down from it. Gems go on it.</summary>
    private static void SecretLedge(Layout L)
    {
        var grid = L.Grid;
        var options = new List<((int I, int J) Cell, Area Low, Area High)>();
        foreach (var low in L.Areas)
            foreach (var c in Free(L, low))
                foreach (var nb in Layout.Neighbours4(c))
                {
                    var high = L.Areas.FirstOrDefault(x => x.Set.Contains(nb));
                    if (high != null && high.Z - low.Z >= 160) options.Add((c, low, high));
                }
        if (options.Count == 0) return;
        var (cell, lowArea, highArea) = L.Random.Pick(options);
        // Two cells along the cliff, 96 under the higher floor (out of a jump's reach from below, a short glide from above).
        int z = highArea.Z - 96;
        if (z - lowArea.Z <= SpyroMoves.MaxLedge + 16) return;
        // Two cells along the cliff and two deep, so a drop from the edge above lands on it.
        var toHigh = Layout.Neighbours4(cell).First(highArea.Set.Contains);
        var (ni, nj) = (toHigh.I - cell.I, toHigh.J - cell.J);
        var free = Free(L, lowArea).ToHashSet();
        List<(int I, int J)>? cells = null;
        foreach (int side in new[] { 1, -1 })
        {
            var along = (nj * side, ni * side);
            var c4 = new List<(int I, int J)> { cell, (cell.I + along.Item1, cell.J + along.Item2), (cell.I - ni, cell.J - nj), (cell.I - ni + along.Item1, cell.J - nj + along.Item2) };
            if (c4.All(free.Contains) && highArea.Set.Contains((c4[1].I + ni, c4[1].J + nj))) { cells = c4; break; }
        }
        if (cells == null) return;
        foreach (var c in cells)
        {
            var g = grid.Ground(c.I, c.J)!;
            g.SetTop(z); g.TopRole = SurfaceRole.Paving; g.SideRole = SurfaceRole.Rock; g.SideTopRole = SurfaceRole.CliffTop; g.Required = false;
            L.Reserved.Add(c);
            L.GemSpots.Add(L.CellTop(c.I, c.J));
        }
        var from = Layout.Neighbours4(cell).First(highArea.Set.Contains);
        L.Links.Add(new(highArea.Id, lowArea.Id, "drop to secret ledge", L.CellTop(from.I, from.J), L.CellTop(cell.I, cell.J)));
        L.Count("secret ledge");
    }

    /// <summary>A supercharge leap (measured in the game, <see cref="SpyroMoves"/>) inside one large area, along a straight
    /// line of free floor: a hill with a flat top, whose far side is a ramp of supercharge ground falling 180 over four cells
    /// (Spyro walks up it, turns at the top and charges down; he supercharges after about 150 of descent), a flat runway of
    /// two cells to a lip, a deadly pit four cells wide (480: a normal jump and glide carries about 390, a supercharged jump
    /// about 680) and four cells of the area's floor beyond it, then nothing taller to run into. Spyro can always walk round
    /// the pit, so it is a shortcut, never a trap; gems go on the far side.</summary>
    private static bool SuperchargeLeap(Layout L)
    {
        const int Top = 1, Down = 4, Run = 2, Pit = 4, Land = 4, Rise = 180, RunOut = 3;
        const int Length = Top + Down + Run + Pit + Land;
        var grid = L.Grid;
        var options = new List<(Area A, (int I, int J) Start, int Di, int Dj, int Pi, int Pj)>();
        foreach (var area in L.Areas.Where(a => a.Kind is "field" or "start" or "courtyard" && a.Cells.Count >= 60))
        {
            var free = Free(L, area).ToHashSet();
            foreach (var c0 in area.Cells)
                foreach (var (di, dj) in Layout.Axes)
                {
                    var (pi, pj) = (dj, di);
                    bool ok = true;
                    // Two wide along the hill and runway, four wide along the pit and landing, all free floor of the area.
                    for (int t = 0; t < Length && ok; t++)
                        for (int w = t < Top + Down + Run ? 0 : -1; w < (t < Top + Down + Run ? 2 : 3) && ok; w++)
                            ok = free.Contains((c0.I + di * t + pi * w, c0.J + dj * t + pj * w));
                    // A supercharged landing runs on: nothing taller than the area's floor just beyond the landing.
                    for (int t = Length; t < Length + RunOut && ok; t++)
                        for (int w = 0; w < 2 && ok; w++)
                        {
                            var c = (c0.I + di * t + pi * w, c0.J + dj * t + pj * w);
                            ok = grid.Inside(c.Item1, c.Item2) && grid.At(c.Item1, c.Item2).Count > 0 && grid.At(c.Item1, c.Item2).All(x => x.MaxTop <= area.Z + 8);
                        }
                    if (ok) options.Add((area, c0, di, dj, pi, pj));
                }
        }
        if (options.Count == 0) return false;
        var (A, s0, dI, dJ, qI, qJ) = L.Random.Pick(options);
        int floor = A.Z, top = A.Z + Rise;
        var pitKind = L.Liquid == SurfaceRole.Lava ? AuthoredSurfaceKind.DeadlyLava : L.Liquid == SurfaceRole.Goo ? AuthoredSurfaceKind.DeadlyOoze : AuthoredSurfaceKind.DeadlyWater;
        for (int t = 0; t < Length; t++)
            for (int w = -1; w < 3; w++)
            {
                bool narrow = t < Top + Down + Run;
                if (narrow && w is -1 or 2) continue;
                var c = (s0.I + dI * t + qI * w, s0.J + dJ * t + qJ * w);
                var g = grid.Ground(c.Item1, c.Item2)!;
                L.Reserved.Add(c);
                if (t < Top) { g.SetTop(top); g.TopRole = SurfaceRole.Paving; g.SideRole = SurfaceRole.Rock; g.SideTopRole = SurfaceRole.CliffTop; continue; }
                if (t < Top + Down)
                {
                    // Falling toward the lip (+d): the corner on the +d side of cell k is k+1 quarters down.
                    int k = t - Top;
                    int nearZ = top - Rise * k / Down, farZ = top - Rise * (k + 1) / Down;
                    for (int q = 0; q < 4; q++)
                    {
                        var (ci, cj) = (q is 1 or 2 ? 1 : 0, q is 2 or 3 ? 1 : 0);
                        int along = dI != 0 ? (dI > 0 ? ci : 1 - ci) : (dJ > 0 ? cj : 1 - cj);
                        g.Top[q] = along == 0 ? nearZ : farZ;
                    }
                    g.TopRole = SurfaceRole.Charge; g.Kind = AuthoredSurfaceKind.Supercharge; g.SideRole = SurfaceRole.Rock; g.SideTopRole = null;
                    continue;
                }
                if (t < Top + Down + Run) { g.TopRole = SurfaceRole.Charge; g.Kind = AuthoredSurfaceKind.Supercharge; continue; }
                if (t < Top + Down + Run + Pit)
                {
                    g.SetTop(Math.Max(120, floor - 320)); g.Kind = pitKind; g.Required = false;
                    g.TopRole = L.Liquid; g.SideRole = SurfaceRole.Rock; g.SideTopRole = null;
                    continue;
                }
                if (w is 0 or 1) L.GemSpots.Add(L.CellTop(c.Item1, c.Item2));
            }
        var from = L.Point(s0.I + 0.5f + qI * 0.5f, s0.J + 0.5f + qJ * 0.5f, top);
        var lip = L.Point(s0.I + 0.5f + dI * (Top + Down + Run - 0.5f) + qI * 0.5f, s0.J + 0.5f + dJ * (Top + Down + Run - 0.5f) + qJ * 0.5f, floor);
        var to = L.Point(s0.I + 0.5f + dI * (Length - 1) + qI * 0.5f, s0.J + 0.5f + dJ * (Length - 1) + qJ * 0.5f, floor);
        L.Links.Add(new(A.Id, A.Id, "supercharge leap", from, to)
        { JumpAt = MathF.Sqrt((lip.X - from.X) * (lip.X - from.X) + (lip.Y - from.Y) * (lip.Y - from.Y)) });
        L.Count("supercharge leap");
        return true;
    }

    private static float Dist((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static (float D, float T) SegmentDistance((float X, float Y) p, (float X, float Y) a, (float X, float Y) b)
    {
        float vx = b.X - a.X, vy = b.Y - a.Y, len = vx * vx + vy * vy;
        float t = len == 0 ? 0 : Math.Clamp(((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len, 0, 1);
        return (Dist(p, (a.X + t * vx, a.Y + t * vy)), t);
    }
}
