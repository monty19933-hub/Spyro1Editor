using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

/// <summary>Where an egg thief can run, as the game moves him (USA, decoded from the class-0x21 code and the routines
/// it calls).
/// <para>He steers by x and y alone: the path follower (0x80039E94) takes the heading to the next point with atan2 of
/// the x and y differences (0x80016AB4) and counts a point reached by a flat distance (0x80017990, x and y only); a
/// point's height is compared only when the caller asks (flag 0x100), which no thief does. His height comes from the
/// floor: a move probes from 1,500 game units (93.75 here) above him down 0x1000 (0x80038340, 0x8004D5EC). A point's
/// fourth word is his speed from there (0 = the running speed); the editor never changes it.</para>
/// <para>The game's own runs also take ledges the probe alone does not explain (his jump states, 40-44, not decoded):
/// drops of 127, 128 and 144 (High Caves, Alpine Ridge, Town Square), a step up of 125 (Magic Crafters), and legs a
/// straight line does not follow (Town Square's last, Magic Crafters' gap). So a run is good when, walking its straight
/// legs, he always finds floor no more than <see cref="Ledge"/> (150, a little over the largest of the game's own)
/// above or below where he is (Spyro can turn him round, so a drop counts as a climb the other way); and a changed leg
/// may keep a step the game's own leg already has (<see cref="Judge"/>). The game's own points lie anywhere from 195
/// below to 64 above the floor (the height is not used); points the editor moves are put 32 above the floor, as most of
/// the game's are.</para></summary>
public static class EggThiefRun
{
    public const float Climb = 1500 / 16f, Drop = (0x1000 - 1500) / 16f, Ledge = 150f, PointHeight = 32f, Sample = 16f;
    /// <summary>Faces steeper than this are walls, not floor.</summary>
    private const double WallSlope = 0.35;

    /// <summary>A place the thief can't run: on the leg from point <see cref="From"/> to point <see cref="To"/> (indexes;
    /// From -1 = from where he stands). <see cref="NoGround"/>: no floor at all there.</summary>
    public sealed record Problem(int From, int To, float X, float Y, string Text, bool NoGround = false)
    {
        public bool Touches(int point) => From == point || To == point;
    }

    /// <summary>The floor under each point as the thief reaches it (NaN for points not walked), and every problem.</summary>
    public sealed record Result(IReadOnlyList<float> Floors, IReadOnlyList<Problem> Problems)
    {
        public bool Ok => Problems.Count == 0;
    }

    private sealed class Faces
    {
        private readonly List<(TerrainPolygon P, float X0, float Y0, float X1, float Y1)> _all = [];
        public Faces(IReadOnlyList<TerrainPolygon> polygons, Func<TerrainPolygon, bool>? include)
        {
            foreach (var p in polygons)
            {
                if (p.IsTerrainRemoved || include != null && !include(p) || IsWall(p) || p.Points.Count < 3) continue;
                float x0 = p.Points.Min(q => q.X), x1 = p.Points.Max(q => q.X), y0 = p.Points.Min(q => q.Y), y1 = p.Points.Max(q => q.Y);
                _all.Add((p, x0, y0, x1, y1));
            }
        }
        public List<TerrainPolygon> Near(float x0, float y0, float x1, float y1)
        {
            float ax = MathF.Min(x0, x1) - 1, bx = MathF.Max(x0, x1) + 1, ay = MathF.Min(y0, y1) - 1, by = MathF.Max(y0, y1) + 1;
            return [.. _all.Where(f => f.X1 >= ax && f.X0 <= bx && f.Y1 >= ay && f.Y0 <= by).Select(f => f.P)];
        }
        /// <summary>Heights of the floor faces at (x, y).</summary>
        public static IEnumerable<float> At(List<TerrainPolygon> near, float x, float y)
        {
            foreach (var p in near)
                if (p.TryGetZ(x, y, out float z) && float.IsFinite(z)) yield return z;
        }
    }

    /// <summary>The floor the thief's probe finds at (x, y) coming from height <paramref name="from"/>: the highest floor
    /// face at or below from + <see cref="Climb"/>, if it is not more than <see cref="Drop"/> below.</summary>
    public static bool TryFloor(IReadOnlyList<TerrainPolygon> polygons, float x, float y, float from, out float floor, Func<TerrainPolygon, bool>? include = null) =>
        Probe(new Faces(polygons, include).Near(x, y, x, y), x, y, from, out floor);

