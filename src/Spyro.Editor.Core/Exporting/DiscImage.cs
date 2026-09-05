using System.Buffers.Binary;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record DiscLayout(int SectorSize, int UserOffset, int RootExtent, int RootLength);

internal sealed record DiscFileRecord(string Name, int Lba, int Size);

internal static class DiscImage
{
    public static DiscLayout DetectLayout(string path)
    {
        using FileStream stream = File.OpenRead(path);
        foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
        {
            DiscLayout? layout = TryReadPrimaryVolumeDescriptor(stream, sectorSize, userOffset);
            if (layout != null)
                return layout;
        }

        throw new InvalidOperationException("Could not find an ISO9660 primary volume descriptor.");
    }

    public static DiscFileRecord FindRootFileRecord(FileStream stream, DiscLayout layout, Predicate<string> matchesName)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(matchesName);
        if (layout.RootLength <= 0)
            throw new InvalidDataException("The ISO root directory has no readable extent.");
        RequireReadRange(stream, layout, layout.RootExtent, 0, layout.RootLength);
        // A source-controlled RootLength must never become a single allocation.
        // ISO directory records may not cross a logical sector boundary.
        for (long directoryOffset = 0; directoryOffset < layout.RootLength; directoryOffset += 2048)
        {
            int count = (int)Math.Min(2048, layout.RootLength - directoryOffset);
            byte[] data = ReadFileBytes(stream, layout, layout.RootExtent, directoryOffset, count);
            for (int offset = 0; offset < data.Length;)
            {
                int recordLength = data[offset];
                if (recordLength == 0)
                    break;
                if (recordLength < 34 || offset + recordLength > data.Length)
                    throw new InvalidDataException("An ISO directory record crosses its sector or directory boundary.");
                int nameLength = data[offset + 32];
                int minimumLength = 33 + nameLength + (nameLength % 2 == 0 ? 1 : 0);
                if (nameLength == 0 || minimumLength > recordLength)
                    throw new InvalidDataException("An ISO directory identifier exceeds its containing record.");
                int lba = ReadBothEndianInt32(data, offset + 2, "file extent");
                int size = ReadBothEndianInt32(data, offset + 10, "file length");
                RequireReadRange(stream, layout, lba, 0, size);
                string name = Encoding.ASCII.GetString(data, offset + 33, nameLength).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
                if (matchesName(name))
                    return new DiscFileRecord(name, lba, size);
                offset += recordLength;
            }
        }

