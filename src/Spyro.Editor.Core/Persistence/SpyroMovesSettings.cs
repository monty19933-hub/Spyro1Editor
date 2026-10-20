using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's choice of moves from later Spyro games for the games Create BIN makes (today: Spyro 3's hover,
/// <see cref="Exporting.NativeHover"/>). Saved as spyro-moves.json in the project folder; no file means none, the
/// original game's moves.</summary>
public static class SpyroMovesSettings
{
    public const string FileName = "spyro-moves.json";
    public const int SchemaVersion = 1;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    /// <summary>Whether the games Create BIN makes have Spyro 3's hover.</summary>
    public static bool LoadHover(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return false;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if ((root["schemaVersion"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out int version) != true || version != SchemaVersion)
            throw new InvalidDataException($"{FileName} has an unknown version.");
        return (root["hover"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out bool hover) == true ? hover
            : throw new InvalidDataException($"{FileName} has no hover setting.");
    }

    /// <summary>Saves the choice; off removes the file, so a project that never turned a move on is unchanged.</summary>
    public static void SaveHover(string workspaceRoot, bool hover)
    {
        string path = PathFor(workspaceRoot);
        if (!hover) { if (File.Exists(path)) File.Delete(path); return; }
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["hover"] = true };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
