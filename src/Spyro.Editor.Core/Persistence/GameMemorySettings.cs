using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's "Memory" choice for the games Create BIN makes (<see cref="GameMemoryMode"/>). Saved as
/// memory-mode.json in the project folder. No file means the default, Emulator (8 MB RAM) since 2.3 (a game made that
/// way still runs in 2 MB wherever its levels fit; until 2.2 no file meant PlayStation (2 MB)); PlayStation (2 MB), the
/// game's own layout for a console or a disc, is saved by name, which every version since 1.4 reads.</summary>
public static class GameMemorySettings
{
    public const string FileName = "memory-mode.json";
    public const int SchemaVersion = 1;
    private const string Emulator8MB = "emulator-8mb", PlayStation2MB = "playstation-2mb";
    /// <summary>What a project that never chose gets.</summary>
    public const GameMemoryMode Default = GameMemoryMode.Emulator8MB;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    public static GameMemoryMode Load(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return Default;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        return (root["memory"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? value) == true ? value switch
        {
            Emulator8MB => GameMemoryMode.Emulator8MB,
            PlayStation2MB => GameMemoryMode.PlayStation2MB,
            _ => throw new InvalidDataException($"{FileName} names an unknown memory setting \"{value}\"."),
        } : throw new InvalidDataException($"{FileName} has no memory setting.");
    }

    /// <summary>Saves the choice; the default removes the file, so a project that never chose is unchanged.</summary>
    public static void Save(string workspaceRoot, GameMemoryMode mode)
    {
        string path = PathFor(workspaceRoot);
        if (mode is not (GameMemoryMode.PlayStation2MB or GameMemoryMode.Emulator8MB)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == Default) { if (File.Exists(path)) File.Delete(path); return; }
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["memory"] = mode == GameMemoryMode.Emulator8MB ? Emulator8MB : PlayStation2MB };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
