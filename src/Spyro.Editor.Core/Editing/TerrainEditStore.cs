using System.Text.Json;
using System.Globalization;
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

            if (polygon.SurfaceBehaviorEdit is TerrainSurfaceBehaviorEdit surfaceBehavior)
            {
                edit["nativeSurfaceBehaviorEdit"] = true;
                edit["nativeSurfaceType"] = surfaceBehavior.SurfaceType;
                edit["nativeSurfaceParam1"] = surfaceBehavior.Param1;
                edit["nativeSurfaceParam2"] = surfaceBehavior.Param2;
                edit["nativeSurfaceSourceLevelKey"] = surfaceBehavior.SourceLevelKey;
                edit["nativeSurfaceSourceRuntimeKey"] = surfaceBehavior.SourceRuntimeKey;
                edit["nativeSurfaceLabel"] = surfaceBehavior.Label;
            }

            if (polygon.HasTextureEdit || polygon.HasTextureVisualEdit)
            {
                edit["textureEditMode"] = polygon.HasTextureVisualEdit
                    ? "native-resident-face-swap"
                    : "texture-id-preview";
                edit["textureIdOriginal"] = polygon.OriginalTextureId;
                edit["textureIdEdited"] = polygon.TextureId;
            }

            if (polygon.TextureVisualEdit is TerrainTextureVisualEdit textureVisual)
            {
                edit["nativeTextureVisualEdit"] = true;
                edit["nativeTextureVisualSourceLevelKey"] = textureVisual.SourceLevelKey;
                edit["nativeTextureVisualSourceTextureId"] = textureVisual.SourceTextureId;
                edit["nativeTextureVisualSourceRuntimeKey"] = textureVisual.SourceRuntimeKey;
                edit["nativeTextureVisualSourceSectorOffset"] = textureVisual.SourceSectorOffset >= 0
                    ? $"0x{textureVisual.SourceSectorOffset:X}"
                    : null;
                edit["nativeTextureVisualSourceFaceOffset"] = textureVisual.SourceFaceOffset >= 0
                    ? $"0x{textureVisual.SourceFaceOffset:X}"
                    : null;
                edit["nativeTextureVisualNearColors"] = textureVisual.Corners
                    .Select(corner => ToHex(corner.NearColor))
                    .ToArray();
                edit["nativeTextureVisualFarColors"] = textureVisual.Corners
                    .Select(corner => ToHex(corner.FarColor))
                    .ToArray();
                edit["nativeTextureVisualLabel"] = textureVisual.Label;

                if (textureVisual.UniqueCornerPairCount == 1)
                {
                    edit["nativeTextureVisualNearColor"] = ToHex(textureVisual.Corner0.NearColor);
                    edit["nativeTextureVisualFarColor"] = ToHex(textureVisual.Corner0.FarColor);
                }
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

    public static int Load(string path, IEnumerable<TerrainPolygon> polygons) =>
        LoadCore(path, polygons, requireExactBindings: false);

    public static int LoadStrict(string path, IEnumerable<TerrainPolygon> polygons) =>
        LoadCore(path, polygons, requireExactBindings: true);

    private static int LoadCore(
        string path,
        IEnumerable<TerrainPolygon> polygons,
        bool requireExactBindings)
    {
        TerrainPolygon[] polygonArray = polygons.ToArray();
        if (!File.Exists(path))
        {
            foreach (TerrainPolygon polygon in polygonArray)
                polygon.ResetTerrainEdit();
            return 0;
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("edits", out JsonElement editsElement) ||
            editsElement.ValueKind != JsonValueKind.Array)
        {
            if (requireExactBindings)
                throw new InvalidDataException("The terrain-edit file must contain one root object with an edits array.");
            foreach (TerrainPolygon polygon in polygonArray)
                polygon.ResetTerrainEdit();
            return 0;
        }

        Dictionary<string, TerrainPolygon> byKey = polygonArray
            .Where(polygon => !string.IsNullOrWhiteSpace(polygon.RuntimeKey))
            .ToDictionary(polygon => polygon.RuntimeKey, StringComparer.OrdinalIgnoreCase);
        JsonElement[] edits = editsElement.EnumerateArray().ToArray();
        StrictTerrainEditRow[] strictRows = requireExactBindings
            ? ValidateStrictRows(document.RootElement, edits, byKey)
            : Array.Empty<StrictTerrainEditRow>();

        foreach (TerrainPolygon polygon in polygonArray)
            polygon.ResetTerrainEdit();

        if (requireExactBindings)
        {
            foreach (StrictTerrainEditRow row in strictRows)
            {
                if (row.EditedZ != null)
                    row.Polygon.ApplyTerrainZValues(row.EditedZ);
                if (row.EditedTextureId.HasValue)
                    row.Polygon.ApplyTextureOverride(row.EditedTextureId.Value);
            }

            return strictRows.Length;
        }

        int applied = 0;
        foreach (JsonElement edit in edits)
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
            ApplyNativeTextureVisual(polygon, edit);
            ApplyMaterialMetadata(polygon, edit);
            ApplyNativeSurfaceBehavior(polygon, edit);
            ApplyStructureEdit(polygon, JsonValue.GetString(edit, "structureEditMode"));

            applied++;
        }

        return applied;
    }

    private static StrictTerrainEditRow[] ValidateStrictRows(
        JsonElement root,
        IReadOnlyList<JsonElement> edits,
        IReadOnlyDictionary<string, TerrainPolygon> byKey)
    {
        if (!root.TryGetProperty("editCount", out JsonElement editCountElement) ||
            editCountElement.ValueKind != JsonValueKind.Number ||
            !editCountElement.TryGetInt32(out int declaredEditCount) ||
            declaredEditCount < 0 ||
            declaredEditCount != edits.Count)
        {
            throw new InvalidDataException(
                "The terrain-edit file's editCount must exactly match its edits array.");
        }

        List<StrictTerrainEditRow> rows = new(edits.Count);
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement edit in edits)
        {
            if (edit.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Every terrain-edit row must be an object.");

            string runtimeKey = RequireStrictString(edit, "runtimeKey", "terrain-edit row");
            if (string.IsNullOrWhiteSpace(runtimeKey) || !seenKeys.Add(runtimeKey))
            {
                throw new InvalidDataException(
                    "Every terrain-edit row must have one unique, nonempty runtimeKey.");
            }
            if (!byKey.TryGetValue(runtimeKey, out TerrainPolygon? polygon))
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' does not bind to the locked source geometry.");
            }

            int sectorIndex = RequireStrictInt32(edit, "sectorIndex", runtimeKey);
            int faceIndex = RequireStrictInt32(edit, "faceIndex", runtimeKey);
            string detail = RequireStrictString(edit, "detail", runtimeKey);
            if (!string.Equals(polygon.Detail, "hp", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase) ||
                sectorIndex != polygon.SectorIndex ||
                faceIndex != polygon.FaceIndex ||
                !string.Equals(runtimeKey, polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    runtimeKey,
                    $"{sectorIndex}:{faceIndex}:{detail}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' does not exactly match one locked HP sector, face, and detail identity.");
            }

            int vertexCount = polygon.OriginalZValues.Length;
            if (vertexCount <= 0 ||
                polygon.ZValues.Length != vertexCount ||
                polygon.OriginalPoints.Count != vertexCount ||
                polygon.Points.Count != vertexCount)
            {
                throw new InvalidDataException(
                    $"Locked HP face '{runtimeKey}' does not have one exact point/Z value per vertex.");
            }

            RequireStrictOffset(edit, "sectorOffset", polygon.SectorOffset, runtimeKey);
            RequireStrictOffset(edit, "faceOffset", polygon.FaceOffset, runtimeKey);
            RequireStrictIntArray(
                edit,
                "vertexIndexes",
                NormalizeVertexIndexes(polygon.VertexIndexes, vertexCount),
                runtimeKey);
            RequireStrictNullableString(edit, "word3", polygon.Word3, runtimeKey);
            RequireStrictNullableString(edit, "word4", polygon.Word4, runtimeKey);
            RequireStrictNullableInt32(edit, "depth", polygon.FaceDepth, runtimeKey);
            RequireStrictBoolean(edit, "flip", polygon.FaceFlip, runtimeKey);
            RequireStrictIntArray(edit, "colourIndexes", polygon.ColourIndexes, runtimeKey);
            int originalTextureId = RequireStrictInt32(edit, "textureId", runtimeKey);
            if (originalTextureId != polygon.OriginalTextureId)
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' does not match the locked face's original texture ID.");
            }

            Vector2f[] originalPoints = RequireStrictVector2Array(
                edit,
                "originalPoints",
                vertexCount,
                runtimeKey);
            RequireExactPoints(originalPoints, polygon.OriginalPoints, "originalPoints", runtimeKey);
            float[] originalZ = RequireStrictFloatArray(
                edit,
                "originalZ",
                vertexCount,
                runtimeKey);
            RequireExactFloats(originalZ, polygon.OriginalZValues, "originalZ", runtimeKey);

            ValidateStrictMaterialMetadata(edit, polygon, runtimeKey);
            ValidateStrictUnsupportedEdits(edit, polygon, vertexCount, runtimeKey);

            float[]? editedZ = null;
            bool hasHeightEdit = false;
            if (edit.TryGetProperty("editedZ", out JsonElement editedZElement))
            {
                editedZ = ReadStrictFloatArray(
                    editedZElement,
                    "editedZ",
                    vertexCount,
                    runtimeKey);
                hasHeightEdit = editedZ
                    .Zip(polygon.OriginalZValues, (edited, original) => MathF.Abs(edited - original))
                    .Any(delta => delta > 0.001f);
            }

            ValidateStrictDerivedHeightPayload(edit, polygon, editedZ, vertexCount, runtimeKey);
            int? editedTextureId = ValidateStrictTexturePayload(edit, polygon, runtimeKey);
            if (!hasHeightEdit && !editedTextureId.HasValue)
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' has no real supported HP-Z or resident texture-ID edit.");
            }

            rows.Add(new StrictTerrainEditRow(polygon, editedZ, editedTextureId));
        }

        return rows.ToArray();
    }

    private static void ValidateStrictMaterialMetadata(
        JsonElement edit,
        TerrainPolygon polygon,
        string runtimeKey)
    {
        RequireOptionalExactString(edit, "surface", polygon.Surface, runtimeKey);
        RequireOptionalExactString(edit, "surfaceSource", polygon.SurfaceSource, runtimeKey);
        RequireOptionalExactString(edit, "surfaceColor", ToHex(polygon.SurfaceColor), runtimeKey, ignoreCase: true);
        RequireOptionalExactString(edit, "behavior", polygon.Behavior, runtimeKey);
        RequireOptionalExactString(edit, "behaviorSource", polygon.BehaviorSource, runtimeKey);
        RequireOptionalExactString(edit, "behaviorConfidence", polygon.BehaviorConfidence, runtimeKey);
        RequireOptionalExactString(edit, "behaviorNote", polygon.BehaviorNote, runtimeKey);
    }

    private static void ValidateStrictUnsupportedEdits(
        JsonElement edit,
        TerrainPolygon polygon,
        int vertexCount,
        string runtimeKey)
    {
        string structureEditMode = edit.TryGetProperty("structureEditMode", out _)
            ? RequireStrictString(edit, "structureEditMode", runtimeKey)
            : "none";
        if (!string.Equals(structureEditMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{runtimeKey}' contains an unsupported structural edit.");
        }

        if (edit.EnumerateObject().Any(property =>
                property.Name.StartsWith("nativeTextureVisual", StringComparison.Ordinal) ||
                property.Name.StartsWith("nativeSurface", StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{runtimeKey}' contains an unsupported native visual or surface-behavior edit.");
        }

        if (edit.TryGetProperty("editedPoints", out JsonElement editedPointsElement))
        {
            Vector2f[] editedPoints = ReadStrictVector2Array(
                editedPointsElement,
                "editedPoints",
                vertexCount,
                runtimeKey);
            RequireExactPoints(editedPoints, polygon.OriginalPoints, "editedPoints", runtimeKey);
        }

        if (edit.TryGetProperty("vertexDeltaXY", out JsonElement vertexDeltaElement))
        {
            Vector2f[] vertexDeltas = ReadStrictVector2Array(
                vertexDeltaElement,
                "vertexDeltaXY",
                vertexCount,
                runtimeKey);
            if (vertexDeltas.Any(delta => delta.X != 0 || delta.Y != 0))
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' contains an unsupported XY position edit.");
            }
        }
    }

    private static void ValidateStrictDerivedHeightPayload(
        JsonElement edit,
        TerrainPolygon polygon,
        IReadOnlyList<float>? editedZ,
        int vertexCount,
        string runtimeKey)
    {
        if (edit.TryGetProperty("vertexDeltaZ", out JsonElement vertexDeltaElement))
        {
            float[] vertexDeltas = ReadStrictFloatArray(
                vertexDeltaElement,
                "vertexDeltaZ",
                vertexCount,
                runtimeKey);
            for (int index = 0; index < vertexCount; index++)
            {
                float expected = editedZ == null
                    ? 0
                    : editedZ[index] - polygon.OriginalZValues[index];
                if (MathF.Abs(vertexDeltas[index] - expected) > 0.001f)
                {
                    throw new InvalidDataException(
                        $"Terrain-edit row '{runtimeKey}' has a vertexDeltaZ array inconsistent with editedZ.");
                }
            }
        }

        if (edit.TryGetProperty("deltaZ", out JsonElement deltaElement))
        {
            float deltaZ = ReadStrictFiniteSingle(deltaElement, "deltaZ", runtimeKey);
            float expected = editedZ == null
                ? 0
                : editedZ.Zip(polygon.OriginalZValues, (edited, original) => edited - original).Average();
            if (MathF.Abs(deltaZ - expected) > 0.001f)
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{runtimeKey}' has a deltaZ value inconsistent with editedZ.");
            }
        }
    }

    private static int? ValidateStrictTexturePayload(
        JsonElement edit,
        TerrainPolygon polygon,
        string runtimeKey)
    {
        bool hasMode = edit.TryGetProperty("textureEditMode", out _);
        bool hasOriginal = edit.TryGetProperty("textureIdOriginal", out _);
        bool hasEdited = edit.TryGetProperty("textureIdEdited", out _);
        if (!hasMode && !hasOriginal && !hasEdited)
            return null;
        if (!hasMode || !hasOriginal || !hasEdited)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{runtimeKey}' has an incomplete resident texture-ID edit.");
        }

        string mode = RequireStrictString(edit, "textureEditMode", runtimeKey);
        int original = RequireStrictInt32(edit, "textureIdOriginal", runtimeKey);
        int edited = RequireStrictInt32(edit, "textureIdEdited", runtimeKey);
        if (!string.Equals(mode, "texture-id-preview", StringComparison.Ordinal) ||
            original != polygon.OriginalTextureId ||
            edited < 0 ||
            edited == original)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{runtimeKey}' is not one real resident texture-ID override from the locked original texture.");
        }

        return edited;
    }

    private static int RequireStrictInt32(JsonElement element, string name, string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int result))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' must contain an exact integer {name}.");
        }

        return result;
    }

    private static string RequireStrictString(JsonElement element, string name, string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' must contain a string {name}.");
        }

        return value.GetString() ?? "";
    }

    private static void RequireStrictOffset(
        JsonElement element,
        string name,
        int expected,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' is missing locked preimage field {name}.");
        }

        if (expected < 0 && value.ValueKind == JsonValueKind.Null)
            return;

        int actual;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out actual))
        {
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            string text = (value.GetString() ?? "").Trim();
            bool parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out actual)
                : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out actual);
            if (!parsed)
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{context}' has a malformed locked {name}.");
            }
        }
        else
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has a malformed locked {name}.");
        }

        if (actual != expected)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireStrictNullableString(
        JsonElement element,
        string name,
        string expected,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' is missing locked preimage field {name}.");
        }

        string actual = value.ValueKind switch
        {
            JsonValueKind.Null when string.IsNullOrWhiteSpace(expected) => "",
            JsonValueKind.String => value.GetString() ?? "",
            _ => throw new InvalidDataException(
                $"Terrain-edit row '{context}' has a malformed locked {name}.")
        };
        if (!string.Equals(actual, expected ?? "", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireStrictNullableInt32(
        JsonElement element,
        string name,
        int expected,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' is missing locked preimage field {name}.");
        }

        if (expected < 0 && value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int actual) ||
            actual != expected)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireStrictBoolean(
        JsonElement element,
        string name,
        bool expected,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            value.GetBoolean() != expected)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireStrictIntArray(
        JsonElement element,
        string name,
        IReadOnlyList<int> expected,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' must contain a numeric {name} array.");
        }

        JsonElement[] items = values.EnumerateArray().ToArray();
        if (items.Length != expected.Count)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has the wrong {name} count.");
        }
        for (int index = 0; index < items.Length; index++)
        {
            if (items[index].ValueKind != JsonValueKind.Number ||
                !items[index].TryGetInt32(out int actual) ||
                actual != expected[index])
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{context}' does not match locked {name}.");
            }
        }
    }

    private static float[] RequireStrictFloatArray(
        JsonElement element,
        string name,
        int expectedCount,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement values))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' is missing locked preimage array {name}.");
        }

        return ReadStrictFloatArray(values, name, expectedCount, context);
    }

    private static float[] ReadStrictFloatArray(
        JsonElement values,
        string name,
        int expectedCount,
        string context)
    {
        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' must contain a numeric {name} array.");
        }

        JsonElement[] items = values.EnumerateArray().ToArray();
        if (items.Length != expectedCount)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has the wrong {name} count.");
        }

        float[] result = new float[items.Length];
        for (int index = 0; index < items.Length; index++)
            result[index] = ReadStrictFiniteSingle(items[index], $"{name}[{index}]", context);
        return result;
    }

    private static Vector2f[] RequireStrictVector2Array(
        JsonElement element,
        string name,
        int expectedCount,
        string context)
    {
        if (!element.TryGetProperty(name, out JsonElement values))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' is missing locked preimage array {name}.");
        }

        return ReadStrictVector2Array(values, name, expectedCount, context);
    }

    private static Vector2f[] ReadStrictVector2Array(
        JsonElement values,
        string name,
        int expectedCount,
        string context)
    {
        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' must contain a numeric {name} array.");
        }

        JsonElement[] items = values.EnumerateArray().ToArray();
        if (items.Length != expectedCount)
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has the wrong {name} count.");
        }

        Vector2f[] result = new Vector2f[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            JsonElement item = items[index];
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("x", out JsonElement xElement) ||
                !item.TryGetProperty("y", out JsonElement yElement))
            {
                throw new InvalidDataException(
                    $"Terrain-edit row '{context}' has a malformed {name}[{index}] point.");
            }

            result[index] = new Vector2f(
                ReadStrictFiniteSingle(xElement, $"{name}[{index}].x", context),
                ReadStrictFiniteSingle(yElement, $"{name}[{index}].y", context));
        }

        return result;
    }

    private static float ReadStrictFiniteSingle(JsonElement value, string name, string context)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetSingle(out float result) ||
            !float.IsFinite(result))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has a malformed or non-finite {name} value.");
        }

        return result;
    }

    private static void RequireExactFloats(
        IReadOnlyList<float> actual,
        IReadOnlyList<float> expected,
        string name,
        string context)
    {
        if (actual.Count != expected.Count ||
            actual.Where((value, index) => value != expected[index]).Any())
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireExactPoints(
        IReadOnlyList<Vector2f> actual,
        IReadOnlyList<Vector2f> expected,
        string name,
        string context)
    {
        if (actual.Count != expected.Count ||
            actual.Where((point, index) =>
                point.X != expected[index].X || point.Y != expected[index].Y).Any())
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' does not match locked {name}.");
        }
    }

    private static void RequireOptionalExactString(
        JsonElement element,
        string name,
        string expected,
        string context,
        bool ignoreCase = false)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return;
        if (value.ValueKind != JsonValueKind.String ||
            !string.Equals(
                value.GetString() ?? "",
                expected ?? "",
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Terrain-edit row '{context}' has {name} metadata that differs from the locked face.");
        }
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

    private static void ApplyNativeSurfaceBehavior(TerrainPolygon polygon, JsonElement edit)
    {
        if (!JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit"))
            return;

        int surfaceType = JsonValue.GetInt32(edit, "nativeSurfaceType", int.MinValue);
        if (surfaceType == int.MinValue)
            return;

        polygon.ApplySurfaceBehaviorEdit(new TerrainSurfaceBehaviorEdit(
            surfaceType,
            JsonValue.GetInt32(edit, "nativeSurfaceParam1"),
            JsonValue.GetInt32(edit, "nativeSurfaceParam2"),
            JsonValue.GetString(edit, "nativeSurfaceSourceLevelKey"),
            JsonValue.GetString(edit, "nativeSurfaceSourceRuntimeKey"),
            JsonValue.GetString(edit, "nativeSurfaceLabel", "native terrain behavior")));
    }

    private static void ApplyNativeTextureVisual(TerrainPolygon polygon, JsonElement edit)
    {
        if (!JsonValue.GetBoolean(edit, "nativeTextureVisualEdit"))
            return;

        IReadOnlyList<ColorRgba> nearColors = ReadColorArray(edit, "nativeTextureVisualNearColors");
        IReadOnlyList<ColorRgba> farColors = ReadColorArray(edit, "nativeTextureVisualFarColors");
        if (nearColors.Count != 4 || farColors.Count != 4)
        {
            if (!ColorRgba.TryParseHex(JsonValue.GetString(edit, "nativeTextureVisualNearColor"), out ColorRgba nearColor) ||
                !ColorRgba.TryParseHex(JsonValue.GetString(edit, "nativeTextureVisualFarColor"), out ColorRgba farColor))
            {
                return;
            }

            nearColors = [nearColor, nearColor, nearColor, nearColor];
            farColors = [farColor, farColor, farColor, farColor];
        }

        TerrainTextureVisualCorner[] corners = Enumerable.Range(0, 4)
            .Select(index => new TerrainTextureVisualCorner(nearColors[index], farColors[index]))
            .ToArray();
        polygon.ApplyTextureVisualEdit(new TerrainTextureVisualEdit(
            JsonValue.GetInt32(edit, "nativeTextureVisualSourceTextureId", polygon.TextureId),
            JsonValue.GetString(edit, "nativeTextureVisualSourceLevelKey"),
            JsonValue.GetString(edit, "nativeTextureVisualSourceRuntimeKey"),
            JsonValue.GetInt32(edit, "nativeTextureVisualSourceSectorOffset", -1),
            JsonValue.GetInt32(edit, "nativeTextureVisualSourceFaceOffset", -1),
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            JsonValue.GetString(edit, "nativeTextureVisualLabel", "native terrain texture visual")));
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

    private static IReadOnlyList<ColorRgba> ReadColorArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement colors) || colors.ValueKind != JsonValueKind.Array)
            return Array.Empty<ColorRgba>();

        List<ColorRgba> result = new();
        foreach (JsonElement value in colors.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                !ColorRgba.TryParseHex(value.GetString(), out ColorRgba color))
                return Array.Empty<ColorRgba>();
            result.Add(color);
        }

        return result;
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

    private sealed record StrictTerrainEditRow(
        TerrainPolygon Polygon,
        float[]? EditedZ,
        int? EditedTextureId);
}
