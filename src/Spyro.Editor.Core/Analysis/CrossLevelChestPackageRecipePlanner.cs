using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelChestPackageRecipePlanner
{
    private const int WadLba = 37;

    private static readonly ChestPackageSource[] Sources =
    [
        new(
            Family: "lockedChest",
            DisplayName: "Key Chest",
            ActorIdHex: "0x00AE",
            SourceLevelKey: "peacekeepers",
            SourcePackageStartHex: "0x1B3978",
            SourcePackageLengthHex: "0x1008",
            SourceTemplateId: "common.locked_chest.peacekeepers.t79",
            Note: "Uses the minimal Peace Keepers 0x00AE actor-root span; the larger Artisans candidate remains guarded separately until in-game proof settles whether extra dependency bytes are needed."),
        new(
            Family: "springChest",
            DisplayName: "Spring Chest",
            ActorIdHex: "0x0149",
            SourceLevelKey: "peacekeepers",
            SourcePackageStartHex: "0x1B3450",
            SourcePackageLengthHex: "0x0528",
            SourceTemplateId: "common.spring_chest.peacekeepers.t70",
            Note: "Uses the native Peace Keepers 0x0149 package with the T70 all-zero-special donor row. The current Stone Hill test keeps the package byte-for-byte, appends only the native 0x0149 record, and replaces unused-looking actor 0x000E so the root-table order stays native.")
    ];

    public static CrossLevelChestPackageRecipePlanReport Build(EditorWorkspace workspace, LevelCatalog catalog, string sourceImagePath)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        CrossLevelChestStrategyReport strategy = CrossLevelChestStrategyAnalyzer.Build(workspace, catalog, sourceImagePath);

        List<CrossLevelChestPackageRecipePlanRow> rows = [];
        foreach (CrossLevelChestLevelStrategy levelStrategy in strategy.Levels.OrderBy(level => level.LevelName, StringComparer.OrdinalIgnoreCase))
        {
            LevelDefinition? level = catalog.FindByKey(levelStrategy.LevelKey);
            if (level?.HasSourceTable != true)
                continue;

            foreach (ChestPackageSource source in Sources)
            {
                ChestObjectStrategy objectStrategy = string.Equals(source.Family, "lockedChest", StringComparison.OrdinalIgnoreCase)
                    ? levelStrategy.KeyChest
                    : levelStrategy.SpringChest;
                bool needsRecipe = string.Equals(objectStrategy.Strategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase);
                bool hasMappedRecipe = !string.IsNullOrWhiteSpace(objectStrategy.RecipeId);
                if (!needsRecipe && !hasMappedRecipe)
                    continue;

                rows.Add(BuildRow(stream, layout, workspace.RootPath, level, objectStrategy, source));
            }
        }

        return new CrossLevelChestPackageRecipePlanReport(DateTimeOffset.Now, sourceImagePath, rows);
    }

    public static CrossLevelActorPackageRecipe? TryCreateRecipe(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        string sourceImagePath,
        string targetLevelKey,
        string sourceLevelKey,
        string family)
    {
        if (!File.Exists(sourceImagePath))
            return null;

        string normalizedSource = LevelCatalog.NormalizeKey(sourceLevelKey);
        string normalizedFamily = NormalizeFamily(family);
        if (!Sources.Any(source =>
            string.Equals(source.SourceLevelKey, normalizedSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeFamily(source.Family), normalizedFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        CrossLevelChestPackageRecipePlanReport report = Build(workspace, catalog, sourceImagePath);
        CrossLevelChestPackageRecipePlanRow? row = report.Rows.FirstOrDefault(candidate =>
            candidate.CanPlan &&
            string.Equals(LevelCatalog.NormalizeKey(candidate.LevelKey), LevelCatalog.NormalizeKey(targetLevelKey), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(LevelCatalog.NormalizeKey(candidate.SourceLevelKey), normalizedSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeFamily(candidate.Family), normalizedFamily, StringComparison.OrdinalIgnoreCase));
        return row == null ? null : CreateRecipe(row);
    }

    public static async Task WriteAsync(CrossLevelChestPackageRecipePlanReport report, string jsonPath, string markdownPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), cancellationToken);
    }

    private static CrossLevelChestPackageRecipePlanRow BuildRow(FileStream stream, DiscLayout layout, string workspaceRoot, LevelDefinition level, ChestObjectStrategy strategy, ChestPackageSource source)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        long tableRelativeOffset = ParseNumber(level.SourceTableRelativeOffset);
        long entryBase = tableWadOffset - tableRelativeOffset;
        int packageLength = checked((int)ParseNumber(source.SourcePackageLengthHex));

        ZeroRun? zeroRun = FindBestZeroRun(stream, layout, entryBase, tableRelativeOffset, packageLength);
        EmptyRootSlot? rootSlot = FindFirstEmptyRootSlot(stream, layout, entryBase);
        CrossLevelActorPackageRecipe? exactRecipe = CrossLevelActorPackageRecipeCatalog.FindPreferred(level.Key, source.SourceLevelKey, source.Family, workspaceRoot);
        bool canPlan = zeroRun != null && rootSlot != null && exactRecipe == null &&
            (string.Equals(strategy.Strategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(strategy.Strategy, "mapped-package-candidate", StringComparison.OrdinalIgnoreCase));
        string targetStartHex = zeroRun == null ? "" : HexOffset(zeroRun.Start);
        string rootSlotHex = rootSlot == null ? "" : HexOffset(rootSlot.Slot);
        string plannedRecipeId = canPlan
            ? $"{LevelCatalog.NormalizeKey(level.Key)}.{source.SourceLevelKey}.{source.Family}.package.auto{source.ActorIdHex[2..]}Gap{targetStartHex[2..]}.v1"
            : "";

        List<string> notes = [];
        if (!string.IsNullOrWhiteSpace(strategy.RecipeId))
            notes.Add($"Existing recipe mapped: {strategy.RecipeId}.");
        if (zeroRun == null)
            notes.Add($"No zero run large enough for {source.SourcePackageLengthHex} before the source table.");
        if (rootSlot == null)
            notes.Add("No empty actor-root slot was found.");
        if (canPlan)
            notes.Add($"Candidate recipe can copy {source.SourcePackageLengthHex} bytes to {targetStartHex} and register actor {source.ActorIdHex} at root slot {rootSlotHex}.");
        notes.Add(source.Note);

        return new CrossLevelChestPackageRecipePlanRow(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            Family: source.Family,
            DisplayName: source.DisplayName,
            ActorIdHex: source.ActorIdHex,
            SourceTemplateId: source.SourceTemplateId,
            SourceLevelKey: source.SourceLevelKey,
            SourcePackageStartHex: source.SourcePackageStartHex,
            SourcePackageLengthHex: source.SourcePackageLengthHex,
            CurrentStrategy: strategy.Strategy,
            ExistingRecipeId: strategy.RecipeId,
            PlannedRecipeId: plannedRecipeId,
            CanPlan: canPlan,
            TargetPackageStartHex: targetStartHex,
            TargetPackageWadOffsetHex: zeroRun == null ? "" : HexOffset(entryBase + zeroRun.Start),
            ZeroRunLengthHex: zeroRun == null ? "" : HexOffset(zeroRun.Length),
            RootSlotHex: rootSlotHex,
            RootIndex: rootSlot?.Index ?? -1,
            Notes: notes);
    }

    private static CrossLevelActorPackageRecipe CreateRecipe(CrossLevelChestPackageRecipePlanRow row)
    {
        string familyLabel = string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase)
            ? "Key Chest"
            : "Spring Chest";
        return new CrossLevelActorPackageRecipe(
            Id: row.PlannedRecipeId,
            TargetLevelKey: row.LevelKey,
            SourceLevelKey: row.SourceLevelKey,
            Family: row.Family,
            Mode: "AutoRegisterCompanionRoot",
            Status: "experimental-plan-only",
            Risk: $"Auto-planned {familyLabel} package import for {row.LevelName}. The target range and actor-root slot are empty in the source disc, but this is still a disposable candidate until in-game loading, behavior, reward, and nearby-object checks pass.",
            Description: $"{familyLabel} actor package copied from {row.SourceLevelKey} into {row.LevelName} clean zero space and registered as actor {row.ActorIdHex}.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment(row.SourcePackageStartHex, row.TargetPackageStartHex, row.SourcePackageLengthHex)
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry(row.RootSlotHex, row.TargetPackageStartHex, row.ActorIdHex, "", $"Auto-planned root registration for {familyLabel}; keep guarded until this exact candidate passes in-game.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []);
    }

    private static ZeroRun? FindBestZeroRun(FileStream stream, DiscLayout layout, long entryBase, long tableRelativeOffset, int packageLength)
    {
        if (tableRelativeOffset <= packageLength)
            return null;

        const int minStart = 0x200;
        byte[] bytes = DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase, checked((int)tableRelativeOffset));
        ZeroRun? best = null;
        int runStart = -1;
        for (int i = minStart; i < bytes.Length; i++)
        {
            if (bytes[i] == 0)
            {
                if (runStart < 0)
                    runStart = i;
                continue;
            }

            ConsiderRun(runStart, i - runStart);
            runStart = -1;
        }

        ConsiderRun(runStart, bytes.Length - runStart);
        return best;

        void ConsiderRun(int start, int length)
        {
            if (start < 0 || length < packageLength)
                return;

            int alignedStart = AlignUp(start, 4);
            int alignedLength = length - (alignedStart - start);
            if (alignedLength < packageLength)
                return;

            if (best == null || alignedLength > best.Length)
                best = new ZeroRun(alignedStart, alignedLength);
        }
    }

    private static EmptyRootSlot? FindFirstEmptyRootSlot(FileStream stream, DiscLayout layout, long entryBase)
    {
        int highestUsedIndex = -1;
        List<EmptyRootSlot> empty = [];
        for (int index = 0; index < 64; index++)
        {
            int slot = 0x50 + (index * 4);
            uint root = BitConverter.ToUInt32(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + slot, 4), 0);
            ushort actor = BitConverter.ToUInt16(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + 0x150 + (index * 2), 2), 0);
            if (root == 0 && actor == 0)
                empty.Add(new EmptyRootSlot(index, slot));
            else
                highestUsedIndex = index;
        }

        return empty.FirstOrDefault(slot => slot.Index > highestUsedIndex) ?? empty.FirstOrDefault();
    }

    private static string BuildMarkdown(CrossLevelChestPackageRecipePlanReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Universal Chest Package Recipe Planner");
        builder.AppendLine();
        builder.AppendLine("This report scans each target level for a zero-filled package range and an empty actor-root slot. Planned rows are still guarded candidates until they pass disposable in-game testing.");
        builder.AppendLine();
        foreach (IGrouping<string, CrossLevelChestPackageRecipePlanRow> group in report.Rows.GroupBy(row => row.DisplayName).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## {Escape(group.Key)}");
            builder.AppendLine();
            builder.AppendLine($"Plannable: {group.Count(row => row.CanPlan)} / {group.Count()}");
            builder.AppendLine();
            builder.AppendLine("| Level | Current | Planned recipe | Target package | Zero run | Root slot | Notes |");
            builder.AppendLine("|---|---|---|---|---:|---|---|");
            foreach (CrossLevelChestPackageRecipePlanRow row in group.OrderBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"| {Escape(row.LevelName)} | {Escape(row.CurrentStrategy)} | {Escape(row.PlannedRecipeId)} | {Escape(row.TargetPackageStartHex)} | {Escape(row.ZeroRunLengthHex)} | {Escape(row.RootSlotHex)} | {Escape(string.Join(" ", row.Notes))} |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private static long ParseNumber(string value)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(text[2..], 16);
        return long.Parse(text);
    }

    private static string HexOffset(long value) => $"0x{value:X}";
    private static string Escape(string value) => (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    private static string NormalizeFamily(string value) => string.Equals(value, "keyChest", StringComparison.OrdinalIgnoreCase) ? "lockedChest" : value;

    private sealed record ChestPackageSource(
        string Family,
        string DisplayName,
        string ActorIdHex,
        string SourceLevelKey,
        string SourcePackageStartHex,
        string SourcePackageLengthHex,
        string SourceTemplateId,
        string Note);

    private sealed record ZeroRun(int Start, int Length);
    private sealed record EmptyRootSlot(int Index, int Slot);
}

public sealed record CrossLevelChestPackageRecipePlanReport(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    IReadOnlyList<CrossLevelChestPackageRecipePlanRow> Rows);

public sealed record CrossLevelChestPackageRecipePlanRow(
    string LevelKey,
    string LevelName,
    string Family,
    string DisplayName,
    string ActorIdHex,
    string SourceTemplateId,
    string SourceLevelKey,
    string SourcePackageStartHex,
    string SourcePackageLengthHex,
    string CurrentStrategy,
    string ExistingRecipeId,
    string PlannedRecipeId,
    bool CanPlan,
    string TargetPackageStartHex,
    string TargetPackageWadOffsetHex,
    string ZeroRunLengthHex,
    string RootSlotHex,
    int RootIndex,
    IReadOnlyList<string> Notes);
