using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>A homeworld dragon is three rows that name each other by row number: the statue (0xFA) names its pedestal
/// (0x14B) at +0x20 (+0x24 is -1), and the dragon (0x6E) names the statue at +0x00 and the pedestal at +0x04. Copied
/// together, the numbers follow the rows (UniversalIndexedRows); left as they were, the dragon hides whatever object now
/// has the pedestal's old row number when it is rescued.</summary>
public static class UniversalDragonRows
{
    public const int Statue = 0xFA, Dragon = 0x6E, Pedestal = 0x14B;

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
            if (row < 0 || row >= data.Scene().RowCount || BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36)) != cls)
                throw new InvalidDataException("A dragon's row links are not where the game keeps them.");
            indices.Add(new(offset, row, cls));
        }
        return template with { SourceRowIndices = indices };
    }

    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
}
