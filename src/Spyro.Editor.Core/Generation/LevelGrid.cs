using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>A solid block over one grid cell: from its bottom (null: down to the ground under the level) to its top, whose
/// four corners may differ (a ramp). Corners run (x0,y0), (x1,y0), (x1,y1), (x0,y1).</summary>
internal sealed class Slab
{
    public int[] Top = new int[4];
    public int[]? Bottom;
    public SurfaceRole TopRole = SurfaceRole.Ground, SideRole = SurfaceRole.Cliff, BottomRole = SurfaceRole.Underside;
    /// <summary>The band at the top of a tall side (rock with a grass fringe); null: the whole side is <see cref="SideRole"/>.</summary>
    public SurfaceRole? SideTopRole = SurfaceRole.CliffTop;
    public AuthoredSurfaceKind? Kind;
    public bool DrawTop = true, DrawBottom = true;
    /// <summary>Spyro must be able to reach this top from the start and get back (the level's own floors).</summary>
    public bool Required;
    public int Area = -1;

    public Slab Clone() => (Slab)MemberwiseClone() is var s ? Fix(s) : null!;
    private Slab Fix(Slab s) { s.Top = (int[])Top.Clone(); s.Bottom = (int[]?)Bottom?.Clone(); return s; }
    public bool Flat => Top[0] == Top[1] && Top[1] == Top[2] && Top[2] == Top[3];
    public int MinTop => Math.Min(Math.Min(Top[0], Top[1]), Math.Min(Top[2], Top[3]));
    public int MaxTop => Math.Max(Math.Max(Top[0], Top[1]), Math.Max(Top[2], Top[3]));
    public void SetTop(int z) { Top[0] = Top[1] = Top[2] = Top[3] = z; }
    public void SetBottom(int z) { Bottom = [z, z, z, z]; }
    /// <summary>The top's height at (u, v) inside the cell (0-1 each).</summary>
    public float TopAt(float u, float v) => (1 - u) * (1 - v) * Top[0] + u * (1 - v) * Top[1] + u * v * Top[2] + (1 - u) * v * Top[3];
    public float BottomAt(float u, float v) => Bottom is { } b ? (1 - u) * (1 - v) * b[0] + u * (1 - v) * b[1] + u * v * b[2] + (1 - u) * v * b[3] : float.NegativeInfinity;
}

/// <summary>The generated level as columns of slabs on a grid of <see cref="Cell"/>-unit cells, and the faces they make:
/// every top and bottom that is not covered, and every part of a side that faces open air.</summary>
internal sealed class LevelGrid
{
    public const int Cell = 120;
    /// <summary>A side taller than this is drawn in bands, each painted once (no stretched textures); the top band is the
    /// cliff-top texture.</summary>
    public const int TopBand = 112, Band = 168;
    public readonly int W, H, OriginX, OriginY;
    private readonly List<Slab>[,] _cells;

    /// <summary>Lab: the sides left out where two sloping surfaces cross (first few).</summary>
    public List<string> Unmatched { get; } = [];