        throw new InvalidOperationException("Could not find the executable file in the disc root.");
    }

    public static byte[] ReadFileBytes(FileStream stream, DiscLayout layout, int fileLba, long fileOffset, int length)
    {
        RequireReadRange(stream, layout, fileLba, fileOffset, length);
        byte[] result = new byte[length];
        int remaining = length;
        int written = 0;
        long absolute = fileOffset;
        while (remaining > 0)
        {
            int sectorOffset = (int)(absolute % 2048);
            int toRead = Math.Min(2048 - sectorOffset, remaining);
            stream.Position = ConvertFileOffsetToImageOffset(layout, fileLba, absolute);
            stream.ReadExactly(result.AsSpan(written, toRead));

            written += toRead;
            remaining -= toRead;
            absolute += toRead;
        }

        return result;
    }

    public static void WriteFileBytes(FileStream stream, DiscLayout layout, int fileLba, long fileOffset, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(bytes);
        if (fileLba < 0 || fileOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(fileOffset), "Disc writes require nonnegative LBA and logical offset.");
        bool cooked = layout.SectorSize == 2048 && layout.UserOffset == 0;
        bool raw = (layout.SectorSize == 2352 && layout.UserOffset == 24) ||
                   (layout.SectorSize == 2336 && layout.UserOffset == 8);
        if (!cooked && !raw)
            throw new InvalidOperationException("Disc writes require ISO/2048 or MODE2 Form 1 /2352 or /2336.");
        if (!stream.CanSeek || !stream.CanWrite || (raw && !stream.CanRead))
            throw new InvalidOperationException("Disc writes require a seekable writable stream, with read access for raw sectors.");
        long endOffset = checked(fileOffset + bytes.LongLength);
        long firstSector = checked(fileLba + fileOffset / 2048);
        long lastSector = bytes.Length == 0 ? firstSector : checked(fileLba + (endOffset - 1) / 2048);
        long physicalEnd = bytes.Length == 0
            ? checked(firstSector * layout.SectorSize + layout.UserOffset + fileOffset % 2048)
            : checked((lastSector + 1) * layout.SectorSize);
        if (physicalEnd > stream.Length)
            throw new EndOfStreamException("The requested disc write exceeds the existing image; logical writes cannot grow a disc.");
        if (bytes.Length == 0)
            return;

        byte[]? sectorBytes = raw ? new byte[layout.SectorSize] : null;
        if (sectorBytes != null)
        {
            // Preflight the entire range before touching any payload. In particular,
            // a later Form 2 or truncated sector must not leave a partial patch.
            for (long sector = firstSector; sector <= lastSector; sector++)
            {
                stream.Position = checked(sector * layout.SectorSize);
                stream.ReadExactly(sectorBytes);
                RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(sectorBytes, layout, sector);
            }
        }

        int remaining = bytes.Length;
        int written = 0;
        long absolute = fileOffset;
        while (remaining > 0)
        {
            int sectorOffset = (int)(absolute % 2048);
            long sector = checked(fileLba + absolute / 2048);
            int toWrite = Math.Min(2048 - sectorOffset, remaining);
            long sectorStart = checked(sector * layout.SectorSize);
            if (sectorBytes == null)
            {
                stream.Position = checked(sectorStart + sectorOffset);
                stream.Write(bytes, written, toWrite);
            }
            else
            {
                stream.Position = sectorStart;
                stream.ReadExactly(sectorBytes);
                bytes.AsSpan(written, toWrite).CopyTo(sectorBytes.AsSpan(layout.UserOffset + sectorOffset));
                RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(sectorBytes, layout, sector);
                stream.Position = sectorStart;
                stream.Write(sectorBytes);
            }
            written += toWrite;
            remaining -= toWrite;
            absolute += toWrite;
        }
    }

    public static long ConvertFileOffsetToImageOffset(DiscLayout layout, int fileLba, long fileOffset)
    {
        RequireLogicalLayout(layout);
        if (fileLba < 0 || fileOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(fileOffset), "Disc offsets require a nonnegative file LBA and logical offset.");
        long sector = checked(fileLba + fileOffset / 2048);
        long sectorOffset = fileOffset % 2048;
        return checked(sector * layout.SectorSize + layout.UserOffset + sectorOffset);
    }

    public static int IndexOfBytes(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0)
            return -1;

        for (int i = Math.Max(0, start); i <= haystack.Length - needle.Length; i++)
        {
            bool matched = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return i;
        }

        return -1;
    }

    public static string BuildCueText(string sourceCuePath, string outputBinName)
    {
        if (!File.Exists(sourceCuePath))
            return $"FILE \"{outputBinName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n";

        string[] lines = File.ReadAllLines(sourceCuePath, Encoding.ASCII);
        bool replaced = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!replaced && trimmed.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(" BINARY", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"FILE \"{outputBinName}\" BINARY";
                replaced = true;
            }
        }

        return string.Join('\n', replaced ? lines : new[] { $"FILE \"{outputBinName}\" BINARY" }.Concat(lines)) + "\n";
    }

    private static DiscLayout? TryReadPrimaryVolumeDescriptor(FileStream stream, int sectorSize, int userOffset)
    {
        long offset = (16L * sectorSize) + userOffset;
        if (stream.Length < offset + 2048)
            return null;

        byte[] buffer = new byte[2048];
        stream.Position = offset;
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read != buffer.Length)
            return null;

        string signature = Encoding.ASCII.GetString(buffer, 1, 5);
        if (buffer[0] != 1 || signature != "CD001")
            return null;

        if (buffer[6] != 1 || buffer[156] < 34 || buffer[188] != 1 || buffer[189] != 0)
            throw new InvalidDataException("The ISO primary volume descriptor or its root record is malformed.");
        int blockSizeLittle = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(128, 2));
        int blockSizeBig = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(130, 2));
        if (blockSizeLittle != 2048 || blockSizeBig != blockSizeLittle)
            throw new InvalidDataException("The ISO logical block size must be 2048 with matching endian encodings.");
        int rootExtent = ReadBothEndianInt32(buffer, 158, "root extent");
        int rootLength = ReadBothEndianInt32(buffer, 166, "root length");
        if (rootLength == 0)
            throw new InvalidDataException("The ISO root directory has zero length.");
        DiscLayout layout = new(sectorSize, userOffset, rootExtent, rootLength);
        RequireReadRange(stream, layout, rootExtent, 0, rootLength);
        return layout;
    }

    private static void RequireReadRange(FileStream stream, DiscLayout layout, int fileLba, long fileOffset, int length)
    {
        ArgumentNullException.ThrowIfNull(stream);
        RequireLogicalLayout(layout);
        if (!stream.CanSeek || !stream.CanRead)
            throw new InvalidOperationException("Disc reads require a seekable readable stream.");
        if (fileLba < 0 || fileOffset < 0 || length < 0)
            throw new ArgumentOutOfRangeException(nameof(fileOffset), "Disc reads require nonnegative LBA, logical offset and length.");
        long end = checked(fileOffset + length);
        long physicalEnd = length == 0 && fileOffset % 2048 == 0
            ? checked((fileLba + fileOffset / 2048) * layout.SectorSize)
            : length == 0
                ? ConvertFileOffsetToImageOffset(layout, fileLba, fileOffset)
                : checked(ConvertFileOffsetToImageOffset(layout, fileLba, end - 1) + 1);
        if (physicalEnd > stream.Length)
            throw new EndOfStreamException("The requested logical disc extent exceeds the existing image.");
    }

    private static void RequireLogicalLayout(DiscLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!((layout.SectorSize == 2048 && layout.UserOffset == 0) ||
              (layout.SectorSize == 2352 && layout.UserOffset == 24) ||
              (layout.SectorSize == 2336 && layout.UserOffset == 8)))
            throw new InvalidOperationException("Logical disc access requires ISO/2048, MODE2/2352 or MODE2/2336.");
    }

    private static int ReadBothEndianInt32(ReadOnlySpan<byte> bytes, int offset, string field)
    {
        uint little = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
        uint big = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 4, 4));
        if (little != big || little > int.MaxValue)
            throw new InvalidDataException($"The ISO {field} has mismatched endian encodings or exceeds the supported range.");
        return (int)little;
    }
}
