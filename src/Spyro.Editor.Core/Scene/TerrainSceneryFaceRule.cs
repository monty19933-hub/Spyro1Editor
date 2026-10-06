namespace Spyro.Editor.Core.Scene;

/// <summary>
/// A visible terrain face is scenery when the game has no collision on it at all: none of its own triangles is
/// a collision triangle, and no differently cut collision triangle lies on its surface either. Trim strips on
/// doorways and arches are like this. Removing or copying such a face changes only what is drawn, so the
/// editor and Create BIN may do it without a collision owner. Anything with collision on it stays refused.
/// </summary>
public static class TerrainSceneryFaceRule
{
    /// <summary>Distance (world units) at which a collision triangle counts as lying on the face.</summary>
    public const double Tolerance = 0.75;

    // Interior sample points of each visible triangle (barycentric). They stay a quarter of the way in from
    // every edge, so neighbouring faces that only share an edge or a corner do not count.
    private static readonly (double A, double B, double C)[] Samples =
    [
        (1 / 3.0, 1 / 3.0, 1 / 3.0), (0.5, 0.25, 0.25), (0.25, 0.5, 0.25), (0.25, 0.25, 0.5)
    ];

    public static bool HasNoCollisionOnFace(
        IReadOnlyList<(double X, double Y, double Z)> face,
        IEnumerable<IReadOnlyList<SpyroCollisionPoint>> collisionTriangles)
    {
        if (face.Count < 3)
            return false;
        double minX = face.Min(p => p.X) - 1, maxX = face.Max(p => p.X) + 1;
        double minY = face.Min(p => p.Y) - 1, maxY = face.Max(p => p.Y) + 1;
        double minZ = face.Min(p => p.Z) - 1, maxZ = face.Max(p => p.Z) + 1;
        List<(double X, double Y, double Z)> samples = [];
        for (int i = 1; i + 1 < face.Count; i++)
        {
            var (a, b, c) = (face[0], face[i], face[i + 1]);
            foreach (var (u, v, w) in Samples)
                samples.Add((a.X * u + b.X * v + c.X * w, a.Y * u + b.Y * v + c.Y * w, a.Z * u + b.Z * v + c.Z * w));
        }
        foreach (IReadOnlyList<SpyroCollisionPoint> triangle in collisionTriangles)
        {
            if (triangle.Count != 3)
                continue;
            if (triangle.Max(p => p.X) < minX || triangle.Min(p => p.X) > maxX ||
                triangle.Max(p => p.Y) < minY || triangle.Min(p => p.Y) > maxY ||
                triangle.Max(p => p.Z) < minZ || triangle.Min(p => p.Z) > maxZ)
                continue;
            var p0 = (X: (double)triangle[0].X, Y: (double)triangle[0].Y, Z: (double)triangle[0].Z);
            var p1 = (X: (double)triangle[1].X, Y: (double)triangle[1].Y, Z: (double)triangle[1].Z);
            var p2 = (X: (double)triangle[2].X, Y: (double)triangle[2].Y, Z: (double)triangle[2].Z);
            foreach (var sample in samples)
                if (DistanceToTriangle(sample, p0, p1, p2) <= Tolerance)
                    return false;
        }
        return true;
    }

    /// <summary>Euclidean distance from a point to a (possibly degenerate) triangle.</summary>
    public static double DistanceToTriangle(
        (double X, double Y, double Z) p, (double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c)
    {
        // Ericson, Real-Time Collision Detection, closest point on triangle.
        var ab = Sub(b, a); var ac = Sub(c, a); var ap = Sub(p, a);
        double d1 = Dot(ab, ap), d2 = Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return Length(Sub(p, a));
        var bp = Sub(p, b);
        double d3 = Dot(ab, bp), d4 = Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return Length(bp);
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            double v = d1 - d3 == 0 ? 0 : d1 / (d1 - d3);
            return Length(Sub(p, Add(a, Scale(ab, v))));
        }
        var cp = Sub(p, c);
        double d5 = Dot(ab, cp), d6 = Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return Length(cp);
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            double w = d2 - d6 == 0 ? 0 : d2 / (d2 - d6);
            return Length(Sub(p, Add(a, Scale(ac, w))));
        }
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            double denominator = d4 - d3 + (d5 - d6);
            double w = denominator == 0 ? 0 : (d4 - d3) / denominator;
            return Length(Sub(p, Add(b, Scale(Sub(c, b), w))));
        }
        double sum = va + vb + vc;
        if (sum == 0)
            return Math.Min(Length(Sub(p, a)), Math.Min(Length(bp), Length(cp)));
        double vv = vb / sum, ww = vc / sum;
        return Length(Sub(p, Add(a, Add(Scale(ab, vv), Scale(ac, ww)))));
    }

    private static (double X, double Y, double Z) Sub((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double s) => (a.X * s, a.Y * s, a.Z * s);
    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static double Length((double X, double Y, double Z) a) => Math.Sqrt(Dot(a, a));
}
