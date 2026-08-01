using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.App.Views;

public sealed partial class EditorViewport : Control
{
    // Spyro's HP environment renderer works four GTE depth units per editor
    // world unit.  With the retail 0x8000 LOD distance, DPCS holds the near
    // endpoint through 0x1000 GTE units and reaches the far endpoint at 0x2000.
    internal const double NativeTerrainDepthCueNearEnd = 0x1000 / 4.0;
    internal const double NativeTerrainDepthCueFarEnd = 0x2000 / 4.0;
    internal const string NativeTerrainMapMaterialContract =
        "native-load-normal-hq-plus-physical-table2-complete-scene-map-v5";
    internal const int InteractiveFlyMinimumFullMaterialFaceBudget = 768;
    internal const int InteractiveFlyMaximumFullMaterialFaceBudget = 2048;
    internal const double FlyPlanarMoveStep = 260;
    internal const double FlyVerticalMoveStep = 64;
    internal const int NativeEntryCameraElevationRaw = 0xA0;
    internal const int NativeEntryCameraRadiusRaw = 0xA00;
    internal const double NativeEntryCameraCoordinateScale = 16.0;
    internal const double FlyGameViewFogNearDistance = NativeTerrainFarLod.LodDistanceEditorUnits;
    internal const double FlyGameViewFogFarDistance =
        NativeTerrainFarLod.ConservativeAllSectorCullingDistanceFixed16 / 32.0;
    // Native LP faces are emitted by r_environment as one PS1 POLY_G4 packet
    // in their four raw slots. The two retail NCLIP/raster faces are [0,1,2]
    // and [3,1,2] (the latter is the same cyclic winding as [1,2,3]). The
    // [0,1,3]/[2,1,3] order belongs to the separate HP base path. A perimeter
    // fan or that HP slot order overlaps part of each LP quad and leaves holes.
    private static readonly (int A, int B, int C)[] NativeLowDetailTriangleSlots =
    [
        (0, 1, 2),
        (3, 1, 2)
    ];
    private const double InteractiveFlyMaterialBudgetPixelsPerFace = 720.0;
    private static readonly TimeSpan InteractiveFlySettleDelay = TimeSpan.FromMilliseconds(160);

    private static readonly Dictionary<string, string> MobyMarkerImageFiles = new(StringComparer.Ordinal)
    {
        ["Ban"] = "banana-boy.png",
        ["Str"] = "strongarm.png",
        ["WFo"] = "winged-fool.png",
        ["AFo"] = "armored-fool.png",
        ["Msh"] = "mushroom.png",
        ["DDg"] = "puppy-devil-dog.png",
        ["DCp"] = "devil-cupid.png",
        ["Tur"] = "turtle-mutant-turtle.png",
        ["LFo"] = "lamp-fool.png"
    };

    private static readonly Dictionary<string, Bitmap?> MobyMarkerImageCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<MobyRasterIconCacheKey, MobyRasterIconImage?> MobyRasterIconCache = new();
    private static readonly object MobyRasterIconDiagnosticsGate = new();
    private static readonly Dictionary<MobyRasterIconAtlasCellId, int> MobyRasterIconAtlasDrawCounts = new();
    private readonly Dictionary<int, string> _normalTerrainTextureImageFiles = new();
    private readonly Dictionary<int, string> _closeTerrainTextureImageFiles = new();
    private readonly Dictionary<int, TerrainTextureImageFileStamp>
        _normalTerrainTextureImageFileStamps = new();
    private readonly Dictionary<int, TerrainTextureImageFileStamp>
        _closeTerrainTextureImageFileStamps = new();
    private readonly Dictionary<(int TextureId, NativeTerrainTexturePreviewTier Tier), Bitmap?> _terrainTextureImageCache = new();
    private readonly Dictionary<int, Color?> _normalTerrainTextureAverageColorCache = new();
    private readonly Dictionary<int, NativeTerrainLqTextureRecordPayload> _nativeTerrainLqTextureRecords = new();
    private readonly Dictionary<NativeTerrainLqFrameCacheKey, Bitmap?> _nativeTerrainLqFrameCache = new();
    private readonly DispatcherTimer _flyNavigationSettleTimer;
    private readonly HashSet<Key> _activeFlyNavigationKeys = new();

    public static readonly StyledProperty<GeometryCandidate?> GeometryProperty =
        AvaloniaProperty.Register<EditorViewport, GeometryCandidate?>(nameof(Geometry));

    public static readonly StyledProperty<IReadOnlyList<Moby>> MobysProperty =
        AvaloniaProperty.Register<EditorViewport, IReadOnlyList<Moby>>(nameof(Mobys), Array.Empty<Moby>());

    private readonly List<ScreenTerrainFace> _screenTerrainFaces = new();
    private readonly List<ScreenMoby> _screenMobys = new();
    private readonly List<ScreenTerrainSurfaceLabel> _screenTerrainSurfaceLabels = new();
    private readonly Dictionary<int, NativeTerrainFaceRenderKind> _flyHighDetailRenderKinds = new();
    private readonly HashSet<int> _flyCameraInactiveBackdropFaceIndexes = [];
    private readonly Dictionary<string, int> _flyVisibleLowDetailCornerColorCounts = new(StringComparer.Ordinal);
    private ScreenFacingGuide? _screenFacingGuide;
    private Point _lastPointerPosition;
    private bool _isPanning;
    private bool _isDraggingMoby;
    private bool _isDraggingMobyFacing;
    private bool _isDraggingTerrainFace;
    private bool _isDraggingTerrainPoint;
    private bool _draggingTerrainAsAddCopy;
    private bool _isPaintingTerrain;
    private int _draggingMobyIndex = -1;
    private int _draggingMobyFacingIndex = -1;
    private int _draggingTerrainFaceIndex = -1;
    private int _draggingTerrainIndex = -1;
    private int _draggingTerrainPointIndex = -1;
    private double _zoom = 1;
    private Vector _pan;
    private bool _flipMapY = EditorUiDefaults.UseGameViewMapOrientation;
    private ViewportViewMode _viewMode = ViewportViewMode.Map;
    private FlyCamera _flyCamera;
    private GeometryCandidate? _sceneFitGeometry;
    private SceneFitFocus? _sceneFitFocus;
    private int _selectedTerrainIndex = -1;
    private int _selectedTerrainPointIndex = -1;
    private int _selectedMobyIndex = -1;
    private int _hoverTerrainIndex = -1;
    private StandardCursorType? _currentCursorType;
    private bool _objectPlacementMode;
    private bool _terrainFocusMode;
    private bool _terrainTexturePaintMode;
    private bool _terrainTexturePaintBusy;
    private string _terrainTexturePaintHint = "";
    private double _terrainBrushRadius = 512;
    private double _terrainBrushStrength = 64;
    private double _terrainBrushFeather = 50;
    private TerrainBrushAction _terrainBrushAction = TerrainBrushAction.Off;
    private string _terrainBrushSafetyHint = "";
    private Vector2f? _lastTerrainBrushWorldPoint;
    private Vector2f? _terrainBrushPreviewWorldPoint;
    private int _terrainBrushPreviewTerrainIndex = -1;
    private string _emptyMessage = "Open a workspace or build the portable cache to load captured level data.";
    private Func<Moby, bool>? _mobyFilter;
    private Func<GeometryCandidate, TerrainPolygon, bool>? _playableTerrainViewPredicate;
    private Func<GeometryCandidate, TerrainPolygon, bool>? _mapTerrainBackdropOutlinePredicate;
    private TerrainSceneViewMode _terrainSceneViewMode = TerrainSceneViewMode.CompleteScene;
    private Func<TerrainPolygon, TerrainPatchSafetyKind>? _terrainPatchSafetyClassifier;
    private Func<TerrainPolygon, int, TerrainBrushPreviewVertexKind>? _terrainBrushVertexClassifier;
    private EnvironmentGradeViewportPreview? _environmentGradePreview;
    private bool _useNativeTerrainDepthCue = true;
    private bool _flyGameViewCameraLocal;
    private bool _flyCameraIsOverview = true;
    private NativeTerrainLodPreviewMode _nativeTerrainLodPreviewMode =
        NativeTerrainLodPreviewMode.EditorOverviewHighDetail;
    private bool _flyNavigationInteractiveMaterialLod;
    private bool _holdFlyNavigationMaterialLodForTesting;
    private FlyTerrainInteractiveLodSnapshot _flyTerrainInteractiveLodSnapshot =
        FlyTerrainInteractiveLodSnapshot.Empty;
    private NativeTerrainMapMaterialSnapshot _nativeTerrainMapMaterialSnapshot =
        NativeTerrainMapMaterialSnapshot.Empty;
    private FlyTerrainVisibilitySnapshot _flyTerrainVisibilitySnapshot =
        FlyTerrainVisibilitySnapshot.Empty;
    private NativeTerrainOcclusionSnapshot _nativeTerrainOcclusionSnapshot =
        NativeTerrainOcclusionSnapshot.Empty;
    private GeometryCandidate? _lastFlyOcclusionGeometry;
    private int _lastFlyOcclusionGroupIndex = -1;
    private int _lastFlyOcclusionTriangleIndex = -1;
    private double _lastFlyOcclusionFloorZ = double.NegativeInfinity;
    private PortableLevelEntryPose? _levelEntryPose;
    private GameCameraEntrySnapshot _gameCameraEntrySnapshot =
        GameCameraEntrySnapshot.Empty;

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<Moby>? MobyEditRequested;
    public event EventHandler<Moby>? MobyUndoRequested;
    public event EventHandler<ViewportTerrainEditRequestedEventArgs>? TerrainEditRequested;
    public event EventHandler<ViewportTerrainMoveRequestedEventArgs>? TerrainMoveRequested;
    public event EventHandler<ViewportTerrainRemoveRequestedEventArgs>? TerrainRemoveRequested;
    public event EventHandler<ViewportTerrainLookRequestedEventArgs>? TerrainLookCopyRequested;
    public event EventHandler<ViewportTerrainLookRequestedEventArgs>? TerrainLookPasteRequested;
    public event EventHandler<ViewportTerrainPointEditRequestedEventArgs>? TerrainPointEditRequested;
    public event EventHandler<ViewportTerrainPointMoveRequestedEventArgs>? TerrainPointMoveRequested;
    public event EventHandler<ViewportTerrainBrushRequestedEventArgs>? TerrainBrushRequested;
    public event EventHandler<ViewportTerrainBrushAdjustmentRequestedEventArgs>? TerrainBrushAdjustmentRequested;
    public event EventHandler<ViewportTerrainBrushModeRequestedEventArgs>? TerrainBrushModeRequested;
    public event EventHandler? TerrainBrushStrokeFinished;
    public event EventHandler<ViewportTerrainTexturePaintRequestedEventArgs>? TerrainTexturePaintRequested;
    public event EventHandler? TerrainTexturePaintCanceled;
    public event EventHandler<MobyMoveRequestedEventArgs>? MobyMoveRequested;
    public event EventHandler<MobyRotateRequestedEventArgs>? MobyRotateRequested;
    public event EventHandler<ViewportObjectPlacementRequestedEventArgs>? ObjectPlacementRequested;
    public event EventHandler? ObjectPlacementCanceled;
    public event EventHandler? ObjectCopyRequested;
    public event EventHandler<ViewportObjectPasteRequestedEventArgs>? ObjectPasteRequested;
    public event EventHandler<ViewportViewMode>? ViewModeChanged;

    public string EmptyMessage
    {
        get => _emptyMessage;
        set
        {
            _emptyMessage = string.IsNullOrWhiteSpace(value) ? "No captured level data loaded." : value;
            InvalidateVisual();
        }
    }

    public GeometryCandidate? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public IReadOnlyList<Moby> Mobys
    {
        get => GetValue(MobysProperty);
        set => SetValue(MobysProperty, value);
    }

    public ViewportViewMode ViewMode => _viewMode;

    public bool IsMapYFlipped => _flipMapY;

    public TerrainSceneViewMode TerrainSceneViewMode => _terrainSceneViewMode;

    public Point LastPointerPosition => _lastPointerPosition;

    internal void SetEnvironmentGradePreview(EnvironmentGradeViewportPreview? preview)
    {
        _environmentGradePreview = preview;
        InvalidateNativeTerrainBoundedFrame(incrementEnvironmentGeneration: true);
        InvalidateVisual();
    }

    public void SetLevelEntryPose(PortableLevelEntryPose? pose)
    {
        _levelEntryPose = pose;
        _gameCameraEntrySnapshot = GameCameraEntrySnapshot.Empty;
    }

    public bool ObjectPlacementMode
    {
        get => _objectPlacementMode;
        set
        {
            if (_objectPlacementMode == value)
                return;

            _objectPlacementMode = value;
            UpdateViewportCursor(_lastPointerPosition);
        }
    }

    public double TerrainBrushRadius
    {
        get => _terrainBrushRadius;
        set
        {
            double radius = Math.Clamp(value, 1, 4096);
            if (Math.Abs(_terrainBrushRadius - radius) <= 0.001)
                return;

            _terrainBrushRadius = radius;
            InvalidateVisual();
        }
    }

    public double TerrainBrushStrength
    {
        get => _terrainBrushStrength;
        set
        {
            double strength = Math.Clamp(value, 1, 4096);
            if (Math.Abs(_terrainBrushStrength - strength) <= 0.001)
                return;

            _terrainBrushStrength = strength;
            InvalidateVisual();
        }
    }

    public double TerrainBrushFeather
    {
        get => _terrainBrushFeather;
        set
        {
            double feather = Math.Clamp(value, 0, 100);
            if (Math.Abs(_terrainBrushFeather - feather) <= 0.001)
                return;

            _terrainBrushFeather = feather;
            InvalidateVisual();
        }
    }

    public TerrainBrushAction TerrainBrushAction
    {
        get => _terrainBrushAction;
        set
        {
            if (_terrainBrushAction == value)
                return;

            _terrainBrushAction = value;
            _isPaintingTerrain = false;
            _lastTerrainBrushWorldPoint = null;
            _terrainBrushPreviewWorldPoint = null;
            SetTerrainPresentationPin(ref _terrainBrushPreviewTerrainIndex, -1);
            SetTerrainPresentationPin(ref _hoverTerrainIndex, -1);
            SetViewportCursor(null);
            InvalidateVisual();
        }
    }

    public bool TerrainFocusMode
    {
        get => _terrainFocusMode;
        set
        {
            if (_terrainFocusMode == value)
                return;

            _terrainFocusMode = value;
            InvalidateVisual();
        }
    }

    public bool TerrainTexturePaintMode
    {
        get => _terrainTexturePaintMode;
        set
        {
            if (_terrainTexturePaintMode == value)
                return;

            _terrainTexturePaintMode = value;
            _terrainTexturePaintBusy = false;
            UpdateViewportCursor(_lastPointerPosition);
            InvalidateVisual();
        }
    }

    public bool TerrainTexturePaintBusy
    {
        get => _terrainTexturePaintBusy;
        set
        {
            if (_terrainTexturePaintBusy == value)
                return;

            _terrainTexturePaintBusy = value;
            UpdateViewportCursor(_lastPointerPosition);
            InvalidateVisual();
        }
    }

    public string TerrainTexturePaintHint
    {
        get => _terrainTexturePaintHint;
        set
        {
            string hint = value?.Trim() ?? "";
            if (string.Equals(_terrainTexturePaintHint, hint, StringComparison.Ordinal))
                return;

            _terrainTexturePaintHint = hint;
            InvalidateVisual();
        }
    }

    public string TerrainBrushSafetyHint
    {
        get => _terrainBrushSafetyHint;
        set
        {
            string hint = value?.Trim() ?? "";
            if (string.Equals(_terrainBrushSafetyHint, hint, StringComparison.Ordinal))
                return;

            _terrainBrushSafetyHint = hint;
            InvalidateVisual();
        }
    }

    public Func<Moby, bool>? MobyFilter
    {
        get => _mobyFilter;
        set
        {
            _mobyFilter = value;
            if (_terrainSceneViewMode == TerrainSceneViewMode.Playable)
                RefreshTerrainScenePresentation();
            else
                InvalidateVisual();
        }
    }

    public Func<GeometryCandidate, TerrainPolygon, bool>? PlayableTerrainViewPredicate
    {
        get => _playableTerrainViewPredicate;
        set
        {
            if (ReferenceEquals(_playableTerrainViewPredicate, value))
                return;

            _playableTerrainViewPredicate = value;
            RefreshTerrainScenePresentation();
        }
    }

    /// <summary>
    /// Presentation-only role for source-proven overview/backdrop sheets. It
    /// never removes a polygon from Fit, hit testing, snapping, persistence, or
    /// export; Edit Map keeps the exact HP mesh as a selectable outline and
    /// materializes an individual face while it is hovered, selected, or
    /// edited. Game Camera continues to render the native camera/LOD material.
    /// </summary>
    public Func<GeometryCandidate, TerrainPolygon, bool>? MapTerrainBackdropOutlinePredicate
    {
        get => _mapTerrainBackdropOutlinePredicate;
        set
        {
            if (ReferenceEquals(_mapTerrainBackdropOutlinePredicate, value))
                return;

            _mapTerrainBackdropOutlinePredicate = value;
            InvalidateVisual();
        }
    }

    public Func<TerrainPolygon, TerrainPatchSafetyKind>? TerrainPatchSafetyClassifier
    {
        get => _terrainPatchSafetyClassifier;
        set
        {
            _terrainPatchSafetyClassifier = value;
            InvalidateVisual();
        }
    }

    public Func<TerrainPolygon, int, TerrainBrushPreviewVertexKind>? TerrainBrushVertexClassifier
    {
        get => _terrainBrushVertexClassifier;
        set
        {
            _terrainBrushVertexClassifier = value;
            InvalidateVisual();
        }
    }

    static EditorViewport()
    {
        GeometryProperty.Changed.AddClassHandler<EditorViewport>((viewport, _) => viewport.ResetView());
        MobysProperty.Changed.AddClassHandler<EditorViewport>((viewport, _) =>
        {
            viewport._sceneFitGeometry = null;
            viewport._sceneFitFocus = null;
            viewport.ResetSelection();
        });
    }

    public EditorViewport()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        _flyNavigationSettleTimer = new DispatcherTimer
        {
            Interval = InteractiveFlySettleDelay
        };
        _flyNavigationSettleTimer.Tick += (_, _) => SettleFlyNavigationMaterialLod(force: false);
        LostFocus += OnViewportLostFocus;
    }

    internal static void ResetMobyRasterIconRenderDiagnosticsForTesting()
    {
        lock (MobyRasterIconDiagnosticsGate)
            MobyRasterIconAtlasDrawCounts.Clear();
    }

    internal static IReadOnlyDictionary<MobyRasterIconAtlasCellId, int> CaptureMobyRasterIconRenderDiagnosticsForTesting()
    {
        lock (MobyRasterIconDiagnosticsGate)
            return new Dictionary<MobyRasterIconAtlasCellId, int>(MobyRasterIconAtlasDrawCounts);
    }

    internal static IReadOnlyList<(int A, int B, int C)> CaptureNativeLowDetailTriangleSlotsForTesting() =>
        NativeLowDetailTriangleSlots;

    internal void SetFlyGameCameraForTesting(
        double worldX,
        double worldY,
        double worldZ,
        double yaw,
        double pitch)
    {
        _viewMode = ViewportViewMode.Fly3D;
        _flyCamera = new FlyCamera(ToFlyViewX(worldX), ToFlyViewY(worldY), worldZ, yaw, pitch);
        _flyCameraIsOverview = false;
        _flyGameViewCameraLocal = _flipMapY;
        _nativeTerrainLodPreviewMode = NativeTerrainLodPreviewMode.EditorOverviewHighDetail;
        InvalidateVisual();
    }

    internal string? ResolveTerrainTextureImagePathForTesting(int textureId, double minimumCameraDepth)
    {
        NativeTerrainTexturePreviewTier tier =
            NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(minimumCameraDepth);
        return ResolveTerrainTextureImagePath(textureId, tier);
    }

    internal bool LoadTerrainTextureImageForTesting(
        int textureId,
        double minimumCameraDepth)
    {
        NativeTerrainTexturePreviewTier tier =
            NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(
                minimumCameraDepth);
        return GetTerrainTextureImage(textureId, tier) != null;
    }

    internal NativeTerrainLqFrameSnapshot CaptureNativeTerrainLqFrameForTesting(
        int textureId,
        double planarEditorDepth,
        bool lqFadeBypass = false,
        bool hqOverlayBypass = false)
    {
        double[] rawDepths = [planarEditorDepth, planarEditorDepth, planarEditorDepth, planarEditorDepth];
        NativeTerrainLqPreviewSelection selection = NativeTerrainFarLod.SelectHighDetailTexturePath(
            rawDepths,
            lqFadeBypass,
            hqOverlayBypass);
        NativeTerrainTexturePreviewTier? hqTier = selection.HqOverlayEligible
            ? NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(planarEditorDepth)
            : null;

        NativeTerrainLqTextureDescriptorPayload? descriptor = null;
        bool hasPayload = _nativeTerrainLqTextureRecords.TryGetValue(
                textureId,
                out NativeTerrainLqTextureRecordPayload? texture) &&
            selection.DescriptorIndex is >= 0 and < NativeTerrainLqTextureCacheCodec.DescriptorCount;
        if (hasPayload)
            descriptor = texture!.GetDescriptor(selection.DescriptorIndex);

        bool retainedWithoutLowDetail = !selection.HighPolyVisible &&
            Geometry?.LowDetailPolygons.Count == 0;
        Bitmap? bitmap = selection.HighPolyVisible || retainedWithoutLowDetail
            ? GetNativeTerrainLqFrame(
                textureId,
                selection.DescriptorIndex,
                selection.PaletteRow,
                hqTier)
            : null;
        string bitmapSha256 = bitmap != null && TryReadBitmapRgba(bitmap, out byte[] bitmapPixels)
            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bitmapPixels))
            : "";
        return new NativeTerrainLqFrameSnapshot(
            textureId,
            planarEditorDepth,
            selection.HighPolyVisible,
            retainedWithoutLowDetail,
            selection.HqOverlayEligible,
            selection.DescriptorIndex,
            selection.PaletteRow,
            hqTier,
            descriptor != null,
            bitmap != null,
            bitmap?.PixelSize.Width ?? 0,
            bitmap?.PixelSize.Height ?? 0,
            bitmapSha256,
            descriptor?.Abr ?? -1,
            descriptor?.RawDescriptorSha256 ?? "",
            descriptor?.PackedIndicesSha256 ?? "",
            descriptor?.PaletteWordsSha256 ?? "");
    }

    public void SetTerrainTextureImageFiles(
        IReadOnlyDictionary<int, string>? normalFiles,
        IReadOnlyDictionary<int, string>? closeFiles = null,
        IReadOnlyDictionary<int, NativeTerrainLqTextureRecordPayload>? lowDetailTextures = null)
    {
        Dictionary<int, string> nextNormal = new();
        Dictionary<int, string> nextClose = new();
        Dictionary<int, TerrainTextureImageFileStamp> nextNormalStamps = new();
        Dictionary<int, TerrainTextureImageFileStamp> nextCloseStamps = new();
        Dictionary<int, NativeTerrainLqTextureRecordPayload> nextLowDetail = new();
        if (normalFiles != null)
        {
            foreach ((int textureId, string path) in normalFiles)
            {
                if (textureId >= 0 &&
                    TryCreateTerrainTextureImageFileStamp(
                        path,
                        out TerrainTextureImageFileStamp stamp))
                {
                    nextNormal[textureId] = path;
                    nextNormalStamps[textureId] = stamp;
                }
            }
        }
        if (closeFiles != null)
        {
            foreach ((int textureId, string path) in closeFiles)
            {
                if (textureId >= 0 &&
                    TryCreateTerrainTextureImageFileStamp(
                        path,
                        out TerrainTextureImageFileStamp stamp))
                {
                    nextClose[textureId] = path;
                    nextCloseStamps[textureId] = stamp;
                }
            }
        }
        if (lowDetailTextures != null)
        {
            foreach ((int textureId, NativeTerrainLqTextureRecordPayload texture) in lowDetailTextures)
            {
                if (textureId >= 0 && texture != null)
                    nextLowDetail[textureId] = texture;
            }
        }

        HashSet<int> changedTextureIds = ChangedFileStampTextureIds(
            _normalTerrainTextureImageFileStamps,
            nextNormalStamps);
        changedTextureIds.UnionWith(ChangedFileStampTextureIds(
            _closeTerrainTextureImageFileStamps,
            nextCloseStamps));
        HashSet<int> changedLowDetailIds = ChangedPayloadTextureIds(
            _nativeTerrainLqTextureRecords,
            nextLowDetail);
        changedTextureIds.UnionWith(changedLowDetailIds);

        foreach (var key in _terrainTextureImageCache.Keys
            .Where(key => changedTextureIds.Contains(key.TextureId))
            .ToArray())
        {
            _terrainTextureImageCache[key]?.Dispose();
            _terrainTextureImageCache.Remove(key);
        }
        foreach (NativeTerrainLqFrameCacheKey key in _nativeTerrainLqFrameCache.Keys
            .Where(key => changedTextureIds.Contains(key.TextureId))
            .ToArray())
        {
            _nativeTerrainLqFrameCache[key]?.Dispose();
            _nativeTerrainLqFrameCache.Remove(key);
        }
        foreach (int textureId in changedTextureIds)
            _normalTerrainTextureAverageColorCache.Remove(textureId);

        ReplaceDictionary(_normalTerrainTextureImageFiles, nextNormal);
        ReplaceDictionary(_closeTerrainTextureImageFiles, nextClose);
        ReplaceDictionary(_normalTerrainTextureImageFileStamps, nextNormalStamps);
        ReplaceDictionary(_closeTerrainTextureImageFileStamps, nextCloseStamps);
        ReplaceDictionary(_nativeTerrainLqTextureRecords, nextLowDetail);

        if (changedLowDetailIds.Count > 0)
            OnNativeTerrainLqRecordsChanged();
        InvalidateVisual();
    }

    internal IReadOnlyList<(int TextureId, NativeTerrainTexturePreviewTier Tier)>
        GetLoadedTerrainTextureImageCacheKeysForTesting() =>
        _terrainTextureImageCache
            .Where(item => item.Value != null)
            .Select(item => item.Key)
            .OrderBy(item => item.TextureId)
            .ThenBy(item => item.Tier)
            .ToArray();

    private static HashSet<int> ChangedFileStampTextureIds(
        IReadOnlyDictionary<int, TerrainTextureImageFileStamp> current,
        IReadOnlyDictionary<int, TerrainTextureImageFileStamp> next)
    {
        HashSet<int> changed = current.Keys
            .Concat(next.Keys)
            .Distinct()
            .Where(textureId =>
                !current.TryGetValue(
                    textureId,
                    out TerrainTextureImageFileStamp currentStamp) ||
                !next.TryGetValue(
                    textureId,
                    out TerrainTextureImageFileStamp nextStamp) ||
                !string.Equals(
                    currentStamp.FullPath,
                    nextStamp.FullPath,
                    StringComparison.OrdinalIgnoreCase) ||
                currentStamp.Length != nextStamp.Length ||
                currentStamp.LastWriteTimeUtcTicks != nextStamp.LastWriteTimeUtcTicks)
            .ToHashSet();
        return changed;
    }

    private static bool TryCreateTerrainTextureImageFileStamp(
        string? path,
        out TerrainTextureImageFileStamp stamp)
    {
        stamp = default;
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            FileInfo info = new(Path.GetFullPath(path));
            if (!info.Exists)
                return false;
            stamp = new TerrainTextureImageFileStamp(
                info.FullName,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static HashSet<int> ChangedPayloadTextureIds(
        IReadOnlyDictionary<int, NativeTerrainLqTextureRecordPayload> current,
        IReadOnlyDictionary<int, NativeTerrainLqTextureRecordPayload> next)
    {
        return current.Keys
            .Concat(next.Keys)
            .Distinct()
            .Where(textureId =>
                !current.TryGetValue(
                    textureId,
                    out NativeTerrainLqTextureRecordPayload? currentPayload) ||
                !next.TryGetValue(
                    textureId,
                    out NativeTerrainLqTextureRecordPayload? nextPayload) ||
                !ReferenceEquals(currentPayload, nextPayload))
            .ToHashSet();
    }

    private static void ReplaceDictionary<TKey, TValue>(
        Dictionary<TKey, TValue> destination,
        IReadOnlyDictionary<TKey, TValue> source)
        where TKey : notnull
    {
        destination.Clear();
        foreach ((TKey key, TValue value) in source)
            destination[key] = value;
    }

    private readonly record struct TerrainTextureImageFileStamp(
        string FullPath,
        long Length,
        long LastWriteTimeUtcTicks);

    public void ResetView()
    {
        _zoom = 1;
        _pan = default;
        // Clear pinned terrain before calculating Fit. Otherwise a selected
        // off-camera source sheet can remain in the first exhaustive Fit even
        // though Reset immediately clears that selection.
        ResetSelection();
        _sceneFitGeometry = null;
        _sceneFitFocus = null;
        ResetFlyCamera(preferGameViewStart: false);
    }

    public void SetViewMode(ViewportViewMode mode)
    {
        if (_viewMode == mode)
        {
            if (mode == ViewportViewMode.Fly3D && _flipMapY && _flyCameraIsOverview)
            {
                ResetFlyCamera(preferGameViewStart: true);
                InvalidateVisual();
            }
            return;
        }

        _viewMode = mode;
        ResetFlyCamera(preferGameViewStart: mode == ViewportViewMode.Fly3D && _flipMapY);
        ViewModeChanged?.Invoke(this, _viewMode);
        InvalidateVisual();
    }

    public void SetTerrainSceneViewMode(TerrainSceneViewMode mode)
    {
        if (_terrainSceneViewMode == mode)
            return;

        _terrainSceneViewMode = mode;
        RefreshTerrainScenePresentation();
    }

    private void RefreshTerrainScenePresentation()
    {
        _sceneFitGeometry = null;
        _sceneFitFocus = null;
        _screenTerrainFaces.Clear();
        _screenTerrainSurfaceLabels.Clear();

        if (_viewMode == ViewportViewMode.Fly3D &&
            _flyCameraIsOverview &&
            Geometry is { Polygons.Count: > 0 } geometry)
        {
            FitFlyCameraToScene(GetSceneFitFocus(geometry), EffectiveFlyFitBounds());
        }

        InvalidateVisual();
    }

    internal void NotifyTerrainPresentationDataChanged()
    {
        // Edited/removed/add-clone state and vertex positions participate in
        // both Playable visibility and Fit bounds. Mutation callers use this
        // instead of a draw-only invalidation so cached Map/Fly Fit data never
        // describes the previous edit state.
        _sceneFitGeometry = null;
        _sceneFitFocus = null;
        _screenTerrainFaces.Clear();
        _screenTerrainSurfaceLabels.Clear();
        InvalidateNativeTerrainBoundedFrame();
        InvalidateVisual();
    }

    private bool ShouldPresentTerrain(GeometryCandidate geometry, TerrainPolygon polygon)
    {
        if (_terrainSceneViewMode == TerrainSceneViewMode.CompleteScene ||
            _playableTerrainViewPredicate == null)
        {
            return true;
        }

        // Never make the active target or a staged edit disappear. Complete
        // Scene remains available for intentionally editing native support or
        // background faces; active and edited faces survive the return trip.
        return IsPinnedTerrainPresentationFace(geometry, polygon) ||
            polygon.IsTerrainEdited ||
            polygon.IsTerrainRemoved ||
            polygon.IsTerrainAddClone ||
            _playableTerrainViewPredicate(geometry, polygon);
    }

    private bool IsAuditedBroadOverviewUnderlay(GeometryCandidate geometry, TerrainPolygon polygon) =>
        _playableTerrainViewPredicate != null &&
        !_playableTerrainViewPredicate(geometry, polygon);

    private bool IsPinnedTerrainPresentationFace(GeometryCandidate geometry, TerrainPolygon polygon)
    {
        return IsPinned(_selectedTerrainIndex) ||
            IsPinned(_hoverTerrainIndex) ||
            IsPinned(_terrainBrushPreviewTerrainIndex);

        bool IsPinned(int index) =>
            index >= 0 &&
            index < geometry.Polygons.Count &&
            ReferenceEquals(geometry.Polygons[index], polygon);
    }

    private void SetTerrainPresentationPin(ref int field, int nextIndex)
    {
        if (field == nextIndex)
            return;

        if (IsBaseHiddenTerrainIndex(field) || IsBaseHiddenTerrainIndex(nextIndex))
        {
            _sceneFitGeometry = null;
            _sceneFitFocus = null;
        }
        field = nextIndex;
    }

    private bool IsBaseHiddenTerrainIndex(int index)
    {
        GeometryCandidate? geometry = Geometry;
        if (_terrainSceneViewMode != TerrainSceneViewMode.Playable ||
            _playableTerrainViewPredicate == null ||
            geometry == null ||
            index < 0 ||
            index >= geometry.Polygons.Count)
        {
            return false;
        }

        TerrainPolygon polygon = geometry.Polygons[index];
        return !polygon.IsTerrainEdited &&
            !polygon.IsTerrainRemoved &&
            !polygon.IsTerrainAddClone &&
            !_playableTerrainViewPredicate(geometry, polygon);
    }

    internal TerrainSceneViewSnapshot CaptureTerrainSceneViewSnapshot()
    {
        GeometryCandidate? geometry = Geometry;
        if (geometry == null)
            return new TerrainSceneViewSnapshot(_terrainSceneViewMode, 0, 0, 0, false);

        int presented = geometry.Polygons.Count(polygon => ShouldPresentTerrain(geometry, polygon));
        return new TerrainSceneViewSnapshot(
            _terrainSceneViewMode,
            geometry.Polygons.Count,
            presented,
            Math.Max(0, geometry.Polygons.Count - presented),
            _playableTerrainViewPredicate != null);
    }

    internal bool IsTerrainPresentedForEditing(TerrainPolygon polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        GeometryCandidate? geometry = Geometry;
        if (geometry == null)
            return false;

        // Game Camera draws and edits the camera-visible native HP/LP terrain.
        // Edit Map keeps the complete source scene available for selection.
        return _viewMode == ViewportViewMode.Fly3D && UsesFlyGameViewHighDetailVisibility(geometry) ||
            ShouldPresentTerrain(geometry, polygon);
    }

    public void RefreshFlyCameraForLoadedLevel()
    {
        if (_viewMode != ViewportViewMode.Fly3D)
            return;

        ResetFlyCamera(preferGameViewStart: _flipMapY);
        InvalidateVisual();
    }

    public void ResetSelection()
    {
        bool hadPinnedTerrain =
            _selectedTerrainIndex >= 0 ||
            _hoverTerrainIndex >= 0 ||
            _terrainBrushPreviewTerrainIndex >= 0;
        _isDraggingMobyFacing = false;
        _draggingMobyFacingIndex = -1;
        _screenFacingGuide = null;
        SetTerrainPresentationPin(ref _selectedTerrainIndex, -1);
        _selectedTerrainPointIndex = -1;
        _selectedMobyIndex = -1;
        _hoverTerrainIndex = -1;
        _terrainBrushPreviewWorldPoint = null;
        _terrainBrushPreviewTerrainIndex = -1;
        if (hadPinnedTerrain)
        {
            // Active editing pins can force an off-camera face into context.
            // Once cleared, discard Fit data that may still include it.
            _sceneFitGeometry = null;
            _sceneFitFocus = null;
        }
        SetViewportCursor(null);
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.None);
        InvalidateVisual();
    }

    public void ToggleMapYFlip()
    {
        SetMapYFlipped(!_flipMapY);
    }

    public void SetMapYFlipped(bool flipped)
    {
        if (_flipMapY == flipped)
            return;

        _flipMapY = flipped;
        if (_viewMode == ViewportViewMode.Fly3D)
            ResetFlyCamera(preferGameViewStart: flipped);
        else
            _flyGameViewCameraLocal = false;
        _screenTerrainFaces.Clear();
        _screenMobys.Clear();
        InvalidateVisual();
    }

    internal ViewportFitSnapshot CaptureFitSnapshotForTesting()
    {
        GeometryCandidate geometry = Geometry
            ?? throw new InvalidOperationException("A geometry candidate is required to inspect viewport fitting.");
        Rect viewportBounds = EffectiveFlyFitBounds();
        SceneFitFocus focus = GetSceneFitFocus(geometry);
        List<Point> flyPoints = new(focus.Points.Count);
        foreach (Vector3f source in focus.Points)
        {
            if (TryProjectFly(viewportBounds, source.X, source.Y, source.Z, out ProjectedPoint projected))
                flyPoints.Add(projected.Screen);
        }

        SceneTransform mapTransform = CreateGeometryTransform(viewportBounds, geometry);
        List<Point> mapPoints = focus.Points
            .Select(source => mapTransform.Project(source.X, source.Y, source.Z))
            .ToList();
        return new ViewportFitSnapshot(
            viewportBounds,
            new Rect(
                geometry.Bounds.Left,
                geometry.Bounds.Top,
                geometry.Bounds.Width,
                geometry.Bounds.Height),
            new Rect(
                focus.Bounds.Left,
                focus.Bounds.Top,
                focus.Bounds.Width,
                focus.Bounds.Height),
            ScreenBounds(flyPoints),
            ScreenBounds(mapPoints),
            _flyCamera.Yaw,
            _flyCamera.Pitch,
            focus.Points.Count,
            flyPoints.Count,
            focus.SourcePolygonCount,
            focus.FocusPolygonCount);
    }

    internal void SetNativeTerrainDepthCueForTesting(bool enabled)
    {
        _useNativeTerrainDepthCue = enabled;
        InvalidateVisual();
    }

    internal NativeTerrainMapMaterialSnapshot CaptureNativeTerrainMapMaterialSnapshotForTesting() =>
        _nativeTerrainMapMaterialSnapshot;

    internal FlyTerrainInteractiveLodSnapshot CaptureFlyTerrainInteractiveLodSnapshotForTesting() =>
        _flyTerrainInteractiveLodSnapshot;

    internal FlyTerrainVisibilitySnapshot CaptureFlyTerrainVisibilitySnapshotForTesting() =>
        _flyTerrainVisibilitySnapshot;

    internal NativeTerrainOcclusionSnapshot CaptureNativeTerrainOcclusionSnapshotForTesting() =>
        _nativeTerrainOcclusionSnapshot;

    internal IReadOnlyList<int> CaptureVisibleHighDetailTerrainFaceIndexesForTesting() =>
        _screenTerrainFaces.Select(face => face.Index).Distinct().ToArray();

    internal bool WasHighDetailTerrainFaceNativeTexturedForTesting(int terrainIndex) =>
        _flyHighDetailRenderKinds.TryGetValue(terrainIndex, out NativeTerrainFaceRenderKind kind) &&
        kind == NativeTerrainFaceRenderKind.NativeTexture;

    internal IReadOnlyList<int> CaptureFlyCameraInactiveBackdropFaceIndexesForTesting() =>
        _flyCameraInactiveBackdropFaceIndexes.OrderBy(index => index).ToArray();

    internal IReadOnlyList<string> CaptureVisibleLowDetailTerrainFaceRuntimeKeysForTesting() =>
        _flyVisibleLowDetailCornerColorCounts.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

    internal int CaptureVisibleLowDetailTerrainCornerColorCountForTesting(string runtimeKey) =>
        _flyVisibleLowDetailCornerColorCounts.TryGetValue(runtimeKey, out int count) ? count : 0;

    internal GameCameraEntrySnapshot CaptureGameCameraEntrySnapshotForTesting() =>
        _gameCameraEntrySnapshot;

    internal void SetNativeTerrainLodPreviewModeForTesting(NativeTerrainLodPreviewMode mode)
    {
        _nativeTerrainLodPreviewMode = mode;
        InvalidateVisual();
    }

    internal void SimulateFlyNavigationForTesting()
    {
        if (_viewMode != ViewportViewMode.Fly3D ||
            _nativeTerrainLodPreviewMode != NativeTerrainLodPreviewMode.EditorOverviewHighDetail)
        {
            throw new InvalidOperationException(
                $"Interactive Fly navigation requires the high-detail Fly overview; current state is {_viewMode}/{_nativeTerrainLodPreviewMode}.");
        }

        _holdFlyNavigationMaterialLodForTesting = true;
        if (_flipMapY && !_flyCameraIsOverview)
            _flyGameViewCameraLocal = true;
        _flyNavigationSettleTimer.Stop();
        _flyNavigationInteractiveMaterialLod = true;
        InvalidateVisual();
    }

    internal void SettleFlyNavigationForTesting()
    {
        _holdFlyNavigationMaterialLodForTesting = false;
        SettleFlyNavigationMaterialLod(force: true);
    }

    internal void SimulateMissedFlyNavigationReleaseForTesting()
    {
        if (_viewMode != ViewportViewMode.Fly3D)
            throw new InvalidOperationException("Missed-release simulation requires Fly 3D.");

        _holdFlyNavigationMaterialLodForTesting = false;
        _activeFlyNavigationKeys.Add(Key.W);
        _flyNavigationInteractiveMaterialLod = true;
        SettleFlyNavigationMaterialLod(force: false);
        _activeFlyNavigationKeys.Clear();
    }

    internal void EnableNativeTerrainDistanceForTesting()
    {
        SettleFlyNavigationMaterialLod(force: true);
        if (_nativeTerrainLodPreviewMode == NativeTerrainLodPreviewMode.NativeDistance)
            return;

        _nativeTerrainLodPreviewMode = NativeTerrainLodPreviewMode.NativeDistance;
        InvalidateVisual();
    }

    internal void AimFlyCameraAtTerrainForTesting(int terrainIndex, double distance = 1400)
    {
        GeometryCandidate geometry = Geometry
            ?? throw new InvalidOperationException("A geometry candidate is required to aim the Fly 3D camera.");
        if (terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count)
            throw new ArgumentOutOfRangeException(nameof(terrainIndex));

        TerrainPolygon polygon = geometry.Polygons[terrainIndex];
        const double yaw = -Math.PI * 0.5;
        const double pitch = -0.34;
        _viewMode = ViewportViewMode.Fly3D;
        _flyCamera = CreateOverviewCamera(
            ToFlyViewX(polygon.Center.X),
            ToFlyViewY(polygon.Center.Y),
            polygon.AvgZ,
            yaw,
            pitch,
            Math.Max(256, distance));
        _flyCameraIsOverview = false;
        _flyGameViewCameraLocal =
            _flipMapY && _nativeTerrainLodPreviewMode == NativeTerrainLodPreviewMode.EditorOverviewHighDetail;
        SetTerrainPresentationPin(ref _selectedTerrainIndex, -1);
        _selectedTerrainPointIndex = -1;
        _selectedMobyIndex = -1;
        InvalidateVisual();
    }

    internal NativeTerrainDepthCueSnapshot CaptureNativeTerrainDepthCueSnapshotForTesting()
    {
        GeometryCandidate geometry = Geometry
            ?? throw new InvalidOperationException("A geometry candidate is required to inspect native terrain depth cueing.");
        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            bounds = EffectiveFlyFitBounds();
        Rect visibleBounds = bounds.Inflate(220);

        int faceCount = 0;
        int cornerCount = 0;
        int distinctEndpointCorners = 0;
        int nearEndpointCorners = 0;
        int blendedCorners = 0;
        int farEndpointCorners = 0;
        int changedFromLegacyCorners = 0;
        long ir0Total = 0;
        long channelDeltaTotal = 0;
        double minCameraDepth = double.PositiveInfinity;
        double maxCameraDepth = double.NegativeInfinity;

        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (polygon.NearColors.Count < 4 || polygon.FarColors.Count < 4)
                continue;

            List<ProjectedPoint> projected = new();
            int pointCount = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
            for (int index = 0; index < pointCount; index++)
            {
                if (TryProjectFly(bounds, polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index], out ProjectedPoint point))
                    projected.Add(point);
            }

            if (projected.Count != pointCount || projected.Count < 3 ||
                !IntersectsBounds(projected.Select(point => point.Screen).ToArray(), visibleBounds) ||
                !TryBuildRawFaceSlotDepths(polygon, projected.Select(point => point.Depth).ToArray(), out double[] rawDepths))
            {
                continue;
            }

            faceCount++;
            for (int slot = 0; slot < 4; slot++)
            {
                Spyro.Editor.Core.Primitives.ColorRgba legacyNear = polygon.TextureVisualEdit?.Corners[slot].NearColor ?? polygon.NearColors[slot];
                Spyro.Editor.Core.Primitives.ColorRgba legacyFar = polygon.TextureVisualEdit?.Corners[slot].FarColor ?? polygon.FarColors[slot];
                int ir0 = NativeTerrainDepthCueIr0(rawDepths[slot]);
                Spyro.Editor.Core.Primitives.ColorRgba actual = NativeTerrainDepthCueColor(legacyNear, legacyFar, rawDepths[slot]);
                cornerCount++;
                ir0Total += ir0;
                minCameraDepth = Math.Min(minCameraDepth, rawDepths[slot]);
                maxCameraDepth = Math.Max(maxCameraDepth, rawDepths[slot]);
                if (legacyNear != legacyFar)
                    distinctEndpointCorners++;
                if (ir0 == 0x1000)
                    nearEndpointCorners++;
                else if (ir0 == 0)
                    farEndpointCorners++;
                else
                    blendedCorners++;
                if (actual != legacyNear)
                    changedFromLegacyCorners++;
                channelDeltaTotal +=
                    Math.Abs(actual.R - legacyNear.R) +
                    Math.Abs(actual.G - legacyNear.G) +
                    Math.Abs(actual.B - legacyNear.B);
            }
        }

        return new NativeTerrainDepthCueSnapshot(
            faceCount,
            cornerCount,
            distinctEndpointCorners,
            nearEndpointCorners,
            blendedCorners,
            farEndpointCorners,
            changedFromLegacyCorners,
            cornerCount == 0 ? 0 : ir0Total / (cornerCount * 4096.0),
            cornerCount == 0 ? 0 : channelDeltaTotal / (cornerCount * 3.0),
            cornerCount == 0 ? 0 : minCameraDepth,
            cornerCount == 0 ? 0 : maxCameraDepth);
    }

    internal NativeTerrainFarLodSnapshot CaptureNativeTerrainFarLodSnapshotForTesting()
    {
        GeometryCandidate geometry = Geometry
            ?? throw new InvalidOperationException("A geometry candidate is required to inspect native terrain far LOD.");
        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            bounds = EffectiveFlyFitBounds();
        Rect visibleBounds = bounds.Inflate(220);
        Dictionary<int, SceneSectorRenderMetadata> sectors = geometry.SourceSectors
            .GroupBy(sector => sector.SectorIndex)
            .ToDictionary(group => group.Key, group => group.First());
        bool useNativeDistanceLod = UsesNativeTerrainDistanceLod(geometry);

        int queuedHighDetailSectors = useNativeDistanceLod
            ? sectors.Values.Count(sector =>
                NativeTerrainFarLod.ShouldQueueHighDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    sector.DisableHighDetail))
            : sectors.Count;
        int queuedLowDetailSectors = useNativeDistanceLod
            ? sectors.Values.Count(sector =>
                NativeTerrainFarLod.ShouldQueueLowDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    sector.DisableLowDetail,
                    sector.ForceLowDetail))
            : 0;

        int visibleHighDetailFaces = 0;
        int suppressedHighDetailFaces = 0;
        int visibleHighDetailAtOrBeyondCutoff = 0;
        int hqOverlayEligibleFaces = 0;
        int descriptorZeroFaces = 0;
        int descriptorOneFaces = 0;
        int minimumPaletteRow = int.MaxValue;
        int maximumPaletteRow = int.MinValue;
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (useNativeDistanceLod &&
                sectors.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector) &&
                !NativeTerrainFarLod.ShouldQueueHighDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    sector.DisableHighDetail))
            {
                suppressedHighDetailFaces++;
                continue;
            }

            List<ProjectedPoint> projected = [];
            int pointCount = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
            for (int index = 0; index < pointCount; index++)
            {
                if (TryProjectFly(bounds, polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index], out ProjectedPoint point))
                    projected.Add(point);
            }
            if (projected.Count != pointCount || pointCount < 3 ||
                !TryBuildRawFaceSlotDepths(polygon, projected.Select(point => point.Depth).ToArray(), out double[] rawDepths))
            {
                continue;
            }

            NativeTerrainLqPreviewSelection selection = NativeTerrainFarLod.SelectHighDetailTexturePath(
                rawDepths,
                polygon.LqFadeBypass,
                polygon.HqOverlayBypass);
            if (useNativeDistanceLod && !selection.HighPolyVisible)
            {
                suppressedHighDetailFaces++;
                continue;
            }
            if (!IntersectsBounds(projected.Select(point => point.Screen).ToArray(), visibleBounds))
                continue;

            visibleHighDetailFaces++;
            if (!NativeTerrainFarLod.ShouldRenderHighDetailFace(rawDepths))
                visibleHighDetailAtOrBeyondCutoff++;
            if (useNativeDistanceLod && selection.HqOverlayEligible)
                hqOverlayEligibleFaces++;
            if (useNativeDistanceLod)
            {
                if (selection.DescriptorIndex == 0)
                    descriptorZeroFaces++;
                else
                    descriptorOneFaces++;
                minimumPaletteRow = Math.Min(minimumPaletteRow, selection.PaletteRow);
                maximumPaletteRow = Math.Max(maximumPaletteRow, selection.PaletteRow);
            }
        }

        IReadOnlyList<ProjectedLowDetailTerrainFace> lowDetailFaces = useNativeDistanceLod
            ? BuildProjectedLowDetailTerrainFaces(
                geometry,
                sectors,
                bounds,
                visibleBounds,
                forcedHighDetailSectors: new HashSet<int>(),
                cullingDistanceFixed16: NativeTerrainFarLod.ConservativeAllSectorCullingDistanceFixed16)
            : Array.Empty<ProjectedLowDetailTerrainFace>();
        int visibleLowDetailOnlyFaces = lowDetailFaces.Count(face =>
            sectors.TryGetValue(face.Polygon.SectorIndex, out SceneSectorRenderMetadata? sector) && sector.DisableHighDetail);
        int visibleForcedLowDetailFaces = lowDetailFaces.Count(face =>
            sectors.TryGetValue(face.Polygon.SectorIndex, out SceneSectorRenderMetadata? sector) && sector.ForceLowDetail);
        int distinctLowDetailCornerColors = lowDetailFaces
            .SelectMany(face => face.Polygon.CornerColors)
            .Distinct()
            .Count();

        return new NativeTerrainFarLodSnapshot(
            _nativeTerrainLodPreviewMode,
            geometry.HasStaticLowDetailPreview,
            geometry.TerrainLodPreviewContract,
            sectors.Count,
            geometry.Polygons.Count,
            geometry.LowDetailPolygons.Count,
            queuedHighDetailSectors,
            queuedLowDetailSectors,
            visibleHighDetailFaces,
            lowDetailFaces.Count,
            suppressedHighDetailFaces,
            visibleHighDetailAtOrBeyondCutoff,
            visibleLowDetailOnlyFaces,
            visibleForcedLowDetailFaces,
            distinctLowDetailCornerColors,
            hqOverlayEligibleFaces,
            descriptorZeroFaces,
            descriptorOneFaces,
            minimumPaletteRow == int.MaxValue ? -1 : minimumPaletteRow,
            maximumPaletteRow == int.MinValue ? -1 : maximumPaletteRow);
    }

    private static Rect ScreenBounds(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
            return default;
        double minX = points.Min(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxX = points.Max(point => point.X);
        double maxY = points.Max(point => point.Y);
        return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    public void FocusMobys(IReadOnlyList<Moby> mobys)
    {
        List<Moby> visible = mobys.Where(moby => !moby.IsRemoved).ToList();
        if (_mobyFilter != null)
            visible = visible.Where(_mobyFilter).ToList();
        if (visible.Count == 0)
            return;

        if (visible.Count == 1)
        {
            SelectMoby(visible[0], true);
            return;
        }

        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // A terrain face revealed only because it was selected must stop
        // influencing Playable Fit before the Moby-group transform is built.
        SetTerrainPresentationPin(ref _selectedTerrainIndex, -1);
        _selectedTerrainPointIndex = -1;

        double minX = visible.Min(moby => moby.Position.X);
        double maxX = visible.Max(moby => moby.Position.X);
        double minY = visible.Min(moby => moby.Position.Y);
        double maxY = visible.Max(moby => moby.Position.Y);
        double groupWidth = Math.Max(128, maxX - minX);
        double groupHeight = Math.Max(128, maxY - minY);

        GeometryCandidate? geometry = Geometry;
        if (geometry != null && geometry.Polygons.Count > 0)
        {
            SceneFitFocus focus = GetSceneFitFocus(geometry);
            double baseScaleX = bounds.Width / Math.Max(1, focus.Bounds.Width);
            double baseScaleY = bounds.Height / Math.Max(1, focus.Bounds.Height);
            double baseScale = Math.Min(baseScaleX, baseScaleY) * 0.82;
            double desiredScale = Math.Min(bounds.Width / groupWidth, bounds.Height / groupHeight) * 0.42;
            _zoom = Math.Clamp(desiredScale / Math.Max(0.0001, baseScale), 1, 6);
        }
        else
        {
            _zoom = Math.Clamp(Math.Min(bounds.Width / groupWidth, bounds.Height / groupHeight) * 0.42, 1, 6);
        }

        _pan = default;
        SceneTransform transform = CreateMobyTransform(bounds, Mobys);
        Point groupCenter = transform.Project((minX + maxX) * 0.5, (minY + maxY) * 0.5, visible.Average(moby => moby.Position.Z));
        _pan += bounds.Center - groupCenter;
        _selectedMobyIndex = -1;
        for (int i = 0; i < Mobys.Count; i++)
        {
            if (ReferenceEquals(Mobys[i], visible[0]))
            {
                _selectedMobyIndex = i;
                break;
            }
        }
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.ForMoby(visible[0]));
        InvalidateVisual();
    }

    public void FocusTerrain(int terrainIndex)
    {
        GeometryCandidate? geometry = Geometry;
        if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count)
            return;

        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        TerrainPolygon polygon = geometry.Polygons[terrainIndex];
        if (polygon.Points.Count == 0)
            return;

        _viewMode = ViewportViewMode.Map;
        _selectedMobyIndex = -1;
        _selectedTerrainPointIndex = -1;
        // Pin first: a base-hidden face changes the Playable Fit bounds, and
        // the centering transform must be calculated from that same view.
        SetTerrainPresentationPin(ref _selectedTerrainIndex, terrainIndex);
        _zoom = Math.Clamp(_zoom, 1.4, 5.5);
        _pan = default;
        SceneTransform transform = CreateGeometryTransform(bounds, geometry);
        Point point = transform.Project(polygon.Center.X, polygon.Center.Y, polygon.AvgZ);
        _pan += bounds.Center - point;
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, polygon));
        InvalidateVisual();
    }

    public void SetSelectedTerrainPoint(int terrainPointIndex)
    {
        int nextIndex = terrainPointIndex;
        if (Geometry == null || _selectedTerrainIndex < 0 || _selectedTerrainIndex >= Geometry.Polygons.Count)
            nextIndex = -1;
        else if (terrainPointIndex >= 0)
            nextIndex = NormalizeTerrainPointIndex(Geometry.Polygons[_selectedTerrainIndex], terrainPointIndex);

        if (_selectedTerrainPointIndex == nextIndex)
            return;

        _selectedTerrainPointIndex = nextIndex;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        _screenTerrainFaces.Clear();
        _screenMobys.Clear();
        _screenTerrainSurfaceLabels.Clear();
        _flyHighDetailRenderKinds.Clear();
        _flyCameraInactiveBackdropFaceIndexes.Clear();
        _flyVisibleLowDetailCornerColorCounts.Clear();
        _screenFacingGuide = null;
        ClearNativeMovementScreenState();

        context.FillRectangle(new SolidColorBrush(Color.FromRgb(19, 24, 30)), bounds);
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            DrawFlyScene(context, bounds);
            DrawViewportChrome(context, bounds);
            return;
        }

        DrawGrid(context, bounds);
        GeometryCandidate? geometry = Geometry;
        if (geometry != null && geometry.Polygons.Count > 0)
            DrawGeometry(context, bounds, geometry);
        else
            DrawEmptyScene(context, bounds, _emptyMessage);

        DrawMobys(context, bounds);
        DrawNativeMovementOverlays(context, bounds);
        DrawViewportChrome(context, bounds);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _lastPointerPosition = e.GetPosition(this);
        PointerPoint point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            if (_viewMode == ViewportViewMode.Map && point.Properties.IsRightButtonPressed && !ShouldPrioritizeTerrainInteraction())
            {
                ScreenMoby? moby = FindScreenMoby(_lastPointerPosition);
                if (moby != null && Mobys[moby.Index].HasAnyEdit)
                {
                    MobyUndoRequested?.Invoke(this, Mobys[moby.Index]);
                    e.Handled = true;
                    return;
                }
            }

            _isPanning = true;
            SetViewportCursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            if (_objectPlacementMode)
            {
                ObjectPlacementRequested?.Invoke(this, new ViewportObjectPlacementRequestedEventArgs(_lastPointerPosition));
                e.Handled = true;
                return;
            }

            if (_terrainTexturePaintMode)
            {
                if (!_terrainTexturePaintBusy)
                {
                    ScreenTerrainFace? terrain = FindScreenTerrain(_lastPointerPosition);
                    if (terrain != null && Geometry != null)
                    {
                        SelectTerrain(terrain);
                        TerrainTexturePaintRequested?.Invoke(
                            this,
                            new ViewportTerrainTexturePaintRequestedEventArgs(
                                terrain.Index,
                                Geometry.Polygons[terrain.Index]));
                    }
                }

                e.Handled = true;
                return;
            }

            bool terrainFirst = ShouldPrioritizeTerrainInteraction();
            if (!terrainFirst && TryBeginNativeMovementHandleDrag(_lastPointerPosition, e.Pointer))
            {
                e.Handled = true;
                return;
            }
            ScreenFacingGuide? facingGuide = terrainFirst ? null : FindScreenFacingGuide(_lastPointerPosition);
            if (facingGuide != null)
            {
                SelectMoby(facingGuide.Index, false);
                _isDraggingMobyFacing = true;
                _draggingMobyFacingIndex = facingGuide.Index;
                SetViewportCursor(StandardCursorType.Cross);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            ScreenMoby? moby = terrainFirst ? null : FindScreenMoby(_lastPointerPosition);
            if (e.ClickCount >= 2)
            {
                if (terrainFirst && TryOpenTerrainEditAt(_lastPointerPosition))
                {
                    e.Handled = true;
                    return;
                }

                moby ??= FindScreenMoby(_lastPointerPosition);
                if (moby != null && !terrainFirst)
                {
                    SelectMoby(moby.Index, false);
                    MobyEditRequested?.Invoke(this, Mobys[moby.Index]);
                    e.Handled = true;
                    return;
                }

                if (terrainFirst && TryOpenTerrainEditAt(_lastPointerPosition))
                {
                    e.Handled = true;
                    return;
                }

                if (moby != null)
                {
                    SelectMoby(moby.Index, false);
                    MobyEditRequested?.Invoke(this, Mobys[moby.Index]);
                    e.Handled = true;
                    return;
                }
            }

            if (_terrainBrushAction != TerrainBrushAction.Off)
            {
                ScreenTerrainFace? terrain = FindScreenTerrain(_lastPointerPosition);
                if (terrain != null && Geometry != null && TryGetBrushWorldPoint(_lastPointerPosition, terrain.Index, out Vector2f brushCenter))
                {
                    SelectTerrain(terrain);
                    _isPaintingTerrain = true;
                    _lastTerrainBrushWorldPoint = brushCenter;
                    _terrainBrushPreviewWorldPoint = brushCenter;
                    SetTerrainPresentationPin(ref _terrainBrushPreviewTerrainIndex, terrain.Index);
                    TerrainBrushRequested?.Invoke(this, new ViewportTerrainBrushRequestedEventArgs(terrain.Index, Geometry.Polygons[terrain.Index], brushCenter, _terrainBrushAction, true, 1.0));
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }

            if (terrainFirst && TryBeginTerrainDragAt(_lastPointerPosition, e.KeyModifiers, e.Pointer))
            {
                e.Handled = true;
                return;
            }

            moby ??= FindScreenMoby(_lastPointerPosition);
            if (moby != null)
            {
                SelectMoby(moby.Index, false);
                _isDraggingMoby = true;
                _draggingMobyIndex = moby.Index;
                SetViewportCursor(StandardCursorType.SizeAll);
                e.Pointer.Capture(this);
            }
            else
            {
                if (terrainFirst && TryBeginTerrainDragAt(_lastPointerPosition, e.KeyModifiers, e.Pointer))
                {
                    e.Handled = true;
                    return;
                }

                SelectAt(_lastPointerPosition);
            }

            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        UpdateTerrainBrushPreview(position);
        UpdateTerrainHover(position);
        UpdateViewportCursor(position);
        if (_isPanning)
        {
            if (_viewMode == ViewportViewMode.Fly3D)
            {
                Vector delta = position - _lastPointerPosition;
                if (Math.Abs(delta.X) > double.Epsilon || Math.Abs(delta.Y) > double.Epsilon)
                    ResumeNativeTerrainLodAfterFlyNavigation();
                _flyCamera.Yaw += delta.X * 0.0065;
                _flyCamera.Pitch = Math.Clamp(_flyCamera.Pitch - (delta.Y * 0.0048), -1.15, 0.45);
            }
            else
            {
                _pan += position - _lastPointerPosition;
            }
            _lastPointerPosition = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (TryDragNativeMovementHandle(position))
        {
            _lastPointerPosition = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingMobyFacing && _draggingMobyFacingIndex >= 0 && _draggingMobyFacingIndex < Mobys.Count)
        {
            Moby moby = Mobys[_draggingMobyFacingIndex];
            if (!moby.IsRemoved && TryGetMobyYawByteAtScreenPoint(moby, position, out int yawByte) && moby.YawByte != yawByte)
                MobyRotateRequested?.Invoke(this, new MobyRotateRequestedEventArgs(moby, yawByte));

            _lastPointerPosition = position;
            e.Handled = true;
            return;
        }

        if (_isPaintingTerrain && Geometry != null)
        {
            ScreenTerrainFace? terrain = FindScreenTerrain(position);
            if (terrain != null && TryGetBrushWorldPoint(position, terrain.Index, out Vector2f brushCenter))
            {
                _terrainBrushPreviewWorldPoint = brushCenter;
                SetTerrainPresentationPin(ref _terrainBrushPreviewTerrainIndex, terrain.Index);
                if (TryBuildTerrainBrushPath(brushCenter, out IReadOnlyList<TerrainBrushPathSample> brushSamples))
                {
                    SelectTerrain(terrain);
                    foreach (TerrainBrushPathSample sample in brushSamples)
                        TerrainBrushRequested?.Invoke(this, new ViewportTerrainBrushRequestedEventArgs(terrain.Index, Geometry.Polygons[terrain.Index], sample.Center, _terrainBrushAction, false, sample.StrengthScale));
                }
            }

            _lastPointerPosition = position;
            e.Handled = true;
            return;
        }

        if (_isDraggingTerrainFace && Geometry != null && _draggingTerrainFaceIndex >= 0 && _draggingTerrainFaceIndex < Geometry.Polygons.Count)
        {
            TerrainPolygon terrain = Geometry.Polygons[_draggingTerrainFaceIndex];
            bool verticalDrag = IsTerrainVerticalDrag(e.KeyModifiers);
            if (verticalDrag)
            {
                float dz = TerrainVerticalDragDelta(_lastPointerPosition, position);
                if (Math.Abs(dz) > 0.001f)
                    TerrainMoveRequested?.Invoke(this, new ViewportTerrainMoveRequestedEventArgs(_draggingTerrainFaceIndex, terrain, 0, 0, dz, _draggingTerrainAsAddCopy));
            }
            else
            {
                if (!TryGetTerrainDragDelta(terrain.AvgZ, _lastPointerPosition, position, out float dx, out float dy))
                {
                    _lastPointerPosition = position;
                    e.Handled = true;
                    return;
                }

                if (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f)
                    TerrainMoveRequested?.Invoke(this, new ViewportTerrainMoveRequestedEventArgs(_draggingTerrainFaceIndex, terrain, dx, dy, 0, _draggingTerrainAsAddCopy));
            }

            _lastPointerPosition = position;
            e.Handled = true;
            return;
        }

        if (_isDraggingTerrainPoint && Geometry != null && _draggingTerrainIndex >= 0 && _draggingTerrainIndex < Geometry.Polygons.Count)
        {
            TerrainPolygon terrain = Geometry.Polygons[_draggingTerrainIndex];
            int pointIndex = NormalizeTerrainPointIndex(terrain, _draggingTerrainPointIndex);
            if (pointIndex < 0)
            {
                _lastPointerPosition = position;
                e.Handled = true;
                return;
            }

            bool verticalDrag = IsTerrainVerticalDrag(e.KeyModifiers);
            if (verticalDrag)
            {
                float dz = TerrainVerticalDragDelta(_lastPointerPosition, position);
                if (Math.Abs(dz) > 0.001f)
                    TerrainPointMoveRequested?.Invoke(this, new ViewportTerrainPointMoveRequestedEventArgs(_draggingTerrainIndex, pointIndex, terrain, 0, 0, dz));
            }
            else
            {
                if (!TryGetTerrainDragDelta(terrain.ZValues[pointIndex], _lastPointerPosition, position, out float dx, out float dy))
                {
                    _lastPointerPosition = position;
                    e.Handled = true;
                    return;
                }

                if (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f)
                    TerrainPointMoveRequested?.Invoke(this, new ViewportTerrainPointMoveRequestedEventArgs(_draggingTerrainIndex, pointIndex, terrain, dx, dy, 0));
            }

            _lastPointerPosition = position;
            e.Handled = true;
            return;
        }

        if (_isDraggingMoby && _draggingMobyIndex >= 0 && _draggingMobyIndex < Mobys.Count)
        {
            Moby moby = Mobys[_draggingMobyIndex];
            if (!moby.IsRemoved)
            {
                float dx;
                float dy;
                if (_viewMode == ViewportViewMode.Fly3D)
                {
                    if (!TryUnprojectFlyToZ(new Rect(Bounds.Size), _lastPointerPosition, moby.Position.Z, out Point previousWorld) ||
                        !TryUnprojectFlyToZ(new Rect(Bounds.Size), position, moby.Position.Z, out Point currentWorld))
                    {
                        _lastPointerPosition = position;
                        e.Handled = true;
                        return;
                    }

                    dx = (float)(currentWorld.X - previousWorld.X);
                    dy = (float)(currentWorld.Y - previousWorld.Y);
                }
                else
                {
                    SceneTransform transform = CreateMobyTransform(new Rect(Bounds.Size), Mobys);
                    Point previousWorld = transform.Unproject(_lastPointerPosition, moby.Position.Z);
                    Point currentWorld = transform.Unproject(position, moby.Position.Z);
                    dx = (float)(currentWorld.X - previousWorld.X);
                    dy = (float)(currentWorld.Y - previousWorld.Y);
                }

                if (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f)
                    MobyMoveRequested?.Invoke(this, new MobyMoveRequestedEventArgs(moby, dx, dy, 0));
            }

            _lastPointerPosition = position;
            e.Handled = true;
        }

        _lastPointerPosition = position;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHoverTerrain(-1);
        ClearTerrainBrushPreview();
        SetViewportCursor(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        bool completedFlyLook = _viewMode == ViewportViewMode.Fly3D && _isPanning;
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (_isDraggingMoby)
        {
            _isDraggingMoby = false;
            _draggingMobyIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (EndNativeMovementHandleDrag(e.Pointer))
            e.Handled = true;

        if (_isDraggingMobyFacing)
        {
            _isDraggingMobyFacing = false;
            _draggingMobyFacingIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (_isDraggingTerrainFace)
        {
            _isDraggingTerrainFace = false;
            _draggingTerrainAsAddCopy = false;
            _draggingTerrainFaceIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (_isDraggingTerrainPoint)
        {
            _isDraggingTerrainPoint = false;
            _draggingTerrainIndex = -1;
            _draggingTerrainPointIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (_isPaintingTerrain)
        {
            TryFlushTerrainBrushResidual();
            _isPaintingTerrain = false;
            _lastTerrainBrushWorldPoint = null;
            TerrainBrushStrokeFinished?.Invoke(this, EventArgs.Empty);
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        if (completedFlyLook)
            ScheduleFlyNavigationMaterialSettle();

        UpdateViewportCursor(e.GetPosition(this));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_terrainBrushAction != TerrainBrushAction.Off && TryRequestTerrainBrushAdjustment(e))
            return;

        if (_viewMode == ViewportViewMode.Fly3D)
        {
            ResumeNativeTerrainLodAfterFlyNavigation();
            MoveFlyCamera(e.Delta.Y > 0 ? 420 : -420, 0, 0);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        double oldZoom = _zoom;
        double factor = e.Delta.Y > 0 ? 1.12 : 0.89;
        _zoom = Math.Clamp(_zoom * factor, 0.35, 6.0);

        Point pointer = e.GetPosition(this);
        Vector fromCenter = pointer - new Point(Bounds.Width * 0.5, Bounds.Height * 0.5);
        if (oldZoom > 0)
            _pan = (_pan - fromCenter) * (_zoom / oldZoom) + fromCenter;

        InvalidateVisual();
        e.Handled = true;
    }

    private bool TryRequestTerrainBrushAdjustment(PointerWheelEventArgs e)
    {
        TerrainBrushAdjustmentKind? kind = null;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
            kind = TerrainBrushAdjustmentKind.Size;
        else if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
            kind = TerrainBrushAdjustmentKind.Strength;
        else if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            kind = TerrainBrushAdjustmentKind.Feather;

        if (kind == null)
            return false;

        int direction = e.Delta.Y >= 0 ? 1 : -1;
        TerrainBrushAdjustmentRequested?.Invoke(this, new ViewportTerrainBrushAdjustmentRequestedEventArgs(kind.Value, direction));
        InvalidateVisual();
        e.Handled = true;
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_objectPlacementMode && e.Key == Key.Escape)
        {
            _objectPlacementMode = false;
            ObjectPlacementCanceled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (_terrainTexturePaintMode && e.Key == Key.Escape)
        {
            TerrainTexturePaintCanceled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (TryRequestTerrainBrushMode(e))
            return;
        if (TryRequestTerrainRemoval(e))
            return;
        if (TryRequestObjectCopyPaste(e))
            return;
        if (TryRequestTerrainLookShortcut(e))
            return;

        if (_viewMode != ViewportViewMode.Fly3D)
            return;

        const double turnStep = 0.08;
        bool handled = true;
        switch (e.Key)
        {
            case Key.W:
                MoveFlyCamera(FlyPlanarMoveStep, 0, 0);
                break;
            case Key.S:
                MoveFlyCamera(-FlyPlanarMoveStep, 0, 0);
                break;
            case Key.A:
                MoveFlyCamera(0, -FlyPlanarMoveStep, 0);
                break;
            case Key.D:
                MoveFlyCamera(0, FlyPlanarMoveStep, 0);
                break;
            case Key.Q:
                MoveFlyCamera(0, 0, FlyVerticalMoveStep);
                break;
            case Key.E:
                MoveFlyCamera(0, 0, -FlyVerticalMoveStep);
                break;
            case Key.Left:
                _flyCamera.Yaw -= turnStep;
                break;
            case Key.Right:
                _flyCamera.Yaw += turnStep;
                break;
            case Key.Up:
                _flyCamera.Pitch = Math.Clamp(_flyCamera.Pitch + turnStep, -1.15, 0.45);
                break;
            case Key.Down:
                _flyCamera.Pitch = Math.Clamp(_flyCamera.Pitch - turnStep, -1.15, 0.45);
                break;
            default:
                handled = false;
                break;
        }

        if (!handled)
            return;

        _activeFlyNavigationKeys.Add(e.Key);
        ResumeNativeTerrainLodAfterFlyNavigation();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (!_activeFlyNavigationKeys.Remove(e.Key))
            return;

        if (_activeFlyNavigationKeys.Count == 0)
            ScheduleFlyNavigationMaterialSettle();
        e.Handled = true;
    }

    private void OnViewportLostFocus(object? sender, RoutedEventArgs e)
    {
        bool interruptedFlyNavigation = _activeFlyNavigationKeys.Count > 0 || _isPanning;
        _activeFlyNavigationKeys.Clear();
        _isPanning = false;
        if (interruptedFlyNavigation)
            ScheduleFlyNavigationMaterialSettle();
    }

    private bool TryRequestObjectCopyPaste(KeyEventArgs e)
    {
        if (!IsPlatformCopyPasteModifier(e.KeyModifiers))
            return false;

        if (e.Key == Key.C)
        {
            ObjectCopyRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.V)
        {
            ObjectPasteRequested?.Invoke(this, new ViewportObjectPasteRequestedEventArgs(_lastPointerPosition));
            e.Handled = true;
            return true;
        }

        return false;
    }

    private static bool IsPlatformCopyPasteModifier(KeyModifiers modifiers)
    {
        return (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;
    }

    private bool TryRequestTerrainBrushMode(KeyEventArgs e)
    {
        TerrainBrushAction? action = e.Key switch
        {
            Key.D1 or Key.NumPad1 => TerrainBrushAction.Raise,
            Key.D2 or Key.NumPad2 => TerrainBrushAction.Lower,
            Key.D3 or Key.NumPad3 => TerrainBrushAction.Blend,
            Key.D4 or Key.NumPad4 => TerrainBrushAction.Flatten,
            Key.D5 or Key.NumPad5 => TerrainBrushAction.Restore,
            Key.D0 or Key.NumPad0 or Key.Escape => TerrainBrushAction.Off,
            _ => null
        };
        if (action == null)
            return false;

        TerrainBrushModeRequested?.Invoke(this, new ViewportTerrainBrushModeRequestedEventArgs(action.Value));
        if (e.Key == Key.Escape && _selectedTerrainIndex >= 0)
            ResetSelection();

        e.Handled = true;
        return true;
    }

    private bool TryRequestTerrainRemoval(KeyEventArgs e)
    {
        if (e.Key is not (Key.Delete or Key.Back))
            return false;

        GeometryCandidate? geometry = Geometry;
        if (geometry == null || _selectedTerrainIndex < 0 || _selectedTerrainIndex >= geometry.Polygons.Count)
            return false;

        TerrainRemoveRequested?.Invoke(this, new ViewportTerrainRemoveRequestedEventArgs(_selectedTerrainIndex, geometry.Polygons[_selectedTerrainIndex]));
        e.Handled = true;
        return true;
    }

    private bool TryRequestTerrainLookShortcut(KeyEventArgs e)
    {
        if (e.Key is not (Key.C or Key.V))
            return false;

        GeometryCandidate? geometry = Geometry;
        if (geometry == null || _selectedTerrainIndex < 0 || _selectedTerrainIndex >= geometry.Polygons.Count)
            return false;

        TerrainPolygon terrain = geometry.Polygons[_selectedTerrainIndex];
        ViewportTerrainLookRequestedEventArgs args = new(_selectedTerrainIndex, terrain);
        if (e.Key == Key.C)
            TerrainLookCopyRequested?.Invoke(this, args);
        else
            TerrainLookPasteRequested?.Invoke(this, args);

        e.Handled = true;
        return true;
    }

    public void SelectMoby(Moby moby, bool focus)
    {
        for (int i = 0; i < Mobys.Count; i++)
        {
            if (!ReferenceEquals(Mobys[i], moby))
                continue;

            SelectMoby(i, focus);
            return;
        }
    }

    private void SelectMoby(int index, bool focus)
    {
        if (index < 0 || index >= Mobys.Count || Mobys[index].IsRemoved)
            return;

        _selectedMobyIndex = index;
        SetTerrainPresentationPin(ref _selectedTerrainIndex, -1);
        _selectedTerrainPointIndex = -1;
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.ForMoby(Mobys[index]));
        if (focus)
            CenterOnMoby(Mobys[index]);

        InvalidateVisual();
    }

    private void SelectAt(Point point)
    {
        if (ShouldPrioritizeTerrainInteraction())
        {
            ScreenTerrainPoint? priorityTerrainPoint = FindScreenTerrainPoint(point);
            if (priorityTerrainPoint != null)
            {
                SelectTerrainPoint(priorityTerrainPoint);
                return;
            }

            ScreenTerrainFace? priorityTerrain = FindScreenTerrain(point);
            if (priorityTerrain != null)
            {
                SelectTerrain(priorityTerrain);
                return;
            }
        }

        ScreenMoby? moby = FindScreenMoby(point);
        if (moby != null)
        {
            SelectMoby(moby.Index, false);
            return;
        }

        ResetSelection();
    }

    private bool TryOpenTerrainEditAt(Point point)
    {
        if (Geometry == null || !ShouldPrioritizeTerrainInteraction())
            return false;

        ScreenTerrainPoint? terrainPoint = FindScreenTerrainPoint(point);
        if (terrainPoint != null)
        {
            SelectTerrainPoint(terrainPoint);
            TerrainPointEditRequested?.Invoke(this, new ViewportTerrainPointEditRequestedEventArgs(
                terrainPoint.TerrainIndex,
                terrainPoint.PointIndex,
                Geometry.Polygons[terrainPoint.TerrainIndex]));
            return true;
        }

        ScreenTerrainFace? terrain = FindScreenTerrain(point);
        if (terrain != null)
        {
            SelectTerrain(terrain);
            TerrainEditRequested?.Invoke(this, new ViewportTerrainEditRequestedEventArgs(terrain.Index, Geometry.Polygons[terrain.Index]));
            return true;
        }

        return false;
    }

    private bool TryBeginTerrainDragAt(Point point, KeyModifiers keyModifiers, IPointer pointer)
    {
        if (!ShouldPrioritizeTerrainInteraction())
            return false;

        ScreenTerrainPoint? terrainPoint = FindScreenTerrainPoint(point);
        if (terrainPoint != null)
        {
            SelectTerrainPoint(terrainPoint);
            _isDraggingTerrainPoint = true;
            _draggingTerrainIndex = terrainPoint.TerrainIndex;
            _draggingTerrainPointIndex = terrainPoint.PointIndex;
            SetViewportCursor(StandardCursorType.SizeAll);
            pointer.Capture(this);
            return true;
        }

        ScreenTerrainFace? terrain = FindScreenTerrain(point);
        if (terrain != null)
        {
            SelectTerrain(terrain);
            _isDraggingTerrainFace = true;
            _draggingTerrainAsAddCopy = IsTerrainAddCopyDrag(keyModifiers);
            _draggingTerrainFaceIndex = terrain.Index;
            SetViewportCursor(StandardCursorType.SizeAll);
            pointer.Capture(this);
            return true;
        }

        return false;
    }

    private bool ShouldPrioritizeTerrainInteraction()
    {
        return _terrainFocusMode ||
            _terrainTexturePaintMode ||
            _terrainBrushAction != TerrainBrushAction.Off;
    }

    private void SelectTerrain(ScreenTerrainFace face)
    {
        SetTerrainPresentationPin(ref _selectedTerrainIndex, face.Index);
        _selectedTerrainPointIndex = -1;
        _selectedMobyIndex = -1;
        TerrainPolygon polygon = Geometry!.Polygons[face.Index];
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.ForTerrain(face.Index, polygon));
        InvalidateVisual();
    }

    private void SelectTerrainPoint(ScreenTerrainPoint point)
    {
        if (Geometry == null || point.TerrainIndex < 0 || point.TerrainIndex >= Geometry.Polygons.Count)
            return;

        TerrainPolygon polygon = Geometry.Polygons[point.TerrainIndex];
        int pointIndex = NormalizeTerrainPointIndex(polygon, point.PointIndex);
        if (pointIndex < 0)
            return;

        SetTerrainPresentationPin(ref _selectedTerrainIndex, point.TerrainIndex);
        _selectedTerrainPointIndex = pointIndex;
        _selectedMobyIndex = -1;
        SelectionChanged?.Invoke(this, ViewportSelectionChangedEventArgs.ForTerrain(point.TerrainIndex, polygon, pointIndex));
        InvalidateVisual();
    }

    private ScreenMoby? FindScreenMoby(Point point)
    {
        return _screenMobys
            .OrderBy(item => DistanceSquared(item.Center, point))
            .ThenByDescending(item => ScreenMobyHitPriority(item))
            .FirstOrDefault(item => DistanceSquared(item.Center, point) <= item.HitRadius * item.HitRadius);
    }

    private ScreenFacingGuide? FindScreenFacingGuide(Point point)
    {
        ScreenFacingGuide? guide = _screenFacingGuide;
        if (guide == null || guide.Index != _selectedMobyIndex)
            return null;

        const double shaftHitRadius = 9;
        const double handleHitRadius = 16;
        return DistanceSquared(point, guide.End) <= handleHitRadius * handleHitRadius ||
            DistanceToSegmentSquared(point, guide.Start, guide.End) <= shaftHitRadius * shaftHitRadius
                ? guide
                : null;
    }

    private int ScreenMobyHitPriority(ScreenMoby item)
    {
        if (item.Index < 0 || item.Index >= Mobys.Count)
            return 0;

        return MobyDrawLayer(Mobys[item.Index]);
    }

    private ScreenTerrainFace? FindScreenTerrain(Point point, double? referenceZ = null)
    {
        if (Geometry == null)
            return null;

        ScreenTerrainFace? best = null;
        int bestPriority = int.MinValue;
        double bestArea = double.MaxValue;
        double bestDepth = double.MaxValue;
        double bestZ = double.MinValue;
        double bestZDistance = double.MaxValue;
        int bestDrawOrder = -1;
        for (int i = 0; i < _screenTerrainFaces.Count; i++)
        {
            ScreenTerrainFace face = _screenTerrainFaces[i];
            if (face.Index < 0 || face.Index >= Geometry.Polygons.Count)
                continue;
            if (!ContainsPoint(face.Points, point))
                continue;

            int priority = TerrainHitPriority(face.Index, Geometry.Polygons[face.Index]);
            double area = Math.Max(1, Math.Abs(PolygonArea(face.Points)));
            double zDistance = referenceZ is double z ? Math.Abs(face.AvgZ - z) : double.MaxValue;
            bool better = _viewMode == ViewportViewMode.Fly3D
                ? priority > bestPriority
                    || (priority == bestPriority && face.Depth < bestDepth - 0.001)
                    || (priority == bestPriority && Math.Abs(face.Depth - bestDepth) <= 0.001 && face.AvgZ > bestZ + 0.001)
                    || (priority == bestPriority && Math.Abs(face.Depth - bestDepth) <= 0.001 && Math.Abs(face.AvgZ - bestZ) <= 0.001 && i > bestDrawOrder)
                : referenceZ.HasValue
                    ? priority > bestPriority
                    || (priority == bestPriority && zDistance < bestZDistance - 0.001)
                    || (priority == bestPriority && Math.Abs(zDistance - bestZDistance) <= 0.001 && area < bestArea * 0.86)
                    || (priority == bestPriority && Math.Abs(zDistance - bestZDistance) <= 0.001 && Math.Abs(area - bestArea) <= 0.001 && i > bestDrawOrder)
                    : priority > bestPriority
                    || (priority == bestPriority && area < bestArea * 0.86)
                    || (priority == bestPriority && Math.Abs(area - bestArea) <= 0.001 && i > bestDrawOrder);
            if (!better)
                continue;

            best = face;
            bestPriority = priority;
            bestArea = area;
            bestDepth = face.Depth;
            bestZ = face.AvgZ;
            bestZDistance = zDistance;
            bestDrawOrder = i;
        }

        return best;
    }

    private ScreenTerrainPoint? FindScreenTerrainPoint(Point point)
    {
        if (Geometry == null)
            return null;

        double maxDistance = _viewMode == ViewportViewMode.Fly3D
            ? 12
            : Math.Clamp(9 + (_zoom * 1.4), 10, 18);
        double bestDistance = maxDistance * maxDistance;
        int bestPriority = int.MinValue;
        ScreenTerrainPoint? best = null;

        for (int faceIndex = _screenTerrainFaces.Count - 1; faceIndex >= 0; faceIndex--)
        {
            ScreenTerrainFace face = _screenTerrainFaces[faceIndex];
            if (face.Index < 0 || face.Index >= Geometry.Polygons.Count)
                continue;

            TerrainPolygon polygon = Geometry.Polygons[face.Index];
            int priority = TerrainHitPriority(face.Index, polygon);
            int pointCount = Math.Min(Math.Min(face.Points.Count, polygon.Points.Count), polygon.ZValues.Length);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                double distance = DistanceSquared(face.Points[pointIndex], point);
                if (distance > bestDistance)
                    continue;
                if (Math.Abs(distance - bestDistance) <= 0.001 && priority < bestPriority)
                    continue;

                bestDistance = distance;
                bestPriority = priority;
                best = new ScreenTerrainPoint(face.Index, pointIndex, face.Points[pointIndex]);
            }
        }

        return best;
    }

    private int TerrainHitPriority(int terrainIndex, TerrainPolygon polygon)
    {
        if (terrainIndex == _selectedTerrainIndex)
            return 1000;
        if (polygon.IsTerrainEdited)
            return 900;
        if (polygon.IsTerrainRemoved)
            return 850;
        if (IsMapUnderlaySurface(polygon.Surface))
            return 100 + MapTerrainSurfaceHitRank(polygon.Surface);

        return 500 + MapTerrainSurfaceHitRank(polygon.Surface);
    }

    private static int MapTerrainSurfaceHitRank(string surface)
    {
        return TerrainMaterialClassifier.NormalizeSurfaceName(surface) switch
        {
            "stone" or "brick" or "cliff" or "metal" => 60,
            "grass" or "ground" => 54,
            "sand" or "wood" => 48,
            "ice" => 42,
            "unknown" => 30,
            "ooze" => 18,
            "lava" => 16,
            "water" => 14,
            _ => 36
        };
    }

    private void DrawGrid(DrawingContext context, Rect bounds)
    {
        Pen major = new(new SolidColorBrush(Color.FromArgb(42, 87, 102, 118)), 1);
        Pen minor = new(new SolidColorBrush(Color.FromArgb(14, 87, 102, 118)), 1);
        double spacing = Math.Clamp(48 * _zoom, 24, 120);
        double startX = bounds.Left + (_pan.X % spacing);
        double startY = bounds.Top + (_pan.Y % spacing);

        for (double x = startX; x < bounds.Right; x += spacing)
            context.DrawLine(Math.Abs(x % (spacing * 4)) < 1 ? major : minor, new Point(x, bounds.Top), new Point(x, bounds.Bottom));

        for (double y = startY; y < bounds.Bottom; y += spacing)
            context.DrawLine(Math.Abs(y % (spacing * 4)) < 1 ? major : minor, new Point(bounds.Left, y), new Point(bounds.Right, y));
    }

    private void DrawEmptyScene(DrawingContext context, Rect bounds, string message)
    {
        Typeface headingTypeface = new("Inter", FontStyle.Normal, FontWeight.SemiBold);
        Typeface bodyTypeface = new("Inter");
        FormattedText heading = new(
            "No captured level data loaded",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            headingTypeface,
            22,
            new SolidColorBrush(Color.FromRgb(228, 234, 240)));
        FormattedText body = new(
            message,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            bodyTypeface,
            15,
            new SolidColorBrush(Color.FromRgb(173, 185, 198)))
        {
            MaxTextWidth = Math.Max(260, bounds.Width * 0.58)
        };

        Point start = new(bounds.Left + 32, bounds.Top + 72);
        context.DrawText(heading, start);
        context.DrawText(body, start + new Vector(0, 42));
    }

    private void DrawFlyScene(DrawingContext context, Rect bounds)
    {
        DrawFlyBackground(context, bounds);

        GeometryCandidate? geometry = Geometry;
        if (geometry == null || geometry.Polygons.Count == 0)
        {
            _flyTerrainInteractiveLodSnapshot = FlyTerrainInteractiveLodSnapshot.Empty;
            _flyTerrainVisibilitySnapshot = FlyTerrainVisibilitySnapshot.Empty;
            DrawEmptyScene(context, bounds, _emptyMessage);
            return;
        }

        List<ProjectedTerrainFace> terrainFaces = new();
        bool useStaticNativeLod = UsesNativeTerrainDistanceLod(geometry);
        bool useFlyGameViewVisibility = UsesFlyGameViewHighDetailVisibility(geometry);
        bool useNativeDistanceLod = useStaticNativeLod || useFlyGameViewVisibility;
        Rect visibleBounds = bounds.Inflate(useNativeDistanceLod ? 220 : 48);
        Dictionary<int, SceneSectorRenderMetadata>? sectorByIndex = useNativeDistanceLod
            ? geometry.SourceSectors
                .GroupBy(sector => sector.SectorIndex)
                .ToDictionary(group => group.Key, group => group.First())
            : null;
        HashSet<int> forcedGameViewSectors = useFlyGameViewVisibility
            ? BuildFlyGameViewForcedSectorIndexes(geometry)
            : [];
        NativeTerrainOcclusionSelection flyOcclusion = useFlyGameViewVisibility
            ? ResolveFlyTerrainOcclusion(geometry)
            : NativeTerrainOcclusionSelection.Unavailable;
        // The retail group remains source provenance and painter-order context.
        // Every unique sector follows its native HP/LP distance queues, and any
        // high-detail face that survives the normal camera/frustum path keeps
        // its source material regardless of group membership.
        IReadOnlySet<int>? nativeGroupSectors = flyOcclusion.VisibleSectors;
        FlyHighDetailGroupSelection highDetailGroup = useFlyGameViewVisibility
            ? ResolveFlyHighDetailGroup(geometry, flyOcclusion, sectorByIndex!)
            : FlyHighDetailGroupSelection.AllSectors;
        int queuedHighDetailSectors = sectorByIndex == null
            ? geometry.SourceSectors.Select(sector => sector.SectorIndex).Distinct().Count()
            : sectorByIndex.Values.Count(sector =>
                forcedGameViewSectors.Contains(sector.SectorIndex) ||
                NativeTerrainFarLod.ShouldQueueHighDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    sector.DisableHighDetail));
        int suppressedHighDetailByGroupCount = 0;
        int suppressedHighDetailBySectorCount = 0;
        int suppressedHighDetailByFaceDistanceCount = 0;
        int foggedHighDetailFaceCount = 0;
        int fullyFoggedHighDetailFaceCount = 0;
        for (int index = 0; index < geometry.Polygons.Count; index++)
        {
            TerrainPolygon polygon = geometry.Polygons[index];
            // Game Camera applies native HP/LP queues across every source
            // sector. Keep its local terrain complete, including genuine
            // source water and support geometry.
            if (!useFlyGameViewVisibility && !ShouldPresentTerrain(geometry, polygon))
                continue;

            bool forceGameViewSector = forcedGameViewSectors.Contains(polygon.SectorIndex);
            if (ShouldDrawAddCopySourcePreview(polygon))
            {
                List<ProjectedPoint> sourceProjected = ProjectFlyTerrainPoints(bounds, polygon.OriginalPoints, polygon.OriginalZValues);
                if (sourceProjected.Count >= 3)
                {
                    Point[] sourcePoints = sourceProjected.Select(point => point.Screen).ToArray();
                    if (IntersectsBounds(sourcePoints, visibleBounds))
                        terrainFaces.Add(new ProjectedTerrainFace(
                            index,
                            polygon,
                            sourcePoints,
                            sourceProjected.Average(point => point.Depth),
                            true,
                            sourceProjected.Select(point => point.Depth).ToArray()));
                }
            }

            if (useNativeDistanceLod &&
                !forceGameViewSector &&
                sectorByIndex!.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector) &&
                !NativeTerrainFarLod.ShouldQueueHighDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    sector.DisableHighDetail))
            {
                suppressedHighDetailBySectorCount++;
                continue;
            }

            List<ProjectedPoint> projected = new();
            int count = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
            for (int i = 0; i < count; i++)
            {
                if (TryProjectFly(bounds, polygon.Points[i].X, polygon.Points[i].Y, polygon.ZValues[i], out ProjectedPoint point))
                    projected.Add(point);
            }

            if (projected.Count != count || projected.Count < 3)
                continue;

            Point[] points = projected.Select(point => point.Screen).ToArray();
            if (!IntersectsBounds(points, visibleBounds))
                continue;

            double[] cameraDepths = projected.Select(point => point.Depth).ToArray();
            if (useNativeDistanceLod &&
                !forceGameViewSector &&
                (!TryBuildRawFaceSlotDepths(polygon, cameraDepths, out double[] rawDepths) ||
                 !NativeTerrainFarLod.ShouldRenderHighDetailFace(rawDepths)))
            {
                suppressedHighDetailByFaceDistanceCount++;
                continue;
            }

            if (useFlyGameViewVisibility && !forceGameViewSector)
            {
                double averageCameraDepth = cameraDepths.Average();
                if (averageCameraDepth > FlyGameViewFogNearDistance)
                    foggedHighDetailFaceCount++;
                if (averageCameraDepth >= FlyGameViewFogFarDistance)
                    fullyFoggedHighDetailFaceCount++;
            }

            double depth = projected.Average(point => point.Depth);
            terrainFaces.Add(new ProjectedTerrainFace(
                index,
                polygon,
                points,
                depth,
                CameraDepths: cameraDepths));
        }

        int nativeLowDetailCullingDistance =
            NativeTerrainFarLod.ConservativeAllSectorCullingDistanceFixed16;
        IReadOnlyList<ProjectedLowDetailTerrainFace> lowDetailFaces = useNativeDistanceLod
            ? BuildProjectedLowDetailTerrainFaces(
                geometry,
                sectorByIndex!,
                bounds,
                visibleBounds,
                forcedGameViewSectors,
                nativeLowDetailCullingDistance)
            : Array.Empty<ProjectedLowDetailTerrainFace>();

        // A terrain face may be outside the camera's native occlusion group,
        // but that must never turn its material into outline-only editor
        // context. Distance/sector visibility still follows the retail path;
        // every face that reaches this draw list keeps its native material.
        suppressedHighDetailByGroupCount = 0;
        foreach (ProjectedLowDetailTerrainFace face in lowDetailFaces)
        {
            _flyVisibleLowDetailCornerColorCounts[face.Polygon.RuntimeKey] =
                face.Polygon.CornerColors.Distinct().Count();
        }

        // Keep the rendered material topology invariant while the camera is
        // moving.  The former input-time budget re-ranked sectors on every
        // frame, reduced their Gouraud subdivision, and restored full detail
        // after a 160 ms pause.  That produced the visible moving/stopped
        // flash without a measurable navigation-speed benefit.  Gesture state
        // remains tracked for diagnostics, but every visible source face now
        // keeps the same material path throughout the gesture.
        HashSet<int>? interactiveFullMaterialFaces = null;
        int visibleTerrainFaceCount = terrainFaces.Count(face => !face.IsAddCopySourcePreview);
        int forcedFullMaterialFaceCount = terrainFaces.Count(face =>
            !face.IsAddCopySourcePreview &&
            (face.Index == _selectedTerrainIndex ||
             face.Polygon.IsTerrainEdited ||
             face.Polygon.IsTerrainRemoved));
        int fullMaterialFaceCount = interactiveFullMaterialFaces == null
            ? visibleTerrainFaceCount
            : terrainFaces.Count(face =>
                !face.IsAddCopySourcePreview &&
                (interactiveFullMaterialFaces.Contains(face.Index) ||
                 face.Index == _selectedTerrainIndex ||
                 face.Polygon.IsTerrainEdited ||
                 face.Polygon.IsTerrainRemoved));
        int partiallyFullMaterialSectorCount = interactiveFullMaterialFaces == null
            ? 0
            : terrainFaces
                .Where(face =>
                    !face.IsAddCopySourcePreview &&
                    face.Polygon.HasNativeHighPolyMaterialPayload &&
                    !RequiresGuardedNativeTerrainBlendFallback(face.Polygon))
                .GroupBy(face => face.Polygon.SectorIndex)
                .Count(group =>
                {
                    int selectedCount = group.Count(face => interactiveFullMaterialFaces.Contains(face.Index));
                    return selectedCount > 0 && selectedCount < group.Count();
                });
        _flyTerrainInteractiveLodSnapshot = new FlyTerrainInteractiveLodSnapshot(
            _flyNavigationInteractiveMaterialLod,
            InteractiveFlyMaterialFaceBudget(bounds),
            visibleTerrainFaceCount,
            fullMaterialFaceCount,
            Math.Max(0, visibleTerrainFaceCount - fullMaterialFaceCount),
            forcedFullMaterialFaceCount,
            partiallyFullMaterialSectorCount);
        bool usedNativeBoundedCompositor = TryDrawNativeTerrainBoundedFrame(
            context,
            bounds,
            geometry,
            terrainFaces,
            lowDetailFaces,
            useFlyGameViewVisibility ? highDetailGroup.Sectors : null,
            useFlyGameViewVisibility ? highDetailGroup.GroupIndex : -1);
        if (!usedNativeBoundedCompositor)
        {
            foreach (ProjectedLowDetailTerrainFace face in lowDetailFaces
                .OrderByDescending(face => face.SortDepth)
                .ThenBy(face => face.Polygon.SectorIndex)
                .ThenBy(face => face.Polygon.FaceIndex))
            {
                DrawLowDetailTerrainFace(context, face);
            }
        }

        List<ProjectedTerrainSideWall> terrainSideWalls = useFlyGameViewVisibility
            ? []
            : BuildFlyTerrainSideWallPreviews(
                geometry,
                bounds,
                visibleBounds,
                applyTerrainSceneFilter: true);
        foreach (ProjectedTerrainSideWall wall in OrderFlyTerrainSideWallsForDrawing(terrainSideWalls))
            DrawTerrainSideWallPreview(context, wall);

        IReadOnlyList<ProjectedTerrainFace> orderedTerrainFaces = OrderFlyTerrainFacesForDrawing(terrainFaces);
        if (!usedNativeBoundedCompositor &&
            useFlyGameViewVisibility &&
            highDetailGroup.Sectors is { } foregroundSectors)
        {
            // The fallback painter has no depth buffer. Preserve ordinary
            // depth ordering inside each layer, but let the collision-selected
            // retail context cover inactive whole-scene geometry where their
            // arbitrary editor-camera projections cross.
            orderedTerrainFaces = orderedTerrainFaces
                .Where(face => !foregroundSectors.Contains(face.Polygon.SectorIndex))
                .Concat(orderedTerrainFaces.Where(face => foregroundSectors.Contains(face.Polygon.SectorIndex)))
                .ToArray();
        }
        foreach (ProjectedTerrainFace face in orderedTerrainFaces)
        {
            if (!face.IsAddCopySourcePreview)
                _screenTerrainFaces.Add(new ScreenTerrainFace(face.Index, face.Points, face.Depth, face.Polygon.AvgZ));
            Color color = TerrainDisplayColor(face.Polygon, geometry);
            if (face.IsAddCopySourcePreview)
            {
                DrawAddCopySourcePreview(context, face.Points, color, flyView: true);
                continue;
            }

            bool selected = face.Index == _selectedTerrainIndex;
            bool edited = face.Polygon.IsTerrainEdited;
            bool simplifyInteractiveMaterial = interactiveFullMaterialFaces != null &&
                !interactiveFullMaterialFaces.Contains(face.Index) &&
                !selected &&
                !edited &&
                !face.Polygon.IsTerrainRemoved;
            bool subduedBroadUnderlay =
                !useFlyGameViewVisibility &&
                _flyCameraIsOverview &&
                IsAuditedBroadOverviewUnderlay(geometry, face.Polygon) &&
                !selected &&
                !edited &&
                face.Index != _hoverTerrainIndex;
            TerrainPatchSafetyKind safety = TerrainPatchSafetyFor(face.Polygon);
            Color fillColor = selected
                ? Color.FromArgb(224, 216, 189, 82)
                : edited
                    ? BlendColor(color, TerrainPatchSafetyColor(safety), 0.38)
                    : TerrainFlyFillColor(color, face.Polygon);
            Pen? pen = selected
                ? new Pen(new SolidColorBrush(Color.FromRgb(255, 236, 127)), 2.2)
                : edited
                    ? new Pen(new SolidColorBrush(TerrainPatchSafetyPenColor(safety)), 1.9)
                    : !simplifyInteractiveMaterial && RequiresGuardedNativeTerrainBlendFallback(face.Polygon)
                        ? CreateTerrainFlyUnderlayPen(face.Polygon.Surface)
                        : null;
            if (usedNativeBoundedCompositor)
            {
                if (selected || edited || face.Polygon.IsTerrainRemoved)
                {
                    byte overlayAlpha = selected ? (byte)122 : (byte)56;
                    Color overlay = Color.FromArgb(overlayAlpha, fillColor.R, fillColor.G, fillColor.B);
                    DrawPolygon(context, face.Points, new SolidColorBrush(overlay), pen);
                }
            }
            else
            {
                if (subduedBroadUnderlay)
                {
                    using (context.PushOpacity(0.42))
                    {
                        _flyHighDetailRenderKinds[face.Index] = DrawTerrainFace(
                            context,
                            face.Points,
                            face.Polygon,
                            fillColor,
                            pen,
                            flyView: true,
                            emphasis: 0,
                            cameraDepths: face.CameraDepths,
                            simplifyInteractiveMaterial: true,
                            suppressGameViewFog: true);
                    }
                }
                else
                {
                    _flyHighDetailRenderKinds[face.Index] = DrawTerrainFace(
                        context,
                        face.Points,
                        face.Polygon,
                        fillColor,
                        pen,
                        flyView: true,
                        emphasis: selected ? 0.48 : edited ? 0.22 : 0,
                        cameraDepths: face.CameraDepths,
                        simplifyInteractiveMaterial: simplifyInteractiveMaterial,
                        suppressGameViewFog: !useFlyGameViewVisibility ||
                            forcedGameViewSectors.Contains(face.Polygon.SectorIndex));
                }
            }
        }

        // Game Camera is the source-faithful material view. Repainting every
        // terrain edge after the opaque fills lets outlines from occluded far
        // faces show through nearby floors and walls because this Avalonia
        // overlay has no depth buffer. Edit Map/Fly overview keeps the global
        // mesh, while hover, selection, edited-face, and point-handle overlays
        // below remain available here for direct editing.
        if (!useFlyGameViewVisibility)
        {
            DrawFlyForegroundTerrainEdges(
                context,
                orderedTerrainFaces,
                interactiveFullMaterialFaces,
                useFlyGameViewVisibility,
                forcedGameViewSectors);
        }
        DrawTerrainHoverTarget(context, orderedTerrainFaces, flyView: true);
        DrawTerrainSelectionChip(context, orderedTerrainFaces, bounds, flyView: true);
        DrawTerrainPriorityOutlines(context, orderedTerrainFaces, flyView: true);
        DrawSelectedTerrainPointHandles(context, orderedTerrainFaces, flyView: true);
        DrawFlyTerrainBrushFootprint(context, bounds, geometry);
        FlyMobyVisibilityCounts mobyVisibility = DrawFlyMobys(context, bounds, useFlyGameViewVisibility);
        DrawNativeMovementOverlays(context, bounds);
        _nativeTerrainOcclusionSnapshot = new NativeTerrainOcclusionSnapshot(
            "Game Camera",
            flyOcclusion.Available,
            flyOcclusion.CollisionTriangleResolved,
            flyOcclusion.GroupIndex,
            flyOcclusion.TriangleIndex,
            flyOcclusion.FloorZ,
            geometry.SourceSectors.Count,
            nativeGroupSectors?.Count ?? geometry.SourceSectors.Count,
            geometry.SourceSectors.Count,
            forcedGameViewSectors.Count,
            flyOcclusion.Available
                ? SourceSceneOverlayContract.TerrainOcclusion
                : "Native terrain occlusion payload unavailable; failed open to source sectors.");
        _flyTerrainVisibilitySnapshot = new FlyTerrainVisibilitySnapshot(
            useStaticNativeLod
                ? FlyTerrainVisibilityMode.NativeDistanceResearch
                : useFlyGameViewVisibility
                    ? FlyTerrainVisibilityMode.GameViewCameraLocalHighDetail
                    : FlyTerrainVisibilityMode.EditorOverviewHighDetail,
            _flipMapY,
            _flyCameraIsOverview,
            geometry.SourceSectors.Select(sector => sector.SectorIndex).Distinct().Count(),
            queuedHighDetailSectors,
            geometry.Polygons.Count,
            visibleTerrainFaceCount,
            suppressedHighDetailByGroupCount,
            suppressedHighDetailBySectorCount,
            suppressedHighDetailByFaceDistanceCount,
            foggedHighDetailFaceCount,
            fullyFoggedHighDetailFaceCount,
            geometry.LowDetailPolygons.Count,
            lowDetailFaces.Count,
            forcedGameViewSectors.Count,
            highDetailGroup.GroupIndex,
            highDetailGroup.Sectors?.Count ?? geometry.SourceSectors.Count,
            mobyVisibility.VisibleCount,
            mobyVisibility.SuppressedFarCount,
            flyOcclusion.Available
                ? "Game Camera keeps every unique source sector on its native HP/LP distance path. Every visible close HP face keeps its source material, including off-group terrain; the resolved retail group remains provenance and painter-order context. Scene data is unchanged."
                : "Game Camera keeps every source sector on native HP/LP distance queues; near HP stays textured, distant source LP remains Gouraud-colored, and source geometry is unchanged.");
    }

    private static void DrawFlyBackground(DrawingContext context, Rect bounds)
    {
        const int bandCount = 18;

        for (int i = 0; i < bandCount; i++)
        {
            double t0 = i / (double)bandCount;
            double t1 = (i + 1) / (double)bandCount;
            double t = (t0 + t1) * 0.5;
            Color color = FlyBackgroundColor(t);
            context.FillRectangle(
                new SolidColorBrush(color),
                new Rect(bounds.Left, bounds.Top + (bounds.Height * t0), bounds.Width, bounds.Height * (t1 - t0) + 1));
        }
    }

    private static Color FlyBackgroundColor(double normalizedY)
    {
        Color top = Color.FromRgb(39, 58, 82);
        Color horizon = Color.FromRgb(48, 61, 68);
        Color bottom = Color.FromRgb(34, 45, 50);
        double t = Math.Clamp(normalizedY, 0, 1);
        return t < 0.52
            ? BlendColor(top, horizon, t / 0.52)
            : BlendColor(horizon, bottom, (t - 0.52) / 0.48);
    }

    private IReadOnlyList<ProjectedLowDetailTerrainFace> BuildProjectedLowDetailTerrainFaces(
        GeometryCandidate geometry,
        IReadOnlyDictionary<int, SceneSectorRenderMetadata> sectorByIndex,
        Rect bounds,
        Rect visibleBounds,
        IReadOnlySet<int> forcedHighDetailSectors,
        int cullingDistanceFixed16)
    {
        List<ProjectedLowDetailTerrainFace> result = [];
        foreach (LowDetailTerrainPolygon polygon in geometry.LowDetailPolygons)
        {
            if (!polygon.HasCompleteNativePayload ||
                forcedHighDetailSectors.Contains(polygon.SectorIndex) ||
                !sectorByIndex.TryGetValue(polygon.SectorIndex, out SceneSectorRenderMetadata? sector) ||
                sector.DisableLowDetail ||
                !NativeTerrainFarLod.ShouldQueueLowDetailSector(
                    FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z),
                    sector.Radius,
                    disableLowDetail: false,
                    sector.ForceLowDetail))
            {
                continue;
            }

            List<ProjectedPoint> projected = [];
            int count = Math.Min(polygon.Points.Count, polygon.ZValues.Count);
            for (int pointIndex = 0; pointIndex < count; pointIndex++)
            {
                if (TryProjectFly(
                    bounds,
                    polygon.Points[pointIndex].X,
                    polygon.Points[pointIndex].Y,
                    polygon.ZValues[pointIndex],
                    out ProjectedPoint point))
                {
                    projected.Add(point);
                }
            }

            // The bounded preview intentionally omits near-plane polygon
            // clipping. Requiring all unique vertices prevents a clipped face
            // from being re-indexed into a different native corner topology.
            if (projected.Count != count || projected.Count < 3 ||
                !TryBuildLowDetailRawFaceSlots(
                    polygon,
                    projected.Select(point => point.Screen).ToArray(),
                    projected.Select(point => point.Depth).ToArray(),
                    out Point[] rawPoints,
                    out double[] rawDepths) ||
                !NativeTerrainFarLod.ShouldRenderLowDetailFace(
                    rawDepths,
                    polygon.TransitionBias,
                    cullingDistanceFixed16))
            {
                continue;
            }

            Point[] uniquePoints = projected.Select(point => point.Screen).ToArray();
            if (!IntersectsBounds(uniquePoints, visibleBounds))
                continue;

            double averageDepth = rawDepths.Average();
            result.Add(new ProjectedLowDetailTerrainFace(
                polygon,
                rawPoints,
                rawDepths,
                averageDepth,
                averageDepth + (polygon.OrderingTableBias * 8.0)));
        }

        return result;
    }

    private void DrawLowDetailTerrainFace(DrawingContext context, ProjectedLowDetailTerrainFace face)
    {
        LowDetailTerrainPolygon polygon = face.Polygon;
        Color[] colors = Enumerable.Range(0, SourceSceneOverlayContract.CornerSlotCount)
            .Select(slot => NativeLowDetailCornerColor(polygon.CornerColors[slot], polygon))
            .ToArray();
        foreach ((int a, int b, int c) in NativeLowDetailTriangleSlots)
        {
            Point[] trianglePoints = [face.RawPoints[a], face.RawPoints[b], face.RawPoints[c]];
            double area = Math.Abs(PolygonArea(trianglePoints));
            if (area < 0.01)
                continue;

            int subdivisions = area >= 7200 ? 3 : 2;
            DrawGouraudTriangle(
                context,
                face.RawPoints[a], colors[a],
                face.RawPoints[b], colors[b],
                face.RawPoints[c], colors[c],
                subdivisions);
        }
    }

    private Color NativeLowDetailCornerColor(
        Spyro.Editor.Core.Primitives.ColorRgba source,
        LowDetailTerrainPolygon polygon)
    {
        Spyro.Editor.Core.Primitives.ColorRgba graded = PreviewEnvironmentColor(source);
        Color color = Color.FromRgb(graded.R, graded.G, graded.B);
        if (!polygon.SemiTransparent)
            return color;

        // Avalonia uses source-over compositing rather than the PSX GPU's four
        // blend equations. Preserve the native mode and use a bounded visual
        // approximation; this preview does not claim exact PSX blending.
        byte alpha = polygon.BlendMode switch
        {
            1 => 120,
            2 => 112,
            3 => 72,
            _ => 144
        };
        color = polygon.BlendMode switch
        {
            1 => BlendColor(color, Colors.White, 0.12),
            2 => BlendColor(color, Colors.Black, 0.36),
            _ => color
        };
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool TryBuildLowDetailRawFaceSlots(
        LowDetailTerrainPolygon polygon,
        IReadOnlyList<Point> projectedPoints,
        IReadOnlyList<double> projectedDepths,
        out Point[] rawPoints,
        out double[] rawDepths)
    {
        rawPoints = new Point[SourceSceneOverlayContract.CornerSlotCount];
        rawDepths = new double[SourceSceneOverlayContract.CornerSlotCount];
        if (polygon.CornerPointIndexes.Count != SourceSceneOverlayContract.CornerSlotCount ||
            projectedPoints.Count != projectedDepths.Count)
        {
            return false;
        }

        for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
        {
            int pointIndex = polygon.CornerPointIndexes[slot];
            if (pointIndex < 0 || pointIndex >= projectedPoints.Count)
                return false;
            rawPoints[slot] = projectedPoints[pointIndex];
            rawDepths[slot] = projectedDepths[pointIndex];
        }

        return true;
    }

    private FlyMobyVisibilityCounts DrawFlyMobys(
        DrawingContext context,
        Rect bounds,
        bool useFlyGameViewVisibility)
    {
        IReadOnlyList<Moby> mobys = Mobys;
        if (mobys.Count == 0)
            return default;

        List<VisibleMoby> visible = new();
        int suppressedFarCount = 0;
        for (int i = 0; i < mobys.Count; i++)
        {
            Moby moby = mobys[i];
            if (moby.IsRemoved)
                continue;
            if (!IsMobyVisible(moby))
                continue;

            if (!TryProjectFly(bounds, moby.Position.X, moby.Position.Y, moby.Position.Z, out ProjectedPoint point))
                continue;

            if (useFlyGameViewVisibility &&
                i != _selectedMobyIndex &&
                point.Depth >= FlyGameViewFogFarDistance)
            {
                suppressedFarCount++;
                continue;
            }

            if (!bounds.Inflate(80).Contains(point.Screen))
                continue;

            double size = FlyMobyMarkerSize(moby, point.Depth);
            Point anchorPoint = point.Screen;
            Point markerPoint = AdjustMobyMarkerPoint(moby, anchorPoint, size);
            _screenMobys.Add(new ScreenMoby(i, markerPoint, MobyHitRadius(moby, size)));
            visible.Add(new VisibleMoby(i, moby, markerPoint, anchorPoint, size, point.Depth));
        }

        HashSet<int> linkedTrueIndexes = BuildLinkedTrueIndexSet(mobys);
        DrawSelectedMobyLinks(context, visible, linkedTrueIndexes);
        bool terrainFocus = IsTerrainVisualFocusActive();
        double ambientOpacity = terrainFocus ? 0.34 : 1.0;
        double linkedOpacity = terrainFocus ? 0.62 : 1.0;
        double selectedOpacity = terrainFocus ? 0.72 : 1.0;

        foreach (VisibleMoby item in visible
            .Where(item => item.Index != _selectedMobyIndex && !linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .OrderBy(item => MobyDrawLayer(item.Moby))
            .ThenByDescending(item => item.Depth))
            DrawMobyWithOpacity(context, item.Point, item.Moby, false, false, item.Size, ambientOpacity);

        foreach (VisibleMoby item in visible
            .Where(item => item.Index != _selectedMobyIndex && linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .OrderBy(item => MobyDrawLayer(item.Moby))
            .ThenByDescending(item => item.Depth))
            DrawMobyWithOpacity(context, item.Point, item.Moby, false, true, item.Size, linkedOpacity);

        foreach (VisibleMoby item in visible.Where(item => item.Index == _selectedMobyIndex))
        {
            double selectedSize = IsPinnedTransportMarker(item.Moby) ? item.Size : item.Size + 2.5;
            DrawMobyWithOpacity(context, item.Point, item.Moby, true, false, selectedSize, selectedOpacity);
        }

        DrawPinnedTransportAnchors(context, visible);
        DrawSelectedMobyFacingGuide(context, bounds, visible, flyView: true);
        DrawMobyLabels(context, bounds, visible, linkedTrueIndexes);
        return new FlyMobyVisibilityCounts(visible.Count, suppressedFarCount);
    }

    private static double FlyMobyMarkerSize(Moby moby, double depth)
    {
        double baseSize = Math.Clamp(2850 / Math.Max(1, depth), 7.2, 20.0);
        return Math.Clamp(baseSize * MobyMarkerVisualScale(moby), 6.8, 24.0);
    }

    private static double MobyMarkerVisualScale(Moby moby)
    {
        string text = GetMobyMarkerText(moby).ToLowerInvariant();
        if (text.Contains("balloonist") || text.Contains("baloonist"))
            return 1.18;

        return moby.VisualKind switch
        {
            MobyVisualKind.Chest => 1.22,
            MobyVisualKind.Dragon => 1.16,
            MobyVisualKind.Actor => 1.12,
            MobyVisualKind.FlightTarget => 1.08,
            MobyVisualKind.Key => 1.08,
            MobyVisualKind.Portal => 1.08,
            MobyVisualKind.Control => 0.96,
            _ => 1.0
        };
    }

    private static int MobyDrawLayer(Moby moby)
    {
        string text = GetMobyMarkerText(moby).ToLowerInvariant();
        if (IsTransportBalloonMarkerText(text))
            return 0;
        if (moby.VisualKind == MobyVisualKind.Scenery)
            return 5;
        if (moby.VisualKind == MobyVisualKind.Control)
            return 8;
        if (moby.VisualKind == MobyVisualKind.Gem)
            return 15;
        if (moby.VisualKind == MobyVisualKind.FlightTarget)
            return 20;
        if (moby.VisualKind == MobyVisualKind.Chest)
            return 25;
        if (moby.VisualKind == MobyVisualKind.Actor)
            return text.Contains("balloonist") || text.Contains("baloonist") ? 38 : 30;
        if (moby.VisualKind is MobyVisualKind.Key or MobyVisualKind.Dragon or MobyVisualKind.Portal or MobyVisualKind.Whirlwind)
            return 35;

        return 10;
    }

    private static Point AdjustMobyMarkerPoint(Moby moby, Point point, double size)
    {
        Vector offset = MobyMarkerDisplayOffset(moby, size);
        return new Point(point.X + offset.X, point.Y + offset.Y);
    }

    private static Vector MobyMarkerDisplayOffset(Moby moby, double size)
    {
        string text = GetMobyMarkerText(moby).ToLowerInvariant();
        if (moby.IsFlyInLandingControl)
            return new Vector(0, -(size * 1.35));

        if (IsReturnHomeText(text))
            return new Vector(0, -(size * 0.66));

        if (text.Contains("balloonist") || text.Contains("baloonist"))
            return new Vector(size * 0.58, -size * 0.62);

        if (IsTransportBalloonMarkerText(text))
            return new Vector(-size * 0.28, size * 0.22);

        return default;
    }

    private void DrawGeometry(DrawingContext context, Rect bounds, GeometryCandidate geometry)
    {
        SceneTransform transform = CreateGeometryTransform(bounds, geometry);
        Rect visibleBounds = bounds.Inflate(32);
        List<ProjectedTerrainFace> visibleFaces = new();
        NativeTerrainOcclusionSelection mapOcclusion = ResolveMapTerrainOcclusion(geometry);

        for (int index = 0; index < geometry.Polygons.Count; index++)
        {
            TerrainPolygon polygon = geometry.Polygons[index];
            if (!ShouldPresentTerrain(geometry, polygon))
                continue;

            if (ShouldDrawAddCopySourcePreview(polygon))
            {
                Point[] sourcePoints = ProjectMapTerrainPoints(
                    transform,
                    polygon.OriginalPoints,
                    polygon.OriginalZValues,
                    OriginalAverageZ(polygon));
                if (IntersectsBounds(sourcePoints, visibleBounds))
                    visibleFaces.Add(new ProjectedTerrainFace(index, polygon, sourcePoints, 0, true));
            }

            Point[] points = ProjectMapTerrainPoints(transform, polygon.Points, polygon.ZValues, polygon.AvgZ);

            if (!IntersectsBounds(points, visibleBounds))
                continue;

            visibleFaces.Add(new ProjectedTerrainFace(index, polygon, points, 0));
        }

        IReadOnlyList<ProjectedTerrainFace> orderedFaces = OrderMapTerrainFacesForDrawing(visibleFaces);
        int nativeMaterialCandidateFaceCount = 0;
        int nativeTextureFaceCount = 0;
        int nativeUntexturedGouraudFaceCount = 0;
        int guardedUnderlayFallbackFaceCount = 0;
        int opaqueGuardedUnderlayFallbackFaceCount = 0;
        int projectionDegenerateFaceCount = 0;
        int unresolvedFallbackFaceCount = 0;
        int auditedBroadUnderlayFaceCount = 0;
        int subduedBroadUnderlayFaceCount = 0;
        int offCameraSourceOutlineFaceCount = 0;
        foreach (ProjectedTerrainFace face in orderedFaces)
        {
            TerrainPolygon polygon = face.Polygon;
            Color surfaceColor = TerrainDisplayColor(polygon, geometry);
            if (face.IsAddCopySourcePreview)
            {
                DrawAddCopySourcePreview(context, face.Points, surfaceColor, flyView: false);
                continue;
            }

            bool selected = face.Index == _selectedTerrainIndex;
            bool edited = polygon.IsTerrainEdited;
            TerrainPatchSafetyKind safety = TerrainPatchSafetyFor(polygon);
            Color fillColor = selected
                ? Color.FromArgb(214, 216, 189, 82)
                : edited
                    ? BlendColor(surfaceColor, TerrainPatchSafetyColor(safety), 0.32)
                    : TerrainMapFillColor(surfaceColor, polygon);
            Pen? pen = selected
                ? new Pen(new SolidColorBrush(Color.FromRgb(255, 236, 127)), 2.4)
                : edited
                    ? new Pen(new SolidColorBrush(TerrainPatchSafetyPenColor(safety)), 1.9)
                    : RequiresGuardedNativeTerrainBlendFallback(polygon)
                        ? CreateTerrainUnderlayPen(polygon.Surface)
                        : CreateTerrainFaceDetailPen(flyView: false);
            NativeTerrainFaceRenderKind nativeRenderKind = DrawTerrainFace(
                context,
                face.Points,
                polygon,
                fillColor,
                pen,
                flyView: false,
                emphasis: selected ? 0.48 : edited ? 0.22 : 0,
                cameraDepths: null,
                simplifyInteractiveMaterial: false);
            if (polygon.HasNativeHighPolyMaterialPayload)
            {
                nativeMaterialCandidateFaceCount++;
                switch (nativeRenderKind)
                {
                    case NativeTerrainFaceRenderKind.NativeTexture:
                        nativeTextureFaceCount++;
                        break;
                    case NativeTerrainFaceRenderKind.NativeUntexturedGouraud:
                        nativeUntexturedGouraudFaceCount++;
                        break;
                    case NativeTerrainFaceRenderKind.GuardedUnderlayFallback:
                        guardedUnderlayFallbackFaceCount++;
                        if (!polygon.NativePrimitiveSemiTransparent)
                            opaqueGuardedUnderlayFallbackFaceCount++;
                        break;
                    case NativeTerrainFaceRenderKind.ProjectionDegenerate:
                        projectionDegenerateFaceCount++;
                        break;
                    default:
                        unresolvedFallbackFaceCount++;
                        break;
                }
            }
            if (!face.IsAddCopySourcePreview)
                _screenTerrainFaces.Add(new ScreenTerrainFace(face.Index, face.Points, face.Depth, face.Polygon.AvgZ));
        }

        _nativeTerrainMapMaterialSnapshot = new NativeTerrainMapMaterialSnapshot(
            NativeTerrainMapMaterialContract,
            orderedFaces.Count(face => !face.IsAddCopySourcePreview),
            auditedBroadUnderlayFaceCount,
            subduedBroadUnderlayFaceCount,
            nativeMaterialCandidateFaceCount,
            nativeTextureFaceCount,
            nativeUntexturedGouraudFaceCount,
            guardedUnderlayFallbackFaceCount,
            opaqueGuardedUnderlayFallbackFaceCount,
            projectionDegenerateFaceCount,
            unresolvedFallbackFaceCount,
            _normalTerrainTextureImageFiles.Count,
            offCameraSourceOutlineFaceCount,
            "Edit Map material-renders every captured face with native normal-HQ texture data and the proven physical HP table-2 overview endpoint. Game Camera supplies the camera-dependent two-table depth cue and distance LOD.");

        _nativeTerrainOcclusionSnapshot = new NativeTerrainOcclusionSnapshot(
            "Edit Map",
            mapOcclusion.Available,
            mapOcclusion.CollisionTriangleResolved,
            mapOcclusion.GroupIndex,
            mapOcclusion.TriangleIndex,
            mapOcclusion.FloorZ,
            geometry.SourceSectors.Count,
            mapOcclusion.VisibleSectors?.Count ?? geometry.SourceSectors.Count,
            geometry.SourceSectors.Count,
            _selectedTerrainIndex >= 0 ? 1 : 0,
            mapOcclusion.Available
                ? SourceSceneOverlayContract.TerrainOcclusion
                : "Native terrain occlusion payload unavailable; failed open to all source sectors.");

        DrawMapForegroundTerrainEdges(context, orderedFaces);
        DrawTerrainHoverTarget(context, orderedFaces, flyView: false);
        DrawTerrainSelectionChip(context, orderedFaces, bounds, flyView: false);
        DrawTerrainPriorityOutlines(context, orderedFaces, flyView: false);
        DrawSelectedTerrainPointHandles(context, orderedFaces, flyView: false);
        DrawTerrainBrushFootprint(context, bounds, geometry, transform);
        BuildTerrainSurfaceLabels(orderedFaces, bounds);
    }

    private static Point[] ProjectMapTerrainPoints(
        SceneTransform transform,
        IReadOnlyList<Vector2f> points,
        IReadOnlyList<float> zValues,
        double fallbackZ)
    {
        Point[] projected = new Point[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            double z = i < zValues.Count ? zValues[i] : fallbackZ;
            projected[i] = transform.Project(points[i].X, points[i].Y, z);
        }

        return projected;
    }

    private IReadOnlyList<ProjectedTerrainFace> OrderMapTerrainFacesForDrawing(IReadOnlyList<ProjectedTerrainFace> faces)
    {
        return faces
            .OrderBy(MapTerrainVisualLayer)
            .ThenBy(face => face.Polygon.AvgZ)
            .ThenByDescending(face => Math.Abs(PolygonArea(face.Points)))
            .ThenBy(face => face.Index)
            .ToArray();
    }

    private int MapTerrainVisualLayer(ProjectedTerrainFace face)
    {
        if (face.IsAddCopySourcePreview)
            return 0;
        if (face.Index == _selectedTerrainIndex)
            return 70;
        if (face.Polygon.IsTerrainEdited)
            return 60;
        if (face.Polygon.IsTerrainRemoved)
            return 58;

        // The previous editor styling painted material families in a fixed
        // order, so stone/cliff faces always covered grass/ground where their
        // top-down projections overlapped. That created a systematic tan bias
        // unrelated to the source geometry. Unedited native faces now share a
        // layer and are ordered by their actual average height below.
        return 30;
    }

    private IReadOnlyList<ProjectedTerrainFace> OrderFlyTerrainFacesForDrawing(IReadOnlyList<ProjectedTerrainFace> faces)
    {
        return faces
            .OrderBy(FlyTerrainVisualLayer)
            .ThenByDescending(face => face.Depth)
            .ThenBy(face => face.Index)
            .ToArray();
    }

    private static int InteractiveFlyMaterialFaceBudget(Rect bounds)
    {
        int viewportScaledBudget = (int)Math.Round(
            Math.Max(1, bounds.Width) * Math.Max(1, bounds.Height) /
            InteractiveFlyMaterialBudgetPixelsPerFace);
        return Math.Clamp(
            viewportScaledBudget,
            InteractiveFlyMinimumFullMaterialFaceBudget,
            InteractiveFlyMaximumFullMaterialFaceBudget);
    }

    private static HashSet<int> SelectInteractiveFlyFullMaterialFaces(
        IReadOnlyList<ProjectedTerrainFace> faces,
        Rect bounds)
    {
        int budget = InteractiveFlyMaterialFaceBudget(bounds);
        ProjectedTerrainFace[][] sectors = faces
            .Where(face =>
                !face.IsAddCopySourcePreview &&
                face.Polygon.HasNativeHighPolyMaterialPayload &&
                !RequiresGuardedNativeTerrainBlendFallback(face.Polygon))
            .GroupBy(face => face.Polygon.SectorIndex)
            .Select(group => group
                .OrderBy(face => face.Index)
                .ToArray())
            .OrderBy(group => group.Min(face => face.Depth))
            .ThenByDescending(group => group.Sum(face => Math.Abs(PolygonArea(face.Points))))
            .ThenBy(group => group[0].Polygon.SectorIndex)
            .ToArray();

        HashSet<int> selected = [];
        foreach (ProjectedTerrainFace[] sector in sectors)
        {
            // Whole sectors switch material quality together. The previous
            // per-face budget left alternating textured and flat triangles on
            // one cliff wall, which looked like missing geometry while moving.
            if (selected.Count > 0 && selected.Count + sector.Length > budget)
                continue;

            foreach (ProjectedTerrainFace face in sector)
                selected.Add(face.Index);
            if (selected.Count >= budget)
                break;
        }

        return selected;
    }

    private List<ProjectedTerrainSideWall> BuildFlyTerrainSideWallPreviews(
        GeometryCandidate geometry,
        Rect bounds,
        Rect visibleBounds,
        bool applyTerrainSceneFilter)
    {
        List<TerrainSideWallPreviewCandidate> candidates = BuildTerrainSideWallPreviewCandidates(geometry);
        if (applyTerrainSceneFilter)
            candidates.RemoveAll(candidate => !ShouldPresentTerrain(geometry, candidate.Polygon));
        if (candidates.Count == 0)
            return new List<ProjectedTerrainSideWall>();

        Dictionary<string, List<TerrainSideWallPreviewCandidate>> candidatesByOriginalEdge = candidates
            .GroupBy(candidate => candidate.OriginalEdgeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        List<ProjectedTerrainSideWall> previews = new();
        foreach (TerrainSideWallPreviewCandidate candidate in candidates)
        {
            if (HasMatchingEditedTerrainPreviewEdge(candidate, candidatesByOriginalEdge))
                continue;

            ProjectedPoint[] projected =
            [
                ProjectSideWallPoint(candidate.EditedA),
                ProjectSideWallPoint(candidate.EditedB),
                ProjectSideWallPoint(candidate.OriginalB),
                ProjectSideWallPoint(candidate.OriginalA)
            ];
            if (projected.Any(point => point.Depth <= 0))
                continue;

            Point[] points = projected.Select(point => point.Screen).ToArray();
            if (!IntersectsBounds(points, visibleBounds))
                continue;

            Color baseColor = TerrainDisplayColor(candidate.Polygon, geometry);
            Color fill = TerrainSideWallFillColor(baseColor, candidate.Polygon, candidate.PolygonIndex == _selectedTerrainIndex);
            Color line = TerrainSideWallLineColor(fill, candidate.PolygonIndex == _selectedTerrainIndex);
            previews.Add(new ProjectedTerrainSideWall(
                candidate.PolygonIndex,
                candidate.EdgeIndex,
                candidate.Polygon,
                points,
                projected.Average(point => point.Depth),
                fill,
                line));
        }

        return previews;

        ProjectedPoint ProjectSideWallPoint(Vector3f point)
        {
            return TryProjectFly(bounds, point.X, point.Y, point.Z, out ProjectedPoint projected)
                ? projected
                : default;
        }
    }

    private static List<TerrainSideWallPreviewCandidate> BuildTerrainSideWallPreviewCandidates(GeometryCandidate geometry)
    {
        List<TerrainSideWallPreviewCandidate> candidates = new();
        int candidateId = 0;
        for (int polygonIndex = 0; polygonIndex < geometry.Polygons.Count; polygonIndex++)
        {
            TerrainPolygon polygon = geometry.Polygons[polygonIndex];
            if (polygon.IsTerrainRemoved || polygon.IsTerrainAddClone || (!polygon.HasHeightEdit && !polygon.HasPositionEdit))
                continue;

            int count = Math.Min(
                Math.Min(polygon.Points.Count, polygon.OriginalPoints.Count),
                Math.Min(polygon.ZValues.Length, polygon.OriginalZValues.Length));
            if (count < 3)
                continue;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3f originalA = ToTerrainPreviewPoint(polygon.OriginalPoints[i], polygon.OriginalZValues[i]);
                Vector3f originalB = ToTerrainPreviewPoint(polygon.OriginalPoints[next], polygon.OriginalZValues[next]);
                Vector3f editedA = ToTerrainPreviewPoint(polygon.Points[i], polygon.ZValues[i]);
                Vector3f editedB = ToTerrainPreviewPoint(polygon.Points[next], polygon.ZValues[next]);
                if (!TerrainPreviewPointChanged(originalA, editedA) && !TerrainPreviewPointChanged(originalB, editedB))
                    continue;

                candidates.Add(new TerrainSideWallPreviewCandidate(
                    ++candidateId,
                    polygonIndex,
                    i,
                    polygon,
                    originalA,
                    originalB,
                    editedA,
                    editedB,
                    TerrainPreviewEdgeKey(originalA, originalB),
                    TerrainPreviewEdgeKey(editedA, editedB)));
            }
        }

        return candidates;
    }

    private static bool HasMatchingEditedTerrainPreviewEdge(
        TerrainSideWallPreviewCandidate candidate,
        IReadOnlyDictionary<string, List<TerrainSideWallPreviewCandidate>> candidatesByOriginalEdge)
    {
        if (!candidatesByOriginalEdge.TryGetValue(candidate.OriginalEdgeKey, out List<TerrainSideWallPreviewCandidate>? peers))
            return false;

        return peers.Any(peer =>
            peer.CandidateId != candidate.CandidateId &&
            string.Equals(peer.EditedEdgeKey, candidate.EditedEdgeKey, StringComparison.Ordinal));
    }

    private static Vector3f ToTerrainPreviewPoint(Vector2f point, float z)
    {
        return new Vector3f(point.X, point.Y, z);
    }

    private static bool TerrainPreviewPointChanged(Vector3f original, Vector3f edited)
    {
        const float epsilon = 0.01f;
        return MathF.Abs(original.X - edited.X) > epsilon
            || MathF.Abs(original.Y - edited.Y) > epsilon
            || MathF.Abs(original.Z - edited.Z) > epsilon;
    }

    private static string TerrainPreviewEdgeKey(Vector3f a, Vector3f b)
    {
        string aKey = TerrainPreviewPointKey(a);
        string bKey = TerrainPreviewPointKey(b);
        return string.CompareOrdinal(aKey, bKey) <= 0
            ? $"{aKey}|{bKey}"
            : $"{bKey}|{aKey}";
    }

    private static string TerrainPreviewPointKey(Vector3f point)
    {
        return $"{QuantizeTerrainPreviewCoordinate(point.X)}:{QuantizeTerrainPreviewCoordinate(point.Y)}:{QuantizeTerrainPreviewCoordinate(point.Z)}";
    }

    private static long QuantizeTerrainPreviewCoordinate(float value)
    {
        return (long)MathF.Round(value * 8f);
    }

    private static IReadOnlyList<ProjectedTerrainSideWall> OrderFlyTerrainSideWallsForDrawing(IReadOnlyList<ProjectedTerrainSideWall> walls)
    {
        return walls
            .OrderByDescending(wall => wall.Depth)
            .ThenBy(wall => wall.PolygonIndex)
            .ThenBy(wall => wall.EdgeIndex)
            .ToArray();
    }

    private static Color TerrainSideWallFillColor(Color baseColor, TerrainPolygon polygon, bool selected)
    {
        Color fill = IsMapUnderlaySurface(polygon.Surface)
            ? BlendColor(baseColor, Color.FromRgb(29, 42, 56), 0.36)
            : BlendColor(baseColor, Colors.Black, 0.24);
        byte alpha = IsMapUnderlaySurface(polygon.Surface) ? (byte)166 : (byte)218;
        if (selected)
            fill = BlendColor(fill, Color.FromRgb(255, 236, 127), 0.18);
        return Color.FromArgb(alpha, fill.R, fill.G, fill.B);
    }

    private static Color TerrainSideWallLineColor(Color fill, bool selected)
    {
        Color line = selected
            ? Color.FromRgb(255, 236, 127)
            : BlendColor(fill, Colors.Black, 0.36);
        return Color.FromArgb(selected ? (byte)190 : (byte)118, line.R, line.G, line.B);
    }

    private static void DrawTerrainSideWallPreview(DrawingContext context, ProjectedTerrainSideWall wall)
    {
        Pen borderPen = new(new SolidColorBrush(wall.LineColor), 0.82);
        DrawPolygon(context, wall.Points, new SolidColorBrush(wall.FillColor), borderPen);
        if (wall.Points.Count >= 4)
        {
            Color creaseColor = BlendColor(wall.LineColor, Colors.Black, 0.16);
            Pen creasePen = new(new SolidColorBrush(Color.FromArgb(72, creaseColor.R, creaseColor.G, creaseColor.B)), 0.55);
            context.DrawLine(creasePen, wall.Points[0], wall.Points[2]);
        }
    }

    private int FlyTerrainVisualLayer(ProjectedTerrainFace face)
    {
        if (face.IsAddCopySourcePreview)
            return 0;
        if (face.Index == _selectedTerrainIndex)
            return 50;
        if (face.Polygon.IsTerrainEdited)
            return 44;
        if (face.Polygon.IsTerrainRemoved)
            return 42;
        // Fly is a perspective view. Source depth, not an editor surface
        // label, decides which unedited native face is in front. A broad
        // semantic underlay layer caused opaque cliff walls whose texture was
        // labeled "lava" to be painted behind unrelated geometry.
        return 28;
    }

    private static Color TerrainMapFillColor(Color color, TerrainPolygon polygon)
    {
        if (!RequiresGuardedNativeTerrainBlendFallback(polygon))
            return color;

        Color softened = TryGetTerrainFamilyColor(polygon.Surface, out Color familyColor)
            ? BlendColor(color, familyColor, 0.24)
            : color;
        byte alpha = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface) switch
        {
            "lava" => 112,
            "ooze" => 90,
            _ => 72
        };
        return Color.FromArgb((byte)Math.Min(color.A, alpha), softened.R, softened.G, softened.B);
    }

    private Color TerrainFlyFillColor(Color color, TerrainPolygon polygon)
    {
        if (!RequiresGuardedNativeTerrainBlendFallback(polygon))
            return Color.FromArgb(255, color.R, color.G, color.B);

        Color softened = TryGetTerrainFamilyColor(polygon.Surface, out Color familyColor)
            ? BlendColor(color, familyColor, IsTerrainVisualFocusActive() ? 0.34 : 0.42)
            : color;
        bool terrainFocus = IsTerrainVisualFocusActive();
        byte alpha = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface) switch
        {
            "lava" => terrainFocus ? (byte)202 : (byte)188,
            "ooze" => terrainFocus ? (byte)192 : (byte)178,
            _ => terrainFocus ? (byte)198 : (byte)184
        };
        return Color.FromArgb((byte)Math.Min(color.A, alpha), softened.R, softened.G, softened.B);
    }

    private Pen CreateTerrainUnderlayPen(string surface)
    {
        double detail = Math.Clamp((_zoom - 0.75) / 2.0, 0, 1);
        string normalized = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        byte alpha = normalized switch
        {
            "lava" => (byte)Math.Round(Lerp(34, 58, detail)),
            "ooze" => (byte)Math.Round(Lerp(30, 50, detail)),
            _ => (byte)Math.Round(Lerp(22, 38, detail))
        };
        Color color = normalized switch
        {
            "lava" => Color.FromArgb(alpha, 92, 28, 20),
            "ooze" => Color.FromArgb(alpha, 24, 64, 32),
            _ => Color.FromArgb(alpha, 18, 49, 80)
        };
        return new Pen(new SolidColorBrush(color), Lerp(0.32, 0.46, detail));
    }

    private Pen CreateTerrainFlyUnderlayPen(string surface)
    {
        string normalized = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        bool terrainFocus = IsTerrainVisualFocusActive();
        Color color = normalized switch
        {
            "lava" => Color.FromArgb(terrainFocus ? (byte)54 : (byte)38, 88, 26, 18),
            "ooze" => Color.FromArgb(terrainFocus ? (byte)46 : (byte)32, 22, 62, 32),
            _ => Color.FromArgb(terrainFocus ? (byte)34 : (byte)22, 18, 48, 78)
        };
        return new Pen(new SolidColorBrush(color), terrainFocus ? 0.34 : 0.24);
    }

    private static bool IsMapUnderlaySurface(string surface)
    {
        return TerrainMaterialClassifier.NormalizeSurfaceName(surface) is "water" or "lava" or "ooze";
    }

    private static bool RequiresGuardedNativeTerrainBlendFallback(TerrainPolygon polygon)
    {
        // A material/surface label is not native blend proof. Many levels reuse
        // visually similar texture IDs on opaque walls and on hazards. Only a
        // raw semitransparent primitive needs the conservative source-over
        // fallback; opaque native textures must always keep their decoded art.
        return polygon.HasNativeHighPolyMaterialPayload &&
            polygon.NativePrimitiveSemiTransparent &&
            !polygon.IsNativeUntexturedSentinel;
    }

    private void BuildTerrainSurfaceLabels(IReadOnlyList<ProjectedTerrainFace> faces, Rect bounds)
    {
        _screenTerrainSurfaceLabels.Clear();
        if (!ShouldDrawTerrainSurfaceLabels(bounds))
            return;

        Rect labelBounds = bounds.Deflate(new Thickness(18, 58, 18, 28));
        Dictionary<string, TerrainSurfaceLabelAccumulator> labels = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectedTerrainFace face in faces)
        {
            if (face.IsAddCopySourcePreview || face.Points.Count < 3)
                continue;

            string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Polygon.Surface);
            if (string.IsNullOrWhiteSpace(surface))
                surface = "unknown";

            Point center = Centroid(face.Points);
            if (!labelBounds.Contains(center))
                continue;

            double area = Math.Abs(PolygonArea(face.Points));
            if (area < 18)
                continue;

            if (!labels.TryGetValue(surface, out TerrainSurfaceLabelAccumulator? label))
            {
                label = new TerrainSurfaceLabelAccumulator(surface, TerrainSurfaceLabelColor(face.Polygon));
                labels.Add(surface, label);
            }

            label.Add(face, center, area);
        }

        int maxLabels = _zoom >= 1.45 ? 8 : 5;
        foreach (TerrainSurfaceLabelAccumulator label in labels.Values
            .Where(label => label.FaceCount >= 3 || label.TotalArea >= 420)
            .OrderByDescending(label => TerrainSurfaceLabelScore(label))
            .Take(maxLabels))
        {
            Point point = label.Center;
            if (!labelBounds.Contains(point))
                point = new Point(
                    Math.Clamp(point.X, labelBounds.Left, labelBounds.Right),
                    Math.Clamp(point.Y, labelBounds.Top, labelBounds.Bottom));

            _screenTerrainSurfaceLabels.Add(new ScreenTerrainSurfaceLabel(
                TerrainMaterialClassifier.FormatSurface(label.Surface),
                point,
                label.Color,
                TerrainSurfaceLabelScore(label)));
        }
    }

    private bool ShouldDrawTerrainSurfaceLabels(Rect bounds)
    {
        return _viewMode == ViewportViewMode.Map
            && bounds.Width >= 460
            && IsTerrainVisualFocusActive()
            && _zoom >= 0.82;
    }

    private static double TerrainSurfaceLabelScore(TerrainSurfaceLabelAccumulator label)
    {
        double surfaceBoost = TerrainMaterialClassifier.NormalizeSurfaceName(label.Surface) switch
        {
            "water" or "lava" or "ooze" => 1.45,
            "unknown" => 1.25,
            "grass" or "ground" => 0.9,
            _ => 1.0
        };
        return (Math.Sqrt(label.TotalArea) + (label.FaceCount * 18)) * surfaceBoost;
    }

    private static Color TerrainSurfaceLabelColor(TerrainPolygon polygon)
    {
        return TryGetTerrainFamilyColor(polygon.Surface, out Color familyColor)
            ? familyColor
            : Color.FromRgb(polygon.SurfaceColor.R, polygon.SurfaceColor.G, polygon.SurfaceColor.B);
    }

    private void DrawTerrainBrushFootprint(DrawingContext context, Rect bounds, GeometryCandidate geometry, SceneTransform transform)
    {
        if (_terrainBrushAction == TerrainBrushAction.Off)
            return;

        int terrainIndex = _terrainBrushPreviewTerrainIndex >= 0 ? _terrainBrushPreviewTerrainIndex : _selectedTerrainIndex;
        if (terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count || _terrainBrushRadius <= 1)
            return;

        TerrainPolygon polygon = geometry.Polygons[terrainIndex];
        Vector2f brushCenter = _terrainBrushPreviewWorldPoint ?? polygon.Center;
        Point center = transform.Project(brushCenter.X, brushCenter.Y, TerrainZAt(polygon, brushCenter));
        Vector2f brushEdge = new((float)(brushCenter.X + _terrainBrushRadius), brushCenter.Y);
        Point edge = transform.Project(brushEdge.X, brushEdge.Y, TerrainZAt(polygon, brushEdge));
        double radius = Math.Max(4, Math.Abs(edge.X - center.X));
        Color color = TerrainBrushActionColor(_terrainBrushAction);
        IBrush fill = new SolidColorBrush(Color.FromArgb(16, color.R, color.G, color.B));
        Pen rim = new(new SolidColorBrush(Color.FromArgb(216, color.R, color.G, color.B)), 1.45);
        Pen softRim = new(new SolidColorBrush(Color.FromArgb(46, color.R, color.G, color.B)), 4.2);
        context.DrawEllipse(fill, softRim, center, radius, radius);
        context.DrawEllipse(null, rim, center, radius, radius);
        double coreRadius = TerrainBrushCorePreviewRadius(radius);
        if (coreRadius < radius - 2)
        {
            Pen core = new(new SolidColorBrush(Color.FromArgb(136, color.R, color.G, color.B)), 0.9);
            context.DrawEllipse(null, core, center, coreRadius, coreRadius);
        }

        IReadOnlyList<TerrainBrushPreviewPoint> preview = BuildTerrainBrushVertexPreview(
            geometry,
            brushCenter,
            _terrainBrushRadius,
            _terrainBrushFeather,
            (polygon, index) => transform.Project(polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index]),
            applyTerrainSceneFilter: true,
            out bool clipped);
        DrawTerrainBrushAffectedVertices(context, preview, color);
        DrawTerrainBrushPreviewChip(context, bounds, center, color, TerrainBrushPreviewAnalyzer.Summarize(preview.Select(item => item.Kind), clipped), flyView: false);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(232, color.R, color.G, color.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(210, 18, 24, 32)), 0.9),
            center,
            3.2,
            3.2);
    }

    private void DrawFlyTerrainBrushFootprint(DrawingContext context, Rect bounds, GeometryCandidate geometry)
    {
        if (_terrainBrushAction == TerrainBrushAction.Off)
            return;

        int terrainIndex = _terrainBrushPreviewTerrainIndex >= 0 ? _terrainBrushPreviewTerrainIndex : _selectedTerrainIndex;
        if (terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count || _terrainBrushRadius <= 1)
            return;

        TerrainPolygon polygon = geometry.Polygons[terrainIndex];
        Vector2f brushCenter = _terrainBrushPreviewWorldPoint ?? polygon.Center;
        float z = TerrainZAt(polygon, brushCenter);
        if (!TryProjectFly(bounds, brushCenter.X, brushCenter.Y, z, out ProjectedPoint center))
            return;
        if (!TryProjectFly(bounds, brushCenter.X + _terrainBrushRadius, brushCenter.Y, z, out ProjectedPoint edge))
            return;

        double radius = Math.Clamp(Math.Abs(edge.Screen.X - center.Screen.X), 4, 220);
        Color color = TerrainBrushActionColor(_terrainBrushAction);
        IBrush fill = new SolidColorBrush(Color.FromArgb(18, color.R, color.G, color.B));
        Pen rim = new(new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)), 1.45);
        Pen softRim = new(new SolidColorBrush(Color.FromArgb(52, color.R, color.G, color.B)), 4.2);
        context.DrawEllipse(fill, softRim, center.Screen, radius, radius);
        context.DrawEllipse(null, rim, center.Screen, radius, radius);
        double coreRadius = TerrainBrushCorePreviewRadius(radius);
        if (coreRadius < radius - 2)
        {
            Pen core = new(new SolidColorBrush(Color.FromArgb(142, color.R, color.G, color.B)), 0.9);
            context.DrawEllipse(null, core, center.Screen, coreRadius, coreRadius);
        }

        IReadOnlyList<TerrainBrushPreviewPoint> preview = BuildTerrainBrushVertexPreview(
            geometry,
            brushCenter,
            _terrainBrushRadius,
            _terrainBrushFeather,
            (polygon, index) =>
            {
                return TryProjectFly(bounds, polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index], out ProjectedPoint projected)
                    ? projected.Screen
                    : null;
            },
            applyTerrainSceneFilter: !UsesFlyGameViewHighDetailVisibility(geometry),
            out bool clipped);
        DrawTerrainBrushAffectedVertices(context, preview, color);
        DrawTerrainBrushPreviewChip(context, bounds, center.Screen, color, TerrainBrushPreviewAnalyzer.Summarize(preview.Select(item => item.Kind), clipped), flyView: true);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(236, color.R, color.G, color.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(210, 18, 24, 32)), 0.9),
            center.Screen,
            3.2,
            3.2);
    }

    private static void DrawTerrainBrushAffectedVertices(
        DrawingContext context,
        IReadOnlyList<TerrainBrushPreviewPoint> preview,
        Color color)
    {
        IBrush visualOnlyBrush = new SolidColorBrush(Color.FromArgb(44, 255, 185, 92));
        Pen visualOnlyPen = new(new SolidColorBrush(Color.FromArgb(178, 255, 185, 92)), 0.85);
        foreach (TerrainBrushPreviewPoint item in preview)
        {
            byte alpha = (byte)Math.Round(72 + (item.Falloff * 126));
            double size = 1.4 + (item.Falloff * 2.2);
            if (item.Kind == TerrainBrushPreviewVertexKind.VisualOnly)
            {
                context.DrawEllipse(visualOnlyBrush, visualOnlyPen, item.Point, size + 0.7, size + 0.7);
                continue;
            }

            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
                null,
                item.Point,
                size,
                size);
        }
    }

    private static void DrawTerrainBrushPreviewChip(
        DrawingContext context,
        Rect bounds,
        Point center,
        Color color,
        TerrainBrushPreviewStats stats,
        bool flyView)
    {
        if (bounds.Width < 180 || bounds.Height < 120)
            return;

        string label = TerrainBrushPreviewAnalyzer.FormatSummary(stats);
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            flyView ? 10 : 10.5,
            new SolidColorBrush(Color.FromArgb(230, 244, 248, 252)))
        {
            MaxTextWidth = 120
        };

        double topInset = flyView ? 52 : 46;
        double bottomInset = 34;
        double horizontalInset = 14;
        double availableWidth = Math.Max(1, bounds.Width - (horizontalInset * 2));
        double availableHeight = Math.Max(1, bounds.Height - topInset - bottomInset);
        Rect safeBounds = new(
            bounds.X + horizontalInset,
            bounds.Y + topInset,
            availableWidth,
            availableHeight);
        double maxOriginX = Math.Max(safeBounds.Left, safeBounds.Right - text.Width - 7);
        double maxOriginY = Math.Max(safeBounds.Top, safeBounds.Bottom - text.Height - 4);
        Point desiredOrigin = center + new Vector(12, flyView ? 14 : 16);
        Point origin = new(
            Math.Clamp(desiredOrigin.X, safeBounds.Left, maxOriginX),
            Math.Clamp(desiredOrigin.Y, safeBounds.Top, maxOriginY));
        Rect background = new(origin.X - 7, origin.Y - 4, text.Width + 14, text.Height + 8);
        Color fill = stats.TotalVertices == 0
            ? Color.FromRgb(68, 52, 54)
            : stats.EditableVertices == 0 && stats.VisualOnlyVertices > 0
                ? Color.FromRgb(82, 54, 30)
            : BlendColor(color, Color.FromRgb(18, 24, 31), 0.74);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(214, fill.R, fill.G, fill.B)), background, 5);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(145, color.R, color.G, color.B)), 1),
            background,
            5,
            5);
        context.DrawText(text, origin);
    }

    private IReadOnlyList<TerrainBrushPreviewPoint> BuildTerrainBrushVertexPreview(
        GeometryCandidate geometry,
        Vector2f brushCenter,
        double brushRadius,
        double brushFeather,
        Func<TerrainPolygon, int, Point?> project,
        bool applyTerrainSceneFilter,
        out bool clipped)
    {
        double safeRadius = Math.Max(1, brushRadius);
        List<(Point Point, double Distance, double Falloff, TerrainBrushPreviewVertexKind Kind)> points = new();
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (polygon.IsTerrainRemoved ||
                applyTerrainSceneFilter && !ShouldPresentTerrain(geometry, polygon))
                continue;

            int count = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
            for (int i = 0; i < count; i++)
            {
                double distance = WorldDistance(brushCenter, polygon.Points[i]);
                if (distance > safeRadius)
                    continue;

                Point? projected = project(polygon, i);
                if (projected == null)
                    continue;

                double falloff = TerrainBrushPreviewFalloff(distance / safeRadius, brushFeather);
                TerrainBrushPreviewVertexKind kind = _terrainBrushVertexClassifier?.Invoke(polygon, i) ?? TerrainBrushPreviewVertexKind.Editable;
                points.Add((projected.Value, distance, falloff, kind));
            }
        }

        clipped = points.Count > 140;
        return points
            .OrderBy(item => item.Distance)
            .Take(140)
            .Select(item => new TerrainBrushPreviewPoint(item.Point, item.Falloff, item.Kind))
            .ToArray();
    }

    private void DrawMobys(DrawingContext context, Rect bounds)
    {
        IReadOnlyList<Moby> mobys = Mobys;
        if (mobys.Count == 0)
            return;

        SceneTransform transform = CreateMobyTransform(bounds, mobys);
        List<VisibleMoby> visible = new();

        for (int i = 0; i < mobys.Count; i++)
        {
            Moby moby = mobys[i];
            if (moby.IsRemoved)
                continue;
            if (!IsMobyVisible(moby))
                continue;

            Point point = transform.Project(moby.Position.X, moby.Position.Y, moby.Position.Z);
            double size = MapMobyMarkerSize(moby);
            Point anchorPoint = point;
            Point markerPoint = AdjustMobyMarkerPoint(moby, anchorPoint, size);
            _screenMobys.Add(new ScreenMoby(i, markerPoint, MobyHitRadius(moby, size)));
            visible.Add(new VisibleMoby(i, moby, markerPoint, anchorPoint, size, 0));
        }

        HashSet<int> linkedTrueIndexes = BuildLinkedTrueIndexSet(mobys);
        DrawSelectedMobyLinks(context, visible, linkedTrueIndexes);
        bool terrainFocus = IsTerrainVisualFocusActive();
        double ambientOpacity = terrainFocus ? 0.34 : 1.0;
        double linkedOpacity = terrainFocus ? 0.62 : 1.0;
        double selectedOpacity = terrainFocus ? 0.72 : 1.0;

        foreach (VisibleMoby item in visible
            .Where(item => item.Index != _selectedMobyIndex && !linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .OrderBy(item => MobyDrawLayer(item.Moby)))
            DrawMobyWithOpacity(context, item.Point, item.Moby, false, false, item.Size, ambientOpacity);

        foreach (VisibleMoby item in visible
            .Where(item => item.Index != _selectedMobyIndex && linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .OrderBy(item => MobyDrawLayer(item.Moby)))
            DrawMobyWithOpacity(context, item.Point, item.Moby, false, true, item.Size, linkedOpacity);

        foreach (VisibleMoby item in visible.Where(item => item.Index == _selectedMobyIndex))
        {
            double selectedSize = IsPinnedTransportMarker(item.Moby) ? item.Size : item.Size + 2.4;
            DrawMobyWithOpacity(context, item.Point, item.Moby, true, false, selectedSize, selectedOpacity);
        }

        DrawPinnedTransportAnchors(context, visible);
        DrawSelectedMobyFacingGuide(context, bounds, visible, flyView: false);
        DrawMobyLabels(context, bounds, visible, linkedTrueIndexes);
    }

    private double MapMobyMarkerSize(Moby moby)
    {
        double baseSize = Math.Clamp(6.2 + (_zoom * 1.35), 7.2, 13.8);
        return Math.Clamp(baseSize * MobyMarkerVisualScale(moby), 6.8, 16.2);
    }

    private bool IsMobyVisible(Moby moby)
    {
        return _mobyFilter?.Invoke(moby) ?? true;
    }

    private bool IsTerrainVisualFocusActive()
    {
        return _terrainFocusMode
            || _terrainTexturePaintMode
            || _terrainBrushAction != TerrainBrushAction.Off
            || _isPaintingTerrain
            || _isDraggingTerrainFace
            || _isDraggingTerrainPoint
            || _selectedTerrainIndex >= 0;
    }

    private static void DrawMobyWithOpacity(DrawingContext context, Point point, Moby moby, bool selected, bool linked, double size, double opacity)
    {
        if (opacity >= 0.995)
        {
            DrawMoby(context, point, moby, selected, linked, size);
            return;
        }

        using (context.PushOpacity(opacity))
            DrawMoby(context, point, moby, selected, linked, size);
    }

    private HashSet<int> BuildLinkedTrueIndexSet(IReadOnlyList<Moby> mobys)
    {
        if (_selectedMobyIndex < 0 || _selectedMobyIndex >= mobys.Count)
            return new HashSet<int>();

        return MobyLinkTraversal.GetVisibleLinkedTrueIndexes(mobys[_selectedMobyIndex]);
    }

    private void DrawSelectedMobyLinks(DrawingContext context, IReadOnlyList<VisibleMoby> visible, IReadOnlySet<int> linkedTrueIndexes)
    {
        if (_selectedMobyIndex < 0 || linkedTrueIndexes.Count == 0)
            return;

        VisibleMoby? selected = visible.FirstOrDefault(item => item.Index == _selectedMobyIndex);
        if (selected == null)
            return;

        Dictionary<int, string> relationshipByTrueIndex = new();
        foreach (MobyLink link in MobyLinkTraversal.GetVisibleLinks(selected.Moby))
        {
            string relationship = string.IsNullOrWhiteSpace(link.DisplayName) ? link.Kind : link.DisplayName;
            foreach (int trueIndex in MobyLinkTraversal.GetVisibleLinkTrueIndexes(selected.Moby, link))
                relationshipByTrueIndex.TryAdd(trueIndex, relationship);
        }

        bool terrainFocus = IsTerrainVisualFocusActive();
        Pen linePen = new(new SolidColorBrush(Color.FromArgb(terrainFocus ? (byte)64 : (byte)210, 255, 236, 127)), terrainFocus ? 1.15 : 1.8);
        int labelsDrawn = 0;
        foreach (VisibleMoby target in visible.Where(item => linkedTrueIndexes.Contains(item.Moby.TrueIndex)))
        {
            Point selectedAnchor = MobyRelationshipAnchor(selected);
            Point targetAnchor = MobyRelationshipAnchor(target);
            context.DrawLine(linePen, selectedAnchor, targetAnchor);
            if (terrainFocus || labelsDrawn >= 6 || !relationshipByTrueIndex.TryGetValue(target.Moby.TrueIndex, out string? relationship))
                continue;

            Point labelPoint = new((selectedAnchor.X + targetAnchor.X) * 0.5, (selectedAnchor.Y + targetAnchor.Y) * 0.5);
            DrawRelationshipLabel(context, labelPoint, relationship);
            labelsDrawn++;
        }
    }

    private void DrawPinnedTransportAnchors(DrawingContext context, IReadOnlyList<VisibleMoby> visible)
    {
        foreach (VisibleMoby item in visible.Where(item => IsPinnedTransportMarker(item.Moby)))
        {
            bool selected = item.Index == _selectedMobyIndex;
            Color color = item.Moby.IsFlyInLandingControl
                ? Color.FromRgb(82, 225, 246)
                : Color.FromRgb(255, 219, 66);
            byte alpha = selected ? (byte)255 : (byte)225;
            Pen stem = new(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), selected ? 2.2 : 1.5);
            Pen rim = new(new SolidColorBrush(Color.FromArgb(220, 15, 24, 30)), 1.2);
            context.DrawLine(stem, item.Point, item.AnchorPoint);
            context.DrawEllipse(new SolidColorBrush(color), rim, item.AnchorPoint, selected ? 4.2 : 3.4, selected ? 4.2 : 3.4);
        }
    }

    private static bool IsPinnedTransportMarker(Moby moby) =>
        moby.IsFlyInLandingControl || IsReturnHomeText(GetMobyMarkerText(moby).ToLowerInvariant());

    private static Point MobyRelationshipAnchor(VisibleMoby item) =>
        IsPinnedTransportMarker(item.Moby) ? item.AnchorPoint : item.Point;

    private void DrawSelectedMobyFacingGuide(DrawingContext context, Rect bounds, IReadOnlyList<VisibleMoby> visible, bool flyView)
    {
        if (_selectedMobyIndex < 0)
            return;

        VisibleMoby? selected = visible.FirstOrDefault(item => item.Index == _selectedMobyIndex);
        if (selected == null || selected.Moby.YawByte < 0)
            return;

        if (!TryGetMobyFacingScreenVector(bounds, selected.Moby, flyView, out Vector screenVector))
            return;

        Point guideAnchor = IsPinnedTransportMarker(selected.Moby) ? selected.AnchorPoint : selected.Point;
        if (!TryCreateFacingGuideGeometry(guideAnchor, selected.Size, screenVector, flyView, out FacingGuideGeometry guide))
            return;

        _screenFacingGuide = new ScreenFacingGuide(selected.Index, guide.Start, guide.End);
        DrawFacingGuide(context, bounds, selected.Moby, selected.Size, guide);
    }

    private bool TryGetMobyFacingScreenVector(Rect bounds, Moby moby, bool flyView, out Vector screenVector)
    {
        screenVector = default;
        Vector2f direction = MobyFacingDirection(moby);
        double guideWorldLength = FacingGuideWorldLength(moby);

        if (flyView)
        {
            double z = moby.Position.Z;
            if (TryProjectFly(bounds, moby.Position.X, moby.Position.Y, z, out ProjectedPoint origin) &&
                TryProjectFly(
                    bounds,
                    moby.Position.X + (direction.X * guideWorldLength),
                    moby.Position.Y + (direction.Y * guideWorldLength),
                    z,
                    out ProjectedPoint target))
            {
                screenVector = target.Screen - origin.Screen;
                if (screenVector.Length >= 0.2)
                    return true;
            }
        }
        else
        {
            SceneTransform transform = CreateMobyTransform(bounds, Mobys);
            Point origin = transform.Project(moby.Position.X, moby.Position.Y, moby.Position.Z);
            Point target = transform.Project(
                moby.Position.X + (direction.X * guideWorldLength),
                moby.Position.Y + (direction.Y * guideWorldLength),
                moby.Position.Z);
            screenVector = target - origin;
            if (screenVector.Length >= 0.2)
                return true;
        }

        screenVector = new Vector(
            direction.X * EditorUiDefaults.MapXAxisScreenSign(_flipMapY),
            direction.Y * EditorUiDefaults.MapYAxisScreenSign(_flipMapY));
        return screenVector.Length >= 0.2;
    }

    private static Vector2f MobyFacingDirection(Moby moby)
    {
        if (moby.IsFlyInLandingControl)
            return FlyInLandingEditorControl.HeadingByteToWorldDirection(moby.YawByte);

        double radians = Moby.YawByteToDegrees(moby.YawByte) * Math.PI / 180.0;
        return new Vector2f((float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    private bool TryGetMobyYawByteAtScreenPoint(Moby moby, Point screenPoint, out int yawByte)
    {
        yawByte = moby.YawByte;
        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        Point world;
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (!TryUnprojectFlyToZ(bounds, screenPoint, moby.Position.Z, out world))
                return false;
        }
        else
        {
            SceneTransform transform = CreateMobyTransform(bounds, Mobys);
            world = transform.Unproject(screenPoint, moby.Position.Z);
        }

        double dx = world.X - moby.Position.X;
        double dy = world.Y - moby.Position.Y;
        if ((dx * dx) + (dy * dy) < 0.0001)
            return false;

        double degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        yawByte = moby.IsFlyInLandingControl
            ? FlyInLandingEditorControl.DegreesToHeadingByte(degrees)
            : Moby.DegreesToYawByte(degrees);
        return true;
    }

    private static double FacingGuideWorldLength(Moby moby)
    {
        return moby.VisualKind switch
        {
            MobyVisualKind.Actor or MobyVisualKind.Dragon => 560,
            MobyVisualKind.Chest or MobyVisualKind.Scenery or MobyVisualKind.Portal => 440,
            MobyVisualKind.Gem or MobyVisualKind.Key => 300,
            _ => 380
        };
    }

    private static bool TryCreateFacingGuideGeometry(
        Point point,
        double markerSize,
        Vector screenVector,
        bool flyView,
        out FacingGuideGeometry guide)
    {
        double vectorLength = screenVector.Length;
        if (vectorLength < 0.2)
        {
            guide = default;
            return false;
        }

        Vector direction = screenVector / vectorLength;
        double arrowLength = Math.Clamp(markerSize * (flyView ? 5.0 : 4.5), flyView ? 34 : 30, flyView ? 64 : 56);
        Point start = point + (direction * (markerSize + 6));
        Point end = point + (direction * arrowLength);
        guide = new FacingGuideGeometry(start, end, direction);
        return true;
    }

    private static void DrawFacingGuide(
        DrawingContext context,
        Rect bounds,
        Moby moby,
        double markerSize,
        FacingGuideGeometry guide)
    {
        Vector direction = guide.Direction;
        Point start = guide.Start;
        Point end = guide.End;
        Vector perpendicular = new(-direction.Y, direction.X);

        Color accent = Color.FromRgb(82, 225, 246);
        Color shadow = Color.FromArgb(176, 8, 13, 18);
        Pen shadowPen = new(new SolidColorBrush(shadow), 5.2);
        Pen linePen = new(new SolidColorBrush(accent), 3.0);
        Pen rimPen = new(new SolidColorBrush(Color.FromArgb(230, 7, 15, 22)), 1.2);
        context.DrawLine(shadowPen, start + new Vector(0, 1.5), end + new Vector(0, 1.5));
        context.DrawLine(linePen, start, end);

        double headLength = Math.Clamp(markerSize * 0.9, 7.5, 11.5);
        double headWidth = Math.Clamp(markerSize * 0.62, 5.5, 9.5);
        Point[] head =
        [
            end,
            end - (direction * headLength) + (perpendicular * headWidth),
            end - (direction * headLength) - (perpendicular * headWidth)
        ];
        DrawPolygon(context, head, new SolidColorBrush(accent), rimPen);
        context.DrawEllipse(new SolidColorBrush(accent), rimPen, start, 2.8, 2.8);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(238, 253, 255)),
            new Pen(new SolidColorBrush(Color.FromRgb(20, 65, 75)), 1.2),
            end,
            3.8,
            3.8);

        if (bounds.Width >= 260 && bounds.Height >= 160)
        {
            string label = moby.IsFlyInLandingControl
                ? $"Flies in {FlyInLandingEditorControl.HeadingByteToDegrees(moby.YawByte):0.#} deg"
                : $"Faces {Moby.YawByteToDegrees(moby.YawByte):0.#} deg";
            DrawFacingGuideLabel(context, bounds, end, direction, label);
        }
    }

    private static void DrawFacingGuideLabel(DrawingContext context, Rect bounds, Point anchor, Vector direction, string label)
    {
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            10.5,
            new SolidColorBrush(Color.FromRgb(231, 250, 255)))
        {
            MaxTextWidth = 116
        };

        Vector offset = new(direction.X >= 0 ? 8 : -text.Width - 8, direction.Y >= 0 ? 5 : -text.Height - 5);
        Point origin = anchor + offset;
        origin = new Point(
            Math.Clamp(origin.X, bounds.Left + 8, Math.Max(bounds.Left + 8, bounds.Right - text.Width - 14)),
            Math.Clamp(origin.Y, bounds.Top + 44, Math.Max(bounds.Top + 44, bounds.Bottom - text.Height - 12)));
        Rect background = new(origin - new Vector(5, 3), new Size(text.Width + 10, text.Height + 6));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(226, 23, 38, 48)), background, 4);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(180, 82, 225, 246)), 1),
            background,
            4,
            4);
        context.DrawText(text, origin);
    }

    private static void DrawRelationshipLabel(DrawingContext context, Point point, string relationship)
    {
        FormattedText text = new(
            relationship,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            11,
            new SolidColorBrush(Color.FromRgb(32, 38, 45)))
        {
            MaxTextWidth = 150
        };
        Rect background = new(point - new Vector(5, 4), new Size(text.Width + 10, text.Height + 8));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 255, 236, 127)), background, 4);
        context.DrawText(text, point);
    }

    private static void DrawMoby(DrawingContext context, Point point, Moby moby, bool selected, bool linked, double size)
    {
        Color color = Color.FromArgb(moby.Color.A, moby.Color.R, moby.Color.G, moby.Color.B);
        IBrush fill = new SolidColorBrush(color);
        string markerText = GetMobyMarkerText(moby);
        string markerTextLower = markerText.ToLowerInvariant();
        Pen rim;
        if (selected)
        {
            rim = new Pen(new SolidColorBrush(Color.FromRgb(255, 236, 127)), 3);
        }
        else if (linked)
        {
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 255, 236, 127)), null, point, size + 6, size + 6);
            rim = new Pen(new SolidColorBrush(Color.FromRgb(255, 236, 127)), 2.2);
        }
        else
        {
            rim = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1.5);
        }

        if (TryDrawMobyRasterIcon(context, point, size, rim, moby))
        {
            if (IsQuestionableMobyMarker(moby))
                DrawNeedsIdBadge(context, point, size);
            return;
        }

        if (!IsPinnedTransportMarker(moby))
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, point + new Vector(0, size + 3), size + 2, Math.Max(3, size * 0.45));

        if (IsReturnHomeText(markerTextLower))
        {
            DrawReturnHomeMarker(context, point, size, rim);
            if (IsQuestionableMobyMarker(moby))
                DrawNeedsIdBadge(context, point, size);
            return;
        }

        switch (moby.VisualKind)
        {
            case MobyVisualKind.Gem:
                DrawGemMarker(context, point, size, color, rim, moby.Gem);
                break;
            case MobyVisualKind.Key:
                DrawKeyMarker(context, point, size, rim);
                break;
            case MobyVisualKind.Chest:
                DrawChestMarker(context, point, size, fill, rim, GetMobyStamp(moby), markerText, moby.RewardGem);
                break;
            case MobyVisualKind.Dragon:
                DrawDragonStatueMarker(context, point, size, rim);
                break;
            case MobyVisualKind.Actor:
                DrawActorMarker(context, point, size, fill, rim, GetMobyStamp(moby), markerText);
                break;
            case MobyVisualKind.FlightTarget:
                DrawFlightTargetMarker(context, point, size, fill, rim, GetMobyStamp(moby), markerText);
                break;
            case MobyVisualKind.Portal:
                context.DrawEllipse(null, rim, point, size, size * 1.35);
                context.DrawEllipse(fill, null, point, size * 0.42, size * 0.72);
                DrawMobyStamp(context, point, GetMobyStamp(moby), size);
                break;
            case MobyVisualKind.Scenery:
                DrawSceneryMarker(context, point, size, fill, rim, GetMobyStamp(moby));
                break;
            case MobyVisualKind.Whirlwind:
                context.DrawEllipse(null, rim, point, size * 0.95, size * 0.95);
                context.DrawLine(rim, new Point(point.X, point.Y - size), new Point(point.X + (size * 0.55), point.Y - (size * 1.45)));
                context.DrawLine(rim, new Point(point.X - (size * 0.62), point.Y + (size * 0.18)), new Point(point.X + (size * 0.46), point.Y + (size * 0.18)));
                DrawMobyStamp(context, point, GetMobyStamp(moby), size);
                break;
            case MobyVisualKind.Control:
                context.DrawRectangle(fill, rim, new Rect(point.X - (size * 0.82), point.Y - (size * 0.82), size * 1.64, size * 1.64), 1, 1);
                context.DrawLine(rim, new Point(point.X - (size * 0.55), point.Y), new Point(point.X + (size * 0.55), point.Y));
                context.DrawLine(rim, new Point(point.X, point.Y - (size * 0.55)), new Point(point.X, point.Y + (size * 0.55)));
                DrawMobyStamp(context, point, GetMobyStamp(moby), size);
                break;
            default:
                context.DrawEllipse(fill, rim, point, size, size);
                DrawTinyQuestionMark(context, point, size);
                break;
        }

        if (IsQuestionableMobyMarker(moby))
            DrawNeedsIdBadge(context, point, size);
    }

    internal static void DrawMobyMarkerPreview(
        DrawingContext context,
        Point point,
        Moby moby,
        double size) =>
        DrawMoby(context, point, moby, selected: false, linked: false, size);

    private static bool IsQuestionableMobyMarker(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
        return text.Contains("?") ||
            moby.VisualKind == MobyVisualKind.Unknown ||
            text.Contains("unknown") ||
            text.Contains("candidate") ||
            text.Contains("pattern-inferred") ||
            text.Contains("needs live") ||
            text.Contains("needs validation") ||
            text.Contains("not confirmed");
    }

    private static void DrawNeedsIdBadge(DrawingContext context, Point point, double size)
    {
        if (size < 6.2)
            return;

        double radius = Math.Clamp(size * 0.32, 3.5, 6.5);
        Point badgePoint = new(point.X + (size * 0.62), point.Y - (size * 0.62));
        IBrush fill = new SolidColorBrush(Color.FromRgb(255, 217, 86));
        Pen rim = new(new SolidColorBrush(Color.FromArgb(230, 40, 35, 24)), Math.Clamp(size * 0.1, 1, 1.7));
        context.DrawEllipse(fill, rim, badgePoint, radius, radius);

        FormattedText text = new(
            "?",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Clamp(size * 0.5, 6, 10),
            new SolidColorBrush(Color.FromRgb(35, 30, 20)));
        context.DrawText(text, new Point(badgePoint.X - (text.Width * 0.5), badgePoint.Y - (text.Height * 0.58)));
    }

    private static void DrawDiamond(DrawingContext context, Point point, double size, IBrush fill, Pen rim)
    {
        DrawPolygon(context, new[]
        {
            new Point(point.X, point.Y - (size * 1.25)),
            new Point(point.X + size, point.Y),
            new Point(point.X, point.Y + (size * 1.25)),
            new Point(point.X - size, point.Y)
        }, fill, rim);
    }

    private static void DrawGemMarker(DrawingContext context, Point point, double size, Color color, Pen rim, GemValue gem)
    {
        if (MobyGemIconCatalog.TryDraw(context, point, size, rim, gem))
            return;

        color = GemMarkerColor(color, gem);
        DrawSpyroDiamondGem(context, point, size * 0.72, color, rim);
    }

    private static double MobyHitRadius(Moby moby, double markerSize)
    {
        return moby.VisualKind == MobyVisualKind.Gem
            ? MobyGemIconCatalog.HitRadius(markerSize, moby.Gem)
            : 12;
    }

    private static Color GemMarkerColor(Color fallback, GemValue gem)
    {
        return gem == GemValue.Unknown
            ? fallback
            : Color.FromArgb(gem.Color.A, gem.Color.R, gem.Color.G, gem.Color.B);
    }

    private static void DrawSpyroDiamondGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        double angle = -0.08;
        double width = 0.95;
        double height = 1.05;
        Color top = BlendColor(color, Colors.White, 0.42);
        Color bright = BlendColor(color, Colors.White, 0.68);
        Color face = BlendColor(color, Colors.White, 0.1);
        Color shadow = AdjustColor(color, 0.58);
        Color deep = BlendColor(AdjustColor(color, 0.4), Colors.Black, 0.26);
        Color darkest = BlendColor(AdjustColor(color, 0.32), Colors.Black, 0.42);

        Point topPoint = GemPoint(point, size, width, height, angle, 0.0, -1.22);
        Point upperLeft = GemPoint(point, size, width, height, angle, -0.74, -0.48);
        Point left = GemPoint(point, size, width, height, angle, -1.08, 0.06);
        Point lowerLeft = GemPoint(point, size, width, height, angle, -0.54, 0.7);
        Point bottom = GemPoint(point, size, width, height, angle, 0.0, 1.12);
        Point lowerRight = GemPoint(point, size, width, height, angle, 0.62, 0.66);
        Point right = GemPoint(point, size, width, height, angle, 1.08, 0.02);
        Point upperRight = GemPoint(point, size, width, height, angle, 0.72, -0.52);
        Point center = GemPoint(point, size, width, height, angle, 0.0, -0.02);
        Point centerLeft = GemPoint(point, size, width, height, angle, -0.42, -0.02);
        Point centerRight = GemPoint(point, size, width, height, angle, 0.46, -0.04);
        Point[] outline = [topPoint, upperRight, right, lowerRight, bottom, lowerLeft, left, upperLeft];

        DrawGemBackFace(context, outline, size * 0.85);
        Pen outlinePen = new(new SolidColorBrush(Color.FromArgb(210, 17, 19, 24)), Math.Max(0.9, size * 0.11));
        DrawPolygon(context, outline, new SolidColorBrush(face), outlinePen);
        DrawPolygon(context, [topPoint, upperLeft, center], new SolidColorBrush(bright), null);
        DrawPolygon(context, [topPoint, center, upperRight], new SolidColorBrush(top), null);
        DrawPolygon(context, [upperRight, right, centerRight, center], new SolidColorBrush(deep), null);
        DrawPolygon(context, [right, lowerRight, centerRight], new SolidColorBrush(darkest), null);
        DrawPolygon(context, [centerRight, lowerRight, bottom, center], new SolidColorBrush(deep), null);
        DrawPolygon(context, [center, bottom, lowerLeft, centerLeft], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [left, centerLeft, lowerLeft], new SolidColorBrush(AdjustColor(color, 0.52)), null);
        DrawPolygon(context, [upperLeft, left, centerLeft, center], new SolidColorBrush(BlendColor(color, Colors.White, 0.2)), null);

        Pen lightFacet = new(new SolidColorBrush(Color.FromArgb(118, 255, 255, 245)), Math.Max(0.55, size * 0.045));
        Pen darkFacet = new(new SolidColorBrush(Color.FromArgb(90, 18, 20, 28)), Math.Max(0.55, size * 0.045));
        context.DrawLine(lightFacet, upperLeft, center);
        context.DrawLine(lightFacet, topPoint, center);
        context.DrawLine(darkFacet, centerRight, lowerRight);
        context.DrawLine(darkFacet, center, bottom);

        DrawPolygon(
            context,
            [
                GemPoint(point, size, width, height, angle, -0.42, -0.42),
                GemPoint(point, size, width, height, angle, -0.02, -0.62),
                GemPoint(point, size, width, height, angle, -0.13, -0.18)
            ],
            new SolidColorBrush(Color.FromArgb(190, 255, 255, 245)),
            null);

        if (rim.Thickness > 1.8)
            DrawPolygon(context, outline, new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), rim);
    }

    private static void DrawSpyroFlatGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        double width = 1.28;
        double height = 0.58;
        double angle = -0.06;
        Color top = BlendColor(color, Colors.White, 0.28);
        Color face = BlendColor(color, Colors.White, 0.08);
        Color bright = BlendColor(color, Colors.White, 0.62);
        Color shadow = AdjustColor(color, 0.58);
        Color deep = BlendColor(AdjustColor(color, 0.42), Colors.Black, 0.28);
        Color darkest = BlendColor(AdjustColor(color, 0.34), Colors.Black, 0.42);

        Point left = GemPoint(point, size, width, height, angle, -1.16, 0.0);
        Point topLeft = GemPoint(point, size, width, height, angle, -0.66, -0.58);
        Point topMid = GemPoint(point, size, width, height, angle, -0.02, -0.72);
        Point topRight = GemPoint(point, size, width, height, angle, 0.74, -0.54);
        Point right = GemPoint(point, size, width, height, angle, 1.18, -0.02);
        Point bottomRight = GemPoint(point, size, width, height, angle, 0.56, 0.56);
        Point bottom = GemPoint(point, size, width, height, angle, -0.08, 0.72);
        Point bottomLeft = GemPoint(point, size, width, height, angle, -0.72, 0.46);
        Point center = GemPoint(point, size, width, height, angle, -0.02, -0.03);
        Point centerRight = GemPoint(point, size, width, height, angle, 0.48, -0.02);
        Point centerLeft = GemPoint(point, size, width, height, angle, -0.48, -0.02);
        Point[] outline = [left, topLeft, topMid, topRight, right, bottomRight, bottom, bottomLeft];

        DrawGemBackFace(context, outline, size * 0.72);
        Pen outlinePen = new(new SolidColorBrush(Color.FromArgb(210, 19, 20, 28)), Math.Max(0.9, size * 0.1));
        DrawPolygon(context, outline, new SolidColorBrush(face), outlinePen);
        DrawPolygon(context, [left, topLeft, centerLeft], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [topLeft, topMid, center], new SolidColorBrush(top), null);
        DrawPolygon(context, [topMid, topRight, centerRight, center], new SolidColorBrush(BlendColor(top, Colors.White, 0.16)), null);
        DrawPolygon(context, [topRight, right, centerRight], new SolidColorBrush(deep), null);
        DrawPolygon(context, [right, bottomRight, centerRight], new SolidColorBrush(darkest), null);
        DrawPolygon(context, [centerRight, bottomRight, bottom, center], new SolidColorBrush(deep), null);
        DrawPolygon(context, [center, bottom, bottomLeft, centerLeft], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [left, centerLeft, bottomLeft], new SolidColorBrush(AdjustColor(color, 0.52)), null);
        DrawPolygon(context, [centerLeft, topLeft, center], new SolidColorBrush(bright), null);

        Pen lightFacet = new(new SolidColorBrush(Color.FromArgb(96, 255, 255, 245)), Math.Max(0.55, size * 0.04));
        Pen darkFacet = new(new SolidColorBrush(Color.FromArgb(82, 18, 20, 28)), Math.Max(0.55, size * 0.04));
        context.DrawLine(lightFacet, topLeft, centerLeft);
        context.DrawLine(lightFacet, topMid, center);
        context.DrawLine(darkFacet, centerRight, bottomRight);
        context.DrawLine(darkFacet, center, bottom);

        DrawPolygon(
            context,
            [
                GemPoint(point, size, width, height, angle, -0.52, -0.42),
                GemPoint(point, size, width, height, angle, -0.12, -0.48),
                GemPoint(point, size, width, height, angle, -0.28, -0.14)
            ],
            new SolidColorBrush(Color.FromArgb(185, 255, 255, 245)),
            null);

        if (rim.Thickness > 1.8)
            DrawPolygon(context, outline, new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), rim);
    }

    private static void DrawSpyroRedGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        DrawSpyroShardGem(context, point, size, 1.34, 0.76, -0.22, color, rim);

        Color sparkle = BlendColor(color, Colors.White, 0.76);
        DrawPolygon(
            context,
            [
                GemPoint(point, size, 1.34, 0.76, -0.22, -0.22, -0.78),
                GemPoint(point, size, 1.34, 0.76, -0.22, 0.18, -0.58),
                GemPoint(point, size, 1.34, 0.76, -0.22, -0.12, -0.3)
            ],
            new SolidColorBrush(Color.FromArgb(190, sparkle.R, sparkle.G, sparkle.B)),
            null);
    }

    private static void DrawSpyroGreenGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        Color highlight = BlendColor(color, Colors.White, 0.62);
        Color bright = BlendColor(color, Colors.White, 0.24);
        Color shadow = AdjustColor(color, 0.58);
        Color deep = AdjustColor(color, 0.38);
        double angle = 0.2;
        double width = 1.42;
        double height = 0.78;

        Point left = GemPoint(point, size, width, height, angle, -1.02, 0.0);
        Point top = GemPoint(point, size, width, height, angle, -0.16, -0.76);
        Point rightTop = GemPoint(point, size, width, height, angle, 0.9, -0.38);
        Point right = GemPoint(point, size, width, height, angle, 1.12, 0.22);
        Point bottom = GemPoint(point, size, width, height, angle, 0.0, 0.72);
        Point center = GemPoint(point, size, width, height, angle, 0.08, -0.02);
        Point[] outline = [left, top, rightTop, right, bottom];

        DrawGemBase(context, outline, color, size);
        DrawPolygon(context, [left, top, center], new SolidColorBrush(highlight), null);
        DrawPolygon(context, [top, rightTop, center], new SolidColorBrush(bright), null);
        DrawPolygon(context, [rightTop, right, center], new SolidColorBrush(deep), null);
        DrawPolygon(context, [right, bottom, center], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [bottom, left, center], new SolidColorBrush(AdjustColor(color, 0.72)), null);
        DrawGemFacetLines(context, [top, center, bottom], [left, center, rightTop], size);
        DrawSelectedGemRim(context, outline, rim);
    }

    private static void DrawSpyroBlueGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        DrawSpyroShardGem(context, point, size, 1.82, 0.58, -0.28, color, rim);
        Color pale = BlendColor(color, Colors.White, 0.7);
        Pen glint = new(new SolidColorBrush(Color.FromArgb(185, pale.R, pale.G, pale.B)), Math.Max(0.75, size * 0.055));
        context.DrawLine(
            glint,
            GemPoint(point, size, 1.82, 0.58, -0.28, -0.58, -0.48),
            GemPoint(point, size, 1.82, 0.58, -0.28, 0.5, -0.38));
    }

    private static void DrawSpyroYellowGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        Color highlight = BlendColor(color, Colors.White, 0.7);
        Color bright = BlendColor(color, Colors.White, 0.3);
        Color shadow = AdjustColor(color, 0.68);
        Color deep = AdjustColor(color, 0.46);
        double angle = 0.1;
        double width = 1.0;
        double height = 1.24;

        Point top = GemPoint(point, size, width, height, angle, 0.0, -1.08);
        Point rightTop = GemPoint(point, size, width, height, angle, 0.82, -0.24);
        Point rightBottom = GemPoint(point, size, width, height, angle, 0.46, 0.78);
        Point bottom = GemPoint(point, size, width, height, angle, -0.04, 1.06);
        Point leftBottom = GemPoint(point, size, width, height, angle, -0.72, 0.38);
        Point leftTop = GemPoint(point, size, width, height, angle, -0.62, -0.46);
        Point center = GemPoint(point, size, width, height, angle, 0.0, -0.02);
        Point[] outline = [top, rightTop, rightBottom, bottom, leftBottom, leftTop];

        DrawGemBase(context, outline, color, size);
        DrawPolygon(context, [top, leftTop, center], new SolidColorBrush(highlight), null);
        DrawPolygon(context, [top, rightTop, center], new SolidColorBrush(bright), null);
        DrawPolygon(context, [rightTop, rightBottom, center], new SolidColorBrush(AdjustColor(color, 0.82)), null);
        DrawPolygon(context, [rightBottom, bottom, center], new SolidColorBrush(deep), null);
        DrawPolygon(context, [bottom, leftBottom, center], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [leftBottom, leftTop, center], new SolidColorBrush(BlendColor(color, Colors.White, 0.12)), null);
        DrawGemFacetLines(context, [top, center, bottom], [leftTop, center, rightBottom], size);
        DrawSelectedGemRim(context, outline, rim);
    }

    private static void DrawSpyroPurpleGem(DrawingContext context, Point point, double size, Color color, Pen rim)
    {
        DrawSpyroShardGem(context, point, size, 1.66, 0.86, 0.24, color, rim);
        Color warmHighlight = BlendColor(BlendColor(color, Colors.White, 0.48), Color.FromRgb(244, 208, 63), 0.18);
        DrawPolygon(
            context,
            [
                GemPoint(point, size, 1.66, 0.86, 0.24, -0.72, -0.08),
                GemPoint(point, size, 1.66, 0.86, 0.24, -0.2, -0.52),
                GemPoint(point, size, 1.66, 0.86, 0.24, -0.06, -0.1)
            ],
            new SolidColorBrush(Color.FromArgb(170, warmHighlight.R, warmHighlight.G, warmHighlight.B)),
            null);
    }

    private static void DrawSpyroShardGem(DrawingContext context, Point point, double size, double width, double height, double angle, Color color, Pen rim)
    {
        Color highlight = BlendColor(color, Colors.White, 0.64);
        Color bright = BlendColor(color, Colors.White, 0.28);
        Color mid = BlendColor(color, Colors.White, 0.1);
        Color shadow = AdjustColor(color, 0.66);
        Color deep = AdjustColor(color, 0.43);
        Point leftTip = GemPoint(point, size, width, height, angle, -1.16, -0.02);
        Point topLeft = GemPoint(point, size, width, height, angle, -0.5, -0.64);
        Point topRight = GemPoint(point, size, width, height, angle, 0.42, -0.68);
        Point rightTip = GemPoint(point, size, width, height, angle, 1.2, -0.18);
        Point rightLower = GemPoint(point, size, width, height, angle, 0.82, 0.36);
        Point bottom = GemPoint(point, size, width, height, angle, -0.02, 0.68);
        Point leftLower = GemPoint(point, size, width, height, angle, -0.86, 0.42);
        Point centerLeft = GemPoint(point, size, width, height, angle, -0.22, -0.06);
        Point centerRight = GemPoint(point, size, width, height, angle, 0.42, -0.04);

        Point[] outline = [leftTip, topLeft, topRight, rightTip, rightLower, bottom, leftLower];
        Pen outlinePen = new(new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)), Math.Max(1.0, size * 0.105));
        Color blackCut = BlendColor(deep, Colors.Black, 0.42);

        DrawGemBackFace(context, outline, size);
        DrawPolygon(context, outline, new SolidColorBrush(mid), outlinePen);
        DrawPolygon(context, [leftTip, topLeft, centerLeft], new SolidColorBrush(highlight), null);
        DrawPolygon(context, [topLeft, topRight, centerRight, centerLeft], new SolidColorBrush(bright), null);
        DrawPolygon(context, [topRight, rightTip, centerRight], new SolidColorBrush(blackCut), null);
        DrawPolygon(context, [rightTip, rightLower, centerRight], new SolidColorBrush(deep), null);
        DrawPolygon(context, [centerRight, rightLower, bottom], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [centerLeft, centerRight, bottom, leftLower], new SolidColorBrush(mid), null);
        DrawPolygon(context, [leftTip, centerLeft, leftLower], new SolidColorBrush(shadow), null);
        DrawPolygon(context, [centerLeft, bottom, leftLower], new SolidColorBrush(deep), null);
        DrawPolygon(context, [topLeft, centerLeft, centerRight], new SolidColorBrush(BlendColor(highlight, Colors.White, 0.22)), null);

        Pen lightFacet = new(new SolidColorBrush(Color.FromArgb(122, 255, 255, 255)), Math.Max(0.7, size * 0.045));
        Pen darkFacet = new(new SolidColorBrush(Color.FromArgb(96, 20, 22, 28)), Math.Max(0.7, size * 0.045));
        context.DrawLine(lightFacet, topLeft, centerLeft);
        context.DrawLine(lightFacet, topRight, centerRight);
        context.DrawLine(darkFacet, centerRight, bottom);
        context.DrawLine(darkFacet, centerLeft, leftLower);

        DrawPolygon(
            context,
            [
                GemPoint(point, size, width, height, angle, -0.52, -0.35),
                GemPoint(point, size, width, height, angle, -0.16, -0.45),
                GemPoint(point, size, width, height, angle, -0.28, -0.18)
            ],
            new SolidColorBrush(Color.FromArgb(210, 255, 255, 232)),
            null);

        if (rim.Thickness > 1.8)
            DrawPolygon(context, outline, new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), rim);
    }

    private static void DrawGemBase(DrawingContext context, Point[] outline, Color color, double size)
    {
        Pen outlinePen = new(new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)), Math.Max(1.0, size * 0.105));
        DrawGemBackFace(context, outline, size);
        DrawPolygon(context, outline, new SolidColorBrush(BlendColor(color, Colors.White, 0.08)), outlinePen);
    }

    private static void DrawGemBackFace(DrawingContext context, Point[] outline, double size)
    {
        Point[] back = OffsetPoints(outline, size * 0.12, size * 0.18);
        DrawPolygon(context, back, new SolidColorBrush(Color.FromArgb(150, 7, 9, 14)), null);
    }

    private static Point[] OffsetPoints(Point[] points, double x, double y)
    {
        Point[] shifted = new Point[points.Length];
        for (int i = 0; i < points.Length; i++)
            shifted[i] = new Point(points[i].X + x, points[i].Y + y);
        return shifted;
    }

    private static void DrawGemFacetLines(DrawingContext context, Point[] lightPath, Point[] darkPath, double size)
    {
        Pen lightFacet = new(new SolidColorBrush(Color.FromArgb(118, 255, 255, 245)), Math.Max(0.7, size * 0.042));
        Pen darkFacet = new(new SolidColorBrush(Color.FromArgb(96, 19, 22, 28)), Math.Max(0.7, size * 0.042));
        for (int i = 1; i < lightPath.Length; i++)
            context.DrawLine(lightFacet, lightPath[i - 1], lightPath[i]);
        for (int i = 1; i < darkPath.Length; i++)
            context.DrawLine(darkFacet, darkPath[i - 1], darkPath[i]);
    }

    private static void DrawSelectedGemRim(DrawingContext context, Point[] outline, Pen rim)
    {
        if (rim.Thickness > 1.8)
            DrawPolygon(context, outline, new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), rim);
    }

    private static Point GemPoint(Point origin, double size, double width, double height, double angle, double x, double y)
    {
        double px = x * size * width;
        double py = y * size * height;
        double sin = Math.Sin(angle);
        double cos = Math.Cos(angle);
        return new Point(origin.X + (px * cos) - (py * sin), origin.Y + (px * sin) + (py * cos));
    }

    private static void DrawTriangle(DrawingContext context, Point point, double size, IBrush fill, Pen rim)
    {
        DrawPolygon(context, new[]
        {
            new Point(point.X, point.Y - (size * 1.2)),
            new Point(point.X + (size * 1.12), point.Y + size),
            new Point(point.X - (size * 1.12), point.Y + size)
        }, fill, rim);
    }

    private static void DrawKeyMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(225, 121, 78, 8)), Math.Max(1.15, size * 0.18));
        Pen gold = new(new SolidColorBrush(Color.FromRgb(255, 218, 50)), Math.Max(0.9, size * 0.125));
        Pen highlight = new(new SolidColorBrush(Color.FromArgb(205, 255, 250, 152)), Math.Max(0.6, size * 0.055));
        IBrush goldFill = new SolidColorBrush(Color.FromRgb(255, 205, 28));
        IBrush brightFill = new SolidColorBrush(Color.FromRgb(255, 239, 98));
        IBrush darkGold = new SolidColorBrush(Color.FromRgb(194, 131, 10));

        context.DrawLine(outline, P(-0.42, -0.08), P(1.06, 0.38));
        context.DrawLine(gold, P(-0.42, -0.08), P(1.06, 0.38));
        context.DrawEllipse(goldFill, outline, P(-0.58, -0.2), size * 0.48, size * 0.38);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(230, 43, 36, 18)), null, P(-0.58, -0.2), size * 0.22, size * 0.16);
        context.DrawEllipse(brightFill, null, P(-0.72, -0.28), size * 0.12, size * 0.1);
        DrawPolygon(context, new[] { P(0.72, 0.26), P(1.18, 0.4), P(0.9, 0.58), P(0.66, 0.44) }, goldFill, outline);
        DrawPolygon(context, new[] { P(0.9, 0.32), P(1.25, 0.42), P(1.05, 0.22) }, brightFill, outline);
        DrawPolygon(context, new[] { P(0.7, 0.2), P(0.9, 0.28), P(0.82, 0.5), P(0.58, 0.42) }, darkGold, null);
        context.DrawLine(highlight, P(-0.28, -0.02), P(0.74, 0.28));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.28, size * 1.05);
    }

    private static void DrawDragonStatueMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        static Point P(Point origin, double scale, double x, double y) =>
            new(origin.X + (scale * x), origin.Y + (scale * y));

        size *= 0.78;
        Color crystalGlow = Color.FromRgb(139, 241, 255);
        Color crystalLight = Color.FromRgb(95, 219, 246);
        Color crystalMid = Color.FromRgb(24, 166, 219);
        Color crystalBlue = Color.FromRgb(15, 111, 193);
        Color crystalDark = Color.FromRgb(10, 55, 126);
        Color crystalDeep = Color.FromRgb(4, 32, 82);
        Color pedestalTop = Color.FromRgb(135, 211, 219);
        Color pedestalSide = Color.FromRgb(102, 166, 190);
        Color pedestalFront = Color.FromRgb(72, 126, 160);
        Color horn = Color.FromRgb(228, 247, 255);
        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 5, 35, 82)), Math.Max(0.62, size * 0.07));
        Pen facetPen = new(new SolidColorBrush(Color.FromArgb(140, 226, 252, 255)), Math.Max(0.34, size * 0.032));
        Pen darkFacetPen = new(new SolidColorBrush(Color.FromArgb(118, 2, 38, 93)), Math.Max(0.32, size * 0.028));
        Pen tailPen = new(new SolidColorBrush(crystalBlue), Math.Max(0.75, size * 0.11));
        Pen tailLoopPen = new(new SolidColorBrush(Color.FromArgb(230, 22, 132, 197)), Math.Max(0.62, size * 0.075));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(78, 0, 0, 0)), null,
            P(point, size, 0.02, 0.88), size * 0.96, size * 0.23);

        DrawPolygon(context, new[]
        {
            P(point, size, -0.93, 0.5),
            P(point, size, -0.52, 0.8),
            P(point, size, 0.58, 0.8),
            P(point, size, 0.96, 0.51),
            P(point, size, 0.5, 0.31),
            P(point, size, -0.48, 0.31)
        }, new SolidColorBrush(pedestalSide), outline);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.72, 0.36),
            P(point, size, -0.35, 0.54),
            P(point, size, 0.43, 0.54),
            P(point, size, 0.74, 0.36),
            P(point, size, 0.35, 0.19),
            P(point, size, -0.47, 0.2)
        }, new SolidColorBrush(pedestalTop), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.35, 0.54),
            P(point, size, 0.43, 0.54),
            P(point, size, 0.58, 0.78),
            P(point, size, -0.43, 0.79)
        }, new SolidColorBrush(pedestalFront), null);

        // Big faceted crystal wing, matching the in-game dragon statue silhouette.
        DrawPolygon(context, new[]
        {
            P(point, size, -0.04, -0.42),
            P(point, size, -1.36, -1.5),
            P(point, size, -0.94, -0.34),
            P(point, size, -1.35, 0.0),
            P(point, size, -0.68, 0.15),
            P(point, size, -0.18, 0.42),
            P(point, size, 0.18, -0.06),
            P(point, size, 0.72, -0.26),
            P(point, size, 0.34, -0.64),
            P(point, size, 0.64, -1.02)
        }, new SolidColorBrush(crystalMid), outline);
        DrawPolygon(context, new[]
        {
            P(point, size, -1.36, -1.5),
            P(point, size, -0.94, -0.34),
            P(point, size, -0.04, -0.42)
        }, new SolidColorBrush(crystalLight), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -1.36, -1.5),
            P(point, size, -0.18, -0.94),
            P(point, size, 0.64, -1.02),
            P(point, size, 0.34, -0.64),
            P(point, size, -0.04, -0.42)
        }, new SolidColorBrush(crystalBlue), null);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.34, -0.64),
            P(point, size, 0.72, -0.26),
            P(point, size, 0.18, -0.06),
            P(point, size, -0.04, -0.42)
        }, new SolidColorBrush(crystalDeep), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.94, -0.34),
            P(point, size, -0.44, -0.1),
            P(point, size, -0.16, 0.38),
            P(point, size, -0.68, 0.15)
        }, new SolidColorBrush(BlendColor(crystalMid, Colors.White, 0.18)), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.86, -0.28),
            P(point, size, -0.34, -0.38),
            P(point, size, -0.68, -0.02)
        }, new SolidColorBrush(BlendColor(crystalGlow, Colors.White, 0.16)), null);
        context.DrawLine(facetPen, P(point, size, -1.04, -1.17), P(point, size, 0.04, -0.22));
        context.DrawLine(facetPen, P(point, size, -0.42, -0.78), P(point, size, 0.54, -0.86));
        context.DrawLine(darkFacetPen, P(point, size, -0.94, -0.34), P(point, size, -0.2, 0.32));
        context.DrawLine(darkFacetPen, P(point, size, 0.18, -0.06), P(point, size, 0.62, -0.26));

        context.DrawEllipse(null, tailLoopPen, P(point, size, -0.17, 0.35), size * 0.36, size * 0.45);
        context.DrawLine(tailPen, P(point, size, 0.02, 0.36), P(point, size, -0.32, 0.5));
        context.DrawLine(tailPen, P(point, size, -0.32, 0.5), P(point, size, -0.62, 0.34));
        context.DrawLine(tailPen, P(point, size, -0.62, 0.34), P(point, size, -0.48, 0.08));
        context.DrawLine(new Pen(new SolidColorBrush(crystalGlow), Math.Max(0.34, size * 0.04)),
            P(point, size, -0.37, 0.46),
            P(point, size, -0.57, 0.31));

        DrawPolygon(context, new[]
        {
            P(point, size, -0.17, 0.35),
            P(point, size, 0.02, -0.15),
            P(point, size, 0.36, -0.02),
            P(point, size, 0.32, 0.38),
            P(point, size, 0.05, 0.54)
        }, new SolidColorBrush(crystalMid), outline);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.13, 0.37),
            P(point, size, 0.38, -0.02),
            P(point, size, 0.48, 0.27),
            P(point, size, 0.25, 0.42)
        }, new SolidColorBrush(crystalLight), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.04, 0.35),
            P(point, size, 0.08, -0.03),
            P(point, size, 0.2, 0.36)
        }, new SolidColorBrush(BlendColor(crystalMid, Colors.White, 0.14)), null);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.09, 0.5),
            P(point, size, 0.25, 0.56),
            P(point, size, 0.2, 0.18)
        }, new SolidColorBrush(crystalDeep), null);
        DrawPolygon(context, new[]
        {
            P(point, size, -0.12, 0.5),
            P(point, size, 0.01, 0.56),
            P(point, size, 0.04, 0.21)
        }, new SolidColorBrush(crystalDark), null);

        DrawPolygon(context, new[]
        {
            P(point, size, 0.18, -0.02),
            P(point, size, 0.34, -0.66),
            P(point, size, 0.54, -0.46),
            P(point, size, 0.42, 0.08)
        }, new SolidColorBrush(crystalLight), outline);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.35, -0.64),
            P(point, size, 0.49, -0.44),
            P(point, size, 0.31, -0.04),
            P(point, size, 0.19, -0.02)
        }, new SolidColorBrush(BlendColor(crystalGlow, Colors.White, 0.18)), null);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.43, -0.58),
            P(point, size, 1.05, -0.49),
            P(point, size, 0.7, -0.21),
            P(point, size, 0.41, -0.22)
        }, new SolidColorBrush(crystalMid), outline);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.7, -0.21),
            P(point, size, 1.05, -0.49),
            P(point, size, 0.78, -0.14)
        }, new SolidColorBrush(crystalDeep), null);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.75, -0.48),
            P(point, size, 1.17, -0.62),
            P(point, size, 0.87, -0.31)
        }, new SolidColorBrush(horn), null);
        DrawPolygon(context, new[]
        {
            P(point, size, 0.44, -0.43),
            P(point, size, 0.56, -0.35),
            P(point, size, 0.43, -0.31)
        }, new SolidColorBrush(Color.FromArgb(175, 4, 39, 100)), null);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(205, 2, 42, 104)), null,
            P(point, size, 0.58, -0.39), size * 0.035, size * 0.028);
        context.DrawLine(darkFacetPen, P(point, size, 0.33, -0.5), P(point, size, 0.48, 0.06));
        context.DrawLine(facetPen, P(point, size, 0.18, -0.02), P(point, size, 0.28, 0.36));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.98, size * 1.05);
    }

    private static void DrawChestMarker(DrawingContext context, Point point, double size, IBrush fill, Pen rim, string stamp, string text, GemValue rewardGem)
    {
        string lower = text.ToLowerInvariant();
        if (IsFlameChargeChestText(lower))
        {
            DrawFlameChargeChestMarker(context, point, size, rim);
            return;
        }

        if (IsChargeChestText(lower))
        {
            DrawChargeOnlyChestMarker(context, point, size, rim);
            return;
        }

        if (IsLockedChestText(lower))
        {
            DrawLockedChestMarker(context, point, size, rim);
            return;
        }

        if (IsLifeChestText(lower))
        {
            DrawLifeChestMarker(context, point, size, rim);
            return;
        }

        if (IsSpringChestText(lower))
        {
            DrawSpringChestMarker(context, point, size, rim, rewardGem);
            return;
        }

        if (IsFireworkChestText(lower))
        {
            DrawFireworkChestMarker(context, point, size, rim);
            return;
        }

        if (IsFlameSpinChestText(lower))
        {
            DrawFlameSpinChestMarker(context, point, size, rim);
            return;
        }

        Rect body = new(point.X - size, point.Y - (size * 0.66), size * 2, size * 1.32);
        Rect lid = new(point.X - (size * 0.92), point.Y - (size * 0.98), size * 1.84, size * 0.62);
        IBrush bodyFill = lower.Contains("key chest") || lower.Contains("locked") || lower.Contains("unlock")
            ? new SolidColorBrush(Color.FromRgb(196, 111, 34))
            : lower.Contains("spring")
                ? new SolidColorBrush(Color.FromRgb(78, 185, 96))
                : fill;
        IBrush lidFill = lower.Contains("spring")
            ? new SolidColorBrush(Color.FromRgb(61, 142, 66))
            : lower.Contains("metal")
                ? new SolidColorBrush(Color.FromRgb(176, 184, 192))
                : new SolidColorBrush(Color.FromArgb(220, 121, 78, 43));

        context.DrawRectangle(bodyFill, rim, body, 2, 2);
        context.DrawRectangle(lidFill, rim, lid, 2, 2);
        context.DrawLine(rim, new Point(point.X - size, point.Y), new Point(point.X + size, point.Y));

        if (lower.Contains("key chest") || lower.Contains("locked") || lower.Contains("unlock"))
        {
            Pen gold = new(new SolidColorBrush(Color.FromRgb(255, 222, 88)), Math.Max(1.1, size * 0.13));
            context.DrawEllipse(null, gold, new Point(point.X, point.Y - (size * 0.06)), size * 0.44, size * 0.35);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(230, 48, 42, 34)), gold, new Rect(point.X - (size * 0.33), point.Y + (size * 0.08), size * 0.66, size * 0.44), 1, 1);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 222, 88)), null, new Point(point.X, point.Y + (size * 0.24)), size * 0.08, size * 0.08);
        }
        else if (lower.Contains("spring"))
        {
            Pen spring = new(new SolidColorBrush(Color.FromRgb(229, 116, 218)), Math.Max(1.1, size * 0.12));
            for (int i = 0; i < 4; i++)
            {
                double y = point.Y - (size * 0.12) + (i * size * 0.18);
                context.DrawLine(spring, new Point(point.X - (size * 0.55), y), new Point(point.X + (size * 0.55), y + (size * 0.12)));
            }
        }
        else if (lower.Contains("life"))
        {
            Pen mark = new(new SolidColorBrush(Color.FromArgb(230, 255, 236, 127)), Math.Max(1.2, size * 0.18));
            context.DrawLine(mark, new Point(point.X, point.Y - (size * 0.42)), new Point(point.X, point.Y + (size * 0.42)));
            context.DrawLine(mark, new Point(point.X - (size * 0.42), point.Y), new Point(point.X + (size * 0.42), point.Y));
        }
        else if (lower.Contains("metal"))
        {
            IBrush rivet = new SolidColorBrush(Color.FromArgb(220, 230, 237, 244));
            context.DrawEllipse(rivet, null, new Point(point.X - (size * 0.58), point.Y - (size * 0.32)), size * 0.13, size * 0.13);
            context.DrawEllipse(rivet, null, new Point(point.X + (size * 0.58), point.Y - (size * 0.32)), size * 0.13, size * 0.13);
            context.DrawEllipse(rivet, null, new Point(point.X - (size * 0.58), point.Y + (size * 0.34)), size * 0.13, size * 0.13);
            context.DrawEllipse(rivet, null, new Point(point.X + (size * 0.58), point.Y + (size * 0.34)), size * 0.13, size * 0.13);
        }
        else if (lower.Contains("charge"))
        {
            DrawPolygon(context, new[]
            {
                new Point(point.X - (size * 0.38), point.Y - (size * 0.42)),
                new Point(point.X + (size * 0.48), point.Y),
                new Point(point.X - (size * 0.38), point.Y + (size * 0.42))
            }, new SolidColorBrush(Color.FromArgb(150, 255, 236, 127)), rim);
        }
        else if (lower.Contains("firework"))
        {
            Pen fuse = new(new SolidColorBrush(Color.FromRgb(255, 236, 127)), Math.Max(1.0, size * 0.12));
            context.DrawLine(fuse, new Point(point.X + (size * 0.26), point.Y - (size * 0.72)), new Point(point.X + (size * 0.68), point.Y - (size * 1.16)));
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 236, 127)), null, new Point(point.X + (size * 0.76), point.Y - (size * 1.24)), size * 0.12, size * 0.12);
        }

        DrawMobyStamp(context, point, stamp, size);
    }

    private static bool IsFlameChargeChestText(string lower)
    {
        return lower.Contains("flame/charge") || lower.Contains("flame-or-charge") || lower.Contains("flame or charge");
    }

    private static bool IsChargeChestText(string lower)
    {
        return lower.Contains("charge chest") || lower.Contains("charge-only") || lower.Contains("charge only");
    }

    private static bool IsLockedChestText(string lower)
    {
        return lower.Contains("key chest") || lower.Contains("locked") || lower.Contains("unlock chest");
    }

    private static bool IsLifeChestText(string lower)
    {
        return lower.Contains("life chest") || lower.Contains("1up chest") || lower.Contains("1-up chest");
    }

    private static bool IsSpringChestText(string lower)
    {
        return lower.Contains("spring chest");
    }

    private static bool IsFlameSpinChestText(string lower)
    {
        return lower.Contains("flame spin") ||
            lower.Contains("flame-spin") ||
            lower.Contains("spin chest") ||
            lower.Contains("3x flame chest") ||
            lower.Contains("super flame chest");
    }

    private static bool IsFireworkChestText(string lower)
    {
        return lower.Contains("firework chest") || lower.Contains("firework");
    }

    private static bool IsReturnHomeText(string lower)
    {
        return lower.Contains("return-home") || lower.Contains("return home") || lower.Contains("home pad");
    }

    private static void DrawLockedChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(230, 22, 26, 32)), Math.Max(0.92, size * 0.09));
        Pen seam = new(new SolidColorBrush(Color.FromArgb(185, 87, 101, 119)), Math.Max(0.55, size * 0.045));
        Pen shackle = new(new SolidColorBrush(Color.FromRgb(248, 211, 60)), Math.Max(0.82, size * 0.076));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(88, 0, 0, 0));
        IBrush stone = new SolidColorBrush(Color.FromRgb(112, 122, 138));
        IBrush stoneLight = new SolidColorBrush(Color.FromRgb(155, 167, 186));
        IBrush stoneDark = new SolidColorBrush(Color.FromRgb(62, 69, 82));
        IBrush lidTop = new SolidColorBrush(Color.FromRgb(235, 244, 252));
        IBrush lidFront = new SolidColorBrush(Color.FromRgb(191, 205, 223));
        IBrush lockPlate = new SolidColorBrush(Color.FromRgb(226, 233, 243));
        IBrush keyhole = new SolidColorBrush(Color.FromRgb(17, 19, 25));
        IBrush foot = new SolidColorBrush(Color.FromRgb(16, 17, 21));

        context.DrawEllipse(shadow, null, P(0.0, 0.72), size * 1.15, size * 0.24);
        DrawPolygon(context, new[] { P(-0.92, -0.08), P(0.92, -0.08), P(0.82, 0.54), P(-0.82, 0.54) }, stone, outline);
        DrawPolygon(context, new[] { P(-0.92, -0.08), P(-0.68, 0.54), P(-0.82, 0.54) }, stoneLight, null);
        DrawPolygon(context, new[] { P(0.64, -0.08), P(0.92, -0.08), P(0.82, 0.54), P(0.58, 0.54) }, stoneDark, null);
        DrawPolygon(context, new[] { P(-0.98, -0.5), P(-0.72, -0.78), P(0.72, -0.78), P(0.98, -0.5), P(0.88, -0.08), P(-0.88, -0.08) }, lidTop, outline);
        DrawPolygon(context, new[] { P(-0.88, -0.08), P(0.88, -0.08), P(0.82, 0.08), P(-0.82, 0.08) }, lidFront, null);
        DrawPolygon(context, new[] { P(-0.74, 0.42), P(-0.48, 0.42), P(-0.54, 0.72), P(-0.84, 0.72) }, foot, null);
        DrawPolygon(context, new[] { P(0.48, 0.42), P(0.74, 0.42), P(0.84, 0.72), P(0.54, 0.72) }, foot, null);
        DrawPolygon(context, new[] { P(-0.2, -0.04), P(0.2, -0.04), P(0.18, 0.46), P(-0.18, 0.46) }, lockPlate, outline);
        context.DrawLine(shackle, P(-0.04, -0.06), P(0.04, -0.38));
        context.DrawLine(shackle, P(0.04, -0.38), P(0.26, -0.18));
        context.DrawLine(seam, P(-0.82, 0.08), P(0.82, 0.08));
        context.DrawLine(seam, P(-0.76, 0.28), P(0.76, 0.28));
        DrawPolygon(context, new[] { P(-0.05, 0.18), P(0.06, 0.18), P(0.0, 0.38) }, keyhole, null);
        context.DrawEllipse(keyhole, null, P(0.0, 0.15), size * 0.1, size * 0.1);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.18, size * 1.04);
    }

    private static void DrawLifeChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 34, 26, 45)), Math.Max(0.9, size * 0.09));
        Pen trim = new(new SolidColorBrush(Color.FromRgb(198, 157, 54)), Math.Max(0.75, size * 0.07));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(82, 0, 0, 0));
        IBrush purple = new SolidColorBrush(Color.FromRgb(78, 68, 122));
        IBrush purpleLight = new SolidColorBrush(Color.FromRgb(111, 96, 166));
        IBrush purpleDark = new SolidColorBrush(Color.FromRgb(46, 39, 78));
        IBrush black = new SolidColorBrush(Color.FromRgb(8, 8, 10));
        IBrush eye = new SolidColorBrush(Color.FromRgb(244, 247, 246));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(16, 18, 22));

        context.DrawEllipse(shadow, null, P(0.02, 0.68), size * 1.15, size * 0.24);
        DrawPolygon(context, new[] { P(-0.94, -0.02), P(-0.72, 0.48), P(-0.16, 0.66), P(0.78, 0.5), P(0.96, 0.08), P(0.58, -0.12), P(-0.62, -0.12) }, purple, outline);
        DrawPolygon(context, new[] { P(0.2, -0.08), P(0.96, 0.08), P(0.78, 0.5), P(0.08, 0.62) }, purpleDark, null);
        DrawPolygon(context, new[] { P(-0.94, -0.28), P(-0.62, -0.72), P(0.06, -0.9), P(0.76, -0.72), P(1.0, -0.28), P(0.58, -0.12), P(-0.62, -0.12) }, purpleLight, outline);
        DrawPolygon(context, new[] { P(-0.56, -0.72), P(0.08, -0.9), P(0.76, -0.72), P(0.34, -0.56), P(-0.24, -0.48) }, new SolidColorBrush(Color.FromRgb(91, 80, 143)), null);
        DrawPolygon(context, new[] { P(-0.88, -0.28), P(0.92, -0.28), P(0.62, 0.08), P(-0.72, 0.08) }, black, outline);
        context.DrawLine(trim, P(-0.88, -0.3), P(0.92, -0.3));
        context.DrawLine(trim, P(-0.72, 0.1), P(0.62, 0.1));
        context.DrawEllipse(eye, null, P(-0.26, -0.1), size * 0.18, size * 0.2);
        context.DrawEllipse(eye, null, P(0.22, -0.12), size * 0.2, size * 0.21);
        context.DrawEllipse(pupil, null, P(-0.18, -0.08), size * 0.08, size * 0.09);
        context.DrawEllipse(pupil, null, P(0.3, -0.1), size * 0.09, size * 0.1);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(135, 136, 116, 182)), Math.Max(0.4, size * 0.035)), P(-0.46, -0.64), P(0.66, -0.64));
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(108, 54, 42, 88)), Math.Max(0.4, size * 0.035)), P(-0.34, -0.78), P(0.54, -0.58));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 0.98);
    }

    private static void DrawSpringChestMarker(DrawingContext context, Point point, double size, Pen rim, GemValue rewardGem)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));
        Color gemColor = GemMarkerColor(Color.FromRgb(205, 38, 29), rewardGem);

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 84, 54, 12)), Math.Max(0.9, size * 0.086));
        Pen trim = new(new SolidColorBrush(Color.FromRgb(173, 119, 37)), Math.Max(0.68, size * 0.062));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(78, 0, 0, 0));
        IBrush gold = new SolidColorBrush(Color.FromRgb(210, 160, 46));
        IBrush goldLight = new SolidColorBrush(Color.FromRgb(249, 204, 70));
        IBrush goldDark = new SolidColorBrush(Color.FromRgb(137, 91, 30));
        IBrush gem = new SolidColorBrush(gemColor);
        IBrush gemDark = new SolidColorBrush(BlendColor(AdjustColor(gemColor, 0.5), Colors.Black, 0.18));
        IBrush gemLight = new SolidColorBrush(BlendColor(gemColor, Colors.White, 0.48));

        context.DrawEllipse(shadow, null, P(0.0, 0.7), size * 1.05, size * 0.22);
        DrawPolygon(context, new[] { P(-0.76, 0.04), P(0.76, 0.04), P(0.68, 0.58), P(-0.68, 0.58) }, gold, outline);
        DrawPolygon(context, new[] { P(0.46, 0.04), P(0.76, 0.04), P(0.68, 0.58), P(0.42, 0.58) }, goldDark, null);
        DrawPolygon(context, new[] { P(-0.84, -0.32), P(-0.58, -0.58), P(0.58, -0.58), P(0.84, -0.32), P(0.76, 0.04), P(-0.76, 0.04) }, goldLight, outline);
        DrawPolygon(context, new[] { P(-0.76, 0.04), P(0.76, 0.04), P(0.68, 0.18), P(-0.68, 0.18) }, new SolidColorBrush(Color.FromArgb(170, 248, 218, 82)), null);
        DrawPolygon(context, new[] { P(-0.36, -0.72), P(0.0, -0.96), P(0.4, -0.72), P(0.24, -0.46), P(-0.24, -0.46) }, gem, outline);
        DrawPolygon(context, new[] { P(0.0, -0.96), P(0.4, -0.72), P(0.24, -0.46), P(0.0, -0.56) }, gemDark, null);
        DrawPolygon(context, new[] { P(-0.28, -0.68), P(0.0, -0.86), P(0.04, -0.58), P(-0.18, -0.52) }, gemLight, null);
        context.DrawLine(trim, P(-0.6, 0.42), P(0.6, 0.42));
        context.DrawLine(trim, P(-0.66, -0.2), P(0.66, -0.2));
        context.DrawEllipse(goldDark, null, P(-0.42, 0.3), size * 0.06, size * 0.06);
        context.DrawEllipse(goldDark, null, P(0.42, 0.3), size * 0.06, size * 0.06);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.08, size * 0.98);
    }

    private static void DrawFireworkChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 24, 21, 28)), Math.Max(0.86, size * 0.082));
        Pen metal = new(new SolidColorBrush(Color.FromRgb(107, 105, 120)), Math.Max(0.62, size * 0.058));
        Pen woodLine = new(new SolidColorBrush(Color.FromArgb(170, 87, 50, 32)), Math.Max(0.42, size * 0.035));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(82, 0, 0, 0));
        IBrush wood = new SolidColorBrush(Color.FromRgb(111, 61, 45));
        IBrush woodLight = new SolidColorBrush(Color.FromRgb(155, 86, 58));
        IBrush inside = new SolidColorBrush(Color.FromRgb(37, 35, 52));
        IBrush gem = new SolidColorBrush(Color.FromRgb(232, 206, 58));
        IBrush gemLight = new SolidColorBrush(Color.FromRgb(255, 244, 106));
        IBrush purple = new SolidColorBrush(Color.FromRgb(126, 80, 174));
        IBrush teal = new SolidColorBrush(Color.FromRgb(54, 216, 194));
        IBrush red = new SolidColorBrush(Color.FromRgb(194, 55, 48));
        IBrush fuse = new SolidColorBrush(Color.FromRgb(42, 46, 60));
        IBrush star = new SolidColorBrush(Color.FromRgb(255, 235, 105));

        context.DrawEllipse(shadow, null, P(0.0, 0.76), size * 1.16, size * 0.24);
        DrawPolygon(context, new[] { P(-0.96, -0.14), P(0.96, -0.14), P(0.78, 0.58), P(-0.78, 0.58) }, wood, outline);
        DrawPolygon(context, new[] { P(-0.84, -0.28), P(0.84, -0.28), P(0.96, -0.14), P(-0.96, -0.14) }, inside, outline);
        DrawPolygon(context, new[] { P(-0.78, 0.22), P(0.78, 0.22), P(0.72, 0.4), P(-0.72, 0.4) }, woodLight, null);
        context.DrawLine(metal, P(-0.9, -0.08), P(0.9, -0.08));
        context.DrawLine(metal, P(-0.7, 0.58), P(0.7, 0.58));
        context.DrawLine(woodLine, P(-0.46, 0.0), P(-0.38, 0.54));
        context.DrawLine(woodLine, P(0.0, -0.02), P(0.0, 0.56));
        context.DrawLine(woodLine, P(0.44, 0.0), P(0.36, 0.54));

        DrawPolygon(context, new[] { P(-0.84, -0.32), P(-0.36, -0.62), P(-0.18, -0.22) }, teal, outline);
        DrawPolygon(context, new[] { P(0.34, -0.58), P(0.86, -0.32), P(0.52, -0.16) }, teal, outline);
        DrawPolygon(context, new[] { P(-0.26, -0.86), P(0.14, -0.94), P(0.28, -0.18), P(-0.12, -0.12) }, purple, outline);
        DrawPolygon(context, new[] { P(0.02, -0.72), P(0.24, -0.96), P(0.46, -0.48), P(0.22, -0.38) }, red, outline);
        DrawPolygon(context, new[] { P(0.34, -0.88), P(0.54, -0.88), P(0.58, -0.34), P(0.38, -0.32) }, red, outline);
        context.DrawEllipse(fuse, null, P(0.45, -0.98), size * 0.055, size * 0.12);
        context.DrawLine(new Pen(fuse, Math.Max(0.42, size * 0.038)), P(0.46, -1.08), P(0.52, -1.24));

        DrawPolygon(context, new[] { P(-0.32, -0.06), P(0.0, -0.28), P(0.38, -0.08), P(0.24, 0.22), P(-0.12, 0.28) }, gem, outline);
        DrawPolygon(context, new[] { P(-0.24, -0.04), P(0.0, -0.22), P(0.08, 0.06), P(-0.1, 0.18) }, gemLight, null);
        DrawPolygon(context, new[] { P(-0.1, -0.72), P(-0.02, -0.58), P(-0.12, -0.44), P(-0.22, -0.58) }, star, null);
        DrawPolygon(context, new[] { P(0.58, -0.36), P(0.66, -0.24), P(0.56, -0.12), P(0.48, -0.24) }, star, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.08);
    }

    private static void DrawFlameSpinChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(228, 32, 24, 31)), Math.Max(0.88, size * 0.085));
        Pen bronze = new(new SolidColorBrush(Color.FromRgb(177, 116, 56)), Math.Max(0.72, size * 0.068));
        Pen bright = new(new SolidColorBrush(Color.FromArgb(190, 255, 250, 226)), Math.Max(0.58, size * 0.048));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(82, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(48, 47, 103));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(77, 76, 140));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(24, 25, 54));
        IBrush lid = new SolidColorBrush(Color.FromRgb(239, 239, 229));
        IBrush lidShade = new SolidColorBrush(Color.FromRgb(180, 165, 139));
        IBrush wood = new SolidColorBrush(Color.FromRgb(132, 78, 42));
        IBrush flame = new SolidColorBrush(Color.FromRgb(248, 226, 68));
        IBrush flameDark = new SolidColorBrush(Color.FromRgb(181, 119, 31));

        context.DrawEllipse(shadow, null, P(0.0, 0.76), size * 1.18, size * 0.24);
        DrawPolygon(context, new[] { P(-1.02, -0.12), P(-0.86, 0.52), P(-0.2, 0.72), P(0.78, 0.5), P(1.02, -0.08), P(0.7, -0.22), P(-0.72, -0.22) }, body, outline);
        DrawPolygon(context, new[] { P(0.18, -0.2), P(1.02, -0.08), P(0.78, 0.5), P(0.08, 0.68) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.82, 0.08), P(0.88, -0.02), P(0.82, 0.18), P(-0.78, 0.3) }, bodyLight, null);
        DrawPolygon(context, new[] { P(-1.0, -0.38), P(-0.64, -0.72), P(0.0, -0.84), P(0.72, -0.7), P(1.02, -0.36), P(0.66, -0.14), P(-0.66, -0.16) }, lid, outline);
        DrawPolygon(context, new[] { P(-0.64, -0.72), P(0.0, -0.84), P(0.72, -0.7), P(0.26, -0.52), P(-0.28, -0.5) }, new SolidColorBrush(Color.FromRgb(255, 255, 250)), null);
        DrawPolygon(context, new[] { P(0.72, -0.7), P(1.02, -0.36), P(0.66, -0.14), P(0.26, -0.52) }, lidShade, null);
        DrawPolygon(context, new[] { P(-0.88, -0.2), P(0.82, -0.28), P(0.7, -0.12), P(-0.72, -0.04) }, wood, null);
        context.DrawLine(bronze, P(-0.94, 0.05), P(0.96, -0.06));
        context.DrawLine(bronze, P(-0.72, 0.56), P(0.78, 0.4));
        DrawPolygon(context, new[] { P(-0.18, -0.8), P(0.0, -1.05), P(0.18, -0.78), P(0.0, -0.64) }, lidShade, outline);
        DrawPolygon(context, new[] { P(-0.12, -0.7), P(0.2, -0.28), P(0.0, 0.0), P(-0.28, -0.22) }, lid, outline);
        DrawPolygon(context, new[] { P(-0.12, 0.04), P(0.16, -0.16), P(0.46, -0.02), P(0.24, 0.26) }, flame, outline);
        DrawPolygon(context, new[] { P(0.16, -0.16), P(0.46, -0.02), P(0.24, 0.26), P(0.08, 0.08) }, flameDark, null);
        context.DrawLine(bright, P(-0.82, -0.34), P(0.66, -0.46));
        context.DrawLine(bright, P(-0.78, 0.1), P(0.72, 0.0));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.04);
    }

    private static void DrawFlameChargeChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(228, 45, 28, 18)), Math.Max(0.9, size * 0.09));
        Pen goldLine = new(new SolidColorBrush(Color.FromRgb(255, 219, 91)), Math.Max(0.72, size * 0.068));
        Pen redLine = new(new SolidColorBrush(Color.FromRgb(159, 33, 30)), Math.Max(0.65, size * 0.06));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(86, 0, 0, 0));
        IBrush gold = new SolidColorBrush(Color.FromRgb(221, 168, 60));
        IBrush goldLight = new SolidColorBrush(Color.FromRgb(255, 223, 113));
        IBrush red = new SolidColorBrush(Color.FromRgb(196, 47, 43));
        IBrush redDark = new SolidColorBrush(Color.FromRgb(123, 35, 29));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(112, 46, 32));
        IBrush cyan = new SolidColorBrush(Color.FromRgb(118, 224, 210));

        context.DrawEllipse(shadow, null, P(0.0, 0.72), size * 1.12, size * 0.24);
        DrawPolygon(context, new[] { P(-0.92, -0.04), P(0.92, -0.04), P(0.82, 0.58), P(-0.82, 0.58) }, red, outline);
        DrawPolygon(context, new[] { P(0.54, -0.04), P(0.92, -0.04), P(0.82, 0.58), P(0.48, 0.58) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.98, -0.44), P(-0.72, -0.76), P(0.72, -0.76), P(0.98, -0.44), P(0.88, -0.04), P(-0.88, -0.04) }, goldLight, outline);
        DrawPolygon(context, new[] { P(-0.56, -0.64), P(0.56, -0.64), P(0.36, -0.3), P(-0.36, -0.3) }, red, null);
        DrawPolygon(context, new[] { P(-0.88, -0.04), P(0.88, -0.04), P(0.8, 0.14), P(-0.8, 0.14) }, gold, null);
        DrawPolygon(context, new[] { P(-0.78, 0.22), P(0.78, 0.22), P(0.7, 0.42), P(-0.7, 0.42) }, gold, null);

        context.DrawLine(goldLine, P(-0.76, -0.52), P(0.76, -0.52));
        context.DrawLine(goldLine, P(-0.68, 0.5), P(0.68, 0.5));
        context.DrawLine(redLine, P(-0.74, 0.0), P(0.74, 0.0));
        context.DrawEllipse(cyan, new Pen(new SolidColorBrush(Color.FromArgb(190, 255, 247, 184)), Math.Max(0.45, size * 0.04)), P(0.0, 0.32), size * 0.19, size * 0.1);
        context.DrawEllipse(cyan, null, P(-0.42, -0.52), size * 0.1, size * 0.055);
        context.DrawEllipse(cyan, null, P(0.42, -0.52), size * 0.1, size * 0.055);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.08);
    }

    private static void DrawChargeOnlyChestMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(230, 18, 23, 30)), Math.Max(0.9, size * 0.09));
        Pen seam = new(new SolidColorBrush(Color.FromArgb(190, 112, 126, 146)), Math.Max(0.55, size * 0.045));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(88, 0, 0, 0));
        IBrush lidTop = new SolidColorBrush(Color.FromRgb(248, 251, 254));
        IBrush lidFront = new SolidColorBrush(Color.FromRgb(215, 225, 239));
        IBrush bodyFront = new SolidColorBrush(Color.FromRgb(34, 41, 52));
        IBrush bodySide = new SolidColorBrush(Color.FromRgb(21, 27, 36));
        IBrush band = new SolidColorBrush(Color.FromRgb(237, 242, 247));
        IBrush topShade = new SolidColorBrush(Color.FromRgb(194, 209, 226));
        IBrush brightEdge = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255));

        context.DrawEllipse(shadow, null, P(0.0, 0.7), size * 1.12, size * 0.24);
        DrawPolygon(context, new[] { P(-0.92, -0.06), P(0.92, -0.06), P(0.82, 0.58), P(-0.82, 0.58) }, bodyFront, outline);
        DrawPolygon(context, new[] { P(0.52, -0.06), P(0.92, -0.06), P(0.82, 0.58), P(0.48, 0.58) }, bodySide, null);
        DrawPolygon(context, new[] { P(-0.84, 0.24), P(0.84, 0.24), P(0.76, 0.44), P(-0.76, 0.44) }, band, null);
        DrawPolygon(context, new[] { P(-0.98, -0.48), P(-0.72, -0.78), P(0.72, -0.78), P(0.98, -0.48), P(0.88, -0.06), P(-0.88, -0.06) }, lidTop, outline);
        DrawPolygon(context, new[] { P(-0.88, -0.06), P(0.88, -0.06), P(0.82, 0.12), P(-0.82, 0.12) }, lidFront, null);
        DrawPolygon(context, new[] { P(-0.36, -0.66), P(0.36, -0.66), P(0.22, -0.34), P(-0.22, -0.34) }, topShade, null);
        DrawPolygon(context, new[] { P(-0.82, -0.02), P(0.82, -0.02), P(0.78, 0.08), P(-0.78, 0.08) }, brightEdge, null);
        context.DrawLine(seam, P(-0.76, 0.12), P(0.76, 0.12));
        context.DrawLine(seam, P(-0.72, 0.42), P(0.72, 0.42));
        context.DrawEllipse(band, null, P(-0.52, -0.26), size * 0.07, size * 0.055);
        context.DrawEllipse(band, null, P(0.52, -0.26), size * 0.07, size * 0.055);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.16, size * 1.0);
    }

    private static void DrawSkinnyTreeMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(205, 24, 72, 34)), Math.Max(0.8, size * 0.075));
        Pen barkLine = new(new SolidColorBrush(Color.FromArgb(148, 92, 57, 27)), Math.Max(0.45, size * 0.04));
        Pen leafBand = new(new SolidColorBrush(Color.FromArgb(112, 35, 118, 45)), Math.Max(0.55, size * 0.045));
        IBrush trunk = new SolidColorBrush(Color.FromRgb(125, 77, 38));
        IBrush trunkDark = new SolidColorBrush(Color.FromRgb(85, 52, 28));
        IBrush leaf = new SolidColorBrush(Color.FromRgb(72, 183, 68));
        IBrush leafLight = new SolidColorBrush(Color.FromRgb(100, 210, 89));
        IBrush leafDark = new SolidColorBrush(Color.FromRgb(35, 124, 48));
        IBrush leafMid = new SolidColorBrush(Color.FromRgb(58, 158, 58));

        DrawPolygon(context, new[] { P(-0.16, 1.1), P(0.18, 1.08), P(0.14, 0.25), P(-0.1, 0.28) }, trunk, outline);
        DrawPolygon(context, new[] { P(0.02, 1.08), P(0.18, 1.08), P(0.14, 0.25), P(0.02, 0.36) }, trunkDark, null);
        context.DrawLine(barkLine, P(-0.05, 0.98), P(0.06, 0.34));
        DrawPolygon(context, new[] { P(-0.14, -1.42), P(-0.72, -0.18), P(-0.58, 0.28), P(-0.02, 0.66), P(0.58, 0.34), P(0.72, -0.2), P(0.18, -1.34) }, leaf, outline);
        DrawPolygon(context, new[] { P(-0.14, -1.42), P(-0.68, -0.18), P(-0.06, -0.02), P(0.18, -1.34) }, leafLight, null);
        DrawPolygon(context, new[] { P(0.18, -1.34), P(-0.06, -0.02), P(0.7, -0.2), P(0.58, 0.34), P(0.08, 0.14) }, leafDark, null);
        DrawPolygon(context, new[] { P(-0.58, 0.28), P(-0.02, 0.66), P(0.58, 0.34), P(0.08, 0.14), P(-0.3, 0.1) }, leafMid, null);
        context.DrawLine(leafBand, P(-0.56, -0.22), P(0.56, -0.02));
        context.DrawLine(leafBand, P(-0.46, 0.22), P(0.42, 0.38));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.92, size * 1.2);
    }

    private static void DrawPlatformMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(220, 55, 64, 72)), Math.Max(0.8, size * 0.075));
        Pen innerLine = new(new SolidColorBrush(Color.FromArgb(120, 42, 47, 54)), Math.Max(0.45, size * 0.04));
        IBrush top = new SolidColorBrush(Color.FromRgb(128, 150, 132));
        IBrush topLight = new SolidColorBrush(Color.FromRgb(164, 184, 158));
        IBrush side = new SolidColorBrush(Color.FromRgb(92, 106, 96));
        IBrush sideDark = new SolidColorBrush(Color.FromRgb(67, 77, 72));

        DrawPolygon(context, new[] { P(-0.74, -0.2), P(-0.16, -0.58), P(0.72, -0.26), P(0.2, 0.16) }, top, outline);
        DrawPolygon(context, new[] { P(-0.16, -0.58), P(0.72, -0.26), P(0.24, -0.08), P(-0.48, -0.28) }, topLight, null);
        DrawPolygon(context, new[] { P(-0.74, -0.2), P(0.2, 0.16), P(0.2, 0.5), P(-0.68, 0.18) }, side, outline);
        DrawPolygon(context, new[] { P(0.2, 0.16), P(0.72, -0.26), P(0.66, 0.1), P(0.2, 0.5) }, sideDark, outline);
        context.DrawLine(innerLine, P(-0.5, -0.05), P(0.42, 0.24));
        context.DrawLine(innerLine, P(-0.12, -0.32), P(0.28, -0.16));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.04, size * 0.82);
    }

    private static void DrawCactusMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(220, 38, 79, 34)), Math.Max(0.76, size * 0.072));
        Pen rib = new(new SolidColorBrush(Color.FromArgb(130, 42, 105, 45)), Math.Max(0.42, size * 0.034));
        IBrush green = new SolidColorBrush(Color.FromRgb(91, 139, 70));
        IBrush greenLight = new SolidColorBrush(Color.FromRgb(124, 167, 85));
        IBrush greenDark = new SolidColorBrush(Color.FromRgb(57, 95, 48));
        IBrush flower = new SolidColorBrush(Color.FromRgb(239, 174, 24));
        IBrush flowerDark = new SolidColorBrush(Color.FromRgb(178, 105, 21));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(72, 0, 0, 0)), null, P(0.02, 1.0), size * 0.82, size * 0.16);
        DrawPolygon(context, new[] { P(-0.28, 1.0), P(0.28, 1.0), P(0.34, -0.78), P(0.18, -1.12), P(-0.18, -1.12), P(-0.34, -0.78) }, green, outline);
        DrawPolygon(context, new[] { P(0.06, 0.98), P(0.28, 1.0), P(0.34, -0.78), P(0.18, -1.12), P(0.1, -0.68) }, greenDark, null);
        DrawPolygon(context, new[] { P(-0.28, 0.98), P(-0.12, -0.96), P(0.02, -1.08), P(-0.04, 0.94) }, greenLight, null);
        DrawPolygon(context, new[] { P(-0.7, 0.28), P(-0.42, 0.24), P(-0.34, -0.44), P(-0.56, -0.36) }, green, outline);
        DrawPolygon(context, new[] { P(0.34, 0.36), P(0.66, 0.18), P(0.58, -0.46), P(0.34, -0.34) }, greenDark, outline);
        DrawPolygon(context, new[] { P(0.46, 0.08), P(0.74, -0.04), P(0.78, -0.32), P(0.58, -0.28) }, green, outline);
        context.DrawLine(rib, P(-0.18, 0.82), P(-0.2, -0.76));
        context.DrawLine(rib, P(0.0, 0.88), P(0.0, -0.96));
        context.DrawLine(rib, P(0.18, 0.78), P(0.2, -0.72));
        context.DrawLine(rib, P(-0.52, 0.18), P(-0.44, -0.28));
        context.DrawLine(rib, P(0.5, 0.2), P(0.48, -0.24));
        context.DrawEllipse(greenDark, null, P(-0.1, -0.36), size * 0.035, size * 0.035);
        context.DrawEllipse(greenDark, null, P(0.12, 0.18), size * 0.035, size * 0.035);
        DrawPolygon(context, new[] { P(-0.12, -1.18), P(0.0, -1.48), P(0.12, -1.18), P(0.0, -1.08) }, flower, outline);
        DrawPolygon(context, new[] { P(-0.68, -0.36), P(-0.98, -0.5), P(-0.72, -0.64), P(-0.52, -0.46) }, flower, outline);
        DrawPolygon(context, new[] { P(0.68, -0.26), P(0.98, -0.36), P(0.84, -0.58), P(0.58, -0.42) }, flower, outline);
        context.DrawEllipse(flowerDark, null, P(0.0, -1.18), size * 0.08, size * 0.06);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.96, size * 1.26);
    }

    private static void DrawLampMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 26, 25, 61)), Math.Max(0.76, size * 0.072));
        Pen highlight = new(new SolidColorBrush(Color.FromArgb(150, 230, 255, 226)), Math.Max(0.48, size * 0.04));
        IBrush pole = new SolidColorBrush(Color.FromRgb(90, 67, 151));
        IBrush poleDark = new SolidColorBrush(Color.FromRgb(55, 44, 112));
        IBrush glass = new SolidColorBrush(Color.FromRgb(31, 156, 73));
        IBrush glassLight = new SolidColorBrush(Color.FromRgb(105, 235, 145));
        IBrush glassDark = new SolidColorBrush(Color.FromRgb(6, 103, 45));
        IBrush cap = new SolidColorBrush(Color.FromRgb(76, 55, 148));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, P(0.0, 1.0), size * 0.42, size * 0.12);
        DrawPolygon(context, new[] { P(-0.16, 1.02), P(0.16, 1.02), P(0.08, 0.34), P(-0.08, 0.34) }, pole, outline);
        DrawPolygon(context, new[] { P(0.0, 1.02), P(0.16, 1.02), P(0.08, 0.34), P(0.0, 0.36) }, poleDark, null);
        DrawPolygon(context, new[] { P(-0.52, -0.24), P(-0.24, -0.58), P(0.24, -0.58), P(0.52, -0.24), P(0.34, 0.18), P(-0.34, 0.18) }, glass, outline);
        DrawPolygon(context, new[] { P(-0.42, -0.22), P(-0.22, -0.48), P(0.0, -0.48), P(-0.18, -0.12) }, glassLight, null);
        DrawPolygon(context, new[] { P(0.06, -0.52), P(0.24, -0.58), P(0.52, -0.24), P(0.34, 0.18), P(0.12, 0.08) }, glassDark, null);
        DrawPolygon(context, new[] { P(-0.2, -0.58), P(0.0, -0.98), P(0.22, -0.58) }, cap, outline);
        DrawPolygon(context, new[] { P(-0.34, 0.18), P(0.34, 0.18), P(0.18, 0.4), P(-0.18, 0.4) }, pole, outline);
        context.DrawLine(highlight, P(-0.36, -0.18), P(-0.06, -0.42));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.82, size * 1.12);
    }

    private static void DrawCannonMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 42, 43, 46)), Math.Max(0.82, size * 0.078));
        Pen metalLine = new(new SolidColorBrush(Color.FromArgb(150, 99, 103, 112)), Math.Max(0.44, size * 0.038));
        Pen spoke = new(new SolidColorBrush(Color.FromRgb(93, 69, 46)), Math.Max(0.48, size * 0.044));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush wheel = new SolidColorBrush(Color.FromRgb(99, 108, 111));
        IBrush wheelInner = new SolidColorBrush(Color.FromRgb(143, 101, 70));
        IBrush wheelDark = new SolidColorBrush(Color.FromRgb(55, 61, 65));
        IBrush barrel = new SolidColorBrush(Color.FromRgb(221, 224, 238));
        IBrush barrelLight = new SolidColorBrush(Color.FromRgb(244, 246, 255));
        IBrush barrelShade = new SolidColorBrush(Color.FromRgb(164, 169, 187));
        IBrush muzzle = new SolidColorBrush(Color.FromRgb(135, 141, 151));
        IBrush baseMetal = new SolidColorBrush(Color.FromRgb(186, 190, 202));

        context.DrawEllipse(shadow, null, P(0.02, 0.78), size * 1.28, size * 0.28);
        context.DrawEllipse(wheel, outline, P(-0.72, 0.24), size * 0.28, size * 0.46);
        context.DrawEllipse(wheelDark, null, P(-0.74, 0.25), size * 0.19, size * 0.36);
        context.DrawEllipse(wheelInner, null, P(-0.72, 0.24), size * 0.1, size * 0.26);
        context.DrawLine(spoke, P(-0.82, 0.16), P(-0.62, 0.32));
        context.DrawLine(spoke, P(-0.82, 0.34), P(-0.62, 0.16));
        context.DrawEllipse(wheel, outline, P(0.78, 0.26), size * 0.28, size * 0.46);
        context.DrawEllipse(wheelDark, null, P(0.8, 0.27), size * 0.19, size * 0.36);
        context.DrawEllipse(wheelInner, null, P(0.78, 0.26), size * 0.1, size * 0.26);
        context.DrawLine(spoke, P(0.68, 0.18), P(0.88, 0.34));
        context.DrawLine(spoke, P(0.68, 0.36), P(0.88, 0.18));
        DrawPolygon(context, new[] { P(-0.46, 0.36), P(0.46, 0.38), P(0.58, 0.58), P(-0.58, 0.56) }, baseMetal, outline);
        DrawPolygon(context, new[] { P(-0.36, 0.28), P(0.3, 0.28), P(0.5, 0.02), P(-0.2, -0.02) }, barrelShade, outline);
        DrawPolygon(context, new[] { P(-0.22, 0.38), P(0.34, 0.52), P(0.78, -0.82), P(0.34, -1.08) }, barrel, outline);
        DrawPolygon(context, new[] { P(-0.1, 0.32), P(0.16, 0.38), P(0.62, -0.9), P(0.34, -1.08) }, barrelLight, null);
        DrawPolygon(context, new[] { P(0.18, 0.48), P(0.34, 0.52), P(0.78, -0.82), P(0.58, -0.72) }, barrelShade, null);
        DrawPolygon(context, new[] { P(-0.3, 0.32), P(0.2, 0.46), P(0.06, 0.72), P(-0.48, 0.58) }, muzzle, outline);
        context.DrawLine(metalLine, P(-0.36, 0.5), P(0.1, 0.62));
        context.DrawLine(metalLine, P(0.0, -0.08), P(0.58, -0.72));
        context.DrawLine(metalLine, P(0.42, -0.92), P(0.66, -0.78));
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(45, 47, 52)), null, P(-0.18, 0.48), size * 0.035, size * 0.035);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(45, 47, 52)), null, P(0.02, 0.54), size * 0.035, size * 0.035);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.02);
    }

    private static void DrawTentMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 96, 54, 36)), Math.Max(0.76, size * 0.07));
        Pen stripeLine = new(new SolidColorBrush(Color.FromArgb(130, 128, 93, 54)), Math.Max(0.34, size * 0.03));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(66, 0, 0, 0));
        IBrush orange = new SolidColorBrush(Color.FromRgb(239, 151, 78));
        IBrush orangeLight = new SolidColorBrush(Color.FromRgb(255, 190, 102));
        IBrush teal = new SolidColorBrush(Color.FromRgb(54, 190, 177));
        IBrush tealDark = new SolidColorBrush(Color.FromRgb(34, 140, 139));
        IBrush canvas = new SolidColorBrush(Color.FromRgb(246, 202, 130));
        IBrush pole = new SolidColorBrush(Color.FromRgb(114, 79, 45));
        IBrush flag = new SolidColorBrush(Color.FromRgb(74, 157, 221));

        context.DrawEllipse(shadow, null, P(0.02, 1.02), size * 0.92, size * 0.18);
        context.DrawLine(new Pen(pole, Math.Max(0.52, size * 0.045)), P(0.0, -1.08), P(0.0, -1.44));
        DrawPolygon(context, new[] { P(0.02, -1.44), P(0.54, -1.28), P(0.18, -1.14) }, flag, outline);
        DrawPolygon(context, new[] { P(-0.7, -0.46), P(0.0, -1.08), P(0.7, -0.46), P(0.58, -0.18), P(-0.58, -0.18) }, orangeLight, outline);
        DrawPolygon(context, new[] { P(-0.54, -0.52), P(-0.24, -0.82), P(0.02, -0.18), P(-0.42, -0.18) }, orange, null);
        DrawPolygon(context, new[] { P(-0.18, -0.92), P(0.0, -1.08), P(0.22, -0.88), P(0.14, -0.18), P(-0.06, -0.18) }, teal, null);
        DrawPolygon(context, new[] { P(0.22, -0.88), P(0.7, -0.46), P(0.58, -0.18), P(0.14, -0.18) }, orange, null);
        DrawPolygon(context, new[] { P(-0.68, -0.34), P(0.68, -0.34), P(0.58, -0.16), P(-0.58, -0.16) }, teal, outline);
        DrawPolygon(context, new[] { P(-0.52, -0.16), P(0.52, -0.16), P(0.44, 0.9), P(-0.44, 0.9) }, canvas, outline);
        DrawPolygon(context, new[] { P(-0.52, -0.16), P(-0.28, -0.16), P(-0.24, 0.9), P(-0.44, 0.9) }, orange, null);
        DrawPolygon(context, new[] { P(-0.2, -0.16), P(0.0, -0.16), P(0.02, 0.9), P(-0.16, 0.9) }, teal, null);
        DrawPolygon(context, new[] { P(0.1, -0.16), P(0.3, -0.16), P(0.26, 0.9), P(0.1, 0.9) }, orange, null);
        DrawPolygon(context, new[] { P(0.34, -0.16), P(0.52, -0.16), P(0.44, 0.9), P(0.3, 0.9) }, tealDark, null);
        context.DrawLine(stripeLine, P(-0.44, 0.14), P(0.44, 0.14));
        context.DrawLine(stripeLine, P(-0.42, 0.52), P(0.42, 0.52));
        DrawPolygon(context, new[] { P(-0.12, 0.9), P(0.12, 0.9), P(0.04, 0.2), P(-0.04, 0.2) }, new SolidColorBrush(Color.FromArgb(76, 80, 49, 34)), null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.98, size * 1.18);
    }

    private static void DrawBalloonMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 58, 38, 20)), Math.Max(0.72, size * 0.068));
        Pen rope = new(new SolidColorBrush(Color.FromRgb(105, 78, 34)), Math.Max(0.48, size * 0.044));
        Pen basketLine = new(new SolidColorBrush(Color.FromArgb(150, 71, 50, 20)), Math.Max(0.36, size * 0.032));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(66, 0, 0, 0));
        IBrush red = new SolidColorBrush(Color.FromRgb(141, 25, 22));
        IBrush redDark = new SolidColorBrush(Color.FromRgb(88, 16, 19));
        IBrush gold = new SolidColorBrush(Color.FromRgb(163, 128, 36));
        IBrush goldLight = new SolidColorBrush(Color.FromRgb(214, 174, 60));
        IBrush band = new SolidColorBrush(Color.FromRgb(186, 140, 52));
        IBrush bandLight = new SolidColorBrush(Color.FromRgb(236, 194, 83));
        IBrush basket = new SolidColorBrush(Color.FromRgb(119, 82, 31));
        IBrush basketDark = new SolidColorBrush(Color.FromRgb(65, 47, 20));

        context.DrawEllipse(shadow, null, P(0.0, 1.16), size * 0.78, size * 0.18);
        DrawPolygon(context, new[] { P(-0.94, -0.56), P(-0.7, -1.06), P(-0.2, -1.28), P(0.36, -1.22), P(0.84, -0.88), P(1.02, -0.34), P(0.86, 0.26), P(0.38, 0.58), P(-0.18, 0.62), P(-0.72, 0.32), P(-1.02, -0.2) }, red, outline);
        DrawPolygon(context, new[] { P(-0.72, -0.9), P(-0.36, -1.2), P(-0.18, 0.56), P(-0.48, 0.42), P(-0.82, -0.18) }, gold, null);
        DrawPolygon(context, new[] { P(-0.22, -1.28), P(0.08, -1.3), P(0.0, 0.62), P(-0.18, 0.62) }, redDark, null);
        DrawPolygon(context, new[] { P(0.16, -1.26), P(0.5, -1.12), P(0.34, 0.58), P(0.1, 0.62) }, goldLight, null);
        DrawPolygon(context, new[] { P(0.56, -1.04), P(0.86, -0.74), P(0.76, 0.22), P(0.48, 0.5) }, redDark, null);
        DrawPolygon(context, new[] { P(-0.98, -0.16), P(-0.72, -0.34), P(-0.14, -0.42), P(0.52, -0.36), P(0.98, -0.14), P(0.9, 0.08), P(0.32, 0.22), P(-0.42, 0.2), P(-0.98, 0.02) }, band, outline);
        DrawPolygon(context, new[] { P(-0.86, -0.18), P(-0.24, -0.34), P(0.5, -0.28), P(0.88, -0.12), P(0.78, -0.02), P(0.24, 0.08), P(-0.46, 0.06), P(-0.9, -0.04) }, bandLight, null);
        context.DrawLine(rope, P(-0.64, 0.08), P(-0.42, 0.9));
        context.DrawLine(rope, P(0.58, 0.06), P(0.38, 0.9));
        context.DrawLine(rope, P(-0.2, 0.2), P(-0.2, 0.9));
        context.DrawLine(rope, P(0.18, 0.2), P(0.18, 0.9));
        DrawPolygon(context, new[] { P(-0.5, 0.86), P(0.48, 0.86), P(0.34, 1.26), P(-0.32, 1.26) }, basket, outline);
        DrawPolygon(context, new[] { P(0.0, 0.86), P(0.48, 0.86), P(0.34, 1.26), P(0.02, 1.18) }, basketDark, null);
        context.DrawLine(basketLine, P(-0.42, 0.96), P(0.34, 1.2));
        context.DrawLine(basketLine, P(0.38, 0.96), P(-0.24, 1.22));
        context.DrawLine(basketLine, P(-0.26, 0.86), P(-0.18, 1.24));
        context.DrawLine(basketLine, P(0.22, 0.86), P(0.14, 1.24));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.25);
    }

    private static void DrawSceneryMarker(DrawingContext context, Point point, double size, IBrush fill, Pen rim, string stamp)
    {
        if (stamp == "Bal")
        {
            DrawBalloonMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Cn")
        {
            DrawCannonMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Te")
        {
            DrawTentMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Ca")
        {
            DrawCactusMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Tr")
        {
            DrawSkinnyTreeMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Pl")
        {
            DrawPlatformMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Fl")
        {
            Pen stem = new(new SolidColorBrush(Color.FromRgb(63, 176, 117)), Math.Max(1.2, size * 0.15));
            context.DrawLine(stem, new Point(point.X, point.Y + size), new Point(point.X, point.Y - (size * 0.2)));
            context.DrawEllipse(fill, rim, new Point(point.X, point.Y - (size * 0.62)), size * 0.34, size * 0.5);
            context.DrawEllipse(fill, rim, new Point(point.X - (size * 0.36), point.Y - (size * 0.34)), size * 0.34, size * 0.42);
            context.DrawEllipse(fill, rim, new Point(point.X + (size * 0.36), point.Y - (size * 0.34)), size * 0.34, size * 0.42);
            DrawMobyStamp(context, point + new Vector(0, size * 0.14), stamp, size);
            return;
        }

        if (stamp == "G")
        {
            Pen blade = new(fill, Math.Max(1.2, size * 0.17));
            context.DrawLine(blade, new Point(point.X - (size * 0.65), point.Y + size), new Point(point.X - (size * 0.25), point.Y - (size * 0.45)));
            context.DrawLine(blade, new Point(point.X, point.Y + size), new Point(point.X, point.Y - (size * 0.75)));
            context.DrawLine(blade, new Point(point.X + (size * 0.65), point.Y + size), new Point(point.X + (size * 0.25), point.Y - (size * 0.45)));
            DrawMobyStamp(context, point + new Vector(0, size * 0.18), stamp, size);
            return;
        }

        if (stamp == "To")
        {
            DrawTorchMarker(context, point, size, rim);
            return;
        }

        if (stamp == "L")
        {
            DrawLampMarker(context, point, size, rim);
            return;
        }

        if (stamp == "F")
        {
            Pen pole = new(new SolidColorBrush(Color.FromRgb(92, 64, 42)), Math.Max(1.2, size * 0.16));
            context.DrawLine(pole, new Point(point.X - (size * 0.42), point.Y + size), new Point(point.X - (size * 0.42), point.Y - size));
            DrawPolygon(context, new[]
            {
                new Point(point.X - (size * 0.32), point.Y - (size * 0.9)),
                new Point(point.X + (size * 0.82), point.Y - (size * 0.48)),
                new Point(point.X - (size * 0.32), point.Y - (size * 0.08))
            }, fill, rim);
            DrawMobyStamp(context, point + new Vector(size * 0.12, size * 0.2), stamp, size);
            return;
        }

        Pen trunk = new(new SolidColorBrush(Color.FromRgb(92, 64, 42)), Math.Max(1.4, size * 0.22));
        context.DrawLine(trunk, new Point(point.X, point.Y + (size * 1.05)), new Point(point.X, point.Y - (size * 0.25)));
        context.DrawEllipse(fill, rim, new Point(point.X, point.Y - (size * 0.55)), size * 0.85, size * 0.72);
        context.DrawEllipse(fill, rim, new Point(point.X - (size * 0.45), point.Y - (size * 0.05)), size * 0.62, size * 0.54);
        context.DrawEllipse(fill, rim, new Point(point.X + (size * 0.45), point.Y - (size * 0.05)), size * 0.62, size * 0.54);
        DrawMobyStamp(context, point + new Vector(0, size * 0.18), stamp, size);
    }

    private static void DrawTorchMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(220, 67, 39, 12)), Math.Max(0.78, size * 0.075));
        Pen handleLine = new(new SolidColorBrush(Color.FromRgb(92, 64, 42)), Math.Max(1.1, size * 0.16));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        IBrush handle = new SolidColorBrush(Color.FromRgb(124, 83, 44));
        IBrush wrap = new SolidColorBrush(Color.FromRgb(73, 44, 28));
        IBrush flameOuter = new SolidColorBrush(Color.FromRgb(237, 83, 30));
        IBrush flameMid = new SolidColorBrush(Color.FromRgb(255, 169, 35));
        IBrush flameInner = new SolidColorBrush(Color.FromRgb(255, 244, 86));

        context.DrawEllipse(shadow, null, P(0.02, 0.96), size * 0.66, size * 0.16);
        context.DrawLine(handleLine, P(-0.08, 0.94), P(0.12, -0.12));
        DrawPolygon(context, new[] { P(-0.24, 0.42), P(0.18, 0.5), P(0.34, -0.12), P(-0.08, -0.2) }, handle, outline);
        context.DrawLine(new Pen(wrap, Math.Max(0.52, size * 0.052)), P(-0.16, 0.26), P(0.24, 0.34));
        context.DrawLine(new Pen(wrap, Math.Max(0.52, size * 0.052)), P(-0.06, -0.04), P(0.28, 0.02));

        DrawPolygon(context, new[] { P(-0.26, -0.18), P(-0.06, -0.72), P(0.18, -1.18), P(0.48, -0.56), P(0.34, -0.12) }, flameOuter, outline);
        DrawPolygon(context, new[] { P(-0.08, -0.22), P(0.08, -0.64), P(0.22, -0.96), P(0.4, -0.48), P(0.24, -0.16) }, flameMid, null);
        DrawPolygon(context, new[] { P(0.08, -0.22), P(0.18, -0.54), P(0.26, -0.34), P(0.18, -0.12) }, flameInner, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.92, size * 1.12);
    }

    private static void DrawFodderSheepMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(218, 32, 34, 32)), Math.Max(0.75, size * 0.075));
        Pen leg = new(new SolidColorBrush(Color.FromRgb(32, 34, 32)), Math.Max(0.85, size * 0.075));
        IBrush wool = new SolidColorBrush(Color.FromRgb(238, 229, 177));
        IBrush woolLight = new SolidColorBrush(Color.FromRgb(255, 248, 205));
        IBrush woolShade = new SolidColorBrush(Color.FromRgb(214, 202, 153));
        IBrush face = new SolidColorBrush(Color.FromRgb(27, 28, 29));
        IBrush eye = new SolidColorBrush(Color.FromRgb(218, 246, 255));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(34, 55, 68));

        context.DrawLine(leg, P(-0.3, 0.44), P(-0.34, 1.05));
        context.DrawLine(leg, P(0.22, 0.48), P(0.24, 1.08));
        context.DrawLine(leg, P(0.58, 0.42), P(0.62, 0.92));
        context.DrawEllipse(woolShade, outline, P(0.18, 0.02), size * 0.96, size * 0.62);
        context.DrawEllipse(wool, null, P(-0.22, -0.18), size * 0.42, size * 0.38);
        context.DrawEllipse(woolLight, null, P(0.22, -0.32), size * 0.44, size * 0.36);
        context.DrawEllipse(wool, null, P(0.62, -0.08), size * 0.42, size * 0.38);
        context.DrawEllipse(woolLight, null, P(0.12, 0.18), size * 0.5, size * 0.38);
        context.DrawEllipse(face, outline, P(-0.64, 0.02), size * 0.36, size * 0.52);
        DrawPolygon(context, new[] { P(-0.78, 0.36), P(-0.62, 0.78), P(-0.46, 0.32) }, face, null);
        context.DrawEllipse(eye, null, P(-0.58, -0.16), size * 0.12, size * 0.18);
        context.DrawEllipse(pupil, null, P(-0.54, -0.12), size * 0.045, size * 0.07);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.05, size * 0.98);
    }

    private static void DrawEggThiefMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(225, 10, 18, 28)), Math.Max(0.82, size * 0.085));
        Pen robeLine = new(new SolidColorBrush(Color.FromArgb(112, 101, 160, 220)), Math.Max(0.44, size * 0.035));
        IBrush robe = new SolidColorBrush(Color.FromRgb(42, 91, 150));
        IBrush robeLight = new SolidColorBrush(Color.FromRgb(70, 123, 187));
        IBrush robeDark = new SolidColorBrush(Color.FromRgb(19, 43, 82));
        IBrush face = new SolidColorBrush(Color.FromRgb(8, 12, 18));
        IBrush grin = new SolidColorBrush(Color.FromRgb(244, 249, 238));
        IBrush egg = new SolidColorBrush(Color.FromRgb(232, 143, 177));
        IBrush eggLight = new SolidColorBrush(Color.FromRgb(255, 188, 208));
        IBrush eggSpot = new SolidColorBrush(Color.FromRgb(188, 90, 128));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(78, 0, 0, 0)), null, P(0.08, 0.94), size * 0.74, size * 0.18);
        DrawPolygon(context, new[] { P(-0.46, 0.98), P(0.58, 0.98), P(0.36, 0.22), P(-0.12, 0.12), P(-0.58, 0.48) }, robe, outline);
        DrawPolygon(context, new[] { P(0.08, 0.12), P(0.42, 0.24), P(0.58, 0.98), P(0.0, 0.98) }, robeDark, null);
        DrawPolygon(context, new[] { P(-0.5, 0.46), P(-0.08, 0.08), P(0.24, 0.32), P(-0.2, 0.62) }, robeLight, null);

        DrawPolygon(context, new[] { P(-0.5, -0.72), P(-0.18, -1.14), P(0.44, -1.18), P(0.78, -0.86), P(0.52, -0.38), P(0.08, -0.22), P(-0.44, -0.3) }, robe, outline);
        DrawPolygon(context, new[] { P(-0.18, -1.14), P(0.44, -1.18), P(0.78, -0.86), P(0.42, -0.78), P(0.08, -0.92) }, robeLight, null);
        DrawPolygon(context, new[] { P(0.38, -0.78), P(0.78, -0.86), P(0.52, -0.38), P(0.12, -0.2), P(0.08, -0.58) }, robeDark, null);
        DrawPolygon(context, new[] { P(-0.44, -0.34), P(0.12, -0.5), P(0.52, -0.38), P(0.2, -0.12), P(-0.32, -0.08) }, face, outline);
        DrawPolygon(context, new[] { P(-0.18, -0.32), P(0.26, -0.4), P(0.12, -0.28), P(-0.12, -0.22) }, grin, null);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(170, 8, 12, 18)), Math.Max(0.44, size * 0.038)), P(-0.12, -0.23), P(0.12, -0.28));

        DrawPolygon(context, new[] { P(-0.72, -0.02), P(-0.42, -0.24), P(-0.18, 0.08), P(-0.42, 0.36) }, robeDark, outline);
        context.DrawEllipse(egg, outline, P(-0.56, 0.2), size * 0.38, size * 0.5);
        context.DrawEllipse(eggLight, null, P(-0.65, 0.04), size * 0.1, size * 0.13);
        context.DrawEllipse(eggSpot, null, P(-0.45, 0.02), size * 0.045, size * 0.055);
        context.DrawEllipse(eggSpot, null, P(-0.62, 0.28), size * 0.04, size * 0.05);
        context.DrawLine(robeLine, P(-0.18, -0.96), P(0.28, -1.1));
        context.DrawLine(robeLine, P(-0.38, 0.78), P(0.42, 0.84));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.9, size * 1.18);
    }

    private static void DrawDogEnemyMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 24, 25, 28)), Math.Max(0.78, size * 0.08));
        Pen thin = new(new SolidColorBrush(Color.FromArgb(156, 32, 34, 38)), Math.Max(0.42, size * 0.04));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(111, 116, 118));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(151, 156, 156));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(63, 68, 70));
        IBrush snout = new SolidColorBrush(Color.FromRgb(30, 32, 34));
        IBrush white = new SolidColorBrush(Color.FromRgb(242, 244, 238));
        IBrush red = new SolidColorBrush(Color.FromRgb(165, 34, 30));
        IBrush yellow = new SolidColorBrush(Color.FromRgb(255, 231, 51));
        IBrush cyan = new SolidColorBrush(Color.FromRgb(87, 225, 234));

        context.DrawEllipse(shadow, null, P(0.06, 0.9), size * 1.08, size * 0.2);
        DrawPolygon(context, new[] { P(-1.02, 0.08), P(-1.34, -0.42), P(-0.74, -0.16), P(-0.72, 0.46) }, bodyLight, outline);
        DrawPolygon(context, new[] { P(-0.8, -0.44), P(-0.22, -0.78), P(0.42, -0.52), P(0.48, 0.16), P(-0.24, 0.62), P(-0.84, 0.28) }, body, outline);
        DrawPolygon(context, new[] { P(-0.22, -0.78), P(0.42, -0.52), P(0.12, -0.18), P(-0.52, -0.14) }, bodyLight, null);
        DrawPolygon(context, new[] { P(0.12, -0.18), P(0.48, 0.16), P(-0.24, 0.62), P(-0.52, 0.24) }, bodyDark, null);
        DrawPolygon(context, new[] { P(0.34, -0.62), P(0.88, -0.9), P(1.28, -0.76), P(1.1, -0.28), P(0.52, 0.04), P(0.18, -0.24) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(0.56, -0.72), P(1.28, -0.76), P(1.1, -0.28), P(0.78, -0.34) }, snout, null);
        DrawPolygon(context, new[] { P(0.26, -0.54), P(0.48, -0.92), P(0.56, -0.42) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(-0.38, 0.52), P(-0.2, 0.96), P(0.1, 0.54) }, bodyLight, outline);
        DrawPolygon(context, new[] { P(0.14, 0.36), P(0.34, 0.92), P(0.58, 0.3) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(0.9, -0.2), P(1.04, 0.12), P(0.98, 0.42), P(0.78, 0.04) }, red, outline);
        DrawPolygon(context, new[] { P(0.88, -0.28), P(0.98, -0.38), P(1.08, -0.28) }, white, null);
        DrawPolygon(context, new[] { P(0.72, -0.18), P(0.82, -0.28), P(0.9, -0.16) }, white, null);
        context.DrawEllipse(white, null, P(0.54, -0.56), size * 0.08, size * 0.055);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(12, 14, 16)), null, P(0.56, -0.56), size * 0.035, size * 0.028);
        DrawPolygon(context, new[] { P(-0.16, -0.4), P(-0.05, -0.18), P(0.18, -0.18), P(0.0, -0.02), P(0.06, 0.22), P(-0.16, 0.08), P(-0.36, 0.22), P(-0.28, -0.04), P(-0.48, -0.18), P(-0.22, -0.18) }, yellow, new Pen(new SolidColorBrush(Color.FromArgb(120, 122, 87, 0)), Math.Max(0.3, size * 0.028)));
        context.DrawEllipse(cyan, null, P(-1.18, -0.1), size * 0.05, size * 0.05);
        context.DrawEllipse(cyan, null, P(-0.98, 0.04), size * 0.055, size * 0.055);
        context.DrawLine(thin, P(-0.62, -0.38), P(0.1, -0.54));
        context.DrawLine(thin, P(-0.72, 0.18), P(-0.2, 0.46));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.15, size * 1.04);
    }

    private static void DrawBirdEnemyMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(222, 23, 18, 16)), Math.Max(0.78, size * 0.078));
        Pen leg = new(new SolidColorBrush(Color.FromRgb(83, 48, 17)), Math.Max(0.58, size * 0.05));
        IBrush black = new SolidColorBrush(Color.FromRgb(28, 25, 22));
        IBrush white = new SolidColorBrush(Color.FromRgb(242, 239, 229));
        IBrush purple = new SolidColorBrush(Color.FromRgb(123, 80, 103));
        IBrush beak = new SolidColorBrush(Color.FromRgb(232, 186, 36));
        IBrush red = new SolidColorBrush(Color.FromRgb(220, 49, 33));
        IBrush foot = new SolidColorBrush(Color.FromRgb(178, 129, 34));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, P(0.0, 0.96), size * 0.78, size * 0.18);
        DrawPolygon(context, new[] { P(-0.34, 0.7), P(0.36, 0.72), P(0.56, 0.18), P(0.08, -0.18), P(-0.36, 0.06) }, black, outline);
        DrawPolygon(context, new[] { P(-0.6, 0.12), P(-0.12, -0.24), P(0.04, 0.3), P(-0.38, 0.62) }, black, outline);
        DrawPolygon(context, new[] { P(-0.32, 0.0), P(0.38, 0.02), P(0.32, 0.32), P(-0.08, 0.52) }, white, outline);
        DrawPolygon(context, new[] { P(-0.1, -0.82), P(0.26, -0.92), P(0.54, -0.52), P(0.34, -0.16), P(0.0, -0.22) }, purple, outline);
        DrawPolygon(context, new[] { P(0.44, -0.58), P(0.96, -0.42), P(0.48, -0.24) }, beak, outline);
        DrawPolygon(context, new[] { P(0.56, -0.45), P(0.96, -0.42), P(0.56, -0.32) }, red, null);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(236, 238, 226)), null, P(0.24, -0.6), size * 0.06, size * 0.06);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(10, 12, 14)), null, P(0.26, -0.6), size * 0.028, size * 0.028);
        context.DrawLine(leg, P(-0.14, 0.68), P(-0.24, 1.02));
        context.DrawLine(leg, P(0.16, 0.68), P(0.24, 1.0));
        DrawPolygon(context, new[] { P(-0.3, 1.0), P(-0.08, 1.02), P(-0.24, 1.12) }, foot, null);
        DrawPolygon(context, new[] { P(0.16, 0.98), P(0.42, 0.98), P(0.24, 1.12) }, foot, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.02, size * 1.1);
    }

    private static void DrawBalloonistMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(225, 34, 28, 20)), Math.Max(0.72, size * 0.072));
        Pen stripe = new(new SolidColorBrush(Color.FromArgb(120, 44, 88, 116)), Math.Max(0.34, size * 0.03));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush skin = new SolidColorBrush(Color.FromRgb(204, 130, 62));
        IBrush skinDark = new SolidColorBrush(Color.FromRgb(132, 78, 38));
        IBrush scarf = new SolidColorBrush(Color.FromRgb(213, 42, 35));
        IBrush scarfDark = new SolidColorBrush(Color.FromRgb(124, 22, 27));
        IBrush shirt = new SolidColorBrush(Color.FromRgb(158, 207, 224));
        IBrush shirtLight = new SolidColorBrush(Color.FromRgb(198, 232, 238));
        IBrush shirtDark = new SolidColorBrush(Color.FromRgb(87, 137, 163));
        IBrush boot = new SolidColorBrush(Color.FromRgb(99, 62, 25));
        IBrush bootLight = new SolidColorBrush(Color.FromRgb(151, 94, 35));
        IBrush hair = new SolidColorBrush(Color.FromRgb(32, 25, 20));
        IBrush white = new SolidColorBrush(Color.FromRgb(244, 241, 226));

        context.DrawEllipse(shadow, null, P(0.02, 1.04), size * 0.86, size * 0.18);
        DrawPolygon(context, new[] { P(-0.52, 0.16), P(-0.92, 0.42), P(-0.72, 0.72), P(-0.38, 0.42) }, skinDark, outline);
        DrawPolygon(context, new[] { P(0.5, 0.08), P(0.88, 0.36), P(0.7, 0.7), P(0.34, 0.42) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.56, -0.02), P(0.54, -0.06), P(0.44, 0.74), P(-0.42, 0.74) }, shirt, outline);
        DrawPolygon(context, new[] { P(-0.46, -0.02), P(-0.1, -0.04), P(-0.18, 0.7), P(-0.4, 0.72) }, shirtLight, null);
        DrawPolygon(context, new[] { P(0.16, -0.06), P(0.54, -0.06), P(0.44, 0.74), P(0.1, 0.72) }, shirtDark, null);
        context.DrawLine(stripe, P(-0.28, 0.1), P(-0.3, 0.66));
        context.DrawLine(stripe, P(-0.04, 0.06), P(-0.08, 0.7));
        context.DrawLine(stripe, P(0.2, 0.02), P(0.18, 0.68));
        DrawPolygon(context, new[] { P(-0.44, 0.68), P(-0.12, 0.68), P(-0.34, 1.14), P(-0.74, 0.96) }, boot, outline);
        DrawPolygon(context, new[] { P(0.12, 0.68), P(0.42, 0.68), P(0.76, 0.96), P(0.32, 1.12) }, boot, outline);
        DrawPolygon(context, new[] { P(-0.38, 0.76), P(-0.16, 0.7), P(-0.34, 0.96) }, bootLight, null);
        DrawPolygon(context, new[] { P(0.24, 0.72), P(0.42, 0.7), P(0.56, 0.92) }, bootLight, null);
        DrawPolygon(context, new[] { P(-0.36, -0.58), P(-0.04, -0.88), P(0.42, -0.72), P(0.56, -0.3), P(0.28, 0.02), P(-0.22, -0.02), P(-0.5, -0.28) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.5, -0.34), P(-0.18, -0.74), P(0.16, -0.9), P(0.54, -0.62), P(0.28, -0.56), P(0.02, -0.66), P(-0.2, -0.5) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.18, -0.76), P(-0.28, -1.04), P(-0.08, -0.86) }, hair, outline);
        DrawPolygon(context, new[] { P(0.06, -0.86), P(0.08, -1.16), P(0.22, -0.86) }, hair, outline);
        DrawPolygon(context, new[] { P(0.28, -0.74), P(0.5, -0.98), P(0.42, -0.66) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.5, -0.22), P(0.42, -0.26), P(0.46, 0.12), P(-0.42, 0.16) }, scarf, outline);
        DrawPolygon(context, new[] { P(0.14, -0.22), P(0.42, -0.26), P(0.46, 0.12), P(0.06, 0.08) }, scarfDark, null);
        DrawPolygon(context, new[] { P(0.34, -0.02), P(0.72, 0.18), P(0.44, 0.26) }, scarf, outline);
        context.DrawEllipse(white, null, P(0.08, -0.48), size * 0.07, size * 0.06);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(15, 17, 20)), null, P(0.1, -0.48), size * 0.032, size * 0.028);
        context.DrawLine(new Pen(hair, Math.Max(0.38, size * 0.034)), P(-0.1, -0.94), P(-0.28, -1.16));
        context.DrawLine(new Pen(hair, Math.Max(0.38, size * 0.034)), P(0.18, -0.94), P(0.14, -1.2));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 0.98, size * 1.14);
    }

    private static void DrawFatLadyCauldronMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 88, 36, 16)), Math.Max(0.78, size * 0.078));
        Pen potLine = new(new SolidColorBrush(Color.FromArgb(150, 23, 63, 63)), Math.Max(0.42, size * 0.036));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush skin = new SolidColorBrush(Color.FromRgb(247, 144, 18));
        IBrush skinLight = new SolidColorBrush(Color.FromRgb(255, 189, 39));
        IBrush skinDark = new SolidColorBrush(Color.FromRgb(186, 78, 15));
        IBrush dress = new SolidColorBrush(Color.FromRgb(211, 20, 15));
        IBrush dressDark = new SolidColorBrush(Color.FromRgb(135, 12, 18));
        IBrush pot = new SolidColorBrush(Color.FromRgb(43, 117, 112));
        IBrush potLight = new SolidColorBrush(Color.FromRgb(73, 154, 149));
        IBrush potDark = new SolidColorBrush(Color.FromRgb(22, 62, 65));
        IBrush hair = new SolidColorBrush(Color.FromRgb(45, 122, 54));
        IBrush white = new SolidColorBrush(Color.FromRgb(246, 244, 232));
        IBrush boot = new SolidColorBrush(Color.FromRgb(111, 89, 26));
        IBrush spoon = new SolidColorBrush(Color.FromRgb(180, 123, 32));

        context.DrawEllipse(shadow, null, P(-0.2, 1.04), size * 1.08, size * 0.2);
        context.DrawEllipse(shadow, null, P(0.72, 0.98), size * 0.82, size * 0.18);

        DrawPolygon(context, new[] { P(-0.86, 0.02), P(-1.06, 0.58), P(-0.76, 0.72), P(-0.52, 0.18) }, skin, outline);
        DrawPolygon(context, new[] { P(0.12, 0.1), P(0.46, 0.38), P(0.3, 0.7), P(-0.02, 0.26) }, skinDark, outline);
        DrawPolygon(context, new[] { P(-0.72, -0.18), P(0.12, -0.2), P(0.32, 0.92), P(-0.58, 0.92) }, dress, outline);
        DrawPolygon(context, new[] { P(-0.16, -0.18), P(0.12, -0.2), P(0.32, 0.92), P(-0.08, 0.92) }, dressDark, null);
        DrawPolygon(context, new[] { P(-0.7, -0.28), P(-0.36, -0.5), P(-0.06, -0.3), P(-0.24, 0.0), P(-0.56, -0.02) }, skinLight, null);
        DrawPolygon(context, new[] { P(-0.56, 0.88), P(-0.28, 0.88), P(-0.48, 1.2), P(-0.76, 1.08) }, boot, outline);
        DrawPolygon(context, new[] { P(0.0, 0.88), P(0.28, 0.88), P(0.44, 1.08), P(0.12, 1.18) }, boot, outline);

        DrawPolygon(context, new[] { P(-0.68, -0.76), P(-0.32, -1.08), P(0.14, -0.9), P(0.24, -0.42), P(-0.1, -0.16), P(-0.52, -0.28) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.54, -0.62), P(-0.32, -1.08), P(0.14, -0.9), P(0.0, -0.68), P(-0.26, -0.6) }, skinLight, null);
        DrawPolygon(context, new[] { P(-0.2, -1.12), P(-0.06, -1.42), P(0.06, -1.1) }, skin, outline);
        context.DrawLine(new Pen(hair, Math.Max(0.4, size * 0.038)), P(-0.08, -1.1), P(-0.2, -1.34));
        context.DrawLine(new Pen(hair, Math.Max(0.4, size * 0.038)), P(-0.06, -1.1), P(0.0, -1.38));
        context.DrawLine(new Pen(hair, Math.Max(0.4, size * 0.038)), P(-0.04, -1.1), P(0.16, -1.3));
        context.DrawEllipse(white, null, P(-0.34, -0.68), size * 0.08, size * 0.05);
        context.DrawEllipse(white, null, P(-0.12, -0.7), size * 0.08, size * 0.05);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 18, 14)), null, P(-0.32, -0.68), size * 0.028, size * 0.024);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 18, 14)), null, P(-0.1, -0.7), size * 0.028, size * 0.024);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(119, 65, 68)), Math.Max(0.38, size * 0.032)), P(-0.24, -0.38), P(0.04, -0.42));

        DrawPolygon(context, new[] { P(0.26, 0.02), P(1.18, 0.02), P(1.04, 0.74), P(0.42, 0.76) }, pot, outline);
        DrawPolygon(context, new[] { P(0.26, 0.02), P(1.18, 0.02), P(1.08, 0.22), P(0.34, 0.24) }, potLight, null);
        DrawPolygon(context, new[] { P(0.84, 0.04), P(1.18, 0.02), P(1.04, 0.74), P(0.78, 0.74) }, potDark, null);
        DrawPolygon(context, new[] { P(0.44, 0.72), P(0.6, 0.72), P(0.5, 0.96) }, potDark, outline);
        DrawPolygon(context, new[] { P(0.88, 0.72), P(1.02, 0.72), P(0.98, 0.94) }, potDark, outline);
        context.DrawLine(potLine, P(0.34, 0.24), P(1.08, 0.22));
        context.DrawLine(new Pen(spoon, Math.Max(0.58, size * 0.052)), P(0.14, 0.22), P(0.54, 0.02));
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(80, 54, 36)), null, P(0.6, 0.0), size * 0.16, size * 0.05);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.18, size * 1.16);
    }

    private static void DrawArmoredDruidMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 24, 46, 20)), Math.Max(0.78, size * 0.078));
        Pen staff = new(new SolidColorBrush(Color.FromRgb(153, 101, 18)), Math.Max(0.8, size * 0.075));
        Pen spark = new(new SolidColorBrush(Color.FromArgb(180, 190, 246, 255)), Math.Max(0.42, size * 0.038));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush armor = new SolidColorBrush(Color.FromRgb(201, 237, 231));
        IBrush armorDark = new SolidColorBrush(Color.FromRgb(52, 103, 101));
        IBrush robe = new SolidColorBrush(Color.FromRgb(45, 101, 35));
        IBrush robeDark = new SolidColorBrush(Color.FromRgb(18, 55, 24));
        IBrush hair = new SolidColorBrush(Color.FromRgb(143, 220, 44));
        IBrush hairDark = new SolidColorBrush(Color.FromRgb(138, 98, 22));
        IBrush face = new SolidColorBrush(Color.FromRgb(242, 174, 86));
        IBrush eye = new SolidColorBrush(Color.FromRgb(244, 250, 243));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(10, 18, 16));
        IBrush gem = new SolidColorBrush(Color.FromRgb(247, 48, 29));
        IBrush gemLight = new SolidColorBrush(Color.FromRgb(255, 173, 39));

        context.DrawEllipse(shadow, null, P(0.0, 0.96), size * 1.0, size * 0.2);
        DrawPolygon(context, new[] { P(-0.68, 0.82), P(-0.52, -0.08), P(0.06, -0.32), P(0.62, 0.08), P(0.48, 0.84) }, armor, outline);
        DrawPolygon(context, new[] { P(0.02, -0.28), P(0.62, 0.08), P(0.48, 0.84), P(0.08, 0.54) }, armorDark, null);
        DrawPolygon(context, new[] { P(-0.66, 0.0), P(-1.08, 0.44), P(-0.58, 0.44) }, robe, outline);
        DrawPolygon(context, new[] { P(0.36, -0.08), P(0.86, 0.18), P(0.48, 0.5) }, robeDark, outline);
        DrawPolygon(context, new[] { P(-0.34, -0.26), P(-0.1, -0.62), P(0.18, -0.42), P(0.08, -0.16) }, face, outline);
        context.DrawEllipse(eye, null, P(-0.22, -0.36), size * 0.12, size * 0.07);
        context.DrawEllipse(pupil, null, P(-0.2, -0.36), size * 0.045, size * 0.028);
        DrawPolygon(context, new[] { P(-0.58, -0.56), P(-0.28, -1.02), P(-0.14, -0.52) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.28, -0.66), P(0.04, -1.08), P(0.04, -0.5) }, hairDark, outline);
        DrawPolygon(context, new[] { P(0.0, -0.6), P(0.48, -0.96), P(0.18, -0.42) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.48, -0.26), P(-0.98, -0.44), P(-0.66, -0.06) }, hair, outline);
        DrawPolygon(context, new[] { P(0.18, -0.26), P(0.7, -0.4), P(0.28, -0.02) }, robeDark, outline);

        context.DrawLine(staff, P(0.98, 0.8), P(0.98, -1.06));
        DrawPolygon(context, new[] { P(0.78, -1.08), P(1.08, -1.32), P(1.34, -1.0), P(1.08, -0.72) }, gem, outline);
        DrawPolygon(context, new[] { P(0.9, -1.06), P(1.08, -1.32), P(1.28, -1.04), P(1.02, -0.98) }, gemLight, null);
        context.DrawLine(spark, P(0.72, -1.24), P(0.54, -1.02));
        context.DrawLine(spark, P(1.34, -1.26), P(1.54, -1.08));
        context.DrawLine(spark, P(1.38, -0.74), P(1.58, -0.58));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.16, size * 1.18);
    }

    private static void DrawGreenDruidMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 13, 38, 17)), Math.Max(0.78, size * 0.078));
        Pen hairLine = new(new SolidColorBrush(Color.FromRgb(106, 122, 54)), Math.Max(0.42, size * 0.04));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush robe = new SolidColorBrush(Color.FromRgb(34, 105, 36));
        IBrush robeLight = new SolidColorBrush(Color.FromRgb(91, 171, 57));
        IBrush robeDark = new SolidColorBrush(Color.FromRgb(12, 58, 25));
        IBrush beard = new SolidColorBrush(Color.FromRgb(190, 194, 94));
        IBrush hair = new SolidColorBrush(Color.FromRgb(144, 148, 78));
        IBrush nose = new SolidColorBrush(Color.FromRgb(225, 112, 54));
        IBrush eye = new SolidColorBrush(Color.FromRgb(246, 250, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(11, 18, 14));
        IBrush orb = new SolidColorBrush(Color.FromRgb(93, 210, 84));

        context.DrawEllipse(shadow, null, P(0.0, 1.0), size * 1.0, size * 0.2);
        DrawPolygon(context, new[] { P(-0.62, 0.98), P(0.62, 0.98), P(0.36, -0.22), P(-0.36, -0.22) }, robe, outline);
        DrawPolygon(context, new[] { P(-0.36, -0.22), P(-1.08, -1.04), P(-0.7, -0.02) }, robeLight, outline);
        DrawPolygon(context, new[] { P(0.36, -0.22), P(1.08, -1.04), P(0.7, -0.02) }, robeDark, outline);
        DrawPolygon(context, new[] { P(-0.42, -0.34), P(0.38, -0.36), P(0.18, 0.46), P(-0.22, 0.48) }, beard, outline);
        DrawPolygon(context, new[] { P(-0.28, -0.72), P(0.0, -1.0), P(0.36, -0.72), P(0.26, -0.34), P(-0.22, -0.34) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.18, -0.5), P(0.12, -0.48), P(0.04, -0.2), P(-0.2, -0.24) }, nose, outline);
        context.DrawEllipse(eye, null, P(-0.28, -0.48), size * 0.12, size * 0.08);
        context.DrawEllipse(eye, null, P(0.22, -0.5), size * 0.12, size * 0.08);
        context.DrawEllipse(pupil, null, P(-0.24, -0.48), size * 0.045, size * 0.032);
        context.DrawEllipse(pupil, null, P(0.18, -0.5), size * 0.045, size * 0.032);
        context.DrawLine(hairLine, P(-0.08, -0.98), P(-0.26, -1.28));
        context.DrawLine(hairLine, P(0.06, -0.98), P(0.0, -1.3));
        context.DrawLine(hairLine, P(0.16, -0.94), P(0.34, -1.2));
        context.DrawEllipse(orb, outline, P(0.56, -1.38), size * 0.18, size * 0.18);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.08, size * 1.18);
    }

    private static void DrawGreenWizardMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 13, 55, 25)), Math.Max(0.78, size * 0.078));
        Pen lightning = new(new SolidColorBrush(Color.FromArgb(226, 252, 250, 255)), Math.Max(0.58, size * 0.055));
        Pen rain = new(new SolidColorBrush(Color.FromArgb(150, 130, 170, 107)), Math.Max(0.34, size * 0.032));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush robe = new SolidColorBrush(Color.FromRgb(48, 185, 95));
        IBrush robeLight = new SolidColorBrush(Color.FromRgb(104, 231, 138));
        IBrush robeDark = new SolidColorBrush(Color.FromRgb(20, 118, 52));
        IBrush hat = new SolidColorBrush(Color.FromRgb(45, 213, 95));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(16, 112, 50));
        IBrush eye = new SolidColorBrush(Color.FromRgb(248, 252, 240));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(12, 18, 14));

        context.DrawEllipse(shadow, null, P(0.0, 1.0), size * 0.94, size * 0.18);
        DrawPolygon(context, new[] { P(-0.7, 0.98), P(0.64, 0.98), P(0.42, -0.2), P(-0.42, -0.2) }, robe, outline);
        DrawPolygon(context, new[] { P(-0.54, -0.04), P(-1.08, 0.08), P(-0.48, 0.42) }, robeLight, outline);
        DrawPolygon(context, new[] { P(0.48, -0.04), P(1.02, 0.08), P(0.46, 0.42) }, robeDark, outline);
        DrawPolygon(context, new[] { P(-0.98, -0.34), P(1.02, -0.42), P(0.44, -0.78), P(-0.48, -0.72) }, hatDark, outline);
        DrawPolygon(context, new[] { P(-0.42, -0.72), P(0.16, -1.34), P(0.54, -0.42), P(-0.18, -0.38) }, hat, outline);
        DrawPolygon(context, new[] { P(0.16, -1.34), P(0.54, -0.42), P(0.2, -0.5) }, robeLight, null);
        context.DrawEllipse(eye, null, P(-0.18, -0.34), size * 0.12, size * 0.07);
        context.DrawEllipse(eye, null, P(0.12, -0.34), size * 0.12, size * 0.07);
        context.DrawEllipse(pupil, null, P(-0.14, -0.34), size * 0.04, size * 0.028);
        context.DrawEllipse(pupil, null, P(0.08, -0.34), size * 0.04, size * 0.028);
        context.DrawLine(lightning, P(-0.72, -1.46), P(-1.0, -1.06));
        context.DrawLine(lightning, P(-1.0, -1.06), P(-0.78, -1.02));
        context.DrawLine(lightning, P(-0.78, -1.02), P(-1.06, -0.56));
        context.DrawLine(rain, P(0.48, -1.18), P(0.42, -0.9));
        context.DrawLine(rain, P(0.82, -1.06), P(0.78, -0.78));
        context.DrawLine(rain, P(-0.28, -1.14), P(-0.32, -0.86));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.08, size * 1.18);
    }

    private static void DrawElderWizardMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 11, 25, 54)), Math.Max(0.78, size * 0.078));
        Pen beardLine = new(new SolidColorBrush(Color.FromArgb(130, 102, 202, 232)), Math.Max(0.36, size * 0.032));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush robe = new SolidColorBrush(Color.FromRgb(93, 171, 221));
        IBrush robeLight = new SolidColorBrush(Color.FromRgb(183, 228, 246));
        IBrush robeDark = new SolidColorBrush(Color.FromRgb(35, 92, 171));
        IBrush sleeve = new SolidColorBrush(Color.FromRgb(116, 198, 238));
        IBrush sleeveDark = new SolidColorBrush(Color.FromRgb(41, 99, 178));
        IBrush hat = new SolidColorBrush(Color.FromRgb(19, 60, 126));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(9, 25, 68));
        IBrush hatLight = new SolidColorBrush(Color.FromRgb(48, 109, 190));
        IBrush faceDark = new SolidColorBrush(Color.FromRgb(8, 17, 40));
        IBrush beard = new SolidColorBrush(Color.FromRgb(222, 250, 255));
        IBrush beardShade = new SolidColorBrush(Color.FromRgb(151, 218, 237));
        IBrush eye = new SolidColorBrush(Color.FromRgb(252, 254, 246));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(10, 14, 22));
        IBrush sail = new SolidColorBrush(Color.FromRgb(248, 246, 236));
        IBrush sailBlue = new SolidColorBrush(Color.FromRgb(42, 106, 192));

        context.DrawEllipse(shadow, null, P(0.02, 1.0), size * 1.0, size * 0.2);
        DrawPolygon(context, new[] { P(-0.98, -0.1), P(-0.38, -0.28), P(-0.34, 0.12), P(-1.08, 0.24) }, sleeve, outline);
        DrawPolygon(context, new[] { P(0.38, -0.28), P(1.08, -0.04), P(0.98, 0.24), P(0.34, 0.12) }, sleeveDark, outline);
        DrawPolygon(context, new[] { P(-0.62, 0.98), P(0.6, 0.98), P(0.42, -0.28), P(-0.42, -0.28) }, robe, outline);
        DrawPolygon(context, new[] { P(-0.42, -0.24), P(-0.62, 0.98), P(-0.1, 0.98), P(0.02, -0.2) }, robeLight, null);
        DrawPolygon(context, new[] { P(0.08, -0.22), P(0.6, 0.98), P(0.18, 0.98), P(0.32, -0.22) }, robeDark, null);

        DrawPolygon(context, new[] { P(-0.72, -0.62), P(0.02, -1.38), P(0.82, -0.7), P(0.66, -0.34), P(-0.58, -0.32) }, hat, outline);
        DrawPolygon(context, new[] { P(0.02, -1.38), P(0.82, -0.7), P(0.26, -0.66) }, hatLight, null);
        DrawPolygon(context, new[] { P(-1.04, -0.46), P(-0.34, -0.76), P(0.86, -0.64), P(1.1, -0.4), P(0.4, -0.18), P(-0.78, -0.22) }, hatDark, outline);
        DrawPolygon(context, new[] { P(-0.64, -0.48), P(0.18, -0.54), P(0.52, -0.34), P(0.2, -0.18), P(-0.5, -0.22) }, faceDark, null);

        context.DrawEllipse(eye, null, P(-0.18, -0.36), size * 0.13, size * 0.075);
        context.DrawEllipse(eye, null, P(0.18, -0.36), size * 0.13, size * 0.075);
        context.DrawEllipse(pupil, null, P(-0.14, -0.36), size * 0.043, size * 0.03);
        context.DrawEllipse(pupil, null, P(0.14, -0.36), size * 0.043, size * 0.03);

        DrawPolygon(context, new[]
        {
            P(-0.38, -0.18),
            P(0.38, -0.18),
            P(0.26, 0.24),
            P(0.06, 0.18),
            P(-0.04, 0.48),
            P(-0.18, 0.2),
            P(-0.34, 0.28)
        }, beard, outline);
        DrawPolygon(context, new[] { P(0.04, -0.14), P(0.38, -0.18), P(0.26, 0.24), P(0.0, 0.2) }, beardShade, null);
        context.DrawLine(beardLine, P(-0.22, 0.02), P(-0.32, 0.24));
        context.DrawLine(beardLine, P(0.06, -0.02), P(-0.02, 0.32));
        context.DrawLine(beardLine, P(0.22, 0.0), P(0.12, 0.22));

        DrawPolygon(context, new[] { P(-0.08, -0.94), P(0.18, -1.06), P(0.08, -0.78) }, sail, outline);
        DrawPolygon(context, new[] { P(0.02, -0.84), P(0.18, -0.9), P(0.08, -0.78) }, sailBlue, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.1, size * 1.18);
    }

    private static void DrawTornadoWizardMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 36, 20, 34)), Math.Max(0.78, size * 0.078));
        Pen swirl = new(new SolidColorBrush(Color.FromArgb(218, 250, 248, 255)), Math.Max(0.66, size * 0.06));
        Pen swirlSoft = new(new SolidColorBrush(Color.FromArgb(135, 240, 232, 255)), Math.Max(0.52, size * 0.046));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(68, 0, 0, 0));
        IBrush hat = new SolidColorBrush(Color.FromRgb(54, 16, 42));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(24, 7, 22));
        IBrush collar = new SolidColorBrush(Color.FromRgb(17, 101, 117));
        IBrush collarLight = new SolidColorBrush(Color.FromRgb(43, 157, 166));
        IBrush robe = new SolidColorBrush(Color.FromRgb(251, 203, 31));
        IBrush robeLight = new SolidColorBrush(Color.FromRgb(255, 230, 88));
        IBrush skin = new SolidColorBrush(Color.FromRgb(217, 132, 89));
        IBrush skinDark = new SolidColorBrush(Color.FromRgb(159, 73, 66));
        IBrush hair = new SolidColorBrush(Color.FromRgb(114, 205, 42));
        IBrush eye = new SolidColorBrush(Color.FromRgb(247, 248, 236));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(13, 17, 16));
        IBrush tornado = new SolidColorBrush(Color.FromArgb(104, 238, 226, 250));
        IBrush tornadoShade = new SolidColorBrush(Color.FromArgb(92, 143, 114, 197));
        IBrush sparkle = new SolidColorBrush(Color.FromRgb(104, 239, 232));

        context.DrawEllipse(shadow, null, P(0.0, 1.04), size * 0.92, size * 0.2);
        DrawPolygon(context, new[] { P(-0.26, -0.02), P(0.34, -0.04), P(0.22, 0.34), P(0.06, 1.1), P(-0.34, 0.44) }, tornado, outline);
        DrawPolygon(context, new[] { P(-0.02, 0.12), P(0.34, -0.04), P(0.22, 0.34), P(0.0, 0.54) }, tornadoShade, null);
        context.DrawLine(swirl, P(-0.56, 0.0), P(0.52, 0.02));
        context.DrawLine(swirlSoft, P(-0.68, 0.3), P(0.42, 0.3));
        context.DrawLine(swirl, P(-0.54, 0.62), P(0.26, 0.62));
        context.DrawLine(swirlSoft, P(-0.38, 0.92), P(0.12, 0.92));

        DrawPolygon(context, new[] { P(-1.06, -0.28), P(-1.34, 0.04), P(-0.72, -0.08), P(-0.22, -0.34) }, skin, outline);
        DrawPolygon(context, new[] { P(0.34, -0.34), P(0.96, -0.08), P(1.32, -0.26), P(0.64, -0.52) }, skinDark, outline);
        DrawPolygon(context, new[] { P(-0.7, -0.52), P(-0.1, -0.76), P(0.56, -0.56), P(0.62, -0.24), P(-0.28, -0.2) }, collar, outline);
        DrawPolygon(context, new[] { P(-0.62, -0.48), P(-0.1, -0.76), P(0.08, -0.28), P(-0.28, -0.2) }, collarLight, null);
        DrawPolygon(context, new[] { P(-0.28, -0.2), P(0.24, -0.22), P(0.14, 0.22), P(-0.12, 0.28) }, robe, outline);
        DrawPolygon(context, new[] { P(-0.22, -0.16), P(0.02, -0.18), P(-0.04, 0.22), P(-0.12, 0.28) }, robeLight, null);
        DrawPolygon(context, new[] { P(-0.26, -0.78), P(0.02, -0.98), P(0.32, -0.8), P(0.36, -0.48), P(0.0, -0.34), P(-0.34, -0.48) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.54, -0.76), P(-0.24, -1.0), P(0.38, -0.92), P(0.58, -0.66), P(0.06, -0.72) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.84, -0.92), P(-0.06, -1.46), P(0.52, -0.9), P(0.64, -0.58), P(-0.44, -0.62) }, hat, outline);
        DrawPolygon(context, new[] { P(-0.06, -1.46), P(0.52, -0.9), P(0.2, -0.84) }, hatDark, null);
        DrawPolygon(context, new[] { P(-1.02, -0.64), P(-0.32, -0.88), P(0.7, -0.72), P(0.94, -0.52), P(0.22, -0.34), P(-0.76, -0.42) }, hatDark, outline);
        context.DrawEllipse(eye, null, P(-0.1, -0.58), size * 0.09, size * 0.065);
        context.DrawEllipse(eye, null, P(0.18, -0.58), size * 0.09, size * 0.065);
        context.DrawEllipse(pupil, null, P(-0.07, -0.58), size * 0.03, size * 0.025);
        context.DrawEllipse(pupil, null, P(0.15, -0.58), size * 0.03, size * 0.025);
        context.DrawEllipse(sparkle, null, P(0.92, 0.28), size * 0.06, size * 0.06);
        context.DrawLine(new Pen(sparkle, Math.Max(0.34, size * 0.03)), P(1.02, 0.22), P(1.18, 0.06));
        context.DrawLine(new Pen(sparkle, Math.Max(0.34, size * 0.03)), P(1.04, 0.4), P(1.2, 0.56));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.18);
    }

    private static void DrawMetalbackSpiderMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 16, 18, 33)), Math.Max(0.76, size * 0.076));
        Pen leg = new(new SolidColorBrush(Color.FromRgb(23, 43, 104)), Math.Max(0.78, size * 0.074));
        Pen legDark = new(new SolidColorBrush(Color.FromRgb(8, 18, 45)), Math.Max(0.58, size * 0.052));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(77, 71, 146));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(117, 104, 198));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(37, 35, 91));
        IBrush shell = new SolidColorBrush(Color.FromRgb(212, 242, 236));
        IBrush shellShade = new SolidColorBrush(Color.FromRgb(134, 181, 186));
        IBrush face = new SolidColorBrush(Color.FromRgb(232, 249, 245));
        IBrush mouth = new SolidColorBrush(Color.FromRgb(178, 61, 155));
        IBrush eye = new SolidColorBrush(Color.FromRgb(248, 250, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(15, 16, 24));

        context.DrawEllipse(shadow, null, P(0.08, 0.98), size * 1.1, size * 0.2);
        context.DrawLine(leg, P(-0.48, -0.1), P(-1.04, -0.64));
        context.DrawLine(legDark, P(-1.04, -0.64), P(-1.46, -0.34));
        context.DrawLine(leg, P(-0.5, 0.1), P(-1.18, 0.2));
        context.DrawLine(legDark, P(-1.18, 0.2), P(-1.5, 0.68));
        context.DrawLine(leg, P(0.52, -0.12), P(1.04, -0.66));
        context.DrawLine(legDark, P(1.04, -0.66), P(1.46, -0.34));
        context.DrawLine(leg, P(0.52, 0.12), P(1.18, 0.22));
        context.DrawLine(legDark, P(1.18, 0.22), P(1.5, 0.68));

        DrawPolygon(context, new[] { P(-0.44, -0.48), P(0.48, -0.48), P(0.76, 0.0), P(0.38, 0.48), P(-0.38, 0.48), P(-0.76, 0.0) }, body, outline);
        DrawPolygon(context, new[] { P(-0.24, -0.5), P(0.48, -0.48), P(0.76, 0.0), P(0.14, -0.02) }, bodyLight, null);
        DrawPolygon(context, new[] { P(0.14, -0.02), P(0.76, 0.0), P(0.38, 0.48), P(-0.08, 0.3) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.86, -0.2), P(-0.18, -0.54), P(0.34, -0.24), P(0.18, 0.34), P(-0.42, 0.46), P(-0.88, 0.16) }, shell, outline);
        DrawPolygon(context, new[] { P(-0.2, -0.52), P(0.34, -0.24), P(0.18, 0.34), P(-0.12, 0.06) }, shellShade, null);
        DrawPolygon(context, new[] { P(-0.7, -0.08), P(-0.18, -0.22), P(0.12, -0.06), P(-0.24, 0.14), P(-0.72, 0.12) }, face, null);
        DrawPolygon(context, new[] { P(-0.34, 0.1), P(0.14, -0.04), P(0.22, 0.16), P(-0.12, 0.26) }, mouth, null);
        context.DrawEllipse(eye, null, P(-0.2, -0.18), size * 0.08, size * 0.055);
        context.DrawEllipse(pupil, null, P(-0.18, -0.18), size * 0.03, size * 0.024);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 50, 70, 112)), Math.Max(0.32, size * 0.03)), P(0.0, -0.4), P(0.36, -0.28));
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 50, 70, 112)), Math.Max(0.32, size * 0.03)), P(0.18, 0.34), P(-0.18, 0.42));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.18, size * 1.04);
    }

    private static void DrawSpottedChickenMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 66, 39, 27)), Math.Max(0.78, size * 0.078));
        Pen legPen = new(new SolidColorBrush(Color.FromRgb(129, 65, 24)), Math.Max(0.48, size * 0.05));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(154, 105, 69));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(198, 150, 99));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(96, 57, 45));
        IBrush spot = new SolidColorBrush(Color.FromRgb(77, 43, 34));
        IBrush beak = new SolidColorBrush(Color.FromRgb(242, 196, 37));
        IBrush beakDark = new SolidColorBrush(Color.FromRgb(196, 92, 35));
        IBrush comb = new SolidColorBrush(Color.FromRgb(189, 62, 58));
        IBrush eye = new SolidColorBrush(Color.FromRgb(248, 250, 239));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(26, 22, 21));

        context.DrawEllipse(shadow, null, P(0.02, 0.98), size * 1.08, size * 0.22);
        DrawPolygon(context, new[]
        {
            P(-0.78, 0.42),
            P(-0.68, -0.28),
            P(-0.24, -0.78),
            P(0.38, -0.74),
            P(0.74, -0.28),
            P(0.68, 0.42),
            P(0.28, 0.76),
            P(-0.4, 0.7)
        }, body, outline);
        DrawPolygon(context, new[] { P(-0.7, -0.12), P(-1.02, 0.12), P(-0.68, 0.46), P(-0.34, 0.2) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(0.5, -0.1), P(0.92, 0.1), P(0.6, 0.42), P(0.26, 0.18) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(-0.48, -0.38), P(-0.12, -0.7), P(0.32, -0.66), P(0.08, -0.22) }, bodyLight, null);

        context.DrawEllipse(spot, null, P(-0.42, -0.36), size * 0.08, size * 0.06);
        context.DrawEllipse(spot, null, P(-0.08, -0.48), size * 0.07, size * 0.055);
        context.DrawEllipse(spot, null, P(0.24, -0.32), size * 0.08, size * 0.06);
        context.DrawEllipse(spot, null, P(-0.3, 0.02), size * 0.08, size * 0.06);
        context.DrawEllipse(spot, null, P(0.12, 0.18), size * 0.07, size * 0.05);
        context.DrawEllipse(spot, null, P(0.4, -0.06), size * 0.065, size * 0.05);

        DrawPolygon(context, new[] { P(-0.18, -0.8), P(-0.04, -1.12), P(0.14, -0.8) }, comb, outline);
        DrawPolygon(context, new[] { P(0.02, -0.76), P(0.28, -1.02), P(0.32, -0.68) }, comb, outline);
        context.DrawEllipse(eye, outline, P(-0.2, -0.44), size * 0.18, size * 0.2);
        context.DrawEllipse(eye, outline, P(0.22, -0.44), size * 0.18, size * 0.2);
        context.DrawEllipse(pupil, null, P(-0.16, -0.42), size * 0.06, size * 0.07);
        context.DrawEllipse(pupil, null, P(0.18, -0.42), size * 0.06, size * 0.07);
        DrawPolygon(context, new[] { P(-0.1, -0.2), P(0.46, -0.12), P(0.04, 0.16) }, beak, outline);
        DrawPolygon(context, new[] { P(0.04, 0.16), P(0.38, 0.14), P(0.08, 0.34) }, beakDark, outline);

        context.DrawLine(legPen, P(-0.2, 0.7), P(-0.24, 1.04));
        context.DrawLine(legPen, P(0.18, 0.7), P(0.22, 1.04));
        context.DrawLine(legPen, P(-0.24, 1.04), P(-0.38, 1.12));
        context.DrawLine(legPen, P(0.22, 1.04), P(0.4, 1.1));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.1, size * 1.12);
    }

    private static void DrawBoarMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 49, 31, 28)), Math.Max(0.84, size * 0.082));
        Pen hairPen = new(new SolidColorBrush(Color.FromRgb(38, 21, 20)), Math.Max(0.45, size * 0.045));
        Pen legPen = new(new SolidColorBrush(Color.FromRgb(36, 28, 27)), Math.Max(0.62, size * 0.06));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush fur = new SolidColorBrush(Color.FromRgb(132, 93, 83));
        IBrush furLight = new SolidColorBrush(Color.FromRgb(177, 130, 117));
        IBrush furDark = new SolidColorBrush(Color.FromRgb(78, 54, 55));
        IBrush snout = new SolidColorBrush(Color.FromRgb(226, 78, 77));
        IBrush snoutDark = new SolidColorBrush(Color.FromRgb(120, 43, 45));
        IBrush eyeWhite = new SolidColorBrush(Color.FromRgb(248, 248, 239));
        IBrush eyeDark = new SolidColorBrush(Color.FromRgb(16, 15, 21));
        IBrush tusk = new SolidColorBrush(Color.FromRgb(232, 229, 228));

        context.DrawEllipse(shadow, null, P(0.02, 0.98), size * 1.18, size * 0.23);
        DrawPolygon(context, new[] { P(-0.86, -0.42), P(-1.24, 0.1), P(-0.72, 0.42), P(-0.42, -0.08) }, furDark, outline);
        DrawPolygon(context, new[] { P(0.86, -0.42), P(1.24, 0.1), P(0.72, 0.42), P(0.42, -0.08) }, furDark, outline);
        DrawPolygon(context, new[] { P(-0.62, -0.84), P(-0.38, -1.32), P(-0.04, -0.8), P(-0.26, -0.48) }, furLight, outline);
        DrawPolygon(context, new[] { P(0.62, -0.84), P(0.38, -1.32), P(0.04, -0.8), P(0.26, -0.48) }, furLight, outline);
        DrawPolygon(context, new[]
        {
            P(-0.86, -0.46),
            P(-0.48, -0.96),
            P(0.48, -0.96),
            P(0.86, -0.46),
            P(0.72, 0.46),
            P(0.3, 0.9),
            P(-0.3, 0.9),
            P(-0.72, 0.46)
        }, fur, outline);
        DrawPolygon(context, new[] { P(-0.34, -0.82), P(0.28, -0.88), P(0.56, -0.28), P(0.08, 0.02), P(-0.52, -0.16) }, furLight, null);
        DrawPolygon(context, new[] { P(0.34, -0.7), P(0.86, -0.46), P(0.72, 0.46), P(0.24, 0.38), P(0.08, 0.02) }, furDark, null);

        DrawPolygon(context, new[] { P(-0.52, -0.42), P(-0.06, -0.58), P(0.44, -0.42), P(0.22, -0.2), P(-0.28, -0.22) }, eyeWhite, null);
        context.DrawEllipse(eyeDark, null, P(-0.2, -0.4), size * 0.14, size * 0.08);
        context.DrawEllipse(eyeDark, null, P(0.24, -0.4), size * 0.14, size * 0.08);
        DrawPolygon(context, new[] { P(-0.92, -0.1), P(-1.2, 0.12), P(-0.78, 0.28) }, tusk, outline);
        DrawPolygon(context, new[] { P(0.92, -0.1), P(1.2, 0.12), P(0.78, 0.28) }, tusk, outline);

        DrawPolygon(context, new[] { P(-0.38, 0.1), P(0.38, 0.1), P(0.46, 0.55), P(0.18, 0.76), P(-0.18, 0.76), P(-0.46, 0.55) }, snout, outline);
        context.DrawEllipse(snoutDark, null, P(-0.18, 0.34), size * 0.08, size * 0.08);
        context.DrawEllipse(snoutDark, null, P(0.18, 0.34), size * 0.08, size * 0.08);
        DrawPolygon(context, new[] { P(-0.28, 0.64), P(0.28, 0.64), P(0.2, 0.86), P(-0.18, 0.86) }, new SolidColorBrush(Color.FromRgb(45, 23, 24)), null);
        DrawPolygon(context, new[] { P(-0.18, 0.74), P(-0.04, 0.74), P(-0.1, 0.88) }, new SolidColorBrush(Color.FromRgb(232, 229, 228)), null);
        DrawPolygon(context, new[] { P(0.06, 0.74), P(0.2, 0.74), P(0.12, 0.88) }, new SolidColorBrush(Color.FromRgb(232, 229, 228)), null);

        context.DrawLine(hairPen, P(-0.22, -0.98), P(-0.26, -1.2));
        context.DrawLine(hairPen, P(0.0, -1.0), P(0.02, -1.24));
        context.DrawLine(hairPen, P(0.22, -0.98), P(0.28, -1.18));
        context.DrawLine(legPen, P(-0.52, 0.48), P(-0.58, 0.92));
        context.DrawLine(legPen, P(0.52, 0.48), P(0.58, 0.92));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.14);
    }

    private static void DrawDragonEatingPlantMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 41, 51, 27)), Math.Max(0.82, size * 0.082));
        Pen leafLine = new(new SolidColorBrush(Color.FromRgb(37, 111, 55)), Math.Max(0.5, size * 0.045));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(84, 78, 43));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(123, 112, 58));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(48, 43, 26));
        IBrush lip = new SolidColorBrush(Color.FromRgb(64, 55, 31));
        IBrush leaf = new SolidColorBrush(Color.FromRgb(69, 173, 91));
        IBrush leafLight = new SolidColorBrush(Color.FromRgb(115, 232, 126));
        IBrush leafDark = new SolidColorBrush(Color.FromRgb(25, 104, 56));
        IBrush eye = new SolidColorBrush(Color.FromRgb(238, 236, 220));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(15, 15, 14));
        IBrush tooth = new SolidColorBrush(Color.FromRgb(228, 226, 211));

        context.DrawEllipse(shadow, null, P(0.02, 1.0), size * 1.2, size * 0.23);
        DrawPolygon(context, new[] { P(-0.58, -0.78), P(-0.76, -1.72), P(-0.24, -0.86), P(-0.1, -0.38) }, leafDark, outline);
        DrawPolygon(context, new[] { P(-0.16, -0.82), P(0.0, -1.92), P(0.28, -0.72), P(0.12, -0.34) }, leaf, outline);
        DrawPolygon(context, new[] { P(0.3, -0.72), P(0.86, -1.56), P(0.62, -0.5), P(0.24, -0.28) }, leafLight, outline);
        DrawPolygon(context, new[] { P(0.58, -0.32), P(1.28, -0.74), P(0.82, -0.06), P(0.22, 0.0) }, leaf, outline);
        context.DrawLine(leafLine, P(-0.56, -0.72), P(-0.68, -1.42));
        context.DrawLine(leafLine, P(0.0, -0.8), P(0.0, -1.62));
        context.DrawLine(leafLine, P(0.44, -0.66), P(0.76, -1.22));

        DrawPolygon(context, new[]
        {
            P(-0.96, -0.26),
            P(-0.56, -0.76),
            P(0.0, -0.7),
            P(0.38, -0.86),
            P(0.86, -0.36),
            P(0.66, 0.18),
            P(-0.74, 0.2)
        }, bodyLight, outline);
        DrawPolygon(context, new[]
        {
            P(-0.96, -0.26),
            P(-0.74, 0.2),
            P(-0.62, 0.72),
            P(0.58, 0.72),
            P(0.84, 0.16),
            P(0.66, 0.18)
        }, body, outline);
        DrawPolygon(context, new[] { P(0.0, -0.7), P(0.38, -0.86), P(0.86, -0.36), P(0.28, -0.2) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.68, -0.14), P(-0.2, -0.22), P(-0.04, 0.1), P(-0.76, 0.1) }, eye, outline);
        DrawPolygon(context, new[] { P(0.16, -0.2), P(0.64, -0.16), P(0.72, 0.08), P(0.02, 0.1) }, eye, outline);
        context.DrawEllipse(pupil, null, P(-0.42, -0.04), size * 0.1, size * 0.06);
        context.DrawEllipse(pupil, null, P(0.36, -0.04), size * 0.1, size * 0.06);
        DrawPolygon(context, new[] { P(-0.22, -0.42), P(0.16, -0.64), P(0.44, -0.28), P(0.08, -0.06) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(-0.78, 0.08), P(0.78, 0.08), P(0.62, 0.32), P(-0.66, 0.32) }, lip, null);
        DrawPolygon(context, new[] { P(-0.18, 0.12), P(-0.06, 0.12), P(-0.12, 0.28) }, tooth, null);
        DrawPolygon(context, new[] { P(0.14, 0.12), P(0.26, 0.12), P(0.2, 0.28) }, tooth, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.16);
    }

    private static void DrawAttackFrogMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 20, 71, 96)), Math.Max(0.82, size * 0.082));
        Pen mouthPen = new(new SolidColorBrush(Color.FromRgb(80, 61, 151)), Math.Max(0.76, size * 0.078));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(76, 192, 213));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(126, 232, 239));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(40, 129, 177));
        IBrush belly = new SolidColorBrush(Color.FromRgb(211, 181, 111));
        IBrush bellyShade = new SolidColorBrush(Color.FromRgb(152, 134, 92));
        IBrush eye = new SolidColorBrush(Color.FromRgb(246, 248, 239));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(16, 16, 20));
        IBrush antenna = new SolidColorBrush(Color.FromRgb(235, 159, 29));

        context.DrawEllipse(shadow, null, P(0.0, 1.02), size * 1.18, size * 0.23);
        DrawPolygon(context, new[] { P(-0.82, 0.0), P(-1.14, 0.58), P(-0.72, 0.8), P(-0.42, 0.18) }, bodyDark, outline);
        DrawPolygon(context, new[] { P(0.82, 0.0), P(1.14, 0.58), P(0.72, 0.8), P(0.42, 0.18) }, bodyDark, outline);
        DrawPolygon(context, new[]
        {
            P(-0.92, -0.5),
            P(-0.44, -0.94),
            P(0.44, -0.94),
            P(0.92, -0.5),
            P(0.84, 0.28),
            P(0.46, 0.78),
            P(-0.46, 0.78),
            P(-0.84, 0.28)
        }, body, outline);
        DrawPolygon(context, new[] { P(-0.52, -0.72), P(0.3, -0.88), P(0.72, -0.38), P(0.16, -0.18), P(-0.64, -0.26) }, bodyLight, null);
        DrawPolygon(context, new[] { P(-0.5, 0.18), P(0.5, 0.18), P(0.36, 0.8), P(-0.36, 0.8) }, belly, null);
        DrawPolygon(context, new[] { P(0.08, 0.18), P(0.5, 0.18), P(0.36, 0.8), P(0.0, 0.68) }, bellyShade, null);
        DrawPolygon(context, new[] { P(-0.76, -0.52), P(-0.28, -0.62), P(-0.2, -0.24), P(-0.78, -0.2) }, eye, outline);
        DrawPolygon(context, new[] { P(0.28, -0.62), P(0.76, -0.52), P(0.78, -0.2), P(0.2, -0.24) }, eye, outline);
        context.DrawEllipse(pupil, null, P(-0.46, -0.42), size * 0.1, size * 0.08);
        context.DrawEllipse(pupil, null, P(0.46, -0.42), size * 0.1, size * 0.08);
        context.DrawLine(mouthPen, P(-0.76, -0.08), P(0.76, -0.08));
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 192, 245, 248)), Math.Max(0.34, size * 0.032)), P(-0.42, -0.72), P(0.2, -0.82));
        context.DrawLine(new Pen(antenna, Math.Max(0.46, size * 0.045)), P(0.0, -0.9), P(0.06, -1.28));
        DrawPolygon(context, new[] { P(0.02, -1.24), P(0.18, -1.42), P(0.16, -1.1) }, antenna, outline);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.1);
    }

    private static bool TryDrawReferenceMarkerImage(DrawingContext context, Point point, double size, Pen rim, string stamp)
    {
        Bitmap? bitmap = GetReferenceMarkerImage(stamp);
        if (bitmap == null)
            return false;

        DrawMarkerBitmap(context, point, size, rim, bitmap);
        return true;
    }

    private static bool TryDrawMobyRasterIcon(DrawingContext context, Point point, double size, Pen rim, Moby moby)
    {
        if (!MobyRasterIconCatalog.TryMatch(moby, out MobyRasterIconDefinition definition))
            return false;

        MobyRasterIconImage? icon = GetMobyRasterIcon(definition);
        if (icon == null)
            return false;

        if (icon.AtlasCellId is MobyRasterIconAtlasCellId atlasCellId)
        {
            lock (MobyRasterIconDiagnosticsGate)
            {
                MobyRasterIconAtlasDrawCounts[atlasCellId] =
                    MobyRasterIconAtlasDrawCounts.GetValueOrDefault(atlasCellId) + 1;
            }
        }

        DrawMarkerBitmap(context, point, size, rim, icon.Bitmap, icon.SourceRect);
        return true;
    }

    private static void DrawMarkerBitmap(
        DrawingContext context,
        Point point,
        double size,
        Pen rim,
        Bitmap bitmap,
        Rect? sourceRect = null)
    {
        Rect source = sourceRect ?? new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        double sourceWidth = Math.Max(1, source.Width);
        double sourceHeight = Math.Max(1, source.Height);
        double aspect = sourceWidth / sourceHeight;
        double maxWidth = size * 2.7;
        double maxHeight = size * 2.65;
        double width = maxWidth;
        double height = width / aspect;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * aspect;
        }

        IBrush shadow = new SolidColorBrush(Color.FromArgb(82, 0, 0, 0));
        context.DrawEllipse(shadow, null, new Point(point.X, point.Y + (size * 0.96)), width * 0.38, size * 0.2);

        Rect destination = new(point.X - (width / 2), point.Y - (height * 0.56), width, height);
        context.DrawImage(bitmap, source, destination);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, Math.Max(size * 1.05, width * 0.42), Math.Max(size * 1.05, height * 0.42));
    }

    private static MobyRasterIconImage? GetMobyRasterIcon(MobyRasterIconDefinition definition)
    {
        MobyRasterIconCacheKey cacheKey = new(definition.FileName, definition.AtlasCellId);
        if (MobyRasterIconCache.TryGetValue(cacheKey, out MobyRasterIconImage? cached))
            return cached;

        string path = MobyRasterIconCatalog.GetAssetPath(AppContext.BaseDirectory, definition);
        try
        {
            if (File.Exists(path))
            {
                Bitmap bitmap = new(path);
                MobyRasterIconImage individual = new(bitmap, null, null);
                MobyRasterIconCache[cacheKey] = individual;
                return individual;
            }

            Bitmap? atlas = GetMobyRasterIconAtlas(definition.Atlas);
            if (atlas != null && definition.Atlas.ContainsUsedCell(definition.AtlasCell))
            {
                MobyRasterIconImage fromAtlas = new(
                    atlas,
                    MobyIconAtlasLoader.GetCellSourceRect(atlas.PixelSize, definition.Atlas, definition.AtlasCell),
                    definition.AtlasCellId);
                MobyRasterIconCache[cacheKey] = fromAtlas;
                return fromAtlas;
            }

            MobyRasterIconCache[cacheKey] = null;
            return null;
        }
        catch
        {
            // An optional icon can never make a level unloadable; retain the old marker.
            MobyRasterIconCache[cacheKey] = null;
            return null;
        }
    }

    private static Bitmap? GetMobyRasterIconAtlas(MobyRasterIconAtlasContract atlas)
    {
        string path = MobyRasterIconCatalog.GetAtlasAssetPath(AppContext.BaseDirectory, atlas);
        return MobyIconAtlasLoader.Load(path, atlas).Bitmap;
    }

    private static Bitmap? GetReferenceMarkerImage(string stamp)
    {
        if (MobyMarkerImageCache.TryGetValue(stamp, out Bitmap? cached))
            return cached;

        if (!MobyMarkerImageFiles.TryGetValue(stamp, out string? fileName))
        {
            MobyMarkerImageCache[stamp] = null;
            return null;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Markers", fileName);
        try
        {
            Bitmap? bitmap = File.Exists(path) ? new Bitmap(path) : null;
            MobyMarkerImageCache[stamp] = bitmap;
            return bitmap;
        }
        catch
        {
            MobyMarkerImageCache[stamp] = null;
            return null;
        }
    }

    private static void DrawBananaBoyMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(232, 54, 28, 7)), Math.Max(0.82, size * 0.082));
        Pen browPen = new(new SolidColorBrush(Color.FromRgb(20, 14, 9)), Math.Max(0.6, size * 0.056));
        Pen arrowShaft = new(new SolidColorBrush(Color.FromRgb(92, 65, 37)), Math.Max(0.52, size * 0.048));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush fur = new SolidColorBrush(Color.FromRgb(206, 112, 15));
        IBrush furLight = new SolidColorBrush(Color.FromRgb(247, 155, 28));
        IBrush furDark = new SolidColorBrush(Color.FromRgb(102, 53, 12));
        IBrush face = new SolidColorBrush(Color.FromRgb(230, 133, 18));
        IBrush hat = new SolidColorBrush(Color.FromRgb(106, 47, 116));
        IBrush arrowHead = new SolidColorBrush(Color.FromRgb(226, 217, 244));
        IBrush arrowFletch = new SolidColorBrush(Color.FromRgb(255, 197, 38));
        IBrush mouth = new SolidColorBrush(Color.FromRgb(82, 34, 84));
        IBrush teeth = new SolidColorBrush(Color.FromRgb(250, 241, 226));
        IBrush eye = new SolidColorBrush(Color.FromRgb(250, 250, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 16, 15));
        IBrush lip = new SolidColorBrush(Color.FromRgb(164, 88, 190));
        IBrush foot = new SolidColorBrush(Color.FromRgb(75, 121, 35));

        context.DrawEllipse(shadow, null, P(0.02, 1.04), size * 1.06, size * 0.22);
        context.DrawLine(arrowShaft, P(-1.26, -1.2), P(1.32, -1.28));
        DrawPolygon(context, new[] { P(-1.48, -1.18), P(-1.08, -1.42), P(-1.08, -0.98) }, arrowHead, outline);
        DrawPolygon(context, new[] { P(0.86, -1.4), P(1.44, -1.42), P(1.16, -1.08), P(0.86, -1.1) }, arrowFletch, outline);
        DrawPolygon(context, new[] { P(-0.28, -1.08), P(0.12, -1.52), P(0.48, -1.1), P(0.25, -0.76), P(-0.2, -0.78) }, hat, outline);
        DrawPolygon(context, new[] { P(0.06, -1.48), P(0.34, -1.88), P(0.5, -1.18) }, arrowFletch, outline);

        DrawPolygon(context, new[] { P(-0.72, -0.08), P(-1.34, 0.18), P(-1.18, 0.96), P(-0.56, 0.8), P(-0.2, 0.16) }, furLight, outline);
        DrawPolygon(context, new[] { P(0.72, -0.08), P(1.34, 0.18), P(1.14, 0.96), P(0.54, 0.8), P(0.2, 0.16) }, fur, outline);
        DrawPolygon(context, new[] { P(-0.8, -0.48), P(-0.36, -0.92), P(0.34, -0.92), P(0.8, -0.48), P(0.7, 0.22), P(0.34, 0.76), P(-0.28, 0.86), P(-0.68, 0.28) }, fur, outline);
        DrawPolygon(context, new[] { P(-0.58, -0.48), P(-0.16, -0.8), P(0.42, -0.74), P(0.62, -0.28), P(0.32, 0.04), P(-0.38, 0.0) }, face, outline);
        DrawPolygon(context, new[] { P(0.08, -0.72), P(0.62, -0.28), P(0.32, 0.04), P(0.02, -0.14) }, furDark, null);
        DrawPolygon(context, new[] { P(-0.6, -0.28), P(-0.16, -0.5), P(0.04, -0.1), P(-0.5, -0.02) }, eye, outline);
        DrawPolygon(context, new[] { P(0.08, -0.5), P(0.58, -0.28), P(0.48, -0.02), P(0.02, -0.1) }, eye, outline);
        context.DrawEllipse(pupil, null, P(-0.2, -0.18), size * 0.1, size * 0.1);
        context.DrawEllipse(pupil, null, P(0.24, -0.18), size * 0.1, size * 0.1);
        context.DrawLine(browPen, P(-0.62, -0.5), P(-0.08, -0.6));
        context.DrawLine(browPen, P(0.08, -0.6), P(0.64, -0.45));
        DrawPolygon(context, new[] { P(-0.48, 0.12), P(0.48, 0.08), P(0.36, 0.54), P(-0.24, 0.62) }, mouth, outline);
        DrawPolygon(context, new[] { P(-0.32, 0.4), P(0.3, 0.38), P(0.22, 0.6), P(-0.16, 0.62) }, lip, null);
        context.DrawRectangle(teeth, null, new Rect(P(-0.25, 0.15), new Size(size * 0.52, size * 0.16)));
        DrawPolygon(context, new[] { P(-0.32, 0.78), P(0.18, 0.78), P(0.0, 1.08) }, foot, outline);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.14);
    }

    private static void DrawStrongarmMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(230, 54, 31, 9)), Math.Max(0.84, size * 0.082));
        Pen grinPen = new(new SolidColorBrush(Color.FromRgb(83, 30, 62)), Math.Max(0.66, size * 0.06));
        Pen arrowShaft = new(new SolidColorBrush(Color.FromRgb(103, 76, 40)), Math.Max(0.5, size * 0.045));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(190, 104, 12));
        IBrush bodyLight = new SolidColorBrush(Color.FromRgb(231, 146, 23));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(95, 51, 10));
        IBrush face = new SolidColorBrush(Color.FromRgb(218, 124, 17));
        IBrush hat = new SolidColorBrush(Color.FromRgb(105, 49, 106));
        IBrush arrowHead = new SolidColorBrush(Color.FromRgb(224, 214, 240));
        IBrush arrowFletch = new SolidColorBrush(Color.FromRgb(255, 203, 39));
        IBrush mouth = new SolidColorBrush(Color.FromRgb(88, 28, 54));
        IBrush teeth = new SolidColorBrush(Color.FromRgb(250, 241, 226));
        IBrush eye = new SolidColorBrush(Color.FromRgb(250, 250, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 16, 15));
        IBrush foot = new SolidColorBrush(Color.FromRgb(78, 123, 34));

        context.DrawEllipse(shadow, null, P(0.02, 1.04), size * 1.22, size * 0.25);
        context.DrawLine(arrowShaft, P(-1.38, -1.2), P(1.38, -1.28));
        DrawPolygon(context, new[] { P(-1.5, -1.17), P(-1.1, -1.38), P(-1.1, -0.98) }, arrowHead, outline);
        DrawPolygon(context, new[] { P(0.82, -1.34), P(1.4, -1.36), P(1.16, -1.06), P(0.84, -1.08) }, arrowFletch, outline);
        DrawPolygon(context, new[] { P(-0.22, -1.1), P(0.08, -1.38), P(0.44, -1.1), P(0.28, -0.82), P(-0.12, -0.82) }, hat, outline);
        DrawPolygon(context, new[] { P(-0.38, -0.08), P(-1.58, 0.0), P(-1.52, 0.86), P(-0.6, 0.78), P(-0.2, 0.14) }, bodyLight, outline);
        DrawPolygon(context, new[] { P(0.38, -0.08), P(1.58, 0.0), P(1.52, 0.86), P(0.6, 0.78), P(0.2, 0.14) }, body, outline);
        DrawPolygon(context, new[] { P(-0.76, -0.5), P(-0.24, -0.92), P(0.42, -0.9), P(0.84, -0.46), P(0.72, 0.66), P(0.06, 1.08), P(-0.62, 0.76) }, body, outline);
        DrawPolygon(context, new[] { P(-0.22, -0.84), P(0.2, -0.86), P(0.28, 0.58), P(-0.18, 0.66) }, bodyLight, null);
        DrawPolygon(context, new[] { P(0.22, -0.84), P(0.84, -0.46), P(0.58, 0.68), P(0.16, 0.52) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.54, -0.72), P(-0.12, -0.98), P(0.36, -0.96), P(0.58, -0.7), P(0.3, -0.48), P(-0.34, -0.5) }, face, outline);
        context.DrawEllipse(eye, null, P(-0.18, -0.48), size * 0.15, size * 0.085);
        context.DrawEllipse(eye, null, P(0.28, -0.5), size * 0.15, size * 0.085);
        context.DrawEllipse(pupil, null, P(-0.14, -0.52), size * 0.048, size * 0.04);
        context.DrawEllipse(pupil, null, P(0.24, -0.54), size * 0.048, size * 0.04);
        context.DrawLine(grinPen, P(-0.32, -0.1), P(0.4, -0.08));
        DrawPolygon(context, new[] { P(-0.14, -0.02), P(0.3, -0.02), P(0.16, 0.2), P(-0.02, 0.2) }, mouth, null);
        context.DrawRectangle(teeth, null, new Rect(P(-0.2, -0.03), new Size(size * 0.4, size * 0.09)));
        DrawPolygon(context, new[] { P(-0.28, 0.72), P(-0.02, 1.08), P(0.17, 0.74) }, foot, outline);
        DrawPolygon(context, new[] { P(0.1, 0.72), P(0.4, 1.02), P(0.5, 0.7) }, new SolidColorBrush(Color.FromRgb(70, 111, 28)), outline);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.16, size * 1.1);
    }

    private static void DrawWingedFoolMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 65, 32, 25)), Math.Max(0.78, size * 0.08));
        Pen armPen = new(new SolidColorBrush(Color.FromRgb(235, 137, 119)), Math.Max(0.72, size * 0.072));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        IBrush hat = new SolidColorBrush(Color.FromRgb(254, 52, 10));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(180, 24, 8));
        IBrush hatSpot = new SolidColorBrush(Color.FromArgb(165, 112, 19, 17));
        IBrush skin = new SolidColorBrush(Color.FromRgb(239, 146, 126));
        IBrush shirt = new SolidColorBrush(Color.FromRgb(44, 149, 61));
        IBrush shirtDark = new SolidColorBrush(Color.FromRgb(20, 91, 43));
        IBrush sash = new SolidColorBrush(Color.FromRgb(232, 35, 20));
        IBrush sashGold = new SolidColorBrush(Color.FromRgb(255, 188, 28));
        IBrush wingGold = new SolidColorBrush(Color.FromRgb(252, 239, 89));
        IBrush wingBlue = new SolidColorBrush(Color.FromRgb(73, 214, 221));
        IBrush wingPurple = new SolidColorBrush(Color.FromRgb(145, 72, 190));
        IBrush eye = new SolidColorBrush(Color.FromRgb(246, 246, 236));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 17, 18));

        context.DrawEllipse(shadow, null, P(0.08, 1.04), size * 1.08, size * 0.22);
        context.DrawLine(armPen, P(-0.34, -0.14), P(-1.48, 0.02));
        context.DrawLine(armPen, P(0.38, -0.14), P(1.48, 0.0));
        DrawPolygon(context, new[] { P(0.12, -0.48), P(1.18, -0.74), P(1.02, -0.08), P(0.28, 0.16) }, wingGold, outline);
        DrawPolygon(context, new[] { P(0.42, -0.38), P(1.18, -0.74), P(0.86, -0.08) }, wingBlue, null);
        DrawPolygon(context, new[] { P(0.34, -0.08), P(0.82, -0.38), P(0.7, 0.18) }, wingPurple, null);
        DrawPolygon(context, new[] { P(-0.58, -0.02), P(0.52, -0.04), P(0.62, 0.7), P(0.0, 1.04), P(-0.58, 0.66) }, shirt, outline);
        DrawPolygon(context, new[] { P(0.04, -0.02), P(0.52, -0.04), P(0.62, 0.7), P(0.04, 0.72) }, shirtDark, null);
        DrawPolygon(context, new[] { P(-0.46, 0.38), P(0.56, 0.36), P(0.38, 0.72), P(-0.22, 0.72) }, sash, null);
        DrawPolygon(context, new[] { P(0.2, 0.82), P(0.52, 1.0), P(0.42, 0.66) }, sashGold, outline);
        DrawPolygon(context, new[] { P(-0.38, -0.12), P(-0.18, -0.62), P(0.28, -0.6), P(0.44, -0.08), P(0.16, 0.16), P(-0.2, 0.1) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.9, -0.78), P(-0.34, -1.78), P(0.0, -0.78), P(0.3, -1.58), P(0.9, -0.66), P(0.34, -0.42), P(-0.4, -0.46) }, hat, outline);
        DrawPolygon(context, new[] { P(-0.34, -1.78), P(0.0, -0.78), P(-0.12, -0.58) }, hatDark, null);
        DrawPolygon(context, new[] { P(0.3, -1.58), P(0.9, -0.66), P(0.34, -0.52) }, hatDark, null);
        context.DrawEllipse(hatSpot, null, P(-0.2, -1.22), size * 0.04, size * 0.035);
        context.DrawEllipse(hatSpot, null, P(0.14, -1.04), size * 0.04, size * 0.035);
        context.DrawEllipse(hatSpot, null, P(0.44, -0.82), size * 0.035, size * 0.03);
        context.DrawEllipse(eye, null, P(-0.02, -0.32), size * 0.14, size * 0.058);
        context.DrawEllipse(pupil, null, P(0.02, -0.32), size * 0.043, size * 0.028);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(171, 27, 28)), Math.Max(0.42, size * 0.04)), P(-0.14, -0.02), P(0.26, -0.03));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.16);
    }

    private static void DrawArmoredFoolMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 38, 42, 33)), Math.Max(0.82, size * 0.082));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush armor = new SolidColorBrush(Color.FromRgb(218, 239, 235));
        IBrush armorGlow = new SolidColorBrush(Color.FromArgb(180, 248, 255, 255));
        IBrush armorShade = new SolidColorBrush(Color.FromRgb(106, 137, 132));
        IBrush armorDark = new SolidColorBrush(Color.FromRgb(59, 69, 62));
        IBrush collar = new SolidColorBrush(Color.FromRgb(40, 43, 35));
        IBrush sleeve = new SolidColorBrush(Color.FromRgb(70, 74, 58));
        IBrush skin = new SolidColorBrush(Color.FromRgb(232, 126, 112));
        IBrush hat = new SolidColorBrush(Color.FromRgb(248, 54, 13));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(179, 27, 9));
        IBrush hatSpot = new SolidColorBrush(Color.FromArgb(150, 108, 18, 16));
        IBrush foot = new SolidColorBrush(Color.FromRgb(250, 190, 25));
        IBrush wingGold = new SolidColorBrush(Color.FromRgb(255, 237, 78));
        IBrush wingBlue = new SolidColorBrush(Color.FromRgb(94, 216, 216));
        IBrush eye = new SolidColorBrush(Color.FromRgb(246, 246, 236));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 17, 18));

        context.DrawEllipse(shadow, null, P(0.0, 1.04), size * 1.16, size * 0.22);
        DrawPolygon(context, new[] { P(-0.64, -0.14), P(-1.52, -0.02), P(-1.54, 0.54), P(-0.72, 0.5) }, sleeve, outline);
        DrawPolygon(context, new[] { P(0.64, -0.14), P(1.52, -0.02), P(1.54, 0.54), P(0.72, 0.5) }, sleeve, outline);
        DrawPolygon(context, new[] { P(-1.08, -0.2), P(-0.58, -0.7), P(0.58, -0.7), P(1.08, -0.2), P(0.76, 0.7), P(0.24, 1.08), P(-0.28, 1.06), P(-0.78, 0.7) }, armor, outline);
        DrawPolygon(context, new[] { P(0.0, -0.68), P(1.08, -0.2), P(0.76, 0.7), P(0.08, 0.76) }, armorShade, null);
        DrawPolygon(context, new[] { P(-0.56, 0.0), P(0.5, 0.0), P(0.36, 0.78), P(-0.36, 0.8) }, armorGlow, null);
        DrawPolygon(context, new[] { P(0.42, 0.0), P(0.72, 0.66), P(0.28, 0.92), P(0.18, 0.12) }, armorDark, null);
        DrawPolygon(context, new[] { P(-0.92, -0.44), P(0.92, -0.44), P(0.58, 0.02), P(-0.58, 0.02) }, collar, outline);
        DrawPolygon(context, new[] { P(-0.22, -1.12), P(0.24, -1.12), P(0.32, -0.34), P(-0.14, -0.34) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.42, -1.46), P(-0.04, -1.92), P(0.18, -1.36), P(0.48, -1.12), P(0.18, -0.96), P(-0.2, -1.02) }, hat, outline);
        DrawPolygon(context, new[] { P(-0.08, -1.78), P(0.18, -1.34), P(0.16, -1.02) }, hatDark, null);
        DrawPolygon(context, new[] { P(0.18, -1.34), P(0.48, -1.12), P(0.22, -1.0) }, hatDark, null);
        context.DrawEllipse(hatSpot, null, P(-0.04, -1.44), size * 0.035, size * 0.03);
        context.DrawEllipse(hatSpot, null, P(0.18, -1.22), size * 0.034, size * 0.028);
        DrawPolygon(context, new[] { P(0.28, -0.78), P(1.08, -1.0), P(0.7, -0.22), P(0.26, 0.02) }, wingGold, outline);
        DrawPolygon(context, new[] { P(0.56, -0.56), P(1.08, -1.0), P(0.7, -0.22) }, wingBlue, null);
        context.DrawEllipse(eye, null, P(0.02, -1.08), size * 0.1, size * 0.06);
        context.DrawEllipse(pupil, null, P(0.04, -1.08), size * 0.032, size * 0.022);
        DrawPolygon(context, new[] { P(-0.24, 0.92), P(-0.02, 1.14), P(0.16, 0.92) }, foot, outline);
        DrawPolygon(context, new[] { P(0.24, 0.88), P(0.48, 1.08), P(0.6, 0.82) }, foot, outline);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.14);
    }

    private static void DrawMushroomMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(224, 72, 49, 31)), Math.Max(0.78, size * 0.078));
        Pen speckPen = new(new SolidColorBrush(Color.FromArgb(132, 83, 68, 35)), Math.Max(0.3, size * 0.028));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        IBrush stem = new SolidColorBrush(Color.FromRgb(151, 130, 118));
        IBrush stemLight = new SolidColorBrush(Color.FromRgb(188, 166, 148));
        IBrush capRed = new SolidColorBrush(Color.FromRgb(174, 43, 31));
        IBrush capGold = new SolidColorBrush(Color.FromRgb(196, 159, 59));
        IBrush capOlive = new SolidColorBrush(Color.FromRgb(120, 113, 47));
        IBrush capDark = new SolidColorBrush(Color.FromRgb(94, 40, 29));
        IBrush spot = new SolidColorBrush(Color.FromRgb(70, 55, 34));
        IBrush eye = new SolidColorBrush(Color.FromRgb(245, 246, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(16, 16, 16));

        context.DrawEllipse(shadow, null, P(0.0, 0.98), size * 1.04, size * 0.22);
        DrawPolygon(context, new[] { P(-0.48, -0.02), P(0.5, -0.02), P(0.56, 0.58), P(0.18, 0.98), P(-0.38, 0.9), P(-0.58, 0.42) }, stem, outline);
        DrawPolygon(context, new[] { P(-0.4, 0.0), P(0.04, -0.08), P(0.08, 0.82), P(-0.28, 0.76) }, stemLight, null);
        DrawPolygon(context, new[] { P(-1.16, -0.02), P(-0.86, -0.62), P(-0.08, -1.0), P(0.78, -0.82), P(1.16, -0.04), P(0.9, 0.18), P(-0.9, 0.18) }, capRed, outline);
        DrawPolygon(context, new[] { P(-0.86, -0.62), P(-0.08, -1.0), P(0.78, -0.82), P(0.44, -0.42), P(-0.56, -0.32) }, capGold, null);
        DrawPolygon(context, new[] { P(-0.74, -0.56), P(-0.08, -0.94), P(0.42, -0.76), P(0.18, -0.52), P(-0.42, -0.42) }, capOlive, null);
        DrawPolygon(context, new[] { P(0.28, -0.46), P(1.16, -0.04), P(0.9, 0.18), P(0.12, 0.02) }, capDark, null);
        context.DrawLine(speckPen, P(-0.7, -0.5), P(-0.54, -0.42));
        context.DrawLine(speckPen, P(-0.28, -0.72), P(-0.14, -0.64));
        context.DrawLine(speckPen, P(0.2, -0.6), P(0.34, -0.52));
        context.DrawLine(speckPen, P(0.58, -0.3), P(0.72, -0.24));
        context.DrawEllipse(spot, null, P(-0.52, -0.52), size * 0.09, size * 0.065);
        context.DrawEllipse(spot, null, P(-0.1, -0.74), size * 0.08, size * 0.06);
        context.DrawEllipse(spot, null, P(0.32, -0.58), size * 0.095, size * 0.065);
        context.DrawEllipse(spot, null, P(0.66, -0.24), size * 0.07, size * 0.045);
        context.DrawEllipse(eye, null, P(0.0, 0.1), size * 0.085, size * 0.145);
        context.DrawEllipse(eye, null, P(0.3, 0.1), size * 0.075, size * 0.125);
        context.DrawEllipse(pupil, null, P(0.04, 0.1), size * 0.032, size * 0.058);
        context.DrawEllipse(pupil, null, P(0.31, 0.1), size * 0.028, size * 0.052);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.05, size * 1.05);
    }

    private static void DrawPuppyDevilDogMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 82, 52, 7)), Math.Max(0.84, size * 0.084));
        Pen cord = new(new SolidColorBrush(Color.FromRgb(156, 86, 16)), Math.Max(0.45, size * 0.043));
        Pen wrinkle = new(new SolidColorBrush(Color.FromArgb(150, 114, 77, 17)), Math.Max(0.34, size * 0.028));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush fur = new SolidColorBrush(Color.FromRgb(178, 119, 15));
        IBrush furLight = new SolidColorBrush(Color.FromRgb(221, 174, 36));
        IBrush furDark = new SolidColorBrush(Color.FromRgb(104, 66, 12));
        IBrush face = new SolidColorBrush(Color.FromRgb(255, 228, 48));
        IBrush faceShade = new SolidColorBrush(Color.FromRgb(221, 174, 28));
        IBrush mouth = new SolidColorBrush(Color.FromRgb(114, 17, 23));
        IBrush eye = new SolidColorBrush(Color.FromRgb(28, 24, 21));

        context.DrawEllipse(shadow, null, P(0.02, 1.02), size * 1.08, size * 0.22);
        context.DrawLine(cord, P(-0.06, -1.0), P(-0.06, -1.82));
        DrawPolygon(context, new[] { P(-0.74, -0.68), P(-1.2, -0.42), P(-0.78, -0.04) }, fur, outline);
        DrawPolygon(context, new[] { P(0.66, -0.74), P(1.12, -1.28), P(0.9, -0.04) }, fur, outline);
        DrawPolygon(context, new[] { P(-0.68, -0.58), P(-1.04, -0.42), P(-0.76, -0.18) }, furLight, null);
        DrawPolygon(context, new[] { P(0.78, -0.72), P(1.06, -1.12), P(0.86, -0.28) }, furLight, null);
        DrawPolygon(context, new[] { P(-0.78, 0.2), P(-1.02, 0.82), P(-0.54, 1.02), P(-0.34, 0.44) }, furLight, outline);
        DrawPolygon(context, new[] { P(0.78, 0.2), P(1.02, 0.82), P(0.54, 1.02), P(0.34, 0.44) }, fur, outline);
        DrawPolygon(context, new[] { P(-0.82, -0.38), P(-0.36, -0.92), P(0.46, -0.92), P(0.84, -0.38), P(0.64, 0.42), P(0.0, 0.92), P(-0.64, 0.42) }, fur, outline);
        DrawPolygon(context, new[] { P(-0.58, -0.7), P(0.58, -0.7), P(0.58, -0.06), P(0.2, 0.16), P(-0.2, 0.16), P(-0.58, -0.06) }, face, outline);
        DrawPolygon(context, new[] { P(0.16, -0.66), P(0.58, -0.7), P(0.58, -0.06), P(0.14, -0.08) }, faceShade, null);
        context.DrawLine(new Pen(eye, Math.Max(0.42, size * 0.043)), P(-0.2, -0.5), P(-0.02, -0.32));
        context.DrawLine(new Pen(eye, Math.Max(0.42, size * 0.043)), P(-0.02, -0.5), P(-0.2, -0.32));
        context.DrawLine(new Pen(eye, Math.Max(0.42, size * 0.043)), P(0.06, -0.5), P(0.26, -0.32));
        context.DrawLine(new Pen(eye, Math.Max(0.42, size * 0.043)), P(0.26, -0.5), P(0.06, -0.32));
        DrawPolygon(context, new[] { P(-0.68, -0.02), P(0.68, -0.02), P(0.48, 0.42), P(-0.44, 0.42) }, furDark, outline);
        DrawPolygon(context, new[] { P(-0.58, 0.18), P(0.58, 0.18), P(0.4, 0.66), P(-0.36, 0.66) }, mouth, outline);
        context.DrawLine(wrinkle, P(-0.5, 0.02), P(0.48, 0.0));
        context.DrawLine(wrinkle, P(-0.34, 0.46), P(0.34, 0.46));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.1, size * 1.14);
    }

    private static void DrawDevilCupidMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 103, 16, 12)), Math.Max(0.84, size * 0.084));
        Pen bowPen = new(new SolidColorBrush(Color.FromRgb(118, 116, 36)), Math.Max(0.58, size * 0.056));
        Pen stringPen = new(new SolidColorBrush(Color.FromArgb(205, 232, 228, 192)), Math.Max(0.32, size * 0.028));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(68, 0, 0, 0));
        IBrush skin = new SolidColorBrush(Color.FromRgb(232, 29, 19));
        IBrush skinLight = new SolidColorBrush(Color.FromRgb(255, 68, 35));
        IBrush skinDark = new SolidColorBrush(Color.FromRgb(137, 18, 21));
        IBrush horn = new SolidColorBrush(Color.FromRgb(178, 211, 219));
        IBrush hair = new SolidColorBrush(Color.FromRgb(255, 214, 36));
        IBrush eye = new SolidColorBrush(Color.FromRgb(248, 248, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 17, 18));
        IBrush teeth = new SolidColorBrush(Color.FromRgb(250, 246, 232));

        context.DrawEllipse(shadow, null, P(0.0, 1.02), size * 1.0, size * 0.2);
        DrawPolygon(context, new[] { P(-0.66, -0.32), P(-1.18, -0.08), P(-0.72, 0.24), P(-0.22, -0.06) }, skin, outline);
        DrawPolygon(context, new[] { P(0.44, -0.02), P(0.9, 0.24), P(0.66, 0.76), P(0.18, 0.36) }, skinDark, outline);
        DrawPolygon(context, new[] { P(-0.62, 0.18), P(0.44, 0.08), P(0.64, 0.82), P(-0.36, 0.9) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.8, -0.64), P(-0.2, -1.12), P(0.54, -0.88), P(0.82, -0.34), P(0.46, 0.12), P(-0.4, 0.08) }, skin, outline);
        DrawPolygon(context, new[] { P(-0.5, -0.62), P(-0.08, -1.04), P(0.5, -0.88), P(0.12, -0.46) }, skinLight, null);
        DrawPolygon(context, new[] { P(-0.58, -0.9), P(-0.92, -1.25), P(-0.38, -1.08) }, horn, outline);
        DrawPolygon(context, new[] { P(0.36, -0.9), P(0.74, -1.25), P(0.64, -0.78) }, horn, outline);
        DrawPolygon(context, new[] { P(-0.04, -1.0), P(0.12, -1.34), P(0.32, -0.94) }, hair, outline);
        DrawPolygon(context, new[] { P(-0.58, -0.44), P(-0.08, -0.54), P(0.02, -0.28), P(-0.56, -0.22) }, eye, null);
        DrawPolygon(context, new[] { P(0.1, -0.54), P(0.58, -0.46), P(0.44, -0.2), P(0.02, -0.28) }, eye, null);
        context.DrawEllipse(pupil, null, P(-0.2, -0.36), size * 0.047, size * 0.032);
        context.DrawEllipse(pupil, null, P(0.2, -0.36), size * 0.047, size * 0.032);
        DrawPolygon(context, new[] { P(-0.52, -0.02), P(0.58, -0.04), P(0.5, 0.3), P(-0.42, 0.32) }, teeth, outline);
        for (int i = 0; i < 6; i++)
        {
            double x = -0.34 + (i * 0.16);
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(180, 90, 28, 24)), Math.Max(0.24, size * 0.02)), P(x, 0.0), P(x, 0.27));
        }
        context.DrawLine(bowPen, P(0.82, -0.28), P(1.26, -0.92));
        context.DrawLine(bowPen, P(0.82, -0.28), P(1.34, 0.36));
        context.DrawLine(stringPen, P(1.26, -0.92), P(1.34, 0.36));

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.12, size * 1.08);
    }

    private static void DrawMutantTurtleMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 6, 31, 36)), Math.Max(0.84, size * 0.084));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(74, 0, 0, 0));
        IBrush body = new SolidColorBrush(Color.FromRgb(16, 74, 72));
        IBrush bodyDark = new SolidColorBrush(Color.FromRgb(5, 35, 42));
        IBrush belly = new SolidColorBrush(Color.FromRgb(218, 230, 220));
        IBrush bellyShade = new SolidColorBrush(Color.FromRgb(144, 166, 165));
        IBrush eye = new SolidColorBrush(Color.FromRgb(238, 28, 24));
        IBrush tooth = new SolidColorBrush(Color.FromRgb(240, 244, 236));

        context.DrawEllipse(shadow, null, P(0.0, 1.02), size * 1.32, size * 0.23);
        DrawPolygon(context, new[] { P(-0.76, -0.18), P(-1.5, 0.1), P(-1.16, 0.64), P(-0.46, 0.22) }, body, outline);
        DrawPolygon(context, new[] { P(0.76, -0.18), P(1.5, 0.1), P(1.16, 0.64), P(0.46, 0.22) }, body, outline);
        DrawPolygon(context, new[] { P(-1.1, -0.6), P(-0.46, -1.0), P(0.46, -1.0), P(1.1, -0.6), P(0.94, 0.42), P(0.38, 0.94), P(-0.38, 0.94), P(-0.94, 0.42) }, body, outline);
        DrawPolygon(context, new[] { P(0.18, -0.84), P(1.1, -0.6), P(0.94, 0.42), P(0.22, 0.16) }, bodyDark, null);
        DrawPolygon(context, new[] { P(-0.6, -0.22), P(0.6, -0.22), P(0.5, 0.74), P(0.0, 1.02), P(-0.5, 0.74) }, belly, outline);
        DrawPolygon(context, new[] { P(0.08, -0.2), P(0.6, -0.22), P(0.5, 0.74), P(0.0, 0.64) }, bellyShade, null);
        DrawPolygon(context, new[] { P(-0.6, -0.7), P(-0.18, -0.8), P(-0.2, -0.54), P(-0.6, -0.5) }, eye, outline);
        DrawPolygon(context, new[] { P(0.18, -0.8), P(0.6, -0.7), P(0.6, -0.5), P(0.2, -0.54) }, eye, outline);
        DrawPolygon(context, new[] { P(-0.7, -0.4), P(-0.5, -0.4), P(-0.58, -0.1) }, tooth, null);
        DrawPolygon(context, new[] { P(-0.3, -0.4), P(-0.1, -0.4), P(-0.2, -0.1) }, tooth, null);
        DrawPolygon(context, new[] { P(0.1, -0.4), P(0.3, -0.4), P(0.18, -0.1) }, tooth, null);
        DrawPolygon(context, new[] { P(0.5, -0.4), P(0.7, -0.4), P(0.58, -0.1) }, tooth, null);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.14, size * 1.1);
    }

    private static void DrawLampFoolMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(226, 84, 39, 10)), Math.Max(0.8, size * 0.08));
        IBrush shadow = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        IBrush glow = new SolidColorBrush(Color.FromArgb(188, 255, 248, 90));
        IBrush glowCore = new SolidColorBrush(Color.FromArgb(228, 255, 255, 178));
        IBrush hat = new SolidColorBrush(Color.FromRgb(248, 53, 13));
        IBrush hatDark = new SolidColorBrush(Color.FromRgb(188, 28, 8));
        IBrush skin = new SolidColorBrush(Color.FromRgb(239, 129, 110));
        IBrush lamp = new SolidColorBrush(Color.FromRgb(250, 236, 73));
        IBrush lampDark = new SolidColorBrush(Color.FromRgb(164, 112, 28));
        IBrush eye = new SolidColorBrush(Color.FromRgb(248, 248, 238));
        IBrush pupil = new SolidColorBrush(Color.FromRgb(18, 17, 18));

        context.DrawEllipse(shadow, null, P(0.0, 1.04), size * 1.08, size * 0.22);
        context.DrawEllipse(glow, null, P(0.04, 0.18), size * 1.0, size * 0.9);
        DrawPolygon(context, new[] { P(-0.82, -0.84), P(-0.26, -1.72), P(0.0, -0.78), P(0.3, -1.56), P(0.86, -0.72), P(0.3, -0.5), P(-0.36, -0.52) }, hat, outline);
        DrawPolygon(context, new[] { P(-0.24, -1.64), P(0.02, -0.78), P(-0.1, -0.62) }, hatDark, null);
        DrawPolygon(context, new[] { P(0.28, -1.48), P(0.86, -0.72), P(0.34, -0.62) }, hatDark, null);
        DrawPolygon(context, new[] { P(-0.24, -0.56), P(0.28, -0.54), P(0.24, -0.12), P(-0.2, -0.12) }, skin, outline);
        context.DrawEllipse(eye, null, P(0.0, -0.32), size * 0.092, size * 0.054);
        context.DrawEllipse(pupil, null, P(0.02, -0.32), size * 0.032, size * 0.022);
        DrawPolygon(context, new[] { P(-0.72, -0.08), P(0.72, -0.08), P(0.76, 0.84), P(-0.74, 0.84) }, lamp, outline);
        DrawPolygon(context, new[] { P(0.14, -0.04), P(0.72, -0.08), P(0.76, 0.84), P(0.08, 0.64) }, lampDark, null);
        context.DrawRectangle(glowCore, null, new Rect(P(-0.48, 0.08), new Size(size * 0.96, size * 0.56)), 1, 1);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)), null, new Rect(P(-0.32, 0.18), new Size(size * 0.66, size * 0.26)), 1, 1);
        DrawPolygon(context, new[] { P(-0.28, 0.78), P(-0.02, 1.06), P(0.22, 0.78) }, new SolidColorBrush(Color.FromRgb(224, 34, 18)), outline);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.08, size * 1.12);
    }

    private static void DrawActorMarker(DrawingContext context, Point point, double size, IBrush fill, Pen rim, string stamp, string markerText = "")
    {
        string markerTextLower = markerText.ToLowerInvariant();
        if (stamp == "FLC")
        {
            DrawFatLadyCauldronMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Bst")
        {
            DrawBalloonistMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Cn")
        {
            DrawCannonMarker(context, point, size, rim);
            return;
        }

        if (stamp == "ADr")
        {
            DrawArmoredDruidMarker(context, point, size, rim);
            return;
        }

        if (stamp == "GDr")
        {
            DrawGreenDruidMarker(context, point, size, rim);
            return;
        }

        if (stamp == "GWz")
        {
            DrawGreenWizardMarker(context, point, size, rim);
            return;
        }

        if (stamp == "EWz")
        {
            DrawElderWizardMarker(context, point, size, rim);
            return;
        }

        if (stamp == "TWz")
        {
            DrawTornadoWizardMarker(context, point, size, rim);
            return;
        }

        if (stamp == "MSp")
        {
            DrawMetalbackSpiderMarker(context, point, size, rim);
            return;
        }

        if (stamp == "SCk")
        {
            DrawSpottedChickenMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Bor")
        {
            DrawBoarMarker(context, point, size, rim);
            return;
        }

        if (stamp == "DEP")
        {
            DrawDragonEatingPlantMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Frg")
        {
            DrawAttackFrogMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Ban")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawBananaBoyMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Str")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawStrongarmMarker(context, point, size, rim);
            return;
        }

        if (stamp == "WFo")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawWingedFoolMarker(context, point, size, rim);
            return;
        }

        if (stamp == "AFo")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawArmoredFoolMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Msh")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawMushroomMarker(context, point, size, rim);
            return;
        }

        if (stamp == "DDg")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawPuppyDevilDogMarker(context, point, size, rim);
            return;
        }

        if (stamp == "DCp")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawDevilCupidMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Tur")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawMutantTurtleMarker(context, point, size, rim);
            return;
        }

        if (stamp == "LFo")
        {
            if (TryDrawReferenceMarkerImage(context, point, size, rim, stamp))
                return;
            DrawLampFoolMarker(context, point, size, rim);
            return;
        }

        if (ShouldDrawEnemyMarker(stamp, markerTextLower))
        {
            DrawGnorcEnemyMarker(context, point, size, rim, string.IsNullOrWhiteSpace(stamp) || stamp == "A" ? "Gn" : stamp);
            return;
        }

        if (stamp == "Dg")
        {
            DrawDogEnemyMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Bd")
        {
            DrawBirdEnemyMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Ck")
        {
            context.DrawEllipse(fill, rim, new Point(point.X, point.Y + (size * 0.05)), size * 0.72, size * 0.66);
            context.DrawEllipse(fill, rim, new Point(point.X + (size * 0.55), point.Y - (size * 0.45)), size * 0.36, size * 0.36);
            DrawPolygon(context, new[]
            {
                new Point(point.X + (size * 0.86), point.Y - (size * 0.44)),
                new Point(point.X + (size * 1.18), point.Y - (size * 0.32)),
                new Point(point.X + (size * 0.86), point.Y - (size * 0.2))
            }, new SolidColorBrush(Color.FromRgb(244, 208, 63)), rim);
            context.DrawLine(rim, new Point(point.X - (size * 0.18), point.Y + (size * 0.68)), new Point(point.X - (size * 0.3), point.Y + (size * 1.05)));
            context.DrawLine(rim, new Point(point.X + (size * 0.22), point.Y + (size * 0.68)), new Point(point.X + (size * 0.34), point.Y + (size * 1.05)));
            DrawMobyStamp(context, point + new Vector(0, size * 0.18), stamp, size);
            return;
        }

        if (stamp == "Egg")
        {
            DrawEggThiefMarker(context, point, size, rim);
            return;
        }

        if (stamp == "Air")
        {
            context.DrawLine(rim, new Point(point.X - (size * 1.05), point.Y), new Point(point.X + (size * 1.05), point.Y));
            DrawPolygon(context, new[]
            {
                new Point(point.X - (size * 0.28), point.Y - (size * 0.2)),
                new Point(point.X + (size * 1.1), point.Y),
                new Point(point.X - (size * 0.28), point.Y + (size * 0.2))
            }, fill, rim);
            context.DrawLine(rim, new Point(point.X - (size * 0.55), point.Y - (size * 0.52)), new Point(point.X + (size * 0.1), point.Y));
            context.DrawLine(rim, new Point(point.X - (size * 0.55), point.Y + (size * 0.52)), new Point(point.X + (size * 0.1), point.Y));
            DrawMobyStamp(context, point + new Vector(0, size * 0.3), stamp, size);
            return;
        }

        if (stamp == "Cp")
        {
            context.DrawEllipse(fill, rim, new Point(point.X, point.Y + (size * 0.12)), size * 0.74, size * 0.58);
            context.DrawLine(rim, new Point(point.X - size, point.Y - (size * 0.72)), new Point(point.X + size, point.Y - (size * 0.72)));
            context.DrawLine(rim, new Point(point.X, point.Y - (size * 1.08)), new Point(point.X, point.Y - (size * 0.34)));
            DrawMobyStamp(context, point + new Vector(0, size * 0.16), stamp, size);
            return;
        }

        if (stamp == "Fd")
        {
            DrawFodderSheepMarker(context, point, size, rim);
            return;
        }

        DrawTriangle(context, point, size, fill, rim);
        DrawMobyStamp(context, point + new Vector(0, size * 0.18), stamp, size);
    }

    private static bool ShouldDrawEnemyMarker(string stamp, string markerTextLower)
    {
        if (stamp is "Gn" or "Sh" or "Bo" or "Rm" or "Bl")
            return true;

        if (stamp is "Egg" or "Fd" or "Dg" or "Ck" or "SCk" or "Bor" or "DEP" or "Frg" or "Ban" or "Str" or "WFo" or "AFo" or "Msh" or "DDg" or "DCp" or "Tur" or "LFo" or "Air" or "Cp" or "Bd" or "Th" or "ADr" or "GDr" or "GWz" or "EWz" or "TWz" or "MSp")
            return false;

        if (!markerTextLower.Contains("enemy"))
            return false;

        return !markerTextLower.Contains("fodder")
            && !markerTextLower.Contains("sheep")
            && !markerTextLower.Contains("chicken")
            && !markerTextLower.Contains("dog")
            && !markerTextLower.Contains("egg thief")
            && !markerTextLower.Contains("dragon");
    }

    /// <summary>
    /// Reports only the four broad renderer fallthroughs. This intentionally ignores
    /// optional raster overrides so release QA can measure both the original baseline
    /// and how much of it the raster catalog covers.
    /// </summary>
    public static MobyGenericMarkerFallbackKind ClassifyGenericMarkerFallback(Moby moby)
    {
        string markerText = GetMobyMarkerText(moby);
        string markerTextLower = markerText.ToLowerInvariant();
        string stamp = GetMobyStamp(moby);

        if (moby.VisualKind == MobyVisualKind.FlightTarget)
        {
            return markerTextLower.Contains("airplane") ||
                markerTextLower.Contains("plane") ||
                markerTextLower.Contains("copter") ||
                markerTextLower.Contains("arch") ||
                markerTextLower.Contains("ring") ||
                markerTextLower.Contains("timer") ||
                markerTextLower.Contains("boat")
                    ? MobyGenericMarkerFallbackKind.None
                    : MobyGenericMarkerFallbackKind.FlightBullseye;
        }

        if (moby.VisualKind == MobyVisualKind.Scenery)
        {
            return stamp is "Bal" or "Cn" or "Te" or "Ca" or "Tr" or "Pl" or "Fl" or "G" or "To" or "L" or "F"
                ? MobyGenericMarkerFallbackKind.None
                : MobyGenericMarkerFallbackKind.GenericTree;
        }

        if (moby.VisualKind != MobyVisualKind.Actor)
            return MobyGenericMarkerFallbackKind.None;

        if (stamp is "FLC" or "Bst" or "Cn" or "ADr" or "GDr" or "GWz" or "EWz" or "TWz" or "MSp" or
            "SCk" or "Bor" or "DEP" or "Frg" or "Ban" or "Str" or "WFo" or "AFo" or "Msh" or "DDg" or
            "DCp" or "Tur" or "LFo" or "Bo" ||
            stamp == "Gn" && !markerTextLower.Contains("armored gnorc"))
        {
            return MobyGenericMarkerFallbackKind.None;
        }

        if (ShouldDrawEnemyMarker(stamp, markerTextLower))
            return MobyGenericMarkerFallbackKind.GenericGnorc;

        return stamp is "Dg" or "Bd" or "Ck" or "Egg" or "Air" or "Cp" or "Fd"
            ? MobyGenericMarkerFallbackKind.None
            : MobyGenericMarkerFallbackKind.ActorTriangle;
    }

    private static void DrawGnorcEnemyMarker(DrawingContext context, Point point, double size, Pen rim, string stamp)
    {
        Color green = Color.FromRgb(93, 196, 45);
        Color greenLight = Color.FromRgb(155, 230, 87);
        Color greenDark = Color.FromRgb(48, 112, 27);
        Color tunic = Color.FromRgb(26, 22, 19);
        Color boot = Color.FromRgb(125, 78, 21);
        Color orange = Color.FromRgb(242, 85, 16);
        Color yellow = Color.FromRgb(255, 185, 45);
        Color purple = Color.FromRgb(170, 55, 160);
        Pen outline = new(new SolidColorBrush(Color.FromArgb(215, 17, 20, 18)), Math.Max(0.9, size * 0.11));
        Pen thin = new(new SolidColorBrush(Color.FromArgb(210, 17, 20, 18)), Math.Max(0.75, size * 0.08));

        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.68), point.Y + (size * 0.48)),
            new Point(point.X - (size * 0.38), point.Y + (size * 0.88)),
            new Point(point.X - (size * 0.02), point.Y + (size * 0.62)),
            new Point(point.X - (size * 0.26), point.Y + (size * 0.28))
        }, new SolidColorBrush(boot), outline);
        DrawPolygon(context, new[]
        {
            new Point(point.X + (size * 0.18), point.Y + (size * 0.44)),
            new Point(point.X + (size * 0.62), point.Y + (size * 0.82)),
            new Point(point.X + (size * 0.94), point.Y + (size * 0.54)),
            new Point(point.X + (size * 0.42), point.Y + (size * 0.22))
        }, new SolidColorBrush(boot), outline);

        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.78), point.Y - (size * 0.04)),
            new Point(point.X - (size * 0.5), point.Y + (size * 0.56)),
            new Point(point.X + (size * 0.42), point.Y + (size * 0.58)),
            new Point(point.X + (size * 0.78), point.Y + (size * 0.1)),
            new Point(point.X + (size * 0.45), point.Y - (size * 0.42)),
            new Point(point.X - (size * 0.42), point.Y - (size * 0.44))
        }, new SolidColorBrush(greenDark), outline);
        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.72), point.Y - (size * 0.28)),
            new Point(point.X - (size * 0.5), point.Y + (size * 0.2)),
            new Point(point.X + (size * 0.54), point.Y + (size * 0.22)),
            new Point(point.X + (size * 0.72), point.Y - (size * 0.22)),
            new Point(point.X + (size * 0.28), point.Y - (size * 0.58)),
            new Point(point.X - (size * 0.36), point.Y - (size * 0.56))
        }, new SolidColorBrush(tunic), null);

        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.54), point.Y - (size * 0.58)),
            new Point(point.X - (size * 0.2), point.Y - (size * 1.02)),
            new Point(point.X + (size * 0.42), point.Y - (size * 0.92)),
            new Point(point.X + (size * 0.64), point.Y - (size * 0.42)),
            new Point(point.X + (size * 0.08), point.Y - (size * 0.16)),
            new Point(point.X - (size * 0.66), point.Y - (size * 0.22))
        }, new SolidColorBrush(green), outline);
        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.2), point.Y - (size * 1.02)),
            new Point(point.X + (size * 0.28), point.Y - (size * 0.88)),
            new Point(point.X + (size * 0.04), point.Y - (size * 0.5)),
            new Point(point.X - (size * 0.54), point.Y - (size * 0.58))
        }, new SolidColorBrush(greenLight), null);
        DrawPolygon(context, new[]
        {
            new Point(point.X + (size * 0.28), point.Y - (size * 0.88)),
            new Point(point.X + (size * 0.64), point.Y - (size * 0.42)),
            new Point(point.X + (size * 0.08), point.Y - (size * 0.16)),
            new Point(point.X + (size * 0.04), point.Y - (size * 0.5))
        }, new SolidColorBrush(greenDark), null);
        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.62), point.Y - (size * 0.22)),
            new Point(point.X + (size * 0.76), point.Y - (size * 0.36)),
            new Point(point.X + (size * 0.16), point.Y - (size * 0.06)),
            new Point(point.X - (size * 0.46), point.Y - (size * 0.04))
        }, new SolidColorBrush(Color.FromRgb(122, 221, 70)), thin);
        context.DrawLine(new Pen(new SolidColorBrush(purple), Math.Max(0.9, size * 0.1)), new Point(point.X - (size * 0.5), point.Y - (size * 0.16)), new Point(point.X + (size * 0.54), point.Y - (size * 0.28)));

        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(232, 238, 216)), null, new Point(point.X + (size * 0.02), point.Y - (size * 0.58)), size * 0.26, size * 0.19);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(16, 18, 20)), null, new Point(point.X + (size * 0.08), point.Y - (size * 0.58)), size * 0.09, size * 0.08);

        Pen hairPen = new(new SolidColorBrush(Color.FromArgb(230, 24, 22, 18)), Math.Max(0.9, size * 0.09));
        context.DrawLine(hairPen, new Point(point.X - (size * 0.16), point.Y - (size * 1.02)), new Point(point.X - (size * 0.44), point.Y - (size * 1.2)));
        context.DrawLine(hairPen, new Point(point.X + (size * 0.34), point.Y - (size * 0.9)), new Point(point.X + (size * 0.48), point.Y - (size * 1.1)));
        DrawPolygon(context, new[]
        {
            new Point(point.X - (size * 0.2), point.Y - (size * 1.02)),
            new Point(point.X - (size * 0.08), point.Y - (size * 1.28)),
            new Point(point.X + (size * 0.04), point.Y - (size * 1.0))
        }, new SolidColorBrush(orange), null);
        DrawPolygon(context, new[]
        {
            new Point(point.X + (size * 0.0), point.Y - (size * 1.0)),
            new Point(point.X + (size * 0.22), point.Y - (size * 1.23)),
            new Point(point.X + (size * 0.18), point.Y - (size * 0.94))
        }, new SolidColorBrush(yellow), null);
        DrawPolygon(context, new[]
        {
            new Point(point.X + (size * 0.18), point.Y - (size * 0.94)),
            new Point(point.X + (size * 0.42), point.Y - (size * 1.16)),
            new Point(point.X + (size * 0.32), point.Y - (size * 0.88))
        }, new SolidColorBrush(orange), null);

        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(111, 124, 126)), thin, new Rect(point.X - (size * 0.25), point.Y + (size * 0.1), size * 0.42, size * 0.28), 1, 1);
        DrawMobyStamp(context, point + new Vector(0, size * 0.28), stamp, size * 0.72);
        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.0, size * 1.08);
    }

    private static void DrawReturnHomeMarker(DrawingContext context, Point point, double size, Pen rim)
    {
        Point P(double x, double y) => new(point.X + (size * x), point.Y + (size * y));

        Pen outline = new(new SolidColorBrush(Color.FromArgb(220, 88, 58, 12)), Math.Max(0.85, size * 0.08));
        Pen goldLine = new(new SolidColorBrush(Color.FromRgb(255, 219, 66)), Math.Max(0.75, size * 0.07));
        Pen smokePen = new(new SolidColorBrush(Color.FromArgb(210, 246, 246, 235)), Math.Max(0.75, size * 0.07));
        IBrush side = new SolidColorBrush(Color.FromRgb(151, 95, 26));
        IBrush sideDark = new SolidColorBrush(Color.FromRgb(105, 65, 22));
        IBrush top = new SolidColorBrush(Color.FromRgb(225, 171, 37));
        IBrush topLight = new SolidColorBrush(Color.FromRgb(255, 218, 68));
        IBrush center = new SolidColorBrush(Color.FromRgb(178, 126, 31));
        IBrush glow = new SolidColorBrush(Color.FromArgb(96, 255, 230, 90));

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(72, 0, 0, 0)), null, P(0.02, 0.62), size * 1.05, size * 0.22);
        DrawPolygon(context, new[] { P(-1.0, -0.04), P(-0.64, 0.48), P(0.0, 0.66), P(0.72, 0.46), P(1.0, -0.04), P(0.72, 0.18), P(0.0, 0.32), P(-0.72, 0.18) }, side, outline);
        DrawPolygon(context, new[] { P(0.72, 0.18), P(1.0, -0.04), P(0.72, 0.46), P(0.0, 0.66), P(0.0, 0.32) }, sideDark, null);
        DrawPolygon(context, new[] { P(-1.0, -0.2), P(-0.64, -0.54), P(0.0, -0.66), P(0.72, -0.52), P(1.0, -0.2), P(0.72, 0.16), P(0.0, 0.3), P(-0.72, 0.16) }, top, outline);
        DrawPolygon(context, new[] { P(-0.64, -0.54), P(0.0, -0.66), P(0.72, -0.52), P(0.38, -0.24), P(-0.26, -0.18) }, topLight, null);
        context.DrawEllipse(center, goldLine, P(0.02, -0.16), size * 0.48, size * 0.22);
        context.DrawEllipse(glow, null, P(0.02, -0.2), size * 0.82, size * 0.36);
        context.DrawLine(smokePen, P(-0.08, -0.42), P(0.04, -0.82));
        context.DrawLine(smokePen, P(0.14, -0.38), P(0.28, -0.74));
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(165, 255, 255, 245)), null, P(0.06, -0.92), size * 0.11, size * 0.1);

        if (rim.Thickness > 1.8)
            context.DrawEllipse(null, rim, point, size * 1.08, size * 0.86);
    }

    private static void DrawFlightTargetMarker(DrawingContext context, Point point, double size, IBrush fill, Pen rim, string stamp, string markerText)
    {
        string text = markerText.ToLowerInvariant();
        if (text.Contains("airplane") || text.Contains("plane") || text.Contains("copter"))
        {
            DrawActorMarker(context, point, size, fill, rim, stamp);
            return;
        }

        if (text.Contains("arch"))
        {
            Pen arch = new(new SolidColorBrush(Color.FromRgb(122, 211, 255)), Math.Max(1.3, size * 0.16));
            Point leftFoot = new(point.X - (size * 0.82), point.Y + (size * 1.08));
            Point leftShoulder = new(point.X - (size * 0.72), point.Y - (size * 0.22));
            Point top = new(point.X, point.Y - (size * 1.1));
            Point rightShoulder = new(point.X + (size * 0.72), point.Y - (size * 0.22));
            Point rightFoot = new(point.X + (size * 0.82), point.Y + (size * 1.08));
            context.DrawLine(arch, leftFoot, leftShoulder);
            context.DrawLine(arch, leftShoulder, top);
            context.DrawLine(arch, top, rightShoulder);
            context.DrawLine(arch, rightShoulder, rightFoot);
            context.DrawLine(arch, new Point(point.X - (size * 0.72), point.Y + (size * 0.42)), new Point(point.X - (size * 0.72), point.Y + (size * 1.08)));
            context.DrawLine(arch, new Point(point.X + (size * 0.72), point.Y + (size * 0.42)), new Point(point.X + (size * 0.72), point.Y + (size * 1.08)));
            DrawMobyStamp(context, point + new Vector(0, size * 0.18), stamp, size);
            return;
        }

        if (text.Contains("ring"))
        {
            Pen ring = new(new SolidColorBrush(Color.FromRgb(255, 221, 91)), Math.Max(1.6, size * 0.18));
            context.DrawEllipse(null, ring, point, size * 0.95, size * 0.95);
            context.DrawEllipse(null, rim, point, size * 0.48, size * 0.48);
            DrawMobyStamp(context, point, stamp, size);
            return;
        }

        if (text.Contains("timer"))
        {
            context.DrawEllipse(fill, rim, point, size, size);
            Pen hand = new(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), Math.Max(1.1, size * 0.12));
            context.DrawLine(hand, point, new Point(point.X, point.Y - (size * 0.62)));
            context.DrawLine(hand, point, new Point(point.X + (size * 0.46), point.Y + (size * 0.18)));
            DrawMobyStamp(context, point, stamp, size);
            return;
        }

        if (text.Contains("boat"))
        {
            DrawPolygon(context, new[]
            {
                new Point(point.X - size, point.Y),
                new Point(point.X - (size * 0.62), point.Y + (size * 0.62)),
                new Point(point.X + (size * 0.62), point.Y + (size * 0.62)),
                new Point(point.X + size, point.Y)
            }, fill, rim);
            DrawPolygon(context, new[]
            {
                new Point(point.X - (size * 0.24), point.Y - (size * 0.12)),
                new Point(point.X + (size * 0.52), point.Y - (size * 0.02)),
                new Point(point.X - (size * 0.24), point.Y - (size * 0.72))
            }, new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), rim);
            DrawMobyStamp(context, point + new Vector(0, size * 0.2), stamp, size);
            return;
        }

        context.DrawEllipse(null, rim, point, size, size);
        context.DrawEllipse(fill, null, point, size * 0.42, size * 0.42);
        DrawMobyStamp(context, point, stamp, size);
    }

    private static string GetMobyStamp(Moby moby)
    {
        string text = GetMobyMarkerText(moby).ToLowerInvariant();
        if (moby.VisualKind == MobyVisualKind.Key)
            return "K";
        if (moby.VisualKind == MobyVisualKind.Gem && moby.Gem.Value > 0)
            return moby.Gem.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (text.Contains("key chest") || text.Contains("locked") || text.Contains("unlock chest"))
            return "Key";
        if (text.Contains("spring chest"))
            return "Sp";
        if (text.Contains("life chest"))
            return "1UP";
        if (text.Contains("firework"))
            return "Fw";
        if (text.Contains("timer number") || text.Contains("flight timer"))
            return "#";
        if (text.Contains("ring"))
            return "Rg";
        if (text.Contains("arch"))
            return "Ar";
        if (text.Contains("lighthouse"))
            return "Li";
        if (text.Contains("boat"))
            return "Bt";
        if (text.Contains("train"))
            return "Tn";
        if (text.Contains("torch"))
            return "To";
        if (text.Contains("fat momma") || text.Contains("fatmomma") || text.Contains("fat lady") || text.Contains("fatlady") || text.Contains("cauldron encounter") || text.Contains("cauldron support") || text.Contains("cauldron reward"))
            return "FLC";
        if (text.Contains("armored druid"))
            return "ADr";
        if (text.Contains("green druid"))
            return "GDr";
        if (text.Contains("green wizard") || text.Contains("lightning wizard"))
            return "GWz";
        if (text.Contains("elder wizard"))
            return "EWz";
        if (text.Contains("tornado wizard"))
            return "TWz";
        if (text.Contains("metalback spider"))
            return "MSp";
        if (text.Contains("balloonist") || text.Contains("baloonist"))
            return "Bst";
        if (IsTransportBalloonMarkerText(text))
            return "Bal";
        if (text.Contains("cannon"))
            return "Cn";
        if (text.Contains("tent"))
            return "Te";
        if (text.Contains("flight chest") || text.Contains("chest target"))
            return "Ct";
        if (text.Contains("metal"))
            return "M";
        if (text.Contains("banana boy"))
            return "Ban";
        if (text.Contains("strongarm") || text.Contains("strong arm"))
            return "Str";
        if (text.Contains("winged fool") || text.Contains("winged guy") || text.Contains("winged guys"))
            return "WFo";
        if (text.Contains("armored fool") || text.Contains("armored guy") || text.Contains("armored guys"))
            return "AFo";
        if (text.Contains("lamp fool") || text.Contains("lamp guy"))
            return "LFo";
        if (text.Contains("devil cupid"))
            return "DCp";
        if (text.Contains("puppy") || text.Contains("devil puppy") || text.Contains("devil dog"))
            return "DDg";
        if (text.Contains("mutant turtle") || text.Contains("turtle"))
            return "Tur";
        if (text.Contains("mushroom"))
            return "Msh";
        if (text.Contains("dog"))
            return "Dg";
        if (text.Contains("dragon-eating plant") || text.Contains("dragon eating plant"))
            return "DEP";
        if (text.Contains("attack frog") || text.Contains("frog"))
            return "Frg";
        if (text.Contains("egg thief"))
            return "Egg";
        if (text.Contains("airplane") || text.Contains("plane") || text.Contains("copter target"))
            return "Air";
        if (text.Contains("copter"))
            return "Cp";
        if (text.Contains("bull"))
            return "Bl";
        if (text.Contains("boar"))
            return "Bor";
        if (text.Contains("bird"))
            return "Bd";
        if (text.Contains("spotted chicken"))
            return "SCk";
        if (text.Contains("chicken"))
            return "Ck";
        if (text.Contains("kamikaze") || text.Contains("kamikazi"))
            return "Gn";
        if (text.Contains("gnorc"))
            return "Gn";
        if (text.Contains("sheep") || text.Contains("fodder"))
            return "Fd";
        if (text.Contains("ram"))
            return "Rm";
        if (text.Contains("shepherd"))
            return "Sh";
        if (text.Contains("toasty") || text.Contains("boss"))
            return "Bo";
        if (text.Contains("thief"))
            return "Th";
        if (text.Contains("cactus"))
            return "Ca";
        if (text.Contains("tree"))
            return "Tr";
        if (text.Contains("platform scenery") || text.Contains("platform prop") || text.Contains("dragon-platform scenery") || text.Contains("dragon platform scenery"))
            return "Pl";
        if (text.Contains("flower"))
            return "Fl";
        if (text.Contains("grass"))
            return "G";
        if (text.Contains("lamp"))
            return "L";
        if (text.Contains("flag"))
            return "F";
        if (text.Contains("flame/charge") || text.Contains("flame-or-charge"))
            return "F/C";
        if (text.Contains("breakable"))
            return "Br";
        if (text.Contains("charge"))
            return "Ch";
        if (text.Contains("chest"))
            return "C";
        if (text.Contains("dragon"))
            return "D";
        if (text.Contains("return-home") || text.Contains("return home"))
            return "H";
        if (text.Contains("portal"))
            return "P";
        if (text.Contains("whirlwind"))
            return "W";
        if (text.Contains("control") || text.Contains("nonvisual") || text.Contains("helper"))
            return "!";

        return moby.VisualKind switch
        {
            MobyVisualKind.Actor => "A",
            MobyVisualKind.FlightTarget => "Ft",
            MobyVisualKind.Scenery => "S",
            MobyVisualKind.Control => "!",
            _ => ""
        };
    }

    private static bool IsTransportBalloonMarkerText(string text)
    {
        return !text.Contains("balloonist") &&
            !text.Contains("baloonist") &&
            !text.Contains("balloon gnorc") &&
            !text.Contains("balloognorc") &&
            (text.Contains("transport balloon") ||
                text.Contains("balloon scenery") ||
                text.Contains("balloon object") ||
                text.Contains("balloon prop") ||
                text == "balloon");
    }

    private static string GetMobyMarkerText(Moby moby)
    {
        return $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote} {moby.CrossLevelFamily}";
    }

    private static void DrawMobyStamp(DrawingContext context, Point point, string stamp, double size)
    {
        if (string.IsNullOrWhiteSpace(stamp) || size < 6.2)
            return;

        double lengthScale = stamp.Length <= 1 ? 0.92 : stamp.Length == 2 ? 0.78 : 0.58;
        FormattedText text = new(
            stamp,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Clamp(size * lengthScale, 6.5, 13),
            new SolidColorBrush(Color.FromArgb(225, 20, 24, 29)));
        context.DrawText(text, new Point(point.X - (text.Width * 0.5), point.Y - (text.Height * 0.52)));
    }

    private static void DrawTinyQuestionMark(DrawingContext context, Point point, double size)
    {
        if (size < 7)
            return;

        FormattedText text = new(
            "?",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Clamp(size * 1.15, 8, 15),
            new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)));
        context.DrawText(text, new Point(point.X - (text.Width * 0.5), point.Y - (text.Height * 0.58)));
    }

    private void DrawMobyLabels(DrawingContext context, Rect bounds, IReadOnlyList<VisibleMoby> visible, IReadOnlySet<int> linkedTrueIndexes)
    {
        List<Rect> claimed = new();
        bool terrainFocus = IsTerrainVisualFocusActive();
        if (terrainFocus)
            return;

        if (_selectedMobyIndex < 0)
        {
            DrawAmbientMobyLabels(context, bounds, visible, linkedTrueIndexes, claimed);
            return;
        }

        VisibleMoby? selected = visible.FirstOrDefault(item => item.Index == _selectedMobyIndex);
        if (selected == null)
        {
            DrawAmbientMobyLabels(context, bounds, visible, linkedTrueIndexes, claimed);
            return;
        }

        DrawMobyLabel(context, selected.Point + new Vector(12, -30), BuildMobyMapLabel(selected.Moby, true), true, claimed);

        int drawn = 0;
        foreach (VisibleMoby linked in visible
            .Where(item => linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .OrderBy(item => DistanceSquared(item.Point, selected.Point)))
        {
            if (drawn >= 8)
                break;

            DrawMobyLabel(context, linked.Point + new Vector(10, -24 - ((drawn % 3) * 4)), BuildMobyMapLabel(linked.Moby, true), false, claimed);
            drawn++;
        }

        DrawAmbientMobyLabels(context, bounds, visible, linkedTrueIndexes, claimed);
    }

    private void DrawAmbientMobyLabels(DrawingContext context, Rect bounds, IReadOnlyList<VisibleMoby> visible, IReadOnlySet<int> linkedTrueIndexes, List<Rect> claimed)
    {
        bool flyLabels = _viewMode == ViewportViewMode.Fly3D;
        if (!flyLabels && _zoom < 1.65)
            return;

        int maxLabels = Math.Clamp((int)(bounds.Width / 42), 10, 42);
        int drawn = 0;
        IEnumerable<VisibleMoby> candidates = visible
            .Where(item => item.Index != _selectedMobyIndex)
            .Where(item => !linkedTrueIndexes.Contains(item.Moby.TrueIndex))
            .Where(item => ShouldDrawAmbientMobyLabel(item.Moby, flyLabels))
            .Where(item => flyLabels ? item.Size >= 8.2 && item.Depth < 9000 : bounds.Contains(item.Point))
            .OrderBy(item => MobyAmbientLabelPriority(item.Moby))
            .ThenBy(item => flyLabels ? item.Depth : DistanceSquared(item.Point, bounds.Center));

        foreach (VisibleMoby item in candidates)
        {
            if (drawn >= maxLabels)
                break;

            string label = ShortMobyLabel(item.Moby);
            if (string.IsNullOrWhiteSpace(label))
                continue;

            Point point = item.Point + new Vector(item.Size + 6, -item.Size - 14);
            if (DrawMobyLabel(context, point, label, false, claimed, true))
                drawn++;
        }
    }

    private bool ShouldDrawAmbientMobyLabel(Moby moby, bool flyLabels)
    {
        if (IsQuestionableMobyMarker(moby))
            return true;

        if (moby.VisualKind == MobyVisualKind.Gem)
            return _zoom >= 2.55 || flyLabels && moby.Gem.Value >= 10;

        if (moby.VisualKind is MobyVisualKind.Control or MobyVisualKind.Scenery)
            return _zoom >= 2.15 || flyLabels && moby.VisualKind == MobyVisualKind.Scenery;

        return true;
    }

    private static int MobyAmbientLabelPriority(Moby moby)
    {
        if (IsQuestionableMobyMarker(moby))
            return 0;

        string text = GetMobyMarkerText(moby).ToLowerInvariant();
        if (text.Contains("balloonist") || text.Contains("baloonist"))
            return 1;
        if (moby.VisualKind is MobyVisualKind.Dragon or MobyVisualKind.Portal or MobyVisualKind.Whirlwind or MobyVisualKind.Key)
            return 1;
        if (text.Contains("key chest") || text.Contains("life chest") || text.Contains("spring chest"))
            return 2;
        if (moby.VisualKind is MobyVisualKind.Chest or MobyVisualKind.Actor or MobyVisualKind.FlightTarget)
            return 3;
        if (moby.VisualKind == MobyVisualKind.Scenery)
            return 4;
        if (moby.VisualKind == MobyVisualKind.Control)
            return 5;
        if (moby.VisualKind == MobyVisualKind.Gem)
            return 6;

        return 7;
    }

    private static string ShortMobyLabel(Moby moby)
    {
        string label = moby.DisplayLabel;
        if (IsQuestionableMobyMarker(moby))
        {
            if (!HasSpecificMobyLabel(moby))
            {
                string text = GetMobyMarkerText(moby).ToLowerInvariant();
                if (text.Contains("scenery") || text.Contains("prop"))
                    label = "Needs ID: Scenery";
                else if (text.Contains("actor") || text.Contains("enemy") || text.Contains("container"))
                    label = "Needs ID: Actor";
                else if (text.Contains("control") || text.Contains("helper") || text.Contains("nonvisual"))
                    label = "Needs ID: Control";
                else if (text.Contains("flight") || text.Contains("special"))
                    label = "Needs ID: Special";
                else
                    label = "Needs ID";
            }
        }

        int observed = label.IndexOf(" (", StringComparison.Ordinal);
        if (observed > 0)
            label = label[..observed];

        label = label
            .Replace(" (1)", "", StringComparison.Ordinal)
            .Replace(" object?", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" candidate", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (label.Length > 24)
            label = label[..24].TrimEnd() + "...";

        return label;
    }

    private static bool HasSpecificMobyLabel(Moby moby)
    {
        string label = moby.DisplayLabel.Trim();
        if (string.IsNullOrWhiteSpace(label) ||
            label.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, Moby.FallbackLabel(moby.Type), StringComparison.OrdinalIgnoreCase))
            return false;

        string text = label.ToLowerInvariant();
        if (text.Contains("?") ||
            text.Contains("unknown") ||
            text is "object" or "moby" or "prop" or "scenery" or "actor" or "enemy" or "chest" or "control" or "helper" or "marker" ||
            text.Contains("object?") ||
            text.Contains("marker?") ||
            text.Contains("scenery/prop object") ||
            text.Contains("actor/container object") ||
            text.Contains("nonvisual control marker"))
            return false;

        return true;
    }

    private static string BuildMobyMapLabel(Moby moby, bool includeIndex)
    {
        string label = ShortMobyLabel(moby);
        if (string.IsNullOrWhiteSpace(label))
            label = moby.VisualKind == MobyVisualKind.Unknown ? "Unknown moby" : moby.VisualKind.ToString();

        return includeIndex
            ? $"{moby.DisplayIndex} {label}"
            : label;
    }

    private static bool DrawMobyLabel(DrawingContext context, Point point, string label, bool selected, List<Rect>? claimed = null, bool compact = false)
    {
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            selected ? 12 : compact ? 10 : 11,
            selected
                ? new SolidColorBrush(Color.FromRgb(32, 38, 45))
                : new SolidColorBrush(Color.FromRgb(230, 237, 244)))
        {
            MaxTextWidth = selected ? 220 : compact ? 128 : 180
        };

        Rect background = new(point - new Vector(5, 3), new Size(text.Width + 10, text.Height + 6));
        if (claimed != null)
        {
            Rect padded = background.Inflate(new Thickness(3));
            if (claimed.Any(existing => existing.Intersects(padded)))
                return false;

            claimed.Add(padded);
        }

        context.FillRectangle(
            selected
                ? new SolidColorBrush(Color.FromArgb(235, 255, 236, 127))
                : new SolidColorBrush(Color.FromArgb((byte)(compact ? 185 : 215), 34, 42, 51)),
            background,
            4);
        context.DrawText(text, point);
        return true;
    }

    private void DrawViewportChrome(DrawingContext context, Rect bounds)
    {
        Typeface typeface = new("Inter");
        string brushSafety = string.IsNullOrWhiteSpace(_terrainBrushSafetyHint) ? "" : $"    {_terrainBrushSafetyHint}";
        string help = BuildViewportHelpText(brushSafety);
        FormattedText text = new(
            help,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            13,
            new SolidColorBrush(Color.FromArgb(210, 224, 232, 240)));
        context.DrawText(text, new Point(bounds.Left + 18, bounds.Top + 16));
        DrawTerrainSurfaceLabels(context, bounds);
        DrawTerrainTargetReadout(context, bounds);
        DrawTerrainPatchLegend(context, bounds);
    }

    private string BuildViewportHelpText(string brushSafety)
    {
        bool flyView = _viewMode == ViewportViewMode.Fly3D;
        string mode = flyView ? "Game Camera" : "Edit Map";
        string gameTerrainStatus = flyView && _flipMapY
            ? _flyGameViewCameraLocal && !_flyCameraIsOverview
                ? "    all editable terrain visible"
                : "    complete terrain overview"
            : "";
        if (_terrainTexturePaintMode)
        {
            string paintState = _terrainTexturePaintBusy ? "applying..." : "click a terrain face to paint";
            string source = string.IsNullOrWhiteSpace(_terrainTexturePaintHint)
                ? "selected texture"
                : _terrainTexturePaintHint;
            return $"{mode} texture paint: {source}    {paintState}    Esc stop{gameTerrainStatus}";
        }

        if (_terrainBrushAction != TerrainBrushAction.Off)
            return $"{mode} brush: {TerrainBrushActionLabel(_terrainBrushAction)} size {_terrainBrushRadius:0} strength {_terrainBrushStrength:0} feather {_terrainBrushFeather:0}%{brushSafety}    1-5 switch    0 off    Shift/Option/Ctrl-scroll{gameTerrainStatus}";

        if (IsTerrainVisualFocusActive())
        {
            string navigation = flyView
                ? "W/A/S/D move    Q up / E down (fine)    right-drag look"
                : "scroll zoom    right/middle drag pan";
            return $"{mode} terrain: {navigation}    click face    drag move    Shift height    Alt/Option copy    Del remove    C/V copy look{gameTerrainStatus}";
        }

        return flyView
            ? $"Game Camera: W/A/S/D move    Q up / E down (fine)    right-drag look    drag objects to move{gameTerrainStatus}"
            : "Edit Map: scroll zoom    right/middle drag pan    click object select";
    }

    private void DrawTerrainTargetReadout(DrawingContext context, Rect bounds)
    {
        if (!IsTerrainVisualFocusActive())
            return;

        GeometryCandidate? geometry = Geometry;
        if (geometry == null)
            return;

        int terrainIndex = _hoverTerrainIndex >= 0
            ? _hoverTerrainIndex
            : _terrainBrushPreviewTerrainIndex >= 0
                ? _terrainBrushPreviewTerrainIndex
                : _selectedTerrainIndex;
        if (terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count)
            return;

        TerrainPolygon terrain = geometry.Polygons[terrainIndex];
        Color accent = TerrainSurfaceLabelColor(terrain);
        TerrainPatchSafetyKind safety = TerrainPatchSafetyFor(terrain);
        string editStatus = terrain.IsTerrainEdited
            ? TerrainPatchSafetyLabel(safety)
            : "Original";
        string texture = terrain.TextureId >= 0 ? $"Texture {terrain.TextureId}" : "No texture";
        string visualRole = IsMapUnderlaySurface(terrain.Surface)
            ? "Underlay surface"
            : "Foreground terrain";
        string label =
            $"Face {terrainIndex}  |  {TerrainMaterialClassifier.FormatSurface(terrain.Surface)}  |  {visualRole}  |  {texture}  |  {TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior)}  |  {editStatus}";

        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            11,
            new SolidColorBrush(Color.FromArgb(230, 236, 242, 248)))
        {
            MaxTextWidth = Math.Max(220, bounds.Width - 440)
        };

        Rect panel = new(
            bounds.Left + 18,
            bounds.Bottom - text.Height - 32,
            text.Width + 32,
            text.Height + 14);
        Color fill = BlendColor(accent, Color.FromRgb(20, 26, 33), 0.82);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(218, fill.R, fill.G, fill.B)), panel, 6);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(152, accent.R, accent.G, accent.B)), 1),
            panel,
            6,
            6);

        Point swatch = new(panel.Left + 12, panel.Top + (panel.Height * 0.5));
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(230, accent.R, accent.G, accent.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(210, 245, 249, 252)), 0.9),
            swatch,
            4.4,
            4.4);
        context.DrawText(text, new Point(panel.Left + 22, panel.Top + 7));
    }

    private void DrawTerrainSurfaceLabels(DrawingContext context, Rect bounds)
    {
        if (_screenTerrainSurfaceLabels.Count == 0)
            return;

        List<Rect> claimed =
        [
            new Rect(bounds.Left, bounds.Top, bounds.Width, 48)
        ];

        foreach (ScreenTerrainSurfaceLabel label in _screenTerrainSurfaceLabels.OrderByDescending(label => label.Score))
        {
            FormattedText text = new(
                label.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                10.5,
                new SolidColorBrush(Color.FromArgb(225, 236, 242, 248)))
            {
                MaxTextWidth = 120
            };

            Rect background = new(
                label.Point.X - (text.Width * 0.5) - 17,
                label.Point.Y - (text.Height * 0.5) - 5,
                text.Width + 34,
                text.Height + 10);
            Rect padded = background.Inflate(new Thickness(5));
            if (claimed.Any(rect => rect.Intersects(padded)))
                continue;

            claimed.Add(padded);
            Color accent = label.Color;
            Color fill = BlendColor(accent, Color.FromRgb(21, 27, 34), 0.78);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(208, fill.R, fill.G, fill.B)), background, 5);
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(150, accent.R, accent.G, accent.B)), 1),
                background,
                5,
                5);

            Point swatch = new(background.Left + 11, background.Top + (background.Height * 0.5));
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(228, accent.R, accent.G, accent.B)),
                new Pen(new SolidColorBrush(Color.FromArgb(210, 245, 249, 252)), 0.9),
                swatch,
                4.2,
                4.2);
            context.DrawText(text, new Point(background.Left + 20, background.Top + 5));
        }
    }

    private void DrawTerrainPatchLegend(DrawingContext context, Rect bounds)
    {
        GeometryCandidate? geometry = Geometry;
        if (geometry == null || geometry.Polygons.All(polygon => !polygon.IsTerrainEdited))
            return;

        (TerrainPatchSafetyKind Kind, string Label)[] items =
        [
            (TerrainPatchSafetyKind.Structural, "Structure"),
            (TerrainPatchSafetyKind.PlayableHeight, "Playable"),
            (TerrainPatchSafetyKind.PartialHeight, "Partial"),
            (TerrainPatchSafetyKind.VisualOnlyHeight, "Visual-only"),
            (TerrainPatchSafetyKind.UnknownHeight, "Unknown"),
            (TerrainPatchSafetyKind.TextureOnly, "Texture")
        ];

        double rowHeight = 18;
        double width = 136;
        double height = 12 + (items.Length * rowHeight);
        Rect panel = new(bounds.Right - width - 14, bounds.Bottom - height - 14, width, height);
        if (panel.Left < bounds.Left + 18 || panel.Top < bounds.Top + 42)
            return;

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(176, 24, 30, 37)), panel, 6);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(110, 224, 232, 240)), 1), panel, 6, 6);

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            Point swatch = new(panel.Left + 14, panel.Top + 14 + (i * rowHeight));
            Color color = TerrainPatchSafetyPenColor(item.Kind);
            context.DrawEllipse(new SolidColorBrush(color), new Pen(new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), 1), swatch, 5, 5);
            FormattedText label = new(
                item.Label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                10.5,
                new SolidColorBrush(Color.FromArgb(225, 230, 237, 244)));
            context.DrawText(label, new Point(swatch.X + 12, swatch.Y - 7));
        }
    }

    private static void DrawMobyLegend(DrawingContext context, Rect bounds)
    {
        (MobyVisualKind Kind, string Stamp, string Label, Color Color, string MarkerText)[] items =
        [
            (MobyVisualKind.Gem, "", "Gem", Color.FromRgb(24, 78, 232), ""),
            (MobyVisualKind.Key, "", "Key", Color.FromRgb(241, 196, 15), ""),
            (MobyVisualKind.Chest, "", "Key Chest", Color.FromRgb(146, 157, 176), "key chest"),
            (MobyVisualKind.Chest, "", "Flame/Charge", Color.FromRgb(230, 126, 34), "flame/charge chest"),
            (MobyVisualKind.Chest, "", "Charge Chest", Color.FromRgb(226, 232, 240), "charge chest"),
            (MobyVisualKind.Actor, "Gn", "Enemy", Color.FromRgb(93, 196, 45), "enemy"),
            (MobyVisualKind.Actor, "Egg", "Egg Thief", Color.FromRgb(42, 91, 150), "egg thief"),
            (MobyVisualKind.Actor, "Fd", "Fodder", Color.FromRgb(238, 229, 177), "sheep fodder"),
            (MobyVisualKind.Scenery, "Tr", "Tree", Color.FromRgb(72, 183, 68), "tree"),
            (MobyVisualKind.Portal, "", "Return Home", Color.FromRgb(225, 171, 37), "return home"),
            (MobyVisualKind.Scenery, "Fl", "Scenery", Color.FromRgb(63, 176, 117), ""),
            (MobyVisualKind.Dragon, "", "Dragon", Color.FromRgb(24, 197, 126), ""),
            (MobyVisualKind.Portal, "P", "Portal", Color.FromRgb(73, 192, 211), ""),
            (MobyVisualKind.Control, "!", "Control", Color.FromRgb(143, 166, 184), "")
        ];

        double rowHeight = 22;
        double width = 186;
        double height = 12 + (items.Length * rowHeight);
        Rect panel = new(bounds.Right - width - 14, bounds.Top + 48, width, height);
        if (panel.Left < bounds.Left + 18)
            return;

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(190, 24, 30, 37)), panel, 6);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 224, 232, 240)), 1), panel, 6, 6);

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            Point markerPoint = new(panel.Left + 16, panel.Top + 16 + (i * rowHeight));
            DrawLegendMarker(context, markerPoint, item.Kind, item.Stamp, item.Color, item.MarkerText);
            FormattedText label = new(
                item.Label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                11,
                new SolidColorBrush(Color.FromArgb(225, 230, 237, 244)));
            context.DrawText(label, new Point(panel.Left + 38, markerPoint.Y - 8));
        }
    }

    private static void DrawLegendMarker(DrawingContext context, Point point, MobyVisualKind kind, string stamp, Color color, string markerText = "")
    {
        IBrush fill = new SolidColorBrush(color);
        Pen rim = new(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 1.3);
        string markerTextLower = markerText.ToLowerInvariant();
        double size = kind == MobyVisualKind.Chest
            ? 7.4
            : kind == MobyVisualKind.Key || IsReturnHomeText(markerTextLower) || stamp is "Egg" or "Fd" or "Tr"
                ? 6.8
                : 6.2;
        switch (kind)
        {
            case MobyVisualKind.Gem:
                DrawGemMarker(context, point, size, color, rim, GemValue.Blue);
                break;
            case MobyVisualKind.Key:
                DrawKeyMarker(context, point, size, rim);
                break;
            case MobyVisualKind.Chest:
                DrawChestMarker(context, point, size, fill, rim, stamp, string.IsNullOrWhiteSpace(markerText) ? "flame/charge chest" : markerText, GemValue.Blue);
                break;
            case MobyVisualKind.Actor:
                DrawActorMarker(context, point, size, fill, rim, stamp);
                break;
            case MobyVisualKind.FlightTarget:
                DrawFlightTargetMarker(context, point, size, fill, rim, stamp, string.IsNullOrWhiteSpace(markerText) ? "flight arch" : markerText);
                break;
            case MobyVisualKind.Scenery:
                DrawSceneryMarker(context, point, size, fill, rim, stamp);
                break;
            case MobyVisualKind.Dragon:
                DrawDragonStatueMarker(context, point, size, rim);
                break;
            case MobyVisualKind.Portal:
                if (IsReturnHomeText(markerTextLower))
                {
                    DrawReturnHomeMarker(context, point, size, rim);
                    break;
                }

                context.DrawEllipse(null, rim, point, size, size * 1.35);
                context.DrawEllipse(fill, null, point, size * 0.42, size * 0.72);
                DrawMobyStamp(context, point, stamp, size);
                break;
            case MobyVisualKind.Control:
                context.DrawRectangle(fill, rim, new Rect(point.X - (size * 0.82), point.Y - (size * 0.82), size * 1.64, size * 1.64), 1, 1);
                DrawMobyStamp(context, point, stamp, size);
                break;
        }
    }

    private void ResetFlyCamera(bool preferGameViewStart)
    {
        if (!_holdFlyNavigationMaterialLodForTesting)
        {
            _flyNavigationSettleTimer.Stop();
            _flyNavigationInteractiveMaterialLod = false;
            _activeFlyNavigationKeys.Clear();
        }
        _flyGameViewCameraLocal = false;
        _flyCameraIsOverview = true;
        // Fit/Reset is an exhaustive editor overview, not a camera pose the
        // retail game can reach. Keep all HP source geometry visible until the
        // user returns to the local native-group Game Camera.
        _nativeTerrainLodPreviewMode = NativeTerrainLodPreviewMode.EditorOverviewHighDetail;
        if (preferGameViewStart && TrySetFlyGameViewStartCamera())
        {
            _flyGameViewCameraLocal = true;
            _flyCameraIsOverview = false;
            return;
        }

        if (Geometry != null && Geometry.Polygons.Count > 0)
        {
            FitFlyCameraToScene(GetSceneFitFocus(Geometry), EffectiveFlyFitBounds());
            return;
        }

        IReadOnlyList<Moby> mobys = Mobys.Where(moby => !moby.IsRemoved).ToList();
        if (mobys.Count == 0)
        {
            _flyCamera = new FlyCamera(0, 2400, 1800, -Math.PI * 0.5, -0.64);
            return;
        }

        FitFlyCameraToScene(BuildMobyFitFocus(mobys), EffectiveFlyFitBounds());
    }

    private bool TrySetFlyGameViewStartCamera()
    {
        if (_levelEntryPose != null)
        {
            SetFlyGameCameraFromEntryPose(
                _levelEntryPose.X,
                _levelEntryPose.Y,
                _levelEntryPose.Z,
                _levelEntryPose.YawByte,
                "portable retail entry cache");
            return true;
        }

        Moby? entry = Mobys.FirstOrDefault(moby => !moby.IsRemoved && moby.IsFlyInLandingControl);
        if (entry != null)
        {
            SetFlyGameCameraFromEntryPose(
                entry.Position.X,
                entry.Position.Y,
                entry.Position.Z,
                entry.YawByte,
                "source-disc fly-in marker fallback");
            return true;
        }

        List<Moby> focusMobys = Mobys
            .Where(moby =>
                !moby.IsRemoved &&
                moby.VisualKind is not MobyVisualKind.Control and not MobyVisualKind.Scenery)
            .ToList();
        if (focusMobys.Count == 0)
        {
            focusMobys = Mobys
                .Where(moby => !moby.IsRemoved && moby.VisualKind != MobyVisualKind.Control)
                .ToList();
        }
        if (focusMobys.Count == 0)
            return false;

        double targetX = Median(focusMobys.Select(moby => (double)moby.Position.X));
        double targetY = Median(focusMobys.Select(moby => (double)moby.Position.Y));
        double targetZ = Median(focusMobys.Select(moby => (double)moby.Position.Z)) + 100;
        _flyCamera = CreateOverviewCamera(
            ToFlyViewX(targetX),
            ToFlyViewY(targetY),
            targetZ,
            -Math.PI * 0.5,
            -0.28,
            1400);
        _gameCameraEntrySnapshot = GameCameraEntrySnapshot.Fallback(
            "No portable retail entry pose was available; used the median object fallback.",
            _flyCamera.X,
            _flyCamera.Y,
            _flyCamera.Z);
        return true;
    }

    private void SetFlyGameCameraFromEntryPose(
        double focusX,
        double focusY,
        double focusZ,
        int yawByte,
        string source)
    {
        Vector2f direction = FlyInLandingEditorControl.HeadingByteToWorldDirection(yawByte);
        double elevation = NativeEntryCameraElevationRaw * Math.Tau / 4096.0;
        double radius = NativeEntryCameraRadiusRaw / NativeEntryCameraCoordinateScale;
        double desiredBehind = radius * Math.Cos(elevation);
        double desiredRise = radius * Math.Sin(elevation);
        double appliedBehind = CollisionSafeEntryCameraBehindDistance(
            focusX,
            focusY,
            focusZ,
            direction,
            desiredBehind,
            desiredRise,
            out int collisionGroup);
        double appliedScale = desiredBehind <= 0 ? 0 : appliedBehind / desiredBehind;
        double appliedRise = desiredRise * appliedScale;
        double cameraWorldX = focusX - (direction.X * appliedBehind);
        double cameraWorldY = focusY - (direction.Y * appliedBehind);
        double cameraWorldZ = focusZ + appliedRise;

        SetFlyCameraLookingAt(
            ToFlyViewX(cameraWorldX),
            ToFlyViewY(cameraWorldY),
            cameraWorldZ,
            ToFlyViewX(focusX),
            ToFlyViewY(focusY),
            focusZ);
        _gameCameraEntrySnapshot = new GameCameraEntrySnapshot(
            PoseAvailable: true,
            Source: source,
            FocusX: focusX,
            FocusY: focusY,
            FocusZ: focusZ,
            YawByte: yawByte & 0xFF,
            DesiredBehind: desiredBehind,
            AppliedBehind: appliedBehind,
            AppliedRise: appliedRise,
            CameraX: cameraWorldX,
            CameraY: cameraWorldY,
            CameraZ: cameraWorldZ,
            CollisionGroup: collisionGroup,
            CollisionConstrained: appliedBehind + 0.001 < desiredBehind,
            Note: "Camera offset uses retail spherical preset D_8006C934 (radius 0xA00, elevation 0xA0); a native collision-group discontinuity can only step it inward.");
    }

    private double CollisionSafeEntryCameraBehindDistance(
        double focusX,
        double focusY,
        double focusZ,
        Vector2f direction,
        double desiredBehind,
        double desiredRise,
        out int collisionGroup)
    {
        collisionGroup = -1;
        NativeTerrainOcclusionData? occlusion = Geometry?.NativeTerrainOcclusion;
        if (occlusion == null || occlusion.EnvironmentGroups.Count == 0 || desiredBehind <= 0)
            return Math.Max(0, desiredBehind);

        const double maximumStep = 16.0;
        int steps = Math.Max(1, (int)Math.Ceiling(desiredBehind / maximumStep));
        int continuousGroup = -1;
        double accepted = 0;
        for (int step = 0; step <= steps; step++)
        {
            double behind = desiredBehind * step / steps;
            double scale = behind / desiredBehind;
            double x = focusX - (direction.X * behind);
            double y = focusY - (direction.Y * behind);
            double z = focusZ + (desiredRise * scale);
            if (!occlusion.TryResolveGroup(x, y, z, out int group, out _, out _) || group < 0)
                break;
            if (continuousGroup < 0)
                continuousGroup = group;
            else if (group != continuousGroup)
                break;

            accepted = behind;
            collisionGroup = group;
        }

        return accepted;
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
            return 0;
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) * 0.5
            : ordered[middle];
    }

    private Rect EffectiveFlyFitBounds()
    {
        Rect bounds = new(Bounds.Size);
        return bounds.Width >= 320 && bounds.Height >= 240
            ? bounds
            : new Rect(0, 0, 1280, 720);
    }

    private SceneFitFocus GetSceneFitFocus(GeometryCandidate geometry)
    {
        if (ReferenceEquals(_sceneFitGeometry, geometry) && _sceneFitFocus != null)
            return _sceneFitFocus;

        _sceneFitGeometry = geometry;
        _sceneFitFocus = BuildSceneFitFocus(geometry);
        return _sceneFitFocus;
    }

    private SceneFitFocus BuildSceneFitFocus(GeometryCandidate geometry)
    {
        List<TerrainPolygon> drawable = geometry.Polygons
            .Where(polygon =>
                !polygon.IsTerrainRemoved &&
                polygon.Points.Count >= 3 &&
                polygon.ZValues.Length >= 3 &&
                ShouldPresentTerrain(geometry, polygon))
            .ToList();
        List<TerrainPolygon> solids = drawable
            .Where(polygon => !RequiresGuardedNativeTerrainBlendFallback(polygon))
            .ToList();
        List<TerrainPolygon> focused = solids.Count > 0 ? solids : drawable;

        List<Vector3f> points = new(focused.Sum(polygon => Math.Min(polygon.Points.Count, polygon.ZValues.Length)));
        foreach (TerrainPolygon polygon in focused)
        {
            int count = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
            for (int index = 0; index < count; index++)
                points.Add(new Vector3f(polygon.Points[index].X, polygon.Points[index].Y, polygon.ZValues[index]));
        }

        // The retained internal filtered-view fixture can omit wide native
        // support plates. Keep every visible object inside its Fit bounds,
        // particularly flight targets that sit beyond nearby terrain.
        if (_terrainSceneViewMode == TerrainSceneViewMode.Playable)
        {
            foreach (Moby moby in Mobys.Where(moby =>
                !moby.IsRemoved && (_mobyFilter == null || _mobyFilter(moby))))
            {
                points.Add(moby.Position);
            }
        }

        if (points.Count == 0)
        {
            points.Add(new Vector3f(geometry.Bounds.Left, geometry.Bounds.Top, geometry.MinZ));
            points.Add(new Vector3f(geometry.Bounds.Right, geometry.Bounds.Bottom, geometry.MaxZ));
        }

        return SceneFitFocus.FromPoints(points, drawable.Count, focused.Count);
    }

    private static SceneFitFocus BuildMobyFitFocus(IReadOnlyList<Moby> mobys)
    {
        List<Vector3f> points = mobys
            .Select(moby => moby.Position)
            .ToList();
        if (points.Count == 1)
        {
            Vector3f point = points[0];
            points =
            [
                new Vector3f(point.X - 512, point.Y - 512, point.Z - 128),
                new Vector3f(point.X + 512, point.Y + 512, point.Z + 384)
            ];
        }
        return SceneFitFocus.FromPoints(points, 0, 0);
    }

    private void FitFlyCameraToScene(SceneFitFocus focus, Rect bounds)
    {
        const double yaw = -Math.PI * 0.5;
        const double pitch = -0.66;
        double targetX = ToFlyViewX((focus.Bounds.Left + focus.Bounds.Right) * 0.5);
        double targetY = ToFlyViewY((focus.Bounds.Top + focus.Bounds.Bottom) * 0.5);
        double targetZ = (focus.MinZ + focus.MaxZ) * 0.5;
        double span = Math.Max(Math.Max(focus.Bounds.Width, focus.Bounds.Height), focus.MaxZ - focus.MinZ);
        double low = Math.Max(256, span * 0.12);
        double high = Math.Max(2048, span * 1.5);
        FlyCamera candidate = CreateOverviewCamera(targetX, targetY, targetZ, yaw, pitch, high);
        for (int attempt = 0; attempt < 18 && !FlyCameraFits(bounds, focus.Points, candidate); attempt++)
        {
            high *= 1.5;
            candidate = CreateOverviewCamera(targetX, targetY, targetZ, yaw, pitch, high);
        }

        for (int attempt = 0; attempt < 36; attempt++)
        {
            double distance = (low + high) * 0.5;
            candidate = CreateOverviewCamera(targetX, targetY, targetZ, yaw, pitch, distance);
            if (FlyCameraFits(bounds, focus.Points, candidate))
                high = distance;
            else
                low = distance;
        }

        _flyCamera = CreateOverviewCamera(targetX, targetY, targetZ, yaw, pitch, high);
    }

    private static FlyCamera CreateOverviewCamera(
        double targetX,
        double targetY,
        double targetZ,
        double yaw,
        double pitch,
        double distance)
    {
        double horizontalDistance = distance * Math.Cos(pitch);
        return new FlyCamera(
            targetX - (Math.Cos(yaw) * horizontalDistance),
            targetY - (Math.Sin(yaw) * horizontalDistance),
            targetZ - (Math.Sin(pitch) * distance),
            yaw,
            pitch);
    }

    private bool FlyCameraFits(Rect bounds, IReadOnlyList<Vector3f> points, FlyCamera camera)
    {
        double horizontalMargin = bounds.Width * 0.06;
        double verticalMargin = bounds.Height * 0.10;
        Rect safeBounds = bounds.Deflate(new Thickness(horizontalMargin, verticalMargin));
        foreach (Vector3f source in points)
        {
            if (!TryProjectFly(bounds, camera, ToFlyViewX(source.X), ToFlyViewY(source.Y), source.Z, out ProjectedPoint projected) ||
                !safeBounds.Contains(projected.Screen))
            {
                return false;
            }
        }
        return true;
    }

    private void SetFlyCameraLookingAt(double cameraX, double cameraY, double cameraZ, double targetX, double targetY, double targetZ)
    {
        double dx = targetX - cameraX;
        double dy = targetY - cameraY;
        double dz = targetZ - cameraZ;
        double horizontal = Math.Sqrt((dx * dx) + (dy * dy));
        _flyCamera = new FlyCamera(
            cameraX,
            cameraY,
            cameraZ,
            Math.Atan2(dy, dx),
            Math.Clamp(Math.Atan2(dz, Math.Max(1, horizontal)), -1.0, 0.2));
    }

    private void MoveFlyCamera(double forward, double right, double up)
    {
        double cosYaw = Math.Cos(_flyCamera.Yaw);
        double sinYaw = Math.Sin(_flyCamera.Yaw);
        _flyCamera.X += (cosYaw * forward) + (-sinYaw * right);
        _flyCamera.Y += (sinYaw * forward) + (cosYaw * right);
        _flyCamera.Z += up;
    }

    private bool UsesNativeTerrainDistanceLod(GeometryCandidate geometry) =>
        _nativeTerrainLodPreviewMode == NativeTerrainLodPreviewMode.NativeDistance &&
        geometry.HasStaticLowDetailPreview &&
        geometry.LowDetailPolygons.Count > 0 &&
        geometry.SourceSectors.Count > 0;

    private bool UsesFlyGameViewHighDetailVisibility(GeometryCandidate geometry) =>
        _flipMapY &&
        _flyGameViewCameraLocal &&
        !_flyCameraIsOverview &&
        _nativeTerrainLodPreviewMode == NativeTerrainLodPreviewMode.EditorOverviewHighDetail &&
        geometry.SourceSectors.Count > 0;

    private NativeTerrainOcclusionSelection ResolveFlyTerrainOcclusion(GeometryCandidate geometry)
    {
        NativeTerrainOcclusionData? data = geometry.NativeTerrainOcclusion;
        if (data == null)
            return NativeTerrainOcclusionSelection.Unavailable;

        if (!ReferenceEquals(_lastFlyOcclusionGeometry, geometry))
        {
            _lastFlyOcclusionGeometry = geometry;
            _lastFlyOcclusionGroupIndex = -1;
            _lastFlyOcclusionTriangleIndex = -1;
            _lastFlyOcclusionFloorZ = double.NegativeInfinity;
        }

        bool hit = data.TryResolveGroup(
            FromFlyViewX(_flyCamera.X),
            FromFlyViewY(_flyCamera.Y),
            _flyCamera.Z,
            out int groupIndex,
            out int triangleIndex,
            out double floorZ);
        hit = hit && groupIndex >= 0;
        if (hit)
        {
            _lastFlyOcclusionGroupIndex = groupIndex;
            _lastFlyOcclusionTriangleIndex = triangleIndex;
            _lastFlyOcclusionFloorZ = floorZ;
        }
        else if (_lastFlyOcclusionGroupIndex >= 0)
        {
            // A free editor camera can leave every collision triangle even
            // though it is still looking at the same retail environment. Do
            // not turn that momentary miss into an all-sector fail-open frame;
            // preserve the last collision-proven group until another triangle
            // selects a new one or a different geometry instance is loaded.
            groupIndex = _lastFlyOcclusionGroupIndex;
            triangleIndex = _lastFlyOcclusionTriangleIndex;
            floorZ = _lastFlyOcclusionFloorZ;
        }
        return new NativeTerrainOcclusionSelection(
            Available: true,
            CollisionTriangleResolved: hit,
            GroupIndex: groupIndex,
            TriangleIndex: triangleIndex,
            FloorZ: floorZ,
            VisibleSectors: data.VisibleSectorsForGroup(groupIndex));
    }

    private FlyHighDetailGroupSelection ResolveFlyHighDetailGroup(
        GeometryCandidate geometry,
        NativeTerrainOcclusionSelection nativeSelection,
        IReadOnlyDictionary<int, SceneSectorRenderMetadata> sectors)
    {
        NativeTerrainOcclusionData? data = geometry.NativeTerrainOcclusion;
        if (data == null || data.EnvironmentGroups.Count == 0)
            return FlyHighDetailGroupSelection.AllSectors;

        if (nativeSelection.CollisionTriangleResolved && nativeSelection.VisibleSectors != null)
        {
            return new FlyHighDetailGroupSelection(
                nativeSelection.GroupIndex,
                nativeSelection.VisibleSectors,
                UsedProximityFallback: false);
        }

        // A free editor camera can be above or beyond the collision mesh. In
        // that case, choose the nearest forward source group as the foreground
        // camera context. Every unique sector still follows its own normal
        // HP/LP distance path; the context only wins projected overlap in the
        // editor's whole-scene compositor.
        Dictionary<int, int> preferredGroupBySector = new();
        for (int groupIndex = 0; groupIndex < data.EnvironmentGroups.Count; groupIndex++)
        {
            foreach (int sectorIndex in data.EnvironmentGroups[groupIndex])
            {
                if (!preferredGroupBySector.TryGetValue(sectorIndex, out int existing) ||
                    data.EnvironmentGroups[groupIndex].Count < data.EnvironmentGroups[existing].Count ||
                    (data.EnvironmentGroups[groupIndex].Count == data.EnvironmentGroups[existing].Count && groupIndex < existing))
                {
                    preferredGroupBySector[sectorIndex] = groupIndex;
                }
            }
        }

        int aimedGroupIndex = -1;
        double bestAimOffset = double.PositiveInfinity;
        double bestAimDepth = double.PositiveInfinity;
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (!preferredGroupBySector.TryGetValue(polygon.SectorIndex, out int groupIndex) ||
                !TryFlyCameraAimOffset(polygon.Center.X, polygon.Center.Y, polygon.AvgZ, out double aimOffset, out double depth))
            {
                continue;
            }

            if (aimOffset < bestAimOffset - 0.00000000000001 ||
                (Math.Abs(aimOffset - bestAimOffset) <= 0.00000000000001 && depth < bestAimDepth))
            {
                bestAimOffset = aimOffset;
                bestAimDepth = depth;
                aimedGroupIndex = groupIndex;
            }
        }
        if (aimedGroupIndex >= 0)
        {
            return new FlyHighDetailGroupSelection(
                aimedGroupIndex,
                data.VisibleSectorsForGroup(aimedGroupIndex),
                UsedProximityFallback: true);
        }

        foreach (SceneSectorRenderMetadata sector in sectors.Values
            .Where(sector =>
                FlyCameraDepth(sector.Center.X, sector.Center.Y, sector.Center.Z) +
                    Math.Max(0, sector.Radius) > 48)
            .OrderBy(SectorCameraSurfaceDistance)
            .ThenBy(sector => sector.SectorIndex))
        {
            int groupIndex = Enumerable.Range(0, data.EnvironmentGroups.Count)
                .Where(index => data.EnvironmentGroups[index].Contains(sector.SectorIndex))
                .OrderBy(index => data.EnvironmentGroups[index].Count)
                .ThenBy(index => index)
                .FirstOrDefault(-1);
            if (groupIndex >= 0)
            {
                return new FlyHighDetailGroupSelection(
                    groupIndex,
                    data.VisibleSectorsForGroup(groupIndex),
                    UsedProximityFallback: true);
            }
        }

        return nativeSelection.VisibleSectors != null
            ? new FlyHighDetailGroupSelection(
                nativeSelection.GroupIndex,
                nativeSelection.VisibleSectors,
                UsedProximityFallback: false)
            : FlyHighDetailGroupSelection.AllSectors;

        double SectorCameraSurfaceDistance(SceneSectorRenderMetadata sector)
        {
            double dx = ToFlyViewX(sector.Center.X) - _flyCamera.X;
            double dy = ToFlyViewY(sector.Center.Y) - _flyCamera.Y;
            double dz = sector.Center.Z - _flyCamera.Z;
            return Math.Max(0, Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) - Math.Max(0, sector.Radius));
        }

        bool TryFlyCameraAimOffset(
            double worldX,
            double worldY,
            double worldZ,
            out double aimOffset,
            out double depth)
        {
            double dx = ToFlyViewX(worldX) - _flyCamera.X;
            double dy = ToFlyViewY(worldY) - _flyCamera.Y;
            double dz = worldZ - _flyCamera.Z;
            double sinYaw = Math.Sin(_flyCamera.Yaw);
            double cosYaw = Math.Cos(_flyCamera.Yaw);
            double forwardHorizontal = (cosYaw * dx) + (sinYaw * dy);
            double right = (-sinYaw * dx) + (cosYaw * dy);
            double sinPitch = Math.Sin(_flyCamera.Pitch);
            double cosPitch = Math.Cos(_flyCamera.Pitch);
            depth = (forwardHorizontal * cosPitch) + (dz * sinPitch);
            double vertical = (dz * cosPitch) - (forwardHorizontal * sinPitch);
            if (depth <= 48)
            {
                aimOffset = double.PositiveInfinity;
                return false;
            }

            aimOffset = ((right * right) + (vertical * vertical)) / (depth * depth);
            return double.IsFinite(aimOffset);
        }
    }

    private NativeTerrainOcclusionSelection ResolveMapTerrainOcclusion(GeometryCandidate geometry)
    {
        NativeTerrainOcclusionData? data = geometry.NativeTerrainOcclusion;
        if (data == null)
            return NativeTerrainOcclusionSelection.Unavailable;

        bool hit = false;
        int groupIndex = -1;
        int triangleIndex = -1;
        double floorZ = double.NegativeInfinity;
        if (_levelEntryPose != null)
        {
            // Homeworlds do not necessarily expose a destination fly-in Moby,
            // but the portable cache carries the same retail entry XYZ used by
            // Game Camera. Seed Edit Map from it so opening Map first never
            // materializes every mutually-exclusive environment group.
            hit = data.TryResolveGroup(
                _levelEntryPose.X,
                _levelEntryPose.Y,
                _levelEntryPose.Z + 220,
                out groupIndex,
                out triangleIndex,
                out floorZ);
            hit = hit && groupIndex >= 0;
        }
        if (!hit || groupIndex < 0)
        {
            // A portable entry record and a source Moby are independent proofs.
            // If the pose sits just outside this level's collision tessellation,
            // still try the native fly-in control before failing open.
            Moby? entry = Mobys.FirstOrDefault(moby => !moby.IsRemoved && moby.IsFlyInLandingControl);
            if (entry != null)
            {
                hit = data.TryResolveGroup(
                    entry.Position.X,
                    entry.Position.Y,
                    entry.Position.Z + 220,
                    out groupIndex,
                    out triangleIndex,
                    out floorZ);
                hit = hit && groupIndex >= 0;
            }
        }
        if (!hit && groupIndex < 0 &&
            ReferenceEquals(_lastFlyOcclusionGeometry, geometry) &&
            _nativeTerrainOcclusionSnapshot.PayloadAvailable &&
            string.Equals(_nativeTerrainOcclusionSnapshot.View, "Game Camera", StringComparison.Ordinal) &&
            _nativeTerrainOcclusionSnapshot.GroupIndex >= 0 &&
            _nativeTerrainOcclusionSnapshot.GroupIndex < data.EnvironmentGroups.Count)
        {
            // Edit Map follows the most recent explicit camera context when a
            // source fly-in control is unavailable (for example, a portable
            // read-only cache without its configured disc beside it).
            hit = _nativeTerrainOcclusionSnapshot.CollisionTriangleResolved;
            groupIndex = _nativeTerrainOcclusionSnapshot.GroupIndex;
            triangleIndex = _nativeTerrainOcclusionSnapshot.TriangleIndex;
            floorZ = _nativeTerrainOcclusionSnapshot.FloorZ;
        }

        int selectedSector = Geometry != null &&
            _selectedTerrainIndex >= 0 &&
            _selectedTerrainIndex < Geometry.Polygons.Count
                ? Geometry.Polygons[_selectedTerrainIndex].SectorIndex
                : -1;
        IReadOnlySet<int>? visibleSectors = data.VisibleSectorsForGroup(groupIndex);
        if (selectedSector >= 0 && (visibleSectors == null || !visibleSectors.Contains(selectedSector)))
        {
            int selectedGroup = Enumerable.Range(0, data.EnvironmentGroups.Count)
                .Where(index => data.EnvironmentGroups[index].Contains(selectedSector))
                .OrderBy(index => data.EnvironmentGroups[index].Count)
                .ThenBy(index => index)
                .FirstOrDefault(-1);
            if (selectedGroup >= 0)
            {
                groupIndex = selectedGroup;
                visibleSectors = data.VisibleSectorsForGroup(groupIndex);
            }
        }

        return new NativeTerrainOcclusionSelection(
            Available: true,
            CollisionTriangleResolved: hit,
            GroupIndex: groupIndex,
            TriangleIndex: triangleIndex,
            FloorZ: floorZ,
            VisibleSectors: visibleSectors);
    }

    private HashSet<int> BuildFlyGameViewForcedSectorIndexes(GeometryCandidate geometry)
    {
        HashSet<int> result = [];
        for (int index = 0; index < geometry.Polygons.Count; index++)
        {
            TerrainPolygon polygon = geometry.Polygons[index];
            if (index == _selectedTerrainIndex ||
                index == _hoverTerrainIndex ||
                index == _terrainBrushPreviewTerrainIndex ||
                polygon.IsTerrainEdited ||
                polygon.IsTerrainRemoved ||
                polygon.IsTerrainAddClone)
            {
                result.Add(polygon.SectorIndex);
            }
        }
        return result;
    }

    private void ResumeNativeTerrainLodAfterFlyNavigation()
    {
        // Game Camera uses the local collision group plus native HP/LP distance
        // queues. Never enable that path from the impossible whole-level Fit
        // camera; the exhaustive overview remains an editing projection.
        if (_flipMapY && !_flyCameraIsOverview)
            _flyGameViewCameraLocal = true;
        BeginFlyNavigationMaterialLod(scheduleSettle: true);
    }

    private void BeginFlyNavigationMaterialLod(bool scheduleSettle)
    {
        if (_viewMode != ViewportViewMode.Fly3D ||
            _nativeTerrainLodPreviewMode != NativeTerrainLodPreviewMode.EditorOverviewHighDetail)
        {
            return;
        }

        _flyNavigationInteractiveMaterialLod = true;
        _flyNavigationSettleTimer.Stop();
        if (scheduleSettle)
            ScheduleFlyNavigationMaterialSettle();
    }

    private void ScheduleFlyNavigationMaterialSettle()
    {
        if (!_flyNavigationInteractiveMaterialLod)
            return;

        // Restart on every navigation event. This doubles as an inactivity
        // watchdog: if Avalonia/macOS misses KeyUp or pointer release, the
        // viewport still restores full source material after input goes quiet.
        _flyNavigationSettleTimer.Stop();
        _flyNavigationSettleTimer.Start();
    }

    private void SettleFlyNavigationMaterialLod(bool force)
    {
        _flyNavigationSettleTimer.Stop();
        if (!force && _holdFlyNavigationMaterialLodForTesting)
            return;
        if (force)
            _holdFlyNavigationMaterialLodForTesting = false;
        if (!_flyNavigationInteractiveMaterialLod)
            return;

        _flyNavigationInteractiveMaterialLod = false;
        InvalidateVisual();
    }

    private bool TryProjectFly(Rect bounds, double x, double y, double z, out ProjectedPoint point)
    {
        if (_nativeGteProjectionExperimentEnabled)
            return TryProjectNativeGteExperimentPoint(bounds, x, y, z, out point);
        return TryProjectFly(bounds, _flyCamera, ToFlyViewX(x), ToFlyViewY(y), z, out point);
    }

    private double FlyCameraDepth(double x, double y, double z)
    {
        if (_nativeGteProjectionExperimentEnabled && TryNativeGteExperimentDepth(x, y, z, out double nativeDepth))
            return nativeDepth;
        return FlyCameraDepth(_flyCamera, ToFlyViewX(x), ToFlyViewY(y), z);
    }

    private static double FlyCameraDepth(FlyCamera camera, double viewX, double viewY, double z)
    {
        double dx = viewX - camera.X;
        double dy = viewY - camera.Y;
        double dz = z - camera.Z;
        double forwardHorizontal = (Math.Cos(camera.Yaw) * dx) + (Math.Sin(camera.Yaw) * dy);
        return (forwardHorizontal * Math.Cos(camera.Pitch)) + (dz * Math.Sin(camera.Pitch));
    }

    private static bool TryProjectFly(
        Rect bounds,
        FlyCamera camera,
        double viewX,
        double viewY,
        double z,
        out ProjectedPoint point)
    {
        double dx = viewX - camera.X;
        double dy = viewY - camera.Y;
        double dz = z - camera.Z;
        double sinYaw = Math.Sin(camera.Yaw);
        double cosYaw = Math.Cos(camera.Yaw);
        double forwardHorizontal = (cosYaw * dx) + (sinYaw * dy);
        double right = (-sinYaw * dx) + (cosYaw * dy);
        double sinPitch = Math.Sin(camera.Pitch);
        double cosPitch = Math.Cos(camera.Pitch);
        double depth = (forwardHorizontal * cosPitch) + (dz * sinPitch);
        double vertical = (dz * cosPitch) - (forwardHorizontal * sinPitch);
        if (depth <= 48)
        {
            point = default;
            return false;
        }

        double focal = Math.Min(bounds.Width, bounds.Height) * 0.9;
        point = new ProjectedPoint(
            new Point(bounds.Center.X + ((right * focal) / depth), bounds.Center.Y - ((vertical * focal) / depth)),
            depth);
        return true;
    }

    private bool TryUnprojectFlyToZ(Rect bounds, Point screen, double z, out Point world)
    {
        double focal = Math.Min(bounds.Width, bounds.Height) * 0.9;
        if (focal <= 0)
        {
            world = default;
            return false;
        }

        double screenRight = screen.X - bounds.Center.X;
        double screenVertical = -(screen.Y - bounds.Center.Y);
        double sinPitch = Math.Sin(_flyCamera.Pitch);
        double cosPitch = Math.Cos(_flyCamera.Pitch);
        double verticalPerDepth = screenVertical / focal;
        double denominator = sinPitch + (verticalPerDepth * cosPitch);
        if (Math.Abs(denominator) < 0.0001)
        {
            world = default;
            return false;
        }

        double depth = (z - _flyCamera.Z) / denominator;
        if (depth <= 48)
        {
            world = default;
            return false;
        }

        double right = (screenRight * depth) / focal;
        double vertical = (screenVertical * depth) / focal;
        double forwardHorizontal = (depth * cosPitch) - (vertical * sinPitch);
        double cosYaw = Math.Cos(_flyCamera.Yaw);
        double sinYaw = Math.Sin(_flyCamera.Yaw);
        double viewX =
            _flyCamera.X + (cosYaw * forwardHorizontal) - (sinYaw * right);
        double viewY =
            _flyCamera.Y + (sinYaw * forwardHorizontal) + (cosYaw * right);
        world = new Point(FromFlyViewX(viewX), FromFlyViewY(viewY));
        return true;
    }

    private double ToFlyViewX(double x)
    {
        return x * EditorUiDefaults.MapXAxisScreenSign(_flipMapY);
    }

    private double ToFlyViewY(double y)
    {
        return y * EditorUiDefaults.MapYAxisScreenSign(_flipMapY);
    }

    private double FromFlyViewX(double x)
    {
        return x * EditorUiDefaults.MapXAxisScreenSign(_flipMapY);
    }

    private double FromFlyViewY(double y)
    {
        return y * EditorUiDefaults.MapYAxisScreenSign(_flipMapY);
    }

    private SceneTransform CreateGeometryTransform(Rect bounds, GeometryCandidate geometry)
    {
        SceneFitFocus focus = GetSceneFitFocus(geometry);
        double scaleX = bounds.Width / Math.Max(1, focus.Bounds.Width);
        double scaleY = bounds.Height / Math.Max(1, focus.Bounds.Height);
        double scale = Math.Min(scaleX, scaleY) * 0.82 * _zoom;
        double centerX = (focus.Bounds.Left + focus.Bounds.Right) * 0.5;
        double centerY = (focus.Bounds.Top + focus.Bounds.Bottom) * 0.5;
        return new SceneTransform(bounds.Center + _pan, centerX, centerY, scale, 0.035 * _zoom, _flipMapY);
    }

    private SceneTransform CreateMobyTransform(Rect bounds, IReadOnlyList<Moby> mobys)
    {
        GeometryCandidate? geometry = Geometry;
        if (geometry != null && geometry.Polygons.Count > 0)
            return CreateGeometryTransform(bounds, geometry);

        double minX = mobys.Min(moby => moby.Position.X);
        double maxX = mobys.Max(moby => moby.Position.X);
        double minY = mobys.Min(moby => moby.Position.Y);
        double maxY = mobys.Max(moby => moby.Position.Y);
        double scale = Math.Min(bounds.Width / Math.Max(1, maxX - minX), bounds.Height / Math.Max(1, maxY - minY)) * 0.78 * _zoom;
        return new SceneTransform(bounds.Center + _pan, (minX + maxX) * 0.5, (minY + maxY) * 0.5, scale, 0.02 * _zoom, _flipMapY);
    }

    private bool TryGetBrushWorldPoint(Point screenPoint, int terrainIndex, out Vector2f worldPoint)
    {
        worldPoint = default;
        GeometryCandidate? geometry = Geometry;
        if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count)
            return false;

        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        TerrainPolygon terrain = geometry.Polygons[terrainIndex];
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (!TryGetFlyTerrainWorldPoint(bounds, screenPoint, terrain, out worldPoint))
                return false;

            return true;
        }

        if (!TryGetMapTerrainWorldPoint(bounds, screenPoint, terrain, out Vector3f mapHit))
            return false;

        worldPoint = new Vector2f(mapHit.X, mapHit.Y);
        return true;
    }

    public bool TryGetObjectPlacementPosition(Point screenPoint, Vector3f fallback, out Vector3f position)
    {
        return TryGetObjectPlacementPosition(screenPoint, fallback, out position, out _);
    }

    public bool TryGetObjectPlacementPosition(Point screenPoint, Vector3f fallback, out Vector3f position, out int terrainIndex)
    {
        position = fallback;
        terrainIndex = -1;
        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        GeometryCandidate? geometry = Geometry;
        if (geometry != null && geometry.Polygons.Count > 0)
        {
            if (_viewMode == ViewportViewMode.Fly3D &&
                TryFindFlyTerrainHit(bounds, screenPoint, out terrainIndex, out Vector3f flyHit))
            {
                position = flyHit;
                return true;
            }

            ScreenTerrainFace? terrainHit = _viewMode == ViewportViewMode.Fly3D
                ? null
                : FindScreenTerrain(screenPoint, fallback.Z);
            if (terrainHit != null && terrainHit.Index >= 0 && terrainHit.Index < geometry.Polygons.Count)
            {
                TerrainPolygon terrain = geometry.Polygons[terrainHit.Index];
                terrainIndex = terrainHit.Index;
                if (TryGetMapTerrainWorldPoint(bounds, screenPoint, terrain, out Vector3f mapHit))
                {
                    position = mapHit;
                    return true;
                }
            }

            if (_viewMode == ViewportViewMode.Fly3D)
            {
                if (TryUnprojectFlyToZ(bounds, screenPoint, fallback.Z, out Point flyWorld))
                {
                    position = new Vector3f((float)flyWorld.X, (float)flyWorld.Y, fallback.Z);
                    return true;
                }
            }
            else
            {
                SceneTransform transform = CreateGeometryTransform(bounds, geometry);
                Point world = transform.Unproject(screenPoint, fallback.Z);
                position = new Vector3f((float)world.X, (float)world.Y, fallback.Z);
                return true;
            }

            return false;
        }

        IReadOnlyList<Moby> mobys = Mobys.Where(moby => !moby.IsRemoved).ToList();
        if (mobys.Count == 0)
            return false;

        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (!TryUnprojectFlyToZ(bounds, screenPoint, fallback.Z, out Point flyWorld))
                return false;

            position = new Vector3f((float)flyWorld.X, (float)flyWorld.Y, fallback.Z);
            return true;
        }

        SceneTransform mobyTransform = CreateMobyTransform(bounds, mobys);
        Point mobyWorld = mobyTransform.Unproject(screenPoint, fallback.Z);
        position = new Vector3f((float)mobyWorld.X, (float)mobyWorld.Y, fallback.Z);
        return true;
    }

    private bool TryGetTerrainDragDelta(double z, Point previousScreen, Point currentScreen, out float dx, out float dy)
    {
        dx = 0;
        dy = 0;
        GeometryCandidate? geometry = Geometry;
        Rect bounds = new(Bounds.Size);
        if (geometry == null || bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        Point previousWorld;
        Point currentWorld;
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (!TryUnprojectFlyToZ(bounds, previousScreen, z, out previousWorld) ||
                !TryUnprojectFlyToZ(bounds, currentScreen, z, out currentWorld))
                return false;
        }
        else
        {
            SceneTransform transform = CreateGeometryTransform(bounds, geometry);
            previousWorld = transform.Unproject(previousScreen, z);
            currentWorld = transform.Unproject(currentScreen, z);
        }

        dx = (float)(currentWorld.X - previousWorld.X);
        dy = (float)(currentWorld.Y - previousWorld.Y);
        return true;
    }

    private static bool IsTerrainVerticalDrag(KeyModifiers modifiers)
    {
        return (modifiers & KeyModifiers.Shift) != 0;
    }

    private static bool IsTerrainAddCopyDrag(KeyModifiers modifiers)
    {
        return (modifiers & KeyModifiers.Alt) != 0;
    }

    private static float TerrainVerticalDragDelta(Point previousScreen, Point currentScreen)
    {
        const float unitsPerPixel = 8;
        return (float)((previousScreen.Y - currentScreen.Y) * unitsPerPixel);
    }

    private bool TryFindFlyTerrainHit(Rect bounds, Point screenPoint, out int terrainIndex, out Vector3f hit)
    {
        terrainIndex = -1;
        hit = default;
        GeometryCandidate? geometry = Geometry;
        if (geometry == null || !TryGetFlyRay(bounds, screenPoint, out Vector3f origin, out Vector3f direction))
            return false;

        float closestDistance = float.MaxValue;
        bool useLocalGameViewTerrain = UsesFlyGameViewHighDetailVisibility(geometry);
        for (int index = 0; index < geometry.Polygons.Count; index++)
        {
            TerrainPolygon polygon = geometry.Polygons[index];
            // The local gameplay camera intentionally retains real nearby
            // source terrain. Whole-level Fly uses the same Playable/Complete
            // layer choice as Map, so invisible support sheets cannot catch an
            // object-placement or terrain-edit ray.
            if (!useLocalGameViewTerrain && !ShouldPresentTerrain(geometry, polygon))
                continue;
            if (!TerrainRaycaster.TryIntersect(
                    polygon,
                    origin,
                    direction,
                    out Vector3f candidate,
                    out float candidateDistance) ||
                candidateDistance >= closestDistance)
            {
                continue;
            }

            terrainIndex = index;
            hit = candidate;
            closestDistance = candidateDistance;
        }

        return terrainIndex >= 0;
    }

    private bool TryGetFlyRay(Rect bounds, Point screenPoint, out Vector3f origin, out Vector3f direction)
    {
        origin = default;
        direction = default;
        double focal = Math.Min(bounds.Width, bounds.Height) * 0.9;
        if (focal <= 0)
            return false;

        double right = (screenPoint.X - bounds.Center.X) / focal;
        double vertical = -(screenPoint.Y - bounds.Center.Y) / focal;
        double sinPitch = Math.Sin(_flyCamera.Pitch);
        double cosPitch = Math.Cos(_flyCamera.Pitch);
        double forwardHorizontal = cosPitch - (vertical * sinPitch);
        double dz = sinPitch + (vertical * cosPitch);
        double cosYaw = Math.Cos(_flyCamera.Yaw);
        double sinYaw = Math.Sin(_flyCamera.Yaw);
        double viewDx = (cosYaw * forwardHorizontal) - (sinYaw * right);
        double viewDy = (sinYaw * forwardHorizontal) + (cosYaw * right);
        double worldDx = FromFlyViewX(viewDx);
        double worldDy = FromFlyViewY(viewDy);
        double length = Math.Sqrt((worldDx * worldDx) + (worldDy * worldDy) + (dz * dz));
        if (length <= 0.0001)
            return false;

        origin = new Vector3f(
            (float)FromFlyViewX(_flyCamera.X),
            (float)FromFlyViewY(_flyCamera.Y),
            (float)_flyCamera.Z);
        direction = new Vector3f(
            (float)(worldDx / length),
            (float)(worldDy / length),
            (float)(dz / length));
        return true;
    }

    private bool TryGetMapTerrainWorldPoint(Rect bounds, Point screenPoint, TerrainPolygon terrain, out Vector3f worldPoint)
    {
        worldPoint = default;
        GeometryCandidate? geometry = Geometry;
        int count = Math.Min(terrain.Points.Count, terrain.ZValues.Length);
        if (geometry == null || count < 3)
            return false;

        SceneTransform transform = CreateGeometryTransform(bounds, geometry);
        for (int i = 1; i < count - 1; i++)
        {
            Point p0 = transform.Project(terrain.Points[0].X, terrain.Points[0].Y, terrain.ZValues[0]);
            Point p1 = transform.Project(terrain.Points[i].X, terrain.Points[i].Y, terrain.ZValues[i]);
            Point p2 = transform.Project(terrain.Points[i + 1].X, terrain.Points[i + 1].Y, terrain.ZValues[i + 1]);
            if (!TryGetBarycentric(screenPoint, p0, p1, p2, out double w0, out double w1, out double w2))
                continue;

            worldPoint = new Vector3f(
                (float)((terrain.Points[0].X * w0) + (terrain.Points[i].X * w1) + (terrain.Points[i + 1].X * w2)),
                (float)((terrain.Points[0].Y * w0) + (terrain.Points[i].Y * w1) + (terrain.Points[i + 1].Y * w2)),
                (float)((terrain.ZValues[0] * w0) + (terrain.ZValues[i] * w1) + (terrain.ZValues[i + 1] * w2)));
            return true;
        }

        return false;
    }

    private bool TryGetFlyTerrainWorldPoint(Rect bounds, Point screenPoint, TerrainPolygon terrain, out Vector2f worldPoint)
    {
        worldPoint = default;
        if (!TryGetFlyRay(bounds, screenPoint, out Vector3f origin, out Vector3f direction) ||
            !TerrainRaycaster.TryIntersect(terrain, origin, direction, out Vector3f hit, out _))
        {
            return false;
        }

        worldPoint = new Vector2f(hit.X, hit.Y);
        return true;
    }

    private bool TryBuildTerrainBrushPath(Vector2f brushCenter, out IReadOnlyList<TerrainBrushPathSample> brushSamples)
    {
        if (_lastTerrainBrushWorldPoint == null)
        {
            _lastTerrainBrushWorldPoint = brushCenter;
            brushSamples = [new TerrainBrushPathSample(brushCenter, 1.0)];
            return true;
        }

        Vector2f previous = _lastTerrainBrushWorldPoint.Value;
        IReadOnlyList<TerrainBrushPathSample> samples = TerrainBrushPathSampler.Build(previous, brushCenter, TerrainBrushStepDistance());
        if (samples.Count == 0)
        {
            brushSamples = Array.Empty<TerrainBrushPathSample>();
            return false;
        }

        _lastTerrainBrushWorldPoint = samples[^1].Center;
        brushSamples = samples;
        return samples.Count > 0;
    }

    private bool TryFlushTerrainBrushResidual()
    {
        if (Geometry == null ||
            _lastTerrainBrushWorldPoint == null ||
            _terrainBrushPreviewWorldPoint == null ||
            _terrainBrushPreviewTerrainIndex < 0 ||
            _terrainBrushPreviewTerrainIndex >= Geometry.Polygons.Count)
        {
            return false;
        }

        TerrainBrushPathSample? sample = TerrainBrushPathSampler.BuildFinalResidual(
            _lastTerrainBrushWorldPoint.Value,
            _terrainBrushPreviewWorldPoint.Value,
            TerrainBrushStepDistance());
        if (sample == null)
            return false;

        TerrainPolygon terrain = Geometry.Polygons[_terrainBrushPreviewTerrainIndex];
        _lastTerrainBrushWorldPoint = sample.Value.Center;
        TerrainBrushRequested?.Invoke(this, new ViewportTerrainBrushRequestedEventArgs(
            _terrainBrushPreviewTerrainIndex,
            terrain,
            sample.Value.Center,
            _terrainBrushAction,
            false,
            sample.Value.StrengthScale));
        return true;
    }

    private double TerrainBrushStepDistance()
    {
        return Math.Clamp(_terrainBrushRadius * 0.045, 10, 56);
    }

    private double TerrainBrushCorePreviewRadius(double outerRadius)
    {
        double feather = Math.Clamp(_terrainBrushFeather / 100, 0, 1);
        return Math.Max(3, outerRadius * (1 - (feather * 0.6)));
    }

    private static double TerrainBrushPreviewFalloff(double normalizedDistance, double featherPercent)
    {
        double t = Math.Clamp(1.0 - normalizedDistance, 0.0, 1.0);
        double smooth = t * t * (3.0 - (2.0 * t));
        double feather = Math.Clamp(featherPercent / 100.0, 0.0, 1.0);
        double exponent = 0.35 + (feather * 1.3);
        return Math.Pow(smooth, exponent);
    }

    private void UpdateTerrainBrushPreview(Point screenPoint)
    {
        if (_terrainBrushAction == TerrainBrushAction.Off || Geometry == null)
            return;

        ScreenTerrainFace? terrain = FindScreenTerrain(screenPoint);
        if (terrain == null || !TryGetBrushWorldPoint(screenPoint, terrain.Index, out Vector2f brushCenter))
        {
            ClearTerrainBrushPreview();
            return;
        }

        _terrainBrushPreviewWorldPoint = brushCenter;
        SetTerrainPresentationPin(ref _terrainBrushPreviewTerrainIndex, terrain.Index);
        InvalidateVisual();
    }

    private void UpdateTerrainHover(Point screenPoint)
    {
        if (Geometry == null || !ShouldPrioritizeTerrainInteraction() || _isPanning || _isDraggingMoby || _isDraggingMobyFacing || _isDraggingTerrainFace || _isDraggingTerrainPoint)
        {
            SetHoverTerrain(-1);
            return;
        }

        ScreenTerrainFace? terrain = FindScreenTerrain(screenPoint);
        SetHoverTerrain(terrain?.Index ?? -1);
    }

    private void SetHoverTerrain(int terrainIndex)
    {
        if (_hoverTerrainIndex == terrainIndex)
            return;

        SetTerrainPresentationPin(ref _hoverTerrainIndex, terrainIndex);
        InvalidateVisual();
    }

    private void UpdateViewportCursor(Point screenPoint)
    {
        if (_isDraggingMobyFacing)
        {
            SetViewportCursor(StandardCursorType.Cross);
            return;
        }

        if (_isPanning || _isDraggingMoby || _isDraggingTerrainFace || _isDraggingTerrainPoint)
        {
            SetViewportCursor(StandardCursorType.SizeAll);
            return;
        }

        if (_objectPlacementMode)
        {
            SetViewportCursor(StandardCursorType.Cross);
            return;
        }

        if (_terrainTexturePaintMode)
        {
            SetViewportCursor(
                !_terrainTexturePaintBusy && FindScreenTerrain(screenPoint) != null
                    ? StandardCursorType.Cross
                    : null);
            return;
        }

        if (!ShouldPrioritizeTerrainInteraction() && FindScreenFacingGuide(screenPoint) != null)
        {
            SetViewportCursor(StandardCursorType.Hand);
            return;
        }

        if (_terrainBrushAction != TerrainBrushAction.Off)
        {
            SetViewportCursor(FindScreenTerrain(screenPoint) == null ? null : StandardCursorType.Cross);
            return;
        }

        if (ShouldPrioritizeTerrainInteraction())
        {
            if (FindScreenTerrainPoint(screenPoint) != null)
            {
                SetViewportCursor(StandardCursorType.Cross);
                return;
            }

            if (FindScreenTerrain(screenPoint) != null)
            {
                SetViewportCursor(StandardCursorType.Hand);
                return;
            }
        }

        if (FindScreenMoby(screenPoint) != null)
        {
            SetViewportCursor(StandardCursorType.Hand);
            return;
        }

        SetViewportCursor(null);
    }

    private void SetViewportCursor(StandardCursorType? cursorType)
    {
        if (_currentCursorType == cursorType)
            return;

        _currentCursorType = cursorType;
        Cursor = cursorType.HasValue ? new Cursor(cursorType.Value) : null;
    }

    private void ClearTerrainBrushPreview()
    {
        if (_terrainBrushPreviewWorldPoint == null && _terrainBrushPreviewTerrainIndex < 0)
            return;

        _terrainBrushPreviewWorldPoint = null;
        SetTerrainPresentationPin(ref _terrainBrushPreviewTerrainIndex, -1);
        InvalidateVisual();
    }

    private static float TerrainZAt(TerrainPolygon terrain, Vector2f point)
    {
        return terrain.TryGetZ(point.X, point.Y, out float z) ? z : terrain.AvgZ;
    }

    private void CenterOnMoby(Moby moby)
    {
        Rect bounds = new(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (_viewMode == ViewportViewMode.Fly3D)
        {
            double targetX = ToFlyViewX(moby.Position.X);
            double targetY = ToFlyViewY(moby.Position.Y);
            double targetZ = moby.Position.Z + 96;
            const double horizontalDistance = 950;
            const double verticalDistance = 420;
            double yaw = _flyCamera.Yaw;
            SetFlyCameraLookingAt(
                targetX - (Math.Cos(yaw) * horizontalDistance),
                targetY - (Math.Sin(yaw) * horizontalDistance),
                targetZ + verticalDistance,
                targetX,
                targetY,
                targetZ);
            _flyCameraIsOverview = false;
            _flyGameViewCameraLocal = _flipMapY;
            ResumeNativeTerrainLodAfterFlyNavigation();
            return;
        }

        SceneTransform transform = CreateMobyTransform(bounds, Mobys);
        Point point = transform.Project(moby.Position.X, moby.Position.Y, moby.Position.Z);
        _pan += bounds.Center - point;
    }

    private static void DrawPolygon(DrawingContext context, IReadOnlyList<Point> points, IBrush? fill, Pen? pen)
    {
        if (points.Count < 3)
            return;

        StreamGeometry geometry = new();
        using (StreamGeometryContext stream = geometry.Open())
        {
            stream.BeginFigure(points[0], true);
            for (int i = 1; i < points.Count; i++)
                stream.LineTo(points[i]);
            stream.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawTerrainRegionUnderlay(
        DrawingContext context,
        IReadOnlyList<ProjectedTerrainFace> faces,
        IReadOnlyDictionary<string, Color> toneCache,
        bool flyView)
    {
        if (faces.Count == 0)
            return;

        foreach (ProjectedTerrainFace face in faces)
        {
            if (!toneCache.TryGetValue(TerrainToneKey(face.Polygon), out Color tone))
                tone = Color.FromArgb(face.Polygon.SurfaceColor.A, face.Polygon.SurfaceColor.R, face.Polygon.SurfaceColor.G, face.Polygon.SurfaceColor.B);

            if (TryGetTerrainFamilyColor(face.Polygon.Surface, out Color family))
                tone = BlendColor(tone, family, TerrainFamilyBlendAmount(face.Polygon.Surface) * 0.82);

            byte fillAlpha = flyView ? (byte)62 : (byte)92;
            byte edgeAlpha = flyView ? (byte)96 : (byte)138;
            double edgeWidth = flyView ? 7.5 : 12.5;
            Color soft = BlendColor(tone, Colors.White, flyView ? 0.02 : 0.04);
            DrawPolygon(
                context,
                face.Points,
                new SolidColorBrush(Color.FromArgb(fillAlpha, soft.R, soft.G, soft.B)),
                new Pen(new SolidColorBrush(Color.FromArgb(edgeAlpha, soft.R, soft.G, soft.B)), edgeWidth));
        }
    }

    private static void DrawTerrainMaterialAreaWash(
        DrawingContext context,
        IReadOnlyList<ProjectedTerrainFace> faces,
        IReadOnlyDictionary<string, Color> toneCache,
        bool flyView)
    {
        if (faces.Count == 0)
            return;

        double bucketSize = flyView ? 210 : 340;
        IEnumerable<IGrouping<string, ProjectedTerrainFace>> groups = faces
            .Where(face => face.Points.Count >= 3)
            .GroupBy(face =>
            {
                Point center = Centroid(face.Points);
                int bucketX = (int)Math.Floor(center.X / bucketSize);
                int bucketY = (int)Math.Floor(center.Y / bucketSize);
                return $"{TerrainToneKey(face.Polygon)}:{bucketX}:{bucketY}";
            }, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ProjectedTerrainFace> group in groups)
        {
            List<ProjectedTerrainFace> clusteredFaces = group.ToList();
            if (clusteredFaces.Count < 2)
                continue;

            List<Point> points = clusteredFaces.SelectMany(face => face.Points).ToList();
            Rect bounds = BoundsFor(points);
            if (bounds.Width < 26 || bounds.Height < 26)
                continue;

            TerrainPolygon sample = clusteredFaces[0].Polygon;
            if (!toneCache.TryGetValue(TerrainToneKey(sample), out Color tone))
                tone = Color.FromArgb(sample.SurfaceColor.A, sample.SurfaceColor.R, sample.SurfaceColor.G, sample.SurfaceColor.B);

            if (TryGetTerrainFamilyColor(sample.Surface, out Color familyColor))
                tone = BlendColor(tone, familyColor, TerrainFamilyBlendAmount(sample.Surface) * 0.95);

            Point weightedCenter = WeightedScreenCenter(clusteredFaces);
            double radiusX = Math.Clamp(bounds.Width * (flyView ? 0.42 : 0.5), 18, flyView ? 96 : 170);
            double radiusY = Math.Clamp(bounds.Height * (flyView ? 0.42 : 0.5), 18, flyView ? 96 : 170);
            double densityBoost = Math.Clamp(clusteredFaces.Count / 12.0, 0, 1);
            byte alpha = (byte)Math.Round((flyView ? 16 : 22) + (densityBoost * (flyView ? 8 : 12)));
            Color light = BlendColor(tone, Colors.White, flyView ? 0.045 : 0.065);
            Color shade = BlendColor(tone, Colors.Black, flyView ? 0.02 : 0.03);

            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(alpha, light.R, light.G, light.B)),
                null,
                weightedCenter,
                radiusX,
                radiusY);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb((byte)Math.Max(8, alpha - 8), shade.R, shade.G, shade.B)),
                null,
                weightedCenter + new Vector(radiusX * 0.22, radiusY * 0.18),
                radiusX * 0.68,
                radiusY * 0.52);
            DrawTerrainAreaMaterialStrokes(context, clusteredFaces, sample.Surface, tone, weightedCenter, radiusX, radiusY, flyView);
        }
    }

    private static void DrawTerrainAreaMaterialStrokes(
        DrawingContext context,
        IReadOnlyList<ProjectedTerrainFace> faces,
        string surface,
        Color tone,
        Point center,
        double radiusX,
        double radiusY,
        bool flyView)
    {
        if (faces.Count < 2)
            return;

        string normalized = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        int seed = HashCode.Combine(normalized, faces.Count, (int)Math.Round(center.X), (int)Math.Round(center.Y));
        int strokeCount = flyView ? 1 : 2;
        double span = Math.Clamp(Math.Min(radiusX, radiusY) * 1.35, 18, flyView ? 105 : 170);
        Color light = BlendColor(tone, Colors.White, SurfaceWashLightAmount(normalized) * 1.15);
        Color shade = BlendColor(tone, Colors.Black, SurfaceWashShadeAmount(normalized) * 1.1);

        for (int i = 0; i < strokeCount; i++)
        {
            double angle = (DeterministicUnit(seed, i, 53) * Math.PI) + (i * 0.42);
            Vector tangent = new(Math.Cos(angle), Math.Sin(angle));
            Vector normal = new(-tangent.Y, tangent.X);
            Point strokeCenter = center + normal * ((DeterministicUnit(seed, i, 59) - 0.5) * Math.Min(radiusX, radiusY) * 0.72);
            byte lightAlpha = (byte)(flyView ? 10 : 13);
            byte shadeAlpha = (byte)(flyView ? 7 : 9);
            double lightWidth = Math.Max(1.6, span * (flyView ? 0.045 : 0.04));
            double shadeWidth = Math.Max(1.1, span * (flyView ? 0.032 : 0.028));

            DrawSoftStroke(context, strokeCenter - tangent * span * 0.52, strokeCenter + tangent * span * 0.52, light, lightAlpha, lightWidth);
            DrawSoftStroke(context, strokeCenter - tangent * span * 0.24 + normal * span * 0.11, strokeCenter + tangent * span * 0.28 + normal * span * 0.11, shade, shadeAlpha, shadeWidth);
        }
    }

    private static void DrawTerrainMaterialGlaze(
        DrawingContext context,
        IReadOnlyList<ProjectedTerrainFace> faces,
        IReadOnlyDictionary<string, Color> toneCache,
        bool flyView)
    {
        if (faces.Count == 0)
            return;

        double bucketSize = flyView ? 260 : 430;
        IEnumerable<IGrouping<string, ProjectedTerrainFace>> groups = faces
            .Where(face => face.Points.Count >= 3)
            .GroupBy(face =>
            {
                Point center = Centroid(face.Points);
                int bucketX = (int)Math.Floor(center.X / bucketSize);
                int bucketY = (int)Math.Floor(center.Y / bucketSize);
                return $"{TerrainToneKey(face.Polygon)}:{bucketX}:{bucketY}";
            }, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ProjectedTerrainFace> group in groups)
        {
            List<ProjectedTerrainFace> clusteredFaces = group.ToList();
            if (clusteredFaces.Count < 2)
                continue;

            List<Point> points = clusteredFaces.SelectMany(face => face.Points).ToList();
            Rect bounds = BoundsFor(points);
            if (bounds.Width < 32 || bounds.Height < 32)
                continue;

            TerrainPolygon sample = clusteredFaces[0].Polygon;
            if (!toneCache.TryGetValue(TerrainToneKey(sample), out Color tone))
                tone = Color.FromArgb(sample.SurfaceColor.A, sample.SurfaceColor.R, sample.SurfaceColor.G, sample.SurfaceColor.B);

            if (TryGetTerrainFamilyColor(sample.Surface, out Color familyColor))
                tone = BlendColor(tone, familyColor, TerrainFamilyBlendAmount(sample.Surface) * 0.9);

            Point center = WeightedScreenCenter(clusteredFaces);
            double radiusX = Math.Clamp(bounds.Width * (flyView ? 0.36 : 0.43), 18, flyView ? 92 : 155);
            double radiusY = Math.Clamp(bounds.Height * (flyView ? 0.36 : 0.43), 18, flyView ? 92 : 155);
            double densityBoost = Math.Clamp(clusteredFaces.Count / 14.0, 0, 1);
            byte alpha = (byte)Math.Round((flyView ? 10 : 14) + (densityBoost * (flyView ? 5 : 7)));
            Color glaze = BlendColor(tone, Colors.White, flyView ? 0.025 : 0.035);

            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(alpha, glaze.R, glaze.G, glaze.B)),
                null,
                center,
                radiusX,
                radiusY);
        }
    }

    private void DrawTerrainPriorityOutlines(DrawingContext context, IReadOnlyList<ProjectedTerrainFace> faces, bool flyView)
    {
        foreach (ProjectedTerrainFace face in faces)
        {
            if (face.IsAddCopySourcePreview)
                continue;

            bool selected = face.Index == _selectedTerrainIndex;
            bool edited = face.Polygon.IsTerrainEdited;
            if (!selected && !edited)
                continue;

            TerrainPatchSafetyKind safety = TerrainPatchSafetyFor(face.Polygon);
            Pen pen = selected
                ? new Pen(new SolidColorBrush(Color.FromRgb(255, 236, 127)), flyView ? 2.2 : 2.4)
                : new Pen(new SolidColorBrush(TerrainPatchSafetyPenColor(safety)), 1.9);
            DrawPolygon(context, face.Points, null, pen);
        }
    }

    private void DrawSelectedTerrainPointHandles(DrawingContext context, IReadOnlyList<ProjectedTerrainFace> faces, bool flyView)
    {
        if (_selectedTerrainIndex < 0)
            return;

        ProjectedTerrainFace? selected = faces.FirstOrDefault(face => !face.IsAddCopySourcePreview && face.Index == _selectedTerrainIndex);
        if (selected == null || selected.Points.Count == 0)
            return;

        int pointCount = Math.Min(Math.Min(selected.Points.Count, selected.Polygon.Points.Count), selected.Polygon.ZValues.Length);
        if (pointCount <= 0)
            return;

        double radius = flyView ? 4.2 : Math.Clamp(3.8 + (_zoom * 1.3), 4.2, 7.2);
        IBrush idleFill = new SolidColorBrush(Color.FromArgb(218, 244, 248, 252));
        Pen idlePen = new(new SolidColorBrush(Color.FromArgb(210, 37, 51, 64)), flyView ? 1.1 : 1.25);
        IBrush activeFill = new SolidColorBrush(Color.FromRgb(255, 236, 90));
        Pen activePen = new(new SolidColorBrush(Color.FromRgb(24, 32, 42)), flyView ? 1.8 : 2.1);

        for (int i = 0; i < pointCount; i++)
        {
            Point point = selected.Points[i];
            bool active = i == _selectedTerrainPointIndex;
            double handleRadius = active ? radius * 1.35 : radius;
            context.DrawEllipse(active ? activeFill : idleFill, active ? activePen : idlePen, point, handleRadius, handleRadius);
            if (!active)
                continue;

            Pen cross = new(new SolidColorBrush(Color.FromRgb(24, 32, 42)), flyView ? 1.1 : 1.25);
            context.DrawLine(cross, new Point(point.X - handleRadius * 1.55, point.Y), new Point(point.X + handleRadius * 1.55, point.Y));
            context.DrawLine(cross, new Point(point.X, point.Y - handleRadius * 1.55), new Point(point.X, point.Y + handleRadius * 1.55));
        }
    }

    private static bool ShouldDrawAddCopySourcePreview(TerrainPolygon polygon)
    {
        return polygon.IsTerrainAddClone && (polygon.HasPositionEdit || polygon.HasHeightEdit);
    }

    private void DrawMapForegroundTerrainEdges(DrawingContext context, IReadOnlyList<ProjectedTerrainFace> faces)
    {
        if (_zoom < 0.72)
            return;

        Pen pen = CreateMapForegroundTerrainEdgePen();
        foreach (ProjectedTerrainFace face in faces)
        {
            if (face.IsAddCopySourcePreview)
                continue;
            if (face.Index == _selectedTerrainIndex || face.Polygon.IsTerrainEdited || face.Polygon.IsTerrainRemoved)
                continue;
            if (RequiresGuardedNativeTerrainBlendFallback(face.Polygon))
                continue;

            DrawPolygon(context, face.Points, null, pen);
        }
    }

    private void DrawFlyForegroundTerrainEdges(
        DrawingContext context,
        IReadOnlyList<ProjectedTerrainFace> faces,
        IReadOnlySet<int>? interactiveFullMaterialFaces,
        bool useFlyGameViewVisibility,
        IReadOnlySet<int> forcedGameViewSectors)
    {
        Pen pen = CreateFlyForegroundTerrainEdgePen();
        foreach (ProjectedTerrainFace face in faces)
        {
            if (face.IsAddCopySourcePreview)
                continue;
            if (interactiveFullMaterialFaces != null &&
                !interactiveFullMaterialFaces.Contains(face.Index))
            {
                continue;
            }
            if (face.Index == _selectedTerrainIndex || face.Polygon.IsTerrainEdited || face.Polygon.IsTerrainRemoved)
                continue;
            if (useFlyGameViewVisibility &&
                !forcedGameViewSectors.Contains(face.Polygon.SectorIndex) &&
                face.Depth >= FlyGameViewFogNearDistance)
            {
                continue;
            }
            if (RequiresGuardedNativeTerrainBlendFallback(face.Polygon))
                continue;

            DrawPolygon(context, face.Points, null, pen);
        }
    }

    private Pen CreateMapForegroundTerrainEdgePen()
    {
        double detail = Math.Clamp((_zoom - 0.72) / 2.2, 0, 1);
        bool terrainFocus = IsTerrainVisualFocusActive();
        byte alpha = (byte)Math.Round(terrainFocus
            ? Lerp(34, 86, detail)
            : Lerp(18, 46, detail));
        double width = terrainFocus
            ? Lerp(0.52, 0.92, detail)
            : Lerp(0.36, 0.62, detail);
        return new Pen(new SolidColorBrush(Color.FromArgb(alpha, 12, 18, 24)), width);
    }

    private Pen CreateFlyForegroundTerrainEdgePen()
    {
        bool terrainFocus = IsTerrainVisualFocusActive();
        byte alpha = terrainFocus ? (byte)78 : (byte)48;
        double width = terrainFocus ? 0.72 : 0.5;
        return new Pen(new SolidColorBrush(Color.FromArgb(alpha, 10, 16, 22)), width);
    }

    private void DrawTerrainHoverTarget(DrawingContext context, IReadOnlyList<ProjectedTerrainFace> faces, bool flyView)
    {
        int terrainIndex = _terrainBrushPreviewTerrainIndex >= 0 ? _terrainBrushPreviewTerrainIndex : _hoverTerrainIndex;
        if (terrainIndex < 0 || terrainIndex == _selectedTerrainIndex)
            return;

        ProjectedTerrainFace? face = faces.FirstOrDefault(item => !item.IsAddCopySourcePreview && item.Index == terrainIndex);
        if (face == null)
            return;

        Color accent = _terrainBrushAction == TerrainBrushAction.Off
            ? Color.FromRgb(130, 218, 255)
            : TerrainBrushActionColor(_terrainBrushAction);
        bool underlay = IsMapUnderlaySurface(face.Polygon.Surface);
        byte fillAlpha = flyView ? (byte)0 : (byte)34;
        byte lineAlpha = flyView
            ? underlay ? (byte)92 : (byte)122
            : (byte)190;
        IBrush? fill = fillAlpha == 0
            ? null
            : new SolidColorBrush(Color.FromArgb(fillAlpha, accent.R, accent.G, accent.B));
        Pen pen = new(new SolidColorBrush(Color.FromArgb(lineAlpha, accent.R, accent.G, accent.B)), flyView ? 1.05 : 1.55);
        DrawPolygon(context, face.Points, fill, pen);
    }

    private void DrawTerrainSelectionChip(DrawingContext context, IReadOnlyList<ProjectedTerrainFace> faces, Rect bounds, bool flyView)
    {
        if (!IsTerrainVisualFocusActive())
            return;

        int terrainIndex = _hoverTerrainIndex >= 0
            ? _hoverTerrainIndex
            : _terrainBrushPreviewTerrainIndex >= 0
                ? _terrainBrushPreviewTerrainIndex
                : _selectedTerrainIndex;
        if (terrainIndex < 0)
            return;

        ProjectedTerrainFace? face = faces.FirstOrDefault(item => !item.IsAddCopySourcePreview && item.Index == terrainIndex);
        if (face == null || face.Points.Count < 3)
            return;

        string role = IsMapUnderlaySurface(face.Polygon.Surface) ? "underlay" : "foreground";
        string label = $"{TerrainMaterialClassifier.FormatSurface(face.Polygon.Surface)} {role}  F{terrainIndex}";
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            flyView ? 10.5 : 11,
            new SolidColorBrush(Color.FromArgb(235, 244, 248, 252)))
        {
            MaxTextWidth = flyView ? 160 : 180
        };

        Point center = Centroid(face.Points);
        Rect labelBounds = bounds.Deflate(new Thickness(14, 54, 14, 42));
        Point origin = new(center.X + 12, center.Y - text.Height - 18);
        origin = new Point(
            Math.Clamp(origin.X, labelBounds.Left, Math.Max(labelBounds.Left, labelBounds.Right - text.Width - 20)),
            Math.Clamp(origin.Y, labelBounds.Top, Math.Max(labelBounds.Top, labelBounds.Bottom - text.Height - 12)));

        Color accent = TerrainSurfaceLabelColor(face.Polygon);
        Color fill = BlendColor(accent, Color.FromRgb(18, 24, 31), 0.78);
        Rect background = new(origin.X - 8, origin.Y - 4, text.Width + 16, text.Height + 8);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(216, fill.R, fill.G, fill.B)), background, 5);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(158, accent.R, accent.G, accent.B)), 1),
            background,
            5,
            5);
        context.DrawText(text, origin);
    }

    private List<ProjectedPoint> ProjectFlyTerrainPoints(Rect bounds, IReadOnlyList<Vector2f> points, IReadOnlyList<float> zValues)
    {
        List<ProjectedPoint> projected = new();
        int count = Math.Min(points.Count, zValues.Count);
        for (int i = 0; i < count; i++)
        {
            if (TryProjectFly(bounds, points[i].X, points[i].Y, zValues[i], out ProjectedPoint point))
                projected.Add(point);
        }

        return projected;
    }

    private static float OriginalAverageZ(TerrainPolygon polygon)
    {
        if (polygon.OriginalZValues.Length == 0)
            return polygon.AvgZ;

        float sum = 0;
        foreach (float z in polygon.OriginalZValues)
            sum += z;

        return sum / polygon.OriginalZValues.Length;
    }

    private static void DrawAddCopySourcePreview(DrawingContext context, IReadOnlyList<Point> points, Color color, bool flyView)
    {
        Color fill = BlendColor(color, Color.FromRgb(180, 190, 202), 0.48);
        fill = Color.FromArgb(flyView ? (byte)78 : (byte)86, fill.R, fill.G, fill.B);
        Pen pen = new(new SolidColorBrush(Color.FromArgb(flyView ? (byte)150 : (byte)168, 230, 237, 244)), flyView ? 1.15 : 1.25);
        DrawPolygon(context, points, new SolidColorBrush(fill), pen);
    }

    private NativeTerrainFaceRenderKind DrawTerrainFace(
        DrawingContext context,
        IReadOnlyList<Point> points,
        TerrainPolygon polygon,
        Color fillColor,
        Pen? detailPen,
        bool flyView,
        double emphasis,
        IReadOnlyList<double>? cameraDepths,
        bool simplifyInteractiveMaterial,
        bool suppressGameViewFog = false)
    {
        byte alphaCap = flyView
            ? RequiresGuardedNativeTerrainBlendFallback(polygon)
                ? (byte)224
                : (byte)255
            : RequiresGuardedNativeTerrainBlendFallback(polygon)
                ? (byte)170
                : (byte)255;
        byte alpha = fillColor.A < alphaCap ? fillColor.A : alphaCap;
        Color faceColor = Color.FromArgb(alpha, fillColor.R, fillColor.G, fillColor.B);
        NativeTerrainFaceRenderKind nativeRenderKind = TryDrawNativeTerrainFace(
            context,
            points,
            polygon,
            faceColor,
            flyView,
            cameraDepths,
            simplifyInteractiveMaterial);
        bool renderedNative = nativeRenderKind is
            NativeTerrainFaceRenderKind.NativeTexture or
            NativeTerrainFaceRenderKind.NativeUntexturedGouraud;
        if (!renderedNative)
        {
            Pen? borderPen = detailPen ?? CreateTerrainFaceDetailPen(flyView);
            DrawPolygon(context, points, new SolidColorBrush(faceColor), borderPen);
        }
        else
        {
            if (emphasis > 0)
            {
                byte overlayAlpha = (byte)Math.Clamp(Math.Round(255 * emphasis), 0, 255);
                Color overlay = Color.FromArgb(overlayAlpha, faceColor.R, faceColor.G, faceColor.B);
                DrawPolygon(context, points, new SolidColorBrush(overlay), null);
            }
            if (emphasis > 0 || polygon.IsTerrainRemoved)
            {
                Pen? borderPen = detailPen ?? CreateTerrainFaceDetailPen(flyView);
                DrawPolygon(context, points, null, borderPen);
            }
        }
        DrawFlyGameViewFog(context, points, cameraDepths, suppressGameViewFog);
        if (polygon.IsTerrainRemoved)
        {
            Rect bounds = BoundsFor(points);
            Pen removePen = new(new SolidColorBrush(Color.FromArgb(180, 255, 116, 126)), flyView ? 1.2 : 1.4);
            context.DrawLine(removePen, bounds.TopLeft, bounds.BottomRight);
            context.DrawLine(removePen, bounds.TopRight, bounds.BottomLeft);
        }
        return nativeRenderKind;
    }

    private void DrawFlyGameViewFog(
        DrawingContext context,
        IReadOnlyList<Point> points,
        IReadOnlyList<double>? cameraDepths,
        bool suppressed)
    {
        if (suppressed ||
            !_flyGameViewCameraLocal ||
            !_flipMapY ||
            _flyCameraIsOverview ||
            _nativeTerrainLodPreviewMode != NativeTerrainLodPreviewMode.EditorOverviewHighDetail ||
            points.Count < 3 ||
            cameraDepths == null ||
            cameraDepths.Count == 0)
        {
            return;
        }

        double averageDepth = cameraDepths.Average();
        double linear = Math.Clamp(
            (averageDepth - FlyGameViewFogNearDistance) /
            (FlyGameViewFogFarDistance - FlyGameViewFogNearDistance),
            0,
            1);
        if (linear <= 0)
            return;

        double smooth = linear * linear * (3 - (2 * linear));
        byte alpha = (byte)Math.Clamp(Math.Round(255 * smooth), 0, 255);
        double averageY = points.Average(point => point.Y);
        double normalizedY = Bounds.Height <= 0 ? 0.52 : averageY / Bounds.Height;
        Color fog = FlyBackgroundColor(normalizedY);
        DrawPolygon(
            context,
            points,
            new SolidColorBrush(Color.FromArgb(alpha, fog.R, fog.G, fog.B)),
            null);
    }

    private NativeTerrainFaceRenderKind TryDrawNativeTerrainFace(
        DrawingContext context,
        IReadOnlyList<Point> points,
        TerrainPolygon polygon,
        Color fallbackColor,
        bool flyView,
        IReadOnlyList<double>? cameraDepths,
        bool reduceMaterialDetail)
    {
        // Opaque-untextured native primitives still carry exact four-corner
        // Gouraud color, even when the editor classifies their region as a
        // fluid underlay. Render that color with fallbackColor's alpha cap;
        // only genuinely textured fluids stay on the guarded translucent
        // editor fallback until their PS1 blend path is reproduced here.
        if (RequiresGuardedNativeTerrainBlendFallback(polygon))
            return NativeTerrainFaceRenderKind.GuardedUnderlayFallback;
        if (points.Count < 3 || polygon.VertexIndexes.Count < 4)
            return NativeTerrainFaceRenderKind.UnresolvedFallback;

        if (!TryBuildRawFaceSlotPoints(polygon, points, out Point[] rawPoints))
            return NativeTerrainFaceRenderKind.UnresolvedFallback;

        double[]? rawDepths = null;
        if (flyView &&
            (cameraDepths == null || !TryBuildRawFaceSlotDepths(polygon, cameraDepths, out rawDepths)))
        {
            return NativeTerrainFaceRenderKind.UnresolvedFallback;
        }

        int uniqueVertexCount = polygon.VertexIndexes.Take(4).Distinct().Count();
        int[] winding = polygon.FaceFlip
            ? uniqueVertexCount == 3 ? [1, 2, 3] : [0, 1, 2, 3]
            : uniqueVertexCount == 3 ? [3, 2, 1] : [3, 2, 1, 0];
        if (winding.Select(slot => rawPoints[slot]).Distinct().Count() < 3)
            return NativeTerrainFaceRenderKind.ProjectionDegenerate;

        double area = Math.Abs(PolygonArea(winding.Select(slot => rawPoints[slot]).ToArray()));
        // A camera gesture must not change the face's tessellation: changing
        // between one and two/three Gouraud subdivisions exposed different
        // underlay samples along affine texture edges and looked like texture
        // flicker.  Explicit broad-overview underlays may still request the
        // reduced lane, but ordinary terrain keeps one stable topology.
        int subdivisions = reduceMaterialDetail
            ? 1
            : area >= 7200 ? 3 : 2;
        if (polygon.IsNativeUntexturedSentinel)
        {
            Color[] untexturedColors = Enumerable.Range(0, 4)
                .Select(slot => flyView
                    ? NativeTerrainCornerColor(
                        polygon,
                        slot,
                        rawDepths![slot],
                        fallbackColor,
                        forTextureModulation: false)
                    : NativeTerrainMapCornerColor(
                        polygon,
                        slot,
                        fallbackColor,
                        forTextureModulation: false))
                .ToArray();
            DrawNativeTerrainGouraudFan(
                context,
                rawPoints,
                winding,
                untexturedColors,
                subdivisions);
            return NativeTerrainFaceRenderKind.NativeUntexturedGouraud;
        }

        Bitmap? texture;
        if (!flyView || _nativeTerrainLodPreviewMode == NativeTerrainLodPreviewMode.EditorOverviewHighDetail)
        {
            // The whole-level Fit camera is an editor-only inspection pose.
            // Draw the normal HP composite regardless of retail distance so
            // its texture detail remains useful instead of falling back to a
            // flat material color.
            texture = GetTerrainTextureImage(
                polygon.TextureId,
                NativeTerrainTexturePreviewTier.Normal);
        }
        else
        {
            NativeTerrainLqPreviewSelection nativeSelection = NativeTerrainFarLod.SelectHighDetailTexturePath(
                rawDepths!,
                polygon.LqFadeBypass,
                polygon.HqOverlayBypass);
            // Beast Makers and Misty Bog contain no retail LP payload. Their
            // HP geometry remains the terrain path and must keep its far LQ
            // base rather than disappearing or becoming a solid fallback at
            // the ordinary HP-to-LP cutoff.
            bool hasLowPolyReplacement = Geometry?.LowDetailPolygons.Count > 0;
            if (!nativeSelection.HighPolyVisible && hasLowPolyReplacement)
                return NativeTerrainFaceRenderKind.UnresolvedFallback;

            NativeTerrainTexturePreviewTier? hqTier = nativeSelection.HqOverlayEligible
                ? NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(rawDepths!.Min())
                : null;
            texture = GetNativeTerrainLqFrame(
                polygon.TextureId,
                nativeSelection.DescriptorIndex,
                nativeSelection.PaletteRow,
                hqTier);
        }
        if (texture == null)
            return NativeTerrainFaceRenderKind.UnresolvedFallback;

        Point[] uv =
        [
            new Point(0, texture.PixelSize.Height),
            new Point(texture.PixelSize.Width, texture.PixelSize.Height),
            new Point(texture.PixelSize.Width, 0),
            new Point(0, 0)
        ];

        Color[] tint = Enumerable.Range(0, 4)
            .Select(slot => flyView
                ? NativeTerrainCornerColor(polygon, slot, rawDepths![slot], fallbackColor)
                : NativeTerrainMapCornerColor(polygon, slot, fallbackColor))
            .ToArray();
        DrawNativeTerrainGouraudFan(context, rawPoints, winding, tint, subdivisions);

        RenderOptions textureOptions = new()
        {
            BitmapInterpolationMode = BitmapInterpolationMode.None,
            BitmapBlendingMode = BitmapBlendingMode.Multiply
        };
        using (context.PushRenderOptions(textureOptions))
        {
            for (int i = 1; i < winding.Length - 1; i++)
            {
                int a = winding[0];
                int b = winding[i];
                int c = winding[i + 1];
                DrawAffineTextureTriangle(
                    context,
                    texture,
                    uv[a], uv[b], uv[c],
                    rawPoints[a], rawPoints[b], rawPoints[c]);
            }
        }

        return NativeTerrainFaceRenderKind.NativeTexture;
    }

    private static void DrawNativeTerrainGouraudFan(
        DrawingContext context,
        IReadOnlyList<Point> rawPoints,
        IReadOnlyList<int> winding,
        IReadOnlyList<Color> colors,
        int subdivisions)
    {
        for (int i = 1; i < winding.Count - 1; i++)
        {
            int a = winding[0];
            int b = winding[i];
            int c = winding[i + 1];
            DrawGouraudTriangle(
                context,
                rawPoints[a], colors[a],
                rawPoints[b], colors[b],
                rawPoints[c], colors[c],
                subdivisions);
        }
    }

    private Color NativeTerrainMapCornerColor(
        TerrainPolygon polygon,
        int slot,
        Color fallback,
        bool forTextureModulation = true)
    {
        if (!TryGetNativeTerrainOverviewSourceColor(polygon, slot, out Spyro.Editor.Core.Primitives.ColorRgba source))
        {
            source = Spyro.Editor.Core.Primitives.ColorRgba.FromArgb(
                fallback.A,
                fallback.R,
                fallback.G,
                fallback.B);
        }

        Spyro.Editor.Core.Primitives.ColorRgba graded = PreviewEnvironmentColor(source);
        // Avalonia's Multiply blend treats 255 as neutral; PS1 texture
        // modulation treats 128 as neutral. Doubling converts the native shade
        // to the equivalent multiplier for this editor-only top-down preview.
        return Color.FromArgb(
            (byte)Math.Min(graded.A, fallback.A),
            forTextureModulation ? (byte)Math.Min(255, graded.R * 2) : graded.R,
            forTextureModulation ? (byte)Math.Min(255, graded.G * 2) : graded.G,
            forTextureModulation ? (byte)Math.Min(255, graded.B * 2) : graded.B);
    }

    internal static bool TryGetNativeTerrainOverviewSourceColor(
        TerrainPolygon polygon,
        int slot,
        out Spyro.Editor.Core.Primitives.ColorRgba source)
    {
        // The legacy NearColor/NearColors field is physical HP table 2. This
        // is the source lane used by the previously verified all-level Map
        // renderer. A camera-less overview has no single retail SZ value, so
        // use that stable source endpoint while Game Camera performs DPCS.
        if (polygon.TextureVisualEdit is TerrainTextureVisualEdit visual &&
            slot >= 0 &&
            slot < visual.Corners.Count)
        {
            source = visual.Corners[slot].NearColor;
            return true;
        }

        if (slot >= 0 && slot < polygon.NearColors.Count)
        {
            source = polygon.NearColors[slot];
            return true;
        }

        source = default;
        return false;
    }

    private Bitmap? GetTerrainTextureImage(int textureId, NativeTerrainTexturePreviewTier tier)
    {
        if (textureId < 0)
            return null;
        var key = (textureId, tier);
        if (_terrainTextureImageCache.TryGetValue(key, out Bitmap? cached))
            return cached;

        string? path = ResolveTerrainTextureImagePath(textureId, tier);
        if (path == null)
        {
            _terrainTextureImageCache[key] = null;
            return null;
        }

        try
        {
            Bitmap bitmap = new(path);
            _terrainTextureImageCache[key] = bitmap;
            if (tier == NativeTerrainTexturePreviewTier.Normal &&
                !_normalTerrainTextureAverageColorCache.ContainsKey(textureId))
            {
                _normalTerrainTextureAverageColorCache[textureId] = CalculateTextureAverageColor(bitmap);
            }
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _terrainTextureImageCache[key] = null;
            return null;
        }
    }

    private Bitmap? GetNativeTerrainLqFrame(
        int textureId,
        int descriptorIndex,
        int paletteRow,
        NativeTerrainTexturePreviewTier? hqTier)
    {
        if (textureId < 0 ||
            descriptorIndex is < 0 or >= NativeTerrainLqTextureCacheCodec.DescriptorCount ||
            paletteRow is < 0 or >= NativeTerrainLqTextureCacheCodec.PaletteRowCount)
        {
            return null;
        }

        int hqTierKey = hqTier switch
        {
            NativeTerrainTexturePreviewTier.Normal => 0,
            NativeTerrainTexturePreviewTier.Close => 1,
            _ => -1
        };
        NativeTerrainLqFrameCacheKey key = new(textureId, descriptorIndex, paletteRow, hqTierKey);
        if (_nativeTerrainLqFrameCache.TryGetValue(key, out Bitmap? cached))
            return cached;

        if (!_nativeTerrainLqTextureRecords.TryGetValue(textureId, out NativeTerrainLqTextureRecordPayload? record))
        {
            _nativeTerrainLqFrameCache[key] = null;
            return null;
        }

        try
        {
            NativeTerrainLqTextureDescriptorPayload descriptor = record.GetDescriptor(descriptorIndex);
            Rgba32[] lowDetail = descriptor.MaterializeRgba(paletteRow);
            Bitmap? highDetail = hqTier.HasValue
                ? GetTerrainTextureImageExact(textureId, hqTier.Value)
                : null;
            if (hqTier.HasValue && highDetail == null)
            {
                _nativeTerrainLqFrameCache[key] = null;
                return null;
            }
            int width = highDetail?.PixelSize.Width ?? NativeTerrainLqTextureCacheCodec.TextureSide;
            int height = highDetail?.PixelSize.Height ?? NativeTerrainLqTextureCacheCodec.TextureSide;
            byte[]? highDetailPixels = null;
            if (highDetail != null &&
                (!TryReadBitmapRgba(highDetail, out highDetailPixels) ||
                 highDetailPixels.Length != checked(width * height * 4)))
            {
                _nativeTerrainLqFrameCache[key] = null;
                return null;
            }

            byte[] composed = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                int sourceY = y * NativeTerrainLqTextureCacheCodec.TextureSide / height;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = x * NativeTerrainLqTextureCacheCodec.TextureSide / width;
                    Rgba32 lq = lowDetail[(sourceY * NativeTerrainLqTextureCacheCodec.TextureSide) + sourceX];
                    int offset = ((y * width) + x) * 4;
                    if (highDetailPixels == null)
                    {
                        composed[offset] = lq.R;
                        composed[offset + 1] = lq.G;
                        composed[offset + 2] = lq.B;
                        composed[offset + 3] = lq.A;
                        continue;
                    }

                    CompositeRgbaOver(
                        highDetailPixels[offset],
                        highDetailPixels[offset + 1],
                        highDetailPixels[offset + 2],
                        highDetailPixels[offset + 3],
                        lq.R,
                        lq.G,
                        lq.B,
                        lq.A,
                        composed.AsSpan(offset, 4));
                }
            }

            Bitmap bitmap = CreateBitmapFromTightRgba(width, height, composed);
            _nativeTerrainLqFrameCache[key] = bitmap;
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or OverflowException)
        {
            _nativeTerrainLqFrameCache[key] = null;
            return null;
        }
    }

    private static bool TryReadBitmapRgba(Bitmap source, out byte[] pixels)
    {
        pixels = [];
        WriteableBitmap? copy = null;
        try
        {
            copy = new WriteableBitmap(
                source.PixelSize,
                source.Dpi,
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul);
            using ILockedFramebuffer framebuffer = copy.Lock();
            source.CopyPixels(framebuffer);
            if (framebuffer.Format != PixelFormat.Rgba8888)
                return false;

            int tightRowBytes = checked(source.PixelSize.Width * 4);
            byte[] stored = new byte[checked(framebuffer.RowBytes * source.PixelSize.Height)];
            Marshal.Copy(framebuffer.Address, stored, 0, stored.Length);
            pixels = new byte[checked(tightRowBytes * source.PixelSize.Height)];
            for (int y = 0; y < source.PixelSize.Height; y++)
            {
                stored.AsSpan(y * framebuffer.RowBytes, tightRowBytes)
                    .CopyTo(pixels.AsSpan(y * tightRowBytes, tightRowBytes));
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException)
        {
            pixels = [];
            return false;
        }
        finally
        {
            copy?.Dispose();
        }
    }

    private static Bitmap CreateBitmapFromTightRgba(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            throw new ArgumentException("RGBA pixels do not match the requested bitmap dimensions.", nameof(pixels));

        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);
        try
        {
            using ILockedFramebuffer framebuffer = bitmap.Lock();
            if (framebuffer.Format != PixelFormat.Rgba8888)
                throw new NotSupportedException($"Expected RGBA8888 terrain pixels, received {framebuffer.Format}.");

            int tightRowBytes = checked(width * 4);
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(
                    pixels,
                    y * tightRowBytes,
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                    tightRowBytes);
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void CompositeRgbaOver(
        byte foregroundRed,
        byte foregroundGreen,
        byte foregroundBlue,
        byte foregroundAlpha,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue,
        byte backgroundAlpha,
        Span<byte> destination)
    {
        int inverseAlpha = 255 - foregroundAlpha;
        int outputAlpha = foregroundAlpha + ((backgroundAlpha * inverseAlpha + 127) / 255);
        if (outputAlpha <= 0)
        {
            destination.Clear();
            return;
        }

        destination[0] = CompositeChannel(foregroundRed, foregroundAlpha, backgroundRed, backgroundAlpha, inverseAlpha, outputAlpha);
        destination[1] = CompositeChannel(foregroundGreen, foregroundAlpha, backgroundGreen, backgroundAlpha, inverseAlpha, outputAlpha);
        destination[2] = CompositeChannel(foregroundBlue, foregroundAlpha, backgroundBlue, backgroundAlpha, inverseAlpha, outputAlpha);
        destination[3] = (byte)outputAlpha;
    }

    private static byte CompositeChannel(
        int foreground,
        int foregroundAlpha,
        int background,
        int backgroundAlpha,
        int inverseAlpha,
        int outputAlpha)
    {
        int backgroundPremultiplied = (background * backgroundAlpha * inverseAlpha + 127) / 255;
        int outputPremultiplied = (foreground * foregroundAlpha) + backgroundPremultiplied;
        return (byte)Math.Clamp((outputPremultiplied + (outputAlpha / 2)) / outputAlpha, 0, 255);
    }

    private string? ResolveTerrainTextureImagePath(int textureId, NativeTerrainTexturePreviewTier tier)
    {
        if (textureId < 0)
            return null;

        Dictionary<int, string> preferredFiles = tier == NativeTerrainTexturePreviewTier.Close
            ? _closeTerrainTextureImageFiles
            : _normalTerrainTextureImageFiles;
        Dictionary<int, string> fallbackFiles = tier == NativeTerrainTexturePreviewTier.Close
            ? _normalTerrainTextureImageFiles
            : _closeTerrainTextureImageFiles;
        if ((!preferredFiles.TryGetValue(textureId, out string? path) || !File.Exists(path)) &&
            (!fallbackFiles.TryGetValue(textureId, out path) || !File.Exists(path)))
            return null;

        return path;
    }

    private Bitmap? GetTerrainTextureImageExact(int textureId, NativeTerrainTexturePreviewTier tier)
    {
        Dictionary<int, string> exactFiles = tier == NativeTerrainTexturePreviewTier.Close
            ? _closeTerrainTextureImageFiles
            : _normalTerrainTextureImageFiles;
        if (!exactFiles.TryGetValue(textureId, out string? path) || !File.Exists(path))
            return null;

        return GetTerrainTextureImage(textureId, tier);
    }

    private static bool TryBuildRawFaceSlotPoints(
        TerrainPolygon polygon,
        IReadOnlyList<Point> projectedPoints,
        out Point[] rawPoints)
    {
        rawPoints = new Point[4];
        if (!TryBuildRawFaceSlotIndexes(polygon, projectedPoints.Count, out int[] projectedIndexes))
            return false;

        for (int slot = 0; slot < rawPoints.Length; slot++)
            rawPoints[slot] = projectedPoints[projectedIndexes[slot]];
        return true;
    }

    private static bool TryBuildRawFaceSlotDepths(
        TerrainPolygon polygon,
        IReadOnlyList<double> projectedDepths,
        out double[] rawDepths)
    {
        rawDepths = new double[4];
        if (!TryBuildRawFaceSlotIndexes(polygon, projectedDepths.Count, out int[] projectedIndexes))
            return false;

        for (int slot = 0; slot < rawDepths.Length; slot++)
            rawDepths[slot] = projectedDepths[projectedIndexes[slot]];
        return true;
    }

    private static bool TryBuildRawFaceSlotIndexes(
        TerrainPolygon polygon,
        int projectedPointCount,
        out int[] projectedIndexes)
    {
        projectedIndexes = new int[4];
        if (polygon.VertexIndexes.Count < 4)
            return false;

        if (polygon.CornerPointIndexes.Count == 4)
        {
            for (int slot = 0; slot < projectedIndexes.Length; slot++)
            {
                int projectedIndex = polygon.CornerPointIndexes[slot];
                if (projectedIndex < 0 || projectedIndex >= projectedPointCount)
                    return false;
                projectedIndexes[slot] = projectedIndex;
            }

            return true;
        }

        Dictionary<int, int> projectedIndexByVertex = new();
        int nextProjectedIndex = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            int vertexIndex = polygon.VertexIndexes[slot];
            if (!projectedIndexByVertex.TryGetValue(vertexIndex, out int projectedIndex))
            {
                projectedIndex = nextProjectedIndex++;
                projectedIndexByVertex[vertexIndex] = projectedIndex;
            }

            if (projectedIndex < 0 || projectedIndex >= projectedPointCount)
                return false;
            projectedIndexes[slot] = projectedIndex;
        }

        return true;
    }

    private Color NativeTerrainCornerColor(
        TerrainPolygon polygon,
        int slot,
        double cameraDepth,
        Color fallback,
        bool forTextureModulation = true)
    {
        Spyro.Editor.Core.Primitives.ColorRgba legacyNearField;
        Spyro.Editor.Core.Primitives.ColorRgba legacyFarField;
        if (polygon.TextureVisualEdit is TerrainTextureVisualEdit visual && slot >= 0 && slot < visual.Corners.Count)
        {
            legacyNearField = visual.Corners[slot].NearColor;
            legacyFarField = visual.Corners[slot].FarColor;
        }
        else if (slot >= 0 && slot < polygon.NearColors.Count && slot < polygon.FarColors.Count)
        {
            legacyNearField = polygon.NearColors[slot];
            legacyFarField = polygon.FarColors[slot];
        }
        else
        {
            legacyNearField = Spyro.Editor.Core.Primitives.ColorRgba.FromArgb(fallback.A, fallback.R, fallback.G, fallback.B);
            legacyFarField = legacyNearField;
        }

        // The v3 overlay preserves the historical exporter names: NearColors is
        // the second physical HP color table and FarColors is the first.  The
        // retail renderer proves the first table is the near DPCS endpoint and
        // the second is the far endpoint.  Keep serialization stable here while
        // consuming those two raw slots in their engine-semantic direction.
        Spyro.Editor.Core.Primitives.ColorRgba gradedLegacyNear = PreviewEnvironmentColor(legacyNearField);
        Spyro.Editor.Core.Primitives.ColorRgba gradedLegacyFar = PreviewEnvironmentColor(legacyFarField);
        Spyro.Editor.Core.Primitives.ColorRgba graded = _flyCameraIsOverview
            ? gradedLegacyNear
            : _useNativeTerrainDepthCue
                ? NativeTerrainDepthCueColor(gradedLegacyNear, gradedLegacyFar, cameraDepth)
                : gradedLegacyNear;
        return Color.FromArgb(
            (byte)Math.Min(graded.A, fallback.A),
            forTextureModulation ? (byte)Math.Min(255, graded.R * 2) : graded.R,
            forTextureModulation ? (byte)Math.Min(255, graded.G * 2) : graded.G,
            forTextureModulation ? (byte)Math.Min(255, graded.B * 2) : graded.B);
    }

    private Color InteractiveFlyTerrainFlatColor(
        TerrainPolygon polygon,
        IReadOnlyList<double>? cameraDepths,
        Color fallbackColor)
    {
        if (!polygon.HasNativeHighPolyMaterialPayload ||
            RequiresGuardedNativeTerrainBlendFallback(polygon) ||
            cameraDepths == null ||
            !TryBuildRawFaceSlotDepths(polygon, cameraDepths, out double[] rawDepths))
        {
            return fallbackColor;
        }

        bool forTextureModulation = !polygon.IsNativeUntexturedSentinel;
        Color[] cornerColors = Enumerable.Range(0, 4)
            .Select(slot => NativeTerrainCornerColor(
                polygon,
                slot,
                rawDepths[slot],
                fallbackColor,
                forTextureModulation))
            .ToArray();
        Color averageCorner = AverageColor(cornerColors);
        if (polygon.IsNativeUntexturedSentinel)
        {
            return Color.FromArgb(
                fallbackColor.A,
                averageCorner.R,
                averageCorner.G,
                averageCorner.B);
        }

        Color? averageTexture = GetNormalTerrainTextureAverageColor(polygon.TextureId);
        if (averageTexture == null)
            return fallbackColor;

        Color modulated = Color.FromArgb(
            fallbackColor.A,
            MultiplyColorChannel(averageTexture.Value.R, averageCorner.R),
            MultiplyColorChannel(averageTexture.Value.G, averageCorner.G),
            MultiplyColorChannel(averageTexture.Value.B, averageCorner.B));
        return BlendColor(modulated, fallbackColor, 0.08);
    }

    private Color? GetNormalTerrainTextureAverageColor(int textureId)
    {
        if (_normalTerrainTextureAverageColorCache.TryGetValue(textureId, out Color? cached))
            return cached;

        Bitmap? texture = GetTerrainTextureImage(textureId, NativeTerrainTexturePreviewTier.Normal);
        if (_normalTerrainTextureAverageColorCache.TryGetValue(textureId, out cached))
            return cached;

        Color? average = texture == null ? null : CalculateTextureAverageColor(texture);
        _normalTerrainTextureAverageColorCache[textureId] = average;
        return average;
    }

    private static Color? CalculateTextureAverageColor(Bitmap texture)
    {
        if (!TryReadBitmapRgba(texture, out byte[] pixels) || pixels.Length < 4)
            return null;

        long red = 0;
        long green = 0;
        long blue = 0;
        long alphaWeight = 0;
        for (int offset = 0; offset <= pixels.Length - 4; offset += 4)
        {
            int alpha = pixels[offset + 3];
            red += pixels[offset] * alpha;
            green += pixels[offset + 1] * alpha;
            blue += pixels[offset + 2] * alpha;
            alphaWeight += alpha;
        }

        if (alphaWeight <= 0)
            return null;

        return Color.FromRgb(
            (byte)Math.Clamp((red + (alphaWeight / 2)) / alphaWeight, 0, 255),
            (byte)Math.Clamp((green + (alphaWeight / 2)) / alphaWeight, 0, 255),
            (byte)Math.Clamp((blue + (alphaWeight / 2)) / alphaWeight, 0, 255));
    }

    private static byte MultiplyColorChannel(byte source, byte modulation) =>
        (byte)(((source * modulation) + 127) / 255);

    internal static Spyro.Editor.Core.Primitives.ColorRgba NativeTerrainDepthCueColor(
        Spyro.Editor.Core.Primitives.ColorRgba legacyNearField,
        Spyro.Editor.Core.Primitives.ColorRgba legacyFarField,
        double editorCameraDepth)
    {
        int ir0 = NativeTerrainDepthCueIr0(editorCameraDepth);
        // Runtime table 1 is exposed by the legacy FarColors field and is the
        // near endpoint. Runtime table 2 is exposed by NearColors and is far.
        Spyro.Editor.Core.Primitives.ColorRgba runtimeNear = legacyFarField;
        Spyro.Editor.Core.Primitives.ColorRgba runtimeFar = legacyNearField;
        return Spyro.Editor.Core.Primitives.ColorRgba.FromArgb(
            DpcsChannel(runtimeFar.A, runtimeNear.A, ir0),
            DpcsChannel(runtimeFar.R, runtimeNear.R, ir0),
            DpcsChannel(runtimeFar.G, runtimeNear.G, ir0),
            DpcsChannel(runtimeFar.B, runtimeNear.B, ir0));
    }

    internal static int NativeTerrainDepthCueIr0(double editorCameraDepth)
    {
        if (!double.IsFinite(editorCameraDepth))
            return 0x1000;

        // RTPS exposes an integer SZ. HP scene/camera coordinates are four
        // times the editor units, so quantize that value before subtracting it
        // from the renderer's 0x2000 cue origin.
        double gteDepth = Math.Floor(editorCameraDepth * 4.0);
        return (int)Math.Clamp(0x2000 - gteDepth, 0, 0x1000);
    }

    private static int DpcsChannel(int far, int near, int ir0)
    {
        // GTE DPCS uses a signed MAC and arithmetic >> 12, so retaining the
        // integer shift also preserves the retail truncation for descending
        // color ramps.
        return Math.Clamp(((far << 12) + ((near - far) * ir0)) >> 12, 0, 255);
    }

    private static void DrawGouraudTriangle(
        DrawingContext context,
        Point a,
        Color colorA,
        Point b,
        Color colorB,
        Point c,
        Color colorC,
        int subdivisions)
    {
        int steps = Math.Clamp(subdivisions, 1, 4);
        for (int i = 0; i < steps; i++)
        {
            for (int j = 0; j < steps - i; j++)
            {
                GouraudSample p00 = SampleGouraudTriangle(a, colorA, b, colorB, c, colorC, i / (double)steps, j / (double)steps);
                GouraudSample p10 = SampleGouraudTriangle(a, colorA, b, colorB, c, colorC, (i + 1) / (double)steps, j / (double)steps);
                GouraudSample p01 = SampleGouraudTriangle(a, colorA, b, colorB, c, colorC, i / (double)steps, (j + 1) / (double)steps);
                DrawPolygon(
                    context,
                    [p00.Point, p10.Point, p01.Point],
                    new SolidColorBrush(AverageColor(p00.Color, p10.Color, p01.Color)),
                    null);

                if (i + j >= steps - 1)
                    continue;

                GouraudSample p11 = SampleGouraudTriangle(a, colorA, b, colorB, c, colorC, (i + 1) / (double)steps, (j + 1) / (double)steps);
                DrawPolygon(
                    context,
                    [p10.Point, p11.Point, p01.Point],
                    new SolidColorBrush(AverageColor(p10.Color, p11.Color, p01.Color)),
                    null);
            }
        }
    }

    private static GouraudSample SampleGouraudTriangle(
        Point a,
        Color colorA,
        Point b,
        Color colorB,
        Point c,
        Color colorC,
        double u,
        double v)
    {
        double w = 1 - u - v;
        return new GouraudSample(
            new Point(
                (a.X * w) + (b.X * u) + (c.X * v),
                (a.Y * w) + (b.Y * u) + (c.Y * v)),
            Color.FromArgb(
                WeightedByte(colorA.A, colorB.A, colorC.A, w, u, v),
                WeightedByte(colorA.R, colorB.R, colorC.R, w, u, v),
                WeightedByte(colorA.G, colorB.G, colorC.G, w, u, v),
                WeightedByte(colorA.B, colorB.B, colorC.B, w, u, v)));
    }

    private static byte WeightedByte(byte a, byte b, byte c, double wa, double wb, double wc) =>
        (byte)Math.Clamp(Math.Round((a * wa) + (b * wb) + (c * wc)), 0, 255);

    private static Color AverageColor(Color a, Color b, Color c) =>
        Color.FromArgb(
            (byte)((a.A + b.A + c.A) / 3),
            (byte)((a.R + b.R + c.R) / 3),
            (byte)((a.G + b.G + c.G) / 3),
            (byte)((a.B + b.B + c.B) / 3));

    private static Color AverageColor(IReadOnlyList<Color> colors)
    {
        if (colors.Count == 0)
            return Colors.Transparent;

        int alpha = 0;
        int red = 0;
        int green = 0;
        int blue = 0;
        foreach (Color color in colors)
        {
            alpha += color.A;
            red += color.R;
            green += color.G;
            blue += color.B;
        }

        return Color.FromArgb(
            (byte)(alpha / colors.Count),
            (byte)(red / colors.Count),
            (byte)(green / colors.Count),
            (byte)(blue / colors.Count));
    }

    private static void DrawAffineTextureTriangle(
        DrawingContext context,
        Bitmap texture,
        Point sourceA,
        Point sourceB,
        Point sourceC,
        Point destinationA,
        Point destinationB,
        Point destinationC)
    {
        double determinant =
            (sourceA.X * (sourceB.Y - sourceC.Y)) +
            (sourceB.X * (sourceC.Y - sourceA.Y)) +
            (sourceC.X * (sourceA.Y - sourceB.Y));
        if (Math.Abs(determinant) < 0.000001)
            return;

        double m11 = ((destinationA.X * (sourceB.Y - sourceC.Y)) + (destinationB.X * (sourceC.Y - sourceA.Y)) + (destinationC.X * (sourceA.Y - sourceB.Y))) / determinant;
        double m21 = ((destinationA.X * (sourceC.X - sourceB.X)) + (destinationB.X * (sourceA.X - sourceC.X)) + (destinationC.X * (sourceB.X - sourceA.X))) / determinant;
        double m31 = ((destinationA.X * ((sourceB.X * sourceC.Y) - (sourceC.X * sourceB.Y))) + (destinationB.X * ((sourceC.X * sourceA.Y) - (sourceA.X * sourceC.Y))) + (destinationC.X * ((sourceA.X * sourceB.Y) - (sourceB.X * sourceA.Y)))) / determinant;
        double m12 = ((destinationA.Y * (sourceB.Y - sourceC.Y)) + (destinationB.Y * (sourceC.Y - sourceA.Y)) + (destinationC.Y * (sourceA.Y - sourceB.Y))) / determinant;
        double m22 = ((destinationA.Y * (sourceC.X - sourceB.X)) + (destinationB.Y * (sourceA.X - sourceC.X)) + (destinationC.Y * (sourceB.X - sourceA.X))) / determinant;
        double m32 = ((destinationA.Y * ((sourceB.X * sourceC.Y) - (sourceC.X * sourceB.Y))) + (destinationB.Y * ((sourceC.X * sourceA.Y) - (sourceA.X * sourceC.Y))) + (destinationC.Y * ((sourceA.X * sourceB.Y) - (sourceB.X * sourceA.Y)))) / determinant;

        StreamGeometry clip = new();
        using (StreamGeometryContext stream = clip.Open())
        {
            stream.BeginFigure(destinationA, true);
            stream.LineTo(destinationB);
            stream.LineTo(destinationC);
            stream.EndFigure(true);
        }

        using (context.PushGeometryClip(clip))
        using (context.PushTransform(new Matrix(m11, m12, m21, m22, m31, m32)))
        {
            context.DrawImage(texture, new Rect(0, 0, texture.PixelSize.Width, texture.PixelSize.Height));
        }
    }

    private readonly record struct GouraudSample(Point Point, Color Color);

    private double TerrainFaceDetailAmount(bool flyView)
    {
        if (flyView)
            return 0.84;

        return Math.Clamp((_zoom - 0.62) / 2.0, 0.22, 1.0);
    }

    private static void DrawTerrainMaterialBloom(DrawingContext context, IReadOnlyList<Point> points, TerrainPolygon polygon, Color fillColor, bool flyView, double detailAmount)
    {
        if (points.Count < 3)
            return;

        Rect bounds = BoundsFor(points);
        double area = Math.Abs(PolygonArea(points));
        if (area < (flyView ? 720 : 960) || bounds.Width < 16 || bounds.Height < 16)
            return;

        Color bloomColor = fillColor;
        if (TryGetTerrainFamilyColor(polygon.Surface, out Color familyColor))
            bloomColor = BlendColor(fillColor, familyColor, TerrainFamilyBlendAmount(polygon.Surface) * 0.55);

        double extent = Math.Min(bounds.Width, bounds.Height);
        double radiusX = Math.Clamp(bounds.Width * (flyView ? 0.46 : 0.58), 8, flyView ? 44 : 78);
        double radiusY = Math.Clamp(bounds.Height * (flyView ? 0.46 : 0.58), 8, flyView ? 44 : 78);
        if (extent < 28)
        {
            radiusX *= 0.75;
            radiusY *= 0.75;
        }

        Point center = Centroid(points);
        int seed = HashCode.Combine(polygon.TextureId, polygon.SectorIndex, polygon.FaceIndex, polygon.Points.Count);
        Point shiftedCenter = center + new Vector(
            (DeterministicUnit(seed, 0, 23) - 0.5) * radiusX * 0.28,
            (DeterministicUnit(seed, 0, 29) - 0.5) * radiusY * 0.28);

        Color light = BlendColor(bloomColor, Colors.White, flyView ? 0.06 : 0.08);
        Color shade = BlendColor(bloomColor, Colors.Black, flyView ? 0.025 : 0.035);
        byte lightAlpha = (byte)Math.Round((flyView ? 18 : 24) * detailAmount);
        byte shadeAlpha = (byte)Math.Round((flyView ? 12 : 16) * detailAmount);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(lightAlpha, light.R, light.G, light.B)), null, shiftedCenter, radiusX, radiusY);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(shadeAlpha, shade.R, shade.G, shade.B)), null, shiftedCenter + new Vector(radiusX * 0.22, radiusY * 0.28), radiusX * 0.72, radiusY * 0.58);
    }

    private static void DrawTerrainAmbientShade(DrawingContext context, IReadOnlyList<Point> points, Color fillColor, bool flyView, double detailAmount)
    {
        if (points.Count < 3)
            return;

        Rect bounds = BoundsFor(points);
        double area = Math.Abs(PolygonArea(points));
        if (area < (flyView ? 900 : 1400) || bounds.Width < 24 || bounds.Height < 24)
            return;

        Point center = Centroid(points);
        double radiusX = Math.Clamp(bounds.Width * 0.18, 4, flyView ? 18 : 32);
        double radiusY = Math.Clamp(bounds.Height * 0.18, 4, flyView ? 18 : 32);
        Color light = BlendColor(fillColor, Colors.White, flyView ? 0.07 : 0.09);
        Color shade = BlendColor(fillColor, Colors.Black, flyView ? 0.05 : 0.07);
        byte lightAlpha = (byte)Math.Round((flyView ? 16 : 22) * detailAmount);
        byte shadeAlpha = (byte)Math.Round((flyView ? 10 : 15) * detailAmount);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(lightAlpha, light.R, light.G, light.B)), null, center - new Vector(radiusX * 0.3, radiusY * 0.35), radiusX, radiusY);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(shadeAlpha, shade.R, shade.G, shade.B)), null, center + new Vector(radiusX * 0.38, radiusY * 0.42), radiusX * 1.05, radiusY * 0.85);
    }

    private static void DrawTerrainSurfaceWash(DrawingContext context, IReadOnlyList<Point> points, TerrainPolygon polygon, Color fillColor, bool flyView, double detailAmount)
    {
        if (points.Count < 3)
            return;

        Rect bounds = BoundsFor(points);
        double area = Math.Abs(PolygonArea(points));
        if (bounds.Width < 22 || bounds.Height < 22 || area < (flyView ? 1200 : 1600))
            return;

        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface);
        int seed = HashCode.Combine(polygon.TextureId, polygon.SectorIndex, polygon.FaceIndex, polygon.Points.Count, surface);
        Point center = Centroid(points);
        Point edge = points[Math.Abs(seed) % points.Count];
        Point other = points[Math.Abs(seed + 2) % points.Count];
        Vector tangent = other - edge;
        double tangentLength = Math.Max(0.001, Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y)));
        tangent = new Vector(tangent.X / tangentLength, tangent.Y / tangentLength);
        Vector normal = new(-tangent.Y, tangent.X);
        double span = Math.Clamp(Math.Min(bounds.Width, bounds.Height) * 0.42, 8, flyView ? 34 : 56);
        double offset = (DeterministicUnit(seed, 0, 41) - 0.5) * span * 0.35;
        Point washCenter = center + (normal * offset);

        Color light = BlendColor(fillColor, Colors.White, SurfaceWashLightAmount(surface));
        Color dark = BlendColor(fillColor, Colors.Black, SurfaceWashShadeAmount(surface));
        byte lightAlpha = (byte)Math.Round((flyView ? 12 : 15) * detailAmount);
        byte darkAlpha = (byte)Math.Round((flyView ? 8 : 10) * detailAmount);
        double lightWidth = Math.Max(1.1, span * (flyView ? 0.1 : 0.085));
        double darkWidth = Math.Max(0.9, span * (flyView ? 0.075 : 0.065));

        DrawSoftStroke(context, washCenter - tangent * span * 0.58, washCenter + tangent * span * 0.58, light, lightAlpha, lightWidth);
        DrawSoftStroke(context, washCenter - tangent * span * 0.3 + normal * span * 0.18, washCenter + tangent * span * 0.36 + normal * span * 0.18, dark, darkAlpha, darkWidth);
    }

    private static double SurfaceWashLightAmount(string surface)
    {
        return surface switch
        {
            "grass" or "ground" => 0.12,
            "stone" or "brick" or "cliff" => 0.09,
            "water" or "ice" => 0.18,
            "sand" or "wood" => 0.13,
            "lava" or "ooze" => 0.16,
            _ => 0.1
        };
    }

    private static double SurfaceWashShadeAmount(string surface)
    {
        return surface switch
        {
            "grass" or "ground" => 0.08,
            "stone" or "brick" or "cliff" => 0.1,
            "water" or "ice" => 0.05,
            "sand" or "wood" => 0.08,
            "lava" or "ooze" => 0.08,
            _ => 0.07
        };
    }

    private static void DrawTerrainMaterialTexture(DrawingContext context, IReadOnlyList<Point> points, TerrainPolygon polygon, Color fillColor, bool flyView, double detailAmount)
    {
        if (points.Count < 3)
            return;

        Rect bounds = BoundsFor(points);
        double area = Math.Abs(PolygonArea(points));
        if (bounds.Width < 18 || bounds.Height < 18 || area < 360)
            return;

        double opacityScale = (flyView ? 0.72 : Math.Clamp((Math.Sqrt(area) - 42) / 90, 0, 1)) * detailAmount;
        if (!flyView)
            opacityScale *= Math.Clamp((Math.Min(bounds.Width, bounds.Height) - 28) / 52, 0, 1);
        if (opacityScale <= 0.05)
            return;

        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface);
        int marks = (int)Math.Clamp(Math.Round(area / (flyView ? 9800.0 : 12800.0)), 1, flyView ? 1 : 2);
        Point center = Centroid(points);
        int seed = HashCode.Combine(polygon.TextureId, polygon.SectorIndex, polygon.FaceIndex, polygon.Points.Count);

        for (int i = 0; i < marks; i++)
        {
            Point edge = points[Math.Abs(seed + (i * 17)) % points.Count];
            double t = 0.22 + (DeterministicUnit(seed, i, 3) * 0.58);
            double side = (DeterministicUnit(seed, i, 7) - 0.5) * 0.28;
            Point next = points[Math.Abs(seed + (i * 17) + 1) % points.Count];
            Point basePoint = Lerp(center, edge, t);
            Vector tangent = next - edge;
            double tangentLength = Math.Max(0.001, Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y)));
            tangent = new Vector(tangent.X / tangentLength, tangent.Y / tangentLength);
            Point markCenter = basePoint + (tangent * side * Math.Min(bounds.Width, bounds.Height));
            double scale = Math.Clamp(Math.Min(bounds.Width, bounds.Height) * (0.17 + DeterministicUnit(seed, i, 11) * 0.16), 4, flyView ? 16 : 28);

            DrawTerrainMaterialMark(context, surface, fillColor, markCenter, tangent, scale, i, opacityScale);
        }
    }

    private static void DrawTerrainMaterialMark(DrawingContext context, string surface, Color fillColor, Point center, Vector tangent, double scale, int index, double opacityScale)
    {
        Color light = BlendColor(fillColor, Colors.White, 0.16);
        Color dark = BlendColor(fillColor, Colors.Black, 0.14);
        Vector normal = new(-tangent.Y, tangent.X);
        byte A(int alpha) => (byte)Math.Clamp(Math.Round(alpha * opacityScale), 1, 255);

        switch (surface)
        {
            case "grass":
            case "ground":
                DrawSoftStroke(context, center - tangent * scale * 0.48, center + tangent * scale * 0.5, light, A(15), Math.Max(1.2, scale * 0.16));
                DrawSoftStroke(context, center - normal * scale * 0.22, center + normal * scale * 0.22, dark, A(8), Math.Max(1.0, scale * 0.12));
                break;
            case "stone":
            case "brick":
            case "cliff":
            case "metal":
                DrawSoftStroke(context, center - tangent * scale * 0.7, center + tangent * scale * 0.65, dark, A(14), Math.Max(1.2, scale * 0.15));
                DrawSoftStroke(context, center - tangent * scale * 0.38 + normal * scale * 0.17, center + tangent * scale * 0.34 + normal * scale * 0.17, light, A(9), Math.Max(1.0, scale * 0.1));
                break;
            case "water":
            case "ice":
                DrawSoftStroke(context, center - tangent * scale * 0.8, center + tangent * scale * 0.8, BlendColor(fillColor, Colors.White, 0.28), A(22), Math.Max(1.2, scale * 0.13));
                break;
            case "sand":
            case "wood":
                DrawSoftStroke(context, center - tangent * scale * 0.62, center + tangent * scale * 0.56, light, A(13), Math.Max(1.0, scale * 0.11));
                DrawSoftStroke(context, center - tangent * scale * 0.16 + normal * scale * 0.18, center + tangent * scale * 0.2 + normal * scale * 0.18, dark, A(7), Math.Max(0.9, scale * 0.08));
                break;
            case "lava":
            case "ooze":
                DrawSoftStroke(context, center - tangent * scale * 0.55, center + tangent * scale * 0.55, BlendColor(fillColor, Colors.White, 0.22), A(16), Math.Max(1.2, scale * 0.13));
                break;
            default:
                if (index % 2 == 0)
                    DrawSoftStroke(context, center - tangent * scale * 0.42, center + tangent * scale * 0.42, light, A(7), Math.Max(0.9, scale * 0.08));
                break;
        }
    }

    private static void DrawSoftStroke(DrawingContext context, Point start, Point end, Color color, byte alpha, double thickness)
    {
        context.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), thickness),
            start,
            end);
    }

    private static Rect BoundsFor(IReadOnlyList<Point> points)
    {
        double minX = points.Min(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxX = points.Max(point => point.X);
        double maxY = points.Max(point => point.Y);
        return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    private static Point Centroid(IReadOnlyList<Point> points)
    {
        double x = 0;
        double y = 0;
        foreach (Point point in points)
        {
            x += point.X;
            y += point.Y;
        }

        return new Point(x / points.Count, y / points.Count);
    }

    private static Point WeightedScreenCenter(IReadOnlyList<ProjectedTerrainFace> faces)
    {
        double x = 0;
        double y = 0;
        double weightSum = 0;
        foreach (ProjectedTerrainFace face in faces)
        {
            double weight = Math.Max(1, Math.Abs(PolygonArea(face.Points)));
            Point center = Centroid(face.Points);
            x += center.X * weight;
            y += center.Y * weight;
            weightSum += weight;
        }

        return weightSum <= 0 ? Centroid(faces[0].Points) : new Point(x / weightSum, y / weightSum);
    }

    private static double PolygonArea(IReadOnlyList<Point> points)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Point a = points[i];
            Point b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum * 0.5;
    }

    private static Point Lerp(Point from, Point to, double amount)
    {
        return new Point(
            from.X + ((to.X - from.X) * amount),
            from.Y + ((to.Y - from.Y) * amount));
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }

    private static double DeterministicUnit(int seed, int index, int salt)
    {
        unchecked
        {
            uint value = (uint)(seed + (index * 374761393) + (salt * 668265263));
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0xFFFF) / 65535.0;
        }
    }

    private Pen? CreateTerrainFaceDetailPen(bool flyView)
    {
        if (flyView)
            return new Pen(new SolidColorBrush(Color.FromArgb(58, 11, 16, 22)), 0.62);

        if (_zoom >= 2.8)
            return new Pen(new SolidColorBrush(Color.FromArgb(92, 18, 25, 32)), 0.82);
        if (_zoom >= 1.45)
            return new Pen(new SolidColorBrush(Color.FromArgb(68, 18, 25, 32)), 0.68);
        return new Pen(new SolidColorBrush(Color.FromArgb(48, 18, 25, 32)), 0.54);
    }

    private Dictionary<string, Color> BuildTerrainToneCache(GeometryCandidate geometry)
    {
        Dictionary<string, (long R, long G, long B, int Count)> sums = new(StringComparer.OrdinalIgnoreCase);
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (!ShouldPresentTerrain(geometry, polygon))
                continue;

            string key = TerrainToneKey(polygon);
            Spyro.Editor.Core.Primitives.ColorRgba color = PreviewEnvironmentColor(polygon.SurfaceColor);
            if (!sums.TryGetValue(key, out (long R, long G, long B, int Count) sum))
                sum = default;

            sum.R += color.R;
            sum.G += color.G;
            sum.B += color.B;
            sum.Count++;
            sums[key] = sum;

        }

        return sums.ToDictionary(
            item => item.Key,
            item => Color.FromRgb(
                (byte)Math.Clamp(item.Value.R / Math.Max(1, item.Value.Count), 0, 255),
                (byte)Math.Clamp(item.Value.G / Math.Max(1, item.Value.Count), 0, 255),
                (byte)Math.Clamp(item.Value.B / Math.Max(1, item.Value.Count), 0, 255)),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string TerrainToneKey(TerrainPolygon polygon)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface);
        if (!string.Equals(surface, "unknown", StringComparison.OrdinalIgnoreCase))
            return $"surface:{surface}";
        if (polygon.TextureId >= 0)
            return $"texture:{polygon.TextureId}";
        return "unknown";
    }

    private Color TerrainDisplayColor(TerrainPolygon polygon, GeometryCandidate geometry)
    {
        double height = Math.Clamp((polygon.AvgZ - geometry.MinZ) / Math.Max(1, geometry.MaxZ - geometry.MinZ), 0, 1);
        Spyro.Editor.Core.Primitives.ColorRgba preview = TerrainPreviewColor.ApplyHeightTint(
            PreviewEnvironmentColor(polygon.SurfaceColor),
            height);
        return Color.FromArgb(preview.A, preview.R, preview.G, preview.B);
    }

    private Spyro.Editor.Core.Primitives.ColorRgba PreviewEnvironmentColor(Spyro.Editor.Core.Primitives.ColorRgba color) =>
        _environmentGradePreview?.TransformSceneColor(color) ?? color;

    private Color PreviewEnvironmentColor(Color color)
    {
        Spyro.Editor.Core.Primitives.ColorRgba graded = PreviewEnvironmentColor(
            Spyro.Editor.Core.Primitives.ColorRgba.FromArgb(color.A, color.R, color.G, color.B));
        return Color.FromArgb(graded.A, graded.R, graded.G, graded.B);
    }

    private static bool TryGetTerrainFamilyColor(string surface, out Color color)
    {
        switch (TerrainMaterialClassifier.NormalizeSurfaceName(surface))
        {
            case "grass":
                color = Color.FromRgb(83, 158, 72);
                return true;
            case "ground":
                color = Color.FromRgb(105, 124, 78);
                return true;
            case "stone":
                color = Color.FromRgb(118, 119, 109);
                return true;
            case "brick":
                color = Color.FromRgb(142, 111, 82);
                return true;
            case "sand":
                color = Color.FromRgb(188, 150, 76);
                return true;
            case "water":
                color = Color.FromRgb(49, 119, 184);
                return true;
            case "ice":
                color = Color.FromRgb(132, 188, 216);
                return true;
            case "lava":
                color = Color.FromRgb(214, 76, 28);
                return true;
            case "ooze":
                color = Color.FromRgb(74, 142, 49);
                return true;
            case "cliff":
                color = Color.FromRgb(112, 108, 92);
                return true;
            case "wood":
                color = Color.FromRgb(132, 88, 52);
                return true;
            case "metal":
                color = Color.FromRgb(132, 141, 146);
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static double TerrainFamilyBlendAmount(string surface)
    {
        return TerrainMaterialClassifier.NormalizeSurfaceName(surface) switch
        {
            "grass" or "ground" => 0.32,
            "stone" or "brick" or "cliff" => 0.28,
            "water" or "ice" => 0.34,
            "sand" or "wood" => 0.26,
            "lava" or "ooze" => 0.22,
            _ => 0.16
        };
    }

    private static IBrush CreateTerrainFaceBrush(Color color, bool flyView, double detailAmount)
    {
        if (!flyView)
        {
            byte alphaCap = (byte)Math.Round(Lerp(154, 214, detailAmount));
            byte alpha = color.A < alphaCap ? color.A : alphaCap;
            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }

        Color highlight = BlendColor(color, Colors.White, 0.035 * detailAmount);
        Color middle = BlendColor(color, Colors.White, 0.012 * detailAmount);
        Color shadow = BlendColor(color, Colors.Black, 0.018 * detailAmount);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.08, 0.03, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.94, 0.98, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(highlight, 0),
                new(middle, 0.62),
                new(shadow, 1)
            }
        };
    }

    private static string TerrainBrushActionLabel(TerrainBrushAction action)
    {
        return action switch
        {
            TerrainBrushAction.Raise => "raise",
            TerrainBrushAction.Lower => "lower",
            TerrainBrushAction.Blend => "blend",
            TerrainBrushAction.Flatten => "level",
            TerrainBrushAction.Restore => "restore",
            _ => "select"
        };
    }

    private static Color TerrainBrushActionColor(TerrainBrushAction action)
    {
        return action switch
        {
            TerrainBrushAction.Raise => Color.FromRgb(255, 196, 77),
            TerrainBrushAction.Lower => Color.FromRgb(93, 173, 226),
            TerrainBrushAction.Blend => Color.FromRgb(72, 201, 176),
            TerrainBrushAction.Flatten => Color.FromRgb(186, 143, 255),
            TerrainBrushAction.Restore => Color.FromRgb(236, 240, 241),
            _ => Color.FromRgb(255, 236, 127)
        };
    }

    private TerrainPatchSafetyKind TerrainPatchSafetyFor(TerrainPolygon polygon)
    {
        if (!polygon.IsTerrainEdited)
            return TerrainPatchSafetyKind.None;

        return _terrainPatchSafetyClassifier?.Invoke(polygon)
            ?? (polygon.HasStructureEdit
                ? TerrainPatchSafetyKind.Structural
                : polygon.HasHeightEdit || polygon.HasPositionEdit ? TerrainPatchSafetyKind.UnknownHeight : TerrainPatchSafetyKind.TextureOnly);
    }

    private static string TerrainPatchSafetyLabel(TerrainPatchSafetyKind safety)
    {
        return safety switch
        {
            TerrainPatchSafetyKind.Structural => "Structure edit",
            TerrainPatchSafetyKind.PlayableHeight => "Playable edit",
            TerrainPatchSafetyKind.PartialHeight => "Partial edit",
            TerrainPatchSafetyKind.VisualOnlyHeight => "Visual-only edit",
            TerrainPatchSafetyKind.UnknownHeight => "Unknown edit",
            TerrainPatchSafetyKind.TextureOnly => "Texture edit",
            _ => "Edited"
        };
    }

    private static Color TerrainPatchSafetyColor(TerrainPatchSafetyKind safety)
    {
        return safety switch
        {
            TerrainPatchSafetyKind.Structural => Color.FromArgb(210, 236, 86, 93),
            TerrainPatchSafetyKind.PlayableHeight => Color.FromArgb(225, 74, 210, 122),
            TerrainPatchSafetyKind.PartialHeight => Color.FromArgb(225, 255, 196, 77),
            TerrainPatchSafetyKind.VisualOnlyHeight => Color.FromArgb(230, 255, 111, 97),
            TerrainPatchSafetyKind.UnknownHeight => Color.FromArgb(220, 154, 143, 255),
            TerrainPatchSafetyKind.TextureOnly => Color.FromArgb(220, 73, 192, 211),
            _ => Color.FromArgb(220, 255, 198, 82)
        };
    }

    private static Color TerrainPatchSafetyPenColor(TerrainPatchSafetyKind safety)
    {
        return safety switch
        {
            TerrainPatchSafetyKind.Structural => Color.FromArgb(245, 255, 116, 126),
            TerrainPatchSafetyKind.PlayableHeight => Color.FromArgb(240, 100, 240, 150),
            TerrainPatchSafetyKind.PartialHeight => Color.FromArgb(240, 255, 215, 95),
            TerrainPatchSafetyKind.VisualOnlyHeight => Color.FromArgb(245, 255, 128, 114),
            TerrainPatchSafetyKind.UnknownHeight => Color.FromArgb(235, 183, 175, 255),
            TerrainPatchSafetyKind.TextureOnly => Color.FromArgb(235, 100, 218, 235),
            _ => Color.FromArgb(235, 255, 198, 82)
        };
    }

    private static Color AdjustColor(Color color, double factor)
    {
        byte r = (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255);
        return Color.FromArgb(color.A, r, g, b);
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte a = (byte)Math.Clamp(Math.Round(from.A + ((to.A - from.A) * amount)), 0, 255);
        byte r = (byte)Math.Clamp(Math.Round(from.R + ((to.R - from.R) * amount)), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(from.G + ((to.G - from.G) * amount)), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round(from.B + ((to.B - from.B) * amount)), 0, 255);
        return Color.FromArgb(a, r, g, b);
    }

    private static bool ContainsPoint(IReadOnlyList<Point> points, Point point)
    {
        bool inside = false;
        int j = points.Count - 1;
        for (int i = 0; i < points.Count; i++)
        {
            double yi = points[i].Y;
            double yj = points[j].Y;
            if (((yi > point.Y) != (yj > point.Y)) &&
                (point.X < ((points[j].X - points[i].X) * (point.Y - yi) / ((yj - yi) == 0 ? 0.0001 : yj - yi)) + points[i].X))
                inside = !inside;

            j = i;
        }

        return inside;
    }

    private static bool TryGetBarycentric(Point point, Point a, Point b, Point c, out double wa, out double wb, out double wc)
    {
        double v0x = b.X - a.X;
        double v0y = b.Y - a.Y;
        double v1x = c.X - a.X;
        double v1y = c.Y - a.Y;
        double v2x = point.X - a.X;
        double v2y = point.Y - a.Y;
        double denominator = (v0x * v1y) - (v1x * v0y);
        if (Math.Abs(denominator) < 0.000001)
        {
            wa = wb = wc = 0;
            return false;
        }

        wb = ((v2x * v1y) - (v1x * v2y)) / denominator;
        wc = ((v0x * v2y) - (v2x * v0y)) / denominator;
        wa = 1 - wb - wc;
        const double tolerance = -0.001;
        return wa >= tolerance && wb >= tolerance && wc >= tolerance;
    }

    private static bool IntersectsBounds(IReadOnlyList<Point> points, Rect bounds)
    {
        if (points.Count == 0)
            return false;

        double minX = points.Min(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxX = points.Max(point => point.X);
        double maxY = points.Max(point => point.Y);

        return maxX >= bounds.Left &&
            minX <= bounds.Right &&
            maxY >= bounds.Top &&
            minY <= bounds.Bottom;
    }

    private static double DistanceSquared(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double DistanceToSegmentSquared(Point point, Point start, Point end)
    {
        Vector segment = end - start;
        double lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        if (lengthSquared <= 0.0001)
            return DistanceSquared(point, start);

        Vector relative = point - start;
        double t = Math.Clamp(((relative.X * segment.X) + (relative.Y * segment.Y)) / lengthSquared, 0, 1);
        Point closest = start + (segment * t);
        return DistanceSquared(point, closest);
    }

    private static double WorldDistance(Vector2f a, Vector2f b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int NormalizeTerrainPointIndex(TerrainPolygon terrain, int index)
    {
        int count = Math.Min(Math.Min(terrain.Points.Count, terrain.OriginalPoints.Count), Math.Min(terrain.ZValues.Length, terrain.OriginalZValues.Length));
        if (count <= 0)
            return -1;
        if (index < 0)
            return -1;
        if (index >= count)
            return count - 1;
        return index;
    }

    private sealed record ScreenTerrainFace(int Index, IReadOnlyList<Point> Points, double Depth, double AvgZ);
    private sealed record ScreenTerrainPoint(int TerrainIndex, int PointIndex, Point Point);
    private sealed record ScreenTerrainSurfaceLabel(string Text, Point Point, Color Color, double Score);
    private sealed record ScreenMoby(int Index, Point Center, double HitRadius);
    private readonly record struct NativeTerrainLqFrameCacheKey(
        int TextureId,
        int DescriptorIndex,
        int PaletteRow,
        int HqTier);
    private readonly record struct MobyRasterIconCacheKey(
        string IndividualFileName,
        MobyRasterIconAtlasCellId AtlasCellId);
    private sealed record MobyRasterIconImage(
        Bitmap Bitmap,
        Rect? SourceRect,
        MobyRasterIconAtlasCellId? AtlasCellId);
    private sealed record ScreenFacingGuide(int Index, Point Start, Point End);
    private sealed record VisibleMoby(int Index, Moby Moby, Point Point, Point AnchorPoint, double Size, double Depth);
    private readonly record struct FlyMobyVisibilityCounts(int VisibleCount, int SuppressedFarCount);
    private readonly record struct FacingGuideGeometry(Point Start, Point End, Vector Direction);
    private sealed record SceneFitFocus(
        Rect2f Bounds,
        float MinZ,
        float MaxZ,
        IReadOnlyList<Vector3f> Points,
        int SourcePolygonCount,
        int FocusPolygonCount)
    {
        public static SceneFitFocus FromPoints(
            IReadOnlyList<Vector3f> points,
            int sourcePolygonCount,
            int focusPolygonCount)
        {
            float minX = points.Min(point => point.X);
            float minY = points.Min(point => point.Y);
            float minZ = points.Min(point => point.Z);
            float maxX = points.Max(point => point.X);
            float maxY = points.Max(point => point.Y);
            float maxZ = points.Max(point => point.Z);
            return new SceneFitFocus(
                Rect2f.FromBounds(minX, minY, maxX, maxY),
                minZ,
                maxZ,
                points,
                sourcePolygonCount,
                focusPolygonCount);
        }
    }
    private sealed record ProjectedTerrainFace(
        int Index,
        TerrainPolygon Polygon,
        IReadOnlyList<Point> Points,
        double Depth,
        bool IsAddCopySourcePreview = false,
        IReadOnlyList<double>? CameraDepths = null);
    private sealed record ProjectedLowDetailTerrainFace(
        LowDetailTerrainPolygon Polygon,
        IReadOnlyList<Point> RawPoints,
        IReadOnlyList<double> RawDepths,
        double Depth,
        double SortDepth);
    private sealed record ProjectedTerrainSideWall(int PolygonIndex, int EdgeIndex, TerrainPolygon Polygon, IReadOnlyList<Point> Points, double Depth, Color FillColor, Color LineColor);
    private sealed record TerrainSideWallPreviewCandidate(
        int CandidateId,
        int PolygonIndex,
        int EdgeIndex,
        TerrainPolygon Polygon,
        Vector3f OriginalA,
        Vector3f OriginalB,
        Vector3f EditedA,
        Vector3f EditedB,
        string OriginalEdgeKey,
        string EditedEdgeKey);
    private enum NativeTerrainFaceRenderKind
    {
        UnresolvedFallback,
        GuardedUnderlayFallback,
        ProjectionDegenerate,
        InteractiveMaterialSimplified,
        NativeUntexturedGouraud,
        NativeTexture
    }

    private readonly record struct TerrainBrushPreviewPoint(Point Point, double Falloff, TerrainBrushPreviewVertexKind Kind);
    private readonly record struct ProjectedPoint(Point Screen, double Depth);
    private record struct FlyCamera(double X, double Y, double Z, double Yaw, double Pitch);
    private sealed record NativeTerrainOcclusionSelection(
        bool Available,
        bool CollisionTriangleResolved,
        int GroupIndex,
        int TriangleIndex,
        double FloorZ,
        IReadOnlySet<int>? VisibleSectors)
    {
        public static NativeTerrainOcclusionSelection Unavailable { get; } = new(
            false,
            false,
            -1,
            -1,
            double.NegativeInfinity,
            null);
    }

    private sealed record FlyHighDetailGroupSelection(
        int GroupIndex,
        IReadOnlySet<int>? Sectors,
        bool UsedProximityFallback)
    {
        public static FlyHighDetailGroupSelection AllSectors { get; } = new(
            -1,
            null,
            UsedProximityFallback: false);
    }

    private sealed class TerrainSurfaceLabelAccumulator
    {
        private double _weightedX;
        private double _weightedY;

        public TerrainSurfaceLabelAccumulator(string surface, Color color)
        {
            Surface = surface;
            Color = color;
        }

        public string Surface { get; }
        public Color Color { get; }
        public double TotalArea { get; private set; }
        public int FaceCount { get; private set; }

        public Point Center => TotalArea <= 0
            ? default
            : new Point(_weightedX / TotalArea, _weightedY / TotalArea);

        public void Add(ProjectedTerrainFace face, Point center, double area)
        {
            double weight = Math.Max(1, area);
            _weightedX += center.X * weight;
            _weightedY += center.Y * weight;
            TotalArea += weight;
            FaceCount++;
        }
    }

    private readonly record struct SceneTransform(Point Center, double WorldCenterX, double WorldCenterY, double Scale, double HeightScale, bool FlipY)
    {
        public Point Project(double x, double y, double z)
        {
            double xSign = EditorUiDefaults.MapXAxisScreenSign(FlipY);
            double ySign = EditorUiDefaults.MapYAxisScreenSign(FlipY);
            return new Point(
                ((x - WorldCenterX) * Scale * xSign) + Center.X,
                ((y - WorldCenterY) * Scale * ySign) + Center.Y - (z * HeightScale));
        }

        public Point Unproject(Point point, double z)
        {
            double xSign = EditorUiDefaults.MapXAxisScreenSign(FlipY);
            double ySign = EditorUiDefaults.MapYAxisScreenSign(FlipY);
            return new Point(
                ((point.X - Center.X) / (Scale * xSign)) + WorldCenterX,
                ((point.Y - Center.Y + (z * HeightScale)) / (Scale * ySign)) + WorldCenterY);
        }
    }
}

public enum MobyGenericMarkerFallbackKind
{
    None,
    GenericGnorc,
    GenericTree,
    ActorTriangle,
    FlightBullseye
}

internal sealed record ViewportFitSnapshot(
    Rect ViewportBounds,
    Rect FullGeometryBounds,
    Rect FocusGeometryBounds,
    Rect FlyProjectedBounds,
    Rect MapProjectedBounds,
    double Yaw,
    double Pitch,
    int FocusPointCount,
    int FlyProjectedPointCount,
    int SourcePolygonCount,
    int FocusPolygonCount);

internal sealed record TerrainSceneViewSnapshot(
    TerrainSceneViewMode Mode,
    int StoredFaceCount,
    int PresentedFaceCount,
    int HiddenFaceCount,
    bool HasPlayablePredicate);

internal sealed record NativeTerrainDepthCueSnapshot(
    int VisibleFaceCount,
    int VisibleCornerCount,
    int DistinctEndpointCornerCount,
    int NearEndpointCornerCount,
    int BlendedCornerCount,
    int FarEndpointCornerCount,
    int ChangedFromLegacyCornerCount,
    double AverageNearWeight,
    double MeanChannelDeltaFromLegacy,
    double MinCameraDepth,
    double MaxCameraDepth);

internal sealed record NativeTerrainMapMaterialSnapshot(
    string Contract,
    int VisibleFaceCount,
    int AuditedBroadUnderlayFaceCount,
    int SubduedBroadUnderlayFaceCount,
    int NativeMaterialCandidateFaceCount,
    int NativeTextureFaceCount,
    int NativeUntexturedGouraudFaceCount,
    int GuardedUnderlayFallbackFaceCount,
    int OpaqueGuardedUnderlayFallbackFaceCount,
    int ProjectionDegenerateFaceCount,
    int UnresolvedFallbackFaceCount,
    int AvailableNormalTextureCount,
    int OffCameraSourceOutlineFaceCount,
    string ScopeNote)
{
    public static NativeTerrainMapMaterialSnapshot Empty { get; } = new(
        EditorViewport.NativeTerrainMapMaterialContract,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "Map terrain has not rendered yet.");
}

internal sealed record FlyTerrainInteractiveLodSnapshot(
    bool Active,
    int FullMaterialFaceBudget,
    int VisibleTerrainFaceCount,
    int FullMaterialFaceCount,
    int SimplifiedMaterialFaceCount,
    int ForcedFullMaterialFaceCount,
    int PartiallyFullMaterialSectorCount)
{
    public static FlyTerrainInteractiveLodSnapshot Empty { get; } = new(
        false,
        0,
        0,
        0,
        0,
        0,
        0);
}

internal sealed record FlyTerrainVisibilitySnapshot(
    FlyTerrainVisibilityMode Mode,
    bool GameViewEnabled,
    bool CameraIsOverview,
    int SectorCount,
    int QueuedHighDetailSectorCount,
    int StoredHighDetailFaceCount,
    int VisibleHighDetailFaceCount,
    int SuppressedHighDetailByGroupCount,
    int SuppressedHighDetailBySectorCount,
    int SuppressedHighDetailByFaceDistanceCount,
    int FoggedHighDetailFaceCount,
    int FullyFoggedHighDetailFaceCount,
    int StoredLowDetailFaceCount,
    int VisibleLowDetailFaceCount,
    int ForcedVisibleSectorCount,
    int LocalHighDetailGroupIndex,
    int LocalHighDetailGroupSectorCount,
    int VisibleMobyCount,
    int SuppressedFarMobyCount,
    string ScopeNote)
{
    public static FlyTerrainVisibilitySnapshot Empty { get; } = new(
        Mode: FlyTerrainVisibilityMode.EditorOverviewHighDetail,
        GameViewEnabled: true,
        CameraIsOverview: true,
        SectorCount: 0,
        QueuedHighDetailSectorCount: 0,
        StoredHighDetailFaceCount: 0,
        VisibleHighDetailFaceCount: 0,
        SuppressedHighDetailByGroupCount: 0,
        SuppressedHighDetailBySectorCount: 0,
        SuppressedHighDetailByFaceDistanceCount: 0,
        FoggedHighDetailFaceCount: 0,
        FullyFoggedHighDetailFaceCount: 0,
        StoredLowDetailFaceCount: 0,
        VisibleLowDetailFaceCount: 0,
        ForcedVisibleSectorCount: 0,
        LocalHighDetailGroupIndex: -1,
        LocalHighDetailGroupSectorCount: 0,
        VisibleMobyCount: 0,
        SuppressedFarMobyCount: 0,
        ScopeNote: "Fly terrain has not rendered yet.");
}

internal sealed record NativeTerrainOcclusionSnapshot(
    string View,
    bool PayloadAvailable,
    bool CollisionTriangleResolved,
    int GroupIndex,
    int TriangleIndex,
    double FloorZ,
    int StoredSectorCount,
    int NativeGroupSectorCount,
    int VisibleSectorCount,
    int ForcedSectorCount,
    string Contract)
{
    public static NativeTerrainOcclusionSnapshot Empty { get; } = new(
        "none",
        false,
        false,
        -1,
        -1,
        double.NegativeInfinity,
        0,
        0,
        0,
        0,
        "Terrain occlusion has not rendered yet.");
}

internal sealed record GameCameraEntrySnapshot(
    bool PoseAvailable,
    string Source,
    double FocusX,
    double FocusY,
    double FocusZ,
    int YawByte,
    double DesiredBehind,
    double AppliedBehind,
    double AppliedRise,
    double CameraX,
    double CameraY,
    double CameraZ,
    int CollisionGroup,
    bool CollisionConstrained,
    string Note)
{
    public static GameCameraEntrySnapshot Empty { get; } = Fallback(
        "The Game Camera has not resolved a level entry pose.",
        0,
        0,
        0);

    public static GameCameraEntrySnapshot Fallback(string note, double cameraX, double cameraY, double cameraZ) => new(
        false,
        "fallback",
        0,
        0,
        0,
        -1,
        0,
        0,
        0,
        cameraX,
        cameraY,
        cameraZ,
        -1,
        false,
        note);
}

internal enum FlyTerrainVisibilityMode
{
    EditorOverviewHighDetail,
    GameViewCameraLocalHighDetail,
    NativeDistanceResearch
}

internal sealed record NativeTerrainLqFrameSnapshot(
    int TextureId,
    double PlanarEditorDepth,
    bool HighPolyVisible,
    bool RetainedWithoutLowDetail,
    bool HqOverlayEligible,
    int DescriptorIndex,
    int PaletteRow,
    NativeTerrainTexturePreviewTier? HqTier,
    bool HasIndexedPayload,
    bool BitmapResolved,
    int BitmapWidth,
    int BitmapHeight,
    string BitmapSha256,
    int Abr,
    string RawDescriptorSha256,
    string PackedIndicesSha256,
    string PaletteWordsSha256);

internal sealed record NativeTerrainFarLodSnapshot(
    NativeTerrainLodPreviewMode PreviewMode,
    bool HasStaticLowDetailPreview,
    string Contract,
    int SectorCount,
    int StoredHighDetailFaces,
    int StoredLowDetailFaces,
    int QueuedHighDetailSectors,
    int QueuedLowDetailSectors,
    int VisibleHighDetailFaces,
    int VisibleLowDetailFaces,
    int SuppressedHighDetailFaces,
    int VisibleHighDetailFacesAtOrBeyondCutoff,
    int VisibleLowDetailOnlyFaces,
    int VisibleForcedLowDetailFaces,
    int DistinctVisibleLowDetailCornerColors,
    int HqOverlayEligibleFaces,
    int LqDescriptorZeroFaces,
    int LqDescriptorOneFaces,
    int MinimumLqPaletteRow,
    int MaximumLqPaletteRow);

internal enum NativeTerrainLodPreviewMode
{
    EditorOverviewHighDetail,
    NativeDistance
}

public sealed class MobyMoveRequestedEventArgs : EventArgs
{
    public MobyMoveRequestedEventArgs(Moby moby, float dx, float dy, float dz)
    {
        Moby = moby;
        Dx = dx;
        Dy = dy;
        Dz = dz;
    }

    public Moby Moby { get; }
    public float Dx { get; }
    public float Dy { get; }
    public float Dz { get; }
}

public sealed class MobyRotateRequestedEventArgs : EventArgs
{
    public MobyRotateRequestedEventArgs(Moby moby, int yawByte)
    {
        Moby = moby;
        YawByte = yawByte & 0xFF;
    }

    public Moby Moby { get; }
    public int YawByte { get; }
}

public sealed class ViewportObjectPlacementRequestedEventArgs : EventArgs
{
    public ViewportObjectPlacementRequestedEventArgs(Point screenPoint)
    {
        ScreenPoint = screenPoint;
    }

    public Point ScreenPoint { get; }
}

public sealed class ViewportObjectPasteRequestedEventArgs : EventArgs
{
    public ViewportObjectPasteRequestedEventArgs(Point screenPoint)
    {
        ScreenPoint = screenPoint;
    }

    public Point ScreenPoint { get; }
}

public sealed class ViewportTerrainEditRequestedEventArgs : EventArgs
{
    public ViewportTerrainEditRequestedEventArgs(int terrainIndex, TerrainPolygon terrain)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
}

public sealed class ViewportTerrainMoveRequestedEventArgs : EventArgs
{
    public ViewportTerrainMoveRequestedEventArgs(int terrainIndex, TerrainPolygon terrain, float dx, float dy, float dz, bool stageAddCopy)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
        Dx = dx;
        Dy = dy;
        Dz = dz;
        StageAddCopy = stageAddCopy;
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
    public float Dx { get; }
    public float Dy { get; }
    public float Dz { get; }
    public bool StageAddCopy { get; }
}

public sealed class ViewportTerrainRemoveRequestedEventArgs : EventArgs
{
    public ViewportTerrainRemoveRequestedEventArgs(int terrainIndex, TerrainPolygon terrain)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
}

public sealed class ViewportTerrainLookRequestedEventArgs : EventArgs
{
    public ViewportTerrainLookRequestedEventArgs(int terrainIndex, TerrainPolygon terrain)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
}

public sealed class ViewportTerrainPointEditRequestedEventArgs : EventArgs
{
    public ViewportTerrainPointEditRequestedEventArgs(int terrainIndex, int pointIndex, TerrainPolygon terrain)
    {
        TerrainIndex = terrainIndex;
        PointIndex = pointIndex;
        Terrain = terrain;
    }

    public int TerrainIndex { get; }
    public int PointIndex { get; }
    public TerrainPolygon Terrain { get; }
}

public sealed class ViewportTerrainPointMoveRequestedEventArgs : EventArgs
{
    public ViewportTerrainPointMoveRequestedEventArgs(int terrainIndex, int pointIndex, TerrainPolygon terrain, float dx, float dy, float dz)
    {
        TerrainIndex = terrainIndex;
        PointIndex = pointIndex;
        Terrain = terrain;
        Dx = dx;
        Dy = dy;
        Dz = dz;
    }

    public int TerrainIndex { get; }
    public int PointIndex { get; }
    public TerrainPolygon Terrain { get; }
    public float Dx { get; }
    public float Dy { get; }
    public float Dz { get; }
}

public sealed class ViewportTerrainBrushRequestedEventArgs : EventArgs
{
    public ViewportTerrainBrushRequestedEventArgs(int terrainIndex, TerrainPolygon terrain, Vector2f brushCenter, TerrainBrushAction action, bool isStrokeStart, double strengthScale = 1.0)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
        BrushCenter = brushCenter;
        Action = action;
        IsStrokeStart = isStrokeStart;
        StrengthScale = Math.Clamp(strengthScale, 0.05, 1.0);
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
    public Vector2f BrushCenter { get; }
    public TerrainBrushAction Action { get; }
    public bool IsStrokeStart { get; }
    public double StrengthScale { get; }
}

public sealed class ViewportTerrainBrushAdjustmentRequestedEventArgs : EventArgs
{
    public ViewportTerrainBrushAdjustmentRequestedEventArgs(TerrainBrushAdjustmentKind kind, int direction)
    {
        Kind = kind;
        Direction = direction >= 0 ? 1 : -1;
    }

    public TerrainBrushAdjustmentKind Kind { get; }
    public int Direction { get; }
}

public sealed class ViewportTerrainBrushModeRequestedEventArgs : EventArgs
{
    public ViewportTerrainBrushModeRequestedEventArgs(TerrainBrushAction action)
    {
        Action = action;
    }

    public TerrainBrushAction Action { get; }
}

public sealed class ViewportTerrainTexturePaintRequestedEventArgs : EventArgs
{
    public ViewportTerrainTexturePaintRequestedEventArgs(int terrainIndex, TerrainPolygon terrain)
    {
        TerrainIndex = terrainIndex;
        Terrain = terrain;
    }

    public int TerrainIndex { get; }
    public TerrainPolygon Terrain { get; }
}

public sealed class ViewportSelectionChangedEventArgs : EventArgs
{
    private ViewportSelectionChangedEventArgs(Moby? moby, TerrainPolygon? terrain, int terrainIndex, int terrainPointIndex)
    {
        Moby = moby;
        Terrain = terrain;
        TerrainIndex = terrainIndex;
        TerrainPointIndex = terrainPointIndex;
    }

    public Moby? Moby { get; }
    public TerrainPolygon? Terrain { get; }
    public int TerrainIndex { get; }
    public int TerrainPointIndex { get; }

    public static ViewportSelectionChangedEventArgs None { get; } = new(null, null, -1, -1);

    public static ViewportSelectionChangedEventArgs ForMoby(Moby moby)
    {
        return new ViewportSelectionChangedEventArgs(moby, null, -1, -1);
    }

    public static ViewportSelectionChangedEventArgs ForTerrain(int index, TerrainPolygon terrain, int terrainPointIndex = -1)
    {
        return new ViewportSelectionChangedEventArgs(null, terrain, index, terrainPointIndex);
    }
}

public enum ViewportViewMode
{
    Map,
    Fly3D
}

public enum TerrainSceneViewMode
{
    Playable,
    CompleteScene
}

public enum TerrainBrushAction
{
    Off,
    Raise,
    Lower,
    Blend,
    Flatten,
    Restore
}

public enum TerrainBrushAdjustmentKind
{
    Size,
    Strength,
    Feather
}

public enum TerrainPatchSafetyKind
{
    None,
    Structural,
    TextureOnly,
    PlayableHeight,
    PartialHeight,
    VisualOnlyHeight,
    UnknownHeight
}
