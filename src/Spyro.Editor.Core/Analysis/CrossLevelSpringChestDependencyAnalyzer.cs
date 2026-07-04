using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelSpringChestDependencyAnalyzer
{
    private const int WadLba = 37;
    private const int RecordStride = 0x58;
    private const int TypeOffset = 0x50;

    public static CrossLevelSpringChestDependencyReport Build(EditorWorkspace workspace, LevelCatalog catalog, string sourceImagePath)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);

        List<SpringChestLevelDependencySummary> levels = new();
        foreach (string levelKey in new[] { "townsquare", "peacekeepers", "drycanyon", "stonehill" })
        {
            LevelDefinition? level = catalog.FindByKey(levelKey);
            if (level?.HasSourceTable != true)
                continue;

            levels.Add(ReadLevel(workspace, stream, layout, level));
        }

        SpringChestLevelDependencySummary? stoneHill = levels.FirstOrDefault(level => string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase));
        string conclusion = stoneHill == null
            ? "Stone Hill was not available for dependency comparison."
            : stoneHill.SpringChestRecordCount == 0 && stoneHill.ActorId0149RecordCount == 0
            ? "Stone Hill has no native spring chest source records or 0x0149 actor records; the first import must carry both actor package code and any special-data/dependency cluster."
            : "Stone Hill already has some spring chest signatures; inspect collisions before enabling imports.";

        return new CrossLevelSpringChestDependencyReport(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: sourceImagePath,
            Levels: levels,
            Conclusion: conclusion);
    }

    public static async Task WriteAsync(CrossLevelSpringChestDependencyReport report, string jsonPath, string markdownPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), cancellationToken);
    }

    private static SpringChestLevelDependencySummary ReadLevel(EditorWorkspace workspace, FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        long tableRelativeOffset = ParseNumber(level.SourceTableRelativeOffset);
        long entryBase = tableWadOffset - tableRelativeOffset;
        SpecialLayout specialLayout = BuildSpecialLayout(stream, layout, tableWadOffset, tableRelativeOffset, level.SourceRecordCount);
        Dictionary<int, RuntimeMobySummary> runtimeByTrueIndex = LoadRuntimeMobys(workspace, level.Key);
        IReadOnlyList<SpringChestActorRootDependency> actorRoots = ReadActorRoots(stream, layout, entryBase);

        List<SpringChestRecordDependency> springRecords = new();
        Dictionary<string, int> dependencyActorCounts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["0x00C2"] = 0,
            ["0x0149"] = 0,
            ["0x0186"] = 0,
            ["0x01FE"] = 0
        };
        int actor0149Records = 0;
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            int actorId = record[0x36] | (record[0x37] << 8);
            string actorIdHex = HexWord(actorId);
            if (dependencyActorCounts.ContainsKey(actorIdHex))
                dependencyActorCounts[actorIdHex]++;
            if (actorId == 0x0149)
                actor0149Records++;
            if (actorId != 0x0149)
                continue;

            uint specialOffset0 = BitConverter.ToUInt32(record, 0);
            uint specialOffset8 = BitConverter.ToUInt32(record, 8);
            uint specialOffset = IsLevelSpecialDataOffset(tableRelativeOffset, specialOffset0)
                ? specialOffset0
                : IsLevelSpecialDataOffset(tableRelativeOffset, specialOffset8)
                    ? specialOffset8
                    : 0;
            int specialLength = specialOffset == 0 ? 0 : GetSpecialDataLength(specialLayout, specialOffset, record);
            byte[] specialBytes = specialOffset == 0 ? [] : DiscImage.ReadFileBytes(stream, layout, WadLba, specialLayout.WadBaseOffset + specialOffset, specialLength);
            RuntimeMobySummary runtime = runtimeByTrueIndex.TryGetValue(trueIndex, out RuntimeMobySummary? cached)
                ? cached
                : RuntimeMobySummary.Empty;

            springRecords.Add(new SpringChestRecordDependency(
                TrueIndex: trueIndex,
                TypeHex: HexByte(record[TypeOffset]),
                ActorIdHex: HexWord(actorId),
                StateHex: HexByte(record[0x51]),
                RewardByteHex: HexByte(record[0x53]),
                SourceSpecialOffsetHex: specialOffset == 0 ? "" : HexOffset(specialOffset),
                SourceSpecialLength: specialLength,
                SourceSpecialSha1: specialBytes.Length == 0 ? "" : ShortSha1(specialBytes),
                SourceSpecialWords: FormatWords(specialBytes),
                RuntimeSpecialDataPointer: runtime.SpecialDataPointer,
                RuntimePosition: runtime.Position,
                RuntimeLabel: runtime.Label));
        }

        IReadOnlyList<string> distinctSpecialHashes = springRecords
            .Select(record => record.SourceSpecialSha1)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<string> distinctRuntimePointers = springRecords
            .Select(record => record.RuntimeSpecialDataPointer)
            .Where(pointer => !string.IsNullOrWhiteSpace(pointer) && !string.Equals(pointer, "0x00000000", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SpringChestLevelDependencySummary(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            SourceRecordCount: level.SourceRecordCount,
            ActorId0149RecordCount: actor0149Records,
            SpringChestRecordCount: springRecords.Count,
            DependencyActorCounts: dependencyActorCounts,
            ActorRoots: actorRoots,
            DistinctSourceSpecialDataHashes: distinctSpecialHashes,
            DistinctRuntimeSpecialDataPointers: distinctRuntimePointers,
            Records: springRecords);
    }

    private static IReadOnlyList<SpringChestActorRootDependency> ReadActorRoots(FileStream stream, DiscLayout layout, long entryBase)
    {
        List<SpringChestActorRootDependency> roots = new();
        List<(int Index, int Slot, uint Root, ushort ActorId)> raw = new();
        for (int index = 0; index < 64; index++)
        {
            int slot = 0x50 + (index * 4);
            uint root = BitConverter.ToUInt32(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + slot, 4), 0);
            ushort actorId = BitConverter.ToUInt16(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + 0x150 + (index * 2), 2), 0);
            if (root == 0 && actorId == 0)
                continue;
            raw.Add((index, slot, root, actorId));
        }

        List<uint> sortedRoots = raw
            .Where(entry => entry.Root > 0)
            .Select(entry => entry.Root)
            .Distinct()
            .Order()
            .ToList();

        foreach ((int index, int slot, uint root, ushort actorId) in raw)
        {
            uint nextRoot = 0;
            if (root > 0)
                nextRoot = sortedRoots.FirstOrDefault(candidate => candidate > root);
            int approxLength = nextRoot > root ? checked((int)(nextRoot - root)) : 0;
            roots.Add(new SpringChestActorRootDependency(
                RootIndex: index,
                RootSlotHex: HexOffset((uint)slot),
                RootOffsetHex: root == 0 ? "0x00000000" : HexOffset(root),
                ActorIdHex: HexWord(actorId),
                ApproxLengthToNextRoot: approxLength,
                IsSpringChestDependency: actorId is 0x00C2 or 0x0149 or 0x0186 or 0x01FE));
        }

        return roots;
    }

    private static SpecialLayout BuildSpecialLayout(FileStream stream, DiscLayout layout, long tableWadOffset, long tableRelativeOffset, int sourceRecordCount)
    {
        List<SpecialEntry> entries = new();
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            uint offset = BitConverter.ToUInt32(record, 0);
            if (IsLevelSpecialDataOffset(tableRelativeOffset, offset))
                entries.Add(new SpecialEntry(offset, record[TypeOffset]));
        }

        List<SpecialEntry> unique = entries
            .GroupBy(entry => entry.Offset)
            .Select(group => group.First())
            .OrderBy(entry => entry.Offset)
            .ToList();
        Dictionary<uint, int> lengths = new();
        for (int i = 0; i < unique.Count; i++)
        {
            SpecialEntry entry = unique[i];
            int length = 0;
            if (i + 1 < unique.Count)
            {
                uint delta = unique[i + 1].Offset - entry.Offset;
                if (delta > 0 && delta <= 0x400)
                    length = (int)delta;
            }

            lengths[entry.Offset] = length <= 0 ? GetDefaultSpecialDataLength(entry.Type) : length;
        }

        return new SpecialLayout(tableWadOffset - tableRelativeOffset, lengths);
    }

    private static int GetSpecialDataLength(SpecialLayout layout, uint sourceOffset, byte[] recordBytes)
    {
        return layout.Lengths.TryGetValue(sourceOffset, out int length)
            ? length
            : GetDefaultSpecialDataLength(recordBytes[TypeOffset]);
    }

    private static int GetDefaultSpecialDataLength(int type) => type == 0x00 ? 0x28 : 0x18;

    private static Dictionary<int, RuntimeMobySummary> LoadRuntimeMobys(EditorWorkspace workspace, string levelKey)
    {
        string path = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(path))
            return [];

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("mobys", out JsonElement mobysElement) || mobysElement.ValueKind != JsonValueKind.Array)
            return [];

        Dictionary<int, RuntimeMobySummary> result = new();
        foreach (JsonElement moby in mobysElement.EnumerateArray())
        {
            int trueIndex = JsonValue.GetInt32(moby, "trueIndex", -1);
            if (trueIndex < 0)
                continue;

            result[trueIndex] = new RuntimeMobySummary(
                SpecialDataPointer: NormalizeHex32(JsonValue.GetString(moby, "specialDataPointer", "0x00000000")),
                Position: $"{JsonValue.GetSingle(moby, "x"):0.##},{JsonValue.GetSingle(moby, "y"):0.##},{JsonValue.GetSingle(moby, "z"):0.##}",
                Label: JsonValue.GetString(moby, "label"));
        }

        return result;
    }

    private static string BuildMarkdown(CrossLevelSpringChestDependencyReport report)
    {
        StringBuilder markdown = new();
        markdown.AppendLine("# Spring Chest dependency audit");
        markdown.AppendLine();
        markdown.AppendLine(report.Conclusion);
        markdown.AppendLine();
        markdown.AppendLine("| Level | 0x0149 records | Spring records | Source special hashes | Runtime special pointers |");
        markdown.AppendLine("|---|---:|---:|---|---|");
        foreach (SpringChestLevelDependencySummary level in report.Levels)
        {
            markdown.AppendLine($"| {level.LevelName} | {level.ActorId0149RecordCount} | {level.SpringChestRecordCount} | {Join(level.DistinctSourceSpecialDataHashes)} | {Join(level.DistinctRuntimeSpecialDataPointers)} |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Package actor IDs");
        markdown.AppendLine();
        markdown.AppendLine("These are the actor IDs involved in the current Spring Chest import recipes. Stone Hill has no native `0x0149` Spring Chest actor, but it does already have `0x00C2`, so replacing or aliasing the wrong helper/root can disturb existing chest behavior.");
        markdown.AppendLine();
        markdown.AppendLine("| Level | 0x00C2 | 0x0149 | 0x0186 | 0x01FE |");
        markdown.AppendLine("|---|---:|---:|---:|---:|");
        foreach (SpringChestLevelDependencySummary level in report.Levels)
        {
            markdown.AppendLine($"| {level.LevelName} | {ActorCount(level, "0x00C2")} | {ActorCount(level, "0x0149")} | {ActorCount(level, "0x0186")} | {ActorCount(level, "0x01FE")} |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Actor-root registrations");
        markdown.AppendLine();
        markdown.AppendLine("These are the loader root-table entries for the actor IDs involved in the Spring Chest recipes. A level can have no visible moby records for an actor while still carrying a root package, so this is the more direct load-time comparison.");
        markdown.AppendLine();
        markdown.AppendLine("| Level | Nonzero roots | 0x00C2 roots | 0x0149 roots | 0x0186 roots | 0x01FE roots |");
        markdown.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (SpringChestLevelDependencySummary level in report.Levels)
        {
            markdown.AppendLine($"| {level.LevelName} | {level.ActorRoots.Count} | {RootCount(level, "0x00C2")} | {RootCount(level, "0x0149")} | {RootCount(level, "0x0186")} | {RootCount(level, "0x01FE")} |");
        }

        markdown.AppendLine();
        markdown.AppendLine("| Level | Slot | Index | Root | Actor | Approx bytes to next root |");
        markdown.AppendLine("|---|---:|---:|---|---|---:|");
        foreach (SpringChestLevelDependencySummary level in report.Levels)
        {
            foreach (SpringChestActorRootDependency root in level.ActorRoots.Where(root => root.IsSpringChestDependency))
            {
                markdown.AppendLine($"| {level.LevelName} | `{root.RootSlotHex}` | {root.RootIndex} | `{root.RootOffsetHex}` | `{root.ActorIdHex}` | {root.ApproxLengthToNextRoot} |");
            }
        }

        markdown.AppendLine();
        if (report.Levels
            .Where(level => !string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
            .All(level => RootCount(level, "0x01FE") == 0))
        {
            markdown.AppendLine("Audit note: no native spring-chest source level registers a `0x01FE` actor root. The frozen Stone Hill recipe used `0x01FE` as a private alias for the copied `0x00C2` package, so that alias should stay blocked until a native-shaped replacement or a verified dependency rebase is tested.");
            markdown.AppendLine();
        }

        SpringChestLevelDependencySummary? stoneHill = report.Levels.FirstOrDefault(level => string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase));
        if (stoneHill != null && ActorCount(stoneHill, "0x00C2") > 0 && ActorCount(stoneHill, "0x0149") == 0)
        {
            markdown.AppendLine($"Stone Hill risk note: {ActorCount(stoneHill, "0x00C2")} native Stone Hill source record(s) already use actor `0x00C2`, while none use Spring Chest actor `0x0149`. New Spring Chest candidates should preserve Stone Hill's native `0x00C2` package unless a dependency rebase proves replacing it is safe.");
            markdown.AppendLine();
        }

        foreach (SpringChestLevelDependencySummary level in report.Levels)
        {
            markdown.AppendLine($"## {level.LevelName}");
            markdown.AppendLine();
            if (level.Records.Count == 0)
            {
                markdown.AppendLine("No native spring chest source records were found.");
                markdown.AppendLine();
                continue;
            }

            markdown.AppendLine("| T | Type | Actor | Reward | Source special | Len | Hash | Runtime special | Runtime XYZ | Words |");
            markdown.AppendLine("|---:|---|---|---|---|---:|---|---|---|---|");
            foreach (SpringChestRecordDependency record in level.Records)
            {
                markdown.AppendLine($"| {record.TrueIndex} | `{record.TypeHex}` | `{record.ActorIdHex}` | `{record.RewardByteHex}` | `{record.SourceSpecialOffsetHex}` | {record.SourceSpecialLength} | `{record.SourceSpecialSha1}` | `{record.RuntimeSpecialDataPointer}` | {record.RuntimePosition} | `{record.SourceSpecialWords}` |");
            }

            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private static string Join(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return string.IsNullOrWhiteSpace(joined) ? "-" : joined;
    }

    private static int ActorCount(SpringChestLevelDependencySummary level, string actorId)
    {
        return level.DependencyActorCounts.TryGetValue(actorId, out int count) ? count : 0;
    }

    private static int RootCount(SpringChestLevelDependencySummary level, string actorId)
    {
        return level.ActorRoots.Count(root => string.Equals(root.ActorIdHex, actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatWords(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "";

        List<string> words = new();
        for (int offset = 0; offset + 4 <= bytes.Length && words.Count < 8; offset += 4)
            words.Add(Hex32(BitConverter.ToUInt32(bytes, offset)));
        return string.Join(" ", words);
    }

    private static string ShortSha1(byte[] bytes)
    {
        return Convert.ToHexString(SHA1.HashData(bytes)).Substring(0, 12).ToLowerInvariant();
    }

    private static bool IsLevelSpecialDataOffset(long tableRelativeOffset, uint offset)
    {
        return tableRelativeOffset > 0 && offset > 0 && offset < (uint)tableRelativeOffset;
    }

    private static long ParseNumber(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string HexByte(int value) => $"0x{value & 0xFF:X2}";
    private static string HexWord(int value) => $"0x{value & 0xFFFF:X4}";
    private static string HexOffset(uint value) => $"0x{value:X}";
    private static string Hex32(uint value) => $"0x{value:X8}";

    private static string NormalizeHex32(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "0x00000000";
        long parsed = ParseNumber(value);
        return $"0x{parsed & 0xFFFFFFFFL:X8}";
    }
}

public sealed record CrossLevelSpringChestDependencyReport(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    IReadOnlyList<SpringChestLevelDependencySummary> Levels,
    string Conclusion);

public sealed record SpringChestLevelDependencySummary(
    string LevelKey,
    string LevelName,
    int SourceRecordCount,
    int ActorId0149RecordCount,
    int SpringChestRecordCount,
    IReadOnlyDictionary<string, int> DependencyActorCounts,
    IReadOnlyList<SpringChestActorRootDependency> ActorRoots,
    IReadOnlyList<string> DistinctSourceSpecialDataHashes,
    IReadOnlyList<string> DistinctRuntimeSpecialDataPointers,
    IReadOnlyList<SpringChestRecordDependency> Records);

public sealed record SpringChestActorRootDependency(
    int RootIndex,
    string RootSlotHex,
    string RootOffsetHex,
    string ActorIdHex,
    int ApproxLengthToNextRoot,
    bool IsSpringChestDependency);

public sealed record SpringChestRecordDependency(
    int TrueIndex,
    string TypeHex,
    string ActorIdHex,
    string StateHex,
    string RewardByteHex,
    string SourceSpecialOffsetHex,
    int SourceSpecialLength,
    string SourceSpecialSha1,
    string SourceSpecialWords,
    string RuntimeSpecialDataPointer,
    string RuntimePosition,
    string RuntimeLabel);

internal sealed record SpecialLayout(long WadBaseOffset, IReadOnlyDictionary<uint, int> Lengths);

internal sealed record SpecialEntry(uint Offset, int Type);

internal sealed record RuntimeMobySummary(string SpecialDataPointer, string Position, string Label)
{
    public static RuntimeMobySummary Empty { get; } = new("0x00000000", "", "");
}
