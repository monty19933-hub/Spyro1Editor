using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>A part of the level Spyro must reach: its cells, its floor height and what it is.</summary>
internal sealed class Area
{
    public int Id;
    public List<(int I, int J)> Cells = [];
    public int Z;
    public string Kind = "field";
    public (int I, int J) Seed;
    public bool Covered;
    public HashSet<(int I, int J)> Set => _set ??= [.. Cells];
    private HashSet<(int I, int J)>? _set;
    public void Changed() => _set = null;
    public (float I, float J) Centre => (Cells.Average(c => c.I + 0.5f), Cells.Average(c => c.J + 0.5f));
}

/// <summary>How two areas are joined, for the report and the in-game route checks: walk (open ground, a ramp), ledge
/// (jump up, drop down), charge (a ramp only charging climbs), bridge, stones (a chain of jumps), glide (one way, from
/// higher ground), tunnel. <see cref="From"/>/<see cref="To"/> are where the move starts and ends.</summary>
public sealed record LevelLink(int A, int B, string Kind, Vector3f From, Vector3f To)
{
    /// <summary>Where the floor ends on the way from <see cref="From"/> (editor units along the line): where a jump starts.</summary>
    public float? JumpAt { get; init; }
}

/// <summary>The layout under construction: the grid, its areas and links, reserved cells, gem spots and notes.</summary>
internal sealed class Layout
{
    public required LevelGrid Grid;
    public required LevelTheme Theme;
    public required LevelStyle Style;
    public required LevelRandom Random;
    public required FeatureSet Features;
    public List<Area> Areas = [];
    public List<LevelLink> Links = [];
    /// <summary>Cells no feature may change (ramps, the start pad, bridge ends).</summary>
    public HashSet<(int I, int J)> Reserved = [];
    public List<Vector3f> GemSpots = [];
    public List<string> Notes = [];
    public (int I, int J) StartCell;
    public int StartArea;
    /// <summary>Counts of what was built, for the report.</summary>
    public Dictionary<string, int> Counts = [];
    public void Count(string what, int n = 1) => Counts[what] = (Counts.TryGetValue(what, out int c) ? c : 0) + n;

    public SurfaceRole Liquid => Theme.Liquid;
    public AuthoredSurfaceKind LiquidKind => Theme.Liquid switch
    {
        SurfaceRole.Lava => AuthoredSurfaceKind.Lava, SurfaceRole.Goo => AuthoredSurfaceKind.Ooze, _ => AuthoredSurfaceKind.Water
    };

    public Area NewArea(string kind, int z)
    {
        var a = new Area { Id = Areas.Count, Kind = kind, Z = z };
        Areas.Add(a);
        return a;
    }

    public Vector3f Point(float i, float j, float z) => new(Grid.X(0) + i * LevelGrid.Cell, Grid.Y(0) + j * LevelGrid.Cell, z);
    /// <summary>The middle of a cell's floor: its highest required floor (a bridge deck over water, a cave floor under its
    /// roof), else its ground, else its top slab.</summary>
    public Vector3f CellTop(int i, int j)
    {
        var slabs = Grid.Inside(i, j) ? Grid.At(i, j) : [];
        var s = slabs.LastOrDefault(x => x.Required) ?? Grid.Ground(i, j) ?? slabs.LastOrDefault();
        return Point(i + 0.5f, j + 0.5f, s?.TopAt(0.5f, 0.5f) ?? 0);
    }

    /// <summary>A plain ground slab for an area's cell.</summary>
    public Slab GroundSlab(int z, Area? area, SurfaceRole top = SurfaceRole.Ground)
    {
        var s = new Slab { TopRole = top, SideRole = SurfaceRole.Cliff, SideTopRole = SurfaceRole.CliffTop, Required = area != null, Area = area?.Id ?? -1 };
        s.SetTop(z);
        return s;
    }

    /// <summary>Cells in a fixed order (row by row): hash sets of cells enumerate in a different order in every process, and
    /// the same seed must make the same level every time.</summary>
    public static List<(int I, int J)> Sorted(IEnumerable<(int I, int J)> cells) => [.. cells.OrderBy(c => c.J).ThenBy(c => c.I)];

