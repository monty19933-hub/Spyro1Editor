using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class EditorViewport
{
    private IReadOnlyDictionary<int, NativeMobyPath> _nativeMobyPathsByOwner =
        new Dictionary<int, NativeMobyPath>();
    private IReadOnlyDictionary<int, ViewportDragonRunToTarget> _dragonRunToTargetsByOwner =
        new Dictionary<int, ViewportDragonRunToTarget>();
    private readonly List<ScreenNativeMovementHandle> _screenNativeMovementHandles = [];
    private ScreenNativeMovementHandle? _draggingNativeMovementHandle;
    private int _visibleNativePathSegmentCount;

    public event EventHandler<ViewportNativePathNodeMoveRequestedEventArgs>? NativePathNodeMoveRequested;
    public event EventHandler<ViewportDragonRunToMoveRequestedEventArgs>? DragonRunToMoveRequested;

    public void SetNativeMovementData(
        IEnumerable<NativeMobyPath>? paths,
        IEnumerable<ViewportDragonRunToTarget>? dragonRunToTargets = null)
    {
        _nativeMobyPathsByOwner = (paths ?? [])
            .GroupBy(path => path.OwnerTrueIndex)
            .ToDictionary(group => group.Key, group => group.Single());
        _dragonRunToTargetsByOwner = (dragonRunToTargets ?? [])
            .GroupBy(target => target.OwnerTrueIndex)
            .ToDictionary(group => group.Key, group => group.Single());
        _screenNativeMovementHandles.Clear();
        _draggingNativeMovementHandle = null;
        InvalidateVisual();
    }

    public void RefreshNativeMovementData() => InvalidateVisual();

    public NativePathOverlaySnapshot CaptureNativePathOverlaySnapshotForTesting()
    {
        int selectedOwnerTrueIndex = _selectedMobyIndex >= 0 && _selectedMobyIndex < Mobys.Count
            ? Mobys[_selectedMobyIndex].TrueIndex
            : -1;
        return new NativePathOverlaySnapshot(
            _viewMode,
            selectedOwnerTrueIndex,
            _screenNativeMovementHandles.Count(handle => handle.Kind == NativeMovementHandleKind.PathNode),
            _visibleNativePathSegmentCount);
    }

    private void ClearNativeMovementScreenState()
    {
        _screenNativeMovementHandles.Clear();
        _visibleNativePathSegmentCount = 0;
        if (_selectedMobyIndex < 0 || _selectedMobyIndex >= Mobys.Count)
            _draggingNativeMovementHandle = null;
    }

    private void DrawNativeMovementOverlays(DrawingContext context, Rect bounds)
    {
        if (_selectedMobyIndex < 0 || _selectedMobyIndex >= Mobys.Count)
            return;

        Moby owner = Mobys[_selectedMobyIndex];
        if (_nativeMobyPathsByOwner.TryGetValue(owner.TrueIndex, out NativeMobyPath? path))
            DrawNativePathOverlay(context, bounds, path);
        if (_dragonRunToTargetsByOwner.TryGetValue(owner.TrueIndex, out ViewportDragonRunToTarget? target))
            DrawDragonRunToOverlay(context, bounds, owner, target);
    }

    private void DrawNativePathOverlay(DrawingContext context, Rect bounds, NativeMobyPath path)
    {
        List<(NativePathNode Node, Point Screen)> projected = [];
        foreach (NativePathNode node in path.Nodes)
        {
            if (TryProjectNativeMovementPoint(bounds, node.Position, out Point screen))
                projected.Add((node, screen));
        }

        Pen pathPen = new(new SolidColorBrush(Color.FromArgb(230, 255, 201, 64)), 2.2);
        for (int index = 1; index < projected.Count; index++)
        {
            if (projected[index].Node.Index == projected[index - 1].Node.Index + 1)
            {
                context.DrawLine(pathPen, projected[index - 1].Screen, projected[index].Screen);
                _visibleNativePathSegmentCount++;
            }
        }

        foreach ((NativePathNode node, Point screen) in projected)
        {
            bool edited = node.HasEdit;
            Color fillColor = edited ? Color.FromRgb(255, 126, 76) : Color.FromRgb(255, 225, 92);
            double radius = edited ? 8.2 : 7.2;
            context.DrawEllipse(
                new SolidColorBrush(fillColor),
                new Pen(new SolidColorBrush(Color.FromRgb(34, 40, 47)), 1.7),
                screen,
                radius,
                radius);
            DrawNativeMovementHandleLabel(context, screen, (node.Index + 1).ToString(), Colors.Black);
            _screenNativeMovementHandles.Add(new ScreenNativeMovementHandle(
                NativeMovementHandleKind.PathNode,
                path.OwnerTrueIndex,
                node.Index,
                screen,
                Math.Max(11, radius + 4),
                node.Position.Z));
        }
    }

    private void DrawDragonRunToOverlay(
        DrawingContext context,
        Rect bounds,
        Moby owner,
        ViewportDragonRunToTarget target)
    {
        if (!TryProjectNativeMovementPoint(bounds, owner.Position, out Point ownerPoint) ||
            !TryProjectNativeMovementPoint(bounds, target.Position, out Point targetPoint))
        {
            return;
        }

        DrawDashedLine(
            context,
            new Pen(new SolidColorBrush(Color.FromArgb(235, 105, 224, 255)), 2.2),
            ownerPoint,
            targetPoint,
            9,
            6);
        context.DrawEllipse(
            new SolidColorBrush(target.HasEdit ? Color.FromRgb(255, 126, 76) : Color.FromRgb(105, 224, 255)),
            new Pen(new SolidColorBrush(Color.FromRgb(25, 37, 47)), 2),
            targetPoint,
            9,
            9);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.White), 1.5), targetPoint, 4.2, 4.2);
        DrawNativeMovementChip(context, targetPoint + new Vector(13, -24), "Spyro runs here");
        _screenNativeMovementHandles.Add(new ScreenNativeMovementHandle(
            NativeMovementHandleKind.DragonRunTo,
            owner.TrueIndex,
            -1,
            targetPoint,
            14,
            target.Position.Z));
    }

    private bool TryProjectNativeMovementPoint(Rect bounds, Vector3f point, out Point screen)
    {
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (TryProjectFly(bounds, point.X, point.Y, point.Z, out ProjectedPoint projected))
            {
                screen = projected.Screen;
                return bounds.Inflate(80).Contains(screen);
            }

            screen = default;
            return false;
        }

        screen = CreateMobyTransform(bounds, Mobys).Project(point.X, point.Y, point.Z);
        return bounds.Inflate(80).Contains(screen);
    }

    private bool TryBeginNativeMovementHandleDrag(Point position, IPointer pointer)
    {
        ScreenNativeMovementHandle? handle = FindNativeMovementHandle(position);
        if (handle == null)
            return false;

        _draggingNativeMovementHandle = handle;
        SetViewportCursor(StandardCursorType.SizeAll);
        pointer.Capture(this);
        return true;
    }

    private bool TryDragNativeMovementHandle(Point position)
    {
        ScreenNativeMovementHandle? handle = _draggingNativeMovementHandle;
        if (handle == null)
            return false;

        Rect bounds = new(Bounds.Size);
        Point previousWorld;
        Point currentWorld;
        if (_viewMode == ViewportViewMode.Fly3D)
        {
            if (!TryUnprojectFlyToZ(bounds, _lastPointerPosition, handle.WorldZ, out previousWorld) ||
                !TryUnprojectFlyToZ(bounds, position, handle.WorldZ, out currentWorld))
            {
                return true;
            }
        }
        else
        {
            SceneTransform transform = CreateMobyTransform(bounds, Mobys);
            previousWorld = transform.Unproject(_lastPointerPosition, handle.WorldZ);
            currentWorld = transform.Unproject(position, handle.WorldZ);
        }

        float dx = (float)(currentWorld.X - previousWorld.X);
        float dy = (float)(currentWorld.Y - previousWorld.Y);
        if (Math.Abs(dx) <= 0.001f && Math.Abs(dy) <= 0.001f)
            return true;

        if (handle.Kind == NativeMovementHandleKind.PathNode &&
            _nativeMobyPathsByOwner.TryGetValue(handle.OwnerTrueIndex, out NativeMobyPath? path) &&
            handle.NodeIndex >= 0 && handle.NodeIndex < path.Nodes.Count)
        {
            NativePathNodeMoveRequested?.Invoke(
                this,
                new ViewportNativePathNodeMoveRequestedEventArgs(path, path.Nodes[handle.NodeIndex], dx, dy));
        }
        else if (handle.Kind == NativeMovementHandleKind.DragonRunTo &&
            _dragonRunToTargetsByOwner.TryGetValue(handle.OwnerTrueIndex, out ViewportDragonRunToTarget? target))
        {
            DragonRunToMoveRequested?.Invoke(
                this,
                new ViewportDragonRunToMoveRequestedEventArgs(target, dx, dy));
        }

        return true;
    }

    private bool EndNativeMovementHandleDrag(IPointer pointer)
    {
        if (_draggingNativeMovementHandle == null)
            return false;

        _draggingNativeMovementHandle = null;
        pointer.Capture(null);
        SetViewportCursor(null);
        return true;
    }

    private ScreenNativeMovementHandle? FindNativeMovementHandle(Point point)
    {
        for (int index = _screenNativeMovementHandles.Count - 1; index >= 0; index--)
        {
            ScreenNativeMovementHandle handle = _screenNativeMovementHandles[index];
            Vector delta = point - handle.Center;
            if ((delta.X * delta.X) + (delta.Y * delta.Y) <= handle.HitRadius * handle.HitRadius)
                return handle;
        }

        return null;
    }

    private static void DrawDashedLine(
        DrawingContext context,
        Pen pen,
        Point start,
        Point end,
        double dashLength,
        double gapLength)
    {
        Vector delta = end - start;
        double length = delta.Length;
        if (length <= 0.001)
            return;
        Vector direction = delta / length;
        for (double offset = 0; offset < length; offset += dashLength + gapLength)
        {
            double dashEnd = Math.Min(length, offset + dashLength);
            context.DrawLine(pen, start + (direction * offset), start + (direction * dashEnd));
        }
    }

    private static void DrawNativeMovementHandleLabel(DrawingContext context, Point center, string label, Color color)
    {
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10,
            new SolidColorBrush(color));
        context.DrawText(text, center - new Vector(text.Width * 0.5, text.Height * 0.5));
    }

    private static void DrawNativeMovementChip(DrawingContext context, Point origin, string label)
    {
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            11,
            new SolidColorBrush(Colors.White));
        Rect background = new(origin - new Vector(5, 3), new Size(text.Width + 10, text.Height + 6));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(225, 24, 45, 58)), background, 4);
        context.DrawText(text, origin);
    }

    private enum NativeMovementHandleKind
    {
        PathNode,
        DragonRunTo
    }

    private sealed record ScreenNativeMovementHandle(
        NativeMovementHandleKind Kind,
        int OwnerTrueIndex,
        int NodeIndex,
        Point Center,
        double HitRadius,
        double WorldZ);
}

public sealed class ViewportDragonRunToTarget
{
    public ViewportDragonRunToTarget(
        int ownerTrueIndex,
        Vector3f originalPosition,
        Vector3f position,
        bool terrainHit)
    {
        OwnerTrueIndex = ownerTrueIndex;
        OriginalPosition = originalPosition;
        Position = position;
        TerrainHit = terrainHit;
    }

    public int OwnerTrueIndex { get; }
    public Vector3f OriginalPosition { get; }
    public Vector3f Position { get; set; }
    public bool TerrainHit { get; set; }
    public bool HasEdit =>
        Math.Abs(Position.X - OriginalPosition.X) > 0.001f ||
        Math.Abs(Position.Y - OriginalPosition.Y) > 0.001f;
}

public sealed record ViewportNativePathNodeMoveRequestedEventArgs(
    NativeMobyPath Path,
    NativePathNode Node,
    float Dx,
    float Dy);

public sealed record ViewportDragonRunToMoveRequestedEventArgs(
    ViewportDragonRunToTarget Target,
    float Dx,
    float Dy);

public sealed record NativePathOverlaySnapshot(
    ViewportViewMode ViewMode,
    int OwnerTrueIndex,
    int ProjectedNodeHandleCount,
    int PolylineSegmentCount);
