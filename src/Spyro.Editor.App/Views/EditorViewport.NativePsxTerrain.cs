using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Rendering;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class EditorViewport
{
    private readonly Dictionary<int, NativeTerrainHqMaterialRecordPayload> _nativeTerrainHqMaterialRecords = new();
    private readonly Dictionary<NativeTerrainBoundedHqDescriptorKey, PsxTerrainTextureDescriptor> _nativeTerrainBoundedHqDescriptors = new();
    private readonly Dictionary<NativeTerrainBoundedLqDescriptorKey, PsxTerrainTextureDescriptor> _nativeTerrainBoundedLqDescriptors = new();
    private WriteableBitmap? _nativeTerrainBoundedBitmap;
    private NativeTerrainBoundedFrameKey? _nativeTerrainBoundedFrameKey;
    private NativeTerrainBoundedCompositorSnapshot _nativeTerrainBoundedSnapshot =
        NativeTerrainBoundedCompositorSnapshot.Fallback("The bounded compositor has not rendered a frame.");
    private string _nativeTerrainHqMaterialFingerprint = "";
    private string _nativeTerrainBoundedCompatibilityBlocker = "Raw native HQ material data is not loaded.";
    private bool _nativeTerrainBoundedCompatibilityEnabled;
    private int _nativeTerrainBoundedMaterialGeneration;
    private int _nativeTerrainBoundedEnvironmentGeneration;

    /// <summary>
    /// Supplies the validated native-load HQ material payload used by the bounded
    /// RGB5 compositor. Callers must disable compatibility for custom imports or
    /// relocations because their final raw descriptor payload is not represented
    /// by the native sidecar.
    /// </summary>
    public void SetNativeTerrainHqMaterialSet(
        NativeTerrainHqMaterialSet? materialSet,
        bool compatible = true,
        string? incompatibilityReason = null)
    {
        _nativeTerrainHqMaterialRecords.Clear();
        _nativeTerrainBoundedHqDescriptors.Clear();
        _nativeTerrainHqMaterialFingerprint = "";
        _nativeTerrainBoundedCompatibilityEnabled = false;
        _nativeTerrainBoundedCompatibilityBlocker = string.IsNullOrWhiteSpace(incompatibilityReason)
            ? "Raw native HQ material data is not loaded."
            : incompatibilityReason.Trim();

        string validationBlocker = "";
        if (materialSet != null && compatible && TryValidateNativeTerrainHqMaterialSet(materialSet, out validationBlocker))
        {
            foreach (NativeTerrainHqMaterialRecordPayload record in materialSet.Textures)
                _nativeTerrainHqMaterialRecords[record.TextureId] = record;
            _nativeTerrainHqMaterialFingerprint = materialSet.ContentSha256;
            _nativeTerrainBoundedCompatibilityEnabled = true;
            _nativeTerrainBoundedCompatibilityBlocker = "";
        }
        else if (materialSet != null && compatible)
        {
            _nativeTerrainBoundedCompatibilityBlocker = validationBlocker;
        }

        _nativeTerrainBoundedMaterialGeneration++;
        InvalidateNativeTerrainBoundedFrame();
        InvalidateVisual();
    }

    internal NativeTerrainBoundedCompositorSnapshot CaptureNativeTerrainBoundedCompositorSnapshotForTesting() =>
        _nativeTerrainBoundedSnapshot;

    internal NativeTerrainBoundedTileSeamProof CaptureNativeTerrainBoundedTileSeamProofForTesting(
        int gridColumns,
        int tileSide)
    {
        if (gridColumns is not (2 or 4) || tileSide is not (16 or 32))
            throw new ArgumentOutOfRangeException(nameof(gridColumns), "The seam fixture supports the native 2x2/4x4 and 16/32-pixel descriptor shapes.");

        int compositeSide = checked(gridColumns * tileSide);
        PsxRgb5 background = new(31, 0, 31);
        PsxRgb5Framebuffer framebuffer = new(compositeSide, compositeSide, background);
        BoundedTerrainVertex[] vertices =
        [
            new(0, compositeSide, 0, compositeSide, 128, 128, 128),
            new(compositeSide, compositeSide, compositeSide, compositeSide, 128, 128, 128),
            new(compositeSide, 0, compositeSide, 0, 128, 128, 128),
            new(0, 0, 0, 0, 128, 128, 128)
        ];
        int[] winding = [3, 2, 1, 0];
        List<PsxTerrainBoundedRenderCommand> commands = [];
        PsxRgb5[] tileColors = new PsxRgb5[gridColumns * gridColumns];
        int sequence = 0;
        for (int descriptorIndex = 0; descriptorIndex < tileColors.Length; descriptorIndex++)
        {
            int tileX = descriptorIndex % gridColumns;
            int tileY = descriptorIndex / gridColumns;
            PsxRgb5 color = new(
                1 + ((descriptorIndex * 7) % 30),
                1 + ((descriptorIndex * 11) % 30),
                1 + ((descriptorIndex * 13) % 30));
            tileColors[descriptorIndex] = color;
            ushort[] rawWords = Enumerable.Repeat(color.PackedWord, tileSide * tileSide).ToArray();
            PsxTerrainTextureDescriptor descriptor = new(
                tileSide,
                tileSide,
                rawWords,
                (PsxSemiTransparencyMode)(descriptorIndex & 3));
            double minimumU = tileX * tileSide;
            double minimumV = tileY * tileSide;
            double maximumU = minimumU + tileSide;
            double maximumV = minimumV + tileSide;
            for (int triangle = 1; triangle < winding.Length - 1; triangle++)
            {
                List<BoundedTerrainVertex> clipped = ClipNativeTerrainBoundedTriangle(
                    vertices[winding[0]],
                    vertices[winding[triangle]],
                    vertices[winding[triangle + 1]],
                    minimumU,
                    minimumV,
                    maximumU,
                    maximumV);
                for (int clippedTriangle = 1; clippedTriangle < clipped.Count - 1; clippedTriangle++)
                {
                    commands.Add(new PsxTerrainBoundedRenderCommand(
                        0,
                        sequence++,
                        CreateNativeTerrainBoundedTexturedTriangle(
                            clipped[0].WithLocalUv(minimumU, minimumV),
                            clipped[clippedTriangle].WithLocalUv(minimumU, minimumV),
                            clipped[clippedTriangle + 1].WithLocalUv(minimumU, minimumV),
                            descriptor,
                            primitiveSemiTransparent: false)));
                }
            }
        }

        PsxTerrainRasterResult raster = PsxTerrainBoundedRasterizer.Paint(framebuffer, commands, capturePainterSequence: false);
        int backgroundPixels = 0;
        int mismatchedPixels = 0;
        for (int y = 0; y < compositeSide; y++)
        {
            for (int x = 0; x < compositeSide; x++)
            {
                PsxRgb5 actual = framebuffer.GetPixel(x, y);
                PsxRgb5 source = tileColors[((y / tileSide) * gridColumns) + (x / tileSide)];
                PsxRgb5 expected = PsxGpuDither.ModulateTextureColor(
                    source,
                    PsxRgb8.NeutralModulation,
                    x,
                    y);
                if (actual == background)
                    backgroundPixels++;
                if (actual != expected)
                    mismatchedPixels++;
            }
        }
        byte[] packed = framebuffer.CopyPackedLittleEndianBytes();
        return new NativeTerrainBoundedTileSeamProof(
            gridColumns,
            tileSide,
            compositeSide,
            tileColors.Length,
            commands.Count,
            raster.Statistics.DegenerateTriangles,
            backgroundPixels,
            mismatchedPixels,
            Convert.ToHexString(SHA256.HashData(packed)));
    }

    private void OnNativeTerrainLqRecordsChanged()
    {
        _nativeTerrainBoundedLqDescriptors.Clear();
        _nativeTerrainBoundedMaterialGeneration++;
        InvalidateNativeTerrainBoundedFrame();
    }

    private void InvalidateNativeTerrainBoundedFrame(bool incrementEnvironmentGeneration = false)
    {
        if (incrementEnvironmentGeneration)
            _nativeTerrainBoundedEnvironmentGeneration++;
        _nativeTerrainBoundedFrameKey = null;
    }

    private bool TryDrawNativeTerrainBoundedFrame(
        DrawingContext context,
        Rect bounds,
        GeometryCandidate geometry,
        IReadOnlyList<ProjectedTerrainFace> terrainFaces,
        IReadOnlyList<ProjectedLowDetailTerrainFace> lowDetailFaces,
        IReadOnlySet<int>? activeRetailSectors,
        int activeRetailGroupIndex)
    {
        string? eligibilityBlocker = NativeTerrainBoundedEligibilityBlocker(bounds, geometry);
        if (eligibilityBlocker != null)
        {
            _nativeTerrainBoundedSnapshot = NativeTerrainBoundedCompositorSnapshot.Fallback(eligibilityBlocker);
            return false;
        }

        int width = _nativeGteProjectionExperimentEnabled
            ? NativeGteActiveWidth
            : checked((int)Math.Ceiling(bounds.Width));
        int height = _nativeGteProjectionExperimentEnabled
            ? NativeGteActiveHeight
            : checked((int)Math.Ceiling(bounds.Height));
        NativeTerrainBoundedFrameKey key = new(
            width,
            height,
            bounds.Width,
            bounds.Height,
            bounds.Left,
            bounds.Top,
            _flyCamera.X,
            _flyCamera.Y,
            _flyCamera.Z,
            _flyCamera.Yaw,
            _flyCamera.Pitch,
            _flipMapY,
            _useNativeTerrainDepthCue,
            _nativeGteProjectionExperimentEnabled,
            activeRetailGroupIndex,
            RuntimeHelpers.GetHashCode(geometry),
            _nativeTerrainBoundedMaterialGeneration,
            _nativeTerrainBoundedEnvironmentGeneration);
        if (_nativeTerrainBoundedFrameKey == key && _nativeTerrainBoundedBitmap != null)
        {
            context.DrawImage(
                _nativeTerrainBoundedBitmap,
                new Rect(0, 0, width, height),
                bounds);
            _nativeTerrainBoundedSnapshot = _nativeTerrainBoundedSnapshot with
            {
                Activated = true,
                Used = true,
                CacheHit = true,
                FallbackReason = ""
            };
            return true;
        }

        try
        {
            IReadOnlyList<ProjectedTerrainFace> commandTerrainFaces = terrainFaces;
            IReadOnlyList<ProjectedLowDetailTerrainFace> commandLowDetailFaces = lowDetailFaces;
            Rect commandProjectionBounds = bounds;
            if (_nativeGteProjectionExperimentEnabled)
            {
                if (!TryBuildNativeGteProjectionExperiment(
                        geometry,
                        out commandTerrainFaces,
                        out commandLowDetailFaces,
                        out string projectionBlocker))
                {
                    _nativeTerrainBoundedSnapshot = NativeTerrainBoundedCompositorSnapshot.Fallback(
                        $"Native GTE projection experiment failed closed: {projectionBlocker}");
                    return false;
                }
                commandProjectionBounds = new Rect(
                    0,
                    NativeGteActiveTop,
                    NativeGteActiveWidth,
                    NativeGteActiveHeight);
            }

            if (!TryBuildNativeTerrainBoundedCommandStream(
                    commandProjectionBounds,
                    geometry,
                    commandTerrainFaces,
                    commandLowDetailFaces,
                    activeRetailSectors,
                    out List<PsxTerrainBoundedRenderCommand> backgroundCommands,
                    out List<PsxTerrainBoundedRenderCommand> foregroundCommands,
                    out List<PsxTerrainBoundedRenderCommand> commands,
                    out NativeTerrainBoundedBuildStatistics build,
                    out string blocker))
            {
                _nativeTerrainBoundedSnapshot = NativeTerrainBoundedCompositorSnapshot.Fallback(blocker);
                return false;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            PsxRgb5Framebuffer framebuffer = CreateNativeTerrainBoundedBackground(width, height);
            PsxTerrainRasterResult backgroundResult = PsxTerrainBoundedRasterizer.Paint(
                framebuffer,
                backgroundCommands,
                capturePainterSequence: false);
            PsxTerrainRasterResult foregroundResult = PsxTerrainBoundedRasterizer.Paint(
                framebuffer,
                foregroundCommands,
                capturePainterSequence: false);
            PsxTerrainRasterStatistics rasterStatistics = MergeNativeTerrainRasterStatistics(
                backgroundResult.Statistics,
                foregroundResult.Statistics);
            byte[] packedRgb5 = framebuffer.CopyPackedLittleEndianBytes();
            byte[] rgba = ExpandNativeTerrainRgb5(framebuffer.AsPackedWords());
            PresentNativeTerrainBoundedBitmap(width, height, rgba);
            stopwatch.Stop();

            context.DrawImage(
                _nativeTerrainBoundedBitmap!,
                new Rect(0, 0, width, height),
                bounds);
            _nativeTerrainBoundedFrameKey = key;
            _nativeTerrainBoundedSnapshot = new NativeTerrainBoundedCompositorSnapshot(
                Activated: true,
                Used: true,
                CacheHit: false,
                FallbackReason: "",
                Contract: PsxTerrainBoundedRasterContract.Name,
                PainterOrderingContract: foregroundCommands.Count > 0
                    ? $"{PsxTerrainBoundedRasterContract.PainterOrdering}; inactive-scene layer then active-retail-group layer"
                    : PsxTerrainBoundedRasterContract.PainterOrdering,
                CoverageContract: PsxTerrainBoundedRasterContract.CoverageRule,
                TextureModulationContract: PsxTerrainBoundedRasterContract.TextureModulation,
                DitheringContract: PsxTerrainBoundedRasterContract.Dithering,
                NativeBaseOrderingContract: NativeTerrainOrderingTableContract.Name,
                NativePassOrderContract: NativeTerrainOrderingTableContract.PassOrder,
                Width: width,
                Height: height,
                LowPolyFaceCount: build.LowPolyFaceCount,
                LowPolyTriangleCount: build.LowPolyTriangleCount,
                TexturedHighPolyFaceCount: build.TexturedHighPolyFaceCount,
                SentinelHighPolyFaceCount: build.SentinelHighPolyFaceCount,
                LowQualityTriangleCount: build.LowQualityTriangleCount,
                MinimumLqPaletteRow: build.MinimumLqPaletteRow,
                MaximumLqPaletteRow: build.MaximumLqPaletteRow,
                LqPaletteRow15FaceCount: build.LqPaletteRow15FaceCount,
                HighQualityFaceCount: build.HighQualityFaceCount,
                HighQualityDescriptorCount: build.HighQualityDescriptorCount,
                HighQualityTriangleCount: build.HighQualityTriangleCount,
                HighQualityNormalFaceCount: build.HighQualityNormalFaceCount,
                HighQualityCloseFaceCount: build.HighQualityCloseFaceCount,
                LowPolyPhaseCommandCount: build.LowPolyPhaseCommandCount,
                HighPolyBasePhaseCommandCount: build.HighPolyBasePhaseCommandCount,
                NormalHighQualityPhaseCommandCount: build.NormalHighQualityPhaseCommandCount,
                CloseHighQualityPhaseCommandCount: build.CloseHighQualityPhaseCommandCount,
                CommandCount: commands.Count,
                MinimumOtBucket: build.MinimumOtBucket,
                MaximumOtBucket: build.MaximumOtBucket,
                TiedOtBucketCount: build.TiedOtBucketCount,
                CommandSha256: NativeTerrainBoundedCommandSha256(commands),
                Rgb5Sha256: Convert.ToHexString(SHA256.HashData(packedRgb5)),
                RgbaSha256: Convert.ToHexString(SHA256.HashData(rgba)),
                RasterMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                MaterialFingerprint: _nativeTerrainHqMaterialFingerprint,
                RasterStatistics: rasterStatistics,
                ScopeNote: _nativeGteProjectionExperimentEnabled
                    ? $"{NativeGteProjectionExperimentContract} supplies certified source coordinates, exact fixed-point editor-camera registers, exact base RTPS SXY/SZ, and a fixed 512x224 active raster. RGB5/STP/ABR/modulation and {PsxTerrainBoundedRasterContract.Dithering} are exact for the supplied command stream. Queue-context culling, HQ generated-vertex projection/tile buckets, clipping, occlusion groups, non-terrain interleaving, and equivalence to a captured gameplay camera are not claimed exact."
                    : $"RGB5/STP/ABR/modulation and {PsxTerrainBoundedRasterContract.Dithering} are exact for the bounded command stream's supplied pixels and shades. {NativeTerrainOrderingTableContract.Name} supplies exact native LP/HP base-bucket equations and coarse FIFO phases. Every queued visible HP face retains its source material; a resolved environment group supplies provenance and coarse painter-order context rather than material suppression. This editor layer is not a retail ordering-table claim. HQ tile-local buckets/subdivision, projection, clipping, GTE interpolation, non-terrain interleaving, and full retail ordering-table traversal are not claimed exact.")
            {
                NativeProjectionContract = _nativeGteProjectionExperimentEnabled
                    ? NativeGteProjectionExperimentContract
                    : "",
                NativeCameraContract = _nativeGteProjectionExperimentEnabled
                    ? SpyroNativeTerrainCamera.Contract
                    : "",
                NativeCameraSource = _nativeGteProjectionExperimentEnabled
                    ? SpyroEditorDerivedTerrainCamera.Contract
                    : ""
            };
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException or NotSupportedException)
        {
            _nativeTerrainBoundedFrameKey = null;
            _nativeTerrainBoundedSnapshot = NativeTerrainBoundedCompositorSnapshot.Fallback(
                $"Bounded native terrain command construction failed closed: {ex.Message}");
            return false;
        }
    }

    private string? NativeTerrainBoundedEligibilityBlocker(Rect bounds, GeometryCandidate geometry)
    {
        if (_nativeTerrainLodPreviewMode != NativeTerrainLodPreviewMode.NativeDistance)
            return "The Fit/editor-overview camera uses the legacy terrain preview.";
        if (!_nativeTerrainBoundedCompatibilityEnabled)
            return string.IsNullOrWhiteSpace(_nativeTerrainBoundedCompatibilityBlocker)
                ? "The raw native material path is not proven compatible."
                : _nativeTerrainBoundedCompatibilityBlocker;
        if (bounds.Width < 1 || bounds.Height < 1 || bounds.Width > 8192 || bounds.Height > 8192)
            return "The viewport dimensions are outside the bounded compositor range.";
        if (!geometry.HasExactHighPolyMaterialState ||
            !string.Equals(geometry.HighPolyMaterialContract, SourceSceneOverlayContract.HpFaceMaterialPayload, StringComparison.Ordinal))
        {
            return "The scene does not carry the exact native HP material-byte contract.";
        }
        if (_nativeGteProjectionExperimentEnabled &&
            (!geometry.HasExactHighPolyCoordinateState ||
             !string.Equals(
                 geometry.HighPolyCoordinateContract,
                 NativeTerrainHighPolyCoordinates.Contract,
                 StringComparison.Ordinal)))
        {
            return "The scene does not carry the certified native HP coordinate contract.";
        }
        if (_nativeGteProjectionExperimentEnabled && !_flipMapY)
            return "The native GTE projection experiment requires the game-view Y orientation.";
        if (!string.Equals(geometry.TerrainLodPreviewContract, SourceSceneOverlayContract.TerrainLodPreview, StringComparison.Ordinal) ||
            geometry.SourceSectors.Count == 0)
        {
            return "The scene does not carry the proven native-distance sector/LOD contract.";
        }
        if (!geometry.HasStaticLowDetailPreview)
            return "The scene's native static-LOD payload contract did not validate.";
        if (geometry.LowDetailPolygons.Any(polygon => !polygon.HasCompleteNativePayload))
            return "At least one LP record lacks its complete four-slot native payload.";
        if (geometry.Polygons.Any(polygon => polygon.IsTerrainEdited))
            return "Terrain edits require the whole-frame legacy preview until their final raw native material payload is rebuilt.";
        if (_nativeTerrainLqTextureRecords.Count == 0)
            return "The validated indexed TexLq material sidecar is not loaded.";
        return null;
    }

    private static PsxTerrainRasterStatistics MergeNativeTerrainRasterStatistics(
        PsxTerrainRasterStatistics background,
        PsxTerrainRasterStatistics foreground) =>
        new(
            background.CommandsSubmitted + foreground.CommandsSubmitted,
            background.CommandsExecuted + foreground.CommandsExecuted,
            background.DegenerateTriangles + foreground.DegenerateTriangles,
            background.CoveredSamples + foreground.CoveredSamples,
            background.FramebufferWrites + foreground.FramebufferWrites,
            background.TransparentTexelSkips + foreground.TransparentTexelSkips,
            background.OpaqueWrites + foreground.OpaqueWrites,
            background.SemiTransparentWrites + foreground.SemiTransparentWrites,
            background.Abr0Writes + foreground.Abr0Writes,
            background.Abr1Writes + foreground.Abr1Writes,
            background.Abr2Writes + foreground.Abr2Writes,
            background.Abr3Writes + foreground.Abr3Writes);

    private bool TryBuildNativeTerrainBoundedCommandStream(
        Rect bounds,
        GeometryCandidate geometry,
        IReadOnlyList<ProjectedTerrainFace> terrainFaces,
        IReadOnlyList<ProjectedLowDetailTerrainFace> lowDetailFaces,
        IReadOnlySet<int>? activeRetailSectors,
        out List<PsxTerrainBoundedRenderCommand> backgroundCommands,
        out List<PsxTerrainBoundedRenderCommand> foregroundCommands,
        out List<PsxTerrainBoundedRenderCommand> commands,
        out NativeTerrainBoundedBuildStatistics statistics,
        out string blocker)
    {
        backgroundCommands = [];
        foregroundCommands = [];
        commands = [];
        blocker = "";
        NativeTerrainPhasedCommandBuilder backgroundPhasedCommands = new();
        NativeTerrainPhasedCommandBuilder foregroundPhasedCommands = new();
        int lowPolyFaceCount = 0;
        int lowPolyTriangleCount = 0;
        int texturedHighPolyFaceCount = 0;
        int sentinelHighPolyFaceCount = 0;
        int sentinelHighPolyTriangleCount = 0;
        int lowQualityTriangleCount = 0;
        int minimumLqPaletteRow = int.MaxValue;
        int maximumLqPaletteRow = int.MinValue;
        int lqPaletteRow15FaceCount = 0;
        int highQualityFaceCount = 0;
        int highQualityDescriptorCount = 0;
        int highQualityTriangleCount = 0;
        int highQualityNormalFaceCount = 0;
        int highQualityCloseFaceCount = 0;
        int highQualityNormalTriangleCount = 0;
        int highQualityCloseTriangleCount = 0;

        foreach (ProjectedLowDetailTerrainFace face in lowDetailFaces
            .OrderBy(face => face.Polygon.SectorIndex)
            .ThenBy(face => face.Polygon.FaceIndex))
        {
            LowDetailTerrainPolygon polygon = face.Polygon;
            NativeTerrainPhasedCommandBuilder phasedCommands =
                activeRetailSectors?.Contains(polygon.SectorIndex) == true
                    ? foregroundPhasedCommands
                    : backgroundPhasedCommands;
            if (!polygon.HasCompleteNativePayload)
            {
                statistics = default;
                blocker = $"LP face {polygon.RuntimeKey} does not carry all four native slots.";
                return false;
            }

            PsxLowPolyRasterVertex[] vertices = new PsxLowPolyRasterVertex[4];
            for (int slot = 0; slot < 4; slot++)
            {
                Spyro.Editor.Core.Primitives.ColorRgba color = PreviewEnvironmentColor(polygon.CornerColors[slot]);
                vertices[slot] = new PsxLowPolyRasterVertex(
                    NativeTerrainBoundedScreenPoint(face.RawPoints[slot], bounds),
                    NativeTerrainBoundedRgb8(color));
            }

            if (!NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
                    face.RawDepths,
                    polygon.RawWord1,
                    out int bucket,
                    out _))
            {
                statistics = default;
                blocker = $"LP face {polygon.RuntimeKey} produced a depth or native OT bucket outside the proven 0..0x{NativeTerrainOrderingTableContract.MaximumBucket:X} range.";
                return false;
            }
            foreach ((int a, int b, int c) in NativeLowDetailTriangleSlots)
            {
                if (face.RawPoints[a] == face.RawPoints[b] ||
                    face.RawPoints[b] == face.RawPoints[c] ||
                    face.RawPoints[c] == face.RawPoints[a])
                {
                    continue;
                }

                phasedCommands.Add(
                    NativeTerrainStaticPassPhase.LowPoly,
                    bucket,
                    new PsxLowPolyRasterTriangle(
                        vertices[a],
                        vertices[b],
                        vertices[c],
                        polygon.SemiTransparent,
                        (PsxSemiTransparencyMode)polygon.BlendMode));
                lowPolyTriangleCount++;
            }
            lowPolyFaceCount++;
        }

        bool hasLowPolyReplacement = geometry.LowDetailPolygons.Count > 0;
        foreach (ProjectedTerrainFace face in terrainFaces
            .Where(face => !face.IsAddCopySourcePreview)
            .OrderBy(face => face.Index))
        {
            TerrainPolygon polygon = face.Polygon;
            NativeTerrainPhasedCommandBuilder phasedCommands =
                activeRetailSectors?.Contains(polygon.SectorIndex) == true
                    ? foregroundPhasedCommands
                    : backgroundPhasedCommands;
            if (!polygon.HasNativeHighPolyMaterialPayload || !polygon.HasNativeCornerPayload || face.CameraDepths == null ||
                !TryBuildRawFaceSlotPoints(polygon, face.Points, out Point[] rawPoints) ||
                !TryBuildRawFaceSlotDepths(polygon, face.CameraDepths, out double[] rawDepths))
            {
                statistics = default;
                blocker = $"HP face {polygon.RuntimeKey} does not carry the exact material/corner projection payload.";
                return false;
            }

            NativeTerrainLqPreviewSelection selection = NativeTerrainFarLod.SelectHighDetailTexturePath(
                rawDepths,
                polygon.LqFadeBypass,
                polygon.HqOverlayBypass);
            if (!NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
                    rawDepths,
                    polygon.NativeFaceWord3,
                    out int bucket,
                    out int rawGteDepthSum) ||
                rawGteDepthSum != selection.RawHpDepthSum)
            {
                statistics = default;
                blocker = $"HP face {polygon.RuntimeKey} produced an inconsistent depth or native OT bucket outside the proven 0..0x{NativeTerrainOrderingTableContract.MaximumBucket:X} range.";
                return false;
            }
            if (!selection.HighPolyVisible && hasLowPolyReplacement)
                continue;

            int uniqueVertexCount = polygon.VertexIndexes.Take(4).Distinct().Count();
            int[] winding = polygon.FaceFlip
                ? uniqueVertexCount == 3 ? [1, 2, 3] : [0, 1, 2, 3]
                : uniqueVertexCount == 3 ? [3, 2, 1] : [3, 2, 1, 0];
            if (winding.Select(slot => rawPoints[slot]).Distinct().Count() < 3)
                continue;

            PsxRgb8[] shades = Enumerable.Range(0, 4)
                .Select(slot => NativeTerrainBoundedCornerShade(polygon, slot, rawDepths[slot]))
                .ToArray();
            if (polygon.IsNativeUntexturedSentinel)
            {
                for (int triangle = 1; triangle < winding.Length - 1; triangle++)
                {
                    int a = winding[0];
                    int b = winding[triangle];
                    int c = winding[triangle + 1];
                    phasedCommands.Add(
                        NativeTerrainStaticPassPhase.HighPolyBase,
                        bucket,
                        new PsxLowPolyRasterTriangle(
                            new PsxLowPolyRasterVertex(NativeTerrainBoundedScreenPoint(rawPoints[a], bounds), shades[a]),
                            new PsxLowPolyRasterVertex(NativeTerrainBoundedScreenPoint(rawPoints[b], bounds), shades[b]),
                            new PsxLowPolyRasterVertex(NativeTerrainBoundedScreenPoint(rawPoints[c], bounds), shades[c]),
                            semiTransparent: false,
                            PsxSemiTransparencyMode.HalfBackgroundPlusHalfForeground));
                }
                sentinelHighPolyFaceCount++;
                sentinelHighPolyTriangleCount += winding.Length - 2;
                continue;
            }

            int textureId = polygon.NativeTextureId;
            if (!_nativeTerrainLqTextureRecords.TryGetValue(textureId, out NativeTerrainLqTextureRecordPayload? lqRecord) ||
                !_nativeTerrainHqMaterialRecords.TryGetValue(textureId, out NativeTerrainHqMaterialRecordPayload? hqRecord))
            {
                statistics = default;
                blocker = $"Texture {textureId} used by {polygon.RuntimeKey} is missing its validated raw LQ or HQ record.";
                return false;
            }

            PsxTerrainTextureDescriptor lqDescriptor = GetNativeTerrainBoundedLqDescriptor(
                lqRecord,
                selection.DescriptorIndex,
                selection.PaletteRow);
            minimumLqPaletteRow = Math.Min(minimumLqPaletteRow, selection.PaletteRow);
            maximumLqPaletteRow = Math.Max(maximumLqPaletteRow, selection.PaletteRow);
            if (selection.PaletteRow == NativeTerrainLqTextureCacheCodec.PaletteRowCount - 1)
                lqPaletteRow15FaceCount++;
            BoundedTerrainVertex[] baseVertices = BuildNativeTerrainBoundedFaceVertices(
                rawPoints,
                shades,
                bounds,
                NativeTerrainLqTextureCacheCodec.TextureSide);
            for (int triangle = 1; triangle < winding.Length - 1; triangle++)
            {
                phasedCommands.Add(
                    NativeTerrainStaticPassPhase.HighPolyBase,
                    bucket,
                    CreateNativeTerrainBoundedTexturedTriangle(
                        baseVertices[winding[0]],
                        baseVertices[winding[triangle]],
                        baseVertices[winding[triangle + 1]],
                        lqDescriptor,
                        polygon.NativePrimitiveSemiTransparent));
                lowQualityTriangleCount++;
            }
            texturedHighPolyFaceCount++;

            if (!selection.HqOverlayEligible)
                continue;

            NativeTerrainHqMaterialTier tier = NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(rawDepths.Min()) ==
                NativeTerrainTexturePreviewTier.Close
                    ? NativeTerrainHqMaterialTier.Close
                    : NativeTerrainHqMaterialTier.Normal;
            NativeTerrainHqMaterialTierPayload tierPayload = hqRecord.GetTier(tier);
            BoundedTerrainVertex[] hqVertices = BuildNativeTerrainBoundedFaceVertices(
                rawPoints,
                shades,
                bounds,
                tierPayload.CompositeSide);
            int faceHqTriangleCount = 0;
            int faceDescriptorCount = 0;
            NativeTerrainStaticPassPhase hqPhase = tier == NativeTerrainHqMaterialTier.Close
                ? NativeTerrainStaticPassPhase.CloseHighQuality
                : NativeTerrainStaticPassPhase.NormalHighQuality;
            foreach (NativeTerrainHqMaterialDescriptorPayload descriptorPayload in tierPayload.Descriptors
                .OrderBy(descriptor => descriptor.DescriptorIndex))
            {
                int tileX = descriptorPayload.DescriptorIndex % tierPayload.GridColumns;
                int tileY = descriptorPayload.DescriptorIndex / tierPayload.GridColumns;
                double minimumU = tileX * tierPayload.TileSide;
                double minimumV = tileY * tierPayload.TileSide;
                double maximumU = minimumU + tierPayload.TileSide;
                double maximumV = minimumV + tierPayload.TileSide;
                int descriptorTriangles = 0;
                PsxTerrainTextureDescriptor descriptor = GetNativeTerrainBoundedHqDescriptor(textureId, tier, descriptorPayload);
                for (int triangle = 1; triangle < winding.Length - 1; triangle++)
                {
                    List<BoundedTerrainVertex> clipped = ClipNativeTerrainBoundedTriangle(
                        hqVertices[winding[0]],
                        hqVertices[winding[triangle]],
                        hqVertices[winding[triangle + 1]],
                        minimumU,
                        minimumV,
                        maximumU,
                        maximumV);
                    for (int clippedTriangle = 1; clippedTriangle < clipped.Count - 1; clippedTriangle++)
                    {
                        BoundedTerrainVertex a = clipped[0].WithLocalUv(minimumU, minimumV);
                        BoundedTerrainVertex b = clipped[clippedTriangle].WithLocalUv(minimumU, minimumV);
                        BoundedTerrainVertex c = clipped[clippedTriangle + 1].WithLocalUv(minimumU, minimumV);
                        // This slice proves the native HP base bucket and the
                        // coarse delayed-HQ phase. Descriptor-local native HQ
                        // subdivision/depth remains explicitly bounded, so the
                        // overlay temporarily inherits its parent face bucket.
                        phasedCommands.Add(
                            hqPhase,
                            bucket,
                            CreateNativeTerrainBoundedTexturedTriangle(
                                a,
                                b,
                                c,
                                descriptor,
                                polygon.NativePrimitiveSemiTransparent));
                        descriptorTriangles++;
                    }
                }
                if (descriptorTriangles > 0)
                {
                    faceDescriptorCount++;
                    faceHqTriangleCount += descriptorTriangles;
                }
            }
            if (faceHqTriangleCount > 0)
            {
                highQualityFaceCount++;
                highQualityDescriptorCount += faceDescriptorCount;
                highQualityTriangleCount += faceHqTriangleCount;
                if (tier == NativeTerrainHqMaterialTier.Close)
                {
                    highQualityCloseFaceCount++;
                    highQualityCloseTriangleCount += faceHqTriangleCount;
                }
                else
                {
                    highQualityNormalFaceCount++;
                    highQualityNormalTriangleCount += faceHqTriangleCount;
                }
            }
        }

        backgroundCommands = backgroundPhasedCommands.Build();
        foregroundCommands = foregroundPhasedCommands.Build();
        int diagnosticSequence = 0;
        commands = backgroundCommands
            .Concat(foregroundCommands)
            .Select(command => new PsxTerrainBoundedRenderCommand(
                command.OtBucket,
                diagnosticSequence++,
                command.Primitive))
            .ToList();
        int highPolyBasePhaseCommandCount = checked(lowQualityTriangleCount + sentinelHighPolyTriangleCount);
        int phasedCommandCount = checked(
            lowPolyTriangleCount +
            highPolyBasePhaseCommandCount +
            highQualityNormalTriangleCount +
            highQualityCloseTriangleCount);
        if (phasedCommandCount != commands.Count)
        {
            statistics = default;
            blocker = $"Native terrain coarse-phase accounting produced {phasedCommandCount} commands for a {commands.Count}-command stream.";
            return false;
        }
        int[] buckets = commands.Select(command => command.OtBucket).ToArray();
        statistics = new NativeTerrainBoundedBuildStatistics(
            lowPolyFaceCount,
            lowPolyTriangleCount,
            texturedHighPolyFaceCount,
            sentinelHighPolyFaceCount,
            lowQualityTriangleCount,
            minimumLqPaletteRow == int.MaxValue ? -1 : minimumLqPaletteRow,
            maximumLqPaletteRow == int.MinValue ? -1 : maximumLqPaletteRow,
            lqPaletteRow15FaceCount,
            highQualityFaceCount,
            highQualityDescriptorCount,
            highQualityTriangleCount,
            highQualityNormalFaceCount,
            highQualityCloseFaceCount,
            lowPolyTriangleCount,
            highPolyBasePhaseCommandCount,
            highQualityNormalTriangleCount,
            highQualityCloseTriangleCount,
            buckets.Length == 0 ? 0 : buckets.Min(),
            buckets.Length == 0 ? 0 : buckets.Max(),
            buckets.GroupBy(bucket => bucket).Count(group => group.Count() > 1));
        return true;
    }

    private static bool TryValidateNativeTerrainHqMaterialSet(NativeTerrainHqMaterialSet set, out string blocker)
    {
        blocker = "";
        if (set.Textures.Count == 0 || string.IsNullOrWhiteSpace(set.ContentSha256) || set.ContentSha256.Length != 64)
        {
            blocker = "The raw HQ material set is empty or lacks its validated content fingerprint.";
            return false;
        }
        if (set.Textures.Select(texture => texture.TextureId).Distinct().Count() != set.Textures.Count)
        {
            blocker = "The raw HQ material set contains duplicate texture ids.";
            return false;
        }
        foreach (NativeTerrainHqMaterialRecordPayload record in set.Textures)
        {
            if (record.TextureId < 0 || record.TextureId >= set.NativeTextureCount ||
                !TryValidateNativeTerrainHqTier(record.Normal, NativeTerrainHqMaterialCacheCodec.NormalDescriptorCount) ||
                !TryValidateNativeTerrainHqTier(record.Close, NativeTerrainHqMaterialCacheCodec.CloseDescriptorCount))
            {
                blocker = $"Texture {record.TextureId} has an incomplete raw HQ descriptor grid.";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateNativeTerrainHqTier(NativeTerrainHqMaterialTierPayload tier, int expectedDescriptors) =>
        tier.DescriptorCount == expectedDescriptors &&
        tier.GridColumns > 0 &&
        tier.TileSide > 0 &&
        tier.CompositeSide == checked(tier.GridColumns * tier.TileSide) &&
        tier.Descriptors.Select(descriptor => descriptor.DescriptorIndex).Order().SequenceEqual(Enumerable.Range(0, expectedDescriptors)) &&
        tier.Descriptors.All(descriptor => descriptor.Side == tier.TileSide && descriptor.WordCount == checked(tier.TileSide * tier.TileSide) && descriptor.Abr is >= 0 and <= 3);

    private PsxTerrainTextureDescriptor GetNativeTerrainBoundedLqDescriptor(
        NativeTerrainLqTextureRecordPayload record,
        int descriptorIndex,
        int paletteRow)
    {
        NativeTerrainBoundedLqDescriptorKey key = new(record.TextureId, descriptorIndex, paletteRow);
        if (_nativeTerrainBoundedLqDescriptors.TryGetValue(key, out PsxTerrainTextureDescriptor? cached))
            return cached;

        NativeTerrainLqTextureDescriptorPayload payload = record.GetDescriptor(descriptorIndex);
        ushort[] words = new ushort[NativeTerrainLqTextureCacheCodec.TextureSide * NativeTerrainLqTextureCacheCodec.TextureSide];
        for (int y = 0; y < NativeTerrainLqTextureCacheCodec.TextureSide; y++)
        {
            for (int x = 0; x < NativeTerrainLqTextureCacheCodec.TextureSide; x++)
            {
                words[(y * NativeTerrainLqTextureCacheCodec.TextureSide) + x] =
                    payload.ReadPaletteWord(paletteRow, payload.ReadPaletteIndex(x, y));
            }
        }
        PsxTerrainTextureDescriptor descriptor = new(
            NativeTerrainLqTextureCacheCodec.TextureSide,
            NativeTerrainLqTextureCacheCodec.TextureSide,
            words,
            (PsxSemiTransparencyMode)payload.Abr);
        _nativeTerrainBoundedLqDescriptors[key] = descriptor;
        return descriptor;
    }

    private PsxTerrainTextureDescriptor GetNativeTerrainBoundedHqDescriptor(
        int textureId,
        NativeTerrainHqMaterialTier tier,
        NativeTerrainHqMaterialDescriptorPayload payload)
    {
        NativeTerrainBoundedHqDescriptorKey key = new(textureId, tier, payload.DescriptorIndex);
        if (_nativeTerrainBoundedHqDescriptors.TryGetValue(key, out PsxTerrainTextureDescriptor? cached))
            return cached;
        PsxTerrainTextureDescriptor descriptor = new(
            payload.Side,
            payload.Side,
            payload.MaterializeRawWords(),
            (PsxSemiTransparencyMode)payload.Abr);
        _nativeTerrainBoundedHqDescriptors[key] = descriptor;
        return descriptor;
    }

    private static BoundedTerrainVertex[] BuildNativeTerrainBoundedFaceVertices(
        IReadOnlyList<Point> rawPoints,
        IReadOnlyList<PsxRgb8> shades,
        Rect bounds,
        int textureSide) =>
    [
        new(rawPoints[0].X - bounds.Left, rawPoints[0].Y - bounds.Top, 0, textureSide, shades[0].Red, shades[0].Green, shades[0].Blue),
        new(rawPoints[1].X - bounds.Left, rawPoints[1].Y - bounds.Top, textureSide, textureSide, shades[1].Red, shades[1].Green, shades[1].Blue),
        new(rawPoints[2].X - bounds.Left, rawPoints[2].Y - bounds.Top, textureSide, 0, shades[2].Red, shades[2].Green, shades[2].Blue),
        new(rawPoints[3].X - bounds.Left, rawPoints[3].Y - bounds.Top, 0, 0, shades[3].Red, shades[3].Green, shades[3].Blue)
    ];

    private static PsxTexturedRasterTriangle CreateNativeTerrainBoundedTexturedTriangle(
        BoundedTerrainVertex a,
        BoundedTerrainVertex b,
        BoundedTerrainVertex c,
        PsxTerrainTextureDescriptor descriptor,
        bool primitiveSemiTransparent) =>
        new(
            a.ToTexturedRasterVertex(),
            b.ToTexturedRasterVertex(),
            c.ToTexturedRasterVertex(),
            descriptor,
            primitiveSemiTransparent);

    private static List<BoundedTerrainVertex> ClipNativeTerrainBoundedTriangle(
        BoundedTerrainVertex a,
        BoundedTerrainVertex b,
        BoundedTerrainVertex c,
        double minimumU,
        double minimumV,
        double maximumU,
        double maximumV)
    {
        List<BoundedTerrainVertex> polygon = [a, b, c];
        polygon = ClipNativeTerrainBoundedPolygon(polygon, vertex => vertex.U >= minimumU, (from, to) => IntersectNativeTerrainBoundedU(from, to, minimumU));
        polygon = ClipNativeTerrainBoundedPolygon(polygon, vertex => vertex.U <= maximumU, (from, to) => IntersectNativeTerrainBoundedU(from, to, maximumU));
        polygon = ClipNativeTerrainBoundedPolygon(polygon, vertex => vertex.V >= minimumV, (from, to) => IntersectNativeTerrainBoundedV(from, to, minimumV));
        polygon = ClipNativeTerrainBoundedPolygon(polygon, vertex => vertex.V <= maximumV, (from, to) => IntersectNativeTerrainBoundedV(from, to, maximumV));
        return polygon;
    }

    private static List<BoundedTerrainVertex> ClipNativeTerrainBoundedPolygon(
        IReadOnlyList<BoundedTerrainVertex> input,
        Func<BoundedTerrainVertex, bool> inside,
        Func<BoundedTerrainVertex, BoundedTerrainVertex, BoundedTerrainVertex> intersection)
    {
        List<BoundedTerrainVertex> output = [];
        if (input.Count == 0)
            return output;
        BoundedTerrainVertex previous = input[^1];
        bool previousInside = inside(previous);
        foreach (BoundedTerrainVertex current in input)
        {
            bool currentInside = inside(current);
            if (currentInside != previousInside)
                output.Add(intersection(previous, current));
            if (currentInside)
                output.Add(current);
            previous = current;
            previousInside = currentInside;
        }
        return output;
    }

    private static BoundedTerrainVertex IntersectNativeTerrainBoundedU(BoundedTerrainVertex from, BoundedTerrainVertex to, double u)
    {
        double denominator = to.U - from.U;
        double amount = Math.Abs(denominator) <= 1e-12 ? 0 : (u - from.U) / denominator;
        return BoundedTerrainVertex.Lerp(from, to, amount) with { U = u };
    }

    private static BoundedTerrainVertex IntersectNativeTerrainBoundedV(BoundedTerrainVertex from, BoundedTerrainVertex to, double v)
    {
        double denominator = to.V - from.V;
        double amount = Math.Abs(denominator) <= 1e-12 ? 0 : (v - from.V) / denominator;
        return BoundedTerrainVertex.Lerp(from, to, amount) with { V = v };
    }

    private PsxRgb8 NativeTerrainBoundedCornerShade(TerrainPolygon polygon, int slot, double cameraDepth)
    {
        Spyro.Editor.Core.Primitives.ColorRgba legacyNear = polygon.TextureVisualEdit?.Corners[slot].NearColor ?? polygon.NearColors[slot];
        Spyro.Editor.Core.Primitives.ColorRgba legacyFar = polygon.TextureVisualEdit?.Corners[slot].FarColor ?? polygon.FarColors[slot];
        Spyro.Editor.Core.Primitives.ColorRgba gradedNear = PreviewEnvironmentColor(legacyNear);
        Spyro.Editor.Core.Primitives.ColorRgba gradedFar = PreviewEnvironmentColor(legacyFar);
        Spyro.Editor.Core.Primitives.ColorRgba graded = _useNativeTerrainDepthCue
            ? NativeTerrainDepthCueColor(gradedNear, gradedFar, cameraDepth)
            : gradedNear;
        return new PsxRgb8(graded.R, graded.G, graded.B);
    }

    private static PsxRgb5Framebuffer CreateNativeTerrainBoundedBackground(int width, int height)
    {
        const int bandCount = 18;
        Color top = Color.FromRgb(39, 58, 82);
        Color horizon = Color.FromRgb(48, 61, 68);
        Color bottom = Color.FromRgb(34, 45, 50);
        PsxRgb5Framebuffer framebuffer = new(width, height, NativeTerrainBoundedRgb5(top));
        for (int band = 0; band < bandCount; band++)
        {
            double t0 = band / (double)bandCount;
            double t1 = (band + 1) / (double)bandCount;
            double t = (t0 + t1) * 0.5;
            Color color = t < 0.52
                ? BlendColor(top, horizon, t / 0.52)
                : BlendColor(horizon, bottom, (t - 0.52) / 0.48);
            int firstY = (int)Math.Floor(height * t0);
            int lastYExclusive = band == bandCount - 1 ? height : (int)Math.Floor(height * t1);
            framebuffer.FillRows(firstY, Math.Max(0, lastYExclusive - firstY), NativeTerrainBoundedRgb5(color));
        }
        return framebuffer;
    }

    private void PresentNativeTerrainBoundedBitmap(int width, int height, byte[] rgba)
    {
        if (_nativeTerrainBoundedBitmap == null ||
            _nativeTerrainBoundedBitmap.PixelSize.Width != width ||
            _nativeTerrainBoundedBitmap.PixelSize.Height != height)
        {
            _nativeTerrainBoundedBitmap?.Dispose();
            _nativeTerrainBoundedBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul);
        }
        using ILockedFramebuffer locked = _nativeTerrainBoundedBitmap.Lock();
        int tightRowBytes = checked(width * 4);
        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(
                rgba,
                y * tightRowBytes,
                IntPtr.Add(locked.Address, y * locked.RowBytes),
                tightRowBytes);
        }
    }

    private static byte[] ExpandNativeTerrainRgb5(ReadOnlySpan<ushort> words)
    {
        byte[] rgba = new byte[checked(words.Length * 4)];
        for (int index = 0; index < words.Length; index++)
        {
            ushort word = words[index];
            int offset = index * 4;
            rgba[offset] = ExpandNativeTerrainRgb5Channel(word & 0x1F);
            rgba[offset + 1] = ExpandNativeTerrainRgb5Channel((word >> 5) & 0x1F);
            rgba[offset + 2] = ExpandNativeTerrainRgb5Channel((word >> 10) & 0x1F);
            rgba[offset + 3] = byte.MaxValue;
        }
        return rgba;
    }

    private static byte ExpandNativeTerrainRgb5Channel(int value) => (byte)((value << 3) | (value >> 2));

    private static PsxScreenPoint NativeTerrainBoundedScreenPoint(Point point, Rect bounds) =>
        new(NativeTerrainBoundedRound(point.X - bounds.Left), NativeTerrainBoundedRound(point.Y - bounds.Top));

    private static int NativeTerrainBoundedRound(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private static PsxRgb5 NativeTerrainBoundedRgb5(Spyro.Editor.Core.Primitives.ColorRgba color) =>
        new(color.R >> 3, color.G >> 3, color.B >> 3);

    private static PsxRgb5 NativeTerrainBoundedRgb5(Color color) =>
        new(color.R >> 3, color.G >> 3, color.B >> 3);

    private static PsxRgb5 NativeTerrainBoundedRgb5(PsxRgb8 color) =>
        new(color.Red >> 3, color.Green >> 3, color.Blue >> 3);

    private static PsxRgb8 NativeTerrainBoundedRgb8(Spyro.Editor.Core.Primitives.ColorRgba color) =>
        new(color.R, color.G, color.B);

    private static string NativeTerrainBoundedCommandSha256(IReadOnlyList<PsxTerrainBoundedRenderCommand> commands)
    {
        StringBuilder text = new();
        foreach (PsxTerrainBoundedRenderCommand command in commands)
        {
            text.Append(command.OtBucket).Append('|').Append(command.InsertSequence).Append('|');
            switch (command.Primitive)
            {
                case PsxLowPolyRasterTriangle low:
                    AppendLow(low.Vertex0);
                    AppendLow(low.Vertex1);
                    AppendLow(low.Vertex2);
                    text.Append(low.SemiTransparent ? 1 : 0).Append('|').Append((int)low.Abr);
                    break;
                case PsxTexturedRasterTriangle textured:
                    AppendTextured(textured.Vertex0);
                    AppendTextured(textured.Vertex1);
                    AppendTextured(textured.Vertex2);
                    text.Append(textured.PrimitiveSemiTransparent ? 1 : 0).Append('|')
                        .Append((int)textured.Descriptor.Abr).Append('|')
                        .Append(textured.Descriptor.Width).Append('x').Append(textured.Descriptor.Height).Append('|')
                        .Append(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(textured.Descriptor.AsRawWords()))));
                    break;
            }
            text.AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));

        void AppendLow(PsxLowPolyRasterVertex vertex) => text
            .Append(vertex.Position.X).Append(',').Append(vertex.Position.Y).Append(',')
            .Append(vertex.Color.Red).Append(',').Append(vertex.Color.Green).Append(',').Append(vertex.Color.Blue).Append('|');
        void AppendTextured(PsxTexturedRasterVertex vertex) => text
            .Append(vertex.Position.X).Append(',').Append(vertex.Position.Y).Append(',')
            .Append(vertex.U).Append(',').Append(vertex.V).Append(',')
            .Append(vertex.Modulation.Red).Append(',').Append(vertex.Modulation.Green).Append(',').Append(vertex.Modulation.Blue).Append('|');
    }

    private readonly record struct NativeTerrainBoundedFrameKey(
        int Width,
        int Height,
        double ViewportWidth,
        double ViewportHeight,
        double Left,
        double Top,
        double CameraX,
        double CameraY,
        double CameraZ,
        double CameraYaw,
        double CameraPitch,
        bool FlipMapY,
        bool DepthCue,
        bool NativeGteProjectionExperiment,
        int ActiveRetailGroupIndex,
        int GeometryIdentity,
        int MaterialGeneration,
        int EnvironmentGeneration);

    private readonly record struct NativeTerrainBoundedHqDescriptorKey(
        int TextureId,
        NativeTerrainHqMaterialTier Tier,
        int DescriptorIndex);

    private readonly record struct NativeTerrainBoundedLqDescriptorKey(
        int TextureId,
        int DescriptorIndex,
        int PaletteRow);

    private readonly record struct BoundedTerrainVertex(
        double X,
        double Y,
        double U,
        double V,
        double Red,
        double Green,
        double Blue)
    {
        public static BoundedTerrainVertex Lerp(BoundedTerrainVertex from, BoundedTerrainVertex to, double amount) =>
            new(
                from.X + ((to.X - from.X) * amount),
                from.Y + ((to.Y - from.Y) * amount),
                from.U + ((to.U - from.U) * amount),
                from.V + ((to.V - from.V) * amount),
                from.Red + ((to.Red - from.Red) * amount),
                from.Green + ((to.Green - from.Green) * amount),
                from.Blue + ((to.Blue - from.Blue) * amount));

        public BoundedTerrainVertex WithLocalUv(double minimumU, double minimumV) =>
            this with { U = U - minimumU, V = V - minimumV };

        public PsxTexturedRasterVertex ToTexturedRasterVertex() =>
            new(
                new PsxScreenPoint(NativeTerrainBoundedRound(X), NativeTerrainBoundedRound(Y)),
                NativeTerrainBoundedRound(U),
                NativeTerrainBoundedRound(V),
                new PsxRgb8(
                    Math.Clamp(NativeTerrainBoundedRound(Red), 0, 255),
                    Math.Clamp(NativeTerrainBoundedRound(Green), 0, 255),
                    Math.Clamp(NativeTerrainBoundedRound(Blue), 0, 255)));
    }

    private readonly record struct NativeTerrainBoundedBuildStatistics(
        int LowPolyFaceCount,
        int LowPolyTriangleCount,
        int TexturedHighPolyFaceCount,
        int SentinelHighPolyFaceCount,
        int LowQualityTriangleCount,
        int MinimumLqPaletteRow,
        int MaximumLqPaletteRow,
        int LqPaletteRow15FaceCount,
        int HighQualityFaceCount,
        int HighQualityDescriptorCount,
        int HighQualityTriangleCount,
        int HighQualityNormalFaceCount,
        int HighQualityCloseFaceCount,
        int LowPolyPhaseCommandCount,
        int HighPolyBasePhaseCommandCount,
        int NormalHighQualityPhaseCommandCount,
        int CloseHighQualityPhaseCommandCount,
        int MinimumOtBucket,
        int MaximumOtBucket,
        int TiedOtBucketCount);
}

