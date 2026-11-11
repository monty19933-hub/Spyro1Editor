using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Game Settings › This level › Creatures' colours: a colour for each kind of enemy of the open level, in that
/// level only (<see cref="NativeCreatureColours"/>), so the same gnorc can be red in Artisans and blue in Dark Hollow.
/// The card lists the level's enemy kinds (one of the game's levels: its own; a level you built: the kinds placed in
/// it), each drawn in its colour, with Randomize (another hue for every kind) and Their Own Colours. Choices are kept in
/// the project (<see cref="CreatureColourSettings"/>) and put in the game by Create BIN.</summary>
public sealed partial class MainWindow
{
    private sealed record CreatureKind(string Key, string Name, NativeCreatureColours.Signature Model, (byte R, byte G, byte B) Own, byte[] SourceLevel);

    private readonly StackPanel _creatureColourRows = new() { Name = "CreatureColourRows", Spacing = 6 };
    private readonly TextBlock _creatureColourNote = new() { Name = "CreatureColourNote", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private Button? _creatureRandomize, _creatureOwn;
    private List<CreatureKind> _creatureKinds = [];
    private Dictionary<string, (byte R, byte G, byte B)> _creatureColours = new(StringComparer.OrdinalIgnoreCase);
    private string _creatureLevelKey = "", _creatureError = "";
    private int _creatureGeneration;
    private Task _creatureLoad = Task.CompletedTask;
    private Random _creatureRandom = new();
    /// <summary>The level Game Settings' level cards are about (the open level; during a load, the one being opened).</summary>
    private Spyro.Editor.Core.Levels.LevelDefinition? _settingsLevel;
    private string _creaturePlaced = "";

    /// <summary>The kinds the card lists (key and name) and the colours chosen for the open level.</summary>
    internal IReadOnlyList<(string Key, string Name)> CreatureKindsForTesting => [.. _creatureKinds.Select(k => (k.Key, k.Name))];
    internal IReadOnlyDictionary<string, (byte R, byte G, byte B)> CreatureColoursForTesting => _creatureColours;
    internal Task CreatureKindsReadyForTesting => _creatureLoad;
    internal string CreatureColourNoteForTesting => _creatureColourNote.Text ?? "";
    internal void RandomizeCreatureColoursForTesting(int seed) { _creatureRandom = new Random(seed); RandomizeCreatureColours(); }
    internal void SetCreatureColourForTesting(string key, (byte R, byte G, byte B)? colour) => SetCreatureColour(_creatureKinds.First(k => k.Key == key), colour);
    internal void ClearCreatureColoursForTesting() => ClearCreatureColours();

    private Control BuildCreatureColourSection()
    {
        StackPanel content = new() { Name = "CreatureColourPanel", Spacing = 8, Margin = new Thickness(2, 4, 2, 2) };
        content.Children.Add(new TextBlock
        {
            Text = "Give this level's enemies colours of their own. A colour chosen here is for this level only, so the same kind of " +
                   "enemy can be red in one level and blue in the next. Each keeps its shading; what is of another colour on it (skin, armour, eyes) stays.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        _creatureRandomize = NewButton("Randomize", RandomizeCreatureColours);
        _creatureRandomize.Name = "CreatureColourRandomize";
        ToolTip.SetTip(_creatureRandomize, "Another colour for every kind of enemy in this level; click again for others");
        _creatureOwn = NewButton("Their Own Colours", ClearCreatureColours);
        _creatureOwn.Name = "CreatureColourOwn";
        ToolTip.SetTip(_creatureOwn, "Every enemy of this level in its own colours again");
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _creatureRandomize, _creatureOwn } });
        content.Children.Add(_creatureColourRows);
        _creatureColourNote.Foreground = new SolidColorBrush(ModernMutedInk);
        content.Children.Add(_creatureColourNote);
        RefreshCreatureColours();
        var section = BuildModernDisclosure("Creatures' colours", content, false);
        section.Name = "CreatureColourSection";
        return section;
    }

    private static int CreatureClass(Spyro.Editor.Core.Scene.Moby m) => m.SourceByte36 | m.SourceByte37 << 8;

