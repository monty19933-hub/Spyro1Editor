using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Advisory placement check for moved native Mobys. Spyro painter-sorts world
/// geometry without a depth buffer, so a rotating object whose native render
/// envelope intersects a wall can be emitted normally and still disappear
/// behind that wall from some camera angles.
/// </summary>
public static class MobyTerrainClearanceSafety
{
    public const float WallNormalZMaximum = 0.35f;
    public const float MinimumWorseningToReport = 2f;

    public static MobySourceEditSafetyFinding? Inspect(
        GeometryCandidate? geometry,
        Vector3f originalPosition,
        Vector3f editedPosition,
        int nativeRenderRadius,
        string label)
    {
        if (geometry is not { Polygons.Count: > 0 } ||
            nativeRenderRadius is <= 0 or >= 0x80 ||
            !IsFinite(originalPosition) ||
            !IsFinite(editedPosition))
        {
            return null;
        }

        float radius = nativeRenderRadius;
        if (!TryFindNearestWallDistance(geometry, editedPosition, radius, out WallDistance editedWall) ||
            editedWall.Distance >= radius)
        {
            return null;
        }

        bool originalWallFound =
            TryFindNearestWallDistance(geometry, originalPosition, radius, out WallDistance originalWall);
        float originalDistance = originalWallFound ? originalWall.Distance : float.PositiveInfinity;
        if (originalDistance < radius &&
            originalDistance - editedWall.Distance <= MinimumWorseningToReport)
        {
            return null;
        }

        float overlap = radius - editedWall.Distance;
        string originalComparison = float.IsFinite(originalDistance)
            ? $"; native placement clearance was {originalDistance:0.###}"
            : "";
        return new MobySourceEditSafetyFinding(
            "moby-terrain-wall-clearance",
            MobyBuildSafetyStatus.Review,
            $"{label}'s edited position is {editedWall.Distance:0.###} units from terrain wall " +
            $"{editedWall.RuntimeKey}, inside its native render radius {radius:0.###} by " +
            $"{overlap:0.###} units{originalComparison}. The object may flicker or be overdrawn " +
            "from some camera angles; move it farther from the wall or review this placement intentionally.");
    }

    private static bool TryFindNearestWallDistance(
        GeometryCandidate geometry,
        Vector3f position,
        float searchRadius,
        out WallDistance nearest)
    {
        nearest = default;
        bool found = false;
        float bestDistanceSquared = float.PositiveInfinity;

        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (polygon.IsTerrainRemoved ||
                polygon.Points.Count < 3 ||
                polygon.ZValues.Length != polygon.Points.Count ||
                position.X < polygon.Bounds.Left - searchRadius ||
                position.X > polygon.Bounds.Right + searchRadius ||
                position.Y < polygon.Bounds.Top - searchRadius ||
                position.Y > polygon.Bounds.Bottom + searchRadius ||
                position.Z < polygon.MinZ - searchRadius ||
                position.Z > polygon.MaxZ + searchRadius)
            {
                continue;
            }

            Point3 a = ToPoint(polygon, 0);
            for (int index = 1; index < polygon.Points.Count - 1; index++)
            {
                Point3 b = ToPoint(polygon, index);
                Point3 c = ToPoint(polygon, index + 1);
                if (!IsWallLike(a, b, c))
                    continue;

                float distanceSquared = DistanceSquaredToTriangle(
                    new Point3(position.X, position.Y, position.Z),
                    a,
                    b,
                    c);
                if (!float.IsFinite(distanceSquared) || distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                nearest = new WallDistance(
                    MathF.Sqrt(MathF.Max(0, distanceSquared)),
                    polygon.RuntimeKey);
                found = true;
            }
        }

        return found;
    }

    private static Point3 ToPoint(TerrainPolygon polygon, int index) =>
        new(polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index]);

    private static bool IsWallLike(Point3 a, Point3 b, Point3 c)
    {
        Point3 ab = b - a;
        Point3 ac = c - a;
        Point3 normal = Cross(ab, ac);
        float magnitudeSquared = Dot(normal, normal);
        if (magnitudeSquared <= 0.000001f)
            return false;

        float normalZ = MathF.Abs(normal.Z) / MathF.Sqrt(magnitudeSquared);
        return normalZ <= WallNormalZMaximum;
    }

    // Closest-point region test from Real-Time Collision Detection. Returning
    // squared distance keeps the inner terrain scan allocation-free.
    private static float DistanceSquaredToTriangle(Point3 point, Point3 a, Point3 b, Point3 c)
    {
        Point3 ab = b - a;
        Point3 ac = c - a;
        Point3 ap = point - a;
        float d1 = Dot(ab, ap);
        float d2 = Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0)
            return Dot(ap, ap);

        Point3 bp = point - b;
        float d3 = Dot(ab, bp);
        float d4 = Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3)
            return Dot(bp, bp);

        float vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            float v = d1 / (d1 - d3);
            Point3 projection = a + (ab * v);
            Point3 delta = point - projection;
            return Dot(delta, delta);
        }

        Point3 cp = point - c;
        float d5 = Dot(ab, cp);
        float d6 = Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6)
            return Dot(cp, cp);

        float vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            float w = d2 / (d2 - d6);
            Point3 projection = a + (ac * w);
            Point3 delta = point - projection;
            return Dot(delta, delta);
        }

        float va = (d3 * d6) - (d5 * d4);
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            Point3 projection = b + ((c - b) * w);
            Point3 delta = point - projection;
            return Dot(delta, delta);
        }

        float denominator = 1f / (va + vb + vc);
        float faceV = vb * denominator;
        float faceW = vc * denominator;
        Point3 faceProjection = a + (ab * faceV) + (ac * faceW);
        Point3 faceDelta = point - faceProjection;
        return Dot(faceDelta, faceDelta);
    }

    private static Point3 Cross(Point3 a, Point3 b) =>
        new(
            (a.Y * b.Z) - (a.Z * b.Y),
            (a.Z * b.X) - (a.X * b.Z),
            (a.X * b.Y) - (a.Y * b.X));

    private static float Dot(Point3 a, Point3 b) =>
        (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static bool IsFinite(Vector3f value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private readonly record struct WallDistance(float Distance, string RuntimeKey);

    private readonly record struct Point3(float X, float Y, float Z)
    {
        public static Point3 operator +(Point3 left, Point3 right) =>
            new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Point3 operator -(Point3 left, Point3 right) =>
            new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Point3 operator *(Point3 value, float scale) =>
            new(value.X * scale, value.Y * scale, value.Z * scale);
    }
}
