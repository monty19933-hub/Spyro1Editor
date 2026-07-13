using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

public sealed record SkyboxSceneColorTableCandidate(
    int SectorOffset,
    int SectorSize,
    int ColorTableOffset,
    int ColorWordCount,
    int Command30WordCount,
    double RelativePosition,
    int TailDistanceBytes,
    int LpVertexCount,
    int LpFaceCount,
    int HpVertexCount,
    int HpColorCount,
    int HpFaceCount,
    Rect2f XyBounds,
    float MinZ,
    float MaxZ,
    int VerifiedStoneHillWordCount,
    string FirstColor,
    string LastColor);

public sealed record SkyboxSceneColorThresholdScore(
    double MinimumRelativePosition,
    int CandidateTableCount,
    int SelectedWordCount,
    int VerifiedWordCount,
    int VerifiedSelectedWordCount,
    int FalseNegativeWordCount,
    int UnverifiedSelectedWordCount,
    double Recall,
    double Precision);

public sealed record SkyboxLevelSceneColorResearch(
    string Key,
    string DisplayName,
    int WadEntry,
    long ModelSubfileSize,
    int ParsedSectorCount,
    int CandidateTableCount,
    int CandidateColorWordCount,
    IReadOnlyList<SkyboxSceneColorTableCandidate> CandidateTables,
    IReadOnlyList<SkyboxSceneColorThresholdScore> VerifiedTailScores);

public sealed record SkyboxSceneColorResearchReport(
    DateTimeOffset GeneratedAt,
    string SourceImageFileName,
    int LevelCount,
    int ModelSubfileCoverage,
    IReadOnlyList<SkyboxLevelSceneColorResearch> Levels,
    IReadOnlyList<string> SafetyNotes);

public sealed record SkyboxSceneColorResearchWriteResult(string JsonPath, string MarkdownPath);

public static class SkyboxSceneColorResearchAnalyzer
{
    private const int ModelSubfileIndex = 1;
    private const byte ColorCommand = 0x30;
    private static readonly double[] TailThresholds = [0.50, 0.65, 0.75, 0.80, 0.85, 0.90, 0.92, 0.94];

    public static SkyboxSceneColorResearchReport Analyze(string imagePath, string wadAnalysisPath, LevelCatalog catalog)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Missing source disc image.", imagePath);
        if (!File.Exists(wadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", wadAnalysisPath);

        using FileStream analysisStream = File.OpenRead(wadAnalysisPath);
        using JsonDocument analysis = JsonDocument.Parse(analysisStream);
        int wadLba = JsonValue.GetInt32(analysis.RootElement.GetProperty("wad"), "lba", 37);
        Dictionary<int, WadModelSubfile> modelSubfiles = LoadModelSubfiles(analysis.RootElement);
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        HashSet<int> verifiedStoneHillWords = BuildVerifiedStoneHillLoadedWordOffsets();

        List<SkyboxLevelSceneColorResearch> levels = new();
        foreach (LevelDefinition level in catalog.Levels)
        {
            if (!modelSubfiles.TryGetValue(level.SourceWadEntry, out WadModelSubfile? modelSubfile))
                continue;
            if (modelSubfile.Size > int.MaxValue)
                throw new InvalidOperationException($"{level.DisplayName} model subfile is too large to inspect.");

            byte[] bytes = DiscImage.ReadFileBytes(image, layout, wadLba, modelSubfile.WadOffset, checked((int)modelSubfile.Size));
            IReadOnlyList<SceneSector> sectors = FindSceneSectors(bytes);
            SkyboxSceneColorTableCandidate[] candidates = sectors
                .Select(sector => BuildCandidate(bytes, sector, verifiedStoneHillWords))
                .Where(candidate => candidate != null)
                .Cast<SkyboxSceneColorTableCandidate>()
                .OrderBy(candidate => candidate.ColorTableOffset)
                .ToArray();
            IReadOnlyList<SkyboxSceneColorThresholdScore> tailScores = string.Equals(level.Key, "stonehill", StringComparison.OrdinalIgnoreCase)
                ? BuildTailScores(candidates, verifiedStoneHillWords)
                : Array.Empty<SkyboxSceneColorThresholdScore>();

            levels.Add(new SkyboxLevelSceneColorResearch(
                Key: level.Key,
                DisplayName: level.DisplayName,
                WadEntry: level.SourceWadEntry,
                ModelSubfileSize: modelSubfile.Size,
                ParsedSectorCount: sectors.Count,
                CandidateTableCount: candidates.Length,
                CandidateColorWordCount: candidates.Sum(candidate => candidate.ColorWordCount),
                CandidateTables: candidates,
                VerifiedTailScores: tailScores));
        }

        return new SkyboxSceneColorResearchReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImageFileName: Path.GetFileName(imagePath),
            LevelCount: catalog.Levels.Count,
            ModelSubfileCoverage: levels.Count,
            Levels: levels,
            SafetyNotes:
            [
                "This analyzer is read-only and does not patch BIN/CUE files.",
                "Candidates must parse as native scene sectors with a non-empty low-detail vertex, color, and face table.",
                "Every candidate color word currently carries command byte 0x30; this is still evidence, not an all-level runtime approval.",
                "Tail-position scores are calibrated only against the existing Stone Hill runtime-verified target set."
            ]);
    }

