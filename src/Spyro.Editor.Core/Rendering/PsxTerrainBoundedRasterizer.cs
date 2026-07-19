namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Truthful name for the first CPU terrain compositor slice. RGB5 writes,
/// texture zero/STP decisions, descriptor-local ABR, bounded RGB5-by-RGB8
/// modulation, native 4x4 GPU dithering, painter sequencing, and the documented
/// integer top-left fill rule are deterministic. The App command builder uses the
/// narrower <see cref="NativeTerrainOrderingTableContract"/> for native LP/HP
/// base buckets and coarse terrain pass phases. Native HQ tile-local buckets,
/// GTE projection, clipping, and full retail ordering-table
/// reconstruction remain outside this contract.
/// </summary>
public static class PsxTerrainBoundedRasterContract
{
    public const string Name = "psx-terrain-blend-dither-exact-bounded-ot-v2";
    public const string PainterOrdering = "ot-bucket-descending-insert-sequence-ascending-v1";
    public const string CoverageRule = "integer-vertices-half-pixel-samples-top-left-v1";
    public const string TextureModulation = "rgb5-times-rgb8-shift4-then-native-4x4-dither-shift3-clamp-v2";
    public const string Dithering = PsxGpuDither.Contract;
}

public readonly record struct PsxScreenPoint(int X, int Y);

public readonly record struct PsxLowPolyRasterVertex
{
    public PsxLowPolyRasterVertex(PsxScreenPoint position, PsxRgb8 color)
    {
        Position = position;
        Color = color;
    }

    /// <summary>
    /// Compatibility constructor for RGB5 fixtures. Expanding to the center of
    /// each eight-value quantization bin makes the supplied five-bit value
    /// stable across all native dither offsets (-4 through +3). Native callers
    /// should retain their original RGB8 polygon colors.
    /// </summary>
    public PsxLowPolyRasterVertex(PsxScreenPoint position, PsxRgb5 color)
        : this(position, new PsxRgb8(
            (color.Red << 3) + 4,
            (color.Green << 3) + 4,
            (color.Blue << 3) + 4))
    {
    }

    public PsxScreenPoint Position { get; }
    public PsxRgb8 Color { get; }
}

public readonly record struct PsxTexturedRasterVertex
{
    /// <summary>
    /// Backward-compatible vertex constructor. RGB8 0x80 is neutral under the
    /// bounded modulation equation, so existing unshaded fixtures are unchanged.
    /// </summary>
    public PsxTexturedRasterVertex(PsxScreenPoint position, int u, int v)
        : this(position, u, v, PsxRgb8.NeutralModulation)
    {
    }

    public PsxTexturedRasterVertex(PsxScreenPoint position, int u, int v, PsxRgb8 modulation)
    {
        Position = position;
        U = u;
        V = v;
        Modulation = modulation;
    }

    public PsxScreenPoint Position { get; }
    public int U { get; }
    public int V { get; }
    public PsxRgb8 Modulation { get; }
}

/// <summary>
/// Immutable logical texture descriptor. Raw PSX555 words retain zero-word
/// transparency and STP, while ABR remains descriptor-local instead of being
/// incorrectly promoted to a texture-wide or frame-wide setting.
/// </summary>
public sealed class PsxTerrainTextureDescriptor
{
    private readonly ushort[] _rawWords;

    public PsxTerrainTextureDescriptor(
        int width,
        int height,
        ReadOnlySpan<ushort> rawWords,
        PsxSemiTransparencyMode abr)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Texture width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Texture height must be positive.");
        if ((uint)abr > 3u)
            throw new ArgumentOutOfRangeException(nameof(abr), abr, "A PSX ABR mode must be between 0 and 3.");

        int requiredWordCount = checked(width * height);
        if (rawWords.Length != requiredWordCount)
        {
            throw new ArgumentException(
                $"Texture payload has {rawWords.Length} words; {requiredWordCount} are required.",
                nameof(rawWords));
        }

        Width = width;
        Height = height;
        Abr = abr;
        _rawWords = rawWords.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public PsxSemiTransparencyMode Abr { get; }