    public LevelGrid(int w, int h, int originX, int originY)
    {
        W = w; H = h; OriginX = originX; OriginY = originY;
        _cells = new List<Slab>[w, h];
        for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) _cells[i, j] = [];
    }

    public bool Inside(int i, int j) => i >= 0 && j >= 0 && i < W && j < H;
    public List<Slab> At(int i, int j) => _cells[i, j];
    public IEnumerable<(int I, int J)> AllCells() { for (int j = 0; j < H; j++) for (int i = 0; i < W; i++) yield return (i, j); }
    /// <summary>The cell's ground (its lowest slab when that reaches down to the ground), or null.</summary>
    public Slab? Ground(int i, int j) => Inside(i, j) && _cells[i, j].Count > 0 && _cells[i, j][0].Bottom == null ? _cells[i, j][0] : null;
    /// <summary>The cell's walkable floor: the top of the highest slab that has open air above it, or null.</summary>
    public Slab? Floor(int i, int j) => Inside(i, j) && _cells[i, j].Count > 0 ? _cells[i, j][^1] : null;
    public void Clear(int i, int j) => _cells[i, j].Clear();
    public void Set(int i, int j, Slab s) { _cells[i, j].Clear(); _cells[i, j].Add(s); }
    public void Add(int i, int j, Slab s) { _cells[i, j].Add(s); _cells[i, j].Sort((a, b) => a.MinTop.CompareTo(b.MinTop)); }
    public float X(int i) => OriginX + i * Cell;
    public float Y(int j) => OriginY + j * Cell;

    /// <summary>Corner offsets of the four corners, in the slab corner order.</summary>
    private static readonly (int Di, int Dj)[] Corners = [(0, 0), (1, 0), (1, 1), (0, 1)];
    /// <summary>The four sides: neighbour offset, and the two corners of this cell on that side in the order a wall facing
    /// the neighbour walks with the neighbour on its right.</summary>
    private static readonly (int Di, int Dj, int A, int B)[] Sides = [(1, 0, 1, 2), (-1, 0, 3, 0), (0, 1, 2, 3), (0, -1, 0, 1)];
    /// <summary>For a side of this cell, the matching corners of the neighbour cell (same points).</summary>
    private static int Opposite(int dir, int corner) => (dir, corner) switch
    {
        (0, 1) => 0, (0, 2) => 3, (1, 3) => 2, (1, 0) => 1, (2, 2) => 1, (2, 3) => 0, (3, 0) => 3, (3, 1) => 2, _ => throw new ArgumentException()
    };

    /// <summary>One face before materials: corners, role, ground kind, whether Spyro collides with it, which way it faces
    /// (for light), and whether it lies under a roof (a cave).</summary>
    public sealed record Piece(Vector3f[] Corners, SurfaceRole Role, AuthoredSurfaceKind? Kind, bool Collides, bool Covered, int Area)
    {
        /// <summary>Corners at the bottom of a wall, darkened (where it meets the floor).</summary>
        public bool[]? Foot { get; init; }
    }

    public List<Piece> Faces(out int unpaired)
    {
        List<Piece> faces = [];
        unpaired = 0;
        var covered = new bool[W, H];
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) covered[i, j] = _cells[i, j].Count > 1;
        Vector3f P(int i, int j, int corner, float z) => new(X(i + Corners[corner].Di), Y(j + Corners[corner].Dj), z);

        // Tops, merged into 2x2 and 2x1 tiles where the cells are flat at one height with the same look.
        var done = new HashSet<(int, int, int)>();
        string? Key(int i, int j, int k)
        {
            if (!Inside(i, j) || k >= _cells[i, j].Count) return null;
            var s = _cells[i, j][k];
            if (!s.DrawTop || TopHidden(i, j, k) || done.Contains((i, j, k))) return null;
            return $"{s.TopRole}|{s.Kind}|{s.Area}|{k}|{covered[i, j]}";
        }
        // Whether cells (i, j) .. (i + wi - 1, j + wj - 1) form one smooth patch: every cell's corners lie on the bilinear
        // surface through the patch's four outer corners (flat cells at one height are the plain case).
        bool Patch(int i, int j, int wi, int wj, int k, out int[] outer)
        {
            outer = [_cells[i, j][k].Top[0], _cells[i + wi - 1, j][k].Top[1], _cells[i + wi - 1, j + wj - 1][k].Top[2], _cells[i, j + wj - 1][k].Top[3]];
            for (int a = 0; a < wi; a++)
                for (int b = 0; b < wj; b++)
                {
                    var s = _cells[i + a, j + b][k];
                    for (int c = 0; c < 4; c++)
                    {
                        float u = (a + Corners[c].Di) / (float)wi, v = (b + Corners[c].Dj) / (float)wj;
                        float z = (1 - u) * (1 - v) * outer[0] + u * (1 - v) * outer[1] + u * v * outer[2] + (1 - u) * v * outer[3];
                        if (Math.Abs(z - s.Top[c]) > 0.01f) return false;
                    }
                }
            return true;
        }
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                for (int k = 0; k < _cells[i, j].Count; k++)
                {
                    if (done.Contains((i, j, k))) continue;
                    var s = _cells[i, j][k];
                    if (!s.DrawTop || TopHidden(i, j, k)) continue;
                    string? key = Key(i, j, k);
                    int wi = 1, wj = 1;
                    int[] outer = s.Top;
                    if (key != null && i % 2 == 0 && j % 2 == 0 && Key(i + 1, j, k) == key && Key(i, j + 1, k) == key && Key(i + 1, j + 1, k) == key && Patch(i, j, 2, 2, k, out var o22)) { wi = 2; wj = 2; outer = o22; }
                    else if (key != null && i % 2 == 0 && Key(i + 1, j, k) == key && Patch(i, j, 2, 1, k, out var o21)) { wi = 2; outer = o21; }
                    else if (key != null && j % 2 == 0 && Key(i, j + 1, k) == key && Patch(i, j, 1, 2, k, out var o12)) { wj = 2; outer = o12; }
                    for (int a = 0; a < wi; a++) for (int b = 0; b < wj; b++) done.Add((i + a, j + b, k));
                    Vector3f[] c = [new(X(i), Y(j), outer[0]), new(X(i + wi), Y(j), outer[1]), new(X(i + wi), Y(j + wj), outer[2]), new(X(i), Y(j + wj), outer[3])];
                    faces.Add(new(c, s.TopRole, s.Kind, true, covered[i, j] && k < _cells[i, j].Count - 1, s.Area));
                }
        // Bottoms (undersides, cave ceilings, bridge decks seen from below): Spyro never touches them.
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                for (int k = 0; k < _cells[i, j].Count; k++)
                {
                    var s = _cells[i, j][k];
                    if (s.Bottom is not { } b || !s.DrawBottom || BottomHidden(i, j, k)) continue;
                    faces.Add(new([P(i, j, 0, b[0]), P(i, j, 3, b[3]), P(i, j, 2, b[2]), P(i, j, 1, b[1])], s.BottomRole, null, false, k > 0, s.Area));
                }
        // Sides: each slab's part not matched by the neighbour's slabs, facing the neighbour; merged along straight runs.
        List<(int Dir, int I, int J, float A0, float A1, float B0, float B1, SurfaceRole Role, SurfaceRole Lower, bool Foot, bool Covered, int Area, float F0, float F1)> walls = [];
        int unmatched = 0;
        // One side of slab s over the stretch f0..f1 of the cell's edge (0 = corner ca, 1 = corner cb): the parts the
        // neighbour leaves open at both ends, paired bottom up. Where two sloping surfaces cross, the ends disagree and
        // the stretch is halved (down to 15 units) until they agree.
        void Side(Slab s, List<Slab>? theirs, int dir, int i, int j, int ca, int cb, float f0, float f1, int depth)
        {
            var pa = Exposed(s, ca, cb, f0, theirs, Opposite(dir, ca), Opposite(dir, cb));
            var pb = Exposed(s, ca, cb, f1, theirs, Opposite(dir, ca), Opposite(dir, cb));
            if (pa.Count != pb.Count)
            {
                // Only zero-height pieces where surfaces touch, and nothing else: no wall here.
                if (pa.All(p => p.Hi - p.Lo < 0.01f) && pb.All(p => p.Hi - p.Lo < 0.01f)) return;
                if (depth < 3) { float m = (f0 + f1) / 2; Side(s, theirs, dir, i, j, ca, cb, f0, m, depth + 1); Side(s, theirs, dir, i, j, ca, cb, m, f1, depth + 1); }
                else
                {
                    unmatched++;
                    if (Unmatched.Count < 12)
                        Unmatched.Add($"cell {i},{j} dir {dir} f {f0:0.###}-{f1:0.###} mine top {string.Join('/', s.Top)} bottom {(s.Bottom == null ? "-" : string.Join('/', s.Bottom))} {s.TopRole}; " +
                            $"theirs {string.Join(" | ", (theirs ?? []).Select(t => $"top {string.Join('/', t.Top)} bottom {(t.Bottom == null ? "-" : string.Join('/', t.Bottom))} {t.TopRole}"))}; " +
                            $"a {string.Join(",", pa.Select(p => $"[{p.Lo},{p.Hi}]"))} b {string.Join(",", pb.Select(p => $"[{p.Lo},{p.Hi}]"))}");
                }
                return;
            }
            for (int n = 0; n < pa.Count; n++)
            {
                var (lo0, hi0) = pa[n]; var (lo1, hi1) = pb[n];
                if (hi0 - lo0 <= 0 && hi1 - lo1 <= 0) continue;
                bool foot = theirs != null && theirs.Any(t => Math.Abs(t.TopAt(0.5f, 0.5f) - (lo0 + lo1) / 2) < 1);
                var role = s.SideRole;
                // The top band of a tall side shows the slab's cliff top (its grass fringe), where the side reaches the slab's top.
                if (s.SideTopRole is { } topRole && Math.Abs(hi0 - Lerp(s.Top[ca], s.Top[cb], f0)) < 1 && Math.Abs(hi1 - Lerp(s.Top[ca], s.Top[cb], f1)) < 1) role = topRole;
                walls.Add((dir, i, j, lo0, lo1, hi0, hi1, role, s.SideRole, foot, covered[i, j], s.Area, f0, f1));
            }
        }
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                for (int dir = 0; dir < 4; dir++)
                {
                    var (di, dj, ca, cb) = Sides[dir];
                    int ni = i + di, nj = j + dj;
                    var mine = _cells[i, j];
                    if (mine.Count == 0) continue;
                    var theirs = Inside(ni, nj) ? _cells[ni, nj] : null;
                    // Nothing beyond the map's edge is ever seen from inside: no outer walls for ground there.
                    foreach (var s in mine)
                    {
                        if ((theirs == null || theirs.Count == 0) && s.Bottom == null) continue;
                        Side(s, theirs, dir, i, j, ca, cb, 0f, 1f, 0);
                    }
                }
        foreach (var w in walls.GroupBy(w => (w.Dir, Line: w.Dir < 2 ? w.I : w.J)))
        {
            // Along the run, join neighbours whose bottom and top are the same flat heights.
            var ordered = w.OrderBy(x => x.Dir < 2 ? x.J : x.I).ToList();
            for (int n = 0; n < ordered.Count; n++)
            {
                var a = ordered[n];
                int span = 1;
                if (n + 1 < ordered.Count)
                {
                    var b = ordered[n + 1];
                    bool next = (a.Dir < 2 ? b.J - a.J : b.I - a.I) == 1;
                    bool flat = a.A0 == a.A1 && a.B0 == a.B1 && b.A0 == b.A1 && b.B0 == b.B1 && a.A0 == b.A0 && a.B0 == b.B0 && a.F0 == 0 && a.F1 == 1 && b.F0 == 0 && b.F1 == 1;
                    bool start = (a.Dir < 2 ? a.J : a.I) % 2 == 0;
                    if (next && flat && start && a.Role == b.Role && a.Lower == b.Lower && a.Foot == b.Foot && a.Covered == b.Covered) { span = 2; n++; }
                }
                EmitWall(faces, a.Dir, a.I, a.J, span, a.A0, a.A1, a.B0, a.B1, a.Role, a.Lower, a.Foot, a.Covered, a.Area, a.F0, a.F1);
            }
        }
        unpaired = unmatched;
        return faces;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private bool TopHidden(int i, int j, int k)
    {
        var s = _cells[i, j][k];
        return _cells[i, j].Any(o => o != s && o.Bottom is { } b && b.SequenceEqual(s.Top));
    }
    private bool BottomHidden(int i, int j, int k)
    {
        var s = _cells[i, j][k];
        return _cells[i, j].Any(o => o != s && o.Top.SequenceEqual(s.Bottom!));
    }

    /// <summary>The parts of slab <paramref name="s"/> at the point <paramref name="f"/> of the way from its corner
    /// <paramref name="ca"/> to <paramref name="cb"/> that the neighbour's slabs (corners <paramref name="ta"/>,
    /// <paramref name="tb"/> at the same points) do not cover, bottom up. A slab reaching the ground beside open air is
    /// cut a little below its top.</summary>
    private static List<(float Lo, float Hi)> Exposed(Slab s, int ca, int cb, float f, List<Slab>? theirs, int ta, int tb)
    {
        float hi = Lerp(s.Top[ca], s.Top[cb], f), lo = s.Bottom is { } b ? Lerp(b[ca], b[cb], f) : float.NegativeInfinity;
        List<(float Lo, float Hi)> parts = [(lo, hi)];
        if (theirs != null)
            foreach (var t in theirs)
            {
                float tl = t.Bottom is { } tb2 ? Lerp(tb2[ta], tb2[tb], f) : float.NegativeInfinity, th = Lerp(t.Top[ta], t.Top[tb], f);
                List<(float, float)> next = [];
                foreach (var (a, c) in parts)
                {
                    if (th <= a || tl >= c) { next.Add((a, c)); continue; }
                    if (tl > a) next.Add((a, tl));
                    // Equal tops leave a zero-height piece: the point where a ramp's side wall starts.
                    if (th <= c) next.Add((th, c));
                }
                parts = next;
            }
        return [.. parts.Select(p => (float.IsNegativeInfinity(p.Lo) ? p.Hi - 96 : p.Lo, p.Hi)).Where(p => p.Item2 >= p.Item1 - 0.01f)];
    }

    private void EmitWall(List<Piece> faces, int dir, int i, int j, int span, float lo0, float lo1, float hi0, float hi1, SurfaceRole role, SurfaceRole lower, bool foot, bool covered, int area, float f0 = 0, float f1 = 1)
    {
        var (_, _, ca, cb) = Sides[dir];
        // The run covers cells (i, j) onwards along the side (span cells); the east and south sides walk up the run, the
        // west and north sides down it, so the wall's first corner is in the first or the last cell.
        int ax = dir < 2 ? 0 : 1, ay = dir < 2 ? 1 : 0, last = span - 1;
        bool up = dir is 0 or 3;
        (float X, float Y) End(int corner, int step) => (X(i + Corners[corner].Di + ax * step), Y(j + Corners[corner].Dj + ay * step));
        var a0 = End(ca, up ? 0 : last);
        var b0 = End(cb, up ? last : 0);
        // A part of the cell's side only (where two slopes cross): its ends f0 and f1 of the way from corner ca to cb.
        var a = (X: a0.X + (b0.X - a0.X) * f0, Y: a0.Y + (b0.Y - a0.Y) * f0);
        var b = (X: a0.X + (b0.X - a0.X) * f1, Y: a0.Y + (b0.Y - a0.Y) * f1);
        // Bands: the top band (cliff top), then bands of at most Band down to the bottom.
        float h0 = hi0 - lo0, h1 = hi1 - lo1, tall = Math.Max(h0, h1);
        List<float> cuts = [0];
        if (tall > TopBand + 24)
        {
            float rest = tall - TopBand;
            int n = (int)Math.Ceiling(rest / Band);
            for (int k = 1; k <= n; k++) cuts.Add(rest * k / n / tall);
            cuts[^1] = (tall - TopBand) / tall;
        }
        cuts.Add(1);
        cuts = [.. cuts.Distinct().Order()];
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            float c0 = cuts[k], c1 = cuts[k + 1];
            float za0 = MathF.Round(lo0 + h0 * c0), za1 = MathF.Round(lo0 + h0 * c1), zb0 = MathF.Round(lo1 + h1 * c0), zb1 = MathF.Round(lo1 + h1 * c1);
            bool top = k == cuts.Count - 2;
            var bandRole = top ? role : lower;
            // Corners 0-1 along the top, 2-3 along the bottom, so the texture stands upright (its bottom edge is corners 2-3);
            // anticlockwise seen from the neighbour's side.
            Vector3f ta = new(a.X, a.Y, za1), tb = new(b.X, b.Y, zb1), ba = new(a.X, a.Y, za0), bb = new(b.X, b.Y, zb0);
            Vector3f[] c;
            bool[] f;
            bool bottom = k == 0 && foot;
            if (za1 <= za0) { c = [tb, ba, bb]; f = [false, bottom, bottom]; }
            else if (zb1 <= zb0) { c = [tb, ta, ba]; f = [false, false, bottom]; }
            else { c = [tb, ta, ba, bb]; f = [false, false, bottom, bottom]; }
            if (c.Distinct().Count() < 3) continue;
            faces.Add(new(c, bandRole, null, true, covered, area) { Foot = f });
        }
    }
}
