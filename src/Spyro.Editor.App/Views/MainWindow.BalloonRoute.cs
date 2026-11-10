using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ › World's Balloon Route…: where the project's new world lies on the balloonists' route (after which of
/// the game's homeworlds, or off the route), what the balloonist before it asks before he flies Spyro there, what the
/// world's own balloonist asks before he flies Spyro on, and that balloonist's name. Create BIN builds it into every
/// homeworld (<see cref="NativeBalloonTravel"/>).</summary>
public sealed partial class MainWindow
{
    /// <summary>Answers the Balloon Route dialog in tests (null: cancel).</summary>
    internal Func<WorldBalloonPlace?>? BalloonRouteChoiceForTesting { get; set; }

    private static readonly string[] BalloonRequirementNames = ["Nothing", "Dragons rescued", "Treasure found", "Dragon eggs recovered"];

    /// <summary>True when a balloonist stands among <paramref name="objects"/> (the game's six kinds).</summary>
    private static bool HasBalloonist(IEnumerable<Spyro.Editor.Core.Scene.Moby> objects) =>
        objects.Any(m => !m.IsRemoved && m.SourceByte37 == 0 && m.SourceByte36 is >= UniversalBalloonistRows.BalloonistClass and <= UniversalBalloonistRows.BalloonistClass + 5);

    /// <summary>"Artisans'", "Gnasty's World's", "SKY ISLES'": a name's possessive.</summary>
    private static string Owned(string name) => name.EndsWith('s') || name.EndsWith('S') ? name + "'" : name + "'s";

    private string BalloonRouteSentence(NewWorldDocument world, WorldBalloonPlace place)
    {
        if (!place.OnRoute) return $"{world.Name} is off the balloon route: Spyro reaches it through its doorway only.";
        var route = place.Route()!;
        int[] order = NativeBalloonTravel.Order(route);
        string Name(int id) => id == NewWorldDocument.Homeworld ? world.Name : _retailCatalog.Levels.FirstOrDefault(l => l.LevelId == id)?.DisplayName ?? id.ToString();
        int q = Array.IndexOf(order, NewWorldDocument.Homeworld);
        string before = Name(order[q - 1]);
        string there = route.ToWorld.Kind == BalloonRequirementKind.Nothing
            ? $"{Owned(before)} balloonist flies Spyro to {world.Name} for the asking"
            : $"{Owned(before)} balloonist flies Spyro to {world.Name} once Spyro has managed to {route.ToWorld.Describe()}";
        if (q == order.Length - 1) return $"{Owned(before)} balloonist opens {world.Name} with every other world; it is the last on the balloon route.";
        return there + (route.Onward.Kind == BalloonRequirementKind.Nothing
            ? $", and {place.Balloonist} flies him on to {Name(order[q + 1])} for the asking."
            : $", and {place.Balloonist} flies him on to {Name(order[q + 1])} once Spyro has managed to {route.Onward.Describe()}.");
    }

