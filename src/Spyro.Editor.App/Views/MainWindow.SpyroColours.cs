using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Game Settings › Spyro's colours: a colour for each part of Spyro in the games Create BIN makes
/// (<see cref="NativeSpyroColours"/>), saved with the project (<see cref="SpyroColourSettings"/>) as soon as it is chosen.
/// A picture of Spyro in the chosen colours heads the card: the game's own stand-in Spyro model
/// (<see cref="NativeSpyroColours.PreviewModel"/>, read from the original disc) drawn by the Add object window's model
/// preview, redrawn as soon as a look, a colour or a slider changes. Each part has a swatch that opens a picker (quick
/// colours, hue / richness / brightness, #RRGGBB), his own colour beside it (read from the original disc) and a button
/// to put it back; looks set every part at once, and Surprise me makes one up.</summary>
public sealed partial class MainWindow
{
    private readonly Image _spyroColourPreview = new() { Name = "SpyroColourPreview", Width = UniversalMobyModelPreview.Size, Height = UniversalMobyModelPreview.Size, Stretch = Stretch.Uniform, IsVisible = false };
    private readonly TextBlock _spyroColourPreviewNote = new() { Name = "SpyroColourPreviewNote", FontSize = 12, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
    private byte[]? _spyroPreviewLevel;
    private bool _spyroPreviewBusy;
    private Dictionary<string, (byte R, byte G, byte B)>? _spyroPreviewPending;
    private readonly Random _spyroSurprise = new();
    private readonly StackPanel _spyroColourRows = new() { Name = "SpyroColourRows", Spacing = 4 };
    private readonly TextBlock _spyroColourSummary = new() { Name = "SpyroColourSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private Dictionary<string, (byte R, byte G, byte B)> _spyroColours = [];
    private IReadOnlyDictionary<string, (byte R, byte G, byte B)>? _spyroOriginalColours;
    private string? _spyroOriginalColoursDisc;
    private bool _spyroColoursUnreadable;

    /// <summary>Whole-body looks for the preset buttons (part key -> #RRGGBB; none: his own colours).</summary>
    private static readonly (string Name, (string Part, string Colour)[] Colours)[] SpyroColourPresets =
    [
        ("Classic", []),
        ("Ember", [("body", "#C0282D"), ("belly", "#F2C14E"), ("horns", "#3A2A2A"), ("spikes", "#2B2B2B"), ("wings", "#2B2B2B"), ("wingSkin", "#F2C14E"), ("tailTip", "#2B2B2B")]),
        ("Ice", [("body", "#7FC8F8"), ("belly", "#FFFFFF"), ("horns", "#E8F4FF"), ("spikes", "#BFE6FF"), ("wings", "#5AA0D8"), ("wingSkin", "#D6EFFF"), ("tailTip", "#BFE6FF"), ("pupils", "#1B3A6B")]),
        ("Emerald", [("body", "#2FA84F"), ("belly", "#F5E6A8"), ("horns", "#D4A017"), ("spikes", "#D4A017"), ("wings", "#D4A017"), ("wingSkin", "#8BC34A"), ("tailTip", "#D4A017")]),
        ("Gold", [("body", "#E0B030"), ("belly", "#FFF2B0"), ("horns", "#8A5A20"), ("spikes", "#8A5A20"), ("wings", "#C07820"), ("wingSkin", "#F0D070"), ("tailTip", "#8A5A20")]),
        ("Shadow", [("body", "#3A3A48"), ("belly", "#8A7FA0"), ("horns", "#C0C0C0"), ("spikes", "#9A5CFF"), ("wings", "#9A5CFF"), ("wingSkin", "#4B2A6B"), ("tailTip", "#9A5CFF"), ("eyes", "#FFE066")]),
    ];

    /// <summary>Quick colours in the picker.</summary>
    private static readonly string[] SpyroQuickColours =
    [
        "#FFFFFF", "#C0C0C0", "#808080", "#404040", "#101010", "#7A4FA8", "#4B2A6B", "#E0B030",
        "#F2C14E", "#C07820", "#8A5A20", "#C0282D", "#FF6B6B", "#FF9F1C", "#FFE066", "#8BC34A",
        "#2FA84F", "#1B7F5A", "#3BC9DB", "#7FC8F8", "#2F6FD6", "#1B3A6B", "#9A5CFF", "#F06FC0",
    ];

    private Control BuildSpyroColourSection()
    {
        StackPanel content = new() { Name = "SpyroColourPanel", Spacing = 8, Margin = new Thickness(2, 4, 2, 2) };
        content.Children.Add(new TextBlock
        {
            Text = "Give Spyro any colours you like, part by part. Each part keeps his shading. The colours are in every level, " +
                   "on the title screen, the loading screens and the cutscenes of the games you create.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        // Spyro in the chosen colours, on a sky-to-grass backdrop so light and dark looks both read.
        _spyroColourPreviewNote.Foreground = new SolidColorBrush(ModernMutedInk);
        ToolTip.SetTip(_spyroColourPreview, "Spyro as he will look in the game you create");
        content.Children.Add(new Border
        {
            Name = "SpyroColourPreviewHost", Height = UniversalMobyModelPreview.Size + 8, CornerRadius = new CornerRadius(8), ClipToBounds = true,
            BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#CFE6FB"), 0), new GradientStop(Color.Parse("#EAF4FD"), 0.62), new GradientStop(Color.Parse("#CFE8C4"), 0.78), new GradientStop(Color.Parse("#B9DDAA"), 1) }
            },
            Child = new Grid { Children = { _spyroColourPreview, _spyroColourPreviewNote } }
        });
        WrapPanel presets = new() { Name = "SpyroColourPresets", Orientation = Orientation.Horizontal };
        foreach (var (name, colours) in SpyroColourPresets)
        {
            var button = NewButton(name, () => ChooseSpyroColours(name, colours.ToDictionary(c => c.Part, c =>
                SpyroColourSettings.TryParse(c.Colour, out var v) ? v : throw new InvalidOperationException("Bad preset colour."))));
            button.Name = "SpyroColourPreset" + name;
            button.Margin = new Thickness(0, 0, 6, 6);
            ToolTip.SetTip(button, name == "Classic" ? "Spyro's own colours" : $"Every part in the {name} look");
            presets.Children.Add(button);
        }
        var surprise = NewButton("Surprise me", SurpriseSpyroColours);
        surprise.Name = "SpyroColourSurprise";
        surprise.Margin = new Thickness(0, 0, 6, 6);
        ToolTip.SetTip(surprise, "A look made up on the spot; click again for another");
        presets.Children.Add(surprise);
        content.Children.Add(presets);
        content.Children.Add(_spyroColourRows);
        content.Children.Add(_spyroColourSummary);
        RefreshSpyroColourSettings();
        // Open from the start: his picture is the quickest way to see what the card does.
        var section = BuildModernDisclosure("Spyro's colours", content, true);
        section.Name = "SpyroColourSection";
        return section;
    }

    /// <summary>Shows the project's saved colours (on project changes and after each choice).</summary>
    private void RefreshSpyroColourSettings()
    {
        string error = "";
        try { _spyroColours = SpyroColourSettings.Load(_workspace.RootPath); _spyroColoursUnreadable = false; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _spyroColours = []; _spyroColoursUnreadable = true; error = e.Message; }
        LoadSpyroOriginalColours();
        _spyroColourRows.Children.Clear();
        foreach (var part in NativeSpyroColours.Parts) _spyroColourRows.Children.Add(BuildSpyroColourRow(part));
        _spyroColourSummary.Text = error.Length > 0
            ? "The saved colours could not be read, so Create BIN stops until you choose colours again (a preset or Classic): " + error
            : _spyroColours.Count == 0 ? "Spyro has his own colours."
            : $"{_spyroColours.Count} of {NativeSpyroColours.Parts.Count} parts changed. Create BIN puts them in the game.";
        ShowSpyroPreview(_spyroColours);
    }

    /// <summary>Draws Spyro in these colours (the saved ones, or a colour being tried in the picker). Drawing runs in the
    /// background one picture at a time; while one is drawn, only the newest request waits.</summary>
    private void ShowSpyroPreview(IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        byte[]? level = _spyroPreviewLevel;
        if (level == null)
        {
            if (!_spyroColourPreview.IsVisible)
                _spyroColourPreviewNote.Text = _spyroOriginalColoursDisc == null ? "Open your game to see Spyro here." : "Drawing Spyro…";
            return;
        }
        var request = new Dictionary<string, (byte R, byte G, byte B)>(colours);
        if (_spyroPreviewBusy) { _spyroPreviewPending = request; return; }
        _spyroPreviewBusy = true;
        _ = Task.Run(() =>
        {
            try
            {
                return NativeSpyroColours.PreviewModel(level, request) is { } model ? UniversalMobyModelPreview.Decode(model.Package, model.Vram).Pixels : null;
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { return null; }
        }).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            _spyroPreviewBusy = false;
            if (task.Status == TaskStatus.RanToCompletion && task.Result is { } pixels && ReferenceEquals(level, _spyroPreviewLevel))
            {
                const int size = UniversalMobyModelPreview.Size;
                WriteableBitmap bitmap = new(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using (var buffer = bitmap.Lock())
                    for (int y = 0; y < size; y++) System.Runtime.InteropServices.Marshal.Copy(pixels, y * size * 4, buffer.Address + y * buffer.RowBytes, size * 4);
                var old = _spyroColourPreview.Source as IDisposable;
                _spyroColourPreview.Source = bitmap; _spyroColourPreview.IsVisible = true; _spyroColourPreviewNote.IsVisible = false;
                old?.Dispose();
            }
            else if (!_spyroColourPreview.IsVisible) _spyroColourPreviewNote.Text = "Spyro can't be drawn from this disc; the colours still work.";
            if (_spyroPreviewPending is { } next) { _spyroPreviewPending = null; ShowSpyroPreview(next); }
        }), TaskScheduler.Default);
    }

    /// <summary>A look made up on the spot: a body colour, a pale belly, and one accent family for horns, spikes, wings and
    /// tail tip (his eyes stay his own).</summary>
    private void SurpriseSpyroColours()
    {
        var random = _spyroSurprise;
        double Next(double from, double to) => from + random.NextDouble() * (to - from);
        (byte, byte, byte) Colour(double h, double sat, double l)
        {
            var (r, g, b) = NativeSpyroColours.FromHsl((h % 1 + 1) % 1, sat, l);
            return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
        double body = random.NextDouble(), accent = body + new[] { 0.5, 0.33, -0.33, 0.08, -0.08 }[random.Next(5)] + Next(-0.03, 0.03);
        bool darkBody = random.Next(4) == 0;
        var colours = new Dictionary<string, (byte R, byte G, byte B)>
        {
            ["body"] = Colour(body, Next(0.5, 0.85), darkBody ? Next(0.2, 0.3) : Next(0.38, 0.6)),
            ["belly"] = Colour(random.Next(2) == 0 ? accent : body + Next(-0.06, 0.06), Next(0.45, 0.85), Next(0.72, 0.88)),
            ["horns"] = Colour(accent, Next(0.3, 0.8), random.Next(2) == 0 ? Next(0.25, 0.4) : Next(0.7, 0.9)),
            ["spikes"] = Colour(accent, Next(0.7, 0.95), Next(0.48, 0.62)),
            ["wings"] = Colour(accent, Next(0.55, 0.9), Next(0.32, 0.5)),
            ["wingSkin"] = Colour(accent + Next(-0.07, 0.07), Next(0.55, 0.9), Next(0.5, 0.7)),
        };
        colours["tailTip"] = colours["spikes"];
        SaveSpyroColours(colours, "A new look for Spyro. Click Surprise me again for another, or change any part below.");
    }

    private Control BuildSpyroColourRow(NativeSpyroColours.Part part)
    {
        bool changed = _spyroColours.TryGetValue(part.Key, out var chosen);
        (byte R, byte G, byte B)? own = _spyroOriginalColours != null && _spyroOriginalColours.TryGetValue(part.Key, out var o) ? o : null;
        var shown = changed ? chosen : own ?? (128, 128, 128);
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Name = "SpyroColourRow_" + part.Key };
        Button swatch = new()
        {
            Name = "SpyroColourSwatch_" + part.Key,
            Width = 46, Height = 28, Padding = new Thickness(3), CornerRadius = new CornerRadius(6),
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(shown.R, shown.G, shown.B)), CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), BorderThickness = new Thickness(1),
                // His own colour, when the part is changed: a small corner of the swatch.
                Child = changed && own is { } mine ? new Border
                {
                    Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Color.FromRgb(mine.R, mine.G, mine.B)), CornerRadius = new CornerRadius(3, 0, 3, 0)
                } : null
            },
            Flyout = BuildSpyroColourPicker(part, shown)
        };
        ToolTip.SetTip(swatch, $"Choose the colour of his {part.Name.ToLowerInvariant()}");
        AutomationProperties.SetName(swatch, $"{part.Name} colour");
        row.Children.Add(swatch);
        StackPanel label = new() { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = part.Name, FontSize = 13, FontWeight = changed ? FontWeight.SemiBold : FontWeight.Normal, Foreground = new SolidColorBrush(ModernInk) });
        label.Children.Add(new TextBlock
        {
            Text = changed ? $"{part.Description} · {SpyroColourSettings.Format(chosen)}" : part.Description,
            FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        if (changed)
        {
            var reset = NewButton("His own", () => SetSpyroColour(part, null));
            reset.Name = "SpyroColourReset_" + part.Key;
            reset.VerticalAlignment = VerticalAlignment.Center;
            ToolTip.SetTip(reset, $"Put back Spyro's own {part.Name.ToLowerInvariant()} colour");
            Grid.SetColumn(reset, 2);
            row.Children.Add(reset);
        }
        return row;
    }

    /// <summary>The picker: quick colours, hue / richness / brightness sliders with a live swatch, and #RRGGBB.
    /// Use applies the colour; the flyout closes.</summary>
    private Flyout BuildSpyroColourPicker(NativeSpyroColours.Part part, (byte R, byte G, byte B) start)
    {
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        var (h0, s0, l0) = NativeSpyroColours.ToHsl(start.R / 255.0, start.G / 255.0, start.B / 255.0);
        Slider hue = new() { Name = "SpyroColourHue", Minimum = 0, Maximum = 360, Value = h0 * 360, Width = 220 };
        Slider sat = new() { Name = "SpyroColourRichness", Minimum = 0, Maximum = 100, Value = s0 * 100, Width = 220 };
        Slider light = new() { Name = "SpyroColourBrightness", Minimum = 0, Maximum = 100, Value = l0 * 100, Width = 220 };
        TextBox hex = new() { Name = "SpyroColourHex", Text = SpyroColourSettings.Format(start), Width = 96, MaxLength = 7 };
        Border preview = new() { Width = 64, Height = 40, CornerRadius = new CornerRadius(6), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), BorderThickness = new Thickness(1) };
        // What each slider chooses from: a strip of the colours along it.
        Border Strip() => new() { Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(10, 2, 10, -6), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), BorderThickness = new Thickness(1) };
        Border hueStrip = Strip(), satStrip = Strip(), lightStrip = Strip();
        bool syncing = false, opened = false;
        (byte R, byte G, byte B) current = start;
        void Show((byte R, byte G, byte B) c, bool fromSliders)
        {
            current = c;
            preview.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            // Spyro's picture tries the colour on while the picker is open.
            if (opened) ShowSpyroPreview(new Dictionary<string, (byte R, byte G, byte B)>(_spyroColoursUnreadable ? [] : _spyroColours) { [part.Key] = c });
            syncing = true;
            try
            {
                if (!fromSliders)
                {
                    var (h, s, l) = NativeSpyroColours.ToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0);
                    // A grey keeps the hue the slider had.
                    if (s > 0) hue.Value = h * 360;
                    sat.Value = s * 100; light.Value = l * 100;
                }
                if (hex.Text?.Trim().ToUpperInvariant() != SpyroColourSettings.Format(c)) hex.Text = SpyroColourSettings.Format(c);
                var (hr, hg, hb) = NativeSpyroColours.FromHsl(hue.Value / 360, 1, 0.5);
                satStrip.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromRgb(128, 128, 128), 0), new GradientStop(Color.FromRgb((byte)(hr * 255), (byte)(hg * 255), (byte)(hb * 255)), 1) }
                };
            }
            finally { syncing = false; }
        }
        void FromSliders()
        {
            if (syncing) return;
            var (r, g, b) = NativeSpyroColours.FromHsl(hue.Value / 360, sat.Value / 100, light.Value / 100);
            Show(((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255)), true);
        }
        hue.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) FromSliders(); };
        sat.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) FromSliders(); };
        light.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) FromSliders(); };
        hex.PropertyChanged += (_, e) =>
        {
            if (syncing || e.Property != TextBox.TextProperty) return;
            if (SpyroColourSettings.TryParse(hex.Text, out var c)) Show(c, false);
        };
        var hueBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
        };
        for (int i = 0; i <= 6; i++)
        {
            var (r, g, b) = NativeSpyroColours.FromHsl(i / 6.0, 1, 0.5);
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)), i / 6.0));
        }
        hueStrip.Background = hueBrush;
        lightStrip.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Color.FromRgb(128, 128, 128), 0.5), new GradientStop(Colors.White, 1) }
        };

        UniformGrid quick = new() { Columns = 8, Name = "SpyroColourQuick" };
        foreach (string text in SpyroQuickColours)
        {
            if (!SpyroColourSettings.TryParse(text, out var c)) continue;
            var chip = new Button
            {
                Width = 26, Height = 22, Margin = new Thickness(2), Padding = new Thickness(0), CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), BorderThickness = new Thickness(1)
            };
            ToolTip.SetTip(chip, text);
            chip.Click += (_, _) => Show(c, false);
            quick.Children.Add(chip);
        }

        StackPanel Labeled(string text, params Control[] controls)
        {
            var p = new StackPanel { Spacing = 2 };
            p.Children.Add(new TextBlock { Text = text, FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk) });
            p.Children.AddRange(controls);
            return p;
        }
        bool keep = false;   // Use (or His own) saves a choice: the picture follows that, not the closing picker
        var use = NewButton("Use this colour", () =>
        {
            keep = true;
            flyout.Hide();
            SetSpyroColour(part, current);
        });
        use.Name = "SpyroColourUse";
        var own = NewButton("His own", () =>
        {
            keep = true;
            flyout.Hide();
            SetSpyroColour(part, null);
        });
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 6, Children = { use, own } };
        StackPanel top = new() { Orientation = Orientation.Horizontal, Spacing = 10, Children = { preview, Labeled("Colour (#RRGGBB)", hex) } };
        flyout.Content = new StackPanel
        {
            Name = "SpyroColourPicker_" + part.Key, Spacing = 8, Width = 240,
            Children =
            {
                new TextBlock { Text = part.Name, FontSize = 13, FontWeight = FontWeight.SemiBold },
                quick, Labeled("Hue", hueStrip, hue), Labeled("Richness", satStrip, sat), Labeled("Brightness", lightStrip, light), top, buttons
            }
        };
        flyout.Opened += (_, _) => opened = true;
        // Closed without Use: the picture goes back to the saved colours.
        flyout.Closed += (_, _) => { opened = false; if (!keep) ShowSpyroPreview(_spyroColours); keep = false; };
        hex.KeyDown += (_, e) => { if (e.Key == Key.Enter && SpyroColourSettings.TryParse(hex.Text, out var _)) { keep = true; flyout.Hide(); SetSpyroColour(part, current); } };
        Show(start, false);
        return flyout;
    }

    private void SetSpyroColour(NativeSpyroColours.Part part, (byte R, byte G, byte B)? colour)
    {
        var colours = _spyroColoursUnreadable ? new Dictionary<string, (byte R, byte G, byte B)>() : new Dictionary<string, (byte R, byte G, byte B)>(_spyroColours);
        if (colour is { } c) colours[part.Key] = c; else colours.Remove(part.Key);
        SaveSpyroColours(colours, colour is { } chosen
            ? $"Spyro's {part.Name.ToLowerInvariant()}: {SpyroColourSettings.Format(chosen)}. Create BIN puts it in the game."
            : $"Spyro's {part.Name.ToLowerInvariant()} is his own colour again.");
    }

    private void ChooseSpyroColours(string preset, Dictionary<string, (byte R, byte G, byte B)> colours) =>
        SaveSpyroColours(colours, colours.Count == 0 ? "Spyro has his own colours again." : $"Spyro is in the {preset} look. Create BIN puts it in the game.");

    private void SaveSpyroColours(Dictionary<string, (byte R, byte G, byte B)> colours, string status)
    {
        try
        {
            if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
            SpyroColourSettings.Save(_workspace.RootPath, colours);
            EditorDiagnostics.RecordAction("Spyro's colours", string.Join(", ", colours.Select(c => $"{c.Key} {SpyroColourSettings.Format(c.Value)}")));
            _statusText.Text = status;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            EditorDiagnostics.RecordException("saving Spyro's colours", error);
            _statusText.Text = "Could not save Spyro's colours: " + error.Message;
        }
        RefreshSpyroColourSettings();
    }

    /// <summary>His own colour for each part and the stand-in Spyro for the picture, read once per disc in the background
    /// (the swatches show grey until then).</summary>
    private void LoadSpyroOriginalColours()
    {
        string disc;
        try { disc = ResolveUniversalSourceDisc(); }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { return; }
        if (string.IsNullOrEmpty(disc) || !File.Exists(disc) || _spyroOriginalColoursDisc == disc) return;
        _spyroOriginalColoursDisc = disc;
        _ = Task.Run(() =>
        {
            try
            {
                var image = UniversalDiscImage.Read(disc);
                byte[]? originals = NativeSpyroColours.FindOriginals(image.EntryBytes);
                // Gnasty Gnorc's level has the stand-in Spyro the card draws.
                byte[] standIn = image.EntryBytes(NativeSpyroColours.StandInWadEntry);
                return (Own: originals == null ? null : NativeSpyroColours.OriginalColours(originals),
                    Level: originals != null && NativeSpyroColours.PreviewModel(standIn, new Dictionary<string, (byte R, byte G, byte B)>()) != null ? standIn : null);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return default; }
        }).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            if (task.Status != TaskStatus.RanToCompletion || _spyroOriginalColoursDisc != disc) return;
            _spyroPreviewLevel = task.Result.Level;
            if (task.Result.Level == null && !_spyroColourPreview.IsVisible) _spyroColourPreviewNote.Text = "Spyro can't be drawn from this disc; the colours still work.";
            if (task.Result.Own == null) { ShowSpyroPreview(_spyroColours); return; }
            _spyroOriginalColours = task.Result.Own;
            RefreshSpyroColourSettings();
        }), TaskScheduler.Default);
    }

    /// <summary>What the game got from Game Settings, for Create BIN's status (nothing when neither is on).</summary>
    private static string GameSettingsExportNote()
    {
        List<string> parts = [];
        if (NativeSpyroColours.Enabled) parts.Add("Spyro's colours");
        if (NativeHover.Enabled) parts.Add("the hover");
        return (parts.Count == 0 ? "" : " With " + string.Join(" and ", parts) + " from Game Settings.") + HoverFlapNote();
    }

    /// <summary>The levels of the export just made where Spyro hovers without flapping (no room for the flap).</summary>
    private static string HoverFlapNote() => NativeHoverFlap.NoRoomNote(NativeHover.CurrentLevelsWithoutFlap);

    /// <summary>Create BIN's colour scope: the export's last steps recolour Spyro when the project asks.</summary>
    private NativeSpyroColours.Scope UseProjectSpyroColours()
    {
        Dictionary<string, (byte R, byte G, byte B)> colours;
        try { colours = SpyroColourSettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { throw new InvalidDataException("Spyro's colours in Game Settings could not be read. Choose them again (a preset or Classic). (" + e.Message + ")", e); }
        return NativeSpyroColours.Use(colours);
    }
}
