using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Dragons of a level of the game in a level that is not theirs (a level of a new world). A dragon is rescued
/// through four things its own level holds, and all of them come along:
/// <list type="bullet">
/// <item>its rows (statue 0xFA, pedestal, the fairy who waits there 0x6E), which name each other
/// (<see cref="UniversalDragonRows"/>);</item>
/// <item>its rescue scene: one of the level file's subfiles 4-9 (the dragon's and Spyro's models and animation, the
/// dragon's voice, the camera's track), which the statue names by number at +0x18. The game reads it from the level
/// the player is in, so each placed statue gets a copy as the next subfile, filmed where the statue now stands
/// (<see cref="NativeDragonRescueCamera"/>), and its number;</item>
/// <item>the scene's pictures, drawn from the level's texture memory: brought in like a model's (the composer);</item>
/// <item>its level's code for the statue, the fairy and the crystal's splinters (0xFB), under their own numbers: the
/// game finds the statue's model by number 250 to know where the scene may load, and creates the splinters by number
/// (<see cref="UniversalNativeRelease"/>). One level's code runs them all, so the dragons of one level of the game
/// can stand in a level, not those of two.</item>
/// </list>
/// A level file names ten subfiles, four of them the level itself: six dragons at most.</summary>
public sealed record UniversalDragonGuests(string SourceKey, IReadOnlyList<UniversalMobyPlacement> Placements, IReadOnlyList<byte[]> Streams,
    IReadOnlySet<int> Released, int Statues)
{
    public const int MaxStatues = 6, RescueActorClass = 0xFB, StreamWord = 0x18;
    /// <summary>The objects a rescue puts into the level's spare object slots while it runs: the crystal's splinters and
    /// the sparks of the scene. Counted in the emulator, 2026-11-14, over some forty rescues (Artisans', Stone Hill's,
    /// High Caves', Tree Tops' and Ice Cavern's dragons): 43 to 52 more than were there before, 54 in the slots at most.
    /// The number here and the margin below keep 64 slots free, ten more than the most seen.</summary>
    public const int RescueSpawns = 48;
    /// <summary>The fewest spare object slots a level with a dragon may have (the rescue's objects and a margin).</summary>
    public const int RescueSpareSlots = RescueSpawns + UniversalMobyComposer.DropSlotMargin;

    /// <summary>The spare object slots a level with a dragon needs: the rescue's own, one for every enemy or chest that
    /// drops something (a dropped gem lies in a slot until Spyro takes it) and the margin; never more than the game's
    /// own levels keep (91), with which they free their dragons whatever lies about.</summary>
    public static int SpareSlotsNeeded(int dropCapable) =>
        Math.Min(UniversalMobyComposer.RetailDynamicSlots, Math.Max(RescueSpareSlots, dropCapable + UniversalMobyComposer.DropSlotMargin + RescueSpawns));

    /// <summary>Why a level with <paramref name="active"/> objects can't have a dragon as well.</summary>
    public static string TooManyObjects(string levelName, int active) =>
        $"{levelName} has {active} objects, and with a dragon in it a level can hold {256 - RescueSpareSlots}: a rescue puts the crystal's splinters and sparks " +
        $"among the level's objects, and the game has 256 places for all of them. Take {active - (256 - RescueSpareSlots)} objects out (loose gems are the easiest).";

    /// <summary>Null, or why the composed level <paramref name="levelData"/> (with <paramref name="spareSlots"/> spare
    /// object slots left) has too little room for a rescue and what its enemies and chests drop.</summary>
    public static string? SpareSlotRefusal(string levelName, byte[] levelData, int spareSlots)
    {
        var data = new UniversalLevelData(levelData);
        int drops = UniversalMobyComposer.DropCapableRows(data), needed = SpareSlotsNeeded(drops), rows = data.Scene().RowCount;
        if (spareSlots >= needed) return null;
        return $"{levelName} has {rows} objects, {drops} of them enemies and chests that drop something, and with a dragon in it that is too many: a rescue puts " +
               $"the crystal's splinters and sparks among the level's objects, every dropped gem lies among them until Spyro takes it, and the game has 256 places " +
               $"for all of them ({needed} must stay free, {spareSlots} would). Take {needed - spareSlots} objects out (loose gems are the easiest).";
    }

    /// <summary>The classes that come with the dragons without being placed (the splinters).</summary>
    public IReadOnlyList<(int Class, string SourceKey)> Dependencies => [(RescueActorClass, SourceKey)];

    /// <summary>The level's data with the placed dragons' scenes as its subfiles 4 on.</summary>
    public byte[] WithStreams(byte[] levelData)
    {
        var data = new UniversalLevelData(levelData.ToArray());
        data.ReplaceSubfilesFrom(4, Streams);
        return data.Bytes;
    }

    /// <summary>The game memory the rescue scene of the statue in <paramref name="row"/> of its own level needs while it
    /// plays (the scene is read in two parts, its voice first: the larger part, in whole disc sectors);
    /// <see cref="int.MaxValue"/> for a row that is no statue with a scene.</summary>
    public static int SceneRoom(UniversalLevelData source, string sourceKey, int row)
    {
        try
        {
            byte[] properties = source.CaptureRow(sourceKey, row).Properties;
            var streams = source.Subfiles().Skip(4).ToList();
            int number = properties.Length >= StreamWord + 4 ? BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(StreamWord)) : -1;
            if (number < 0 || number >= streams.Count) return int.MaxValue;
            int prefix = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(streams[number].Offset + 12));
            return Math.Max(UniversalLevelData.Align(prefix, 2048), UniversalLevelData.Align(streams[number].Length - prefix, 2048));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { return int.MaxValue; }
    }

    private static int Class(UniversalSourceLibrary donors, UniversalMobyPlacement p) =>
        BinaryPrimitives.ReadUInt16LittleEndian(donors.Get(p.SourceLevelKey).Data.Row(p.SourceRowIndex).AsSpan(0x36));

    private static bool Native(UniversalSourceLibrary donors, UniversalMobyPlacement p)
    {
        if (p.Retired || p.SpecialId != null || string.IsNullOrEmpty(p.SourceLevelKey)) return false;
        var data = donors.Get(p.SourceLevelKey).Data;
        return p.SourceRowIndex >= 0 && p.SourceRowIndex < data.Scene().RowCount;
    }

    /// <summary>The dragons among <paramref name="placements"/> made ready for <paramref name="levelName"/> (null: it has
    /// none), or the refusal that says in plain words what stands in the way. <paramref name="ownKey"/>: the level of the
    /// game whose place the level is built on, whose own dragons are not guests (null: none).</summary>
    public static UniversalDragonGuests? Plan(string levelName, IReadOnlyList<UniversalMobyPlacement> placements, UniversalSourceLibrary donors, string? ownKey = null)
    {
        var family = placements.Select((p, i) => (Placement: p, Index: i)).Where(x => Native(donors, x.Placement) &&
            Class(donors, x.Placement) is var c && (c is UniversalDragonRows.Statue or UniversalDragonRows.Dragon || UniversalDragonRows.IsPedestal(c))).ToList();
        if (family.Count == 0) return null;
        var keys = family.Select(x => x.Placement.SourceLevelKey.ToLowerInvariant()).Distinct().ToList();
        string Name(string key) => donors.Get(key).Level.DisplayName;
        if (keys.Count > 1)
            throw new InvalidDataException($"{levelName} has dragons from {string.Join(" and ", keys.Take(3).Select(Name))}. A level's dragons all come from one level of the game: " +
                $"each level frees its dragons with its own code, and a level can run one level's. Keep the dragons of one of them in {levelName} " +
                "(another of your world's levels can have the others').");
        string key = keys[0];
        if (ownKey != null && string.Equals(key, ownKey, StringComparison.OrdinalIgnoreCase)) return null;
        var source = donors.Get(key).Data;
        var streams = source.Subfiles().Skip(4).ToList();
        var statues = family.Where(x => Class(donors, x.Placement) == UniversalDragonRows.Statue).ToList();
        if (statues.Count > MaxStatues)
            throw new InvalidDataException($"{levelName} has {statues.Count} dragons, and a level can hold {MaxStatues}: each dragon's rescue scene is a part of the level's file, " +
                $"which has room for {MaxStatues}. Take {statues.Count - MaxStatues} out.");
        // Every dragon whole: its statue, its pedestal and its fairy, in one group.
        foreach (var group in family.GroupBy(x => x.Placement.LinkGroupId ?? ""))
        {
            int[] classes = [.. group.Select(x => Class(donors, x.Placement))];
            if (group.Key.Length == 0 || classes.Count(c => c == UniversalDragonRows.Statue) != 1 || classes.Count(UniversalDragonRows.IsPedestal) != 1 ||
                classes.Count(c => c == UniversalDragonRows.Dragon) != 1)
                throw new InvalidDataException($"A dragon in {levelName} is not whole (a dragon is its statue, its pedestal and the fairy who waits there). " +
                    "Remove what is left of it and add the dragon again with Add Object.");
        }
        List<byte[]> scenes = [];
        var ready = placements.ToArray();
        foreach (var (p, index) in statues)
        {
            byte[] row = source.Row(p.SourceRowIndex);
            byte[] properties = source.CaptureRow(key, p.SourceRowIndex).Properties;
            int number = properties.Length >= StreamWord + 4 ? BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(StreamWord)) : -1;
            if (properties.Length < 0x3C || number < 0 || number >= streams.Count || BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(0x24)) != -1)
                throw new InvalidDataException($"A dragon of {Name(key)} in {levelName} has no rescue scene the editor can bring along.");
            byte[] stream = source.Bytes.AsSpan(streams[number].Offset, streams[number].Length).ToArray();
            if (!NativeDragonRescueCamera.Films(stream, properties))
                throw new InvalidDataException($"A dragon of {Name(key)} in {levelName} is filmed from somewhere else than its statue says, so its rescue scene can't be moved with it.");
            scenes.Add(NativeDragonRescueCamera.MoveStream(stream, row, p.X, p.Y, p.Z, p.Yaw ?? row[0x46]));
            Dictionary<int, int> words = p.PropertyWordOverrides == null ? [] : new(p.PropertyWordOverrides);
            words[StreamWord] = scenes.Count - 1;
            ready[index] = p with { PropertyWordOverrides = words };
        }
        HashSet<int> released = [UniversalDragonRows.Statue, RescueActorClass];
        if (family.Any(x => Class(donors, x.Placement) == UniversalDragonRows.Dragon)) released.Add(UniversalDragonRows.Dragon);
        return new(key, ready, scenes, released, statues.Count);
    }
}
