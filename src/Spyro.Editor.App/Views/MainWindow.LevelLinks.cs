using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ › Level Links… (and Open Level Links… in Game Settings › Portals &amp; Return Home): every homeworld's
/// doorways and where each leads, every level's Return Home and whether it has its vortex, the project's own world, and
/// what is wrong, in one window (<see cref="LevelLinks"/>). A doorway's "leads to" and a level's Return Home are saved at
/// once to the project's portal routes (the same file and checks as the Game Settings controls); Undo Last Change takes
/// the last change back, and Create BIN puts the links in the game.</summary>
public sealed partial class MainWindow
{
    /// <summary>The Level Links window while it is open (tests drive it).</summary>
    internal Window? LevelLinksWindowForTesting { get; private set; }

    private Task LevelLinksAsync() => ShowLevelLinksAsync();

    /// <summary>Opens Level Links over the editor and returns when it is closed. <paramref name="focusLevelId"/>: a level to
    /// show first (a level just made: its doorway and its Return Home are marked and scrolled to).</summary>
    internal async Task ShowLevelLinksAsync(int? focusLevelId = null)
    {
        if (_createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy)
        {
            _statusText.Text = "Level Links opens once the editor has finished what it is doing.";
            return;
        }
        if (LevelLinksWindowForTesting != null) { LevelLinksWindowForTesting.Activate(); return; }
        string root = _workspace.RootPath;
        if (string.IsNullOrEmpty(root)) return;

        var content = new StackPanel { Name = "LevelLinksContent", Margin = new Thickness(20, 16, 20, 8), Spacing = 10 };
        var scroll = new ScrollViewer { Name = "LevelLinksScroll", Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var status = new TextBlock { Name = "LevelLinksStatus", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk) };
        var undo = NewButton("Undo Last Change"); undo.Name = "LevelLinksUndo";
        var close = NewButton("Close"); close.Name = "LevelLinksClose";
        StyleModernPrimaryButton(close, ModernBlue, 96);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(undo); actions.Children.Add(close);
        var footer = new StackPanel { Margin = new Thickness(20, 8, 20, 14), Spacing = 6 };
        footer.Children.Add(status); footer.Children.Add(actions);
        var footerFrame = new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        var body = new DockPanel();
        DockPanel.SetDock(footerFrame, Dock.Bottom);
        body.Children.Add(footerFrame);
        body.Children.Add(scroll);
        var dialog = new Window
        {
            Name = "LevelLinksDialog", Title = "Level Links", Width = 860, MinWidth = 560, MinHeight = 360,
            SizeToContent = SizeToContent.Height, CanResize = true, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = body
        };
        // No taller than the free part of the screen: the links scroll, Undo and Close stay at the foot.
        if (ScreenFit.MaxHeightOver(this) is { } tallest) dialog.MaxHeight = tallest;

        LevelLinks? links = null;
        PortalRouteSnapshot? shown = null;
        Func<Task>? afterClose = null;
        bool rendering = false, rendered = false;

        void Say(string text) { status.Text = text; _statusText.Text = text; }
        // As the Game Settings route controls, except that no level has to be open: the window is about the whole game.
        bool CanEdit() => shown != null && !_startBlankBusy && !FreeFlightOperationBusy();
        void RefreshUndo() => undo.IsEnabled = CanEdit() && _portalRouteUndo is { } u && u.Workspace == root && u.AfterFingerprint == shown!.Fingerprint;

        // A change to the saved routes, checked against the routes the window shows (a change made elsewhere since then
        // stops it), kept for Undo, and shown at once.
        void Change(Func<PortalRouteDocument, PortalRouteDocument> change, string done)
        {
            // Refused: the lists go back to what is saved.
            if (!CanEdit() || shown == null) { Say("The links can't be changed while the editor is busy."); Dispatcher.UIThread.Post(Render); return; }
            try
            {
                var before = shown;
                var after = PortalRouteStore.Save(root, change(before.Document), before);
                _portalRouteUndo = new(root, before.Document, after.Fingerprint);
                InvalidateBuildSafetySummary(); RefreshPortalRoutingEditor();
                Say(done + " Saved in your project; Create BIN puts it in the game.");
            }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or UnauthorizedAccessException)
            { Say("Not changed: " + Plain(e)); }
            Dispatcher.UIThread.Post(Render);
        }
        static string Plain(Exception e) => e is System.Text.Json.JsonException
            ? $"the project's saved doorway links ({PortalRouteStore.FileName}) can't be read ({e.Message})."
            : e.Message;

