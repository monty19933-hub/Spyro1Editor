using System.IO.Compression;
using System.Text;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

public static class TerrainTexturePngWriter
{
    public static async Task WriteRgbaAsync(string path, int width, int height, IReadOnlyList<Rgba32> pixels, CancellationToken cancellationToken = default)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        if (pixels.Count < width * height)
            throw new ArgumentOutOfRangeException(nameof(pixels), "Not enough pixels were provided for the requested texture dimensions.");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using MemoryStream raw = new();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < width; x++)
            {
                Rgba32 pixel = pixels[(y * width) + x];
                raw.WriteByte(pixel.R);
                raw.WriteByte(pixel.G);
                raw.WriteByte(pixel.B);
                raw.WriteByte(pixel.A);
            }
        }

        using MemoryStream compressed = new();
        await using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            await zlib.WriteAsync(raw.ToArray(), cancellationToken);

        using MemoryStream png = new();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WritePngChunk(png, "IHDR", BuildPngHeader(width, height));
        WritePngChunk(png, "IDAT", compressed.ToArray());
        WritePngChunk(png, "IEND", []);
        await File.WriteAllBytesAsync(path, png.ToArray(), cancellationToken);
    }

    public static async Task WriteGradientAsync(string path, int width, int height, ColorRgba low, ColorRgba high, CancellationToken cancellationToken = default)
    {
        await WritePaletteAsync(path, width, height, [low, high], cancellationToken);
    }

    public static async Task WritePaletteAsync(string path, int width, int height, IReadOnlyList<ColorRgba> colors, CancellationToken cancellationToken = default)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        if (colors.Count < 2)
            throw new ArgumentOutOfRangeException(nameof(colors), "Terrain texture palettes need at least two colors.");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using MemoryStream raw = new();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < width; x++)
            {
                double vertical = height <= 1 ? 0.0 : y / (double)(height - 1);
                double diagonal = width <= 1 ? vertical : ((x / (double)(width - 1)) * 0.35) + (vertical * 0.65);
                double checker = ((x / 8) + (y / 8)) % 2 == 0 ? -0.06 : 0.06;
                double t = Math.Clamp(diagonal + checker, 0.0, 1.0);
                ColorRgba color = PaletteColorAt(colors, t);
                raw.WriteByte(color.R);
                raw.WriteByte(color.G);
                raw.WriteByte(color.B);
                raw.WriteByte(255);
            }
        }

        using MemoryStream compressed = new();
        await using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            await zlib.WriteAsync(raw.ToArray(), cancellationToken);

        using MemoryStream png = new();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WritePngChunk(png, "IHDR", BuildPngHeader(width, height));
        WritePngChunk(png, "IDAT", compressed.ToArray());
        WritePngChunk(png, "IEND", []);
        await File.WriteAllBytesAsync(path, png.ToArray(), cancellationToken);
    }

    private static ColorRgba PaletteColorAt(IReadOnlyList<ColorRgba> colors, double t)
    {
        if (colors.Count == 2)
            return LerpColor(colors[0], colors[1], t);

        double scaled = Math.Clamp(t, 0.0, 1.0) * (colors.Count - 1);
        int index = Math.Clamp((int)Math.Floor(scaled), 0, colors.Count - 2);
        double localT = scaled - index;
        return LerpColor(colors[index], colors[index + 1], localT);
    }

    private static ColorRgba LerpColor(ColorRgba a, ColorRgba b, double t)
    {
        return ColorRgba.FromRgb(
            Lerp(a.R, b.R, t),
            Lerp(a.G, b.G, t),
            Lerp(a.B, b.B, t));
    }

    private static byte Lerp(byte a, byte b, double t)
    {
        return (byte)Math.Clamp((int)Math.Round(a + ((b - a) * t)), 0, 255);
    }

    private static byte[] BuildPngHeader(int width, int height)
    {
        byte[] header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        return header;
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);
        uint crc = Crc32(typeBytes, data);
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, unchecked((int)crc));
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Crc32(byte[] typeBytes, byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in typeBytes.Concat(data))
        {
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
