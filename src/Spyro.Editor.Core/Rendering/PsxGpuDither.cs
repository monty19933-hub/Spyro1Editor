namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Native-resolution PlayStation GPU color dithering used before an RGB8-ish
/// polygon result is quantized into the five-bit framebuffer channel domain.
/// Spyro enables DrawEnv.dtd for both display buffers, and terrain is emitted
/// as shaded or modulated polygons, so its terrain primitives take this path.
/// </summary>
public static class PsxGpuDither
{
    public const string Contract = "psx-gpu-native-4x4-dither-before-rgb5-v1";
    public const int MatrixSide = 4;
    public const int MaximumPreQuantizedChannel = 511;

    // GPU matrix, indexed by native framebuffer y then x.
    private static ReadOnlySpan<sbyte> Matrix =>
    [
        -4,  0, -3,  1,
         2, -2,  3, -1,
        -3,  1, -4,  0,
         3, -1,  2, -2
    ];

    public static int OffsetAt(int framebufferX, int framebufferY) =>
        Matrix[((framebufferY & 3) * MatrixSide) + (framebufferX & 3)];

    /// <summary>
    /// Matches the GPU/DuckStation LUT equation:
    /// clamp((value + matrix[y&amp;3][x&amp;3]) &gt;&gt; 3, 0, 31).
    /// </summary>
    public static byte QuantizeChannel(
        int preQuantizedChannel,
        int framebufferX,
        int framebufferY)
    {
        if ((uint)preQuantizedChannel > MaximumPreQuantizedChannel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preQuantizedChannel),
                preQuantizedChannel,
                $"A PSX pre-quantized channel must be between 0 and {MaximumPreQuantizedChannel}.");
        }

        int value = (preQuantizedChannel + OffsetAt(framebufferX, framebufferY)) >> 3;
        return (byte)Math.Clamp(value, 0, 31);
    }

    public static PsxRgb5 QuantizeColor(
        PsxRgb8 color,
        int framebufferX,
        int framebufferY) =>
        new(
            QuantizeChannel(color.Red, framebufferX, framebufferY),
            QuantizeChannel(color.Green, framebufferX, framebufferY),
            QuantizeChannel(color.Blue, framebufferX, framebufferY));

    /// <summary>
    /// Textured-Gouraud modulation follows the software GPU's 8-bit path:
    /// first (texel5 * shade8) &gt;&gt; 4, then the native dither/quantize step.
    /// </summary>
    public static byte ModulateTextureChannel(
        int textureChannel,
        int shadeChannel,
        int framebufferX,
        int framebufferY)
    {
        if ((uint)textureChannel > 31u)
        {
            throw new ArgumentOutOfRangeException(
                nameof(textureChannel),
                textureChannel,
                "A PSX texture channel must be between 0 and 31.");
        }
        if ((uint)shadeChannel > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shadeChannel),
                shadeChannel,
                "An RGB8 modulation channel must be between 0 and 255.");
        }

        int preQuantized = (textureChannel * shadeChannel) >> 4;
        return QuantizeChannel(preQuantized, framebufferX, framebufferY);
    }

    public static PsxRgb5 ModulateTextureColor(
        PsxRgb5 textureColor,
        PsxRgb8 shade,
        int framebufferX,
        int framebufferY) =>
        new(
            ModulateTextureChannel(textureColor.Red, shade.Red, framebufferX, framebufferY),
            ModulateTextureChannel(textureColor.Green, shade.Green, framebufferX, framebufferY),
            ModulateTextureChannel(textureColor.Blue, shade.Blue, framebufferX, framebufferY));
}
