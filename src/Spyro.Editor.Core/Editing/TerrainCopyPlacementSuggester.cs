using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class TerrainCopyPlacementSuggester
{
    public static Vector2f SuggestedBesideOffset(
        TerrainPolygon terrain,
        float defaultOffset = 64f,
        float extraPadding = 96f,
        float maxOffset = 768f)
    {
        return SuggestedBesideOffset(terrain.Points, defaultOffset, extraPadding, maxOffset);
    }

    public static Vector2f SuggestedBesideOffset(
        IReadOnlyList<Vector2f> points,
        float defaultOffset = 64f,
        float extraPadding = 96f,
        float maxOffset = 768f)
    {
        if (points.Count < 2)
            return new Vector2f(defaultOffset, 0);

        Vector2f center = Center(points);
        float minX = points[0].X;
        float minY = points[0].Y;
        float maxX = points[0].X;
        float maxY = points[0].Y;
        float bestLength = 0;
        float bestNormalX = 1;
        float bestNormalY = 0;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2f a = points[i];
            Vector2f b = points[(i + 1) % points.Count];
            minX = Math.Min(minX, a.X);
            minY = Math.Min(minY, a.Y);
            maxX = Math.Max(maxX, a.X);
            maxY = Math.Max(maxY, a.Y);

            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length <= bestLength || length <= 0.001f)
                continue;

            float normalX = -dy / length;
            float normalY = dx / length;
            float midpointX = (a.X + b.X) * 0.5f;
            float midpointY = (a.Y + b.Y) * 0.5f;
            float awayX = midpointX - center.X;
            float awayY = midpointY - center.Y;
            if (((normalX * awayX) + (normalY * awayY)) < 0)
            {
                normalX = -normalX;
                normalY = -normalY;
            }

            bestLength = length;
            bestNormalX = normalX;
            bestNormalY = normalY;
        }

        if (bestLength <= 0.001f)
            return new Vector2f(defaultOffset, 0);

        float width = maxX - minX;
        float height = maxY - minY;
        float size = Math.Max(Math.Max(width, height), defaultOffset);
        float distance = Math.Clamp(MathF.Round(size + extraPadding), defaultOffset, maxOffset);
        return new Vector2f(bestNormalX * distance, bestNormalY * distance);
    }

    private static Vector2f Center(IReadOnlyList<Vector2f> points)
    {
        double x = 0;
        double y = 0;
        foreach (Vector2f point in points)
        {
            x += point.X;
            y += point.Y;
        }

        return new Vector2f((float)(x / points.Count), (float)(y / points.Count));
    }
}