    private static SkyboxSceneColorTableCandidate? BuildCandidate(byte[] bytes, SceneSector sector, IReadOnlySet<int> verifiedWords)
    {
        if (sector.NumLpVertices <= 0 || sector.NumLpColours < 2 || sector.NumLpFaces <= 0)
            return null;

        int dataStart = sector.Offset + 28;
        int colorStart = dataStart + (sector.NumLpVertices * 4);
        int colorEnd = colorStart + (sector.NumLpColours * 4);
        if (colorStart < 0 || colorEnd > sector.Offset + sector.SizeBytes || colorEnd > bytes.Length)
            return null;

        int command30Words = 0;
        for (int offset = colorStart; offset < colorEnd; offset += 4)
        {
            if (bytes[offset + 3] == ColorCommand)
                command30Words++;
        }
        if (command30Words != sector.NumLpColours)
            return null;

        List<Vector3f> vertices = new();
        for (int vertex = 0; vertex < sector.NumLpVertices; vertex++)
            vertices.Add(ConvertSceneVertex(ReadUInt32(bytes, dataStart + (vertex * 4)), sector));
        if (vertices.Count == 0)
            return null;

        Rect2f bounds = Rect2f.FromBounds(
            vertices.Min(vertex => vertex.X),
            vertices.Min(vertex => vertex.Y),
            vertices.Max(vertex => vertex.X),
            vertices.Max(vertex => vertex.Y));
        int verifiedCount = Enumerable.Range(0, sector.NumLpColours)
            .Count(word => verifiedWords.Contains(colorStart + (word * 4)));
        return new SkyboxSceneColorTableCandidate(
            SectorOffset: sector.Offset,
            SectorSize: sector.SizeBytes,
            ColorTableOffset: colorStart,
            ColorWordCount: sector.NumLpColours,
            Command30WordCount: command30Words,
            RelativePosition: Math.Round((double)colorStart / bytes.Length, 6),
            TailDistanceBytes: bytes.Length - colorStart,
            LpVertexCount: sector.NumLpVertices,
            LpFaceCount: sector.NumLpFaces,
            HpVertexCount: sector.NumHpVertices,
            HpColorCount: sector.NumHpColours,
            HpFaceCount: sector.NumHpFaces,
            XyBounds: bounds,
            MinZ: vertices.Min(vertex => vertex.Z),
            MaxZ: vertices.Max(vertex => vertex.Z),
            VerifiedStoneHillWordCount: verifiedCount,
            FirstColor: ToColor(bytes, colorStart),
            LastColor: ToColor(bytes, colorEnd - 4));
    }

