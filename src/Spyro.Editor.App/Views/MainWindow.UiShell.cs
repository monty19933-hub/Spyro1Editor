using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private static readonly string[] EverydayMobyCategoryOptions =
    [
        "All objects",
        "Treasure Objects",
        "Gems",
        "Keys",
        "Chests",
        "Reward Chests",
        "Enemies",
        "Fodder",
        "Egg Thieves",
        "Dragons",
        "Transport",
        "Scenery",
        "Portals",
        "Whirlwinds",
        "Flight Targets",
        "Edited"
    ];

    private readonly ToggleButton _modernMapViewButton = new()
    {
        Name = "EditMapViewButton",
        Content = "Edit Map",
        MinWidth = 92
    };
    private readonly ToggleButton _modernFlyViewButton = new()
    {
        Name = "GameCameraViewButton",
        Content = "Game Camera",
        MinWidth = 108
    };
    private readonly TextBlock _levelBuildingEditorIndicator = new()
    {
        Name = "LevelBuildingEditorIndicator",
        Text = "Level Building Editor"
    };
    private Border? _editorWorkspaceIndicator;
    private Button? _modernViewportActionButton;
    private TabControl? _modernWorkspaceTabs;
    private TabItem? _modernObjectWorkspaceTab;
    private TabItem? _modernLevelBuildingObjectWorkspaceTab;
    private TabItem? _modernTerrainWorkspaceTab;
    private TabItem? _modernLevelWorkspaceTab;
    private TabItem? _modernEnvironmentWorkspaceTab;
    private TabItem? _modernResearchWorkspaceTab;
    private IReadOnlyList<TabItem> _existingLevelEditorTabs = Array.Empty<TabItem>();
    private IReadOnlyList<TabItem> _levelBuildingWorkspaceTabs = Array.Empty<TabItem>();
    private TabItem? _lastExistingLevelEditorTab;
    private TabItem? _lastLevelBuildingWorkspaceTab;
    private LevelEditorExperience _activeEditorExperience = LevelEditorExperience.ExistingLevel;
    private bool _switchingEditorShellWorkspace;
    private Expander? _modernLinkedObjectsExpander;

    private static readonly Color ModernInk = Color.FromRgb(31, 38, 45);
    private static readonly Color ModernMutedInk = Color.FromRgb(82, 92, 104);
    private static readonly Color ModernLine = Color.FromRgb(215, 221, 228);
    private static readonly Color ModernSurface = Color.FromRgb(250, 251, 252);
    private static readonly Color ModernPanel = Color.FromRgb(244, 247, 249);
    private static readonly Color ModernBlue = Color.FromRgb(31, 105, 191);
    private static readonly Color ModernTeal = Color.FromRgb(16, 126, 145);
    private static readonly Color ModernGreen = Color.FromRgb(39, 139, 83);
    private static readonly Color ModernRed = Color.FromRgb(177, 57, 57);

    private Control BuildModernLayout()
    {
        Grid root = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(38))
            },
            Background = new SolidColorBrush(Color.FromRgb(238, 242, 245))
        };

        root.Children.Add(BuildModernToolbar());

        Control notifications = BuildAppNotificationArea();
        Grid.SetRow(notifications, 1);
        root.Children.Add(notifications);

        Grid body = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(5)),
                new ColumnDefinition(new GridLength(384)) { MinWidth = 360, MaxWidth = 520 }
            }
        };
        Grid.SetRow(body, 2);

        Grid.SetColumn(_viewport, 0);
        body.Children.Add(_viewport);
        Control welcome = BuildWelcomeOverlay();
        Grid.SetColumn(welcome, 0);
        body.Children.Add(welcome);

        GridSplitter splitter = new()
        {
            Width = 5,
            Background = new SolidColorBrush(Color.FromRgb(221, 226, 232)),
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);

        Control sidebar = BuildModernSidebar();
        _modernSidebarHost = sidebar;
        Grid.SetColumn(sidebar, 2);
        body.Children.Add(sidebar);
        root.Children.Add(body);

        Border status = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 251)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(10)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Children =
                {
                    new Border
                    {
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(ModernTeal),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    _visibleStatusText,
                    BuildShowExportedGameButton(),
                    NewAsyncButton("Details", ShowStatusDetailsAsync)
                }
            }
        };
        Grid.SetColumn(((Grid)status.Child!).Children[2], 2);
        _visibleStatusText.Foreground = new SolidColorBrush(Color.FromRgb(59, 68, 78));
        _visibleStatusText.FontSize = 12;
        _visibleStatusText.VerticalAlignment = VerticalAlignment.Center;
        _visibleStatusText.TextWrapping = TextWrapping.NoWrap;
        _visibleStatusText.TextTrimming = TextTrimming.CharacterEllipsis;
        _statusText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
                _visibleStatusText.Text = VisibleEditorStatus(_statusText.Text ?? "");
        };
        _visibleStatusText.Text = VisibleEditorStatus(_statusText.Text ?? "");
        Grid.SetColumn(_visibleStatusText, 1);
        Grid.SetColumn(((Grid)status.Child!).Children[^1], 3);
        Grid.SetRow(status, 3);
        root.Children.Add(status);

        return root;
    }

    private Control BuildModernToolbar()
    {
        Border shell = new()
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 8)
        };

        Grid layout = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 6
        };

        Grid topRow = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(215)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };
        topRow.Children.Add(BuildModernBrandTitle());

        Control editorWorkspace = BuildEditorWorkspaceControl();
        Grid.SetColumn(editorWorkspace, 1);
        topRow.Children.Add(editorWorkspace);

        StackPanel fileActions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _toolbarOpenDiscImageButton = NewAsyncButton("Open Game…", ChooseSourceDiscImageAsync);
        ToolTip.SetTip(_toolbarOpenDiscImageButton, "Choose your original Spyro BIN or CUE. Export creates a separate playable copy.");
        StyleModernToolbarButton(_toolbarOpenDiscImageButton, 106);
        fileActions.Children.Add(BuildActionMenu("File", "EditorFileMenu", new ToolbarAction("New Level…", NewMainLevelAsync),
            new ToolbarAction("Start Blank in Current Level…", StartBlankCurrentLevelAsync),
            new ToolbarAction("Restore Before Clear…", RestoreBeforeClearAsync),
            new ToolbarAction("Open Created Level…", OpenMainFreshLevelAsync),
            new ToolbarAction("Save Level", SaveFromToolbarAsync)));
        fileActions.Children.Add(_toolbarOpenDiscImageButton);
        _toolbarSaveButton = NewAsyncButton("Save", SaveFromToolbarAsync);
        _toolbarSaveButton.Name = "SaveLevelButton";
        StyleModernToolbarButton(_toolbarSaveButton, 64);
        ToolTip.SetTip(_toolbarSaveButton, "Save this level's object and terrain edits (⌘S on Mac, Ctrl+S on Windows). Create BIN exports the game.");
        fileActions.Children.Add(_toolbarSaveButton);
        Button freshResearch = BuildNativeFreshDocumentButton();
        freshResearch.Content = "Fresh research…";
        ToolTip.SetTip(freshResearch, "Earlier experimental document and its separate research export. Use File > New Level to author a neutral level in this main editor.");
        if (!_releaseMode) fileActions.Children.Add(freshResearch);

        _toolbarCreateBinButton = NewAsyncButton("Create BIN", CreateObjectTestBinAsync);
        StyleModernPrimaryButton(_toolbarCreateBinButton, ModernBlue, 110);
        ToolTip.SetTip(_toolbarCreateBinButton, "Save current edits and export all saved levels to a playable BIN/CUE copy.");
        fileActions.Children.Add(_toolbarCreateBinButton);

        List<ToolbarAction> moreActions =
        [
            new("Project Data", ShowProjectDataAsync),
            new("Check for Updates", ShowUpdatesAsync),
            new("Check for Export Issues…", ShowBuildSafetyInspectorAsync),
            new("Help", ShowHelpAsync)
        ];
        if (!_releaseMode)
            moreActions.Insert(3, new ToolbarAction("Diagnostics", ShowDiagnosticsAsync));
        _toolbarMoreControl = BuildActionMenu("More", "EditorMoreMenu", moreActions.ToArray());
        if (_toolbarMoreControl is Button moreBox)
        {
            moreBox.Width = 74;
            moreBox.MinHeight = 34;
            moreBox.Margin = new Thickness(0);
        }
        fileActions.Children.Add(_toolbarMoreControl);
        Grid.SetColumn(fileActions, 2);
        topRow.Children.Add(fileActions);
        layout.Children.Add(topRow);

        Grid bottomRow = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        StackPanel workspaceCommands = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        _levelJumpBox.MinHeight = 34;
        _levelJumpBox.Width = 184;
        _levelJumpBox.PlaceholderText = "Choose level";
        _levelJumpBox.Margin = new Thickness(0);
        WireLevelJumpSelection();
        workspaceCommands.Children.Add(_levelJumpBox);
        workspaceCommands.Children.Add(BuildModernViewModeControl());

        _modernViewportActionButton = NewButton("", RunContextualViewportAction);
        _modernViewportActionButton.Name = "ViewportActionButton";
        StyleModernToolbarButton(_modernViewportActionButton, 118);
        RefreshContextualViewportAction();
        workspaceCommands.Children.Add(_modernViewportActionButton);
        bottomRow.Children.Add(workspaceCommands);

        _sourceDiscStatusText.FontSize = 11;
        _sourceDiscStatusText.FontWeight = FontWeight.SemiBold;
        _sourceDiscStatusText.TextTrimming = TextTrimming.CharacterEllipsis;
        _sourceDiscStatusText.MaxWidth = 220;
        _sourceDiscStatusText.VerticalAlignment = VerticalAlignment.Center;
        _sourceDiscStatusText.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_sourceDiscStatusText, 1);
        bottomRow.Children.Add(_sourceDiscStatusText);
        Grid.SetRow(bottomRow, 1);
        layout.Children.Add(bottomRow);

        shell.Child = layout;
        return shell;
    }

    private static Control BuildModernBrandTitle()
    {
        Grid brand = new()
        {
            Height = 52,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(48)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        try
        {
            brand.Children.Add(new Image
            {
                Source = LoadAssetBitmap("Assets/Brand/app-icon.png"),
                Width = 48,
                Height = 48,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        catch
        {
            brand.Children.Add(new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(ModernTeal)
            });
        }

        StackPanel words = new()
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        words.Children.Add(new TextBlock
        {
            Text = "Spyro the Dragon",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(232, 145, 0)),
            TextWrapping = TextWrapping.NoWrap
        });
        words.Children.Add(new TextBlock
        {
            Text = "LEVEL EDITOR",
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        Grid.SetColumn(words, 1);
        brand.Children.Add(words);
        return brand;
    }

    private Control BuildEditorWorkspaceControl()
    {
        _levelBuildingEditorIndicator.FontSize = 13;
        _levelBuildingEditorIndicator.FontWeight = FontWeight.SemiBold;
        _levelBuildingEditorIndicator.Foreground = Brushes.White;
        _levelBuildingEditorIndicator.HorizontalAlignment = HorizontalAlignment.Center;
        _levelBuildingEditorIndicator.VerticalAlignment = VerticalAlignment.Center;

        _editorWorkspaceIndicator = new Border
        {
            Name = "EditorWorkspaceSelector",
            Background = new SolidColorBrush(ModernTeal),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(18, 9),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _levelBuildingEditorIndicator
        };
        ToolTip.SetTip(
            _editorWorkspaceIndicator,
            "Build your level with the main editor's terrain and object tools. Create BIN makes it playable from New Game.");
        return _editorWorkspaceIndicator;
    }

    private Control BuildModernViewModeControl()
    {
        _modernMapViewButton.Padding = new Thickness(12, 6);
        _modernFlyViewButton.Padding = new Thickness(12, 6);
        _modernMapViewButton.MinHeight = 34;
        _modernFlyViewButton.MinHeight = 34;
        _modernMapViewButton.Click += (_, _) => _viewport.SetViewMode(ViewportViewMode.Map);
        _modernFlyViewButton.Click += (_, _) => _viewport.SetViewMode(ViewportViewMode.Fly3D);

        Grid segments = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        segments.Children.Add(_modernMapViewButton);
        Grid.SetColumn(_modernFlyViewButton, 1);
        segments.Children.Add(_modernFlyViewButton);
        RefreshModernViewModeButtons(_viewport.ViewMode);

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(188, 197, 207)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true,
            Child = segments
        };
    }

    private void RefreshModernViewModeButtons(ViewportViewMode mode)
    {
        StyleModernViewSegment(_modernMapViewButton, mode == ViewportViewMode.Map);
        StyleModernViewSegment(_modernFlyViewButton, mode == ViewportViewMode.Fly3D);
    }

    private static void StyleModernViewSegment(ToggleButton button, bool active)
    {
        button.IsChecked = active;
        button.Background = new SolidColorBrush(active ? ModernBlue : Colors.White);
        button.Foreground = new SolidColorBrush(active ? Colors.White : ModernInk);
        button.BorderThickness = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private void RunContextualViewportAction()
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation("using viewport commands"))
            return;

        if (_viewport.ViewMode == ViewportViewMode.Map)
        {
            _viewport.ResetView();
            _statusText.Text = "Edit Map fitted to every captured terrain face.";
            return;
        }

        if (_selectedMoby != null && !_selectedMoby.IsRemoved)
        {
            _viewport.SelectMoby(_selectedMoby, focus: true);
            _statusText.Text = $"Game Camera framed {_selectedMoby.DisplayLabel}.";
            return;
        }

        _viewport.RefreshFlyCameraForLoadedLevel();
        _statusText.Text = "Game Camera returned to the level entry.";
    }

    private void RefreshContextualViewportAction()
    {
        if (_modernViewportActionButton == null)
            return;

        if (_viewport.ViewMode == ViewportViewMode.Map)
        {
            _modernViewportActionButton.Content = "Fit All";
            ToolTip.SetTip(
                _modernViewportActionButton,
                "Fit all editable terrain in the top-down view.");
            return;
        }

        bool canFrameSelection = _selectedMoby != null && !_selectedMoby.IsRemoved;
        _modernViewportActionButton.Content = canFrameSelection ? "Frame Selection" : "Return to Entry";
        ToolTip.SetTip(
            _modernViewportActionButton,
            canFrameSelection
                ? "Move the Game Camera to the selected object without changing scene contents."
                : "Return the Game Camera to the level entry. Off-camera terrain remains available in Edit Map.");
    }

    private Control BuildModernSidebar()
    {
        Border shell = new()
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1, 0, 0, 0)
        };
        Grid layout = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        layout.Children.Add(BuildModernLevelHeader());

        _modernObjectWorkspaceTab = NewModernWorkspaceTab(
            "Objects",
            BuildModernObjectWorkspace());
        _modernLevelBuildingObjectWorkspaceTab = NewModernWorkspaceTab(
            "Object Manager",
            BuildId65LevelBuildingObjectManager());

        _retailTerrainWorkspace=_releaseMode ? BuildReleaseTerrainWorkspace() : BuildTerrainTabControls();
        _modernTerrainWorkspaceTab = NewModernWorkspaceTab(
            "Terrain",
            new StackPanel { Spacing = 8, Children = { BuildFreshTerrainTools(),
                _retailTerrainWorkspace } });
        _modernLevelWorkspaceTab = NewModernWorkspaceTab("Game Settings", BuildModernLevelWorkspace());
        _modernEnvironmentWorkspaceTab = NewModernWorkspaceTab(
            "Environment",
            BuildModernEnvironmentWorkspace());
        List<TabItem> existingLevelItems =
        [
            _modernObjectWorkspaceTab,
            _modernTerrainWorkspaceTab,
            _modernLevelWorkspaceTab,
            _modernEnvironmentWorkspaceTab
        ];
        List<TabItem> levelBuildingItems =
        [
            _modernLevelBuildingObjectWorkspaceTab,
            _modernTerrainWorkspaceTab,
            _modernLevelWorkspaceTab,
            _modernEnvironmentWorkspaceTab
        ];
        if (!_releaseMode)
        {
            _modernResearchWorkspaceTab = NewModernWorkspaceTab(
                "Research",
                BuildModernResearchWorkspace());
            existingLevelItems.Add(_modernResearchWorkspaceTab);
            levelBuildingItems.Add(_modernResearchWorkspaceTab);
        }
        _existingLevelEditorTabs = existingLevelItems;
        _levelBuildingWorkspaceTabs = levelBuildingItems;
        _lastExistingLevelEditorTab = _modernObjectWorkspaceTab;
        _lastLevelBuildingWorkspaceTab = _modernLevelBuildingObjectWorkspaceTab;

        _modernWorkspaceTabs = new TabControl
        {
            Name = "EditorWorkspaceToolTabs",
            MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _modernWorkspaceTabs.SelectionChanged += (_, _) =>
        {
            if (!_switchingEditorShellWorkspace)
                RememberSelectedEditorWorkspaceTab();
            RefreshEditorWorkspaceTerrainFocusMode();
        };
        ApplyEditorExperienceForCurrentLevel(_modernObjectWorkspaceTab);
        Grid.SetRow(_modernWorkspaceTabs, 1);
        layout.Children.Add(_modernWorkspaceTabs);

        shell.Child = layout;
        return shell;
    }

    private void ApplyEditorExperienceForCurrentLevel(TabItem? preferredTab = null)
    {
        RememberSelectedEditorWorkspaceTab();
        TabItem? selectedBefore = _modernWorkspaceTabs?.SelectedItem as TabItem;
        LevelEditorExperience experience = LevelEditorExperienceResolver.Resolve(_currentLevel);
        _switchingEditorShellWorkspace = true;
        try
        {
            _activeEditorExperience = experience;
            if (_modernWorkspaceTabs != null)
            {
                IReadOnlyList<TabItem> tabs = experience == LevelEditorExperience.ExistingLevel
                    ? _existingLevelEditorTabs
                    : _levelBuildingWorkspaceTabs;
                TabItem? remembered = experience == LevelEditorExperience.ExistingLevel
                    ? _lastExistingLevelEditorTab
                    : _lastLevelBuildingWorkspaceTab;
                TabItem? target = preferredTab != null && tabs.Contains(preferredTab)
                    ? preferredTab
                    : selectedBefore != null && tabs.Contains(selectedBefore)
                        ? selectedBefore
                        : remembered;
                if (target == null || !tabs.Contains(target))
                    target = tabs.FirstOrDefault();
                _modernWorkspaceTabs.ItemsSource = tabs;
                _modernWorkspaceTabs.SelectedItem = target;
                if (_modernWorkspaceTabs.SelectedItem == null && tabs.Count > 0)
                    _modernWorkspaceTabs.SelectedIndex = 0;
            }
        }
        finally
        {
            _switchingEditorShellWorkspace = false;
        }
        RememberSelectedEditorWorkspaceTab();
        RefreshEditorWorkspaceIndicator();
        RefreshEditorWorkspaceTerrainFocusMode();
    }

    private void SelectEditorToolTab(TabItem? tab) =>
        ApplyEditorExperienceForCurrentLevel(tab);

    private void RememberSelectedEditorWorkspaceTab()
    {
        if (_modernWorkspaceTabs?.SelectedItem is not TabItem selected)
            return;
        if (_activeEditorExperience == LevelEditorExperience.ExistingLevel &&
            _existingLevelEditorTabs.Contains(selected))
        {
            _lastExistingLevelEditorTab = selected;
        }
        else if (_activeEditorExperience == LevelEditorExperience.FreshBlankLevel &&
                 _levelBuildingWorkspaceTabs.Contains(selected))
        {
            _lastLevelBuildingWorkspaceTab = selected;
        }
    }

    private void RefreshEditorWorkspaceTerrainFocusMode()
    {
        _viewport.TerrainFocusMode = IsTerrainWorkspaceTabSelected();
        // Terrain brushes belong to the Terrain tab: leaving it turns them off so clicks select objects.
        if (!_viewport.TerrainFocusMode)
        {
            if (_viewport.TerrainBrushAction != TerrainBrushAction.Off)
                SelectTerrainBrushMode(TerrainBrushAction.Off, announce: false);
            if (_activeTerrainTexturePaintBrush != null || _viewport.TerrainTexturePaintMode)
                StopTerrainTexturePaintMode(announce: false);
        }
    }

    private bool IsTerrainWorkspaceTabSelected() =>
        _modernWorkspaceTabs != null &&
        _modernTerrainWorkspaceTab != null &&
        ReferenceEquals(_modernWorkspaceTabs.SelectedItem, _modernTerrainWorkspaceTab);

    private void RefreshEditorWorkspaceIndicator()
    {
        if (_editorWorkspaceIndicator != null)
        {
            _editorWorkspaceIndicator.IsVisible =
                _activeEditorExperience == LevelEditorExperience.FreshBlankLevel;
        }
    }

    internal EditorShellSessionSnapshot CaptureEditorShellSessionSnapshotForTesting() => new(
        _activeEditorExperience,
        _workspace,
        _catalog,
        _currentLevel,
        _currentGeometry,
        _currentMobys,
        _selectedMoby,
        _selectedTerrain,
        _viewport,
        _levelLoadRequestId,
        _savedMobyEditSignature,
        _savedTerrainEditSignature,
        BuildMobyEditSignature(_currentMobys),
        BuildTerrainEditSignature(_currentGeometry),
        _savedNativeMovementEditSignature,
        BuildNativeMovementEditSignature(),
        _savedDragonRunToEditSignature,
        BuildDragonRunToEditSignature(),
        HasUnsavedMobyEdits(),
        HasUnsavedTerrainEdits(),
        _viewport.CaptureNavigationSnapshotForTesting());

    private Control BuildModernLevelHeader()
    {
        _levelTitle.FontSize = 18;
        _levelTitle.FontWeight = FontWeight.SemiBold;
        _levelTitle.Foreground = new SolidColorBrush(ModernInk);
        _levelTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _gemCounterText.FontSize = 12;
        _gemCounterText.FontWeight = FontWeight.SemiBold;
        _gemCounterText.Foreground = new SolidColorBrush(Color.FromRgb(36, 94, 130));
        _gemCounterText.TextWrapping = TextWrapping.Wrap;

        StackPanel text = new() { Spacing = 3 };
        text.Children.Add(_levelTitle);
        text.Children.Add(_gemCounterText);
        return new Border
        {
            Background = new SolidColorBrush(ModernSurface),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12),
            Child = text
        };
    }

    private static TabItem NewModernWorkspaceTab(string name, Control content)
    {
        return new TabItem
        {
            Foreground = new SolidColorBrush(ModernInk),
            Header = new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(ModernInk),
                TextWrapping = TextWrapping.NoWrap
            },
            Content = new ScrollViewer
            {
                Content = new Border
                {
                    Padding = new Thickness(14),
                    Child = content
                },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
    }

    private Control? _objectUniversalAddPanel;
    private Grid? _objectPrimaryGrid;
    private Grid? _objectSecondaryGrid;
    private Control? _objectPositionDisclosure;
    private Control? _objectReplaceFlightGateButton;
    private Expander? _objectRecoveryExpander;
    private Border? _objectActionHintBorder;

    /// <summary>
    /// Shipping Objects tab: an "Add" card, a "Selected object" card with everything that acts on the
    /// selection, then the list of what is in the level. Same controls as the development layout.
    /// </summary>
    private Control BuildReleaseObjectWorkspace()
    {
        Control selection = BuildModernSelectionSummary();
        _ = BuildModernPrimaryObjectControls();
        StackPanel root = new() { Name = "ObjectsPanel", Spacing = 12, Margin = new Thickness(0, 10, 0, 0) };

        StackPanel add = new();
        if (_objectUniversalAddPanel != null) add.Children.Add(Detach(_objectUniversalAddPanel));
        if (_objectAddButton != null)
        {
            Detach(_objectAddButton);
            _objectAddButton.Content = "Add a gem or copy an object…";
            _objectAddButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            ToolTip.SetTip(_objectAddButton, "Quickly add a gem of any value, or another copy of something already in this level.");
            add.Children.Add(_objectAddButton);
        }
        root.Children.Add(TerrainCard("Add to this level", add));

        StackPanel selected = new();
        if (selection is StackPanel summary && summary.Children.Count > 0)
            summary.Children.RemoveAt(0); // its own "Selection" heading; the card has one
        selected.Children.Add(selection);
        if (_objectPrimaryGrid != null && _objectEditButton != null && _objectSwapCatalogButton != null)
        {
            Grid actions = TwoColumnGrid(1);
            Place(actions, _objectEditButton, 0, 0, "Edit…", "Change this object's name, value, position and facing. Double-clicking it does the same.");
            Place(actions, _objectSwapCatalogButton, 1, 0, "Replace…");
            selected.Children.Add(actions);
        }
        if (_objectReplaceFlightGateButton != null) selected.Children.Add(Detach(_objectReplaceFlightGateButton));
        if (_objectSecondaryGrid != null) selected.Children.Add(Detach(_objectSecondaryGrid));
        if (_objectUndoRemoveButton != null) selected.Children.Add(Detach(_objectUndoRemoveButton));
        if (_objectPositionDisclosure != null) selected.Children.Add(Detach(_objectPositionDisclosure));
        if (_objectActionHintBorder != null) selected.Children.Add(Detach(_objectActionHintBorder));
        root.Children.Add(TerrainCard("Selected object", selected));

        root.Children.Add(BuildModernMobyBrowser());
        if (_objectRecoveryExpander != null) root.Children.Add(Detach(_objectRecoveryExpander));

        var checks = (Expander)BuildModernDisclosure("Export check", BuildModernBuildSafetyPanel(), false);
        var checkSummary = new TextBlock { Name = "ExportCheckSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        void RefreshCheckSummary()
        {
            checkSummary.Text = "Export check · " + _buildSafetySummaryText.Text;
            checkSummary.Foreground = _buildSafetySummaryText.Foreground;
        }
        _buildSafetySummaryText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty || e.Property == TextBlock.ForegroundProperty)
                RefreshCheckSummary();
        };
        RefreshCheckSummary(); checks.Header = checkSummary;
        root.Children.Add(checks);
        ShowSelection(ViewportSelectionChangedEventArgs.None);
        return root;
    }

    private Control BuildModernObjectWorkspace()
    {
        if (_releaseMode)
            return BuildReleaseObjectWorkspace();
        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(BuildModernSelectionSummary());
        panel.Children.Add(ModernDivider());
        panel.Children.Add(BuildModernPrimaryObjectControls());
        panel.Children.Add(ModernDivider());
        var checks = (Expander)BuildModernDisclosure("Export check", BuildModernBuildSafetyPanel(), false);
        var checkSummary = new TextBlock { Name = "ExportCheckSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        void RefreshCheckSummary()
        {
            checkSummary.Text = "Export check · " + _buildSafetySummaryText.Text;
            checkSummary.Foreground = _buildSafetySummaryText.Foreground;
        }
        _buildSafetySummaryText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty || e.Property == TextBlock.ForegroundProperty)
                RefreshCheckSummary();
        };
        RefreshCheckSummary(); checks.Header = checkSummary;
        panel.Children.Add(checks);
        panel.Children.Add(BuildModernMobyBrowser());
        ShowSelection(ViewportSelectionChangedEventArgs.None);
        return panel;
    }

    private Control BuildModernSelectionSummary()
    {
        StackPanel panel = new() { Spacing = 7 };
        panel.Children.Add(ModernSectionHeading("Selection"));
        _selectionTitle.FontSize = 17;
        _selectionTitle.FontWeight = FontWeight.SemiBold;
        _selectionTitle.Foreground = new SolidColorBrush(ModernInk);
        _selectionTitle.TextWrapping = TextWrapping.Wrap;
        _selectionDetails.TextWrapping = TextWrapping.Wrap;
        _selectionDetails.Foreground = new SolidColorBrush(ModernMutedInk);
        _selectionDetails.FontSize = 12;
        _selectionDetails.LineHeight = 17;
        // No empty gap under "Nothing selected".
        _selectionDetails.IsVisible = !string.IsNullOrWhiteSpace(_selectionDetails.Text);
        _selectionDetails.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
                _selectionDetails.IsVisible = !string.IsNullOrWhiteSpace(_selectionDetails.Text);
        };
        panel.Children.Add(_selectionTitle);
        panel.Children.Add(_selectionDetails);

        _linkedMobyHint.TextWrapping = TextWrapping.Wrap;
        _linkedMobyHint.Foreground = new SolidColorBrush(ModernMutedInk);
        _linkedMobyHint.FontSize = 12;
        StackPanel linked = new() { Spacing = 7, Margin = new Thickness(0, 6, 0, 0) };
        linked.Children.Add(_linkedMobyHint);
        linked.Children.Add(BuildLinkedMobyList());
        _modernLinkedObjectsExpander = new Expander
        {
            Header = "Linked objects",
            IsExpanded = false,
            IsVisible = false,
            Content = linked
        };
        panel.Children.Add(_modernLinkedObjectsExpander);
        return panel;
    }

    private Control BuildModernPrimaryObjectControls()
    {
        StackPanel panel = new() { Spacing = 9 };
        panel.Children.Add(ModernSectionHeading("Add or edit objects"));

        _objectAddButton = NewAsyncButton("Quick Add…", AddMobyNearSelectionAsync);
        _objectEditButton = NewAsyncButton("Edit", async () =>
        {
            if (_selectedMoby != null)
                await EditMobyAsync(_selectedMoby);
        });
        _objectSwapCatalogButton = NewAsyncButton("Replace", ShowCrossLevelSwapCatalogAsync);
        StyleModernSecondaryButton(_objectAddButton);
        ToolTip.SetTip(_objectAddButton, "Add a standard gem by value or copy an object already in this level. Use Add object for the full game catalog.");
        StyleModernPrimaryButton(_objectEditButton, ModernBlue);
        StyleModernPrimaryButton(_objectSwapCatalogButton, ModernTeal);

        _objectUniversalAddPanel = BuildUniversalAddButtons();
        panel.Children.Add(_objectUniversalAddPanel);
        Grid primary = NewEqualButtonGrid(3);
        AddModernGridButton(primary, _objectEditButton, 0);
        AddModernGridButton(primary, _objectSwapCatalogButton, 1);
        AddModernGridButton(primary, _objectAddButton, 2);
        _objectPrimaryGrid = primary;
        panel.Children.Add(primary);
        var replaceFlightGate = BuildReplaceFlightGateButton();
        StyleModernPrimaryButton(replaceFlightGate, ModernTeal);
        _objectReplaceFlightGateButton = replaceFlightGate;
        panel.Children.Add(replaceFlightGate);

        _objectNativeMovementButton = NewAsyncButton("Edit Native Movement", EditSelectedNativeMovementAsync);
        StyleModernPrimaryButton(_objectNativeMovementButton, Color.FromRgb(111, 86, 174));
        _objectNativeMovementButton.IsVisible = false;
        if (!_releaseMode)
            panel.Children.Add(_objectNativeMovementButton);

        _objectCopyButton = NewButton("Copy", CopySelectedMoby);
        _objectPasteButton = NewAsyncButton("Paste", PasteMobyClipboardAtLastPointerAsync);
        _objectUndoButton = NewButton("Undo", () =>
        {
            if (_selectedMoby != null)
                UndoMoby(_selectedMoby);
        });
        _objectRemoveButton = NewButton("Remove", RemoveSelectedMoby);
        StyleModernSecondaryButton(_objectCopyButton);
        StyleModernSecondaryButton(_objectPasteButton);
        StyleModernSecondaryButton(_objectUndoButton);
        StyleModernSecondaryButton(_objectRemoveButton, ModernRed);

        Grid secondary = NewEqualButtonGrid(4);
        AddModernGridButton(secondary, _objectCopyButton, 0);
        AddModernGridButton(secondary, _objectPasteButton, 1);
        AddModernGridButton(secondary, _objectUndoButton, 2);
        AddModernGridButton(secondary, _objectRemoveButton, 3);
        _objectSecondaryGrid = secondary;
        panel.Children.Add(secondary);

        var positionTools = new StackPanel { Spacing = 8 };
        positionTools.Children.Add(BuildSelectedMobyZControls());

        _objectLayerDownButton = NewButton("Below", () => MoveSelectedMobyToAdjacentTerrainLayer(-1));
        _objectLayerUpButton = NewButton("Above", () => MoveSelectedMobyToAdjacentTerrainLayer(1));
        StyleModernSecondaryButton(_objectLayerDownButton);
        StyleModernSecondaryButton(_objectLayerUpButton);
        ToolTip.SetTip(_objectLayerDownButton, "Put the object on the next floor below it, such as the ground under a bridge.");
        ToolTip.SetTip(_objectLayerUpButton, "Put the object on the next floor above it, such as a bridge or roof over the ground.");

        Grid layerRow = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(82)),
                new ColumnDefinition(new GridLength(82))
            },
            ColumnSpacing = 6
        };
        layerRow.Children.Add(new TextBlock
        {
            Text = "Jump to floor",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernMutedInk),
            VerticalAlignment = VerticalAlignment.Center
        });
        AddModernGridButton(layerRow, _objectLayerDownButton, 1);
        AddModernGridButton(layerRow, _objectLayerUpButton, 2);
        positionTools.Children.Add(layerRow);
        _objectPositionDisclosure = BuildModernDisclosure("Height and ground", positionTools, false);
        panel.Children.Add(_objectPositionDisclosure);

        _objectSwapTestButton = NewAsyncButton("Create Swap Test", CreateObjectCandidateBinAsync);
        StyleModernPrimaryButton(_objectSwapTestButton, Color.FromRgb(176, 103, 20));
        _objectSwapTestButton.IsVisible = false;
        ToolTip.SetTip(_objectSwapCatalogButton, "Swap this chest or enemy for another kind that is known to work in this level.");
        ToolTip.SetTip(_objectSwapTestButton, "Create a disposable BIN/CUE for a staged cross-level swap.");
        if (!_releaseMode)
            panel.Children.Add(_objectSwapTestButton);

        _objectUndoRemoveButton = NewButton("Undo last removal", UndoLastRemovedMoby);
        _objectRestoreLevelButton = NewAsyncButton("Restore entire level", RestoreCurrentLevelAsync);
        StyleModernSecondaryButton(_objectUndoRemoveButton);
        StyleModernSecondaryButton(_objectRestoreLevelButton, ModernRed);
        _objectUndoRemoveButton.IsVisible = false;
        StackPanel recovery = new() { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(_objectUndoRemoveButton);
        recovery.Children.Add(_objectRestoreLevelButton);
        var beforeClear=NewAsyncButton("Restore Before Clear…",RestoreBeforeClearAsync);beforeClear.Name="RestoreBeforeClear";
        StyleModernSecondaryButton(beforeClear);recovery.Children.Add(beforeClear);
        _objectRecoveryExpander = new Expander
        {
            Header = "Recovery",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = recovery
        };
        panel.Children.Add(_objectRecoveryExpander);

        _objectActionHint.TextWrapping = TextWrapping.Wrap;
        _objectActionHint.Foreground = new SolidColorBrush(Color.FromRgb(63, 75, 86));
        _objectActionHint.FontSize = 12;
        _objectActionHint.LineHeight = 17;
        _objectActionHintBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(237, 244, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 212, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = _objectActionHint
        };
        panel.Children.Add(_objectActionHintBorder);

        RefreshActionAvailability();
        return panel;
    }

    private Control BuildModernMobyBrowser()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Objects in this level"));
        if (!_releaseMode)
            panel.Children.Add(BuildNativeMobyLibraryButton("RetailNativeMobyLibraryButton", allowExistingTemplateSelection: true));

        _mobyCategoryBox.ItemsSource = _releaseMode ? EverydayMobyCategoryOptions : MobyCategoryOptions;
        _mobyCategoryBox.SelectedIndex = 0;
        _mobyCategoryBox.MinHeight = 34;
        _mobyCategoryBox.PlaceholderText = "Category";
        _mobyCategoryBox.SelectionChanged += (_, _) => RefreshMobyList(_selectedMoby);

        _mobySearchBox.PlaceholderText = "Find placed objects…";
        _mobySearchBox.MinHeight = 34;
        _mobySearchBox.TextChanged += (_, _) => RefreshMobyList(_selectedMoby);

        Grid filters = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(142)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        filters.Children.Add(_mobyCategoryBox);
        Grid.SetColumn(_mobySearchBox, 1);
        filters.Children.Add(_mobySearchBox);
        panel.Children.Add(filters);

        _mobyListHint.TextWrapping = TextWrapping.Wrap;
        _mobyListHint.Foreground = new SolidColorBrush(ModernMutedInk);
        _mobyListHint.FontSize = 11;
        panel.Children.Add(_mobyListHint);

        _mobyList.MinHeight = _releaseMode ? 320 : 240;
        _mobyList.MaxHeight = _releaseMode ? 520 : 420;
        _mobyList.Background = Brushes.White;
        _mobyList.BorderThickness = new Thickness(0);
        _mobyList.ItemTemplate = new FuncDataTemplate<Moby>((moby, _) => BuildMobyListItemControl(moby));
        _mobyList.SelectionChanged += (_, _) =>
        {
            if (_syncingMobyList || _mobyList.SelectedItem is not Moby moby)
                return;
            _viewport.SelectMoby(moby, true);
        };
        // Rows take the width of the list (no sideways scrolling), so the selection colour covers the whole
        // row instead of only the part behind the text.
        ScrollViewer.SetHorizontalScrollBarVisibility(_mobyList, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        // Selecting a row scrolls the list itself. It must not also drag the whole side panel down and
        // carry the Selected object card (Edit…, Remove) off the screen.
        _mobyList.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        _mobyList.DoubleTapped += async (_, _) =>
        {
            if (_mobyList.SelectedItem is Moby moby)
                await EditMobyAsync(moby);
        };
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = _mobyList
        });
        return panel;
    }

    private Control BuildModernLevelWorkspace()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Level settings"));
        if (_releaseMode)
            panel.Children.Add(new TextBlock
            {
                Text = "Names, music and portal routes are kept in your project as soon as you apply them. Create BIN puts them in the game.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
        panel.Children.Add(BuildFreshDocumentSettings());
        _retailNameDisclosure=BuildModernDisclosure("Level name", BuildLevelTextPanel(), true);
        _retailMusicDisclosure=BuildModernDisclosure("Music", BuildLevelMusicPanel(), false);
        panel.Children.Add(_retailNameDisclosure);panel.Children.Add(_retailMusicDisclosure);
        if (!_releaseMode) panel.Children.Add(BuildModernDisclosure("Starting Abilities", new StackPanel
        {
            Name = "StartingAbilitiesPanel", Spacing = 8,
            Children = { BuildModernDisclosure("Fly Anywhere", BuildFreeFlightSettingsPanel(), true),
                BuildModernDisclosure("Play as Crash", BuildCrashPlayerSettingsPanel(), false) }
        }, false));
        else
        {
            // One switch for the whole exported game, off unless the user turns it on.
            var flight = BuildModernDisclosure("Fly Anywhere", BuildFreeFlightSettingsPanel(), true);
            flight.Name = "FlyAnywhereSection";
            panel.Children.Add(flight);
            var crash = BuildModernDisclosure("Play as Crash", BuildCrashPlayerSettingsPanel(), false);
            crash.Name = "PlayAsCrashSection";
            panel.Children.Add(crash);
        }
        _homeworldTextGroup = BuildModernDisclosure("Portals & Return Home", BuildPortalControlPanel(), false);
        panel.Children.Add(_homeworldTextGroup);

        // V5 whole-level replacement is deliberately a separate, guarded research
        // workflow. It never enters the normal Create BIN dispatcher.
        if (!_releaseMode) panel.Children.Add(BuildNativeLevelReplacementDisclosure());

        // The evidence-bound ID65 lab is intentionally narrower than the full
        // Research workspace and therefore remains reachable from the public V5
        // shell. Its own disclosure owns the locked-base and disposable-output
        // guards; it never enters normal Create BIN.
        if (!_releaseMode) panel.Children.Add(BuildId65BlankLevelLabDisclosure());

        _levelDetails.TextWrapping = TextWrapping.Wrap;
        _levelDetails.Foreground = new SolidColorBrush(ModernMutedInk);
        _levelDetails.FontSize = 12;
        _levelDetails.LineHeight = 18;
        panel.Children.Add(BuildModernDisclosure("Level details", _levelDetails, false));
        return panel;
    }

    private Control BuildModernEnvironmentWorkspace()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Sky and lighting"));
        panel.Children.Add(BuildSkyboxPanel());
        return panel;
    }

    private Control BuildModernResearchWorkspace()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Research tools"));
        panel.Children.Add(BuildModernDisclosure("Observed ID and naming", BuildIdentityObservationPanel(), false));
        panel.Children.Add(BuildModernDisclosure("Map and cache", BuildAdvancedMapPanel(), false));
        panel.Children.Add(BuildModernDisclosure("Object tests", BuildAdvancedObjectExportPanel(), false));
        panel.Children.Add(BuildModernDisclosure("Identity tests", BuildIdentityBatchPanel(), false));
        panel.Children.Add(BuildModernDisclosure("Original disc", BuildDiscImagePanel(), false));
        panel.Children.Add(BuildModernDisclosure("UI text", BuildUiTextPanel(), false));
        return panel;
    }

    private static Control BuildModernDisclosure(string header, Control content, bool expanded)
    {
        return new Expander
        {
            Header = new TextBlock
            {
                Text = header,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(ModernInk)
            },
            IsExpanded = expanded,
            // Every section spans the panel, so a column of them lines up instead of following its contents.
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Border
            {
                Padding = new Thickness(0, 8, 0, 4),
                Child = content
            }
        };
    }

    private static Grid NewEqualButtonGrid(int columns)
    {
        Grid grid = new() { ColumnSpacing = 6 };
        for (int index = 0; index < columns; index++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        return grid;
    }

    private static void AddModernGridButton(Grid grid, Button button, int column)
    {
        button.Margin = new Thickness(0);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private static TextBlock ModernSectionHeading(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(93, 102, 113))
        };
    }

    private static Border ModernDivider()
    {
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(ModernLine)
        };
    }

    private static void StyleModernToolbarButton(Button button, double minWidth)
    {
        button.Margin = new Thickness(0);
        button.MinWidth = minWidth;
        button.MinHeight = 34;
        button.Padding = new Thickness(10, 6);
        button.Background = new SolidColorBrush(Color.FromRgb(238, 242, 246));
        button.Foreground = new SolidColorBrush(ModernInk);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private static void StyleModernPrimaryButton(Button button, Color background, double minWidth = 0)
    {
        button.Margin = new Thickness(0);
        button.MinWidth = minWidth;
        button.MinHeight = 36;
        button.Padding = new Thickness(10, 7);
        button.Background = new SolidColorBrush(background);
        button.Foreground = Brushes.White;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        KeepButtonColorUnderPointer(button, background, Colors.White);
    }

    /// <summary>
    /// The stock theme repaints a hovered or pressed button in its own grey, so a coloured button looks
    /// switched off just as the pointer reaches it. Keep the colour, a shade darker, instead.
    /// </summary>
    private static void KeepButtonColorUnderPointer(Button button, Color background, Color foreground)
    {
        const string prefix = "Button";
        static Color Shade(Color color, double factor) =>
            Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
        IBrush text = new SolidColorBrush(foreground);
        button.Resources[prefix + "BackgroundPointerOver"] = new SolidColorBrush(Shade(background, 0.90));
        button.Resources[prefix + "BackgroundPressed"] = new SolidColorBrush(Shade(background, 0.80));
        button.Resources[prefix + "ForegroundPointerOver"] = text;
        button.Resources[prefix + "ForegroundPressed"] = text;
    }

    private static void StyleModernSecondaryButton(Button button, Color? foreground = null)
    {
        button.Margin = new Thickness(0);
        button.MinWidth = 0;
        button.MinHeight = 34;
        button.Padding = new Thickness(8, 6);
        button.Background = new SolidColorBrush(Color.FromRgb(238, 242, 246));
        button.Foreground = new SolidColorBrush(foreground ?? ModernInk);
        button.FontSize = 12;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private Control BuildModernMobyEditDialogContent(
        Window dialog,
        Moby moby,
        TextBox nameBox,
        TextBox typeBox,
        TextBox stateBox,
        TextBox xBox,
        TextBox yBox,
        TextBox zBox,
        TextBox yawBox,
        ComboBox? gemBox,
        bool editsRewardGem,
        IReadOnlyList<RewardTriggerEditorRow> rewardRows,
        IReadOnlyList<ChestContentEditorRow> chestRows,
        ComboBox? transformBox,
        TextBlock? transformNote,
        TextBox? signTextBox = null,
        ComboBox? eggColourBox = null,
        DragonColorEditor? dragonColor = null)
    {
        StackPanel body = new()
        {
            Spacing = 14,
            Margin = new Thickness(20, 18, 20, 16)
        };
        body.Children.Add(BuildModernDialogHeader(
            moby.DisplayLabel,
            _releaseMode
                ? BuildFriendlyMobySummary(moby)
                : $"T{moby.TrueIndex}  |  {BuildFriendlyMobySummary(moby)}"));
        body.Children.Add(BuildModernDialogField("Name", nameBox));

        Grid transform = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 16
        };
        transform.Children.Add(BuildModernDialogField("Position", BuildMobyPositionFields(xBox, yBox, zBox)));
        Control rotation = BuildModernDialogField("Rotation", BuildMobyRotationFields(yawBox));
        Grid.SetColumn(rotation, 1);
        transform.Children.Add(rotation);
        body.Children.Add(transform);
        if (dragonColor != null) body.Children.Add(BuildModernDialogField("Dragon color", dragonColor.Control));

        if (eggColourBox != null)
        {
            body.Children.Add(BuildModernDialogField("Egg color", eggColourBox));
            body.Children.Add(BuildUniversalEggEditPreview(eggColourBox));
        }

        if (signTextBox != null)
        {
            body.Children.Add(BuildModernDialogField("Sign message", signTextBox));
            body.Children.Add(BuildUniversalSignPreview(signTextBox));
            body.Children.Add(NewSmallNote($"Up to {UniversalSpecialBehaviors.SignLines} lines of {UniversalSpecialBehaviors.SignLineMaxChars} characters. The game's letters support A-Z, 0-9, space and ! ? . , (lowercase is shown in capitals)."));
        }

        if (gemBox != null)
        {
            body.Children.Add(BuildModernDialogField(editsRewardGem ? "Reward gem" : "Gem value", gemBox));
        }

        TextBlock validationMessage = NewSmallNote("");
        validationMessage.Name = "MobyEditValidationText";
        validationMessage.Foreground = new SolidColorBrush(Color.FromRgb(180, 42, 42));
        validationMessage.IsVisible = false;
        body.Children.Add(validationMessage);

        if (rewardRows.Count > 0)
        {
            StackPanel rewards = new() { Spacing = 7 };
            rewards.Children.Add(NewSmallNote(BuildRewardTriggerSummary(rewardRows.Select(row => row.Moby))));
            foreach (RewardTriggerEditorRow row in rewardRows)
                rewards.Children.Add(BuildRewardTriggerRow(row));
            body.Children.Add(BuildModernDisclosure("Reward gems", rewards, true));
        }

        if (chestRows.Count > 0)
        {
            StackPanel contents = new() { Spacing = 7 };
            contents.Children.Add(NewSmallNote(BuildGemSummary(chestRows.Select(row => row.Moby))));
            foreach (ChestContentEditorRow row in chestRows)
                contents.Children.Add(BuildChestContentRow(row));
            body.Children.Add(BuildModernDisclosure("Chest contents", contents, true));
        }

        if (transformBox != null && transformNote != null)
        {
            StackPanel replace = new() { Spacing = 7 };
            replace.Children.Add(transformBox);
            replace.Children.Add(transformNote);
            body.Children.Add(BuildModernDisclosure("Replace object", replace, false));
        }

        if (!_releaseMode)
        {
            Grid technical = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12
            };
            technical.Children.Add(BuildModernDialogField(
                "Render radius (+0x50)",
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        typeBox,
                        SectionLabelWithHelp("Render-radius reference", async () => await ShowMobyTypeStateHelpAsync("Render radius", typeBox, stateBox))
                    }
                }));
            Control state = BuildModernDialogField(
                "Was drawn (+0x51)",
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        stateBox,
                        SectionLabelWithHelp("Draw-state reference", async () => await ShowMobyTypeStateHelpAsync("Was drawn", typeBox, stateBox))
                    }
                });
            Grid.SetColumn(state, 1);
            technical.Children.Add(state);
            StackPanel technicalDetails = new() { Spacing = 8 };
            technicalDetails.Children.Add(technical);
            technicalDetails.Children.Add(NewSmallNote(
                $"Native class (+0x36/+0x37): 0x{moby.SourceByte37:X2}{moby.SourceByte36:X2}. " +
                $"Update distance (+0x52): 0x{moby.Flag4A:X2}; drop Moby/class (+0x53): 0x{moby.Flag4B:X2}; " +
                $"specular/metal type (+0x4F): 0x{moby.SourceByte4F:X2}. Native class drives identity; radius and draw state do not."));
            body.Children.Add(BuildModernDisclosure("Technical properties", technicalDetails, false));
        }
        else
        {
            StackPanel hiddenNativeFields = new()
            {
                IsVisible = false,
                Children = { typeBox, stateBox }
            };
            body.Children.Add(hiddenNativeFields);
        }

        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply changes");
        StyleModernSecondaryButton(cancel);
        StyleModernPrimaryButton(apply, ModernBlue, 124);
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            if (!TryValidateDirectGemEdit(moby, typeBox.Text, gemBox, editsRewardGem, transformBox, out string message) ||
                dragonColor != null && !dragonColor.Validate(out message))
            {
                validationMessage.Text = message;
                validationMessage.IsVisible = true;
                return;
            }

            validationMessage.Text = "";
            validationMessage.IsVisible = false;
            dialog.Close(true);
        };
        typeBox.TextChanged += (_, _) =>
        {
            validationMessage.Text = "";
            validationMessage.IsVisible = false;
        };
        return BuildModernDialogFrame(body, cancel, apply);
    }

    private Control BuildModernAddMobyDialogContent(
        Window dialog,
        ComboBox placementBox,
        TextBlock placementHint,
        ComboBox kindBox,
        ComboBox gemBox,
        TextBox nameBox,
        TextBox typeBox,
        TextBox stateBox,
        TextBox xBox,
        TextBox yBox,
        TextBox zBox,
        TextBox yawBox,
        TextBlock note)
    {
        StackPanel body = new()
        {
            Spacing = 14,
            Margin = new Thickness(20, 18, 20, 16)
        };
        body.Children.Add(_releaseMode
            ? BuildModernDialogHeader("Add a gem or copy an object", "Pick what to add, then click in the view where it should go.")
            : BuildModernDialogHeader("Add object", "Create a new level object."));
        Button chooseObjectButton = NewButton("");
        chooseObjectButton.Name = "ObjectGalleryButton";
        chooseObjectButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        chooseObjectButton.MinHeight = 44;
        StyleModernSecondaryButton(chooseObjectButton);
        void RefreshObjectGalleryButton()
        {
            AddMobyTemplate selected = kindBox.SelectedItem as AddMobyTemplate
                ?? (kindBox.ItemsSource as IEnumerable<AddMobyTemplate>)?.FirstOrDefault()
                ?? throw new InvalidOperationException("The Add Object gallery has no safe templates.");
            chooseObjectButton.Content = $"Choose Object — {BuildObjectGalleryDisplayName(selected)}";
        }
        chooseObjectButton.Click += async (_, _) =>
        {
            IReadOnlyList<AddMobyTemplate> templates =
                (kindBox.ItemsSource as IEnumerable<AddMobyTemplate>)?.ToArray()
                ?? Array.Empty<AddMobyTemplate>();
            AddMobyTemplate selected = kindBox.SelectedItem as AddMobyTemplate
                ?? templates.First();
            AddMobyTemplate? chosen = await ShowObjectGalleryAsync(dialog, templates, selected);
            if (chosen != null)
                kindBox.SelectedItem = chosen;
        };
        kindBox.SelectionChanged += (_, _) => RefreshObjectGalleryButton();
        RefreshObjectGalleryButton();
        body.Children.Add(BuildModernDialogField("Object", chooseObjectButton));
        Button nativeLibraryButton = NewButton("Browse all native source variants…");
        nativeLibraryButton.Name = "AddNativeMobyLibraryButton";
        StyleModernSecondaryButton(nativeLibraryButton);
        nativeLibraryButton.Click += async (_, _) =>
        {
            var templates = (kindBox.ItemsSource as IEnumerable<AddMobyTemplate>)?.ToArray() ?? [];
            var chosen = await ShowNativeMobyLibraryAsync(dialog, templates);
            if (chosen != null) kindBox.SelectedItem = chosen;
        };
        // The main window's Add object… is the one catalogue in shipping builds.
        nativeLibraryButton.IsVisible = !_releaseMode;
        body.Children.Add(nativeLibraryButton);
        Button universalButton = NewButton("Any object from any level…");
        universalButton.Name = "AddUniversalMobyLibraryButton";
        StyleModernPrimaryButton(universalButton, Color.FromRgb(124, 77, 181));
        universalButton.Click += async (_, _) =>
        {
            dialog.Close(false);
            await ShowUniversalMobyLibraryAsync(this);
        };
        universalButton.IsVisible = !_releaseMode;
        body.Children.Add(universalButton);

        Control gemField = BuildModernDialogField("Gem value", gemBox);
        gemField.IsVisible = (kindBox.SelectedItem as AddMobyTemplate)?.UsesGem == true;
        kindBox.SelectionChanged += (_, _) =>
        {
            gemField.IsVisible = (kindBox.SelectedItem as AddMobyTemplate)?.UsesGem == true;
        };
        body.Children.Add(gemField);
        body.Children.Add(BuildModernDialogField("Name", nameBox));
        body.Children.Add(BuildModernDialogField("Placement", BuildPlacementField(placementBox, placementHint)));
        body.Children.Add(BuildModernDialogField("Rotation", BuildMobyRotationFields(yawBox)));
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(237, 244, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 212, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = note
        });

        StackPanel exact = new() { Spacing = 10 };
        exact.Children.Add(BuildModernDialogField("Exact position", BuildMobyPositionFields(xBox, yBox, zBox)));
        if (!_releaseMode)
        {
            Grid native = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12
            };
            native.Children.Add(BuildModernDialogField("Render radius (+0x50)", typeBox));
            Control stateField = BuildModernDialogField("Was drawn (+0x51)", stateBox);
            Grid.SetColumn(stateField, 1);
            native.Children.Add(stateField);
            exact.Children.Add(native);
        }
        else
        {
            exact.Children.Add(new StackPanel
            {
                IsVisible = false,
                Children = { typeBox, stateBox }
            });
        }
        body.Children.Add(BuildModernDisclosure(
            _releaseMode ? "Exact position" : "Exact placement and technical properties",
            exact,
            false));

        Button cancel = NewButton("Cancel");
        Button add = NewButton("Add object");
        StyleModernSecondaryButton(cancel);
        StyleModernPrimaryButton(add, ModernGreen, 112);
        cancel.Click += (_, _) => dialog.Close(false);
        add.Click += (_, _) => dialog.Close(true);
        return BuildModernDialogFrame(body, cancel, add);
    }

    private static Control BuildModernDialogHeader(string title, string subtitle)
    {
        StackPanel panel = new() { Spacing = 3 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Foreground = new SolidColorBrush(ModernMutedInk),
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static Control BuildModernDialogField(string label, Control field)
    {
        StackPanel panel = new() { Spacing = 5 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        panel.Children.Add(field);
        return panel;
    }

    private static Control BuildModernDialogFrame(Control body, params Button[] buttons)
    {
        Grid root = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Background = Brushes.White
        };
        root.Children.Add(new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        StackPanel footerButtons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (Button button in buttons)
            footerButtons.Children.Add(button);
        Border footer = new()
        {
            Background = new SolidColorBrush(ModernSurface),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10),
            Child = footerButtons
        };
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }
}

internal sealed record EditorShellSessionSnapshot(
    LevelEditorExperience ActiveExperience,
    EditorWorkspace Workspace,
    LevelCatalog Catalog,
    LevelDefinition? CurrentLevel,
    GeometryCandidate? CurrentGeometry,
    IReadOnlyList<Moby> CurrentMobys,
    Moby? SelectedMoby,
    TerrainPolygon? SelectedTerrain,
    EditorViewport Viewport,
    int LevelLoadRequestId,
    string SavedMobyEditSignature,
    string SavedTerrainEditSignature,
    string CurrentMobyEditSignature,
    string CurrentTerrainEditSignature,
    string SavedNativeMovementEditSignature,
    string CurrentNativeMovementEditSignature,
    string SavedDragonRunToEditSignature,
    string CurrentDragonRunToEditSignature,
    bool HasUnsavedMobyEdits,
    bool HasUnsavedTerrainEdits,
    EditorViewportNavigationSnapshot Navigation);