    /// <summary>
    /// Samples the descriptor's logical tile with positive wrapping. Native
    /// texture-page/window addressing is a later command-builder concern.
    /// </summary>
    public Psx555TextureWord SampleWrapped(int u, int v)
    {
        int wrappedU = PositiveModulo(u, Width);
        int wrappedV = PositiveModulo(v, Height);
        return new Psx555TextureWord(_rawWords[(wrappedV * Width) + wrappedU]);
    }

    public ReadOnlySpan<ushort> AsRawWords() => _rawWords;

    private static int PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

public abstract class PsxTerrainRasterPrimitive
{
    private protected PsxTerrainRasterPrimitive()
    {
    }
}

/// <summary>
/// One untextured LP Gouraud triangle. When semitransparency is enabled every
/// covered sample blends; LP has no texture STP gate.
/// </summary>
public sealed class PsxLowPolyRasterTriangle : PsxTerrainRasterPrimitive
{
    public PsxLowPolyRasterTriangle(
        PsxLowPolyRasterVertex vertex0,
        PsxLowPolyRasterVertex vertex1,
        PsxLowPolyRasterVertex vertex2,
        bool semiTransparent,
        PsxSemiTransparencyMode abr)
    {
        if ((uint)abr > 3u)
            throw new ArgumentOutOfRangeException(nameof(abr), abr, "A PSX ABR mode must be between 0 and 3.");

        Vertex0 = vertex0;
        Vertex1 = vertex1;
        Vertex2 = vertex2;
        SemiTransparent = semiTransparent;
        Abr = abr;
    }

    public PsxLowPolyRasterVertex Vertex0 { get; }
    public PsxLowPolyRasterVertex Vertex1 { get; }
    public PsxLowPolyRasterVertex Vertex2 { get; }
    public bool SemiTransparent { get; }
    public PsxSemiTransparencyMode Abr { get; }
}

/// <summary>
/// One textured triangle whose descriptor supplies both raw PSX555 texels and
/// its local ABR mode. Per-vertex RGB8 is interpolated and applies the bounded
/// shift-four plus native dither/shift-three modulation contract before
/// STP/ABR. Native GTE color interpolation details remain outside this slice.
/// </summary>
public sealed class PsxTexturedRasterTriangle : PsxTerrainRasterPrimitive
{
    public PsxTexturedRasterTriangle(
        PsxTexturedRasterVertex vertex0,
        PsxTexturedRasterVertex vertex1,
        PsxTexturedRasterVertex vertex2,
        PsxTerrainTextureDescriptor descriptor,
        bool primitiveSemiTransparent)
    {
        Vertex0 = vertex0;
        Vertex1 = vertex1;
        Vertex2 = vertex2;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        PrimitiveSemiTransparent = primitiveSemiTransparent;
    }

    public PsxTexturedRasterVertex Vertex0 { get; }
    public PsxTexturedRasterVertex Vertex1 { get; }
    public PsxTexturedRasterVertex Vertex2 { get; }
    public PsxTerrainTextureDescriptor Descriptor { get; }
    public bool PrimitiveSemiTransparent { get; }
}

/// <summary>
/// A command in the current bounded terrain painter stream. Buckets execute
/// from largest to smallest; commands tied in a bucket execute in ascending
/// explicit insertion sequence. Duplicate sequences in one bucket fail closed.
/// This is deterministic but is not a claim of reconstructed retail OT order.
/// </summary>
public sealed class PsxTerrainBoundedRenderCommand
{
    public PsxTerrainBoundedRenderCommand(
        int otBucket,
        int insertSequence,
        PsxTerrainRasterPrimitive primitive)
    {
        if (insertSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(insertSequence),
                insertSequence,
                "Painter insertion sequence cannot be negative.");
        }

        OtBucket = otBucket;
        InsertSequence = insertSequence;
        Primitive = primitive ?? throw new ArgumentNullException(nameof(primitive));
    }

    public int OtBucket { get; }
    public int InsertSequence { get; }
    public PsxTerrainRasterPrimitive Primitive { get; }
}

public readonly record struct PsxTerrainPainterSequenceEntry(
    int OtBucket,
    int InsertSequence,
    string PrimitiveKind);

