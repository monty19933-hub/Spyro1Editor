namespace Spyro.Editor.Core.Generation;

/// <summary>Whether Spyro can reach every floor the level needs from the start and get back, judged on the level's own
/// slabs with the measured moves (<see cref="SpyroMoves"/>). Floors are sampled every quarter cell; floors joined by
/// walking (slopes up to what charging climbs, no steps) form plates; drops, jumps up a ledge and held jumps with a glide
/// join plates one way.</summary>
internal sealed class LevelReach
{
    public const int Sub = 4;
    public const float Step = LevelGrid.Cell / (float)Sub;

    private sealed class Floor
    {
        public int Si, Sj; public float Z; public Slab Slab = null!; public bool Standable, Required; public int Plate;
    }

    private readonly LevelGrid _grid;
    private readonly List<Floor>[,] _floors;
    private readonly List<Floor> _all = [];
    private int[] _parent = [];

    public int Plates { get; private set; }
    public int GlideEdges { get; private set; }
    public int ChargeSlopes { get; private set; }
    /// <summary>Required floors (samples) that cannot be reached from the start, and those from which the start cannot be
    /// reached again.</summary>
    public int RequiredFloors { get; private set; }
    public int Unreached { get; private set; }
    public int NoWayBack { get; private set; }
    /// <summary>Per area: required samples, and how many are both reachable and have a way back.</summary>
    public Dictionary<int, (int Total, int Good)> ByArea { get; } = [];
    /// <summary>Floors (not required) Spyro can reach but not leave except by losing a life.</summary>
    public int Traps { get; private set; }
    /// <summary>Required samples (sub-cell indices) that fail: not reached, or no way back.</summary>
    public List<(int Si, int Sj)> Bad { get; } = [];
    /// <summary>Areas reached only by gliding.</summary>
    public HashSet<int> GlideOnlyAreas { get; } = [];

    public LevelReach(LevelGrid grid)
    {
        _grid = grid;
        int w = grid.W * Sub, h = grid.H * Sub;
        _floors = new List<Floor>[w, h];
        for (int si = 0; si < w; si++)
            for (int sj = 0; sj < h; sj++)
            {
                var list = _floors[si, sj] = [];
                int i = si / Sub, j = sj / Sub;
                float u = (si % Sub + 0.5f) / Sub, v = (sj % Sub + 0.5f) / Sub;
                var slabs = grid.At(i, j);
                foreach (var s in slabs)
                {
                    float z = s.TopAt(u, v);
                    // Room to stand: nothing above within 60 units.
                    if (slabs.Any(o => o != s && o.BottomAt(u, v) > z - 1 && o.BottomAt(u, v) < z + 60)) continue;
                    var f = new Floor { Si = si, Sj = sj, Z = z, Slab = s, Standable = s.Kind is null || s.Kind == Editing.AuthoredSurfaceKind.Supercharge };
                    f.Required = f.Standable && s.Required;
                    list.Add(f); _all.Add(f);
                }
            }
    }

    private bool Solid(int si, int sj, float lo, float hi)
    {
        if (si < 0 || sj < 0 || si >= _grid.W * Sub || sj >= _grid.H * Sub) return false;
        int i = si / Sub, j = sj / Sub;
        float u = (si % Sub + 0.5f) / Sub, v = (sj % Sub + 0.5f) / Sub;
        foreach (var s in _grid.At(i, j))
            if (s.BottomAt(u, v) < hi && s.TopAt(u, v) > lo) return true;
        return false;
    }

    private int Find(int x) { while (_parent[x] != x) x = _parent[x] = _parent[_parent[x]]; return x; }

