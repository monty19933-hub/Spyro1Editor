using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Spyro the Dragon kills Spyro when he is below his level's kill height, wherever he stands: a table of 36 signed
/// words in the USA executable (file offset 0x5F1A4, RAM 0x8006E9A4, world units = editor units x 16, index (world - 1) x 6
/// + level, e.g. Artisans 512, Dry Canyon 11,000, Cliff Town 12,500, Ice Cavern 16,000, High Caves 32,000). The main loop
/// compares Spyro's height with it in four places (0x80042030, 0x80042C68, 0x8004A080, 0x8004A54C). A level rebuilt in a
/// retail slot keeps its slot's height, which can be above the floors you build (Ice Cavern's is 1,000 editor units; Start
/// Blank's floor is at 544), so Create BIN lowers it to <see cref="Margin"/> under the rebuilt level's lowest face, never
/// under <see cref="FloorWorldUnits"/> and never above the slot's own. That one word per rebuilt slot is the only change
/// Create BIN makes to the table (<see cref="RequireOwned"/>).</summary>
public static class NativeKillHeights
{
    public const int TableOffset = 0x5F1A4, Count = 36;
    /// <summary>SHA-256 of the USA release's table.</summary>
    public const string UsaTableSha256 = "f3fb7da7be468080eb037ba4c9016bb7dbadebe61c27dcf11eed4f0f49366ae7";
    /// <summary>The USA release's table (world units, by index): what <see cref="UsaTableSha256"/> hashes. Exports read the
    /// executable they are given; this copy documents the values and lets tests run without the disc.</summary>
    public static IReadOnlyList<int> UsaWorldUnits { get; } =
    [
        512, 1024, 1024, 1024, 1024, 1024,          // Artisans, Stone Hill, Dark Hollow, Town Square, Toasty, Sunny Flight
        1024, 11000, 12500, 16000, 15275, 1024,     // Peace Keepers, Dry Canyon, Cliff Town, Ice Cavern, Doctor Shemp, Night Flight
        18000, 8000, 32000, 15000, 1024, 1024,      // Magic Crafters, Alpine Ridge, High Caves, Wizard Peak, Blowhard, Crystal Flight
        1024, 4096, 1024, 18432, 1024, 1024,        // Beast Makers, Terrace Village, Misty Bog, Tree Tops, Metalhead, Wild Flight
        10500, 22000, 22500, 24000, 25000, 1024,    // Dream Weavers, Dark Passage, Lofty Castle, Haunted Towers, Jacques, Icy Flight
        1024, 1024, 1024, 1024, 1024, 1024,         // Gnorc Gnexus, Gnorc Cove, Twilight Harbor, Gnasty Gnorc, Gnasty's Loot, level 65
    ];
    /// <summary>Editor units kept between a rebuilt level's lowest face (or start) and its kill height: deep enough that
    /// Spyro stands on the lowest floor, close enough that a fall off the level still ends in a death.</summary>
    public const float Margin = 256;
    /// <summary>The lowest kill height Create BIN writes, in world units (64 editor units): the height most retail levels
    /// already use. A slot whose own height is lower (Artisans, 512) keeps its own.</summary>
    public const int FloorWorldUnits = 1024;

    public static int Index(int levelId)
    {
        if (levelId is < 10 or > 65 || levelId % 10 > 5) throw new ArgumentOutOfRangeException(nameof(levelId));
        return (levelId / 10 - 1) * 6 + levelId % 10;
    }

    /// <summary>File offset of level <paramref name="levelId"/>'s word in the executable.</summary>
    public static int Offset(int levelId) => TableOffset + 4 * Index(levelId);

    /// <summary>The height (editor units) below which Spyro dies in level <paramref name="levelId"/> of the USA executable.</summary>
    public static float ReadOriginal(byte[] usaExe, int levelId)
    {
        RequireOriginal(usaExe);
        return Read(usaExe, levelId);
    }

    public static float Read(byte[] exe, int levelId) => Word(exe, levelId) / 16f;

    /// <summary>The lowest point Spyro can stand in <paramref name="document"/> (editor units): its lowest face, or its start
    /// when that is lower.</summary>
    public static float Lowest(SceneDocument document)
    {
        float lowest = document.Spawn.Z;
        foreach (var s in document.Surfaces)
            foreach (var v in s.Vertices)
                lowest = Math.Min(lowest, v.Z);
        return lowest;
    }

