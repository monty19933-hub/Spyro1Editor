using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's colours for Spyro in the games Create BIN makes (<see cref="NativeSpyroColours"/>): a colour per
/// changed part. Saved as spyro-colours.json in the project folder; no file means his own colours.</summary>
public static class SpyroColourSettings
{
    public const string FileName = "spyro-colours.json";
    public const int SchemaVersion = 1;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    /// <summary>Part key -> colour for every part the project changes (empty: none).</summary>
    public static Dictionary<string, (byte R, byte G, byte B)> Load(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return [];
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if ((root["schemaVersion"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out int version) != true || version != SchemaVersion)
            throw new InvalidDataException($"{FileName} has an unknown version.");
        if (root["colours"] is not JsonObject colours) throw new InvalidDataException($"{FileName} has no colours.");
        Dictionary<string, (byte, byte, byte)> result = [];
        foreach (var (key, value) in colours)
        {
            if (NativeSpyroColours.FindPart(key) == null) throw new InvalidDataException($"{FileName} names a part Spyro doesn't have: {key}.");
            string text = (value as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? s) == true ? s : "";
            result[key] = TryParse(text, out var colour) ? colour : throw new InvalidDataException($"{FileName}: \"{text}\" is not a colour (#RRGGBB).");
        }
        return result;
    }

    /// <summary>Saves the colours; none removes the file, so a project that never changed them is unchanged.</summary>
    public static void Save(string workspaceRoot, IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        foreach (string key in colours.Keys)
            if (NativeSpyroColours.FindPart(key) == null) throw new ArgumentException($"Spyro has no part called \"{key}\".");
        string path = PathFor(workspaceRoot);
        if (colours.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
        JsonObject parts = [];
        foreach (var part in NativeSpyroColours.Parts)
            if (colours.TryGetValue(part.Key, out var c)) parts[part.Key] = Format(c);
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["colours"] = parts };
        string temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }

    public static string Format((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool TryParse(string? text, out (byte R, byte G, byte B) colour)
    {
        colour = default;
        text = text?.Trim();
        if (text is not { Length: 7 } || text[0] != '#' ||
            !int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
        colour = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}
