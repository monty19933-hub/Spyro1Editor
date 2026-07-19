namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Native PlayStation GPU semi-transparency modes encoded by the two ABR bits.
/// </summary>
public enum PsxSemiTransparencyMode : byte
{
    HalfBackgroundPlusHalfForeground = 0,
    BackgroundPlusForeground = 1,
    BackgroundMinusForeground = 2,
    BackgroundPlusQuarterForeground = 3
}

/// <summary>
/// Per-pixel action selected from the raw texture word and primitive command.
/// </summary>
public enum PsxTexturedPixelDisposition : byte
{
    Transparent = 0,
    Opaque = 1,
    SemiTransparent = 2
}

/// <summary>
/// Material classification encoded by the low byte of an HP terrain face's
/// native word at +0x08.
/// </summary>
public enum PsxTerrainPrimitiveMaterialKind : byte
{
    OpaqueTextured = 0,
    SemiTransparentTextured = 1,
    OpaqueUntexturedSentinel = 2
}

/// <summary>
/// A framebuffer/foreground color in the PS1 GPU's unsigned five-bit RGB
/// channel domain. Bit 15 is intentionally not part of framebuffer color math.
/// </summary>
public readonly record struct PsxRgb5
{
    public PsxRgb5(int red, int green, int blue)
    {
        Red = RequireChannel(red, nameof(red));
        Green = RequireChannel(green, nameof(green));
        Blue = RequireChannel(blue, nameof(blue));
    }

    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }

    public ushort PackedWord => (ushort)(Red | (Green << 5) | (Blue << 10));

    /// <summary>
    /// Decodes only the RGB channels. A source texture word's raw STP bit must
    /// remain in <see cref="Psx555TextureWord"/>, not in this framebuffer value.
    /// </summary>
    public static PsxRgb5 FromPackedWord(ushort word) =>
        new(word & 0x1F, (word >> 5) & 0x1F, (word >> 10) & 0x1F);

    private static byte RequireChannel(int value, string parameterName)
    {
        if ((uint)value > 31u)
            throw new ArgumentOutOfRangeException(parameterName, value, "A PSX RGB5 channel must be between 0 and 31.");
        return (byte)value;
    }
}

/// <summary>
/// Per-vertex textured-primitive modulation color. The PS1 textured-Gouraud
/// path treats 0x80 as the neutral multiplier for each channel.
/// </summary>
public readonly record struct PsxRgb8
{
    public static PsxRgb8 NeutralModulation { get; } = new(0x80, 0x80, 0x80);

    public PsxRgb8(int red, int green, int blue)
    {
        Red = RequireChannel(red, nameof(red));
        Green = RequireChannel(green, nameof(green));
        Blue = RequireChannel(blue, nameof(blue));
    }

    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }

    private static byte RequireChannel(int value, string parameterName)
    {
        if ((uint)value > byte.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, value, "An RGB8 modulation channel must be between 0 and 255.");
        return (byte)value;
    }
}

/// <summary>
/// An unmodified 16-bit PSX texture/CLUT word. The raw value preserves both
/// zero-word transparency and bit-15 STP state.
/// </summary>
public readonly record struct Psx555TextureWord(ushort RawWord)
{
    public bool IsTransparent => RawWord == 0;
    public bool HasSemiTransparencyBit => (RawWord & 0x8000) != 0;
    public PsxRgb5 Color => PsxRgb5.FromPackedWord(RawWord);
}

/// <summary>
/// Typed interpretation of one HP terrain face material byte.
/// </summary>
public readonly record struct PsxTerrainPrimitiveClassification(
    byte RawMaterialByte,
    PsxTerrainPrimitiveMaterialKind Kind,
    int TextureId)
{
    public bool IsTextured => Kind != PsxTerrainPrimitiveMaterialKind.OpaqueUntexturedSentinel;
    public bool PrimitiveSemiTransparent => Kind == PsxTerrainPrimitiveMaterialKind.SemiTransparentTextured;
    public bool IsOpaqueUntexturedSentinel => Kind == PsxTerrainPrimitiveMaterialKind.OpaqueUntexturedSentinel;
}

/// <summary>
/// Result of applying texture-word transparency/STP classification and, when
/// required, an ABR equation to one framebuffer pixel.
/// </summary>
public readonly record struct PsxTerrainTexturedPixelResult(
    Psx555TextureWord TextureWord,
    PsxTexturedPixelDisposition Disposition,
    bool WritesFramebuffer,
    PsxRgb5 OutputColor);

/// <summary>
/// Exact integer blend/classification kernel for a future PS1 terrain software
/// framebuffer. Projection, raster coverage, ordering-table traversal, and
/// presentation are intentionally out of scope. The bounded rasterizer supplies
/// its explicitly documented modulated and dithered foreground through the
/// overload below.
/// </summary>
public static class PsxTerrainBlendKernel
{
    public const byte OpaqueUntexturedMaterialSentinel = 0xFF;