    /// <summary>The kill height (world units) Create BIN gives a slot whose original is <paramref name="originalWorldUnits"/>
    /// for a rebuilt level whose lowest point is <paramref name="lowest"/> (editor units).</summary>
    public static int For(int originalWorldUnits, float lowest) =>
        Math.Min(originalWorldUnits, Math.Max(FloorWorldUnits, (int)Math.Clamp(MathF.Floor((lowest - Margin) * 16), int.MinValue / 2, int.MaxValue / 2)));

    /// <summary>The lowest kill height (editor units) Create BIN can give level <paramref name="levelId"/>: faces under it
    /// still kill Spyro, so an importer lifts a level whose faces would be.</summary>
    public static float Minimum(byte[] originalExe, int levelId)
    {
        RequireOriginal(originalExe);
        return Math.Min(Word(originalExe, levelId), FloorWorldUnits) / 16f;
    }

    /// <summary>Lowers level <paramref name="levelId"/>'s kill height in <paramref name="stagedExe"/> for the rebuilt
    /// <paramref name="document"/> (<see cref="For"/>); returns the new height (editor units), or null when the slot's own
    /// is already low enough. The staged word must still be the original's (or this exact result): anything else is
    /// another edit of this slot's kill height, which is refused rather than overwritten.</summary>
    public static float? LowerFor(byte[] originalExe, byte[] stagedExe, int levelId, SceneDocument document)
    {
        RequireOriginal(originalExe);
        if (stagedExe.Length != originalExe.Length)
            throw new InvalidDataException("The rebuilt level's kill height needs the complete staged executable.");
        int original = Word(originalExe, levelId), current = Word(stagedExe, levelId), lowered = For(original, Lowest(document));
        if (current != original && current != lowered)
            throw new InvalidDataException($"The rebuilt level conflicts with an already edited kill height for level {levelId} " +
                $"({current / 16f:0.##} instead of {original / 16f:0.##}).");
        BinaryPrimitives.WriteInt32LittleEndian(stagedExe.AsSpan(Offset(levelId)), lowered);
        return lowered == original ? null : lowered / 16f;
    }

    /// <summary>The declared window: <paramref name="exe"/>'s kill table differs from the original only in the words of
    /// <paramref name="rebuiltLevelIds"/>, and each of those is lower than the original and not under
    /// <see cref="FloorWorldUnits"/>.</summary>
    public static void RequireOwned(byte[] originalExe, byte[] exe, IReadOnlyCollection<int> rebuiltLevelIds)
    {
        RequireOriginal(originalExe);
        if (exe.Length != originalExe.Length)
            throw new InvalidDataException("The kill-height check needs the complete executable.");
        for (int index = 0; index < Count; index++)
        {
            int levelId = (index / 6 + 1) * 10 + index % 6, before = Word(originalExe, levelId), after = Word(exe, levelId);
            if (after == before) continue;
            if (!rebuiltLevelIds.Contains(levelId) || after > before || after < FloorWorldUnits)
                throw new InvalidDataException($"Create BIN changed level {levelId}'s kill height outside its rebuilt level's window " +
                    $"({before / 16f:0.##} -> {after / 16f:0.##}).");
        }
    }

    /// <summary>A plain warning when faces of <paramref name="document"/>, rebuilt in level <paramref name="levelId"/>, are
    /// below the kill height Create BIN can give that slot (Spyro dies on them); null when none are.</summary>
    public static string? Warning(byte[] originalExe, int levelId, SceneDocument document)
    {
        RequireOriginal(originalExe);
        float kill = For(Word(originalExe, levelId), Lowest(document)) / 16f;
        int below = document.Surfaces.Count(s => s.Vertices.Any(v => v.Z < kill));
        return below == 0 ? null
            : $"{below} {(below == 1 ? "face is" : "faces are")} below height {kill:0.##}, where Spyro dies in this level slot. Raise {(below == 1 ? "it" : "them")} above {kill:0.##}.";
    }

    private static int Word(byte[] exe, int levelId) => BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(Offset(levelId)));

    private static void RequireOriginal(byte[] usaExe)
    {
        if (usaExe.Length < TableOffset + Count * 4 ||
            Convert.ToHexStringLower(SHA256.HashData(usaExe.AsSpan(TableOffset, Count * 4))) != UsaTableSha256)
            throw new InvalidDataException("The Spyro the Dragon executable's kill heights differ from the USA release.");
    }
}
