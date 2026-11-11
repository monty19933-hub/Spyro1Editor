using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spyro.Editor.Core.Persistence;

/// <summary>The parts of a project Create BIN can make a game without (Fix It For Me's last resort): each has an id that
/// <see cref="ExportLeftOutSettings"/> remembers.</summary>
public static class ExportParts
{
    /// <summary>The project's new world: its levels, the way in and its place on the balloonists' route.</summary>
    public const string World = "world";
    /// <summary>Saved changes to the game's own levels that the game cannot take (Create BIN names them each time).</summary>
    public const string SkippedChanges = "changes-the-game-cannot-take";
    public const string Hover = "setting:hover", SpyroColours = "setting:spyro-colours", CreatureColours = "setting:creature-colours",
        EggThieves = "setting:egg-thieves", FlyAnywhere = "setting:fly-anywhere", Doorways = "setting:doorways", PlayAsCrash = "setting:play-as-crash";
    /// <summary>The kinds of saved change to one of the game's own levels.</summary>
    public const string Objects = "objects", Terrain = "terrain", Sky = "sky", Music = "music", Name = "name";
    public static readonly IReadOnlyList<string> LevelKinds = [Objects, Terrain, Sky, Music, Name];

    /// <summary>Every saved change to one of the game's own levels.</summary>
    public static string Level(string key) => "level:" + key.ToLowerInvariant();
    /// <summary>One kind of saved change to one of the game's own levels.</summary>
    public static string Level(string key, string kind) => Level(key) + ":" + kind;
    /// <summary>One kind of object added to one of the game's own levels (every copy of it): the narrowest part.
    /// <paramref name="kind"/> is <c>UniversalPlacementReader.KindOf</c>.</summary>
    public static string Added(string key, string kind) => AddedPrefix(key) + kind;
    public static string AddedPrefix(string key) => Level(key) + ":added:";
    /// <summary>A level built in the place of <paramref name="key"/> (the new level's key is <c>authored65</c>).</summary>
    public static string Built(string key) => "built:" + key.ToLowerInvariant();
}

/// <summary>What Fix It For Me left out of the project's games (<c>left-out.json</c>): when Create BIN stops on
/// something the editor has no repair for, it makes the game without the part that stops it and remembers the part here,
/// so the next Create BIN does not stop there again. Nothing is deleted from the project; putting a part back is
/// removing its line. A list written by another version of the editor is not used: that version's problem may be gone,
/// so everything is tried again.</summary>
public static class ExportLeftOutSettings
{
    public const string FileName = "left-out.json";
    public const int SchemaVersion = 1;

    /// <summary>One part left out: its id (<see cref="ExportParts"/>), the refusal that led to it and when.</summary>
    public sealed record Item(string Part, string Why, DateTimeOffset When);

    public static string PathFor(string workspaceRoot) => Path.Combine(workspaceRoot, FileName);

    /// <summary>The parts this version of the editor left out (none: no file, another version's, or unreadable).</summary>
    public static IReadOnlyList<Item> Load(string workspaceRoot, string editorVersion)
    {
        string path = PathFor(workspaceRoot);
        if (string.IsNullOrEmpty(workspaceRoot) || !File.Exists(path)) return [];
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root || root["schemaVersion"]?.GetValue<int>() != SchemaVersion ||
                !string.Equals(root["editor"]?.GetValue<string>(), editorVersion, StringComparison.Ordinal) || root["items"] is not JsonArray items)
                return [];
            List<Item> result = [];
            foreach (var node in items)
            {
                if (node is not JsonObject item || item["part"]?.GetValue<string>() is not { Length: > 0 } part) continue;
                if (result.Any(i => i.Part == part)) continue;
                DateTimeOffset when = DateTimeOffset.TryParse(item["when"]?.GetValue<string>(), out var parsed) ? parsed : DateTimeOffset.MinValue;
                result.Add(new(part, item["why"]?.GetValue<string>() ?? "", when));
            }
            return result;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        { return []; }
    }

    /// <summary>Writes the list (no parts: the file is removed).</summary>
    public static void Save(string workspaceRoot, string editorVersion, IReadOnlyList<Item> items)
    {
        string path = PathFor(workspaceRoot);
        if (items.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
        JsonObject root = new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["editor"] = editorVersion,
            ["note"] = "Parts of this project that Fix It For Me leaves out of the games Create BIN makes. Nothing was deleted; remove a line (or use More › Left Out of the Game…) to put a part back.",
            ["items"] = new JsonArray(items.Select(i => (JsonNode)new JsonObject { ["part"] = i.Part, ["why"] = i.Why, ["when"] = i.When.ToString("O") }).ToArray()),
        };
        string pending = path + ".pending";
        File.WriteAllText(pending, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(pending, path, overwrite: true);
    }
}
