using System.Buffers.Binary;

namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Mutable CPU framebuffer whose pixels remain in the PlayStation GPU's
/// unsigned RGB5 domain. Bit 15 is never stored in framebuffer pixels; it is
/// texture/CLUT state and belongs in <see cref="Psx555TextureWord"/>.
/// </summary>
public sealed class PsxRgb5Framebuffer
{
    private readonly ushort[] _pixels;

    public PsxRgb5Framebuffer(int width, int height, PsxRgb5 clearColor)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Framebuffer width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Framebuffer height must be positive.");

        int pixelCount = checked(width * height);
        Width = width;
        Height = height;
        _pixels = new ushort[pixelCount];
        Clear(clearColor);
    }

    public int Width { get; }
    public int Height { get; }
    public int PixelCount => _pixels.Length;

    public PsxRgb5 GetPixel(int x, int y) =>
        PsxRgb5.FromPackedWord(_pixels[GetPixelIndex(x, y)]);

    public ushort GetPackedPixel(int x, int y) => _pixels[GetPixelIndex(x, y)];

    public void SetPixel(int x, int y, PsxRgb5 color) =>
        _pixels[GetPixelIndex(x, y)] = color.PackedWord;

    public void Clear(PsxRgb5 color) => Array.Fill(_pixels, color.PackedWord);

    /// <summary>
    /// Fills complete scanlines without exposing the mutable backing buffer.
    /// This is used to seed the bounded terrain framebuffer from deterministic
    /// editor background bands without one bounds-checked call per pixel.
    /// </summary>
    public void FillRows(int firstY, int rowCount, PsxRgb5 color)
    {
        if (firstY < 0 || firstY > Height)
            throw new ArgumentOutOfRangeException(nameof(firstY));
        if (rowCount < 0 || firstY + rowCount > Height)
            throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (rowCount == 0)
            return;

        Array.Fill(
            _pixels,
            color.PackedWord,
            checked(firstY * Width),
            checked(rowCount * Width));
    }

    /// <summary>
    /// Returns a read-only view for deterministic inspection. Callers cannot
    /// mutate the framebuffer through the returned span.
    /// </summary>
    public ReadOnlySpan<ushort> AsPackedWords() => _pixels;

    /// <summary>
    /// Copies the packed RGB5 framebuffer as explicit little-endian words.
    /// This is the canonical platform-independent byte representation used by
    /// rasterizer fingerprints and raw framebuffer comparisons.
    /// </summary>
    public byte[] CopyPackedLittleEndianBytes()
    {
        byte[] bytes = new byte[checked(_pixels.Length * sizeof(ushort))];
        for (int index = 0; index < _pixels.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort)), _pixels[index]);
        return bytes;
    }

    private int GetPixelIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Pixel X is outside the framebuffer.");
        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, "Pixel Y is outside the framebuffer.");
        return checked((y * Width) + x);
    }
}
