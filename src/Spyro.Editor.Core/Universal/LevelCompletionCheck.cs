using System.Buffers.Binary;
using System.Text.Json;

namespace Spyro.Editor.Core.Universal;

/// <summary>What one level still lacks for the game to be finished 100%: every one of the game's 80 dragons and 12
/// dragon eggs lives in a particular level, so a level you change or rebuild must keep its own dragons (statues) and
/// egg thieves; and a level that had a Return Home vortex needs one to leave it the way the game's levels are left.
/// Gems are not checked here: Create BIN moves the level's gem total with the gems you place.
/// <see cref="DuplicateDragons"/>/<see cref="DuplicateThieves"/>: own dragons or thieves in the level more than once
/// (each has one save identity, so the game counts only one of them).</summary>
public sealed record LevelCompletionGap(string LevelKey, int DragonsNeeded, int DragonsPresent, int ThievesNeeded, int ThievesPresent,
    int DuplicateDragons = 0, int DuplicateThieves = 0, bool ExitNeeded = false, bool ExitPresent = false)
{
    public bool Complete => DragonsPresent >= DragonsNeeded && ThievesPresent >= ThievesNeeded && (ExitPresent || !ExitNeeded);
    public int MissingDragons => Math.Max(0, DragonsNeeded - DragonsPresent);
    public int MissingThieves => Math.Max(0, ThievesNeeded - ThievesPresent);
}

/// <summary>A level's own dragons, egg thieves and Return Home vortexes: rows in its original object table.</summary>
public sealed record LevelOwnRows(IReadOnlyList<int> Dragons, IReadOnlyList<int> Thieves, IReadOnlyList<int> Exits)
{
    public static LevelOwnRows None { get; } = new([], [], []);
}

public static class LevelCompletionCheck
{
    public const int DragonStatueClass = 0xFA, EggThiefClass = 0x21, ReturnHomeClass = 0x009;
    /// <summary>The dragon egg a thief carries: not a row of the level, the thief's own update spawns it (a shared,
    /// always-loaded model) and its handler counts it when collected (eggs 0x80075810, the level's 0x80076FE8 + 4 x index).</summary>
    public const int DragonEggClass = 0x22;

    /// <summary>The level's own dragon statues, egg thieves and Return Home vortexes (rows in the original level).</summary>
    public static LevelOwnRows OriginalRows(UniversalLevelData data)
    {
        List<int> dragons = [], thieves = [], exits = [];
        int rows = data.Scene().RowCount;
        for (int i = 0; i < rows; i++)
        {
            int c = ClassOf(data, i);
            if (c == DragonStatueClass) dragons.Add(i);
            else if (c == EggThiefClass) thieves.Add(i);
            else if (c == ReturnHomeClass) exits.Add(i);
        }
        return new(dragons, thieves, exits);
    }

