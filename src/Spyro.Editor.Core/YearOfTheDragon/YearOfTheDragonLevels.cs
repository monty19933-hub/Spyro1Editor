using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>What a Spyro: Year of the Dragon level is in its game.</summary>
public enum YearOfTheDragonLevelKind { Homeworld, Level, Speedway, Boss, Bonus }

/// <summary>A Spyro: Year of the Dragon level (USA, Rev 1): its place in the game's level order (the world id, the order of
/// the executable's level-name table and of WAD.WAD), its name, the WAD entry holding its level data (entry 97 + 2 x
/// index; the code overlay is the entry after it) and the SHA-256 of that entry as the release stores it. The importer
/// rebuilds its main area (the area Spyro enters first; the sub-areas a level's characters and challenges use are later
/// sub-files and are not imported).</summary>
public sealed record YearOfTheDragonLevel(int Index, string Name, YearOfTheDragonLevelKind Kind, int WadEntry, string EntrySha256)
{
    /// <summary>The name as a Spyro the Dragon level name: capitals, spaces and apostrophes only, at most 19 letters
    /// ("Sgt. Byrd's Base" becomes SGT BYRD'S BASE, "Agent 9's Lab" AGENT NINE'S LAB, a homeworld loses "Home").</summary>
    public string SpyroName
    {
        get
        {
            string name = Name.EndsWith(" Home", StringComparison.Ordinal) ? Name[..^5] : Name;
            name = name.Replace("9", "NINE", StringComparison.Ordinal).ToUpperInvariant();
            name = new string([.. name.Where(c => c is >= 'A' and <= 'Z' or ' ' or '\'')]);
            name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return name.Length <= 19 ? name : name[..19].TrimEnd();
        }
    }
    public override string ToString() => Name;
}

/// <summary>The 37 levels of Spyro: Year of the Dragon (USA, Rev 1, SCUS-94467), read from the user's disc: the executable's
/// level-name table (37 pointers at file offset 0x5A5D8, pinned by the SHA-256 of the names) gives the order and names;
/// each level's data is WAD entry 97 + 2 x index (pinned per level). Kinds follow the game: the four homeworlds,
/// the four speedways, the four boss arenas plus the final battle, and the Super Bonus Round.</summary>
public static class YearOfTheDragonLevels
{
    public const int NameTableOffset = 0x5A5D8, Count = 37, FirstDataEntry = 97;
    /// <summary>SHA-256 of the 37 names joined by line feeds (Latin-1).</summary>
    public const string NamesSha256 = "04b9452a794cc22e9e221074120df4bd71a2aece1b4cbc7e33a7999f27f61805";
    private const uint ExeLoadAddress = 0x80010000, ExeHeaderBytes = 0x800;