    /// <summary>The enemy kinds the open level has now: the level a kind's model comes from (the level itself, or for an
    /// object brought in from another level, that level), its class there, and its name without what one of it gives.
    /// Read from the level's own objects, so it needs nothing but the level open.</summary>
    private List<(string Source, int Class, string Name)> OpenLevelEnemyKinds(Spyro.Editor.Core.Levels.LevelDefinition level)
    {
        if (_currentLevel?.Key != level.Key) return [];   // still loading: the objects are the level's before
        bool built = _freshSceneDocument != null;
        return [.. _currentMobys.Where(m => !m.IsRemoved && (m.CrossLevelSourceLevelKey.Length > 0 || !built))
            .Where(m => UniversalMobyCatalogBuilder.Categorize(m.DisplayLabel, m.CandidateKind).Category == "Enemies")
            .Select(m => (Source: (m.CrossLevelSourceLevelKey.Length > 0 ? m.CrossLevelSourceLevelKey : level.Key).ToLowerInvariant(), Class: CreatureClass(m),
                Name: System.Text.RegularExpressions.Regex.Replace(m.DisplayLabel, @"\s*\([^)]*reward\)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim()))
            .GroupBy(k => (k.Source, k.Class))
            .Select(g => (g.Key.Source, g.Key.Class, g.GroupBy(k => k.Name).OrderByDescending(n => n.Count()).ThenBy(n => n.Key, StringComparer.Ordinal).First().Key))
            .OrderBy(k => k.Source, StringComparer.Ordinal).ThenBy(k => k.Class)];
    }

    /// <summary>Shows the open level's enemy kinds and their saved colours (on level and project changes, when the
    /// level's objects change, and after each choice).</summary>
    private void RefreshCreatureColours()
    {
        int generation = ++_creatureGeneration;
        var level = _settingsLevel ?? _currentLevel;
        string root = _workspace.RootPath;
        _creatureLevelKey = level?.Key ?? "";
        _creatureError = "";
        try { _creatureColours = level != null && CreatureColourSettings.Load(root).TryGetValue(level.Key, out var saved) ? saved : new(StringComparer.OrdinalIgnoreCase); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _creatureColours = new(StringComparer.OrdinalIgnoreCase); _creatureError = e.Message; }
        var wanted = level == null ? [] : OpenLevelEnemyKinds(level);
        _creaturePlaced = string.Join(",", wanted.Select(w => $"{w.Source}:{w.Class:x}"));
        string disc = ResolveUniversalSourceDisc();
        if (level == null || wanted.Count == 0 || string.IsNullOrWhiteSpace(disc) || !File.Exists(disc)) { _creatureKinds = []; _creatureLoad = Task.CompletedTask; RebuildCreatureRows(); return; }
        var catalog = _retailCatalog;
        _creatureLoad = Task.Run(() =>
        {
            UniversalSourceLibrary.RequireOriginal(disc);
            var library = _worldEntranceOriginals is { } cached && cached.ImagePath == disc ? cached : _worldEntranceOriginals = new UniversalSourceLibrary(disc, catalog);
            List<CreatureKind> kinds = [];
            foreach (var group in wanted.GroupBy(w => w.Source))
            {
                if (catalog.FindByKey(group.Key) is not { } source) continue;   // creatures of another game have no colour to choose here
                byte[] bytes;
                lock (library) bytes = library.Disc.EntryBytes(source.SourceWadEntry);
                var found = NativeCreatureColours.Kinds(bytes).GroupBy(k => k.ClassId).ToDictionary(g => g.Key, g => g.First());
                foreach (var w in group)
                    if (found.TryGetValue(w.Class, out var kind))
                        kinds.Add(new(CreatureColourSettings.KindKey(source.Key, w.Class), w.Name, kind.Model, kind.Colour, bytes));
            }
            return kinds.OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase).ThenBy(k => k.Key, StringComparer.Ordinal).ToList();
        }).ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (generation != _creatureGeneration) return;
            _creatureKinds = t.IsCompletedSuccessfully ? t.Result : [];
            RebuildCreatureRows();
        }), TaskScheduler.Default);
        RebuildCreatureRows();
    }

    /// <summary>After the level's objects were read, added to or taken away: the card lists the kinds it has now.</summary>
    private void RefreshCreatureKindsIfChanged()
    {
        if (_settingsLevel is not { } level || _currentLevel?.Key != level.Key) return;
        if (string.Join(",", OpenLevelEnemyKinds(level).Select(w => $"{w.Source}:{w.Class:x}")) != _creaturePlaced) RefreshCreatureColours();
    }

    private void RebuildCreatureRows()
    {
        _creatureColourRows.Children.Clear();
        foreach (var kind in _creatureKinds) _creatureColourRows.Children.Add(BuildCreatureRow(kind));
        int chosen = _creatureKinds.Count(k => _creatureColours.ContainsKey(k.Key));
        var open = _settingsLevel ?? _currentLevel;
        string where = open?.DisplayName ?? "this level";
        if (_creatureRandomize != null) _creatureRandomize.IsEnabled = _creatureKinds.Count > 0;
        if (_creatureOwn != null) _creatureOwn.IsEnabled = _creatureColours.Count > 0;
        string disc = ResolveUniversalSourceDisc();
        _creatureColourNote.Text = _creatureError.Length > 0
            ? "The saved colours could not be read, so Create BIN stops until you choose again (Their Own Colours clears them): " + _creatureError
            : string.IsNullOrWhiteSpace(disc) || !File.Exists(disc) ? "Open your game first (Open Game…): the enemies are read from it."
            : open == null ? "Open a level to choose its enemies' colours."
            : _creatureKinds.Count == 0 ? (!_creatureLoad.IsCompleted ? "Reading the level's enemies…"
                : _freshSceneDocument != null ? $"{where} has no enemies yet. Add some (Objects › Add Object, or Random objects) and they are listed here."
                : $"{where} has no enemies whose colour can be changed.")
            : chosen == 0 ? $"{where}'s enemies have their own colours."
            : $"{chosen} of {where}'s {Count(_creatureKinds.Count, "kind", "kinds")} of enemy {(chosen == 1 ? "has" : "have")} a chosen colour. Create BIN puts {(chosen == 1 ? "it" : "them")} in the game.";
    }

    private Control BuildCreatureRow(CreatureKind kind)
    {
        bool changed = _creatureColours.TryGetValue(kind.Key, out var chosen);
        var shown = changed ? chosen : kind.Own;
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10, Name = "CreatureColourRow_" + kind.Key.Replace(':', '_') };
        // The creature in the colour it will have.
        Image picture = new() { Width = 56, Height = 56, Stretch = Stretch.Uniform };
        Border frame = new()
        {
            Width = 60, Height = 60, CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#DCEAF5")), Child = picture
        };
        DrawCreature(kind, changed ? chosen : null, picture);
        row.Children.Add(frame);
        Button swatch = new()
        {
            Name = "CreatureColourSwatch_" + kind.Key.Replace(':', '_'), Width = 46, Height = 28, Padding = new Thickness(3), CornerRadius = new CornerRadius(6), VerticalAlignment = VerticalAlignment.Center,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(shown.R, shown.G, shown.B)), CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), BorderThickness = new Thickness(1)
            },
            Flyout = BuildCreatureColourPicker(kind, shown)
        };
        ToolTip.SetTip(swatch, $"Choose the colour of {kind.Name} in this level");
        Avalonia.Automation.AutomationProperties.SetName(swatch, $"{kind.Name} colour");
        Grid.SetColumn(swatch, 1);
        row.Children.Add(swatch);
        StackPanel label = new() { VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = kind.Name, FontSize = 13, FontWeight = changed ? FontWeight.SemiBold : FontWeight.Normal, Foreground = new SolidColorBrush(ModernInk), TextWrapping = TextWrapping.Wrap });
        label.Children.Add(new TextBlock { Text = changed ? SpyroColourSettings.Format(chosen) : "Its own colour", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk) });
        Grid.SetColumn(label, 2);
        row.Children.Add(label);
        if (changed)
        {
            var reset = NewButton("Its own", () => SetCreatureColour(kind, null));
            reset.Name = "CreatureColourReset_" + kind.Key.Replace(':', '_');
            reset.VerticalAlignment = VerticalAlignment.Center;
            ToolTip.SetTip(reset, $"Put back {kind.Name}'s own colour in this level");
            Grid.SetColumn(reset, 3);
            row.Children.Add(reset);
        }
        return row;
    }

    /// <summary>Draws a creature in a colour (null: its own), in the background.</summary>
    private static void DrawCreature(CreatureKind kind, (byte R, byte G, byte B)? colour, Image picture)
    {
        Task.Run(() =>
        {
            if (NativeCreatureColours.PreviewModel(kind.SourceLevel, kind.Model, colour) is not { } model) return null;
            try { return UniversalMobyModelPreview.Decode(model.Package, model.Vram).Pixels; }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { return null; }
        }).ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!t.IsCompletedSuccessfully || t.Result is not { } pixels) return;
            const int size = UniversalMobyModelPreview.Size;
            WriteableBitmap bitmap = new(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var buffer = bitmap.Lock())
                for (int y = 0; y < size; y++) Marshal.Copy(pixels, y * size * 4, buffer.Address + y * buffer.RowBytes, size * 4);
            picture.Source = bitmap;
        }), TaskScheduler.Default);
    }

    /// <summary>The picker: quick colours and #RRGGBB.</summary>
    private Flyout BuildCreatureColourPicker(CreatureKind kind, (byte R, byte G, byte B) start)
    {
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        StackPanel panel = new() { Spacing = 8, Margin = new Thickness(4), Width = 232 };
        panel.Children.Add(new TextBlock { Text = kind.Name, FontWeight = FontWeight.SemiBold });
        WrapPanel quick = new() { Orientation = Orientation.Horizontal };
        foreach (string hexColour in SpyroQuickColours)
        {
            if (!SpyroColourSettings.TryParse(hexColour, out var c)) continue;
            Button chip = new()
            {
                Width = 24, Height = 24, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), BorderThickness = new Thickness(1)
            };
            ToolTip.SetTip(chip, hexColour);
            chip.Click += (_, _) => { flyout.Hide(); SetCreatureColour(kind, c); };
            quick.Children.Add(chip);
        }
        panel.Children.Add(quick);
        TextBox hex = new() { Name = "CreatureColourHex", Text = SpyroColourSettings.Format(start), Width = 96, MaxLength = 7 };
        Button use = NewButton("Use", () =>
        {
            if (!SpyroColourSettings.TryParse(hex.Text, out var c)) { _statusText.Text = "A colour is written #RRGGBB, for example #C0282D."; return; }
            flyout.Hide(); SetCreatureColour(kind, c);
        });
        use.Name = "CreatureColourUse";
        Button own = NewButton("Its Own Colour", () => { flyout.Hide(); SetCreatureColour(kind, null); });
        own.Name = "CreatureColourPickerOwn";
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { hex, use, own } });
        flyout.Content = panel;
        return flyout;
    }

    private void SetCreatureColour(CreatureKind kind, (byte R, byte G, byte B)? colour)
    {
        var colours = new Dictionary<string, (byte R, byte G, byte B)>(_creatureError.Length > 0 ? [] : _creatureColours, StringComparer.OrdinalIgnoreCase);
        if (colour is { } c) colours[kind.Key] = c; else colours.Remove(kind.Key);
        string where = (_settingsLevel ?? _currentLevel)?.DisplayName ?? "this level";
        SaveCreatureColours(colours, colour is { } chosen
            ? $"{kind.Name} in {where}: {SpyroColourSettings.Format(chosen)}. Create BIN puts it in the game."
            : $"{kind.Name} has its own colour again in {where}.");
    }

    /// <summary>Another hue for every kind (its own light and richness kept), no two of a level close together.</summary>
    private void RandomizeCreatureColours()
    {
        if (_creatureKinds.Count == 0) return;
        Dictionary<string, (byte R, byte G, byte B)> colours = new(StringComparer.OrdinalIgnoreCase);
        double start = _creatureRandom.NextDouble(), step = 1.0 / Math.Max(3, _creatureKinds.Count);
        int[] order = [.. Enumerable.Range(0, _creatureKinds.Count).OrderBy(_ => _creatureRandom.Next())];
        for (int n = 0; n < order.Length; n++)
        {
            var kind = _creatureKinds[order[n]];
            double hue = start + n * step + (_creatureRandom.NextDouble() - 0.5) * step * 0.4;
            // Not the hue it has already: a turned colour should be seen.
            var (own, _, _) = NativeSpyroColours.ToHsl(kind.Own.R / 255.0, kind.Own.G / 255.0, kind.Own.B / 255.0);
            double apart = Math.Abs(((hue - own) % 1 + 1.5) % 1 - 0.5);
            if (apart < 0.1) hue += 0.2;
            colours[kind.Key] = NativeCreatureColours.WithHue(kind.Own, hue);
        }
        string where = (_settingsLevel ?? _currentLevel)?.DisplayName ?? "this level";
        SaveCreatureColours(colours, $"New colours for {where}'s enemies. Click Randomize again for others, or choose any of them below. Create BIN puts them in the game.");
    }

    private void ClearCreatureColours() =>
        SaveCreatureColours(new(StringComparer.OrdinalIgnoreCase), $"{(_settingsLevel ?? _currentLevel)?.DisplayName ?? "This level"}'s enemies have their own colours again.");

    private void SaveCreatureColours(Dictionary<string, (byte R, byte G, byte B)> colours, string status)
    {
        try
        {
            if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
            var level = _settingsLevel ?? _currentLevel;
            if (level == null || level.Key != _creatureLevelKey) throw new InvalidOperationException("Open the level first.");
            if (_creatureError.Length > 0 && File.Exists(CreatureColourSettings.PathFor(_workspace.RootPath))) File.Delete(CreatureColourSettings.PathFor(_workspace.RootPath));
            CreatureColourSettings.SaveLevel(_workspace.RootPath, level.Key, colours);
            EditorDiagnostics.RecordAction("Creatures' colours", $"{level.Key}: " + string.Join(", ", colours.Select(c => $"{c.Key} {SpyroColourSettings.Format(c.Value)}")));
            _statusText.Text = status;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or System.Text.Json.JsonException)
        {
            EditorDiagnostics.RecordException("saving creatures' colours", error);
            _statusText.Text = "Could not save the creatures' colours: " + error.Message;
        }
        // The kinds are the same: only their colours are read again.
        _creatureError = "";
        try { _creatureColours = (_settingsLevel ?? _currentLevel) is { } now && CreatureColourSettings.Load(_workspace.RootPath).TryGetValue(now.Key, out var saved) ? saved : new(StringComparer.OrdinalIgnoreCase); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _creatureColours = new(StringComparer.OrdinalIgnoreCase); _creatureError = e.Message; }
        RebuildCreatureRows();
    }

    /// <summary>Create BIN's creature colours: every level's chosen kinds, each found by what its model is.</summary>
    private NativeCreatureColours.Scope UseProjectCreatureColours(Func<int, string?> levelOfEntry)
    {
        Dictionary<string, Dictionary<string, (byte R, byte G, byte B)>> saved;
        try { saved = CreatureColourSettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { throw new InvalidDataException("The creatures' colours in Game Settings could not be read. Choose them again (Their Own Colours clears a level's). (" + e.Message + ")", e); }
        if (saved.Count == 0) return NativeCreatureColours.Use(null, levelOfEntry);
        string disc = ResolveUniversalSourceDisc();
        if (string.IsNullOrWhiteSpace(disc) || !File.Exists(disc)) throw new InvalidDataException("Creatures' colours need your original Spyro the Dragon (USA) disc: choose it with Open Game….");
        var library = _universalCatalog is { } ready && PathsEqual(ready.Path, disc) ? ready.Library : new UniversalSourceLibrary(disc, _retailCatalog);
        Dictionary<string, IReadOnlyList<NativeCreatureColours.Recipe>> levels = new(StringComparer.OrdinalIgnoreCase);
        // A kind's colours are worked out from the game's own level it comes from, once for each kind and colour.
        Dictionary<(string, int, (byte, byte, byte)), NativeCreatureColours.Recipe?> made = [];
        foreach (var (level, kinds) in saved)
        {
            List<NativeCreatureColours.Recipe> recipes = [];
            foreach (var (kind, colour) in kinds)
            {
                if (!CreatureColourSettings.TryParseKind(kind, out string sourceKey, out int classId) || _retailCatalog.FindByKey(sourceKey) is not { } source) continue;
                if (!made.TryGetValue((source.Key, classId, colour), out var recipe))
                {
                    lock (library)
                    {
                        try
                        {
                            byte[] bytes = library.Disc.EntryBytes(source.SourceWadEntry);
                            var model = NativeCreatureColours.Kinds(bytes).FirstOrDefault(k => k.ClassId == classId)?.Model;
                            recipe = model == null ? null : NativeCreatureColours.RecipeFor(bytes, model, colour);
                        }
                        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException) { recipe = null; }
                    }
                    made[(source.Key, classId, colour)] = recipe;
                }
                if (recipe != null) recipes.Add(recipe);
            }
            if (recipes.Count > 0) levels[level] = recipes;
        }
        return NativeCreatureColours.Use(levels, levelOfEntry);
    }
}