    private async Task BalloonRouteAsync()
    {
        if (_createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy) return;
        string root = _workspace.RootPath;
        NewWorldDocument? world;
        try { world = string.IsNullOrEmpty(root) ? null : NewWorldDocument.Read(root); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { _statusText.Text = "The project's world could not be read: " + e.Message; return; }
        if (world == null) { _statusText.Text = "Make a world first with File ▾ › New World…: the balloon route is the route to it."; return; }
        bool inHomeworld = _freshSceneDocument?.WorldLevelId == NewWorldDocument.Homeworld;
        var current = world.BalloonPlaceWith(inHomeworld && HasBalloonist(_currentMobys));
        var choice = BalloonRouteChoiceForTesting != null ? BalloonRouteChoiceForTesting() : await ShowBalloonRouteDialogAsync(world, current);
        if (choice == null || _workspace.RootPath != root) return;
        if (choice.Refusal() is { } why) { _statusText.Text = "The balloon route was not changed: " + why; return; }
        try { await (world with { Balloon = choice }).SaveAsync(root); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { _statusText.Text = "Could not save the balloon route: " + e.Message; return; }
        string note = "";
        if (choice.OnRoute && choice.After != "gnastysworld" && inHomeworld && !HasBalloonist(_currentMobys))
            note = $" {Owned(world.Name)} homeworld needs a balloonist and his balloon for that: Objects › Random objects places them (\"The way out\"), or Add Object….";
        else if (choice.OnRoute && choice.After != "gnastysworld" && !inHomeworld)
            note = $" {Owned(world.Name)} homeworld must have a balloonist and his balloon for that (Objects › Random objects places them there).";
        _statusText.Text = BalloonRouteSentence(world, choice) + note + " Create BIN builds the route into every homeworld.";
    }

    private async Task<WorldBalloonPlace?> ShowBalloonRouteDialogAsync(NewWorldDocument world, WorldBalloonPlace current)
    {
        var homes = NewWorldDocument.HomeworldKeys.Select(k => _retailCatalog.FindByKey(k)).OfType<LevelDefinition>().ToList();
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        content.Children.Add(new TextBlock { Text = $"{world.Name} on the balloon route", FontSize = 20, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, LineHeight = 20,
            Text = "The game's balloonists fly Spyro from world to world in a fixed order, each once Spyro has done what he asks. " +
                   "Your world takes a place in that order. The balloonist before it then flies Spyro to your world, and your world's own " +
                   "balloonist flies him on to the world that used to come next."
        });
        var after = new ComboBox
        {
            Name = "BalloonRouteAfter", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = homes.Select((h, i) => i == homes.Count - 1 ? $"After {h.DisplayName} (the last world)" : $"After {h.DisplayName}, before {homes[i + 1].DisplayName}")
                .Append("Not on the balloon route").ToArray(),
            SelectedIndex = current.OnRoute ? Math.Max(0, homes.FindIndex(h => h.Key == current.After)) : homes.Count
        };
        content.Children.Add(new TextBlock { Text = "Your world comes" });
        content.Children.Add(after);
        (ComboBox Kind, NumericUpDown Count, Control Row) Price(string name, BalloonRequirement value)
        {
            var kind = new ComboBox { Name = name + "Kind", ItemsSource = BalloonRequirementNames, SelectedIndex = (int)value.Kind, HorizontalAlignment = HorizontalAlignment.Stretch };
            var count = new NumericUpDown { Name = name + "Count", Minimum = 1, Maximum = BalloonRequirement.MostTreasure, Increment = 1, Value = Math.Max(1, value.Count), FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch, ClipValueToMinMax = true };
            var row = new Grid { Name = name + "Row", ColumnDefinitions = new ColumnDefinitions("*,12,150") };
            Grid.SetColumn(count, 2);
            row.Children.Add(kind); row.Children.Add(count);
            return (kind, count, row);
        }
        var toLabel = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var (toKind, toCount, toRow) = Price("BalloonRouteToWorld", current.ToWorld);
        var onLabel = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var (onKind, onCount, onRow) = Price("BalloonRouteOnward", current.Onward);
        content.Children.Add(toLabel); content.Children.Add(toRow); content.Children.Add(onLabel); content.Children.Add(onRow);
        var name = new TextBox { Name = "BalloonRouteBalloonist", Text = current.Balloonist, MaxLength = WorldBalloonPlace.MaxBalloonistName, Watermark = "NAME" };
        var nameLabel = new TextBlock { Text = "Your world's balloonist is called (his talk is headed \"… THE BALLOONIST\")" };
        content.Children.Add(nameLabel); content.Children.Add(name);
        var note = new TextBlock { Name = "BalloonRouteNote", TextWrapping = TextWrapping.Wrap, LineHeight = 19, Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 112)) };
        var problem = new TextBlock { Name = "BalloonRouteProblem", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(168, 98, 0)) };
        content.Children.Add(note); content.Children.Add(problem);
        Button cancel = NewButton("Cancel"), save = NewButton("Save Route");
        cancel.Name = "BalloonRouteCancel"; save.Name = "BalloonRouteSave";
        var dialog = new Window
        {
            Name = "BalloonRouteDialog", Title = "World's Balloon Route", Width = 560, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content
        };
        BalloonRequirement Read(ComboBox kind, NumericUpDown count) =>
            kind.SelectedIndex <= 0 ? BalloonRequirement.Nothing : new((BalloonRequirementKind)kind.SelectedIndex, (int)(count.Value ?? 1));
        WorldBalloonPlace Chosen()
        {
            int i = after.SelectedIndex;
            string who = (name.Text ?? "").Trim().ToUpperInvariant();
            if (i < 0 || i >= homes.Count) return new(WorldBalloonPlace.Off, BalloonRequirement.Nothing, BalloonRequirement.Nothing, who.Length > 0 ? who : WorldBalloonPlace.DefaultBalloonist);
            bool last = i == homes.Count - 1;
            return new(homes[i].Key, last ? BalloonRequirement.Nothing : Read(toKind, toCount), last ? BalloonRequirement.Nothing : Read(onKind, onCount), who);
        }
        int shown = -2;
        void Refresh()
        {
            int i = after.SelectedIndex;
            bool on = i >= 0 && i < homes.Count, last = i == homes.Count - 1;
            // A new place starts from the game's own pace: free to your world, and the game's price to fly on.
            if (i != shown && shown != -2 && on && !last)
            {
                toKind.SelectedIndex = 0;
                var price = WorldBalloonPlace.GamePrice(homes[i].Key);
                onKind.SelectedIndex = (int)price.Kind; onCount.Value = Math.Max(1, price.Count);
            }
            shown = i;
            foreach (var c in new Control[] { toLabel, toRow, onLabel, onRow }) c.IsVisible = on && !last;
            nameLabel.IsVisible = name.IsVisible = on;
            toCount.IsVisible = toKind.SelectedIndex > 0; onCount.IsVisible = onKind.SelectedIndex > 0;
            foreach (var (kind, count) in new[] { (toKind, toCount), (onKind, onCount) })
            {
                count.Maximum = kind.SelectedIndex switch { 1 => BalloonRequirement.MostDragons, 3 => BalloonRequirement.MostEggs, _ => BalloonRequirement.MostTreasure };
                if (count.Value > count.Maximum) count.Value = count.Maximum;
            }
            if (on && !last)
            {
                var gameAsks = WorldBalloonPlace.GamePrice(homes[i].Key);
                toLabel.Text = $"To fly Spyro to {world.Name}, {Owned(homes[i].DisplayName)} balloonist asks for (in the whole game)";
                onLabel.Text = $"To fly Spyro on to {homes[i + 1].DisplayName}, {Owned(world.Name)} balloonist asks for (the game asks there to {gameAsks.Describe()})";
            }
            var place = Chosen();
            string? why = place.Refusal();
            problem.Text = why ?? "";
            save.IsEnabled = why == null;
            note.Text = !on
                ? "The balloonists will not know your world. Spyro reaches it through its doorway (Terrain › World Entrance) and leaves the same way."
                : last
                    ? $"Gnasty's World's balloonist opens {world.Name} with everything else, and asks for nothing (he has no room to). A balloonist in your world then offers every world; without one, Spyro leaves through its doorway."
                    : why == null
                        ? BalloonRouteSentence(world, place) + $" {Owned(world.Name)} homeworld needs a balloonist and his balloon (Objects › Random objects places them)." +
                          (homes[i].Key == "magiccrafters" && place.ToWorld != WorldBalloonPlace.GamePrice("magiccrafters") ? " Magic Crafters' balloonist then no longer hints where egg thieves hide: his lines are his new ones." : "") +
                          " Dragons and treasure of your world's levels count too; dragon eggs are the twelve the game's thieves carry."
                        : "";
        }
        after.SelectionChanged += (_, _) => Refresh();
        toKind.SelectionChanged += (_, _) => Refresh(); onKind.SelectionChanged += (_, _) => Refresh();
        toCount.ValueChanged += (_, _) => Refresh(); onCount.ValueChanged += (_, _) => Refresh();
        name.TextChanged += (_, _) => Refresh();
        Refresh();
        cancel.Click += (_, _) => dialog.Close(false); save.Click += (_, _) => dialog.Close(true);
        StyleModernPrimaryButton(save, ModernBlue, 132);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(cancel); actions.Children.Add(save); content.Children.Add(actions);
        if (!await dialog.ShowDialog<bool>(this)) return null;
        return Chosen();
    }
}
