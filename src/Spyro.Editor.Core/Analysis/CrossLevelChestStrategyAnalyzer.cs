using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelChestStrategyAnalyzer
{
    private const int WadLba = 37;
    private const int RecordStride = 0x58;

    public static CrossLevelChestStrategyReport Build(EditorWorkspace workspace, LevelCatalog catalog, string sourceImagePath)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);

        List<CrossLevelChestLevelStrategy> levels = [];
        foreach (LevelDefinition level in catalog.Levels.Where(level => level.HasSourceTable))
            levels.Add(ReadLevel(workspace, catalog, stream, layout, level));

        return new CrossLevelChestStrategyReport(DateTimeOffset.Now, sourceImagePath, levels);
    }

    public static async Task WriteAsync(CrossLevelChestStrategyReport report, string jsonPath, string markdownPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), cancellationToken);
    }

    private static CrossLevelChestLevelStrategy ReadLevel(EditorWorkspace workspace, LevelCatalog catalog, FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        long tableRelativeOffset = ParseNumber(level.SourceTableRelativeOffset);
        long entryBase = tableWadOffset - tableRelativeOffset;
        Dictionary<int, List<ChestRecordSignature>> recordsByActor = ReadRecords(stream, layout, tableWadOffset, tableRelativeOffset, level.SourceRecordCount);
        Dictionary<int, List<ActorRootSignature>> rootsByActor = ReadRoots(stream, layout, entryBase);

        ChestObjectStrategy key = new(
            Family: "key",
            DisplayName: "Key",
            ActorIdHex: "0x00AD",
            NativeRecordCount: recordsByActor.GetValueOrDefault(0x00AD)?.Count ?? 0,
            NativeRootCount: rootsByActor.GetValueOrDefault(0x00AD)?.Count ?? 0,
            FirstNativeTrueIndex: recordsByActor.GetValueOrDefault(0x00AD)?.FirstOrDefault()?.TrueIndex ?? -1,
            FirstNativeSpecialHash: recordsByActor.GetValueOrDefault(0x00AD)?.FirstOrDefault()?.SpecialSha1 ?? "",
            RecipeId: "",
            Strategy: "universal-lightweight",
            NextAction: "Use direct source-record append and preserve key identity bytes.");

        ChestObjectStrategy keyChest = BuildObjectStrategy(
            workspace,
            level,
            "lockedChest",
            "Key Chest",
            0x00AE,
            ["peacekeepers"],
            recordsByActor,
            rootsByActor);
        ChestObjectStrategy springChest = BuildObjectStrategy(
            workspace,
            level,
            "springChest",
            "Spring Chest",
            0x0149,
            ["peacekeepers", "townsquare"],
            recordsByActor,
            rootsByActor);

        return new CrossLevelChestLevelStrategy(level.Key, level.DisplayName, level.SourceRecordCount, key, keyChest, springChest);
    }

    private static ChestObjectStrategy BuildObjectStrategy(
        EditorWorkspace workspace,
        LevelDefinition level,
        string family,
        string displayName,
        int actorId,
        IReadOnlyList<string> sourceLevelKeys,
        Dictionary<int, List<ChestRecordSignature>> recordsByActor,
        Dictionary<int, List<ActorRootSignature>> rootsByActor)
    {
        List<ChestRecordSignature> nativeRecords = recordsByActor.GetValueOrDefault(actorId) ?? [];
        List<ActorRootSignature> nativeRoots = rootsByActor.GetValueOrDefault(actorId) ?? [];
        CrossLevelActorPackageRecipe? recipe = null;
        foreach (string sourceLevelKey in sourceLevelKeys)
        {
            recipe = CrossLevelActorPackageRecipeCatalog.FindPreferred(level.Key, sourceLevelKey, family, workspace.RootPath);
            if (recipe != null)
                break;
        }
        string recipeId = recipe?.Id ?? "";
        string strategy;
        string nextAction;

        if (nativeRecords.Count > 0 && nativeRoots.Count > 0)
        {
            strategy = "native-clone";
            nextAction = $"Prefer cloning local {displayName} donor T{nativeRecords[0].TrueIndex}; no cross-level actor package import should be required.";
        }
        else if (nativeRoots.Count > 0)
        {
            strategy = "native-root-needs-signature";
            nextAction = $"Actor package is already loaded; identify a safe local {displayName} source signature or clone from a level-specific donor.";
        }
        else if (recipe != null)
        {
            strategy = "mapped-package-candidate";
            nextAction = $"Use guarded recipe {recipe.Id} in a disposable candidate, then promote only after in-game proof.";
        }
        else
        {
            strategy = "needs-package-recipe";
            nextAction = $"Map a level-safe {displayName} actor package/root recipe before normal Create BIN can add it.";
        }

        return new ChestObjectStrategy(
            Family: family,
            DisplayName: displayName,
            ActorIdHex: HexWord(actorId),
            NativeRecordCount: nativeRecords.Count,
            NativeRootCount: nativeRoots.Count,
            FirstNativeTrueIndex: nativeRecords.FirstOrDefault()?.TrueIndex ?? -1,
            FirstNativeSpecialHash: nativeRecords.FirstOrDefault()?.SpecialSha1 ?? "",
            RecipeId: recipeId,
            Strategy: strategy,
            NextAction: nextAction);
    }

    private static Dictionary<int, List<ChestRecordSignature>> ReadRecords(FileStream stream, DiscLayout layout, long tableWadOffset, long tableRelativeOffset, int sourceRecordCount)
    {
        Dictionary<int, List<ChestRecordSignature>> result = [];
        SpecialLayout specialLayout = BuildSpecialLayout(stream, layout, tableWadOffset, tableRelativeOffset, sourceRecordCount);
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            int actorId = record[0x36] | (record[0x37] << 8);
            uint specialOffset = BitConverter.ToUInt32(record, 0);
            string specialHash = "";
            if (IsSourceSpecialDataOffset(tableRelativeOffset, specialOffset))
            {
                int length = specialLayout.Lengths.TryGetValue(specialOffset, out int mappedLength)
                    ? mappedLength
                    : record[0x50] == 0x00 ? 0x28 : 0x18;
                byte[] specialBytes = DiscImage.ReadFileBytes(stream, layout, WadLba, specialLayout.WadBaseOffset + specialOffset, length);
                specialHash = Convert.ToHexString(SHA1.HashData(specialBytes))[..12].ToLowerInvariant();
            }

            if (!result.TryGetValue(actorId, out List<ChestRecordSignature>? records))
            {
                records = [];
                result[actorId] = records;
            }

            records.Add(new ChestRecordSignature(
                TrueIndex: trueIndex,
                TypeHex: HexByte(record[0x50]),
                StateHex: HexByte(record[0x51]),
                Flag4AHex: HexByte(record[0x52]),
                Flag4BHex: HexByte(record[0x53]),
                SpecialOffsetHex: specialOffset == 0 ? "" : HexOffset(specialOffset),
                SpecialSha1: specialHash));
        }

        return result;
    }

    private static Dictionary<int, List<ActorRootSignature>> ReadRoots(FileStream stream, DiscLayout layout, long entryBase)
    {
        Dictionary<int, List<ActorRootSignature>> result = [];
        for (int index = 0; index < 64; index++)
        {
            int rootSlot = 0x50 + (index * 4);
            uint root = BitConverter.ToUInt32(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + rootSlot, 4), 0);
            ushort actorId = BitConverter.ToUInt16(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + 0x150 + (index * 2), 2), 0);
            if (root == 0 && actorId == 0)
                continue;

            if (!result.TryGetValue(actorId, out List<ActorRootSignature>? roots))
            {
                roots = [];
                result[actorId] = roots;
            }

            roots.Add(new ActorRootSignature(HexOffset((uint)rootSlot), HexOffset(root), HexWord(actorId)));
        }

        return result;
    }

    private static SpecialLayout BuildSpecialLayout(FileStream stream, DiscLayout layout, long tableWadOffset, long tableRelativeOffset, int sourceRecordCount)
    {
        List<(uint Offset, byte Type)> entries = [];
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            uint offset = BitConverter.ToUInt32(record, 0);
            if (IsSourceSpecialDataOffset(tableRelativeOffset, offset))
                entries.Add((offset, record[0x50]));
        }

        List<(uint Offset, byte Type)> unique = entries
            .GroupBy(entry => entry.Offset)
            .Select(group => group.First())
            .OrderBy(entry => entry.Offset)
            .ToList();
        Dictionary<uint, int> lengths = [];
        for (int i = 0; i < unique.Count; i++)
        {
            int length = 0;
            if (i + 1 < unique.Count)
            {
                uint delta = unique[i + 1].Offset - unique[i].Offset;
                if (delta > 0 && delta <= 0x400)
                    length = (int)delta;
            }

            lengths[unique[i].Offset] = length <= 0
                ? unique[i].Type == 0x00 ? 0x28 : 0x18
                : length;
        }

        return new SpecialLayout(tableWadOffset - tableRelativeOffset, lengths);
    }

    private static string BuildMarkdown(CrossLevelChestStrategyReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Universal Chest Strategy");
        builder.AppendLine();
        builder.AppendLine("This report separates safe local cloning from risky cross-level actor-package imports. Keys are universal lightweight adds; Key Chests and Spring Chests should use native level donors when the level already has the actor record/root.");
        builder.AppendLine();
        builder.AppendLine("| Object | Native clone | Native root only | Package recipe | Needs recipe |");
        builder.AppendLine("|---|---:|---:|---:|---:|");
        foreach (string family in new[] { "lockedChest", "springChest" })
        {
            List<ChestObjectStrategy> strategies = report.Levels
                .Select(level => family == "lockedChest" ? level.KeyChest : level.SpringChest)
                .ToList();
            builder.AppendLine($"| {strategies[0].DisplayName} | {Count(strategies, "native-clone")} | {Count(strategies, "native-root-needs-signature")} | {Count(strategies, "mapped-package-candidate")} | {Count(strategies, "needs-package-recipe")} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Level | Key | Key Chest | Spring Chest | Next Best Action |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (CrossLevelChestLevelStrategy level in report.Levels.OrderBy(level => level.LevelName, StringComparer.OrdinalIgnoreCase))
        {
            string nextAction = level.KeyChest.Strategy != "native-clone"
                ? level.KeyChest.NextAction
                : level.SpringChest.Strategy != "native-clone"
                    ? level.SpringChest.NextAction
                    : "Use local clone path for both chest types; verify reward/link behavior in a disposable test.";
            builder.AppendLine($"| {Escape(level.LevelName)} | {Format(level.Key)} | {Format(level.KeyChest)} | {Format(level.SpringChest)} | {Escape(nextAction)} |");
        }

        return builder.ToString();
    }

    private static int Count(IEnumerable<ChestObjectStrategy> strategies, string strategy)
    {
        return strategies.Count(item => string.Equals(item.Strategy, strategy, StringComparison.OrdinalIgnoreCase));
    }

    private static string Format(ChestObjectStrategy strategy)
    {
        string donor = strategy.FirstNativeTrueIndex >= 0 ? $" T{strategy.FirstNativeTrueIndex}" : "";
        string recipe = string.IsNullOrWhiteSpace(strategy.RecipeId) ? "" : $" `{strategy.RecipeId}`";
        return $"{strategy.Strategy}{donor}{recipe}";
    }

    private static bool IsSourceSpecialDataOffset(long tableRelativeOffset, uint offset)
    {
        return tableRelativeOffset > 0 && offset > 0 && offset < (uint)tableRelativeOffset;
    }

    private static long ParseNumber(string value)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(text[2..], 16);
        return long.Parse(text);
    }

    private static string HexOffset(uint value) => $"0x{value:X}";
    private static string HexByte(int value) => $"0x{value & 0xFF:X2}";
    private static string HexWord(int value) => $"0x{value & 0xFFFF:X4}";
    private static string Escape(string value) => (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private sealed record SpecialLayout(long WadBaseOffset, IReadOnlyDictionary<uint, int> Lengths);
}

public sealed record CrossLevelChestStrategyReport(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    IReadOnlyList<CrossLevelChestLevelStrategy> Levels);

public sealed record CrossLevelChestLevelStrategy(
    string LevelKey,
    string LevelName,
    int SourceRecordCount,
    ChestObjectStrategy Key,
    ChestObjectStrategy KeyChest,
    ChestObjectStrategy SpringChest);

public sealed record ChestObjectStrategy(
    string Family,
    string DisplayName,
    string ActorIdHex,
    int NativeRecordCount,
    int NativeRootCount,
    int FirstNativeTrueIndex,
    string FirstNativeSpecialHash,
    string RecipeId,
    string Strategy,
    string NextAction);

public sealed record ChestRecordSignature(
    int TrueIndex,
    string TypeHex,
    string StateHex,
    string Flag4AHex,
    string Flag4BHex,
    string SpecialOffsetHex,
    string SpecialSha1);

public sealed record ActorRootSignature(
    string RootSlotHex,
    string RootOffsetHex,
    string ActorIdHex);
