using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelObjectImportAnalyzer
{
    private const int WadLba = 37;
    private const int RecordStride = 0x58;
    private const int MainRamSize = 0x200000;
    private const int ActorPointerTableRamOffset = 0x76378;

    public static CrossLevelObjectImportAnalysis Build(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        string sourceImagePath,
        string templatePath,
        IReadOnlyList<string> targetLevelKeys)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Missing cross-level object template catalog.", templatePath);

        List<CrossLevelObjectTemplateSummary> templates = LoadTemplates(templatePath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);

        List<CrossLevelObjectImportRow> rows = new();
        foreach (CrossLevelObjectTemplateSummary template in templates)
        {
            LevelDefinition? sourceLevel = catalog.FindByKey(template.SourceLevelKey);
            SourceMobyRecord? sourceRecord = sourceLevel?.HasSourceTable == true && template.SourceTrueIndex >= 0 && template.SourceTrueIndex < sourceLevel.SourceRecordCount
                ? ReadSourceRecord(stream, layout, sourceLevel, template.SourceTrueIndex)
                : null;

            foreach (string targetLevelKey in targetLevelKeys)
            {
                LevelDefinition? targetLevel = catalog.FindByKey(targetLevelKey);
                if (targetLevel == null || !targetLevel.HasSourceTable)
                {
                    rows.Add(CrossLevelObjectImportRow.MissingTarget(template, targetLevelKey));
                    continue;
                }

                TargetMobySignatureSummary targetSummary = ReadTargetSummary(stream, layout, targetLevel, template);
                TargetRamActorSlot actorSlot = ReadTargetActorSlot(workspace, stream, layout, targetLevel, template.ActorId16);
                string supportStatus = ClassifySupport(template, targetSummary, actorSlot);
                CrossLevelActorPackageRecipe? recipe = CrossLevelActorPackageRecipeCatalog.FindPreferred(targetLevel.Key, template.SourceLevelKey, template.Family, workspace.RootPath) ??
                    CrossLevelChestPackageRecipePlanner.TryCreateRecipe(workspace, catalog, sourceImagePath, targetLevel.Key, template.SourceLevelKey, template.Family);
                string nextStep = BuildNextStep(template, actorSlot, supportStatus, recipe);
                rows.Add(new CrossLevelObjectImportRow(
                    TemplateId: template.Id,
                    DisplayName: template.DisplayName,
                    Family: template.Family,
                    SourceLevelName: sourceLevel?.DisplayName ?? template.SourceLevelName,
                    SourceTrueIndex: template.SourceTrueIndex,
                    TargetLevelKey: targetLevel.Key,
                    TargetLevelName: targetLevel.DisplayName,
                    TypeHex: HexByte(template.Type),
                    ActorId16: HexWord(template.ActorId16),
                    SourceRecordWadOffset: sourceRecord?.RecordWadOffsetHex ?? "",
                    SourceRecordSpecialOffset: sourceRecord?.SpecialOffsetHex ?? "",
                    SourceRecordHasLevelSpecialData: sourceRecord?.HasLevelSpecialData ?? false,
                    TargetSameActorIdCount: targetSummary.SameActorIdCount,
                    TargetSameTemplateSignatureCount: targetSummary.SameTemplateSignatureCount,
                    TargetSameTypeCount: targetSummary.SameTypeCount,
                    TargetRamPath: actorSlot.RamPath,
                    TargetActorSlotAddress: actorSlot.ActorSlotRuntimeAddress,
                    TargetActorPointer: actorSlot.PointerHex,
                    TargetActorPointerIsZero: actorSlot.PointerIsZero,
                    RuntimeSpecialDataPointer: template.SpecialDataPointerHex,
                    RequiredExporterFeature: template.RequiredExporterFeature,
                    RecipeId: recipe?.Id ?? "",
                    RecipeMode: recipe?.Mode ?? "",
                    RecipeStatus: recipe?.Status ?? "",
                    RecipeDescription: recipe?.Description ?? "",
                    RecipeRisk: recipe?.Risk ?? "",
                    RecipeCopySegmentCount: recipe?.CopySegmentCount ?? 0,
                    RecipeRootEntryCount: recipe?.RootEntryCount ?? 0,
                    RecipeReplaceRootEntryCount: recipe?.ReplaceRootEntryCount ?? 0,
                    Recipe: recipe,
                    SupportStatus: supportStatus,
                    NextStep: nextStep));
            }
        }

        return new CrossLevelObjectImportAnalysis(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: sourceImagePath,
            TemplatePath: templatePath,
            TargetLevelKeys: targetLevelKeys.ToArray(),
            Rows: rows);
    }

    public static async Task WriteAsync(CrossLevelObjectImportAnalysis analysis, string jsonPath, string markdownPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        JsonSerializerOptions options = new() { WriteIndented = true };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(analysis, options), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(analysis), cancellationToken);
    }

    private static List<CrossLevelObjectTemplateSummary> LoadTemplates(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("templates", out JsonElement templatesElement) ||
            templatesElement.ValueKind != JsonValueKind.Array)
            return [];

        List<CrossLevelObjectTemplateSummary> templates = new();
        foreach (JsonElement item in templatesElement.EnumerateArray())
        {
            if (!JsonValue.GetBoolean(item, "showInAddList"))
                continue;

            int sourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex", 0);
            int sourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex", 0);
            templates.Add(new CrossLevelObjectTemplateSummary(
                Id: JsonValue.GetString(item, "id"),
                DisplayName: JsonValue.GetString(item, "displayName", "Object"),
                Family: JsonValue.GetString(item, "family"),
                SourceLevelKey: JsonValue.GetString(item, "sourceLevelKey"),
                SourceLevelName: JsonValue.GetString(item, "sourceLevelName"),
                SourceTrueIndex: JsonValue.GetInt32(item, "sourceTrueIndex", -1),
                Type: JsonValue.GetInt32(item, "typeHex", 0),
                State: JsonValue.GetInt32(item, "stateHex", 0),
                SourceByte36: sourceByte36,
                SourceByte37: sourceByte37,
                SourceByte4F: JsonValue.GetInt32(item, "sourceByte4FHex", 0),
                Flag4A: JsonValue.GetInt32(item, "flag4AHex", 0),
                Flag4B: JsonValue.GetInt32(item, "flag4BHex", 0),
                SpecialDataPointerHex: NormalizeHex32(JsonValue.GetString(item, "specialDataPointer", "0x00000000")),
                AddSupportStatus: JsonValue.GetString(item, "addSupportStatus"),
                RequiredExporterFeature: JsonValue.GetString(item, "requiredExporterFeature"),
                ActorId16: sourceByte36 | (sourceByte37 << 8)));
        }

        return templates;
    }

    private static SourceMobyRecord ReadSourceRecord(FileStream stream, DiscLayout layout, LevelDefinition level, int trueIndex)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        long tableRelativeOffset = ParseNumber(level.SourceTableRelativeOffset);
        long recordWadOffset = tableWadOffset + ((long)trueIndex * RecordStride);
        byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, recordWadOffset, RecordStride);
        uint specialOffset0 = BitConverter.ToUInt32(record, 0);
        uint specialOffset8 = BitConverter.ToUInt32(record, 8);
        bool special0 = IsLevelSpecialDataOffset(tableRelativeOffset, specialOffset0);
        bool special8 = IsLevelSpecialDataOffset(tableRelativeOffset, specialOffset8);
        uint special = special0 ? specialOffset0 : special8 ? specialOffset8 : 0;
        return new SourceMobyRecord(
            RecordWadOffsetHex: HexOffset(recordWadOffset),
            SpecialOffsetHex: special == 0 ? "" : HexOffset(special),
            HasLevelSpecialData: special != 0);
    }

    private static TargetMobySignatureSummary ReadTargetSummary(FileStream stream, DiscLayout layout, LevelDefinition level, CrossLevelObjectTemplateSummary template)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        int sameType = 0;
        int sameActor = 0;
        int sameTemplate = 0;
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            int actorId = record[0x36] | (record[0x37] << 8);
            if (record[0x50] == template.Type)
                sameType++;
            if (actorId == template.ActorId16)
                sameActor++;
            if (record[0x50] == template.Type &&
                record[0x51] == template.State &&
                record[0x36] == template.SourceByte36 &&
                record[0x37] == template.SourceByte37 &&
                record[0x4F] == template.SourceByte4F &&
                record[0x52] == template.Flag4A &&
                record[0x53] == template.Flag4B)
            {
                sameTemplate++;
            }
        }

        return new TargetMobySignatureSummary(sameType, sameActor, sameTemplate);
    }

    private static TargetRamActorSlot ReadTargetActorSlot(EditorWorkspace workspace, FileStream stream, DiscLayout layout, LevelDefinition targetLevel, int actorId16)
    {
        string ramPath = FindWorkspaceSiblingFile(workspace, $"{targetLevel.Key}-before-clean.bin");
        if (!File.Exists(ramPath))
            ramPath = FindWorkspaceSiblingFile(workspace, $"{LevelCatalog.NormalizeKey(targetLevel.ScriptKey)}-before-clean.bin");
        if (!File.Exists(ramPath))
            return ReadTargetSourceActorRoot(stream, layout, targetLevel, actorId16);

        byte[] ram = File.ReadAllBytes(ramPath);
        if (ram.Length != MainRamSize)
            return ReadTargetSourceActorRoot(stream, layout, targetLevel, actorId16);

        int offset = ActorPointerTableRamOffset + (actorId16 * 4);
        if (offset < 0 || offset + 4 > ram.Length)
            return ReadTargetSourceActorRoot(stream, layout, targetLevel, actorId16);

        uint pointer = BitConverter.ToUInt32(ram, offset);
        if (pointer == 0)
        {
            TargetRamActorSlot sourceRoot = ReadTargetSourceActorRoot(stream, layout, targetLevel, actorId16);
            if (!sourceRoot.PointerIsZero)
                return sourceRoot;
        }

        return new TargetRamActorSlot(
            RamPath: ramPath,
            ActorSlotRuntimeAddress: Hex32(0x80000000u + (uint)offset),
            PointerHex: Hex32(pointer),
            PointerIsZero: pointer == 0);
    }

    private static TargetRamActorSlot ReadTargetSourceActorRoot(FileStream stream, DiscLayout layout, LevelDefinition targetLevel, int actorId16)
    {
        if (!targetLevel.HasSourceTable)
            return new TargetRamActorSlot("", "", "0x00000000", true);

        long tableWadOffset = ParseNumber(targetLevel.SourceTableWadOffset);
        long tableRelativeOffset = ParseNumber(targetLevel.SourceTableRelativeOffset);
        long entryBase = tableWadOffset - tableRelativeOffset;
        for (int index = 0; index < 64; index++)
        {
            long actorIdOffset = entryBase + 0x150 + (index * 2);
            ushort actorId = BitConverter.ToUInt16(DiscImage.ReadFileBytes(stream, layout, WadLba, actorIdOffset, 2), 0);
            if (actorId != actorId16)
                continue;

            int rootSlot = 0x50 + (index * 4);
            uint root = BitConverter.ToUInt32(DiscImage.ReadFileBytes(stream, layout, WadLba, entryBase + rootSlot, 4), 0);
            return new TargetRamActorSlot(
                RamPath: $"source-wad:{targetLevel.Key}",
                ActorSlotRuntimeAddress: $"source-root-slot:{HexByte(rootSlot)}",
                PointerHex: root == 0 ? "0x00000000" : HexOffset(root),
                PointerIsZero: root == 0);
        }

        return new TargetRamActorSlot(
            RamPath: $"source-wad:{targetLevel.Key}",
            ActorSlotRuntimeAddress: "",
            PointerHex: "0x00000000",
            PointerIsZero: true);
    }

    private static string FindWorkspaceSiblingFile(EditorWorkspace workspace, string fileName)
    {
        foreach (string root in CandidateRoots(workspace))
        {
            string path = Path.Combine(root, fileName);
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, fileName);
    }

    private static IEnumerable<string> CandidateRoots(EditorWorkspace workspace)
    {
        yield return workspace.RootPath;
        DirectoryInfo? parent = Directory.GetParent(workspace.RootPath);
        if (parent == null)
            yield break;

        foreach (DirectoryInfo sibling in SafeEnumerateDirectories(parent))
        {
            yield return sibling.FullName;
            foreach (DirectoryInfo nested in SafeEnumerateDirectories(sibling))
                yield return nested.FullName;
        }
    }

    private static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories().ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<DirectoryInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }

    private static string ClassifySupport(CrossLevelObjectTemplateSummary template, TargetMobySignatureSummary target, TargetRamActorSlot actorSlot)
    {
        if (string.Equals(template.RequiredExporterFeature, "DirectSourceRecordAppend", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(template.AddSupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase))
            return "supported-lightweight-object";

        if (string.IsNullOrWhiteSpace(actorSlot.RamPath))
            return "needs-target-ram-capture";

        if (actorSlot.PointerIsZero)
            return "blocked-missing-target-actor-package";

        if (target.SameTemplateSignatureCount == 0)
            return "blocked-missing-template-signature";

        return "candidate-needs-game-test";
    }

    private static string BuildNextStep(
        CrossLevelObjectTemplateSummary template,
        TargetRamActorSlot actorSlot,
        string supportStatus,
        CrossLevelActorPackageRecipe? recipe)
    {
        if (recipe != null && string.Equals(recipe.Status, "in-game-blocked-load-freeze", StringComparison.OrdinalIgnoreCase))
            return $"Recipe {recipe.Id} wrote cleanly at the byte level but froze during in-game level load. Keep it guarded and map the missing runtime dependency before another promoted write.";

        if (recipe != null && supportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase))
            return $"Native recipe candidate {recipe.Id} is available. Next step: emit a guarded plan-only package import patch, then verify in a disposable BIN/CUE.";

        return supportStatus switch
        {
            "supported-lightweight-object" => "Use direct source-record append and preserve identity bytes.",
            "needs-target-ram-capture" => "Capture this target level's before-clean RAM or map the source WAD actor root table.",
            "blocked-missing-target-actor-package" => $"Import/rebase the {template.DisplayName} actor package and patch actor pointer slot {actorSlot.ActorSlotRuntimeAddress}.",
            "blocked-missing-template-signature" => "The target actor slot is populated, but the exact source identity/signature is absent; test package remap or dependency-cluster import.",
            _ => "Build a disposable BIN/CUE and verify in game before promoting this template."
        };
    }

    private static string BuildMarkdown(CrossLevelObjectImportAnalysis analysis)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Cross-Level Object Import Analysis");
        builder.AppendLine();
        builder.AppendLine("This report checks whether each cross-level object template can be handled by a simple source-record append or whether the target level needs actor-package/dependency import work.");
        builder.AppendLine();
        builder.AppendLine("| Object | Target | Actor | Target pointer | Same actor | Same signature | Recipe | Status | Next step |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---|---|---|");
        foreach (CrossLevelObjectImportRow row in analysis.Rows)
        {
            string recipe = string.IsNullOrWhiteSpace(row.RecipeId)
                ? ""
                : $"{row.RecipeId} ({row.RecipeCopySegmentCount} copy, {row.RecipeRootEntryCount + row.RecipeReplaceRootEntryCount} root)";
            builder.AppendLine($"| {Escape(row.DisplayName)} | {Escape(row.TargetLevelName)} | `{row.ActorId16}` | `{row.TargetActorPointer}` | {row.TargetSameActorIdCount} | {row.TargetSameTemplateSignatureCount} | {Escape(recipe)} | {Escape(row.SupportStatus)} | {Escape(row.NextStep)} |");
        }

        List<CrossLevelObjectImportRow> recipeRows = analysis.Rows
            .Where(row => row.Recipe != null)
            .OrderBy(row => row.TargetLevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipeRows.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Native Recipe Candidates");
            foreach (CrossLevelObjectImportRow row in recipeRows)
            {
                CrossLevelActorPackageRecipe recipe = row.Recipe!;
                builder.AppendLine();
                builder.AppendLine($"### {row.TargetLevelName}: {row.DisplayName}");
                builder.AppendLine();
                builder.AppendLine($"- Recipe: `{recipe.Id}`");
                builder.AppendLine($"- Mode: {Escape(recipe.Mode)}");
                builder.AppendLine($"- Status: {Escape(recipe.Status)}");
                builder.AppendLine($"- Risk: {Escape(recipe.Risk)}");
                builder.AppendLine($"- Description: {Escape(recipe.Description)}");
                AppendSegments(builder, "Copy segments", recipe.CopySegments.Select(segment => $"`{segment.SourceStart}` -> `{segment.TargetStart}` length `{segment.Length}`"));
                AppendSegments(builder, "Root registrations", recipe.RootEntries.Select(entry => $"slot `{entry.TargetRootSlot}` root `{entry.TargetRoot}` actor `{entry.ActorId}`{FormatOriginalActor(entry)}{FormatNote(entry)}"));
                AppendSegments(builder, "Root replacements", recipe.ReplaceRootEntries.Select(entry => $"slot `{entry.TargetRootSlot}` root `{entry.TargetRoot}` actor `{entry.ActorId}`{FormatOriginalActor(entry)}{FormatNote(entry)}"));
                AppendSegments(builder, "Internal dependency rebases", recipe.InternalDependencyRebases.Select(rebase => $"`{rebase.SourceRoot}` -> `{rebase.TargetRoot}` length `{rebase.Length}` actor `{rebase.ActorId}`{FormatNote(rebase.Note)}"));
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static void AppendSegments(StringBuilder builder, string label, IEnumerable<string> lines)
    {
        List<string> materialized = lines.ToList();
        if (materialized.Count == 0)
            return;

        builder.AppendLine($"- {label}:");
        foreach (string line in materialized)
            builder.AppendLine($"  - {line}");
    }

    private static string FormatOriginalActor(CrossLevelActorPackageRootEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.OriginalActor) ? "" : $" replacing `{entry.OriginalActor}`";
    }

    private static string FormatNote(CrossLevelActorPackageRootEntry entry)
    {
        return FormatNote(entry.Note);
    }

    private static string FormatNote(string note)
    {
        return string.IsNullOrWhiteSpace(note) ? "" : $" ({Escape(note)})";
    }

    private static bool IsLevelSpecialDataOffset(long tableRelativeOffset, uint offset)
    {
        return tableRelativeOffset > 0 && offset > 0 && offset < (uint)tableRelativeOffset;
    }

    private static long ParseNumber(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string NormalizeHex32(string text)
    {
        long value = 0;
        if (!string.IsNullOrWhiteSpace(text))
            value = text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(text.Trim()[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(text.Trim(), CultureInfo.InvariantCulture);
        return Hex32((uint)value);
    }

    private static string HexByte(int value) => $"0x{Math.Clamp(value, 0, 255):X2}";
    private static string HexWord(int value) => $"0x{Math.Clamp(value, 0, 0xFFFF):X4}";
    private static string HexOffset(long value) => $"0x{value:X}";
    private static string Hex32(uint value) => $"0x{value:X8}";
}

public sealed record CrossLevelObjectImportAnalysis(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string TemplatePath,
    IReadOnlyList<string> TargetLevelKeys,
    IReadOnlyList<CrossLevelObjectImportRow> Rows);

public sealed record CrossLevelObjectImportRow(
    string TemplateId,
    string DisplayName,
    string Family,
    string SourceLevelName,
    int SourceTrueIndex,
    string TargetLevelKey,
    string TargetLevelName,
    string TypeHex,
    string ActorId16,
    string SourceRecordWadOffset,
    string SourceRecordSpecialOffset,
    bool SourceRecordHasLevelSpecialData,
    int TargetSameActorIdCount,
    int TargetSameTemplateSignatureCount,
    int TargetSameTypeCount,
    string TargetRamPath,
    string TargetActorSlotAddress,
    string TargetActorPointer,
    bool TargetActorPointerIsZero,
    string RuntimeSpecialDataPointer,
    string RequiredExporterFeature,
    string RecipeId,
    string RecipeMode,
    string RecipeStatus,
    string RecipeDescription,
    string RecipeRisk,
    int RecipeCopySegmentCount,
    int RecipeRootEntryCount,
    int RecipeReplaceRootEntryCount,
    CrossLevelActorPackageRecipe? Recipe,
    string SupportStatus,
    string NextStep)
{
    public static CrossLevelObjectImportRow MissingTarget(CrossLevelObjectTemplateSummary template, string targetLevelKey)
    {
        return new CrossLevelObjectImportRow(
            TemplateId: template.Id,
            DisplayName: template.DisplayName,
            Family: template.Family,
            SourceLevelName: template.SourceLevelName,
            SourceTrueIndex: template.SourceTrueIndex,
            TargetLevelKey: targetLevelKey,
            TargetLevelName: targetLevelKey,
            TypeHex: $"0x{template.Type:X2}",
            ActorId16: $"0x{template.ActorId16:X4}",
            SourceRecordWadOffset: "",
            SourceRecordSpecialOffset: "",
            SourceRecordHasLevelSpecialData: false,
            TargetSameActorIdCount: 0,
            TargetSameTemplateSignatureCount: 0,
            TargetSameTypeCount: 0,
            TargetRamPath: "",
            TargetActorSlotAddress: "",
            TargetActorPointer: "0x00000000",
            TargetActorPointerIsZero: true,
            RuntimeSpecialDataPointer: template.SpecialDataPointerHex,
            RequiredExporterFeature: template.RequiredExporterFeature,
            RecipeId: "",
            RecipeMode: "",
            RecipeStatus: "",
            RecipeDescription: "",
            RecipeRisk: "",
            RecipeCopySegmentCount: 0,
            RecipeRootEntryCount: 0,
            RecipeReplaceRootEntryCount: 0,
            Recipe: null,
            SupportStatus: "missing-target-level",
            NextStep: "Map this target level before attempting cross-level object import.");
    }
}

public sealed record CrossLevelObjectTemplateSummary(
    string Id,
    string DisplayName,
    string Family,
    string SourceLevelKey,
    string SourceLevelName,
    int SourceTrueIndex,
    int Type,
    int State,
    int SourceByte36,
    int SourceByte37,
    int SourceByte4F,
    int Flag4A,
    int Flag4B,
    string SpecialDataPointerHex,
    string AddSupportStatus,
    string RequiredExporterFeature,
    int ActorId16);

internal sealed record SourceMobyRecord(string RecordWadOffsetHex, string SpecialOffsetHex, bool HasLevelSpecialData);
internal sealed record TargetMobySignatureSummary(int SameTypeCount, int SameActorIdCount, int SameTemplateSignatureCount);
internal sealed record TargetRamActorSlot(string RamPath, string ActorSlotRuntimeAddress, string PointerHex, bool PointerIsZero);
