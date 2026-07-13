using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record SkyboxColorCommandRun(
    int StartOffset,
    int EndOffsetExclusive,
    int WordCount,
    string FirstColor,
    string LastColor);

public sealed record SkyboxColorRunThresholdScore(
    int MinimumRunWords,
    int SelectedWordCount,
    int VerifiedWordCount,
    int VerifiedSelectedWordCount,
    int FalseNegativeWordCount,
    int UnverifiedSelectedWordCount,
    double Recall,
    double Precision);

public sealed record SkyboxLevelColorRunResearch(
    string Key,
    string DisplayName,
    int WadEntry,
    int ModelSubfileIndex,
    long ModelSubfileSize,
    int Command30WordCount,
    int RunCount,
    int LongRunCount,
    int LongRunWordCount,
    IReadOnlyList<SkyboxColorCommandRun> LongRuns,
    IReadOnlyList<SkyboxColorRunThresholdScore> VerifiedThresholdScores);

public sealed record SkyboxColorRunResearchReport(
    DateTimeOffset GeneratedAt,
    string SourceImageFileName,
    int WadLba,
    int LevelCount,
    int ModelSubfileCoverage,
    int LongRunMinimumWords,
    IReadOnlyList<SkyboxLevelColorRunResearch> Levels,
    IReadOnlyList<string> SafetyNotes);

public sealed record SkyboxColorRunResearchWriteResult(string JsonPath, string MarkdownPath);

public static class SkyboxColorRunResearchAnalyzer
{
    private const int ModelSubfileIndex = 1;
    private const byte ColorCommand = 0x30;
    private const int LongRunMinimumWords = 4;
    private static readonly int[] Thresholds = [1, 2, 3, 4, 5, 7, 10, 16, 20];

    public static SkyboxColorRunResearchReport Analyze(string imagePath, string wadAnalysisPath, LevelCatalog catalog)
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

        List<SkyboxLevelColorRunResearch> levels = new();
        foreach (LevelDefinition level in catalog.Levels)
        {
            if (!modelSubfiles.TryGetValue(level.SourceWadEntry, out WadModelSubfile? modelSubfile))
                continue;
            if (modelSubfile.Size > int.MaxValue)
                throw new InvalidOperationException($"{level.DisplayName} model subfile is too large to inspect.");

            byte[] bytes = DiscImage.ReadFileBytes(
                image,
                layout,
                wadLba,
                modelSubfile.WadOffset,
                checked((int)modelSubfile.Size));
            IReadOnlyList<SkyboxColorCommandRun> runs = FindRuns(bytes);
            SkyboxColorCommandRun[] longRuns = runs
                .Where(run => run.WordCount >= LongRunMinimumWords)
                .ToArray();
            IReadOnlyList<SkyboxColorRunThresholdScore> scores = string.Equals(level.Key, "stonehill", StringComparison.OrdinalIgnoreCase)
                ? BuildThresholdScores(runs, verifiedStoneHillWords)
                : Array.Empty<SkyboxColorRunThresholdScore>();

            levels.Add(new SkyboxLevelColorRunResearch(
                Key: level.Key,
                DisplayName: level.DisplayName,
                WadEntry: level.SourceWadEntry,
                ModelSubfileIndex: ModelSubfileIndex,
                ModelSubfileSize: modelSubfile.Size,
                Command30WordCount: runs.Sum(run => run.WordCount),
                RunCount: runs.Count,
                LongRunCount: longRuns.Length,
                LongRunWordCount: longRuns.Sum(run => run.WordCount),
                LongRuns: longRuns,
                VerifiedThresholdScores: scores));
        }

        return new SkyboxColorRunResearchReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImageFileName: Path.GetFileName(imagePath),
            WadLba: wadLba,
            LevelCount: catalog.Levels.Count,
            ModelSubfileCoverage: levels.Count,
            LongRunMinimumWords: LongRunMinimumWords,
            Levels: levels,
            SafetyNotes:
            [
                "This analyzer is read-only and does not patch BIN/CUE files.",
                "A 0x30 command-byte run is a structural color-stream candidate, not automatically a sky primitive.",
                "Only Stone Hill has runtime-verified sky target words in the current evidence set.",
                "An all-level palette exporter must keep unverified selected words out of release writeback until runtime evidence or stronger geometry classification proves them."
            ]);
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

    private static IReadOnlyList<SkyboxColorCommandRun> FindRuns(byte[] bytes)
    {
        List<SkyboxColorCommandRun> runs = new();
        int offset = 0;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset + 3] != ColorCommand)
            {
                offset += 4;
                continue;
            }

            int start = offset;
            while (offset + 4 <= bytes.Length && bytes[offset + 3] == ColorCommand)
                offset += 4;
            int words = (offset - start) / 4;
            runs.Add(new SkyboxColorCommandRun(
                StartOffset: start,
                EndOffsetExclusive: offset,
                WordCount: words,
                FirstColor: ToColor(bytes, start),
                LastColor: ToColor(bytes, offset - 4)));
        }

        return runs;
    }

    private static IReadOnlyList<SkyboxColorRunThresholdScore> BuildThresholdScores(
        IReadOnlyList<SkyboxColorCommandRun> runs,
        IReadOnlySet<int> verifiedWords)
    {
        return Thresholds.Select(minimum =>
        {
            HashSet<int> selected = runs
                .Where(run => run.WordCount >= minimum)
                .SelectMany(run => Enumerable.Range(0, run.WordCount).Select(word => run.StartOffset + (word * 4)))
                .ToHashSet();
            int verifiedSelected = verifiedWords.Count(selected.Contains);
            int unverifiedSelected = selected.Count(offset => !verifiedWords.Contains(offset));
            double recall = verifiedWords.Count == 0 ? 0 : (double)verifiedSelected / verifiedWords.Count;
            double precision = selected.Count == 0 ? 0 : (double)verifiedSelected / selected.Count;
            return new SkyboxColorRunThresholdScore(
                MinimumRunWords: minimum,
                SelectedWordCount: selected.Count,
                VerifiedWordCount: verifiedWords.Count,
                VerifiedSelectedWordCount: verifiedSelected,
                FalseNegativeWordCount: verifiedWords.Count - verifiedSelected,
                UnverifiedSelectedWordCount: unverifiedSelected,
                Recall: Math.Round(recall, 6),
                Precision: Math.Round(precision, 6));
        }).ToArray();
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

    private static string ToColor(byte[] bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length
            ? $"#{bytes[offset]:X2}{bytes[offset + 1]:X2}{bytes[offset + 2]:X2}/0x{bytes[offset + 3]:X2}"
            : "";
    }

    private sealed record WadModelSubfile(long WadOffset, long Size);
}

public static class SkyboxColorRunResearchReportWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static async Task<SkyboxColorRunResearchWriteResult> WriteAsync(
        SkyboxColorRunResearchReport report,
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
        return new SkyboxColorRunResearchWriteResult(jsonPath, markdownPath);
    }

    private static string BuildMarkdown(SkyboxColorRunResearchReport report)
    {
        StringBuilder text = new();
        text.AppendLine("# Spyro Skybox Color-Run Research");
        text.AppendLine();
        text.AppendLine($"Coverage: {report.ModelSubfileCoverage}/{report.LevelCount} loaded model subfiles");
        text.AppendLine($"Long run threshold: {report.LongRunMinimumWords} consecutive 0x30 command words");
        text.AppendLine();
        foreach (string note in report.SafetyNotes)
            text.AppendLine($"- {note}");

        text.AppendLine();
        text.AppendLine("## All Levels");
        text.AppendLine();
        text.AppendLine("| Level | WAD | Subfile bytes | 0x30 words | Runs | Long runs | Long-run words |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (SkyboxLevelColorRunResearch level in report.Levels)
            text.AppendLine($"| {Escape(level.DisplayName)} | {level.WadEntry} | {level.ModelSubfileSize:N0} | {level.Command30WordCount:N0} | {level.RunCount:N0} | {level.LongRunCount:N0} | {level.LongRunWordCount:N0} |");

        SkyboxLevelColorRunResearch? stoneHill = report.Levels.FirstOrDefault(level => string.Equals(level.Key, "stonehill", StringComparison.OrdinalIgnoreCase));
        if (stoneHill != null)
        {
            text.AppendLine();
            text.AppendLine("## Stone Hill Verification Score");
            text.AppendLine();
            text.AppendLine("| Minimum words | Selected | Verified selected | False negatives | Unverified selected | Recall | Precision |");
            text.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (SkyboxColorRunThresholdScore score in stoneHill.VerifiedThresholdScores)
                text.AppendLine($"| {score.MinimumRunWords} | {score.SelectedWordCount:N0} | {score.VerifiedSelectedWordCount:N0}/{score.VerifiedWordCount:N0} | {score.FalseNegativeWordCount:N0} | {score.UnverifiedSelectedWordCount:N0} | {score.Recall:P1} | {score.Precision:P1} |");

            text.AppendLine();
            text.AppendLine("## Stone Hill Long Runs");
            text.AppendLine();
            text.AppendLine("| Start | Words | First | Last |");
            text.AppendLine("| ---: | ---: | --- | --- |");
            foreach (SkyboxColorCommandRun run in stoneHill.LongRuns)
                text.AppendLine($"| 0x{run.StartOffset:X} | {run.WordCount} | {run.FirstColor} | {run.LastColor} |");
        }

        return text.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
