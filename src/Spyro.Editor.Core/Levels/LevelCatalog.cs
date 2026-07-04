using System.Text.Json;
using Spyro.Editor.Core;

namespace Spyro.Editor.Core.Levels;

public sealed class LevelCatalog
{
    private readonly IReadOnlyList<LevelDefinition> _levels;
    private readonly Dictionary<string, LevelDefinition> _byKey;

    public LevelCatalog(IEnumerable<LevelDefinition> levels)
    {
        _levels = levels.Where(level => !string.IsNullOrWhiteSpace(level.Key)).ToArray();
        _byKey = _levels.ToDictionary(level => NormalizeKey(level.Key), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<LevelDefinition> Levels => _levels;

    public LevelDefinition? FindByKey(string key)
    {
        _byKey.TryGetValue(NormalizeKey(key), out LevelDefinition? level);
        return level;
    }

    public int SourceRecordCountForKey(string key)
    {
        return FindByKey(key)?.SourceRecordCount ?? 0;
    }

    public static LevelCatalog Load(string workspacePath)
    {
        string? path = FindReadableCatalogPath(workspacePath);
        if (string.IsNullOrWhiteSpace(path))
            return new LevelCatalog(Array.Empty<LevelDefinition>());

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("levels", out JsonElement levelsElement) || levelsElement.ValueKind != JsonValueKind.Array)
            return new LevelCatalog(Array.Empty<LevelDefinition>());

        List<LevelDefinition> levels = new();
        foreach (JsonElement item in levelsElement.EnumerateArray())
        {
            levels.Add(new LevelDefinition
            {
                Key = JsonValue.GetString(item, "key"),
                ScriptKey = JsonValue.GetString(item, "scriptKey"),
                DisplayName = JsonValue.GetString(item, "displayName"),
                LevelId = JsonValue.GetInt32(item, "levelId", -1),
                SourceWadEntry = JsonValue.GetInt32(item, "sourceWadEntry", -1),
                SourceTableWadOffset = JsonValue.GetString(item, "sourceTableWadOffset"),
                SourceTableRelativeOffset = JsonValue.GetString(item, "sourceTableRelativeOffset"),
                SourceRecordCount = JsonValue.GetInt32(item, "sourceRecordCount", 0),
                Confidence = JsonValue.GetString(item, "confidence"),
                RuntimeMobyPointer = JsonValue.GetString(item, "runtimeMobyPointer")
            });
        }

        return new LevelCatalog(levels);
    }

    public static string NormalizeKey(string value)
    {
        return (value ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string? FindReadableCatalogPath(string workspacePath)
    {
        string[] candidates =
        [
            Path.Combine(workspacePath, "spyro-level-catalog.json"),
            Path.Combine(workspacePath, "support", "spyro-level-catalog.json")
        ];

        foreach (string candidate in candidates)
        {
            if (CanRead(candidate))
                return candidate;
        }

        return null;
    }

    private static bool CanRead(string path)
    {
        try
        {
            using FileStream _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
