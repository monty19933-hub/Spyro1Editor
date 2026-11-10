using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's colour for egg thieves in the games Create BIN makes (<see cref="NativeEggThiefColours"/>).
/// Saved as egg-thief-colour.json in the project folder; no file means their own blue.</summary>
public static class EggThiefColourSettings
{
    public const string FileName = "egg-thief-colour.json";
    public const int SchemaVersion = 1;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    public static (byte R, byte G, byte B)? Load(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return null;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if ((root["schemaVersion"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out int version) != true || version != SchemaVersion)
            throw new InvalidDataException($"{FileName} has an unknown version.");
        string text = (root["colour"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? s) == true ? s : "";
        return SpyroColourSettings.TryParse(text, out var colour) ? colour : throw new InvalidDataException($"{FileName}: \"{text}\" is not a colour (#RRGGBB).");
    }

    /// <summary>Saves the colour; none removes the file, so a project that never changed it is unchanged.</summary>
    public static void Save(string workspaceRoot, (byte R, byte G, byte B)? colour)
    {
        string path = PathFor(workspaceRoot);
        if (colour is not { } c) { if (File.Exists(path)) File.Delete(path); return; }
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["colour"] = SpyroColourSettings.Format(c) };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
