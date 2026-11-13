using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>A part of one of the game's levels chosen to be cut out whole: the floor inside an outline drawn round it
/// (in the level's own coordinates) with every wall, tower and roof standing on it, between two heights. <see cref="Swap"/>:
/// pairs of textures, one a built level cannot carry (a frame of the game's moving pictures) and the still picture of
/// the same level most like it, shown in its place.</summary>
internal sealed record CutDefinition(string Level, string Name, double[] Outline, double ZLow = double.MinValue, double ZHigh = double.MaxValue, int[]? Swap = null);

/// <summary>A part cut from one of the game's levels: its faces as the level has them, about the middle of its floor's
/// outer edge (x, y) and that edge's mean height (z), and the edge itself.</summary>
internal sealed class LevelCut
{
    public required string Name { get; init; }
    public required string Level { get; init; }
    public required IReadOnlyList<SourceFace> Faces { get; init; }
    /// <summary>The floor's outer edge, counter-clockwise seen from above: corners of the floor's own faces.</summary>
    public required Vector3f[] Rim { get; init; }
    /// <summary>How far the edge reaches from the middle.</summary>
    public double Radius { get; init; }
}

/// <summary>A cut stood in a new level: at a point, turned, its edge's mean height at <see cref="Z"/>.</summary>
internal sealed class PlacedCut
{
    public required LevelCut Cut { get; init; }
    public double X, Y, Z, Angle;
    private (double X, double Y, double Z, double Angle) _at = (double.NaN, 0, 0, 0);
    private Vector3f[] _rim = [];
    private (double X, double Y)[] _outline = [];

    /// <summary>A point of the cut where the new level has it; rounded as <see cref="LevelProps.Place"/> rounds the
    /// cut's faces, so the edge lies exactly on their corners.</summary>
    public Vector3f At(Vector3f v)
    {
        double c = SpyroPlanner.Cos(Angle), s = SpyroPlanner.Sin(Angle);
        return new((float)Math.Round(X + v.X * c - v.Y * s), (float)Math.Round(Y + v.X * s + v.Y * c), (float)Math.Round(Z - 0 + v.Z));
    }

    /// <summary>The floor's outer edge where the new level has it, counter-clockwise.</summary>
    public Vector3f[] Rim
    {
        get
        {
            if (_at != (X, Y, Z, Angle))
            {
                _rim = [.. Cut.Rim.Select(At)];
                _outline = [.. _rim.Select(v => ((double)v.X, (double)v.Y))];
                _at = (X, Y, Z, Angle);
            }
            return _rim;
        }
    }

    /// <summary>Signed distance from the edge (negative inside).</summary>
    public double Sdf(double x, double y)
    {
        _ = Rim;
        return TerrainHull.Distance(_outline, x, y);
    }

    /// <summary>The edge's height near a point: the edge's corners weighted by nearness.</summary>
    public double RimHeight(double x, double y)
    {
        double sum = 0, weights = 0;
        foreach (var v in Rim)
        {
            double d2 = (v.X - x) * (v.X - x) + (v.Y - y) * (v.Y - y) + 400;
            double w = 1 / (d2 * d2);
            sum += w * v.Z; weights += w;
        }
        return weights <= 0 ? Z : sum / weights;
    }

    /// <summary>The cut's faces where the new level has them.</summary>
    public IEnumerable<TerrainPiece> Faces() =>
        LevelProps.Place(new LevelProp(Cut.Faces, Cut.Radius, 0, Cut.Faces.Select(f => f.TextureId).ToHashSet()), X, Y, Z, Angle, 0);
}

/// <summary>Parts of the game's own levels, cut out whole and stood in new levels (the walled yards, keeps and hills
/// that make a level of the game what it is), read from the user's own disc (the editor's level cache). A new level
/// takes its cuts from the level whose look it has, so every texture is that level's. The generator gives a cut a room
/// of its own, sews the room's floor to the cut's floor edge all round (<see cref="TerrainMesher"/>) and leaves the
/// cut's inside as the game has it.</summary>
internal static class LevelCuts
{
    /// <summary>How far past a cut's floor edge the room's own floor stops: the seam between is made of faces joining
    /// the two edges corner to corner.</summary>
    public const double Margin = 110;
    /// <summary>Open floor all round a cut, beyond the seam, to walk round it to its ways in.</summary>
    public const double Walk = 160;

