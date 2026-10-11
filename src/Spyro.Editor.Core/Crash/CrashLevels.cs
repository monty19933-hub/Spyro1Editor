namespace Spyro.Editor.Core.Crash;

/// <summary>What a Crash Bandicoot: Warped level is in its game.</summary>
public enum CrashLevelKind { Level, Hub, Boss }

/// <summary>A Crash Bandicoot: Warped (USA) level a Spyro the Dragon level can be made from: its name, its level file on
/// the disc and that file's SHA-256 as the release stores it (<see cref="CrashWarpedDisc.Level"/> refuses any other
/// bytes, so a conversion is always the same).</summary>
public sealed record CrashLevel(string Name, CrashLevelKind Kind, string File, string Sha256, string Notes)
{
    /// <summary>The name as a Spyro the Dragon level name: capitals, spaces and apostrophes only, at most 19 letters.</summary>
    public string SpyroName
    {
        get
        {
            string name = new([.. Name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or ' ' or '\'')]);
            name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return name.Length <= 19 ? name : name[..19].TrimEnd();
        }
    }
    public override string ToString() => Name;
}

/// <summary>The Crash Bandicoot: Warped levels whose terrain can become a Spyro the Dragon level: the levels Crash walks
/// through on foot (a path of zones with ground, walls and pits), the Time Twister warp room and the boss arenas. Levels
/// were told apart by their object sets and renders (2026-10-10); the vehicle, flying, water-ski and underwater levels
/// are left out (no ground to walk on), and so is N. Gin's arena (S0/S0000005.NSF has no scenery: the fight is drawn
/// with models).</summary>
public static class CrashLevels
{
    public static IReadOnlyList<CrashLevel> All { get; } =
    [
        new("Toad Village", CrashLevelKind.Level, "S0/S000000B.NSF", "f5744a2434bcbae0adf067232c0dddbcef0ad915aa59386c44abaae123cdd44c",
            "medieval village path, 27 zones, 9,385 faces in its main area"),
        new("Gee Wiz", CrashLevelKind.Level, "S0/S000000F.NSF", "35b60c0b49bbc4ea6d715b834deb1b34376f8a8ba3d91d3428433a352f100523",
            "medieval castle path, 37 zones, 13,489 faces (needs a part of it: a Spyro level holds 7,167)"),
        new("Double Header", CrashLevelKind.Level, "S1/S000001D.NSF", "10ae4ef48e49d8405a5bdaf63e328dd6a9303aef41db9e078135a824ad0481df",
            "medieval path, 36 zones, 13,148 faces (needs a part of it)"),
        new("Warp Room", CrashLevelKind.Hub, "S0/S0000002.NSF", "cbdf0dc7f4f237d295eb88548c28df4c5d1b3cfe4415db368a1daac23a4aecdc",
            "the Time Twister hub, 4,480 faces"),
        new("Dingodile's Arena", CrashLevelKind.Boss, "S0/S0000003.NSF", "70816fd9d1120572e1ef328cfa49265211759a4d63020c9d8af7dd3fba656eb9",
            "boss arena, 660 faces"),
        new("N. Tropy's Arena", CrashLevelKind.Boss, "S0/S0000004.NSF", "298e53d673800e15a9ff633771e2c8fa44580a7874aa44e75a0f796d89aa33b8",
            "boss arena, 3,025 faces (a tall tower: Spyro cannot climb its platforms)"),
        new("Tiny Tiger's Arena", CrashLevelKind.Boss, "S0/S0000006.NSF", "2e3de3a0565205baec0a47df142d0d52266de4373699aaffd4a3936d468ab9b9",
            "boss arena, 891 faces"),
        new("N. Cortex's Arena", CrashLevelKind.Boss, "S0/S0000007.NSF", "53024a97c3d2b577da65b4fc162e51cf9ca135861ab7528de523518ea24491a1",
            "boss arena, 486 faces"),
    ];

    /// <summary>The level of that name (case, spaces and punctuation ignored: "toad village", "TOADVILLAGE", "Tiny Tiger"),
    /// or by its level file ("S0/S000000B.NSF").</summary>
    public static CrashLevel Find(string name)
    {
        string Key(string s) => new([.. s.ToLowerInvariant().Replace("'s arena", "", StringComparison.Ordinal).Where(char.IsAsciiLetterOrDigit)]);
        return All.FirstOrDefault(l => string.Equals(l.File, name, StringComparison.OrdinalIgnoreCase))
            ?? All.FirstOrDefault(l => Key(l.Name) == Key(name))
            ?? throw new ArgumentException($"'{name}' is not a Crash Bandicoot: Warped level the editor can import. Levels: {string.Join(", ", All.Select(l => l.Name))}.");
    }

    /// <summary>The level's terrain read from the user's disc (the file checked against the release).</summary>
    public static CrashLevelData Read(CrashWarpedDisc disc, CrashLevel level) => CrashLevelData.Read(disc.Level(level.File, level.Sha256));
}
