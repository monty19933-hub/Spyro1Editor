using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>Neutral original-coordinate descriptor grammar, not source or destination authority.</summary>
public sealed class NativeStaticTerrainTextureDescriptor
{
    private readonly byte[] _descriptor;
    internal NativeStaticTerrainTextureDescriptor(ReadOnlySpan<byte> raw, int offset, int bpp, int side,
        int pixelOffset, int pixelWidth, int paletteOffset, int paletteWidth, int paletteRows)
    {
        _descriptor = raw.ToArray(); RecordOffset = offset; BitsPerPixel = bpp; TileSide = side;
        PixelByteOffset = pixelOffset; PixelRowByteLength = pixelWidth; PixelRowCount = side;
        PaletteByteOffset = paletteOffset; PaletteRowByteLength = paletteWidth; PaletteRowCount = paletteRows;
    }
    public int RecordOffset { get; }
    public int BitsPerPixel { get; }
    public int TileSide { get; }
    public int PixelByteOffset { get; }
    public int PixelRowByteLength { get; }
    public int PixelRowCount { get; }
    public int PaletteByteOffset { get; }
    public int PaletteRowByteLength { get; }
    public int PaletteRowCount { get; }
    public byte[] CopyDescriptor() => _descriptor.ToArray();
    public byte[] CopyPixels(ReadOnlySpan<byte> completeVram) =>
        CopyRectangle(completeVram, PixelByteOffset, PixelRowByteLength, PixelRowCount);
    public byte[] CopyPalette(ReadOnlySpan<byte> completeVram) =>
        CopyRectangle(completeVram, PaletteByteOffset, PaletteRowByteLength, PaletteRowCount);

    private static byte[] CopyRectangle(ReadOnlySpan<byte> completeVram, int offset, int width, int rows)
    {
        if (completeVram.Length != 0x80000)
            throw new InvalidDataException("Texture extraction requires the exact right-half VRAM upload, excluding SPU bytes.");
        byte[] result = new byte[checked(width * rows)];
        for (int row = 0; row < rows; row++)
            completeVram.Slice(offset + row * 1024, width).CopyTo(result.AsSpan(row * width));
        return result;
    }
}

/// <summary>
/// Bounded complete native LQ16/HQ168 record, restricted to canonical orientation0.
/// The three LQ aliases include all sixteen distance-palette rows. This decoder
/// does not allocate, mutate, identify a donor, or certify any renderer/runtime.
/// </summary>
public sealed class NativeStaticTerrainTextureLayout
{
    private readonly byte[] _record;
    private NativeStaticTerrainTextureLayout(ReadOnlySpan<byte> record,
        NativeStaticTerrainTextureDescriptor[] descriptors)
    {
        _record = record.ToArray(); Descriptors = Array.AsReadOnly(descriptors);
        RecordSha256 = Convert.ToHexStringLower(SHA256.HashData(record));
    }
    public const int RecordByteLength = 184;
    public bool SourceCertified => false;
    public IReadOnlyList<NativeStaticTerrainTextureDescriptor> Descriptors { get; }
    public string RecordSha256 { get; }
    public byte[] CopyRecord() => _record.ToArray();

    public static NativeStaticTerrainTextureLayout Decode(ReadOnlySpan<byte> record)
    {
        if (record.Length != RecordByteLength)
            throw new InvalidDataException("A complete native terrain record is exactly16 LQ plus168 HQ bytes.");
        if (!record[..8].SequenceEqual(record.Slice(8, 8)) || !record[..8].SequenceEqual(record.Slice(16, 8)))
            throw new InvalidDataException("Native LQ0/LQ1/HQ-leading aliases must retain identical descriptor bytes.");
        var descriptors = new NativeStaticTerrainTextureDescriptor[23];
        for (int i = 0; i < descriptors.Length; i++)
        {
            ReadOnlySpan<byte> raw = record.Slice(i * 8, 8);
            bool low = i < 3;
            int side = low ? 32 : raw[4] - raw[0] + 1;
            if (raw[5] != raw[1] || raw[4] < raw[0] ||
                (low ? raw[0] % 2 != 0 || raw[4] - raw[0] != 31 || raw[7] != 0x80 || (raw[6] & 0x88) != 0x08
                     : (raw[6] & 0x88) != 0x88 || (raw[7] & 0x7F) != 0 ||
                       (i < 7 ? side != 32 : side is not (16 or 32))))
                throw new InvalidDataException("Native texture depth/shape/page/orientation is outside the canonical static subset.");
            int x = low ? (raw[6] & 7) * 128 + raw[0] / 2 : (raw[6] & 15) * 128 + raw[0] - 1024;
            int y = ((raw[6] & 16) != 0 ? 256 : 0) + raw[1];
            if (raw[1] + side > 256)
                throw new InvalidDataException("Native texture rectangle crosses its encoded vertical page.");
            int width = low ? 16 : side;
            int paletteX, paletteY, paletteWidth, paletteRows;
            if (low)
            {
                paletteX = raw[2] * 32 - 1024; paletteY = raw[3] * 4;
                paletteWidth = 32; paletteRows = 16;
            }
            else
            {
                ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]);
                if ((clut & 0x8000) != 0)
                    throw new InvalidDataException("Reserved CLUT bit cannot authorize a wrapped source palette.");
                paletteX = ((clut & 63) * 16 - 512) * 2; paletteY = clut >> 6;
                paletteWidth = 512; paletteRows = 1;
            }
            RequireRectangle(x, y, width, side); RequireRectangle(paletteX, paletteY, paletteWidth, paletteRows);
            descriptors[i] = new(raw, i * 8, low ? 4 : 8, side, y * 1024 + x, width,
                paletteY * 1024 + paletteX, paletteWidth, paletteRows);
        }
        return new(record, descriptors);
    }
    private static void RequireRectangle(int x, int y, int width, int rows)
    {
        if (x < 0 || width <= 0 || x + width > 1024 || y < 0 || rows <= 0 || y + rows > 512)
            throw new InvalidDataException("Native texture or full distance-palette rows escape the exact GPU upload domain.");
    }
}
