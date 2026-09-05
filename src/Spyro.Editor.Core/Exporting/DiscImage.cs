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
        byte[] data = new byte[layout.RootLength];
        int remaining = data.Length;
        int written = 0;
        int sector = layout.RootExtent;
        while (remaining > 0)
        {
            byte[] sectorBytes = ReadUserSector(stream, layout, sector);
            int toCopy = Math.Min(2048, remaining);
            Array.Copy(sectorBytes, 0, data, written, toCopy);
            written += toCopy;
            remaining -= toCopy;
            sector++;
        }

        int offset = 0;
        while (offset < data.Length)
        {
            int recordLength = data[offset];
            if (recordLength == 0)
            {
                offset = ((offset / 2048) + 1) * 2048;
                continue;
            }

            if (offset + recordLength > data.Length || recordLength < 34)
                break;

            int nameLength = data[offset + 32];
            string name = Encoding.ASCII.GetString(data, offset + 33, nameLength).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (matchesName(name))
                return new DiscFileRecord(name, GetUInt32(data, offset + 2), GetUInt32(data, offset + 10));

            offset += recordLength;
        }

        throw new InvalidOperationException("Could not find the executable file in the disc root.");
    }

    public static byte[] ReadFileBytes(FileStream stream, DiscLayout layout, int fileLba, long fileOffset, int length)
    {
        byte[] result = new byte[length];
        int remaining = length;
        int written = 0;
        long absolute = fileOffset;
        while (remaining > 0)
        {
            int sectorOffset = (int)(absolute % 2048);
            int sector = fileLba + (int)Math.Floor(absolute / 2048d);
            int toRead = Math.Min(2048 - sectorOffset, remaining);
            stream.Position = ((long)sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
            int read = stream.Read(result, written, toRead);
            if (read != toRead)
                throw new EndOfStreamException("Could not read the requested disc bytes.");

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
        long sector = fileLba + (long)Math.Floor(fileOffset / 2048d);
        long sectorOffset = fileOffset % 2048;
        return (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
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

        return new DiscLayout(sectorSize, userOffset, GetUInt32(buffer, 158), GetUInt32(buffer, 166));
    }

    private static byte[] ReadUserSector(FileStream stream, DiscLayout layout, int lba)
    {
        byte[] buffer = new byte[2048];
        stream.Position = ((long)lba * layout.SectorSize) + layout.UserOffset;
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read != buffer.Length)
            throw new EndOfStreamException("Could not read a full disc sector.");
        return buffer;
    }

    private static int GetUInt32(byte[] bytes, int offset) => (int)BitConverter.ToUInt32(bytes, offset);
}