internal sealed record NativeTerrainBoundedCompositorSnapshot(
    bool Activated,
    bool Used,
    bool CacheHit,
    string FallbackReason,
    string Contract,
    string PainterOrderingContract,
    string CoverageContract,
    string TextureModulationContract,
    string DitheringContract,
    string NativeBaseOrderingContract,
    string NativePassOrderContract,
    int Width,
    int Height,
    int LowPolyFaceCount,
    int LowPolyTriangleCount,
    int TexturedHighPolyFaceCount,
    int SentinelHighPolyFaceCount,
    int LowQualityTriangleCount,
    int MinimumLqPaletteRow,
    int MaximumLqPaletteRow,
    int LqPaletteRow15FaceCount,
    int HighQualityFaceCount,
    int HighQualityDescriptorCount,
    int HighQualityTriangleCount,
    int HighQualityNormalFaceCount,
    int HighQualityCloseFaceCount,
    int LowPolyPhaseCommandCount,
    int HighPolyBasePhaseCommandCount,
    int NormalHighQualityPhaseCommandCount,
    int CloseHighQualityPhaseCommandCount,
    int CommandCount,
    int MinimumOtBucket,
    int MaximumOtBucket,
    int TiedOtBucketCount,
    string CommandSha256,
    string Rgb5Sha256,
    string RgbaSha256,
    double RasterMilliseconds,
    string MaterialFingerprint,
    PsxTerrainRasterStatistics? RasterStatistics,
    string ScopeNote)
{
    public string NativeProjectionContract { get; init; } = "";
    public string NativeCameraContract { get; init; } = "";
    public string NativeCameraSource { get; init; } = "";

    public static NativeTerrainBoundedCompositorSnapshot Fallback(string reason) =>
        new(
            Activated: false,
            Used: false,
            CacheHit: false,
            FallbackReason: reason,
            Contract: PsxTerrainBoundedRasterContract.Name,
            PainterOrderingContract: PsxTerrainBoundedRasterContract.PainterOrdering,
            CoverageContract: PsxTerrainBoundedRasterContract.CoverageRule,
            TextureModulationContract: PsxTerrainBoundedRasterContract.TextureModulation,
            DitheringContract: PsxTerrainBoundedRasterContract.Dithering,
            NativeBaseOrderingContract: NativeTerrainOrderingTableContract.Name,
            NativePassOrderContract: NativeTerrainOrderingTableContract.PassOrder,
            Width: 0,
            Height: 0,
            LowPolyFaceCount: 0,
            LowPolyTriangleCount: 0,
            TexturedHighPolyFaceCount: 0,
            SentinelHighPolyFaceCount: 0,
            LowQualityTriangleCount: 0,
            MinimumLqPaletteRow: -1,
            MaximumLqPaletteRow: -1,
            LqPaletteRow15FaceCount: 0,
            HighQualityFaceCount: 0,
            HighQualityDescriptorCount: 0,
            HighQualityTriangleCount: 0,
            HighQualityNormalFaceCount: 0,
            HighQualityCloseFaceCount: 0,
            LowPolyPhaseCommandCount: 0,
            HighPolyBasePhaseCommandCount: 0,
            NormalHighQualityPhaseCommandCount: 0,
            CloseHighQualityPhaseCommandCount: 0,
            CommandCount: 0,
            MinimumOtBucket: 0,
            MaximumOtBucket: 0,
            TiedOtBucketCount: 0,
            CommandSha256: "",
            Rgb5Sha256: "",
            RgbaSha256: "",
            RasterMilliseconds: 0,
            MaterialFingerprint: "",
            RasterStatistics: null,
            ScopeNote: "The bounded compositor was not used; the whole frame remained on the legacy preview path.");
}

internal sealed record NativeTerrainBoundedTileSeamProof(
    int GridColumns,
    int TileSide,
    int CompositeSide,
    int DescriptorCount,
    int CommandCount,
    int DegenerateTriangleCount,
    int BackgroundPixelCount,
    int MismatchedPixelCount,
    string Rgb5Sha256);
