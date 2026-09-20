using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentPreview
{
    private const string NativeDefaultDestinationNotice = "native default—resolved during source compilation";
    private bool HasFamilies => AggregateIntent?.Placements.Any(p => p is NativeFreshLockedChestFamilyPlacementIntent) == true;
    private double FamilyLegendHeight => HasFamilies ? 132 : 0;

    internal string FamilyPreviewNotice => HasFamilies
        ? "Families show XY anchors and explicit WORLD destinations; Z is numeric. Native defaults have no invented positions. This is not a trajectory or native model preview."
        : "";

    // The viewport is only a drawing boundary. Authored coordinates and projected
    // points remain exact even when they fall outside it; no camera-edge clamp.
    internal Rect FamilyMarkerViewport => new(8, 84, Math.Max(0, Bounds.Width - 16),
        Math.Max(0, SchematicCaptionTop - 92));

    internal sealed record FamilyPreviewMember(string Role, string Label, int? X, int? Y, int? Z,
        bool IsNativeDefault, bool IsOutsideSurface, Point? ProjectedPoint, bool IsOutsideView, string Description);
    internal sealed record FamilyPreview(string EditorId, string DefinitionId, int PlacementNumber, bool IsSelected,
        IReadOnlyList<FamilyPreviewMember> Members);
    internal sealed record FamilyMarkerLabel(string EditorId, string Role, string Text, Point Position,
        Rect MeasuredBounds, bool IsVisible);

    // Labels never move a marker or pretend a different WORLD coordinate. Hide
    // only later colliding text; every marker and complete legend row survives.
    internal IReadOnlyList<FamilyMarkerLabel> FamilyMarkerLabels
    {
        get
        {
            var labels = new List<FamilyMarkerLabel>();
            var occupied = new List<Rect>();
            foreach (var family in FamilyPreviews.Where(f => f.IsSelected))
            foreach (var member in family.Members)
            {
                if (member.ProjectedPoint is not { } point || member.IsOutsideView) continue;
                Point position = point + new Vector(8, -14);
                var text = new FormattedText(member.Label, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.White);
                var bounds = new Rect(position, new Size(text.Width, text.Height));
                bool visible = !occupied.Any(other => other.Intersects(bounds));
                if (visible) occupied.Add(bounds);
                labels.Add(new(family.EditorId, member.Role, member.Label, position, bounds, visible));
            }
            return labels.AsReadOnly();
        }
    }

    /// <summary>
    /// Complete neutral presentation of every authored family and all seven
    /// semantic members. A default has no coordinate/marker. No source lookup,
    /// native row identity, resource count or export/runtime authority is issued.
    /// </summary>
    internal IReadOnlyList<FamilyPreview> FamilyPreviews
    {
        get
        {
            if (AggregateIntent is not { } document || Presentation() is not { } surface || !HasFamilies)
                return Array.Empty<FamilyPreview>();
            var projection = CreateProjection(surface);
            var viewport = FamilyMarkerViewport;
            var result = new List<FamilyPreview>();
            for (int i = 0; i < document.Placements.Count; i++)
            {
                if (document.Placements[i] is not NativeFreshLockedChestFamilyPlacementIntent family) continue;
                var members = new List<FamilyPreviewMember>(7)
                {
                    Located("key", "Key", family.KeyX, family.KeyY, family.KeyZ),
                    Located("chest", "Chest", family.ChestX, family.ChestY, family.ChestZ)
                };
                for (int reward = 0; reward < 5; reward++)
                {
                    string role = $"reward.{reward + 1}", label = $"Reward{reward + 1}";
                    var destination = family.RewardDestinations?.Values.SingleOrDefault(d =>
                        d.Role == (NativeFreshChestRewardRole)reward);
                    members.Add(destination == null
                        ? new(role, label, null, null, null, true, false, null, false,
                            $"{label}: {NativeDefaultDestinationNotice}")
                        : Located(role, label, destination.X, destination.Y, destination.Z));
                }
                result.Add(new(family.EditorId, family.Source.DefinitionId, i + 1,
                    family.EditorId == SelectedEditorId, members.AsReadOnly()));
            }
            return result.AsReadOnly();

            FamilyPreviewMember Located(string role, string label, int x, int y, int z)
            {
                // Deliberately XY at the schematic plane. Z is numeric only,
                // exactly like the existing Wizard and single-row markers.
                Point point = projection.Project(x, y, surface.FloorZ);
                bool outsideSurface = !document.Content.ContainsInteriorXY(x, y);
                bool outsideView = viewport.Width < 14 || viewport.Height < 14 || !viewport.Deflate(7).Contains(point);
                string description = FormattableString.Invariant($"{label}: WORLD {x}, {y}, {z}");
                if (outsideSurface) description += "; outside authored surface (XY)";
                if (outsideView) description += "; outside view (not plotted)";
                return new(role, label, x, y, z, false, outsideSurface, point, outsideView, description);
            }
        }
    }

    private string FamilyPlacementCaption
    {
        get
        {
            var placements = AggregateIntent!.Placements;
            int routes = placements.OfType<NativeFreshWizardRouteIntent>().Count();
            int native = placements.OfType<NativeFreshMobyPlacementIntent>().Count();
            int families = placements.OfType<NativeFreshLockedChestFamilyPlacementIntent>().Count();
            return $"{routes} authored route{(routes == 1 ? "" : "s")} + " +
                $"{native} native marker{(native == 1 ? "" : "s")} + " +
                $"{families} authored famil{(families == 1 ? "y" : "ies")} (XY only)";
        }
    }

    private string FamilySceneCaption(SurfaceView surface)
    {
        var placements = AggregateIntent!.Placements;
        int routes = placements.OfType<NativeFreshWizardRouteIntent>().Count();
        int native = placements.OfType<NativeFreshMobyPlacementIntent>().Count();
        int families = placements.OfType<NativeFreshLockedChestFamilyPlacementIntent>().Count();
        return $"Heading {surface.Heading} · track {surface.MusicTrackId:00} · " +
            $"{routes} route{(routes == 1 ? "" : "s")} · {native} native · " +
            $"{families} famil{(families == 1 ? "y" : "ies")} (XY)";
    }

    private void RenderFamilies(DrawingContext context)
    {
        var families = FamilyPreviews;
        if (families.Count == 0) return;
        using (context.PushClip(FamilyMarkerViewport))
        {
            foreach (var family in families)
            foreach (var member in family.Members)
            {
                if (member.ProjectedPoint is not { } point || member.IsOutsideView) continue;
                IBrush color = member.Role switch { "key" => Brushes.Gold, "chest" => Brushes.MediumPurple, _ => Brushes.LimeGreen };
                var edge = new Pen(family.IsSelected ? Brushes.Yellow : Brushes.White, family.IsSelected ? 2 : 1);
                double radius = family.IsSelected ? 6 : 4;
                if (member.Role == "chest")
                    context.DrawRectangle(color, edge, new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
                else context.DrawEllipse(color, edge, point, radius, radius);
            }
            foreach (var label in FamilyMarkerLabels.Where(label => label.IsVisible))
                Text(context, label.Text, label.Position, Brushes.White, 10);
        }

        // One parent remains the selectable authored entry. The selected family
        // (or explicitly identified first family) supplies a complete seven-row
        // legend; all other families retain markers and inspectable descriptions.
        var shown = families.FirstOrDefault(f => f.IsSelected) ?? families[0];
        var legend = new Rect(0, SchematicCaptionTop, Bounds.Width, FamilyLegendHeight);
        using (context.PushClip(legend))
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(230, 18, 30, 45)), legend);
            string heading = $"Family placement {shown.PlacementNumber} · seven members · " +
                (shown.IsSelected ? "selected parent" : "first family; select a family to inspect");
            FamilyText(context, heading, new Point(12, legend.Top + 5), Brushes.White, 11);
            for (int i = 0; i < shown.Members.Count; i++)
            {
                var member = shown.Members[i];
                string row = member.IsNativeDefault ? member.Description :
                    FormattableString.Invariant($"{member.Label}: WORLD {member.X}, {member.Y}, {member.Z}");
                // Keep callouts and exact coordinates on the same visible row.
                // Full unabridged meanings are also retained in Description.
                if (member.IsOutsideSurface) row += " [off-surface]";
                if (member.IsOutsideView) row += " [outside view]";
                FamilyText(context, row, new Point(12, legend.Top + 24 + i * 15),
                    member.IsOutsideView || member.IsOutsideSurface ? Brushes.Orange : Brushes.White, 10.5);
            }
        }
    }

    private void FamilyText(DrawingContext context, string value, Point position, IBrush brush, double size)
    {
        var text = Format(size);
        double available = Math.Max(1, Bounds.Width - 24);
        if (text.Width > available) text = Format(Math.Max(8, size * available / text.Width));
        context.DrawText(text, position);

        FormattedText Format(double fontSize) => new(value, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, fontSize, brush);
    }
}