    private static bool Probe(List<TerrainPolygon> near, float x, float y, float from, out float floor)
    {
        float best = float.NegativeInfinity;
        foreach (float z in Faces.At(near, x, y))
            if (z <= from + Climb + 0.01f && z >= from - Drop - 0.01f && z > best) best = z;
        floor = best;
        return float.IsFinite(best);
    }

    /// <summary>The floor at (x, y) nearest <paramref name="z"/>, or NaN.</summary>
    private static float Nearest(List<TerrainPolygon> near, float x, float y, float z)
    {
        float best = float.NaN;
        foreach (float f in Faces.At(near, x, y))
            if (float.IsNaN(best) || MathF.Abs(f - z) < MathF.Abs(best - z)) best = f;
        return best;
    }

    /// <summary>Walks the run as the thief does: from where he stands to point <paramref name="current"/>, then on round
    /// every point and back (the run is a loop: the last point leads to the first). <paramref name="legs"/>: only these
    /// legs (by their first point; -1 = from where he stands), each from the floor at its start; null = all, in order.
    /// After a problem the walk goes on from the floor at the next point, so every leg is looked at.</summary>
    public static Result Walk(IReadOnlyList<TerrainPolygon> polygons, Vector3f owner, IReadOnlyList<Vector3f> points, int current,
        Func<TerrainPolygon, bool>? include = null, IReadOnlySet<int>? legs = null)
    {
        int n = points.Count;
        float[] floors = Enumerable.Repeat(float.NaN, n).ToArray();
        List<Problem> problems = [];
        if (n == 0) return new(floors, [new(-1, -1, owner.X, owner.Y, "the run has no points", true)]);
        current = Math.Clamp(current, 0, n - 1);
        var faces = new Faces(polygons, include);
        float StartFloor(Vector3f p)
        {
            var near = faces.Near(p.X, p.Y, p.X, p.Y);
            if (Probe(near, p.X, p.Y, p.Z, out float f)) return f;
            float g = Nearest(near, p.X, p.Y, p.Z);
            return float.IsFinite(g) ? g : p.Z;
        }
        float h;
        if (legs == null || legs.Contains(-1))
        {
            var near = faces.Near(owner.X, owner.Y, owner.X, owner.Y);
            if (!Probe(near, owner.X, owner.Y, owner.Z, out h) && !Probe(near, owner.X, owner.Y, owner.Z + 64, out h))
            {
                problems.Add(new(-1, current, owner.X, owner.Y, "the egg thief himself is not standing on ground", true));
                h = StartFloor(points[current]);
            }
            else Leg(owner.X, owner.Y, points[current], ref h, -1, current);
            floors[current] = h;
        }
        else h = StartFloor(points[current]);
        for (int k = 0; k < n && n > 1; k++)
        {
            int a = (current + k) % n, b = (a + 1) % n;
            if (legs != null)
            {
                if (!legs.Contains(a)) continue;
                h = StartFloor(points[a]);
                if (float.IsNaN(floors[a])) floors[a] = h;
            }
            Leg(points[a].X, points[a].Y, points[b], ref h, a, b);
            if (legs != null || k < n - 1) floors[b] = h;
            // The whole loop walked: it must close on the floor it started from.
            else if (MathF.Abs(h - floors[current]) > 1f && !problems.Any(p => p.Touches(b)))
                problems.Add(new(a, b, points[b].X, points[b].Y,
                    $"the run does not come back to the same ground at point {b + 1} (it ends {MathF.Abs(h - floors[current]):0} units {(h > floors[current] ? "above" : "below")} it)"));
        }
        return new(floors, problems);

        void Leg(float x0, float y0, Vector3f to, ref float height, int from, int toIndex)
        {
            float x1 = to.X, y1 = to.Y;
            var near = faces.Near(x0, y0, x1, y1);
            float length = MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(1, (int)MathF.Ceiling(length / Sample));
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps, x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                // The floor his probe finds (no further down than a ledge he could get back up), or a ledge up he can take.
                if (Probe(near, x, y, height, out float floor) && height - floor <= Ledge + 0.01f) { height = floor; continue; }
                float above = float.PositiveInfinity, below = float.NegativeInfinity;
                foreach (float z in Faces.At(near, x, y)) { if (z > height && z < above) above = z; if (z <= height && z > below) below = z; }
                if (float.IsFinite(above) && above - height <= Ledge + 0.01f) { height = above; continue; }
                string leg = from < 0 ? $"from where the egg thief stands to point {toIndex + 1}" : $"from point {from + 1} to point {toIndex + 1}";
                bool none = !float.IsFinite(above) && !float.IsFinite(below);
                string why = none ? "there is no ground there"
                    : float.IsFinite(above) && (!float.IsFinite(below) || above - height < height - below)
                        ? $"there is a step up of {above - height:0} units (a thief gets up {Ledge:0} at most)"
                        : $"the ground drops {height - below:0} units (a thief gets down, and back up, {Ledge:0} at most)";
                problems.Add(new(from, toIndex, x, y, $"on the way {leg}, at {x:0}, {y:0}, {why}", none));
                height = StartFloor(to);
                return;
            }
        }
    }

    /// <summary>What stops an edited run: the problems on the legs to and from the moved points (and from where the
    /// thief stands, if he was moved) that the game's own run (<paramref name="original"/>, null for a thief added to the
    /// level) does not have on the same leg. A leg the game's own thief already jumps or takes round a corner may keep
    /// its step; ground that is missing is allowed only where his own leg is missing ground too (a gap he jumps).</summary>
    public static IReadOnlyList<Problem> Judge(IReadOnlyList<TerrainPolygon> polygons, Vector3f owner, IReadOnlyList<Vector3f> points, int current,
        IReadOnlyList<Vector3f>? original, Vector3f? originalOwner = null, Func<TerrainPolygon, bool>? include = null)
    {
        int n = points.Count;
        if (n == 0) return [new(-1, -1, owner.X, owner.Y, "the run has no points", true)];
        bool fresh = original == null || original.Count != n;
        var edited = fresh ? Enumerable.Range(0, n).ToHashSet() : Enumerable.Range(0, n).Where(i => Moved(points[i], original![i])).ToHashSet();
        bool ownerMoved = originalOwner == null || Moved(owner, originalOwner.Value);
        if (edited.Count == 0 && !ownerMoved) return [];
        HashSet<int> legs = [];
        foreach (int i in edited) { legs.Add(i); legs.Add((i - 1 + n) % n); }
        if (ownerMoved || edited.Contains(Math.Clamp(current, 0, n - 1))) legs.Add(-1);
        var now = Walk(polygons, owner, points, current, include, legs).Problems;
        if (now.Count == 0 || fresh) return now;
        var before = Walk(polygons, originalOwner ?? owner, original!, current, include, legs).Problems;
        return [.. now.Where(p => !before.Any(b => b.From == p.From && b.To == p.To && (b.NoGround || !p.NoGround)))];
    }

    private static bool Moved(Vector3f a, Vector3f b) => MathF.Abs(a.X - b.X) > 0.01f || MathF.Abs(a.Y - b.Y) > 0.01f;

    /// <summary>A point moved to (x, y): 32 above the floor there nearest <paramref name="near"/> (the floor the thief
    /// was on at that point); false when there is no floor at all.</summary>
    public static bool TryPlace(IReadOnlyList<TerrainPolygon> polygons, float x, float y, float near, out Vector3f placed, Func<TerrainPolygon, bool>? include = null)
    {
        placed = new Vector3f(x, y, near);
        var faces = new Faces(polygons, include).Near(x, y, x, y);
        float floor = Probe(faces, x, y, near, out float probed) ? probed : Nearest(faces, x, y, near);
        if (!float.IsFinite(floor)) return false;
        placed = new Vector3f(x, y, floor + PointHeight);
        return true;
    }

    /// <summary>The floor under a point of the run (from the point's own height: the probe, else the nearest floor), or NaN.</summary>
    public static float FloorAt(IReadOnlyList<TerrainPolygon> polygons, Vector3f point, Func<TerrainPolygon, bool>? include = null)
    {
        var faces = new Faces(polygons, include).Near(point.X, point.Y, point.X, point.Y);
        return Probe(faces, point.X, point.Y, point.Z, out float f) ? f : Nearest(faces, point.X, point.Y, point.Z);
    }

    private static bool IsWall(TerrainPolygon polygon)
    {
        var p = polygon.Points; var z = polygon.ZValues;
        if (p.Count < 3 || z.Length < 3) return false;
        double ax = p[1].X - p[0].X, ay = p[1].Y - p[0].Y, az = z[1] - z[0];
        double bx = p[2].X - p[0].X, by = p[2].Y - p[0].Y, bz = z[2] - z[0];
        double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
        double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return length > 1e-6 && Math.Abs(nz) / length < WallSlope;
    }
}