    private static readonly (string Name, YearOfTheDragonLevelKind Kind, string Sha)[] Release =
    [
        ("Sunrise Spring Home", YearOfTheDragonLevelKind.Homeworld, "7527bd4f0e4787c981bbf6c6ab38995d3aa6b72e64f135a1e3d3b03f9d799305"),
        ("Sunny Villa", YearOfTheDragonLevelKind.Level, "7a3bf789a06144f91a00a63a93a1b7af2c2d1eafc296f843674ffe5ee389a4f4"),
        ("Cloud Spires", YearOfTheDragonLevelKind.Level, "4ae0171b088a1590a5a3ec3b6955e5522330444a5fbd34c3f00da90f7178d606"),
        ("Molten Crater", YearOfTheDragonLevelKind.Level, "5cd3968d5c429d04f6f8f52c6ebb16961a438258adf4f031bd9916794f8d6e82"),
        ("Seashell Shore", YearOfTheDragonLevelKind.Level, "15cd971c1b82530cb24e567c5f7b51e6ce8d125962dafaad5f3ff1364bec3fc8"),
        ("Mushroom Speedway", YearOfTheDragonLevelKind.Speedway, "667662b70213810e9f8af4d03fcd839b9abb52946277bb48755064c3de9f49c4"),
        ("Sheila's Alp", YearOfTheDragonLevelKind.Level, "e4bf1e6c1c7f02ec3f85e97f736bc685259ed2342ac225d3a4d3a154532f49a8"),
        ("Buzz's Dungeon", YearOfTheDragonLevelKind.Boss, "cdedccc5804da150c9e9235f28c565c5750b81653d30ee6c777deae474f8ef20"),
        ("Crawdad Farm", YearOfTheDragonLevelKind.Level, "8653936fd6a1d8e1465a0edbf6d3349c60de414ff53d87d04675f2b437589212"),
        ("Midday Garden Home", YearOfTheDragonLevelKind.Homeworld, "cc36f5933533ff1bf3676ccc0e1eaa2f830346f3d680639dae8057cad9eef6f7"),
        ("Icy Peak", YearOfTheDragonLevelKind.Level, "b0f2fdce1f02e7f35c6e09589f1a532082d0e67cceaf9bdb440ce2a64fddc91f"),
        ("Enchanted Towers", YearOfTheDragonLevelKind.Level, "54a49f27ae36176f08f9907e4a70e7d879c4eb3818312f688efef13dc98af806"),
        ("Spooky Swamp", YearOfTheDragonLevelKind.Level, "a7c1a31ab925a37f333e5b3a6abfcaa45c665c3446b82b01459584b503f09e5d"),
        ("Bamboo Terrace", YearOfTheDragonLevelKind.Level, "1916fd9d6b50aa9fc4bb6f7b605a11939dfe35125e1f8bb9d739a84a2aa7c14f"),
        ("Country Speedway", YearOfTheDragonLevelKind.Speedway, "049bc262e8e4446ac6aad5becbfd19371712bfcc4739c9d25a64184a4fb09b4d"),
        ("Sgt. Byrd's Base", YearOfTheDragonLevelKind.Level, "fa60a750f088c79761c16a950ed17ddb551ccc7934c492178cd370b0297a9aa0"),
        ("Spike's Arena", YearOfTheDragonLevelKind.Boss, "bb05e2e68ae3af0cfa3e9fcefc9cc7ea253c467539fcdd1469b80c89b4091928"),
        ("Spider Town", YearOfTheDragonLevelKind.Level, "280ab365d513a0f80098fb3b0b935e5e5396a19454d06a7a6a38610944866d96"),
        ("Evening Lake Home", YearOfTheDragonLevelKind.Homeworld, "c30d596f6998ff8701ff2992d4830913c0bc75d615da2918f84490203787df31"),
        ("Frozen Altars", YearOfTheDragonLevelKind.Level, "3a2852b2b93e3856cfbb8c10b6c8b512bbf53ffe41a824568ebbb53414a87e26"),
        ("Lost Fleet", YearOfTheDragonLevelKind.Level, "b21003d81198cf8c84e3c22ebf0b4f29ade56b28fcef8c14a50edf62b3b3794f"),
        ("Fireworks Factory", YearOfTheDragonLevelKind.Level, "9405f8c16b9030d3bcf0548f7ac89cbd128c585971c81fcb6a3ec9a3ab0b39af"),
        ("Charmed Ridge", YearOfTheDragonLevelKind.Level, "0c3f0b03f1f6586a23c07b7db8ba5260453490cd203fe265ffd1597eecfcc17d"),
        ("Honey Speedway", YearOfTheDragonLevelKind.Speedway, "142cb7211ac86ab3fbbf10bb380b2ddb875e29cea7a3f167271a26d0c0c6ea3a"),
        ("Bentley's Outpost", YearOfTheDragonLevelKind.Level, "8f8452d58750031a0cfb3704778b5e0f2ad462bb5c3965dec73c7dabb1bb87a7"),
        ("Scorch's Pit", YearOfTheDragonLevelKind.Boss, "cb627054fc4b1e3bc8638901e2f48e78cb98decf353b3fd3024ea5b80fc423a2"),
        ("Starfish Reef", YearOfTheDragonLevelKind.Level, "4347b5e16b93cb2f858c3765ff3c22431aae32dd00e7a8a001b15ae9f20b63d7"),
        ("Midnight Mountain Home", YearOfTheDragonLevelKind.Homeworld, "fb41304f3601181d2b52d0762c7980380865cdb56dee5208001124dbcdeaf7f2"),
        ("Crystal Islands", YearOfTheDragonLevelKind.Level, "885234c61cc18cb5cc3858fe5295e4f577d5ba8f76fff0788b8c76ee691272ff"),
        ("Desert Ruins", YearOfTheDragonLevelKind.Level, "3d252c881194127d01910ee2452edd5d7db27af99ded23a89487c8739e87d0d9"),
        ("Haunted Tomb", YearOfTheDragonLevelKind.Level, "88afbb5e25abbb38324393ef032fd19b70a8f94696a068e2169bf0ce29e9ae14"),
        ("Dino Mines", YearOfTheDragonLevelKind.Level, "a8e329249117bdb746d65d1686559ba53232eeacf2c6ce64bcde35cf83c18498"),
        ("Harbor Speedway", YearOfTheDragonLevelKind.Speedway, "44f33f2a824992529e67e58c3d6f24fadc8c31ae4c4379f5c55c6ccaa65cf4f5"),
        ("Agent 9's Lab", YearOfTheDragonLevelKind.Level, "28a61b31ade2788ba8d1529303352838358d467637f60b85e6aba8abc99d4608"),
        ("Sorceress's Lair", YearOfTheDragonLevelKind.Boss, "03e15bb4b0dc250d198362ca8300a8e84bd93db50379523b91b046f48dd4ea47"),
        ("Bugbot Factory", YearOfTheDragonLevelKind.Boss, "e4d5c1d3fc5b5603979eccceb26ffde24792cc25d202250894b377c7c0c6bb53"),
        ("Super Bonus Round", YearOfTheDragonLevelKind.Bonus, "2be9c182bb6a0bdb9bab288532f0092ddf129ea5b3aefc8af6ec7cc75d455489"),
    ];