        void Open(Func<Task> action) { afterClose = action; dialog.Close(); }

        TextBlock Text(string text, double size = 13, IBrush? brush = null, FontWeight weight = FontWeight.Normal) =>
            new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = weight, Foreground = brush ?? new SolidColorBrush(ModernInk) };
        IBrush Amber() => new SolidColorBrush(Color.FromRgb(168, 98, 0));
        IBrush Good() => new SolidColorBrush(Color.FromRgb(46, 125, 72));
        IBrush Muted() => new SolidColorBrush(ModernMutedInk);
        Border Card(Control child, string? name = null, bool marked = false) => new()
        {
            Name = name, Child = child, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(marked ? 2 : 1),
            BorderBrush = new SolidColorBrush(marked ? ModernBlue : ModernLine), Background = new SolidColorBrush(marked ? Color.FromRgb(234, 243, 252) : ModernSurface)
        };
        Grid Row(string label, Control value, string? note, IBrush? noteBrush, out TextBlock labelBlock)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("210,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 2) };
            labelBlock = Text(label); labelBlock.VerticalAlignment = VerticalAlignment.Center; labelBlock.Margin = new Thickness(0, 0, 10, 0);
            grid.Children.Add(labelBlock);
            Grid.SetColumn(value, 1); grid.Children.Add(value);
            if (!string.IsNullOrEmpty(note))
            {
                var n = Text(note, 12, noteBrush ?? Muted()); n.Margin = new Thickness(0, 3, 0, 4);
                Grid.SetColumn(n, 1); Grid.SetRow(n, 1); grid.Children.Add(n);
            }
            return grid;
        }

        void Render()
        {
            if (rendering) return;
            rendering = true;
            var offset = scroll.Offset;
            Control? focusTarget = null;
            try
            {
                content.Children.Clear();
                content.Children.Add(Text("Level links", 20, weight: FontWeight.SemiBold));
                content.Children.Add(Text("How Spyro gets into each level and back out again. Each doorway in a homeworld takes Spyro into the level it leads to. " +
                    "Leaving a level by its Return Home vortex (or Exit Level in the pause menu) brings him back out in front of a doorway. " +
                    "Choose below; every change is kept in your project at once and Create BIN puts it in the game.", 13, Muted()));
                LevelLinksMap map;
                try
                {
                    shown = PortalRouteStore.Capture(root);
                    links = ReadLevelLinks(root);
                    map = links.Map(shown.Document, IsLeftOut(ExportParts.Doorways));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or UnauthorizedAccessException)
                {
                    shown = null;
                    content.Children.Add(Text("The links could not be read, so nothing can be changed here: " + Plain(e) +
                        " Your saved files were left as they are.", 13, Amber()));
                    RefreshUndo();
                    return;
                }
                bool editable = CanEdit();

                // What the window was opened for (a level just made).
                if (focusLevelId is int focus)
                    content.Children.Add(Card(Text(FocusSentence(map, links, focus)), "LevelLinksFocus", true));

                // Problems first, in plain words, with a button where one change puts it right.
                var problems = new StackPanel { Spacing = 6 };
                if (map.Problems.Count == 0)
                    problems.Children.Add(Text("✓ Every level has a doorway leading to it, and Return Home from each brings Spyro back out of the doorway he went in by.", 13, Good()));
                foreach (var problem in map.Problems)
                {
                    var line = new StackPanel { Spacing = 4 };
                    line.Children.Add(Text((problem.StopsCreateBin ? "⛔ " : "• ") + problem.Text, 13, problem.StopsCreateBin ? new SolidColorBrush(ModernRed) : Amber()));
                    if (problem.Fix is { } fix && problem.FixLabel is { } label)
                    {
                        var button = NewButton(label, () => Change(fix, "Done: " + label + "."));
                        button.Name = "LevelLinksFix" + problems.Children.Count; button.IsEnabled = editable; button.HorizontalAlignment = HorizontalAlignment.Left;
                        line.Children.Add(button);
                    }
                    problems.Children.Add(line);
                }
                content.Children.Add(Card(problems, "LevelLinksProblems"));

                // Doorways: each homeworld's, with where it leads.
                content.Children.Add(Text("Doorways: where each one leads", 15, weight: FontWeight.SemiBold));
                foreach (var home in map.Homeworlds)
                {
                    var panel = new StackPanel { Spacing = 4 };
                    var head = new DockPanel();
                    var openHome = NewButton(home.Rebuilt ? $"Open {home.RebuiltAs}" : $"Open {home.Name}");
                    openHome.Name = "LevelLinksOpen" + home.LevelId; openHome.Margin = new Thickness(8, 0, 0, 0); openHome.MinHeight = 28; openHome.Padding = new Thickness(10, 3);
                    var openTarget = _retailCatalog.FindByKey(home.Key);
                    openHome.Click += (_, _) => { if (openTarget != null) Open(() => SelectLevelFromPickerAsync(openTarget)); };
                    DockPanel.SetDock(openHome, Dock.Right);
                    head.Children.Add(openHome);
                    head.Children.Add(Text(home.Rebuilt ? $"{home.Name} — rebuilt as {home.RebuiltAs}" : home.Name, 14, weight: FontWeight.SemiBold));
                    panel.Children.Add(head);
                    if (home.Rebuilt)
                        panel.Children.Add(Text($"A homeworld you rebuilt keeps one doorway for each level of its own, and each always leads to the level in that place " +
                            $"(a level you build in Stone Hill's place is reached by the Stone Hill doorway). Add, move or remove them with Links › Doorways in {home.RebuiltAs}.", 12, Muted()));
                    if (home.Doorways.Count == 0)
                        panel.Children.Add(Text("No doorways yet.", 12, Amber()));
                    foreach (var doorway in home.Doorways)
                    {
                        var choices = map.Destinations;
                        var box = new ComboBox
                        {
                            Name = "LevelLinksDoorway" + doorway.OriginalLevelId, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32,
                            ItemsSource = choices, SelectedItem = choices.FirstOrDefault(c => c.LevelId == doorway.LeadsTo),
                            IsEnabled = editable && doorway.Routable,
                        };
                        // Said only where the doorway differs from the game's: where it led, and whether Spyro comes back out of it.
                        var routeOf = map.ReturnOf(doorway.LeadsTo);
                        IBrush? brush = null;
                        List<string> parts = [];
                        if (doorway.Changed) parts.Add($"In the game it leads to {links.PlaceOf(doorway.OriginalLevelId).SlotName}.");
                        if (routeOf != null)
                        {
                            if (routeOf.Effective != doorway.Portal) { parts.Add($"Return Home from {routeOf.Level.Name} comes back out of another doorway."); brush = Amber(); }
                            else if (doorway.Changed) { parts.Add("Return Home comes back out here."); brush = Good(); }
                        }
                        string note = string.Join(" ", parts);
                        bool marked = focusLevelId == doorway.LeadsTo;
                        var row = Row(doorway.DoorwayName + " →", box, note, brush, out var label);
                        if (marked) { label.FontWeight = FontWeight.SemiBold; label.Foreground = new SolidColorBrush(ModernBlue); focusTarget ??= row; }
                        var portal = doorway.Portal;
                        box.SelectionChanged += (_, _) =>
                        {
                            if (rendering || box.SelectedItem is not LevelLinkPlace to || to.LevelId == doorway.LeadsTo) return;
                            Change(r => links!.Send(r, portal, to.LevelId), $"{LevelLinks.Possessive(home.Name)} {doorway.DoorwayName} now leads to {to.Name}" +
                                (LevelLinks.HasReturnHome(to.LevelId) ? ", and Return Home from it comes back out of that doorway." : "."));
                        };
                        panel.Children.Add(row);
                    }
                    if (map.World is { } w)
                    {
                        // Your world by balloon: the balloonist of the homeworld before it on the route flies Spyro there.
                        if (w.Place.OnRoute && (NewWorldDocument.HomeworldKeys.ToList().IndexOf(w.Place.After) + 1) * 10 == home.LevelId)
                            panel.Children.Add(Text(w.Place.After == "gnastysworld"
                                ? $"Its balloonist opens your world {w.Name} with every other world, at the end of the balloon route (see Your world below)."
                                : $"Its balloonist flies Spyro to your world {w.Name} (see Your world below).", 12, Muted()));
                        // A doorway into your world, only when you chose one (or the world is off the balloon route).
                        if (!w.BalloonOnly && string.Equals(w.EntranceHomeworldKey ?? w.EntryHomeworldKey, home.Key, StringComparison.OrdinalIgnoreCase))
                            panel.Children.Add(Text(w.EntranceHomeworldKey == null
                                ? $"The doorway into your world {w.Name} you chose goes here too: Create BIN places it near where Spyro arrives (or place it yourself with Links › World Entrance)."
                                : $"The doorway into your world {w.Name} you chose stands here (Links › World Entrance moves it).", 12, Muted()));
                    }
                    content.Children.Add(Card(panel, "LevelLinksHome" + home.LevelId, focusLevelId == home.LevelId));
                    if (focusLevelId == home.LevelId) focusTarget ??= content.Children[^1];
                }

                // Return Home: each level's way back.
                content.Children.Add(Text("Return Home: where Spyro comes back out", 15, weight: FontWeight.SemiBold));
                content.Children.Add(Text("Return Home is the vortex at a level's end, and Exit Level in the pause menu. \"Automatic\" is the doorway that leads to the level.", 12, Muted()));
                var returnsPanel = new StackPanel { Spacing = 2 };
                var routable = map.Doorways.Where(d => d.Routable).ToList();
                string Phrase(PortalEndpoint p) { var d = p.Resolve(); var h = map.Homeworlds.FirstOrDefault(x => x.Key == d.HomeworldKey); return $"{LevelLinks.Possessive(h?.Name ?? d.HomeworldKey)} {d.DestinationName} doorway"; }
                foreach (var back in map.Returns)
                {
                    int id = back.Level.LevelId;
                    var items = new List<ReturnPortalChoice>
                    {
                        new(null, back.Automatic is { } a ? $"Automatic: the doorway that leads here ({Phrase(a)})"
                            : back.Effective is { } e ? $"Automatic: no doorway leads here yet ({Phrase(e)} for now)" : "Automatic: no doorway leads here yet")
                    };
                    foreach (var d in routable)
                        items.Add(new(d.Portal, $"{d.HomeworldName} — {d.DoorwayName}" + (d.LeadsTo == id ? "" : $" (leads to {links.PlaceOf(d.LeadsTo).Name})")));
                    if (!back.IsAutomatic && back.Effective is { } eff && items.All(i => i.Endpoint != eff))
                        items.Add(new(eff, Phrase(eff) + " (in a rebuilt homeworld: can't be used)"));
                    var box = new ComboBox
                    {
                        Name = "LevelLinksReturn" + id, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32, ItemsSource = items,
                        SelectedItem = back.IsAutomatic ? items[0] : items.FirstOrDefault(i => i.Endpoint == back.Effective) ?? items[0],
                        IsEnabled = editable,
                    };
                    var (vortexText, vortexBrush) = back.Vortex switch
                    {
                        { Present: true } => ("Return Home vortex: yes.", (IBrush?)null),
                        { Present: false, Expected: true } => ("Return Home vortex: missing — Spyro can leave only by Exit Level.", Amber()),
                        { Present: false } when back.Level.Built => ("Return Home vortex: none — Spyro can leave only by Exit Level.", Amber()),
                        { Present: false } => ("Return Home vortex: none (the game's own level has none either).", (IBrush?)null),
                        _ => ("Return Home vortex: not known until the level has been opened once.", (IBrush?)null),
                    };
                    var row = Row(back.Level.Label, box, vortexText, vortexBrush, out var label);
                    if (focusLevelId == id) { label.FontWeight = FontWeight.SemiBold; label.Foreground = new SolidColorBrush(ModernBlue); focusTarget ??= row; }
                    box.SelectionChanged += (_, _) =>
                    {
                        if (rendering || box.SelectedItem is not ReturnPortalChoice choice) return;
                        if (choice.Endpoint == null ? back.IsAutomatic : choice.Endpoint == back.Effective && !back.IsAutomatic) return;
                        Change(r => links!.SetReturn(r, id, choice.Endpoint), choice.Endpoint == null
                            ? $"Return Home from {back.Level.Name} now comes back out of the doorway that leads to it."
                            : $"Return Home from {back.Level.Name} now comes back out of {Phrase(choice.Endpoint)}.");
                    };
                    returnsPanel.Children.Add(row);
                }
                content.Children.Add(Card(returnsPanel, "LevelLinksReturns"));

                // The project's own world: its links come from the world's own tools.
                if (map.World is { } world)
                {
                    var panel = new StackPanel { Spacing = 5 };
                    panel.Children.Add(Text("Your world: " + world.Name, 14, weight: FontWeight.SemiBold));
                    // How players get there and back, in one sentence (the same words as the balloon route's window).
                    var access = Text(world.Access); access.Name = "LevelLinksWorldAccess";
                    panel.Children.Add(access);
                    if (world.Place.OnRoute)
                        panel.Children.Add(Text(world.HasBalloonist
                            ? $"{LevelLinks.Possessive(world.HomeworldName)} balloonist stands in {world.HomeworldName}, with his balloon."
                            : world.BalloonOnly
                                ? $"{world.HomeworldName} has no balloonist yet, so Spyro could not fly back: Create BIN stops on it, and Fix It For Me stands one near where Spyro arrives."
                                : $"{world.HomeworldName} has no balloonist: Spyro leaves through its doorway back.", 13, world.HasBalloonist ? null : world.BalloonOnly ? Amber() : Muted()));
                    panel.Children.Add(Text(world.Levels.Count == 0 ? $"{world.HomeworldName} has no levels yet."
                        : $"{LevelLinks.Possessive(world.HomeworldName)} doorways lead to: " + string.Join(", ", world.Levels.Select(l => l.Name + (l.HasDoorway ? "" : " (no doorway yet)"))) +
                          $". Return Home from each of them brings Spyro back out of its doorway in {world.HomeworldName}."));
                    if (world.BalloonOnly)
                    {
                        // No doorway in, none out: say so where one was left from before.
                        if (world.EntranceHomeworldKey is { } placedIn)
                            panel.Children.Add(Text($"A doorway into {world.Name} that was placed in {_retailCatalog.FindByKey(placedIn)?.DisplayName ?? placedIn} before is kept in your project but not built: " +
                                "your world needs no doorway. To have one as well, open that homeworld and use Links › World Entrance.", 12, Muted()));
                    }
                    else
                    {
                        // The doorway you chose (or the only way in, off the balloon route) and its way back.
                        string why = world.Place.OnRoute ? "too (you chose one)" : "(the only way in: your world is off the balloon route)";
                        panel.Children.Add(Text(world.EntranceHomeworldKey == null
                            ? $"Players can walk in through a doorway in {world.EntryHomeworldName} {why}. It is not placed yet: Create BIN places it near where Spyro arrives there, or open {world.EntryHomeworldName} and use Links › World Entrance."
                            : string.Equals(world.EntranceHomeworldKey, world.EntryHomeworldKey, StringComparison.OrdinalIgnoreCase)
                                ? $"Players can walk in through its doorway in {world.EntryHomeworldName} {why}; Links › World Entrance there moves it."
                                : $"Its doorway stands in {links.PlaceOf(_retailCatalog.FindByKey(world.EntranceHomeworldKey)?.LevelId ?? 10).SlotName}, but the world is entered from {world.EntryHomeworldName}: Create BIN places a new one in {world.EntryHomeworldName}."));
                        panel.Children.Add(Text(world.WayBackTo is int back
                            ? $"Its doorway home leads back to {links.PlaceOf(back).SlotName}, and brings Spyro out of the entrance there."
                            : $"{world.HomeworldName} has no doorway home yet (Links › Doorways › Add Doorway… there, then {world.EntryHomeworldName}).", 13, world.WayBackTo == null ? Amber() : null));
                    }
                    var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
                    var openWorld = NewButton($"Open {world.HomeworldName}"); openWorld.Name = "LevelLinksOpenWorld";
                    openWorld.Click += (_, _) => Open(() => SelectLevelFromPickerAsync(NewWorldDocument.Target(NewWorldDocument.Homeworld, world.HomeworldName)));
                    var openEntry = NewButton($"Open {world.EntryHomeworldName}"); openEntry.Name = "LevelLinksOpenEntrance";
                    var entryLevel = _retailCatalog.FindByKey(world.EntryHomeworldKey);
                    openEntry.Click += (_, _) => { if (entryLevel != null) Open(() => SelectLevelFromPickerAsync(entryLevel)); };
                    var balloon = NewButton("World's Balloon Route…"); balloon.Name = "LevelLinksBalloon";
                    balloon.Click += (_, _) => Open(BalloonRouteAsync);
                    buttons.Children.Add(openWorld); buttons.Children.Add(openEntry); buttons.Children.Add(balloon);
                    panel.Children.Add(buttons);
                    bool markedWorld = focusLevelId is >= NewWorldDocument.Homeworld and <= 74;
                    content.Children.Add(Card(panel, "LevelLinksWorld", markedWorld));
                    if (markedWorld) focusTarget ??= content.Children[^1];
                }
                RefreshUndo();
                if (!editable) Say("The links can't be changed while the editor is busy; close this window and try again in a moment.");
            }
            finally { rendering = false; }
            // Back where the reader was after a change; the marked level the first time.
            var target = focusTarget;
            bool first = !rendered;
            rendered = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (first && target != null) target.BringIntoView();
                else if (!first) scroll.Offset = offset;
            }, DispatcherPriority.Background);
        }

        // The one-step undo the Game Settings controls share: only while the saved links are still those the change left.
        undo.Click += (_, _) =>
        {
            if (CanEdit() && _portalRouteUndo is { } u && u.Workspace == root)
                try
                {
                    var current = PortalRouteStore.Capture(root);
                    if (current.Fingerprint != u.AfterFingerprint)
                        throw new InvalidDataException("the saved links changed after that change, so it was left as it is.");
                    PortalRouteStore.Save(root, u.Before, current);
                    _portalRouteUndo = null;
                    InvalidateBuildSafetySummary(); RefreshPortalRoutingEditor();
                    Say("Undone: the links are as they were before the last change.");
                }
                catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
                { Say("Could not undo: " + Plain(e)); }
            Render();
        };
        close.Click += (_, _) => dialog.Close();
        Render();
        LevelLinksWindowForTesting = dialog;
        try { await dialog.ShowDialog(this); }
        finally { LevelLinksWindowForTesting = null; }
        if (afterClose != null) await afterClose();
    }

    /// <summary>The project's levels and world as saved, with the open level as it is now (its name and objects), for
    /// Level Links. Whether a level has its Return Home vortex is read from the saved levels and the editor's object cache.</summary>
    private LevelLinks ReadLevelLinks(string root)
    {
        var scenes = SceneDocumentStore.ReadAll(root, _retailCatalog).ToList();
        var world = NewWorldDocument.Read(root);
        var worldScenes = world == null ? new List<SceneDocument>() : SceneDocumentStore.ReadWorld(root, _retailCatalog).ToList();
        var entrance = WorldEntranceDocument.Read(root);
        // The level open in the editor counts as it is now (it is saved; its name or objects may have changed since).
        var open = _freshSceneDocument;
        if (open != null)
        {
            var list = open.WorldLevelId != null ? worldScenes : scenes;
            int at = list.FindIndex(s => string.Equals(s.TargetKey, open.TargetKey, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) list[at] = list[at] with { Name = open.Name };
        }
        bool liveObjects = _currentLevel != null && _currentMobys.Count > 0;
        bool OpenIs(int levelId) => liveObjects && _currentLevel!.LevelId == levelId;
        Dictionary<int, ReturnHomeVortex> vortex = [];
        foreach (var level in _retailCatalog.Levels.Where(l => LevelLinks.HasReturnHome(l.LevelId)))
        {
            var own = ReturnHomeVortexCheck.OwnRows(root, level.Key);
            var built = scenes.FirstOrDefault(s => string.Equals(s.TargetBinding?.LevelKey, level.Key, StringComparison.OrdinalIgnoreCase));
            var found = built != null ? ReturnHomeVortexCheck.ForScene(built, own == null ? null : own.Count > 0) : ReturnHomeVortexCheck.ForGameLevel(root, level.Key);
            if (OpenIs(level.LevelId)) found = found with { Present = ReturnHomeVortexCheck.Has(_currentMobys) };
            vortex[level.LevelId] = found;
        }
        if (scenes.FirstOrDefault(s => s.TargetBinding == null && s.WorldLevelId == null) is { } newLevel)
            vortex[65] = OpenIs(65) && _freshSceneDocument?.TargetBinding == null ? new(ReturnHomeVortexCheck.Has(_currentMobys), false) : ReturnHomeVortexCheck.ForScene(newLevel, false);
        foreach (var scene in worldScenes.Where(s => s.WorldLevelId is > NewWorldDocument.Homeworld))
            vortex[scene.WorldLevelId!.Value] = OpenIs(scene.WorldLevelId.Value) ? new(ReturnHomeVortexCheck.Has(_currentMobys), false) : ReturnHomeVortexCheck.ForScene(scene, false);
        return new LevelLinks(_retailCatalog, scenes, world, worldScenes, entrance, id => vortex.GetValueOrDefault(id, ReturnHomeVortex.Unknown));
    }

    /// <summary>What the window says first about the level it was opened for.</summary>
    private static string FocusSentence(LevelLinksMap map, LevelLinks links, int levelId)
    {
        if (levelId is >= NewWorldDocument.Homeworld and <= 74)
            return map.World is { } w
                ? levelId == NewWorldDocument.Homeworld
                    ? $"{w.HomeworldName} is your world's homeworld. {w.Access} Its links are below, under Your world."
                    : (w.Levels.FirstOrDefault(l => l.LevelId == levelId) is { } l
                        ? l.HasDoorway
                            ? $"{l.Name} is a level of your world: Spyro gets in through its doorway in {w.HomeworldName}, and Return Home brings him back out of it."
                            : $"{l.Name} is a level of your world. It has no doorway in {w.HomeworldName} yet: open {w.HomeworldName} and use Links › Doorways › Add Doorway…."
                        : "This level of your world is not saved yet.")
                : "Your world could not be read.";
        if (levelId % 10 == 0 && map.Homeworlds.FirstOrDefault(h => h.LevelId == levelId) is { } home)
            return home.Rebuilt
                ? $"{home.RebuiltAs} is built in {LevelLinks.Possessive(home.Name)} place. Its doorways ({string.Join(", ", home.Doorways.Select(d => d.DoorwayName))}) lead to the levels of {home.Name}, and Return Home from each comes back out of its doorway."
                : $"{LevelLinks.Possessive(home.Name)} doorways are marked below.";
        var back = map.ReturnOf(levelId);
        var place = links.PlaceOf(levelId);
        if (back == null) return $"{place.Name} is marked below.";
        if (back.LeadingHere.Count == 0)
            return $"{place.Label}: no doorway leads to it yet. Choose it as a doorway's \"leads to\" below to give Spyro a way in; Return Home will bring him back out of that doorway.";
        string ways = string.Join(" and ", back.LeadingHere.Select(d => $"{LevelLinks.Possessive(d.HomeworldName)} {d.DoorwayName}"));
        string way = back.Effective != null && back.LeadingHere.Any(d => d.Portal == back.Effective)
            ? "Return Home brings him back out of " + (back.LeadingHere.Count == 1 ? "it" : $"the {back.Effective.Resolve().DestinationName} one")
            : "Return Home brings him back out of another doorway (see below)";
        return $"{place.Label}: Spyro gets in through {ways}, and {way}. Change either below.";
    }
}
