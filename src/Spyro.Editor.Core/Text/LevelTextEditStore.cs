using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Text;

public static class LevelTextEditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string PlanPath(string workspaceRoot, TextTargetEntry target) =>
        Path.Combine(workspaceRoot, $"{target.LevelKey}-level-text-edit-plan.json");

    public static LevelTextEditPlan? Load(string workspaceRoot, TextTargetEntry target)
    {
        string path = PlanPath(workspaceRoot, target);
        if (!File.Exists(path))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string levelKey = GetString(root, "levelKey");
            string originalText = GetString(root, "originalText");
            string replacement = TextTargetCatalog.NormalizeReplacement(GetString(root, "replacementText"));
            if (!string.Equals(LevelCatalog.NormalizeKey(levelKey), LevelCatalog.NormalizeKey(target.LevelKey), StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(originalText) && !string.Equals(originalText, target.OriginalText, StringComparison.Ordinal) ||
                !TextTargetCatalog.IsSafeReplacement(replacement, target.MaxLength) ||
                string.Equals(replacement, target.OriginalText, StringComparison.Ordinal))
            {
                return null;
            }

            return new LevelTextEditPlan(
                GeneratedAt: GetDateTimeOffset(root, "generatedAt"),
                Kind: GetString(root, "kind", "exe-level-name-string-edit"),
                LevelKey: target.LevelKey,
                LevelName: target.DisplayName,
                ScriptKey: target.ScriptKey,
                OriginalText: target.OriginalText,
                ReplacementText: replacement,
                MaxLength: target.MaxLength,
                TableKind: target.TableKind,
                TableIndex: target.TableIndex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static async Task<string> SaveAsync(
        string workspaceRoot,
        TextTargetEntry target,
        string replacementText,
        CancellationToken cancellationToken = default)
    {
        string replacement = TextTargetCatalog.NormalizeReplacement(replacementText);
        if (!TextTargetCatalog.IsSafeReplacement(replacement, target.MaxLength))
            throw new InvalidOperationException($"Use {target.MaxLength} or fewer letters, spaces, or apostrophes for {target.OriginalText}.");

        string path = PlanPath(workspaceRoot, target);
        if (string.Equals(replacement, target.OriginalText, StringComparison.Ordinal))
        {
            Delete(workspaceRoot, target);
            return path;
        }

        LevelTextEditPlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            Kind: "exe-level-name-string-edit",
            LevelKey: target.LevelKey,
            LevelName: target.DisplayName,
            ScriptKey: target.ScriptKey,
            OriginalText: target.OriginalText,
            ReplacementText: replacement,
            MaxLength: target.MaxLength,
            TableKind: target.TableKind,
            TableIndex: target.TableIndex);

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? workspaceRoot);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(plan, JsonOptions), cancellationToken);
        return path;
    }

    public static void Delete(string workspaceRoot, TextTargetEntry target)
    {
        string path = PlanPath(workspaceRoot, target);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string GetString(JsonElement root, string propertyName, string fallback = "")
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? fallback
                : fallback;
        }

        return fallback;
    }

    private static DateTimeOffset GetDateTimeOffset(JsonElement root, string propertyName)
    {
        string value = GetString(root, propertyName);
        return DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : DateTimeOffset.MinValue;
    }
}

public sealed record LevelTextEditPlan(
    DateTimeOffset GeneratedAt,
    string Kind,
    string LevelKey,
    string LevelName,
    string ScriptKey,
    string OriginalText,
    string ReplacementText,
    int MaxLength,
    LevelTextTableKind TableKind,
    int TableIndex);
