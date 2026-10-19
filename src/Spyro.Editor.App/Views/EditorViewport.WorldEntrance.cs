using Avalonia;
using Avalonia.Media;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain › World Entrance in one of the game's homeworlds: the New World's doorway is drawn over the level as a
/// preview (its frame's faces and its opening), because it only becomes terrain when Create BIN builds it.</summary>
public sealed partial class EditorViewport
{
    private IReadOnlyList<(Vector3f[] Corners, bool Front)> _worldEntranceFaces = [];
    private IReadOnlyList<Vector3f> _worldEntranceOpening = [];

    /// <summary>Shows (or with empty lists hides) the entrance preview: frame faces (corners in editor units; fronts drawn
    /// lighter) and the opening's outline.</summary>
    internal void SetWorldEntrancePreview(IReadOnlyList<(Vector3f[] Corners, bool Front)> faces, IReadOnlyList<Vector3f> opening)
    {
        _worldEntranceFaces = faces;
        _worldEntranceOpening = opening;
        InvalidateVisual();
    }

    internal int WorldEntrancePreviewFaceCountForTesting => _worldEntranceFaces.Count;

    private static readonly IBrush WorldEntranceFront = new SolidColorBrush(Color.FromArgb(215, 176, 170, 158));
    private static readonly IBrush WorldEntranceSide = new SolidColorBrush(Color.FromArgb(215, 128, 122, 112));
    private static readonly IBrush WorldEntranceSky = new SolidColorBrush(Color.FromArgb(150, 196, 150, 232));
    private static readonly IPen WorldEntranceEdge = new Pen(new SolidColorBrush(Color.FromRgb(70, 64, 58)), 0.8);
    private static readonly IPen WorldEntranceOutline = new Pen(new SolidColorBrush(Color.FromRgb(246, 214, 92)), 2);

    private void DrawWorldEntrancePreview(DrawingContext context, Rect bounds, bool fly)
    {
        if (_worldEntranceFaces.Count == 0 && _worldEntranceOpening.Count == 0) return;
        Func<Vector3f, (Point Screen, double Depth)?> project;
        if (fly)
            project = p => TryProjectFly(bounds, p.X, p.Y, p.Z, out ProjectedPoint q) ? (q.Screen, q.Depth) : null;
        else
        {
            if (Geometry is not { Polygons.Count: > 0 } geometry) return;
            SceneTransform transform = CreateGeometryTransform(bounds, geometry);
            project = p => (transform.Project(p.X, p.Y, p.Z), p.Z);
        }
        var drawn = new List<(Point[] Points, double Depth, IBrush Fill, IPen? Pen)>();
        void Add(IEnumerable<Vector3f> corners, IBrush fill, IPen? pen)
        {
            var points = corners.Select(project).ToArray();
            if (points.Any(p => p == null)) return;
            drawn.Add((points.Select(p => p!.Value.Screen).ToArray(), fly ? points.Average(p => p!.Value.Depth) : -points.Average(p => p!.Value.Depth), fill, pen));
        }
        if (_worldEntranceOpening.Count >= 3) Add(_worldEntranceOpening, WorldEntranceSky, WorldEntranceOutline);
        foreach (var (corners, front) in _worldEntranceFaces) Add(corners, front ? WorldEntranceFront : WorldEntranceSide, WorldEntranceEdge);
        foreach (var face in drawn.OrderByDescending(d => d.Depth))
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(face.Points[0], true);
                foreach (var p in face.Points.Skip(1)) g.LineTo(p);
                g.EndFigure(true);
            }
            context.DrawGeometry(face.Fill, face.Pen, geometry);
        }
    }
}
