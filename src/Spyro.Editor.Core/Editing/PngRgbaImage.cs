using System.IO.Compression;
using System.Text;

namespace Spyro.Editor.Core.Editing;

public static class PngRgbaImage
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static Rgba32[] ReadRgba(string path, out int width, out int height)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return Decode(bytes, out width, out height);
    }

    public static Rgba32[] ReadResizedRgba(string path, int targetWidth, int targetHeight)
    {
        Rgba32[] source = ReadRgba(path, out int width, out int height);
        if (width == targetWidth && height == targetHeight)
            return source;

        Rgba32[] resized = new Rgba32[targetWidth * targetHeight];
        for (int y = 0; y < targetHeight; y++)
        {
            int sourceY = Math.Clamp((int)Math.Floor(y * height / (double)targetHeight), 0, height - 1);
            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX = Math.Clamp((int)Math.Floor(x * width / (double)targetWidth), 0, width - 1);
                resized[(y * targetWidth) + x] = source[(sourceY * width) + sourceX];
            }
        }

        return resized;
    }

    private static Rgba32[] Decode(byte[] bytes, out int width, out int height)
    {
        if (bytes.Length < Signature.Length || !bytes.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            throw new InvalidOperationException("PNG file expected.");

        width = 0;
        height = 0;
        int bitDepth = 0;
        int colorType = 0;
        MemoryStream compressed = new();
        int offset = Signature.Length;
        while (offset + 12 <= bytes.Length)
        {
            int length = ReadBigEndianInt32(bytes, offset);
            string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            int dataOffset = offset + 8;
            if (length < 0 || dataOffset + length + 4 > bytes.Length)
                throw new InvalidOperationException("PNG file is truncated.");

            if (type == "IHDR")
            {
                width = ReadBigEndianInt32(bytes, dataOffset);
                height = ReadBigEndianInt32(bytes, dataOffset + 4);
                bitDepth = bytes[dataOffset + 8];
                colorType = bytes[dataOffset + 9];
                int compression = bytes[dataOffset + 10];
                int filter = bytes[dataOffset + 11];
                int interlace = bytes[dataOffset + 12];
                if (width <= 0 || height <= 0 || bitDepth != 8 || compression != 0 || filter != 0 || interlace != 0)
                    throw new InvalidOperationException("PNG must be 8-bit, non-interlaced RGB or RGBA.");
                if (colorType is not (2 or 6))
                    throw new InvalidOperationException("PNG must be RGB or RGBA.");
            }
            else if (type == "IDAT")
            {
                compressed.Write(bytes, dataOffset, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset = dataOffset + length + 4;
        }

        if (width <= 0 || height <= 0 || compressed.Length == 0)
            throw new InvalidOperationException("PNG is missing image data.");

        byte[] compressedBytes = compressed.ToArray();
        using MemoryStream compressedStream = new(compressedBytes);
        using ZLibStream zlib = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream rawStream = new();
        zlib.CopyTo(rawStream);
        byte[] raw = rawStream.ToArray();

        int channels = colorType == 6 ? 4 : 3;
        int stride = width * channels;
        int expected = (stride + 1) * height;
        if (raw.Length < expected)
            throw new InvalidOperationException("PNG decompressed data is shorter than expected.");

        byte[] previous = new byte[stride];
        byte[] current = new byte[stride];
        Rgba32[] pixels = new Rgba32[width * height];
        int rawOffset = 0;
        for (int y = 0; y < height; y++)
        {
            int filterType = raw[rawOffset++];
            Array.Copy(raw, rawOffset, current, 0, stride);
            rawOffset += stride;
            Unfilter(current, previous, channels, filterType);

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = x * channels;
                byte alpha = channels == 4 ? current[pixelOffset + 3] : (byte)255;
                pixels[(y * width) + x] = new Rgba32(current[pixelOffset], current[pixelOffset + 1], current[pixelOffset + 2], alpha);
            }

            (previous, current) = (current, previous);
        }

        return pixels;
    }

    private static void Unfilter(byte[] current, byte[] previous, int bytesPerPixel, int filterType)
    {
        for (int i = 0; i < current.Length; i++)
        {
            int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
            int up = previous[i];
            int upperLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            int value = filterType switch
            {
                0 => current[i],
                1 => current[i] + left,
                2 => current[i] + up,
                3 => current[i] + ((left + up) / 2),
                4 => current[i] + Paeth(left, up, upperLeft),
                _ => throw new InvalidOperationException($"Unsupported PNG filter type {filterType}.")
            };
            current[i] = (byte)(value & 0xFF);
        }
    }

    private static int Paeth(int left, int up, int upperLeft)
    {
        int p = left + up - upperLeft;
        int pa = Math.Abs(p - left);
        int pb = Math.Abs(p - up);
        int pc = Math.Abs(p - upperLeft);
        if (pa <= pb && pa <= pc)
            return left;
        return pb <= pc ? up : upperLeft;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);