    private static int ClassOf(UniversalLevelData data, int row) => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36));

    /// <summary>The level as it will be exported: <paramref name="cleared"/> when its own objects are all gone (Start Blank,
    /// New Level), else those in <paramref name="removedNativeRows"/>; <paramref name="added"/> the objects added to it.
    /// Only the level's own dragons and thieves count: they carry its save identities.</summary>
    public static LevelCompletionGap Check(UniversalSourceLibrary library, string levelKey, bool cleared,
        IEnumerable<int> removedNativeRows, IEnumerable<(string SourceKey, int Row)> added) =>
        Check(OriginalRows(library.Get(levelKey).Data), levelKey, cleared, removedNativeRows, added, SourceClass(library));

    /// <summary>The class of an added object's source row (-1 when it can't be read).</summary>
    public static Func<string, int, int> SourceClass(UniversalSourceLibrary library) => (key, row) =>
    {
        try { var data = library.Get(key).Data; return row >= 0 && row < data.Scene().RowCount ? ClassOf(data, row) : -1; }
        catch (InvalidDataException) { return -1; }
    };

    /// <summary>As above, with the level's own rows already read (<see cref="OriginalRows"/>); <paramref name="sourceClass"/>
    /// tells an added Return Home vortex (any level's) from other objects.</summary>
    public static LevelCompletionGap Check(LevelOwnRows own, string levelKey, bool cleared,
        IEnumerable<int> removedNativeRows, IEnumerable<(string SourceKey, int Row)> added, Func<string, int, int>? sourceClass = null)
    {
        HashSet<int> removed = [.. removedNativeRows];
        var all = added.ToList();
        var copies = all.Where(a => string.Equals(a.SourceKey, levelKey, StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.Row).ToDictionary(g => g.Key, g => g.Count());
        int Times(int r) => (!cleared && !removed.Contains(r) ? 1 : 0) + copies.GetValueOrDefault(r);
        int Present(IReadOnlyList<int> rows) => rows.Count(r => Times(r) > 0);
        int Twice(IReadOnlyList<int> rows) => rows.Count(r => Times(r) > 1);
        bool exit = own.Exits.Any(r => Times(r) > 0) ||
            sourceClass != null && all.Any(a => !string.IsNullOrEmpty(a.SourceKey) && sourceClass(a.SourceKey, a.Row) == ReturnHomeClass);
        return new(levelKey, own.Dragons.Count, Present(own.Dragons), own.Thieves.Count, Present(own.Thieves),
            Twice(own.Dragons), Twice(own.Thieves), own.Exits.Count > 0, exit);
    }

    /// <summary>The objects a saved native-edits file removes from its level (rows) and adds (source level, row).</summary>
    public static (List<int> Removed, List<(string SourceKey, int Row)> Added) ReadNativeEdits(string nativeEditsPath)
    {
        List<int> removed = [];
        if (File.Exists(nativeEditsPath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(nativeEditsPath));
            if (doc.RootElement.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
                foreach (var e in edits.EnumerateArray())
                    if (e.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True &&
                        !(e.TryGetProperty("added", out var a) && a.ValueKind == JsonValueKind.True) &&
                        e.TryGetProperty("trueIndex", out var t) && t.TryGetInt32(out int row) && row >= 0)
                        removed.Add(row);
        }
        var added = UniversalPlacementReader.Read(nativeEditsPath).Where(p => !p.Retired).Select(p => (p.SourceLevelKey, p.SourceRowIndex)).ToList();
        return (removed, added);
    }

    /// <summary>One line for the user: what to add back, or null when nothing is missing.</summary>
    public static string? Describe(LevelCompletionGap gap, string slotName, string? builtName = null)
    {
        string where = builtName != null && !string.Equals(builtName, slotName, StringComparison.OrdinalIgnoreCase)
            ? $"{builtName} (in {slotName}'s place)" : slotName;
        List<string> lines = [], parts = [], from = [];
        if (gap.MissingDragons > 0)
            parts.Add(gap.MissingDragons == 1 ? $"1 of {slotName}'s dragons" : $"{gap.MissingDragons} of {slotName}'s {gap.DragonsNeeded} dragons");
        if (gap.MissingThieves > 0)
            parts.Add(gap.ThievesNeeded == 1 ? $"{slotName}'s egg thief" : $"{gap.MissingThieves} of {slotName}'s {gap.ThievesNeeded} egg thieves");
        string thief = gap.ThievesNeeded == 1 ? "egg thief" : "egg thieves";
        if (parts.Count > 0)
            from.Add((gap.MissingDragons > 0 && gap.MissingThieves > 0 ? "dragons and " + thief : gap.MissingDragons > 0 ? "dragons" : thief) + $" from {slotName}");
        if (gap.ExitNeeded && !gap.ExitPresent) { parts.Add("a Return Home vortex"); from.Add("Return Home from any level"); }
        if (parts.Count > 0)
            lines.Add($"{where} needs {Join(parts)} (Add Object: {string.Join(", ", from)}).");
        if (Twice(gap) is { } twice)
            lines.Add($"{where} has {twice}; the game counts only one, so remove the extra copy.");
        return lines.Count == 0 ? null : string.Join(" ", lines);
    }

    private static string Join(List<string> parts) => parts.Count == 1 ? parts[0] : string.Join(", ", parts[..^1]) + " and " + parts[^1];

    /// <summary>"the same dragon twice" / "2 egg thieves twice", or null.</summary>
    private static string? Twice(LevelCompletionGap gap)
    {
        List<string> parts = [];
        if (gap.DuplicateDragons > 0) parts.Add(gap.DuplicateDragons == 1 ? "the same dragon twice" : $"{gap.DuplicateDragons} dragons twice");
        if (gap.DuplicateThieves > 0) parts.Add(gap.DuplicateThieves == 1 ? "the same egg thief twice" : $"{gap.DuplicateThieves} egg thieves twice");
        return parts.Count == 0 ? null : string.Join(" and ", parts);
    }

    /// <summary>The short line the editor shows under a level's treasure, e.g. "To finish 100%: dragons 4 of 4, egg thief 0 of 1
    /// · Return Home vortex: yes", or null for a level with none of them.</summary>
    public static string? Summary(LevelCompletionGap gap)
    {
        if (gap.DragonsNeeded == 0 && gap.ThievesNeeded == 0 && !gap.ExitNeeded) return null;
        List<string> parts = [], lines = [];
        if (gap.DragonsNeeded > 0) parts.Add($"dragons {gap.DragonsPresent} of {gap.DragonsNeeded}");
        if (gap.ThievesNeeded > 0) parts.Add($"egg {(gap.ThievesNeeded == 1 ? "thief" : "thieves")} {gap.ThievesPresent} of {gap.ThievesNeeded}");
        if (parts.Count > 0) lines.Add("To finish 100%: " + string.Join(", ", parts));
        if (gap.ExitNeeded) lines.Add("Return Home vortex: " + (gap.ExitPresent ? "yes" : "missing"));
        return string.Join("  ·  ", lines) + (gap.Complete && Twice(gap) == null ? " ✓" : "");
    }
}
