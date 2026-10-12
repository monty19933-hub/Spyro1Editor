using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Editing;

/// <summary>The project's new world (File ▾ › New World…): a homeworld (level id 70) and up to four levels (71-74), each
/// a level you build like File ▾ › New Level's, saved as its own scene (<see cref="SceneDocument.WorldLevelId"/>). In the
/// game they take turns in the game's spare level slot. The world is entered by a doorway in an existing homeworld
/// (<see cref="EntryHomeworldKey"/>); its levels' Return Home leads to its homeworld.</summary>
public sealed record NewWorldDocument(int SchemaVersion, string Name, string EntryHomeworldKey, IReadOnlyList<int> LevelIds)
{
    public const string FileName = "new-world.json";
    public const int Homeworld = 70, MaxLevels = 4, MaxNameLength = 19;
    public static IReadOnlyList<int> LevelSlots { get; } = [71, 72, 73, 74];
    public static IReadOnlyList<string> HomeworldKeys { get; } = ["artisans", "peacekeepers", "magiccrafters", "beastmakers", "dreamweavers", "gnastysworld"];

    public static string KeyFor(int levelId) => $"world{levelId}";

    public static bool IsWorldKey(string? key, out int levelId)
    {
        levelId = 0;
        return key != null && key.StartsWith("world", StringComparison.Ordinal) && int.TryParse(key.AsSpan(5), out levelId) &&
            (levelId == Homeworld || LevelSlots.Contains(levelId)) && key == KeyFor(levelId);
    }

    /// <summary>The level-picker entry of a world level (it is built in the game's spare slot, like New Level's).</summary>
    public static LevelDefinition Target(int levelId, string name) => new()
    {
        Key = KeyFor(levelId), ScriptKey = $"World{levelId}", DisplayName = name,
        LevelId = levelId, SourceWadEntry = 80, SourceRecordCount = 0,
        Confidence = "authored neutral document; new world level in the spare level slot"
    };

    /// <summary>Capital letters, digits, spaces and apostrophes (the game's level-name font), at most 19.</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength && name.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '\'');

    public void Validate()
    {
        if (SchemaVersion != 1 || !IsValidName(Name) || !HomeworldKeys.Contains(EntryHomeworldKey) ||
            LevelIds.Count is < 1 or > MaxLevels + 1 || LevelIds[0] != Homeworld ||
            LevelIds.Skip(1).Any(id => !LevelSlots.Contains(id)) || LevelIds.Distinct().Count() != LevelIds.Count)
            throw new InvalidDataException("The project's new world is not valid: a name, an entry homeworld, homeworld 70 and up to four levels 71-74.");
    }

    public static string PathFor(string workspace) => Path.Combine(workspace, FileName);

    public static NewWorldDocument? Read(string workspace)
    {
        string path = PathFor(workspace);
        if (!File.Exists(path)) return null;
        var world = JsonSerializer.Deserialize<NewWorldDocument>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("The new world file is empty.");
        world.Validate();
        return world;
    }

    public async Task SaveAsync(string workspace)
    {
        Validate();
        string path = PathFor(workspace), temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
