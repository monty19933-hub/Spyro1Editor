using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>A homeworld dragon is three rows that name each other by row number: the statue (0xFA) names its pedestal
/// at +0x20 (+0x24 is -1), and the dragon (0x6E) names the statue at +0x00 and the pedestal at +0x04. The pedestal's class
/// is its world's: 0x14B (Artisans, Beast Makers, Dream Weavers and Gnasty's worlds), 0x14C (Peace Keepers), 0x14D (Magic
/// Crafters). Copied
/// together, the numbers follow the rows (UniversalIndexedRows); left as they were, the dragon hides whatever object now
/// has the pedestal's old row number when it is rescued.</summary>
public static class UniversalDragonRows
{
    public const int Statue = 0xFA, Dragon = 0x6E, Pedestal = 0x14B;
    /// <summary>A dragon pedestal of any world.</summary>
    public static bool IsPedestal(int classId) => classId is 0x14B or 0x14C or 0x14D;

    public static bool Applies(UniversalRowTemplate template) => template.ClassId is Statue or Dragon;

    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalRowTemplate template)
    {
        if (!Applies(template) || template.SourceRowIndices.Count > 0) return template;
        var links = template.ClassId == Statue ? new[] { (0x20, Pedestal) } : [(0x00, Statue), (0x04, Pedestal)];
        if (template.ClassId == Statue && (template.Properties.Length < 0x28 || I(template.Properties, 0x24) != -1))
            throw new InvalidDataException("A dragon statue's pedestal link is not where the game keeps it.");
        List<UniversalSourceRowIndex> indices = [];
        foreach (var (offset, cls) in links)
        {
            if (template.Properties.Length < offset + 4 || template.InternalPointers.Any(p => p.Offset == offset) || template.ExternalRowPointers.Any(p => p.Offset == offset))
                throw new InvalidDataException("A dragon's row links are not where the game keeps them.");
            int row = I(template.Properties, offset);
            int found = row < 0 || row >= data.Scene().RowCount ? -1 : BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36));
            if (found != cls && !(cls == Pedestal && IsPedestal(found)))
                throw new InvalidDataException("A dragon's row links are not where the game keeps them.");
            indices.Add(new(offset, row, found));
        }
        return template with { SourceRowIndices = indices };
    }

    /// <summary>The rows of the dragon <paramref name="row"/> belongs to (its statue, pedestal and dragon, in that order),
    /// so adding any of them adds the dragon whole; empty for any other row.</summary>
    public static IReadOnlyList<int> Family(UniversalLevelData data, string levelKey, int row)
    {
        int rows = data.Scene().RowCount;
        int Class(int r) => r >= 0 && r < rows ? BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36)) : -1;
        int Link(int r, int offset)
        {
            var p = data.CaptureRow(levelKey, r).Properties;
            return p.Length >= offset + 4 ? I(p, offset) : -1;
        }
        int cls = Class(row), statue = -1;
        if (cls == Statue) statue = row;
        else if (cls == Dragon) statue = Link(row, 0x00);
        else if (IsPedestal(cls)) statue = Enumerable.Range(0, rows).FirstOrDefault(r => Class(r) == Statue && Link(r, 0x20) == row, -1);
        if (Class(statue) != Statue) return [];
        int pedestal = Link(statue, 0x20);
        if (!IsPedestal(Class(pedestal))) return [];
        return [statue, pedestal, .. Enumerable.Range(0, rows).Where(r => Class(r) == Dragon && Link(r, 0x00) == statue)];
    }

    /// <summary>The first live placement among <paramref name="placements"/> that is part of a dragon (statue, dragon or
    /// pedestal), or null.</summary>
    public static UniversalMobyPlacement? FirstDragon(IEnumerable<UniversalMobyPlacement> placements, UniversalSourceLibrary donors)
    {
        foreach (var p in placements)
        {
            if (p.Retired || p.SpecialId != null) continue;
            var data = donors.Get(p.SourceLevelKey).Data;
            if (p.SourceRowIndex < 0 || p.SourceRowIndex >= data.Scene().RowCount) continue;
            int c = BinaryPrimitives.ReadUInt16LittleEndian(data.Row(p.SourceRowIndex).AsSpan(0x36));
            if (c is Statue or Dragon || IsPedestal(c)) return p;
        }
        return null;
    }

    /// <summary>Null, or why <paramref name="levelName"/>, a level compiled like File › New Level's (level 65, or a level of
    /// a new world), can't have the dragons among <paramref name="placements"/>: a dragon's rescue scene is in its own
    /// level's data, which such a level does not have, and the game stops when the rescue starts (emulator, 2026-10-18).
    /// A level rebuilt with Start Blank keeps its own dragons.</summary>
    public static string? BuiltLevelRefusal(string levelName, IEnumerable<UniversalMobyPlacement> placements, UniversalSourceLibrary donors) =>
        FirstDragon(placements, donors) is { } p
            ? $"{levelName} has a dragon from {donors.Get(p.SourceLevelKey).Level.DisplayName}, but a dragon can only be rescued in its own " +
              "level (the game stops when the rescue starts). Remove it; to keep a level's dragons, build on that level with Start Blank."
            : null;

    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
}
