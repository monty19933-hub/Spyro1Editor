using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>A locked chest (class 0xAE) names its key (class 0xAD) by row number in its first property word, in every
/// one of the game's 13 levels that have one. Each frame the chest's update looks that row up: while Spyro has no key
/// it switches the row on (draw distance +0x50 = 0x18, update distance +0x52 = 0x40; a key's row starts with both 0),
/// and once the key is taken it is the chest that opens. A chest copied without the word following its key switches
/// on whatever object sits at the old row number, and the key itself stays switched off: it is drawn only now and then
/// and cannot be picked up (seen in a game made by 2.5: the chest named row 95, its key was row 35).
/// Here the word becomes an indexed row link (<see cref="UniversalIndexedRows"/>), so a chest and its key are copied as
/// a pair and the word follows the key to its new row.</summary>
public static class UniversalLockedChestRows
{
    public const int ChestClass = 0xAE, KeyClass = 0xAD;
    /// <summary>Said by Create BIN (a note, not a refusal) for a locked chest left without any key.</summary>
    public const string NoKeyNote = "A locked chest has no key in its level, so it stays locked in the game: add a Key (Add Object), or remove the chest and add a Key Chest again, which brings its key.";

    /// <summary>The row of a locked chest's key in the chest's own level (-1: not a locked chest that names one).</summary>
    public static int KeyRow(UniversalLevelData data, UniversalRowTemplate chest)
    {
        if (chest.ClassId != ChestClass || chest.Properties.Length < 4 ||
            chest.InternalPointers.Any(p => p.Offset == 0) || chest.ExternalRowPointers.Any(p => p.Offset == 0)) return -1;
        int row = BinaryPrimitives.ReadInt32LittleEndian(chest.Properties);
        return row >= 0 && row < data.Scene().RowCount && ClassOf(data, row) == KeyClass ? row : -1;
    }

    /// <summary>The chest's link to its key (required: copied together, the word follows the key).</summary>
    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalRowTemplate template)
    {
        if (template.ClassId != ChestClass || template.SourceRowIndices.Count > 0) return template;
        int row = KeyRow(data, template);
        return row < 0 ? template : template with { SourceRowIndices = [new(0, row, KeyClass)] };
    }

    /// <summary>Pairs every placed locked chest with a key before the links are resolved. A chest whose own key (its
    /// level's key, in the chest's linked group) is placed exactly once is left as it is: that is how Add Object and
    /// Random objects place them, and how projects made before 2.6 hold them (chest and key as two objects of one
    /// level, in no group). Otherwise the chest takes a key no other chest has (its own level's first, then the
    /// nearest), and the two get a group of their own; a key of another level becomes the chest's level's key (keys
    /// are alike in every level). A chest left without any key is returned in <paramref name="keyless"/>: it must
    /// name itself (<see cref="Keyless"/>), or it would switch on an arbitrary row.</summary>
    public static IReadOnlyList<UniversalMobyPlacement> Pair(IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string, (LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay)> source, out IReadOnlySet<int> keyless)
    {
        int Class(UniversalMobyPlacement p)
        {
            if (p.SpecialId != null || p.Retired) return -1;
            var data = source(p.SourceLevelKey).Data;
            return p.SourceRowIndex >= 0 && p.SourceRowIndex < data.Scene().RowCount ? ClassOf(data, p.SourceRowIndex) : -1;
        }
        int Key(UniversalMobyPlacement chest)
        {
            var data = source(chest.SourceLevelKey).Data;
            return KeyRow(data, data.CaptureRow(chest.SourceLevelKey, chest.SourceRowIndex));
        }
        return Pair(placements, Class, Key, out keyless);
    }

    /// <summary>As above, told each placement's class (<paramref name="classOf"/>; -1: not an object of a level) and
    /// the row of each locked chest's key in the chest's own level (<paramref name="keyRowOf"/>).</summary>
    public static IReadOnlyList<UniversalMobyPlacement> Pair(IReadOnlyList<UniversalMobyPlacement> placements,
        Func<UniversalMobyPlacement, int> classOf, Func<UniversalMobyPlacement, int> keyRowOf, out IReadOnlySet<int> keyless)
    {
        keyless = new HashSet<int>();
        int[] classes = [.. placements.Select(classOf)];
        var chests = Enumerable.Range(0, placements.Count).Where(i => classes[i] == ChestClass).ToList();
        if (chests.Count == 0) return placements;
        var keys = Enumerable.Range(0, placements.Count).Where(i => classes[i] == KeyClass).ToList();
        int[] keyRow = new int[placements.Count];
        foreach (int c in chests) keyRow[c] = keyRowOf(placements[c]);
        bool Own(int chest, int key) => placements[key].SourceRowIndex == keyRow[chest] &&
            string.Equals(placements[key].SourceLevelKey, placements[chest].SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(placements[key].LinkGroupId ?? "", placements[chest].LinkGroupId ?? "", StringComparison.Ordinal);
        // A key and a chest that are a pair already: exactly one of each for the other.
        HashSet<int> taken = [], settled = [];
        foreach (int c in chests)
        {
            if (keyRow[c] < 0) { settled.Add(c); continue; }       // (no such chest in the game: left as it came)
            var own = keys.Where(k => Own(c, k)).ToList();
            if (own.Count == 1 && chests.Count(other => keyRow[other] >= 0 && Own(other, own[0])) == 1) { taken.Add(own[0]); settled.Add(c); }
        }
        if (settled.Count == chests.Count) return placements;
        var result = placements.ToArray(); HashSet<int> none = [];
        long Far(int a, int b) { long dx = placements[a].X - placements[b].X, dy = placements[a].Y - placements[b].Y, dz = placements[a].Z - placements[b].Z; return dx * dx + dy * dy + dz * dz; }
        foreach (int c in chests.Where(c => !settled.Contains(c)))
        {
            var free = keys.Where(k => !taken.Contains(k)).ToList();
            if (free.Count == 0) { none.Add(c); continue; }
            int key = free.OrderBy(k => string.Equals(placements[k].SourceLevelKey, placements[c].SourceLevelKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(k => Far(c, k)).ThenBy(k => k).First();
            taken.Add(key);
            // A group of their own, named after the chest so that the same project always makes the same game.
            string group = "locked-chest:" + (placements[c].EditorId ?? c.ToString(System.Globalization.CultureInfo.InvariantCulture));
            result[c] = placements[c] with { LinkGroupId = group };
            result[key] = placements[key] with { SourceLevelKey = placements[c].SourceLevelKey, SourceRowIndex = keyRow[c], LinkGroupId = group };
        }
        keyless = none;
        return result;
    }

    /// <summary>A locked chest without a key names its own row: what its update switches on is then the chest itself
    /// (harmless), and it stays locked.</summary>
    public static UniversalRowTemplate Keyless(UniversalRowTemplate chest) =>
        chest.ClassId != ChestClass ? chest : chest with { SourceRowIndices = [], ImportedRowIndices = [], OwnRowIndexOffsets = [.. chest.OwnRowIndexOffsets.Append(0).Distinct()] };

    private static int ClassOf(UniversalLevelData data, int row) => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36));
}
