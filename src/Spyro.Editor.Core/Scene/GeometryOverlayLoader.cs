using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Rendering;

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

        return LoadCandidate(first, document.RootElement);
    }

    private static GeometryCandidate LoadCandidate(JsonElement candidate, JsonElement root)
    {
        int sourceOverlayFormatVersion = JsonValue.GetInt32(root, "sourceOverlayFormatVersion", 0);
        string highPolyMaterialContract = JsonValue.GetString(root, "hpFaceMaterialPayload");
        string highPolyCoordinateContract = JsonValue.GetString(root, "hpCoordinatePayload");
        bool hasExplicitSourceProvenance = SourceSceneOverlayContract.HasExplicitSourceProvenance(root);
        bool requiresExactHighPolyMaterialState =
            hasExplicitSourceProvenance ||
            sourceOverlayFormatVersion >= SourceSceneOverlayContract.CurrentFormatVersion ||
            string.Equals(
                highPolyMaterialContract,
                SourceSceneOverlayContract.HpFaceMaterialPayload,
                StringComparison.Ordinal);
        if (requiresExactHighPolyMaterialState &&
            (sourceOverlayFormatVersion < SourceSceneOverlayContract.CurrentFormatVersion ||
             !string.Equals(
                 highPolyMaterialContract,
                 SourceSceneOverlayContract.HpFaceMaterialPayload,
                 StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Source-scene overlay is missing the current exact HP raw-word 0-3 contract.");
        }
        bool requiresExactHighPolyCoordinateState =
            hasExplicitSourceProvenance ||
            sourceOverlayFormatVersion >= SourceSceneOverlayContract.CurrentFormatVersion ||
            string.Equals(
                highPolyCoordinateContract,
                SourceSceneOverlayContract.HpCoordinatePayload,
                StringComparison.Ordinal);
        if (requiresExactHighPolyCoordinateState &&
            (sourceOverlayFormatVersion < SourceSceneOverlayContract.CurrentFormatVersion ||
             !string.Equals(
                 highPolyCoordinateContract,
                 SourceSceneOverlayContract.HpCoordinatePayload,
                 StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Source-scene overlay is missing the current exact HP scene-sector coordinate contract.");
        }

        GeometryCandidate result = new()
        {
            Name = JsonValue.GetString(candidate, "runtimeAddress", "geometry"),
            Bounds = ReadBounds(candidate),
            MinZ = ReadHeightBound(candidate, "minZ", 0),
            MaxZ = ReadHeightBound(candidate, "maxZ", 1),
            TerrainLodPreviewContract = JsonValue.GetString(root, "terrainLodPreview"),
            HighPolyMaterialContract = highPolyMaterialContract,
            HighPolyCoordinateContract = highPolyCoordinateContract
        };

        int expectedHighPolyFaceCount = requiresExactHighPolyMaterialState
            ? ReadExpectedHighPolyFaceCount(candidate, root)
            : -1;

        result.HasStaticLowDetailPreview =
            sourceOverlayFormatVersion >= SourceSceneOverlayContract.CurrentFormatVersion &&
            string.Equals(highPolyMaterialContract, SourceSceneOverlayContract.HpFaceMaterialPayload, StringComparison.Ordinal) &&
            string.Equals(highPolyCoordinateContract, SourceSceneOverlayContract.HpCoordinatePayload, StringComparison.Ordinal) &&
            string.Equals(JsonValue.GetString(root, "lpColorLayout"), SourceSceneOverlayContract.LpColorLayout, StringComparison.Ordinal) &&
            string.Equals(JsonValue.GetString(root, "lpFacePayload"), SourceSceneOverlayContract.LpFacePayload, StringComparison.Ordinal) &&
            string.Equals(result.TerrainLodPreviewContract, SourceSceneOverlayContract.TerrainLodPreview, StringComparison.Ordinal);

        if (candidate.TryGetProperty("polygons", out JsonElement polygons) && polygons.ValueKind == JsonValueKind.Array)
        {
            string candidateDetail = JsonValue.GetString(candidate, "detail");
            foreach (JsonElement polygon in polygons.EnumerateArray())
            {
                TerrainPolygon? parsed = ReadPolygon(
                    polygon,
                    candidateDetail,
                    requiresExactHighPolyMaterialState);
                if (parsed != null)
                    result.Polygons.Add(parsed);
            }
        }

        bool hasHighPolyFace = result.Polygons.Any(polygon =>
            polygon.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase));
        int loadedHighPolyFaceCount = result.Polygons.Count(polygon =>
            polygon.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase));
        if (requiresExactHighPolyMaterialState)
            ValidateExactHighPolyFaceCoverage(result.Polygons, root, expectedHighPolyFaceCount);
        result.HasExactHighPolyMaterialState =
            requiresExactHighPolyMaterialState &&
            hasHighPolyFace &&
            loadedHighPolyFaceCount == expectedHighPolyFaceCount &&
            result.Polygons
                .Where(polygon => polygon.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase))
                .All(polygon => polygon.HasCompleteNativeHighPolyFacePayload);
        if (requiresExactHighPolyMaterialState && !result.HasExactHighPolyMaterialState)
            throw new InvalidDataException("Current source-scene overlay has an incomplete HP raw-word 0-3 payload.");

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


        if (root.TryGetProperty("sectorRenderMetadata", out JsonElement sectorMetadata) && sectorMetadata.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sector in sectorMetadata.EnumerateArray())
            {
                if (!sector.TryGetProperty("center", out JsonElement center) || center.ValueKind != JsonValueKind.Object)
                {
                    if (requiresExactHighPolyCoordinateState)
                        throw new InvalidDataException("Current source-scene overlay sector metadata is missing its derived center.");
                    continue;
                }

                Vector3f derivedCenter = new(
                    JsonValue.GetSingle(center, "x"),
                    JsonValue.GetSingle(center, "y"),
                    JsonValue.GetSingle(center, "z"));
                int radius = JsonValue.GetInt32(sector, "radius");
                bool disableLowDetail = JsonValue.GetBoolean(sector, "disableLowDetail");
                bool disableHighDetail = JsonValue.GetBoolean(sector, "disableHighDetail");
                bool forceLowDetail = JsonValue.GetBoolean(sector, "forceLowDetail");
                NativeTerrainHpSectorCoordinatePayload? highPolyCoordinates = requiresExactHighPolyCoordinateState
                    ? ReadNativeHpSectorCoordinatePayload(
                        sector,
                        derivedCenter,
                        radius,
                        disableLowDetail,
                        disableHighDetail,
                        forceLowDetail)
                    : null;

                result.SourceSectors.Add(new SceneSectorRenderMetadata(
                    JsonValue.GetInt32(sector, "sectorIndex", -1),
                    JsonValue.GetInt32(sector, "sectorOffset", -1),
                    derivedCenter,
                    radius,
                    disableLowDetail,
                    disableHighDetail,
                    forceLowDetail,
                    JsonValue.GetInt32(sector, "lpVertices"),
                    JsonValue.GetInt32(sector, "lpColors"),
                    JsonValue.GetInt32(sector, "lpFaces"),
                    JsonValue.GetInt32(sector, "hpVertices"),
                    JsonValue.GetInt32(sector, "hpColors"),
                    JsonValue.GetInt32(sector, "hpFaces"),
                    highPolyCoordinates));
            }
        }

        bool requiresTerrainOcclusion =
            sourceOverlayFormatVersion >= SourceSceneOverlayContract.CurrentFormatVersion ||
            root.TryGetProperty("terrainOcclusionContract", out _);
        if (requiresTerrainOcclusion)
        {
            result.NativeTerrainOcclusion = ReadNativeTerrainOcclusion(
                root,
                result.SourceSectors.Count);
        }

        if (requiresExactHighPolyCoordinateState)
            ValidateExactHighPolyCoordinateCoverage(result, root, expectedHighPolyFaceCount);
        result.HasExactHighPolyCoordinateState =
            requiresExactHighPolyCoordinateState &&
            result.SourceSectors.Count > 0 &&
            result.SourceSectors.All(sector => sector.HighPolyCoordinates.HasValue);
        if (requiresExactHighPolyCoordinateState && !result.HasExactHighPolyCoordinateState)
            throw new InvalidDataException("Current source-scene overlay has an incomplete HP scene-sector coordinate payload.");

        if (root.TryGetProperty("lowDetail", out JsonElement lowDetail) && lowDetail.ValueKind == JsonValueKind.Object &&
            lowDetail.TryGetProperty("polygons", out JsonElement lowDetailPolygons) && lowDetailPolygons.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement polygon in lowDetailPolygons.EnumerateArray())
            {
                LowDetailTerrainPolygon? parsed = ReadLowDetailPolygon(polygon);
                if (parsed != null)
                    result.LowDetailPolygons.Add(parsed);
            }
        }

        if (result.Bounds.IsEmpty)
            result.Bounds = ComputeBounds(result);

        return result;
    }

    private static NativeTerrainOcclusionData ReadNativeTerrainOcclusion(
        JsonElement root,
        int sourceSectorCount)
    {
        string contract = JsonValue.GetString(root, "terrainOcclusionContract");
        if (!string.Equals(contract, SourceSceneOverlayContract.TerrainOcclusion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Source-scene overlay terrain occlusion contract is '{contract}', expected '{SourceSceneOverlayContract.TerrainOcclusion}'.");
        }
        if (!root.TryGetProperty("terrainOcclusion", out JsonElement payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Source-scene overlay is missing its native terrain occlusion payload.");
        }

        int environmentGroupCount = JsonValue.GetInt32(payload, "environmentGroupCount", -1);
        if (environmentGroupCount < 0 ||
            !payload.TryGetProperty("environmentGroups", out JsonElement groupsElement) ||
            groupsElement.ValueKind != JsonValueKind.Array ||
            groupsElement.GetArrayLength() != environmentGroupCount)
        {
            throw new InvalidDataException("Source-scene overlay has inconsistent native environment occlusion groups.");
        }

        List<IReadOnlyList<int>> groups = new(environmentGroupCount);
        int groupIndex = 0;
        foreach (JsonElement groupElement in groupsElement.EnumerateArray())
        {
            if (groupElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Native environment occlusion group {groupIndex} is not an array.");
            List<int> sectors = new(groupElement.GetArrayLength());
            foreach (JsonElement sectorElement in groupElement.EnumerateArray())
            {
                if (!sectorElement.TryGetInt32(out int sectorIndex) ||
                    sectorIndex < 0 ||
                    sectorIndex >= sourceSectorCount)
                {
                    throw new InvalidDataException(
                        $"Native environment occlusion group {groupIndex} contains an invalid sector index.");
                }
                sectors.Add(sectorIndex);
            }
            groups.Add(sectors);
            groupIndex++;
        }

        int collisionTriangleCount = JsonValue.GetInt32(payload, "collisionTriangleCount", -1);
        if (collisionTriangleCount < 0 ||
            !payload.TryGetProperty("collisionTriangleWords", out JsonElement trianglesElement) ||
            trianglesElement.ValueKind != JsonValueKind.Array ||
            trianglesElement.GetArrayLength() != collisionTriangleCount)
        {
            throw new InvalidDataException("Source-scene overlay has inconsistent collision-triangle occlusion assignments.");
        }

        List<NativeTerrainOcclusionTriangle> triangles = new(collisionTriangleCount);
        int triangleIndex = 0;
        foreach (JsonElement triangleElement in trianglesElement.EnumerateArray())
        {
            if (triangleElement.ValueKind != JsonValueKind.Array ||
                triangleElement.GetArrayLength() != 4)
            {
                throw new InvalidDataException($"Native collision triangle {triangleIndex} is not a four-word row.");
            }

            JsonElement.ArrayEnumerator words = triangleElement.EnumerateArray();
            words.MoveNext();
            uint xWord = words.Current.GetUInt32();
            words.MoveNext();
            uint yWord = words.Current.GetUInt32();
            words.MoveNext();
            uint zWord = words.Current.GetUInt32();
            words.MoveNext();
            int assignment = words.Current.GetInt32();
            if (assignment is < 0 or > 0xFF)
                throw new InvalidDataException($"Native collision triangle {triangleIndex} has an invalid occlusion assignment.");
            triangles.Add(NativeTerrainOcclusionTriangle.FromWords(
                triangleIndex,
                xWord,
                yWord,
                zWord,
                assignment));
            triangleIndex++;
        }

        return new NativeTerrainOcclusionData(groups, triangles);
    }

    private static int ReadExpectedHighPolyFaceCount(JsonElement candidate, JsonElement root)
    {
        if (!TryReadRequiredInt32(candidate, "validPolygons", out int candidateFaceCount) ||
            candidateFaceCount < 0 ||
            !candidate.TryGetProperty("polygons", out JsonElement polygons) ||
            polygons.ValueKind != JsonValueKind.Array ||
            polygons.GetArrayLength() != candidateFaceCount ||
            !root.TryGetProperty("sceneCandidates", out JsonElement sceneCandidates) ||
            sceneCandidates.ValueKind != JsonValueKind.Array ||
            sceneCandidates.GetArrayLength() != 1)
        {
            throw new InvalidDataException("Current source-scene overlay is missing a consistent HP face count.");
        }

        JsonElement sourceScene = sceneCandidates.EnumerateArray().FirstOrDefault();
        if (sourceScene.ValueKind == JsonValueKind.Undefined ||
            !TryReadRequiredInt32(sourceScene, "hpFaces", out int nativeFaceCount) ||
            nativeFaceCount < 0 ||
            nativeFaceCount != candidateFaceCount)
        {
            throw new InvalidDataException("Current source-scene overlay HP face count does not match its source-scene metadata.");
        }

        return nativeFaceCount;
    }

    private static void ValidateExactHighPolyFaceCoverage(
        IReadOnlyList<TerrainPolygon> polygons,
        JsonElement root,
        int expectedFaceCount)
    {
        if (!root.TryGetProperty("sectorRenderMetadata", out JsonElement sectors) ||
            sectors.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("sceneCandidates", out JsonElement sceneCandidates) ||
            sceneCandidates.ValueKind != JsonValueKind.Array ||
            sceneCandidates.GetArrayLength() != 1)
        {
            throw new InvalidDataException("Current source-scene overlay is missing exact HP sector metadata.");
        }

        JsonElement sourceScene = sceneCandidates.EnumerateArray().Single();
        if (!TryReadRequiredInt32(sourceScene, "numSectors", out int expectedSectorCount) ||
            expectedSectorCount < 0 ||
            sectors.GetArrayLength() != expectedSectorCount)
        {
            throw new InvalidDataException("Current source-scene overlay HP sector metadata count does not match its source-scene metadata.");
        }

        Dictionary<int, (int Faces, int Vertices, int Colours)> expectedBySector = [];
        int metadataFaceCount = 0;
        foreach (JsonElement sector in sectors.EnumerateArray())
        {
            if (!TryReadRequiredInt32(sector, "sectorIndex", out int sectorIndex) ||
                sectorIndex != expectedBySector.Count ||
                !TryReadRequiredInt32(sector, "hpFaces", out int faceCount) || faceCount < 0 ||
                !TryReadRequiredInt32(sector, "hpVertices", out int vertexCount) || vertexCount < 0 ||
                !TryReadRequiredInt32(sector, "hpColors", out int colourCount) || colourCount < 0)
            {
                throw new InvalidDataException("Current source-scene overlay has invalid HP sector metadata.");
            }

            expectedBySector.Add(sectorIndex, (faceCount, vertexCount, colourCount));
            metadataFaceCount += faceCount;
        }
        if (metadataFaceCount != expectedFaceCount)
            throw new InvalidDataException("Current source-scene overlay HP sector face counts do not match its source-scene metadata.");

        Dictionary<int, HashSet<int>> facesBySector = [];
        foreach (TerrainPolygon polygon in polygons.Where(face =>
                     face.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase)))
        {
            if (!expectedBySector.TryGetValue(polygon.SectorIndex, out var expected) ||
                polygon.FaceIndex < 0 || polygon.FaceIndex >= expected.Faces ||
                polygon.VertexIndexes.Any(index => index < 0 || index >= expected.Vertices) ||
                polygon.ColourIndexes.Any(index => index < 0 || index >= expected.Colours))
            {
                throw new InvalidDataException($"Current source-scene overlay has an out-of-range HP face identity or slot index at {polygon.RuntimeKey}.");
            }

            if (!facesBySector.TryGetValue(polygon.SectorIndex, out HashSet<int>? faceIndexes))
            {
                faceIndexes = [];
                facesBySector.Add(polygon.SectorIndex, faceIndexes);
            }
            if (!faceIndexes.Add(polygon.FaceIndex))
                throw new InvalidDataException($"Current source-scene overlay has a duplicate HP face identity at {polygon.RuntimeKey}.");
        }

        if (expectedBySector.Any(pair =>
                pair.Value.Faces > 0 &&
                (!facesBySector.TryGetValue(pair.Key, out HashSet<int>? indexes) || indexes.Count != pair.Value.Faces)))
        {
            throw new InvalidDataException("Current source-scene overlay is missing one or more HP face identities.");
        }
    }

    private static NativeTerrainHpSectorCoordinatePayload ReadNativeHpSectorCoordinatePayload(
        JsonElement sector,
        Vector3f derivedCenter,
        int radius,
        bool disableLowDetail,
        bool disableHighDetail,
        bool forceLowDetail)
    {
        if (!TryReadUInt32(sector, "nativeCenterXyWord", out uint centerXyWord) ||
            !TryReadUInt32(sector, "nativeCenterZRadiusFlagsWord", out uint centerZRadiusFlagsWord) ||
            !TryReadUInt32(sector, "nativeXyPositionWord", out uint xyPositionWord) ||
            !TryReadUInt32(sector, "nativeZPositionWord", out uint zPositionWord) ||
            !sector.TryGetProperty("decodedOrigin", out JsonElement decodedOrigin) ||
            decodedOrigin.ValueKind != JsonValueKind.Object ||
            !TryReadRequiredInt32(decodedOrigin, "x", out int originX) ||
            !TryReadRequiredInt32(decodedOrigin, "y", out int originY) ||
            !TryReadRequiredInt32(decodedOrigin, "z", out int originZ) ||
            !TryReadRequiredInt32(sector, "xOriginQuarterResidue", out int xResidue) ||
            !TryReadRequiredInt32(sector, "zOriginQuarterResidue", out int zResidue) ||
            !TryReadRequiredBoolean(sector, "specialZScale", out bool specialZScale))
        {
            throw new InvalidDataException(
                "Current source-scene overlay sector is missing a complete raw header-word HP coordinate payload.");
        }

        NativeTerrainHpSectorCoordinatePayload payload = new(
            centerXyWord,
            centerZRadiusFlagsWord,
            xyPositionWord,
            zPositionWord,
            SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
        if (!IsExactInteger(derivedCenter.X, payload.CenterX) ||
            !IsExactInteger(derivedCenter.Y, payload.CenterY) ||
            !IsExactInteger(derivedCenter.Z, payload.CenterZ) ||
            radius != payload.Radius ||
            disableLowDetail != payload.DisableLowDetail ||
            disableHighDetail != payload.DisableHighDetail ||
            forceLowDetail != payload.ForceLowDetail ||
            originX != payload.DecodedOriginX ||
            originY != payload.DecodedOriginY ||
            originZ != payload.DecodedOriginZ ||
            xResidue != payload.XOriginQuarterResidue ||
            zResidue != payload.ZOriginQuarterResidue ||
            specialZScale != payload.UsesSpecialZScale)
        {
            throw new InvalidDataException(
                "Current source-scene overlay sector raw header words do not match its derived center, radius/flags, origin, residue, or special-Z fields.");
        }
        if (payload.UsesSpecialZScale)
        {
            throw new InvalidDataException(
                "Current source-scene overlay uses unsupported scene-sector header bit 12 (special Z scaling).");
        }

        return payload;
    }

    private static void ValidateExactHighPolyCoordinateCoverage(
        GeometryCandidate geometry,
        JsonElement root,
        int expectedHighPolyFaceCount)
    {
        if (!root.TryGetProperty("sceneCandidates", out JsonElement sceneCandidates) ||
            sceneCandidates.ValueKind != JsonValueKind.Array ||
            sceneCandidates.GetArrayLength() != 1)
        {
            throw new InvalidDataException("Current source-scene overlay is missing source-scene coordinate metadata.");
        }

        JsonElement sourceScene = sceneCandidates.EnumerateArray().Single();
        if (!TryReadRequiredInt32(sourceScene, "numSectors", out int expectedSectorCount) ||
            expectedSectorCount < 0 ||
            geometry.SourceSectors.Count != expectedSectorCount)
        {
            throw new InvalidDataException(
                "Current source-scene overlay HP coordinate sector count does not match its source-scene metadata.");
        }

        Dictionary<int, SceneSectorRenderMetadata> sectors = [];
        int metadataHighPolyFaceCount = 0;
        foreach (SceneSectorRenderMetadata sector in geometry.SourceSectors)
        {
            if (sector.SectorIndex != sectors.Count ||
                sector.SectorOffset < 0 ||
                sector.HighDetailFaceCount < 0 ||
                !sector.HighPolyCoordinates.HasValue ||
                sector.HighPolyCoordinates.Value.UsesSpecialZScale ||
                !sectors.TryAdd(sector.SectorIndex, sector))
            {
                throw new InvalidDataException(
                    "Current source-scene overlay has invalid, duplicate, or unsupported HP coordinate sector metadata.");
            }
            metadataHighPolyFaceCount += sector.HighDetailFaceCount;
        }
        if (metadataHighPolyFaceCount != expectedHighPolyFaceCount)
        {
            throw new InvalidDataException(
                "Current source-scene overlay HP coordinate sector face counts do not match its source-scene metadata.");
        }

        Dictionary<int, int> loadedFacesBySector = [];
        foreach (TerrainPolygon polygon in geometry.Polygons.Where(face =>
                     face.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase)))
        {
            if (!sectors.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector) ||
                sector.HighDetailFaceCount <= 0 ||
                !sector.HighPolyCoordinates.HasValue)
            {
                throw new InvalidDataException(
                    $"Current source-scene overlay HP face {polygon.RuntimeKey} has no certified coordinate sector.");
            }

            foreach ((Vector2f point, float z) in polygon.Points.Zip(polygon.ZValues))
            {
                if (!TryGetExactInteger(point.X, out int x) ||
                    !TryGetExactInteger(point.Y, out int y) ||
                    !TryGetExactInteger(z, out int worldZ))
                {
                    throw new InvalidDataException(
                        $"Current source-scene overlay HP face {polygon.RuntimeKey} has a non-integer source point.");
                }

                _ = NativeTerrainHighPolyCoordinates.ReconstructCertifiedWorldPointX4(
                    sector.HighPolyCoordinates.Value,
                    x,
                    y,
                    worldZ);
            }

            loadedFacesBySector[polygon.SectorIndex] =
                loadedFacesBySector.GetValueOrDefault(polygon.SectorIndex) + 1;
        }

        if (sectors.Values.Any(sector =>
                sector.HighDetailFaceCount != loadedFacesBySector.GetValueOrDefault(sector.SectorIndex)))
        {
            throw new InvalidDataException(
                "Current source-scene overlay HP face-to-coordinate-sector coverage is incomplete.");
        }
    }

    private static bool IsExactInteger(float value, int expected) =>
        float.IsFinite(value) && value == expected;

    private static bool TryGetExactInteger(float value, out int result)
    {
        result = 0;
        if (!float.IsFinite(value) || value < int.MinValue || value > int.MaxValue || value != MathF.Truncate(value))
            return false;
        result = checked((int)value);
        return true;
    }

    private static LowDetailTerrainPolygon? ReadLowDetailPolygon(JsonElement polygon)
    {
        if (!TryReadPoints(polygon, out List<Vector2f> points, out List<float> zValues))
            return null;

        int sectorOffset = JsonValue.GetInt32(polygon, "sectorOffset", -1);
        int faceOffset = JsonValue.GetInt32(polygon, "sourceWadOffset", -1);
        if (faceOffset < 0)
            faceOffset = JsonValue.GetInt32(polygon, "faceOffset", -1);

        return new LowDetailTerrainPolygon(
            points,
            zValues,
            JsonValue.GetInt32(polygon, "sectorIndex", -1),
            JsonValue.GetInt32(polygon, "faceIndex", -1),
            sectorOffset,
            faceOffset,
            ReadIntArray(polygon, "vertexIndexes"),
            ReadIntArray(polygon, "cornerPointIndexes"),
            ReadIntArray(polygon, "colourIndexes"),
            ReadColorArray(polygon, "cornerColors"),
            unchecked((uint)JsonValue.GetInt64(polygon, "rawWord0")),
            unchecked((uint)JsonValue.GetInt64(polygon, "rawWord1")),
            JsonValue.GetInt32(polygon, "transitionBias"),
            JsonValue.GetBoolean(polygon, "doubleSided"),
            JsonValue.GetBoolean(polygon, "semiTransparent"),
            JsonValue.GetInt32(polygon, "blendMode"),
            JsonValue.GetInt32(polygon, "orderingTableBias"));
    }

    private static TerrainPolygon? ReadPolygon(
        JsonElement polygon,
        string candidateDetail,
        bool requiresExactHighPolyMaterialState)
    {
        if (!TryReadPoints(polygon, out List<Vector2f> points, out List<float> zValues))
            return null;

        int sectorOffset = JsonValue.GetInt32(polygon, "sectorOffset", -1);
        int faceOffset = JsonValue.GetInt32(polygon, "sourceWadOffset", -1);
        if (faceOffset < 0)
            faceOffset = JsonValue.GetInt32(polygon, "faceOffset", -1);

        string detail = JsonValue.GetString(polygon, "detail");
        if (string.IsNullOrWhiteSpace(detail))
            detail = candidateDetail;
        NativeHighPolyFacePayload? material = null;
        if (requiresExactHighPolyMaterialState && detail.Equals("hp", StringComparison.OrdinalIgnoreCase))
            material = ReadNativeHighPolyFacePayload(polygon);

        return new TerrainPolygon(
            points,
            zValues,
            JsonValue.GetInt32(polygon, "textureId", -1),
            JsonValue.GetInt32(polygon, "sectorIndex", -1),
            JsonValue.GetInt32(polygon, "faceIndex", -1),
            detail,
            ReadFaceColor(polygon),
            sectorOffset,
            faceOffset,
            ReadIntArray(polygon, "vertexIndexes"),
            JsonValue.GetString(polygon, "word3"),
            JsonValue.GetString(polygon, "word4"),
            JsonValue.GetBoolean(polygon, "flip"),
            JsonValue.GetInt32(polygon, "depth", -1),
            ReadIntArray(polygon, "colourIndexes"),
            ReadColorArray(polygon, "nearColors"),
            ReadColorArray(polygon, "farColors"),
            ReadIntArray(polygon, "cornerPointIndexes"),
            material?.NativeFaceWord2,
            material?.NativeMaterialByte ?? -1,
            material?.NativeUntexturedSentinel,
            material?.NativePrimitiveSemiTransparent,
            material?.NativeTextureId ?? -1,
            material?.NativeFaceWord0,
            material?.NativeFaceWord1,
            material?.NativeFaceWord3);
    }

    private static NativeHighPolyFacePayload ReadNativeHighPolyFacePayload(JsonElement polygon)
    {
        if (!TryReadUInt32(polygon, "rawWord0", out uint nativeWord0) ||
            !TryReadUInt32(polygon, "rawWord1", out uint nativeWord1) ||
            !TryReadUInt32(polygon, "word3", out uint legacyWord2) ||
            !TryReadUInt32(polygon, "nativeFaceWord2", out uint nativeWord2) ||
            legacyWord2 != nativeWord2 ||
            !TryReadUInt32(polygon, "word4", out uint legacyWord3) ||
            !TryReadUInt32(polygon, "nativeFaceWord3", out uint nativeWord3) ||
            legacyWord3 != nativeWord3 ||
            !TryReadByte(polygon, "nativeMaterialByte", out byte materialByte) ||
            !TryReadRequiredBoolean(polygon, "nativeUntexturedSentinel", out bool untexturedSentinel) ||
            !TryReadRequiredBoolean(polygon, "nativePrimitiveSemiTransparent", out bool primitiveSemiTransparent) ||
            !TryReadRequiredInt32(polygon, "nativeTextureId", out int nativeTextureId) ||
            !TryReadRequiredInt32(polygon, "textureId", out int textureId))
        {
            throw new InvalidDataException("HP face is missing a complete raw-word 0-3 and material payload.");
        }

        PsxTerrainPrimitiveClassification expected =
            PsxTerrainBlendKernel.ClassifyHighPolyMaterialByte((byte)(nativeWord2 & 0xFF));
        if (materialByte != expected.RawMaterialByte ||
            untexturedSentinel != expected.IsOpaqueUntexturedSentinel ||
            primitiveSemiTransparent != expected.PrimitiveSemiTransparent ||
            nativeTextureId != expected.TextureId ||
            textureId != expected.TextureId)
        {
            throw new InvalidDataException(
                $"HP face explicit material fields do not match native word +0x08 0x{nativeWord2:X8}.");
        }

        return new NativeHighPolyFacePayload(
            nativeWord0,
            nativeWord1,
            nativeWord2,
            nativeWord3,
            materialByte,
            untexturedSentinel,
            primitiveSemiTransparent,
            nativeTextureId);
    }

    private static bool TryReadUInt32(JsonElement element, string name, out uint value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out JsonElement property))
            return false;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out value))
            return true;
        return property.ValueKind == JsonValueKind.String && TryParseUInt32(property.GetString(), out value);
    }

    private static bool TryParseUInt32(string? text, out uint value)
    {
        string candidate = (text ?? "").Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[2..];
        return uint.TryParse(candidate, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadByte(JsonElement element, string name, out byte value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetByte(out value);
    }

    private static bool TryReadRequiredBoolean(JsonElement element, string name, out bool value)
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

    private static bool TryReadRequiredInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadPoints(JsonElement polygon, out List<Vector2f> points, out List<float> zValues)
    {
        points = [];
        zValues = [];
        if (!polygon.TryGetProperty("points", out JsonElement pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement point in pointsElement.EnumerateArray())
        {
            points.Add(new Vector2f(JsonValue.GetSingle(point, "x"), JsonValue.GetSingle(point, "y")));
            zValues.Add(JsonValue.GetSingle(point, "z"));
        }

        return points.Count >= 3;
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

    private static IReadOnlyList<ColorRgba> ReadColorArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<ColorRgba>();

        List<ColorRgba> result = [];
        foreach (JsonElement color in values.EnumerateArray())
        {
            if (color.ValueKind != JsonValueKind.Object)
                continue;

            result.Add(ColorRgba.FromArgb(
                JsonValue.GetInt32(color, "a", 255),
                JsonValue.GetInt32(color, "r", 255),
                JsonValue.GetInt32(color, "g", 255),
                JsonValue.GetInt32(color, "b", 255)));
        }

        return result;
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

    private sealed record NativeHighPolyFacePayload(
        uint NativeFaceWord0,
        uint NativeFaceWord1,
        uint NativeFaceWord2,
        uint NativeFaceWord3,
        byte NativeMaterialByte,
        bool NativeUntexturedSentinel,
        bool NativePrimitiveSemiTransparent,
        int NativeTextureId);
}