public sealed record PsxTerrainRasterStatistics(
    int CommandsSubmitted,
    int CommandsExecuted,
    int DegenerateTriangles,
    long CoveredSamples,
    long FramebufferWrites,
    long TransparentTexelSkips,
    long OpaqueWrites,
    long SemiTransparentWrites,
    long Abr0Writes,
    long Abr1Writes,
    long Abr2Writes,
    long Abr3Writes)
{
    public long GetAbrWriteCount(PsxSemiTransparencyMode mode) => mode switch
    {
        PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground => Abr0Writes,
        PsxSemiTransparencyMode.BackgroundPlusForeground => Abr1Writes,
        PsxSemiTransparencyMode.BackgroundMinusForeground => Abr2Writes,
        PsxSemiTransparencyMode.BackgroundPlusQuarterForeground => Abr3Writes,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "A PSX ABR mode must be between 0 and 3.")
    };
}

public sealed record PsxTerrainRasterResult(
    PsxTerrainRasterStatistics Statistics,
    IReadOnlyList<PsxTerrainPainterSequenceEntry> PainterSequence);

/// <summary>
/// Avalonia-independent RGB5 terrain painter for the bounded-v2 contract.
/// Triangles use integer screen vertices, half-pixel sample positions, and a
/// top-left edge rule. There is no Z-buffer: commands paint in the explicit
/// bounded ordering stream.
/// </summary>
public static class PsxTerrainBoundedRasterizer
{
    private const int MaximumAbsoluteFixtureCoordinate = 1_000_000;

    public static PsxTerrainRasterResult Paint(
        PsxRgb5Framebuffer framebuffer,
        IReadOnlyList<PsxTerrainBoundedRenderCommand> commands)
    {
        return Paint(framebuffer, commands, capturePainterSequence: true);
    }

    /// <summary>
    /// Paints a deterministic command stream. Production callers may omit the
    /// per-command diagnostic sequence to avoid one frame-sized allocation;
    /// command validation, ordering, pixels, and statistics remain identical.
    /// </summary>
    public static PsxTerrainRasterResult Paint(
        PsxRgb5Framebuffer framebuffer,
        IReadOnlyList<PsxTerrainBoundedRenderCommand> commands,
        bool capturePainterSequence)
    {
        ArgumentNullException.ThrowIfNull(framebuffer);
        ArgumentNullException.ThrowIfNull(commands);

        var ordered = new PsxTerrainBoundedRenderCommand[commands.Count];
        for (int index = 0; index < commands.Count; index++)
        {
            PsxTerrainBoundedRenderCommand command = commands[index] ??
                throw new ArgumentException($"Command {index} is null.", nameof(commands));
            ordered[index] = command;
        }

        Array.Sort(ordered, static (left, right) =>
        {
            int bucketOrder = right.OtBucket.CompareTo(left.OtBucket);
            return bucketOrder != 0
                ? bucketOrder
                : left.InsertSequence.CompareTo(right.InsertSequence);
        });
        for (int index = 1; index < ordered.Length; index++)
        {
            PsxTerrainBoundedRenderCommand previous = ordered[index - 1];
            PsxTerrainBoundedRenderCommand current = ordered[index];
            if (previous.OtBucket == current.OtBucket &&
                previous.InsertSequence == current.InsertSequence)
            {
                throw new ArgumentException(
                    $"Bucket {current.OtBucket} contains duplicate insertion sequence {current.InsertSequence}.",
                    nameof(commands));
            }
        }

        var counters = new MutableStatistics(commands.Count);
        PsxTerrainPainterSequenceEntry[]? sequence = capturePainterSequence
            ? new PsxTerrainPainterSequenceEntry[ordered.Length]
            : null;
        for (int index = 0; index < ordered.Length; index++)
        {
            PsxTerrainBoundedRenderCommand command = ordered[index];
            counters.CommandsExecuted++;
            switch (command.Primitive)
            {
                case PsxLowPolyRasterTriangle lowPoly:
                    if (sequence != null)
                    {
                        sequence[index] = new PsxTerrainPainterSequenceEntry(
                            command.OtBucket,
                            command.InsertSequence,
                            nameof(PsxLowPolyRasterTriangle));
                    }
                    PaintLowPolyTriangle(framebuffer, lowPoly, counters);
                    break;
                case PsxTexturedRasterTriangle textured:
                    if (sequence != null)
                    {
                        sequence[index] = new PsxTerrainPainterSequenceEntry(
                            command.OtBucket,
                            command.InsertSequence,
                            nameof(PsxTexturedRasterTriangle));
                    }
                    PaintTexturedTriangle(framebuffer, textured, counters);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported bounded terrain primitive {command.Primitive.GetType().FullName}.");
            }
        }

        return new PsxTerrainRasterResult(
            counters.Freeze(),
            sequence ?? Array.Empty<PsxTerrainPainterSequenceEntry>());
    }

