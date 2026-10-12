using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Where the level window's levels go on the disc: new WAD entries (102-255, unused by the game). Inside WAD.WAD
/// while the grown WAD and the executable still fit before the movie files; otherwise after the image's last sector,
/// outside every ISO file (the game reads WAD rows as raw sectors from LBA 37, so a row's offset may point there).</summary>
public static class NativeLevelWindowDisc
{
    public const int FirstFreeEntry = 102, WadLba = 37, User = 2048, RawSector = 2352;

    /// <summary>The WAD with the new rows, and the bytes to write after the image (empty when everything fits inside).</summary>
    public sealed record Placement(byte[] Wad, IReadOnlyList<(int Entry, int Lba, byte[] Data)> AfterImage)
    {
        public bool AfterMovies => AfterImage.Count > 0;
        public ReadOnlySpan<byte> Table => Wad.AsSpan(0, 0x800);
    }

    /// <summary>Places <paramref name="entries"/> (entry number to bytes, 102-255, each still empty in
    /// <paramref name="wad"/>). <paramref name="exeLength"/> is the executable that will follow the WAD;
    /// <paramref name="forceAfterImage"/> places them after the image even when they would fit (tests).</summary>
    public static Placement Place(UniversalDiscImage image, byte[] wad, IReadOnlyDictionary<int, byte[]> entries, int exeLength, bool forceAfterImage = false)
    {
        if (image.WadLba != WadLba) throw new InvalidDataException("WAD.WAD must start at LBA 37 (the game's fixed position).");
        if (wad.Length % User != 0) throw new InvalidDataException("WAD image must be sector aligned.");
        foreach (var (entry, data) in entries)
        {
            if (entry is < FirstFreeEntry or > 255) throw new InvalidDataException($"New levels use WAD entries {FirstFreeEntry}-255, not {entry}.");
            if (BinaryPrimitives.ReadUInt64LittleEndian(wad.AsSpan(8 * entry)) != 0) throw new InvalidDataException($"WAD entry {entry} is already in use.");
            if (data.Length == 0) throw new InvalidDataException($"WAD entry {entry} would be empty.");
        }
        int Sectors(int bytes) => (bytes + User - 1) / User;
        int inside = wad.Length / User + entries.Values.Sum(d => Sectors(d.Length));
        bool fits = WadLba + inside + Sectors(exeLength) <= image.NextFileLba;
        if (fits && !forceAfterImage)
        {
            using var m = new MemoryStream();
            m.Write(wad);
            byte[] grown = wad.ToArray();
            List<(int, int, int)> rows = [];
            foreach (var (entry, data) in entries.OrderBy(p => p.Key))
            {
                rows.Add((entry, (int)m.Position, data.Length));
                m.Write(data); m.Write(new byte[Sectors(data.Length) * User - data.Length]);
            }
            grown = m.ToArray();
            foreach (var (entry, offset, length) in rows) Row(grown, entry, offset, length);
            return new(grown, []);
        }
        long imageBytes = new FileInfo(image.Path).Length;
        if (imageBytes % RawSector != 0) throw new InvalidDataException("The image is not whole 2352-byte sectors.");
        int lba = checked((int)(imageBytes / RawSector));
        byte[] table = wad.ToArray();
        List<(int, int, byte[])> after = [];
        foreach (var (entry, data) in entries.OrderBy(p => p.Key))
        {
            Row(table, entry, checked((lba - WadLba) * User), data.Length);
            after.Add((entry, lba, data));
            lba += Sectors(data.Length);
        }
        return new(table, after);
    }

    /// <summary>Writes the image: the WAD and executable as <see cref="UniversalDiscImage.WriteImage"/> does, then the
    /// levels placed after the image.</summary>
    public static void Write(UniversalDiscImage image, string outputPath, Placement placement, byte[] exe)
    {
        image.WriteImage(outputPath, placement.Wad, exe);
        foreach (var (_, lba, data) in placement.AfterImage) UniversalDiscImage.WriteRawSectors(outputPath, lba, data);
    }

    /// <summary>Reads WAD entry <paramref name="entry"/> of an image whether it lies inside WAD.WAD or after the image.</summary>
    public static byte[] ReadEntry(string imagePath, int entry)
    {
        var image = UniversalDiscImage.Read(imagePath);
        var (offset, length) = image.Entry(entry);
        if (offset + (long)length <= image.Wad.Length) return image.EntryBytes(entry);
        if (offset % User != 0) throw new InvalidDataException($"WAD entry {entry} past the WAD is not sector aligned.");
        return UniversalDiscImage.ReadRawSectors(imagePath, WadLba + offset / User, length);
    }

    private static void Row(byte[] wad, int entry, int offset, int length)
    {
        BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(8 * entry), offset);
        BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(8 * entry + 4), length);
    }
}