    public static IEnumerable<(int I, int J)> Neighbours4((int I, int J) c)
    { yield return (c.I + 1, c.J); yield return (c.I - 1, c.J); yield return (c.I, c.J + 1); yield return (c.I, c.J - 1); }
    public static readonly (int Di, int Dj)[] Axes = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Cells of a rounded blob of about <paramref name="radius"/> cells around (ci, cj), its edge wobbling by the
    /// seed (a few waves round the centre).</summary>
    public List<(int I, int J)> Blob(float ci, float cj, float radius, float wobble = 0.25f)
    {
        double p1 = Random.NextDouble() * Math.PI * 2, p2 = Random.NextDouble() * Math.PI * 2, p3 = Random.NextDouble() * Math.PI * 2;
        double a1 = wobble * Random.NextDouble(), a2 = wobble * Random.NextDouble(), a3 = wobble * 0.5 * Random.NextDouble();
        List<(int, int)> cells = [];
        int r = (int)Math.Ceiling(radius * (1 + wobble * 2)) + 1;
        for (int i = (int)ci - r; i <= (int)ci + r; i++)
            for (int j = (int)cj - r; j <= (int)cj + r; j++)
            {
                if (!Grid.Inside(i, j)) continue;
                double dx = i + 0.5 - ci, dy = j + 0.5 - cj, d = Math.Sqrt(dx * dx + dy * dy), t = Math.Atan2(dy, dx);
                double edge = radius * (1 + a1 * Math.Sin(2 * t + p1) + a2 * Math.Sin(3 * t + p2) + a3 * Math.Sin(5 * t + p3));
                if (d <= edge) cells.Add((i, j));
            }
        return cells;
    }

