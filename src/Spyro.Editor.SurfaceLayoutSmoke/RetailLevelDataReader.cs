using System.Text;

namespace Spyro.Editor.SurfaceLayoutSmoke;

internal static class RetailLevelDataReader
{
    private const int WadLba = 37;

    public static byte[] ReadLogicalWadBytes(string imagePath, long wadOffset, int length)
    {
        if (wadOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(wadOffset));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        (int sectorSize, int userOffset) = DetectDiscLayout(imagePath);
        byte[] result = new byte[length];
        using FileStream stream = File.OpenRead(imagePath);
        int written = 0;
        long logicalOffset = wadOffset;
        while (written < length)
        {
            int sectorOffset = (int)(logicalOffset % 2048);
            long sector = WadLba + (logicalOffset / 2048);
            int readLength = Math.Min(2048 - sectorOffset, length - written);
            stream.Position = (sector * sectorSize) + userOffset + sectorOffset;
            int read = stream.Read(result, written, readLength);
            if (read != readLength)
                throw new EndOfStreamException("Could not read the native level-data block from the source image.");
            written += read;
            logicalOffset += read;
        }
        return result;
    }

    private static (int SectorSize, int UserOffset) DetectDiscLayout(string imagePath)
    {
        using FileStream stream = File.OpenRead(imagePath);
        byte[] marker = new byte[6];
        foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
        {
            long pvd = (16L * sectorSize) + userOffset;
            if (pvd + marker.Length > stream.Length)
                continue;
            stream.Position = pvd;
            if (stream.Read(marker) == marker.Length &&
                marker[0] == 1 &&
                Encoding.ASCII.GetString(marker.AsSpan(1)) == "CD001")
            {
                return (sectorSize, userOffset);
            }
        }
        throw new InvalidDataException("Could not identify the source disc image sector layout.");
    }
}
