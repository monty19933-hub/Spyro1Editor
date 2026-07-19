using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Rendering;

namespace Spyro.Editor.Core.Scene;

public sealed record GeometryCacheHealthIssue(
    string LevelKey,
    string OverlayPath,
    string Severity,
    string Message,
    bool BlocksLoading);

public static class GeometryCacheHealth
{
    public static GeometryCacheHealthIssue? InspectOverlay(string levelKey, string overlayPath)
    {
        if (!File.Exists(overlayPath))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(overlayPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (IsKnownBadGnastysLootOverlay(levelKey, document.RootElement))
            {
                return new GeometryCacheHealthIssue(
                    levelKey,
                    overlayPath,
                    "bad-capture",
                    "Gnasty's Loot terrain cache matches the Gnasty's World homeworld capture. The editor is hiding this terrain until a true Gnasty's Loot RAM capture or source-derived overlay is available.",
                    BlocksLoading: true);
            }

            if (IsStaleSourceOverlay(document.RootElement))
            {
                return new GeometryCacheHealthIssue(
                    levelKey,
                    overlayPath,
                    "stale-source-color-layout",
                    "This BIN/CUE terrain cache is missing the current source-scene HP raw-word/material/color, topology, or static far-transition contract. The editor will rebuild it from the selected Spyro disc before displaying the level.",
                    BlocksLoading: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new GeometryCacheHealthIssue(
                levelKey,
                overlayPath,
                "invalid-cache",
                $"This terrain cache could not be validated and will not be loaded: {ex.Message}",
                BlocksLoading: true);
        }

        return null;
    }

    private static bool IsStaleSourceOverlay(JsonElement root)
    {
        if (!SourceSceneOverlayContract.HasExplicitSourceProvenance(root))
            return false;

        if (JsonValue.GetInt32(root, "sourceOverlayFormatVersion", 0) < SourceSceneOverlayContract.CurrentFormatVersion ||
            !string.Equals(
                JsonValue.GetString(root, "hpColorLayout"),
                SourceSceneOverlayContract.HpColorLayout,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "hpCornerPayload"),
                SourceSceneOverlayContract.HpCornerPayload,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "hpFaceMaterialPayload"),
                SourceSceneOverlayContract.HpFaceMaterialPayload,
                StringComparison.Ordinal) ||
            !string.Equals(
                JsonValue.GetString(root, "hpCoordinatePayload"),
                SourceSceneOverlayContract.HpCoordinatePayload,
                StringComparison.Ordinal) ||
            !string.Equals(
                JsonValue.GetString(root, "lpColorLayout"),
                SourceSceneOverlayContract.LpColorLayout,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "lpFacePayload"),
                SourceSceneOverlayContract.LpFacePayload,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "terrainLodPreview"),
                SourceSceneOverlayContract.TerrainLodPreview,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "sceneChainPolicy"),
                SourceSceneOverlayContract.SceneChainPolicy,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                JsonValue.GetString(root, "terrainOcclusionContract"),
                SourceSceneOverlayContract.TerrainOcclusion,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !HasCompleteHpCornerPayload(root) ||
            !HasCompleteHpCoordinatePayload(root) ||
            !HasCompleteLowDetailPayload(root) ||
            !HasCompleteTerrainOcclusionPayload(root);
    }

    private static bool HasCompleteTerrainOcclusionPayload(JsonElement root)
    {
        if (!TryReadSourceSceneCounts(root, out int expectedSectorCount, out _, out _) ||
            !root.TryGetProperty("terrainOcclusion", out JsonElement payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadInt32(payload, "environmentGroupCount", out int groupCount) ||
            groupCount < 0 ||
            !payload.TryGetProperty("environmentGroups", out JsonElement groups) ||
            groups.ValueKind != JsonValueKind.Array ||
            groups.GetArrayLength() != groupCount ||
            !TryReadInt32(payload, "collisionTriangleCount", out int triangleCount) ||
            triangleCount < 0 ||
            !payload.TryGetProperty("collisionTriangleWords", out JsonElement triangles) ||
            triangles.ValueKind != JsonValueKind.Array ||
            triangles.GetArrayLength() != triangleCount)
        {
            return false;
        }

        foreach (JsonElement group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Array ||
                group.EnumerateArray().Any(sector =>
                    !sector.TryGetInt32(out int index) || index < 0 || index >= expectedSectorCount))
            {
                return false;
            }
        }

        foreach (JsonElement triangle in triangles.EnumerateArray())
        {
            if (triangle.ValueKind != JsonValueKind.Array || triangle.GetArrayLength() != 4)
                return false;
            JsonElement[] row = triangle.EnumerateArray().ToArray();
            if (!row[0].TryGetUInt32(out _) ||
                !row[1].TryGetUInt32(out _) ||
                !row[2].TryGetUInt32(out _) ||
                !row[3].TryGetInt32(out int assignment) ||
                assignment is < 0 or > 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompleteHpCoordinatePayload(JsonElement root)
    {
        if (!TryReadSourceSceneCounts(root, out int expectedSectorCount, out _, out int expectedHpFaces) ||
            !root.TryGetProperty("sectorRenderMetadata", out JsonElement sectorsElement) ||
            sectorsElement.ValueKind != JsonValueKind.Array ||
            sectorsElement.GetArrayLength() != expectedSectorCount ||
            !root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() != 1)
        {
            return false;
        }

        Dictionary<int, (NativeTerrainHpSectorCoordinatePayload Coordinates, int HpFaces)> sectors = [];
        int metadataHpFaces = 0;
        foreach (JsonElement sector in sectorsElement.EnumerateArray())
        {
            if (!TryReadInt32(sector, "sectorIndex", out int sectorIndex) ||
                sectorIndex != sectors.Count ||
                !HasIntegerProperty(sector, "sectorOffset") ||
                JsonValue.GetInt64(sector, "sectorOffset", -1) < 0 ||
                !TryReadInt32(sector, "hpFaces", out int hpFaces) || hpFaces < 0 ||
                !TryReadUInt32(sector, "nativeCenterXyWord", out uint centerXyWord) ||
                !TryReadUInt32(sector, "nativeCenterZRadiusFlagsWord", out uint centerZRadiusFlagsWord) ||
                !TryReadUInt32(sector, "nativeXyPositionWord", out uint xyPositionWord) ||
                !TryReadUInt32(sector, "nativeZPositionWord", out uint zPositionWord) ||
                !sector.TryGetProperty("center", out JsonElement center) || center.ValueKind != JsonValueKind.Object ||
                !TryReadExactInt32(center, "x", out int centerX) ||
                !TryReadExactInt32(center, "y", out int centerY) ||
                !TryReadExactInt32(center, "z", out int centerZ) ||
                !TryReadInt32(sector, "radius", out int radius) ||
                !TryReadBoolean(sector, "disableLowDetail", out bool disableLowDetail) ||
                !TryReadBoolean(sector, "disableHighDetail", out bool disableHighDetail) ||
                !TryReadBoolean(sector, "forceLowDetail", out bool forceLowDetail) ||
                !sector.TryGetProperty("decodedOrigin", out JsonElement origin) || origin.ValueKind != JsonValueKind.Object ||
                !TryReadExactInt32(origin, "x", out int originX) ||
                !TryReadExactInt32(origin, "y", out int originY) ||
                !TryReadExactInt32(origin, "z", out int originZ) ||
                !TryReadInt32(sector, "xOriginQuarterResidue", out int xResidue) ||
                !TryReadInt32(sector, "zOriginQuarterResidue", out int zResidue) ||
                !TryReadBoolean(sector, "specialZScale", out bool specialZScale))
            {
                return false;
            }

            NativeTerrainHpSectorCoordinatePayload coordinates = new(
                centerXyWord,
                centerZRadiusFlagsWord,
                xyPositionWord,
                zPositionWord,
                SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
            if (centerX != coordinates.CenterX ||
                centerY != coordinates.CenterY ||
                centerZ != coordinates.CenterZ ||
                radius != coordinates.Radius ||
                disableLowDetail != coordinates.DisableLowDetail ||
                disableHighDetail != coordinates.DisableHighDetail ||
                forceLowDetail != coordinates.ForceLowDetail ||
                originX != coordinates.DecodedOriginX ||
                originY != coordinates.DecodedOriginY ||
                originZ != coordinates.DecodedOriginZ ||
                xResidue != coordinates.XOriginQuarterResidue ||
                zResidue != coordinates.ZOriginQuarterResidue ||
                specialZScale != coordinates.UsesSpecialZScale ||
                coordinates.UsesSpecialZScale ||
                !sectors.TryAdd(sectorIndex, (coordinates, hpFaces)))
            {
                return false;
            }
            metadataHpFaces += hpFaces;
        }
        if (metadataHpFaces != expectedHpFaces)
            return false;

        JsonElement candidate = candidates.EnumerateArray().Single();
        if (!candidate.TryGetProperty("polygons", out JsonElement polygons) ||
            polygons.ValueKind != JsonValueKind.Array ||
            polygons.GetArrayLength() != expectedHpFaces)
        {
            return false;
        }

        Dictionary<int, int> loadedFacesBySector = [];
        foreach (JsonElement polygon in polygons.EnumerateArray())
        {
            if (!TryReadInt32(polygon, "sectorIndex", out int sectorIndex) ||
                !sectors.TryGetValue(sectorIndex, out var sector) ||
                sector.HpFaces <= 0 ||
                !polygon.TryGetProperty("points", out JsonElement points) ||
                points.ValueKind != JsonValueKind.Array ||
                points.GetArrayLength() is < 3 or > SourceSceneOverlayContract.CornerSlotCount)
            {
                return false;
            }

            foreach (JsonElement point in points.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Object ||
                    !TryReadExactInt32(point, "x", out int x) ||
                    !TryReadExactInt32(point, "y", out int y) ||
                    !TryReadExactInt32(point, "z", out int z))
                {
                    return false;
                }

                try
                {
                    _ = NativeTerrainHighPolyCoordinates.ReconstructCertifiedWorldPointX4(
                        sector.Coordinates,
                        x,
                        y,
                        z);
                }
                catch (Exception ex) when (ex is InvalidDataException or OverflowException)
                {
                    return false;
                }
            }

            loadedFacesBySector[sectorIndex] = loadedFacesBySector.GetValueOrDefault(sectorIndex) + 1;
        }

        return sectors.All(pair =>
            pair.Value.HpFaces == loadedFacesBySector.GetValueOrDefault(pair.Key));
    }

    private static bool HasCompleteLowDetailPayload(JsonElement root)
    {
        if (!TryReadSourceSceneCounts(root, out int expectedSectorCount, out int expectedLpFaces, out _))
            return false;
        if (!root.TryGetProperty("sectorRenderMetadata", out JsonElement sectors) ||
            sectors.ValueKind != JsonValueKind.Array ||
            sectors.GetArrayLength() != expectedSectorCount)
        {
            return false;
        }

        Dictionary<int, int> expectedFacesBySector = new();
        int metadataLpFaces = 0;
        int expectedIndex = 0;
        foreach (JsonElement sector in sectors.EnumerateArray())
        {
            int sectorIndex = JsonValue.GetInt32(sector, "sectorIndex", -1);
            int lpFaces = JsonValue.GetInt32(sector, "lpFaces", -1);
            if (sectorIndex != expectedIndex++ || lpFaces < 0 ||
                !HasIntegerProperty(sector, "sectorOffset") ||
                !HasIntegerProperty(sector, "radius") ||
                !HasBooleanProperty(sector, "disableLowDetail") ||
                !HasBooleanProperty(sector, "disableHighDetail") ||
                !HasBooleanProperty(sector, "forceLowDetail") ||
                !HasNonNegativeIntegerProperty(sector, "lpVertices") ||
                !HasNonNegativeIntegerProperty(sector, "lpColors") ||
                !HasNonNegativeIntegerProperty(sector, "hpVertices") ||
                !HasNonNegativeIntegerProperty(sector, "hpColors") ||
                !HasNonNegativeIntegerProperty(sector, "hpFaces") ||
                !HasVector3(sector, "center"))
            {
                return false;
            }

            expectedFacesBySector[sectorIndex] = lpFaces;
            metadataLpFaces += lpFaces;
        }

        if (metadataLpFaces != expectedLpFaces ||
            !root.TryGetProperty("lowDetail", out JsonElement lowDetail) || lowDetail.ValueKind != JsonValueKind.Object ||
            !string.Equals(JsonValue.GetString(lowDetail, "detail"), "lp", StringComparison.OrdinalIgnoreCase) ||
            JsonValue.GetInt32(lowDetail, "validPolygons", -1) != expectedLpFaces ||
            !lowDetail.TryGetProperty("polygons", out JsonElement polygons) || polygons.ValueKind != JsonValueKind.Array ||
            polygons.GetArrayLength() != expectedLpFaces)
        {
            return false;
        }

        Dictionary<int, HashSet<int>> facesBySector = new();
        foreach (JsonElement polygon in polygons.EnumerateArray())
        {
            if (!HasCompleteLowDetailFacePayload(polygon))
                return false;

            int sectorIndex = JsonValue.GetInt32(polygon, "sectorIndex", -1);
            int faceIndex = JsonValue.GetInt32(polygon, "faceIndex", -1);
            if (!expectedFacesBySector.TryGetValue(sectorIndex, out int sectorFaceCount) ||
                faceIndex < 0 || faceIndex >= sectorFaceCount)
            {
                return false;
            }

            if (!facesBySector.TryGetValue(sectorIndex, out HashSet<int>? faceIndexes))
            {
                faceIndexes = [];
                facesBySector.Add(sectorIndex, faceIndexes);
            }
            if (!faceIndexes.Add(faceIndex))
                return false;
        }

        return expectedFacesBySector.All(pair =>
            pair.Value == 0 ||
            (facesBySector.TryGetValue(pair.Key, out HashSet<int>? faceIndexes) && faceIndexes.Count == pair.Value));
    }

    private static bool TryReadSourceSceneCounts(JsonElement root, out int sectorCount, out int lpFaces, out int hpFaces)
    {
        sectorCount = -1;
        lpFaces = -1;
        hpFaces = -1;
        if (!root.TryGetProperty("sceneCandidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
            return false;

        JsonElement candidate = candidates.EnumerateArray().FirstOrDefault();
        if (candidate.ValueKind == JsonValueKind.Undefined)
            return false;

        sectorCount = JsonValue.GetInt32(candidate, "numSectors", -1);
        lpFaces = JsonValue.GetInt32(candidate, "lpFaces", -1);
        hpFaces = JsonValue.GetInt32(candidate, "hpFaces", -1);
        return sectorCount >= 0 && lpFaces >= 0 && hpFaces >= 0;
    }

    private static bool HasCompleteLowDetailFacePayload(JsonElement polygon)
    {
        if (!polygon.TryGetProperty("points", out JsonElement points) || points.ValueKind != JsonValueKind.Array)
            return false;

        int pointCount = points.GetArrayLength();
        if (pointCount is < 3 or > SourceSceneOverlayContract.CornerSlotCount ||
            !TryReadCornerIntArray(polygon, "vertexIndexes", out int[] vertexIndexes) ||
            !TryReadCornerIntArray(polygon, "cornerPointIndexes", out int[] cornerPointIndexes) ||
            !TryReadCornerIntArray(polygon, "colourIndexes", out int[] colourIndexes) ||
            !HasFourColors(polygon, "cornerColors") ||
            !TryReadUInt32(polygon, "rawWord0", out uint rawWord0) ||
            !TryReadUInt32(polygon, "rawWord1", out uint rawWord1))
        {
            return false;
        }

        int[] nativeVertexIndexes = ReadPackedSixBitSlots(rawWord0);
        int[] nativeColourIndexes = ReadPackedSixBitSlots(rawWord1);
        if (!vertexIndexes.SequenceEqual(nativeVertexIndexes) ||
            !colourIndexes.SequenceEqual(nativeColourIndexes) ||
            JsonValue.GetInt32(polygon, "transitionBias", -1) != (int)(rawWord0 & 0x1F) ||
            JsonValue.GetBoolean(polygon, "doubleSided") != ((rawWord0 & 0x80) != 0) ||
            JsonValue.GetBoolean(polygon, "semiTransparent") != ((rawWord1 & 0x04) != 0) ||
            JsonValue.GetInt32(polygon, "blendMode", -1) != (int)(rawWord1 & 0x03) ||
            JsonValue.GetInt32(polygon, "orderingTableBias", -1) != (int)((rawWord1 >> 3) & 0x1F))
        {
            return false;
        }

        Dictionary<int, int> expectedPointIndexByVertex = new();
        for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
        {
            if (vertexIndexes[slot] is < 0 or > 0x3F || colourIndexes[slot] is < 0 or > 0x3F)
                return false;

            int vertexIndex = vertexIndexes[slot];
            if (!expectedPointIndexByVertex.TryGetValue(vertexIndex, out int expectedPointIndex))
            {
                expectedPointIndex = expectedPointIndexByVertex.Count;
                expectedPointIndexByVertex.Add(vertexIndex, expectedPointIndex);
            }
            if (cornerPointIndexes[slot] != expectedPointIndex || cornerPointIndexes[slot] < 0 || cornerPointIndexes[slot] >= pointCount)
                return false;
        }

        return expectedPointIndexByVertex.Count == pointCount;
    }

    private static int[] ReadPackedSixBitSlots(uint word) =>
        [(int)((word >> 26) & 0x3F), (int)((word >> 20) & 0x3F), (int)((word >> 14) & 0x3F), (int)((word >> 8) & 0x3F)];

    private static bool HasCompleteHpCornerPayload(JsonElement root)
    {
        if (!TryReadSourceSceneCounts(root, out int expectedSectorCount, out _, out int expectedHpFaces) ||
            !root.TryGetProperty("sectorRenderMetadata", out JsonElement sectors) ||
            sectors.ValueKind != JsonValueKind.Array ||
            sectors.GetArrayLength() != expectedSectorCount ||
            !root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() != 1)
        {
            return false;
        }

        Dictionary<int, (int Faces, int Vertices, int Colours)> expectedFacesBySector = [];
        int metadataHpFaces = 0;
        int expectedSectorIndex = 0;
        foreach (JsonElement sector in sectors.EnumerateArray())
        {
            int sectorIndex = JsonValue.GetInt32(sector, "sectorIndex", -1);
            int hpFaces = JsonValue.GetInt32(sector, "hpFaces", -1);
            int hpVertices = JsonValue.GetInt32(sector, "hpVertices", -1);
            int hpColours = JsonValue.GetInt32(sector, "hpColors", -1);
            if (sectorIndex != expectedSectorIndex++ || hpFaces < 0 || hpVertices < 0 || hpColours < 0)
                return false;
            expectedFacesBySector.Add(sectorIndex, (hpFaces, hpVertices, hpColours));
            metadataHpFaces += hpFaces;
        }
        if (metadataHpFaces != expectedHpFaces)
            return false;

        bool foundHpFace = false;
        int serializedHpFaces = 0;
        Dictionary<int, HashSet<int>> facesBySector = [];
        foreach (JsonElement candidate in candidates.EnumerateArray())
        {
            string candidateDetail = JsonValue.GetString(candidate, "detail");
            if (!candidateDetail.Equals("hp", StringComparison.OrdinalIgnoreCase) ||
                !candidate.TryGetProperty("polygons", out JsonElement polygons) ||
                polygons.ValueKind != JsonValueKind.Array ||
                JsonValue.GetInt32(candidate, "validPolygons", -1) != expectedHpFaces ||
                polygons.GetArrayLength() != expectedHpFaces)
            {
                return false;
            }

            foreach (JsonElement polygon in polygons.EnumerateArray())
            {
                string detail = JsonValue.GetString(polygon, "detail");
                if (string.IsNullOrWhiteSpace(detail))
                    detail = candidateDetail;
                if (!detail.Equals("hp", StringComparison.OrdinalIgnoreCase))
                    continue;

                foundHpFace = true;
                serializedHpFaces++;
                if (!HasCompleteHpFacePayload(polygon))
                    return false;

                int sectorIndex = JsonValue.GetInt32(polygon, "sectorIndex", -1);
                int faceIndex = JsonValue.GetInt32(polygon, "faceIndex", -1);
                if (!expectedFacesBySector.TryGetValue(sectorIndex, out var expected) ||
                    faceIndex < 0 || faceIndex >= expected.Faces ||
                    !TryReadCornerIntArray(polygon, "vertexIndexes", out int[] vertexIndexes) ||
                    vertexIndexes.Any(index => index < 0 || index >= expected.Vertices) ||
                    !TryReadCornerIntArray(polygon, "colourIndexes", out int[] colourIndexes) ||
                    colourIndexes.Any(index => index < 0 || index >= expected.Colours))
                {
                    return false;
                }

                if (!facesBySector.TryGetValue(sectorIndex, out HashSet<int>? faceIndexes))
                {
                    faceIndexes = [];
                    facesBySector.Add(sectorIndex, faceIndexes);
                }
                if (!faceIndexes.Add(faceIndex))
                    return false;
            }
        }

        return foundHpFace &&
            serializedHpFaces == expectedHpFaces &&
            expectedFacesBySector.All(pair =>
                pair.Value.Faces == 0 ||
                (facesBySector.TryGetValue(pair.Key, out HashSet<int>? indexes) && indexes.Count == pair.Value.Faces));
    }

    private static bool HasCompleteHpFacePayload(JsonElement polygon)
    {
        if (!polygon.TryGetProperty("points", out JsonElement points) || points.ValueKind != JsonValueKind.Array)
            return false;

        int pointCount = points.GetArrayLength();
        if (pointCount is < 3 or > SourceSceneOverlayContract.CornerSlotCount ||
            !TryReadCornerIntArray(polygon, "vertexIndexes", out int[] vertexIndexes) ||
            !TryReadCornerIntArray(polygon, "cornerPointIndexes", out int[] cornerPointIndexes) ||
            !TryReadCornerIntArray(polygon, "colourIndexes", out int[] colourIndexes) ||
            !HasFourColors(polygon, "nearColors") ||
            !HasFourColors(polygon, "farColors") ||
            !TryReadUInt32(polygon, "rawWord0", out uint nativeWord0) ||
            !TryReadUInt32(polygon, "rawWord1", out uint nativeWord1) ||
            !TryReadUInt32(polygon, "word3", out uint legacyWord2) ||
            !TryReadUInt32(polygon, "nativeFaceWord2", out uint nativeWord2) ||
            legacyWord2 != nativeWord2 ||
            !TryReadUInt32(polygon, "word4", out uint legacyWord3) ||
            !TryReadUInt32(polygon, "nativeFaceWord3", out uint nativeWord3) ||
            legacyWord3 != nativeWord3 ||
            !TryReadByte(polygon, "nativeMaterialByte", out byte nativeMaterialByte) ||
            !TryReadBoolean(polygon, "nativeUntexturedSentinel", out bool nativeUntexturedSentinel) ||
            !TryReadBoolean(polygon, "nativePrimitiveSemiTransparent", out bool nativePrimitiveSemiTransparent) ||
            !TryReadInt32(polygon, "nativeTextureId", out int nativeTextureId) ||
            !TryReadInt32(polygon, "textureId", out int textureId))
        {
            return false;
        }

        if (!TryPackNativeByteSlots(vertexIndexes, out uint packedWord0) ||
            packedWord0 != nativeWord0 ||
            !TryPackNativeByteSlots(colourIndexes, out uint packedWord1) ||
            packedWord1 != nativeWord1 ||
            !TryReadBoolean(polygon, "flip", out bool faceFlip) ||
            faceFlip != ((nativeWord3 & 0x02) != 0) ||
            !TryReadInt32(polygon, "depth", out int faceDepth) ||
            faceDepth != (int)((nativeWord3 >> 3) & 0x1F))
        {
            return false;
        }

        PsxTerrainPrimitiveClassification expectedMaterial =
            PsxTerrainBlendKernel.ClassifyHighPolyMaterialByte((byte)(nativeWord2 & 0xFF));
        if (nativeMaterialByte != expectedMaterial.RawMaterialByte ||
            nativeUntexturedSentinel != expectedMaterial.IsOpaqueUntexturedSentinel ||
            nativePrimitiveSemiTransparent != expectedMaterial.PrimitiveSemiTransparent ||
            nativeTextureId != expectedMaterial.TextureId ||
            textureId != expectedMaterial.TextureId)
        {
            return false;
        }

        if (vertexIndexes.Any(index => index < 0) ||
            colourIndexes.Any(index => index is < 0 or > byte.MaxValue) ||
            cornerPointIndexes.Any(index => index < 0 || index >= pointCount))
        {
            return false;
        }

        Dictionary<int, int> expectedPointIndexByVertex = new();
        for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
        {
            int vertexIndex = vertexIndexes[slot];
            if (!expectedPointIndexByVertex.TryGetValue(vertexIndex, out int expectedPointIndex))
            {
                expectedPointIndex = expectedPointIndexByVertex.Count;
                expectedPointIndexByVertex.Add(vertexIndex, expectedPointIndex);
            }

            if (cornerPointIndexes[slot] != expectedPointIndex)
                return false;
        }

        return expectedPointIndexByVertex.Count == pointCount;
    }

    private static bool TryPackNativeByteSlots(IReadOnlyList<int> slots, out uint word)
    {
        word = 0;
        if (slots.Count != SourceSceneOverlayContract.CornerSlotCount ||
            slots.Any(slot => slot is < byte.MinValue or > byte.MaxValue))
        {
            return false;
        }

        word = (uint)slots[0] |
            ((uint)slots[1] << 8) |
            ((uint)slots[2] << 16) |
            ((uint)slots[3] << 24);
        return true;
    }

    private static bool TryReadCornerIntArray(JsonElement element, string name, out int[] values)
    {
        values = Array.Empty<int>();
        if (!element.TryGetProperty(name, out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() != SourceSceneOverlayContract.CornerSlotCount)
        {
            return false;
        }

        values = new int[SourceSceneOverlayContract.CornerSlotCount];
        int index = 0;
        foreach (JsonElement value in array.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out values[index]))
                return false;
            index++;
        }

        return true;
    }

    private static bool HasFourColors(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement colors) ||
            colors.ValueKind != JsonValueKind.Array ||
            colors.GetArrayLength() != SourceSceneOverlayContract.CornerSlotCount)
        {
            return false;
        }

        foreach (JsonElement color in colors.EnumerateArray())
        {
            if (color.ValueKind != JsonValueKind.Object ||
                !HasByteProperty(color, "r") ||
                !HasByteProperty(color, "g") ||
                !HasByteProperty(color, "b") ||
                !HasByteProperty(color, "a"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasByteProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out int number) &&
            number is >= byte.MinValue and <= byte.MaxValue;
    }

    private static bool TryReadByte(JsonElement element, string name, out byte value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetByte(out value);
    }

    private static bool TryReadBoolean(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadExactInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }
        if (property.TryGetInt32(out value))
            return true;
        if (!property.TryGetDouble(out double number) ||
            !double.IsFinite(number) ||
            number < int.MinValue ||
            number > int.MaxValue ||
            number != Math.Truncate(number))
        {
            return false;
        }
        value = checked((int)number);
        return true;
    }

    private static bool HasIntegerProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) ||
         value.ValueKind == JsonValueKind.String && JsonValue.GetInt64(element, name, long.MinValue) != long.MinValue);

    private static bool HasNonNegativeIntegerProperty(JsonElement element, string name) =>
        HasIntegerProperty(element, name) && JsonValue.GetInt64(element, name, -1) >= 0;

    private static bool HasBooleanProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool HasVector3(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement vector) || vector.ValueKind != JsonValueKind.Object)
            return false;

        return HasFiniteNumber(vector, "x") && HasFiniteNumber(vector, "y") && HasFiniteNumber(vector, "z");
    }

    private static bool HasFiniteNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double number) &&
        double.IsFinite(number);

    private static bool TryReadUInt32(JsonElement element, string name, out uint value)
    {
        value = 0;
        if (!HasIntegerProperty(element, name))
            return false;

        long number = JsonValue.GetInt64(element, name, -1);
        if (number < uint.MinValue || number > uint.MaxValue)
            return false;

        value = (uint)number;
        return true;
    }

    private static bool IsKnownBadGnastysLootOverlay(string levelKey, JsonElement root)
    {
        if (!string.Equals(levelKey, "gnastysloot", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("candidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
            return false;

        JsonElement candidate = candidates.EnumerateArray().FirstOrDefault();
        if (candidate.ValueKind == JsonValueKind.Undefined)
            return false;

        string sceneAddress = JsonValue.GetString(candidate, "sceneRuntimeAddress");
        int sectorCount = JsonValue.GetInt32(candidate, "sectorCount", -1);
        int validPolygons = JsonValue.GetInt32(candidate, "validPolygons", -1);
        if (!string.Equals(sceneAddress, "0x800873E4", StringComparison.OrdinalIgnoreCase)
            || sectorCount != 115
            || validPolygons != 3435)
        {
            return false;
        }

        if (!candidate.TryGetProperty("projectedBounds", out JsonElement bounds) || bounds.ValueKind != JsonValueKind.Object)
            return false;

        return JsonValue.GetInt32(bounds, "minX", -1) == 415
            && JsonValue.GetInt32(bounds, "maxX", -1) == 9880
            && JsonValue.GetInt32(bounds, "minY", -1) == 420
            && JsonValue.GetInt32(bounds, "maxY", -1) == 9875;
    }
}
