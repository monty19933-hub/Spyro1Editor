using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

/// <summary>
/// The editor laid out by what the user is doing (2.5): the game's worlds and levels down the left ("Your game"), a
/// search that finds any command by name, Create and the project's commands in the top bar, what is selected at the
/// top of the side panel whatever tab is open, and five tabs: Ground, Objects, Look (this level's sky, colours, music
/// and name), Links (how this level is reached and left: doorways, Return Home, the world entrance, the balloon route)
/// and Game (what applies to the whole game: Spyro, the egg thieves, moves, memory).
/// </summary>
public sealed partial class MainWindow
{
    private TabItem? _modernLinksWorkspaceTab;
    private TabItem? _modernGameWorkspaceTab;
    private Button? _toolbarCreateMenu;
    private Control? _terrainGroundCard, _terrainTilesCard, _terrainDoorwaysCard, _terrainWorldEntranceCard, _terrainBrushCard, _terrainTexturesCard;

    /// <summary>Create ▾: everything that makes something new.</summary>
    private ToolbarAction[] CreateActions() =>
    [
        new("New Level…", NewMainLevelAsync),
        new("Random Level…", RandomLevelAsync),
        new("Random Homeworld…", RandomHomeworldAsync),
        new("New World…", NewWorldAsync),
        new("New Level from Spyro: Year of the Dragon…", NewLevelFromYearOfTheDragonAsync),
    ];

    /// <summary>☰: the project and the editor: open, save, start over or restore, checks, updates, help.</summary>
    private ToolbarAction[] ProjectActions()
    {
        List<ToolbarAction> actions =
        [
            new("Open Game…", ChooseSourceDiscImageAsync),
            new("Save Level", SaveFromToolbarAsync),
            new("Open Created Level…", OpenMainFreshLevelAsync),
            MenuSeparator,
            new("Start Blank in Current Level…", StartBlankCurrentLevelAsync),
            new("Fill In What the Level Needs…", FillInLevelAsync),
            MenuSeparator,
            new("Restore Before Clear…", RestoreBeforeClearAsync),
            new("Free Deleted Objects' Save Rows…", FreeRetiredSaveRowsAsync),
            new("Check for Export Issues…", ShowBuildSafetyInspectorAsync),
            new("Left Out of the Game…", ShowLeftOutAsync),
            MenuSeparator,
            new(ClearProjectCommand, ClearEntireProjectAsync),
            MenuSeparator,
            new("Project Data", ShowProjectDataAsync),
            new("Check for Updates", ShowUpdatesAsync),
            new("Help", ShowHelpAsync),
        ];
        if (!_releaseMode) actions.Insert(actions.Count - 1, new ToolbarAction("Diagnostics", ShowDiagnosticsAsync));
        return [.. actions];
    }

