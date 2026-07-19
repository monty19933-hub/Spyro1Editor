using Avalonia;
using Spyro.Editor.Core.Rendering;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class EditorViewport
{
    private const int NativeGteActiveWidth = 512;
    private const int NativeGteActiveHeight = 224;
    private const int NativeGteActiveTop = 8;
    private const string NativeGteProjectionExperimentContract =
        "editor-camera-derived-native-gte-base-points-fixed-512x224-v1";

    private bool _nativeGteProjectionExperimentEnabled;
    private NativeGteExperimentCameraKey? _nativeGteExperimentCameraKey;
    private SpyroRetailTerrainCameraState _nativeGteExperimentCamera;

    internal void SetNativeGteProjectionExperimentForTesting(bool enabled)
    {
        if (_nativeGteProjectionExperimentEnabled == enabled)
            return;

        _nativeGteProjectionExperimentEnabled = enabled;
        InvalidateNativeTerrainBoundedFrame();
        InvalidateVisual();
    }

    private bool TryProjectNativeGteExperimentPoint(
        Rect bounds,
        double worldX,
        double worldY,
        double worldZ,
        out ProjectedPoint point)
    {
        point = default;
        if (!_nativeGteProjectionExperimentEnabled ||
            !TryGetNativeGteExperimentCamera(out SpyroRetailTerrainCameraState camera) ||
            !TryExactInt(worldX, out int x) ||
            !TryExactInt(worldY, out int y) ||
            !TryExactInt(worldZ, out int z) ||
            !SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
                camera,
                new SpyroRetailLowPolyWorldPoint(
                    x,
                    y,
                    z,
                    SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates),
                out PsxGteVector vertex))
        {
            return false;
        }

        PsxGteProjectionResult projection = PsxGteProjection.Rtps(
            camera.ProjectionRegisters,
            PsxGteProjectionFifo.Empty,
            vertex,
            PsxGteProjectionCommand.SpyroEnvironment);
        if (projection.Fifo.Sz3 == 0)
            return false;

        point = new ProjectedPoint(
            new Point(
                bounds.Left + ((projection.Fifo.Sxy2.X / (double)NativeGteActiveWidth) * bounds.Width),
                bounds.Top + (((projection.Fifo.Sxy2.Y - NativeGteActiveTop) / (double)NativeGteActiveHeight) * bounds.Height)),
            projection.Fifo.Sz3);
        return true;
    }

    private bool TryNativeGteExperimentDepth(
        double worldX,
        double worldY,
        double worldZ,
        out double depth)
    {
        depth = 0;
        if (!_nativeGteProjectionExperimentEnabled ||
            !TryGetNativeGteExperimentCamera(out SpyroRetailTerrainCameraState camera) ||
            !TryExactInt(worldX, out int x) ||
            !TryExactInt(worldY, out int y) ||
            !TryExactInt(worldZ, out int z) ||
            !SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
                camera,
                new SpyroRetailLowPolyWorldPoint(
                    x,
                    y,
                    z,
                    SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates),
                out PsxGteVector vertex))
        {
            return false;
        }

        PsxGteProjectionResult projection = PsxGteProjection.Rtps(
            camera.ProjectionRegisters,
            PsxGteProjectionFifo.Empty,
            vertex,
            PsxGteProjectionCommand.SpyroEnvironment);
        depth = projection.Fifo.Sz3;
        return true;
    }

    private bool TryGetNativeGteExperimentCamera(out SpyroRetailTerrainCameraState camera)
    {
        camera = default;
        NativeGteExperimentCameraKey key = new(
            _flyCamera.X,
            _flyCamera.Y,
            _flyCamera.Z,
            _flyCamera.Yaw,
            _flyCamera.Pitch,
            _flipMapY);
        if (_nativeGteExperimentCameraKey == key)
        {
            camera = _nativeGteExperimentCamera;
            return camera.IsEditorDerived;
        }

        _nativeGteExperimentCameraKey = key;
        if (!SpyroEditorDerivedTerrainCamera.TryCreate(
                key.X,
                key.Y,
                key.Z,
                key.Yaw,
                key.Pitch,
                key.FlipMapY,
                out SpyroRetailTerrainCameraInput input) ||
            !SpyroNativeTerrainCamera.TryBuildState(input, out camera) ||
            !camera.IsEditorDerived)
        {
            _nativeGteExperimentCamera = default;
            return false;
        }

        _nativeGteExperimentCamera = camera;
        return true;
    }

    private bool TryBuildNativeGteProjectionExperiment(
        GeometryCandidate geometry,
        out IReadOnlyList<ProjectedTerrainFace> highDetailFaces,
        out IReadOnlyList<ProjectedLowDetailTerrainFace> lowDetailFaces,
        out string blocker)
    {
        highDetailFaces = Array.Empty<ProjectedTerrainFace>();
        lowDetailFaces = Array.Empty<ProjectedLowDetailTerrainFace>();
        blocker = "";

        if (!geometry.HasExactHighPolyCoordinateState ||
            !string.Equals(
                geometry.HighPolyCoordinateContract,
                NativeTerrainHighPolyCoordinates.Contract,
                StringComparison.Ordinal))
        {
            blocker = "The scene does not carry the certified native HP coordinate contract.";
            return false;
        }

        if (!SpyroEditorDerivedTerrainCamera.TryCreate(
                _flyCamera.X,
                _flyCamera.Y,
                _flyCamera.Z,
                _flyCamera.Yaw,
                _flyCamera.Pitch,
                _flipMapY,
                out SpyroRetailTerrainCameraInput input) ||
            !SpyroNativeTerrainCamera.TryBuildState(input, out SpyroRetailTerrainCameraState camera) ||
            !camera.IsEditorDerived)
        {
            blocker = "The Fly camera cannot be represented by the named editor-derived fixed-point adapter.";
            return false;
        }

        Dictionary<int, SceneSectorRenderMetadata> sectors = geometry.SourceSectors
            .GroupBy(sector => sector.SectorIndex)
            .ToDictionary(group => group.Key, group => group.Single());
        if (sectors.Count != geometry.SourceSectors.Count)
        {
            blocker = "The source scene contains duplicate sector identities.";
            return false;
        }

        Dictionary<int, double> sectorDepths = [];
        foreach (SceneSectorRenderMetadata sector in sectors.Values)
        {
            if (!TryExactInt(sector.Center.X, out int centerX) ||
                !TryExactInt(sector.Center.Y, out int centerY) ||
                !TryExactInt(sector.Center.Z, out int centerZ) ||
                !SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
                    camera,
                    new SpyroRetailLowPolyWorldPoint(
                        centerX,
                        centerY,
                        centerZ,
                        SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates),
                    out PsxGteVector centerVector))
            {
                blocker = $"Sector {sector.SectorIndex} center is outside the certified native GTE input lattice.";
                return false;
            }

            PsxGteProjectionResult centerProjection = PsxGteProjection.Rtps(
                camera.ProjectionRegisters,
                PsxGteProjectionFifo.Empty,
                centerVector,
                PsxGteProjectionCommand.SpyroEnvironment);
            sectorDepths.Add(sector.SectorIndex, centerProjection.Fifo.Sz3);
        }

        List<ProjectedTerrainFace> high = [];
        for (int polygonIndex = 0; polygonIndex < geometry.Polygons.Count; polygonIndex++)
        {
            TerrainPolygon polygon = geometry.Polygons[polygonIndex];
            if (!sectors.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector) ||
                !sector.HighPolyCoordinates.HasValue)
            {
                blocker = $"HP face {polygon.RuntimeKey} is missing its certified raw sector words.";
                return false;
            }
            if (!NativeTerrainFarLod.ShouldQueueHighDetailSector(
                    sectorDepths[sector.SectorIndex],
                    sector.Radius,
                    sector.DisableHighDetail))
            {
                continue;
            }
            if (!TryBuildNativeHighPolySlots(
                    camera,
                    sector.HighPolyCoordinates.Value,
                    polygon,
                    out PsxGteVector[] sourceSlots,
                    out int pointCount,
                    out blocker))
            {
                return false;
            }

            NativeTerrainBaseProjectionResult projection = NativeTerrainBaseProjection.Project(
                camera.ProjectionRegisters,
                NativeTerrainBaseProjectionContext.RawSxy,
                sourceSlots);
            if (projection.Slots.Any(slot => slot.Depth == 0))
                continue;

            Point[] points = new Point[pointCount];
            double[] depths = new double[pointCount];
            bool[] assigned = new bool[pointCount];
            for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
            {
                int pointIndex = polygon.CornerPointIndexes[slot];
                NativeTerrainBaseProjectedSlot projected = projection.Slots[slot];
                Point point = new(projected.ScreenPoint.X, projected.ScreenPoint.Y);
                double depth = projected.Depth / 4.0;
                if (assigned[pointIndex] &&
                    (points[pointIndex] != point || depths[pointIndex] != depth))
                {
                    blocker = $"HP face {polygon.RuntimeKey} repeated a source point with inconsistent native projection.";
                    return false;
                }
                points[pointIndex] = point;
                depths[pointIndex] = depth;
                assigned[pointIndex] = true;
            }
            if (assigned.Any(value => !value))
            {
                blocker = $"HP face {polygon.RuntimeKey} did not project every unique native point.";
                return false;
            }

            high.Add(new ProjectedTerrainFace(
                polygonIndex,
                polygon,
                points,
                depths.Average(),
                CameraDepths: depths));
        }

        List<ProjectedLowDetailTerrainFace> low = [];
        foreach (LowDetailTerrainPolygon polygon in geometry.LowDetailPolygons)
        {
            if (!sectors.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector))
            {
                blocker = $"LP face {polygon.RuntimeKey} references an unknown sector.";
                return false;
            }
            if (!NativeTerrainFarLod.ShouldQueueLowDetailSector(
                    sectorDepths[sector.SectorIndex],
                    sector.Radius,
                    sector.DisableLowDetail,
                    sector.ForceLowDetail))
            {
                continue;
            }
            if (!TryBuildNativeLowPolySlots(camera, polygon, out PsxGteVector[] sourceSlots, out blocker))
                return false;

            Point[] rawPoints = new Point[SourceSceneOverlayContract.CornerSlotCount];
            double[] rawDepths = new double[SourceSceneOverlayContract.CornerSlotCount];
            PsxGteProjectionFifo fifo = PsxGteProjectionFifo.Empty;
            bool behind = false;
            for (int slot = 0; slot < sourceSlots.Length; slot++)
            {
                PsxGteProjectionResult projected = PsxGteProjection.Rtps(
                    camera.ProjectionRegisters,
                    fifo,
                    sourceSlots[slot],
                    PsxGteProjectionCommand.SpyroEnvironment);
                fifo = projected.Fifo;
                rawPoints[slot] = new Point(projected.Fifo.Sxy2.X, projected.Fifo.Sxy2.Y);
                rawDepths[slot] = projected.Fifo.Sz3;
                behind |= projected.Fifo.Sz3 == 0;
            }
            if (behind ||
                !NativeTerrainFarLod.ShouldRenderLowDetailFace(rawDepths, polygon.TransitionBias) ||
                !NativeTerrainOrderingTableContract.TryComputeLowPolyBucket(
                    rawDepths,
                    polygon.RawWord1,
                    out _,
                    out _))
            {
                continue;
            }

            double averageDepth = rawDepths.Average();
            low.Add(new ProjectedLowDetailTerrainFace(
                polygon,
                rawPoints,
                rawDepths,
                averageDepth,
                averageDepth + (polygon.OrderingTableBias * 8.0)));
        }

        highDetailFaces = high;
        lowDetailFaces = low;
        return true;
    }

    private static bool TryBuildNativeHighPolySlots(
        in SpyroRetailTerrainCameraState camera,
        NativeTerrainHpSectorCoordinatePayload sector,
        TerrainPolygon polygon,
        out PsxGteVector[] slots,
        out int pointCount,
        out string blocker)
    {
        slots = new PsxGteVector[SourceSceneOverlayContract.CornerSlotCount];
        pointCount = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
        blocker = "";
        if (!polygon.HasCompleteNativeHighPolyFacePayload ||
            pointCount is < 3 or > SourceSceneOverlayContract.CornerSlotCount ||
            polygon.CornerPointIndexes.Count != SourceSceneOverlayContract.CornerSlotCount)
        {
            blocker = $"HP face {polygon.RuntimeKey} is missing its exact four-slot coordinate payload.";
            return false;
        }

        SpyroRetailHighPolyWorldPoint4[] worldPoints = new SpyroRetailHighPolyWorldPoint4[pointCount];
        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            if (!TryExactInt(polygon.Points[pointIndex].X, out int x) ||
                !TryExactInt(polygon.Points[pointIndex].Y, out int y) ||
                !TryExactInt(polygon.ZValues[pointIndex], out int z))
            {
                blocker = $"HP face {polygon.RuntimeKey} contains a non-integer source coordinate.";
                return false;
            }

            try
            {
                worldPoints[pointIndex] = NativeTerrainHighPolyCoordinates.ReconstructCertifiedWorldPointX4(
                    sector,
                    x,
                    y,
                    z);
            }
            catch (InvalidDataException ex)
            {
                blocker = $"HP face {polygon.RuntimeKey} failed native coordinate reconstruction: {ex.Message}";
                return false;
            }
        }

        for (int slot = 0; slot < slots.Length; slot++)
        {
            int pointIndex = polygon.CornerPointIndexes[slot];
            if (pointIndex < 0 || pointIndex >= pointCount ||
                !SpyroNativeTerrainCamera.TryAdaptHighPolyWorldToGte(
                    camera,
                    worldPoints[pointIndex],
                    out slots[slot]))
            {
                blocker = $"HP face {polygon.RuntimeKey} slot {slot} is outside the certified GTE halfword lattice.";
                return false;
            }
        }
        return true;
    }

    private static bool TryBuildNativeLowPolySlots(
        in SpyroRetailTerrainCameraState camera,
        LowDetailTerrainPolygon polygon,
        out PsxGteVector[] slots,
        out string blocker)
    {
        slots = new PsxGteVector[SourceSceneOverlayContract.CornerSlotCount];
        blocker = "";
        int pointCount = Math.Min(polygon.Points.Count, polygon.ZValues.Count);
        if (!polygon.HasCompleteNativePayload ||
            pointCount is < 3 or > SourceSceneOverlayContract.CornerSlotCount ||
            polygon.CornerPointIndexes.Count != SourceSceneOverlayContract.CornerSlotCount)
        {
            blocker = $"LP face {polygon.RuntimeKey} is missing its exact four-slot coordinate payload.";
            return false;
        }

        SpyroRetailLowPolyWorldPoint[] worldPoints = new SpyroRetailLowPolyWorldPoint[pointCount];
        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            if (!TryExactInt(polygon.Points[pointIndex].X, out int x) ||
                !TryExactInt(polygon.Points[pointIndex].Y, out int y) ||
                !TryExactInt(polygon.ZValues[pointIndex], out int z))
            {
                blocker = $"LP face {polygon.RuntimeKey} contains a non-integer source coordinate.";
                return false;
            }
            worldPoints[pointIndex] = new SpyroRetailLowPolyWorldPoint(
                x,
                y,
                z,
                SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates);
        }

        for (int slot = 0; slot < slots.Length; slot++)
        {
            int pointIndex = polygon.CornerPointIndexes[slot];
            if (pointIndex < 0 || pointIndex >= pointCount ||
                !SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
                    camera,
                    worldPoints[pointIndex],
                    out slots[slot]))
            {
                blocker = $"LP face {polygon.RuntimeKey} slot {slot} is outside the certified GTE halfword lattice.";
                return false;
            }
        }
        return true;
    }

    private static bool TryExactInt(float value, out int result)
    {
        if (!float.IsFinite(value) || value < int.MinValue || value > int.MaxValue || value != MathF.Truncate(value))
        {
            result = 0;
            return false;
        }
        result = checked((int)value);
        return true;
    }

    private static bool TryExactInt(double value, out int result)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue || value != Math.Truncate(value))
        {
            result = 0;
            return false;
        }
        result = checked((int)value);
        return true;
    }

    private readonly record struct NativeGteExperimentCameraKey(
        double X,
        double Y,
        double Z,
        double Yaw,
        double Pitch,
        bool FlipMapY);
}
