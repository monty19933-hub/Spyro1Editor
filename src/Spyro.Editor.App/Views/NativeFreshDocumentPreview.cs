using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.App.Views;

/// <summary>Bounded authored-coordinate diagram, not native rendering or collision evidence.</summary>
internal sealed partial class NativeFreshDocumentPreview : Control
{
    private NativeId65FreshDocumentIntent? _intent;
    private bool _aggregatePresentation;
    internal NativeId65FreshDocumentIntent? Intent
    {
        get => _intent;
        set { _intent = value; _aggregatePresentation = false; AggregateIntent = null; SelectedEditorId = null; InvalidateVisual(); }
    }
    internal NativeFreshLevelDocumentIntent? AggregateIntent { get; private set; }
    internal string? SelectedEditorId { get; private set; }
    internal string MaterialPreviewNotice => AggregateIntent?.FloorMaterials != null || AggregateIntent?.TiledContent != null
        ? "Selected materials are not rendered in this schematic." : "";
    internal string PlacementPreviewCaption
    {
        get
        {
            if (AggregateIntent == null) return "0 placed Mobys";
            if (HasFamilies) return FamilyPlacementCaption;
            int native = AggregateIntent.Placements.OfType<NativeFreshMobyPlacementIntent>().Count();
            if (native == 0) return $"{AggregateIntent.Placements.Count} authored routes (XY only)";
            int routes = AggregateIntent.Placements.Count - native;
            return $"{routes} authored route{(routes == 1 ? "" : "s")} + {native} native marker{(native == 1 ? "" : "s")} (XY only)";
        }
    }
    internal double SchematicCaptionTop => Bounds.Height - (MaterialPreviewNotice.Length == 0 ? 34 : 56) - FamilyLegendHeight;
    internal Rect? FloorSchematicBounds
    {
        get
        {
            if (Presentation() is not { } intent) return null;
            var projection = CreateProjection(intent);
            Point[] corners = intent.Tiles.SelectMany(tile => new[]
            {
                projection.Project(tile.X, tile.Y, tile.Z),
                projection.Project(tile.X + tile.Width, tile.Y, tile.Z),
                projection.Project(tile.X, tile.Y + tile.Depth, tile.Z),
                projection.Project(tile.X + tile.Width, tile.Y + tile.Depth, tile.Z)
            }).ToArray();
            double x = corners.Min(p => p.X), y = corners.Min(p => p.Y);
            return new Rect(x, y, corners.Max(p => p.X) - x, corners.Max(p => p.Y) - y);
        }
    }