    /// <summary>What the search finds: every menu command, every card of every tab, and (when typed) every level.</summary>
    private void RegisterEditorCommands()
    {
        _editorCommands.Clear();
        foreach (var a in CreateActions()) AddEditorCommand(a.Label, "Create", a.Execute);
        foreach (var a in ProjectActions().Where(a => !ReferenceEquals(a, MenuSeparator))) AddEditorCommand(a.Label, "☰ menu", a.Execute);
        AddEditorCommand("Create BIN", "Top bar · play the game you made", CreateObjectTestBinAsync);
        AddEditorCommand("Level Links…", "Links tab · every doorway and Return Home", LevelLinksAsync);
        AddEditorCommand("World's Balloon Route…", "Links tab · balloonists", BalloonRouteAsync);
        AddEditorPlace("Paint the ground", "Ground", _modernTerrainWorkspaceTab, _terrainGroundCard, "grass dirt stone sand snow water lava goo");
        AddEditorPlace("Tiles", "Ground", _modernTerrainWorkspaceTab, _terrainTilesCard, "move tile corner type add terrain remove");
        AddEditorPlace("Brush", "Ground", _modernTerrainWorkspaceTab, _terrainBrushCard, "raise lower smooth level hills");
        AddEditorPlace("Textures", "Ground", _modernTerrainWorkspaceTab, _terrainTexturesCard, "texture picture paint");
        AddEditorPlace("Add an object", "Objects", _modernObjectWorkspaceTab, _modernObjectWorkspaceTab?.Content as Control, "gem egg sign enemy chest");
        AddEditorPlace("Random objects", "Objects", _modernObjectWorkspaceTab, _modernObjectWorkspaceTab?.Content as Control, "randomize enemies chests gems difficulty");
        AddEditorPlace("Level name", "Look", _modernLevelWorkspaceTab, _retailNameDisclosure, "rename title");
        AddEditorPlace("Music", "Look", _modernLevelWorkspaceTab, _retailMusicDisclosure, "song track");
        AddEditorPlace("Sky and lighting", "Look", _modernLevelWorkspaceTab, _lookPlaces.Sky, "skybox colours sky import");
        AddEditorPlace("This level's colours", "Look", _modernLevelWorkspaceTab, _lookPlaces.Colours, "enemies chests dragons animals randomize");
        AddEditorPlace("Doorways", "Links", _modernLinksWorkspaceTab, _terrainDoorwaysCard, "homeworld portal leads to shape");
        AddEditorPlace("World entrance", "Links", _modernLinksWorkspaceTab, _terrainWorldEntranceCard, "new world doorway artisans");
        AddEditorPlace("Portals and Return Home", "Links", _modernLinksWorkspaceTab, _homeworldTextGroup, "return home vortex exit");
        AddEditorPlace("Spyro's colours", "Game", _modernGameWorkspaceTab, _modernGameWorkspaceTab?.Content as Control, "spyro colour skin");
        AddEditorPlace("Egg thieves' colour", "Game", _modernGameWorkspaceTab, _modernGameWorkspaceTab?.Content as Control, "thief colour");
        AddEditorPlace("Hover, Fly Anywhere, Play as Crash", "Game", _modernGameWorkspaceTab, _modernGameWorkspaceTab?.Content as Control, "moves fly crash hover");
        AddEditorPlace("Memory (2 MB or 8 MB)", "Game", _modernGameWorkspaceTab, _modernGameWorkspaceTab?.Content as Control, "emulator playstation");
        AddEditorPlace("Export check", "Game", _modernGameWorkspaceTab, _exportCheckDisclosure, "problems fix");
    }

    // ---------------------------------------------------------------- Your game

    private StackPanel? _gameNavigatorRows;
    private TextBox? _gameNavigatorFilter;
    private readonly Dictionary<int, bool> _gameNavigatorOpen = [];
    private static readonly string[] GameWorldNames = ["", "Artisans", "Peace Keepers", "Magic Crafters", "Beast Makers", "Dream Weavers", "Gnasty's World"];

