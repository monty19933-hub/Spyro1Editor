using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class TerrainEditStore
{
    public static async Task<int> SaveAsync(string path, IEnumerable<TerrainPolygon> polygons, string levelName, CancellationToken cancellationToken = default)
    {
        List<object> edits = new();
        foreach (TerrainPolygon polygon in polygons)
        {
            if (!polygon.IsTerrainEdited)
                continue;

            Dictionary<string, object?> edit = new()
            {
                ["runtimeKey"] = polygon.RuntimeKey,
                ["sectorIndex"] = polygon.SectorIndex,
                ["faceIndex"] = polygon.FaceIndex,
                ["detail"] = polygon.Detail,
                ["sectorOffset"] = polygon.SectorOffset >= 0 ? $"0x{polygon.SectorOffset:X}" : null,
                ["faceOffset"] = polygon.FaceOffset >= 0 ? $"0x{polygon.FaceOffset:X}" : null,
                ["vertexIndexes"] = NormalizeVertexIndexes(polygon.VertexIndexes, polygon.ZValues.Length),
                ["word3"] = string.IsNullOrWhiteSpace(polygon.Word3) ? null : polygon.Word3,
                ["word4"] = string.IsNullOrWhiteSpace(polygon.Word4) ? null : polygon.Word4,
                ["depth"] = polygon.FaceDepth >= 0 ? polygon.FaceDepth : null,
                ["flip"] = polygon.FaceFlip,
                ["colourIndexes"] = polygon.ColourIndexes,
                ["surface"] = polygon.Surface,
                ["surfaceSource"] = polygon.SurfaceSource,
                ["surfaceColor"] = ToHex(polygon.SurfaceColor),
                ["behavior"] = polygon.Behavior,
                ["behaviorSource"] = polygon.BehaviorSource,
                ["behaviorConfidence"] = polygon.BehaviorConfidence,
                ["behaviorNote"] = polygon.BehaviorNote,
                ["structureEditMode"] = StructureEditModeFor(polygon.StructureEdit),
                ["textureId"] = polygon.OriginalTextureId,
                ["deltaZ"] = polygon.TerrainEditDeltaZ,
                ["vertexDeltaZ"] = polygon.TerrainVertexDeltas(),
                ["vertexDeltaXY"] = JsonPoints(polygon.TerrainVertexXYDeltas()),
                ["originalPoints"] = JsonPoints(polygon.OriginalPoints),
                ["editedPoints"] = JsonPoints(polygon.Points),
                ["originalZ"] = polygon.OriginalZValues,
                ["editedZ"] = polygon.ZValues
            };

            if (polygon.HasTextureEdit)
            {
                edit["textureEditMode"] = "texture-id-preview";
                edit["textureIdOriginal"] = polygon.OriginalTextureId;
                edit["textureIdEdited"] = polygon.TextureId;
            }

            edits.Add(edit);
        }

        var root = new
        {
            generatedAt = DateTime.Now.ToString("s"),
            editor = "Spyro.Editor.Core",
            levelName = string.IsNullOrWhiteSpace(levelName) ? "Unknown" : levelName,
            note = "Cross-platform editor terrain edits keyed by runtime scene-sector face handles.",
            editCount = edits.Count,
            edits
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, cancellationToken: cancellationToken);
        return edits.Count;
    }

    public static int Load(string path, IEnumerable<TerrainPolygon> polygons)
    {
        TerrainPolygon[] polygonArray = polygons.ToArray();
        foreach (TerrainPolygon polygon in polygonArray)
            polygon.ResetTerrainEdit();

        if (!File.Exists(path))
            return 0;

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement editsElement) || editsElement.ValueKind != JsonValueKind.Array)
            return 0;

        Dictionary<string, TerrainPolygon> byKey = polygonArray
            .Where(polygon => !string.IsNullOrWhiteSpace(polygon.RuntimeKey))
            .ToDictionary(polygon => polygon.RuntimeKey, StringComparer.OrdinalIgnoreCase);

        int applied = 0;
        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            string key = JsonValue.GetString(edit, "runtimeKey");
            if (string.IsNullOrWhiteSpace(key))
                key = $"{JsonValue.GetInt32(edit, "sectorIndex", -1)}:{JsonValue.GetInt32(edit, "faceIndex", -1)}:{JsonValue.GetString(edit, "detail")}";

            if (!byKey.TryGetValue(key, out TerrainPolygon? polygon))
                continue;

            IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
            if (editedZ.Count > 0)
                polygon.ApplyTerrainZValues(editedZ);
            else
                polygon.ApplyTerrainDeltaZ(JsonValue.GetSingle(edit, "deltaZ"));
            IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
            if (editedPoints.Count > 0)
                polygon.ApplyTerrainPointPositions(editedPoints);
            int textureIdEdited = JsonValue.GetInt32(edit, "textureIdEdited", -1);
            if (textureIdEdited >= 0)
                polygon.ApplyTextureOverride(textureIdEdited);
            ApplyMaterialMetadata(polygon, edit);
            ApplyStructureEdit(polygon, JsonValue.GetString(edit, "structureEditMode"));

            applied++;
        }

        return applied;
    }

    private static void ApplyMaterialMetadata(TerrainPolygon polygon, JsonElement edit)
    {
        string surface = JsonValue.GetString(edit, "surface");
        string surfaceSource = JsonValue.GetString(edit, "surfaceSource", "saved terrain edit");
        if (!string.IsNullOrWhiteSpace(surface) &&
            (edit.TryGetProperty("surfaceColor", out _) || edit.TryGetProperty("surfaceSource", out _)))
        {
            ColorRgba color = ReadColor(edit, "surfaceColor", polygon.SurfaceColor);
            polygon.SetSurface(surface, color, surfaceSource);
        }

        string behavior = JsonValue.GetString(edit, "behavior");
        if (!string.IsNullOrWhiteSpace(behavior) &&
            (edit.TryGetProperty("behaviorSource", out _) ||
                edit.TryGetProperty("behaviorConfidence", out _) ||
                edit.TryGetProperty("behaviorNote", out _)))
        {
            polygon.SetBehavior(
                behavior,
                JsonValue.GetString(edit, "behaviorSource", "saved terrain edit"),
                JsonValue.GetString(edit, "behaviorConfidence", polygon.BehaviorConfidence),
                JsonValue.GetString(edit, "behaviorNote", polygon.BehaviorNote));
        }
    }

    private static string StructureEditModeFor(TerrainStructureEditKind kind)
    {
        return kind switch
        {
            TerrainStructureEditKind.RemoveFace => "remove-face",
            TerrainStructureEditKind.AddCloneFace => "add-clone-face",
            _ => "none"
        };
    }

    private static void ApplyStructureEdit(TerrainPolygon polygon, string mode)
    {
        if (string.Equals(mode, "remove-face", StringComparison.OrdinalIgnoreCase))
            polygon.StageTerrainRemoval();
        else if (string.Equals(mode, "add-clone-face", StringComparison.OrdinalIgnoreCase))
            polygon.StageTerrainAddClone();
    }

    private static IReadOnlyList<float> ReadFloatArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<float>();

        List<float> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number))
                result.Add(number);
            else if (float.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                result.Add(parsed);
        }

        return result;
    }

    private static IReadOnlyList<Vector2f> ReadVector2Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<Vector2f>();

        List<Vector2f> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                result.Add(new Vector2f(JsonValue.GetSingle(value, "x"), JsonValue.GetSingle(value, "y")));
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                float[] parts = value.EnumerateArray()
                    .Select(part => part.ValueKind == JsonValueKind.Number && part.TryGetSingle(out float number)
                        ? number
                        : float.TryParse(part.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                            ? parsed
                            : 0)
                    .Take(2)
                    .ToArray();
                if (parts.Length == 2)
                    result.Add(new Vector2f(parts[0], parts[1]));
            }
        }

        return result;
    }

    private static object[] JsonPoints(IReadOnlyList<Vector2f> points)
    {
        return points
            .Select(point => new { x = point.X, y = point.Y })
            .Cast<object>()
            .ToArray();
    }

    private static ColorRgba ReadColor(JsonElement element, string name, ColorRgba fallback)
    {
        return ColorRgba.TryParseHex(JsonValue.GetString(element, name), out ColorRgba color)
            ? color
            : fallback;
    }

    private static string ToHex(ColorRgba color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static IReadOnlyList<int> NormalizeVertexIndexes(IReadOnlyList<int> indexes, int zValueCount)
    {
        if (indexes.Count <= zValueCount)
            return indexes;

        List<int> result = new();
        HashSet<int> seen = new();
        foreach (int index in indexes)
        {
            if (!seen.Add(index))
                continue;

            result.Add(index);
            if (result.Count == zValueCount)
                break;
        }

        return result.Count == zValueCount ? result : indexes.Take(zValueCount).ToArray();
    }
}
