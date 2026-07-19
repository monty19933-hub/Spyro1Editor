using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Decodes the generated Moby icon atlas once and removes its baked checkerboard
/// without changing the source asset. Background colors are learned exclusively
/// from the cells the atlas contract explicitly reserves as empty. Only palette
/// pixels connected to an individual contract cell's edge are cleared. Adaptive
/// sheets also use the empty cells' learned checker period and sample palette to
/// clear enclosed checker components while retaining non-periodic silver/white
/// subject detail.
/// </summary>
public static class MobyIconAtlasLoader
{
    private const int MinimumInteriorCheckerComponentPixels = 64;
    private const int MinimumCheckerParityGroupPixels = 8;
    private const double MinimumInteriorCheckerSampleColorFraction = 0.50;
    private const double MinimumInteriorCheckerParityDelta = 4.0;
    private const double LearnedCheckerParityDeltaFraction = 0.40;
    private const int MinimumIndependentEmptyCellsForPeriodicCleanup = 3;
    private const double MinimumLearnedCheckerParityDeltaForPeriodicCleanup = 10.0;

    private static readonly object CacheGate = new();
    private static readonly Dictionary<MobyIconAtlasCacheKey, MobyIconAtlasLoadResult> Cache = new();

    public static MobyIconAtlasLoadResult Load(string? path, MobyRasterIconAtlasContract atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        if (string.IsNullOrWhiteSpace(path))
            return Failure(atlas, "", "No Moby icon atlas path was supplied.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception)
        {
            return Failure(atlas, path, $"The Moby icon atlas path is invalid: {exception.Message}");
        }

        MobyIconAtlasCacheKey cacheKey = new(
            fullPath,
            atlas.Key,
            atlas.FileName,
            atlas.Columns,
            atlas.Rows,
            atlas.UsedCellCount);
        lock (CacheGate)
        {
            if (Cache.TryGetValue(cacheKey, out MobyIconAtlasLoadResult? cached))
                return cached;

            MobyIconAtlasLoadResult loaded = LoadUncached(fullPath, atlas);
            Cache[cacheKey] = loaded;
            return loaded;
        }
    }

    public static PixelRect GetCellPixelRect(
        PixelSize atlasSize,
        MobyRasterIconAtlasContract atlas,
        int atlasCell)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        if (!atlas.ContainsCell(atlasCell))
            throw new ArgumentOutOfRangeException(nameof(atlasCell));
        if (atlasSize.Width < atlas.Columns || atlasSize.Height < atlas.Rows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atlasSize),
                $"The atlas is too small for its {atlas.Columns} x {atlas.Rows} cell contract.");
        }

        int column = atlasCell % atlas.Columns;
        int row = atlasCell / atlas.Columns;
        int left = column * atlasSize.Width / atlas.Columns;
        int right = (column + 1) * atlasSize.Width / atlas.Columns;
        int top = row * atlasSize.Height / atlas.Rows;
        int bottom = (row + 1) * atlasSize.Height / atlas.Rows;
        return new PixelRect(left, top, right - left, bottom - top);
    }

    public static Rect GetCellSourceRect(
        PixelSize atlasSize,
        MobyRasterIconAtlasContract atlas,
        int atlasCell)
    {
        PixelRect pixelRect = GetCellPixelRect(atlasSize, atlas, atlasCell);
        return new Rect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height);
    }

    private static MobyIconAtlasLoadResult LoadUncached(
        string path,
        MobyRasterIconAtlasContract atlas)
    {
        if (!File.Exists(path))
            return Failure(atlas, path, "The optional Moby icon atlas file does not exist.");

        WriteableBitmap? output = null;
        try
        {
            using Bitmap source = new(path);
            PixelSize size = source.PixelSize;
            if (size.Width < atlas.Columns || size.Height < atlas.Rows)
            {
                return Failure(
                    atlas,
                    path,
                    $"The Moby icon atlas is only {size.Width} x {size.Height} pixels for its {atlas.Columns} x {atlas.Rows} contract.");
            }

            output = new WriteableBitmap(
                size,
                source.Dpi,
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul);

            using ILockedFramebuffer framebuffer = output.Lock();
            source.CopyPixels(framebuffer);
            if (framebuffer.Format != PixelFormat.Rgba8888)
                throw new NotSupportedException($"Expected RGBA8888 atlas pixels, received {framebuffer.Format}.");

            int byteCount = checked(framebuffer.RowBytes * size.Height);
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);

            int sourceTransparentPixels = CountTransparentPixels(pixels, framebuffer.RowBytes, size);
            (CheckerboardProfile checkerboard, int samplePixels, int sampleMatches) =
                DetectCheckerboardColors(pixels, framebuffer.RowBytes, size, atlas);

            bool[] connectedBackground = FindConnectedBackground(
                pixels,
                framebuffer.RowBytes,
                size,
                atlas,
                checkerboard);
            int connectedBackgroundPixels = 0;
            for (int index = 0; index < connectedBackground.Length; index++)
            {
                if (!connectedBackground[index])
                    continue;

                int x = index % size.Width;
                int y = index / size.Width;
                int offset = (y * framebuffer.RowBytes) + (x * 4);
                pixels[offset] = 0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = 0;
                pixels[offset + 3] = 0;
                connectedBackgroundPixels++;
            }

            int preservedBackgroundLikePixels = CountPreservedBackgroundLikePixels(
                pixels,
                framebuffer.RowBytes,
                size,
                checkerboard);
            int transparentPixels = CountTransparentPixels(pixels, framebuffer.RowBytes, size);
            IReadOnlyList<MobyIconAtlasCellDiagnostics> cells = BuildCellDiagnostics(
                pixels,
                framebuffer.RowBytes,
                size,
                atlas);

            Marshal.Copy(pixels, 0, framebuffer.Address, byteCount);
            MobyIconAtlasDiagnostics diagnostics = new(
                atlas,
                path,
                true,
                size.Width,
                size.Height,
                sourceTransparentPixels,
                samplePixels,
                sampleMatches,
                checkerboard.RepresentativeColors,
                checkerboard.MinimumLightChannel,
                checkerboard.MaximumChroma,
                connectedBackgroundPixels,
                preservedBackgroundLikePixels,
                transparentPixels,
                size.Width * size.Height,
                cells,
                "");
            return new MobyIconAtlasLoadResult(output, diagnostics);
        }
        catch (Exception exception)
        {
            output?.Dispose();
            return Failure(atlas, path, $"The optional Moby icon atlas could not be decoded safely: {exception.Message}");
        }
    }

    private static (CheckerboardProfile Profile, int SamplePixels, int SampleMatches) DetectCheckerboardColors(
        byte[] pixels,
        int rowBytes,
        PixelSize size,
        MobyRasterIconAtlasContract atlas)
    {
        Dictionary<int, int> histogram = new();
        int[] lightChannelHistogram = new int[256];
        int[] chromaHistogram = new int[256];
        int samplePixels = 0;
        for (int atlasCell = atlas.UsedCellCount; atlasCell < atlas.CellCount; atlasCell++)
        {
            PixelRect cell = GetCellPixelRect(size, atlas, atlasCell);
            for (int y = cell.Y; y < cell.Bottom; y++)
            {
                for (int x = cell.X; x < cell.Right; x++)
                {
                    int offset = (y * rowBytes) + (x * 4);
                    byte red = pixels[offset];
                    byte green = pixels[offset + 1];
                    byte blue = pixels[offset + 2];
                    int key = (red << 16) | (green << 8) | blue;
                    histogram[key] = histogram.GetValueOrDefault(key) + 1;
                    int minimum = Math.Min(red, Math.Min(green, blue));
                    int maximum = Math.Max(red, Math.Max(green, blue));
                    lightChannelHistogram[minimum]++;
                    chromaHistogram[maximum - minimum]++;
                    samplePixels++;
                }
            }
        }

        KeyValuePair<int, int>[] dominant = histogram
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(2)
            .ToArray();
        if (dominant.Length != 2)
            throw new InvalidDataException("The contractually empty atlas cells do not contain a detectable two-color checkerboard.");

        MobyIconAtlasColor[] colors = dominant.Select(pair => MobyIconAtlasColor.FromRgbKey(pair.Key)).ToArray();
        if (colors.Any(color => Math.Min(color.R, Math.Min(color.G, color.B)) < 190))
            throw new InvalidDataException("The detected empty-cell checkerboard colors are not light background colors.");

        int redDelta = colors[0].R - colors[1].R;
        int greenDelta = colors[0].G - colors[1].G;
        int blueDelta = colors[0].B - colors[1].B;
        int distanceSquared = (redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta);
        if (distanceSquared > 80 * 80)
            throw new InvalidDataException("The detected empty-cell colors are too far apart to be one checkerboard background.");

        byte minimumLightChannel = (byte)Math.Max(190, WeightedPercentile(lightChannelHistogram, samplePixels, 0.005) - 2);
        byte maximumChroma = (byte)Math.Min(32, WeightedPercentile(chromaHistogram, samplePixels, 0.995) + 2);
        (int checkerTilesPerAxis, double checkerParityLumaDelta) = DetectCheckerboardPeriod(
            pixels,
            rowBytes,
            size,
            atlas);
        CheckerboardProfile profile = new(
            colors,
            histogram.Keys.ToHashSet(),
            minimumLightChannel,
            maximumChroma,
            checkerTilesPerAxis,
            checkerParityLumaDelta);
        int sampleMatches = histogram.Sum(pair => profile.MatchesEnvelope(pair.Key) ? pair.Value : 0);
        double envelopeCoverage = sampleMatches / (double)Math.Max(1, samplePixels);
        if (envelopeCoverage < 0.98)
        {
            throw new InvalidDataException(
                $"The learned light-neutral checkerboard envelope covers only {envelopeCoverage:P1} of the empty-cell sample.");
        }

        return (profile, samplePixels, sampleMatches);
    }

    private static (int TilesPerAxis, double ParityLumaDelta) DetectCheckerboardPeriod(
        byte[] pixels,
        int rowBytes,
        PixelSize size,
        MobyRasterIconAtlasContract atlas)
    {
        const int minimumTilesPerAxis = 8;
        int maximumTilesPerAxis = Math.Min(128, Math.Min(size.Width, size.Height) / 4);
        int bestTilesPerAxis = 0;
        double bestDelta = 0;

        for (int tilesPerAxis = minimumTilesPerAxis; tilesPerAxis <= maximumTilesPerAxis; tilesPerAxis++)
        {
            long[] lumaSums = new long[2];
            int[] counts = new int[2];
            for (int atlasCell = atlas.UsedCellCount; atlasCell < atlas.CellCount; atlasCell++)
            {
                PixelRect cell = GetCellPixelRect(size, atlas, atlasCell);
                for (int y = cell.Y; y < cell.Bottom; y += 2)
                {
                    for (int x = cell.X; x < cell.Right; x += 2)
                    {
                        int parity =
                            ((int)((long)x * tilesPerAxis / size.Width) +
                            (int)((long)y * tilesPerAxis / size.Height)) & 1;
                        int offset = (y * rowBytes) + (x * 4);
                        lumaSums[parity] += pixels[offset] + pixels[offset + 1] + pixels[offset + 2];
                        counts[parity]++;
                    }
                }
            }

            if (counts[0] == 0 || counts[1] == 0)
                continue;
            double parity0Luma = lumaSums[0] / (counts[0] * 3.0);
            double parity1Luma = lumaSums[1] / (counts[1] * 3.0);
            double delta = Math.Abs(parity0Luma - parity1Luma);
            if (delta > bestDelta)
            {
                bestDelta = delta;
                bestTilesPerAxis = tilesPerAxis;
            }
        }

        return (bestTilesPerAxis, bestDelta);
    }

    private static bool[] FindConnectedBackground(
        byte[] pixels,
        int rowBytes,
        PixelSize size,
        MobyRasterIconAtlasContract atlas,
        CheckerboardProfile checkerboard)
    {
        int pixelCount = checked(size.Width * size.Height);
        bool[] connected = new bool[pixelCount];
        bool[] visited = new bool[pixelCount];
        int[] queue = new int[pixelCount];

        // Preserve the original whole-atlas flood first. This keeps clean sheets
        // byte-for-byte stable and lets a real background component flow across
        // contract boundaries wherever subjects do not interrupt it.
        int outerReadIndex = 0;
        int outerWriteIndex = 0;
        void EnqueueOuterBackground(int x, int y)
        {
            int index = (y * size.Width) + x;
            if (connected[index])
                return;
            int offset = (y * rowBytes) + (x * 4);
            if (!IsConnectedBackgroundLikePixel(pixels, offset, checkerboard))
                return;

            connected[index] = true;
            queue[outerWriteIndex++] = index;
        }

        for (int x = 0; x < size.Width; x++)
        {
            EnqueueOuterBackground(x, 0);
            EnqueueOuterBackground(x, size.Height - 1);
        }
        for (int y = 1; y < size.Height - 1; y++)
        {
            EnqueueOuterBackground(0, y);
            EnqueueOuterBackground(size.Width - 1, y);
        }
        while (outerReadIndex < outerWriteIndex)
        {
            int index = queue[outerReadIndex++];
            int x = index % size.Width;
            int y = index / size.Width;
            if (x > 0)
                EnqueueOuterBackground(x - 1, y);
            if (x + 1 < size.Width)
                EnqueueOuterBackground(x + 1, y);
            if (y > 0)
                EnqueueOuterBackground(x, y - 1);
            if (y + 1 < size.Height)
                EnqueueOuterBackground(x, y + 1);
        }

        // Low-variance sheets that provide only the minimum two empty sample
        // cells remain on the byte-stable legacy outer-flood path. A broader
        // connected envelope is one proof that adaptive cleanup is needed.
        // Independently, three or more contract-owned empty cells plus a strong
        // learned alternating period are enough data to prove a checker sheet
        // even when its colors are low-chroma and require no envelope expansion.
        // If neither proof exists, fail closed and preserve all enclosed pixels.
        bool hasIndependentPeriodicCheckerProof =
            atlas.EmptyCellCount >= MinimumIndependentEmptyCellsForPeriodicCleanup &&
            checkerboard.CheckerTilesPerAxis >= 8 &&
            checkerboard.CheckerParityLumaDelta >= MinimumLearnedCheckerParityDeltaForPeriodicCleanup;
        if (!checkerboard.HasExpandedConnectedEnvelope && !hasIndependentPeriodicCheckerProof)
            return connected;

        // Subjects and generated grid seams can isolate checker regions from the
        // atlas perimeter. Flood each exact integer contract cell independently
        // to remove edge-connected islands as well.
        for (int atlasCell = 0; atlasCell < atlas.CellCount; atlasCell++)
        {
            PixelRect cell = GetCellPixelRect(size, atlas, atlasCell);
            void ClearComponentFromEdge(int seedX, int seedY)
            {
                int seedIndex = (seedY * size.Width) + seedX;
                if (connected[seedIndex] || visited[seedIndex])
                    return;
                int seedOffset = (seedY * rowBytes) + (seedX * 4);
                if (!IsConnectedBackgroundLikePixel(pixels, seedOffset, checkerboard))
                    return;

                int readIndex = 0;
                int writeIndex = 0;

                void EnqueueIfBackground(int x, int y)
                {
                    int index = (y * size.Width) + x;
                    if (connected[index] || visited[index])
                        return;
                    int offset = (y * rowBytes) + (x * 4);
                    if (!IsConnectedBackgroundLikePixel(pixels, offset, checkerboard))
                        return;

                    visited[index] = true;
                    queue[writeIndex++] = index;
                }

                EnqueueIfBackground(seedX, seedY);
                while (readIndex < writeIndex)
                {
                    int index = queue[readIndex++];
                    int x = index % size.Width;
                    int y = index / size.Width;
                    if (x > cell.X)
                        EnqueueIfBackground(x - 1, y);
                    if (x + 1 < cell.Right)
                        EnqueueIfBackground(x + 1, y);
                    if (y > cell.Y)
                        EnqueueIfBackground(x, y - 1);
                    if (y + 1 < cell.Bottom)
                        EnqueueIfBackground(x, y + 1);
                }

                for (int index = 0; index < writeIndex; index++)
                    connected[queue[index]] = true;
            }

            for (int x = cell.X; x < cell.Right; x++)
            {
                ClearComponentFromEdge(x, cell.Y);
                ClearComponentFromEdge(x, cell.Bottom - 1);
            }
            for (int y = cell.Y + 1; y < cell.Bottom - 1; y++)
            {
                ClearComponentFromEdge(cell.X, y);
                ClearComponentFromEdge(cell.Right - 1, y);
            }
        }

        // A subject can completely surround negative space, leaving baked
        // checker tiles unreachable from any edge. Classify only substantial
        // neutral components that reuse at least half of the exact empty-cell
        // palette and reproduce the learned alternating tile luminance. This
        // removes enclosed checker holes while protecting non-periodic armor,
        // petals, horns, wizard beards, and fairy glow.
        Array.Clear(visited);
        double minimumParityDelta = Math.Max(
            MinimumInteriorCheckerParityDelta,
            checkerboard.CheckerParityLumaDelta * LearnedCheckerParityDeltaFraction);
        for (int atlasCell = 0; atlasCell < atlas.UsedCellCount; atlasCell++)
        {
            PixelRect cell = GetCellPixelRect(size, atlas, atlasCell);

            void ClearInteriorCheckerComponent(int seedX, int seedY)
            {
                int seedIndex = (seedY * size.Width) + seedX;
                if (connected[seedIndex] || visited[seedIndex])
                    return;
                int seedOffset = (seedY * rowBytes) + (seedX * 4);
                if (!IsConnectedBackgroundLikePixel(pixels, seedOffset, checkerboard))
                    return;

                int readIndex = 0;
                int writeIndex = 0;
                int exactSampleColorPixels = 0;
                long[] parityLumaSums = new long[2];
                int[] parityPixelCounts = new int[2];

                void EnqueueIfBackground(int x, int y)
                {
                    int index = (y * size.Width) + x;
                    if (connected[index] || visited[index])
                        return;
                    int offset = (y * rowBytes) + (x * 4);
                    if (!IsConnectedBackgroundLikePixel(pixels, offset, checkerboard))
                        return;

                    visited[index] = true;
                    queue[writeIndex++] = index;
                }

                EnqueueIfBackground(seedX, seedY);
                while (readIndex < writeIndex)
                {
                    int index = queue[readIndex++];
                    int x = index % size.Width;
                    int y = index / size.Width;
                    int offset = (y * rowBytes) + (x * 4);
                    int rgbKey =
                        (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
                    if (checkerboard.IsExactSampleColor(rgbKey))
                        exactSampleColorPixels++;
                    int parity = checkerboard.GetCheckerParity(x, y, size);
                    parityLumaSums[parity] +=
                        pixels[offset] + pixels[offset + 1] + pixels[offset + 2];
                    parityPixelCounts[parity]++;

                    if (x > cell.X)
                        EnqueueIfBackground(x - 1, y);
                    if (x + 1 < cell.Right)
                        EnqueueIfBackground(x + 1, y);
                    if (y > cell.Y)
                        EnqueueIfBackground(x, y - 1);
                    if (y + 1 < cell.Bottom)
                        EnqueueIfBackground(x, y + 1);
                }

                if (writeIndex < MinimumInteriorCheckerComponentPixels ||
                    exactSampleColorPixels / (double)writeIndex < MinimumInteriorCheckerSampleColorFraction ||
                    parityPixelCounts[0] < MinimumCheckerParityGroupPixels ||
                    parityPixelCounts[1] < MinimumCheckerParityGroupPixels)
                {
                    return;
                }

                double parity0Luma = parityLumaSums[0] / (parityPixelCounts[0] * 3.0);
                double parity1Luma = parityLumaSums[1] / (parityPixelCounts[1] * 3.0);
                if (Math.Abs(parity0Luma - parity1Luma) < minimumParityDelta)
                    return;

                for (int index = 0; index < writeIndex; index++)
                    connected[queue[index]] = true;
            }

            for (int y = cell.Y; y < cell.Bottom; y++)
            {
                for (int x = cell.X; x < cell.Right; x++)
                    ClearInteriorCheckerComponent(x, y);
            }
        }

        return connected;
    }

    private static IReadOnlyList<MobyIconAtlasCellDiagnostics> BuildCellDiagnostics(
        byte[] pixels,
        int rowBytes,
        PixelSize size,
        MobyRasterIconAtlasContract atlas)
    {
        List<MobyIconAtlasCellDiagnostics> result = new(atlas.CellCount);
        Span<byte> dimensions = stackalloc byte[8];
        for (int atlasCell = 0; atlasCell < atlas.CellCount; atlasCell++)
        {
            PixelRect cell = GetCellPixelRect(size, atlas, atlasCell);
            int transparent = 0;
            int opaque = 0;
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            BinaryPrimitives.WriteInt32LittleEndian(dimensions[..4], cell.Width);
            BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], cell.Height);
            hash.AppendData(dimensions);
            for (int y = cell.Y; y < cell.Bottom; y++)
            {
                int rowOffset = (y * rowBytes) + (cell.X * 4);
                ReadOnlySpan<byte> row = pixels.AsSpan(rowOffset, cell.Width * 4);
                hash.AppendData(row);
                for (int x = 0; x < cell.Width; x++)
                {
                    if (row[(x * 4) + 3] == 0)
                        transparent++;
                    else
                        opaque++;
                }
            }

            result.Add(new MobyIconAtlasCellDiagnostics(
                atlasCell,
                cell,
                opaque,
                transparent,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
        }

        return result;
    }

    private static int CountTransparentPixels(byte[] pixels, int rowBytes, PixelSize size)
    {
        int count = 0;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (pixels[(y * rowBytes) + (x * 4) + 3] == 0)
                    count++;
            }
        }
        return count;
    }

    private static int CountPreservedBackgroundLikePixels(
        byte[] pixels,
        int rowBytes,
        PixelSize size,
        CheckerboardProfile checkerboard)
    {
        int count = 0;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                int offset = (y * rowBytes) + (x * 4);
                if (pixels[offset + 3] != 0 && IsBackgroundLikePixel(pixels, offset, checkerboard))
                    count++;
            }
        }
        return count;
    }

    private static bool IsBackgroundLikePixel(
        byte[] pixels,
        int offset,
        CheckerboardProfile checkerboard)
    {
        if (pixels[offset + 3] == 0)
            return false;
        int rgbKey = (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
        return checkerboard.Matches(rgbKey);
    }

    private static bool IsConnectedBackgroundLikePixel(
        byte[] pixels,
        int offset,
        CheckerboardProfile checkerboard)
    {
        if (pixels[offset + 3] == 0)
            return false;
        int rgbKey = (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
        return checkerboard.MatchesConnectedEnvelope(rgbKey);
    }

    private static int WeightedPercentile(IReadOnlyList<int> histogram, int total, double percentile)
    {
        int target = Math.Max(1, (int)Math.Ceiling(total * percentile));
        int cumulative = 0;
        for (int value = 0; value < histogram.Count; value++)
        {
            cumulative += histogram[value];
            if (cumulative >= target)
                return value;
        }
        return histogram.Count - 1;
    }

    private static MobyIconAtlasLoadResult Failure(
        MobyRasterIconAtlasContract atlas,
        string path,
        string reason) =>
        new(null, new MobyIconAtlasDiagnostics(
            atlas,
            path,
            false,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<MobyIconAtlasColor>(),
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<MobyIconAtlasCellDiagnostics>(),
            reason));

    private readonly record struct MobyIconAtlasCacheKey(
        string FullPath,
        string AtlasKey,
        string AtlasFileName,
        int Columns,
        int Rows,
        int UsedCellCount)
    {
        public bool Equals(MobyIconAtlasCacheKey other) =>
            FullPath.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase) &&
            AtlasKey.Equals(other.AtlasKey, StringComparison.OrdinalIgnoreCase) &&
            AtlasFileName.Equals(other.AtlasFileName, StringComparison.OrdinalIgnoreCase) &&
            Columns == other.Columns &&
            Rows == other.Rows &&
            UsedCellCount == other.UsedCellCount;

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(FullPath, StringComparer.OrdinalIgnoreCase);
            hash.Add(AtlasKey, StringComparer.OrdinalIgnoreCase);
            hash.Add(AtlasFileName, StringComparer.OrdinalIgnoreCase);
            hash.Add(Columns);
            hash.Add(Rows);
            hash.Add(UsedCellCount);
            return hash.ToHashCode();
        }
    }

    private sealed class CheckerboardProfile
    {
        private readonly HashSet<int> _sampleRgbKeys;

        public CheckerboardProfile(
            IReadOnlyList<MobyIconAtlasColor> representativeColors,
            HashSet<int> sampleRgbKeys,
            byte minimumLightChannel,
            byte maximumChroma,
            int checkerTilesPerAxis,
            double checkerParityLumaDelta)
        {
            RepresentativeColors = representativeColors;
            _sampleRgbKeys = sampleRgbKeys;
            MinimumLightChannel = minimumLightChannel;
            MaximumChroma = maximumChroma;

            // More chromatic variation in the contractually empty sample is a
            // reliable signal that the generated checker also drifts darker in
            // occupied cells. Expand only the connectivity envelope; the strict
            // learned profile remains unchanged for diagnostics and enclosed
            // subject-detail accounting. Atlas 1's low-variance checker receives
            // no expansion.
            int variation = Math.Max(0, maximumChroma - 4);
            ConnectedMinimumLightChannel =
                (byte)Math.Max(190, minimumLightChannel - (variation * 2));
            ConnectedMaximumChroma =
                (byte)Math.Min(32, maximumChroma + variation);
            CheckerTilesPerAxis = checkerTilesPerAxis;
            CheckerParityLumaDelta = checkerParityLumaDelta;
        }

        public IReadOnlyList<MobyIconAtlasColor> RepresentativeColors { get; }
        public byte MinimumLightChannel { get; }
        public byte MaximumChroma { get; }
        public byte ConnectedMinimumLightChannel { get; }
        public byte ConnectedMaximumChroma { get; }
        public int CheckerTilesPerAxis { get; }
        public double CheckerParityLumaDelta { get; }
        public bool HasExpandedConnectedEnvelope =>
            ConnectedMinimumLightChannel != MinimumLightChannel ||
            ConnectedMaximumChroma != MaximumChroma;

        public bool Matches(int rgbKey) => _sampleRgbKeys.Contains(rgbKey) || MatchesEnvelope(rgbKey);

        public bool MatchesEnvelope(int rgbKey)
        {
            int red = (rgbKey >> 16) & 0xFF;
            int green = (rgbKey >> 8) & 0xFF;
            int blue = rgbKey & 0xFF;
            int minimum = Math.Min(red, Math.Min(green, blue));
            int maximum = Math.Max(red, Math.Max(green, blue));
            return minimum >= MinimumLightChannel && maximum - minimum <= MaximumChroma;
        }

        public bool MatchesConnectedEnvelope(int rgbKey)
        {
            if (_sampleRgbKeys.Contains(rgbKey))
                return true;

            int red = (rgbKey >> 16) & 0xFF;
            int green = (rgbKey >> 8) & 0xFF;
            int blue = rgbKey & 0xFF;
            int minimum = Math.Min(red, Math.Min(green, blue));
            int maximum = Math.Max(red, Math.Max(green, blue));
            return minimum >= ConnectedMinimumLightChannel &&
                maximum - minimum <= ConnectedMaximumChroma;
        }

        public bool IsExactSampleColor(int rgbKey) => _sampleRgbKeys.Contains(rgbKey);

        public int GetCheckerParity(int x, int y, PixelSize size) =>
            ((int)((long)x * CheckerTilesPerAxis / size.Width) +
            (int)((long)y * CheckerTilesPerAxis / size.Height)) & 1;
    }
}