    internal void SetAggregate(NativeFreshLevelDocumentIntent? aggregate, string? selectedEditorId)
    {
        AggregateIntent = aggregate;
        _aggregatePresentation = true;
        SelectedEditorId = selectedEditorId;
        _intent = aggregate?.LegacyContent?.Base;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        var intent = Presentation();
        context.FillRectangle(intent == null ? new SolidColorBrush(Color.Parse("#E2E9F0")) :
            new SolidColorBrush(Color.FromRgb(intent.SkyColor.R, intent.SkyColor.G, intent.SkyColor.B)), bounds);
        if (intent == null)
        {
            Text(context, "No authored scene", new Point(24, 28), Brushes.DarkSlateGray, 20);
            Text(context, _aggregatePresentation ? "No aggregate exists. Fork a saved legacy floor explicitly." :
                "Open a saved document or choose New starter.", new Point(24, 60), Brushes.DarkSlateGray, 13);
            return;
        }

        // Same A,B,C / B,D,C topology as the bounded native surface producer.
        // Positions here are a neutral projection of intent, not a source-issued payload.
        var projection = CreateProjection(intent);
        Point Project(double x, double y, double z) => projection.Project(x, y, z);
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)), 2);
        foreach (var tile in intent.Tiles)
        {
            Point a = Project(tile.X, tile.Y, tile.Z), b = Project(tile.X + tile.Width, tile.Y, tile.Z);
            Point c = Project(tile.X, tile.Y + tile.Depth, tile.Z), e = Project(tile.X + tile.Width, tile.Y + tile.Depth, tile.Z);
            IBrush floor = new SolidColorBrush(Color.FromRgb(tile.Color.R, tile.Color.G, tile.Color.B));
            Polygon(context, [a, b, c], floor, edge); Polygon(context, [b, e, c], floor, edge);
        }
        Point foot = Project(intent.SpawnX, intent.SpawnY, intent.SpawnZ - 512);
        Point spawn = Project(intent.SpawnX, intent.SpawnY, intent.SpawnZ);
        context.DrawLine(new Pen(Brushes.White, 2, new DashStyle([3, 3], 0)), foot, spawn);
        context.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 2), foot, 5, 3);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#A64AC9")), new Pen(Brushes.White, 2), spawn, 7, 7);
        double angle = intent.Heading * Math.PI * 2 / 256;
        Point tip = Project(intent.SpawnX + Math.Cos(angle) * 150, intent.SpawnY + Math.Sin(angle) * 150, intent.SpawnZ);
        context.DrawLine(new Pen(Brushes.White, 3), spawn, tip);
        Vector direction = tip - spawn;
        double length = Math.Max(1, Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        Vector unit = direction / length;
        Vector side = new(-unit.Y, unit.X);
        Polygon(context, [tip, tip - unit * 10 + side * 5, tip - unit * 10 - side * 5], Brushes.White, null);

        if (AggregateIntent is { } aggregate)
        {
            // Display typed authored routes/markers in XY at the floor projection.
            // Exact Z/yaw remain numeric: no native model or height simulation.
            for (int i = 0; i < aggregate.Placements.Count; i++)
            {
                var placement = aggregate.Placements[i];
                bool selected = placement.EditorId == SelectedEditorId;
                if (placement is NativeFreshWizardRouteIntent route)
                {
                    Point origin = Project(route.OriginX, route.OriginY, intent.FloorZ);
                    Point end = Project(route.EndX, route.EndY, intent.FloorZ);
                    IBrush ink = selected ? Brushes.Yellow : Brushes.Cyan;
                    context.DrawLine(new Pen(ink, selected ? 3 : 1.5, new DashStyle([4, 3], 0)), origin, end);
                    context.DrawEllipse(ink, new Pen(Brushes.DarkSlateGray, 1), origin, selected ? 6 : 4, selected ? 6 : 4);
                    context.DrawEllipse(Brushes.Transparent, new Pen(ink, selected ? 3 : 1.5), end, 4, 4);
                    if (selected) Text(context, $"Route {i + 1} · authored Z {route.OriginZ}", origin + new Vector(9, -20), Brushes.White, 12);
                }
                else if (placement is NativeFreshMobyPlacementIntent moby)
                {
                    Point point = Project(moby.X, moby.Y, intent.FloorZ);
                    double radius = selected ? 7 : 5;
                    IBrush ink = selected ? Brushes.Yellow : Brushes.Orange;
                    Polygon(context, [point + new Vector(0, -radius), point + new Vector(radius, 0),
                        point + new Vector(0, radius), point + new Vector(-radius, 0)], ink, new Pen(Brushes.DarkSlateGray, 1));
                    if (selected) Text(context, $"Native marker {i + 1} · Z {moby.Z} · yaw {moby.NativeYawByte}",
                        point + new Vector(9, -20), Brushes.White, 12);
                }
            }
        }

        if (HasFamilies) RenderFamilies(context);

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)), new Rect(0, 0, bounds.Width, 76));
        Text(context, intent.Tiled ? $"{intent.Tiles.Count} ordered tiles · bounds {intent.FloorWidth} × {intent.FloorDepth}"
            : $"{intent.FloorWidth} × {intent.FloorDepth}  ·  floor Z {intent.FloorZ}", new Point(16, 12), Brushes.White, 17);
        Text(context, $"Origin {intent.FloorX}, {intent.FloorY}  ·  spawn {intent.SpawnX}, {intent.SpawnY}, {intent.SpawnZ}",
            new Point(16, 40), Brushes.White, 12);
        bool hasMaterialNotice = MaterialPreviewNotice.Length != 0;
        double captionHeight = hasMaterialNotice ? 56 : 34;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)),
            new Rect(0, bounds.Height - captionHeight, bounds.Width, captionHeight));
        if (HasFamilies)
            FamilyText(context, FamilySceneCaption(intent), new Point(12, bounds.Height - captionHeight + 8), Brushes.White, 12);
        else
            Text(context, $"Heading {intent.Heading}/255 (schematic)  ·  music track {intent.MusicTrackId:00}  ·  {PlacementPreviewCaption}",
                new Point(16, bounds.Height - captionHeight + 8), Brushes.White, 12);
        if (hasMaterialNotice)
            Text(context, MaterialPreviewNotice, new Point(16, bounds.Height - 26), Brushes.White, 12);
    }

    // Display-only projection data, never a fabricated legacy document or an
    // alternate persisted geometry authority. Legacy rendering stays byte/pixel
    // compatible; tiled rendering reads every actual active tile.
    private sealed record TileView(int X, int Y, int Z, int Width, int Depth, ColorRgba Color);
    private sealed record SurfaceView(int FloorX, int FloorY, int FloorZ, int FloorWidth, int FloorDepth,
        int SpawnX, int SpawnY, int SpawnZ, int Heading, ColorRgba SkyColor, int MusicTrackId,
        IReadOnlyList<TileView> Tiles, bool Tiled);

    private SurfaceView? Presentation()
    {
        if (AggregateIntent?.TiledContent is { } tiled)
        {
            var tiles = tiled.Tiles.Select(t => new TileView(t.X,t.Y,t.Z,t.Width,t.Depth,t.FloorColor)).ToArray();
            int x = tiles.Min(t => t.X), y = tiles.Min(t => t.Y), z = tiles.Min(t => t.Z);
            return new(x,y,z,tiles.Max(t => t.X+t.Width)-x,tiles.Max(t => t.Y+t.Depth)-y,
                tiled.SpawnX,tiled.SpawnY,tiled.SpawnZ,tiled.Heading,tiled.SkyColor,tiled.MusicTrackId,tiles,true);
        }
        if (Intent is not { } old) return null;
        return new(old.FloorX,old.FloorY,old.FloorZ,old.FloorWidth,old.FloorDepth,old.SpawnX,old.SpawnY,
            old.SpawnZ,old.Heading,old.SkyColor,old.MusicTrackId,
            new[] { new TileView(old.FloorX,old.FloorY,old.FloorZ,old.FloorWidth,old.FloorDepth,old.FloorColor) },false);
    }

    private SchematicProjection CreateProjection(SurfaceView intent)
    {
        if (intent.Tiled || HasFamilies)
        {
            // Fit actual projected corners and spawn, including separated heights,
            // between captions. A world-space bounding rectangle alone is not a
            // safe screen bound and must never be rendered as a filled-in floor.
            var points = intent.Tiles.SelectMany(t => new[]
            {
                Raw(t.X,t.Y,t.Z), Raw(t.X+t.Width,t.Y,t.Z),
                Raw(t.X,t.Y+t.Depth,t.Z), Raw(t.X+t.Width,t.Y+t.Depth,t.Z)
            }).Append(Raw(intent.SpawnX,intent.SpawnY,intent.SpawnZ)).ToArray();
            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            double top = 96, bottom = Math.Max(top + 1, SchematicCaptionTop - 20);
            double fit = Math.Max(0.000001, Math.Min((Bounds.Width - 80) / Math.Max(1,maxX-minX),
                (bottom-top) / Math.Max(1,maxY-minY)));
            return new(intent, fit, Bounds.Width/2 - (minX+maxX)*0.5*fit,
                (top+bottom)*0.5 - (minY+maxY)*0.5*fit);

            Point Raw(double x, double y, double z)
            {
                double dx = x-intent.FloorX-intent.FloorWidth/2.0, dy = y-intent.FloorY-intent.FloorDepth/2.0;
                return new((dx-dy)*0.866, (dx+dy)*0.5-(z-intent.FloorZ)*0.75);
            }
        }
        bool materialNotice = MaterialPreviewNotice.Length != 0;
        double w = intent.FloorWidth, d = intent.FloorDepth;
        // Reserve extra material caption space without changing material-absent pixels.
        double padding = materialNotice ? 172 : 150;
        double centerOffset = materialNotice ? 40 : 80;
        double scale = Math.Max(0.01, Math.Min((Bounds.Width - 80) / ((w + d) * 0.866),
            (Bounds.Height - padding) / ((w + d) * 0.5 + 512 * 0.75)));
        return new(intent, scale, Bounds.Width / 2, Bounds.Height / 2 + centerOffset);
    }

    private readonly record struct SchematicProjection(SurfaceView Intent, double Scale,
        double CenterX, double CenterY)
    {
        internal Point Project(double x, double y, double z)
        {
            double dx = x - Intent.FloorX - Intent.FloorWidth / 2.0;
            double dy = y - Intent.FloorY - Intent.FloorDepth / 2.0;
            return new Point(CenterX + (dx - dy) * 0.866 * Scale,
                CenterY + (dx + dy) * 0.5 * Scale - (z - Intent.FloorZ) * 0.75 * Scale);
        }
    }

    private static void Polygon(DrawingContext context, Point[] points, IBrush fill, Pen? pen)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext stream = geometry.Open())
        {
            stream.BeginFigure(points[0], true);
            foreach (Point point in points.Skip(1)) stream.LineTo(point);
            stream.EndFigure(true);
        }
        context.DrawGeometry(fill, pen, geometry);
    }

    private static void Text(DrawingContext context, string value, Point position, IBrush brush, double size) =>
        context.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, size, brush), position);
}