    /// <summary>Runs the check from the floor under <paramref name="start"/> (editor units).</summary>
    public void Check(Primitives.Vector3f start)
    {
        for (int k = 0; k < _all.Count; k++) _all[k].Plate = k;
        _parent = [.. Enumerable.Range(0, _all.Count)];
        int w = _grid.W * Sub, h = _grid.H * Sub;
        float walk = MathF.Tan(SpyroMoves.MaxWalkSlopeDegrees * MathF.PI / 180), charge = MathF.Tan(SpyroMoves.MaxChargeSlopeDegrees * MathF.PI / 180);
        // 1. Plates: walking both ways between neighbouring samples on one surface (or surfaces meeting edge to edge).
        foreach (var f in _all)
        {
            if (!f.Standable) continue;
            foreach (var (di, dj) in Layout.Axes)
            {
                int ni = f.Si + di, nj = f.Sj + dj;
                if (ni < 0 || nj < 0 || ni >= w || nj >= h) continue;
                foreach (var g in _floors[ni, nj])
                {
                    if (!g.Standable || !Continuous(f, g)) continue;
                    float slope = Math.Abs(g.Z - f.Z) / Step;
                    if (slope > charge) continue;
                    if (slope > walk) ChargeSlopes++;
                    int a = Find(f.Plate), b = Find(g.Plate);
                    if (a != b) _parent[a] = b;
                }
            }
        }
        var plateOf = _all.ToDictionary(f => f, f => Find(f.Plate));
        var plateIds = plateOf.Values.Distinct().ToList();
        Plates = plateIds.Count;
        var edges = plateIds.ToDictionary(p => p, _ => new HashSet<int>());
        var glideEdges = new HashSet<(int, int)>();
        // 2. One-way moves between plates.
        foreach (var f in _all)
        {
            if (!f.Standable) continue;
            int pf = plateOf[f];
            bool edge = false;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue;
                    int ni = f.Si + di, nj = f.Sj + dj;
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h) { edge = true; continue; }
                    var near = _floors[ni, nj];
                    if (!near.Any(g => g.Standable && Math.Abs(g.Z - f.Z) < 24)) edge = true;
                    foreach (var g in near)
                    {
                        if (!g.Standable) continue;
                        int pg = plateOf[g];
                        if (pg == pf) continue;
                        // Drop: lower, with the space over the lower floor open at Spyro's height.
                        if (g.Z < f.Z - 1 && !Solid(ni, nj, g.Z + 2, f.Z + 40)) edges[pf].Add(pg);
                        // Jump up a ledge: headroom over both.
                        else if (g.Z > f.Z && g.Z - f.Z <= SpyroMoves.DesignLedge && !Solid(f.Si, f.Sj, f.Z + 2, f.Z + SpyroMoves.Headroom) && !Solid(ni, nj, g.Z + 2, g.Z + 60))
                            edges[pf].Add(pg);
                    }
                }
            if (!edge) continue;
            // Held jump and glide in 16 directions, landing on any floor under the path (Spyro can stop gliding anywhere).
            for (int d = 0; d < 16; d++)
            {
                double a = d * Math.PI / 8;
                float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                float x0 = (f.Si + 0.5f) * Step, y0 = (f.Sj + 0.5f) * Step;
                // The jump needs headroom where it starts.
                if (Solid(f.Si, f.Sj, f.Z + 2, f.Z + SpyroMoves.JumpHeight + 40)) break;
                var seenPlates = new HashSet<int>();
                for (float t = Step; t <= 2800; t += Step * 0.75f)
                {
                    float hz = f.Z + SpyroMoves.GlideHeight(t);
                    int si = (int)MathF.Floor((x0 + dx * t) / Step), sj = (int)MathF.Floor((y0 + dy * t) / Step);
                    if (si < 0 || sj < 0 || si >= w || sj >= h) break;
                    if (Solid(si, sj, hz - 4, hz + 36)) break;
                    // The highest floor below the path with open air between.
                    Floor? land = null;
                    foreach (var g in _floors[si, sj])
                        if (g.Z <= hz && (land == null || g.Z > land.Z)) land = g;
                    if (land != null && land.Standable && !Solid(si, sj, land.Z + 2, hz - 4))
                    {
                        int pg = plateOf[land];
                        if (pg != pf && seenPlates.Add(pg)) { edges[pf].Add(pg); glideEdges.Add((pf, pg)); }
                    }
                    if (hz < -2000) break;
                }
            }
        }
        GlideEdges = glideEdges.Count;
        // 3. Forward from the start, backward to it.
        var startFloor = Nearest(start);
        if (startFloor == null) { RequiredFloors = _all.Count(f => f.Required); Unreached = RequiredFloors; return; }
        int s0 = plateOf[startFloor];
        var forward = Reach(s0, edges);
        var reverse = plateIds.ToDictionary(p => p, _ => new HashSet<int>());
        foreach (var (p, outs) in edges) foreach (int q in outs) reverse[q].Add(p);
        var backward = Reach(s0, reverse);
        // Plates reached only through a glide edge (for the report).
        var walkOnly = Reach(s0, edges.ToDictionary(e => e.Key, e => e.Value.Where(q => !glideEdges.Contains((e.Key, q))).ToHashSet()));
        foreach (var f in _all.Where(f => f.Required))
        {
            int p = plateOf[f];
            bool ok = forward.Contains(p) && backward.Contains(p);
            RequiredFloors++;
            if (!forward.Contains(p)) Unreached++;
            else if (!backward.Contains(p)) NoWayBack++;
            if (!ok) Bad.Add((f.Si, f.Sj));
            var key = f.Slab.Area;
            var (t, g) = ByArea.GetValueOrDefault(key);
            ByArea[key] = (t + 1, g + (ok ? 1 : 0));
            if (ok && !walkOnly.Contains(p)) GlideOnlyAreas.Add(key);
        }
        Traps = _all.Count(f => f.Standable && !f.Required && forward.Contains(plateOf[f]) && !backward.Contains(plateOf[f]));
    }

    /// <summary>Whether two neighbouring samples lie on one surface: the same slab, or slabs meeting at the same height
    /// where they touch.</summary>
    private bool Continuous(Floor f, Floor g)
    {
        if (f.Slab == g.Slab) return true;
        // The point halfway between the samples, in each slab's cell.
        float x = (f.Si + g.Si + 1) * 0.5f * Step, y = (f.Sj + g.Sj + 1) * 0.5f * Step;
        float zf = At(f, x, y), zg = At(g, x, y);
        return Math.Abs(zf - zg) < 3;
    }

    private float At(Floor f, float x, float y)
    {
        int i = f.Si / Sub, j = f.Sj / Sub;
        float u = Math.Clamp(x / LevelGrid.Cell - i, 0, 1), v = Math.Clamp(y / LevelGrid.Cell - j, 0, 1);
        return f.Slab.TopAt(u, v);
    }

    private Floor? Nearest(Primitives.Vector3f p)
    {
        int si = (int)((p.X - _grid.OriginX) / Step), sj = (int)((p.Y - _grid.OriginY) / Step);
        if (si < 0 || sj < 0 || si >= _grid.W * Sub || sj >= _grid.H * Sub) return null;
        return _floors[si, sj].Where(f => f.Standable && f.Z <= p.Z + 8).OrderByDescending(f => f.Z).FirstOrDefault();
    }

    private static HashSet<int> Reach(int from, Dictionary<int, HashSet<int>> edges)
    {
        var seen = new HashSet<int> { from };
        var q = new Queue<int>(); q.Enqueue(from);
        while (q.Count > 0) { int p = q.Dequeue(); foreach (int n in edges[p]) if (seen.Add(n)) q.Enqueue(n); }
        return seen;
    }
}
