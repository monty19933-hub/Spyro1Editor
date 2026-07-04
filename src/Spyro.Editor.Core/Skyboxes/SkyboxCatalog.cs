using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Skyboxes;

public sealed class SkyboxCatalog
{
    private readonly IReadOnlyList<SkyboxLevelEntry> _levels;
    private readonly Dictionary<string, SkyboxLevelEntry> _byKey;

    public SkyboxCatalog(IEnumerable<SkyboxLevelEntry> levels, string sourcePath)
    {
        SourcePath = sourcePath;
        _levels = levels.ToArray();
        _byKey = new Dictionary<string, SkyboxLevelEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (SkyboxLevelEntry level in _levels)
        {
            AddKey(level.Key, level);
            AddKey(level.ScriptKey, level);
            AddKey(level.DisplayName, level);
        }
    }

    public string SourcePath { get; }

    public IReadOnlyList<SkyboxLevelEntry> Levels => _levels;

    public bool HasData => _levels.Count > 0;

    public SkyboxLevelEntry? FindForLevel(string key)
    {
        _byKey.TryGetValue(LevelCatalog.NormalizeKey(key), out SkyboxLevelEntry? entry);
        return entry;
    }

    public static SkyboxCatalog Load(EditorWorkspace workspace)
    {
        string path = workspace.ResolveFile("spyro-skybox-catalog.json", "generated-research");
        if (!File.Exists(path))
            return new SkyboxCatalog(Array.Empty<SkyboxLevelEntry>(), path);

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("levels", out JsonElement levelsElement) || levelsElement.ValueKind != JsonValueKind.Array)
            return new SkyboxCatalog(Array.Empty<SkyboxLevelEntry>(), path);

        List<SkyboxLevelEntry> levels = new();
        foreach (JsonElement item in levelsElement.EnumerateArray())
        {
            levels.Add(new SkyboxLevelEntry(
                Key: JsonValue.GetString(item, "key"),
                ScriptKey: JsonValue.GetString(item, "scriptKey"),
                DisplayName: JsonValue.GetString(item, "displayName"),
                Status: JsonValue.GetString(item, "status"),
                AssetWadEntry: JsonValue.GetInt32(item, "assetWadEntry", -1),
                MetadataWadEntry: JsonValue.GetInt32(item, "metadataWadEntry", -1),
                SkySubfileIndex: JsonValue.GetInt32(item, "skySubfileIndex", -1),
                SkySubfileSize: JsonValue.GetInt32(item, "skySubfileSize", 0),
                SkyWadOffset: JsonValue.GetString(item, "skyWadOffset"),
                SkyImageOffset: JsonValue.GetString(item, "skyImageOffset"),
                SkySha1: JsonValue.GetString(item, "skySha1"),
                CompatibleDonorNames: ReadStringArray(item, "compatibleDonorNames"),
                Notes: ReadStringArray(item, "notes")));
        }

        return new SkyboxCatalog(levels, path);
    }

    private void AddKey(string value, SkyboxLevelEntry level)
    {
        string key = LevelCatalog.NormalizeKey(value);
        if (!string.IsNullOrWhiteSpace(key))
            _byKey[key] = level;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }
}

public sealed record SkyboxLevelEntry(
    string Key,
    string ScriptKey,
    string DisplayName,
    string Status,
    int AssetWadEntry,
    int MetadataWadEntry,
    int SkySubfileIndex,
    int SkySubfileSize,
    string SkyWadOffset,
    string SkyImageOffset,
    string SkySha1,
    IReadOnlyList<string> CompatibleDonorNames,
    IReadOnlyList<string> Notes);
