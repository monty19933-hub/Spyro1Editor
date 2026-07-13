using System.IO.Compression;
using System.Text;

namespace Spyro.Editor.Core.Editing;

public static class PngRgbaImage
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private const int MaximumDimension = 8192;
    private const int MaximumPixels = 16_777_216;

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
        byte[] palette = [];
        byte[] transparency = [];
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
                if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
                    (long)width * height > MaximumPixels)
                {
                    throw new InvalidOperationException($"PNG dimensions must be no larger than {MaximumDimension}x{MaximumDimension} and {MaximumPixels:N0} pixels.");
                }
                if (compression != 0 || filter != 0 || interlace != 0)
                    throw new InvalidOperationException("PNG must use standard compression and be non-interlaced.");
                bool supported = colorType switch
                {
                    0 or 3 => bitDepth is 1 or 2 or 4 or 8,
                    2 or 4 or 6 => bitDepth == 8,
                    _ => false
                };
                if (!supported)
                    throw new InvalidOperationException("PNG must be 8-bit RGB/RGBA/grayscale or 1-, 2-, 4-, or 8-bit indexed/grayscale.");
            }
            else if (type == "PLTE")
            {
                palette = bytes.AsSpan(dataOffset, length).ToArray();
            }
            else if (type == "tRNS")
            {
                transparency = bytes.AsSpan(dataOffset, length).ToArray();
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
        if (colorType == 3 && (palette.Length < 3 || palette.Length % 3 != 0))
            throw new InvalidOperationException("Indexed PNG is missing a valid palette.");

        byte[] compressedBytes = compressed.ToArray();
        using MemoryStream compressedStream = new(compressedBytes);
        using ZLibStream zlib = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream rawStream = new();
        zlib.CopyTo(rawStream);
        byte[] raw = rawStream.ToArray();

        int bitsPerPixel = colorType switch
        {
            0 or 3 => bitDepth,
            2 => bitDepth * 3,
            4 => bitDepth * 2,
            6 => bitDepth * 4,
            _ => throw new InvalidOperationException("Unsupported PNG color type.")
        };
        int bytesPerPixel = Math.Max(1, (bitsPerPixel + 7) / 8);
        int stride = checked((int)(((long)width * bitsPerPixel + 7) / 8));
        int expected = checked((stride + 1) * height);
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
            Unfilter(current, previous, bytesPerPixel, filterType);

            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = DecodePixel(current, x, bitDepth, colorType, palette, transparency);
            }

            (previous, current) = (current, previous);
        }

        return pixels;
    }

    private static Rgba32 DecodePixel(byte[] row, int x, int bitDepth, int colorType, byte[] palette, byte[] transparency)
    {
        switch (colorType)
        {
            case 0:
            {
                int sample = ReadPackedSample(row, x, bitDepth);
                byte gray = ScaleSample(sample, bitDepth);
                int transparentSample = transparency.Length >= 2 ? ReadBigEndianUInt16(transparency, 0) : -1;
                return new Rgba32(gray, gray, gray, sample == transparentSample ? (byte)0 : (byte)255);
            }
            case 2:
            {
                int offset = x * 3;
                byte alpha = 255;
                if (transparency.Length >= 6 &&
                    row[offset] == ReadBigEndianUInt16(transparency, 0) &&
                    row[offset + 1] == ReadBigEndianUInt16(transparency, 2) &&
                    row[offset + 2] == ReadBigEndianUInt16(transparency, 4))
                {
                    alpha = 0;
                }
                return new Rgba32(row[offset], row[offset + 1], row[offset + 2], alpha);
            }
            case 3:
            {
                int paletteIndex = ReadPackedSample(row, x, bitDepth);
                int paletteOffset = paletteIndex * 3;
                if (paletteOffset + 3 > palette.Length)
                    throw new InvalidOperationException($"Indexed PNG palette index {paletteIndex} is outside its palette.");
                byte alpha = paletteIndex < transparency.Length ? transparency[paletteIndex] : (byte)255;
                return new Rgba32(palette[paletteOffset], palette[paletteOffset + 1], palette[paletteOffset + 2], alpha);
            }
            case 4:
            {
                int offset = x * 2;
                return new Rgba32(row[offset], row[offset], row[offset], row[offset + 1]);
            }
            case 6:
            {
                int offset = x * 4;
                return new Rgba32(row[offset], row[offset + 1], row[offset + 2], row[offset + 3]);
            }
            default:
                throw new InvalidOperationException("Unsupported PNG color type.");
        }
    }

    private static int ReadPackedSample(byte[] row, int x, int bitDepth)
    {
        if (bitDepth == 8)
            return row[x];
        int bitOffset = x * bitDepth;
        int shift = 8 - bitDepth - (bitOffset & 7);
        int mask = (1 << bitDepth) - 1;
        return (row[bitOffset >> 3] >> shift) & mask;
    }

    private static byte ScaleSample(int sample, int bitDepth)
    {
        int max = (1 << bitDepth) - 1;
        return (byte)((sample * 255 + (max / 2)) / max);
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

    private static int ReadBigEndianUInt16(byte[] bytes, int offset)
    {
        return (bytes[offset] << 8) | bytes[offset + 1];
    }
}

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);
