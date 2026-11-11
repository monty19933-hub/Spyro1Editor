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

    /// <summary>The whole game's look and the looks of levels that have their own (level key -> part -> colour; an empty
    /// look: his own colours in that level).</summary>
    public sealed record Book(Dictionary<string, (byte R, byte G, byte B)> Whole, Dictionary<string, Dictionary<string, (byte R, byte G, byte B)>> Levels);

    /// <summary>Part key -> colour for every part the project changes in the whole game (empty: none).</summary>
    public static Dictionary<string, (byte R, byte G, byte B)> Load(string workspaceRoot) => LoadAll(workspaceRoot).Whole;

    /// <summary>The whole game's look and each level's own.</summary>
    public static Book LoadAll(string workspaceRoot)
    {
        string path = PathFor(workspaceRoot);
        if (!File.Exists(path)) return new([], new(StringComparer.OrdinalIgnoreCase));
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException($"{FileName} is not a settings object.");
        if ((root["schemaVersion"] as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out int version) != true || version != SchemaVersion)
            throw new InvalidDataException($"{FileName} has an unknown version.");
        if (root["colours"] is not JsonObject colours) throw new InvalidDataException($"{FileName} has no colours.");
        static Dictionary<string, (byte, byte, byte)> Look(JsonObject parts)
        {
            Dictionary<string, (byte, byte, byte)> result = [];
            foreach (var (key, value) in parts)
            {
                if (NativeSpyroColours.FindPart(key) == null) throw new InvalidDataException($"{FileName} names a part Spyro doesn't have: {key}.");
                string text = (value as System.Text.Json.Nodes.JsonValue)?.TryGetValue(out string? s) == true ? s : "";
                result[key] = TryParse(text, out var colour) ? colour : throw new InvalidDataException($"{FileName}: \"{text}\" is not a colour (#RRGGBB).");
            }
            return result;
        }
        Dictionary<string, Dictionary<string, (byte R, byte G, byte B)>> levels = new(StringComparer.OrdinalIgnoreCase);
        if (root["levels"] is JsonObject saved)
            foreach (var (key, value) in saved)
                levels[key] = value is JsonObject parts ? Look(parts) : throw new InvalidDataException($"{FileName}: the look of {key} is not a set of colours.");
        return new(Look(colours), levels);
    }

    /// <summary>Saves the whole game's colours (the levels' own looks stay); with no colours anywhere the file is
    /// removed, so a project that never changed them is unchanged.</summary>
    public static void Save(string workspaceRoot, IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        Book book;
        try { book = LoadAll(workspaceRoot); }
        catch (Exception e) when (e is InvalidDataException or JsonException) { book = new([], new(StringComparer.OrdinalIgnoreCase)); }
        SaveAll(workspaceRoot, book with { Whole = new(colours) });
    }

    /// <summary>Gives a level its own look (<paramref name="colours"/> empty: his own colours there), or (null) takes it
    /// away again so the level has the whole game's.</summary>
    public static void SaveLevel(string workspaceRoot, string levelKey, IReadOnlyDictionary<string, (byte R, byte G, byte B)>? colours)
    {
        var book = LoadAll(workspaceRoot);
        if (colours == null) book.Levels.Remove(levelKey); else book.Levels[levelKey] = new(colours);
        SaveAll(workspaceRoot, book);
    }

    public static void SaveAll(string workspaceRoot, Book book)
    {
        foreach (string key in book.Whole.Keys.Concat(book.Levels.Values.SelectMany(l => l.Keys)))
            if (NativeSpyroColours.FindPart(key) == null) throw new ArgumentException($"Spyro has no part called \"{key}\".");
        string path = PathFor(workspaceRoot);
        if (book.Whole.Count == 0 && book.Levels.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
        static JsonObject Parts(IReadOnlyDictionary<string, (byte R, byte G, byte B)> look)
        {
            JsonObject parts = [];
            foreach (var part in NativeSpyroColours.Parts)
                if (look.TryGetValue(part.Key, out var c)) parts[part.Key] = Format(c);
            return parts;
        }
        // "colours" is always written: editors before 2.1 read it and nothing else.
        var root = new JsonObject { ["schemaVersion"] = SchemaVersion, ["colours"] = Parts(book.Whole) };
        if (book.Levels.Count > 0)
        {
            JsonObject levels = [];
            foreach (var (key, look) in book.Levels.OrderBy(l => l.Key, StringComparer.Ordinal)) levels[key] = Parts(look);
            root["levels"] = levels;
        }
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
