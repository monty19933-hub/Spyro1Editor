using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class GeometryOverlayLoader
{
    public static GeometryCandidate LoadFirstCandidate(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("candidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Geometry overlay has no candidates.");

        JsonElement first = candidates.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("Geometry overlay has no candidates.");

        return LoadCandidate(first);
    }

    private static GeometryCandidate LoadCandidate(JsonElement candidate)
    {
        GeometryCandidate result = new()
        {
            Name = JsonValue.GetString(candidate, "runtimeAddress", "geometry"),
            Bounds = ReadBounds(candidate),
            MinZ = ReadHeightBound(candidate, "minZ", 0),
            MaxZ = ReadHeightBound(candidate, "maxZ", 1)
        };

        if (candidate.TryGetProperty("polygons", out JsonElement polygons) && polygons.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement polygon in polygons.EnumerateArray())
            {
                TerrainPolygon? parsed = ReadPolygon(polygon);
                if (parsed != null)
                    result.Polygons.Add(parsed);
            }
        }

        if (candidate.TryGetProperty("edges", out JsonElement edges) && edges.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement edge in edges.EnumerateArray())
            {
                result.Edges.Add(new TerrainEdge(
                    JsonValue.GetSingle(edge, "x1"),
                    JsonValue.GetSingle(edge, "y1"),
                    JsonValue.GetSingle(edge, "x2"),
                    JsonValue.GetSingle(edge, "y2")));
            }
        }

        if (result.Bounds.IsEmpty)
            result.Bounds = ComputeBounds(result);

        return result;
    }

    private static TerrainPolygon? ReadPolygon(JsonElement polygon)
    {
        if (!polygon.TryGetProperty("points", out JsonElement pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
            return null;

        List<Vector2f> points = new();
        List<float> zValues = new();
        foreach (JsonElement point in pointsElement.EnumerateArray())
        {
            points.Add(new Vector2f(JsonValue.GetSingle(point, "x"), JsonValue.GetSingle(point, "y")));
            zValues.Add(JsonValue.GetSingle(point, "z"));
        }

        if (points.Count < 3)
            return null;

        int sectorOffset = JsonValue.GetInt32(polygon, "sectorOffset", -1);
        int faceOffset = JsonValue.GetInt32(polygon, "sourceWadOffset", -1);
        if (faceOffset < 0)
            faceOffset = JsonValue.GetInt32(polygon, "faceOffset", -1);

        return new TerrainPolygon(
            points,
            zValues,
            JsonValue.GetInt32(polygon, "textureId", -1),
            JsonValue.GetInt32(polygon, "sectorIndex", -1),
            JsonValue.GetInt32(polygon, "faceIndex", -1),
            JsonValue.GetString(polygon, "detail"),
            ReadFaceColor(polygon),
            sectorOffset,
            faceOffset,
            ReadIntArray(polygon, "vertexIndexes"),
            JsonValue.GetString(polygon, "word3"),
            JsonValue.GetString(polygon, "word4"),
            JsonValue.GetBoolean(polygon, "flip"),
            JsonValue.GetInt32(polygon, "depth", -1),
            ReadIntArray(polygon, "colourIndexes"));
    }

    private static IReadOnlyList<int> ReadIntArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        List<int> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                result.Add(number);
            else if (int.TryParse(value.ToString(), out int parsed))
                result.Add(parsed);
        }

        return result;
    }

    private static ColorRgba ReadFaceColor(JsonElement polygon)
    {
        if (!polygon.TryGetProperty("faceColor", out JsonElement color) || color.ValueKind != JsonValueKind.Object)
            return ColorRgba.FromRgb(92, 135, 104);

        return ColorRgba.FromArgb(
            JsonValue.GetInt32(color, "a", 255),
            JsonValue.GetInt32(color, "r", 92),
            JsonValue.GetInt32(color, "g", 135),
            JsonValue.GetInt32(color, "b", 104));
    }

    private static Rect2f ReadBounds(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("projectedBounds", out JsonElement bounds) || bounds.ValueKind != JsonValueKind.Object)
            return Rect2f.Empty;

        return Rect2f.FromBounds(
            JsonValue.GetSingle(bounds, "minX"),
            JsonValue.GetSingle(bounds, "minY"),
            JsonValue.GetSingle(bounds, "maxX"),
            JsonValue.GetSingle(bounds, "maxY"));
    }

    private static float ReadHeightBound(JsonElement candidate, string name, float fallback)
    {
        if (!candidate.TryGetProperty("heightBounds", out JsonElement heightBounds) || heightBounds.ValueKind != JsonValueKind.Object)
            return fallback;

        return JsonValue.GetSingle(heightBounds, name, fallback);
    }

    private static Rect2f ComputeBounds(GeometryCandidate candidate)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (TerrainPolygon polygon in candidate.Polygons)
        {
            minX = Math.Min(minX, polygon.Bounds.Left);
            minY = Math.Min(minY, polygon.Bounds.Top);
            maxX = Math.Max(maxX, polygon.Bounds.Right);
            maxY = Math.Max(maxY, polygon.Bounds.Bottom);
        }

        foreach (TerrainEdge edge in candidate.Edges)
        {
            minX = Math.Min(minX, edge.Bounds.Left);
            minY = Math.Min(minY, edge.Bounds.Top);
            maxX = Math.Max(maxX, edge.Bounds.Right);
            maxY = Math.Max(maxY, edge.Bounds.Bottom);
        }

        return minX == float.MaxValue ? Rect2f.FromBounds(0, 0, 2400, 1600) : Rect2f.FromBounds(minX, minY, maxX, maxY);
    }
}
