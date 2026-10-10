using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Artisans' balloonist (class 0xBB) names its balloon (class 0x1A0) by row number in its only property word;
/// when the player chooses to travel, that row is the balloon that flies. Copied together, the word follows the
/// balloon to its new row (UniversalIndexedRows); without the balloon the balloonist would fly some other object.</summary>
public static class UniversalBalloonistRows
{
    public const int BalloonistClass = 0xBB, BalloonClass = 0x1A0;

    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalRowTemplate template)
    {
        if (template.ClassId != BalloonistClass || template.SourceRowIndices.Count > 0) return template;
        if (template.Properties.Length < 4 || template.InternalPointers.Any(p => p.Offset == 0) || template.ExternalRowPointers.Any(p => p.Offset == 0))
            throw new InvalidDataException("A balloonist's balloon is not where the game keeps it.");
        int row = BinaryPrimitives.ReadInt32LittleEndian(template.Properties);
        if (row < 0 || row >= data.Scene().RowCount || BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36)) != BalloonClass)
            throw new InvalidDataException("A balloonist's balloon is not where the game keeps it.");
        return template with { SourceRowIndices = [new(0, row, BalloonClass)] };
    }
}
