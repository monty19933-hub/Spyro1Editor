using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Game Settings › Egg thieves' colour: any colour in place of the egg thieves' blue in the games Create BIN
/// makes (<see cref="NativeEggThiefColours"/>), with a thief drawn in it on the card.</summary>
public sealed partial class MainWindow
{
    private readonly Image _eggThiefPreview = new() { Name = "EggThiefColourPreview", Width = 150, Height = 150, Stretch = Stretch.Uniform, IsVisible = false };
    private readonly TextBlock _eggThiefPreviewNote = new() { Name = "EggThiefColourPreviewNote", FontSize = 12, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8), Text = "Open your game to see a thief here." };
    private readonly TextBlock _eggThiefSummary = new() { Name = "EggThiefColourSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _eggThiefHex = new() { Name = "EggThiefColourHex", Width = 96, MaxLength = 7, Watermark = "#RRGGBB" };
    private (byte R, byte G, byte B)? _eggThiefColour;
    private byte[]? _eggThiefLevel;
    private string? _eggThiefLevelDisc;
    private bool _eggThiefPreviewBusy;
    private (bool Set, (byte R, byte G, byte B)? Colour) _eggThiefPreviewPending;
    private readonly Random _eggThiefSurprise = new();

    internal (byte R, byte G, byte B)? EggThiefColourForTesting => _eggThiefColour;
    internal void SetEggThiefColourForTesting((byte R, byte G, byte B)? colour) => SaveEggThiefColour(colour, "test");

    private static readonly (string Name, string? Colour)[] EggThiefColours =
    [
        ("Their own blue", null), ("Red", "#C82828"), ("Orange", "#E07818"), ("Gold", "#D8B020"), ("Green", "#2E9E44"), ("Sky", "#40B8F0"),
        ("Purple", "#7A3CC8"), ("Pink", "#E060B0"), ("White", "#E6E6EE"), ("Black", "#34343A"),
    ];

    private Control BuildEggThiefColourSection()
    {
        StackPanel content = new() { Name = "EggThiefColourPanel", Spacing = 8, Margin = new Thickness(2, 4, 2, 2) };
        content.Children.Add(new TextBlock
        {
            Text = "The egg thieves' robes in any colour, in every level that has thieves (and thieves you add to levels you build). " +
                   "They keep their shading; their eyes stay as they are. In High Caves the tornado wizards wear the same cloth, so they change with them.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        _eggThiefPreviewNote.Foreground = new SolidColorBrush(ModernMutedInk);
        content.Children.Add(new Border
        {
            Name = "EggThiefColourPreviewHost", Height = 158, CornerRadius = new CornerRadius(8), ClipToBounds = true,
            BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#CFE6FB"), 0), new GradientStop(Color.Parse("#EAF4FD"), 0.62), new GradientStop(Color.Parse("#CFE8C4"), 0.78), new GradientStop(Color.Parse("#B9DDAA"), 1) }
            },
            Child = new Grid { Children = { _eggThiefPreview, _eggThiefPreviewNote } }
        });
        WrapPanel swatches = new() { Name = "EggThiefColourSwatches", Orientation = Orientation.Horizontal };
        foreach (var (name, hex) in EggThiefColours)
        {
            (byte R, byte G, byte B)? colour = hex != null && SpyroColourSettings.TryParse(hex, out var parsed) ? parsed : null;
            var chip = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(colour is { } c ? Color.FromRgb(c.R, c.G, c.B) : Color.Parse("#3280A6")), VerticalAlignment = VerticalAlignment.Center
            };
            var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { chip, new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } };
            var button = new Button { Name = "EggThiefColour" + name.Replace(" ", ""), Content = face, Margin = new Thickness(0, 0, 6, 6) };
            StyleModernSecondaryButton(button);
            button.Click += (_, _) => SaveEggThiefColour(colour, colour == null ? "The egg thieves wear their own blue again." : $"The egg thieves wear {name.ToLowerInvariant()}. Create BIN puts it in the game.");
            swatches.Children.Add(button);
        }
        content.Children.Add(swatches);
        var use = NewButton("Use This Colour", () =>
        {
            if (SpyroColourSettings.TryParse(_eggThiefHex.Text, out var typed)) SaveEggThiefColour(typed, $"The egg thieves wear {SpyroColourSettings.Format(typed)}. Create BIN puts it in the game.");
            else _statusText.Text = "Type the colour as #RRGGBB (for example #C82828 for red).";
        });
        use.Name = "EggThiefColourUse";
        var surprise = NewButton("Surprise me", () =>
        {
            var (r, g, b) = NativeSpyroColours.FromHsl(_eggThiefSurprise.NextDouble(), 0.5 + 0.4 * _eggThiefSurprise.NextDouble(), 0.3 + 0.3 * _eggThiefSurprise.NextDouble());
            var made = ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
            SaveEggThiefColour(made, $"The egg thieves wear {SpyroColourSettings.Format(made)}. Create BIN puts it in the game.");
        });
        surprise.Name = "EggThiefColourSurprise";
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _eggThiefHex, use, surprise } });
        content.Children.Add(_eggThiefSummary);
        RefreshEggThiefColour();
        var section = BuildModernDisclosure("Egg thieves' colour", content, false);
        section.Name = "EggThiefColourSection";
        return section;
    }

    /// <summary>Shows the project's saved colour (on project changes and after each choice).</summary>
    private void RefreshEggThiefColour()
    {
        try { _eggThiefColour = string.IsNullOrEmpty(_workspace.RootPath) ? null : EggThiefColourSettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException) { _eggThiefColour = null; }
        _eggThiefSummary.Text = _eggThiefColour is { } c
            ? $"Egg thieves wear {SpyroColourSettings.Format(c)} in the games you create."
            : "Egg thieves wear their own blue.";
        if (_eggThiefColour is { } shown && !_eggThiefHex.IsFocused) _eggThiefHex.Text = SpyroColourSettings.Format(shown);
        LoadEggThiefLevel();
        ShowEggThiefPreview(_eggThiefColour);
    }

    private void SaveEggThiefColour((byte R, byte G, byte B)? colour, string status)
    {
        try
        {
            if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
            EggThiefColourSettings.Save(_workspace.RootPath, colour);
            EditorDiagnostics.RecordAction("Egg thieves' colour", colour is { } c ? SpyroColourSettings.Format(c) : "their own");
            _statusText.Text = status;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            EditorDiagnostics.RecordException("saving the egg thieves' colour", error);
            _statusText.Text = "Could not save the egg thieves' colour: " + error.Message;
        }
        RefreshEggThiefColour();
    }

    /// <summary>A level with a thief (Stone Hill) for the picture, read once per disc in the background.</summary>
    private void LoadEggThiefLevel()
    {
        string disc;
        try { disc = ResolveUniversalSourceDisc(); }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { return; }
        if (string.IsNullOrEmpty(disc) || !File.Exists(disc) || _eggThiefLevelDisc == disc) return;
        _eggThiefLevelDisc = disc;
        int entry = _retailCatalog.FindByKey("stonehill")?.SourceWadEntry ?? 0;
        if (entry <= 0) return;
        _ = Task.Run(() =>
        {
            try
            {
                byte[] level = UniversalDiscImage.Read(disc).EntryBytes(entry);
                return NativeEggThiefColours.PreviewModel(level, null) != null ? level : null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return null; }
        }).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            if (task.Status != TaskStatus.RanToCompletion || _eggThiefLevelDisc != disc) return;
            _eggThiefLevel = task.Result;
            if (_eggThiefLevel == null) _eggThiefPreviewNote.Text = "A thief can't be drawn from this disc; the colour still works.";
            ShowEggThiefPreview(_eggThiefColour);
        }), TaskScheduler.Default);
    }

    // ---- The run: a selected egg thief's (or any routed object's) route, shaped with buttons as well as by hand ----
    private Border? _routeToolsCard;
    private TextBlock? _routeToolsNote;

    /// <summary>Objects tab, under the selected object's buttons: tools for the route of an egg thief (or a sheep, or any
    /// object that walks one): a new loop round where it stands, bigger, smaller, turned, the other way round. The
    /// points stay on the ground; the same points can be dragged in the 3D view or typed in Edit Route….</summary>
    private Control BuildRouteToolsCard()
    {
        StackPanel content = new() { Spacing = 6 };
        _routeToolsNote = new() { Name = "RouteToolsNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk) };
        content.Children.Add(_routeToolsNote);
        Button Tool(string name, string text, string tip, Action run)
        {
            Button button = NewButton(text, run);
            button.Name = name; button.HorizontalAlignment = HorizontalAlignment.Stretch;
            StyleModernSecondaryButton(button);
            ToolTip.SetTip(button, tip);
            return button;
        }
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,6,*,6,*"), RowDefinitions = new RowDefinitions("Auto,6,Auto") };
        var tools = new[]
        {
            Tool("RouteToolsLoop", "New Loop Here", "A new round run about where it stands, as wide as the open floor allows. Press again for another.", () => ReshapeSelectedRoute("loop")),
            Tool("RouteToolsBigger", "Bigger", "The whole run a quarter wider about its middle.", () => ReshapeSelectedRoute("bigger")),
            Tool("RouteToolsSmaller", "Smaller", "The whole run a fifth narrower about its middle.", () => ReshapeSelectedRoute("smaller")),
            Tool("RouteToolsTurn", "Turn", "The whole run turned 30° about its middle.", () => ReshapeSelectedRoute("turn")),
            Tool("RouteToolsReverse", "Other Way Round", "The same points in the opposite order: it runs the other way.", () => ReshapeSelectedRoute("reverse")),
            Tool("RouteToolsReset", "As It Came", "The run it came with, moved to where it stands.", () => ReshapeSelectedRoute("reset")),
        };
        for (int i = 0; i < tools.Length; i++) { Grid.SetColumn(tools[i], i % 3 * 2); Grid.SetRow(tools[i], i / 3 * 2); grid.Children.Add(tools[i]); }
        content.Children.Add(grid);
        _routeToolsCard = TerrainCard("Its run", content);
        _routeToolsCard.Name = "RouteToolsCard";
        _routeToolsCard.IsVisible = false;
        return _routeToolsCard;
    }

    private void RefreshRouteToolsCard()
    {
        if (_routeToolsCard == null || _routeToolsNote == null) return;
        var path = SelectedNativeMobyPath();
        bool show = path != null && (path.IsPlacedObjectRoute || IsThiefRun(path)) && path.Nodes.Count >= 2 && !_createBinBusy && !_workspaceTransitionBusy;
        _routeToolsCard.IsVisible = show;
        if (!show || path == null) return;
        double length = 0;
        for (int i = 0; i < path.Nodes.Count; i++)
        {
            var a = path.Nodes[i].Position; var b = path.Nodes[(i + 1) % path.Nodes.Count].Position;
            length += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }
        bool thief = path.OwnerNativeClass == NativeEggThiefColours.ThiefClass;
        _routeToolsNote.Text = $"{(thief ? "This egg thief runs" : "It walks")} a route of {path.Nodes.Count} points, about {length:0} units round" +
            (path.HasEdits ? " (changed from the one it came with)" : "") + $". Drag the numbered points in the 3D view, type them in {(path.IsPlacedObjectRoute ? "Edit Route…" : "Edit Run Path")}, or shape the whole run here. " +
            (thief ? "Give a thief room to run and Spyro room to chase: a round of 1,500 to 3,000 units on open, level floor plays best. " +
                "A point he could not run to (no ground, water, lava or ooze, or a step up or down of more than 150 units) is not taken." : "") +
            ThiefWaterWarning(path);
    }

    /// <summary>Said on the run card when an egg thief stands in, or runs through, water, lava or ooze: he can, but Spyro
    /// sinks there and can't catch him (empty when he is on dry ground).</summary>
    private string ThiefWaterWarning(NativeMobyPath path)
    {
        if (!IsThiefRun(path) || _currentMobys.FirstOrDefault(m => m.TrueIndex == path.OwnerTrueIndex && !m.IsRemoved) is not { } owner) return "";
        bool stands = IsLiquidAt(owner.Position.X, owner.Position.Y);
        int wet = path.Nodes.Count(n => IsLiquidAt(n.Position.X, n.Position.Y));
        if (!stands && wet == 0) return "";
        return " ⚠ " + (stands ? "He stands in water, lava or ooze" : $"{Count(wet, "point", "points")} of his run {(wet == 1 ? "is" : "are")} in water, lava or ooze") +
               ": Spyro sinks there and can't catch him. Move him onto dry ground (drag him, or set him down again with Add Object), then use New Loop Here.";
    }

    internal void ReshapeSelectedRouteForTesting(string how) => ReshapeSelectedRoute(how);
    internal string MoveRunPointForTesting(NativeMobyPath path, int node, float dx, float dy)
    {
        MoveNativePathNodeFromViewport(new ViewportNativePathNodeMoveRequestedEventArgs(path, path.Nodes[node], dx, dy));
        return _statusText.Text ?? "";
    }
    /// <summary>"ok", or what stops the selected run (as the editor and Create BIN judge it).</summary>
    internal string DescribeRunForTesting(NativeMobyPath path) =>
        JudgeRun(path, [.. path.Nodes.Select(n => n.Position)]) is { Count: > 0 } problems ? string.Join(" | ", problems.Select(p => p.Text)) : "ok";

    /// <summary>Reshapes the selected object's route (a placed object's): every point keeps to the ground.</summary>
    private void ReshapeSelectedRoute(string how)
    {
        if (SelectedNativeMobyPath() is not { } path || !path.IsPlacedObjectRoute && !IsThiefRun(path) || _selectedMoby is not { } owner || path.Nodes.Count < 2) return;
        if (TryBlockId65ObjectMutation("Route editing")) return;
        var nodes = path.Nodes;
        int n = nodes.Count;
        var old = nodes.Select(node => node.Position).ToArray();
        float cx = old.Average(p => p.X), cy = old.Average(p => p.Y);
        // A point of the reshaped run on the floor: where it falls, or (when that is up a ledge, down a drop or off the
        // floor: a thief cannot jump) drawn in towards the run's middle until the floor is level with where it was.
        Spyro.Editor.Core.Primitives.Vector3f Ground(float x, float y, float z)
        {
            for (float keep = 1; keep >= 0.35f; keep -= 0.08f)
            {
                float px = cx + (x - cx) * keep, py = cy + (y - cy) * keep;
                if (TryFindTerrainZAt(px, py, z + 40, out float floor, preferTopSurface: false) && Math.Abs(floor - z) <= 24) return new(px, py, floor);
            }
            return new(x, y, z);
        }
        Spyro.Editor.Core.Primitives.Vector3f[] made;
        string done;
        switch (how)
        {
            case "loop":
            {
                // Round where it stands: the widest of a few sizes whose every point finds level floor, started at a turn of its own.
                var at = owner.Position;
                double turn = _eggThiefSurprise.NextDouble() * Math.PI * 2, squash = 0.7 + 0.3 * _eggThiefSurprise.NextDouble();
                made = old;
                float chosen = 0;
                foreach (float radius in new[] { 420f, 340f, 270f, 210f, 160f, 120f })
                {
                    var tried = Enumerable.Range(0, n).Select(i =>
                    {
                        double a = turn + Math.PI * 2 * i / n;
                        float x = at.X + radius * (float)Math.Cos(a), y = at.Y + radius * (float)(Math.Sin(a) * squash);
                        return (Found: TryFindTerrainZAt(x, y, at.Z + 40, out float floor, preferTopSurface: false) && Math.Abs(floor - at.Z) <= 48, P: new Spyro.Editor.Core.Primitives.Vector3f(x, y, floor));
                    }).ToArray();
                    if (tried.All(t => t.Found)) { made = [.. tried.Select(t => t.P)]; chosen = radius; break; }
                }
                if (chosen == 0) { _statusText.Text = "There is no level floor round it for a run: move it to more open ground first."; return; }
                done = $"a new run round where it stands, about {chosen * 2:0} units across";
                break;
            }
            case "bigger": case "smaller":
            {
                float f = how == "bigger" ? 1.25f : 0.8f;
                made = [.. old.Select(p => Ground(cx + (p.X - cx) * f, cy + (p.Y - cy) * f, p.Z))];
                done = how == "bigger" ? "its run a quarter wider" : "its run a fifth narrower";
                break;
            }
            case "turn":
            {
                float c = MathF.Cos(MathF.PI / 6), s = MathF.Sin(MathF.PI / 6);
                made = [.. old.Select(p => Ground(cx + (p.X - cx) * c - (p.Y - cy) * s, cy + (p.X - cx) * s + (p.Y - cy) * c, p.Z))];
                done = "its run turned 30°";
                break;
            }
            case "reverse":
                // The first point stays the first (it is where the run starts); the rest in the opposite order.
                made = [old[0], .. old.Skip(1).Reverse()];
                done = "its run the other way round";
                break;
            default:
                path.ResetEdits();
                StorePlacedObjectRoute(path);
                InvalidateBuildSafetySummary(); _viewport.RefreshNativeMovementData(); RefreshActionAvailability(); RefreshRouteToolsCard();
                _statusText.Text = path.IsPlacedObjectRoute ? $"{owner.DisplayLabel} has the run it came with again. Save the level to keep it."
                    : $"{owner.DisplayLabel} has the game's own run again. Save the level to keep it.";
                return;
        }
        // A step of the game's own routes is at most about 2,000 units: longer ones are not made.
        for (int i = 0; i < n; i++)
        {
            var a = made[i]; var b = made[(i + 1) % n];
            if (Math.Abs(a.X - b.X) > 1900 || Math.Abs(a.Y - b.Y) > 1900 || Math.Abs(a.Z - b.Z) > 1900)
            { _statusText.Text = "That would make a step of the run longer than the game can follow. Use Smaller, or New Loop Here."; return; }
        }
        // An egg thief's points sit on the floor he runs on, and the run must be one he can take.
        if (IsThiefRun(path))
        {
            if (!path.IsPlacedObjectRoute && _currentGeometry is { Polygons.Count: > 0 } geometry)
                made = [.. made.Select(p => EggThiefRun.TryPlace(geometry.Polygons, p.X, p.Y, p.Z, out var placed, ThiefGround(path)) ? placed : p)];
            if (JudgeRun(path, made) is { Count: > 0 } problems)
            { _statusText.Text = $"That would give {owner.DisplayLabel} a run he can't take: {RunProblemText(problems[0])}. Nothing was changed."; return; }
        }
        for (int i = 0; i < n; i++) nodes[i].SetPosition(made[i]);
        StorePlacedObjectRoute(path);
        InvalidateBuildSafetySummary();
        _viewport.RefreshNativeMovementData();
        RefreshActionAvailability();
        RefreshRouteToolsCard();
        _statusText.Text = $"{owner.DisplayLabel}: {done}. Save the level to keep it.";
    }

    private void ShowEggThiefPreview((byte R, byte G, byte B)? colour)
    {
        byte[]? level = _eggThiefLevel;
        if (level == null) return;
        if (_eggThiefPreviewBusy) { _eggThiefPreviewPending = (true, colour); return; }
        _eggThiefPreviewBusy = true;
        _ = Task.Run(() =>
        {
            try { return NativeEggThiefColours.PreviewModel(level, colour) is { } model ? UniversalMobyModelPreview.Decode(model.Package, model.Vram).Pixels : null; }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { return null; }
        }).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            _eggThiefPreviewBusy = false;
            if (task.Status == TaskStatus.RanToCompletion && task.Result is { } pixels && ReferenceEquals(level, _eggThiefLevel))
            {
                const int size = UniversalMobyModelPreview.Size;
                WriteableBitmap bitmap = new(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using (var buffer = bitmap.Lock())
                    for (int y = 0; y < size; y++) System.Runtime.InteropServices.Marshal.Copy(pixels, y * size * 4, buffer.Address + y * buffer.RowBytes, size * 4);
                var old = _eggThiefPreview.Source as IDisposable;
                _eggThiefPreview.Source = bitmap; _eggThiefPreview.IsVisible = true; _eggThiefPreviewNote.IsVisible = false;
                old?.Dispose();
            }
            if (_eggThiefPreviewPending.Set) { var next = _eggThiefPreviewPending.Colour; _eggThiefPreviewPending = default; ShowEggThiefPreview(next); }
        }), TaskScheduler.Default);
    }
}
