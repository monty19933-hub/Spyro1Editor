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
            RequireMode2Form1(raw, sector);
            RebuildRawSector(raw);

            image.Position = imageOffset;
            image.Write(raw);
        }
        return sectors.Count;
    }

    /// <summary>
    /// Copies complete MODE2/2352 Form 1 sectors inside one raw image. Unlike
    /// logical file writes, this can safely initialize destination LBAs that
    /// currently contain Form 2 padding. The destination MSF address and all
    /// integrity fields are rebuilt before publication.
    /// </summary>
    public static int CopyAbsoluteSectors(
        FileStream image,
        DiscLayout layout,
        int sourceLba,
        int destinationLba,
        int sectorCount)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        ValidateLayout(layout);
        if (sourceLba < 0 || destinationLba < 0 || sectorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(sectorCount));
        long totalSectors = image.Length / RawSectorBytes;
        if (sourceLba + (long)sectorCount > totalSectors ||
            destinationLba + (long)sectorCount > totalSectors)
        {
            throw new InvalidDataException("Raw-sector copy exceeds the disc image.");
        }
        if (RangesOverlap(sourceLba, destinationLba, sectorCount))
        {
            throw new InvalidOperationException(
                "Guarded raw-sector copies require non-overlapping source and destination ranges.");
        }

        byte[] raw = new byte[RawSectorBytes];
        for (int index = 0; index < sectorCount; index++)
        {
            long sourceSector = sourceLba + (long)index;
            long destinationSector = destinationLba + (long)index;
            image.Position = checked(sourceSector * RawSectorBytes);
            image.ReadExactly(raw);
            RequireMode2Form1(raw, sourceSector);
            WriteMsfAddress(raw, destinationSector);
            RebuildRawSector(raw);
            image.Position = checked(destinationSector * RawSectorBytes);
            image.Write(raw);
        }
        return sectorCount;
    }

    public static int VerifyAbsoluteSectors(
        FileStream image,
        DiscLayout layout,
        IEnumerable<(int Lba, int SectorCount)> ranges)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ranges);
        ValidateLayout(layout);
        SortedSet<long> sectors = [];
        foreach ((int lba, int sectorCount) in ranges)
        {
            if (lba < 0 || sectorCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(ranges));
            for (int index = 0; index < sectorCount; index++)
                sectors.Add(lba + (long)index);
        }

        byte[] raw = new byte[RawSectorBytes];
        byte[] expected = new byte[RawSectorBytes];
        foreach (long sector in sectors)
        {
            long imageOffset = checked(sector * RawSectorBytes);
            if (imageOffset + RawSectorBytes > image.Length)
                throw new InvalidDataException($"Raw sector {sector} exceeds the disc image.");
            image.Position = imageOffset;
            image.ReadExactly(raw);
            RequireMode2Form1(raw, sector);
            RequireMsfAddress(raw, sector);
            raw.CopyTo(expected, 0);
            RebuildRawSector(expected);
            if (!raw.AsSpan(2072, RawSectorBytes - 2072)
                    .SequenceEqual(expected.AsSpan(2072, RawSectorBytes - 2072)))
            {
                throw new InvalidDataException(
                    $"Raw sector {sector} failed MODE2 Form 1 EDC/ECC verification.");
            }
        }
        return sectors.Count;
    }

    /// <summary>
    /// Rewrites only explicitly allowed MODE2 XA submode flag bits in both
    /// duplicated subheaders, then rebuilds the sector integrity fields. This
    /// is used when an ISO file grows and its EOR/EOF marker must move from the
    /// former last sector to the new last sector.
    /// </summary>
    public static int RewriteDuplicatedSubmodeFlags(
        FileStream image,
        DiscLayout layout,
        int lba,
        byte expectedSubmode,
        byte replacementSubmode,
        byte allowedChangeMask)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        ValidateLayout(layout);
        if (lba < 0 || checked((lba + 1L) * RawSectorBytes) > image.Length)
            throw new ArgumentOutOfRangeException(nameof(lba));
        if (((expectedSubmode ^ replacementSubmode) & ~allowedChangeMask) != 0)
        {
            throw new InvalidOperationException(
                "The requested XA submode rewrite changes bits outside the guarded mask.");
        }
        if ((expectedSubmode & 0x20) != 0 || (replacementSubmode & 0x20) != 0 ||
            (expectedSubmode & 0x08) == 0 || (replacementSubmode & 0x08) == 0)
        {
            throw new InvalidOperationException(
                "Guarded XA boundary rewrites require MODE2 Form 1 data submodes.");
        }

        byte[] raw = new byte[RawSectorBytes];
        long imageOffset = checked((long)lba * RawSectorBytes);
        image.Position = imageOffset;
        image.ReadExactly(raw);
        RequireMode2Form1(raw, lba);
        RequireMsfAddress(raw, lba);
        if (raw[18] != expectedSubmode || raw[22] != expectedSubmode)
        {
            throw new InvalidDataException(
                $"Raw sector {lba} XA submode is {raw[18]:X2}/{raw[22]:X2}, expected duplicated {expectedSubmode:X2}.");
        }
        raw[18] = replacementSubmode;
        raw[22] = replacementSubmode;
        RebuildRawSector(raw);
        image.Position = imageOffset;
        image.Write(raw);
        return 1;
    }

    public static void VerifyDuplicatedSubmode(
        FileStream image,
        DiscLayout layout,
        int lba,
        byte expectedSubmode)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        ValidateLayout(layout);
        if (lba < 0 || checked((lba + 1L) * RawSectorBytes) > image.Length)
            throw new ArgumentOutOfRangeException(nameof(lba));
        byte[] raw = new byte[RawSectorBytes];
        image.Position = checked((long)lba * RawSectorBytes);
        image.ReadExactly(raw);
        RequireMode2Form1(raw, lba);
        RequireMsfAddress(raw, lba);
        if (raw[18] != expectedSubmode || raw[22] != expectedSubmode)
        {
            throw new InvalidDataException(
                $"Raw sector {lba} XA submode is {raw[18]:X2}/{raw[22]:X2}, expected duplicated {expectedSubmode:X2}.");
        }
    }

    private static void ValidateLayout(DiscLayout layout)
    {
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidOperationException("Raw sector integrity requires MODE2/2352 with user offset 24.");
    }

    private static void RequireMode2Form1(ReadOnlySpan<byte> raw, long sector)
    {
        if (raw.Length != RawSectorBytes || raw[15] != 2 ||
            !raw.Slice(16, 4).SequenceEqual(raw.Slice(20, 4)) ||
            (raw[18] & 0x20) != 0)
        {
            throw new InvalidDataException(
                $"Raw sector {sector} is not a duplicated-subheader MODE2 Form 1 sector.");
        }
    }

    private static bool RangesOverlap(int sourceLba, int destinationLba, int sectorCount) =>
        sourceLba < destinationLba + (long)sectorCount &&
        destinationLba < sourceLba + (long)sectorCount;

    private static void WriteMsfAddress(Span<byte> raw, long lba)
    {
        (byte minute, byte second, byte frame) = EncodeMsf(lba);
        raw[12] = minute;
        raw[13] = second;
        raw[14] = frame;
        raw[15] = 2;
    }

    private static void RequireMsfAddress(ReadOnlySpan<byte> raw, long lba)
    {
        (byte minute, byte second, byte frame) = EncodeMsf(lba);
        if (raw[12] != minute || raw[13] != second || raw[14] != frame || raw[15] != 2)
        {
            throw new InvalidDataException(
                $"Raw sector {lba} has an incorrect BCD MSF address.");
        }
    }

    private static (byte Minute, byte Second, byte Frame) EncodeMsf(long lba)
    {
        long absoluteFrame = checked(lba + 150);
        long minute = absoluteFrame / (75 * 60);
        long remainder = absoluteFrame % (75 * 60);
        long second = remainder / 75;
        long frame = remainder % 75;
        if (minute > 99)
            throw new InvalidDataException($"LBA {lba} cannot be represented by a two-digit BCD minute.");
        return (ToBcd(minute), ToBcd(second), ToBcd(frame));
    }

    private static byte ToBcd(long value)
    {
        if (value is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(value));
        return checked((byte)(((value / 10) << 4) | (value % 10)));
    }

    private static void RebuildRawSector(Span<byte> raw)
    {
        uint edc = ComputeEdc(raw.Slice(16, 2056));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.Slice(2072, 4), edc);
        byte h0 = raw[12];
        byte h1 = raw[13];
        byte h2 = raw[14];
        byte h3 = raw[15];
        raw.Slice(12, 4).Clear();
        ComputeEcc(raw.Slice(12), 86, 24, 2, 86, raw.Slice(2076, 172));
        ComputeEcc(raw.Slice(12), 52, 43, 86, 88, raw.Slice(2248, 104));
        raw[12] = h0;
        raw[13] = h1;
        raw[14] = h2;
        raw[15] = h3;
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
