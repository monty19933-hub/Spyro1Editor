using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Music;

public sealed record MusicTrackEntry(int TrackId, string DisplayName, bool IsSelectable)
{
    /// <summary>For a song brought in from another game: the unused XA slot the game plays it from.</summary>
    public int? CustomSlot { get; init; }
    public override string ToString() => CustomSlot == null ? $"{DisplayName} (Track {TrackId:00})" : DisplayName;
}

public static class MusicTrackCatalog
{
    private static readonly int[] NativeTrackIds =
    [
        0, 2, 32, 26, 33, 6,
        17, 3, 27, 13, 8, 11,
        31, 22, 46, 9, 16, 18,
        15, 7, 21, 29, 23, 1,
        34, 12, 14, 4, 28, 10,
        33, 5, 24, 25, 19, 19
    ];

    private static readonly string[] TrackNames =
    [
        "Artisans",
        "Wild Flight",
        "Stone Hill",
        "Dry Canyon",
        "Haunted Towers",
        "Gnorc Cove",
        "Sunny Flight",
        "Terrace Village",
        "Doctor Shemp",
        "Wizard Peak",
        "Icy Flight",
        "Night Flight",
        "Dark Passage",
        "Ice Cavern",
        "Lofty Castle",
        "Beast Makers",
        "Blowhard",
        "Peace Keepers",
        "Crystal Flight",
        "Gnasty's Loot",
        "Main Title",
        "Misty Bog",
        "Alpine Ridge",
        "Metalhead",
        "Twilight Harbor",
        "Gnasty Gnorc",
        "Town Square",
        "Cliff Town",
        "Jacques",
        "Tree Tops",
        "Unused XA slot A",
        "Magic Crafters",
        "Dark Hollow",
        "Toasty / Gnasty's World",
        "Dream Weavers",
        "Unused XA slot B",
        "Hidden alternate 01",
        "Hidden alternate 02",
        "Hidden alternate 03",
        "Hidden alternate 04",
        "Hidden alternate 05",
        "Hidden alternate 06",
        "Hidden alternate 07",
        "Hidden alternate 08",
        "Hidden alternate 09",
        "Hidden alternate 10",
        "High Caves / hidden alternate 11",
        "Hidden alternate 12"
    ];

    public static IReadOnlyList<MusicTrackEntry> Tracks { get; } = TrackNames
        .Select((name, trackId) => new MusicTrackEntry(trackId, name, trackId is not 30 and not 35))
        .ToArray();

    public static IReadOnlyList<MusicTrackEntry> SelectableTracks { get; } = Tracks
        .Where(track => track.IsSelectable)
        .ToArray();

    public static MusicTrackEntry? Find(int trackId) =>
        trackId >= 0 && trackId < Tracks.Count ? Tracks[trackId] : null;

    /// <summary>The two XA slots no level plays; songs from other games are written into them.</summary>
    public const int UnusedSlotA = 30, UnusedSlotB = 35;

    /// <summary>Songs from other games, each in its own unused slot so a game can use both: Crash Bandicoot: Warped's
    /// (rendered from the user's Crash disc at Create BIN) and Spyro: Year of the Dragon's (copied from the user's disc).</summary>
    public static IReadOnlyList<MusicTrackEntry> CustomTracks { get; } =
    [
        .. Crash.CrashMusic.Songs.Select(song => new MusicTrackEntry(song.TrackId, song.Name + " (Crash Bandicoot: Warped)", true) { CustomSlot = UnusedSlotA }),
        .. YearOfTheDragonDisc.LevelSongs.Select(song => new MusicTrackEntry(song.TrackId, song.Name + " (Spyro: Year of the Dragon)", true) { CustomSlot = UnusedSlotB }),
    ];

    /// <summary>True for a song copied from Spyro: Year of the Dragon.</summary>
    public static bool IsYearOfTheDragon(int trackId) => YearOfTheDragonDisc.LevelSongs.Any(song => song.TrackId == trackId);

    /// <summary>What a level can be given: Spyro's own tracks, then the songs from other games.</summary>
    public static IReadOnlyList<MusicTrackEntry> PickerTracks { get; } = [.. SelectableTracks, .. CustomTracks];

    /// <summary>A built-in track or a song from another game.</summary>
    public static MusicTrackEntry? FindAny(int trackId) =>
        Find(trackId) ?? CustomTracks.FirstOrDefault(track => track.TrackId == trackId);

    public static bool IsCustom(int trackId) => CustomTracks.Any(track => track.TrackId == trackId);

    public static int GetLevelIndex(LevelDefinition level)
    {
        int homeworld = level.LevelId / 10 - 1;
        int slot = level.LevelId % 10;
        if (homeworld is < 0 or >= 6 || slot is < 0 or >= 6)
            throw new InvalidOperationException($"{level.DisplayName} has unsupported level ID {level.LevelId} for music mapping.");

        return homeworld * 6 + slot;
    }

    public static int GetNativeTrackId(LevelDefinition level)
    {
        int levelIndex = GetLevelIndex(level);
        if (levelIndex < 0 || levelIndex >= NativeTrackIds.Length)
            throw new InvalidOperationException($"{level.DisplayName} does not have a native music table slot.");

        return NativeTrackIds[levelIndex];
    }
}