    /// <summary>
    /// Interprets the native HP material byte without confusing 0xFF with
    /// semitransparent texture 127.
    /// </summary>
    public static PsxTerrainPrimitiveClassification ClassifyHighPolyMaterialByte(byte materialByte)
    {
        if (materialByte == OpaqueUntexturedMaterialSentinel)
        {
            return new PsxTerrainPrimitiveClassification(
                materialByte,
                PsxTerrainPrimitiveMaterialKind.OpaqueUntexturedSentinel,
                TextureId: -1);
        }

        bool semiTransparent = (materialByte & 0x80) != 0;
        return new PsxTerrainPrimitiveClassification(
            materialByte,
            semiTransparent
                ? PsxTerrainPrimitiveMaterialKind.SemiTransparentTextured
                : PsxTerrainPrimitiveMaterialKind.OpaqueTextured,
            TextureId: materialByte & 0x7F);
    }

    /// <summary>
    /// Applies the PS1 textured-pixel decision matrix. Only raw word 0x0000 is
    /// intrinsically transparent. STP matters only when the primitive command
    /// also enables semi-transparency.
    /// </summary>
    public static PsxTexturedPixelDisposition ClassifyTexturedPixel(
        Psx555TextureWord textureWord,
        bool primitiveSemiTransparent)
    {
        if (textureWord.IsTransparent)
            return PsxTexturedPixelDisposition.Transparent;
        if (!primitiveSemiTransparent || !textureWord.HasSemiTransparencyBit)
            return PsxTexturedPixelDisposition.Opaque;
        return PsxTexturedPixelDisposition.SemiTransparent;
    }

    /// <summary>
    /// Compatibility overload that treats the texture word's RGB channels as
    /// an already-shaded foreground. The bounded rasterizer uses the explicit
    /// shaded-foreground overload after applying its modulation contract.
    /// </summary>
    public static PsxTerrainTexturedPixelResult ResolveTexturedPixel(
        PsxRgb5 background,
        Psx555TextureWord textureWord,
        bool primitiveSemiTransparent,
        PsxSemiTransparencyMode mode)
    {
        return ResolveTexturedPixel(
            background,
            textureWord,
            textureWord.Color,
            primitiveSemiTransparent,
            mode);
    }

    /// <summary>
    /// Resolves a textured pixel after the caller has shaded its RGB foreground.
    /// Zero-word transparency and STP classification still come exclusively from
    /// the unmodified raw texture word.
    /// </summary>
    public static PsxTerrainTexturedPixelResult ResolveTexturedPixel(
        PsxRgb5 background,
        Psx555TextureWord textureWord,
        PsxRgb5 shadedForeground,
        bool primitiveSemiTransparent,
        PsxSemiTransparencyMode mode)
    {
        ValidateMode(mode);
        PsxTexturedPixelDisposition disposition = ClassifyTexturedPixel(textureWord, primitiveSemiTransparent);
        return disposition switch
        {
            PsxTexturedPixelDisposition.Transparent => new PsxTerrainTexturedPixelResult(
                textureWord,
                disposition,
                WritesFramebuffer: false,
                background),
            PsxTexturedPixelDisposition.Opaque => new PsxTerrainTexturedPixelResult(
                textureWord,
                disposition,
                WritesFramebuffer: true,
                shadedForeground),
            _ => new PsxTerrainTexturedPixelResult(
                textureWord,
                disposition,
                WritesFramebuffer: true,
                Blend(background, shadedForeground, mode))
        };
    }

    public static PsxRgb5 Blend(
        PsxRgb5 background,
        PsxRgb5 foreground,
        PsxSemiTransparencyMode mode)
    {
        ValidateMode(mode);
        return new PsxRgb5(
            BlendChannel5(background.Red, foreground.Red, mode),
            BlendChannel5(background.Green, foreground.Green, mode),
            BlendChannel5(background.Blue, foreground.Blue, mode));
    }

    /// <summary>
    /// Applies one native ABR equation in the five-bit channel domain. Division
    /// truncates each term independently, matching bg/2 + fg/2 and bg + fg/4.
    /// </summary>
    public static byte BlendChannel5(
        int background,
        int foreground,
        PsxSemiTransparencyMode mode)
    {
        byte bg = RequireChannel(background, nameof(background));
        byte fg = RequireChannel(foreground, nameof(foreground));
        ValidateMode(mode);

        int result = mode switch
        {
            PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground => (bg >> 1) + (fg >> 1),
            PsxSemiTransparencyMode.BackgroundPlusForeground => bg + fg,
            PsxSemiTransparencyMode.BackgroundMinusForeground => bg - fg,
            PsxSemiTransparencyMode.BackgroundPlusQuarterForeground => bg + (fg >> 2),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown PSX ABR mode.")
        };
        return (byte)Math.Clamp(result, 0, 31);
    }

    private static byte RequireChannel(int value, string parameterName)
    {
        if ((uint)value > 31u)
            throw new ArgumentOutOfRangeException(parameterName, value, "A PSX RGB5 channel must be between 0 and 31.");
        return (byte)value;
    }

    private static void ValidateMode(PsxSemiTransparencyMode mode)
    {
        if ((uint)mode > 3u)
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "A PSX ABR mode must be between 0 and 3.");
    }
}
