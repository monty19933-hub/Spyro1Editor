using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Rebuilds EDC/ECC for modified MODE2/2352 Form 1 sectors. DiscImage patches logical
/// 2048-byte payloads; this closes the raw-sector integrity boundary for runtime candidates.
/// </summary>
internal static class RawMode2Form1SectorIntegrity
{
    private const int RawSectorBytes = 2352;
    private const int UserBytes = 2048;
    private const int UserOffset = 24;
    private static readonly byte[] EccForward = new byte[256];
    private static readonly byte[] EccBackward = new byte[256];
    private static readonly uint[] EdcTable = new uint[256];

    static RawMode2Form1SectorIntegrity()
    {
        for (int index = 0; index < 256; index++)
        {
            int doubled = (index << 1) ^ ((index & 0x80) != 0 ? 0x11D : 0);
            EccForward[index] = (byte)doubled;
            EccBackward[index ^ doubled] = (byte)index;

            uint edc = (uint)index;
            for (int bit = 0; bit < 8; bit++)
                edc = (edc >> 1) ^ ((edc & 1) != 0 ? 0xD8018001u : 0u);
            EdcTable[index] = edc;
        }
    }

    public static int RebuildFileRanges(
        FileStream image,
        DiscLayout layout,
        int fileLba,
        IEnumerable<(long Offset, int ByteLength)> ranges)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ranges);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidOperationException("Raw sector integrity requires MODE2/2352 with user offset 24.");
        SortedSet<long> sectors = [];
        foreach ((long offset, int byteLength) in ranges)
        {
            if (offset < 0 || byteLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(ranges), "Raw-sector patch ranges must be non-empty.");
            long first = offset / UserBytes;
            long last = checked((offset + byteLength - 1) / UserBytes);
            for (long sector = first; sector <= last; sector++)
                sectors.Add(fileLba + sector);
        }

        byte[] raw = new byte[RawSectorBytes];
        foreach (long sector in sectors)
        {
            long imageOffset = checked(sector * RawSectorBytes);
            image.Position = imageOffset;
            image.ReadExactly(raw);
            if (raw[15] != 2 || !raw.AsSpan(16, 4).SequenceEqual(raw.AsSpan(20, 4)) ||
                (raw[18] & 0x20) != 0)
            {
                throw new InvalidDataException(
                    $"Raw sector {sector} is not a duplicated-subheader MODE2 Form 1 sector.");
            }

            uint edc = ComputeEdc(raw.AsSpan(16, 2056));
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(2072, 4), edc);
            byte h0 = raw[12];
            byte h1 = raw[13];
            byte h2 = raw[14];
            byte h3 = raw[15];
            raw.AsSpan(12, 4).Clear();
            ComputeEcc(raw.AsSpan(12), 86, 24, 2, 86, raw.AsSpan(2076, 172));
            ComputeEcc(raw.AsSpan(12), 52, 43, 86, 88, raw.AsSpan(2248, 104));
            raw[12] = h0;
            raw[13] = h1;
            raw[14] = h2;
            raw[15] = h3;

            image.Position = imageOffset;
            image.Write(raw);
        }
        return sectors.Count;
    }

    private static uint ComputeEdc(ReadOnlySpan<byte> bytes)
    {
        uint edc = 0;
        foreach (byte value in bytes)
            edc = (edc >> 8) ^ EdcTable[(edc ^ value) & 0xFF];
        return edc;
    }

    private static void ComputeEcc(
        ReadOnlySpan<byte> source,
        int majorCount,
        int minorCount,
        int majorMultiplier,
        int minorIncrement,
        Span<byte> destination)
    {
        int size = checked(majorCount * minorCount);
        if (source.Length < size || destination.Length < majorCount * 2)
            throw new InvalidDataException("Raw-sector ECC source/destination span is incomplete.");
        for (int major = 0; major < majorCount; major++)
        {
            int index = ((major >> 1) * majorMultiplier) + (major & 1);
            byte a = 0;
            byte b = 0;
            for (int minor = 0; minor < minorCount; minor++)
            {
                byte value = source[index];
                index += minorIncrement;
                if (index >= size)
                    index -= size;
                a ^= value;
                b ^= value;
                a = EccForward[a];
            }
            a = EccBackward[EccForward[a] ^ b];
            destination[major] = a;
            destination[major + majorCount] = (byte)(a ^ b);
        }
    }
}