    /// <summary>The cuts, outlined by hand on the game's levels (study: scratch pieces/zoom.py maps).</summary>
    internal static readonly CutDefinition[] Table =
    [
        // Stone Hill: the keep in its walled yard, its corner towers and its gate.
        // (Its purple scrollwork bands are moving pictures: the plain purple stone with the same cream edges instead.)
        new("stonehill", "the keep", [7480, 8900, 9320, 8900, 9320, 10580, 7480, 10580], 1000, Swap: [22, 18, 23, 17]),
        // Stone Hill: the field walled round with towers, gates west and east.
        new("stonehill", "the walled field", [6880, 8300, 7000, 7600, 7400, 7180, 8200, 7060, 8900, 7180, 9400, 7600, 9560, 8300, 9420, 8980, 9020, 9260, 7400, 9260, 7020, 9020], 1000, Swap: [22, 18, 23, 17, 6, 3]),
        // Artisans: the round hill ringed by its terraces.
        new("artisans", "the round hill", [4930, 4150, 4887, 4349, 4766, 4518, 4584, 4630, 4370, 4670, 4156, 4630, 3974, 4518, 3853, 4349, 3810, 4150, 3853, 3951, 3974, 3782, 4156, 3670, 4370, 3630, 4584, 3670, 4766, 3782, 4887, 3951]),
        // Artisans: the sunken bowl before the castle, its rings and its doorway stands.
        new("artisans", "the castle bowl", [9260, 3170, 9185, 3641, 8973, 4040, 8655, 4306, 8280, 4400, 7905, 4306, 7587, 4040, 7375, 3641, 7300, 3170, 7375, 2699, 7587, 2300, 7905, 2034, 8280, 1940, 8655, 2034, 8973, 2300, 9185, 2699]),
    ];

    /// <summary>Every cut of a level that can be read out of it.</summary>
    public static List<LevelCut> For(LevelPropSource source, Func<int, bool> usable) =>
        [.. Table.Where(d => d.Level == source.LevelKey).Select(d => Extract(source, d, usable)).OfType<LevelCut>()];

    private static double Up(SourceFace f)
    {
        double nx = 0, ny = 0, nz = 0; var p = f.Corners;
        for (int k = 0; k < p.Length; k++)
        {
            var a = p[k]; var b = p[(k + 1) % p.Length];
            nx += (a.Y - b.Y) * (double)(a.Z + b.Z); ny += (a.Z - b.Z) * (double)(a.X + b.X); nz += (a.X - b.X) * (double)(a.Y + b.Y);
        }
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len <= 0 ? 0 : (f.Flip ? -nz : nz) / len;
    }

