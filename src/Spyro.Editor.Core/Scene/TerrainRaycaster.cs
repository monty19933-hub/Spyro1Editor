using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class TerrainRaycaster
{
    private const float IntersectionEpsilon = 0.0001f;

    public static bool TryFindClosestHit(
        IReadOnlyList<TerrainPolygon> polygons,
        Vector3f origin,
        Vector3f direction,
        out int terrainIndex,
        out Vector3f hit,
        out float distance)
    {
        terrainIndex = -1;
        hit = default;
        distance = float.MaxValue;

        for (int i = 0; i < polygons.Count; i++)
        {
            if (!TryIntersect(polygons[i], origin, direction, out Vector3f candidate, out float candidateDistance) ||
                candidateDistance >= distance)
            {
                continue;
            }

            terrainIndex = i;
            hit = candidate;
            distance = candidateDistance;
        }

        return terrainIndex >= 0;
    }

    public static bool TryIntersect(
        TerrainPolygon polygon,
        Vector3f origin,
        Vector3f direction,
        out Vector3f hit,
        out float distance)
    {
        hit = default;
        distance = float.MaxValue;
        if (polygon.IsTerrainRemoved)
            return false;

        int count = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
        if (count < 3)
            return false;

        Vector3f a = PointAt(polygon, 0);
        bool found = false;
        for (int i = 1; i < count - 1; i++)
        {
            if (!TryIntersectTriangle(
                    a,
                    PointAt(polygon, i),
                    PointAt(polygon, i + 1),
                    origin,
                    direction,
                    out float candidateDistance) ||
                candidateDistance >= distance)
            {
                continue;
            }

            distance = candidateDistance;
            found = true;
        }

        if (!found)
            return false;

        hit = Add(origin, Scale(direction, distance));
        return true;
    }

    private static bool TryIntersectTriangle(
        Vector3f a,
        Vector3f b,
        Vector3f c,
        Vector3f origin,
        Vector3f direction,
        out float distance)
    {
        distance = 0;
        Vector3f edge1 = Subtract(b, a);
        Vector3f edge2 = Subtract(c, a);
        Vector3f p = Cross(direction, edge2);
        float determinant = Dot(edge1, p);
        if (Math.Abs(determinant) <= IntersectionEpsilon)
            return false;

        float inverse = 1f / determinant;
        Vector3f fromA = Subtract(origin, a);
        float u = inverse * Dot(fromA, p);
        if (u < -IntersectionEpsilon || u > 1f + IntersectionEpsilon)
            return false;

        Vector3f q = Cross(fromA, edge1);
        float v = inverse * Dot(direction, q);
        if (v < -IntersectionEpsilon || u + v > 1f + IntersectionEpsilon)
            return false;

        float candidateDistance = inverse * Dot(edge2, q);
        if (candidateDistance <= IntersectionEpsilon)
            return false;

        distance = candidateDistance;
        return true;
    }

    private static Vector3f PointAt(TerrainPolygon polygon, int index)
    {
        Vector2f point = polygon.Points[index];
        return new Vector3f(point.X, point.Y, polygon.ZValues[index]);
    }

    private static Vector3f Add(Vector3f a, Vector3f b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static Vector3f Subtract(Vector3f a, Vector3f b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static Vector3f Scale(Vector3f value, float scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale);

    private static float Dot(Vector3f a, Vector3f b) =>
        (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static Vector3f Cross(Vector3f a, Vector3f b) =>
        new(
            (a.Y * b.Z) - (a.Z * b.Y),
            (a.Z * b.X) - (a.X * b.Z),
            (a.X * b.Y) - (a.Y * b.X));
}
