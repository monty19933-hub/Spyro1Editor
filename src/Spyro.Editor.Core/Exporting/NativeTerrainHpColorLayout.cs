namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-verified layout of one Spyro 1 high-detail terrain color payload.
/// The sector stores every four-byte table-1 color first, followed by every
/// four-byte table-2 color. The two colors for one index are not interleaved.
/// </summary>
public static class NativeTerrainHpColorLayout
{
    public const int ColorBytes = 4;
    public const int TableCount = 2;

    public static int TotalByteLength(int colorCount)
    {
        ValidateColorCount(colorCount);
        return checked(colorCount * ColorBytes * TableCount);
    }

    public static NativeTerrainHpColorOffsets GetColorOffsets(
        int colorDataStart,
        int colorCount,
        int colorIndex)
    {
        if (colorDataStart < 0)
            throw new ArgumentOutOfRangeException(nameof(colorDataStart));
        ValidateColorCount(colorCount);
        if (colorIndex < 0 || colorIndex >= colorCount)
            throw new ArgumentOutOfRangeException(nameof(colorIndex));

        int table1Offset = checked(colorDataStart + (colorIndex * ColorBytes));
        int table2Start = checked(colorDataStart + (colorCount * ColorBytes));
        int table2Offset = checked(table2Start + (colorIndex * ColorBytes));
        return new NativeTerrainHpColorOffsets(table1Offset, table2Offset);
    }

    private static void ValidateColorCount(int colorCount)
    {
        if (colorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(colorCount));
    }
}

public readonly record struct NativeTerrainHpColorOffsets(
    int Table1Offset,
    int Table2Offset);