    /// <summary>The cut, or null when its outline is not convex, it holds too little floor or too much of it cannot be
    /// carried. The floor is trimmed to the outline exactly (a tile across it is cut along it, its paint carried to the
    /// new corners), so the cut's edge is the outline: the corners of the trimmed floor along it, and between them, where
    /// no floor reaches the outline (under a wall), the outline's own corners at the height of the floor either side.
    /// Everything else stands in the cut when its middle is inside the outline.</summary>
    public static LevelCut? Extract(LevelPropSource source, CutDefinition def, Func<int, bool> usable)
    {
        bool debug = Environment.GetEnvironmentVariable("SPYRO_RANDOM_LEVEL_DEBUG") == "cut";
        var outline = new (double X, double Y)[def.Outline.Length / 2];
        for (int k = 0; k < outline.Length; k++) outline[k] = (def.Outline[2 * k], def.Outline[2 * k + 1]);
        int n = outline.Length;
        double turn = 0;
        for (int k = 0; k < n; k++) turn += outline[k].X * outline[(k + 1) % n].Y - outline[(k + 1) % n].X * outline[k].Y;
        if (turn < 0) Array.Reverse(outline);
        for (int k = 0; k < n; k++)
            if (Cross(outline[k], outline[(k + 1) % n], outline[(k + 2) % n]) < 0) { if (debug) Console.Error.WriteLine($"cut {def.Name}: outline not convex"); return null; }
        // How far round the outline each of its corners is.
        var at = new double[n + 1];
        for (int k = 0; k < n; k++) at[k + 1] = at[k] + Dist(outline[k], outline[(k + 1) % n]);
        double around = at[n];
        // How far round the outline a point on it is (-1: not on it).
        double Along(double x, double y)
        {
            for (int k = 0; k < n; k++)
            {
                var a = outline[k]; var b = outline[(k + 1) % n];
                double len = at[k + 1] - at[k], t = ((x - a.X) * (b.X - a.X) + (y - a.Y) * (b.Y - a.Y)) / (len * len);
                if (t < -1e-6 || t > 1 + 1e-6) continue;
                double px = a.X + (b.X - a.X) * t - x, py = a.Y + (b.Y - a.Y) * t - y;
                if (px * px + py * py < 0.01) return (at[k] + len * Math.Clamp(t, 0, 1)) % around;
            }
            return -1;
        }
        var swap = new Dictionary<int, int>();
        for (int k = 0; def.Swap != null && k + 1 < def.Swap.Length; k += 2) swap[def.Swap[k]] = def.Swap[k + 1];
        var faces = new List<SourceFace>();
        var marks = new List<(double T, Vector3f P)>();
        var covered = new List<(double From, double To)>();
        int all = 0, dropped = 0, floors = 0;
        foreach (var face in source.Faces)
        {
            var f = swap.TryGetValue(face.TextureId, out int still) ? face with { TextureId = still } : face;
            double cx = f.Corners.Average(v => v.X), cy = f.Corners.Average(v => v.Y), cz = f.Corners.Average(v => v.Z);
            if (cz < def.ZLow || cz > def.ZHigh) continue;
            if (Up(f) < 0.72)
            {
                if (TerrainHull.Distance(outline, cx, cy) >= 0) continue;
                all++;
                if (usable(f.TextureId)) faces.Add(f); else dropped++;
                continue;
            }
            if (f.Corners.All(v => TerrainHull.Distance(outline, v.X, v.Y) <= 0))
            {
                all++; floors++;
                if (usable(f.TextureId)) faces.Add(f); else dropped++;
                continue;
            }
            // A tile across the outline: the part inside, in triangles.
            var part = Clip(f, outline);
            if (part.Count < 3) continue;
            all++; floors++;
            if (!usable(f.TextureId)) { dropped++; continue; }
            var corners = part.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray();
            var paint = part.Select(p => new ColorRgba((byte)Math.Clamp(Math.Round(p.R), 0, 255), (byte)Math.Clamp(Math.Round(p.G), 0, 255), (byte)Math.Clamp(Math.Round(p.B), 0, 255))).ToArray();
            for (int k = 1; k + 1 < corners.Length; k++)
                faces.Add(f with { Corners = [corners[0], corners[k], corners[k + 1]], Near = [paint[0], paint[k], paint[k + 1]] });
            // Its corners on the outline, and the stretches of outline its edges run along.
            var on = part.Select(p => Along(p.X, p.Y)).ToArray();
            for (int k = 0; k < part.Count; k++)
            {
                if (on[k] < 0) continue;
                marks.Add((on[k], corners[k]));
                int next = (k + 1) % part.Count;
                if (on[next] < 0) continue;
                // The stretch of outline between this corner and the next (the short way round).
                covered.Add(Gap(on[k], on[next]) <= around / 2 ? (on[k], on[next]) : (on[next], on[k]));
            }
        }
        if (debug) Console.Error.WriteLine($"cut {def.Name}: {all} faces, {floors} floor, {dropped} with textures that cannot be carried, {marks.Count} floor corners on the outline");
        if (floors < 12 || dropped > 0.08 * all || marks.Count < 4) return null;
        // The edge: the floor's corners on the outline in order round it (one at a point: the lowest); between two
        // with no floor along the outline between them, the outline's corners and a point every so often, their height
        // running evenly from the one to the other.
        marks.Sort((a, b) => a.T.CompareTo(b.T));
        var edge = new List<(double T, Vector3f P)>();
        foreach (var m in marks)
        {
            if (edge.Count > 0 && (Math.Abs(edge[^1].T - m.T) < 0.5 || Math.Round(edge[^1].P.X) == Math.Round(m.P.X) && Math.Round(edge[^1].P.Y) == Math.Round(m.P.Y)))
            {
                if (m.P.Z < edge[^1].P.Z) edge[^1] = m;
                continue;
            }
            edge.Add(m);
        }
        if (edge.Count > 1 && (Math.Abs(edge[0].T + around - edge[^1].T) < 0.5)) { if (edge[^1].P.Z < edge[0].P.Z) edge[0] = edge[^1]; edge.RemoveAt(edge.Count - 1); }
        bool Covered(double from, double to)
        {
            double mid = from + Gap(from, to) / 2;
            return covered.Any(c => Gap(c.From, mid % around) <= Gap(c.From, c.To) + 1e-6);
        }
        double Gap(double from, double to) => ((to - from) % around + around) % around;
        var rim = new List<Vector3f>();
        for (int k = 0; k < edge.Count; k++)
        {
            var (t0, p0) = edge[k]; var (t1, p1) = edge[(k + 1) % edge.Count];
            rim.Add(p0);
            double gap = edge.Count == 1 ? around : Gap(t0, t1);
            if (gap < 1 || Covered(t0, t1)) continue;
            // Under a wall, or a gap in the floor: the outline's corners and a point every 120 units.
            var between = new List<double>();
            for (int c = 0; c < n; c++) { double g = Gap(t0, at[c]); if (g > 1 && g < gap - 1) between.Add(g); }
            int steps = (int)(gap / 120);
            for (int q = 1; q <= steps; q++) between.Add(gap * q / (steps + 1));
            foreach (double g in between.Distinct().OrderBy(g => g))
            {
                var (x, y) = PointAt((t0 + g) % around);
                rim.Add(new Vector3f((float)Math.Round(x), (float)Math.Round(y), (float)(p0.Z + (p1.Z - p0.Z) * g / gap)));
            }
        }
        (double X, double Y) PointAt(double t)
        {
            for (int k = 0; k < n; k++)
                if (t <= at[k + 1] || k == n - 1)
                {
                    double u = (t - at[k]) / (at[k + 1] - at[k]);
                    return (outline[k].X + (outline[(k + 1) % n].X - outline[k].X) * u, outline[k].Y + (outline[(k + 1) % n].Y - outline[k].Y) * u);
                }
            return outline[0];
        }
        // Points that ended up on top of each other once rounded.
        var clean = new List<Vector3f>();
        foreach (var p in rim)
            if (clean.Count == 0 || Math.Abs(clean[^1].X - p.X) + Math.Abs(clean[^1].Y - p.Y) >= 2) clean.Add(p);
        if (clean.Count > 2 && Math.Abs(clean[0].X - clean[^1].X) + Math.Abs(clean[0].Y - clean[^1].Y) < 2) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 4) return null;
        double ox = Math.Round(outline.Average(p => p.X)), oy = Math.Round(outline.Average(p => p.Y)), oz = Math.Round(clean.Average(v => v.Z));
        Vector3f Rel(Vector3f v) => new((float)(v.X - ox), (float)(v.Y - oy), (float)(v.Z - oz));
        var rel = clean.Select(Rel).ToArray();
        return new LevelCut
        {
            Name = def.Name, Level = def.Level, Rim = rel,
            Faces = [.. faces.Select(f => f with { Corners = [.. f.Corners.Select(Rel)] })],
            Radius = rel.Max(v => Math.Sqrt(v.X * (double)v.X + v.Y * (double)v.Y)),
        };
    }

    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>The part of a face inside a convex outline (counter-clockwise), corner by corner with height and paint
    /// carried along each edge it crosses; in the face's own order.</summary>
    internal static List<(double X, double Y, double Z, double R, double G, double B)> Clip(SourceFace f, (double X, double Y)[] outline)
    {
        var poly = new List<(double X, double Y, double Z, double R, double G, double B)>(
            f.Corners.Select((v, k) => ((double)v.X, (double)v.Y, (double)v.Z, (double)f.Near[k].R, (double)f.Near[k].G, (double)f.Near[k].B)));
        for (int e = 0; e < outline.Length && poly.Count > 0; e++)
        {
            var a = outline[e]; var b = outline[(e + 1) % outline.Length];
            double Side((double X, double Y, double Z, double R, double G, double B) p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var next = new List<(double X, double Y, double Z, double R, double G, double B)>();
            for (int k = 0; k < poly.Count; k++)
            {
                var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                double sp = Side(p), sq = Side(q);
                if (sp >= 0) next.Add(p);
                if (sp >= 0 != sq >= 0)
                {
                    double t = sp / (sp - sq);
                    next.Add((p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t, p.Z + (q.Z - p.Z) * t, p.R + (q.R - p.R) * t, p.G + (q.G - p.G) * t, p.B + (q.B - p.B) * t));
                }
            }
            poly = next;
        }
        // Corners closer together than a unit become one.
        var clean = new List<(double X, double Y, double Z, double R, double G, double B)>();
        foreach (var p in poly)
            if (clean.Count == 0 || Math.Abs(clean[^1].X - p.X) + Math.Abs(clean[^1].Y - p.Y) + Math.Abs(clean[^1].Z - p.Z) >= 1) clean.Add(p);
        if (clean.Count > 1 && Math.Abs(clean[0].X - clean[^1].X) + Math.Abs(clean[0].Y - clean[^1].Y) + Math.Abs(clean[0].Z - clean[^1].Z) < 1) clean.RemoveAt(clean.Count - 1);
        return clean;
    }

    /// <summary>Triangles filling the ground between an outer edge (counter-clockwise) and an edge inside it
    /// (counter-clockwise too, the hole): corners as indices into the outer edge's points followed by the inner edge's.
    /// Null when the two cross or the space cannot be filled.</summary>
    internal static List<(int A, int B, int C)>? Sew(IReadOnlyList<(double X, double Y)> outer, IReadOnlyList<(double X, double Y)> inner)
    {
        int no = outer.Count, ni = inner.Count;
        var pts = outer.Concat(inner).ToList();
        // Every inner corner inside the outer edge, every outer corner outside the inner one, and no edges crossing.
        var o = outer.ToArray(); var h = inner.ToArray();
        if (h.Any(p => TerrainHull.Distance(o, p.X, p.Y) >= -0.5) || o.Any(p => TerrainHull.Distance(h, p.X, p.Y) <= 0.5)) return null;
        static bool Crosses((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
        {
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return (d1 > 0 && d2 < 0 || d1 < 0 && d2 > 0) && (d3 > 0 && d4 < 0 || d3 < 0 && d4 > 0);
        }
        // A bridge from the inner edge's rightmost corner to the nearest outer corner it can see.
        int m = 0;
        for (int k = 1; k < ni; k++) if (h[k].X > h[m].X || h[k].X == h[m].X && h[k].Y > h[m].Y) m = k;
        int bridge = -1;
        foreach (int k in Enumerable.Range(0, no).OrderBy(k => (o[k].X - h[m].X) * (o[k].X - h[m].X) + (o[k].Y - h[m].Y) * (o[k].Y - h[m].Y)))
        {
            bool clear = true;
            for (int j = 0; j < no && clear; j++)
            {
                int j1 = (j + 1) % no;
                if (j == k || j1 == k) continue;
                if (Crosses(h[m], o[k], o[j], o[j1])) clear = false;
            }
            for (int j = 0; j < ni && clear; j++)
            {
                int j1 = (j + 1) % ni;
                if (j == m || j1 == m) continue;
                if (Crosses(h[m], o[k], h[j], h[j1])) clear = false;
            }
            if (clear) { bridge = k; break; }
        }
        if (bridge < 0) return null;
        // One polygon: round the outer edge to the bridge, round the inner edge the other way, back over the bridge.
        var poly = new List<int>();
        for (int k = 0; k <= bridge; k++) poly.Add(k);
        for (int k = 0; k <= ni; k++) poly.Add(no + (m - k + ni) % ni);
        for (int k = bridge; k < no; k++) poly.Add(k);
        var tris = new List<(int, int, int)>();
        bool Inside(int p, int a, int b, int c)
        {
            var q = pts[p];
            if (q == pts[a] || q == pts[b] || q == pts[c]) return false;
            return Cross(pts[a], pts[b], q) >= 0 && Cross(pts[b], pts[c], q) >= 0 && Cross(pts[c], pts[a], q) >= 0;
        }
        int guard = 0;
        while (poly.Count > 3 && guard++ < 100000)
        {
            bool cut = false;
            for (int k = 0; k < poly.Count && !cut; k++)
            {
                int a = poly[(k - 1 + poly.Count) % poly.Count], b = poly[k], c = poly[(k + 1) % poly.Count];
                if (Cross(pts[a], pts[b], pts[c]) <= 1e-6) continue;
                bool ear = true;
                foreach (int p in poly) if (p != a && p != b && p != c && Inside(p, a, b, c)) { ear = false; break; }
                if (!ear) continue;
                tris.Add((a, b, c)); poly.RemoveAt(k); cut = true;
            }
            if (cut) continue;
            // No ear: a corner on a straight line (or a doubled one) goes without a triangle.
            int flat = -1;
            for (int k = 0; k < poly.Count && flat < 0; k++)
            {
                int a = poly[(k - 1 + poly.Count) % poly.Count], b = poly[k], c = poly[(k + 1) % poly.Count];
                if (Math.Abs(Cross(pts[a], pts[b], pts[c])) <= 1e-6) flat = k;
            }
            if (flat < 0) return null;
            poly.RemoveAt(flat);
        }
        if (poly.Count == 3 && Cross(pts[poly[0]], pts[poly[1]], pts[poly[2]]) > 1e-6) tris.Add((poly[0], poly[1], poly[2]));
        return tris;
    }
}
