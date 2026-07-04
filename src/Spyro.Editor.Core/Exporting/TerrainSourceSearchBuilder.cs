using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public static class TerrainSourceSearchBuilder
{
    private const int WadLba = 37;
    private const int MaxHitsPerSector = 4;

    public static async Task<TerrainSourceSearchResult> BuildAsync(TerrainSourceSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (!File.Exists(request.RamPath))
            throw new FileNotFoundException("Missing RAM dump for terrain source search.", request.RamPath);

        string outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.Combine(Path.GetDirectoryName(request.RamPath) ?? "", $"{request.Level.Key}-runtime-terrain-source-search-native.json")
            : request.OutputPath;

        TerrainSourceSearchReport report = BuildReport(request.SourceImagePath, request.RamPath, request.Level, request.Geometry);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }), cancellationToken);
        return new TerrainSourceSearchResult(outputPath, report);
    }

    public static async Task<TerrainSourceSearchResult> BuildSourceDerivedAsync(SourceDerivedTerrainSourceSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);

        string outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"{request.Level.Key}-source-derived-terrain-source-search-native.json")
            : request.OutputPath;

        TerrainSourceSearchReport report = BuildSourceDerivedReport(request.SourceImagePath, request.Level, request.Geometry);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }), cancellationToken);
        return new TerrainSourceSearchResult(outputPath, report);
    }

    public static TerrainSourceSearchReport BuildReport(string sourceImagePath, string ramPath, LevelDefinition level, GeometryCandidate geometry)
    {
        byte[] ram = File.ReadAllBytes(ramPath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        byte[] wad = ReadLogicalWad(imageStream, layout);

        List<TerrainSourceSector> sectors = geometry.Polygons
            .Where(face => face.SectorOffset >= 0)
            .GroupBy(face => face.SectorOffset)
            .Select(group => NewSector(ram, group.Key, group.Select(face => face.RuntimeKey).ToArray()))
            .Where(sector => sector != null)
            .Cast<TerrainSourceSector>()
            .ToList();

        Dictionary<ulong, List<TerrainSourceSector>> byPrefix = sectors
            .Where(sector => sector.Bytes.Length >= 8)
            .GroupBy(sector => BinaryPrimitives.ReadUInt64LittleEndian(sector.Bytes.AsSpan(0, 8)))
            .ToDictionary(group => group.Key, group => group.ToList());

        for (int offset = 0; offset <= wad.Length - 8; offset++)
        {
            ulong key = BinaryPrimitives.ReadUInt64LittleEndian(wad.AsSpan(offset, 8));
            if (!byPrefix.TryGetValue(key, out List<TerrainSourceSector>? candidates))
                continue;

            foreach (TerrainSourceSector candidate in candidates)
            {
                if (candidate.Hits.Count >= MaxHitsPerSector || offset + candidate.Bytes.Length > wad.Length)
                    continue;
                if (!wad.AsSpan(offset, candidate.Bytes.Length).SequenceEqual(candidate.Bytes))
                    continue;

                candidate.Hits.Add(new TerrainSourceHit($"0x{offset:X}", $"0x{DiscImageOffset(layout, offset):X}"));
            }
        }

        List<TerrainSourceSearchEntry> results = new();
        foreach (TerrainSourceSector sector in sectors)
        {
            foreach (string runtimeKey in sector.RuntimeKeys)
            {
                results.Add(new TerrainSourceSearchEntry(
                    Edit: runtimeKey,
                    Status: "searched",
                    SectorOffset: $"0x{sector.SectorOffset:X}",
                    SectorSizeBytes: sector.Bytes.Length,
                    FullSectorHits: sector.Hits));
            }
        }

        return new TerrainSourceSearchReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            Purpose: "Native source search for exact runtime scene-sector terrain bytes in the logical Spyro WAD stream.",
            ImagePath: sourceImagePath,
            RamPath: ramPath,
            LevelKey: level.Key,
            WadLba: WadLba,
            SectorCount: sectors.Count,
            MatchedSectorCount: sectors.Count(sector => sector.Hits.Count == 1),
            AmbiguousSectorCount: sectors.Count(sector => sector.Hits.Count > 1),
            MissingSectorCount: sectors.Count(sector => sector.Hits.Count == 0),
            Results: results);
    }

    public static TerrainSourceSearchReport BuildSourceDerivedReport(string sourceImagePath, LevelDefinition level, GeometryCandidate geometry)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        byte[] wad = ReadLogicalWad(imageStream, layout);

        List<TerrainSourceSector> sectors = geometry.Polygons
            .Where(face => face.SectorOffset >= 0)
            .GroupBy(face => face.SectorOffset)
            .Select(group => NewSector(wad, group.Key, group.Select(face => face.RuntimeKey).ToArray()))
            .Where(sector => sector != null)
            .Cast<TerrainSourceSector>()
            .ToList();

        foreach (TerrainSourceSector sector in sectors)
            sector.Hits.Add(new TerrainSourceHit($"0x{sector.SectorOffset:X}", $"0x{DiscImageOffset(layout, sector.SectorOffset):X}"));

        List<TerrainSourceSearchEntry> results = new();
        foreach (TerrainSourceSector sector in sectors)
        {
            foreach (string runtimeKey in sector.RuntimeKeys)
            {
                results.Add(new TerrainSourceSearchEntry(
                    Edit: runtimeKey,
                    Status: "source-derived",
                    SectorOffset: $"0x{sector.SectorOffset:X}",
                    SectorSizeBytes: sector.Bytes.Length,
                    FullSectorHits: sector.Hits));
            }
        }

        return new TerrainSourceSearchReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            Purpose: "Source-derived terrain source map from WAD scene-sector offsets. Used when no RAM capture exists.",
            ImagePath: sourceImagePath,
            RamPath: $"source-wad:{level.Key}",
            LevelKey: level.Key,
            WadLba: WadLba,
            SectorCount: sectors.Count,
            MatchedSectorCount: sectors.Count,
            AmbiguousSectorCount: 0,
            MissingSectorCount: 0,
            Results: results);
    }

    private static TerrainSourceSector? NewSector(byte[] ram, int sectorOffset, IReadOnlyList<string> runtimeKeys)
    {
        SceneSectorInfo? info = ReadSceneSectorHeader(ram, sectorOffset);
        if (info == null)
            return null;

        byte[] bytes = new byte[info.SizeBytes];
        Array.Copy(ram, sectorOffset, bytes, 0, bytes.Length);
        return new TerrainSourceSector(sectorOffset, bytes, runtimeKeys);
    }

    private static SceneSectorInfo? ReadSceneSectorHeader(byte[] ram, int offset)
    {
        if (offset < 0 || offset + 28 > ram.Length)
            return null;

        int numLpVertices = ram[offset + 16];
        int numLpColours = ram[offset + 17];
        int numLpFaces = ram[offset + 18];
        int numHpVertices = ram[offset + 20];
        int numHpColours = ram[offset + 21];
        int numHpFaces = ram[offset + 22];
        int sizeWords = 7 + numLpVertices + numLpColours + (numLpFaces * 2) + numHpVertices + (numHpColours * 2) + (numHpFaces * 4);
        int sizeBytes = sizeWords * 4;
        if (sizeBytes < 28 || sizeBytes > 0x40000 || offset + sizeBytes > ram.Length)
            return null;
        if (numLpVertices + numHpVertices == 0 || numLpFaces + numHpFaces == 0)
            return null;

        return new SceneSectorInfo(sizeBytes);
    }

    private static byte[] ReadLogicalWad(FileStream stream, DiscLayout layout)
    {
        int wadSize = (int)Math.Max(0, Math.Min(110260224L, 2048L * Math.Max(0, (stream.Length / layout.SectorSize) - WadLba)));
        byte[] wad = new byte[wadSize];
        int remaining = wad.Length;
        int written = 0;
        int lba = WadLba;
        while (remaining > 0)
        {
            int toRead = Math.Min(2048, remaining);
            stream.Position = ((long)lba * layout.SectorSize) + layout.UserOffset;
            int read = stream.Read(wad, written, toRead);
            if (read != toRead)
                throw new EndOfStreamException("Could not read logical WAD bytes.");

            written += read;
            remaining -= read;
            lba++;
        }

        return wad;
    }

    private static long DiscImageOffset(DiscLayout layout, long wadOffset)
    {
        long sector = WadLba + (long)Math.Floor(wadOffset / 2048d);
        long sectorOffset = wadOffset % 2048;
        return (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
    }

    private sealed class TerrainSourceSector
    {
        public TerrainSourceSector(int sectorOffset, byte[] bytes, IReadOnlyList<string> runtimeKeys)
        {
            SectorOffset = sectorOffset;
            Bytes = bytes;
            RuntimeKeys = runtimeKeys;
        }

        public int SectorOffset { get; }
        public byte[] Bytes { get; }
        public IReadOnlyList<string> RuntimeKeys { get; }
        public List<TerrainSourceHit> Hits { get; } = new();
    }

    private sealed record SceneSectorInfo(int SizeBytes);
}

public sealed record TerrainSourceSearchRequest(
    string SourceImagePath,
    string OutputPath,
    LevelDefinition Level,
    GeometryCandidate Geometry,
    string RamPath);

public sealed record SourceDerivedTerrainSourceSearchRequest(
    string SourceImagePath,
    string OutputPath,
    LevelDefinition Level,
    GeometryCandidate Geometry);

public sealed record TerrainSourceSearchResult(string OutputPath, TerrainSourceSearchReport Report);

public sealed record TerrainSourceSearchReport(
    DateTimeOffset GeneratedAt,
    string Purpose,
    string ImagePath,
    string RamPath,
    string LevelKey,
    int WadLba,
    int SectorCount,
    int MatchedSectorCount,
    int AmbiguousSectorCount,
    int MissingSectorCount,
    IReadOnlyList<TerrainSourceSearchEntry> Results);

public sealed record TerrainSourceSearchEntry(
    string Edit,
    string Status,
    string SectorOffset,
    int SectorSizeBytes,
    IReadOnlyList<TerrainSourceHit> FullSectorHits);

public sealed record TerrainSourceHit(string WadOffset, string ImageOffset);