    /// <summary>The game's homeworlds with their levels, then the project's own world and created levels: click one to
    /// open it. Levels built in a place say so.</summary>
    private Control BuildGameNavigator()
    {
        _gameNavigatorRows = new StackPanel { Name = "GameNavigatorRows", Spacing = 1 };
        _gameNavigatorFilter = new TextBox { Name = "GameNavigatorFilter", PlaceholderText = "Find a level", MinHeight = 30, FontSize = 12 };
        _gameNavigatorFilter.TextChanged += (_, _) => RefreshGameNavigator();
        _levelJumpBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == SelectingItemsControl.SelectedItemProperty)
                RefreshGameNavigator();
            // Levels can be opened from the list exactly when the (hidden) picker could: not before a game is open,
            // not while something is being made or saved.
            if (e.Property == InputElement.IsEnabledProperty && _gameNavigatorRows != null)
                _gameNavigatorRows.IsEnabled = _levelJumpBox.IsEnabled;
        };
        Grid layout = new()
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            RowSpacing = 8
        };
        layout.Children.Add(new TextBlock { Text = "Your game", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(ModernInk) });
        Grid.SetRow(_gameNavigatorFilter, 1);
        layout.Children.Add(_gameNavigatorFilter);
        var scroll = new ScrollViewer { Content = _gameNavigatorRows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 2);
        layout.Children.Add(scroll);
        RefreshGameNavigator();
        _gameNavigatorRows.IsEnabled = _levelJumpBox.IsEnabled;
        return new Border
        {
            Name = "GameNavigator",
            Background = new SolidColorBrush(ModernPanel),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(10, 12, 8, 10),
            Child = layout
        };
    }

    private void RefreshGameNavigator()
    {
        if (_gameNavigatorRows == null)
            return;
        _gameNavigatorRows.Children.Clear();
        var levels = (_levelJumpBox.ItemsSource as IEnumerable<LevelDefinition>)?.ToList() ?? [];
        if (levels.Count == 0)
        {
            _gameNavigatorRows.Children.Add(TerrainNote().With("Open your game (☰ › Open Game…) to see its worlds and levels here."));
            return;
        }
        string filter = _gameNavigatorFilter?.Text?.Trim() ?? "";
        bool Match(LevelDefinition l) => filter.Length == 0 || l.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
        // The game's six worlds by their ids (10 Artisans … 60 Gnasty's World), then the project's world (70s), then
        // anything else (a created level in a spare place).
        var byWorld = levels.Where(l => l.LevelId is >= 10 and < 80).GroupBy(l => l.LevelId / 10).OrderBy(g => g.Key);
        foreach (var world in byWorld)
        {
            var members = world.OrderBy(l => l.LevelId).ToList();
            var home = members.FirstOrDefault(l => l.LevelId % 10 == 0);
            string title = world.Key < GameWorldNames.Length ? GameWorldNames[world.Key] : home?.ToString() ?? "Your world";
            bool yours = world.Key >= 7;
            if (yours && home != null) title = home.ToString();
            var shown = members.Where(Match).ToList();
            if (shown.Count == 0 && !(filter.Length > 0 && title.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (shown.Count == 0) shown = members;
            bool holdsCurrent = members.Any(l => ReferenceEquals(l, _currentLevel) || l.Key == _currentLevel?.Key);
            bool open = filter.Length > 0 || (_gameNavigatorOpen.TryGetValue(world.Key, out bool o) ? o : holdsCurrent || yours);
            StackPanel rows = new() { Spacing = 1, Margin = new Thickness(10, 2, 0, 4) };
            foreach (var level in shown)
                rows.Children.Add(GameNavigatorRow(level, level.LevelId % 10 == 0 ? (yours ? "your world's homeworld" : "homeworld") : null));
            // A world: a header that opens and closes it, its levels under it.
            rows.IsVisible = open;
            var header = new Button
            {
                Name = "GameNavigatorWorld" + world.Key,
                Content = new TextBlock
                {
                    Text = (open ? "▾  " : "▸  ") + (yours ? title + "  (your world)" : title),
                    FontSize = 12, FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(yours ? ModernTeal : ModernInk)
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4, 4), MinHeight = 26,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0)
            };
            int key = world.Key;
            header.Click += (_, _) =>
            {
                _gameNavigatorOpen[key] = !rows.IsVisible;
                RefreshGameNavigator();
            };
            _gameNavigatorRows.Children.Add(header);
            _gameNavigatorRows.Children.Add(rows);
        }
        var others = levels.Where(l => l.LevelId is < 10 or >= 80).Where(Match).ToList();
        if (others.Count > 0)
        {
            _gameNavigatorRows.Children.Add(new TextBlock { Text = "Created levels", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(ModernInk), Margin = new Thickness(4, 8, 0, 2) });
            foreach (var level in others) _gameNavigatorRows.Children.Add(GameNavigatorRow(level, null));
        }
    }

    private Control GameNavigatorRow(LevelDefinition level, string? note)
    {
        bool current = level.Key == _currentLevel?.Key;
        string? built = null;
        try
        {
            if (_workspace != null && File.Exists(SceneDocumentStore.PathForTarget(_workspace.RootPath, level.Key))) built = "built";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        var text = new TextBlock { Text = level.ToString(), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var tag = new TextBlock
        {
            Text = string.Join(" · ", new[] { built, note }.Where(s => s != null)), FontSize = 10,
            Foreground = new SolidColorBrush(current ? ModernBlue : ModernMutedInk), VerticalAlignment = VerticalAlignment.Center
        };
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6 };
        row.Children.Add(text);
        Grid.SetColumn(tag, 1);
        row.Children.Add(tag);
        var button = new Button
        {
            Name = "GameNavigatorLevel_" + level.Key,
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(6, 3),
            MinHeight = 24,
            Background = current ? new SolidColorBrush(Color.FromRgb(222, 234, 248)) : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Foreground = new SolidColorBrush(current ? ModernBlue : ModernInk),
            FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal
        };
        ToolTip.SetTip(button, current ? $"{level} is open." : $"Open {level}.");
        button.Click += (_, _) =>
        {
            if (level.Key != _currentLevel?.Key) _levelJumpBox.SelectedItem = level;
        };
        return button;
    }

    // ---------------------------------------------------------------- Find any command

    /// <summary>A command the search finds: its name, where it lives (shown under it), and what it does.</summary>
    private sealed record EditorCommand(string Title, string Where, Func<Task> Run)
    {
        public override string ToString() => Title;
    }

    private readonly List<EditorCommand> _editorCommands = [];
    private TextBox? _commandSearchBox;
    private Popup? _commandPopup;
    private ListBox? _commandList;

    private void AddEditorCommand(string title, string where, Func<Task> run) => _editorCommands.Add(new(title, where, run));

    /// <summary>A card of a tab the search can take the user to ("Doorways" → the Links tab, scrolled to it).</summary>
    private void AddEditorPlace(string title, string tabName, TabItem? tab, Control? card, string keywords = "")
    {
        if (tab == null || card == null)
            return;
        _editorCommands.Add(new(title, tabName + " tab" + (keywords.Length > 0 ? " · " + keywords : ""), () =>
        {
            SelectEditorToolTab(tab);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => card.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
            return Task.CompletedTask;
        }));
    }

    private Control BuildCommandSearch()
    {
        _commandSearchBox = new TextBox
        {
            Name = "CommandSearchBox",
            PlaceholderText = OperatingSystem.IsMacOS() ? "Find any command  ⌘K" : "Find any command  Ctrl+K",
            MinWidth = 220, MaxWidth = 340, MinHeight = 34, FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _commandList = new ListBox
        {
            Name = "CommandSearchResults",
            MaxHeight = 380, MinWidth = 340,
            ItemTemplate = new FuncDataTemplate<EditorCommand>((command, _) => new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = command?.Title, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(ModernInk) },
                    new TextBlock { Text = command?.Where, FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk) }
                }
            })
        };
        _commandPopup = new Popup
        {
            PlacementTarget = _commandSearchBox,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Background = Brushes.White, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Child = _commandList
            }
        };
        _commandSearchBox.TextChanged += (_, _) => RefreshCommandResults();
        _commandSearchBox.GotFocus += (_, _) => RefreshCommandResults();
        _commandSearchBox.LostFocus += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_commandSearchBox?.IsFocused != true && _commandList?.IsKeyboardFocusWithin != true && _commandPopup != null)
                _commandPopup.IsOpen = false;
        }, Avalonia.Threading.DispatcherPriority.Background);
        _commandSearchBox.KeyDown += async (_, e) =>
        {
            if (_commandList == null) return;
            switch (e.Key)
            {
                case Key.Down:
                    if (_commandList.ItemCount > 0) _commandList.SelectedIndex = Math.Min(_commandList.ItemCount - 1, _commandList.SelectedIndex + 1);
                    e.Handled = true; break;
                case Key.Up:
                    if (_commandList.ItemCount > 0) _commandList.SelectedIndex = Math.Max(0, _commandList.SelectedIndex - 1);
                    e.Handled = true; break;
                case Key.Enter:
                    e.Handled = true;
                    await RunEditorCommandAsync(_commandList.SelectedItem as EditorCommand ?? _commandList.Items.OfType<EditorCommand>().FirstOrDefault());
                    break;
                case Key.Escape:
                    CloseCommandSearch(); e.Handled = true; break;
            }
        };
        _commandList.Tapped += async (_, _) => await RunEditorCommandAsync(_commandList.SelectedItem as EditorCommand);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.K && (e.KeyModifiers == KeyModifiers.Meta || e.KeyModifiers == KeyModifiers.Control))
            {
                _commandSearchBox?.Focus();
                _commandSearchBox?.SelectAll();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        ToolTip.SetTip(_commandSearchBox, "Type what you want to do (a doorway, the music, a random level, Stone Hill…) and press Enter.");
        return new Panel { VerticalAlignment = VerticalAlignment.Center, Children = { _commandSearchBox, _commandPopup } };
    }

    /// <summary>The commands whose name or place holds every word typed, the closest first; levels to open too.</summary>
    internal IReadOnlyList<string> FindEditorCommandsForTesting(string text) => MatchEditorCommands(text).Select(c => c.Title + " | " + c.Where).ToList();

    private List<EditorCommand> MatchEditorCommands(string text)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var all = new List<EditorCommand>(_editorCommands);
        foreach (var level in (_levelJumpBox.ItemsSource as IEnumerable<LevelDefinition>) ?? [])
        {
            var l = level;
            all.Add(new("Open " + level, "Your game", () => { if (l.Key != _currentLevel?.Key) _levelJumpBox.SelectedItem = l; return Task.CompletedTask; }));
        }
        if (words.Length == 0)
            return [.. all.Take(14)];
        static string Fold(string s) => s.Replace("…", "").Replace("'", "").Replace("’", "");
        return [.. all
            .Where(c => words.All(w => Fold(c.Title + " " + c.Where).Contains(Fold(w), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Title.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : c.Title.Contains(words[0], StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(c => c.Title.Length)
            .Take(14)];
    }

    private void RefreshCommandResults()
    {
        if (_commandList == null || _commandPopup == null || _commandSearchBox == null)
            return;
        var found = MatchEditorCommands(_commandSearchBox.Text ?? "");
        _commandList.ItemsSource = found;
        _commandList.SelectedIndex = found.Count > 0 ? 0 : -1;
        _commandPopup.IsOpen = _commandSearchBox.IsFocused && found.Count > 0;
    }

    private void CloseCommandSearch()
    {
        if (_commandPopup != null) _commandPopup.IsOpen = false;
        if (_commandSearchBox != null) _commandSearchBox.Text = "";
    }

    private async Task RunEditorCommandAsync(EditorCommand? command)
    {
        if (command == null)
            return;
        CloseCommandSearch();
        _viewport.Focus();
        try { await command.Run(); }
        catch (Exception ex)
        {
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordException(command.Title, ex);
            _statusText.Text = $"Could not complete {command.Title}: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- Selected

    private Border? _selectedBox;
    private StackPanel? _selectedObjectActions;
    private Border? _selectedElsewhere;
    private TextBlock? _selectedElsewhereText;
    private Button? _selectedElsewhereButton;

    /// <summary>What is selected, at the top of the side panel whatever tab is open: the object with everything that
    /// acts on it (Edit…, Replace…, Copy, Remove, its height, an egg thief's run and what it gives), or for a tile a
    /// way to its tools on the Ground tab.</summary>
    private Control BuildSelectedBox(Control summary, StackPanel objectActions)
    {
        _selectedObjectActions = objectActions;
        objectActions.Name = "SelectedObjectActions";
        _selectedElsewhereText = TerrainNote();
        _selectedElsewhereButton = NewButton("Show Tile Tools", () =>
        {
            SelectEditorToolTab(_modernTerrainWorkspaceTab);
        });
        _selectedElsewhereButton.Name = "SelectedShowTools";
        StyleModernSecondaryButton(_selectedElsewhereButton);
        _selectedElsewhere = new Border
        {
            IsVisible = false,
            Child = new StackPanel { Spacing = 6, Children = { _selectedElsewhereText, _selectedElsewhereButton } }
        };
        // Its heading folds it away (to give the tabs the room) and back.
        var body = new StackPanel { Spacing = 8, Children = { summary, objectActions, _selectedElsewhere } };
        var label = new Button
        {
            Name = "SelectedBoxFold",
            Content = new TextBlock { Text = "▾  Selected", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(ModernMutedInk) },
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 18
        };
        ToolTip.SetTip(label, "Fold what is selected away, or show it again.");
        label.Click += (_, _) =>
        {
            body.IsVisible = !body.IsVisible;
            ((TextBlock)label.Content!).Text = (body.IsVisible ? "▾  " : "▸  ") + "Selected" + (body.IsVisible ? "" : ": " + _selectionTitle.Text);
        };
        _selectionTitle.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty && !body.IsVisible) ((TextBlock)label.Content!).Text = "▸  Selected: " + _selectionTitle.Text;
        };
        // The details in two lines (the rest when pointed at); the title a little smaller than in a card.
        _selectionTitle.FontSize = 15;
        _selectionDetails.MaxLines = 2;
        _selectionDetails.TextTrimming = TextTrimming.CharacterEllipsis;
        _selectionDetails.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(_selectionDetails, string.IsNullOrWhiteSpace(_selectionDetails.Text) ? null : _selectionDetails.Text);
        };
        var content = new StackPanel { Spacing = 6, Children = { label, body } };
        _selectedBox = new Border
        {
            Name = "SelectedBox",
            Background = new SolidColorBrush(Color.FromRgb(246, 249, 252)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10),
            Child = new ScrollViewer
            {
                Content = content, MaxHeight = 300,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        // After the window has taken the selection in (ShowSelection).
        _viewport.SelectionChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshSelectedBox);
        RefreshSelectedBox();
        return _selectedBox;
    }

    private void RefreshSelectedBox()
    {
        if (_selectedObjectActions == null || _selectedElsewhere == null || _selectedElsewhereText == null)
            return;
        _selectedObjectActions.IsVisible = _selectedMoby != null;
        bool tile = _selectedMoby == null && _selectedTerrain != null;
        _selectedElsewhere.IsVisible = tile && !IsTerrainWorkspaceTabSelected();
        _selectedElsewhereText.Text = tile ? "Move it, change its corners or its kind, or add tiles beside it on the Ground tab." : "";
    }

    // ---------------------------------------------------------------- Tabs

    /// <summary>Look: this level's name, music, sky, light and colours (a built level's start and sky too).</summary>
    private Control BuildLookWorkspace()
    {
        StackPanel panel = new() { Name = "LookWorkspacePanel", Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("This level"));
        panel.Children.Add(new TextBlock
        {
            Text = "How this level looks and sounds. Kept in your project as soon as you apply it; Create BIN puts it in the game.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        panel.Children.Add(BuildFreshDocumentSettings());
        _retailNameDisclosure = BuildModernDisclosure("Level name", BuildLevelTextPanel(), true);
        _retailMusicDisclosure = BuildModernDisclosure("Music", BuildLevelMusicPanel(), false);
        panel.Children.Add(_retailNameDisclosure);
        panel.Children.Add(_retailMusicDisclosure);
        var sky = BuildModernDisclosure("Sky and lighting", BuildSkyboxPanel(), false);
        sky.Name = "LookSkySection";
        panel.Children.Add(sky);
        var colours = BuildCreatureColourSection();
        panel.Children.Add(colours);
        _levelDetails.TextWrapping = TextWrapping.Wrap;
        _levelDetails.Foreground = new SolidColorBrush(ModernMutedInk);
        _levelDetails.FontSize = 12;
        _levelDetails.LineHeight = 18;
        panel.Children.Add(BuildModernDisclosure("Level details", _levelDetails, false));
        _lookPlaces = (sky, colours);
        return panel;
    }

    private (Control Sky, Control Colours) _lookPlaces;

    /// <summary>Links: how Spyro reaches this level and leaves it (its doorway in a homeworld, Exit Level and Return
    /// Home), the doorways in a homeworld, the way into the project's world, and the whole game's map of links.</summary>
    private Control BuildLinksWorkspace(Control? doorways, Control? worldEntrance)
    {
        StackPanel panel = new() { Name = "LinksWorkspacePanel", Spacing = 12, Margin = new Thickness(0, 10, 0, 0) };
        var world = new StackPanel();
        var links = NewAsyncButton("Level Links…", LevelLinksAsync);
        links.Name = "LinksOpenLevelLinks";
        var balloon = NewAsyncButton("World's Balloon Route…", BalloonRouteAsync);
        balloon.Name = "LinksOpenBalloonRoute";
        var newWorld = NewAsyncButton("New World…", NewWorldAsync);
        newWorld.Name = "LinksNewWorld";
        foreach (var b in new[] { links, balloon, newWorld }) { StyleModernSecondaryButton(b); b.HorizontalAlignment = HorizontalAlignment.Stretch; }
        world.Children.Add(TerrainNote().With("Every doorway and Return Home in the game, with problems in plain words and one-press fixes; your world's balloon route; a world of your own."));
        world.Children.Add(links);
        world.Children.Add(balloon);
        world.Children.Add(newWorld);
        panel.Children.Add(TerrainCard("The whole game", world));
        if (doorways != null) panel.Children.Add(Detach(doorways));
        if (worldEntrance != null) panel.Children.Add(Detach(worldEntrance));
        _homeworldTextGroup = BuildModernDisclosure("Portals & Return Home", BuildPortalControlPanel(), true);
        panel.Children.Add(_homeworldTextGroup);
        return panel;
    }

    /// <summary>Game: what applies to the whole game Create BIN makes: Spyro's colours, the egg thieves', his moves,
    /// Fly Anywhere, Play as Crash, memory, and the checks before making it.</summary>
    private Control BuildGameWorkspace(Control exportCheck)
    {
        StackPanel panel = new() { Name = "GameWorkspacePanel", Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("The whole game"));
        panel.Children.Add(new TextBlock
        {
            Name = "GameSettingsWholeGameNote",
            Text = "For every level of the game you create. Saved as soon as you choose; Create BIN puts it in the game.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk)
        });
        panel.Children.Add(BuildSpyroColourSection());
        panel.Children.Add(BuildEggThiefColourSection());
        panel.Children.Add(BuildHoverSection());
        var flight = BuildModernDisclosure("Fly Anywhere", BuildFreeFlightSettingsPanel(), false);
        flight.Name = "FlyAnywhereSection";
        panel.Children.Add(flight);
        var crash = BuildModernDisclosure("Play as Crash", BuildCrashPlayerSettingsPanel(), false);
        crash.Name = "PlayAsCrashSection";
        panel.Children.Add(crash);
        panel.Children.Add(BuildMemoryModeSection());
        var before = ModernSectionHeading("Before you play");
        before.Margin = new Thickness(0, 12, 0, 0);
        panel.Children.Add(before);
        panel.Children.Add(exportCheck);
        if (!_releaseMode) panel.Children.Add(BuildNativeLevelReplacementDisclosure());
        if (!_releaseMode) panel.Children.Add(BuildId65BlankLevelLabDisclosure());
        return panel;
    }

    /// <summary>The top bar's badge: the export check in a few words, opening the full check.</summary>
    private Control BuildExportCheckBadge()
    {
        var text = new TextBlock { Name = "ExportCheckBadgeText", FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170, TextTrimming = TextTrimming.CharacterEllipsis };
        var badge = new Button
        {
            Name = "ExportCheckBadge", Content = text, MinHeight = 34, Padding = new Thickness(10, 4),
            Background = new SolidColorBrush(Color.FromRgb(240, 244, 247)), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(5)
        };
        void Refresh()
        {
            string full = _buildSafetySummaryText.Text ?? "";
            int stop = full.IndexOfAny(['.', ':', '\n']);
            text.Text = full.Length == 0 ? "Export check" : stop > 0 ? full[..stop] : full;
            text.Foreground = _buildSafetySummaryText.Foreground ?? new SolidColorBrush(ModernInk);
            ToolTip.SetTip(badge, full.Length == 0 ? "Check the level for anything that would stop Create BIN." : full + "\nClick for the full check.");
        }
        _buildSafetySummaryText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty || e.Property == TextBlock.ForegroundProperty) Refresh();
        };
        Refresh();
        badge.Click += async (_, _) =>
        {
            try { await ShowBuildSafetyInspectorAsync(); }
            catch (Exception ex) { _statusText.Text = "Could not open the export check: " + ex.Message; }
        };
        return badge;
    }
}

internal static class OverhaulTextExtensions
{
    public static TextBlock With(this TextBlock block, string text)
    {
        block.Text = text;
        return block;
    }
}
