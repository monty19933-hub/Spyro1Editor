using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's "Memory" choice for the games Create BIN makes (<see cref="GameMemoryMode"/>). Saved as
/// memory-mode.json in the project folder; no file means PlayStation (2 MB), the game's own layout.</summary>
public static class GameMemorySettings
{
    public const string FileName = "memory-mode.json";
    public const int SchemaVersion = 1;
    private const string Emulator8MB = "emulator-8mb", PlayStation2MB = "playstation-2mb";

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    public static GameMemoryMode Load(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return GameMemoryMode.PlayStation2MB;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        return (root["memory"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? value) == true ? value switch
        {
            Emulator8MB => GameMemoryMode.Emulator8MB,
            PlayStation2MB => GameMemoryMode.PlayStation2MB,
            _ => throw new InvalidDataException($"{FileName} names an unknown memory setting \"{value}\"."),
        } : throw new InvalidDataException($"{FileName} has no memory setting.");
    }

    /// <summary>Saves the choice; PlayStation (2 MB) removes the file, so a project that never chose 8 MB is unchanged.</summary>
    public static void Save(string workspaceRoot, GameMemoryMode mode)
    {
        string path = PathFor(workspaceRoot);
        if (mode == GameMemoryMode.PlayStation2MB) { if (File.Exists(path)) File.Delete(path); return; }
        if (mode != GameMemoryMode.Emulator8MB) throw new ArgumentOutOfRangeException(nameof(mode));
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["memory"] = Emulator8MB };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
