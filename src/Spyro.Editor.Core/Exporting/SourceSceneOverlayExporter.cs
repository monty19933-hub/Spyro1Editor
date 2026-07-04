using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

public sealed record SourceSceneOverlayResult(
    string OutputPath,
    string LevelKey,
    int WadEntry,
    long SceneWadOffset,
    int SectorCount,
    int HpFaces,
    int LpFaces,
    int PolygonCount,
    Rect2f Bounds);

public static class SourceSceneOverlayExporter
{
    private const int DefaultModelSubfileIndex = 1;

    public static async Task<SourceSceneOverlayResult> ExportAsync(
        string sourceImagePath,
        string wadAnalysisPath,
        LevelDefinition level,
        string outputPath,
        int modelSubfileIndex = DefaultModelSubfileIndex,
        int minSectorCount = 16,
        CancellationToken cancellationToken = default)
    {
        SourceOverlay overlay = Build(sourceImagePath, wadAnalysisPath, level, modelSubfileIndex, minSectorCount);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await using FileStream stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, overlay.Root, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);

        SourceOverlayCandidate best = overlay.Candidates[0];
        return new SourceSceneOverlayResult(
            outputPath,
            level.Key,
            level.SourceWadEntry,
            overlay.SceneWadOffset,
            overlay.Chain.Sectors.Count,
            overlay.Chain.HpFaces,
            overlay.Chain.LpFaces,
            best.SampledPolygons,
            best.Bounds);
    }

    public static SourceSceneOverlayResult Export(
        string sourceImagePath,
        string wadAnalysisPath,
        LevelDefinition level,
        string outputPath,
        int modelSubfileIndex = DefaultModelSubfileIndex,
        int minSectorCount = 16)
    {
        return ExportAsync(sourceImagePath, wadAnalysisPath, level, outputPath, modelSubfileIndex, minSectorCount).GetAwaiter().GetResult();
    }

    private static SourceOverlay Build(string sourceImagePath, string wadAnalysisPath, LevelDefinition level, int modelSubfileIndex, int minSectorCount)
    {
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} does not have a source WAD entry.");
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (!File.Exists(wadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", wadAnalysisPath);

        WadSubfileInfo subfile = LoadSubfileInfo(wadAnalysisPath, level.SourceWadEntry, modelSubfileIndex);
        using FileStream image = File.OpenRead(sourceImagePath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        byte[] bytes = DiscImage.ReadFileBytes(image, layout, subfile.WadLba, subfile.AbsoluteWadOffset, checked((int)subfile.Size));
        List<SceneSectorChain> chains = FindSceneSectorChains(bytes, minSectorCount);
        if (chains.Count == 0)
            throw new InvalidOperationException($"Could not find a source scene-sector chain in WAD entry {level.SourceWadEntry} subfile {modelSubfileIndex}.");

        SceneSectorChain chain = chains[0];
        long sceneWadOffset = subfile.AbsoluteWadOffset + chain.StartOffset;
        SourceGeometry hpGeometry = BuildGeometry(bytes, chain, subfile.AbsoluteWadOffset, "hp");
        if (hpGeometry.Points.Count < 16 || hpGeometry.Polygons.Count < 8)
            throw new InvalidOperationException($"Source scene-sector chain for {level.DisplayName} did not produce enough high-detail geometry.");

        SourceOverlayCandidate candidate = BuildCandidate(chain, hpGeometry, sceneWadOffset, "hp", "xy");
        object root = new
        {
            sourceImage = sourceImagePath,
            sourceWadAnalysis = wadAnalysisPath,
            generatedAt = DateTime.Now.ToString("s"),
            note = "Source-derived scene geometry decoded directly from Spyro WAD scene-sector bytes. This is used when a live RAM scene capture is missing or known-bad.",
            sourcePackage = new
            {
                levelKey = level.Key,
                wadEntry = level.SourceWadEntry,
                modelSubfileIndex,
                subfileWadOffset = ToHex(subfile.AbsoluteWadOffset),
                subfileSize = subfile.Size,
                imageOffset = ToHex(DiscImage.ConvertFileOffsetToImageOffset(layout, subfile.WadLba, subfile.AbsoluteWadOffset))
            },
            sceneCandidates = new[]
            {
                new
                {
                    runtimeAddress = $"source-wad:{ToHex(sceneWadOffset)}",
                    source = "source-wad-sector-chain",
                    score = Math.Round((chain.Sectors.Count * 10.0) + (chain.HpFaces / 10.0), 2),
                    size = ToHex(chain.EndOffset - chain.StartOffset),
                    iForget = "source",
                    numSectors = chain.Sectors.Count,
                    validSectors = chain.Sectors.Count,
                    terminatorSectors = chain.TerminatorSectors,
                    hpVertices = chain.HpVertices,
                    hpFaces = chain.HpFaces,
                    lpVertices = chain.LpVertices,
                    lpFaces = chain.LpFaces,
                    firstPointers = chain.Sectors.Take(6).Select(sector => $"source-wad:{ToHex(subfile.AbsoluteWadOffset + sector.Offset)}").ToArray()
                }
            },
            candidateChains = chains.Take(8).Select(candidateChain => new
            {
                startWadOffset = ToHex(subfile.AbsoluteWadOffset + candidateChain.StartOffset),
                sectorCount = candidateChain.Sectors.Count,
                hpFaces = candidateChain.HpFaces,
                lpFaces = candidateChain.LpFaces,
                terminatorSectors = candidateChain.TerminatorSectors
            }).ToArray(),
            candidates = new[]
            {
                candidate.ToSerializable()
            }
        };

        return new SourceOverlay(root, chain, sceneWadOffset, [candidate]);
    }

    private static WadSubfileInfo LoadSubfileInfo(string wadAnalysisPath, int wadEntry, int subfileIndex)
    {
        using FileStream stream = File.OpenRead(wadAnalysisPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        int wadLba = JsonValue.GetInt32(document.RootElement.GetProperty("wad"), "lba", 37);
        foreach (JsonElement entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (JsonValue.GetInt32(entry, "index", -1) != wadEntry)
                continue;

            long entryOffset = JsonValue.GetInt64(entry, "offset", -1);
            if (!entry.TryGetProperty("level", out JsonElement level) || !level.TryGetProperty("subfiles", out JsonElement subfiles))
                break;

            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                if (JsonValue.GetInt32(subfile, "index", -1) == subfileIndex)
                    return new WadSubfileInfo(wadLba, entryOffset, JsonValue.GetInt64(subfile, "offset"), JsonValue.GetInt64(subfile, "size"));
            }
        }

        throw new InvalidOperationException($"WAD entry {wadEntry} subfile {subfileIndex} was not found in {wadAnalysisPath}.");
    }

    private static List<SceneSectorChain> FindSceneSectorChains(byte[] bytes, int minSectorCount)
    {
        Dictionary<int, SourceSceneSector> valid = new();
        for (int offset = 0; offset < bytes.Length - 28; offset += 4)
        {
            SourceSceneSector? sector = TryReadSceneSector(bytes, offset);
            if (sector != null)
                valid[offset] = sector;
        }

        List<SceneSectorChain> chains = new();
        foreach (int start in valid.Keys.Order())
        {
            List<SourceSceneSector> sectors = new();
            int offset = start;
            int guard = 0;
            while (valid.TryGetValue(offset, out SourceSceneSector? sector) && guard < 4096)
            {
                sector = sector with { SectorIndex = sectors.Count };
                sectors.Add(sector);
                offset += sector.SizeBytes;
                guard++;
            }

            if (sectors.Count >= minSectorCount)
                chains.Add(new SceneSectorChain(start, offset, sectors));
        }

        return chains
            .OrderByDescending(chain => chain.Sectors.Count)
            .ThenByDescending(chain => chain.HpFaces)
            .ToList();
    }

    private static SourceSceneSector? TryReadSceneSector(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 28 > bytes.Length)
            return null;

        int numLpVertices = bytes[offset + 16];
        int numLpColours = bytes[offset + 17];
        int numLpFaces = bytes[offset + 18];
        int numHpVertices = bytes[offset + 20];
        int numHpColours = bytes[offset + 21];
        int numHpFaces = bytes[offset + 22];
        int sizeWords = 7 + numLpVertices + numLpColours + (numLpFaces * 2) + numHpVertices + (numHpColours * 2) + (numHpFaces * 4);
        int sizeBytes = sizeWords * 4;
        if (sizeBytes < 28 || sizeBytes > 0x40000 || offset + sizeBytes > bytes.Length)
            return null;
        if (numLpVertices + numHpVertices == 0 || numLpFaces + numHpFaces == 0)
            return null;
        if (numLpVertices + numHpVertices > 512 || numLpFaces + numHpFaces > 512)
            return null;

        SourceSceneSector sector = new(
            offset,
            ReadUInt16(bytes, offset + 4),
            ReadUInt32(bytes, offset + 8),
            ReadUInt32(bytes, offset + 12),
            ReadUInt32(bytes, offset + 24),
            sizeBytes,
            numLpVertices,
            numLpColours,
            numLpFaces,
            numHpVertices,
            numHpColours,
            numHpFaces,
            -1);
        Vector3f sample = ConvertSceneVertex(ReadUInt32(bytes, offset + 28), sector);
        if (sample.X < 0 || sample.X > 32768 || sample.Y < 0 || sample.Y > 32768 || sample.Z < 0 || sample.Z > 32768)
            return null;

        return sector;
    }

    private static SourceGeometry BuildGeometry(byte[] bytes, SceneSectorChain chain, long subfileWadOffset, string detail)
    {
        List<Vector3f> points = new();
        List<SourceEdge> edges = new();
        List<SourcePolygon> polygons = new();
        HashSet<string> edgeKeys = new(StringComparer.Ordinal);
        foreach (SourceSceneSector sector in chain.Sectors)
            AddSectorGeometry(bytes, sector, subfileWadOffset + sector.Offset, detail, points, edges, polygons, edgeKeys);

        return new SourceGeometry(points, edges, polygons);
    }

    private static void AddSectorGeometry(byte[] bytes, SourceSceneSector sector, long sourceWadOffset, string detail, List<Vector3f> points, List<SourceEdge> edges, List<SourcePolygon> polygons, HashSet<string> edgeKeys)
    {
        int dataStart = sector.Offset + 28;
        List<FaceSet> sets = new();
        if (detail.Equals("lp", StringComparison.OrdinalIgnoreCase) || detail.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            sets.Add(new FaceSet(0, sector.NumLpVertices, sector.NumLpVertices, 4, sector.NumLpVertices + sector.NumLpColours, sector.NumLpFaces, 2, "lp"));
        }
        if (detail.Equals("hp", StringComparison.OrdinalIgnoreCase) || detail.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
            int hpColourStartWords = hpVertexStartWords + sector.NumHpVertices;
            int hpFaceStartWords = hpColourStartWords + (sector.NumHpColours * 2);
            sets.Add(new FaceSet(hpVertexStartWords, sector.NumHpVertices, hpColourStartWords, 8, hpFaceStartWords, sector.NumHpFaces, 4, "hp"));
        }

        foreach (FaceSet set in sets)
        {
            if (set.VertexCount <= 0 || set.FaceCount <= 0)
                continue;

            List<Vector3f> vertices = new();
            for (int i = 0; i < set.VertexCount; i++)
            {
                Vector3f vertex = ConvertSceneVertex(ReadUInt32(bytes, dataStart + ((set.VertexStartWords + i) * 4)), sector);
                vertices.Add(vertex);
                points.Add(vertex);
            }

            for (int face = 0; face < set.FaceCount; face++)
            {
                int faceOffset = dataStart + ((set.FaceStartWords + (face * set.FaceWords)) * 4);
                if (faceOffset + 4 > bytes.Length)
                    continue;

                int[] indexes = [bytes[faceOffset], bytes[faceOffset + 1], bytes[faceOffset + 2], bytes[faceOffset + 3]];
                List<Vector3f?> faceVertices = indexes
                    .Select(index => index >= 0 && index < vertices.Count ? (Vector3f?)vertices[index] : null)
                    .ToList();
                List<Vector3f> ordered = new();
                HashSet<int> seen = new();
                foreach (int index in indexes)
                {
                    if (index < 0 || index >= vertices.Count)
                        continue;
                    if (seen.Add(index))
                        ordered.Add(vertices[index]);
                }
                if (ordered.Count < 3)
                    continue;

                int textureId = -1;
                bool flip = false;
                int depth = -1;
                string word3 = "";
                string word4 = "";
                int[] colourIndexes = [];
                ColorRgba faceColor = ColorRgba.FromRgb(96, 128, 96);
                if (set.Detail == "hp" && faceOffset + 16 <= bytes.Length)
                {
                    uint rawWord3 = ReadUInt32(bytes, faceOffset + 8);
                    uint rawWord4 = ReadUInt32(bytes, faceOffset + 12);
                    textureId = (int)(rawWord3 & 0x7F);
                    flip = ((rawWord4 >> 1) & 1) != 0;
                    depth = (int)((rawWord4 >> 3) & 0x1F);
                    word3 = ToHex(rawWord3, 8);
                    word4 = ToHex(rawWord4, 8);
                    colourIndexes = [bytes[faceOffset + 4], bytes[faceOffset + 5], bytes[faceOffset + 6], bytes[faceOffset + 7]];
                    List<ColorRgba> colors = new();
                    int colourStart = dataStart + (set.ColourStartWords * 4);
                    foreach (int colourIndex in colourIndexes)
                        colors.Add(ReadRawRgbColor(bytes, colourStart + (colourIndex * set.ColourEntryBytes) + 4));
                    faceColor = AverageColor(colors);
                }

                AddEdges(vertices, indexes, edges, edgeKeys);
                float minZ = ordered.Min(point => point.Z);
                float maxZ = ordered.Max(point => point.Z);
                float avgZ = ordered.Sum(point => point.Z) / ordered.Count;
                polygons.Add(new SourcePolygon(
                    ordered,
                    faceVertices,
                    avgZ,
                    minZ,
                    maxZ,
                    sourceWadOffset,
                    set.Detail,
                    sector.SectorIndex,
                    face,
                    faceOffset,
                    sourceWadOffset + (faceOffset - sector.Offset),
                    indexes,
                    textureId,
                    flip,
                    depth,
                    word3,
                    word4,
                    colourIndexes,
                    faceColor));
            }
        }
    }

    private static void AddEdges(List<Vector3f> vertices, int[] indexes, List<SourceEdge> edges, HashSet<string> edgeKeys)
    {
        List<int> ordered = new();
        foreach (int index in indexes)
        {
            if (index < 0 || index >= vertices.Count || ordered.Contains(index))
                continue;
            ordered.Add(index);
        }
        if (ordered.Count < 3)
            return;

        for (int i = 0; i < ordered.Count; i++)
        {
            Vector3f a = vertices[ordered[i]];
            Vector3f b = vertices[ordered[(i + 1) % ordered.Count]];
            string aKey = $"{(int)a.X},{(int)a.Y},{(int)a.Z}";
            string bKey = $"{(int)b.X},{(int)b.Y},{(int)b.Z}";
            string edgeKey = string.CompareOrdinal(aKey, bKey) <= 0 ? $"{aKey}|{bKey}" : $"{bKey}|{aKey}";
            if (edgeKeys.Add(edgeKey))
                edges.Add(new SourceEdge(a, b));
        }
    }

    private static SourceOverlayCandidate BuildCandidate(SceneSectorChain chain, SourceGeometry geometry, long sceneWadOffset, string detail, string projection)
    {
        List<ProjectedPoint> points = geometry.Points.Select(point => ProjectPoint(point, projection)).ToList();
        List<object> edges = geometry.Edges.Select(edge =>
        {
            ProjectedPoint a = ProjectPoint(edge.A, projection);
            ProjectedPoint b = ProjectPoint(edge.B, projection);
            return new { x1 = a.x, y1 = a.y, x2 = b.x, y2 = b.y };
        }).Cast<object>().ToList();
        List<object> polygons = geometry.Polygons.Select(polygon => ProjectPolygon(polygon, projection)).Cast<object>().ToList();
        Rect2f bounds = ComputeBounds(points);
        float minZ = geometry.Points.Min(point => point.Z);
        float maxZ = geometry.Points.Max(point => point.Z);
        return new SourceOverlayCandidate(
            $"source-wad {ToHex(sceneWadOffset)} {detail} {projection}",
            $"source-wad:{ToHex(sceneWadOffset)}",
            ToHex(sceneWadOffset),
            detail,
            $"runtime-scene-{projection}",
            chain.Sectors.Count,
            geometry.Points.Count,
            geometry.Edges.Count,
            geometry.Polygons.Count,
            bounds,
            minZ,
            maxZ,
            polygons,
            points.Cast<object>().ToList(),
            edges);
    }

    private static object ProjectPolygon(SourcePolygon polygon, string projection)
    {
        ProjectedPoint[] points = polygon.Vertices.Select(point => ProjectPoint(point, projection)).ToArray();
        Dictionary<string, object> result = new()
        {
            ["points"] = points,
            ["avgZ"] = polygon.AvgZ,
            ["minZ"] = polygon.MinZ,
            ["maxZ"] = polygon.MaxZ,
            ["sectorOffset"] = ToHex(polygon.SectorWadOffset),
            ["detail"] = polygon.Detail,
            ["sectorIndex"] = polygon.SectorIndex,
            ["faceIndex"] = polygon.FaceIndex,
            ["faceOffset"] = ToHex(polygon.FaceOffset),
            ["sourceWadOffset"] = ToHex(polygon.SourceWadOffset),
            ["vertexIndexes"] = polygon.VertexIndexes,
            ["textureId"] = polygon.TextureId,
            ["flip"] = polygon.Flip,
            ["depth"] = polygon.Depth,
            ["word3"] = polygon.Word3,
            ["word4"] = polygon.Word4,
            ["colourIndexes"] = polygon.ColourIndexes,
            ["faceColor"] = new { r = polygon.FaceColor.R, g = polygon.FaceColor.G, b = polygon.FaceColor.B, a = polygon.FaceColor.A }
        };
        if (polygon.FaceVertices.Count >= 4 && polygon.FaceVertices.Take(4).All(point => point.HasValue))
        {
            result["textureCorners"] = new
            {
                topLeft = ProjectPoint(polygon.FaceVertices[3]!.Value, projection),
                topRight = ProjectPoint(polygon.FaceVertices[2]!.Value, projection),
                bottomLeft = ProjectPoint(polygon.FaceVertices[0]!.Value, projection),
                bottomRight = ProjectPoint(polygon.FaceVertices[1]!.Value, projection)
            };
        }

        return result;
    }

    private static ProjectedPoint ProjectPoint(Vector3f point, string projection)
    {
        return projection switch
        {
            "yx" => new ProjectedPoint(point.Y, point.X, point.Z),
            "xz" => new ProjectedPoint(point.X, point.Z, point.Y),
            "zx" => new ProjectedPoint(point.Z, point.X, point.Y),
            "yz" => new ProjectedPoint(point.Y, point.Z, point.X),
            "zy" => new ProjectedPoint(point.Z, point.Y, point.X),
            _ => new ProjectedPoint(point.X, point.Y, point.Z)
        };
    }

    private static Rect2f ComputeBounds(IEnumerable<ProjectedPoint> points)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        int count = 0;
        foreach (ProjectedPoint point in points)
        {
            minX = Math.Min(minX, point.x);
            minY = Math.Min(minY, point.y);
            maxX = Math.Max(maxX, point.x);
            maxY = Math.Max(maxY, point.y);
            count++;
        }

        return count == 0 ? Rect2f.Empty : Rect2f.FromBounds(minX, minY, maxX, maxY);
    }

    private static Vector3f ConvertSceneVertex(uint word, SourceSceneSector sector)
    {
        uint xyPos = sector.XyPos;
        uint zPos = sector.ZPos;
        int sectorX = (int)((xyPos >> 16) & 0xFFFF);
        int sectorY = (int)(xyPos & 0xFFFF);
        int sectorZ = (int)((zPos >> 14) & 0xFFFF);
        sectorZ >>= 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((sector.CentreRadiusAndFlags >> 12) & 1) == 1)
            z >>= 3;
        return new Vector3f(x, y, z);
    }

    private static ColorRgba ReadRawRgbColor(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 3 > bytes.Length)
            return ColorRgba.FromRgb(96, 128, 96);
        return ColorRgba.FromRgb(bytes[offset], bytes[offset + 1], bytes[offset + 2]);
    }

    private static ColorRgba AverageColor(IReadOnlyList<ColorRgba> colors)
    {
        if (colors.Count == 0)
            return ColorRgba.FromRgb(96, 128, 96);
        int r = colors.Sum(color => color.R) / colors.Count;
        int g = colors.Sum(color => color.G) / colors.Count;
        int b = colors.Sum(color => color.B) / colors.Count;
        return ColorRgba.FromRgb(r, g, b);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);

    private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);

    private static string ToHex(long value) => $"0x{value:X}";

    private static string ToHex(uint value, int width) => $"0x{value.ToString($"X{width}")}";

    private sealed record WadSubfileInfo(int WadLba, long EntryOffset, long SubfileOffset, long Size)
    {
        public long AbsoluteWadOffset => EntryOffset + SubfileOffset;
    }

    private sealed record SourceSceneSector(
        int Offset,
        int CentreRadiusAndFlags,
        uint XyPos,
        uint ZPos,
        uint ZTerminator,
        int SizeBytes,
        int NumLpVertices,
        int NumLpColours,
        int NumLpFaces,
        int NumHpVertices,
        int NumHpColours,
        int NumHpFaces,
        int SectorIndex);

    private sealed record SceneSectorChain(int StartOffset, int EndOffset, IReadOnlyList<SourceSceneSector> Sectors)
    {
        public int HpFaces => Sectors.Sum(sector => sector.NumHpFaces);
        public int LpFaces => Sectors.Sum(sector => sector.NumLpFaces);
        public int HpVertices => Sectors.Sum(sector => sector.NumHpVertices);
        public int LpVertices => Sectors.Sum(sector => sector.NumLpVertices);
        public int TerminatorSectors => Sectors.Count(sector => sector.ZTerminator == 0xFFFFFFFFu);
    }

    private sealed record FaceSet(int VertexStartWords, int VertexCount, int ColourStartWords, int ColourEntryBytes, int FaceStartWords, int FaceCount, int FaceWords, string Detail);

    private sealed record SourceGeometry(IReadOnlyList<Vector3f> Points, IReadOnlyList<SourceEdge> Edges, IReadOnlyList<SourcePolygon> Polygons);

    private sealed record SourceEdge(Vector3f A, Vector3f B);

    private sealed record SourcePolygon(
        IReadOnlyList<Vector3f> Vertices,
        IReadOnlyList<Vector3f?> FaceVertices,
        float AvgZ,
        float MinZ,
        float MaxZ,
        long SectorWadOffset,
        string Detail,
        int SectorIndex,
        int FaceIndex,
        int FaceOffset,
        long SourceWadOffset,
        IReadOnlyList<int> VertexIndexes,
        int TextureId,
        bool Flip,
        int Depth,
        string Word3,
        string Word4,
        IReadOnlyList<int> ColourIndexes,
        ColorRgba FaceColor);

    private readonly record struct ProjectedPoint(float x, float y, float z);

    private sealed record SourceOverlay(object Root, SceneSectorChain Chain, long SceneWadOffset, IReadOnlyList<SourceOverlayCandidate> Candidates);

    private sealed record SourceOverlayCandidate(
        string RuntimeAddress,
        string SceneRuntimeAddress,
        string SceneOffset,
        string Detail,
        string Projection,
        int SectorCount,
        int ValidVertices,
        int ValidEdges,
        int SampledPolygons,
        Rect2f Bounds,
        float MinZ,
        float MaxZ,
        IReadOnlyList<object> Polygons,
        IReadOnlyList<object> ProjectedPoints,
        IReadOnlyList<object> Edges)
    {
        public object ToSerializable()
        {
            return new
            {
                runtimeAddress = RuntimeAddress,
                source = "source-wad-sector-chain",
                sceneRuntimeAddress = SceneRuntimeAddress,
                sceneOffset = SceneOffset,
                encoding = "spyroedit-source-scene-sector",
                detail = Detail,
                projection = Projection,
                editorPlane = Projection == "runtime-scene-xy" ? "runtime-moby-xy" : "diagnostic-axis-probe",
                fitScore = 0.0,
                mobysInsideProjection = 0,
                edgeSource = "source-scene-sector-faces",
                sectorCount = SectorCount,
                validSectors = SectorCount,
                validVertices = ValidVertices,
                validEdges = ValidEdges,
                validPolygons = SampledPolygons,
                sampledVertices = ProjectedPoints.Count,
                sampledEdges = Edges.Count,
                sampledPolygons = Polygons.Count,
                totalEdges = Edges.Count,
                totalPolygons = Polygons.Count,
                projectedBounds = new { minX = Bounds.Left, maxX = Bounds.Right, minY = Bounds.Top, maxY = Bounds.Bottom },
                heightBounds = new { minZ = MinZ, maxZ = MaxZ },
                runtimeMobyBounds = (object?)null,
                polygons = Polygons,
                projectedPoints = ProjectedPoints,
                edges = Edges
            };
        }
    }
}
