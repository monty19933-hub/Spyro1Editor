using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>One face before materials: corners (whole units; a floor's turn anticlockwise seen from above, a wall's
/// run top edge first so its texture stands upright), surface, light per corner (0-1), ground kind and what it is.</summary>
internal sealed record TerrainPiece(Vector3f[] Corners, SurfaceRole Role, double[] Light, AuthoredSurfaceKind? Kind, int Layer, bool Wall)
{
    public bool Collides { get; init; } = true;
    public bool TwoSided { get; init; }
    public bool FrontReversed { get; init; }
    /// <summary>A face lifted from the look's level keeps that level's texture and its own colours, not a role's.</summary>
    public int? Texture { get; init; }
    public ColorRgba[]? Paint { get; init; }
}

/// <summary>Turns a <see cref="TerrainPlan"/> into faces the way the game's own levels are built: one welded sheet of
/// four-cornered faces about 128 units across, whose lines follow the land's edges (the sheet's corners are moved onto
/// them, and a cell an edge crosses cornerwise is cut in two), with upright wall faces wherever floors of different
/// heights meet.</summary>
internal sealed class TerrainMesher
{
    private readonly TerrainPlan _plan;
    private readonly double _s;
    private int _nx, _ny;
    private double[] _x = [], _y = [];
    private bool[] _snapped = [];
    private readonly List<(int Layer, int[] V)> _faces = [];
    /// <summary>Per sheet corner: each layer's height there, after heights a step apart or less are made one.</summary>
    private Dictionary<int, double>[] _z = [];

    public const double BandHeight = 150, Weld = 14;
    public List<TerrainPiece> Pieces { get; } = [];
    /// <summary>Floor faces by layer, for the reach check and the map: sheet corner numbers and the piece made of them.</summary>
    public List<(int Layer, int[] V, TerrainPiece Piece)> Floors { get; } = [];
    /// <summary>Zones holding a part of one of the game's levels (<see cref="TerrainZone.Cut"/>) whose ground was sewn to
    /// the cut's floor edge all round: the sheet has no floor or wall of its own there. A cut whose seam could not be
    /// made keeps the sheet's floor (and is left out of the level).</summary>
    public HashSet<int> Sewn { get; } = [];

    public TerrainMesher(TerrainPlan plan, double spacing) { _plan = plan; _s = spacing; }

    private int At(int i, int j) => j * (_nx + 1) + i;

    public void Build(LevelRandom rnd)
    {
        var (x0, y0, x1, y1) = _plan.Bounds();
        _nx = Math.Max(2, (int)Math.Ceiling((x1 - x0) / _s)); _ny = Math.Max(2, (int)Math.Ceiling((y1 - y0) / _s));
        int n = (_nx + 1) * (_ny + 1);
        _x = new double[n]; _y = new double[n]; _snapped = new bool[n];
        var warp = new TerrainNoise(rnd.Fork("warp"));
        for (int j = 0; j <= _ny; j++)
            for (int i = 0; i <= _nx; i++)
            {
                double x = x0 + i * _s, y = y0 + j * _s;
                // The sheet is not a ruled grid: its lines wander a little, as hand-made levels' do.
                _x[At(i, j)] = x + 0.2 * _s * warp.At(x / (5.5 * _s), y / (5.5 * _s));
                _y[At(i, j)] = y + 0.2 * _s * warp.At(x / (5.5 * _s) + 40, y / (5.5 * _s) + 40);
            }
        Snap();
        Cells();
        Heights();
        Emit();
        Apron(x0, y0, x1, y1);
    }

    /// <summary>Open water out to the horizon: big faces a little under the level's own water (which they pass under at
    /// its outer edge), from where that water ends. Spyro never reaches them.</summary>
    private void Apron(double x0, double y0, double x1, double y1)
    {
        if (_plan.Kind != EnclosureKind.Sea) return;
        const double cell = 800, far = 2400;
        int nx = (int)Math.Ceiling((x1 - x0 + 2 * far) / cell), ny = (int)Math.Ceiling((y1 - y0 + 2 * far) / cell);
        float z = (float)Math.Round(_plan.SeaLevel) - 6;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                double ax = x0 - far + i * cell, ay = y0 - far + j * cell, bx = ax + cell, by = ay + cell;
                // Wholly under the level's own water or land: not needed.
                bool covered = true;
                foreach (var (px, py) in new[] { (ax, ay), (bx, ay), (bx, by), (ax, by), ((ax + bx) / 2, (ay + by) / 2) })
                    if (_plan.Land(px, py) > _plan.SeaWidth - 140) covered = false;
                if (covered) continue;
                Vector3f[] c = [new((float)Math.Round(ax), (float)Math.Round(ay), z), new((float)Math.Round(bx), (float)Math.Round(ay), z), new((float)Math.Round(bx), (float)Math.Round(by), z), new((float)Math.Round(ax), (float)Math.Round(by), z)];
                if (c.Any(p => p.X < 0 || p.Y < 0 || p.X > 16383 || p.Y > 16383)) continue;
                Pieces.Add(new TerrainPiece(c, _plan.LiquidRole, [0.2, 0.2, 0.2, 0.2], null, TerrainPlan.Sea, false) { Collides = false });
            }
    }

    /// <summary>Moves sheet corners onto the plan's lines: of the two ends of every sheet line a plan line crosses, the
    /// nearer one.</summary>
    private void Snap()
    {
        int n = _x.Length;
        var cost = new double[n]; var curve = new int[n];
        Array.Fill(cost, double.MaxValue); Array.Fill(curve, -1);
        var curves = _plan.Curves().ToList();
        var values = new double[n];
        for (int c = 0; c < curves.Count; c++)
        {
            var (field, weight) = curves[c];
            for (int k = 0; k < n; k++) values[k] = field(_x[k], _y[k]);
            void Edge(int a, int b)
            {
                double fa = values[a], fb = values[b];
                if (fa == 0 || fb == 0 || fa < 0 == fb < 0) return;
                double t = fa / (fa - fb);
                int near = t < 0.5 ? a : b;
                double w = Math.Min(t, 1 - t) * weight;
                if (w < cost[near]) { cost[near] = w; curve[near] = c; }
            }
            for (int j = 0; j <= _ny; j++)
                for (int i = 0; i <= _nx; i++)
                {
                    if (i < _nx) Edge(At(i, j), At(i + 1, j));
                    if (j < _ny) Edge(At(i, j), At(i, j + 1));
                }
        }
        var ox = (double[])_x.Clone(); var oy = (double[])_y.Clone();
        for (int k = 0; k < n; k++)
        {
            if (curve[k] < 0) continue;
            var field = curves[curve[k]].Field;
            double x = _x[k], y = _y[k];
            bool ok = true;
            for (int step = 0; step < 4; step++)
            {
                double f = field(x, y);
                if (Math.Abs(f) < 0.5) break;
                double gx = (field(x + 3, y) - field(x - 3, y)) / 6, gy = (field(x, y + 3) - field(x, y - 3)) / 6, g2 = gx * gx + gy * gy;
                if (g2 < 1e-6) { ok = false; break; }
                x -= f * gx / g2; y -= f * gy / g2;
            }
            double moved = Math.Sqrt((x - ox[k]) * (x - ox[k]) + (y - oy[k]) * (y - oy[k]));
            if (!ok || moved > 0.62 * _s) continue;
            _x[k] = x; _y[k] = y; _snapped[k] = true;
        }
        // Two moved corners that ended up almost on top of each other: the one that moved further goes back.
        for (int pass = 0; pass < 2; pass++)
            for (int j = 0; j <= _ny; j++)
                for (int i = 0; i <= _nx; i++)
                {
                    void Apart(int a, int b)
                    {
                        double dx = _x[a] - _x[b], dy = _y[a] - _y[b];
                        if (dx * dx + dy * dy >= 0.3 * _s * 0.3 * _s) return;
                        int back = !_snapped[a] ? b : !_snapped[b] ? a : cost[a] > cost[b] ? a : b;
                        if (!_snapped[back]) return;
                        _x[back] = ox[back]; _y[back] = oy[back]; _snapped[back] = false;
                    }
                    if (i < _nx) Apart(At(i, j), At(i + 1, j));
                    if (j < _ny) Apart(At(i, j), At(i, j + 1));
                    if (i < _nx && j < _ny) { Apart(At(i, j), At(i + 1, j + 1)); Apart(At(i + 1, j), At(i, j + 1)); }
                }
    }

    /// <summary>Cells of nothing that the level's own water surrounds (the middle of a ring of islands): water too.</summary>
    private bool[]? _lake;

    private void Lakes()
    {
        if (_plan.Kind != EnclosureKind.Sea) return;
        var open = new bool[_nx * _ny]; var nothing = new bool[_nx * _ny];
        for (int j = 0; j < _ny; j++)
            for (int i = 0; i < _nx; i++)
            {
                int a = At(i, j), c = At(i + 1, j + 1);
                nothing[j * _nx + i] = _plan.Classify((_x[a] + _x[c]) / 2, (_y[a] + _y[c]) / 2) == TerrainPlan.None;
            }
        var queue = new Queue<int>();
        void Visit(int i, int j) { if (i < 0 || j < 0 || i >= _nx || j >= _ny) return; int k = j * _nx + i; if (!nothing[k] || open[k]) return; open[k] = true; queue.Enqueue(k); }
        for (int i = 0; i < _nx; i++) { Visit(i, 0); Visit(i, _ny - 1); }
        for (int j = 0; j < _ny; j++) { Visit(0, j); Visit(_nx - 1, j); }
        while (queue.Count > 0) { int k = queue.Dequeue(), i = k % _nx, j = k / _nx; Visit(i + 1, j); Visit(i - 1, j); Visit(i, j + 1); Visit(i, j - 1); }
        _lake = new bool[_nx * _ny];
        for (int k = 0; k < _lake.Length; k++) _lake[k] = nothing[k] && !open[k];
    }

    private int _cell;

    private int ClassifyTriangle(int a, int b, int c)
    {
        int layer = _plan.Classify((_x[a] + _x[b] + _x[c]) / 3, (_y[a] + _y[b] + _y[c]) / 3);
        return layer == TerrainPlan.None && _lake != null && _lake[_cell] ? TerrainPlan.Sea : layer;
    }

    private double Cross(int a, int b, int c) => (_x[b] - _x[a]) * (_y[c] - _y[a]) - (_y[b] - _y[a]) * (_x[c] - _x[a]);

    /// <summary>Every cell of the sheet becomes a four-cornered face of the layer it lies in, or two triangles where a
    /// line of the plan crosses it cornerwise.</summary>
    private void Cells()
    {
        Lakes();
        for (int j = 0; j < _ny; j++)
            for (int i = 0; i < _nx; i++)
            {
                _cell = j * _nx + i;
                int a = At(i, j), b = At(i + 1, j), c = At(i + 1, j + 1), d = At(i, j + 1);
                int abc = ClassifyTriangle(a, b, c), acd = ClassifyTriangle(a, c, d), abd = ClassifyTriangle(a, b, d), bcd = ClassifyTriangle(b, c, d);
                bool convex = Cross(a, b, c) > 1 && Cross(b, c, d) > 1 && Cross(c, d, a) > 1 && Cross(d, a, b) > 1;
                if (abc == acd && abd == bcd && abc == abd && convex)
                {
                    if (abc != TerrainPlan.None) _faces.Add((abc, [a, b, c, d]));
                    continue;
                }
                bool splitAc = abc != acd, splitBd = abd != bcd;
                bool useAc;
                if (!convex) useAc = Cross(a, b, c) > 1 && Cross(a, c, d) > 1;
                else if (splitAc != splitBd) useAc = splitAc;
                else useAc = _snapped[a] && _snapped[c] || !(_snapped[b] && _snapped[d]);
                if (useAc) { Triangle(abc, a, b, c); Triangle(acd, a, c, d); }
                else { Triangle(abd, a, b, d); Triangle(bcd, b, c, d); }
            }
    }

    private void Triangle(int layer, int a, int b, int c)
    {
        if (layer != TerrainPlan.None && Cross(a, b, c) > 1) _faces.Add((layer, [a, b, c]));
    }

    /// <summary>Each layer's height at each corner it uses; where two layers at a corner are within a small step of each
    /// other they take one height, so floors that are meant to meet do (Spyro cannot walk up the smallest step).</summary>
    private void Heights()
    {
        _z = new Dictionary<int, double>[_x.Length];
        foreach (var (layer, v) in _faces)
            foreach (int k in v)
            {
                var at = _z[k] ??= [];
                if (!at.ContainsKey(layer)) at[layer] = _plan.Height(layer, _x[k], _y[k]);
            }
        foreach (var at in _z)
        {
            if (at == null || at.Count < 2) continue;
            var order = at.OrderBy(p => p.Value).ThenBy(p => p.Key).ToList();
            int from = 0;
            for (int k = 1; k <= order.Count; k++)
            {
                if (k < order.Count && order[k].Value - order[k - 1].Value <= Weld) continue;
                if (k - from > 1)
                {
                    // Water keeps its level; otherwise the middle.
                    var group = order.GetRange(from, k - from);
                    var liquid = group.Where(p => _plan.KindOf(p.Key) != null).ToList();
                    double z = liquid.Count > 0 ? liquid[0].Value : group.Average(p => p.Value);
                    foreach (var p in group) at[p.Key] = z;
                }
                from = k;
            }
        }
    }

    private Vector3f Point(int k, double z) => new((float)Math.Round(_x[k]), (float)Math.Round(_y[k]), (float)Math.Round(z));
    private Vector3f Point(int k, int layer) => Point(k, _z[k][layer]);

    private void Emit()
    {
        // Floors.
        var edges = new Dictionary<(int, int), List<(int Face, int A, int B)>>();
        for (int f = 0; f < _faces.Count; f++)
        {
            var v = _faces[f].V;
            for (int k = 0; k < v.Length; k++)
            {
                int a = v[k], b = v[(k + 1) % v.Length];
                var key = a < b ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out var list)) edges[key] = list = [];
                list.Add((f, a, b));
            }
        }
        Seams(edges);
        for (int f = 0; f < _faces.Count; f++)
        {
            var (layer, v) = _faces[f];
            var corners = v.Select(k => Point(k, layer)).ToArray();
            double cx = v.Average(k => _x[k]), cy = v.Average(k => _y[k]);
            double nz = Up(corners);
            var role = _plan.Role(layer, cx, cy, nz);
            var light = v.Select(k => _plan.Light(layer, _x[k], _y[k])).ToArray();
            var piece = Clean(corners, role, light, _plan.KindOf(layer), layer, false);
            if (piece == null) continue;
            // Under a sewn cut: the game's own floor is there; kept only for the way-through check.
            if (Sewn.Contains(layer)) { Floors.Add((layer, v, piece)); continue; }
            Pieces.Add(piece);
            Floors.Add((layer, v, piece));
            // A rock roof over it: the same face turned over, higher up.
            if (Roofed(f))
            {
                var up = v.Reverse().Select(k => Point(k, _z[k][layer] + _plan.RoofAt(layer, _x[k], _y[k]))).ToArray();
                var dark = v.Reverse().Select(k => Math.Clamp(0.12 + 0.3 * _plan.Light(layer, _x[k], _y[k]), 0, 1)).ToArray();
                if (Clean(up, SurfaceRole.Ceiling, dark, null, layer, false) is { } roof) Pieces.Add(roof);
            }
            // A bridge in the sky is seen from below too.
            if (_plan.Kind == EnclosureKind.Sky && layer >= 0 && _plan.Zones[layer].Kind == "bridge")
            {
                var under = v.Reverse().Select(k => Point(k, _z[k][layer] - 44)).ToArray();
                if (Clean(under, SurfaceRole.Wood, [.. v.Select(_ => 0.25)], null, layer, false) is { } below) Pieces.Add(below with { Collides = false });
            }
        }
        // Walls, where two layers meet at different heights; in the order of the sheet, so the level is the same every time.
        foreach (var (key, list) in edges.OrderBy(e => e.Key.Item1).ThenBy(e => e.Key.Item2))
        {
            if (list.Count == 1 && _plan.Kind == EnclosureKind.Sky) { Underside(list[0].Face, list[0].A, list[0].B); continue; }
            if (list.Count != 2) continue;
            var (f1, a1, b1) = list[0]; var (f2, _, _) = list[1];
            int l1 = _faces[f1].Layer, l2 = _faces[f2].Layer;
            if (Sewn.Contains(l1) || Sewn.Contains(l2)) continue;
            if (Roofed(f1) != Roofed(f2) && l1 != TerrainPlan.Enclosure && l2 != TerrainPlan.Enclosure) Fascia(Roofed(f1) ? f1 : f2, Roofed(f1) ? f2 : f1, a1, b1, Roofed(f1));
            if (l1 == l2) continue;
            // Along face 1's own direction a -> b.
            double za1 = _z[a1][l1], zb1 = _z[b1][l1], za2 = _z[a1][l2], zb2 = _z[b1][l2];
            if (za1 == za2 && zb1 == zb2) continue;
            if ((za1 - za2) * (zb1 - zb2) < 0)
            {
                // The two floors cross along this line: a wall each side of the crossing.
                double t = (za1 - za2) / ((za1 - za2) - (zb1 - zb2));
                double mx = _x[a1] + (_x[b1] - _x[a1]) * t, my = _y[a1] + (_y[b1] - _y[a1]) * t, mz = za1 + (zb1 - za1) * t;
                var mid = new Vector3f((float)Math.Round(mx), (float)Math.Round(my), (float)Math.Round(mz));
                if (za1 < za2) { WallPart(l1, l2, Point(a1, za1), mid, Point(a1, za2), mid); WallPart(l2, l1, mid, Point(b1, zb2), mid, Point(b1, zb1), reverse: true); }
                else { WallPart(l2, l1, Point(a1, za2), mid, Point(a1, za1), mid, reverse: true); WallPart(l1, l2, mid, Point(b1, zb1), mid, Point(b1, zb2)); }
                continue;
            }
            bool firstLower = za1 + zb1 < za2 + zb2;
            if (firstLower) WallPart(l1, l2, Point(a1, za1), Point(b1, zb1), Point(a1, za2), Point(b1, zb2));
            else WallPart(l2, l1, Point(a1, za2), Point(b1, zb2), Point(a1, za1), Point(b1, zb1), reverse: true);
        }
    }

    /// <summary>The ground between the sheet and each part cut from one of the game's levels: the sheet stops where the
    /// cut's zone begins (its edges there, walked round, with the heights of the floors they belong to) and the cut's
    /// floor edge lies inside; the space between is filled with faces joining the two edges corner to corner, so the
    /// floors meet without a crack. Lit and surfaced as the room round it.</summary>
    private void Seams(Dictionary<(int, int), List<(int Face, int A, int B)>> edges)
    {
        foreach (var zone in _plan.Zones)
        {
            if (zone.Cut is not { } cut) continue;
            // The sheet's edges against the cut's zone, each in its own face's direction (the zone on its right).
            var next = new Dictionary<int, (int B, double Z)>();
            bool ok = true;
            foreach (var list in edges.Values)
            {
                if (list.Count != 2) continue;
                int l0 = _faces[list[0].Face].Layer, l1 = _faces[list[1].Face].Layer;
                if ((l0 == zone.Id) == (l1 == zone.Id)) continue;
                var (face, a, b) = l0 == zone.Id ? list[1] : list[0];
                int layer = _faces[face].Layer;
                if (next.ContainsKey(a)) { ok = false; break; }
                next[a] = (b, _z[a][layer]);
            }
            if (!ok || next.Count < 3) continue;
            // Walked round: the longest way round the zone.
            var seen = new HashSet<int>();
            List<int>? best = null; double bestArea = 0;
            foreach (int s in next.Keys.OrderBy(k => k))
            {
                if (seen.Contains(s)) continue;
                var loop = new List<int>();
                int at = s; bool closed = false;
                while (next.TryGetValue(at, out var e) && !seen.Contains(at))
                {
                    seen.Add(at); loop.Add(at); at = e.B;
                    if (at == s) { closed = true; break; }
                }
                if (!closed) continue;
                double area = 0;
                for (int k = 0; k < loop.Count; k++) area += _x[loop[k]] * _y[loop[(k + 1) % loop.Count]] - _x[loop[(k + 1) % loop.Count]] * _y[loop[k]];
                // Round a hole the sheet's edges run clockwise.
                if (-area > bestArea) { bestArea = -area; best = loop; }
            }
            if (best == null) continue;
            best.Reverse();
            var rim = cut.Rim;
            var outer = best.Select(k => (_x[k], _y[k])).ToList();
            var inner = rim.Select(v => ((double)v.X, (double)v.Y)).ToList();
            if (LevelCuts.Sew(outer, inner) is not { } tris) continue;
            var points = best.Select(k => Point(k, next[k].Z)).Concat(rim).ToList();
            int room = _plan.Zones.FirstOrDefault(z => z.Kind == "room" && z.Area == zone.Area)?.Id ?? TerrainPlan.Fill;
            var seam = new List<TerrainPiece>();
            foreach (var (a, b, c) in tris)
            {
                Vector3f[] corners = [points[a], points[b], points[c]];
                double cx = corners.Average(p => p.X), cy = corners.Average(p => p.Y);
                var role = _plan.Role(room, cx, cy, Up(corners));
                var light = corners.Select(p => _plan.Light(room, p.X, p.Y)).ToArray();
                if (Clean(corners, role, light, null, room, false) is { } piece) seam.Add(piece);
            }
            if (seam.Count == 0) continue;
            Pieces.AddRange(seam);
            Sewn.Add(zone.Id);
        }
    }

    /// <summary>A wall between a lower floor's edge p -> q (in the direction the lower face runs it, or against it when
    /// <paramref name="reverse"/>) and the higher floor's edge above it, facing the lower floor, in bands.</summary>
    private void WallPart(int low, int high, Vector3f lowP, Vector3f lowQ, Vector3f highP, Vector3f highQ, bool reverse = false)
    {
        if (reverse) { (lowP, lowQ) = (lowQ, lowP); (highP, highQ) = (highQ, highP); }
        double hp = highP.Z - lowP.Z, hq = highQ.Z - lowQ.Z, tall = Math.Max(hp, hq);
        if (tall < 1) return;
        int bands = Math.Max(1, (int)Math.Ceiling(tall / BandHeight));
        for (int k = 0; k < bands; k++)
        {
            double t0 = k / (double)bands, t1 = (k + 1) / (double)bands;
            Vector3f P(Vector3f lo, double h, double t) => new(lo.X, lo.Y, (float)Math.Round(lo.Z + h * t));
            // Top edge first: seen from the lower floor, top right (p), top left (q), bottom left, bottom right.
            Vector3f[] c = [P(lowP, hp, t1), P(lowQ, hq, t1), P(lowQ, hq, t0), P(lowP, hp, t0)];
            double[] light = [WallLight(t1, c[0]), WallLight(t1, c[1]), WallLight(t0, c[2]), WallLight(t0, c[3])];
            var role = _plan.WallRole(low, high, k == bands - 1, tall);
            var piece = Clean(c, role, light, null, high, true);
            if (piece != null) Pieces.Add(piece);
        }
    }

    private bool Roofed(int face)
    {
        var (layer, v) = _faces[face];
        return layer >= 0 && _plan.Zones[layer].Roof > 0 && v.All(k => _plan.RoofAt(layer, _x[k], _y[k]) > 0);
    }

    /// <summary>Where a roofed floor meets open floor: the rock face over the cave's mouth, from the roof's edge up to
    /// the top of the rock, facing the open side. a -> b is the first face's direction (the roofed one when
    /// <paramref name="roofedFirst"/>).</summary>
    private void Fascia(int roofed, int open, int a, int b, bool roofedFirst)
    {
        int layer = _faces[roofed].Layer;
        // Seen from the open side: its face runs the edge b -> a when the roofed face runs it a -> b.
        var (p, q) = roofedFirst ? (b, a) : (a, b);
        double rp = _z[p][layer] + _plan.RoofAt(layer, _x[p], _y[p]), rq = _z[q][layer] + _plan.RoofAt(layer, _x[q], _y[q]);
        double tp = Math.Max(rp + 40, _plan.Height(TerrainPlan.Enclosure, _x[p], _y[p])), tq = Math.Max(rq + 40, _plan.Height(TerrainPlan.Enclosure, _x[q], _y[q]));
        WallPart(_faces[open].Layer, layer, Point(p, rp), Point(q, rq), Point(p, tp), Point(q, tq));
    }

    /// <summary>The edge of floating land: a band of rock straight down, then the underside running in to a point below.</summary>
    private void Underside(int face, int a, int b)
    {
        int layer = _faces[face].Layer;
        if (layer < 0 && layer != TerrainPlan.Fill) return;
        bool thin = layer >= 0 && _plan.Zones[layer].Kind is "bridge" or "stone";
        double drop = thin ? 44 : 96;
        // The outside is on the right of a -> b: the wall is seen from there with b on its left.
        Vector3f tb = Point(b, layer), ta = Point(a, layer), bb = new(tb.X, tb.Y, tb.Z - (float)drop), ba = new(ta.X, ta.Y, ta.Z - (float)drop);
        if (Clean([tb, ta, ba, bb], thin ? SurfaceRole.Wood : SurfaceRole.CliffTop, [0.7, 0.7, 0.35, 0.35], null, layer, true) is { } band) Pieces.Add(band);
        var (ax, ay, az) = _plan.Apex(layer, (_x[a] + _x[b]) / 2, (_y[a] + _y[b]) / 2);
        if (layer >= 0 && _plan.Zones[layer].Kind == "bridge")
        {
            // A bridge has a flat underside: its floor faces turned over (made with the floors), nothing more here.
            return;
        }
        var apex = new Vector3f((float)Math.Round(ax), (float)Math.Round(ay), (float)Math.Round(Math.Min(az, Math.Min(bb.Z, ba.Z) - 60)));
        if (Clean([bb, ba, apex], SurfaceRole.Underside, [0.4, 0.4, 0.05], null, layer, true) is { } cone) Pieces.Add(cone with { Collides = false });
    }

    /// <summary>What Spyro can reach: floors he can stand on are joined where they meet level, where one is a jump above
    /// the other (both ways) or higher (down only), and by the plan's glides. Counts the floors he must reach, those he
    /// cannot reach from the start and those he cannot get back from.</summary>
    public (int Required, int Unreached, int NoWayBack, List<(double X, double Y)> Bad) Reach(double startX, double startY,
        IEnumerable<(int From, int To)> glides)
    {
        int n = Floors.Count;
        var stand = new bool[n];
        var index = new Dictionary<int, List<int>>();
        var edge = new Dictionary<(int, int), List<int>>();
        double limit = Math.Cos(SpyroMoves.MaxWalkSlopeDegrees * Math.PI / 180);
        for (int f = 0; f < n; f++)
        {
            var (layer, v, piece) = Floors[f];
            // A charge ramp counts: Spyro climbs it charging.
            bool charge = layer >= 0 && _plan.Zones[layer].Kind == "charge";
            stand[f] = layer != TerrainPlan.Enclosure && layer != TerrainPlan.Sea && piece.Kind == null &&
                Up(piece.Corners) >= (charge ? Math.Cos(SpyroMoves.MaxChargeSlopeDegrees * Math.PI / 180) : limit);
            if (layer >= 0) { if (!index.TryGetValue(layer, out var l)) index[layer] = l = []; l.Add(f); }
            for (int k = 0; k < v.Length; k++)
            {
                int a = v[k], b = v[(k + 1) % v.Length];
                var key = a < b ? (a, b) : (b, a);
                if (!edge.TryGetValue(key, out var list)) edge[key] = list = [];
                list.Add(f);
            }
        }
        var forward = new List<int>[n]; var back = new List<int>[n];
        for (int f = 0; f < n; f++) { forward[f] = []; back[f] = []; }
        void Link(int from, int to) { forward[from].Add(to); back[to].Add(from); }
        foreach (var ((a, b), list) in edge)
        {
            if (list.Count != 2 || !stand[list[0]] || !stand[list[1]]) continue;
            int f = list[0], g = list[1], lf = Floors[f].Layer, lg = Floors[g].Layer;
            double df = Math.Max(_z[a][lf] - _z[a][lg], _z[b][lf] - _z[b][lg]), dg = Math.Max(_z[a][lg] - _z[a][lf], _z[b][lg] - _z[b][lf]);
            // f above g by df at most, g above f by dg at most.
            if (df <= SpyroMoves.MaxLedge - 4) Link(g, f);
            if (dg <= SpyroMoves.MaxLedge - 4) Link(f, g);
        }
        foreach (var (from, to) in glides)
            if (index.TryGetValue(from, out var tops) && index.TryGetValue(to, out var landings))
                foreach (int t in tops.Where(t => stand[t])) foreach (int l in landings.Where(l => stand[l]).Take(3)) Link(t, l);
        // Jumps across a gap (stepping stones, a ledge across a channel): floors whose middles are within a running
        // jump, the far one no more than a ledge higher. Where land stands free (islands), glides too: from a floor to
        // any lower one a jump and glide carries to.
        var mid = new (double X, double Y, double Z)[n];
        for (int f = 0; f < n; f++) { var c = Floors[f].Piece.Corners; mid[f] = (c.Average(p => p.X), c.Average(p => p.Y), c.Average(p => p.Z)); }
        bool free = _plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky;
        double cell = 256;
        var grid = new Dictionary<(int, int), List<int>>();
        for (int f = 0; f < n; f++)
        {
            if (!stand[f]) continue;
            var key = ((int)Math.Floor(mid[f].X / cell), (int)Math.Floor(mid[f].Y / cell));
            if (!grid.TryGetValue(key, out var list)) grid[key] = list = [];
            list.Add(f);
        }
        for (int f = 0; f < n; f++)
        {
            if (!stand[f]) continue;
            int gx = (int)Math.Floor(mid[f].X / cell), gy = (int)Math.Floor(mid[f].Y / cell);
            for (int i = gx - 1; i <= gx + 1; i++)
                for (int j = gy - 1; j <= gy + 1; j++)
                {
                    if (!grid.TryGetValue((i, j), out var near)) continue;
                    foreach (int g in near)
                    {
                        if (g == f) continue;
                        if (mid[g].Z - mid[f].Z > SpyroMoves.DesignLedge) continue;
                        // The nearest corners of the two floors within an easy jump (Spyro jumps 185 on the flat).
                        double d = double.MaxValue;
                        foreach (var p in Floors[f].Piece.Corners) foreach (var q in Floors[g].Piece.Corners)
                            d = Math.Min(d, Math.Sqrt((p.X - q.X) * (double)(p.X - q.X) + (p.Y - q.Y) * (double)(p.Y - q.Y)));
                        if (d <= SpyroMoves.DesignJumpGap + 24) Link(f, g);
                    }
                }
        }
        if (free)
            for (int f = 0; f < n; f++)
            {
                if (!stand[f]) continue;
                for (int g = 0; g < n; g++)
                {
                    if (g == f || !stand[g] || mid[g].Z >= mid[f].Z) continue;
                    double d = Math.Sqrt((mid[f].X - mid[g].X) * (mid[f].X - mid[g].X) + (mid[f].Y - mid[g].Y) * (mid[f].Y - mid[g].Y));
                    if (d - 128 <= SpyroMoves.GlideReach((float)(mid[f].Z - mid[g].Z), 0.2f)) Link(f, g);
                }
            }
        int start = -1; double best = double.MaxValue;
        for (int f = 0; f < n; f++)
        {
            if (!stand[f]) continue;
            var c = Floors[f].Piece.Corners;
            double d = Math.Pow(c.Average(p => p.X) - startX, 2) + Math.Pow(c.Average(p => p.Y) - startY, 2);
            if (d < best) { best = d; start = f; }
        }
        bool[] Flood(List<int>[] graph)
        {
            var seen = new bool[n];
            if (start < 0) return seen;
            var queue = new Queue<int>(); queue.Enqueue(start); seen[start] = true;
            while (queue.Count > 0) foreach (int g in graph[queue.Dequeue()]) if (!seen[g]) { seen[g] = true; queue.Enqueue(g); }
            return seen;
        }
        var reached = Flood(forward); var returns = Flood(back);
        int required = 0, unreached = 0, noWayBack = 0; var bad = new List<(double, double)>();
        for (int f = 0; f < n; f++)
        {
            // Floors Spyro need not reach: fill between floors, a house's top and the top of a castle wall.
            if (!stand[f] || Floors[f].Layer == TerrainPlan.Fill || Floors[f].Layer >= 0 && SpyroPlanner.IsBuilt(_plan.Zones[Floors[f].Layer].Kind)) continue;
            required++;
            if (!reached[f]) unreached++; else if (!returns[f]) noWayBack++;
            if (!reached[f] || !returns[f]) { var c = Floors[f].Piece.Corners; bad.Add((c.Average(p => p.X), c.Average(p => p.Y))); }
        }
        return (required, unreached, noWayBack, bad);
    }

    private double WallLight(double t, Vector3f p) =>
        Math.Clamp(0.2 + 0.78 * t * Math.Sqrt(t) + 0.08 * _plan.Noise.At(p.X / 300.0 + 61, p.Y / 300.0 + 3), 0, 1);

    private static double Up(Vector3f[] p)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int k = 0; k < p.Length; k++)
        {
            var a = p[k]; var b = p[(k + 1) % p.Length];
            nx += (a.Y - b.Y) * (double)(a.Z + b.Z); ny += (a.Z - b.Z) * (double)(a.X + b.X); nz += (a.X - b.X) * (double)(a.Y + b.Y);
        }
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len <= 0 ? 0 : nz / len;
    }

    private static bool Flat(Vector3f a, Vector3f b, Vector3f c)
    {
        double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
        return x * x + y * y + z * z < 4;
    }

    /// <summary>A face the game can take: no corner twice, and neither half of a four-cornered face without area (the
    /// level's collision is made of the halves 0-1-2 and 0-2-3).</summary>
    internal static TerrainPiece? Clean(Vector3f[] c, SurfaceRole role, double[] light, AuthoredSurfaceKind? kind, int layer, bool wall)
    {
        var corners = new List<Vector3f>(); var lights = new List<double>();
        for (int k = 0; k < c.Length; k++)
        {
            if (corners.Count > 0 && corners[^1] == c[k]) continue;
            corners.Add(c[k]); lights.Add(light[k]);
        }
        if (corners.Count > 1 && corners[0] == corners[^1]) { corners.RemoveAt(corners.Count - 1); lights.RemoveAt(lights.Count - 1); }
        if (corners.Count < 3) return null;
        if (corners.Count == 4)
        {
            bool first = Flat(corners[0], corners[1], corners[2]), second = Flat(corners[0], corners[2], corners[3]);
            if (first || second)
            {
                // Turn the face one corner on: the other cut may have area on both sides.
                bool third = Flat(corners[1], corners[2], corners[3]), fourth = Flat(corners[1], corners[3], corners[0]);
                if (!third && !fourth && !wall)
                {
                    corners = [corners[1], corners[2], corners[3], corners[0]]; lights = [lights[1], lights[2], lights[3], lights[0]];
                }
                else if (first && second) return null;
                else if (first) { corners.RemoveAt(1); lights.RemoveAt(1); }
                else { corners.RemoveAt(3); lights.RemoveAt(3); }
            }
        }
        if (corners.Count == 3 && Flat(corners[0], corners[1], corners[2])) return null;
        return new TerrainPiece([.. corners], role, [.. lights], kind, layer, wall);
    }
}
