using System.Buffers.Binary;
using System.Globalization;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record FlyInLandingData(
    long WadOffset,
    int RawX,
    int RawY,
    int RawZ,
    int YawByte,
    int EntryDataByteLength);

public static class FlyInLandingLocator
{
    private const int WadLba = 37;
    private const int LevelEntryHeaderLength = 0x20;
    private const int LandingRecordLength = 0x10;

    public static FlyInLandingData Locate(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return Locate(stream, layout, level);
    }

    internal static FlyInLandingData Locate(FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped source WAD entry.");

        byte[] entryHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = ReadUInt32(entryHeader, 0);
        int entryByteLength = checked((int)ReadUInt32(entryHeader, 4));
        if (entryWadOffset <= 0 || entryByteLength < LevelEntryHeaderLength)
            throw new InvalidOperationException($"{level.DisplayName} has an invalid source WAD entry header.");

        byte[] levelHeader = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            entryWadOffset,
            LevelEntryHeaderLength);
        int entryDataRelativeOffset = checked((int)ReadUInt32(levelHeader, 0x18));
        int entryDataByteLength = checked((int)ReadUInt32(levelHeader, 0x1C));
        if (entryDataRelativeOffset < LevelEntryHeaderLength ||
            entryDataByteLength < LandingRecordLength ||
            (long)entryDataRelativeOffset + entryDataByteLength > entryByteLength)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} does not contain one valid destination-level entry data block.");
        }

        long landingWadOffset = entryWadOffset + entryDataRelativeOffset;
        byte[] landing = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            landingWadOffset,
            LandingRecordLength);
        int rawX = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(0, 4));
        int rawY = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(4, 4));
        int rawZ = BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(8, 4));
        if (!IsPlausibleCoordinate(rawX) || !IsPlausibleCoordinate(rawY) || !IsPlausibleCoordinate(rawZ))
        {
            throw new InvalidOperationException(
                $"{level.DisplayName}'s destination-level entry XYZ is outside the expected native coordinate range.");
        }

        if (level.HasSourceTable)
        {
            long tableWadOffset = ParseOffset(level.SourceTableWadOffset);
            long tableEnd = checked(tableWadOffset + ((long)level.SourceRecordCount * 0x58));
            long entryDataEnd = checked(landingWadOffset + entryDataByteLength);
            if (tableWadOffset < landingWadOffset || tableEnd > entryDataEnd)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName}'s mapped object table is not contained in its destination-level entry data block.");
            }
        }

        return new FlyInLandingData(
            WadOffset: landingWadOffset,
            RawX: rawX,
            RawY: rawY,
            RawZ: rawZ,
            YawByte: landing[0x0E],
            EntryDataByteLength: entryDataByteLength);
    }

    private static bool IsPlausibleCoordinate(int value) => value is > -1_000_000 and < 1_000_000;

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static long ParseOffset(string value)
    {
        string text = value.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(text, CultureInfo.InvariantCulture);
    }
}
