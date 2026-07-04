using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

public static class TerrainBrushLocalSmoother
{
    private const double SelfWeightScale = 0.5;

    public static IReadOnlyList<TerrainBrushSmoothTarget> BuildLocalAverageTargets(
        IReadOnlyList<TerrainBrushVertexSample> vertices,
        Vector2f brushCenter,
        float brushRadius,
        float neighborhoodRadius)
    {
        float safeBrushRadius = Math.Max(1, brushRadius);
        float safeNeighborhoodRadius = Math.Max(1, neighborhoodRadius);
        TerrainBrushVertexSample[] targetVertices = vertices
            .Where(vertex => Distance(vertex.Point, brushCenter) <= safeBrushRadius)
            .ToArray();
        if (targetVertices.Length == 0)
            return Array.Empty<TerrainBrushSmoothTarget>();

        List<TerrainBrushSmoothTarget> targets = new(targetVertices.Length);
        foreach (TerrainBrushVertexSample vertex in targetVertices)
        {
            double weighted = 0;
            double totalWeight = 0;
            int neighbors = 0;
            foreach (TerrainBrushVertexSample neighbor in vertices)
            {
                float distance = Distance(vertex.Point, neighbor.Point);
                if (distance > safeNeighborhoodRadius)
                    continue;

                double t = Math.Clamp(1.0 - (distance / safeNeighborhoodRadius), 0.0, 1.0);
                double weight = t * t * (3.0 - (2.0 * t));
                if (weight <= 0.000001)
                    continue;

                if (neighbor.FaceIndex == vertex.FaceIndex && neighbor.VertexIndex == vertex.VertexIndex)
                    weight *= SelfWeightScale;

                weighted += neighbor.Z * weight;
                totalWeight += weight;
                neighbors++;
            }

            float targetZ = totalWeight <= 0.000001
                ? vertex.Z
                : (float)(weighted / totalWeight);
            targets.Add(new TerrainBrushSmoothTarget(vertex.FaceIndex, vertex.VertexIndex, targetZ, neighbors));
        }

        return targets;
    }

    private static float Distance(Vector2f a, Vector2f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct TerrainBrushVertexSample(int FaceIndex, int VertexIndex, Vector2f Point, float Z);

public readonly record struct TerrainBrushSmoothTarget(int FaceIndex, int VertexIndex, float TargetZ, int NeighborCount);
