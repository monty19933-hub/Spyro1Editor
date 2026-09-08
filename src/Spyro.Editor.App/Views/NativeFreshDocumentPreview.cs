using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.App.Views;

/// <summary>Bounded authored-coordinate diagram, not native rendering or collision evidence.</summary>
internal sealed class NativeFreshDocumentPreview : Control
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

    internal void SetAggregate(NativeFreshLevelDocumentIntent? aggregate, string? selectedEditorId)
    {
        AggregateIntent = aggregate;
        _aggregatePresentation = true;
        SelectedEditorId = selectedEditorId;
        _intent = aggregate?.Base;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        var intent = Intent;
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
        double w = intent.FloorWidth, d = intent.FloorDepth;
        double scale = Math.Max(0.01, Math.Min((bounds.Width - 80) / ((w + d) * 0.866),
            (bounds.Height - 150) / ((w + d) * 0.5 + NativeId65FreshDocumentIntent.SpawnHeightAboveFloor * 0.75)));
        Point Project(double x, double y, double z)
        {
            double dx = x - intent.FloorX - w / 2;
            double dy = y - intent.FloorY - d / 2;
            return new Point(bounds.Width / 2 + (dx - dy) * 0.866 * scale,
                bounds.Height / 2 + 80 + (dx + dy) * 0.5 * scale - (z - intent.FloorZ) * 0.75 * scale);
        }
        Point a = Project(intent.FloorX, intent.FloorY, intent.FloorZ);
        Point b = Project(intent.FloorX + w, intent.FloorY, intent.FloorZ);
        Point c = Project(intent.FloorX, intent.FloorY + d, intent.FloorZ);
        Point e = Project(intent.FloorX + w, intent.FloorY + d, intent.FloorZ);
        IBrush floor = new SolidColorBrush(Color.FromRgb(intent.FloorColor.R, intent.FloorColor.G, intent.FloorColor.B));
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)), 2);
        Polygon(context, [a, b, c], floor, edge);
        Polygon(context, [b, e, c], floor, edge);
        Point foot = Project(intent.SpawnX, intent.SpawnY, intent.FloorZ);
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
            // Display every authored route in XY at the floor projection. Exact Z
            // remains numeric; this is not a native trajectory/height simulation.
            for (int i = 0; i < aggregate.Placements.Count; i++)
            {
                var route = (NativeFreshWizardRouteIntent)aggregate.Placements[i];
                bool selected = route.EditorId == SelectedEditorId;
                Point origin = Project(route.OriginX, route.OriginY, intent.FloorZ);
                Point end = Project(route.EndX, route.EndY, intent.FloorZ);
                IBrush ink = selected ? Brushes.Yellow : Brushes.Cyan;
                context.DrawLine(new Pen(ink, selected ? 3 : 1.5, new DashStyle([4, 3], 0)), origin, end);
                context.DrawEllipse(ink, new Pen(Brushes.DarkSlateGray, 1), origin, selected ? 6 : 4, selected ? 6 : 4);
                context.DrawEllipse(Brushes.Transparent, new Pen(ink, selected ? 3 : 1.5), end, 4, 4);
                if (selected) Text(context, $"Route {i + 1} · authored Z {route.OriginZ}", origin + new Vector(9, -20), Brushes.White, 12);
            }
        }

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)), new Rect(0, 0, bounds.Width, 76));
        Text(context, $"{intent.FloorWidth} × {intent.FloorDepth}  ·  floor Z {intent.FloorZ}", new Point(16, 12), Brushes.White, 17);
        Text(context, $"Origin {intent.FloorX}, {intent.FloorY}  ·  spawn {intent.SpawnX}, {intent.SpawnY}, {intent.SpawnZ}",
            new Point(16, 40), Brushes.White, 12);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 18, 30, 45)), new Rect(0, bounds.Height - 34, bounds.Width, 34));
        string placementCaption = AggregateIntent == null ? "0 placed Mobys" : $"{AggregateIntent.Placements.Count} authored routes (XY only)";
        Text(context, $"Heading {intent.Heading}/255 (schematic)  ·  music track {intent.MusicTrackId:00}  ·  {placementCaption}",
            new Point(16, bounds.Height - 26), Brushes.White, 12);
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
