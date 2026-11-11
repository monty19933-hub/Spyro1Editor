using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The project's colours for creatures, level by level, in the games Create BIN makes
/// (<see cref="NativeCreatureColours"/>): for each level, a colour for each kind that has one, the kind named by the
/// level it comes from and its class there ("artisans:72"). Saved as creature-colours.json in the project folder; no
/// file means every creature has its own colours.</summary>
public static class CreatureColourSettings
{
    public const string FileName = "creature-colours.json";
    public const int SchemaVersion = 1;

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    /// <summary>A kind's name in the file: the level its model comes from and its class there (hexadecimal).</summary>
    public static string KindKey(string sourceLevelKey, int classId) => $"{sourceLevelKey}:{classId:x}";

    public static bool TryParseKind(string key, out string sourceLevelKey, out int classId)
    {
        sourceLevelKey = ""; classId = 0;
        int at = key.LastIndexOf(':');
        if (at <= 0 || !int.TryParse(key.AsSpan(at + 1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out classId) || classId is < 0 or > 0xFFFF) return false;
        sourceLevelKey = key[..at];
        return true;
    }

    /// <summary>Level key -> kind -> colour (empty: none).</summary>
    public static Dictionary<string, Dictionary<string, (byte R, byte G, byte B)>> Load(string workspaceRoot)
    {
        Dictionary<string, Dictionary<string, (byte R, byte G, byte B)>> result = new(StringComparer.OrdinalIgnoreCase);
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return result;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if ((root["schemaVersion"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out int version) != true || version != SchemaVersion)
            throw new InvalidDataException($"{FileName} has an unknown version.");
        if (root["levels"] is not JsonObject levels) throw new InvalidDataException($"{FileName} has no levels.");
        foreach (var (level, value) in levels)
        {
            if (value is not JsonObject kinds) throw new InvalidDataException($"{FileName}: the colours of {level} are not a set of colours.");
            Dictionary<string, (byte R, byte G, byte B)> colours = new(StringComparer.OrdinalIgnoreCase);
            foreach (var (kind, colour) in kinds)
            {
                if (!TryParseKind(kind, out _, out _)) throw new InvalidDataException($"{FileName}: \"{kind}\" does not name a kind of creature.");
                string text = (colour as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? s) == true ? s : "";
                colours[kind] = SpyroColourSettings.TryParse(text, out var c) ? c : throw new InvalidDataException($"{FileName}: \"{text}\" is not a colour (#RRGGBB).");
            }
            if (colours.Count > 0) result[level] = colours;
        }
        return result;
    }

    /// <summary>Saves a level's colours (the other levels' stay); none removes the level, and with no level left the
    /// file, so a project that never changed them is unchanged.</summary>
    public static void SaveLevel(string workspaceRoot, string levelKey, IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        var all = Load(workspaceRoot);
        if (colours.Count == 0) all.Remove(levelKey); else all[levelKey] = new(colours, StringComparer.OrdinalIgnoreCase);
        string path = PathFor(workspaceRoot);
        if (all.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
        JsonObject levels = [];
        foreach (var (level, kinds) in all.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            JsonObject set = [];
            foreach (var (kind, colour) in kinds.OrderBy(k => k.Key, StringComparer.Ordinal)) set[kind] = SpyroColourSettings.Format(colour);
            levels[level] = set;
        }
        string temp = path + ".tmp";
        File.WriteAllText(temp, new JsonObject { ["schemaVersion"] = SchemaVersion, ["levels"] = levels }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
