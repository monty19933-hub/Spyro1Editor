using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Editing;

/// <summary>The new world's place on the balloonists' route (World Settings): the homeworld it follows
/// (<see cref="After"/>, or <see cref="Off"/>: the balloonists do not know the world, it is reached through its doorway
/// only), what that homeworld's balloonist asks before he flies Spyro to the world, what the world's own balloonist asks
/// before he flies Spyro on to the next world, and the world's balloonist's name (the talk's heading "… THE
/// BALLOONIST").</summary>
public sealed record WorldBalloonPlace(string After, BalloonRequirement ToWorld, BalloonRequirement Onward, string Balloonist = WorldBalloonPlace.DefaultBalloonist)
{
    public const string Off = "off", DefaultBalloonist = "NILS";
    public const int MaxBalloonistName = 10;
    /// <summary>A world saved without a place (made before the balloonists knew new worlds): last, after Gnasty's World,
    /// where nothing of the game's own route changes and nothing can be asked.</summary>
    public static WorldBalloonPlace Default { get; } = new("gnastysworld", BalloonRequirement.Nothing, BalloonRequirement.Nothing);

    [JsonIgnore] public bool OnRoute => After != Off;

    /// <summary>The route for Create BIN; null when the world is not on it.</summary>
    public BalloonRoute? Route() => OnRoute ? new((NewWorldDocument.HomeworldKeys.ToList().IndexOf(After) + 1) * 10, ToWorld, Onward) : null;

    /// <summary>What the homeworld <paramref name="key"/>'s balloonist asks in the game itself before he flies on (the
    /// price the new world's balloonist takes over by default when the world follows that homeworld).</summary>
    public static BalloonRequirement GamePrice(string key) =>
        NewWorldDocument.HomeworldKeys.ToList().IndexOf(key) is >= 0 and < 5 and var w ? NativeBalloonTravel.Retail[w] : BalloonRequirement.Nothing;

    /// <summary>The place a new world gets by default: after its entry homeworld; that homeworld's balloonist flies
    /// Spyro there for nothing, and the world's balloonist asks what the game asks there before flying on, so the rest
    /// of the game opens up exactly as fast as it did.</summary>
    public static WorldBalloonPlace Following(string homeworldKey) =>
        new(homeworldKey, BalloonRequirement.Nothing, GamePrice(homeworldKey));

    public string? Refusal()
    {
        if (!OnRoute) return null;
        if (!NewWorldDocument.HomeworldKeys.Contains(After)) return "The world follows one of the game's six homeworlds on the balloon route, or is off it.";
        if (Route()!.Refusal() is { } why) return why;
        if (string.IsNullOrEmpty(Balloonist) || Balloonist.Length > MaxBalloonistName || Balloonist.Any(c => c is not (>= 'A' and <= 'Z')))
            return $"The balloonist's name is 1 to {MaxBalloonistName} capital letters.";
        return null;
    }
}

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
    /// <summary>The world's place on the balloonists' route as chosen in File ▾ › World's Balloon Route…; null: none was
    /// chosen (<see cref="BalloonPlaceWith"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorldBalloonPlace? Balloon { get; init; }

    /// <summary>The world's place on the route. When none was chosen: after the homeworld its doorway is in, with the
    /// game's own pace (<see cref="WorldBalloonPlace.Following"/>), if its homeworld has a balloonist to fly Spyro on;
    /// otherwise last, after Gnasty's World, where it needs none (a world made before the balloonists knew new worlds
    /// has no balloonist).</summary>
    public WorldBalloonPlace BalloonPlaceWith(bool homeworldHasBalloonist) =>
        Balloon ?? (homeworldHasBalloonist ? WorldBalloonPlace.Following(EntryHomeworldKey) : WorldBalloonPlace.Default);

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
        if (Balloon?.Refusal() is { } why) throw new InvalidDataException("The world's place on the balloon route is not valid: " + why);
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
