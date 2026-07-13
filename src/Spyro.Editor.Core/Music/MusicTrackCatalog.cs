using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Music;

public sealed record MusicTrackEntry(int TrackId, string DisplayName, bool IsSelectable)
{
    public override string ToString() => $"{DisplayName} (Track {TrackId:00})";
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