public sealed class MobyIconAtlasLoadResult
{
    internal MobyIconAtlasLoadResult(Bitmap? bitmap, MobyIconAtlasDiagnostics diagnostics)
    {
        Bitmap = bitmap;
        Diagnostics = diagnostics;
    }

    public Bitmap? Bitmap { get; }
    public MobyIconAtlasDiagnostics Diagnostics { get; }
    public bool Succeeded => Bitmap != null && Diagnostics.Succeeded;
}

public sealed record MobyIconAtlasDiagnostics(
    MobyRasterIconAtlasContract Atlas,
    string SourcePath,
    bool Succeeded,
    int Width,
    int Height,
    int SourceTransparentPixelCount,
    int CheckerboardSamplePixelCount,
    int CheckerboardSampleMatchCount,
    IReadOnlyList<MobyIconAtlasColor> CheckerboardColors,
    byte CheckerboardMinimumLightChannel,
    byte CheckerboardMaximumChroma,
    int ConnectedBackgroundPixelCount,
    int PreservedDisconnectedBackgroundLikePixelCount,
    int TransparentPixelCount,
    int TotalPixelCount,
    IReadOnlyList<MobyIconAtlasCellDiagnostics> Cells,
    string FailureReason)
{
    public double TransparentFraction => TransparentPixelCount / (double)Math.Max(1, TotalPixelCount);
    public double CheckerboardSampleCoverage => CheckerboardSampleMatchCount / (double)Math.Max(1, CheckerboardSamplePixelCount);
}

public readonly record struct MobyIconAtlasColor(byte R, byte G, byte B)
{
    public string Hex => $"#{R:x2}{G:x2}{B:x2}";

    internal static MobyIconAtlasColor FromRgbKey(int key) =>
        new((byte)(key >> 16), (byte)(key >> 8), (byte)key);
}

public sealed record MobyIconAtlasCellDiagnostics(
    int AtlasCell,
    PixelRect Bounds,
    int OpaquePixelCount,
    int TransparentPixelCount,
    string Sha256)
{
    public int TotalPixelCount => OpaquePixelCount + TransparentPixelCount;
    public double TransparentFraction => TransparentPixelCount / (double)Math.Max(1, TotalPixelCount);
}