    /// <summary>Distance in cells from each cell of <paramref name="cells"/> to the nearest cell outside it (1 at the edge).</summary>
    public static Dictionary<(int I, int J), int> EdgeDistance(IReadOnlyCollection<(int I, int J)> cells)
    {
        var set = cells as HashSet<(int, int)> ?? [.. cells];
        Dictionary<(int, int), int> d = [];
        var queue = new Queue<(int, int)>();
        foreach (var c in Sorted(cells))
            if (Neighbours4(c).Any(n => !set.Contains(n))) { d[c] = 1; queue.Enqueue(c); }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbours4(c))
                if (set.Contains(n) && !d.ContainsKey(n)) { d[n] = d[c] + 1; queue.Enqueue(n); }
        }
        return d;
    }

    /// <summary>The largest square of cells (at least <paramref name="size"/> wide) inside <paramref name="cells"/>, nearest
    /// to <paramref name="near"/>: its lowest corner cell, or null.</summary>
    public static (int I, int J)? Square(HashSet<(int I, int J)> cells, int size, (float I, float J) near)
    {
        (int, int)? best = null; double bestD = double.MaxValue;
        foreach (var (i, j) in Sorted(cells))
        {
            bool ok = true;
            for (int a = 0; a < size && ok; a++) for (int b = 0; b < size && ok; b++) ok = cells.Contains((i + a, j + b));
            if (!ok) continue;
            double d = Math.Pow(i + size / 2.0 - near.I, 2) + Math.Pow(j + size / 2.0 - near.J, 2);
            if (d < bestD) { bestD = d; best = (i, j); }
        }
        return best;
    }

    /// <summary>Sets a cell's ground top (all four corners) keeping its slab's other settings.</summary>
    public void SetGround(int i, int j, int z)
    {
        var g = Grid.Ground(i, j);
        if (g != null) g.SetTop(z);
    }

    /// <summary>A ramp between two areas whose floors differ by more than a jump: <paramref name="length"/> cells from the
    /// edge of <paramref name="upper"/> into <paramref name="lower"/> (or cut back into the upper area when the lower one has
    /// no room), <paramref name="width"/> cells wide, rising <paramref name="rise"/> a cell at most. Returns false when no
    /// stretch of their border has room.</summary>
    public bool TryRamp(Area upper, Area lower, int rise, int width, SurfaceRole role, string kind)
    {
        int dz = upper.Z - lower.Z;
        int length = (int)Math.Ceiling(dz / (double)rise);
        // Border pairs: an upper cell with the lower cell beside it, in the direction of the lower area.
        List<((int I, int J) U, (int Di, int Dj) D, bool Into, double Score)> options = [];
        var (lc, uc) = (lower.Centre, upper.Centre);
        foreach (var u in upper.Cells)
            foreach (var (di, dj) in Axes)
            {
                var l = (u.I + di, u.J + dj);
                if (!lower.Set.Contains(l)) continue;
                var (pi, pj) = (dj, di);   // across the ramp
                foreach (bool into in new[] { true, false })
                {
                    bool fits = true;
                    for (int t = 0; t < length && fits; t++)
                        for (int w = 0; w < width && fits; w++)
                        {
                            var c = into ? (u.I + di * (t + 1) + pi * w, u.J + dj * (t + 1) + pj * w) : (u.I - di * t + pi * w, u.J - dj * t + pj * w);
                            var home = into ? lower : upper;
                            fits = home.Set.Contains(c) && !Reserved.Contains(c) && Grid.At(c.Item1, c.Item2).Count == 1;
                            // A ramp must not open onto anything but its own two areas at its top and bottom.
                            if (fits && into && t == 0) fits = upper.Set.Contains((c.Item1 - di, c.Item2 - dj));
                            if (fits && !into && t == 0) fits = lower.Set.Contains((c.Item1 + di, c.Item2 + dj)) && !Reserved.Contains((c.Item1 + di, c.Item2 + dj));
                        }
                    // The far end of the ramp opens onto its area's floor.
                    if (fits)
                    {
                        var end = into ? (u.I + di * (length + 1), u.J + dj * (length + 1)) : (u.I - di * length, u.J - dj * length);
                        for (int w = 0; w < width && fits; w++)
                            fits = (into ? lower : upper).Set.Contains((end.Item1 + pi * w, end.Item2 + pj * w)) && !Reserved.Contains((end.Item1 + pi * w, end.Item2 + pj * w));
                    }
                    if (!fits) continue;
                    double score = Math.Abs(u.I - (uc.I + lc.I) / 2) + Math.Abs(u.J - (uc.J + lc.J) / 2) + (into ? 0 : 3) + Random.NextDouble() * 3;
                    options.Add((u, (di, dj), into, score));
                }
            }
        if (options.Count == 0) return false;
        var (cell, (dI, dJ), inward, _) = options.MinBy(o => o.Score);
        var (qi, qj) = (dJ, dI);
        // Heights along the ramp: from the upper floor at the border down to the lower floor, `rise` a cell at most.
        int step = (int)Math.Ceiling(dz / (double)length);
        for (int t = 0; t < length; t++)
            for (int w = 0; w < width; w++)
            {
                (int, int) c = inward ? (cell.I + dI * (t + 1) + qi * w, cell.J + dJ * (t + 1) + qj * w) : (cell.I - dI * t + qi * w, cell.J - dJ * t + qj * w);
                // Distance from the border at this cell's near and far edges (in cells).
                // Inward: the edge nearer the border is the higher one. Cut back: the edge nearer the border is the lower one.
                int nearZ = inward ? upper.Z - step * t : lower.Z + step * t;
                int farZ = inward ? Math.Max(lower.Z, upper.Z - step * (t + 1)) : Math.Min(upper.Z, lower.Z + step * (t + 1));
                var g = Grid.At(c.Item1, c.Item2)[0];
                g.TopRole = role; g.SideTopRole = null; g.SideRole = SurfaceRole.Rock;
                // "near" is the edge toward the border (direction -d for inward ramps, +d for cut-back ramps).
                for (int k = 0; k < 4; k++)
                {
                    var (ci, cj) = (k is 1 or 2 ? 1 : 0, k is 2 or 3 ? 1 : 0);
                    // The corner's offset along d: 0 at the cell edge nearer the border.
                    int along = dI != 0 ? (dI > 0 ? ci : 1 - ci) : (dJ > 0 ? cj : 1 - cj);
                    if (!inward) along = 1 - along;
                    g.Top[k] = along == 0 ? nearZ : farZ;
                }
                Reserved.Add(c);
            }
        // The landings: the upper area's cell at the ramp's top and the lower area's cell at its foot (an inward ramp starts
        // at the border and runs into the lower area; a cut-back ramp starts at the border and runs back into the upper one).
        var topCell = inward ? cell : (cell.I - dI * length, cell.J - dJ * length);
        var bottomCell = inward ? (cell.I + dI * (length + 1), cell.J + dJ * (length + 1)) : (cell.I + dI, cell.J + dJ);
        for (int w = 0; w < width; w++)
        {
            Reserved.Add((topCell.Item1 + qi * w, topCell.Item2 + qj * w));
            Reserved.Add((bottomCell.Item1 + qi * w, bottomCell.Item2 + qj * w));
        }
        var top = Point(topCell.Item1 + 0.5f + qi * (width - 1) / 2f, topCell.Item2 + 0.5f + qj * (width - 1) / 2f, upper.Z);
        var bottom = Point(bottomCell.Item1 + 0.5f + qi * (width - 1) / 2f, bottomCell.Item2 + 0.5f + qj * (width - 1) / 2f, lower.Z);
        Links.Add(new(lower.Id, upper.Id, kind, bottom, top));
        Count(kind);
        return true;
    }

    /// <summary>Gently rolling floors: open ground well inside an area rises and falls by up to <paramref name="amplitude"/>
    /// (a multiple of 4) on the corners of 2 x 2 tiles, the cells between following each tile's corners so a tile stays one
    /// smooth face. Borders, ramps, the start pad, features and anything with a roof stay as they were; gem spots and link
    /// ends follow the floor.</summary>
    public void Undulate(int amplitude)
    {
        if (amplitude <= 0) return;
        var owner = new Dictionary<(int, int), Area>();
        foreach (var a in Areas) foreach (var c in a.Cells) owner[c] = a;
        bool Eligible(int i, int j) =>
            owner.TryGetValue((i, j), out var a) && a.Kind is not "tunnel" && !Reserved.Contains((i, j)) && Grid.At(i, j).Count == 1 &&
            Grid.At(i, j)[0] is { Flat: true, Kind: null } s && s.Top[0] == a.Z && s.TopRole is SurfaceRole.Ground or SurfaceRole.GroundAlt or SurfaceRole.Path or SurfaceRole.Sand or SurfaceRole.Dirt;
        double p1 = Random.NextDouble() * 6, p2 = Random.NextDouble() * 6, p3 = Random.NextDouble() * 6;
        // Offsets at even lattice points (tile corners) whose 16 surrounding cells are all eligible and in one area.
        var offset = new Dictionary<(int, int), int>();
        for (int x = 2; x < Grid.W - 1; x += 2)
            for (int y = 2; y < Grid.H - 1; y += 2)
            {
                if (!owner.TryGetValue((x, y), out var area)) continue;
                bool ok = true;
                for (int i = x - 2; i < x + 2 && ok; i++) for (int j = y - 2; j < y + 2 && ok; j++) ok = Eligible(i, j) && owner[(i, j)] == area;
                if (!ok) continue;
                double n = Math.Sin(x * 0.55 + p1) * Math.Cos(y * 0.47 + p2) + 0.5 * Math.Sin((x + y) * 0.9 + p3);
                int o = 4 * (int)Math.Round(n / 1.5 * amplitude / 4.0);
                if (o != 0) offset[(x, y)] = Math.Clamp(o, -amplitude, amplitude);
            }
        if (offset.Count == 0) return;
        int At(int x, int y)
        {
            // Bilinear in the tile: even points carry offsets, odd points the average of their even neighbours.
            int O(int a, int b) => offset.GetValueOrDefault((a, b));
            bool ex = x % 2 == 0, ey = y % 2 == 0;
            if (ex && ey) return O(x, y);
            if (ex) return (O(x, y - 1) + O(x, y + 1)) / 2;
            if (ey) return (O(x - 1, y) + O(x + 1, y)) / 2;
            return (O(x - 1, y - 1) + O(x + 1, y - 1) + O(x - 1, y + 1) + O(x + 1, y + 1)) / 4;
        }
        int[] ci = [0, 1, 1, 0], cj = [0, 0, 1, 1];
        var touched = new HashSet<(int, int)>();
        foreach (var (x, y) in offset.Keys)
            for (int i = x - 2; i < x + 2; i++)
                for (int j = y - 2; j < y + 2; j++)
                    if (touched.Add((i, j)))
                    {
                        var s = Grid.At(i, j)[0];
                        for (int k = 0; k < 4; k++) s.Top[k] = owner[(i, j)].Z + At(i + ci[k], j + cj[k]);
                    }
        // Gem spots and link ends stand on the floor again.
        float FloorZ(Vector3f p)
        {
            int i = (int)MathF.Floor((p.X - Grid.X(0)) / LevelGrid.Cell), j = (int)MathF.Floor((p.Y - Grid.Y(0)) / LevelGrid.Cell);
            if (!Grid.Inside(i, j) || !touched.Contains((i, j))) return p.Z;
            float u = (p.X - Grid.X(i)) / LevelGrid.Cell, v = (p.Y - Grid.Y(j)) / LevelGrid.Cell;
            return Grid.At(i, j)[0].TopAt(u, v);
        }
        GemSpots = [.. GemSpots.Select(g => g with { Z = FloorZ(g) })];
        Links = [.. Links.Select(l => l with { From = l.From with { Z = FloorZ(l.From) }, To = l.To with { Z = FloorZ(l.To) } })];
    }

    /// <summary>Paints a path of <paramref name="role"/> from a to b over flat ground cells of one area.</summary>
    public void PaintPath((int I, int J) a, (int I, int J) b, Area area, SurfaceRole role)
    {
        int i = a.I, j = a.J;
        int guard = 0;
        while ((i, j) != b && guard++ < 400)
        {
            if (area.Set.Contains((i, j)) && Grid.At(i, j).Count == 1 && Grid.Ground(i, j) is { Flat: true, Kind: null } g && g.TopRole is SurfaceRole.Ground or SurfaceRole.GroundAlt)
                g.TopRole = role;
            int di = Math.Sign(b.I - i), dj = Math.Sign(b.J - j);
            if (di != 0 && dj != 0) { if (Random.Chance(0.5)) i += di; else j += dj; }
            else { i += di; j += dj; }
        }
    }
}

/// <summary>The features chosen for one level (the options' choices, with the open ones decided by the seed).</summary>
internal sealed record FeatureSet(bool Caves, bool ChargePaths, bool Glides, bool Hazards, bool Bridges, bool Secrets);
