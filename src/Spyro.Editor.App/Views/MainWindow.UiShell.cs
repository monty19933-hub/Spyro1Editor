using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Scene;

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
    private Button? _modernViewportActionButton;
    private TabControl? _modernWorkspaceTabs;
    private TabItem? _modernTerrainWorkspaceTab;
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
                new RowDefinition(new GridLength(34))
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
                new ColumnDefinition(new GridLength(404))
            }
        };
        Grid.SetRow(body, 2);

        Grid.SetColumn(_viewport, 0);
        body.Children.Add(_viewport);

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
                    new ColumnDefinition(GridLength.Star)
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
                    _statusText
                }
            }
        };
        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(59, 68, 78));
        _statusText.FontSize = 12;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.TextWrapping = TextWrapping.NoWrap;
        _statusText.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_statusText, 1);
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
                new ColumnDefinition(new GridLength(242)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };
        topRow.Children.Add(BuildModernBrandTitle());

        StackPanel fileActions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button open = NewAsyncButton("Open BIN/CUE", ChooseSourceDiscImageAsync);
        StyleModernToolbarButton(open, 118);
        fileActions.Children.Add(open);

        _toolbarCreateBinButton = NewAsyncButton("Create BIN", CreateObjectTestBinAsync);
        StyleModernPrimaryButton(_toolbarCreateBinButton, ModernBlue, 110);
        fileActions.Children.Add(_toolbarCreateBinButton);

        Control more = BuildToolMenu(
            "More",
            new ToolbarAction("Project Data", ShowProjectDataAsync),
            new ToolbarAction("Check for Updates", ShowUpdatesAsync),
            new ToolbarAction("Build Safety", ShowBuildSafetyInspectorAsync),
            new ToolbarAction("Diagnostics", ShowDiagnosticsAsync),
            new ToolbarAction("Help", ShowHelpAsync));
        if (more is ComboBox moreBox)
        {
            moreBox.Width = 112;
            moreBox.MinHeight = 34;
            moreBox.Margin = new Thickness(0);
        }
        fileActions.Children.Add(more);
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
        _sourceDiscStatusText.MaxWidth = 320;
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
                "Fit every captured terrain face in the exhaustive top-down Edit Map.");
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

        List<TabItem> items =
        [
            NewModernWorkspaceTab("Objects", BuildModernObjectWorkspace())
        ];

        _modernTerrainWorkspaceTab = NewModernWorkspaceTab(
            "Terrain",
            _releaseMode ? BuildReleaseTerrainTextureWorkspace() : BuildTerrainTabControls());
        items.Add(_modernTerrainWorkspaceTab);

        items.Add(NewModernWorkspaceTab("Level", BuildModernLevelWorkspace()));
        items.Add(NewModernWorkspaceTab("Environment", BuildModernEnvironmentWorkspace()));
        if (!_releaseMode)
            items.Add(NewModernWorkspaceTab("Research", BuildModernResearchWorkspace()));

        _modernWorkspaceTabs = new TabControl
        {
            ItemsSource = items,
            SelectedIndex = 0,
            MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _modernWorkspaceTabs.SelectionChanged += (_, _) =>
        {
            _viewport.TerrainFocusMode = _modernTerrainWorkspaceTab != null &&
                ReferenceEquals(_modernWorkspaceTabs.SelectedItem, _modernTerrainWorkspaceTab);
        };
        _viewport.TerrainFocusMode = false;
        Grid.SetRow(_modernWorkspaceTabs, 1);
        layout.Children.Add(_modernWorkspaceTabs);

        shell.Child = layout;
        return shell;
    }

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

    private Control BuildModernObjectWorkspace()
    {
        StackPanel panel = new() { Spacing = 14 };
        panel.Children.Add(BuildModernSelectionSummary());
        panel.Children.Add(ModernDivider());
        panel.Children.Add(BuildModernPrimaryObjectControls());
        panel.Children.Add(ModernDivider());
        panel.Children.Add(BuildModernBuildSafetyPanel());
        panel.Children.Add(ModernDivider());
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
        panel.Children.Add(ModernSectionHeading("Edit object"));

        _objectAddButton = NewAsyncButton("Add", AddMobyNearSelectionAsync);
        _objectEditButton = NewAsyncButton("Edit", async () =>
        {
            if (_selectedMoby != null)
                await EditMobyAsync(_selectedMoby);
        });
        _objectSwapCatalogButton = NewAsyncButton("Replace", ShowCrossLevelSwapCatalogAsync);
        StyleModernPrimaryButton(_objectAddButton, ModernGreen);
        StyleModernPrimaryButton(_objectEditButton, ModernBlue);
        StyleModernPrimaryButton(_objectSwapCatalogButton, ModernTeal);

        Grid primary = NewEqualButtonGrid(3);
        AddModernGridButton(primary, _objectAddButton, 0);
        AddModernGridButton(primary, _objectEditButton, 1);
        AddModernGridButton(primary, _objectSwapCatalogButton, 2);
        panel.Children.Add(primary);

        _objectNativeMovementButton = NewAsyncButton("Edit Native Movement", EditSelectedNativeMovementAsync);
        StyleModernPrimaryButton(_objectNativeMovementButton, Color.FromRgb(111, 86, 174));
        _objectNativeMovementButton.IsVisible = false;
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
        panel.Children.Add(secondary);

        panel.Children.Add(BuildSelectedMobyZControls());

        _objectLayerDownButton = NewButton("Lower", () => MoveSelectedMobyToAdjacentTerrainLayer(-1));
        _objectLayerUpButton = NewButton("Upper", () => MoveSelectedMobyToAdjacentTerrainLayer(1));
        StyleModernSecondaryButton(_objectLayerDownButton);
        StyleModernSecondaryButton(_objectLayerUpButton);
        ToolTip.SetTip(_objectLayerDownButton, "Snap to the next terrain surface below at the same X/Y.");
        ToolTip.SetTip(_objectLayerUpButton, "Snap to the next terrain surface above at the same X/Y.");

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
            Text = "Terrain layer",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernMutedInk),
            VerticalAlignment = VerticalAlignment.Center
        });
        AddModernGridButton(layerRow, _objectLayerDownButton, 1);
        AddModernGridButton(layerRow, _objectLayerUpButton, 2);
        panel.Children.Add(layerRow);

        _objectSwapTestButton = NewAsyncButton("Create Swap Test", CreateObjectCandidateBinAsync);
        StyleModernPrimaryButton(_objectSwapTestButton, Color.FromRgb(176, 103, 20));
        _objectSwapTestButton.IsVisible = false;
        ToolTip.SetTip(_objectSwapCatalogButton, "Replace this existing chest or self-contained enemy slot from the swap catalogue.");
        ToolTip.SetTip(_objectSwapTestButton, "Create a disposable BIN/CUE for a staged cross-level swap.");
        panel.Children.Add(_objectSwapTestButton);

        _objectUndoRemoveButton = NewButton("Undo last removal", UndoLastRemovedMoby);
        _objectRestoreLevelButton = NewAsyncButton("Restore entire level", RestoreCurrentLevelAsync);
        StyleModernSecondaryButton(_objectUndoRemoveButton);
        StyleModernSecondaryButton(_objectRestoreLevelButton, ModernRed);
        _objectUndoRemoveButton.IsVisible = false;
        StackPanel recovery = new() { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        recovery.Children.Add(_objectUndoRemoveButton);
        recovery.Children.Add(_objectRestoreLevelButton);
        panel.Children.Add(new Expander
        {
            Header = "Recovery",
            IsExpanded = false,
            Content = recovery
        });

        _objectActionHint.TextWrapping = TextWrapping.Wrap;
        _objectActionHint.Foreground = new SolidColorBrush(Color.FromRgb(63, 75, 86));
        _objectActionHint.FontSize = 12;
        _objectActionHint.LineHeight = 17;
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(237, 244, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 212, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = _objectActionHint
        });

        RefreshActionAvailability();
        return panel;
    }

    private Control BuildModernMobyBrowser()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Object browser"));

        _mobyCategoryBox.ItemsSource = _releaseMode ? EverydayMobyCategoryOptions : MobyCategoryOptions;
        _mobyCategoryBox.SelectedIndex = 0;
        _mobyCategoryBox.MinHeight = 34;
        _mobyCategoryBox.PlaceholderText = "Category";
        _mobyCategoryBox.SelectionChanged += (_, _) => RefreshMobyList(_selectedMoby);

        _mobySearchBox.PlaceholderText = "Search objects";
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

        _mobyList.MinHeight = 240;
        _mobyList.MaxHeight = 420;
        _mobyList.Background = Brushes.White;
        _mobyList.BorderThickness = new Thickness(0);
        _mobyList.ItemTemplate = new FuncDataTemplate<Moby>((moby, _) => BuildMobyListItemControl(moby));
        _mobyList.SelectionChanged += (_, _) =>
        {
            if (_syncingMobyList || _mobyList.SelectedItem is not Moby moby)
                return;
            _viewport.SelectMoby(moby, true);
        };
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
        panel.Children.Add(BuildModernDisclosure("Level name", BuildLevelTextPanel(), true));
        panel.Children.Add(BuildModernDisclosure("Music", BuildLevelMusicPanel(), false));
        _homeworldTextGroup = BuildModernDisclosure("Portal location", BuildPortalControlPanel(), false);
        panel.Children.Add(_homeworldTextGroup);

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
        TextBlock? transformNote)
    {
        StackPanel body = new()
        {
            Spacing = 14,
            Margin = new Thickness(20, 18, 20, 16)
        };
        body.Children.Add(BuildModernDialogHeader(
            moby.DisplayLabel,
            $"T{moby.TrueIndex}  |  {BuildFriendlyMobySummary(moby)}"));
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

        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply changes");
        StyleModernSecondaryButton(cancel);
        StyleModernPrimaryButton(apply, ModernBlue, 124);
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            if (!TryValidateDirectGemEdit(moby, typeBox.Text, gemBox, editsRewardGem, transformBox, out string message))
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
        body.Children.Add(BuildModernDialogHeader("Add object", "Create a new level object."));
        body.Children.Add(BuildModernDialogField("Object", kindBox));

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
        body.Children.Add(BuildModernDisclosure("Exact placement and technical properties", exact, false));

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