    /// <summary>
    /// Applies the bounded textured-Gouraud modulation equation. A shade of
    /// 128 is neutral; values above it brighten and clamp in RGB5.
    /// </summary>
    public static PsxRgb5 ModulateTextureColor(PsxRgb5 textureColor, PsxRgb8 shade) =>
        new(
            ModulateTextureChannel5(textureColor.Red, shade.Red),
            ModulateTextureChannel5(textureColor.Green, shade.Green),
            ModulateTextureChannel5(textureColor.Blue, shade.Blue));

    public static byte ModulateTextureChannel5(int textureChannel, int shadeChannel)
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

        return (byte)Math.Min(31, (textureChannel * shadeChannel) >> 7);
    }

    private static void PaintLowPolyTriangle(
        PsxRgb5Framebuffer framebuffer,
        PsxLowPolyRasterTriangle triangle,
        MutableStatistics counters)
    {
        PsxLowPolyRasterVertex vertex0 = triangle.Vertex0;
        PsxLowPolyRasterVertex vertex1 = triangle.Vertex1;
        PsxLowPolyRasterVertex vertex2 = triangle.Vertex2;
        if (!NormalizeWinding(ref vertex0, ref vertex1, ref vertex2))
        {
            counters.DegenerateTriangles++;
            return;
        }

        if (!TryPrepareCoverage(
                framebuffer,
                vertex0.Position,
                vertex1.Position,
                vertex2.Position,
                out CoverageSetup coverage))
        {
            return;
        }

        for (int y = coverage.MinimumY; y <= coverage.MaximumY; y++)
        {
            long sampleY = (y * 2L) + 1L;
            for (int x = coverage.MinimumX; x <= coverage.MaximumX; x++)
            {
                long sampleX = (x * 2L) + 1L;
                long weight0 = Edge(coverage.X1, coverage.Y1, coverage.X2, coverage.Y2, sampleX, sampleY);
                long weight1 = Edge(coverage.X2, coverage.Y2, coverage.X0, coverage.Y0, sampleX, sampleY);
                long weight2 = Edge(coverage.X0, coverage.Y0, coverage.X1, coverage.Y1, sampleX, sampleY);
                if (!IsInsideEdge(weight0, coverage.Edge0IsTopLeft) ||
                    !IsInsideEdge(weight1, coverage.Edge1IsTopLeft) ||
                    !IsInsideEdge(weight2, coverage.Edge2IsTopLeft))
                {
                    continue;
                }

                counters.CoveredSamples++;
                PsxRgb8 interpolatedColor = InterpolateColor(
                    vertex0.Color,
                    vertex1.Color,
                    vertex2.Color,
                    weight0,
                    weight1,
                    weight2,
                    coverage.Area);
                PsxRgb5 foreground = PsxGpuDither.QuantizeColor(interpolatedColor, x, y);
                if (triangle.SemiTransparent)
                {
                    PsxRgb5 background = framebuffer.GetPixel(x, y);
                    framebuffer.SetPixel(x, y, PsxTerrainBlendKernel.Blend(background, foreground, triangle.Abr));
                    counters.RecordSemiTransparentWrite(triangle.Abr);
                }
                else
                {
                    framebuffer.SetPixel(x, y, foreground);
                    counters.RecordOpaqueWrite();
                }
            }
        }
    }

    private static void PaintTexturedTriangle(
        PsxRgb5Framebuffer framebuffer,
        PsxTexturedRasterTriangle triangle,
        MutableStatistics counters)
    {
        PsxTexturedRasterVertex vertex0 = triangle.Vertex0;
        PsxTexturedRasterVertex vertex1 = triangle.Vertex1;
        PsxTexturedRasterVertex vertex2 = triangle.Vertex2;
        if (!NormalizeWinding(ref vertex0, ref vertex1, ref vertex2))
        {
            counters.DegenerateTriangles++;
            return;
        }

        if (!TryPrepareCoverage(
                framebuffer,
                vertex0.Position,
                vertex1.Position,
                vertex2.Position,
                out CoverageSetup coverage))
        {
            return;
        }

        for (int y = coverage.MinimumY; y <= coverage.MaximumY; y++)
        {
            long sampleY = (y * 2L) + 1L;
            for (int x = coverage.MinimumX; x <= coverage.MaximumX; x++)
            {
                long sampleX = (x * 2L) + 1L;
                long weight0 = Edge(coverage.X1, coverage.Y1, coverage.X2, coverage.Y2, sampleX, sampleY);
                long weight1 = Edge(coverage.X2, coverage.Y2, coverage.X0, coverage.Y0, sampleX, sampleY);
                long weight2 = Edge(coverage.X0, coverage.Y0, coverage.X1, coverage.Y1, sampleX, sampleY);
                if (!IsInsideEdge(weight0, coverage.Edge0IsTopLeft) ||
                    !IsInsideEdge(weight1, coverage.Edge1IsTopLeft) ||
                    !IsInsideEdge(weight2, coverage.Edge2IsTopLeft))
                {
                    continue;
                }

                counters.CoveredSamples++;
                int u = InterpolateCoordinate(
                    vertex0.U,
                    vertex1.U,
                    vertex2.U,
                    weight0,
                    weight1,
                    weight2,
                    coverage.Area);
                int v = InterpolateCoordinate(
                    vertex0.V,
                    vertex1.V,
                    vertex2.V,
                    weight0,
                    weight1,
                    weight2,
                    coverage.Area);
                Psx555TextureWord textureWord = triangle.Descriptor.SampleWrapped(u, v);
                PsxRgb8 modulation = InterpolateModulation(
                    vertex0.Modulation,
                    vertex1.Modulation,
                    vertex2.Modulation,
                    weight0,
                    weight1,
                    weight2,
                    coverage.Area);
                PsxRgb5 shadedForeground = PsxGpuDither.ModulateTextureColor(textureWord.Color, modulation, x, y);
                PsxTerrainTexturedPixelResult pixel = PsxTerrainBlendKernel.ResolveTexturedPixel(
                    framebuffer.GetPixel(x, y),
                    textureWord,
                    shadedForeground,
                    triangle.PrimitiveSemiTransparent,
                    triangle.Descriptor.Abr);

                if (!pixel.WritesFramebuffer)
                {
                    counters.TransparentTexelSkips++;
                    continue;
                }

                framebuffer.SetPixel(x, y, pixel.OutputColor);
                if (pixel.Disposition == PsxTexturedPixelDisposition.SemiTransparent)
                    counters.RecordSemiTransparentWrite(triangle.Descriptor.Abr);
                else
                    counters.RecordOpaqueWrite();
            }
        }
    }

    private static bool TryPrepareCoverage(
        PsxRgb5Framebuffer framebuffer,
        PsxScreenPoint vertex0,
        PsxScreenPoint vertex1,
        PsxScreenPoint vertex2,
        out CoverageSetup coverage)
    {
        ValidatePoint(vertex0);
        ValidatePoint(vertex1);
        ValidatePoint(vertex2);

        long x0 = vertex0.X * 2L;
        long y0 = vertex0.Y * 2L;
        long x1 = vertex1.X * 2L;
        long y1 = vertex1.Y * 2L;
        long x2 = vertex2.X * 2L;
        long y2 = vertex2.Y * 2L;
        long area = Edge(x0, y0, x1, y1, x2, y2);
        if (area <= 0)
            throw new InvalidOperationException("Triangle winding normalization failed.");

        int minimumX = Math.Max(0, Math.Min(vertex0.X, Math.Min(vertex1.X, vertex2.X)));
        int minimumY = Math.Max(0, Math.Min(vertex0.Y, Math.Min(vertex1.Y, vertex2.Y)));
        int maximumX = Math.Min(
            framebuffer.Width - 1,
            Math.Max(vertex0.X, Math.Max(vertex1.X, vertex2.X)) - 1);
        int maximumY = Math.Min(
            framebuffer.Height - 1,
            Math.Max(vertex0.Y, Math.Max(vertex1.Y, vertex2.Y)) - 1);
        if (minimumX > maximumX || minimumY > maximumY)
        {
            coverage = default;
            return false;
        }

        coverage = new CoverageSetup(
            x0,
            y0,
            x1,
            y1,
            x2,
            y2,
            area,
            minimumX,
            minimumY,
            maximumX,
            maximumY,
            IsTopLeft(vertex1, vertex2),
            IsTopLeft(vertex2, vertex0),
            IsTopLeft(vertex0, vertex1));
        return true;
    }

    private static PsxRgb8 InterpolateColor(
        PsxRgb8 color0,
        PsxRgb8 color1,
        PsxRgb8 color2,
        long weight0,
        long weight1,
        long weight2,
        long area) =>
        new(
            InterpolateChannel(color0.Red, color1.Red, color2.Red, weight0, weight1, weight2, area),
            InterpolateChannel(color0.Green, color1.Green, color2.Green, weight0, weight1, weight2, area),
            InterpolateChannel(color0.Blue, color1.Blue, color2.Blue, weight0, weight1, weight2, area));

    private static int InterpolateChannel(
        int value0,
        int value1,
        int value2,
        long weight0,
        long weight1,
        long weight2,
        long area)
    {
        long numerator = (weight0 * value0) + (weight1 * value1) + (weight2 * value2);
        return Math.Clamp((int)(numerator / area), 0, byte.MaxValue);
    }

    private static PsxRgb8 InterpolateModulation(
        PsxRgb8 color0,
        PsxRgb8 color1,
        PsxRgb8 color2,
        long weight0,
        long weight1,
        long weight2,
        long area) =>
        new(
            InterpolateModulationChannel(color0.Red, color1.Red, color2.Red, weight0, weight1, weight2, area),
            InterpolateModulationChannel(color0.Green, color1.Green, color2.Green, weight0, weight1, weight2, area),
            InterpolateModulationChannel(color0.Blue, color1.Blue, color2.Blue, weight0, weight1, weight2, area));

    private static int InterpolateModulationChannel(
        int value0,
        int value1,
        int value2,
        long weight0,
        long weight1,
        long weight2,
        long area)
    {
        long numerator = (weight0 * value0) + (weight1 * value1) + (weight2 * value2);
        return Math.Clamp((int)(numerator / area), 0, byte.MaxValue);
    }

    private static int InterpolateCoordinate(
        int value0,
        int value1,
        int value2,
        long weight0,
        long weight1,
        long weight2,
        long area)
    {
        long numerator = checked((weight0 * value0) + (weight1 * value1) + (weight2 * value2));
        long result = FloorDivide(numerator, area);
        if (result is < int.MinValue or > int.MaxValue)
            throw new OverflowException("Interpolated texture coordinate exceeds Int32 range.");
        return (int)result;
    }

    private static long FloorDivide(long numerator, long positiveDenominator)
    {
        long quotient = numerator / positiveDenominator;
        long remainder = numerator % positiveDenominator;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static bool NormalizeWinding(
        ref PsxLowPolyRasterVertex vertex0,
        ref PsxLowPolyRasterVertex vertex1,
        ref PsxLowPolyRasterVertex vertex2)
    {
        ValidatePoint(vertex0.Position);
        ValidatePoint(vertex1.Position);
        ValidatePoint(vertex2.Position);
        long area = SignedArea(vertex0.Position, vertex1.Position, vertex2.Position);
        if (area == 0)
            return false;
        if (area < 0)
            (vertex1, vertex2) = (vertex2, vertex1);
        return true;
    }

    private static bool NormalizeWinding(
        ref PsxTexturedRasterVertex vertex0,
        ref PsxTexturedRasterVertex vertex1,
        ref PsxTexturedRasterVertex vertex2)
    {
        ValidatePoint(vertex0.Position);
        ValidatePoint(vertex1.Position);
        ValidatePoint(vertex2.Position);
        long area = SignedArea(vertex0.Position, vertex1.Position, vertex2.Position);
        if (area == 0)
            return false;
        if (area < 0)
            (vertex1, vertex2) = (vertex2, vertex1);
        return true;
    }

    private static long SignedArea(PsxScreenPoint vertex0, PsxScreenPoint vertex1, PsxScreenPoint vertex2) =>
        Edge(
            vertex0.X * 2L,
            vertex0.Y * 2L,
            vertex1.X * 2L,
            vertex1.Y * 2L,
            vertex2.X * 2L,
            vertex2.Y * 2L);

    private static long Edge(long ax, long ay, long bx, long by, long px, long py) =>
        ((bx - ax) * (py - ay)) - ((by - ay) * (px - ax));

    // In the editor's y-down screen domain, an upward edge or a horizontal
    // rightward edge is the inclusive half of the standard top-left rule.
    private static bool IsTopLeft(PsxScreenPoint start, PsxScreenPoint end)
    {
        int deltaY = end.Y - start.Y;
        int deltaX = end.X - start.X;
        return deltaY < 0 || (deltaY == 0 && deltaX > 0);
    }

    private static bool IsInsideEdge(long edgeValue, bool isTopLeft) =>
        edgeValue > 0 || (edgeValue == 0 && isTopLeft);

    private static void ValidatePoint(PsxScreenPoint point)
    {
        if (point.X is < -MaximumAbsoluteFixtureCoordinate or > MaximumAbsoluteFixtureCoordinate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                point,
                $"Screen X must stay within +/-{MaximumAbsoluteFixtureCoordinate} for overflow-safe bounded rasterization.");
        }
        if (point.Y is < -MaximumAbsoluteFixtureCoordinate or > MaximumAbsoluteFixtureCoordinate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                point,
                $"Screen Y must stay within +/-{MaximumAbsoluteFixtureCoordinate} for overflow-safe bounded rasterization.");
        }
    }

    private readonly record struct CoverageSetup(
        long X0,
        long Y0,
        long X1,
        long Y1,
        long X2,
        long Y2,
        long Area,
        int MinimumX,
        int MinimumY,
        int MaximumX,
        int MaximumY,
        bool Edge0IsTopLeft,
        bool Edge1IsTopLeft,
        bool Edge2IsTopLeft);

    private sealed class MutableStatistics
    {
        public MutableStatistics(int commandsSubmitted)
        {
            CommandsSubmitted = commandsSubmitted;
        }

        public int CommandsSubmitted { get; }
        public int CommandsExecuted { get; set; }
        public int DegenerateTriangles { get; set; }
        public long CoveredSamples { get; set; }
        public long FramebufferWrites { get; private set; }
        public long TransparentTexelSkips { get; set; }
        public long OpaqueWrites { get; private set; }
        public long SemiTransparentWrites { get; private set; }
        public long Abr0Writes { get; private set; }
        public long Abr1Writes { get; private set; }
        public long Abr2Writes { get; private set; }
        public long Abr3Writes { get; private set; }

        public void RecordOpaqueWrite()
        {
            FramebufferWrites++;
            OpaqueWrites++;
        }

        public void RecordSemiTransparentWrite(PsxSemiTransparencyMode mode)
        {
            FramebufferWrites++;
            SemiTransparentWrites++;
            switch (mode)
            {
                case PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground:
                    Abr0Writes++;
                    break;
                case PsxSemiTransparencyMode.BackgroundPlusForeground:
                    Abr1Writes++;
                    break;
                case PsxSemiTransparencyMode.BackgroundMinusForeground:
                    Abr2Writes++;
                    break;
                case PsxSemiTransparencyMode.BackgroundPlusQuarterForeground:
                    Abr3Writes++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "A PSX ABR mode must be between 0 and 3.");
            }
        }

        public PsxTerrainRasterStatistics Freeze() => new(
            CommandsSubmitted,
            CommandsExecuted,
            DegenerateTriangles,
            CoveredSamples,
            FramebufferWrites,
            TransparentTexelSkips,
            OpaqueWrites,
            SemiTransparentWrites,
            Abr0Writes,
            Abr1Writes,
            Abr2Writes,
            Abr3Writes);
    }
}
