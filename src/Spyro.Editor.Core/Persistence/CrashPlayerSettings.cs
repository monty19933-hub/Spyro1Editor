using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's "Play as Crash" choice: the levels (by key) where Crash replaces Spyro in the exported game.
/// Saved as crash-player.json in the project folder.</summary>
public static class CrashPlayerSettings
{
    public const string FileName = "crash-player.json";
    public const int SchemaVersion = 1;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    public static IReadOnlySet<string> Load(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return keys;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if (root["levels"] is JsonArray levels)
            foreach (var level in levels)
                if (level?.GetValue<string>() is { Length: > 0 } key) keys.Add(key);
        return keys;
    }

    public static void Save(string workspaceRoot, IEnumerable<string> levelKeys)
    {
        var keys = levelKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        string path = PathFor(workspaceRoot);
        if (keys.Length == 0) { if (File.Exists(path)) File.Delete(path); return; }
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["levels"] = new JsonArray([.. keys.Select(k => (JsonNode?)System.Text.Json.Nodes.JsonValue.Create(k))]) };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