    private static IReadOnlyList<SkyboxSceneColorThresholdScore> BuildTailScores(
        IReadOnlyList<SkyboxSceneColorTableCandidate> candidates,
        IReadOnlySet<int> verifiedWords)
    {
        return TailThresholds.Select(threshold =>
        {
            SkyboxSceneColorTableCandidate[] selectedTables = candidates
                .Where(candidate => candidate.RelativePosition >= threshold)
                .ToArray();
            HashSet<int> selectedWords = selectedTables
                .SelectMany(candidate => Enumerable.Range(0, candidate.ColorWordCount)
                    .Select(word => candidate.ColorTableOffset + (word * 4)))
                .ToHashSet();
            int verifiedSelected = verifiedWords.Count(selectedWords.Contains);
            int unverifiedSelected = selectedWords.Count(offset => !verifiedWords.Contains(offset));
            return new SkyboxSceneColorThresholdScore(
                MinimumRelativePosition: threshold,
                CandidateTableCount: selectedTables.Length,
                SelectedWordCount: selectedWords.Count,
                VerifiedWordCount: verifiedWords.Count,
                VerifiedSelectedWordCount: verifiedSelected,
                FalseNegativeWordCount: verifiedWords.Count - verifiedSelected,
                UnverifiedSelectedWordCount: unverifiedSelected,
                Recall: verifiedWords.Count == 0 ? 0 : Math.Round((double)verifiedSelected / verifiedWords.Count, 6),
                Precision: selectedWords.Count == 0 ? 0 : Math.Round((double)verifiedSelected / selectedWords.Count, 6));
        }).ToArray();
    }

    private static IReadOnlyList<SceneSector> FindSceneSectors(byte[] bytes)
    {
        List<SceneSector> result = new();
        for (int offset = 0; offset <= bytes.Length - 28; offset += 4)
        {
            SceneSector? sector = TryReadSceneSector(bytes, offset);
            if (sector != null)
                result.Add(sector);
        }
        return result;
    }

    private static SceneSector? TryReadSceneSector(byte[] bytes, int offset)
    {
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

        SceneSector sector = new(
            Offset: offset,
            CentreRadiusAndFlags: ReadUInt16(bytes, offset + 4),
            XyPos: ReadUInt32(bytes, offset + 8),
            ZPos: ReadUInt32(bytes, offset + 12),
            SizeBytes: sizeBytes,
            NumLpVertices: numLpVertices,
            NumLpColours: numLpColours,
            NumLpFaces: numLpFaces,
            NumHpVertices: numHpVertices,
            NumHpColours: numHpColours,
            NumHpFaces: numHpFaces);
        Vector3f sample = ConvertSceneVertex(ReadUInt32(bytes, offset + 28), sector);
        if (sample.X < 0 || sample.X > 32768 || sample.Y < 0 || sample.Y > 32768 || sample.Z < 0 || sample.Z > 32768)
            return null;
        return sector;
    }

    private static Vector3f ConvertSceneVertex(uint word, SceneSector sector)
    {
        int sectorX = (int)((sector.XyPos >> 16) & 0xFFFF);
        int sectorY = (int)(sector.XyPos & 0xFFFF);
        int sectorZ = (int)((sector.ZPos >> 14) & 0xFFFF);
        sectorZ >>= 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((sector.CentreRadiusAndFlags >> 12) & 1) == 1)
            z >>= 3;
        return new Vector3f(x, y, z);
    }

    private static Dictionary<int, WadModelSubfile> LoadModelSubfiles(JsonElement root)
    {
        Dictionary<int, WadModelSubfile> result = new();
        foreach (JsonElement entry in root.GetProperty("entries").EnumerateArray())
        {
            int entryIndex = JsonValue.GetInt32(entry, "index", -1);
            long entryOffset = JsonValue.GetInt64(entry, "offset", -1);
            if (entryIndex < 0 || entryOffset < 0 ||
                !entry.TryGetProperty("level", out JsonElement level) ||
                level.ValueKind != JsonValueKind.Object ||
                !level.TryGetProperty("subfiles", out JsonElement subfiles) ||
                subfiles.ValueKind != JsonValueKind.Array)
                continue;
            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                if (JsonValue.GetInt32(subfile, "index", -1) != ModelSubfileIndex)
                    continue;
                long relativeOffset = JsonValue.GetInt64(subfile, "offset", -1);
                long size = JsonValue.GetInt64(subfile, "size", -1);
                if (relativeOffset >= 0 && size > 0)
                    result[entryIndex] = new WadModelSubfile(entryOffset + relativeOffset, size);
            }
        }
        return result;
    }

    private static HashSet<int> BuildVerifiedStoneHillLoadedWordOffsets()
    {
        HashSet<int> result = new();
        foreach (SkyboxTargetRecord record in SkyboxTargets.ForPreset("StoneHillNightKeeper"))
        {
            foreach (SkyboxTargetPair pair in record.Pairs().Where(pair => pair.Entry == 12 && pair.Subfile == ModelSubfileIndex))
            {
                for (int word = 0; word < record.WordCount; word++)
                    result.Add(checked((int)(pair.Start + (word * 4L))));
            }
        }
        return result;
    }

    private static string ToColor(byte[] bytes, int offset) =>
        $"#{bytes[offset]:X2}{bytes[offset + 1]:X2}{bytes[offset + 2]:X2}/0x{bytes[offset + 3]:X2}";
    private static ushort ReadUInt16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);
    private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);

    private sealed record WadModelSubfile(long WadOffset, long Size);
    private sealed record SceneSector(
        int Offset,
        int CentreRadiusAndFlags,
        uint XyPos,
        uint ZPos,
        int SizeBytes,
        int NumLpVertices,
        int NumLpColours,
        int NumLpFaces,
        int NumHpVertices,
        int NumHpColours,
        int NumHpFaces);
}

public static class SkyboxSceneColorResearchReportWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static async Task<SkyboxSceneColorResearchWriteResult> WriteAsync(
        SkyboxSceneColorResearchReport report,
        string jsonPath,
        string markdownPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath) ?? ".");
        await using (FileStream output = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }, cancellationToken);
        }
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), Utf8NoBom, cancellationToken);
        return new SkyboxSceneColorResearchWriteResult(jsonPath, markdownPath);
    }

    private static string BuildMarkdown(SkyboxSceneColorResearchReport report)
    {
        StringBuilder text = new();
        text.AppendLine("# Spyro Skybox Scene-Color Research");
        text.AppendLine();
        text.AppendLine($"Coverage: {report.ModelSubfileCoverage}/{report.LevelCount} loaded model subfiles");
        text.AppendLine();
        foreach (string note in report.SafetyNotes)
            text.AppendLine($"- {note}");

        text.AppendLine();
        text.AppendLine("## All Levels");
        text.AppendLine();
        text.AppendLine("| Level | WAD | Parsed sectors | Candidate tables | Candidate words | Tail >= 80% | Tail words |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (SkyboxLevelSceneColorResearch level in report.Levels)
        {
            SkyboxSceneColorTableCandidate[] tail = level.CandidateTables.Where(candidate => candidate.RelativePosition >= 0.80).ToArray();
            text.AppendLine($"| {Escape(level.DisplayName)} | {level.WadEntry} | {level.ParsedSectorCount:N0} | {level.CandidateTableCount:N0} | {level.CandidateColorWordCount:N0} | {tail.Length:N0} | {tail.Sum(candidate => candidate.ColorWordCount):N0} |");
        }

        SkyboxLevelSceneColorResearch? stoneHill = report.Levels.FirstOrDefault(level => level.Key == "stonehill");
        if (stoneHill != null)
        {
            text.AppendLine();
            text.AppendLine("## Stone Hill Tail Scores");
            text.AppendLine();
            text.AppendLine("| Minimum position | Tables | Selected | Verified selected | False negatives | Unverified selected | Recall | Precision |");
            text.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (SkyboxSceneColorThresholdScore score in stoneHill.VerifiedTailScores)
                text.AppendLine($"| {score.MinimumRelativePosition:P0} | {score.CandidateTableCount} | {score.SelectedWordCount} | {score.VerifiedSelectedWordCount}/{score.VerifiedWordCount} | {score.FalseNegativeWordCount} | {score.UnverifiedSelectedWordCount} | {score.Recall:P1} | {score.Precision:P1} |");

            text.AppendLine();
            text.AppendLine("## Stone Hill Candidate Tables");
            text.AppendLine();
            text.AppendLine("| Color offset | Words | Position | Tail bytes | XY bounds | Z | Verified | First | Last |");
            text.AppendLine("| ---: | ---: | ---: | ---: | --- | --- | ---: | --- | --- |");
            foreach (SkyboxSceneColorTableCandidate candidate in stoneHill.CandidateTables)
            {
                string bounds = $"{candidate.XyBounds.Left:0},{candidate.XyBounds.Top:0}..{candidate.XyBounds.Right:0},{candidate.XyBounds.Bottom:0}";
                text.AppendLine($"| 0x{candidate.ColorTableOffset:X} | {candidate.ColorWordCount} | {candidate.RelativePosition:P1} | {candidate.TailDistanceBytes:N0} | {bounds} | {candidate.MinZ:0}..{candidate.MaxZ:0} | {candidate.VerifiedStoneHillWordCount} | {candidate.FirstColor} | {candidate.LastColor} |");
            }
        }

        return text.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