    /// <summary>The release's levels (no disc needed, for lists; <see cref="Read"/> checks them against a disc).</summary>
    public static IReadOnlyList<YearOfTheDragonLevel> All { get; } =
        [.. Release.Select((r, i) => new YearOfTheDragonLevel(i, r.Name, r.Kind, FirstDataEntry + 2 * i, r.Sha))];

    /// <summary>The level of that name (case, punctuation and a trailing "Home" ignored: "lost fleet", "LOSTFLEET",
    /// "Sunrise Spring"), or by its index ("20").</summary>
    public static YearOfTheDragonLevel Find(string name)
    {
        string Key(string s) => new([.. s.ToLowerInvariant().Replace(" home", "", StringComparison.Ordinal).Where(char.IsAsciiLetterOrDigit)]);
        if (int.TryParse(name, out int index) && index >= 0 && index < Count) return All[index];
        return All.FirstOrDefault(l => Key(l.Name) == Key(name))
            ?? throw new ArgumentException($"'{name}' is not a Spyro: Year of the Dragon level. Levels: {string.Join(", ", All.Select(l => l.Name))}.");
    }

    /// <summary>The level names as the disc's executable lists them; throws when they differ from the release.</summary>
    public static IReadOnlyList<YearOfTheDragonLevel> Read(YearOfTheDragonDisc disc)
    {
        var names = ReadNames(disc.Executable());
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(string.Join('\n', names)))) != NamesSha256)
            throw new InvalidDataException("This Spyro: Year of the Dragon disc's level list differs from the USA Rev 1 release.");
        return All;
    }

    /// <summary>The 37 names of the executable's level-name table.</summary>
    public static IReadOnlyList<string> ReadNames(byte[] exe)
    {
        if (exe.Length < NameTableOffset + 4 * Count) throw new InvalidDataException("The Spyro: Year of the Dragon executable is too short.");
        List<string> names = [];
        for (int i = 0; i < Count; i++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(NameTableOffset + 4 * i));
            long at = pointer - ExeLoadAddress + ExeHeaderBytes;
            if (at < ExeHeaderBytes || at >= exe.Length) throw new InvalidDataException("The level-name table does not point into the executable.");
            int end = Array.IndexOf(exe, (byte)0, (int)at);
            if (end < 0 || end - at > 40) throw new InvalidDataException("A level name is not terminated.");
            names.Add(Encoding.Latin1.GetString(exe, (int)at, end - (int)at));
        }
        return names;
    }

    /// <summary>The level's data entry as stored, checked against the release.</summary>
    public static byte[] ReadEntry(YearOfTheDragonDisc disc, YearOfTheDragonLevel level)
    {
        byte[] entry = disc.WadEntry(level.WadEntry);
        if (Convert.ToHexStringLower(SHA256.HashData(entry)) != level.EntrySha256)
            throw new InvalidDataException($"This Spyro: Year of the Dragon disc's {level.Name} differs from the USA Rev 1 release.");
        return entry;
    }
}
